using DataMedix.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IExcelReader
    {
        Task<List<LabRowDto>> ReadAsync(Stream fileStream);
    }
}
