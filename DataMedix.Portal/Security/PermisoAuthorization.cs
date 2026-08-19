using System.Security.Claims;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DataMedix.Portal.Security
{
    /// <summary>Exige un código de permiso concreto dentro del tenant actual.</summary>
    public sealed class PermisoRequirement : IAuthorizationRequirement
    {
        public string Codigo { get; }
        public PermisoRequirement(string codigo) => Codigo = codigo;
    }

    /// <summary>
    /// Resuelve el permiso contra la matriz del tenant al que pertenece el usuario.
    /// El tenant sale del claim de la cookie, no de un parámetro: así una página
    /// no puede pedir autorización "de otro tenant".
    /// </summary>
    public sealed class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
    {
        private readonly IPermisoService _permisos;

        public PermisoAuthorizationHandler(IPermisoService permisos) => _permisos = permisos;

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, PermisoRequirement requirement)
        {
            var user = context.User;
            if (user?.Identity is not { IsAuthenticated: true }) return;

            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            // El dueño del SaaS nunca queda fuera: si un tenant editara mal su
            // matriz, todavía debe poder entrar a arreglarla.
            if (roles.Any(r => string.Equals(r, "SUPERADMIN", StringComparison.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
                return;
            }

            if (!Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId) ||
                tenantId == Guid.Empty)
                return;

            if (await _permisos.TienePermisoAsync(tenantId, roles, requirement.Codigo))
                context.Succeed(requirement);
        }
    }

    /// <summary>
    /// Genera las policies "perm:&lt;codigo&gt;" bajo demanda.
    ///
    /// Los permisos viven en base de datos y se pueden agregar sin recompilar,
    /// así que no se pueden registrar una por una en el arranque.
    /// </summary>
    public sealed class PermisoPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;

        public PermisoPolicyProvider(IOptions<AuthorizationOptions> options)
            => _fallback = new DefaultAuthorizationPolicyProvider(options);

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (policyName.StartsWith(Permisos.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var codigo = policyName[Permisos.PolicyPrefix.Length..];
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermisoRequirement(codigo))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(policy);
            }

            return _fallback.GetPolicyAsync(policyName);
        }
    }
}
