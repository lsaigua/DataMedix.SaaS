using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
   
        public interface ITenantResolver
        {
            Task<Tenant?> ResolveAsync(string host);
        }
    
}
