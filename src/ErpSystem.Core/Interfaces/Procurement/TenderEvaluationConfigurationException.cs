namespace ErpSystem.Core.Interfaces.Procurement;

public sealed class TenderEvaluationConfigurationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
