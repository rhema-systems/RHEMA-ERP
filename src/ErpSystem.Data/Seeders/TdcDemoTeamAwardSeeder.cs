using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Awards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the one award record the API cannot produce: a <b>conferred team award</b> and the
/// <see cref="TeamAwardRecipient"/> rows that say who shared it.
///
/// <para><b>Why this is a seeder and not a scenario.</b> Every other awards table is filled through
/// the real doors by <c>scenarios/130-awards.mjs</c>. <c>TeamAwardRecipients</c> has no door at all:
/// <c>ITeamAwardRecipientRepository</c> is registered in <c>HrModuleServiceRegistration</c> and read
/// by the reporting side, but <b>no service and no controller writes it</b>. The two routes that
/// could have are both closed to a team:</para>
/// <list type="bullet">
/// <item><c>POST /api/Awards/nominations/{id}/confer</c> refuses a nomination whose
/// <c>NomineeId</c> is null — "a team nomination, which has no single recipient. Conferring a team
/// award is not supported on this route."</item>
/// <item><c>POST /api/Awards</c> refuses any award type whose candidates come from
/// <c>OpenNomination</c>, which the Team Excellence Award's do.</item>
/// </list>
/// <para>So a team award can be nominated for and its nominees listed, and then the process stops.
/// This seeder writes the end of it by hand so Book 3 §5 can show a team award that was actually
/// given, and so the recipients screen opens on data. The gap itself is recorded as a defect.</para>
///
/// <para><b>What it writes.</b> One <see cref="EmployeeAward"/> for last year's Team Excellence
/// Award, held on the record of the team lead — the entity requires a single <c>EmployeeId</c>, and
/// the shared nature of the award is expressed by the recipient rows rather than by that column —
/// plus one <see cref="TeamAwardRecipient"/> per member with the share of the prize each took. The
/// shares sum to 100 per cent of a GHS 20,000 award.</para>
///
/// <para>Deterministic: the team is chosen by position title, the award number is fixed, and the
/// whole block is skipped when a live <c>TeamAwardRecipient</c> already exists for the tenant.</para>
/// </summary>
public class TdcDemoTeamAwardSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoTeamAwardSeeder> _logger;

    private const string By = "TdcDemoTeamAwardSeeder";
    private const string AwardTypeName = "Team Excellence Award";
    private const string TeamName = "Community 25 Phase 2 Delivery Team";

    public TdcDemoTeamAwardSeeder(ApplicationDbContext context, ILogger<TdcDemoTeamAwardSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the team award.");
            return;
        }

        var tenantId = tenant.Id;

        if (await _context.Set<TeamAwardRecipient>().IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == tenantId && !r.IsDeleted, ct))
        {
            _logger.LogInformation("Team award recipients already present for the DEFAULT tenant. Skipping.");
            return;
        }

        var awardType = await _context.Set<AwardType>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Name == AwardTypeName, ct);
        if (awardType is null)
        {
            _logger.LogWarning("Award type '{Name}' not found — the team award cannot be seeded.", AwardTypeName);
            return;
        }

        // The team, by position title. Each member must be a live TDC employee; anyone the
        // establishment does not currently hold is simply left out of the citation.
        var wanted = new[]
        {
            new { Title = "Head of Development", Role = "Team Lead", Share = 30m,
                  Contribution = "Held the programme and the six consultants to a single delivery schedule for eleven months." },
            new { Title = "Project Coordinator", Role = "Coordinator", Share = 25m,
                  Contribution = "Ran the weekly site coordination meeting and kept the risk register current throughout." },
            new { Title = "Environmental Officer", Role = "Environmental Lead", Share = 20m,
                  Contribution = "Kept every EPA permit condition met through clearance, earthworks and services." },
            new { Title = "Quantity Surveyor", Role = "Cost Control", Share = 25m,
                  Contribution = "Held the contract sum inside the approved budget across four variations." },
        };

        var people = new List<(Employee Employee, string Role, decimal Share, string Contribution)>();
        foreach (var w in wanted)
        {
            var employee = await _context.Set<Employee>().IgnoreQueryFilters()
                .Include(e => e.Position)
                .Where(e => e.TenantId == tenantId && !e.IsDeleted
                    && e.EmployeeNumber.StartsWith("TDC/")
                    && e.Position.Title == w.Title)
                .OrderBy(e => e.EmployeeNumber)
                .FirstOrDefaultAsync(ct);

            if (employee is not null) people.Add((employee, w.Role, w.Share, w.Contribution));
        }

        if (people.Count == 0)
        {
            _logger.LogWarning("None of the team-award positions is filled — the team award cannot be seeded.");
            return;
        }

        var now = DateTime.UtcNow;
        var year = now.Year - 1;
        var lead = people[0].Employee;
        const decimal prize = 20000m;

        var nomination = await _context.Set<AwardNomination>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && !n.IsDeleted && n.TeamName == TeamName, ct);

        var award = new EmployeeAward
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AwardNumber = $"AWD-TEAM-{year}-001",
            EmployeeId = lead.Id,
            AwardTypeId = awardType.Id,
            AwardDate = new DateTime(year, 12, 18, 0, 0, 0, DateTimeKind.Utc),
            Citation = $"{TeamName}. For delivering 240 serviced plots at Community 25 Phase 2 inside the "
                + "approved budget, four months ahead of the programme, with no reportable environmental incident.",
            MonetaryAmount = prize,
            CertificateIssued = true,
            TrophyIssued = true,
            PaymentProcessed = false,
            PublishToWebsite = true,
            PresentationDate = new DateTime(year, 12, 18, 0, 0, 0, DateTimeKind.Utc),
            PresentationVenue = "Tema Development Corporation head office, staff durbar ground",
            PublicationNotes = "Read at the Long Service and Awards Night; the prize is shared between the team.",
            CreatedAt = now,
            CreatedBy = By,
        };

        // The nomination is only linked when one exists — the scenario module creates it, and this
        // seeder must still work on a database where the scenarios have not been run.
        if (nomination is not null) award.AwardNominationId = nomination.Id;

        _context.Set<EmployeeAward>().Add(award);

        foreach (var (employee, role, share, contribution) in people)
        {
            _context.Set<TeamAwardRecipient>().Add(new TeamAwardRecipient
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AwardId = award.Id,
                EmployeeId = employee.Id,
                Role = role,
                Contribution = contribution,
                MonetaryShare = Math.Round(prize * share / 100m, 2),
                LeaveDaysShare = null,
                CreatedAt = now,
                CreatedBy = By,
            });
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded the {Year} team award and {Count} recipients.", year, people.Count);
    }
}
