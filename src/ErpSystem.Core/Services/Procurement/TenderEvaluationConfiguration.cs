using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>Checks persisted configuration; never changes an issued tender's scoring rules.</summary>
public static class TenderEvaluationConfiguration
{
    public static async Task ValidateAsync(IUnitOfWork unitOfWork, Guid tenantId,
        Guid? templateId, bool useQCBS, decimal technicalWeight, decimal financialWeight,
        decimal minimumTechnicalScore)
    {
        // Preserve the existing legacy, template-free scoring route.
        if (!templateId.HasValue) return;
        var template = await unitOfWork.Repository<EvaluationTemplate>().GetByIdAsync(templateId.Value);
        if (template is null || template.TenantId != tenantId || template.IsDeleted || !template.IsActive)
            throw new TenderEvaluationConfigurationException("TENDER_EVALUATION_TEMPLATE_UNAVAILABLE",
                "Select an active evaluation template in this tenant before continuing.");
        Validate(template, useQCBS, technicalWeight, financialWeight, minimumTechnicalScore);
    }

    public static Task ValidateAsync(IUnitOfWork unitOfWork, Tender tender) =>
        ValidateAsync(unitOfWork, tender.TenantId, tender.EvaluationTemplateId, tender.UseQCBSEvaluation,
            tender.TechnicalWeight, tender.FinancialWeight, tender.MinimumTechnicalScore);

    public static void Validate(EvaluationTemplate template, bool useQCBS,
        decimal technicalWeight, decimal financialWeight, decimal minimumTechnicalScore)
    {
        if (template.ScoringMethod is not ("WeightedAverage" or "SimpleAverage" or "PassFail" or "QCBS"))
            throw new TenderEvaluationConfigurationException("TENDER_EVALUATION_METHOD_UNSUPPORTED",
                "The evaluation template uses an unsupported scoring method. Select a supported template.");

        if ((template.ScoringMethod == "QCBS") != useQCBS)
            throw new TenderEvaluationConfigurationException("TENDER_EVALUATION_METHOD_MISMATCH",
                $"The selected template uses {template.ScoringMethod}, but QCBS evaluation is {(useQCBS ? "enabled" : "disabled")} on the tender. " +
                "Align the template and evaluation method while the tender is a draft. Issued tenders require a governed correction; do not change their scoring rules silently.");

        if (useQCBS && (technicalWeight != template.TechnicalWeight ||
                       financialWeight != template.FinancialWeight ||
                       minimumTechnicalScore != template.MinimumTechnicalScore))
            throw new TenderEvaluationConfigurationException("TENDER_EVALUATION_WEIGHTS_MISMATCH",
                "The tender's QCBS weights or minimum technical score differ from its evaluation template. Align them before approval or evaluation.");
    }
}
