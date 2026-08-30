import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  toast: vi.fn(),
  listProjectEngineerAssignments: vi.fn(),
  projectEngineerAssignmentLookups: vi.fn(),
  projectEngineerAssignmentHistory: vi.fn(),
  assignProjectEngineer: vi.fn(),
  endProjectEngineerAssignment: vi.fn(),
}));

vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: mocks.toast }) }));
vi.mock('@/services/civil-engineering-supervision.service', () => ({
  civilEngineeringSupervisionService: mocks,
}));

import { CivilEngineeringProjectEngineerAssignmentsPanel } from './CivilEngineeringProjectEngineerAssignmentsPanel';

const assignment = {
  id: 'assignment-1', projectId: 'project-1', projectMemberId: 'member-1', assignedUserId: 'user-1',
  assignedUserName: 'Amina Engineer', sourceCivilRole: 'TDC_CIVIL_ENGINEER', projectRole: 'TDC_PROJECT_ENGINEER',
  authority: 'SiteSupervisionAndInstructions' as const, effectiveFrom: '2026-08-20T00:00:00Z',
  effectiveTo: null, isActive: true, createdAt: '2026-08-20T00:00:00Z', createdBy: 'hod', rowVersion: 'AQID',
};

describe('CivilEngineeringProjectEngineerAssignmentsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.listProjectEngineerAssignments.mockResolvedValue([assignment]);
    mocks.projectEngineerAssignmentHistory.mockResolvedValue([]);
  });

  it('keeps existing appointments visible when a workspace reader has no management lookup permission', async () => {
    mocks.projectEngineerAssignmentLookups.mockRejectedValue(new Error('forbidden'));
    render(<CivilEngineeringProjectEngineerAssignmentsPanel projectId="project-1" />);

    expect(await screen.findByText('Amina Engineer')).toBeTruthy();
    expect(screen.getByText(/view appointment history/i)).toBeTruthy();
    expect(screen.queryByText('Eligible engineer')).toBeNull();
    expect(screen.queryByText('End')).toBeNull();
  });

  it('uses server-provided engineers and authority values rather than free-text identifiers', async () => {
    mocks.projectEngineerAssignmentLookups.mockResolvedValue({
      candidates: [{ userId: 'user-2', displayName: 'Kwame Supervisor', sourceCivilRole: 'TDC_SUPERVISING_CIVIL_ENGINEER' }],
      authorities: ['SiteSupervision', 'SiteSupervisionAndInstructions', 'FullProjectEngineer'],
      defaultAuthority: 'SiteSupervisionAndInstructions',
    });
    render(<CivilEngineeringProjectEngineerAssignmentsPanel projectId="project-1" />);

    await waitFor(() => expect(mocks.projectEngineerAssignmentLookups).toHaveBeenCalledWith('project-1'));
    expect(screen.getByText('Eligible engineer')).toBeTruthy();
    expect(screen.getByText('Appointment authority')).toBeTruthy();
    expect(screen.getByText('Replacement reason')).toBeTruthy();
    expect(screen.getByRole('button', { name: /replace project engineer/i })).toBeTruthy();
    expect(screen.queryByText(/user id/i)).toBeNull();
  });
});
