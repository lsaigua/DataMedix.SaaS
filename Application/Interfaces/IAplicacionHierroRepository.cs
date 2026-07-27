using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IAplicacionHierroRepository
    {
        Task BatchReplaceAsync(IEnumerable<Guid> cronogramaIds, List<AplicacionHierro> aplicaciones);
        Task DeleteByCronogramaIdsAsync(IEnumerable<Guid> cronogramaIds);
    }
}
