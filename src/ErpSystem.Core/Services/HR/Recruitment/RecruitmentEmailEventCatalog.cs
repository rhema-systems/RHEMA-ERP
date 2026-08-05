using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>DI-registered wrapper exposing the static <see cref="RecruitmentEmailCatalog"/>.</summary>
public sealed class RecruitmentEmailEventCatalog : IEmailEventCatalog
{
    public string Module => RecruitmentEmailCatalog.Module;
    public IReadOnlyList<EmailEventDescriptor> Events => RecruitmentEmailCatalog.All;
}
