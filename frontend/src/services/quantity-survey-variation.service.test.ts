import { expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { quantitySurveyVariationService as service } from './quantity-survey-variation.service';
vi.mock('./api.service', () => ({ apiService: { get: vi.fn() } }));
it('restores PascalCase server source names into the controlled source selector without changing record identity', async () => {
  vi.mocked(apiService.get).mockResolvedValue({ contracts: [], siteInstructions: [], changeRequests: [], boqLines: [],
    variations: [{ id: 'existing', sourceType: 'DirectVariation' }, { id: 'instruction', sourceType: 'SiteInstruction', siteInstructionId: 'source' }] });
  const result = await service.workspace('project');
  expect(result.variations).toEqual([{ id: 'existing', sourceType: 'directVariation' }, { id: 'instruction', sourceType: 'siteInstruction', siteInstructionId: 'source' }]);
});
