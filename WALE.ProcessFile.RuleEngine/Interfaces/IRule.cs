using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.RuleEngine.Interfaces;

/// <summary>
/// Interface for defining rules that can evaluate content and return a result
/// </summary>
/// <typeparam name="T">The type of result the rule returns</typeparam>
public interface IRule<out T>
{
    /// <summary>
    /// Gets the name of the rule for identification purposes
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// Gets the priority of the rule (higher numbers = higher priority)
    /// Rules with higher priority are evaluated first
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines if this rule applies to the given content
    /// </summary>
    /// <param name="content">The content to evaluate</param>
    /// <returns>True if the rule applies, false otherwise</returns>
    bool CanApply(MatchesResult content);

    /// <summary>
    /// Applies the rule to the content and returns the result
    /// </summary>
    /// <param name="content">The content to evaluate</param>
    /// <returns>The result of applying the rule</returns>
    T Apply(MatchesResult content);
}
