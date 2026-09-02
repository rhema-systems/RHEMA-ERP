import { describe, expect, it } from 'vitest';
import { getTenderPublicationPresentation } from './procurement-tender-publication';

describe('tender publication presentation', () => {
  it('keeps release-only ITB publication simple and omits statutory controls', () => {
    const presentation = getTenderPublicationPresentation({
      tenderType: 'ITB',
    });

    expect(presentation.hasAdvancedSourcingCase).toBe(false);
    expect(presentation.requiresControlledPublication).toBe(false);
    expect(presentation.advancedControlLabel).toBeUndefined();
    expect(presentation.supplierAccessMessage).toContain('selected suppliers');
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
});
