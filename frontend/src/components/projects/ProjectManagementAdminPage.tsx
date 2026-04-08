'use client';

import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { ProjectPhaseLibraryAdmin } from '@/components/projects/ProjectPhaseLibraryAdmin';
import {
  CreateProjectCatalogEntryDto,
  CreateProjectPriorityDto,
  CreateProjectTemplateDto,
  CreateProjectTypeDto,
  ProjectCatalogEntryDto,
  ProjectMasterDataOverviewDto,
  ProjectManagementSettingsDto,
  ProjectPriorityDto,
  ProjectTemplateDto,
  ProjectTypeDto,
  projectService,
  UpdateProjectManagementSettingsDto,
} from '@/services/projectService';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

type AdminTab = 'overview' | 'catalogs' | 'types' | 'priorities' | 'templates' | 'phases' | 'settings';

interface Props {
  initialTab?: AdminTab;
}

interface TemplateDevelopmentProfileBuilder {
  deliveryStructure: string;
  developmentType: string;
  siteName: string;
  siteAddress: string;
  landReference: string;
  procurementRoute: string;
  contractStrategy: string;
  consultantTeam: string;
  fundingArrangement: string;
  handoverStrategy: string;
  notes: string;
}

interface TemplatePhaseBuilder {
  id: string;
  code: string;
  name: string;
  description: string;
  status: string;
  isOptional: boolean;
  isStageGateRequired: boolean;
}

interface TemplateWorkItemBuilder {
  id: string;
  title: string;
  description: string;
  nodeType: string;
  status: string;
  priority: string;
}

interface TemplateMilestoneBuilder {
  id: string;
  title: string;
  description: string;
  targetDate: string;
  status: string;
  requiresApproval: boolean;
}

interface TemplateBuilderState {
  developmentProfile: TemplateDevelopmentProfileBuilder;
  phases: TemplatePhaseBuilder[];
  workItems: TemplateWorkItemBuilder[];
  milestones: TemplateMilestoneBuilder[];
  extraDefinition: Record<string, unknown>;
}

const DELIVERY_STRUCTURES = ['WholeDevelopment', 'SingleUnit', 'MultiUnit'];
const DEVELOPMENT_TYPES = ['Residential', 'Commercial', 'Industrial', 'MixedUse', 'Hospitality', 'Institutional', 'Infrastructure', 'Renovation'];
const PROCUREMENT_ROUTES = ['Traditional', 'DesignBuild', 'ConstructionManagement', 'DirectLabour', 'Negotiated', 'FrameworkCallOff'];
const CONTRACT_STRATEGIES = ['LumpSum', 'MeasuredWorks', 'CostPlus', 'TargetCost', 'ManagementContract', 'SubcontractPackages'];
const HANDOVER_STRATEGIES = ['SingleHandover', 'PhasedHandover', 'UnitByUnitHandover', 'ShellAndCore', 'Turnkey'];

const DEFAULT_TEMPLATE_PHASE_BLUEPRINTS = [
  { code: 'FEASIBILITY', name: 'Feasibility', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: true },
  { code: 'CONCEPT_DESIGN', name: 'Concept Design', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: true },
  { code: 'DETAILED_DESIGN', name: 'Detailed Design', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: true },
  { code: 'APPROVALS', name: 'Approvals & Permits', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: true },
  { code: 'PROCUREMENT', name: 'Procurement', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: true },
  { code: 'CONSTRUCTION', name: 'Construction', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: false },
  { code: 'COMMISSIONING', name: 'Testing & Commissioning', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: true },
  { code: 'HANDOVER', name: 'Handover', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: true },
  { code: 'DEFECTS_LIABILITY', name: 'Defects Liability', description: '', status: 'NotStarted', isOptional: false, isStageGateRequired: false },
];

const createTemplateRowId = () =>
  typeof globalThis.crypto?.randomUUID === 'function'
    ? globalThis.crypto.randomUUID()
    : Math.random().toString(36).slice(2, 10);

const formatCatalogLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

const asRecord = (value: unknown): Record<string, unknown> | null =>
  typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;

const readStringValue = (value: Record<string, unknown>, key: string) =>
  typeof value[key] === 'string' ? (value[key] as string) : '';

const readBooleanValue = (value: Record<string, unknown>, key: string) =>
  typeof value[key] === 'boolean' ? (value[key] as boolean) : false;

const normalizeDateInput = (value: string) => {
  const trimmed = value.trim();
  return trimmed.length >= 10 ? trimmed.slice(0, 10) : trimmed;
};

const createBlankTemplateBuilder = (): TemplateBuilderState => ({
  developmentProfile: {
    deliveryStructure: 'WholeDevelopment',
    developmentType: '',
    siteName: '',
    siteAddress: '',
    landReference: '',
    procurementRoute: '',
    contractStrategy: '',
    consultantTeam: '',
    fundingArrangement: '',
    handoverStrategy: '',
    notes: '',
  },
  phases: [],
  workItems: [],
  milestones: [],
  extraDefinition: {},
});

const createDefaultTemplateBuilder = (): TemplateBuilderState => ({
  ...createBlankTemplateBuilder(),
  phases: DEFAULT_TEMPLATE_PHASE_BLUEPRINTS.map((phase) => ({
    id: createTemplateRowId(),
    ...phase,
  })),
});

const cloneTemplateBuilder = (builder: TemplateBuilderState): TemplateBuilderState => ({
  developmentProfile: { ...builder.developmentProfile },
  phases: builder.phases.map((phase) => ({ ...phase })),
  workItems: builder.workItems.map((item) => ({ ...item })),
  milestones: builder.milestones.map((item) => ({ ...item })),
  extraDefinition: { ...builder.extraDefinition },
});

const parseTemplatePhases = (value: unknown): TemplatePhaseBuilder[] => {
  if (!Array.isArray(value)) return [];

  return value
    .map((item) => {
      if (typeof item === 'string') {
        return {
          id: createTemplateRowId(),
          code: '',
          name: item,
          description: '',
          status: 'NotStarted',
          isOptional: false,
          isStageGateRequired: false,
        };
      }

      const record = asRecord(item);
      if (!record) return null;

      return {
        id: createTemplateRowId(),
        code: readStringValue(record, 'code'),
        name: readStringValue(record, 'name') || readStringValue(record, 'title') || 'Unnamed phase',
        description: readStringValue(record, 'description'),
        status: readStringValue(record, 'status') || 'NotStarted',
        isOptional: readBooleanValue(record, 'isOptional'),
        isStageGateRequired: readBooleanValue(record, 'isStageGateRequired') || readBooleanValue(record, 'stageGateRequired'),
      };
    })
    .filter((item): item is TemplatePhaseBuilder => item !== null);
};

const parseTemplateWorkItems = (value: unknown): TemplateWorkItemBuilder[] => {
  if (!Array.isArray(value)) return [];

  return value
    .map((item) => {
      if (typeof item === 'string') {
        return {
          id: createTemplateRowId(),
          title: item,
          description: '',
          nodeType: 'Task',
          status: 'New',
          priority: 'Normal',
        };
      }

      const record = asRecord(item);
      if (!record) return null;

      return {
        id: createTemplateRowId(),
        title: readStringValue(record, 'title') || 'Untitled work item',
        description: readStringValue(record, 'description'),
        nodeType: readStringValue(record, 'nodeType') || 'Task',
        status: readStringValue(record, 'status') || 'New',
        priority: readStringValue(record, 'priority') || 'Normal',
      };
    })
    .filter((item): item is TemplateWorkItemBuilder => item !== null);
};

const parseTemplateMilestones = (value: unknown): TemplateMilestoneBuilder[] => {
  if (!Array.isArray(value)) return [];

  return value
    .map((item) => {
      if (typeof item === 'string') {
        return {
          id: createTemplateRowId(),
          title: item,
          description: '',
          targetDate: '',
          status: 'Draft',
          requiresApproval: false,
        };
      }

      const record = asRecord(item);
      if (!record) return null;

      return {
        id: createTemplateRowId(),
        title: readStringValue(record, 'title') || 'Untitled milestone',
        description: readStringValue(record, 'description'),
        targetDate: normalizeDateInput(readStringValue(record, 'targetDate')),
        status: readStringValue(record, 'status') || 'Draft',
        requiresApproval: readBooleanValue(record, 'requiresApproval'),
      };
    })
    .filter((item): item is TemplateMilestoneBuilder => item !== null);
};

const parseTemplateDefinition = (templateDefinitionJson?: string, useDefaultWhenEmpty: boolean = false) => {
  const fallbackBuilder = useDefaultWhenEmpty ? createDefaultTemplateBuilder() : createBlankTemplateBuilder();
  if (!templateDefinitionJson?.trim()) {
    return { builder: fallbackBuilder, error: null as string | null };
  }

  try {
    const parsed = JSON.parse(templateDefinitionJson);
    const root = asRecord(parsed);
    if (!root) {
      return { builder: fallbackBuilder, error: 'Template definition must be a JSON object.' };
    }

    const { developmentProfile, constructionProfile, projectPhases, phases, workItems, milestones, ...extraDefinition } = root;
    const profile = asRecord(developmentProfile) ?? asRecord(constructionProfile) ?? {};
    const builder: TemplateBuilderState = {
      developmentProfile: {
        deliveryStructure: readStringValue(profile, 'deliveryStructure') || 'WholeDevelopment',
        developmentType: readStringValue(profile, 'developmentType'),
        siteName: readStringValue(profile, 'siteName'),
        siteAddress: readStringValue(profile, 'siteAddress'),
        landReference: readStringValue(profile, 'landReference'),
        procurementRoute: readStringValue(profile, 'procurementRoute'),
        contractStrategy: readStringValue(profile, 'contractStrategy'),
        consultantTeam: readStringValue(profile, 'consultantTeam'),
        fundingArrangement: readStringValue(profile, 'fundingArrangement'),
        handoverStrategy: readStringValue(profile, 'handoverStrategy'),
        notes: readStringValue(profile, 'notes'),
      },
      phases: parseTemplatePhases(projectPhases ?? phases),
      workItems: parseTemplateWorkItems(workItems),
      milestones: parseTemplateMilestones(milestones),
      extraDefinition,
    };

    if (useDefaultWhenEmpty && builder.phases.length === 0) {
      builder.phases = createDefaultTemplateBuilder().phases;
    }

    return { builder, error: null as string | null };
  } catch {
    return { builder: fallbackBuilder, error: 'Template JSON is invalid. Use Advanced mode to repair it before saving.' };
  }
};

const buildTemplateDefinitionJson = (builder: TemplateBuilderState) => {
  const payload: Record<string, unknown> = { ...builder.extraDefinition };
  const {
    deliveryStructure,
    developmentType,
    siteName,
    siteAddress,
    landReference,
    procurementRoute,
    contractStrategy,
    consultantTeam,
    fundingArrangement,
    handoverStrategy,
    notes,
  } = builder.developmentProfile;

  const developmentProfile = Object.fromEntries(
    Object.entries({
      deliveryStructure: deliveryStructure || undefined,
      developmentType: developmentType.trim() || undefined,
      siteName: siteName.trim() || undefined,
      siteAddress: siteAddress.trim() || undefined,
      landReference: landReference.trim() || undefined,
      procurementRoute: procurementRoute.trim() || undefined,
      contractStrategy: contractStrategy.trim() || undefined,
      consultantTeam: consultantTeam.trim() || undefined,
      fundingArrangement: fundingArrangement.trim() || undefined,
      handoverStrategy: handoverStrategy.trim() || undefined,
      notes: notes.trim() || undefined,
    }).filter(([, value]) => value !== undefined),
  );

  if (Object.keys(developmentProfile).length > 0) {
    payload.developmentProfile = developmentProfile;
  }

  const phases = builder.phases
    .map((phase, index) => ({
      code: phase.code.trim() || undefined,
      name: phase.name.trim(),
      description: phase.description.trim() || undefined,
      status: phase.status.trim() || 'NotStarted',
      sortOrder: index,
      isOptional: phase.isOptional,
      isStageGateRequired: phase.isStageGateRequired,
    }))
    .filter((phase) => phase.name);

  if (phases.length > 0) {
    payload.projectPhases = phases;
  }

  const workItems = builder.workItems
    .map((item, index) => ({
      title: item.title.trim(),
      description: item.description.trim() || undefined,
      nodeType: item.nodeType.trim() || 'Task',
      status: item.status.trim() || 'New',
      priority: item.priority.trim() || 'Normal',
      sortOrder: index,
    }))
    .filter((item) => item.title);

  if (workItems.length > 0) {
    payload.workItems = workItems;
  }

  const milestones = builder.milestones
    .map((milestone) => ({
      title: milestone.title.trim(),
      description: milestone.description.trim() || undefined,
      targetDate: milestone.targetDate || undefined,
      status: milestone.status.trim() || 'Draft',
      requiresApproval: milestone.requiresApproval,
    }))
    .filter((milestone) => milestone.title);

  if (milestones.length > 0) {
    payload.milestones = milestones;
  }

  return JSON.stringify(payload, null, 2);
};

const emptyType: CreateProjectTypeDto = { code: '', name: '', description: '', isActive: true, requiresSponsor: false, requiresApproval: true, mandatoryFieldsJson: '' };
const emptyPriority: CreateProjectPriorityDto = { code: '', name: '', colorHex: '#2563eb', sortOrder: 10, isActive: true };
const emptyTemplate: CreateProjectTemplateDto = {
  code: '',
  name: '',
  description: '',
  versionLabel: '1.0',
  templateDefinitionJson: buildTemplateDefinitionJson(createDefaultTemplateBuilder()),
  isActive: true,
};
const emptySettings: UpdateProjectManagementSettingsDto = {
  projectNumberFormat: 'PRJ-{YYYY}-{SEQ:0000}',
  requireSponsor: false,
  defaultApprovalRequired: true,
  mandatoryFieldsByTypeJson: '',
  notes: '',
};
const emptyCatalog: CreateProjectCatalogEntryDto = {
  catalogType: 'methodologies',
  code: '',
  name: '',
  description: '',
  sortOrder: 10,
  isActive: true,
};

export default function ProjectManagementAdminPage({ initialTab = 'overview' }: Props) {
  const [tab, setTab] = useState<AdminTab>(initialTab);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [types, setTypes] = useState<ProjectTypeDto[]>([]);
  const [priorities, setPriorities] = useState<ProjectPriorityDto[]>([]);
  const [templates, setTemplates] = useState<ProjectTemplateDto[]>([]);
  const [masterDataOverview, setMasterDataOverview] = useState<ProjectMasterDataOverviewDto | null>(null);
  const [catalogEntries, setCatalogEntries] = useState<ProjectCatalogEntryDto[]>([]);
  const [settings, setSettings] = useState<ProjectManagementSettingsDto | null>(null);
  const [typeForm, setTypeForm] = useState<CreateProjectTypeDto>(emptyType);
  const [priorityForm, setPriorityForm] = useState<CreateProjectPriorityDto>(emptyPriority);
  const [templateForm, setTemplateForm] = useState<CreateProjectTemplateDto>(emptyTemplate);
  const [templateBuilder, setTemplateBuilder] = useState<TemplateBuilderState>(() =>
    cloneTemplateBuilder(parseTemplateDefinition(emptyTemplate.templateDefinitionJson, true).builder),
  );
  const [templateJsonError, setTemplateJsonError] = useState<string | null>(null);
  const [showAdvancedTemplateJson, setShowAdvancedTemplateJson] = useState(false);
  const [catalogForm, setCatalogForm] = useState<CreateProjectCatalogEntryDto>(emptyCatalog);
  const [settingsForm, setSettingsForm] = useState<UpdateProjectManagementSettingsDto>(emptySettings);
  const [editingTypeId, setEditingTypeId] = useState<string | null>(null);
  const [editingPriorityId, setEditingPriorityId] = useState<string | null>(null);
  const [editingTemplateId, setEditingTemplateId] = useState<string | null>(null);
  const [editingCatalogId, setEditingCatalogId] = useState<string | null>(null);
  const [selectedCatalogType, setSelectedCatalogType] = useState<string>('methodologies');
  const [seedingCatalogs, setSeedingCatalogs] = useState(false);

  const resetTemplateEditor = (nextForm: CreateProjectTemplateDto = emptyTemplate, useDefaultWhenEmpty: boolean = true) => {
    const parsed = parseTemplateDefinition(nextForm.templateDefinitionJson, useDefaultWhenEmpty);
    setTemplateForm(nextForm);
    setTemplateBuilder(cloneTemplateBuilder(parsed.builder));
    setTemplateJsonError(parsed.error);
    setShowAdvancedTemplateJson(false);
  };

  const updateTemplateBuilder = (updater: (current: TemplateBuilderState) => TemplateBuilderState) => {
    setTemplateBuilder((current) => {
      const next = updater(current);
      const templateDefinitionJson = buildTemplateDefinitionJson(next);
      setTemplateForm((prev) => ({
        ...prev,
        templateDefinitionJson,
      }));
      setTemplateJsonError(null);
      return next;
    });
  };

  const updateTemplateDevelopmentProfile = (updates: Partial<TemplateDevelopmentProfileBuilder>) => {
    updateTemplateBuilder((current) => ({
      ...current,
      developmentProfile: {
        ...current.developmentProfile,
        ...updates,
      },
    }));
  };

  const updateAdvancedTemplateJson = (value: string) => {
    setTemplateForm((prev) => ({
      ...prev,
      templateDefinitionJson: value,
    }));

    const parsed = parseTemplateDefinition(value, false);
    if (parsed.error) {
      setTemplateJsonError(parsed.error);
      return;
    }

    setTemplateBuilder(cloneTemplateBuilder(parsed.builder));
    setTemplateJsonError(null);
  };

  const loadData = async () => {
    try {
      setLoading(true);
      const [loadedOverview, loadedTypes, loadedPriorities, loadedTemplates, loadedSettings] = await Promise.all([
        projectService.getMasterDataOverview(),
        projectService.getProjectTypes(),
        projectService.getProjectPriorities(),
        projectService.getProjectTemplates(),
        projectService.getSettings(),
      ]);
      setMasterDataOverview(loadedOverview);
      const nextCatalogType = loadedOverview.recommendedCatalogs[0]?.key || selectedCatalogType;
      setSelectedCatalogType(nextCatalogType);
      setCatalogForm((prev) => ({ ...prev, catalogType: nextCatalogType }));
      setTypes(loadedTypes);
      setPriorities(loadedPriorities);
      setTemplates(loadedTemplates);
      setSettings(loadedSettings);
      setSettingsForm({
        projectNumberFormat: loadedSettings.projectNumberFormat,
        requireSponsor: loadedSettings.requireSponsor,
        defaultApprovalRequired: loadedSettings.defaultApprovalRequired,
        defaultProjectTypeId: loadedSettings.defaultProjectTypeId,
        defaultProjectPriorityId: loadedSettings.defaultProjectPriorityId,
        defaultTemplateId: loadedSettings.defaultTemplateId,
        mandatoryFieldsByTypeJson: loadedSettings.mandatoryFieldsByTypeJson || '',
        notes: loadedSettings.notes || '',
      });
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project administration data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const loadCatalogEntries = async (catalogType: string) => {
    try {
      setCatalogEntries(await projectService.getCatalogEntries(catalogType));
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project catalog entries');
    }
  };

  useEffect(() => {
    if (!selectedCatalogType) return;
    setCatalogForm((prev) => ({ ...prev, catalogType: selectedCatalogType }));
    loadCatalogEntries(selectedCatalogType);
  }, [selectedCatalogType]);

  const saveType = async () => {
    try {
      setSaving(true);
      if (editingTypeId) {
        await projectService.updateProjectType(editingTypeId, typeForm);
      } else {
        await projectService.createProjectType(typeForm);
      }
      setTypeForm(emptyType);
      setEditingTypeId(null);
      await loadData();
      toast.success('Project type saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project type');
    } finally {
      setSaving(false);
    }
  };

  const savePriority = async () => {
    try {
      setSaving(true);
      if (editingPriorityId) {
        await projectService.updateProjectPriority(editingPriorityId, priorityForm);
      } else {
        await projectService.createProjectPriority(priorityForm);
      }
      setPriorityForm(emptyPriority);
      setEditingPriorityId(null);
      await loadData();
      toast.success('Project priority saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project priority');
    } finally {
      setSaving(false);
    }
  };

  const saveTemplate = async () => {
    try {
      const parsed = parseTemplateDefinition(templateForm.templateDefinitionJson, false);
      if (parsed.error) {
        toast.error(parsed.error);
        return;
      }

      setSaving(true);
      const payload = {
        ...templateForm,
        templateDefinitionJson: templateForm.templateDefinitionJson?.trim()
          ? templateForm.templateDefinitionJson
          : buildTemplateDefinitionJson(templateBuilder),
      };
      if (editingTemplateId) {
        await projectService.updateProjectTemplate(editingTemplateId, payload);
      } else {
        await projectService.createProjectTemplate(payload);
      }
      resetTemplateEditor();
      setEditingTemplateId(null);
      await loadData();
      toast.success('Project template saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project template');
    } finally {
      setSaving(false);
    }
  };

  const saveSettings = async () => {
    try {
      setSaving(true);
      const updated = await projectService.updateSettings(settingsForm);
      setSettings(updated);
      toast.success('Project settings updated');
    } catch (error: any) {
      toast.error(error.message || 'Failed to update project settings');
    } finally {
      setSaving(false);
    }
  };

  const saveCatalogEntry = async () => {
    try {
      setSaving(true);
      if (editingCatalogId) {
        await projectService.updateCatalogEntry(editingCatalogId, catalogForm);
      } else {
        await projectService.createCatalogEntry(catalogForm);
      }
      setCatalogForm({ ...emptyCatalog, catalogType: selectedCatalogType });
      setEditingCatalogId(null);
      await Promise.all([loadCatalogEntries(selectedCatalogType), loadData()]);
      toast.success('Project catalog entry saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project catalog entry');
    } finally {
      setSaving(false);
    }
  };

  const removeType = async (id: string) => {
    if (!window.confirm('Delete this project type?')) return;
    try {
      await projectService.deleteProjectType(id);
      if (editingTypeId === id) {
        setEditingTypeId(null);
        setTypeForm(emptyType);
      }
      await loadData();
      toast.success('Project type deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project type');
    }
  };

  const removePriority = async (id: string) => {
    if (!window.confirm('Delete this project priority?')) return;
    try {
      await projectService.deleteProjectPriority(id);
      if (editingPriorityId === id) {
        setEditingPriorityId(null);
        setPriorityForm(emptyPriority);
      }
      await loadData();
      toast.success('Project priority deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project priority');
    }
  };

  const removeTemplate = async (id: string) => {
    if (!window.confirm('Delete this project template?')) return;
    try {
      await projectService.deleteProjectTemplate(id);
      if (editingTemplateId === id) {
        setEditingTemplateId(null);
        resetTemplateEditor();
      }
      await loadData();
      toast.success('Project template deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project template');
    }
  };

  const removeCatalogEntry = async (id: string) => {
    if (!window.confirm('Delete this project catalog entry?')) return;
    try {
      await projectService.deleteCatalogEntry(id);
      if (editingCatalogId === id) {
        setEditingCatalogId(null);
        setCatalogForm({ ...emptyCatalog, catalogType: selectedCatalogType });
      }
      await Promise.all([loadCatalogEntries(selectedCatalogType), loadData()]);
      toast.success('Project catalog entry deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project catalog entry');
    }
  };

  const seedCatalogDefaults = async () => {
    try {
      setSeedingCatalogs(true);
      await projectService.seedCatalogDefaults(selectedCatalogType);
      await Promise.all([loadCatalogEntries(selectedCatalogType), loadData()]);
      toast.success('Recommended defaults seeded');
    } catch (error: any) {
      toast.error(error.message || 'Failed to seed recommended defaults');
    } finally {
      setSeedingCatalogs(false);
    }
  };

  if (loading) {
    return <div className="py-20 text-center text-muted-foreground">Loading project management setup...</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Project Management Setup</h1>
          <p className="text-muted-foreground">Configure defaults, templates, catalogs, priorities, and project types.</p>
        </div>
        <Button onClick={saveSettings} disabled={saving}>
          Save Settings
        </Button>
      </div>

      <Tabs value={tab} onValueChange={(value) => setTab(value as AdminTab)} className="space-y-6">
        <TabsList className="grid w-full grid-cols-7">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="catalogs">Catalogs</TabsTrigger>
          <TabsTrigger value="types">Types</TabsTrigger>
          <TabsTrigger value="priorities">Priorities</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
          <TabsTrigger value="phases">Phases</TabsTrigger>
          <TabsTrigger value="settings">Settings</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-8">
            <Card><CardHeader><CardTitle className="text-base">Types</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectTypeCount ?? types.length}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Priorities</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectPriorityCount ?? priorities.length}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Templates</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectTemplateCount ?? templates.length}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Phase Templates</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectPhaseTemplateCount ?? 0}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Gate Rules</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectStageGateRuleCount ?? 0}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Portfolios</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.portfolioCount ?? 0}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Programs</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.programCount ?? 0}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Approval Default</CardTitle></CardHeader><CardContent><Badge>{settings?.defaultApprovalRequired ? 'Required' : 'Optional'}</Badge></CardContent></Card>
          </div>
          <Card>
            <CardHeader>
              <CardTitle>Project Setup Catalogs</CardTitle>
              <CardDescription>Manage tenant-specific project setup catalogs.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 lg:grid-cols-2">
              {(masterDataOverview?.recommendedCatalogs || []).map((group) => (
                <div key={group.key} className="rounded-lg border p-4">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <div className="font-semibold">{group.displayName}</div>
                      <div className="text-sm text-muted-foreground">
                        {masterDataOverview?.catalogCoverage.find((item) => item.catalogType === group.key)?.configuredCount ?? 0} configured of {group.items.length} recommended.
                      </div>
                    </div>
                    <Badge variant="outline">{group.items.length} items</Badge>
                  </div>
                  <div className="mt-4 flex flex-wrap gap-2">
                    {group.items.map((item) => (
                      <Badge key={`${group.key}-${item.code}`} variant="secondary">
                        {item.name}
                      </Badge>
                    ))}
                  </div>
                </div>
              ))}
              {!(masterDataOverview?.recommendedCatalogs?.length) ? (
                <div className="text-sm text-muted-foreground">No catalog groups are available yet.</div>
              ) : null}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Setup Coverage</CardTitle>
              <CardDescription>Review configured setup records and available catalog groups.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="rounded-lg border p-4">
                <div className="font-medium">Operational setup</div>
                <div className="mt-2 text-sm text-muted-foreground">Types, priorities, templates, numbering rules, portfolios, and programs are available as active setup records.</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="font-medium">Additional setup areas</div>
                <div className="mt-2 text-sm text-muted-foreground">Methodologies, lifecycle statuses, risk ratings, cost categories, expense categories, billing types, and resource roles can be maintained here as tenant-specific records.</div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="catalogs">
          <Card>
            <CardHeader>
              <CardTitle>Project Catalogs</CardTitle>
              <CardDescription>Manage tenant-specific methodologies, lifecycle statuses, stages, billing types, cost categories, and related project setup catalogs.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-4">
                <div className="flex flex-col gap-4 md:flex-row md:items-end">
                  <div className="grid gap-2 md:flex-1">
                    <Label>Catalog Type</Label>
                    <Select value={selectedCatalogType} onValueChange={setSelectedCatalogType}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {(masterDataOverview?.recommendedCatalogs || []).map((group) => (
                          <SelectItem key={group.key} value={group.key}>{group.displayName}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <Button variant="outline" onClick={seedCatalogDefaults} disabled={seedingCatalogs}>
                    Seed Recommended
                  </Button>
                </div>
                <div className="space-y-3">
                  {catalogEntries.length === 0 ? <div className="text-sm text-muted-foreground">No entries exist yet for this catalog type.</div> : null}
                  {catalogEntries.map((item) => (
                    <div key={item.id} className="rounded-lg border p-4">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-semibold">{item.name}</span>
                            <Badge variant="outline">{item.code}</Badge>
                            <Badge variant={item.isActive ? 'secondary' : 'outline'}>{item.isActive ? 'Active' : 'Inactive'}</Badge>
                          </div>
                          <div className="text-sm text-muted-foreground">Sort order {item.sortOrder}</div>
                          {item.description ? <div className="mt-2 text-sm text-muted-foreground">{item.description}</div> : null}
                        </div>
                        <div className="flex gap-2">
                          <Button variant="outline" size="sm" onClick={() => {
                            setEditingCatalogId(item.id);
                            setCatalogForm({
                              catalogType: item.catalogType,
                              code: item.code,
                              name: item.name,
                              description: item.description || '',
                              sortOrder: item.sortOrder,
                              isActive: item.isActive,
                            });
                          }}>Edit</Button>
                          <Button variant="ghost" size="icon" onClick={() => removeCatalogEntry(item.id)}><Trash2 className="h-4 w-4" /></Button>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
              <div className="rounded-lg border p-4 space-y-4">
                <div className="font-semibold">{editingCatalogId ? 'Edit Catalog Entry' : 'New Catalog Entry'}</div>
                <div className="grid gap-2">
                  <Label>Catalog Type</Label>
                  <Select value={catalogForm.catalogType} onValueChange={(value) => { setSelectedCatalogType(value); setCatalogForm((prev) => ({ ...prev, catalogType: value })); }}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {(masterDataOverview?.recommendedCatalogs || []).map((group) => (
                        <SelectItem key={group.key} value={group.key}>{group.displayName}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Code</Label><Input value={catalogForm.code} onChange={(e) => setCatalogForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Name</Label><Input value={catalogForm.name} onChange={(e) => setCatalogForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Description</Label><Textarea rows={3} value={catalogForm.description || ''} onChange={(e) => setCatalogForm((prev) => ({ ...prev, description: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Sort Order</Label><Input type="number" value={catalogForm.sortOrder ?? 0} onChange={(e) => setCatalogForm((prev) => ({ ...prev, sortOrder: Number(e.target.value || '0') }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={catalogForm.isActive !== false} onCheckedChange={(checked) => setCatalogForm((prev) => ({ ...prev, isActive: checked }))} /></div>
                <div className="flex gap-2">
                  <Button onClick={saveCatalogEntry} disabled={saving || !catalogForm.catalogType || !catalogForm.code || !catalogForm.name}>Save Entry</Button>
                  {editingCatalogId && <Button variant="outline" onClick={() => { setEditingCatalogId(null); setCatalogForm({ ...emptyCatalog, catalogType: selectedCatalogType }); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="types">
          <Card>
            <CardHeader>
              <CardTitle>Project Types</CardTitle>
              <CardDescription>Lifecycle and governance behavior by project type.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-3">
                {types.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-semibold">{item.name}</span>
                          <Badge variant="outline">{item.code}</Badge>
                        </div>
                        <div className="text-sm text-muted-foreground">{item.description || 'No description'}</div>
                      </div>
                      <div className="flex gap-2">
                        <Button variant="outline" size="sm" onClick={() => {
                          setEditingTypeId(item.id);
                          setTypeForm({ code: item.code, name: item.name, description: item.description || '', isActive: item.isActive, requiresSponsor: item.requiresSponsor, requiresApproval: item.requiresApproval, mandatoryFieldsJson: item.mandatoryFieldsJson || '' });
                        }}>Edit</Button>
                        <Button variant="ghost" size="icon" onClick={() => removeType(item.id)}><Trash2 className="h-4 w-4" /></Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
              <div className="rounded-lg border p-4 space-y-4">
                <div className="font-semibold">{editingTypeId ? 'Edit Type' : 'New Type'}</div>
                <div className="grid gap-2"><Label>Code</Label><Input value={typeForm.code} onChange={(e) => setTypeForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Name</Label><Input value={typeForm.name} onChange={(e) => setTypeForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Description</Label><Textarea rows={3} value={typeForm.description} onChange={(e) => setTypeForm((prev) => ({ ...prev, description: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Mandatory Fields JSON</Label><Textarea rows={5} value={typeForm.mandatoryFieldsJson} onChange={(e) => setTypeForm((prev) => ({ ...prev, mandatoryFieldsJson: e.target.value }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Require Sponsor</span><Switch checked={!!typeForm.requiresSponsor} onCheckedChange={(checked) => setTypeForm((prev) => ({ ...prev, requiresSponsor: checked }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Require Approval</span><Switch checked={typeForm.requiresApproval !== false} onCheckedChange={(checked) => setTypeForm((prev) => ({ ...prev, requiresApproval: checked }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={typeForm.isActive !== false} onCheckedChange={(checked) => setTypeForm((prev) => ({ ...prev, isActive: checked }))} /></div>
                <div className="flex gap-2">
                  <Button onClick={saveType} disabled={saving || !typeForm.code || !typeForm.name}>Save Type</Button>
                  {editingTypeId && <Button variant="outline" onClick={() => { setEditingTypeId(null); setTypeForm(emptyType); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="priorities">
          <Card>
            <CardHeader>
              <CardTitle>Project Priorities</CardTitle>
              <CardDescription>Default urgency levels used in project and task planning.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-3">
                {priorities.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4 flex items-center justify-between">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold">{item.name}</span>
                        <Badge variant="outline">{item.code}</Badge>
                        <span className="h-3 w-3 rounded-full border" style={{ backgroundColor: item.colorHex || '#64748b' }} />
                      </div>
                      <div className="text-sm text-muted-foreground">Sort order {item.sortOrder}</div>
                    </div>
                    <div className="flex gap-2">
                      <Button variant="outline" size="sm" onClick={() => {
                        setEditingPriorityId(item.id);
                        setPriorityForm({ code: item.code, name: item.name, colorHex: item.colorHex || '#2563eb', sortOrder: item.sortOrder, isActive: item.isActive });
                      }}>Edit</Button>
                      <Button variant="ghost" size="icon" onClick={() => removePriority(item.id)}><Trash2 className="h-4 w-4" /></Button>
                    </div>
                  </div>
                ))}
              </div>
              <div className="rounded-lg border p-4 space-y-4">
                <div className="font-semibold">{editingPriorityId ? 'Edit Priority' : 'New Priority'}</div>
                <div className="grid gap-2"><Label>Code</Label><Input value={priorityForm.code} onChange={(e) => setPriorityForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Name</Label><Input value={priorityForm.name} onChange={(e) => setPriorityForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Color</Label><Input value={priorityForm.colorHex} onChange={(e) => setPriorityForm((prev) => ({ ...prev, colorHex: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Sort Order</Label><Input type="number" value={priorityForm.sortOrder} onChange={(e) => setPriorityForm((prev) => ({ ...prev, sortOrder: Number(e.target.value || '0') }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={priorityForm.isActive !== false} onCheckedChange={(checked) => setPriorityForm((prev) => ({ ...prev, isActive: checked }))} /></div>
                <div className="flex gap-2">
                  <Button onClick={savePriority} disabled={saving || !priorityForm.code || !priorityForm.name}>Save Priority</Button>
                  {editingPriorityId && <Button variant="outline" onClick={() => { setEditingPriorityId(null); setPriorityForm(emptyPriority); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="templates">
          <Card>
            <CardHeader>
              <CardTitle>Project Templates</CardTitle>
              <CardDescription>
                Build reusable project blueprints with construction profile defaults, lifecycle phases, work items, and milestones.
                Raw JSON is still available for advanced administrators, but normal setup no longer requires hand-editing JSON.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-3">
                {templates.map((item) => {
                  const parsed = parseTemplateDefinition(item.templateDefinitionJson, false);
                  const summary = parsed.builder;

                  return (
                    <div key={item.id} className="rounded-lg border p-4">
                      <div className="flex items-start justify-between gap-4">
                        <div className="space-y-3">
                          <div>
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="font-semibold">{item.name}</span>
                              <Badge variant="outline">{item.code}</Badge>
                              <Badge variant="secondary">{item.versionLabel}</Badge>
                              {item.projectTypeName ? <Badge variant="outline">{item.projectTypeName}</Badge> : null}
                            </div>
                            <div className="text-sm text-muted-foreground">{item.description || 'No description'}</div>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            <Badge variant="secondary">{summary.phases.length} phases</Badge>
                            <Badge variant="secondary">{summary.workItems.length} work items</Badge>
                            <Badge variant="secondary">{summary.milestones.length} milestones</Badge>
                            {summary.developmentProfile.developmentType ? (
                              <Badge variant="outline">{formatCatalogLabel(summary.developmentProfile.developmentType)}</Badge>
                            ) : null}
                            <Badge variant={item.isActive ? 'secondary' : 'outline'}>
                              {item.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                          </div>
                          {parsed.error ? <div className="text-sm text-amber-600">{parsed.error}</div> : null}
                        </div>
                        <div className="flex gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                              setEditingTemplateId(item.id);
                              resetTemplateEditor(
                                {
                                  code: item.code,
                                  name: item.name,
                                  description: item.description || '',
                                  projectTypeId: item.projectTypeId,
                                  versionLabel: item.versionLabel,
                                  templateDefinitionJson: item.templateDefinitionJson || '',
                                  isActive: item.isActive,
                                },
                                !item.templateDefinitionJson?.trim(),
                              );
                            }}
                          >
                            Edit
                          </Button>
                          <Button variant="ghost" size="icon" onClick={() => removeTemplate(item.id)}><Trash2 className="h-4 w-4" /></Button>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
              <div className="rounded-lg border p-4 space-y-6">
                <div>
                  <div className="font-semibold">{editingTemplateId ? 'Edit Template' : 'New Template'}</div>
                  <div className="text-sm text-muted-foreground">
                    Set up a business-friendly template here. The system will still store the final template as JSON behind the scenes.
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div className="grid gap-2"><Label>Code</Label><Input value={templateForm.code} onChange={(e) => setTemplateForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                  <div className="grid gap-2"><Label>Name</Label><Input value={templateForm.name} onChange={(e) => setTemplateForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                  <div className="grid gap-2">
                    <Label>Project Type</Label>
                    <Select value={templateForm.projectTypeId || 'none'} onValueChange={(value) => setTemplateForm((prev) => ({ ...prev, projectTypeId: value === 'none' ? undefined : value }))}>
                      <SelectTrigger><SelectValue placeholder="General template" /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">General template</SelectItem>
                        {types.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid gap-2"><Label>Version</Label><Input value={templateForm.versionLabel} onChange={(e) => setTemplateForm((prev) => ({ ...prev, versionLabel: e.target.value }))} /></div>
                </div>

                <div className="grid gap-2"><Label>Description</Label><Textarea rows={3} value={templateForm.description} onChange={(e) => setTemplateForm((prev) => ({ ...prev, description: e.target.value }))} /></div>

                <div className="rounded-lg border p-4">
                  <div className="mb-4">
                    <div className="font-medium">Construction Profile Defaults</div>
                    <div className="text-sm text-muted-foreground">These defaults will be applied when a project is created from this template.</div>
                  </div>
                  <div className="grid gap-4 md:grid-cols-2">
                    <div className="grid gap-2">
                      <Label>Delivery Structure</Label>
                      <Select value={templateBuilder.developmentProfile.deliveryStructure || 'WholeDevelopment'} onValueChange={(value) => updateTemplateDevelopmentProfile({ deliveryStructure: value })}>
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {DELIVERY_STRUCTURES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid gap-2">
                      <Label>Development Type</Label>
                      <Select value={templateBuilder.developmentProfile.developmentType || 'none'} onValueChange={(value) => updateTemplateDevelopmentProfile({ developmentType: value === 'none' ? '' : value })}>
                        <SelectTrigger><SelectValue placeholder="No development type" /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">No development type</SelectItem>
                          {DEVELOPMENT_TYPES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid gap-2"><Label>Site Name</Label><Input value={templateBuilder.developmentProfile.siteName} onChange={(e) => updateTemplateDevelopmentProfile({ siteName: e.target.value })} /></div>
                    <div className="grid gap-2"><Label>Land Reference</Label><Input value={templateBuilder.developmentProfile.landReference} onChange={(e) => updateTemplateDevelopmentProfile({ landReference: e.target.value })} /></div>
                    <div className="grid gap-2">
                      <Label>Procurement Route</Label>
                      <Select value={templateBuilder.developmentProfile.procurementRoute || 'none'} onValueChange={(value) => updateTemplateDevelopmentProfile({ procurementRoute: value === 'none' ? '' : value })}>
                        <SelectTrigger><SelectValue placeholder="No procurement route" /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">No procurement route</SelectItem>
                          {PROCUREMENT_ROUTES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid gap-2">
                      <Label>Contract Strategy</Label>
                      <Select value={templateBuilder.developmentProfile.contractStrategy || 'none'} onValueChange={(value) => updateTemplateDevelopmentProfile({ contractStrategy: value === 'none' ? '' : value })}>
                        <SelectTrigger><SelectValue placeholder="No contract strategy" /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">No contract strategy</SelectItem>
                          {CONTRACT_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid gap-2">
                      <Label>Handover Strategy</Label>
                      <Select value={templateBuilder.developmentProfile.handoverStrategy || 'none'} onValueChange={(value) => updateTemplateDevelopmentProfile({ handoverStrategy: value === 'none' ? '' : value })}>
                        <SelectTrigger><SelectValue placeholder="No handover strategy" /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">No handover strategy</SelectItem>
                          {HANDOVER_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid gap-2"><Label>Consultant Team</Label><Input value={templateBuilder.developmentProfile.consultantTeam} onChange={(e) => updateTemplateDevelopmentProfile({ consultantTeam: e.target.value })} /></div>
                    <div className="grid gap-2"><Label>Funding Arrangement</Label><Input value={templateBuilder.developmentProfile.fundingArrangement} onChange={(e) => updateTemplateDevelopmentProfile({ fundingArrangement: e.target.value })} /></div>
                    <div className="grid gap-2 md:col-span-2"><Label>Site Address</Label><Textarea rows={2} value={templateBuilder.developmentProfile.siteAddress} onChange={(e) => updateTemplateDevelopmentProfile({ siteAddress: e.target.value })} /></div>
                    <div className="grid gap-2 md:col-span-2"><Label>Profile Notes</Label><Textarea rows={2} value={templateBuilder.developmentProfile.notes} onChange={(e) => updateTemplateDevelopmentProfile({ notes: e.target.value })} /></div>
                  </div>
                </div>

                <div className="rounded-lg border p-4 space-y-4">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <div className="font-medium">Default Lifecycle Phases</div>
                      <div className="text-sm text-muted-foreground">Preload the construction lifecycle that new projects should start with.</div>
                    </div>
                    <Button
                      variant="outline"
                      onClick={() =>
                        updateTemplateBuilder((current) => ({
                          ...current,
                          phases: [
                            ...current.phases,
                            {
                              id: createTemplateRowId(),
                              code: '',
                              name: '',
                              description: '',
                              status: 'NotStarted',
                              isOptional: false,
                              isStageGateRequired: false,
                            },
                          ],
                        }))
                      }
                    >
                      Add Phase
                    </Button>
                  </div>
                  <div className="space-y-3">
                    {templateBuilder.phases.length === 0 ? <div className="text-sm text-muted-foreground">No phases added yet.</div> : null}
                    {templateBuilder.phases.map((phase, index) => (
                      <div key={phase.id} className="rounded-md border p-3 space-y-3">
                        <div className="flex items-center justify-between gap-3">
                          <div className="font-medium">Phase {index + 1}</div>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() =>
                              updateTemplateBuilder((current) => ({
                                ...current,
                                phases: current.phases.filter((item) => item.id !== phase.id),
                              }))
                            }
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                        <div className="grid gap-3 md:grid-cols-2">
                          <div className="grid gap-2"><Label>Code</Label><Input value={phase.code} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, phases: current.phases.map((item) => item.id === phase.id ? { ...item, code: e.target.value } : item) }))} /></div>
                          <div className="grid gap-2"><Label>Name</Label><Input value={phase.name} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, phases: current.phases.map((item) => item.id === phase.id ? { ...item, name: e.target.value } : item) }))} /></div>
                          <div className="grid gap-2"><Label>Status</Label><Input value={phase.status} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, phases: current.phases.map((item) => item.id === phase.id ? { ...item, status: e.target.value } : item) }))} /></div>
                          <div className="grid gap-2"><Label>Description</Label><Input value={phase.description} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, phases: current.phases.map((item) => item.id === phase.id ? { ...item, description: e.target.value } : item) }))} /></div>
                        </div>
                        <div className="grid gap-3 md:grid-cols-2">
                          <div className="flex items-center justify-between rounded-md border p-3">
                            <span className="text-sm">Optional phase</span>
                            <Switch checked={phase.isOptional} onCheckedChange={(checked) => updateTemplateBuilder((current) => ({ ...current, phases: current.phases.map((item) => item.id === phase.id ? { ...item, isOptional: checked } : item) }))} />
                          </div>
                          <div className="flex items-center justify-between rounded-md border p-3">
                            <span className="text-sm">Stage gate required</span>
                            <Switch checked={phase.isStageGateRequired} onCheckedChange={(checked) => updateTemplateBuilder((current) => ({ ...current, phases: current.phases.map((item) => item.id === phase.id ? { ...item, isStageGateRequired: checked } : item) }))} />
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="rounded-lg border p-4 space-y-4">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <div className="font-medium">Default Work Items</div>
                      <div className="text-sm text-muted-foreground">Optional starter work items or WBS entries for new projects.</div>
                    </div>
                    <Button
                      variant="outline"
                      onClick={() =>
                        updateTemplateBuilder((current) => ({
                          ...current,
                          workItems: [
                            ...current.workItems,
                            {
                              id: createTemplateRowId(),
                              title: '',
                              description: '',
                              nodeType: 'Task',
                              status: 'New',
                              priority: 'Normal',
                            },
                          ],
                        }))
                      }
                    >
                      Add Work Item
                    </Button>
                  </div>
                  <div className="space-y-3">
                    {templateBuilder.workItems.length === 0 ? <div className="text-sm text-muted-foreground">No starter work items added yet.</div> : null}
                    {templateBuilder.workItems.map((item, index) => (
                      <div key={item.id} className="rounded-md border p-3 space-y-3">
                        <div className="flex items-center justify-between gap-3">
                          <div className="font-medium">Work Item {index + 1}</div>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() =>
                              updateTemplateBuilder((current) => ({
                                ...current,
                                workItems: current.workItems.filter((entry) => entry.id !== item.id),
                              }))
                            }
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                        <div className="grid gap-3 md:grid-cols-2">
                          <div className="grid gap-2"><Label>Title</Label><Input value={item.title} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, workItems: current.workItems.map((entry) => entry.id === item.id ? { ...entry, title: e.target.value } : entry) }))} /></div>
                          <div className="grid gap-2"><Label>Node Type</Label><Input value={item.nodeType} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, workItems: current.workItems.map((entry) => entry.id === item.id ? { ...entry, nodeType: e.target.value } : entry) }))} /></div>
                          <div className="grid gap-2"><Label>Status</Label><Input value={item.status} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, workItems: current.workItems.map((entry) => entry.id === item.id ? { ...entry, status: e.target.value } : entry) }))} /></div>
                          <div className="grid gap-2"><Label>Priority</Label><Input value={item.priority} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, workItems: current.workItems.map((entry) => entry.id === item.id ? { ...entry, priority: e.target.value } : entry) }))} /></div>
                          <div className="grid gap-2 md:col-span-2"><Label>Description</Label><Textarea rows={2} value={item.description} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, workItems: current.workItems.map((entry) => entry.id === item.id ? { ...entry, description: e.target.value } : entry) }))} /></div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="rounded-lg border p-4 space-y-4">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <div className="font-medium">Default Milestones</div>
                      <div className="text-sm text-muted-foreground">Optional milestones to preload into project planning.</div>
                    </div>
                    <Button
                      variant="outline"
                      onClick={() =>
                        updateTemplateBuilder((current) => ({
                          ...current,
                          milestones: [
                            ...current.milestones,
                            {
                              id: createTemplateRowId(),
                              title: '',
                              description: '',
                              targetDate: '',
                              status: 'Draft',
                              requiresApproval: false,
                            },
                          ],
                        }))
                      }
                    >
                      Add Milestone
                    </Button>
                  </div>
                  <div className="space-y-3">
                    {templateBuilder.milestones.length === 0 ? <div className="text-sm text-muted-foreground">No starter milestones added yet.</div> : null}
                    {templateBuilder.milestones.map((milestone, index) => (
                      <div key={milestone.id} className="rounded-md border p-3 space-y-3">
                        <div className="flex items-center justify-between gap-3">
                          <div className="font-medium">Milestone {index + 1}</div>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() =>
                              updateTemplateBuilder((current) => ({
                                ...current,
                                milestones: current.milestones.filter((entry) => entry.id !== milestone.id),
                              }))
                            }
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                        <div className="grid gap-3 md:grid-cols-2">
                          <div className="grid gap-2"><Label>Title</Label><Input value={milestone.title} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, milestones: current.milestones.map((entry) => entry.id === milestone.id ? { ...entry, title: e.target.value } : entry) }))} /></div>
                          <div className="grid gap-2"><Label>Status</Label><Input value={milestone.status} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, milestones: current.milestones.map((entry) => entry.id === milestone.id ? { ...entry, status: e.target.value } : entry) }))} /></div>
                          <div className="grid gap-2"><Label>Target Date</Label><Input type="date" value={milestone.targetDate} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, milestones: current.milestones.map((entry) => entry.id === milestone.id ? { ...entry, targetDate: e.target.value } : entry) }))} /></div>
                          <div className="flex items-center justify-between rounded-md border p-3">
                            <span className="text-sm">Requires approval</span>
                            <Switch checked={milestone.requiresApproval} onCheckedChange={(checked) => updateTemplateBuilder((current) => ({ ...current, milestones: current.milestones.map((entry) => entry.id === milestone.id ? { ...entry, requiresApproval: checked } : entry) }))} />
                          </div>
                          <div className="grid gap-2 md:col-span-2"><Label>Description</Label><Textarea rows={2} value={milestone.description} onChange={(e) => updateTemplateBuilder((current) => ({ ...current, milestones: current.milestones.map((entry) => entry.id === milestone.id ? { ...entry, description: e.target.value } : entry) }))} /></div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="rounded-lg border p-4 space-y-4">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <div className="font-medium">Advanced JSON</div>
                      <div className="text-sm text-muted-foreground">Only use this when you need to inspect or repair the raw stored template definition.</div>
                    </div>
                    <Button variant="outline" onClick={() => setShowAdvancedTemplateJson((current) => !current)}>
                      {showAdvancedTemplateJson ? 'Hide Advanced JSON' : 'Show Advanced JSON'}
                    </Button>
                  </div>
                  {showAdvancedTemplateJson ? (
                    <div className="space-y-3">
                      <div className="grid gap-2">
                        <Label>Template Definition JSON</Label>
                        <Textarea rows={14} value={templateForm.templateDefinitionJson} onChange={(e) => updateAdvancedTemplateJson(e.target.value)} />
                      </div>
                      {templateJsonError ? (
                        <div className="text-sm text-amber-600">{templateJsonError}</div>
                      ) : (
                        <div className="text-sm text-muted-foreground">The structured builder above stays in sync whenever this JSON is valid.</div>
                      )}
                    </div>
                  ) : null}
                </div>

                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={templateForm.isActive !== false} onCheckedChange={(checked) => setTemplateForm((prev) => ({ ...prev, isActive: checked }))} /></div>

                <div className="flex gap-2">
                  <Button onClick={saveTemplate} disabled={saving || !templateForm.code || !templateForm.name || !!templateJsonError}>Save Template</Button>
                  {editingTemplateId && <Button variant="outline" onClick={() => { setEditingTemplateId(null); resetTemplateEditor(); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="phases">
          <ProjectPhaseLibraryAdmin onChanged={loadData} />
        </TabsContent>

        <TabsContent value="settings">
          <Card>
            <CardHeader>
              <CardTitle>Project Settings</CardTitle>
              <CardDescription>Tenant-aware defaults for numbering, workflow, and baseline records.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="grid gap-2"><Label>Project Number Format</Label><Input value={settingsForm.projectNumberFormat} onChange={(e) => setSettingsForm((prev) => ({ ...prev, projectNumberFormat: e.target.value }))} /></div>
                <div className="grid gap-2">
                  <Label>Default Project Type</Label>
                  <Select value={settingsForm.defaultProjectTypeId || 'none'} onValueChange={(value) => setSettingsForm((prev) => ({ ...prev, defaultProjectTypeId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="No default type" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No default type</SelectItem>
                      {types.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Default Priority</Label>
                  <Select value={settingsForm.defaultProjectPriorityId || 'none'} onValueChange={(value) => setSettingsForm((prev) => ({ ...prev, defaultProjectPriorityId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="No default priority" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No default priority</SelectItem>
                      {priorities.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Default Template</Label>
                  <Select value={settingsForm.defaultTemplateId || 'none'} onValueChange={(value) => setSettingsForm((prev) => ({ ...prev, defaultTemplateId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="No default template" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No default template</SelectItem>
                      {templates.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <div className="flex items-center justify-between border rounded-md p-4"><span className="text-sm">Require Sponsor</span><Switch checked={settingsForm.requireSponsor} onCheckedChange={(checked) => setSettingsForm((prev) => ({ ...prev, requireSponsor: checked }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-4"><span className="text-sm">Default Approval Required</span><Switch checked={settingsForm.defaultApprovalRequired} onCheckedChange={(checked) => setSettingsForm((prev) => ({ ...prev, defaultApprovalRequired: checked }))} /></div>
              </div>
              <div className="grid gap-2"><Label>Mandatory Fields By Type JSON</Label><Textarea rows={6} value={settingsForm.mandatoryFieldsByTypeJson} onChange={(e) => setSettingsForm((prev) => ({ ...prev, mandatoryFieldsByTypeJson: e.target.value }))} /></div>
              <div className="grid gap-2"><Label>Notes</Label><Textarea rows={4} value={settingsForm.notes} onChange={(e) => setSettingsForm((prev) => ({ ...prev, notes: e.target.value }))} /></div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
