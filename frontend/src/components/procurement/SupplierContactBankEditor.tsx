import React from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import type {
  SupplierBankAccountDetails,
  SupplierContactDetails,
} from '@/lib/supplier-registration-details';
import { Plus, Trash2 } from 'lucide-react';

interface EditorProps {
  contacts: SupplierContactDetails[];
  bankAccounts: SupplierBankAccountDetails[];
  disabled: boolean;
  onContactsChange: (contacts: SupplierContactDetails[]) => void;
  onBankAccountsChange: (accounts: SupplierBankAccountDetails[]) => void;
}

export function SupplierContactBankEditor({
  contacts,
  bankAccounts,
  disabled,
  onContactsChange,
  onBankAccountsChange,
}: EditorProps) {
  const updateContact = (
    index: number,
    patch: Partial<SupplierContactDetails>
  ) => {
    onContactsChange(
      contacts.map((contact, current) =>
        current === index ? { ...contact, ...patch } : contact
      )
    );
  };

  const updateBankAccount = (
    index: number,
    patch: Partial<SupplierBankAccountDetails>
  ) => {
    onBankAccountsChange(
      bankAccounts.map((account, current) =>
        current === index ? { ...account, ...patch } : account
      )
    );
  };

  return (
    <>
      <section
        className="space-y-4 md:col-span-2"
        aria-labelledby="contacts-heading"
      >
        <div className="flex items-center justify-between gap-3">
          <div>
            <h3 id="contacts-heading" className="font-semibold">
              Contact Persons
            </h3>
            <p className="text-sm text-muted-foreground">
              Add the people procurement should contact about this application.
            </p>
          </div>
          <Button
            type="button"
            variant="outline"
            disabled={disabled || contacts.length >= 10}
            onClick={() =>
              onContactsChange([
                ...contacts,
                {
                  contactName: '',
                  isPrimary: contacts.length === 0,
                },
              ])
            }
          >
            <Plus className="mr-2 h-4 w-4" />
            Add contact
          </Button>
        </div>
        {contacts.length >= 10 && (
          <p className="text-sm text-amber-700">
            The maximum of 10 contacts has been reached.
          </p>
        )}
        {contacts.map((contact, index) => (
          <div key={contact.id || index} className="rounded-lg border p-4">
            <div className="mb-4 flex items-center justify-between">
              <span className="font-medium">Contact {index + 1}</span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled}
                aria-label={`Remove contact ${index + 1}`}
                onClick={() =>
                  onContactsChange(
                    contacts
                      .filter((_, current) => current !== index)
                      .map((item, current) => ({
                        ...item,
                        isPrimary:
                          item.isPrimary ||
                          (current === 0 &&
                            contacts
                              .filter((_, i) => i !== index)
                              .every((candidate) => !candidate.isPrimary)),
                      }))
                  )
                }
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <Field
                id={`contact-${index}-name`}
                label="Full name"
                value={contact.contactName}
                disabled={disabled}
                onChange={(value) =>
                  updateContact(index, { contactName: value })
                }
              />
              <Field
                id={`contact-${index}-title`}
                label="Position"
                value={contact.contactTitle}
                disabled={disabled}
                onChange={(value) =>
                  updateContact(index, { contactTitle: value })
                }
              />
              <Field
                id={`contact-${index}-department`}
                label="Department"
                value={contact.department}
                disabled={disabled}
                onChange={(value) =>
                  updateContact(index, { department: value })
                }
              />
              <Field
                id={`contact-${index}-email`}
                label="Contact email"
                type="email"
                value={contact.email}
                disabled={disabled}
                onChange={(value) => updateContact(index, { email: value })}
              />
              <Field
                id={`contact-${index}-phone`}
                label="Contact phone"
                value={contact.phone}
                disabled={disabled}
                onChange={(value) => updateContact(index, { phone: value })}
              />
              <label className="flex items-center gap-2 self-end pb-2 text-sm">
                <input
                  type="radio"
                  name="primary-contact"
                  checked={contact.isPrimary}
                  disabled={disabled}
                  onChange={() =>
                    onContactsChange(
                      contacts.map((item, current) => ({
                        ...item,
                        isPrimary: current === index,
                      }))
                    )
                  }
                />
                Primary contact
              </label>
            </div>
          </div>
        ))}
      </section>

      <section
        className="space-y-4 md:col-span-2"
        aria-labelledby="bank-accounts-heading"
      >
        <div className="flex items-center justify-between gap-3">
          <div>
            <h3 id="bank-accounts-heading" className="font-semibold">
              Bank Accounts
            </h3>
            <p className="text-sm text-muted-foreground">
              Add one or more accounts used for supplier payments.
            </p>
          </div>
          <Button
            type="button"
            variant="outline"
            disabled={disabled || bankAccounts.length >= 10}
            onClick={() =>
              onBankAccountsChange([
                ...bankAccounts,
                {
                  bankName: '',
                  accountNumber: '',
                  currency: 'GHS',
                  isPrimary: bankAccounts.length === 0,
                },
              ])
            }
          >
            <Plus className="mr-2 h-4 w-4" />
            Add bank account
          </Button>
        </div>
        {bankAccounts.length >= 10 && (
          <p className="text-sm text-amber-700">
            The maximum of 10 bank accounts has been reached.
          </p>
        )}
        {bankAccounts.map((account, index) => (
          <div key={account.id || index} className="rounded-lg border p-4">
            <div className="mb-4 flex items-center justify-between">
              <span className="font-medium">Bank account {index + 1}</span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled}
                aria-label={`Remove bank account ${index + 1}`}
                onClick={() =>
                  onBankAccountsChange(
                    bankAccounts
                      .filter((_, current) => current !== index)
                      .map((item, current) => ({
                        ...item,
                        isPrimary:
                          item.isPrimary ||
                          (current === 0 &&
                            bankAccounts
                              .filter((_, i) => i !== index)
                              .every((candidate) => !candidate.isPrimary)),
                      }))
                  )
                }
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <Field
                id={`bank-${index}-name`}
                label="Bank name"
                value={account.bankName}
                disabled={disabled}
                onChange={(value) =>
                  updateBankAccount(index, { bankName: value })
                }
              />
              <Field
                id={`bank-${index}-account-name`}
                label="Account name"
                value={account.accountName}
                disabled={disabled}
                onChange={(value) =>
                  updateBankAccount(index, { accountName: value })
                }
              />
              <Field
                id={`bank-${index}-account-number`}
                label="Account number"
                value={account.accountNumber}
                disabled={disabled}
                autoComplete="off"
                onChange={(value) =>
                  updateBankAccount(index, { accountNumber: value })
                }
              />
              <Field
                id={`bank-${index}-branch`}
                label="Bank branch"
                value={account.bankBranch}
                disabled={disabled}
                onChange={(value) =>
                  updateBankAccount(index, { bankBranch: value })
                }
              />
              <Field
                id={`bank-${index}-branch-code`}
                label="Branch code"
                value={account.bankBranchCode}
                disabled={disabled}
                onChange={(value) =>
                  updateBankAccount(index, { bankBranchCode: value })
                }
              />
              <Field
                id={`bank-${index}-currency`}
                label="Currency"
                value={account.currency}
                disabled={disabled}
                onChange={(value) =>
                  updateBankAccount(index, { currency: value })
                }
              />
              <Field
                id={`bank-${index}-swift`}
                label="SWIFT code"
                value={account.swiftCode}
                disabled={disabled}
                onChange={(value) =>
                  updateBankAccount(index, { swiftCode: value })
                }
              />
              <label className="flex items-center gap-2 self-end pb-2 text-sm">
                <input
                  type="radio"
                  name="primary-bank-account"
                  checked={account.isPrimary}
                  disabled={disabled}
                  onChange={() =>
                    onBankAccountsChange(
                      bankAccounts.map((item, current) => ({
                        ...item,
                        isPrimary: current === index,
                      }))
                    )
                  }
                />
                Primary bank account
              </label>
            </div>
          </div>
        ))}
      </section>
    </>
  );
}

function Field({
  id,
  label,
  value,
  disabled,
  type = 'text',
  autoComplete,
  onChange,
}: {
  id: string;
  label: string;
  value?: string;
  disabled: boolean;
  type?: string;
  autoComplete?: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        autoComplete={autoComplete}
        value={value || ''}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}
