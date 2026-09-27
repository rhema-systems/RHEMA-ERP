import React from 'react';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import LegalSearchCasePage from './page';

const mocks = vi.hoisted(() => ({ getCase: vi.fn(), router: { replace: vi.fn() } }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'case-1' }), useRouter: () => mocks.router }));
vi.mock('@/services/procedure-case.service', () => ({ procedureCaseService: { getCase: mocks.getCase } }));

describe('Legal search case route', () => {
  beforeEach(() => { vi.stubGlobal('React', React); vi.clearAllMocks(); });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('resolves the authorized record into its actual procedure detail route', async () => {
    mocks.getCase.mockResolvedValue({ id: 'case-1', module: 'Legal', entityType: 'LegalCourtProcess' });
    render(<LegalSearchCasePage />);
    await waitFor(() => expect(mocks.router.replace).toHaveBeenCalledWith('/legal/LegalCourtProcess/cases/case-1'));
    expect(mocks.getCase).toHaveBeenCalledWith('case-1');
  });

  it('does not route a non-Legal record into the Legal workspace', async () => {
    mocks.getCase.mockResolvedValue({ id: 'case-1', module: 'Estate', entityType: 'EstateProcedure' });
    render(<LegalSearchCasePage />);
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Legal case was not found.'));
    expect(mocks.router.replace).not.toHaveBeenCalled();
  });

  it('shows access failures without navigating', async () => {
    mocks.getCase.mockRejectedValue(new Error('You cannot view this case.'));
    render(<LegalSearchCasePage />);
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('You cannot view this case.'));
    expect(mocks.router.replace).not.toHaveBeenCalled();
  });
});
