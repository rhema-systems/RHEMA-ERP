'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { CheckCircle2, Download, FileUp, RefreshCw, Save, Send, ShieldCheck, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { quantitySurveyContractClaimService as service, type ContractClaim, type ContractClaimType, type ContractClaimWorkspace } from '@/services/quantity-survey-contract-claim.service';

type Props = { projectId: string; external?: boolean };
const empty: ContractClaimWorkspace = { contracts: [], variations: [], extensionsOfTime: [], approvedBoqVersions: [], claims: [] };
const claimTypes: ContractClaimType[] = ['ExtensionOfTime', 'LossAndExpense', 'Variation', 'Daywork', 'AdditionalWork', 'Other'];
const label = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
const money = (value: number, currency = 'GHS') => new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 2 }).format(value || 0);

export function QuantitySurveyContractClaimsWorkspace({ projectId, external = false }: Props) {
  const { hasPermission } = useAuth();
  const canRead = external || hasPermission('quantity-survey.workspace.read');
  const canManage = external || hasPermission('quantity-survey.claims.manage');
  const canApprove = !external && hasPermission('quantity-survey.transactions.approve');
  const requests = useRef<Record<string, string>>({});
  const [workspace, setWorkspace] = useState(empty); const [selectedId, setSelectedId] = useState('');
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [contractId, setContractId] = useState(''); const [claimType, setClaimType] = useState<ContractClaimType>('LossAndExpense');
  const [boqId, setBoqId] = useState('none'); const [sourceId, setSourceId] = useState('none');
  const [title, setTitle] = useState(''); const [basis, setBasis] = useState(''); const [claimedAmount, setClaimedAmount] = useState(0);
  const [reason, setReason] = useState(''); const [assessedAmount, setAssessedAmount] = useState(0);
  const [evidenceTitle, setEvidenceTitle] = useState(''); const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [settledTotal, setSettledTotal] = useState(0); const [settlementReference, setSettlementReference] = useState('');
  const [settlementDate, setSettlementDate] = useState(new Date().toISOString().slice(0, 10));
  const selected = useMemo(() => workspace.claims.find(value => value.id === selectedId), [selectedId, workspace.claims]);
  const selectedContract = workspace.contracts.find(value => value.id === contractId);
  const needsVariation = ['Variation', 'Daywork', 'AdditionalWork'].includes(claimType);
  const needsExtension = claimType === 'ExtensionOfTime';
  const sourceOptions = needsExtension ? workspace.extensionsOfTime : needsVariation
    ? workspace.variations.filter(value => claimType === 'Variation' || value.type === claimType) : [];
  const requestId = (key: string) => requests.current[key] ?? (requests.current[key] = crypto.randomUUID());
  const complete = (key: string) => { delete requests.current[key]; };

  const load = useCallback(async (preferred?: string) => {
    setLoading(true);
    try { const value = await service.workspace(projectId, external); setWorkspace(value); setSelectedId(preferred ?? value.claims[0]?.id ?? ''); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Contract claims could not be loaded.'); }
    finally { setLoading(false); }
  }, [external, projectId]);
  useEffect(() => { if (canRead) void load(); }, [canRead, load]);

  const select = (value: ContractClaim) => {
    setSelectedId(value.id); setContractId(value.contractId); setClaimType(value.claimType);
    setBoqId(value.approvedBoqVersionId ?? 'none'); setSourceId(value.variationOrderId ?? value.extensionOfTimeId ?? 'none');
    setTitle(value.title); setBasis(value.basis); setClaimedAmount(value.claimedAmount); setAssessedAmount(value.qsAssessedAmount ?? value.claimedAmount);
    setSettledTotal(value.settledAmount); setSettlementReference(value.settlementReference ?? '');
    setSettlementDate(value.settlementDate?.slice(0, 10) ?? new Date().toISOString().slice(0, 10));
  };
  const run = async (key: string, operation: () => Promise<ContractClaim>, message: string) => {
    setBusy(true); try { const value = await operation(); complete(key); setReason(''); toast.success(message); await load(value.id); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'The claim action failed.'); } finally { setBusy(false); }
  };
  const save = async () => {
    if (!external || !contractId || title.trim().length < 3 || basis.trim().length < 10 || claimedAmount <= 0 || ((needsVariation || needsExtension) && sourceId === 'none')) {
      toast.error('Select the controlled contract/source and complete the claim title, basis and positive amount.'); return;
    }
    const key = `save:${selected?.id ?? 'new'}:${contractId}:${claimType}:${boqId}:${sourceId}:${title}:${basis}:${claimedAmount}`;
    await run(key, () => service.saveExternal(projectId, { id: selected?.id ?? null, clientRequestId: requestId(key), contractId,
      approvedBoqVersionId: boqId === 'none' ? null : boqId, variationOrderId: needsVariation ? sourceId : null,
      extensionOfTimeId: needsExtension ? sourceId : null, claimType, title: title.trim(), basis: basis.trim(), claimedAmount,
      rowVersion: selected?.rowVersion ?? null }), 'Claim Draft saved.');
  };
  const upload = async () => {
    if (!selected || !evidenceFile || evidenceTitle.trim().length < 3) { toast.error('Select an editable claim, evidence title and file.'); return; }
    const key = `evidence:${selected.id}:${evidenceFile.name}:${evidenceFile.size}:${evidenceTitle}`; setBusy(true);
    try { await service.uploadEvidence(projectId, selected.id, requestId(key), evidenceTitle.trim(), evidenceFile, external); complete(key); setEvidenceTitle(''); setEvidenceFile(null); toast.success('Clean evidence retained in the central DMS.'); await load(selected.id); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Evidence upload failed.'); } finally { setBusy(false); }
  };
  const action = async (kind: 'submit' | 'vet' | 'submitApproval' | 'approve' | 'reject' | 'dispute' | 'acceptDispute' | 'rejectDispute' | 'settle') => {
    if (!selected || reason.trim().length < 5) { toast.error('Enter an action reason of at least 5 characters.'); return; }
    const key = `${kind}:${selected.id}:${selected.rowVersion}:${reason}:${assessedAmount}:${settledTotal}:${settlementReference}:${settlementDate}`;
    const request = { clientRequestId: requestId(key), rowVersion: selected.rowVersion, reason: reason.trim() };
    const operations = {
      submit: () => service.submitExternal(projectId, selected.id, request), dispute: () => service.disputeExternal(projectId, selected.id, request),
      vet: () => service.vet(selected.id, { ...request, assessedAmount }), submitApproval: () => service.submitApproval(selected.id, request),
      approve: () => service.approve(selected.id, request), reject: () => service.reject(selected.id, request),
      acceptDispute: () => service.resolveDispute(selected.id, true, request), rejectDispute: () => service.resolveDispute(selected.id, false, request),
      settle: () => service.settle(selected.id, { ...request, amount: settledTotal, settlementReference: settlementReference.trim(), settlementDate: new Date(`${settlementDate}T00:00:00Z`).toISOString() }),
    };
    await run(key, operations[kind], `Claim ${label(kind).toLowerCase()} completed.`);
  };
  const download = async (evidenceId: string, fileName: string) => {
    if (!selected) return; try { const blob = await service.evidenceContent(projectId, selected.id, evidenceId, external); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click(); URL.revokeObjectURL(url); }
    catch (error) { toast.error(error instanceof Error ? error.message : 'Evidence could not be opened.'); }
  };
  if (!canRead) return null;
  const editable = external && (!selected || ['Draft', 'Rejected'].includes(selected.status));
  return <Card data-testid={external ? 'qs-contractor-claims-portal' : 'qs-contract-claims-workspace'}>
    <CardHeader className="pb-3"><div className="flex flex-wrap items-center justify-between gap-2"><div><CardTitle>Contract claims</CardTitle><CardDescription>{external ? 'Submit governed claims and monitor QS decisions, disputes and settlement.' : 'Vet contractor claims, route approval, resolve disputes and record settlement.'}</CardDescription></div><Button variant="outline" size="sm" onClick={() => void load(selectedId)} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div></CardHeader>
    <CardContent className="space-y-4">
      <div className="flex flex-wrap gap-2">{workspace.claims.map(value => <Button key={value.id} variant={selectedId === value.id ? 'default' : 'outline'} size="sm" onClick={() => select(value)}>{value.claimNumber}<Badge className="ml-2" variant="secondary">{label(value.status)}</Badge></Button>)}{external && <Button size="sm" variant={!selectedId ? 'default' : 'outline'} onClick={() => { setSelectedId(''); setContractId(''); setClaimType('LossAndExpense'); setBoqId('none'); setSourceId('none'); setTitle(''); setBasis(''); setClaimedAmount(0); }}>New claim</Button>}</div>
      {external && <div className="grid gap-3 rounded-md border p-4 md:grid-cols-4">
        <div className="grid gap-2 md:col-span-2"><Label>Active Works contract</Label><Select disabled={!editable} value={contractId} onValueChange={setContractId}><SelectTrigger><SelectValue placeholder="Select contract" /></SelectTrigger><SelectContent>{workspace.contracts.map(value => <SelectItem key={value.id} value={value.id}>{value.number} · {value.contractor}</SelectItem>)}</SelectContent></Select></div>
        <div className="grid gap-2"><Label>Claim type</Label><Select disabled={!editable} value={claimType} onValueChange={value => { setClaimType(value as ContractClaimType); setSourceId('none'); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{claimTypes.map(value => <SelectItem key={value} value={value}>{label(value)}</SelectItem>)}</SelectContent></Select></div>
        <div className="grid gap-2"><Label>Approved BoQ version</Label><Select disabled={!editable} value={boqId} onValueChange={setBoqId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Not applicable</SelectItem>{workspace.approvedBoqVersions.map(value => <SelectItem key={value.id} value={value.id}>Version {value.versionNumber}</SelectItem>)}</SelectContent></Select></div>
        {(needsVariation || needsExtension) && <div className="grid gap-2 md:col-span-2"><Label>{needsExtension ? 'Extension of time' : 'Approved variation'}</Label><Select disabled={!editable} value={sourceId} onValueChange={setSourceId}><SelectTrigger><SelectValue placeholder="Select governed source" /></SelectTrigger><SelectContent>{sourceOptions.map(value => <SelectItem key={value.id} value={value.id}>{value.reference} · {value.title}</SelectItem>)}</SelectContent></Select></div>}
        <div className="grid gap-2 md:col-span-2"><Label>Claim title</Label><Input disabled={!editable} value={title} onChange={event => setTitle(event.target.value)} /></div>
        <div className="grid gap-2"><Label>Claimed amount ({selectedContract?.currency ?? selected?.currency ?? 'contract currency'})</Label><Input disabled={!editable} type="number" min="0.01" step="0.01" value={claimedAmount || ''} onChange={event => setClaimedAmount(Number(event.target.value))} /></div>
        <div className="grid gap-2 md:col-span-4"><Label>Contractual basis and particulars</Label><Textarea disabled={!editable} rows={3} value={basis} onChange={event => setBasis(event.target.value)} /></div>
        {editable && <div className="md:col-span-4 flex justify-end"><Button onClick={() => void save()} disabled={busy}><Save className="mr-2 h-4 w-4" />Save Draft</Button></div>}
      </div>}
      {selected && <div className="space-y-4 rounded-md border p-4">
        <div className="grid gap-3 md:grid-cols-5"><div><Label>Contractor</Label><p>{selected.contractorName}</p></div><div><Label>Claimed</Label><p>{money(selected.claimedAmount, selected.currency)}</p></div><div><Label>QS assessed</Label><p>{selected.qsAssessedAmount == null ? 'Pending' : money(selected.qsAssessedAmount, selected.currency)}</p></div><div><Label>Approved / rejected</Label><p>{money(selected.approvedAmount ?? 0, selected.currency)} / {money(selected.rejectedAmount ?? 0, selected.currency)}</p></div><div><Label>Dispute / settlement</Label><p>{label(selected.disputeStatus)} · {label(selected.settlementStatus)}</p></div></div>
        <div><Label>Basis</Label><p className="whitespace-pre-wrap text-sm text-muted-foreground">{selected.basis}</p></div>
        <div className="space-y-2"><Label>Central DMS evidence</Label>{selected.evidence.length ? selected.evidence.map(value => <div key={value.id} className="flex items-center justify-between rounded border p-2 text-sm"><span>{value.title} · {value.fileName}</span><Button size="sm" variant="ghost" onClick={() => void download(value.id, value.fileName)}><Download className="mr-2 h-4 w-4" />Open</Button></div>) : <p className="text-sm text-muted-foreground">No evidence attached.</p>}</div>
        {canManage && ['Draft', 'Rejected'].includes(selected.status) && <div className="grid gap-2 md:grid-cols-4"><Input placeholder="Evidence title" value={evidenceTitle} onChange={event => setEvidenceTitle(event.target.value)} /><Input type="file" onChange={event => setEvidenceFile(event.target.files?.[0] ?? null)} /><Button variant="outline" onClick={() => void upload()} disabled={busy}><FileUp className="mr-2 h-4 w-4" />Upload evidence</Button></div>}
        <div className="grid gap-3 md:grid-cols-4">{!external && selected.status === 'Submitted' && <div className="grid gap-2"><Label>QS assessed amount</Label><Input type="number" min="0" max={selected.claimedAmount} value={assessedAmount} onChange={event => setAssessedAmount(Number(event.target.value))} /></div>}{!external && selected.status === 'Approved' && <><div className="grid gap-2"><Label>Settled total</Label><Input type="number" min={selected.settledAmount} max={selected.approvedAmount ?? 0} value={settledTotal} onChange={event => setSettledTotal(Number(event.target.value))} /></div><div className="grid gap-2"><Label>Settlement reference</Label><Input value={settlementReference} onChange={event => setSettlementReference(event.target.value)} /></div><div className="grid gap-2"><Label>Settlement date</Label><Input type="date" value={settlementDate} onChange={event => setSettlementDate(event.target.value)} /></div></>}
          <div className="grid gap-2 md:col-span-4"><Label>Action reason / review note</Label><Textarea value={reason} onChange={event => setReason(event.target.value)} /></div>
        </div>
        <div className="flex flex-wrap justify-end gap-2">{external && ['Draft', 'Rejected'].includes(selected.status) && <Button onClick={() => void action('submit')} disabled={busy}><Send className="mr-2 h-4 w-4" />Submit to QS</Button>}{external && ['Approved', 'Rejected'].includes(selected.status) && selected.disputeStatus === 'None' && <Button variant="outline" onClick={() => void action('dispute')} disabled={busy}>Open dispute</Button>}{!external && canManage && selected.status === 'Submitted' && <Button onClick={() => void action('vet')} disabled={busy}><ShieldCheck className="mr-2 h-4 w-4" />Record QS vetting</Button>}{!external && canManage && selected.status === 'Vetted' && <Button onClick={() => void action('submitApproval')} disabled={busy}><Send className="mr-2 h-4 w-4" />Submit approval</Button>}{canApprove && selected.status === 'PendingApproval' && <><Button onClick={() => void action('approve')} disabled={busy}><CheckCircle2 className="mr-2 h-4 w-4" />Approve</Button><Button variant="destructive" onClick={() => void action('reject')} disabled={busy}><XCircle className="mr-2 h-4 w-4" />Reject</Button></>}{canApprove && selected.disputeStatus === 'Open' && <><Button variant="outline" onClick={() => void action('acceptDispute')} disabled={busy}>Accept dispute</Button><Button variant="outline" onClick={() => void action('rejectDispute')} disabled={busy}>Reject dispute</Button></>}{!external && canManage && selected.status === 'Approved' && (selected.approvedAmount ?? 0) > selected.settledAmount && <Button onClick={() => void action('settle')} disabled={busy}>Record settlement</Button>}</div>
      </div>}
      {!loading && !workspace.contracts.length && <p className="text-sm text-muted-foreground">No active Procurement Works contract is available for this project and contractor.</p>}
    </CardContent>
  </Card>;
}
