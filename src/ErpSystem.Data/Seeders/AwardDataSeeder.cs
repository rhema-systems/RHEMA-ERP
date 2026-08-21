using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds sample Award data for an existing tenant (typically DEFAULT).
/// Safe to run multiple times (idempotent by award type name).
/// </summary>
public sealed class AwardDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AwardDataSeeder> _logger;

    public AwardDataSeeder(ApplicationDbContext context, ILogger<AwardDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedForDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT", cancellationToken);
        if (defaultTenant == null)
        {
            _logger.LogError("Default tenant not found. Cannot seed awards.");
            return 0;
        }

        return await SeedForTenantAsync(defaultTenant.Id, cancellationToken);
    }

    public async Task<int> SeedForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Seeding sample award data for tenant {TenantId}...", tenantId);

        var totalCreated = 0;

        // Get a sample employee for seeding (use the hardcoded employee ID from the UI services)
        var employeeId = Guid.Parse("831704D1-2E99-411A-85AD-A2414618876F");
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
        
        if (employee == null)
        {
            _logger.LogWarning("Employee {EmployeeId} not found. Will create award types and budgets only.", employeeId);
        }

        var now = DateTime.UtcNow;
        var currentYear = now.Year;

        // 1. Seed Award Types
        var awardTypesData = new[]
        {
            new { Name = "Employee of the Month", Description = "Recognizes outstanding performance each month", Category = AwardCategory.Performance, Frequency = AwardFrequency.Monthly, IsMonetary = true, IsTeamAward = false },
            new { Name = "Employee of the Year", Description = "Annual recognition for exceptional contributions", Category = AwardCategory.Performance, Frequency = AwardFrequency.Annual, IsMonetary = true, IsTeamAward = false },
            new { Name = "Innovation Award", Description = "Recognizes creative solutions and innovation", Category = AwardCategory.Innovation, Frequency = AwardFrequency.Quarterly, IsMonetary = true, IsTeamAward = false },
            new { Name = "Safety Excellence", Description = "Acknowledges commitment to workplace safety", Category = AwardCategory.Safety, Frequency = AwardFrequency.Quarterly, IsMonetary = false, IsTeamAward = false },
            new { Name = "Customer Service Excellence", Description = "Outstanding customer service delivery", Category = AwardCategory.CustomerService, Frequency = AwardFrequency.Monthly, IsMonetary = true, IsTeamAward = false },
            new { Name = "Team Excellence Award", Description = "Recognizes high-performing teams", Category = AwardCategory.TeamPlayer, Frequency = AwardFrequency.Quarterly, IsMonetary = true, IsTeamAward = true },
            new { Name = "Leadership Award", Description = "Exceptional leadership and mentorship", Category = AwardCategory.Leadership, Frequency = AwardFrequency.Annual, IsMonetary = true, IsTeamAward = false },
            new { Name = "Peer Recognition", Description = "Peer-to-peer appreciation and recognition", Category = AwardCategory.SpecialRecognition, Frequency = AwardFrequency.Monthly, IsMonetary = false, IsTeamAward = false }
        };

        var awardTypes = new Dictionary<string, AwardType>();

        foreach (var data in awardTypesData)
        {
            var existing = await _context.AwardTypes
                .FirstOrDefaultAsync(at => at.TenantId == tenantId && at.Name == data.Name, cancellationToken);

            if (existing == null)
            {
                var awardType = new AwardType
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = data.Name.Replace(" ", "").ToUpper().Substring(0, Math.Min(20, data.Name.Replace(" ", "").Length)),
                    Name = data.Name,
                    Description = data.Description,
                    Category = data.Category,
                    Frequency = data.Frequency,

                    // Area 14 slice 2 replaced AutoGenerateNominees (a flag nothing read) with the
                    // two axes of decision D-3. These are the entity defaults, stated explicitly
                    // because this seeder is still deferred pending TDC's real award catalogue —
                    // whoever rewrites it against that catalogue must set them per award type
                    // rather than inherit a default. See HrSeedOrchestrator.DeferredSteps.
                    NominationSource = AwardNominationSource.OpenNomination,
                    WinnerDecision = AwardWinnerDecision.CommitteeScore,
                    AllowSelfNomination = false,

                    // Performance triggers are left unset: TDC has not said what score or how many
                    // goals should put somebody forward automatically, and a generated candidate
                    // list built on an invented threshold would look authoritative.
                    MinPerformanceScore = null,
                    MinGoalsAchieved = null,

                    CreatedAt = now,
                    CreatedBy = string.Empty
                };

                _context.AwardTypes.Add(awardType);
                awardTypes[data.Name] = awardType;
                totalCreated++;
                _logger.LogInformation("Created award type: {Name}", data.Name);
            }
            else
            {
                awardTypes[data.Name] = existing;
                _logger.LogInformation("Award type already exists: {Name}", data.Name);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 2. Seed Award Levels for monetary awards
        var levelsData = new[]
        {
            new { TypeName = "Employee of the Month", LevelName = "Bronze", Amount = 250m, Points = 100 },
            new { TypeName = "Employee of the Month", LevelName = "Silver", Amount = 500m, Points = 200 },
            new { TypeName = "Employee of the Month", LevelName = "Gold", Amount = 1000m, Points = 500 },
            
            new { TypeName = "Employee of the Year", LevelName = "Bronze", Amount = 2000m, Points = 1000 },
            new { TypeName = "Employee of the Year", LevelName = "Silver", Amount = 3500m, Points = 1500 },
            new { TypeName = "Employee of the Year", LevelName = "Gold", Amount = 5000m, Points = 2500 },
            
            new { TypeName = "Innovation Award", LevelName = "Standard", Amount = 1500m, Points = 750 },
            new { TypeName = "Innovation Award", LevelName = "Exceptional", Amount = 3000m, Points = 1500 },
            
            new { TypeName = "Customer Service Excellence", LevelName = "Bronze", Amount = 200m, Points = 100 },
            new { TypeName = "Customer Service Excellence", LevelName = "Silver", Amount = 400m, Points = 200 },
            new { TypeName = "Customer Service Excellence", LevelName = "Gold", Amount = 750m, Points = 400 },
            
            new { TypeName = "Team Excellence Award", LevelName = "Bronze", Amount = 2500m, Points = 1000 },
            new { TypeName = "Team Excellence Award", LevelName = "Silver", Amount = 5000m, Points = 2000 },
            new { TypeName = "Team Excellence Award", LevelName = "Gold", Amount = 10000m, Points = 5000 },
            
            new { TypeName = "Leadership Award", LevelName = "Emerging Leader", Amount = 2000m, Points = 1000 },
            new { TypeName = "Leadership Award", LevelName = "Exceptional Leader", Amount = 4000m, Points = 2000 }
        };

        foreach (var data in levelsData)
        {
            if (awardTypes.TryGetValue(data.TypeName, out var awardType))
            {
                var existing = await _context.AwardLevels
                    .FirstOrDefaultAsync(al => al.AwardTypeId == awardType.Id && al.Name == data.LevelName, cancellationToken);

                if (existing == null)
                {
                    var level = new AwardLevel
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        AwardTypeId = awardType.Id,
                        Code = data.LevelName.Replace(" ", "").ToUpper(),
                        Name = data.LevelName,
                        Description = $"{data.LevelName} level award",
                        Rank = data.LevelName == "Gold" ? 1 : data.LevelName == "Silver" ? 2 : data.LevelName == "Exceptional" ? 1 : 3,
                        MonetaryAmount = data.Amount,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = string.Empty
                    };

                    _context.AwardLevels.Add(level);
                    totalCreated++;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 3. Seed Award Budgets
        var budgetData = new[]
        {
            new { TypeName = "Employee of the Month", Amount = 15000m, Year = 2026 },
            new { TypeName = "Employee of the Year", Amount = 50000m, Year = 2026 },
            new { TypeName = "Innovation Award", Amount = 25000m, Year = 2026 },
            new { TypeName = "Customer Service Excellence", Amount = 10000m, Year = 2026 },
            new { TypeName = "Team Excellence Award", Amount = 40000m, Year = 2026 },
            new { TypeName = "Leadership Award", Amount = 30000m, Year = 2026 }
        };

        foreach (var data in budgetData)
        {
            if (awardTypes.TryGetValue(data.TypeName, out var awardType))
            {
                var existing = await _context.AwardBudgets
                    .FirstOrDefaultAsync(ab => ab.AwardTypeId == awardType.Id && ab.Year == data.Year, cancellationToken);

                if (existing == null)
                {
                    var budget = new AwardBudget
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        BudgetCode = $"{awardType.Code}_{data.Year}",
                        AwardTypeId = awardType.Id,
                        Year = data.Year,
                        BudgetAmount = data.Amount,
                        SpentAmount = 0m,
                        ReservedAmount = 0m,
                        CreatedAt = now,
                        CreatedBy = string.Empty
                    };

                    _context.AwardBudgets.Add(budget);
                    totalCreated++;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 4. Seed Award Committees
        if (employee != null)
        {
            var committeeData = new[]
            {
                new { Name = "Awards Review Committee", Description = "Reviews and approves all award nominations", IsActive = true },
                new { Name = "Innovation Awards Panel", Description = "Specialized panel for innovation award evaluations", IsActive = true },
                new { Name = "Annual Awards Committee", Description = "Committee for annual employee of the year awards", IsActive = true }
            };

            var committees = new List<AwardCommittee>();

            foreach (var data in committeeData)
            {
                var existing = await _context.AwardCommittees
                    .FirstOrDefaultAsync(ac => ac.TenantId == tenantId && ac.Name == data.Name, cancellationToken);

                if (existing == null)
                {
                    var committee = new AwardCommittee
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = data.Name,
                        Description = data.Description,
                        IsActive = data.IsActive,
                        CreatedAt = now,
                        CreatedBy = string.Empty
                    };

                    _context.AwardCommittees.Add(committee);
                    committees.Add(committee);
                    totalCreated++;

                    // Add the sample employee as a committee member
                    var member = new AwardCommitteeMember
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CommitteeId = committee.Id,
                        EmployeeId = employeeId,
                        Role = "Member",
                        IsActive = true,
                        StartDate = now.AddMonths(-3),
                        CreatedAt = now,
                        CreatedBy = string.Empty
                    };

                    _context.AwardCommitteeMembers.Add(member);
                    totalCreated++;
                }
                else
                {
                    committees.Add(existing);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            // 5. Seed Award Nominations
            if (awardTypes.TryGetValue("Employee of the Month", out var eomType))
            {
                var nomination = new AwardNomination
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    NominationNumber = $"NOM-{currentYear}-001",
                    AwardTypeId = eomType.Id,
                    NomineeId = employeeId,
                    NominatedById = employeeId,
                    NominationDate = now.AddDays(-10),
                    Year = currentYear,
                    Justification = "Outstanding performance in delivering critical project milestones ahead of schedule while maintaining exceptional quality standards.",
                    Status = AwardNominationStatus.Submitted,
                    CreatedAt = now.AddDays(-10),
                    CreatedBy = string.Empty
                };

                _context.AwardNominations.Add(nomination);
                totalCreated++;
            }

            if (awardTypes.TryGetValue("Innovation Award", out var innovationType))
            {
                var nomination = new AwardNomination
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    NominationNumber = $"NOM-{currentYear}-002",
                    AwardTypeId = innovationType.Id,
                    NomineeId = employeeId,
                    NominatedById = employeeId,
                    NominationDate = now.AddDays(-5),
                    Year = currentYear,
                    Justification = "Developed an innovative solution that reduced processing time by 40% and improved customer satisfaction scores.",
                    Status = AwardNominationStatus.UnderReview,
                    CreatedAt = now.AddDays(-5),
                    CreatedBy = string.Empty
                };

                _context.AwardNominations.Add(nomination);
                totalCreated++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            // 6. Committee reviews are NOT seeded.
            //
            // This block used to create an empty review row per nomination - ReviewDate null,
            // Approved null - to represent "pending review". Area 14 slice 6 replaced that
            // approve/reject field with a required score, because TDC decides these awards on the
            // highest average score and a boolean cannot rank anything.
            //
            // Under that model an empty review row is not merely useless, it is wrong: the scoring
            // service counts rows to decide whether a nomination has met AwardType.MinRequiredReviewers,
            // and a placeholder would count as a reviewer who had scored while contributing a
            // meaningless value to the mean. "Not yet reviewed" is now the ABSENCE of a row, which is
            // also the honest representation - a member who has not scored has not left an opinion.

            // 7. Seed Employee Awards (approved awards)
            if (awardTypes.TryGetValue("Customer Service Excellence", out var cseType))
            {
                var level = await _context.AwardLevels
                    .FirstOrDefaultAsync(al => al.AwardTypeId == cseType.Id && al.Name == "Gold", cancellationToken);

                if (level != null)
                {
                    var award = new EmployeeAward
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        AwardNumber = $"CSE-{currentYear}-001",
                        EmployeeId = employeeId,
                        AwardTypeId = cseType.Id,
                        AwardLevelId = level.Id,
                        AwardDate = now.AddMonths(-2),
                        Citation = "Consistently exceeded customer service expectations, achieving 98% satisfaction rating.",
                        MonetaryAmount = level.MonetaryAmount,
                        CertificateIssued = true,
                        PaymentProcessed = false,
                        PublishToWebsite = true,
                        CreatedAt = now.AddMonths(-2),
                        CreatedBy = string.Empty
                    };

                    _context.EmployeeAwards.Add(award);
                    totalCreated++;
                }
            }

            if (awardTypes.TryGetValue("Safety Excellence", out var safetyType))
            {
                var award = new EmployeeAward
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    AwardNumber = $"SAFE-{currentYear}-001",
                    EmployeeId = employeeId,
                    AwardTypeId = safetyType.Id,
                    AwardDate = now.AddMonths(-1),
                    Citation = "Led safety initiatives resulting in zero workplace incidents for 6 consecutive months.",
                    MonetaryAmount = 0m,
                    CertificateIssued = true,
                    TrophyIssued = true,
                    PaymentProcessed = false,
                    PublishToWebsite = true,
                    CreatedAt = now.AddMonths(-1),
                    CreatedBy = string.Empty
                };

                _context.EmployeeAwards.Add(award);
                totalCreated++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            // 8. Seed Long Service Awards
            var hireDate = now.AddYears(-10).AddMonths(-3); // Employee hired 10+ years ago
            
            var longServiceAward = new LongServiceAward
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = employeeId,
                YearsOfService = 10,
                ServiceStartDate = hireDate,
                MilestoneDate = hireDate.AddYears(10),
                AwardDescription = "Recognizes 10 years of dedicated service to the organization",
                MonetaryAmount = 5000m,
                LeaveDaysBonus = 5,
                OtherBenefits = "Engraved Plaque, Watch, or Weekend Getaway",
                IsProcessed = false,
                PaymentProcessed = false,
                CreatedAt = now,
                CreatedBy = string.Empty
            };

            _context.LongServiceAwards.Add(longServiceAward);
            totalCreated++;

            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Award seeding completed. Created {Count} records.", totalCreated);
        return totalCreated;
    }
}
