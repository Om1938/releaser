namespace Releaser.Domain.Common;

/// <summary>Raised when an operation would break a business rule. Maps to HTTP 409/422 at the boundary.</summary>
public sealed class DomainRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
