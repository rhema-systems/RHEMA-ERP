import { describe, expect, it } from 'vitest';
import {
  buildTravelRequestCreate,
  buildTravelRequestFields,
  buildTravelRequestUpdate,
  isInternationalTrip,
  type TravelRequestFormOutput,
} from './travel-request-payload';

const GHANA = '11111111-1111-1111-1111-111111111111';
const NIGERIA = '22222222-2222-2222-2222-222222222222';

const values = (overrides: Partial<TravelRequestFormOutput> = {}): TravelRequestFormOutput => ({
  travelType: 'International',
  travelPurpose: 'ClientMeeting',
  purposeDescription: '',
  priority: 'Routine',
  riskLevel: 'Medium',
  originCountryId: GHANA,
  originCity: 'Accra',
  destinationCountryId: NIGERIA,
  destinationCity: 'Lagos',
  travelStartDate: '2026-11-02',
  travelEndDate: '2026-11-06',
  estimatedTotalCost: 18500,
  currencyCode: 'GHS',
  requiresVisa: true,
  requiresHealthClearance: false,
  amendmentReason: '',
  ...overrides,
});

describe('travel request payloads', () => {
  it('an edit names the request and carries nothing the form does not show', () => {
    const update = buildTravelRequestUpdate(values(), { id: 'req-1' });
    expect(update.id).toBe('req-1');
  });

  it("never sends what the server or the group decides: the unit, international, the policy, the approved budget, the group (lane 1)", () => {
    const create = buildTravelRequestCreate(values(), 'emp-9');
    const update = buildTravelRequestUpdate(values(), { id: 'req-1' });
    for (const payload of [create, update, buildTravelRequestFields(values(), false)]) {
      expect('organizationUnitId' in payload).toBe(false);
      expect('isInternational' in payload).toBe(false);
      expect('policyId' in payload).toBe(false);
      expect('approvedBudget' in payload).toBe(false);
      // Slice 1c: group membership is the group's routes' — an edit that omitted the link used to
      // take the traveller out of their group (A8).
      expect('groupTravelId' in payload).toBe(false);
    }
  });

  it('keeps the reason for a change on an edit only', () => {
    expect(buildTravelRequestFields(values({ amendmentReason: 'Dates moved' }), true).amendmentReason).toBe('Dates moved');
    expect(buildTravelRequestFields(values({ amendmentReason: 'Dates moved' }), false).amendmentReason).toBeNull();
    expect(buildTravelRequestFields(values({ amendmentReason: '' }), true).amendmentReason).toBeNull();
  });

  it('tells international from the two countries, as the server does', () => {
    expect(isInternationalTrip(GHANA, NIGERIA)).toBe(true);
    expect(isInternationalTrip(GHANA, GHANA)).toBe(false);
    expect(isInternationalTrip(GHANA, '')).toBe(false);
  });

  it('a desk create names the traveller and the role, never the initiator', () => {
    const create = buildTravelRequestCreate(values({ initiatedByRole: 'Manager' }), 'emp-9');
    expect(create.employeeId).toBe('emp-9');
    expect(create.initiatedByRole).toBe('Manager');
    expect('initiatedById' in create).toBe(false);
    expect(buildTravelRequestCreate(values(), 'emp-9').initiatedByRole).toBe('TravelDesk');
  });
});
