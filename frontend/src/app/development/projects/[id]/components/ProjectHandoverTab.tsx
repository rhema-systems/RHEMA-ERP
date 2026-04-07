import { type Dispatch, type SetStateAction, useMemo } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type {
  CreateProjectCommissioningItemDto,
  CreateProjectHandoverItemDto,
  ProjectDetailDto,
  ProjectUnitDto,
} from '@/services/projectService';

type ProjectHandoverTabProps = {
  project: ProjectDetailDto;
  units: ProjectUnitDto[];
  commissioningDraft: CreateProjectCommissioningItemDto;
  setCommissioningDraft: Dispatch<SetStateAction<CreateProjectCommissioningItemDto>>;
  handoverDraft: CreateProjectHandoverItemDto;
  setHandoverDraft: Dispatch<SetStateAction<CreateProjectHandoverItemDto>>;
  commissioningStatusOptions: string[];
  handoverStatusOptions: string[];
  handoverTypeOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  onAddCommissioningItem: () => void;
  onDeleteCommissioningItem: (commissioningItemId: string) => void;
  onAddHandoverItem: () => void;
  onDeleteHandoverItem: (handoverItemId: string) => void;
};

export function ProjectHandoverTab({
  project,
  units,
  commissioningDraft,
  setCommissioningDraft,
  handoverDraft,
  setHandoverDraft,
  commissioningStatusOptions,
  handoverStatusOptions,
  handoverTypeOptions,
  formatCatalogLabel,
  formatDateLabel,
  onAddCommissioningItem,
  onDeleteCommissioningItem,
  onAddHandoverItem,
  onDeleteHandoverItem,
}: ProjectHandoverTabProps) {
  const outstandingCommissioningCount = useMemo(
    () => project.commissioningItems.filter((item) => !['Completed', 'Waived'].includes(item.status)).length,
    [project.commissioningItems],
  );
  const outstandingHandoverCount = useMemo(
    () => project.handoverItems.filter((item) => !['Completed', 'Waived'].includes(item.status)).length,
    [project.handoverItems],
  );
  const regulatoryInspectionCount = useMemo(
    () => project.commissioningItems.filter((item) => item.requiresRegulatoryInspection).length,
    [project.commissioningItems],
  );
  const practicalCompletionCount = useMemo(
    () => project.handoverItems.filter((item) => item.handoverType === 'PracticalCompletion').length,
    [project.handoverItems],
  );

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Closeout Snapshot</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Commissioning Items</div>
            <div className="text-2xl font-semibold">{project.commissioningItems.length}</div>
            <div className="text-sm text-muted-foreground">{outstandingCommissioningCount} still open</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Regulatory Checks</div>
            <div className="text-2xl font-semibold">{regulatoryInspectionCount}</div>
            <div className="text-sm text-muted-foreground">Need external inspection or signoff</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Handover Items</div>
            <div className="text-2xl font-semibold">{project.handoverItems.length}</div>
            <div className="text-sm text-muted-foreground">{outstandingHandoverCount} still open</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Practical Completion</div>
            <div className="text-2xl font-semibold">{practicalCompletionCount}</div>
            <div className="text-sm text-muted-foreground">Formal closeout milestones tracked</div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Commissioning Register</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="rounded-lg border p-4 space-y-4">
            <div className="font-medium">Add Commissioning Item</div>
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2 md:col-span-2">
                <Label>Title</Label>
                <Input value={commissioningDraft.title} onChange={(event) => setCommissioningDraft((current) => ({ ...current, title: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={commissioningDraft.status || commissioningStatusOptions[0]} onValueChange={(value) => setCommissioningDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{commissioningStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Unit</Label>
                <Select value={commissioningDraft.projectUnitId || 'none'} onValueChange={(value) => setCommissioningDraft((current) => ({ ...current, projectUnitId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Whole project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Whole project</SelectItem>
                    {units.map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} · ${unit.name}` : unit.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>System Area</Label>
                <Input value={commissioningDraft.systemArea || ''} onChange={(event) => setCommissioningDraft((current) => ({ ...current, systemArea: event.target.value || undefined }))} placeholder="Electrical / Plumbing / Lift" />
              </div>
              <div className="grid gap-2">
                <Label>Planned</Label>
                <Input type="date" value={commissioningDraft.plannedDate || ''} onChange={(event) => setCommissioningDraft((current) => ({ ...current, plannedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Completed</Label>
                <Input type="date" value={commissioningDraft.completedDate || ''} onChange={(event) => setCommissioningDraft((current) => ({ ...current, completedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Inspection</Label>
                <Select value={commissioningDraft.requiresRegulatoryInspection ? 'true' : 'false'} onValueChange={(value) => setCommissioningDraft((current) => ({ ...current, requiresRegulatoryInspection: value === 'true' }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="false">Internal only</SelectItem>
                    <SelectItem value="true">Regulatory required</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Certificate Ref</Label>
                <Input value={commissioningDraft.certificateReference || ''} onChange={(event) => setCommissioningDraft((current) => ({ ...current, certificateReference: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-2">
                <Label>Responsible Party</Label>
                <Input value={commissioningDraft.responsibleParty || ''} onChange={(event) => setCommissioningDraft((current) => ({ ...current, responsibleParty: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Notes</Label>
                <Textarea rows={2} value={commissioningDraft.notes || ''} onChange={(event) => setCommissioningDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end">
              <Button disabled={!commissioningDraft.title?.trim()} onClick={onAddCommissioningItem}>
                <Plus className="mr-2 h-4 w-4" />
                Add Commissioning Item
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {project.commissioningItems.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No commissioning items have been recorded yet.
              </div>
            ) : null}
            {project.commissioningItems.map((item) => (
              <div key={item.id} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.title}</div>
                      <Badge>{formatCatalogLabel(item.status)}</Badge>
                      {item.systemArea ? <Badge variant="outline">{item.systemArea}</Badge> : null}
                      {item.projectUnitName ? <Badge variant="secondary">{item.projectUnitCode ? `${item.projectUnitCode} · ${item.projectUnitName}` : item.projectUnitName}</Badge> : null}
                      {item.requiresRegulatoryInspection ? <Badge variant="secondary">Regulatory</Badge> : null}
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      {item.plannedDate ? <span>Planned {formatDateLabel(item.plannedDate)}</span> : null}
                      {item.completedDate ? <span>Completed {formatDateLabel(item.completedDate)}</span> : null}
                      {item.certificateReference ? <span>Cert {item.certificateReference}</span> : null}
                      {item.responsibleParty ? <span>{item.responsibleParty}</span> : null}
                    </div>
                    {item.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</div> : null}
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => onDeleteCommissioningItem(item.id)}>
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
          <CardTitle>Handover Register</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="rounded-lg border p-4 space-y-4">
            <div className="font-medium">Add Handover Item</div>
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2 md:col-span-2">
                <Label>Title</Label>
                <Input value={handoverDraft.title} onChange={(event) => setHandoverDraft((current) => ({ ...current, title: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Type</Label>
                <Select value={handoverDraft.handoverType || handoverTypeOptions[0]} onValueChange={(value) => setHandoverDraft((current) => ({ ...current, handoverType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{handoverTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={handoverDraft.status || handoverStatusOptions[0]} onValueChange={(value) => setHandoverDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{handoverStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Unit</Label>
                <Select value={handoverDraft.projectUnitId || 'none'} onValueChange={(value) => setHandoverDraft((current) => ({ ...current, projectUnitId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Whole project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Whole project</SelectItem>
                    {units.map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} · ${unit.name}` : unit.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Responsible Party</Label>
                <Input value={handoverDraft.responsibleParty || ''} onChange={(event) => setHandoverDraft((current) => ({ ...current, responsibleParty: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Reference</Label>
                <Input value={handoverDraft.referenceNumber || ''} onChange={(event) => setHandoverDraft((current) => ({ ...current, referenceNumber: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Target</Label>
                <Input type="date" value={handoverDraft.targetDate || ''} onChange={(event) => setHandoverDraft((current) => ({ ...current, targetDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Completed</Label>
                <Input type="date" value={handoverDraft.completedDate || ''} onChange={(event) => setHandoverDraft((current) => ({ ...current, completedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Notes</Label>
                <Textarea rows={2} value={handoverDraft.notes || ''} onChange={(event) => setHandoverDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end">
              <Button disabled={!handoverDraft.title?.trim()} onClick={onAddHandoverItem}>
                <Plus className="mr-2 h-4 w-4" />
                Add Handover Item
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {project.handoverItems.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No handover items have been recorded yet.
              </div>
            ) : null}
            {project.handoverItems.map((item) => (
              <div key={item.id} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.title}</div>
                      <Badge>{formatCatalogLabel(item.status)}</Badge>
                      <Badge variant="outline">{formatCatalogLabel(item.handoverType)}</Badge>
                      {item.projectUnitName ? <Badge variant="secondary">{item.projectUnitCode ? `${item.projectUnitCode} · ${item.projectUnitName}` : item.projectUnitName}</Badge> : null}
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      {item.responsibleParty ? <span>{item.responsibleParty}</span> : null}
                      {item.referenceNumber ? <span>Ref {item.referenceNumber}</span> : null}
                      {item.targetDate ? <span>Target {formatDateLabel(item.targetDate)}</span> : null}
                      {item.completedDate ? <span>Completed {formatDateLabel(item.completedDate)}</span> : null}
                    </div>
                    {item.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</div> : null}
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => onDeleteHandoverItem(item.id)}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
