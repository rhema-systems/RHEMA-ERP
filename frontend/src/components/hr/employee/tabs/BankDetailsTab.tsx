'use client';

import { z } from 'zod';
import type { UseFormReturn } from 'react-hook-form';
import { useQuery } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { bankService } from '@/services/hr/bank.service';
import { employeeService } from '@/services/hr/employee.service';
import {
  BANK_ACCOUNT_TYPE_OPTIONS,
  type EmployeeBankDetail,
} from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { FieldRow, NumberField, SelectField, SwitchField, TextField } from './fields';

const schema = z
  .object({
    // Either a catalogue bank (and optionally its branch) OR a typed name — never both.
    bankNotListed: z.boolean(),
    bankId: z.string().optional().or(z.literal('')),
    branchId: z.string().optional().or(z.literal('')),
    bankName: z.string().max(200).optional().or(z.literal('')),
    branchName: z.string().max(100).optional().or(z.literal('')),
    accountNumber: z.string().min(1, 'Account number is required').max(50),
    accountName: z.string().min(1, 'Account name is required').max(200),
    accountType: z.enum(['Current', 'Savings', 'MobileMoney']),
    mobileMoneyNumber: z.string().max(30).optional().or(z.literal('')),
    allocationPercentage: z.coerce
      .number()
      .min(0, 'Cannot be negative')
      .max(100, 'Cannot exceed 100'),
    isPrimary: z.boolean(),
    isActive: z.boolean(),
  })
  .refine((v) => v.accountType !== 'MobileMoney' || !!v.mobileMoneyNumber, {
    message: 'Mobile money number is required for a mobile money account',
    path: ['mobileMoneyNumber'],
  })
  .refine((v) => (v.bankNotListed ? !!v.bankName : !!v.bankId), {
    message: 'Choose a bank, or tick "not in the list" and type its name',
    path: ['bankId'],
  })
  .refine((v) => !v.bankNotListed || !!v.bankName, {
    message: 'Bank name is required',
    path: ['bankName'],
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  bankNotListed: false,
  bankId: '',
  branchId: '',
  bankName: '',
  branchName: '',
  accountNumber: '',
  accountName: '',
  accountType: 'Savings',
  mobileMoneyNumber: '',
  allocationPercentage: 100,
  isPrimary: false,
  isActive: true,
};

const toPayload = (employeeId: string, v: FormValues) => ({
  employeeId,
  // The server mirrors the catalogue names into the text columns, so when a bank is chosen the
  // names are sent empty and filled from the master — never both sources at once.
  bankId: v.bankNotListed ? null : v.bankId || null,
  branchId: v.bankNotListed ? null : v.branchId || null,
  bankName: v.bankNotListed ? v.bankName ?? '' : '',
  branchName: v.bankNotListed ? v.branchName ?? '' : '',
  accountNumber: v.accountNumber,
  accountName: v.accountName,
  accountType: v.accountType,
  mobileMoneyNumber: v.mobileMoneyNumber || null,
  allocationPercentage: v.allocationPercentage,
  isPrimary: v.isPrimary,
});

/**
 * Bank accounts — where net pay goes.
 *
 * Round 2 (E-14): the bank and branch come from the banks catalogue the setup screen maintains.
 * The row already had `bankId`/`branchId`; this tab simply never sent them, and the reads never
 * loaded the navigations, so linking had no visible effect. The typed-name fallback stays for
 * banks not in the list and for rows recorded before the catalogue existed.
 */
export function BankDetailsTab({ employeeId }: { employeeId: string }) {
  const { data: banks } = useQuery({
    queryKey: ['hr', 'banks', 'active'],
    queryFn: () => bankService.getActiveBanks(),
  });
  const bankOptions = (banks ?? []).map((b) => ({ value: b.id, label: `${b.name} (${b.code})` }));

  return (
    <EmployeeSubResourceTab<EmployeeBankDetail, FormValues>
      employeeId={employeeId}
      title="bank accounts"
      singular="bank account"
      itemLabel={(b) => b.bankName}
      queryKey="bank-details"
      emptyDescription="Where the employee's net pay is sent."
      getId={(b) => b.id}
      list={employeeService.getBankDetails.bind(employeeService)}
      create={(id, v) => employeeService.addBankDetail(id, toPayload(id, v))}
      update={(id, bankId, v) =>
        employeeService.updateBankDetail(id, bankId, {
          id: bankId,
          ...toPayload(id, v),
          isActive: v.isActive,
          // A null id means "not supplied" on the update DTO; switching a row from a catalogue
          // bank back to a typed name has to say so explicitly.
          clearBankLink: v.bankNotListed,
        })
      }
      remove={employeeService.removeBankDetail.bind(employeeService)}
      actions={[
        {
          label: 'Set as primary',
          visible: (b) => !b.isPrimary,
          run: (b) => employeeService.setPrimaryBankDetail(employeeId, b.id),
        },
        {
          label: (b) => (b.isVerified ? 'Mark unverified' : 'Mark verified'),
          run: (b) =>
            b.isVerified
              ? employeeService.unverifyBankDetail(employeeId, b.id)
              : // This endpoint takes the verifier and timestamp as query parameters.
                employeeService.verifyBankDetail(
                  employeeId,
                  b.id,
                  employeeId,
                  new Date().toISOString(),
                ),
        },
        {
          label: (b) => (b.isActive ? 'Deactivate' : 'Activate'),
          run: (b) =>
            b.isActive
              ? employeeService.deactivateBankDetail(employeeId, b.id)
              : employeeService.activateBankDetail(employeeId, b.id),
        },
      ]}
      columns={[
        {
          header: 'Bank',
          cell: (b) => (
            <span className="inline-flex items-center gap-2">
              {b.bankName}
              {!b.bankId && (
                <Badge variant="outline" title="Typed, not from the banks list">Not in list</Badge>
              )}
            </span>
          ),
        },
        { header: 'Branch', cell: (b) => b.branchName || '—' },
        { header: 'Account name', cell: (b) => b.accountName },
        { header: 'Account number', cell: (b) => b.accountNumber },
        { header: 'Type', cell: (b) => b.accountType },
        { header: 'Allocation', cell: (b) => `${b.allocationPercentage ?? 0}%` },
        {
          header: 'Status',
          cell: (b) => (
            <div className="flex gap-1">
              {b.isPrimary && <Badge variant="secondary">Primary</Badge>}
              {b.isVerified && <Badge variant="secondary">Verified</Badge>}
              {!b.isActive && <Badge variant="outline">Inactive</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      dialogClassName="sm:max-w-[620px]"
      toForm={(b) => ({
        bankNotListed: !b.bankId,
        bankId: b.bankId ?? '',
        branchId: b.branchId ?? '',
        bankName: b.bankName,
        branchName: b.branchName ?? '',
        accountNumber: b.accountNumber,
        accountName: b.accountName,
        accountType: b.accountType,
        mobileMoneyNumber: b.mobileMoneyNumber ?? '',
        allocationPercentage: b.allocationPercentage ?? 100,
        isPrimary: b.isPrimary,
        isActive: b.isActive,
      })}
      renderFields={(form) => <BankFields form={form} bankOptions={bankOptions} />}
    />
  );
}

function BankFields({
  form,
  bankOptions,
}: {
  form: UseFormReturn<FormValues>;
  bankOptions: { value: string; label: string }[];
}) {
  const notListed = form.watch('bankNotListed');
  const bankId = form.watch('bankId');

  // Branches belong to a bank; the list follows the chosen one. The server refuses a branch of
  // another bank regardless, so this is convenience, not the rule.
  const { data: branches } = useQuery({
    queryKey: ['hr', 'banks', bankId, 'branches', 'active'],
    queryFn: () => bankService.getActiveBranches(bankId as string),
    enabled: !notListed && !!bankId,
  });
  const branchOptions = (branches ?? []).map((br) => ({
    value: br.id,
    label: br.code ? `${br.name} (${br.code})` : br.name,
  }));

  return (
    <>
      {!notListed ? (
        <FieldRow>
          <SelectField
            form={form}
            name="bankId"
            label="Bank"
            required
            options={bankOptions}
            placeholder={bankOptions.length ? 'Choose a bank…' : 'No banks set up yet'}
          />
          {bankId ? (
            <SelectField
              form={form}
              name="branchId"
              label="Branch"
              options={branchOptions}
              allowEmpty
              emptyLabel="Not specified"
              placeholder={branchOptions.length ? 'Choose a branch…' : 'No branches for this bank'}
            />
          ) : (
            <div className="space-y-2">
              <p className="text-sm font-medium">Branch</p>
              <p className="text-sm text-muted-foreground">Choose the bank first.</p>
            </div>
          )}
        </FieldRow>
      ) : (
        <FieldRow>
          <TextField form={form} name="bankName" label="Bank" required />
          <TextField form={form} name="branchName" label="Branch" />
        </FieldRow>
      )}
      <SwitchField
        form={form}
        name="bankNotListed"
        label="Bank not in the list"
        description="Type the bank's name instead. Ask HR setup to add it to the banks list."
      />
      <FieldRow>
        <TextField form={form} name="accountName" label="Account name" required />
        <TextField form={form} name="accountNumber" label="Account number" required />
      </FieldRow>
      <FieldRow>
        <SelectField
          form={form}
          name="accountType"
          label="Account type"
          required
          options={BANK_ACCOUNT_TYPE_OPTIONS}
        />
        <TextField
          form={form}
          name="mobileMoneyNumber"
          label="Mobile money number"
          type="tel"
        />
      </FieldRow>
      <NumberField
        form={form}
        name="allocationPercentage"
        label="Allocation (%)"
        step="0.01"
        required
      />
      <FieldRow>
        <SwitchField form={form} name="isPrimary" label="Primary account" />
        <SwitchField form={form} name="isActive" label="Active" />
      </FieldRow>
    </>
  );
}
