import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ConversionFunnelWidget, DashboardCoverageNotice, DashboardModuleUnavailableWidget, ExpenseAccountsWidget, PipelineWidget, RiskMixWidget } from './ExecutiveDashboardWidgets';

const pipelineStage = (
  stageId: string,
  stage: string,
  stageOrder: number,
  opportunities: number,
  amountsByCurrency: Array<{ currency: string; amount: number }> = [],
) => ({
  stageId,
  stage,
  stageOrder,
  isClosed: stage === 'Signed',
  isWon: stage === 'Signed',
  opportunities,
  quotes: 0,
  percentageOfActivePipeline: 0,
  averageAgeDays: 4,
  stalledOpportunityCount: 0,
  overdueOpportunityCount: 0,
  amountsByCurrency,
  weightedAmountsByCurrency: amountsByCurrency.map((value) => ({ ...value, amount: value.amount / 2 })),
});

const funnelStage = (
  stageId: string,
  stage: string,
  stageOrder: number,
  count: number,
  overallConversionRate: number | null,
  amountsByCurrency: Array<{ currency: string; amount: number }> = [],
) => ({
  stageId,
  stage,
  stageOrder,
  count,
  conversionRate: overallConversionRate,
  overallConversionRate,
  amountsByCurrency,
});

describe('Executive dashboard widgets', () => {
  it('uses a compact actionable empty state instead of an empty chart canvas', () => {
    render(<ExpenseAccountsWidget data={[]} formatValue={(value) => String(value)} href="/finance/ledger" />);

    expect(screen.getByText('No expense balances yet')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open ledger →' })).toHaveAttribute('href', '/finance/ledger');
  });

  it('preserves stage drilldowns in the redesigned CRM pipeline', () => {
    render(
      <PipelineWidget
        data={[{ ...pipelineStage('qualified-id', 'Qualified', 20, 8), quotes: 3 }]}
        href="/crm/opportunities"
        getHref={(stageId) => `/crm/opportunities?stageDefinitionId=${stageId}`}
      />,
    );

    expect(screen.getByRole('link', { name: /Qualified/ })).toHaveAttribute('href', '/crm/opportunities?stageDefinitionId=qualified-id');
    expect(screen.getByRole('combobox', { name: 'CRM pipeline metric' })).toHaveValue('value');
    fireEvent.change(screen.getByRole('combobox', { name: 'CRM pipeline metric' }), { target: { value: 'count' } });
    expect(screen.getAllByText('8')).toHaveLength(2);
  });

  it('uses configured pipeline order and keeps recorded totals separate by currency', () => {
    render(
      <PipelineWidget
        data={[
          pipelineStage('signed-id', 'Signed', 50, 1, [{ currency: 'USD', amount: 250 }]),
          pipelineStage('review-id', 'Technical Review', 20, 4, [{ currency: 'GHS', amount: 480000 }]),
          pipelineStage('offer-id', 'Offer Issued', 30, 1, [{ currency: 'USD', amount: 50 }]),
        ]}
        href="/crm/opportunities"
        getHref={(stageId) => `/crm/opportunities?stageDefinitionId=${stageId}`}
      />,
    );

    const stageLinks = screen.getAllByRole('link').filter((link) => /^(Technical Review|Offer Issued|Signed)/.test(link.textContent ?? ''));
    expect(stageLinks.map((link) => link.textContent?.match(/^(Technical Review|Offer Issued|Signed)/)?.[0])).toEqual([
      'Technical Review', 'Offer Issued', 'Signed',
    ]);
    expect(screen.getByText('GHS 480,000')).toBeInTheDocument();
    expect(screen.getAllByText('USD 50')).toHaveLength(2);
    expect(screen.getByRole('combobox', { name: 'CRM pipeline metric' })).toHaveValue('value');
    expect(screen.getByRole('combobox', { name: 'CRM pipeline bar currency' })).toHaveValue('GHS');
    fireEvent.change(screen.getByRole('combobox', { name: 'CRM pipeline metric' }), { target: { value: 'count' } });
    expect(screen.getByText('Active opportunities')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('keeps the configured funnel sequence even when counts are not descending', () => {
    render(
      <ConversionFunnelWidget
        data={[
          funnelStage('won-id', 'Won', 50, 1, 25),
          funnelStage('enquiry-id', 'Enquiry', 10, 4, 100),
          funnelStage('proposal-id', 'Proposal', 30, 6, 150),
        ]}
        href="/crm/opportunities"
        getHref={(stageId) => `/crm/opportunities?reachedStageDefinitionId=${stageId}`}
      />,
    );

    const rows = screen.getAllByRole('link').filter((link) => link.textContent?.includes('%'));
    expect(rows.map((link) => link.textContent)).toEqual([
      expect.stringContaining('Enquiry'),
      expect.stringContaining('Proposal'),
      expect.stringContaining('Won'),
    ]);
    expect(rows[0]).toHaveAttribute('href', '/crm/opportunities?reachedStageDefinitionId=enquiry-id');
  });

  it('defaults the funnel to recorded value without reordering configured stages', () => {
    render(
      <ConversionFunnelWidget
        data={[
          funnelStage('proposal-id', 'Proposal', 30, 2, 50, [{ currency: 'GHS', amount: 100 }]),
          funnelStage('qualified-id', 'Qualified', 20, 1, 25, [{ currency: 'GHS', amount: 500 }]),
        ]}
        href="/crm/opportunities"
        getHref={(stageId) => `/crm/opportunities?reachedStageDefinitionId=${stageId}`}
      />,
    );

    expect(screen.getByRole('combobox', { name: 'Conversion funnel metric' })).toHaveValue('GHS');
    expect(screen.getByRole('img', { name: 'Opportunity stage funnel' })).toHaveTextContent('');
    const rows = () => screen.getAllByRole('link').filter((link) => link.textContent?.includes('%'));
    expect(rows().map((link) => link.textContent)).toEqual([
      expect.stringContaining('Qualified'),
      expect.stringContaining('Proposal'),
    ]);
    fireEvent.change(screen.getByRole('combobox', { name: 'Conversion funnel metric' }), { target: { value: 'count' } });
    expect(rows().map((link) => link.textContent)).toEqual([
      expect.stringContaining('Qualified'),
      expect.stringContaining('Proposal'),
    ]);
  });

  it('keeps a strict funnel silhouette when configured stage values tie', () => {
    const configuredStages = [
      ['enquiry-id', 'Enquiry', 10],
      ['qualified-id', 'Qualified', 20],
      ['proposal-id', 'Proposal', 30],
      ['negotiation-id', 'Negotiation', 40],
      ['won-id', 'Won', 50],
    ] as const;
    render(
      <ConversionFunnelWidget
        data={[...configuredStages].reverse().map(([stageId, stage, stageOrder]) =>
          funnelStage(stageId, stage, stageOrder, 1, 100, [{ currency: 'GHS', amount: 100 }]))}
        href="/crm/opportunities"
        getHref={() => '/crm/opportunities'}
      />,
    );

    const rows = screen.getAllByRole('link').filter((link) => link.textContent?.includes('%'));
    expect(rows.map((link) => link.textContent?.split('GHS')[0])).toEqual([
      expect.stringContaining('Enquiry'),
      expect.stringContaining('Qualified'),
      expect.stringContaining('Proposal'),
      expect.stringContaining('Negotiation'),
      expect.stringContaining('Won'),
    ]);
    const segmentWidths = Array.from(screen.getByRole('img', { name: 'Opportunity stage funnel' }).children)
      .map((segment) => Number.parseFloat((segment as HTMLElement).style.width));
    expect(segmentWidths).toHaveLength(5);
    expect(segmentWidths.every((width, index) => index === 0 || width < segmentWidths[index - 1])).toBe(true);
  });

  it('calculates the account total and keeps risk-band links', () => {
    render(<RiskMixWidget data={[{ name: 'Low Risk', value: 7 }, { name: 'High Risk', value: 3 }]} href="/crm/accounts" />);

    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Low Risk/ })).toHaveAttribute('href', '/crm/accounts?healthCategory=Low%20Risk');
  });

  it('shows restricted CRM analytics as an access state instead of claiming there are no opportunities', () => {
    render(<DashboardModuleUnavailableWidget title="CRM Pipeline" moduleName="CRM" accessRestricted href="/crm/opportunities" />);

    expect(screen.getByText('CRM dashboard access restricted')).toBeInTheDocument();
    expect(screen.queryByText('No CRM data yet')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Open CRM/ })).not.toBeInTheDocument();
  });

  it('identifies a failed Finance projection without claiming the ledger has no data', () => {
    render(<DashboardModuleUnavailableWidget title="Financial performance" moduleName="Finance" href="/finance" />);

    expect(screen.getByText('Finance dashboard unavailable')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open Finance →' })).toHaveAttribute('href', '/finance');
  });

  it('warns when alerts or queues cover only permitted dashboard modules', () => {
    render(<DashboardCoverageNotice modules={['CRM', 'Procurement']} />);

    expect(screen.getByRole('status')).toHaveTextContent('Partial view: CRM, Procurement dashboard data is restricted or unavailable.');
  });
});
