'use client';

import React, { useEffect, useRef, useState } from 'react';
import {
  Columns3,
  CopyPlus,
  Loader2,
  Maximize2,
  Minimize2,
  Plus,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PostingAccountPicker } from '@/components/finance/PostingAccountPicker';
import {
  accountsPayableService,
  supplierInvoiceDistributionService,
  type SupplierDistributionAccount,
} from '@/services/procurementSupplierInvoiceService';
import type { VendorInvoiceDistribution } from '@/types/ap';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

type Row = Omit<
  VendorInvoiceDistribution['lines'][number],
  'debit' | 'credit'
> & { debit: string; credit: string };
const amount = (value: number) =>
  value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
const cents = (value: string | number) => Math.round(Number(value || 0) * 100);
const moneyValid = (value: string) =>
  /^(?:\d+)(?:\.\d{1,2})?$/.test(value || '0') &&
  Number(value || 0) <= 999999999999.99;

export function ProcurementInvoiceDistribution({
  invoiceId,
}: {
  invoiceId: string;
}) {
  const [open, setOpen] = useState(false);
  const [fullPage, setFullPage] = useState(false);
  const [data, setData] = useState<VendorInvoiceDistribution | null>(null);
  const [lines, setLines] = useState<Row[]>([]);
  const [accounts, setAccounts] = useState<SupplierDistributionAccount[]>([]);
  const [error, setError] = useState('');
  const [accountError, setAccountError] = useState('');
  const [notice, setNotice] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [retry, setRetry] = useState(0);
  const [confirm, setConfirm] = useState<'close' | 'reset' | 'reload' | null>(
    null
  );
  const inFlight = useRef(false);
  const apply = (result: VendorInvoiceDistribution) => {
    setData(result);
    setLines(
      result.lines.map((line) => ({
        ...line,
        debit: String(line.debit),
        credit: String(line.credit),
      }))
    );
    setDirty(false);
  };
  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError('');
    setNotice('');
    setAccountError('');
    setData(null);
    setAccounts([]);
    accountsPayableService
      .getInvoiceDistribution(invoiceId)
      .then(async (result) => {
        if (cancelled) return;
        apply(result);
        if (
          result.canEdit &&
          result.status !== 'Posted' &&
          result.groups?.some((group) => group.canChangeAccount)
        ) {
          try {
            const catalogue =
              await supplierInvoiceDistributionService.accounts(invoiceId);
            if (!cancelled) setAccounts(catalogue);
          } catch (cause) {
            if (!cancelled)
              setAccountError(
                getProcurementProblemMessage(
                  cause,
                  'Unable to load eligible accounts. Reload to try again.'
                )
              );
          }
        }
      })
      .catch((cause) => {
        if (!cancelled)
          setError(
            getProcurementProblemMessage(
              cause,
              'Unable to load the invoice distribution.'
            )
          );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, invoiceId, retry]);

  const canEdit = Boolean(data?.canEdit && data.status !== 'Posted');
  const groups = data?.groups ?? [];
  const total = (side: 'debit' | 'credit') =>
    lines.reduce(
      (sum, row) =>
        sum + (Number.isFinite(Number(row[side])) ? cents(row[side]) : 0),
      0
    );
  const debit = total('debit'),
    credit = total('credit'),
    difference = debit - credit;
  const update = (next: Row[]) => {
    setLines(next);
    setDirty(true);
    setNotice('');
    setError('');
  };
  const change = (id: string, values: Partial<Row>) =>
    update(
      lines.map((row) => (row.lineId === id ? { ...row, ...values } : row))
    );
  const requestClose = (next: boolean) => {
    if (inFlight.current) return;
    if (!next && dirty) setConfirm('close');
    else setOpen(next);
  };
  const reload = () =>
    dirty ? setConfirm('reload') : setRetry((value) => value + 1);
  const add = () => {
    const group = groups.find((value) => value.canChangeAccount) ?? groups[0];
    if (!group) return;
    const original = data?.lines.find((row) => row.groupId === group.groupId);
    update([
      ...lines,
      {
        lineId: crypto.randomUUID(),
        groupId: group.groupId,
        accountId: group.accountId,
        accountCode: original?.accountCode ?? '',
        accountName: original?.accountName ?? '',
        description: group.description ?? '',
        type: group.type,
        source: 'Distribution edit',
        debit: '0',
        credit: '0',
      },
    ]);
  };
  const split = (row: Row) => {
    const d = Math.floor(cents(row.debit) / 2),
      c = Math.floor(cents(row.credit) / 2);
    update(
      lines.flatMap((value) =>
        value.lineId !== row.lineId
          ? [value]
          : [
              {
                ...row,
                debit: String((cents(row.debit) - d) / 100),
                credit: String((cents(row.credit) - c) / 100),
              },
              {
                ...row,
                lineId: crypto.randomUUID(),
                debit: String(d / 100),
                credit: String(c / 100),
              },
            ]
      )
    );
  };
  const validate = () => {
    if (lines.length < 2 || lines.length > 1000)
      return 'Use between 2 and 1,000 distribution lines.';
    if (!Number.isSafeInteger(debit) || !Number.isSafeInteger(credit))
      return 'Distribution totals exceed the supported editing precision.';
    if (
      lines.some(
        (row) =>
          !row.accountId ||
          !row.groupId ||
          !moneyValid(row.debit) ||
          !moneyValid(row.credit) ||
          (cents(row.debit) === 0) === (cents(row.credit) === 0)
      )
    )
      return 'Each line needs an account and one positive debit or credit, with at most two decimal places.';
    if (difference !== 0)
      return 'Total debits and credits must balance before saving.';
    for (const group of groups) {
      const rows = lines.filter((row) => row.groupId === group.groupId);
      if (
        rows.reduce((sum, row) => sum + cents(row.debit), 0) !==
          cents(group.debit) ||
        rows.reduce((sum, row) => sum + cents(row.credit), 0) !==
          cents(group.credit)
      )
        return `${group.type} must total debit ${amount(group.debit)} and credit ${amount(group.credit)}. Preserve each invoice amount when splitting.`;
    }
    return '';
  };
  const save = async (reset = false) => {
    if (!data || !canEdit || inFlight.current) return false;
    const message = reset ? '' : validate();
    if (message) {
      setError(message);
      return false;
    }
    if (!data.version || !data.basisVersion) {
      setError('Reload the distribution before saving.');
      return false;
    }
    inFlight.current = true;
    setSaving(true);
    setError('');
    setNotice('');
    try {
      const input = {
        version: data.version,
        basisVersion: data.basisVersion,
        lines: reset
          ? []
          : lines.map((row) => ({
              lineId: row.lineId,
              groupId: row.groupId!,
              accountId: row.accountId,
              debit: Number(row.debit || 0),
              credit: Number(row.credit || 0),
            })),
      };
      const result = await (reset
        ? supplierInvoiceDistributionService.reset(invoiceId, input)
        : supplierInvoiceDistributionService.save(invoiceId, input));
      apply(result);
      setNotice(
        reset ? 'Default distribution restored.' : 'Distribution saved.'
      );
      return true;
    } catch (cause) {
      setError(
        getProcurementProblemMessage(
          cause,
          'Unable to save distribution. Your edits have been retained.'
        )
      );
      return false;
    } finally {
      inFlight.current = false;
      setSaving(false);
    }
  };

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => {
          setFullPage(false);
          setOpen(true);
        }}
      >
        <Columns3 className="mr-1 h-4 w-4" />
        Distribution
      </Button>
      <Dialog open={open} onOpenChange={requestClose}>
        <DialogContent
          className={`flex flex-col overflow-hidden ${fullPage ? 'h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-none' : 'h-[min(680px,90dvh)] w-[min(1100px,calc(100vw-2rem))] max-w-none'}`}
        >
          <DialogHeader className="shrink-0 pr-8">
            <div className="flex items-center gap-3">
              <DialogTitle>Invoice Distribution</DialogTitle>
              {data && <Badge variant="outline">{data.status}</Badge>}
              <Button
                variant="ghost"
                size="sm"
                className="ml-auto"
                onClick={() => setFullPage((value) => !value)}
              >
                {fullPage ? (
                  <Minimize2 className="mr-1 h-4 w-4" />
                ) : (
                  <Maximize2 className="mr-1 h-4 w-4" />
                )}
                {fullPage ? 'Restore' : 'Full page'}
              </Button>
            </div>
            <DialogDescription>
              {data
                ? `${data.currency} · ${data.journalEntryNumber || data.basis}`
                : 'Invoice GL accounts and amounts.'}
            </DialogDescription>
          </DialogHeader>
          <div className="min-h-0 flex-1 overflow-auto">
            {loading && (
              <div role="status" className="flex items-center gap-2 p-4">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading distribution…
              </div>
            )}
            {(error || accountError) && (
              <div
                role="alert"
                className="mb-2 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800"
              >
                {error || accountError}
                <Button
                  variant="outline"
                  size="sm"
                  disabled={saving}
                  className="ml-3"
                  onClick={reload}
                >
                  {data ? 'Reload' : 'Retry'}
                </Button>
              </div>
            )}
            {notice && (
              <p role="status" className="mb-2 text-sm text-green-700">
                {notice}
              </p>
            )}
            {data?.editBlockReason && (
              <p role="status" className="mb-2 rounded border p-3 text-sm">
                {data.editBlockReason}
              </p>
            )}
            {data?.needsReview && (
              <p role="alert" className="mb-2 text-sm text-amber-800">
                The saved distribution needs review. Current invoice defaults
                are shown; review and save them before posting.
              </p>
            )}
            {canEdit && (
              <div className="mb-3 flex items-center gap-3">
                <p className="mr-auto text-xs text-muted-foreground">
                  Split invoice amounts between eligible accounts. Control, tax,
                  receipt and budget accounts retain their source account.
                  Saving does not post.
                </p>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={saving || loading || lines.length >= 1000}
                  onClick={add}
                >
                  <Plus className="mr-1 h-4 w-4" />
                  Add line
                </Button>
              </div>
            )}
            {data && (
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-muted">
                  <tr>
                    {[
                      'Account',
                      'Description',
                      'Posting purpose',
                      'Debit',
                      'Credit',
                      ...(canEdit ? ['Actions'] : []),
                    ].map((label) => (
                      <th
                        key={label}
                        className={`px-2 py-2 text-left font-medium ${label === 'Debit' ? 'text-green-700 dark:text-green-400' : label === 'Credit' ? 'text-red-700 dark:text-red-400' : ''}`}
                      >
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {lines.map((row, index) => {
                    const group = groups.find(
                      (value) => value.groupId === row.groupId
                    );
                    return (
                      <tr key={row.lineId} className="border-b">
                        <td className="min-w-[230px] px-2 py-2">
                          {canEdit && group?.canChangeAccount ? (
                            <>
                              <label
                                className="sr-only"
                                htmlFor={`distribution-account-${index}`}
                              >
                                Account {index + 1}
                              </label>
                              <PostingAccountPicker
                                id={`distribution-account-${index}`}
                                value={row.accountId}
                                accounts={accounts}
                                allowClear={false}
                                disabled={
                                  saving || loading || Boolean(accountError)
                                }
                                invalidValueLabel={`${row.accountCode} — ${row.accountName}`}
                                onChange={(value) => {
                                  const account = accounts.find(
                                    (item) => item.id === value
                                  );
                                  if (account)
                                    change(row.lineId, {
                                      accountId: account.id,
                                      accountCode:
                                        account.accountNumber ||
                                        account.accountCode,
                                      accountName: account.accountName,
                                    });
                                }}
                              />
                            </>
                          ) : (
                            <span title={group?.accountRestriction}>
                              {row.accountCode}
                            </span>
                          )}
                        </td>
                        <td className="px-2 py-2" title={row.description}>
                          {row.accountName}
                        </td>
                        <td className="max-w-[250px] px-2 py-2">
                          {canEdit ? (
                            <select
                              aria-label={`Posting purpose ${index + 1}`}
                              className="w-full rounded border bg-background p-2"
                              value={row.groupId}
                              disabled={saving}
                              onChange={(event) => {
                                const next = groups.find(
                                  (value) =>
                                    value.groupId === event.target.value
                                )!;
                                const original = data.lines.find(
                                  (value) => value.groupId === next.groupId
                                );
                                change(row.lineId, {
                                  groupId: next.groupId,
                                  type: next.type,
                                  accountId: next.accountId,
                                  accountCode: original?.accountCode ?? '',
                                  accountName: original?.accountName ?? '',
                                  description: next.description ?? '',
                                });
                              }}
                            >
                              {groups.map((value) => (
                                <option
                                  key={value.groupId}
                                  value={value.groupId}
                                >
                                  {value.type} · {value.description}
                                </option>
                              ))}
                            </select>
                          ) : (
                            row.type
                          )}
                        </td>
                        {(['debit', 'credit'] as const).map((side) => (
                          <td
                            key={side}
                            className={`px-2 py-2 text-right font-medium tabular-nums ${side === 'debit' ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400'}`}
                          >
                            {canEdit ? (
                              <Input
                                aria-label={`${side === 'debit' ? 'Debit' : 'Credit'} ${index + 1}`}
                                className={`w-28 text-right font-semibold ${side === 'debit' ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400'}`}
                                type="number"
                                min="0"
                                step="0.01"
                                value={row[side]}
                                disabled={saving}
                                onChange={(event) =>
                                  change(row.lineId, {
                                    [side]: event.target.value,
                                  })
                                }
                              />
                            ) : (
                              amount(Number(row[side]))
                            )}
                          </td>
                        ))}
                        {canEdit && (
                          <td className="whitespace-nowrap px-2 py-2">
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Split line ${index + 1}`}
                              disabled={
                                saving ||
                                lines.length >= 1000 ||
                                cents(row.debit) + cents(row.credit) < 2
                              }
                              onClick={() => split(row)}
                            >
                              <CopyPlus className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Remove line ${index + 1}`}
                              disabled={saving}
                              onClick={() =>
                                update(
                                  lines.filter(
                                    (value) => value.lineId !== row.lineId
                                  )
                                )
                              }
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </td>
                        )}
                      </tr>
                    );
                  })}
                  {!lines.length && (
                    <tr>
                      <td colSpan={canEdit ? 6 : 5} className="p-4 text-center">
                        No invoice distribution lines.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            )}
          </div>
          <DialogFooter className="shrink-0 flex-wrap items-center gap-2 border-t pt-3">
            {data && (
              <div className="mr-auto flex flex-wrap items-center gap-3 text-xs tabular-nums">
                <span className="font-semibold text-green-700 dark:text-green-400">
                  Debit {amount(debit / 100)}
                </span>
                <span className="font-semibold text-red-700 dark:text-red-400">
                  Credit {amount(credit / 100)} {data.currency}
                </span>
                <span
                  className={difference ? 'text-red-700' : 'text-green-700'}
                >
                  {difference
                    ? `Difference ${amount(Math.abs(difference) / 100)}`
                    : 'Balanced'}
                </span>
                {dirty && <span>Unsaved changes</span>}
              </div>
            )}
            {canEdit && (
              <>
                <Button
                  variant="outline"
                  disabled={saving || loading}
                  onClick={() => setConfirm('reset')}
                >
                  Reset to defaults
                </Button>
                <Button
                  disabled={
                    saving ||
                    loading ||
                    Boolean(accountError) ||
                    (!dirty && !data?.needsReview)
                  }
                  onClick={() => void save()}
                >
                  {saving ? 'Saving…' : 'Save distribution'}
                </Button>
              </>
            )}
            <Button
              variant="outline"
              disabled={saving}
              onClick={() => requestClose(false)}
            >
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <ConfirmationDialog
        open={confirm !== null}
        onOpenChange={(value) => {
          if (!value) setConfirm(null);
        }}
        title={
          confirm === 'reset'
            ? 'Reset distribution?'
            : 'Discard unsaved changes?'
        }
        description={
          confirm === 'reset'
            ? 'Replace the saved account splits with current invoice defaults.'
            : 'Your unsaved distribution edits will be discarded.'
        }
        confirmText={
          confirm === 'reset' ? 'Reset distribution' : 'Discard changes'
        }
        isLoading={saving}
        onConfirm={async () => {
          if (confirm === 'reset') return await save(true);
          if (confirm === 'reload') setRetry((value) => value + 1);
          else setOpen(false);
          setDirty(false);
          return true;
        }}
      >
        {confirm === 'reset' && error && (
          <p role="alert" className="text-sm text-red-700">
            {error}
          </p>
        )}
      </ConfirmationDialog>
    </>
  );
}
