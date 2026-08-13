# Finance Integration Consumer-Test Template

Every producer-module adapter should prove what it sends to Finance. The shared test assertion lives in `tests/ErpSystem.Api.Tests/Services/Finance/FinanceConsumerContractAssertions.cs`.

## Minimal capture pattern

```csharp
FinancePostingRequestDto? captured = null;
var finance = new Mock<IFinancePostingEngine>();
finance
    .Setup(service => service.PostAsync(
        It.IsAny<FinancePostingRequestDto>(),
        It.IsAny<CancellationToken>()))
    .Callback<FinancePostingRequestDto, CancellationToken>((request, _) => captured = request)
    .ReturnsAsync(new FinancePostingResultDto
    {
        PostingEventId = Guid.NewGuid(),
        JournalEntryId = Guid.NewGuid(),
        JournalEntryNumber = "JE-CONTRACT-001",
        PostingStatus = "Posted",
        FunctionalCurrencyCode = "GHS"
    });

// Execute the approved producer operation through its real adapter.
await adapter.PostAsync(sourceDocument);

captured.Should().NotBeNull();
captured!.ShouldSatisfyPostingContract(
    expectedOriginModule: "Inventory",
    expectedDocumentType: "StockAdjustment",
    expectedDocumentId: sourceDocument.Id,
    expectedTenantId: sourceDocument.TenantId);

// Add contract-specific assertions: exact accounts, amounts, currency evidence and back-references.
captured.Lines.Should().ContainSingle(line =>
    line.AccountId == expectedControlAccountId && line.CreditAmount == expectedAmount);
```

## Required companion tests

The minimal test is not sufficient on its own. Add:

- an identical retry test proving one journal/posting event and stable returned references;
- a Finance failure test proving the source remains retryable and is not marked posted;
- cross-tenant and missing-account/configuration tests;
- closed/locked-period behaviour when the adapter controls the posting date;
- AR/AP visibility tests for allocations, aging, statements and reversals;
- SQL Server coverage for migrations, unique constraints, transactions or concurrency.

## CI registration

Add the focused test class or method to `api_filter` in `.github/workflows/finance-integration-gate.yml`. Update `assert_trx_count` to the exact new total. The exact-count assertion is deliberate: a renamed or skipped contract must make the gate fail instead of giving a false green result.
