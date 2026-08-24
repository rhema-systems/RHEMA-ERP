using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

// ⚠ An ALIAS, not `using ErpSystem.Core.Entities.Finance.FixedAssets`. That namespace declares its
// own `AssetTransfer`, and this file declares HR's — the collision recorded in build plan §3.3.
// C#'s local-declaration-wins rule would in fact resolve it correctly here, but relying on that
// puts a silent trap one edit away from firing, and this exact collision has already bitten twice.
using FixedAsset = ErpSystem.Core.Entities.Finance.FixedAssets.FixedAsset;

namespace ErpSystem.Core.Entities.HR.Assets;

/// <summary>
/// Definition for different types of assets
/// </summary>
public class AssetType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool HasExtraAttributes { get; set; }

    public virtual ICollection<AssetTypeAttribute> AssetTypeAttributes { get; set; } = new List<AssetTypeAttribute>();
}

/// <summary>
/// Represents extra details specific to an asset type
/// </summary>
public class AssetTypeAttribute : TenantEntity
{
    public Guid AssetTypeId { get; set; }

    [Required]
    [MaxLength(70)]
    public string AttributeName { get; set; } = string.Empty;

    public AssetAttributeDataType DataType { get; set; } = AssetAttributeDataType.Text;

    public bool IsRequired { get; set; }

    public bool IsExpiryDate { get; set; }

    [MaxLength(2000)]
    public string AttributeOptions { get; set; } = string.Empty;

    [ForeignKey("AssetTypeId")]
    public virtual AssetType AssetType { get; set; } = null!;
}

/// <summary>
/// Company asset definition
/// </summary>
public class CompanyAsset : TenantEntity
{
    [MaxLength(70)]
    public string AssetNumber { get; set; } = string.Empty;

    [MaxLength(70)]
    public string AssetTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AssetName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// AST-7 — the field TDC calls "Additional Remarks".
    /// </summary>
    /// <remarks>
    /// A new column rather than a rename. The change document asks to rename "Additional
    /// Description" to "Additional Remarks", but no field of that name exists anywhere in this
    /// model — the only <c>AdditionalDescription</c> in the repository is on a payroll component.
    /// <see cref="Description"/> and <see cref="Specifications"/> are both already spoken for and
    /// mean different things, so renaming either would have made two fields wrong to fix a label.
    /// </remarks>
    [MaxLength(1000)]
    public string? AdditionalRemarks { get; set; }

    public Guid AssetTypeId { get; set; }

    /// <summary>
    /// AST-11 — whether HR created this entry or picked it from the Finance fixed-asset register.
    /// </summary>
    /// <remarks>
    /// ⚠ Not decoration. It decides ownership: see <see cref="FixedAssetId"/>.
    /// </remarks>
    public AssetSource Source { get; set; } = AssetSource.HrCreated;

    /// <summary>
    /// AST-11 — the Finance fixed asset this entry stands for, where it stands for one.
    /// </summary>
    /// <remarks>
    /// <para>Null on an HR-created asset, set on one picked from Fixed Assets. The pattern is the
    /// one <b>Finance itself already uses</b> to reach into Operations —
    /// <c>FixedAsset.MaintenanceAssetId</c>, a nullable FK plus a navigation — so this is the
    /// house convention rather than a new idea.</para>
    ///
    /// <para><b>HR reads across this link and never writes.</b> Capitalisation, depreciation,
    /// valuation and disposal accounting stay in Finance (area-16 decision D1); HR owns custody —
    /// who holds the thing. On a linked asset the service refuses edits to the purchase figures and
    /// refuses disposal outright, and says so, rather than keeping a second copy of the truth that
    /// drifts.</para>
    /// </remarks>
    public Guid? FixedAssetId { get; set; }

    // Identification
    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(70)]
    public string? ModelNumber { get; set; }

    [MaxLength(70)]
    public string? SerialNumber { get; set; }

    // Purchase Information
    public DateOnly? PurchaseDate { get; set; }

    public decimal? PurchaseCost { get; set; }

    [MaxLength(100)]
    public string? Supplier { get; set; }

    [MaxLength(70)]
    public string? InvoiceNumber { get; set; }

    // Warranty
    public bool HasWarranty { get; set; }

    public DateOnly? WarrantyStartDate { get; set; }

    public DateOnly? WarrantyEndDate { get; set; }

    [MaxLength(100)]
    public string? WarrantyProvider { get; set; }

    // Physical Details
    [MaxLength(30)]
    public string? Color { get; set; }

    [MaxLength(30)]
    public string? Size { get; set; }

    [MaxLength(300)]
    public string? Specifications { get; set; }

    // Status
    public CompanyAssetStatus Status { get; set; } = CompanyAssetStatus.Available;

    public HRAssetCondition Condition { get; set; } = HRAssetCondition.Good;

    // Location
    public Guid? LocationId { get; set; }

    [MaxLength(500)]
    public string? LocationDetails { get; set; }

    // Organization Unit
    public Guid? UnitId { get; set; }

    // Assignment
    public bool IsAssignable { get; set; }

    public bool IsCurrentlyAssigned { get; set; }

    public Guid? CurrentAssignedToId { get; set; }

    // Maintenance
    public bool RequiresRegularMaintenance { get; set; }

    public int? MaintenanceIntervalDays { get; set; }

    public DateOnly? LastMaintenanceDate { get; set; }

    public DateOnly? NextMaintenanceDate { get; set; }

    // Rental — AST-9, decision D2
    /// <summary>
    /// Whether this asset is one an employee can be <b>charged for holding</b> — staff housing, a
    /// company vehicle used privately, a serviced flat.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="IsAssignable"/>, which asks whether it can be issued at all. Every
    /// rentable asset is assignable; almost no assignable asset is rentable. The flag exists so
    /// that rental terms cannot be attached to a stapler, and so the register can answer "what
    /// property do we let to staff" without inferring it from whatever happens to carry a rent.
    /// </remarks>
    public bool IsRentable { get; set; }

    /// <summary>The going rate for this asset, per period. A <b>default</b> the assignment copies.</summary>
    /// <remarks>
    /// Kept on the asset because it is a property of the thing, not of who holds it: the flat is
    /// worth what it is worth whoever lives in it. The assignment may charge less — a subsidy — and
    /// the difference between the two is what makes the arrangement a taxable benefit.
    /// </remarks>
    public decimal? StandardRentalAmount { get; set; }

    [MaxLength(3)]
    public string? RentalCurrencyCode { get; set; }

    // Insurance
    public bool IsInsured { get; set; }

    [MaxLength(70)]
    public string? InsurancePolicyNumber { get; set; }

    public decimal? InsuredValue { get; set; }

    /// <summary>AST-4 — when the cover lapses. Null where the asset is not insured, or not known.</summary>
    /// <remarks>
    /// A <c>DateOnly</c>, like every other date on this entity: cover expires on a day, not at an
    /// instant, and storing a time would invent a precision the policy document does not have.
    /// </remarks>
    public DateOnly? InsuranceExpiryDate { get; set; }

    // Disposal
    public DateOnly? DisposalDate { get; set; }

    public DisposalMethod? DisposalMethod { get; set; }

    [MaxLength(1000)]
    public string? DisposalNotes { get; set; }

    [ForeignKey(nameof(AssetTypeId))]
    public virtual AssetType AssetType { get; set; } = null!;

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    [ForeignKey(nameof(UnitId))]
    public virtual OrganizationUnit? Unit { get; set; }

    [ForeignKey(nameof(CurrentAssignedToId))]
    public virtual Employee? CurrentAssignedTo { get; set; }

    [ForeignKey(nameof(FixedAssetId))]
    public virtual FixedAsset? FixedAsset { get; set; }

    public virtual ICollection<AssetAssignment> AssignmentHistory { get; set; } = new List<AssetAssignment>();

    public virtual ICollection<AssetMaintenance> MaintenanceRecords { get; set; } = new List<AssetMaintenance>();

    public virtual ICollection<AssetAttributeValue> AssetAttributeValues { get; set; } = new List<AssetAttributeValue>();

    public virtual ICollection<AssetImage> Images { get; set; } = new List<AssetImage>();

    public virtual ICollection<AssetAttachment> Attachments { get; set; } = new List<AssetAttachment>();
}

public class AssetAttributeValue : TenantEntity
{
    public Guid AssetId { get; set; }

    public Guid AssetTypeAttributeId { get; set; }

    [MaxLength(700)]
    public string Value { get; set; } = string.Empty;

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(AssetTypeAttributeId))]
    public virtual AssetTypeAttribute AssetTypeAttribute { get; set; } = null!;
}

/// <summary>
/// Asset images for visual identification and documentation
/// </summary>
public class AssetImage : TenantEntity
{
    public Guid AssetId { get; set; }
    
    public string FilePath { get; set; } = string.Empty;
    
    public string FileName { get; set; } = string.Empty;
    
    public DateTime UploadDate { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;
}

/// <summary>
/// Asset assignment to employee
/// </summary>
public class AssetAssignment : TenantEntity
{
    [MaxLength(70)]
    public string AssignmentNumber { get; set; } = string.Empty;

    public Guid AssetId { get; set; }

    public Guid EmployeeId { get; set; }

    /// <summary>
    /// The requisition this assignment fulfils, where it came from one — D-e.
    /// </summary>
    /// <remarks>
    /// Null for an assignment HR raises directly, which is most of them. Set by
    /// <c>AssetRequisitionService.FulfillAsync</c>, and it is the only record of what a requisition
    /// produced now that the single <c>AssignedAssetId</c> column is gone.
    /// </remarks>
    public Guid? RequisitionId { get; set; }

    /// <summary>
    /// The transfer this custody came from, where it came from one — area 16, slice 4.
    /// </summary>
    /// <remarks>
    /// The twin of <see cref="RequisitionId"/>, and it exists for the same reason: an assignment
    /// that appears from nowhere cannot be traced back to what authorised it. Set by
    /// <c>AssetTransferService.CompleteAsync</c> when an employee-to-employee move hands the asset
    /// to its new holder. Null for every assignment HR raises directly, which is most of them.
    /// </remarks>
    public Guid? TransferId { get; set; }

    // Assignment Details
    public DateOnly AssignmentDate { get; set; }
    
    public DateOnly? ExpectedReturnDate { get; set; }
    
    public AssignmentType Type { get; set; } // Permanent, Temporary, Project-based
    
    public AssignmentPurpose Purpose { get; set; }

    [MaxLength(1000)]
    public string? AssignmentNotes { get; set; }
    
    public bool IsPrimaryUser { get; set; }

    // Condition at Assignment
    public HRAssetCondition ConditionAtAssignment { get; set; }
    
    [MaxLength(1000)]
    public string? ConditionNotes { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    // Acknowledgement
    public bool EmployeeAcknowledged { get; set; }
    
    public DateTime? AcknowledgementDate { get; set; }

    // Terms & Responsibilities
    public bool ResponsibleForLoss { get; set; }
    
    public bool ResponsibleForDamage { get; set; }
    
    [MaxLength(2000)]
    public string? TermsAndConditions { get; set; }

    /// <summary>
    /// When the responsibility-and-terms document was last emailed to the holder — AST-5b.
    /// </summary>
    /// <remarks>
    /// Three columns rather than a boolean, because "was it sent?" is really three questions and a
    /// flag answers none of them well: <b>when</b>, <b>to which address</b> (an employee's recorded
    /// email changes, and the copy went to whatever it was that day) and <b>by whom</b>. The
    /// download route deliberately does not stamp these: printing a copy is not serving it on
    /// somebody, and recording it as though it were would let an unsent form look sent.
    /// </remarks>
    public DateTime? TermsDocumentSentAt { get; set; }

    [MaxLength(256)]
    public string? TermsDocumentSentTo { get; set; }

    public Guid? TermsDocumentSentById { get; set; }

    // Return
    public AssignmentStatus Status { get; set; }
    
    public DateTime? ReturnDate { get; set; }
    
    public HRAssetCondition? ConditionAtReturn { get; set; }
    
    [MaxLength(1000)]
    public string? ReturnNotes { get; set; }
    
    public bool ReturnedInGoodCondition { get; set; }

    public Guid? ReturnedToId { get; set; }

    // Rental — AST-10, decision D2
    /// <summary>
    /// What this employee is charged for holding the asset, per <see cref="RentalFrequency"/>.
    /// </summary>
    /// <remarks>
    /// <para>Null where no rent is charged. Zero is different from null and is allowed on purpose:
    /// zero means <i>provided free</i>, which is a stated arrangement — and usually a taxable one —
    /// whereas null means nobody has said. A screen that treated them alike would lose the
    /// difference between "free accommodation" and "we have not set this up yet".</para>
    ///
    /// <para>⚠ Nothing deducts this. It is declared here and read by payroll through
    /// <c>GET Assets/payroll/rental-deductions</c>. Decision D2.</para>
    /// </remarks>
    public decimal? RentalAmount { get; set; }

    [MaxLength(3)]
    public string? RentalCurrencyCode { get; set; }

    public RentalDeductionFrequency? RentalFrequency { get; set; }

    /// <summary>When the rent starts running. Defaults to the assignment date when terms are set.</summary>
    public DateOnly? RentalEffectiveFrom { get; set; }

    /// <summary>
    /// When it stops. Null means open-ended, and <b>closing the custody closes this</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ Set automatically by every act that ends a custody — a return, a loss or damage report,
    /// and a completed transfer. Without that, payroll would keep deducting rent for a house the
    /// employee moved out of, which is the kind of defect nobody notices until a payslip is wrong.
    /// Three doors close a custody in this module and all three close the rent.
    /// </remarks>
    public DateOnly? RentalEffectiveTo { get; set; }

    /// <summary>Whether the arrangement is taxable as a benefit in kind — AST-10.</summary>
    public bool IsBenefitInKind { get; set; }

    /// <summary>
    /// The taxable value of the benefit per period, where it is not simply the rent charged.
    /// </summary>
    /// <remarks>
    /// <para>Decision D2 said "a benefit-in-kind flag", and a flag alone turned out not to be
    /// enough: the taxable value of subsidised accommodation is the <b>market rate less what the
    /// employee pays</b>, and the flag cannot carry that number. Left null it is computed from the
    /// asset's <c>StandardRentalAmount</c> minus <see cref="RentalAmount"/>, which is a fact HR
    /// holds; assessing tax on it stays payroll's.</para>
    /// </remarks>
    public decimal? BenefitInKindValue { get; set; }

    // Damages/Loss
    public bool DamageReported { get; set; }
    
    [MaxLength(1000)]
    public string? DamageDescription { get; set; }
    
    public bool EmployeeLiable { get; set; }
    
    public decimal? RepairCost { get; set; }
    
    public decimal? ReplacementCost { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    [ForeignKey(nameof(ReturnedToId))]
    public virtual Employee? ReturnedTo { get; set; }

    [ForeignKey(nameof(RequisitionId))]
    public virtual AssetRequisition? Requisition { get; set; }

    [ForeignKey(nameof(TransferId))]
    public virtual AssetTransfer? Transfer { get; set; }

    [ForeignKey(nameof(TermsDocumentSentById))]
    public virtual Employee? TermsDocumentSentBy { get; set; }
}

/// <summary>
/// Asset maintenance record
/// </summary>
public class AssetMaintenance : TenantEntity
{
    [MaxLength(70)]
    public string MaintenanceNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    public DateTime MaintenanceDate { get; set; }
    
    public AssetMaintenanceType Type { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? WorkPerformed { get; set; }
    
    [MaxLength(2000)]
    public string? PartsReplaced { get; set; }

    public bool IsInternalMaintenance { get; set; }
    
    public Guid? PerformedById { get; set; }

    [MaxLength(1000)]
    public string? ExternalServiceProvider { get; set; }
    
    [MaxLength(70)]
    public string? ServiceTicketNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Cost { get; set; }
    
    public DateTime? NextMaintenanceDate { get; set; }

    public MaintenanceStatus Status { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(PerformedById))]
    public virtual Employee? PerformedBy { get; set; }
}

public class AssetAttachment : TenantEntity
{
    [Required]
    public Guid AssetId { get; set; }

    [MaxLength(250)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public DateTime UploadDate { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;
}

/// <summary>
/// Asset requisition/request from employee
/// </summary>
public class AssetRequisition : TenantEntity
{
    [MaxLength(70)]
    public string RequisitionNumber { get; set; } = string.Empty;

    /// <summary>Who raised the request. Always taken from the token, never from the payload.</summary>
    [Required]
    public Guid RequestedById { get; set; }

    /// <summary>
    /// AST-6b — who the asset is actually <b>for</b>, when that is not the person who asked.
    /// </summary>
    /// <remarks>
    /// <para>Null means "me": the requester is the beneficiary. Set only when someone raises a
    /// request on another employee's behalf, which HR may always do and a recorded line manager may
    /// do for their own reports.</para>
    ///
    /// <para><b>Two columns, because one cannot answer "who did this, and to whom".</b> That is the
    /// area-9 lesson: a record with a single actor column silently attributes the act to its
    /// subject, or the subject to its actor, and neither can be recovered afterwards. Fulfilment
    /// reads this one — the asset is assigned to the beneficiary, not to whoever typed the form.</para>
    /// </remarks>
    public Guid? BeneficiaryEmployeeId { get; set; }

    public DateTime RequestDate { get; set; }
    
    [Required]
    public Guid AssetTypeId { get; set; }
    
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }
    
    public HRAssetRequisitionPriority Priority { get; set; }

    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;
    
    public DateTime? RequiredByDate { get; set; }

    public AssetRequisitionStatus Status { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    
    public DateTime? ApprovalDate { get; set; }
    
    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Fulfillment
    public bool IsFulfilled { get; set; }
    
    public DateTime? FulfilledDate { get; set; }
    
    public Guid? FulfilledById { get; set; }

    [ForeignKey(nameof(RequestedById))]
    public virtual Employee RequestedBy { get; set; } = null!;

    [ForeignKey(nameof(AssetTypeId))]
    public virtual AssetType AssetType { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    [ForeignKey(nameof(FulfilledById))]
    public virtual Employee? FulfilledBy { get; set; }

    [ForeignKey(nameof(BeneficiaryEmployeeId))]
    public virtual Employee? BeneficiaryEmployee { get; set; }

    /// <summary>
    /// What this requisition actually produced — D-e.
    /// </summary>
    /// <remarks>
    /// ⚠ This replaces a single <c>AssignedAssetId</c> column. A requisition carries a
    /// <c>Quantity</c> and <c>FulfillAssetRequisitionDto</c> has always accepted a <b>list</b> of
    /// assets, so fulfilling one request with three assets created three assignments and then
    /// remembered exactly one of them. Rather than add a join table, the assignments themselves are
    /// the record: each one cites the requisition it came from, so "what did this yield" is a query
    /// and there is only one place the answer lives.
    /// </remarks>
    public virtual ICollection<AssetAssignment> FulfilledAssignments { get; set; } = new List<AssetAssignment>();
}

/// <summary>
/// Asset transfer between locations/employees
/// </summary>
public class AssetTransfer : TenantEntity
{
    [MaxLength(70)]
    public string TransferNumber { get; set; } = string.Empty;

    [Required]
    public Guid AssetId { get; set; }

    public DateTime TransferDate { get; set; }
    
    public HRAssetTransferType Type { get; set; } // Employee-to-Employee, Location-to-Location

    // From
    public Guid? FromEmployeeId { get; set; }
    
    public Guid? FromLocationId { get; set; }
    
    public Guid? FromUnitId { get; set; }

    // To
    public Guid? ToEmployeeId { get; set; }
    
    public Guid? ToLocationId { get; set; }
    
    public Guid? ToUnitId { get; set; }

    [MaxLength(1000)]
    public string? TransferReason { get; set; }

    public Guid InitiatedById { get; set; }

    public HRAssetTransferStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    public DateTime? CompletionDate { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(AssetId))]
    public virtual CompanyAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(FromEmployeeId))]
    public virtual Employee? FromEmployee { get; set; }

    [ForeignKey(nameof(FromLocationId))]
    public virtual Location? FromLocation { get; set; }
    
    [ForeignKey(nameof(FromUnitId))]
    public virtual OrganizationUnit? FromUnit { get; set; }

    [ForeignKey(nameof(ToEmployeeId))]
    public virtual Employee? ToEmployee { get; set; }

    [ForeignKey(nameof(ToLocationId))]
    public virtual Location? ToLocation { get; set; }
    
    [ForeignKey(nameof(ToUnitId))]
    public virtual OrganizationUnit? ToUnit { get; set; }

    [ForeignKey(nameof(InitiatedById))]
    public virtual Employee InitiatedBy { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
}

#region Asset Surcharges — area 16 slice 7 (AST-3, defect D-d, decision D9)

/// <summary>
/// A charge raised against an employee for a company asset they damaged, lost or never returned —
/// <b>AST-3</b>, and the reader that <b>defect D-d</b> had been waiting for.
/// </summary>
/// <remarks>
/// <para><b>Why this is an entity and not five more columns on the assignment.</b>
/// <c>EmployeeLiable</c>, <c>RepairCost</c> and <c>ReplacementCost</c> already sit on
/// <see cref="AssetAssignment"/>, and slice 4 made them coherent — they cannot be set on a return
/// that reports no damage. But they are <i>facts about the asset</i>: what it would cost to mend or
/// replace. A surcharge is a <i>decision about a person</i>: that this employee owes this amount,
/// taken by somebody, on a date, after they were given a chance to answer. The two are not the same
/// and one is not derivable from the other — an employer routinely charges less than the repair
/// cost, or nothing at all. Recording the decision as though it were the cost is how a record ends
/// up unable to explain a number somebody was actually asked to pay.</para>
///
/// <para><b>The right of reply is load-bearing.</b> A surcharge cannot go for approval until it has
/// been put to the employee: <c>Draft → AwaitingEmployeeResponse → Submitted → Approved</c>. The
/// answer is data on this record rather than a status, because accepting and disputing lead to the
/// same next step and differ only in what the approver is reading (decision D9).</para>
///
/// <para><b>What this does NOT do.</b> It does not deduct anything. <c>RecoveryMethod</c>,
/// <c>InstalmentCount</c> and <c>RecoveryStartDate</c> are a <i>declaration to payroll</i>, and
/// <see cref="AssetSurchargeRecovery"/> records what was actually collected. Payroll owns the
/// deduction; the exit settlement (FR-HR-184) applies whatever is still outstanding. No GL posting
/// happens here — it is registered in <c>docs/HR-FINANCE-INTEGRATION-BACKLOG.md</c> for the one
/// comprehensive sweep after the module.</para>
/// </remarks>
public class AssetSurcharge : TenantEntity
{
    [MaxLength(70)]
    public string SurchargeNumber { get; set; } = string.Empty;

    /// <summary>The custody this arises from. Always present — a charge with no custody is a claim about nobody.</summary>
    public Guid AssignmentId { get; set; }

    /// <summary>
    /// The employee being charged.
    /// </summary>
    /// <remarks>
    /// Derivable from the assignment and stamped anyway. The subject of a financial claim is not a
    /// thing to infer through a join two years later, and area 9's lesson stands: a record with one
    /// actor column cannot answer "who did this to whom".
    /// </remarks>
    public Guid EmployeeId { get; set; }

    public AssetSurchargeReason Reason { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    // ── The money ────────────────────────────────────────────────────────────

    /// <summary>What the employee is being asked to pay. A decision, not a cost.</summary>
    public decimal AssessedAmount { get; set; }

    /// <summary>
    /// The currency of every amount on this record.
    /// </summary>
    /// <remarks>
    /// Validated against Finance's canonical currency list on the write path. That is the read-side
    /// integration this module is allowed to do now (area 13 recorded what happens without it:
    /// <c>"ZZZ"</c> was accepted and stored). GL posting is what waits for the sweep, not this.
    /// </remarks>
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// The repair and replacement costs on the assignment when the charge was raised.
    /// </summary>
    /// <remarks>
    /// Copied rather than read through, so that the decision can still be compared to what it was
    /// based on after somebody edits the assignment. This is the whole of what D-d's inert fields
    /// now feed: they seed a default and they are kept as the basis, and the amount charged remains
    /// the employer's to set.
    /// </remarks>
    public decimal? BasisRepairCost { get; set; }

    public decimal? BasisReplacementCost { get; set; }

    /// <summary>What has actually been collected, accumulated from the recovery rows.</summary>
    public decimal AmountRecovered { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    public AssetSurchargeStatus Status { get; set; } = AssetSurchargeStatus.Draft;

    public Guid? RaisedById { get; set; }

    public DateTime RaisedAt { get; set; }

    // ── The employee's side (decision D9) ────────────────────────────────────

    /// <summary>When the charge was put to the employee. Null until it has been.</summary>
    public DateTime? NotifiedAt { get; set; }

    public AssetSurchargeEmployeeResponse EmployeeResponse { get; set; }
        = AssetSurchargeEmployeeResponse.NotYetGiven;

    public DateTime? EmployeeRespondedAt { get; set; }

    [MaxLength(2000)]
    public string? EmployeeResponseComments { get; set; }

    /// <summary>
    /// Why the charge went for approval although the employee never answered.
    /// </summary>
    /// <remarks>
    /// The right of reply is a right to be <b>asked</b>, not a veto exercised by silence. Without
    /// this, an employee who simply never responds blocks the charge for ever; with it, HR can
    /// proceed but must say why, on the record, where the approver reads it. Required by
    /// <c>SubmitAsync</c> exactly when <c>EmployeeResponse</c> is still <c>NotYetGiven</c>.
    /// </remarks>
    [MaxLength(1000)]
    public string? ProceededWithoutResponseReason { get; set; }

    // ── The decision ─────────────────────────────────────────────────────────

    public Guid? ApprovedById { get; set; }

    public DateTime? ApprovalDate { get; set; }

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    public DateTime? RejectedDate { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // ── Recovery — declared, not computed ────────────────────────────────────

    public AssetSurchargeRecoveryMethod? RecoveryMethod { get; set; }

    /// <summary>How many pay periods the deduction is to be spread over, where that is the method.</summary>
    public int? InstalmentCount { get; set; }

    public DateOnly? RecoveryStartDate { get; set; }

    // ── Endings other than recovery ──────────────────────────────────────────

    public Guid? WaivedById { get; set; }

    public DateTime? WaivedAt { get; set; }

    [MaxLength(1000)]
    public string? WaiverReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    [ForeignKey(nameof(AssignmentId))]
    public virtual AssetAssignment Assignment { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(RaisedById))]
    public virtual Employee? RaisedBy { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    [ForeignKey(nameof(WaivedById))]
    public virtual Employee? WaivedBy { get; set; }

    public virtual ICollection<AssetSurchargeRecovery> Recoveries { get; set; }
        = new List<AssetSurchargeRecovery>();
}

/// <summary>
/// One instalment or payment actually collected against a surcharge.
/// </summary>
/// <remarks>
/// A record of what happened, not an instruction for what should. HR needs it to know the
/// outstanding balance — which the exit settlement then deducts — and a single accumulating column
/// could not say when, how much or against what payroll period, which is exactly what somebody
/// disputing a deduction asks.
/// </remarks>
public class AssetSurchargeRecovery : TenantEntity
{
    public Guid SurchargeId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly RecoveredOn { get; set; }

    public AssetSurchargeRecoveryMethod Method { get; set; }

    /// <summary>The payroll period, receipt number or settlement this came through.</summary>
    [MaxLength(200)]
    public string? Reference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? RecordedById { get; set; }

    [ForeignKey(nameof(SurchargeId))]
    public virtual AssetSurcharge Surcharge { get; set; } = null!;

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee? RecordedBy { get; set; }
}

#endregion

