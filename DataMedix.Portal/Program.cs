using DataMedix.Application;
using DataMedix.Infrastructure;
using DataMedix.Portal.Components;
using DataMedix.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ── VALIDAR CONFIG CRÍTICA ─────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DatabasePostgres");
if (string.IsNullOrWhiteSpace(connStr))
    throw new InvalidOperationException(
        "ConnectionStrings:DatabasePostgres está vacío. " +
        "Local: verifica ASPNETCORE_ENVIRONMENT=Development y appsettings.Development.json. " +
        "Railway: define ConnectionStrings__DatabasePostgres como variable de entorno.");

// ── FORWARDED HEADERS (Railway / reverse proxy) ────────────────────────────────
// Sin esto: HTTPS no detectado, redirect loops, cookies inseguras detrás del proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear(); // confiar en cualquier proxy (Railway cambia IPs)
    options.KnownProxies.Clear();
});

// ── CAPAS DE APLICACIÓN E INFRAESTRUCTURA ─────────────────────────────────────
builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddApplication();

// ── SERVICIOS PORTAL ──────────────────────────────────────────────────────────
builder.Services.AddScoped<GlobalLoadingService>();
builder.Services.AddScoped<GlobalToastService>();

// ── BLAZOR + MVC ──────────────────────────────────────────────────────────────
builder.Services
    .AddRazorComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // Sube el límite del hub SignalR a 50 MB para permitir upload de Excel grandes
        options.MaximumReceiveMessageSize = 50 * 1024 * 1024;
    });
builder.Services.AddControllers();

// ── AUTENTICACIÓN (Cookie-based) ───────────────────────────────────────────────
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

// ── LOGGING ────────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsProduction())
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

// ── BUILD ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Crear tabla data_protection_keys si no existe (primera vez / nuevos entornos)
await app.Services.EnsureDataProtectionTableAsync();

// PRIMERO: leer los headers del proxy antes de cualquier redirect/auth
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html; charset=utf-8";
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var msg = WebUtility.HtmlEncode(feature?.Error?.Message ?? "Error interno del servidor");
            var reqId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
            await context.Response.WriteAsync(
                $"<html><body style='font-family:system-ui;padding:2rem;max-width:600px'>" +
                $"<h2 style='color:#dc2626'>Error del servidor</h2>" +
                $"<p>{msg}</p>" +
                $"<p style='color:#9ca3af;font-size:0.8rem'>Request ID: {reqId}</p>" +
                $"</body></html>");
        });
    });
    app.UseHsts();
}

// UseHttpsRedirection es seguro aquí porque UseForwardedHeaders ya corrigió el scheme
app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Log 404s con el path para facilitar diagnóstico en Railway Deploy Logs
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == 404)
    {
        var log = context.RequestServices.GetRequiredService<ILogger<Program>>();
        log.LogWarning("404 {Method} {Path} | WebRoot={WebRoot}",
            context.Request.Method,
            context.Request.Path,
            context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath);
    }
});

// MapStaticAssets reemplaza UseStaticFiles en .NET 9/10 para Blazor Web Apps.
// Sirve wwwroot/ físico + _framework/blazor.web.js y demás static web assets del framework.
app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
