using DataMedix.Application.DTOs;

namespace DataMedix.Application.Interfaces
{
    /// <summary>
    /// Consola comercial del dueño del SaaS: define el modelo de cobro de cada
    /// cliente y consulta el consolidado de lo que hay que facturar cada mes.
    ///
    /// Es el único servicio que atraviesa tenants a propósito, por eso sus
    /// operaciones están detrás del permiso de plataforma "tarifas.configurar".
    /// </summary>
    public interface ITarifaService
    {
        /// <summary>Configuración comercial de todos los clientes.</summary>
        Task<List<TarifaTenantDto>> ListarTenantsAsync();

        Task<TarifaTenantDto?> ObtenerAsync(Guid tenantId);

        Task GuardarAsync(TarifaTenantDto dto, Guid usuarioId);

        /// <summary>Cargos puntuales (implementación, migración) de un cliente.</summary>
        Task<List<CargoUnicoDto>> ListarCargosAsync(Guid tenantId);

        Task AgregarCargoAsync(Guid tenantId, CargoUnicoDto cargo, Guid usuarioId);

        Task EliminarCargoAsync(Guid tenantId, Guid cargoId, Guid usuarioId);

        /// <summary>
        /// Consolidado del período para todos los clientes: lo que hay que
        /// cobrar este mes. Los períodos cerrados se leen del libro; los
        /// abiertos se calculan en vivo.
        /// </summary>
        Task<List<FacturacionGlobalDto>> ConsolidadoAsync(int anio, int mes);
    }
}
