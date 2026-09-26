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
import type { TaxGroup } from '@/types/tax';
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
  it.each(['Supplier', 'Vendor', 'Manufacturer', 'Contractor', 'Both', 'CustomerAndSupplier'])
    ('keeps supplier withholding controls for %s roles', (partnerType) => {
      render(<PartnerTaxDefaultsFields partnerType={partnerType}
        value={{ ...emptyBusinessPartnerPostingDefaults(), subjectToWithholdingDeduction: true, withholdingTaxRate: 7.5, defaultWithholdingTaxId: 'saved-wht' }}
        onChange={vi.fn()} taxGroups={[]} />);
      expect(screen.getByRole('switch', { name: 'Subject To Withholding Deduction' })).toBeChecked();
      expect(screen.getByLabelText('WHT Rate (%)')).toHaveValue(7.5);
      expect(screen.getByRole('combobox', { name: 'WHT Configuration' })).toHaveTextContent('Saved selection (unavailable)');
    });

  it('hides supplier withholding for customers without clearing it when the customer tax schedule changes', () => {
    const value = { ...emptyBusinessPartnerPostingDefaults(), subjectToWithholdingDeduction: true,
      withholdingTaxRate: 7.5, defaultWithholdingTaxId: 'saved-wht' };
    const onChange = vi.fn();
    const { rerender } = render(<PartnerTaxDefaultsFields partnerType="Customer" value={value}
      onChange={onChange} taxGroups={[{ id: 'sales-tax', code: 'SALES', name: 'Sales tax' }] as TaxGroup[]} />);
    expect(screen.queryByRole('switch', { name: 'Subject To Withholding Deduction' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('WHT Rate (%)')).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'WHT Configuration' })).not.toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('combobox', { name: 'Tax', exact: true }));
    fireEvent.click(screen.getByRole('option', { name: 'SALES - Sales tax' }));
    expect(onChange).toHaveBeenCalledWith({ ...value, defaultTaxGroupId: 'sales-tax' });
    rerender(<PartnerTaxDefaultsFields partnerType="Both" value={value} onChange={onChange} taxGroups={[]} />);
    expect(screen.getByRole('switch', { name: 'Subject To Withholding Deduction' })).toBeChecked();
    expect(screen.getByLabelText('WHT Rate (%)')).toHaveValue(7.5);
  });

  it('offers only direct income or expense writeoff accounts and preserves an unsupported saved mapping', () => {
    const accounts = [
      { id: 'income', accountNumber: '4900', accountName: 'Writeoff income', status: 'Active', accountType: 'Revenue', isControlAccount: false, allowDirectPosting: true },
      { id: 'expense', accountNumber: '6000', accountName: 'Writeoff expense', status: 'Active', accountType: 'Expense', isControlAccount: false, allowDirectPosting: true },
      { id: 'asset', accountNumber: '1400', accountName: 'Legacy asset', status: 'Active', accountType: 'Asset', isControlAccount: false, allowDirectPosting: true },
      { id: 'control', accountNumber: '4901', accountName: 'Income control', status: 'Active', accountType: 'Revenue', isControlAccount: true, allowDirectPosting: true },
    ] as Account[];
    const onChange = vi.fn();
    render(<PartnerAccountsFields value={{ ...emptyBusinessPartnerPostingDefaults(), defaultWriteoffAccountId: 'asset' }}
      onChange={onChange} accounts={accounts} bankAccounts={[]} />);
    expect(screen.getByRole('combobox', { name: 'Writeoffs' })).toHaveTextContent('Saved account unavailable');
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('combobox', { name: 'Writeoffs' }));
    expect(screen.getByRole('option', { name: /4900 — Writeoff income/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /6000 — Writeoff expense/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Legacy asset|Income control/ })).not.toBeInTheDocument();
  });

  it.each(['Supplier', 'Vendor', 'Manufacturer', 'Contractor', 'Both', 'CustomerAndSupplier'])
    ('shows the AP chequebook for %s roles', (partnerType) => {
      render(<PartnerOptionsFields partnerType={partnerType}
        value={{ ...emptyBusinessPartnerPostingDefaults(), defaultBankAccountId: 'saved-bank' }}
        onChange={vi.fn()} options={{ paymentTermId: '', taxNumber: '', creditLimit: '' }}
        onOptionsChange={vi.fn()} paymentTerms={[]} bankAccounts={[]} />);
      expect(screen.getByRole('combobox', { name: 'ChequeBook ID' })).toHaveTextContent('Saved selection (unavailable)');
    });

  it('hides the AP chequebook for a customer while preserving its saved supplier mapping', () => {
    const value = { ...emptyBusinessPartnerPostingDefaults(), defaultBankAccountId: 'saved-bank' };
    const onChange = vi.fn();
    const props = { value, onChange, options: { paymentTermId: '', taxNumber: '', creditLimit: '' },
      onOptionsChange: vi.fn(), paymentTerms: [], bankAccounts: [] };
    const { rerender } = render(<PartnerOptionsFields {...props} partnerType="Customer" />);
    expect(screen.queryByRole('combobox', { name: 'ChequeBook ID' })).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Payment Terms' })).toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
    expect(value.defaultBankAccountId).toBe('saved-bank');
    rerender(<PartnerOptionsFields {...props} partnerType="Both" />);
    expect(screen.getByRole('combobox', { name: 'ChequeBook ID' })).toHaveTextContent('Saved selection (unavailable)');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('offers only eligible input-tax accounts and preserves an unsupported saved mapping until explicitly changed', () => {
    const onChange = vi.fn();
    const accounts = [
      { id: 'input', accountNumber: '1400', accountName: 'Input VAT', status: 'Active', accountType: 'Asset', isControlAccount: true, allowDirectPosting: false },
      { id: 'net', accountNumber: '2100', accountName: 'VAT Control', status: 'Active', accountType: 'Liability', isControlAccount: false, allowDirectPosting: true },
      { id: 'legacy', accountNumber: '6000', accountName: 'Legacy Tax Expense', status: 'Active', accountType: 'Expense', isControlAccount: false, allowDirectPosting: true },
      { id: 'inactive', accountNumber: '1401', accountName: 'Inactive VAT', status: 'Inactive', accountType: 'Asset', isControlAccount: true, allowDirectPosting: false },
      { id: 'summary', accountNumber: '1402', accountName: 'Summary VAT', status: 'Active', accountType: 'Asset', isControlAccount: false, allowDirectPosting: false },
    ] as Account[];
    render(<PartnerAccountsFields
      value={{ ...emptyBusinessPartnerPostingDefaults(), defaultTaxAccountId: 'legacy' }}
      onChange={onChange} accounts={accounts} bankAccounts={[]} />);
    expect(screen.getByText(/recoverable purchase tax only when the tax rule has no receivable account/)).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Input tax fallback' })).toHaveTextContent('Saved account unavailable');
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('combobox', { name: 'Input tax fallback' }));
    expect(screen.getByRole('option', { name: /1400 — Input VAT/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /2100 — VAT Control/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Legacy Tax Expense/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Inactive VAT|Summary VAT/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('option', { name: /1400 — Input VAT/ }));
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ defaultTaxAccountId: 'input' }));
  });

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

  it('allows liability control accounts for AP and accrued purchases while keeping freight on direct expense accounts', () => {
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
