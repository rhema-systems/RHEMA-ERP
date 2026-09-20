'use client';

import React, { useEffect, useRef, useState } from 'react';
import {
  Columns3,
  CopyPlus,
  Loader2,
  Maximize2,
  Minimize2,
  Plus,
  RotateCcw,
  Save,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
  purchaseReceiptDistributionService,
  type PurchaseReceiptDistribution as Distribution,
  type PurchaseReceiptDistributionAccount,
  type PurchaseReceiptDistributionLine,
  type ReceiptDistributionPurpose,
  type SavePurchaseReceiptDistribution,
} from '@/services/purchaseReceiptDistributionService';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

type EditableLine = Omit<
  PurchaseReceiptDistributionLine,
  'debit' | 'credit' | 'lineId'
> & {
  lineId: string;
  debit: string;
  credit: string;
};
type SourceOption = {
  inventoryItemId: string;
  purpose: ReceiptDistributionPurpose;
  itemCode?: string;
  itemName?: string;
  accountId: string;
};

const amount = (value: number) =>
  value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
const cents = (value: string | number) => Math.round(Number(value || 0) * 100);
const toEditableLines = (
  lines: PurchaseReceiptDistributionLine[]
): EditableLine[] =>
  lines.map((line) => ({
    ...line,
    lineId: line.lineId || crypto.randomUUID(),
    debit: String(line.debit),
    credit: String(line.credit),
  }));
const sourceKey = (
  line: Pick<PurchaseReceiptDistributionLine, 'inventoryItemId' | 'purpose'>
) => `${line.inventoryItemId}:${line.purpose}`;
const purposeLabel = (purpose?: string) =>
  purpose === 'AccruedPurchases'
    ? 'Accrued purchases'
    : purpose === 'PurchasePriceVariance'
      ? 'Price variance'
      : purpose || '';
const sourceLabel = (line: SourceOption) =>
  `${line.itemCode || line.itemName || 'Item'} · ${purposeLabel(line.purpose)}`;

export function PurchaseReceiptDistribution({
  receiptId,
}: {
  receiptId: string;
}) {
  const [open, setOpen] = useState(false);
  const [fullPage, setFullPage] = useState(false);
  const [data, setData] = useState<Distribution | null>(null);
  const [lines, setLines] = useState<EditableLine[]>([]);
  const [accounts, setAccounts] = useState<
    PurchaseReceiptDistributionAccount[]
  >([]);
  const [error, setError] = useState('');
  const [accountError, setAccountError] = useState('');
  const [notice, setNotice] = useState('');
  const [loading, setLoading] = useState(false);
  const [accountsLoading, setAccountsLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [retry, setRetry] = useState(0);
  const [accountRetry, setAccountRetry] = useState(0);
  const [confirm, setConfirm] = useState<'close' | 'reset' | null>(null);
  const mutationInFlight = useRef(false);

  const applyResult = (result: Distribution) => {
    setData(result);
    setLines(toEditableLines(result.lines));
    setDirty(false);
  };

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError('');
    setAccountError('');
    setNotice('');
    setData(null);
    setAccounts([]);
    setDirty(false);
    purchaseReceiptDistributionService
      .get(receiptId)
      .then((result) => {
        if (!cancelled) applyResult(result);
      })
      .catch((cause) => {
        if (!cancelled)
          setError(
            getProcurementProblemMessage(
              cause,
              'Unable to load the distribution.'
            )
          );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, receiptId, retry]);

  const canEdit = Boolean(data?.canEdit && data.status !== 'Posted');
  useEffect(() => {
    if (!open || !canEdit) return;
    let cancelled = false;
    setAccountsLoading(true);
    setAccountError('');
    purchaseReceiptDistributionService
      .accounts(receiptId)
      .then((result) => {
        if (!cancelled) setAccounts(result);
      })
      .catch((cause) => {
        if (!cancelled)
          setAccountError(
            getProcurementProblemMessage(
              cause,
              'Unable to load receipt posting accounts.'
            )
          );
      })
      .finally(() => {
        if (!cancelled) setAccountsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, canEdit, receiptId, accountRetry]);

  const sourceOptions: SourceOption[] = data?.groups
    ? data.groups.map((group) => ({
        ...group,
        accountId: group.defaultAccountId,
      }))
    : [
        ...new Map(
          (data?.lines ?? [])
            .filter(
              (
                line
              ): line is PurchaseReceiptDistributionLine &
                Pick<SourceOption, 'inventoryItemId' | 'purpose'> =>
                Boolean(line.inventoryItemId && line.purpose)
            )
            .map((line) => [
              sourceKey(line),
              {
                inventoryItemId: line.inventoryItemId,
                purpose: line.purpose,
                itemCode: line.itemCode,
                itemName: line.itemName,
                accountId: line.accountId,
              },
            ])
        ).values(),
      ];
  const totalDebit = lines.reduce(
    (total, line) =>
      total + (Number.isFinite(Number(line.debit)) ? cents(line.debit) : 0),
    0
  );
  const totalCredit = lines.reduce(
    (total, line) =>
      total + (Number.isFinite(Number(line.credit)) ? cents(line.credit) : 0),
    0
  );
  const difference = totalDebit - totalCredit;
  const updateLines = (next: EditableLine[]) => {
    setLines(next);
    setDirty(true);
    setNotice('');
    setError('');
  };
  const updateLine = (lineId: string, changes: Partial<EditableLine>) =>
    updateLines(
      lines.map((line) =>
        line.lineId === lineId ? { ...line, ...changes } : line
      )
    );
  const requestClose = (next: boolean) => {
    if (next) {
      setOpen(true);
      return;
    }
    if (saving) return;
    if (dirty) setConfirm('close');
    else setOpen(false);
  };
  const addLine = () => {
    const template = sourceOptions[0];
    if (!template) return;
    const account = accounts.find((item) => item.id === template.accountId);
    updateLines([
      ...lines,
      {
        ...template,
        lineId: crypto.randomUUID(),
        debit: '0',
        credit: '0',
        type: purposeLabel(template.purpose),
        accountCode: account?.accountNumber || account?.accountCode || '',
        accountName: account?.accountName || 'Saved account',
        source: 'Receipt override',
      },
    ]);
  };
  const splitLine = (lineId: string) => {
    const index = lines.findIndex((line) => line.lineId === lineId);
    if (index < 0) return;
    const original = lines[index];
    const splitDebit = Math.floor(cents(original.debit) / 2);
    const splitCredit = Math.floor(cents(original.credit) / 2);
    const next = [...lines];
    next.splice(
      index,
      1,
      {
        ...original,
        debit: String((cents(original.debit) - splitDebit) / 100),
        credit: String((cents(original.credit) - splitCredit) / 100),
      },
      {
        ...original,
        lineId: crypto.randomUUID(),
        debit: String(splitDebit / 100),
        credit: String(splitCredit / 100),
        source: 'Receipt override',
      }
    );
    updateLines(next);
  };

  const save = async () => {
    if (!data || !canEdit || mutationInFlight.current) return;
    setError('');
    setNotice('');
    if (!data.version || !data.basisVersion) {
      setError(
        'This receipt version is unavailable. Close and reopen Distribution before saving.'
      );
      return;
    }
    if (!lines.length) {
      setError('Add at least one distribution line.');
      return;
    }
    const invalidIndex = lines.findIndex(
      (line) =>
        !line.inventoryItemId ||
        !line.purpose ||
        !line.accountId ||
        !Number.isFinite(Number(line.debit)) ||
        !Number.isFinite(Number(line.credit)) ||
        Number(line.debit) < 0 ||
        Number(line.credit) < 0 ||
        (cents(line.debit) > 0 && cents(line.credit) > 0) ||
        (cents(line.debit) === 0 && cents(line.credit) === 0)
    );
    if (invalidIndex >= 0) {
      setError(
        `Line ${invalidIndex + 1}: select an item and account, then enter a positive debit or credit, not both.`
      );
      return;
    }
    if (difference !== 0) {
      setError(
        `Distribution is out of balance by ${amount(Math.abs(difference) / 100)} ${data.currency}. Debit and credit must match.`
      );
      return;
    }
    const input: SavePurchaseReceiptDistribution = {
      version: data.version,
      basisVersion: data.basisVersion,
      lines: lines.map((line) => ({
        lineId: line.lineId,
        inventoryItemId: line.inventoryItemId as string,
        purpose: line.purpose as ReceiptDistributionPurpose,
        accountId: line.accountId,
        debit: Number(line.debit),
        credit: Number(line.credit),
      })),
    };
    mutationInFlight.current = true;
    setSaving(true);
    try {
      applyResult(
        await purchaseReceiptDistributionService.save(receiptId, input)
      );
      setNotice('Distribution saved.');
    } catch (cause) {
      setError(
        getProcurementProblemMessage(cause, 'Unable to save the distribution.')
      );
    } finally {
      mutationInFlight.current = false;
      setSaving(false);
    }
  };

  const reset = async () => {
    if (
      !data ||
      !canEdit ||
      !data.version ||
      !data.basisVersion ||
      mutationInFlight.current
    )
      return false;
    mutationInFlight.current = true;
    setSaving(true);
    setError('');
    setNotice('');
    try {
      applyResult(
        await purchaseReceiptDistributionService.reset(receiptId, {
          version: data.version,
          basisVersion: data.basisVersion,
        })
      );
      setNotice('Default distribution restored.');
      return true;
    } catch (cause) {
      setError(
        getProcurementProblemMessage(cause, 'Unable to reset the distribution.')
      );
      setConfirm(null);
      return false;
    } finally {
      mutationInFlight.current = false;
      setSaving(false);
    }
  };

  return (
    <>
      <Button
        variant="outline"
        onClick={() => {
          setFullPage(false);
          setOpen(true);
        }}
      >
        <Columns3 className="mr-2 h-4 w-4" />
        Distribution
      </Button>
      <Dialog open={open} onOpenChange={requestClose}>
        <DialogContent
          className={`flex flex-col overflow-hidden ${fullPage ? 'h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-none' : 'h-[min(620px,90dvh)] w-[min(960px,calc(100vw-2rem))] max-w-none'}`}
        >
          <DialogHeader className="shrink-0 pr-8">
            <div className="flex items-center gap-3">
              <DialogTitle>Distribution</DialogTitle>
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
                ? `${data.currency} · ${data.journalEntryNumber ?? data.basis}`
                : 'Receipt GL accounts and amounts.'}
            </DialogDescription>
          </DialogHeader>
          {data?.editBlockReason && !canEdit && (
            <p className="text-sm text-muted-foreground">
              {data.editBlockReason}
            </p>
          )}
          {data?.needsReview && (
            <p
              role="alert"
              className="rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900"
            >
              Receipt quantities or posting defaults changed. Review the
              distribution before saving.
            </p>
          )}
          {canEdit && (
            <div className="flex shrink-0 items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={addLine}
                disabled={saving || !sourceOptions.length}
              >
                <Plus className="mr-1 h-4 w-4" />
                Add line
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setConfirm('reset')}
                disabled={
                  saving ||
                  !data?.version ||
                  (!dirty && !data?.hasOverrides && !data?.needsReview)
                }
              >
                <RotateCcw className="mr-1 h-4 w-4" />
                Reset defaults
              </Button>
              {dirty && (
                <span className="ml-auto text-xs text-amber-700">
                  Unsaved changes
                </span>
              )}
            </div>
          )}
          <div className="min-h-0 flex-1 overflow-auto">
            {loading && (
              <div role="status" className="flex items-center gap-2 p-4">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading distribution…
              </div>
            )}
            {error && (
              <div
                role="alert"
                className="mb-2 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800"
              >
                {error}
                {!data && (
                  <Button
                    variant="outline"
                    size="sm"
                    className="ml-3"
                    onClick={() => setRetry((value) => value + 1)}
                  >
                    Retry
                  </Button>
                )}
              </div>
            )}
            {accountError && (
              <div
                role="alert"
                className="mb-2 rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"
              >
                {accountError}
                <Button
                  variant="outline"
                  size="sm"
                  className="ml-3"
                  onClick={() => setAccountRetry((value) => value + 1)}
                >
                  Retry accounts
                </Button>
              </div>
            )}
            {notice && (
              <p role="status" className="mb-2 text-sm text-green-700">
                {notice}
              </p>
            )}
            {data && (
              <table className="w-full text-xs">
                <thead className="sticky top-0 z-10 bg-muted">
                  <tr>
                    {(canEdit
                      ? ['Item / Type', 'Account', 'Debit', 'Credit', '']
                      : [
                          'Account',
                          'Description',
                          'Type',
                          'Default source',
                          'Debit',
                          'Credit',
                        ]
                    ).map((label, index) => (
                      <th
                        key={`${label}-${index}`}
                        className={`px-2 py-2 font-medium ${label === 'Debit' || label === 'Credit' ? 'text-right' : 'text-left'}`}
                      >
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {lines.map((line, index) => (
                    <tr key={line.lineId} className="border-b">
                      {canEdit ? (
                        <>
                          <td className="min-w-[190px] px-2 py-1.5">
                            <Select
                              value={sourceKey(line)}
                              disabled={saving}
                              onValueChange={(key) => {
                                const source = sourceOptions.find(
                                  (item) => sourceKey(item) === key
                                );
                                if (source)
                                  updateLine(line.lineId, {
                                    inventoryItemId: source.inventoryItemId,
                                    purpose: source.purpose,
                                    itemCode: source.itemCode,
                                    itemName: source.itemName,
                                    type: purposeLabel(source.purpose),
                                  });
                              }}
                            >
                              <SelectTrigger
                                className="h-9 text-xs"
                                aria-label={`Item and type line ${index + 1}`}
                              >
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {sourceOptions.map((source) => (
                                  <SelectItem
                                    key={sourceKey(source)}
                                    value={sourceKey(source)}
                                  >
                                    {sourceLabel(source)}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </td>
                          <td className="min-w-[260px] px-2 py-1.5">
                            <Label
                              htmlFor={`receipt-account-${line.lineId}`}
                              className="sr-only"
                            >
                              Account line {index + 1}
                            </Label>
                            <PostingAccountPicker
                              id={`receipt-account-${line.lineId}`}
                              value={line.accountId}
                              accounts={accounts.map((account) => ({
                                ...account,
                                accountNumber:
                                  account.accountNumber || account.accountCode,
                              }))}
                              allowClear={false}
                              disabled={
                                saving ||
                                accountsLoading ||
                                Boolean(accountError)
                              }
                              invalidValueLabel={`${line.accountCode} — ${line.accountName}`}
                              onChange={(id) => {
                                const account = accounts.find(
                                  (item) => item.id === id
                                );
                                if (account)
                                  updateLine(line.lineId, {
                                    accountId: account.id,
                                    accountCode:
                                      account.accountNumber ||
                                      account.accountCode,
                                    accountName: account.accountName,
                                  });
                              }}
                            />
                          </td>
                          <td className="w-[112px] min-w-[100px] px-2 py-1.5">
                            <Input
                              aria-label={`Debit line ${index + 1}`}
                              className="h-9 text-right text-xs tabular-nums"
                              type="number"
                              min="0"
                              step="0.01"
                              disabled={saving}
                              value={line.debit}
                              onChange={(event) =>
                                updateLine(line.lineId, {
                                  debit: event.target.value,
                                })
                              }
                            />
                          </td>
                          <td className="w-[112px] min-w-[100px] px-2 py-1.5">
                            <Input
                              aria-label={`Credit line ${index + 1}`}
                              className="h-9 text-right text-xs tabular-nums"
                              type="number"
                              min="0"
                              step="0.01"
                              disabled={saving}
                              value={line.credit}
                              onChange={(event) =>
                                updateLine(line.lineId, {
                                  credit: event.target.value,
                                })
                              }
                            />
                          </td>
                          <td className="whitespace-nowrap px-1 py-1.5">
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-8 w-8"
                              title="Split line"
                              aria-label={`Split line ${index + 1}`}
                              disabled={saving}
                              onClick={() => splitLine(line.lineId)}
                            >
                              <CopyPlus className="h-4 w-4" />
                            </Button>
                            <Button
                              size="icon"
                              variant="ghost"
                              className="h-8 w-8 text-red-600"
                              title="Remove line"
                              aria-label={`Remove line ${index + 1}`}
                              disabled={saving}
                              onClick={() =>
                                updateLines(
                                  lines.filter(
                                    (item) => item.lineId !== line.lineId
                                  )
                                )
                              }
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </td>
                        </>
                      ) : (
                        <>
                          <td className="whitespace-nowrap px-2 py-2">
                            {line.accountCode}
                          </td>
                          <td className="px-2 py-2">{line.accountName}</td>
                          <td className="px-2 py-2">{line.type}</td>
                          <td className="px-2 py-2">{line.source}</td>
                          <td className="px-2 py-2 text-right tabular-nums">
                            {amount(Number(line.debit))}
                          </td>
                          <td className="px-2 py-2 text-right tabular-nums">
                            {amount(Number(line.credit))}
                          </td>
                        </>
                      )}
                    </tr>
                  ))}
                  {!lines.length && (
                    <tr>
                      <td
                        colSpan={canEdit ? 5 : 6}
                        className="p-4 text-center text-muted-foreground"
                      >
                        {canEdit
                          ? 'Add a distribution line or reset defaults.'
                          : 'No stock distribution for the current receipt quantities.'}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            )}
          </div>
          <DialogFooter className="shrink-0 flex-wrap items-center gap-2 border-t pt-3">
            {data && (
              <div className="mr-auto flex flex-wrap items-center gap-x-3 gap-y-1 text-xs tabular-nums">
                <span>Debit {amount(totalDebit / 100)}</span>
                <span>
                  Credit {amount(totalCredit / 100)} {data.currency}
                </span>
                <span
                  className={
                    difference ? 'font-medium text-red-700' : 'text-green-700'
                  }
                >
                  {difference
                    ? `Difference ${amount(Math.abs(difference) / 100)}`
                    : 'Balanced'}
                </span>
              </div>
            )}
            <Button
              variant="outline"
              disabled={saving}
              onClick={() => requestClose(false)}
            >
              Close
            </Button>
            {canEdit && (
              <Button
                disabled={saving || loading || (!dirty && !data?.needsReview)}
                onClick={save}
              >
                {saving ? (
                  <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                ) : (
                  <Save className="mr-1 h-4 w-4" />
                )}
                Save
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <ConfirmationDialog
        open={confirm !== null}
        onOpenChange={(value) => {
          if (!value && !saving) setConfirm(null);
        }}
        title={
          confirm === 'reset'
            ? 'Reset distribution?'
            : 'Discard unsaved changes?'
        }
        description={
          confirm === 'reset'
            ? 'Replace the current lines with the receipt posting defaults.'
            : 'Your unsaved distribution changes will be discarded.'
        }
        confirmText={confirm === 'reset' ? 'Reset defaults' : 'Discard changes'}
        isLoading={saving}
        onConfirm={async () => {
          if (confirm === 'reset') return reset();
          setDirty(false);
          setOpen(false);
          return true;
        }}
      />
    </>
  );
}
