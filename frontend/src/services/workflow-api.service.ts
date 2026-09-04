import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';
import type { ApiResponse } from '../types';
import type {
  WorkflowDefinitionAdminDto,
  CreateWorkflowDefinitionAdminDto,
  UpdateWorkflowDefinitionAdminDto,
  WorkflowDefinitionDto,
  WorkflowDefinitionVersionDto,
  WorkflowDefinitionComparisonDto,
  WorkflowDefinitionFilterDto,
  WorkflowInstanceFilterDto,
  WorkflowStatusDto,
  WorkflowEntitySummaryDto,
  WorkflowEntityAuditDto,
  WorkflowSummaryDto,
  WorkflowValidationResult,
  WorkflowExecutionResult,
  WorkflowStepInstance,
  WorkflowApproval,
  StartWorkflowRequest,
  ExecuteStepRequest,
  CancelWorkflowRequest,
  RecallWorkflowRequest,
  ProcessStepRequest,
  AssignStepRequest,
  ProcessApprovalRequest,
  WorkflowInstance,
  WorkflowVariableInfo,
  WorkflowEntityTypeInfo,
  WorkflowModuleConformanceReport,
  WorkflowApprovalPolicySetDto,
  SaveWorkflowApprovalPolicyRequest,
  WorkflowDirectoryUser,
  WorkflowDelegationDto,
  WorkflowDelegationScopeOptionsDto,
  SaveWorkflowDelegationRequest,
  WorkflowWorkingCalendarDto,
  WorkflowCorrectionRequestDto,
  WorkflowEvidencePolicyDto,
  WorkflowEvidenceDocumentDto,
  WorkflowEvidenceReviewInstanceDto,
  WorkflowSlaBreachesDto,
  WorkflowSignatureSubmissionDto,
  WorkflowApprovalChecklistResponseDto,
  WorkflowTaskAttachmentDto
} from '../types/workflow';

type ApiPagedResponse<T> = ApiResponse<T> & {
  metadata?: {
    totalCount?: number;
    page?: number;
    pageSize?: number;
  };
};

type WorkflowEvidenceResponse = Omit<
  WorkflowEvidenceDocumentDto,
  'verificationStatus' | 'malwareScanStatus'
> & {
  verificationStatus: number | string;
  malwareScanStatus: number | string;
};

// ASP.NET serializes enums by name; consumers use the numeric workflow enums.
const normalizeEvidenceStatus = (value: number | string, names: string[]): number => {
  const named = typeof value === 'string' ? names.indexOf(value.trim().toLowerCase()) : -1;
  if (named >= 0) return named;
  const numeric = typeof value === 'string' && !value.trim() ? NaN : Number(value);
  return Number.isInteger(numeric) && numeric >= 0 && numeric < names.length ? numeric : -1;
};

/**
 * Service for handling workflow-related API operations
 */
export class WorkflowApiService {
  private readonly basePath = '/workflow';

  private requireData<T>(response: ApiResponse<T>, errorMessage: string): T {
    if (response.data === undefined || response.data === null) {
      throw new Error(errorMessage);
    }

    return response.data;
  }

  // Workflow Definition Management

  /**
   * Gets all workflow definitions with optional filtering and pagination
   */
  async getWorkflowDefinitions(filter: WorkflowDefinitionFilterDto): Promise<{
    data: WorkflowDefinitionAdminDto[];
    totalCount: number;
    page: number;
    pageSize: number;
  }> {
    const params = new URLSearchParams();
    params.append('page', filter.page.toString());
    params.append('pageSize', filter.pageSize.toString());
    params.append('sortBy', filter.sortBy);
    params.append('sortDescending', filter.sortDescending.toString());
    
    if (filter.searchTerm) params.append('searchTerm', filter.searchTerm);
    if (filter.entityType) params.append('entityType', filter.entityType);
    if (filter.isActive !== undefined) params.append('isActive', filter.isActive.toString());
    if (filter.createdAfter) params.append('createdAfter', filter.createdAfter.toISOString());
    if (filter.createdBefore) params.append('createdBefore', filter.createdBefore.toISOString());

    const response = await apiService.get<ApiPagedResponse<WorkflowDefinitionAdminDto[]>>(
      `${this.basePath}/definitions?${params.toString()}`
    );

    return {
      data: response.data || [],
      totalCount: response.metadata?.totalCount || 0,
      page: response.metadata?.page || 1,
      pageSize: response.metadata?.pageSize || 25
    };
  }

  /**
   * Gets a specific workflow definition by ID
   */
  async getWorkflowDefinition(id: string): Promise<WorkflowDefinitionDto> {
    const response = await apiService.get<ApiResponse<WorkflowDefinitionDto>>(
      `${this.basePath}/definitions/${id}`
    );
    return this.requireData(response, 'Workflow definition response did not include data.');
  }

  /**
   * Creates a new workflow definition
   */
  async createWorkflowDefinition(createDto: CreateWorkflowDefinitionAdminDto): Promise<WorkflowDefinitionDto> {
    const response = await apiService.post<ApiResponse<WorkflowDefinitionDto>>(
      `${this.basePath}/definitions`,
      createDto
    );
    return this.requireData(response, 'Workflow definition creation response did not include data.');
  }

  /**
   * Updates an existing workflow definition
   */
  async updateWorkflowDefinition(id: string, updateDto: UpdateWorkflowDefinitionAdminDto): Promise<WorkflowDefinitionDto> {
    const response = await apiService.put<ApiResponse<WorkflowDefinitionDto>>(
      `${this.basePath}/definitions/${id}`,
      updateDto
    );
    return this.requireData(response, 'Workflow definition update response did not include data.');
  }

  /**
   * Deletes a workflow definition
   */
  async deleteWorkflowDefinition(id: string): Promise<void> {
    await apiService.delete<ApiResponse<void>>(
      `${this.basePath}/definitions/${id}`
    );
  }

  /**
   * Validates a workflow definition structure
   */
  async validateWorkflowDefinition(id: string): Promise<WorkflowValidationResult> {
    const response = await apiService.post<ApiResponse<WorkflowValidationResult>>(
      `${this.basePath}/definitions/${id}/validate`
    );
    return this.requireData(response, 'Workflow validation response did not include data.');
  }

  /**
   * Gets available condition variables for a given entity type
   */
  async getWorkflowVariables(entityType: string): Promise<WorkflowVariableInfo[]> {
    const params = new URLSearchParams();
    params.append('entityType', entityType);
    const response = await apiService.get<ApiResponse<WorkflowVariableInfo[]>>(
      `${this.basePath}/variables?${params.toString()}`
    );
    return response.data || [];
  }

  /**
   * Gets available workflow entity types
   */
  async getWorkflowEntityTypes(): Promise<WorkflowEntityTypeInfo[]> {
    const response = await apiService.get<ApiResponse<WorkflowEntityTypeInfo[]>>(
      `${this.basePath}/entity-types`
    );
    return response.data || [];
  }

  /**
   * Seeds common workflow entity types for the current tenant
   */
  async seedWorkflowEntityTypes(): Promise<WorkflowEntityTypeInfo[]> {
    const response = await apiService.post<ApiResponse<WorkflowEntityTypeInfo[]>>(
      `${this.basePath}/entity-types/seed`
    );
    return response.data || [];
  }

  /**
   * Ensures workflow entity types exist for both fresh and older tenants.
   * The seed endpoint is idempotent and fills missing defaults without
   * replacing custom entity types.
   */
  async ensureWorkflowEntityTypes(): Promise<WorkflowEntityTypeInfo[]> {
    const existing = await this.getWorkflowEntityTypes();

    try {
      const seeded = await this.seedWorkflowEntityTypes();
      return seeded.length > 0 ? seeded : existing;
    } catch {
      return existing;
    }
  }

  // Workflow Instance Management

  /**
   * Gets workflow instances with optional filtering and pagination
   */
  async getWorkflowInstances(filter: WorkflowInstanceFilterDto): Promise<{
    data: WorkflowStatusDto[];
    totalCount: number;
    page: number;
    pageSize: number;
  }> {
    const params = new URLSearchParams();
    params.append('page', filter.page.toString());
    params.append('pageSize', filter.pageSize.toString());
    params.append('sortBy', filter.sortBy);
    params.append('sortDescending', filter.sortDescending.toString());
    
    if (filter.workflowDefinitionId) params.append('workflowDefinitionId', filter.workflowDefinitionId);
    if (filter.entityType) params.append('entityType', filter.entityType);
    if (filter.entityId) params.append('entityId', filter.entityId);
    if (filter.status) params.append('status', filter.status);
    if (filter.initiatedById) params.append('initiatedById', filter.initiatedById);
    if (filter.startedAfter) params.append('startedAfter', filter.startedAfter.toISOString());
    if (filter.startedBefore) params.append('startedBefore', filter.startedBefore.toISOString());
    if (filter.completedAfter) params.append('completedAfter', filter.completedAfter.toISOString());
    if (filter.completedBefore) params.append('completedBefore', filter.completedBefore.toISOString());

    const response = await apiService.get<ApiPagedResponse<WorkflowStatusDto[]>>(
      `${this.basePath}/instances?${params.toString()}`
    );

    return {
      data: response.data || [],
      totalCount: response.metadata?.totalCount || 0,
      page: response.metadata?.page || 1,
      pageSize: response.metadata?.pageSize || 25
    };
  }

  /**
   * Gets a specific workflow instance status
   */
  async getWorkflowInstance(id: string): Promise<WorkflowStatusDto> {
    const response = await apiService.get<ApiResponse<WorkflowStatusDto>>(
      `${this.basePath}/instances/${id}`
    );
    return this.requireData(response, 'Workflow instance response did not include data.');
  }

  /**
   * Gets a lightweight workflow summary for a specific entity record (current step, pending approvers, and whether the current user can approve).
   */
  async getWorkflowEntitySummary(entityType: string, entityId: string): Promise<WorkflowEntitySummaryDto> {
    const params = new URLSearchParams();
    params.append('entityType', entityType);
    params.append('entityId', entityId);

    const response = await apiService.get<ApiResponse<WorkflowEntitySummaryDto>>(
      `${this.basePath}/entity-summary?${params.toString()}`
    );

    return this.requireData(response, 'Workflow entity summary response did not include data.');
  }

  /**
   * Batch variant of getWorkflowEntitySummary (preferred for list/grids).
   */
  async getWorkflowEntitySummariesBatch(
    entities: { entityType: string; entityId: string }[]
  ): Promise<WorkflowEntitySummaryDto[]> {
    const response = await apiService.post<ApiResponse<WorkflowEntitySummaryDto[]>>(
      `${this.basePath}/entity-summary/batch`,
      { entities }
    );

    return response.data || [];
  }

  /**
   * Gets detailed workflow audit information (steps + approvals) for a specific entity record.
   */
  async getWorkflowEntityAudit(entityType: string, entityId: string): Promise<WorkflowEntityAuditDto | null> {
    const params = new URLSearchParams();
    params.append('entityType', entityType);
    params.append('entityId', entityId);

    const response = await apiService.get<ApiResponse<WorkflowEntityAuditDto | null>>(
      `${this.basePath}/entity-audit?${params.toString()}`
    );

    return response.data ?? null;
  }

  /**
   * Starts a new workflow instance
   */
  async startWorkflow(request: StartWorkflowRequest): Promise<WorkflowInstance> {
    const response = await apiService.post<ApiResponse<WorkflowInstance>>(
      `${this.basePath}/instances/start`,
      request
    );
    return this.requireData(response, 'Workflow start response did not include data.');
  }

  /**
   * Executes the next step in a workflow instance
   */
  async executeNextStep(instanceId: string, request: ExecuteStepRequest): Promise<WorkflowExecutionResult> {
    const response = await apiService.post<ApiResponse<WorkflowExecutionResult>>(
      `${this.basePath}/instances/${instanceId}/execute`,
      request
    );
    return this.requireData(response, 'Workflow execution response did not include data.');
  }

  /**
   * Cancels a running workflow instance
   */
  async cancelWorkflow(instanceId: string, request: CancelWorkflowRequest): Promise<void> {
    await apiService.post<ApiResponse<void>>(
      `${this.basePath}/instances/${instanceId}/cancel`,
      request
    );
  }

  async getWorkflowModuleConformance(): Promise<WorkflowModuleConformanceReport> {
    const response = await apiService.get<ApiResponse<WorkflowModuleConformanceReport>>(
      `${this.basePath}/conformance`
    );
    return this.requireData(response, 'Workflow conformance response did not include data.');
  }

  async getApprovalPolicies(): Promise<WorkflowApprovalPolicySetDto[]> {
    const response = await apiService.get<ApiResponse<WorkflowApprovalPolicySetDto[]>>(
      `${this.basePath}/approval-policies`
    );
    return response.data || [];
  }

  async createApprovalPolicy(request: SaveWorkflowApprovalPolicyRequest): Promise<WorkflowApprovalPolicySetDto> {
    const response = await apiService.post<ApiResponse<WorkflowApprovalPolicySetDto>>(
      `${this.basePath}/approval-policies`, request
    );
    return this.requireData(response, 'Approval policy creation response did not include data.');
  }

  async updateApprovalPolicy(id: string, request: SaveWorkflowApprovalPolicyRequest): Promise<WorkflowApprovalPolicySetDto> {
    const response = await apiService.put<ApiResponse<WorkflowApprovalPolicySetDto>>(
      `${this.basePath}/approval-policies/${id}`, request
    );
    return this.requireData(response, 'Approval policy update response did not include data.');
  }

  async publishApprovalPolicy(id: string): Promise<WorkflowApprovalPolicySetDto> {
    const response = await apiService.post<ApiResponse<WorkflowApprovalPolicySetDto>>(
      `${this.basePath}/approval-policies/${id}/publish`, {}
    );
    return this.requireData(response, 'Approval policy publish response did not include data.');
  }

  async retireApprovalPolicy(id: string): Promise<WorkflowApprovalPolicySetDto> {
    const response = await apiService.post<ApiResponse<WorkflowApprovalPolicySetDto>>(
      `${this.basePath}/approval-policies/${id}/retire`, {}
    );
    return this.requireData(response, 'Approval policy retirement response did not include data.');
  }

  async cloneApprovalPolicyDraft(id: string): Promise<WorkflowApprovalPolicySetDto> {
    const response = await apiService.post<ApiResponse<WorkflowApprovalPolicySetDto>>(
      `${this.basePath}/approval-policies/${id}/clone-draft`, {}
    );
    return this.requireData(response, 'Approval policy clone response did not include data.');
  }

  async getWorkflowDirectoryUsers(search?: string): Promise<WorkflowDirectoryUser[]> {
    const query = search ? `?search=${encodeURIComponent(search)}` : '';
    const response = await apiService.get<ApiResponse<WorkflowDirectoryUser[]>>(
      `${this.basePath}/governance/users${query}`
    );
    return response.data || [];
  }

  async getWorkflowDelegationScopeOptions(): Promise<WorkflowDelegationScopeOptionsDto> {
    const response = await apiService.get<ApiResponse<WorkflowDelegationScopeOptionsDto>>(
      `${this.basePath}/governance/delegation-scope-options`
    );
    return this.requireData(response, 'Delegation scope response did not include data.');
  }

  async getWorkflowDelegations(): Promise<WorkflowDelegationDto[]> {
    const response = await apiService.get<ApiResponse<WorkflowDelegationDto[]>>(
      `${this.basePath}/governance/delegations`
    );
    return response.data || [];
  }

  async createWorkflowDelegation(request: SaveWorkflowDelegationRequest): Promise<WorkflowDelegationDto> {
    const response = await apiService.post<ApiResponse<WorkflowDelegationDto>>(
      `${this.basePath}/governance/delegations`, request
    );
    return this.requireData(response, 'Delegation response did not include data.');
  }

  async revokeWorkflowDelegation(id: string, reason: string): Promise<WorkflowDelegationDto> {
    const response = await apiService.post<ApiResponse<WorkflowDelegationDto>>(
      `${this.basePath}/governance/delegations/${id}/revoke`, { reason }
    );
    return this.requireData(response, 'Delegation revocation response did not include data.');
  }

  async getWorkflowSlaBreaches(days = 30): Promise<WorkflowSlaBreachesDto> {
    const response = await apiService.get<ApiResponse<WorkflowSlaBreachesDto>>(
      `${this.basePath}/governance/sla-breaches?days=${encodeURIComponent(String(days))}`
    );
    return this.requireData(response, 'SLA breach response did not include data.');
  }

  async getWorkflowCalendar(): Promise<WorkflowWorkingCalendarDto | undefined> {
    const response = await apiService.get<ApiResponse<any>>(`${this.basePath}/governance/calendar`);
    const calendar = response.data;
    if (!calendar) return undefined;
    return { ...calendar, holidays: JSON.parse(calendar.holidaysJson || '[]') };
  }

  async saveWorkflowCalendar(request: WorkflowWorkingCalendarDto): Promise<WorkflowWorkingCalendarDto> {
    const response = await apiService.put<ApiResponse<any>>(`${this.basePath}/governance/calendar`, request);
    const calendar = this.requireData(response, 'Working calendar response did not include data.');
    return { ...calendar, holidays: JSON.parse(calendar.holidaysJson || '[]') };
  }

  async delegateApproval(approvalId: string, delegateToId: string, comments: string): Promise<WorkflowApproval> {
    return this.processApproval(approvalId, { action: 2, delegateToId, comments });
  }

  async sendApprovalBack(approvalId: string, request: {
    correctionOwnerId?: string; targetStepInstanceId?: string; instructions: string; dueWorkingHours?: number;
  }): Promise<WorkflowCorrectionRequestDto> {
    const response = await apiService.post<ApiResponse<WorkflowCorrectionRequestDto>>(
      `${this.basePath}/governance/approvals/${approvalId}/send-back`, request
    );
    return this.requireData(response, 'Correction request response did not include data.');
  }

  async getMyWorkflowCorrections(): Promise<WorkflowCorrectionRequestDto[]> {
    const response = await apiService.get<ApiResponse<WorkflowCorrectionRequestDto[]>>(
      `${this.basePath}/governance/corrections/my`
    );
    return response.data || [];
  }

  async resubmitWorkflowCorrection(id: string, data?: unknown, comments?: string): Promise<WorkflowCorrectionRequestDto> {
    const response = await apiService.post<ApiResponse<WorkflowCorrectionRequestDto>>(
      `${this.basePath}/governance/corrections/${id}/resubmit`, { data, comments }
    );
    return this.requireData(response, 'Correction resubmission response did not include data.');
  }

  async stageWorkflowSignature(approvalId: string, signature: WorkflowSignatureSubmissionDto): Promise<void> {
    await apiService.post<ApiResponse<void>>(`${this.basePath}/evidence/signatures/${approvalId}/stage`, signature);
  }

  async getWorkflowEvidencePolicy(): Promise<WorkflowEvidencePolicyDto> {
    const response = await apiService.get<ApiResponse<any>>(`${this.basePath}/evidence/policy`);
    const policy = response.data;
    if (!policy) {
      return { allowedExtensions: ['.pdf', '.png', '.jpg', '.jpeg'], maximumFileSizeBytes: 10 * 1024 * 1024,
        retentionDays: 2555, requireMalwareScan: true };
    }
    let allowedExtensions: string[] = [];
    try { allowedExtensions = JSON.parse(policy.allowedExtensionsJson || '[]'); } catch { allowedExtensions = []; }
    return { ...policy, allowedExtensions };
  }

  async saveWorkflowEvidencePolicy(request: WorkflowEvidencePolicyDto): Promise<WorkflowEvidencePolicyDto> {
    const response = await apiService.put<ApiResponse<any>>(`${this.basePath}/evidence/policy`, request);
    const policy = this.requireData(response, 'Evidence policy response did not include data.');
    let allowedExtensions: string[] = [];
    try { allowedExtensions = JSON.parse(policy.allowedExtensionsJson || '[]'); } catch { allowedExtensions = []; }
    return { ...policy, allowedExtensions };
  }

  async getWorkflowStepEvidence(stepInstanceId: string): Promise<WorkflowEvidenceDocumentDto[]> {
    const response = await apiService.get<ApiResponse<WorkflowEvidenceResponse[]>>(
      `${this.basePath}/evidence/step/${stepInstanceId}`
    );
    return (response.data || []).map(item => ({
      ...item,
      verificationStatus: normalizeEvidenceStatus(item.verificationStatus, ['pending', 'verified', 'rejected']),
      malwareScanStatus: normalizeEvidenceStatus(item.malwareScanStatus, ['pending', 'clean', 'infected', 'failed']),
    }));
  }

  async getWorkflowEvidenceReviewInstances(params: {
    search?: string;
    entityType?: string;
    workflowDefinitionId?: string;
    workflowInstanceId?: string;
    pageSize?: number;
  } = {}): Promise<WorkflowEvidenceReviewInstanceDto[]> {
    const query = new URLSearchParams();
    if (params.search) query.append('search', params.search);
    if (params.entityType) query.append('entityType', params.entityType);
    if (params.workflowDefinitionId) query.append('workflowDefinitionId', params.workflowDefinitionId);
    if (params.workflowInstanceId) query.append('workflowInstanceId', params.workflowInstanceId);
    if (params.pageSize) query.append('pageSize', String(params.pageSize));
    const suffix = query.toString() ? `?${query.toString()}` : '';
    const response = await apiService.get<ApiResponse<WorkflowEvidenceReviewInstanceDto[]>>(
      `${this.basePath}/evidence/review/instances${suffix}`
    );
    return response.data || [];
  }

  async verifyWorkflowEvidence(id: string, accepted: boolean, notes?: string): Promise<void> {
    await apiService.post<ApiResponse<void>>(`${this.basePath}/evidence/${id}/verify`, { accepted, notes });
  }

  async setWorkflowEvidenceLegalHold(id: string, enabled: boolean, reason?: string): Promise<void> {
    await apiService.post<ApiResponse<void>>(`${this.basePath}/evidence/${id}/legal-hold`, { enabled, reason });
  }

  async getWorkflowTemplates(): Promise<any[]> {
    const response = await apiService.get<ApiResponse<any[]>>(`${this.basePath}/platform/templates`);
    return response.data || [];
  }

  async exportWorkflowTemplate(id: string): Promise<any> {
    return apiService.get<any>(`${this.basePath}/platform/templates/${id}/export`);
  }

  async importWorkflowTemplate(workflowPackage: any): Promise<any> {
    const response = await apiService.post<ApiResponse<any>>(`${this.basePath}/platform/templates/import`, workflowPackage);
    return this.requireData(response, 'Template import response did not include data.');
  }

  async simulateWorkflowDefinition(id: string, dataContext?: unknown): Promise<any> {
    const response = await apiService.post<ApiResponse<any>>(`${this.basePath}/platform/definitions/${id}/simulate`, { dataContext });
    return this.requireData(response, 'Simulation response did not include data.');
  }

  async rollbackWorkflowDefinition(id: string): Promise<any> {
    const response = await apiService.post<ApiResponse<any>>(`${this.basePath}/platform/definitions/${id}/rollback-draft`, {});
    return this.requireData(response, 'Rollback response did not include data.');
  }

  async getWorkflowAnalytics(days = 30): Promise<any> {
    const response = await apiService.get<ApiResponse<any>>(`${this.basePath}/platform/analytics?days=${days}`);
    return this.requireData(response, 'Analytics response did not include data.');
  }

  async getWorkflowIntegrationExceptions(): Promise<any[]> {
    const response = await apiService.get<ApiResponse<any[]>>(`${this.basePath}/platform/integrations/exceptions`);
    return response.data || [];
  }

  async retryWorkflowIntegration(id: string): Promise<void> {
    await apiService.post<ApiResponse<void>>(`${this.basePath}/platform/integrations/${id}/retry`, {});
  }

  async getMobileWorkflowInbox(): Promise<any[]> {
    const response = await apiService.get<ApiResponse<any[]>>(`${this.basePath}/platform/mobile/inbox`);
    return response.data || [];
  }

  async submitOfflineWorkflowAction(request: { idempotencyKey: string; actionType: string; payload: any }): Promise<any> {
    const response = await apiService.post<ApiResponse<any>>(`${this.basePath}/platform/mobile/actions`, request);
    return response.data;
  }

  async subscribeWorkflowPush(subscription: PushSubscription): Promise<void> {
    await apiService.post<any>('/pwa/subscribe', subscription.toJSON());
  }

  async addAdHocApprover(stepInstanceId: string, request: {
    userId?: string; role?: string; approvalGroup: number; dueWorkingHours?: number; reason: string;
  }): Promise<WorkflowApproval> {
    const response = await apiService.post<ApiResponse<WorkflowApproval>>(
      `${this.basePath}/governance/steps/${stepInstanceId}/ad-hoc-approvers`, request
    );
    return this.requireData(response, 'Ad hoc approval response did not include data.');
  }

  async removeAdHocApprover(approvalId: string, reason: string): Promise<void> {
    await apiService.delete<ApiResponse<void>>(
      `${this.basePath}/governance/approvals/${approvalId}/ad-hoc?reason=${encodeURIComponent(reason)}`
    );
  }

  async cloneWorkflowDefinitionDraft(id: string, changeSummary?: string): Promise<WorkflowDefinitionDto> {
    const response = await apiService.post<ApiResponse<WorkflowDefinitionDto>>(
      `${this.basePath}/definitions/${id}/clone-draft`,
      { changeSummary }
    );
    return this.requireData(response, 'Workflow draft clone response did not include data.');
  }

  async publishWorkflowDefinition(id: string): Promise<WorkflowDefinitionDto> {
    const response = await apiService.post<ApiResponse<WorkflowDefinitionDto>>(
      `${this.basePath}/definitions/${id}/publish`
    );
    return this.requireData(response, 'Workflow publish response did not include data.');
  }

  async retireWorkflowDefinition(id: string): Promise<WorkflowDefinitionDto> {
    const response = await apiService.post<ApiResponse<WorkflowDefinitionDto>>(
      `${this.basePath}/definitions/${id}/retire`
    );
    return this.requireData(response, 'Workflow retire response did not include data.');
  }

  async getWorkflowDefinitionVersions(id: string): Promise<WorkflowDefinitionVersionDto[]> {
    const response = await apiService.get<ApiResponse<WorkflowDefinitionVersionDto[]>>(
      `${this.basePath}/definitions/${id}/versions`
    );
    return response.data || [];
  }

  async compareWorkflowDefinitions(
    fromDefinitionId: string,
    toDefinitionId: string
  ): Promise<WorkflowDefinitionComparisonDto> {
    const params = new URLSearchParams({ fromDefinitionId, toDefinitionId });
    const response = await apiService.get<ApiResponse<WorkflowDefinitionComparisonDto>>(
      `${this.basePath}/definitions/compare?${params.toString()}`
    );
    return this.requireData(response, 'Workflow comparison response did not include data.');
  }

  /**
   * Recalls the active workflow for an entity record back to draft.
   */
  async recallWorkflowEntity(entityType: string, entityId: string, request: RecallWorkflowRequest = {}): Promise<void> {
    await apiService.post<ApiResponse<void>>(
      `${this.basePath}/entity/${encodeURIComponent(entityType)}/${encodeURIComponent(entityId)}/recall`,
      request
    );
  }

  // Workflow Step Management

  /**
   * Gets pending tasks for the current user
   */
  async getPendingTasks(): Promise<WorkflowStepInstance[]> {
    const response = await apiService.get<ApiResponse<WorkflowStepInstance[]>>(
      `${this.basePath}/tasks/pending`
    );
    return response.data || [];
  }

  /**
   * Processes a specific workflow step
   */
  async processStep(stepInstanceId: string, request: ProcessStepRequest): Promise<WorkflowExecutionResult> {
    const response = await apiService.post<ApiResponse<WorkflowExecutionResult>>(
      `${this.basePath}/steps/${stepInstanceId}/process`,
      request
    );
    return this.requireData(response, 'Workflow step processing response did not include data.');
  }

  /**
   * Saves checklist responses against a workflow step before the current action is processed.
   */
  async saveStepChecklistResponses(stepInstanceId: string, responses: WorkflowApprovalChecklistResponseDto[]): Promise<void> {
    await apiService.post<ApiResponse<void>>(
      `${this.basePath}/steps/${stepInstanceId}/checklist-responses`,
      { responses }
    );
  }

  /**
   * Gets documents uploaded against a workflow task or approval checklist step.
   */
  async getStepAttachments(stepInstanceId: string): Promise<WorkflowTaskAttachmentDto[]> {
    const response = await apiService.get<ApiResponse<WorkflowTaskAttachmentDto[]>>(
      `${this.basePath}/steps/${stepInstanceId}/attachments`
    );

    return response.data || [];
  }

  /**
   * Downloads a task attachment for review from workflow history or the current step.
   */
  async downloadStepAttachment(stepInstanceId: string, attachmentId: string): Promise<Blob> {
    return rawApiService.downloadBlob(
      `${this.basePath}/steps/${encodeURIComponent(stepInstanceId)}/attachments/${encodeURIComponent(attachmentId)}/download`
    );
  }

  /**
   * Uploads workflow task or approval-checklist evidence using multipart/form-data.
   */
  async uploadStepAttachment(
    stepInstanceId: string,
    file: File,
    requirementKey?: string,
    documentName?: string,
    documentType?: string
  ): Promise<WorkflowTaskAttachmentDto> {
    const formData = new FormData();
    formData.append('file', file);

    if (requirementKey) {
      formData.append('requirementKey', requirementKey);
    }

    if (documentName) {
      formData.append('documentName', documentName);
    }

    if (documentType) {
      formData.append('documentType', documentType);
    }

    const response = await rawApiService.request<ApiResponse<WorkflowTaskAttachmentDto>>(
      `${this.basePath}/steps/${stepInstanceId}/attachments`,
      {
        method: 'POST',
        body: formData,
      }
    );

    return this.requireData(response, 'Workflow task attachment upload response did not include data.');
  }

  /**
   * Assigns a step to a user
   */
  async assignStep(stepInstanceId: string, request: AssignStepRequest): Promise<void> {
    await apiService.post<ApiResponse<void>>(
      `${this.basePath}/steps/${stepInstanceId}/assign`,
      request
    );
  }

  // Workflow Approval Management

  /**
   * Gets pending approvals for the current user
   */
  async getPendingApprovals(): Promise<WorkflowApproval[]> {
    const response = await apiService.get<ApiResponse<WorkflowApproval[]>>(
      `${this.basePath}/approvals/pending`
    );
    return response.data || [];
  }

  /**
   * Processes an approval decision
   */
  async processApproval(approvalId: string, request: ProcessApprovalRequest): Promise<WorkflowApproval> {
    const response = await apiService.post<ApiResponse<WorkflowApproval>>(
      `${this.basePath}/approvals/${approvalId}/process`,
      request
    );
    return this.requireData(response, 'Workflow approval response did not include data.');
  }

  // Administration and Statistics

  /**
   * Gets workflow administration summary statistics
   */
  async getWorkflowSummary(): Promise<WorkflowSummaryDto> {
    const response = await apiService.get<ApiResponse<WorkflowSummaryDto>>(
      `${this.basePath}/administration/summary`
    );
    return this.requireData(response, 'Workflow summary response did not include data.');
  }

  /**
   * Gets overdue workflow steps
   */
  async getOverdueSteps(): Promise<WorkflowStepInstance[]> {
    const response = await apiService.get<ApiResponse<WorkflowStepInstance[]>>(
      `${this.basePath}/administration/overdue-steps`
    );
    return response.data || [];
  }

  // Helper Methods

  /**
   * Helper method to format workflow status for display
   */
  formatWorkflowStatus(status: number): string {
    const statusMap: Record<number, string> = {
      0: 'Created',
      1: 'In Progress',
      2: 'Completed',
      3: 'Cancelled',
      4: 'Failed',
      5: 'Suspended',
      6: 'Waiting'
    };
    return statusMap[status] || 'Unknown';
  }

  /**
   * Helper method to format workflow step status for display
   */
  formatStepStatus(status: number): string {
    const statusMap: Record<number, string> = {
      0: 'Pending',
      1: 'In Progress',
      2: 'Completed',
      3: 'Cancelled',
      4: 'Failed'
    };
    return statusMap[status] || 'Unknown';
  }

  /**
   * Helper method to format approval status for display
   */
  formatApprovalStatus(status: number): string {
    const statusMap: Record<number, string> = {
      0: 'Pending',
      1: 'Approved',
      2: 'Rejected',
      3: 'Delegated',
      4: 'Expired',
      5: 'More Info Requested'
    };
    return statusMap[status] || 'Unknown';
  }

  /**
   * Helper method to get status badge color class
   */
  getStatusBadgeClass(status: number): string {
    const statusClassMap: Record<number, string> = {
      0: 'badge-secondary', // Created/Pending
      1: 'badge-primary',   // In Progress
      2: 'badge-success',   // Completed/Approved
      3: 'badge-warning',   // Cancelled/Delegated
      4: 'badge-danger',    // Failed/Rejected
      5: 'badge-info',      // Suspended/Expired
      6: 'badge-info'       // Waiting/More Info
    };
    return statusClassMap[status] || 'badge-secondary';
  }

  /**
   * Helper method to format date for display
   */
  formatDate(date: Date | string): string {
    if (!date) return 'N/A';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleDateString() + ' ' + dateObj.toLocaleTimeString();
  }

  /**
   * Helper method to calculate days since date
   */
  getDaysSince(date: Date | string): number {
    if (!date) return 0;
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    const now = new Date();
    const diffTime = Math.abs(now.getTime() - dateObj.getTime());
    return Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  }

  /**
   * Helper method to check if a step is overdue
   */
  isOverdue(dueDate?: Date | string): boolean {
    if (!dueDate) return false;
    const dueDateObj = typeof dueDate === 'string' ? new Date(dueDate) : dueDate;
    return dueDateObj < new Date();
  }
}

// Create and export a singleton instance
export const workflowApiService = new WorkflowApiService();
export default workflowApiService;
