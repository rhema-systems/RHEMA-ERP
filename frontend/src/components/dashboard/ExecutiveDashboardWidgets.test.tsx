import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ExpenseAccountsWidget, PipelineWidget, RiskMixWidget } from './ExecutiveDashboardWidgets';

describe('Executive dashboard widgets', () => {
  it('uses a compact actionable empty state instead of an empty chart canvas', () => {
    render(<ExpenseAccountsWidget data={[]} formatValue={(value) => String(value)} href="/finance/ledger" />);

    expect(screen.getByText('No expense balances yet')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open ledger →' })).toHaveAttribute('href', '/finance/ledger');
  });

  it('preserves stage drilldowns in the redesigned CRM pipeline', () => {
    render(
      <PipelineWidget
        data={[{ stage: 'Qualified', opportunities: 8, quotes: 3 }]}
        href="/crm/opportunities"
        getHref={(stage) => `/crm/opportunities?stage=${stage}`}
      />,
    );

    expect(screen.getByRole('link', { name: /Qualified/ })).toHaveAttribute('href', '/crm/opportunities?stage=Qualified');
    expect(screen.getByText('8')).toBeInTheDocument();
  });

  it('calculates the account total and keeps risk-band links', () => {
    render(<RiskMixWidget data={[{ name: 'Low Risk', value: 7 }, { name: 'High Risk', value: 3 }]} href="/crm/accounts" />);

    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Low Risk/ })).toHaveAttribute('href', '/crm/accounts?healthCategory=Low%20Risk');
  });
});
