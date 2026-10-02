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
  it('sends the group link back, so a save does not take the traveller out of their group (A8)', () => {
    const update = buildTravelRequestUpdate(values(), { id: 'req-1', groupTravelId: 'group-1' });
    expect(update.id).toBe('req-1');
    expect(update.groupTravelId).toBe('group-1');
  });

  it('sends null, not undefined, for the group link when the record has none', () => {
    expect(buildTravelRequestUpdate(values(), { id: 'req-1' }).groupTravelId).toBeNull();
  });

  it("never sends what the server decides: the unit, international, the policy, the approved budget (lane 1)", () => {
    const create = buildTravelRequestCreate(values(), 'emp-9');
    const update = buildTravelRequestUpdate(values(), { id: 'req-1', groupTravelId: null });
    for (const payload of [create, update, buildTravelRequestFields(values(), false)]) {
      expect('organizationUnitId' in payload).toBe(false);
      expect('isInternational' in payload).toBe(false);
      expect('policyId' in payload).toBe(false);
      expect('approvedBudget' in payload).toBe(false);
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
