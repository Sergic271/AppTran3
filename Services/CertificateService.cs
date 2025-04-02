// Services/CertificateService.cs

using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

public class CertificateService : ICertificateService
{
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(ILogger<CertificateService> logger)
    {
        _logger = logger;
    }

    public async Task<List<CertificateInfo>> GetAvailableCertificates()
    {
        var certificates = new List<CertificateInfo>();

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            foreach (var cert in store.Certificates)
            {
                if (IsValidCertificate(cert))
                {
                    var certInfo = new CertificateInfo
                    {
                        Certificate = cert,
                        Thumbprint = cert.Thumbprint,
                        SubjectName = cert.SubjectName.Name,
                        INN = GetInnFromCertificate(cert),
                        Issuer = GetIssuerOrganization(cert),
                        NotBefore = cert.NotBefore,
                        NotAfter = cert.NotAfter
                    };

                    _logger.LogInformation($"Created CertificateInfo: SubjectName={certInfo.SubjectName}, INN={certInfo.INN}, Issuer={certInfo.Issuer}");
                    certificates.Add(certInfo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available certificates");
            throw;
        }

        return certificates;
    }





    private bool IsPersonalCertificate(X509Certificate2 cert)
    {
        try
        {
            var subject = cert.Subject;
            // Less strict filtering - only check for CN (Common Name)
            return subject.Contains("CN=");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if certificate is personal");
            return false;
        }
    }




    private bool IsValidCertificate(X509Certificate2 cert)
    {
        try
        {
            // Check expiration
            if (DateTime.Now < cert.NotBefore || DateTime.Now > cert.NotAfter)
            {
                _logger.LogInformation($"Certificate {cert.Thumbprint} is expired or not yet valid");
                return false;
            }

            // Check for SNILS/—Õ»À— in both Latin and Cyrillic
            var subject = cert.Subject;
            var hasSnils = subject.Contains("SNILS=", StringComparison.OrdinalIgnoreCase) ||
                          subject.Contains("—Õ»À—=", StringComparison.OrdinalIgnoreCase);
            var hasRequiredOid = false;

            try
            {
                foreach (var extension in cert.Extensions)
                {
                    if (extension.Oid.Value == "1.2.643.100.3")
                    {
                        hasRequiredOid = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking certificate extensions");
            }

            if (!hasSnils && !hasRequiredOid)
            {
                _logger.LogInformation($"Certificate {cert.Thumbprint} does not have required attributes");
                return false;
            }

            _logger.LogInformation($"Certificate validated: {cert.Thumbprint}, has SNILS/—Õ»À—: {hasSnils}, has OID: {hasRequiredOid}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating certificate {Subject}", cert.Subject);
            return false;
        }
    }

    public Task<string> GetCertificateFingerprint(X509Certificate2 certificate)
    {
        return Task.FromResult(certificate.Thumbprint);
    }

    public Task<string> GetUserFullName(X509Certificate2 certificate)
    {
        var subjectName = certificate.SubjectName.Name;
        var cn = subjectName.Split(',')
            .FirstOrDefault(x => x.TrimStart().StartsWith("CN="))
            ?.Split('=')[1];

        return Task.FromResult(cn ?? "Unknown");
    }

    private string GetInnFromCertificate(X509Certificate2 cert)
    {
        try
        {
            var subject = cert.Subject;
            _logger.LogInformation($"Looking for INN in subject: {subject}"); // Debug log

            // First try »ÕÕ/INN in subject
            var subjectParts = subject.Split(',').Select(x => x.Trim());
            var innMatch = subjectParts.FirstOrDefault(x =>
                x.StartsWith("INN=", StringComparison.OrdinalIgnoreCase) ||
                x.StartsWith("»ÕÕ=", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(innMatch))
            {
                var inn = innMatch.Split('=')[1].Trim();
                _logger.LogInformation($"Found INN in subject: {inn}");
                return inn;
            }

            // Try OID 1.2.643.3.131.1.1
            _logger.LogInformation("Looking for INN in extensions");
            foreach (var extension in cert.Extensions)
            {
                _logger.LogInformation($"Checking extension: {extension.Oid?.Value}");
                if (extension.Oid?.Value == "1.2.643.3.131.1.1")
                {
                    var inn = extension.Format(false);
                    _logger.LogInformation($"Found INN in extension: {inn}");
                    return inn;
                }
            }

            _logger.LogInformation("No INN found");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting INN from certificate");
            return string.Empty;
        }
    }

    private string GetIssuerOrganization(X509Certificate2 cert)
    {
        try
        {
            var issuer = cert.Issuer;
            _logger.LogInformation($"Looking for Organization in issuer: {issuer}");

            var issuerParts = issuer.Split(',').Select(x => x.Trim());
            var orgMatch = issuerParts.FirstOrDefault(x => x.StartsWith("O=", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(orgMatch))
            {
                var org = orgMatch.Split('=')[1].Trim();
                _logger.LogInformation($"Found Organization: {org}");
                return org;
            }

            _logger.LogInformation("No Organization found in issuer");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issuer organization from certificate");
            return string.Empty;
        }
    }


}