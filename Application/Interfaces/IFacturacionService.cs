using DataMedix.Application.DTOs;

namespace DataMedix.Application.Interfaces
{
    /// <summary>
    /// Facturación por período: base del plan + tarifa por paciente procesado.
    ///
    /// Un paciente cuenta para el mes si tuvo ACTIVIDAD en él (laboratorio,
    /// snapshot, prescripción o cronograma), no por su estado actual en el
    /// padrón. Al cerrar el período el detalle se congela, de modo que una baja
    /// o una depuración posterior no reescriben una factura ya emitida.
    /// </summary>
    public interface IFacturacionService
    {
        /// <summary>
        /// Obtiene el período. Si ya está cerrado devuelve el libro inmutable;
        /// si no, calcula la actividad en vivo sin persistir nada.
        /// </summary>
        Task<FacturacionPeriodoDto> ObtenerPeriodoAsync(Guid tenantId, int anio, int mes);

        /// <summary>
        /// Congela el período: persiste cabecera y detalle por paciente con copia
        /// de cédula y nombre. Idempotente — si ya estaba cerrado no lo altera.
        /// </summary>
        Task<FacturacionPeriodoDto> CerrarPeriodoAsync(Guid tenantId, int anio, int mes, Guid usuarioId);

        /// <summary>
        /// Reabre un período cerrado para permitir recalcularlo. Solo debería
        /// usarse ante un error de facturación, y queda registrado en auditoría.
        /// </summary>
        Task ReabrirPeriodoAsync(Guid tenantId, int anio, int mes, Guid usuarioId);

        /// <summary>Histórico de períodos con cierre registrado, más reciente primero.</summary>
        Task<List<FacturacionResumenDto>> ListarPeriodosAsync(Guid tenantId);
    }
}
