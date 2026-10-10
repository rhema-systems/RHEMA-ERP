'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Edit2, Loader2, MonitorSmartphone, Plus, RefreshCw, Search, ShieldOff, Store, UserPlus, WalletCards } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  MobilePosAdministrationReferences,
  MobilePosDevice,
  MobilePosOfflinePolicy,
  MobilePosOfflinePolicyInput,
  MobilePosReferenceOption,
  MobilePosStore,
  MobilePosStoreInput,
  MobilePosTill,
  MobilePosTillCloseSubmission,
  MobilePosTillInput,
  mobilePosAdminService,
} from '@/services/mobile-pos-admin.service';

const STORE_STATUS: Record<number, string> = { 1: 'Draft', 2: 'Active', 3: 'Suspended', 4: 'Retired' };
const DEVICE_STATUS: Record<number, string> = { 1: 'Pending', 2: 'Active', 3: 'Suspended', 4: 'Revoked', 5: 'Retired' };
const TILL_CLOSE_STATUS: Record<number, string> = { 1: 'Ready for review', 2: 'Pending sync', 3: 'Sync exception resolved', 4: 'Finalized', 5: 'Returned for recount' };
const BANK_DEPOSIT_STATUS: Record<number, string> = { 1: 'Draft', 2: 'Submitted', 3: 'Approved', 4: 'Posted', 5: 'Returned', 6: 'Rejected', 7: 'Cancelled', 8: 'Reversed' };
const money = (value: number, currency = 'GHS') => `${currency} ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const EMPTY_REFERENCES: MobilePosAdministrationReferences = {
  customers: [], currencies: [], locations: [], warehouses: [], companyProfiles: [], cashTills: [], bankAccounts: [], paymentMethods: [], users: [], dimensions: [],
};

const blankPolicy = (): MobilePosOfflinePolicyInput => ({
  name: '', isActive: true, authorizationWindowMinutes: 480,
  maximumTransactionAmount: undefined, maximumAggregateAmount: undefined,
  maximumTransactionCount: undefined, maximumOfflineAgeMinutes: 480,
  allowCashSale: true, allowCashReceipt: true, allowPartialPayment: true,
  allowReturns: false, allowReversals: false, allowProvisionalReceipt: true,
  allowDayEndSubmissionWithPendingSync: false,
  requireExternalReferenceForElectronicTender: true, rowVersion: '',
});

const errorMessage = (error: unknown, fallback: string) => {
  const candidate = error as { response?: { data?: { detail?: string; title?: string; error?: string } | string }; message?: string };
  const data = candidate?.response?.data;
  if (typeof data === 'string' && data.trim()) return data;
  if (data && typeof data === 'object') return data.detail || data.error || data.title || fallback;
  return candidate?.message || fallback;
};

function SearchChoice({
  label, value, options, onChange, required = false, placeholder = 'Search by code or name',
}: {
  label: string; value?: string; options: Array<MobilePosReferenceOption & { selectionId?: string }>;
  onChange: (value: string) => void; required?: boolean; placeholder?: string;
}) {
  const [search, setSearch] = useState('');
  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return options;
    return options.filter(option => `${option.code} ${option.name} ${option.secondary || ''}`.toLowerCase().includes(needle));
  }, [options, search]);
  const selected = options.find(option => (option.selectionId || option.id) === value);
  return (
    <div className="space-y-2">
      <Label>{label}{required ? ' *' : ''}</Label>
      <div className="rounded-md border">
        <div className="relative border-b">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input value={search} onChange={event => setSearch(event.target.value)} placeholder={placeholder} className="border-0 pl-9 shadow-none focus-visible:ring-0" />
        </div>
        {selected && <div className="border-b bg-primary/5 px-3 py-2 text-sm"><span className="font-medium">Selected:</span> {selected.code} · {selected.name}</div>}
        <div className="max-h-36 overflow-y-auto p-1">
          {!required && <button type="button" className="w-full rounded px-2 py-2 text-left text-sm hover:bg-muted" onClick={() => onChange('')}>None</button>}
          {visible.map(option => {
            const id = option.selectionId || option.id;
            return <button key={id} type="button" onClick={() => onChange(id)} className={`w-full rounded px-2 py-2 text-left text-sm hover:bg-muted ${value === id ? 'bg-primary/10 text-primary' : ''}`}>
              <span className="font-medium">{option.code}</span> · {option.name}{option.secondary ? <span className="block text-xs text-muted-foreground">{option.secondary}</span> : null}
            </button>;
          })}
          {!visible.length && <p className="px-2 py-3 text-sm text-muted-foreground">No matching options.</p>}
        </div>
      </div>
    </div>
  );
}

export default function MobilePosAdministrationPage() {
  const { hasPermission, user } = useAuth();
  const canManage = hasPermission('MobilePOS.Store.Manage');
  const canApproveDevices = hasPermission('MobilePOS.Device.Approve');
  const canReviewTills = hasPermission('MobilePOS.Till.Review') && hasPermission('Finance.CashTills.Closures.Review');
  const canResolveSync = canReviewTills && hasPermission('MobilePOS.Sync.Resolve');
  const canCreateDeposits = hasPermission('MobilePOS.Till.Review') && hasPermission('Finance.Banking.Deposits.Create');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [references, setReferences] = useState(EMPTY_REFERENCES);
  const [stores, setStores] = useState<MobilePosStore[]>([]);
  const [policies, setPolicies] = useState<MobilePosOfflinePolicy[]>([]);
  const [tills, setTills] = useState<MobilePosTill[]>([]);
  const [devices, setDevices] = useState<MobilePosDevice[]>([]);
  const [tillClosures, setTillClosures] = useState<MobilePosTillCloseSubmission[]>([]);
  const [tillHistory, setTillHistory] = useState<MobilePosTillCloseSubmission[]>([]);
  const [storeEditor, setStoreEditor] = useState<{ id?: string; value: MobilePosStoreInput }>();
  const [tillEditor, setTillEditor] = useState<{ id?: string; value: MobilePosTillInput }>();
  const [policyEditor, setPolicyEditor] = useState<{ id?: string; value: MobilePosOfflinePolicyInput }>();
  const [deviceEditor, setDeviceEditor] = useState<{ device: MobilePosDevice; storeId: string; tillId: string; reason: string }>();
  const [revokeTarget, setRevokeTarget] = useState<MobilePosDevice>();
  const [revokeReason, setRevokeReason] = useState('');
  const [assignmentOpen, setAssignmentOpen] = useState(false);
  const [assignment, setAssignment] = useState({ userId: '', storeId: '', from: '', to: '', reason: '' });
  const [reviewAction, setReviewAction] = useState<{ submission: MobilePosTillCloseSubmission; mode: 'approve' | 'return'; comments: string }>();
  const [syncResolution, setSyncResolution] = useState<{ submission: MobilePosTillCloseSubmission; reason: string }>();
  const [depositProposal, setDepositProposal] = useState<{ submission: MobilePosTillCloseSubmission; bankAccountId: string; depositDate: string; depositReference: string; notes: string }>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [nextReferences, nextStores, nextPolicies, nextTills, nextDevices, nextTillClosures, nextTillHistory] = await Promise.all([
        mobilePosAdminService.references(), mobilePosAdminService.stores(), mobilePosAdminService.policies(), mobilePosAdminService.tills(), canApproveDevices ? mobilePosAdminService.devices() : Promise.resolve([]), canReviewTills ? mobilePosAdminService.tillCloseSubmissions() : Promise.resolve([]), canReviewTills ? mobilePosAdminService.tillCloseReport() : Promise.resolve([]),
      ]);
      setReferences(nextReferences); setStores(nextStores); setPolicies(nextPolicies); setTills(nextTills); setDevices(nextDevices); setTillClosures(nextTillClosures); setTillHistory(nextTillHistory);
    } catch (error) { toast.error(errorMessage(error, 'Failed to load Mobile POS administration.')); }
    finally { setLoading(false); }
  }, [canApproveDevices, canReviewTills]);

  useEffect(() => { void load(); }, [load]);

  const openStore = (store?: MobilePosStore) => setStoreEditor({
    id: store?.id,
    value: store ? {
      code: store.code, name: store.name, status: store.status, companyProfileId: store.companyProfileId,
      locationId: store.locationId, warehouseId: store.warehouseId, currencyCode: store.currencyCode,
      timeZoneId: store.timeZoneId, defaultWalkInBusinessPartnerId: store.defaultWalkInBusinessPartnerId,
      defaultWalkInBusinessPartnerRoleId: store.defaultWalkInBusinessPartnerRoleId, offlinePolicyId: store.offlinePolicyId,
      notes: store.notes, rowVersion: store.rowVersion,
      dimensionDefaults: store.dimensionDefaults.map(item => ({ financeDimensionDefinitionId: item.financeDimensionDefinitionId, financeDimensionValueId: item.financeDimensionValueId })),
    } : {
      code: '', name: '', status: 1, locationId: '',
      currencyCode: references.currencies.find(item => item.secondary === 'Base currency')?.code || references.currencies[0]?.code || '',
      timeZoneId: 'Africa/Accra',
      defaultWalkInBusinessPartnerId: '', defaultWalkInBusinessPartnerRoleId: '', dimensionDefaults: [],
    },
  });

  const saveStore = async () => {
    if (!storeEditor) return;
    const value = storeEditor.value;
    if (!value.code.trim() || !value.name.trim() || !value.locationId || !value.currencyCode || !value.defaultWalkInBusinessPartnerId || !value.defaultWalkInBusinessPartnerRoleId) {
      toast.error('Store code, name, location, currency, and an approved default walk-in customer are required.'); return;
    }
    setSaving(true);
    try { await mobilePosAdminService.saveStore(storeEditor.id, value); toast.success(`Store ${storeEditor.id ? 'updated' : 'created'}.`); setStoreEditor(undefined); await load(); }
    catch (error) {
      const candidate = error as { response?: { status?: number } };
      if (storeEditor.id && candidate.response?.status === 409) {
        try {
          const latestStores = await mobilePosAdminService.stores();
          setStores(latestStores);
          const latest = latestStores.find(item => item.id === storeEditor.id);
          if (latest) setStoreEditor(current => current ? { ...current, value: { ...current.value, rowVersion: latest.rowVersion } } : current);
          toast.error(`${errorMessage(error, 'The store changed while you were editing it.')} The latest version is loaded; review and save again.`);
        } catch {
          toast.error(errorMessage(error, 'The store changed while you were editing it. Refresh and try again.'));
        }
      } else toast.error(errorMessage(error, 'Failed to save the store.'));
    }
    finally { setSaving(false); }
  };

  const openTill = (till?: MobilePosTill) => setTillEditor({
    id: till?.id,
    value: till ? {
      mobilePosStoreId: till.mobilePosStoreId, tillNumber: till.tillNumber, name: till.name, status: till.status,
      liquidityAccountId: till.liquidityAccountId, notes: till.notes, rowVersion: till.rowVersion,
      paymentMethods: till.paymentMethods.map(item => ({ paymentMethodId: item.paymentMethodId, allowOnline: item.allowOnline, allowOffline: item.allowOffline, requireExternalAuthorizationReference: item.requireExternalAuthorizationReference, displayOrder: item.displayOrder })),
    } : { mobilePosStoreId: '', tillNumber: '', name: '', status: 1, liquidityAccountId: '', paymentMethods: [] },
  });

  const saveTill = async () => {
    if (!tillEditor) return;
    if (!tillEditor.value.mobilePosStoreId || !tillEditor.value.liquidityAccountId || !tillEditor.value.tillNumber.trim() || !tillEditor.value.name.trim()) {
      toast.error('Store, cash-till liquidity account, till number, and name are required.'); return;
    }
    setSaving(true);
    try { await mobilePosAdminService.saveTill(tillEditor.id, tillEditor.value); toast.success(`Till ${tillEditor.id ? 'updated' : 'created'}.`); setTillEditor(undefined); await load(); }
    catch (error) { toast.error(errorMessage(error, 'Failed to save the till.')); }
    finally { setSaving(false); }
  };

  const savePolicy = async () => {
    if (!policyEditor) return;
    if (!policyEditor.value.name.trim()) { toast.error('Policy name is required.'); return; }
    setSaving(true);
    try { await mobilePosAdminService.savePolicy(policyEditor.id, policyEditor.value); toast.success(`Offline policy ${policyEditor.id ? 'updated' : 'created'}.`); setPolicyEditor(undefined); await load(); }
    catch (error) { toast.error(errorMessage(error, 'Failed to save the policy.')); }
    finally { setSaving(false); }
  };

  const saveDevice = async () => {
    if (!deviceEditor) return;
    if (!deviceEditor.storeId || !deviceEditor.tillId || !deviceEditor.reason.trim()) { toast.error('Store, till, and approval reason are required.'); return; }
    setSaving(true);
    try { await mobilePosAdminService.approveDevice(deviceEditor.device.id, { mobilePosStoreId: deviceEditor.storeId, mobilePosTillId: deviceEditor.tillId, reason: deviceEditor.reason, rowVersion: deviceEditor.device.rowVersion }); toast.success('Device assignment approved.'); setDeviceEditor(undefined); await load(); }
    catch (error) { toast.error(errorMessage(error, 'Failed to approve the device.')); }
    finally { setSaving(false); }
  };

  const saveAssignment = async () => {
    if (!assignment.userId || !assignment.storeId || !assignment.reason.trim()) { toast.error('User, store, and assignment reason are required.'); return; }
    setSaving(true);
    try {
      await mobilePosAdminService.assignUser({ userId: assignment.userId, mobilePosStoreId: assignment.storeId, effectiveFromUtc: assignment.from ? new Date(assignment.from).toISOString() : new Date().toISOString(), effectiveToUtc: assignment.to ? new Date(assignment.to).toISOString() : undefined, reason: assignment.reason });
      toast.success('User store assignment saved.'); setAssignmentOpen(false); setAssignment({ userId: '', storeId: '', from: '', to: '', reason: '' });
    } catch (error) { toast.error(errorMessage(error, 'Failed to assign the user.')); }
    finally { setSaving(false); }
  };

  const runTillReview = async () => {
    if (!reviewAction) return false;
    const comments = reviewAction.comments.trim();
    if (reviewAction.mode === 'return' && comments.length < 10) {
      toast.error('Enter recount instructions of at least 10 characters.'); return false;
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
      setReviewAction(undefined); await load(); return true;
    } catch (error) { toast.error(errorMessage(error, 'The till review could not be completed.')); return false; }
    finally { setSaving(false); }
  };

  const resolveTillSync = async () => {
    if (!syncResolution) return false;
    const reason = syncResolution.reason.trim();
    if (reason.length < 20) { toast.error('Enter an independent sync resolution reason of at least 20 characters.'); return false; }
    setSaving(true);
    try {
      await mobilePosAdminService.resolvePendingSync(syncResolution.submission.id, { reason, rowVersion: syncResolution.submission.rowVersion });
      toast.success('Pending-sync exception recorded for independent till review.');
      setSyncResolution(undefined); await load(); return true;
    } catch (error) { toast.error(errorMessage(error, 'The pending-sync exception could not be resolved.')); return false; }
    finally { setSaving(false); }
  };

  const createDepositProposal = async () => {
    if (!depositProposal) return;
    if (!depositProposal.bankAccountId || depositProposal.depositReference.trim().length < 3) {
      toast.error('Select a bank account and enter the deposit slip or preparation reference.'); return;
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
      setDepositProposal(undefined); await load();
    } catch (error) { toast.error(errorMessage(error, 'The bank deposit proposal could not be created.')); }
    finally { setSaving(false); }
  };

  const exportTillReport = () => {
    const quote = (value: unknown) => `"${String(value ?? '').replaceAll('"', '""')}"`;
    const rows = tillHistory.map(item => [
      item.session.sessionNumber, item.session.businessDate, item.session.cashierName,
      item.session.tillCode, item.session.currency, item.session.expectedClosingAmount,
      item.session.countedClosingAmount, item.session.varianceAmount,
      TILL_CLOSE_STATUS[item.status], item.pendingMutationCount,
      item.bankDepositNumber || '', item.bankDepositStatus ? BANK_DEPOSIT_STATUS[item.bankDepositStatus] : '',
    ]);
    const csv = [['Session', 'Business date', 'Cashier', 'Till', 'Currency', 'Expected', 'Counted', 'Variance', 'Close status', 'Pending sync', 'Deposit', 'Deposit status'], ...rows]
      .map(row => row.map(quote).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a'); link.href = url; link.download = `mobile-pos-till-report-${new Date().toISOString().slice(0, 10)}.csv`; link.click(); URL.revokeObjectURL(url);
  };

  const customerOptions = references.customers.map(customer => ({ id: customer.businessPartnerId, selectionId: customer.businessPartnerRoleId, code: customer.code, name: customer.name }));
  const activeStores = stores.filter(store => store.status === 2).map(store => ({ id: store.id, code: store.code, name: store.name }));
  const activeTills = deviceEditor ? tills.filter(till => till.status === 2 && till.mobilePosStoreId === deviceEditor.storeId).map(till => ({ id: till.id, code: till.tillNumber, name: till.name, secondary: till.liquidityAccountCode })) : [];

  return <div className="space-y-6 p-4 md:p-6">
    <div className="flex flex-col justify-between gap-3 md:flex-row md:items-center">
      <div><h1 className="text-2xl font-bold tracking-tight">Mobile POS Administration</h1><p className="text-muted-foreground">Govern stores, walk-in customer defaults, tills, offline limits, devices, and user assignments.</p></div>
      <Button variant="outline" onClick={() => void load()} disabled={loading}><RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />Refresh</Button>
    </div>
    <Card className="border-blue-200 bg-blue-50/50 dark:border-blue-900 dark:bg-blue-950/20"><CardContent className="pt-6 text-sm"><strong>Walk-in control:</strong> every store must map to an approved customer with an active Customer role and effective AR profile. The mobile app uses that customer automatically unless the cashier selects another approved customer.</CardContent></Card>
    {loading ? <div className="flex min-h-60 items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-primary" /></div> :
      <Tabs defaultValue="stores" className="space-y-4">
        <TabsList className="grid h-auto w-full grid-cols-2 lg:grid-cols-7"><TabsTrigger value="stores">Stores</TabsTrigger><TabsTrigger value="tills">Tills</TabsTrigger><TabsTrigger value="day-end">Day end</TabsTrigger><TabsTrigger value="reports">Till reports</TabsTrigger><TabsTrigger value="devices">Devices</TabsTrigger><TabsTrigger value="policies">Offline policies</TabsTrigger><TabsTrigger value="assignments">User assignments</TabsTrigger></TabsList>

        <TabsContent value="stores"><Section title="Stores" description="Operating locations and their governed walk-in customer." action={canManage ? <Button onClick={() => openStore()}><Plus className="mr-2 h-4 w-4" />Add store</Button> : undefined}>
          <Table headers={['Store', 'Status', 'Location / warehouse', 'Default walk-in customer', 'Offline policy', '']} rows={stores.map(store => [<div key="s"><p className="font-medium">{store.code} · {store.name}</p><p className="text-xs text-muted-foreground">{store.currencyCode} · {store.timeZoneId}</p></div>, <Badge key="st" variant={store.status === 2 ? 'default' : 'outline'}>{STORE_STATUS[store.status]}</Badge>, <div key="l">{store.locationName}<p className="text-xs text-muted-foreground">{store.warehouseName || 'No warehouse default'}</p></div>, <div key="c"><p className="font-medium">{store.defaultWalkInCustomerCode}</p><p className="text-xs text-muted-foreground">{store.defaultWalkInCustomerName}</p></div>, store.offlinePolicyName || 'Online only', canManage ? <Button key="e" variant="ghost" size="sm" onClick={() => openStore(store)}><Edit2 className="mr-2 h-4 w-4" />Edit</Button> : null])} />
        </Section></TabsContent>

        <TabsContent value="tills"><Section title="Tills" description="Map one Mobile POS till to one active CashTill liquidity account." action={canManage ? <Button onClick={() => openTill()}><Plus className="mr-2 h-4 w-4" />Add till</Button> : undefined}>
          <Table headers={['Till', 'Store', 'Status', 'Cash account', 'Payment methods', '']} rows={tills.map(till => [<div key="t"><p className="font-medium">{till.tillNumber} · {till.name}</p><p className="text-xs text-muted-foreground">{till.currencyCode}</p></div>, `${till.storeCode} · ${till.storeName}`, <Badge key="st" variant={till.status === 2 ? 'default' : 'outline'}>{STORE_STATUS[till.status]}</Badge>, till.liquidityAccountCode, till.paymentMethods.map(item => item.code).join(', ') || 'None', canManage ? <Button key="e" variant="ghost" size="sm" onClick={() => openTill(till)}><Edit2 className="mr-2 h-4 w-4" />Edit</Button> : null])} />
        </Section></TabsContent>

        <TabsContent value="day-end"><Section title="Mobile POS day-end review" description="Review server-derived cash custody, variance, and retained offline synchronization evidence before Finance finalization.">
          {!canReviewTills ? <p className="rounded-md border p-6 text-center text-sm text-muted-foreground">MobilePOS.Till.Review and Finance.CashTills.Closures.Review are both required.</p> :
          <Table headers={['Session', 'Cashier / date', 'Count and variance', 'Sync evidence', 'Actions']} rows={tillClosures.map(submission => {
            const ownSession = Boolean(user?.id && submission.session.cashierUserId.toLowerCase() === user.id.toLowerCase());
            const pendingSync = submission.status === 2;
            return [
              <div key="session"><p className="font-medium">{submission.session.sessionNumber}</p><p className="text-xs text-muted-foreground">{submission.session.tillCode} · {submission.session.tillName}</p><Badge className="mt-1" variant={pendingSync ? 'destructive' : 'outline'}>{TILL_CLOSE_STATUS[submission.status]}</Badge></div>,
              <div key="cashier"><p>{submission.session.cashierName}</p><p className="text-xs text-muted-foreground">{new Date(submission.session.businessDate).toLocaleDateString()}</p>{ownSession && <p className="mt-1 text-xs font-medium text-amber-700">Independent reviewer required</p>}</div>,
              <div key="count"><p>Expected: {money(submission.session.expectedClosingAmount, submission.session.currency)}</p><p>Counted: {money(submission.session.countedClosingAmount, submission.session.currency)}</p><p className={submission.session.varianceAmount ? 'font-medium text-amber-700' : 'text-muted-foreground'}>Variance: {money(submission.session.varianceAmount, submission.session.currency)}</p></div>,
              <div key="sync"><p>{submission.pendingMutationCount} pending mutation{submission.pendingMutationCount === 1 ? '' : 's'}</p><p className="max-w-56 truncate text-xs text-muted-foreground" title={submission.pendingMutationDigest}>{submission.pendingMutationDigest}</p>{submission.syncExceptionResolutionReason && <p className="mt-1 text-xs">HQ exception: {submission.syncExceptionResolutionReason}</p>}</div>,
              <div key="actions" className="flex min-w-44 flex-col gap-2"><Button size="sm" disabled={ownSession || pendingSync} onClick={() => setReviewAction({ submission, mode: 'approve', comments: '' })}>Approve close</Button><Button size="sm" variant="outline" disabled={ownSession} onClick={() => setReviewAction({ submission, mode: 'return', comments: '' })}>Return for recount</Button>{pendingSync && canResolveSync && <Button size="sm" variant="secondary" disabled={ownSession} onClick={() => setSyncResolution({ submission, reason: '' })}>Resolve sync exception</Button>}</div>,
            ];
          })} />}
        </Section></TabsContent>

        <TabsContent value="reports"><Section title="HQ till and Mobile POS report" description="Historical close, sync, variance, and Finance bank-deposit status by cashier session." action={canReviewTills ? <Button variant="outline" onClick={exportTillReport}>Export CSV</Button> : undefined}>
          {!canReviewTills ? <p className="rounded-md border p-6 text-center text-sm text-muted-foreground">MobilePOS.Till.Review and Finance.CashTills.Closures.Review are both required.</p> :
          <Table headers={['Session / till', 'Cashier / business date', 'Expected / counted', 'Variance', 'Sync / close status', 'Bank deposit']} rows={tillHistory.map(submission => [
            <div key="session"><p className="font-medium">{submission.session.sessionNumber}</p><p className="text-xs text-muted-foreground">{submission.session.tillCode} · {submission.session.tillName}</p></div>,
            <div key="cashier"><p>{submission.session.cashierName}</p><p className="text-xs text-muted-foreground">{new Date(submission.session.businessDate).toLocaleDateString()}</p></div>,
            <div key="count"><p>{money(submission.session.expectedClosingAmount, submission.session.currency)}</p><p className="text-xs text-muted-foreground">Counted {money(submission.session.countedClosingAmount, submission.session.currency)}</p></div>,
            <span key="variance" className={submission.session.varianceAmount ? 'font-medium text-amber-700' : ''}>{money(submission.session.varianceAmount, submission.session.currency)}</span>,
            <div key="status"><Badge variant={submission.status === 2 ? 'destructive' : 'outline'}>{TILL_CLOSE_STATUS[submission.status]}</Badge><p className="mt-1 text-xs text-muted-foreground">{submission.pendingMutationCount} pending mutation{submission.pendingMutationCount === 1 ? '' : 's'}</p></div>,
            submission.bankDepositBatchId ? <div key="deposit"><p className="font-medium">{submission.bankDepositNumber}</p><p className="text-xs text-muted-foreground">{submission.bankDepositStatus ? BANK_DEPOSIT_STATUS[submission.bankDepositStatus] : 'Linked'}</p></div> : submission.status === 4 && canCreateDeposits ? <Button key="proposal" size="sm" variant="outline" onClick={() => setDepositProposal({ submission, bankAccountId: '', depositDate: new Date().toISOString().slice(0, 10), depositReference: '', notes: '' })}>Create deposit proposal</Button> : <span key="none" className="text-muted-foreground">Not available</span>,
          ])} />}
        </Section></TabsContent>

        <TabsContent value="devices"><Section title="Devices" description="Approve enrollment requests only after assigning an active store and till.">
          {!canApproveDevices ? <p className="rounded-md border p-6 text-center text-sm text-muted-foreground">MobilePOS.Device.Approve is required to view and manage enrolled devices.</p> :
          <Table headers={['Device', 'Status', 'Assignment', 'Last seen', 'Actions']} rows={devices.map(device => [<div key="d"><p className="font-medium">{device.deviceName}</p><p className="text-xs text-muted-foreground">{[device.manufacturer, device.model, device.appVersion].filter(Boolean).join(' · ')}</p></div>, <Badge key="st" variant={device.status === 2 ? 'default' : device.status === 4 ? 'destructive' : 'outline'}>{DEVICE_STATUS[device.status]}</Badge>, device.storeName ? `${device.storeName} · ${device.tillNumber}` : 'Unassigned', device.lastSeenAtUtc ? new Date(device.lastSeenAtUtc).toLocaleString() : 'Never', <div key="a" className="flex gap-2"><Button size="sm" variant="outline" onClick={() => setDeviceEditor({ device, storeId: device.mobilePosStoreId || '', tillId: device.mobilePosTillId || '', reason: '' })}>{device.status === 1 ? 'Approve' : 'Reassign'}</Button>{device.status !== 4 && <Button size="sm" variant="ghost" className="text-destructive" onClick={() => { setRevokeTarget(device); setRevokeReason(''); }}><ShieldOff className="mr-2 h-4 w-4" />Revoke</Button>}</div>])} />
          }
        </Section></TabsContent>

        <TabsContent value="policies"><Section title="Offline policies" description="Explicit transaction limits and authorization windows; no device receives offline authority by default." action={canManage ? <Button onClick={() => setPolicyEditor({ value: blankPolicy() })}><Plus className="mr-2 h-4 w-4" />Add policy</Button> : undefined}>
          <Table headers={['Policy', 'Window', 'Limits', 'Permitted offline', '']} rows={policies.map(policy => [<div key="p"><p className="font-medium">{policy.name}</p><Badge variant={policy.isActive ? 'default' : 'outline'}>{policy.isActive ? 'Active' : 'Inactive'}</Badge></div>, `${policy.authorizationWindowMinutes} min authority / ${policy.maximumOfflineAgeMinutes} min age`, <div key="l"><p>Per transaction: {policy.maximumTransactionAmount ?? 'No limit'}</p><p>Aggregate: {policy.maximumAggregateAmount ?? 'No limit'} · Count: {policy.maximumTransactionCount ?? 'No limit'}</p></div>, [policy.allowCashSale && 'Cash sale', policy.allowCashReceipt && 'Cash receipt', policy.allowPartialPayment && 'Partial payment', policy.allowReturns && 'Returns', policy.allowReversals && 'Reversals'].filter(Boolean).join(', ') || 'None', canManage ? <Button key="e" variant="ghost" size="sm" onClick={() => setPolicyEditor({ id: policy.id, value: { ...policy } })}><Edit2 className="mr-2 h-4 w-4" />Edit</Button> : null])} />
        </Section></TabsContent>

        <TabsContent value="assignments"><Section title="User store assignments" description="A signed-in user can bootstrap a device only for the active store assigned here." action={canManage ? <Button onClick={() => setAssignmentOpen(true)}><UserPlus className="mr-2 h-4 w-4" />Assign user</Button> : undefined}>
          <div className="grid gap-4 md:grid-cols-3"><Metric icon={<Store />} label="Active stores" value={String(stores.filter(item => item.status === 2).length)} /><Metric icon={<WalletCards />} label="Active tills" value={String(tills.filter(item => item.status === 2).length)} /><Metric icon={<MonitorSmartphone />} label="Pending devices" value={String(devices.filter(item => item.status === 1).length)} /></div>
          <p className="mt-4 text-sm text-muted-foreground">Saving an assignment closes the user&apos;s current active store assignment and records the reason in the audit trail.</p>
        </Section></TabsContent>
      </Tabs>}

    <Dialog open={!!storeEditor} onOpenChange={open => !open && setStoreEditor(undefined)}><DialogContent className="max-w-4xl"><DialogHeader><DialogTitle>{storeEditor?.id ? 'Edit' : 'Create'} Mobile POS store</DialogTitle><DialogDescription>The default walk-in customer is mandatory and is restricted to approved customer records with an effective AR profile.</DialogDescription></DialogHeader>{storeEditor && <div className="grid gap-5 md:grid-cols-2">
      <Field label="Store code *"><Input value={storeEditor.value.code} onChange={e => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, code: e.target.value } })} /></Field><Field label="Store name *"><Input value={storeEditor.value.name} onChange={e => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, name: e.target.value } })} /></Field>
      <SelectField label="Status" value={String(storeEditor.value.status)} onChange={value => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, status: Number(value) as 1 | 2 | 3 | 4 } })} options={Object.entries(STORE_STATUS).map(([value, label]) => ({ value, label }))} />
      <SearchChoice label="Operating location" required value={storeEditor.value.locationId} options={references.locations} onChange={value => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, locationId: value } })} />
      <SearchChoice label="Warehouse default" value={storeEditor.value.warehouseId} options={references.warehouses} onChange={value => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, warehouseId: value || undefined } })} />
      <SearchChoice label="Company profile" value={storeEditor.value.companyProfileId} options={references.companyProfiles} onChange={value => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, companyProfileId: value || undefined } })} />
      <SearchChoice label="Default walk-in customer" required value={storeEditor.value.defaultWalkInBusinessPartnerRoleId} options={customerOptions} onChange={roleId => { const customer = references.customers.find(item => item.businessPartnerRoleId === roleId); setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, defaultWalkInBusinessPartnerRoleId: roleId, defaultWalkInBusinessPartnerId: customer?.businessPartnerId || '' } }); }} />
      <div className="space-y-2">
        <SelectField label="Operating mode / offline policy" value={storeEditor.value.offlinePolicyId || 'none'} onChange={value => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, offlinePolicyId: value === 'none' ? undefined : value } })} options={[{ value: 'none', label: 'Online only (offline transactions blocked)' }, ...policies.filter(item => item.isActive).map(item => ({ value: item.id, label: item.name }))]} />
        <p className="text-xs text-muted-foreground">{policies.some(item => item.isActive) ? 'Choose Online only or one active policy. A policy controls the offline authorization window, transaction limits, and permitted operations.' : 'No active offline policy is configured. Create and activate one on the Offline policies tab before enabling offline transactions for this store.'}</p>
      </div>
      <SearchChoice label="Currency" required value={storeEditor.value.currencyCode} options={references.currencies.map(item => ({ ...item, selectionId: item.code }))} onChange={value => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, currencyCode: value } })} placeholder="Search configured currencies" /><Field label="Time zone *"><Input value={storeEditor.value.timeZoneId} onChange={e => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, timeZoneId: e.target.value } })} /></Field>
      {references.dimensions.map(dimension => <SelectField key={dimension.definitionId} label={`${dimension.name} default`} value={storeEditor.value.dimensionDefaults.find(item => item.financeDimensionDefinitionId === dimension.definitionId)?.financeDimensionValueId || 'none'} onChange={value => { const without = storeEditor.value.dimensionDefaults.filter(item => item.financeDimensionDefinitionId !== dimension.definitionId); setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, dimensionDefaults: value === 'none' ? without : [...without, { financeDimensionDefinitionId: dimension.definitionId, financeDimensionValueId: value }] } }); }} options={[{ value: 'none', label: 'No default' }, ...dimension.values.map(item => ({ value: item.id, label: `${item.code} · ${item.name}` }))]} />)}
      <div className="md:col-span-2"><Field label="Notes"><Textarea value={storeEditor.value.notes || ''} onChange={e => setStoreEditor({ ...storeEditor, value: { ...storeEditor.value, notes: e.target.value } })} /></Field></div>
    </div>}<DialogFooter><Button variant="outline" onClick={() => setStoreEditor(undefined)}>Cancel</Button><Button onClick={() => void saveStore()} disabled={saving}>{saving ? 'Saving...' : 'Save store'}</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={!!tillEditor} onOpenChange={open => !open && setTillEditor(undefined)}><DialogContent className="max-w-3xl"><DialogHeader><DialogTitle>{tillEditor?.id ? 'Edit' : 'Create'} Mobile POS till</DialogTitle><DialogDescription>A CashTill liquidity account can belong to only one active Mobile POS till.</DialogDescription></DialogHeader>{tillEditor && <div className="space-y-5"><div className="grid gap-4 md:grid-cols-2">
      <SearchChoice label="Store" required value={tillEditor.value.mobilePosStoreId} options={stores.map(item => ({ id: item.id, code: item.code, name: item.name, secondary: STORE_STATUS[item.status] }))} onChange={value => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, mobilePosStoreId: value } })} />
      <SearchChoice label="CashTill liquidity account" required value={tillEditor.value.liquidityAccountId} options={references.cashTills} onChange={value => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, liquidityAccountId: value } })} />
      <Field label="Till number *"><Input value={tillEditor.value.tillNumber} onChange={e => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, tillNumber: e.target.value } })} /></Field><Field label="Name *"><Input value={tillEditor.value.name} onChange={e => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, name: e.target.value } })} /></Field>
      <SelectField label="Status" value={String(tillEditor.value.status)} onChange={value => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, status: Number(value) as 1 | 2 | 3 | 4 } })} options={Object.entries(STORE_STATUS).map(([value, label]) => ({ value, label }))} />
    </div><div><Label>Accepted payment methods</Label><div className="mt-2 divide-y rounded-md border">{references.paymentMethods.map((method, index) => { const configured = tillEditor.value.paymentMethods.find(item => item.paymentMethodId === method.id); return <div key={method.id} className="grid gap-3 p-3 md:grid-cols-[minmax(0,1fr)_auto_auto_auto] md:items-center"><label className="flex items-center gap-2"><Checkbox checked={!!configured} onCheckedChange={checked => { const remaining = tillEditor.value.paymentMethods.filter(item => item.paymentMethodId !== method.id); setTillEditor({ ...tillEditor, value: { ...tillEditor.value, paymentMethods: checked ? [...remaining, { paymentMethodId: method.id, allowOnline: true, allowOffline: false, requireExternalAuthorizationReference: false, displayOrder: index }] : remaining } }); }} /><span><strong>{method.code}</strong> · {method.name}<small className="block text-muted-foreground">{method.secondary}</small></span></label>{configured && <><Toggle label="Online" checked={configured.allowOnline} onChange={checked => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, paymentMethods: tillEditor.value.paymentMethods.map(item => item.paymentMethodId === method.id ? { ...item, allowOnline: checked } : item) } })} /><Toggle label="Offline" checked={configured.allowOffline} onChange={checked => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, paymentMethods: tillEditor.value.paymentMethods.map(item => item.paymentMethodId === method.id ? { ...item, allowOffline: checked } : item) } })} /><Toggle label="External reference" checked={configured.requireExternalAuthorizationReference} onChange={checked => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, paymentMethods: tillEditor.value.paymentMethods.map(item => item.paymentMethodId === method.id ? { ...item, requireExternalAuthorizationReference: checked } : item) } })} /></>}</div>; })}</div></div><Field label="Notes"><Textarea value={tillEditor.value.notes || ''} onChange={e => setTillEditor({ ...tillEditor, value: { ...tillEditor.value, notes: e.target.value } })} /></Field></div>}<DialogFooter><Button variant="outline" onClick={() => setTillEditor(undefined)}>Cancel</Button><Button onClick={() => void saveTill()} disabled={saving}>{saving ? 'Saving...' : 'Save till'}</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={!!policyEditor} onOpenChange={open => !open && setPolicyEditor(undefined)}><DialogContent className="max-w-3xl"><DialogHeader><DialogTitle>{policyEditor?.id ? 'Edit' : 'Create'} offline policy</DialogTitle><DialogDescription>Offline operation remains denied unless a store policy and a valid device grant explicitly allow it.</DialogDescription></DialogHeader>{policyEditor && <div className="grid gap-4 md:grid-cols-2"><Field label="Policy name *"><Input value={policyEditor.value.name} onChange={e => setPolicyEditor({ ...policyEditor, value: { ...policyEditor.value, name: e.target.value } })} /></Field><Toggle label="Active" checked={policyEditor.value.isActive} onChange={checked => setPolicyEditor({ ...policyEditor, value: { ...policyEditor.value, isActive: checked } })} />
      {([['Authorization window (minutes)', 'authorizationWindowMinutes'], ['Maximum offline age (minutes)', 'maximumOfflineAgeMinutes'], ['Per transaction limit', 'maximumTransactionAmount'], ['Aggregate limit', 'maximumAggregateAmount'], ['Transaction count limit', 'maximumTransactionCount']] as const).map(([label, key]) => <Field key={key} label={label}><Input type="number" min="0" value={policyEditor.value[key] ?? ''} onChange={e => setPolicyEditor({ ...policyEditor, value: { ...policyEditor.value, [key]: e.target.value === '' ? undefined : Number(e.target.value) } })} /></Field>)}
      <div className="md:col-span-2 grid gap-3 rounded-md border p-4 sm:grid-cols-2">{([['Cash sale', 'allowCashSale'], ['Cash receipt', 'allowCashReceipt'], ['Partial payment', 'allowPartialPayment'], ['Returns', 'allowReturns'], ['Reversals', 'allowReversals'], ['Provisional receipt', 'allowProvisionalReceipt'], ['Day-end with pending sync', 'allowDayEndSubmissionWithPendingSync'], ['Require electronic tender reference', 'requireExternalReferenceForElectronicTender']] as const).map(([label, key]) => <Toggle key={key} label={label} checked={policyEditor.value[key]} onChange={checked => setPolicyEditor({ ...policyEditor, value: { ...policyEditor.value, [key]: checked } })} />)}</div>
    </div>}<DialogFooter><Button variant="outline" onClick={() => setPolicyEditor(undefined)}>Cancel</Button><Button onClick={() => void savePolicy()} disabled={saving}>{saving ? 'Saving...' : 'Save policy'}</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={!!deviceEditor} onOpenChange={open => !open && setDeviceEditor(undefined)}><DialogContent><DialogHeader><DialogTitle>Approve or reassign device</DialogTitle><DialogDescription>{deviceEditor?.device.deviceName}. Assigning a device increments its revocation epoch and invalidates previous offline authority.</DialogDescription></DialogHeader>{deviceEditor && <div className="space-y-4"><SearchChoice label="Active store" required value={deviceEditor.storeId} options={activeStores} onChange={value => setDeviceEditor({ ...deviceEditor, storeId: value, tillId: '' })} /><SearchChoice label="Active till" required value={deviceEditor.tillId} options={activeTills} onChange={value => setDeviceEditor({ ...deviceEditor, tillId: value })} /><Field label="Approval reason *"><Textarea value={deviceEditor.reason} onChange={e => setDeviceEditor({ ...deviceEditor, reason: e.target.value })} /></Field></div>}<DialogFooter><Button variant="outline" onClick={() => setDeviceEditor(undefined)}>Cancel</Button><Button onClick={() => void saveDevice()} disabled={saving}>{saving ? 'Saving...' : 'Approve assignment'}</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={assignmentOpen} onOpenChange={setAssignmentOpen}><DialogContent><DialogHeader><DialogTitle>Assign user to store</DialogTitle><DialogDescription>Only active tenant users and active stores are selectable.</DialogDescription></DialogHeader><div className="space-y-4"><SearchChoice label="User" required value={assignment.userId} options={references.users} onChange={value => setAssignment({ ...assignment, userId: value })} /><SearchChoice label="Active store" required value={assignment.storeId} options={activeStores} onChange={value => setAssignment({ ...assignment, storeId: value })} /><div className="grid gap-4 sm:grid-cols-2"><Field label="Effective from"><Input type="datetime-local" value={assignment.from} onChange={e => setAssignment({ ...assignment, from: e.target.value })} /></Field><Field label="Effective to"><Input type="datetime-local" value={assignment.to} onChange={e => setAssignment({ ...assignment, to: e.target.value })} /></Field></div><Field label="Reason *"><Textarea value={assignment.reason} onChange={e => setAssignment({ ...assignment, reason: e.target.value })} /></Field></div><DialogFooter><Button variant="outline" onClick={() => setAssignmentOpen(false)}>Cancel</Button><Button onClick={() => void saveAssignment()} disabled={saving}>{saving ? 'Saving...' : 'Save assignment'}</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={!!depositProposal} onOpenChange={open => !open && setDepositProposal(undefined)}><DialogContent><DialogHeader><DialogTitle>Create governed bank deposit proposal</DialogTitle><DialogDescription>The eligible custody entries from this finalized till session will be reserved in a draft Finance bank deposit. Finance submission, approval, posting, and bank confirmation remain unchanged.</DialogDescription></DialogHeader>{depositProposal && <div className="space-y-4"><div className="rounded-md border bg-muted/30 p-3 text-sm"><p className="font-medium">{depositProposal.submission.session.sessionNumber} · {depositProposal.submission.session.tillName}</p><p>Counted close: {money(depositProposal.submission.session.countedClosingAmount, depositProposal.submission.session.currency)}</p></div><SearchChoice label="Destination bank account" required value={depositProposal.bankAccountId} options={references.bankAccounts} onChange={value => setDepositProposal({ ...depositProposal, bankAccountId: value })} /><Field label="Deposit date *"><Input type="date" value={depositProposal.depositDate} onChange={event => setDepositProposal({ ...depositProposal, depositDate: event.target.value })} /></Field><Field label="Deposit slip / preparation reference *"><Input value={depositProposal.depositReference} onChange={event => setDepositProposal({ ...depositProposal, depositReference: event.target.value })} /></Field><Field label="Notes"><Textarea value={depositProposal.notes} onChange={event => setDepositProposal({ ...depositProposal, notes: event.target.value })} /></Field></div>}<DialogFooter><Button variant="outline" onClick={() => setDepositProposal(undefined)}>Cancel</Button><Button disabled={saving || !depositProposal?.bankAccountId || (depositProposal?.depositReference.trim().length || 0) < 3} onClick={() => void createDepositProposal()}>{saving ? 'Creating...' : 'Create Finance proposal'}</Button></DialogFooter></DialogContent></Dialog>

    <ConfirmationDialog open={!!reviewAction} onOpenChange={open => !open && setReviewAction(undefined)} title={reviewAction?.mode === 'approve' ? 'Approve and finalize till close' : 'Return till for recount'} description={reviewAction?.mode === 'approve' ? 'Finance will close the till only after the retained Mobile POS sync evidence passes final validation.' : 'The cashier session will reopen and the prior submission will remain in the audit trail.'} confirmText={reviewAction?.mode === 'approve' ? 'Approve close' : 'Return for recount'} variant={reviewAction?.mode === 'return' ? 'destructive' : 'default'} isLoading={saving} confirmDisabled={reviewAction?.mode === 'return' && (reviewAction.comments.trim().length < 10)} onConfirm={runTillReview}>{reviewAction && <Field label={reviewAction.mode === 'approve' ? 'Reviewer comments' : 'Recount instructions *'}><Textarea value={reviewAction.comments} onChange={event => setReviewAction({ ...reviewAction, comments: event.target.value })} /></Field>}</ConfirmationDialog>

    <ConfirmationDialog open={!!syncResolution} onOpenChange={open => !open && setSyncResolution(undefined)} title="Resolve pending-sync exception" description="Use this only after an independent HQ review confirms why the retained pending mutations may proceed to closure." confirmText="Record resolution" isLoading={saving} confirmDisabled={!syncResolution || syncResolution.reason.trim().length < 20} onConfirm={resolveTillSync}>{syncResolution && <Field label="Independent resolution reason *"><Textarea value={syncResolution.reason} onChange={event => setSyncResolution({ ...syncResolution, reason: event.target.value })} /></Field>}</ConfirmationDialog>

    <ConfirmationDialog open={!!revokeTarget} onOpenChange={open => !open && setRevokeTarget(undefined)} title="Revoke Mobile POS device" description="This immediately blocks bootstrap and heartbeat requests and revokes active offline grants." confirmText="Revoke device" variant="destructive" isLoading={saving} confirmDisabled={!revokeReason.trim()} onConfirm={async () => { if (!revokeTarget) return false; setSaving(true); try { await mobilePosAdminService.revokeDevice(revokeTarget.id, { reason: revokeReason, rowVersion: revokeTarget.rowVersion }); toast.success('Device revoked.'); setRevokeTarget(undefined); await load(); } catch (error) { toast.error(errorMessage(error, 'Failed to revoke the device.')); return false; } finally { setSaving(false); } }}><Field label="Revocation reason *"><Textarea value={revokeReason} onChange={event => setRevokeReason(event.target.value)} /></Field></ConfirmationDialog>
  </div>;
}

function Section({ title, description, action, children }: { title: string; description: string; action?: React.ReactNode; children: React.ReactNode }) { return <Card><CardHeader><div className="flex flex-col justify-between gap-3 md:flex-row md:items-center"><div><CardTitle>{title}</CardTitle><CardDescription>{description}</CardDescription></div>{action}</div></CardHeader><CardContent>{children}</CardContent></Card>; }
function Table({ headers, rows }: { headers: string[]; rows: React.ReactNode[][] }) { return <div className="overflow-x-auto rounded-md border"><table className="w-full text-sm"><thead className="bg-muted/50 text-left"><tr>{headers.map((header, index) => <th key={`${header}-${index}`} className="whitespace-nowrap px-4 py-3 font-medium">{header}</th>)}</tr></thead><tbody className="divide-y">{rows.map((row, rowIndex) => <tr key={rowIndex} className="align-top">{row.map((cell, cellIndex) => <td key={cellIndex} className="px-4 py-3">{cell}</td>)}</tr>)}{!rows.length && <tr><td colSpan={headers.length} className="px-4 py-10 text-center text-muted-foreground">No records yet.</td></tr>}</tbody></table></div>; }
function Field({ label, children }: { label: string; children: React.ReactNode }) { return <div className="space-y-2"><Label>{label}</Label>{children}</div>; }
function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) { return <label className="flex items-center justify-between gap-3 text-sm"><span>{label}</span><Switch checked={checked} onCheckedChange={onChange} /></label>; }
function SelectField({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: Array<{ value: string; label: string }> }) { return <Field label={label}><Select value={value} onValueChange={onChange}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{options.map(option => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}</SelectContent></Select></Field>; }
function Metric({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) { return <div className="flex items-center gap-3 rounded-md border p-4"><div className="text-primary">{icon}</div><div><p className="text-2xl font-bold">{value}</p><p className="text-sm text-muted-foreground">{label}</p></div></div>; }
