import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type {
  DocumentRequirement,
  TenderFormData,
} from '@/app/procurement/tenders/new/page';
import TenderDocuments from './TenderDocuments';

const requirement: DocumentRequirement = {
  documentType: 'TaxClearance',
  documentName: 'Tax Clearance Certificate',
  isRequired: true,
  description: 'Current tax clearance certificate',
  maxFileSizeMB: 10,
  allowedFileTypes: 'PDF',
};

const formData = (
  documentRequirements: DocumentRequirement[]
): TenderFormData => ({
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
  useQCBSEvaluation: false,
  technicalWeight: 80,
  financialWeight: 20,
  minimumTechnicalScore: 70,
  evaluationTemplateId: 'template-1',
  evaluationTemplateName: 'Standard tender evaluation',
  lots: [],
  items: [],
  documentRequirements,
  requiresAcceptanceDeclaration: false,
  acceptanceDeclarationFile: null,
  acceptanceDeclarationDocumentName: '',
  proposalTemplateFile: null,
  proposalTemplateName: '',
  documents: [],
  fees: [],
  invitations: [],
  invitedBusinessPartnerIds: [],
  sendNotifications: false,
});

describe('TenderDocuments template recovery', () => {
  it('offers the template in edit mode only when requirements are empty', () => {
    const updateFormData = vi.fn();
    const { rerender } = render(
      <TenderDocuments
        formData={formData([])}
        updateFormData={updateFormData}
        tenderId="tender-1"
        isEditMode
      />
    );

    expect(
      screen.getByRole('button', { name: 'Load Template' })
    ).toBeInTheDocument();

    rerender(
      <TenderDocuments
        formData={formData([requirement])}
        updateFormData={updateFormData}
        tenderId="tender-1"
        isEditMode
      />
    );

    expect(
      screen.queryByRole('button', { name: 'Load Template' })
    ).not.toBeInTheDocument();
  });
});
