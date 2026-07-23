export type ProcurementWarehouseScopeMode = 'None' | 'All' | 'Restricted';
export type ProcurementCommitteeStatus = 'Draft' | 'Active' | 'Retired';
export type ProcurementCommitteeMemberKind = 'Chair' | 'VotingMember' | 'NonVotingMember' | 'Observer' | 'Secretary';

export interface ProcurementAccessReadiness {
  requiredRoleCount: number; configuredRoleCount: number; requiredPermissionCount: number; configuredPermissionCount: number;
  requiredCommitteeCount: number; configuredCommitteeCount: number; readyCommitteeCount: number;
  requiredWorkflowCount: number; configuredWorkflowCount: number; publishedWorkflowCount: number;
  activeAssignmentCount: number; internalAuditIsReadOnly: boolean; isReadyForUat: boolean; issues: string[];
}

export interface ProcurementAccessRole {
  code: string; name: string; description: string; isConfigured: boolean; isReadOnly: boolean;
  requiredPermissionCount: number; configuredPermissionCount: number; permissionCodes: string[];
  missingPermissionCodes: string[]; unexpectedMutationPermissions: string[];
}

export interface ProcurementAccessPermission {
  code: string; name: string; description: string; isMutation: boolean; isWarehouseScoped: boolean; isConfigured: boolean;
}

export interface ProcurementAccessUser { userId: string; username: string; displayName: string; isActive: boolean; }
export interface ProcurementAccessWarehouse { warehouseId: string; code: string; name: string; isActive: boolean; }

export interface ProcurementResponsibilityAssignment {
  id: string; userId: string; username: string; userDisplayName: string; roleId: string; roleName: string; roleDisplayName: string;
  warehouseScopeMode: ProcurementWarehouseScopeMode; warehouses: ProcurementAccessWarehouse[];
  effectiveFrom: string; effectiveTo?: string; isActive: boolean; reason: string; rowVersion: string;
}

export interface SaveProcurementResponsibilityAssignment {
  userId: string; roleName: string; warehouseScopeMode: ProcurementWarehouseScopeMode; warehouseIds: string[];
  effectiveFrom: string; effectiveTo?: string; isActive: boolean; reason: string; rowVersion?: string;
}

export interface ProcurementCommitteeMember {
  id: string; assignmentId: string; userId: string; username: string; userDisplayName: string; roleName: string;
  memberKind: ProcurementCommitteeMemberKind; isVoting: boolean; isActive: boolean; effectiveFrom: string; effectiveTo?: string; rowVersion: string;
}

export interface ProcurementCommittee {
  id: string; code: string; name: string; description?: string; committeeType: string; status: ProcurementCommitteeStatus;
  requiredQuorum: number; activeVotingMemberCount: number; meetsQuorum: boolean; requiredRoleName: string;
  effectiveFrom: string; effectiveTo?: string; rowVersion: string; members: ProcurementCommitteeMember[];
}

export interface UpdateProcurementCommittee {
  name: string; description?: string; requiredQuorum: number; status: ProcurementCommitteeStatus;
  effectiveFrom: string; effectiveTo?: string; reason: string; rowVersion: string;
}

export interface SaveProcurementCommitteeMember {
  assignmentId: string; memberKind: ProcurementCommitteeMemberKind; isVoting: boolean;
  effectiveFrom: string; effectiveTo?: string; reason: string;
}

export interface ProcurementAccessWorkflow {
  templateCode: string; templateName: string; entityTypeCode: string; workflowDefinitionId?: string; version?: number;
  status: string; isPublished: boolean; initiatorRoleName: string; approvalRoleName: string; sourceDecisionKeys: string;
}

export interface ProcurementAccessCapabilityRequest {
  permissionCode: string; warehouseId?: string; committeeCode?: string; sourceType: string; sourceReference: string;
}

export interface ProcurementAccessCapabilityDecision {
  allowed: boolean; code: string; message: string; actorUserId: string; tenantId: string; permissionCode: string;
  warehouseId?: string; committeeCode?: string; matchedAssignmentIds: string[]; matchedRoles: string[];
  correlationId: string; evaluatedAtUtc: string;
}

export interface ProcurementAccessAudit {
  id: string; timestamp: string; actorUserId: string; actorName: string; action: string; resource: string;
  resourceId?: string; oldValues?: string; newValues?: string;
}
