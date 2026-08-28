using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectWorkspaceDto?> GetProjectWorkspaceAsync(Guid id)
    {
        var project = await GetProjectByIdAsync(id);
        if (project == null)
        {
            return null;
        }

        ProjectFinancialControlSummaryDto? financialSummary = null;
        ProjectGovernanceSummaryDto? governanceSummary = null;
        try
        {
            financialSummary = await GetFinancialControlSummaryAsync(id);
        }
        catch (UnauthorizedAccessException)
        {
            // The aggregate workspace is also used by project members whose
            // role permits project/QS work but not project-financial control.
        }

        try
        {
            governanceSummary = await GetGovernanceSummaryAsync(id);
        }
        catch (UnauthorizedAccessException)
        {
            // Governance data remains absent for members without its distinct
            // project role instead of making every other workspace tab fail.
        }

        var commercialSummary = await GetCommercialSummaryAsync(id);
        var integrationSummary = await GetIntegrationSummaryAsync(id);
        var postHandoverSummary = await GetPostHandoverSummaryAsync(id);
        var linkOptions = await GetProjectLinkOptionsAsync(id);
        var phaseGateEvaluations = await GetProjectPhaseGateEvaluationsAsync(id);

        return new ProjectWorkspaceDto
        {
            Project = project,
            FinancialSummary = financialSummary,
            CommercialSummary = commercialSummary,
            IntegrationSummary = integrationSummary,
            GovernanceSummary = governanceSummary,
            PostHandoverSummary = postHandoverSummary,
            LinkOptions = linkOptions,
            PhaseGateEvaluations = phaseGateEvaluations.ToList(),
        };
    }
}
