'use client';

import Link from 'next/link';
import {
  FormEvent,
  ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react';
import {
  ArrowLeft,
  CreditCard,
  Edit3,
  Loader2,
  RefreshCw,
  Save,
  Search,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import {
  PayrollCodeSetup,
  PayrollContributionTransaction,
  PayrollEmployeeProfile,
  PayrollParameterSet,
  payrollService,
} from '@/services/payrollService';

type ContributionOption = {
  key: string;
  codeType: string;
  code: string;
  name: string;
};

type ContributionTransactionType = 'Withdrawal' | 'Interest';

const today = new Date().toISOString().slice(0, 10);
const CONTRIBUTION_CODE_TYPE_ALIASES = ['CON', 'CONT', 'CONTR', 'CONTRIB'];

const defaultForm = {
  id: '',
  employeeProfileId: '',
  employeeNumber: '',
  employeeName: '',
  contributionKey: '',
  transactionType: 'Withdrawal' as ContributionTransactionType,
  effectiveDate: today,
  amount: 0,
};

function normalize(value?: string | null) {
  return (value || '').trim().toUpperCase();
}

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function dateInRange(value: string, from?: string | null, to?: string | null) {
  return Boolean(value && (!from || value >= from) && (!to || value <= to));
}

function transactionDateForPeriod(
  from?: string | null,
  to?: string | null,
  currentValue = today
) {
  const start = dateValue(from);
  const end = dateValue(to);
  return dateInRange(currentValue, start, end)
    ? currentValue
    : start || end || currentValue;
}

function formatPayrollPeriodLabel(parameters: PayrollParameterSet | null) {
  const source =
    dateValue(parameters?.currentPeriodTo) ||
    dateValue(parameters?.currentPeriodFrom);
  if (!source) {
    return 'Not configured';
  }

  const [year, month] = source.split('-').map(Number);
  return year && month
    ? new Intl.DateTimeFormat('en-US', {
        month: 'long',
        year: 'numeric',
      }).format(new Date(year, month - 1, 1))
    : 'Not configured';
}

function money(value: number | null | undefined, currency = 'GHS') {
  return new Intl.NumberFormat('en-GH', {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(value ?? 0);
}

function contributionKey(
  option: Pick<ContributionOption, 'codeType' | 'code'>
) {
  return `${option.codeType}::${option.code}`;
}

function findContributionCodeType(setup: PayrollCodeSetup) {
  return (
    setup.codeTypes.find((type) =>
      CONTRIBUTION_CODE_TYPE_ALIASES.includes(normalize(type.codeType))
    ) ??
    setup.codeTypes.find((type) =>
      normalize(type.description).includes('CONTRIBUTION')
    )
  );
}

function contributionOptionsFromSetup(
  setup: PayrollCodeSetup,
  preferredCodeType?: string | null
) {
  const targetCodeType =
    normalize(preferredCodeType) ||
    normalize(findContributionCodeType(setup)?.codeType);
  return setup.codeValues
    .filter(
      (value) =>
        !value.blocked &&
        (!targetCodeType || normalize(value.codeType) === targetCodeType)
    )
    .map((value) => ({
      key: contributionKey({
        codeType: value.codeType,
        code: value.actualCode,
      }),
      codeType: value.codeType,
      code: value.actualCode,
      name: value.description,
    }))
    .sort((first, second) => first.name.localeCompare(second.name));
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

export default function ContributionsPage() {
  const { toast } = useToast();
  const [activeParameters, setActiveParameters] =
    useState<PayrollParameterSet | null>(null);
  const [codeSetup, setCodeSetup] = useState<PayrollCodeSetup>({
    codeTypes: [],
    codeValues: [],
  });
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [transactions, setTransactions] = useState<
    PayrollContributionTransaction[]
  >([]);
  const [form, setForm] = useState(defaultForm);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const contributionOptions = useMemo(
    () => contributionOptionsFromSetup(codeSetup),
    [codeSetup]
  );
  const selectedContribution = useMemo(
    () =>
      contributionOptions.find(
        (option) => option.key === form.contributionKey
      ) ?? null,
    [contributionOptions, form.contributionKey]
  );
  const currency = activeParameters?.baseCurrency || 'GHS';
  const currentPeriodFrom = dateValue(activeParameters?.currentPeriodFrom);
  const currentPeriodTo = dateValue(activeParameters?.currentPeriodTo);

  const filteredProfiles = useMemo(() => {
    const activeProfiles = profiles.filter((profile) => profile.payrollActive);
    const term = employeeSearch.trim().toLowerCase();
    if (!term) {
      return activeProfiles;
    }

    return activeProfiles.filter(
      (profile) =>
        profile.employeeNumber.toLowerCase().includes(term) ||
        profile.employeeName.toLowerCase().includes(term)
    );
  }, [employeeSearch, profiles]);

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, profileList, allCodes, transactionList] = await Promise.all(
        [
          payrollService.getSetupSummary(),
          payrollService.getEmployeeProfiles(),
          payrollService.getCodeSetup(),
          payrollService.getContributionTransactions(),
        ]
      );
      const contributionCodeType = findContributionCodeType(allCodes);
      const scopedCodes = contributionCodeType
        ? await payrollService.getCodeSetup(contributionCodeType.codeType)
        : allCodes;
      const options = contributionOptionsFromSetup(
        scopedCodes,
        contributionCodeType?.codeType
      );
      const periodDate = transactionDateForPeriod(
        setup.activeParameters?.currentPeriodFrom,
        setup.activeParameters?.currentPeriodTo
      );

      setActiveParameters(setup.activeParameters ?? null);
      setProfiles(profileList);
      setCodeSetup(scopedCodes);
      setTransactions(transactionList);
      setForm((current) => ({
        ...current,
        contributionKey: current.contributionKey || options[0]?.key || '',
        effectiveDate: dateInRange(
          current.effectiveDate,
          dateValue(setup.activeParameters?.currentPeriodFrom),
          dateValue(setup.activeParameters?.currentPeriodTo)
        )
          ? current.effectiveDate
          : periodDate,
      }));
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to load contributions.'
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const chooseProfile = (profile: PayrollEmployeeProfile) => {
    setForm((current) => ({
      ...current,
      employeeProfileId: profile.id || '',
      employeeNumber: profile.employeeNumber,
      employeeName: profile.employeeName,
    }));
  };

  const editTransaction = (transaction: PayrollContributionTransaction) => {
    const nextContributionKey = contributionKey({
      codeType: transaction.contributionCodeType,
      code: transaction.contributionCode,
    });

    setForm({
      id: transaction.id || '',
      employeeProfileId: transaction.employeeProfileId,
      employeeNumber: transaction.employeeNumber,
      employeeName: transaction.employeeName,
      contributionKey: nextContributionKey,
      transactionType: transaction.transactionType,
      effectiveDate:
        dateValue(transaction.effectiveDate) ||
        transactionDateForPeriod(
          activeParameters?.currentPeriodFrom,
          activeParameters?.currentPeriodTo
        ),
      amount: transaction.amount,
    });
    setEmployeeSearch(transaction.employeeNumber);
  };

  const resetForm = (parameters = activeParameters) => {
    setForm({
      ...defaultForm,
      contributionKey: contributionOptions[0]?.key || '',
      effectiveDate: transactionDateForPeriod(
        parameters?.currentPeriodFrom,
        parameters?.currentPeriodTo
      ),
    });
    setEmployeeSearch('');
  };

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedContribution) {
      setError('Contribution type is required.');
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await payrollService.upsertContributionTransaction({
        id: form.id || undefined,
        employeeProfileId: form.employeeProfileId,
        employeeNumber: form.employeeNumber,
        contributionCodeType: selectedContribution.codeType,
        contributionCode: selectedContribution.code,
        contributionName: selectedContribution.name,
        transactionType: form.transactionType,
        effectiveDate: form.effectiveDate,
        amount: form.amount,
        legacyCompanyCode: activeParameters?.legacyCompanyCode ?? null,
        isActive: true,
      });
      const [setup, transactionList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getContributionTransactions(),
      ]);
      setActiveParameters(setup.activeParameters ?? null);
      setTransactions(transactionList);
      resetForm(setup.activeParameters ?? null);
      toast({
        title: 'Contribution saved',
        description: `${form.employeeNumber} updated.`,
      });
    } catch (err) {
      const message =
        err instanceof Error ? err.message : 'Unable to save contribution.';
      setError(message);
      toast({
        title: 'Contribution error',
        description: message,
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <main className="flex min-h-[420px] items-center justify-center p-6 text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading contributions
      </main>
    );
  }

  return (
    <main className="space-y-4 p-6">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div className="flex flex-wrap items-center gap-2">
          <Button asChild variant="outline" size="sm">
            <Link href="/hr/payroll">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Payroll
            </Link>
          </Button>
          <h1 className="text-2xl font-semibold tracking-normal">
            Contribution Transactions
          </h1>
        </div>
        <div className="grid min-w-[280px] gap-3 rounded-md border bg-muted/40 p-3 text-sm sm:grid-cols-[1fr_auto] xl:min-w-[420px]">
          <div>
            <div className="text-xs text-muted-foreground">Payroll Period</div>
            <div className="font-semibold">
              {formatPayrollPeriodLabel(activeParameters)}
            </div>
          </div>
          <div className="sm:text-right">
            <div className="text-xs text-muted-foreground">Period No</div>
            <div className="font-semibold">
              {activeParameters?.currentPayPeriod || '-'}
            </div>
          </div>
        </div>
      </div>

      {error && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="grid gap-4 xl:grid-cols-[minmax(0,430px)_1fr]">
        <Card className="rounded-md">
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <CreditCard className="h-4 w-4" />
              Contribution
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search employee no or name"
                value={employeeSearch}
                onChange={(event) => setEmployeeSearch(event.target.value)}
              />
              <Button
                type="button"
                variant="outline"
                onClick={() => void loadWorkspace()}
              >
                <RefreshCw className="h-4 w-4" />
              </Button>
            </div>
            <form className="grid gap-3" onSubmit={save}>
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Employee Number">
                  <select
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={form.employeeProfileId}
                    onChange={(event) => {
                      const profile = profiles.find(
                        (item) => item.id === event.target.value
                      );
                      if (profile) {
                        chooseProfile(profile);
                      }
                    }}
                  >
                    <option value="">Select employee</option>
                    {filteredProfiles.map((profile) => (
                      <option
                        key={profile.id || profile.employeeNumber}
                        value={profile.id}
                      >
                        {profile.employeeNumber}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field label="Employee Name">
                  <Input readOnly value={form.employeeName} />
                </Field>
              </div>
              <Field label="Contribution Type">
                <select
                  className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                  value={form.contributionKey}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      contributionKey: event.target.value,
                    }))
                  }
                >
                  <option value="">Select contribution</option>
                  {contributionOptions.map((option) => (
                    <option key={option.key} value={option.key}>
                      {option.code} - {option.name}
                    </option>
                  ))}
                </select>
              </Field>
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Effective Date">
                  <Input
                    type="date"
                    min={currentPeriodFrom || undefined}
                    max={currentPeriodTo || undefined}
                    value={form.effectiveDate}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        effectiveDate: event.target.value,
                      }))
                    }
                  />
                </Field>
                <Field label="Transaction Type">
                  <select
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={form.transactionType}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        transactionType: event.target
                          .value as ContributionTransactionType,
                      }))
                    }
                  >
                    <option value="Withdrawal">Withdrawal</option>
                    <option value="Interest">Interest</option>
                  </select>
                </Field>
              </div>
              <Field label="Amount">
                <Input
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.amount}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      amount: Number(event.target.value),
                    }))
                  }
                />
              </Field>
              <div className="flex gap-2">
                <Button
                  type="submit"
                  disabled={
                    !form.employeeProfileId ||
                    !selectedContribution ||
                    form.amount <= 0 ||
                    busy
                  }
                >
                  {busy ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="mr-2 h-4 w-4" />
                  )}
                  Save
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => resetForm()}
                >
                  Clear
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <Card className="rounded-md">
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <CardTitle className="flex items-center gap-2 text-base">
                <Search className="h-4 w-4" />
                History
              </CardTitle>
              <PayrollGridExportButton
                rows={transactions}
                fileName="contribution-transactions"
                columns={[
                  { header: 'Employee No', value: (row) => row.employeeNumber },
                  { header: 'Employee Name', value: (row) => row.employeeName },
                  {
                    header: 'Code Type',
                    value: (row) => row.contributionCodeType,
                  },
                  {
                    header: 'Contribution Code',
                    value: (row) => row.contributionCode,
                  },
                  {
                    header: 'Contribution',
                    value: (row) => row.contributionName,
                  },
                  {
                    header: 'Effective Date',
                    value: (row) => dateValue(row.effectiveDate),
                  },
                  {
                    header: 'Transaction Type',
                    value: (row) => row.transactionType,
                  },
                  { header: 'Amount', value: (row) => row.amount },
                ]}
              />
            </div>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <Table className="min-w-[960px]">
                <TableHeader>
                  <TableRow>
                    <TableHead>Employee No</TableHead>
                    <TableHead>Employee Name</TableHead>
                    <TableHead>Code Type</TableHead>
                    <TableHead>Code</TableHead>
                    <TableHead>Contribution</TableHead>
                    <TableHead>Effective Date</TableHead>
                    <TableHead>Transaction Type</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                    <TableHead className="w-12" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {transactions.map((transaction) => (
                    <TableRow
                      key={transaction.id}
                      className="cursor-pointer"
                      onClick={() => editTransaction(transaction)}
                    >
                      <TableCell className="font-medium">
                        {transaction.employeeNumber}
                      </TableCell>
                      <TableCell>{transaction.employeeName}</TableCell>
                      <TableCell>{transaction.contributionCodeType}</TableCell>
                      <TableCell>{transaction.contributionCode}</TableCell>
                      <TableCell>{transaction.contributionName}</TableCell>
                      <TableCell>
                        {dateValue(transaction.effectiveDate)}
                      </TableCell>
                      <TableCell>{transaction.transactionType}</TableCell>
                      <TableCell className="text-right">
                        {money(transaction.amount, currency)}
                      </TableCell>
                      <TableCell>
                        <Edit3 className="h-4 w-4 text-muted-foreground" />
                      </TableCell>
                    </TableRow>
                  ))}
                  {transactions.length === 0 && (
                    <TableRow>
                      <TableCell
                        colSpan={9}
                        className="py-6 text-center text-sm text-muted-foreground"
                      >
                        No contribution transactions saved.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
