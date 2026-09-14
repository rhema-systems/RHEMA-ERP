import React, { useState } from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import {
  emptyBusinessPartnerPostingDefaults,
  eligiblePartnerAccounts,
  PartnerAccountsFields,
  PartnerOptionsFields,
  PartnerTaxDefaultsFields,
  useBusinessPartnerPostingCatalogues,
} from './BusinessPartnerPostingFields';
import type { Account } from '@/types/finance';
import type { BankAccount } from '@/types/cash-management';
import type { PaymentTermListDto } from '@/services/financeCommonService';
import {
  businessPartnerService,
  type BusinessPartnerPostingDefaults,
} from '@/services/businessPartnerService';

Object.assign(globalThis, {
  React,
  ResizeObserver: class {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
});
Object.defineProperty(Element.prototype, 'scrollIntoView', {
  configurable: true,
  value: vi.fn(),
});

describe('business partner posting fields', () => {
  it('enables the WHT rate only when withholding deduction is selected', () => {
    function Form() {
      const [value, setValue] = useState(emptyBusinessPartnerPostingDefaults);
      return (
        <PartnerTaxDefaultsFields
          value={value}
          onChange={setValue}
          taxGroups={[]}
        />
      );
    }
    render(<Form />);
    expect(screen.getByLabelText('WHT Rate (%)')).toBeDisabled();
    fireEvent.click(
      screen.getByRole('switch', { name: 'Subject To Withholding Deduction' })
    );
    expect(screen.getByLabelText('WHT Rate (%)')).toBeEnabled();
    fireEvent.change(screen.getByLabelText('WHT Rate (%)'), {
      target: { value: '7.5' },
    });
    expect(screen.getByLabelText('WHT Rate (%)')).toHaveValue(7.5);
    fireEvent.click(
      screen.getByRole('switch', { name: 'Subject To Withholding Deduction' })
    );
    expect(screen.getByLabelText('WHT Rate (%)')).toBeDisabled();
  });

  it('provides searchable payment terms and reuses TIN/credit limit fields', () => {
    const onOptionsChange = vi.fn();
    render(
      <PartnerOptionsFields
        value={emptyBusinessPartnerPostingDefaults()}
        onChange={vi.fn()}
        options={{
          paymentTermId: '',
          taxNumber: 'TIN-123',
          creditLimit: '500',
        }}
        onOptionsChange={onOptionsChange}
        bankAccounts={[]}
        paymentTerms={
          [
            { id: 'net30', code: 'N30', name: 'Net 30 Days' },
            { id: 'cash', code: 'COD', name: 'Cash on delivery' },
          ] as PaymentTermListDto[]
        }
      />
    );
    expect(screen.getByLabelText('TIN')).toHaveValue('TIN-123');
    expect(screen.getByLabelText('Credit Limit')).toHaveValue(500);
    fireEvent.click(screen.getByRole('combobox', { name: 'Payment Terms' }));
    fireEvent.change(screen.getByPlaceholderText('Search payment terms...'), {
      target: { value: 'N30' },
    });
    expect(
      screen.queryByText('COD - Cash on delivery')
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByText('N30 - Net 30 Days'));
    expect(onOptionsChange).toHaveBeenCalledWith({ paymentTermId: 'net30' });
  });

  it('suggests the configured WHT rate but allows an independent supplier default rate', () => {
    function Form() {
      const [value, setValue] = useState<BusinessPartnerPostingDefaults>({
        ...emptyBusinessPartnerPostingDefaults(),
        subjectToWithholdingDeduction: true,
        withholdingTaxRate: 5,
        defaultTaxGroupId: 'vat',
      });
      return (
        <PartnerTaxDefaultsFields
          value={value}
          onChange={setValue}
          taxGroups={[]}
          withholdingTaxes={[
            {
              id: 'wht7',
              code: 'WHT7',
              name: 'Service withholding',
              rate: 7.5,
              taxPayableAccountId: 'payable',
            },
          ]}
        />
      );
    }
    render(<Form />);
    fireEvent.click(
      screen.getByRole('combobox', { name: 'WHT Configuration' })
    );
    fireEvent.change(
      screen.getByPlaceholderText('Search wht configuration...'),
      { target: { value: 'WHT7' } }
    );
    fireEvent.click(
      screen.getByRole('option', { name: /Service withholding/ })
    );
    expect(screen.getByLabelText('WHT Rate (%)')).toHaveValue(7.5);
    expect(screen.getByLabelText('WHT Rate (%)')).not.toHaveAttribute(
      'readonly'
    );
    fireEvent.change(screen.getByLabelText('WHT Rate (%)'), {
      target: { value: '6.25' },
    });
    expect(screen.getByLabelText('WHT Rate (%)')).toHaveValue(6.25);
    expect(
      screen.getByRole('combobox', { name: 'WHT Configuration' })
    ).toHaveTextContent('WHT7');
    expect(screen.getByRole('combobox', { name: 'Tax' })).toHaveTextContent(
      'Saved selection (unavailable)'
    );
  });

  it('shows saved unavailable selections without silently clearing them', () => {
    const onChange = vi.fn();
    render(
      <PartnerOptionsFields
        value={{
          ...emptyBusinessPartnerPostingDefaults(),
          defaultBankAccountId: 'saved-bank',
        }}
        onChange={onChange}
        options={{ paymentTermId: '', taxNumber: '', creditLimit: '' }}
        onOptionsChange={vi.fn()}
        bankAccounts={[]}
        paymentTerms={[]}
      />
    );
    expect(
      screen.getByRole('combobox', { name: 'ChequeBook ID' })
    ).toHaveTextContent('Saved selection (unavailable)');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('shows the selected chequebook cash account and does not overwrite the creditor cash mapping', () => {
    const onChange = vi.fn();
    render(
      <PartnerAccountsFields
        value={{
          ...emptyBusinessPartnerPostingDefaults(),
          defaultBankAccountId: 'bank',
          defaultCashAccountId: 'creditor-cash',
        }}
        onChange={onChange}
        accounts={[]}
        bankAccounts={
          [
            { id: 'bank', glAccountNumber: '1000', glAccountName: 'Main Bank' },
          ] as BankAccount[]
        }
      />
    );
    expect(screen.getByText('1000 - Main Bank')).toBeInTheDocument();
    expect(screen.getByText('Accounts Payable')).toBeInTheDocument();
    expect(screen.getByText('Accrued Purchases')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('radio', { name: 'Creditor' }));
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        cashAccountSource: 'BusinessPartner',
        defaultCashAccountId: 'creditor-cash',
      })
    );
  });

  it('allows system-posted liability control accounts for AP and accrued purchases only', () => {
    const accounts = [
      {
        id: 'ap',
        status: 'Active',
        accountType: 'Liability',
        isControlAccount: true,
        allowDirectPosting: false,
      },
      {
        id: 'expense',
        status: 'Active',
        accountType: 'Expense',
        isControlAccount: false,
        allowDirectPosting: true,
      },
      {
        id: 'inactive',
        status: 'Inactive',
        accountType: 'Liability',
        isControlAccount: true,
        allowDirectPosting: true,
      },
    ] as Account[];
    expect(
      eligiblePartnerAccounts(accounts, 'defaultApAccountId').map((a) => a.id)
    ).toEqual(['ap']);
    expect(
      eligiblePartnerAccounts(accounts, 'defaultAccruedPurchasesAccountId').map(
        (a) => a.id
      )
    ).toEqual(['ap']);
    expect(
      eligiblePartnerAccounts(accounts, 'defaultFreightAccountId').map(
        (a) => a.id
      )
    ).toEqual(['expense']);
  });

  it('uses the purpose-scoped business partner lookup and retains prior options when a reload fails', async () => {
    const lookup = vi
      .spyOn(businessPartnerService, 'getPostingOptions')
      .mockResolvedValueOnce({
        accounts: [],
        bankAccounts: [{ id: 'bank', isActive: true }] as BankAccount[],
        taxGroups: [],
      })
      .mockRejectedValueOnce(new Error('Unavailable'));
    function State({ type }: { type: string }) {
      const catalogues = useBusinessPartnerPostingCatalogues(type);
      return <div>{JSON.stringify(catalogues)}</div>;
    }
    const { rerender } = render(<State type="Supplier" />);
    await waitFor(() =>
      expect(screen.getByText(/"loading":false/)).toHaveTextContent(
        '"id":"bank"'
      )
    );
    rerender(<State type="Customer" />);
    await waitFor(() =>
      expect(screen.getByText(/"loading":false/)).toHaveTextContent(
        '"unavailable":["posting options"]'
      )
    );
    expect(screen.getByText(/"loading":false/)).toHaveTextContent(
      '"id":"bank"'
    );
    expect(lookup).toHaveBeenNthCalledWith(1, 'Supplier');
    expect(lookup).toHaveBeenNthCalledWith(2, 'Customer');
    vi.restoreAllMocks();
  });
});
