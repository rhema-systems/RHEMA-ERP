'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Separator } from '@/components/ui/separator';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger,
} from '@/components/ui/dialog';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';
import {
  FileText, ArrowLeft, Pause, Play,
  RefreshCw, Ban, AlertTriangle, Calendar, Building2, Home
} from 'lucide-react';
import { toast } from 'sonner';
import {
  salesAgreementService, type SalesAgreementDetailDto,
  type RenewAgreementDto
} from '@/services/salesAgreementService';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
  Draft: { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
  PendingApproval: { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
  Active: { variant: 'default', className: 'bg-green-100 text-green-800' },
  Expiring: { variant: 'secondary', className: 'bg-orange-100 text-orange-800' },
  Renewed: { variant: 'default', className: 'bg-blue-100 text-blue-800' },
  Expired: { variant: 'outline', className: 'bg-slate-100 text-slate-700' },
  Terminated: { variant: 'destructive', className: 'bg-red-100 text-red-800' },
  Suspended: { variant: 'secondary', className: 'bg-amber-100 text-amber-800' },
};

const MILESTONE_STATUS_COLORS: Record<string, string> = {
  Pending: 'bg-gray-100 text-gray-700',
  Completed: 'bg-blue-100 text-blue-700',
  Paid: 'bg-green-100 text-green-700',
  Overdue: 'bg-red-100 text-red-700',
};

export default function SalesAgreementDetailPage() {
  const params = useParams();
  const router = useRouter();
  const agreementId = params.id as string;

  const [agreement, setAgreement] = useState<SalesAgreementDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);

  // Dialog states
  const [terminateDialogOpen, setTerminateDialogOpen] = useState(false);
  const [terminateReason, setTerminateReason] = useState('');
  const [suspendDialogOpen, setSuspendDialogOpen] = useState(false);
  const [suspendReason, setSuspendReason] = useState('');
  const [renewDialogOpen, setRenewDialogOpen] = useState(false);
  const [renewEndDate, setRenewEndDate] = useState('');
  const [renewValue, setRenewValue] = useState('');
  const [renewTerms, setRenewTerms] = useState('');

  useEffect(() => { if (agreementId) loadAgreement(); }, [agreementId]);

  const loadAgreement = async () => {
    try {
      setLoading(true);
      const data = await salesAgreementService.getAgreementById(agreementId);
      setAgreement(data);
    } catch (error) {
      toast.error('Failed to load agreement');
    } finally {
      setLoading(false);
    }
  };

  const handleAction = async (action: () => Promise<SalesAgreementDetailDto>, msg: string) => {
    try {
      setActionLoading(true);
      const updated = await action();
      setAgreement(updated);
      toast.success(msg);
    } catch (error: any) {
      toast.error(error.message || 'Action failed');
    } finally {
      setActionLoading(false);
    }
  };

  const handleWorkflowSubmit = async () => {
    const updated = await salesAgreementService.submitForApproval(agreementId);
    setAgreement(updated);
  };

  const handleWorkflowApprove = async (comments: string) => {
    const updated = await salesAgreementService.processApproval(agreementId, { isApproved: true, comments });
    setAgreement(updated);
  };

  const handleWorkflowReject = async (comments: string) => {
    const updated = await salesAgreementService.processApproval(agreementId, { isApproved: false, comments });
    setAgreement(updated);
  };

  const handleSuspend = () => handleAction(() => salesAgreementService.suspend(agreementId, suspendReason), 'Agreement suspended').then(() => setSuspendDialogOpen(false));
  const handleResume = () => handleAction(() => salesAgreementService.resume(agreementId), 'Agreement resumed');
  const handleTerminate = () => handleAction(() => salesAgreementService.terminate(agreementId, terminateReason), 'Agreement terminated').then(() => setTerminateDialogOpen(false));
  const handleRenew = () => handleAction(() => salesAgreementService.renew(agreementId, {
    newEndDate: renewEndDate,
    newValue: renewValue ? parseFloat(renewValue) : undefined,
    renewalTerms: renewTerms || undefined,
  }), 'Agreement renewed').then(() => setRenewDialogOpen(false));

  const handleMilestoneUpdate = async (milestoneId: string, status: string) => {
    try {
      await salesAgreementService.updateMilestone(milestoneId, { status });
      toast.success(`Milestone marked as ${status}`);
      loadAgreement();
    } catch (error: any) {
      toast.error(error.message);
    }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const formatCurrency = (n: number) => `${agreement?.currency || 'GHS'} ${n.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  const formatLabel = (value?: string) => value ? value.replace(/([A-Z])/g, ' $1').trim() : '-';
  const getStatusBadge = (s: string) => {
    const c = STATUS_CONFIG[s] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{s.replace(/([A-Z])/g, ' $1').trim()}</Badge>;
  };

  if (loading) return <div className="container mx-auto py-6 text-center py-16"><FileText className="h-12 w-12 animate-pulse mx-auto mb-4 text-purple-500" /><p className="text-gray-500">Loading...</p></div>;
  if (!agreement) return <div className="container mx-auto py-6 text-center py-16"><AlertTriangle className="h-12 w-12 mx-auto mb-4 text-yellow-500" /><p className="text-gray-500">Agreement not found</p><Button variant="outline" className="mt-4" onClick={() => router.push('/sales/agreements')}><ArrowLeft className="h-4 w-4 mr-2" />Back</Button></div>;

  const canSubmit = agreement.agreementStatus === 'Draft';
  const canApprove = agreement.agreementStatus === 'PendingApproval';
  const canSuspend = agreement.agreementStatus === 'Active';
  const canResume = agreement.agreementStatus === 'Suspended';
  const canTerminate = !['Terminated', 'Expired'].includes(agreement.agreementStatus);
  const canRenew = ['Active', 'Expiring'].includes(agreement.agreementStatus);
  const utilPct = agreement.agreedValue > 0 ? Math.round((agreement.utilizedValue / agreement.agreedValue) * 100) : 0;

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/sales/agreements')}><ArrowLeft className="h-4 w-4" /></Button>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2"><FileText className="h-6 w-6 text-purple-600" />{agreement.documentNumber}</h1>
            <div className="flex items-center gap-2 mt-1">
              {getStatusBadge(agreement.agreementStatus)}
              <span className="text-sm text-gray-500">•</span>
              <span className="text-sm text-gray-500">{agreement.customerName}</span>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <WorkflowApprovalActions
            entityType="SalesAgreement"
            entityId={agreementId}
            entityLabel="Sales Agreement"
            entityNumber={agreement.documentNumber}
            status={agreement.agreementStatus}
            showStepBadge
            loadWorkflowSummary
            canSubmit={canSubmit}
            canApproveReject={canApprove}
            onSubmit={handleWorkflowSubmit}
            onApprove={handleWorkflowApprove}
            onReject={handleWorkflowReject}
            onAfterAction={loadAgreement}
            onOpenWorkflows={() => router.push('/administration/workflow')}
          />
          {canRenew && (
            <Dialog open={renewDialogOpen} onOpenChange={setRenewDialogOpen}>
              <DialogTrigger asChild><Button variant="outline"><RefreshCw className="h-4 w-4 mr-2" />Renew</Button></DialogTrigger>
              <DialogContent>
                <DialogHeader><DialogTitle>Renew Agreement</DialogTitle><DialogDescription>Extend the agreement with new terms.</DialogDescription></DialogHeader>
                <div className="space-y-3">
                  <div><Label>New End Date *</Label><Input type="date" value={renewEndDate} onChange={(e) => setRenewEndDate(e.target.value)} /></div>
                  <div><Label>New Value (GHS)</Label><Input type="number" placeholder="Leave empty to keep current" value={renewValue} onChange={(e) => setRenewValue(e.target.value)} /></div>
                  <div><Label>Renewal Terms</Label><Textarea placeholder="Updated terms..." value={renewTerms} onChange={(e) => setRenewTerms(e.target.value)} /></div>
                </div>
                <DialogFooter><Button onClick={handleRenew} disabled={actionLoading || !renewEndDate}>Confirm Renewal</Button></DialogFooter>
              </DialogContent>
            </Dialog>
          )}
          {canSuspend && (
            <Dialog open={suspendDialogOpen} onOpenChange={setSuspendDialogOpen}>
              <DialogTrigger asChild><Button variant="outline"><Pause className="h-4 w-4 mr-2" />Suspend</Button></DialogTrigger>
              <DialogContent>
                <DialogHeader><DialogTitle>Suspend Agreement</DialogTitle></DialogHeader>
                <Textarea placeholder="Suspension reason..." value={suspendReason} onChange={(e) => setSuspendReason(e.target.value)} />
                <DialogFooter><Button onClick={handleSuspend} disabled={actionLoading}>Confirm Suspend</Button></DialogFooter>
              </DialogContent>
            </Dialog>
          )}
          {canResume && <Button variant="outline" onClick={handleResume} disabled={actionLoading}><Play className="h-4 w-4 mr-2" />Resume</Button>}
          {canTerminate && (
            <Dialog open={terminateDialogOpen} onOpenChange={setTerminateDialogOpen}>
              <DialogTrigger asChild><Button variant="destructive"><Ban className="h-4 w-4 mr-2" />Terminate</Button></DialogTrigger>
              <DialogContent>
                <DialogHeader><DialogTitle>Terminate Agreement</DialogTitle><DialogDescription>This action cannot be undone.</DialogDescription></DialogHeader>
                <Textarea placeholder="Termination reason..." value={terminateReason} onChange={(e) => setTerminateReason(e.target.value)} />
                <DialogFooter><Button variant="destructive" onClick={handleTerminate} disabled={actionLoading || !terminateReason}>Confirm</Button></DialogFooter>
              </DialogContent>
            </Dialog>
          )}
        </div>
      </div>

      {agreement.projectUnitContext && (
        <Card className="border-blue-200 bg-blue-50/40">
          <CardContent className="flex flex-col gap-4 pt-6 lg:flex-row lg:items-start lg:justify-between">
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-sm font-medium text-blue-700">
                <Building2 className="h-4 w-4" />
                Project-Linked Unit
              </div>
              <div>
                <p className="text-base font-semibold text-slate-900">
                  {agreement.projectUnitContext.projectCode}
                  {agreement.projectUnitContext.projectTitle ? ` • ${agreement.projectUnitContext.projectTitle}` : ''}
                </p>
                <p className="text-sm text-slate-600">
                  {agreement.projectUnitContext.projectUnitCode || agreement.projectUnitContext.projectUnitName}
                  {agreement.projectUnitContext.projectUnitCode && agreement.projectUnitContext.projectUnitName
                    ? ` • ${agreement.projectUnitContext.projectUnitName}`
                    : ''}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <Badge variant="outline">{formatLabel(agreement.projectUnitContext.projectUnitType)}</Badge>
                <Badge variant="outline">{formatLabel(agreement.projectUnitContext.projectUnitStatus)}</Badge>
                <Badge variant="secondary">{formatLabel(agreement.projectUnitContext.projectUnitCommercialStatus)}</Badge>
                <Badge variant="outline">{formatLabel(agreement.projectUnitContext.projectUnitHandoverStatus)}</Badge>
              </div>
              <div className="grid gap-3 text-sm text-slate-600 md:grid-cols-3">
                <div>
                  <p className="text-xs uppercase tracking-wide text-slate-500">Market Release</p>
                  <p className="font-medium text-slate-900">{agreement.projectUnitContext.isReleasedForMarket ? 'Released' : 'Not Released'}</p>
                </div>
                <div>
                  <p className="text-xs uppercase tracking-wide text-slate-500">Handover</p>
                  <p className="font-medium text-slate-900">{formatDate(agreement.projectUnitContext.handoverDate)}</p>
                </div>
                <div>
                  <p className="text-xs uppercase tracking-wide text-slate-500">Unit Name</p>
                  <p className="font-medium text-slate-900">{agreement.projectUnitContext.projectUnitName}</p>
                </div>
              </div>
            </div>
            <Button
              variant="outline"
              className="w-full lg:w-auto"
              onClick={() => {
                const projectId = agreement.projectUnitContext?.projectId;
                if (projectId) {
                  router.push(`/development/projects/${projectId}/units`);
                }
              }}
            >
              <Home className="mr-2 h-4 w-4" />
              Open Project Unit
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Details */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader><CardTitle>{agreement.agreementTitle}</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              <div><p className="text-sm text-gray-500">Type</p><Badge variant="outline">{agreement.agreementType.replace(/([A-Z])/g, ' $1').trim()}</Badge></div>
              <div><p className="text-sm text-gray-500">Start Date</p><p className="font-medium">{formatDate(agreement.startDate)}</p></div>
              <div><p className="text-sm text-gray-500">End Date</p><p className="font-medium">{formatDate(agreement.endDate)}</p></div>
              <div><p className="text-sm text-gray-500">Auto-Renew</p><p className="font-medium">{agreement.autoRenew ? `Yes (${agreement.renewalPeriodMonths}m)` : 'No'}</p></div>
              <div><p className="text-sm text-gray-500">Expiry Warning</p><p className="font-medium">{agreement.expiryWarningDays} days</p></div>
              {agreement.salesRepName && <div><p className="text-sm text-gray-500">Sales Rep</p><p className="font-medium">{agreement.salesRepName}</p></div>}
              {agreement.propertyReference && <div><p className="text-sm text-gray-500">Property Ref</p><p className="font-medium">{agreement.propertyReference}</p></div>}
              {agreement.propertyType && <div><p className="text-sm text-gray-500">Property Type</p><p className="font-medium">{agreement.propertyType}</p></div>}
              {agreement.propertyLocation && <div><p className="text-sm text-gray-500">Location</p><p className="font-medium">{agreement.propertyLocation}</p></div>}
            </div>
            {agreement.propertyDescription && <><Separator /><div><p className="text-sm text-gray-500">Property Description</p><p className="text-sm">{agreement.propertyDescription}</p></div></>}
            {agreement.pricingTerms && <div><p className="text-sm text-gray-500">Pricing Terms</p><p className="text-sm">{agreement.pricingTerms}</p></div>}
            {agreement.termsAndConditions && <><Separator /><div><p className="text-sm text-gray-500">Terms & Conditions</p><p className="text-sm whitespace-pre-wrap">{agreement.termsAndConditions}</p></div></>}
            {(agreement.notes || agreement.internalNotes) && (<><Separator /><div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {agreement.notes && <div><p className="text-sm text-gray-500">Notes</p><p className="text-sm">{agreement.notes}</p></div>}
              {agreement.internalNotes && <div><p className="text-sm text-gray-500">Internal Notes</p><p className="text-sm italic">{agreement.internalNotes}</p></div>}
            </div></>)}
            {agreement.terminationReason && <div className="bg-red-50 border border-red-200 rounded-lg p-4"><p className="text-sm font-semibold text-red-700">Termination Reason</p><p className="text-sm text-red-600">{agreement.terminationReason}</p><p className="text-xs text-gray-500 mt-1">Terminated: {formatDate(agreement.terminatedDate)} by {agreement.terminatedByName}</p></div>}
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader><CardTitle className="text-lg">Financial Summary</CardTitle></CardHeader>
            <CardContent className="space-y-3">
              <div className="flex justify-between"><span className="text-gray-500">Agreed Value</span><span className="font-bold text-lg text-purple-600">{formatCurrency(agreement.agreedValue)}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">Min Commitment</span><span>{formatCurrency(agreement.minimumCommitment)}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">Max Commitment</span><span>{formatCurrency(agreement.maximumCommitment)}</span></div>
              {agreement.discountPercentage && <div className="flex justify-between"><span className="text-gray-500">Discount</span><span>{agreement.discountPercentage}%</span></div>}
              <Separator />
              <div><p className="text-sm text-gray-500 mb-1">Utilization</p>
                <div className="flex items-center gap-3">
                  <div className="flex-1 bg-gray-200 rounded-full h-3"><div className="bg-purple-500 h-3 rounded-full" style={{ width: `${Math.min(utilPct, 100)}%` }} /></div>
                  <span className="font-bold">{utilPct}%</span>
                </div>
                <p className="text-sm text-gray-500 mt-1">{formatCurrency(agreement.utilizedValue)} utilized</p>
              </div>
            </CardContent>
          </Card>
          {agreement.approvedByName && (
            <Card>
              <CardHeader><CardTitle className="text-lg">Approval</CardTitle></CardHeader>
              <CardContent className="space-y-2 text-sm">
                <div className="flex justify-between"><span className="text-gray-500">Approved By</span><span>{agreement.approvedByName}</span></div>
                <div className="flex justify-between"><span className="text-gray-500">Approved Date</span><span>{formatDate(agreement.approvedDate)}</span></div>
                {agreement.approvalComments && <div><p className="text-gray-500">Comments</p><p className="italic">{agreement.approvalComments}</p></div>}
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      <WorkflowApprovalHistoryPanel
        entityType="SalesAgreement"
        entityId={agreementId}
        entityLabel="Sales Agreement"
        entityNumber={agreement.documentNumber}
        status={agreement.agreementStatus}
        canSubmit={canSubmit}
        canApproveReject={canApprove}
        onSubmit={handleWorkflowSubmit}
        onApprove={handleWorkflowApprove}
        onReject={handleWorkflowReject}
        onAfterAction={loadAgreement}
        onOpenWorkflows={() => router.push('/administration/workflow')}
        showActions={false}
      />

      {/* Milestones */}
      {agreement.milestones.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="flex items-center gap-2"><Calendar className="h-5 w-5" />Payment Milestones</CardTitle><CardDescription>{agreement.milestones.filter(m => m.status === 'Paid' || m.status === 'Completed').length} of {agreement.milestones.length} milestones completed</CardDescription></CardHeader>
          <CardContent>
            <Table>
              <TableHeader><TableRow>
                <TableHead>#</TableHead><TableHead>Milestone</TableHead><TableHead>%</TableHead>
                <TableHead className="text-right">Amount</TableHead><TableHead>Due</TableHead>
                <TableHead>Status</TableHead><TableHead>Actions</TableHead>
              </TableRow></TableHeader>
              <TableBody>
                {agreement.milestones.map(m => (
                  <TableRow key={m.id}>
                    <TableCell>{m.sequenceNumber}</TableCell>
                    <TableCell><div><p className="font-medium">{m.milestoneName}</p>{m.description && <p className="text-xs text-gray-500">{m.description}</p>}</div></TableCell>
                    <TableCell>{m.paymentPercentage}%</TableCell>
                    <TableCell className="text-right font-semibold">{formatCurrency(m.paymentAmount)}</TableCell>
                    <TableCell>{formatDate(m.dueDate)}</TableCell>
                    <TableCell><Badge className={MILESTONE_STATUS_COLORS[m.status] || ''}>{m.status}</Badge></TableCell>
                    <TableCell>
                      {m.status === 'Pending' && <Button variant="outline" size="sm" onClick={() => handleMilestoneUpdate(m.id, 'Completed')}>Complete</Button>}
                      {m.status === 'Completed' && <Button variant="outline" size="sm" onClick={() => handleMilestoneUpdate(m.id, 'Paid')}>Mark Paid</Button>}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Agreement Lines */}
      {agreement.lines.length > 0 && (
        <Card>
          <CardHeader><CardTitle>Agreement Lines</CardTitle><CardDescription>{agreement.lines.length} item(s)</CardDescription></CardHeader>
          <CardContent>
            <Table>
              <TableHeader><TableRow>
                <TableHead>#</TableHead><TableHead>Description</TableHead><TableHead>Code</TableHead>
                <TableHead className="text-right">Agreed Price</TableHead><TableHead className="text-right">Min Qty</TableHead>
                <TableHead className="text-right">Max Qty</TableHead><TableHead className="text-right">Used</TableHead>
                <TableHead>Disc %</TableHead>
              </TableRow></TableHeader>
              <TableBody>
                {agreement.lines.map(l => (
                  <TableRow key={l.id}>
                    <TableCell>{l.lineNumber}</TableCell>
                    <TableCell className="font-medium">{l.description}</TableCell>
                    <TableCell className="text-gray-500">{l.productCode || '-'}</TableCell>
                    <TableCell className="text-right">{formatCurrency(l.agreedPrice)}</TableCell>
                    <TableCell className="text-right">{l.minimumQuantity}</TableCell>
                    <TableCell className="text-right">{l.maximumQuantity}</TableCell>
                    <TableCell className="text-right">{l.utilizedQuantity}</TableCell>
                    <TableCell>{l.discountPercentage}%</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Renewal History */}
      {agreement.renewals.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="flex items-center gap-2"><RefreshCw className="h-5 w-5" />Renewal History</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-3">
              {agreement.renewals.map(r => (
                <div key={r.id} className="p-4 bg-blue-50 rounded-lg border border-blue-200">
                  <div className="flex justify-between items-start">
                    <div>
                      <p className="font-semibold">Renewal #{r.renewalNumber}</p>
                      <p className="text-sm text-gray-600">{formatDate(r.newStartDate)} → {formatDate(r.newEndDate)}</p>
                      {r.priceChangePercentage && <p className="text-sm"><span className={r.priceChangePercentage > 0 ? 'text-red-600' : 'text-green-600'}>{r.priceChangePercentage > 0 ? '+' : ''}{r.priceChangePercentage.toFixed(1)}%</span> price change</p>}
                    </div>
                    <div className="text-right text-sm">
                      <p className="font-semibold">{formatCurrency(r.newValue)}</p>
                      <p className="text-gray-500">{r.renewedByName} • {formatDate(r.renewedDate)}</p>
                    </div>
                  </div>
                  {r.renewalTerms && <p className="text-sm mt-2 text-gray-600">{r.renewalTerms}</p>}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
