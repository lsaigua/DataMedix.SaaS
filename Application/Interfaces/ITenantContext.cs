namespace DataMedix.Application.Interfaces
{
    /// <summary>
    /// Contexto de tenant scoped por request HTTP.
    /// Poblado por TenantContextMiddleware antes de que llegue a cualquier handler.
    /// La lógica de negocio solo lee; la infraestructura escribe vía TenantContext (concrete).
    /// </summary>
    public interface ITenantContext
    {
        /// <summary>TenantId activo. Guid.Empty si el tenant no fue resuelto aún.</summary>
        Guid TenantId { get; }

        /// <summary>Subdominio extraído del host (ej. "lab01" de lab01.app.com).</summary>
        string? Subdomain { get; }

        /// <summary>
        /// True cuando el middleware pobló correctamente el contexto.
        /// False durante startup, background jobs o requests sin autenticación.
        /// Los Global Query Filters de EF Core lo usan para decidir si filtrar o no.
        /// </summary>
        bool IsResolved { get; }
    }
}
