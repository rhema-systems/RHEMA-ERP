import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ProjectMobilePage from './page';
import { projectService } from '@/services/projectService';

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('@/services/projectService', async () => {
  const actual = await vi.importActual<typeof import('@/services/projectService')>('@/services/projectService');
  return {
    ...actual,
    projectService: {
      ...actual.projectService,
      getMobileSummary: vi.fn(),
      getCatalogEntries: vi.fn(),
      submitMobileTimesheet: vi.fn(),
      submitMobileExpense: vi.fn(),
      updateMobileWorkItemProgress: vi.fn(),
      uploadProjectDocument: vi.fn(),
    },
  };
});

describe('ProjectMobilePage', () => {
  const summary = {
    assignmentCount: 1,
    overdueCount: 0,
    pendingHours: 3,
    pendingExpenses: 12,
    assignments: [
      {
        projectId: 'proj-1',
        workItemId: 'task-1',
        projectCode: 'PRJ-2026-0042',
        projectTitle: 'Field Deployment',
        workItemTitle: 'Configure edge device',
        status: 'Assigned',
        percentComplete: 25,
        plannedEndDate: '2026-03-15T00:00:00Z',
      },
    ],
  };

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem('user', JSON.stringify({ id: 'user-1' }));
    vi.mocked(projectService.getMobileSummary).mockResolvedValue(summary);
    vi.mocked(projectService.getCatalogEntries).mockResolvedValue([]);
    vi.mocked(projectService.submitMobileTimesheet).mockResolvedValue({
      id: 'time-1',
      projectId: 'proj-1',
      workItemId: 'task-1',
      userId: 'user-1',
      entryDate: '2026-03-09T00:00:00Z',
      hours: 8,
      isBillable: false,
      hourlyRate: 0,
      costAmount: 0,
      workType: 'Field',
      status: 'PendingApproval',
    });
    vi.mocked(projectService.updateMobileWorkItemProgress).mockResolvedValue({
      id: 'task-1',
      projectId: 'proj-1',
      nodeType: 'Task',
      title: 'Configure edge device',
      status: 'Assigned',
      sortOrder: 1,
      percentComplete: 25,
      isRollupEnabled: true,
      children: [],
    });
    vi.mocked(projectService.submitMobileExpense).mockResolvedValue({
      id: 'exp-1',
      projectId: 'proj-1',
      workItemId: 'task-1',
      userId: 'user-1',
      expenseDate: '2026-03-09T00:00:00Z',
      category: 'Travel',
      currency: 'USD',
      amount: 0,
      taxAmount: 0,
      isBillable: false,
      status: 'PendingApproval',
    });
    vi.mocked(projectService.uploadProjectDocument).mockResolvedValue({
      id: 'doc-1',
      projectId: 'proj-1',
      documentName: 'Configure edge device evidence',
      category: 'FieldEvidence',
      documentType: 'MobileEvidence',
      filePath: '/uploads/evidence.png',
      createdAt: '2026-03-10T00:00:00Z',
    });
  });

  it('renders the mobile summary and assignment details', async () => {
    render(<ProjectMobilePage />);

    expect(await screen.findByText('Project Mobile')).toBeInTheDocument();
    expect(screen.getByText('PRJ-2026-0042')).toBeInTheDocument();
    expect(screen.getAllByText('Configure edge device').length).toBeGreaterThan(0);
    expect(screen.getByText('Field Deployment | due Mar 15, 2026')).toBeInTheDocument();
    expect(projectService.getMobileSummary).toHaveBeenCalledTimes(1);
  });

  it('submits progress and time from the mobile page', async () => {
    vi.mocked(projectService.getMobileSummary)
      .mockResolvedValueOnce(summary)
      .mockResolvedValueOnce(summary)
      .mockResolvedValueOnce(summary);

    render(<ProjectMobilePage />);

    expect(await screen.findByText('Project Mobile')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Update Progress' }));
    await waitFor(() => expect(projectService.updateMobileWorkItemProgress).toHaveBeenCalledWith(
      'proj-1',
      'task-1',
      expect.objectContaining({
        status: 'Assigned',
        percentComplete: 25,
      }),
    ));

    fireEvent.click(screen.getByRole('button', { name: 'Submit Time' }));
    await waitFor(() => expect(projectService.submitMobileTimesheet).toHaveBeenCalledWith(
      'proj-1',
      expect.objectContaining({
        userId: 'user-1',
        workItemId: 'task-1',
        hours: 8,
        workType: 'Field',
      }),
    ));

    expect(projectService.getMobileSummary).toHaveBeenCalledTimes(3);
  });
});
