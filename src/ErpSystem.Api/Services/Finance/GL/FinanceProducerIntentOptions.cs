namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinanceProducerIntentOptions
{
    public const string SectionName = "Finance:ProducerIntents";
    // Owner cutovers are enabled only after their adapter and operational packet are independently approved.
    public bool Enabled { get; set; }
}
