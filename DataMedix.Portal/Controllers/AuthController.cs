using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using DataMedix.Application.Interfaces;
using DataMedix.Application.DTOs;

namespace DataMedix.Portal.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IUsuarioRepository _usuarioRepo;

        public AuthController(IAuthService authService, IUsuarioRepository usuarioRepo)
        {
            _authService = authService;
            _usuarioRepo = usuarioRepo;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginRequest request)
        {
            var user = await _authService.ValidateUserAsync(request);

            if (user == null)
                return Redirect("/login?error=1");

            // Obtener el rol principal del usuario
            var roles = user.Roles
                .Where(r => r.Activo)
                .Select(r => r.Rol?.Nombre ?? "OPERADOR")
                .ToList();

            var rolPrincipal = roles.Contains("SUPERADMIN") ? "SUPERADMIN"
                : roles.Contains("ADMIN") ? "ADMIN"
                : roles.Contains("MEDICO") ? "MEDICO"
                : roles.Contains("OPERADOR") ? "OPERADOR"
                : "VISUALIZADOR";

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.NombreCompleto),
                new(ClaimTypes.Role, rolPrincipal),
                // tenant_id real del usuario (no hardcodeado)
                new("tenant_id", user.TenantId?.ToString() ?? Guid.Empty.ToString()),
                new("nombre_completo", user.NombreCompleto)
            };

            // Añadir todos los roles como claims adicionales
            foreach (var rol in roles)
                claims.Add(new Claim("rol", rol));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Actualizar último acceso
            await _usuarioRepo.UpdateUltimoAccesoAsync(user.Id);

            return Redirect("/dashboard");
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/login");
        }
    }
}
