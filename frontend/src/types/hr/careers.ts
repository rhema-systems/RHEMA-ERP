// The candidate-facing careers surface: the anonymous public board (api/public) and the
// authenticated candidate self-service (api/candidate, Candidate role on the main JWT scheme).
// Written from RecruitmentDTOs.cs and HREnums.cs, source-verified 2026-08-30.

import type { PublicVacancy } from '@/types/hr/recruitment';
import type {
  Gender,
  LanguageProficiency,
  PreferredWorkArrangement,
  ProficiencyLevel,
  QualificationType,
  WorkAuthorizationStatus,
} from '@/types/hr/recruitment-pipeline';

export type { PublicVacancy, LanguageProficiency };
// LanguageProficiency — HREnums.cs (Basic=1 … Native=5); one definition, shared with the HR tab.
export { LANGUAGE_PROFICIENCIES } from '@/types/hr/recruitment-pipeline';

// ── Public catalogues (api/public/catalogue/*, anonymous, X-Tenant-Id) ────────

export interface PublicCatalogueSkill {
  id: string;
  name: string;
}

/** `type` is the row's kind (QualificationType name) — the form filters the list by it. */
export interface PublicCatalogueQualification {
  id: string;
  name: string;
  type: QualificationType;
}

export interface PublicCatalogueLanguage {
  id: string;
  name: string;
  code?: string | null;
}

export interface PublicCatalogueIdentificationType {
  id: string;
  name: string;
  code?: string | null;
}

export interface PublicCatalogueCurrency {
  code: string;
  name: string;
  symbol?: string | null;
}

// The subset of ApplicationStatus the candidate surface renders.
export type CandidateApplicationStatus =
  | 'Draft'
  | 'New'
  | 'Submitted'
  | 'UnderReview'
  | 'Shortlisted'
  | 'InterviewScheduled'
  | 'OfferExtended'
  | 'Hired'
  | 'Withdrawn'
  | 'Rejected';

// ── Profile ────────────────────────────────────────────────────────────────

/** Child rows carry the DB row id, or the all-zeros guid for new rows — the whole list is a
 * replace-set: omitting a row deletes it. */
export const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

export interface CareersWorkHistory {
  id: string;
  institutionName: string;
  positionHeld: string;
  startDate: string;
  endDate?: string | null;
  responsibilities?: string | null;
  reasonForLeaving?: string | null;
}

export interface CareersQualification {
  id: string;
  qualificationType: QualificationType;
  qualificationName: string;
  qualificationId?: string | null;
  institution: string;
  dateAwarded: string;
  grade?: string | null;
}

export interface CareersReferee {
  id: string;
  fullName: string;
  position: string;
  organization: string;
  email: string;
  phone: string;
  relationship: string;
  yearsKnown?: number | null;
}

export interface CareersSkill {
  id: string;
  skillName: string;
  skillId?: string | null;
  proficiency?: ProficiencyLevel | null;
  yearsOfExperience?: number | null;
  isCertified: boolean;
  certificationName?: string | null;
  // Round 3, lane C1 — the server clears all four together when `isCertified` is off.
  certificationNumber?: string | null;
  certifyingBody?: string | null;
  certificationExpiryDate?: string | null;
}

/**
 * Either `languageId` (a catalogue row; the name is mirrored back) or a typed `languageName`.
 * The server refuses a row with neither, and an id it does not hold.
 */
export interface CareersLanguage {
  id: string;
  languageId?: string | null;
  languageCode?: string | null;
  languageName?: string | null;
  proficiency: LanguageProficiency;
}

export interface CareersInterest {
  id: string;
  detail: string;
}

export interface CandidateDocument {
  id: string;
  jobCandidateId: string;
  documentType: string;
  fileName: string;
  filePath?: string | null;
  uploadDate: string;
  /** What the file is, in the candidate's words (round 3, lane C1). */
  description?: string | null;
}

export interface CandidateProfile {
  accountId: string;
  candidateId?: string | null;
  email: string;
  isEmailVerified: boolean;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  phone: string;
  alternatePhone?: string | null;
  dateOfBirth?: string | null;
  gender?: Gender | null;
  city?: string | null;
  countryId?: string | null;
  countryName?: string | null;
  postalAddress?: string | null;
  digitalAddress?: string | null;
  linkedInProfile?: string | null;
  portfolioUrl?: string | null;
  gitHubUrl?: string | null;
  headline?: string | null;
  professionalSummary?: string | null;
  currentJobTitle?: string | null;
  currentEmployer?: string | null;
  totalYearsExperience?: number | null;
  noticePeriodDays?: number | null;
  availableFrom?: string | null;
  preferredWorkArrangement: PreferredWorkArrangement;
  expectedSalaryMin?: number | null;
  expectedSalaryMax?: number | null;
  expectedSalaryCurrency?: string | null;
  workAuthorizationStatus: WorkAuthorizationStatus;
  // National identity (round 3, lane C1)
  nationalIdTypeId?: string | null;
  nationalIdTypeName?: string | null;
  nationalIdNumber?: string | null;
  nationalIdExpiryDate?: string | null;
  cvFilePath?: string | null;
  profilePhotoUrl?: string | null;
  /** A photograph is on file — fetch `GET /candidate/profile/photo` only then (round 3, lane C2). */
  hasPhoto: boolean;
  isInTalentPool: boolean;
  workHistories: CareersWorkHistory[];
  qualifications: CareersQualification[];
  referees: CareersReferee[];
  skills: CareersSkill[];
  languages: CareersLanguage[];
  interests: CareersInterest[];
  documents: CandidateDocument[];
}

/** UpdateCandidatePortalProfileDto — every child list is the WHOLE set (replace-set). */
export interface SaveCandidateProfilePayload {
  firstName: string;
  middleName?: string | null;
  lastName: string;
  phone: string;
  alternatePhone?: string | null;
  dateOfBirth?: string | null;
  gender?: Gender | null;
  city?: string | null;
  /** Optional since 2026-09-14 — send null for "no country". The old EMPTY_GUID sentinel is retired. */
  countryId?: string | null;
  postalAddress?: string | null;
  digitalAddress?: string | null;
  linkedInProfile?: string | null;
  portfolioUrl?: string | null;
  gitHubUrl?: string | null;
  headline?: string | null;
  professionalSummary?: string | null;
  currentJobTitle?: string | null;
  currentEmployer?: string | null;
  totalYearsExperience?: number | null;
  noticePeriodDays?: number | null;
  availableFrom?: string | null;
  preferredWorkArrangement: PreferredWorkArrangement;
  expectedSalaryMin?: number | null;
  expectedSalaryMax?: number | null;
  expectedSalaryCurrency?: string | null;
  workAuthorizationStatus: WorkAuthorizationStatus;
  /** Round 3, lane C1: refused (400) for a type the tenant does not accept. */
  nationalIdTypeId?: string | null;
  nationalIdNumber?: string | null;
  nationalIdExpiryDate?: string | null;
  /** ⚠ Ignored by the server — the photograph is set by uploading it. Kept optional for old callers. */
  profilePhotoUrl?: string | null;
  isInTalentPool: boolean;
  workHistories: CareersWorkHistory[];
  qualifications: CareersQualification[];
  referees: CareersReferee[];
  skills: CareersSkill[];
  languages: CareersLanguage[];
  interests: CareersInterest[];
}

// ── Applications ───────────────────────────────────────────────────────────

export interface CandidateApplicationSummary {
  applicationId: string;
  applicationNumber: string;
  trackingToken?: string | null;
  jobTitle: string;
  vacancyNumber: string;
  departmentName?: string | null;
  locationName?: string | null;
  employmentTypeLabel: string;
  status: CandidateApplicationStatus;
  statusLabel: string;
  applicationDate: string;
  shortlistedDate?: string | null;
  withdrawnDate?: string | null;
  rejectedDate?: string | null;
  canWithdraw: boolean;
}

export interface ApplyPayload {
  vacancyId: string;
  coverLetter?: string | null;
  yearsOfExperience?: number | null;
  availableFrom?: string | null;
  /**
   * The advert the candidate came through (`/careers/{vacancyId}?posting={id}`; round 3, lane A).
   * The server derives the source from its channel; without it the source is the company website.
   */
  jobPostingId?: string | null;
  /** ⚠ Ignored by the server since lane A — the source is derived, never typed by the candidate. */
  source?: string;
  addToTalentPool?: boolean;
}

export interface CandidateDashboard {
  profile: CandidateProfile;
  applications: CandidateApplicationSummary[];
  totalApplications: number;
  activeApplications: number;
  shortlistedCount: number;
}

// ── Offers ─────────────────────────────────────────────────────────────────

export interface CandidateOffer {
  id: string;
  offerNumber: string;
  positionTitle?: string | null;
  departmentName?: string | null;
  reportsToTitle?: string | null;
  gradeTitle?: string | null;
  locationName?: string | null;
  employmentType?: string | null;
  contractDurationMonths?: number | null;
  workMode?: string | null;
  baseSalary?: number | null;
  currencyCode?: string | null;
  bonus?: number | null;
  bonusTerms?: string | null;
  commission?: number | null;
  commissionStructure?: string | null;
  benefits?: string | null;
  probationPeriodMonths?: number | null;
  noticePeriodMonths?: number | null;
  annualLeaveDays?: number | null;
  weeklyHours?: number | null;
  ndaRequired: boolean;
  proposedStartDate?: string | null;
  expiryDate?: string | null;
  additionalTerms?: string | null;
  offerLetterPath?: string | null;
  isConditional: boolean;
  offerStatus: string;
  candidateResponseNotes?: string | null;
  acceptedDate?: string | null;
  declinedDate?: string | null;
}

/** JobOfferStatus values the respond endpoint accepts. */
export type OfferResponseChoice = 'Accepted' | 'Negotiating' | 'Declined';

/**
 * CandidateOfferSummaryDto — what the anonymous tokenised link sees. Deliberately lean: the
 * money and dates arrive pre-formatted, and validate answers 200 even for a dead token, with
 * the state carried in the two flags.
 */
export interface OfferTokenValidation {
  offerId: string;
  offerNumber: string;
  positionTitle: string;
  baseSalary?: string | null;
  startDate?: string | null;
  expiryDate?: string | null;
  additionalTerms?: string | null;
  isConditional: boolean;
  offerLetterUrl?: string | null;
  tokenExpired: boolean;
  tokenAlreadyUsed: boolean;
}

export interface OfferLetter {
  offerId: string;
  offerNumber: string;
  candidateName: string;
  positionTitle: string;
  subject: string;
  htmlBody: string;
}

// ── Registration ───────────────────────────────────────────────────────────

export interface CandidateRegisterPayload {
  firstName: string;
  lastName: string;
  username: string;
  email: string;
  phoneNumber: string;
  password: string;
  recaptchaToken?: string | null;
}

export interface PublicTenant {
  id: string;
  name: string;
  code?: string | null;
}
