using System.Data;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

        var transaction = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "The verified supplier-contact correction transaction could not be resolved.");
        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText =
            """
            EXEC sys.sp_set_session_context
                @key=N'TDC_SUPPLIER_CONTACT_ACCESS_ID',
                @value=@accessId,
                @read_only=0;
            EXEC sys.sp_set_session_context
                @key=N'TDC_SUPPLIER_CONTACT_ACTOR_ID',
                @value=@actorId,
                @read_only=0;
            EXEC sys.sp_set_session_context
                @key=N'TDC_SUPPLIER_CONTACT_HASH',
                @value=@contactHash,
                @read_only=0;

            SELECT CASE WHEN
                TRY_CONVERT(uniqueidentifier,
                    SESSION_CONTEXT(N'TDC_SUPPLIER_CONTACT_ACCESS_ID')) = @accessId
                AND TRY_CONVERT(uniqueidentifier,
                    SESSION_CONTEXT(N'TDC_SUPPLIER_CONTACT_ACTOR_ID')) = @actorId
                AND TRY_CONVERT(nvarchar(64),
                    SESSION_CONTEXT(N'TDC_SUPPLIER_CONTACT_HASH')) = @contactHash
                THEN 1 ELSE 0 END;
            """;
        AddParameter(command, "@accessId", DbType.Guid, applicantAccessId);
        AddParameter(command, "@actorId", DbType.Guid, actorUserId);
        AddParameter(
            command,
            "@contactHash",
            DbType.String,
            verifiedContactHashSha256,
            size: 64);
        var contextAccepted = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));
        if (contextAccepted != 1)
            throw new InvalidOperationException(
                "The database did not accept the governed supplier-contact correction context.");
    }

    public async Task ClearVerifiedContactCorrectionContextAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return;

        var connection = _context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        if (_context.Database.CurrentTransaction is { } transaction)
            command.Transaction = transaction.GetDbTransaction();
        command.CommandText =
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
            """;
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        DbType type,
        object value,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        if (size.HasValue)
            parameter.Size = size.Value;
        command.Parameters.Add(parameter);
    }
}
