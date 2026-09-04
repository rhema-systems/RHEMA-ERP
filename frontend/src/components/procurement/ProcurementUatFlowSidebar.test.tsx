import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProcurementUatFlowSidebar } from './ProcurementUatFlowSidebar';

describe('ProcurementUatFlowSidebar', () => {
  it('keeps the default view focused on the immediate dependency, blocker, and next stage', () => {
    render(
      <ProcurementUatFlowSidebar
        currentStage="tender-documents-publication"
        recordReference="TND-2026-0003"
        stageStates={{
          'tender-rfq-preparation': {
            status: 'complete',
            context: 'Tender approved.',
          },
          'tender-documents-publication': {
            status: 'blocked',
            blockers: [
              'No effective published tender-document template is bound.',
            ],
            responsibleRole: 'Procurement Officer',
            href: '/procurement/tenders/tender-1/document-controls',
            actionLabel: 'Open document register',
          },
          'supplier-bidding': { status: 'not-started' },
        }}
      />
    );

    expect(screen.getByText('TND-2026-0003')).toBeInTheDocument();
    expect(screen.getByText('Prerequisite')).toBeInTheDocument();
    expect(screen.getByText('Current')).toBeInTheDocument();
    expect(screen.getByText('Next')).toBeInTheDocument();
    expect(
      screen.getByText(
        'No effective published tender-document template is bound.'
      )
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', {
        name: 'Current action: Open document register',
      })
    ).toHaveAttribute(
      'href',
      '/procurement/tenders/tender-1/document-controls'
    );
    expect(screen.getByText('View full process')).toBeInTheDocument();
    expect(screen.queryByText('2/12 complete')).not.toBeInTheDocument();
    expect(screen.getByText('1/12 complete')).toBeInTheDocument();
  });

  it('points to the next stage action after the current stage is complete', () => {
    render(
      <ProcurementUatFlowSidebar
        currentStage="award"
        stageStates={{
          award: { status: 'complete' },
          'purchase-order-commitment': {
            status: 'ready',
            href: '/procurement/purchase-orders/new',
            actionLabel: 'Create purchase order',
            responsibleRole: 'Procurement Officer',
          },
        }}
      />
    );

    expect(
      screen.getByRole('link', { name: 'Next action: Create purchase order' })
    ).toHaveAttribute('href', '/procurement/purchase-orders/new');
  });

  it('does not infer missing stages as complete', () => {
    render(
      <ProcurementUatFlowSidebar
        currentStage="budget-plan"
        stageStates={{ 'budget-plan': { status: 'in-progress' } }}
      />
    );

    expect(screen.getByText('0/12 complete')).toBeInTheDocument();
    expect(
      screen.getByText('This is the first procurement stage.')
    ).toBeInTheDocument();
  });
});
