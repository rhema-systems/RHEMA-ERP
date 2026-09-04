import React from 'react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  maskBankAccountNumber,
  type SupplierBankAccountDetails,
  type SupplierContactDetails,
} from '@/lib/supplier-registration-details';
import { Landmark, Mail } from 'lucide-react';

export function SupplierContactsPanel({
  contacts,
}: {
  contacts: SupplierContactDetails[];
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Mail className="h-5 w-5" />
          Contact Persons
        </CardTitle>
      </CardHeader>
      <CardContent>
        {contacts.length === 0 ? (
          <p className="py-8 text-center text-gray-500">
            No contact persons have been provided.
          </p>
        ) : (
          <div className="space-y-3">
            {contacts.map((contact, index) => (
              <div
                key={contact.id || `${contact.contactName}-${index}`}
                className="rounded-lg border p-4"
              >
                <div className="mb-3 flex items-center justify-between gap-2">
                  <h3 className="font-semibold">
                    {contact.contactName || `Contact ${index + 1}`}
                  </h3>
                  {contact.isPrimary && <Badge>Primary Contact</Badge>}
                </div>
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  <Detail label="Position" value={contact.contactTitle} />
                  <Detail label="Department" value={contact.department} />
                  <Detail label="Email" value={contact.email} />
                  <Detail label="Phone" value={contact.phone} />
                  {contact.mobile && (
                    <Detail label="Mobile" value={contact.mobile} />
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

export function SupplierBankAccountsPanel({
  accounts,
  maskAccountNumbers = false,
}: {
  accounts: SupplierBankAccountDetails[];
  maskAccountNumbers?: boolean;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Landmark className="h-5 w-5" />
          Bank Accounts
        </CardTitle>
      </CardHeader>
      <CardContent>
        {accounts.length === 0 ? (
          <p className="py-8 text-center text-gray-500">
            No bank accounts have been provided.
          </p>
        ) : (
          <div className="space-y-3">
            {accounts.map((account, index) => (
              <div
                key={
                  account.id ||
                  `${account.bankName}-${account.accountNumber}-${index}`
                }
                className="rounded-lg border p-4"
              >
                <div className="mb-3 flex items-center justify-between gap-2">
                  <h3 className="font-semibold">
                    {account.bankName || `Bank account ${index + 1}`}
                  </h3>
                  {account.isPrimary && <Badge>Primary Account</Badge>}
                </div>
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  <Detail label="Account Name" value={account.accountName} />
                  <Detail
                    label="Account Number"
                    mono
                    value={
                      maskAccountNumbers
                        ? maskBankAccountNumber(account.accountNumber)
                        : account.accountNumber
                    }
                  />
                  <Detail label="Bank Branch" value={account.bankBranch} />
                  <Detail label="Branch Code" value={account.bankBranchCode} />
                  <Detail label="Currency" value={account.currency} />
                  <Detail label="SWIFT Code" value={account.swiftCode} mono />
                  {account.iban && (
                    <Detail label="IBAN" value={account.iban} mono />
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function Detail({
  label,
  value,
  mono = false,
}: {
  label: string;
  value?: string;
  mono?: boolean;
}) {
  return (
    <div>
      <Label className="text-gray-600">{label}</Label>
      <p className={`font-semibold ${mono ? 'font-mono' : ''}`}>
        {value || 'Not provided'}
      </p>
    </div>
  );
}
