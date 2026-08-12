import { beforeEach, describe, expect, it, vi } from 'vitest';
import { projectService } from './projectService';

describe('projectService', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'test-token');
  });

  it('loads the authority-scoped Quantity Survey cost dashboard route', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ projectId: 'project-1', lines: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    await projectService.getQuantitySurveyCostDashboard('project-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        '/projects/project-1/quantity-survey-cost-dashboard'
      ),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      })
    );
  });

  it('posts a selected project member using the controlled user UUID', async () => {
    const dto = {
      userId: '58cafd8b-42ce-4f67-8dbb-08de862e82ee',
      role: 'QuantitySurveyor',
    };
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ id: 'member-1', ...dto, isActive: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    await projectService.addMember('cb8af66a-941e-4dd1-8fbd-d05f7c0cb681', dto);

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        '/projects/cb8af66a-941e-4dd1-8fbd-d05f7c0cb681/members'
      ),
      expect.objectContaining({ method: 'POST', body: JSON.stringify(dto) })
    );
  });

  it('rejects an invalid project-member identifier before calling the API', async () => {
    const fetchMock = vi.spyOn(global, 'fetch');

    await expect(
      projectService.addMember('project-1', {
        userId: 'admin',
        role: 'QuantitySurveyor',
      })
    ).rejects.toThrow('Select a valid active user');

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('requests resource optimization suggestions from the expected endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify([
          {
            userId: 'user-1',
            severity: 'High',
            recommendation: 'Reassign work.',
            matchedSkills: ['Scheduling'],
          },
        ]),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const result = await projectService.getResourceOptimizationSuggestions(
      '2026-03-01',
      '2026-03-31'
    );

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        '/projects/reports/resource-optimization?startDate=2026-03-01&endDate=2026-03-31'
      ),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      })
    );
    expect(result).toHaveLength(1);
    expect(result[0].severity).toBe('High');
  });

  it('requests mobile project summary from the mobile api route', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          assignmentCount: 1,
          overdueCount: 0,
          pendingHours: 2,
          pendingExpenses: 15,
          assignments: [],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const result = await projectService.getMobileSummary();

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/mobile/projects/summary'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      })
    );
    expect(result.assignmentCount).toBe(1);
  });

  it('submits an external deliverable to the shared portal endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'del-1',
          projectId: 'proj-1',
          title: 'Acceptance Pack',
          status: 'In Review',
          externalSubmissionAllowed: true,
          externalSignOffRequired: true,
          isExternalVisible: true,
          acceptanceNotes: 'Submitted from customer portal.',
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const result = await projectService.submitExternalDeliverable(
      'proj-1',
      'del-1',
      {
        submittedDocumentId: 'doc-1',
        notes: 'Submitted from customer portal.',
      }
    );

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        '/projects/external/my-projects/proj-1/deliverables/del-1/submit'
      ),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
        body: JSON.stringify({
          submittedDocumentId: 'doc-1',
          notes: 'Submitted from customer portal.',
        }),
      })
    );
    expect(result.status).toBe('In Review');
  });

  it('requests schedule analysis from the project analysis endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          projectId: 'proj-1',
          dependencyCount: 4,
          criticalPathTaskCount: 2,
          criticalPathWorkItemIds: ['task-1', 'task-2'],
          forecastFinishDate: '2026-04-12T00:00:00Z',
          totalSlackDays: 3,
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const result = await projectService.analyzeSchedule('proj-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/proj-1/schedule-analysis'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      })
    );
    expect(result.criticalPathTaskCount).toBe(2);
    expect(result.criticalPathWorkItemIds).toEqual(['task-1', 'task-2']);
  });

  it('requests ai insights from the project intelligence endpoint', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify([
          {
            category: 'Forecast',
            severity: 'Medium',
            title: 'Burn-rate variance',
            recommendation: 'Review high-cost work packages.',
          },
        ]),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const result = await projectService.getAiInsights('proj-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/proj-1/ai-insights'),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      })
    );
    expect(result).toHaveLength(1);
    expect(result[0].category).toBe('Forecast');
  });

  it('loads controlled Recorded measurements for a BoQ remeasurement', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          projectId: 'project-1',
          sourceApprovedBoqVersionId: 'boq-1',
          sourceApprovedBoqVersionNumber: 4,
          eligibleMeasurements: [],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const result =
      await projectService.getProjectBoqRemeasurementWorkspace('project-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        '/projects/project-1/boq-remeasurements/workspace'
      ),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
        }),
      })
    );
    expect(result.sourceApprovedBoqVersionNumber).toBe(4);
  });

  it('creates a measurement-derived candidate using stable selected sheet ids', async () => {
    const payload = {
      clientRequestId: 'request-1',
      measurementSheetIds: ['sheet-1', 'sheet-2'],
      changeSummary: 'Recorded site quantities for Block A.',
    };
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'version-5',
          versionType: 'Remeasurement',
          status: 'Draft',
          lines: [],
        }),
        { status: 201, headers: { 'Content-Type': 'application/json' } }
      )
    );

    const result = await projectService.createProjectBoqRemeasurement(
      'project-1',
      payload
    );

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/project-1/boq-remeasurements'),
      expect.objectContaining({ method: 'POST', body: JSON.stringify(payload) })
    );
    expect(result.id).toBe('version-5');
  });
});
