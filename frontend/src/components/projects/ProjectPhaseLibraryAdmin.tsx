'use client';

import { useEffect, useMemo, useState } from 'react';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  type CreateProjectPhaseTemplateDto,
  type CreateProjectStageGateRuleDto,
  type ProjectPhaseTemplateDto,
  type ProjectStageGateRuleDto,
  type ProjectTypeDto,
  projectService,
} from '@/services/projectService';

const FILTER_ALL = '__all__';
const NONE_VALUE = '__none__';
const GLOBAL_SCOPE = '__global__';

const DELIVERY_STRUCTURES = ['WholeDevelopment', 'SingleUnit', 'MultiUnit'];
const PHASE_STATUS_OPTIONS = ['NotStarted', 'InProgress', 'Blocked', 'Completed', 'Waived', 'Cancelled'];
const REQUIREMENT_TYPES = [
  'ApprovedApprovals',
  'Packages',
  'BoqItems',
  'Documents',
  'CompletedCommissioningItems',
  'CompletedHandoverItems',
  'OpenSnagItems',
];
const GATE_SCOPES = ['Phase', 'Project'];

const createPhaseTemplateForm = (): CreateProjectPhaseTemplateDto => ({
  name: '',
  code: '',
  defaultStatus: 'NotStarted',
  sortOrder: 10,
  isOptional: false,
  isStageGateRequired: false,
  isActive: true,
});

const createStageGateRuleForm = (): CreateProjectStageGateRuleDto => ({
  projectPhaseTemplateId: '',
  code: '',
  name: '',
  requirementType: 'Documents',
  scope: 'Project',
  minimumCount: 1,
  isBlocking: true,
  sortOrder: 10,
  isActive: true,
});

type FlattenedTemplate = ProjectPhaseTemplateDto & { depth: number };

const flattenTemplates = (templates: ProjectPhaseTemplateDto[], depth = 0): FlattenedTemplate[] =>
  templates.flatMap((template) => [
    { ...template, depth },
    ...flattenTemplates(template.children ?? [], depth + 1),
  ]);

const formatLabel = (value?: string | null) =>
  (value || 'Not set')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

interface Props {
  onChanged?: () => Promise<void> | void;
}

export function ProjectPhaseLibraryAdmin({ onChanged }: Props) {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [projectTypes, setProjectTypes] = useState<ProjectTypeDto[]>([]);
  const [phaseTemplates, setPhaseTemplates] = useState<ProjectPhaseTemplateDto[]>([]);
  const [stageGateRules, setStageGateRules] = useState<ProjectStageGateRuleDto[]>([]);
  const [selectedProjectTypeId, setSelectedProjectTypeId] = useState(FILTER_ALL);
  const [selectedTemplateId, setSelectedTemplateId] = useState(FILTER_ALL);
  const [editingTemplateId, setEditingTemplateId] = useState<string | null>(null);
  const [editingRuleId, setEditingRuleId] = useState<string | null>(null);
  const [phaseTemplateForm, setPhaseTemplateForm] = useState<CreateProjectPhaseTemplateDto>(createPhaseTemplateForm);
  const [stageGateRuleForm, setStageGateRuleForm] = useState<CreateProjectStageGateRuleDto>(createStageGateRuleForm);

  const flattenedTemplates = useMemo(() => flattenTemplates(phaseTemplates), [phaseTemplates]);

  const loadData = async (projectTypeId: string = selectedProjectTypeId, templateId: string = selectedTemplateId) => {
    try {
      setLoading(true);
      const [loadedProjectTypes, loadedTemplates, loadedRules] = await Promise.all([
        projectService.getProjectTypes(),
        projectService.getProjectPhaseTemplates(projectTypeId !== FILTER_ALL ? projectTypeId : undefined),
        projectService.getProjectStageGateRules(templateId !== FILTER_ALL ? templateId : undefined),
      ]);

      setProjectTypes(loadedProjectTypes);
      setPhaseTemplates(loadedTemplates);
      setStageGateRules(loadedRules);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load phase library');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData(FILTER_ALL, FILTER_ALL);
  }, []);

  useEffect(() => {
    if (loading) return;
    const nextTemplateId =
      selectedTemplateId !== FILTER_ALL && flattenedTemplates.some((template) => template.id === selectedTemplateId)
        ? selectedTemplateId
        : FILTER_ALL;
    if (nextTemplateId !== selectedTemplateId) {
      setSelectedTemplateId(nextTemplateId);
      return;
    }

    void loadData(selectedProjectTypeId, nextTemplateId);
  }, [selectedProjectTypeId]);

  useEffect(() => {
    if (loading) return;
    void loadData(selectedProjectTypeId, selectedTemplateId);
  }, [selectedTemplateId]);

  const resetTemplateForm = () => {
    setEditingTemplateId(null);
    setPhaseTemplateForm(createPhaseTemplateForm());
  };

  const resetRuleForm = () => {
    setEditingRuleId(null);
    setStageGateRuleForm({
      ...createStageGateRuleForm(),
      projectPhaseTemplateId:
        selectedTemplateId !== FILTER_ALL && flattenedTemplates.some((template) => template.id === selectedTemplateId)
          ? selectedTemplateId
          : '',
    });
  };

  const savePhaseTemplate = async () => {
    try {
      setSaving(true);
      const payload = {
        ...phaseTemplateForm,
        projectTypeId: phaseTemplateForm.projectTypeId || undefined,
        parentPhaseTemplateId: phaseTemplateForm.parentPhaseTemplateId || undefined,
        appliesToDeliveryStructure: phaseTemplateForm.appliesToDeliveryStructure || undefined,
        appliesToDevelopmentType: phaseTemplateForm.appliesToDevelopmentType?.trim() || undefined,
      };

      if (editingTemplateId) {
        await projectService.updateProjectPhaseTemplate(editingTemplateId, payload);
      } else {
        await projectService.createProjectPhaseTemplate(payload);
      }

      await loadData(selectedProjectTypeId, selectedTemplateId);
      await onChanged?.();
      resetTemplateForm();
      toast.success('Phase template saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save phase template');
    } finally {
      setSaving(false);
    }
  };

  const saveStageGateRule = async () => {
    try {
      setSaving(true);
      const payload = {
        ...stageGateRuleForm,
        scope: stageGateRuleForm.scope || 'Project',
        minimumCount:
          stageGateRuleForm.minimumCount === undefined || Number.isNaN(stageGateRuleForm.minimumCount)
            ? undefined
            : stageGateRuleForm.minimumCount,
        maximumCount:
          stageGateRuleForm.maximumCount === undefined || Number.isNaN(stageGateRuleForm.maximumCount)
            ? undefined
            : stageGateRuleForm.maximumCount,
      };

      if (editingRuleId) {
        await projectService.updateProjectStageGateRule(editingRuleId, payload);
      } else {
        await projectService.createProjectStageGateRule(payload);
      }

      await loadData(selectedProjectTypeId, selectedTemplateId);
      await onChanged?.();
      resetRuleForm();
      toast.success('Stage gate rule saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save stage gate rule');
    } finally {
      setSaving(false);
    }
  };

  const beginTemplateEdit = (template: ProjectPhaseTemplateDto) => {
    setEditingTemplateId(template.id);
    setPhaseTemplateForm({
      parentPhaseTemplateId: template.parentPhaseTemplateId,
      projectTypeId: template.projectTypeId,
      code: template.code || '',
      name: template.name,
      description: template.description || '',
      defaultStatus: template.defaultStatus,
      sortOrder: template.sortOrder,
      isOptional: template.isOptional,
      isStageGateRequired: template.isStageGateRequired,
      isActive: template.isActive,
      appliesToDeliveryStructure: template.appliesToDeliveryStructure || '',
      appliesToDevelopmentType: template.appliesToDevelopmentType || '',
    });
  };

  const beginRuleEdit = (rule: ProjectStageGateRuleDto) => {
    setEditingRuleId(rule.id);
    setStageGateRuleForm({
      projectPhaseTemplateId: rule.projectPhaseTemplateId,
      code: rule.code,
      name: rule.name,
      description: rule.description || '',
      requirementType: rule.requirementType,
      scope: rule.scope,
      minimumCount: rule.minimumCount,
      maximumCount: rule.maximumCount,
      isBlocking: rule.isBlocking,
      sortOrder: rule.sortOrder,
      isActive: rule.isActive,
    });
    setSelectedTemplateId(rule.projectPhaseTemplateId);
  };

  const removeTemplate = async (templateId: string) => {
    if (!window.confirm('Delete this phase template and its child templates/rules?')) return;

    try {
      await projectService.deleteProjectPhaseTemplate(templateId);
      await loadData(selectedProjectTypeId, selectedTemplateId === templateId ? FILTER_ALL : selectedTemplateId);
      await onChanged?.();
      if (editingTemplateId === templateId) {
        resetTemplateForm();
      }
      toast.success('Phase template deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete phase template');
    }
  };

  const removeRule = async (ruleId: string) => {
    if (!window.confirm('Delete this stage gate rule?')) return;

    try {
      await projectService.deleteProjectStageGateRule(ruleId);
      await loadData(selectedProjectTypeId, selectedTemplateId);
      await onChanged?.();
      if (editingRuleId === ruleId) {
        resetRuleForm();
      }
      toast.success('Stage gate rule deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete stage gate rule');
    }
  };

  if (loading) {
    return <div className="py-16 text-center text-sm text-muted-foreground">Loading phase library...</div>;
  }

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Phase Templates</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{flattenedTemplates.length}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Stage Gate Rules</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">{stageGateRules.length}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Stage-Gated Phases</CardTitle>
          </CardHeader>
          <CardContent className="text-3xl font-semibold">
            {flattenedTemplates.filter((template) => template.isStageGateRequired).length}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Filtered Scope</CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-slate-600">
            {selectedProjectTypeId === FILTER_ALL
              ? 'All project types'
              : projectTypes.find((projectType) => projectType.id === selectedProjectTypeId)?.name || 'Selected scope'}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.1fr,0.9fr]">
        <Card className="border-slate-200/70 shadow-sm">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Phase Library</CardTitle>
            <CardDescription>
              Define the reusable construction phases here, then let user projects inherit them during setup.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Project Type Scope</Label>
                <Select value={selectedProjectTypeId} onValueChange={setSelectedProjectTypeId}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={FILTER_ALL}>All project types</SelectItem>
                    {projectTypes.map((projectType) => (
                      <SelectItem key={projectType.id} value={projectType.id}>
                        {projectType.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Stage Gate Focus</Label>
                <Select value={selectedTemplateId} onValueChange={setSelectedTemplateId}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={FILTER_ALL}>All phase templates</SelectItem>
                    {flattenedTemplates.map((template) => (
                      <SelectItem key={template.id} value={template.id}>
                        {'\u00A0'.repeat(template.depth * 2)}
                        {template.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <ScrollArea className="h-[420px] rounded-xl border border-slate-200">
              <div className="divide-y divide-slate-200">
                {flattenedTemplates.map((template) => (
                  <div key={template.id} className="flex flex-col gap-3 px-4 py-4 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-slate-900" style={{ marginLeft: `${template.depth * 18}px` }}>
                          {template.name}
                        </span>
                        {template.code ? (
                          <Badge variant="outline" className="border-slate-300 text-[11px] uppercase tracking-[0.16em]">
                            {template.code}
                          </Badge>
                        ) : null}
                        <Badge variant="secondary" className="text-[11px]">
                          {formatLabel(template.defaultStatus)}
                        </Badge>
                        {template.projectTypeName ? (
                          <Badge variant="outline" className="border-blue-300 text-[11px] text-blue-700">
                            {template.projectTypeName}
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="border-slate-300 text-[11px]">
                            Global
                          </Badge>
                        )}
                        {template.isStageGateRequired ? (
                          <Badge className="bg-amber-100 text-[11px] text-amber-900 hover:bg-amber-100">
                            Stage Gate
                          </Badge>
                        ) : null}
                        {!template.isActive ? (
                          <Badge className="bg-slate-100 text-[11px] text-slate-800 hover:bg-slate-100">
                            Inactive
                          </Badge>
                        ) : null}
                      </div>
                      {template.description ? (
                        <p className="mt-2 text-sm text-slate-600" style={{ marginLeft: `${template.depth * 18}px` }}>
                          {template.description}
                        </p>
                      ) : null}
                      <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-slate-500" style={{ marginLeft: `${template.depth * 18}px` }}>
                        <span>Sort {template.sortOrder}</span>
                        <span>{template.stageGateRules.length} gate rule(s)</span>
                        {template.appliesToDeliveryStructure ? <span>{formatLabel(template.appliesToDeliveryStructure)}</span> : null}
                        {template.appliesToDevelopmentType ? <span>{template.appliesToDevelopmentType}</span> : null}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Button variant="outline" size="sm" onClick={() => beginTemplateEdit(template)}>
                        Edit
                      </Button>
                      <Button variant="ghost" size="icon" onClick={() => removeTemplate(template.id)}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                ))}
                {flattenedTemplates.length === 0 ? (
                  <div className="px-4 py-6 text-sm text-slate-600">No phase templates found for the selected scope.</div>
                ) : null}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>

        <Card className="border-slate-200/70 shadow-sm">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">{editingTemplateId ? 'Edit Phase Template' : 'Add Phase Template'}</CardTitle>
            <CardDescription>
              Templates drive what users see in project creation and in the project phases workspace.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="phase-template-name">Phase Name</Label>
                <Input
                  id="phase-template-name"
                  value={phaseTemplateForm.name ?? ''}
                  onChange={(event) => setPhaseTemplateForm((current) => ({ ...current, name: event.target.value }))}
                  placeholder="Detailed Design"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="phase-template-code">Code</Label>
                <Input
                  id="phase-template-code"
                  value={phaseTemplateForm.code ?? ''}
                  onChange={(event) => setPhaseTemplateForm((current) => ({ ...current, code: event.target.value }))}
                  placeholder="DETAILED_DESIGN"
                />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Project Type</Label>
                <Select
                  value={phaseTemplateForm.projectTypeId || GLOBAL_SCOPE}
                  onValueChange={(value) => setPhaseTemplateForm((current) => ({ ...current, projectTypeId: value === GLOBAL_SCOPE ? undefined : value }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={GLOBAL_SCOPE}>Global phase library</SelectItem>
                    {projectTypes.map((projectType) => (
                      <SelectItem key={projectType.id} value={projectType.id}>
                        {projectType.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Parent Phase</Label>
                <Select
                  value={phaseTemplateForm.parentPhaseTemplateId || NONE_VALUE}
                  onValueChange={(value) =>
                    setPhaseTemplateForm((current) => ({
                      ...current,
                      parentPhaseTemplateId: value === NONE_VALUE ? undefined : value,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE_VALUE}>Top-level phase</SelectItem>
                    {flattenedTemplates
                      .filter((template) => template.id !== editingTemplateId)
                      .map((template) => (
                        <SelectItem key={template.id} value={template.id}>
                          {'\u00A0'.repeat(template.depth * 2)}
                          {template.name}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Default Status</Label>
                <Select
                  value={phaseTemplateForm.defaultStatus || 'NotStarted'}
                  onValueChange={(value) => setPhaseTemplateForm((current) => ({ ...current, defaultStatus: value }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {PHASE_STATUS_OPTIONS.map((status) => (
                      <SelectItem key={status} value={status}>
                        {formatLabel(status)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="phase-template-sort-order">Sort Order</Label>
                <Input
                  id="phase-template-sort-order"
                  type="number"
                  min={0}
                  value={phaseTemplateForm.sortOrder ?? 0}
                  onChange={(event) =>
                    setPhaseTemplateForm((current) => ({
                      ...current,
                      sortOrder: Number.isFinite(event.target.valueAsNumber) ? event.target.valueAsNumber : 0,
                    }))
                  }
                />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Delivery Structure</Label>
                <Select
                  value={phaseTemplateForm.appliesToDeliveryStructure || NONE_VALUE}
                  onValueChange={(value) =>
                    setPhaseTemplateForm((current) => ({
                      ...current,
                      appliesToDeliveryStructure: value === NONE_VALUE ? undefined : value,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE_VALUE}>Any delivery structure</SelectItem>
                    {DELIVERY_STRUCTURES.map((structure) => (
                      <SelectItem key={structure} value={structure}>
                        {formatLabel(structure)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="phase-template-development-type">Development Type</Label>
                <Input
                  id="phase-template-development-type"
                  value={phaseTemplateForm.appliesToDevelopmentType ?? ''}
                  onChange={(event) => setPhaseTemplateForm((current) => ({ ...current, appliesToDevelopmentType: event.target.value }))}
                  placeholder="Residential"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="phase-template-description">Description</Label>
              <Textarea
                id="phase-template-description"
                rows={3}
                value={phaseTemplateForm.description ?? ''}
                onChange={(event) => setPhaseTemplateForm((current) => ({ ...current, description: event.target.value }))}
                placeholder="Describe what this phase covers and how users should apply it."
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm">
                <Switch
                  checked={phaseTemplateForm.isOptional ?? false}
                  onCheckedChange={(checked) => setPhaseTemplateForm((current) => ({ ...current, isOptional: checked }))}
                />
                <span>
                  <span className="block font-medium text-slate-900">Optional</span>
                  <span className="block text-slate-500">Allow users to skip it when not applicable.</span>
                </span>
              </label>
              <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm">
                <Switch
                  checked={phaseTemplateForm.isStageGateRequired ?? false}
                  onCheckedChange={(checked) => setPhaseTemplateForm((current) => ({ ...current, isStageGateRequired: checked }))}
                />
                <span>
                  <span className="block font-medium text-slate-900">Stage Gate</span>
                  <span className="block text-slate-500">Require gate checks before progression.</span>
                </span>
              </label>
              <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm">
                <Switch
                  checked={phaseTemplateForm.isActive ?? true}
                  onCheckedChange={(checked) => setPhaseTemplateForm((current) => ({ ...current, isActive: checked }))}
                />
                <span>
                  <span className="block font-medium text-slate-900">Active</span>
                  <span className="block text-slate-500">Hide inactive templates from new project setup.</span>
                </span>
              </label>
            </div>

            <div className="flex flex-wrap items-center gap-2 pt-2">
              <Button onClick={savePhaseTemplate} disabled={saving}>
                {editingTemplateId ? 'Save Template' : 'Add Template'}
              </Button>
              {editingTemplateId ? (
                <Button variant="outline" onClick={resetTemplateForm}>
                  Cancel
                </Button>
              ) : null}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.05fr,0.95fr]">
        <Card className="border-slate-200/70 shadow-sm">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Stage Gate Rules</CardTitle>
            <CardDescription>
              Define the readiness rules that determine when a phase can truly proceed.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {selectedTemplateId === FILTER_ALL ? (
              <div className="rounded-xl border border-dashed border-slate-300 bg-slate-50/70 px-4 py-4 text-sm text-slate-600">
                Pick a phase template above if you want to focus on one phase. The list below shows every configured rule in the current scope.
              </div>
            ) : null}
            <ScrollArea className="h-[360px] rounded-xl border border-slate-200">
              <div className="divide-y divide-slate-200">
                {stageGateRules.map((rule) => (
                  <div key={rule.id} className="flex flex-col gap-3 px-4 py-4 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-slate-900">{rule.name}</span>
                        <Badge variant="outline" className="border-slate-300 text-[11px] uppercase tracking-[0.16em]">
                          {rule.code}
                        </Badge>
                        <Badge variant="secondary" className="text-[11px]">
                          {formatLabel(rule.requirementType)}
                        </Badge>
                        <Badge variant="outline" className="border-slate-300 text-[11px]">
                          {formatLabel(rule.scope)}
                        </Badge>
                        {rule.isBlocking ? (
                          <Badge className="bg-rose-100 text-[11px] text-rose-900 hover:bg-rose-100">Blocking</Badge>
                        ) : (
                          <Badge className="bg-slate-100 text-[11px] text-slate-800 hover:bg-slate-100">Advisory</Badge>
                        )}
                      </div>
                      <div className="mt-2 text-xs text-slate-500">
                        {rule.projectPhaseTemplateName || 'Phase template'} | min {rule.minimumCount ?? '-'} | max {rule.maximumCount ?? '-'} | sort {rule.sortOrder}
                      </div>
                      {rule.description ? <p className="mt-2 text-sm text-slate-600">{rule.description}</p> : null}
                    </div>
                    <div className="flex items-center gap-2">
                      <Button variant="outline" size="sm" onClick={() => beginRuleEdit(rule)}>
                        Edit
                      </Button>
                      <Button variant="ghost" size="icon" onClick={() => removeRule(rule.id)}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                ))}
                {stageGateRules.length === 0 ? (
                  <div className="px-4 py-6 text-sm text-slate-600">No stage gate rules found for the selected scope.</div>
                ) : null}
              </div>
            </ScrollArea>
          </CardContent>
        </Card>

        <Card className="border-slate-200/70 shadow-sm">
          <CardHeader className="pb-3">
            <CardTitle className="text-base">{editingRuleId ? 'Edit Stage Gate Rule' : 'Add Stage Gate Rule'}</CardTitle>
            <CardDescription>
              Gate rules are what make phase progression enforceable instead of just informational.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label>Phase Template</Label>
              <Select
                value={stageGateRuleForm.projectPhaseTemplateId || NONE_VALUE}
                onValueChange={(value) =>
                  setStageGateRuleForm((current) => ({
                    ...current,
                    projectPhaseTemplateId: value === NONE_VALUE ? '' : value,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE_VALUE}>Select phase template</SelectItem>
                  {flattenedTemplates.map((template) => (
                    <SelectItem key={template.id} value={template.id}>
                      {'\u00A0'.repeat(template.depth * 2)}
                      {template.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="stage-gate-name">Rule Name</Label>
                <Input
                  id="stage-gate-name"
                  value={stageGateRuleForm.name ?? ''}
                  onChange={(event) => setStageGateRuleForm((current) => ({ ...current, name: event.target.value }))}
                  placeholder="Approved Statutory Approvals"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="stage-gate-code">Code</Label>
                <Input
                  id="stage-gate-code"
                  value={stageGateRuleForm.code ?? ''}
                  onChange={(event) => setStageGateRuleForm((current) => ({ ...current, code: event.target.value }))}
                  placeholder="APPROVED_PERMITS"
                />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Requirement Type</Label>
                <Select
                  value={stageGateRuleForm.requirementType}
                  onValueChange={(value) => setStageGateRuleForm((current) => ({ ...current, requirementType: value }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {REQUIREMENT_TYPES.map((requirementType) => (
                      <SelectItem key={requirementType} value={requirementType}>
                        {formatLabel(requirementType)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Scope</Label>
                <Select
                  value={stageGateRuleForm.scope || 'Project'}
                  onValueChange={(value) => setStageGateRuleForm((current) => ({ ...current, scope: value }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {GATE_SCOPES.map((scope) => (
                      <SelectItem key={scope} value={scope}>
                        {formatLabel(scope)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="stage-gate-minimum">Minimum Count</Label>
                <Input
                  id="stage-gate-minimum"
                  type="number"
                  min={0}
                  value={stageGateRuleForm.minimumCount ?? ''}
                  onChange={(event) =>
                    setStageGateRuleForm((current) => ({
                      ...current,
                      minimumCount: Number.isFinite(event.target.valueAsNumber) ? event.target.valueAsNumber : undefined,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="stage-gate-maximum">Maximum Count</Label>
                <Input
                  id="stage-gate-maximum"
                  type="number"
                  min={0}
                  value={stageGateRuleForm.maximumCount ?? ''}
                  onChange={(event) =>
                    setStageGateRuleForm((current) => ({
                      ...current,
                      maximumCount: Number.isFinite(event.target.valueAsNumber) ? event.target.valueAsNumber : undefined,
                    }))
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="stage-gate-sort-order">Sort Order</Label>
                <Input
                  id="stage-gate-sort-order"
                  type="number"
                  min={0}
                  value={stageGateRuleForm.sortOrder ?? 0}
                  onChange={(event) =>
                    setStageGateRuleForm((current) => ({
                      ...current,
                      sortOrder: Number.isFinite(event.target.valueAsNumber) ? event.target.valueAsNumber : 0,
                    }))
                  }
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="stage-gate-description">Description</Label>
              <Textarea
                id="stage-gate-description"
                rows={3}
                value={stageGateRuleForm.description ?? ''}
                onChange={(event) => setStageGateRuleForm((current) => ({ ...current, description: event.target.value }))}
                placeholder="Explain the readiness check in business terms."
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm">
                <Switch
                  checked={stageGateRuleForm.isBlocking ?? true}
                  onCheckedChange={(checked) => setStageGateRuleForm((current) => ({ ...current, isBlocking: checked }))}
                />
                <span>
                  <span className="block font-medium text-slate-900">Blocking Rule</span>
                  <span className="block text-slate-500">Fail the gate if this requirement is not satisfied.</span>
                </span>
              </label>
              <label className="flex items-start gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-4 py-3 text-sm">
                <Switch
                  checked={stageGateRuleForm.isActive ?? true}
                  onCheckedChange={(checked) => setStageGateRuleForm((current) => ({ ...current, isActive: checked }))}
                />
                <span>
                  <span className="block font-medium text-slate-900">Active Rule</span>
                  <span className="block text-slate-500">Inactive rules stay stored but do not apply to new projects.</span>
                </span>
              </label>
            </div>

            <div className="flex flex-wrap items-center gap-2 pt-2">
              <Button onClick={saveStageGateRule} disabled={saving || !stageGateRuleForm.projectPhaseTemplateId}>
                {editingRuleId ? 'Save Rule' : 'Add Rule'}
              </Button>
              {editingRuleId ? (
                <Button variant="outline" onClick={resetRuleForm}>
                  Cancel
                </Button>
              ) : null}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
