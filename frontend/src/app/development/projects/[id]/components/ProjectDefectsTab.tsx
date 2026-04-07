import { type Dispatch, type SetStateAction, useMemo } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type {
  CreateProjectDefectLiabilityCaseDto,
  ProjectLinkOptionsDto,
  CreateProjectSnagItemDto,
  ProjectDetailDto,
  ProjectUnitDto,
} from '@/services/projectService';

type ProjectDefectsTabProps = {
  project: ProjectDetailDto;
  units: ProjectUnitDto[];
  activeBusinessPartners: BusinessPartnerDto[];
  linkOptions: ProjectLinkOptionsDto;
  snagDraft: CreateProjectSnagItemDto;
  setSnagDraft: Dispatch<SetStateAction<CreateProjectSnagItemDto>>;
  defectLiabilityDraft: CreateProjectDefectLiabilityCaseDto;
  setDefectLiabilityDraft: Dispatch<SetStateAction<CreateProjectDefectLiabilityCaseDto>>;
  snagStatusOptions: string[];
  snagSeverityOptions: string[];
  defectLiabilityStatusOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  followThroughBusyKey: string | null;
  onAddSnagItem: () => void;
  onDeleteSnagItem: (snagItemId: string) => void;
  onAddDefectLiabilityCase: () => void;
  onDeleteDefectLiabilityCase: (defectLiabilityCaseId: string) => void;
  onCreateDefectLiabilityJobCard: (defectLiabilityCaseId: string) => void;
  onCreateDefectLiabilityWorkOrder: (defectLiabilityCaseId: string) => void;
};

export function ProjectDefectsTab({
  project,
  units,
  activeBusinessPartners,
  linkOptions,
  snagDraft,
  setSnagDraft,
  defectLiabilityDraft,
  setDefectLiabilityDraft,
  snagStatusOptions,
  snagSeverityOptions,
  defectLiabilityStatusOptions,
  formatCatalogLabel,
  formatDateLabel,
  formatMoney,
  followThroughBusyKey,
  onAddSnagItem,
  onDeleteSnagItem,
  onAddDefectLiabilityCase,
  onDeleteDefectLiabilityCase,
  onCreateDefectLiabilityJobCard,
  onCreateDefectLiabilityWorkOrder,
}: ProjectDefectsTabProps) {
  const openSnagCount = useMemo(
    () => project.snagItems.filter((item) => !['Closed', 'Waived'].includes(item.status)).length,
    [project.snagItems],
  );
  const highSeveritySnagCount = useMemo(
    () => project.snagItems.filter((item) => ['High', 'Critical'].includes(item.severity)).length,
    [project.snagItems],
  );
  const openDefectCount = useMemo(
    () => project.defectLiabilityCases.filter((item) => !['Resolved', 'Closed', 'WarrantyExpired'].includes(item.status)).length,
    [project.defectLiabilityCases],
  );
  const rectificationExposure = useMemo(
    () => project.defectLiabilityCases.reduce((sum, item) => sum + (item.rectificationCost || 0), 0),
    [project.defectLiabilityCases],
  );
  const availableWorkOrders = useMemo(
    () => defectLiabilityDraft.jobCardId
      ? linkOptions.workOrders.filter((item) => !item.jobCardId || item.jobCardId === defectLiabilityDraft.jobCardId)
      : linkOptions.workOrders,
    [defectLiabilityDraft.jobCardId, linkOptions.workOrders],
  );
  const hasMaintenanceAssetLink = useMemo(
    () => project.assetLinks.some((item) => Boolean(item.maintenanceAssetId)),
    [project.assetLinks],
  );

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Defects Snapshot</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Snags</div>
            <div className="text-2xl font-semibold">{project.snagItems.length}</div>
            <div className="text-sm text-muted-foreground">{openSnagCount} still open</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">High Severity</div>
            <div className="text-2xl font-semibold">{highSeveritySnagCount}</div>
            <div className="text-sm text-muted-foreground">Need urgent verification or rectification</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Defect Liability Cases</div>
            <div className="text-2xl font-semibold">{project.defectLiabilityCases.length}</div>
            <div className="text-sm text-muted-foreground">{openDefectCount} still active</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Rectification Exposure</div>
            <div className="text-2xl font-semibold">{formatMoney(rectificationExposure, project.defectLiabilityCases[0]?.currency)}</div>
            <div className="text-sm text-muted-foreground">Tracked estimated rectification cost</div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Snag Register</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="rounded-lg border p-4 space-y-4">
            <div className="font-medium">Add Snag Item</div>
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2 md:col-span-2">
                <Label>Title</Label>
                <Input value={snagDraft.title} onChange={(event) => setSnagDraft((current) => ({ ...current, title: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Severity</Label>
                <Select value={snagDraft.severity || snagSeverityOptions[0]} onValueChange={(value) => setSnagDraft((current) => ({ ...current, severity: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{snagSeverityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={snagDraft.status || snagStatusOptions[0]} onValueChange={(value) => setSnagDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{snagStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Unit</Label>
                <Select value={snagDraft.projectUnitId || 'none'} onValueChange={(value) => setSnagDraft((current) => ({ ...current, projectUnitId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Whole project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Whole project</SelectItem>
                    {units.map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} · ${unit.name}` : unit.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Reported</Label>
                <Input type="date" value={snagDraft.reportedDate || ''} onChange={(event) => setSnagDraft((current) => ({ ...current, reportedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Target Closure</Label>
                <Input type="date" value={snagDraft.targetClosureDate || ''} onChange={(event) => setSnagDraft((current) => ({ ...current, targetClosureDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Closed</Label>
                <Input type="date" value={snagDraft.closedDate || ''} onChange={(event) => setSnagDraft((current) => ({ ...current, closedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Raised By</Label>
                <Input value={snagDraft.raisedByName || ''} onChange={(event) => setSnagDraft((current) => ({ ...current, raisedByName: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-2">
                <Label>Responsible Party</Label>
                <Input value={snagDraft.responsibleParty || ''} onChange={(event) => setSnagDraft((current) => ({ ...current, responsibleParty: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Description</Label>
                <Textarea rows={2} value={snagDraft.description || ''} onChange={(event) => setSnagDraft((current) => ({ ...current, description: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Notes</Label>
                <Textarea rows={2} value={snagDraft.notes || ''} onChange={(event) => setSnagDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end">
              <Button disabled={!snagDraft.title?.trim()} onClick={onAddSnagItem}>
                <Plus className="mr-2 h-4 w-4" />
                Add Snag Item
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {project.snagItems.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No snag items have been recorded yet.
              </div>
            ) : null}
            {project.snagItems.map((item) => (
              <div key={item.id} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.title}</div>
                      <Badge>{formatCatalogLabel(item.status)}</Badge>
                      <Badge variant="outline">{formatCatalogLabel(item.severity)}</Badge>
                      {item.projectUnitName ? <Badge variant="secondary">{item.projectUnitCode ? `${item.projectUnitCode} · ${item.projectUnitName}` : item.projectUnitName}</Badge> : null}
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      <span>Reported {formatDateLabel(item.reportedDate)}</span>
                      {item.targetClosureDate ? <span>Target {formatDateLabel(item.targetClosureDate)}</span> : null}
                      {item.closedDate ? <span>Closed {formatDateLabel(item.closedDate)}</span> : null}
                      {item.raisedByName ? <span>{item.raisedByName}</span> : null}
                      {item.responsibleParty ? <span>{item.responsibleParty}</span> : null}
                    </div>
                    {item.description ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.description}</div> : null}
                    {item.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</div> : null}
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => onDeleteSnagItem(item.id)}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Defect Liability Register</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="rounded-lg border p-4 space-y-4">
            <div className="font-medium">Add Defect Liability Case</div>
            {!hasMaintenanceAssetLink ? (
              <div className="rounded-md border border-dashed px-3 py-2 text-xs text-muted-foreground">
                Link a maintenance asset in the Access tab to enable direct job card and work order follow-through for defect cases.
              </div>
            ) : null}
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2 md:col-span-2">
                <Label>Title</Label>
                <Input value={defectLiabilityDraft.title} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, title: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={defectLiabilityDraft.status || defectLiabilityStatusOptions[0]} onValueChange={(value) => setDefectLiabilityDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{defectLiabilityStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Unit</Label>
                <Select value={defectLiabilityDraft.projectUnitId || 'none'} onValueChange={(value) => setDefectLiabilityDraft((current) => ({ ...current, projectUnitId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Whole project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Whole project</SelectItem>
                    {units.map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} · ${unit.name}` : unit.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Customer</Label>
                <Select value={defectLiabilityDraft.customerBusinessPartnerId || 'none'} onValueChange={(value) => setDefectLiabilityDraft((current) => ({ ...current, customerBusinessPartnerId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Optional customer" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No customer</SelectItem>
                    {activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{partner.partnerName}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Job Card</Label>
                <Select
                  value={defectLiabilityDraft.jobCardId || 'none'}
                  onValueChange={(value) => setDefectLiabilityDraft((current) => ({
                    ...current,
                    jobCardId: value === 'none' ? undefined : value,
                    workOrderId: undefined,
                  }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional job card" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No job card</SelectItem>
                    {linkOptions.jobCards.map((jobCard) => (
                      <SelectItem key={jobCard.id} value={jobCard.id}>
                        {jobCard.jobCardNumber} - {jobCard.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Work Order</Label>
                <Select
                  value={defectLiabilityDraft.workOrderId || 'none'}
                  onValueChange={(value) => setDefectLiabilityDraft((current) => ({ ...current, workOrderId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional work order" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No work order</SelectItem>
                    {availableWorkOrders.map((workOrder) => (
                      <SelectItem key={workOrder.id} value={workOrder.id}>
                        {workOrder.workOrderNumber} - {workOrder.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Reported</Label>
                <Input type="date" value={defectLiabilityDraft.reportedDate || ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, reportedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Target Resolution</Label>
                <Input type="date" value={defectLiabilityDraft.targetResolutionDate || ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, targetResolutionDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Resolved</Label>
                <Input type="date" value={defectLiabilityDraft.resolvedDate || ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, resolvedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Warranty</Label>
                <Select value={defectLiabilityDraft.isWarrantyRelated ? 'true' : 'false'} onValueChange={(value) => setDefectLiabilityDraft((current) => ({ ...current, isWarrantyRelated: value === 'true' }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="true">Warranty item</SelectItem>
                    <SelectItem value="false">Chargeable / outside warranty</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Warranty Expiry</Label>
                <Input type="date" value={defectLiabilityDraft.warrantyExpiryDate || ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, warrantyExpiryDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Rectification Cost</Label>
                <Input type="number" min="0" step="0.01" value={defectLiabilityDraft.rectificationCost ?? ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, rectificationCost: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Chargeable Amount</Label>
                <Input type="number" min="0" step="0.01" value={defectLiabilityDraft.chargeableAmount ?? ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, chargeableAmount: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Description</Label>
                <Textarea rows={2} value={defectLiabilityDraft.description || ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, description: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Notes</Label>
                <Textarea rows={2} value={defectLiabilityDraft.notes || ''} onChange={(event) => setDefectLiabilityDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end">
              <Button disabled={!defectLiabilityDraft.title?.trim()} onClick={onAddDefectLiabilityCase}>
                <Plus className="mr-2 h-4 w-4" />
                Add Defect Liability Case
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {project.defectLiabilityCases.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No defects-liability cases have been recorded yet.
              </div>
            ) : null}
            {project.defectLiabilityCases.map((item) => (
              <div key={item.id} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.title}</div>
                      <Badge>{formatCatalogLabel(item.status)}</Badge>
                      {item.isWarrantyRelated ? <Badge variant="outline">Warranty</Badge> : <Badge variant="outline">Chargeable</Badge>}
                      {item.projectUnitName ? <Badge variant="secondary">{item.projectUnitCode ? `${item.projectUnitCode} · ${item.projectUnitName}` : item.projectUnitName}</Badge> : null}
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      <span>Reported {formatDateLabel(item.reportedDate)}</span>
                      {item.targetResolutionDate ? <span>Target {formatDateLabel(item.targetResolutionDate)}</span> : null}
                      {item.resolvedDate ? <span>Resolved {formatDateLabel(item.resolvedDate)}</span> : null}
                      {item.customerBusinessPartnerName ? <span>{item.customerBusinessPartnerName}</span> : null}
                      {item.jobCardNumber ? <span>Job card {item.jobCardNumber}</span> : null}
                      {item.workOrderNumber ? <span>Work order {item.workOrderNumber}</span> : null}
                      {item.warrantyExpiryDate ? <span>Warranty expires {formatDateLabel(item.warrantyExpiryDate)}</span> : null}
                    </div>
                    <div className="grid gap-3 text-sm md:grid-cols-2">
                      <div>
                        <div className="text-muted-foreground">Rectification Cost</div>
                        <div className="font-medium">{formatMoney(item.rectificationCost, item.currency)}</div>
                      </div>
                      <div>
                        <div className="text-muted-foreground">Chargeable Amount</div>
                        <div className="font-medium">{formatMoney(item.chargeableAmount, item.currency)}</div>
                      </div>
                    </div>
                    {item.description ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.description}</div> : null}
                    {item.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</div> : null}
                  </div>
                  <div className="flex flex-col items-stretch gap-2 xl:items-end">
                    {!item.jobCardId ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!hasMaintenanceAssetLink || followThroughBusyKey === `defect-job-card:${item.id}`}
                        onClick={() => onCreateDefectLiabilityJobCard(item.id)}
                      >
                        {followThroughBusyKey === `defect-job-card:${item.id}` ? 'Creating job card...' : 'Create Job Card'}
                      </Button>
                    ) : null}
                    {!item.workOrderId ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={(!hasMaintenanceAssetLink && !item.jobCardId) || followThroughBusyKey === `defect-work-order:${item.id}`}
                        onClick={() => onCreateDefectLiabilityWorkOrder(item.id)}
                      >
                        {followThroughBusyKey === `defect-work-order:${item.id}` ? 'Creating work order...' : 'Create Work Order'}
                      </Button>
                    ) : null}
                    <Button variant="ghost" size="sm" onClick={() => onDeleteDefectLiabilityCase(item.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
