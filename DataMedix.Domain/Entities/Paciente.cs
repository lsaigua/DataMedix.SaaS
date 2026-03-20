using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DataMedix.Domain.Entities
{
    [Table("paciente")]
    public class Paciente
    {
        public Guid IdPaciente { get; set; }

        public string CodigoPaciente { get; set; } = null!;
        public string Identificacion { get; set; } = null!;

        public string PrimerNombre { get; set; } = null!;
        public string? SegundoNombre { get; set; }   // NULLABLE

        public string PrimerApellido { get; set; } = null!;
        public string SegundoApellido { get; set; } = null!;

        public string? Genero { get; set; }            //NULLABLE
        public DateTime FechaNacimiento { get; set; }         // NULLABLE

        public string Email { get; set; } = null!;
        public string Telefono { get; set; } = null!;

        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; }
        public string NombreCompleto => $"{PrimerNombre} {SegundoNombre} {PrimerApellido} {SegundoApellido}".Trim();
        private Paciente() { }

        public Paciente(string identificacion, string nombre)
        {
            IdPaciente = Guid.NewGuid();
            Identificacion = identificacion;
           // NombreCompleto = nombre;
            Activo = true;
        }
    }
}
