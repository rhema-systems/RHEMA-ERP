using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Estate;

public sealed class PublicPropertyEnquiryContactContractTests
{
    [Theory]
    [InlineData(nameof(EstateExternalDocumentsController.RequestPublicPropertyEnquiryContactChallenge))]
    [InlineData(nameof(EstateExternalDocumentsController.VerifyPublicPropertyEnquiryContact))]
    [InlineData(nameof(EstateExternalDocumentsController.CreatePublicListingEnquiry))]
    public void AnonymousContactEndpointsRetainSensitiveRateLimiting(string methodName)
    {
        var method = typeof(EstateExternalDocumentsController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(
            "SensitivePolicy",
            method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void EmailSelectionAcceptsOnlyEmailAndRequiresVerificationGrant()
    {
        var valid = Request(
            preferredContactMethod: "Email",
            email: "prospect@example.test",
            phone: null,
            verificationToken: new string('a', 64));

        Assert.Empty(Validate(valid));
        Assert.Contains(Validate(Request("Email", "prospect@example.test", "+233201234567", new string('a', 64))), item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.ContactPhone)));
        Assert.Contains(Validate(Request("Email", null, null, new string('a', 64))), item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.ContactEmail)));
        Assert.Contains(Validate(Request("Email", "prospect@example.test", null, string.Empty)), item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.ContactVerificationToken)));
    }

    [Fact]
    public void PhoneSelectionAcceptsOnlyValidatedInternationalPhone()
    {
        var valid = Request(
            preferredContactMethod: "Phone",
            email: null,
            phone: "+233201234567",
            verificationToken: new string('b', 64));

        Assert.Empty(Validate(valid));
        Assert.Contains(Validate(Request("Phone", "prospect@example.test", "+233201234567", new string('b', 64))), item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.ContactEmail)));
        Assert.Contains(Validate(Request("Phone", null, "020 123 4567", new string('b', 64))), item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.ContactPhone)));
        Assert.Contains(Validate(Request("Phone", null, "+023201234567", new string('b', 64))), item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.ContactPhone)));
    }

    [Fact]
    public void PublicContractRejectsEitherAndAlternatePhone()
    {
        var invalid = Request(
            preferredContactMethod: "Either",
            email: "prospect@example.test",
            phone: "+233201234567",
            verificationToken: new string('c', 64),
            alternativePhone: "+233241234567");

        var errors = Validate(invalid);

        Assert.Contains(errors, item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.PreferredContactMethod)));
        Assert.Contains(errors, item =>
            item.MemberNames.Contains(nameof(PublicPropertyListingEnquiryRequestDto.AlternativePhoneNumber)));
    }

    [Fact]
    public void VerifiedContactIdentityIsUniquePerTenantChannelAndNormalizedContact()
    {
        using var db = Database();
        var entity = db.Model.FindEntityType(typeof(EhcPublicPropertyEnquiryContact));

        Assert.NotNull(entity);
        var index = Assert.Single(entity!.GetIndexes(), candidate =>
            candidate.IsUnique &&
            candidate.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(EhcPublicPropertyEnquiryContact.TenantId),
                nameof(EhcPublicPropertyEnquiryContact.Channel),
                nameof(EhcPublicPropertyEnquiryContact.NormalizedContact)
            }));
        Assert.Equal("[IsDeleted] = 0", index.GetFilter());
    }

    [Fact]
    public void EnquiriesLinkToDurableVerifiedContactForHistoryGrouping()
    {
        using var db = Database();
        var ticket = db.Model.FindEntityType(typeof(EhcTicket));
        var foreignKey = Assert.Single(ticket!.GetForeignKeys(), candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(EhcTicket.PublicPropertyEnquiryContactId) }));

        Assert.Equal(typeof(EhcPublicPropertyEnquiryContact), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void VerificationGrantHashIsUniqueAndRawOtpHasNoDatabaseField()
    {
        using var db = Database();
        var entity = db.Model.FindEntityType(typeof(EhcPublicPropertyEnquiryVerification));

        Assert.NotNull(entity);
        Assert.Single(entity!.GetIndexes(), candidate => candidate.IsUnique &&
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(EhcPublicPropertyEnquiryVerification.VerificationTokenHash) }));
        Assert.Null(entity.FindProperty("OtpCode"));
        Assert.Null(entity.FindProperty("VerificationToken"));
    }

    private static PublicPropertyListingEnquiryRequestDto Request(
        string preferredContactMethod,
        string? email,
        string? phone,
        string verificationToken,
        string? alternativePhone = null) => new()
        {
            SubmissionId = Guid.NewGuid(),
            Message = "Please arrange a viewing.",
            ContactName = "Ama Prospect",
            ContactEmail = email,
            ContactPhone = phone,
            AlternativePhoneNumber = alternativePhone,
            PreferredContactMethod = preferredContactMethod,
            ContactVerificationToken = verificationToken
        };

    private static IReadOnlyList<ValidationResult> Validate(PublicPropertyListingEnquiryRequestDto request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static ApplicationDbContext Database() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
