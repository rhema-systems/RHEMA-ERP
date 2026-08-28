import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { QuantitySurveyCostDashboardPage } from './QuantitySurveyCostDashboardPage';

vi.mock('@tanstack/react-query', () => ({
  useQuery: ({ queryKey }: { queryKey: string[] }) =>
    queryKey[0] === 'projects'
      ? {
          data: {
            items: [
              {
                id: '11111111-1111-1111-1111-111111111111',
                projectCode: 'PRJ-001',
                title: 'Works project',
              },
            ],
          },
          isFetching: false,
          refetch: vi.fn(),
        }
      : {
          data: {
            projectId: '11111111-1111-1111-1111-111111111111',
            projectCode: 'PRJ-001',
            projectTitle: 'Works project',
            projectStatus: 'InProgress',
            currencyCode: 'GHS',
            generatedAtUtc: '2026-08-11T00:00:00Z',
            approvedBudget: 1000,
            committedValue: 800,
            certifiedValue: 500,
            actualCost: 450,
            approvedVariationValue: 100,
            forecastCost: 1100,
            finalProjectedCost: 1150,
            costToComplete: 700,
            budgetVariance: -150,
            forecastBasis: 'Active forecast EAC: v2',
            hasConversionGaps: false,
            missingExchangeRateCount: 0,
            warnings: [],
            lines: [
              {
                boqItemId: '22222222-2222-2222-2222-222222222222',
                projectPackageId: '33333333-3333-3333-3333-333333333333',
                packageCode: 'PKG-01',
                sectionCode: 'SEC-01',
                sectionName: 'Substructure',
                costCode: 'CC-01',
                costCodeName: 'Concrete',
                lineNumber: '1.1',
                description: 'Concrete foundations',
                quantity: 10,
                unitOfMeasure: 'm3',
                budgetAmount: 1000,
                committedAmount: 800,
                actualAmount: 450,
                forecastAmount: 1100,
                forecastVarianceAmount: -100,
              },
            ],
          },
          isFetching: false,
          isError: false,
          refetch: vi.fn(),
        },
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasAnyRole: () => false, hasPermission: () => true }),
}));

vi.mock('@/services/projectService', () => ({
  projectService: {
    getProjects: vi.fn(),
    getQuantitySurveyCostDashboard: vi.fn(),
  },
}));

describe('QuantitySurveyCostDashboardPage', () => {
  it('shows every required cost metric and controlled line classifications', () => {
    render(<QuantitySurveyCostDashboardPage />);

    expect(
      screen.getByRole('heading', { name: 'QS Cost Dashboard' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('combobox', { name: 'Project' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('combobox', { name: 'Section' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('combobox', { name: 'Cost code' })
    ).toBeInTheDocument();
    for (const metric of [
      'Approved budget',
      'Commitments',
      'Certified value',
      'Actual cost',
      'Approved variations',
      'Forecast',
      'Final projected cost',
    ]) {
      expect(screen.getAllByText(metric).length).toBeGreaterThan(0);
    }
    expect(screen.getByText('Concrete foundations')).toBeInTheDocument();
    expect(screen.getByText('SEC-01 · Substructure')).toBeInTheDocument();
    expect(screen.getByText('CC-01 · Concrete')).toBeInTheDocument();
  });
});
