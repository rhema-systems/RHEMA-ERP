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
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { contractService, type ContractDto } from '@/services/contractService';
import {
  CreateProjectDto,
  ProjectCatalogEntryDto,
  ProjectPortfolioDto,
  ProjectProgramDto,
  ProjectPriorityDto,
  ProjectTemplateDto,
  ProjectTypeDto,
  projectService,
} from '@/services/projectService';
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

const resolveCatalogOptions = (entries: ProjectCatalogEntryDto[], fallbackValues: string[], currentValue?: string) => {
  const configured = entries
    .filter((entry) => entry.isActive)
    .map((entry) => entry.name.trim())
    .filter(Boolean);

  const values = configured.length > 0 ? configured : fallbackValues;
  return currentValue && !values.includes(currentValue) ? [currentValue, ...values] : values;
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
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [programs, setPrograms] = useState<ProjectProgramDto[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [businessPartners, setBusinessPartners] = useState<BusinessPartnerDto[]>([]);
  const [contracts, setContracts] = useState<ContractDto[]>([]);
  const [methodologyCatalog, setMethodologyCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [fundingSourceCatalog, setFundingSourceCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [form, setForm] = useState<CreateProjectDto>(initialForm);

  const methodologyOptions = resolveCatalogOptions(methodologyCatalog, DEFAULT_METHODOLOGIES, form.methodology);
  const fundingSourceOptions = resolveCatalogOptions(fundingSourceCatalog, DEFAULT_FUNDING_SOURCES, form.fundingSource);
  const activeUsers = useMemo(() => users.filter((user) => user.isActive), [users]);
  const availableBusinessPartners = useMemo(
    () => businessPartners.filter((partner) => partner.status === 'Active'),
    [businessPartners],
  );
  const availableContracts = useMemo(
    () => contracts.filter((contract) => !form.businessPartnerId || contract.businessPartnerId === form.businessPartnerId),
    [contracts, form.businessPartnerId],
  );

  useEffect(() => {
    const loadSetup = async () => {
      try {
        const [loadedTypes, loadedPriorities, loadedTemplates, loadedPortfolios, settings, loadedMethodologies, loadedFundingSources, loadedUsers, loadedPartners, loadedContracts] = await Promise.all([
          projectService.getProjectTypes(),
          projectService.getProjectPriorities(),
          projectService.getProjectTemplates(),
          projectService.getPortfolios(),
          projectService.getSettings(),
          projectService.getCatalogEntries('methodologies').catch(() => []),
          projectService.getCatalogEntries('funding-sources').catch(() => []),
          userService.searchUsers('').catch(() => []),
          businessPartnerService.getActivePartners().catch(() => []),
          contractService.getActiveContracts().catch(() => []),
        ]);
        setTypes(loadedTypes);
        setPriorities(loadedPriorities);
        setTemplates(loadedTemplates);
        setPortfolios(loadedPortfolios);
        setMethodologyCatalog(loadedMethodologies);
        setFundingSourceCatalog(loadedFundingSources);
        setUsers(loadedUsers);
        setBusinessPartners(loadedPartners);
        setContracts(loadedContracts);
        setForm((prev) => ({
          ...prev,
          projectTypeId: settings.defaultProjectTypeId,
          projectPriorityId: settings.defaultProjectPriorityId,
          templateId: settings.defaultTemplateId,
          approvalRequired: settings.defaultApprovalRequired,
          methodology: resolveCatalogOptions(loadedMethodologies, DEFAULT_METHODOLOGIES, prev.methodology)[0] || prev.methodology,
          fundingSource: resolveCatalogOptions(loadedFundingSources, DEFAULT_FUNDING_SOURCES, prev.fundingSource)[0] || prev.fundingSource,
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
          <CardDescription>Capture enough context to open the project as a governed draft.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-6 md:grid-cols-2">
          <div className="grid gap-2 md:col-span-2">
            <Label htmlFor="title">Title</Label>
            <Input id="title" value={form.title} onChange={(e) => setForm((prev) => ({ ...prev, title: e.target.value }))} />
          </div>
          <div className="grid gap-2">
            <Label>Project Type</Label>
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
            <Label>Template</Label>
            <Select value={form.templateId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, templateId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select template" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No template</SelectItem>
                {templates.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
              </SelectContent>
            </Select>
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
          <div className="grid gap-2">
            <Label>Methodology</Label>
            <Select value={form.methodology || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, methodology: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select methodology" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No methodology</SelectItem>
                {methodologyOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Delivery Structure</Label>
            <Select value={form.developmentProfile?.deliveryStructure || 'WholeDevelopment'} onValueChange={(value) => updateDevelopmentProfile({ deliveryStructure: value })}>
              <SelectTrigger><SelectValue placeholder="Select delivery structure" /></SelectTrigger>
              <SelectContent>
                {DELIVERY_STRUCTURES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Development Type</Label>
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
            <Label>Procurement Route</Label>
            <Select value={form.developmentProfile?.procurementRoute || 'none'} onValueChange={(value) => updateDevelopmentProfile({ procurementRoute: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select procurement route" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No procurement route</SelectItem>
                {PROCUREMENT_ROUTES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Contract Strategy</Label>
            <Select value={form.developmentProfile?.contractStrategy || 'none'} onValueChange={(value) => updateDevelopmentProfile({ contractStrategy: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select contract strategy" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No contract strategy</SelectItem>
                {CONTRACT_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Handover Strategy</Label>
            <Select value={form.developmentProfile?.handoverStrategy || 'none'} onValueChange={(value) => updateDevelopmentProfile({ handoverStrategy: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select handover strategy" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No handover strategy</SelectItem>
                {HANDOVER_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="start-date">Start Date</Label>
            <Input id="start-date" type="date" value={form.startDate || ''} onChange={(e) => setForm((prev) => ({ ...prev, startDate: e.target.value || undefined }))} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="end-date">Target End Date</Label>
            <Input id="end-date" type="date" value={form.targetEndDate || ''} onChange={(e) => setForm((prev) => ({ ...prev, targetEndDate: e.target.value || undefined }))} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="estimated-budget">Estimated Budget</Label>
            <Input id="estimated-budget" type="number" value={form.estimatedBudget ?? ''} onChange={(e) => setForm((prev) => ({ ...prev, estimatedBudget: e.target.value ? Number(e.target.value) : undefined }))} />
          </div>
          <div className="grid gap-2">
            <Label>Funding Source</Label>
            <Select value={form.fundingSource || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, fundingSource: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select funding source" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No funding source</SelectItem>
                {fundingSourceOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Sponsor</Label>
            <Select value={form.sponsorId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, sponsorId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select sponsor" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No sponsor</SelectItem>
                {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Project Manager</Label>
            <Select value={form.projectManagerId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, projectManagerId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select project manager" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No project manager</SelectItem>
                {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Business Partner</Label>
            <Select value={form.businessPartnerId || 'none'} onValueChange={(value) => setForm((prev) => ({ ...prev, businessPartnerId: value === 'none' ? undefined : value, contractId: undefined }))}>
              <SelectTrigger><SelectValue placeholder="Select business partner" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No business partner</SelectItem>
                {availableBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{formatBusinessPartnerLabel(partner)}</SelectItem>)}
              </SelectContent>
            </Select>
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
            <Label htmlFor="land-reference">Land Reference</Label>
            <Input id="land-reference" value={form.developmentProfile?.landReference || ''} onChange={(e) => updateDevelopmentProfile({ landReference: e.target.value || undefined })} />
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
