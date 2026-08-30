import { apiService } from '@/services/api.service';
import type {
  CivilEngineeringDirectTask,
  CivilEngineeringDirectTaskFeedback,
  CivilEngineeringDirectTaskFeedbackLookups,
  CivilEngineeringDirectTaskLookups,
  CreateCivilEngineeringDirectTaskRequest,
  EscalateCivilEngineeringUrgentTasksRequest,
  ProcessCivilEngineeringDirectTaskFeedbackRequest,
} from '@/types/civil-engineering-direct-task';

const root = (projectId: string) => `/projects/${projectId}/civil-engineering/direct-tasks`;

export const civilEngineeringDirectTaskService = {
  lookups: (projectId: string) => apiService.get<CivilEngineeringDirectTaskLookups>(`${root(projectId)}/lookups`),
  list: (projectId: string) => apiService.get<CivilEngineeringDirectTask[]>(root(projectId)),
  create: (projectId: string, request: CreateCivilEngineeringDirectTaskRequest) => apiService.post<CivilEngineeringDirectTask>(root(projectId), request),
  escalateUrgent: (projectId: string, request: EscalateCivilEngineeringUrgentTasksRequest) => apiService.post<CivilEngineeringDirectTask[]>(`${root(projectId)}/escalate-urgent`, request),
  feedbackLookups: (projectId: string, taskId: string) => apiService.get<CivilEngineeringDirectTaskFeedbackLookups>(`${root(projectId)}/${taskId}/feedback/lookups`),
  feedback: (projectId: string, taskId: string) => apiService.get<CivilEngineeringDirectTaskFeedback[]>(`${root(projectId)}/${taskId}/feedback`),
  submitAssigneeFeedback: (projectId: string, taskId: string, request: ProcessCivilEngineeringDirectTaskFeedbackRequest) =>
    apiService.post<CivilEngineeringDirectTask>(`${root(projectId)}/${taskId}/feedback/assignee`, request),
  submitReviewFeedback: (projectId: string, taskId: string, request: ProcessCivilEngineeringDirectTaskFeedbackRequest) =>
    apiService.post<CivilEngineeringDirectTask>(`${root(projectId)}/${taskId}/feedback/review`, request),
};
