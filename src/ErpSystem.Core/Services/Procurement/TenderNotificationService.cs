using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderNotificationService : ITenderNotificationService
{
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderAwardRepository _awardRepository;
    private readonly ITenderInterviewRepository _interviewRepository;
    private readonly ITenderClarificationRepository _clarificationRepository;
    private readonly ITenderRevisionRepository _revisionRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ITenderInvitationDocumentService _tenderInvitationDocumentService;
    private readonly INotificationService _notificationService;
    private readonly IAwardLetterService _awardLetterService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenderNotificationService> _logger;

    public TenderNotificationService(
        ITenderRepository tenderRepository,
        ITenderBidRepository bidRepository,
        ITenderAwardRepository awardRepository,
        ITenderInterviewRepository interviewRepository,
        ITenderClarificationRepository clarificationRepository,
        ITenderRevisionRepository revisionRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ITenderInvitationDocumentService tenderInvitationDocumentService,
        INotificationService notificationService,
        IAwardLetterService awardLetterService,
        ICurrentUserProvider currentUserProvider,
        IConfiguration configuration,
        ILogger<TenderNotificationService> logger)
    {
        _tenderRepository = tenderRepository;
        _bidRepository = bidRepository;
        _awardRepository = awardRepository;
        _interviewRepository = interviewRepository;
        _clarificationRepository = clarificationRepository;
        _revisionRepository = revisionRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _tenderInvitationDocumentService = tenderInvitationDocumentService;
        _notificationService = notificationService;
        _awardLetterService = awardLetterService;
        _currentUserProvider = currentUserProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendTenderPublishedNotificationAsync(Guid tenderId, List<Guid> businessPartnerIds, List<string>? externalRecipientEmails = null)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            // For RFQs, attach a concise PDF package so even pending/unregistered suppliers can respond.
            // We generate once per tender and attach to each recipient (sent individually for privacy).
            List<EmailAttachmentInfo>? rfqAttachments = null;
            if (string.Equals(tender.TenderType, "RFQ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var (content, fileName) = await _tenderInvitationDocumentService.GenerateTenderInvitationPdfAsync(tenderId);
                    rfqAttachments = new List<EmailAttachmentInfo>
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
                    _logger.LogError(ex, "Failed to generate RFQ invitation PDF for tender {TenderId}. Continuing without attachments.", tenderId);
                    rfqAttachments = null;
                }
            }

            foreach (var businessPartnerId in businessPartnerIds)
            {
                // Get business partner details for email and user ID
                var businessPartner = await _businessPartnerRepository.GetByIdAsync(businessPartnerId);

                if (businessPartner == null)
                {
                    _logger.LogWarning("Business partner {BusinessPartnerId} not found, skipping notification", businessPartnerId);
                    continue;
                }

                // Check if business partner has a linked user account
                if (!businessPartner.UserId.HasValue)
                {
                    _logger.LogWarning("Business partner {PartnerCode} does not have a linked user account, skipping in-app notification",
                        businessPartner.PartnerCode);
                }
                else
                {
                    // Create in-app notification using the user ID
                    await _notificationService.CreateNotificationAsync(
                        new CreateNotificationDto
                        {
                            RecipientId = businessPartner.UserId.Value,
                            Type = "InApp",
                            Title = "New Tender Invitation",
                            Message = $"You have been invited to tender {tender.TenderNumber} - {tender.Title}. Submission deadline: {tender.SubmissionDeadline:yyyy-MM-dd HH:mm}",
                            Priority = "High",
                            EntityType = "Tender",
                            EntityId = tenderId,
                            ActionUrl = $"/external-portal/tenders/{tenderId}"
                        },
                        _currentUserProvider.UserId,
                        _currentUserProvider.TenantId
                    );

                    _logger.LogInformation("Sent in-app notification to user {UserId} for business partner {PartnerCode}",
                        businessPartner.UserId.Value, businessPartner.PartnerCode);
                }

                // Send email notification if business partner has email
                if (!string.IsNullOrEmpty(businessPartner.PrimaryEmail))
                {
                    var emailSubject = $"Tender Invitation: {tender.TenderNumber}";
                    var emailBody = GenerateTenderInvitationEmailBody(tender, businessPartner);
                    if (rfqAttachments != null && rfqAttachments.Count > 0)
                    {
                        await _notificationService.SendEmailWithAttachmentsAsync(
                            businessPartner.PrimaryEmail,
                            emailSubject,
                            emailBody,
                            rfqAttachments,
                            isHtml: true
                        );
                    }
                    else
                    {
                        await _notificationService.SendEmailAsync(
                            businessPartner.PrimaryEmail,
                            emailSubject,
                            emailBody,
                            isHtml: true
                        );
                    }

                    _logger.LogInformation("Sent tender invitation email to {Email} for tender {TenderId}",
                        businessPartner.PrimaryEmail, tenderId);
                }
            }

            if (externalRecipientEmails != null && externalRecipientEmails.Any())
            {
                var distinctExternalEmails = externalRecipientEmails
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var email in distinctExternalEmails)
                {
                    if (!email.Contains('@'))
                    {
                        _logger.LogWarning("Skipping invalid external recipient email: {Email}", email);
                        continue;
                    }

                    var emailSubject = $"Tender Invitation: {tender.TenderNumber}";
                    var emailBody = GenerateTenderInvitationEmailBody(tender, businessPartner: null);

                    if (rfqAttachments != null && rfqAttachments.Count > 0)
                    {
                        await _notificationService.SendEmailWithAttachmentsAsync(
                            email,
                            emailSubject,
                            emailBody,
                            rfqAttachments,
                            isHtml: true
                        );
                    }
                    else
                    {
                        await _notificationService.SendEmailAsync(
                            email,
                            emailSubject,
                            emailBody,
                            isHtml: true
                        );
                    }

                    _logger.LogInformation("Sent external tender invitation email to {Email} for tender {TenderId}", email, tenderId);
                }
            }

            _logger.LogInformation(
                "Sent tender published notifications for tender {TenderId} to {BusinessPartnerCount} business partners and {ExternalCount} external recipients",
                tenderId, businessPartnerIds.Count, externalRecipientEmails?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tender published notifications for tender {TenderId}", tenderId);
            throw;
        }
    }

    private string GetTenderTypeFullName(string tenderType)
    {
        return tenderType switch
        {
            "RFQ" => "Request for Quotation (RFQ)",
            "RFP" => "Request for Proposal (RFP)",
            "ITB" => "Invitation to Bid (ITB)",
            "EOI" => "Expression of Interest (EOI)",
            _ => tenderType
        };
    }

    private string GenerateTenderInvitationEmailBody(Entities.Procurement.Tender tender, Entities.Procurement.BusinessPartner? businessPartner)
    {
        var portalUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
        var tenderTypeFullName = GetTenderTypeFullName(tender.TenderType);
        var recipientName = businessPartner?.PartnerName ?? "Supplier";
        var showPortalCta = businessPartner?.UserId.HasValue == true;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2563eb; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9fafb; padding: 20px; margin-top: 20px; }}
        .details {{ background-color: white; padding: 15px; margin: 15px 0; border-left: 4px solid #2563eb; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #2563eb; color: white; text-decoration: none; border-radius: 4px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Tender Invitation</h1>
        </div>
        <div class=""content"">
            <p>Dear {recipientName},</p>

            <p>You have been invited to participate in the following tender:</p>

            <div class=""details"">
                <h3>{tender.Title}</h3>
                <p><strong>Tender Number:</strong> {tender.TenderNumber}</p>
                <p><strong>Type:</strong> {tenderTypeFullName}</p>
                <p><strong>Submission Deadline:</strong> {tender.SubmissionDeadline:dddd, MMMM dd, yyyy 'at' HH:mm}</p>
                {(tender.OpeningDate.HasValue ? $"<p><strong>Opening Date:</strong> {tender.OpeningDate.Value:dddd, MMMM dd, yyyy 'at' HH:mm}</p>" : "")}
            </div>

            {(!string.IsNullOrEmpty(tender.Description) ? $"<p><strong>Description:</strong><br/>{tender.Description}</p>" : "")}

            {(string.Equals(tender.TenderType, "RFQ", StringComparison.OrdinalIgnoreCase)
                ? "<p>Please find the RFQ details attached as a PDF.</p>"
                : "<p>Please review the tender details and prepare your submission.</p>")}

            <div style=""text-align: center;"">
                {(showPortalCta ? $@"<a href=""{portalUrl}/external-portal/tenders/{tender.Id}"" class=""button"">View Tender Details</a>" : "")}
            </div>

            <p><strong>Important Notes:</strong></p>
            <ul>
                <li>Ensure you submit your bid before the deadline</li>
                <li>All required documents must be uploaded</li>
                <li>Late submissions will not be accepted</li>
            </ul>
        </div>
        <div class=""footer"">
            <p>This is an automated message. Please do not reply to this email.</p>
            <p>&copy; {DateTime.UtcNow.Year} ERP System. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public async Task SendBidSubmittedNotificationAsync(Guid bidId)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId) 
                ?? throw new InvalidOperationException($"Bid with ID {bidId} not found");

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId) 
                ?? throw new InvalidOperationException($"Tender with ID {bid.TenderId} not found");

            if (tender.CreatedById.HasValue)
            {
                await _notificationService.CreateNotificationAsync(
                    new CreateNotificationDto
                    {
                        RecipientId = tender.CreatedById.Value,
                        Type = "InApp",
                        Title = "New Bid Submitted",
                        Message = $"A new bid ({bid.BidNumber}) has been submitted for tender {tender.TenderNumber}",
                        Priority = "Normal",
                        EntityType = "TenderBid",
                        EntityId = bidId,
                        ActionUrl = $"/tenders/{tender.Id}/bids/{bidId}"
                    },
                    _currentUserProvider.UserId,
                    _currentUserProvider.TenantId
                );
            }

            _logger.LogInformation("Sent bid submitted notification for bid {BidId}", bidId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bid submitted notification for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task SendEvaluationAssignedNotificationAsync(Guid tenderId, List<Guid> evaluatorIds)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId) 
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            foreach (var evaluatorId in evaluatorIds)
            {
                await _notificationService.CreateNotificationAsync(
                    new CreateNotificationDto
                    {
                        RecipientId = evaluatorId,
                        Type = "InApp",
                        Title = "Tender Evaluation Assignment",
                        Message = $"You have been assigned to evaluate tender {tender.TenderNumber} - {tender.Title}",
                        Priority = "High",
                        EntityType = "Tender",
                        EntityId = tenderId,
                        ActionUrl = $"/tenders/{tenderId}/evaluations"
                    },
                    _currentUserProvider.UserId,
                    _currentUserProvider.TenantId
                );
            }

            _logger.LogInformation("Sent evaluation assigned notifications for tender {TenderId} to {Count} evaluators",
                tenderId, evaluatorIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending evaluation assigned notifications for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task SendInterviewScheduledNotificationAsync(Guid interviewId)
    {
        try
        {
            var interview = await _interviewRepository.GetByIdAsync(interviewId)
                ?? throw new InvalidOperationException($"Interview with ID {interviewId} not found");

            var bid = await _bidRepository.GetByIdAsync(interview.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {interview.TenderBidId} not found");

            await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = bid.BusinessPartnerId,
                    Type = "InApp",
                    Title = "Interview Scheduled",
                    Message = $"An interview has been scheduled for your bid on {interview.ScheduledDate:yyyy-MM-dd HH:mm}. Location: {interview.Location}",
                    Priority = "High",
                    EntityType = "TenderInterview",
                    EntityId = interviewId,
                    ActionUrl = $"/tenders/interviews/{interviewId}"
                },
                _currentUserProvider.UserId,
                _currentUserProvider.TenantId
            );

            _logger.LogInformation("Sent interview scheduled notification for interview {InterviewId}", interviewId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending interview scheduled notification for interview {InterviewId}", interviewId);
            throw;
        }
    }

    public async Task SendAwardNotificationAsync(Guid awardId)
    {
        try
        {
            var award = await _awardRepository.GetByIdAsync(awardId)
                ?? throw new InvalidOperationException($"Award with ID {awardId} not found");

            var bid = await _bidRepository.GetByIdAsync(award.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {award.TenderBidId} not found");

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {bid.TenderId} not found");

            // Get business partner details for user ID and email
            var businessPartner = await _businessPartnerRepository.GetByIdAsync(bid.BusinessPartnerId);
            if (businessPartner == null)
            {
                _logger.LogWarning("Business partner {BusinessPartnerId} not found for award notification", bid.BusinessPartnerId);
                return;
            }

            _logger.LogInformation("Processing award notification for business partner {PartnerCode} ({PartnerId}), UserId: {UserId}, Email: {Email}",
                businessPartner.PartnerCode, businessPartner.Id, businessPartner.UserId, businessPartner.PrimaryEmail);

            // Send in-app notification if business partner has a linked user account
            if (businessPartner.UserId.HasValue)
            {
                // Build award amount message including negotiation info if applicable
                var awardAmountMessage = award.IsNegotiated
                    ? $"Award amount: {award.AwardedAmount:N2} {award.Currency} (negotiated from {award.OriginalBidAmount:N2})"
                    : $"Award amount: {award.AwardedAmount:N2} {award.Currency}";

                await _notificationService.CreateNotificationAsync(
                    new CreateNotificationDto
                    {
                        RecipientId = businessPartner.UserId.Value,
                        Type = "InApp",
                        Title = "🏆 Congratulations! Tender Awarded",
                        Message = $"Your bid for tender {tender.TenderNumber} - {tender.Title} has been awarded! {awardAmountMessage}",
                        Priority = "High",
                        EntityType = "TenderAward",
                        EntityId = awardId,
                        ActionUrl = $"/external-portal/my-bids/{bid.Id}"
                    },
                    _currentUserProvider.UserId,
                    _currentUserProvider.TenantId
                );

                _logger.LogInformation("Sent in-app award notification to user {UserId} for award {AwardId}",
                    businessPartner.UserId.Value, awardId);
            }
            else
            {
                _logger.LogWarning("Business partner {PartnerCode} ({PartnerId}) has no linked UserId, skipping in-app notification",
                    businessPartner.PartnerCode, businessPartner.Id);
            }

            // Send email notification if business partner has email
            if (!string.IsNullOrEmpty(businessPartner.PrimaryEmail))
            {
                _logger.LogInformation("Sending award email to {Email} for award {AwardId}",
                    businessPartner.PrimaryEmail, awardId);

                var emailSubject = $"🏆 Congratulations! You Have Been Awarded Tender {tender.TenderNumber}";
                var emailBody = GenerateAwardEmailBody(tender, businessPartner, award, bid);

                // Generate PDF award letter
                var attachments = new List<EmailAttachmentInfo>();
                try
                {
                    var (pdfBytes, fileName) = await _awardLetterService.GenerateAwardLetterAsync(awardId);
                    attachments.Add(new EmailAttachmentInfo
                    {
                        FileName = fileName,
                        Content = pdfBytes,
                        ContentType = "application/pdf"
                    });
                    _logger.LogInformation("Generated PDF award letter {FileName} ({Size} bytes) for award {AwardId}",
                        fileName, pdfBytes.Length, awardId);
                }
                catch (Exception pdfEx)
                {
                    _logger.LogWarning(pdfEx, "Failed to generate PDF award letter for award {AwardId}, sending email without attachment", awardId);
                }

                // Send email with PDF attachment
                await _notificationService.SendEmailWithAttachmentsAsync(
                    businessPartner.PrimaryEmail,
                    emailSubject,
                    emailBody,
                    attachments,
                    isHtml: true
                );

                _logger.LogInformation("Award email sent successfully to {Email} for award {AwardId} with {AttachmentCount} attachment(s)",
                    businessPartner.PrimaryEmail, awardId, attachments.Count);
            }
            else
            {
                _logger.LogWarning("Business partner {PartnerCode} ({PartnerId}) has no PrimaryEmail configured, skipping email notification",
                    businessPartner.PartnerCode, businessPartner.Id);
            }

            _logger.LogInformation("Completed award notification for award {AwardId}", awardId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending award notification for award {AwardId}", awardId);
            throw;
        }
    }

    private string GenerateAwardEmailBody(Entities.Procurement.Tender tender, Entities.Procurement.BusinessPartner businessPartner, Entities.Procurement.TenderAward award, Entities.Procurement.TenderBid bid)
    {
        var portalUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
        var tenderTypeFullName = GetTenderTypeFullName(tender.TenderType);

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Tender Award Notification</title>
</head>
<body style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background-color: #f5f5f5;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff;"">
        <!-- Header with celebration gradient -->
        <div style=""background: linear-gradient(135deg, #10b981 0%, #059669 50%, #047857 100%); color: white; padding: 40px 30px; text-align: center;"">
            <div style=""font-size: 60px; margin-bottom: 15px;"">🏆</div>
            <h1 style=""margin: 0; font-size: 28px; font-weight: 600;"">Congratulations!</h1>
            <p style=""margin: 10px 0 0 0; font-size: 16px; opacity: 0.95;"">You Have Been Awarded a Tender</p>
        </div>

        <!-- Main content -->
        <div style=""padding: 30px;"">
            <p style=""font-size: 16px; color: #374151; margin-bottom: 20px;"">
                Dear <strong>{businessPartner.PartnerName}</strong>,
            </p>

            <p style=""font-size: 16px; color: #374151; margin-bottom: 25px;"">
                We are pleased to inform you that your bid has been successfully awarded for the following tender:
            </p>

            <!-- Tender Details Card -->
            <div style=""background: linear-gradient(to right, #ecfdf5, #d1fae5); border: 2px solid #10b981; border-radius: 12px; padding: 25px; margin-bottom: 25px;"">
                <h2 style=""margin: 0 0 15px 0; color: #047857; font-size: 18px;"">{tender.TenderNumber}</h2>
                <p style=""margin: 0 0 15px 0; font-size: 16px; color: #374151; font-weight: 500;"">{tender.Title}</p>

                <table style=""width: 100%; border-collapse: collapse;"">
                    <tr>
                        <td style=""padding: 8px 0; color: #6b7280; font-size: 14px;"">Tender Type:</td>
                        <td style=""padding: 8px 0; color: #111827; font-size: 14px; font-weight: 500;"">{tenderTypeFullName}</td>
                    </tr>
                    <tr>
                        <td style=""padding: 8px 0; color: #6b7280; font-size: 14px;"">Your Bid Number:</td>
                        <td style=""padding: 8px 0; color: #111827; font-size: 14px; font-weight: 500;"">{bid.BidNumber}</td>
                    </tr>
                </table>
            </div>

            <!-- Award Amount Highlight -->
            <div style=""background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%); border: 2px solid #f59e0b; border-radius: 12px; padding: 25px; margin-bottom: 25px; text-align: center;"">
                <p style=""margin: 0 0 8px 0; color: #92400e; font-size: 14px; text-transform: uppercase; letter-spacing: 1px;"">Award Amount{(award.IsNegotiated ? " (Negotiated)" : "")}</p>
                <p style=""margin: 0; color: #78350f; font-size: 32px; font-weight: 700;"">{award.Currency} {award.AwardedAmount:N2}</p>
                {(award.IsNegotiated ? $@"<p style=""margin: 8px 0 0 0; color: #92400e; font-size: 13px; text-decoration: line-through;"">Original: {award.Currency} {award.OriginalBidAmount:N2}</p>" : "")}
                <p style=""margin: 10px 0 0 0; color: #92400e; font-size: 14px;"">Awarded on {award.AwardDate:MMMM dd, yyyy}</p>
            </div>

            <p style=""font-size: 15px; color: #374151; margin-bottom: 20px;"">
                <strong>Next Steps:</strong>
            </p>
            <ul style=""color: #374151; font-size: 15px; padding-left: 20px; margin-bottom: 25px;"">
                <li style=""margin-bottom: 8px;"">Our team will contact you shortly to discuss contract finalization</li>
                <li style=""margin-bottom: 8px;"">Please prepare all necessary documentation for contract signing</li>
                <li style=""margin-bottom: 8px;"">Log in to the portal to view your award details</li>
            </ul>

            <!-- CTA Button -->
            <div style=""text-align: center; margin: 30px 0;"">
                <a href=""{portalUrl}/external-portal/my-bids/{bid.Id}""
                   style=""display: inline-block; background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: white; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 16px; box-shadow: 0 4px 14px rgba(16, 185, 129, 0.4);"">
                    View Award Details
                </a>
            </div>

            <p style=""font-size: 15px; color: #374151;"">
                Thank you for your participation in our procurement process. We look forward to a successful partnership.
            </p>

            <p style=""font-size: 15px; color: #374151; margin-top: 25px;"">
                Best regards,<br>
                <strong>Procurement Team</strong>
            </p>
        </div>

        <!-- Footer -->
        <div style=""background-color: #f9fafb; padding: 20px 30px; text-align: center; border-top: 1px solid #e5e7eb;"">
            <p style=""margin: 0; color: #6b7280; font-size: 13px;"">
                This is an automated notification. Please do not reply to this email.
            </p>
            <p style=""margin: 10px 0 0 0; color: #9ca3af; font-size: 12px;"">
                &copy; {DateTime.UtcNow.Year} ERP System. All rights reserved.
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public async Task SendRejectionNotificationsAsync(Guid tenderId, List<Guid> rejectedBidIds)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            foreach (var bidId in rejectedBidIds)
            {
                var bid = await _bidRepository.GetByIdAsync(bidId);
                if (bid == null) continue;

                // Get business partner details for user ID and email
                var businessPartner = await _businessPartnerRepository.GetByIdAsync(bid.BusinessPartnerId);
                if (businessPartner == null) continue;

                // Send in-app notification if business partner has a linked user account
                if (businessPartner.UserId.HasValue)
                {
                    await _notificationService.CreateNotificationAsync(
                        new CreateNotificationDto
                        {
                            RecipientId = businessPartner.UserId.Value,
                            Type = "InApp",
                            Title = "Tender Result Notification",
                            Message = $"Thank you for your bid on tender {tender.TenderNumber} - {tender.Title}. Unfortunately, your bid was not successful this time.",
                            Priority = "Normal",
                            EntityType = "TenderBid",
                            EntityId = bidId,
                            ActionUrl = $"/external-portal/my-bids/{bidId}"
                        },
                        _currentUserProvider.UserId,
                        _currentUserProvider.TenantId
                    );
                }

                // Send email notification if business partner has email
                if (!string.IsNullOrEmpty(businessPartner.PrimaryEmail))
                {
                    var emailSubject = $"Tender Result Notification - {tender.TenderNumber}";
                    var emailBody = GenerateRejectionEmailBody(tender, businessPartner, bid);

                    await _notificationService.SendEmailAsync(
                        businessPartner.PrimaryEmail,
                        emailSubject,
                        emailBody,
                        isHtml: true
                    );
                }
            }

            _logger.LogInformation("Sent rejection notifications for tender {TenderId} to {Count} bids",
                tenderId, rejectedBidIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending rejection notifications for tender {TenderId}", tenderId);
            throw;
        }
    }

    private string GenerateRejectionEmailBody(Entities.Procurement.Tender tender, Entities.Procurement.BusinessPartner businessPartner, Entities.Procurement.TenderBid bid)
    {
        var portalUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Tender Result Notification</title>
</head>
<body style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background-color: #f5f5f5;"">
    <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff;"">
        <div style=""background-color: #374151; color: white; padding: 30px; text-align: center;"">
            <h1 style=""margin: 0; font-size: 24px; font-weight: 600;"">Tender Result Notification</h1>
        </div>

        <div style=""padding: 30px;"">
            <p style=""font-size: 16px; color: #374151; margin-bottom: 20px;"">
                Dear <strong>{businessPartner.PartnerName}</strong>,
            </p>

            <p style=""font-size: 16px; color: #374151; margin-bottom: 25px;"">
                Thank you for your participation in the following tender:
            </p>

            <div style=""background-color: #f3f4f6; border-radius: 8px; padding: 20px; margin-bottom: 25px;"">
                <h2 style=""margin: 0 0 10px 0; color: #374151; font-size: 16px;"">{tender.TenderNumber}</h2>
                <p style=""margin: 0; font-size: 15px; color: #6b7280;"">{tender.Title}</p>
                <p style=""margin: 10px 0 0 0; font-size: 14px; color: #6b7280;"">Your Bid: {bid.BidNumber}</p>
            </div>

            <p style=""font-size: 15px; color: #374151; margin-bottom: 20px;"">
                After careful evaluation of all submissions, we regret to inform you that your bid was not selected for this tender.
            </p>

            <p style=""font-size: 15px; color: #374151; margin-bottom: 20px;"">
                We appreciate your time and effort in preparing your bid and encourage you to participate in our future procurement opportunities.
            </p>

            <div style=""text-align: center; margin: 30px 0;"">
                <a href=""{portalUrl}/external-portal/tenders""
                   style=""display: inline-block; background-color: #374151; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; font-weight: 500;"">
                    View Other Tenders
                </a>
            </div>

            <p style=""font-size: 15px; color: #374151;"">
                Best regards,<br>
                <strong>Procurement Team</strong>
            </p>
        </div>

        <div style=""background-color: #f9fafb; padding: 20px 30px; text-align: center; border-top: 1px solid #e5e7eb;"">
            <p style=""margin: 0; color: #6b7280; font-size: 13px;"">
                This is an automated notification. Please do not reply to this email.
            </p>
            <p style=""margin: 10px 0 0 0; color: #9ca3af; font-size: 12px;"">
                &copy; {DateTime.UtcNow.Year} ERP System. All rights reserved.
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public async Task SendClarificationNotificationAsync(Guid clarificationId, bool isAnswer = false)
    {
        try
        {
            var clarification = await _clarificationRepository.GetByIdAsync(clarificationId)
                ?? throw new InvalidOperationException($"Clarification with ID {clarificationId} not found");

            var tender = await _tenderRepository.GetByIdAsync(clarification.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {clarification.TenderId} not found");

            Guid recipientId;
            string title;
            string message;

            if (isAnswer)
            {
                recipientId = clarification.BusinessPartnerId ?? Guid.Empty;
                title = "Clarification Answered";
                message = $"Your clarification request for tender {tender.TenderNumber} has been answered.";
            }
            else
            {
                recipientId = tender.CreatedById ?? _currentUserProvider.UserId;
                title = "New Clarification Request";
                message = $"A new clarification request has been received for tender {tender.TenderNumber}.";
            }

            await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = recipientId,
                    Type = "InApp",
                    Title = title,
                    Message = message,
                    Priority = "Normal",
                    EntityType = "TenderClarification",
                    EntityId = clarificationId,
                    ActionUrl = $"/tenders/{tender.Id}/clarifications/{clarificationId}"
                },
                _currentUserProvider.UserId,
                _currentUserProvider.TenantId
            );

            _logger.LogInformation("Sent clarification notification for clarification {ClarificationId}", clarificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending clarification notification for clarification {ClarificationId}", clarificationId);
            throw;
        }
    }

    public async Task SendRevisionNotificationAsync(Guid revisionId)
    {
        try
        {
            var revision = await _revisionRepository.GetByIdAsync(revisionId)
                ?? throw new InvalidOperationException($"Revision with ID {revisionId} not found");

            var tender = await _tenderRepository.GetByIdAsync(revision.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {revision.TenderId} not found");

            // Get all bids for this tender to notify bidders
            var bids = await _bidRepository.GetByTenderIdAsync(tender.Id);

            foreach (var bid in bids)
            {
                await _notificationService.CreateNotificationAsync(
                    new CreateNotificationDto
                    {
                        RecipientId = bid.BusinessPartnerId,
                        Type = "InApp",
                        Title = "Tender Revised",
                        Message = $"Tender {tender.TenderNumber} - {tender.Title} has been revised. Revision: {revision.Description}",
                        Priority = "High",
                        EntityType = "TenderRevision",
                        EntityId = revisionId,
                        ActionUrl = $"/tenders/{tender.Id}/revisions/{revisionId}"
                    },
                    _currentUserProvider.UserId,
                    _currentUserProvider.TenantId
                );
            }

            _logger.LogInformation("Sent revision notifications for revision {RevisionId} to {Count} bidders",
                revisionId, bids.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending revision notification for revision {RevisionId}", revisionId);
            throw;
        }
    }
}
