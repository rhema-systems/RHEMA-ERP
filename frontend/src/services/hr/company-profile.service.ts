import { apiService } from '../api.service';
import type {
  CompanyProfile,
  CompanySealAsset,
  CompanySealAssetKind,
  UpdateCompanyProfileRequest,
} from '@/types/hr/company-profile';

/**
 * The tenant's company (legal-employer) profile. Backend route: `api/hr/company-profile`.
 *
 * One record per tenant, so there is no id in either direction — GET resolves the current tenant's
 * row (or Tenant/config-derived defaults when none is saved yet) and PUT upserts it.
 *
 * Gated on SuperAdmin / TenantAdmin / HR, read as well as write: the letterhead is not secret, but
 * the same record carries the tenant's TIN, VAT number and SSNIT employer number.
 */
class CompanyProfileService {
  private readonly baseUrl = '/hr/company-profile';

  get(): Promise<CompanyProfile> {
    return apiService.get<CompanyProfile>(this.baseUrl);
  }

  update(data: UpdateCompanyProfileRequest): Promise<CompanyProfile> {
    return apiService.put<CompanyProfile>(this.baseUrl, data);
  }

  // ── The seal and the signature ────────────────────────────────────────────
  //
  // ⚠ There is no URL for either. They live in private storage and a letter embeds the bytes, so
  // these methods manage WHICH image is in force, never where it is.

  /** Every seal and signature ever used, newest first — the audit trail. */
  getSealAssets(): Promise<CompanySealAsset[]> {
    return apiService.get<CompanySealAsset[]>(`${this.baseUrl}/seal-assets`);
  }

  /** Replaces the seal or signature, retiring whatever it supersedes. Admin-gated. */
  replaceSealAsset(kind: CompanySealAssetKind, file: File, reason?: string): Promise<CompanySealAsset> {
    const form = new FormData();
    form.append('file', file);
    if (reason) form.append('reason', reason);
    return apiService.post<CompanySealAsset>(`${this.baseUrl}/seal-assets/${kind}`, form);
  }

  /**
   * Withdraws the current one without replacing it.
   *
   * ⚠ A real operation, not a delete: a compromised seal has to stop being used before a
   * replacement exists. Letters then render without one, which beats stamping documents with an
   * image known to be bad.
   */
  retireSealAsset(kind: CompanySealAssetKind, reason?: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/seal-assets/${kind}/retire`, { reason: reason ?? null });
  }
}

export const companyProfileService = new CompanyProfileService();

/**
 * Builds the update payload from a fetched profile.
 *
 * The read model is NOT the write model: it carries `id`, `tenantId`, `legalFormName`,
 * `countryName`, `countryOfIncorporationName` and the BaseDto audit columns, none of which
 * `UpdateCompanyProfileDto` models. Spreading a profile into the PUT would post eight keys the
 * endpoint has no home for, so the mapping is written out.
 */
export function toUpdateRequest(profile: CompanyProfile): UpdateCompanyProfileRequest {
  return {
    legalName: profile.legalName,
    tradingName: profile.tradingName,
    legalForm: profile.legalForm,
    registrationNumber: profile.registrationNumber,
    dateOfIncorporation: profile.dateOfIncorporation,
    countryOfIncorporationId: profile.countryOfIncorporationId,

    taxIdentificationNumber: profile.taxIdentificationNumber,
    vatNumber: profile.vatNumber,
    ssnitEmployerNumber: profile.ssnitEmployerNumber,
    otherStatutoryRegistrations: profile.otherStatutoryRegistrations,

    registeredAddress: profile.registeredAddress,
    digitalAddress: profile.digitalAddress,
    city: profile.city,
    region: profile.region,
    countryId: profile.countryId,
    geoAreaId: profile.geoAreaId,
    postalCode: profile.postalCode,
    phonePrimary: profile.phonePrimary,
    hrEmail: profile.hrEmail,
    generalEmail: profile.generalEmail,
    website: profile.website,

    defaultSignatoryName: profile.defaultSignatoryName,
    defaultSignatoryTitle: profile.defaultSignatoryTitle,
    // ⚠ signatureImageUrl and companySealImageUrl are NOT sent. They are legacy read-only: a seal
    // is an instrument of authority, uploaded through the gate and versioned, not typed as a path. Nor is a logo
    // URL since lane 4c (F-55): the logo is uploaded the same way.
    offerAcceptanceInstructions: profile.offerAcceptanceInstructions,
    documentFooterText: profile.documentFooterText,
  };
}
