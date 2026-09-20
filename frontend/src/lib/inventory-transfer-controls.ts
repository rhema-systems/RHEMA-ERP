export type InventoryTransferControlKind = 'resolve' | 'close';

export function getInventoryTransferStatusLabel(status: string, approvalRequired?: boolean): string {
  if (status === 'Approved' && approvalRequired === false) return 'Ready to ship';
  if (status === 'Submitted') return 'Pending Approval';
  if (status === 'InTransit') return 'In Transit';
  return status;
}

export interface InventoryTransferControlAction {
  actionType: string;
  actorUserId: string;
}

export interface InventoryTransferControlCapabilityInput {
  kind: InventoryTransferControlKind;
  status: string;
  hasOpenDiscrepancy: boolean;
  hasTransferPermission: boolean;
  approvalRequired?: boolean;
  currentUserId?: string | null;
  actions: InventoryTransferControlAction[];
}

export interface InventoryTransferControlCapability {
  allowed: boolean;
  reason?: string;
}

const PARTICIPATING_ACTIONS = new Set([
  'submitted',
  'approved',
  'dispatched',
  'received',
]);

const normalize = (value: string | null | undefined) => value?.trim().toLowerCase() ?? '';

export function getInventoryTransferControlCapability(
  input: InventoryTransferControlCapabilityInput
): InventoryTransferControlCapability {
  if (!input.hasTransferPermission) {
    return {
      allowed: false,
      reason: 'Your assigned role does not permit transfer discrepancy resolution or closure.',
    };
  }

  if (input.status !== 'Received') {
    return {
      allowed: false,
      reason: input.status === 'Completed'
        ? 'This transfer is already closed.'
        : 'The transfer must be fully received before discrepancy resolution or closure.',
    };
  }

  if (input.kind === 'resolve' && !input.hasOpenDiscrepancy) {
    return { allowed: false, reason: 'This transfer has no open discrepancy to resolve.' };
  }

  if (input.kind === 'close' && input.hasOpenDiscrepancy) {
    return { allowed: false, reason: 'Resolve every open discrepancy before closing the transfer.' };
  }

  const currentUserId = normalize(input.currentUserId);
  if (!currentUserId) {
    return { allowed: false, reason: 'Your current user identity could not be verified.' };
  }

  const participated = input.actions.some((action) =>
    PARTICIPATING_ACTIONS.has(normalize(action.actionType)) &&
    normalize(action.actorUserId) === currentUserId
  );

  if (participated && input.approvalRequired !== false) {
    return {
      allowed: false,
      reason: 'An independent authorized user who did not request, approve, dispatch or receive this transfer must perform this action.',
    };
  }

  return { allowed: true };
}

export function getInventoryTransferProblemMessage(error: unknown, fallback: string): string {
  if (!error || typeof error !== 'object') return fallback;

  const candidate = error as {
    detail?: unknown;
    message?: unknown;
    code?: unknown;
    extensions?: { code?: unknown };
    response?: {
      data?: unknown;
      detail?: unknown;
      message?: unknown;
      code?: unknown;
      extensions?: { code?: unknown };
    };
  };
  const responseData = candidate.response?.data;
  const problem = responseData && typeof responseData === 'object'
    ? responseData as { detail?: unknown; message?: unknown; code?: unknown; extensions?: { code?: unknown } }
    : undefined;

  const detail = typeof responseData === 'string'
    ? responseData
    : typeof problem?.detail === 'string'
      ? problem.detail
      : typeof problem?.message === 'string'
        ? problem.message
        : typeof candidate.response?.detail === 'string'
          ? candidate.response.detail
          : typeof candidate.response?.message === 'string'
            ? candidate.response.message
            : typeof candidate.detail === 'string'
              ? candidate.detail
              : typeof candidate.message === 'string'
                ? candidate.message
                : fallback;

  const code = problem?.code ??
    problem?.extensions?.code ??
    candidate.response?.code ??
    candidate.response?.extensions?.code ??
    candidate.code ??
    candidate.extensions?.code;

  return typeof code === 'string' && code.trim() && !detail.includes(code)
    ? `${detail} (${code})`
    : detail;
}
