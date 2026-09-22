using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds a coherent Orientation demo dataset for the DEFAULT tenant.
/// Cross-module references (employees, organization units) are resolved from the
/// database at run time — no hard-coded IDs. Idempotent: keyed on natural codes,
/// safe to run repeatedly.
/// </summary>
public class OrientationDataSeeder
{
    private static readonly Guid DefaultTenantIdFallback = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string SeedUser = "system-seed";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrientationDataSeeder> _logger;

    public OrientationDataSeeder(ApplicationDbContext context, ILogger<OrientationDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting Orientation demo data seeding...");

        var tenantId = await ResolveDefaultTenantIdAsync();
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("Default tenant not found; skipping Orientation seeding.");
            return;
        }

        var employees = await _context.Employees
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .ToListAsync();

        if (employees.Count == 0)
        {
            _logger.LogWarning("No employees in DEFAULT tenant; run HR seeding first. Skipping Orientation seeding.");
            return;
        }

        var orgUnits = await _context.OrganizationUnits
            .Where(o => o.TenantId == tenantId && !o.IsDeleted)
            .ToListAsync();

        var hrUnit = orgUnits.FirstOrDefault(o => o.Name.Contains("Human Resource", StringComparison.OrdinalIgnoreCase)
                                                || o.Name.Contains("HR", StringComparison.OrdinalIgnoreCase))
                     ?? orgUnits.FirstOrDefault();

        // ── Categories ───────────────────────────────────────────────────────────
        var onboardingCat = await GetOrCreateCategoryAsync(tenantId, "Onboarding", "New-hire onboarding programs.", 1);
        var complianceCat = await GetOrCreateCategoryAsync(tenantId, "Compliance & Regulatory", "Mandatory compliance awareness.", 2);
        var productCat = await GetOrCreateCategoryAsync(tenantId, "Product & Launches", "Product and service rollouts.", 3);

        // ── Programs ─────────────────────────────────────────────────────────────
        var onboarding = await GetOrCreateProgramAsync(tenantId, "ORI-ONB-001", "New Employee Onboarding",
            onboardingCat.Id, OrientationProgramType.Onboarding, OrientationDeliveryMode.Blended,
            OrientationPriority.High, OrientationAudienceScope.NewHires, hrUnit?.Id, employees[0].Id,
            estimatedMinutes: 240, requiresAssessment: true, passingScore: 70, requiresAck: true,
            isCertificate: true, certValidityMonths: 24, deadlineDays: 30,
            description: "Everything a new joiner needs in their first month: company overview, policies, systems and a knowledge check.",
            objectives: "Understand company culture, complete mandatory policy acknowledgements, and pass the onboarding knowledge check.");

        var antiHarassment = await GetOrCreateProgramAsync(tenantId, "ORI-CMP-001", "Anti-Harassment & Code of Conduct",
            complianceCat.Id, OrientationProgramType.Compliance, OrientationDeliveryMode.SelfPacedOnline,
            OrientationPriority.Mandatory, OrientationAudienceScope.AllEmployees, hrUnit?.Id, employees[0].Id,
            estimatedMinutes: 60, requiresAssessment: true, passingScore: 80, requiresAck: true,
            isCertificate: true, certValidityMonths: 12, deadlineDays: 14,
            description: "Annual mandatory training on workplace conduct, anti-harassment policy and reporting channels.",
            objectives: "Recognise unacceptable conduct, understand reporting channels, and acknowledge the code of conduct.");

        var productLaunch = await GetOrCreateProgramAsync(tenantId, "ORI-PRD-001", "Q3 Product Launch Briefing",
            productCat.Id, OrientationProgramType.ProductLaunch, OrientationDeliveryMode.VirtualInstructor,
            OrientationPriority.Medium, OrientationAudienceScope.Role, hrUnit?.Id, employees[0].Id,
            estimatedMinutes: 90, requiresAssessment: false, passingScore: null, requiresAck: false,
            isCertificate: false, certValidityMonths: null, deadlineDays: 21,
            description: "Briefing for the customer-facing teams on the new Q3 product line, positioning and pricing.",
            objectives: "Be able to articulate the new product value proposition and answer common customer questions.");

        // ── Modules + content (onboarding) ───────────────────────────────────────
        await SeedOnboardingContentAsync(tenantId, onboarding);
        await SeedComplianceContentAsync(tenantId, antiHarassment);

        // ── Assessment (onboarding + compliance) ─────────────────────────────────
        await SeedOnboardingAssessmentAsync(tenantId, onboarding);
        await SeedComplianceAssessmentAsync(tenantId, antiHarassment);

        // ── Audience rules + prerequisite ────────────────────────────────────────
        await SeedAudienceRulesAsync(tenantId, onboarding, antiHarassment);
        await SeedPrerequisiteAsync(tenantId, program: antiHarassment, prerequisite: onboarding);

        // ── Session + facilitators (product launch, instructor-led) ──────────────
        var session = await SeedSessionAsync(tenantId, productLaunch, employees);

        // ── Enrollments + runtime artifacts ──────────────────────────────────────
        await SeedEnrollmentsAsync(tenantId, onboarding, antiHarassment, productLaunch, session, employees);

        // ── Notifications ────────────────────────────────────────────────────────
        await SeedNotificationsAsync(tenantId, onboarding, antiHarassment, employees);

        _logger.LogInformation("Orientation demo data seeding completed.");
    }

    // ── Categories ───────────────────────────────────────────────────────────────

    private async Task<OrientationCategory> GetOrCreateCategoryAsync(Guid tenantId, string name, string description, int order)
    {
        var existing = await _context.OrientationCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name == name);
        if (existing != null) return existing;

        var category = new OrientationCategory
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
            DisplayOrder = order,
            IsActive = true,
            CreatedBy = SeedUser,
        };
        _context.OrientationCategories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    // ── Programs ───────────────────────────────────────────────────────────────

    private async Task<OrientationProgram> GetOrCreateProgramAsync(
        Guid tenantId, string code, string title, Guid categoryId,
        OrientationProgramType type, OrientationDeliveryMode mode, OrientationPriority priority,
        OrientationAudienceScope scope, Guid? ownerUnitId, Guid ownerEmployeeId,
        int estimatedMinutes, bool requiresAssessment, decimal? passingScore, bool requiresAck,
        bool isCertificate, int? certValidityMonths, int deadlineDays,
        string description, string objectives)
    {
        var existing = await _context.OrientationPrograms
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProgramCode == code);
        if (existing != null) return existing;

        var program = new OrientationProgram
        {
            TenantId = tenantId,
            ProgramCode = code,
            Title = title,
            Description = description,
            Objectives = objectives,
            CategoryId = categoryId,
            ProgramType = type,
            DefaultDeliveryMode = mode,
            Status = OrientationProgramStatus.Active,
            Priority = priority,
            AudienceScope = scope,
            EstimatedDurationMinutes = estimatedMinutes,
            RequiresAssessment = requiresAssessment,
            PassingScorePercent = passingScore,
            RequiresAcknowledgement = requiresAck,
            CompletionDeadlineDays = deadlineDays,
            IsCertificateIssued = isCertificate,
            CertificateValidityMonths = certValidityMonths,
            EnableReminders = true,
            Version = "v1.0",
            EffectiveFrom = DateTime.UtcNow.AddMonths(-2),
            OwnerOrganizationUnitId = ownerUnitId,
            OwnerEmployeeId = ownerEmployeeId,
            CreatedBy = SeedUser,
        };
        _context.OrientationPrograms.Add(program);
        await _context.SaveChangesAsync();
        return program;
    }

    // ── Content ────────────────────────────────────────────────────────────────

    private async Task SeedOnboardingContentAsync(Guid tenantId, OrientationProgram program)
    {
        if (await _context.OrientationModules.AnyAsync(m => m.ProgramId == program.Id)) return;

        var welcome = AddModule(tenantId, program.Id, "Welcome & Company Overview", OrientationModuleType.InformationContent, 1, 60);
        var policies = AddModule(tenantId, program.Id, "Policies & Compliance", OrientationModuleType.Acknowledgement, 2, 60);
        var systems = AddModule(tenantId, program.Id, "Tools & Systems", OrientationModuleType.VideoLesson, 3, 60);
        var quiz = AddModule(tenantId, program.Id, "Knowledge Check", OrientationModuleType.Assessment, 4, 60);
        await _context.SaveChangesAsync();

        AddContent(tenantId, welcome.Id, "CEO Welcome Message", OrientationContentType.Video, 1, "/orientation/onboarding/ceo-welcome.mp4", durationSeconds: 420);
        AddContent(tenantId, welcome.Id, "Company History & Values", OrientationContentType.Presentation, 2, "/orientation/onboarding/history.pptx");
        AddContent(tenantId, policies.Id, "Employee Handbook", OrientationContentType.PDF, 1, "/orientation/onboarding/handbook.pdf");
        AddContent(tenantId, policies.Id, "Code of Conduct", OrientationContentType.PDF, 2, "/orientation/onboarding/code-of-conduct.pdf");
        AddContent(tenantId, systems.Id, "Email & Collaboration Tools", OrientationContentType.Video, 1, "/orientation/onboarding/tools.mp4", durationSeconds: 600);
        AddContent(tenantId, quiz.Id, "Onboarding Knowledge Check", OrientationContentType.Quiz, 1, null);
        await _context.SaveChangesAsync();
    }

    private async Task SeedComplianceContentAsync(Guid tenantId, OrientationProgram program)
    {
        if (await _context.OrientationModules.AnyAsync(m => m.ProgramId == program.Id)) return;

        var module = AddModule(tenantId, program.Id, "Anti-Harassment Essentials", OrientationModuleType.VideoLesson, 1, 45);
        await _context.SaveChangesAsync();

        AddContent(tenantId, module.Id, "What Constitutes Harassment", OrientationContentType.Video, 1, "/orientation/compliance/harassment-intro.mp4", durationSeconds: 900);
        AddContent(tenantId, module.Id, "Reporting Channels & Protections", OrientationContentType.Document, 2, "/orientation/compliance/reporting.pdf");
        await _context.SaveChangesAsync();
    }

    private OrientationModule AddModule(Guid tenantId, Guid programId, string title, OrientationModuleType type, int order, int minutes)
    {
        var module = new OrientationModule
        {
            TenantId = tenantId,
            ProgramId = programId,
            Title = title,
            SequenceOrder = order,
            ModuleType = type,
            EstimatedDurationMinutes = minutes,
            IsSequentiallyRequired = true,
            IsActive = true,
            CreatedBy = SeedUser,
        };
        _context.OrientationModules.Add(module);
        return module;
    }

    private void AddContent(Guid tenantId, Guid moduleId, string title, OrientationContentType type, int order, string? url, int? durationSeconds = null)
    {
        _context.OrientationContentItems.Add(new OrientationContentItem
        {
            TenantId = tenantId,
            ModuleId = moduleId,
            Title = title,
            ContentType = type,
            ResourceUrl = url,
            MediaDurationSeconds = durationSeconds,
            SequenceOrder = order,
            IsRequired = true,
            IsActive = true,
            CreatedBy = SeedUser,
        });
    }

    // ── Assessment ───────────────────────────────────────────────────────────────

    private async Task SeedOnboardingAssessmentAsync(Guid tenantId, OrientationProgram program)
    {
        if (await _context.OrientationAssessmentQuestions.AnyAsync(q => q.ProgramId == program.Id)) return;

        var q1 = AddQuestion(tenantId, program.Id, "Where can you find the official leave policy?", OrientationQuestionType.SingleChoice, 1,
            "The Employee Handbook is the source of truth for all HR policies.");
        var q2 = AddQuestion(tenantId, program.Id, "Which of the following are company core values? (select all that apply)", OrientationQuestionType.MultiSelect, 2,
            "Integrity, Customer Focus and Collaboration are our three core values.");
        var q3 = AddQuestion(tenantId, program.Id, "New employees must complete onboarding within 30 days.", OrientationQuestionType.TrueFalse, 3,
            "Onboarding has a 30-day completion deadline.");
        await _context.SaveChangesAsync();

        AddOption(tenantId, q1.Id, "The Employee Handbook", true, 1);
        AddOption(tenantId, q1.Id, "The company website footer", false, 2);
        AddOption(tenantId, q1.Id, "Ask a colleague", false, 3);

        AddOption(tenantId, q2.Id, "Integrity", true, 1);
        AddOption(tenantId, q2.Id, "Customer Focus", true, 2);
        AddOption(tenantId, q2.Id, "Collaboration", true, 3);
        AddOption(tenantId, q2.Id, "Secrecy", false, 4);

        AddOption(tenantId, q3.Id, "True", true, 1);
        AddOption(tenantId, q3.Id, "False", false, 2);
        await _context.SaveChangesAsync();
    }

    private async Task SeedComplianceAssessmentAsync(Guid tenantId, OrientationProgram program)
    {
        if (await _context.OrientationAssessmentQuestions.AnyAsync(q => q.ProgramId == program.Id)) return;

        var q1 = AddQuestion(tenantId, program.Id, "Harassment can only occur between a manager and a subordinate.", OrientationQuestionType.TrueFalse, 1,
            "Harassment can occur between any colleagues regardless of reporting line.");
        await _context.SaveChangesAsync();

        AddOption(tenantId, q1.Id, "True", false, 1);
        AddOption(tenantId, q1.Id, "False", true, 2);
        await _context.SaveChangesAsync();
    }

    private OrientationAssessmentQuestion AddQuestion(Guid tenantId, Guid programId, string text, OrientationQuestionType type, int order, string explanation)
    {
        var question = new OrientationAssessmentQuestion
        {
            TenantId = tenantId,
            ProgramId = programId,
            QuestionText = text,
            QuestionType = type,
            Points = 1,
            Explanation = explanation,
            SequenceOrder = order,
            IsActive = true,
            CreatedBy = SeedUser,
        };
        _context.OrientationAssessmentQuestions.Add(question);
        return question;
    }

    private void AddOption(Guid tenantId, Guid questionId, string text, bool isCorrect, int order)
    {
        _context.OrientationAssessmentOptions.Add(new OrientationAssessmentOption
        {
            TenantId = tenantId,
            QuestionId = questionId,
            OptionText = text,
            IsCorrect = isCorrect,
            DisplayOrder = order,
            CreatedBy = SeedUser,
        });
    }

    // ── Audience rules + prerequisite ────────────────────────────────────────────

    private async Task SeedAudienceRulesAsync(Guid tenantId, OrientationProgram onboarding, OrientationProgram compliance)
    {
        if (!await _context.OrientationAudienceRules.AnyAsync(r => r.ProgramId == onboarding.Id))
        {
            _context.OrientationAudienceRules.Add(new OrientationAudienceRule
            {
                TenantId = tenantId,
                ProgramId = onboarding.Id,
                RuleName = "All new hires on joining",
                Description = "Auto-enrol every new employee 1 day after their hire date.",
                TargetType = HrAudienceTargetType.AllEmployees,
                Population = OrientationAudiencePopulation.NewHires,
                Trigger = OrientationEnrollmentTrigger.OnHire,
                EnrollmentDelayDays = 1,
                IsInclusive = true,
                IsActive = true,
                CreatedBy = SeedUser,
            });
        }

        if (!await _context.OrientationAudienceRules.AnyAsync(r => r.ProgramId == compliance.Id))
        {
            _context.OrientationAudienceRules.Add(new OrientationAudienceRule
            {
                TenantId = tenantId,
                ProgramId = compliance.Id,
                RuleName = "All employees annually",
                Description = "Enrol all employees for the annual compliance refresh, by hand: press Enrol audience now on the programme.",
                TargetType = HrAudienceTargetType.AllEmployees,
                // ⚠ Manual, not Scheduled (round 4 lane I). Since rules fire, a scheduled rule on
                // everyone is a tenant-wide enrolment on the first night — it put all 1,135 active
                // UAT employees on this programme in three seconds. A demo seed must not do that
                // unasked; HR runs it deliberately, with the preview, from the programme's rules tab.
                Trigger = OrientationEnrollmentTrigger.Manual,
                EnrollmentDelayDays = 0,
                IsInclusive = true,
                IsActive = true,
                CreatedBy = SeedUser,
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedPrerequisiteAsync(Guid tenantId, OrientationProgram program, OrientationProgram prerequisite)
    {
        if (await _context.OrientationPrerequisites.AnyAsync(p => p.ProgramId == program.Id && p.PrerequisiteProgramId == prerequisite.Id))
            return;

        _context.OrientationPrerequisites.Add(new OrientationPrerequisite
        {
            TenantId = tenantId,
            ProgramId = program.Id,
            PrerequisiteProgramId = prerequisite.Id,
            IsMandatory = false,
            Notes = "Recommended to complete onboarding before the compliance module.",
            CreatedBy = SeedUser,
        });
        await _context.SaveChangesAsync();
    }

    // ── Session + facilitators ───────────────────────────────────────────────────

    private async Task<OrientationSession> SeedSessionAsync(Guid tenantId, OrientationProgram program, List<Employee> employees)
    {
        var existing = await _context.OrientationSessions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SessionCode == "OSN-PRD-001");
        if (existing != null) return existing;

        var session = new OrientationSession
        {
            TenantId = tenantId,
            ProgramId = program.Id,
            SessionCode = "OSN-PRD-001",
            Title = "Q3 Product Launch — Live Briefing",
            Description = "Live virtual briefing for customer-facing teams.",
            DeliveryMode = OrientationDeliveryMode.VirtualInstructor,
            Status = OrientationSessionStatus.EnrollmentOpen,
            ScheduledStartAt = DateTime.UtcNow.AddDays(7).Date.AddHours(10),
            ScheduledEndAt = DateTime.UtcNow.AddDays(7).Date.AddHours(11).AddMinutes(30),
            VirtualMeetingUrl = "https://meet.example.com/q3-launch",
            MaxParticipants = 50,
            EnrollmentDeadlineAt = DateTime.UtcNow.AddDays(6),
            AllowWaitlist = true,
            ParticipantInstructions = "Join 5 minutes early. The recording will be shared afterwards.",
            CreatedBy = SeedUser,
        };
        _context.OrientationSessions.Add(session);
        await _context.SaveChangesAsync();

        _context.OrientationSessionFacilitators.Add(new OrientationSessionFacilitator
        {
            TenantId = tenantId,
            SessionId = session.Id,
            EmployeeId = employees[0].Id,
            Role = OrientationFacilitatorRole.Lead,
            HasConfirmed = true,
            CreatedBy = SeedUser,
        });
        if (employees.Count > 1)
        {
            _context.OrientationSessionFacilitators.Add(new OrientationSessionFacilitator
            {
                TenantId = tenantId,
                SessionId = session.Id,
                EmployeeId = employees[1].Id,
                Role = OrientationFacilitatorRole.SubjectMatterExpert,
                HasConfirmed = false,
                CreatedBy = SeedUser,
            });
        }
        await _context.SaveChangesAsync();
        return session;
    }

    // ── Enrollments + runtime artifacts ──────────────────────────────────────────

    private async Task SeedEnrollmentsAsync(
        Guid tenantId, OrientationProgram onboarding, OrientationProgram compliance,
        OrientationProgram productLaunch, OrientationSession session, List<Employee> employees)
    {
        if (await _context.EmployeeOrientations.AnyAsync(e => e.ProgramId == onboarding.Id))
            return;

        var now = DateTime.UtcNow;

        // 1) A COMPLETED onboarding enrollment with assessment, acknowledgement, feedback and certificate.
        var completed = new EmployeeOrientation
        {
            TenantId = tenantId,
            ProgramId = onboarding.Id,
            EmployeeId = employees[0].Id,
            EnrollmentStatus = OrientationEnrollmentStatus.Completed,
            EnrollmentSource = OrientationEnrollmentSource.AutoRule,
            EnrolledAt = now.AddDays(-25),
            StartedAt = now.AddDays(-24),
            CompletedAt = now.AddDays(-18),
            LastActivityAt = now.AddDays(-18),
            ProgressPercentage = 100,
            CompletionStatus = OrientationCompletionStatus.Completed,
            FinalScore = 100m,
            AttemptCount = 1,
            IsPassed = true,
            AcknowledgementSigned = true,
            NextDueDate = now.AddDays(5),
            CreatedBy = SeedUser,
        };
        _context.EmployeeOrientations.Add(completed);
        await _context.SaveChangesAsync();

        await SeedContentProgressAsync(tenantId, completed, onboarding.Id, completeAll: true);
        await SeedSignedAcknowledgementAsync(tenantId, completed);
        SeedFeedback(tenantId, completed.Id, employees[0].Id);
        await SeedCertificateAsync(tenantId, completed, "OCERT-2026-00001", onboarding);
        await _context.SaveChangesAsync();

        // 2) An IN-PROGRESS onboarding enrollment with partial content progress.
        if (employees.Count > 1)
        {
            var inProgress = new EmployeeOrientation
            {
                TenantId = tenantId,
                ProgramId = onboarding.Id,
                EmployeeId = employees[1].Id,
                EnrollmentStatus = OrientationEnrollmentStatus.Active,
                EnrollmentSource = OrientationEnrollmentSource.HrAssigned,
                EnrolledAt = now.AddDays(-5),
                StartedAt = now.AddDays(-4),
                LastActivityAt = now.AddDays(-1),
                ProgressPercentage = 40,
                CompletionStatus = OrientationCompletionStatus.InProgress,
                NextDueDate = now.AddDays(25),
                CreatedBy = SeedUser,
            };
            _context.EmployeeOrientations.Add(inProgress);
            await _context.SaveChangesAsync();
            await SeedContentProgressAsync(tenantId, inProgress, onboarding.Id, completeAll: false);
            await _context.SaveChangesAsync();
        }

        // 3) A NOT-STARTED mandatory compliance enrollment (overdue).
        if (employees.Count > 2)
        {
            _context.EmployeeOrientations.Add(new EmployeeOrientation
            {
                TenantId = tenantId,
                ProgramId = compliance.Id,
                EmployeeId = employees[2].Id,
                EnrollmentStatus = OrientationEnrollmentStatus.Confirmed,
                EnrollmentSource = OrientationEnrollmentSource.AutoRule,
                EnrolledAt = now.AddDays(-20),
                ProgressPercentage = 0,
                CompletionStatus = OrientationCompletionStatus.Overdue,
                NextDueDate = now.AddDays(-6),
                CreatedBy = SeedUser,
            });
            await _context.SaveChangesAsync();
        }

        // 4) A session enrollment for the product launch, with an attendance record.
        var sessionEnrollment = new EmployeeOrientation
        {
            TenantId = tenantId,
            ProgramId = productLaunch.Id,
            SessionId = session.Id,
            EmployeeId = employees[0].Id,
            EnrollmentStatus = OrientationEnrollmentStatus.Confirmed,
            EnrollmentSource = OrientationEnrollmentSource.SelfEnrollment,
            EnrolledAt = now.AddDays(-2),
            ProgressPercentage = 0,
            CompletionStatus = OrientationCompletionStatus.NotStarted,
            NextDueDate = now.AddDays(19),
            CreatedBy = SeedUser,
        };
        _context.EmployeeOrientations.Add(sessionEnrollment);
        await _context.SaveChangesAsync();

        _context.OrientationAttendanceRecords.Add(new OrientationAttendanceRecord
        {
            TenantId = tenantId,
            EnrollmentId = sessionEnrollment.Id,
            SessionDay = 1,
            AttendanceStatus = OrientationAttendanceStatus.NotRecorded,
            CreatedBy = SeedUser,
        });
        await _context.SaveChangesAsync();
    }

    private async Task SeedContentProgressAsync(Guid tenantId, EmployeeOrientation enrollment, Guid programId, bool completeAll)
    {
        var contentItems = await _context.OrientationContentItems
            .Where(c => c.Module.ProgramId == programId && !c.IsDeleted)
            .OrderBy(c => c.Module.SequenceOrder).ThenBy(c => c.SequenceOrder)
            .ToListAsync();

        var cutoff = completeAll ? contentItems.Count : contentItems.Count / 2;
        var now = DateTime.UtcNow;

        for (var i = 0; i < contentItems.Count; i++)
        {
            var done = i < cutoff;
            _context.OrientationContentProgresses.Add(new OrientationContentProgress
            {
                TenantId = tenantId,
                EmployeeOrientationId = enrollment.Id,
                ContentItemId = contentItems[i].Id,
                Status = done ? OrientationContentProgressStatus.Completed : OrientationContentProgressStatus.NotStarted,
                FirstAccessedAt = done ? now.AddDays(-3) : null,
                LastAccessedAt = done ? now.AddDays(-3) : null,
                CompletedAt = done ? now.AddDays(-3) : null,
                TotalTimeSpentSeconds = done ? 300 : 0,
                AccessCount = done ? 1 : 0,
                CreatedBy = SeedUser,
            });
        }
    }

    private async Task SeedSignedAcknowledgementAsync(Guid tenantId, EmployeeOrientation enrollment)
    {
        if (await _context.OrientationAcknowledgements.AnyAsync(a => a.EmployeeOrientationId == enrollment.Id))
            return;

        _context.OrientationAcknowledgements.Add(new OrientationAcknowledgement
        {
            TenantId = tenantId,
            EmployeeOrientationId = enrollment.Id,
            Title = "Code of Conduct Acknowledgement",
            AcknowledgementText = "I confirm that I have read, understood and agree to abide by the Company Code of Conduct.",
            Status = OrientationAcknowledgementStatus.Signed,
            PresentedAt = DateTime.UtcNow.AddDays(-19),
            SignedAt = DateTime.UtcNow.AddDays(-18),
            SignatureIpAddress = "10.0.0.5",
            CreatedBy = SeedUser,
        });
    }

    private void SeedFeedback(Guid tenantId, Guid enrollmentId, Guid employeeId)
    {
        _context.OrientationFeedbacks.Add(new OrientationFeedback
        {
            TenantId = tenantId,
            EmployeeOrientationId = enrollmentId,
            OverallRating = 5,
            ContentRating = 4,
            RelevanceRating = 5,
            Comments = "Great onboarding experience — clear and well structured.",
            IsAnonymous = false,
            SubmittedByEmployeeId = employeeId,
            SubmittedAt = DateTime.UtcNow.AddDays(-17),
            CreatedBy = SeedUser,
        });
    }

    private async Task SeedCertificateAsync(Guid tenantId, EmployeeOrientation enrollment, string number, OrientationProgram program)
    {
        if (await _context.OrientationCertificates.AnyAsync(c => c.CertificateNumber == number))
            return;

        var issuedAt = DateTime.UtcNow.AddDays(-18);
        var expiresAt = program.CertificateValidityMonths is > 0
            ? issuedAt.AddMonths(program.CertificateValidityMonths.Value)
            : (DateTime?)null;

        _context.OrientationCertificates.Add(new OrientationCertificate
        {
            TenantId = tenantId,
            EmployeeOrientationId = enrollment.Id,
            CertificateNumber = number,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Status = OrientationCertificateStatus.Active,
            VerificationUrl = $"https://verify.example.com/orientation/{number}",
            CreatedBy = SeedUser,
        });

        enrollment.CertificateIssued = true;
        enrollment.CertificateSerialNumber = number;
        enrollment.CertificateExpiresAt = expiresAt;
    }

    // ── Notifications ────────────────────────────────────────────────────────────

    private async Task SeedNotificationsAsync(Guid tenantId, OrientationProgram onboarding, OrientationProgram compliance, List<Employee> employees)
    {
        if (await _context.OrientationNotifications.AnyAsync(n => n.ProgramId == onboarding.Id || n.ProgramId == compliance.Id))
            return;

        _context.OrientationNotifications.Add(new OrientationNotification
        {
            TenantId = tenantId,
            ProgramId = onboarding.Id,
            RecipientEmployeeId = employees[0].Id,
            Type = OrientationNotificationType.Completion,
            Subject = "Onboarding completed 🎉",
            Message = "Congratulations on completing your onboarding. Your certificate is now available.",
            NavigationUrl = "/me/orientation",
            IsRead = false,
            SentAt = DateTime.UtcNow.AddDays(-18),
            CreatedBy = SeedUser,
        });

        if (employees.Count > 2)
        {
            _context.OrientationNotifications.Add(new OrientationNotification
            {
                TenantId = tenantId,
                ProgramId = compliance.Id,
                RecipientEmployeeId = employees[2].Id,
                Type = OrientationNotificationType.Overdue,
                Subject = "Action required: Compliance training overdue",
                Message = "Your mandatory Anti-Harassment & Code of Conduct training is overdue. Please complete it as soon as possible.",
                NavigationUrl = "/me/orientation",
                IsRead = false,
                SentAt = DateTime.UtcNow.AddDays(-6),
                CreatedBy = SeedUser,
            });
        }

        await _context.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<Guid> ResolveDefaultTenantIdAsync()
    {
        var tenant = await _context.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == DefaultTenantIdFallback);
        return tenant?.Id ?? DefaultTenantIdFallback;
    }
}
