namespace DataMedix.Application.RuleEngine
{
    public interface IRuleEngine
    {
        Task<EvaluationResult> EvaluateAsync(EvaluationContext context);
    }
}
