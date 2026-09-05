import { describe, expect, it } from 'vitest';

import {
  buildCreateTenderDto,
  buildUpdateTenderDto,
  type PersistedTenderFormData,
} from './tender-form-payload';

const evaluationTemplateId = 'b24f8882-a7b0-4801-a7a1-65b1dbace513';

const formData: PersistedTenderFormData = {
  title: 'Supply of network equipment',
  description: 'Configured tender description',
  tenderType: 'ITB',
  submissionDeadline: '2026-09-30T12:00',
  openingDate: '2026-09-30T14:00',
  estimatedValue: 125000,
  currency: 'GHS',
  minimumPerformanceRating: 4,
  requiresPrequalification: true,
  allowPartialBids: true,
  priceWeightage: 40,
  qualityWeightage: 30,
  deliveryWeightage: 20,
  experienceWeightage: 10,
  evaluationCriteriaJson: '{"mandatory":true}',
  notes: 'Retain these notes',
  termsAndConditions: 'Retain these terms',
  documentRequirements: [
    { documentType: 'TaxClearance', isRequired: true },
  ],
  requiresAcceptanceDeclaration: true,
  evaluationTemplateId,
  useQCBSEvaluation: true,
  technicalWeight: 70,
  financialWeight: 30,
  minimumTechnicalScore: 75,
  items: [
    {
      lineNumber: 1,
      itemCode: 'NET-001',
      description: 'Network switch',
      quantity: 2,
      unitOfMeasure: 'EA',
    },
  ],
};

describe('tender form persistence payloads', () => {
  it('retains the selected evaluation template and every persisted setting on create', () => {
    const payload = buildCreateTenderDto(
      formData,
      '2bbf5386-526a-4ae2-b3dd-6eef2b09e4d2',
      true
    );

    expect(payload.evaluationTemplateId).toBe(evaluationTemplateId);
    expect(payload).toMatchObject({
      tenderType: 'ITB',
      useQCBSEvaluation: true,
      technicalWeight: 70,
      financialWeight: 30,
      minimumTechnicalScore: 75,
      requiresAcceptanceDeclaration: true,
      currency: 'GHS',
    });
    expect(payload.requiredDocuments).toBeDefined();
    expect(JSON.parse(payload.requiredDocuments ?? '[]')).toEqual(
      formData.documentRequirements
    );
    expect(payload.items).toEqual(formData.items);
  });

  it('retains the selected evaluation template and every persisted setting on update', () => {
    const payload = buildUpdateTenderDto(formData);

    expect(payload.evaluationTemplateId).toBe(evaluationTemplateId);
    expect(payload).toMatchObject({
      useQCBSEvaluation: true,
      technicalWeight: 70,
      financialWeight: 30,
      minimumTechnicalScore: 75,
      priceWeightage: 40,
      qualityWeightage: 30,
      deliveryWeightage: 20,
      experienceWeightage: 10,
      minimumPerformanceRating: 4,
    });
    expect(payload.requiredDocuments).toBeDefined();
    expect(JSON.parse(payload.requiredDocuments ?? '[]')).toEqual(
      formData.documentRequirements
    );
  });
});
