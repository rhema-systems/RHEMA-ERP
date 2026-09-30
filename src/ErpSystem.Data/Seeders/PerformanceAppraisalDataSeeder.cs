using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds demo data for the Performance &amp; Appraisals module (grades, KPIs, competencies,
/// settings, template, cycle, strategic/company goals, employee goals, appraisals, and a
/// mid-year review event) for an existing tenant (typically DEFAULT).
/// Idempotent — checks for the "Standard Annual Appraisal" settings profile before seeding.
/// Requires employees to already exist (run seed-hr-full first) for goal/appraisal records.
/// </summary>
public sealed class PerformanceAppraisalDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PerformanceAppraisalDataSeeder> _logger;

    public PerformanceAppraisalDataSeeder(ApplicationDbContext context, ILogger<PerformanceAppraisalDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedForDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT", cancellationToken);
        if (defaultTenant == null)
        {
            _logger.LogError("Default tenant not found. Cannot seed performance/appraisal data.");
            return 0;
        }

        return await SeedForTenantAsync(defaultTenant.Id, cancellationToken);
    }

    public async Task<int> SeedForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding Performance & Appraisal demo data for tenant {TenantId}...", tenantId);

        var existingSettings = await _context.AppraisalSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SettingsName == "Standard Annual Appraisal", cancellationToken);
        if (existingSettings != null)
        {
            _logger.LogInformation("Performance/Appraisal demo data already seeded for tenant {TenantId}. Skipping.", tenantId);
            return 0;
        }

        var totalCreated = 0;
        var now = DateTime.UtcNow;

        var employees = await _context.Employees
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.EmployeeNumber)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            _logger.LogWarning("No employees found for tenant {TenantId}. Run seed-hr-full first for employee-linked goals/appraisals. Seeding configuration-only data.", tenantId);
        }

        // 1. Grade Definitions
        var gradeData = new[]
        {
            new { Name = "Unsatisfactory", Min = 0m, Max = 40m, Rating = PerformanceRating.Unsatisfactory },
            new { Name = "Below Expectations", Min = 41m, Max = 55m, Rating = PerformanceRating.BelowExpectations },
            new { Name = "Meets Expectations", Min = 56m, Max = 75m, Rating = PerformanceRating.MeetsExpectations },
            new { Name = "Exceeds Expectations", Min = 76m, Max = 90m, Rating = PerformanceRating.ExceedsExpectations },
            new { Name = "Outstanding", Min = 91m, Max = 100m, Rating = PerformanceRating.Outstanding }
        };

        var grades = new Dictionary<string, AppraisalGradeDefinition>();
        foreach (var g in gradeData)
        {
            var grade = new AppraisalGradeDefinition
            {
                TenantId = tenantId,
                GradeName = g.Name,
                Description = $"{g.Name} performance band",
                IsActive = true,
                OverallMinScore = g.Min,
                OverallMaxScore = g.Max,
                MappedRating = g.Rating,
                CreatedAt = now
            };
            _context.AppraisalGradeDefinitions.Add(grade);
            grades[g.Name] = grade;
            totalCreated++;
        }
        await _context.SaveChangesAsync(cancellationToken);

        // 2. KPI Definitions
        var kpiData = new[]
        {
            new { Name = "Sales Target Achievement", Type = MeasurementType.PercentageTarget, Unit = "%" },
            new { Name = "Project Delivery Timeliness", Type = MeasurementType.PercentageTarget, Unit = "%" },
            new { Name = "Customer Satisfaction Score", Type = MeasurementType.NumericAbsolute, Unit = "pts" },
            new { Name = "Quality Defect Rate", Type = MeasurementType.PercentageTarget, Unit = "%" },
            new { Name = "Revenue Growth", Type = MeasurementType.PercentageTarget, Unit = "%" }
        };

        var kpis = new Dictionary<string, KpiDefinition>();
        foreach (var k in kpiData)
        {
            var kpi = new KpiDefinition
            {
                TenantId = tenantId,
                KpiName = k.Name,
                Description = $"Measures {k.Name.ToLower()}",
                MeasurementType = k.Type,
                Unit = k.Unit,
                TolerancePercent = 5m,
                IsActive = true,
                CreatedAt = now
            };
            _context.KpiDefinitions.Add(kpi);
            kpis[k.Name] = kpi;
            totalCreated++;
        }
        await _context.SaveChangesAsync(cancellationToken);

        // 3. Competencies
        var competencyNames = new[] { "Communication", "Teamwork", "Problem Solving", "Leadership", "Adaptability" };
        var competencies = new Dictionary<string, AppraisalCompetency>();
        foreach (var name in competencyNames)
        {
            var comp = new AppraisalCompetency
            {
                TenantId = tenantId,
                Code = name.Replace(" ", "").ToUpperInvariant().Substring(0, Math.Min(10, name.Replace(" ", "").Length)),
                CriteriaName = name,
                Description = $"Demonstrates strong {name.ToLower()} skills",
                RequireEvidence = false,
                IsActive = true,
                CreatedAt = now
            };
            _context.AppraisalCompetencies.Add(comp);
            competencies[name] = comp;
            totalCreated++;
        }
        await _context.SaveChangesAsync(cancellationToken);

        // 4. Appraisal Settings — the tenant's default profile when it has none yet (performance
        // closure B6/S1): the flag answers GET /default, which read "the newest profile" and so
        // crowned whatever a test run created last. At most one per tenant (a filtered unique index).
        var tenantHasDefault = await _context.AppraisalSettings
            .AnyAsync(s => s.TenantId == tenantId && s.IsDefault, cancellationToken);
        var settings = new AppraisalSettings
        {
            TenantId = tenantId,
            SettingsName = "Standard Annual Appraisal",
            IsDefault = !tenantHasDefault,
            RequireSelfEvaluation = true,
            SelfEvaluationWeight = 0.1m,
            RequirePeerReviews = true,
            PeerNominationMode = PeerNominationMode.Employee,
            MinPeerEvaluators = 2,
            MaxPeerEvaluators = 4,
            PeerReviewsAnonymous = true,
            PeerEvaluationWeight = 0.2m,
            PeerEvaluationOpenMode = PeerEvaluationOpenMode.WithSelfEval,
            RequireManagerEvaluation = true,
            ManagerEvaluationWeight = 0.7m,
            ShowSelfScoreToManager = true,
            ShowPeerScoresToManager = true,
            ShowScoreBreakdownToEmployee = true,
            RequireCalibration = true,
            RequireHRReview = true,
            HRCanModifyScores = false,
            HRReviewTiming = HRReviewTiming.AfterCalibration,
            RequireEmployeeAcknowledgment = true,
            AllowEmployeeResponse = true,
            AllowAcknowledgmentWithoutConversation = false,
            EnableAppeals = true,
            AppealWindowDays = 7,
            AppealReevaluationWindowDays = 5,
            RequireGoalSetting = true,
            RequireManagerGoalApproval = true,
            MaxGoalsPerEmployee = 6,
            MinGoalsPerEmployee = 3,
            EnableCheckIns = true,
            EnablePrivateJournal = true,
            RequireKickOffConversation = true,
            RequireMidYearConversation = true,
            RequireFinalConversation = true,
            ReviewFrequency = ReviewFrequency.MidYearOnly,
            InterimReviewDepth = InterimReviewDepth.LightTouch,
            RequireMidYearSelfAssessment = true,
            RequireGoalProgressUpdateAtReview = true,
            AutoLockOnDeadline = false,
            CreatedAt = now
        };
        _context.AppraisalSettings.Add(settings);
        totalCreated++;
        await _context.SaveChangesAsync(cancellationToken);

        // 5. Template with sections + items + grade ranges
        var template = new AppraisalTemplate
        {
            TenantId = tenantId,
            TemplateName = "Standard Employee Template 2026",
            Description = "Default template covering KPIs and core competencies",
            IsActive = true,
            ApprovalStatus = TemplateApprovalStatus.Approved,
            ApprovalDate = now,
            CreatedAt = now
        };
        _context.AppraisalTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        totalCreated++;

        var kpiSection = new AppraisalTemplateSection
        {
            TenantId = tenantId,
            AppraisalTemplateId = template.Id,
            SectionName = "Key Performance Indicators",
            Description = "Quantitative KPI-based measures",
            DisplayOrder = 1,
            Weight = 60,
            CreatedAt = now
        };
        var competencySection = new AppraisalTemplateSection
        {
            TenantId = tenantId,
            AppraisalTemplateId = template.Id,
            SectionName = "Core Competencies",
            Description = "Qualitative behavioral/soft-skill measures",
            DisplayOrder = 2,
            Weight = 40,
            CreatedAt = now
        };
        _context.AppraisalTemplateSections.AddRange(kpiSection, competencySection);
        await _context.SaveChangesAsync(cancellationToken);
        totalCreated += 2;

        var templateItems = new List<AppraisalTemplateItem>
        {
            new()
            {
                TenantId = tenantId,
                AppraisalTemplateSectionId = kpiSection.Id,
                KpiDefinitionId = kpis["Sales Target Achievement"].Id,
                KpiTargetValue = 100m,
                KpiMinValue = 0m,
                KpiMaxValue = 150m,
                DisplayOrder = 1,
                Weight = 50,
                CreatedAt = now
            },
            new()
            {
                TenantId = tenantId,
                AppraisalTemplateSectionId = kpiSection.Id,
                KpiDefinitionId = kpis["Project Delivery Timeliness"].Id,
                KpiTargetValue = 100m,
                KpiMinValue = 0m,
                KpiMaxValue = 120m,
                DisplayOrder = 2,
                Weight = 50,
                CreatedAt = now
            },
            new()
            {
                TenantId = tenantId,
                AppraisalTemplateSectionId = competencySection.Id,
                CompetencyId = competencies["Communication"].Id,
                DisplayOrder = 1,
                Weight = 50,
                CreatedAt = now
            },
            new()
            {
                TenantId = tenantId,
                AppraisalTemplateSectionId = competencySection.Id,
                CompetencyId = competencies["Teamwork"].Id,
                DisplayOrder = 2,
                Weight = 50,
                CreatedAt = now
            }
        };
        _context.AppraisalTemplateItems.AddRange(templateItems);
        await _context.SaveChangesAsync(cancellationToken);
        totalCreated += templateItems.Count;

        foreach (var item in templateItems)
        {
            foreach (var g in gradeData)
            {
                _context.TemplateItemGradeRanges.Add(new TemplateItemGradeRange
                {
                    TenantId = tenantId,
                    AppraisalTemplateItemId = item.Id,
                    GradeDefinitionId = grades[g.Name].Id,
                    LowScore = (int)g.Min,
                    HighScore = (int)g.Max,
                    CreatedAt = now
                });
                totalCreated++;
            }
        }
        await _context.SaveChangesAsync(cancellationToken);

        // 6. Appraisal Cycle (the 2026 annual cycle, open since January). Open, not InProgress: that status
        // is gone (performance closure D-14), and this seeder was its only writer.
        var cycle = new AppraisalCycle
        {
            TenantId = tenantId,
            CycleCode = "APC2026",
            CycleName = "Annual Performance Cycle 2026",
            Year = 2026,
            AppraisalType = AppraisalType.Annual,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            AppraisalSettingsId = settings.Id,
            Status = AppraisalCycleStatus.Open,
            GoalSettingOpenDate = new DateOnly(2026, 1, 5),
            GoalSettingDeadline = new DateOnly(2026, 1, 31),
            MidYearOpenDate = new DateOnly(2026, 6, 1),
            MidYearDeadline = new DateOnly(2026, 6, 30),
            PeerNominationDeadline = new DateOnly(2026, 11, 10),
            SelfEvaluationOpenDate = new DateOnly(2026, 11, 1),
            SelfEvaluationDeadline = new DateOnly(2026, 11, 15),
            PeerEvaluationOpenDate = new DateOnly(2026, 11, 1),
            PeerEvaluationDeadline = new DateOnly(2026, 11, 20),
            ManagerEvaluationOpenDate = new DateOnly(2026, 11, 21),
            ManagerEvaluationDeadline = new DateOnly(2026, 12, 5),
            CalibrationOpenDate = new DateOnly(2026, 12, 6),
            CalibrationDeadline = new DateOnly(2026, 12, 12),
            HRReviewOpenDate = new DateOnly(2026, 12, 13),
            HRReviewDeadline = new DateOnly(2026, 12, 18),
            EmployeeAcknowledgeDeadline = new DateOnly(2026, 12, 24),
            FinalConversationDeadline = new DateOnly(2026, 12, 24),
            OpenedDate = new DateTime(2026, 1, 2),
            CreatedAt = now
        };
        _context.AppraisalCycles.Add(cycle);
        await _context.SaveChangesAsync(cancellationToken);
        totalCreated++;

        _context.AppraisalCycleTemplates.Add(new AppraisalCycleTemplate
        {
            TenantId = tenantId,
            AppraisalCycleId = cycle.Id,
            AppraisalTemplateId = template.Id,
            Priority = 10,
            IsActive = true,
            CreatedAt = now
        });
        totalCreated++;

        // 7. Strategic & Company Goals
        var strategicGoal = new StrategicGoal
        {
            TenantId = tenantId,
            Title = "Operational Excellence 2026-2028",
            Description = "Multi-year drive to improve efficiency, quality, and customer experience",
            SuccessCriteria = "Sustained YoY improvement across cost, quality, and CSAT metrics",
            Priority = GoalPriority.High,
            StartYear = 2026,
            EndYear = 2028,
            IsActive = true,
            CreatedAt = now
        };
        _context.StrategicGoals.Add(strategicGoal);
        await _context.SaveChangesAsync(cancellationToken);
        totalCreated++;

        var companyGoals = new List<CompanyGoal>
        {
            new()
            {
                TenantId = tenantId,
                AppraisalCycleId = cycle.Id,
                StrategicGoalId = strategicGoal.Id,
                Title = "Improve Customer Satisfaction by 15%",
                Description = "Raise overall CSAT score across all customer-facing teams",
                SuccessCriteria = "CSAT survey average improves by 15% over 2025 baseline",
                Priority = GoalPriority.High,
                TargetValue = 15m,
                Unit = "%",
                DueDate = new DateOnly(2026, 12, 31),
                IsVisible = true,
                CreatedAt = now
            },
            new()
            {
                TenantId = tenantId,
                AppraisalCycleId = cycle.Id,
                StrategicGoalId = strategicGoal.Id,
                Title = "Reduce Operational Costs by 10%",
                Description = "Identify and eliminate inefficiencies across departments",
                SuccessCriteria = "Operating cost per unit reduced by 10% vs 2025",
                Priority = GoalPriority.Critical,
                TargetValue = 10m,
                Unit = "%",
                DueDate = new DateOnly(2026, 12, 31),
                IsVisible = true,
                CreatedAt = now
            }
        };
        _context.CompanyGoals.AddRange(companyGoals);
        await _context.SaveChangesAsync(cancellationToken);
        totalCreated += companyGoals.Count;

        if (employees.Count == 0)
        {
            _logger.LogInformation("Performance/Appraisal configuration data seeding completed ({Count} records). No employees available for goals/appraisals.", totalCreated);
            return totalCreated;
        }

        // 8. Per-employee appraisals + goals + a mid-year review event
        var statusCycle = new[] { AppraisalStatus.Active, AppraisalStatus.Active, AppraisalStatus.Draft, AppraisalStatus.Active, AppraisalStatus.Active };
        for (var i = 0; i < employees.Count; i++)
        {
            var employee = employees[i];
            var status = statusCycle[i % statusCycle.Length];

            var appraisal = new PerformanceAppraisal
            {
                TenantId = tenantId,
                AppraisalCycleId = cycle.Id,
                AppraisalNumber = $"APR-2026-{i + 1:000}",
                EmployeeId = employee.Id,
                AppraisalTemplateId = template.Id,
                Year = 2026,
                StartDate = cycle.StartDate,
                EndDate = cycle.EndDate,
                Status = status,
                PeerEvaluatorsCount = 0,
                CreatedAt = now
            };
            _context.PerformanceAppraisals.Add(appraisal);
            await _context.SaveChangesAsync(cancellationToken);
            totalCreated++;

            var goal1 = new EmployeeGoal
            {
                TenantId = tenantId,
                EmployeeId = employee.Id,
                AppraisalCycleId = cycle.Id,
                PerformanceAppraisalId = appraisal.Id,
                CompanyGoalId = companyGoals[i % companyGoals.Count].Id,
                Title = $"Contribute to: {companyGoals[i % companyGoals.Count].Title}",
                Description = "Departmental contribution toward the company-wide goal",
                SuccessCriteria = "Achieve assigned quarterly milestones",
                Weight = 50,
                Priority = GoalPriority.High,
                Status = GoalStatus.Approved,
                MeasurementType = MeasurementType.PercentageTarget,
                Period = GoalPeriod.FullCycle,
                TargetValue = 100m,
                MinValue = 0m,
                MaxValue = 120m,
                Unit = "%",
                StartDate = cycle.StartDate,
                DueDate = cycle.EndDate,
                ProgressPercent = 55m,
                ApprovalDate = new DateTime(2026, 1, 20),
                IsLocked = true,
                LockedDate = new DateTime(2026, 2, 1),
                CreatedAt = now
            };

            var goal2 = new EmployeeGoal
            {
                TenantId = tenantId,
                EmployeeId = employee.Id,
                AppraisalCycleId = cycle.Id,
                PerformanceAppraisalId = appraisal.Id,
                KpiDefinitionId = kpis["Sales Target Achievement"].Id,
                Title = "Achieve Sales Target Milestones",
                Description = "Meet or exceed assigned sales targets for the year",
                SuccessCriteria = "Reach 100% of allocated sales target",
                Weight = 50,
                Priority = GoalPriority.Medium,
                Status = GoalStatus.Approved,
                MeasurementType = MeasurementType.PercentageTarget,
                Period = GoalPeriod.FullCycle,
                TargetValue = 100m,
                MinValue = 0m,
                MaxValue = 150m,
                Unit = "%",
                StartDate = cycle.StartDate,
                DueDate = cycle.EndDate,
                ProgressPercent = 48m,
                ApprovalDate = new DateTime(2026, 1, 20),
                IsLocked = true,
                LockedDate = new DateTime(2026, 2, 1),
                CreatedAt = now
            };

            _context.EmployeeGoals.AddRange(goal1, goal2);
            await _context.SaveChangesAsync(cancellationToken);
            totalCreated += 2;

            _context.AppraisalReviewEvents.Add(new AppraisalReviewEvent
            {
                TenantId = tenantId,
                AppraisalCycleId = cycle.Id,
                PerformanceAppraisalId = appraisal.Id,
                Type = ReviewEventType.MidYearReview,
                EventDate = new DateOnly(2026, 6, 30),
                Status = AppraisalReviewStatus.Completed,
                IsLightTouch = true,
                IsFullAppraisal = false,
                AchievementsSummary = "On track against assigned goals with steady mid-year progress.",
                ChallengesSummary = "Some resourcing constraints affecting delivery timelines.",
                ManagerNotes = "Discussed progress and agreed on focus areas for H2.",
                CreatedAt = now
            });
            totalCreated++;
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Performance/Appraisal demo data seeding completed. {Count} records created for tenant {TenantId}.", totalCreated, tenantId);
        return totalCreated;
    }
}
