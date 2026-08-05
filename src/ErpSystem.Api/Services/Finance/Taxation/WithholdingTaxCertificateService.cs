using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Taxation;

/// <summary>
/// Owns the Finance-only WHT compliance lifecycle around canonical AP payments. It reuses the
/// posted payment, configured tax, GL account and journal as sources of truth; certificates and
/// remittances are immutable evidence/read models and never become alternative posting sources.
/// </summary>
public sealed class WithholdingTaxCertificateService : IWithholdingTaxCertificateService
{
    private const string PostedStatus = "Posted";
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;

    public WithholdingTaxCertificateService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName) ? "system" : _currentUser.UserName!;
    private string? UserId => string.IsNullOrWhiteSpace(_currentUser.UserId) ? null : _currentUser.UserId;

    public async Task<PagedResult<WhtCertificateDto>> GetApCertificatesAsync(
        WhtCertificateQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new WhtCertificateQueryDto();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var payments = BuildEligibleApPaymentQuery(query);
        var normalizedStatus = NormalizeStatus(query.Status);

        // Lifecycle status belongs to the immutable certificate versions, not to the legacy
        // convenience fields on VendorPayment. Correlated predicates keep filtering in SQL.
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            payments = normalizedStatus switch
            {
                "Missing" => payments.Where(payment => !_context.WithholdingTaxCertificates.Any(certificate =>
                    certificate.TenantId == TenantId && !certificate.IsDeleted && certificate.VendorPaymentId == payment.Id)),
                "Issued" => payments.Where(payment => _context.WithholdingTaxCertificates.Any(certificate =>
                    certificate.TenantId == TenantId && !certificate.IsDeleted && certificate.VendorPaymentId == payment.Id
                    && certificate.Status == WhtCertificateStatus.Issued)),
                "Cancelled" => payments.Where(payment =>
                    !_context.WithholdingTaxCertificates.Any(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted
                        && certificate.VendorPaymentId == payment.Id && certificate.Status == WhtCertificateStatus.Issued)
                    && _context.WithholdingTaxCertificates.Any(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted
                        && certificate.VendorPaymentId == payment.Id && certificate.Status == WhtCertificateStatus.Cancelled)),
                _ => payments
            };
        }

        var totalCount = await payments.CountAsync(cancellationToken);
        var items = await payments
            .OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var paymentIds = items.Select(payment => payment.Id).ToList();
        var versions = await LoadCertificateVersionsAsync(paymentIds, cancellationToken);
        var remittances = await LoadActiveRemittancesAsync(paymentIds, cancellationToken);

        return new PagedResult<WhtCertificateDto>
        {
            Items = items.Select(payment => MapToDto(
                payment,
                versions.GetValueOrDefault(payment.Id) ?? new List<WithholdingTaxCertificate>(),
                remittances.GetValueOrDefault(payment.Id))).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WhtCertificateDto?> GetApCertificateAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadEligibleApPaymentAsync(vendorPaymentId, asTracking: false, cancellationToken);
        if (payment == null)
        {
            return null;
        }

        await EnsurePaymentJournalIsPostedAsync(payment, cancellationToken);
        var versions = await LoadCertificateVersionsAsync(new[] { payment.Id }, cancellationToken);
        var remittances = await LoadActiveRemittancesAsync(new[] { payment.Id }, cancellationToken);
        return MapToDto(
            payment,
            versions.GetValueOrDefault(payment.Id) ?? new List<WithholdingTaxCertificate>(),
            remittances.GetValueOrDefault(payment.Id));
    }

    public Task<WhtCertificateDto> GenerateApCertificateAsync(
        Guid vendorPaymentId,
        GenerateWhtCertificateDto dto,
        CancellationToken cancellationToken = default)
        => ExecuteSerializableAsync(
            () => IssueCertificateCoreAsync(vendorPaymentId, dto ?? new GenerateWhtCertificateDto(), cancellationToken),
            cancellationToken);

    public Task<WhtCertificateDto> ReissueApCertificateAsync(
        Guid vendorPaymentId,
        ReissueWhtCertificateDto dto,
        CancellationToken cancellationToken = default)
        => ExecuteSerializableAsync(
            () => ReissueCertificateCoreAsync(vendorPaymentId, dto ?? new ReissueWhtCertificateDto(), cancellationToken),
            cancellationToken);

    public Task<WhtCertificateDto> CancelApCertificateAsync(
        Guid vendorPaymentId,
        CancelWhtCertificateDto dto,
        CancellationToken cancellationToken = default)
        => ExecuteSerializableAsync(
            () => CancelCertificateCoreAsync(vendorPaymentId, dto ?? new CancelWhtCertificateDto(), cancellationToken),
            cancellationToken);

    public async Task<string> GetApCertificateHtmlAsync(
        Guid vendorPaymentId,
        Guid? certificateId = null,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadEligibleApPaymentAsync(vendorPaymentId, asTracking: false, cancellationToken)
            ?? throw new InvalidOperationException("AP WHT payment was not found.");
        await EnsurePaymentJournalIsPostedAsync(payment, cancellationToken);

        var certificateQuery = _context.WithholdingTaxCertificates
            .AsNoTracking()
            .Where(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted
                && certificate.VendorPaymentId == vendorPaymentId);
        var certificate = certificateId.HasValue
            ? await certificateQuery.FirstOrDefaultAsync(item => item.Id == certificateId.Value, cancellationToken)
            : await certificateQuery
                .OrderByDescending(item => item.Status == WhtCertificateStatus.Issued)
                .ThenByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
        if (certificate == null)
        {
            throw new InvalidOperationException("Issue a WHT certificate before printing.");
        }

        await RecordCertificateAuditAsync(
            FinanceAuditEvents.WithholdingCertificatePrinted,
            payment,
            null,
            new { certificate.Id, certificate.CertificateNumber, certificate.VersionNumber, Status = certificate.Status.ToString() },
            "AP WHT certificate print view generated from its immutable issued snapshot.",
            cancellationToken);
        return BuildCertificateHtml(certificate);
    }

    public async Task<WhtCalculationResultDto> CalculateApWithholdingAsync(
        WhtCalculationRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        dto ??= new WhtCalculationRequestDto();
        if (dto.TaxId == Guid.Empty || dto.SupplierId == Guid.Empty)
        {
            throw new InvalidOperationException("A configured WHT tax and supplier are required.");
        }

        if (dto.TaxableBase < 0m)
        {
            throw new InvalidOperationException("WHT taxable base cannot be negative.");
        }

        var supplierExists = await _context.Set<Supplier>().AsNoTracking().AnyAsync(supplier =>
            supplier.TenantId == TenantId && !supplier.IsDeleted && supplier.Id == dto.SupplierId,
            cancellationToken);
        if (!supplierExists)
        {
            throw new InvalidOperationException("Supplier was not found for this tenant.");
        }

        var tax = await _context.Taxes.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && !item.IsDeleted && item.IsActive && item.Id == dto.TaxId
            && item.Category == TaxCategory.Withholding
            && (item.Applicability == TaxApplicability.Purchases || item.Applicability == TaxApplicability.Both),
            cancellationToken)
            ?? throw new InvalidOperationException("The selected tax is not an active purchase WHT configuration for this tenant.");

        var paymentDate = dto.PaymentDate == default ? DateTime.UtcNow.Date : dto.PaymentDate.Date;
        var effectiveRate = await ResolveEffectiveRateAsync(tax, paymentDate, cancellationToken)
            ?? throw new InvalidOperationException($"WHT tax {tax.Code} is not effective on {paymentDate:yyyy-MM-dd}.");
        var fiscalYearStart = new DateTime(paymentDate.Year, 1, 1);
        var fiscalYearEnd = fiscalYearStart.AddYears(1);

        // Payments below the threshold still carry TaxId/BaseAmount and therefore contribute to
        // the annual supplier aggregate. Cancelled/reversed/failed records are excluded because
        // they no longer represent an eligible statutory payment.
        var cumulativeQuery = _context.Set<VendorPayment>().AsNoTracking().Where(payment =>
            payment.TenantId == TenantId && !payment.IsDeleted
            && payment.SupplierId == dto.SupplierId && payment.WithholdingTaxId == dto.TaxId
            && payment.PaymentDate >= fiscalYearStart && payment.PaymentDate < fiscalYearEnd
            && payment.Status != VendorPaymentStatus.Voided
            && payment.Status != VendorPaymentStatus.Reversed
            && payment.Status != VendorPaymentStatus.Failed);
        if (dto.ExcludeVendorPaymentId.HasValue)
        {
            cumulativeQuery = cumulativeQuery.Where(payment => payment.Id != dto.ExcludeVendorPaymentId.Value);
        }

        var cumulativeBefore = RoundMoney(await cumulativeQuery.SumAsync(
            payment => payment.WithholdingTaxBaseAmount,
            cancellationToken));
        var taxableBase = RoundMoney(dto.TaxableBase);
        var cumulativeAfter = RoundMoney(cumulativeBefore + taxableBase);
        decimal? threshold = tax.ThresholdAmount is > 0m ? RoundMoney(tax.ThresholdAmount.Value) : null;
        var thresholdApplied = taxableBase > 0m && (!threshold.HasValue || cumulativeAfter >= threshold.Value);
        var withholdingAmount = thresholdApplied
            ? RoundMoney(taxableBase * effectiveRate / 100m)
            : 0m;
        var remaining = threshold.HasValue
            ? Math.Max(RoundMoney(threshold.Value - cumulativeAfter), 0m)
            : 0m;
        var note = !threshold.HasValue
            ? $"{tax.Code} has no minimum threshold; {effectiveRate:N4}% applies to the full taxable base."
            : thresholdApplied
                ? $"Annual supplier aggregate {cumulativeAfter:N2} meets/exceeds the configured {threshold.Value:N2} threshold; {effectiveRate:N4}% applies to the full current payment base."
                : $"Annual supplier aggregate {cumulativeAfter:N2} remains below the configured {threshold.Value:N2} threshold; no WHT is deducted."
;

        return new WhtCalculationResultDto
        {
            TaxId = tax.Id,
            TaxCode = tax.Code,
            TaxName = tax.Name,
            TaxRate = RoundRate(effectiveRate),
            TaxableBase = taxableBase,
            CumulativeBefore = cumulativeBefore,
            CumulativeAfter = cumulativeAfter,
            ThresholdAmount = threshold,
            RemainingBeforeThreshold = remaining,
            ThresholdApplied = thresholdApplied,
            WithholdingAmount = withholdingAmount,
            TaxPayableAccountId = tax.TaxPayableAccountId,
            CalculationNote = note
        };
    }

    public async Task<IReadOnlyList<WhtRemittanceLiabilityDto>> GetUnremittedLiabilitiesAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string? currencyCode,
        CancellationToken cancellationToken = default)
    {
        var query = BuildUnremittedLiabilityQuery(fromDate, toDate, currencyCode);
        var payments = await query
            .OrderBy(payment => payment.PaymentDate)
            .ThenBy(payment => payment.PaymentNumber)
            .Take(1000)
            .ToListAsync(cancellationToken);
        var versions = await LoadCertificateVersionsAsync(payments.Select(payment => payment.Id), cancellationToken);

        return payments.Select(payment =>
        {
            var activeCertificate = (versions.GetValueOrDefault(payment.Id) ?? new List<WithholdingTaxCertificate>())
                .FirstOrDefault(certificate => certificate.Status == WhtCertificateStatus.Issued);
            return new WhtRemittanceLiabilityDto
            {
                VendorPaymentId = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                PaymentDate = payment.PaymentDate,
                SupplierId = payment.SupplierId,
                SupplierName = payment.Supplier?.Name ?? string.Empty,
                SupplierTin = payment.Supplier?.TaxId,
                CurrencyCode = NormalizeCurrency(payment.CurrencyCode),
                TaxCode = payment.WithholdingTax?.Code,
                TaxableBase = ResolveTaxableBase(payment),
                WithholdingAmount = RoundMoney(payment.WithholdingTaxAmount),
                CertificateId = activeCertificate?.Id,
                CertificateNumber = activeCertificate?.CertificateNumber,
                JournalEntryId = payment.JournalEntryId
            };
        }).ToList();
    }

    public async Task<PagedResult<WhtRemittanceDto>> GetRemittancesAsync(
        WhtRemittanceQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new WhtRemittanceQueryDto();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var remittances = _context.WithholdingTaxRemittances.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted);
        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            remittances = remittances.Where(item => item.PeriodTo >= from);
        }
        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.Date;
            remittances = remittances.Where(item => item.PeriodFrom <= to);
        }
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<WhtRemittanceStatus>(query.Status.Trim(), true, out var status))
        {
            remittances = remittances.Where(item => item.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            remittances = remittances.Where(item => item.RemittanceNumber.Contains(term)
                || (item.SubmissionReference != null && item.SubmissionReference.Contains(term))
                || (item.PaymentReference != null && item.PaymentReference.Contains(term)));
        }

        var totalCount = await remittances.CountAsync(cancellationToken);
        var items = await remittances
            .Include(item => item.Lines.Where(line => !line.IsDeleted))
            .OrderByDescending(item => item.PeriodTo)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<WhtRemittanceDto>
        {
            Items = items.Select(MapRemittance).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WhtRemittanceDto?> GetRemittanceAsync(
        Guid remittanceId,
        CancellationToken cancellationToken = default)
    {
        var remittance = await _context.WithholdingTaxRemittances.AsNoTracking()
            .Include(item => item.Lines.Where(line => !line.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Id == remittanceId, cancellationToken);
        return remittance == null ? null : MapRemittance(remittance);
    }

    public Task<WhtRemittanceDto> CreateRemittanceAsync(
        CreateWhtRemittanceDto dto,
        CancellationToken cancellationToken = default)
        => ExecuteSerializableAsync(() => CreateRemittanceCoreAsync(dto ?? new CreateWhtRemittanceDto(), cancellationToken), cancellationToken);

    public async Task<WhtRemittanceDto> SubmitRemittanceAsync(
        Guid remittanceId,
        SubmitWhtRemittanceDto dto,
        CancellationToken cancellationToken = default)
    {
        dto ??= new SubmitWhtRemittanceDto();
        var reference = RequireReason(dto.SubmissionReference, "Submission reference", 3);
        var remittance = await LoadRemittanceForUpdateAsync(remittanceId, cancellationToken);
        if (remittance.Status != WhtRemittanceStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft WHT remittance can be submitted.");
        }
        if (remittance.Lines.Count == 0)
        {
            throw new InvalidOperationException("A WHT remittance must contain at least one liability.");
        }

        var before = RemittanceAuditSnapshot(remittance);
        remittance.Status = WhtRemittanceStatus.Submitted;
        remittance.SubmissionReference = reference;
        remittance.SubmittedAtUtc = DateTime.UtcNow;
        remittance.SubmittedByName = UserName;
        remittance.SubmittedByUserId = UserId;
        remittance.UpdatedAt = DateTime.UtcNow;
        remittance.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordRemittanceAuditAsync(FinanceAuditEvents.WhtRemittanceSubmitted, remittance, before, RemittanceAuditSnapshot(remittance), cancellationToken);
        return MapRemittance(remittance);
    }

    public async Task<WhtRemittanceDto> MarkRemittancePaidAsync(
        Guid remittanceId,
        PayWhtRemittanceDto dto,
        CancellationToken cancellationToken = default)
    {
        dto ??= new PayWhtRemittanceDto();
        var paymentReference = RequireReason(dto.PaymentReference, "Payment reference", 3);
        if (dto.PaymentDate == default)
        {
            throw new InvalidOperationException("Remittance payment date is required.");
        }

        var remittance = await LoadRemittanceForUpdateAsync(remittanceId, cancellationToken);
        if (remittance.Status != WhtRemittanceStatus.Submitted)
        {
            throw new InvalidOperationException("Only a submitted WHT remittance can be marked paid.");
        }
        if (dto.PaymentDate.Date < remittance.PeriodFrom.Date)
        {
            throw new InvalidOperationException("Remittance payment date cannot precede the liability period.");
        }

        var before = RemittanceAuditSnapshot(remittance);
        remittance.Status = WhtRemittanceStatus.Paid;
        remittance.PaymentReference = paymentReference;
        remittance.PaymentDate = dto.PaymentDate.Date;
        remittance.AuthorityReceiptReference = Normalize(dto.AuthorityReceiptReference);
        remittance.PaidAtUtc = DateTime.UtcNow;
        remittance.PaidByName = UserName;
        remittance.PaidByUserId = UserId;
        remittance.UpdatedAt = DateTime.UtcNow;
        remittance.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordRemittanceAuditAsync(FinanceAuditEvents.WhtRemittancePaid, remittance, before, RemittanceAuditSnapshot(remittance), cancellationToken);
        return MapRemittance(remittance);
    }

    public async Task<WhtRemittanceDto> CancelRemittanceAsync(
        Guid remittanceId,
        CancelWhtRemittanceDto dto,
        CancellationToken cancellationToken = default)
    {
        dto ??= new CancelWhtRemittanceDto();
        var reason = RequireReason(dto.Reason, "Cancellation reason", 10);
        var remittance = await LoadRemittanceForUpdateAsync(remittanceId, cancellationToken);
        if (remittance.Status == WhtRemittanceStatus.Paid)
        {
            throw new InvalidOperationException("A paid remittance cannot be cancelled. Record the statutory correction and payment reversal through the approved Finance correction path.");
        }
        if (remittance.Status == WhtRemittanceStatus.Cancelled)
        {
            return MapRemittance(remittance);
        }

        var before = RemittanceAuditSnapshot(remittance);
        remittance.Status = WhtRemittanceStatus.Cancelled;
        remittance.CancelledAtUtc = DateTime.UtcNow;
        remittance.CancelledByName = UserName;
        remittance.CancelledByUserId = UserId;
        remittance.CancellationReason = reason;
        remittance.UpdatedAt = DateTime.UtcNow;
        remittance.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordRemittanceAuditAsync(FinanceAuditEvents.WhtRemittanceCancelled, remittance, before, RemittanceAuditSnapshot(remittance), cancellationToken);
        return MapRemittance(remittance);
    }

    public async Task<WhtRegisterExportDto> ExportRegisterAsync(
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var query = BuildEligibleApPaymentQuery(new WhtCertificateQueryDto { FromDate = fromDate, ToDate = toDate });
        var payments = await query.OrderBy(item => item.PaymentDate).ThenBy(item => item.PaymentNumber).ToListAsync(cancellationToken);
        var paymentIds = payments.Select(item => item.Id).ToList();
        var versions = await LoadCertificateVersionsAsync(paymentIds, cancellationToken);
        var remittances = await LoadActiveRemittancesAsync(paymentIds, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Payment Date,Payment Number,Supplier,Supplier TIN,Tax Code,Tax Rate,Taxable Base,WHT Amount,Currency,Journal Entry,Certificate Number,Certificate Version,Certificate Status,Issue Date,Remittance Number,Remittance Status,Submission Reference,Remittance Payment Reference");
        foreach (var payment in payments)
        {
            var paymentVersions = versions.GetValueOrDefault(payment.Id) ?? new List<WithholdingTaxCertificate>();
            var current = paymentVersions.FirstOrDefault(item => item.Status == WhtCertificateStatus.Issued)
                ?? paymentVersions.OrderByDescending(item => item.VersionNumber).FirstOrDefault();
            var remittance = remittances.GetValueOrDefault(payment.Id);
            builder.AppendLine(string.Join(',', new[]
            {
                Csv(payment.PaymentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(payment.PaymentNumber),
                Csv(payment.Supplier?.Name),
                Csv(payment.Supplier?.TaxId),
                Csv(payment.WithholdingTax?.Code),
                Csv(RoundRate(payment.WithholdingTaxRate != 0m ? payment.WithholdingTaxRate : payment.WithholdingTax?.Rate ?? 0m).ToString("0.####", CultureInfo.InvariantCulture)),
                Csv(ResolveTaxableBase(payment).ToString("0.00", CultureInfo.InvariantCulture)),
                Csv(RoundMoney(payment.WithholdingTaxAmount).ToString("0.00", CultureInfo.InvariantCulture)),
                Csv(NormalizeCurrency(payment.CurrencyCode)),
                Csv(payment.JournalEntryId?.ToString()),
                Csv(current?.CertificateNumber),
                Csv(current?.VersionNumber.ToString(CultureInfo.InvariantCulture)),
                Csv(current?.Status.ToString() ?? "Missing"),
                Csv(current?.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(remittance?.RemittanceNumber),
                Csv(remittance?.Status.ToString() ?? "Unremitted"),
                Csv(remittance?.SubmissionReference),
                Csv(remittance?.PaymentReference)
            }));
        }

        var rangeFrom = fromDate?.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "all";
        var rangeTo = toDate?.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "all";
        var export = new WhtRegisterExportDto
        {
            Content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString()),
            FileName = $"wht-statutory-register-{rangeFrom}-{rangeTo}.csv"
        };
        if (_financeAuditService != null)
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = FinanceAuditEvents.WhtCertificateRegisterExported,
                TenantId = TenantId,
                SourceModule = "Tax",
                SourceDocumentType = "WhtCertificateRegister",
                SourceDocumentId = Guid.Empty,
                AfterValues = new { FromDate = fromDate?.Date, ToDate = toDate?.Date, RowCount = payments.Count, export.FileName },
                Comment = "Finance WHT statutory certificate/remittance register exported.",
                Resource = "Finance.WHTCertificateRegister",
                ResourceId = $"{rangeFrom}:{rangeTo}"
            }, cancellationToken);
        }
        return export;
    }

    private async Task<WhtCertificateDto> IssueCertificateCoreAsync(
        Guid vendorPaymentId,
        GenerateWhtCertificateDto dto,
        CancellationToken cancellationToken)
    {
        var payment = await LoadEligibleApPaymentAsync(vendorPaymentId, asTracking: true, cancellationToken)
            ?? throw new InvalidOperationException("AP WHT payment was not found or is not eligible for certificate issue.");
        await EnsurePaymentJournalIsPostedAsync(payment, cancellationToken);
        var versions = await _context.WithholdingTaxCertificates
            .Where(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted && certificate.VendorPaymentId == payment.Id)
            .OrderByDescending(certificate => certificate.VersionNumber)
            .ToListAsync(cancellationToken);
        var active = versions.FirstOrDefault(certificate => certificate.Status == WhtCertificateStatus.Issued);
        if (active != null)
        {
            var remittances = await LoadActiveRemittancesAsync(new[] { payment.Id }, cancellationToken);
            return MapToDto(payment, versions, remittances.GetValueOrDefault(payment.Id));
        }
        if (versions.Count > 0)
        {
            throw new InvalidOperationException("This payment has certificate history. Use the controlled reissue action instead of issuing a disconnected certificate.");
        }

        var requestedNumber = NormalizeCertificateNumber(dto.CertificateNumber);
        var certificateNumber = requestedNumber
            ?? await GenerateCertificateNumberAsync(payment.PaymentDate, cancellationToken);
        await EnsureCertificateNumberIsUniqueAsync(certificateNumber, cancellationToken);
        var certificate = CreateCertificateSnapshot(payment, certificateNumber, 1, dto.CertificateDate ?? DateTime.UtcNow, null, null);
        await _context.WithholdingTaxCertificates.AddAsync(certificate, cancellationToken);
        UpdatePaymentCertificateProjection(payment, certificate);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordCertificateAuditAsync(
            FinanceAuditEvents.WithholdingCertificateIssued,
            payment,
            null,
            CertificateAuditSnapshot(certificate),
            "AP supplier WHT certificate issued from a posted payment snapshot.",
            cancellationToken);
        return MapToDto(payment, new List<WithholdingTaxCertificate> { certificate }, null);
    }

    private async Task<WhtCertificateDto> ReissueCertificateCoreAsync(
        Guid vendorPaymentId,
        ReissueWhtCertificateDto dto,
        CancellationToken cancellationToken)
    {
        var reason = RequireReason(dto.Reason, "Reissue reason", 10);
        var payment = await LoadEligibleApPaymentAsync(vendorPaymentId, asTracking: true, cancellationToken)
            ?? throw new InvalidOperationException("AP WHT payment was not found or is not eligible for certificate reissue.");
        await EnsurePaymentJournalIsPostedAsync(payment, cancellationToken);
        var versions = await _context.WithholdingTaxCertificates
            .Where(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted && certificate.VendorPaymentId == payment.Id)
            .OrderByDescending(certificate => certificate.VersionNumber)
            .ToListAsync(cancellationToken);
        var current = versions.FirstOrDefault(certificate => certificate.Status == WhtCertificateStatus.Issued)
            ?? throw new InvalidOperationException("Issue the first certificate before using reissue.");
        var activeRemittance = await _context.WithholdingTaxRemittanceLines.AsNoTracking().AnyAsync(line =>
            line.TenantId == TenantId && !line.IsDeleted && line.VendorPaymentId == payment.Id
            && !line.Remittance.IsDeleted && line.Remittance.Status != WhtRemittanceStatus.Cancelled,
            cancellationToken);
        if (activeRemittance)
        {
            // A remittance line is an immutable statutory snapshot of the certificate that
            // accompanied the liability. Reissuing underneath that snapshot would make the
            // certificate register disagree with the submitted/paid evidence batch.
            throw new InvalidOperationException("Cancel or correct the active WHT remittance before reissuing its certificate.");
        }

        var certificateNumber = await GenerateCertificateNumberAsync(payment.PaymentDate, cancellationToken);
        await EnsureCertificateNumberIsUniqueAsync(certificateNumber, cancellationToken);
        var replacement = CreateCertificateSnapshot(
            payment,
            certificateNumber,
            versions.Max(certificate => certificate.VersionNumber) + 1,
            dto.CertificateDate ?? DateTime.UtcNow,
            current.Id,
            reason);
        var before = CertificateAuditSnapshot(current);
        current.Status = WhtCertificateStatus.Superseded;
        current.LifecycleReason = reason;
        current.UpdatedAt = DateTime.UtcNow;
        current.UpdatedBy = UserName;

        // Retire the issued row first so the database's filtered unique index (one issued
        // certificate per payment) is never temporarily violated. The enclosing serializable
        // transaction keeps this three-step lifecycle atomic if a later save fails.
        await _context.SaveChangesAsync(cancellationToken);
        await _context.WithholdingTaxCertificates.AddAsync(replacement, cancellationToken);
        UpdatePaymentCertificateProjection(payment, replacement);
        await _context.SaveChangesAsync(cancellationToken);
        // Set the reverse link only after the replacement exists, otherwise SQL Server would
        // correctly reject the self-referencing foreign key during the retirement save.
        current.SupersededByCertificateId = replacement.Id;
        await _context.SaveChangesAsync(cancellationToken);

        versions.Insert(0, replacement);
        await RecordCertificateAuditAsync(
            FinanceAuditEvents.WithholdingCertificateReissued,
            payment,
            before,
            CertificateAuditSnapshot(replacement),
            reason,
            cancellationToken);
        var remittances = await LoadActiveRemittancesAsync(new[] { payment.Id }, cancellationToken);
        return MapToDto(payment, versions, remittances.GetValueOrDefault(payment.Id));
    }

    private async Task<WhtCertificateDto> CancelCertificateCoreAsync(
        Guid vendorPaymentId,
        CancelWhtCertificateDto dto,
        CancellationToken cancellationToken)
    {
        var reason = RequireReason(dto.Reason, "Cancellation reason", 10);
        var payment = await LoadEligibleApPaymentAsync(vendorPaymentId, asTracking: true, cancellationToken)
            ?? throw new InvalidOperationException("AP WHT payment was not found or is not eligible for certificate cancellation.");
        await EnsurePaymentJournalIsPostedAsync(payment, cancellationToken);
        var versions = await _context.WithholdingTaxCertificates
            .Where(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted && certificate.VendorPaymentId == payment.Id)
            .OrderByDescending(certificate => certificate.VersionNumber)
            .ToListAsync(cancellationToken);
        var current = versions.FirstOrDefault(certificate => certificate.Status == WhtCertificateStatus.Issued)
            ?? throw new InvalidOperationException("There is no issued certificate to cancel.");
        var activeRemittance = await _context.WithholdingTaxRemittanceLines.AsNoTracking().AnyAsync(line =>
            line.TenantId == TenantId && !line.IsDeleted && line.VendorPaymentId == payment.Id
            && !line.Remittance.IsDeleted && line.Remittance.Status != WhtRemittanceStatus.Cancelled,
            cancellationToken);
        if (activeRemittance)
        {
            throw new InvalidOperationException("Cancel or correct the active WHT remittance before cancelling its certificate.");
        }

        var before = CertificateAuditSnapshot(current);
        current.Status = WhtCertificateStatus.Cancelled;
        current.CancelledAtUtc = DateTime.UtcNow;
        current.CancelledByName = UserName;
        current.CancelledByUserId = UserId;
        current.CancellationReason = reason;
        current.UpdatedAt = DateTime.UtcNow;
        current.UpdatedBy = UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordCertificateAuditAsync(
            FinanceAuditEvents.WithholdingCertificateCancelled,
            payment,
            before,
            CertificateAuditSnapshot(current),
            reason,
            cancellationToken);
        return MapToDto(payment, versions, null);
    }

    private async Task<WhtRemittanceDto> CreateRemittanceCoreAsync(
        CreateWhtRemittanceDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.PeriodFrom == default || dto.PeriodTo == default || dto.PeriodTo.Date < dto.PeriodFrom.Date)
        {
            throw new InvalidOperationException("A valid WHT remittance period is required.");
        }
        var paymentIds = dto.VendorPaymentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (paymentIds.Count == 0)
        {
            throw new InvalidOperationException("Select at least one unremitted posted WHT liability.");
        }
        var currency = NormalizeCurrency(dto.CurrencyCode);
        var eligible = await BuildUnremittedLiabilityQuery(dto.PeriodFrom, dto.PeriodTo, currency)
            .Where(payment => paymentIds.Contains(payment.Id))
            .OrderBy(payment => payment.PaymentDate)
            .ThenBy(payment => payment.PaymentNumber)
            .ToListAsync(cancellationToken);
        if (eligible.Count != paymentIds.Count)
        {
            throw new InvalidOperationException("One or more selected WHT liabilities are not posted, fall outside the period/currency, are reversed, or already belong to an active remittance.");
        }
        var versions = await LoadCertificateVersionsAsync(paymentIds, cancellationToken);
        var remittanceNumber = await GenerateRemittanceNumberAsync(dto.PeriodTo, cancellationToken);
        var now = DateTime.UtcNow;
        var dueDate = dto.DueDate?.Date ?? DefaultRemittanceDueDate(dto.PeriodTo.Date);
        if (dueDate < dto.PeriodTo.Date)
        {
            throw new InvalidOperationException("Remittance due date cannot precede the period end.");
        }

        var remittance = new WithholdingTaxRemittance
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RemittanceNumber = remittanceNumber,
            PeriodFrom = dto.PeriodFrom.Date,
            PeriodTo = dto.PeriodTo.Date,
            DueDate = dueDate,
            CurrencyCode = currency,
            Status = WhtRemittanceStatus.Draft,
            TotalWithholdingAmount = RoundMoney(eligible.Sum(payment => payment.WithholdingTaxAmount)),
            Notes = Normalize(dto.Notes),
            CreatedAt = now,
            CreatedBy = UserName
        };
        foreach (var payment in eligible)
        {
            var activeCertificate = (versions.GetValueOrDefault(payment.Id) ?? new List<WithholdingTaxCertificate>())
                .FirstOrDefault(certificate => certificate.Status == WhtCertificateStatus.Issued);
            remittance.Lines.Add(new WithholdingTaxRemittanceLine
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VendorPaymentId = payment.Id,
                CertificateId = activeCertificate?.Id,
                SupplierId = payment.SupplierId,
                TaxId = payment.WithholdingTaxId,
                JournalEntryId = payment.JournalEntryId,
                PaymentNumber = payment.PaymentNumber,
                SupplierName = payment.Supplier?.Name ?? string.Empty,
                SupplierTin = payment.Supplier?.TaxId,
                TaxCode = payment.WithholdingTax?.Code,
                PaymentDate = payment.PaymentDate.Date,
                TaxableBase = ResolveTaxableBase(payment),
                WithholdingAmount = RoundMoney(payment.WithholdingTaxAmount),
                CreatedAt = now,
                CreatedBy = UserName
            });
        }

        await _context.WithholdingTaxRemittances.AddAsync(remittance, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordRemittanceAuditAsync(FinanceAuditEvents.WhtRemittanceCreated, remittance, null, RemittanceAuditSnapshot(remittance), cancellationToken);
        return MapRemittance(remittance);
    }

    private IQueryable<VendorPayment> BuildEligibleApPaymentQuery(WhtCertificateQueryDto query)
    {
        var tenantId = TenantId;
        var payments = _context.Set<VendorPayment>()
            .AsNoTracking()
            .Include(payment => payment.Supplier)
            .Include(payment => payment.WithholdingTax)
            .Include(payment => payment.WithholdingTaxAccount)
            .Where(payment => payment.TenantId == tenantId && !payment.IsDeleted
                && payment.WithholdingTaxAmount > 0m
                && payment.Status != VendorPaymentStatus.Reversed
                && payment.Status != VendorPaymentStatus.Voided
                && payment.JournalEntryId.HasValue
                && _context.Set<JournalEntry>().Any(journal => journal.TenantId == tenantId && !journal.IsDeleted
                    && journal.Id == payment.JournalEntryId && journal.PostingStatus == PostedStatus));
        if (query.SupplierId.HasValue)
        {
            payments = payments.Where(payment => payment.SupplierId == query.SupplierId.Value);
        }
        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            payments = payments.Where(payment => payment.PaymentDate >= from);
        }
        if (query.ToDate.HasValue)
        {
            var toExclusive = query.ToDate.Value.Date.AddDays(1);
            payments = payments.Where(payment => payment.PaymentDate < toExclusive);
        }
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            payments = payments.Where(payment => payment.PaymentNumber.Contains(term)
                || (payment.Supplier != null && payment.Supplier.Name.Contains(term))
                || (payment.Supplier != null && payment.Supplier.TaxId != null && payment.Supplier.TaxId.Contains(term))
                || _context.WithholdingTaxCertificates.Any(certificate => certificate.TenantId == tenantId
                    && !certificate.IsDeleted && certificate.VendorPaymentId == payment.Id
                    && certificate.CertificateNumber.Contains(term)));
        }
        return payments;
    }

    private IQueryable<VendorPayment> BuildUnremittedLiabilityQuery(DateTime? fromDate, DateTime? toDate, string? currencyCode)
    {
        var query = BuildEligibleApPaymentQuery(new WhtCertificateQueryDto { FromDate = fromDate, ToDate = toDate });
        var normalizedCurrency = Normalize(currencyCode)?.ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            query = query.Where(payment => payment.CurrencyCode == normalizedCurrency);
        }
        return query.Where(payment => !_context.WithholdingTaxRemittanceLines.Any(line =>
            line.TenantId == TenantId && !line.IsDeleted && line.VendorPaymentId == payment.Id
            && !line.Remittance.IsDeleted && line.Remittance.Status != WhtRemittanceStatus.Cancelled));
    }

    private async Task<VendorPayment?> LoadEligibleApPaymentAsync(Guid vendorPaymentId, bool asTracking, CancellationToken cancellationToken)
    {
        var query = _context.Set<VendorPayment>()
            .Include(payment => payment.Supplier)
            .Include(payment => payment.WithholdingTax)
            .Include(payment => payment.WithholdingTaxAccount)
            .Where(payment => payment.TenantId == TenantId && !payment.IsDeleted && payment.Id == vendorPaymentId
                && payment.WithholdingTaxAmount > 0m
                && payment.Status != VendorPaymentStatus.Reversed
                && payment.Status != VendorPaymentStatus.Voided);
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task EnsurePaymentJournalIsPostedAsync(VendorPayment payment, CancellationToken cancellationToken)
    {
        if (!payment.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException($"AP payment {payment.PaymentNumber} must be posted before a WHT certificate can be issued.");
        }
        var posted = await _context.Set<JournalEntry>().AsNoTracking().AnyAsync(journal =>
            journal.TenantId == payment.TenantId && !journal.IsDeleted && journal.Id == payment.JournalEntryId.Value
            && journal.PostingStatus == PostedStatus,
            cancellationToken);
        if (!posted)
        {
            throw new InvalidOperationException($"AP payment {payment.PaymentNumber} is not linked to a posted journal.");
        }
    }

    private async Task<Dictionary<Guid, List<WithholdingTaxCertificate>>> LoadCertificateVersionsAsync(
        IEnumerable<Guid> paymentIds,
        CancellationToken cancellationToken)
    {
        var ids = paymentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, List<WithholdingTaxCertificate>>();
        }
        return (await _context.WithholdingTaxCertificates.AsNoTracking()
            .Where(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted && ids.Contains(certificate.VendorPaymentId))
            .OrderByDescending(certificate => certificate.VersionNumber)
            .ToListAsync(cancellationToken))
            .GroupBy(certificate => certificate.VendorPaymentId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private async Task<Dictionary<Guid, WithholdingTaxRemittance>> LoadActiveRemittancesAsync(
        IEnumerable<Guid> paymentIds,
        CancellationToken cancellationToken)
    {
        var ids = paymentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, WithholdingTaxRemittance>();
        }
        var lines = await _context.WithholdingTaxRemittanceLines.AsNoTracking()
            .Include(line => line.Remittance)
            .Where(line => line.TenantId == TenantId && !line.IsDeleted && ids.Contains(line.VendorPaymentId)
                && !line.Remittance.IsDeleted && line.Remittance.Status != WhtRemittanceStatus.Cancelled)
            .ToListAsync(cancellationToken);
        return lines.GroupBy(line => line.VendorPaymentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(line => line.CreatedAt).First().Remittance);
    }

    private async Task<WithholdingTaxRemittance> LoadRemittanceForUpdateAsync(Guid id, CancellationToken cancellationToken)
        => await _context.WithholdingTaxRemittances
            .Include(item => item.Lines.Where(line => !line.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted && item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("WHT remittance was not found for this tenant.");

    private WithholdingTaxCertificate CreateCertificateSnapshot(
        VendorPayment payment,
        string certificateNumber,
        int versionNumber,
        DateTime issueDate,
        Guid? supersedesCertificateId,
        string? lifecycleReason)
    {
        var now = DateTime.UtcNow;
        return new WithholdingTaxCertificate
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            VendorPaymentId = payment.Id,
            CertificateNumber = certificateNumber,
            VersionNumber = versionNumber,
            Status = WhtCertificateStatus.Issued,
            IssueDate = issueDate.Date,
            IssuedAtUtc = now,
            IssuedByName = UserName,
            IssuedByUserId = UserId,
            SupersedesCertificateId = supersedesCertificateId,
            LifecycleReason = lifecycleReason,
            PaymentNumber = payment.PaymentNumber,
            SupplierId = payment.SupplierId,
            SupplierName = payment.Supplier?.Name ?? string.Empty,
            SupplierTin = payment.Supplier?.TaxId,
            PaymentDate = payment.PaymentDate.Date,
            CurrencyCode = NormalizeCurrency(payment.CurrencyCode),
            TaxId = payment.WithholdingTaxId,
            TaxCode = payment.WithholdingTax?.Code,
            TaxName = payment.WithholdingTax?.Name,
            TaxRate = RoundRate(payment.WithholdingTaxRate != 0m ? payment.WithholdingTaxRate : payment.WithholdingTax?.Rate ?? 0m),
            TaxableBase = ResolveTaxableBase(payment),
            WithholdingAmount = RoundMoney(payment.WithholdingTaxAmount),
            NetPaidAmount = RoundMoney(payment.TotalAmount),
            TaxAccountId = payment.WithholdingTaxAccountId,
            TaxAccountNumber = payment.WithholdingTaxAccount?.AccountNumber,
            TaxAccountName = payment.WithholdingTaxAccount?.AccountName,
            JournalEntryId = payment.JournalEntryId,
            CreatedAt = now,
            CreatedBy = UserName
        };
    }

    private static void UpdatePaymentCertificateProjection(VendorPayment payment, WithholdingTaxCertificate certificate)
    {
        // Existing tax reports read the payment projection. Keep it synchronized with the active
        // controlled version while the immutable certificate table owns lifecycle/history.
        payment.WithholdingCertificateNumber = certificate.CertificateNumber;
        payment.WithholdingCertificateDate = certificate.IssueDate;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.UpdatedBy = certificate.IssuedByName;
    }

    private async Task<string> GenerateCertificateNumberAsync(DateTime paymentDate, CancellationToken cancellationToken)
    {
        var prefix = $"WHT-{paymentDate:yyyyMM}-";
        var existing = await _context.WithholdingTaxCertificates.AsNoTracking()
            .Where(certificate => certificate.TenantId == TenantId && !certificate.IsDeleted
                && certificate.CertificateNumber.StartsWith(prefix))
            .Select(certificate => certificate.CertificateNumber)
            .ToListAsync(cancellationToken);
        var next = existing.Select(number => TryReadSequence(number, prefix)).DefaultIfEmpty(0).Max() + 1;
        return $"{prefix}{next:0000}";
    }

    private async Task<string> GenerateRemittanceNumberAsync(DateTime periodTo, CancellationToken cancellationToken)
    {
        var prefix = $"WHT-REM-{periodTo:yyyyMM}-";
        var existing = await _context.WithholdingTaxRemittances.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && item.RemittanceNumber.StartsWith(prefix))
            .Select(item => item.RemittanceNumber)
            .ToListAsync(cancellationToken);
        var next = existing.Select(number => TryReadSequence(number, prefix)).DefaultIfEmpty(0).Max() + 1;
        return $"{prefix}{next:0000}";
    }

    private async Task EnsureCertificateNumberIsUniqueAsync(string number, CancellationToken cancellationToken)
    {
        var exists = await _context.WithholdingTaxCertificates.AsNoTracking().AnyAsync(certificate =>
            certificate.TenantId == TenantId && !certificate.IsDeleted && certificate.CertificateNumber == number,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"WHT certificate number {number} is already in use.");
        }
    }

    private async Task<decimal?> ResolveEffectiveRateAsync(Tax tax, DateTime transactionDate, CancellationToken cancellationToken)
    {
        if (tax.EffectiveFrom.Date <= transactionDate.Date)
        {
            return tax.Rate;
        }
        return await _context.Set<TaxRateHistory>().AsNoTracking()
            .Where(history => history.TenantId == TenantId && !history.IsDeleted && history.TaxId == tax.Id
                && history.EffectiveFrom <= transactionDate
                && (!history.EffectiveTo.HasValue || history.EffectiveTo.Value >= transactionDate))
            .OrderByDescending(history => history.EffectiveFrom)
            .Select(history => (decimal?)history.Rate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<T> ExecuteSerializableAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await action();
        }
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task RecordCertificateAuditAsync(
        string eventType,
        VendorPayment payment,
        object? beforeValues,
        object? afterValues,
        string comment,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }
        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = payment.TenantId,
            SourceModule = "Tax",
            SourceDocumentType = nameof(VendorPayment),
            SourceDocumentId = payment.Id,
            JournalEntryId = payment.JournalEntryId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Comment = comment,
            Resource = "Finance.WHTCertificate",
            ResourceId = payment.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordRemittanceAuditAsync(
        string eventType,
        WithholdingTaxRemittance remittance,
        object? beforeValues,
        object? afterValues,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }
        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = remittance.TenantId,
            SourceModule = "Tax",
            SourceDocumentType = nameof(WithholdingTaxRemittance),
            SourceDocumentId = remittance.Id,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Comment = $"WHT remittance {remittance.RemittanceNumber} lifecycle updated.",
            Resource = "Finance.WHTRemittance",
            ResourceId = remittance.Id.ToString()
        }, cancellationToken);
    }

    private static WhtCertificateDto MapToDto(
        VendorPayment payment,
        IReadOnlyCollection<WithholdingTaxCertificate> versions,
        WithholdingTaxRemittance? remittance)
    {
        var current = versions.FirstOrDefault(certificate => certificate.Status == WhtCertificateStatus.Issued)
            ?? versions.OrderByDescending(certificate => certificate.VersionNumber).FirstOrDefault();
        var status = current?.Status.ToString() ?? "Missing";
        return new WhtCertificateDto
        {
            CertificateId = current?.Id,
            VendorPaymentId = payment.Id,
            PaymentNumber = payment.PaymentNumber,
            PaymentStatus = payment.Status,
            SupplierId = payment.SupplierId,
            SupplierName = current?.SupplierName ?? payment.Supplier?.Name ?? string.Empty,
            SupplierTin = current?.SupplierTin ?? payment.Supplier?.TaxId,
            PaymentDate = current?.PaymentDate ?? payment.PaymentDate,
            CurrencyCode = current?.CurrencyCode ?? NormalizeCurrency(payment.CurrencyCode),
            TaxId = current?.TaxId ?? payment.WithholdingTaxId,
            TaxCode = current?.TaxCode ?? payment.WithholdingTax?.Code,
            TaxName = current?.TaxName ?? payment.WithholdingTax?.Name,
            TaxRate = current?.TaxRate ?? RoundRate(payment.WithholdingTaxRate != 0m ? payment.WithholdingTaxRate : payment.WithholdingTax?.Rate ?? 0m),
            TaxableBase = current?.TaxableBase ?? ResolveTaxableBase(payment),
            WithholdingAmount = current?.WithholdingAmount ?? RoundMoney(payment.WithholdingTaxAmount),
            NetPaidAmount = current?.NetPaidAmount ?? RoundMoney(payment.TotalAmount),
            TaxAccountNumber = current?.TaxAccountNumber ?? payment.WithholdingTaxAccount?.AccountNumber,
            TaxAccountName = current?.TaxAccountName ?? payment.WithholdingTaxAccount?.AccountName,
            CertificateNumber = current?.CertificateNumber,
            CertificateDate = current?.IssueDate,
            CertificateStatus = status,
            VersionNumber = current?.VersionNumber ?? 0,
            IssuedAtUtc = current?.IssuedAtUtc,
            IssuedByName = current?.IssuedByName,
            LifecycleReason = current?.LifecycleReason,
            CancelledAtUtc = current?.CancelledAtUtc,
            CancellationReason = current?.CancellationReason,
            JournalEntryId = current?.JournalEntryId ?? payment.JournalEntryId,
            RemittanceId = remittance?.Id,
            RemittanceNumber = remittance?.RemittanceNumber,
            RemittanceStatus = remittance?.Status.ToString() ?? "Unremitted",
            Versions = versions.OrderByDescending(certificate => certificate.VersionNumber).Select(certificate => new WhtCertificateVersionDto
            {
                CertificateId = certificate.Id,
                CertificateNumber = certificate.CertificateNumber,
                VersionNumber = certificate.VersionNumber,
                Status = certificate.Status.ToString(),
                IssueDate = certificate.IssueDate,
                IssuedAtUtc = certificate.IssuedAtUtc,
                IssuedByName = certificate.IssuedByName,
                LifecycleReason = certificate.LifecycleReason,
                CancelledAtUtc = certificate.CancelledAtUtc,
                CancellationReason = certificate.CancellationReason
            }).ToList()
        };
    }

    private static WhtRemittanceDto MapRemittance(WithholdingTaxRemittance remittance)
        => new()
        {
            Id = remittance.Id,
            RemittanceNumber = remittance.RemittanceNumber,
            PeriodFrom = remittance.PeriodFrom,
            PeriodTo = remittance.PeriodTo,
            DueDate = remittance.DueDate,
            CurrencyCode = remittance.CurrencyCode,
            Status = remittance.Status.ToString(),
            TotalWithholdingAmount = RoundMoney(remittance.TotalWithholdingAmount),
            LineCount = remittance.Lines.Count(line => !line.IsDeleted),
            SubmissionReference = remittance.SubmissionReference,
            SubmittedAtUtc = remittance.SubmittedAtUtc,
            SubmittedByName = remittance.SubmittedByName,
            PaymentReference = remittance.PaymentReference,
            PaymentDate = remittance.PaymentDate,
            AuthorityReceiptReference = remittance.AuthorityReceiptReference,
            PaidAtUtc = remittance.PaidAtUtc,
            PaidByName = remittance.PaidByName,
            Notes = remittance.Notes,
            CancelledAtUtc = remittance.CancelledAtUtc,
            CancellationReason = remittance.CancellationReason,
            Lines = remittance.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.PaymentDate).ThenBy(line => line.PaymentNumber)
                .Select(line => new WhtRemittanceLineDto
                {
                    Id = line.Id,
                    VendorPaymentId = line.VendorPaymentId,
                    CertificateId = line.CertificateId,
                    PaymentNumber = line.PaymentNumber,
                    PaymentDate = line.PaymentDate,
                    SupplierId = line.SupplierId,
                    SupplierName = line.SupplierName,
                    SupplierTin = line.SupplierTin,
                    TaxCode = line.TaxCode,
                    TaxableBase = RoundMoney(line.TaxableBase),
                    WithholdingAmount = RoundMoney(line.WithholdingAmount),
                    JournalEntryId = line.JournalEntryId
                }).ToList()
        };

    private static object CertificateAuditSnapshot(WithholdingTaxCertificate certificate) => new
    {
        certificate.Id,
        certificate.CertificateNumber,
        certificate.VersionNumber,
        Status = certificate.Status.ToString(),
        certificate.IssueDate,
        certificate.SupersedesCertificateId,
        certificate.SupersededByCertificateId,
        certificate.LifecycleReason,
        certificate.CancelledAtUtc,
        certificate.CancellationReason,
        certificate.WithholdingAmount
    };

    private static object RemittanceAuditSnapshot(WithholdingTaxRemittance remittance) => new
    {
        remittance.Id,
        remittance.RemittanceNumber,
        remittance.PeriodFrom,
        remittance.PeriodTo,
        remittance.DueDate,
        Status = remittance.Status.ToString(),
        remittance.TotalWithholdingAmount,
        LineCount = remittance.Lines.Count(line => !line.IsDeleted),
        remittance.SubmissionReference,
        remittance.PaymentReference,
        remittance.PaymentDate,
        remittance.AuthorityReceiptReference,
        remittance.CancellationReason
    };

    private static string BuildCertificateHtml(WithholdingTaxCertificate certificate)
    {
        var watermark = certificate.Status == WhtCertificateStatus.Issued
            ? string.Empty
            : $"<div class=\"watermark\">{Html(certificate.Status.ToString().ToUpperInvariant())}</div>";
        var historyNote = certificate.Status == WhtCertificateStatus.Issued
            ? string.Empty
            : $"<div class=\"notice\">Lifecycle status: {Html(certificate.Status.ToString())}. {Html(certificate.CancellationReason ?? certificate.LifecycleReason ?? string.Empty)}</div>";
        return $$$"""
<!doctype html>
<html><head><meta charset="utf-8" /><title>{{{Html($"WHT Certificate {certificate.CertificateNumber}")}}}</title>
<style>
body{font-family:Arial,sans-serif;color:#111827;margin:40px}.certificate{position:relative;max-width:840px;margin:0 auto;border:1px solid #d1d5db;padding:32px;overflow:hidden}.header{display:flex;justify-content:space-between;gap:24px;border-bottom:2px solid #111827;padding-bottom:18px;margin-bottom:24px}.title{font-size:24px;font-weight:700;margin:0 0 6px}.muted{color:#6b7280}.number{font-size:16px;font-weight:700;text-align:right}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px 32px;margin:24px 0}.label{font-size:12px;color:#6b7280;text-transform:uppercase;letter-spacing:.04em;margin-bottom:4px}.value{font-size:16px;font-weight:600}table{width:100%;border-collapse:collapse;margin-top:24px}th,td{border:1px solid #d1d5db;padding:10px;text-align:left}th{background:#f3f4f6}.right{text-align:right}.footer{margin-top:54px;display:grid;grid-template-columns:1fr 1fr;gap:48px}.line{border-top:1px solid #111827;padding-top:8px}.actions{max-width:840px;margin:16px auto;text-align:right}button{padding:8px 14px;border:1px solid #111827;background:#111827;color:white;cursor:pointer;border-radius:4px}.watermark{position:absolute;top:42%;left:8%;transform:rotate(-28deg);font-size:88px;font-weight:800;color:rgba(185,28,28,.14);z-index:0}.notice{border:1px solid #fecaca;background:#fef2f2;color:#991b1b;padding:10px;margin-bottom:20px}.certificate>section,.certificate>table{position:relative;z-index:1}@media print{body{margin:0}.actions{display:none}.certificate{border:none}}
</style></head><body><div class="actions"><button onclick="window.print()">Print</button></div><main class="certificate">{{{watermark}}}{{{historyNote}}}
<section class="header"><div><h1 class="title">Withholding Tax Certificate</h1><div class="muted">Supplier WHT deducted on a posted AP payment</div></div><div class="number"><div>{{{Html(certificate.CertificateNumber)}}}</div><div class="muted">Version {{{certificate.VersionNumber}}} | {{{certificate.IssueDate:yyyy-MM-dd}}}</div></div></section>
<section class="grid"><div><div class="label">Supplier</div><div class="value">{{{Html(certificate.SupplierName)}}}</div></div><div><div class="label">Supplier TIN</div><div class="value">{{{Html(certificate.SupplierTin ?? "Not supplied")}}}</div></div><div><div class="label">Payment Reference</div><div class="value">{{{Html(certificate.PaymentNumber)}}}</div></div><div><div class="label">Payment Date</div><div class="value">{{{certificate.PaymentDate:yyyy-MM-dd}}}</div></div></section>
<table><thead><tr><th>Tax Type</th><th class="right">Rate</th><th class="right">Taxable Base</th><th class="right">Withheld</th><th class="right">Net Paid</th></tr></thead><tbody><tr><td>{{{Html(certificate.TaxName ?? certificate.TaxCode ?? "WHT")}}}</td><td class="right">{{{certificate.TaxRate:N2}}}%</td><td class="right">{{{Html(certificate.CurrencyCode)}}} {{{certificate.TaxableBase:N2}}}</td><td class="right">{{{Html(certificate.CurrencyCode)}}} {{{certificate.WithholdingAmount:N2}}}</td><td class="right">{{{Html(certificate.CurrencyCode)}}} {{{certificate.NetPaidAmount:N2}}}</td></tr></tbody></table>
<section class="grid"><div><div class="label">WHT Account</div><div class="value">{{{Html($"{certificate.TaxAccountNumber} {certificate.TaxAccountName}".Trim())}}}</div></div><div><div class="label">Journal Reference</div><div class="value">{{{Html(certificate.JournalEntryId?.ToString() ?? "Not linked")}}}</div></div><div><div class="label">Issued By</div><div class="value">{{{Html(certificate.IssuedByName)}}}</div></div><div><div class="label">Issued At</div><div class="value">{{{certificate.IssuedAtUtc:yyyy-MM-dd HH:mm}}} UTC</div></div></section>
<section class="footer"><div class="line">Prepared by</div><div class="line">Authorized signatory</div></section></main></body></html>
""";
    }

    private static decimal ResolveTaxableBase(VendorPayment payment)
        => RoundMoney(payment.WithholdingTaxBaseAmount > 0m
            ? payment.WithholdingTaxBaseAmount
            : payment.TotalAmount + payment.WithholdingTaxAmount);

    private static DateTime DefaultRemittanceDueDate(DateTime periodTo)
        => new DateTime(periodTo.Year, periodTo.Month, 1).AddMonths(1).AddDays(14);

    private static string RequireReason(string? value, string label, int minimumLength)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < minimumLength)
        {
            throw new InvalidOperationException($"{label} must contain at least {minimumLength} characters.");
        }
        return normalized;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeCertificateNumber(string? value)
        => Normalize(value)?.ToUpperInvariant();

    private static string? NormalizeStatus(string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (string.Equals(normalized, "Generated", StringComparison.OrdinalIgnoreCase))
        {
            return "Issued";
        }
        return normalized;
    }

    private static int TryReadSequence(string number, string prefix)
        => number.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(number[prefix.Length..], out var sequence)
                ? sequence
                : 0;

    private static string NormalizeCurrency(string? currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode) ? "GHS" : currencyCode.Trim().ToUpperInvariant();

    private static string Csv(string? value)
        => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static string Html(string value) => WebUtility.HtmlEncode(value);
    private static decimal RoundMoney(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundRate(decimal amount) => decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
}
