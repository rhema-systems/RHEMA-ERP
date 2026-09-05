import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import Page from './page';
import ExistingPage from '../[id]/page';

const api = vi.hoisted(() => ({ bid: vi.fn(), tender: vi.fn(), template: vi.fn(), create: vi.fn(), evaluation: vi.fn() }));
vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'evaluation' }), useSearchParams: () => new URLSearchParams('bidId=bid'),
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/services/tenderBidService', () => ({ getBidById: api.bid }));
vi.mock('@/services/tenderService', () => ({ tenderService: { getTenderById: api.tender } }));
vi.mock('@/services/evaluationTemplateService', () => ({ evaluationTemplateService: { getById: api.template } }));
vi.mock('@/services/tenderEvaluationService', () => ({ createEvaluation: api.create, getEvaluationById: api.evaluation }));

const template = {
  id: 'template', templateName: 'Standard', scoringMethod: 'WeightedAverage', isActive: true,
  passingScore: 80, technicalWeight: 70, financialWeight: 30, minimumTechnicalScore: 80, criteria: [],
};
beforeEach(() => {
  vi.stubGlobal('React', React);
  vi.clearAllMocks();
  api.bid.mockResolvedValue({ id: 'bid', tenderId: 'tender', bidNumber: 'BID-1', totalBidAmount: 52000, currency: 'GHS' });
  api.tender.mockResolvedValue({ id: 'tender', evaluationTemplateId: 'template', usesControlledTenderLifecycle: false, useQCBSEvaluation: false });
  api.template.mockResolvedValue(template);
  api.evaluation.mockResolvedValue({ id: 'evaluation', tenderBidId: 'bid', status: 'Draft' });
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('evaluation configuration guard', () => {
  it.each([['create', Page], ['existing draft', ExistingPage]] as const)('blocks a mismatch on %s without saving', async (_, Component) => {
    api.template.mockResolvedValue({ ...template, scoringMethod: 'QCBS' });
    render(<Component />);
    expect(await screen.findByText('Evaluation configuration needs correction')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Save/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Submit/ })).toBeNull();
    expect(screen.queryByRole('slider')).toBeNull();
    expect(api.create).not.toHaveBeenCalled();
  });
  it('uses the authoritative tender template even when bid projection omits it', async () => {
    render(<Page />);
    expect(await screen.findByRole('button', { name: 'Save as Draft' })).toBeTruthy();
    expect(api.template).toHaveBeenCalledWith('template');
  });
  it('blocks unavailable template instead of falling back to blank scoring', async () => {
    api.template.mockRejectedValue(new Error('Unavailable'));
    render(<Page />);
    expect(await screen.findByText('Evaluation configuration needs correction')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Save as Draft' })).toBeNull();
  });
  it('blocks unknown methods rather than interpreting them as PassFail', async () => {
    api.template.mockResolvedValue({ ...template, scoringMethod: 'Unknown' });
    render(<Page />);
    expect(await screen.findByText(/unsupported scoring method/)).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });
});
