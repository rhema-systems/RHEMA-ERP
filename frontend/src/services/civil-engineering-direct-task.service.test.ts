import { describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringDirectTaskService } from './civil-engineering-direct-task.service';

describe('civilEngineeringDirectTaskService', () => {
  it('uses project-scoped governed assignment and feedback routes', () => {
    const request = { clientRequestId: 'request', title: 'Set out footing', instructions: 'Confirm the approved setting-out coordinates.', assignedToUserId: 'user', assignedRoleId: 'role', urgency: 'Priority' as const, dueDate: '2026-08-22T09:00:00.000Z' };
    const feedback = { clientRequestId: 'feedback', action: 'Complete' as const, message: 'Completed against the approved drawing.', measurementValue: 12.5, measurementUnitId: 'unit', capturedOfflineAtUtc: '2026-08-21T09:00:00.000Z', rowVersion: 'row-version' };
    civilEngineeringDirectTaskService.lookups('project');
    civilEngineeringDirectTaskService.list('project');
    civilEngineeringDirectTaskService.create('project', request);
    civilEngineeringDirectTaskService.escalateUrgent('project', { clientRequestId: 'urgent-escalation' });
    civilEngineeringDirectTaskService.feedbackLookups('project', 'task');
    civilEngineeringDirectTaskService.feedback('project', 'task');
    civilEngineeringDirectTaskService.submitAssigneeFeedback('project', 'task', feedback);
    civilEngineeringDirectTaskService.submitReviewFeedback('project', 'task', { ...feedback, action: 'Accept' });
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/project/civil-engineering/direct-tasks/lookups');
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/project/civil-engineering/direct-tasks');
    expect(api.get).toHaveBeenNthCalledWith(3, '/projects/project/civil-engineering/direct-tasks/task/feedback/lookups');
    expect(api.get).toHaveBeenNthCalledWith(4, '/projects/project/civil-engineering/direct-tasks/task/feedback');
    expect(api.post).toHaveBeenCalledWith('/projects/project/civil-engineering/direct-tasks', request);
    expect(api.post).toHaveBeenCalledWith('/projects/project/civil-engineering/direct-tasks/escalate-urgent', { clientRequestId: 'urgent-escalation' });
    expect(api.post).toHaveBeenCalledWith('/projects/project/civil-engineering/direct-tasks/task/feedback/assignee', feedback);
    expect(api.post).toHaveBeenCalledWith('/projects/project/civil-engineering/direct-tasks/task/feedback/review', { ...feedback, action: 'Accept' });
  });
});
