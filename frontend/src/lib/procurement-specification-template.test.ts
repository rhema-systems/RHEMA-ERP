import { describe, expect, it } from 'vitest';

import {
  procurementSpecificationTemplateActions,
  validateProcurementSpecificationTemplate,
} from './procurement-specification-template';
import type { SaveProcurementSpecificationTemplate } from '@/types/procurement-specification-template';

const valid = (): SaveProcurementSpecificationTemplate => ({
  templateCode: 'GOODS-STD',
  name: 'Standard goods specification',
  kind: 'Goods',
  isDefault: false,
  effectiveFromUtc: '2026-07-21T00:00:00.000Z',
  purpose: 'Purpose',
  functionalAndPerformanceRequirements: 'Performance',
  processAndMaterialsRequirements: 'Materials',
  dimensionsAndMarkingRequirements: 'Dimensions',
  testingAndInspectionRequirements: 'Testing',
  applicableStandards: 'Standards',
  deliverables: 'Deliverables',
  acceptanceCriteria: 'Acceptance',
});

describe('procurement specification templates', () => {
  it('requires all eight standard sections', () => {
    const value = valid();
    value.acceptanceCriteria = '';
    expect(validateProcurementSpecificationTemplate(value)).toContain(
      'Acceptance criteria'
    );
  });

  it('exposes lifecycle actions only for the valid status', () => {
    expect(
      procurementSpecificationTemplateActions({ status: 'Draft' })
    ).toMatchObject({
      canEdit: true,
      canSubmit: true,
      canPublish: false,
    });
    expect(
      procurementSpecificationTemplateActions({ status: 'Published' })
    ).toMatchObject({ canClone: true, canRetire: true, canEdit: false });
  });
});
