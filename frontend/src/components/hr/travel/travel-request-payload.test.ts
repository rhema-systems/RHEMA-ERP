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
  organizationUnitId: '',
  requiresVisa: true,
  requiresHealthClearance: false,
  amendmentReason: '',
  ...overrides,
});

describe('travel request payloads', () => {
  it('sends back the three fields the form does not show, so a save does not erase them (A8)', () => {
    const update = buildTravelRequestUpdate(values(), {
      id: 'req-1',
      approvedBudget: 20000,
      policyId: 'policy-1',
      groupTravelId: 'group-1',
    });
    expect(update.id).toBe('req-1');
    expect(update.approvedBudget).toBe(20000);
    expect(update.policyId).toBe('policy-1');
    expect(update.groupTravelId).toBe('group-1');
  });

  it('sends null, not undefined, for those three when the record has none', () => {
    const update = buildTravelRequestUpdate(values(), { id: 'req-1' });
    expect(update.approvedBudget).toBeNull();
    expect(update.policyId).toBeNull();
    expect(update.groupTravelId).toBeNull();
  });

  it('never sends an empty string where the API expects an id', () => {
    expect(buildTravelRequestFields(values({ organizationUnitId: '' }), false).organizationUnitId).toBeNull();
    expect(buildTravelRequestFields(values({ organizationUnitId: 'unit-1' }), false).organizationUnitId).toBe('unit-1');
  });

  it('keeps the reason for a change on an edit only', () => {
    expect(buildTravelRequestFields(values({ amendmentReason: 'Dates moved' }), true).amendmentReason).toBe('Dates moved');
    expect(buildTravelRequestFields(values({ amendmentReason: 'Dates moved' }), false).amendmentReason).toBeNull();
    expect(buildTravelRequestFields(values({ amendmentReason: '' }), true).amendmentReason).toBeNull();
  });

  it('derives international from the two countries', () => {
    expect(isInternationalTrip(GHANA, NIGERIA)).toBe(true);
    expect(isInternationalTrip(GHANA, GHANA)).toBe(false);
    expect(isInternationalTrip(GHANA, '')).toBe(false);
    expect(buildTravelRequestFields(values({ destinationCountryId: GHANA }), false).isInternational).toBe(false);
  });

  it('a desk create names the traveller and the role, never the initiator', () => {
    const create = buildTravelRequestCreate(values({ initiatedByRole: 'Manager' }), 'emp-9');
    expect(create.employeeId).toBe('emp-9');
    expect(create.initiatedByRole).toBe('Manager');
    expect('initiatedById' in create).toBe(false);
    expect(buildTravelRequestCreate(values(), 'emp-9').initiatedByRole).toBe('TravelDesk');
  });
});
