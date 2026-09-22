using ErpSystem.Core.Interfaces.Common;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// The single source of truth for recruitment transactional emails: their stable event keys, shipped
/// default subject/body (tokenised copies of the formerly-hardcoded inline HTML), and the tokens each
/// exposes. Consumed by:
/// <list type="bullet">
///   <item>the seeder — to create the editable default <c>EmailTemplate</c> rows per tenant;</item>
///   <item><see cref="ErpSystem.Core.Services.Common.TemplatedEmailService"/> — as the runtime fallback
///     when no stored template exists;</item>
///   <item>the token-catalogue API — to drive the authoring palette + preview sample data.</item>
/// </list>
///
/// <para>Body strings use non-interpolated verbatim literals so the <c>{{Token}}</c> / <c>{{#if}}</c>
/// merge syntax appears verbatim; the composed chrome (<see cref="Shell"/>, <see cref="PrimaryButton"/>)
/// is concatenated in.</para>
/// </summary>
public static class RecruitmentEmailCatalog
{
    public const string Module = "Recruitment";

    // ── Event keys (stable; referenced from services) ──────────────────────────
    public static class Events
    {
        public const string ApplicationReceived     = "ApplicationReceived";
        public const string ApplicationWithdrawn     = "ApplicationWithdrawn";
        public const string ApplicationUnderReview    = "ApplicationUnderReview";
        public const string ApplicationShortlisted    = "ApplicationShortlisted";
        public const string ApplicationRejected       = "ApplicationRejected";
        public const string AssessmentPending          = "AssessmentPending";
        public const string InterviewInvitation        = "InterviewInvitation";
        public const string InterviewRescheduled       = "InterviewRescheduled";
        public const string InterviewPanelistAssignment = "InterviewPanelistAssignment";
        public const string OfferIssued                = "OfferIssued";
        public const string OfferAccepted              = "OfferAccepted";
        public const string OfferLetter                = "OfferLetter";
        /// <summary>
        /// The link that activates a self-registered careers account — round 4.
        /// </summary>
        /// <remarks>
        /// ⚠ This is the FIRST thing a candidate ever receives from the organisation, sent before
        /// they can sign in, and for many it is the only one they will see if it does not work. It
        /// belongs in the catalogue for the same reason every other letter does: the wording of
        /// something that leaves the building is the employer's, not a developer's.
        /// </remarks>
        public const string CandidateAccountActivation = "CandidateAccountActivation";
    }

    private static IReadOnlyList<EmailEventDescriptor>? _all;

    public static IReadOnlyList<EmailEventDescriptor> All => _all ??= Build();

    public static EmailEventDescriptor? Find(string eventKey) =>
        All.FirstOrDefault(e => string.Equals(e.EventKey, eventKey, StringComparison.OrdinalIgnoreCase));

    // ── HTML shell helpers (mirror the original inline-email chrome) ────────────
    private const string BlueGradient = "linear-gradient(135deg,#1e3a8a,#1a56db)";
    private const string GreenGradient = "linear-gradient(135deg,#065f46,#059669)";

    private static string Shell(string gradient, string headerHtml, string innerHtml) =>
        "\n<html><body style='font-family:sans-serif;color:#374151;max-width:600px;margin:0 auto'>\n" +
        $"<div style='background:{gradient};padding:2rem;border-radius:8px 8px 0 0'>\n" +
        $"  <h1 style='color:#fff;margin:0;font-size:1.5rem'>{headerHtml}</h1>\n" +
        "</div>\n" +
        "<div style='background:#f9fafb;padding:1.5rem;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 8px 8px'>\n" +
        innerHtml + "\n" +
        "</div>\n</body></html>";

    private static string PrimaryButton(string href, string label) =>
        "\n  <p style='margin-top:1.5rem'>\n" +
        $"    <a href='{href}'\n" +
        "       style='background:#1a56db;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>\n" +
        $"      {label}\n" +
        "    </a>\n  </p>";

    private static EmailTokenDescriptor T(string token, string desc, string sample) => new(token, desc, sample);

    private static readonly EmailTokenDescriptor PortalUrlToken =
        T("PortalUrl", "Base URL of the candidate careers portal.", "https://careers.example.com");

    private static List<EmailEventDescriptor> Build()
    {
        var list = new List<EmailEventDescriptor>();

        // ── 0. Account activation ──────────────────────────────────────────────
        // Deliberately first: it is the earliest email in the candidate's whole journey, and it
        // is the one that decides whether there IS a journey.
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.CandidateAccountActivation,
            Name = "Careers Account Activation",
            Category = "Account",
            Description =
                "Sent the moment someone registers on the careers site. The link both confirms the "
                + "email address and activates the account — until it is followed the account cannot "
                + "sign in, so this is the one recruitment email that must never be switched off.",
            DefaultSubject = "Activate your {{CompanyName}} careers account",
            DefaultHtmlBody = Shell(BlueGradient, "Confirm your email address",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>
    Thank you for creating a careers account with <strong>{{CompanyName}}</strong>. Confirm your
    email address to activate it — you will not be able to sign in or apply for a role until you do.
  </p>" +
                PrimaryButton("{{ActivationLink}}", "Activate my account") + @"
  <p style='color:#6b7280;font-size:0.875rem;margin-top:1.5rem'>
    This link expires in {{ExpiryHours}} hours and can be used once. If it has expired, request a
    new one from the sign-in page.
  </p>
  <div style='background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:0.875rem;margin-top:1rem'>
    <div style='color:#6b7280;font-size:0.8rem;margin-bottom:0.35rem'>
      If the button does not work, paste this address into your browser:
    </div>
    <div style='font-family:monospace;font-size:0.75rem;word-break:break-all;color:#374151'>{{ActivationLink}}</div>
  </div>
  <p style='color:#9ca3af;font-size:0.8rem;margin-top:2rem'>
    If you did not create this account, no action is needed — it stays inactive and the link expires
    on its own. {{#if SupportEmail}}Questions? Write to {{SupportEmail}}.{{/if}}
  </p>"),
            Tokens = new()
            {
                T("CandidateName", "The name the candidate registered with.", "Ada Boahen"),
                T("CompanyName", "The employer's name, from the company profile.", "Tema Development Corporation"),
                // ⚠ A whole URL, not a portal base plus a path. The token is single-use and bound to
                // one account, so the link cannot be reconstructed from parts in the template.
                T("ActivationLink", "The complete, single-use activation URL. Do not split it.",
                  "https://careers.example.com/careers/verify-email?uid=…&token=…"),
                T("ExpiryHours", "How long the link remains valid.", "24"),
                T("SupportEmail", "Where to write for help. Omitted when none is configured.",
                  "recruitment@example.com"),
            }
        });

        // ── 1. Application received ────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.ApplicationReceived,
            Name = "Application Received",
            Category = "Application",
            Description = "Sent to a candidate immediately after their application is submitted.",
            DefaultSubject = "Application received: {{JobTitle}} ({{ApplicationNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "Application Received",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>Thank you for applying! We have successfully received your application for:</p>
  <div style='background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:1rem;margin:1rem 0'>
    <div style='font-size:1.125rem;font-weight:700;color:#111827'>{{JobTitle}}</div>
    <div style='color:#6b7280;font-size:0.875rem;margin-top:0.25rem'>Vacancy: {{VacancyNumber}}</div>
    <div style='margin-top:0.75rem;display:flex;gap:1.5rem'>
      <span><strong>Ref:</strong> {{ApplicationNumber}}</span>
      <span><strong>Submitted:</strong> {{SubmittedAt}}</span>
    </div>
  </div>
  <p>You can track your application status at any time using your application reference number.</p>" +
                PrimaryButton("{{PortalUrl}}/careers/portal/dashboard", "Track my application") + @"
  <p style='color:#9ca3af;font-size:0.8rem;margin-top:2rem'>
    Our team will review your application and be in touch if your profile matches our requirements.
  </p>"),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("JobTitle", "Title of the role applied for.", "Senior Accountant"),
                T("VacancyNumber", "Reference number of the vacancy.", "VAC-000123"),
                T("ApplicationNumber", "Reference number of this application.", "APP-000456"),
                T("SubmittedAt", "Date/time the application was submitted.", "15 Jul 2026 14:32 UTC"),
                PortalUrlToken,
            }
        });

        // ── 2. Application withdrawn ───────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.ApplicationWithdrawn,
            Name = "Application Withdrawn",
            Category = "Application",
            Description = "Confirms to a candidate that their application has been withdrawn.",
            DefaultSubject = "Application withdrawn: {{JobTitle}} ({{ApplicationNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "Application Withdrawn",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>Your application for <strong>{{JobTitle}}</strong> (ref: <strong>{{ApplicationNumber}}</strong>) has been successfully withdrawn.</p>
  <p>We appreciate your interest in our organisation. You are welcome to apply for other open positions at any time.</p>" +
                PrimaryButton("{{PortalUrl}}/careers", "Browse open roles")),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("JobTitle", "Title of the role.", "Senior Accountant"),
                T("ApplicationNumber", "Reference number of this application.", "APP-000456"),
                PortalUrlToken,
            }
        });

        // ── 3. Application under review ────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.ApplicationUnderReview,
            Name = "Application Under Review",
            Category = "Application",
            Description = "Notifies a candidate that their application is now being actively reviewed.",
            DefaultSubject = "Your application is under review — {{JobTitle}} ({{ApplicationNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "Your application is under review",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>Good news — your application for <strong>{{JobTitle}}</strong> (ref: <strong>{{ApplicationNumber}}</strong>) is now being actively reviewed by our recruitment team.</p>
  <p>We will be in touch with an update once our review is complete. No action is required from you at this stage.</p>" +
                PrimaryButton("{{PortalUrl}}/careers/portal/dashboard", "Track my application") + @"
  <p style='color:#9ca3af;font-size:0.8rem;margin-top:2rem'>Thank you for your patience.</p>"),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("JobTitle", "Title of the role.", "Senior Accountant"),
                T("ApplicationNumber", "Reference number of this application.", "APP-000456"),
                PortalUrlToken,
            }
        });

        // ── 4. Application shortlisted ─────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.ApplicationShortlisted,
            Name = "Application Shortlisted",
            Category = "Application",
            Description = "Congratulates a candidate on being shortlisted.",
            DefaultSubject = "Congratulations! You've been shortlisted — {{JobTitle}} ({{ApplicationNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "&#127881; You've been shortlisted!",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>Exciting news! After reviewing your application for <strong>{{JobTitle}}</strong> (ref: <strong>{{ApplicationNumber}}</strong>), we are pleased to let you know that you have been shortlisted.</p>
  <p>Our team will contact you shortly with the next steps in the recruitment process. Please ensure your contact details are up to date.</p>
  <div style='background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:1rem;margin:1rem 0'>
    <p style='color:#166534;margin:0;font-weight:600'>What happens next?</p>
    <p style='color:#166534;margin:0.5rem 0 0'>You may be invited for an interview or an assessment. Keep an eye on your inbox and phone for communications from our team.</p>
  </div>" +
                PrimaryButton("{{PortalUrl}}/careers/portal/dashboard", "View my application status")),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("JobTitle", "Title of the role.", "Senior Accountant"),
                T("ApplicationNumber", "Reference number of this application.", "APP-000456"),
                PortalUrlToken,
            }
        });

        // ── 5. Application rejected ────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.ApplicationRejected,
            Name = "Application Unsuccessful",
            Category = "Application",
            Description = "Informs a candidate that their application will not be progressing. The reason block only appears when a reason is supplied.",
            DefaultSubject = "Update on your application — {{JobTitle}} ({{ApplicationNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "Update on your application",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>Thank you for your interest in the <strong>{{JobTitle}}</strong> position (ref: <strong>{{ApplicationNumber}}</strong>) and for taking the time to apply.</p>
  <p>After careful consideration, we regret to inform you that we will not be progressing with your application at this time.</p>
  {{#if RejectionReason}}<div style='background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:1rem;margin:1rem 0'><p style='margin:0;color:#6b7280;font-style:italic'>{{RejectionReason}}</p></div>{{/if}}
  <p>We appreciate your effort and encourage you to apply for future opportunities that match your profile. We wish you all the best in your career search.</p>" +
                PrimaryButton("{{PortalUrl}}/careers", "Browse open roles") + @"
  <p style='color:#9ca3af;font-size:0.8rem;margin-top:2rem'>This email was sent regarding your application {{ApplicationNumber}}.</p>"),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("JobTitle", "Title of the role.", "Senior Accountant"),
                T("ApplicationNumber", "Reference number of this application.", "APP-000456"),
                T("RejectionReason", "Optional reason shown in a callout box when present.", "The role was filled by an internal candidate."),
                PortalUrlToken,
            }
        });

        // ── 6. Assessment pending ──────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.AssessmentPending,
            Name = "Assessment Invitation",
            Category = "Assessment",
            Description = "Invites a candidate to complete an assessment stage.",
            DefaultSubject = "Action required: Assessment for {{JobTitle}} ({{ApplicationNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "Assessment invitation",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>Congratulations on progressing to the next stage of the recruitment process for <strong>{{JobTitle}}</strong> (ref: <strong>{{ApplicationNumber}}</strong>).</p>
  <p>You have been selected to complete the <strong>{{StageName}}</strong> stage. Please log in to your candidate portal to view the details and instructions.</p>
  <div style='background:#fffbeb;border:1px solid #fde68a;border-radius:8px;padding:1rem;margin:1rem 0'>
    <p style='color:#92400e;margin:0;font-weight:600'>&#9888; Action required</p>
    <p style='color:#92400e;margin:0.5rem 0 0'>Please check your portal for assessment instructions and any deadlines that may apply.</p>
  </div>" +
                PrimaryButton("{{PortalUrl}}/careers/portal/dashboard", "View assessment details")),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("JobTitle", "Title of the role.", "Senior Accountant"),
                T("ApplicationNumber", "Reference number of this application.", "APP-000456"),
                T("StageName", "Name of the assessment stage.", "Technical Assessment"),
                PortalUrlToken,
            }
        });

        // ── 7 & 8. Interview invitation / rescheduled (shared details table) ────
        const string interviewDetailsTable = @"
  <table style='width:100%;border-collapse:collapse;margin:1rem 0'>
    <tr><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600;width:40%'>Date</td><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{{Date}}</td></tr>
    <tr><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Your time slot</td><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{{TimeSlot}}</td></tr>
    {{#if SessionWindow}}<tr><td style='padding:0.5rem;background:#f0f9ff;border:1px solid #e5e7eb;font-weight:600;font-size:0.85rem'>Session window</td><td style='padding:0.5rem;background:#f0f9ff;border:1px solid #e5e7eb;font-size:0.85rem;color:#6b7280'>{{SessionWindow}}</td></tr>{{/if}}
    <tr><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Format</td><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{{Format}}</td></tr>
    <tr><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Location / Link</td><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{{Location}}</td></tr>
  </table>
  <p style='margin-top:1.5rem'>
    <a href='{{PortalUrl}}/careers/portal/confirm-interview/{{ConfirmToken}}'
       style='background:#1a56db;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Confirm attendance
    </a>
  </p>
  <p style='color:#6b7280;font-size:0.85rem;margin-top:0.5rem'>
    If the button does not work, copy this link into your browser:<br/>
    <a href='{{PortalUrl}}/careers/portal/confirm-interview/{{ConfirmToken}}' style='color:#1a56db;word-break:break-all'>{{PortalUrl}}/careers/portal/confirm-interview/{{ConfirmToken}}</a>
  </p>
  <p style='color:#9ca3af;font-size:0.8rem;margin-top:2rem'>If you have any questions, please contact our recruitment team.</p>";

        var interviewTokens = new List<EmailTokenDescriptor>
        {
            T("CandidateName", "Candidate's full name.", "Ada Boahen"),
            T("JobTitle", "Title of the role.", "Senior Accountant"),
            T("InterviewNumber", "Reference number of the interview.", "INT-000078"),
            T("Date", "Interview date.", "Monday, 20 July 2026"),
            T("TimeSlot", "The candidate's time slot.", "09:00 AM – 09:45 AM"),
            T("SessionWindow", "Overall session window (shown only when a personal slot is set).", "09:00 AM – 12:00 PM"),
            T("Format", "Interview format and round.", "Panel Interview (Round 1)"),
            T("Location", "Location or meeting link.", "Boardroom 2, Head Office"),
            T("ConfirmToken", "Single-use token appended to the confirm-attendance link.", "abc123"),
            PortalUrlToken,
        };

        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.InterviewInvitation,
            Name = "Interview Invitation",
            Category = "Interview",
            Description = "Invites a candidate to an interview.",
            DefaultSubject = "Interview invitation — {{JobTitle}} ({{InterviewNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "You have been invited to an interview",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>We are pleased to invite you to an interview for the <strong>{{JobTitle}}</strong> position. Please review the details below.</p>" +
                interviewDetailsTable),
            Tokens = interviewTokens.ToList()
        });

        var rescheduleTokens = interviewTokens.ToList();
        rescheduleTokens.Insert(rescheduleTokens.Count - 1,
            T("RescheduleReason", "Optional reason shown in a callout when the interview is rescheduled.", "Panel availability changed."));

        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.InterviewRescheduled,
            Name = "Interview Rescheduled",
            Category = "Interview",
            Description = "Notifies a candidate that their interview has been rescheduled.",
            DefaultSubject = "Interview rescheduled — {{JobTitle}} ({{InterviewNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "Your interview has been rescheduled",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>We are writing to let you know that your interview for <strong>{{JobTitle}}</strong> has been rescheduled. Please note the updated details below.</p>
  {{#if RescheduleReason}}<div style='background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;padding:1rem;margin:1rem 0'><p style='color:#9a3412;margin:0;font-weight:600'>Reason for reschedule</p><p style='color:#9a3412;margin:0.5rem 0 0'>{{RescheduleReason}}</p></div>{{/if}}" +
                interviewDetailsTable),
            Tokens = rescheduleTokens
        });

        // ── 9. Interview panelist assignment ───────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.InterviewPanelistAssignment,
            Name = "Interview Panel Assignment",
            Category = "Interview",
            Description = "Notifies an interview panelist of their assignment. The instructions block and confirm button appear only when supplied.",
            DefaultSubject = "Interview panel assignment — {{JobTitle}} ({{InterviewNumber}})",
            DefaultHtmlBody = Shell(GreenGradient, "Interview panel assignment",
                @"  <p>Hi <strong>{{PanelistName}}</strong>,</p>
  <p>You have been assigned as a panelist for an upcoming interview for the <strong>{{JobTitle}}</strong> position. Please review the details below.</p>
  <table style='width:100%;border-collapse:collapse;margin:1rem 0'>
    <tr><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600;width:40%'>Interview</td><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{{InterviewNumber}}</td></tr>
    <tr><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Date</td><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{{Date}}</td></tr>
    <tr><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Time</td><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{{Time}}</td></tr>
    <tr><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Format</td><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{{Format}}</td></tr>
    <tr><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Location / Link</td><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{{Location}}</td></tr>
  </table>
  {{#if Instructions}}<div style='background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:1rem;margin:1.25rem 0'><p style='color:#166534;margin:0 0 0.4rem;font-weight:600;font-size:0.9rem'>&#x1F4CB; Notes &amp; Instructions</p><p style='color:#166534;margin:0;white-space:pre-wrap;font-size:0.9rem'>{{Instructions}}</p></div>{{/if}}
  {{#if ConfirmToken}}<p style='margin-top:1.5rem'>
    <a href='{{PortalUrl}}/interviews/confirm-panelist/{{ConfirmToken}}'
       style='background:#059669;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      Confirm my assignment
    </a>
  </p>
  <p style='color:#6b7280;font-size:0.85rem;margin-top:0.5rem'>
    If the button does not work, copy this link into your browser:<br/>
    <a href='{{PortalUrl}}/interviews/confirm-panelist/{{ConfirmToken}}' style='color:#059669;word-break:break-all'>{{PortalUrl}}/interviews/confirm-panelist/{{ConfirmToken}}</a>
  </p>{{/if}}
  <p style='color:#9ca3af;font-size:0.8rem;margin-top:2rem'>If you have any questions, please contact the HR team.</p>"),
            Tokens = new()
            {
                T("PanelistName", "Panelist's full name.", "Kwame Mensah"),
                T("JobTitle", "Title of the role.", "Senior Accountant"),
                T("InterviewNumber", "Reference number of the interview.", "INT-000078"),
                T("Date", "Interview date.", "Monday, 20 July 2026"),
                T("Time", "Interview time range.", "09:00 AM – 12:00 PM"),
                T("Format", "Interview format and round.", "Panel Interview (Round 1)"),
                T("Location", "Location or meeting link.", "Boardroom 2, Head Office"),
                T("Instructions", "Optional notes/instructions block shown when present.", "Please prepare competency-based questions."),
                T("ConfirmToken", "Optional single-use token; when present a confirm button is shown.", "xyz789"),
                PortalUrlToken,
            }
        });

        // ── 10. Offer issued ───────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.OfferIssued,
            Name = "Offer Issued",
            Category = "Offer",
            Description = "Notifies a candidate that a job offer has been extended. Salary, start-date and expiry rows appear only when set.",
            DefaultSubject = "Job offer — {{PositionTitle}} ({{OfferNumber}})",
            DefaultHtmlBody = Shell(BlueGradient, "&#127881; You have received a job offer!",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>Congratulations! We are delighted to extend to you an offer of employment for the position of <strong>{{PositionTitle}}</strong> (ref: <strong>{{OfferNumber}}</strong>).</p>
  <p>Please log in to the candidate portal to review the full offer details and submit your response before the expiry date.</p>
  <table style='width:100%;border-collapse:collapse;margin:1rem 0'>
    <tr><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Position</td><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{{PositionTitle}}</td></tr>
    {{#if SalaryLine}}<tr><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Offered salary</td><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{{SalaryLine}}</td></tr>{{/if}}
    {{#if StartDate}}<tr><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb;font-weight:600'>Proposed start date</td><td style='padding:0.5rem;background:#fff;border:1px solid #e5e7eb'>{{StartDate}}</td></tr>{{/if}}
    {{#if ExpiryDate}}<tr><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb;font-weight:600'>Offer expires</td><td style='padding:0.5rem;background:#f9fafb;border:1px solid #e5e7eb'>{{ExpiryDate}}</td></tr>{{/if}}
  </table>
  <p>Click the button below to sign in to your candidate portal account and view the offer.</p>
  <p style='margin-top:1.5rem'>
    <a href='{{RespondUrl}}'
       style='background:#1a56db;color:#fff;padding:0.75rem 1.5rem;border-radius:6px;text-decoration:none;font-weight:600'>
      View offer in portal
    </a>
  </p>
  <p style='color:#9ca3af;font-size:0.75rem;margin-top:2rem'>Offer reference: {{OfferNumber}}. Please sign in with the email address associated with your candidate account.</p>"),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("PositionTitle", "Title of the offered position.", "Senior Accountant"),
                T("OfferNumber", "Reference number of the offer.", "OFF-000012"),
                T("SalaryLine", "Formatted salary line; row hidden when empty.", "GHS 90,000.00 per annum"),
                T("StartDate", "Proposed start date; row hidden when empty.", "Monday, 3 August 2026"),
                T("ExpiryDate", "Offer expiry date; row hidden when empty.", "Friday, 25 July 2026"),
                T("RespondUrl", "Deep link that signs the candidate in to view the offer.", "https://careers.example.com/careers/portal/login"),
                PortalUrlToken,
            }
        });

        // ── 11. Offer accepted ─────────────────────────────────────────────────
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.OfferAccepted,
            Name = "Offer Accepted",
            Category = "Offer",
            Description = "Welcomes a candidate after they accept an offer.",
            DefaultSubject = "Welcome aboard! — {{PositionTitle}}",
            DefaultHtmlBody = Shell(BlueGradient, "&#127881; Welcome aboard, {{CandidateName}}!",
                @"  <p>Hi <strong>{{CandidateName}}</strong>,</p>
  <p>We are thrilled to confirm that your offer for <strong>{{PositionTitle}}</strong> (ref: <strong>{{OfferNumber}}</strong>) has been accepted. Welcome to the team!</p>
  <div style='background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:1rem;margin:1rem 0'>
    <p style='color:#166534;margin:0;font-weight:600'>What happens next?</p>
    <p style='color:#166534;margin:0.5rem 0 0'>{{#if StartDate}}Your proposed start date is <strong>{{StartDate}}</strong>. Our HR team will be in touch with onboarding details.{{else}}Our HR team will be in touch shortly with onboarding details and your confirmed start date.{{/if}}</p>
  </div>
  <p>If you have any questions in the meantime, please don't hesitate to reach out to our HR team.</p>" +
                PrimaryButton("{{PortalUrl}}/careers/portal/dashboard", "Go to my portal")),
            Tokens = new()
            {
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("PositionTitle", "Title of the offered position.", "Senior Accountant"),
                T("OfferNumber", "Reference number of the offer.", "OFF-000012"),
                T("StartDate", "Proposed start date; when empty a generic message is shown.", "Monday, 3 August 2026"),
                PortalUrlToken,
            }
        });

        // ── 12. Offer letter (formal document) ─────────────────────────────────
        // A full, HR-editable offer-of-employment letter. Rendered on demand (portal view,
        // print-to-PDF, and as the email body) by OfferLetterService, which supplies the rich
        // token set below — including pre-built HTML fragments ({{{SalaryBreakdownTable}}},
        // {{{BenefitsList}}}, {{{DutiesList}}}, {{{ConditionsList}}}) it assembles itself.
        list.Add(new EmailEventDescriptor
        {
            Module = Module,
            EventKey = Events.OfferLetter,
            Name = "Offer Letter (document)",
            Category = "Offer",
            Description = "The formal offer-of-employment letter. Sections appear only when the underlying "
                        + "data is present, so HR can restyle the whole document while empty fields stay hidden.",
            DefaultSubject = "Offer of Employment — {{PositionTitle}} ({{OfferNumber}})",
            DefaultHtmlBody = OfferLetterBody,
            Tokens = new()
            {
                T("CompanyName", "Employer's registered name (letterhead).", "Acme Manufacturing Ltd"),
                T("CompanyAddress", "Employer's registered address (letterhead).", "12 Independence Ave, Accra, Ghana"),
                T("CompanyLogoUrl", "Company logo URL; shown in the letterhead when set.", "https://acme.example.com/logo.png"),
                T("CompanyFooter", "Document footer line (registered office / reg. number); hidden when empty.", "Registered in Ghana No. CS123456 · 12 Independence Ave, Accra"),
                T("LetterDate", "Date the letter is issued.", "17 July 2026"),
                T("CandidateName", "Candidate's full name.", "Ada Boahen"),
                T("CandidateAddress", "Candidate's postal address; block hidden when empty.", "P.O. Box 45, East Legon, Accra"),
                T("OfferNumber", "Reference number of the offer.", "OFR-000012"),
                T("PositionTitle", "Title of the offered position.", "Senior Accountant"),
                T("DepartmentName", "Department / organisation unit.", "Finance"),
                T("GradeTitle", "Salary grade.", "Grade M2"),
                T("StaffLevel", "Seniority classification; row hidden when empty.", "Senior Staff"),
                T("ReportsToTitle", "Reporting line; row hidden when empty.", "Finance Manager"),
                T("EmploymentType", "Engagement type.", "Permanent"),
                T("WorkMode", "On-site / hybrid / remote.", "On-site"),
                T("LocationName", "Duty location; row hidden when empty.", "Head Office, Accra"),
                T("StartDate", "Proposed start date; row hidden when empty.", "Monday, 3 August 2026"),
                T("ProbationText", "Probation description; row hidden when empty.", "6 months"),
                T("NoticeText", "Notice period the employee must give; row hidden when empty.", "1 month"),
                T("WeeklyHours", "Contracted weekly hours; row hidden when empty.", "40"),
                T("AnnualLeaveDays", "Annual leave entitlement in days; row hidden when empty.", "20"),
                T("IsBargainingUnit", "Truthy when the role is covered by a collective agreement (shows the union clause).", "true"),
                T("UnionName", "Union / bargaining unit name; shown inside the union clause.", "Industrial & Commercial Workers' Union"),
                T("JobSummary", "Role summary paragraph; block hidden when empty.", "Lead the financial reporting function…"),
                T("DutiesList", "Pre-built HTML <ul> of key duties; block hidden when empty.", "<ul><li>Prepare monthly accounts</li></ul>"),
                T("EssentialFunctions", "Essential-functions note; block hidden when empty.", "Must be able to meet statutory reporting deadlines."),
                T("SalaryBreakdownTable", "Pre-built HTML table itemising basic + allowances + gross.", "<table>…</table>"),
                T("BaseSalaryLine", "Formatted basic salary line.", "GHS 90,000.00 per annum"),
                T("GrossSalaryLine", "Formatted gross (basic + allowances) line; hidden when empty.", "GHS 108,000.00 per annum"),
                T("BonusTerms", "Bonus terms; block hidden when empty.", "Discretionary annual bonus up to 10% of basic."),
                T("CommissionStructure", "Commission structure; block hidden when empty.", "2% of net sales."),
                T("BenefitsList", "Pre-built HTML <ul> of benefits; block hidden when empty.", "<ul><li>Medical cover</li></ul>"),
                T("NdaRequired", "Truthy when a non-disclosure agreement is required (shows the NDA clause).", "true"),
                T("IsConditional", "Truthy when the offer is conditional (shows conditions-precedent block).", "true"),
                T("ConditionsList", "Pre-built HTML <ul> of pre-employment conditions; shown when conditional.", "<ul><li>Satisfactory references</li></ul>"),
                // Round 4, lane H3. The same list, offered to EVERY letter rather than only a
                // conditional one — a permanent appointment still asks for references and a medical.
                T("HasPreEmploymentChecks", "Truthy when the offer carries a pre-employment check set.", "true"),
                T("PreEmploymentChecklist", "⚠ Raw HTML — the checks the candidate must produce, with instructions and expected turnaround.", "<ul><li>Police clearance</li></ul>"),
                T("AdditionalTerms", "Free-text additional terms; block hidden when empty.", "Relocation assistance provided."),
                T("ExpiryDate", "Offer expiry date; acceptance clause hidden when empty.", "Friday, 25 July 2026"),
                T("AcceptanceInstructions", "How to accept the offer.", "Sign and return one copy of this letter, or accept via the candidate portal."),
                T("SignatoryName", "Name of the company signatory; block hidden when empty.", "Kwaku Owusu"),
                T("SignatoryTitle", "Title of the company signatory.", "Head of Human Resources"),
                // ⚠ Both are embedded IMAGES, not links. The seal lives in private storage with no
                // public URL, so the letter carries the bytes; the block is hidden when nothing is
                // in force, which is also what happens after a seal is withdrawn.
                T("SignatureImageUrl", "Authorised signature image, embedded; hidden when none is in force.", ""),
                T("CompanySealImageUrl", "Company seal image, embedded; hidden when none is in force.", ""),
            }
        });

        return list;
    }

    // ── Offer letter document body (formal, HR-editable) ─────────────────────────
    private const string OfferLetterBody = @"
<div style='font-family:Georgia,""Times New Roman"",serif;color:#1f2937;max-width:800px;margin:0 auto;line-height:1.55;font-size:14px'>
  <div style='border-bottom:2px solid #1e3a8a;padding-bottom:0.75rem;margin-bottom:1.5rem'>
    {{#if CompanyLogoUrl}}<img src='{{CompanyLogoUrl}}' alt='{{CompanyName}}' style='max-height:64px;margin-bottom:0.5rem' />{{/if}}
    <div style='font-size:1.4rem;font-weight:700;color:#1e3a8a'>{{CompanyName}}</div>
    {{#if CompanyAddress}}<div style='color:#6b7280;font-size:0.85rem'>{{CompanyAddress}}</div>{{/if}}
  </div>

  <p style='text-align:right;margin:0 0 1rem'>{{LetterDate}}</p>

  <p style='margin:0'><strong>{{CandidateName}}</strong></p>
  {{#if CandidateAddress}}<p style='margin:0 0 1rem;white-space:pre-wrap'>{{CandidateAddress}}</p>{{else}}<div style='margin-bottom:1rem'></div>{{/if}}

  <p>Dear {{CandidateName}},</p>

  <h2 style='font-size:1.05rem;color:#1e3a8a;margin:1rem 0 0.5rem'>RE: OFFER OF EMPLOYMENT &mdash; {{PositionTitle}}</h2>
  <p>We are pleased to offer you employment with {{CompanyName}} in the position of <strong>{{PositionTitle}}</strong>
     (offer reference {{OfferNumber}}), on the terms and conditions set out below.</p>

  <h3 style='font-size:0.95rem;color:#1e3a8a;border-bottom:1px solid #e5e7eb;padding-bottom:0.25rem;margin:1.25rem 0 0.5rem'>1. Position &amp; Terms</h3>
  <table style='width:100%;border-collapse:collapse;font-size:13px'>
    <tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600;width:40%'>Position</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{PositionTitle}}</td></tr>
    {{#if DepartmentName}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Department</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{DepartmentName}}</td></tr>{{/if}}
    {{#if GradeTitle}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Grade</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{GradeTitle}}</td></tr>{{/if}}
    {{#if StaffLevel}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Staff level</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{StaffLevel}}</td></tr>{{/if}}
    {{#if ReportsToTitle}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Reports to</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{ReportsToTitle}}</td></tr>{{/if}}
    <tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Employment type</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{EmploymentType}}</td></tr>
    <tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Work mode</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{WorkMode}}</td></tr>
    {{#if LocationName}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Location</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{LocationName}}</td></tr>{{/if}}
    {{#if StartDate}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Proposed start date</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{StartDate}}</td></tr>{{/if}}
    {{#if ProbationText}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Probation</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{ProbationText}}</td></tr>{{/if}}
    {{#if NoticeText}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Notice period</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{NoticeText}}</td></tr>{{/if}}
    {{#if WeeklyHours}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Weekly hours</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{WeeklyHours}}</td></tr>{{/if}}
    {{#if AnnualLeaveDays}}<tr><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb;background:#f9fafb;font-weight:600'>Annual leave</td><td style='padding:0.35rem 0.5rem;border:1px solid #e5e7eb'>{{AnnualLeaveDays}} working days per year</td></tr>{{/if}}
  </table>
  {{#if IsBargainingUnit}}<p style='margin:0.75rem 0 0;font-size:13px'>This position falls within a collective bargaining unit{{#if UnionName}} represented by <strong>{{UnionName}}</strong>{{/if}}. Your terms are subject to the applicable collective agreement.</p>{{/if}}

  {{#if JobSummary}}<h3 style='font-size:0.95rem;color:#1e3a8a;border-bottom:1px solid #e5e7eb;padding-bottom:0.25rem;margin:1.25rem 0 0.5rem'>2. Role Summary</h3>
  <p>{{JobSummary}}</p>{{/if}}
  {{#if DutiesList}}<p style='margin:0.25rem 0 0.25rem;font-weight:600;font-size:13px'>Key duties</p>{{{DutiesList}}}{{/if}}
  {{#if EssentialFunctions}}<p style='font-size:13px;color:#6b7280'>{{EssentialFunctions}}</p>{{/if}}

  <h3 style='font-size:0.95rem;color:#1e3a8a;border-bottom:1px solid #e5e7eb;padding-bottom:0.25rem;margin:1.25rem 0 0.5rem'>3. Remuneration</h3>
  {{{SalaryBreakdownTable}}}
  {{#if BonusTerms}}<p style='font-size:13px;margin:0.5rem 0 0'><strong>Bonus:</strong> {{BonusTerms}}</p>{{/if}}
  {{#if CommissionStructure}}<p style='font-size:13px;margin:0.25rem 0 0'><strong>Commission:</strong> {{CommissionStructure}}</p>{{/if}}

  {{#if BenefitsList}}<h3 style='font-size:0.95rem;color:#1e3a8a;border-bottom:1px solid #e5e7eb;padding-bottom:0.25rem;margin:1.25rem 0 0.5rem'>4. Benefits</h3>
  {{{BenefitsList}}}{{/if}}

  {{#if HasPreEmploymentChecks}}<h3 style='font-size:0.95rem;color:#b45309;border-bottom:1px solid #fde68a;padding-bottom:0.25rem;margin:1.25rem 0 0.5rem'>{{#if IsConditional}}Conditions Precedent{{else}}Pre-employment Requirements{{/if}}</h3>
  <p style='font-size:13px'>{{#if IsConditional}}This offer is conditional upon satisfactory completion of the following pre-employment checks:{{else}}Please arrange the following before your start date. They do not affect this offer, but your appointment cannot be finalised until they are complete:{{/if}}</p>
  {{{PreEmploymentChecklist}}}{{/if}}

  {{#if NdaRequired}}<p style='font-size:13px;margin:1rem 0 0'>Your employment is subject to your signing the company's Non-Disclosure Agreement.</p>{{/if}}
  {{#if AdditionalTerms}}<h3 style='font-size:0.95rem;color:#1e3a8a;border-bottom:1px solid #e5e7eb;padding-bottom:0.25rem;margin:1.25rem 0 0.5rem'>Additional Terms</h3>
  <p style='white-space:pre-wrap;font-size:13px'>{{AdditionalTerms}}</p>{{/if}}

  <h3 style='font-size:0.95rem;color:#1e3a8a;border-bottom:1px solid #e5e7eb;padding-bottom:0.25rem;margin:1.25rem 0 0.5rem'>Acceptance</h3>
  <p style='font-size:13px'>{{AcceptanceInstructions}}{{#if ExpiryDate}} This offer remains open for acceptance until <strong>{{ExpiryDate}}</strong>.{{/if}}</p>

  <p style='margin-top:1.5rem'>Yours sincerely,</p>
  <div style='margin-top:1rem'>
    {{#if SignatureImageUrl}}<img src='{{SignatureImageUrl}}' alt='' style='height:56px;display:block;margin-bottom:0.25rem' />{{/if}}
    {{#if SignatoryName}}<div style='font-weight:700'>{{SignatoryName}}</div>{{/if}}
    <div style='color:#6b7280;font-size:13px'>{{SignatoryTitle}}</div>
    <div style='color:#6b7280;font-size:13px'>{{CompanyName}}</div>
    {{#if CompanySealImageUrl}}<img src='{{CompanySealImageUrl}}' alt='' style='height:84px;display:block;margin-top:0.75rem' />{{/if}}
  </div>

  <div style='margin-top:2.5rem;border-top:1px dashed #9ca3af;padding-top:1rem;font-size:13px'>
    <p style='font-weight:600;margin:0 0 1.5rem'>Acceptance by candidate</p>
    <p style='margin:0'>Signature: __________________________&nbsp;&nbsp;&nbsp;Date: ______________</p>
    <p style='margin:1rem 0 0'>Name: {{CandidateName}}</p>
  </div>

  {{#if CompanyFooter}}<div style='margin-top:2rem;border-top:1px solid #e5e7eb;padding-top:0.75rem;color:#9ca3af;font-size:11px;text-align:center'>{{CompanyFooter}}</div>{{/if}}
</div>";
}
