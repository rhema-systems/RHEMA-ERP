import { describe, expect, it } from 'vitest';
import { mapJournalEntryFormToCreateDto } from './journal-entry-mapper';

const header = {
  entryDate: '2025-01-01',
  journalType: 'General',
  description: 'Foreign journal',
  referenceNumber: 'FX-001',
  notes: '',
  bookClassification: 'IFRS',
};

describe('journal entry mapper currency contract', () => {
  it('requires the Finance functional currency', () => {
    expect(() =>
      mapJournalEntryFormToCreateDto(header, [], undefined, '')
    ).toThrow(
      'A valid Finance functional currency is required to map a journal entry.'
    );
  });

  it('preserves foreign evidence and omits currency fields for functional lines', () => {
    const result = mapJournalEntryFormToCreateDto(
      header,
      [
        {
          id: '1',
          accountId: 'expense',
          description: 'USD expense',
          currencyCode: 'usd',
          exchangeRateId: 'rate-1',
          exchangeRate: 12.5,
          debit: 1250,
          credit: 0,
          foreignDebit: 100,
        },
        {
          id: '2',
          accountId: 'payable',
          description: 'Functional credit',
          currencyCode: 'ghs',
          exchangeRate: 1,
          debit: 0,
          credit: 1250,
        },
      ],
      undefined,
      'ghs'
    );

    expect(result.transactions).toEqual([
      expect.objectContaining({
        accountId: 'expense',
        currencyCode: 'USD',
        exchangeRateId: 'rate-1',
        exchangeRate: 12.5,
        foreignAmount: 100,
        amount: 1250,
      }),
      expect.objectContaining({
        accountId: 'payable',
        currencyCode: undefined,
        exchangeRate: undefined,
        foreignAmount: undefined,
        amount: 1250,
      }),
    ]);
  });

  it('fails closed when foreign evidence is incomplete', () => {
    expect(() =>
      mapJournalEntryFormToCreateDto(
        header,
        [
          {
            id: '1',
            accountId: 'expense',
            description: 'USD expense',
            currencyCode: 'USD',
            exchangeRateId: 'rate-1',
            exchangeRate: 12.5,
            debit: 1250,
            credit: 0,
          },
        ],
        undefined,
        'GHS'
      )
    ).toThrow('The original USD debit amount is required.');
  });

  it('fails closed when the approved foreign-rate record is missing', () => {
    expect(() =>
      mapJournalEntryFormToCreateDto(
        header,
        [{
          id: '1', accountId: 'expense', description: 'USD expense',
          currencyCode: 'USD', exchangeRate: 12.5, debit: 1250, credit: 0,
          foreignDebit: 100,
        }],
        undefined,
        'GHS'
      )
    ).toThrow('The approved USD exchange-rate record is required.');
  });

  it('maps line coding dimensions as structured Finance values', () => {
    const result = mapJournalEntryFormToCreateDto(
      header,
      [{
        id: '1', accountId: 'expense', description: 'Department expense',
        currencyCode: 'GHS', exchangeRate: 1, debit: 100, credit: 0,
        dimensions: { DEPT: 'FIN', PROJECT: '', FUND: 'GOG' },
      }],
      undefined,
      'GHS'
    );

    expect(result.transactions[0].dimensions).toEqual([
      { dimensionCode: 'DEPT', valueCode: 'FIN' },
      { dimensionCode: 'FUND', valueCode: 'GOG' },
    ]);
  });

  it('sends the same per-line money precision enforced during posting', () => {
    const result = mapJournalEntryFormToCreateDto(
      header,
      [
        {
          id: '1', accountId: 'expense', description: 'USD expense',
          currencyCode: 'USD', exchangeRateId: 'rate-1', exchangeRate: 12.345,
          debit: 37.035, credit: 0, foreignDebit: 3,
        },
        {
          id: '2', accountId: 'payable', description: 'Functional credit',
          currencyCode: 'GHS', exchangeRate: 1, debit: 0, credit: 37.04,
        },
      ],
      undefined,
      'GHS'
    );

    expect(result.transactions.map(line => line.amount)).toEqual([37.04, 37.04]);
  });
});
