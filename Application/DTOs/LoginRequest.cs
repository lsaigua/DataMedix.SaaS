using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DataMedix.Application.DTOs
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        public string? Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(5, ErrorMessage = "La contraseña debe tener al menos 5 caracteres.")]
        public string? Password { get; set; } = string.Empty;
    }
}
