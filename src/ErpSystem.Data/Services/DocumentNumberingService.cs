using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Numbering;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Numbering;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Services;

public class DocumentNumberingService : IDocumentNumberingService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserProvider _currentUser;

    public DocumentNumberingService(ApplicationDbContext context, ICurrentUserProvider currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<string> GenerateAsync(
        string module,
        string documentType,
        Guid? tenantId = null,
        DateTime? documentDate = null,
        string? entityType = null,
        Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = ResolveTenantId(tenantId);
        var effectiveDate = documentDate ?? DateTime.UtcNow;

        if (_context.Database.CurrentTransaction != null)
        {
            return await GenerateCoreAsync(
                module,
                documentType,
                resolvedTenantId,
                effectiveDate,
                entityType,
                entityId,
                cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var documentNumber = await GenerateCoreAsync(
                module,
                documentType,
                resolvedTenantId,
                effectiveDate,
                entityType,
                entityId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return documentNumber;
        });
    }

    private async Task<string> GenerateCoreAsync(
        string module,
        string documentType,
        Guid resolvedTenantId,
        DateTime effectiveDate,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultsAsync(resolvedTenantId, cancellationToken);

        var definition = await _context.DocumentSequenceDefinitions
            .Where(d => d.TenantId == resolvedTenantId
                && d.Module == module
                && d.DocumentType == documentType
                && d.IsActive
                && d.IsDefault
                && !d.IsDeleted
                && (d.EffectiveFrom == null || d.EffectiveFrom <= effectiveDate)
                && (d.EffectiveTo == null || d.EffectiveTo >= effectiveDate))
            .OrderByDescending(d => d.EffectiveFrom ?? DateTime.MinValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (definition == null)
        {
            throw new InvalidOperationException($"No active document sequence is configured for {module}/{documentType}.");
        }

        var periodKey = GetPeriodKey(definition.ResetPolicy, effectiveDate);
        if (definition.LastResetPeriodKey != periodKey)
        {
            definition.NextNumber = definition.StartNumber;
            definition.LastResetPeriodKey = periodKey;
        }

        if (!await _context.DocumentNumberReservations.AnyAsync(r => r.DocumentSequenceDefinitionId == definition.Id, cancellationToken))
        {
            var legacyNextNumber = await CalculateNextNumberFromExistingDocumentsAsync(definition, effectiveDate, cancellationToken);
            if (legacyNextNumber > definition.NextNumber)
            {
                definition.NextNumber = legacyNextNumber;
            }
        }

        var sequenceNumber = definition.NextNumber;
        var documentNumber = FormatNumber(definition, effectiveDate, sequenceNumber);

        definition.NextNumber++;
        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedBy = _currentUser.Username;

        _context.DocumentNumberReservations.Add(new DocumentNumberReservation
        {
            TenantId = resolvedTenantId,
            DocumentSequenceDefinitionId = definition.Id,
            Module = module,
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            SequenceNumber = sequenceNumber,
            PeriodKey = periodKey,
            EntityType = entityType,
            EntityId = entityId,
            Status = DocumentNumberReservationStatuses.Used,
            ReservedBy = _currentUser.Username,
            ReservedAt = DateTime.UtcNow,
            UsedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return documentNumber;
    }

    public async Task<IReadOnlyList<DocumentSequenceDefinitionDto>> GetDefinitionsAsync(
        string? module = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = ResolveTenantId(tenantId);
        await EnsureDefaultsAsync(resolvedTenantId, cancellationToken);

        var query = _context.DocumentSequenceDefinitions
            .AsNoTracking()
            .Where(d => d.TenantId == resolvedTenantId && !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(d => d.Module == module);
        }

        return await query
            .OrderBy(d => d.Module)
            .ThenBy(d => d.DocumentType)
            .Select(d => ToDto(d))
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentSequenceDefinitionDto> UpdateDefinitionAsync(
        Guid id,
        UpdateDocumentSequenceDefinitionDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId(null);
        var definition = await _context.DocumentSequenceDefinitions
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Document sequence definition was not found.");

        if (!string.IsNullOrWhiteSpace(dto.Name)) definition.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Format)) definition.Format = dto.Format.Trim();
        if (dto.NextNumber.HasValue) definition.NextNumber = dto.NextNumber.Value;
        if (dto.StartNumber.HasValue) definition.StartNumber = dto.StartNumber.Value;
        if (dto.MinimumDigits.HasValue) definition.MinimumDigits = dto.MinimumDigits.Value;
        if (!string.IsNullOrWhiteSpace(dto.ResetPolicy)) definition.ResetPolicy = dto.ResetPolicy.Trim();
        if (dto.IsContinuous.HasValue) definition.IsContinuous = dto.IsContinuous.Value;
        if (dto.AllowManualEntry.HasValue) definition.AllowManualEntry = dto.AllowManualEntry.Value;
        if (dto.IsActive.HasValue) definition.IsActive = dto.IsActive.Value;
        if (dto.IsDefault.HasValue) definition.IsDefault = dto.IsDefault.Value;
        if (dto.EffectiveFrom.HasValue) definition.EffectiveFrom = dto.EffectiveFrom.Value;
        if (dto.EffectiveTo.HasValue) definition.EffectiveTo = dto.EffectiveTo.Value;
        if (dto.Description != null) definition.Description = dto.Description.Trim();

        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedBy = _currentUser.Username;

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(definition);
    }

    public async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var defaults = DocumentSequenceDefaults.Create(tenantId);
        var existingKeys = await _context.DocumentSequenceDefinitions
            .Where(d => d.TenantId == tenantId && !d.IsDeleted)
            .Select(d => d.Module + "|" + d.DocumentType)
            .ToListAsync(cancellationToken);

        var existing = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = defaults
            .Where(d => !existing.Contains(d.Module + "|" + d.DocumentType))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var definition in missing)
        {
            definition.CreatedBy = _currentUser.Username;
            definition.LastResetPeriodKey = GetPeriodKey(definition.ResetPolicy, DateTime.UtcNow);
        }

        _context.DocumentSequenceDefinitions.AddRange(missing);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private Guid ResolveTenantId(Guid? tenantId)
    {
        var resolved = tenantId ?? _currentUser.TenantId;
        if (resolved == Guid.Empty)
        {
            throw new InvalidOperationException("A tenant is required to generate document numbers.");
        }

        return resolved;
    }

    private async Task<long> CalculateNextNumberFromExistingDocumentsAsync(
        DocumentSequenceDefinition definition,
        DateTime documentDate,
        CancellationToken cancellationToken)
    {
        var numbers = await GetExistingNumbersAsync(definition, cancellationToken);
        if (numbers.Count == 0)
        {
            return definition.NextNumber;
        }

        var regex = BuildRegex(definition, documentDate);
        var max = numbers
            .Select(n => regex.Match(n))
            .Where(m => m.Success)
            .Select(m => long.TryParse(m.Groups["seq"].Value, out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();

        return max + 1;
    }

    private Task<List<string>> GetExistingNumbersAsync(DocumentSequenceDefinition definition, CancellationToken cancellationToken)
    {
        var tenantId = definition.TenantId;

        return (definition.Module, definition.DocumentType) switch
        {
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.JournalEntry) => _context.Set<JournalEntry>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.JournalEntryNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.UnitJournalEntry) => _context.Set<UnitJournalEntry>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.EntryNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.ARInvoice) => _context.Set<Invoice>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.InvoiceNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.ARPayment) or
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.ARCreditNote) => _context.Set<CustomerPayment>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.PaymentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.ARAdjustmentJournal) or
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.APAdjustmentJournal) => _context.Set<SubledgerAdjustmentJournal>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.AdjustmentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.APInvoice) => _context.Set<VendorInvoice>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.InvoiceNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.APPayment) => _context.Set<VendorPayment>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.PaymentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.APPaymentBatch) => _context.Set<PaymentBatch>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.BatchNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.CashReceipt) or
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.CashPayment) or
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.BankTransfer) => _context.Set<CashTransaction>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.TransactionNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.AssetTransfer) => _context.Set<AssetTransfer>()
                .Where(e => e.TenantId == tenantId && e.ReferenceNumber != null)
                .Select(e => e.ReferenceNumber!)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.AssetVerification) => _context.Set<AssetVerificationSession>()
                .Where(e => e.TenantId == tenantId && e.ReferenceNumber != null)
                .Select(e => e.ReferenceNumber!)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.AssetDisposal) => _context.Set<AssetDisposal>()
                .Where(e => e.TenantId == tenantId && e.ReferenceNumber != null)
                .Select(e => e.ReferenceNumber!)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.FinancePurchaseOrder) => _context.Set<FinancePurchaseOrder>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.OrderNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.FinancePurchaseOrderReceipt) => _context.Set<FinancePurchaseOrderReceipt>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.ReceiptNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.FixedAssetJournal) or
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.LeaseJournal) or
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.CurrencyRevaluation) or
            (DocumentNumberingModules.Finance, FinanceDocumentTypes.YearEndClose) => _context.Set<JournalEntry>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.JournalEntryNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.Quote) => _context.Set<Quote>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.DocumentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.SalesOrder) => _context.Set<SalesOrder>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.DocumentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.DeliveryNote) => _context.Set<DeliveryNote>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.DocumentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.SalesAgreement) => _context.Set<SalesAgreement>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.DocumentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.ReturnOrder) => _context.Set<ReturnOrder>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.DocumentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.CreditNote) => _context.Set<CreditNote>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.DocumentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.Refund) => _context.Set<Refund>()
                .Where(e => e.TenantId == tenantId)
                .Select(e => e.DocumentNumber)
                .ToListAsync(cancellationToken),
            (DocumentNumberingModules.Sales, SalesDocumentTypes.CommissionStatement) => _context.Set<CommissionStatement>()
                .Select(e => e.StatementNumber)
                .ToListAsync(cancellationToken),
            _ => Task.FromResult(new List<string>())
        };
    }

    private static string FormatNumber(DocumentSequenceDefinition definition, DateTime documentDate, long sequenceNumber)
    {
        var formatted = definition.Format
            .Replace("{YYYY}", documentDate.ToString("yyyy"), StringComparison.OrdinalIgnoreCase)
            .Replace("{YY}", documentDate.ToString("yy"), StringComparison.OrdinalIgnoreCase)
            .Replace("{MM}", documentDate.ToString("MM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{DD}", documentDate.ToString("dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{SEQ}", sequenceNumber.ToString().PadLeft(definition.MinimumDigits, '0'), StringComparison.OrdinalIgnoreCase);

        return Regex.Replace(formatted, @"\{(#+)\}", match => sequenceNumber.ToString().PadLeft(match.Groups[1].Value.Length, '0'));
    }

    private static Regex BuildRegex(DocumentSequenceDefinition definition, DateTime documentDate)
    {
        var pattern = new StringBuilder();
        var format = definition.Format;

        for (var i = 0; i < format.Length; i++)
        {
            if (format.Substring(i).StartsWith("{YYYY}", StringComparison.OrdinalIgnoreCase))
            {
                pattern.Append(Regex.Escape(documentDate.ToString("yyyy")));
                i += 5;
            }
            else if (format.Substring(i).StartsWith("{YY}", StringComparison.OrdinalIgnoreCase))
            {
                pattern.Append(Regex.Escape(documentDate.ToString("yy")));
                i += 3;
            }
            else if (format.Substring(i).StartsWith("{MM}", StringComparison.OrdinalIgnoreCase))
            {
                pattern.Append(Regex.Escape(documentDate.ToString("MM")));
                i += 3;
            }
            else if (format.Substring(i).StartsWith("{DD}", StringComparison.OrdinalIgnoreCase))
            {
                pattern.Append(Regex.Escape(documentDate.ToString("dd")));
                i += 3;
            }
            else if (format.Substring(i).StartsWith("{SEQ}", StringComparison.OrdinalIgnoreCase))
            {
                pattern.Append("(?<seq>\\d+)");
                i += 4;
            }
            else if (format[i] == '{')
            {
                var end = format.IndexOf('}', i);
                var token = end >= 0 ? format.Substring(i + 1, end - i - 1) : string.Empty;
                if (token.Length > 0 && token.All(c => c == '#'))
                {
                    pattern.Append("(?<seq>\\d+)");
                    i = end;
                }
                else
                {
                    pattern.Append(Regex.Escape(format[i].ToString()));
                }
            }
            else
            {
                pattern.Append(Regex.Escape(format[i].ToString()));
            }
        }

        return new Regex("^" + pattern + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static string GetPeriodKey(string resetPolicy, DateTime documentDate)
    {
        return resetPolicy switch
        {
            DocumentSequenceResetPolicies.Yearly => documentDate.ToString("yyyy"),
            DocumentSequenceResetPolicies.Monthly => documentDate.ToString("yyyyMM"),
            _ => "ALL"
        };
    }

    private static DocumentSequenceDefinitionDto ToDto(DocumentSequenceDefinition definition)
    {
        return new DocumentSequenceDefinitionDto
        {
            Id = definition.Id,
            TenantId = definition.TenantId,
            Module = definition.Module,
            DocumentType = definition.DocumentType,
            Name = definition.Name,
            Format = definition.Format,
            NextNumber = definition.NextNumber,
            StartNumber = definition.StartNumber,
            MinimumDigits = definition.MinimumDigits,
            ResetPolicy = definition.ResetPolicy,
            LastResetPeriodKey = definition.LastResetPeriodKey,
            IsContinuous = definition.IsContinuous,
            AllowManualEntry = definition.AllowManualEntry,
            IsActive = definition.IsActive,
            IsDefault = definition.IsDefault,
            EffectiveFrom = definition.EffectiveFrom,
            EffectiveTo = definition.EffectiveTo,
            Description = definition.Description
        };
    }
}
