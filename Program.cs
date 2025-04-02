using AppTran.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging.File;

System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

var builder = WebApplication.CreateBuilder(args);


var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
Directory.CreateDirectory(logPath);

// Get current date for log file name
var logFileName = $"{DateTime.Now:dd-MM-yyyy}_log.txt";
var logFilePath = Path.Combine(logPath, logFileName);

// Configure logging to write everything to the same file
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFile(logFilePath, outputTemplate: "{Timestamp:dd.MM.yyyy HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}");

// Security headers
builder.Services.AddAntiforgery(options => {
    options.HeaderName = "X-XSRF-TOKEN";
});

// Services
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.RemoveAll<ICertificateService>();
builder.Services.RemoveAll<IUserService>();
builder.Services.AddScoped<IUserService, PasswordUserService>();
//builder.Services.AddScoped<IUserService, CrossPlatformUserService>();
builder.Services.AddScoped<IApplicantService, ApplicantService>();


// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        // Add this line to prevent redirects for unauthorized requests
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Auth/Login"))
            {
                // Don't redirect if we're already on the login page
                context.Response.StatusCode = 200;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// Session
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) => {
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();