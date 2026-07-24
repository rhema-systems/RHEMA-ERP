using System.Data;
using System.Net;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Taxation;

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

    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? "system"
        : _currentUser.UserName!;

    public async Task<PagedResult<WhtCertificateDto>> GetApCertificatesAsync(
        WhtCertificateQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new WhtCertificateQueryDto();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var payments = BuildEligibleApPaymentQuery(query);
        var totalCount = await payments.CountAsync(cancellationToken);

        var items = await payments
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<WhtCertificateDto>
        {
            Items = items.Select(MapToDto).ToList(),
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

        // Certificate details and printable evidence must follow the same posted-accounting
        // boundary as the list and generation paths.
        await EnsurePaymentJournalIsPostedAsync(payment, cancellationToken);
        return MapToDto(payment);
    }

    public async Task<WhtCertificateDto> GenerateApCertificateAsync(
        Guid vendorPaymentId,
        GenerateWhtCertificateDto dto,
        CancellationToken cancellationToken = default)
    {
        dto ??= new GenerateWhtCertificateDto();

        if (_context.Database.CurrentTransaction != null)
        {
            return await GenerateApCertificateCoreAsync(vendorPaymentId, dto, cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Certificate numbering and assignment are one serializable operation. This prevents
            // concurrent requests from issuing the same tenant/month sequence or manual number.
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var certificate = await GenerateApCertificateCoreAsync(vendorPaymentId, dto, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return certificate;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private async Task<WhtCertificateDto> GenerateApCertificateCoreAsync(
        Guid vendorPaymentId,
        GenerateWhtCertificateDto dto,
        CancellationToken cancellationToken)
    {
        var payment = await LoadEligibleApPaymentAsync(vendorPaymentId, asTracking: true, cancellationToken)
            ?? throw new InvalidOperationException("AP WHT payment was not found or is not eligible for certificate generation.");

        await EnsurePaymentJournalIsPostedAsync(payment, cancellationToken);

        var before = new
        {
            payment.WithholdingCertificateNumber,
            payment.WithholdingCertificateDate
        };

        var requestedNumber = NormalizeCertificateNumber(dto.CertificateNumber);
        if (!string.IsNullOrWhiteSpace(payment.WithholdingCertificateNumber))
        {
            if (!string.IsNullOrWhiteSpace(requestedNumber)
                && !string.Equals(payment.WithholdingCertificateNumber, requestedNumber, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"AP payment {payment.PaymentNumber} already has certificate {payment.WithholdingCertificateNumber}.");
            }

            return MapToDto(payment);
        }

        var certificateNumber = string.IsNullOrWhiteSpace(requestedNumber)
            ? await GenerateCertificateNumberAsync(payment.PaymentDate, cancellationToken)
            : requestedNumber;

        await EnsureCertificateNumberIsUniqueAsync(certificateNumber, payment.Id, cancellationToken);

        payment.WithholdingCertificateNumber = certificateNumber;
        payment.WithholdingCertificateDate = (dto.CertificateDate ?? DateTime.UtcNow).Date;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.UpdatedBy = UserName;

        await _context.SaveChangesAsync(cancellationToken);

        await RecordCertificateAuditAsync(
            FinanceAuditEvents.WithholdingCertificateReferenceCreated,
            payment,
            before,
            new
            {
                payment.WithholdingCertificateNumber,
                payment.WithholdingCertificateDate,
                payment.WithholdingTaxAmount,
                payment.WithholdingTaxRate
            },
            cancellationToken);

        return MapToDto(payment);
    }

    public async Task<string> GetApCertificateHtmlAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default)
    {
        var certificate = await GetApCertificateAsync(vendorPaymentId, cancellationToken)
            ?? throw new InvalidOperationException("AP WHT certificate was not found.");

        if (string.IsNullOrWhiteSpace(certificate.CertificateNumber))
        {
            throw new InvalidOperationException("Generate a WHT certificate before printing.");
        }

        return BuildCertificateHtml(certificate);
    }

    private IQueryable<VendorPayment> BuildEligibleApPaymentQuery(WhtCertificateQueryDto query)
    {
        var tenantId = TenantId;
        var payments = _context.Set<VendorPayment>()
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.WithholdingTax)
            .Include(p => p.WithholdingTaxAccount)
            .Where(p =>
                p.TenantId == tenantId
                && !p.IsDeleted
                && p.WithholdingTaxAmount > 0m
                && p.JournalEntryId.HasValue
                && _context.Set<JournalEntry>().Any(j =>
                    j.TenantId == tenantId
                    && !j.IsDeleted
                    && j.Id == p.JournalEntryId
                    && j.PostingStatus == PostedStatus));

        if (query.SupplierId.HasValue)
        {
            payments = payments.Where(p => p.SupplierId == query.SupplierId.Value);
        }

        if (query.FromDate.HasValue)
        {
            var fromDate = query.FromDate.Value.Date;
            payments = payments.Where(p => p.PaymentDate.Date >= fromDate);
        }

        if (query.ToDate.HasValue)
        {
            var toDate = query.ToDate.Value.Date;
            payments = payments.Where(p => p.PaymentDate.Date <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            payments = payments.Where(p =>
                p.PaymentNumber.Contains(term)
                || (p.WithholdingCertificateNumber != null && p.WithholdingCertificateNumber.Contains(term))
                || (p.Supplier != null && p.Supplier.Name.Contains(term))
                || (p.Supplier != null && p.Supplier.TaxId != null && p.Supplier.TaxId.Contains(term)));
        }

        var status = NormalizeStatus(query.Status);
        if (!string.IsNullOrWhiteSpace(status))
        {
            payments = status switch
            {
                "Missing" => payments.Where(p => p.WithholdingCertificateNumber == null || p.WithholdingCertificateNumber == string.Empty),
                "Generated" => payments.Where(p => p.WithholdingCertificateNumber != null && p.WithholdingCertificateNumber != string.Empty && p.WithholdingCertificateDate.HasValue),
                "NumberOnly" => payments.Where(p => p.WithholdingCertificateNumber != null && p.WithholdingCertificateNumber != string.Empty && !p.WithholdingCertificateDate.HasValue),
                _ => payments
            };
        }

        return payments;
    }

    private async Task<VendorPayment?> LoadEligibleApPaymentAsync(
        Guid vendorPaymentId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = _context.Set<VendorPayment>()
            .Include(p => p.Supplier)
            .Include(p => p.WithholdingTax)
            .Include(p => p.WithholdingTaxAccount)
            .Where(p =>
                p.TenantId == TenantId
                && !p.IsDeleted
                && p.Id == vendorPaymentId
                && p.WithholdingTaxAmount > 0m);

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
            throw new InvalidOperationException($"AP payment {payment.PaymentNumber} must be posted before a WHT certificate can be generated.");
        }

        var posted = await _context.Set<JournalEntry>()
            .AsNoTracking()
            .AnyAsync(j =>
                j.TenantId == payment.TenantId
                && !j.IsDeleted
                && j.Id == payment.JournalEntryId.Value
                && j.PostingStatus == PostedStatus,
                cancellationToken);

        if (!posted)
        {
            throw new InvalidOperationException($"AP payment {payment.PaymentNumber} is not linked to a posted journal.");
        }
    }

    private async Task<string> GenerateCertificateNumberAsync(DateTime paymentDate, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var prefix = $"WHT-{paymentDate:yyyyMM}-";
        var existingNumbers = await _context.Set<VendorPayment>()
            .AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId
                && !p.IsDeleted
                && p.WithholdingCertificateNumber != null
                && p.WithholdingCertificateNumber.StartsWith(prefix))
            .Select(p => p.WithholdingCertificateNumber!)
            .ToListAsync(cancellationToken);

        var next = existingNumbers
            .Select(number => TryReadSequence(number, prefix))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{next:0000}";
    }

    private async Task EnsureCertificateNumberIsUniqueAsync(
        string certificateNumber,
        Guid vendorPaymentId,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var exists = await _context.Set<VendorPayment>()
            .AsNoTracking()
            .AnyAsync(p =>
                p.TenantId == tenantId
                && !p.IsDeleted
                && p.Id != vendorPaymentId
                && p.WithholdingCertificateNumber == certificateNumber,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"WHT certificate number {certificateNumber} is already in use.");
        }
    }

    private async Task RecordCertificateAuditAsync(
        string eventType,
        VendorPayment payment,
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
            TenantId = payment.TenantId,
            SourceModule = "Tax",
            SourceDocumentType = nameof(VendorPayment),
            SourceDocumentId = payment.Id,
            JournalEntryId = payment.JournalEntryId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Comment = "AP WHT certificate reference generated from posted supplier payment.",
            Resource = "Finance.WHTCertificate",
            ResourceId = payment.Id.ToString()
        }, cancellationToken);
    }

    private static WhtCertificateDto MapToDto(VendorPayment payment)
    {
        return new WhtCertificateDto
        {
            VendorPaymentId = payment.Id,
            PaymentNumber = payment.PaymentNumber,
            PaymentStatus = payment.Status,
            SupplierId = payment.SupplierId,
            SupplierName = payment.Supplier?.Name ?? string.Empty,
            SupplierTin = payment.Supplier?.TaxId,
            PaymentDate = payment.PaymentDate,
            CurrencyCode = NormalizeCurrency(payment.CurrencyCode),
            TaxId = payment.WithholdingTaxId,
            TaxCode = payment.WithholdingTax?.Code,
            TaxName = payment.WithholdingTax?.Name,
            TaxRate = RoundRate(payment.WithholdingTaxRate != 0m ? payment.WithholdingTaxRate : payment.WithholdingTax?.Rate ?? 0m),
            TaxableBase = RoundMoney(payment.TotalAmount + payment.WithholdingTaxAmount),
            WithholdingAmount = RoundMoney(payment.WithholdingTaxAmount),
            NetPaidAmount = RoundMoney(payment.TotalAmount),
            TaxAccountNumber = payment.WithholdingTaxAccount?.AccountNumber,
            TaxAccountName = payment.WithholdingTaxAccount?.AccountName,
            CertificateNumber = payment.WithholdingCertificateNumber,
            CertificateDate = payment.WithholdingCertificateDate,
            CertificateStatus = CertificateStatus(payment.WithholdingCertificateNumber, payment.WithholdingCertificateDate),
            JournalEntryId = payment.JournalEntryId
        };
    }

    private static string BuildCertificateHtml(WhtCertificateDto certificate)
    {
        var certificateDate = certificate.CertificateDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        var paymentDate = certificate.PaymentDate.ToString("yyyy-MM-dd");
        var title = $"WHT Certificate {certificate.CertificateNumber}";

        return $$"""
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>{{Html(title)}}</title>
  <style>
    body { font-family: Arial, sans-serif; color: #111827; margin: 40px; }
    .certificate { max-width: 840px; margin: 0 auto; border: 1px solid #d1d5db; padding: 32px; }
    .header { display: flex; justify-content: space-between; gap: 24px; border-bottom: 2px solid #111827; padding-bottom: 18px; margin-bottom: 24px; }
    .title { font-size: 24px; font-weight: 700; margin: 0 0 6px; }
    .muted { color: #6b7280; }
    .number { font-size: 16px; font-weight: 700; text-align: right; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 18px 32px; margin: 24px 0; }
    .label { font-size: 12px; color: #6b7280; text-transform: uppercase; letter-spacing: .04em; margin-bottom: 4px; }
    .value { font-size: 16px; font-weight: 600; }
    table { width: 100%; border-collapse: collapse; margin-top: 24px; }
    th, td { border: 1px solid #d1d5db; padding: 10px; text-align: left; }
    th { background: #f3f4f6; }
    .right { text-align: right; }
    .footer { margin-top: 54px; display: grid; grid-template-columns: 1fr 1fr; gap: 48px; }
    .line { border-top: 1px solid #111827; padding-top: 8px; }
    .actions { max-width: 840px; margin: 16px auto; text-align: right; }
    button { padding: 8px 14px; border: 1px solid #111827; background: #111827; color: white; cursor: pointer; border-radius: 4px; }
    @media print { body { margin: 0; } .actions { display: none; } .certificate { border: none; } }
  </style>
</head>
<body>
  <div class="actions"><button onclick="window.print()">Print</button></div>
  <main class="certificate">
    <section class="header">
      <div>
        <h1 class="title">Withholding Tax Certificate</h1>
        <div class="muted">Supplier withholding tax deducted on posted AP payment</div>
      </div>
      <div class="number">
        <div>{{Html(certificate.CertificateNumber!)}}</div>
        <div class="muted">{{Html(certificateDate)}}</div>
      </div>
    </section>

    <section class="grid">
      <div>
        <div class="label">Supplier</div>
        <div class="value">{{Html(certificate.SupplierName)}}</div>
      </div>
      <div>
        <div class="label">Supplier TIN</div>
        <div class="value">{{Html(certificate.SupplierTin ?? "Not supplied")}}</div>
      </div>
      <div>
        <div class="label">Payment Reference</div>
        <div class="value">{{Html(certificate.PaymentNumber)}}</div>
      </div>
      <div>
        <div class="label">Payment Date</div>
        <div class="value">{{Html(paymentDate)}}</div>
      </div>
    </section>

    <table>
      <thead>
        <tr>
          <th>Tax Type</th>
          <th class="right">Rate</th>
          <th class="right">Taxable Base</th>
          <th class="right">Withheld Amount</th>
          <th class="right">Net Paid</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td>{{Html(certificate.TaxName ?? certificate.TaxCode ?? "WHT")}}</td>
          <td class="right">{{certificate.TaxRate:N2}}%</td>
          <td class="right">{{Html(certificate.CurrencyCode)}} {{certificate.TaxableBase:N2}}</td>
          <td class="right">{{Html(certificate.CurrencyCode)}} {{certificate.WithholdingAmount:N2}}</td>
          <td class="right">{{Html(certificate.CurrencyCode)}} {{certificate.NetPaidAmount:N2}}</td>
        </tr>
      </tbody>
    </table>

    <section class="grid">
      <div>
        <div class="label">WHT Account</div>
        <div class="value">{{Html(AccountLabel(certificate))}}</div>
      </div>
      <div>
        <div class="label">Journal Reference</div>
        <div class="value">{{Html(certificate.JournalEntryId?.ToString() ?? "Not linked")}}</div>
      </div>
    </section>

    <section class="footer">
      <div class="line">Prepared by</div>
      <div class="line">Authorized signatory</div>
    </section>
  </main>
</body>
</html>
""";
    }

    private static string CertificateStatus(string? certificateNumber, DateTime? certificateDate)
    {
        if (string.IsNullOrWhiteSpace(certificateNumber))
        {
            return "Missing";
        }

        return certificateDate.HasValue ? "Generated" : "NumberOnly";
    }

    private static string? NormalizeCertificateNumber(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToUpperInvariant();
    }

    private static string? NormalizeStatus(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(trimmed, "Issued", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Received", StringComparison.OrdinalIgnoreCase))
        {
            return "Generated";
        }

        return trimmed;
    }

    private static int TryReadSequence(string number, string prefix)
    {
        if (!number.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(number[prefix.Length..], out var sequence) ? sequence : 0;
    }

    private static string NormalizeCurrency(string? currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode) ? "GHS" : currencyCode.Trim().ToUpperInvariant();

    private static string AccountLabel(WhtCertificateDto certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate.TaxAccountNumber)
            && string.IsNullOrWhiteSpace(certificate.TaxAccountName))
        {
            return "Not configured";
        }

        return $"{certificate.TaxAccountNumber} {certificate.TaxAccountName}".Trim();
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static decimal RoundMoney(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal amount)
        => decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
}
