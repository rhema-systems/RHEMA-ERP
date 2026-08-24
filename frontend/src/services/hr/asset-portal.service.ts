import { apiService } from '../api.service';
import type {
  AssetAssignment,
  AssetAssignmentSummary,
  AssetRequisition,
  AssetRequisitionSummary,
  AssetTermsLetter,
  AssetTypeSummary,
  CreateAssetRequisitionRequest,
  EmployeeAssetSummary,
  UpdateAssetRequisitionRequest,
} from '@/types/hr/assets';

/**
 * The employee's own company assets. Backend route: `api/employee-portal`.
 *
 * ⚠ **No method here takes an employee id, and none ever should.** The register at `api/Assets`
 * exposes the same data through `assignments/employee/{id}` and it is correctly gated — but a
 * client that has to know its own employee id in order to ask a question can pass someone else's,
 * and that is how several of this module's authorization holes started. The portal derives the
 * employee from the JWT and the client has nothing to get wrong.
 *
 * Each route delegates server-side to the same service `api/Assets` calls, so the rules are the
 * ones the register enforces — including the one that surprises people: **acknowledgement is
 * refused for everybody but the assignment's own holder, HR included.** It is the employee's
 * testimony that they received the thing, not an administrative tick.
 */
class AssetPortalService {
  private readonly baseUrl = '/employee-portal';

  // ── What I hold ────────────────────────────────────────────────────────────

  /** Assets currently in the employee's hands. */
  getMyAssets(): Promise<AssetAssignmentSummary[]> {
    return apiService.get<AssetAssignmentSummary[]>(`${this.baseUrl}/assets`);
  }

  /** Everything they have ever held, returned assets included. */
  getMyAssetHistory(): Promise<AssetAssignmentSummary[]> {
    return apiService.get<AssetAssignmentSummary[]>(`${this.baseUrl}/assets/history`);
  }

  /** The landing counters and the three short lists behind them, in one call. */
  getMyAssetSummary(): Promise<EmployeeAssetSummary> {
    return apiService.get<EmployeeAssetSummary>(`${this.baseUrl}/assets/summary`);
  }

  getMyAsset(assignmentId: string): Promise<AssetAssignment> {
    return apiService.get<AssetAssignment>(`${this.baseUrl}/assets/${assignmentId}`);
  }

  /**
   * The employee signs for what they were given — AST-8.
   *
   * Refused for anyone but the holder, and refused once the assignment has been closed.
   */
  acknowledge(assignmentId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/assets/${assignmentId}/acknowledge`, {});
  }

  /** The responsibility-and-terms document for one of their own assignments — AST-5. */
  getTermsDocument(assignmentId: string): Promise<AssetTermsLetter> {
    return apiService.get<AssetTermsLetter>(`${this.baseUrl}/assets/${assignmentId}/terms-document`);
  }

  // ── What I have asked for ──────────────────────────────────────────────────

  /**
   * Requests the employee raised **and** requests raised for them.
   *
   * Both actor columns, deliberately: filtering on the requester alone means the employee a manager
   * raised a laptop request for is the only person who cannot see it.
   */
  getMyRequisitions(): Promise<AssetRequisitionSummary[]> {
    return apiService.get<AssetRequisitionSummary[]>(`${this.baseUrl}/asset-requisitions`);
  }

  getMyRequisition(id: string): Promise<AssetRequisition> {
    return apiService.get<AssetRequisition>(`${this.baseUrl}/asset-requisitions/${id}`);
  }

  /**
   * Raise a request — AST-6, and AST-6b when `beneficiaryEmployeeId` names somebody else.
   *
   * What comes back is a **draft**. Nothing reaches an approver until `submit`.
   */
  createRequisition(payload: CreateAssetRequisitionRequest): Promise<AssetRequisition> {
    return apiService.post<AssetRequisition>(`${this.baseUrl}/asset-requisitions`, payload);
  }

  updateRequisition(id: string, payload: UpdateAssetRequisitionRequest): Promise<AssetRequisition> {
    return apiService.put<AssetRequisition>(`${this.baseUrl}/asset-requisitions/${id}`, payload);
  }

  submitRequisition(id: string): Promise<AssetRequisition> {
    return apiService.post<AssetRequisition>(`${this.baseUrl}/asset-requisitions/${id}/submit`, {});
  }

  recallRequisition(id: string, reason?: string): Promise<AssetRequisition> {
    return apiService.post<AssetRequisition>(`${this.baseUrl}/asset-requisitions/${id}/recall`, {
      reason,
    });
  }

  withdrawRequisition(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/asset-requisitions/${id}`);
  }

  // ── The one picker that is not on the portal ───────────────────────────────

  /**
   * The asset-type catalogue for the request form.
   *
   * Lives on the register (`api/Assets/types`) rather than the portal because it is not
   * employee-scoped — it is the same list for everybody, and it carries only `[Authorize]`.
   */
  getAssetTypes(): Promise<AssetTypeSummary[]> {
    return apiService.get<AssetTypeSummary[]>('/Assets/types');
  }
}

export const assetPortalService = new AssetPortalService();
