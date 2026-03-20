using DataMedix.Application.Interfaces;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DataMedixDbContext _context;
        private IDbContextTransaction _transaction;

        public UnitOfWork(DataMedixDbContext context)
        {
            _context = context;
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitAsync()
        {
            await _context.SaveChangesAsync();

            if (_transaction != null)
                await _transaction.CommitAsync();
        }

        public async Task RollbackAsync()
        {
            if (_transaction != null)
                await _transaction.RollbackAsync();
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();

            if (_transaction != null)
                await _transaction.CommitAsync();
        }
    }
}
