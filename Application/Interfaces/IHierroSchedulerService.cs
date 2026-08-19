namespace DataMedix.Application.Interfaces
{
    /// <summary>
    /// Distribuye aplicaciones de Hierro IV siguiendo estándares clínicos de hemodiálisis.
    /// Cada aplicación es de 100 mg fija. El total de aplicaciones = HierroMgMes / 100.
    /// Las fechas se distribuyen uniformemente con mínimo 48 h entre aplicaciones.
    /// </summary>
    public interface IHierroSchedulerService
    {
        /// <summary>
        /// Retorna las fechas de sesión donde se deben programar aplicaciones de 100 mg.
        /// </summary>
        List<DateTime> GenerarFechasAplicacion(decimal? hierroMgMes, IReadOnlyList<DateTime> sessionDates);
    }
}
