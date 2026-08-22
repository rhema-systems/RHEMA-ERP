/**
 * The tenant's company (legal-employer) profile — one record per tenant.
 *
 * Transcribed from a live `GET api/hr/company-profile` (dev-harness/hr-tierb-tail,
 * `SLICE=1 node probe-ui-payloads.mjs`), not inferred from the endpoint name. Two things the probe
 * settled that a guess would have got wrong:
 *
 *   - `legalForm` serialises as a **string** ("LimitedCompany"), because the API registers
 *     JsonStringEnumConverter. A numeric union would have compiled and matched nothing.
 *   - the read model carries five fields the update command does not model — `id`, `tenantId`,
 *     `legalFormName`, `countryName`, `countryOfIncorporationName` — plus the BaseDto audit
 *     columns. Spreading a fetched profile straight into the PUT sends keys the endpoint has no
 *     home for, so `toUpdateRequest` below builds the payload explicitly.
 */

/**
 * Mirrors `CompanyLegalForm` (HREnums.cs:5343). Serialised as a string by the API.
 *
 * ⚠ Transcribed from the enum, after a first draft of this file invented three members that do not
 * exist (`CompanyLimitedByGuarantee`, `Cooperative`, `NonGovernmentalOrganisation`) and missed the
 * two that do (`Ngo`, `StatutoryBody`). A string union of plausible names compiles perfectly and
 * matches nothing — read the enum, never the endpoint name.
 */
export type CompanyLegalForm =
  | 'LimitedCompany'
  | 'PublicLimitedCompany'
  | 'Partnership'
  | 'SoleProprietorship'
  | 'Ngo'
  | 'StatutoryBody'
  | 'Other';

export interface CompanyProfile {
  id: string;
  tenantId: string;

  // Legal identity
  legalName: string;
  tradingName: string | null;
  legalForm: CompanyLegalForm;
  /** Display twin of `legalForm`. Identical to it today, because the enum already serialises as its name. */
  legalFormName: string;
  registrationNumber: string | null;
  dateOfIncorporation: string | null;
  countryOfIncorporationId: string | null;
  countryOfIncorporationName: string | null;

  // Statutory / tax
  taxIdentificationNumber: string | null;
  vatNumber: string | null;
  ssnitEmployerNumber: string | null;
  otherStatutoryRegistrations: string | null;

  // Registered address & contact
  registeredAddress: string | null;
  digitalAddress: string | null;
  city: string | null;
  region: string | null;
  countryId: string | null;
  countryName: string | null;
  postalCode: string | null;
  phonePrimary: string | null;
  hrEmail: string | null;
  generalEmail: string | null;
  website: string | null;

  // Document presentation
  defaultSignatoryName: string | null;
  defaultSignatoryTitle: string | null;
  signatureImageUrl: string | null;
  companySealImageUrl: string | null;
  logoUrl: string | null;
  offerAcceptanceInstructions: string | null;
  documentFooterText: string | null;

  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** `UpdateCompanyProfileDto`. No id — the service resolves the current tenant's row, or creates it. */
export interface UpdateCompanyProfileRequest {
  legalName: string;
  tradingName: string | null;
  legalForm: CompanyLegalForm;
  registrationNumber: string | null;
  dateOfIncorporation: string | null;
  countryOfIncorporationId: string | null;

  taxIdentificationNumber: string | null;
  vatNumber: string | null;
  ssnitEmployerNumber: string | null;
  otherStatutoryRegistrations: string | null;

  registeredAddress: string | null;
  digitalAddress: string | null;
  city: string | null;
  region: string | null;
  countryId: string | null;
  postalCode: string | null;
  phonePrimary: string | null;
  hrEmail: string | null;
  generalEmail: string | null;
  website: string | null;

  defaultSignatoryName: string | null;
  defaultSignatoryTitle: string | null;
  signatureImageUrl: string | null;
  companySealImageUrl: string | null;
  logoUrl: string | null;
  offerAcceptanceInstructions: string | null;
  documentFooterText: string | null;
}

/**
 * Display labels, carried client-side on purpose.
 *
 * The enum's `[Description]` attributes ("Limited Liability Company", "Non-Governmental
 * Organisation", …) never reach the client: `legalFormName` on the read model is just
 * `LegalForm.ToString()`, so it repeats the member name and adds nothing this union does not
 * already say. Nothing in the repository reads `[Description]` on any enum, in any module, so
 * these labels live here rather than being fetched.
 */
export const COMPANY_LEGAL_FORMS: { value: CompanyLegalForm; label: string }[] = [
  { value: 'LimitedCompany', label: 'Limited Liability Company' },
  { value: 'PublicLimitedCompany', label: 'Public Limited Company' },
  { value: 'Partnership', label: 'Partnership' },
  { value: 'SoleProprietorship', label: 'Sole Proprietorship' },
  { value: 'Ngo', label: 'Non-Governmental Organisation' },
  { value: 'StatutoryBody', label: 'Statutory / State Entity' },
  { value: 'Other', label: 'Other' },
];
