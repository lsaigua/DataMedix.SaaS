using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Infrastructure.Repositories
{
    public class PacienteRepository : IPacienteRepository
    {
        private readonly DataMedixDbContext _context;

        public PacienteRepository(DataMedixDbContext context)
        {
            _context = context;
        }

        public async Task<Paciente?> GetByIdentificacionAsync(string identificacion)
        {
            return await _context.Pacientes
                .FirstOrDefaultAsync(x => x.Identificacion == identificacion);
        }

        public async Task AddAsync(Paciente paciente)
        {
            await _context.Pacientes.AddAsync(paciente);
        }
    }
}
