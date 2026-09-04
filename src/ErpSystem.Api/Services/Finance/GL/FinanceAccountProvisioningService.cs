using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinanceAccountProvisioningService : IFinanceAccountProvisioningService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FinanceAccountProvisioningService> _logger;
    private readonly IAccountSegmentIdentityService _segmentIdentity;

    public FinanceAccountProvisioningService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<FinanceAccountProvisioningService> logger,
        IAccountSegmentIdentityService? segmentIdentity = null)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
        _segmentIdentity = segmentIdentity ?? new ErpSystem.Api.Services.Finance.Segments.AccountSegmentIdentityService(db);
    }

    public async Task<ProvisionedFinanceAccountDto> ProvisionAsync(
        ProvisionFinanceAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        if (request.TenantId == Guid.Empty || request.TenantId != tenantId)
            throw new InvalidOperationException("Finance account provisioning tenant context is invalid.");
        var accountCode = NormalizeRequired(request.AccountCode, "Account code", 50).ToUpperInvariant();
        var accountName = NormalizeRequired(request.AccountName, "Account name", 200);
        var currencyCode = NormalizeRequired(request.CurrencyCode, "Currency code", 3).ToUpperInvariant();
        var classificationCode = FinanceClassificationManifestSeeder.ResolveReviewedClassificationCode(
            accountCode, request.CoreAccountType)
            ?? throw new InvalidOperationException(
                "The requested account code and core type are not present in the reviewed Finance classification manifest.");

        var ownsTransaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await new FinanceSegmentDimensionManifestSeeder(_db, _logger).SeedAsync(tenantId, DateTime.UtcNow, cancellationToken);
        var identity = await _segmentIdentity.ResolveProvisioningIdentityAsync(tenantId, accountCode, cancellationToken);
        var accountNumber = identity.AccountNumber;

        var matches = await _db.Accounts.Where(item => item.TenantId == tenantId && !item.IsDeleted
                && (item.AccountCode == accountCode || item.AccountNumber == accountNumber))
            .ToListAsync(cancellationToken);
        if (matches.Select(item => item.Id).Distinct().Count() > 1)
            throw new InvalidOperationException("Finance account provisioning found an ambiguous stable account code or number.");

        var account = matches.SingleOrDefault();
        var wasCreated = account == null;
        if (account == null)
        {
            account = new Account
            {
                TenantId = tenantId,
                AccountCode = accountCode,
                AccountNumber = accountNumber,
                AccountName = accountName,
                AccountType = request.CoreAccountType,
                CurrencyCode = currencyCode,
                Description = string.IsNullOrWhiteSpace(request.Description) ? string.Empty : request.Description.Trim(),
                IsSegmented = true,
                AllowDirectPosting = true,
                IsSystemAccount = true,
                Status = AccountStatus.Active,
                ReferenceNumber = accountCode,
                EffectiveDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserName ?? "system"
            };
            foreach (var value in identity.Values)
            {
                account.SegmentValues.Add(new AccountSegmentValue
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = account.Id,
                    SegmentStructureId = value.SegmentStructureId, SegmentPosition = value.SegmentPosition,
                    SegmentValue = value.SegmentValue, SegmentLookupValueId = value.SegmentLookupValueId,
                    EffectiveDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserName ?? "system"
                });
            }
            _db.Accounts.Add(account);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (account.AccountType != request.CoreAccountType)
                throw new InvalidOperationException("The existing Finance account has a different core account type.");
            var readiness = await _segmentIdentity.GetReadinessAsync(tenantId, account.Id, cancellationToken);
            if (!readiness.IsReady)
                throw new InvalidOperationException(
                    $"Existing Finance account '{account.AccountCode}' is not ready for the active account-number structure: {string.Join("; ", readiness.Issues)}");
        }

        await new FinanceClassificationManifestSeeder(_db, _logger)
            .SeedAsync(tenantId, DateTime.UtcNow, cancellationToken);
        var bookCodes = await _db.AccountAccountingBooks.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.AccountId == account.Id && item.IsEnabled && !item.IsDeleted)
            .Join(_db.AccountingBooks.AsNoTracking(), mapping => mapping.AccountingBookId, book => book.Id,
                (_, book) => book.Code)
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        var result = new ProvisionedFinanceAccountDto
        {
            AccountId = account.Id,
            AccountCode = account.AccountCode,
            ClassificationCode = classificationCode,
            AccountingBookCodes = bookCodes,
            WasCreated = wasCreated
        };
        if (transaction != null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static string NormalizeRequired(string? value, string label, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
            throw new InvalidOperationException($"{label} is required and cannot exceed {maxLength} characters.");
        return normalized;
    }
}
