
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using EFCore.BulkExtensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Infrastructure.Repositories
{
    public class ResultadoRepository : IResultadoRepository
    {
        private readonly DataMedixDbContext _context;

        public ResultadoRepository(DataMedixDbContext context)
        {
            _context = context;
        }

        public async Task BulkInsertAsync(List<ResultadoLaboratorio> resultados)
        {
            await _context.ResultadosLaboratorio.AddRangeAsync(resultados);
        }
    }
}
