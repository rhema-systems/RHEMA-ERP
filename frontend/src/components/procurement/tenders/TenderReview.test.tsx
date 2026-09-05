import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { TenderFormData } from '@/app/procurement/tenders/new/page';
import TenderReview from './TenderReview';

const formData: TenderFormData = {
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
  lots: [
    {
      id: 'lot-1',
      lotNumber: 1,
      lotCode: 'LOT-001',
      title: 'Network equipment',
      displayOrder: 0,
      items: [
        {
          id: 'item-1',
          lineNumber: 1,
          description: 'Network switch',
          quantity: 2,
        },
        {
          id: 'item-2',
          lineNumber: 2,
          description: 'Wireless access point',
          quantity: 4,
        },
      ],
    },
  ],
  items: [],
  documentRequirements: [],
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
};

describe('TenderReview', () => {
  it('reports PR-derived lots and their nested item counts', () => {
    render(<TenderReview formData={formData} />);

    expect(screen.getByText('1 lot(s) added')).toBeInTheDocument();
    expect(screen.getByText('LOT-001: Network equipment')).toBeInTheDocument();
    expect(screen.getByText('2 item(s)')).toBeInTheDocument();
    expect(screen.queryByText('0 lot(s) added')).not.toBeInTheDocument();
  });
});
