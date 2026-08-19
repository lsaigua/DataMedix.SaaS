using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    /// <summary>
    /// Resuelve qué puede hacer cada rol dentro de un tenant.
    ///
    /// La asignación tiene dos niveles: los registros con TenantId NULL son el
    /// valor de fábrica y aplican a cualquier clínica; si un tenant define su
    /// propia fila para (rol, permiso), esa pisa al default solo para él.
    /// </summary>
    public interface IPermisoService
    {
        /// <summary>Catálogo completo de permisos, ordenado por grupo y orden.</summary>
        Task<List<Permiso>> GetCatalogoAsync();

        /// <summary>
        /// Códigos de permiso efectivos para los roles indicados dentro del tenant.
        /// Un permiso se concede si CUALQUIERA de los roles lo tiene.
        /// </summary>
        Task<HashSet<string>> GetPermisosEfectivosAsync(Guid tenantId, IEnumerable<string> roles);

        /// <summary>Comprueba un permiso concreto.</summary>
        Task<bool> TienePermisoAsync(Guid tenantId, IEnumerable<string> roles, string permisoCodigo);

        /// <summary>
        /// Matriz editable del tenant: por cada rol, los códigos que tiene
        /// concedidos, ya resueltos entre default y override del tenant.
        /// </summary>
        Task<Dictionary<Guid, HashSet<string>>> GetMatrizAsync(Guid tenantId);

        /// <summary>Roles asignables dentro del tenant.</summary>
        Task<List<Rol>> GetRolesAsync(bool incluirGlobales = false);

        /// <summary>
        /// Guarda la matriz del tenant. Escribe overrides propios sin tocar los
        /// defaults de fábrica, de modo que otros tenants no se ven afectados.
        /// </summary>
        Task GuardarMatrizAsync(Guid tenantId, Dictionary<Guid, HashSet<string>> matriz, Guid usuarioId);

        /// <summary>
        /// Devuelve el tenant a los valores de fábrica eliminando sus overrides.
        /// </summary>
        Task RestaurarPorDefectoAsync(Guid tenantId, Guid usuarioId);

        /// <summary>Invalida la caché de permisos del tenant.</summary>
        void InvalidarCache(Guid tenantId);
    }
}
