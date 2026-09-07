using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds a coherent SHE (Safety, Health &amp; Environment) demo dataset for the DEFAULT tenant.
/// Cross-module references (employees, locations, organization units, departments) are resolved
/// from the database at run time and referenced by their existing IDs — no rows are created for
/// them. Idempotent: each area is keyed on its natural code/number and skips if already present,
/// so the seeder is safe to run repeatedly.
///
/// The dataset is shaped so every SHE dashboard tile (open incidents, overdue corrective actions,
/// hazards due for review, high-risk hazards, expiring risk assessments, inspections due, active /
/// expiring / suspended permits, equipment due-for-inspection / expiring-cert / out-of-service,
/// PPE below reorder) reports a non-zero count.
/// </summary>
public class SheDataSeeder
{
    private static readonly Guid DefaultTenantIdFallback = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string SeedUser = "system-seed";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SheDataSeeder> _logger;

    private Guid _tenantId;
    private DateTime _today;
    private List<Employee> _employees = new();
    private List<Location> _locations = new();
    private List<OrganizationUnit> _orgUnits = new();
    private List<Department> _departments = new();

    public SheDataSeeder(ApplicationDbContext context, ILogger<SheDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting SHE demo data seeding...");

        _tenantId = await ResolveDefaultTenantIdAsync();
        if (_tenantId == Guid.Empty)
        {
            _logger.LogWarning("Default tenant not found; skipping SHE seeding.");
            return;
        }

        _today = DateTime.Today;

        _employees = await _context.Employees
            .Where(e => e.TenantId == _tenantId && !e.IsDeleted)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .ToListAsync();

        if (_employees.Count == 0)
        {
            _logger.LogWarning("No employees in DEFAULT tenant; run HR seeding first. Skipping SHE seeding.");
            return;
        }

        _locations = await _context.Locations
            .Where(l => l.TenantId == _tenantId && !l.IsDeleted)
            .OrderBy(l => l.Name).ToListAsync();

        if (_locations.Count == 0)
        {
            _logger.LogWarning("No locations in DEFAULT tenant; run HR seeding first. Skipping SHE seeding.");
            return;
        }

        _orgUnits = await _context.OrganizationUnits
            .Where(o => o.TenantId == _tenantId && !o.IsDeleted).ToListAsync();
        _departments = await _context.Departments
            .Where(d => d.TenantId == _tenantId && !d.IsDeleted).ToListAsync();

        // Area order matters: reference catalogs first (incidents/hazards reference them),
        // then contractors (waste/permits reference them), then everything else.
        var bodies = await SeedRegulatoryBodiesAsync();
        var incidentTypes = await SeedIncidentTypesAsync(bodies);
        var injuryTypes = await SeedInjuryTypesAsync();
        var bodyParts = await SeedBodyPartsAsync();
        var caTemplates = await SeedCorrectiveActionTemplatesAsync();

        await SeedIncidentsAsync(incidentTypes, injuryTypes, bodyParts);
        await SeedHazardsAsync(caTemplates);
        await SeedRiskAssessmentsAsync();
        await SeedInspectionsAsync(caTemplates);
        await SeedPermitsAsync();
        await SeedPpeAsync();
        await SeedEquipmentAsync();
        var contractors = await SeedContractorsAsync();
        await SeedTrainingAsync();
        await SeedWasteAsync(contractors);
        await SeedEnvironmentalAsync();
        await SeedOccupationalHealthAsync();
        await SeedEmergencyAsync();
        await SeedRegulatoryObligationsAsync(bodies);
        await SeedSignsAsync();
        await SeedPerformanceAsync();
        await SeedCommitteeAsync();
        await SeedReturnToWorkAsync();

        _logger.LogInformation("SHE demo data seeding completed.");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private T New<T>(T e) where T : TenantEntity
    {
        e.TenantId = _tenantId;
        e.CreatedBy = SeedUser;
        return e;
    }

    private Guid Emp(int i) => _employees[i % _employees.Count].Id;
    private Guid? Loc(int i) => _locations.Count == 0 ? null : _locations[i % _locations.Count].Id;
    private Guid LocReq(int i) => _locations[i % _locations.Count].Id;
    private Guid? Org(int i) => _orgUnits.Count == 0 ? null : _orgUnits[i % _orgUnits.Count].Id;

    private async Task<Guid> ResolveDefaultTenantIdAsync()
    {
        var tenant = await _context.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == DefaultTenantIdFallback);
        return tenant?.Id ?? DefaultTenantIdFallback;
    }

    // ── A. Reference catalog ─────────────────────────────────────────────────

    private async Task<Dictionary<string, SheRegulatoryBody>> SeedRegulatoryBodiesAsync()
    {
        var existing = await _context.SheRegulatoryBodies
            .Where(b => b.TenantId == _tenantId).ToListAsync();
        var map = existing.ToDictionary(b => b.Name, b => b);

        void Add(string name, string shortName, SheRegulatoryDomain domain, string? email)
        {
            if (map.ContainsKey(name)) return;
            var b = New(new SheRegulatoryBody
            {
                Name = name, ShortName = shortName, Domain = domain, Email = email,
                ContactAddress = "Accra, Ghana", IsActive = true,
            });
            _context.SheRegulatoryBodies.Add(b);
            map[name] = b;
        }

        Add("Department of Factories Inspectorate", "DFI", SheRegulatoryDomain.OccupationalSafety, "info@dfi.gov.gh");
        Add("Environmental Protection Agency", "EPA", SheRegulatoryDomain.EnvironmentalProtection, "info@epa.gov.gh");
        Add("Ghana National Fire Service", "GNFS", SheRegulatoryDomain.FireSafety, "info@gnfs.gov.gh");

        await _context.SaveChangesAsync();
        return map;
    }

    private async Task<Dictionary<string, SheIncidentType>> SeedIncidentTypesAsync(Dictionary<string, SheRegulatoryBody> bodies)
    {
        var existing = await _context.SheIncidentTypes
            .Where(t => t.TenantId == _tenantId).ToListAsync();
        var map = existing.ToDictionary(t => t.Code, t => t);

        void Add(string code, string name, SheIncidentCategory cat, bool reportable, string? bodyName, int? windowHours)
        {
            if (map.ContainsKey(code)) return;
            var t = New(new SheIncidentType
            {
                Code = code, Name = name, Category = cat, IsReportable = reportable,
                ReportingWindowHours = windowHours, IsActive = true,
                RegulatoryBodyId = bodyName is not null && bodies.TryGetValue(bodyName, out var b) ? b.Id : null,
            });
            _context.SheIncidentTypes.Add(t);
            map[code] = t;
        }

        Add("INC-ACC", "Workplace Accident", SheIncidentCategory.Accident, true, "Department of Factories Inspectorate", 24);
        Add("INC-NM", "Near Miss", SheIncidentCategory.NearMiss, false, null, null);
        Add("INC-ENV", "Environmental Release", SheIncidentCategory.EnvironmentalIncident, true, "Environmental Protection Agency", 48);
        Add("INC-FIRE", "Fire Incident", SheIncidentCategory.FireIncident, true, "Ghana National Fire Service", 24);

        await _context.SaveChangesAsync();
        return map;
    }

    private async Task<Dictionary<string, SheInjuryType>> SeedInjuryTypesAsync()
    {
        var existing = await _context.SheInjuryTypes.Where(t => t.TenantId == _tenantId).ToListAsync();
        var map = existing.ToDictionary(t => t.Code, t => t);

        void Add(string code, string name)
        {
            if (map.ContainsKey(code)) return;
            var t = New(new SheInjuryType { Code = code, Name = name, IsActive = true });
            _context.SheInjuryTypes.Add(t);
            map[code] = t;
        }

        Add("LAC", "Laceration / Cut");
        Add("FRAC", "Fracture");
        Add("BURN", "Burn / Scald");
        Add("STRAIN", "Sprain / Strain");
        Add("BRUISE", "Contusion / Bruise");

        await _context.SaveChangesAsync();
        return map;
    }

    private async Task<Dictionary<string, SheBodyPart>> SeedBodyPartsAsync()
    {
        var existing = await _context.SheBodyParts.Where(t => t.TenantId == _tenantId).ToListAsync();
        var map = existing.ToDictionary(t => t.Code, t => t);

        void Add(string code, string name, string region)
        {
            if (map.ContainsKey(code)) return;
            var t = New(new SheBodyPart { Code = code, Name = name, Region = region, IsActive = true });
            _context.SheBodyParts.Add(t);
            map[code] = t;
        }

        Add("HAND", "Hand", "Upper Limb");
        Add("EYE", "Eye", "Head");
        Add("FOOT", "Foot", "Lower Limb");
        Add("BACK", "Back", "Torso");
        Add("HEAD", "Head", "Head");

        await _context.SaveChangesAsync();
        return map;
    }

    private async Task<Dictionary<string, SheCorrectiveActionTemplate>> SeedCorrectiveActionTemplatesAsync()
    {
        var existing = await _context.SheCorrectiveActionTemplates.Where(t => t.TenantId == _tenantId).ToListAsync();
        var map = existing.ToDictionary(t => t.Code, t => t);

        void Add(string code, string title, SheCorrectiveActionCategory cat, int deadline)
        {
            if (map.ContainsKey(code)) return;
            var t = New(new SheCorrectiveActionTemplate
            {
                Code = code, Title = title, Description = title, Category = cat,
                DefaultDeadlineDays = deadline, IsActive = true,
            });
            _context.SheCorrectiveActionTemplates.Add(t);
            map[code] = t;
        }

        Add("CA-GUARD", "Install / repair machine guarding", SheCorrectiveActionCategory.Engineering, 14);
        Add("CA-TRAIN", "Conduct refresher training", SheCorrectiveActionCategory.Training, 30);
        Add("CA-PPE", "Issue / replace PPE", SheCorrectiveActionCategory.PPE, 7);
        Add("CA-SOP", "Update safe work procedure", SheCorrectiveActionCategory.PolicyProcedure, 21);
        Add("CA-HOUSE", "Housekeeping clean-up", SheCorrectiveActionCategory.Administrative, 3);

        await _context.SaveChangesAsync();
        return map;
    }

    // ── B. Incidents ─────────────────────────────────────────────────────────

    private async Task SeedIncidentsAsync(
        Dictionary<string, SheIncidentType> types,
        Dictionary<string, SheInjuryType> injuryTypes,
        Dictionary<string, SheBodyPart> bodyParts)
    {
        if (await _context.SafetyIncidents.AnyAsync(i => i.TenantId == _tenantId && i.IncidentNumber == "INC-2026-001"))
            return;

        Guid? TypeId(string code) => types.TryGetValue(code, out var t) ? t.Id : null;

        // 1) Major lost-time accident, under investigation, with overdue corrective action.
        var inc1 = New(new SafetyIncident
        {
            IncidentNumber = "INC-2026-001",
            Category = SheIncidentCategory.Accident,
            Severity = SheIncidentSeverity.Major,
            Status = SheIncidentStatus.InvestigationInProgress,
            IncidentTypeId = TypeId("INC-ACC"),
            IncidentDate = _today.AddDays(-12),
            IncidentTime = new TimeSpan(10, 30, 0),
            LocationId = Loc(0),
            SpecificArea = "Packing line 2",
            OrganizationUnitId = Org(0),
            SupervisorId = Emp(1),
            Description = "Operator's hand caught in an unguarded conveyor pinch point during a clearing task.",
            ImmediateCause = "Missing machine guard; clearing jam with machine running.",
            UnderlyingCause = "Guard removed during prior maintenance and not reinstated.",
            ReportedById = Emp(0),
            ReportedDate = _today.AddDays(-12),
            ImmediateActionTaken = "First aid administered; conveyor isolated and locked out.",
            LikelihoodBefore = 4, SeverityBefore = 4, RiskScoreBefore = 16,
            LikelihoodAfter = 2, SeverityAfter = 3, RiskScoreAfter = 6,
            RequiresInvestigation = true,
            LeadInvestigatorId = Emp(2),
            InvestigationStartDate = _today.AddDays(-10),
            InvestigationTargetDate = _today.AddDays(4),
            RootCauseMethod = SheRootCauseMethod.FiveWhy,
            ReportableToAuthority = true,
            IsLostTimeInjury = true,
            TotalLostDays = 5,
        });
        var person = New(new SafetyIncidentInvolvedPerson
        {
            IsEmployee = true,
            EmployeeId = Emp(3),
            FullName = $"{_employees[3 % _employees.Count].FirstName} {_employees[3 % _employees.Count].LastName}",
            RoleInIncident = SheInvolvedPersonRole.PrimaryVictim,
            WasOnDuty = true,
            ActivityBeingPerformed = "Clearing a product jam on the conveyor.",
            WasUsingPpe = true, PpeUsed = "Gloves, safety boots", PpeWasAdequate = false,
            WasInjured = true,
            InjuryDescription = "Deep laceration to the left hand.",
            InjuryClassification = SheInjuryClassification.LostTimeInjury,
            InjuryTypeId = injuryTypes.TryGetValue("LAC", out var lac) ? lac.Id : null,
            FirstAidGiven = true, FirstAidDetails = "Wound dressed on site.",
            MedicalTreatmentRequired = true, TreatmentDate = _today.AddDays(-12),
            ResultedInTimeOff = true,
            TimeOffStartDate = _today.AddDays(-12), TimeOffEndDate = _today.AddDays(-7), LostDays = 5,
        });
        if (bodyParts.TryGetValue("HAND", out var hand))
        {
            person.InjuredBodyParts.Add(New(new SafetyIncidentInjuredBodyPart
            {
                BodyPartId = hand.Id, Side = SheBodySide.Left, Notes = "Palm and two fingers.",
            }));
        }
        inc1.InvolvedPersons.Add(person);
        inc1.CorrectiveActions.Add(New(new SafetyIncidentCorrectiveAction
        {
            ActionDescription = "Install fixed guarding on the conveyor pinch point.",
            Priority = SheCorrectiveActionPriority.Critical,
            Status = SheCorrectiveActionStatus.InProgress,
            ResponsiblePersonId = Emp(1),
            DueDate = _today.AddDays(-3), // overdue
        }));
        inc1.CorrectiveActions.Add(New(new SafetyIncidentCorrectiveAction
        {
            ActionDescription = "Re-train line operators on lock-out / tag-out before clearing jams.",
            Priority = SheCorrectiveActionPriority.High,
            Status = SheCorrectiveActionStatus.Pending,
            ResponsiblePersonId = Emp(2),
            DueDate = _today.AddDays(10),
        }));

        // 2) Near miss, just reported.
        var inc2 = New(new SafetyIncident
        {
            IncidentNumber = "INC-2026-002",
            Category = SheIncidentCategory.NearMiss,
            Severity = SheIncidentSeverity.Minor,
            Status = SheIncidentStatus.Reported,
            IncidentTypeId = TypeId("INC-NM"),
            IncidentDate = _today.AddDays(-4),
            LocationId = Loc(1),
            SpecificArea = "Warehouse aisle 3",
            Description = "A pallet fell from a forklift; no one was struck.",
            CouldHaveCausedInjury = true,
            PotentialConsequence = "Crush injury to a pedestrian.",
            ReportedById = Emp(1),
            ReportedDate = _today.AddDays(-4),
            RequiresInvestigation = false,
        });

        // 3) Fire incident, pending corrective actions, with overdue action.
        var inc3 = New(new SafetyIncident
        {
            IncidentNumber = "INC-2026-003",
            Category = SheIncidentCategory.FireIncident,
            Severity = SheIncidentSeverity.Moderate,
            Status = SheIncidentStatus.PendingCorrective,
            IncidentTypeId = TypeId("INC-FIRE"),
            IncidentDate = _today.AddDays(-25),
            LocationId = Loc(0),
            SpecificArea = "Electrical switch room",
            Description = "A small electrical fire in a distribution board, extinguished with a CO2 extinguisher.",
            ImmediateCause = "Overloaded circuit.",
            ReportedById = Emp(2),
            ReportedDate = _today.AddDays(-25),
            RequiresInvestigation = true,
            LeadInvestigatorId = Emp(0),
            ReportableToAuthority = true,
        });
        inc3.CorrectiveActions.Add(New(new SafetyIncidentCorrectiveAction
        {
            ActionDescription = "Carry out a thermographic survey of all distribution boards.",
            Priority = SheCorrectiveActionPriority.High,
            Status = SheCorrectiveActionStatus.InProgress,
            ResponsiblePersonId = Emp(1),
            DueDate = _today.AddDays(-2), // overdue
        }));

        // 4) Closed minor accident (won't count as open).
        var inc4 = New(new SafetyIncident
        {
            IncidentNumber = "INC-2026-004",
            Category = SheIncidentCategory.Accident,
            Severity = SheIncidentSeverity.Minor,
            Status = SheIncidentStatus.Closed,
            IncidentTypeId = TypeId("INC-ACC"),
            IncidentDate = _today.AddDays(-50),
            LocationId = Loc(2),
            Description = "Minor slip on a wet floor; no injury beyond a bruise.",
            ReportedById = Emp(3),
            ReportedDate = _today.AddDays(-50),
            RequiresInvestigation = false,
            ClosedDate = _today.AddDays(-30),
            ClosedById = Emp(0),
            ClosureNotes = "Wet-floor signage and a mopping schedule introduced.",
        });

        _context.SafetyIncidents.AddRange(inc1, inc2, inc3, inc4);
        await _context.SaveChangesAsync();
    }

    // ── C. Hazards ───────────────────────────────────────────────────────────

    private async Task SeedHazardsAsync(Dictionary<string, SheCorrectiveActionTemplate> ca)
    {
        if (await _context.SheHazards.AnyAsync(h => h.TenantId == _tenantId && h.Code == "HAZ-001"))
            return;

        var h1 = New(new SheHazard
        {
            Code = "HAZ-001", Name = "Unguarded conveyor pinch point", Category = SheHazardCategory.Mechanical,
            Description = "Pinch point on packing-line conveyor where guards have historically been removed.",
            LocationId = Loc(0), SpecificArea = "Packing line 2",
            InherentLikelihood = 4, InherentSeverity = 4, InherentRiskScore = 16,
            ResidualLikelihood = 2, ResidualSeverity = 3, ResidualRiskScore = 6,
            ResidualRiskLevel = SheHazardRiskLevel.Medium,
            Status = SheHazardStatus.ControlsInPlace,
            OwnerId = Emp(0),
            ReviewDueDate = _today.AddDays(15), // due for review
            IsActive = true,
        });
        h1.Controls.Add(New(new SheHazardControl
        {
            ControlLevel = SheHierarchyOfControl.Engineering,
            ControlDescription = "Fixed mesh guard over the pinch point.",
            Status = SheControlStatus.Implemented,
            ResponsiblePersonId = Emp(1),
            ImplementationDate = _today.AddDays(-2),
        }));

        var h2 = New(new SheHazard
        {
            Code = "HAZ-002", Name = "Exposure to welding fumes", Category = SheHazardCategory.Chemical,
            Description = "Manganese-bearing welding fumes in the fabrication bay with inadequate extraction.",
            LocationId = Loc(1), SpecificArea = "Fabrication bay",
            InherentLikelihood = 4, InherentSeverity = 4, InherentRiskScore = 16,
            ResidualLikelihood = 3, ResidualSeverity = 4, ResidualRiskScore = 12,
            ResidualRiskLevel = SheHazardRiskLevel.High,
            Status = SheHazardStatus.UnderAssessment,
            OwnerId = Emp(2),
            ReviewDueDate = _today.AddDays(10), // due for review + high residual risk
            IsActive = true,
        });
        h2.Controls.Add(New(new SheHazardControl
        {
            ControlLevel = SheHierarchyOfControl.Engineering,
            ControlDescription = "Install local exhaust ventilation at welding bays.",
            Status = SheControlStatus.Planned,
            ResponsiblePersonId = Emp(0),
            ReviewDate = _today.AddDays(30),
        }));

        var h3 = New(new SheHazard
        {
            Code = "HAZ-003", Name = "Wet floor in canteen", Category = SheHazardCategory.SlipTripFall,
            Description = "Spillages around the beverage station create slip risk.",
            LocationId = Loc(2), SpecificArea = "Staff canteen",
            InherentLikelihood = 3, InherentSeverity = 3, InherentRiskScore = 9,
            ResidualLikelihood = 2, ResidualSeverity = 2, ResidualRiskScore = 4,
            ResidualRiskLevel = SheHazardRiskLevel.Low,
            Status = SheHazardStatus.Monitoring,
            OwnerId = Emp(1),
            ReviewDueDate = _today.AddDays(90),
            IsActive = true,
        });

        _context.SheHazards.AddRange(h1, h2, h3);
        await _context.SaveChangesAsync();
    }

    // ── C. Risk assessments ──────────────────────────────────────────────────

    private async Task SeedRiskAssessmentsAsync()
    {
        if (await _context.SheRiskAssessments.AnyAsync(r => r.TenantId == _tenantId && r.AssessmentNumber == "RA-2026-001"))
            return;

        var ra1 = New(new SheRiskAssessment
        {
            AssessmentNumber = "RA-2026-001", Title = "Hot work in the tank farm",
            Type = SheRiskAssessmentType.JHA,
            Scope = "All hot-work activities (welding, grinding, cutting) within the tank-farm bund.",
            LocationId = Loc(1), SpecificActivity = "Welding repairs on pipework",
            OrganizationUnitId = Org(0),
            Status = SheRiskAssessmentStatus.Active,
            PreparedById = Emp(0), PreparedDate = _today.AddDays(-30),
            ReviewedById = Emp(2), ReviewedDate = _today.AddDays(-28),
            ApprovedById = Emp(1), ApprovedDate = _today.AddDays(-27),
            ValidFrom = _today.AddDays(-27),
            ValidUntil = _today.AddDays(20),       // expiring within 30 days
            NextReviewDate = _today.AddDays(20),    // due for review within 30 days
            Version = 1,
        });
        ra1.AssessedHazards.Add(New(new SheRiskAssessmentHazard
        {
            ItemNumber = 1,
            HazardDescription = "Ignition of flammable vapours during hot work.",
            PotentialConsequences = "Fire / explosion, severe burns.",
            AffectedPersons = "Welders, nearby operators.",
            InherentLikelihood = 4, InherentSeverity = 5, InherentRiskScore = 20, InherentRiskLevel = SheRiskLevel.Critical,
            ControlMeasures = "Gas testing before and during work; fire watch; extinguishers on standby; permit-to-work.",
            ResidualLikelihood = 2, ResidualSeverity = 4, ResidualRiskScore = 8, ResidualRiskLevel = SheRiskLevel.Medium,
            ResponsiblePerson = "Hot-work supervisor", TargetDate = _today.AddDays(-25),
        }));
        ra1.Acknowledgements.Add(New(new SheRiskAssessmentAcknowledgement
        {
            EmployeeId = Emp(3), AcknowledgedDate = _today.AddDays(-26),
            Comments = "Briefed and understood.",
        }));

        var ra2 = New(new SheRiskAssessment
        {
            AssessmentNumber = "RA-2026-002", Title = "Office workstation ergonomics",
            Type = SheRiskAssessmentType.ErgoAssessment,
            Scope = "Display-screen-equipment workstations in the administration block.",
            LocationId = Loc(0),
            Status = SheRiskAssessmentStatus.Draft,
            PreparedById = Emp(2), PreparedDate = _today.AddDays(-5),
            ValidUntil = _today.AddDays(200),
            NextReviewDate = _today.AddDays(200),
            Version = 1,
        });

        _context.SheRiskAssessments.AddRange(ra1, ra2);
        await _context.SaveChangesAsync();
    }

    // ── D. Inspections ───────────────────────────────────────────────────────

    // Three published templates (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §6): the
    // original general walk, now sectioned, plus TDC's own two paper forms transcribed — the Food
    // Vendor Screening & Inspection Checklist (percentage bands + critical disqualifiers) and the
    // Cafeteria Inspection Checklist (qualitative rating). Then one run against each shape.
    private async Task SeedInspectionsAsync(Dictionary<string, SheCorrectiveActionTemplate> ca)
    {
        // Each template seeds independently, so `seed-hr-all` tops an existing database up with the
        // two TDC forms without a rebuild. A number that already exists is left exactly as it is.
        var have = await _context.SheInspectionChecklists
            .Where(c => c.TenantId == _tenantId && !c.IsDeleted)
            .Select(c => c.ChecklistNumber).ToListAsync();
        if (have.Contains("CHK-001") && have.Contains("CHK-002") && have.Contains("CHK-003"))
            return;

        var publishedOn = _today.AddDays(-30);

        SheInspectionChecklist NewChecklist(string number, string name, string description, SheInspectionType type,
            SheChecklistScoringMode mode, string printTitle, string? printSubtitle, string? instructions, string? criticalNote)
            => New(new SheInspectionChecklist
            {
                ChecklistNumber = number, Name = name, Description = description, Type = type, Version = 1, IsActive = true,
                Status = SheChecklistStatus.Published, PublishedAt = publishedOn, PublishedById = Emp(0),
                ScoringMode = mode, PrintTitle = printTitle, PrintSubtitle = printSubtitle,
                Instructions = instructions, CriticalSectionNote = criticalNote,
            });

        SheInspectionChecklistSection AddSection(SheInspectionChecklist chk, string? code, string title, SheChecklistSectionKind kind, params string[] items)
        {
            var section = New(new SheInspectionChecklistSection
            {
                DisplayOrder = chk.Sections.Count + 1, Code = code, Title = title, Kind = kind,
            });
            chk.Sections.Add(section);
            var order = 0;
            foreach (var text in items)
                chk.Items.Add(New(new SheInspectionChecklistItem
                {
                    Section = section, ItemOrder = ++order, Category = title, ItemDescription = text, IsMandatory = true,
                }));
            return section;
        }

        void AddField(SheInspectionChecklist chk, string label, SheChecklistFieldType type, bool required = false, string? options = null)
            => chk.Fields.Add(New(new SheInspectionChecklistField
            {
                DisplayOrder = chk.Fields.Count + 1, Label = label, FieldType = type, IsRequired = required, ChoiceOptions = options,
            }));

        SheInspectionChecklistOutcome AddOutcome(SheInspectionChecklist chk, string label, decimal? min, decimal? max, int? reinspect = null, bool disqualifying = false, string? description = null)
        {
            var outcome = New(new SheInspectionChecklistOutcome
            {
                DisplayOrder = chk.Outcomes.Count + 1, Label = label, Description = description,
                MinPercent = min, MaxPercent = max, ReinspectionWithinDays = reinspect, IsDisqualifying = disqualifying,
            });
            chk.Outcomes.Add(outcome);
            return outcome;
        }

        SheInspectionChecklistSignatory AddSignatory(SheInspectionChecklist chk, string role, SheChecklistSignatoryKind kind, bool required = true)
        {
            var signatory = New(new SheInspectionChecklistSignatory
            {
                DisplayOrder = chk.Signatories.Count + 1, RoleLabel = role, Kind = kind, IsRequired = required,
            });
            chk.Signatories.Add(signatory);
            return signatory;
        }

        SheInspectionChecklist? chk1 = null, chk2 = null, chk3 = null;
        SheInspectionChecklistOutcome? approvedWithCa = null;
        SheInspectionChecklistSignatory? vendorSig = null, officerSig = null, siteRepSig = null;

        // ── CHK-001 · General workplace walk ──
        if (!have.Contains("CHK-001"))
        {
        chk1 = NewChecklist("CHK-001", "General Workplace Inspection",
            "Routine general-area workplace inspection checklist.", SheInspectionType.Routine,
            SheChecklistScoringMode.CompliancePercentage, "GENERAL WORKPLACE INSPECTION CHECKLIST", null,
            "Score every item C, NC or NA. Non-compliant items become corrective actions tracked to close-out.", null);
        AddField(chk1, "Specific area", SheChecklistFieldType.Text, required: true);
        AddField(chk1, "Weather (if applicable)", SheChecklistFieldType.Text);
        AddSection(chk1, "A", "Housekeeping", SheChecklistSectionKind.Standard,
            "Walkways clear of obstructions.",
            "Work areas free of spills and waste.");
        AddSection(chk1, "B", "Fire Safety", SheChecklistSectionKind.Standard,
            "Fire extinguishers accessible and in date.",
            "Emergency exits unobstructed and signed.");
        AddSection(chk1, "C", "Electrical", SheChecklistSectionKind.Standard,
            "No damaged cables or overloaded sockets.");
        AddOutcome(chk1, "Satisfactory", 80m, 100m);
        AddOutcome(chk1, "Requires improvement", 60m, 79.99m, reinspect: 14);
        AddOutcome(chk1, "Unsatisfactory", 0m, 59.99m, reinspect: 7);
        AddSignatory(chk1, "Inspector", SheChecklistSignatoryKind.SystemUser);
        AddSignatory(chk1, "Area supervisor", SheChecklistSignatoryKind.External, required: false);
        AddSignatory(chk1, "SHE Supervisor", SheChecklistSignatoryKind.SystemUser);
        }

        // ── CHK-002 · Food vendor screening & inspection (the Community 27 form) ──
        if (!have.Contains("CHK-002"))
        {
        chk2 = NewChecklist("CHK-002", "Food Vendor Screening & Inspection",
            "Pre-qualification, routine inspection and periodic SHE audit of food vendors operating on a construction site.",
            SheInspectionType.Routine, SheChecklistScoringMode.CompliancePercentage,
            "FOOD VENDOR SCREENING & INSPECTION CHECKLIST", "Community 27 Construction Site",
            "Immediate Disqualification Conditions — a vendor should not be permitted to operate on the site if any of the following are observed: " +
            "no valid food handler's medical certificate; evidence of food contamination or spoiled food; active pest infestation; unsafe LPG installation or gas leakage; " +
            "lack of potable water for food preparation and handwashing; serious personal hygiene deficiencies; failure to comply with TDC Ghana Ltd SHE requirements or corrective actions.\n\n" +
            "This checklist is suitable for pre-qualification, routine inspections and periodic SHE audits of food vendors operating at the construction site and aligns with good practices in food hygiene, occupational health and construction site safety.",
            "Tick where applicable.");
        AddField(chk2, "Site", SheChecklistFieldType.Text, required: true);
        AddField(chk2, "Inspection time", SheChecklistFieldType.Time);
        AddField(chk2, "Vendor name", SheChecklistFieldType.Text, required: true);
        AddField(chk2, "Food", SheChecklistFieldType.Text);
        AddField(chk2, "Stall / location", SheChecklistFieldType.Text);
        AddField(chk2, "Vendor contact", SheChecklistFieldType.Text);
        AddField(chk2, "Type of food sold", SheChecklistFieldType.Text);
        AddField(chk2, "SHE Officer", SheChecklistFieldType.Employee, required: true);
        AddField(chk2, "Inspection type", SheChecklistFieldType.Choice, required: true, options: "Initial|Routine|Follow-up|Complaint Investigation");
        AddSection(chk2, "A", "Vendor Documentation", SheChecklistSectionKind.Standard,
            "Vendor has management approval to operate on site",
            "Vendor possesses a valid national identification",
            "Food handler has a valid medical certificate",
            "Vendor possesses a valid Food Hygiene Certificate (where applicable)",
            "Vendor has emergency contact details available");
        AddSection(chk2, "B", "Personal Hygiene", SheChecklistSectionKind.Standard,
            "Vendor appears clean and well-groomed",
            "Fingernails are clean and trimmed",
            "Hair is properly covered",
            "Clean protective clothing/apron worn",
            "Closed shoes worn during food preparation",
            "No visible skin infections or wounds",
            "Hands washed before food preparation",
            "Disposable gloves used where necessary");
        AddSection(chk2, "C", "Food Preparation Area", SheChecklistSectionKind.Standard,
            "Food preparation area is clean",
            "Tables and work surfaces are clean",
            "Adequate lighting available",
            "Area is free from dust and smoke",
            "Food protected from contamination",
            "Cooking equipment is clean",
            "Food served with utensils rather than bare hands");
        AddSection(chk2, "D", "Food Safety", SheChecklistSectionKind.Standard,
            "Raw and cooked foods stored separately",
            "Food is adequately covered",
            "Perishable foods properly refrigerated or kept hot",
            "No spoiled or expired food observed",
            "Ingredients appear fresh",
            "Cooking oil appears clean and suitable for use",
            "Food served at safe temperature");
        AddSection(chk2, "E", "Water Supply", SheChecklistSectionKind.Standard,
            "Potable water available",
            "Water stored in clean containers",
            "Separate handwashing facility provided",
            "Soap available for handwashing");
        AddSection(chk2, "F", "Waste Management", SheChecklistSectionKind.Standard,
            "Waste bins available",
            "Waste bins have covers",
            "Waste disposed of regularly",
            "Waste area free from foul odour");
        AddSection(chk2, "G", "Pest Control", SheChecklistSectionKind.Standard,
            "No evidence of rodents",
            "No evidence of cockroaches",
            "No flies on food",
            "Food adequately protected from insects");
        AddSection(chk2, "H", "Fire & General Safety", SheChecklistSectionKind.Standard,
            "LPG cylinder in good condition",
            "LPG hose in good condition",
            "Gas regulator properly fitted",
            "No gas leakage detected",
            "Fire extinguisher available",
            "Fire extinguisher inspection date valid",
            "Cooking area well ventilated",
            "No combustible materials near cooking area",
            "Electrical wiring safe");
        AddSection(chk2, "I", "Environmental Compliance", SheChecklistSectionKind.Standard,
            "Surroundings kept clean",
            "No stagnant water",
            "No offensive odour",
            "Adequate drainage around stall");
        AddSection(chk2, "J", "Worker Welfare", SheChecklistSectionKind.Standard,
            "Vendor has access to toilet facilities",
            "Vendor understands food safety requirements",
            "Vendor follows site SHE rules");
        AddSection(chk2, null, "Critical Non-Conformities (Immediate Disqualification)", SheChecklistSectionKind.Critical,
            "Food handler has infectious disease",
            "Expired food being sold",
            "Unsafe drinking water",
            "Evidence of food poisoning risk",
            "Pest infestation",
            "Gas leakage",
            "Food prepared in unhygienic conditions",
            "Lack of valid medical certificate");
        AddOutcome(chk2, "Approved to Operate", 95m, 100m, description: "Approved");
        approvedWithCa = AddOutcome(chk2, "Approved with Corrective Actions", 85m, 94.99m, description: "Approved with corrective actions");
        AddOutcome(chk2, "Conditional Approval", 70m, 84.99m, reinspect: 7, description: "Conditional approval; re-inspection required within 7 days");
        AddOutcome(chk2, "Rejected", 0m, 69.99m, disqualifying: true, description: "Not approved to operate");
        AddOutcome(chk2, "Temporarily Suspended", null, null, description: "Operation suspended pending corrective actions");
        vendorSig = AddSignatory(chk2, "Vendor", SheChecklistSignatoryKind.External);
        officerSig = AddSignatory(chk2, "SHE Officer", SheChecklistSignatoryKind.SystemUser);
        siteRepSig = AddSignatory(chk2, "Site Representative", SheChecklistSignatoryKind.External);

        }

        // ── CHK-003 · Cafeteria inspection ──
        if (!have.Contains("CHK-003"))
        {
        chk3 = NewChecklist("CHK-003", "Cafeteria Inspection",
            "Routine SHE inspection of a staff cafeteria: housekeeping, food hygiene, fire and occupational safety, sanitation, documentation.",
            SheInspectionType.Routine, SheChecklistScoringMode.QualitativeRating,
            "CAFETERIA INSPECTION CHECKLIST", "Safety, Health and Environment (SHE) Section",
            "This checklist is aligned with occupational safety, food hygiene, housekeeping, fire safety and workplace health inspection requirements, making it suitable for routine SHE inspections and audit documentation at TDC Ghana Ltd.",
            null);
        AddField(chk3, "Time", SheChecklistFieldType.Time);
        AddField(chk3, "Location", SheChecklistFieldType.Location, required: true);
        AddField(chk3, "Organization unit", SheChecklistFieldType.OrganizationUnit);
        AddField(chk3, "Name of cafeteria operator", SheChecklistFieldType.Text, required: true);
        AddField(chk3, "Weather (if applicable)", SheChecklistFieldType.Text);
        AddSection(chk3, "A", "General Housekeeping", SheChecklistSectionKind.Standard,
            "Floors are clean, dry and free from slip hazards.",
            "Walls and ceilings are clean and in good condition.",
            "Work surfaces are clean and sanitized.",
            "Waste bins are available, covered and regularly emptied.",
            "No accumulation of rubbish or food waste.",
            "Good housekeeping practices are maintained.");
        AddSection(chk3, "B", "Food Safety & Hygiene", SheChecklistSectionKind.Standard,
            "Food is properly covered and protected from contamination.",
            "Raw and cooked foods are stored separately.",
            "Perishable foods are stored at appropriate temperatures.",
            "Food preparation areas are hygienic.",
            "Expired food items are not present.",
            "Food storage shelves are clean and organized.");
        AddSection(chk3, "C", "Personal Hygiene", SheChecklistSectionKind.Standard,
            "Food handlers wear clean uniforms/aprons.",
            "Hairnets/head covers are worn.",
            "Disposable gloves are used where required.",
            "Staff wash hands before handling food.",
            "No jewellery worn while handling food (except permitted items).",
            "Staff appear medically fit for food handling.");
        AddSection(chk3, "D", "Kitchen Equipment", SheChecklistSectionKind.Standard,
            "Cooking equipment is clean and functional.",
            "Refrigerators/freezers are operational.",
            "Temperature records are maintained.",
            "Gas cylinders are properly secured.",
            "Gas hoses and regulators are in good condition.",
            "Electrical appliances are in good condition.");
        AddSection(chk3, "E", "Fire Safety", SheChecklistSectionKind.Standard,
            "Fire extinguisher available and accessible.",
            "Fire extinguisher inspection tag is current.",
            "Fire blanket available and accessible.",
            "Emergency exits are unobstructed.",
            "Emergency evacuation signage displayed.",
            "Staff are aware of emergency procedures.");
        AddSection(chk3, "F", "Occupational Safety", SheChecklistSectionKind.Standard,
            "Wet floor signs available and used when required.",
            "Adequate lighting provided.",
            "Adequate ventilation provided.",
            "Walkways are free from obstruction.",
            "First aid box available and fully stocked.",
            "Staff use appropriate PPE where necessary.");
        AddSection(chk3, "G", "Pest Control", SheChecklistSectionKind.Standard,
            "No evidence of rodents or insects.",
            "Pest control programme is current.",
            "Doors and windows fitted with insect screens where applicable.");
        AddSection(chk3, "H", "Water & Sanitation", SheChecklistSectionKind.Standard,
            "Potable water available.",
            "Handwashing facilities available and functional.",
            "Soap and hand-drying facilities available.",
            "Toilets are clean and hygienic.");
        AddSection(chk3, "I", "Documentation", SheChecklistSectionKind.Standard,
            "Food handlers possess valid medical certificates (where required).",
            "Cleaning schedule is available and implemented.",
            "Pest control records are available.",
            "Equipment maintenance records are available.",
            "Previous inspection findings have been addressed.");
        AddOutcome(chk3, "Excellent", null, null);
        AddOutcome(chk3, "Satisfactory", null, null);
        AddOutcome(chk3, "Requires Improvement", null, null, reinspect: 30);
        AddOutcome(chk3, "Unsatisfactory", null, null, reinspect: 7);
        AddSignatory(chk3, "Inspector", SheChecklistSignatoryKind.SystemUser);
        AddSignatory(chk3, "Cafeteria Supervisor", SheChecklistSignatoryKind.External);
        AddSignatory(chk3, "Reviewed by (SHE Supervisor)", SheChecklistSignatoryKind.SystemUser);
        }

        _context.SheInspectionChecklists.AddRange(new[] { chk1, chk2, chk3 }.Where(c => c != null)!);
        await _context.SaveChangesAsync();

        // ── Runs ──

        // Materialises a template's items onto an inspection in form order; the answer function decides each status.
        void Materialise(SafetyInspection insp, SheInspectionChecklist chk, Func<int, SheInspectionChecklistItem, SheComplianceStatus> answer)
        {
            var order = 0;
            var ordered = chk.Sections.OrderBy(s => s.DisplayOrder)
                .SelectMany(s => chk.Items.Where(i => i.Section == s).OrderBy(i => i.ItemOrder));
            foreach (var item in ordered)
            {
                var n = ++order;
                var status = answer(n, item);
                insp.Items.Add(New(new SafetyInspectionItem
                {
                    ChecklistItemId = item.Id, DisplayOrder = n, ItemDescription = item.ItemDescription, Status = status,
                    IsResolved = status is SheComplianceStatus.Compliant or SheComplianceStatus.NotApplicable,
                }));
            }
        }

        // Applies the §2.5 scoring maths to a materialised run.
        void Score(SafetyInspection insp, SheInspectionChecklist chk, SheInspectionChecklistOutcome? recommended, SheInspectionChecklistOutcome? chosen)
        {
            bool IsCritical(SafetyInspectionItem i) => chk.Items.First(c => c.Id == i.ChecklistItemId).Section?.Kind == SheChecklistSectionKind.Critical;
            var standard = insp.Items.Where(i => !IsCritical(i)).ToList();
            insp.TotalCompliantItems = standard.Count(i => i.Status == SheComplianceStatus.Compliant);
            insp.TotalNonCompliantItems = standard.Count(i => i.Status == SheComplianceStatus.NonCompliant);
            insp.TotalPartiallyCompliantItems = standard.Count(i => i.Status == SheComplianceStatus.PartiallyCompliant);
            insp.TotalApplicableItems = insp.TotalCompliantItems + insp.TotalNonCompliantItems + insp.TotalPartiallyCompliantItems;
            insp.CriticalNonConformityCount = insp.Items.Count(i => IsCritical(i) && i.Status == SheComplianceStatus.NonCompliant);
            insp.CompliancePercentage = insp.TotalApplicableItems > 0
                ? Math.Round(insp.TotalCompliantItems.Value * 100m / insp.TotalApplicableItems.Value, 2) : null;
            insp.ComplianceScore = insp.CompliancePercentage == null ? null : (int)Math.Round(insp.CompliancePercentage.Value, MidpointRounding.AwayFromZero);
            insp.RecommendedOutcomeId = recommended?.Id;
            insp.OutcomeId = chosen?.Id;
        }

        var runs = new List<SafetyInspection>();

        if (chk1 != null)
        {
        // INSP-2026-001: the general walk, one open finding — matches the corrective-action seed below.
        var insp1 = New(new SafetyInspection
        {
            InspectionNumber = "INSP-2026-001",
            InspectionDate = _today.AddDays(-7),
            LocationId = Loc(0), SpecificArea = "Production hall",
            OrganizationUnitId = Org(0),
            Type = SheInspectionType.Routine, Category = SheInspectionCategory.General,
            ChecklistId = chk1.Id,
            InspectorId = Emp(0),
            FindingsAndObservations = "Two housekeeping issues and one blocked fire exit identified.",
            RecommendedActions = "Clear obstructions; reinforce housekeeping standard.",
            Status = SheInspectionStatus.PendingCorrectiveActions,
            OverallRiskRating = SheRiskLevel.Medium,
            ComplianceDeadline = _today.AddDays(14),
            NextInspectionDueDate = _today.AddDays(10), // due within 30 days
            CompletedAt = _today.AddDays(-7).AddHours(11), CompletedById = Emp(0),
        });
        Materialise(insp1, chk1, (n, _) => n == 1 ? SheComplianceStatus.NonCompliant : SheComplianceStatus.Compliant);
        var walkway = insp1.Items.First(i => i.DisplayOrder == 1);
        walkway.DeficiencyNoted = "Pallets stored across the main walkway.";
        walkway.ActionRequired = "Relocate pallets to the racking area.";
        walkway.RiskLevel = SheRiskLevel.Medium;
        walkway.TargetDate = _today.AddDays(7);
        walkway.ResponsiblePersonId = Emp(1);
        Score(insp1, chk1, chk1.Outcomes.First(o => o.Label == "Satisfactory"), chk1.Outcomes.First(o => o.Label == "Satisfactory"));
        insp1.FieldValues.Add(New(new SafetyInspectionFieldValue { ChecklistFieldId = chk1.Fields.First(f => f.Label == "Specific area").Id, ValueText = "Production hall, bays 1-4" }));
        insp1.Signatures.Add(New(new SafetyInspectionSignature { ChecklistSignatoryId = chk1.Signatories.First(s => s.RoleLabel == "Inspector").Id, RoleLabel = "Inspector", SignedByEmployeeId = Emp(0), SignedName = _employees[0].FullName, SignedAt = _today.AddDays(-7).AddHours(11) }));
        var ih = New(new SafetyInspectionHazard
        {
            HazardDescription = "Blocked fire exit in the production hall.",
            Status = SheHazardStatus.Identified,
            InitialRiskLevel = SheHazardRiskLevel.High,
            ResidualRiskLevel = SheHazardRiskLevel.Medium,
            ReviewDueDate = _today.AddDays(14),
            OwnerId = Emp(1),
        });
        if (ca.TryGetValue("CA-HOUSE", out var house))
        {
            ih.Actions.Add(New(new SafetyInspectionHazardAction
            {
                CorrectiveActionTemplateId = house.Id,
                Status = SheCorrectiveActionStatus.InProgress,
                DueDate = _today.AddDays(2),
                AssignedToId = Emp(1),
            }));
        }
        insp1.Hazards.Add(ih);

        // INSP-2026-002: a free-form (no template) inspection — the path that predates the builder.
        var insp2 = New(new SafetyInspection
        {
            InspectionNumber = "INSP-2026-002",
            InspectionDate = _today.AddDays(-20),
            LocationId = Loc(1), SpecificArea = "Warehouse",
            Type = SheInspectionType.Planned, Category = SheInspectionCategory.Housekeeping,
            InspectorId = Emp(2),
            FindingsAndObservations = "Good standard overall.",
            PositiveObservations = "Excellent racking labelling and segregation.",
            Status = SheInspectionStatus.Completed,
            OverallRiskRating = SheRiskLevel.Low,
            ComplianceScore = 94,
            NextInspectionDueDate = _today.AddDays(5), // due within 30 days
        });
        runs.Add(insp1);
        runs.Add(insp2);
        }

        if (chk2 != null && approvedWithCa != null && vendorSig != null && officerSig != null && siteRepSig != null)
        {
        // INSP-2026-003: a completed food-vendor screening — 50 compliant, 3 non-compliant, 2 N/A → 94.34% →
        // "Approved with Corrective Actions"; no critical hits; all three signatures on the form.
        var insp3 = New(new SafetyInspection
        {
            InspectionNumber = "INSP-2026-003",
            InspectionDate = _today.AddDays(-3),
            LocationId = Loc(0), SpecificArea = "Vendor stall 4, site canteen row",
            OrganizationUnitId = Org(0),
            Type = SheInspectionType.Routine, Category = SheInspectionCategory.General,
            ChecklistId = chk2.Id,
            InspectorId = Emp(0),
            FindingsAndObservations = "Vendor documentation complete. Two hygiene lapses (hair cover, gloves) and the extinguisher tag is out of date.",
            RecommendedActions = "Replace extinguisher inspection tag; issue hair nets and gloves; re-check at next routine visit.",
            SubjectComments = "Will buy hair nets today and call the fire service contractor for the extinguisher.",
            Status = SheInspectionStatus.PendingCorrectiveActions,
            OverallRiskRating = SheRiskLevel.Low,
            ComplianceDeadline = _today.AddDays(7),
            NextInspectionDueDate = _today.AddDays(27),
            CompletedAt = _today.AddDays(-3).AddHours(10), CompletedById = Emp(0),
        });
        var nonCompliant = new HashSet<int> { 8, 13, 45 };   // hair covered · gloves · extinguisher date
        var notApplicable = new HashSet<int> { 4, 41 };      // hygiene certificate (where applicable) · LPG hose (charcoal stove)
        Materialise(insp3, chk2, (n, item) =>
            item.Section!.Kind == SheChecklistSectionKind.Critical ? SheComplianceStatus.Compliant
            : nonCompliant.Contains(n) ? SheComplianceStatus.NonCompliant
            : notApplicable.Contains(n) ? SheComplianceStatus.NotApplicable
            : SheComplianceStatus.Compliant);
        foreach (var item in insp3.Items.Where(i => i.Status == SheComplianceStatus.NonCompliant))
        {
            item.RiskLevel = item.DisplayOrder == 45 ? SheRiskLevel.Medium : SheRiskLevel.Low;
            item.TargetDate = _today.AddDays(item.DisplayOrder == 45 ? 7 : 2);
            item.ResponsiblePersonId = Emp(0);
            item.DeficiencyNoted = item.DisplayOrder switch
            {
                8 => "Hair uncovered while serving.",
                13 => "Bare hands used to plate rice.",
                _ => "Extinguisher inspection tag expired two months ago.",
            };
        }
        Score(insp3, chk2, approvedWithCa, approvedWithCa);
        SafetyInspectionFieldValue Val(string label, string? text = null, Guid? reference = null)
            => New(new SafetyInspectionFieldValue { ChecklistFieldId = chk2.Fields.First(f => f.Label == label).Id, ValueText = text, ValueReferenceId = reference });
        insp3.FieldValues.Add(Val("Site", "Community 27 Construction Site"));
        insp3.FieldValues.Add(Val("Inspection time", "09:30"));
        insp3.FieldValues.Add(Val("Vendor name", "Akosua Mensah"));
        insp3.FieldValues.Add(Val("Food", "Waakye, jollof, fried fish"));
        insp3.FieldValues.Add(Val("Stall / location", "Stall 4, canteen row"));
        insp3.FieldValues.Add(Val("Vendor contact", "024 000 0000"));
        insp3.FieldValues.Add(Val("Type of food sold", "Cooked meals"));
        insp3.FieldValues.Add(Val("SHE Officer", reference: Emp(0)));
        insp3.FieldValues.Add(Val("Inspection type", "Routine"));
        var signedAt = _today.AddDays(-3).AddHours(10);
        insp3.Signatures.Add(New(new SafetyInspectionSignature { ChecklistSignatoryId = vendorSig.Id, RoleLabel = vendorSig.RoleLabel, SignedName = "Akosua Mensah", SignedAt = signedAt }));
        insp3.Signatures.Add(New(new SafetyInspectionSignature { ChecklistSignatoryId = officerSig.Id, RoleLabel = officerSig.RoleLabel, SignedByEmployeeId = Emp(0), SignedName = _employees[0].FullName, SignedAt = signedAt }));
        insp3.Signatures.Add(New(new SafetyInspectionSignature { ChecklistSignatoryId = siteRepSig.Id, RoleLabel = siteRepSig.RoleLabel, SignedName = "K. Boateng (Site Agent)", SignedAt = signedAt.AddMinutes(20) }));
        runs.Add(insp3);
        }

        if (runs.Count > 0)
        {
            _context.SafetyInspections.AddRange(runs);
            await _context.SaveChangesAsync();
        }
    }

    // ── E. Permits-to-work ───────────────────────────────────────────────────

    private async Task SeedPermitsAsync()
    {
        if (await _context.ShePermitToWorks.AnyAsync(p => p.TenantId == _tenantId && p.PermitNumber == "PTW-2026-001"))
            return;

        var p1 = New(new ShePermitToWork
        {
            PermitNumber = "PTW-2026-001", PermitType = ShePermitType.HotWork,
            WorkDescription = "Welding repairs to handrails on the mezzanine.",
            LocationId = Loc(0), SpecificArea = "Mezzanine level",
            RequestedById = Emp(0), RequestedDate = _today.AddDays(-1),
            PlannedStartDate = _today.AddDays(-1), PlannedStartTime = new TimeSpan(8, 0, 0),
            PlannedEndDate = _today.AddDays(1), PlannedEndTime = new TimeSpan(17, 0, 0),
            ActualStartDate = _today.AddDays(-1),
            Status = ShePermitStatus.Active,
            IssuedById = Emp(1), IssuedDate = _today.AddDays(-1),
            ApprovedById = Emp(1), ApprovedDate = _today.AddDays(-1),
            HazardsIdentified = "Sparks, hot surfaces, fume.",
            ControlMeasures = "Fire watch, screens, extinguisher on standby.",
            PpeRequired = "Welding mask, leather gauntlets, FR coveralls.",
        });
        p1.AuthorisedWorkers.Add(New(new ShePermitToWorkWorker { EmployeeId = Emp(2), WorkerName = "Welder A", TradeOrRole = "Welder", Briefed = true, BriefedDate = _today.AddDays(-1), SignedOff = true, SignedDate = _today.AddDays(-1) }));
        p1.AuthorisedWorkers.Add(New(new ShePermitToWorkWorker { EmployeeId = Emp(3), WorkerName = "Fire Watch", TradeOrRole = "Fire watch", Briefed = true, BriefedDate = _today.AddDays(-1) }));

        var p2 = New(new ShePermitToWork
        {
            PermitNumber = "PTW-2026-002", PermitType = ShePermitType.ConfinedSpaceEntry,
            WorkDescription = "Inspection of the effluent sump.",
            LocationId = Loc(1), SpecificArea = "Effluent plant",
            RequestedById = Emp(1), RequestedDate = _today.AddDays(-1),
            PlannedStartDate = _today, PlannedStartTime = new TimeSpan(9, 0, 0),
            PlannedEndDate = _today, PlannedEndTime = new TimeSpan(13, 0, 0), // expiring within 1 day
            ActualStartDate = _today,
            Status = ShePermitStatus.Active,
            IssuedById = Emp(0), IssuedDate = _today,
            ApprovedById = Emp(0), ApprovedDate = _today,
            HazardsIdentified = "Oxygen deficiency, H2S.",
            ControlMeasures = "Gas testing, forced ventilation, standby attendant, tripod rescue.",
            GasTestResults = "O2 20.9%, H2S 0 ppm, LEL 0%.",
        });

        var p3 = New(new ShePermitToWork
        {
            PermitNumber = "PTW-2026-003", PermitType = ShePermitType.WorkingAtHeight,
            WorkDescription = "Roof gutter cleaning.",
            LocationId = Loc(2), SpecificArea = "Main roof",
            RequestedById = Emp(2), RequestedDate = _today.AddDays(-2),
            PlannedStartDate = _today.AddDays(-1), PlannedStartTime = new TimeSpan(8, 0, 0),
            PlannedEndDate = _today.AddDays(2), PlannedEndTime = new TimeSpan(16, 0, 0),
            Status = ShePermitStatus.Suspended,
            IssuedById = Emp(0), IssuedDate = _today.AddDays(-1),
            ApprovedById = Emp(0), ApprovedDate = _today.AddDays(-1),
            IsSuspended = true, SuspendedDate = _today, SuspendedById = Emp(1),
            SuspensionReason = "High winds — work suspended until conditions improve.",
            HazardsIdentified = "Fall from height.",
            ControlMeasures = "Harness with twin lanyard, edge protection.",
        });

        _context.ShePermitToWorks.AddRange(p1, p2, p3);
        await _context.SaveChangesAsync();
    }

    // ── F. PPE ───────────────────────────────────────────────────────────────

    private async Task SeedPpeAsync()
    {
        if (await _context.PpeTypes.AnyAsync(t => t.TenantId == _tenantId && t.Code == "PPE-HEL"))
            return;

        SheTypeRef Add(string code, string name, ShePpeCategory cat, string standard, int qty, int reorder, decimal cost)
        {
            var type = New(new PpeType
            {
                Code = code, Name = name, Category = cat, Standard = standard,
                LifespanMonths = 24, IsActive = true,
            });
            _context.PpeTypes.Add(type);
            var inv = New(new PpeInventory
            {
                PpeType = type, ItemCode = code + "-STD", Brand = "SafeGuard", Model = name,
                Size = "M", QuantityInStock = qty, MinimumStockLevel = reorder / 2, ReorderLevel = reorder,
                StorageLocation = "Central PPE store", UnitCost = cost, Supplier = "Industrial Safety Supplies Ltd",
                LastRestockDate = _today.AddDays(-40),
            });
            _context.PpeInventories.Add(inv);
            return new SheTypeRef(type);
        }

        var hel = Add("PPE-HEL", "Hard Hat", ShePpeCategory.HeadProtection, "EN 397", 60, 20, 25m);
        var glv = Add("PPE-GLV", "Cut-Resistant Gloves", ShePpeCategory.HandProtection, "EN 388", 5, 20, 8m);   // below reorder
        var boo = Add("PPE-BOO", "Safety Boots", ShePpeCategory.FootProtection, "EN ISO 20345", 45, 15, 40m);
        var gog = Add("PPE-GOG", "Safety Goggles", ShePpeCategory.EyeFaceProtection, "EN 166", 8, 15, 6m);       // below reorder
        Add("PPE-EAR", "Ear Defenders", ShePpeCategory.HearingProtection, "EN 352", 30, 10, 12m);

        await _context.SaveChangesAsync();

        _context.PpeIssuances.Add(New(new PpeIssuance
        {
            EmployeeId = Emp(0), PpeTypeId = hel.Type.Id, IssueDate = _today.AddDays(-30), Quantity = 1, Size = "L",
            ConditionWhenIssued = ShePpeCondition.New, IssuedById = Emp(1), IsReturned = false,
        }));
        _context.PpeIssuances.Add(New(new PpeIssuance
        {
            EmployeeId = Emp(2), PpeTypeId = glv.Type.Id, IssueDate = _today.AddDays(-10), Quantity = 2, Size = "M",
            ConditionWhenIssued = ShePpeCondition.New, IssuedById = Emp(1), IsReturned = false,
        }));

        _context.JobRolePpeRequirements.Add(New(new JobRolePpeRequirement
        {
            JobRoleCode = "WELDER", JobRoleName = "Welder", PpeTypeId = gog.Type.Id,
            Quantity = 1, ReplacementFrequencyMonths = 12, IsMandatory = true,
        }));
        _context.JobRolePpeRequirements.Add(New(new JobRolePpeRequirement
        {
            JobRoleCode = "WELDER", JobRoleName = "Welder", PpeTypeId = glv.Type.Id,
            Quantity = 2, ReplacementFrequencyMonths = 3, IsMandatory = true,
        }));
        _context.JobRolePpeRequirements.Add(New(new JobRolePpeRequirement
        {
            JobRoleCode = "OPERATOR", JobRoleName = "Production Operator", PpeTypeId = boo.Type.Id,
            Quantity = 1, ReplacementFrequencyMonths = 12, IsMandatory = true,
        }));

        await _context.SaveChangesAsync();
    }

    private sealed record SheTypeRef(PpeType Type);

    // ── G. Safety equipment ──────────────────────────────────────────────────

    private async Task SeedEquipmentAsync()
    {
        if (await _context.SafetyEquipment.AnyAsync(e => e.TenantId == _tenantId && e.EquipmentNumber == "EQ-2026-001"))
            return;

        var eq1 = New(new SafetyEquipment
        {
            EquipmentNumber = "EQ-2026-001", Name = "Fire Extinguisher — Main Lobby",
            Type = SheSafetyEquipmentType.FireExtinguisher,
            LocationId = LocReq(0), SpecificArea = "Reception",
            OrganizationUnitId = Org(0),
            Manufacturer = "FireMaster", Model = "CO2-5kg", SerialNumber = "FM-5K-00123",
            PurchaseDate = _today.AddYears(-2),
            RequiresRegularInspection = true, InspectionFrequencyDays = 30,
            LastInspectionDate = _today.AddDays(-20),
            NextInspectionDueDate = _today.AddDays(10),       // due within 30 days
            RequiresCertification = true,
            CertificationExpiryDate = _today.AddDays(20),      // expiring within 30 days
            Status = SheSafetyEquipmentStatus.Operational,
            ResponsiblePersonId = Emp(1),
        });
        eq1.Inspections.Add(New(new SafetyEquipmentInspection
        {
            InspectionDate = _today.AddDays(-20), InspectionType = SheInspectionType.Routine,
            InspectedById = Emp(1), Result = SheInspectionResult.Pass,
            Findings = "Gauge in green, seal intact.", NextInspectionDate = _today.AddDays(10),
        }));

        var eq2 = New(new SafetyEquipment
        {
            EquipmentNumber = "EQ-2026-002", Name = "AED — Reception",
            Type = SheSafetyEquipmentType.AED,
            LocationId = LocReq(0), SpecificArea = "Reception desk",
            Manufacturer = "HeartSave", Model = "AED-300", SerialNumber = "HS-300-0099",
            RequiresRegularInspection = true, InspectionFrequencyDays = 30,
            Status = SheSafetyEquipmentStatus.OutOfService,         // out of service
            OutOfServiceDate = _today.AddDays(-5),
            OutOfServiceReason = "Battery and pads expired; awaiting replacement.",
            ResponsiblePersonId = Emp(0),
        });

        var eq3 = New(new SafetyEquipment
        {
            EquipmentNumber = "EQ-2026-003", Name = "Eye Wash Station — Laboratory",
            Type = SheSafetyEquipmentType.EyeWashStation,
            LocationId = LocReq(1), SpecificArea = "QC laboratory",
            Manufacturer = "AquaSafe", Model = "EW-Twin",
            RequiresRegularInspection = true, InspectionFrequencyDays = 30,
            LastInspectionDate = _today.AddDays(-5),
            NextInspectionDueDate = _today.AddDays(25),       // due within 30 days
            Status = SheSafetyEquipmentStatus.Operational,
            ResponsiblePersonId = Emp(2),
        });

        _context.SafetyEquipment.AddRange(eq1, eq2, eq3);
        await _context.SaveChangesAsync();
    }

    // ── H. Contractors ───────────────────────────────────────────────────────

    private async Task<List<SheContractor>> SeedContractorsAsync()
    {
        var existing = await _context.SheContractors.Where(c => c.TenantId == _tenantId).ToListAsync();
        if (existing.Any(c => c.ContractorCode == "CON-001"))
            return existing;

        var c1 = New(new SheContractor
        {
            ContractorCode = "CON-001", CompanyName = "Apex Mechanical Services",
            TradingName = "Apex", Address = "Tema Industrial Area", Phone = "+233 30 111 2222",
            Email = "ops@apexmech.com", RegistrationNumber = "RC-558712",
            PrimaryContactName = "Kofi Mensah", PrimaryContactPhone = "+233 24 111 2222", PrimaryContactEmail = "kofi@apexmech.com",
            SheStatus = SheContractorStatus.Approved,
            PreQualificationScore = 85, PreQualificationDate = _today.AddDays(-60),
            PreQualificationExpiryDate = _today.AddDays(300), PreQualifiedById = Emp(0),
            SheConditions = "Valid PLI and a method statement required for each work order.",
            IsActive = true,
        });
        c1.Inductions.Add(New(new SheContractorInduction
        {
            WorkerName = "Yaw Boateng", WorkerIdOrPassport = "GHA-99213", Trade = "Fitter",
            InductionDate = _today.AddDays(-50), ConductedById = Emp(1), InductionPassed = true,
            InductionExpiryDate = _today.AddDays(315),
        }));
        c1.SheInspections.Add(New(new SheContractorInspection
        {
            InspectionNumber = "CINS-2026-001", InspectionDate = _today.AddDays(-15),
            LocationId = Loc(0), InspectorId = Emp(0), ComplianceScore = 88,
            Result = SheInspectionResult.Pass, Findings = "Good standard; minor housekeeping advisory.",
            Status = SheInspectionStatus.Completed,
        }));
        c1.Documents.Add(New(new SheContractorDocument
        {
            DocumentType = SheContractorDocumentType.InsuranceCertificate, FileName = "apex-pli-2026.pdf",
            FilePath = "/contractors/apex/pli-2026.pdf", Title = "Public Liability Insurance",
            DocumentDate = _today.AddDays(-60), ExpiryDate = _today.AddDays(305),
            IsVerified = true, VerifiedById = Emp(0), VerifiedDate = _today.AddDays(-58),
            UploadedDate = _today.AddDays(-60), UploadedById = Emp(0),
        }));

        var c2 = New(new SheContractor
        {
            ContractorCode = "CON-002", CompanyName = "BuildRight Construction",
            Address = "Spintex Road, Accra", Phone = "+233 30 333 4444", Email = "info@buildright.com",
            RegistrationNumber = "RC-771203",
            PrimaryContactName = "Ama Owusu", PrimaryContactPhone = "+233 20 333 4444",
            SheStatus = SheContractorStatus.ConditionalApproval,
            PreQualificationScore = 62, PreQualificationDate = _today.AddDays(-20),
            PreQualificationExpiryDate = _today.AddDays(160), PreQualifiedById = Emp(0),
            SheConditions = "Conditional on closing out the outstanding non-compliance notice.",
            IsActive = true,
        });
        c2.NonCompliances.Add(New(new SheContractorNonCompliance
        {
            NoticeNumber = "NC-2026-001", IssuedDate = _today.AddDays(-8), IssuedById = Emp(1),
            ViolationDescription = "Workers observed without fall protection while working at height.",
            Severity = SheNonComplianceSeverity.Major, RectificationDeadline = _today.AddDays(5),
            Status = SheNonComplianceStatus.Open,
        }));

        _context.SheContractors.AddRange(c1, c2);
        await _context.SaveChangesAsync();
        return new List<SheContractor> { c1, c2 };
    }

    // ── I. Training ──────────────────────────────────────────────────────────

    private async Task SeedTrainingAsync()
    {
        if (await _context.SheTrainingPlans.AnyAsync(p => p.TenantId == _tenantId && p.PlanNumber == "SHE-TP-2026"))
            return;

        var plan = New(new SheTrainingPlan
        {
            PlanNumber = "SHE-TP-2026", Title = "2026 SHE Training Plan",
            Year = _today.Year, OrganizationUnitId = Org(0),
            Status = SheTrainingPlanStatus.Active,
            PreparedById = Emp(0), PreparedDate = _today.AddDays(-90),
            ApprovedById = Emp(1), ApprovedDate = _today.AddDays(-85),
            Notes = "Annual mandatory and role-based SHE training.",
        });
        _context.SheTrainingPlans.Add(plan);
        await _context.SaveChangesAsync();

        var prg1 = New(new SheTrainingProgram
        {
            ProgramCode = "SHE-PRG-001", Title = "Fire Safety & Evacuation",
            Description = "Fire awareness and evacuation drill.",
            Category = SheTrainingCategory.FireSafetyAndEvacuation,
            PlanId = plan.Id, DeliveryMethod = SheTrainingDeliveryMethod.PracticalDrill,
            DurationMinutes = 120, ActualDate = _today.AddDays(-20),
            LocationId = Loc(0), TrainerId = Emp(1),
            Status = SheTrainingStatus.Completed, MaxParticipants = 30, ActualAttendees = 2,
            WasEvaluated = true, EvaluationSummary = "Positive feedback; evacuation time within target.",
            EvaluatedById = Emp(0), EvaluationDate = _today.AddDays(-19),
        });
        prg1.Attendances.Add(New(new SheTrainingAttendance { IsEmployee = true, EmployeeId = Emp(2), AttendanceName = "Attendee", Attended = true, SignedDate = _today.AddDays(-20), AssessmentPassed = true, AssessmentScore = 90 }));
        prg1.Attendances.Add(New(new SheTrainingAttendance { IsEmployee = true, EmployeeId = Emp(3), AttendanceName = "Attendee", Attended = true, SignedDate = _today.AddDays(-20), AssessmentPassed = true, AssessmentScore = 85 }));

        var prg2 = New(new SheTrainingProgram
        {
            ProgramCode = "SHE-PRG-002", Title = "Working at Height",
            Description = "Safe work at height and fall-arrest equipment.",
            Category = SheTrainingCategory.WorkingAtHeight,
            PlanId = plan.Id, DeliveryMethod = SheTrainingDeliveryMethod.Classroom,
            DurationMinutes = 180, ScheduledDate = _today.AddDays(14),
            LocationId = Loc(1), TrainerId = Emp(1),
            Status = SheTrainingStatus.Scheduled, MaxParticipants = 20,
        });

        _context.SheTrainingPrograms.AddRange(prg1, prg2);
        await _context.SaveChangesAsync();
    }

    // ── J. Waste ─────────────────────────────────────────────────────────────

    private async Task SeedWasteAsync(List<SheContractor> contractors)
    {
        if (await _context.SheWasteTypes.AnyAsync(t => t.TenantId == _tenantId && t.Code == "WT-GEN"))
            return;

        var gen = New(new SheWasteType { Code = "WT-GEN", Name = "General Waste", Classification = SheWasteClassification.NonHazardous, IsActive = true });
        var oil = New(new SheWasteType { Code = "WT-OIL", Name = "Used Oil", Classification = SheWasteClassification.Hazardous, RequiresManifest = true, DisposalRequirements = "Licensed hazardous-waste handler; manifest required.", RegulatoryReference = "EPA LI 1652", IsActive = true });
        var ewaste = New(new SheWasteType { Code = "WT-EW", Name = "E-Waste", Classification = SheWasteClassification.EWaste, RequiresManifest = true, IsActive = true });
        _context.SheWasteTypes.AddRange(gen, oil, ewaste);
        await _context.SaveChangesAsync();

        var contractorId = contractors.FirstOrDefault(c => c.ContractorCode == "CON-001")?.Id;

        _context.SheWasteDisposalRecords.Add(New(new SheWasteDisposalRecord
        {
            RecordNumber = "WD-2026-001", WasteTypeId = oil.Id, LocationId = Loc(1),
            GenerationArea = "Maintenance workshop", DisposalDate = _today.AddDays(-10),
            Quantity = 200, Unit = SheWasteMeasurementUnit.Litres, DisposalMethod = SheWasteDisposalMethod.Incineration,
            WasteContractorId = contractorId, ManifestNumber = "MAN-008812", DisposalSite = "Licensed treatment facility, Tema",
            RecordedById = Emp(0),
        }));
        _context.SheWasteDisposalRecords.Add(New(new SheWasteDisposalRecord
        {
            RecordNumber = "WD-2026-002", WasteTypeId = gen.Id, LocationId = Loc(0),
            GenerationArea = "Site-wide", DisposalDate = _today.AddDays(-3),
            Quantity = 500, Unit = SheWasteMeasurementUnit.Kilograms, DisposalMethod = SheWasteDisposalMethod.Landfill,
            DisposalSite = "Municipal landfill", RecordedById = Emp(1),
        }));

        await _context.SaveChangesAsync();
    }

    // ── K. Environmental ─────────────────────────────────────────────────────

    private async Task SeedEnvironmentalAsync()
    {
        if (await _context.SheEnvironmentalIncidents.AnyAsync(i => i.TenantId == _tenantId && i.IncidentNumber == "ENV-2026-001"))
            return;

        _context.SheEnvironmentalIncidents.Add(New(new SheEnvironmentalIncident
        {
            IncidentNumber = "ENV-2026-001", Type = SheEnvironmentalIncidentType.OilSpill,
            AffectedMedia = SheEnvironmentalMedia.Soil, Severity = SheIncidentSeverity.Moderate,
            IncidentDate = _today.AddDays(-6), LocationId = Loc(1), SpecificArea = "Generator yard",
            Description = "Approx. 50 litres of diesel spilled from a ruptured day-tank hose.",
            SpillVolume = "~50 litres", SubstanceInvolved = "Diesel",
            ImmediateResponseAction = "Spill kit deployed; contaminated soil excavated.",
            ReportedToEpa = true, EpaNotificationDate = _today.AddDays(-6), EpaReferenceNumber = "EPA-SPL-2026-44",
            Status = SheEnvironmentalIncidentStatus.ResponseInProgress,
            ReportedById = Emp(0), ReportedDate = _today.AddDays(-6),
        }));
        _context.SheEnvironmentalIncidents.Add(New(new SheEnvironmentalIncident
        {
            IncidentNumber = "ENV-2026-002", Type = SheEnvironmentalIncidentType.NoiseExceedance,
            AffectedMedia = SheEnvironmentalMedia.Air, Severity = SheIncidentSeverity.Minor,
            IncidentDate = _today.AddDays(-40), LocationId = Loc(0), SpecificArea = "Compressor house",
            Description = "Boundary noise briefly exceeded the permitted daytime limit.",
            Status = SheEnvironmentalIncidentStatus.Closed,
            ReportedById = Emp(2), ReportedDate = _today.AddDays(-40),
            ClosedDate = _today.AddDays(-20), ClosedById = Emp(0),
            CorrectiveActions = "Acoustic enclosure serviced; silencer replaced.",
        }));

        _context.SheEnvironmentalMonitoringRecords.Add(New(new SheEnvironmentalMonitoringRecord
        {
            RecordNumber = "EM-2026-001", MonitoringType = SheEnvironmentalMonitoringType.Noise,
            LocationId = Loc(0), MonitoringPoint = "North boundary", MeasurementDate = _today.AddDays(-5),
            MeasuredValue = 92, Unit = "dB(A)", RegulatoryLimit = 85, ActionLevel = 80,
            ExceedsLimit = true, ExceedsActionLevel = true, InstrumentUsed = "Type-1 sound level meter",
            MeasuredById = Emp(2), Comments = "Driven by the compressor house; mitigation underway.",
        }));
        _context.SheEnvironmentalMonitoringRecords.Add(New(new SheEnvironmentalMonitoringRecord
        {
            RecordNumber = "EM-2026-002", MonitoringType = SheEnvironmentalMonitoringType.AmbientDust,
            LocationId = Loc(1), MonitoringPoint = "South boundary", MeasurementDate = _today.AddDays(-5),
            MeasuredValue = 0.3m, Unit = "mg/m³", RegulatoryLimit = 0.5m, ActionLevel = 0.4m,
            ExceedsLimit = false, ExceedsActionLevel = false, MeasuredById = Emp(2),
        }));

        await _context.SaveChangesAsync();
    }

    // ── L. Occupational health ───────────────────────────────────────────────

    private async Task SeedOccupationalHealthAsync()
    {
        if (await _context.SheOccupationalHealthSurveillances.AnyAsync(s => s.TenantId == _tenantId && s.SurveillanceNumber == "OHS-2026-001"))
            return;

        _context.SheOccupationalHealthSurveillances.Add(New(new SheOccupationalHealthSurveillance
        {
            SurveillanceNumber = "OHS-2026-001", EmployeeId = Emp(2),
            Type = SheHealthSurveillanceType.Audiometry, ExposureHazard = "Workshop noise > 85 dB(A)",
            ExaminationDate = _today.AddDays(-30), NextExaminationDate = _today.AddDays(335),
            Result = SheHealthSurveillanceResult.Normal, Findings = "No threshold shift.",
            RecordedById = Emp(0),
        }));
        _context.SheOccupationalHealthSurveillances.Add(New(new SheOccupationalHealthSurveillance
        {
            SurveillanceNumber = "OHS-2026-002", EmployeeId = Emp(3),
            Type = SheHealthSurveillanceType.LungFunctionSpirometry, ExposureHazard = "Welding fume",
            ExaminationDate = _today.AddDays(-15), NextExaminationDate = _today.AddDays(350),
            Result = SheHealthSurveillanceResult.ActionRequired, Findings = "Mild reduction in FEV1.",
            Recommendations = "Review fume extraction and respirator fit; re-test in 3 months.",
            WorkRestrictionIssued = true, WorkRestrictionDetails = "Limit welding exposure pending review.",
            RecordedById = Emp(0),
        }));

        _context.SheFirstAidStations.Add(New(new SheFirstAidStation
        {
            StationCode = "FAS-001", Name = "Main Block First Aid Room", LocationId = LocReq(0),
            SpecificArea = "Ground floor", Type = SheFirstAidStationType.MedicalRoom,
            ResponsibleAiderId = Emp(1), LastInspectionDate = _today.AddDays(-10), NextInspectionDate = _today.AddDays(20),
            IsFullyStocked = true, IsActive = true,
        }));
        _context.SheFirstAidStations.Add(New(new SheFirstAidStation
        {
            StationCode = "FAS-002", Name = "Workshop First Aid Kit", LocationId = LocReq(1),
            SpecificArea = "Maintenance workshop", Type = SheFirstAidStationType.FullKit,
            ResponsibleAiderId = Emp(2), LastInspectionDate = _today.AddDays(-35), NextInspectionDate = _today.AddDays(-5),
            IsFullyStocked = false, StockingDeficiencies = "Burn dressings and eye wash low.", IsActive = true,
        }));

        _context.SheWellnessPrograms.Add(New(new SheWellnessProgram
        {
            ProgramCode = "WP-001", Title = "Annual Health Screening", Type = SheWellnessProgramType.HealthScreening,
            Description = "On-site BP, BMI and blood-sugar screening for all staff.",
            StartDate = _today.AddDays(-10), Status = SheWellnessProgramStatus.Active,
            CoordinatorId = Emp(0), ParticipantsCount = 45, IsActive = true,
        }));
        _context.SheWellnessPrograms.Add(New(new SheWellnessProgram
        {
            ProgramCode = "WP-002", Title = "Mental Health Awareness", Type = SheWellnessProgramType.MentalHealthSupport,
            Description = "Awareness sessions and an employee-assistance referral pathway.",
            StartDate = _today.AddDays(20), Status = SheWellnessProgramStatus.Planned,
            CoordinatorId = Emp(1), IsActive = true,
        }));

        await _context.SaveChangesAsync();
    }

    // ── M. Emergency ─────────────────────────────────────────────────────────

    private async Task SeedEmergencyAsync()
    {
        if (await _context.EmergencyPlans.AnyAsync(p => p.TenantId == _tenantId && p.PlanNumber == "EP-001"))
            return;

        var deptId = _departments.FirstOrDefault()?.Id;

        var ep1 = New(new EmergencyPlan
        {
            PlanNumber = "EP-001", PlanName = "Fire Emergency Response Plan", Type = SheEmergencyType.Fire,
            Description = "Response and evacuation procedure for fire emergencies across the main site.",
            Procedures = "On discovering a fire: raise the alarm, attack only if trained and safe, evacuate via the nearest route, report to the assembly point, and account for personnel.",
            LocationId = Loc(0), LastReviewed = _today.AddDays(-120), NextReviewDate = _today.AddDays(245),
            PlanOwnerId = Emp(0), IsActive = true,
        });
        ep1.AssemblyPoints.Add(New(new SheAssemblyPoint { Name = "Assembly Point A", Description = "Main car park, north end.", LocationId = Loc(0), Capacity = 200, IsActive = true }));
        ep1.AssemblyPoints.Add(New(new SheAssemblyPoint { Name = "Assembly Point B", Description = "Sports field, east gate.", LocationId = Loc(0), Capacity = 150, IsActive = true }));
        ep1.EmergencyContacts.Add(New(new EmergencyContact { Name = "Ghana National Fire Service", Role = "Fire & Rescue", PrimaryPhone = "192", IsExternal = true, DisplayOrder = 1, IsActive = true }));
        ep1.EmergencyContacts.Add(New(new EmergencyContact { Name = "Ambulance Service", Role = "Medical Emergency", PrimaryPhone = "193", IsExternal = true, DisplayOrder = 2, IsActive = true }));
        ep1.EmergencyContacts.Add(New(new EmergencyContact { Name = "Site Chief Warden", Role = "Internal Coordinator", PrimaryPhone = "+233 24 000 0001", IsExternal = false, DisplayOrder = 3, IsActive = true }));
        ep1.Drills.Add(New(new EmergencyDrill
        {
            DrillNumber = "DRILL-2026-001", DrillName = "Q1 Fire Evacuation Drill", DrillDate = _today.AddDays(-15),
            DrillTime = new TimeSpan(11, 0, 0), Scenario = "Simulated fire in the production hall.",
            LocationId = Loc(0), DepartmentId = deptId, WasAnnounced = false, ParticipantsCount = 50,
            EvacuationTime = new TimeSpan(0, 4, 30), ObjectivesMet = true,
            StrengthsIdentified = "Prompt alarm response.", AreasForImprovement = "Two staff used a non-designated route.",
            CoordinatorId = Emp(1), NextDrillScheduledDate = _today.AddDays(75),
        }));
        ep1.TeamMembers.Add(New(new EmergencyResponseTeam { EmployeeId = Emp(1), Role = "Fire Warden", Responsibilities = "Sweep and clear the ground floor.", IsActive = true }));
        ep1.TeamMembers.Add(New(new EmergencyResponseTeam { EmployeeId = Emp(2), Role = "First Aider", Responsibilities = "Triage at the assembly point.", IsActive = true }));

        var ep2 = New(new EmergencyPlan
        {
            PlanNumber = "EP-002", PlanName = "Medical Emergency Plan", Type = SheEmergencyType.MedicalEmergency,
            Description = "Procedure for serious injury or sudden illness on site.",
            Procedures = "Summon the nearest first aider, call the ambulance service, control the scene, and escort the ambulance on arrival.",
            LocationId = Loc(0), LastReviewed = _today.AddDays(-60), NextReviewDate = _today.AddDays(305),
            PlanOwnerId = Emp(0), IsActive = true,
        });

        _context.EmergencyPlans.AddRange(ep1, ep2);
        await _context.SaveChangesAsync();
    }

    // ── N. Regulatory obligations ────────────────────────────────────────────

    private async Task SeedRegulatoryObligationsAsync(Dictionary<string, SheRegulatoryBody> bodies)
    {
        if (await _context.SheRegulatoryObligations.AnyAsync(o => o.TenantId == _tenantId && o.ObligationCode == "REG-001"))
            return;

        Guid? BodyId(string name) => bodies.TryGetValue(name, out var b) ? b.Id : null;

        var o1 = New(new SheRegulatoryObligation
        {
            ObligationCode = "REG-001", Title = "Annual Factory Registration",
            Description = "Maintain a current factory registration certificate.",
            Domain = SheRegulatoryDomain.OccupationalSafety, LegislationName = "Factories, Offices and Shops Act, 1970 (Act 328)",
            SectionOrClause = "s.1", RegulatoryBodyId = BodyId("Department of Factories Inspectorate"),
            ComplianceStatus = SheComplianceStatus.Compliant, ObligationOwnerId = Emp(0),
            LastReviewedDate = _today.AddDays(-30), NextReviewDate = _today.AddDays(200), IsActive = true,
        });
        o1.EvidenceRecords.Add(New(new SheRegulatoryComplianceEvidence
        {
            EvidenceTitle = "Factory registration certificate 2026", EvidenceDate = _today.AddDays(-30),
            ExpiryDate = _today.AddDays(335), DocumentPath = "/compliance/factory-reg-2026.pdf",
            RecordedById = Emp(0),
        }));

        var o2 = New(new SheRegulatoryObligation
        {
            ObligationCode = "REG-002", Title = "EPA Environmental Permit",
            Description = "Hold a valid environmental permit for site operations.",
            Domain = SheRegulatoryDomain.EnvironmentalProtection, LegislationName = "Environmental Protection Agency Act, 1994 (Act 490)",
            RegulatoryBodyId = BodyId("Environmental Protection Agency"),
            ComplianceStatus = SheComplianceStatus.NonCompliant,
            ComplianceNotes = "Renewal application outstanding.", ObligationOwnerId = Emp(0),
            LastReviewedDate = _today.AddDays(-10), NextReviewDate = _today.AddDays(15), IsActive = true,
        });

        var o3 = New(new SheRegulatoryObligation
        {
            ObligationCode = "REG-003", Title = "Fire Certificate",
            Description = "Maintain a current fire certificate for the premises.",
            Domain = SheRegulatoryDomain.FireSafety, RegulatoryBodyId = BodyId("Ghana National Fire Service"),
            ComplianceStatus = SheComplianceStatus.PartiallyCompliant,
            ComplianceNotes = "Re-inspection scheduled following minor remedial works.",
            ObligationOwnerId = Emp(1), LastReviewedDate = _today.AddDays(-20), NextReviewDate = _today.AddDays(25), IsActive = true,
        });

        _context.SheRegulatoryObligations.AddRange(o1, o2, o3);
        await _context.SaveChangesAsync();
    }

    // ── O. Signage ───────────────────────────────────────────────────────────

    private async Task SeedSignsAsync()
    {
        if (await _context.SafetySigns.AnyAsync(s => s.TenantId == _tenantId && s.SignCode == "SGN-001"))
            return;

        _context.SafetySigns.Add(New(new SafetySign
        {
            SignCode = "SGN-001", Description = "Mandatory PPE Area", SignType = SheSafetySignType.Mandatory,
            LocationId = LocReq(0), SpecificPosition = "Production hall entrance", InstallationDate = _today.AddYears(-1),
            Material = "Photoluminescent rigid PVC", IsPhotoluminescent = true, Status = SheSafetySignStatus.Good,
            LastInspectionDate = _today.AddDays(-30), NextInspectionDate = _today.AddDays(20), IsActive = true,
        }));
        _context.SafetySigns.Add(New(new SafetySign
        {
            SignCode = "SGN-002", Description = "Fire Exit", SignType = SheSafetySignType.EmergencyEscape,
            LocationId = LocReq(0), SpecificPosition = "Above east stairwell", InstallationDate = _today.AddYears(-3),
            Material = "Photoluminescent", IsPhotoluminescent = true, Status = SheSafetySignStatus.Faded,
            LastInspectionDate = _today.AddDays(-95), NextInspectionDate = _today.AddDays(-5), // overdue inspection
            InspectionNotes = "Legend faded; schedule replacement.", IsActive = true,
        }));
        _context.SafetySigns.Add(New(new SafetySign
        {
            SignCode = "SGN-003", Description = "No Smoking", SignType = SheSafetySignType.Prohibition,
            LocationId = LocReq(1), SpecificPosition = "Fuel storage area", InstallationDate = _today.AddYears(-2),
            Material = "Aluminium", Status = SheSafetySignStatus.Good,
            LastInspectionDate = _today.AddDays(-20), NextInspectionDate = _today.AddDays(160), IsActive = true,
        }));

        await _context.SaveChangesAsync();
    }

    // ── P. Performance / KPI ─────────────────────────────────────────────────

    private async Task SeedPerformanceAsync()
    {
        if (await _context.ShePerformanceSnapshots.AnyAsync(s => s.TenantId == _tenantId && s.SnapshotNumber == "PERF-2026-Q1"))
            return;

        _context.ShePerformanceSnapshots.Add(New(new ShePerformanceSnapshot
        {
            SnapshotNumber = "PERF-2026-Q1", PeriodType = SheSnapshotPeriodType.Quarterly,
            Year = _today.Year, PeriodNumber = 1, LocationId = Loc(0),
            TotalAccidents = 2, TotalIncidents = 4, TotalNearMisses = 1, TotalDangerousOccurrences = 0,
            TotalFatalities = 0, TotalLostTimeInjuries = 1,
            LostTimeInjuryFrequencyRate = 4.0m, TotalManHoursWorked = 250000, TotalLostDays = 5,
            InspectionsPlanned = 4, InspectionsConducted = 2, InspectionsOverdue = 0,
            CorrectiveActionsIssued = 5, CorrectiveActionsCompleted = 2, CorrectiveActionsOverdue = 2,
            CorrectiveActionClosureRate = 40.0m,
            TrainingProgramsPlanned = 2, TrainingProgramsConducted = 1, TotalTrainingHours = 4,
            ContractorsOnSite = 2, ContractorInspectionsConducted = 1, ContractorNonComplianceNoticesIssued = 1,
            ContractorComplianceRate = 88.0m,
            EnvironmentalIncidents = 2, EnvironmentalIncidentsReportedToEpa = 1,
            EmergencyDrillsPlanned = 1, EmergencyDrillsConducted = 1,
            PpeComplianceRate = 92.0m, HousekeepingComplianceRating = 85.0m,
            RegulatoryObligationsTotal = 3, RegulatoryObligationsCompliant = 1,
            RegulatoryObligationsNonCompliant = 1, RegulatoryObligationsExpiringSoon = 2,
            PreparedById = Emp(0), PreparedDate = _today.AddDays(-30),
            ManagementComments = "LTIFR within target; corrective-action closure rate needs improvement.",
        }));

        await _context.SaveChangesAsync();
    }

    // ── Q. Committee & meetings ──────────────────────────────────────────────

    private async Task SeedCommitteeAsync()
    {
        if (await _context.SafetyCommittees.AnyAsync(c => c.TenantId == _tenantId && c.CommitteeName == "Central HSE Committee"))
            return;

        var committee = New(new SafetyCommittee
        {
            CommitteeName = "Central HSE Committee",
            Description = "Site-wide health, safety and environment committee.",
            EstablishedDate = _today.AddYears(-1), ChairPersonId = Emp(0),
            MeetingFrequencyDays = 30, MeetingSchedule = "Monthly — first Tuesday.", IsActive = true,
        });
        committee.Members.Add(New(new SafetyCommitteeMember { EmployeeId = Emp(0), Role = "Chairperson", JoinDate = _today.AddYears(-1), IsActive = true }));
        committee.Members.Add(New(new SafetyCommitteeMember { EmployeeId = Emp(1), Role = "Secretary", JoinDate = _today.AddYears(-1), IsActive = true }));
        committee.Members.Add(New(new SafetyCommitteeMember { EmployeeId = Emp(2), Role = "Member (Worker Rep)", JoinDate = _today.AddMonths(-6), IsActive = true }));
        _context.SafetyCommittees.Add(committee);
        await _context.SaveChangesAsync();

        var meeting = New(new SafetyMeeting
        {
            CommitteeId = committee.Id, MeetingNumber = "MTG-2026-001", MeetingDate = _today.AddDays(-15),
            StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0),
            Location = "Boardroom A", Type = SheSafetyMeetingType.CommitteeMeeting,
            Agenda = "Review of open incidents, inspection findings and the training plan.",
            Minutes = "Reviewed INC-2026-001 corrective actions; agreed to expedite machine guarding.",
            TopicsDiscussed = "Incidents; inspections; PPE stock; upcoming drills.",
            DecisionsMade = "Expedite conveyor guarding; replenish glove and goggle stock.",
            FacilitatorId = Emp(0), AttendeesCount = 3,
        });
        meeting.Attendees.Add(New(new SafetyMeetingAttendee { EmployeeId = Emp(0), Attended = true, SignedDate = _today.AddDays(-15) }));
        meeting.Attendees.Add(New(new SafetyMeetingAttendee { EmployeeId = Emp(1), Attended = true, SignedDate = _today.AddDays(-15) }));
        meeting.Attendees.Add(New(new SafetyMeetingAttendee { EmployeeId = Emp(2), Attended = true, SignedDate = _today.AddDays(-15) }));
        meeting.ActionItems.Add(New(new SafetyMeetingActionItem
        {
            ActionDescription = "Replenish cut-resistant gloves and safety goggles to reorder level.",
            Priority = SheActionItemPriority.High, Status = SheActionItemStatus.Open,
            AssignedToId = Emp(1), DueDate = _today.AddDays(-2), // overdue open action
        }));
        meeting.ActionItems.Add(New(new SafetyMeetingActionItem
        {
            ActionDescription = "Circulate the Q1 fire-drill report to all wardens.",
            Priority = SheActionItemPriority.Medium, Status = SheActionItemStatus.Completed,
            AssignedToId = Emp(0), DueDate = _today.AddDays(-8), CompletionDate = _today.AddDays(-9),
        }));

        _context.SafetyMeetings.Add(meeting);
        await _context.SaveChangesAsync();
    }

    // ── R. Return-to-work ────────────────────────────────────────────────────

    private async Task SeedReturnToWorkAsync()
    {
        if (await _context.SheReturnToWorkPlans.AnyAsync(p => p.TenantId == _tenantId && p.PlanNumber == "RTW-2026-001"))
            return;

        var incidentId = await _context.SafetyIncidents
            .Where(i => i.TenantId == _tenantId && i.IncidentNumber == "INC-2026-001")
            .Select(i => (Guid?)i.Id).FirstOrDefaultAsync();

        var plan = New(new SheReturnToWorkPlan
        {
            EmployeeId = Emp(3), SafetyIncidentId = incidentId, PlanNumber = "RTW-2026-001",
            PlanDate = _today.AddDays(-7), PlannedReturnDate = _today.AddDays(-5),
            ActualReturnDate = _today.AddDays(-5),
            MedicalRestrictions = "No heavy lifting; no repetitive gripping with the left hand for 4 weeks.",
            MedicalClearanceDate = _today.AddDays(-6), MedicalClearanceNotes = "Fit for light duties.",
            RequiresWorkplaceModifications = true,
            WorkplaceModificationsDescription = "Reassign to light packing duties on a graduated basis.",
            Status = SheReturnToWorkStatus.Active, CoordinatorId = Emp(0), SupervisorId = Emp(1),
        });
        plan.Phases.Add(New(new SheReturnToWorkPhase
        {
            PhaseName = "Phase 1 — Light Duties", PhaseNumber = 1, StartDate = _today.AddDays(-5),
            RequiresReducedHours = true, HoursPerDay = 4, DaysPerWeek = 5,
            Duties = "Light packing and quality checks at a seated station.",
            Restrictions = "No lifting above 5 kg; no use of vibrating tools.",
            AssessmentDate = _today.AddDays(-6), AssessedById = Emp(0),
            EmployeeProgress = "Coping well; no reported pain.",
        }));
        plan.Reviews.Add(New(new SheReturnToWorkReview
        {
            ReviewDate = _today.AddDays(-1), ReviewNumber = 1,
            EmployeeCondition = "Improving; grip strength returning.",
            WorkProgress = "Managing light duties without difficulty.",
            ReviewedById = Emp(0), NextReviewDate = _today.AddDays(6),
        }));

        _context.SheReturnToWorkPlans.Add(plan);
        await _context.SaveChangesAsync();
    }
}
