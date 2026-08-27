import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  AppraisalCalendarEvent,
  AppraisalCriterion,
  AppraisalCycle,
  AppraisalCycleProgress,
  AppraisalCycleTarget,
  AppraisalCycleTargetExclusion,
  AppraisalCycleTemplate,
  AppraisalGradeDefinition,
  AppraisalNotification,
  AppraisalNotificationSummary,
  AppraisalSettings,
  AppraisalTemplate,
  AppraisalTemplateItem,
  AppraisalTemplateSection,
  AppraisalTemplateSummary,
  AppraisalType,
  CopyAppraisalTemplate,
  CoveragePreview,
  CreateAppraisalCriterion,
  CreateAppraisalCycle,
  CreateAppraisalCycleTarget,
  CreateAppraisalCycleTargetExclusion,
  CreateAppraisalCycleTemplate,
  CreateAppraisalGradeDefinition,
  CreateAppraisalSettings,
  CreateAppraisalTemplate,
  CreateAppraisalTemplateItem,
  CreateAppraisalTemplateSection,
  GenerateAppraisalsResult,
  TemplateItemGradeRange,
  UpdateAppraisalCriterion,
  UpdateAppraisalCycle,
  UpdateAppraisalCycleTarget,
  UpdateAppraisalCycleTargetExclusion,
  UpdateAppraisalCycleTemplate,
  UpdateAppraisalGradeDefinition,
  UpdateAppraisalSettings,
  UpdateAppraisalTemplate,
  UpdateAppraisalTemplateItem,
  UpdateAppraisalTemplateSection,
  UpsertTemplateItemGradeRanges,
} from '@/types/hr/appraisal';

/**
 * Appraisal cycles and the forms they run on.
 *
 * Paging here is `pageNumber`/`pageSize` throughout — unlike the goal library, which is the
 * odd one out. Each class below matches one controller, and the routes deliberately keep the
 * ported inconsistencies (`AppraisalCompetency` singular, `AppraisalCycle` singular,
 * `AppraisalTemplates` plural) rather than being normalised.
 */

// ── Appraisal settings ─────────────────────────────────────────────────────────────

/**
 * api/AppraisalSettings — the named policy profiles a cycle runs under.
 *
 * ⚠ Create and update both reject a weight total that is not 1.0, having first zeroed the
 * weight of every evaluator that is switched off. `evaluationWeightTotal` in the types module
 * reproduces that rule so a form can warn before the round trip.
 */
class AppraisalSettingsService {
  private readonly baseUrl = '/AppraisalSettings';

  getAll(): Promise<AppraisalSettings[]> {
    return apiService.get<AppraisalSettings[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<AppraisalSettings>> {
    return apiService.get<PagedResult<AppraisalSettings>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<AppraisalSettings> {
    return apiService.get<AppraisalSettings>(`${this.baseUrl}/${id}`);
  }

  /** The most recently created profile. 404 when the tenant has none yet. */
  getDefault(): Promise<AppraisalSettings> {
    return apiService.get<AppraisalSettings>(`${this.baseUrl}/default`);
  }

  create(data: CreateAppraisalSettings): Promise<AppraisalSettings> {
    return apiService.post<AppraisalSettings>(this.baseUrl, data);
  }

  update(id: string, data: UpdateAppraisalSettings): Promise<AppraisalSettings> {
    return apiService.put<AppraisalSettings>(`${this.baseUrl}/${id}`, data);
  }

  /** Refused with 400 while any cycle still uses the profile. */
  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }

  validateWeights(id: string): Promise<{ isValid: boolean; message: string }> {
    return apiService.get<{ isValid: boolean; message: string }>(
      `${this.baseUrl}/${id}/validate-weights`,
    );
  }
}

// ── Appraisal criteria ─────────────────────────────────────────────────────────────

/** api/AppraisalCompetency — the non-KPI things a template item can score. */
class AppraisalCriteriaService {
  private readonly baseUrl = '/AppraisalCompetency';

  getAll(): Promise<AppraisalCriterion[]> {
    return apiService.get<AppraisalCriterion[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<AppraisalCriterion>> {
    return apiService.get<PagedResult<AppraisalCriterion>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<AppraisalCriterion> {
    return apiService.get<AppraisalCriterion>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateAppraisalCriterion): Promise<AppraisalCriterion> {
    return apiService.post<AppraisalCriterion>(this.baseUrl, data);
  }

  update(id: string, data: UpdateAppraisalCriterion): Promise<AppraisalCriterion> {
    return apiService.put<AppraisalCriterion>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

// ── Grade definitions ──────────────────────────────────────────────────────────────

/** api/AppraisalGradeDefinitions — the grades template items score into. */
class AppraisalGradeDefinitionService {
  private readonly baseUrl = '/AppraisalGradeDefinitions';

  getAll(): Promise<AppraisalGradeDefinition[]> {
    return apiService.get<AppraisalGradeDefinition[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<AppraisalGradeDefinition>> {
    return apiService.get<PagedResult<AppraisalGradeDefinition>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<AppraisalGradeDefinition> {
    return apiService.get<AppraisalGradeDefinition>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateAppraisalGradeDefinition): Promise<AppraisalGradeDefinition> {
    return apiService.post<AppraisalGradeDefinition>(this.baseUrl, data);
  }

  update(id: string, data: UpdateAppraisalGradeDefinition): Promise<AppraisalGradeDefinition> {
    return apiService.put<AppraisalGradeDefinition>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

// ── Appraisal templates ────────────────────────────────────────────────────────────

/**
 * api/AppraisalTemplates — the form, its sections, its items and their grade bands.
 *
 * Two rules run through every write here and explain most of the errors a user will see:
 * a template assigned to an Open or InProgress cycle is frozen (clone it instead), and
 * activating or submitting one validates that section weights total 100, item weights total
 * 100 within each section, and every item has grade bands.
 *
 * Submit/approve/reject/recall run on the generic workflow engine, so who may approve comes
 * from the published AppraisalTemplate workflow definition, not from a role.
 */
class AppraisalTemplateService {
  private readonly baseUrl = '/AppraisalTemplates';

  getAll(): Promise<AppraisalTemplate[]> {
    return apiService.get<AppraisalTemplate[]>(this.baseUrl);
  }

  /** List projection with section/item counts — what the list page renders. */
  getSummaries(): Promise<AppraisalTemplateSummary[]> {
    return apiService.get<AppraisalTemplateSummary[]>(`${this.baseUrl}/summaries`);
  }

  getActive(): Promise<AppraisalTemplate[]> {
    return apiService.get<AppraisalTemplate[]>(`${this.baseUrl}/active`);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<AppraisalTemplate>> {
    return apiService.get<PagedResult<AppraisalTemplate>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<AppraisalTemplate> {
    return apiService.get<AppraisalTemplate>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateAppraisalTemplate): Promise<AppraisalTemplate> {
    return apiService.post<AppraisalTemplate>(this.baseUrl, data);
  }

  update(id: string, data: UpdateAppraisalTemplate): Promise<AppraisalTemplate> {
    return apiService.put<AppraisalTemplate>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Body is a bare boolean. Activating runs the weight/grade-band validation first. */
  setActive(id: string, isActive: boolean): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/active-status`, isActive);
  }

  /** The copy lands inactive and in Draft, whatever state the source was in. */
  clone(sourceTemplateId: string, data: CopyAppraisalTemplate): Promise<AppraisalTemplate> {
    return apiService.post<AppraisalTemplate>(`${this.baseUrl}/${sourceTemplateId}/clone`, data);
  }

  // ── Approval, through the workflow engine ────────────────────────────────────────

  submitForApproval(id: string): Promise<AppraisalTemplate> {
    return apiService.post<AppraisalTemplate>(`${this.baseUrl}/${id}/submit-for-approval`);
  }

  /** 403 when the caller is not an approver for the current step. */
  approve(id: string): Promise<AppraisalTemplate> {
    return apiService.post<AppraisalTemplate>(`${this.baseUrl}/${id}/approve`);
  }

  reject(id: string, reason?: string | null): Promise<AppraisalTemplate> {
    return apiService.post<AppraisalTemplate>(`${this.baseUrl}/${id}/reject`, {
      reason: reason ?? null,
    });
  }

  /** Author's undo, allowed only while still pending. */
  recall(id: string): Promise<AppraisalTemplate> {
    return apiService.post<AppraisalTemplate>(`${this.baseUrl}/${id}/recall`);
  }

  // ── Sections ─────────────────────────────────────────────────────────────────────

  getSections(templateId: string): Promise<AppraisalTemplateSection[]> {
    return apiService.get<AppraisalTemplateSection[]>(`${this.baseUrl}/${templateId}/sections`);
  }

  addSection(
    templateId: string,
    data: CreateAppraisalTemplateSection,
  ): Promise<AppraisalTemplateSection> {
    return apiService.post<AppraisalTemplateSection>(
      `${this.baseUrl}/${templateId}/sections`,
      data,
    );
  }

  updateSection(
    templateId: string,
    sectionId: string,
    data: UpdateAppraisalTemplateSection,
  ): Promise<AppraisalTemplateSection> {
    return apiService.put<AppraisalTemplateSection>(
      `${this.baseUrl}/${templateId}/sections/${sectionId}`,
      data,
    );
  }

  removeSection(templateId: string, sectionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${templateId}/sections/${sectionId}`);
  }

  /** Body is a bare array of ids, in the order wanted. */
  reorderSections(templateId: string, orderedSectionIds: string[]): Promise<void> {
    return apiService.patch<void>(
      `${this.baseUrl}/${templateId}/sections/reorder`,
      orderedSectionIds,
    );
  }

  // ── Items ────────────────────────────────────────────────────────────────────────
  // Note these hang off `sections/{sectionId}`, not off the template.

  getItems(sectionId: string): Promise<AppraisalTemplateItem[]> {
    return apiService.get<AppraisalTemplateItem[]>(`${this.baseUrl}/sections/${sectionId}/items`);
  }

  /** 409 when the same criterion or KPI is already on the template, in any section. */
  addItem(sectionId: string, data: CreateAppraisalTemplateItem): Promise<AppraisalTemplateItem> {
    return apiService.post<AppraisalTemplateItem>(
      `${this.baseUrl}/sections/${sectionId}/items`,
      data,
    );
  }

  updateItem(
    sectionId: string,
    itemId: string,
    data: UpdateAppraisalTemplateItem,
  ): Promise<AppraisalTemplateItem> {
    return apiService.put<AppraisalTemplateItem>(
      `${this.baseUrl}/sections/${sectionId}/items/${itemId}`,
      data,
    );
  }

  removeItem(sectionId: string, itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/sections/${sectionId}/items/${itemId}`);
  }

  reorderItems(sectionId: string, orderedItemIds: string[]): Promise<void> {
    return apiService.patch<void>(
      `${this.baseUrl}/sections/${sectionId}/items/reorder`,
      orderedItemIds,
    );
  }

  // ── Item grade bands ─────────────────────────────────────────────────────────────

  getItemGradeRanges(itemId: string): Promise<TemplateItemGradeRange[]> {
    return apiService.get<TemplateItemGradeRange[]>(`${this.baseUrl}/items/${itemId}/grade-ranges`);
  }

  /** Replace-all. Overlapping bands or a repeated grade are refused with 400. */
  saveItemGradeRanges(
    itemId: string,
    data: UpsertTemplateItemGradeRanges,
  ): Promise<TemplateItemGradeRange[]> {
    return apiService.put<TemplateItemGradeRange[]>(
      `${this.baseUrl}/items/${itemId}/grade-ranges`,
      data,
    );
  }

  /** Convenience read for the band editor's grade picker. */
  getActiveGradeDefinitions(): Promise<AppraisalGradeDefinition[]> {
    return apiService.get<AppraisalGradeDefinition[]>(`${this.baseUrl}/grade-definitions/active`);
  }
}

// ── Appraisal cycles ───────────────────────────────────────────────────────────────

/**
 * api/AppraisalCycle — the cycle, its target groups, and the operations that move it along.
 *
 * The lifecycle is deliberately three separate steps rather than one button: open the cycle,
 * check the coverage preview, then generate appraisals. Generation throws if any in-scope
 * employee has no template or a template conflict, which is exactly what the preview reports.
 */
class AppraisalCycleService {
  private readonly baseUrl = '/AppraisalCycle';

  getAll(): Promise<AppraisalCycle[]> {
    return apiService.get<AppraisalCycle[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<AppraisalCycle>> {
    return apiService.get<PagedResult<AppraisalCycle>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<AppraisalCycle> {
    return apiService.get<AppraisalCycle>(`${this.baseUrl}/${id}`);
  }

  getByYear(year: number): Promise<AppraisalCycle[]> {
    return apiService.get<AppraisalCycle[]>(`${this.baseUrl}/year/${year}`);
  }

  getByType(type: AppraisalType): Promise<AppraisalCycle[]> {
    return apiService.get<AppraisalCycle[]>(`${this.baseUrl}/type/${type}`);
  }

  getActive(): Promise<AppraisalCycle[]> {
    return apiService.get<AppraisalCycle[]>(`${this.baseUrl}/active`);
  }

  create(data: CreateAppraisalCycle): Promise<AppraisalCycle> {
    return apiService.post<AppraisalCycle>(this.baseUrl, data);
  }

  update(id: string, data: UpdateAppraisalCycle): Promise<AppraisalCycle> {
    return apiService.put<AppraisalCycle>(`${this.baseUrl}/${id}`, data);
  }

  /** Only a cycle that has never been opened can be deleted. */
  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }

  // ── Lifecycle ────────────────────────────────────────────────────────────────────

  /**
   * 400 when another non-closed cycle of the same type and year already covers any of these
   * employees — the message names the overlapping cycles. Also raises the "cycle is open"
   * notification to everyone in scope.
   */
  open(id: string): Promise<AppraisalCycle> {
    return apiService.post<AppraisalCycle>(`${this.baseUrl}/${id}/open`);
  }

  close(id: string): Promise<AppraisalCycle> {
    return apiService.post<AppraisalCycle>(`${this.baseUrl}/${id}/close`);
  }

  /** Creates the appraisal records themselves. Run the coverage preview first. */
  generateAppraisals(id: string): Promise<GenerateAppraisalsResult> {
    return apiService.post<GenerateAppraisalsResult>(`${this.baseUrl}/${id}/generate-appraisals`);
  }

  /** Repeat-safe: an identical unread reminder is skipped rather than duplicated. */
  sendDeadlineReminders(id: string): Promise<{ notificationsRaised: number }> {
    return apiService.post<{ notificationsRaised: number }>(
      `${this.baseUrl}/${id}/deadline-reminders`,
    );
  }

  // ── Reads ────────────────────────────────────────────────────────────────────────

  getProgress(id: string): Promise<AppraisalCycleProgress> {
    return apiService.get<AppraisalCycleProgress>(`${this.baseUrl}/${id}/progress`);
  }

  getCalendar(id: string): Promise<AppraisalCalendarEvent[]> {
    return apiService.get<AppraisalCalendarEvent[]>(`${this.baseUrl}/${id}/calendar`);
  }

  /** Bare employee ids — useful only as a count on this screen. */
  getEmployeesInScope(id: string): Promise<string[]> {
    return apiService.get<string[]>(`${this.baseUrl}/${id}/employees`);
  }

  /** Read-only simulation of generation. Page size defaults high; the summary is the point. */
  getCoveragePreview(id: string, pageNumber = 1, pageSize = 200): Promise<CoveragePreview> {
    return apiService.get<CoveragePreview>(`${this.baseUrl}/${id}/coverage-preview`, {
      pageNumber,
      pageSize,
    });
  }

  // ── Target groups ────────────────────────────────────────────────────────────────

  getTargets(cycleId: string): Promise<AppraisalCycleTarget[]> {
    return apiService.get<AppraisalCycleTarget[]>(`${this.baseUrl}/${cycleId}/targets`);
  }

  addTarget(cycleId: string, data: CreateAppraisalCycleTarget): Promise<AppraisalCycleTarget> {
    return apiService.post<AppraisalCycleTarget>(`${this.baseUrl}/${cycleId}/targets`, data);
  }

  updateTarget(
    cycleId: string,
    targetId: string,
    data: UpdateAppraisalCycleTarget,
  ): Promise<AppraisalCycleTarget> {
    return apiService.put<AppraisalCycleTarget>(
      `${this.baseUrl}/${cycleId}/targets/${targetId}`,
      data,
    );
  }

  removeTarget(cycleId: string, targetId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${cycleId}/targets/${targetId}`);
  }
}

// ── Target exclusions ──────────────────────────────────────────────────────────────

/**
 * api/AppraisalCycleTarget — only the exclusion endpoints are used here; target CRUD goes
 * through the cycle-nested routes above so the cycle is validated with it.
 */
class AppraisalCycleTargetService {
  private readonly baseUrl = '/AppraisalCycleTarget';

  getExclusions(targetId: string): Promise<AppraisalCycleTargetExclusion[]> {
    return apiService.get<AppraisalCycleTargetExclusion[]>(
      `${this.baseUrl}/${targetId}/exclusions`,
    );
  }

  addExclusion(
    targetId: string,
    data: CreateAppraisalCycleTargetExclusion,
  ): Promise<AppraisalCycleTargetExclusion> {
    return apiService.post<AppraisalCycleTargetExclusion>(
      `${this.baseUrl}/${targetId}/exclusions`,
      data,
    );
  }

  updateExclusion(
    targetId: string,
    exclusionId: string,
    data: UpdateAppraisalCycleTargetExclusion,
  ): Promise<AppraisalCycleTargetExclusion> {
    return apiService.put<AppraisalCycleTargetExclusion>(
      `${this.baseUrl}/${targetId}/exclusions/${exclusionId}`,
      data,
    );
  }

  removeExclusion(targetId: string, exclusionId: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${targetId}/exclusions/${exclusionId}`);
  }

  validate(id: string): Promise<{ isValid: boolean; message: string }> {
    return apiService.get<{ isValid: boolean; message: string }>(`${this.baseUrl}/${id}/validate`);
  }
}

// ── Cycle ↔ template assignments ───────────────────────────────────────────────────

/** api/AppraisalCycleTemplates — which forms a cycle may draw on, and in what priority. */
class AppraisalCycleTemplateService {
  private readonly baseUrl = '/AppraisalCycleTemplates';

  getByCycle(cycleId: string): Promise<AppraisalCycleTemplate[]> {
    return apiService.get<AppraisalCycleTemplate[]>(`${this.baseUrl}/by-cycle/${cycleId}`);
  }

  getByTemplate(templateId: string): Promise<AppraisalCycleTemplate[]> {
    return apiService.get<AppraisalCycleTemplate[]>(`${this.baseUrl}/by-template/${templateId}`);
  }

  create(data: CreateAppraisalCycleTemplate): Promise<AppraisalCycleTemplate> {
    return apiService.post<AppraisalCycleTemplate>(this.baseUrl, data);
  }

  update(id: string, data: UpdateAppraisalCycleTemplate): Promise<AppraisalCycleTemplate> {
    return apiService.put<AppraisalCycleTemplate>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  bulkAssign(
    cycleId: string,
    assignments: CreateAppraisalCycleTemplate[],
  ): Promise<AppraisalCycleTemplate[]> {
    return apiService.post<AppraisalCycleTemplate[]>(
      `${this.baseUrl}/bulk-assign/${cycleId}`,
      assignments,
    );
  }

  /** The single highest-priority template that applies to one employee. 404 when none does. */
  resolveForEmployee(cycleId: string, employeeId: string): Promise<AppraisalTemplate> {
    return apiService.get<AppraisalTemplate>(`${this.baseUrl}/resolve/${cycleId}/${employeeId}`);
  }
}

// ── Notifications ──────────────────────────────────────────────────────────────────
//
// `AppraisalNotificationService` lived here and was deleted in area 25 slice 14, having had no
// consumer since slice 11. The portal's unified feed (`GET employee-portal/my-notifications`)
// merges the appraisal store server-side alongside general, orientation and movement
// notifications, and dispatches mark-read BY SOURCE — so a second client-side appraisal bell
// would be a competing, partial view of the same rows. `api/AppraisalNotifications` still exists
// and is still read: by the portal aggregate, in C#, not from here.

export const appraisalSettingsService = new AppraisalSettingsService();
export const appraisalCriteriaService = new AppraisalCriteriaService();
export const appraisalGradeDefinitionService = new AppraisalGradeDefinitionService();
export const appraisalTemplateService = new AppraisalTemplateService();
export const appraisalCycleService = new AppraisalCycleService();
export const appraisalCycleTargetService = new AppraisalCycleTargetService();
export const appraisalCycleTemplateService = new AppraisalCycleTemplateService();
