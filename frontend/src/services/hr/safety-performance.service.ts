import { apiService } from '../api.service';
import type {
  ShePerformanceSnapshot,
  ShePerformanceSnapshotSummary,
  ShePerformanceSnapshotCreateRequest,
  ShePerformanceSnapshotUpdateRequest,
  ShePerformanceSnapshotReviewRequest,
  SheSnapshotPeriodType,
} from '@/types/hr/safety';
import type {
  SheComputedKpis,
  SheContractorRanking,
  SheDepartmentalCompliance,
  SheHazardHeatmap,
} from '@/types/hr/safety-kpi';

/**
 * SHE performance snapshots (KPI figures per period) and the slice-14 KPI computation
 * engine. Backend route: api/safety/performance. HR-only.
 *
 * Figures are hand-reported at creation; compute(id) rewrites every derivable figure from
 * the live registers (kpisComputedAt stamps the refresh). The server refuses (422): a
 * duplicate snapshot number, a second snapshot for the same period + location, and any
 * edit OR recompute after management review (the snapshot locks).
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

  // ── computed KPIs (slice 14) ──────────────────────────────────────────────

  /** Recomputes the snapshot's derivable figures from live data. 422 once reviewed. */
  compute(id: string): Promise<ShePerformanceSnapshot> {
    return apiService.post<ShePerformanceSnapshot>(`${this.baseUrl}/${id}/compute`, {});
  }

  /** What a snapshot for this period would compute, without persisting. Pass manHours
   * to see the frequency rates (LTIFR / TRIR / near-miss). */
  computePreview(
    periodType: SheSnapshotPeriodType,
    year: number,
    periodNumber?: number | null,
    locationId?: string | null,
    manHours?: number,
  ): Promise<SheComputedKpis> {
    const params = new URLSearchParams({ periodType, year: String(year) });
    if (periodNumber != null) params.set('periodNumber', String(periodNumber));
    if (locationId) params.set('locationId', locationId);
    if (manHours) params.set('manHours', String(manHours));
    return apiService.get<SheComputedKpis>(`${this.baseUrl}/compute/preview?${params.toString()}`);
  }

  /** Per-organization-unit compliance for a period (FR-SHE-230). */
  getDepartmentalCompliance(
    periodType: SheSnapshotPeriodType,
    year: number,
    periodNumber?: number | null,
  ): Promise<SheDepartmentalCompliance[]> {
    const params = new URLSearchParams({ periodType, year: String(year) });
    if (periodNumber != null) params.set('periodNumber', String(periodNumber));
    return apiService.get<SheDepartmentalCompliance[]>(
      `${this.baseUrl}/kpis/departmental?${params.toString()}`,
    );
  }

  /** Contractor SHE ranking for a period (FR-CON-001), best average score first. */
  getContractorRanking(
    periodType: SheSnapshotPeriodType,
    year: number,
    periodNumber?: number | null,
  ): Promise<SheContractorRanking[]> {
    const params = new URLSearchParams({ periodType, year: String(year) });
    if (periodNumber != null) params.set('periodNumber', String(periodNumber));
    return apiService.get<SheContractorRanking[]>(
      `${this.baseUrl}/kpis/contractor-ranking?${params.toString()}`,
    );
  }

  /** 5×5 likelihood × severity counts over the active hazard register (FR-SHE-232). */
  getHazardHeatmap(): Promise<SheHazardHeatmap> {
    return apiService.get<SheHazardHeatmap>(`${this.baseUrl}/kpis/hazard-heatmap`);
  }
}

export const safetyPerformanceService = new SafetyPerformanceService();
export default safetyPerformanceService;
