using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public sealed class ProcurementSupplierApplicantContactCorrectionStore :
    IProcurementSupplierApplicantContactCorrectionStore
{
    private readonly ApplicationDbContext _context;

    public ProcurementSupplierApplicantContactCorrectionStore(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public bool HasRequiredTransaction =>
        !_context.Database.IsRelational() ||
        _context.Database.CurrentTransaction is not null;

    public async Task SetVerifiedContactCorrectionContextAsync(
        Guid applicantAccessId,
        Guid actorUserId,
        string verifiedContactHashSha256,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return;
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Verified supplier-contact correction requires an active database transaction.");
        if (applicantAccessId == Guid.Empty || actorUserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(verifiedContactHashSha256) ||
            verifiedContactHashSha256.Length != 64)
        {
            throw new ArgumentException(
                "The controlled supplier-contact correction context is incomplete.");
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             EXEC sys.sp_set_session_context
                 @key=N'TDC_SUPPLIER_CONTACT_ACCESS_ID',
                 @value={applicantAccessId},
                 @read_only=0;
             EXEC sys.sp_set_session_context
                 @key=N'TDC_SUPPLIER_CONTACT_ACTOR_ID',
                 @value={actorUserId},
                 @read_only=0;
             EXEC sys.sp_set_session_context
                 @key=N'TDC_SUPPLIER_CONTACT_HASH',
                 @value={verifiedContactHashSha256},
                 @read_only=0;
             """,
            cancellationToken);
    }

    public async Task ClearVerifiedContactCorrectionContextAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return;

        await _context.Database.ExecuteSqlRawAsync(
            """
            EXEC sys.sp_set_session_context
                @key=N'TDC_SUPPLIER_CONTACT_ACCESS_ID',
                @value=NULL,
                @read_only=0;
            EXEC sys.sp_set_session_context
                @key=N'TDC_SUPPLIER_CONTACT_ACTOR_ID',
                @value=NULL,
                @read_only=0;
            EXEC sys.sp_set_session_context
                @key=N'TDC_SUPPLIER_CONTACT_HASH',
                @value=NULL,
                @read_only=0;
            """,
            cancellationToken);
    }
}
