using System.Globalization;
using System.Text.RegularExpressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Who hears what about a trip (travel final closure, lane 8, slice 8a — D-4, D-45, D-46): one topic per event and
/// audience, <c>StaffTravel.{Event}.{Traveller|Desk}</c>, on leave's recipient rules.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> Every travel notice went to the HR role, in the app — every submission, approval, rejection,
/// cancellation and completion — and nothing at all reached the traveller but an alert's email and the workflow engine's
/// own notices to whoever pressed Submit, whose link opens the desk's page the traveller cannot see (U1).</para>
///
/// <para><b>The traveller</b> is told in the app and by email when they have a login (<c>UserFromEmployeeIdData</c>); by
/// email alone at their employee address when they have none (<c>EmailFromData</c>, the data key set only then, so a
/// traveller with a login is not emailed twice); and when they have neither, the desk is told to tell them
/// (<c>TravellerNotReachable</c>) — a message to nobody is the defect this lane closes. Nobody is told of their own
/// act.</para>
///
/// <para><b>The desk</b> is the HR role's active holders, resolved here so that whoever did the thing is left out
/// (<c>UsersFromData</c>: the publisher can neither exclude nor dedupe). In the app only. D-46: the desk hears what it
/// must act on — some events only when someone outside the desk did them.</para>
///
/// <para><b>Never fails the act.</b> Every caller has already committed; a notice that cannot be sent is logged, not
/// thrown, and the publisher saves its rows on the caller's unit of work (U2) — so callers publish only after their own
/// save. Tenant-explicit throughout: the nightly sweep (8b, 8c) uses it with nobody signed in.</para>
///
/// <para><b>D-45.</b> The engine's three notices to the submitter and the five old HR lifecycle topics are switched off
/// here, each time the topics are ensured. ⚠ The engine's topic seeders (<c>seed-db</c>, and the Notification Topics
/// screen's "seed workflow topics") switch the three back on; the next travel notice or sweep switches them off again.</para>
///
/// <para>The words carry the request number, route and dates — never the purpose, and never a reason or a note's
/// text: a notice travels further than the record. The link opens the page where the reader acts.</para>
/// </remarks>
public sealed class StaffTravelNotices
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<StaffTravelNotices> _logger;

    // Per scope: a request publishes several notices, and each would otherwise re-check the topics and the desk.
    private readonly HashSet<Guid> _topicsEnsured = new();
    private readonly Dictionary<Guid, List<Guid>> _deskByTenant = new();

    public StaffTravelNotices(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        UserManager<ApplicationUser> userManager,
        ILogger<StaffTravelNotices> logger)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _userManager = userManager;
        _logger = logger;
    }

    public const string TopicEntityType = "StaffTravel";
    public const string ToTraveller = "Traveller";
    public const string ToDesk = "Desk";

    // ---- the events (the middle of the topic key) ----------------------------
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Returned = "Returned";
    public const string ChangeRequested = "ChangeRequested";
    public const string Cancelled = "Cancelled";
    public const string AdvanceRequested = "AdvanceRequested";
    public const string AdvanceApproved = "AdvanceApproved";
    public const string AdvanceRejected = "AdvanceRejected";
    public const string AdvanceDisbursed = "AdvanceDisbursed";
    public const string ClaimSubmitted = "ClaimSubmitted";
    public const string ClaimReturned = "ClaimReturned";
    public const string ClaimApproved = "ClaimApproved";
    public const string ClaimRejected = "ClaimRejected";
    public const string ClaimPaid = "ClaimPaid";
    public const string AlertIssued = "AlertIssued";
    public const string BriefingToAcknowledge = "BriefingToAcknowledge";
    public const string NoteShared = "NoteShared";
    public const string TravellerMessage = "TravellerMessage";
    public const string TravellerFile = "TravellerFile";
    public const string TravellerNotReachable = "TravellerNotReachable";

    // ---- links ----------------------------------------------------------------
    // The portal's trip page and the desk's open on a tab named in the query (8a gave both pages `?tab=`).

    public static string TravellerTrip(Guid tripId, string? tab = null)
        => tab is null ? $"/me/travel/{tripId}" : $"/me/travel/{tripId}?tab={tab}";

    public static string TravellerClaim(Guid claimId) => $"/me/travel/claims/{claimId}";

    public static string DeskTrip(Guid tripId, string? tab = null)
        => tab is null ? $"/hr/travel/{tripId}" : $"/hr/travel/{tripId}?tab={tab}";

    public static string DeskClaim(Guid claimId) => $"/hr/travel/claims/{claimId}";

    // ---- formatting -------------------------------------------------------------

    public static string Money(string? currency, decimal amount)
        => $"{currency} {amount.ToString("N2", Invariant)}".Trim();

    public static string Date(DateOnly date) => date.ToString("d MMM yyyy", Invariant);

    // ---- telling people -----------------------------------------------------------

    /// <summary>
    /// Tells the trip's traveller — unless they did it themselves (<paramref name="actorEmployeeId"/>).
    /// </summary>
    /// <param name="data">The event's own tokens (a number, an amount, an outcome), added to the trip's.</param>
    public async Task TellTravellerAsync(
        StaffTravelRequest trip, string evt, string actionPath, Guid? actorUserId, Guid? actorEmployeeId,
        IReadOnlyDictionary<string, object>? data = null, CancellationToken cancellationToken = default)
    {
        if (actorEmployeeId is Guid actor && actor == trip.EmployeeId) return;

        try
        {
            await EnsureTopicsAsync(trip.TenantId, cancellationToken);
            var traveller = await TravellerAsync(trip, cancellationToken);
            var tokens = TripTokens(trip, traveller.Name, actionPath, data);
            tokens["EmployeeId"] = trip.EmployeeId;

            if (!traveller.HasLogin && string.IsNullOrWhiteSpace(traveller.Email))
            {
                // Nobody can be told directly: the desk is, with what to tell them.
                var seed = Seeds.First(s => s.Event == evt && s.Audience == ToTraveller);
                tokens["What"] = Render(seed.NotReachableWhat ?? seed.Title, tokens);
                tokens["ActionPath"] = DeskTrip(trip.Id);
                var desk = await DeskUsersAsync(trip.TenantId, cancellationToken);
                if (desk.Count == 0)
                {
                    _logger.LogWarning("Travel notice {Event} for {Reference} reached nobody: the traveller has no login or email, and no HR role holder is active",
                        evt, trip.RequestNumber);
                    return;
                }
                tokens["DeskUserIds"] = desk;
                await PublishAsync(trip, TravellerNotReachable, ToDesk, actorUserId, tokens, cancellationToken);
                return;
            }

            if (!traveller.HasLogin)
                tokens["TravellerEmail"] = traveller.Email!;
            await PublishAsync(trip, evt, ToTraveller, actorUserId, tokens, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Travel notice {Event} to the traveller of {Reference} could not be sent", evt, trip.RequestNumber);
        }
    }

    /// <summary>
    /// Tells the travel desk — the HR role's active holders but whoever did it. With
    /// <paramref name="onlyWhenActorOutsideDesk"/>, nobody is told when a desk officer did it (D-46: a trip cancelled or
    /// sent back by the traveller or their approver, not by the desk itself).
    /// </summary>
    public async Task TellDeskAsync(
        StaffTravelRequest trip, string evt, string actionPath, Guid? actorUserId, Guid? actorEmployeeId,
        IReadOnlyDictionary<string, object>? data = null, bool onlyWhenActorOutsideDesk = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var desk = await DeskUsersAsync(trip.TenantId, cancellationToken);
            var actor = actorUserId is Guid a && a != Guid.Empty ? a : (Guid?)null;
            if (onlyWhenActorOutsideDesk && actor is Guid inside && desk.Contains(inside)) return;
            var to = desk.Where(u => u != actor).ToList();
            if (to.Count == 0) return;

            await EnsureTopicsAsync(trip.TenantId, cancellationToken);
            var traveller = await TravellerAsync(trip, cancellationToken);
            var tokens = TripTokens(trip, traveller.Name, actionPath, data);
            tokens["DeskUserIds"] = to;
            tokens["By"] = await ActorNameAsync(trip, actorEmployeeId, cancellationToken);
            await PublishAsync(trip, evt, ToDesk, actorUserId, tokens, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Travel notice {Event} to the desk about {Reference} could not be sent", evt, trip.RequestNumber);
        }
    }

    // ---- the pieces ------------------------------------------------------------------

    private sealed record TravellerFacts(string Name, string? Email, bool HasLogin);

    private async Task<TravellerFacts> TravellerAsync(StaffTravelRequest trip, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.Id == trip.EmployeeId && e.TenantId == trip.TenantId)
            .Select(e => new { e.FirstName, e.LastName, e.EmailAddress })
            .FirstOrDefaultAsync(cancellationToken);
        var hasLogin = await _userManager.Users
            .AnyAsync(u => u.TenantId == trip.TenantId && u.IsActive && u.EmployeeId == trip.EmployeeId, cancellationToken);
        var name = employee is null ? "The traveller" : $"{employee.FirstName} {employee.LastName}".Trim();
        return new TravellerFacts(name, employee?.EmailAddress, hasLogin);
    }

    /// <summary>Who did it, as the desk reads it.</summary>
    private async Task<string> ActorNameAsync(StaffTravelRequest trip, Guid? actorEmployeeId, CancellationToken cancellationToken)
    {
        if (actorEmployeeId is not Guid actor) return "Someone";
        if (actor == trip.EmployeeId) return "The traveller";
        var name = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.Id == actor && e.TenantId == trip.TenantId)
            .Select(e => (e.FirstName + " " + e.LastName).Trim())
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? "Someone" : name;
    }

    private async Task<List<Guid>> DeskUsersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_deskByTenant.TryGetValue(tenantId, out var cached)) return cached;
        var holders = await _userManager.GetUsersInRoleAsync(Constants.Roles.Hr);
        var ids = holders.Where(u => u.IsActive && u.TenantId == tenantId).Select(u => u.Id).Distinct().ToList();
        _deskByTenant[tenantId] = ids;
        return ids;
    }

    private static Dictionary<string, object> TripTokens(
        StaffTravelRequest trip, string travellerName, string actionPath, IReadOnlyDictionary<string, object>? data)
    {
        var tokens = new Dictionary<string, object>
        {
            ["Reference"] = trip.RequestNumber ?? string.Empty,
            ["Traveller"] = travellerName,
            ["Route"] = $"{trip.OriginCity} to {trip.DestinationCity}",
            ["Dates"] = $"{Date(trip.TravelStartDate)} to {Date(trip.TravelEndDate)}",
            ["ActionPath"] = actionPath,
        };
        if (data is not null)
            foreach (var (key, value) in data)
                tokens[key] = value;
        return tokens;
    }

    private Task PublishAsync(
        StaffTravelRequest trip, string evt, string audience, Guid? actorUserId,
        Dictionary<string, object> data, CancellationToken cancellationToken)
        // The trip's id is every notice's entity (U9): the suites' teardowns find a trip's notices by it, whatever the
        // notice is about; the link carries the claim or advance.
        => _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = trip.TenantId,
            EntityType = TopicEntityType,
            Activity = evt,
            Audience = audience,
            EntityId = trip.Id,
            TriggeredByUserId = actorUserId is Guid a && a != Guid.Empty ? a : null,
            Data = data,
        }, cancellationToken);

    private static string Render(string template, Dictionary<string, object> tokens)
        => TokenRegex.Replace(template, m => tokens.TryGetValue(m.Groups[1].Value, out var v) && v is not null
            ? v.ToString() ?? string.Empty
            : string.Empty);

    // ---- the topics ------------------------------------------------------------------

    private sealed record TopicSeed(
        string Event, string Audience, string Name, string Description, string Title, string Body,
        string? NotReachableWhat = null);

    /// <remarks>
    /// Admins can change any topic's words and recipients on the Notification Topics screen; these are the defaults a
    /// tenant gets the first time a travel notice is sent.
    /// </remarks>
    private static readonly TopicSeed[] Seeds =
    {
        // ---- the traveller: in the app and by email ----
        new(Submitted, ToTraveller, "Travel: your request was submitted (traveller)",
            "Sent to the traveller when someone else — the travel desk — submits a trip for them. Whoever submits their own trip is not told what they have just done.",
            "Your travel request {{Reference}} was sent for approval",
            "{{Reference}} — {{Route}}, {{Dates}} — was submitted for approval for you. You can follow it under My travel.",
            "Travel request {{Reference}} was submitted for approval for them"),
        new(Approved, ToTraveller, "Travel: your trip is approved (traveller)",
            "Sent to the traveller when their trip is approved — after its last approval stage.",
            "Your trip {{Reference}} is approved",
            "{{Reference}} — {{Route}}, {{Dates}} — has been approved. The travel desk books it from here; what they arrange appears under My travel.",
            "Their trip {{Reference}} is approved"),
        new(Rejected, ToTraveller, "Travel: your request was not approved (traveller)",
            "Sent to the traveller when their trip is rejected.",
            "Your travel request {{Reference}} was not approved",
            "{{Reference}} — {{Route}}, {{Dates}} — was rejected. Open it under My travel to see why.",
            "Their travel request {{Reference}} was rejected"),
        new(Returned, ToTraveller, "Travel: your request needs changes (traveller)",
            "Sent to the traveller when an approver returns their request for revision.",
            "Your travel request {{Reference}} needs changes",
            "{{Reference}} — {{Route}}, {{Dates}} — was returned to you to change before it goes for approval again. Open it under My travel to see what is asked.",
            "Their travel request {{Reference}} was returned for changes"),
        new(ChangeRequested, ToTraveller, "Travel: your approved trip was sent back for a change (traveller)",
            "Sent to the traveller when the travel desk or their approver sends their approved trip back for a change. A traveller who asks for the change is not told.",
            "Your trip {{Reference}} was sent back for a change",
            "{{Reference}} — {{Route}}, {{Dates}} — needs changing and goes for approval again; its bookings, advances and claims stay with it. Open it under My travel to see why.",
            "Their approved trip {{Reference}} was sent back for a change"),
        new(Cancelled, ToTraveller, "Travel: your trip was cancelled (traveller)",
            "Sent to the traveller when someone else cancels their trip. A traveller who withdraws their own is not told.",
            "Your trip {{Reference}} was cancelled",
            "{{Reference}} — {{Route}}, {{Dates}} — was cancelled. Open it under My travel to see why.",
            "Their trip {{Reference}} was cancelled"),
        new(AdvanceApproved, ToTraveller, "Travel: your advance is approved (traveller)",
            "Sent to the traveller when a travel advance for their trip is approved.",
            "Your travel advance {{Number}} is approved",
            "{{Amount}} was approved for {{Reference}} ({{Route}}, {{Dates}}). The travel desk pays it out next.",
            "Their travel advance {{Number}} ({{Amount}}) is approved"),
        new(AdvanceRejected, ToTraveller, "Travel: your advance was not approved (traveller)",
            "Sent to the traveller when a travel advance for their trip is rejected.",
            "Your travel advance {{Number}} was not approved",
            "The advance of {{Amount}} for {{Reference}} ({{Route}}, {{Dates}}) was rejected. Ask the travel desk if you need to know why.",
            "Their travel advance {{Number}} was rejected"),
        new(AdvanceDisbursed, ToTraveller, "Travel: your advance has been paid out (traveller)",
            "Sent to the traveller when a travel advance is paid out to them, with the date it must be accounted for by.",
            "Your travel advance {{Number}} has been paid out",
            "{{Amount}} was paid out to you for {{Reference}} ({{Route}}, {{Dates}}). Account for it by {{Deadline}} — with an expense claim, or by handing back what you did not spend.",
            "Their travel advance {{Number}} ({{Amount}}) was paid out; it is to be accounted for by {{Deadline}}"),
        new(ClaimReturned, ToTraveller, "Travel: your expense claim was returned (traveller)",
            "Sent to the claimant when the travel desk returns their expense claim for changes.",
            "Your expense claim {{Number}} was returned to you",
            "Claim {{Number}} for {{Reference}} needs changes before it can be approved. Open it to read the travel desk's note, change it and send it again.",
            "Their expense claim {{Number}} was returned for changes"),
        new(ClaimApproved, ToTraveller, "Travel: your expense claim is approved (traveller)",
            "Sent to the claimant when their expense claim is approved, in full or in part.",
            "Your expense claim {{Number}} is {{Outcome}}",
            "Claim {{Number}} for {{Reference}} is {{Outcome}}: {{Amount}} approved. It is paid next; open it to see each expense's decision.",
            "Their expense claim {{Number}} is {{Outcome}} ({{Amount}})"),
        new(ClaimRejected, ToTraveller, "Travel: your expense claim was rejected (traveller)",
            "Sent to the claimant when their expense claim is rejected.",
            "Your expense claim {{Number}} was rejected",
            "Claim {{Number}} for {{Reference}} was rejected. Open it to read why.",
            "Their expense claim {{Number}} was rejected"),
        new(ClaimPaid, ToTraveller, "Travel: your expense claim has been paid (traveller)",
            "Sent to the claimant when their expense claim is paid.",
            "Your expense claim {{Number}} has been paid",
            "Claim {{Number}} for {{Reference}}: {{Settlement}}",
            "Their expense claim {{Number}} has been paid"),
        new(AlertIssued, ToTraveller, "Travel: a destination alert for your trip (traveller)",
            "Sent to the traveller of every approved or under-way trip a destination alert reaches, when it is raised or sent.",
            "{{Severity}} travel alert for your trip: {{Country}}",
            "{{AlertTitle}} — it affects {{Reference}} ({{Route}}, {{Dates}}). Read it on the trip's Before you go tab under My travel.",
            "A {{Severity}} alert for {{Country}} ({{AlertTitle}}) affects their trip {{Reference}}"),
        new(BriefingToAcknowledge, ToTraveller, "Travel: a risk briefing to acknowledge (traveller)",
            "Sent to the traveller when the travel desk records a risk assessment for their trip, or raises its level — they are asked to read and acknowledge it.",
            "Read and acknowledge the risk briefing for {{Reference}}",
            "The travel desk assessed the risk for {{Reference}} ({{Route}}, {{Dates}}) as {{RiskLevel}}. Read it on the trip's Before you go tab under My travel, and acknowledge it.",
            "A {{RiskLevel}} risk briefing for their trip {{Reference}} waits for their acknowledgement"),
        new(NoteShared, ToTraveller, "Travel: a note on your trip (traveller)",
            "Sent to the traveller when the travel desk or an approver writes a note they can see on the trip.",
            "A note on your trip {{Reference}}",
            "{{By}} wrote on {{Reference}} ({{Route}}, {{Dates}}). Read it — and reply — on the trip's Messages tab under My travel.",
            "{{By}} wrote a note to them on {{Reference}}"),

        // ---- the travel desk: in the app ----
        new(Approved, ToDesk, "Travel: approved, ready to book (desk)",
            "Sent to the HR role's holders, except whoever gave the last approval, when a trip is approved.",
            "Ready to book: {{Reference}}",
            "{{Traveller}} — {{Route}}, {{Dates}} — is approved. Book what the trip needs."),
        new(Cancelled, ToDesk, "Travel: cancelled outside the desk (desk)",
            "Sent to the HR role's holders when the traveller, or anyone outside the desk, cancels a trip.",
            "Trip cancelled: {{Reference}}",
            "{{By}} cancelled {{Traveller}}'s trip {{Reference}} ({{Route}}, {{Dates}}). Cancel what was booked for it and settle any advance."),
        new(ChangeRequested, ToDesk, "Travel: sent back for a change outside the desk (desk)",
            "Sent to the HR role's holders when the traveller or their approver sends an approved trip back for a change.",
            "Sent back for a change: {{Reference}}",
            "{{By}} asked for a change to {{Traveller}}'s approved trip {{Reference}} ({{Route}}, {{Dates}}). It goes for approval again; check what is booked and held for it."),
        new(AdvanceRequested, ToDesk, "Travel: advance to approve (desk)",
            "Sent to the HR role's holders, except whoever recorded it, when a travel advance is requested.",
            "Advance to approve: {{Number}}",
            "{{Amount}} requested for {{Traveller}}'s trip {{Reference}} ({{Route}}, {{Dates}}). Approve or reject it on the trip's Finance tab."),
        new(ClaimSubmitted, ToDesk, "Travel: expense claim to review (desk)",
            "Sent to the HR role's holders, except whoever submitted it, when an expense claim is submitted — by the traveller on the portal or by the desk.",
            "Expense claim to review: {{Number}}",
            "Claim {{Number}} ({{Amount}}) for {{Traveller}}'s trip {{Reference}} was submitted. Review it."),
        new(AlertIssued, ToDesk, "Travel: destination alert sent to a trip (desk)",
            "Sent to the HR role's holders, except whoever sent it, for each trip a destination alert reaches.",
            "{{Severity}} alert reached {{Reference}}: {{Country}}",
            "{{AlertTitle}} reached {{Traveller}}'s trip {{Reference}} ({{Route}}, {{Dates}}). Check what is booked for it."),
        new(TravellerMessage, ToDesk, "Travel: the traveller wrote (desk)",
            "Sent to the HR role's holders when a traveller writes on their own trip — a question, or a reply to a shared note.",
            "{{Traveller}} wrote on {{Reference}}",
            "A {{Kind}} from {{Traveller}} on {{Reference}} ({{Route}}, {{Dates}}). Read it on the trip's Comments tab."),
        new(TravellerFile, ToDesk, "Travel: the traveller added a file (desk)",
            "Sent to the HR role's holders when a traveller adds a file to their own trip.",
            "{{Traveller}} added a file to {{Reference}}",
            "{{FileType}}: {{FileName}}, on {{Reference}} ({{Route}}, {{Dates}}). It is on the trip's Attachments tab."),
        new(TravellerNotReachable, ToDesk, "Travel: tell the traveller (desk)",
            "Sent to the HR role's holders when a traveller has no login and no email address on file, so a notice for them reached nobody.",
            "Tell {{Traveller}}: {{What}}",
            "{{What}} — {{Reference}} ({{Route}}, {{Dates}}). {{Traveller}} has no login and no email address on file, so nobody has told them."),
    };

    /// <summary>What nothing publishes to any more (D-45, D-46, E6), switched off whenever the topics are ensured.</summary>
    private static readonly (string Key, string Why)[] RetiredTopics =
    {
        ("StaffTravelRequest.Submitted.Internal", RetiredLifecycle),
        ("StaffTravelRequest.Approved.Internal", RetiredLifecycle),
        ("StaffTravelRequest.Rejected.Internal", RetiredLifecycle),
        ("StaffTravelRequest.Cancelled.Internal", RetiredLifecycle),
        ("StaffTravelRequest.Completed.Internal", RetiredLifecycle),
        ("StaffTravelRequest.WorkflowSubmitted.Internal", RetiredEngine),
        ("StaffTravelRequest.WorkflowCompleted.Internal", RetiredEngine),
        ("StaffTravelRequest.WorkflowRejected.Internal", RetiredEngine),
        ("StaffTravelAlert.Issued.Internal",
            "Switched off by Staff Travel (final closure lane 8): an alert now reaches the traveller in the app and by email, and the desk, through StaffTravel.AlertIssued.Traveller and .Desk."),
    };

    private const string RetiredLifecycle =
        "Switched off by Staff Travel (final closure lane 8, D-46): the desk hears what it must act on through StaffTravel.*.Desk, and the traveller through StaffTravel.*.Traveller.";

    private const string RetiredEngine =
        "Switched off by Staff Travel (final closure lane 8, D-45): this went to whoever pressed Submit — the traveller only on a self-service submission — and linked the desk's page, which the traveller cannot open. StaffTravel.*.Traveller replaces it. A workflow-topic seed switches it back on; the next travel notice switches it off again.";

    private async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_topicsEnsured.Contains(tenantId)) return;

        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = Seeds.Select(Key).ToArray();
        var existing = new HashSet<string>(await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken), StringComparer.OrdinalIgnoreCase);

        var retiredKeys = RetiredTopics.Select(r => r.Key).ToArray();
        var live = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.IsSystem && t.IsActive && retiredKeys.Contains(t.Key))
            .ToListAsync(cancellationToken);

        if (existing.Count == Seeds.Length && live.Count == 0)
        {
            _topicsEnsured.Add(tenantId);
            return;
        }

        foreach (var old in live)
        {
            old.IsActive = false;
            old.Description = RetiredTopics.First(r => string.Equals(r.Key, old.Key, StringComparison.OrdinalIgnoreCase)).Why;
            old.UpdatedAt = DateTime.UtcNow;
            old.UpdatedBy = "System";
        }

        var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
        foreach (var seed in Seeds)
        {
            var key = Key(seed);
            if (existing.Contains(key)) continue;

            var toTraveller = seed.Audience == ToTraveller;
            var topic = new NotificationTopic
            {
                TenantId = tenantId, Key = key, Name = seed.Name, Description = seed.Description,
                EntityType = TopicEntityType, IsSystem = true, IsActive = true,
                EnableInApp = true, EnableEmail = toTraveller, EnableSms = false,
                InAppTitleTemplate = seed.Title,
                InAppBodyTemplate = seed.Body,
                ActionUrlTemplate = "{{ActionPath}}",
                CreatedBy = "System",
            };
            await topicRepo.AddAsync(topic);

            if (toTraveller)
            {
                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    TenantId = tenantId, TopicId = topic.Id,
                    RecipientKind = "UserFromEmployeeIdData", RecipientValue = "EmployeeId",
                    IsSystem = true, SendInApp = true, SendEmail = true, CreatedBy = "System",
                });
                // Set in the data only for a traveller with no login (see the class remarks).
                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    TenantId = tenantId, TopicId = topic.Id,
                    RecipientKind = "EmailFromData", RecipientValue = "TravellerEmail",
                    IsSystem = true, SendInApp = false, SendEmail = true, CreatedBy = "System",
                });
            }
            else
            {
                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    TenantId = tenantId, TopicId = topic.Id,
                    RecipientKind = "UsersFromData", RecipientValue = "DeskUserIds",
                    IsSystem = true, SendInApp = true, SendEmail = false, CreatedBy = "System",
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _topicsEnsured.Add(tenantId);
    }

    private static string Key(TopicSeed seed) => $"{TopicEntityType}.{seed.Event}.{seed.Audience}";
}
