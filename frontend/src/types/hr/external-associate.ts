import type { AuditFields } from './common';

/**
 * External associates — interview panellists, technical assessors and advisers who act for the
 * organisation without holding an ERP login.
 *
 * ⚠ Every field below was transcribed from a live payload (`SLICE=8 node probe-ui-payloads.mjs`),
 * not from the endpoint's name. That rule exists because a type written from a route compiles
 * perfectly and matches nothing — it cost areas 12 and 14 a slice each.
 */

/** Mirrors ExternalAssociateSummaryDto — the register rows, `/active`, and `/paged`. */
export interface ExternalAssociateSummary {
  id: string;
  associateNumber: string;
  /** Mr / Mrs / Dr … null on every live row today. */
  title?: string | null;
  firstName: string;
  lastName: string;
  /** Server-composed; do NOT rebuild it from the parts, the server includes the title. */
  fullName: string;
  email: string;
  phoneNumber: string;
  companyName?: string | null;
  role?: string | null;
  isActive: boolean;
}

/** Mirrors ExternalAssociateDto — the detail screen and every write response. */
export interface ExternalAssociate extends AuditFields {
  tenantId: string;
  associateNumber: string;
  title?: string | null;
  firstName: string;
  /** Serialised as `""`, never null, when there is no middle name. */
  middleName: string;
  lastName: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  companyName?: string | null;
  role?: string | null;
  /** Serialised as `""` when unset. Nothing uploads to it yet. */
  picturePath: string;
  /**
   * ⚠ Dormant. Grepped across every service, template and screen in slice 8: nothing reads either
   * of these. They are carried so a round trip does not wipe an existing value, and they are
   * deliberately absent from the form.
   */
  hasFixedModule: boolean;
  /** @see hasFixedModule */
  moduleId?: number | null;
  isActive: boolean;
  /** ISO date-time — when they were added to the register. */
  dateAdded: string;
  /**
   * How many interview panels they sit on. Nonzero means a delete will be refused, and the detail
   * screen says so before the button is pressed rather than after.
   *
   * ⚠ Present on the detail, update, activate and deactivate responses. The **list** rows are
   * `ExternalAssociateSummary` and carry no count — do not read one there.
   */
  interviewPanelCount: number;
}

/** Mirrors ExternalAssociateSearchResultDto — the recruitment panel picker's row. */
export interface ExternalAssociateSearchResult {
  id: string;
  associateNumber: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  role?: string | null;
  companyName?: string | null;
}

/** Mirrors CreateExternalAssociateDto. The number is minted server-side and never sent. */
export interface CreateExternalAssociateRequest {
  title?: string | null;
  firstName: string;
  middleName?: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  companyName?: string | null;
  role?: string | null;
  picturePath?: string | null;
  isActive: boolean;
}

/** Mirrors UpdateExternalAssociateDto (adds Id; the route id must match, or it is a 400). */
export interface UpdateExternalAssociateRequest extends CreateExternalAssociateRequest {
  id: string;
  /**
   * ⚠ Dormant, and passed straight back from the loaded record. `HasFixedModule` is a
   * non-nullable bool on the server DTO, so omitting it writes `false` — the edit form does not
   * offer these, which is precisely why it has to carry them.
   */
  hasFixedModule?: boolean;
  /** @see hasFixedModule */
  moduleId?: number | null;
}
