namespace ErpSystem.Core.Enums;

public enum ProcurementSupplierPerformanceMetricKey
{
    DeliveryTimeliness = 0,
    GrnQuality = 1,
    RejectionRate = 2,
    PriceCompetitiveness = 3,
    Responsiveness = 4,
    ComplaintResolution = 5,
    ContractCompletion = 6
}

public enum ProcurementSupplierPerformanceDataStatus
{
    Complete = 0,
    InsufficientCoverage = 1,
    NoQualifyingActivity = 2
}
