using ErpSystem.Api.Services.Finance.Segments;
using ErpSystem.Api.Services.Finance;
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
        var operations = new PhaseFiveMigration().BuildUpOperations();
        operations.OfType<DropColumnOperation>().Should().Contain(item => item.Table == "AccountSegmentStructures" && item.Name == "IsMandatory");
        operations.OfType<AddColumnOperation>().Should().Contain(item => item.Table == "AccountSegmentStructures" && item.Name == "RowVersion");
        operations.OfType<AddColumnOperation>().Should().Contain(item => item.Table == "AccountSegmentStructures" && item.Name == "LifecycleStatus");
        operations.OfType<AddCheckConstraintOperation>().Should().ContainSingle(item =>
            item.Table == "AccountSegmentStructures" && item.Name == "CK_AccountSegmentStructures_LifecycleActive");
        operations.OfType<SqlOperation>().Should().Contain(item => item.Sql.Contains("Phase 5 preflight failed"));
        operations.OfType<CreateIndexOperation>().Count(item => item.Table == "AccountSegmentValues" && item.IsUnique).Should().Be(2);
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
        IsActive = true, LifecycleStatus = AccountSegmentLifecycleStatus.Active
    };

    private static List<AccountSegmentValueCreateDto> ValidValues(AccountSegmentStructure company,
        AccountSegmentStructure natural, SegmentLookupValue companyValue) =>
    [
        new() { SegmentStructureId = company.Id, SegmentPosition = 1, SegmentValue = companyValue.SegmentValue, SegmentLookupValueId = companyValue.Id },
        new() { SegmentStructureId = natural.Id, SegmentPosition = 2, SegmentValue = "6100" }
    ];

    private sealed class PhaseFiveMigration : AddGovernedAccountSegmentIdentity
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer"); Up(builder); return builder.Operations;
        }
    }
}
