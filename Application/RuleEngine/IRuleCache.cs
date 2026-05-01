using DataMedix.Domain.Entities;

namespace DataMedix.Application.RuleEngine
{
    /// <summary>
    /// Cache en memoria de las reglas clínicas activas.
    /// Se invalida cuando se modifica una regla para forzar recarga desde BD.
    /// </summary>
    public interface IRuleCache
    {
        Task<IReadOnlyList<ReglaClinica>> GetActiveRulesAsync();
        void Invalidate();
    }
}
