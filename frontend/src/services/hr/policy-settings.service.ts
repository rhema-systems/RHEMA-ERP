import { apiService } from '../api.service';
import type {
  CompanyHrPolicySettings,
  UpdateCompanyHrPolicySettingsRequest,
} from '@/types/hr/policy-settings';

/**
 * Company-wide HR policy settings. Backend route: `api/hr/policy-settings`.
 *
 * One record per tenant; GET resolves it (or coded defaults, without creating a row) and PUT
 * upserts it.
 *
 * ⚠ Read and write are gated differently. Read is SuperAdmin / TenantAdmin / HR; **write is
 * SuperAdmin / TenantAdmin only**, because three of these knobs are trust boundaries rather than
 * preferences — when HR may terminate without the Managing Director's signature (FR-HR-092), and
 * whether exceeding an approved budget or an authorised establishment blocks a hire (FR-HR-136).
 * An HR officer therefore sees this screen read-only and gets a 403 on save.
 */
class PolicySettingsService {
  private readonly baseUrl = '/hr/policy-settings';

  get(): Promise<CompanyHrPolicySettings> {
    return apiService.get<CompanyHrPolicySettings>(this.baseUrl);
  }

  update(data: UpdateCompanyHrPolicySettingsRequest): Promise<CompanyHrPolicySettings> {
    return apiService.put<CompanyHrPolicySettings>(this.baseUrl, data);
  }
}

export const policySettingsService = new PolicySettingsService();

/**
 * Strips the six keys the update command does not model. Everything else round-trips, so unlike
 * the company profile a spread is safe here — provided these six go.
 */
export function toUpdateRequest(
  settings: CompanyHrPolicySettings,
): UpdateCompanyHrPolicySettingsRequest {
  const { id, tenantId, createdAt, createdBy, updatedAt, updatedBy, ...rest } = settings;
  void id; void tenantId; void createdAt; void createdBy; void updatedAt; void updatedBy;
  return rest;
}
