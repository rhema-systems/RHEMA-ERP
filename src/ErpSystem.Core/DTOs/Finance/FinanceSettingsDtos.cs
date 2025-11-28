using System;

namespace ErpSystem.Core.DTOs.Finance
{
    public class FinanceSettingsDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CoaType { get; set; } = "Standard";
        public bool CoaConfigurationLocked { get; set; }
        public string BaseCurrency { get; set; } = "GHS";
        public Guid? RetainedEarningsAccountId { get; set; }
        public Guid? UnrealizedGainLossAccountId { get; set; }
        public Guid? RealizedGainLossAccountId { get; set; }
        public Guid? SuspenseAccountId { get; set; }
    }

    public class UpdateFinanceSettingsDto
    {
        public string? CoaType { get; set; }
        public string? BaseCurrency { get; set; }
        public Guid? RetainedEarningsAccountId { get; set; }
        public Guid? UnrealizedGainLossAccountId { get; set; }
        public Guid? RealizedGainLossAccountId { get; set; }
        public Guid? SuspenseAccountId { get; set; }
    }
}
