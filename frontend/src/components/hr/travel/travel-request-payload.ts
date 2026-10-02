/**
 * What the travel-request form sends, built in one place so it can be tested without a browser.
 *
 * ⚠ The update is a REPLACE, not a patch: the server writes every field it receives and an omitted
 * one as its default. Since lane 1 of the travel final closure the server owns the organisation unit
 * (the traveller's own), whether the trip is international (the two countries) and the policy, the
 * approved budget is the approver's, and the group a trip is on is the group's own routes' (slice 1c)
 * — none of the five is sent. A group participant edited on the form used to leave the group, because
 * the edit wrote the link it had not been sent (finding A8); an edit now cannot touch the link at all.
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
  requiresVisa: boolean;
  requiresHealthClearance: boolean;
  amendmentReason?: string;
}

export type TravelRequestFields = Omit<CreateStaffTravelRequest, 'employeeId' | 'initiatedByRole'>;

/**
 * A trip is international when its two countries differ — the server's rule too, which decides it
 * from the same two fields. The form uses this only to show the notice and offer the visa switch.
 */
export function isInternationalTrip(originCountryId?: string | null, destinationCountryId?: string | null) {
  return !!originCountryId && !!destinationCountryId && originCountryId !== destinationCountryId;
}

/** The fields every surface sends: the desk's create and edit, and the traveller's own. */
export function buildTravelRequestFields(v: TravelRequestFormOutput, isEdit: boolean): TravelRequestFields {
  return {
    travelType: v.travelType,
    travelPurpose: v.travelPurpose,
    purposeDescription: v.purposeDescription || undefined,
    priority: v.priority,
    destinationCountryId: v.destinationCountryId,
    destinationCity: v.destinationCity,
    originCountryId: v.originCountryId,
    originCity: v.originCity,
    travelStartDate: v.travelStartDate,
    travelEndDate: v.travelEndDate,
    estimatedTotalCost: v.estimatedTotalCost,
    currencyCode: v.currencyCode,
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

/** An edit: the form's fields and the request's id — nothing the form does not show. */
export function buildTravelRequestUpdate(
  v: TravelRequestFormOutput,
  existing: Pick<StaffTravelRequest, 'id'>,
): UpdateStaffTravelRequest {
  return {
    ...buildTravelRequestFields(v, true),
    id: existing.id,
  };
}
