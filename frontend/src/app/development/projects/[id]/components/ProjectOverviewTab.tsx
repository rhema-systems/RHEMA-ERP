import { type Dispatch, type SetStateAction } from 'react';
import { addMonths, format, parseISO } from 'date-fns';
import { Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowRecordPanel } from '@/components/workflow/WorkflowRecordTab';
import { ReadyLandPortionSelect } from '@/components/projects/ReadyLandPortionSelect';
import {
  type AddProjectMemberDto,
  type CreateProjectBillingScheduleDto,
  type CreateProjectBudgetRevisionDto,
  type CreateProjectForecastVersionDto,
  type CreateProjectInvoiceRequestDto,
  type ProjectBudgetRevisionDto,
  type ProjectDetailDto,
  type ProjectFinancialControlSummaryDto,
  type ProjectForecastVersionDto,
  type ProjectIntegrationSummaryDto,
  type UpdateProjectDto,
  projectService,
} from '@/services/projectService';

type NamedOption = {
  id: string;
  name: string;
};

type IdentifiedRecord = {
  id: string;
};

const DELIVERY_STRUCTURES = ['WholeDevelopment', 'SingleUnit', 'MultiUnit'];
const DEVELOPMENT_TYPES = ['Residential', 'Commercial', 'Industrial', 'MixedUse', 'Hospitality', 'Institutional', 'Infrastructure', 'Renovation'];
const PROCUREMENT_ROUTES = ['Traditional', 'DesignBuild', 'ConstructionManagement', 'DirectLabour', 'Negotiated', 'FrameworkCallOff'];
const CONTRACT_STRATEGIES = ['LumpSum', 'MeasuredWorks', 'CostPlus', 'TargetCost', 'ManagementContract', 'SubcontractPackages'];
const HANDOVER_STRATEGIES = ['SingleHandover', 'PhasedHandover', 'UnitByUnitHandover', 'ShellAndCore', 'Turnkey'];

const flattenProjectPhases = (
  phases: ProjectDetailDto['phases'],
  depth = 0,
): Array<ProjectDetailDto['phases'][number] & { depth: number }> =>
  phases.flatMap((phase) => [
    { ...phase, depth },
    ...flattenProjectPhases(phase.children || [], depth + 1),
  ]);

type ProjectOverviewTabProps = {
  project: ProjectDetailDto;
  overview: UpdateProjectDto;
  setOverview: Dispatch<SetStateAction<UpdateProjectDto>>;
  financialSummary: ProjectFinancialControlSummaryDto | null;
  integrationSummary: ProjectIntegrationSummaryDto | null;
  types: NamedOption[];
  priorities: NamedOption[];
  templates: NamedOption[];
  portfolios: NamedOption[];
  programs: NamedOption[];
  selectedPortfolioLabel: string;
  selectedProgramLabel: string;
  methodologyOptions: string[];
  fundingSourceOptions: string[];
  activeBusinessPartners: IdentifiedRecord[];
  activeContracts: IdentifiedRecord[];
  activeUsers: IdentifiedRecord[];
  member: AddProjectMemberDto;
  setMember: Dispatch<SetStateAction<AddProjectMemberDto>>;
  memberRoleOptions: string[];
  billing: CreateProjectBillingScheduleDto;
  setBilling: Dispatch<SetStateAction<CreateProjectBillingScheduleDto>>;
  billingTypeOptions: string[];
  invoice: CreateProjectInvoiceRequestDto;
  setInvoice: Dispatch<SetStateAction<CreateProjectInvoiceRequestDto>>;
  baseCurrencyCode: string;
  projectCurrencyOptions: string[];
  invoiceCurrencyOptions: string[];
  budgetRevision: CreateProjectBudgetRevisionDto;
  setBudgetRevision: Dispatch<SetStateAction<CreateProjectBudgetRevisionDto>>;
  budgetRevisions: ProjectBudgetRevisionDto[];
  forecastVersion: CreateProjectForecastVersionDto;
  setForecastVersion: Dispatch<SetStateAction<CreateProjectForecastVersionDto>>;
  forecastVersions: ProjectForecastVersionDto[];
  expandedBudgetRevisionHistoryId: string | null;
  setExpandedBudgetRevisionHistoryId: Dispatch<SetStateAction<string | null>>;
  boolValue: (value?: boolean | null) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  formatDateLabel: (value?: string) => string;
  formatCatalogLabel: (value?: string | null) => string;
  formatUserLabel: (user: IdentifiedRecord) => string;
  formatBusinessPartnerLabel: (partner: IdentifiedRecord) => string;
  formatContractLabel: (contract: IdentifiedRecord) => string;
  getResolvedUserLabel: (userId?: string, displayName?: string, fallback?: string) => string;
  getCurrencyOptionLabel: (code: string) => string;
  today: () => string;
  onLoad: () => Promise<void>;
  onOpenWorkflows: () => void;
  onPortfolioChange: (value: string) => Promise<void> | void;
  onAddMember: () => Promise<void> | void;
  onRemoveMember: (memberId: string) => Promise<void> | void;
  onAddBillingSchedule: () => Promise<void> | void;
  onInvoiceScheduleChange: (value: string) => void;
  onAddInvoiceRequest: () => Promise<void> | void;
  onSubmitInvoiceRequest: (invoiceRequestId: string) => Promise<void> | void;
  onSendInvoiceRequestToFinance: (invoiceRequest: ProjectDetailDto['invoiceRequests'][number]) => Promise<void> | void;
  onMarkInvoiceRequestInvoiced: (invoiceRequest: ProjectDetailDto['invoiceRequests'][number]) => Promise<void> | void;
  onMarkInvoiceRequestPaid: (invoiceRequestId: string) => Promise<void> | void;
  onGenerateInvoiceRequestFromSchedule: (billingScheduleId: string) => Promise<void> | void;
  onDeleteBillingSchedule: (billingScheduleId: string) => Promise<void> | void;
  onCreateBudgetRevision: () => Promise<void> | void;
  onCreateForecastVersion: () => Promise<void> | void;
  onActivateForecastVersion: (forecastVersionId: string) => Promise<void> | void;
};

export function ProjectOverviewTab({
  project,
  overview,
  setOverview,
  financialSummary,
  integrationSummary,
  types,
  priorities,
  templates,
  portfolios,
  programs,
  selectedPortfolioLabel,
  selectedProgramLabel,
  methodologyOptions,
  fundingSourceOptions,
  activeBusinessPartners,
  activeContracts,
  activeUsers,
  member,
  setMember,
  memberRoleOptions,
  billing,
  setBilling,
  billingTypeOptions,
  invoice,
  setInvoice,
  baseCurrencyCode,
  projectCurrencyOptions,
  invoiceCurrencyOptions,
  budgetRevision,
  setBudgetRevision,
  budgetRevisions,
  forecastVersion,
  setForecastVersion,
  forecastVersions,
  expandedBudgetRevisionHistoryId,
  setExpandedBudgetRevisionHistoryId,
  boolValue,
  formatMoney,
  formatDateLabel,
  formatCatalogLabel,
  formatUserLabel,
  formatBusinessPartnerLabel,
  formatContractLabel,
  getResolvedUserLabel,
  getCurrencyOptionLabel,
  today,
  onLoad,
  onOpenWorkflows,
  onPortfolioChange,
  onAddMember,
  onRemoveMember,
  onAddBillingSchedule,
  onInvoiceScheduleChange,
  onAddInvoiceRequest,
  onSubmitInvoiceRequest,
  onSendInvoiceRequestToFinance,
  onMarkInvoiceRequestInvoiced,
  onMarkInvoiceRequestPaid,
  onGenerateInvoiceRequestFromSchedule,
  onDeleteBillingSchedule,
  onCreateBudgetRevision,
  onCreateForecastVersion,
  onActivateForecastVersion,
}: ProjectOverviewTabProps) {
  const lifecyclePhases = flattenProjectPhases(project.phases);
  const showForecastVersions = false;
  const slackMonths = typeof overview.slackMonths === 'number' && Number.isFinite(overview.slackMonths)
    ? overview.slackMonths
    : project.slackMonths;
  const trueProjectEndDate = (() => {
    const targetEndDate = overview.targetEndDate || project.targetEndDate;
    if (!targetEndDate) {
      return project.trueEndDate ? String(project.trueEndDate).slice(0, 10) : '';
    }

    const parsed = parseISO(String(targetEndDate));
    if (Number.isNaN(parsed.getTime())) {
      return project.trueEndDate ? String(project.trueEndDate).slice(0, 10) : '';
    }

    return format(addMonths(parsed, slackMonths || 0), 'yyyy-MM-dd');
  })();

  const updateDevelopmentProfile = (updates: Partial<NonNullable<UpdateProjectDto['developmentProfile']>>) => {
    setOverview((current) => ({
      ...current,
      developmentProfile: {
        deliveryStructure: current.developmentProfile?.deliveryStructure || 'WholeDevelopment',
        ...current.developmentProfile,
        ...updates,
      },
    }));
  };

  return (
    <div className="space-y-6">
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-base">Progress</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{project.progressPercent}%</div></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-base">Budget</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{typeof project.estimatedBudget === 'number' ? formatMoney(project.estimatedBudget) : 'N/A'}</div></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-base">Open Risks</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{project.risks.filter((item) => item.status === 'Open').length}</div></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-base">Open Issues</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{project.issues.filter((item) => item.status === 'Open').length}</div></CardContent></Card>
      </div>

      <WorkflowRecordPanel
        entityType="Project"
        entityId={project.id}
        entityLabel="Project"
        entityNumber={project.projectCode}
        status={project.status}
        canSubmit={project.status === 'Draft'}
        canApproveReject={project.status === 'PendingApproval'}
        onSubmit={() => projectService.submitProject(project.id)}
        onApprove={(comments) => projectService.approveProject(project.id, comments)}
        onReject={(comments) => projectService.rejectProject(project.id, comments || 'Rejected', comments)}
        onAfterAction={onLoad}
        onOpenWorkflows={onOpenWorkflows}
      />

      <Card>
        <CardHeader>
          <CardTitle>Construction Foundation</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-4">
            <div className="flex flex-wrap gap-2">
              <Badge variant="outline">{formatCatalogLabel(project.developmentProfile?.deliveryStructure || overview.developmentProfile?.deliveryStructure || 'WholeDevelopment')}</Badge>
              {project.developmentProfile?.developmentType || overview.developmentProfile?.developmentType ? (
                <Badge variant="outline">{project.developmentProfile?.developmentType || overview.developmentProfile?.developmentType}</Badge>
              ) : null}
              {project.developmentProfile?.procurementRoute || overview.developmentProfile?.procurementRoute ? (
                <Badge variant="secondary">{formatCatalogLabel(project.developmentProfile?.procurementRoute || overview.developmentProfile?.procurementRoute)}</Badge>
              ) : null}
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Site</div>
                <div className="mt-1 text-base font-semibold">{project.developmentProfile?.siteName || overview.developmentProfile?.siteName || 'Not set'}</div>
                {project.developmentProfile?.siteAddress || overview.developmentProfile?.siteAddress ? (
                  <div className="mt-1 text-sm text-muted-foreground">{project.developmentProfile?.siteAddress || overview.developmentProfile?.siteAddress}</div>
                ) : null}
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Contract Strategy</div>
                <div className="mt-1 text-base font-semibold">{formatCatalogLabel(project.developmentProfile?.contractStrategy || overview.developmentProfile?.contractStrategy || 'Not set')}</div>
                <div className="mt-1 text-sm text-muted-foreground">Handover: {formatCatalogLabel(project.developmentProfile?.handoverStrategy || overview.developmentProfile?.handoverStrategy || 'Not set')}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Consultant Team</div>
                <div className="mt-1 text-base font-semibold">{project.developmentProfile?.consultantTeam || overview.developmentProfile?.consultantTeam || 'Not set'}</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Funding Arrangement</div>
                <div className="mt-1 text-base font-semibold">{project.developmentProfile?.fundingArrangement || overview.developmentProfile?.fundingArrangement || 'Not set'}</div>
                {project.developmentProfile?.landReference || overview.developmentProfile?.landReference ? (
                  <div className="mt-1 text-sm text-muted-foreground">Land Ref: {project.developmentProfile?.landReference || overview.developmentProfile?.landReference}</div>
                ) : null}
              </div>
              <div className="rounded-lg border p-4">
                <div className="text-sm text-muted-foreground">Project Base Currency</div>
                <div className="mt-1 text-base font-semibold">{getCurrencyOptionLabel(overview.baseCurrencyCode || project.baseCurrencyCode || baseCurrencyCode)}</div>
                <div className="mt-1 text-sm text-muted-foreground">Used as the project reporting and default transaction currency.</div>
              </div>
            </div>
          </div>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <div>
                <div className="text-sm font-medium">Lifecycle Phases</div>
                <div className="text-sm text-muted-foreground">{lifecyclePhases.length} configured phase{lifecyclePhases.length === 1 ? '' : 's'}</div>
              </div>
            </div>
            {lifecyclePhases.length === 0 ? (
              <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
                No project phases are configured yet. A template or construction profile will seed them here.
              </div>
            ) : (
              <div className="space-y-2">
                {lifecyclePhases.map((phase) => (
                  <div key={phase.id} className="flex items-center justify-between rounded-lg border p-3" style={{ marginLeft: phase.depth * 12 }}>
                    <div>
                      <div className="font-medium">{phase.name}</div>
                      <div className="text-xs text-muted-foreground">
                        {phase.code ? `${phase.code} | ` : ''}{phase.isStageGateRequired ? 'Stage gate required' : 'Open transition'}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {phase.isOptional ? <Badge variant="outline">Optional</Badge> : null}
                      <Badge variant={phase.status === 'Completed' ? 'secondary' : 'outline'}>{formatCatalogLabel(phase.status)}</Badge>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Financial Control Snapshot</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {financialSummary ? (
              <>
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Budget Baseline</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.budgetBaseline)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Actual Cost</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.actualCost)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Committed Cost</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.committedCost)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Pending Cost</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.pendingCost)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Procurement Requested</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.procurementRequestedAmount)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Open Procurement Commitment</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.procurementOpenCommitmentAmount)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Planned Value</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.plannedValue)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Earned Value</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.earnedValue)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Estimate at Completion</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.estimateAtCompletion)}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Total Exposure</div><div className="mt-1 text-2xl font-semibold">{formatMoney(financialSummary.totalExposureAmount)}</div></div>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant={financialSummary.thresholdExceeded ? 'destructive' : 'secondary'}>
                    {financialSummary.thresholdExceeded ? 'Threshold exceeded' : 'Within threshold'}
                  </Badge>
                  <Badge variant="outline">{financialSummary.healthStatus}</Badge>
                  <span className="text-sm text-muted-foreground">Consumption {financialSummary.budgetConsumptionPercent}%</span>
                  <span className="text-sm text-muted-foreground">CPI {financialSummary.costPerformanceIndex?.toFixed(2) ?? 'N/A'} / SPI {financialSummary.schedulePerformanceIndex?.toFixed(2) ?? 'N/A'}</span>
                  <span className="text-sm text-muted-foreground">Warn {financialSummary.thresholdWarningPercent}% / Critical {financialSummary.thresholdCriticalPercent}%</span>
                </div>
                <div className="flex flex-wrap gap-2">
                  {financialSummary.currentBudgetRevisionName ? <Badge variant="outline">{financialSummary.currentBudgetRevisionName}</Badge> : null}
                  {financialSummary.activeForecastVersionName ? <Badge variant="outline">{financialSummary.activeForecastVersionName}</Badge> : null}
                  <Badge variant="outline">{financialSummary.budgetRevisionCount} budget revision(s)</Badge>
                  <Badge variant="outline">{financialSummary.forecastVersionCount} forecast version(s)</Badge>
                </div>
                <div className="space-y-2">
                  {financialSummary.alerts.length === 0 ? <div className="text-sm text-muted-foreground">No financial alerts are currently triggered.</div> : null}
                  {financialSummary.alerts.map((alert, index) => (
                    <div key={`${alert.severity}-${index}`} className="rounded-lg border p-4 text-sm">
                      <div className="font-medium">{alert.severity}</div>
                      <div className="text-muted-foreground">{alert.message}</div>
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <div className="text-sm text-muted-foreground">Financial details are not available for this user.</div>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Integration Snapshot</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {integrationSummary ? (
              <>
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Business Partner</div><div className="mt-1 text-lg font-semibold">{integrationSummary.hasBusinessPartner ? 'Linked' : 'Not linked'}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Contract</div><div className="mt-1 text-lg font-semibold">{integrationSummary.hasContract ? 'Linked' : 'Not linked'}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Assets</div><div className="mt-1 text-lg font-semibold">{integrationSummary.linkedAssetCount}</div></div>
                  <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Access Policies</div><div className="mt-1 text-lg font-semibold">{integrationSummary.sharedExternalPolicyCount}</div></div>
                </div>
                <div className="space-y-2">
                  {integrationSummary.links.slice(0, 6).map((link, index) => (
                    <div key={`${link.linkType}-${link.reference}-${index}`} className="rounded-lg border p-4 text-sm">
                      <div className="font-medium">{link.linkType}</div>
                      <div className="text-muted-foreground">{link.status} | {link.reference}</div>
                    </div>
                  ))}
                  {integrationSummary.links.length === 0 ? <div className="text-sm text-muted-foreground">No integration links are available.</div> : null}
                </div>
                {integrationSummary.warnings.length ? (
                  <div className="space-y-2">
                    {integrationSummary.warnings.map((warning, index) => (
                      <div key={`${warning}-${index}`} className="rounded-lg border p-4 text-sm text-muted-foreground">{warning}</div>
                    ))}
                  </div>
                ) : null}
              </>
            ) : (
              <div className="text-sm text-muted-foreground">Integration details are not available for this user.</div>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Project Details</CardTitle></CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={overview.title || ''} onChange={(event) => setOverview((current) => ({ ...current, title: event.target.value }))} /></div>
          <div className="grid gap-2"><Label>Type</Label><Select value={overview.projectTypeId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, projectTypeId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No type</SelectItem>{types.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Priority</Label><Select value={overview.projectPriorityId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, projectPriorityId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No priority</SelectItem>{priorities.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Template</Label><Select value={overview.templateId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, templateId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No template</SelectItem>{templates.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Portfolio</Label><Select value={overview.portfolioId || 'none'} onValueChange={onPortfolioChange}><SelectTrigger><span className={overview.portfolioId ? '' : 'text-muted-foreground'}>{selectedPortfolioLabel}</span></SelectTrigger><SelectContent><SelectItem value="none">No portfolio</SelectItem>{portfolios.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2"><Label>Program</Label><Select value={overview.programId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, programId: value === 'none' ? undefined : value }))}><SelectTrigger><span className={overview.programId ? '' : 'text-muted-foreground'}>{selectedProgramLabel}</span></SelectTrigger><SelectContent><SelectItem value="none">No program</SelectItem>{programs.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent></Select></div>
          <div className="grid gap-2">
            <Label>Methodology</Label>
            <Select value={overview.methodology || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, methodology: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select methodology" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No methodology</SelectItem>
                {methodologyOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Delivery Structure</Label>
            <Select value={overview.developmentProfile?.deliveryStructure || 'WholeDevelopment'} onValueChange={(value) => updateDevelopmentProfile({ deliveryStructure: value })}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>{DELIVERY_STRUCTURES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Development Type</Label>
            <Select value={overview.developmentProfile?.developmentType || 'none'} onValueChange={(value) => updateDevelopmentProfile({ developmentType: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select development type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No development type</SelectItem>
                {DEVELOPMENT_TYPES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2"><Label>Site Name</Label><Input value={overview.developmentProfile?.siteName || ''} onChange={(event) => updateDevelopmentProfile({ siteName: event.target.value || undefined })} /></div>
          <div className="grid gap-2">
            <Label>Procurement Route</Label>
            <Select value={overview.developmentProfile?.procurementRoute || 'none'} onValueChange={(value) => updateDevelopmentProfile({ procurementRoute: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select procurement route" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No procurement route</SelectItem>
                {PROCUREMENT_ROUTES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Contract Strategy</Label>
            <Select value={overview.developmentProfile?.contractStrategy || 'none'} onValueChange={(value) => updateDevelopmentProfile({ contractStrategy: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select contract strategy" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No contract strategy</SelectItem>
                {CONTRACT_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Handover Strategy</Label>
            <Select value={overview.developmentProfile?.handoverStrategy || 'none'} onValueChange={(value) => updateDevelopmentProfile({ handoverStrategy: value === 'none' ? undefined : value })}>
              <SelectTrigger><SelectValue placeholder="Select handover strategy" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No handover strategy</SelectItem>
                {HANDOVER_STRATEGIES.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2"><Label>Start Date</Label><Input type="date" value={overview.startDate ? String(overview.startDate).slice(0, 10) : ''} onChange={(event) => setOverview((current) => ({ ...current, startDate: event.target.value || undefined }))} /></div>
          <div className="grid gap-2"><Label>Target End Date</Label><Input type="date" value={overview.targetEndDate ? String(overview.targetEndDate).slice(0, 10) : ''} onChange={(event) => setOverview((current) => ({ ...current, targetEndDate: event.target.value || undefined }))} /></div>
          <div className="grid gap-2">
            <Label>Slack / Delay (Months)</Label>
            <Input
              type="number"
              min={0}
              value={slackMonths}
              onChange={(event) => setOverview((current) => ({ ...current, slackMonths: Math.max(0, Number(event.target.value || '0')) }))}
            />
            <div className="text-xs text-muted-foreground">Adds buffer after the target end date to derive the true project finish.</div>
          </div>
          <div className="grid gap-2">
            <Label>True Project End</Label>
            <Input type="date" value={trueProjectEndDate} readOnly className="bg-slate-50 text-slate-700" />
            <div className="text-xs text-muted-foreground">Calculated as target end date plus the configured slack months.</div>
          </div>
          <div className="grid gap-2"><Label>Estimated Budget</Label><Input type="number" value={overview.estimatedBudget ?? ''} onChange={(event) => setOverview((current) => ({ ...current, estimatedBudget: event.target.value ? Number(event.target.value) : undefined }))} /></div>
          <div className="grid gap-2">
            <Label>Project Base Currency</Label>
            <Select value={overview.baseCurrencyCode || project.baseCurrencyCode || baseCurrencyCode} onValueChange={(value) => setOverview((current) => ({ ...current, baseCurrencyCode: value }))}>
              <SelectTrigger><SelectValue placeholder="Select project base currency" /></SelectTrigger>
              <SelectContent>
                {projectCurrencyOptions.map((item) => <SelectItem key={item} value={item}>{getCurrencyOptionLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2"><Label>Approved Budget</Label><Input type="number" value={overview.approvedBudget ?? ''} onChange={(event) => setOverview((current) => ({ ...current, approvedBudget: event.target.value ? Number(event.target.value) : undefined }))} /></div>
          <div className="grid gap-2"><Label>Actual Cost</Label><Input type="number" value={overview.actualCost ?? ''} onChange={(event) => setOverview((current) => ({ ...current, actualCost: event.target.value ? Number(event.target.value) : undefined }))} /></div>
          <div className="grid gap-2">
            <Label>Funding Source</Label>
            <Select value={overview.fundingSource || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, fundingSource: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select funding source" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No funding source</SelectItem>
                {fundingSourceOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Business Partner</Label>
            <Select value={overview.businessPartnerId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, businessPartnerId: value === 'none' ? undefined : value, contractId: undefined }))}>
              <SelectTrigger><SelectValue placeholder="Select business partner" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No business partner</SelectItem>
                {activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{formatBusinessPartnerLabel(partner)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Contract</Label>
            <Select value={overview.contractId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, contractId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select contract" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No contract</SelectItem>
                {activeContracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{formatContractLabel(contract)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Sponsor</Label>
            <Select value={overview.sponsorId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, sponsorId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select sponsor" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No sponsor</SelectItem>
                {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Project Manager</Label>
            <Select value={overview.projectManagerId || 'none'} onValueChange={(value) => setOverview((current) => ({ ...current, projectManagerId: value === 'none' ? undefined : value }))}>
              <SelectTrigger><SelectValue placeholder="Select project manager" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No project manager</SelectItem>
                {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2"><Label>Portal Access</Label><Select value={boolValue(overview.externalPortalAccessEnabled)} onValueChange={(value) => setOverview((current) => ({ ...current, externalPortalAccessEnabled: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Internal only</SelectItem><SelectItem value="true">Shared externally</SelectItem></SelectContent></Select></div>
          <div className="grid gap-2"><Label>Portal Collaboration</Label><Select value={boolValue(overview.externalCollaborationEnabled)} onValueChange={(value) => setOverview((current) => ({ ...current, externalCollaborationEnabled: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Read only</SelectItem><SelectItem value="true">Allow updates</SelectItem></SelectContent></Select></div>
          <div className="grid gap-2 md:col-span-2"><Label>Summary</Label><Textarea rows={3} value={overview.summary || ''} onChange={(event) => setOverview((current) => ({ ...current, summary: event.target.value }))} /></div>
          <div className="grid gap-2 md:col-span-2"><Label>Objectives</Label><Textarea rows={3} value={overview.objectives || ''} onChange={(event) => setOverview((current) => ({ ...current, objectives: event.target.value }))} /></div>
          <div className="grid gap-2 md:col-span-2"><Label>Site Address</Label><Textarea rows={2} value={overview.developmentProfile?.siteAddress || ''} onChange={(event) => updateDevelopmentProfile({ siteAddress: event.target.value || undefined })} /></div>
          <ReadyLandPortionSelect
            projectId={project.id}
            value={overview.developmentProfile?.landReference}
            onValueChange={(landReference) =>
              updateDevelopmentProfile({ landReference })
            }
          />
          <div className="grid gap-2"><Label>Funding Arrangement</Label><Input value={overview.developmentProfile?.fundingArrangement || ''} onChange={(event) => updateDevelopmentProfile({ fundingArrangement: event.target.value || undefined })} /></div>
          <div className="grid gap-2 md:col-span-2"><Label>Consultant Team</Label><Textarea rows={2} value={overview.developmentProfile?.consultantTeam || ''} onChange={(event) => updateDevelopmentProfile({ consultantTeam: event.target.value || undefined })} /></div>
          <div className="grid gap-2 md:col-span-2"><Label>Construction Notes</Label><Textarea rows={3} value={overview.developmentProfile?.notes || ''} onChange={(event) => updateDevelopmentProfile({ notes: event.target.value || undefined })} /></div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Members</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-[1fr_1fr_auto]">
            <div className="grid gap-2">
              <Label>Member</Label>
              <Select value={member.userId || 'none'} onValueChange={(value) => setMember((current) => ({ ...current, userId: value === 'none' ? '' : value }))}>
                <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No user</SelectItem>
                  {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Role</Label>
              <Select value={member.role || memberRoleOptions[0]} onValueChange={(value) => setMember((current) => ({ ...current, role: value }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{memberRoleOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="flex items-end"><Button onClick={onAddMember}><Plus className="mr-2 h-4 w-4" />Add</Button></div>
          </div>
          {project.members.map((item) => <div key={item.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{getResolvedUserLabel(item.userId, item.userDisplayName)}</div><div className="text-sm text-muted-foreground">{item.role}</div></div><Button variant="ghost" size="sm" onClick={() => onRemoveMember(item.id)}><Trash2 className="h-4 w-4" /></Button></div>)}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Billing</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={billing.name} onChange={(event) => setBilling((current) => ({ ...current, name: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Type</Label><Select value={billing.billingType} onValueChange={(value) => setBilling((current) => ({ ...current, billingType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{billingTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Amount</Label><Input type="number" value={billing.amount} onChange={(event) => setBilling((current) => ({ ...current, amount: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Billing Date</Label><Input type="date" value={String(billing.billingDate).slice(0, 10)} onChange={(event) => setBilling((current) => ({ ...current, billingDate: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Milestone</Label><Select value={billing.milestoneId || 'none'} onValueChange={(value) => setBilling((current) => ({ ...current, milestoneId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No milestone</SelectItem>{project.milestones.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2 md:col-span-2"><Label>Description</Label><Input value={billing.description || ''} onChange={(event) => setBilling((current) => ({ ...current, description: event.target.value }))} /></div>
            <div className="flex items-end"><Button onClick={onAddBillingSchedule}><Plus className="mr-2 h-4 w-4" />Add Schedule</Button></div>
          </div>
          <div className="grid gap-4 md:grid-cols-[1fr_1fr_1fr_auto]">
            <div className="grid gap-2"><Label>Schedule</Label><Select value={invoice.billingScheduleId || 'none'} onValueChange={onInvoiceScheduleChange}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Manual invoice</SelectItem>{project.billingSchedules.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Requested Amount</Label><Input type="number" value={invoice.requestedAmount} onChange={(event) => setInvoice((current) => ({ ...current, requestedAmount: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2">
              <Label>Currency</Label>
              <Select value={invoice.currency || invoiceCurrencyOptions[0] || baseCurrencyCode} onValueChange={(value) => setInvoice((current) => ({ ...current, currency: value }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {invoiceCurrencyOptions.map((code) => (
                    <SelectItem key={code} value={code}>
                      {getCurrencyOptionLabel(code)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-end"><Button onClick={onAddInvoiceRequest}><Plus className="mr-2 h-4 w-4" />Add Invoice</Button></div>
          </div>
          <div className="space-y-3">
            {project.billingSchedules.map((item) => <div key={item.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{item.name}</div><div className="text-sm text-muted-foreground">{item.billingType} | {formatMoney(item.amount)} | {formatDateLabel(item.billingDate)}</div></div><div className="flex gap-2"><Button variant="outline" size="sm" onClick={() => onGenerateInvoiceRequestFromSchedule(item.id)}>Generate Invoice</Button><Button variant="ghost" size="sm" onClick={() => onDeleteBillingSchedule(item.id)}><Trash2 className="h-4 w-4" /></Button></div></div>)}
            {project.invoiceRequests.map((item) => (
              <div key={item.id} className="rounded-lg border p-4">
                <div className="flex items-center justify-between gap-3">
                  <div className="font-medium">{item.requestNumber}</div>
                  <Badge variant={item.status === 'Paid' ? 'secondary' : item.status === 'Invoiced' ? 'secondary' : item.status === 'SentToFinance' ? 'secondary' : 'outline'}>{item.status}</Badge>
                </div>
                <div className="mt-1 text-sm text-muted-foreground">{formatMoney(item.requestedAmount, item.currency)} | {format(new Date(item.requestedAt), 'MMM dd, yyyy HH:mm')}</div>
                {item.externalReference ? <div className="mt-1 text-xs text-muted-foreground">Reference: {item.externalReference}</div> : null}
                <div className="mt-3 flex flex-wrap gap-2">
                  {item.status === 'Draft' ? <Button variant="outline" size="sm" onClick={() => onSubmitInvoiceRequest(item.id)}>Submit</Button> : null}
                  {(item.status === 'Draft' || item.status === 'Submitted') ? <Button variant="outline" size="sm" onClick={() => onSendInvoiceRequestToFinance(item)}>Send to Finance</Button> : null}
                  {(item.status === 'Submitted' || item.status === 'SentToFinance') ? <Button variant="outline" size="sm" onClick={() => onMarkInvoiceRequestInvoiced(item)}>Mark Invoiced</Button> : null}
                  {(item.status === 'SentToFinance' || item.status === 'Invoiced') ? <Button variant="outline" size="sm" onClick={() => onMarkInvoiceRequestPaid(item.id)}>Mark Paid</Button> : null}
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Budget Revisions</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={budgetRevision.revisionName} onChange={(event) => setBudgetRevision((current) => ({ ...current, revisionName: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Type</Label><Select value={budgetRevision.revisionType || 'Revision'} onValueChange={(value) => setBudgetRevision((current) => ({ ...current, revisionType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Revision', 'Increase', 'Decrease', 'Baseline', 'ForecastAdjustment'].map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div>
            <div className="grid gap-2"><Label>Effective Date</Label><Input type="date" value={budgetRevision.effectiveDate || today()} onChange={(event) => setBudgetRevision((current) => ({ ...current, effectiveDate: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Estimated</Label><Input type="number" value={budgetRevision.estimatedBudget} onChange={(event) => setBudgetRevision((current) => ({ ...current, estimatedBudget: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Approved</Label><Input type="number" value={budgetRevision.approvedBudget} onChange={(event) => setBudgetRevision((current) => ({ ...current, approvedBudget: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Committed</Label><Input type="number" value={budgetRevision.committedCost} onChange={(event) => setBudgetRevision((current) => ({ ...current, committedCost: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Forecast</Label><Input type="number" value={budgetRevision.forecastCost} onChange={(event) => setBudgetRevision((current) => ({ ...current, forecastCost: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Warn %</Label><Input type="number" value={budgetRevision.thresholdWarningPercent ?? 75} onChange={(event) => setBudgetRevision((current) => ({ ...current, thresholdWarningPercent: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2"><Label>Critical %</Label><Input type="number" value={budgetRevision.thresholdCriticalPercent ?? 90} onChange={(event) => setBudgetRevision((current) => ({ ...current, thresholdCriticalPercent: Number(event.target.value || '0') }))} /></div>
            <div className="grid gap-2 md:col-span-2"><Label>Reason</Label><Input value={budgetRevision.changeReason || ''} onChange={(event) => setBudgetRevision((current) => ({ ...current, changeReason: event.target.value }))} /></div>
            <div className="grid gap-2 md:col-span-3"><Label>Notes</Label><Textarea rows={2} value={budgetRevision.notes || ''} onChange={(event) => setBudgetRevision((current) => ({ ...current, notes: event.target.value }))} /></div>
            <div className="flex items-end"><Button onClick={onCreateBudgetRevision}><Plus className="mr-2 h-4 w-4" />Create Revision</Button></div>
          </div>
          <div className="space-y-3">
            {budgetRevisions.length === 0 ? <div className="text-sm text-muted-foreground">No budget revisions have been created yet.</div> : null}
            {budgetRevisions.map((item) => (
              <div key={item.id} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.revisionName}</div>
                      <Badge variant="outline">v{item.versionNumber}</Badge>
                      <Badge variant={item.status === 'Approved' ? 'secondary' : item.status === 'PendingApproval' ? 'default' : 'outline'}>{item.status}</Badge>
                    </div>
                    <div className="text-sm text-muted-foreground">{item.revisionType} | approved {formatMoney(item.approvedBudget)} | committed {formatMoney(item.committedCost)} | forecast {formatMoney(item.forecastCost)}</div>
                    <div className="text-sm text-muted-foreground">Warn {item.thresholdWarningPercent}% / Critical {item.thresholdCriticalPercent}% | effective {formatDateLabel(item.effectiveDate)}</div>
                    {item.changeReason ? <div className="text-sm text-muted-foreground">{item.changeReason}</div> : null}
                    {item.rejectionReason ? <div className="text-sm text-red-600">{item.rejectionReason}</div> : null}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <WorkflowApprovalActions
                      entityType="ProjectBudgetRevision"
                      entityId={item.id}
                      entityLabel="Budget Revision"
                      entityNumber={item.revisionName}
                      status={item.status}
                      loadWorkflowSummary
                      showStepBadge
                      canSubmit={item.status === 'Draft' || item.status === 'Rejected'}
                      canApproveReject={item.status === 'PendingApproval'}
                      onSubmit={async () => { await projectService.submitBudgetRevision(item.id); }}
                      onApprove={async (comments) => { await projectService.approveBudgetRevision(item.id, comments); }}
                      onReject={async (comments) => { await projectService.rejectBudgetRevision(item.id, comments || 'Rejected', comments); }}
                      onAfterAction={onLoad}
                      onOpenWorkflows={onOpenWorkflows}
                    />
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setExpandedBudgetRevisionHistoryId((current) => current === item.id ? null : item.id)}
                    >
                      {expandedBudgetRevisionHistoryId === item.id ? 'Hide Approval History' : 'View Approval History'}
                    </Button>
                  </div>
                </div>
                {expandedBudgetRevisionHistoryId === item.id ? (
                  <div className="mt-4">
                    <WorkflowRecordPanel
                      entityType="ProjectBudgetRevision"
                      entityId={item.id}
                      entityLabel="Budget Revision"
                      entityNumber={item.revisionName}
                      status={item.status}
                      canSubmit={item.status === 'Draft' || item.status === 'Rejected'}
                      canApproveReject={item.status === 'PendingApproval'}
                      onSubmit={async () => { await projectService.submitBudgetRevision(item.id); }}
                      onApprove={async (comments) => { await projectService.approveBudgetRevision(item.id, comments); }}
                      onReject={async (comments) => { await projectService.rejectBudgetRevision(item.id, comments || 'Rejected', comments); }}
                      onAfterAction={onLoad}
                      onOpenWorkflows={onOpenWorkflows}
                    />
                  </div>
                ) : null}
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {showForecastVersions ? (
        <Card>
          <CardHeader><CardTitle>Forecast Versions</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-4">
              <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={forecastVersion.versionName} onChange={(event) => setForecastVersion((current) => ({ ...current, versionName: event.target.value }))} /></div>
              <div className="grid gap-2"><Label>As Of</Label><Input type="date" value={forecastVersion.asOfDate || today()} onChange={(event) => setForecastVersion((current) => ({ ...current, asOfDate: event.target.value }))} /></div>
              <div className="grid gap-2"><Label>Activate</Label><Select value={boolValue(forecastVersion.isActive)} onValueChange={(value) => setForecastVersion((current) => ({ ...current, isActive: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="true">Yes</SelectItem><SelectItem value="false">No</SelectItem></SelectContent></Select></div>
              <div className="grid gap-2"><Label>Forecast Cost</Label><Input type="number" value={forecastVersion.forecastCost} onChange={(event) => setForecastVersion((current) => ({ ...current, forecastCost: Number(event.target.value || '0') }))} /></div>
              <div className="grid gap-2"><Label>EAC</Label><Input type="number" value={forecastVersion.estimateAtCompletion} onChange={(event) => setForecastVersion((current) => ({ ...current, estimateAtCompletion: Number(event.target.value || '0') }))} /></div>
              <div className="grid gap-2"><Label>Revenue</Label><Input type="number" value={forecastVersion.forecastRevenue} onChange={(event) => setForecastVersion((current) => ({ ...current, forecastRevenue: Number(event.target.value || '0') }))} /></div>
              <div className="grid gap-2"><Label>Margin</Label><Input type="number" value={forecastVersion.forecastMargin} onChange={(event) => setForecastVersion((current) => ({ ...current, forecastMargin: Number(event.target.value || '0') }))} /></div>
              <div className="grid gap-2 md:col-span-3"><Label>Notes</Label><Textarea rows={2} value={forecastVersion.notes || ''} onChange={(event) => setForecastVersion((current) => ({ ...current, notes: event.target.value }))} /></div>
              <div className="flex items-end"><Button onClick={onCreateForecastVersion}><Plus className="mr-2 h-4 w-4" />Create Forecast</Button></div>
            </div>
            <div className="space-y-3">
              {forecastVersions.length === 0 ? <div className="text-sm text-muted-foreground">No forecast versions have been created yet.</div> : null}
              {forecastVersions.map((item) => (
                <div key={item.id} className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <div className="font-medium">{item.versionName}</div>
                      <Badge variant="outline">v{item.versionNumber}</Badge>
                      {item.isActive ? <Badge>Active</Badge> : null}
                    </div>
                    <div className="text-sm text-muted-foreground">{formatDateLabel(item.asOfDate)} | forecast {formatMoney(item.forecastCost)} | EAC {formatMoney(item.estimateAtCompletion)} | revenue {formatMoney(item.forecastRevenue)}</div>
                  </div>
                  {!item.isActive ? <Button variant="outline" size="sm" onClick={() => onActivateForecastVersion(item.id)}>Activate</Button> : null}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
