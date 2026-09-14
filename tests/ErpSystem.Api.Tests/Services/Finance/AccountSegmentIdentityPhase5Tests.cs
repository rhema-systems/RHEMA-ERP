using ErpSystem.Api.Services.Finance.Segments;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountSegmentIdentityPhase5Tests
{
    [Fact]
    public async Task ExactActiveSet_ComposesStableTenantScopedIdentity()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var service = new AccountSegmentIdentityService(db);

        var result = await service.ValidateAndComposeAsync(fixture.TenantId,
            ValidValues(fixture.Company, fixture.Natural, fixture.CompanyValue));

        result.AccountNumber.Should().Be("TDC-6100");
        result.NaturalAccountCode.Should().Be("6100");
        result.Values.Select(item => item.SegmentStructureId).Should()
            .Equal(fixture.Company.Id, fixture.Natural.Id);
    }

    [Fact]
    public async Task InvalidSetsAndValues_FailClosedAtServerBoundary()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var other = await SeedStructureAsync(db, "ALT");
        var service = new AccountSegmentIdentityService(db);
        var valid = ValidValues(fixture.Company, fixture.Natural, fixture.CompanyValue);

        var invalidCases = new List<IReadOnlyCollection<AccountSegmentValueCreateDto>>
        {
            valid.Take(1).ToList(),
            new List<AccountSegmentValueCreateDto> { valid[0], valid[0], valid[1] },
            new List<AccountSegmentValueCreateDto> { valid[0], valid[1], new() { SegmentStructureId = other.Company.Id, SegmentPosition = 3, SegmentValue = "ALT" } },
            new List<AccountSegmentValueCreateDto> { valid[0], new() { SegmentStructureId = Guid.NewGuid(), SegmentPosition = 2, SegmentValue = "6100" } },
            new List<AccountSegmentValueCreateDto> { valid[0], new() { SegmentStructureId = fixture.Natural.Id, SegmentPosition = 1, SegmentValue = "6100" } },
            new List<AccountSegmentValueCreateDto> { valid[0], new() { SegmentStructureId = fixture.Natural.Id, SegmentPosition = 2, SegmentValue = "610" } },
            new List<AccountSegmentValueCreateDto> { valid[0], new() { SegmentStructureId = fixture.Natural.Id, SegmentPosition = 2, SegmentValue = "ABCD" } },
            new List<AccountSegmentValueCreateDto> { new() { SegmentStructureId = fixture.Company.Id, SegmentPosition = 1, SegmentValue = other.CompanyValue.SegmentValue,
                SegmentLookupValueId = other.CompanyValue.Id }, valid[1] }
        };

        foreach (var values in invalidCases)
            await service.Invoking(item => item.ValidateAndComposeAsync(fixture.TenantId, values))
                .Should().ThrowAsync<InvalidOperationException>();

        fixture.Company.IsActive = false;
        await db.SaveChangesAsync();
        await service.Invoking(item => item.ValidateAndComposeAsync(fixture.TenantId, valid))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CollisionAndClientComposedMismatch_AreRejected()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountCode = "6100", AccountNumber = "TDC-6100",
            AccountName = "Existing", AccountType = AccountType.Expense, CurrencyCode = "GHS"
        });
        await db.SaveChangesAsync();
        var service = new AccountSegmentIdentityService(db);
        var values = ValidValues(fixture.Company, fixture.Natural, fixture.CompanyValue);

        await service.Invoking(item => item.ValidateAndComposeAsync(fixture.TenantId, values, "CLIENT-6100"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*server-composed*");
        await service.Invoking(item => item.ValidateAndComposeAsync(fixture.TenantId, values))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task ExistingIncompleteAccount_ReturnsExplicitReconciliationEvidence()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountCode = "6100", AccountNumber = "6100",
            AccountName = "Legacy account", AccountType = AccountType.Expense, CurrencyCode = "GHS"
        };
        db.Accounts.Add(account); await db.SaveChangesAsync();

        var readiness = await new AccountSegmentIdentityService(db).GetReadinessAsync(fixture.TenantId, account.Id);

        readiness.IsReady.Should().BeFalse();
        readiness.MissingSegmentCodes.Should().BeEquivalentTo("COMPANY", "NATURAL_ACCOUNT");
        readiness.Issues.Should().ContainSingle(item => item.Contains("Missing required GL account segments"));
    }

    [Fact]
    public async Task Manifest_IsIdempotentTenantScopedAndPreservesAdministratorDimensions()
    {
        await using var db = CreateContext();
        var first = NewTenant("TDC"); var second = NewTenant("ALT");
        db.Tenants.AddRange(first, second);
        db.FinanceDimensionDefinitions.Add(new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(), TenantId = first.Id, Code = "DEPARTMENT", Name = "Administrator department",
            Classification = "Analytical", ValueSourceType = "Lookup", IsActive = false, CreatedBy = "Administrator"
        });
        await db.SaveChangesAsync();
        var seeder = new FinanceSegmentDimensionManifestSeeder(db, NullLogger.Instance);

        await seeder.SeedAsync(first.Id, DateTime.UnixEpoch);
        var administratorCompany = await db.AccountSegmentStructures.SingleAsync(item =>
            item.TenantId == first.Id && item.SegmentCode == FinanceSegmentDimensionManifestSeeder.CompanyCode);
        administratorCompany.SegmentName = "Administrator legal entity";
        administratorCompany.LifecycleStatus = AccountSegmentLifecycleStatus.Frozen;
        administratorCompany.UpdatedBy = "Administrator";
        administratorCompany.UpdatedAt = DateTime.UnixEpoch.AddDays(1);
        await db.SaveChangesAsync();
        var administratorTimestamp = administratorCompany.UpdatedAt;
        await seeder.SeedAsync(second.Id, DateTime.UnixEpoch);
        await seeder.SeedAsync(first.Id, DateTime.UnixEpoch);

        foreach (var tenant in new[] { first, second })
        {
            var identityCodes = await db.AccountSegmentStructures.Where(item => item.TenantId == tenant.Id && item.IsActive)
                .OrderBy(item => item.SegmentPosition).Select(item => item.SegmentCode).ToListAsync();
            identityCodes.Should().Equal("COMPANY", "NATURAL_ACCOUNT");
            (await db.FinanceDimensionDefinitions.Where(item => item.TenantId == tenant.Id && !item.IsDeleted)
                .Select(item => item.Code).ToListAsync()).Should().BeEquivalentTo(FinanceSegmentDimensionManifestSeeder.TransactionDimensionCodes);
        }
        (await db.FinanceDimensionDefinitions.SingleAsync(item => item.TenantId == first.Id && item.Code == "DEPARTMENT"))
            .Name.Should().Be("Administrator department");
        var preservedCompany = await db.AccountSegmentStructures.SingleAsync(item =>
            item.TenantId == first.Id && item.SegmentCode == FinanceSegmentDimensionManifestSeeder.CompanyCode);
        preservedCompany.SegmentName.Should().Be("Administrator legal entity");
        preservedCompany.LifecycleStatus.Should().Be(AccountSegmentLifecycleStatus.Frozen);
        preservedCompany.UpdatedAt.Should().Be(administratorTimestamp);
    }

    [Fact]
    public void Migration_RemovesOptionalityAndAddsLifecycleConcurrencyAndExactSetIndexes()
    {
        var source = ArchivedMigrationSource.Read("20260904003118_AddGovernedAccountSegmentIdentity.cs");
        foreach (var token in new[] { "IsMandatory", "RowVersion", "LifecycleStatus",
            "CK_AccountSegmentStructures_LifecycleActive", "Phase 5 preflight failed",
            "cross-tenant account/segment lineage", "wrong-segment lookup lineage",
            "AK_Accounts_TenantId_Id", "AK_AccountSegmentStructures_TenantId_Id",
            "AK_SegmentLookupValues_TenantId_Id", "AccountSegmentValues" }) source.Should().Contain(token);
    }

    [Fact]
    public void Migration_IsDiscoveredByEfCoreWithoutOpeningADatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PhaseFiveMigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Keys.Should()
            .Equal("20260913162402_DisposableDevelopmentCurrentModelBaseline");
        ArchivedMigrationSource.Read("20260904003118_AddGovernedAccountSegmentIdentity.cs")
            .Should().Contain("AddGovernedAccountSegmentIdentity");
    }

    [Fact]
    public void PublicMutationContracts_DoNotPermitOptionalOrReportingIdentityFlags()
    {
        typeof(SegmentStructureCreateDto).GetProperty("IsMandatory").Should().BeNull();
        typeof(SegmentStructureUpdateDto).GetProperty("IsMandatory").Should().BeNull();
        typeof(AccountSegmentStructureCreateDto).GetProperty("IsMandatory").Should().BeNull();
        typeof(SegmentStructureCreateDto).GetProperty("IsReportingDimension").Should().BeNull();
        typeof(SegmentStructureUpdateDto).GetProperty("IsReportingDimension").Should().BeNull();
    }

    [Fact]
    public void StartupRepair_DoesNotFabricateLegacyDepartmentOrProjectIdentityValues()
    {
        var program = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Program.cs"));
        program.Should().Contain("Startup repair must not fabricate DEPT/PROJ placeholders");
        program.Should().NotContain("WHEN segment.[SegmentCode] = N'DEPT'");
        program.Should().NotContain("WHEN segment.[SegmentCode] = N'PROJ'");
    }

    [Fact]
    public async Task Lifecycle_TransitionsDraftToActiveToFrozen_AndRejectsFurtherMutation()
    {
        await using var db = CreateContext();
        var tenant = NewTenant("TDC");
        var segment = Structure(tenant.Id, "COMPANY", 1, 3, false, true);
        segment.LifecycleStatus = AccountSegmentLifecycleStatus.Draft;
        segment.IsActive = false;
        segment.RowVersion = [1];
        var natural = Structure(tenant.Id, "NATURAL_ACCOUNT", 2, 4, true, false);
        db.Tenants.Add(tenant); db.AccountSegmentStructures.AddRange(segment, natural);
        db.SegmentLookupValues.Add(new SegmentLookupValue
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, SegmentStructureId = segment.Id,
            SegmentValue = "TDC", Description = "TDC", IsActive = true
        });
        await db.SaveChangesAsync();
        var service = StructureService(db, tenant.Id, Audit());

        var active = await service.ActivateAsync(segment.Id, new AccountSegmentLifecycleTransitionDto
        {
            RowVersion = Convert.ToBase64String(segment.RowVersion), Reason = "Approved structure"
        });
        active.LifecycleStatus.Should().Be("Active");
        active.IsRequired.Should().BeTrue();

        var frozen = await service.FreezeAsync(segment.Id, new AccountSegmentLifecycleTransitionDto
        {
            RowVersion = active.RowVersion, Reason = "Ready for controlled use"
        });
        frozen.LifecycleStatus.Should().Be("Frozen");

        var update = new AccountSegmentStructureUpdateDto
        {
            Id = segment.Id, SegmentName = "Changed", SegmentCode = "COMPANY", SegmentPosition = 1,
            SegmentLength = 3, DataType = "Alphanumeric", LookupTableRequired = true,
            RowVersion = frozen.RowVersion
        };
        await service.Invoking(item => item.UpdateAsync(update)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be changed*");
    }

    [Fact]
    public async Task Activation_RejectsAnExistingAccountWithoutTheResultingExactSegmentSet()
    {
        await using var db = CreateContext();
        var tenant = NewTenant("TDC");
        var segment = Structure(tenant.Id, "COMPANY", 1, 3, false, true);
        segment.LifecycleStatus = AccountSegmentLifecycleStatus.Draft; segment.IsActive = false; segment.RowVersion = [1];
        db.Tenants.Add(tenant); db.AccountSegmentStructures.Add(segment);
        db.SegmentLookupValues.Add(new SegmentLookupValue
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, SegmentStructureId = segment.Id,
            SegmentValue = "TDC", Description = "TDC", IsActive = true
        });
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, AccountCode = "6100", AccountNumber = "6100",
            AccountName = "Legacy", AccountType = AccountType.Expense, CurrencyCode = "GHS"
        });
        await db.SaveChangesAsync();
        var service = StructureService(db, tenant.Id, Audit());

        await service.Invoking(item => item.ActivateAsync(segment.Id, new AccountSegmentLifecycleTransitionDto
        {
            RowVersion = Convert.ToBase64String(segment.RowVersion)
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*every existing GL account*");
    }

    [Fact]
    public async Task RelationalMutation_RollsBackWhenAuditFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        await CreateRelationalSegmentTablesAsync(db);
        var tenant = NewTenant("TDC");
        var segmentId = Guid.NewGuid();
        var segmentName = "Company"; var segmentCode = "COMPANY"; var dataType = "Alphanumeric"; var createdBy = "seed";
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO AccountSegmentStructures
            (Id, TenantId, SegmentName, SegmentCode, SegmentPosition, SegmentLength, DataType,
             LookupTableRequired, IsReportingDimension, IsNaturalAccount, IsActive, LifecycleStatus,
             IsSystemDefined, RowVersion, CreatedAt, CreatedBy, IsDeleted)
            VALUES ({segmentId}, {tenant.Id}, {segmentName}, {segmentCode}, {1}, {3}, {dataType},
             {true}, {false}, {false}, {false}, {(int)AccountSegmentLifecycleStatus.Draft},
             {false}, {new byte[] { 1 }}, {DateTime.UtcNow}, {createdBy}, {false})");
        var failingAudit = new Mock<IFinanceAuditService>();
        failingAudit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));
        var service = StructureService(db, tenant.Id, failingAudit.Object);

        var update = new AccountSegmentStructureUpdateDto
        {
            Id = segmentId, SegmentName = "Changed", SegmentCode = "COMPANY", SegmentPosition = 1,
            SegmentLength = 3, DataType = "Alphanumeric", LookupTableRequired = true,
            RowVersion = Convert.ToBase64String([1])
        };
        await service.Invoking(item => item.UpdateAsync(update)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("audit unavailable");

        db.ChangeTracker.Clear();
        (await db.AccountSegmentStructures.SingleAsync(item => item.Id == segmentId)).SegmentName.Should().Be("Company");
    }

    [Fact]
    public async Task RelationalMutation_RejectsAStaleOriginalRowVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        await CreateRelationalSegmentTablesAsync(db);
        var tenant = NewTenant("TDC"); var segmentId = Guid.NewGuid();
        var segmentName = "Company"; var segmentCode = "COMPANY"; var dataType = "Alphanumeric"; var createdBy = "seed";
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO AccountSegmentStructures
            (Id, TenantId, SegmentName, SegmentCode, SegmentPosition, SegmentLength, DataType,
             LookupTableRequired, IsReportingDimension, IsNaturalAccount, IsActive, LifecycleStatus,
             IsSystemDefined, RowVersion, CreatedAt, CreatedBy, IsDeleted)
            VALUES ({segmentId}, {tenant.Id}, {segmentName}, {segmentCode}, {1}, {3}, {dataType},
             {true}, {false}, {false}, {false}, {(int)AccountSegmentLifecycleStatus.Draft},
             {false}, {new byte[] { 2 }}, {DateTime.UtcNow}, {createdBy}, {false})");
        var service = StructureService(db, tenant.Id, Audit());
        var update = new AccountSegmentStructureUpdateDto
        {
            Id = segmentId, SegmentName = "Stale change", SegmentCode = "COMPANY", SegmentPosition = 1,
            SegmentLength = 3, DataType = "Alphanumeric", LookupTableRequired = true,
            RowVersion = Convert.ToBase64String([1])
        };

        await service.Invoking(item => item.UpdateAsync(update)).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task BulkCombinations_PersistOnlyCanonicalIdentityEvidence()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var service = CombinationService(db, fixture.TenantId);
        var combination = ValidCombination(fixture);
        combination.AccountNumber = " tdc-6100 ";
        combination.SegmentValues[0].Value = " tdc ";
        combination.SegmentValues[1].Value = " 6100 ";

        var result = await service.BulkCreateAccountsAsync(new BulkCreateAccountsRequestDto
        {
            Combinations = [combination]
        });

        result.SuccessCount.Should().Be(1);
        result.ErrorCount.Should().Be(0);
        var account = await db.Accounts.Include(item => item.SegmentValues).SingleAsync();
        account.AccountNumber.Should().Be("TDC-6100");
        account.AccountCode.Should().Be("6100");
        account.SegmentValues.OrderBy(item => item.SegmentPosition).Select(item => item.SegmentValue)
            .Should().Equal("TDC", "6100");
        account.SegmentValues.Should().OnlyContain(item => !item.IsLocked && item.EndDate == null);
    }

    [Fact]
    public async Task BulkCombinations_RejectMalformedAlphaAndAlphanumericEvidence()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var service = CombinationService(db, fixture.TenantId);

        var malformedAlphanumeric = ValidCombination(fixture);
        malformedAlphanumeric.SegmentValues[0].Value = "T-@";
        var first = await service.BulkCreateAccountsAsync(new BulkCreateAccountsRequestDto
        {
            Combinations = [malformedAlphanumeric]
        });
        first.ErrorCount.Should().Be(1);
        first.Errors.Single().Error.Should().Contain("Alphanumeric format");

        fixture.Natural.DataType = "Alpha";
        await db.SaveChangesAsync();
        var malformedAlpha = ValidCombination(fixture);
        malformedAlpha.AccountNumber = "TDC-A1CD";
        malformedAlpha.SegmentValues[1].Value = "A1CD";
        var second = await service.BulkCreateAccountsAsync(new BulkCreateAccountsRequestDto
        {
            Combinations = [malformedAlpha]
        });
        second.ErrorCount.Should().Be(1);
        second.Errors.Single().Error.Should().Contain("Alpha format");
        (await db.Accounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BulkCombinations_RejectInvalidSetLineagePositionLengthAndClientNumber()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var other = await SeedStructureAsync(db, "ALT");
        var service = CombinationService(db, fixture.TenantId);
        var cases = new List<AccountCombinationPreviewDto>();

        var missing = ValidCombination(fixture); missing.SegmentValues.RemoveAt(1); cases.Add(missing);
        var duplicate = ValidCombination(fixture); duplicate.SegmentValues.Add(CloneValue(duplicate.SegmentValues[0])); cases.Add(duplicate);
        var extra = ValidCombination(fixture); extra.SegmentValues.Add(new SegmentValuePreviewDto
        {
            SegmentStructureId = other.Natural.Id, SegmentPosition = 3, Value = "6200"
        }); cases.Add(extra);
        var crossTenantLookup = ValidCombination(fixture);
        crossTenantLookup.SegmentValues[0].LookupValueId = other.CompanyValue.Id;
        crossTenantLookup.SegmentValues[0].Value = other.CompanyValue.SegmentValue;
        cases.Add(crossTenantLookup);
        var wrongPosition = ValidCombination(fixture); wrongPosition.SegmentValues[1].SegmentPosition = 1; cases.Add(wrongPosition);
        var wrongLength = ValidCombination(fixture); wrongLength.SegmentValues[1].Value = "610"; cases.Add(wrongLength);
        var mismatch = ValidCombination(fixture); mismatch.AccountNumber = "BAD-6100"; cases.Add(mismatch);

        foreach (var invalid in cases)
        {
            var result = await service.BulkCreateAccountsAsync(new BulkCreateAccountsRequestDto
            {
                Combinations = [invalid]
            });
            result.ErrorCount.Should().Be(1);
            result.SuccessCount.Should().Be(0);
        }

        fixture.CompanyValue.IsActive = false;
        await db.SaveChangesAsync();
        var inactive = await service.BulkCreateAccountsAsync(new BulkCreateAccountsRequestDto
        {
            Combinations = [ValidCombination(fixture)]
        });
        inactive.ErrorCount.Should().Be(1);
        (await db.Accounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AccountUpdate_PersistsValidatorNormalizedRowsAndRecomposedNumber()
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var account = AddAccountWithIdentity(db, fixture, "TDC-6100");
        await db.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(db);
        var currentUser = CurrentUser(fixture.TenantId);
        var books = new Mock<IAccountingBookService>();
        books.Setup(item => item.SyncAccountMappingsAsync(
                It.IsAny<Account>(), It.IsAny<IReadOnlyCollection<AccountAccountingBookUpdateDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new AccountService(unitOfWork, currentUser.Object, books.Object,
            NullLogger<AccountService>.Instance, new AccountSegmentIdentityService(db));
        var untrustedDate = DateTime.UnixEpoch;

        var response = await service.UpdateAsync(new AccountUpdateDto
        {
            Id = account.Id, AccountCode = "6100", AccountNumber = "tdc-6100", AccountName = account.AccountName,
            AccountType = account.AccountType.ToString(), AccountCategory = account.AccountCategory,
            AccountSubCategory = account.AccountSubCategory, CurrencyCode = account.CurrencyCode,
            SegmentValues =
            [
                new() { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), SegmentStructureId = fixture.Company.Id,
                    SegmentPosition = 1, SegmentValue = " tdc ", SegmentLookupValueId = fixture.CompanyValue.Id,
                    IsLocked = true, EffectiveDate = untrustedDate, EndDate = untrustedDate.AddDays(1) },
                new() { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), SegmentStructureId = fixture.Natural.Id,
                    SegmentPosition = 2, SegmentValue = " 6100 ", IsLocked = true,
                    EffectiveDate = untrustedDate, EndDate = untrustedDate.AddDays(1) }
            ]
        });

        response.AccountNumber.Should().Be("TDC-6100");
        var responseRows = response.SegmentValues.ToList();
        responseRows.Should().HaveCount(2);
        responseRows.Select(item => item.SegmentPosition).Should().Equal(1, 2);
        responseRows.Select(item => item.SegmentValue).Should().Equal("TDC", "6100");
        responseRows.Select(item => item.SegmentStructureId)
            .Should().Equal(fixture.Company.Id, fixture.Natural.Id);
        responseRows[0].SegmentLookupValueId.Should().Be(fixture.CompanyValue.Id);
        responseRows[1].SegmentLookupValueId.Should().BeNull();
        responseRows.Should().OnlyContain(item => item.AccountId == account.Id
            && item.TenantId == fixture.TenantId && !item.IsLocked && item.EndDate == null);

        await unitOfWork.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var stored = await db.Accounts.Include(item => item.SegmentValues).SingleAsync(item => item.Id == account.Id);
        stored.AccountNumber.Should().Be("TDC-6100");
        stored.AccountCode.Should().Be("6100");
        var active = stored.SegmentValues.Where(item => !item.IsDeleted).OrderBy(item => item.SegmentPosition).ToList();
        active.Select(item => item.SegmentValue).Should().Equal("TDC", "6100");
        active.Should().OnlyContain(item => item.AccountId == account.Id && item.TenantId == fixture.TenantId
            && !item.IsLocked && item.EndDate == null && item.EffectiveDate > untrustedDate.AddYears(1));
    }

    [Theory]
    [InlineData("foreign-row-tenant")]
    [InlineData("foreign-structure")]
    [InlineData("foreign-lookup")]
    [InlineData("inactive-lookup")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("wrong-position")]
    [InlineData("wrong-length")]
    [InlineData("wrong-type")]
    [InlineData("account-number")]
    public async Task Freeze_RejectsEveryMalformedPersistedIdentity(string corruption)
    {
        await using var db = CreateContext();
        var fixture = await SeedStructureAsync(db, "TDC");
        var other = await SeedStructureAsync(db, "ALT");
        var account = AddAccountWithIdentity(db, fixture, "TDC-6100");
        var rows = account.SegmentValues.OrderBy(item => item.SegmentPosition).ToList();
        switch (corruption)
        {
            case "foreign-row-tenant": rows[0].TenantId = other.TenantId; break;
            case "foreign-structure": rows[0].SegmentStructureId = other.Company.Id; break;
            case "foreign-lookup": rows[0].SegmentLookupValueId = other.CompanyValue.Id; break;
            case "inactive-lookup": fixture.CompanyValue.IsActive = false; break;
            case "duplicate": account.SegmentValues.Add(new AccountSegmentValue
            {
                Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountId = account.Id,
                SegmentStructureId = fixture.Company.Id, SegmentPosition = 3, SegmentValue = "TDC",
                SegmentLookupValueId = fixture.CompanyValue.Id
            }); break;
            case "missing": rows[1].IsDeleted = true; break;
            case "wrong-position": rows[1].SegmentPosition = 1; break;
            case "wrong-length": rows[1].SegmentValue = "610"; break;
            case "wrong-type": rows[1].SegmentValue = "ABCD"; break;
            case "account-number": account.AccountNumber = "TDC-9999"; break;
        }
        await db.SaveChangesAsync();
        var service = StructureService(db, fixture.TenantId, Audit());

        await service.Invoking(item => item.FreezeAsync(fixture.Company.Id, new AccountSegmentLifecycleTransitionDto
        {
            RowVersion = Convert.ToBase64String(fixture.Company.RowVersion)
        })).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*require reconciliation*")
            .WithMessage($"*{account.Id}*");
    }

    [Fact]
    public async Task RelationalDelete_RejectsStaleRowVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        await CreateRelationalSegmentTablesAsync(db);
        var tenant = NewTenant("TDC"); var segmentId = Guid.NewGuid();
        await InsertDraftSegmentAsync(db, tenant.Id, segmentId, "COMPANY", 1, [1]);
        var firstClientRowVersion = Convert.ToBase64String(new byte[] { 1 });
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE AccountSegmentStructures SET RowVersion = {new byte[] { 2 }} WHERE Id = {segmentId}");
        var service = StructureService(db, tenant.Id, Audit());

        await service.Invoking(item => item.DeleteAsync(segmentId, new AccountSegmentDeleteDto
        {
            RowVersion = firstClientRowVersion
        })).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task RelationalReorder_RejectsAnyStaleSegmentRowVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        await CreateRelationalSegmentTablesAsync(db);
        var tenant = NewTenant("TDC"); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await InsertDraftSegmentAsync(db, tenant.Id, first, "COMPANY", 1, [1]);
        await InsertDraftSegmentAsync(db, tenant.Id, second, "NATURAL_ACCOUNT", 2, [1]);
        var firstClientVersions = new Dictionary<Guid, string>
        {
            [first] = Convert.ToBase64String(new byte[] { 1 }),
            [second] = Convert.ToBase64String(new byte[] { 1 })
        };
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE AccountSegmentStructures SET RowVersion = {new byte[] { 2 }} WHERE Id = {first}");
        var service = StructureService(db, tenant.Id, Audit());

        await service.Invoking(item => item.ReorderSegmentsAsync(
        [
            new ReorderSegmentDto { SegmentId = first, NewPosition = 2, RowVersion = firstClientVersions[first] },
            new ReorderSegmentDto { SegmentId = second, NewPosition = 1, RowVersion = firstClientVersions[second] }
        ])).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static AccountCombinationService CombinationService(ApplicationDbContext db, Guid tenantId)
    {
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(item => item.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        return new AccountCombinationService(new UnitOfWork(db), CurrentUser(tenantId).Object,
            tenantSettings.Object, new AccountSegmentIdentityService(db),
            NullLogger<AccountCombinationService>.Instance);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("phase5.tests");
        return currentUser;
    }

    private static AccountCombinationPreviewDto ValidCombination(
        (Guid TenantId, AccountSegmentStructure Company, AccountSegmentStructure Natural, SegmentLookupValue CompanyValue) fixture) => new()
    {
        AccountNumber = $"{fixture.CompanyValue.SegmentValue}-6100",
        GeneratedName = "Operating expense",
        AccountType = AccountType.Expense.ToString(),
        CurrencyCode = "GHS",
        Status = CombinationStatus.Valid,
        SegmentValues =
        [
            new SegmentValuePreviewDto
            {
                SegmentStructureId = fixture.Company.Id, SegmentPosition = fixture.Company.SegmentPosition,
                LookupValueId = fixture.CompanyValue.Id, Value = fixture.CompanyValue.SegmentValue,
                Description = fixture.CompanyValue.Description
            },
            new SegmentValuePreviewDto
            {
                SegmentStructureId = fixture.Natural.Id, SegmentPosition = fixture.Natural.SegmentPosition,
                LookupValueId = Guid.Empty, Value = "6100", Description = "Operating expense"
            }
        ]
    };

    private static SegmentValuePreviewDto CloneValue(SegmentValuePreviewDto source) => new()
    {
        SegmentStructureId = source.SegmentStructureId,
        SegmentPosition = source.SegmentPosition,
        LookupValueId = source.LookupValueId,
        Value = source.Value,
        Description = source.Description
    };

    private static Account AddAccountWithIdentity(
        ApplicationDbContext db,
        (Guid TenantId, AccountSegmentStructure Company, AccountSegmentStructure Natural, SegmentLookupValue CompanyValue) fixture,
        string accountNumber)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountCode = "6100", AccountNumber = accountNumber,
            AccountName = "Operating expense", AccountType = AccountType.Expense, CurrencyCode = "GHS",
            AccountCategory = "Operating"
        };
        account.SegmentValues.Add(new AccountSegmentValue
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountId = account.Id,
            SegmentStructureId = fixture.Company.Id, SegmentPosition = fixture.Company.SegmentPosition,
            SegmentValue = fixture.CompanyValue.SegmentValue, SegmentLookupValueId = fixture.CompanyValue.Id
        });
        account.SegmentValues.Add(new AccountSegmentValue
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, AccountId = account.Id,
            SegmentStructureId = fixture.Natural.Id, SegmentPosition = fixture.Natural.SegmentPosition,
            SegmentValue = "6100"
        });
        db.Accounts.Add(account);
        return account;
    }

    private static async Task InsertDraftSegmentAsync(ApplicationDbContext db, Guid tenantId, Guid segmentId,
        string code, int position, byte[] rowVersion)
    {
        var segmentName = code; var dataType = code == "NATURAL_ACCOUNT" ? "Numeric" : "Alphanumeric"; var createdBy = "seed";
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO AccountSegmentStructures
            (Id, TenantId, SegmentName, SegmentCode, SegmentPosition, SegmentLength, DataType,
             LookupTableRequired, IsReportingDimension, IsNaturalAccount, IsActive, LifecycleStatus,
             IsSystemDefined, RowVersion, CreatedAt, CreatedBy, IsDeleted)
            VALUES ({segmentId}, {tenantId}, {segmentName}, {code}, {position}, {4}, {dataType},
             {false}, {false}, {code == "NATURAL_ACCOUNT"}, {false}, {(int)AccountSegmentLifecycleStatus.Draft},
             {false}, {rowVersion}, {DateTime.UtcNow}, {createdBy}, {false})");
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"phase5-segments-{Guid.NewGuid():N}").Options);

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static async Task CreateRelationalSegmentTablesAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"CREATE TABLE AccountSegmentStructures (
            Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, SegmentName TEXT NOT NULL,
            SegmentCode TEXT NOT NULL, SegmentPosition INTEGER NOT NULL, SegmentLength INTEGER NOT NULL,
            DataType TEXT NOT NULL, SeparatorCharacter TEXT NULL, LookupTableRequired INTEGER NOT NULL,
            IsReportingDimension INTEGER NOT NULL, IsNaturalAccount INTEGER NOT NULL, IsActive INTEGER NOT NULL,
            LifecycleStatus INTEGER NOT NULL, IsSystemDefined INTEGER NOT NULL, RowVersion BLOB NOT NULL,
            FrozenByUserId TEXT NULL, FrozenAtUtc TEXT NULL, RetirementReason TEXT NULL, Description TEXT NULL,
            CreatedAt TEXT NOT NULL, UpdatedAt TEXT NULL, CreatedBy TEXT NULL, UpdatedBy TEXT NULL,
            CreatedById TEXT NULL, LastModifiedById TEXT NULL, IsDeleted INTEGER NOT NULL,
            DeletedAt TEXT NULL, DeletedBy TEXT NULL);");
        await db.Database.ExecuteSqlRawAsync(@"CREATE TABLE AccountSegmentValues (
            Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, SegmentStructureId TEXT NOT NULL,
            IsDeleted INTEGER NOT NULL);");
    }

    private static AccountSegmentStructureService StructureService(ApplicationDbContext db, Guid tenantId, IFinanceAuditService audit)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(item => item.UserName).Returns("phase5.tests");
        return new AccountSegmentStructureService(db, currentUser.Object, audit,
            new AccountSegmentIdentityService(db),
            NullLogger<AccountSegmentStructureService>.Instance);
    }

    private static IFinanceAuditService Audit()
    {
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        return audit.Object;
    }

    private static async Task<(Guid TenantId, AccountSegmentStructure Company, AccountSegmentStructure Natural, SegmentLookupValue CompanyValue)>
        SeedStructureAsync(ApplicationDbContext db, string tenantCode)
    {
        var tenant = NewTenant(tenantCode); db.Tenants.Add(tenant);
        var company = Structure(tenant.Id, "COMPANY", 1, tenantCode.Length, false, true);
        var natural = Structure(tenant.Id, "NATURAL_ACCOUNT", 2, 4, true, false);
        var value = new SegmentLookupValue
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, SegmentStructureId = company.Id,
            SegmentValue = tenantCode, Description = tenantCode, IsActive = true
        };
        db.AccountSegmentStructures.AddRange(company, natural); db.SegmentLookupValues.Add(value);
        await db.SaveChangesAsync();
        return (tenant.Id, company, natural, value);
    }

    private static Tenant NewTenant(string code) => new() { Id = Guid.NewGuid(), Code = code, Name = code };
    private static AccountSegmentStructure Structure(Guid tenantId, string code, int position, int length, bool natural, bool lookup) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, SegmentCode = code, SegmentName = code,
        SegmentPosition = position, SegmentLength = length, DataType = natural ? "Numeric" : "Alphanumeric",
        SeparatorCharacter = position == 1 ? "-" : null, LookupTableRequired = lookup, IsNaturalAccount = natural,
        IsActive = true, LifecycleStatus = AccountSegmentLifecycleStatus.Active, RowVersion = [1]
    };

    private static List<AccountSegmentValueCreateDto> ValidValues(AccountSegmentStructure company,
        AccountSegmentStructure natural, SegmentLookupValue companyValue) =>
    [
        new() { SegmentStructureId = company.Id, SegmentPosition = 1, SegmentValue = companyValue.SegmentValue, SegmentLookupValueId = companyValue.Id },
        new() { SegmentStructureId = natural.Id, SegmentPosition = 2, SegmentValue = "6100" }
    ];

}
