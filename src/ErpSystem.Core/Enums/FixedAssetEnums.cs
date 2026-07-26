using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Enums
{
    public enum DepreciationMethod
    {
        [Display(Name = "Straight Line")]
        StraightLine = 1,

        [Display(Name = "Declining Balance")]
        DecliningBalance = 2,

        [Display(Name = "Double Declining Balance")]
        DoubleDecliningBalance = 3,

        [Display(Name = "Sum of Years Digits")]
        SumOfYearsDigits = 4,

        [Display(Name = "Units of Production")]
        UnitsOfProduction = 5,

        [Display(Name = "None")]
        None = 0
    }

    public enum FixedAssetStatus
    {
        [Display(Name = "Draft")]
        Draft = 1,

        [Display(Name = "Active")]
        Active = 2,

        [Display(Name = "Fully Depreciated")]
        FullyDepreciated = 3,

        [Display(Name = "Disposed")]
        Disposed = 4,

        [Display(Name = "Held for Sale")]
        HeldForSale = 5,

        [Display(Name = "Written Off")]
        WrittenOff = 6,

        [Display(Name = "Under Construction")] // Asset in Progress (CIP)
        UnderConstruction = 7,

        [Display(Name = "On Hold")]
        OnHold = 8,  // Temporarily paused (depreciation suspended)

        [Display(Name = "Acquired")]
        Acquired = 9,

        [Display(Name = "Capitalized")]
        Capitalized = 10,

        [Display(Name = "Pending Approval")]
        PendingApproval = 11,

        [Display(Name = "Rejected")]
        Rejected = 12
    }

    public enum DepreciationConvention
    {
        [Display(Name = "Full Month")]
        FullMonth = 1,    // Depreciate full amount in the month of acquisition

        [Display(Name = "Mid-Month")]
        MidMonth = 2,     // 50% depreciation in month of acquisition

        [Display(Name = "Half-Year")]
        HalfYear = 3,     // 50% of annual depreciation in first year

        [Display(Name = "Actual Days")]
        ActualDays = 4    // Pro-rata based on days
    }

    public enum DisposalType
    {
        [Display(Name = "Sale")]
        Sale = 1,

        [Display(Name = "Scrap")]
        Scrap = 2,

        [Display(Name = "Donation")]
        Donation = 3,

        [Display(Name = "Damage/Theft")]
        DamageTheft = 4
    }

    public enum AssetTransferStatus
    {
        [Display(Name = "Draft")]
        Draft = 1,

        [Display(Name = "Pending Approval")]
        PendingApproval = 2,

        [Display(Name = "Approved")]
        Approved = 3,

        [Display(Name = "Completed")]
        Completed = 4,

        [Display(Name = "Rejected")]
        Rejected = 5,

        [Display(Name = "Cancelled")]
        Cancelled = 6
    }

    public enum AssetTransferType
    {
        [Display(Name = "Internal")]
        Internal = 1,

        [Display(Name = "External")]
        External = 2,

        [Display(Name = "Custodial")]
        Custodial = 3,

        [Display(Name = "Segment Movement")]
        SegmentMovement = 4,

        [Display(Name = "GL Reclassification")]
        GlReclassification = 5
    }

    public enum AssetDisposalStatus
    {
        [Display(Name = "Draft")]
        Draft = 1,

        [Display(Name = "Pending Approval")]
        PendingApproval = 2,

        [Display(Name = "Approved")]
        Approved = 3,

        [Display(Name = "Completed")]
        Completed = 4,

        [Display(Name = "Rejected")]
        Rejected = 5,

        [Display(Name = "Cancelled")]
        Cancelled = 6
    }

    public enum VerificationSessionStatus
    {
        [Display(Name = "Draft")]
        Draft = 1,

        [Display(Name = "In Progress")]
        InProgress = 2,

        [Display(Name = "Completed")]
        Completed = 3,

        [Display(Name = "Cancelled")]
        Cancelled = 4,

        [Display(Name = "Pending Approval")]
        PendingApproval = 5,

        [Display(Name = "Approved")]
        Approved = 6,

        [Display(Name = "Rejected")]
        Rejected = 7
    }

    public enum AssetCondition
    {
        [Display(Name = "Excellent")]
        Excellent = 1,

        [Display(Name = "Good")]
        Good = 2,

        [Display(Name = "Fair")]
        Fair = 3,

        [Display(Name = "Poor")]
        Poor = 4,

        [Display(Name = "Broken")]
        Broken = 5,

        [Display(Name = "Missing")]
        Missing = 6
    }

    /// <summary>
    /// Type of asset valuation change (IAS 16 / IAS 36)
    /// </summary>
    public enum ValuationType
    {
        [Display(Name = "Revaluation")]
        Revaluation = 1,        // IAS 16 — increase in fair value

        [Display(Name = "Impairment")]
        Impairment = 2,         // IAS 36 — write-down due to impairment

        [Display(Name = "Impairment Reversal")]
        ImpairmentReversal = 3  // IAS 36 — reversal of prior impairment
    }

    /// <summary>
    /// Status of a lease contract (IFRS 16 / ASC 842)
    /// </summary>
    public enum LeaseStatus
    {
        [Display(Name = "Draft")]
        Draft = 1,

        [Display(Name = "Active")]
        Active = 2,

        [Display(Name = "Terminated")]
        Terminated = 3,

        [Display(Name = "Completed")]
        Completed = 4,

        [Display(Name = "Pending Approval")]
        PendingApproval = 5,

        [Display(Name = "Rejected")]
        Rejected = 6
    }

    /// <summary>
    /// Lease payment frequency — value equals months per period
    /// </summary>
    public enum PaymentFrequency
    {
        [Display(Name = "Monthly")]
        Monthly = 1,

        [Display(Name = "Quarterly")]
        Quarterly = 3,

        [Display(Name = "Semi-Annually")]
        SemiAnnually = 6,

        [Display(Name = "Annually")]
        Annually = 12
    }
}
