using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IUnitOfWork
    {
        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();
        Task SaveChangesAsync();
        
    }
}
