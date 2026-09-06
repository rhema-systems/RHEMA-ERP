import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProcurementUatFlowSidebar } from './ProcurementUatFlowSidebar';

describe('ProcurementUatFlowSidebar', () => {
  it('provides the document register action for an approved tender awaiting publication', () => {
    render(
      <ProcurementUatFlowSidebar
        currentStage="tender-documents-publication"
        stageStates={{
          'tender-rfq-preparation': { status: 'complete' },
          'tender-documents-publication': {
            status: 'ready',
            href: '/procurement/tenders/tender-1/document-controls',
            actionLabel: 'Open document register',
          },
          'supplier-bidding': { status: 'not-started' },
        }}
      />
    );

    expect(
      screen.getByRole('link', {
        name: 'Current action: Open document register',
      })
    ).toHaveAttribute(
      'href',
      '/procurement/tenders/tender-1/document-controls'
    );
    const documentAction = within(
      screen.getByRole('group', {
        name: 'Current: Controlled documents and publication',
      })
    ).getByRole('link', { name: 'Current action: Open document register' });
    expect(documentAction).toHaveAttribute('data-slot', 'button');
    expect(
      screen.getByRole('group', {
        name: 'Current: Controlled documents and publication',
      })
    ).toHaveAttribute('aria-current', 'step');
    expect(
      screen.getByRole('group', {
        name: 'Current: Controlled documents and publication',
      })
    ).toHaveClass('border-l-4', 'bg-primary/5');
    expect(
      screen.getByRole('group', { name: 'Next: Supplier bidding' })
    ).not.toHaveClass('border-l-4');
    expect(documentAction).toHaveClass(
      'min-h-11',
      'shadow-md',
      'cursor-pointer'
    );
    expect(
      within(
        screen.getByRole('group', { name: 'Next: Supplier bidding' })
      ).queryByRole('link')
    ).not.toBeInTheDocument();
  });

  it('keeps earlier document-register access in the full flow after moving to bidding', () => {
    render(
      <ProcurementUatFlowSidebar
        currentStage="supplier-bidding"
        stageStates={{
          'tender-documents-publication': {
            status: 'complete',
            href: '/procurement/tenders/tender-1/document-controls',
            actionLabel: 'Open document register',
          },
          'supplier-bidding': {
            status: 'in-progress',
            href: '/procurement/tenders/tender-1',
            actionLabel: 'Open tender',
          },
        }}
      />
    );

    fireEvent.click(screen.getByText('View full process'));
    expect(
      screen.getByRole('link', { name: 'Open document register' })
    ).toHaveAttribute(
      'href',
      '/procurement/tenders/tender-1/document-controls'
    );
    expect(
      screen.getByRole('link', { name: 'Current action: Open tender' })
    ).toBeInTheDocument();
  });

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
    expect(screen.getByText('1 complete')).toBeInTheDocument();
    expect(
      screen.getByLabelText('Process progress coverage')
    ).toHaveTextContent('Step 6 of 12 · 9 unverified');
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
    expect(
      within(
        screen.getByRole('group', { name: /Next: Purchase order/ })
      ).getByRole('link', { name: 'Next action: Create purchase order' })
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('group', { name: 'Current: Award' })).queryByRole(
        'link'
      )
    ).not.toBeInTheDocument();
  });

  it('does not infer missing stages as complete', () => {
    render(
      <ProcurementUatFlowSidebar
        currentStage="budget-plan"
        stageStates={{ 'budget-plan': { status: 'in-progress' } }}
      />
    );

    expect(screen.queryByText('0/12 complete')).not.toBeInTheDocument();
    expect(screen.getByText('Progress unverified')).toBeInTheDocument();
    expect(
      screen.getByLabelText('Process progress coverage')
    ).toHaveTextContent('Step 1 of 12 · 11 unverified');
    expect(
      screen.getByText('This is the first procurement stage.')
    ).toBeInTheDocument();
  });
});
