namespace ErpSystem.Api.Services.Finance.GL;

public sealed class AccountingEventOptions
{
    public const string SectionName = "Finance:AccountingEvents";
    // Deliberately false until C6 schema, rehearsal, and owner integrations are separately released.
    public bool Enabled { get; set; }
}
