// Controllers/AuthController.cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

public class AuthController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public IActionResult Name()
    {
        //return RedirectToAction("Login");
        return RedirectToAction("Index", "Home");
    }


    public IActionResult Login()
    {
        _logger.LogInformation($"Login page requested from IP: {HttpContext.Connection.RemoteIpAddress}");

        // If already authenticated, redirect to home
        if (User.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User already authenticated, redirecting to Home/Index");
            return RedirectToAction("Index", "Home");
        }

        _logger.LogInformation("Rendering login form");
        return View(new LoginViewModel { IsRedirected = false });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        _logger.LogInformation($"Login POST received for username: {model.Login}, IsRedirected: {model.IsRedirected}");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Log request details to help with debugging
        _logger.LogInformation($"Client IP: {HttpContext.Connection.RemoteIpAddress}");

        var user = _userService.AuthenticateUser(model.Login, model.Password);

        if (user == null)
        {
            _logger.LogWarning($"Authentication failed for {model.Login}");
            ModelState.AddModelError("", "Неверное имя пользователя или пароль");
            return View(model);
        }

        _logger.LogInformation($"Authentication successful - Login: {model.Login}, Full Name: {user.FullName}, Role: {user.Role}");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("Login", user.Login)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied()
    {
        _logger.LogWarning($"Access denied for request from IP: {HttpContext.Connection.RemoteIpAddress}");
        return View();
    }
}

public class LoginViewModel
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}