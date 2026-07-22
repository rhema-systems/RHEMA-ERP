'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  buildProjectCurrencyOptions,
  DEFAULT_PROJECT_CURRENCY,
  findProjectCurrency,
  formatProjectCurrencyLabel,
  loadProjectCurrencyContext,
  type ProjectCurrencyReference,
} from '@/lib/project-currency';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { contractService, type ContractDto } from '@/services/contractService';
import { type CurrencyListDto } from '@/services/financeCommonService';
import {
  CreateProjectDto,
  ProjectCatalogEntryDto,
  ProjectManagementSettingsDto,
  ProjectPortfolioDto,
  ProjectProgramDto,
  ProjectPriorityDto,
  ProjectTemplateDto,
  ProjectTypeDto,
  projectService,
} from '@/services/projectService';
import {
  estateLandManagementService,
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { userService } from '@/services/user';
import type { User } from '@/types';
import { ArrowLeft, Save } from 'lucide-react';
import { toast } from 'sonner';

const DEFAULT_METHODOLOGIES = ['Waterfall', 'Agile', 'Hybrid', 'Program', 'Internal'];
const DEFAULT_FUNDING_SOURCES = ['Customer Contract', 'Internal Budget', 'Capex Allocation', 'Grant Funding', 'Department Allocation'];
const DELIVERY_STRUCTURES = ['WholeDevelopment', 'SingleUnit', 'MultiUnit'];
const DEVELOPMENT_TYPES = ['Residential', 'Commercial', 'Industrial', 'MixedUse', 'Hospitality', 'Institutional', 'Infrastructure', 'Renovation'];
const PROCUREMENT_ROUTES = ['Traditional', 'DesignBuild', 'ConstructionManagement', 'DirectLabour', 'Negotiated', 'FrameworkCallOff'];
const CONTRACT_STRATEGIES = ['LumpSum', 'MeasuredWorks', 'CostPlus', 'TargetCost', 'ManagementContract', 'SubcontractPackages'];
const HANDOVER_STRATEGIES = ['SingleHandover', 'PhasedHandover', 'UnitByUnitHandover', 'ShellAndCore', 'Turnkey'];

interface ProjectTemplatePreview {
  phaseNames: string[];
  milestoneCount: number;
  workItemCount: number;
  developmentProfile: Partial<NonNullable<CreateProjectDto['developmentProfile']>>;
}

const formatCatalogLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

const formatUserLabel = (user: User) => {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  return fullName ? `${fullName} (${user.username})` : user.username;
};

const formatBusinessPartnerLabel = (partner: BusinessPartnerDto) =>
  `${partner.partnerName}${partner.partnerType ? ` (${partner.partnerType})` : ''}`;

const formatContractLabel = (contract: ContractDto) =>
  `${contract.contractNumber} - ${contract.contractTitle}`;

const readyLandReferenceValue = (asset: EstateManagedAsset) =>
  asset.projectCode?.trim() || asset.assetCode;

const formatReadyLandLabel = (asset: EstateManagedAsset) => {
  const details = [asset.assetCode, asset.name, asset.location].filter(Boolean).join(' - ');
  const acquisitionReference = asset.projectCode?.trim();
  return acquisitionReference && acquisitionReference !== asset.assetCode
    ? `${acquisitionReference} (${details})`
    : details;
};

const resolveCatalogOptions = (entries: ProjectCatalogEntryDto[], fallbackValues: string[], currentValue?: string) => {
  const configured = entries
    .filter((entry) => entry.isActive)
    .map((entry) => entry.name.trim())
    .filter(Boolean);

  const values = configured.length > 0 ? configured : fallbackValues;
  return currentValue && !values.includes(currentValue) ? [currentValue, ...values] : values;
};

const readRequiredFields = (value?: string): string[] => {
  if (!value?.trim()) return [];

  try {
    const parsed = JSON.parse(value);
    if (Array.isArray(parsed)) {
      return parsed.filter((item): item is string => typeof item === 'string' && item.trim().length > 0);
    }

    if (parsed && typeof parsed === 'object' && Array.isArray((parsed as { fields?: unknown }).fields)) {
      return (parsed as { fields: unknown[] }).fields.filter((item): item is string => typeof item === 'string' && item.trim().length > 0);
    }
  } catch {
    return [];
  }

  return [];
};

const parseTemplatePreview = (templateDefinitionJson?: string): ProjectTemplatePreview | null => {
  if (!templateDefinitionJson?.trim()) return null;

  try {
    const parsed = JSON.parse(templateDefinitionJson) as Record<string, unknown>;
    const developmentProfileSource =
      typeof parsed.developmentProfile === 'object' && parsed.developmentProfile !== null
        ? (parsed.developmentProfile as Record<string, unknown>)
        : typeof parsed.constructionProfile === 'object' && parsed.constructionProfile !== null
          ? (parsed.constructionProfile as Record<string, unknown>)
          : null;

    const phaseSource = Array.isArray(parsed.projectPhases)
      ? parsed.projectPhases
      : Array.isArray(parsed.phases)
        ? parsed.phases
        : [];

    const phaseNames = phaseSource
      .map((phase) => {
        if (typeof phase === 'string') return phase.trim();
        if (phase && typeof phase === 'object') {
          const record = phase as Record<string, unknown>;
          if (typeof record.name === 'string' && record.name.trim()) return record.name.trim();
          if (typeof record.title === 'string' && record.title.trim()) return record.title.trim();
        }
        return '';
      })
      .filter(Boolean);

    return {
      phaseNames,
      milestoneCount: Array.isArray(parsed.milestones) ? parsed.milestones.length : 0,
      workItemCount: Array.isArray(parsed.workItems) ? parsed.workItems.length : 0,
      developmentProfile: {
        deliveryStructure: typeof developmentProfileSource?.deliveryStructure === 'string' ? developmentProfileSource.deliveryStructure : undefined,
        developmentType: typeof developmentProfileSource?.developmentType === 'string' ? developmentProfileSource.developmentType : undefined,
        siteName: typeof developmentProfileSource?.siteName === 'string' ? developmentProfileSource.siteName : undefined,
        siteAddress: typeof developmentProfileSource?.siteAddress === 'string' ? developmentProfileSource.siteAddress : undefined,
        landReference: typeof developmentProfileSource?.landReference === 'string' ? developmentProfileSource.landReference : undefined,
        procurementRoute: typeof developmentProfileSource?.procurementRoute === 'string' ? developmentProfileSource.procurementRoute : undefined,
        contractStrategy: typeof developmentProfileSource?.contractStrategy === 'string' ? developmentProfileSource.contractStrategy : undefined,
        consultantTeam: typeof developmentProfileSource?.consultantTeam === 'string' ? developmentProfileSource.consultantTeam : undefined,
        fundingArrangement: typeof developmentProfileSource?.fundingArrangement === 'string' ? developmentProfileSource.fundingArrangement : undefined,
        handoverStrategy: typeof developmentProfileSource?.handoverStrategy === 'string' ? developmentProfileSource.handoverStrategy : undefined,
        notes: typeof developmentProfileSource?.notes === 'string' ? developmentProfileSource.notes : undefined,
      },
    };
  } catch {
    return null;
  }
};

const labelWithRequired = (label: string, required: boolean) => (
  <span>
    {label}
    {required ? <span className="text-destructive"> *</span> : null}
  </span>
);

const hasValue = (value: unknown) => {
  if (typeof value === 'string') {
    return value.trim().length > 0;
  }

  return value !== undefined && value !== null;
};

const initialForm: CreateProjectDto = {
  title: '',
  summary: '',
  businessCase: '',
  objectives: '',
  methodology: 'Hybrid',
  startDate: '',
  targetEndDate: '',
  approvalRequired: true,
  developmentProfile: {
    deliveryStructure: 'WholeDevelopment',
  },
};

export default function NewProjectPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [types, setTypes] = useState<ProjectTypeDto[]>([]);
  const [priorities, setPriorities] = useState<ProjectPriorityDto[]>([]);
  const [templates, setTemplates] = useState<ProjectTemplateDto[]>([]);
  const [projectSettings, setProjectSettings] = useState<ProjectManagementSettingsDto | null>(null);
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [programs, setPrograms] = useState<ProjectProgramDto[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [financeBaseCurrency, setFinanceBaseCurrency] = useState<ProjectCurrencyReference>(DEFAULT_PROJECT_CURRENCY);
  const [businessPartners, setBusinessPartners] = useState<BusinessPartnerDto[]>([]);
  const [contracts, setContracts] = useState<ContractDto[]>([]);
  const [readyLandAssets, setReadyLandAssets] = useState<EstateManagedAsset[]>([]);
  const [methodologyCatalog, setMethodologyCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [fundingSourceCatalog, setFundingSourceCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [form, setForm] = useState<CreateProjectDto>(initialForm);

  const methodologyOptions = resolveCatalogOptions(methodologyCatalog, DEFAULT_METHODOLOGIES, form.methodology);
  const fundingSourceOptions = resolveCatalogOptions(fundingSourceCatalog, DEFAULT_FUNDING_SOURCES, form.fundingSource);
  const projectCurrencyOptions = useMemo(
    () => buildProjectCurrencyOptions(currencies, financeBaseCurrency, form.baseCurrencyCode),
    [currencies, financeBaseCurrency, form.baseCurrencyCode],
  );
  const getCurrencyOptionLabel = (code: string) =>
    formatProjectCurrencyLabel(findProjectCurrency(currencies, code, financeBaseCurrency), code);
  const activeUsers = useMemo(() => users.filter((user) => user.isActive), [users]);
  const selectedProjectType = useMemo(
    () => types.find((item) => item.id === form.projectTypeId),
    [types, form.projectTypeId],
  );
  const selectedTemplate = useMemo(
    () => templates.find((item) => item.id === form.templateId),
    [templates, form.templateId],
  );
  const selectedTemplatePreview = useMemo(
    () => parseTemplatePreview(selectedTemplate?.templateDefinitionJson),
    [selectedTemplate?.templateDefinitionJson],
  );
  const availableTemplates = useMemo(() => {
    const filtered = templates.filter((item) => !form.projectTypeId || !item.projectTypeId || item.projectTypeId === form.projectTypeId || item.id === form.templateId);
    return filtered;
  }, [templates, form.projectTypeId, form.templateId]);
  const approvedBusinessPartners = useMemo(
    () =>
      businessPartners.filter((partner) => {
        const status = partner.status?.trim().toLowerCase();
        const approvalStatus = partner.approvalStatus?.trim().toLowerCase();
        return !partner.isBlacklisted && (status === 'active' || status === 'approved' || approvalStatus === 'approved');
      }),
    [businessPartners],
  );
  const availableBusinessPartners = useMemo(() => {
    const fallbackPartners = businessPartners.filter((partner) => !partner.isBlacklisted);
    const source = approvedBusinessPartners.length > 0 ? approvedBusinessPartners : fallbackPartners;
    return [...source].sort((left, right) => left.partnerName.localeCompare(right.partnerName));
  }, [businessPartners, approvedBusinessPartners]);
  const isUsingBusinessPartnerFallback = businessPartners.length > 0 && approvedBusinessPartners.length === 0 && availableBusinessPartners.length > 0;
  const availableContracts = useMemo(
    () => contracts.filter((contract) => !form.businessPartnerId || contract.businessPartnerId === form.businessPartnerId),
    [contracts, form.businessPartnerId],
  );
  const requiredFields = useMemo(() => {
    const required = new Set<string>(['Title', 'DevelopmentProfile.DeliveryStructure']);

    if (selectedProjectType?.requiresSponsor) {
      required.add('SponsorId');
    }

    readRequiredFields(selectedProjectType?.mandatoryFieldsJson).forEach((field) => required.add(field));

    if (projectSettings?.mandatoryFieldsByTypeJson && selectedProjectType?.code) {
      try {
        const parsed = JSON.parse(projectSettings.mandatoryFieldsByTypeJson) as Record<string, unknown>;
        const typeSpecific = parsed[selectedProjectType.code];
        if (Array.isArray(typeSpecific)) {
          typeSpecific
            .filter((field): field is string => typeof field === 'string' && field.trim().length > 0)
            .forEach((field) => required.add(field));
        }
      } catch {
        // Ignore malformed setup JSON and rely on the hard validation paths instead.
      }
    }

    return required;
  }, [projectSettings?.mandatoryFieldsByTypeJson, selectedProjectType]);

  useEffect(() => {
    const loadSetup = async () => {
      try {
        const [loadedTypes, loadedPriorities, loadedTemplates, loadedPortfolios, settings, loadedMethodologies, loadedFundingSources, loadedUsers, loadedPartners, loadedContracts, loadedReadyLandAssets, currencyContext] = await Promise.all([
          projectService.getProjectTypes(),
          projectService.getProjectPriorities(),
          projectService.getProjectTemplates(),
          projectService.getPortfolios(),
          projectService.getSettings(),
          projectService.getCatalogEntries('methodologies').catch(() => []),
          projectService.getCatalogEntries('funding-sources').catch(() => []),
          userService.searchUsers('').catch(() => []),
          businessPartnerService.getAllPartnersForDropdown().catch(() => businessPartnerService.getActivePartners().catch(() => [])),
          contractService.getActiveContracts().catch(() => []),
          estateLandManagementService
            .getManagedAssets({
              assetType: EstateManagedAssetType.Land,
              status: EstateManagedAssetStatus.LandBank,
              take: 500,
            })
            .then((assets) => assets.filter((asset) => asset.isReadyForProjectManagement))
            .catch(() => []),
          loadProjectCurrencyContext(),
        ]);
        setTypes(loadedTypes);
        setPriorities(loadedPriorities);
        setTemplates(loadedTemplates);
        setProjectSettings(settings);
        setPortfolios(loadedPortfolios);
        setMethodologyCatalog(loadedMethodologies);
        setFundingSourceCatalog(loadedFundingSources);
        setUsers(loadedUsers);
        setCurrencies(currencyContext.activeCurrencies);
        setFinanceBaseCurrency(currencyContext.baseCurrency);
        setBusinessPartners(loadedPartners);
        setContracts(loadedContracts);
        setReadyLandAssets(loadedReadyLandAssets);
        setForm((prev) => ({
          ...prev,
          projectTypeId: settings.defaultProjectTypeId,
          projectPriorityId: settings.defaultProjectPriorityId,
          templateId: settings.defaultTemplateId,
          approvalRequired: settings.defaultApprovalRequired,
          methodology: resolveCatalogOptions(loadedMethodologies, DEFAULT_METHODOLOGIES, prev.methodology)[0] || prev.methodology,
          fundingSource: resolveCatalogOptions(loadedFundingSources, DEFAULT_FUNDING_SOURCES, prev.fundingSource)[0] || prev.fundingSource,
          baseCurrencyCode: prev.baseCurrencyCode || currencyContext.baseCurrency.code,
        }));
      } catch (error: any) {
        toast.error(error.message || 'Failed to load project setup data');
      }
    };

    loadSetup();
  }, []);

  useEffect(() => {
    const loadPrograms = async () => {
      try {
        const loadedPrograms = await projectService.getPrograms(form.portfolioId);
        setPrograms(loadedPrograms);
      } catch (error: any) {
        toast.error(error.message || 'Failed to load project programs');
      }
    };

    loadPrograms();
  }, [form.portfolioId]);

  const updateDevelopmentProfile = (updates: Partial<NonNullable<CreateProjectDto['developmentProfile']>>) => {
    setForm((prev) => ({
      ...prev,
      developmentProfile: {
        deliveryStructure: prev.developmentProfile?.deliveryStructure || 'WholeDevelopment',
        ...prev.developmentProfile,
        ...updates,
      },
    }));
  };

  const saveProject = async () => {
    try {
      if (!form.title?.trim()) {
        toast.error('Project title is required');
        return;
      }

      const validationTargets: Array<{ field: string; label: string; value: unknown }> = [
        { field: 'Title', label: 'Project title', value: form.title?.trim() },
        { field: 'DevelopmentProfile.DeliveryStructure', label: 'Delivery structure', value: form.developmentProfile?.deliveryStructure },
        { field: 'SponsorId', label: 'Sponsor', value: form.sponsorId },
        { field: 'ProjectManagerId', label: 'Project manager', value: form.projectManagerId },
        { field: 'EstimatedBudget', label: 'Estimated budget', value: form.estimatedBudget },
        { field: 'BaseCurrencyCode', label: 'Project base currency', value: form.baseCurrencyCode },
        { field: 'StartDate', label: 'Start date', value: form.startDate },
        { field: 'TargetEndDate', label: 'Target end date', value: form.targetEndDate },
      ];

      validationTargets.push(
        { field: 'ProjectTypeId', label: 'Project type', value: form.projectTypeId },
        { field: 'TemplateId', label: 'Template', value: form.templateId },
        { field: 'Methodology', label: 'Methodology', value: form.methodology },
        { field: 'DevelopmentProfile.DevelopmentType', label: 'Development type', value: form.developmentProfile?.developmentType },
        { field: 'DevelopmentProfile.ProcurementRoute', label: 'Procurement route', value: form.developmentProfile?.procurementRoute },
        { field: 'DevelopmentProfile.ContractStrategy', label: 'Contract strategy', value: form.developmentProfile?.contractStrategy },
        { field: 'DevelopmentProfile.HandoverStrategy', label: 'Handover strategy', value: form.developmentProfile?.handoverStrategy },
        { field: 'DevelopmentProfile.LandReference', label: 'Demarcated land', value: form.developmentProfile?.landReference },
        { field: 'FundingSource', label: 'Funding source', value: form.fundingSource },
        { field: 'BusinessPartnerId', label: 'Business partner', value: form.businessPartnerId },
      );

      const missingRequirement = validationTargets.find((item) => requiredFields.has(item.field) && !hasValue(item.value));
      if (missingRequirement) {
        toast.error(`${missingRequirement.label} is required for the selected project setup`);
        return;
      }

      setLoading(true);
      const created = await projectService.createProject(form);
      toast.success('Project created');
      router.push(`/development/projects/${created.id}`);
    } catch (error: any) {
      toast.error(error.message || 'Failed to create project');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={() => router.push('/development/projects')}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Create Project</h1>
            <p className="text-muted-foreground">Register a new project request and seed the initial workspace.</p>
          </div>
        </div>
        <Button onClick={saveProject} disabled={loading}>
          <Save className="mr-2 h-4 w-4" />
          {loading ? 'Creating...' : 'Create Project'}
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Initiation</CardTitle>
          <CardDescription>
            Capture enough context to open the project as a governed draft. Fields marked with <span className="text-destructive">*</span> are required for the current project setup.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-6 md:grid-cols-2">
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="title">{labelWithRequired('Title', requiredFields.has('Title'))}</Label>
            <Input id="title" value={form.title} onChange={(e) => setForm((prev) => ({ ...prev, title: e.target.value }))} />
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Project Type', requiredFields.has('ProjectTypeId'))}</Label>
            <Select value={form.projectTypeId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, projectTypeId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No type</SelectItem>
                {types.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Priority</Label>
            <Select value={form.projectPriorityId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, projectPriorityId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select priority" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No priority</SelectItem>
                {priorities.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Template', requiredFields.has('TemplateId'))}</Label>
            <Select
              value={form.templateId || 'none'}
              onValueChange={(value) => {
                const nextTemplateId = value === 'none' ? undefined : value;
                const nextTemplate = templates.find((item) => item.id === nextTemplateId);
                const preview = parseTemplatePreview(nextTemplate?.templateDefinitionJson);

                setForm((prev) => ({
                  ...prev,
                  templateId: nextTemplateId,
                  projectTypeId: nextTemplate?.projectTypeId || prev.projectTypeId,
                  developmentProfile: {
                    deliveryStructure: preview?.developmentProfile.deliveryStructure || prev.developmentProfile?.deliveryStructure || 'WholeDevelopment',
                    ...prev.developmentProfile,
                    ...preview?.developmentProfile,
                  },
                }));
              }}
            >
              <SelectTrigger><SelectValue placeholder="Select template" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No template</SelectItem>
                {availableTemplates.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
              </SelectContent>
            </Select>
            <div className="text-xs text-muted-foreground">
              Phase defaults belong to Administration &gt; Project Management &gt; Templates. The selected template seeds those phases into the project after creation.
            </div>
          </div>
          <div className="grid gap-2">
            <Label>Portfolio</Label>
            <Select value={form.portfolioId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, portfolioId: value === 'none' ? undefined : value, programId: undefined }))}>
              <SelectTrigger><SelectValue placeholder="Select portfolio" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No portfolio</SelectItem>
                {portfolios.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Program</Label>
            <Select value={form.programId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, programId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select program" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No program</SelectItem>
                {programs.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          {selectedTemplatePreview ? (
            <div className="md:col-span-2 rounded-md border bg-muted/20 p-4">
              <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                <div className="space-y-1">
                  <div className="font-medium">Template Preview</div>
                  <div className="text-sm text-muted-foreground">
                    Phases are maintained in administration and will be seeded into the project workspace after creation.
                  </div>
                </div>
                <div className="text-sm text-muted-foreground">
                  {selectedTemplatePreview.phaseNames.length} phases, {selectedTemplatePreview.workItemCount} work items, {selectedTemplatePreview.milestoneCount} milestones
                </div>
              </div>
              <div className="mt-3 flex flex-wrap gap-2">
                {selectedTemplatePreview.phaseNames.map((phase) => (
                  <span key={phase} className="rounded-full border px-3 py-1 text-xs text-muted-foreground">
                    {phase}
                  </span>
                ))}
              </div>
              <div className="mt-3 grid gap-2 md:grid-cols-2 xl:grid-cols-4">
                {selectedTemplatePreview.developmentProfile.developmentType ? (
                  <div className="rounded-md border px-3 py-2 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Development</div>
                    <div>{formatCatalogLabel(selectedTemplatePreview.developmentProfile.developmentType)}</div>
                  </div>
                ) : null}
                {selectedTemplatePreview.developmentProfile.deliveryStructure ? (
                  <div className="rounded-md border px-3 py-2 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Delivery</div>
                    <div>{formatCatalogLabel(selectedTemplatePreview.developmentProfile.deliveryStructure)}</div>
                  </div>
                ) : null}
                {selectedTemplatePreview.developmentProfile.procurementRoute ? (
                  <div className="rounded-md border px-3 py-2 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Procurement</div>
                    <div>{formatCatalogLabel(selectedTemplatePreview.developmentProfile.procurementRoute)}</div>
                  </div>
                ) : null}
                {selectedTemplatePreview.developmentProfile.contractStrategy ? (
                  <div className="rounded-md border px-3 py-2 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Contract</div>
                    <div>{formatCatalogLabel(selectedTemplatePreview.developmentProfile.contractStrategy)}</div>
                  </div>
                ) : null}
              </div>
            </div>
          ) : null}
          <div className="grid gap-2">
            <Label>{labelWithRequired('Methodology', requiredFields.has('Methodology'))}</Label>
            <Select value={form.methodology || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, methodology: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select methodology" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No methodology</SelectItem>
                {methodologyOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Delivery Structure', requiredFields.has('DevelopmentProfile.DeliveryStructure'))}</Label>
            <Select value={form.developmentProfile?.deliveryStructure || 'WholeDevelopment'} onValueChange={(value) => updateDevelopmentProfile({ deliveryStructure: value })}>
              <SelectTrigger><SelectValue placeholder="Select delivery structure" /></SelectTrigger>
              <SelectContent>
                {DELIVERY_STRUCTURES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Development Type', requiredFields.has('DevelopmentProfile.DevelopmentType'))}</Label>
            <Select value={form.developmentProfile?.developmentType || 'none'} onValueChange={(value) => updateDevelopmentProfile({ developmentType: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select development type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No development type</SelectItem>
                {DEVELOPMENT_TYPES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="site-name">Site Name</Label>
            <Input id="site-name" value={form.developmentProfile?.siteName || ''} onChange={(e) => updateDevelopmentProfile({ siteName: e.target.value || undefined })} />
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Procurement Route', requiredFields.has('DevelopmentProfile.ProcurementRoute'))}</Label>
            <Select value={form.developmentProfile?.procurementRoute || 'none'} onValueChange={(value) => updateDevelopmentProfile({ procurementRoute: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select procurement route" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No procurement route</SelectItem>
                {PROCUREMENT_ROUTES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Contract Strategy', requiredFields.has('DevelopmentProfile.ContractStrategy'))}</Label>
            <Select value={form.developmentProfile?.contractStrategy || 'none'} onValueChange={(value) => updateDevelopmentProfile({ contractStrategy: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select contract strategy" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No contract strategy</SelectItem>
                {CONTRACT_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Handover Strategy', requiredFields.has('DevelopmentProfile.HandoverStrategy'))}</Label>
            <Select value={form.developmentProfile?.handoverStrategy || 'none'} onValueChange={(value) => updateDevelopmentProfile({ handoverStrategy: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select handover strategy" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No handover strategy</SelectItem>
                {HANDOVER_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="start-date">{labelWithRequired('Start Date', requiredFields.has('StartDate'))}</Label>
            <Input id="start-date" type="date" value={form.startDate || ''} onChange={(e) => setForm((prev) => ({ ...prev, startDate: e.target.value || undefined }))} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="end-date">{labelWithRequired('Target End Date', requiredFields.has('TargetEndDate'))}</Label>
            <Input id="end-date" type="date" value={form.targetEndDate || ''} onChange={(e) => setForm((prev) => ({ ...prev, targetEndDate: e.target.value || undefined }))} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="estimated-budget">{labelWithRequired('Estimated Budget', requiredFields.has('EstimatedBudget'))}</Label>
            <Input id="estimated-budget" type="number" value={form.estimatedBudget ?? ''} onChange={(e) => setForm((prev) => ({ ...prev, estimatedBudget: e.target.value ? Number(e.target.value) : undefined }))} />
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Project Base Currency', requiredFields.has('BaseCurrencyCode'))}</Label>
            <Select value={form.baseCurrencyCode || financeBaseCurrency.code} onValueChange={(value) => setForm((prev) => ({ ...prev, baseCurrencyCode: value }))}>
              <SelectTrigger><SelectValue placeholder="Select project base currency" /></SelectTrigger>
              <SelectContent>
                {projectCurrencyOptions.map((item) => (
                  <SelectItem key={item} value={item}>
                    {getCurrencyOptionLabel(item)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <div className="text-xs text-muted-foreground">
              Finance base currency is {getCurrencyOptionLabel(financeBaseCurrency.code)}. Keep it for standard reporting or choose a project-specific reporting currency.
            </div>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Funding Source', requiredFields.has('FundingSource'))}</Label>
            <Select value={form.fundingSource || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, fundingSource: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select funding source" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No funding source</SelectItem>
                {fundingSourceOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Sponsor', requiredFields.has('SponsorId'))}</Label>
            <Select value={form.sponsorId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, sponsorId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select sponsor" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No sponsor</SelectItem>
                {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Project Manager', requiredFields.has('ProjectManagerId'))}</Label>
            <Select value={form.projectManagerId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, projectManagerId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select project manager" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No project manager</SelectItem>
                {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Business Partner', requiredFields.has('BusinessPartnerId'))}</Label>
            <Select value={form.businessPartnerId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, businessPartnerId: value === 'none' ? undefined : value, contractId: undefined }))}>
              <SelectTrigger><SelectValue placeholder="Select business partner" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No business partner</SelectItem>
                {availableBusinessPartners.map((partner) => (
                  <SelectItem key={partner.id} value={partner.id}>
                    {formatBusinessPartnerLabel(partner)}
                    {partner.status !== 'Active' && partner.status !== 'Approved' ? ` - ${partner.status}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isUsingBusinessPartnerFallback ? (
              <div className="text-xs text-muted-foreground">
                No approved active partners were found, so all non-blacklisted partners are shown as a fallback.
              </div>
            ) : null}
            {availableBusinessPartners.length === 0 ? (
              <div className="text-xs text-muted-foreground">
                No business partners are available yet. Create or approve one in Procurement first.
              </div>
            ) : null}
          </div>
          <div className="grid gap-2">
            <Label>Contract</Label>
            <Select value={form.contractId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, contractId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select contract" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No contract</SelectItem>
                {availableContracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{formatContractLabel(contract)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="summary">Summary</Label>
            <Textarea id="summary" rows={3} value={form.summary || ''} onChange={(e) => setForm((prev) => ({ ...prev, summary: e.target.value }))} />
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="business-case">Business Case</Label>
            <Textarea id="business-case" rows={4} value={form.businessCase || ''} onChange={(e) => setForm((prev) => ({ ...prev, businessCase: e.target.value }))} />
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="objectives">Objectives</Label>
            <Textarea id="objectives" rows={3} value={form.objectives || ''} onChange={(e) => setForm((prev) => ({ ...prev, objectives: e.target.value }))} />
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="scope">Scope Statement</Label>
            <Textarea id="scope" rows={3} value={form.scopeStatement || ''} onChange={(e) => setForm((prev) => ({ ...prev, scopeStatement: e.target.value }))} />
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="site-address">Site Address</Label>
            <Textarea id="site-address" rows={2} value={form.developmentProfile?.siteAddress || ''} onChange={(e) => updateDevelopmentProfile({ siteAddress: e.target.value || undefined })} />
          </div>
          <div className="grid gap-2">
            <Label>{labelWithRequired('Demarcated Land', requiredFields.has('DevelopmentProfile.LandReference'))}</Label>
            <Select
              value={form.developmentProfile?.landReference || 'none'}
              onValueChange={(value) => updateDevelopmentProfile({ landReference: value === 'none' ? undefined : value })}
            >
              <SelectTrigger><SelectValue placeholder="Select ready land" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No land selected</SelectItem>
                {readyLandAssets.map((asset) => (
                  <SelectItem key={asset.id} value={readyLandReferenceValue(asset)}>
                    {formatReadyLandLabel(asset)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {readyLandAssets.length === 0 ? (
              <div className="text-xs text-muted-foreground">
                No demarcated land is ready for project management yet.
              </div>
            ) : null}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="funding-arrangement">Funding Arrangement</Label>
            <Input id="funding-arrangement" value={form.developmentProfile?.fundingArrangement || ''} onChange={(e) => updateDevelopmentProfile({ fundingArrangement: e.target.value || undefined })} />
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="benefits">Expected Benefits</Label>
            <Textarea id="benefits" rows={3} value={form.expectedBenefits || ''} onChange={(e) => setForm((prev) => ({ ...prev, expectedBenefits: e.target.value }))} />
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="consultant-team">Consultant Team</Label>
            <Textarea id="consultant-team" rows={2} value={form.developmentProfile?.consultantTeam || ''} onChange={(e) => updateDevelopmentProfile({ consultantTeam: e.target.value || undefined })} />
          </div>
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="construction-notes">Construction Notes</Label>
            <Textarea id="construction-notes" rows={3} value={form.developmentProfile?.notes || ''} onChange={(e) => updateDevelopmentProfile({ notes: e.target.value || undefined })} />
          </div>
          <div className="md:col-span-2 flex items-center justify-between rounded-md border p-4">
            <div>
              <div className="font-medium">Approval Required</div>
              <div className="text-sm text-muted-foreground">Keep enabled to submit initiation through the workflow engine.</div>
            </div>
            <Switch checked={!!form.approvalRequired} onCheckedChange={(checked) => setForm((prev) => ({ ...prev, approvalRequired: checked }))} />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
