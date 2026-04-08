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
  CreateProjectRfiDto,
  CreateProjectSiteInstructionDto,
  ProjectPackageDto,
  ProjectPhaseDto,
  ProjectRfiDto,
  ProjectSiteInstructionDto,
} from '@/services/projectService';

type ProjectSiteControlsTabProps = {
  rfis: ProjectRfiDto[];
  siteInstructions: ProjectSiteInstructionDto[];
  phases: ProjectPhaseDto[];
  packages: ProjectPackageDto[];
  rfiDraft: CreateProjectRfiDto;
  setRfiDraft: Dispatch<SetStateAction<CreateProjectRfiDto>>;
  siteInstructionDraft: CreateProjectSiteInstructionDto;
  setSiteInstructionDraft: Dispatch<SetStateAction<CreateProjectSiteInstructionDto>>;
  rfiStatusOptions: string[];
  rfiPriorityOptions: string[];
  siteInstructionStatusOptions: string[];
  siteInstructionTypeOptions: string[];
  currencyOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  formatDateLabel: (value?: string) => string;
  formatMoney: (amount?: number, currency?: string) => string;
  onAddRfi: () => void;
  onDeleteRfi: (rfiId: string) => void;
  onAddSiteInstruction: () => void;
  onDeleteSiteInstruction: (siteInstructionId: string) => void;
};

const flattenPhases = (phases: ProjectPhaseDto[], depth = 0): Array<ProjectPhaseDto & { depth: number }> =>
  phases.flatMap((phase) => [{ ...phase, depth }, ...flattenPhases(phase.children ?? [], depth + 1)]);

export function ProjectSiteControlsTab({
  rfis,
  siteInstructions,
  phases,
  packages,
  rfiDraft,
  setRfiDraft,
  siteInstructionDraft,
  setSiteInstructionDraft,
  rfiStatusOptions,
  rfiPriorityOptions,
  siteInstructionStatusOptions,
  siteInstructionTypeOptions,
  currencyOptions,
  formatCatalogLabel,
  formatDateLabel,
  formatMoney,
  onAddRfi,
  onDeleteRfi,
  onAddSiteInstruction,
  onDeleteSiteInstruction,
}: ProjectSiteControlsTabProps) {
  const phaseOptions = flattenPhases(phases);

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-4">
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">RFIs</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{rfis.length}</div>
          </CardContent>
        </Card>
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Open RFIs</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{rfis.filter((item) => item.status === 'Draft' || item.status === 'Submitted').length}</div>
          </CardContent>
        </Card>
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Instructions</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{siteInstructions.length}</div>
          </CardContent>
        </Card>
        <Card className="border-slate-200/70 shadow-sm">
          <CardContent className="px-4 py-4">
            <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">Live Instructions</div>
            <div className="mt-2 text-2xl font-semibold text-slate-900">{siteInstructions.filter((item) => item.status === 'Issued' || item.status === 'InProgress').length}</div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.05fr,0.95fr]">
        <Card className="border-slate-200/70 shadow-sm">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Requests For Information</CardTitle>
            <CardDescription>Track formal clarification requests against phases and packages during execution.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Subject</Label>
                <Input value={rfiDraft.subject ?? ''} onChange={(event) => setRfiDraft((current) => ({ ...current, subject: event.target.value }))} placeholder="Clarify bathroom waterproofing detail" />
              </div>
              <div className="space-y-2">
                <Label>Reference</Label>
                <Input value={rfiDraft.referenceNumber ?? ''} onChange={(event) => setRfiDraft((current) => ({ ...current, referenceNumber: event.target.value }))} placeholder="RFI-009" />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Question</Label>
              <Textarea rows={4} value={rfiDraft.question ?? ''} onChange={(event) => setRfiDraft((current) => ({ ...current, question: event.target.value }))} placeholder="State the required clarification or conflict needing response." />
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Priority</Label>
                <Select value={rfiDraft.priority || rfiPriorityOptions[0]} onValueChange={(value) => setRfiDraft((current) => ({ ...current, priority: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {rfiPriorityOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Status</Label>
                <Select value={rfiDraft.status || rfiStatusOptions[0]} onValueChange={(value) => setRfiDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {rfiStatusOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Phase</Label>
                <Select value={rfiDraft.projectPhaseId || 'none'} onValueChange={(value) => setRfiDraft((current) => ({ ...current, projectPhaseId: value === 'none' ? undefined : value }))}>
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
                <Select value={rfiDraft.projectPackageId || 'none'} onValueChange={(value) => setRfiDraft((current) => ({ ...current, projectPackageId: value === 'none' ? undefined : value }))}>
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
                <Label>Raised Date</Label>
                <Input type="date" value={rfiDraft.raisedDate?.slice(0, 10) ?? ''} onChange={(event) => setRfiDraft((current) => ({ ...current, raisedDate: event.target.value || undefined }))} />
              </div>
              <div className="space-y-2">
                <Label>Response Due</Label>
                <Input type="date" value={rfiDraft.responseDueDate?.slice(0, 10) ?? ''} onChange={(event) => setRfiDraft((current) => ({ ...current, responseDueDate: event.target.value || undefined }))} />
              </div>
            </div>
            <div className="flex justify-end">
              <Button onClick={onAddRfi}><Plus className="mr-2 h-4 w-4" />Save RFI</Button>
            </div>

            <div className="space-y-3">
              {rfis.length === 0 ? (
                <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 px-4 py-5 text-sm text-slate-600">
                  No RFIs have been logged yet.
                </div>
              ) : rfis.map((rfi) => (
                <div key={rfi.id} className="rounded-xl border border-slate-200 bg-white px-4 py-4 shadow-sm">
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-slate-900">{rfi.subject}</span>
                        <Badge variant="outline">{formatCatalogLabel(rfi.priority)}</Badge>
                        <Badge variant="secondary">{formatCatalogLabel(rfi.status)}</Badge>
                      </div>
                      <div className="text-xs text-slate-500">
                        Ref: {rfi.referenceNumber || 'N/A'} | Phase: {rfi.projectPhaseName || 'General'} | Package: {rfi.projectPackageName || 'Unassigned'}
                      </div>
                      <p className="text-sm text-slate-700">{rfi.question}</p>
                      <div className="text-xs text-slate-500">Raised {formatDateLabel(rfi.raisedDate)} | Due {formatDateLabel(rfi.responseDueDate)}</div>
                    </div>
                    <Button variant="ghost" size="icon" onClick={() => onDeleteRfi(rfi.id)}>
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
            <CardTitle className="text-base">Site Instructions</CardTitle>
            <CardDescription>Record formal instructions affecting package execution, cost, and time on site.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Title</Label>
                <Input value={siteInstructionDraft.title ?? ''} onChange={(event) => setSiteInstructionDraft((current) => ({ ...current, title: event.target.value }))} placeholder="Revise external paving layout" />
              </div>
              <div className="space-y-2">
                <Label>Reference</Label>
                <Input value={siteInstructionDraft.referenceNumber ?? ''} onChange={(event) => setSiteInstructionDraft((current) => ({ ...current, referenceNumber: event.target.value }))} placeholder="SI-004" />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea rows={4} value={siteInstructionDraft.description ?? ''} onChange={(event) => setSiteInstructionDraft((current) => ({ ...current, description: event.target.value }))} placeholder="Describe the formal site instruction and expected action." />
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Instruction Type</Label>
                <Select value={siteInstructionDraft.instructionType || siteInstructionTypeOptions[0]} onValueChange={(value) => setSiteInstructionDraft((current) => ({ ...current, instructionType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {siteInstructionTypeOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Status</Label>
                <Select value={siteInstructionDraft.status || siteInstructionStatusOptions[0]} onValueChange={(value) => setSiteInstructionDraft((current) => ({ ...current, status: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {siteInstructionStatusOptions.map((item) => (
                      <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Phase</Label>
                <Select value={siteInstructionDraft.projectPhaseId || 'none'} onValueChange={(value) => setSiteInstructionDraft((current) => ({ ...current, projectPhaseId: value === 'none' ? undefined : value }))}>
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
                <Select value={siteInstructionDraft.projectPackageId || 'none'} onValueChange={(value) => setSiteInstructionDraft((current) => ({ ...current, projectPackageId: value === 'none' ? undefined : value }))}>
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
            <div className="grid gap-4 md:grid-cols-3">
              <div className="space-y-2">
                <Label>Issued Date</Label>
                <Input type="date" value={siteInstructionDraft.issuedDate?.slice(0, 10) ?? ''} onChange={(event) => setSiteInstructionDraft((current) => ({ ...current, issuedDate: event.target.value || undefined }))} />
              </div>
              <div className="space-y-2">
                <Label>Cost Impact</Label>
                <Input type="number" min={0} step="0.01" value={siteInstructionDraft.estimatedCostImpact ?? ''} onChange={(event) => setSiteInstructionDraft((current) => ({ ...current, estimatedCostImpact: Number.isFinite(event.target.valueAsNumber) ? event.target.valueAsNumber : undefined }))} />
              </div>
              <div className="space-y-2">
                <Label>Currency</Label>
                <Select value={siteInstructionDraft.currency || currencyOptions[0] || 'USD'} onValueChange={(value) => setSiteInstructionDraft((current) => ({ ...current, currency: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((item) => (
                      <SelectItem key={item} value={item}>{item}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="flex justify-end">
              <Button onClick={onAddSiteInstruction}><Plus className="mr-2 h-4 w-4" />Save instruction</Button>
            </div>

            <div className="space-y-3">
              {siteInstructions.length === 0 ? (
                <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 px-4 py-5 text-sm text-slate-600">
                  No site instructions have been issued yet.
                </div>
              ) : siteInstructions.map((instruction) => (
                <div key={instruction.id} className="rounded-xl border border-slate-200 bg-white px-4 py-4 shadow-sm">
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-slate-900">{instruction.title}</span>
                        <Badge variant="outline">{formatCatalogLabel(instruction.instructionType)}</Badge>
                        <Badge variant="secondary">{formatCatalogLabel(instruction.status)}</Badge>
                      </div>
                      <div className="text-xs text-slate-500">
                        Ref: {instruction.referenceNumber || 'N/A'} | Phase: {instruction.projectPhaseName || 'General'} | Package: {instruction.projectPackageName || 'Unassigned'}
                      </div>
                      {instruction.description ? <p className="text-sm text-slate-700">{instruction.description}</p> : null}
                      <div className="text-xs text-slate-500">
                        Issued {formatDateLabel(instruction.issuedDate)}
                        {instruction.estimatedCostImpact ? ` | Cost impact ${formatMoney(instruction.estimatedCostImpact, instruction.currency)}` : ''}
                      </div>
                    </div>
                    <Button variant="ghost" size="icon" onClick={() => onDeleteSiteInstruction(instruction.id)}>
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
