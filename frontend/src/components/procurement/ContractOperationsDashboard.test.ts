import { describe, expect, it } from 'vitest';
import { formatContractKpiPercentage } from './ContractOperationsDashboard';

describe('formatContractKpiPercentage', () => {
  it('renders missing API values without invoking numeric formatting', () => {
    expect(formatContractKpiPercentage(null)).toBe('—');
    expect(formatContractKpiPercentage(undefined)).toBe('—');
  });

  it('formats available KPI values consistently', () => {
    expect(formatContractKpiPercentage(92.345)).toBe('92.34%');
  });
});
