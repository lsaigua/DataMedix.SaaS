using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IPacienteRepository
    {
        Task<Paciente?> GetByIdentificacionAsync(string identificacion);
        Task AddAsync(Paciente paciente);
    }
}
