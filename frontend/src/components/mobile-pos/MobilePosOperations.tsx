'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Download, Loader2, RefreshCw, ShieldOff } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  MobilePosDevice,
  MobilePosReferenceOption,
  MobilePosStore,
  MobilePosTill,
  MobilePosTillCloseSubmission,
  mobilePosAdminService,
} from '@/services/mobile-pos-admin.service';

const DEVICE_STATUS: Record<number, string> = { 1: 'Pending', 2: 'Active', 3: 'Suspended', 4: 'Revoked', 5: 'Retired' };
const TILL_CLOSE_STATUS: Record<number, string> = { 1: 'Ready for review', 2: 'Pending sync', 3: 'Sync exception resolved', 4: 'Finalized', 5: 'Returned for recount' };
const BANK_DEPOSIT_STATUS: Record<number, string> = { 1: 'Draft', 2: 'Submitted', 3: 'Approved', 4: 'Posted', 5: 'Returned', 6: 'Rejected', 7: 'Cancelled', 8: 'Reversed' };

const money = (value: number, currency = 'GHS') => `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const errorMessage = (error: unknown, fallback: string) => {
  const candidate = error as { response?: { data?: { detail?: string; title?: string; error?: string } | string }; message?: string };
  const data = candidate?.response?.data;
  if (typeof data === 'string' && data.trim()) return data;
  if (data && typeof data === 'object') return data.detail || data.error || data.title || fallback;
  return candidate?.message || fallback;
};

function PageHeader({ title, description, loading, onRefresh, action }: {
  title: string;
  description: string;
  loading: boolean;
  onRefresh: () => void;
  action?: React.ReactNode;
}) {
  return <div className="flex flex-col justify-between gap-3 md:flex-row md:items-center">
    <div>
      <p className="text-sm font-medium text-primary">Sales · Point of Sales</p>
      <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
      <p className="text-muted-foreground">{description}</p>
    </div>
    <div className="flex flex-wrap gap-2">
      {action}
      <Button variant="outline" onClick={onRefresh} disabled={loading}>
        <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />Refresh
      </Button>
    </div>
  </div>;
}

function LoadingState() {
  return <div className="flex min-h-60 items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-primary" /></div>;
}

function Section({ title, description, children }: { title: string; description: string; children: React.ReactNode }) {
  return <Card><CardHeader><CardTitle>{title}</CardTitle><CardDescription>{description}</CardDescription></CardHeader><CardContent>{children}</CardContent></Card>;
}

function Table({ headers, rows }: { headers: string[]; rows: React.ReactNode[][] }) {
  return <div className="overflow-x-auto rounded-md border"><table className="w-full text-sm"><thead className="bg-muted/50 text-left"><tr>{headers.map((header, index) => <th key={`${header}-${index}`} className="whitespace-nowrap px-4 py-3 font-medium">{header}</th>)}</tr></thead><tbody className="divide-y">{rows.map((row, rowIndex) => <tr key={rowIndex} className="align-top">{row.map((cell, cellIndex) => <td key={cellIndex} className="px-4 py-3">{cell}</td>)}</tr>)}{!rows.length && <tr><td colSpan={headers.length} className="px-4 py-10 text-center text-muted-foreground">No records yet.</td></tr>}</tbody></table></div>;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return <div className="space-y-2"><Label>{label}</Label>{children}</div>;
}

function SearchChoice({ label, value, options, onChange, required = false }: {
  label: string;
  value?: string;
  options: MobilePosReferenceOption[];
  onChange: (value: string) => void;
  required?: boolean;
}) {
  const [search, setSearch] = useState('');
  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase();
    return needle
      ? options.filter(option => `${option.code} ${option.name} ${option.secondary || ''}`.toLowerCase().includes(needle))
      : options;
  }, [options, search]);
  const selected = options.find(option => option.id === value);

  return <div className="space-y-2">
    <Label>{label}{required ? ' *' : ''}</Label>
    <div className="overflow-hidden rounded-md border">
      <div className="border-b p-2"><Input value={search} onChange={event => setSearch(event.target.value)} placeholder="Search by code or name" /></div>
      {selected && <div className="border-b bg-primary/5 px-3 py-2 text-sm"><span className="font-medium">Selected:</span> {selected.code} · {selected.name}</div>}
      <div className="max-h-36 overflow-y-auto p-1">
        {!required && <button type="button" className="w-full rounded px-2 py-2 text-left text-sm hover:bg-muted" onClick={() => onChange('')}>None</button>}
        {visible.map(option => <button key={option.id} type="button" onClick={() => onChange(option.id)} className={`w-full rounded px-2 py-2 text-left text-sm hover:bg-muted ${value === option.id ? 'bg-primary/10 text-primary' : ''}`}>
          <span className="font-medium">{option.code}</span> · {option.name}{option.secondary ? <span className="block text-xs text-muted-foreground">{option.secondary}</span> : null}
        </button>)}
        {!visible.length && <p className="px-2 py-3 text-sm text-muted-foreground">No matching options.</p>}
      </div>
    </div>
  </div>;
}

export function MobilePosDayEndWorkspace() {
  const { user, hasPermission } = useAuth();
  const canResolveSync = hasPermission('MobilePOS.Sync.Resolve');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [submissions, setSubmissions] = useState<MobilePosTillCloseSubmission[]>([]);
  const [reviewAction, setReviewAction] = useState<{ submission: MobilePosTillCloseSubmission; mode: 'approve' | 'return'; comments: string }>();
  const [syncResolution, setSyncResolution] = useState<{ submission: MobilePosTillCloseSubmission; reason: string }>();

  const load = useCallback(async () => {
    setLoading(true);
    try { setSubmissions(await mobilePosAdminService.tillCloseSubmissions()); }
    catch (error) { toast.error(errorMessage(error, 'Failed to load Mobile POS day-end submissions.')); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const runTillReview = async () => {
    if (!reviewAction) return false;
    const comments = reviewAction.comments.trim();
    if (reviewAction.mode === 'return' && comments.length < 10) {
      toast.error('Enter recount instructions of at least 10 characters.');
      return false;
    }
    setSaving(true);
    try {
      const input = { comments, rowVersion: reviewAction.submission.session.rowVersion };
      if (reviewAction.mode === 'approve') {
        await mobilePosAdminService.approveTillClose(reviewAction.submission.cashierTillSessionId, input);
        toast.success('Till close finalized through the Finance custody workflow.');
      } else {
        await mobilePosAdminService.returnTillClose(reviewAction.submission.cashierTillSessionId, input);
        toast.success('Till count returned to the cashier for recount.');
      }
      setReviewAction(undefined);
      await load();
      return true;
    } catch (error) {
      toast.error(errorMessage(error, 'The till review could not be completed.'));
      return false;
    } finally { setSaving(false); }
  };

  const resolveTillSync = async () => {
    if (!syncResolution) return false;
    const reason = syncResolution.reason.trim();
    if (reason.length < 20) {
      toast.error('Enter an independent sync resolution reason of at least 20 characters.');
      return false;
    }
    setSaving(true);
    try {
      await mobilePosAdminService.resolvePendingSync(syncResolution.submission.id, { reason, rowVersion: syncResolution.submission.rowVersion });
      toast.success('Pending-sync exception recorded for independent till review.');
      setSyncResolution(undefined);
      await load();
      return true;
    } catch (error) {
      toast.error(errorMessage(error, 'The pending-sync exception could not be resolved.'));
      return false;
    } finally { setSaving(false); }
  };

  return <div className="space-y-6 p-4 md:p-6">
    <PageHeader title="Day End" description="Review cash custody, variances, and retained offline synchronization evidence before Finance finalization." loading={loading} onRefresh={() => void load()} />
    {loading ? <LoadingState /> : <Section title="Till close review queue" description="A cashier cannot approve their own session. Pending synchronization must be resolved before final approval.">
      <Table headers={['Session', 'Cashier / date', 'Count and variance', 'Sync evidence', 'Actions']} rows={submissions.map(submission => {
        const ownSession = Boolean(user?.id && submission.session.cashierUserId.toLowerCase() === user.id.toLowerCase());
        const pendingSync = submission.status === 2;
        return [
          <div key="session"><p className="font-medium">{submission.session.sessionNumber}</p><p className="text-xs text-muted-foreground">{submission.session.tillCode} · {submission.session.tillName}</p><Badge className="mt-1" variant={pendingSync ? 'destructive' : 'outline'}>{TILL_CLOSE_STATUS[submission.status]}</Badge></div>,
          <div key="cashier"><p>{submission.session.cashierName}</p><p className="text-xs text-muted-foreground">{new Date(submission.session.businessDate).toLocaleDateString()}</p>{ownSession && <p className="mt-1 text-xs font-medium text-amber-700">Independent reviewer required</p>}</div>,
          <div key="count"><p>Expected: {money(submission.session.expectedClosingAmount, submission.session.currency)}</p><p>Counted: {money(submission.session.countedClosingAmount, submission.session.currency)}</p><p className={submission.session.varianceAmount ? 'font-medium text-amber-700' : 'text-muted-foreground'}>Variance: {money(submission.session.varianceAmount, submission.session.currency)}</p></div>,
          <div key="sync"><p>{submission.pendingMutationCount} pending mutation{submission.pendingMutationCount === 1 ? '' : 's'}</p><p className="max-w-56 truncate text-xs text-muted-foreground" title={submission.pendingMutationDigest}>{submission.pendingMutationDigest}</p>{submission.syncExceptionResolutionReason && <p className="mt-1 text-xs">HQ exception: {submission.syncExceptionResolutionReason}</p>}</div>,
          <div key="actions" className="flex min-w-44 flex-col gap-2"><Button size="sm" disabled={ownSession || pendingSync} onClick={() => setReviewAction({ submission, mode: 'approve', comments: '' })}>Approve close</Button><Button size="sm" variant="outline" disabled={ownSession} onClick={() => setReviewAction({ submission, mode: 'return', comments: '' })}>Return for recount</Button>{pendingSync && canResolveSync && <Button size="sm" variant="secondary" disabled={ownSession} onClick={() => setSyncResolution({ submission, reason: '' })}>Resolve sync exception</Button>}</div>,
        ];
      })} />
    </Section>}

    <ConfirmationDialog open={!!reviewAction} onOpenChange={open => !open && setReviewAction(undefined)} title={reviewAction?.mode === 'approve' ? 'Approve and finalize till close' : 'Return till for recount'} description={reviewAction?.mode === 'approve' ? 'Finance will close the till only after the retained Mobile POS sync evidence passes final validation.' : 'The cashier session will reopen and the prior submission will remain in the audit trail.'} confirmText={reviewAction?.mode === 'approve' ? 'Approve close' : 'Return for recount'} variant={reviewAction?.mode === 'return' ? 'destructive' : 'default'} isLoading={saving} confirmDisabled={reviewAction?.mode === 'return' && reviewAction.comments.trim().length < 10} onConfirm={runTillReview}>{reviewAction && <Field label={reviewAction.mode === 'approve' ? 'Reviewer comments' : 'Recount instructions *'}><Textarea value={reviewAction.comments} onChange={event => setReviewAction({ ...reviewAction, comments: event.target.value })} /></Field>}</ConfirmationDialog>
    <ConfirmationDialog open={!!syncResolution} onOpenChange={open => !open && setSyncResolution(undefined)} title="Resolve pending-sync exception" description="Use this only after an independent HQ review confirms why the retained pending mutations may proceed to closure." confirmText="Record resolution" isLoading={saving} confirmDisabled={!syncResolution || syncResolution.reason.trim().length < 20} onConfirm={resolveTillSync}>{syncResolution && <Field label="Independent resolution reason *"><Textarea value={syncResolution.reason} onChange={event => setSyncResolution({ ...syncResolution, reason: event.target.value })} /></Field>}</ConfirmationDialog>
  </div>;
}

export function MobilePosTillReportWorkspace() {
  const { hasPermission } = useAuth();
  const canCreateDeposits = hasPermission('MobilePOS.Till.Review') && hasPermission('Finance.Banking.Deposits.Create');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [history, setHistory] = useState<MobilePosTillCloseSubmission[]>([]);
  const [bankAccounts, setBankAccounts] = useState<MobilePosReferenceOption[]>([]);
  const [depositProposal, setDepositProposal] = useState<{ submission: MobilePosTillCloseSubmission; bankAccountId: string; depositDate: string; depositReference: string; notes: string }>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [nextHistory, references] = await Promise.all([
        mobilePosAdminService.tillCloseReport(),
        canCreateDeposits ? mobilePosAdminService.references() : Promise.resolve(undefined),
      ]);
      setHistory(nextHistory);
      setBankAccounts(references?.bankAccounts ?? []);
    } catch (error) { toast.error(errorMessage(error, 'Failed to load the Mobile POS till report.')); }
    finally { setLoading(false); }
  }, [canCreateDeposits]);

  useEffect(() => { void load(); }, [load]);

  const exportTillReport = () => {
    const quote = (value: unknown) => `"${String(value ?? '').replaceAll('"', '""')}"`;
    const rows = history.map(item => [
      item.session.sessionNumber, item.session.businessDate, item.session.cashierName,
      item.session.tillCode, item.session.currency, item.session.expectedClosingAmount,
      item.session.countedClosingAmount, item.session.varianceAmount,
      TILL_CLOSE_STATUS[item.status], item.pendingMutationCount,
      item.bankDepositNumber || '', item.bankDepositStatus ? BANK_DEPOSIT_STATUS[item.bankDepositStatus] : '',
    ]);
    const csv = [['Session', 'Business date', 'Cashier', 'Till', 'Currency', 'Expected', 'Counted', 'Variance', 'Close status', 'Pending sync', 'Deposit', 'Deposit status'], ...rows]
      .map(row => row.map(quote).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `mobile-pos-till-report-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const createDepositProposal = async () => {
    if (!depositProposal) return;
    if (!depositProposal.bankAccountId || depositProposal.depositReference.trim().length < 3) {
      toast.error('Select a bank account and enter the deposit slip or preparation reference.');
      return;
    }
    setSaving(true);
    try {
      const result = await mobilePosAdminService.createBankDepositProposal(depositProposal.submission.id, {
        bankAccountId: depositProposal.bankAccountId,
        depositDate: depositProposal.depositDate,
        depositReference: depositProposal.depositReference.trim(),
        notes: depositProposal.notes.trim() || undefined,
        rowVersion: depositProposal.submission.rowVersion,
      });
      toast.success(`Finance bank deposit ${result.depositNumber} created as a governed proposal.`);
      setDepositProposal(undefined);
      await load();
    } catch (error) { toast.error(errorMessage(error, 'The bank deposit proposal could not be created.')); }
    finally { setSaving(false); }
  };

  return <div className="space-y-6 p-4 md:p-6">
    <PageHeader title="Till Report" description="Review historical close, sync, variance, and Finance bank-deposit status by cashier session." loading={loading} onRefresh={() => void load()} action={<Button variant="outline" onClick={exportTillReport} disabled={loading || history.length === 0}><Download className="mr-2 h-4 w-4" />Export CSV</Button>} />
    {loading ? <LoadingState /> : <Section title="HQ till and Mobile POS report" description="A tenant-scoped operational record of till closures and their Finance custody outcome.">
      <Table headers={['Session / till', 'Cashier / business date', 'Expected / counted', 'Variance', 'Sync / close status', 'Bank deposit']} rows={history.map(submission => [
        <div key="session"><p className="font-medium">{submission.session.sessionNumber}</p><p className="text-xs text-muted-foreground">{submission.session.tillCode} · {submission.session.tillName}</p></div>,
        <div key="cashier"><p>{submission.session.cashierName}</p><p className="text-xs text-muted-foreground">{new Date(submission.session.businessDate).toLocaleDateString()}</p></div>,
        <div key="count"><p>{money(submission.session.expectedClosingAmount, submission.session.currency)}</p><p className="text-xs text-muted-foreground">Counted {money(submission.session.countedClosingAmount, submission.session.currency)}</p></div>,
        <span key="variance" className={submission.session.varianceAmount ? 'font-medium text-amber-700' : ''}>{money(submission.session.varianceAmount, submission.session.currency)}</span>,
        <div key="status"><Badge variant={submission.status === 2 ? 'destructive' : 'outline'}>{TILL_CLOSE_STATUS[submission.status]}</Badge><p className="mt-1 text-xs text-muted-foreground">{submission.pendingMutationCount} pending mutation{submission.pendingMutationCount === 1 ? '' : 's'}</p></div>,
        submission.bankDepositBatchId
          ? <div key="deposit"><p className="font-medium">{submission.bankDepositNumber}</p><p className="text-xs text-muted-foreground">{submission.bankDepositStatus ? BANK_DEPOSIT_STATUS[submission.bankDepositStatus] : 'Linked'}</p></div>
          : submission.status === 4 && canCreateDeposits
            ? <Button key="proposal" size="sm" variant="outline" onClick={() => setDepositProposal({ submission, bankAccountId: '', depositDate: new Date().toISOString().slice(0, 10), depositReference: '', notes: '' })}>Create deposit proposal</Button>
            : <span key="none" className="text-muted-foreground">Not available</span>,
      ])} />
    </Section>}

    <Dialog open={!!depositProposal} onOpenChange={open => !open && setDepositProposal(undefined)}><DialogContent><DialogHeader><DialogTitle>Create governed bank deposit proposal</DialogTitle><DialogDescription>The eligible custody entries from this finalized till session will be reserved in a draft Finance bank deposit. Finance submission, approval, posting, and bank confirmation remain unchanged.</DialogDescription></DialogHeader>{depositProposal && <div className="space-y-4"><div className="rounded-md border bg-muted/30 p-3 text-sm"><p className="font-medium">{depositProposal.submission.session.sessionNumber} · {depositProposal.submission.session.tillName}</p><p>Counted close: {money(depositProposal.submission.session.countedClosingAmount, depositProposal.submission.session.currency)}</p></div><SearchChoice label="Destination bank account" required value={depositProposal.bankAccountId} options={bankAccounts} onChange={value => setDepositProposal({ ...depositProposal, bankAccountId: value })} /><Field label="Deposit date *"><Input type="date" value={depositProposal.depositDate} onChange={event => setDepositProposal({ ...depositProposal, depositDate: event.target.value })} /></Field><Field label="Deposit slip / preparation reference *"><Input value={depositProposal.depositReference} onChange={event => setDepositProposal({ ...depositProposal, depositReference: event.target.value })} /></Field><Field label="Notes"><Textarea value={depositProposal.notes} onChange={event => setDepositProposal({ ...depositProposal, notes: event.target.value })} /></Field></div>}<DialogFooter><Button variant="outline" onClick={() => setDepositProposal(undefined)}>Cancel</Button><Button disabled={saving || !depositProposal?.bankAccountId || (depositProposal?.depositReference.trim().length || 0) < 3} onClick={() => void createDepositProposal()}>{saving ? 'Creating...' : 'Create Finance proposal'}</Button></DialogFooter></DialogContent></Dialog>
  </div>;
}

export function MobilePosDevicesWorkspace() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [devices, setDevices] = useState<MobilePosDevice[]>([]);
  const [stores, setStores] = useState<MobilePosStore[]>([]);
  const [tills, setTills] = useState<MobilePosTill[]>([]);
  const [deviceEditor, setDeviceEditor] = useState<{ device: MobilePosDevice; storeId: string; tillId: string; reason: string }>();
  const [revokeTarget, setRevokeTarget] = useState<MobilePosDevice>();
  const [revokeReason, setRevokeReason] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [nextDevices, nextStores, nextTills] = await Promise.all([
        mobilePosAdminService.devices(), mobilePosAdminService.stores(), mobilePosAdminService.tills(),
      ]);
      setDevices(nextDevices);
      setStores(nextStores);
      setTills(nextTills);
    } catch (error) { toast.error(errorMessage(error, 'Failed to load Mobile POS devices.')); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const activeStores = stores.filter(store => store.status === 2).map(store => ({ id: store.id, code: store.code, name: store.name }));
  const activeTills = deviceEditor
    ? tills.filter(till => till.status === 2 && till.mobilePosStoreId === deviceEditor.storeId).map(till => ({ id: till.id, code: till.tillNumber, name: till.name, secondary: till.liquidityAccountCode }))
    : [];

  const saveDevice = async () => {
    if (!deviceEditor) return;
    if (!deviceEditor.storeId || !deviceEditor.tillId || !deviceEditor.reason.trim()) {
      toast.error('Store, till, and approval reason are required.');
      return;
    }
    setSaving(true);
    try {
      await mobilePosAdminService.approveDevice(deviceEditor.device.id, { mobilePosStoreId: deviceEditor.storeId, mobilePosTillId: deviceEditor.tillId, reason: deviceEditor.reason, rowVersion: deviceEditor.device.rowVersion });
      toast.success('Device assignment approved.');
      setDeviceEditor(undefined);
      await load();
    } catch (error) { toast.error(errorMessage(error, 'Failed to approve the device.')); }
    finally { setSaving(false); }
  };

  const revokeDevice = async () => {
    if (!revokeTarget) return false;
    setSaving(true);
    try {
      await mobilePosAdminService.revokeDevice(revokeTarget.id, { reason: revokeReason, rowVersion: revokeTarget.rowVersion });
      toast.success('Device revoked.');
      setRevokeTarget(undefined);
      await load();
      return true;
    } catch (error) {
      toast.error(errorMessage(error, 'Failed to revoke the device.'));
      return false;
    } finally { setSaving(false); }
  };

  return <div className="space-y-6 p-4 md:p-6">
    <PageHeader title="Devices" description="Approve, assign, reassign, or revoke Mobile POS device enrollments." loading={loading} onRefresh={() => void load()} />
    {loading ? <LoadingState /> : <Section title="Device enrollment register" description="Approve enrollment requests only after assigning an active store and till.">
      <Table headers={['Device', 'Status', 'Assignment', 'Last seen', 'Actions']} rows={devices.map(device => [
        <div key="d"><p className="font-medium">{device.deviceName}</p><p className="text-xs text-muted-foreground">{[device.manufacturer, device.model, device.appVersion].filter(Boolean).join(' · ')}</p></div>,
        <Badge key="st" variant={device.status === 2 ? 'default' : device.status === 4 ? 'destructive' : 'outline'}>{DEVICE_STATUS[device.status]}</Badge>,
        device.storeName ? `${device.storeName} · ${device.tillNumber}` : 'Unassigned',
        device.lastSeenAtUtc ? new Date(device.lastSeenAtUtc).toLocaleString() : 'Never',
        <div key="a" className="flex gap-2"><Button size="sm" variant="outline" onClick={() => setDeviceEditor({ device, storeId: device.mobilePosStoreId || '', tillId: device.mobilePosTillId || '', reason: '' })}>{device.status === 1 ? 'Approve' : 'Reassign'}</Button>{device.status !== 4 && <Button size="sm" variant="ghost" className="text-destructive" onClick={() => { setRevokeTarget(device); setRevokeReason(''); }}><ShieldOff className="mr-2 h-4 w-4" />Revoke</Button>}</div>,
      ])} />
    </Section>}

    <Dialog open={!!deviceEditor} onOpenChange={open => !open && setDeviceEditor(undefined)}><DialogContent><DialogHeader><DialogTitle>Approve or reassign device</DialogTitle><DialogDescription>{deviceEditor?.device.deviceName}. Assigning a device increments its revocation epoch and invalidates previous offline authority.</DialogDescription></DialogHeader>{deviceEditor && <div className="space-y-4"><SearchChoice label="Active store" required value={deviceEditor.storeId} options={activeStores} onChange={value => setDeviceEditor({ ...deviceEditor, storeId: value, tillId: '' })} /><SearchChoice label="Active till" required value={deviceEditor.tillId} options={activeTills} onChange={value => setDeviceEditor({ ...deviceEditor, tillId: value })} /><Field label="Approval reason *"><Textarea value={deviceEditor.reason} onChange={event => setDeviceEditor({ ...deviceEditor, reason: event.target.value })} /></Field></div>}<DialogFooter><Button variant="outline" onClick={() => setDeviceEditor(undefined)}>Cancel</Button><Button onClick={() => void saveDevice()} disabled={saving}>{saving ? 'Saving...' : 'Approve assignment'}</Button></DialogFooter></DialogContent></Dialog>
    <ConfirmationDialog open={!!revokeTarget} onOpenChange={open => !open && setRevokeTarget(undefined)} title="Revoke Mobile POS device" description="This immediately blocks bootstrap and heartbeat requests and revokes active offline grants." confirmText="Revoke device" variant="destructive" isLoading={saving} confirmDisabled={!revokeReason.trim()} onConfirm={revokeDevice}><Field label="Revocation reason *"><Textarea value={revokeReason} onChange={event => setRevokeReason(event.target.value)} /></Field></ConfirmationDialog>
  </div>;
}
