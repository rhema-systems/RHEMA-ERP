import { apiService } from '../api.service';
import type {
  ConcernReceipt,
  EmployeeRelationsConcern,
  ReportConcernRequest,
  TrackConcernRequest,
  AddConcernUpdateRequest,
} from '@/types/hr/employee-relations';

/**
 * The reporter's side of the anonymous-concern channel — area 9c slice 6.
 *
 * ⚠ **Kept apart from the HR-facing service on purpose.** These three calls are the only endpoints
 * in the module that take **no actor at all** — not from a payload, and not from the token either.
 * The service method behind `report` has no employee-id parameter to pass one to, so there is no
 * path by which the caller's identity could reach the row even by mistake. Splitting them into
 * their own file keeps that property visible instead of buried among HR's attributed writes.
 *
 * ⚠ **Rate limited to 5 requests a minute per caller** (`SensitivePolicy` on the server). A screen
 * that retries on failure, or polls, will trip it and the 429 will look like a defect.
 */
class EmployeeRelationsConcernPortalService {
  private readonly baseUrl = '/hr/employee-relations/concerns';

  /**
   * Reports a concern. Open to every internal user, and deliberately so — a whistleblowing channel
   * only some staff could use would not be one.
   *
   * ⚠ The receipt is the **only** time the retrieval code is ever shown. It is hashed on the way in
   * and cannot be recovered or reissued, by HR or by anyone. A screen that shows it must say so and
   * must not offer to send it anywhere.
   */
  report(payload: ReportConcernRequest): Promise<ConcernReceipt> {
    return apiService.post<ConcernReceipt>(this.baseUrl, payload);
  }

  /**
   * The reporter's own view of their report, unlocked by number and code.
   *
   * ⚠ Answers the same refusal whether the number or the code is wrong. That is not vagueness: a
   * message distinguishing them would confirm whether a given concern number exists, which is a
   * slow but perfectly good way to discover that somebody reported something. Do not try to
   * improve the error text by guessing which half failed.
   */
  track(payload: TrackConcernRequest): Promise<EmployeeRelationsConcern> {
    return apiService.post<EmployeeRelationsConcern>(`${this.baseUrl}/track`, payload);
  }

  /** The reporter adds to their own thread, still without giving a name. */
  addUpdate(payload: AddConcernUpdateRequest): Promise<EmployeeRelationsConcern> {
    return apiService.post<EmployeeRelationsConcern>(`${this.baseUrl}/track/updates`, payload);
  }
}

export const employeeRelationsConcernPortalService = new EmployeeRelationsConcernPortalService();
