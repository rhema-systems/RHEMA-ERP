import { describe, expect, it } from 'vitest';
import {
  bankAccountsFromRegistrationData,
  contactsFromRegistrationData,
  ensureOnePrimary,
  maskBankAccountNumber,
  parseRegistrationData,
} from './supplier-registration-details';

describe('supplier registration detail compatibility', () => {
  it('reads legacy wrapped singular contact and bank fields', () => {
    const data = parseRegistrationData(
      JSON.stringify({
        RegistrationData: JSON.stringify({
          contactPersonName: 'Legacy Contact',
          contactPersonEmail: 'legacy@example.test',
          bankName: 'Legacy Bank',
          bankAccountNumber: '1234567890',
          bankBranchCode: '001',
        }),
      })
    );

    expect(contactsFromRegistrationData(data)).toEqual([
      expect.objectContaining({
        contactName: 'Legacy Contact',
        email: 'legacy@example.test',
        isPrimary: true,
      }),
    ]);
    expect(bankAccountsFromRegistrationData(data)).toEqual([
      expect.objectContaining({
        bankName: 'Legacy Bank',
        accountNumber: '1234567890',
        bankBranchCode: '001',
        isPrimary: true,
      }),
    ]);
  });

  it('reads the repeatable contract and normalizes exactly one primary', () => {
    const data = {
      contacts: [
        { contactName: 'First', isPrimary: true },
        { contactName: 'Second', isPrimary: true },
      ],
      bankAccounts: [
        {
          bankName: 'Bank One',
          branchName: 'Head Office',
          accountNumber: '9876543210',
          isPrimary: false,
        },
        {
          bankName: 'Bank Two',
          accountNumber: '1122334455',
          isPrimary: false,
        },
      ],
    };

    expect(ensureOnePrimary(contactsFromRegistrationData(data))).toEqual([
      expect.objectContaining({ contactName: 'First', isPrimary: true }),
      expect.objectContaining({ contactName: 'Second', isPrimary: false }),
    ]);
    expect(ensureOnePrimary(bankAccountsFromRegistrationData(data))).toEqual([
      expect.objectContaining({
        bankName: 'Bank One',
        bankBranch: 'Head Office',
        isPrimary: true,
      }),
      expect.objectContaining({ bankName: 'Bank Two', isPrimary: false }),
    ]);
    expect(maskBankAccountNumber('9876543210')).toBe('•••••• 3210');
  });
});
