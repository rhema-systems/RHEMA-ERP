using System.Linq.Expressions;
using ErpSystem.Core.Entities.DocumentManagement;

namespace ErpSystem.Core.Services.DocumentManagement;

/// <summary>
/// Defines the central-DMS lifecycle state that downstream controlled
/// processes may consume as evidence.
/// </summary>
public static class CentralDocumentEvidenceRules
{
    public const string ActiveLifecycleStatus = "Active";
    public const string PublishedVersionStatus = "Published";

    public static Expression<Func<CentralDocumentVersion, bool>> CurrentPublished() => version =>
        !version.IsDeleted &&
        !version.DocumentRecord.IsDeleted &&
        version.DocumentRecord.LifecycleStatus == ActiveLifecycleStatus &&
        version.DocumentRecord.VersionStatus == PublishedVersionStatus &&
        version.DocumentRecord.CurrentVersion == version.VersionNumber &&
        version.Status == PublishedVersionStatus &&
        version.PublishedAt.HasValue;
}
