using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string Generate(Usuario user);
    }
}
