namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinanceProducerIntentGroupOptions
{
    public const string SectionName = "Finance:ProducerIntentGroups";
    // Group execution remains opt-in after C8 schema and each owner adapter pass independent review.
    public bool Enabled { get; set; }
}
