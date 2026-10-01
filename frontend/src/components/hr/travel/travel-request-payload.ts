/**
 * What the travel-request form sends, built in one place so it can be tested without a browser.
 *
 * ⚠ The update is a REPLACE, not a patch: the server writes every field it receives and an omitted
 * one as its default. Three fields are on no form — `approvedBudget`, `policyId`, `groupTravelId` —
 * so the form used to send them as nothing, and every save erased them: a group participant edited
 * on the form silently left the group (travel final closure, lane 0 — finding A8). They now go back
 * exactly as the record has them, until lane 1 takes them out of the update DTO altogether.
 */
import type {
  CreateStaffTravelRequest,
  StaffTravelPriority,
  StaffTravelPurpose,
  StaffTravelRequest,
  StaffTravelType,
  TravelInitiatorRole,
  TravelRiskLevel,
  UpdateStaffTravelRequest,
} from '@/types/hr/travel';

/** The form's values once its schema has parsed them. */
export interface TravelRequestFormOutput {
  employeeId?: string;
  initiatedByRole?: TravelInitiatorRole;
  travelType: StaffTravelType;
  travelPurpose: StaffTravelPurpose;
  purposeDescription?: string;
  priority: StaffTravelPriority;
  riskLevel: TravelRiskLevel;
  originCountryId: string;
  originCity: string;
  destinationCountryId: string;
  destinationCity: string;
  travelStartDate: string;
  travelEndDate: string;
  estimatedTotalCost: number;
  currencyCode: string;
  organizationUnitId?: string;
  requiresVisa: boolean;
  requiresHealthClearance: boolean;
  amendmentReason?: string;
}

export type TravelRequestFields = Omit<CreateStaffTravelRequest, 'employeeId' | 'initiatedByRole'>;

/** A trip is international when its two countries differ — never a separate answer on the form. */
export function isInternationalTrip(originCountryId?: string | null, destinationCountryId?: string | null) {
  return !!originCountryId && !!destinationCountryId && originCountryId !== destinationCountryId;
}

/** The fields every surface sends: the desk's create and edit, and the traveller's own. */
export function buildTravelRequestFields(v: TravelRequestFormOutput, isEdit: boolean): TravelRequestFields {
  return {
    travelType: v.travelType,
    travelPurpose: v.travelPurpose,
    purposeDescription: v.purposeDescription || undefined,
    // `null`, never `''` — an empty string fails the API's Guid binder.
    organizationUnitId: v.organizationUnitId || null,
    priority: v.priority,
    destinationCountryId: v.destinationCountryId,
    destinationCity: v.destinationCity,
    originCountryId: v.originCountryId,
    originCity: v.originCity,
    travelStartDate: v.travelStartDate,
    travelEndDate: v.travelEndDate,
    estimatedTotalCost: v.estimatedTotalCost,
    currencyCode: v.currencyCode,
    isInternational: isInternationalTrip(v.originCountryId, v.destinationCountryId),
    requiresVisa: v.requiresVisa,
    requiresHealthClearance: v.requiresHealthClearance,
    riskLevel: v.riskLevel,
    amendmentReason: isEdit ? v.amendmentReason || null : null,
  };
}

/** The desk raising a trip for someone else: the traveller and the role it was raised under. */
export function buildTravelRequestCreate(v: TravelRequestFormOutput, travellerId: string): CreateStaffTravelRequest {
  return {
    ...buildTravelRequestFields(v, false),
    employeeId: travellerId,
    // No initiator id: who raised it is stamped from the token. Only the ROLE is an input.
    initiatedByRole: v.initiatedByRole ?? 'TravelDesk',
  };
}

/** An edit: the form's fields, plus the three it does not show, sent back as they are. */
export function buildTravelRequestUpdate(
  v: TravelRequestFormOutput,
  existing: Pick<StaffTravelRequest, 'id' | 'approvedBudget' | 'policyId' | 'groupTravelId'>,
): UpdateStaffTravelRequest {
  return {
    ...buildTravelRequestFields(v, true),
    id: existing.id,
    approvedBudget: existing.approvedBudget ?? null,
    policyId: existing.policyId ?? null,
    groupTravelId: existing.groupTravelId ?? null,
  };
}
