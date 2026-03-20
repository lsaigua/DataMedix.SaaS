using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IIdentityGatewayClient
    {
        Task<bool> LoginAsync(string email, string password, Tenant tenant);
    }
}
