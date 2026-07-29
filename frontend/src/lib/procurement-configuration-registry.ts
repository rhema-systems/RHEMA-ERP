import type {
  ProcurementDecisionFieldDefinition,
  ProcurementDecisionFormDefinition,
} from '@/types/procurement-configuration';

const categories = [
  { value: 'goods', label: 'Goods' },
  { value: 'works', label: 'Works' },
  { value: 'technicalServices', label: 'Technical services' },
  { value: 'consultancyServices', label: 'Consultancy services' },
  { value: 'generalServices', label: 'General services' },
];

const methods = [
  { value: 'requestForQuotation', label: 'Request for quotation' },
  { value: 'nationalCompetitiveTendering', label: 'National competitive tendering' },
  { value: 'internationalCompetitiveTendering', label: 'International competitive tendering' },
  { value: 'restrictedTendering', label: 'Restricted tendering' },
  { value: 'singleSource', label: 'Single source' },
  { value: 'pettyPurchase', label: 'Petty purchase' },
  { value: 'frameworkCallOff', label: 'Framework call-off' },
];

const currencies = [
  { value: 'GHS', label: 'GHS' },
  { value: 'USD', label: 'USD' },
  { value: 'EUR', label: 'EUR' },
  { value: 'GBP', label: 'GBP' },
];

const effectiveFields: ProcurementDecisionFieldDefinition[] = [
  { key: 'effectiveFrom', label: 'Effective from', type: 'date', required: true },
  { key: 'effectiveTo', label: 'Effective to', type: 'date' },
];

const define = (
  decisionKey: string,
  fields: ProcurementDecisionFieldDefinition[],
  defaults: Record<string, unknown> = {},
): ProcurementDecisionFormDefinition => ({
  decisionKey,
  fields: [...fields, ...effectiveFields],
  defaults: { ...defaults, effectiveFrom: '', effectiveTo: '' },
});

export const procurementDecisionFormRegistry: Record<string, ProcurementDecisionFormDefinition> = {
  'DEC-001': define('DEC-001', [
    { key: 'category', label: 'Category', type: 'select', options: categories, required: true },
    { key: 'serviceClass', label: 'Service class', type: 'text', required: true },
    { key: 'method', label: 'Procurement method', type: 'select', options: methods, required: true },
    { key: 'currencyCode', label: 'Currency', type: 'select', options: currencies, required: true },
    { key: 'lowerBound', label: 'Lower bound', type: 'number', min: 0, step: 0.01, required: true },
    { key: 'upperBound', label: 'Upper bound', type: 'number', min: 0, step: 0.01 },
    { key: 'lowerInclusive', label: 'Include lower bound', type: 'boolean' },
    { key: 'upperInclusive', label: 'Include upper bound', type: 'boolean' },
    { key: 'statutoryReference', label: 'Statutory reference', type: 'textarea', required: true },
  ], { category: 'goods', method: 'requestForQuotation', currencyCode: 'GHS', lowerBound: 0, lowerInclusive: true, upperInclusive: true }),
  'DEC-002': define('DEC-002', [
    { key: 'authorityLevel', label: 'Authority level', type: 'text', required: true },
    { key: 'currencyCode', label: 'Currency', type: 'select', options: currencies, required: true },
    { key: 'lowerBound', label: 'Lower bound', type: 'number', min: 0, step: 0.01, required: true },
    { key: 'upperBound', label: 'Upper bound', type: 'number', min: 0, step: 0.01 },
    { key: 'lowerInclusive', label: 'Include lower bound', type: 'boolean' },
    { key: 'upperInclusive', label: 'Include upper bound', type: 'boolean' },
    { key: 'escalationAuthority', label: 'Escalation authority', type: 'text', required: true },
    { key: 'applicableCategories', label: 'Applicable categories', type: 'textList', required: true, placeholder: 'goods, works' },
  ], { currencyCode: 'GHS', lowerBound: 0, lowerInclusive: true, upperInclusive: true, applicableCategories: ['goods'] }),
  'DEC-003': define('DEC-003', [
    { key: 'transactionEntityType', label: 'Transaction/entity type', type: 'text', required: true },
    { key: 'policySelector', label: 'Policy selector', type: 'text', required: true },
    { key: 'workflowDefinitionId', label: 'Shared workflow definition ID', type: 'text', required: true },
    { key: 'applicabilityConditions', label: 'Applicability conditions', type: 'textarea', required: true },
  ]),
  'DEC-004': define('DEC-004', [
    { key: 'authorityOrCommittee', label: 'Authority or committee', type: 'text', required: true },
    { key: 'roleType', label: 'Role type', type: 'select', options: [{ value: 'Approver', label: 'Approver' }, { value: 'Committee', label: 'Committee' }, { value: 'Observer', label: 'Observer' }], required: true },
    { key: 'quorum', label: 'Quorum', type: 'number', min: 1, required: true },
    { key: 'evidenceRequirements', label: 'Evidence requirements', type: 'textList', required: true },
    { key: 'minimumAmount', label: 'Minimum amount', type: 'number', min: 0, step: 0.01 },
    { key: 'maximumAmount', label: 'Maximum amount', type: 'number', min: 0, step: 0.01 },
    { key: 'applicableCategories', label: 'Applicable categories', type: 'textList', required: true },
    { key: 'sequence', label: 'Sequence', type: 'number', min: 1, required: true },
    { key: 'stageGroup', label: 'Stage group', type: 'text', required: true },
    { key: 'escalationAuthority', label: 'Escalation authority', type: 'text', required: true },
  ], { roleType: 'Approver', quorum: 1, sequence: 1, evidenceRequirements: [], applicableCategories: [] }),
  'DEC-005': define('DEC-005', [
    { key: 'pettyThreshold', label: 'Petty threshold', type: 'number', min: 0.01, step: 0.01, required: true },
    { key: 'currencyCode', label: 'Currency', type: 'select', options: currencies, required: true },
    { key: 'waiverEligible', label: 'Waiver eligible', type: 'boolean' },
    { key: 'justificationRequired', label: 'Justification required', type: 'boolean' },
    { key: 'evidenceRequirements', label: 'Evidence requirements', type: 'textList', required: true },
    { key: 'approverRole', label: 'Approver role', type: 'text', required: true },
    { key: 'expiryDate', label: 'Expiry date', type: 'date' },
  ], { currencyCode: 'GHS', waiverEligible: false, justificationRequired: true, evidenceRequirements: [] }),
  'DEC-006': define('DEC-006', [
    { key: 'method', label: 'Exception method', type: 'select', options: methods.filter(item => ['restrictedTendering', 'singleSource'].includes(item.value)), required: true },
    { key: 'prerequisites', label: 'Prerequisites', type: 'textList', required: true },
    { key: 'approvalAuthority', label: 'Approval authority', type: 'text', required: true },
    { key: 'mandatoryEvidenceChecklist', label: 'Mandatory evidence checklist', type: 'textList', required: true },
    { key: 'filingReference', label: 'Filing reference', type: 'text', required: true },
    { key: 'expiryDate', label: 'Expiry date', type: 'date' },
  ], { method: 'restrictedTendering', prerequisites: [], mandatoryEvidenceChecklist: [] }),
  'DEC-007': define('DEC-007', [
    { key: 'mode', label: 'Token fee mode', type: 'select', options: [{ value: 'free', label: 'Free' }, { value: 'paid', label: 'Paid' }], required: true },
    { key: 'feeType', label: 'Fee type', type: 'text', required: true },
    { key: 'amount', label: 'Amount', type: 'number', min: 0, step: 0.01, required: true },
    { key: 'currencyCode', label: 'Currency', type: 'select', options: currencies, required: true },
    { key: 'taxPercent', label: 'Tax percent', type: 'number', min: 0, max: 100, step: 0.01, required: true },
    { key: 'paymentChannels', label: 'Allowed payment-method codes', type: 'textList' },
    { key: 'revenueAccountId', label: 'Fee revenue account ID', type: 'text' },
    { key: 'taxAccountId', label: 'Tax liability account ID', type: 'text' },
    { key: 'exemptionWorkflowDefinitionId', label: 'Exemption workflow definition ID', type: 'text' },
    { key: 'receiptNumberFormat', label: 'Receipt number format', type: 'text', required: true },
    { key: 'exemptionRule', label: 'Exemption rule', type: 'textarea', required: true },
    { key: 'refundRule', label: 'Refund rule', type: 'textarea', required: true },
    { key: 'renewalRule', label: 'Renewal rule', type: 'textarea', required: true },
  ], { mode: 'paid', amount: 0, currencyCode: 'GHS', taxPercent: 0, paymentChannels: [] }),
  'DEC-008': define('DEC-008', [
    { key: 'documentType', label: 'Document type', type: 'text', required: true },
    { key: 'signatureMode', label: 'Signature mode', type: 'select', options: [{ value: 'electronic', label: 'Electronic' }, { value: 'uploadedManualEvidence', label: 'Uploaded manual evidence' }, { value: 'electronicOrManualEvidence', label: 'Electronic or manual evidence' }], required: true },
    { key: 'signatoryRoles', label: 'Signatory roles', type: 'textList', required: true },
    { key: 'signingOrder', label: 'Signing order', type: 'number', min: 1, required: true },
    { key: 'verificationRule', label: 'Verification rule', type: 'textarea', required: true },
    { key: 'evidenceRequirements', label: 'Evidence requirements', type: 'textList', required: true },
  ], { signatureMode: 'electronic', signingOrder: 1, signatoryRoles: [], evidenceRequirements: [] }),
  'DEC-009': define('DEC-009', [
    { key: 'profileCode', label: 'GHANEPS profile', type: 'text', required: true },
    { key: 'fileTemplateMappings', label: 'File/template mappings', type: 'textList', required: true },
    { key: 'frequency', label: 'Frequency', type: 'select', options: [{ value: 'PerEvent', label: 'Per event' }, { value: 'Daily', label: 'Daily' }, { value: 'Monthly', label: 'Monthly' }], required: true },
    { key: 'owner', label: 'Owner', type: 'text', required: true },
    { key: 'acknowledgementRule', label: 'Acknowledgement rule', type: 'textarea', required: true },
    { key: 'reconciliationRule', label: 'Reconciliation rule', type: 'textarea', required: true },
  ], { frequency: 'Monthly', fileTemplateMappings: [] }),
  'DEC-010': define('DEC-010', [
    { key: 'defaultPolicy', label: 'Negative-stock default', type: 'select', options: [{ value: 'prohibited', label: 'Prohibited' }, { value: 'controlledEmergencyOverride', label: 'Controlled emergency override' }], required: true },
    { key: 'emergencyOverrideEligible', label: 'Emergency override eligible', type: 'boolean' },
    { key: 'overridePermission', label: 'Override permission', type: 'text', required: true },
    { key: 'workflowDefinitionId', label: 'Shared workflow definition ID', type: 'text' },
    { key: 'evidenceRequirements', label: 'Evidence requirements', type: 'textList', required: true },
    { key: 'overrideDurationHours', label: 'Override duration (hours)', type: 'number', min: 1, max: 720, required: true },
    { key: 'auditRequired', label: 'Audit required', type: 'boolean' },
  ], { defaultPolicy: 'prohibited', emergencyOverrideEligible: false, evidenceRequirements: [], overrideDurationHours: 1, auditRequired: true }),
  'DEC-011': define('DEC-011', [
    { key: 'reviewFrequencyMonths', label: 'AVL review frequency (months)', type: 'number', min: 1, max: 120, required: true },
    { key: 'exposureWindowMonths', label: 'Spend exposure window (months)', type: 'number', min: 1, max: 120, required: true },
    { key: 'riskDimensions', label: 'Risk dimensions and weights', type: 'textList', required: true, placeholder: 'Delivery=25, Quality=25, Compliance=25, SpendDiversification=25' },
    { key: 'riskBands', label: 'Risk band score ranges', type: 'textList', required: true, placeholder: 'High=0-50, Medium=50-75, Low=75-100' },
    { key: 'concentrationLimitPercent', label: 'Concentration limit percent', type: 'number', min: 0, max: 100, step: 0.01, required: true },
    { key: 'minimumScore', label: 'Minimum score', type: 'number', min: 0, max: 100, step: 0.01, required: true },
    { key: 'eligibilityAction', label: 'Resulting eligibility action', type: 'select', required: true, options: [
      { value: 'alertOnly', label: 'Alert only' },
      { value: 'escalationRequired', label: 'Escalation required' },
      { value: 'awardHardStop', label: 'Award hard stop' },
    ] },
    { key: 'performanceWindowMonths', label: 'Performance window (months)', type: 'number', min: 1, max: 120, required: true },
    { key: 'performanceDimensions', label: 'Performance dimensions and weights', type: 'textList', required: true, placeholder: 'DeliveryTimeliness=15, GrnQuality=15, RejectionRate=15, PriceCompetitiveness=15, Responsiveness=10, ComplaintResolution=10, ContractCompletion=20' },
    { key: 'performanceBands', label: 'Performance band score ranges', type: 'textList', required: true, placeholder: 'Unsatisfactory=0-50, ImprovementRequired=50-75, Satisfactory=75-100' },
    { key: 'minimumPerformanceDataCoveragePercent', label: 'Minimum performance data coverage percent', type: 'number', min: 1, max: 100, step: 0.01, required: true },
    { key: 'responseTargetHours', label: 'Supplier response target (hours)', type: 'number', min: 1, max: 8760, step: 0.01, required: true },
    { key: 'performanceEligibilityAction', label: 'Performance eligibility action', type: 'select', required: true, options: [
      { value: 'alertOnly', label: 'Alert only' },
      { value: 'escalationRequired', label: 'Controlled remediation required' },
      { value: 'awardHardStop', label: 'Award hard stop' },
    ] },
  ], {
    reviewFrequencyMonths: 12,
    riskDimensions: [],
    riskBands: [],
    concentrationLimitPercent: 0,
    minimumScore: 0,
    eligibilityAction: 'alertOnly',
    performanceDimensions: [],
    performanceBands: [],
    performanceEligibilityAction: 'alertOnly',
  }),
  'DEC-012': define('DEC-012', [
    { key: 'cutoverDate', label: 'Cutover date', type: 'date', required: true },
    { key: 'dualRunPeriodDays', label: 'Dual-run period (days)', type: 'number', min: 0, max: 365, required: true },
    { key: 'dataOwner', label: 'Data owner', type: 'text', required: true },
    { key: 'acceptanceSignatories', label: 'Acceptance signatories', type: 'textList', required: true },
    { key: 'releaseStatus', label: 'Release status', type: 'select', options: [{ value: 'Draft', label: 'Draft' }, { value: 'Ready', label: 'Ready' }, { value: 'Approved', label: 'Approved' }], required: true },
    { key: 'evidenceRequirements', label: 'Evidence requirements', type: 'textList', required: true },
  ], { dualRunPeriodDays: 0, releaseStatus: 'Draft', acceptanceSignatories: [], evidenceRequirements: [] }),
  'DEC-013': define('DEC-013', [
    { key: 'documentType', label: 'Receipt document type', type: 'select', options: [{ value: 'grn', label: 'GRN' }, { value: 'mrn', label: 'MRN' }, { value: 'grnAndMrn', label: 'GRN and MRN' }], required: true },
    { key: 'applicabilityRule', label: 'Applicability rule', type: 'textarea', required: true },
    { key: 'coexistenceRule', label: 'Coexistence rule', type: 'select', options: [{ value: 'mutuallyExclusive', label: 'Mutually exclusive' }, { value: 'bothFromSingleReceipt', label: 'Both from one receipt' }, { value: 'sequentialDocuments', label: 'Sequential documents' }], required: true },
    { key: 'numberFormat', label: 'Number format', type: 'text', required: true },
    { key: 'templateReference', label: 'Template reference', type: 'text', required: true },
    { key: 'signatureRequirements', label: 'Signature requirements', type: 'textList', required: true },
    { key: 'evidenceRequirements', label: 'Evidence requirements', type: 'textList', required: true },
  ], { documentType: 'grn', coexistenceRule: 'mutuallyExclusive', signatureRequirements: [], evidenceRequirements: [] }),
  'DEC-014': define('DEC-014', [
    { key: 'workloadScenario', label: 'Workload scenario', type: 'textarea', required: true },
    { key: 'availabilityTargetPercent', label: 'Availability target percent', type: 'number', min: 0, max: 100, step: 0.01, required: true },
    { key: 'responseTargetMilliseconds', label: 'Response target (ms)', type: 'number', min: 1, required: true },
    { key: 'backupFrequencyHours', label: 'Backup frequency (hours)', type: 'number', min: 1, max: 168, required: true },
    { key: 'rpoMinutes', label: 'RPO (minutes)', type: 'number', min: 0, required: true },
    { key: 'rtoMinutes', label: 'RTO (minutes)', type: 'number', min: 0, required: true },
    { key: 'authenticationTarget', label: 'Authentication target', type: 'textarea', required: true },
    { key: 'monitoringTarget', label: 'Monitoring target', type: 'textarea', required: true },
    { key: 'usabilityTarget', label: 'Usability target', type: 'textarea', required: true },
    { key: 'accessibilityTarget', label: 'Accessibility target', type: 'textarea', required: true },
    { key: 'acceptanceMethod', label: 'Acceptance method', type: 'textarea', required: true },
  ], { availabilityTargetPercent: 99, responseTargetMilliseconds: 3000, backupFrequencyHours: 24, rpoMinutes: 60, rtoMinutes: 240 }),
};

export const procurementDecisionOwnerOptions = Array.from(new Set([
  'TDC Procurement + Legal/PPA',
  'MD + Procurement + Finance',
  'TDC Procurement + Legal/Internal Audit',
  'TDC Procurement + Finance',
  'Procurement + Finance',
  'Legal + ICT + Procurement',
  'Procurement + ICT/PPA',
  'Stores + Finance + Internal Audit',
  'Procurement + Internal Audit',
  'Steering Committee',
  'Stores + Procurement + Finance',
  'ICT + Procurement + Stores',
]));

export function createDecisionFormValue(decisionKey: string, value?: Record<string, unknown>): Record<string, unknown> {
  const definition = procurementDecisionFormRegistry[decisionKey];
  if (!definition) throw new Error(`Unknown procurement decision key ${decisionKey}`);
  return { ...definition.defaults, ...(value ?? {}) };
}

export function updateDecisionFormField(
  current: Record<string, unknown>,
  field: ProcurementDecisionFieldDefinition,
  input: string | boolean,
): Record<string, unknown> {
  let value: unknown = input;
  if (field.type === 'number') value = input === '' ? undefined : Number(input);
  if (field.type === 'textList') value = String(input).split(',').map(item => item.trim()).filter(Boolean);
  return { ...current, [field.key]: value };
}

export function displayDecisionFormField(value: unknown, type: ProcurementDecisionFieldDefinition['type']): string {
  if (type === 'textList') return Array.isArray(value) ? value.join(', ') : '';
  if (value === undefined || value === null) return '';
  return String(value);
}
