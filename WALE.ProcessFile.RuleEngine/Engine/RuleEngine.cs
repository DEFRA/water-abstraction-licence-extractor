using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;

namespace WALE.ProcessFile.RuleEngine.Engine;

/// <summary>
/// A generic rule engine implementation
/// </summary>
/// <typeparam name="T">The type of result the rules return</typeparam>
public class RuleEngine<T> : IRuleEngine<T>
{
    private readonly List<IRule<T>> _rules = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void AddRule(IRule<T> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        lock (_lock)
        {
            // Remove any existing rule with the same name
            _rules.RemoveAll(r => r.RuleName.Equals(rule.RuleName, StringComparison.OrdinalIgnoreCase));

            // Add the new rule
            _rules.Add(rule);

            // Sort by priority (highest first)
            _rules.Sort((r1, r2) => r2.Priority.CompareTo(r1.Priority));
        }
    }

    /// <inheritdoc />
    public void SetRegion(string region)
    {
        lock (_lock)
        {
            foreach (var rule in _rules)
            {
                rule.Region = region;
            }
        }
    }

    /// <inheritdoc />
    public T? Evaluate(MatchesResult content)
    {
        ArgumentNullException.ThrowIfNull(content);

        lock (_lock)
        {
            foreach (var rule in _rules.OrderBy(r => r.Priority))
            {
                if (rule.CanApply(content))
                {
                    return rule.Apply(content);
                }
            }
        }

        return default(T);
    }

    /// <inheritdoc />
    public IEnumerable<T> EvaluateAll(MatchesResult content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var results = new List<T>();

        lock (_lock)
        {
            foreach (var rule in _rules)
            {
                if (rule.CanApply(content))
                {
                    var result = rule.Apply(content);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
            }
        }

        return results;
    }

    /// <inheritdoc />
    public IEnumerable<IRule<T>> GetRules()
    {
        lock (_lock)
        {
            return _rules.ToList(); // Return a copy to prevent external modification
        }
    }
}
