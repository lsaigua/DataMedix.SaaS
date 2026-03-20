using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Infrastructure.Repositories
{
    public class ParametroRepository : IParametroRepository
    {
        private readonly DataMedixDbContext _context;

        public ParametroRepository(DataMedixDbContext context)
        {
            _context = context;
        }

        public async Task<ParametroLaboratorio?> GetByNombreAsync(Guid empresaId, string nombre)
        {
            return await _context.ParametroLaboratorios
                .FirstOrDefaultAsync(x =>
                    x.IdEmpresa == empresaId &&
                    x.Nombre == nombre);
        }
    }
}
