using DataMedix.Application.Interfaces;
using DataMedix.Application.RuleEngine;
using DataMedix.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DataMedix.Infrastructure.Services
{
    /// <summary>
    /// Cache de reglas clínicas activas. Singleton: usa IServiceScopeFactory para acceder
    /// al DbContext (Scoped) sin crear una dependencia capturada.
    /// TTL de 30 minutos; se invalida explícitamente al guardar cambios en una regla.
    /// </summary>
    public sealed class RuleCache : IRuleCache
    {
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string CacheKey = "active_reglas_clinicas";

        public RuleCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        public async Task<IReadOnlyList<ReglaClinica>> GetActiveRulesAsync()
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<ReglaClinica>? cached))
                return cached!;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IReglaClinicaRepository>();
            var rules = await repo.GetActiveAsync();

            _cache.Set(CacheKey, rules, TimeSpan.FromMinutes(30));
            return rules;
        }

        public void Invalidate() => _cache.Remove(CacheKey);
    }
}
