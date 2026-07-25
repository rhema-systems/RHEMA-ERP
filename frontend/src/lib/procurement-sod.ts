import type {
  ProcurementSodGuardDecision,
  ProcurementSodGuardForm,
  ProcurementSodGuardRequest,
} from '@/types/procurement-sod';

export const requiredProcurementSodControlCodes = [
  'SOD-INITIATOR-APPROVER',
  'SOD-PO-CREATOR-RECEIVER',
  'SOD-SUPPLIER-CONTROLLER-AWARD',
  'SOD-STOCK-ISSUER-ADJUSTMENT',
  'SOD-INVOICE-PROCESSOR-PAYMENT',
  'SOD-EVALUATOR-AWARD-APPROVER',
] as const;

export const parseProcurementSodUserIds = (value: string) => Array.from(new Set(
  value.split(/[\s,;]+/).map(item => item.trim()).filter(Boolean),
));

export const createProcurementSodGuardRequest = (form: ProcurementSodGuardForm): ProcurementSodGuardRequest => ({
  controlCode: form.controlCode,
  sourceType: form.sourceType.trim(),
  sourceReference: form.sourceReference.trim(),
  prohibitedActorUserIds: parseProcurementSodUserIds(form.prohibitedActorUserIds),
});

export const canRunProcurementSodCheck = (form: ProcurementSodGuardForm) =>
  Boolean(form.controlCode && form.sourceType.trim() && form.sourceReference.trim() &&
    parseProcurementSodUserIds(form.prohibitedActorUserIds).length > 0);

export const isProcurementSodActionAvailable = (decision?: ProcurementSodGuardDecision) =>
  decision?.allowed === true && decision.isHardStop === false;

export const procurementSodDecisionTone = (decision?: ProcurementSodGuardDecision) =>
  !decision ? 'border-border bg-muted/20' : decision.allowed
    ? 'border-emerald-500/40 bg-emerald-500/5 text-emerald-900'
    : 'border-destructive/40 bg-destructive/5 text-destructive';
