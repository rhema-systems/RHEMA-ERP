# TDC cash/bank transaction reversal and trace

## Outcome

`FIN-LIM-0011` is resolved in code for Finance-owned, posted cash receipts, cash payments, and paired bank transfers. The implementation extends the existing `CashTransactionService` and central `IFinancePostingEngine`; it does not create a parallel journal path.

The original posted transaction remains immutable. A successful correction creates an opposite posted journal plus one opposite operational bank row for a receipt/payment or a complete opposite transfer pair. Original and compensating records retain bidirectional lineage.

## Supported and deliberately excluded corrections

Supported:

- standalone cash/bank receipt posted as `CashBankReceipt`;
- standalone cash/bank payment posted as `CashBankPayment`;
- same-currency paired bank transfer posted as `CashBankTransfer`.

The command rejects:

- unposted, cancelled, or already-compensating rows;
- reconciled receipt/payment rows;
- a transfer when either bank leg is reconciled, missing, unposted, or linked to another journal;
- cash rows created as operational mirrors by AP or AR, because those subledgers own the full settlement correction;
- deposit and returned-cheque records, which retain their dedicated Banking & Settlement workflows;
- cross-currency bank transfers, which remain under `FIN-LIM-0021`.

This ownership guard prevents two Finance workflows from reversing the same accounting event.

## Accounting and operational behaviour

The tenant `FinanceSettings` reversal policy resolves the reason and posting date. The default TDC posture is the current open fiscal period, with a minimum 20-character reason unless the tenant changes that configuration.

Receipt reversal:

- reverses the original GL journal through the posting engine;
- creates a posted cash payment linked to the receipt;
- decreases the same bank account's current and available balance snapshots.

Payment reversal:

- reverses the original GL journal;
- creates a posted cash receipt linked to the payment;
- increases the same bank snapshots.

Transfer reversal:

- reverses the original source-bank/destination-bank journal;
- creates an opposite transfer from the original destination back to the original source;
- links each compensating leg to the original leg affecting that same bank;
- restores both bank balance snapshots.

The journal, operational rows, source lineage, bank snapshots, audit event, and both transfer legs commit under one serializable database transaction. Deterministic posting keys and stored reversal links make retries idempotent. After a rollback, EF tracked state is cleared before recording failure audit so an audit save cannot leak failed business changes.

## Access and audit controls

- Read/list/trace operations use Finance `Read` bank scope.
- Capture, workflow submit/cancel, delete, reconciliation marking, and posting use Finance `Operate` scope.
- Workflow approve/reject/return and reversal use Finance `Approve` scope.
- Transfers require the applicable scope on both bank accounts.
- Reversal additionally requires `Finance.CashBank.Transactions.Reverse`.
- Audit events cover reversal success, reversal failure, and trace viewing.

The API returns `403 Forbidden` for bank-scope denial rather than presenting it as a validation error.

## API and UI

- `GET /api/finance/cash-transactions/{id}/trace`
- `POST /api/finance/cash-transactions/{id}/reverse`
- `/finance/cash/transactions/{id}`

The detail screen shows transaction state, original/reversal journal links, related transfer or correction rows, journal lines, audit history, the tenant reason rule, and an optional preferred reversal date. The reversal action is hidden without the dedicated permission, while the API remains authoritative.

## Persistence

Migration `20260802142906_AddCashBankTransactionReversals` adds:

- `IsReversed`;
- `ReversalOfCashTransactionId` and `ReversalCashTransactionId`;
- `ReversalJournalEntryId` and `ReversalPostingEventId`;
- reversal date, timestamp, user, and reason.

All correction foreign keys use restricted deletion to preserve the evidence chain.

## Verification

Focused regression coverage verifies:

- receipt reversal direction, operational correction, bank snapshot, ledger trace, and retry idempotency;
- reconciliation blocking before any reversal posting;
- full paired transfer reversal and restoration of both bank snapshots;
- existing cash/bank posting, workflow approval, operational balance, and bank-reconciliation behaviour;
- action-to-permission mapping.

Database migration application and accountant UAT remain release gates. UAT should include source- and destination-leg navigation, closed-period date policy, reconciliation removal/retry, permission denial, bank-scope denial, print review, and posting-to-bank-snapshot reconciliation.
