using ErpSystem.Api.Services.Finance.Budget;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public class BudgetServiceHardeningTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    [Fact]
    public void BudgetSegmentMigration_UsesSqlServerCompatibleCombinedScopeFilter()
    {
        var operation = new ExposedBudgetSegmentMigration().BuildOperations()
            .OfType<CreateIndexOperation>()
            .Single(item => item.Name ==
                "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId_DistributionDimensionValueId");

        operation.IsUnique.Should().BeTrue();
        operation.Columns.Should().Equal(
            "TenantId", "BudgetScenarioId", "SegmentValueId", "DistributionDimensionValueId");
        operation.Filter.Should().Be("[IsDeleted] = 0");
        operation.Filter.Should().NotContain(" OR ");
    }

    [Fact]
    public async Task UpdateScenarioAsync_OmittedDimensionPolicyPreservesExistingControls()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var definition = CreateDimensionDefinition();
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BudgetScenarioId = scenario.Id,
            FinanceDimensionDefinitionId = definition.Id, DisplayOrder = 0
        });
        db.FiscalYears.Add(new FiscalYear
        {
            Id = scenario.FiscalYearId, TenantId = TenantId, FiscalYearName = "FY Test"
        });
        db.FinanceDimensionDefinitions.Add(definition);
        db.BudgetScenarios.Add(scenario);
        await db.SaveChangesAsync();

        var result = await CreateService(db).UpdateScenarioAsync(new UpdateBudgetScenarioDto
        {
            Id = scenario.Id,
            Name = "FY Budget renamed",
            RowVersion = Convert.ToBase64String(scenario.RowVersion)
        });

        result.ControlDimensions.Should().ContainSingle(item => item.DimensionCode == "DEPT");
        (await db.BudgetScenarioControlDimensions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateScenarioAsync_UnusedDraftCanChangeStructuralBudgetGrain()
    {
        await using var db = CreateContext();
        var originalYear = CreateFiscalYear();
        var replacementYear = CreateFiscalYear();
        replacementYear.Year = 2027;
        replacementYear.FiscalYearCode = "FY2027";
        replacementYear.FiscalYearName = "FY2027";
        replacementYear.StartDate = new DateTime(2027, 1, 1);
        replacementYear.EndDate = new DateTime(2027, 12, 31);
        var scenario = CreateScenario("Draft");
        scenario.FiscalYearId = originalYear.Id;
        var dimension = CreateDimensionDefinition();
        var segment = CreateSegmentStructure();
        db.AddRange(originalYear, replacementYear, scenario, dimension, segment);
        await db.SaveChangesAsync();
        var rowVersion = Convert.ToBase64String(scenario.RowVersion);
        db.ChangeTracker.Clear();

        var result = await CreateService(db).UpdateScenarioAsync(new UpdateBudgetScenarioDto
        {
            Id = scenario.Id,
            Name = "FY2027 Department Budget",
            Description = "Updated before distribution",
            FiscalYearId = replacementYear.Id,
            BaseCurrencyCode = "USD",
            ControlDimensionDefinitionIds = new List<Guid> { dimension.Id },
            ControlSegmentStructureIds = new List<Guid> { segment.Id },
            RowVersion = rowVersion
        });

        result.FiscalYearId.Should().Be(replacementYear.Id);
        result.BaseCurrencyCode.Should().Be("USD");
        result.ControlDimensions.Should().ContainSingle(item =>
            item.FinanceDimensionDefinitionId == dimension.Id);
        result.ControlSegments.Should().ContainSingle(item =>
            item.AccountSegmentStructureId == segment.Id);
    }

    [Fact]
    public async Task UpdateScenarioAsync_StructuralChangeIsBlockedAfterAReturnExists()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var scenario = CreateScenario("Draft");
        scenario.FiscalYearId = fiscalYear.Id;
        db.AddRange(fiscalYear, scenario, CreateReturn(scenario.Id, CurrentUserId));
        await db.SaveChangesAsync();

        var act = () => CreateService(db).UpdateScenarioAsync(new UpdateBudgetScenarioDto
        {
            Id = scenario.Id,
            Name = scenario.Name,
            BaseCurrencyCode = "USD",
            RowVersion = Convert.ToBase64String(scenario.RowVersion)
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already has budget returns*");
    }

    [Fact]
    public async Task DeleteScenarioAsync_SoftDeletesAnUnusedDraftAndItsGrainControls()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario("Draft");
        var dimension = CreateDimensionDefinition();
        var segment = CreateSegmentStructure();
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = dimension.Id
        });
        scenario.ControlSegments.Add(new BudgetScenarioControlSegment
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            AccountSegmentStructureId = segment.Id
        });
        db.AddRange(scenario, dimension, segment);
        await db.SaveChangesAsync();

        var deleted = await CreateService(db).DeleteScenarioAsync(
            scenario.Id, Convert.ToBase64String(scenario.RowVersion));

        deleted.Should().BeTrue();
        (await db.BudgetScenarios.IgnoreQueryFilters().SingleAsync(item => item.Id == scenario.Id))
            .IsDeleted.Should().BeTrue();
        (await db.BudgetScenarioControlDimensions.IgnoreQueryFilters().SingleAsync())
            .IsDeleted.Should().BeTrue();
        (await db.BudgetScenarioControlSegments.IgnoreQueryFilters().SingleAsync())
            .IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteScenarioAsync_RejectsADraftWithReturns()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario("Draft");
        db.AddRange(scenario, CreateReturn(scenario.Id, CurrentUserId));
        await db.SaveChangesAsync();

        var act = () => CreateService(db).DeleteScenarioAsync(
            scenario.Id, Convert.ToBase64String(scenario.RowVersion));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already has budget returns*");
    }

    [Fact]
    public async Task BulkSaveEntriesAsync_PersistsCanonicalDimensionCell()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        var scenario = CreateScenario();
        scenario.FiscalYearId = fiscalYear.Id;
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        var account = CreateAccount(AccountType.Expense);
        account.Status = AccountStatus.Active;
        account.AllowDirectPosting = true;
        var definition = CreateDimensionDefinition();
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = definition.Id,
            Code = "FIN", Name = "Finance",
            EffectiveDate = fiscalYear.StartDate, IsActive = true
        };
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BudgetScenarioId = scenario.Id,
            FinanceDimensionDefinitionId = definition.Id, DisplayOrder = 0
        });
        db.AddRange(fiscalYear, period, scenario, budgetReturn, account, definition, value);
        await db.SaveChangesAsync();

        await CreateService(db).BulkSaveEntriesAsync(new BulkSaveBudgetEntriesDto
        {
            BudgetReturnId = budgetReturn.Id,
            ReturnRowVersion = Convert.ToBase64String(budgetReturn.RowVersion),
            Entries =
            {
                new BudgetEntrySaveDto
                {
                    BudgetReturnId = budgetReturn.Id,
                    AccountId = account.Id,
                    FiscalPeriodId = period.Id,
                    CurrencyCode = "GHS",
                    Amount = 125_000m,
                    DimensionAssignments =
                    {
                        new BudgetDimensionAssignmentInputDto
                        {
                            FinanceDimensionDefinitionId = definition.Id,
                            FinanceDimensionValueId = value.Id
                        }
                    }
                }
            }
        });

        var entry = await db.BudgetEntries.Include(item => item.FinanceDimensionSet)
            .ThenInclude(set => set!.Items).SingleAsync();
        entry.AmountBase.Should().Be(125_000m);
        entry.FinanceDimensionSet.Should().NotBeNull();
        entry.FinanceDimensionSet!.Items.Should().ContainSingle(item =>
            item.FinanceDimensionDefinitionId == definition.Id
            && item.FinanceDimensionValueId == value.Id);
    }

    [Fact]
    public async Task GetMyReturnsAsync_ReturnsOnlyAssignmentsForCurrentUser()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        db.FiscalYears.Add(new FiscalYear
        {
            Id = scenario.FiscalYearId,
            TenantId = TenantId,
            FiscalYearName = "FY Test"
        });
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.AddRange(
            CreateReturn(scenario.Id, CurrentUserId),
            CreateReturn(scenario.Id, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = (await service.GetMyReturnsAsync()).ToList();

        result.Should().ContainSingle();
        result[0].AssignedToUserId.Should().Be(CurrentUserId);
    }

    [Fact]
    public async Task GetReturnAsync_DeniesAUserWhoIsNeitherAssigneeNorBudgetSupervisor()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var budgetReturn = CreateReturn(scenario.Id, Guid.NewGuid());
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(budgetReturn);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = () => service.GetReturnAsync(budgetReturn.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task BulkSaveEntriesAsync_RejectsChangesOutsideCollection()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario("Approved");
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(budgetReturn);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new BulkSaveBudgetEntriesDto
        {
            BudgetReturnId = budgetReturn.Id
        };

        var act = () => service.BulkSaveEntriesAsync(request);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Collecting*");
    }

    [Fact]
    public async Task SubmitScenarioAsync_RequiresEveryReturnToBeApproved()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        db.FiscalYears.Add(new FiscalYear
        {
            Id = scenario.FiscalYearId,
            TenantId = TenantId,
            FiscalYearName = "FY Test"
        });
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(CreateReturn(scenario.Id, CurrentUserId));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var act = () => service.SubmitScenarioAsync(
            scenario.Id,
            Convert.ToBase64String(scenario.RowVersion));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public async Task SubmitReturnAsync_StartsWorkflowAndPersistsSubmittedState()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(budgetReturn);
        db.BudgetEntries.Add(new BudgetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BudgetReturnId = budgetReturn.Id,
            AccountId = Guid.NewGuid(),
            FiscalPeriodId = Guid.NewGuid(),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Amount = 14_000m,
            AmountBase = 14_000m
        });
        await db.SaveChangesAsync();

        var workflowInstanceId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowService>(MockBehavior.Strict);
        workflow.Setup(service => service.StartApprovalWorkflowAsync("BudgetReturn", budgetReturn.Id))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = workflowInstanceId
            });

        var result = await CreateService(db, workflow.Object).SubmitReturnAsync(
            budgetReturn.Id,
            Convert.ToBase64String(budgetReturn.RowVersion));

        result.Status.Should().Be("Submitted");
        result.SubmittedDate.Should().NotBeNull();
        (await db.BudgetReturns.AsNoTracking().SingleAsync(item => item.Id == budgetReturn.Id))
            .Status.Should().Be("Submitted");
        workflow.Verify(service => service.StartApprovalWorkflowAsync("BudgetReturn", budgetReturn.Id), Times.Once);
    }

    [Fact]
    public void BudgetWorkflowMutations_KeepTransactionsInsideTheExecutionStrategy()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the repository root must be discoverable from the test output");
        var source = File.ReadAllText(Path.Combine(
            directory!.FullName,
            "src",
            "ErpSystem.Api",
            "Services",
            "Finance",
            "Budget",
            "BudgetService.cs"));

        AssertRetryableWorkflowMethod(source, "SubmitScenarioAsync", "ArchiveScenarioAsync");
        AssertRetryableWorkflowMethod(source, "SubmitReturnAsync", "RecallReturnAsync");
        AssertRetryableWorkflowMethod(source, "RecallReturnAsync", "ENTRIES");
    }

    [Fact]
    public async Task UpdateReturnAsync_RejectsConflictingAssignAndClearRequest()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.Add(budgetReturn);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new UpdateBudgetReturnDto
        {
            AssignedToUserId = Guid.NewGuid(),
            ClearAssignedToUser = true,
            RowVersion = Convert.ToBase64String(budgetReturn.RowVersion)
        };

        var act = () => service.UpdateReturnAsync(budgetReturn.Id, request);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*set and cleared*");
    }

    [Fact]
    public async Task CreateReturnAsync_RequiresAssignPermissionWhenAnAssigneeIsProvided()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        db.BudgetScenarios.Add(scenario);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id,
            AssignedToUserId = Guid.NewGuid()
        };

        var act = () => service.CreateReturnAsync(request);

        await act.Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Assign Budget Returns*");
    }

    [Fact]
    public async Task CreateReturnAsync_RequiresAValueFromTheScenarioControlDimensions()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var definition = CreateDimensionDefinition();
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BudgetScenarioId = scenario.Id,
            FinanceDimensionDefinitionId = definition.Id, DisplayOrder = 0
        });
        db.AddRange(scenario, definition);
        await db.SaveChangesAsync();

        var act = () => CreateService(db).CreateReturnAsync(new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id
        });

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*distribution value*");
    }

    [Fact]
    public async Task CreateReturnAsync_PersistsAnActiveScenarioDistributionValue()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var definition = CreateDimensionDefinition();
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = definition.Id,
            Code = "FIN", Name = "Finance Department",
            EffectiveDate = new DateTime(2026, 1, 1), IsActive = true
        };
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BudgetScenarioId = scenario.Id,
            FinanceDimensionDefinitionId = definition.Id, DisplayOrder = 0
        });
        db.AddRange(scenario, definition, value);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateReturnAsync(new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id,
            DistributionDimensionValueId = value.Id
        });

        result.DistributionDimensionValueId.Should().Be(value.Id);
        result.DistributionDimensionDefinitionId.Should().Be(definition.Id);
        result.DistributionDimensionCode.Should().Be("FIN");
        result.DistributionDimensionName.Should().Be("Finance Department");
        (await db.BudgetReturns.SingleAsync()).DistributionDimensionValueId.Should().Be(value.Id);
    }

    [Fact]
    public async Task CreateReturnAsync_RequiresAValueFromTheScenarioControlSegments()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var segment = CreateSegmentStructure();
        scenario.ControlSegments.Add(new BudgetScenarioControlSegment
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            AccountSegmentStructureId = segment.Id
        });
        db.AddRange(scenario, segment);
        await db.SaveChangesAsync();

        var act = () => CreateService(db).CreateReturnAsync(new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*segment value*");
    }

    [Fact]
    public async Task CreateReturnAsync_PersistsIndependentSegmentAndDimensionScope()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var segment = CreateSegmentStructure();
        var segmentValue = CreateSegmentValue(segment.Id);
        var dimension = CreateDimensionDefinition();
        var dimensionValue = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = dimension.Id,
            Code = "FIN", Name = "Finance Department",
            EffectiveDate = new DateTime(2026, 1, 1), IsActive = true
        };
        scenario.ControlSegments.Add(new BudgetScenarioControlSegment
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            AccountSegmentStructureId = segment.Id
        });
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = dimension.Id
        });
        db.AddRange(scenario, segment, segmentValue, dimension, dimensionValue);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateReturnAsync(new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id,
            SegmentValueId = segmentValue.Id,
            DistributionDimensionValueId = dimensionValue.Id
        });

        result.SegmentValueId.Should().Be(segmentValue.Id);
        result.SegmentStructureId.Should().Be(segment.Id);
        result.SegmentStructureCode.Should().Be("COMPANY");
        result.SegmentValueCode.Should().Be("DEFAULT");
        result.DistributionDimensionValueId.Should().Be(dimensionValue.Id);
        result.DistributionDimensionDefinitionId.Should().Be(dimension.Id);
    }

    [Fact]
    public async Task CreateReturnAsync_AllowsOneSegmentValueAcrossDifferentDimensions()
    {
        await using var db = CreateContext();
        var scenario = CreateScenario();
        var segment = CreateSegmentStructure();
        var segmentValue = CreateSegmentValue(segment.Id);
        var dimension = CreateDimensionDefinition();
        var finance = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = dimension.Id,
            Code = "FIN", Name = "Finance", EffectiveDate = new DateTime(2026, 1, 1), IsActive = true
        };
        var operations = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = dimension.Id,
            Code = "OPS", Name = "Operations", EffectiveDate = new DateTime(2026, 1, 1), IsActive = true
        };
        scenario.ControlSegments.Add(new BudgetScenarioControlSegment
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            AccountSegmentStructureId = segment.Id
        });
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = dimension.Id
        });
        db.AddRange(scenario, segment, segmentValue, dimension, finance, operations);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.CreateReturnAsync(new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id,
            SegmentValueId = segmentValue.Id,
            DistributionDimensionValueId = finance.Id
        });
        await service.CreateReturnAsync(new CreateBudgetReturnDto
        {
            BudgetScenarioId = scenario.Id,
            SegmentValueId = segmentValue.Id,
            DistributionDimensionValueId = operations.Id
        });

        (await db.BudgetReturns.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task BulkSaveEntriesAsync_RequiresTheReturnDistributionValueOnEveryCell()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        var scenario = CreateScenario();
        scenario.FiscalYearId = fiscalYear.Id;
        var definition = CreateDimensionDefinition();
        var assignedValue = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = definition.Id,
            Code = "FIN", Name = "Finance Department",
            EffectiveDate = fiscalYear.StartDate, IsActive = true
        };
        var otherValue = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = definition.Id,
            Code = "OPS", Name = "Operations Department",
            EffectiveDate = fiscalYear.StartDate, IsActive = true
        };
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        budgetReturn.DistributionDimensionValueId = assignedValue.Id;
        var account = CreateAccount(AccountType.Expense);
        account.Status = AccountStatus.Active;
        account.AllowDirectPosting = true;
        scenario.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BudgetScenarioId = scenario.Id,
            FinanceDimensionDefinitionId = definition.Id, DisplayOrder = 0
        });
        db.AddRange(fiscalYear, period, scenario, definition, assignedValue, otherValue, budgetReturn, account);
        await db.SaveChangesAsync();

        var act = () => CreateService(db).BulkSaveEntriesAsync(new BulkSaveBudgetEntriesDto
        {
            BudgetReturnId = budgetReturn.Id,
            ReturnRowVersion = Convert.ToBase64String(budgetReturn.RowVersion),
            Entries =
            {
                new BudgetEntrySaveDto
                {
                    BudgetReturnId = budgetReturn.Id,
                    AccountId = account.Id,
                    FiscalPeriodId = period.Id,
                    CurrencyCode = "GHS",
                    Amount = 1_000m,
                    DimensionAssignments =
                    {
                        new BudgetDimensionAssignmentInputDto
                        {
                            FinanceDimensionDefinitionId = definition.Id,
                            FinanceDimensionValueId = otherValue.Id
                        }
                    }
                }
            }
        });

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*assigned distribution dimension value*");
    }

    [Fact]
    public async Task AdoptScenarioAsync_SupersedesTheExistingOfficialBudget()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var existingOfficial = CreateScenario("Approved");
        existingOfficial.FiscalYearId = fiscalYear.Id;
        existingOfficial.Name = "Original Budget";
        existingOfficial.IsActive = true;
        var replacement = CreateScenario("Approved");
        replacement.FiscalYearId = fiscalYear.Id;
        replacement.Name = "Revised Budget";
        db.FiscalYears.Add(fiscalYear);
        db.BudgetScenarios.AddRange(existingOfficial, replacement);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.AdoptScenarioAsync(replacement.Id, new AdoptBudgetScenarioDto
        {
            RowVersion = Convert.ToBase64String(replacement.RowVersion),
            EffectiveDate = new DateTime(2026, 7, 1),
            Reason = "Board-approved mid-year revision"
        });

        result.IsActive.Should().BeTrue();
        result.Status.Should().Be("Approved");
        result.AdoptionReason.Should().Be("Board-approved mid-year revision");
        existingOfficial.IsActive.Should().BeFalse();
        existingOfficial.Status.Should().Be("Superseded");
        existingOfficial.SupersessionReason.Should().Be("Board-approved mid-year revision");
    }

    [Fact]
    public async Task GetConsolidatedViewAsync_ApprovedScopeExcludesDraftReturns()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var scenario = CreateScenario();
        scenario.FiscalYearId = fiscalYear.Id;
        var period = CreatePeriod(fiscalYear.Id);
        var account = CreateAccount(AccountType.Expense);
        var approvedReturn = CreateReturn(scenario.Id, CurrentUserId);
        approvedReturn.Status = "Approved";
        var draftReturn = CreateReturn(scenario.Id, CurrentUserId);
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        db.Accounts.Add(account);
        db.BudgetScenarios.Add(scenario);
        db.BudgetReturns.AddRange(approvedReturn, draftReturn);
        db.BudgetEntries.AddRange(
            CreateEntry(approvedReturn.Id, account.Id, period.Id, 125m),
            CreateEntry(draftReturn.Id, account.Id, period.Id, 75m));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetConsolidatedViewAsync(scenario.Id, approvedOnly: true);

        result.IncludedReturnCount.Should().Be(1);
        result.TotalExpenseBudget.Should().Be(125m);
        result.Lines.Should().ContainSingle();
        result.Lines[0].Contributions.Should().ContainSingle();
        result.Lines[0].Contributions[0].BudgetReturnId.Should().Be(approvedReturn.Id);
    }

    [Fact]
    public async Task GetConsolidatedViewAsync_ReturnsExactDimensionCellPositionAndActiveReservation()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        period.PeriodCode = "2026-09";
        period.PeriodName = "September 2026";
        period.PeriodNumber = 9;
        period.StartDate = new DateTime(2026, 9, 1);
        period.EndDate = new DateTime(2026, 9, 30);
        var scenario = CreateScenario("Approved");
        scenario.FiscalYearId = fiscalYear.Id;
        scenario.IsActive = true;
        var budgetReturn = CreateReturn(scenario.Id, CurrentUserId);
        budgetReturn.Status = "Approved";
        var account = CreateAccount(AccountType.Expense);
        var definition = CreateDimensionDefinition();
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = definition.Id,
            Code = "FIN", Name = "Finance",
            EffectiveDate = fiscalYear.StartDate, IsActive = true
        };
        var dimensionSet = new FinanceDimensionSet
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            CombinationHash = "FIN-HASH", DisplayValue = "DEPT: FIN — Finance"
        };
        dimensionSet.Items.Add(new FinanceDimensionSetItem
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionSetId = dimensionSet.Id,
            FinanceDimensionDefinitionId = definition.Id,
            FinanceDimensionValueId = value.Id,
            DimensionCodeSnapshot = "DEPT",
            DimensionValueCodeSnapshot = "FIN",
            DimensionValueNameSnapshot = "Finance"
        });
        var entry = CreateEntry(budgetReturn.Id, account.Id, period.Id, 10_000m);
        entry.FinanceDimensionSetId = dimensionSet.Id;
        var primaryBook = db.AccountingBooks.Local.Single(book => book.IsDefault);
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            JournalEntryNumber = "JE-2026-TEST",
            JournalType = "General",
            EntryDate = new DateTime(2026, 9, 15),
            Description = "Dimension-cell position test",
            PostingStatus = "Posted",
            AccountingBookId = primaryBook.Id,
            ApprovalStatus = "Approved",
            FiscalPeriodId = period.Id,
            TotalDebitAmount = 500m,
            TotalCreditAmount = 500m
        };
        var transaction = new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            JournalEntryId = journal.Id,
            AccountingBookId = primaryBook.Id,
            AccountId = account.Id,
            FiscalPeriodId = period.Id,
            FinanceDimensionSetId = dimensionSet.Id,
            TransactionDate = journal.EntryDate,
            DebitAmount = 500m,
            FunctionalCurrencyCode = "GHS",
            BookClassification = "IFRS",
            PostingStatus = "Posted"
        };
        var parallelBook = new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            Code = "LOCAL", Name = "Local statutory",
            BookType = AccountingBookType.ParallelFull,
            IsDefault = false, IsActive = true, AllowsPosting = true
        };
        var parallelJournal = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            JournalEntryNumber = "JE-2026-PARALLEL",
            JournalType = "General",
            EntryDate = journal.EntryDate,
            Description = "Parallel-book representation",
            PostingStatus = "Posted",
            ApprovalStatus = "Approved",
            AccountingBookId = parallelBook.Id,
            BookClassification = parallelBook.Code,
            FiscalPeriodId = period.Id,
            TotalDebitAmount = 700m,
            TotalCreditAmount = 700m
        };
        var parallelTransaction = new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            JournalEntryId = parallelJournal.Id,
            AccountingBookId = parallelBook.Id,
            AccountId = account.Id,
            FiscalPeriodId = period.Id,
            FinanceDimensionSetId = dimensionSet.Id,
            TransactionDate = parallelJournal.EntryDate,
            DebitAmount = 700m,
            FunctionalCurrencyCode = "GHS",
            BookClassification = parallelBook.Code,
            PostingStatus = "Posted"
        };
        var activeReservation = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            BudgetScenarioId = scenario.Id,
            BudgetReturnId = budgetReturn.Id,
            BudgetEntryId = entry.Id,
            AccountId = account.Id,
            FiscalPeriodId = period.Id,
            FinanceDimensionSetId = dimensionSet.Id,
            CurrencyCode = "GHS",
            SourceDocumentType = "JournalEntry",
            SourceDocumentId = Guid.NewGuid(),
            BudgetDate = journal.EntryDate,
            TransactionCurrencyCode = "GHS",
            TransactionAmount = 300m,
            ReservedAmount = 300m,
            Status = "Reserved",
            EvaluationHash = "ACTIVE-HASH",
            ReservedByUserId = CurrentUserId,
            ReservedAt = DateTime.UtcNow
        };
        var consumedReservation = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            BudgetScenarioId = scenario.Id,
            BudgetReturnId = budgetReturn.Id,
            BudgetEntryId = entry.Id,
            AccountId = account.Id,
            FiscalPeriodId = period.Id,
            FinanceDimensionSetId = dimensionSet.Id,
            CurrencyCode = "GHS",
            SourceDocumentType = "JournalEntry",
            SourceDocumentId = journal.Id,
            BudgetDate = journal.EntryDate,
            TransactionCurrencyCode = "GHS",
            TransactionAmount = 500m,
            ReservedAmount = 500m,
            Status = "Consumed",
            EvaluationHash = "CONSUMED-HASH",
            ReservedByUserId = CurrentUserId,
            ReservedAt = DateTime.UtcNow,
            ConsumedByUserId = CurrentUserId,
            ConsumedAt = DateTime.UtcNow,
            JournalEntryId = journal.Id
        };

        db.AddRange(fiscalYear, period, scenario, budgetReturn, account, definition, value,
            dimensionSet, entry, journal, transaction, parallelBook, parallelJournal,
            parallelTransaction, activeReservation, consumedReservation);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetConsolidatedViewAsync(scenario.Id, approvedOnly: true);

        var cell = result.Lines.Should().ContainSingle().Subject.DimensionCells
            .Should().ContainSingle().Subject;
        cell.FinanceDimensionSetId.Should().Be(dimensionSet.Id);
        cell.DimensionDisplayValue.Should().Be("DEPT: FIN — Finance");
        cell.DimensionAssignments.Should().ContainSingle(assignment =>
            assignment.DimensionCode == "DEPT"
            && assignment.DimensionName == "Department"
            && assignment.ValueCode == "FIN"
            && assignment.ValueName == "Finance");
        cell.BudgetAmount.Should().Be(10_000m);
        cell.ActualAmount.Should().Be(500m);
        cell.ReservedAmount.Should().Be(300m);
        cell.AvailableAmount.Should().Be(9_200m);
    }

    [Fact]
    public async Task GetActiveBudgetVsActualAsync_UsesOnlyTheExplicitlyAdoptedScenario()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var official = CreateScenario("Approved");
        official.FiscalYearId = fiscalYear.Id;
        official.Name = "Official";
        official.IsActive = true;
        var alternative = CreateScenario("Approved");
        alternative.FiscalYearId = fiscalYear.Id;
        alternative.Name = "Alternative";
        db.FiscalYears.Add(fiscalYear);
        db.BudgetScenarios.AddRange(official, alternative);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetActiveBudgetVsActualAsync(fiscalYear.Id);

        result.ScenarioId.Should().Be(official.Id);
        result.IsOfficial.Should().BeTrue();
    }

    [Fact]
    public async Task CreateRevisionAsync_RejectsAVirementThatDoesNotNetToZero()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        var expense = CreateAccount(AccountType.Expense);
        var revenue = CreateAccount(AccountType.Revenue);
        var official = CreateScenario("Approved");
        official.FiscalYearId = fiscalYear.Id;
        official.IsActive = true;
        var budgetReturn = CreateReturn(official.Id, CurrentUserId);
        budgetReturn.Status = "Approved";
        db.AddRange(fiscalYear, period, expense, revenue, official, budgetReturn);
        db.BudgetEntries.Add(CreateEntry(budgetReturn.Id, expense.Id, period.Id, 100m));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = CreateRevisionRequest(official.Id, period.Id, expense.Id, revenue.Id, -25m, 20m);

        var act = () => service.CreateRevisionAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exactly zero*");
    }

    [Fact]
    public async Task CreateRevisionAsync_AcceptsExactGovernedDimensionCombination()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        var expense = CreateAccount(AccountType.Expense);
        var revenue = CreateAccount(AccountType.Revenue);
        var definition = CreateDimensionDefinition();
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionDefinitionId = definition.Id,
            Code = "FIN", Name = "Finance", IsActive = true,
            EffectiveDate = new DateTime(2025, 1, 1)
        };
        var dimensionSet = new FinanceDimensionSet
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            CombinationHash = new string('D', 64),
            DisplayValue = "DEPT: FIN — Finance"
        };
        dimensionSet.Items.Add(new FinanceDimensionSetItem
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FinanceDimensionSetId = dimensionSet.Id,
            FinanceDimensionDefinitionId = definition.Id,
            FinanceDimensionValueId = value.Id,
            DimensionCodeSnapshot = "DEPT",
            DimensionNameSnapshot = "Department",
            DimensionValueCodeSnapshot = "FIN",
            DimensionValueNameSnapshot = "Finance"
        });
        var official = CreateScenario("Approved");
        official.FiscalYearId = fiscalYear.Id;
        official.IsActive = true;
        official.ControlDimensions.Add(new BudgetScenarioControlDimension
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            BudgetScenarioId = official.Id,
            FinanceDimensionDefinitionId = definition.Id,
            DisplayOrder = 1
        });
        var budgetReturn = CreateReturn(official.Id, CurrentUserId);
        budgetReturn.Status = "Approved";
        var releaseEntry = CreateEntry(budgetReturn.Id, expense.Id, period.Id, 100m);
        releaseEntry.FinanceDimensionSetId = dimensionSet.Id;
        var increaseEntry = CreateEntry(budgetReturn.Id, revenue.Id, period.Id, 50m);
        increaseEntry.FinanceDimensionSetId = dimensionSet.Id;
        db.AddRange(fiscalYear, period, expense, revenue, definition, value, dimensionSet,
            official, budgetReturn, releaseEntry, increaseEntry);
        await db.SaveChangesAsync();

        var request = CreateRevisionRequest(
            official.Id, period.Id, expense.Id, revenue.Id, -25m, 25m);
        foreach (var line in request.Lines)
            line.FinanceDimensionSetId = dimensionSet.Id;

        var result = await CreateService(db).CreateRevisionAsync(request);

        result.Lines.Should().HaveCount(2).And.OnlyContain(line =>
            line.FinanceDimensionSetId == dimensionSet.Id
            && line.DimensionCombination == dimensionSet.DisplayValue);
        result.Lines.SelectMany(line => line.DimensionAssignments).Should().OnlyContain(item =>
            item.DimensionCode == "DEPT" && item.ValueCode == "FIN");
    }

    [Fact]
    public async Task ApplyRevisionAsync_CreatesAnImmutableOfficialSuccessorAndSupersedesSource()
    {
        await using var db = CreateContext();
        var fiscalYear = CreateFiscalYear();
        var period = CreatePeriod(fiscalYear.Id);
        var expense = CreateAccount(AccountType.Expense);
        var revenue = CreateAccount(AccountType.Revenue);
        var official = CreateScenario("Approved");
        official.FiscalYearId = fiscalYear.Id;
        official.Name = "FY2026 Original";
        official.IsActive = true;
        official.VersionNumber = 1;
        var budgetReturn = CreateReturn(official.Id, CurrentUserId);
        budgetReturn.Status = "Approved";
        db.AddRange(fiscalYear, period, expense, revenue, official, budgetReturn);
        db.BudgetEntries.AddRange(
            CreateEntry(budgetReturn.Id, expense.Id, period.Id, 100m),
            CreateEntry(budgetReturn.Id, revenue.Id, period.Id, 50m));

        var revision = new BudgetRevision
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RevisionNumber = "BR-2026-00001",
            RevisionType = "Virement",
            SourceScenarioId = official.Id,
            EffectiveDate = new DateTime(2026, 7, 1),
            BoardResolutionReference = "TDC/BOARD/2026/047",
            BoardResolutionDate = new DateTime(2026, 6, 25),
            Justification = "Move approved funds to the higher-priority revenue activity.",
            Status = "Approved",
            SubmittedByUserId = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow,
            RowVersion = new byte[8],
            Lines = new List<BudgetRevisionLine>
            {
                CreateRevisionLine(expense.Id, period.Id, -25m),
                CreateRevisionLine(revenue.Id, period.Id, 25m)
            }
        };
        db.BudgetRevisions.Add(revision);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ApplyRevisionAsync(
            revision.Id,
            Convert.ToBase64String(revision.RowVersion));

        result.Status.Should().Be("Applied");
        result.ResultScenarioId.Should().NotBeNull();
        official.IsActive.Should().BeFalse();
        official.Status.Should().Be("Superseded");
        var successor = await db.BudgetScenarios
            .Include(item => item.BudgetReturns).ThenInclude(item => item.BudgetEntries)
            .SingleAsync(item => item.Id == result.ResultScenarioId);
        successor.IsActive.Should().BeTrue();
        successor.ParentScenarioId.Should().Be(official.Id);
        successor.VersionType.Should().Be("Virement");
        successor.VersionNumber.Should().Be(2);
        successor.BudgetReturns.Single().BudgetEntries.Sum(item => item.AmountBase).Should().Be(150m);
        successor.BudgetReturns.Single().BudgetEntries.Single(item => item.AccountId == expense.Id).AmountBase.Should().Be(75m);
        successor.BudgetReturns.Single().BudgetEntries.Single(item => item.AccountId == revenue.Id).AmountBase.Should().Be(75m);
    }

    [Fact]
    public async Task BudgetReconciliation_FindsTerminalSourceOrphanAndIncompleteConsumption()
    {
        await using var db = CreateContext();
        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            InvoiceNumber = "AP-VOID-001",
            Status = VendorInvoiceStatus.Voided
        };
        var orphan = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            SourceDocumentType = "VendorInvoice", SourceDocumentId = invoice.Id,
            Status = "Reserved", CurrencyCode = "GHS", TransactionCurrencyCode = "GHS",
            EvaluationHash = new string('A', 64), ReservedByUserId = CurrentUserId,
            ReservedAt = DateTime.UtcNow
        };
        var incomplete = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            SourceDocumentType = "ManualJournalEntry", SourceDocumentId = Guid.NewGuid(),
            Status = "Consumed", CurrencyCode = "GHS", TransactionCurrencyCode = "GHS",
            EvaluationHash = new string('B', 64), ReservedByUserId = CurrentUserId,
            ReservedAt = DateTime.UtcNow, ConsumedByUserId = CurrentUserId,
            ConsumedAt = DateTime.UtcNow
        };
        db.AddRange(invoice, orphan, incomplete);
        await db.SaveChangesAsync();

        var report = await CreateService(db).GetBudgetReconciliationAsync();

        report.IsReconciled.Should().BeFalse();
        report.Issues.Should().Contain(item =>
            item.Code == "BUDGET_ORPHAN_RESERVATION_TERMINAL_SOURCE"
            && item.ReservationId == orphan.Id);
        report.Issues.Should().Contain(item =>
            item.Code == "BUDGET_CONSUMED_POSTING_EVIDENCE_MISSING"
            && item.ReservationId == incomplete.Id);
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"budget-hardening-{Guid.NewGuid():N}")
            .Options;
        var db = new ApplicationDbContext(options, TenantId);
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            Code = "PRIMARY", Name = "Primary book",
            BookType = AccountingBookType.PrimaryFull,
            IsDefault = true, IsActive = true, AllowsPosting = true
        });
        db.Currencies.AddRange(
            CreateCurrency("GHS", "936", "Ghana Cedi", true),
            CreateCurrency("USD", "840", "United States Dollar", false));
        return db;
    }

    private BudgetService CreateService(ApplicationDbContext db, IWorkflowService? workflow = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(TenantId);
        currentUser.SetupGet(service => service.UserId).Returns(CurrentUserId.ToString());

        return new BudgetService(db, currentUser.Object, workflow ?? Mock.Of<IWorkflowService>());
    }

    private static void AssertRetryableWorkflowMethod(string source, string methodName, string nextMarker)
    {
        var start = source.IndexOf($" {methodName}(", StringComparison.Ordinal);
        var end = source.IndexOf(nextMarker, start + methodName.Length, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        end.Should().BeGreaterThan(start);

        var method = source[start..end];
        method.Should().Contain("CreateExecutionStrategy()");
        method.Should().Contain("ExecuteInTransactionAsync(");
        method.Should().NotContain("BeginTransactionAsync(");
    }

    private BudgetScenario CreateScenario(string status = "Collecting") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FiscalYearId = Guid.NewGuid(),
            Name = "FY Budget",
            BaseCurrencyCode = "GHS",
            Status = status,
            RowVersion = new byte[8]
        };

    private BudgetReturn CreateReturn(Guid scenarioId, Guid assignedToUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BudgetScenarioId = scenarioId,
            AssignedToUserId = assignedToUserId,
            Status = "Draft",
            RowVersion = new byte[8]
        };

    private FiscalYear CreateFiscalYear() =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FiscalYearName = "FY2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        };

    private FiscalPeriod CreatePeriod(Guid fiscalYearId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FiscalYearId = fiscalYearId,
            PeriodName = "January 2026",
            PeriodCode = "2026-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31)
        };

    private Account CreateAccount(AccountType accountType) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            AccountCode = accountType == AccountType.Revenue ? "4000" : "5000",
            AccountName = accountType == AccountType.Revenue ? "Revenue" : "Expense",
            AccountType = accountType
        };

    private FinanceDimensionDefinition CreateDimensionDefinition() =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            Code = "DEPT", Name = "Department",
            Classification = "Analytical", ValueSourceType = "Lookup", IsActive = true
        };

    private AccountSegmentStructure CreateSegmentStructure() =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            SegmentCode = "COMPANY", SegmentName = "Company",
            SegmentPosition = 1, SegmentLength = 7,
            LookupTableRequired = true, IsNaturalAccount = false,
            IsActive = true, LifecycleStatus = AccountSegmentLifecycleStatus.Active
        };

    private SegmentLookupValue CreateSegmentValue(Guid structureId) =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            SegmentStructureId = structureId,
            SegmentValue = "DEFAULT", Description = "Default company",
            EffectiveDate = new DateTime(2026, 1, 1), IsActive = true
        };

    private Currency CreateCurrency(string code, string numericCode, string name, bool isBase) =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            CurrencyCode = code, NumericCode = numericCode,
            CurrencyName = name, DecimalPlaces = 2,
            IsBaseCurrency = isBase, IsActive = true
        };

    private BudgetEntry CreateEntry(
        Guid returnId,
        Guid accountId,
        Guid periodId,
        decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BudgetReturnId = returnId,
            AccountId = accountId,
            FiscalPeriodId = periodId,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Amount = amount,
            AmountBase = amount,
            RowVersion = new byte[8]
        };

    private CreateBudgetRevisionDto CreateRevisionRequest(
        Guid scenarioId,
        Guid periodId,
        Guid releaseAccountId,
        Guid increaseAccountId,
        decimal release,
        decimal increase) =>
        new()
        {
            SourceScenarioId = scenarioId,
            RevisionType = "Virement",
            EffectiveDate = new DateTime(2026, 7, 1),
            BoardResolutionReference = "TDC/BOARD/2026/047",
            BoardResolutionDate = new DateTime(2026, 6, 25),
            Justification = "Move approved funds to the higher-priority operational activity.",
            Lines = new List<BudgetRevisionLineInputDto>
            {
                new() { AccountId = releaseAccountId, FiscalPeriodId = periodId, AdjustmentAmountBase = release },
                new() { AccountId = increaseAccountId, FiscalPeriodId = periodId, AdjustmentAmountBase = increase }
            }
        };

    private BudgetRevisionLine CreateRevisionLine(Guid accountId, Guid periodId, decimal adjustment) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            AccountId = accountId,
            FiscalPeriodId = periodId,
            AdjustmentAmountBase = adjustment
        };

    private sealed class ExposedBudgetSegmentMigration : AddBudgetScenarioSegmentControls
    {
        public IReadOnlyList<MigrationOperation> BuildOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }
    }
}
