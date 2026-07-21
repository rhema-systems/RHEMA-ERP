namespace ErpSystem.Core.Models;

/// <summary>
/// Configuration for the external candidate portal (Blazor front-end).
/// Bind from the "CandidatePortal" section in appsettings.json.
/// </summary>
public class CandidatePortalOptions
{
    public const string SectionName = "CandidatePortal";

    /// <summary>Root URL of the Blazor Server front-end, e.g. http://localhost:5085</summary>
    public string PortalUrl { get; set; } = string.Empty;
}
