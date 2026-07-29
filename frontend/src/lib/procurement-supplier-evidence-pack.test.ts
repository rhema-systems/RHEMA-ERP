import { describe, expect, it } from 'vitest';

import {
  emptySupplierEvidenceRequirement,
  validateSupplierEvidencePack,
} from './procurement-supplier-evidence-pack';
import type { SaveSupplierEvidencePack } from '@/types/procurement-supplier-evidence-pack';

const pack = (): SaveSupplierEvidencePack => ({
  packCode: 'TDC-GOODS',
  name: 'TDC Goods evidence',
  category: 'Goods',
  effectiveFromUtc: '2026-08-01T00:00:00Z',
  sourceConfigurationProfileId: 'profile-1',
  workflowDefinitionId: 'workflow-1',
  requirements: [
    {
      ...emptySupplierEvidenceRequirement(),
      requirementCode: 'GRA-CLEARANCE',
      name: 'GRA clearance',
      documentType: 'Tax Clearance Certificate',
      approvalStepName: 'Supplier evidence review',
      minimumRemainingDays: 30,
      validityMode: 'MinimumRemainingDays',
    },
  ],
});

describe('supplier evidence-pack validation', () => {
  it('accepts a category-aware document requirement with exact workflow and validity metadata', () => {
    expect(validateSupplierEvidencePack(pack())).toBeNull();
  });

  it('requires a mandatory classification for Works', () => {
    const value = pack();
    value.category = 'Works';

    expect(validateSupplierEvidencePack(value)).toBe(
      'Works needs a mandatory classification requirement.'
    );
  });

  it('rejects duplicate requirement codes and missing remaining-validity days', () => {
    const value = pack();
    value.requirements.push({ ...value.requirements[0] });
    expect(validateSupplierEvidencePack(value)).toContain('duplicated');

    value.requirements = [
      { ...value.requirements[0], minimumRemainingDays: undefined },
    ];
    expect(validateSupplierEvidencePack(value)).toContain(
      'positive remaining-validity days'
    );
  });
});
