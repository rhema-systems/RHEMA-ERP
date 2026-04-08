import { type Dispatch, type SetStateAction, useMemo } from 'react';
import { Pencil, Plus, Save, Trash2, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type { ContractDto } from '@/services/contractService';
import type {
  CreateProjectExtensionOfTimeDto,
  CreateProjectInterimValuationDto,
  CreateProjectPaymentCertificateDto,
  CreateProjectVariationOrderDto,
  ProjectDetailDto,
  ProjectExtensionOfTimeDto,
  ProjectInterimValuationDto,
  ProjectPackageDto,
  ProjectPaymentCertificateDto,
  ProjectPhaseDto,
  ProjectVariationOrderDto,
  UpsertProjectFinalAccountDto,
} from '@/services/projectService';

type Props = {
  project: ProjectDetailDto;
  phases: ProjectPhaseDto[];
  packages: ProjectPackageDto[];
  activeContracts: ContractDto[];
  variationOrderDraft: CreateProjectVariationOrderDto;
  setVariationOrderDraft: Dispatch<SetStateAction<CreateProjectVariationOrderDto>>;
  editingVariationOrderId: string | null;
  interimValuationDraft: CreateProjectInterimValuationDto;
  setInterimValuationDraft: Dispatch<SetStateAction<CreateProjectInterimValuationDto>>;
  editingInterimValuationId: string | null;
  paymentCertificateDraft: CreateProjectPaymentCertificateDto;
  setPaymentCertificateDraft: Dispatch<SetStateAction<CreateProjectPaymentCertificateDto>>;
  editingPaymentCertificateId: string | null;
  extensionOfTimeDraft: CreateProjectExtensionOfTimeDto;
  setExtensionOfTimeDraft: Dispatch<SetStateAction<CreateProjectExtensionOfTimeDto>>;
  editingExtensionOfTimeId: string | null;
  finalAccountDraft: UpsertProjectFinalAccountDto;
  setFinalAccountDraft: Dispatch<SetStateAction<UpsertProjectFinalAccountDto>>;
  variationOrderStatusOptions: string[];
  variationOrderTypeOptions: string[];
  interimValuationStatusOptions: string[];
  paymentCertificateStatusOptions: string[];
  extensionOfTimeStatusOptions: string[];
  finalAccountStatusOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  onSaveVariationOrder: () => void;
  onEditVariationOrder: (variationOrder: ProjectVariationOrderDto) => void;
  onCancelVariationOrderEdit: () => void;
  onDeleteVariationOrder: (variationOrderId: string) => void;
  onSaveInterimValuation: () => void;
  onEditInterimValuation: (valuation: ProjectInterimValuationDto) => void;
  onCancelInterimValuationEdit: () => void;
  onDeleteInterimValuation: (valuationId: string) => void;
  onSavePaymentCertificate: () => void;
  onEditPaymentCertificate: (certificate: ProjectPaymentCertificateDto) => void;
  onCancelPaymentCertificateEdit: () => void;
  onDeletePaymentCertificate: (certificateId: string) => void;
  onSaveExtensionOfTime: () => void;
  onEditExtensionOfTime: (extension: ProjectExtensionOfTimeDto) => void;
  onCancelExtensionOfTimeEdit: () => void;
  onDeleteExtensionOfTime: (extensionId: string) => void;
  onSaveFinalAccount: () => void;
};

const flattenPhases = (phases: ProjectPhaseDto[], depth = 0): Array<{ id: string; label: string }> =>
  phases.flatMap((phase) => [
    { id: phase.id, label: `${' '.repeat(depth * 2)}${phase.name}`.trimStart() },
    ...flattenPhases(phase.children || [], depth + 1),
  ]);

const contractLabel = (contract: ContractDto) =>
  [contract.contractNumber, contract.contractTitle].filter(Boolean).join(' - ') || contract.contractNumber || contract.contractTitle || contract.id;

export function ProjectCommercialAdminTab(props: Props) {
  const {
    project,
    phases,
    packages,
    activeContracts,
    variationOrderDraft,
    setVariationOrderDraft,
    editingVariationOrderId,
    interimValuationDraft,
    setInterimValuationDraft,
    editingInterimValuationId,
    paymentCertificateDraft,
    setPaymentCertificateDraft,
    editingPaymentCertificateId,
    extensionOfTimeDraft,
    setExtensionOfTimeDraft,
    editingExtensionOfTimeId,
    finalAccountDraft,
    setFinalAccountDraft,
    variationOrderStatusOptions,
    variationOrderTypeOptions,
    interimValuationStatusOptions,
    paymentCertificateStatusOptions,
    extensionOfTimeStatusOptions,
    finalAccountStatusOptions,
    formatCatalogLabel,
    formatDateLabel,
    formatMoney,
    onSaveVariationOrder,
    onEditVariationOrder,
    onCancelVariationOrderEdit,
    onDeleteVariationOrder,
    onSaveInterimValuation,
    onEditInterimValuation,
    onCancelInterimValuationEdit,
    onDeleteInterimValuation,
    onSavePaymentCertificate,
    onEditPaymentCertificate,
    onCancelPaymentCertificateEdit,
    onDeletePaymentCertificate,
    onSaveExtensionOfTime,
    onEditExtensionOfTime,
    onCancelExtensionOfTimeEdit,
    onDeleteExtensionOfTime,
    onSaveFinalAccount,
  } = props;
  const phaseOptions = useMemo(() => flattenPhases(phases), [phases]);
  const approvedVariationAmount = useMemo(() => project.variationOrders.reduce((sum, item) => sum + (item.approvedAmount ?? 0), 0), [project.variationOrders]);
  const netValuationAmount = useMemo(() => project.interimValuations.reduce((sum, item) => sum + (item.netValuationAmount ?? 0), 0), [project.interimValuations]);
  const netCertifiedAmount = useMemo(() => project.paymentCertificates.reduce((sum, item) => sum + (item.netCertifiedAmount ?? 0), 0), [project.paymentCertificates]);
  const approvedExtensionDays = useMemo(() => project.extensionOfTimeRequests.reduce((sum, item) => sum + (item.daysApproved ?? 0), 0), [project.extensionOfTimeRequests]);

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
        <Card><CardContent className="p-4"><div className="text-sm text-muted-foreground">Variations</div><div className="text-2xl font-semibold">{project.variationOrders.length}</div><div className="text-sm text-muted-foreground">{formatMoney(approvedVariationAmount, project.finalAccount?.currency || finalAccountDraft.currency)}</div></CardContent></Card>
        <Card><CardContent className="p-4"><div className="text-sm text-muted-foreground">Valuations</div><div className="text-2xl font-semibold">{project.interimValuations.length}</div><div className="text-sm text-muted-foreground">{formatMoney(netValuationAmount, project.finalAccount?.currency || finalAccountDraft.currency)}</div></CardContent></Card>
        <Card><CardContent className="p-4"><div className="text-sm text-muted-foreground">Certificates</div><div className="text-2xl font-semibold">{project.paymentCertificates.length}</div><div className="text-sm text-muted-foreground">{formatMoney(netCertifiedAmount, project.finalAccount?.currency || finalAccountDraft.currency)}</div></CardContent></Card>
        <Card><CardContent className="p-4"><div className="text-sm text-muted-foreground">EOT</div><div className="text-2xl font-semibold">{project.extensionOfTimeRequests.length}</div><div className="text-sm text-muted-foreground">{approvedExtensionDays} approved day(s)</div></CardContent></Card>
        <Card><CardContent className="p-4"><div className="text-sm text-muted-foreground">Final Account</div><div className="text-2xl font-semibold">{project.finalAccount ? formatMoney(project.finalAccount.finalAccountValue, project.finalAccount.currency) : '-'}</div><div className="text-sm text-muted-foreground">{formatCatalogLabel(project.finalAccount?.status || 'NotSet')}</div></CardContent></Card>
        <Card><CardContent className="p-4"><div className="text-sm text-muted-foreground">Contracts</div><div className="text-2xl font-semibold">{activeContracts.length}</div><div className="text-sm text-muted-foreground">{packages.filter((item) => !!item.contractId).length} package(s) linked</div></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Variation Orders</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={variationOrderDraft.title ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, title: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Reference</Label><Input value={variationOrderDraft.referenceNumber ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, referenceNumber: event.target.value || undefined }))} /></div>
            <div className="grid gap-2"><Label>Type</Label><Select value={variationOrderDraft.variationType || variationOrderTypeOptions[0]} onValueChange={(value) => setVariationOrderDraft((current) => ({ ...current, variationType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{variationOrderTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Status</Label><Select value={variationOrderDraft.status || variationOrderStatusOptions[0]} onValueChange={(value) => setVariationOrderDraft((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{variationOrderStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Phase</Label><Select value={variationOrderDraft.projectPhaseId || 'none'} onValueChange={(value) => setVariationOrderDraft((current) => ({ ...current, projectPhaseId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="No phase" /></SelectTrigger><SelectContent><SelectItem value="none">No phase</SelectItem>{phaseOptions.map((phase) => <SelectItem key={phase.id} value={phase.id}>{phase.label}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Package</Label><Select value={variationOrderDraft.projectPackageId || 'none'} onValueChange={(value) => setVariationOrderDraft((current) => ({ ...current, projectPackageId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="No package" /></SelectTrigger><SelectContent><SelectItem value="none">No package</SelectItem>{packages.map((item) => <SelectItem key={item.id} value={item.id}>{item.code ? `${item.code} - ${item.name}` : item.name}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Contract</Label><Select value={variationOrderDraft.contractId || 'none'} onValueChange={(value) => setVariationOrderDraft((current) => ({ ...current, contractId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="No contract" /></SelectTrigger><SelectContent><SelectItem value="none">No contract</SelectItem>{activeContracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{contractLabel(contract)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Requested</Label><Input type="date" value={variationOrderDraft.requestedDate ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, requestedDate: event.target.value || undefined }))} /></div>
            <div className="grid gap-2"><Label>Estimate</Label><Input type="number" value={variationOrderDraft.estimatedAmount ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, estimatedAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
            <div className="grid gap-2"><Label>Approved</Label><Input type="number" value={variationOrderDraft.approvedAmount ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, approvedAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
            <div className="grid gap-2"><Label>Schedule Days</Label><Input type="number" value={variationOrderDraft.scheduleImpactDays ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, scheduleImpactDays: event.target.value ? Number(event.target.value) : undefined }))} /></div>
            <div className="grid gap-2"><Label>Currency</Label><Input value={variationOrderDraft.currency ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, currency: event.target.value || undefined }))} /></div>
            <div className="grid gap-2 md:col-span-4"><Label>Notes</Label><Textarea rows={2} value={variationOrderDraft.notes ?? ''} onChange={(event) => setVariationOrderDraft((current) => ({ ...current, notes: event.target.value || undefined }))} /></div>
          </div>
          <div className="flex justify-end gap-2">{editingVariationOrderId ? <Button variant="outline" onClick={onCancelVariationOrderEdit}><X className="mr-2 h-4 w-4" />Cancel</Button> : null}<Button disabled={!variationOrderDraft.title?.trim()} onClick={onSaveVariationOrder}>{editingVariationOrderId ? <Save className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}{editingVariationOrderId ? 'Save Variation' : 'Add Variation'}</Button></div>
          <div className="space-y-3">{project.variationOrders.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No variation orders recorded yet.</div> : project.variationOrders.map((item) => <div key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between"><div className="space-y-2"><div className="flex flex-wrap items-center gap-2"><div className="font-medium">{item.title}</div><Badge variant="outline">{formatCatalogLabel(item.variationType)}</Badge><Badge>{formatCatalogLabel(item.status)}</Badge>{item.projectPackageName ? <Badge variant="secondary">{item.projectPackageName}</Badge> : null}</div><div className="flex flex-wrap gap-3 text-sm text-muted-foreground">{item.referenceNumber ? <span>{item.referenceNumber}</span> : null}<span>{formatDateLabel(item.requestedDate)}</span><span>{formatMoney(item.approvedAmount, item.currency)}</span></div>{item.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</div> : null}</div><div className="flex items-center gap-2"><Button variant="outline" size="sm" onClick={() => onEditVariationOrder(item)}><Pencil className="mr-2 h-4 w-4" />Edit</Button><Button variant="ghost" size="sm" onClick={() => onDeleteVariationOrder(item.id)}><Trash2 className="h-4 w-4" /></Button></div></div></div>)}</div>
        </CardContent>
      </Card>

      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Interim Valuations</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2"><Label>Title</Label><Input value={interimValuationDraft.title ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, title: event.target.value }))} /></div>
              <div className="grid gap-2"><Label>Valuation No.</Label><Input value={interimValuationDraft.valuationNumber ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, valuationNumber: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Status</Label><Select value={interimValuationDraft.status || interimValuationStatusOptions[0]} onValueChange={(value) => setInterimValuationDraft((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{interimValuationStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Date</Label><Input type="date" value={interimValuationDraft.valuationDate ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, valuationDate: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Package</Label><Select value={interimValuationDraft.projectPackageId || 'none'} onValueChange={(value) => setInterimValuationDraft((current) => ({ ...current, projectPackageId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="No package" /></SelectTrigger><SelectContent><SelectItem value="none">No package</SelectItem>{packages.map((item) => <SelectItem key={item.id} value={item.id}>{item.code ? `${item.code} - ${item.name}` : item.name}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Contract</Label><Select value={interimValuationDraft.contractId || 'none'} onValueChange={(value) => setInterimValuationDraft((current) => ({ ...current, contractId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="No contract" /></SelectTrigger><SelectContent><SelectItem value="none">No contract</SelectItem>{activeContracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{contractLabel(contract)}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Gross Work</Label><Input type="number" value={interimValuationDraft.grossWorkValue ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, grossWorkValue: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Net Valuation</Label><Input type="number" value={interimValuationDraft.netValuationAmount ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, netValuationAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Retention</Label><Input type="number" value={interimValuationDraft.retentionAmount ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, retentionAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Currency</Label><Input value={interimValuationDraft.currency ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, currency: event.target.value || undefined }))} /></div>
              <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Textarea rows={2} value={interimValuationDraft.notes ?? ''} onChange={(event) => setInterimValuationDraft((current) => ({ ...current, notes: event.target.value || undefined }))} /></div>
            </div>
            <div className="flex justify-end gap-2">{editingInterimValuationId ? <Button variant="outline" onClick={onCancelInterimValuationEdit}><X className="mr-2 h-4 w-4" />Cancel</Button> : null}<Button disabled={!interimValuationDraft.title?.trim()} onClick={onSaveInterimValuation}>{editingInterimValuationId ? <Save className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}{editingInterimValuationId ? 'Save Valuation' : 'Add Valuation'}</Button></div>
            <div className="space-y-3">{project.interimValuations.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No interim valuations recorded yet.</div> : project.interimValuations.map((item) => <div key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between"><div className="space-y-2"><div className="flex flex-wrap items-center gap-2"><div className="font-medium">{item.title}</div><Badge>{formatCatalogLabel(item.status)}</Badge>{item.projectPackageName ? <Badge variant="secondary">{item.projectPackageName}</Badge> : null}</div><div className="flex flex-wrap gap-3 text-sm text-muted-foreground">{item.valuationNumber ? <span>{item.valuationNumber}</span> : null}<span>{formatDateLabel(item.valuationDate)}</span><span>{formatMoney(item.netValuationAmount, item.currency)}</span></div></div><div className="flex items-center gap-2"><Button variant="outline" size="sm" onClick={() => onEditInterimValuation(item)}><Pencil className="mr-2 h-4 w-4" />Edit</Button><Button variant="ghost" size="sm" onClick={() => onDeleteInterimValuation(item.id)}><Trash2 className="h-4 w-4" /></Button></div></div></div>)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>Payment Certificates</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2"><Label>Title</Label><Input value={paymentCertificateDraft.title ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, title: event.target.value }))} /></div>
              <div className="grid gap-2"><Label>Certificate No.</Label><Input value={paymentCertificateDraft.certificateNumber ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, certificateNumber: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Status</Label><Select value={paymentCertificateDraft.status || paymentCertificateStatusOptions[0]} onValueChange={(value) => setPaymentCertificateDraft((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{paymentCertificateStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Issue Date</Label><Input type="date" value={paymentCertificateDraft.issueDate ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, issueDate: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Due Date</Label><Input type="date" value={paymentCertificateDraft.paymentDueDate ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, paymentDueDate: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Valuation</Label><Select value={paymentCertificateDraft.projectInterimValuationId || 'none'} onValueChange={(value) => setPaymentCertificateDraft((current) => ({ ...current, projectInterimValuationId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="No valuation" /></SelectTrigger><SelectContent><SelectItem value="none">No valuation</SelectItem>{project.interimValuations.map((item) => <SelectItem key={item.id} value={item.id}>{item.valuationNumber ? `${item.valuationNumber} - ${item.title}` : item.title}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Gross Certified</Label><Input type="number" value={paymentCertificateDraft.grossCertifiedAmount ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, grossCertifiedAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Net Certified</Label><Input type="number" value={paymentCertificateDraft.netCertifiedAmount ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, netCertifiedAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Retention Held</Label><Input type="number" value={paymentCertificateDraft.retentionHeldAmount ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, retentionHeldAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Currency</Label><Input value={paymentCertificateDraft.currency ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, currency: event.target.value || undefined }))} /></div>
              <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Textarea rows={2} value={paymentCertificateDraft.notes ?? ''} onChange={(event) => setPaymentCertificateDraft((current) => ({ ...current, notes: event.target.value || undefined }))} /></div>
            </div>
            <div className="flex justify-end gap-2">{editingPaymentCertificateId ? <Button variant="outline" onClick={onCancelPaymentCertificateEdit}><X className="mr-2 h-4 w-4" />Cancel</Button> : null}<Button disabled={!paymentCertificateDraft.title?.trim()} onClick={onSavePaymentCertificate}>{editingPaymentCertificateId ? <Save className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}{editingPaymentCertificateId ? 'Save Certificate' : 'Add Certificate'}</Button></div>
            <div className="space-y-3">{project.paymentCertificates.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No payment certificates recorded yet.</div> : project.paymentCertificates.map((item) => <div key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between"><div className="space-y-2"><div className="flex flex-wrap items-center gap-2"><div className="font-medium">{item.title}</div><Badge>{formatCatalogLabel(item.status)}</Badge></div><div className="flex flex-wrap gap-3 text-sm text-muted-foreground">{item.certificateNumber ? <span>{item.certificateNumber}</span> : null}<span>{formatDateLabel(item.issueDate)}</span><span>{formatMoney(item.netCertifiedAmount, item.currency)}</span></div></div><div className="flex items-center gap-2"><Button variant="outline" size="sm" onClick={() => onEditPaymentCertificate(item)}><Pencil className="mr-2 h-4 w-4" />Edit</Button><Button variant="ghost" size="sm" onClick={() => onDeletePaymentCertificate(item.id)}><Trash2 className="h-4 w-4" /></Button></div></div></div>)}</div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1fr,0.9fr]">
        <Card>
          <CardHeader><CardTitle>Extension of Time</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-3">
              <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={extensionOfTimeDraft.title ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, title: event.target.value }))} /></div>
              <div className="grid gap-2"><Label>Reference</Label><Input value={extensionOfTimeDraft.referenceNumber ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, referenceNumber: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Status</Label><Select value={extensionOfTimeDraft.status || extensionOfTimeStatusOptions[0]} onValueChange={(value) => setExtensionOfTimeDraft((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{extensionOfTimeStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Requested</Label><Input type="date" value={extensionOfTimeDraft.requestedDate ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, requestedDate: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Decision</Label><Input type="date" value={extensionOfTimeDraft.decisionDate ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, decisionDate: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Days Requested</Label><Input type="number" value={extensionOfTimeDraft.daysRequested ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, daysRequested: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Days Approved</Label><Input type="number" value={extensionOfTimeDraft.daysApproved ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, daysApproved: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Revised Completion</Label><Input type="date" value={extensionOfTimeDraft.revisedCompletionDate ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, revisedCompletionDate: event.target.value || undefined }))} /></div>
              <div className="grid gap-2 md:col-span-3"><Label>Reason</Label><Textarea rows={2} value={extensionOfTimeDraft.reason ?? ''} onChange={(event) => setExtensionOfTimeDraft((current) => ({ ...current, reason: event.target.value || undefined }))} /></div>
            </div>
            <div className="flex justify-end gap-2">{editingExtensionOfTimeId ? <Button variant="outline" onClick={onCancelExtensionOfTimeEdit}><X className="mr-2 h-4 w-4" />Cancel</Button> : null}<Button disabled={!extensionOfTimeDraft.title?.trim()} onClick={onSaveExtensionOfTime}>{editingExtensionOfTimeId ? <Save className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}{editingExtensionOfTimeId ? 'Save EOT' : 'Add EOT'}</Button></div>
            <div className="space-y-3">{project.extensionOfTimeRequests.length === 0 ? <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No EOT requests recorded yet.</div> : project.extensionOfTimeRequests.map((item) => <div key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between"><div className="space-y-2"><div className="flex flex-wrap items-center gap-2"><div className="font-medium">{item.title}</div><Badge>{formatCatalogLabel(item.status)}</Badge></div><div className="flex flex-wrap gap-3 text-sm text-muted-foreground">{item.referenceNumber ? <span>{item.referenceNumber}</span> : null}<span>{item.daysApproved ?? 0} approved day(s)</span>{item.revisedCompletionDate ? <span>{formatDateLabel(item.revisedCompletionDate)}</span> : null}</div>{item.reason ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.reason}</div> : null}</div><div className="flex items-center gap-2"><Button variant="outline" size="sm" onClick={() => onEditExtensionOfTime(item)}><Pencil className="mr-2 h-4 w-4" />Edit</Button><Button variant="ghost" size="sm" onClick={() => onDeleteExtensionOfTime(item.id)}><Trash2 className="h-4 w-4" /></Button></div></div></div>)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>Final Account</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2"><Label>Contract</Label><Select value={finalAccountDraft.contractId || 'none'} onValueChange={(value) => setFinalAccountDraft((current) => ({ ...current, contractId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="No contract" /></SelectTrigger><SelectContent><SelectItem value="none">No contract</SelectItem>{activeContracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{contractLabel(contract)}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Status</Label><Select value={finalAccountDraft.status || finalAccountStatusOptions[0]} onValueChange={(value) => setFinalAccountDraft((current) => ({ ...current, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{finalAccountStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Settlement Date</Label><Input type="date" value={finalAccountDraft.settlementDate ?? ''} onChange={(event) => setFinalAccountDraft((current) => ({ ...current, settlementDate: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Currency</Label><Input value={finalAccountDraft.currency ?? ''} onChange={(event) => setFinalAccountDraft((current) => ({ ...current, currency: event.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Original Contract</Label><Input type="number" value={finalAccountDraft.originalContractValue ?? ''} onChange={(event) => setFinalAccountDraft((current) => ({ ...current, originalContractValue: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Approved Variations</Label><Input type="number" value={finalAccountDraft.approvedVariationAmount ?? ''} onChange={(event) => setFinalAccountDraft((current) => ({ ...current, approvedVariationAmount: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Certified To Date</Label><Input type="number" value={finalAccountDraft.certifiedToDate ?? ''} onChange={(event) => setFinalAccountDraft((current) => ({ ...current, certifiedToDate: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Final Account Value</Label><Input type="number" value={finalAccountDraft.finalAccountValue ?? ''} onChange={(event) => setFinalAccountDraft((current) => ({ ...current, finalAccountValue: event.target.value ? Number(event.target.value) : undefined }))} /></div>
              <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Textarea rows={2} value={finalAccountDraft.notes ?? ''} onChange={(event) => setFinalAccountDraft((current) => ({ ...current, notes: event.target.value || undefined }))} /></div>
            </div>
            <div className="flex justify-end"><Button onClick={onSaveFinalAccount}><Save className="mr-2 h-4 w-4" />Save Final Account</Button></div>
            {project.finalAccount ? <div className="rounded-lg border p-4 space-y-2"><div className="flex flex-wrap items-center gap-2"><div className="font-medium">Current Position</div><Badge>{formatCatalogLabel(project.finalAccount.status)}</Badge></div><div className="grid gap-2 text-sm md:grid-cols-2"><span>Original {formatMoney(project.finalAccount.originalContractValue, project.finalAccount.currency)}</span><span>Certified {formatMoney(project.finalAccount.certifiedToDate, project.finalAccount.currency)}</span><span>Variations {formatMoney(project.finalAccount.approvedVariationAmount, project.finalAccount.currency)}</span><span>Final {formatMoney(project.finalAccount.finalAccountValue, project.finalAccount.currency)}</span></div></div> : <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">No final account has been recorded yet.</div>}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
