import { beforeEach, describe, expect, it, vi } from 'vitest';
import { projectService } from './projectService';

describe('projectService', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'test-token');
  });

  it('requests resource optimization suggestions from the expected endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(new Response(JSON.stringify([
      {
        userId: 'user-1',
        severity: 'High',
        recommendation: 'Reassign work.',
        matchedSkills: ['Scheduling'],
      },
    ]), { status: 200, headers: { 'Content-Type': 'application/json' } }));

    const result = await projectService.getResourceOptimizationSuggestions('2026-03-01', '2026-03-31');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/reports/resource-optimization?startDate=2026-03-01&endDate=2026-03-31'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      }),
    );
    expect(result).toHaveLength(1);
    expect(result[0].severity).toBe('High');
  });

  it('requests mobile project summary from the mobile api route', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(new Response(JSON.stringify({
      assignmentCount: 1,
      overdueCount: 0,
      pendingHours: 2,
      pendingExpenses: 15,
      assignments: [],
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));

    const result = await projectService.getMobileSummary();

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/mobile/projects/summary'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      }),
    );
    expect(result.assignmentCount).toBe(1);
  });

  it('submits an external deliverable to the shared portal endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(new Response(JSON.stringify({
      id: 'del-1',
      projectId: 'proj-1',
      title: 'Acceptance Pack',
      status: 'In Review',
      externalSubmissionAllowed: true,
      externalSignOffRequired: true,
      isExternalVisible: true,
      acceptanceNotes: 'Submitted from customer portal.',
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));

    const result = await projectService.submitExternalDeliverable('proj-1', 'del-1', {
      submittedDocumentId: 'doc-1',
      notes: 'Submitted from customer portal.',
    });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/external/my-projects/proj-1/deliverables/del-1/submit'),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
        body: JSON.stringify({
          submittedDocumentId: 'doc-1',
          notes: 'Submitted from customer portal.',
        }),
      }),
    );
    expect(result.status).toBe('In Review');
  });

  it('requests schedule analysis from the project analysis endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(new Response(JSON.stringify({
      projectId: 'proj-1',
      dependencyCount: 4,
      criticalPathTaskCount: 2,
      criticalPathWorkItemIds: ['task-1', 'task-2'],
      forecastFinishDate: '2026-04-12T00:00:00Z',
      totalSlackDays: 3,
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }));

    const result = await projectService.analyzeSchedule('proj-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/proj-1/schedule-analysis'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      }),
    );
    expect(result.criticalPathTaskCount).toBe(2);
    expect(result.criticalPathWorkItemIds).toEqual(['task-1', 'task-2']);
  });

  it('requests ai insights from the project intelligence endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(new Response(JSON.stringify([
      {
        category: 'Forecast',
        severity: 'Medium',
        title: 'Burn-rate variance',
        recommendation: 'Review high-cost work packages.',
      },
    ]), { status: 200, headers: { 'Content-Type': 'application/json' } }));

    const result = await projectService.getAiInsights('proj-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/proj-1/ai-insights'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      }),
    );
    expect(result).toHaveLength(1);
    expect(result[0].category).toBe('Forecast');
  });
});
