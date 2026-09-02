import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ProjectMobilePage, { buildCivilFeedbackStorageKey } from './page';
import { projectService } from '@/services/projectService';
import { civilEngineeringDirectTaskService } from '@/services/civil-engineering-direct-task.service';

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

vi.mock('@/services/civil-engineering-direct-task.service', () => ({
  civilEngineeringDirectTaskService: {
    feedbackLookups: vi.fn(),
    feedback: vi.fn(),
    submitAssigneeFeedback: vi.fn(),
  },
}));

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
    localStorage.setItem(
      'token',
      'eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJleHAiOjk5OTk5OTk5OTl9.c2lnbmF0dXJl'
    );
    localStorage.setItem(
      'user',
      JSON.stringify({ id: 'user-1', currentTenantId: 'tenant-1' })
    );
    localStorage.setItem(
      'currentTenant',
      JSON.stringify({ id: 'tenant-1', code: 'TDC' })
    );
    Object.defineProperty(window.navigator, 'onLine', {
      configurable: true,
      value: true,
    });
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
      canEdit: true,
      canDelete: true,
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
      baselineVarianceDays: 0,
      isOffBaseline: false,
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
      canEdit: true,
      canDelete: true,
    });
    vi.mocked(projectService.uploadProjectDocument).mockResolvedValue({
      id: 'doc-1',
      artifactType: 'Project',
      documentName: 'Configure edge device evidence',
      category: 'FieldEvidence',
      documentType: 'MobileEvidence',
      filePath: '/uploads/evidence.png',
      versionLabel: '1.0',
      status: 'Active',
      isExternalVisible: false,
      createdAt: '2026-03-10T00:00:00Z',
    });
    vi.mocked(civilEngineeringDirectTaskService.feedbackLookups).mockResolvedValue({
      documents: [],
      availableActions: ['Acknowledge', 'UpdateProgress', 'Complete'],
      measurementUnits: [],
      requireFeedbackEvidence: false,
      requireClosureAcceptance: true,
    });
    vi.mocked(civilEngineeringDirectTaskService.feedback).mockResolvedValue([]);
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

  it('routes governed Civil assignments only through the Civil feedback lifecycle', async () => {
    vi.mocked(projectService.getMobileSummary).mockResolvedValue({
      ...summary,
      assignments: [
        {
          ...summary.assignments[0],
          civilDirectTaskId: 'civil-task-1',
          civilDirectTaskStatus: 'Assigned',
          civilDirectTaskRowVersion: 'civil-row-version',
        },
      ],
    });

    render(<ProjectMobilePage />);

    expect(await screen.findByText('Governed Civil field feedback')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Update Progress' })).not.toBeInTheDocument();
    expect(civilEngineeringDirectTaskService.feedbackLookups).toHaveBeenCalledWith(
      'proj-1',
      'civil-task-1',
    );
    expect(civilEngineeringDirectTaskService.feedback).toHaveBeenCalledWith(
      'proj-1',
      'civil-task-1',
    );
    expect(projectService.updateMobileWorkItemProgress).not.toHaveBeenCalled();
    expect(projectService.uploadProjectDocument).not.toHaveBeenCalled();
  });

  it('never synchronizes queued Civil feedback after the authenticated actor changes', async () => {
    const civilSummary = {
      ...summary,
      assignments: [
        {
          ...summary.assignments[0],
          civilDirectTaskId: 'civil-task-1',
          civilDirectTaskStatus: 'Assigned',
          civilDirectTaskRowVersion: 'civil-row-version',
        },
      ],
    };
    vi.mocked(projectService.getMobileSummary).mockResolvedValue(civilSummary);
    Object.defineProperty(window.navigator, 'onLine', {
      configurable: true,
      value: false,
    });
    const storageKey = buildCivilFeedbackStorageKey(
      'tenant-1',
      'user-1',
      'civil-task-1'
    );
    localStorage.setItem(
      storageKey,
      JSON.stringify({
        tenantId: 'tenant-1',
        userId: 'user-1',
        projectId: 'proj-1',
        taskId: 'civil-task-1',
        assignmentRowVersion: 'civil-row-version',
        queuedAt: '2026-03-10T10:00:00Z',
        request: {
          clientRequestId: 'request-1',
          action: 'Acknowledge',
          rowVersion: 'civil-row-version',
          capturedOfflineAtUtc: '2026-03-10T10:00:00Z',
        },
      })
    );

    render(<ProjectMobilePage />);

    expect(await screen.findByText(/One offline field update is queued/)).toBeInTheDocument();
    localStorage.setItem(
      'user',
      JSON.stringify({ id: 'user-2', currentTenantId: 'tenant-1' })
    );
    Object.defineProperty(window.navigator, 'onLine', {
      configurable: true,
      value: true,
    });
    fireEvent(window, new Event('online'));

    await waitFor(() =>
      expect(
        screen.queryByText(/One offline field update is queued/)
      ).not.toBeInTheDocument()
    );
    expect(
      civilEngineeringDirectTaskService.submitAssigneeFeedback
    ).not.toHaveBeenCalled();
    expect(localStorage.getItem(storageKey)).not.toBeNull();
  });

  it('never synchronizes queued Civil feedback after task reassignment or a stale assignment version', async () => {
    const civilSummary = {
      ...summary,
      assignments: [
        {
          ...summary.assignments[0],
          civilDirectTaskId: 'civil-task-1',
          civilDirectTaskStatus: 'Assigned',
          civilDirectTaskRowVersion: 'civil-row-version',
        },
      ],
    };
    vi.mocked(projectService.getMobileSummary)
      .mockResolvedValueOnce(civilSummary)
      .mockResolvedValueOnce({ ...summary, assignments: [] });
    Object.defineProperty(window.navigator, 'onLine', {
      configurable: true,
      value: false,
    });
    const storageKey = buildCivilFeedbackStorageKey(
      'tenant-1',
      'user-1',
      'civil-task-1'
    );
    localStorage.setItem(
      storageKey,
      JSON.stringify({
        tenantId: 'tenant-1',
        userId: 'user-1',
        projectId: 'proj-1',
        taskId: 'civil-task-1',
        assignmentRowVersion: 'civil-row-version',
        queuedAt: '2026-03-10T10:00:00Z',
        request: {
          clientRequestId: 'request-2',
          action: 'Acknowledge',
          rowVersion: 'civil-row-version',
          capturedOfflineAtUtc: '2026-03-10T10:00:00Z',
        },
      })
    );

    render(<ProjectMobilePage />);

    expect(await screen.findByText(/One offline field update is queued/)).toBeInTheDocument();
    Object.defineProperty(window.navigator, 'onLine', {
      configurable: true,
      value: true,
    });
    fireEvent(window, new Event('online'));

    await waitFor(() => expect(projectService.getMobileSummary).toHaveBeenCalledTimes(2));
    expect(
      civilEngineeringDirectTaskService.submitAssigneeFeedback
    ).not.toHaveBeenCalled();
    expect(localStorage.getItem(storageKey)).not.toBeNull();
  });
});
