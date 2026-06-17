using DataMedix.Application.Interfaces;

namespace DataMedix.Infrastructure.Persistence
{
    /// <summary>
    /// Implementación scoped de ITenantContext.
    /// El middleware (Portal layer) llama a Set() una sola vez por request, antes de
    /// que cualquier handler o DbContext query sea ejecutado.
    /// DataMedixDbContext captura esta instancia por referencia para sus Global Query Filters.
    /// </summary>
    public sealed class TenantContext : ITenantContext
    {
        public Guid TenantId { get; private set; }
        public string? Subdomain { get; private set; }
        public bool IsResolved { get; private set; }

        /// <summary>
        /// Poblado por TenantContextMiddleware. Solo llamar una vez por request.
        /// No expuesto en la interfaz ITenantContext para que la capa de negocio no pueda mutar el contexto.
        /// </summary>
        public void Set(Guid tenantId, string? subdomain)
        {
            TenantId = tenantId;
            Subdomain = subdomain;
            IsResolved = true;
        }
    }
}
