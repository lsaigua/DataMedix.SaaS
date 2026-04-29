using DataMedix.Application;
using DataMedix.Infrastructure;
using DataMedix.Portal.Components;
using DataMedix.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────────────────────
// CAPAS DE APLICACIÓN E INFRAESTRUCTURA
// ──────────────────────────────────────────────────────────────────────────────
builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddApplication();

// ──────────────────────────────────────────────────────────────────────────────
// SERVICIOS PORTAL
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<GlobalLoadingService>();
builder.Services.AddScoped<GlobalToastService>();

// ──────────────────────────────────────────────────────────────────────────────
// BLAZOR + MVC
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    .AddInteractiveServerComponents();
builder.Services.AddControllers();

// ──────────────────────────────────────────────────────────────────────────────
// AUTENTICACIÓN (Cookie-based)
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/acceso-denegado";
        options.LogoutPath = "/auth/logout";
        options.Cookie.Name = "DataMedix.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// ──────────────────────────────────────────────────────────────────────────────
// LOGGING
// ──────────────────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsProduction())
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

// ──────────────────────────────────────────────────────────────────────────────
// BUILD
// ──────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
