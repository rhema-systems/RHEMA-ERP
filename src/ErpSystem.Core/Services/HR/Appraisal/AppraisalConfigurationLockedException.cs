namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// A template or a settings profile refused a change because appraisals rely on it (performance closure E-e, D-66
/// and D-67): a template's structure cannot change underneath the appraisals scored on it, nor a profile's rules
/// underneath the appraisals that read them. The controllers answer it 409 with the reason. It is an
/// <see cref="InvalidOperationException"/>, so a route that already maps those keeps answering.
/// </summary>
public sealed class AppraisalConfigurationLockedException : InvalidOperationException
{
    public AppraisalConfigurationLockedException(string message) : base(message) { }
}
