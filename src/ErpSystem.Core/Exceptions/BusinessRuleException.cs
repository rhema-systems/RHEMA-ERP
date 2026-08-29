namespace ErpSystem.Core.Exceptions;

/// <summary>
/// A safe, user-facing business-rule rejection. Services should use this for
/// expected validation, state, and not-found failures instead of returning
/// controller-specific strings or leaking infrastructure exceptions.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message, int statusCode = 422)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A stable business-rule code is required.", nameof(code));
        }

        Code = code.Trim();
        StatusCode = statusCode is >= 400 and <= 499 ? statusCode : 422;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
