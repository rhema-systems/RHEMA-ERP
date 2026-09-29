using System.Text.Json;
using System.Text.RegularExpressions;

namespace ErpSystem.Api.Services;

/// <summary>Explicit test-only proposals derived from the documented QS acceptance recipe.
/// They are never approvals and are validated again by the configuration owner before save.</summary>
internal static partial class QsUatDecisionRecipes
{
    internal static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
    {
        ["QS-DEC-001"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"officerRoleIds":["{{OfficerRoleId}}"],"approverRoleIds":["{{ApproverRoleId}}"],"oversightRoleIds":["{{OversightRoleId}}"],"currencyCode":"GHS","operationalAuthorityLimit":500000,"seniorAuthorityLimit":5000000,"executiveAuthorityLimit":50000000,"enforceProjectScope":true,"enforceContractScope":true,"enforceSectionScope":true}""",
        ["QS-DEC-002"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"allowedStandards":["smm7","cesmm4","tdcLocal"],"defaultStandard":"cesmm4","projectTypeIds":["{{ProjectTypeId}}"],"requireCostCode":true,"requireTrade":true,"requireWorkPackage":true}""",
        ["QS-DEC-003"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"requiredVersionTypes":["approved","revised","remeasurement","finalAccount"],"boqWorkflowDefinitionId":"{{BoqWorkflow}}","estimateWorkflowDefinitionId":"{{EstimateWorkflow}}","approvedVersionsImmutable":true,"requireWorkflowBeforeUse":true,"requireLineLevelComparison":true}""",
        ["QS-DEC-004"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"components":["material","labour","plant","subcontract","overhead","profit","contingency","wastage","transport"],"maximumOverheadPercent":15,"maximumProfitPercent":15,"maximumContingencyPercent":10,"maximumWastagePercent":10,"decimalPlaces":2}""",
        ["QS-DEC-005"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"dimensions":["projectType","location","period","material","plant","equipment"],"updateCadenceMonths":3,"projectTypeIds":["{{ProjectTypeId}}"],"locationIds":["{{LocationId}}"],"requireMarketEvidence":true}""",
        ["QS-DEC-006"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"indexSources":["gssPbci","controlledManualImport"],"formula":"fixedCoefficientIndexRatio","materialCoefficient":55,"labourCoefficient":25,"plantCoefficient":15,"otherCoefficient":5,"approvalWorkflowDefinitionId":"{{EscalationWorkflow}}","importFormat":"Controlled Excel"}""",
        ["QS-DEC-007"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"workflowDefinitionId":"{{MeasurementWorkflow}}","metadataTemplateId":"{{MeasurementTemplate}}","jointAttendanceRoleIds":["{{OfficerRoleId}}"],"consultantRoleIds":["{{ApproverRoleId}}"],"requireContractorSignature":true,"requireConsultantSignature":true}""",
        ["QS-DEC-008"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"valuationWorkflowDefinitionId":"{{ValuationWorkflow}}","certificateWorkflowDefinitionId":"{{CertificateWorkflow}}","valuationTemplateId":"{{ValuationTemplate}}","certificateTemplateId":"{{CertificateTemplate}}","valuationEvidenceMetadataTemplateId":"{{ValuationDmsTemplate}}","certificateMetadataTemplateId":"{{CertificateDmsTemplate}}","certificateExpenseAccountId":"{{ExpenseAccount}}","certificateAccountsPayableAccountId":"{{ApAccount}}","certificatePaymentTermId":"{{PaymentTerm}}","certificateTaxGroupId":"{{TaxGroup}}","certificateWithholdingTaxId":"{{WithholdingTax}}","taxHandling":"financeCalculated","requireContractorSubmission":true,"requireConsultantEndorsement":true,"requireSupportingEvidence":true,"requirePreviousCertificate":true,"applyAdvanceRecovery":true,"applyRetention":true}""",
        ["QS-DEC-009"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"maximumRetentionPercent":10,"practicalCompletionReleasePercent":50,"sectionalTakeoverReleasePercent":0,"defectsReleasePercent":50,"defectsLiabilityDays":365,"approvalWorkflowDefinitionId":"{{RetentionWorkflow}}","allowRetentionBond":true}""",
        ["QS-DEC-010"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"allowMaterialsOnSite":true,"allowOffSiteMaterials":false,"deductTdcSuppliedMaterials":true,"valuationBasis":"lowerOfCostOrApprovedRate","requireInventoryReconciliation":true,"approvalWorkflowDefinitionId":"{{MaterialWorkflow}}"}""",
        ["QS-DEC-011"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"variationWorkflowDefinitionId":"{{VariationWorkflow}}","claimWorkflowDefinitionId":"{{ClaimWorkflow}}","variationEvidenceMetadataTemplateId":"{{VariationDmsTemplate}}","allowedTypes":["Variation","Claim","Daywork","Additional Work","Site Instruction","Change Order"],"updateContractSum":true,"updateBudget":true,"updateForecast":true,"updateCertificate":true}""",
        ["QS-DEC-012"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"controlProvisionalSums":true,"controlContingencies":true,"controlDefectsLiability":true,"controlSectionalTakeover":true,"controlSubcontracts":true,"controlClaimClauses":true,"controlBackCharges":true,"controlContraCharges":true,"requireCommercialTermsDocument":false,"subcontractWorkflowDefinitionId":"{{SubcontractWorkflow}}","finalAccountWorkflowDefinitionId":"{{FinalWorkflow}}"}""",
        ["QS-DEC-013"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"channels":["externalPortal","controlledExcel","signedPdf"],"allowedFileExtensions":[".xlsx",".csv",".pdf",".docx"],"maximumFileSizeMb":50,"requirePortalIdentity":true,"requireEvidence":true,"requireSignature":true}""",
        ["QS-DEC-014"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"allowedTools":[],"exchangeModes":["controlledExcel","api","signedPdf"],"requireStagingAndReconciliation":true,"metadataTemplateIds":["{{MeasurementTemplate}}","{{ValuationDmsTemplate}}"]}""",
        ["QS-DEC-015"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"reportIds":{{ReportIds}},"viewerRoleIds":["{{OfficerRoleId}}","{{ApproverRoleId}}"],"exportFormats":["PDF","XLSX","CSV"],"enforceScopedDrilldown":true,"allowScheduledDistribution":false}""",
        ["QS-DEC-016"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"modules":["projects","procurement","inventory","contracts","accountsPayable","generalLedger","documentManagement","workflow"],"postingMode":"controlledEvent","requireIdempotencyKey":true,"requireReconciliation":true,"prohibitDuplicatePosting":true}""",
        ["QS-DEC-017"] = """{"effectiveFrom":"{{From}}","effectiveTo":null,"sourceTypes":["excel","csv","legacyDatabase","physicalRecord","centralDms"],"ownerRoleIds":["{{OfficerRoleId}}"],"reviewerRoleIds":["{{ApproverRoleId}}"],"signOffRoleIds":["{{OversightRoleId}}"],"requireStaging":true,"requireReconciliation":true,"requireSignedAcceptance":true}""",
    };

    internal static (JsonElement? Value, string[] Missing) Resolve(string key, IReadOnlyDictionary<string, string> references)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var json = Placeholder().Replace(Values[key], match =>
        {
            var name = match.Groups[1].Value;
            if (references.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
            missing.Add(name);
            return match.Value;
        });
        if (missing.Count > 0) return (null, missing.OrderBy(x => x).ToArray());
        using var document = JsonDocument.Parse(json);
        return (document.RootElement.Clone(), []);
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();
}

