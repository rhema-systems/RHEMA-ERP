import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ hasPermission: vi.fn() }));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: mocks.hasPermission }),
}));

import { TenderHeaderControlActions } from './TenderHeaderControlActions';

describe('TenderHeaderControlActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockReturnValue(true);
  });

  it('renders source-aware links for an exceptional tender', () => {
    render(
      <TenderHeaderControlActions
        tenderId="tender-1"
        tenderType="ITB"
        sourcingCaseId="case-1"
        sourcingMethod="RestrictedTendering"
      />
    );

    expect(mocks.hasPermission).toHaveBeenCalledWith(
      'procurement.records.read'
    );
    expect(
      screen.getByRole('link', { name: 'Committee Controls' })
    ).toHaveAttribute(
      'href',
      '/procurement/tenders/tender-1/committee-controls'
    );
    expect(
      screen.getByRole('link', { name: 'Award Readiness' })
    ).toHaveAttribute(
      'href',
      '/procurement/tenders/tender-1/award-readiness?sourceType=ExceptionalSourcing'
    );
    expect(
      screen.getByRole('link', { name: 'GHANEPS Exchange' })
    ).toHaveAttribute(
      'href',
      '/procurement/tenders/tender-1/ghaneps-exchange?sourceType=ExceptionalSourcing'
    );
  });

  it('does not render unsupported committee controls for a release-only tender', () => {
    render(
      <TenderHeaderControlActions
        tenderId="tender-1"
        tenderType="ITB"
        sourcingMethod="NationalCompetitiveTendering"
      />
    );

    expect(
      screen.queryByRole('link', { name: 'Committee Controls' })
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'Award Readiness' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'GHANEPS Exchange' })
    ).toBeInTheDocument();
  });

  it('renders none of the controls without records permission', () => {
    mocks.hasPermission.mockReturnValue(false);

    const { container } = render(
      <TenderHeaderControlActions
        tenderId="tender-1"
        tenderType="ITB"
        sourcingCaseId="case-1"
        sourcingMethod="NationalCompetitiveTendering"
      />
    );

    expect(container).toBeEmptyDOMElement();
  });
});
