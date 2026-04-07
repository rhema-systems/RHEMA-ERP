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

        var financialSummary = await GetFinancialControlSummaryAsync(id);
        var commercialSummary = await GetCommercialSummaryAsync(id);
        var integrationSummary = await GetIntegrationSummaryAsync(id);
        var governanceSummary = await GetGovernanceSummaryAsync(id);
        var linkOptions = await GetProjectLinkOptionsAsync(id);

        return new ProjectWorkspaceDto
        {
            Project = project,
            FinancialSummary = financialSummary,
            CommercialSummary = commercialSummary,
            IntegrationSummary = integrationSummary,
            GovernanceSummary = governanceSummary,
            LinkOptions = linkOptions,
        };
    }
}
