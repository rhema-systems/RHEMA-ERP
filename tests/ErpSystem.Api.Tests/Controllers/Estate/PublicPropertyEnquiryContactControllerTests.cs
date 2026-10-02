using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Api.Filters;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Otp;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Estate;

public sealed class PublicPropertyEnquiryContactControllerTests
{
    [Fact]
    public async Task SmsDeliveryFailureReturnsActionablePublicErrorAndRetiresChallenge()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Sms.Setup(service => service.SendOtpAsync(
                fixture.Tenant.Id,
                "+233201234567",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider-secret-detail"));

        var result = await fixture.Controller.RequestPublicPropertyEnquiryContactChallenge(
            new PublicPropertyEnquiryContactChallengeRequestDto
            {
                ListingId = fixture.Listing.Id,
                Channel = "Phone",
                Contact = "+233201234567"
            }, CancellationToken.None);

        var unavailable = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(unavailable.Value);
        Assert.Equal("Verification code could not be sent", problem.Title);
        Assert.Equal("PUBLIC_ENQUIRY_SMS_DELIVERY_FAILED", problem.Extensions["code"]);
        Assert.Contains("tenant SMS settings", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<InvalidOperationException>(
            fixture.Controller.HttpContext.Items[
                SystemExceptionResultLoggingFilter.HandledExceptionItemKey]);
        var json = JsonSerializer.Serialize(unavailable.Value);
        Assert.Contains("PUBLIC_ENQUIRY_SMS_DELIVERY_FAILED", json, StringComparison.Ordinal);
        Assert.Contains("tenant SMS settings", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider-secret-detail", json, StringComparison.Ordinal);

        var challenge = await fixture.Db.EhcPublicPropertyEnquiryVerifications
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();
        Assert.True(challenge.IsDeleted);
    }

    [Fact]
    public async Task ChallengeForExistingCustomerRemainsGenericUntilOtpIsProven()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddApprovedCustomerAsync("known@example.test");

        var result = await fixture.Controller.RequestPublicPropertyEnquiryContactChallenge(
            new PublicPropertyEnquiryContactChallengeRequestDto
            {
                ListingId = fixture.Listing.Id,
                Channel = "Email",
                Contact = "known@example.test"
            }, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var json = JsonSerializer.Serialize(accepted.Value);
        Assert.Contains("If the contact can receive messages", json, StringComparison.Ordinal);
        Assert.DoesNotContain("customer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("businessPartner", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("portal", json, StringComparison.OrdinalIgnoreCase);
        fixture.Otp.Verify(service => service.CreateOtpAsync(
            fixture.Tenant.Id,
            OtpPurpose.PublicPropertyEnquiry,
            OtpChannel.Email,
            "known@example.test",
            TimeSpan.FromMinutes(10),
            5,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProfileAndCustomerOutcomeAreReturnedOnlyAfterValidOtp()
    {
        await using var fixture = await Fixture.CreateAsync();
        var customer = await fixture.AddApprovedCustomerAsync("known@example.test");
        fixture.Db.EhcPublicPropertyEnquiryContacts.Add(new EhcPublicPropertyEnquiryContact
        {
            TenantId = fixture.Tenant.Id,
            Channel = "Email",
            NormalizedContact = "known@example.test",
            ContactName = "Ama Returning",
            LastVerifiedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await fixture.Db.SaveChangesAsync();
        await fixture.RequestChallengeAsync("Email", "known@example.test");

        fixture.Otp.Setup(service => service.VerifyOtpAsync(
                fixture.Tenant.Id,
                OtpPurpose.PublicPropertyEnquiry,
                OtpChannel.Email,
                "known@example.test",
                "000000",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OtpVerifyResult(false, "Invalid code"));
        var invalid = await fixture.Controller.VerifyPublicPropertyEnquiryContact(
            fixture.Verification("Email", "known@example.test", "000000"), CancellationToken.None);
        var invalidJson = JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(invalid).Value);
        Assert.DoesNotContain("Ama Returning", invalidJson, StringComparison.Ordinal);
        Assert.DoesNotContain(customer.Id.ToString(), invalidJson, StringComparison.OrdinalIgnoreCase);

        fixture.AcceptOtp("Email", "known@example.test", "123456");
        var verified = await fixture.Controller.VerifyPublicPropertyEnquiryContact(
            fixture.Verification("Email", "KNOWN@example.test ", "123456"), CancellationToken.None);
        var verifiedJson = JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(verified).Value);
        using var document = JsonDocument.Parse(verifiedJson);
        var profile = document.RootElement.GetProperty("data").GetProperty("profile");

        Assert.Equal("Ama Returning", profile.GetProperty("contactName").GetString());
        Assert.Equal("known@example.test", profile.GetProperty("contactEmail").GetString());
        Assert.True(profile.GetProperty("linkedCustomer").GetBoolean());
        Assert.True(profile.GetProperty("requiresPortalLogin").GetBoolean());
        Assert.DoesNotContain(customer.Id.ToString(), verifiedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(customer.PartnerCode, verifiedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfileRecognizesApprovedCustomerThroughDirectUserEmailWhenPartnerEmailIsNull()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddApprovedCustomerThroughUserAsync(
            email: "direct.customer@example.test",
            phone: null,
            throughPartnerUserLink: false);

        await fixture.RequestChallengeAsync("Email", "direct.customer@example.test");
        fixture.AcceptOtp("Email", "direct.customer@example.test", "654321");
        var verified = await fixture.Controller.VerifyPublicPropertyEnquiryContact(
            fixture.Verification("Email", "direct.customer@example.test", "654321"), CancellationToken.None);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(verified).Value));
        var profile = document.RootElement.GetProperty("data").GetProperty("profile");
        Assert.True(profile.GetProperty("linkedCustomer").GetBoolean());
        Assert.True(profile.GetProperty("requiresPortalLogin").GetBoolean());
    }

    [Fact]
    public async Task ProfileRecognizesApprovedCustomerThroughLinkedUserPhoneWhenPartnerPhoneIsNull()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddApprovedCustomerThroughUserAsync(
            email: null,
            phone: "+233201234567",
            throughPartnerUserLink: true);

        await fixture.RequestChallengeAsync("Phone", "+233201234567");
        fixture.AcceptOtp("Phone", "+233201234567", "654321");
        var verified = await fixture.Controller.VerifyPublicPropertyEnquiryContact(
            fixture.Verification("Phone", "+233201234567", "654321"), CancellationToken.None);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(verified).Value));
        var profile = document.RootElement.GetProperty("data").GetProperty("profile");
        Assert.True(profile.GetProperty("linkedCustomer").GetBoolean());
        Assert.True(profile.GetProperty("requiresPortalLogin").GetBoolean());
    }

    [Fact]
    public async Task VerifiedLinkedCustomerCannotSubmitAnAnonymousEnquiry()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddApprovedCustomerAsync("customer@example.test");
        var token = await fixture.ObtainVerificationTokenAsync("Email", "customer@example.test");

        var result = await fixture.Controller.CreatePublicListingEnquiry(
            fixture.Listing.Id,
            fixture.Enquiry(Guid.NewGuid(), "Email", "customer@example.test", null, token),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var json = JsonSerializer.Serialize(conflict.Value);
        Assert.Contains("CUSTOMER_PORTAL_REQUIRED", json, StringComparison.Ordinal);
        Assert.Contains("external-portal", json, StringComparison.OrdinalIgnoreCase);
        fixture.Tickets.Verify(service => service.CreatePublicPropertyEnquiryAsync(
            It.IsAny<CreateEhcTicketRequestDto>(),
            It.IsAny<EhcPropertyListingContextDto>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReservedPropertyIsHiddenAndCannotAcceptAPreviouslyVerifiedEnquiry()
    {
        await using var fixture = await Fixture.CreateAsync();
        var token = await fixture.ObtainVerificationTokenAsync("Email", "prospect@example.test");
        fixture.Db.SalesAllocations.Add(new SalesAllocation
        {
            TenantId = fixture.Tenant.Id,
            SaleableSourceId = Guid.NewGuid(),
            SourceCode = "PROPERTY",
            SourceType = "PropertyRegister",
            AdapterKey = "property-register",
            SourceItemId = fixture.Listing.Id.ToString("D"),
            SourceItemCode = fixture.Listing.AssetCode,
            SourceItemName = fixture.Listing.Name,
            AllocationType = "Reservation",
            Status = "Reserved",
            ReservedUntil = DateTime.UtcNow.AddDays(7)
        });
        await fixture.Db.SaveChangesAsync();

        var listingsResult = Assert.IsType<OkObjectResult>(await fixture.Controller.GetPublicListings(
            cancellationToken: CancellationToken.None));
        using var listingsDocument = JsonDocument.Parse(JsonSerializer.Serialize(listingsResult.Value));
        Assert.Empty(listingsDocument.RootElement.GetProperty("data").EnumerateArray());

        var enquiryResult = await fixture.Controller.CreatePublicListingEnquiry(
            fixture.Listing.Id,
            fixture.Enquiry(Guid.NewGuid(), "Email", "prospect@example.test", null, token),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(enquiryResult);
        fixture.Tickets.Verify(service => service.CreatePublicPropertyEnquiryAsync(
            It.IsAny<CreateEhcTicketRequestDto>(),
            It.IsAny<EhcPropertyListingContextDto>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerificationGrantIsBoundToListingChannelAndNormalizedContact()
    {
        await using var fixture = await Fixture.CreateAsync();
        var otherListing = fixture.AvailableProperty("PROP-002");
        fixture.Db.EstateManagedAssets.Add(otherListing);
        await fixture.Db.SaveChangesAsync();
        var token = await fixture.ObtainVerificationTokenAsync("Email", "prospect@example.test");

        var wrongContact = await fixture.Controller.CreatePublicListingEnquiry(
            fixture.Listing.Id,
            fixture.Enquiry(Guid.NewGuid(), "Email", "other@example.test", null, token),
            CancellationToken.None);
        var wrongChannel = await fixture.Controller.CreatePublicListingEnquiry(
            fixture.Listing.Id,
            fixture.Enquiry(Guid.NewGuid(), "Phone", null, "+233201234567", token),
            CancellationToken.None);
        var wrongListing = await fixture.Controller.CreatePublicListingEnquiry(
            otherListing.Id,
            fixture.Enquiry(Guid.NewGuid(), "Email", "prospect@example.test", null, token),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(wrongContact);
        Assert.IsType<BadRequestObjectResult>(wrongChannel);
        Assert.IsType<BadRequestObjectResult>(wrongListing);
    }

    [Fact]
    public async Task VerificationGrantCanRetrySameSubmissionButRejectsDifferentSubmission()
    {
        await using var fixture = await Fixture.CreateAsync();
        var token = await fixture.ObtainVerificationTokenAsync("Email", "prospect@example.test");
        var firstSubmission = Guid.NewGuid();

        var first = await fixture.Controller.CreatePublicListingEnquiry(
            fixture.Listing.Id,
            fixture.Enquiry(firstSubmission, "Email", "prospect@example.test", null, token),
            CancellationToken.None);
        var replay = await fixture.Controller.CreatePublicListingEnquiry(
            fixture.Listing.Id,
            fixture.Enquiry(Guid.NewGuid(), "Email", "prospect@example.test", null, token),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);
        var blocked = Assert.IsType<BadRequestObjectResult>(replay);
        Assert.Contains("already been used", JsonSerializer.Serialize(blocked.Value), StringComparison.OrdinalIgnoreCase);
        var grant = await fixture.Db.EhcPublicPropertyEnquiryVerifications.AsNoTracking().SingleAsync();
        Assert.NotNull(grant.ConsumedAtUtc);
        Assert.Equal(firstSubmission, grant.ConsumedSubmissionId);
    }

    [Fact]
    public async Task ReturningVerifiedContactGroupsHistoricalAndNewEnquiriesUnderOneIdentity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var historicalTicket = new EhcTicket
        {
            TenantId = fixture.Tenant.Id,
            TicketNumber = "ENQ-HIST-001",
            TicketType = EhcTicketType.Enquiry,
            Source = EhcTicketSource.Web,
            // A closed historical enquiry may be followed by a new enquiry for the same
            // listing. Active duplicates remain blocked by the production duplicate guard.
            Status = EhcTicketStatus.Closed,
            Priority = EhcTicketPriority.Medium,
            Description = "Historical public enquiry",
            PropertyListingContextJson = JsonSerializer.Serialize(new EhcPropertyListingContextDto(
                "estate-public-listing", fixture.Listing.Id, fixture.Listing.AssetCode,
                fixture.Listing.Name, "Sale", "GHS", fixture.Listing.Location, 100000m,
                fixture.Listing.Id, null, null, "Ama Prospect", "Ama Prospect",
                "prospect@example.test", null))
        };
        fixture.Db.EhcTickets.Add(historicalTicket);
        await fixture.Db.SaveChangesAsync();

        var token = await fixture.ObtainVerificationTokenAsync("Email", "prospect@example.test");
        Assert.Empty(await fixture.Db.EhcPublicPropertyEnquiryContacts.AsNoTracking().ToListAsync());
        Assert.Null((await fixture.Db.EhcTickets.AsNoTracking().SingleAsync(item => item.Id == historicalTicket.Id))
            .PublicPropertyEnquiryContactId);

        var submitted = await fixture.Controller.CreatePublicListingEnquiry(
            fixture.Listing.Id,
            fixture.Enquiry(Guid.NewGuid(), "Email", "prospect@example.test", null, token),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(submitted);
        var contact = await fixture.Db.EhcPublicPropertyEnquiryContacts.AsNoTracking().SingleAsync();
        var linkedHistorical = await fixture.Db.EhcTickets.AsNoTracking().SingleAsync(item => item.Id == historicalTicket.Id);
        Assert.Equal(contact.Id, linkedHistorical.PublicPropertyEnquiryContactId);
        fixture.Tickets.Verify(service => service.CreatePublicPropertyEnquiryAsync(
            It.IsAny<CreateEhcTicketRequestDto>(),
            It.Is<EhcPropertyListingContextDto>(context => context.PublicContactId == contact.Id),
            It.IsAny<Guid>(),
            fixture.Tenant.Id,
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(SqliteConnection connection, ApplicationDbContext db, Tenant tenant, EstateManagedAsset listing)
        {
            this.connection = connection;
            Db = db;
            Tenant = tenant;
            Listing = listing;
            Otp = new Mock<IOtpService>(MockBehavior.Strict);
            Tickets = new Mock<IEhcTicketService>(MockBehavior.Strict);
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(service => service.TenantId).Returns((Guid?)null);
            var captcha = new Mock<ICaptchaVerificationService>();
            captcha.Setup(service => service.EnsureCaptchaValidAsync(
                    tenant.Id,
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var notifications = new Mock<INotificationService>();
            notifications.Setup(service => service.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    true,
                    false))
                .Returns(Task.CompletedTask);
            Sms = new Mock<ITenantSmsSender>();
            Sms.Setup(service => service.SendOtpAsync(
                    tenant.Id,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Otp.Setup(service => service.CreateOtpAsync(
                    tenant.Id,
                    OtpPurpose.PublicPropertyEnquiry,
                    It.IsAny<OtpChannel>(),
                    It.IsAny<string>(),
                    TimeSpan.FromMinutes(10),
                    5,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("123456");
            Tickets.Setup(service => service.CreatePublicPropertyEnquiryAsync(
                    It.IsAny<CreateEhcTicketRequestDto>(),
                    It.IsAny<EhcPropertyListingContextDto>(),
                    It.IsAny<Guid>(),
                    tenant.Id,
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EhcTicketDetailDto
                {
                    Id = Guid.NewGuid(),
                    TicketNumber = "ENQ-NEW-001",
                    Status = EhcTicketStatus.New
                });

            Controller = new EstateExternalDocumentsController(
                db,
                user.Object,
                null!,
                null!,
                null!,
                notifications.Object,
                null!,
                Tickets.Object,
                captcha.Object,
                Otp.Object,
                Sms.Object,
                NullLogger<EstateExternalDocumentsController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            Controller.HttpContext.Request.Host = new HostString("public.example.test");
        }

        public ApplicationDbContext Db { get; }
        public Tenant Tenant { get; }
        public EstateManagedAsset Listing { get; }
        public EstateExternalDocumentsController Controller { get; }
        public Mock<IOtpService> Otp { get; }
        public Mock<ITenantSmsSender> Sms { get; }
        public Mock<IEhcTicketService> Tickets { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options);
            await CreateFocusedSqliteSchemaAsync(db);
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Code = "PUBLIC",
                Name = "Public tenant",
                Status = TenantStatus.Active,
                IsDefaultForPublicUsers = true
            };
            var listing = AvailableProperty(tenant.Id, "PROP-001");
            var category = new EhcTicketCategory
            {
                TenantId = tenant.Id,
                Code = "PROPERTY-LISTING",
                Name = "Property enquiry",
                AppliesToType = EhcTicketType.Enquiry
            };
            var workflowActor = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserName = "external",
                NormalizedUserName = "EXTERNAL",
                Email = "external@default.com",
                NormalizedEmail = "EXTERNAL@DEFAULT.COM",
                IsActive = true
            };
            db.AddRange(tenant, listing, category, workflowActor);
            await db.SaveChangesAsync();
            return new Fixture(connection, db, tenant, listing);
        }

        private static async Task CreateFocusedSqliteSchemaAsync(ApplicationDbContext db)
        {
            // ApplicationDbContext is SQL Server-first and its model deliberately contains
            // SQL Server-only store types and check expressions (nvarchar(max), LEN, etc.).
            // Generating the whole schema and trying to rewrite it makes this focused test
            // depend on every unrelated module's DDL. Instead, create only the relational
            // tables exercised by these controller tests from EF metadata. All mapped
            // columns are present so normal materialization/SaveChanges/ExecuteUpdate paths
            // run through SQLite; production constraints remain covered by migrations.
            var entityTypes = new[]
            {
                typeof(Tenant),
                typeof(ApplicationUser),
                typeof(EstateManagedAsset),
                typeof(EstateLandDemarcation),
                typeof(EhcTicketCategory),
                typeof(EhcTicket),
                typeof(EhcPublicPropertyEnquiryContact),
                typeof(EhcPublicPropertyEnquiryVerification),
                typeof(BusinessPartner),
                typeof(BusinessPartnerRole),
                typeof(BusinessPartnerUser),
                typeof(SalesAllocation)
            };

            var mappedEntities = entityTypes
                .Select(type => db.Model.FindEntityType(type)
                    ?? throw new InvalidOperationException($"The test entity {type.Name} is not mapped."))
                .GroupBy(entity => entity.GetTableName()
                    ?? throw new InvalidOperationException($"The test entity {entity.DisplayName()} has no table."),
                    StringComparer.Ordinal);

            foreach (var tableGroup in mappedEntities)
            {
                var storeObject = StoreObjectIdentifier.Table(tableGroup.Key, tableGroup.First().GetSchema());
                var columns = tableGroup
                    .SelectMany(entity => entity.GetProperties())
                    .Select(property => new
                    {
                        Name = property.GetColumnName(storeObject),
                        Property = property
                    })
                    .Where(column => !string.IsNullOrWhiteSpace(column.Name))
                    .GroupBy(column => column.Name!, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToList();

                var keyColumns = tableGroup
                    .SelectMany(entity => entity.FindPrimaryKey()?.Properties ?? Array.Empty<IProperty>())
                    .Select(property => property.GetColumnName(storeObject))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var definitions = columns
                    .Select(column => $"{Quote(column.Name!)} {SqliteAffinity(column.Property)}")
                    .ToList();
                if (keyColumns.Count > 0)
                    definitions.Add($"PRIMARY KEY ({string.Join(", ", keyColumns.Select(Quote))})");

                var createTable = $"CREATE TABLE {Quote(tableGroup.Key)} ({string.Join(", ", definitions)});";
                await db.Database.ExecuteSqlRawAsync(createTable);
            }

            // This is the one database constraint the controller relies on while resolving
            // simultaneous first submissions for a verified contact.
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX "UX_Test_PublicEnquiryContact_Identity"
                ON "EhcPublicPropertyEnquiryContacts" ("TenantId", "Channel", "NormalizedContact")
                WHERE "IsDeleted" = 0;
                """);
        }

        private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

        private static string SqliteAffinity(IProperty property)
        {
            var providerType = property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;
            var type = Nullable.GetUnderlyingType(providerType) ?? providerType;
            if (type == typeof(byte[])) return "BLOB";
            if (type == typeof(bool) || type.IsEnum ||
                type == typeof(byte) || type == typeof(sbyte) ||
                type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) ||
                type == typeof(long) || type == typeof(ulong))
                return "INTEGER";
            if (type == typeof(float) || type == typeof(double)) return "REAL";
            return "TEXT";
        }

        public EstateManagedAsset AvailableProperty(string code) => AvailableProperty(Tenant.Id, code);

        private static EstateManagedAsset AvailableProperty(Guid tenantId, string code) => new()
        {
            TenantId = tenantId,
            AssetCode = code,
            Name = $"Property {code}",
            AssetType = EstateManagedAssetType.Property,
            SourceType = EstateManagedAssetSourceType.Imported,
            Status = EstateManagedAssetStatus.Available,
            Location = "Accra",
            IsPublishedToExternalPortal = true,
            ExternalListingStatus = "Published",
            ExternalListingType = "Sale",
            IsAvailableForSale = true,
            ExternalSalePrice = 100000m,
            ExternalListingCurrency = "GHS"
        };

        public async Task<BusinessPartner> AddApprovedCustomerAsync(string email)
        {
            var partner = new BusinessPartner
            {
                TenantId = Tenant.Id,
                PartnerCode = $"CUS-{Guid.NewGuid():N}"[..12],
                PartnerName = "Known Customer",
                PartnerType = "Customer",
                PrimaryEmail = email,
                IsActive = true,
                ApprovalStatus = "Approved"
            };
            var role = new BusinessPartnerRole
            {
                TenantId = Tenant.Id,
                BusinessPartner = partner,
                RoleType = BusinessPartnerRoleType.Customer,
                Status = BusinessPartnerRoleStatus.Active
            };
            Db.AddRange(partner, role);
            await Db.SaveChangesAsync();
            return partner;
        }

        public async Task<BusinessPartner> AddApprovedCustomerThroughUserAsync(
            string? email,
            string? phone,
            bool throughPartnerUserLink)
        {
            var userId = Guid.NewGuid();
            var user = new ApplicationUser
            {
                Id = userId,
                TenantId = Tenant.Id,
                UserName = $"customer-{userId:N}",
                NormalizedUserName = $"CUSTOMER-{userId:N}",
                Email = email,
                NormalizedEmail = email?.ToUpperInvariant(),
                PhoneNumber = phone,
                IsActive = true
            };
            var partner = new BusinessPartner
            {
                TenantId = Tenant.Id,
                PartnerCode = $"CUS-{Guid.NewGuid():N}"[..12],
                PartnerName = "User-linked Customer",
                PartnerType = "Customer",
                PrimaryEmail = null,
                PrimaryPhone = null,
                UserId = throughPartnerUserLink ? null : user.Id,
                IsActive = true,
                ApprovalStatus = "Approved"
            };
            var role = new BusinessPartnerRole
            {
                TenantId = Tenant.Id,
                BusinessPartner = partner,
                RoleType = BusinessPartnerRoleType.Customer,
                Status = BusinessPartnerRoleStatus.Active
            };

            Db.AddRange(user, partner, role);
            if (throughPartnerUserLink)
            {
                Db.BusinessPartnerUsers.Add(new BusinessPartnerUser
                {
                    TenantId = Tenant.Id,
                    BusinessPartner = partner,
                    User = user,
                    Role = "User",
                    IsActive = true
                });
            }

            await Db.SaveChangesAsync();
            return partner;
        }

        public async Task RequestChallengeAsync(string channel, string contact)
        {
            var result = await Controller.RequestPublicPropertyEnquiryContactChallenge(
                new PublicPropertyEnquiryContactChallengeRequestDto
                {
                    ListingId = Listing.Id,
                    Channel = channel,
                    Contact = contact
                }, CancellationToken.None);
            Assert.IsType<AcceptedResult>(result);
        }

        public void AcceptOtp(string channel, string contact, string code)
        {
            var otpChannel = channel == "Email" ? OtpChannel.Email : OtpChannel.Sms;
            Otp.Setup(service => service.VerifyOtpAsync(
                    Tenant.Id,
                    OtpPurpose.PublicPropertyEnquiry,
                    otpChannel,
                    contact.Trim().ToLowerInvariant(),
                    code,
                    true,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OtpVerifyResult(true, null));
        }

        public PublicPropertyEnquiryContactVerificationRequestDto Verification(string channel, string contact, string code) => new()
        {
            ListingId = Listing.Id,
            Channel = channel,
            Contact = contact,
            OtpCode = code
        };

        public async Task<string> ObtainVerificationTokenAsync(string channel, string contact)
        {
            await RequestChallengeAsync(channel, contact);
            AcceptOtp(channel, contact, "123456");
            var result = await Controller.VerifyPublicPropertyEnquiryContact(
                Verification(channel, contact, "123456"), CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result);
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
            return document.RootElement.GetProperty("data").GetProperty("verificationToken").GetString()!;
        }

        public PublicPropertyListingEnquiryRequestDto Enquiry(
            Guid submissionId,
            string channel,
            string? email,
            string? phone,
            string token) => new()
        {
            SubmissionId = submissionId,
            Message = "Please arrange a viewing.",
            ContactName = "Ama Prospect",
            ContactEmail = email,
            ContactPhone = phone,
            PreferredContactMethod = channel,
            ContactVerificationToken = token
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
