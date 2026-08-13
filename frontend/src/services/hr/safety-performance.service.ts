import { apiService } from '../api.service';
import type {
  ShePerformanceSnapshot,
  ShePerformanceSnapshotSummary,
  ShePerformanceSnapshotCreateRequest,
  ShePerformanceSnapshotUpdateRequest,
  ShePerformanceSnapshotReviewRequest,
} from '@/types/hr/safety';

/**
 * Hand-reported SHE performance snapshots (KPI figures per period). Backend route:
 * api/safety/performance. HR-only.
 *
 * ⚠ Every figure is REPORTED by the SHE officer, not computed — label them that way on screen.
 * The server refuses (422): a duplicate snapshot number, a second snapshot for the same
 * period + location, and any edit after management review (the snapshot locks).
 */
class SafetyPerformanceService {
  private readonly baseUrl = '/safety/performance';

  getById(id: string): Promise<ShePerformanceSnapshot> {
    return apiService.get<ShePerformanceSnapshot>(`${this.baseUrl}/${id}`);
  }

  getByNumber(snapshotNumber: string): Promise<ShePerformanceSnapshot | null> {
    return apiService.get<ShePerformanceSnapshot | null>(
      `${this.baseUrl}/number/${encodeURIComponent(snapshotNumber)}`,
    );
  }

  getByYear(year: number): Promise<ShePerformanceSnapshotSummary[]> {
    return apiService.get<ShePerformanceSnapshotSummary[]>(`${this.baseUrl}/year/${year}`);
  }

  getByLocation(locationId: string): Promise<ShePerformanceSnapshotSummary[]> {
    return apiService.get<ShePerformanceSnapshotSummary[]>(
      `${this.baseUrl}/location/${locationId}`,
    );
  }

  getLatest(): Promise<ShePerformanceSnapshot | null> {
    return apiService.get<ShePerformanceSnapshot | null>(`${this.baseUrl}/latest`);
  }

  create(data: ShePerformanceSnapshotCreateRequest): Promise<ShePerformanceSnapshot> {
    return apiService.post<ShePerformanceSnapshot>(this.baseUrl, data);
  }

  /** Figures only; refused with 422 once the snapshot has been reviewed. */
  update(id: string, data: ShePerformanceSnapshotUpdateRequest): Promise<ShePerformanceSnapshot> {
    return apiService.put<ShePerformanceSnapshot>(`${this.baseUrl}/${id}`, data);
  }

  review(id: string, data: ShePerformanceSnapshotReviewRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/review`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const safetyPerformanceService = new SafetyPerformanceService();
export default safetyPerformanceService;
