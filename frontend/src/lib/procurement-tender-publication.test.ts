import { describe, expect, it } from 'vitest';
import { getTenderPublicationNotificationMessage, getTenderPublicationPresentation } from './procurement-tender-publication';

describe('tender publication presentation', () => {
  it('honours the server standard-route decision even when the tender has an NCT case', () => {
    const presentation = getTenderPublicationPresentation({
      tenderType: 'ITB',
      sourcingCaseId: 'standard-case',
      sourcingMethod: 'NationalCompetitiveTendering',
      usesControlledTenderLifecycle: false,
    });
    expect(presentation.requiresControlledPublication).toBe(false);
    expect(presentation.advancedControlLabel).toBeUndefined();
    expect(presentation.supplierAccessMessage).not.toContain(
      'publication controls'
    );
  });

  it('keeps release-only ITB publication simple and omits statutory controls', () => {
    const presentation = getTenderPublicationPresentation({
      tenderType: 'ITB',
    });

    expect(presentation.hasAdvancedSourcingCase).toBe(false);
    expect(presentation.requiresControlledPublication).toBe(false);
    expect(presentation.advancedControlLabel).toBeUndefined();
    expect(presentation.supplierAccessMessage).toContain('configured procurement route and eligibility requirements');
    expect(presentation.supplierAccessMessage).not.toContain('selected suppliers');
  });

  it('shows statutory fields only for an advanced NCT or ICT sourcing case', () => {
    const presentation = getTenderPublicationPresentation({
      tenderType: 'ITB',
      sourcingCaseId: 'case-1',
      sourcingMethod: 'NationalCompetitiveTendering',
    });

    expect(presentation.requiresControlledPublication).toBe(true);
    expect(presentation.usesStatutoryAdvertisement).toBe(true);
    expect(presentation.controlledPublicationHeading).toContain(
      'Statutory advertisement'
    );
    expect(presentation.advancedControlLabel).toBe('NCT / ICT Controls');
  });

  it('labels QBS and QCBS as a controlled solicitation rather than an NCT advertisement', () => {
    const presentation = getTenderPublicationPresentation({
      sourcingCaseId: 'case-2',
      sourcingMethod: 'QualityAndCostBasedSelection',
    });

    expect(presentation.requiresControlledPublication).toBe(true);
    expect(presentation.usesStatutoryAdvertisement).toBe(false);
    expect(presentation.controlledPublicationHeading).toBe(
      'Controlled solicitation and approved documents'
    );
    expect(presentation.publicationReferenceLabel).toBe(
      'Solicitation reference'
    );
  });

  it('accepts legacy numeric method payloads without exposing controls on release-only records', () => {
    expect(
      getTenderPublicationPresentation({ sourcingMethod: 1 })
        .requiresControlledPublication
    ).toBe(false);
    expect(
      getTenderPublicationPresentation({
        sourcingCaseId: 'case-3',
        sourcingMethod: 1,
      }).requiresControlledPublication
    ).toBe(true);
  });
  it.each([true, false])('does not describe open NCT access as invitation-only for controlled=%s', (usesControlledTenderLifecycle) => {
    const { supplierAccessMessage } = getTenderPublicationPresentation({ sourcingMethod: 'NationalCompetitiveTendering', usesControlledTenderLifecycle });
    expect(supplierAccessMessage).toContain('invitations are notifications, not an open-tender audience restriction');
    expect(supplierAccessMessage).not.toContain('selected suppliers');
  });
  it('explains zero invitation notifications without suggesting a zero-supplier tender audience', () => {
    expect(getTenderPublicationNotificationMessage(0)).toBe('No individual invitation notifications are configured.');
    expect(getTenderPublicationNotificationMessage(2, 1)).toBe('Individual invitation notifications will be sent to 2 invited suppliers and 1 additional email recipient.');
    expect(getTenderPublicationNotificationMessage(0, 1)).toBe('Individual invitation notifications will be sent to 1 additional email recipient.');
  });
});
