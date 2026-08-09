namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementSupplierApplicantContactCorrectionStore
{
    bool HasRequiredTransaction { get; }

    Task SetVerifiedContactCorrectionContextAsync(
        Guid applicantAccessId,
        Guid actorUserId,
        string verifiedContactHashSha256,
        CancellationToken cancellationToken = default);

    Task ClearVerifiedContactCorrectionContextAsync(
        CancellationToken cancellationToken = default);
}
