using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.RuleEngine.Interfaces;

/// <summary>
/// Interface for a rule engine that can evaluate content against multiple rules
/// </summary>
/// <typeparam name="T">The type of result the rules return</typeparam>
public interface IRuleEngine<T>
{
    /// <summary>
    /// Adds a rule to the engine
    /// </summary>
    /// <param name="rule">The rule to add</param>
    void AddRule(IRule<T> rule);

    /// <summary>
    /// Sets the region for the rule engine.
    /// </summary>
    /// <param name="region">The region for teh rule</param>
    void SetRegion(string region);

    /// <summary>
    /// Evaluates content against all rules and returns the result from the first applicable rule
    /// Rules are evaluated in priority order (highest priority first)
    /// </summary>
    /// <param name="content">The content to evaluate</param>
    /// <returns>The result from the first applicable rule, or default(T) if no rules apply</returns>
    T? Evaluate(MatchesResult content);

    /// <summary>
    /// Evaluates content against all rules and returns results from all applicable rules
    /// </summary>
    /// <param name="content">The content to evaluate</param>
    /// <returns>A collection of results from all applicable rules</returns>
    IEnumerable<T> EvaluateAll(MatchesResult content);

    /// <summary>
    /// Gets all registered rules
    /// </summary>
    /// <returns>A collection of all registered rules</returns>
    IEnumerable<IRule<T>> GetRules();
}
