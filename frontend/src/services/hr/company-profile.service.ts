import { apiService } from '../api.service';
import type { CompanyProfile, UpdateCompanyProfileRequest } from '@/types/hr/company-profile';

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
    postalCode: profile.postalCode,
    phonePrimary: profile.phonePrimary,
    hrEmail: profile.hrEmail,
    generalEmail: profile.generalEmail,
    website: profile.website,

    defaultSignatoryName: profile.defaultSignatoryName,
    defaultSignatoryTitle: profile.defaultSignatoryTitle,
    signatureImageUrl: profile.signatureImageUrl,
    companySealImageUrl: profile.companySealImageUrl,
    logoUrl: profile.logoUrl,
    offerAcceptanceInstructions: profile.offerAcceptanceInstructions,
    documentFooterText: profile.documentFooterText,
  };
}
