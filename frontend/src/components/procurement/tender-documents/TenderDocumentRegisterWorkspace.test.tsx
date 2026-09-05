import React, { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ readiness: vi.fn(), getRegister: vi.fn(), searchTemplates: vi.fn(), workflowOptions: vi.fn() }));
vi.mock('@/services/procurement-tender-document.service', () => ({ procurementTenderDocumentService: api }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('./TenderDocumentRegister', () => ({ TenderDocumentRegister: ({ workflowDisabled, canApprove, onWorkflowUpdated }: { workflowDisabled: boolean; canApprove: boolean; onWorkflowUpdated: () => Promise<void> }) => <div>Register loaded<button disabled={workflowDisabled} onClick={() => void onWorkflowUpdated()}>Workflow action</button><button disabled={!canApprove}>Apply decision</button></div> }));
vi.mock('./TenderDocumentActionDialogs', () => ({
  TenderDocumentDecisionDialog: () => null,
  TenderDocumentAcknowledgementDialog: () => null,
  TenderDocumentActionDialogs: ({ onChanged }: { onChanged: () => Promise<void> }) => {
    const [outcome, setOutcome] = useState('');
    return <><button onClick={() => void onChanged().then(() => setOutcome('Refresh succeeded'), () => setOutcome('Refresh failed'))}>Verify refresh</button><p>{outcome}</p></>;
  },
}));

import { TenderDocumentRegisterWorkspace } from './TenderDocumentRegisterWorkspace';

describe('document register real query refresh', () => {
  const clients: QueryClient[] = [];
  beforeEach(() => {
    vi.resetAllMocks();
    api.readiness.mockResolvedValue({ sourceReference: 'TND-001', method: 'NCT', hasRegister: true, ready: true });
    api.getRegister.mockResolvedValue({ id: 'register-1' });
    api.searchTemplates.mockResolvedValue({ items: [] });
    api.workflowOptions.mockResolvedValue([]);
  });
  afterEach(() => { cleanup(); clients.forEach((client) => client.clear()); clients.length = 0; });

  async function mount() {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    clients.push(client);
    render(<QueryClientProvider client={client}><TenderDocumentRegisterWorkspace sourceType="Tender" sourceId="tender-1" /></QueryClientProvider>);
    await screen.findByRole('button', { name: 'Verify refresh' });
    await waitFor(() => expect(api.getRegister).toHaveBeenCalled());
  }

  it('propagates a failed readiness read to the successful-mutation caller', async () => {
    await mount();
    api.readiness.mockRejectedValue(new Error('Readiness temporarily unavailable'));
    fireEvent.click(screen.getByRole('button', { name: 'Verify refresh' }));
    await screen.findByText('Latest register state could not be refreshed');
    await screen.findByText('Refresh failed');
    await waitFor(() => expect(clients[0].getQueryState(['procurement-tender-document-readiness', 'Tender', 'tender-1', false])?.status).toBe('error'));
    expect((screen.getByRole('button', { name: 'Workflow action' }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole('button', { name: 'Apply decision' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('propagates a failed register read instead of reporting refresh success', async () => {
    await mount();
    api.getRegister.mockRejectedValue(new Error('Register temporarily unavailable'));
    fireEvent.click(screen.getByRole('button', { name: 'Verify refresh' }));
    await screen.findByText('Refresh failed');
    expect(screen.queryByText('Refresh succeeded')).toBeNull();
  });

  it('resolves once readiness and register reads succeed', async () => {
    await mount();
    fireEvent.click(screen.getByRole('button', { name: 'Verify refresh' }));
    await screen.findByText('Refresh succeeded');
    await waitFor(() => expect((screen.getByRole('button', { name: 'Workflow action' }) as HTMLButtonElement).disabled).toBe(false));
    expect((screen.getByRole('button', { name: 'Apply decision' }) as HTMLButtonElement).disabled).toBe(false);
  });
});
