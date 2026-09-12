import { apiService } from '../api.service';
import type {
  EmployeeRelationsAnalytics,
  EmployeeRelationsResponder,
  UpsertResponderRequest,
  ResponderCoverage,
  EmployeeRelationsConcern,
  ConcernStatus,
  TriageConcernRequest,
  ReplyToConcernRequest,
  CloseConcernRequest,
  ConvertConcernRequest,
} from '@/types/hr/employee-relations';

/**
 * Employee-relations analytics — area 9c slice 8.
 *
 * ⚠ **One call for the whole page, and that is the design rather than a convenience.** Area 7
 * shipped a dashboard and an analytics page that disagreed about the same number, because each ran
 * its own query over a slightly different set. Every figure here comes from one materialised set on
 * the server, so a screen built from this one response cannot contradict itself — and there is
 * deliberately no per-figure endpoint to assemble a page from windows that do not match.
 */
class EmployeeRelationsAnalyticsService {
  private readonly baseUrl = '/hr/employee-relations/analytics';

  /** `from` / `to` filter on the FILED date. Omit both for all time. */
  get(range: { from?: string; to?: string } = {}): Promise<EmployeeRelationsAnalytics> {
    return apiService.get<EmployeeRelationsAnalytics>(this.baseUrl, range as Record<string, unknown>);
  }
}

/**
 * The responder matrix — area 9c slice 5, FR-HR-084.
 *
 * ⚠ **Resolving to nobody is a supported answer, not a failure.** `coverage` returns 200 with
 * `resolved: false` for every rung the matrix names no one for, because showing HR the gaps is the
 * admin screen's main job and a lookup that errored could not be rendered as a gap. The matrix is
 * opt-in: TDC has no org-authority data (no organisation unit has a head recorded), and this is
 * what replaced guessing at one. It holds **zero rows today**, so every rung on every scope is a
 * gap — the ordinary state, not a broken one.
 */
class EmployeeRelationsResponderService {
  private readonly baseUrl = '/hr/employee-relations/responders';

  /** Every row, tenant-wide defaults first. */
  getAll(): Promise<EmployeeRelationsResponder[]> {
    return apiService.get<EmployeeRelationsResponder[]>(this.baseUrl);
  }

  /** The rows for one scope. Omit the unit for the tenant-wide defaults. */
  getForScope(organizationUnitId?: string | null): Promise<EmployeeRelationsResponder[]> {
    return apiService.get<EmployeeRelationsResponder[]>(
      `${this.baseUrl}/scope`,
      organizationUnitId ? { organizationUnitId } : {},
    );
  }

  /*
   * ⚠ `GET .../responders/resolve?level=…` is deliberately NOT wrapped here.
   *
   * `coverage` returns a `ResponderResolution` for all six rungs of a scope, so a single-rung
   * resolve is a strict subset of a call this client already makes — the same "two endpoints that
   * provably return the same row" shape areas 19–23 found. Adding a method for it would create a
   * client function with no caller, which is exactly what slice 12's service-to-screen grep hunts.
   * Recorded there as a deletion candidate rather than wrapped for the sake of completeness.
   */

  /** Every rung's answer for one scope — the coverage row, and the only resolution read used. */
  getCoverage(organizationUnitId?: string | null): Promise<ResponderCoverage> {
    return apiService.get<ResponderCoverage>(
      `${this.baseUrl}/coverage`,
      organizationUnitId ? { organizationUnitId } : {},
    );
  }

  /**
   * ⚠ Refused when the period OVERLAPS an existing assignment for the same scope and rung — two
   * people answering one rung for one unit on one day would resolve arbitrarily. Consecutive
   * periods are fine; that is how a handover is recorded.
   */
  create(payload: UpsertResponderRequest): Promise<EmployeeRelationsResponder> {
    return apiService.post<EmployeeRelationsResponder>(this.baseUrl, payload);
  }

  update(id: string, payload: UpsertResponderRequest): Promise<EmployeeRelationsResponder> {
    return apiService.put<EmployeeRelationsResponder>(`${this.baseUrl}/${id}`, payload);
  }

  /**
   * ⚠ Gated on Discipline **Admin**, not Write — an HR-role user gets a 403 here by design. Who
   * answered a rung last year is a fact about the cases decided last year, so this is a soft delete
   * and the matrix is read by the audit trail as well as by the router.
   */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/**
 * The anonymous-concern inbox — area 9c slice 6, HR's side.
 *
 * ⚠ **The reporter's own three calls — report, track and add-an-update — are NOT here.** They
 * belong to the portal and arrive with slice 11. They are also the only endpoints in this module
 * that take no actor at all, and keeping them out of an HR-facing service keeps that property
 * obvious.
 *
 * ⚠ **Nothing this service returns identifies a reporter.** The guarantee is an absence, so it
 * cannot be shown by reading this file: `verify-slice6-anonymity.sql` checks the stored rows,
 * because a column could hold the reporter, go unmapped, and every API assertion would still pass.
 */
class EmployeeRelationsConcernService {
  private readonly baseUrl = '/hr/employee-relations/concerns';

  /** The triage queue. */
  getAll(status?: ConcernStatus): Promise<EmployeeRelationsConcern[]> {
    return apiService.get<EmployeeRelationsConcern[]>(this.baseUrl, status ? { status } : {});
  }

  getById(id: string): Promise<EmployeeRelationsConcern> {
    return apiService.get<EmployeeRelationsConcern>(`${this.baseUrl}/${id}`);
  }

  /** Records what HR made of it, and where that leaves it. */
  triage(id: string, payload: TriageConcernRequest): Promise<EmployeeRelationsConcern> {
    return apiService.post<EmployeeRelationsConcern>(`${this.baseUrl}/${id}/triage`, payload);
  }

  /**
   * HR replies on the thread — what makes anonymous intake useful rather than a suggestion box,
   * because the commonest outcome is that HR needs one more detail.
   */
  reply(id: string, payload: ReplyToConcernRequest): Promise<EmployeeRelationsConcern> {
    return apiService.post<EmployeeRelationsConcern>(`${this.baseUrl}/${id}/replies`, payload);
  }

  close(id: string, payload: CloseConcernRequest): Promise<EmployeeRelationsConcern> {
    return apiService.post<EmployeeRelationsConcern>(`${this.baseUrl}/${id}/close`, payload);
  }

  /**
   * Converts a concern into a named employee-relations case.
   *
   * ⚠ Never into a grievance, and the new case carries the subject and statement **only — not the
   * thread**, which may hold things the reporter said precisely because they were anonymous and
   * which would otherwise become readable by the case's primary party.
   */
  convert(id: string, payload: ConvertConcernRequest): Promise<EmployeeRelationsConcern> {
    return apiService.post<EmployeeRelationsConcern>(`${this.baseUrl}/${id}/convert`, payload);
  }
}

export const employeeRelationsAnalyticsService = new EmployeeRelationsAnalyticsService();
export const employeeRelationsResponderService = new EmployeeRelationsResponderService();
export const employeeRelationsConcernService = new EmployeeRelationsConcernService();
