namespace DataMedix.Domain.Entities
{
    public class RangoPrescriba
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; }                 // NULL = global
        public Guid ParametroClinicoId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public decimal? ValorMinimo { get; set; }
        public decimal? ValorMaximo { get; set; }
        public string Accion { get; set; } = null!;         // AUMENTAR | MANTENER | REDUCIR | SUSPENDER
        public string? Medicamento { get; set; }            // EPO | HIERRO_IV | AMBOS
        public string? DosisSugerida { get; set; }
        public decimal? AjustePorcentaje { get; set; }
        public string? Observacion { get; set; }
        public int Orden { get; set; } = 99;
        public DateTime? VigenteDesdé { get; set; }
        public DateTime? VigenteHasta { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }

        public ParametroClinico ParametroClinico { get; set; } = null!;

        public bool AplicaParaValor(decimal valor)
        {
            var minOk = !ValorMinimo.HasValue || valor >= ValorMinimo.Value;
            var maxOk = !ValorMaximo.HasValue || valor < ValorMaximo.Value;
            return minOk && maxOk;
        }
    }

    public static class AccionPrescripcion
    {
        public const string Aumentar = "AUMENTAR";
        public const string Mantener = "MANTENER";
        public const string Reducir = "REDUCIR";
        public const string Suspender = "SUSPENDER";
    }

    public static class MedicamentoPrescripcion
    {
        public const string Epo = "EPO";
        public const string HierroIV = "HIERRO_IV";
        public const string Ambos = "AMBOS";
    }
}
