import { WorkflowAssignmentType, type WorkflowAssignmentRuleDto } from '@/types/workflow';

/**
 * An approval stage's approvers as the workflow designer edits them — and the ones it must leave alone.
 *
 * Part of `WorkflowDesigner.tsx`: the designer loads and saves every approval stage through these
 * functions. They live in their own file so the mapping can be unit-tested
 * (`WorkflowDesigner.approvers.test.ts`), which the 3,900-line component cannot be.
 *
 * The engine resolves five kinds of approver rule on an approval stage (`WorkflowAssignmentType`): a role,
 * a user, a person named by the record (`Dynamic` — a user id the record's workflow context carries under
 * the rule's `dynamicExpression`), the requester's manager, and whoever completed the previous step. The
 * designer edits only roles and users. Until 2026-10-02 it also DROPPED every other kind when it loaded a
 * stage, showed the step's required role in their place, and saved that role as an approver — so opening
 * and republishing a route rewired it without a word (cross-module defect #34; found when staff travel's
 * line-manager stage, addressed by name, would have become "every HR officer").
 *
 * Now the kinds the designer does not edit are kept exactly as stored, shown read-only, and written back
 * unchanged; the step's required role — the engine's fallback when no rule resolves — is kept too. A stage
 * of roles and users only loads and saves exactly as it did before.
 */
export interface ApprovalStageApprovers {
  approverRoles: string[];
  approverUsers: string[];
  /** Approver rules the designer does not edit, kept exactly as stored. */
  preservedApproverRules: WorkflowAssignmentRuleDto[];
  /**
   * The step's required role when the stage carries preserved rules: who the engine asks when none of them
   * resolves. Not an approver role, and not shown as one.
   */
  fallbackRequiredRole?: string;
}

/** The assignment type as the enum, whether the API sent its name or its number. */
export function toAssignmentType(value: unknown): WorkflowAssignmentType | undefined {
  if (typeof value === 'number') return value as WorkflowAssignmentType;
  if (typeof value !== 'string') return undefined;
  switch (value.trim().toLowerCase()) {
    case 'user':
    case '0':
      return WorkflowAssignmentType.User;
    case 'role':
    case '1':
      return WorkflowAssignmentType.Role;
    case 'dynamic':
    case '2':
      return WorkflowAssignmentType.Dynamic;
    case 'requestormanager':
    case 'requestor_manager':
    case 'requestor-manager':
    case '3':
      return WorkflowAssignmentType.RequestorManager;
    case 'previousstepuser':
    case 'previous_step_user':
    case 'previous-step-user':
    case '4':
      return WorkflowAssignmentType.PreviousStepUser;
    default:
      return undefined;
  }
}

const isEditedKind = (rule: WorkflowAssignmentRuleDto) => {
  const kind = toAssignmentType(rule.assignmentType);
  return kind === WorkflowAssignmentType.Role || kind === WorkflowAssignmentType.User;
};

/** Reads a stored approval stage: the roles and users to edit, and the rules to keep as they are. */
export function readApprovalStageApprovers(
  rules: readonly WorkflowAssignmentRuleDto[] | null | undefined,
  requiredRole?: string | null,
): ApprovalStageApprovers {
  const list = rules ?? [];
  const approverRoles = list
    .filter((rule) => toAssignmentType(rule.assignmentType) === WorkflowAssignmentType.Role && rule.role)
    .map((rule) => rule.role ?? '')
    .filter(Boolean);
  const approverUsers = list
    .filter((rule) => toAssignmentType(rule.assignmentType) === WorkflowAssignmentType.User && rule.userId)
    .map((rule) => rule.userId ?? '')
    .filter(Boolean);
  const preservedApproverRules = list.filter((rule) => !isEditedKind(rule)).map((rule) => ({ ...rule }));

  if (preservedApproverRules.length > 0) {
    // The required role is the engine's fallback for these rules, not an approver: keep it, do not show it.
    return {
      approverRoles,
      approverUsers,
      preservedApproverRules,
      fallbackRequiredRole: requiredRole?.trim() || undefined,
    };
  }

  // Unchanged for a stage of roles and users: with no role rule, the required role is shown as its role.
  return {
    approverRoles: approverRoles.length > 0 ? approverRoles : requiredRole ? [requiredRole] : [],
    approverUsers,
    preservedApproverRules: [],
  };
}

/**
 * The approver rules a stage saves: the roles and users exactly as the designer has always built them, then
 * the preserved rules unchanged (their kind written as the enum number, as the designer's own are).
 */
export function buildApprovalStageRules(input: {
  approverRoles: readonly string[];
  approverUsers: readonly string[];
  preservedApproverRules?: readonly WorkflowAssignmentRuleDto[];
  sequential: boolean;
}): WorkflowAssignmentRuleDto[] {
  const { approverRoles, approverUsers, sequential } = input;
  const rules: WorkflowAssignmentRuleDto[] = [];

  approverRoles.forEach((role, index) => {
    rules.push({
      assignmentType: WorkflowAssignmentType.Role,
      role,
      approvalGroup: sequential ? index + 1 : 1,
      priority: approverRoles.length - index,
    });
  });

  approverUsers.forEach((userId, index) => {
    rules.push({
      assignmentType: WorkflowAssignmentType.User,
      userId,
      approvalGroup: sequential ? approverRoles.length + index + 1 : 1,
      priority: approverUsers.length - index,
    });
  });

  (input.preservedApproverRules ?? []).forEach((rule) => {
    rules.push({ ...rule, assignmentType: toAssignmentType(rule.assignmentType) ?? rule.assignmentType });
  });

  return rules;
}

/**
 * The step's required role on save. A stage of roles and users takes its first role, as it always has; a
 * stage with preserved rules keeps the required role it was stored with — the fallback the engine uses
 * when none of them resolves.
 */
export function stageRequiredRole(
  approvers: Pick<ApprovalStageApprovers, 'approverRoles' | 'preservedApproverRules' | 'fallbackRequiredRole'>,
): string | undefined {
  if (approvers.preservedApproverRules.length > 0 && approvers.fallbackRequiredRole) {
    return approvers.fallbackRequiredRole;
  }
  return approvers.approverRoles[0];
}

/** How many approvers a stage names, for the designer's "at least one approver" and minimum checks. */
export function approverSlotCount(
  approvers: Pick<ApprovalStageApprovers, 'approverRoles' | 'approverUsers' | 'preservedApproverRules'>,
): number {
  return approvers.approverRoles.length + approvers.approverUsers.length + approvers.preservedApproverRules.length;
}

/** A preserved rule in words, for the read-only list on the stage. */
export function describePreservedApprover(rule: WorkflowAssignmentRuleDto): string {
  switch (toAssignmentType(rule.assignmentType)) {
    case WorkflowAssignmentType.Dynamic:
      return rule.dynamicExpression
        ? `Person named by the record (${rule.dynamicExpression})`
        : 'Person named by the record (no field set)';
    case WorkflowAssignmentType.RequestorManager:
      return "The requester's manager";
    case WorkflowAssignmentType.PreviousStepUser:
      return 'Whoever completed the previous step';
    default:
      return `Approver rule of type ${String(rule.assignmentType)}`;
  }
}
