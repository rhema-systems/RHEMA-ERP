'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  FormEvent,
  ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react';
import {
  Award,
  BarChart3,
  CheckCircle2,
  CreditCard,
  FileText,
  Loader2,
  Mail,
  Play,
  Printer,
  RefreshCw,
  Save,
  Settings,
  Users,
} from 'lucide-react';

import { PayrollGridExportButton } from '@/components/hr/payroll/PayrollGridExportButton';
import { PayrollPayslipPreviewDialog } from '@/components/hr/payroll/PayrollPayslipPreviewDialog';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import {
  formatPendingApprovers,
  useWorkflowEntitySummaries,
} from '@/hooks/useWorkflowEntitySummaries';
import {
  PayrollEmployeeProfile,
  PayrollJournalPreview,
  PayrollJournalPosting,
  PayrollParameterSet,
  PayrollPayslip,
  PayrollPayslipEmailResult,
  PayrollRun,
  PayrollRunStatus,
  PayrollSummaryReport,
  payrollService,
} from '@/services/payrollService';

const today = new Date().toISOString().slice(0, 10);

const defaultRunForm = {
  payPeriod: Number(new Date().toISOString().slice(0, 7).replace('-', '')),
  payPeriodFrom: today,
  payPeriodTo: today,
  currencyCode: 'GHS',
  isSeparateBonusRun: false,
  separateBonusCode: '',
  notes: '',
};

const payrollTabValues = new Set(['runs', 'reports', 'workflow']);

const statusTone: Record<
  PayrollRunStatus,
  'outline' | 'secondary' | 'default' | 'destructive'
> = {
  Draft: 'outline',
  Calculated: 'secondary',
  InReview: 'secondary',
  Approved: 'default',
  Closed: 'default',
  RolledBack: 'destructive',
};

const statusClassName: Partial<Record<PayrollRunStatus, string>> = {
  Calculated: 'border-amber-300 bg-amber-100 text-amber-900 hover:bg-amber-100',
  InReview: 'border-sky-300 bg-sky-100 text-sky-900 hover:bg-sky-100',
};

function RunStatusBadge({ status }: { status: PayrollRunStatus }) {
  return (
    <Badge variant={statusTone[status]} className={statusClassName[status]}>
      {status}
    </Badge>
  );
}

type PayslipScope = 'all' | 'category' | 'employee';

const payslipCategoryTypes = [
  { value: 'StaffCategory', label: 'Staff Category' },
  { value: 'Department', label: 'Department' },
  { value: 'Section', label: 'Section' },
  { value: 'Position', label: 'Position' },
  { value: 'Location', label: 'Location' },
];

const money = (value: number | null | undefined, currency = 'GHS') =>
  new Intl.NumberFormat('en-GH', {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(value ?? 0);

const roundCurrency = (value: number) =>
  Math.round((value + Number.EPSILON) * 100) / 100;

function journalDebitAmount(line: {
  debitCredit?: string | null;
  amount: number;
}) {
  return line.debitCredit === 'DR' ? Math.abs(line.amount) : null;
}

function journalCreditAmount(line: {
  debitCredit?: string | null;
  amount: number;
}) {
  return line.debitCredit === 'CR' ? Math.abs(line.amount) : null;
}

function compactText(value?: string | null) {
  return value?.trim() || '';
}

function profileCategoryValue(
  profile: PayrollEmployeeProfile,
  categoryType: string
) {
  switch (categoryType) {
    case 'Department':
      return compactText(profile.departmentName);
    case 'Section':
      return compactText(profile.sectionName);
    case 'Position':
      return compactText(profile.positionTitle);
    case 'Location':
      return compactText(profile.jobLocation);
    case 'StaffCategory':
    default:
      return compactText(profile.staffCategory);
  }
}

function dateValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function formatPayrollPeriodLabel(from?: string | null, to?: string | null) {
  const source = dateValue(to) || dateValue(from);
  if (!source) {
    return 'Not configured';
  }

  const [year, month] = source.split('-').map(Number);
  if (!year || !month) {
    return 'Not configured';
  }

  return new Intl.DateTimeFormat('en-US', {
    month: 'long',
    year: 'numeric',
  }).format(new Date(year, month - 1, 1));
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      {children}
    </div>
  );
}

export default function PayrollPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [activeTab, setActiveTab] = useState('runs');
  const [runs, setRuns] = useState<PayrollRun[]>([]);
  const [profiles, setProfiles] = useState<PayrollEmployeeProfile[]>([]);
  const [activeParameters, setActiveParameters] =
    useState<PayrollParameterSet | null>(null);
  const [selectedRunId, setSelectedRunId] = useState('');
  const [selectedRun, setSelectedRun] = useState<PayrollRun | null>(null);
  const [summary, setSummary] = useState<PayrollSummaryReport | null>(null);
  const [payslips, setPayslips] = useState<PayrollPayslip[]>([]);
  const [previewPayslip, setPreviewPayslip] = useState<PayrollPayslip | null>(
    null
  );
  const [selectedPayslipEmployeeIds, setSelectedPayslipEmployeeIds] = useState<
    Set<string>
  >(new Set<string>());
  const [payslipEmailResult, setPayslipEmailResult] =
    useState<PayrollPayslipEmailResult | null>(null);
  const [journalPosting, setJournalPosting] =
    useState<PayrollJournalPosting | null>(null);
  const [journalPreview, setJournalPreview] =
    useState<PayrollJournalPreview | null>(null);
  const [runForm, setRunForm] = useState(defaultRunForm);
  const [payslipDialogOpen, setPayslipDialogOpen] = useState(false);
  const [postingPreviewOpen, setPostingPreviewOpen] = useState(false);
  const [payslipScope, setPayslipScope] = useState<PayslipScope>('all');
  const [payslipCategoryType, setPayslipCategoryType] =
    useState('StaffCategory');
  const [payslipCategoryValue, setPayslipCategoryValue] = useState('');
  const [payslipEmployeeSearch, setPayslipEmployeeSearch] = useState('');
  const [payslipEmployeeId, setPayslipEmployeeId] = useState('');
  const [postDialogOpen, setPostDialogOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const workflowSummaryRefreshKey = useMemo(
    () =>
      runs
        .map(
          (run) =>
            `${run.id}:${run.status}:${run.reviewedAt || ''}:${run.approvedAt || ''}`
        )
        .sort()
        .join('|'),
    [runs]
  );
  const {
    summariesById: workflowSummariesById,
  } = useWorkflowEntitySummaries(
    'PayrollRun',
    runs.map((run) => run.id),
    runs.length > 0,
    workflowSummaryRefreshKey
  );

  const applyActivePayrollParameters = useCallback(
    (parameters?: PayrollParameterSet | null) => {
      setActiveParameters(parameters ?? null);
      if (!parameters) {
        return;
      }

      const periodFrom = dateValue(parameters.currentPeriodFrom);
      const periodTo = dateValue(parameters.currentPeriodTo);

      setRunForm((current) => ({
        ...current,
        payPeriod: parameters.currentPayPeriod || current.payPeriod,
        payPeriodFrom: periodFrom || current.payPeriodFrom,
        payPeriodTo: periodTo || current.payPeriodTo,
        currencyCode: parameters.baseCurrency || current.currencyCode,
      }));
    },
    []
  );

  const selectedRunOption = useMemo(
    () => runs.find((run) => run.id === selectedRunId) ?? null,
    [runs, selectedRunId]
  );

  const selectedWorkflowSummary = selectedRun
    ? workflowSummariesById[selectedRun.id]
    : undefined;
  const selectedPendingApprovers = formatPendingApprovers(
    selectedWorkflowSummary?.pendingApprovers || []
  );
  const showSelectedWorkflowBadges = selectedRun?.status === 'InReview';

  const activeProfiles = useMemo(
    () =>
      profiles
        .filter((profile) => profile.payrollActive)
        .sort((left, right) =>
          left.employeeNumber.localeCompare(right.employeeNumber)
        ),
    [profiles]
  );

  const payslipCategoryValues = useMemo(() => {
    const values = new Set<string>();
    activeProfiles.forEach((profile) => {
      const value = profileCategoryValue(profile, payslipCategoryType);
      if (value) {
        values.add(value);
      }
    });

    return Array.from(values).sort((left, right) => left.localeCompare(right));
  }, [activeProfiles, payslipCategoryType]);

  const filteredPayslipEmployees = useMemo(() => {
    const term = payslipEmployeeSearch.trim().toLowerCase();
    if (!term) {
      return activeProfiles;
    }

    return activeProfiles.filter(
      (profile) =>
        profile.employeeNumber.toLowerCase().includes(term) ||
        profile.employeeName.toLowerCase().includes(term)
    );
  }, [activeProfiles, payslipEmployeeSearch]);

  useEffect(() => {
    const syncTabFromHash = () => {
      const hash = window.location.hash.replace('#', '');
      if (payrollTabValues.has(hash)) {
        setActiveTab(hash);
      }
    };

    syncTabFromHash();
    window.addEventListener('hashchange', syncTabFromHash);
    return () => window.removeEventListener('hashchange', syncTabFromHash);
  }, []);

  const selectTab = (value: string) => {
    setActiveTab(value);
    if (typeof window !== 'undefined') {
      window.history.replaceState(
        null,
        '',
        `${window.location.pathname}#${value}`
      );
    }
  };

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [setup, runList, profileList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getRuns(),
        payrollService.getEmployeeProfiles(),
      ]);
      setRuns(runList);
      setProfiles(profileList);
      applyActivePayrollParameters(setup.activeParameters ?? null);

      const nextRunId = selectedRunId || runList[0]?.id || '';
      setSelectedRunId(nextRunId);
      if (nextRunId) {
        setSelectedRun(await payrollService.getRun(nextRunId));
      } else {
        setSelectedRun(null);
      }
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to load payroll workspace.'
      );
    } finally {
      setLoading(false);
    }
  }, [applyActivePayrollParameters, selectedRunId]);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  useEffect(() => {
    if (!selectedRunId || selectedRun?.id === selectedRunId) {
      return;
    }

    void payrollService
      .getRun(selectedRunId)
      .then(setSelectedRun)
      .catch((err) => {
        setError(
          err instanceof Error ? err.message : 'Unable to load payroll run.'
        );
      });
  }, [selectedRunId, selectedRun?.id]);

  useEffect(() => {
    setJournalPreview(null);
    setPostingPreviewOpen(false);
  }, [selectedRunId]);

  useEffect(() => {
    setPayslipCategoryValue((current) =>
      current && payslipCategoryValues.includes(current)
        ? current
        : payslipCategoryValues[0] || ''
    );
  }, [payslipCategoryValues]);

  useEffect(() => {
    setPayslipEmployeeId((current) =>
      current &&
      activeProfiles.some((profile) => profile.employeeId === current)
        ? current
        : activeProfiles[0]?.employeeId || ''
    );
  }, [activeProfiles]);

  const runOperation = async (
    label: string,
    action: () => Promise<
      | PayrollRun
      | PayrollSummaryReport
      | PayrollPayslip[]
      | PayrollPayslipEmailResult
      | PayrollJournalPosting
      | PayrollEmployeeProfile
      | unknown
    >
  ): Promise<boolean> => {
    setBusy(label);
    setError(null);
    try {
      const result = await action();
      toast({ title: 'Payroll updated', description: `${label} completed.` });
      const resultRunId =
        result &&
        typeof result === 'object' &&
        'id' in result &&
        'status' in result
          ? String((result as PayrollRun).id)
          : result && typeof result === 'object' && 'payrollRunId' in result
            ? String((result as { payrollRunId: string }).payrollRunId)
            : '';
      if (
        result &&
        typeof result === 'object' &&
        'runNumber' in result &&
        'status' in result
      ) {
        setSelectedRun(result as PayrollRun);
        setSelectedRunId((result as PayrollRun).id);
      }
      if (['Create run', 'Process'].includes(label)) {
        setSummary(null);
        setPayslips([]);
        setSelectedPayslipEmployeeIds(new Set<string>());
        setPayslipEmailResult(null);
        setJournalPosting(null);
        setJournalPreview(null);
      }
      const [setup, runList, profileList] = await Promise.all([
        payrollService.getSetupSummary(),
        payrollService.getRuns(),
        payrollService.getEmployeeProfiles(),
      ]);
      applyActivePayrollParameters(setup.activeParameters ?? null);
      setRuns(runList);
      setProfiles(profileList);
      const refreshRunId = resultRunId || selectedRunId;
      if (refreshRunId) {
        setSelectedRun(await payrollService.getRun(refreshRunId));
      }
      return true;
    } catch (err) {
      const message =
        err instanceof Error ? err.message : 'Payroll operation failed.';
      setError(message);
      toast({
        title: 'Payroll error',
        description: message,
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(null);
    }
  };

  const openPayslipDialog = () => {
    setPayslipScope('all');
    setPayslipEmployeeSearch('');
    setPayslipDialogOpen(true);
  };

  const generateScopedPayslips = async () => {
    if (!selectedRun) {
      return;
    }

    const filters =
      payslipScope === 'employee'
        ? { employeeId: payslipEmployeeId }
        : payslipScope === 'category'
          ? {
              categoryType: payslipCategoryType,
              categoryValue: payslipCategoryValue,
            }
          : undefined;

    await runOperation('Payslips', async () => {
      const slips = await payrollService.getPayslips(selectedRun.id, filters);
      setPayslips(slips);
      setSelectedPayslipEmployeeIds(new Set<string>());
      setPayslipEmailResult(null);
      setPayslipDialogOpen(false);
      return slips;
    });
  };

  const postSelectedRun = async () => {
    if (!selectedRun) {
      return;
    }

    return runOperation('Post payroll', async () => {
      const posting = await payrollService.postJournal(
        selectedRun.id,
        'Posted from payroll desk'
      );
      setJournalPosting(posting);
      return posting;
    });
  };

  const loadPostingPreview = async (openDialog = true) => {
    if (!selectedRun) {
      return;
    }

    setBusy('Preview posting');
    setError(null);
    try {
      const preview = await payrollService.getJournalPreview(selectedRun.id);
      setJournalPreview(preview);
      if (openDialog) {
        setPostingPreviewOpen(true);
      }
    } catch (err) {
      const message =
        err instanceof Error
          ? err.message
          : 'Unable to load payroll posting preview.';
      setError(message);
      toast({
        title: 'Payroll posting preview',
        description: message,
        variant: 'destructive',
      });
    } finally {
      setBusy(null);
    }
  };

  const payslipEmployeeIds = useMemo(
    () => payslips.map((slip) => slip.employeeId),
    [payslips]
  );
  const selectedPayslipCount = selectedPayslipEmployeeIds.size;
  const allPayslipsSelected =
    payslips.length > 0 && selectedPayslipCount === payslips.length;

  const togglePayslipSelection = (employeeId: string, checked: boolean) => {
    setSelectedPayslipEmployeeIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(employeeId);
      } else {
        next.delete(employeeId);
      }
      return next;
    });
  };

  useEffect(() => {
    setSelectedPayslipEmployeeIds((current) => {
      const available = new Set(payslipEmployeeIds);
      const next = new Set(
        [...current].filter((employeeId) => available.has(employeeId))
      );
      return next.size === current.size ? current : next;
    });
  }, [payslipEmployeeIds]);

  const selectedCurrency =
    selectedRun?.currencyCode || runForm.currencyCode || 'GHS';
  const currentPayPeriod =
    activeParameters?.currentPayPeriod || runForm.payPeriod;
  const openRunForPeriod = runs.find(
    (run) =>
      run.payPeriod === runForm.payPeriod &&
      run.status !== 'Closed' &&
      run.status !== 'RolledBack'
  );
  const canCreateRun = !openRunForPeriod && busy !== 'Create run';
  const canProcessRun = Boolean(
    selectedRun && selectedRun.status !== 'Closed' && busy !== 'Process'
  );
  const canGenerateOutputForSelectedRun = Boolean(
    selectedRun &&
      selectedRun.status !== 'Draft' &&
      selectedRun.status !== 'InReview' &&
      selectedRun.status !== 'RolledBack'
  );
  const currentJournalPreview =
    journalPreview && journalPreview.payrollRunId === selectedRun?.id
      ? journalPreview
      : null;
  const selectedJournalLines =
    currentJournalPreview?.lines ?? selectedRun?.journalLines ?? [];
  const selectedJournalLineCount = selectedJournalLines.length;
  const selectedJournalDebit = roundCurrency(
    selectedJournalLines
      .filter((line) => line.debitCredit === 'DR')
      .reduce((total, line) => total + Math.abs(line.amount), 0)
  );
  const selectedJournalCredit = roundCurrency(
    selectedJournalLines
      .filter((line) => line.debitCredit === 'CR')
      .reduce((total, line) => total + Math.abs(line.amount), 0)
  );
  const selectedJournalDifference = roundCurrency(
    Math.abs(selectedJournalDebit - selectedJournalCredit)
  );
  const selectedJournalHasLines = selectedJournalLineCount > 0;
  const selectedJournalIsBalanced =
    selectedJournalHasLines && selectedJournalDifference === 0;
  const selectedJournalBalanceLabel = !selectedJournalHasLines
    ? 'No lines'
    : selectedJournalIsBalanced
      ? 'Balanced'
      : 'Out of balance';
  const selectedJournalBalanceDetail = !selectedJournalHasLines
    ? 'Load or generate the posting preview'
    : selectedJournalIsBalanced
      ? 'Debit and credit totals match'
      : `${money(selectedJournalDifference, selectedCurrency)} difference`;
  const selectedJournalBalanceClassName = !selectedJournalHasLines
    ? 'border-slate-300 bg-slate-50 text-slate-800'
    : selectedJournalIsBalanced
      ? 'border-emerald-300 bg-emerald-50 text-emerald-950'
      : 'border-red-300 bg-red-50 text-red-950';
  const selectedJournalPosted =
    currentJournalPreview?.alreadyPosted ??
    selectedJournalLines.some(
      (line) => line.posted || Boolean(line.journalEntryId)
    );
  const selectedJournalUnmappedCount = selectedJournalLines.filter((line) => {
    const accountCode = line.accountCode?.trim().toUpperCase();
    return !accountCode || accountCode === 'UNMAPPED';
  }).length;
  const selectedJournalInvalidAmountCount = selectedJournalLines.filter(
    (line) =>
      (line.debitCredit !== 'DR' && line.debitCredit !== 'CR') ||
      Math.abs(line.amount) <= 0
  ).length;
  const expectedJournalNumber = selectedRun
    ? currentJournalPreview?.journalNumber || `PAY-${selectedRun.runNumber}`
    : '';
  const canPreviewPosting = Boolean(
    selectedRun &&
      selectedRun.employeeCount > 0 &&
      selectedRun.status !== 'Draft' &&
      selectedRun.status !== 'RolledBack' &&
      busy !== 'Preview posting'
  );
  const postPayrollBlocker = (() => {
    if (currentJournalPreview?.blocker) {
      return currentJournalPreview.blocker;
    }

    if (!selectedRun) {
      return 'Select a payroll run before posting.';
    }

    if (busy) {
      return busy === 'Post payroll'
        ? 'Payroll posting is already in progress.'
        : `Finish ${busy.toLowerCase()} before posting payroll.`;
    }

    if (selectedRun.employeeCount <= 0 || selectedRun.status === 'Draft') {
      return 'Calculate the payroll run before posting.';
    }

    if (selectedRun.status === 'InReview') {
      return selectedPendingApprovers.short
        ? `Payroll workflow approval is required before posting. Pending with ${selectedPendingApprovers.short}.`
        : 'Payroll workflow approval is required before posting.';
    }

    if (selectedRun.status === 'RolledBack') {
      return 'Rolled back payroll runs must be recalculated before posting.';
    }

    if (selectedRun.status === 'Closed') {
      return selectedJournalPosted
        ? `Payroll journal ${expectedJournalNumber} has already been posted.`
        : 'This payroll run is already closed.';
    }

    if (!canGenerateOutputForSelectedRun) {
      return 'Payroll output is not ready for posting.';
    }

    if (selectedJournalPosted) {
      return `Payroll journal ${expectedJournalNumber} has already been posted.`;
    }

    if (selectedJournalLineCount > 0 && selectedJournalUnmappedCount > 0) {
      return `${selectedJournalUnmappedCount} payroll journal line(s) still have unmapped GL accounts. Complete Payroll Journal Mapping first.`;
    }

    if (selectedJournalLineCount > 0 && selectedJournalInvalidAmountCount > 0) {
      return `${selectedJournalInvalidAmountCount} payroll journal line(s) have zero or invalid amounts. Recalculate the run before posting.`;
    }

    if (selectedJournalLineCount > 0 && !selectedJournalIsBalanced) {
      return `Payroll journal is not balanced. Debit ${money(selectedJournalDebit, selectedCurrency)}, Credit ${money(selectedJournalCredit, selectedCurrency)}.`;
    }

    return null;
  })();
  const canPostPayroll = !postPayrollBlocker;
  const postingJournalLabel =
    journalPosting?.journalNumber ||
    (selectedJournalPosted
      ? expectedJournalNumber
      : selectedJournalLineCount > 0
        ? `${selectedJournalLineCount} lines ready`
        : 'Built on post');
  const canGeneratePayslips = Boolean(
    canGenerateOutputForSelectedRun &&
      (payslipScope === 'all' ||
        (payslipScope === 'category' && payslipCategoryValue) ||
        (payslipScope === 'employee' && payslipEmployeeId))
  );
  const payslipScopeCount =
    payslipScope === 'employee'
      ? payslipEmployeeId
        ? 1
        : 0
      : payslipScope === 'category'
        ? activeProfiles.filter(
            (profile) =>
              profileCategoryValue(profile, payslipCategoryType) ===
              payslipCategoryValue
          ).length
        : activeProfiles.length;
  const currentPayrollPeriodLabel = activeParameters
    ? formatPayrollPeriodLabel(
        activeParameters.currentPeriodFrom,
        activeParameters.currentPeriodTo
      )
    : 'Not configured';

  const renderPayrollRunWorkflowActions = (
    run: PayrollRun,
    className?: string,
    showStepBadge = run.status === 'InReview'
  ) => {
    const summary = workflowSummariesById[run.id];

    return (
      <WorkflowApprovalActions
        entityType="PayrollRun"
        entityId={run.id}
        entityLabel="Payroll Run"
        entityNumber={run.runNumber}
        status={run.status}
        showStepBadge={showStepBadge}
        currentStepName={summary?.currentStepName}
        workflowSummary={summary}
        canSubmit={run.status === 'Calculated'}
        canApproveReject={run.status === 'InReview'}
        onSubmit={async () => {
          await payrollService.submitRun(
            run.id,
            'Submitted from payroll run desk'
          );
        }}
        onApprove={async (comments) => {
          await payrollService.approveRun(run.id, comments || undefined);
        }}
        onReject={async (comments) => {
          await payrollService.rejectRun(run.id, comments || undefined);
        }}
        onAfterAction={loadWorkspace}
        onOpenWorkflows={() => router.push('/administration/workflow')}
        className={className}
      />
    );
  };

  return (
    <main className="space-y-5 p-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-normal">Payroll</h1>
            <Badge variant="outline">Human Resources</Badge>
          </div>
          <p className="max-w-3xl text-sm text-muted-foreground">
            Run payroll from the migrated Oracle Forms payroll model while using
            ERP HR employees as the staff source.
          </p>
        </div>
        <div className="flex flex-col gap-2 lg:items-end">
          <div className="grid min-w-[280px] gap-3 rounded-md border bg-muted/40 p-3 text-sm sm:grid-cols-[1fr_auto] lg:min-w-[420px]">
            <div>
              <div className="text-xs text-muted-foreground">
                Payroll Period
              </div>
              <div className="font-semibold">{currentPayrollPeriodLabel}</div>
            </div>
            <div className="sm:text-right">
              <div className="text-xs text-muted-foreground">Period No</div>
              <div className="font-semibold">
                {activeParameters ? currentPayPeriod : '-'}
              </div>
            </div>
          </div>
          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => void loadWorkspace()}
              disabled={loading}
            >
              <RefreshCw className="mr-2 h-4 w-4" />
              Refresh
            </Button>
            <Button asChild variant="outline">
              <Link href="/hr/payroll/employee-profiles">
                <Users className="mr-2 h-4 w-4" />
                Employee Profiles
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link href="/hr/payroll/bonus-exceptions">
                <Award className="mr-2 h-4 w-4" />
                Bonus Exceptions
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link href="/administration/hr/payroll">
                <Settings className="mr-2 h-4 w-4" />
                Payroll Setup
              </Link>
            </Button>
          </div>
        </div>
      </div>

      {error && (
        <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </div>
      )}

      <section className="grid gap-3 md:grid-cols-4">
        <div className="rounded-md border p-3">
          <div className="text-sm text-muted-foreground">Current Run</div>
          <div className="mt-1 truncate font-medium">
            {selectedRun?.runNumber ||
              selectedRunOption?.runNumber ||
              'Not started'}
          </div>
        </div>
        <div className="rounded-md border p-3">
          <div className="text-sm text-muted-foreground">Status</div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            {selectedRun ? (
              <RunStatusBadge status={selectedRun.status} />
            ) : (
              <Badge variant="outline">Pending</Badge>
            )}
            {showSelectedWorkflowBadges &&
            selectedWorkflowSummary?.currentStepName ? (
              <Badge variant="outline" className="text-xs">
                Step: {selectedWorkflowSummary.currentStepName}
              </Badge>
            ) : null}
            {showSelectedWorkflowBadges && selectedPendingApprovers.short ? (
              <Badge
                variant="outline"
                className="text-xs"
                title={selectedPendingApprovers.full}
              >
                Pending with: {selectedPendingApprovers.short}
              </Badge>
            ) : null}
          </div>
          {selectedRun
            ? renderPayrollRunWorkflowActions(selectedRun, 'mt-2')
            : null}
        </div>
        <div className="rounded-md border p-3">
          <div className="text-sm text-muted-foreground">Employees</div>
          <div className="mt-1 font-medium">
            {selectedRun?.employeeCount ?? profiles.length}
          </div>
        </div>
        <div className="rounded-md border p-3">
          <div className="text-sm text-muted-foreground">Net Pay</div>
          <div className="mt-1 font-medium">
            {money(selectedRun?.netAmount, selectedCurrency)}
          </div>
        </div>
      </section>

      <Tabs value={activeTab} onValueChange={selectTab} className="space-y-4">
        <TabsList className="flex h-auto flex-wrap justify-start">
          <TabsTrigger value="runs">Run Desk</TabsTrigger>
          <TabsTrigger value="reports">Payslips & Journals</TabsTrigger>
          <WorkflowTabTrigger value="workflow" entityType="PayrollRun" entityId={selectedRun?.id} workflowSummary={selectedWorkflowSummary} />
        </TabsList>

        <TabsContent value="runs">
          <div className="grid gap-4 xl:grid-cols-[minmax(0,420px)_1fr]">
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Play className="h-4 w-4" />
                  Create Run
                </CardTitle>
              </CardHeader>
              <CardContent>
                <form
                  className="grid gap-3"
                  onSubmit={(event: FormEvent) => {
                    event.preventDefault();
                    void runOperation('Create run', async () => {
                      const run = await payrollService.createRun(runForm);
                      setSelectedRunId(run.id);
                      setSelectedRun(run);
                      return run;
                    });
                  }}
                >
                  <div className="grid gap-3 md:grid-cols-[180px_1fr]">
                    <Field label="Separate Bonus Run">
                      <label className="flex h-9 items-center gap-2 rounded-md border px-3 text-sm">
                        <Checkbox
                          checked={runForm.isSeparateBonusRun}
                          onCheckedChange={(checked) =>
                            setRunForm((current) => ({
                              ...current,
                              isSeparateBonusRun: checked === true,
                              separateBonusCode:
                                checked === true
                                  ? current.separateBonusCode
                                  : '',
                            }))
                          }
                        />
                        Yes
                      </label>
                    </Field>
                    <Field label="Separate Bonus Code">
                      <Input
                        value={runForm.separateBonusCode}
                        onChange={(event) =>
                          setRunForm((current) => ({
                            ...current,
                            separateBonusCode: event.target.value,
                          }))
                        }
                        disabled={!runForm.isSeparateBonusRun}
                        placeholder="Optional: run all due separate bonuses"
                      />
                    </Field>
                  </div>
                  <Field label="Notes">
                    <Input
                      value={runForm.notes}
                      onChange={(event) =>
                        setRunForm((current) => ({
                          ...current,
                          notes: event.target.value,
                        }))
                      }
                    />
                  </Field>
                  <Button type="submit" disabled={!canCreateRun}>
                    {busy === 'Create run' ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Save className="mr-2 h-4 w-4" />
                    )}
                    Create Run
                  </Button>
                </form>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-3">
                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <CardTitle className="text-base">Payroll Runs</CardTitle>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <PayrollGridExportButton
                      rows={runs}
                      fileName="payroll-runs"
                      columns={[
                        { header: 'Run', value: (row) => row.runNumber },
                        { header: 'Status', value: (row) => row.status },
                        {
                          header: 'Employees',
                          value: (row) => row.employeeCount,
                        },
                        { header: 'Gross', value: (row) => row.grossAmount },
                        { header: 'Net', value: (row) => row.netAmount },
                      ]}
                    />
                    <select
                      className="h-9 rounded-md border bg-background px-3 text-sm"
                      value={selectedRunId}
                      onChange={(event) => setSelectedRunId(event.target.value)}
                    >
                      <option value="">Select run</option>
                      {runs.map((run) => (
                        <option key={run.id} value={run.id}>
                          {run.runNumber} - {run.status}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    className="bg-emerald-600 text-white hover:bg-emerald-700 focus-visible:ring-emerald-500/30"
                    disabled={!canProcessRun}
                    onClick={() =>
                      selectedRun &&
                      void runOperation('Process', () =>
                        payrollService.calculateRun(selectedRun.id)
                      )
                    }
                  >
                    <CalculatorIcon />
                    Process
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={!canPreviewPosting}
                    title={
                      canPreviewPosting
                        ? undefined
                        : 'Calculate the payroll run before previewing posting.'
                    }
                    onClick={() => void loadPostingPreview(true)}
                  >
                    {busy === 'Preview posting' ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <FileText className="mr-2 h-4 w-4" />
                    )}
                    Preview Posting
                  </Button>
                </div>

                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Run</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Employees</TableHead>
                      <TableHead className="text-right">Gross</TableHead>
                      <TableHead className="text-right">Net</TableHead>
                      <TableHead className="text-right">Workflow</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {runs.map((run) => {
                      const summary = workflowSummariesById[run.id];
                      const pending = formatPendingApprovers(
                        summary?.pendingApprovers || []
                      );
                      const showWorkflowBadges = run.status === 'InReview';

                      return (
                        <TableRow
                          key={run.id}
                          className="cursor-pointer"
                          onClick={() => setSelectedRunId(run.id)}
                        >
                          <TableCell className="font-medium">
                            <div>{run.runNumber}</div>
                            {showWorkflowBadges && summary?.currentStepName ? (
                              <div className="mt-1 text-xs text-muted-foreground">
                                Step: {summary.currentStepName}
                              </div>
                            ) : null}
                            {showWorkflowBadges && pending.short ? (
                              <div
                                className="mt-0.5 text-xs text-muted-foreground"
                                title={pending.full}
                              >
                                Pending with: {pending.short}
                              </div>
                            ) : null}
                          </TableCell>
                          <TableCell>
                            <RunStatusBadge status={run.status} />
                          </TableCell>
                          <TableCell className="text-right">
                            {run.employeeCount}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(run.grossAmount, run.currencyCode)}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(run.netAmount, run.currencyCode)}
                          </TableCell>
                          <TableCell
                            className="text-right"
                            onClick={(event) => event.stopPropagation()}
                          >
                            <div className="flex flex-col items-end gap-1">
                              {showWorkflowBadges && pending.short ? (
                                <div
                                  className="max-w-[220px] truncate text-xs text-muted-foreground"
                                  title={pending.full}
                                >
                                  Pending: {pending.short}
                                </div>
                              ) : null}
                              <div className="flex flex-wrap justify-end gap-1">
                                {renderPayrollRunWorkflowActions(run, undefined, false)}
                                <Button
                                  type="button"
                                  variant="ghost"
                                  size="sm"
                                  className="h-7 px-2 text-xs"
                                  onClick={() => {
                                    setSelectedRunId(run.id);
                                    selectTab('workflow');
                                  }}
                                >
                                  <FileText className="mr-1 h-3.5 w-3.5" />
                                  History
                                </Button>
                              </div>
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="reports">
          <div className="space-y-4">
            <Card>
              <CardHeader className="pb-3">
                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <CardTitle className="flex items-center gap-2 text-base">
                      <BarChart3 className="h-4 w-4" />
                      Payslips & Journals
                    </CardTitle>
                    <CardDescription>
                      Review payroll output and post the final run.
                    </CardDescription>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={!selectedRun || busy === 'Summary'}
                      onClick={() =>
                        selectedRun &&
                        void runOperation('Summary', async () => {
                          const report = await payrollService.getRunSummary(
                            selectedRun.id,
                            false
                          );
                          setSummary(report);
                          return report;
                        })
                      }
                    >
                      <FileText className="mr-2 h-4 w-4" />
                      Summary
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={
                        !canGenerateOutputForSelectedRun || busy === 'Payslips'
                      }
                      onClick={openPayslipDialog}
                    >
                      <CreditCard className="mr-2 h-4 w-4" />
                      {selectedRun?.isSeparateBonusRun
                        ? 'Bonus Slips'
                        : 'Payslips'}
                    </Button>
                    <Button
                      size="sm"
                      variant="warning"
                      disabled={!canPostPayroll}
                      title={postPayrollBlocker ?? undefined}
                      onClick={() => setPostDialogOpen(true)}
                    >
                      <CheckCircle2 className="mr-2 h-4 w-4" />
                      Post Payroll
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="grid gap-3 md:grid-cols-5">
                <div className="rounded-md border p-3">
                  <div className="text-xs text-muted-foreground">Gross</div>
                  <div className="font-semibold">
                    {money(
                      summary?.grossAmount ?? selectedRun?.grossAmount,
                      selectedCurrency
                    )}
                  </div>
                </div>
                <div className="rounded-md border p-3">
                  <div className="text-xs text-muted-foreground">Tax</div>
                  <div className="font-semibold">
                    {money(
                      summary?.taxAmount ?? selectedRun?.taxAmount,
                      selectedCurrency
                    )}
                  </div>
                </div>
                <div className="rounded-md border p-3">
                  <div className="text-xs text-muted-foreground">
                    Employee SSF
                  </div>
                  <div className="font-semibold">
                    {money(
                      summary?.employeeContributionAmount ??
                        selectedRun?.employeeContributionAmount,
                      selectedCurrency
                    )}
                  </div>
                </div>
                <div className="rounded-md border p-3">
                  <div className="text-xs text-muted-foreground">Net</div>
                  <div className="font-semibold">
                    {money(
                      summary?.netAmount ?? selectedRun?.netAmount,
                      selectedCurrency
                    )}
                  </div>
                </div>
                <div className="rounded-md border p-3">
                  <div className="text-xs text-muted-foreground">Journal</div>
                  <div className="truncate font-semibold">
                    {postingJournalLabel}
                  </div>
                  {selectedJournalLineCount > 0 ? (
                    <div className="mt-1 grid grid-cols-2 gap-2 text-xs text-muted-foreground">
                      <div>
                        <span className="font-medium text-foreground">DR</span>{' '}
                        {money(selectedJournalDebit, selectedCurrency)}
                      </div>
                      <div>
                        <span className="font-medium text-foreground">CR</span>{' '}
                        {money(selectedJournalCredit, selectedCurrency)}
                      </div>
                    </div>
                  ) : (
                    <div className="text-xs text-muted-foreground">
                      Generated from payroll output
                    </div>
                  )}
                </div>
                {selectedRun && (
                  <div
                    className={`rounded-md border p-3 text-sm md:col-span-5 ${
                      postPayrollBlocker
                        ? 'border-amber-300 bg-amber-50 text-amber-950'
                        : 'border-emerald-300 bg-emerald-50 text-emerald-950'
                    }`}
                  >
                    {postPayrollBlocker ??
                      (selectedJournalLineCount > 0
                        ? `Payroll journal ${expectedJournalNumber} is balanced and ready to post.`
                        : `Payroll journal ${expectedJournalNumber} will be built and posted from the saved payroll output.`)}
                  </div>
                )}
              </CardContent>
            </Card>

            <Card className="hidden">
              <CardHeader className="pb-3">
                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <CardTitle className="flex items-center gap-2 text-base">
                      <FileText className="h-4 w-4" />
                      Payroll Posting Report
                    </CardTitle>
                    <CardDescription>
                      Preview the exact GL journal lines before the payroll run
                      is posted.
                    </CardDescription>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={!canPreviewPosting}
                      onClick={() => void loadPostingPreview(false)}
                    >
                      {busy === 'Preview posting' ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <RefreshCw className="mr-2 h-4 w-4" />
                      )}
                      Load Report
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={
                        !canPreviewPosting && selectedJournalLineCount === 0
                      }
                      onClick={() =>
                        currentJournalPreview
                          ? setPostingPreviewOpen(true)
                          : void loadPostingPreview(true)
                      }
                    >
                      <FileText className="mr-2 h-4 w-4" />
                      Open Preview
                    </Button>
                    <PayrollGridExportButton
                      rows={selectedJournalLines}
                      fileName={`payroll-posting-${selectedRun?.runNumber || 'run'}`}
                      columns={[
                        { header: 'Seq', value: (row) => row.sequenceNo },
                        {
                          header: 'Transaction Type',
                          value: (row) => row.transactionType,
                        },
                        {
                          header: 'Account Code',
                          value: (row) => row.accountCode,
                        },
                        {
                          header: 'Description',
                          value: (row) => row.description,
                        },
                        {
                          header: 'Debit',
                          value: (row) => journalDebitAmount(row),
                        },
                        {
                          header: 'Credit',
                          value: (row) => journalCreditAmount(row),
                        },
                        {
                          header: 'Posted',
                          value: (row) => (row.posted ? 'Yes' : 'No'),
                        },
                      ]}
                      disabled={selectedJournalLineCount === 0}
                    />
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Journal</div>
                    <div className="truncate font-semibold">
                      {expectedJournalNumber || '-'}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Lines</div>
                    <div className="font-semibold">
                      {selectedJournalLineCount}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Debit</div>
                    <div className="font-semibold">
                      {money(selectedJournalDebit, selectedCurrency)}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Credit</div>
                    <div className="font-semibold">
                      {money(selectedJournalCredit, selectedCurrency)}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">
                      Difference
                    </div>
                    <div className="font-semibold">
                      {money(selectedJournalDifference, selectedCurrency)}
                    </div>
                  </div>
                  <div
                    className={`rounded-md border p-3 ${selectedJournalBalanceClassName}`}
                  >
                    <div className="text-xs opacity-80">Balance Status</div>
                    <div className="font-semibold">
                      {selectedJournalBalanceLabel}
                    </div>
                    <div className="text-xs opacity-80">
                      {selectedJournalBalanceDetail}
                    </div>
                  </div>
                </div>
                {currentJournalPreview?.generatedForPreview && (
                  <div className="rounded-md border border-sky-300 bg-sky-50 px-3 py-2 text-sm text-sky-950">
                    Journal lines were rebuilt from the current payroll output
                    and journal mappings for this preview.
                  </div>
                )}
                {postPayrollBlocker ? (
                  <div className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-950">
                    {postPayrollBlocker}
                  </div>
                ) : (
                  <div className="rounded-md border border-emerald-300 bg-emerald-50 px-3 py-2 text-sm text-emerald-950">
                    Payroll posting is balanced and ready for final posting.
                  </div>
                )}
                <div className="overflow-x-auto rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-[70px]">Seq</TableHead>
                        <TableHead>Transaction</TableHead>
                        <TableHead>Account</TableHead>
                        <TableHead>Description</TableHead>
                        <TableHead className="text-right">Debit</TableHead>
                        <TableHead className="text-right">Credit</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selectedJournalLines.length === 0 ? (
                        <TableRow>
                          <TableCell
                            colSpan={6}
                            className="py-6 text-center text-sm text-muted-foreground"
                          >
                            Load the posting report after calculating a payroll
                            run.
                          </TableCell>
                        </TableRow>
                      ) : (
                        selectedJournalLines.map((line) => (
                          <TableRow key={line.id || line.sequenceNo}>
                            <TableCell>{line.sequenceNo}</TableCell>
                            <TableCell>{line.transactionType}</TableCell>
                            <TableCell className="font-medium">
                              {line.accountCode || 'UNMAPPED'}
                            </TableCell>
                            <TableCell>{line.description}</TableCell>
                            <TableCell className="text-right">
                              {line.debitCredit === 'DR'
                                ? money(line.amount, selectedCurrency)
                                : '-'}
                            </TableCell>
                            <TableCell className="text-right">
                              {line.debitCredit === 'CR'
                                ? money(line.amount, selectedCurrency)
                                : '-'}
                            </TableCell>
                          </TableRow>
                        ))
                      )}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>

            <div className="grid gap-4 xl:grid-cols-2">
              <Card>
                <CardHeader className="pb-3">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <CardTitle className="text-base">
                      Summary Components
                    </CardTitle>
                    <PayrollGridExportButton
                      rows={summary?.components ?? []}
                      fileName={`payroll-summary-components-${selectedRun?.runNumber || 'run'}`}
                      columns={[
                        { header: 'Type', value: (row) => row.transactionType },
                        {
                          header: 'Component Code',
                          value: (row) => row.componentCode,
                        },
                        {
                          header: 'Description',
                          value: (row) => row.description,
                        },
                        { header: 'Amount', value: (row) => row.amount },
                      ]}
                      disabled={!summary}
                    />
                  </div>
                </CardHeader>
                <CardContent>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Type</TableHead>
                        <TableHead>Description</TableHead>
                        <TableHead className="text-right">Amount</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {(summary?.components ?? []).slice(0, 12).map((line) => (
                        <TableRow
                          key={`${line.transactionType}-${line.componentCode ?? ''}`}
                        >
                          <TableCell>{line.transactionType}</TableCell>
                          <TableCell>{line.description}</TableCell>
                          <TableCell className="text-right">
                            {money(line.amount, selectedCurrency)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </CardContent>
              </Card>
              <Card>
                <CardHeader className="pb-3">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <CardTitle className="text-base">
                      {selectedRun?.isSeparateBonusRun
                        ? 'Bonus Slips'
                        : 'Payslips'}
                    </CardTitle>
                    <div className="flex flex-wrap gap-2">
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        disabled={
                          !canGenerateOutputForSelectedRun ||
                          payslips.length === 0 ||
                          busy === 'Email payslips'
                        }
                        onClick={() =>
                          selectedRun &&
                          void runOperation('Email payslips', async () => {
                            const result = await payrollService.emailPayslips(
                              selectedRun.id,
                              {
                                employeeIds: payslips.map(
                                  (slip) => slip.employeeId
                                ),
                              }
                            );
                            setPayslipEmailResult(result);
                            return result;
                          })
                        }
                      >
                        {busy === 'Email payslips' ? (
                          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                          <Mail className="mr-2 h-4 w-4" />
                        )}
                        Email All
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        disabled={
                          !canGenerateOutputForSelectedRun ||
                          selectedPayslipCount === 0 ||
                          busy === 'Email selected payslips'
                        }
                        onClick={() =>
                          selectedRun &&
                          void runOperation(
                            'Email selected payslips',
                            async () => {
                              const result = await payrollService.emailPayslips(
                                selectedRun.id,
                                { employeeIds: [...selectedPayslipEmployeeIds] }
                              );
                              setPayslipEmailResult(result);
                              return result;
                            }
                          )
                        }
                      >
                        {busy === 'Email selected payslips' ? (
                          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                          <Mail className="mr-2 h-4 w-4" />
                        )}
                        Email Selected
                      </Button>
                      <PayrollGridExportButton
                        rows={payslips}
                        fileName={`payroll-payslips-${selectedRun?.runNumber || 'run'}`}
                        columns={[
                          {
                            header: 'Employee No',
                            value: (row) => row.employeeNumber,
                          },
                          {
                            header: 'Employee Name',
                            value: (row) => row.employeeName,
                          },
                          {
                            header: 'Email',
                            value: (row) => row.employeeEmail || '',
                          },
                          { header: 'Gross', value: (row) => row.grossIncome },
                          { header: 'Tax', value: (row) => row.incomeTax },
                          { header: 'Net', value: (row) => row.netIncome },
                          {
                            header: 'Currency',
                            value: (row) => row.currencyCode,
                          },
                        ]}
                      />
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="space-y-3">
                  {payslipEmailResult && (
                    <div className="rounded-md border bg-muted/40 px-3 py-2 text-sm">
                      <span className="font-medium">
                        {payslipEmailResult.sentCount}
                      </span>{' '}
                      queued,{' '}
                      <span className="font-medium">
                        {payslipEmailResult.failedCount}
                      </span>{' '}
                      failed
                    </div>
                  )}
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-[40px]">
                          <Checkbox
                            checked={allPayslipsSelected}
                            onCheckedChange={(value) =>
                              setSelectedPayslipEmployeeIds(
                                value === true
                                  ? new Set(payslipEmployeeIds)
                                  : new Set<string>()
                              )
                            }
                          />
                        </TableHead>
                        <TableHead>Employee</TableHead>
                        <TableHead className="text-right">Gross</TableHead>
                        <TableHead className="text-right">Tax</TableHead>
                        <TableHead className="text-right">Net</TableHead>
                        <TableHead className="w-[220px] text-right">
                          Actions
                        </TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {payslips.map((slip) => (
                        <TableRow key={slip.payrollRunEmployeeId}>
                          <TableCell>
                            <Checkbox
                              checked={selectedPayslipEmployeeIds.has(
                                slip.employeeId
                              )}
                              onCheckedChange={(value) =>
                                togglePayslipSelection(
                                  slip.employeeId,
                                  value === true
                                )
                              }
                            />
                          </TableCell>
                          <TableCell>
                            <div className="font-medium">
                              {slip.employeeNumber}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {slip.employeeName}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {slip.employeeEmail || 'No email'}
                            </div>
                          </TableCell>
                          <TableCell className="text-right">
                            {money(slip.grossIncome, slip.currencyCode)}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(slip.incomeTax, slip.currencyCode)}
                          </TableCell>
                          <TableCell className="text-right">
                            {money(slip.netIncome, slip.currencyCode)}
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex flex-wrap justify-end gap-2">
                              <Button
                                type="button"
                                size="sm"
                                variant="outline"
                                onClick={() => setPreviewPayslip(slip)}
                              >
                                <Printer className="mr-2 h-4 w-4" />
                                {slip.isSeparateBonusRun
                                  ? 'Print Bonus Slip'
                                  : 'Print'}
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </CardContent>
              </Card>
            </div>
          </div>
        </TabsContent>

        {selectedRun ? (
          <WorkflowTabContent
            value="workflow"
            entityType="PayrollRun"
            entityId={selectedRun.id}
            entityLabel="Payroll Run"
            entityNumber={selectedRun.runNumber}
            status={selectedRun.status}
            currentStepName={selectedWorkflowSummary?.currentStepName}
            workflowSummary={selectedWorkflowSummary}
            canSubmit={selectedRun.status === 'Calculated'}
            canApproveReject={selectedRun.status === 'InReview'}
            onSubmit={async () => {
              await payrollService.submitRun(
                selectedRun.id,
                'Submitted from payroll run workflow tab'
              );
            }}
            onApprove={async (comments) => {
              await payrollService.approveRun(selectedRun.id, comments || undefined);
            }}
            onReject={async (comments) => {
              await payrollService.rejectRun(selectedRun.id, comments || undefined);
            }}
            onAfterAction={loadWorkspace}
            onOpenWorkflows={() => router.push('/administration/workflow')}
          />
        ) : (
          <TabsContent value="workflow">
            <Card>
              <CardContent className="py-8 text-center text-sm text-muted-foreground">
                Select a payroll run to view its workflow.
              </CardContent>
            </Card>
          </TabsContent>
        )}
      </Tabs>

      {loading && (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading payroll workspace
        </div>
      )}

      <Dialog open={payslipDialogOpen} onOpenChange={setPayslipDialogOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {selectedRun?.isSeparateBonusRun
                ? 'Generate Bonus Slips'
                : 'Generate Payslips'}
            </DialogTitle>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={(event) => {
              event.preventDefault();
              void generateScopedPayslips();
            }}
          >
            <div className="grid gap-3 md:grid-cols-[220px_1fr_120px]">
              <Field label="Scope">
                <select
                  className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                  value={payslipScope}
                  onChange={(event) =>
                    setPayslipScope(event.target.value as PayslipScope)
                  }
                >
                  <option value="all">All</option>
                  <option value="category">Employee Category</option>
                  <option value="employee">Single Employee</option>
                </select>
              </Field>
              {payslipScope === 'category' ? (
                <div className="grid gap-3 md:grid-cols-2">
                  <Field label="Category">
                    <select
                      className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                      value={payslipCategoryType}
                      onChange={(event) =>
                        setPayslipCategoryType(event.target.value)
                      }
                    >
                      {payslipCategoryTypes.map((category) => (
                        <option key={category.value} value={category.value}>
                          {category.label}
                        </option>
                      ))}
                    </select>
                  </Field>
                  <Field label="Value">
                    <select
                      className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                      value={payslipCategoryValue}
                      onChange={(event) =>
                        setPayslipCategoryValue(event.target.value)
                      }
                    >
                      {payslipCategoryValues.map((value) => (
                        <option key={value} value={value}>
                          {value}
                        </option>
                      ))}
                    </select>
                  </Field>
                </div>
              ) : payslipScope === 'employee' ? (
                <div className="grid gap-3 md:grid-cols-[220px_1fr]">
                  <Field label="Find Employee">
                    <Input
                      value={payslipEmployeeSearch}
                      onChange={(event) =>
                        setPayslipEmployeeSearch(event.target.value)
                      }
                      placeholder="Employee no or name"
                    />
                  </Field>
                  <Field label="Employee">
                    <select
                      className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                      value={payslipEmployeeId}
                      onChange={(event) =>
                        setPayslipEmployeeId(event.target.value)
                      }
                    >
                      {filteredPayslipEmployees.map((profile) => (
                        <option
                          key={profile.employeeId}
                          value={profile.employeeId}
                        >
                          {profile.employeeNumber} - {profile.employeeName}
                        </option>
                      ))}
                    </select>
                  </Field>
                </div>
              ) : (
                <div className="flex h-full min-h-9 items-end rounded-md border bg-muted/30 px-3 py-2 text-sm font-medium">
                  All payroll employees
                </div>
              )}
              <Field label="Selected">
                <div className="flex h-9 items-center rounded-md border bg-muted/30 px-3 text-sm font-medium">
                  {payslipScopeCount}
                </div>
              </Field>
            </div>
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setPayslipDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={!canGeneratePayslips || busy === 'Payslips'}
              >
                {busy === 'Payslips' ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <CreditCard className="mr-2 h-4 w-4" />
                )}
                Generate
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={postingPreviewOpen} onOpenChange={setPostingPreviewOpen}>
        <DialogContent className="left-0 top-0 h-[100dvh] w-screen max-w-none translate-x-0 translate-y-0 grid-rows-[auto_minmax(0,1fr)_auto] gap-0 overflow-hidden rounded-none border-0 p-0 sm:rounded-none">
          <DialogHeader className="border-b px-6 py-4 pr-14">
            <DialogTitle>Payroll Posting Preview</DialogTitle>
          </DialogHeader>
          <div className="min-h-0 space-y-4 overflow-y-auto p-6 text-sm">
            <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
              <div className="rounded-md border p-3">
                <div className="text-xs text-muted-foreground">Run</div>
                <div className="font-semibold">
                  {selectedRun?.runNumber || '-'}
                </div>
              </div>
              <div className="rounded-md border p-3">
                <div className="text-xs text-muted-foreground">Journal</div>
                <div className="truncate font-semibold">
                  {expectedJournalNumber || '-'}
                </div>
              </div>
              <div className="rounded-md border p-3">
                <div className="text-xs text-muted-foreground">Debit</div>
                <div className="font-semibold">
                  {money(selectedJournalDebit, selectedCurrency)}
                </div>
              </div>
              <div className="rounded-md border p-3">
                <div className="text-xs text-muted-foreground">Credit</div>
                <div className="font-semibold">
                  {money(selectedJournalCredit, selectedCurrency)}
                </div>
              </div>
              <div className="rounded-md border p-3">
                <div className="text-xs text-muted-foreground">Difference</div>
                <div className="font-semibold">
                  {money(selectedJournalDifference, selectedCurrency)}
                </div>
              </div>
              <div
                className={`rounded-md border p-3 ${selectedJournalBalanceClassName}`}
              >
                <div className="text-xs opacity-80">Balance Status</div>
                <div className="font-semibold">
                  {selectedJournalBalanceLabel}
                </div>
                <div className="text-xs opacity-80">
                  {selectedJournalBalanceDetail}
                </div>
              </div>
            </div>

            {postPayrollBlocker ? (
              <div className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-amber-950">
                {postPayrollBlocker}
              </div>
            ) : null}

            <div className="flex flex-wrap justify-end gap-2">
              <PayrollGridExportButton
                rows={selectedJournalLines}
                fileName={`payroll-posting-preview-${selectedRun?.runNumber || 'run'}`}
                label="Export Preview"
                columns={[
                  { header: 'Seq', value: (row) => row.sequenceNo },
                  {
                    header: 'Transaction Type',
                    value: (row) => row.transactionType,
                  },
                  {
                    header: 'Account Code',
                    value: (row) => row.accountCode,
                  },
                  {
                    header: 'Description',
                    value: (row) => row.description,
                  },
                  {
                    header: 'Debit',
                    value: (row) => journalDebitAmount(row),
                  },
                  {
                    header: 'Credit',
                    value: (row) => journalCreditAmount(row),
                  },
                ]}
              />
            </div>

            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[70px]">Seq</TableHead>
                    <TableHead>Transaction</TableHead>
                    <TableHead>Account</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead className="text-right">Debit</TableHead>
                    <TableHead className="text-right">Credit</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {selectedJournalLines.length === 0 ? (
                    <TableRow>
                      <TableCell
                        colSpan={6}
                        className="py-6 text-center text-muted-foreground"
                      >
                        No payroll posting lines are available.
                      </TableCell>
                    </TableRow>
                  ) : (
                    selectedJournalLines.map((line) => (
                      <TableRow key={line.id || line.sequenceNo}>
                        <TableCell>{line.sequenceNo}</TableCell>
                        <TableCell>{line.transactionType}</TableCell>
                        <TableCell className="font-medium">
                          {line.accountCode || 'UNMAPPED'}
                        </TableCell>
                        <TableCell>{line.description}</TableCell>
                        <TableCell className="text-right">
                          {line.debitCredit === 'DR'
                            ? money(line.amount, selectedCurrency)
                            : '-'}
                        </TableCell>
                        <TableCell className="text-right">
                          {line.debitCredit === 'CR'
                            ? money(line.amount, selectedCurrency)
                            : '-'}
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </div>
          <DialogFooter className="border-t px-6 py-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => setPostingPreviewOpen(false)}
            >
              Close
            </Button>
            <Button
              type="button"
              variant="warning"
              disabled={!canPostPayroll}
              title={postPayrollBlocker ?? undefined}
              onClick={() => {
                setPostingPreviewOpen(false);
                setPostDialogOpen(true);
              }}
            >
              Continue to Post
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={postDialogOpen}
        onOpenChange={setPostDialogOpen}
        title="Post Payroll?"
        description={
          <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-amber-950">
            Posting will finalize this payroll run and close the payroll period
            for this run.
          </div>
        }
        confirmText="Confirm Post"
        onConfirm={postSelectedRun}
        isLoading={busy === 'Post payroll'}
        confirmDisabled={!canPostPayroll}
        maxWidth="36rem"
      >
        <div className="space-y-3 text-sm">
          <div className="grid gap-2 rounded-md border p-3 sm:grid-cols-2">
            <div>
              <div className="text-xs text-muted-foreground">Run</div>
              <div className="font-medium">{selectedRun?.runNumber || '-'}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Employees</div>
              <div className="font-medium">
                {selectedRun?.employeeCount ?? 0}
              </div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Gross</div>
              <div className="font-medium">
                {money(selectedRun?.grossAmount, selectedCurrency)}
              </div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Net</div>
              <div className="font-medium">
                {money(selectedRun?.netAmount, selectedCurrency)}
              </div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Journal</div>
              <div className="font-medium">{expectedJournalNumber || '-'}</div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Lines</div>
              <div className="font-medium">
                {selectedJournalLineCount > 0
                  ? selectedJournalLineCount
                  : 'Built on post'}
              </div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Debit</div>
              <div className="font-medium">
                {selectedJournalLineCount > 0
                  ? money(selectedJournalDebit, selectedCurrency)
                  : '-'}
              </div>
            </div>
            <div>
              <div className="text-xs text-muted-foreground">Credit</div>
              <div className="font-medium">
                {selectedJournalLineCount > 0
                  ? money(selectedJournalCredit, selectedCurrency)
                  : '-'}
              </div>
            </div>
            <div className="sm:col-span-2">
              <div className="text-xs text-muted-foreground">
                Balance Status
              </div>
              <div
                className={`mt-1 inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${selectedJournalBalanceClassName}`}
              >
                {selectedJournalBalanceLabel}
              </div>
              <div className="mt-1 text-xs text-muted-foreground">
                {selectedJournalBalanceDetail}
              </div>
            </div>
          </div>
          {postPayrollBlocker && (
            <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-amber-950">
              {postPayrollBlocker}
            </div>
          )}
          <ul className="list-disc space-y-1 pl-5 text-muted-foreground">
            <li>A balanced finance journal will be created and posted.</li>
            <li>The payroll run status will change to Closed.</li>
            <li>Loan repayments in the run will be marked as paid.</li>
            <li>Payslip snapshots will be saved for the posted run.</li>
            <li>The active payroll period can advance after posting.</li>
          </ul>
        </div>
      </ConfirmationDialog>

      <PayrollPayslipPreviewDialog
        open={Boolean(previewPayslip)}
        payslip={previewPayslip}
        onOpenChange={(open) => {
          if (!open) {
            setPreviewPayslip(null);
          }
        }}
      />
    </main>
  );
}

function CalculatorIcon() {
  return <Play className="mr-2 h-4 w-4" />;
}
