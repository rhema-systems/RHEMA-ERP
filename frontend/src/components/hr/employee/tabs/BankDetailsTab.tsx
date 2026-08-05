'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { employeeService } from '@/services/hr/employee.service';
import {
  BANK_ACCOUNT_TYPE_OPTIONS,
  type EmployeeBankDetail,
} from '@/types/hr/employee-subresources';
import { EmployeeSubResourceTab } from './EmployeeSubResourceTab';
import { FieldRow, NumberField, SelectField, SwitchField, TextField } from './fields';

const schema = z
  .object({
    bankName: z.string().min(1, 'Bank name is required').max(200),
    branchName: z.string().min(1, 'Branch name is required').max(200),
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
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
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
  bankName: v.bankName,
  branchName: v.branchName,
  accountNumber: v.accountNumber,
  accountName: v.accountName,
  accountType: v.accountType,
  mobileMoneyNumber: v.mobileMoneyNumber || null,
  allocationPercentage: v.allocationPercentage,
  isPrimary: v.isPrimary,
});

export function BankDetailsTab({ employeeId }: { employeeId: string }) {
  return (
    <EmployeeSubResourceTab<EmployeeBankDetail, FormValues>
      employeeId={employeeId}
      title="bank accounts"
      singular="bank account"
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
        { header: 'Bank', cell: (b) => b.bankName },
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
      renderFields={(form) => (
        <>
          <FieldRow>
            <TextField form={form} name="bankName" label="Bank" required />
            <TextField form={form} name="branchName" label="Branch" required />
          </FieldRow>
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
      )}
    />
  );
}
