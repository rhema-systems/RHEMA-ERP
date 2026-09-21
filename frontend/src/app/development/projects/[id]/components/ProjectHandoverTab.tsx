import React, { type Dispatch, type SetStateAction, useMemo, useState } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';
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
  CreateProjectUnitHandoverBatchDto,
  ProjectDetailDto,
  ProjectHandoverItemDto,
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
  onAddHandoverBatch: (dto: CreateProjectUnitHandoverBatchDto) => Promise<void>;
  onDeleteHandoverBatch: (unitHandoverBatchId: string) => Promise<void>;
  onAddCommissioningItem: () => void;
  onDeleteCommissioningItem: (commissioningItemId: string) => void;
  editingHandoverItemId: string | null;
  onEditHandoverItem: (item: ProjectHandoverItemDto) => void;
  onCancelHandoverEdit: () => void;
  onAddHandoverItem: () => void;
  onDeleteHandoverItem: (handoverItemId: string) => void;
};

const DEFAULT_HANDOVER_BATCH_STATUSES = ['Planned', 'InPreparation', 'Active', 'Completed', 'Closed'];

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
  onAddHandoverBatch,
  onDeleteHandoverBatch,
  onAddCommissioningItem,
  onDeleteCommissioningItem,
  editingHandoverItemId,
  onEditHandoverItem,
  onCancelHandoverEdit,
  onAddHandoverItem,
  onDeleteHandoverItem,
}: ProjectHandoverTabProps) {
  const [handoverBatchDraft, setHandoverBatchDraft] = useState<CreateProjectUnitHandoverBatchDto>({
    name: '',
    status: DEFAULT_HANDOVER_BATCH_STATUSES[0],
  });
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
  const handoverBatchItemCounts = useMemo(
    () => project.handoverItems.reduce<Record<string, number>>((accumulator, item) => {
      if (item.projectUnitHandoverBatchId) {
        accumulator[item.projectUnitHandoverBatchId] = (accumulator[item.projectUnitHandoverBatchId] || 0) + 1;
      }
      return accumulator;
    }, {}),
    [project.handoverItems],
  );
  const selectedHandoverUnit = useMemo(
    () => units.find((item) => item.id === handoverDraft.projectUnitId),
    [handoverDraft.projectUnitId, units],
  );
  const availableHandoverBatches = useMemo(
    () => project.unitHandoverBatches.filter((item) => {
      if (selectedHandoverUnit?.projectBuildingId && item.projectBuildingId && item.projectBuildingId !== selectedHandoverUnit.projectBuildingId) {
        return false;
      }
      if (selectedHandoverUnit?.projectFloorId && item.projectFloorId && item.projectFloorId !== selectedHandoverUnit.projectFloorId) {
        return false;
      }
      return true;
    }),
    [project.unitHandoverBatches, selectedHandoverUnit?.projectBuildingId, selectedHandoverUnit?.projectFloorId],
  );

  const handleAddHandoverBatch = async () => {
    if (!handoverBatchDraft.name?.trim()) return;
    await onAddHandoverBatch({
      ...handoverBatchDraft,
      name: handoverBatchDraft.name.trim(),
      code: handoverBatchDraft.code?.trim() || undefined,
      notes: handoverBatchDraft.notes?.trim() || undefined,
      status: handoverBatchDraft.status || DEFAULT_HANDOVER_BATCH_STATUSES[0],
    });
    setHandoverBatchDraft({
      name: '',
      status: DEFAULT_HANDOVER_BATCH_STATUSES[0],
      projectBuildingId: handoverBatchDraft.projectBuildingId,
      projectFloorId: handoverBatchDraft.projectFloorId,
    });
  };

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
          <CardTitle>Phased Handover Batches</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-6 xl:grid-cols-[minmax(0,380px)_1fr]">
          <div className="space-y-3 rounded-lg border p-4">
            <div className="font-medium">Add Batch</div>
            <div className="grid gap-3">
              <div className="grid gap-2">
                <Label>Building</Label>
                <Select
                  value={handoverBatchDraft.projectBuildingId || 'none'}
                  onValueChange={(value) => setHandoverBatchDraft((current) => ({
                    ...current,
                    projectBuildingId: value === 'none' ? undefined : value,
                    projectFloorId: undefined,
                  }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional building" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No building</SelectItem>
                    {project.buildings.map((building) => <SelectItem key={building.id} value={building.id}>{building.code ? `${building.code} · ${building.name}` : building.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Floor</Label>
                <Select
                  value={handoverBatchDraft.projectFloorId || 'none'}
                  onValueChange={(value) => setHandoverBatchDraft((current) => ({ ...current, projectFloorId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional floor" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No floor</SelectItem>
                    {project.floors
                      .filter((floor) => !handoverBatchDraft.projectBuildingId || floor.projectBuildingId === handoverBatchDraft.projectBuildingId)
                      .map((floor) => <SelectItem key={floor.id} value={floor.id}>{floor.code ? `${floor.code} · ${floor.name}` : floor.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Code</Label>
                <Input value={handoverBatchDraft.code || ''} onChange={(event) => setHandoverBatchDraft((current) => ({ ...current, code: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Name</Label>
                <Input value={handoverBatchDraft.name || ''} onChange={(event) => setHandoverBatchDraft((current) => ({ ...current, name: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={handoverBatchDraft.status || DEFAULT_HANDOVER_BATCH_STATUSES[0]} onValueChange={(value) => setHandoverBatchDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{DEFAULT_HANDOVER_BATCH_STATUSES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Planned Handover</Label>
                <Input type="date" value={handoverBatchDraft.plannedHandoverDate || ''} onChange={(event) => setHandoverBatchDraft((current) => ({ ...current, plannedHandoverDate: event.target.value || undefined }))} />
              </div>
              <Button disabled={!handoverBatchDraft.name?.trim()} onClick={() => { void handleAddHandoverBatch(); }}>
                <Plus className="mr-2 h-4 w-4" />
                Add Handover Batch
              </Button>
            </div>
          </div>
          <div className="space-y-3">
            {project.unitHandoverBatches.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No phased handover batches have been recorded yet.
              </div>
            ) : null}
            {project.unitHandoverBatches.map((batch) => (
              <div key={batch.id} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{batch.name}</div>
                      {batch.code ? <Badge variant="outline">{batch.code}</Badge> : null}
                      <Badge variant="secondary">{formatCatalogLabel(batch.status)}</Badge>
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      <span>{batch.projectBuildingName || 'Whole project'}{batch.projectFloorName ? ` · ${batch.projectFloorName}` : ''}</span>
                      <span>{handoverBatchItemCounts[batch.id] || 0} handover items</span>
                      {batch.plannedHandoverDate ? <span>Planned {formatDateLabel(batch.plannedHandoverDate)}</span> : null}
                      {batch.actualHandoverDate ? <span>Actual {formatDateLabel(batch.actualHandoverDate)}</span> : null}
                    </div>
                    {batch.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{batch.notes}</div> : null}
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => { void onDeleteHandoverBatch(batch.id); }}>
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
            <div className="font-medium">{editingHandoverItemId ? 'Edit Handover Item' : 'Add Handover Item'}</div>
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
                <Select
                  value={handoverDraft.projectUnitId || 'none'}
                  onValueChange={(value) => setHandoverDraft((current) => {
                    const projectUnitId = value === 'none' ? undefined : value;
                    const selectedUnit = units.find((item) => item.id === projectUnitId);
                    const nextBatchStillValid = current.projectUnitHandoverBatchId && project.unitHandoverBatches.some((batch) => {
                      if (batch.id !== current.projectUnitHandoverBatchId) return false;
                      if (selectedUnit?.projectBuildingId && batch.projectBuildingId && batch.projectBuildingId !== selectedUnit.projectBuildingId) return false;
                      if (selectedUnit?.projectFloorId && batch.projectFloorId && batch.projectFloorId !== selectedUnit.projectFloorId) return false;
                      return true;
                    });
                    return {
                      ...current,
                      projectUnitId,
                      projectUnitHandoverBatchId: nextBatchStillValid ? current.projectUnitHandoverBatchId : undefined,
                    };
                  })}
                >
                  <SelectTrigger><SelectValue placeholder="Whole project" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Whole project</SelectItem>
                    {units.map((unit) => <SelectItem key={unit.id} value={unit.id}>{unit.code ? `${unit.code} · ${unit.name}` : unit.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Handover Batch</Label>
                <Select
                  value={handoverDraft.projectUnitHandoverBatchId || 'none'}
                  onValueChange={(value) => setHandoverDraft((current) => ({ ...current, projectUnitHandoverBatchId: value === 'none' ? undefined : value }))}
                >
                  <SelectTrigger><SelectValue placeholder="Optional batch" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No batch</SelectItem>
                    {availableHandoverBatches.map((batch) => <SelectItem key={batch.id} value={batch.id}>{batch.code ? `${batch.code} · ${batch.name}` : batch.name}</SelectItem>)}
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
            <div className="flex justify-end gap-2">
              {editingHandoverItemId && <Button variant="outline" onClick={onCancelHandoverEdit}>Cancel edit</Button>}
              <Button disabled={!handoverDraft.title?.trim()} onClick={onAddHandoverItem}>
                <Plus className="mr-2 h-4 w-4" />
                {editingHandoverItemId ? 'Save handover changes' : 'Add Handover Item'}
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
                      {item.projectUnitHandoverBatchName ? (
                        <Badge variant="secondary">
                          {item.projectUnitHandoverBatchCode ? `${item.projectUnitHandoverBatchCode} · ` : ''}{item.projectUnitHandoverBatchName}
                        </Badge>
                      ) : null}
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      {item.responsibleParty ? <span>{item.responsibleParty}</span> : null}
                      {item.referenceNumber ? <span>Ref {item.referenceNumber}</span> : null}
                      {item.targetDate ? <span>Target {formatDateLabel(item.targetDate)}</span> : null}
                      {item.completedDate ? <span>Completed {formatDateLabel(item.completedDate)}</span> : null}
                      {item.projectUnitHandoverBatchStatus ? <span>Batch {formatCatalogLabel(item.projectUnitHandoverBatchStatus)}</span> : null}
                    </div>
                    {item.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</div> : null}
                  </div>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => onEditHandoverItem(item)}>
                      <Pencil className="mr-2 h-4 w-4" />Edit handover
                    </Button>
                    <Button aria-label={`Delete handover ${item.title}`} variant="ghost" size="sm" onClick={() => onDeleteHandoverItem(item.id)}>
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
