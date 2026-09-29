import { apiService } from '../api.service';

/**
 * Developer Test Data — the HR seed as three buttons (Administration → HR → HR Settings).
 *
 * Shapes mirror `HrTestDataSeedService`'s DTOs in the API (enums arrive as strings). Each tier
 * includes the ones before it: Foundation (organisation, positions, reference data, HR workflows —
 * no people) → Workforce (~100 synthetic staff, leave vocabulary, salary scale) → Logins (the demo
 * personas, linked to that workforce).
 *
 * ⚠ Seeds the DEFAULT tenant, as `seed-hr-all` does. The Workforce and Logins tiers refuse when the
 * tenant already holds real (imported or hand-entered) TDC/ staff: the logins would otherwise be
 * linked to real people.
 */

export type HrTestDataTier = 'Foundation' | 'Workforce' | 'Logins';

export interface HrTestDataStep {
  name: string;
  present: boolean;
  /** A reconciliation with no skip probe — it runs on every pass and is never "present". */
  alwaysRuns: boolean;
}

export interface HrTestDataTierInfo {
  key: 'foundation' | 'workforce' | 'logins';
  title: string;
  description: string;
  complete: boolean;
  steps: HrTestDataStep[];
}

export interface HrTestDataPersona {
  username: string;
  positionTitle: string;
  purpose: string;
  roles: string[];
  exists: boolean;
  /** "TDC/00017 Efua Seidu" when the login exists and is linked. */
  linkedEmployee?: string | null;
}

export interface HrTestDataOutcome {
  name: string;
  result: 'Ran' | 'Skipped' | 'Failed';
  error?: string | null;
}

export interface HrTestDataRun {
  id: string;
  tier: HrTestDataTier;
  requestedBy: string;
  startedAt: string;
  finishedAt?: string | null;
  state: 'Running' | 'Succeeded' | 'Failed';
  error?: string | null;
  steps: HrTestDataOutcome[];
}

export interface HrTestDataStatus {
  enabled: boolean;
  disabledReason?: string | null;
  environment: string;
  tenantCode: string;
  realStaffCount: number;
  syntheticStaffBlockedReason?: string | null;
  tiers: HrTestDataTierInfo[];
  personas: HrTestDataPersona[];
  personaPassword: string;
  currentRun?: HrTestDataRun | null;
  lastRun?: HrTestDataRun | null;
  stateError?: string | null;
}

class HrTestDataService {
  private readonly baseUrl = '/administration/hr-test-data';

  getStatus(): Promise<HrTestDataStatus> {
    return apiService.get<HrTestDataStatus>(this.baseUrl);
  }

  /** Starts the seed in the background (202). 403 disabled/forbidden, 409 busy, 422 blocked. */
  run(tier: HrTestDataTier): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/${tier}`, {});
  }
}

export const hrTestDataService = new HrTestDataService();
