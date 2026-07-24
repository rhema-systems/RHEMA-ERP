using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ErpSystem.Core.Services.Procurement;

public class RfqNotificationService : IRfqNotificationService
{
    private readonly IRequestForQuotationRepository _rfqRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IBusinessPartnerUserRepository _businessPartnerUserRepository;
    private readonly IRequestForQuotationQuoteRepository _quoteRepository;
    private readonly IRfqInvitationDocumentService _rfqInvitationDocumentService;
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RfqNotificationService> _logger;

    public RfqNotificationService(
        IRequestForQuotationRepository rfqRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        IRequestForQuotationQuoteRepository quoteRepository,
        IRfqInvitationDocumentService rfqInvitationDocumentService,
        INotificationService notificationService,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<RfqNotificationService> logger)
    {
        _rfqRepository = rfqRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _quoteRepository = quoteRepository;
        _rfqInvitationDocumentService = rfqInvitationDocumentService;
        _notificationService = notificationService;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendRfqSentNotificationAsync(Guid rfqId, List<Guid> businessPartnerIds, List<string>? externalRecipientEmails = null)
    {
        var rfq = await _rfqRepository.GetByIdAsync(rfqId)
            ?? throw new InvalidOperationException($"RFQ with ID {rfqId} not found");

        List<EmailAttachmentInfo>? attachments = null;
        try
        {
            var (content, fileName) = await _rfqInvitationDocumentService.GenerateRfqInvitationPdfAsync(rfqId);
            attachments = new List<EmailAttachmentInfo>
            {
                new EmailAttachmentInfo
                {
                    FileName = fileName,
                    Content = content,
                    ContentType = "application/pdf"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate RFQ invitation PDF for {RfqId}. Continuing without attachments.", rfqId);
            attachments = null;
        }

        var portalUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
        var actionUrl = $"{portalUrl.TrimEnd('/')}/external-portal/rfqs/{rfqId}";

        foreach (var businessPartnerId in businessPartnerIds.Distinct())
        {
            var bp = await _businessPartnerRepository.GetByIdAsync(businessPartnerId);
            if (bp == null)
            {
                _logger.LogWarning("Business partner {BusinessPartnerId} not found for RFQ {RfqId}", businessPartnerId, rfqId);
                continue;
            }

            // In-app notifications to linked portal users.
            var portalUserIds = new HashSet<Guid>();
            if (bp.UserId.HasValue)
                portalUserIds.Add(bp.UserId.Value);

            try
            {
                var activeSubUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                foreach (var u in activeSubUsers)
                    portalUserIds.Add(u.UserId);
            }
            catch
            {
                // Best-effort: repository may not support or may throw based on data.
            }

            foreach (var userId in portalUserIds)
            {
                await _notificationService.CreateInAppNotificationAsync(
                    userId,
                    title: $"New RFQ: {rfq.RfqNumber}",
                    message: $"You have been invited to submit a quotation for \"{rfq.Title}\".",
                    type: "RFQ",
                    data: new Dictionary<string, object>
                    {
                        ["EntityType"] = "Rfq",
                        ["EntityId"] = rfqId,
                        ["RfqId"] = rfqId,
                        ["RfqNumber"] = rfq.RfqNumber,
                        ["ActionUrl"] = actionUrl
                    },
                    tenantId: bp.TenantId
                );
            }

            // Email notification (sent individually).
            if (!string.IsNullOrWhiteSpace(bp.PrimaryEmail))
            {
                var subject = $"RFQ Invitation: {rfq.RfqNumber}";
                var body = GenerateRfqInvitationEmailBody(rfq.RfqNumber, rfq.Title, actionUrl, bp.PartnerName);

                if (attachments != null && attachments.Count > 0)
                {
                    await _notificationService.SendEmailWithAttachmentsAsync(bp.PrimaryEmail, subject, body, attachments, isHtml: true);
                }
                else
                {
                    await _notificationService.SendEmailAsync(bp.PrimaryEmail, subject, body, isHtml: true);
                }
            }
        }

        if (externalRecipientEmails != null && externalRecipientEmails.Any())
        {
            var distinctExternalEmails = externalRecipientEmails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim())
                .Where(e => e.Contains('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var email in distinctExternalEmails)
            {
                var subject = $"RFQ Invitation: {rfq.RfqNumber}";
                var body = GenerateRfqInvitationEmailBody(rfq.RfqNumber, rfq.Title, actionUrl, recipientName: "Supplier");

                if (attachments != null && attachments.Count > 0)
                {
                    await _notificationService.SendEmailWithAttachmentsAsync(email, subject, body, attachments, isHtml: true);
                }
                else
                {
                    await _notificationService.SendEmailAsync(email, subject, body, isHtml: true);
                }
            }
        }
    }

    public async Task SendRfqQuoteSubmittedNotificationAsync(Guid rfqId, Guid quoteId)
    {
        var rfq = await _rfqRepository.GetWithDetailsAsync(rfqId)
            ?? throw new InvalidOperationException($"RFQ with ID {rfqId} not found");

        // Use quotes from the RFQ details include (has BusinessPartner + items).
        var quote = (rfq.Quotes ?? new List<RequestForQuotationQuote>())
            .FirstOrDefault(q => q.Id == quoteId && !q.IsDeleted);

        if (quote == null)
        {
            // Fallback: load directly (in case RFQ include changes in future).
            quote = await _quoteRepository.GetByIdAsync(quoteId);
        }

        if (quote == null)
        {
            throw new InvalidOperationException($"RFQ quote with ID {quoteId} not found");
        }

        var portalUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
        var actionUrl = $"{portalUrl.TrimEnd('/')}/procurement/rfqs/{rfqId}/controls";

        // Default internal recipients: RFQ creator + tenant managers/admins.
        var recipientIds = new HashSet<Guid>();
        if (rfq.CreatedById.HasValue)
        {
            recipientIds.Add(rfq.CreatedById.Value);
        }

        // Align with internal RFQ authorization (SuperAdmin/TenantAdmin/Manager).
        // Note: roles are system-wide; filter to the RFQ tenant.
        var roles = new[] { "SuperAdmin", "TenantAdmin", "Manager" };
        foreach (var role in roles)
        {
            try
            {
                var users = await _userManager.GetUsersInRoleAsync(role);
                foreach (var user in users.Where(u => u.IsActive && u.TenantId == rfq.TenantId))
                {
                    recipientIds.Add(user.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve users in role {Role} for RFQ quote notification", role);
            }
        }

        if (recipientIds.Count == 0)
        {
            return;
        }

        var subject = $"RFQ Quote Submitted: {rfq.RfqNumber}";
        var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; color: #111827; line-height: 1.6;'>
  <div style='max-width: 700px; margin: 0 auto; padding: 16px;'>
    <h2 style='margin: 0 0 8px 0;'>Sealed Quotation Received</h2>
    <div style='padding: 12px; background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px;'>
      <div><strong>RFQ:</strong> {rfq.RfqNumber}</div>
      <div><strong>Receipt status:</strong> Sealed</div>
      <div>Supplier identity, prices, and commercial lines remain hidden until the controlled opening register is completed after the deadline.</div>
      <div style='margin-top: 10px;'>
        <a href='{actionUrl}' target='_blank' rel='noreferrer'
           style='display:inline-block; background:#0f766e; color:#ffffff; text-decoration:none; padding:10px 14px; border-radius:6px;'>
          Open RFQ
        </a>
      </div>
    </div>
    <div style='margin-top: 10px; font-size: 12px; color: #6b7280;'>This is an automated notification from the ERP procurement system.</div>
  </div>
</body>
</html>";

        foreach (var userId in recipientIds)
        {
            try
            {
                await _notificationService.CreateInAppNotificationAsync(
                    userId,
                    title: $"Sealed RFQ quotation received: {rfq.RfqNumber}",
                    message: "A sealed quotation was received. Commercial details remain inaccessible until controlled opening.",
                    type: "RFQ",
                    data: new Dictionary<string, object>
                    {
                        ["EntityType"] = "Rfq",
                        ["EntityId"] = rfqId,
                        ["RfqId"] = rfqId,
                        ["RfqNumber"] = rfq.RfqNumber,
                        ["ActionUrl"] = actionUrl
                    },
                    tenantId: rfq.TenantId
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create in-app RFQ quote notification for user {UserId}", userId);
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user != null && user.IsActive && !string.IsNullOrWhiteSpace(user.Email))
                {
                    await _notificationService.SendEmailAsync(user.Email, subject, body, isHtml: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send RFQ quote email notification to user {UserId}", userId);
            }
        }
    }

    public async Task SendRfqAwardedNotificationAsync(Guid rfqId)
    {
        var rfq = await _rfqRepository.GetWithDetailsAsync(rfqId)
            ?? throw new InvalidOperationException($"RFQ with ID {rfqId} not found");

        var rfqItems = (rfq.Items ?? new List<RequestForQuotationItem>())
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.LineNumber)
            .ToList();

        var totalLineCount = rfqItems.Count;
        if (totalLineCount == 0)
        {
            return;
        }

        var itemsById = rfqItems.ToDictionary(i => i.Id, i => i);

        var awardLines = (rfq.AwardLines ?? new List<RequestForQuotationAwardLine>())
            .Where(a => !a.IsDeleted)
            .ToList();

        if (awardLines.Count == 0)
        {
            return;
        }

        var portalUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
        var actionUrl = $"{portalUrl.TrimEnd('/')}/external-portal/rfqs/{rfqId}";

        foreach (var group in awardLines.GroupBy(a => a.BusinessPartnerId))
        {
            var businessPartnerId = group.Key;
            var bp = group.FirstOrDefault()?.BusinessPartner ?? await _businessPartnerRepository.GetByIdAsync(businessPartnerId);
            if (bp == null)
            {
                _logger.LogWarning("Business partner {BusinessPartnerId} not found for RFQ award notification {RfqId}", businessPartnerId, rfqId);
                continue;
            }

            var awardedLines = group
                .Select(a => new
                {
                    Award = a,
                    Item = itemsById.TryGetValue(a.RfqItemId, out var item) ? item : null
                })
                .OrderBy(x => x.Item?.LineNumber ?? int.MaxValue)
                .ToList();

            var awardedCount = awardedLines.Count;
            if (awardedCount == 0)
            {
                continue;
            }

            var isFullyAwardedForSupplier = awardedCount == totalLineCount;
            var subject = isFullyAwardedForSupplier
                ? $"RFQ Awarded: {rfq.RfqNumber}"
                : $"RFQ Partially Awarded: {rfq.RfqNumber}";

            var lineNumbers = awardedLines
                .Select(x => x.Item?.LineNumber)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var lineNumbersPreview = lineNumbers.Count == 0
                ? null
                : string.Join(", ", lineNumbers.Take(5)) + (lineNumbers.Count > 5 ? "…" : "");

            var message = isFullyAwardedForSupplier
                ? $"Your quotation was awarded for all RFQ line(s) ({awardedCount}/{totalLineCount})."
                : $"You were awarded {awardedCount}/{totalLineCount} RFQ line(s).";

            if (!string.IsNullOrWhiteSpace(lineNumbersPreview))
            {
                message += $" Awarded line(s): {lineNumbersPreview}.";
            }

            // In-app notifications to linked portal users.
            var portalUserIds = new HashSet<Guid>();
            if (bp.UserId.HasValue)
                portalUserIds.Add(bp.UserId.Value);

            try
            {
                var activeSubUsers = await _businessPartnerUserRepository.GetActiveByBusinessPartnerIdAsync(bp.Id);
                foreach (var u in activeSubUsers)
                    portalUserIds.Add(u.UserId);
            }
            catch
            {
                // Best-effort: repository may not support or may throw based on data.
            }

            foreach (var userId in portalUserIds)
            {
                try
                {
                    await _notificationService.CreateInAppNotificationAsync(
                        userId,
                        title: subject,
                        message: message,
                        type: "RFQ",
                        data: new Dictionary<string, object>
                        {
                            ["EntityType"] = "Rfq",
                            ["EntityId"] = rfqId,
                            ["RfqId"] = rfqId,
                            ["RfqNumber"] = rfq.RfqNumber,
                            ["AwardedLineCount"] = awardedCount,
                            ["TotalLineCount"] = totalLineCount,
                            ["AwardedLineNumbers"] = lineNumbers,
                            ["ActionUrl"] = actionUrl
                        },
                        tenantId: bp.TenantId
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create in-app RFQ award notification for user {UserId}", userId);
                }
            }

            // Email notification (sent to supplier primary email).
            if (!string.IsNullOrWhiteSpace(bp.PrimaryEmail))
            {
                try
                {
                    var body = GenerateRfqAwardEmailBody(
                        rfqNumber: rfq.RfqNumber,
                        title: rfq.Title,
                        currency: rfq.Currency,
                        partnerName: bp.PartnerName,
                        actionUrl: actionUrl,
                        awardedCount: awardedCount,
                        totalLineCount: totalLineCount,
                        lines: awardedLines.Select(x => new RfqAwardEmailLine
                        {
                            LineNumber = x.Item?.LineNumber,
                            ItemCode = x.Item?.ItemCode,
                            Description = x.Item?.Description ?? string.Empty,
                            Quantity = x.Item?.Quantity,
                            UnitOfMeasure = x.Item?.UnitOfMeasure,
                            UnitPrice = x.Award.UnitPrice,
                            LineTotal = x.Award.LineTotal
                        }).ToList());

                    await _notificationService.SendEmailAsync(bp.PrimaryEmail, subject, body, isHtml: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send RFQ award email notification to business partner {BusinessPartnerId}", bp.Id);
                }
            }
        }
    }

    private sealed class RfqAwardEmailLine
    {
        public int? LineNumber { get; set; }
        public string? ItemCode { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public string? UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    private static string GenerateRfqAwardEmailBody(
        string rfqNumber,
        string title,
        string currency,
        string partnerName,
        string actionUrl,
        int awardedCount,
        int totalLineCount,
        List<RfqAwardEmailLine> lines)
    {
        var safeRfqNumber = WebUtility.HtmlEncode(rfqNumber);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safePartnerName = WebUtility.HtmlEncode(partnerName);
        var safeCurrency = WebUtility.HtmlEncode(currency);

        var summaryText = awardedCount == totalLineCount
            ? $"All {awardedCount} RFQ line(s) were awarded to you."
            : $"You were awarded {awardedCount} of {totalLineCount} RFQ line(s).";

        var rows = string.Join("", (lines ?? new List<RfqAwardEmailLine>())
            .OrderBy(l => l.LineNumber ?? int.MaxValue)
            .Select(l =>
            {
                var ln = l.LineNumber?.ToString() ?? "";
                var code = WebUtility.HtmlEncode(l.ItemCode ?? "");
                var desc = WebUtility.HtmlEncode(l.Description ?? "");
                var qty = l.Quantity.HasValue ? l.Quantity.Value.ToString("N2") : "";
                var uom = WebUtility.HtmlEncode(l.UnitOfMeasure ?? "");
                var unitPrice = l.UnitPrice.ToString("N4");
                var lineTotal = l.LineTotal.ToString("N2");

                return $@"
        <tr>
          <td style='padding:8px; border-bottom:1px solid #e5e7eb; font-size:13px; color:#111827;'>{ln}</td>
          <td style='padding:8px; border-bottom:1px solid #e5e7eb; font-size:13px; color:#111827;'>{code}</td>
          <td style='padding:8px; border-bottom:1px solid #e5e7eb; font-size:13px; color:#111827;'>{desc}</td>
          <td style='padding:8px; border-bottom:1px solid #e5e7eb; font-size:13px; color:#111827; text-align:right;'>{qty}</td>
          <td style='padding:8px; border-bottom:1px solid #e5e7eb; font-size:13px; color:#111827;'>{uom}</td>
          <td style='padding:8px; border-bottom:1px solid #e5e7eb; font-size:13px; color:#111827; text-align:right;'>{unitPrice}</td>
          <td style='padding:8px; border-bottom:1px solid #e5e7eb; font-size:13px; color:#111827; text-align:right;'>{lineTotal}</td>
        </tr>";
            }));

        return $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; color: #111827; line-height: 1.6;'>
  <div style='max-width: 760px; margin: 0 auto; padding: 16px;'>
    <h2 style='margin: 0 0 8px 0;'>RFQ Award Result</h2>
    <div style='padding: 12px; background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px;'>
      <div><strong>RFQ:</strong> {safeRfqNumber}</div>
      <div><strong>Title:</strong> {safeTitle}</div>
      <div><strong>Supplier:</strong> {safePartnerName}</div>
      <div><strong>Summary:</strong> {WebUtility.HtmlEncode(summaryText)}</div>
      <div style='margin-top: 12px;'>
        <a href='{actionUrl}' target='_blank' rel='noreferrer'
           style='display:inline-block; background:#0f766e; color:#ffffff; text-decoration:none; padding:10px 14px; border-radius:6px;'>
          Open RFQ
        </a>
      </div>
    </div>

    <h3 style='margin: 16px 0 8px 0;'>Awarded Line(s)</h3>
    <div style='border: 1px solid #e5e7eb; border-radius: 8px; overflow: hidden;'>
      <table style='width: 100%; border-collapse: collapse;'>
        <thead style='background: #f3f4f6;'>
          <tr>
            <th style='text-align:left; padding:8px; font-size:12px; color:#374151;'>Line</th>
            <th style='text-align:left; padding:8px; font-size:12px; color:#374151;'>Code</th>
            <th style='text-align:left; padding:8px; font-size:12px; color:#374151;'>Description</th>
            <th style='text-align:right; padding:8px; font-size:12px; color:#374151;'>Qty</th>
            <th style='text-align:left; padding:8px; font-size:12px; color:#374151;'>UOM</th>
            <th style='text-align:right; padding:8px; font-size:12px; color:#374151;'>Unit Price</th>
            <th style='text-align:right; padding:8px; font-size:12px; color:#374151;'>Line Total ({safeCurrency})</th>
          </tr>
        </thead>
        <tbody>
          {rows}
        </tbody>
      </table>
    </div>

    <div style='margin-top: 10px; font-size: 12px; color: #6b7280;'>This is an automated notification from the ERP procurement system.</div>
  </div>
</body>
</html>";
    }

    private static string GenerateRfqInvitationEmailBody(string rfqNumber, string title, string actionUrl, string recipientName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #111827; }}
        .container {{ max-width: 700px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0f766e; color: white; padding: 18px; border-radius: 8px; }}
        .content {{ background-color: #f9fafb; padding: 18px; margin-top: 14px; border-radius: 8px; }}
        .cta {{ display: inline-block; padding: 10px 14px; background-color: #0f766e; color: white !important; text-decoration: none; border-radius: 6px; }}
        .muted {{ color: #6b7280; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin:0;'>Request For Quotation</h2>
            <div style='margin-top:6px; font-size: 14px;'><strong>{rfqNumber}</strong> — {title}</div>
        </div>
        <div class='content'>
            <p>Dear {recipientName},</p>
            <p>You have been invited to submit a quotation. Please review the attached RFQ document for item details.</p>
            <p>
                <a class='cta' href='{actionUrl}' target='_blank' rel='noreferrer'>Open RFQ in Supplier Portal</a>
            </p>
            <p class='muted'>If you do not have portal access, you can still respond by email using the attached RFQ details.</p>
        </div>
        <p class='muted' style='margin-top:14px;'>This is an automated message from the ERP procurement system.</p>
    </div>
</body>
</html>";
    }
}
