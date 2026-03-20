using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DataMedix.Domain.Entities
{
    [Table("resultadolaboratorio")]
    public class ResultadoLaboratorio
    {
        public Guid IdResultadoLaboratorio { get; private set; }
        public Guid IdEmpresa { get; private set; }
        public Guid IdOrdenClinica { get; private set; }
        public Guid IdParametroLaboratorio { get; private set; }

        public string Examen { get; private set; }
        public string Resultado { get; private set; }
        public string ResultadoMedico { get; private set; }
        public string ObservacionTecnica { get; private set; }
        public string UsuarioValida { get; private set; }
        public double? ValorNumerico { get; private set; }

        public bool Flapatologico { get; private set; }
        public string UnidadMedida { get; private set; }
        public double? ValorMinimo { get; private set; }
        public double? ValorMaximo { get; private set; }
        public string EstadoResultado { get; private set; }
        public DateTime FechaCreacion { get; private set; }

        private ResultadoLaboratorio() { }

        public ResultadoLaboratorio(
            Guid idEmpresa,
            Guid idOrden,
            Guid idParametro,
            string examen,
            string resultadoTexto,
            double? valorNumerico,
            double? min,
            double? max)
        {
            IdResultadoLaboratorio = Guid.NewGuid();
            IdEmpresa = idEmpresa;
            IdOrdenClinica = idOrden;
            IdParametroLaboratorio = idParametro;
            Examen = examen;
            Resultado = resultadoTexto;
            ValorNumerico = valorNumerico;
            ValorMinimo = min;
            ValorMaximo = max;

            Flapatologico = CalcularPatologia();
        }

        private bool CalcularPatologia()
        {
            if (!ValorNumerico.HasValue) return false;
            if (!ValorMinimo.HasValue || !ValorMaximo.HasValue) return false;

            return ValorNumerico < ValorMinimo ||
                   ValorNumerico > ValorMaximo;
        }
    }
}
