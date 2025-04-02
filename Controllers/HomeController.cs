using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using AppTran.Services;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Data.OracleClient;
using Dapper;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace AppTran.Controllers
{

    [Authorize]
    public class HomeController : Controller
    {
        private readonly IApplicantService _applicantService;
        private readonly ILogger<HomeController> _logger;
        private readonly string _connectionString;
        private readonly string _oracleConnectionString;
        private readonly TransferLogger _transferLogger;

        public HomeController(

        IApplicantService applicantService,
        ILogger<HomeController> logger,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor
    )
        {
            _applicantService = applicantService;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("MsSqlConnection");
            _oracleConnectionString = configuration.GetConnectionString("OracleConnection");
            _transferLogger = new TransferLogger(logger, httpContextAccessor);
        }


        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> SearchApplicants(string searchText)
        {
            var results = await _applicantService.SearchApplicantsAsync(searchText);
            return Json(results);
        }

        [HttpGet]
        public async Task<JsonResult> CheckRegNumber(string number)
        {
            var exists = await _applicantService.CheckRegNumberExistsAsync(number);
            return Json(exists);
        }

        [HttpPost]
        public async Task<IActionResult> TransferApplication(string applicationNumber, string applicantId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var nameQuery = "SELECT [Name] FROM Applicant WHERE Id = @ApplicantId";
                    var newOwnerName = await connection.QueryFirstAsync<string>(nameQuery, new { ApplicantId = new Guid(applicantId) });


                    var existsQuery = "SELECT COUNT(1) FROM Package WHERE RegNumber = @RegNumber";
                    var exists = await connection.QueryFirstAsync<int>(existsQuery, new { RegNumber = applicationNumber });

                    if (exists == 0)
                    {
                        await _transferLogger.LogNotFoundTransfer(applicationNumber, newOwnerName);
                        return Json(new
                        {
                            success = false,
                            error = "NotFound",
                            applicationNumber = applicationNumber,
                            message = $"{applicationNumber}: заявка не найдена"
                        });
                    }

                    var statusQuery = "SELECT Status FROM Package WHERE RegNumber = @RegNumber";
                    var status = await connection.QueryFirstAsync<string>(statusQuery, new { RegNumber = applicationNumber });

                    if (status == "8" || status == "10")
                    {
                        await _transferLogger.LogFailedTransfer(applicationNumber, newOwnerName);
                        return Json(new
                        {
                            success = false,
                            error = "ProcessingOver",
                            applicationNumber = applicationNumber,
                            message = $"{applicationNumber}: делопроизводство завершено, перевод невозможен"
                        });
                    }

                    string notifyEmail = null;

                    if (applicationNumber[4] == '7' || applicationNumber[4] == '8')
                    {
                        _logger.LogInformation($"Application {applicationNumber} has '7' or '8' in fifth position");

                        var emailQuery = "SELECT NotifyEmail FROM Package WHERE RegNumber = @RegNumber";
                        var packageEmail = await connection.QueryFirstOrDefaultAsync<string>(emailQuery,
                            new { RegNumber = applicationNumber });

                        _logger.LogInformation($"Package NotifyEmail for application {applicationNumber} is: {packageEmail ?? "null"}");

                        try
                        {

                            // Before opening Oracle connection
                            //_logger.LogInformation($"NLS_LANG: {Environment.GetEnvironmentVariable("NLS_LANG")}");
                            //_logger.LogInformation($"Oracle client path: {Environment.GetEnvironmentVariable("ORACLE_HOME")}");
                            //_logger.LogInformation($"TNS_ADMIN path: {Environment.GetEnvironmentVariable("TNS_ADMIN")}");


                            using (var oracleConnection = new OracleConnection(_oracleConnectionString))
                            {
                                await oracleConnection.OpenAsync();
                                using (var checkCommand = oracleConnection.CreateCommand())
                                {
                                    checkCommand.CommandText = @"
                               SELECT APP_CORR_EMAIL, CAST(OC_LETTER_TYPE AS VARCHAR2(10)) AS OC_LETTER_TYPE
                               FROM ROS_APPLICATION_S 
                               WHERE APPLICATION_NUMBER = :RegNumber";

                                    checkCommand.Parameters.Add(new OracleParameter(":RegNumber", applicationNumber));
                                    using (var reader = await checkCommand.ExecuteReaderAsync())
                                    {
                                        if (await reader.ReadAsync())
                                        {
                                            var oracleEmail = reader.IsDBNull(0) ? null : reader.GetString(0);
                                            var letterType = reader.IsDBNull(1) ? null : reader.GetString(1);

                                            if (string.IsNullOrEmpty(packageEmail) && string.IsNullOrEmpty(oracleEmail))
                                            {
                                                _logger.LogInformation("No email updates needed - both emails are empty");
                                                return Json(new { success = true });
                                            }

                                            var newEmailQuery = "SELECT Mail FROM Applicant WHERE Id = @ApplicantId";
                                            notifyEmail = await connection.QueryFirstOrDefaultAsync<string>(newEmailQuery,
                                                new { ApplicantId = new Guid(applicantId) });

                                            if (!string.IsNullOrEmpty(packageEmail) && !string.IsNullOrEmpty(oracleEmail))
                                            {
                                                _logger.LogInformation("Updating both Package and Oracle emails");
                                                using (var updateCommand = oracleConnection.CreateCommand())
                                                {
                                                    updateCommand.CommandText = @"
                                               UPDATE ROS_APPLICATION_S 
                                               SET APP_CORR_EMAIL = :NotifyEmail
                                               WHERE APPLICATION_NUMBER = :RegNumber
                                               AND OC_LETTER_TYPE = 4";

                                                    updateCommand.Parameters.Add(new OracleParameter(":NotifyEmail", notifyEmail));
                                                    updateCommand.Parameters.Add(new OracleParameter(":RegNumber", applicationNumber));

                                                    await updateCommand.ExecuteNonQueryAsync();
                                                }
                                            }
                                            else if (!string.IsNullOrEmpty(packageEmail) && string.IsNullOrEmpty(oracleEmail))
                                            {
                                                _logger.LogInformation("Updating Package email only");
                                            }
                                            else if (string.IsNullOrEmpty(packageEmail) && !string.IsNullOrEmpty(oracleEmail))
                                            {
                                                _logger.LogInformation("Updating Oracle email only");
                                                using (var updateCommand = oracleConnection.CreateCommand())
                                                {
                                                    updateCommand.CommandText = @"
                                               UPDATE ROS_APPLICATION_S 
                                               SET APP_CORR_EMAIL = :NotifyEmail
                                               WHERE APPLICATION_NUMBER = :RegNumber
                                               AND OC_LETTER_TYPE = 4";

                                                    updateCommand.Parameters.Add(new OracleParameter(":NotifyEmail", notifyEmail));
                                                    updateCommand.Parameters.Add(new OracleParameter(":RegNumber", applicationNumber));

                                                    await updateCommand.ExecuteNonQueryAsync();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception oracleEx)
                        {
                            _logger.LogError($"Oracle operation failed. Details:");
                            _logger.LogError($"Exception type: {oracleEx.GetType().FullName}");
                            _logger.LogError($"Message: {oracleEx.Message}");
                            _logger.LogError($"Stack trace: {oracleEx.StackTrace}");
                        }
                    }

                    var query = @"
               declare @ApplicantIdNew uniqueidentifier = @ApplicantId
               declare @t as table (regnumber varchar(80))
               declare @t2 as table (id uniqueidentifier)	
               
               insert into @t 
               select regnumber from Package p 
               where p.RegNumber = @RegNumber
               
               insert into @t2 
               select id from Package p 
               join @t t on t.regnumber = p.RegNumber
               
               UPDATE package 
               set applicantId = @ApplicantIdNew, 
                   applicant = (SELECT [name] FROM applicant WHERE id = @ApplicantIdNew),
                   NotifyEmail = CASE 
                       WHEN NotifyEmail IS NOT NULL THEN (select Mail from Applicant where Id = @ApplicantIdNew)
                       ELSE NULL 
                   END
               WHERE package.id in (select * from @t2) 
               and package.Status NOT IN ('8', '10')";

                    await connection.ExecuteAsync(query, new
                    {
                        ApplicantId = new Guid(applicantId),
                        RegNumber = applicationNumber
                    });

                    await _transferLogger.LogTransfer(applicationNumber, newOwnerName);

                    return Json(new
                    {
                        success = true,
                        applicationNumber = applicationNumber,
                        newOwnerName = newOwnerName
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error transferring application {applicationNumber}: {ex.Message}");
                return Json(new
                {
                    success = false,
                    error = "Error",
                    message = ex.Message
                });
            }
        }
    }

    // Updated TransferLogger class to use ILogger

    // Simplified TransferLogger that only uses ILogger
    public class TransferLogger
    {
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TransferLogger(ILogger logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetClientIpAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "Unknown";

            // Get local IP address
            string ip = context.Connection.LocalIpAddress?.ToString();

            if (string.IsNullOrEmpty(ip) || ip == "::1")
            {
                try
                {
                    // Get hostname's IP address
                    var hostName = System.Net.Dns.GetHostName();
                    var addresses = System.Net.Dns.GetHostAddresses(hostName);
                    ip = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "localhost";
                }
                catch
                {
                    ip = "localhost";
                }
            }

            return ip;
        }


        public Task LogTransfer(string applicationNumber, string recipientName)
            {
                var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Unknown";
                var clientIp = GetClientIpAddress();

                _logger.LogInformation(
                    "Заявка {ApplicationNumber} переведена в ЛК {RecipientName} пользователем {UserName}, IP:{ClientIp}",
                    applicationNumber, recipientName, userName, clientIp);

                return Task.CompletedTask;
            }

            public Task LogFailedTransfer(string applicationNumber, string recipientName)
            {
                var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Unknown";
                var clientIp = GetClientIpAddress();

                _logger.LogWarning(
                    "Делопроизводство по заявке {ApplicationNumber} завершено, заявка не переведена! Пользователь: {UserName}, IP:{ClientIp}",
                    applicationNumber, userName, clientIp);

                return Task.CompletedTask;
            }

            public Task LogNotFoundTransfer(string applicationNumber, string recipientName)
            {
                var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Unknown";
                var clientIp = GetClientIpAddress();

                _logger.LogWarning(
                    "Заявка {ApplicationNumber} не найдена в БД АРМ Регистратор. Пользователь: {UserName}, IP:{ClientIp}",
                    applicationNumber, userName, clientIp);

                return Task.CompletedTask;
            }
        }


    }
