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
  CreateProjectApprovalRegisterItemDto,
  ProjectApprovalRegisterItemDto,
  ProjectDetailDto,
  ProjectPhaseDto,
} from '@/services/projectService';

type ProjectApprovalsTabProps = {
  project: ProjectDetailDto;
  phases: ProjectPhaseDto[];
  approvalDraft: CreateProjectApprovalRegisterItemDto;
  setApprovalDraft: Dispatch<SetStateAction<CreateProjectApprovalRegisterItemDto>>;
  approvalTypeOptions: string[];
  approvalStatusOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  onAddApproval: () => void;
  onDeleteApproval: (approvalId: string) => void;
};

const flattenPhases = (phases: ProjectPhaseDto[], depth = 0): Array<{ id: string; label: string }> =>
  phases.flatMap((phase) => [
    { id: phase.id, label: `${' '.repeat(depth * 2)}${phase.name}`.trimStart() },
    ...flattenPhases(phase.children || [], depth + 1),
  ]);

const countByStatus = (items: ProjectApprovalRegisterItemDto[], status: string) =>
  items.filter((item) => (item.status || '').toLowerCase() === status.toLowerCase()).length;

export function ProjectApprovalsTab({
  project,
  phases,
  approvalDraft,
  setApprovalDraft,
  approvalTypeOptions,
  approvalStatusOptions,
  formatCatalogLabel,
  formatDateLabel,
  onAddApproval,
  onDeleteApproval,
}: ProjectApprovalsTabProps) {
  const phaseOptions = useMemo(() => flattenPhases(phases), [phases]);
  const approvalItems = project.approvalRegister || [];
  const submittedCount = countByStatus(approvalItems, 'Submitted');
  const approvedCount = countByStatus(approvalItems, 'Approved');
  const conditionalCount = approvalItems.filter((item) => !!item.conditionSummary?.trim()).length;
  const expiringSoonCount = approvalItems.filter((item) => {
    if (!item.expiryDate) return false;
    const expiry = new Date(item.expiryDate);
    if (Number.isNaN(expiry.getTime())) return false;
    const daysUntilExpiry = Math.ceil((expiry.getTime() - Date.now()) / 86400000);
    return daysUntilExpiry >= 0 && daysUntilExpiry <= 30;
  }).length;

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Approval Register</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Approvals</div>
              <div className="text-2xl font-semibold">{approvalItems.length}</div>
              <div className="text-sm text-muted-foreground">Statutory and internal approvals tracked</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Submitted</div>
              <div className="text-2xl font-semibold">{submittedCount}</div>
              <div className="text-sm text-muted-foreground">Waiting on external decision</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Approved</div>
              <div className="text-2xl font-semibold">{approvedCount}</div>
              <div className="text-sm text-muted-foreground">Approved items on record</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Conditions / Expiry</div>
              <div className="text-2xl font-semibold">{conditionalCount + expiringSoonCount}</div>
              <div className="text-sm text-muted-foreground">{conditionalCount} conditional, {expiringSoonCount} expiring in 30 days</div>
            </div>
          </div>

          <div className="rounded-lg border p-4 space-y-4">
            <div className="font-medium">New Approval Item</div>
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2 md:col-span-2">
                <Label>Title</Label>
                <Input value={approvalDraft.title} onChange={(event) => setApprovalDraft((current) => ({ ...current, title: event.target.value }))} />
              </div>
              <div className="grid gap-2">
                <Label>Approval Type</Label>
                <Select value={approvalDraft.approvalType || approvalTypeOptions[0]} onValueChange={(value) => setApprovalDraft((current) => ({ ...current, approvalType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{approvalTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Status</Label>
                <Select value={approvalDraft.status || approvalStatusOptions[0]} onValueChange={(value) => setApprovalDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{approvalStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Phase</Label>
                <Select value={approvalDraft.projectPhaseId || 'none'} onValueChange={(value) => setApprovalDraft((current) => ({ ...current, projectPhaseId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select phase" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No phase</SelectItem>
                    {phaseOptions.map((phase) => <SelectItem key={phase.id} value={phase.id}>{phase.label}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Authority</Label>
                <Input value={approvalDraft.authorityName || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, authorityName: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Reference No.</Label>
                <Input value={approvalDraft.referenceNumber || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, referenceNumber: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Submitted</Label>
                <Input type="date" value={approvalDraft.submittedDate || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, submittedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Target Decision</Label>
                <Input type="date" value={approvalDraft.targetDecisionDate || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, targetDecisionDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Approved</Label>
                <Input type="date" value={approvalDraft.approvedDate || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, approvedDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Expiry</Label>
                <Input type="date" value={approvalDraft.expiryDate || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, expiryDate: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2">
                <Label>Required</Label>
                <Select value={approvalDraft.isRequired === false ? 'false' : 'true'} onValueChange={(value) => setApprovalDraft((current) => ({ ...current, isRequired: value === 'true' }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="true">Required</SelectItem>
                    <SelectItem value="false">Optional</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Conditions / Notes</Label>
                <Textarea rows={2} value={approvalDraft.conditionSummary || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, conditionSummary: event.target.value || undefined }))} />
              </div>
              <div className="grid gap-2 md:col-span-4">
                <Label>Additional Notes</Label>
                <Textarea rows={2} value={approvalDraft.notes || ''} onChange={(event) => setApprovalDraft((current) => ({ ...current, notes: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end">
              <Button disabled={!approvalDraft.title?.trim()} onClick={onAddApproval}>
                <Plus className="mr-2 h-4 w-4" />
                Add Approval
              </Button>
            </div>
          </div>

          <div className="space-y-3">
            {approvalItems.length === 0 ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                No approvals have been recorded for this project yet.
              </div>
            ) : null}
            {approvalItems.map((item) => (
              <div key={item.id} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.title}</div>
                      <Badge variant="outline">{formatCatalogLabel(item.approvalType)}</Badge>
                      <Badge>{formatCatalogLabel(item.status)}</Badge>
                      {item.projectPhaseName ? <Badge variant="secondary">{item.projectPhaseName}</Badge> : null}
                      {item.isRequired ? <Badge variant="secondary">Required</Badge> : null}
                    </div>
                    <div className="flex flex-wrap gap-3 text-sm text-muted-foreground">
                      {item.authorityName ? <span>{item.authorityName}</span> : null}
                      {item.referenceNumber ? <span>Ref {item.referenceNumber}</span> : null}
                      {item.submittedDate ? <span>Submitted {formatDateLabel(item.submittedDate)}</span> : null}
                      {item.targetDecisionDate ? <span>Target {formatDateLabel(item.targetDecisionDate)}</span> : null}
                      {item.approvedDate ? <span>Approved {formatDateLabel(item.approvedDate)}</span> : null}
                      {item.expiryDate ? <span>Expires {formatDateLabel(item.expiryDate)}</span> : null}
                    </div>
                    {item.conditionSummary ? <div className="text-sm text-muted-foreground">{item.conditionSummary}</div> : null}
                    {item.notes ? <div className="text-sm text-muted-foreground whitespace-pre-wrap">{item.notes}</div> : null}
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => onDeleteApproval(item.id)}>
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
