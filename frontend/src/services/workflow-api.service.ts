import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';
import type { ApiResponse } from '../types';
import type {
  WorkflowDefinitionAdminDto,
  CreateWorkflowDefinitionAdminDto,
  UpdateWorkflowDefinitionAdminDto,
  WorkflowDefinitionDto,
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
   * Saves approval checklist responses against a workflow step before the approval action is processed.
   */
  async saveStepChecklistResponses(stepInstanceId: string, responses: WorkflowApprovalChecklistResponseDto[]): Promise<void> {
    await apiService.post<ApiResponse<void>>(
      `${this.basePath}/steps/${stepInstanceId}/checklist-responses`,
      { responses }
    );
  }

  /**
   * Gets attachments uploaded against a manual workflow task step.
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
   * Uploads a task attachment. Uses the raw API service so FormData is sent as multipart/form-data.
   */
  async uploadStepAttachment(
    stepInstanceId: string,
    file: File,
    requirementKey?: string,
    documentName?: string
  ): Promise<WorkflowTaskAttachmentDto> {
    const formData = new FormData();
    formData.append('file', file);

    if (requirementKey) {
      formData.append('requirementKey', requirementKey);
    }

    if (documentName) {
      formData.append('documentName', documentName);
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
