export type CivilEngineeringProjectEngineerAuthority =
  | 'SiteSupervision'
  | 'SiteSupervisionAndInstructions'
  | 'FullProjectEngineer';

export type CivilEngineeringProjectEngineerCandidate = {
  userId: string;
  displayName: string;
  sourceCivilRole: string;
};

export type CivilEngineeringProjectEngineerAssignmentLookups = {
  candidates: CivilEngineeringProjectEngineerCandidate[];
  authorities: CivilEngineeringProjectEngineerAuthority[];
  defaultAuthority: CivilEngineeringProjectEngineerAuthority;
};

export type CivilEngineeringProjectEngineerAssignment = {
  id: string;
  projectId: string;
  projectMemberId: string;
  assignedUserId: string;
  assignedUserName: string;
  sourceCivilRole: string;
  projectRole: string;
  authority: CivilEngineeringProjectEngineerAuthority;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
  reason?: string | null;
  createdAt: string;
  createdBy: string;
  rowVersion: string;
};

export type CivilEngineeringProjectEngineerAssignmentRevision = {
  id: string;
  action: string;
  actorUserId: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  before?: Record<string, unknown> | null;
  after: Record<string, unknown>;
  timestamp: string;
};

export type AssignCivilEngineeringProjectEngineerRequest = {
  clientRequestId: string;
  assignedUserId: string;
  authority: CivilEngineeringProjectEngineerAuthority;
  effectiveFrom: string;
  reason?: string;
};

export type EndCivilEngineeringProjectEngineerAssignmentRequest = {
  clientRequestId: string;
  rowVersion: string;
  effectiveTo: string;
  reason: string;
};
