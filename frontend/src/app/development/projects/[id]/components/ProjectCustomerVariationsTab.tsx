import { type Dispatch, type SetStateAction, useMemo } from 'react';
import { Pencil, Plus, Save, Trash2, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type {
  CreateProjectCustomerVariationDto,
  ProjectDetailDto,
  ProjectCustomerVariationDto,
  ProjectLinkOptionsDto,
  ProjectSalesAgreementLinkOptionDto,
  ProjectSalesOrderLinkOptionDto,
  ProjectUnitDto,
} from '@/services/projectService';

type ProjectCustomerVariationsTabProps = {
  project: ProjectDetailDto;
  units: ProjectUnitDto[];
  activeBusinessPartners: BusinessPartnerDto[];
  linkOptions: ProjectLinkOptionsDto;
  variationDraft: CreateProjectCustomerVariationDto;
  setVariationDraft: Dispatch<SetStateAction<CreateProjectCustomerVariationDto>>;
  editingVariationId: string | null;
  variationStatusOptions: string[];
  variationTimingOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  followThroughBusyKey: string | null;
  onSaveVariation: () => void;
  onEditVariation: (variation: ProjectCustomerVariationDto) => void;
  onCancelVariationEdit: () => void;
  onDeleteVariation: (variationId: string) => void;
  onCreateVariationJobCard: (variationId: string) => void;
  onCreateVariationWorkOrder: (variationId: string) => void;
};

const countByStatus = (project: ProjectDetailDto, status: string) =>
  project.customerVariations.filter((item) => (item.status || '').toLowerCase() === status.toLowerCase()).length;

const formatSalesAgreementLabel = (agreement: ProjectSalesAgreementLinkOptionDto) =>
  `${agreement.documentNumber}${agreement.agreementTitle ? ` - ${agreement.agreementTitle}` : ''}`;

const formatSalesOrderLabel = (order: ProjectSalesOrderLinkOptionDto) =>
  `${order.orderNumber}${order.customerName ? ` - ${order.customerName}` : ''}`;

export function ProjectCustomerVariationsTab({
  project,
  units,
  activeBusinessPartners,
  linkOptions,
  variationDraft,
  setVariationDraft,
  editingVariationId,
  variationStatusOptions,
  variationTimingOptions,
  formatCatalogLabel,
  formatDateLabel,
  formatMoney,
  followThroughBusyKey,
  onSaveVariation,
  onEditVariation,
  onCancelVariationEdit,
  onDeleteVariation,
  onCreateVariationJobCard,
  onCreateVariationWorkOrder,
}: ProjectCustomerVariationsTabProps) {
  const quotedValue = useMemo(
    () => project.customerVariations.reduce((sum, item) => sum + (item.quotedAmount || 0), 0),
    [project.customerVariations],
  );
  const billedValue = useMemo(
    () => project.customerVariations.reduce((sum, item) => sum + (item.billedAmount || 0), 0),
    [project.customerVariations],
  );
  const preHandoverCount = project.customerVariations.filter((item) => item.timing === 'PreHandover').length;
  const activeCount = countByStatus(project, 'Requested') + countByStatus(project, 'UnderReview') + countByStatus(project, 'Quoted') + countByStatus(project, 'Approved') + countByStatus(project, 'InProgress');
  const selectedUnit = useMemo(
    () => units.find((item) => item.id === variationDraft.projectUnitId) ?? null,
    [units, variationDraft.projectUnitId],
  );
  const effectiveCustomerId = variationDraft.customerBusinessPartnerId || selectedUnit?.customerBusinessPartnerId;
  const availableSalesAgreements = useMemo(
    () => effectiveCustomerId
      ? linkOptions.salesAgreements.filter((item) => item.businessPartnerId === effectiveCustomerId)
      : linkOptions.salesAgreements,
    [effectiveCustomerId, linkOptions.salesAgreements],
  );
  const availableSalesOrders = useMemo(
    () => effectiveCustomerId
      ? linkOptions.salesOrders.filter((item) => item.businessPartnerId === effectiveCustomerId)
      : linkOptions.salesOrders,
    [effectiveCustomerId, linkOptions.salesOrders],
  );
  const hasMaintenanceAssetLink = useMemo(
    () => project.assetLinks.some((item) => Boolean(item.maintenanceAssetId)),
    [project.assetLinks],
  );

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Customer Variations</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Requests</div>
              <div className="text-2xl font-semibold">{project.customerVariations.length}</div>
              <div className="text-sm text-muted-foreground">{activeCount} active requests</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Pre-Handover</div>
              <div className="text-2xl font-semibold">{preHandoverCount}</div>
              <div className="text-sm text-muted-foreground">Changes affecting delivery scope</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Quoted</div>
              <div className="text-2xl font-semibold">{formatMoney(quotedValue, project.customerVariations[0]?.currency)}</div>
              <div className="text-sm text-muted-foreground">Commercial exposure under quote</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Billed</div>
              <div className="text-2xl font-semibold">{formatMoney(billedValue, project.customerVariations[0]?.currency)}</div>
              <div className="text-sm text-muted-foreground">Captured customer recovery</div>
            </div>
          </div>

          <div className="rounded-lg border p-4 space-y-4">
            <div className="font-medium">{editingVariationId ? 'Edit Customer Variation' : 'Add Customer Variation'}</div>
            {!hasMaintenanceAssetLink ? (
              <div className="rounded-md border border-dashed px-3 py-2 text-xs text-muted-foreground">
                Link a maintenance asset in the Access tab to enable direct job card and work order follow-through for variations.
              </div>
            ) : null}
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2 md:col-span-2">
                <Label>Title</Label>
                <Input value={variationDraft.title} onChange={(event) => setVariationDraft((current) => ({ ...current, title: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Timing</Label>
                <Select value={variationDraft.timing || variationTimingOptions[0]} onValueChange={(value) => setVariationDraft((current) => ({ ...current, timing: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{variationTimingOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={variationDraft.status || variationStatusOptions[0]} onValueChange={(value) => setVariationDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{variationStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Unit</Label>
                <Select
                  value={variationDraft.projectUnitId || 'none'}
                  onValueChange={(value) => {
                    if (value === 'none') {
                      setVariationDraft((current) => ({ ...current, projectUnitId: undefined }));
                      return;
                    }

                    const unit = units.find((item) => item.id === value);
                    setVariationDraft((current) => ({
                      ...current,
                      projectUnitId: value,
                      customerBusinessPartnerId: current.customerBusinessPartnerId || unit?.customerBusinessPartnerId,
                      salesAgreementId: current.salesAgreementId || unit?.salesAgreementId,
                      salesOrderId: current.salesOrderId || unit?.salesOrderId,
                    }));
                  }}
                >
                  <SelectTrigger><SelectValue placeholder="Whole project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Whole project</SelectItem>
                    {units.map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} · ${unit.name}` : unit.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Customer</Label>
                <Select
                  value={variationDraft.customerBusinessPartnerId || 'none'}
                  onValueChange={(value) => setVariationDraft((current) => ({
                    ...current,
                    customerBusinessPartnerId: value === 'none' ? undefined : value,
                    salesAgreementId: undefined,
                    salesOrderId: undefined,
                  }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional customer" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No customer</SelectItem>
                    {activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{partner.partnerName}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Sales Agreement</Label>
                <Select
                  value={variationDraft.salesAgreementId || 'none'}
                  onValueChange={(value) => setVariationDraft((current) => ({ ...current, salesAgreementId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional agreement" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No agreement</SelectItem>
                    {availableSalesAgreements.map((agreement) => (
                      <SelectItem key={agreement.id} value={agreement.id}>
                        {formatSalesAgreementLabel(agreement)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Sales Order</Label>
                <Select
                  value={variationDraft.salesOrderId || 'none'}
                  onValueChange={(value) => setVariationDraft((current) => ({ ...current, salesOrderId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional sales order" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No sales order</SelectItem>
                    {availableSalesOrders.map((order) => (
                      <SelectItem key={order.id} value={order.id}>
                        {formatSalesOrderLabel(order)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Type</Label>
                <Input value={variationDraft.variationType || ''} onChange={(event) => setVariationDraft((current) => ({ ...current, variationType: event.target.value || undefined }))} placeholder="Alteration / Upgrade / Fit-out" />
              </div>
              <div className="grid gap-2">
                <Label>Job Card</Label>
                <Select
                  value={variationDraft.jobCardId || 'none'}
                  onValueChange={(value) => setVariationDraft((current) => ({
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
                  value={variationDraft.workOrderId || 'none'}
                  onValueChange={(value) => setVariationDraft((current) => ({ ...current, workOrderId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional work order" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No work order</SelectItem>
                    {linkOptions.workOrders.map((workOrder) => (
                      <SelectItem key={workOrder.id} value={workOrder.id}>
                        {workOrder.workOrderNumber} - {workOrder.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Request Date</Label>
                <Input type="date" value={variationDraft.requestDate || ''} onChange={(event) => setVariationDraft((current) => ({ ...current, requestDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Target Completion</Label>
                <Input type="date" value={variationDraft.targetCompletionDate || ''} onChange={(event) => setVariationDraft((current) => ({ ...current, targetCompletionDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Estimated</Label>
                <Input type="number" min="0" step="0.01" value={variationDraft.estimatedAmount ?? ''} onChange={(event) => setVariationDraft((current) => ({ ...current, estimatedAmount: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Quoted</Label>
                <Input type="number" min="0" step="0.01" value={variationDraft.quotedAmount ?? ''} onChange={(event) => setVariationDraft((current) => ({ ...current, quotedAmount: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Approved</Label>
                <Input type="number" min="0" step="0.01" value={variationDraft.approvedAmount ?? ''} onChange={(event) => setVariationDraft((current) => ({ ...current, approvedAmount: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Billed</Label>
                <Input type="number" min="0" step="0.01" value={variationDraft.billedAmount ?? ''} onChange={(event) => setVariationDraft((current) => ({ ...current, billedAmount: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Schedule Impact (days)</Label>
                <Input type="number" min="0" step="1" value={variationDraft.scheduleImpactDays ?? ''} onChange={(event) => setVariationDraft((current) => ({ ...current, scheduleImpactDays: event.target.value ? Number(event.target.value) : undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Schedule Adjustment</Label>
                <Select value={variationDraft.requiresScheduleAdjustment ? 'true' : 'false'} onValueChange={(value) => setVariationDraft((current) => ({ ...current, requiresScheduleAdjustment: value === 'true' }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="false">No</SelectItem>
                    <SelectItem value="true">Yes</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Description</Label>
                <Textarea rows={2} value={variationDraft.description || ''} onChange={(event) => setVariationDraft((current) => ({ ...current, description: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Notes</Label>
                <Textarea rows={2} value={variationDraft.notes || ''} onChange={(event) => setVariationDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end gap-2">
              {editingVariationId ? (
                <Button variant="outline" onClick={onCancelVariationEdit}>
                  <X className="mr-2 h-4 w-4" />
                  Cancel
                </Button>
              ) : null}
              <Button disabled={!variationDraft.title?.trim()} onClick={onSaveVariation}>
                {editingVariationId ? <Save className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}
                {editingVariationId ? 'Save Variation' : 'Add Variation'}
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {project.customerVariations.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No customer variations have been recorded for this project yet.
              </div>
            ) : null}
            {project.customerVariations.map((variation) => (
              <div key={variation.id} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{variation.title}</div>
                      <Badge variant="outline">{formatCatalogLabel(variation.timing)}</Badge>
                      <Badge>{formatCatalogLabel(variation.status)}</Badge>
                      {variation.projectUnitName ? <Badge variant="secondary">{variation.projectUnitCode ? `${variation.projectUnitCode} · ${variation.projectUnitName}` : variation.projectUnitName}</Badge> : null}
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      {variation.customerBusinessPartnerName ? <span>{variation.customerBusinessPartnerName}</span> : null}
                      {variation.variationType ? <span>{variation.variationType}</span> : null}
                      {variation.salesAgreementNumber ? <span>Agreement {variation.salesAgreementNumber}</span> : null}
                      {variation.salesOrderNumber ? <span>Order {variation.salesOrderNumber}</span> : null}
                      {variation.jobCardNumber ? <span>Job card {variation.jobCardNumber}</span> : null}
                      {variation.workOrderNumber ? <span>Work order {variation.workOrderNumber}</span> : null}
                      <span>Requested {formatDateLabel(variation.requestDate)}</span>
                      {variation.targetCompletionDate ? <span>Target {formatDateLabel(variation.targetCompletionDate)}</span> : null}
                      {variation.requiresScheduleAdjustment ? <span>{variation.scheduleImpactDays || 0} day schedule impact</span> : null}
                    </div>
                    <div className="grid gap-3 text-sm md:grid-cols-4">
                      <div>
                        <div className="text-muted-foreground">Estimated</div>
                        <div className="font-medium">{formatMoney(variation.estimatedAmount, variation.currency)}</div>
                      </div>
                      <div>
                        <div className="text-muted-foreground">Quoted</div>
                        <div className="font-medium">{formatMoney(variation.quotedAmount, variation.currency)}</div>
                      </div>
                      <div>
                        <div className="text-muted-foreground">Approved</div>
                        <div className="font-medium">{formatMoney(variation.approvedAmount, variation.currency)}</div>
                      </div>
                      <div>
                        <div className="text-muted-foreground">Billed</div>
                        <div className="font-medium">{formatMoney(variation.billedAmount, variation.currency)}</div>
                      </div>
                    </div>
                    {variation.description ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{variation.description}</div> : null}
                    {variation.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{variation.notes}</div> : null}
                  </div>
                  <div className="flex flex-col items-stretch gap-2 xl:items-end">
                    <Button variant="outline" size="sm" onClick={() => onEditVariation(variation)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </Button>
                    {!variation.jobCardId ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!hasMaintenanceAssetLink || followThroughBusyKey === `variation-job-card:${variation.id}`}
                        onClick={() => onCreateVariationJobCard(variation.id)}
                      >
                        {followThroughBusyKey === `variation-job-card:${variation.id}` ? 'Creating job card...' : 'Create Job Card'}
                      </Button>
                    ) : null}
                    {!variation.workOrderId ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={(!hasMaintenanceAssetLink && !variation.jobCardId) || followThroughBusyKey === `variation-work-order:${variation.id}`}
                        onClick={() => onCreateVariationWorkOrder(variation.id)}
                      >
                        {followThroughBusyKey === `variation-work-order:${variation.id}` ? 'Creating work order...' : 'Create Work Order'}
                      </Button>
                    ) : null}
                    <Button variant="ghost" size="sm" onClick={() => onDeleteVariation(variation.id)}>
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
