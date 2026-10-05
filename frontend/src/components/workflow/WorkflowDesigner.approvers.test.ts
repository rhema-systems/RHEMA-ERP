import { describe, expect, it } from 'vitest';

import { WorkflowAssignmentType, type WorkflowAssignmentRuleDto } from '@/types/workflow';
import {
  approverSlotCount,
  buildApprovalStageRules,
  describePreservedApprover,
  readApprovalStageApprovers,
  stageRequiredRole,
} from './WorkflowDesigner.approvers';

/**
 * The designer's approval-stage mapping before cross-module defect #34 was fixed, copied verbatim: load kept
 * only role and user rules (the required role standing in when no role rule was left), and save rebuilt the
 * rules from those alone. A stage of roles and users must still save exactly as this did.
 */
const before = {
  load(rules: WorkflowAssignmentRuleDto[], requiredRole?: string) {
    const configuredApproverRoles = rules
      .filter((rule) => rule.assignmentType === WorkflowAssignmentType.Role && rule.role)
      .map((rule) => rule.role ?? '')
      .filter(Boolean);
    const approverRoles = configuredApproverRoles.length > 0 ? configuredApproverRoles : requiredRole ? [requiredRole] : [];
    const approverUsers = rules
      .filter((rule) => rule.assignmentType === WorkflowAssignmentType.User && rule.userId)
      .map((rule) => rule.userId ?? '')
      .filter(Boolean);
    return { approverRoles, approverUsers };
  },
  save(approverRoles: string[], approverUsers: string[], sequential: boolean) {
    const approverRules: WorkflowAssignmentRuleDto[] = [];
    approverRoles.forEach((role, index) => {
      approverRules.push({ assignmentType: WorkflowAssignmentType.Role, role, approvalGroup: sequential ? index + 1 : 1, priority: approverRoles.length - index });
    });
    approverUsers.forEach((userId, index) => {
      approverRules.push({ assignmentType: WorkflowAssignmentType.User, userId, approvalGroup: sequential ? approverRoles.length + index + 1 : 1, priority: approverUsers.length - index });
    });
    return { approverRules, requiredRole: approverRoles.length > 0 ? approverRoles[0] : undefined };
  },
};

/**
 * Staff travel's line-manager stage exactly as `GET api/Workflow/definitions/{id}` returned it on UAT on
 * 2026-10-02 (version 2 of *Staff Travel Approval*, step required role "HR") — the API names the kind and
 * sends the unused fields as null.
 */
const travelLineStage = [
  { approvalGroup: 1, condition: null, assignmentType: 'Dynamic', userId: null, role: null, dynamicExpression: 'lineApproverUserId', priority: 0 },
  { approvalGroup: 1, condition: null, assignmentType: 'Dynamic', userId: null, role: null, dynamicExpression: 'lineApproverUserId2', priority: 0 },
] as unknown as WorkflowAssignmentRuleDto[];

describe('approval stage approvers — the designer keeps what it does not edit (defect #34)', () => {
  it("loads travel's line-manager stage without inventing an HR approver", () => {
    const read = readApprovalStageApprovers(travelLineStage, 'HR');
    expect(read.approverRoles).toEqual([]);
    expect(read.approverUsers).toEqual([]);
    expect(read.preservedApproverRules).toHaveLength(2);
    expect(read.fallbackRequiredRole).toBe('HR');
    expect(approverSlotCount(read)).toBe(2);
  });

  it('saves that stage back with both named-by-the-record rules and the HR fallback intact', () => {
    const read = readApprovalStageApprovers(travelLineStage, 'HR');
    const saved = buildApprovalStageRules({ ...read, sequential: false });
    expect(saved).toEqual([
      { approvalGroup: 1, condition: null, assignmentType: WorkflowAssignmentType.Dynamic, userId: null, role: null, dynamicExpression: 'lineApproverUserId', priority: 0 },
      { approvalGroup: 1, condition: null, assignmentType: WorkflowAssignmentType.Dynamic, userId: null, role: null, dynamicExpression: 'lineApproverUserId2', priority: 0 },
    ]);
    expect(stageRequiredRole(read)).toBe('HR');
  });

  it('survives being loaded and saved twice — nothing drifts', () => {
    const once = buildApprovalStageRules({ ...readApprovalStageApprovers(travelLineStage, 'HR'), sequential: false });
    const read2 = readApprovalStageApprovers(once, stageRequiredRole(readApprovalStageApprovers(travelLineStage, 'HR')));
    expect(buildApprovalStageRules({ ...read2, sequential: false })).toEqual(once);
    expect(stageRequiredRole(read2)).toBe('HR');
  });

  it('saves a stage of roles and users exactly as the designer did before', () => {
    const rules: WorkflowAssignmentRuleDto[] = [
      { assignmentType: WorkflowAssignmentType.Role, role: 'Manager', approvalGroup: 1, priority: 3 },
      { assignmentType: WorkflowAssignmentType.Role, role: 'HR', approvalGroup: 1, priority: 2 },
      { assignmentType: WorkflowAssignmentType.Role, role: 'TenantAdmin', approvalGroup: 1, priority: 1 },
      { assignmentType: WorkflowAssignmentType.User, userId: 'u-1', approvalGroup: 1, priority: 1 },
    ];
    for (const sequential of [false, true]) {
      const old = before.load(rules);
      const read = readApprovalStageApprovers(rules);
      expect(read.approverRoles).toEqual(old.approverRoles);
      expect(read.approverUsers).toEqual(old.approverUsers);
      expect(read.preservedApproverRules).toEqual([]);
      const oldSave = before.save(old.approverRoles, old.approverUsers, sequential);
      expect(buildApprovalStageRules({ ...read, sequential })).toEqual(oldSave.approverRules);
      expect(stageRequiredRole(read)).toBe(oldSave.requiredRole);
    }
  });

  it('keeps showing the required role as the approver of a stage with no rules, as before', () => {
    const read = readApprovalStageApprovers([], 'HR');
    expect(read.approverRoles).toEqual(before.load([], 'HR').approverRoles);
    expect(read.approverRoles).toEqual(['HR']);
    expect(stageRequiredRole(read)).toBe('HR');
  });

  it('keeps a mixed stage whole: roles and users edited, the named rule kept, the stored fallback kept', () => {
    const rules: WorkflowAssignmentRuleDto[] = [
      { assignmentType: WorkflowAssignmentType.Role, role: 'Finance Manager', approvalGroup: 1, priority: 1 },
      { assignmentType: WorkflowAssignmentType.Dynamic, dynamicExpression: 'budgetHolderUserId', approvalGroup: 2, priority: 5,
        condition: { conditionType: 0, expression: 'amount > 1000' } as WorkflowAssignmentRuleDto['condition'] },
    ];
    const read = readApprovalStageApprovers(rules, 'Finance Manager');
    expect(read.approverRoles).toEqual(['Finance Manager']);
    expect(read.preservedApproverRules).toEqual([rules[1]]);
    const saved = buildApprovalStageRules({ ...read, sequential: false });
    expect(saved[0]).toEqual({ assignmentType: WorkflowAssignmentType.Role, role: 'Finance Manager', approvalGroup: 1, priority: 1 });
    expect(saved[1]).toEqual(rules[1]);
    expect(stageRequiredRole(read)).toBe('Finance Manager');
  });

  it('reads the kind whether the API sends its name or its number', () => {
    const read = readApprovalStageApprovers([
      { assignmentType: 'Role' as unknown as WorkflowAssignmentType, role: 'HR', priority: 0 },
      { assignmentType: 3 as WorkflowAssignmentType, priority: 0 },
      { assignmentType: 'PreviousStepUser' as unknown as WorkflowAssignmentType, priority: 0 },
    ]);
    expect(read.approverRoles).toEqual(['HR']);
    expect(read.preservedApproverRules.map((rule) => describePreservedApprover(rule))).toEqual([
      "The requester's manager",
      'Whoever completed the previous step',
    ]);
  });

  it('describes a named-by-the-record approver by its field', () => {
    expect(describePreservedApprover(travelLineStage[0])).toBe('Person named by the record (lineApproverUserId)');
  });
});
