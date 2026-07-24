import type {
  CreateProcurementTenderDocumentChangeRequest,
  IssueProcurementTenderDocumentRegisterRequest,
  ProcurementTenderDocumentAllowedAction,
  ProcurementTenderDocumentChange,
  ProcurementTenderDocumentChangeStatus,
  ProcurementTenderDocumentChangeType,
  ProcurementTenderDocumentFeeMode,
  ProcurementTenderDocumentRegister,
  ProcurementTenderDocumentTemplateStatus,
  SaveProcurementTenderDocumentTemplate,
} from '@/types/procurement-tender-document';

export const tenderDocumentTemplateStatusLabel: Record<
  ProcurementTenderDocumentTemplateStatus,
  string
> = {
  Draft: 'Draft',
  PendingApproval: 'Pending approval',
  Published: 'Published',
  Retired: 'Retired',
};

export const tenderDocumentChangeStatusLabel: Record<
  ProcurementTenderDocumentChangeStatus,
  string
> = {
  PendingApproval: 'Pending approval',
  Approved: 'Approved',
  Rejected: 'Rejected',
};

export const tenderDocumentChangeTypeLabel: Record<
  ProcurementTenderDocumentChangeType,
  string
> = {
  Addendum: 'Addendum',
  SubmissionDeadlineExtension: 'Submission deadline extension',
  BidValidityExtension: 'Bid-validity extension',
};

export const procurementMethodLabel: Record<string, string> = {
  RequestForQuotation: 'RFQ',
  NationalCompetitiveTendering: 'NCT',
  InternationalCompetitiveTendering: 'ICT',
  RestrictedTendering: 'Restricted tendering',
  SingleSource: 'Single source',
  PettyPurchase: 'Petty purchase',
  FrameworkCallOff: 'Framework call-off',
  QualityBasedSelection: 'QBS',
  QualityAndCostBasedSelection: 'QCBS',
};

export const hasTenderDocumentAction = (
  allowedActions: ProcurementTenderDocumentAllowedAction[] | undefined,
  action: ProcurementTenderDocumentAllowedAction
) =>
  Boolean(
    allowedActions?.some(
      (candidate) => candidate.toLowerCase() === action.toLowerCase()
    )
  );

export const hasAnyTenderDocumentAction = (
  allowedActions: ProcurementTenderDocumentAllowedAction[] | undefined,
  actions: ProcurementTenderDocumentAllowedAction[]
) => actions.some((action) => hasTenderDocumentAction(allowedActions, action));

export const validateTenderDocumentTemplate = (
  value: SaveProcurementTenderDocumentTemplate
) => {
  if (!value.templateCode.trim()) return 'Template code is required.';
  if (!value.name.trim()) return 'Template name is required.';
  if (!value.documentTypeCode.trim())
    return 'Document type code is required.';
  if (!value.applicableMethods.length)
    return 'Select at least one procurement method.';
  if (!value.policySetId) return 'Exact current policy is required.';
  if (!value.policySetCode.trim()) return 'Policy code is required.';
  if (value.policySetVersion < 1) return 'Policy version is required.';
  if (!value.sourceConfigurationProfileId)
    return 'Source configuration profile is required.';
  if (!value.contentReference.trim())
    return 'Controlled content reference is required.';
  if (!/^[A-Fa-f0-9]{64}$/.test(value.contentChecksumSha256.trim()))
    return 'Content checksum must be a 64-character SHA-256 value.';
  if (!value.workflowDefinitionId)
    return 'Exact Published workflow is required.';
  if (!value.effectiveFromUtc) return 'Effective-from date is required.';
  const effectiveFrom = new Date(value.effectiveFromUtc);
  if (Number.isNaN(effectiveFrom.getTime()))
    return 'Effective-from date is invalid.';
  const effectiveTo = value.effectiveToUtc
    ? new Date(value.effectiveToUtc)
    : undefined;
  if (effectiveTo && Number.isNaN(effectiveTo.getTime()))
    return 'Effective-to date is invalid.';
  if (
    effectiveTo &&
    effectiveTo <= effectiveFrom
  )
    return 'Effective-to date must follow effective-from date.';
  return undefined;
};

export const validateTenderDocumentIssue = (
  request: IssueProcurementTenderDocumentRegisterRequest,
  feeMode: ProcurementTenderDocumentFeeMode,
  feeAmount: number
) => {
  if (!request.recipientName.trim()) return 'Recipient name is required.';
  if (!request.receiptNumber.trim()) return 'Receipt number is required.';
  if (!request.issueChannel.trim()) return 'Issue channel is required.';
  if (!request.evidenceReference.trim())
    return 'Issue evidence reference is required.';
  if (feeMode === 'Paid') {
    if (request.amountPaid !== feeAmount)
      return `Amount paid must equal the controlled fee of ${feeAmount}.`;
    if (!request.paymentReference?.trim())
      return 'Paid documents require a payment reference.';
  } else if (request.amountPaid !== 0 || request.paymentReference?.trim()) {
    return 'Free documents cannot carry payment details.';
  }
  return undefined;
};

export const validateTenderDocumentChange = (
  request: CreateProcurementTenderDocumentChangeRequest,
  register: Pick<
    ProcurementTenderDocumentRegister,
    'effectiveSubmissionDeadlineUtc' | 'effectiveBidValidityUntilUtc'
  >,
  now = new Date()
) => {
  if (!request.reason.trim()) return 'A governed reason is required.';
  if (!request.workflowDefinitionId)
    return 'Exact Published workflow is required.';
  if (!request.evidenceReference.trim())
    return 'Shared evidence reference is required.';
  if (request.changeType === 'Addendum' && !request.newTemplateVersionId)
    return 'Addendum requires an exact approved replacement version.';
  if (request.changeType === 'SubmissionDeadlineExtension') {
    if (!request.newValueUtc) return 'New submission deadline is required.';
    const next = new Date(request.newValueUtc);
    if (Number.isNaN(next.getTime()))
      return 'New submission deadline is invalid.';
    if (next <= now) return 'New submission deadline must be in the future.';
    if (next <= new Date(register.effectiveSubmissionDeadlineUtc))
      return 'New submission deadline must move forward.';
  }
  if (request.changeType === 'BidValidityExtension') {
    if (!request.newValueUtc) return 'New bid-validity date is required.';
    const next = new Date(request.newValueUtc);
    if (Number.isNaN(next.getTime()))
      return 'New bid-validity date is invalid.';
    if (next <= now) return 'New bid-validity date must be in the future.';
    if (next <= new Date(register.effectiveBidValidityUntilUtc))
      return 'New bid-validity date must move forward.';
  }
  return undefined;
};

export const pendingMandatoryAcknowledgements = (
  change: Pick<
    ProcurementTenderDocumentChange,
    'status' | 'requiresAcknowledgement' | 'recipients'
  >
) => {
  if (change.status !== 'Approved' || !change.requiresAcknowledgement) return 0;
  return change.recipients.filter((recipient) => !recipient.acknowledgement)
    .length;
};

export const firstTenderDocumentBlock = (
  blockedReasons: string[] | undefined
) => blockedReasons?.find(Boolean);
