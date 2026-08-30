import { apiService } from '@/services/api.service';
import type {
  AssignCivilEngineeringProjectEngineerRequest,
  CivilEngineeringProjectEngineerAssignment,
  CivilEngineeringProjectEngineerAssignmentLookups,
  CivilEngineeringProjectEngineerAssignmentRevision,
  EndCivilEngineeringProjectEngineerAssignmentRequest,
} from '@/types/civil-engineering-supervision';

const root = '/projects/civil-engineering/supervision';

export const civilEngineeringSupervisionService = {
  listProjectEngineerAssignments: (projectId: string) =>
    apiService.get<CivilEngineeringProjectEngineerAssignment[]>(
      `${root}/project-engineer-assignments`,
      { projectId }
    ),
  projectEngineerAssignmentLookups: (projectId: string) =>
    apiService.get<CivilEngineeringProjectEngineerAssignmentLookups>(
      `${root}/project-engineer-assignments/lookups`,
      { projectId }
    ),
  assignProjectEngineer: (
    projectId: string,
    request: AssignCivilEngineeringProjectEngineerRequest
  ) =>
    apiService.post<CivilEngineeringProjectEngineerAssignment>(
      `${root}/projects/${projectId}/project-engineer-assignments`,
      request
    ),
  endProjectEngineerAssignment: (
    assignmentId: string,
    request: EndCivilEngineeringProjectEngineerAssignmentRequest
  ) =>
    apiService.post<CivilEngineeringProjectEngineerAssignment>(
      `${root}/project-engineer-assignments/${assignmentId}/end`,
      request
    ),
  projectEngineerAssignmentHistory: (assignmentId: string) =>
    apiService.get<CivilEngineeringProjectEngineerAssignmentRevision[]>(
      `${root}/project-engineer-assignments/${assignmentId}/history`
    ),
};
