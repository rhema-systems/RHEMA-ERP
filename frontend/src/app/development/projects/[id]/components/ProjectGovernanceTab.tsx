import { type Dispatch, type SetStateAction } from 'react';
import { Plus, Save, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { WorkflowRecordPanel } from '@/components/workflow/WorkflowRecordTab';
import {
  type CreateProjectActionItemDto,
  type CreateProjectChangeRequestDto,
  type CreateProjectDecisionDto,
  type CreateProjectIssueDto,
  type CreateProjectLessonLearnedDto,
  type CreateProjectMeetingMinuteDto,
  type CreateProjectNonConformanceDto,
  type CreateProjectQualityCheckpointDto,
  type CreateProjectRiskDto,
  type ProjectClosureDto,
  type ProjectDetailDto,
  type ProjectGovernanceSummaryDto,
  type UpsertProjectClosureDto,
  projectService,
} from '@/services/projectService';

type FlatWorkItem = { id: string; title: string };
type IdentifiedRecord = { id: string };
type ActHandler = (fn: () => Promise<unknown>, message: string, reset?: () => void) => Promise<void>;

type ProjectGovernanceTabProps = {
  project: ProjectDetailDto;
  flat: FlatWorkItem[];
  governanceSummary: ProjectGovernanceSummaryDto | null;
  risk: CreateProjectRiskDto;
  setRisk: Dispatch<SetStateAction<CreateProjectRiskDto>>;
  riskStatusOptions: string[];
  riskCategoryOptions: string[];
  riskResponseStrategyOptions: string[];
  issue: CreateProjectIssueDto;
  setIssue: Dispatch<SetStateAction<CreateProjectIssueDto>>;
  issueStatusOptions: string[];
  issueSeverityOptions: string[];
  qualityCheckpoint: CreateProjectQualityCheckpointDto;
  setQualityCheckpoint: Dispatch<SetStateAction<CreateProjectQualityCheckpointDto>>;
  qualityCheckpointStatusOptions: string[];
  nonConformance: CreateProjectNonConformanceDto;
  setNonConformance: Dispatch<SetStateAction<CreateProjectNonConformanceDto>>;
  nonConformanceStatusOptions: string[];
  nonConformanceSeverityOptions: string[];
  change: CreateProjectChangeRequestDto;
  setChange: Dispatch<SetStateAction<CreateProjectChangeRequestDto>>;
  changeTypeOptions: string[];
  changeStatusOptions: string[];
  decision: CreateProjectDecisionDto;
  setDecision: Dispatch<SetStateAction<CreateProjectDecisionDto>>;
  decisionStatusOptions: string[];
  meeting: CreateProjectMeetingMinuteDto;
  setMeeting: Dispatch<SetStateAction<CreateProjectMeetingMinuteDto>>;
  meetingTypeOptions: string[];
  actionItem: CreateProjectActionItemDto;
  setActionItem: Dispatch<SetStateAction<CreateProjectActionItemDto>>;
  actionItemPriorityOptions: string[];
  actionItemStatusOptions: string[];
  lessonLearned: CreateProjectLessonLearnedDto;
  setLessonLearned: Dispatch<SetStateAction<CreateProjectLessonLearnedDto>>;
  lessonCategoryOptions: string[];
  lessonVisibilityOptions: string[];
  closure: UpsertProjectClosureDto;
  setClosure: Dispatch<SetStateAction<UpsertProjectClosureDto>>;
  closureRecord: ProjectClosureDto | null;
  currentUserId?: string;
  activeUsers: IdentifiedRecord[];
  formatUserLabel: (user: IdentifiedRecord) => string;
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  boolValue: (value?: boolean | null) => string;
  getResolvedUserLabel: (userId?: string, displayName?: string, fallback?: string) => string;
  act: ActHandler;
  onLoad: () => Promise<void>;
  onOpenWorkflows: () => void;
};

const today = () => new Date().toISOString().slice(0, 10);
const riskInit: CreateProjectRiskDto = { title: '', status: 'Open', probability: 1, impact: 1 };
const issueInit: CreateProjectIssueDto = { title: '', status: 'Open', severity: 'Medium' };
const qualityCheckpointInit: CreateProjectQualityCheckpointDto = { title: '', status: 'Open', requiresQaSignOff: false };
const nonConformanceInit: CreateProjectNonConformanceDto = { title: '', severity: 'Medium', status: 'Open' };
const changeInit: CreateProjectChangeRequestDto = { title: '', status: 'Draft', changeType: 'Scope' };
const decisionInit: CreateProjectDecisionDto = { title: '', decisionDate: today(), status: 'Draft', rationale: '', alternativesConsidered: '', impactSummary: '' };
const meetingInit: CreateProjectMeetingMinuteDto = { title: '', meetingDate: today(), meetingType: 'Status', minutes: '', attendeesJson: '' };
const actionItemInit: CreateProjectActionItemDto = { title: '', description: '', status: 'Open', priority: 'Normal', dueDate: today() };
const lessonLearnedInit: CreateProjectLessonLearnedDto = { title: '', category: 'General', description: '', recommendation: '', appliedPhase: '', visibility: 'Internal' };

export function ProjectGovernanceTab({
  project,
  flat,
  governanceSummary,
  risk,
  setRisk,
  riskStatusOptions,
  riskCategoryOptions,
  riskResponseStrategyOptions,
  issue,
  setIssue,
  issueStatusOptions,
  issueSeverityOptions,
  qualityCheckpoint,
  setQualityCheckpoint,
  qualityCheckpointStatusOptions,
  nonConformance,
  setNonConformance,
  nonConformanceStatusOptions,
  nonConformanceSeverityOptions,
  change,
  setChange,
  changeTypeOptions,
  changeStatusOptions,
  decision,
  setDecision,
  decisionStatusOptions,
  meeting,
  setMeeting,
  meetingTypeOptions,
  actionItem,
  setActionItem,
  actionItemPriorityOptions,
  actionItemStatusOptions,
  lessonLearned,
  setLessonLearned,
  lessonCategoryOptions,
  lessonVisibilityOptions,
  closure,
  setClosure,
  closureRecord,
  currentUserId,
  activeUsers,
  formatUserLabel,
  formatCatalogLabel,
  formatDateLabel,
  boolValue,
  getResolvedUserLabel,
  act,
  onLoad,
  onOpenWorkflows,
}: ProjectGovernanceTabProps) {
  return (
    <div className="space-y-6"><Card>
            <CardHeader><CardTitle>Governance Snapshot</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {governanceSummary ? (
                <>
                  <div className="grid gap-4 md:grid-cols-4">
                    <div><div className="text-sm text-muted-foreground">Open Risks</div><div className="text-2xl font-semibold">{governanceSummary.openRiskCount}</div></div>
                    <div><div className="text-sm text-muted-foreground">Open Issues</div><div className="text-2xl font-semibold">{governanceSummary.openIssueCount}</div></div>
                    <div><div className="text-sm text-muted-foreground">Open Changes</div><div className="text-2xl font-semibold">{governanceSummary.openChangeRequestCount}</div></div>
                    <div><div className="text-sm text-muted-foreground">Open Actions</div><div className="text-2xl font-semibold">{governanceSummary.openActionItemCount}</div></div>
                  </div>
                  <div className="grid gap-4 md:grid-cols-3">
                    <div className="rounded-lg border p-4 text-sm">Deliverables pending approval: {governanceSummary.pendingDeliverableApprovalCount}</div>
                    <div className="rounded-lg border p-4 text-sm">Timesheets pending approval: {governanceSummary.pendingTimesheetApprovalCount}</div>
                    <div className="rounded-lg border p-4 text-sm">Expenses pending approval: {governanceSummary.pendingExpenseApprovalCount}</div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={governanceSummary.hasLockedBaseline ? 'secondary' : 'outline'}>{governanceSummary.hasLockedBaseline ? 'Baseline locked' : 'No locked baseline'}</Badge>
                    <Badge variant={governanceSummary.hasClosureDraft ? 'secondary' : 'outline'}>{governanceSummary.hasClosureDraft ? 'Closure draft exists' : 'No closure draft'}</Badge>
                    <Badge variant={governanceSummary.hasApprovedClosure ? 'secondary' : 'outline'}>{governanceSummary.hasApprovedClosure ? 'Closure approved' : 'Closure not approved'}</Badge>
                  </div>
                  <div className="space-y-2">
                    {governanceSummary.violations.length === 0 ? <div className="text-sm text-muted-foreground">No governance alerts are currently flagged.</div> : null}
                    {governanceSummary.violations.map((violation, index) => (
                      <div key={`${violation.area}-${index}`} className="rounded-lg border p-4 text-sm">
                        <div className="font-medium">{violation.area} | {violation.severity}</div>
                        <div className="text-muted-foreground">{violation.message}</div>
                      </div>
                    ))}
                  </div>
                </>
              ) : (
                <div className="text-sm text-muted-foreground">Governance details are not available for this user.</div>
              )}
            </CardContent>
          </Card>
          <Card><CardHeader><CardTitle>Risks</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-4"><div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={risk.title} onChange={(e) => setRisk((p) => ({ ...p, title: e.target.value }))} /></div><div className="grid gap-2"><Label>Status</Label><Select value={risk.status || riskStatusOptions[0]} onValueChange={(value) => setRisk((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{riskStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Category</Label><Select value={risk.category || riskCategoryOptions[0]} onValueChange={(value) => setRisk((p) => ({ ...p, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{riskCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Owner</Label><Select value={risk.ownerId || 'none'} onValueChange={(value) => setRisk((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Probability</Label><Input type="number" value={risk.probability ?? 1} onChange={(e) => setRisk((p) => ({ ...p, probability: Number(e.target.value || '1') }))} /></div><div className="grid gap-2"><Label>Impact</Label><Input type="number" value={risk.impact ?? 1} onChange={(e) => setRisk((p) => ({ ...p, impact: Number(e.target.value || '1') }))} /></div><div className="grid gap-2 md:col-span-2"><Label>Response Strategy</Label><Select value={risk.responseStrategy || riskResponseStrategyOptions[0]} onValueChange={(value) => setRisk((p) => ({ ...p, responseStrategy: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{riskResponseStrategyOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(() => projectService.addRisk(project.id, risk).then(() => Promise.resolve()), 'Risk added', () => setRisk(riskInit))}><Plus className="mr-2 h-4 w-4" />Add Risk</Button></div>{project.risks.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.title}</div><div className="text-sm text-muted-foreground">{x.status} | {x.category || 'General'} | Exposure {x.exposure}{x.ownerId ? ` | ${getResolvedUserLabel(x.ownerId, x.ownerDisplayName)}` : ''}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteRisk(x.id), 'Risk deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}</CardContent></Card>
          <Card><CardHeader><CardTitle>Issues</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-4"><div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={issue.title} onChange={(e) => setIssue((p) => ({ ...p, title: e.target.value }))} /></div><div className="grid gap-2"><Label>Status</Label><Select value={issue.status || issueStatusOptions[0]} onValueChange={(value) => setIssue((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{issueStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Severity</Label><Select value={issue.severity || issueSeverityOptions[0]} onValueChange={(value) => setIssue((p) => ({ ...p, severity: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{issueSeverityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Owner</Label><Select value={issue.ownerId || 'none'} onValueChange={(value) => setIssue((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(() => projectService.addIssue(project.id, issue).then(() => Promise.resolve()), 'Issue added', () => setIssue(issueInit))}><Plus className="mr-2 h-4 w-4" />Add Issue</Button></div>{project.issues.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.title}</div><div className="text-sm text-muted-foreground">{x.status} | {x.severity || 'Unspecified'}{x.ownerId ? ` | ${getResolvedUserLabel(x.ownerId, x.ownerDisplayName)}` : ''}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteIssue(x.id), 'Issue deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}</CardContent></Card>
          <Card>
            <CardHeader><CardTitle>Quality Checkpoints</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={qualityCheckpoint.title} onChange={(e) => setQualityCheckpoint((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={qualityCheckpoint.status || qualityCheckpointStatusOptions[0]} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{qualityCheckpointStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Due Date</Label><Input type="date" value={qualityCheckpoint.dueDate || ''} onChange={(e) => setQualityCheckpoint((p) => ({ ...p, dueDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={qualityCheckpoint.workItemId || 'none'} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No work item</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Deliverable</Label><Select value={qualityCheckpoint.deliverableId || 'none'} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, deliverableId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No deliverable</SelectItem>{project.deliverables.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>QA Owner</Label><Select value={qualityCheckpoint.qaOwnerId || 'none'} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, qaOwnerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>QA Sign-off</Label><Select value={boolValue(qualityCheckpoint.requiresQaSignOff)} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, requiresQaSignOff: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Not required</SelectItem><SelectItem value="true">Required</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-4"><Label>Description</Label><Textarea rows={2} value={qualityCheckpoint.description || ''} onChange={(e) => setQualityCheckpoint((p) => ({ ...p, description: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addQualityCheckpoint(project.id, qualityCheckpoint).then(() => Promise.resolve()), 'Quality checkpoint added', () => setQualityCheckpoint(qualityCheckpointInit))}><Plus className="mr-2 h-4 w-4" />Add Checkpoint</Button></div>
              <div className="space-y-3">
                {project.qualityCheckpoints.length === 0 ? <div className="text-sm text-muted-foreground">No quality checkpoints have been defined.</div> : null}
                {project.qualityCheckpoints.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.status}</Badge></div><div className="text-sm text-muted-foreground">{x.dueDate ? formatDateLabel(x.dueDate) : 'No due date'}{x.qaOwnerId ? ` | ${getResolvedUserLabel(x.qaOwnerId, x.qaOwnerDisplayName)}` : ''}{x.requiresQaSignOff ? ' | sign-off required' : ''}</div>{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}{x.signedOffAt ? <div className="mt-1 text-sm text-muted-foreground">Signed off {formatDateLabel(x.signedOffAt)}{x.signedOffById ? ` by ${getResolvedUserLabel(x.signedOffById, x.signedOffByDisplayName)}` : ''}{x.signOffNotes ? ` | ${x.signOffNotes}` : ''}</div> : null}</div><div className="flex gap-2">{x.requiresQaSignOff && !x.signedOffAt ? <Button size="sm" variant="outline" onClick={() => act(() => projectService.signOffQualityCheckpoint(x.id, 'Signed off from workspace'), 'Quality checkpoint signed off')}>Sign Off</Button> : null}<Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteQualityCheckpoint(x.id), 'Quality checkpoint deleted')}><Trash2 className="h-4 w-4" /></Button></div></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Non-Conformances</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={nonConformance.title} onChange={(e) => setNonConformance((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={nonConformance.status || nonConformanceStatusOptions[0]} onValueChange={(value) => setNonConformance((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{nonConformanceStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Severity</Label><Select value={nonConformance.severity || nonConformanceSeverityOptions[0]} onValueChange={(value) => setNonConformance((p) => ({ ...p, severity: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{nonConformanceSeverityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Checkpoint</Label><Select value={nonConformance.qualityCheckpointId || 'none'} onValueChange={(value) => setNonConformance((p) => ({ ...p, qualityCheckpointId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No checkpoint</SelectItem>{project.qualityCheckpoints.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Deliverable</Label><Select value={nonConformance.deliverableId || 'none'} onValueChange={(value) => setNonConformance((p) => ({ ...p, deliverableId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No deliverable</SelectItem>{project.deliverables.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Owner</Label><Select value={nonConformance.ownerId || 'none'} onValueChange={(value) => setNonConformance((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Target Resolution</Label><Input type="date" value={nonConformance.targetResolutionDate || ''} onChange={(e) => setNonConformance((p) => ({ ...p, targetResolutionDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Description</Label><Textarea rows={2} value={nonConformance.description || ''} onChange={(e) => setNonConformance((p) => ({ ...p, description: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Corrective Action</Label><Textarea rows={2} value={nonConformance.correctiveAction || ''} onChange={(e) => setNonConformance((p) => ({ ...p, correctiveAction: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Preventive Action</Label><Textarea rows={2} value={nonConformance.preventiveAction || ''} onChange={(e) => setNonConformance((p) => ({ ...p, preventiveAction: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addNonConformance(project.id, nonConformance).then(() => Promise.resolve()), 'Non-conformance added', () => setNonConformance(nonConformanceInit))}><Plus className="mr-2 h-4 w-4" />Add Non-Conformance</Button></div>
              <div className="space-y-3">
                {project.nonConformances.length === 0 ? <div className="text-sm text-muted-foreground">No non-conformances have been logged.</div> : null}
                {project.nonConformances.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant={x.status === 'Resolved' ? 'outline' : x.severity === 'Critical' ? 'destructive' : 'secondary'}>{x.status}</Badge></div><div className="text-sm text-muted-foreground">{x.severity} | reported {formatDateLabel(x.reportedAt)}{x.ownerId ? ` | ${getResolvedUserLabel(x.ownerId, x.ownerDisplayName)}` : ''}</div>{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}{x.correctiveAction ? <div className="mt-1 text-sm text-muted-foreground">Corrective: {x.correctiveAction}</div> : null}{x.preventiveAction ? <div className="mt-1 text-sm text-muted-foreground">Preventive: {x.preventiveAction}</div> : null}{x.resolutionNotes ? <div className="mt-1 text-sm text-muted-foreground">Resolution: {x.resolutionNotes}</div> : null}</div><div className="flex gap-2">{x.status !== 'Resolved' ? <Button size="sm" variant="outline" onClick={() => act(() => projectService.resolveNonConformance(x.id, 'Resolved from workspace'), 'Non-conformance resolved')}>Resolve</Button> : null}<Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteNonConformance(x.id), 'Non-conformance deleted')}><Trash2 className="h-4 w-4" /></Button></div></div>)}
              </div>
            </CardContent>
          </Card>
          <Card><CardHeader><CardTitle>Change Requests</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-4"><div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={change.title} onChange={(e) => setChange((p) => ({ ...p, title: e.target.value }))} /></div><div className="grid gap-2"><Label>Type</Label><Select value={change.changeType || changeTypeOptions[0]} onValueChange={(value) => setChange((p) => ({ ...p, changeType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{changeTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Status</Label><Select value={change.status || changeStatusOptions[0]} onValueChange={(value) => setChange((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{changeStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(() => projectService.addChangeRequest(project.id, change).then(() => Promise.resolve()), 'Change request added', () => setChange(changeInit))}><Plus className="mr-2 h-4 w-4" />Add Change</Button></div>{project.changeRequests.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.title}</div><div className="text-sm text-muted-foreground">{x.status} | {x.changeType || 'General'}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteChangeRequest(x.id), 'Change request deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}</CardContent></Card>
          <Card>
            <CardHeader><CardTitle>Decisions</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={decision.title} onChange={(e) => setDecision((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Decision Date</Label><Input type="date" value={decision.decisionDate || today()} onChange={(e) => setDecision((p) => ({ ...p, decisionDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={decision.status || decisionStatusOptions[0]} onValueChange={(value) => setDecision((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{decisionStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Rationale</Label><Textarea rows={2} value={decision.rationale || ''} onChange={(e) => setDecision((p) => ({ ...p, rationale: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Alternatives</Label><Textarea rows={2} value={decision.alternativesConsidered || ''} onChange={(e) => setDecision((p) => ({ ...p, alternativesConsidered: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Impact</Label><Textarea rows={2} value={decision.impactSummary || ''} onChange={(e) => setDecision((p) => ({ ...p, impactSummary: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addDecision(project.id, decision).then(() => Promise.resolve()), 'Decision logged', () => setDecision({ ...decisionInit, approverId: currentUserId || undefined }))}><Plus className="mr-2 h-4 w-4" />Add Decision</Button></div>
              <div className="space-y-3">
                {project.decisions.length === 0 ? <div className="text-sm text-muted-foreground">No decisions have been logged yet.</div> : null}
                {project.decisions.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.status}</Badge></div><div className="text-sm text-muted-foreground">{formatDateLabel(x.decisionDate)}{x.approverId ? ` | approver ${getResolvedUserLabel(x.approverId, x.approverDisplayName)}` : ''}{x.approvedAt ? ` | approved ${formatDateLabel(x.approvedAt)}` : ''}</div>{x.rationale ? <div className="mt-2 text-sm text-muted-foreground">{x.rationale}</div> : null}{x.impactSummary ? <div className="mt-1 text-sm text-muted-foreground">{x.impactSummary}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteDecision(x.id), 'Decision deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Meetings</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={meeting.title} onChange={(e) => setMeeting((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Date</Label><Input type="date" value={meeting.meetingDate || today()} onChange={(e) => setMeeting((p) => ({ ...p, meetingDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Type</Label><Select value={meeting.meetingType || meetingTypeOptions[0]} onValueChange={(value) => setMeeting((p) => ({ ...p, meetingType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{meetingTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-3"><Label>Minutes</Label><Textarea rows={3} value={meeting.minutes || ''} onChange={(e) => setMeeting((p) => ({ ...p, minutes: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Attendees</Label><Input value={meeting.attendeesJson || ''} onChange={(e) => setMeeting((p) => ({ ...p, attendeesJson: e.target.value }))} placeholder="Comma separated or JSON" /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addMeeting(project.id, meeting).then(() => Promise.resolve()), 'Meeting logged', () => setMeeting({ ...meetingInit, facilitatorId: currentUserId || undefined }))}><Plus className="mr-2 h-4 w-4" />Add Meeting</Button></div>
              <div className="space-y-3">
                {project.meetings.length === 0 ? <div className="text-sm text-muted-foreground">No meeting minutes have been captured yet.</div> : null}
                {project.meetings.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.meetingType}</Badge></div><div className="text-sm text-muted-foreground">{formatDateLabel(x.meetingDate)}{x.facilitatorId ? ` | facilitator ${getResolvedUserLabel(x.facilitatorId, x.facilitatorDisplayName)}` : ''}</div>{x.minutes ? <div className="mt-2 text-sm text-muted-foreground whitespace-pre-wrap">{x.minutes}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteMeeting(x.id), 'Meeting deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Action Items</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={actionItem.title} onChange={(e) => setActionItem((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Priority</Label><Select value={actionItem.priority || actionItemPriorityOptions[0]} onValueChange={(value) => setActionItem((p) => ({ ...p, priority: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{actionItemPriorityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Due Date</Label><Input type="date" value={actionItem.dueDate || today()} onChange={(e) => setActionItem((p) => ({ ...p, dueDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Meeting</Label><Select value={actionItem.meetingMinuteId || 'none'} onValueChange={(value) => setActionItem((p) => ({ ...p, meetingMinuteId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No meeting</SelectItem>{project.meetings.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={actionItem.workItemId || 'none'} onValueChange={(value) => setActionItem((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No work item</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2">
                  <Label>Owner</Label>
                  <Select value={actionItem.ownerId || 'none'} onValueChange={(value) => setActionItem((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No owner</SelectItem>
                      {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Status</Label><Select value={actionItem.status || actionItemStatusOptions[0]} onValueChange={(value) => setActionItem((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{actionItemStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-4"><Label>Description</Label><Textarea rows={2} value={actionItem.description || ''} onChange={(e) => setActionItem((p) => ({ ...p, description: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addActionItem(project.id, actionItem).then(() => Promise.resolve()), 'Action item added', () => setActionItem({ ...actionItemInit, ownerId: currentUserId || undefined }))}><Plus className="mr-2 h-4 w-4" />Add Action</Button></div>
              <div className="space-y-3">
                {project.actionItems.length === 0 ? <div className="text-sm text-muted-foreground">No action items have been captured yet.</div> : null}
                {project.actionItems.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant={x.status === 'Completed' || x.status === 'Closed' ? 'secondary' : 'outline'}>{x.status}</Badge><Badge variant="outline">{x.priority}</Badge></div><div className="text-sm text-muted-foreground">{x.meetingTitle || 'General'}{x.workItemTitle ? ` | ${x.workItemTitle}` : ''}{x.ownerId ? ` | ${getResolvedUserLabel(x.ownerId, x.ownerDisplayName)}` : ''}{x.dueDate ? ` | due ${formatDateLabel(x.dueDate)}` : ''}</div>{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteActionItem(x.id), 'Action item deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Lessons Learned</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={lessonLearned.title} onChange={(e) => setLessonLearned((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Category</Label><Select value={lessonLearned.category || lessonCategoryOptions[0]} onValueChange={(value) => setLessonLearned((p) => ({ ...p, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lessonCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Phase</Label><Input value={lessonLearned.appliedPhase || ''} onChange={(e) => setLessonLearned((p) => ({ ...p, appliedPhase: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Visibility</Label><Select value={lessonLearned.visibility || lessonVisibilityOptions[0]} onValueChange={(value) => setLessonLearned((p) => ({ ...p, visibility: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lessonVisibilityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-3"><Label>Description</Label><Textarea rows={2} value={lessonLearned.description || ''} onChange={(e) => setLessonLearned((p) => ({ ...p, description: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-4"><Label>Recommendation</Label><Textarea rows={2} value={lessonLearned.recommendation || ''} onChange={(e) => setLessonLearned((p) => ({ ...p, recommendation: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addLessonLearned(project.id, lessonLearned).then(() => Promise.resolve()), 'Lesson learned added', () => setLessonLearned(lessonLearnedInit))}><Plus className="mr-2 h-4 w-4" />Add Lesson</Button></div>
              <div className="space-y-3">
                {project.lessonsLearned.length === 0 ? <div className="text-sm text-muted-foreground">No lessons learned have been recorded yet.</div> : null}
                {project.lessonsLearned.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.category}</Badge><Badge variant="outline">{x.visibility}</Badge></div>{x.appliedPhase ? <div className="text-sm text-muted-foreground">Applied phase: {x.appliedPhase}</div> : null}{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}{x.recommendation ? <div className="mt-1 text-sm text-muted-foreground">{x.recommendation}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteLessonLearned(x.id), 'Lesson learned deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Closure</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-wrap gap-2">
                <Badge variant={closureRecord ? 'secondary' : 'outline'}>{closureRecord ? `Status ${closureRecord.status}` : 'No closure record yet'}</Badge>
                <Badge variant={closureRecord?.submittedAt ? 'secondary' : 'outline'}>{closureRecord?.submittedAt ? `Submitted ${formatDateLabel(closureRecord.submittedAt)}` : 'Not submitted'}</Badge>
                <Badge variant={closureRecord?.approvedAt ? 'secondary' : 'outline'}>{closureRecord?.approvedAt ? `Approved ${formatDateLabel(closureRecord.approvedAt)}` : 'Not approved'}</Badge>
              </div>
              {closureRecord?.rejectionReason ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{closureRecord.rejectionReason}</div> : null}
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2"><Label>Final Budget</Label><Input type="number" value={closure.finalBudget ?? 0} onChange={(e) => setClosure((p) => ({ ...p, finalBudget: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Final Cost</Label><Input type="number" value={closure.finalCost ?? 0} onChange={(e) => setClosure((p) => ({ ...p, finalCost: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Deliverables Accepted</Label><Select value={boolValue(closure.deliverablesAccepted)} onValueChange={(value) => setClosure((p) => ({ ...p, deliverablesAccepted: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Tasks Completed</Label><Select value={boolValue(closure.tasksCompletedOrWaived)} onValueChange={(value) => setClosure((p) => ({ ...p, tasksCompletedOrWaived: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Assets Reconciled</Label><Select value={boolValue(closure.assetsReconciled)} onValueChange={(value) => setClosure((p) => ({ ...p, assetsReconciled: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Open Items Disposed</Label><Select value={boolValue(closure.openItemsDisposed)} onValueChange={(value) => setClosure((p) => ({ ...p, openItemsDisposed: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Checklist</Label><Textarea rows={2} value={closure.closureChecklistJson || ''} onChange={(e) => setClosure((p) => ({ ...p, closureChecklistJson: e.target.value }))} placeholder="Checklist notes or JSON payload" /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Open Items Disposition</Label><Textarea rows={2} value={closure.openItemsDisposition || ''} onChange={(e) => setClosure((p) => ({ ...p, openItemsDisposition: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Asset Reconciliation</Label><Textarea rows={2} value={closure.assetReconciliationNotes || ''} onChange={(e) => setClosure((p) => ({ ...p, assetReconciliationNotes: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Lessons Learned Summary</Label><Textarea rows={2} value={closure.lessonsLearnedSummary || ''} onChange={(e) => setClosure((p) => ({ ...p, lessonsLearnedSummary: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Post-Implementation Review</Label><Textarea rows={2} value={closure.postImplementationReview || ''} onChange={(e) => setClosure((p) => ({ ...p, postImplementationReview: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Override Reason</Label><Textarea rows={2} value={closure.overrideReason || ''} onChange={(e) => setClosure((p) => ({ ...p, overrideReason: e.target.value }))} /></div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button onClick={() => act(() => projectService.upsertClosure(project.id, closure).then(() => Promise.resolve()), 'Closure draft saved')}><Save className="mr-2 h-4 w-4" />Save Closure Draft</Button>
              </div>
              {closureRecord ? (
                <WorkflowRecordPanel
                  entityType="ProjectClosure"
                  entityId={closureRecord.id}
                  entityLabel="Project Closure"
                  entityNumber={project.projectCode}
                  status={closureRecord.status}
                  canSubmit={closureRecord.status === 'Draft' || closureRecord.status === 'Rejected'}
                  canApproveReject={closureRecord.status === 'PendingApproval'}
                  onSubmit={() => projectService.submitClosure(project.id)}
                  onApprove={(comments) => projectService.approveClosure(closureRecord.id, comments)}
                  onReject={(comments) => projectService.rejectClosure(closureRecord.id, comments || 'Rejected', comments)}
                  onAfterAction={async () => onLoad()}
                  onOpenWorkflows={onOpenWorkflows}
                />
              ) : null}
            </CardContent>
          </Card>
    </div>
  );
}
