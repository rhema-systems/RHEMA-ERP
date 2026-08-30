import { beforeEach, describe, expect, it, vi } from 'vitest';

import { evaluationTemplateService } from './evaluationTemplateService';

describe('evaluation template dropdown client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'procurement-user-token');
  });

  it('requests active templates for the PR category and tender type', async () => {
    const templates = [{
      id: 'template-1',
      templateName: 'Standard Tender Evaluation',
      templateCode: 'EVAL-TENDER-001',
      category: 'Goods',
      tenderType: 'ITB',
      isDefault: true,
      criteriaCount: 5,
    }];
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(templates), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(evaluationTemplateService.getForDropdown(' Goods ', 'ITB'))
      .resolves.toEqual(templates);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/EvaluationTemplates/dropdown?category=Goods&tenderType=ITB',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer procurement-user-token' }),
      }),
    );
  });
});
