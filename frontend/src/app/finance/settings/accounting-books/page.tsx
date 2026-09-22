'use client';

import React from 'react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
  AlertCircle,
  ArrowLeft,
  BookOpen,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  ShieldAlert,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type {
  AccountingBook,
  AccountingBookActivationReadiness,
  AccountingBookInitialization,
  AccountingBookLifecycleStatus,
  AccountingBookPeriod,
  AccountingBookType,
  SaveAccountingBook,
} from '@/types/finance';
import {
  getAccountingBookAccess,
  isIndependentChecker,
} from '@/components/finance/accounting-books/accounting-book-access';
import { InitializationEvidencePack } from '@/components/finance/accounting-books/initialization-evidence-pack';
import { SearchableOptionPicker } from '@/components/finance/accounting-books/searchable-option-picker';
import type { Currency } from '@/types/finance';

const bookTypes: AccountingBookType[] = ['ParallelFull', 'Delta'];
const bookTypeLabels: Record<AccountingBookType, string> = {
  PrimaryFull: 'Primary / base',
  ParallelFull: 'Parallel / foreign currency',
  Delta: 'Delta / adjustment',
};
const lifecycleStates: AccountingBookLifecycleStatus[] = [
  'Draft',
  'Configuring',
  'Initializing',
  'Active',
  'Suspended',
  'Retired',
];
const lifecycleTargets: Record<
  AccountingBookLifecycleStatus,
  AccountingBookLifecycleStatus[]
> = {
  Draft: ['Configuring', 'Retired'],
  Configuring: ['Initializing', 'Retired'],
  Initializing: ['Active'],
  Active: ['Suspended'],
  Suspended: ['Active', 'Retired'],
  Retired: [],
};

const blankForm = (): SaveAccountingBook => ({
  code: '',
  name: '',
  description: '',
  purpose: '',
  bookType: 'ParallelFull',
  functionalCurrencyCode: '',
  effectiveFromUtc: null,
  effectiveToUtc: null,
  baseAccountingBookId: null,
  replicationStartDate: '',
  parallelOpeningMode: 'ZeroOpening',
  parallelTranslationMethod: null,
  sortOrder: 100,
});

const statusOf = (book: AccountingBook): AccountingBookLifecycleStatus =>
  book.lifecycleStatus ?? (book.isActive ? 'Active' : 'Draft');

const typeOf = (book: AccountingBook): AccountingBookType =>
  book.bookType ?? (book.isDefault ? 'PrimaryFull' : 'ParallelFull');

const baseBookLabel = (book: AccountingBook) =>
  typeOf(book) !== 'PrimaryFull'
    ? book.baseAccountingBookCode || 'Not set'
    : 'Not applicable — tenant Primary';

const dateValue = (value?: string | null) => (value ? value.slice(0, 10) : '');
const utcDate = (value: string) => (value ? `${value}T00:00:00.000Z` : null);

type LifecycleEvidence = {
  loading: boolean;
  error: string | null;
  initialization: AccountingBookInitialization | null;
  readiness: AccountingBookActivationReadiness | null;
  periods: AccountingBookPeriod[];
};

const emptyLifecycleEvidence = (): LifecycleEvidence => ({
  loading: false,
  error: null,
  initialization: null,
  readiness: null,
  periods: [],
});

function LifecycleTransitionEvidence({
  book,
  target,
  evidence,
  retry,
}: {
  book: AccountingBook;
  target: AccountingBookLifecycleStatus;
  evidence: LifecycleEvidence;
  retry: () => void;
}) {
  const current = statusOf(book);
  const hasPreparationEvidence =
    Boolean(evidence.initialization) || evidence.periods.length > 0;
  return (
    <div className="space-y-4 rounded-md border p-4">
      <div>
        <p className="font-medium">Lifecycle decision evidence</p>
        <p className="text-sm text-muted-foreground">
          Review the governed configuration and readiness supporting this{' '}
          {current} → {target} request.
        </p>
      </div>
      <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <span className="text-muted-foreground">Book</span>
          <p className="font-medium">
            {book.code} — {book.name}
          </p>
        </div>
        <div>
          <span className="text-muted-foreground">Transition</span>
          <p>
            {current} → {target}
          </p>
        </div>
        <div>
          <span className="text-muted-foreground">Type</span>
          <p>{typeOf(book)}</p>
        </div>
        <div>
          <span className="text-muted-foreground">Purpose</span>
          <p>{book.purpose}</p>
        </div>
        <div>
          <span className="text-muted-foreground">Currency</span>
          <p>{book.functionalCurrencyCode || 'Inherited from base'}</p>
        </div>
        <div>
          <span className="text-muted-foreground">Base book</span>
          <p>{baseBookLabel(book)}</p>
        </div>
        <div>
          <span className="text-muted-foreground">Effective from</span>
          <p>{dateValue(book.effectiveFromUtc) || 'Not set'}</p>
        </div>
        <div>
          <span className="text-muted-foreground">Request submitted</span>
          <p>
            {book.transitionRequestedAtUtc
              ? new Date(book.transitionRequestedAtUtc).toLocaleString()
              : 'Not yet submitted'}
          </p>
        </div>
        <div className="sm:col-span-2 lg:col-span-4">
          <span className="text-muted-foreground">Transition reason</span>
          <p>
            {book.pendingTransitionReason ||
              'The maker will provide a reason when submitting this request.'}
          </p>
        </div>
      </div>

      {target === 'Initializing' &&
        (evidence.loading ? (
          <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading initialization and period evidence…
          </div>
        ) : evidence.error ? (
          <Alert variant="destructive">
            <AlertTitle>Initialization evidence unavailable</AlertTitle>
            <AlertDescription>
              {evidence.error}{' '}
              <Button
                type="button"
                variant="link"
                className="h-auto p-0"
                onClick={retry}
              >
                Retry
              </Button>
            </AlertDescription>
          </Alert>
        ) : (
          <div className="space-y-4">
            <Alert>
              <ShieldAlert className="h-4 w-4" />
              <AlertTitle>What this approval authorizes</AlertTitle>
              <AlertDescription>
                {hasPreparationEvidence
                  ? 'Configuration-stage initialization evidence already exists and is shown below. This decision authorizes the book to enter Initializing; it does not replace the independent decisions recorded on that evidence.'
                  : 'No initialization evidence has been prepared. This decision authorizes the book to enter Initializing, where its exact-book periods and opening evidence can be prepared and independently approved.'}
              </AlertDescription>
            </Alert>
            {hasPreparationEvidence && (
              <>
                <div className="rounded-md border p-3 text-sm">
                  <p className="font-medium">
                    Exact-book periods prepared during configuration
                  </p>
                  {evidence.periods.length === 0 ? (
                    <p className="mt-1 text-muted-foreground">
                      No exact-book periods are configured.
                    </p>
                  ) : (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {evidence.periods.map((period) => (
                        <Badge
                          key={period.id}
                          variant={
                            period.status === 'Open' ? 'default' : 'outline'
                          }
                        >
                          {period.fiscalPeriodCode}: {period.status}
                        </Badge>
                      ))}
                    </div>
                  )}
                </div>
                {evidence.initialization && (
                  <InitializationEvidencePack
                    initialization={evidence.initialization}
                  />
                )}
              </>
            )}
          </div>
        ))}

      {target === 'Active' &&
        (evidence.loading ? (
          <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading approved initialization and period evidence…
          </div>
        ) : evidence.error ? (
          <Alert variant="destructive">
            <AlertTitle>Activation evidence unavailable</AlertTitle>
            <AlertDescription>
              {evidence.error}{' '}
              <Button
                type="button"
                variant="link"
                className="h-auto p-0"
                onClick={retry}
              >
                Retry
              </Button>
            </AlertDescription>
          </Alert>
        ) : (
          <div className="space-y-4">
            <Alert
              className={
                evidence.readiness?.isReady
                  ? 'border-emerald-300 bg-emerald-50'
                  : 'border-amber-300 bg-amber-50'
              }
            >
              <ShieldAlert className="h-4 w-4" />
              <AlertTitle>
                {evidence.readiness?.isReady
                  ? 'Activation readiness confirmed'
                  : 'Activation is not ready'}
              </AlertTitle>
              <AlertDescription>
                {evidence.readiness?.isReady
                  ? `Approved initialization and ${evidence.readiness.readyPeriodCount} required open period(s) are current.`
                  : evidence.readiness?.blockers.join(' ') ||
                    'Readiness could not be confirmed.'}
              </AlertDescription>
            </Alert>
            <div className="rounded-md border p-3 text-sm">
              <p className="font-medium">Required exact-book periods</p>
              {evidence.periods.length === 0 ? (
                <p className="mt-1 text-muted-foreground">
                  No exact-book periods are configured.
                </p>
              ) : (
                <div className="mt-2 flex flex-wrap gap-2">
                  {evidence.periods.map((period) => (
                    <Badge
                      key={period.id}
                      variant={period.status === 'Open' ? 'default' : 'outline'}
                    >
                      {period.fiscalPeriodCode}: {period.status}
                    </Badge>
                  ))}
                </div>
              )}
            </div>
            {evidence.initialization ? (
              <InitializationEvidencePack
                initialization={evidence.initialization}
              />
            ) : (
              <Alert variant="destructive">
                <AlertTitle>Initialization evidence missing</AlertTitle>
                <AlertDescription>
                  No initialization record is available for this activation
                  decision.
                </AlertDescription>
              </Alert>
            )}
          </div>
        ))}
    </div>
  );
}

export default function AccountingBooksSettingsPage() {
  const {
    user,
    hasPermission,
    isLoading: authLoading,
    error: authError,
  } = useAuth();
  const access = getAccountingBookAccess(hasPermission);
  const { toast } = useToast();
  const [books, setBooks] = useState<AccountingBook[]>([]);
  const [currencies, setCurrencies] = useState<Currency[]>([]);
  const [currencyLoading, setCurrencyLoading] = useState(true);
  const [currencyError, setCurrencyError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<AccountingBook | null | undefined>(
    undefined
  );
  const [form, setForm] = useState<SaveAccountingBook>(blankForm());
  const [detail, setDetail] = useState<AccountingBook | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [transitioning, setTransitioning] = useState<AccountingBook | null>(
    null
  );
  const [targetStatus, setTargetStatus] =
    useState<AccountingBookLifecycleStatus>('Configuring');
  const [transitionReason, setTransitionReason] = useState('');
  const [decision, setDecision] = useState<{
    book: AccountingBook;
    action: 'approve' | 'reject';
  } | null>(null);
  const [decisionReason, setDecisionReason] = useState('');
  const [primaryReplacement, setPrimaryReplacement] =
    useState<AccountingBook | null>(null);
  const [primaryEffectiveDate, setPrimaryEffectiveDate] = useState(
    dateValue(new Date().toISOString())
  );
  const [primaryReason, setPrimaryReason] = useState('');
  const [primaryDecision, setPrimaryDecision] = useState<{
    book: AccountingBook;
    action: 'approve' | 'reject';
  } | null>(null);
  const [primaryDecisionReason, setPrimaryDecisionReason] = useState('');
  const [primaryReversal, setPrimaryReversal] =
    useState<AccountingBook | null>(null);
  const [primaryReversalReason, setPrimaryReversalReason] = useState('');
  const [primaryReversalDecision, setPrimaryReversalDecision] = useState<{
    book: AccountingBook;
    action: 'approve' | 'reject';
  } | null>(null);
  const [primaryReversalDecisionReason, setPrimaryReversalDecisionReason] =
    useState('');
  const [lifecycleEvidence, setLifecycleEvidence] = useState<LifecycleEvidence>(
    emptyLifecycleEvidence
  );
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!access.canRead) return;
    setLoading(true);
    setError(null);
    try {
      const result = await financeDataService.getAccountingBooks(true);
      setBooks(
        [...result].sort(
          (left, right) =>
            left.sortOrder - right.sortOrder ||
            left.code.localeCompare(right.code)
        )
      );
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : 'Accounting books could not be loaded.'
      );
    } finally {
      setLoading(false);
    }
  }, [access.canRead]);

  useEffect(() => {
    if (!authLoading && access.canRead) void load();
    if (!authLoading && !access.canRead) setLoading(false);
  }, [access.canRead, authLoading, load]);

  const loadCurrencies = useCallback(async () => {
    setCurrencyLoading(true);
    setCurrencyError(null);
    try {
      setCurrencies(await financeDataService.getCurrencies());
    } catch (reason) {
      setCurrencyError(
        reason instanceof Error
          ? reason.message
          : 'Currencies could not be loaded.'
      );
    } finally {
      setCurrencyLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!authLoading && access.canManage) void loadCurrencies();
  }, [access.canManage, authLoading, loadCurrencies]);

  const baseBookOptions = useMemo(
    () =>
      books.filter(
        (book) =>
          book.id !== editing?.id &&
          statusOf(book) !== 'Retired' &&
          (form.bookType === 'ParallelFull'
            ? typeOf(book) === 'PrimaryFull'
            : typeOf(book) !== 'Delta')
      ),
    [books, editing?.id, form.bookType]
  );
  const currencyOptions = useMemo(() => {
    const active = currencies
      .filter((currency) => currency.isActive)
      .map((currency) => ({
        value: currency.currencyCode,
        label: `${currency.currencyCode} — ${currency.currencyName}`,
      }));
    const existingCode = editing?.functionalCurrencyCode;
    if (
      existingCode &&
      !active.some((option) => option.value === existingCode)
    ) {
      active.push({
        value: existingCode,
        label: `${existingCode} — current assignment (inactive)`,
      });
    }
    return active.sort((left, right) => left.value.localeCompare(right.value));
  }, [currencies, editing?.functionalCurrencyCode]);

  const openEditor = (book?: AccountingBook) => {
    setEditing(book ?? null);
    setForm(
      book
        ? {
            code: book.code,
            name: book.name,
            description: book.description ?? '',
            purpose: book.purpose,
            bookType: typeOf(book),
            functionalCurrencyCode: book.functionalCurrencyCode ?? '',
            effectiveFromUtc: dateValue(book.effectiveFromUtc),
            effectiveToUtc: dateValue(book.effectiveToUtc),
            baseAccountingBookId: book.baseAccountingBookId ?? null,
            replicationStartDate: dateValue(book.replicationStartDate),
            parallelOpeningMode: book.parallelOpeningMode ?? 'ZeroOpening',
            parallelTranslationMethod:
              book.parallelTranslationMethod ?? null,
            currencyTranslationReserveAccountId:
              book.currencyTranslationReserveAccountId ?? null,
            currencyRoundingAccountId:
              book.currencyRoundingAccountId ?? null,
            sortOrder: book.sortOrder,
            rowVersion: book.rowVersion,
          }
        : blankForm()
    );
  };

  const openDetail = async (book: AccountingBook) => {
    setDetail(book);
    setDetailLoading(true);
    setDetailError(null);
    try {
      setDetail(await financeDataService.getAccountingBook(book.id));
    } catch (reason) {
      setDetailError(
        reason instanceof Error
          ? reason.message
          : 'Book detail could not be loaded.'
      );
    } finally {
      setDetailLoading(false);
    }
  };

  const save = async () => {
    if (!form.code.trim() || !form.name.trim() || !form.purpose.trim()) return;
    setSaving(true);
    try {
      const request: SaveAccountingBook = {
        ...form,
        code: form.code.trim().toUpperCase(),
        name: form.name.trim(),
        purpose: form.purpose.trim(),
        functionalCurrencyCode:
          form.bookType === 'Delta'
            ? null
            : form.functionalCurrencyCode?.trim().toUpperCase(),
        baseAccountingBookId:
          form.bookType === 'PrimaryFull' ? null : form.baseAccountingBookId,
        effectiveFromUtc:
          form.bookType === 'Delta' ? utcDate(form.effectiveFromUtc ?? '') : null,
        effectiveToUtc:
          form.bookType === 'Delta' ? utcDate(form.effectiveToUtc ?? '') : null,
        replicationStartDate:
          form.bookType === 'ParallelFull'
            ? utcDate(form.replicationStartDate ?? '')
            : null,
        parallelOpeningMode:
          form.bookType === 'ParallelFull'
            ? form.parallelOpeningMode
            : null,
        parallelTranslationMethod:
          form.bookType === 'ParallelFull' &&
          form.parallelOpeningMode === 'GovernedOpeningConversion'
            ? form.parallelTranslationMethod
            : null,
      };
      if (editing)
        await financeDataService.updateAccountingBook(editing.id, request);
      else await financeDataService.createAccountingBook(request);
      toast({
        title: editing ? 'Accounting book updated' : 'Accounting book created',
      });
      setEditing(undefined);
      await load();
    } catch (reason) {
      toast({
        title: 'Could not save accounting book',
        description:
          reason instanceof Error ? reason.message : 'The request failed.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const loadLifecycleEvidence = useCallback(
    async (book: AccountingBook, target: AccountingBookLifecycleStatus) => {
      if (target !== 'Initializing' && target !== 'Active') {
        setLifecycleEvidence(emptyLifecycleEvidence());
        return;
      }
      setLifecycleEvidence({ ...emptyLifecycleEvidence(), loading: true });
      try {
        if (target === 'Initializing') {
          const [initialization, periods] = await Promise.all([
            financeDataService.getAccountingBookInitialization(book.id),
            financeDataService.getAccountingBookPeriods(book.id),
          ]);
          setLifecycleEvidence({
            loading: false,
            error: null,
            initialization,
            readiness: null,
            periods,
          });
          return;
        }
        const [initialization, readiness, periods] = await Promise.all([
          financeDataService.getAccountingBookInitialization(book.id),
          financeDataService.getAccountingBookActivationReadiness(book.id),
          financeDataService.getAccountingBookPeriods(book.id),
        ]);
        setLifecycleEvidence({
          loading: false,
          error: null,
          initialization,
          readiness,
          periods,
        });
      } catch (reason) {
        setLifecycleEvidence({
          ...emptyLifecycleEvidence(),
          error:
            reason instanceof Error
              ? reason.message
              : 'Lifecycle evidence could not be loaded.',
        });
      }
    },
    []
  );

  const openTransition = (book: AccountingBook) => {
    const firstTarget = lifecycleTargets[statusOf(book)][0];
    if (!firstTarget) return;
    setTransitioning(book);
    setTargetStatus(firstTarget);
    setTransitionReason('');
    void loadLifecycleEvidence(book, firstTarget);
  };

  const selectTargetStatus = (value: AccountingBookLifecycleStatus) => {
    setTargetStatus(value);
    if (transitioning) void loadLifecycleEvidence(transitioning, value);
  };

  const openDecision = (book: AccountingBook, action: 'approve' | 'reject') => {
    setDecision({ book, action });
    setDecisionReason('');
    if (book.pendingLifecycleStatus)
      void loadLifecycleEvidence(book, book.pendingLifecycleStatus);
  };

  const requestTransition = async () => {
    if (!transitioning?.rowVersion || !transitionReason.trim()) return;
    setSaving(true);
    try {
      await financeDataService.requestAccountingBookTransition(
        transitioning.id,
        {
          targetStatus,
          reason: transitionReason.trim(),
          rowVersion: transitioning.rowVersion,
        }
      );
      toast({
        title: 'Lifecycle transition submitted',
        description: 'A different authorized user must approve this request.',
      });
      setTransitioning(null);
      await load();
    } catch (reason) {
      toast({
        title: 'Could not request transition',
        description:
          reason instanceof Error ? reason.message : 'The request failed.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const decideTransition = async () => {
    if (!decision?.book.rowVersion || !decisionReason.trim()) return;
    setSaving(true);
    try {
      const request = {
        reason: decisionReason.trim(),
        rowVersion: decision.book.rowVersion,
      };
      if (decision.action === 'approve')
        await financeDataService.approveAccountingBookTransition(
          decision.book.id,
          request
        );
      else
        await financeDataService.rejectAccountingBookTransition(
          decision.book.id,
          request
        );
      toast({
        title:
          decision.action === 'approve'
            ? 'Transition approved'
            : 'Transition rejected',
      });
      setDecision(null);
      await load();
    } catch (reason) {
      toast({
        title: 'Could not record transition decision',
        description:
          reason instanceof Error ? reason.message : 'The request failed.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const requestPrimaryReplacement = async () => {
    if (
      !primaryReplacement?.rowVersion ||
      !primaryReason.trim() ||
      !primaryEffectiveDate
    )
      return;
    setSaving(true);
    try {
      await financeDataService.requestPrimaryAccountingBookReplacement(
        primaryReplacement.id,
        {
          effectiveDate: `${primaryEffectiveDate}T00:00:00.000Z`,
          reason: primaryReason.trim(),
          rowVersion: primaryReplacement.rowVersion,
        }
      );
      toast({
        title: 'Primary-book replacement submitted',
        description:
          'A different authorized checker must approve it on or after the effective date.',
      });
      setPrimaryReplacement(null);
      setPrimaryReason('');
      await load();
    } catch (reason) {
      toast({
        title: 'Could not request primary-book replacement',
        description:
          reason instanceof Error ? reason.message : 'The request failed.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const decidePrimaryReplacement = async () => {
    if (!primaryDecision?.book.rowVersion || !primaryDecisionReason.trim())
      return;
    setSaving(true);
    try {
      const request = {
        reason: primaryDecisionReason.trim(),
        rowVersion: primaryDecision.book.rowVersion,
      };
      if (primaryDecision.action === 'approve')
        await financeDataService.approvePrimaryAccountingBookReplacement(
          primaryDecision.book.id,
          request
        );
      else
        await financeDataService.rejectPrimaryAccountingBookReplacement(
          primaryDecision.book.id,
          request
        );
      toast({
        title:
          primaryDecision.action === 'approve'
            ? 'Primary book replaced'
            : 'Primary-book replacement rejected',
      });
      setPrimaryDecision(null);
      setPrimaryDecisionReason('');
      await load();
    } catch (reason) {
      toast({
        title: 'Could not record primary-book decision',
        description:
          reason instanceof Error ? reason.message : 'The request failed.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const requestPrimaryReversal = async () => {
    if (!primaryReversal?.rowVersion || !primaryReversalReason.trim()) return;
    setSaving(true);
    try {
      await financeDataService.requestPrimaryAccountingBookReplacementReversal(
        primaryReversal.id,
        {
          reason: primaryReversalReason.trim(),
          rowVersion: primaryReversal.rowVersion,
        }
      );
      toast({
        title: 'Primary-book reversal submitted',
        description: 'A different authorized checker must approve the correction.',
      });
      setPrimaryReversal(null);
      setPrimaryReversalReason('');
      await load();
    } catch (reason) {
      toast({
        title: 'Could not request primary-book reversal',
        description: reason instanceof Error ? reason.message : 'The request failed.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const decidePrimaryReversal = async () => {
    if (!primaryReversalDecision?.book.rowVersion || !primaryReversalDecisionReason.trim()) return;
    setSaving(true);
    try {
      const request = {
        reason: primaryReversalDecisionReason.trim(),
        rowVersion: primaryReversalDecision.book.rowVersion,
      };
      if (primaryReversalDecision.action === 'approve')
        await financeDataService.approvePrimaryAccountingBookReplacementReversal(
          primaryReversalDecision.book.id,
          request
        );
      else
        await financeDataService.rejectPrimaryAccountingBookReplacementReversal(
          primaryReversalDecision.book.id,
          request
        );
      toast({
        title:
          primaryReversalDecision.action === 'approve'
            ? 'Primary-book replacement reversed'
            : 'Primary-book reversal rejected',
      });
      setPrimaryReversalDecision(null);
      setPrimaryReversalDecisionReason('');
      await load();
    } catch (reason) {
      toast({
        title: 'Could not record primary-book reversal decision',
        description: reason instanceof Error ? reason.message : 'The request failed.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  if (authLoading || loading) {
    return (
      <div className="flex min-h-[320px] items-center justify-center">
        <Loader2
          className="h-6 w-6 animate-spin"
          aria-label="Loading accounting books"
        />
      </div>
    );
  }

  if (authError) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Authentication unavailable</AlertTitle>
        <AlertDescription>
          Accounting-book access could not be verified. Refresh after
          authentication is restored.
        </AlertDescription>
      </Alert>
    );
  }

  if (!access.canRead) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Permission required</AlertTitle>
        <AlertDescription>
          Finance.Read is required to browse accounting-book configuration.
        </AlertDescription>
      </Alert>
    );
  }

  const updateBlocked = Boolean(
    editing?.pendingLifecycleStatus || editing?.primaryReplacementRequestedAtUtc
  );
  const structuralLocked = Boolean(
    editing &&
      (editing.hasAccountingUse ||
        editing.initializationStartedAtUtc ||
        editing.pendingLifecycleStatus ||
        editing.primaryReplacementRequestedAtUtc ||
        ['Initializing', 'Active', 'Suspended', 'Retired'].includes(
          statusOf(editing)
        ))
  );
  const targetIsBlockedActivation =
    targetStatus === 'Active' &&
    (!transitioning?.activationReady ||
      lifecycleEvidence.loading ||
      Boolean(lifecycleEvidence.error) ||
      !lifecycleEvidence.readiness?.isReady ||
      !lifecycleEvidence.initialization);
  const decisionIsBlockedActivation =
    decision?.action === 'approve' &&
    decision.book.pendingLifecycleStatus === 'Active' &&
    (lifecycleEvidence.loading ||
      Boolean(lifecycleEvidence.error) ||
      !lifecycleEvidence.readiness?.isReady ||
      !lifecycleEvidence.initialization);

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Button asChild variant="ghost" className="mb-2 px-0">
            <Link href="/finance/settings">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Finance settings
            </Link>
          </Button>
          <h1 className="flex items-center gap-2 text-2xl font-semibold">
            <BookOpen className="h-6 w-6" />
            Accounting books
          </h1>
          <p className="text-muted-foreground">
            Govern ledger purpose, structural identity and lifecycle without
            enabling cross-book execution.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/finance/settings/accounting-books/applicability">
              Applicability policy
            </Link>
          </Button>
          {access.canManage && (
            <Button onClick={() => openEditor()}>
              <Plus className="mr-2 h-4 w-4" />
              New accounting book
            </Button>
          )}
        </div>
      </div>

      <Alert>
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>About book activation</AlertTitle>
        <AlertDescription>
          Each approved request moves a book to its next lifecycle state. Before
          requesting Active, approve the book&apos;s initialization and open its
          required book period. Activating a book does not automatically post
          transactions to multiple books.
        </AlertDescription>
      </Alert>

      {error ? (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Could not load accounting books</AlertTitle>
          <AlertDescription className="flex items-center justify-between gap-3">
            <span>{error}</span>
            <Button size="sm" variant="outline" onClick={() => void load()}>
              <RefreshCw className="mr-2 h-4 w-4" />
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      ) : books.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center">
            <p className="text-muted-foreground">
              No accounting books are configured.
            </p>
            {access.canManage && (
              <Button className="mt-4" onClick={() => openEditor()}>
                <Plus className="mr-2 h-4 w-4" />
                Create the first book
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {books.map((book) => {
            const status = statusOf(book);
            const isRequester = Boolean(
              book.pendingLifecycleStatus &&
                user?.id &&
                book.transitionRequestedByUserId &&
                user.id.toLowerCase() ===
                  book.transitionRequestedByUserId.toLowerCase()
            );
            const canDecide = Boolean(
              access.canApproveTransition &&
                book.pendingLifecycleStatus &&
                isIndependentChecker(user?.id, book.transitionRequestedByUserId)
            );
            // The tenant Primary is system-provisioned and perpetual. Legacy replacement
            // evidence remains readable, but no new replacement/reversal actions are exposed.
            const canDecidePrimary = false;
            const canRequestPrimary = false;
            const canRequestPrimaryReversal = false;
            const canDecidePrimaryReversal = false;
            const isGovernedChangeRequester = Boolean(
              user?.id &&
                ((book.primaryReplacementRequestedAtUtc &&
                  book.primaryReplacementRequestedByUserId &&
                  user.id.toLowerCase() ===
                    book.primaryReplacementRequestedByUserId.toLowerCase()) ||
                  (book.primaryReversalRequestedAtUtc &&
                    book.primaryReversalRequestedByUserId &&
                    user.id.toLowerCase() ===
                      book.primaryReversalRequestedByUserId.toLowerCase()))
            );
            return (
              <Card key={book.id}>
                <CardHeader>
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <CardTitle className="text-lg">
                        {book.code} — {book.name}
                      </CardTitle>
                      <CardDescription>{book.purpose}</CardDescription>
                    </div>
                    <div className="flex gap-2">
                      <Badge
                        variant={status === 'Active' ? 'default' : 'secondary'}
                      >
                        {status}
                      </Badge>
                      {book.isDefault && (
                        <Badge variant="outline">Primary/default</Badge>
                      )}
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="space-y-3 text-sm">
                  <div className="grid grid-cols-2 gap-2">
                    <span>
                      <span className="text-muted-foreground">Type:</span>{' '}
                      {typeOf(book)}
                    </span>
                    <span>
                      <span className="text-muted-foreground">Currency:</span>{' '}
                      {book.functionalCurrencyCode || 'Inherited from base'}
                    </span>
                    <span>
                      <span className="text-muted-foreground">
                        {typeOf(book) === 'Delta'
                          ? 'Posting window:'
                          : typeOf(book) === 'ParallelFull'
                            ? 'Replication starts:'
                            : 'Availability:'}
                      </span>{' '}
                      {typeOf(book) === 'Delta'
                        ? `${dateValue(book.effectiveFromUtc) || 'Open'} → ${dateValue(book.effectiveToUtc) || 'Open'}`
                        : typeOf(book) === 'ParallelFull'
                          ? dateValue(book.replicationStartDate) || 'Not set'
                          : 'Perpetual'}
                    </span>
                    <span>
                      <span className="text-muted-foreground">Base:</span>{' '}
                      {baseBookLabel(book)}
                    </span>
                  </div>
                  {book.pendingLifecycleStatus && (
                    <Alert
                      className={
                        book.pendingLifecycleStatus === 'Active' &&
                        !book.activationReady
                          ? 'border-amber-300 bg-amber-50'
                          : undefined
                      }
                    >
                      <AlertTitle>
                        Pending {book.pendingLifecycleStatus}
                      </AlertTitle>
                      <AlertDescription>
                        {book.pendingTransitionReason ||
                          'Awaiting independent review.'}
                        {book.pendingLifecycleStatus === 'Active' &&
                        !book.activationReady
                          ? ` ${book.readinessMessage || 'Approved initialization and an open first exact-book period are required.'}`
                          : ''}
                      </AlertDescription>
                    </Alert>
                  )}
                  {book.primaryReplacementRequestedAtUtc && (
                    <Alert className="border-blue-300 bg-blue-50">
                      <AlertTitle>Pending primary-book replacement</AlertTitle>
                      <AlertDescription>
                        Proposed effective date:{' '}
                        {dateValue(book.primaryReplacementEffectiveDate)}.{' '}
                        {book.primaryReplacementReason}
                      </AlertDescription>
                    </Alert>
                  )}
                  {book.primaryReversalRequestedAtUtc && (
                    <Alert className="border-amber-300 bg-amber-50">
                      <AlertTitle>Pending same-day primary reversal</AlertTitle>
                      <AlertDescription>
                        Restore{' '}
                        {books.find(
                          (item) =>
                            item.id ===
                            book.reversiblePrimaryDesignationPreviousBookId
                        )?.code || 'the prior primary book'}
                        . {book.primaryReversalReason}
                      </AlertDescription>
                    </Alert>
                  )}
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => void openDetail(book)}
                    >
                      View details
                    </Button>
                    <Button asChild variant="outline" size="sm">
                      <Link
                        href={`/finance/settings/accounting-books/${book.id}/readiness`}
                      >
                        Initialization
                      </Link>
                    </Button>
                    {typeOf(book) === 'Delta' && status === 'Active' && book.allowsPosting && (
                        <Button asChild variant="outline" size="sm">
                          <Link href={`/finance/journal-entries/new?journalType=Delta%20Adjustment&book=${encodeURIComponent(book.code)}`}>
                            New Delta adjustment
                          </Link>
                        </Button>
                    )}
                    {typeOf(book) === 'Delta' && (
                      <>
                        <Button asChild variant="outline" size="sm">
                          <Link href={`/finance/settings/accounting-books/${book.id}/delta-ledger`}>
                            Delta ledger
                          </Link>
                        </Button>
                        <Button asChild variant="outline" size="sm">
                          <Link href={`/finance/settings/accounting-books/${book.id}/delta-report`}>
                            Base + Delta report
                          </Link>
                        </Button>
                      </>
                    )}
                    {access.canManage && status !== 'Retired' && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={Boolean(
                          book.pendingLifecycleStatus ||
                            book.primaryReplacementRequestedAtUtc ||
                            book.primaryReversalRequestedAtUtc
                        )}
                        onClick={() => openEditor(book)}
                      >
                        <Pencil className="mr-2 h-3.5 w-3.5" />
                        Edit
                      </Button>
                    )}
                    {typeOf(book) !== 'PrimaryFull' &&
                      access.canRequestTransition &&
                      !book.pendingLifecycleStatus &&
                      !book.primaryReplacementRequestedAtUtc &&
                      !book.primaryReversalRequestedAtUtc &&
                      lifecycleTargets[status].length > 0 &&
                      (status === 'Initializing' && !book.activationReady ? (
                        <Button asChild size="sm">
                          <Link
                            href={`/finance/settings/accounting-books/${book.id}/readiness`}
                          >
                            Complete activation readiness
                          </Link>
                        </Button>
                      ) : (
                        <Button size="sm" onClick={() => openTransition(book)}>
                          {lifecycleTargets[status].length === 1
                            ? `Request ${lifecycleTargets[status][0]}`
                            : 'Request lifecycle change'}
                        </Button>
                      ))}
                    {canRequestPrimary && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setPrimaryReplacement(book);
                          setPrimaryEffectiveDate(
                            dateValue(new Date().toISOString())
                          );
                          setPrimaryReason('');
                        }}
                      >
                        Request as primary
                      </Button>
                    )}
                    {canRequestPrimaryReversal && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setPrimaryReversal(book);
                          setPrimaryReversalReason('');
                        }}
                      >
                        Reverse today&apos;s primary change
                      </Button>
                    )}
                    {canDecide && (
                      <>
                        <Button
                          size="sm"
                          disabled={
                            book.pendingLifecycleStatus === 'Active' &&
                            !book.activationReady
                          }
                          onClick={() => openDecision(book, 'approve')}
                        >
                          Review &amp; approve
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => openDecision(book, 'reject')}
                        >
                          Review &amp; reject
                        </Button>
                      </>
                    )}
                    {canDecidePrimary && (
                      <>
                        <Button
                          size="sm"
                          onClick={() => {
                            setPrimaryDecision({ book, action: 'approve' });
                            setPrimaryDecisionReason('');
                          }}
                        >
                          Review primary replacement
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => {
                            setPrimaryDecision({ book, action: 'reject' });
                            setPrimaryDecisionReason('');
                          }}
                        >
                          Reject primary replacement
                        </Button>
                      </>
                    )}
                    {canDecidePrimaryReversal && (
                      <>
                        <Button
                          size="sm"
                          onClick={() => {
                            setPrimaryReversalDecision({ book, action: 'approve' });
                            setPrimaryReversalDecisionReason('');
                          }}
                        >
                          Review primary reversal
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => {
                            setPrimaryReversalDecision({ book, action: 'reject' });
                            setPrimaryReversalDecisionReason('');
                          }}
                        >
                          Reject primary reversal
                        </Button>
                      </>
                    )}
                    {(isRequester || isGovernedChangeRequester) && (
                      <span className="self-center text-xs text-muted-foreground">
                        Awaiting a different authorized checker.
                      </span>
                    )}
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}

      <Dialog
        open={editing !== undefined}
        onOpenChange={(open) => {
          if (!open) setEditing(undefined);
        }}
      >
        <DialogContent className="flex max-h-[calc(100dvh-2rem)] max-w-2xl flex-col gap-0 overflow-hidden p-0">
          <DialogHeader className="shrink-0 px-6 pb-4 pt-6">
            <DialogTitle>
              {editing ? `Edit ${editing.code}` : 'Create accounting book'}
            </DialogTitle>
            <DialogDescription>
              Delta books add governed adjustment layers. Parallel books are
              foreign-currency replicas of the tenant Primary and begin at an
              explicit replication cutoff.
            </DialogDescription>
          </DialogHeader>
          <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-2">
            {structuralLocked && (
              <Alert>
              <ShieldAlert className="h-4 w-4" />
              <AlertTitle>Structural identity locked</AlertTitle>
              <AlertDescription>
                {updateBlocked
                  ? 'No changes can be saved while this book has a pending lifecycle transition.'
                  : 'This lifecycle or its initialization/accounting evidence locks structural fields. Only display name, description and display order can be amended.'}
              </AlertDescription>
              </Alert>
            )}
            <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="book-code">Stable code</Label>
              <Input
                id="book-code"
                value={form.code}
                disabled={structuralLocked || Boolean(editing?.isSystemDefined)}
                onChange={(event) =>
                  setForm({ ...form, code: event.target.value.toUpperCase() })
                }
              />
            </div>
            <div>
              <Label htmlFor="book-name">Name</Label>
              <Input
                id="book-name"
                value={form.name}
                disabled={updateBlocked}
                onChange={(event) =>
                  setForm({ ...form, name: event.target.value })
                }
              />
            </div>
            <div>
              <Label>Book type</Label>
              <Select
                value={form.bookType}
                disabled={structuralLocked}
                onValueChange={(value) =>
                  setForm({
                    ...form,
                    bookType: value as AccountingBookType,
                    baseAccountingBookId: null,
                  })
                }
              >
                <SelectTrigger aria-label="Book type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(form.bookType === 'PrimaryFull'
                    ? (['PrimaryFull'] as AccountingBookType[])
                    : bookTypes
                  ).map((type) => (
                    <SelectItem key={type} value={type}>
                      {bookTypeLabels[type]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="book-purpose">
                Accounting purpose / principle
              </Label>
              <Input
                id="book-purpose"
                value={form.purpose}
                disabled={structuralLocked}
                onChange={(event) =>
                  setForm({ ...form, purpose: event.target.value })
                }
                placeholder="e.g. IFRS reporting"
              />
            </div>
            {form.bookType !== 'PrimaryFull' && (
              <div className="sm:col-span-2">
                <Label>Base accounting book</Label>
                <Select
                  value={form.baseAccountingBookId ?? ''}
                  disabled={structuralLocked}
                  onValueChange={(value) =>
                    setForm({ ...form, baseAccountingBookId: value })
                  }
                >
                  <SelectTrigger aria-label="Base accounting book">
                    <SelectValue placeholder="Select governed base book" />
                  </SelectTrigger>
                  <SelectContent>
                    {baseBookOptions.map((book) => (
                      <SelectItem key={book.id} value={book.id}>
                        {book.code} — {book.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {form.bookType !== 'Delta' && (
              <div>
                <Label>Functional currency</Label>
                <SearchableOptionPicker
                  label="Functional currency"
                  value={form.functionalCurrencyCode ?? ''}
                  options={currencyOptions}
                  onChange={(value) =>
                    setForm({ ...form, functionalCurrencyCode: value })
                  }
                  placeholder={
                    currencyLoading ? 'Loading currencies…' : 'Select currency'
                  }
                  searchPlaceholder="Search code or name…"
                  emptyMessage="No active currency found."
                  disabled={
                    structuralLocked ||
                    currencyLoading ||
                    Boolean(currencyError)
                  }
                />
                {currencyError && (
                  <p className="mt-1 text-sm text-destructive">
                    {currencyError}{' '}
                    <Button
                      type="button"
                      variant="link"
                      className="h-auto p-0"
                      onClick={() => void loadCurrencies()}
                    >
                      Retry currencies
                    </Button>
                  </p>
                )}
              </div>
            )}
            <div>
              <Label htmlFor="book-order">Display order</Label>
              <Input
                id="book-order"
                type="number"
                value={form.sortOrder}
                disabled={updateBlocked}
                onChange={(event) =>
                  setForm({ ...form, sortOrder: Number(event.target.value) })
                }
              />
            </div>
            {form.bookType === 'Delta' && (
              <>
                <Alert className="sm:col-span-2">
                  <AlertTitle>
                    Primary activity remains available as a live inherited view
                  </AlertTitle>
                  <AlertDescription>
                    Primary journals are not copied into this Delta ledger. The
                    Delta stores only explicit adjustment journals, while
                    reporting combines the live Primary balance with selected
                    Delta adjustments without duplicating the base activity.
                  </AlertDescription>
                </Alert>
                <div>
                  <Label htmlFor="book-from">Posting allowed from (optional)</Label>
                  <Input
                    id="book-from"
                    type="date"
                    value={dateValue(form.effectiveFromUtc)}
                    disabled={structuralLocked}
                    onChange={(event) =>
                      setForm({ ...form, effectiveFromUtc: event.target.value })
                    }
                  />
                </div>
                <div>
                  <Label htmlFor="book-to">Posting allowed through (optional)</Label>
                  <Input
                    id="book-to"
                    type="date"
                    value={dateValue(form.effectiveToUtc)}
                    disabled={structuralLocked}
                    onChange={(event) =>
                      setForm({ ...form, effectiveToUtc: event.target.value })
                    }
                  />
                </div>
              </>
            )}
            {form.bookType === 'ParallelFull' && (
              <>
                <div>
                  <Label htmlFor="replication-start">Replication start date</Label>
                  <Input
                    id="replication-start"
                    type="date"
                    value={dateValue(form.replicationStartDate)}
                    disabled={structuralLocked}
                    onChange={(event) =>
                      setForm({ ...form, replicationStartDate: event.target.value })
                    }
                  />
                </div>
                <div>
                  <Label>Opening treatment</Label>
                  <Select
                    value={form.parallelOpeningMode ?? 'ZeroOpening'}
                    disabled={structuralLocked}
                    onValueChange={(value) =>
                      setForm({
                        ...form,
                        parallelOpeningMode: value as SaveAccountingBook['parallelOpeningMode'],
                        parallelTranslationMethod:
                          value === 'GovernedOpeningConversion'
                            ? form.parallelTranslationMethod ?? 'ClassificationDriven'
                            : null,
                      })
                    }
                  >
                    <SelectTrigger aria-label="Parallel opening treatment"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="ZeroOpening">Zero opening</SelectItem>
                      <SelectItem value="GovernedOpeningConversion">Convert Primary closing balances</SelectItem>
                      <SelectItem value="HistoricalReplay">Replay historical Primary transactions</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                {form.parallelOpeningMode === 'GovernedOpeningConversion' && (
                  <div className="sm:col-span-2">
                    <Label>Opening translation method</Label>
                    <Select
                      value={form.parallelTranslationMethod ?? 'ClassificationDriven'}
                      disabled={structuralLocked}
                      onValueChange={(value) =>
                        setForm({
                          ...form,
                          parallelTranslationMethod:
                            value as SaveAccountingBook['parallelTranslationMethod'],
                        })
                      }
                    >
                      <SelectTrigger aria-label="Opening translation method"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="ClassificationDriven">Classification-driven rates</SelectItem>
                        <SelectItem value="SingleApprovedRate">One approved cutoff rate</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                )}
                {form.parallelOpeningMode === 'ZeroOpening' && (
                  <Alert className="sm:col-span-2 border-amber-300 bg-amber-50 text-amber-950">
                    <AlertTitle>Earlier Primary activity will not be copied</AlertTitle>
                    <AlertDescription>
                      This Parallel book starts at zero. Primary journals dated before the replication start date remain outside this foreign-currency ledger. Use governed opening conversion or historical replay when that history must be represented.
                    </AlertDescription>
                  </Alert>
                )}
                {form.parallelOpeningMode === 'GovernedOpeningConversion' && (
                  <Alert className="sm:col-span-2">
                    <AlertTitle>Opening balances will be translated</AlertTitle>
                    <AlertDescription>
                      The approved Primary closing balances at the cutoff will establish this book&apos;s opening; automatic transaction replication begins after that cutoff.
                    </AlertDescription>
                  </Alert>
                )}
                {form.parallelOpeningMode === 'HistoricalReplay' && (
                  <Alert className="sm:col-span-2">
                    <AlertTitle>Full rate history required</AlertTitle>
                    <AlertDescription>
                      Every replayed Primary journal requires an approved rate for its accounting date. Missing rate evidence blocks activation rather than silently excluding transactions.
                    </AlertDescription>
                  </Alert>
                )}
              </>
            )}
            <div className="sm:col-span-2">
              <Label htmlFor="book-description">Description</Label>
              <Textarea
                id="book-description"
                value={form.description ?? ''}
                disabled={updateBlocked}
                onChange={(event) =>
                  setForm({ ...form, description: event.target.value })
                }
              />
            </div>
            </div>
          </div>
          <DialogFooter className="shrink-0 border-t bg-background px-6 py-4">
            <Button variant="outline" onClick={() => setEditing(undefined)}>
              Cancel
            </Button>
            <Button
              disabled={
                saving ||
                updateBlocked ||
                !form.code.trim() ||
                !form.name.trim() ||
                !form.purpose.trim() ||
                (form.bookType !== 'PrimaryFull' && !form.baseAccountingBookId) ||
                (form.bookType === 'ParallelFull' &&
                  (!form.replicationStartDate ||
                    currencyLoading ||
                    Boolean(currencyError) ||
                    !currencyOptions.some(
                      (option) => option.value === form.functionalCurrencyCode
                    )))
              }
              onClick={() => void save()}
            >
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
              book
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={detail !== null}
        onOpenChange={(open) => {
          if (!open) setDetail(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{detail?.code} accounting-book details</DialogTitle>
            <DialogDescription>
              Governed structure and lifecycle evidence for this tenant-owned
              book.
            </DialogDescription>
          </DialogHeader>
          {detailLoading ? (
            <Loader2
              className="mx-auto my-8 h-6 w-6 animate-spin"
              aria-label="Loading book detail"
            />
          ) : detailError ? (
            <Alert variant="destructive">
              <AlertTitle>Could not load book detail</AlertTitle>
              <AlertDescription>
                {detailError}{' '}
                <Button
                  variant="link"
                  className="h-auto p-0"
                  onClick={() => detail && void openDetail(detail)}
                >
                  Retry
                </Button>
              </AlertDescription>
            </Alert>
          ) : (
            detail && (
              <div className="grid gap-3 text-sm sm:grid-cols-2">
                <div>
                  <span className="text-muted-foreground">Lifecycle</span>
                  <p>{statusOf(detail)}</p>
                </div>
                <div>
                  <span className="text-muted-foreground">Type</span>
                  <p>{typeOf(detail)}</p>
                </div>
                <div>
                  <span className="text-muted-foreground">Purpose</span>
                  <p>{detail.purpose}</p>
                </div>
                <div>
                  <span className="text-muted-foreground">
                    Functional currency
                  </span>
                  <p>
                    {detail.functionalCurrencyCode || 'Inherited from base'}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Accounting use</span>
                  <p>
                    {detail.hasAccountingUse ? 'Yes — structure locked' : 'No'}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">
                    Activation readiness
                  </span>
                  <p>{detail.activationReady ? 'Ready' : 'Not ready'}</p>
                </div>
                <div className="sm:col-span-2">
                  <span className="text-muted-foreground">
                    Readiness evidence
                  </span>
                  <p>
                    {detail.readinessMessage ||
                      'Approved initialization and an open first exact-book period are required.'}
                  </p>
                </div>
              </div>
            )
          )}
        </DialogContent>
      </Dialog>

      <Dialog
        open={transitioning !== null}
        onOpenChange={(open) => {
          if (!open) setTransitioning(null);
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Request lifecycle transition</DialogTitle>
            <DialogDescription>
              This audited request requires a different authorized checker to
              approve it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Target lifecycle state</Label>
              <Select
                value={targetStatus}
                onValueChange={(value) =>
                  selectTargetStatus(value as AccountingBookLifecycleStatus)
                }
              >
                <SelectTrigger aria-label="Target lifecycle state">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {transitioning &&
                    lifecycleTargets[statusOf(transitioning)].map((status) => (
                      <SelectItem key={status} value={status}>
                        {status}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
            {transitioning && (
              <LifecycleTransitionEvidence
                book={transitioning}
                target={targetStatus}
                evidence={lifecycleEvidence}
                retry={() =>
                  void loadLifecycleEvidence(transitioning, targetStatus)
                }
              />
            )}
            <div>
              <Label htmlFor="transition-reason">Reason</Label>
              <Textarea
                id="transition-reason"
                value={transitionReason}
                onChange={(event) => setTransitionReason(event.target.value)}
                placeholder="Explain the governed lifecycle change"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTransitioning(null)}>
              Cancel
            </Button>
            <Button
              disabled={
                saving || !transitionReason.trim() || targetIsBlockedActivation
              }
              onClick={() => void requestTransition()}
            >
              Submit for approval
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={decision !== null}
        onOpenChange={(open) => {
          if (!open) setDecision(null);
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {decision?.action === 'approve' ? 'Approve' : 'Reject'} pending
              transition
            </DialogTitle>
            <DialogDescription>
              Review the evidence below. Maker-checker separation and current
              readiness are revalidated by Finance before the decision is
              committed.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            {decision?.book.pendingLifecycleStatus && (
              <LifecycleTransitionEvidence
                book={decision.book}
                target={decision.book.pendingLifecycleStatus}
                evidence={lifecycleEvidence}
                retry={() => {
                  const target = decision.book.pendingLifecycleStatus;
                  if (target) void loadLifecycleEvidence(decision.book, target);
                }}
              />
            )}
            <div>
              <Label htmlFor="decision-reason">Checker reason</Label>
              <Textarea
                id="decision-reason"
                value={decisionReason}
                onChange={(event) => setDecisionReason(event.target.value)}
                placeholder="Summarize the configuration and readiness evidence reviewed"
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Record what you checked; avoid generic reasons such as
                “Approved”.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDecision(null)}>
              Cancel
            </Button>
            <Button
              variant={
                decision?.action === 'reject' ? 'destructive' : 'default'
              }
              disabled={
                saving ||
                !decisionReason.trim() ||
                Boolean(decisionIsBlockedActivation)
              }
              onClick={() => void decideTransition()}
            >
              {decision?.action === 'approve'
                ? 'Approve transition'
                : 'Reject transition'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={primaryReplacement !== null}
        onOpenChange={(open) => {
          if (!open) setPrimaryReplacement(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request primary-book replacement</DialogTitle>
            <DialogDescription>
              This promotes {primaryReplacement?.code} and demotes the current
              primary only after an independent checker approves. Existing
              frozen selections are never rewritten.
            </DialogDescription>
          </DialogHeader>
          <Alert>
            <ShieldAlert className="h-4 w-4" />
            <AlertTitle>Controlled replacement</AlertTitle>
            <AlertDescription>
              The proposed book must remain active, posting-enabled, fully
              initialized, and in the same functional currency. Approval cannot
              occur before the effective date.
            </AlertDescription>
          </Alert>
          <div className="space-y-4">
            <div>
              <Label htmlFor="primary-effective-date">Effective date</Label>
              <Input
                id="primary-effective-date"
                type="date"
                min={dateValue(new Date().toISOString())}
                value={primaryEffectiveDate}
                onChange={(event) =>
                  setPrimaryEffectiveDate(event.target.value)
                }
              />
            </div>
            <div>
              <Label htmlFor="primary-reason">Business reason</Label>
              <Textarea
                id="primary-reason"
                value={primaryReason}
                onChange={(event) => setPrimaryReason(event.target.value)}
                placeholder="Explain why primary accounting authority should move"
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setPrimaryReplacement(null)}
            >
              Cancel
            </Button>
            <Button
              disabled={
                saving || !primaryEffectiveDate || !primaryReason.trim()
              }
              onClick={() => void requestPrimaryReplacement()}
            >
              Submit for approval
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={primaryDecision !== null}
        onOpenChange={(open) => {
          if (!open) setPrimaryDecision(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {primaryDecision?.action === 'approve' ? 'Approve' : 'Reject'}{' '}
              primary-book replacement
            </DialogTitle>
            <DialogDescription>
              Review the authority change before deciding. Finance revalidates
              both books when this decision is committed.
            </DialogDescription>
          </DialogHeader>
          {primaryDecision && (
            <div className="space-y-4">
              <div className="grid gap-3 rounded-md border p-4 text-sm sm:grid-cols-2">
                <div>
                  <span className="text-muted-foreground">Current primary</span>
                  <p>
                    {books.find(
                      (item) =>
                        item.id ===
                        primaryDecision.book.primaryReplacementFromBookId
                    )?.code || 'Unavailable'}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">
                    Proposed primary
                  </span>
                  <p>
                    {primaryDecision.book.code} — {primaryDecision.book.name}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Effective date</span>
                  <p>
                    {dateValue(
                      primaryDecision.book.primaryReplacementEffectiveDate
                    )}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">
                    Functional currency
                  </span>
                  <p>{primaryDecision.book.functionalCurrencyCode}</p>
                </div>
                <div className="sm:col-span-2">
                  <span className="text-muted-foreground">Maker reason</span>
                  <p>{primaryDecision.book.primaryReplacementReason}</p>
                </div>
              </div>
              <div>
                <Label htmlFor="primary-decision-reason">Checker reason</Label>
                <Textarea
                  id="primary-decision-reason"
                  value={primaryDecisionReason}
                  onChange={(event) =>
                    setPrimaryDecisionReason(event.target.value)
                  }
                  placeholder="Record the readiness and authority evidence reviewed"
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setPrimaryDecision(null)}>
              Cancel
            </Button>
            <Button
              variant={
                primaryDecision?.action === 'reject' ? 'destructive' : 'default'
              }
              disabled={saving || !primaryDecisionReason.trim()}
              onClick={() => void decidePrimaryReplacement()}
            >
              {primaryDecision?.action === 'approve'
                ? 'Approve replacement'
                : 'Reject replacement'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={primaryReversal !== null}
        onOpenChange={(open) => {
          if (!open) setPrimaryReversal(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reverse today&apos;s primary-book replacement</DialogTitle>
            <DialogDescription>
              This requests restoration of{' '}
              {books.find(
                (item) =>
                  item.id ===
                  primaryReversal?.reversiblePrimaryDesignationPreviousBookId
              )?.code || 'the prior primary book'}
              . The original designation and both approval trails remain in the audit history.
              Frozen transaction selections are not rewritten.
            </DialogDescription>
          </DialogHeader>
          <Alert>
            <ShieldAlert className="h-4 w-4" />
            <AlertTitle>Same-day governed correction</AlertTitle>
            <AlertDescription>
              Only the latest replacement effective today can be reversed. Finance revalidates
              both books before the checker commits the correction.
            </AlertDescription>
          </Alert>
          <div>
            <Label htmlFor="primary-reversal-reason">Correction reason</Label>
            <Textarea
              id="primary-reversal-reason"
              value={primaryReversalReason}
              onChange={(event) => setPrimaryReversalReason(event.target.value)}
              placeholder="Explain why today’s primary-book replacement must be reversed"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPrimaryReversal(null)}>
              Cancel
            </Button>
            <Button
              disabled={saving || !primaryReversalReason.trim()}
              onClick={() => void requestPrimaryReversal()}
            >
              Submit reversal for approval
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={primaryReversalDecision !== null}
        onOpenChange={(open) => {
          if (!open) setPrimaryReversalDecision(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {primaryReversalDecision?.action === 'approve' ? 'Approve' : 'Reject'}{' '}
              primary-book reversal
            </DialogTitle>
            <DialogDescription>
              Review the same-day correction. Approval restores the previous primary book while
              retaining the original replacement as reversed evidence.
            </DialogDescription>
          </DialogHeader>
          {primaryReversalDecision && (
            <div className="space-y-4">
              <div className="grid gap-3 rounded-md border p-4 text-sm sm:grid-cols-2">
                <div>
                  <span className="text-muted-foreground">Current primary</span>
                  <p>{primaryReversalDecision.book.code}</p>
                </div>
                <div>
                  <span className="text-muted-foreground">Restore primary</span>
                  <p>
                    {books.find(
                      (item) =>
                        item.id ===
                        primaryReversalDecision.book
                          .reversiblePrimaryDesignationPreviousBookId
                    )?.code || 'Unavailable'}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Effective date</span>
                  <p>
                    {dateValue(
                      primaryReversalDecision.book
                        .reversiblePrimaryDesignationEffectiveDate
                    )}
                  </p>
                </div>
                <div className="sm:col-span-2">
                  <span className="text-muted-foreground">Maker reason</span>
                  <p>{primaryReversalDecision.book.primaryReversalReason}</p>
                </div>
              </div>
              <div>
                <Label htmlFor="primary-reversal-decision-reason">Checker reason</Label>
                <Textarea
                  id="primary-reversal-decision-reason"
                  value={primaryReversalDecisionReason}
                  onChange={(event) =>
                    setPrimaryReversalDecisionReason(event.target.value)
                  }
                  placeholder="Record the correction evidence and readiness reviewed"
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setPrimaryReversalDecision(null)}>
              Cancel
            </Button>
            <Button
              variant={
                primaryReversalDecision?.action === 'reject' ? 'destructive' : 'default'
              }
              disabled={saving || !primaryReversalDecisionReason.trim()}
              onClick={() => void decidePrimaryReversal()}
            >
              {primaryReversalDecision?.action === 'approve'
                ? 'Approve reversal'
                : 'Reject reversal'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
