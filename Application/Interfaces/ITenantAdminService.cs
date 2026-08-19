using DataMedix.Application.DTOs;

namespace DataMedix.Application.Interfaces
{
    /// <summary>
    /// Alta y mantenimiento de los clientes del SaaS (clínicas, laboratorios,
    /// farmacias). Es una operación de plataforma, protegida por el permiso
    /// "tenants.gestionar", que está marcado solo_superadmin.
    /// </summary>
    public interface ITenantAdminService
    {
        Task<List<TenantAdminDto>> ListarAsync(bool incluirInactivos = true);

        Task<TenantAdminDto?> ObtenerAsync(Guid tenantId);

        /// <summary>
        /// Crea el cliente junto con su usuario administrador inicial y las
        /// tarifas por defecto. Sin ese primer usuario nadie podría entrar al
        /// tenant, así que ambas cosas van en la misma operación.
        /// </summary>
        Task<Guid> CrearAsync(TenantAdminDto dto, AdminInicialDto admin, Guid usuarioId);

        Task ActualizarAsync(TenantAdminDto dto, Guid usuarioId);

        /// <summary>
        /// Activa o desactiva el cliente. Desactivar impide el acceso pero no
        /// borra nada: los datos clínicos y el histórico de cobro se conservan.
        /// </summary>
        Task CambiarEstadoAsync(Guid tenantId, bool activo, Guid usuarioId);

        /// <summary>Comprueba que el subdominio esté libre.</summary>
        Task<bool> SubdominioDisponibleAsync(string subdomain, Guid? excluirTenantId = null);
    }
}
