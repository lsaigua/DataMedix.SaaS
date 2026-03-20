using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IOrdenClinicaRepository
    {
        Task<OrdenClinica?> GetByPacienteYFechaAsync(Guid pacienteId, DateTime fecha);
        Task AddAsync(OrdenClinica orden);
    }
 
}
