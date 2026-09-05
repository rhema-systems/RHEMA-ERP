import { describe, expect, it } from 'vitest';
import type { ContractDto } from '@/services/contractService';
import { usesCivilWorksEotWorkflow } from './ProjectCommercialAdminTab';

describe('ProjectCommercialAdminTab EOT contract routing', () => {
  const contracts = [
    {
      id: 'works-contract',
      contractType: 'Works',
      contractNumber: 'CW-001',
    },
    {
      id: 'services-contract',
      contractType: 'Services',
      contractNumber: 'CS-001',
    },
  ] as ContractDto[];

  it('routes only the selected Works contract through the Civil EOT workflow', () => {
    expect(usesCivilWorksEotWorkflow(contracts, 'WORKS-CONTRACT')).toBe(true);
    expect(usesCivilWorksEotWorkflow(contracts, 'services-contract')).toBe(false);
    expect(usesCivilWorksEotWorkflow(contracts, undefined)).toBe(false);
  });
});
