using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class RecurringJournalMultilineTests
{
    [Fact]
    public void Create_AppliesAnyNumberOfOrderedBalancedLinesWithEvidence()
    {
        var template = Template();
        var inputs = BalancedFourLines();

        RecurringJournalLineDefinitionEditor.Apply(template, inputs, DateTime.UtcNow, Guid.NewGuid(), "maker",
            allowExistingLineIds: false);

        template.Lines.Should().HaveCount(4);
        template.Lines.OrderBy(line => line.LineNumber).Select(line => line.LineNumber)
            .Should().Equal(1, 2, 3, 4);
        template.Lines.Select(line => line.Id).Should().OnlyHaveUniqueItems();
        template.Lines.Sum(line => line.IsDebit ? line.FixedAmount : 0m).Should().Be(1000m);
        template.Lines.Sum(line => line.IsDebit ? 0m : line.FixedAmount).Should().Be(1000m);
        template.Lines.Should().Contain(line => line.DimensionValuesJson == "{\"DEPT\":\"FIN\"}");
    }

    [Fact]
    public void Edit_PreservesRetainedIdentityAndEvidenceWhileAddingRemovingAndReordering()
    {
        var template = Template();
        RecurringJournalLineDefinitionEditor.Apply(template, BalancedFourLines(), DateTime.UtcNow.AddMinutes(-5),
            Guid.NewGuid(), "maker", allowExistingLineIds: false);
        var original = template.Lines.OrderBy(line => line.LineNumber).ToList();
        var retainedId = original[1].Id;
        var removedId = original[0].Id;

        RecurringJournalLineDefinitionEditor.Apply(template,
        [
            new() { Id = retainedId, AccountId = original[1].AccountId, IsDebit = true, FixedAmount = 1000m,
                Description = "Retained source line", DimensionValuesJson = "{\"DEPT\":\"OPS\"}" },
            new() { AccountId = Guid.NewGuid(), IsDebit = false, FixedAmount = 1000m,
                Description = "New balancing line", DimensionValuesJson = "{}" }
        ], DateTime.UtcNow, Guid.NewGuid(), "editor", allowExistingLineIds: true);

        var activeLines = template.Lines.Where(line => !line.IsDeleted).ToList();
        activeLines.Should().HaveCount(2);
        activeLines.Should().Contain(line => line.Id == retainedId && line.LineNumber == 2 &&
            line.DimensionValuesJson == "{\"DEPT\":\"OPS\"}");
        activeLines.Should().NotContain(line => line.Id == removedId);
        activeLines.Single(line => line.LineNumber == 5).Id.Should().NotBe(retainedId);
    }

    [Fact]
    public void Edit_RejectsLineIdentityOwnedOutsideTheTemplate()
    {
        var template = Template();
        RecurringJournalLineDefinitionEditor.Apply(template, BalancedFourLines(), DateTime.UtcNow,
            Guid.NewGuid(), "maker", allowExistingLineIds: false);

        var action = () => RecurringJournalLineDefinitionEditor.Apply(template,
        [
            new() { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), IsDebit = true, FixedAmount = 10m },
            new() { AccountId = Guid.NewGuid(), IsDebit = false, FixedAmount = 10m }
        ], DateTime.UtcNow, Guid.NewGuid(), "editor", allowExistingLineIds: true);

        action.Should().Throw<InvalidOperationException>().WithMessage("*does not belong*");
    }

    [Fact]
    public async Task Edit_PersistsRetainedAndNewLinesAndDeletesRemovedLines()
    {
        await using var db = CreateContext();
        var template = Template();
        db.Tenants.Add(new Tenant
        {
            Id = template.TenantId, Name = "Tenant", Code = $"T{Guid.NewGuid():N}"[..8],
            BaseCurrency = "GHS", Status = TenantStatus.Active
        });
        RecurringJournalLineDefinitionEditor.Apply(template, BalancedFourLines(), DateTime.UtcNow,
            Guid.NewGuid(), "maker", allowExistingLineIds: false);
        db.RecurringJournalTemplates.Add(template);
        await db.SaveChangesAsync();
        var retained = template.Lines.OrderBy(line => line.LineNumber).Skip(1).First();
        var removedIds = template.Lines.Where(line => line.Id != retained.Id).Select(line => line.Id).ToList();

        var edit = RecurringJournalLineDefinitionEditor.Apply(template,
        [
            new() { Id = retained.Id, AccountId = retained.AccountId, IsDebit = true, FixedAmount = 900m,
                DimensionValuesJson = retained.DimensionValuesJson },
            new() { AccountId = Guid.NewGuid(), IsDebit = false, FixedAmount = 900m, DimensionValuesJson = "{}" }
        ], DateTime.UtcNow, Guid.NewGuid(), "editor", allowExistingLineIds: true);
        db.RecurringJournalTemplateLines.AddRange(edit.AddedLines);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var persisted = await db.RecurringJournalTemplates.Include(item => item.Lines)
            .SingleAsync(item => item.Id == template.Id);
        persisted.Lines.Where(line => !line.IsDeleted).Should().HaveCount(2);
        persisted.Lines.Should().Contain(line => line.Id == retained.Id && line.LineNumber == 2);
        persisted.Lines.Should().Contain(line => !line.IsDeleted && line.LineNumber == 5);
        persisted.Lines.Where(line => !line.IsDeleted).Should().NotContain(line => removedIds.Contains(line.Id));
    }

    private static RecurringJournalTemplate Template() => new()
    {
        Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), TemplateNumber = "RJ-001", Name = "Multiline accrual",
        EffectiveFrom = new DateOnly(2026, 9, 1), Frequency = RecurrenceFrequency.Monthly,
        RecurrenceRuleJson = "{}"
    };

    private static IReadOnlyList<RecurringJournalTemplateLineInputDto> BalancedFourLines() =>
    [
        new() { AccountId = Guid.NewGuid(), IsDebit = true, FixedAmount = 600m,
            Description = "Expense A", DimensionValuesJson = "{\"DEPT\":\"FIN\"}" },
        new() { AccountId = Guid.NewGuid(), IsDebit = true, FixedAmount = 400m,
            Description = "Expense B", DimensionValuesJson = "{}" },
        new() { AccountId = Guid.NewGuid(), IsDebit = false, FixedAmount = 750m,
            Description = "Accrual A", DimensionValuesJson = "{}" },
        new() { AccountId = Guid.NewGuid(), IsDebit = false, FixedAmount = 250m,
            Description = "Accrual B", DimensionValuesJson = "{}" }
    ];

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"recurring-multiline-{Guid.NewGuid():N}").Options;
        return new ApplicationDbContext(options);
    }
}
