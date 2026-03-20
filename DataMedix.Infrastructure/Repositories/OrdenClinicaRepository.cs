using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Infrastructure.Repositories
{
    public class OrdenClinicaRepository : IOrdenClinicaRepository
    {
        private readonly DataMedixDbContext _context;

        public OrdenClinicaRepository(DataMedixDbContext context)
        {
            _context = context;
        }

        public async Task<OrdenClinica?> GetByPacienteYFechaAsync(Guid pacienteId, DateTime fecha)
        {
            return await _context.OrdenesClinicas
                .FirstOrDefaultAsync(x =>
                    x.IdPaciente == pacienteId &&
                    x.FechaOrden.Date == fecha.Date);
        }

        public async Task AddAsync(OrdenClinica orden)
        {
            await _context.OrdenesClinicas.AddAsync(orden);
        }
    }
}
