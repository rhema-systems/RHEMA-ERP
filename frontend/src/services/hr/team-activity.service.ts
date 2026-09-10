import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  CreateTeamObjectiveRequest,
  CreateTeamTaskChecklistItemRequest,
  CreateTeamTaskRequest,
  CreateTeamTermsOfReferenceRequest,
  TeamObjective,
  TeamObjectiveDetail,
  TeamObjectiveStatus,
  TeamObjectiveStatusChangeRequest,
  TeamObjectiveWeightTotal,
  TeamTask,
  TeamTaskAttachment,
  TeamTaskChecklistItem,
  TeamTaskDetail,
  TeamTaskStatusChangeRequest,
  TeamTermsOfReference,
  TeamTermsOfReferenceDetail,
  UpdateTeamObjectiveRequest,
  UpdateTeamTaskChecklistItemRequest,
  UpdateTeamTaskRequest,
  UpdateTeamTermsOfReferenceRequest,
} from '@/types/hr/team-activity';

/**
 * What a team or committee is chartered to do, what it has undertaken, and who is doing it.
 * Backend routes: `api/hr/teams/**` and `api/hr/team-documents/**`.
 *
 * ⚠ **Who may write is decided by the SERVER against the team record, not by the route.** HR acts
 * on any team; the team's lead or deputy on their own; an ordinary member may move, tick and attach
 * to a task assigned to them. A screen that hides a button is a courtesy — every one of those rules
 * is enforced again on the write, and a refusal comes back 403 with a sentence.
 *
 * ⚠ **A refusal here is 422, not 400.** This controller carries `TeamActivityBusinessRules`, like
 * the other nine business-rule filters; the message is the rule's own and is worth showing.
 */
class TeamActivityService {
  private readonly baseUrl = '/hr/teams';
  private readonly docsUrl = '/hr/team-documents';

  // ── Terms of reference ────────────────────────────────────────────────────

  getTerms(teamId: string) {
    return apiService.get<TeamTermsOfReference[]>(`${this.baseUrl}/${teamId}/terms`);
  }

  getTermsById(id: string) {
    return apiService.get<TeamTermsOfReferenceDetail>(`${this.baseUrl}/terms/${id}`);
  }

  createTerms(teamId: string, payload: CreateTeamTermsOfReferenceRequest) {
    return apiService.post<TeamTermsOfReferenceDetail>(`${this.baseUrl}/${teamId}/terms`, payload);
  }

  /** Edits a draft. An approved version is immutable — take a new one instead. */
  updateTerms(id: string, payload: UpdateTeamTermsOfReferenceRequest) {
    return apiService.put<TeamTermsOfReferenceDetail>(`${this.baseUrl}/terms/${id}`, payload);
  }

  submitTerms(id: string) {
    return apiService.post<TeamTermsOfReferenceDetail>(`${this.baseUrl}/terms/${id}/submit`, {});
  }

  approveTerms(id: string) {
    return apiService.post<TeamTermsOfReferenceDetail>(`${this.baseUrl}/terms/${id}/approve`, {});
  }

  newTermsVersion(id: string) {
    return apiService.post<TeamTermsOfReferenceDetail>(`${this.baseUrl}/terms/${id}/new-version`, {});
  }

  deleteTerms(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/terms/${id}`);
  }

  uploadTermsDocument(termsId: string, file: File) {
    return hrDocumentService.upload<unknown>(`${this.docsUrl}/terms/${termsId}/document`, file);
  }

  termsDocumentUrl(termsId: string) {
    return `${this.docsUrl}/terms/${termsId}/document`;
  }

  // ── Objectives ────────────────────────────────────────────────────────────

  getObjectives(teamId: string) {
    return apiService.get<TeamObjective[]>(`${this.baseUrl}/${teamId}/objectives`);
  }

  /** Advisory only — nothing refuses a write because this is not 100. */
  getObjectiveWeightTotal(teamId: string) {
    return apiService.get<TeamObjectiveWeightTotal>(
      `${this.baseUrl}/${teamId}/objectives/weight-total`);
  }

  getObjectiveById(id: string) {
    return apiService.get<TeamObjectiveDetail>(`${this.baseUrl}/objectives/${id}`);
  }

  createObjective(teamId: string, payload: CreateTeamObjectiveRequest) {
    return apiService.post<TeamObjectiveDetail>(`${this.baseUrl}/${teamId}/objectives`, payload);
  }

  updateObjective(id: string, payload: UpdateTeamObjectiveRequest) {
    return apiService.put<TeamObjectiveDetail>(`${this.baseUrl}/objectives/${id}`, payload);
  }

  /**
   * Moves an objective.
   *
   * ⚠ Completing below 100 % needs `outcomeSummary`; cancelling needs `cancelledReason`. The
   * server refuses without them and says so.
   */
  changeObjectiveStatus(
    id: string,
    status: TeamObjectiveStatus,
    payload: TeamObjectiveStatusChangeRequest = {},
  ) {
    return apiService.post<TeamObjectiveDetail>(
      `${this.baseUrl}/objectives/${id}/status/${status}`, payload);
  }

  deleteObjective(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/objectives/${id}`);
  }

  // ── Tasks ─────────────────────────────────────────────────────────────────

  /** `mine` is resolved from the TOKEN server-side; it is not a caller-supplied member id. */
  getTasks(teamId: string, options: { objectiveId?: string; mine?: boolean } = {}) {
    const params = new URLSearchParams();
    if (options.objectiveId) params.set('objectiveId', options.objectiveId);
    if (options.mine) params.set('mine', 'true');
    const query = params.toString();
    return apiService.get<TeamTask[]>(
      `${this.baseUrl}/${teamId}/tasks${query ? `?${query}` : ''}`);
  }

  getTaskById(id: string) {
    return apiService.get<TeamTaskDetail>(`${this.baseUrl}/tasks/${id}`);
  }

  createTask(teamId: string, payload: CreateTeamTaskRequest) {
    return apiService.post<TeamTaskDetail>(`${this.baseUrl}/${teamId}/tasks`, payload);
  }

  updateTask(id: string, payload: UpdateTeamTaskRequest) {
    return apiService.put<TeamTaskDetail>(`${this.baseUrl}/tasks/${id}`, payload);
  }

  /** The one write an ordinary member may make, and only on a task assigned to them. */
  changeTaskStatus(id: string, payload: TeamTaskStatusChangeRequest) {
    return apiService.post<TeamTaskDetail>(`${this.baseUrl}/tasks/${id}/status`, payload);
  }

  deleteTask(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/tasks/${id}`);
  }

  // ── Checklist ─────────────────────────────────────────────────────────────

  addChecklistItem(taskId: string, payload: CreateTeamTaskChecklistItemRequest) {
    return apiService.post<TeamTaskChecklistItem>(
      `${this.baseUrl}/tasks/${taskId}/checklist`, payload);
  }

  updateChecklistItem(id: string, payload: UpdateTeamTaskChecklistItemRequest) {
    return apiService.put<TeamTaskChecklistItem>(`${this.baseUrl}/checklist/${id}`, payload);
  }

  deleteChecklistItem(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/checklist/${id}`);
  }

  // ── Attachments ───────────────────────────────────────────────────────────
  //
  // ⚠ There is no JSON create. A file reaches a task through the gate below, which scans it and
  // registers it in the DMS; the row is written from what the gate returns.

  uploadTaskAttachment(taskId: string, file: File, title?: string) {
    return hrDocumentService.upload<TeamTaskAttachment>(
      `${this.docsUrl}/tasks/${taskId}/attachments`, file, title ? { title } : undefined);
  }

  taskAttachmentUrl(attachmentId: string) {
    return `${this.docsUrl}/tasks/attachments/${attachmentId}`;
  }

  deleteTaskAttachment(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/tasks/attachments/${id}`);
  }
}

export const teamActivityService = new TeamActivityService();
