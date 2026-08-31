'use client';

import { useEffect, useMemo, useState } from 'react';
import { CheckCircle2, FileCheck2, Loader2, Send, ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type {
  FixedAsset,
  FixedAssetCategory,
  SubmitFixedAssetCapitalizationDto,
} from '@/types/fixed-assets';
import type { Account } from '@/types/finance';

const today = () => new Date().toISOString().slice(0, 10);
const toDate = (value?: string) => value ? value.slice(0, 10) : '';
const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(value);
const compactHash = (value?: string) => value ? `${value.slice(0, 12)}…${value.slice(-8)}` : '—';

interface Props {
  asset: FixedAsset;
  category?: FixedAssetCategory;
  onChanged: () => Promise<void> | void;
}

/**
 * Captures the exact journal proposal before maker-checker review. Once submitted, the panel
 * becomes read-only and posting calls contain no accounting values: the API reconstructs the
 * journal exclusively from the approved, hash-protected snapshot.
 */
export function FixedAssetCapitalizationApprovalPanel({ asset, category, onChanged }: Props) {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('Finance.FixedAssets.Manage');
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [busy, setBusy] = useState<'submit' | 'post'>();
  const [form, setForm] = useState<SubmitFixedAssetCapitalizationDto>({
    capitalizationDate: today(),
    amount: asset.acquisitionCost,
    reference: asset.assetCode,
    reason: '',
    comments: '',
    transactionCurrencyCode: asset.transactionCurrencyCode || asset.functionalCurrencyCode,
    exchangeRate: asset.exchangeRate,
    exchangeRateId: asset.exchangeRateId,
    exchangeRateDate: toDate(asset.exchangeRateDate),
  });

  useEffect(() => {
    let active = true;
    financeDataService.getAccounts({ status: 'Active', pageSize: 10000 })
      .then(items => {
        if (active) {
          setAccounts(items.filter(account =>
            ['Asset', 'Liability', 'Equity'].includes(account.accountType) &&
            account.allowDirectPosting));
        }
      })
      .catch(error => {
        toast({
          title: 'Could not load capitalization accounts',
          description: error instanceof Error ? error.message : 'Account options are unavailable.',
          variant: 'destructive',
        });
      });
    return () => { active = false; };
  }, [toast]);

  const snapshot = asset.capitalizationApprovalSnapshot;
  const accountLabels = useMemo(
    () => new Map(accounts.map(account => [
      account.id,
      `${account.accountNumber || account.accountCode} - ${account.accountName}`,
    ])),
    [accounts]
  );
  const sourceOwned = snapshot && snapshot.sourceDocumentType !== 'FixedAsset';
  const approved = !!snapshot && !!asset.capitalizationApprovalApprovedAt && !asset.capitalizationApprovalInvalidatedAt;
  const canSubmit = canManage && (!snapshot || !!asset.capitalizationApprovalInvalidatedAt) &&
    (asset.status === 'Draft' || asset.status === 'Acquired');
  const canPost = canManage && approved && asset.status === 'Acquired';

  if (asset.postingEventId && !asset.capitalizationReversalPostingEventId) return null;

  const submit = async () => {
    if (!form.capitalizationDate || !form.amount || form.amount <= 0 || form.reason.trim().length < 5) {
      toast({
        title: 'Complete the approval proposal',
        description: 'Enter a date, positive amount, and substantive reason before submission.',
        variant: 'destructive',
      });
      return;
    }
    try {
      setBusy('submit');
      await fixedAssetsDataService.submitCapitalizationForApproval(asset.id, {
        ...form,
        creditAccountId: form.creditAccountId || undefined,
        reference: form.reference?.trim() || asset.assetCode,
        reason: form.reason.trim(),
        comments: form.comments?.trim() || undefined,
        exchangeRateDate: form.exchangeRateDate || undefined,
      });
      toast({
        title: 'Capitalization submitted',
        description: 'The date, amount, accounts, currency, rate, reference, and source evidence are now frozen for review.',
      });
      await onChanged();
    } catch (error) {
      toast({ title: 'Submission failed', description: error instanceof Error ? error.message : 'The proposal could not be submitted.', variant: 'destructive' });
    } finally {
      setBusy(undefined);
    }
  };

  const post = async () => {
    try {
      setBusy('post');
      await fixedAssetsDataService.postApprovedCapitalization(asset.id, {
        comments: 'Post the independently approved capitalization snapshot.',
      });
      toast({
        title: 'Capitalization posted',
        description: 'The fixed-asset register and linked GL journal now reflect the approved evidence.',
      });
      await onChanged();
    } catch (error) {
      toast({ title: 'Posting failed', description: error instanceof Error ? error.message : 'The approved proposal could not be posted.', variant: 'destructive' });
    } finally {
      setBusy(undefined);
    }
  };

  return (
    <Card className="border-blue-200 bg-blue-50/30">
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2 text-base">
              <ShieldCheck className="h-5 w-5 text-blue-700" />
              Capitalization approval evidence
            </CardTitle>
            <CardDescription>
              Management approval covers the exact journal proposal shown here. Approved values cannot be replaced at posting.
            </CardDescription>
          </div>
          <Badge variant={approved ? 'default' : asset.status === 'Rejected' ? 'destructive' : 'secondary'}>
            {approved ? 'Approved snapshot' : asset.status === 'PendingApproval' ? 'Pending review' : asset.status}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        {asset.capitalizationApprovalInvalidatedAt && (
          <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
            Previous approval retired: {asset.capitalizationApprovalInvalidationReason || 'the accounting evidence changed after approval'}.
            Save the corrected asset, then submit a new proposal.
          </div>
        )}

        {asset.status === 'Rejected' && !snapshot && (
          <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
            This proposal was rejected. Revise and save the asset first; saving resets it to Draft before a new proposal can be submitted.
          </div>
        )}

        {canSubmit && (
          <>
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="capitalizationApprovalDate">Capitalization date *</Label>
                <Input id="capitalizationApprovalDate" type="date" value={form.capitalizationDate}
                  onChange={event => setForm({ ...form, capitalizationDate: event.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="capitalizationApprovalAmount">Transaction amount *</Label>
                <Input id="capitalizationApprovalAmount" type="number" min="0.01" step="0.01"
                  value={form.amount ?? ''}
                  onChange={event => setForm({ ...form, amount: event.target.value ? Number(event.target.value) : undefined })} />
                <p className="text-xs text-muted-foreground">Currency: {form.transactionCurrencyCode || asset.functionalCurrencyCode}</p>
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="capitalizationCreditAccount">Credit / clearing account</Label>
                <Select value={form.creditAccountId || 'category-default'}
                  onValueChange={value => setForm({ ...form, creditAccountId: value === 'category-default' ? undefined : value })}>
                  <SelectTrigger id="capitalizationCreditAccount" className="min-w-0">
                    <SelectValue placeholder="Use category AUC/CIP account" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="category-default">
                      Category AUC/CIP default{category?.aucAccountId ? ` — ${accountLabels.get(category.aucAccountId) || category.aucAccountId}` : ''}
                    </SelectItem>
                    {accounts.map(account => (
                      <SelectItem key={account.id} value={account.id}>
                        {accountLabels.get(account.id)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">The selected account and the category asset-cost account are resolved and frozen at submission.</p>
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="capitalizationReference">Reference</Label>
                <Input id="capitalizationReference" maxLength={100} value={form.reference || ''}
                  onChange={event => setForm({ ...form, reference: event.target.value })} />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="capitalizationReason">Reason *</Label>
                <Textarea id="capitalizationReason" maxLength={1000} value={form.reason}
                  onChange={event => setForm({ ...form, reason: event.target.value })}
                  placeholder="Explain why this asset and exact amount should be capitalized." />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="capitalizationComments">Submission comment</Label>
                <Textarea id="capitalizationComments" value={form.comments || ''}
                  onChange={event => setForm({ ...form, comments: event.target.value })}
                  placeholder="Point the checker to supporting documents or evidence." />
              </div>
            </div>
            <div className="flex items-center justify-between gap-3 rounded-md border bg-white p-3">
              <p className="text-sm text-muted-foreground">Save any asset edits before submitting; submission reads the persisted register evidence.</p>
              <Button onClick={submit} disabled={!!busy}>
                {busy === 'submit' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
                Submit exact proposal
              </Button>
            </div>
          </>
        )}

        {snapshot && (
          <>
            <div className="grid grid-cols-1 gap-3 rounded-md border bg-white p-4 text-sm md:grid-cols-3">
              <Evidence label="Source" value={`${snapshot.sourceDocumentType} · ${snapshot.reference}`} />
              <Evidence label="Capitalization date" value={new Date(snapshot.capitalizationDate).toLocaleDateString()} />
              <Evidence label="Approved amount" value={money(snapshot.transactionAmount, snapshot.transactionCurrencyCode)} />
              <Evidence label="Debit account" value={accountLabels.get(snapshot.debitAccountId) || snapshot.debitAccountId} />
              <Evidence label="Credit account" value={accountLabels.get(snapshot.creditAccountId) || snapshot.creditAccountId} />
              <Evidence label="Exchange-rate evidence" value={snapshot.exchangeRateId ? `${snapshot.exchangeRate} · ${snapshot.exchangeRateId}` : `${snapshot.exchangeRate} (functional currency)`} />
              <Evidence label="Reason" value={snapshot.reason} className="md:col-span-2" />
              <Evidence label="Integrity hash" value={compactHash(asset.capitalizationApprovalSnapshotHash)} />
            </div>
            {sourceOwned && (
              <p className="text-sm text-muted-foreground">This proposal came through the {snapshot.sourceDocumentType} Finance adapter. Its source identifiers are retained in the approval evidence.</p>
            )}
            {asset.status === 'PendingApproval' && (
              <div className="flex items-center gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                <FileCheck2 className="h-4 w-4" /> Waiting for an independent decision in Finance Approvals.
              </div>
            )}
            {canPost && (
              <div className="flex items-center justify-between gap-3 rounded-md border border-emerald-200 bg-emerald-50 p-3">
                <div className="flex items-center gap-2 text-sm text-emerald-900">
                  <CheckCircle2 className="h-4 w-4" /> Approved evidence is locked and ready to post.
                </div>
                <Button onClick={post} disabled={!!busy} className="bg-emerald-700 hover:bg-emerald-800">
                  {busy === 'post' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <CheckCircle2 className="mr-2 h-4 w-4" />}
                  Post approved capitalization
                </Button>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Evidence({ label, value, className = '' }: { label: string; value: string; className?: string }) {
  return (
    <div className={`min-w-0 ${className}`}>
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="break-words font-medium">{value}</p>
    </div>
  );
}
