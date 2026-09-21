'use client';

/**
 * Payroll's employee-profile window — salary basis, switches, payment split — as one component
 * with two hosts: payroll's own Employee Profiles page (in its dialog) and HR's Salary tab.
 *
 * ⚠ **Extracted, not copied** (round-2 plan § 6.5.1). This is the JSX that lived inline at
 * `app/hr/payroll/employee-profiles/page.tsx:659-932`, closed over that page's state. Lifting it
 * is the single permitted edit to `app/hr/payroll/**` under § 1.2 of the plan, and it REMOVES code
 * from that page rather than adding to it. Payroll's transport stays payroll's: every call here
 * goes through `payrollService` and its own `fetch` wrapper, never `apiService`.
 *
 * ⚠ **The replace-set trap is handled here, not documented away.** Payroll's upsert replaces the
 * whole payment-method list with whatever is sent. Two consequences this component enforces:
 *   1. it always sends EVERY method, with its id, from the list it loaded — never a subset;
 *   2. before saving it re-reads the profile through `reload` and refuses if the method count has
 *      changed since it loaded, because a save over a stale list would silently drop whatever
 *      somebody else added in the meantime.
 *
 * Where a host has no way to read a profile by employee (payroll's list is capped at 250 and keyed
 * on a search term), it passes `reload` accordingly — HR's host reads through its own door.
 */

import { FormEvent, ReactNode, useEffect, useMemo, useState } from 'react';
import { Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollEmployeeProfile,
  PayrollPaymentMethod,
  payrollService,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);
const paymentMethodOptions = ['Bank', 'Cash', 'Cheque'] as const;

export type PaymentMethodForm = {
  id?: string;
  paymentType: string;
  paymentMode: 'Percentage' | 'FixedAmount';
  paymentPercent: number;
  amount: number;
  bankCode: string;
  bankBranchCode: string;
  accountNumber: string;
  chequeNumber: string;
  chequeBankCode: string;
  currencyCode: string;
  exchangeRate: number;
  sequenceNo: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
};

const createDefaultPaymentMethod = (currencyCode = 'GHS', sequenceNo = 1): PaymentMethodForm => ({
  paymentType: 'Bank',
  paymentMode: 'Percentage',
  paymentPercent: 100,
  amount: 0,
  bankCode: '',
  bankBranchCode: '',
  accountNumber: '',
  chequeNumber: '',
  chequeBankCode: '',
  currencyCode,
  exchangeRate: 1,
  sequenceNo,
  startDate: '',
  endDate: '',
  isActive: true,
});

type ProfileForm = {
  id: string;
  employeeId: string;
  employeeNumber: string;
  legacyEmployeeId: string;
  legacyEmployeeNumber: string;
  ssfNumber: string;
  monthlyBasicSalary: number;
  currencyCode: string;
  payrollActive: boolean;
  payTax: boolean;
  ssfApplicable: boolean;
  overtimeEligible: boolean;
  grossUp: boolean;
  tier2Only: boolean;
  paymentMethods: PaymentMethodForm[];
};

const createDefaultProfileForm = (employeeId: string, employeeNumber: string, currencyCode = 'GHS'): ProfileForm => ({
  id: '',
  employeeId,
  employeeNumber,
  legacyEmployeeId: '',
  legacyEmployeeNumber: '',
  ssfNumber: '',
  monthlyBasicSalary: 0,
  currencyCode,
  payrollActive: true,
  payTax: true,
  ssfApplicable: true,
  overtimeEligible: false,
  grossUp: false,
  tier2Only: false,
  paymentMethods: [createDefaultPaymentMethod(currencyCode)],
});

/** Currency-formatted, falling back to a plain number when the code is not one Intl knows. */
export const money = (value: number | null | undefined, currency = 'GHS') => {
  try {
    return new Intl.NumberFormat('en-GH', { style: 'currency', currency, maximumFractionDigits: 2 }).format(value ?? 0);
  } catch {
    return `${currency} ${new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value ?? 0)}`;
  }
};

export function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function normalizePaymentMode(method: Partial<PayrollPaymentMethod>): 'Percentage' | 'FixedAmount' {
  const mode = String(method.paymentMode || '').replace(/\s+/g, '').toLowerCase();
  if (mode === 'fixedamount' || mode === 'amount' || mode === 'a') {
    return 'FixedAmount';
  }

  if (!method.paymentPercent && Number(method.amount || 0) > 0) {
    return 'FixedAmount';
  }

  return 'Percentage';
}

function toPaymentMethodForm(method: PayrollPaymentMethod, index: number, fallbackCurrency: string): PaymentMethodForm {
  const paymentMode = normalizePaymentMode(method);
  return {
    id: method.id,
    paymentType: method.paymentType || 'Bank',
    paymentMode,
    paymentPercent: paymentMode === 'Percentage' ? Number(method.paymentPercent ?? 0) : 0,
    amount: paymentMode === 'FixedAmount' ? Number(method.amount ?? 0) : 0,
    bankCode: method.bankCode || '',
    bankBranchCode: method.bankBranchCode || '',
    accountNumber: method.accountNumber || '',
    chequeNumber: method.chequeNumber || '',
    chequeBankCode: method.chequeBankCode || '',
    currencyCode: method.currencyCode || fallbackCurrency,
    exchangeRate: Number(method.exchangeRate ?? 1) || 1,
    sequenceNo: method.sequenceNo || index + 1,
    startDate: dateValue(method.startDate),
    endDate: dateValue(method.endDate),
    isActive: method.isActive !== false,
  };
}

function toProfileForm(profile: PayrollEmployeeProfile, fallbackCurrency: string): ProfileForm {
  const currencyCode = profile.currencyCode || fallbackCurrency;
  const paymentMethods = profile.paymentMethods?.length
    ? profile.paymentMethods.map((method, index) => toPaymentMethodForm(method, index, currencyCode))
    : [createDefaultPaymentMethod(currencyCode)];

  return {
    id: profile.id || '',
    employeeId: profile.employeeId,
    employeeNumber: profile.employeeNumber,
    legacyEmployeeId: profile.legacyEmployeeId || '',
    legacyEmployeeNumber: profile.legacyEmployeeNumber || '',
    ssfNumber: profile.ssfNumber || '',
    monthlyBasicSalary: profile.salaryBasis?.monthlyBasicSalary ?? 0,
    currencyCode,
    payrollActive: profile.payrollActive,
    payTax: profile.payTax,
    ssfApplicable: profile.ssfApplicable,
    overtimeEligible: profile.overtimeEligible,
    grossUp: profile.grossUp,
    tier2Only: profile.tier2Only,
    paymentMethods,
  };
}

function BooleanField({
  checked,
  label,
  onChange,
  disabled,
}: {
  checked: boolean;
  label: string;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <label className="flex items-center gap-2 text-sm">
      <Checkbox checked={checked} disabled={disabled} onCheckedChange={(value) => onChange(value === true)} />
      <span>{label}</span>
    </label>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

export interface PayrollEmployeeProfileEditorProps {
  employeeId: string;
  employeeNumber: string;
  /** The profile as the host loaded it, or null for somebody payroll has not set up yet. */
  initialProfile: PayrollEmployeeProfile | null;
  /**
   * Re-reads the profile by employee. Called immediately before every save — a changed
   * payment-method count aborts the save, because the upsert would replace the list.
   */
  reload: () => Promise<PayrollEmployeeProfile | null>;
  /** After a successful save, with what payroll returned. Hosts refresh whatever they show. */
  onSaved?: (profile: PayrollEmployeeProfile) => void;
  /** Everything visible, nothing editable — HR's tab for somebody not on payroll. */
  readOnly?: boolean;
  /**
   * Whether the host lets this user save. The tab passes its compensation-write check; payroll's
   * page passes true and relies on its own gating (defect #11, recorded, not HR's to fix).
   */
  canSave?: boolean;
  /**
   * Round 3, lane S. The tenant requires an approved salary change request for the monthly basic,
   * so the figure is shown but not typed here; everything else on the profile stays editable.
   */
  basicSalaryLocked?: boolean;
}

export function PayrollEmployeeProfileEditor({
  employeeId,
  employeeNumber,
  initialProfile,
  reload,
  onSaved,
  readOnly = false,
  canSave = true,
  basicSalaryLocked = false,
}: PayrollEmployeeProfileEditorProps) {
  const { toast } = useToast();

  // The three lookups payroll's window needs, loaded here so a host has nothing to supply. The
  // keys are payroll's own so the two hosts share one cache.
  const setup = useQuery({
    queryKey: ['payroll', 'setup-summary'],
    queryFn: () => payrollService.getSetupSummary(),
  });
  const bankSetup = useQuery({
    queryKey: ['payroll', 'code-setup', 'BNK'],
    queryFn: () => payrollService.getCodeSetup('BNK'),
  });
  const rates = useQuery({
    queryKey: ['payroll', 'exchange-rates'],
    queryFn: () => payrollService.getExchangeRates(),
  });

  const activeParameters = setup.data?.activeParameters ?? null;
  const bankBranches = useMemo(() => setup.data?.bankBranches ?? [], [setup.data]);
  const bankCodeValues = useMemo(() => bankSetup.data?.codeValues ?? [], [bankSetup.data]);
  const exchangeRates = useMemo(() => rates.data ?? [], [rates.data]);

  const fallbackCurrency = activeParameters?.baseCurrency || 'GHS';

  const [profileForm, setProfileForm] = useState<ProfileForm>(() =>
    initialProfile ? toProfileForm(initialProfile, fallbackCurrency) : createDefaultProfileForm(employeeId, employeeNumber, fallbackCurrency),
  );
  const [selectedPaymentMethodIndex, setSelectedPaymentMethodIndex] = useState(0);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // A host that swaps the employee (payroll's page picking another row) gets a fresh form.
  useEffect(() => {
    setProfileForm(
      initialProfile ? toProfileForm(initialProfile, fallbackCurrency) : createDefaultProfileForm(employeeId, employeeNumber, fallbackCurrency),
    );
    setSelectedPaymentMethodIndex(0);
    setError(null);
    // fallbackCurrency is derived from a lookup that resolves after mount; re-seeding on it would
    // wipe edits the moment the setup summary arrives.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [employeeId, employeeNumber, initialProfile]);

  const baseCurrency = activeParameters?.baseCurrency || profileForm.currencyCode || 'GHS';

  const currencyOptions = useMemo(() => {
    const values = new Set<string>(['GHS', 'USD', 'EUR', 'GBP', baseCurrency, activeParameters?.reportingCurrency || '', profileForm.currencyCode]);
    exchangeRates.forEach((rate) => values.add(rate.currencyCode));
    profileForm.paymentMethods.forEach((method) => values.add(method.currencyCode));
    return Array.from(values).filter(Boolean).map((value) => value.toUpperCase()).sort();
  }, [activeParameters?.reportingCurrency, baseCurrency, exchangeRates, profileForm.currencyCode, profileForm.paymentMethods]);

  const bankCodeOptions = useMemo(() => {
    const values = new Set<string>();
    bankCodeValues.forEach((value) => {
      if (value.actualCode) values.add(value.actualCode);
    });
    bankBranches.forEach((branch) => {
      if (branch.bankCode) values.add(branch.bankCode);
    });
    return Array.from(values).sort();
  }, [bankBranches, bankCodeValues]);

  const bankLabelByCode = useMemo(() => {
    const labels = new Map<string, string>();
    bankCodeValues.forEach((value) => {
      const code = value.actualCode?.trim();
      if (!code) return;
      const description = value.description?.trim();
      labels.set(code, description ? `${code} - ${description}` : code);
    });
    bankBranches.forEach((branch) => {
      if (!branch.bankCode || labels.has(branch.bankCode)) return;
      labels.set(branch.bankCode, branch.bankCode);
    });
    return labels;
  }, [bankBranches, bankCodeValues]);

  const activePaymentMethods = profileForm.paymentMethods.filter((method) => method.isActive);
  const percentageTotal = activePaymentMethods
    .filter((method) => method.paymentMode === 'Percentage')
    .reduce((total, method) => total + Number(method.paymentPercent || 0), 0);
  const fixedBaseEquivalent = activePaymentMethods
    .filter((method) => method.paymentMode === 'FixedAmount')
    .reduce((total, method) => total + Number(method.amount || 0) * (Number(method.exchangeRate || 1) || 1), 0);

  const resolveExchangeRate = (currencyCode: string) => {
    const currency = currencyCode.trim().toUpperCase();
    if (!currency || currency === baseCurrency.toUpperCase()) return 1;
    const match = exchangeRates
      .filter((rate) => rate.currencyCode.toUpperCase() === currency)
      .sort((a, b) => (b.payPeriod || 0) - (a.payPeriod || 0))[0];
    return match?.rate && match.rate > 0 ? match.rate : 1;
  };

  const updatePaymentMethod = (index: number, patch: Partial<PaymentMethodForm>) => {
    setProfileForm((current) => ({
      ...current,
      paymentMethods: current.paymentMethods.map((method, methodIndex) => {
        if (methodIndex !== index) return method;
        const next = { ...method, ...patch };
        if (patch.currencyCode) next.exchangeRate = resolveExchangeRate(patch.currencyCode);
        if (patch.paymentMode === 'Percentage') {
          next.amount = 0;
          next.exchangeRate = next.currencyCode.toUpperCase() === baseCurrency.toUpperCase() ? 1 : next.exchangeRate;
        }
        if (patch.paymentMode === 'FixedAmount') {
          next.paymentPercent = 0;
          next.exchangeRate = next.exchangeRate || resolveExchangeRate(next.currencyCode);
        }
        return next;
      }),
    }));
  };

  const addPaymentMethod = () => {
    const nextIndex = profileForm.paymentMethods.length;
    setSelectedPaymentMethodIndex(nextIndex);
    setProfileForm((current) => ({
      ...current,
      paymentMethods: [
        ...current.paymentMethods,
        { ...createDefaultPaymentMethod(current.currencyCode || baseCurrency, current.paymentMethods.length + 1), paymentPercent: 0 },
      ],
    }));
  };

  const removePaymentMethod = (index: number) => {
    const nextCount = Math.max(profileForm.paymentMethods.length - 1, 1);
    setSelectedPaymentMethodIndex((current) => (current > index ? current - 1 : Math.min(current, nextCount - 1)));
    setProfileForm((current) => {
      const nextMethods = current.paymentMethods
        .filter((_, methodIndex) => methodIndex !== index)
        .map((method, methodIndex) => ({ ...method, sequenceNo: methodIndex + 1 }));
      return {
        ...current,
        paymentMethods: nextMethods.length > 0 ? nextMethods : [createDefaultPaymentMethod(current.currencyCode || baseCurrency)],
      };
    });
  };

  const validatePaymentMethods = () => {
    const activeRows = profileForm.paymentMethods.filter((method) => method.isActive);
    if (activeRows.length === 0) return 'Add at least one active payment method.';
    const percentageRows = activeRows.filter((method) => method.paymentMode === 'Percentage');
    if (percentageRows.length === 0) return 'Add one percentage row for the remaining net salary.';
    const percentTotal = percentageRows.reduce((total, method) => total + Number(method.paymentPercent || 0), 0);
    if (Math.round(percentTotal * 10000) / 10000 !== 100) {
      return 'Percentage payment rows must total 100%. Fixed amounts are deducted before this split.';
    }
    const invalidFixedRow = activeRows.find((method) => method.paymentMode === 'FixedAmount' && Number(method.amount || 0) <= 0);
    if (invalidFixedRow) return 'Fixed amount rows must have an amount greater than zero.';
    return null;
  };

  // ⚠ The COMPLETE list, every row with its id. A payload missing a row deletes that row on
  // payroll's side — the upsert is a replace-set.
  const buildPaymentMethodPayload = () =>
    profileForm.paymentMethods.map((method, index) => ({
      id: method.id || undefined,
      paymentType: method.paymentType || 'Bank',
      paymentMode: method.paymentMode,
      paymentPercent: method.paymentMode === 'Percentage' ? Number(method.paymentPercent || 0) : null,
      amount: method.paymentMode === 'FixedAmount' ? Number(method.amount || 0) : null,
      bankCode: method.paymentType === 'Bank' ? method.bankCode || null : null,
      bankBranchCode: method.paymentType === 'Bank' ? method.bankBranchCode || null : null,
      accountNumber: method.paymentType === 'Bank' ? method.accountNumber || null : null,
      chequeNumber: method.paymentType === 'Cheque' ? method.chequeNumber || null : null,
      chequeBankCode: method.paymentType === 'Cheque' ? method.chequeBankCode || null : null,
      currencyCode: method.currencyCode || profileForm.currencyCode || baseCurrency,
      exchangeRate: Number(method.exchangeRate || 1) || 1,
      sequenceNo: index + 1,
      startDate: method.startDate || null,
      endDate: method.endDate || null,
      isActive: method.isActive,
    }));

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (readOnly || !canSave) return;

    const paymentError = validatePaymentMethods();
    if (paymentError) {
      setError(paymentError);
      toast({ title: 'Payment method error', description: paymentError, variant: 'destructive' });
      return;
    }

    setSaving(true);
    setError(null);
    try {
      // ⚠ The stale check. What payroll holds NOW, not what this form loaded: if the list has
      // changed underneath, a save would replace it with this form's view and drop the difference.
      const latest = await reload();
      const loadedCount = initialProfile?.paymentMethods?.length ?? 0;
      const latestCount = latest?.paymentMethods?.length ?? 0;
      if (initialProfile && latest && latestCount !== loadedCount) {
        const message =
          `This profile's payment methods changed since you opened it (${loadedCount} → ${latestCount} rows). ` +
          'Reload before saving, or the save would replace the list with this stale copy.';
        setError(message);
        toast({ title: 'Profile changed elsewhere', description: message, variant: 'destructive' });
        return;
      }

      const saved = await payrollService.upsertEmployeeProfile({
        id: (latest?.id || profileForm.id) || undefined,
        employeeId: profileForm.employeeId,
        employeeNumber: profileForm.employeeNumber,
        legacyEmployeeId: profileForm.legacyEmployeeId || null,
        legacyEmployeeNumber: profileForm.legacyEmployeeNumber || null,
        payrollActive: profileForm.payrollActive,
        payTax: profileForm.payTax,
        ssfApplicable: profileForm.ssfApplicable,
        ssfNumber: profileForm.ssfNumber || null,
        grossUp: profileForm.grossUp,
        tier2Only: profileForm.tier2Only,
        overtimeEligible: profileForm.overtimeEligible,
        currencyCode: profileForm.currencyCode,
        salaryBasis: {
          monthlyBasicSalary: profileForm.monthlyBasicSalary,
          currencyCode: profileForm.currencyCode,
          effectiveFrom: today,
          isActive: true,
        },
        paymentMethods: buildPaymentMethodPayload(),
        employeeComponents: [],
      } as any);

      toast({ title: 'Payroll updated', description: 'Profile saved.' });
      onSaved?.(saved);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Payroll operation failed.';
      setError(message);
      toast({ title: 'Payroll error', description: message, variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const locked = readOnly || !canSave;

  return (
    <form className="grid gap-3" onSubmit={(event) => void save(event)}>
      {error && <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</div>}

      <Tabs defaultValue="profile" className="space-y-3">
        <TabsList className="grid w-full grid-cols-2">
          <TabsTrigger value="profile">Profile</TabsTrigger>
          <TabsTrigger value="payments">Payment Methods</TabsTrigger>
        </TabsList>

        <TabsContent value="profile" className="mt-0 space-y-3">
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Employee Number">
              <Input value={profileForm.employeeNumber} disabled={locked} onChange={(event) => setProfileForm((current) => ({ ...current, employeeNumber: event.target.value }))} />
            </Field>
            <Field label="SSF Number">
              <Input value={profileForm.ssfNumber} disabled={locked} onChange={(event) => setProfileForm((current) => ({ ...current, ssfNumber: event.target.value }))} />
            </Field>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Monthly Basic Salary">
              <Input type="number" step="0.01" value={profileForm.monthlyBasicSalary} disabled={locked || basicSalaryLocked} onChange={(event) => setProfileForm((current) => ({ ...current, monthlyBasicSalary: Number(event.target.value) }))} />
              {basicSalaryLocked && !locked && (
                <p className="mt-1 text-xs text-muted-foreground">Changed through an approved salary change request.</p>
              )}
            </Field>
            <Field label="Currency">
              <select
                className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                value={profileForm.currencyCode}
                disabled={locked}
                onChange={(event) => setProfileForm((current) => ({ ...current, currencyCode: event.target.value }))}
              >
                {currencyOptions.map((currency) => (
                  <option key={currency} value={currency}>{currency}</option>
                ))}
              </select>
            </Field>
          </div>
          <div className="grid gap-2 md:grid-cols-2">
            <BooleanField label="Payroll active" disabled={locked} checked={profileForm.payrollActive} onChange={(checked) => setProfileForm((current) => ({ ...current, payrollActive: checked }))} />
            <BooleanField label="PAYE" disabled={locked} checked={profileForm.payTax} onChange={(checked) => setProfileForm((current) => ({ ...current, payTax: checked }))} />
            <BooleanField label="SSF" disabled={locked} checked={profileForm.ssfApplicable} onChange={(checked) => setProfileForm((current) => ({ ...current, ssfApplicable: checked }))} />
            <BooleanField label="Overtime" disabled={locked} checked={profileForm.overtimeEligible} onChange={(checked) => setProfileForm((current) => ({ ...current, overtimeEligible: checked }))} />
            <BooleanField label="Gross up" disabled={locked} checked={profileForm.grossUp} onChange={(checked) => setProfileForm((current) => ({ ...current, grossUp: checked }))} />
            <BooleanField label="Tier 2 only" disabled={locked} checked={profileForm.tier2Only} onChange={(checked) => setProfileForm((current) => ({ ...current, tier2Only: checked }))} />
          </div>
        </TabsContent>

        <TabsContent value="payments" className="mt-0 space-y-3">
          <div className="grid gap-2 text-xs sm:grid-cols-2">
            <div className={`rounded-md border px-3 py-2 ${Math.round(percentageTotal * 10000) / 10000 === 100 ? 'bg-muted/30' : 'border-destructive/40 bg-destructive/10 text-destructive'}`}>
              <div className="text-muted-foreground">Percentage total</div>
              <div className="font-semibold">{percentageTotal.toFixed(2)}%</div>
            </div>
            <div className="rounded-md border bg-muted/30 px-3 py-2">
              <div className="text-muted-foreground">Fixed base equivalent</div>
              <div className="font-semibold">{money(fixedBaseEquivalent, baseCurrency)}</div>
            </div>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table className="min-w-[1060px] text-xs">
              <TableHeader>
                <TableRow>
                  <TableHead className="h-7 w-[52px] py-1">Row</TableHead>
                  <TableHead className="h-7 w-[88px] py-1">Type</TableHead>
                  <TableHead className="h-7 w-[116px] py-1">Split</TableHead>
                  <TableHead className="h-7 w-[94px] py-1">Value</TableHead>
                  <TableHead className="h-7 w-[72px] py-1">Currency</TableHead>
                  <TableHead className="h-7 w-[72px] py-1">Rate</TableHead>
                  <TableHead className="h-7 w-[180px] py-1">Bank</TableHead>
                  <TableHead className="h-7 w-[150px] py-1">Branch</TableHead>
                  <TableHead className="h-7 w-[136px] py-1">Account/Cheque</TableHead>
                  <TableHead className="h-7 w-[36px] py-1 text-right"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {profileForm.paymentMethods.map((method, index) => {
                  const branchOptions = bankBranches.filter((branch) => !method.bankCode || branch.bankCode === method.bankCode);
                  const isSelected = selectedPaymentMethodIndex === index;

                  return (
                    <TableRow
                      key={method.id || index}
                      className={`cursor-pointer whitespace-nowrap hover:bg-muted/50 ${isSelected ? 'bg-blue-50/70' : ''}`}
                      onClick={() => setSelectedPaymentMethodIndex(index)}
                    >
                      <TableCell className="py-1">
                        <div className="flex items-center gap-2">
                          <Checkbox checked={method.isActive} disabled={locked} onCheckedChange={(value) => updatePaymentMethod(index, { isActive: value === true })} />
                          <span className="font-medium">{index + 1}</span>
                        </div>
                      </TableCell>
                      <TableCell className="py-1">
                        <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" disabled={locked} value={method.paymentType} onChange={(event) => updatePaymentMethod(index, { paymentType: event.target.value })}>
                          {paymentMethodOptions.map((option) => (
                            <option key={option} value={option}>{option}</option>
                          ))}
                        </select>
                      </TableCell>
                      <TableCell className="py-1">
                        <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" disabled={locked} value={method.paymentMode} onChange={(event) => updatePaymentMethod(index, { paymentMode: event.target.value as PaymentMethodForm['paymentMode'] })}>
                          <option value="Percentage">Percentage</option>
                          <option value="FixedAmount">Fixed Amount</option>
                        </select>
                      </TableCell>
                      <TableCell className="py-1">
                        <Input
                          className="h-7 px-1.5 text-xs"
                          type="number"
                          disabled={locked}
                          step={method.paymentMode === 'FixedAmount' ? '0.01' : '0.0001'}
                          value={method.paymentMode === 'FixedAmount' ? method.amount : method.paymentPercent}
                          onChange={(event) => updatePaymentMethod(index, method.paymentMode === 'FixedAmount'
                            ? { amount: Number(event.target.value) }
                            : { paymentPercent: Number(event.target.value) })}
                        />
                      </TableCell>
                      <TableCell className="py-1">
                        <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" disabled={locked} value={method.currencyCode} onChange={(event) => updatePaymentMethod(index, { currencyCode: event.target.value })}>
                          {currencyOptions.map((currency) => (
                            <option key={currency} value={currency}>{currency}</option>
                          ))}
                        </select>
                      </TableCell>
                      <TableCell className="py-1">
                        <Input className="h-7 px-1.5 text-xs" type="number" step="0.0001" disabled={locked} value={method.exchangeRate} onChange={(event) => updatePaymentMethod(index, { exchangeRate: Number(event.target.value) })} />
                      </TableCell>
                      <TableCell className="py-1">
                        {method.paymentType === 'Bank' && (
                          bankCodeOptions.length > 0 ? (
                            <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" disabled={locked} value={method.bankCode} onChange={(event) => updatePaymentMethod(index, { bankCode: event.target.value, bankBranchCode: '' })}>
                              <option value="">Select bank</option>
                              {bankCodeOptions.map((bankCode) => (
                                <option key={bankCode} value={bankCode}>{bankLabelByCode.get(bankCode) ?? bankCode}</option>
                              ))}
                            </select>
                          ) : (
                            <Input className="h-7 px-1.5 text-xs" placeholder="Bank" disabled={locked} value={method.bankCode} onChange={(event) => updatePaymentMethod(index, { bankCode: event.target.value })} />
                          )
                        )}
                        {method.paymentType === 'Cheque' && (
                          bankCodeOptions.length > 0 ? (
                            <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" disabled={locked} value={method.chequeBankCode} onChange={(event) => updatePaymentMethod(index, { chequeBankCode: event.target.value })}>
                              <option value="">Cheque bank</option>
                              {bankCodeOptions.map((bankCode) => (
                                <option key={bankCode} value={bankCode}>{bankLabelByCode.get(bankCode) ?? bankCode}</option>
                              ))}
                            </select>
                          ) : (
                            <Input className="h-7 px-1.5 text-xs" placeholder="Cheque bank" disabled={locked} value={method.chequeBankCode} onChange={(event) => updatePaymentMethod(index, { chequeBankCode: event.target.value })} />
                          )
                        )}
                        {method.paymentType === 'Cash' && (
                          <span className="inline-flex h-7 w-full items-center rounded-md border border-dashed px-2 text-muted-foreground">Cash</span>
                        )}
                      </TableCell>
                      <TableCell className="py-1">
                        {method.paymentType === 'Bank' && (
                          branchOptions.length > 0 ? (
                            <select className="h-7 w-full rounded-md border bg-background px-1.5 text-xs" disabled={locked} value={method.bankBranchCode} onChange={(event) => updatePaymentMethod(index, { bankBranchCode: event.target.value })}>
                              <option value="">Branch</option>
                              {branchOptions.map((branch) => (
                                <option key={`${branch.bankCode}-${branch.branchCode}`} value={branch.branchCode}>
                                  {branch.branchCode} {branch.branchDescription ? `- ${branch.branchDescription}` : ''}
                                </option>
                              ))}
                            </select>
                          ) : (
                            <Input className="h-7 px-1.5 text-xs" placeholder="Branch" disabled={locked} value={method.bankBranchCode} onChange={(event) => updatePaymentMethod(index, { bankBranchCode: event.target.value })} />
                          )
                        )}
                        {method.paymentType !== 'Bank' && (
                          <span className="inline-flex h-7 w-full items-center rounded-md border border-dashed px-2 text-muted-foreground">-</span>
                        )}
                      </TableCell>
                      <TableCell className="py-1">
                        {method.paymentType === 'Bank' && (
                          <Input className="h-7 px-1.5 text-xs" placeholder="Account No" disabled={locked} value={method.accountNumber} onChange={(event) => updatePaymentMethod(index, { accountNumber: event.target.value })} />
                        )}
                        {method.paymentType === 'Cheque' && (
                          <Input className="h-7 px-1.5 text-xs" placeholder="Cheque No" disabled={locked} value={method.chequeNumber} onChange={(event) => updatePaymentMethod(index, { chequeNumber: event.target.value })} />
                        )}
                        {method.paymentType === 'Cash' && (
                          <span className="inline-flex h-7 w-full items-center rounded-md border border-dashed px-2 text-muted-foreground">-</span>
                        )}
                      </TableCell>
                      <TableCell className="py-1 text-right">
                        <Button type="button" variant="ghost" size="icon" className="h-7 w-7" title="Delete row" aria-label="Delete row" disabled={locked || profileForm.paymentMethods.length === 1} onClick={(event) => { event.stopPropagation(); removePaymentMethod(index); }}>
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>

          {!locked && (
            <Button type="button" variant="outline" size="sm" onClick={addPaymentMethod}>
              <Plus className="mr-2 h-4 w-4" />
              Add Row
            </Button>
          )}
        </TabsContent>
      </Tabs>

      {!readOnly && (
        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" disabled={!profileForm.employeeId || saving || !canSave}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            {profileForm.id ? 'Save Changes' : 'Save Profile'}
          </Button>
          {!canSave && (
            <span className="text-xs text-muted-foreground">You can view this profile but not change it.</span>
          )}
        </div>
      )}
    </form>
  );
}
