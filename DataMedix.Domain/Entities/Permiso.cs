namespace DataMedix.Domain.Entities
{
    /// <summary>
    /// Catálogo GLOBAL de acciones y opciones de menú de la aplicación.
    /// No lleva tenant: el conjunto de funciones es el mismo para todas las
    /// clínicas. Lo configurable por tenant es la asignación (ver [RolPermiso]).
    /// </summary>
    public class Permiso
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }

        /// <summary>Agrupador para el menú y para la matriz de configuración.</summary>
        public string Grupo { get; set; } = null!;

        /// <summary>Ruta de la página. NULL = permiso de acción, no de menú.</summary>
        public string? Ruta { get; set; }

        public string? Icono { get; set; }
        public int Orden { get; set; }

        /// <summary>
        /// Permiso de plataforma (facturación consolidada, tarifas): pertenece al
        /// dueño del SaaS y nunca se ofrece en la matriz de un tenant.
        /// </summary>
        public bool SoloSuperadmin { get; set; }

        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool EsOpcionDeMenu => !string.IsNullOrWhiteSpace(Ruta);
    }

    /// <summary>
    /// Asignación de un permiso a un rol.
    /// TenantId NULL = valor por defecto de fábrica, aplicable a cualquier
    /// tenant que no haya definido el suyo. Un registro con TenantId concreto
    /// pisa al default para ese tenant y solo para él.
    /// </summary>
    public class RolPermiso
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; }
        public Guid RolId { get; set; }
        public string PermisoCodigo { get; set; } = null!;
        public bool Permitido { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }

        public Rol Rol { get; set; } = null!;
        public Permiso Permiso { get; set; } = null!;
    }

    /// <summary>Códigos de permiso usados desde el código C#.</summary>
    public static class Permisos
    {
        public const string DashboardVer          = "dashboard.ver";
        public const string PacientesVer          = "pacientes.ver";
        public const string PacientesEditar       = "pacientes.editar";
        public const string LaboratorioImportar   = "laboratorio.importar";
        public const string LaboratorioManual     = "laboratorio.manual";
        public const string LaboratorioHistorial  = "laboratorio.historial";
        public const string HistorialClinico      = "historial.clinico";
        public const string PrescripcionesVer     = "prescripciones.ver";
        public const string PrescripcionesAprobar = "prescripciones.aprobar";
        public const string CronogramaVer         = "cronograma.ver";
        public const string CronogramaEditar      = "cronograma.editar";
        public const string CronogramaPrecios     = "cronograma.precios";
        public const string HojaEpoVer            = "hojaepo.ver";
        public const string CalidadVer            = "calidad.ver";
        public const string ReportesVer           = "reportes.ver";
        public const string ReglasGestionar       = "reglas.gestionar";
        public const string RangosGestionar       = "rangos.gestionar";
        public const string CargasVer             = "cargas.ver";
        public const string UsuariosGestionar     = "usuarios.gestionar";
        public const string RolesGestionar        = "roles.gestionar";
        public const string ConfiguracionVer      = "configuracion.ver";
        public const string AuditoriaVer          = "auditoria.ver";
        public const string DepuracionEjecutar    = "depuracion.ejecutar";
        public const string FacturacionVer        = "facturacion.ver";
        public const string FacturacionCerrar     = "facturacion.cerrar";

        // Plataforma — dueño del SaaS
        public const string TarifasConfigurar     = "tarifas.configurar";
        public const string FacturacionGlobal     = "facturacion.global";

        /// <summary>Prefijo de las policies de autorización: [Authorize(Policy = "perm:xxx")].</summary>
        public const string PolicyPrefix = "perm:";

        public static string Policy(string codigo) => PolicyPrefix + codigo;
    }
}
