using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Ehc;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Ehc;

public sealed class EhcExternalTicketPolicyTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Guid _tenant = Guid.NewGuid();

    [Theory]
    [InlineData(EhcTicketSource.Mobile)]
    [InlineData(EhcTicketSource.Email)]
    [InlineData(EhcTicketSource.Internal)]
    [InlineData(EhcTicketSource.PhoneCall)]
    [InlineData(EhcTicketSource.Sms)]
    [InlineData(EhcTicketSource.WhatsApp)]
    [InlineData((EhcTicketSource)999)]
    public async Task Supplier_cannot_override_category_type_default_priority_or_website_channel(EhcTicketSource source)
    {
        var category = Category(EhcTicketType.Complaint);
        await _db.SaveChangesAsync();
        var request = Request(category.Id); request.Source = source;
        await Apply(request);
        request.TicketType.Should().Be(EhcTicketType.Complaint);
        request.Priority.Should().Be(EhcTicketPriority.Medium);
        request.Source.Should().Be(EhcTicketSource.Web);
    }

    [Fact]
    public async Task Nested_subcategory_inherits_nearest_configured_type()
    {
        var root = Category(EhcTicketType.Helpdesk);
        var middle = Category(EhcTicketType.Enquiry, root.Id);
        var child = Category(null, middle.Id);
        await _db.SaveChangesAsync();
        var request = Request(root.Id); request.SubcategoryId = child.Id;
        await Apply(request);
        request.TicketType.Should().Be(EhcTicketType.Enquiry);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    [InlineData("unmapped")]
    [InlineData("invalid-type")]
    [InlineData("wrong-subtree")]
    [InlineData("cycle")]
    public async Task Unavailable_or_unmapped_category_configuration_is_rejected(string kind)
    {
        var root = Category(EhcTicketType.Helpdesk);
        var request = Request(root.Id);
        if (kind == "missing") request.CategoryId = Guid.NewGuid();
        if (kind == "foreign") root.TenantId = Guid.NewGuid();
        if (kind == "deleted") root.IsDeleted = true;
        if (kind == "unmapped") root.AppliesToType = null;
        if (kind == "invalid-type") root.AppliesToType = (EhcTicketType)999;
        if (kind == "wrong-subtree") request.SubcategoryId = Category(EhcTicketType.Complaint).Id;
        if (kind == "cycle")
        {
            var child = Category(EhcTicketType.Complaint); child.ParentCategoryId = child.Id;
            request.SubcategoryId = child.Id;
        }
        await _db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Apply(request)).Should().ThrowAsync<ArgumentException>();
        (await _db.Set<EhcTicket>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Active_medium_wins_over_sort_order_and_supplier_critical()
    {
        var category = Category(EhcTicketType.Helpdesk);
        Priority(EhcTicketPriority.Critical, true, 0);
        Priority(EhcTicketPriority.Medium, true, 100);
        await _db.SaveChangesAsync();
        var request = Request(category.Id);
        await Apply(request);
        request.Priority.Should().Be(EhcTicketPriority.Medium);
    }

    [Fact]
    public async Task Disabled_medium_uses_first_active_configured_priority_with_existing_order()
    {
        var category = Category(EhcTicketType.Helpdesk);
        Priority(EhcTicketPriority.Medium, false, 0);
        Priority(EhcTicketPriority.High, true, 20);
        Priority(EhcTicketPriority.Low, true, 10);
        await _db.SaveChangesAsync();
        var request = Request(category.Id);
        await Apply(request);
        request.Priority.Should().Be(EhcTicketPriority.Low);
    }

    [Fact]
    public async Task All_disabled_priorities_produce_actionable_configuration_error()
    {
        var category = Category(EhcTicketType.Helpdesk);
        Priority(EhcTicketPriority.Medium, false, 0);
        await _db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Apply(Request(category.Id))).Should().ThrowAsync<ArgumentException>()
            .WithMessage("*No helpdesk priority is enabled*");
    }

    [Fact]
    public async Task Another_tenants_priority_configuration_cannot_change_supplier_default()
    {
        var category = Category(EhcTicketType.Helpdesk);
        var other = Priority(EhcTicketPriority.Critical, true, 0); other.TenantId = Guid.NewGuid();
        await _db.SaveChangesAsync();
        var request = Request(category.Id);
        await Apply(request);
        request.Priority.Should().Be(EhcTicketPriority.Medium);
    }

    private EhcTicketCategory Category(EhcTicketType? type, Guid? parent = null)
    {
        var category = new EhcTicketCategory { TenantId = _tenant, Code = Guid.NewGuid().ToString("N"),
            Name = "Configured category", AppliesToType = type, ParentCategoryId = parent };
        _db.Add(category); return category;
    }
    private EhcTicketPriorityLevel Priority(EhcTicketPriority priority, bool active, int sort)
    {
        var value = new EhcTicketPriorityLevel { TenantId = _tenant, Priority = priority, DisplayName = priority.ToString(), IsActive = active, SortOrder = sort };
        _db.Add(value); return value;
    }
    private static CreateEhcTicketRequestDto Request(Guid categoryId) => new()
    { CategoryId = categoryId, TicketType = EhcTicketType.Enquiry, Priority = EhcTicketPriority.Critical, Source = EhcTicketSource.Email, Description = "Supplier request" };
    private Task Apply(CreateEhcTicketRequestDto request) => EhcExternalTicketPolicy.ApplyAsync(new UnitOfWork(_db), _tenant, request);
    public void Dispose() => _db.Dispose();
}
