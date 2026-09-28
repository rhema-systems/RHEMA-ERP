import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from 'sonner';
import NewProjectPage from './page';

const router = vi.hoisted(() => ({ push: vi.fn() }));
const service = vi.hoisted(() => ({
  getProjectTypes: vi.fn(),
  getProjectPriorities: vi.fn(),
  getProjectTemplates: vi.fn(),
  getPortfolios: vi.fn(),
  getSettings: vi.fn(),
  getCatalogEntries: vi.fn(),
  getContractLookup: vi.fn(),
  getPrograms: vi.fn(),
  createProject: vi.fn(),
}));

vi.mock('next/navigation', () => ({ useRouter: () => router }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/services/projectService', () => ({ projectService: service }));
vi.mock('@/services/user', () => ({ userService: { searchAssignableUsers: vi.fn().mockResolvedValue([]) } }));
vi.mock('@/services/businessPartnerService', () => ({
  businessPartnerService: { getAllPartnersForDropdown: vi.fn().mockResolvedValue([]) },
}));
vi.mock('@/components/projects/ReadyLandPortionSelect', () => ({ ReadyLandPortionSelect: () => null }));
vi.mock('@/lib/project-currency', async (importOriginal) => ({
  ...await importOriginal<typeof import('@/lib/project-currency')>(),
  loadProjectCurrencyContext: vi.fn().mockResolvedValue({
    activeCurrencies: [],
    baseCurrency: { code: 'GHS', name: 'Ghana Cedi', symbol: 'GH₵' },
  }),
}));

const settings = {
  defaultProjectTypeId: 'construction',
  defaultApprovalRequired: true,
  mandatoryFieldsByTypeJson: JSON.stringify({ CONSTRUCTION: ['EstimatedBudget', 'StartDate'] }),
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('React', React);
  service.getProjectTypes.mockResolvedValue([{
    id: 'construction', code: 'CONSTRUCTION', name: 'Construction', isActive: true,
    requiresSponsor: false, mandatoryFieldsJson: JSON.stringify(['EstimatedBudget', 'StartDate']),
  }]);
  service.getSettings.mockResolvedValue(settings);
  service.getProjectPriorities.mockResolvedValue([]);
  service.getProjectTemplates.mockResolvedValue([]);
  service.getPortfolios.mockResolvedValue([]);
  service.getCatalogEntries.mockResolvedValue([]);
  service.getContractLookup.mockResolvedValue([]);
  service.getPrograms.mockResolvedValue([]);
  service.createProject.mockResolvedValue({ id: 'created-project' });
});

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

async function openProjectForm() {
  render(<NewProjectPage />);
  await screen.findByText('Construction');
  fireEvent.change(screen.getByLabelText(/^Title/), { target: { value: 'UAT development' } });
}

describe('project initiation budget', () => {
  it('creates with an unset budget even when both setup sources list it as mandatory', async () => {
    await openProjectForm();
    fireEvent.change(screen.getByLabelText(/^Start Date/), { target: { value: '2026-09-28' } });

    expect(screen.getByLabelText('Estimated Budget (optional)')).toHaveValue(null);
    fireEvent.click(screen.getByRole('button', { name: 'Create Project' }));

    await waitFor(() => expect(service.createProject).toHaveBeenCalledTimes(1));
    expect(service.createProject.mock.calls[0][0]).toMatchObject({
      title: 'UAT development', projectTypeId: 'construction', startDate: '2026-09-28', approvalRequired: true,
    });
    expect(service.createProject.mock.calls[0][0].estimatedBudget).toBeUndefined();
    expect(router.push).toHaveBeenCalledWith('/development/projects/created-project');
    expect(toast.error).not.toHaveBeenCalled();
  });

  it('retains an explicitly entered zero budget', async () => {
    await openProjectForm();
    fireEvent.change(screen.getByLabelText(/^Start Date/), { target: { value: '2026-09-28' } });
    fireEvent.change(screen.getByLabelText('Estimated Budget (optional)'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create Project' }));

    await waitFor(() => expect(service.createProject).toHaveBeenCalledTimes(1));
    expect(service.createProject.mock.calls[0][0].estimatedBudget).toBe(0);
  });

  it('continues to enforce other configured initiation requirements', async () => {
    await openProjectForm();
    fireEvent.click(screen.getByRole('button', { name: 'Create Project' }));

    expect(service.createProject).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith('Start date is required for the selected project setup');
  });
});
