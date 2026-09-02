using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Services.Projects;

public static class ProjectCatalogDefaults
{
    public const string QuantitySurveySections = "qs-sections";
    public const string QuantitySurveyTrades = "qs-trades";
    public const string QuantitySurveyCostCodes = "qs-cost-codes";
    public const string QuantitySurveyMeasurementCodes = "qs-measurement-codes";
    public const string CivilEngineeringCategories = "civil-engineering-categories";
    public const string CivilPlanningConditions = "civil-planning-conditions";
    public const string CivilDevelopmentConstraints = "civil-development-constraints";
    public const string CivilLandUseImpacts = "civil-land-use-impacts";

    public static List<ProjectCatalogGroupDto> GetRecommendedCatalogs() =>
    [
        Create("methodologies", "Methodologies", "Waterfall", "Agile", "Hybrid", "Program", "Internal"),
        Create("lifecycle-statuses", "Lifecycle Statuses", "Draft", "PendingApproval", "Approved", "Planned", "InProgress", "OnHold", "AtRisk", "Delayed", "Completed", "Closed", "Cancelled", "Archived"),
        Create("stages", "Lifecycle Stages", "Initiation", "Planning", "Execution", "Monitoring", "Closure", "Feasibility", "ConceptDesign", "DetailedDesign", "Approvals", "Procurement", "Construction", "Commissioning", "Handover", "DefectsLiability"),
        Create("risk-ratings", "Risk Ratings", "Low", "Medium", "High", "Critical"),
        Create("risk-statuses", "Risk Statuses", "Open", "Monitoring", "Mitigated", "Closed", "Escalated"),
        Create("risk-categories", "Risk Categories", "Scope", "Schedule", "Resource", "Quality", "Vendor", "Compliance", "Financial", "Operational"),
        Create("risk-response-strategies", "Risk Response Strategies", "Monitor", "Mitigate", "Avoid", "Transfer", "Accept", "Escalate"),
        Create("issue-categories", "Issue Categories", "Scope", "Schedule", "Resource", "Quality", "Vendor", "Compliance"),
        Create("issue-statuses", "Issue Statuses", "Open", "InProgress", "PendingReview", "Resolved", "Closed", "Escalated"),
        Create("change-categories", "Change Categories", "Scope", "Budget", "Schedule", "Quality", "Contract"),
        Create("change-statuses", "Change Statuses", "Draft", "PendingApproval", "Approved", "Rejected", "Implemented", "Archived"),
        Create("quality-checkpoint-statuses", "Quality Checkpoint Statuses", "Open", "InReview", "SignedOff", "Passed", "Failed", "Waived"),
        Create("non-conformance-statuses", "Non-Conformance Statuses", "Open", "InReview", "Resolved", "Closed", "Waived"),
        Create("non-conformance-severities", "Non-Conformance Severities", "Low", "Medium", "High", "Critical"),
        Create("milestone-types", "Milestone Types", "StageGate", "Delivery", "Billing", "Acceptance", "Closure"),
        Create("billing-types", "Billing Types", "FixedPrice", "TimeAndMaterials", "Milestone", "Retainer", "CostPlus", "NonBillable"),
        Create("funding-sources", "Funding Sources", "Customer Contract", "Internal Budget", "Capex Allocation", "Grant Funding", "Department Allocation"),
        Create("cost-categories", "Cost Categories", "Labor", "Materials", "Procurement", "Travel", "Equipment", "Subcontractor", "Miscellaneous"),
        Create("expense-categories", "Expense Categories", "Travel", "Meals", "Lodging", "Supplies", "Equipment", "Other"),
        Create("boq-item-types", "BOQ Item Types", "Item", "ProvisionalSum", "PrimeCost", "Variation", "Allowance"),
        Create("resource-roles", "Resource Roles", "ProjectManager", "TeamMember", "TaskOwner", "FinanceOfficer", "RiskOfficer", "ProcurementOfficer", "ExternalContributor",
            CivilEngineeringAccessControlRegistry.CivilEngineerRole,
            CivilEngineeringAccessControlRegistry.ProjectEngineerRole,
            CivilEngineeringAccessControlRegistry.DraftsmanRole,
            CivilEngineeringAccessControlRegistry.TechnicianRole,
            CivilEngineeringAccessControlRegistry.ArtisanRole),
        Create("member-roles", "Member Roles", "Sponsor", "Project Manager", "Team Member", "Task Owner", "Finance Officer", "External Contributor",
            CivilEngineeringAccessControlRegistry.HeadRole,
            CivilEngineeringAccessControlRegistry.SupervisingEngineerRole,
            CivilEngineeringAccessControlRegistry.CivilEngineerRole,
            CivilEngineeringAccessControlRegistry.ProjectEngineerRole,
            CivilEngineeringAccessControlRegistry.DraftsmanRole,
            CivilEngineeringAccessControlRegistry.TechnicianRole,
            CivilEngineeringAccessControlRegistry.ArtisanRole),
        Create("task-statuses", "Task Statuses", "New", "Assigned", "InProgress", "Blocked", "PendingReview", "Completed", "Closed", "Cancelled"),
        Create("task-priorities", "Task Priorities", "Low", "Medium", "High", "Critical"),
        Create("deliverable-statuses", "Deliverable Statuses", "Draft", "InReview", "Approved", "Rejected", "Issued", "Accepted"),
        Create("issue-severities", "Issue Severities", "Low", "Medium", "High", "Critical"),
        Create("timesheet-work-types", "Timesheet Work Types", "Field", "Standard", "Overtime", "Travel", "Support", "BillableDelivery", "Admin"),
        Create("decision-statuses", "Decision Statuses", "Draft", "Approved", "Rejected"),
        Create("meeting-types", "Meeting Types", "Status", "RiskReview", "SteeringCommittee", "Closure", "Customer"),
        Create("action-item-statuses", "Action Item Statuses", "Open", "InProgress", "Completed", "Closed"),
        Create("action-item-priorities", "Action Item Priorities", "Low", "Normal", "High", "Critical"),
        Create("lesson-categories", "Lesson Categories", "General", "Delivery", "Process", "Quality", "Commercial", "Stakeholder"),
        Create("lesson-visibility-levels", "Lesson Visibility Levels", "Internal", "Tenant", "External"),
        Create("asset-link-types", "Asset Link Types", "Asset", "Equipment", "Installation", "Transfer", "Maintenance"),
        Create("asset-link-statuses", "Asset Link Statuses", "Linked", "Reserved", "Installed", "Transferred", "Returned"),
        Create("document-categories", "Document Categories", "Charter", "Plan", "Requirements", "Design", "Minutes", "Contracts", "Drawings", "Reports", "AcceptanceCertificates", "RiskLogs", "ChangeApprovals", "ClosureDocuments", "ExternalSubmissions", "FieldEvidence", "General"),
        Create("document-types", "Document Types", "Attachment", "Evidence", "Approval", "Reference", "Contract", "Drawing", "Minutes", "PortalAttachment", "MobileEvidence", "Photo"),
        Create(QuantitySurveySections, "QS Sections"),
        Create(QuantitySurveyTrades, "QS Trades"),
        Create(QuantitySurveyCostCodes, "QS Cost Codes"),
        Create(QuantitySurveyMeasurementCodes, "QS Measurement Codes"),
        Create(CivilEngineeringCategories, "Civil Engineering Categories", "RoadsAndDrainage", "Structures", "WaterAndSanitation", "SiteInfrastructure", "PropertyDevelopment", "MaintenanceAndRehabilitation", "DefectsAndRectification"),
        Create(CivilPlanningConditions, "Civil Planning Conditions", "Setback", "ZoningApproval", "AccessAndEasement", "DrainageReservation", "EnvironmentalCondition"),
        Create(CivilDevelopmentConstraints, "Civil Development Constraints", "BoundaryConstraint", "UtilityWayleave", "FloodRisk", "Topography", "AccessConstraint"),
        Create(CivilLandUseImpacts, "Civil Land-use Impacts", "NoMaterialImpact", "CompatibleUse", "ConditionalUse", "MaterialChangeOfUse", "RestrictedUse")
    ];

    public static bool IsQuantitySurveyCatalogType(string? catalogType)
    {
        var normalized = NormalizeCatalogType(catalogType);
        return normalized is QuantitySurveySections
            or QuantitySurveyTrades
            or QuantitySurveyCostCodes
            or QuantitySurveyMeasurementCodes;
    }

    private static ProjectCatalogGroupDto Create(string key, string displayName, params string[] items) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            Items = items.Select(item => new ProjectCatalogItemDto
            {
                Code = item,
                Name = item
            }).ToList()
        };

    public static IReadOnlyList<string> GetSupportedCatalogTypes()
        => GetRecommendedCatalogs().Select(x => x.Key).ToList();

    public static ProjectCatalogGroupDto GetRecommendedCatalog(string catalogType)
    {
        var normalized = NormalizeCatalogType(catalogType);
        return GetRecommendedCatalogs().FirstOrDefault(x => string.Equals(x.Key, normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Project catalog type '{catalogType}' is not supported.");
    }

    public static bool IsSupportedCatalogType(string? catalogType)
        => !string.IsNullOrWhiteSpace(catalogType)
            && GetRecommendedCatalogs().Any(x => string.Equals(x.Key, NormalizeCatalogType(catalogType), StringComparison.OrdinalIgnoreCase));

    public static string NormalizeCatalogType(string? catalogType)
        => string.IsNullOrWhiteSpace(catalogType)
            ? string.Empty
            : catalogType.Trim().ToLowerInvariant();
}
