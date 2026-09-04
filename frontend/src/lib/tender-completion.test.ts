import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PersistedTenderFormData } from './tender-form-payload';

const mocks = vi.hoisted(() => ({
  updateTender: vi.fn(),
}));

vi.mock('@/services/tenderService', () => ({
  tenderService: {
    updateTender: mocks.updateTender,
  },
}));

import { completeExistingTenderDraft } from './tender-completion';

const formData: PersistedTenderFormData = {
  title: 'Tender for PR-001',
  description: 'Supply of network equipment',
  tenderType: 'ITB',
  submissionDeadline: '2026-09-30T12:00',
  openingDate: '2026-09-30T14:00',
  estimatedValue: 125000,
  currency: 'GHS',
  minimumPerformanceRating: null,
  requiresPrequalification: false,
  allowPartialBids: false,
  priceWeightage: 40,
  qualityWeightage: 30,
  deliveryWeightage: 20,
  experienceWeightage: 10,
  evaluationCriteriaJson: '',
  notes: '',
  termsAndConditions: '',
  documentRequirements: [
    {
      documentType: 'TaxClearance',
      documentName: 'Tax Clearance Certificate',
      isRequired: true,
    },
  ],
  requiresAcceptanceDeclaration: false,
  evaluationTemplateId: 'template-1',
  useQCBSEvaluation: false,
  technicalWeight: 80,
  financialWeight: 20,
  minimumTechnicalScore: 70,
  items: [],
};

describe('completeExistingTenderDraft', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('awaits the serialized draft update before success and navigation', async () => {
    let resolveUpdate: (() => void) | undefined;
    mocks.updateTender.mockReturnValue(
      new Promise<void>((resolve) => {
        resolveUpdate = resolve;
      })
    );
    const onCompleted = vi.fn();

    const completion = completeExistingTenderDraft(
      'tender-1',
      formData,
      onCompleted
    );

    expect(mocks.updateTender).toHaveBeenCalledWith(
      'tender-1',
      expect.objectContaining({
        requiredDocuments: JSON.stringify(formData.documentRequirements),
      })
    );
    expect(onCompleted).not.toHaveBeenCalled();

    resolveUpdate?.();
    await completion;

    expect(onCompleted).toHaveBeenCalledOnce();
    expect(onCompleted).toHaveBeenCalledWith('tender-1');
  });

  it('does not navigate when the update fails', async () => {
    mocks.updateTender.mockRejectedValue(new Error('Update failed'));
    const onCompleted = vi.fn();

    await expect(
      completeExistingTenderDraft('tender-1', formData, onCompleted)
    ).rejects.toThrow('Update failed');

    expect(onCompleted).not.toHaveBeenCalled();
  });
});
