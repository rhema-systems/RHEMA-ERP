import React from 'react';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import EnvironmentalPermitDetailPage from './page';
import type { SheEnvironmentalPermit } from '@/types/hr/safety-environment-compliance';

const mocks = vi.hoisted(() => ({ getPermit: vi.fn(), id: 'permit-1' }));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: mocks.id }) }));
vi.mock('@/services/hr/safety-environmental-compliance.service', () => ({
  safetyEnvironmentalComplianceService: { getPermit: mocks.getPermit },
}));
const permit = (id: string): SheEnvironmentalPermit => ({
  id, registerNumber: `PER-${id}`, permitName: `Operating licence ${id}`,
  permitType: 'OperatingLicence', permitTypeName: 'OperatingLicence',
  responsibleOfficerId: 'officer-1', responsibleOfficerName: 'Responsible officer',
  issueDate: '2026-01-01', expiryDate: '2027-01-01', status: 'Active', statusName: 'Active',
  authorityReferenceNumber: 'EPA-42', conditions: 'Maintain monitoring\nSubmit annual report', versions: [],
});

describe('Environmental permit search destination', () => {
  beforeEach(() => { vi.stubGlobal('React', React); vi.clearAllMocks(); mocks.id = 'permit-1'; });
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it('loads the exact authorized record and presents a read-only permit', async () => {
    mocks.getPermit.mockResolvedValue(permit('permit-1'));
    render(<EnvironmentalPermitDetailPage />);
    expect(await screen.findByRole('heading', { name: 'Operating licence permit-1' })).toBeInTheDocument();
    expect(mocks.getPermit).toHaveBeenCalledWith('permit-1');
    expect(screen.getByText('EPA-42')).toBeInTheDocument();
    expect(screen.getByText(/Maintain monitoring/)).toHaveTextContent('Submit annual report');
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /save|edit|renew|delete/i })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to permit register' })).toHaveAttribute('href', '/hr/safety/environmental/permits');
  });

  it('surfaces an owner access failure without displaying a record', async () => {
    mocks.getPermit.mockRejectedValue(new Error('Access denied.'));
    render(<EnvironmentalPermitDetailPage />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Access denied.');
    expect(screen.queryByText('EPA-42')).not.toBeInTheDocument();
  });

  it('ignores the previous record response after navigating to another result', async () => {
    let finishFirst!: (value: SheEnvironmentalPermit) => void;
    mocks.getPermit.mockImplementation((id: string) => id === 'permit-1'
      ? new Promise<SheEnvironmentalPermit>(resolve => { finishFirst = resolve; })
      : Promise.resolve(permit(id)));
    const view = render(<EnvironmentalPermitDetailPage />);
    await waitFor(() => expect(mocks.getPermit).toHaveBeenCalledWith('permit-1'));
    mocks.id = 'permit-2';
    view.rerender(<EnvironmentalPermitDetailPage />);
    expect(await screen.findByRole('heading', { name: 'Operating licence permit-2' })).toBeInTheDocument();
    await act(async () => finishFirst(permit('permit-1')));
    expect(screen.getByRole('heading', { name: 'Operating licence permit-2' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Operating licence permit-1' })).not.toBeInTheDocument();
  });
});
