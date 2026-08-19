namespace DataMedix.Application.DTOs
{
    /// <summary>Ficha de un cliente (clínica, laboratorio o farmacia) del SaaS.</summary>
    public class TenantAdminDto
    {
        public Guid? Id { get; set; }
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = "";
        public string Subdomain { get; set; } = "";
        public string? Ruc { get; set; }
        public string? EmailContacto { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string? Ciudad { get; set; }
        public string? Pais { get; set; }
        public string? PlanNombre { get; set; }
        public bool Activo { get; set; } = true;

        public DateTime? CreatedAt { get; set; }

        // Métricas de operación, para saber si el cliente está usando el sistema
        public int Usuarios { get; set; }
        public int Pacientes { get; set; }
        public DateTime? UltimaActividad { get; set; }

        public bool EsNuevo => Id is null;
    }

    /// <summary>
    /// Usuario administrador inicial de un cliente nuevo.
    /// Sin él nadie puede entrar al tenant recién creado.
    /// </summary>
    public class AdminInicialDto
    {
        public string PrimerNombre { get; set; } = "";
        public string PrimerApellido { get; set; } = "";
        public string Identificacion { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
