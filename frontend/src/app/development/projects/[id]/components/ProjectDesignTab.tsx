import { type Dispatch, type SetStateAction } from 'react';
import { Plus, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type {
  CreateProjectDrawingDto,
  CreateProjectSubmittalDto,
  ProjectDrawingDto,
  ProjectPackageDto,
  ProjectPhaseDto,
  ProjectSubmittalDto,
} from '@/services/projectService';

type ProjectDesignTabProps = {
  drawings: ProjectDrawingDto[];
  submittals: ProjectSubmittalDto[];
  phases: ProjectPhaseDto[];
  packages: ProjectPackageDto[];
  drawingDraft: CreateProjectDrawingDto;
  setDrawingDraft: Dispatch<SetStateAction<CreateProjectDrawingDto>>;
  submittalDraft: CreateProjectSubmittalDto;
  setSubmittalDraft: Dispatch<SetStateAction<CreateProjectSubmittalDto>>;
  drawingStatusOptions: string[];
  drawingDisciplineOptions: string[];
  submittalStatusOptions: string[];
  submittalTypeOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  onAddDrawing: () => void;
  onDeleteDrawing: (drawingId: string) => void;
  onAddSubmittal: () => void;
  onDeleteSubmittal: (submittalId: string) => void;
};

const flattenPhases = (phases: ProjectPhaseDto[], depth = 0): Array<ProjectPhaseDto & { depth: number }> =>
  phases.flatMap((phase) => [{ ...phase, depth }, ...flattenPhases(phase.children ?? [], depth + 1)]);

export function ProjectDesignTab({
  drawings,
  submittals,
  phases,
  packages,
  drawingDraft,
  setDrawingDraft,
  submittalDraft,
  setSubmittalDraft,
  drawingStatusOptions,
  drawingDisciplineOptions,
  submittalStatusOptions,
  submittalTypeOptions,
  formatCatalogLabel,
  formatDateLabel,
  onAddDrawing,
  onDeleteDrawing,
  onAddSubmittal,
  onDeleteSubmittal,
}: ProjectDesignTabProps) {
  const phaseOptions = flattenPhases(phases);

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-4">
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Drawings</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{drawings.length}</div>
          </CardContent>
        </Card>
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">For Review</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{drawings.filter((item) => item.status === 'ForReview').length}</div>
          </CardContent>
        </Card>
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Submittals</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{submittals.length}</div>
          </CardContent>
        </Card>
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Awaiting Response</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{submittals.filter((item) => item.status === 'Submitted' || item.status === 'UnderReview').length}</div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.05fr,0.95fr]">
        <Card className="border-slate-200/70 shadow-sm">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Drawing Register</CardTitle>
            <CardDescription>Track issued, reviewed, construction, and as-built drawings by phase.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Drawing Number</Label>
                <Input value={drawingDraft.drawingNumber ?? ''} onChange={(event) => setDrawingDraft((current) => ({ ...current, drawingNumber: event.target.value }))} placeholder="A-201" />
              </div>
              <div className="space-y-2">
                <Label>Title</Label>
                <Input value={drawingDraft.title ?? ''} onChange={(event) => setDrawingDraft((current) => ({ ...current, title: event.target.value }))} placeholder="Typical Floor Plan" />
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-3">
              <div className="space-y-2">
                <Label>Discipline</Label>
                <Select value={drawingDraft.discipline || drawingDisciplineOptions[0]} onValueChange={(value) => setDrawingDraft((current) => ({ ...current, discipline: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {drawingDisciplineOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Status</Label>
                <Select value={drawingDraft.status || drawingStatusOptions[0]} onValueChange={(value) => setDrawingDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {drawingStatusOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Revision</Label>
                <Input value={drawingDraft.revision ?? ''} onChange={(event) => setDrawingDraft((current) => ({ ...current, revision: event.target.value }))} placeholder="Rev 02" />
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-3">
              <div className="space-y-2">
                <Label>Phase</Label>
                <Select value={drawingDraft.projectPhaseId || 'none'} onValueChange={(value) => setDrawingDraft((current) => ({ ...current, projectPhaseId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Any phase" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any phase</SelectItem>
                    {phaseOptions.map((phase) => (
                      <SelectItem key={phase.id} value={phase.id}>{`${' '.repeat(phase.depth * 2)}${phase.name}`}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Issued Date</Label>
                <Input type="date" value={drawingDraft.issuedDate?.slice(0, 10) ?? ''} onChange={(event) => setDrawingDraft((current) => ({ ...current, issuedDate: event.target.value || undefined }))} />
              </div>
              <div className="space-y-2">
                <Label>Review Due</Label>
                <Input type="date" value={drawingDraft.reviewDueDate?.slice(0, 10) ?? ''} onChange={(event) => setDrawingDraft((current) => ({ ...current, reviewDueDate: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea rows={3} value={drawingDraft.notes ?? ''} onChange={(event) => setDrawingDraft((current) => ({ ...current, notes: event.target.value }))} placeholder="Add issue / revision context." />
            </div>
            <div className="flex justify-end">
              <Button onClick={onAddDrawing}><Plus className="mr-2 h-4 w-4" />Save drawing</Button>
            </div>

            <div className="space-y-3">
              {drawings.length === 0 ? (
                <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 px-4 py-5 text-sm text-slate-600">
                  No drawings have been registered yet.
                </div>
              ) : drawings.map((drawing) => (
                <div key={drawing.id} className="rounded-xl border border-slate-200 bg-white px-4 py-4 shadow-sm">
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-slate-900">{drawing.drawingNumber}</span>
                        <Badge variant="outline">{formatCatalogLabel(drawing.discipline)}</Badge>
                        <Badge variant="secondary">{formatCatalogLabel(drawing.status)}</Badge>
                        {drawing.isAsBuilt ? <Badge className="bg-blue-100 text-blue-900 hover:bg-blue-100">As-built</Badge> : null}
                      </div>
                      <div className="text-sm text-slate-700">{drawing.title}</div>
                      <div className="flex flex-wrap gap-3 text-xs text-slate-500">
                        <span>Phase: {drawing.projectPhaseName || 'General'}</span>
                        <span>Revision: {drawing.revision || 'N/A'}</span>
                        <span>Issued: {formatDateLabel(drawing.issuedDate)}</span>
                        <span>Review Due: {formatDateLabel(drawing.reviewDueDate)}</span>
                      </div>
                      {drawing.notes ? <p className="text-xs text-slate-600">{drawing.notes}</p> : null}
                    </div>
                    <Button variant="ghost" size="icon" onClick={() => onDeleteDrawing(drawing.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card className="border-slate-200/70 shadow-sm">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Submittals</CardTitle>
            <CardDescription>Capture materials, shop drawings, samples, and technical submissions against packages.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Title</Label>
                <Input value={submittalDraft.title ?? ''} onChange={(event) => setSubmittalDraft((current) => ({ ...current, title: event.target.value }))} placeholder="Window system shop drawing" />
              </div>
              <div className="space-y-2">
                <Label>Reference</Label>
                <Input value={submittalDraft.referenceNumber ?? ''} onChange={(event) => setSubmittalDraft((current) => ({ ...current, referenceNumber: event.target.value }))} placeholder="SUB-014" />
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Type</Label>
                <Select value={submittalDraft.submittalType || submittalTypeOptions[0]} onValueChange={(value) => setSubmittalDraft((current) => ({ ...current, submittalType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {submittalTypeOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Status</Label>
                <Select value={submittalDraft.status || submittalStatusOptions[0]} onValueChange={(value) => setSubmittalDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {submittalStatusOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Phase</Label>
                <Select value={submittalDraft.projectPhaseId || 'none'} onValueChange={(value) => setSubmittalDraft((current) => ({ ...current, projectPhaseId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Any phase" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any phase</SelectItem>
                    {phaseOptions.map((phase) => (
                      <SelectItem key={phase.id} value={phase.id}>{`${' '.repeat(phase.depth * 2)}${phase.name}`}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Package</Label>
                <Select value={submittalDraft.projectPackageId || 'none'} onValueChange={(value) => setSubmittalDraft((current) => ({ ...current, projectPackageId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Any package" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Any package</SelectItem>
                    {packages.map((item) => (
                      <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Submitted Date</Label>
                <Input type="date" value={submittalDraft.submittedDate?.slice(0, 10) ?? ''} onChange={(event) => setSubmittalDraft((current) => ({ ...current, submittedDate: event.target.value || undefined }))} />
              </div>
              <div className="space-y-2">
                <Label>Response Due</Label>
                <Input type="date" value={submittalDraft.responseDueDate?.slice(0, 10) ?? ''} onChange={(event) => setSubmittalDraft((current) => ({ ...current, responseDueDate: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea rows={3} value={submittalDraft.notes ?? ''} onChange={(event) => setSubmittalDraft((current) => ({ ...current, notes: event.target.value }))} placeholder="Add consultant / package coordination notes." />
            </div>
            <div className="flex justify-end">
              <Button onClick={onAddSubmittal}><Plus className="mr-2 h-4 w-4" />Save submittal</Button>
            </div>

            <div className="space-y-3">
              {submittals.length === 0 ? (
                <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 px-4 py-5 text-sm text-slate-600">
                  No submittals have been registered yet.
                </div>
              ) : submittals.map((submittal) => (
                <div key={submittal.id} className="rounded-xl border border-slate-200 bg-white px-4 py-4 shadow-sm">
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-slate-900">{submittal.title}</span>
                        <Badge variant="outline">{formatCatalogLabel(submittal.submittalType)}</Badge>
                        <Badge variant="secondary">{formatCatalogLabel(submittal.status)}</Badge>
                      </div>
                      <div className="flex flex-wrap gap-3 text-xs text-slate-500">
                        <span>Ref: {submittal.referenceNumber || 'N/A'}</span>
                        <span>Phase: {submittal.projectPhaseName || 'General'}</span>
                        <span>Package: {submittal.projectPackageName || 'Unassigned'}</span>
                        <span>Submitted: {formatDateLabel(submittal.submittedDate)}</span>
                        <span>Due: {formatDateLabel(submittal.responseDueDate)}</span>
                      </div>
                      {submittal.notes ? <p className="text-xs text-slate-600">{submittal.notes}</p> : null}
                    </div>
                    <Button variant="ghost" size="icon" onClick={() => onDeleteSubmittal(submittal.id)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
