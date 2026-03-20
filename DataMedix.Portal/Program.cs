using DataMedix.Application;
using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Application.Services;
using DataMedix.Application.UseCases.Auth;
using DataMedix.Application.UseCases.Laboratorio;
using DataMedix.Infrastructure;
using DataMedix.Infrastructure.Excel;
using DataMedix.Infrastructure.Persistence;
using DataMedix.Infrastructure.Repositories;
using DataMedix.Infrastructure.Security;
using DataMedix.Portal;
using DataMedix.Portal.Components;
using DataMedix.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<DataMedixDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DatabasePostgres")));

builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IExcelReader, ExcelReader>();
builder.Services.AddScoped<IPacienteRepository, PacienteRepository>();
builder.Services.AddScoped<IOrdenClinicaRepository, OrdenClinicaRepository>();
builder.Services.AddScoped<IParametroRepository, ParametroRepository>();
builder.Services.AddScoped<IResultadoRepository, ResultadoRepository>();
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddScoped<ProcesarArchivoLaboratorioUseCase>();
builder.Services.AddScoped<GlobalLoadingService>();
builder.Services.AddScoped<GlobalToastService>();

builder.Services.AddRazorComponents(options =>
{
    options.DetailedErrors = true;
})
.AddInteractiveServerComponents();

builder.Services.AddControllers();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.LogoutPath = "/auth/logout";

        options.Cookie.Name = "DataMedix.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddScoped<IAuthService, LoginUseCase>();
//builder.Services.AddAntiforgery();

var app = builder.Build();
/*
app.MapPost("/auth/login", async (
    LoginRequest request,
    IAuthService authService,
    IHttpContextAccessor httpContextAccessor) =>
{
    var user = await authService.ValidateUserAsync(request);

    if (user == null)
        return Results.Unauthorized();

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.IdUsuario.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim("empresa_id","d8e445fb-b5fb-4416-a39d-b8f00cb10b41".ToString())
    };

    var identity = new ClaimsIdentity(claims, "Cookies");
    var principal = new ClaimsPrincipal(identity);

    await httpContextAccessor.HttpContext!
        .SignInAsync("Cookies", principal);

    return Results.Ok();
});*/

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();     // NECESARIO PARA LOGIN


app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();