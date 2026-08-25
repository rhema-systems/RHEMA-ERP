import type {
  ProcurementPolicyRuleKind,
  ProcurementPolicyRuleValue,
} from '@/types/procurement-policy';

export type ProcurementPolicyRuleFieldType =
  'text' | 'textarea' | 'number' | 'boolean' | 'select' | 'role';

export interface ProcurementPolicyRuleField {
  key: string;
  label: string;
  type: ProcurementPolicyRuleFieldType;
  required?: boolean;
  options?: Array<{ value: string; label: string }>;
  placeholder?: string;
  min?: number;
  step?: number;
  wide?: boolean;
}

export interface ProcurementPolicyRuleDefinition {
  kind: ProcurementPolicyRuleKind;
  label: string;
  description: string;
  payloadKey:
    | 'category'
    | 'method'
    | 'threshold'
    | 'authority'
    | 'evidence'
    | 'exception'
    | 'segregationOfDuties';
  defaultDecisionKey: string;
  defaults: Record<string, unknown>;
  fields: ProcurementPolicyRuleField[];
}

export const canonicalizeProcurementPolicyOptionValue = (
  value: unknown,
  options?: Array<{ value: string; label: string }>
) => {
  const current = String(value ?? '');
  if (!current || !options?.length) return current;
  const normalized = current.replace(/[^a-z0-9]/gi, '').toLowerCase();
  return (
    options.find(
      (option) =>
        option.value.replace(/[^a-z0-9]/gi, '').toLowerCase() === normalized
    )?.value ?? current
  );
};

const categoryOptions = [
  ['Goods', 'Goods'],
  ['Works', 'Works'],
  ['TechnicalServices', 'Technical services'],
  ['ConsultancyServices', 'Consultancy services'],
  ['GeneralServices', 'General services'],
].map(([value, label]) => ({ value, label }));
const methodOptions = [
  ['RequestForQuotation', 'Request for quotation'],
  ['NationalCompetitiveTendering', 'National competitive tendering'],
  ['InternationalCompetitiveTendering', 'International competitive tendering'],
  ['RestrictedTendering', 'Restricted tendering'],
  ['SingleSource', 'Single source'],
  ['PettyPurchase', 'Petty purchase'],
  ['FrameworkCallOff', 'Framework call-off'],
  ['QualityBasedSelection', 'Quality-based selection (QBS)'],
  ['QualityAndCostBasedSelection', 'Quality and cost-based selection (QCBS)'],
].map(([value, label]) => ({ value, label }));
const evidenceStageOptions = [
  'Requisition',
  'Sourcing',
  'Evaluation',
  'Award',
  'Contract',
  'PurchaseOrder',
  'Receipt',
  'Invoice',
  'Payment',
  'Inventory',
].map((value) => ({ value, label: value.replace(/([A-Z])/g, ' $1').trim() }));

export const procurementPolicyRuleKinds: ProcurementPolicyRuleKind[] = [
  'Category',
  'Method',
  'Threshold',
  'Authority',
  'Evidence',
  'Exception',
  'SegregationOfDuties',
];

export const procurementPolicyRuleRegistry: Record<
  ProcurementPolicyRuleKind,
  ProcurementPolicyRuleDefinition
> = {
  Category: {
    kind: 'Category',
    label: 'Category',
    description:
      'Goods, works, and service classifications available to this policy.',
    payloadKey: 'category',
    defaultDecisionKey: 'DEC-001',
    defaults: {
      name: '',
      category: 'Goods',
      serviceClass: '',
      description: '',
      requiresSpecification: true,
      specificationTemplateCode: '',
    },
    fields: [
      { key: 'name', label: 'Rule name', type: 'text', required: true },
      {
        key: 'category',
        label: 'Procurement category',
        type: 'select',
        required: true,
        options: categoryOptions,
      },
      { key: 'serviceClass', label: 'Service class', type: 'text' },
      {
        key: 'specificationTemplateCode',
        label: 'Specification template',
        type: 'text',
      },
      {
        key: 'requiresSpecification',
        label: 'Specification required',
        type: 'boolean',
      },
      {
        key: 'description',
        label: 'Description',
        type: 'textarea',
        wide: true,
      },
    ],
  },
  Method: {
    kind: 'Method',
    label: 'Method',
    description: 'Allowed procurement methods and competition requirements.',
    payloadKey: 'method',
    defaultDecisionKey: 'DEC-001',
    defaults: {
      name: '',
      category: 'Goods',
      serviceClass: '',
      method: 'RequestForQuotation',
      isAllowed: true,
      requiresCompetition: true,
      justificationRequired: false,
      minimumQuotationCount: 3,
      workflowDefinitionId: '',
      applicabilityConditions: '',
    },
    fields: [
      { key: 'name', label: 'Rule name', type: 'text', required: true },
      {
        key: 'category',
        label: 'Category',
        type: 'select',
        required: true,
        options: categoryOptions,
      },
      { key: 'serviceClass', label: 'Service class', type: 'text' },
      {
        key: 'method',
        label: 'Method',
        type: 'select',
        required: true,
        options: methodOptions,
      },
      { key: 'isAllowed', label: 'Method allowed', type: 'boolean' },
      {
        key: 'requiresCompetition',
        label: 'Competition required',
        type: 'boolean',
      },
      {
        key: 'justificationRequired',
        label: 'Sourcing justification required',
        type: 'boolean',
      },
      {
        key: 'minimumQuotationCount',
        label: 'Minimum quotations',
        type: 'number',
        min: 0,
        step: 1,
      },
      {
        key: 'workflowDefinitionId',
        label: 'Published Tender Evaluation workflow',
        type: 'text',
        placeholder: 'Required for Request for Quotation',
      },
      {
        key: 'applicabilityConditions',
        label: 'Applicability conditions',
        type: 'textarea',
        wide: true,
      },
    ],
  },
  Threshold: {
    kind: 'Threshold',
    label: 'Threshold',
    description:
      'Effective-dated monetary bands that relate a category to an allowed method.',
    payloadKey: 'threshold',
    defaultDecisionKey: 'DEC-001',
    defaults: {
      name: '',
      category: 'Goods',
      serviceClass: '',
      method: 'RequestForQuotation',
      currencyCode: 'GHS',
      lowerBound: 0,
      upperBound: undefined,
      lowerInclusive: true,
      upperInclusive: true,
      statutoryReference: '',
    },
    fields: [
      { key: 'name', label: 'Rule name', type: 'text', required: true },
      {
        key: 'category',
        label: 'Category',
        type: 'select',
        required: true,
        options: categoryOptions,
      },
      { key: 'serviceClass', label: 'Service class', type: 'text' },
      {
        key: 'method',
        label: 'Method',
        type: 'select',
        required: true,
        options: methodOptions,
      },
      { key: 'currencyCode', label: 'Currency', type: 'text', required: true },
      {
        key: 'lowerBound',
        label: 'Lower bound',
        type: 'number',
        required: true,
        min: 0,
        step: 0.01,
      },
      {
        key: 'upperBound',
        label: 'Upper bound',
        type: 'number',
        min: 0,
        step: 0.01,
      },
      { key: 'lowerInclusive', label: 'Include lower bound', type: 'boolean' },
      { key: 'upperInclusive', label: 'Include upper bound', type: 'boolean' },
      {
        key: 'statutoryReference',
        label: 'Statutory reference',
        type: 'textarea',
        required: true,
        wide: true,
      },
    ],
  },
  Authority: {
    kind: 'Authority',
    label: 'Authority',
    description:
      'Approval authority bands with sequence, quorum, escalation, and shared workflow linkage.',
    payloadKey: 'authority',
    defaultDecisionKey: 'DEC-002',
    defaults: {
      authorityName: '',
      authorityRoleId: '',
      authorityRole: '',
      category: 'Goods',
      currencyCode: 'GHS',
      lowerBound: 0,
      upperBound: undefined,
      lowerInclusive: true,
      upperInclusive: true,
      sequence: 1,
      quorum: 1,
      isObserver: false,
      escalationAuthorityRoleId: '',
      escalationAuthority: '',
      workflowDefinitionId: '',
    },
    fields: [
      {
        key: 'authorityName',
        label: 'Authority name',
        type: 'text',
        required: true,
      },
      {
        key: 'authorityRoleId',
        label: 'Authority role',
        type: 'role',
        required: true,
      },
      {
        key: 'category',
        label: 'Category',
        type: 'select',
        options: categoryOptions,
      },
      { key: 'currencyCode', label: 'Currency', type: 'text', required: true },
      {
        key: 'lowerBound',
        label: 'Lower bound',
        type: 'number',
        min: 0,
        step: 0.01,
      },
      {
        key: 'upperBound',
        label: 'Upper bound',
        type: 'number',
        min: 0,
        step: 0.01,
      },
      { key: 'sequence', label: 'Sequence', type: 'number', min: 1, step: 1 },
      { key: 'quorum', label: 'Quorum', type: 'number', min: 1, step: 1 },
      { key: 'lowerInclusive', label: 'Include lower bound', type: 'boolean' },
      { key: 'upperInclusive', label: 'Include upper bound', type: 'boolean' },
      { key: 'isObserver', label: 'Observer only', type: 'boolean' },
      {
        key: 'escalationAuthorityRoleId',
        label: 'Escalation authority',
        type: 'role',
      },
      {
        key: 'workflowDefinitionId',
        label: 'Published Purchase Requisition workflow',
        type: 'text',
        required: true,
      },
    ],
  },
  Evidence: {
    kind: 'Evidence',
    label: 'Evidence',
    description:
      'Defines evidence users must provide at a procurement stage. The system generates the internal shared requirement key.',
    payloadKey: 'evidence',
    defaultDecisionKey: 'DEC-006',
    defaults: {
      evidenceName: '',
      stage: 'Requisition',
      category: 'Goods',
      method: 'RequestForQuotation',
      sharedRequirementKey: '',
      isMandatory: true,
      requiresVerification: true,
      maximumAgeDays: undefined,
    },
    fields: [
      {
        key: 'evidenceName',
        label: 'Evidence name',
        type: 'text',
        required: true,
      },
      {
        key: 'stage',
        label: 'Stage',
        type: 'select',
        required: true,
        options: evidenceStageOptions,
      },
      {
        key: 'category',
        label: 'Category',
        type: 'select',
        options: categoryOptions,
      },
      {
        key: 'method',
        label: 'Method',
        type: 'select',
        options: methodOptions,
      },
      {
        key: 'maximumAgeDays',
        label: 'Maximum age (days)',
        type: 'number',
        min: 0,
        step: 1,
      },
      { key: 'isMandatory', label: 'Mandatory', type: 'boolean' },
      {
        key: 'requiresVerification',
        label: 'Verification required',
        type: 'boolean',
      },
    ],
  },
  Exception: {
    kind: 'Exception',
    label: 'Exception',
    description:
      'Governed exception dispositions, justification, evidence, filing, and authority.',
    payloadKey: 'exception',
    defaultDecisionKey: 'DEC-006',
    defaults: {
      exceptionName: '',
      exceptionType: '',
      category: 'Goods',
      method: 'SingleSource',
      disposition: 'ApprovalRequired',
      justificationRequired: true,
      evidenceRequired: true,
      postAwardFilingRequired: true,
      approverRoleId: '',
      approverRole: '',
      workflowDefinitionId: '',
      maximumDurationDays: undefined,
    },
    fields: [
      {
        key: 'exceptionName',
        label: 'Exception name',
        type: 'text',
        required: true,
      },
      {
        key: 'exceptionType',
        label: 'Exception type',
        type: 'text',
        required: true,
      },
      {
        key: 'category',
        label: 'Category',
        type: 'select',
        options: categoryOptions,
      },
      {
        key: 'method',
        label: 'Method',
        type: 'select',
        options: methodOptions,
      },
      {
        key: 'disposition',
        label: 'Disposition',
        type: 'select',
        required: true,
        options: [
          { value: 'Prohibited', label: 'Prohibited' },
          { value: 'ApprovalRequired', label: 'Approval required' },
          { value: 'Permitted', label: 'Permitted' },
        ],
      },
      {
        key: 'approverRoleId',
        label: 'Approver role',
        type: 'role',
        required: true,
      },
      {
        key: 'maximumDurationDays',
        label: 'Maximum duration (days)',
        type: 'number',
        min: 1,
        step: 1,
      },
      {
        key: 'workflowDefinitionId',
        label: 'Published exception approval workflow',
        type: 'text',
      },
      {
        key: 'justificationRequired',
        label: 'Justification required',
        type: 'boolean',
      },
      { key: 'evidenceRequired', label: 'Evidence required', type: 'boolean' },
      {
        key: 'postAwardFilingRequired',
        label: 'Post-award filing required',
        type: 'boolean',
      },
    ],
  },
  SegregationOfDuties: {
    kind: 'SegregationOfDuties',
    label: 'Segregation of duties',
    description:
      'Declarative role conflicts for later enforcement through the shared authorization controls.',
    payloadKey: 'segregationOfDuties',
    defaultDecisionKey: 'DEC-004',
    defaults: {
      name: '',
      initiatorRoleId: '',
      initiatorRole: '',
      conflictingRoleId: '',
      conflictingRole: '',
      entityType: 'ProcurementDocument',
      action: 'Approve',
      enforcement: 'HardStop',
      explanation: '',
    },
    fields: [
      { key: 'name', label: 'Rule name', type: 'text', required: true },
      {
        key: 'initiatorRoleId',
        label: 'Initiator role',
        type: 'role',
        required: true,
      },
      {
        key: 'conflictingRoleId',
        label: 'Conflicting role',
        type: 'role',
        required: true,
      },
      { key: 'entityType', label: 'Entity type', type: 'text', required: true },
      {
        key: 'action',
        label: 'Controlled action',
        type: 'text',
        required: true,
      },
      {
        key: 'enforcement',
        label: 'Enforcement declaration',
        type: 'select',
        required: true,
        options: [
          { value: 'HardStop', label: 'Hard stop' },
          { value: 'ApprovalRequired', label: 'Approval required' },
          { value: 'Warning', label: 'Warning' },
        ],
      },
      {
        key: 'explanation',
        label: 'Explanation',
        type: 'textarea',
        wide: true,
      },
    ],
  },
};

export const createProcurementPolicyRuleValue = (
  kind: ProcurementPolicyRuleKind,
  effectiveFrom: string,
  effectiveTo?: string
): ProcurementPolicyRuleValue => {
  const definition = procurementPolicyRuleRegistry[kind];
  return {
    ruleCode: '',
    priority: 100,
    isEnabled: true,
    effectiveFrom,
    effectiveTo,
    overrideAction: 'Add',
    sourceDecisionKey: definition.defaultDecisionKey,
    ...definition.defaults,
  };
};

export const procurementDecisionKeyOptions = Array.from(
  { length: 14 },
  (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`
);
