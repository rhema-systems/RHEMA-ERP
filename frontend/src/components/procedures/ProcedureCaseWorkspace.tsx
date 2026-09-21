'use client';

import dynamic from 'next/dynamic';
import Link from 'next/link';
import React from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import {
  BookTemplate,
  CheckCircle2,
  ExternalLink,
  Eye,
  FilePenLine,
  FileUp,
  Loader2,
  Plus,
  RefreshCw,
  Save,
  Send,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import {
  CentralDocumentViewerDialog,
  type CentralDocumentViewerFile,
} from '@/components/document-management/CentralDocumentViewerDialog';
import { useToast } from '@/hooks/use-toast';
import { getStatusBadgeClassName } from '@/lib/status-badge';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { OrganizationUnitSummary } from '@/types/hr/organization';
import {
  documentManagementService,
  type CentralDocumentGenerationTemplate,
  type CentralDocumentVersionDownloadFormat,
  type GeneratedCentralDocumentResult,
} from '@/services/document-management.service';
import {
  procedureCaseService,
  type ProcedureCaseDetail,
  type ProcedureCaseDocument,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';
import { getProcedureWorkspaceTerminology } from '@/lib/procedure-workspace';

const ProcedurePdfViewer = dynamic(
  () => import('@/components/procedures/ProcedurePdfViewer'),
  {
    ssr: false,
    loading: () => (
      <div className="rounded-md border border-border bg-background p-4 text-sm text-muted-foreground">
        Loading PDF viewer...
      </div>
    ),
  }
);

interface ProcedureCaseWorkspaceProps {
  module: 'Legal' | 'Estate' | 'Facilities' | 'PropertyManagement' | 'Planning';
  entityType: string;
  defaultTitle: string;
  workspaceType?: string;
  caseId?: string;
  registerOnly?: boolean;
  caseBasePath?: string;
  detailOnly?: boolean;
}

const LAND_FEE_ENTITY_TYPES = new Set([
  'EstateLandsPartiallyServiced',
  'EstateTraditionalLands',
  'EstateTenancyRegularisation',
]);

const CHANGE_OF_USE_ENTITY_TYPES = new Set(['EstateChangeOfUse']);

const GENERATED_DOCUMENT_MODULES = new Set(['Estate', 'Legal']);

type DepartmentOption = {
  id: string;
  name: string;
  code: string;
  organizationLevelId: string;
  levelName?: string | null;
};

const LINKED_LEGAL_STAGE_EDITABLE_FIELDS: Record<string, string[]> = {
  'Legal Intake': ['assignedLegalOfficer'],
  'Agreement Vetting': [
    'dueDiligenceStatus',
    'scheduleStatus',
    'legalVettingStatus',
    'closeoutNotes',
  ],
  'Head of Legal Release': [
    'signatureStatus',
    'sealStatus',
    'dispatchStatus',
    'estateReturnStatus',
    'closeoutNotes',
  ],
};

const LEGAL_TRANSFER_STAGE_EDITABLE_FIELDS: Record<string, string[]> = {
  'Head of Legal Minuting': [
    'assignedLegalOfficer',
    'transferFeePayable',
    'closeoutNotes',
  ],
  'Client Payment Call': ['closeoutNotes'],
  'Transfer Drafting': [
    'draftDocumentReference',
    'transferDeclarationReference',
    'closeoutNotes',
  ],
  'Legal Vetting': [
    'dueDiligenceStatus',
    'cadastralPlanStatus',
    'scheduleStatus',
    'legalVettingStatus',
    'closeoutNotes',
  ],
  'Client Execution': ['interviewDate', 'signatureStatus', 'closeoutNotes'],
  'Legal Officer Signature': ['closeoutNotes'],
  'Legal Admin Signature': ['signatureStatus', 'sealStatus', 'closeoutNotes'],
  'Head of Legal Signature': ['signatureStatus', 'sealStatus', 'closeoutNotes'],
  'Legal Admin Closeout': [
    'dispatchStatus',
    'estateReturnStatus',
    'distributionStatus',
    'closeoutNotes',
  ],
};

const LEGAL_GENERIC_DOCUMENT_NAMES = new Set([
  'Source request / forwarding minute',
  'Property file extract',
  'Payment / receipt evidence',
  'Legal review note',
  'Final signed / dispatched document',
]);

const LEGAL_SPECIFIC_DOCUMENT_ENTITY_TYPES = new Set([
  'LegalCourtProcess',
  'LegalOtherCourtProcess',
  'LegalLeaseVariationRenewalSublease',
  'LegalAssignmentSubleaseVesting',
  'LegalMortgage',
  'LegalMortgageInPrinciple',
  'LegalTerminationRecognition',
  'LegalTransfer',
]);

const LEGAL_TRANSFER_DOCUMENT_STAGES: Record<string, string[]> = {
  'Transfer file from Estate': ['Head of Legal Minuting'],
  'Transfer fee payment receipt': ['Client Payment Call', 'Transfer Drafting'],
  'Draft transfer form': [
    'Transfer Drafting',
    'Legal Vetting',
    'Legal Officer Signature',
    'Legal Admin Signature',
    'Head of Legal Signature',
  ],
  'Executed transfer form': ['Client Execution', 'Legal Officer Signature'],
  'Signed transfer distribution / Estate return note': ['Legal Admin Closeout'],
};

const LEGAL_TRANSFER_GENERATION_STAGES = new Set(['Transfer Drafting']);
const PROCEDURE_GENERATION_TEMPLATE_CODES: Record<string, string[]> = {
  LegalTransfer: ['LEG-TRANSFER-FORM'],
};

const LEGAL_TRANSFER_SIGNATURE_STAGE_ROLES: Record<string, string> = {
  'Legal Officer Signature': 'Legal Officer',
  'Legal Admin Signature': 'Legal Admin Assistant',
  'Head of Legal Signature': 'Head of Legal',
};

const LEGAL_TRANSFER_FINANCE_FIELD_KEYS = new Set([
  'paymentStatus',
  'paymentReceiptReference',
  'transferFeeReceipt',
  'transferFeePaymentRequestReference',
  'transferFeeInvoiceId',
  'transferFeeInvoiceReference',
  'transferFeeInvoiceStatus',
  'transferFeeInvoiceAmount',
  'transferFeeInvoicePaidAmount',
  'transferFeeInvoiceBalance',
  'transferFeePaymentCheckStatus',
  'transferFeeCustomerNotificationStatus',
  'transferFeeCustomerNotifiedAt',
]);

const CALCULATED_PROCEDURE_FIELD_KEYS = new Set([
  'plotSizeHectares',
  'landManagementFeePayable',
  'groundRentComputed',
  'groundRentPayable',
  'changeOfUseFeePayable',
  'newGroundRentPayable',
]);

const parseAmount = (value?: string | null): number | null => {
  if (!value) {
    return null;
  }

  const parsed = Number(value.replace(/[^\d.-]/g, ''));
  return Number.isFinite(parsed) ? parsed : null;
};

function safeDownloadName(value: string) {
  return value.replace(/[\\/:*?"<>|]+/g, '-').replace(/\s+/g, ' ').trim();
}

function triggerBlobDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

type StageFieldRequirement = {
  key: string;
  label: string;
  isSatisfied: (value: string) => boolean;
  message: string;
};

const hasValue = (value: string) => value.trim().length > 0;
const isPositiveAmount = (value: string) => {
  const parsed = parseAmount(value);
  return parsed !== null && parsed > 0;
};
const isOneOf =
  (...allowed: string[]) =>
  (value: string) =>
    allowed.some((item) => item.toLowerCase() === value.trim().toLowerCase());

const LEGAL_TRANSFER_STAGE_REQUIRED_FIELDS: Record<
  string,
  StageFieldRequirement[]
> = {
  'Head of Legal Minuting': [
    {
      key: 'assignedLegalOfficer',
      label: 'Assigned Legal Officer',
      isSatisfied: hasValue,
      message: 'Assign the Legal Officer before submitting.',
    },
    {
      key: 'transferFeePayable',
      label: 'Transfer fee payable',
      isSatisfied: isPositiveAmount,
      message: 'Enter a transfer fee payable greater than zero.',
    },
  ],
  'Transfer Drafting': [
    {
      key: 'draftDocumentReference',
      label: 'Draft document reference',
      isSatisfied: hasValue,
      message: 'Generate the transfer draft before submitting.',
    },
  ],
  'Legal Vetting': [
    {
      key: 'dueDiligenceStatus',
      label: 'Due diligence status',
      isSatisfied: isOneOf('Cleared'),
      message: 'Set due diligence status to Cleared.',
    },
    {
      key: 'cadastralPlanStatus',
      label: 'Cadastral plan status',
      isSatisfied: isOneOf('Available', 'Not required'),
      message: 'Set cadastral plan status to Available or Not required.',
    },
    {
      key: 'scheduleStatus',
      label: 'Schedule insertion status',
      isSatisfied: isOneOf('Inserted', 'Not required'),
      message: 'Set schedule insertion status to Inserted or Not required.',
    },
    {
      key: 'legalVettingStatus',
      label: 'Legal vetting status',
      isSatisfied: isOneOf('Approved'),
      message: 'Set legal vetting status to Approved.',
    },
  ],
  'Client Execution': [
    {
      key: 'interviewDate',
      label: 'Applicant / transferee interview date',
      isSatisfied: hasValue,
      message: 'Enter the applicant / transferee interview date.',
    },
    {
      key: 'signatureStatus',
      label: 'Signature status',
      isSatisfied: isOneOf('Client signed', 'Fully signed'),
      message: 'Set signature status to Client signed.',
    },
  ],
  'Legal Officer Signature': [
    {
      key: 'signatureStatus',
      label: 'Signature status',
      isSatisfied: isOneOf('Legal signed', 'Fully signed'),
      message: 'Sign the executed transfer form before submitting.',
    },
  ],
  'Legal Admin Signature': [
    {
      key: 'sealStatus',
      label: 'Seal / dating status',
      isSatisfied: isOneOf('Sealed', 'Dated', 'Sealed and dated'),
      message: 'Set seal / dating status before submitting.',
    },
  ],
  'Head of Legal Signature': [
    {
      key: 'signatureStatus',
      label: 'Signature status',
      isSatisfied: isOneOf('Head of Legal signed', 'Fully signed'),
      message: 'Set signature status to Head of Legal signed.',
    },
  ],
  'Legal Admin Closeout': [
    {
      key: 'distributionStatus',
      label: 'Signed transfer distribution status',
      isSatisfied: isOneOf('Distributed', 'Returned to Estate Records'),
      message: 'Set signed transfer distribution status.',
    },
    {
      key: 'estateReturnStatus',
      label: 'Estate file return status',
      isSatisfied: isOneOf('Returned to Estate'),
      message: 'Set Estate file return status to Returned to Estate.',
    },
  ],
};

const formatCalculatedAmount = (value: number | null, decimals = 2): string => {
  if (value === null || !Number.isFinite(value)) {
    return '';
  }

  return value.toFixed(decimals);
};

const formatGhsAmount = (value?: string | null): string => {
  const parsed = parseAmount(value);
  if (parsed === null) {
    return value?.trim() || 'Not set';
  }

  return `GHS ${parsed.toLocaleString('en-GH', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
};

const getFinanceStatusBadgeVariant = (
  status?: string | null
): 'default' | 'secondary' | 'destructive' | 'outline' => {
  const normalized = status?.trim().toLowerCase();
  if (normalized === 'paid') {
    return 'default';
  }

  if (
    normalized === 'rejected' ||
    normalized === 'cancelled' ||
    normalized === 'overdue'
  ) {
    return 'destructive';
  }

  if (
    normalized === 'sent' ||
    normalized === 'approved' ||
    normalized === 'partiallypaid'
  ) {
    return 'secondary';
  }

  return 'outline';
};

const formatFinanceStatus = (
  status?: string | null,
  fallback = 'Not generated'
): string => status?.trim().replace(/([a-z])([A-Z])/g, '$1 $2') || fallback;

const isDepartmentLevel = (name?: string | null, code?: string | null) => {
  const normalized = `${name ?? ''} ${code ?? ''}`.trim().toLowerCase();
  return normalized.includes('department') || normalized.includes('dept');
};

const isLegalTransferStageSignatureRecorded = (
  procedureCase: ProcedureCaseDetail,
  document: ProcedureCaseDocument
): boolean => {
  const fileName = document.fileName?.toLowerCase() ?? '';
  const notes = document.notes?.toLowerCase() ?? '';

  switch (procedureCase.currentStageName) {
    case 'Legal Officer Signature':
      return (
        fileName.includes('legal-officer-signed') ||
        notes.includes('legal officer signed from legal transfer workspace') ||
        notes.includes('legal officer digitally signed')
      );
    case 'Legal Admin Signature':
      return (
        fileName.includes('legal-admin-assistant-signed') ||
        notes.includes(
          'legal admin assistant signed from legal transfer workspace'
        ) ||
        notes.includes('legal admin assistant digitally signed')
      );
    case 'Head of Legal Signature':
      return (
        fileName.includes('head-of-legal-signed') ||
        notes.includes('head of legal signed from legal transfer workspace') ||
        notes.includes('head of legal digitally signed')
      );
    default:
      return false;
  }
};

export function ProcedureCaseWorkspace({
  module,
  entityType,
  defaultTitle,
  workspaceType,
  caseId,
  registerOnly = false,
  caseBasePath,
  detailOnly = false,
}: ProcedureCaseWorkspaceProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const terminology = getProcedureWorkspaceTerminology(workspaceType);
  const requestedCaseId = caseId ?? searchParams.get('caseId');
  const prefillSignature = searchParams.toString();
  const prefilledCase = React.useMemo(
    () => ({
      title: searchParams.get('title') || defaultTitle,
      referenceNumber: searchParams.get('referenceNumber') || '',
      applicantName: searchParams.get('applicantName') || '',
      sourceDepartment: searchParams.get('sourceDepartment') || '',
      receivedDate: searchParams.get('receivedDate') || '',
      description: searchParams.get('description') || '',
    }),
    [defaultTitle, prefillSignature, searchParams]
  );
  const prefilledFieldValues = React.useMemo(() => {
    const values: Record<string, string | null> = {};
    searchParams.forEach((value, key) => {
      if (key.startsWith('field_')) {
        values[key.slice('field_'.length)] = value;
      }
    });
    return values;
  }, [prefillSignature, searchParams]);
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [casePage, setCasePage] = React.useState(1);
  const [casePageSize, setCasePageSize] = React.useState(10);
  const [caseTotalCount, setCaseTotalCount] = React.useState(0);
  const [caseTotalPages, setCaseTotalPages] = React.useState(1);
  const [selectedCase, setSelectedCase] =
    React.useState<ProcedureCaseDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [documentFiles, setDocumentFiles] = React.useState<
    Record<string, File | null>
  >({});
  const [previewDocumentId, setPreviewDocumentId] = React.useState<
    string | null
  >(null);
  const [dmsViewerDocumentId, setDmsViewerDocumentId] = React.useState<
    string | null
  >(null);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = React.useState(false);
  const [departmentOptions, setDepartmentOptions] = React.useState<
    DepartmentOption[]
  >([]);
  const [isLoadingDepartments, setIsLoadingDepartments] =
    React.useState(false);
  const [generationTemplates, setGenerationTemplates] = React.useState<
    CentralDocumentGenerationTemplate[]
  >([]);
  const [selectedGenerationTemplate, setSelectedGenerationTemplate] =
    React.useState<string>('');
  const [generatedDocument, setGeneratedDocument] =
    React.useState<GeneratedCentralDocumentResult | null>(null);
  const [isGeneratingDocument, setIsGeneratingDocument] = React.useState(false);
  const [isGeneratedViewerOpen, setIsGeneratedViewerOpen] =
    React.useState(false);
  const [signingDocumentId, setSigningDocumentId] = React.useState<
    string | null
  >(null);
  const [newCase, setNewCase] = React.useState({
    title: prefilledCase.title,
    referenceNumber: prefilledCase.referenceNumber,
    applicantName: prefilledCase.applicantName,
    sourceDepartment: prefilledCase.sourceDepartment,
    organizationLevelId: searchParams.get('organizationLevelId') || '',
    organizationUnitId: searchParams.get('organizationUnitId') || '',
    receivedDate: prefilledCase.receivedDate,
    description: prefilledCase.description,
  });
  const { toast } = useToast();
  const appliedPrefillSignatureRef = React.useRef('');
  const legalTransferCanGenerateDocument =
    entityType !== 'LegalTransfer' ||
    Boolean(
      selectedCase &&
        LEGAL_TRANSFER_GENERATION_STAGES.has(selectedCase.currentStageName)
    );
  const supportsGeneratedDocuments =
    GENERATED_DOCUMENT_MODULES.has(module) &&
    !(module === 'Legal' && entityType === 'LegalPropertyAgreementReview') &&
    !(module === 'Legal' && !legalTransferCanGenerateDocument);
  const allowsManualCaseCreation = !(
    module === 'Legal' && entityType === 'LegalPropertyAgreementReview'
  );
  const generatedDocumentSourceLabel =
    module === 'Legal'
      ? 'Source: Legal Department -> Central DMS'
      : 'Source: Estate / Facility -> Central DMS';
  const generatedDocumentPreparedBy =
    selectedCase?.sourceDepartment ||
    (module === 'Legal' ? 'Legal Department' : 'Estate Section');
  const originatingPropertyCaseId = selectedCase?.fields.find(
    (field) => field.key === 'sourceProcedureCaseId'
  )?.value;
  const isLinkedLegalMatter =
    module === 'Legal' && Boolean(originatingPropertyCaseId);
  const selectedNewCaseDepartment = departmentOptions.find(
    (department) => department.id === newCase.organizationUnitId
  );
  const selectedCaseDepartmentValue =
    selectedCase?.organizationUnitId ||
    departmentOptions.find(
      (department) =>
        department.name.trim().toLowerCase() ===
        (selectedCase?.sourceDepartment ?? '').trim().toLowerCase()
    )?.id ||
    '';
  const legalTransferFinanceSnapshot = React.useMemo(() => {
    if (!selectedCase || entityType !== 'LegalTransfer') {
      return null;
    }

    const fieldValue = (key: string) =>
      selectedCase.fields.find((field) => field.key === key)?.value?.trim() ??
      '';

    const snapshot = {
      transferFeePayable: fieldValue('transferFeePayable'),
      paymentRequestReference: fieldValue('transferFeePaymentRequestReference'),
      invoiceId: fieldValue('transferFeeInvoiceId'),
      invoiceReference: fieldValue('transferFeeInvoiceReference'),
      invoiceStatus: fieldValue('transferFeeInvoiceStatus'),
      invoiceAmount: fieldValue('transferFeeInvoiceAmount'),
      invoicePaidAmount: fieldValue('transferFeeInvoicePaidAmount'),
      invoiceBalance: fieldValue('transferFeeInvoiceBalance'),
      paymentStatus: fieldValue('paymentStatus'),
      receiptReference:
        fieldValue('paymentReceiptReference') ||
        fieldValue('transferFeeReceipt'),
      paymentCheckStatus: fieldValue('transferFeePaymentCheckStatus'),
      customerNotificationStatus: fieldValue(
        'transferFeeCustomerNotificationStatus'
      ),
    };

    const hasFinanceData =
      Boolean(snapshot.transferFeePayable) ||
      Boolean(snapshot.paymentRequestReference) ||
      Boolean(snapshot.invoiceReference) ||
      Boolean(snapshot.paymentStatus) ||
      Boolean(snapshot.receiptReference) ||
      Boolean(snapshot.paymentCheckStatus);

    return hasFinanceData ? snapshot : null;
  }, [entityType, selectedCase]);
  const linkedLegalEditableFields = React.useMemo(() => {
    const currentStageName = selectedCase?.currentStageName ?? '';
    const editableFields =
      entityType === 'LegalTransfer'
        ? LEGAL_TRANSFER_STAGE_EDITABLE_FIELDS[currentStageName]
        : LINKED_LEGAL_STAGE_EDITABLE_FIELDS[currentStageName];

    return new Set(editableFields ?? []);
  }, [entityType, selectedCase?.currentStageName]);

  const currentStageItems = React.useMemo(
    () =>
      selectedCase?.checklistItems.filter(
        (item) => item.stageIndex === selectedCase.currentStageIndex
      ) ?? [],
    [selectedCase]
  );
  const visibleDocuments = React.useMemo(() => {
    if (!selectedCase) {
      return [];
    }

    if (
      module === 'Legal' &&
      LEGAL_SPECIFIC_DOCUMENT_ENTITY_TYPES.has(entityType)
    ) {
      return selectedCase.documents.filter((document) => {
        if (LEGAL_GENERIC_DOCUMENT_NAMES.has(document.name)) {
          return false;
        }

        if (entityType !== 'LegalTransfer') {
          return true;
        }

        const documentStages = LEGAL_TRANSFER_DOCUMENT_STAGES[document.name];
        return (
          Boolean(document.fileName) ||
          !documentStages ||
          documentStages.includes(selectedCase.currentStageName)
        );
      });
    }

    return selectedCase.documents;
  }, [entityType, module, selectedCase]);
  const isLegalTransferClientPaymentStage =
    entityType === 'LegalTransfer' &&
    selectedCase?.currentStageName === 'Client Payment Call';
  const legalTransferPaymentReady = React.useMemo(() => {
    if (!selectedCase || !isLegalTransferClientPaymentStage) {
      return true;
    }

    const fieldValue = (key: string) =>
      selectedCase.fields.find((field) => field.key === key)?.value?.trim() ??
      '';
    const paymentStatus = fieldValue('paymentStatus').toLowerCase();
    const hasReceiptReference = Boolean(
      fieldValue('paymentReceiptReference') || fieldValue('transferFeeReceipt')
    );
    const invoiceAmount = parseAmount(fieldValue('transferFeeInvoiceAmount'));
    const invoicePaidAmount = parseAmount(
      fieldValue('transferFeeInvoicePaidAmount')
    );
    const invoiceBalance = parseAmount(fieldValue('transferFeeInvoiceBalance'));
    const invoiceIsSettled =
      invoiceAmount !== null &&
      invoicePaidAmount !== null &&
      invoiceBalance !== null &&
      invoicePaidAmount + 0.01 >= invoiceAmount &&
      Math.abs(invoiceBalance) < 0.01;

    return (
      paymentStatus === 'paid' && (hasReceiptReference || invoiceIsSettled)
    );
  }, [isLegalTransferClientPaymentStage, selectedCase]);
  const legalTransferRequiredFieldMessages = React.useMemo(() => {
    if (!selectedCase || entityType !== 'LegalTransfer') {
      return [];
    }

    const requiredFields =
      LEGAL_TRANSFER_STAGE_REQUIRED_FIELDS[selectedCase.currentStageName] ?? [];
    if (requiredFields.length === 0) {
      return [];
    }

    const fieldValues = new Map(
      selectedCase.fields.map((field) => [field.key, field.value ?? ''])
    );
    return requiredFields
      .filter(
        (requirement) =>
          !requirement.isSatisfied(fieldValues.get(requirement.key) ?? '')
      )
      .map((requirement) => requirement.message);
  }, [entityType, selectedCase]);
  const legalTransferRequiredFieldKeys = React.useMemo(() => {
    if (!selectedCase || entityType !== 'LegalTransfer') {
      return new Set<string>();
    }

    return new Set(
      (
        LEGAL_TRANSFER_STAGE_REQUIRED_FIELDS[selectedCase.currentStageName] ??
        []
      ).map((requirement) => requirement.key)
    );
  }, [entityType, selectedCase]);
  const facilitiesMaintenanceCloseoutBlocker = React.useMemo(() => {
    if (
      !selectedCase ||
      module !== 'Facilities' ||
      entityType !== 'EstateFacilityMaintenance' ||
      selectedCase.currentStageName !== 'Maintenance Closeout'
    ) {
      return null;
    }

    const fieldValue = (key: string) =>
      selectedCase.fields.find((field) => field.key === key)?.value?.trim() ??
      '';
    const jobCardReference = fieldValue('maintenanceJobCardReference');
    const executionComplete = fieldValue('maintenanceExecutionComplete');
    const executionStatus = fieldValue('maintenanceExecutionStatus');

    if (!jobCardReference) {
      return 'Maintenance closeout cannot be submitted until the Maintenance job card is linked.';
    }

    if (executionComplete !== 'true') {
      return executionStatus
        ? `Maintenance closeout cannot be submitted yet: ${executionStatus}.`
        : `Maintenance closeout cannot be submitted until job card ${jobCardReference} is completed.`;
    }

    return null;
  }, [entityType, module, selectedCase]);
  const currentStageEditableFieldKeys = React.useMemo(
    () => new Set(selectedCase?.currentStageFieldKeys ?? []),
    [selectedCase?.currentStageFieldKeys]
  );
  const shouldLockToCurrentStageFields =
    Boolean(selectedCase?.usesConfiguredWorkflow) &&
    currentStageEditableFieldKeys.size > 0;
  const canEditProcedureField = React.useCallback(
    (key: string) =>
      Boolean(selectedCase?.canEditCurrentStage) &&
      (!shouldLockToCurrentStageFields ||
        currentStageEditableFieldKeys.has(key)),
    [
      currentStageEditableFieldKeys,
      selectedCase?.canEditCurrentStage,
      shouldLockToCurrentStageFields,
    ]
  );
  const isStageSubmitDisabled =
    !selectedCase?.canEditCurrentStage ||
    isSaving ||
    Boolean(signingDocumentId) ||
    currentStageItems.some((item) => !item.isCompleted) ||
    legalTransferRequiredFieldMessages.length > 0 ||
    !legalTransferPaymentReady ||
    Boolean(facilitiesMaintenanceCloseoutBlocker);

  const getDocumentManagementRecordId = (fileUrl?: string | null) => {
    const match = fileUrl?.match(/^\/document-management\/records\/([^/?#]+)/i);
    return match ? decodeURIComponent(match[1]) : null;
  };

  const getDocumentPreviewUrl = (document: ProcedureCaseDocument) => {
    const dmsRecordId = getDocumentManagementRecordId(document.fileUrl);
    return dmsRecordId
      ? `/api/document-management/records/${encodeURIComponent(dmsRecordId)}/content`
      : document.fileUrl;
  };

  const isDocumentManagementDocument = (document: ProcedureCaseDocument) =>
    Boolean(getDocumentManagementRecordId(document.fileUrl));

  const isPdfDocument = (document: ProcedureCaseDocument) => {
    if (getDocumentManagementRecordId(document.fileUrl)) {
      return true;
    }

    const value =
      `${document.fileName ?? ''} ${document.fileUrl ?? ''}`.toLowerCase();
    return value.includes('.pdf');
  };
  const previewDocument =
    selectedCase?.documents.find(
      (document) => document.id === previewDocumentId
    ) ?? null;
  const dmsViewerDocument =
    selectedCase?.documents.find(
      (document) => document.id === dmsViewerDocumentId
    ) ?? null;
  const previewDocumentUrl = previewDocument
    ? getDocumentPreviewUrl(previewDocument)
    : null;

  const toDmsViewerFile = (
    document: ProcedureCaseDocument | null | undefined
  ): CentralDocumentViewerFile | null => {
    if (!document?.centralDocumentRecordId) {
      return null;
    }

    const versionId = document.centralDocumentVersionId;
    return {
      documentRecordId: document.centralDocumentRecordId,
      versionId,
      title: document.name,
      fileName:
        document.fileName ||
        document.centralDocumentVersion ||
        document.name,
      repositoryPath:
        document.centralDocumentRepositoryPath ||
        (versionId
          ? `/api/document-management/records/${encodeURIComponent(
              document.centralDocumentRecordId
            )}/versions/${encodeURIComponent(versionId)}/content`
          : undefined),
      renditionPath: document.centralDocumentRenditionPath,
      contentType: document.centralDocumentContentType,
      sourceLabel: document.providedBy || document.requiredFrom || undefined,
      version: document.centralDocumentVersion,
      annotationStateJson: document.centralDocumentAnnotationStateJson,
    };
  };

  const refreshSelectedCase = async () => {
    if (!selectedCase) {
      return null;
    }

    const refreshed = await procedureCaseService.getCase(selectedCase.id);
    setSelectedCase(refreshed);
    await loadCases();
    return refreshed;
  };

  const loadCases = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      if (detailOnly && requestedCaseId) {
        const detail =
          selectedCase?.id === requestedCaseId
            ? selectedCase
            : await procedureCaseService.getCase(requestedCaseId);
        setCases([]);
        setCaseTotalCount(0);
        setCaseTotalPages(1);
        setSelectedCase(detail);
        return;
      }

      const data = await procedureCaseService.listCasesPage(
        module,
        entityType,
        casePage,
        casePageSize
      );
      setCases(data.items);
      setCaseTotalCount(data.totalCount);
      setCaseTotalPages(Math.max(1, data.totalPages || 1));

      // Notifications and handoff links pass caseId so reviewers land on the exact Estate procedure case.
      const targetCaseId = requestedCaseId || (registerOnly ? null : data.items[0]?.id);

      if (targetCaseId && selectedCase?.id !== targetCaseId) {
        const detail = await procedureCaseService.getCase(targetCaseId);
        setSelectedCase(detail);
      } else if (!targetCaseId) {
        setSelectedCase(null);
      }
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to load workspace records.'
      );
    } finally {
      setIsLoading(false);
    }
  }, [
    casePage,
    casePageSize,
    detailOnly,
    entityType,
    module,
    registerOnly,
    requestedCaseId,
    selectedCase,
    selectedCase?.id,
  ]);

  React.useEffect(() => {
    void loadCases();
  }, [loadCases]);

  React.useEffect(() => {
    let mounted = true;

    const loadDepartments = async () => {
      setIsLoadingDepartments(true);
      try {
        const [levels, units] = await Promise.all([
          organizationLevelService.getAll(),
          organizationUnitService.getSummary(),
        ]);
        if (!mounted) {
          return;
        }

        const departmentLevelIds = new Set(
          levels
            .filter((level) => level.isActive && isDepartmentLevel(level.name, level.code))
            .map((level) => level.id)
        );
        const departmentUnits = units
          .filter((unit: OrganizationUnitSummary) =>
            unit.isActive && departmentLevelIds.has(unit.organizationLevelId)
          )
          .sort((left, right) => left.name.localeCompare(right.name))
          .map((unit) => ({
            id: unit.id,
            name: unit.name,
            code: unit.code,
            organizationLevelId: unit.organizationLevelId,
            levelName: unit.levelName,
          }));
        setDepartmentOptions(departmentUnits);
      } catch {
        if (mounted) {
          setDepartmentOptions([]);
        }
      } finally {
        if (mounted) {
          setIsLoadingDepartments(false);
        }
      }
    };

    void loadDepartments();

    return () => {
      mounted = false;
    };
  }, []);

  React.useEffect(() => {
    if (
      requestedCaseId ||
      appliedPrefillSignatureRef.current === prefillSignature
    ) {
      return;
    }

    const hasPrefill =
      Boolean(prefilledCase.referenceNumber) ||
      Boolean(prefilledCase.applicantName) ||
      Boolean(prefilledCase.sourceDepartment) ||
      Boolean(prefilledCase.receivedDate) ||
      Boolean(prefilledCase.description) ||
      Boolean(searchParams.get('organizationUnitId')) ||
      Object.keys(prefilledFieldValues).length > 0;
    if (!hasPrefill) {
      return;
    }

    appliedPrefillSignatureRef.current = prefillSignature;
    setNewCase((current) => ({
      ...current,
      ...prefilledCase,
      organizationLevelId: searchParams.get('organizationLevelId') || '',
      organizationUnitId: searchParams.get('organizationUnitId') || '',
    }));
  }, [
    prefillSignature,
    prefilledCase,
    prefilledFieldValues,
    requestedCaseId,
    searchParams,
  ]);

  React.useEffect(() => {
    if (!supportsGeneratedDocuments) {
      return;
    }

    let mounted = true;
    const loadTemplates = async () => {
      try {
        const templates =
          await documentManagementService.getGenerationTemplates(module);
        if (!mounted) {
          return;
        }

        const allowedTemplateCodes =
          PROCEDURE_GENERATION_TEMPLATE_CODES[entityType];
        const scopedTemplates = allowedTemplateCodes?.length
          ? templates.filter((template) =>
              allowedTemplateCodes.includes(template.templateCode)
            )
          : templates;

        setGenerationTemplates(scopedTemplates);
        setSelectedGenerationTemplate((current) =>
          current &&
          scopedTemplates.some((template) => template.templateCode === current)
            ? current
            : scopedTemplates[0]?.templateCode || ''
        );
      } catch (err) {
        if (mounted) {
          setError(
            err instanceof Error
              ? err.message
              : `Unable to load ${module} document templates.`
          );
        }
      }
    };

    void loadTemplates();

    return () => {
      mounted = false;
    };
  }, [entityType, module, supportsGeneratedDocuments]);

  React.useEffect(() => {
    if (
      !selectedCase ||
      (!LAND_FEE_ENTITY_TYPES.has(entityType) &&
        !CHANGE_OF_USE_ENTITY_TYPES.has(entityType))
    ) {
      return;
    }

    setSelectedCase((current) => {
      if (!current || current.id !== selectedCase.id) {
        return current;
      }

      const values = new Map(
        current.fields.map((field) => [field.key, field.value ?? ''])
      );
      const plotSizeAcres = parseAmount(values.get('plotSizeAcres'));
      const lmfRatePerAcre = parseAmount(values.get('lmfRatePerAcre'));
      const groundRentRatePerAcre = parseAmount(
        values.get('groundRentRatePerAcre')
      );
      const existingLmfRatePerAcre = parseAmount(
        values.get('existingLmfRatePerAcre')
      );
      const newLmfRatePerAcre = parseAmount(values.get('newLmfRatePerAcre'));
      const newGroundRentRatePerAcre = parseAmount(
        values.get('newGroundRentRatePerAcre')
      );

      const calculatedValues = new Map<string, string>();
      const plotSizeHectares =
        plotSizeAcres === null ? null : plotSizeAcres * 0.40468564224;
      calculatedValues.set(
        'plotSizeHectares',
        formatCalculatedAmount(plotSizeHectares, 4)
      );

      // Estate manuals require these calculations before proposal letters are generated and sent for payment.
      const landManagementFee =
        plotSizeAcres !== null && lmfRatePerAcre !== null
          ? plotSizeAcres * lmfRatePerAcre
          : null;
      calculatedValues.set(
        'landManagementFeePayable',
        formatCalculatedAmount(landManagementFee)
      );

      const groundRentComputed =
        plotSizeAcres !== null && groundRentRatePerAcre !== null
          ? plotSizeAcres * groundRentRatePerAcre
          : null;
      calculatedValues.set(
        'groundRentComputed',
        formatCalculatedAmount(groundRentComputed, 3)
      );
      calculatedValues.set(
        'groundRentPayable',
        formatCalculatedAmount(
          groundRentComputed === null ? null : Math.ceil(groundRentComputed)
        )
      );

      const changeOfUseFee =
        plotSizeAcres !== null &&
        existingLmfRatePerAcre !== null &&
        newLmfRatePerAcre !== null
          ? Math.max(
              (newLmfRatePerAcre - existingLmfRatePerAcre) * plotSizeAcres,
              0
            )
          : null;
      calculatedValues.set(
        'changeOfUseFeePayable',
        formatCalculatedAmount(changeOfUseFee)
      );

      const newGroundRent =
        plotSizeAcres !== null && newGroundRentRatePerAcre !== null
          ? plotSizeAcres * newGroundRentRatePerAcre
          : null;
      calculatedValues.set(
        'newGroundRentPayable',
        formatCalculatedAmount(
          newGroundRent === null ? null : Math.ceil(newGroundRent)
        )
      );

      let changed = false;
      const fields = current.fields.map((field) => {
        if (!calculatedValues.has(field.key)) {
          return field;
        }

        const value = calculatedValues.get(field.key) ?? '';
        if ((field.value ?? '') === value) {
          return field;
        }

        changed = true;
        return { ...field, value };
      });

      return changed ? { ...current, fields } : current;
    });
  }, [entityType, selectedCase]);

  const selectCase = async (id: string) => {
    setIsSaving(true);
    setError(null);
    try {
      const detail = await procedureCaseService.getCase(id);
      setSelectedCase(detail);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Unable to open the workspace record.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const createCase = async () => {
    if (!selectedNewCaseDepartment) {
      setError('Select the source department from HR organization units.');
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const created = await procedureCaseService.createCase({
        module,
        entityType,
        title: newCase.title,
        applicantName: newCase.applicantName,
        sourceDepartment: selectedNewCaseDepartment.name,
        organizationLevelId: selectedNewCaseDepartment.organizationLevelId,
        organizationUnitId: selectedNewCaseDepartment.id,
        receivedDate: newCase.receivedDate,
        description: newCase.description,
        fieldValues: prefilledFieldValues,
      });
      setSelectedCase(created);
      setNewCase({
        title: defaultTitle,
        referenceNumber: '',
        applicantName: '',
        sourceDepartment: '',
        organizationLevelId: '',
        organizationUnitId: '',
        receivedDate: '',
        description: '',
      });
      await loadCases();
      setIsCreateDialogOpen(false);
      if (registerOnly || detailOnly) {
        router.push(caseDetailHref(created.id));
      }
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Unable to create the workspace record.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const updateFieldValue = (key: string, value: string) => {
    setSelectedCase((current) => {
      if (!current) {
        return current;
      }

      return {
        ...current,
        fields: current.fields.map((field) =>
          field.key === key ? { ...field, value } : field
        ),
      };
    });
  };

  const saveIntake = async () => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const fieldValues = Object.fromEntries(
        selectedCase.fields.map((field) => [field.key, field.value ?? null])
      );
      const updated = await procedureCaseService.updateFields(selectedCase.id, {
        fieldValues,
        referenceNumber: selectedCase.referenceNumber,
        applicantName: selectedCase.applicantName,
        sourceDepartment: selectedCase.sourceDepartment,
        organizationLevelId: selectedCase.organizationLevelId,
        organizationUnitId: selectedCase.organizationUnitId,
        receivedDate: selectedCase.receivedDate,
        description: selectedCase.description,
      });
      setSelectedCase(updated);
      await loadCases();
      toast({ title: 'Stage updates saved', variant: 'success' });
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to save intake fields.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const toggleChecklist = async (
    checklistItemId: string,
    isCompleted: boolean
  ) => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.updateChecklistItem(
        selectedCase.id,
        checklistItemId,
        isCompleted
      );
      setSelectedCase(updated);
      await loadCases();
      toast({ title: 'Checklist updated', variant: 'success' });
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to update checklist.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const saveDocument = async (document: ProcedureCaseDocument) => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const selectedFile = documentFiles[document.id];
      if (selectedFile && document.centralDocumentRecordId) {
        await documentManagementService.uploadVersionFile(
          document.centralDocumentRecordId,
          {
            file: selectedFile,
            status: 'Current',
            changeSummary:
              'Edited workflow copy uploaded from the procedure case workspace.',
          }
        );
        setDocumentFiles((current) => ({ ...current, [document.id]: null }));
        await refreshSelectedCase();
        toast({
          title: 'Document version uploaded',
          description: 'The edited copy is now the current DMS version.',
          variant: 'success',
        });
        return;
      }

      const updated = selectedFile
        ? await procedureCaseService.uploadDocument(
            selectedCase.id,
            document.id,
            selectedFile,
            document.notes
          )
        : await procedureCaseService.attachDocument(
            selectedCase.id,
            document.id,
            {
              fileName: document.fileName,
              fileUrl: document.fileUrl,
              notes: document.notes,
            }
          );

      setSelectedCase(updated);
      setDocumentFiles((current) => ({ ...current, [document.id]: null }));
      await loadCases();
      toast({
        title: selectedFile ? 'Document uploaded' : 'Document notes saved',
        description: selectedFile
          ? 'The document is now attached to this case.'
          : undefined,
        variant: 'success',
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to save document.');
    } finally {
      setIsSaving(false);
    }
  };

  const generateDmsRendition = async (file: CentralDocumentViewerFile) => {
    if (!file.documentRecordId || !file.versionId) {
      return null;
    }

    const version = await documentManagementService.generateVersionRendition(
      file.documentRecordId,
      file.versionId,
      Boolean(file.renditionPath)
    );
    await refreshSelectedCase();

    return {
      ...file,
      renditionPath: version.renditionPath,
      repositoryPath: version.repositoryPath || file.repositoryPath,
      contentType: version.contentType || file.contentType,
      fileName: version.fileName || file.fileName,
    };
  };

  const downloadDmsVersion = async (
    file: CentralDocumentViewerFile,
    format: CentralDocumentVersionDownloadFormat
  ) => {
    if (!file.documentRecordId || !file.versionId) {
      throw new Error('This DMS version cannot be downloaded.');
    }

    const blob = await documentManagementService.downloadVersionFile(
      file.documentRecordId,
      file.versionId,
      format
    );
    const extension = format === 'pdf' ? 'pdf' : 'docx';
    const baseName =
      file.fileName?.replace(/\.[^.]+$/, '') || file.title || 'document';

    triggerBlobDownload(
      blob,
      safeDownloadName(`${baseName}-${file.version || 'version'}.${extension}`)
    );
  };

  const saveDmsAnnotations = async (
    file: CentralDocumentViewerFile,
    annotationStateJson: string | null,
    annotatedPdfBlob: Blob | null
  ) => {
    if (!file.documentRecordId || !file.versionId) {
      throw new Error('This DMS version cannot save annotations.');
    }

    const sourcePdf =
      annotatedPdfBlob ||
      (await documentManagementService.downloadVersionFile(
        file.documentRecordId,
        file.versionId,
        'pdf'
      ));
    const baseName =
      file.fileName?.replace(/\.[^.]+$/, '') || file.title || 'document';
    const annotatedFile = new File(
      [sourcePdf],
      safeDownloadName(`${baseName}-annotated.pdf`),
      { type: 'application/pdf' }
    );
    const version = await documentManagementService.uploadVersionFile(
      file.documentRecordId,
      {
        file: annotatedFile,
        status: 'Current',
        changeSummary:
          'PDF annotations, comments, and signatures saved from the procedure case workspace.',
      }
    );
    await documentManagementService.addAnnotationReview(file.documentRecordId, {
      documentVersionId: version.id,
      reviewTitle: `${file.title} annotation save`,
      status: 'Open',
      syncfusionAnnotationStatus: 'Annotations saved',
      reviewNotes:
        'Annotations, comments, and signature marks were saved from the case document viewer.',
      annotationStateJson: annotationStateJson || '{}',
    });
    await refreshSelectedCase();

    return {
      ...file,
      versionId: version.id,
      fileUploadRecordId: version.fileUploadRecordId,
      fileName: version.fileName || file.fileName,
      repositoryPath: version.repositoryPath || file.repositoryPath,
      renditionPath: version.renditionPath || version.repositoryPath,
      contentType: version.contentType || 'application/pdf',
      version: version.versionNumber,
      annotationStateJson: annotationStateJson || '{}',
    };
  };

  const openDocument = async (document: ProcedureCaseDocument) => {
    if (!selectedCase || !document.fileUrl) {
      return;
    }

    if (!document.fileUrl.startsWith('/api/')) {
      window.open(document.fileUrl, '_blank', 'noopener,noreferrer');
      return;
    }

    try {
      const blob = await procedureCaseService.downloadDocumentContent(
        selectedCase.id,
        document.id
      );
      const objectUrl = URL.createObjectURL(blob);
      window.open(objectUrl, '_blank', 'noopener,noreferrer');
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to open document.');
    }
  };

  const updateDocumentNotes = (documentId: string, value: string) => {
    setSelectedCase((current) => {
      if (!current) {
        return current;
      }

      return {
        ...current,
        documents: current.documents.map((document) =>
          document.id === documentId ? { ...document, notes: value } : document
        ),
      };
    });
  };

  const selectDocumentFile = (documentId: string, file: File | null) => {
    setDocumentFiles((current) => ({ ...current, [documentId]: file }));
  };

  const generateProcedureDocument = async () => {
    if (!selectedCase || !selectedGenerationTemplate) {
      return;
    }

    const mergeValues = Object.fromEntries(
      selectedCase.fields.flatMap((field) => {
        const value = field.value ?? '';
        return [
          [field.key, value],
          [field.label, value],
        ];
      })
    );
    const mergeValueByKey = new Map(
      selectedCase.fields.map((field) => [field.key, field.value ?? ''])
    );
    const setMergeAlias = (alias: string, ...keys: string[]) => {
      const value = keys
        .map((key) => mergeValueByKey.get(key))
        .find((item) => item);
      if (value) {
        mergeValues[alias] = value;
      }
    };

    setMergeAlias('ApplicantName', 'applicantName');
    setMergeAlias(
      'PropertyNumber',
      'propertyNumber',
      'housePlotShopNumber',
      'unitNumber'
    );
    setMergeAlias(
      'HousePlotShopNumber',
      'housePlotShopNumber',
      'propertyNumber',
      'unitNumber'
    );
    setMergeAlias('TransferorName', 'transferorName', 'oldLesseeName');
    setMergeAlias('TransfereeName', 'transfereeName', 'newLesseeName');
    setMergeAlias('NewLesseeAddress', 'newLesseeAddress', 'addressOnRecord');
    setMergeAlias('TransferEffectiveDate', 'transferEffectiveDate');
    setMergeAlias(
      'TransferDeclarationReference',
      'transferDeclarationReference'
    );
    setMergeAlias('VoluntaryVacationReference', 'voluntaryVacationReference');
    setMergeAlias('HosFormReference', 'hosFormReference');
    setMergeAlias('HouseType', 'houseType');
    setMergeAlias(
      'PurchaseAmount',
      'purchaseAmount',
      'sellingPrice',
      'considerationAmount'
    );
    setMergeAlias('PurchaseDate', 'purchaseDate');
    setMergeAlias('SopSectionReference', 'sopSectionReference');
    setMergeAlias(
      'ApprovedFeeScheduleReference',
      'approvedFeeScheduleReference',
      'approvedRateReference'
    );
    setMergeAlias('DocumentTemplateReference', 'documentTemplateReference');
    setMergeAlias('FinanceReference', 'financeReference', 'feeReference');
    setMergeAlias('LegalReference', 'legalReference');
    setMergeAlias(
      'RecordsReference',
      'estateRecordsReference',
      'recordsUpdateReference',
      'registerReference'
    );
    setMergeAlias(
      'ReportReference',
      'reportingReference',
      'quarterlyReportReference',
      'boardSubmissionReference'
    );
    setMergeAlias('OfferLetterReference', 'offerLetterReference');
    setMergeAlias('RightOfEntryReference', 'rightOfEntryReference');
    setMergeAlias('LeaseRequestFormReference', 'leaseRequestFormReference');
    setMergeAlias('RegisteredLeaseReference', 'registeredLeaseReference');
    setMergeAlias('LandUse', 'landUse');
    setMergeAlias(
      'Premium',
      'landManagementFeePayable',
      'renewalPremium',
      'transferFeePayable'
    );
    setMergeAlias('GroundRent', 'groundRentPayable', 'improvedGroundRent');
    setMergeAlias('PaymentFrequency', 'paymentFrequency');
    setMergeAlias('LeaseTerm', 'leaseTerm', 'leaseTermYears');
    setMergeAlias(
      'MoveInDate',
      'moveInDate',
      'dateOfTenancy',
      'leaseCommencementDate'
    );
    setMergeAlias(
      'OriginalLeaseReference',
      'originalLeaseReference',
      'registeredLeaseReference'
    );
    setMergeAlias('VariationReason', 'variationReason', 'leaseVariationReason');
    setMergeAlias('VendorName', 'vendorName', 'ownerName');
    setMergeAlias(
      'AgreedAmount',
      'agreedAmount',
      'considerationAmount',
      'purchaseAmount'
    );
    setMergeAlias('PaymentBasis', 'paymentBasis', 'vendorPaymentMethod');
    setMergeAlias(
      'ApprovalReference',
      'approvalReference',
      'mdApprovalReference'
    );
    setMergeAlias('OfferExpiryDate', 'offerExpiryDate', 'paymentDeadline');
    setMergeAlias('CaseReference', 'referenceNumber', 'fileReference');
    setMergeAlias(
      'InstrumentType',
      'instrumentType',
      'transferProcessType',
      'mortgageType',
      'housingRequestType'
    );
    setMergeAlias('ScheduleReference', 'scheduleReference');
    setMergeAlias('ClientExecutionDate', 'clientExecutionDate');
    setMergeAlias('MortgageeName', 'mortgageeName');
    setMergeAlias(
      'PaymentReceiptReference',
      'paymentReceiptReference',
      'transferFeeReceipt',
      'feeReference'
    );
    setMergeAlias('MortgageLetterReference', 'mortgageLetterReference');
    setMergeAlias(
      'MdApprovalReference',
      'mdApprovalReference',
      'approvalReference'
    );
    setMergeAlias(
      'TransferFeeReceipt',
      'transferFeeReceipt',
      'paymentReceiptReference'
    );
    setMergeAlias('TerminationReason', 'terminationReason');
    setMergeAlias('SiteReportReference', 'siteReportReference');
    setMergeAlias('NoticePostingStartDate', 'noticePostingStartDate');
    setMergeAlias('NoticePostingEndDate', 'noticePostingEndDate');
    setMergeAlias(
      'RecognitionApplicantName',
      'recognitionApplicantName',
      'applicantName'
    );
    setMergeAlias(
      'RecognitionPaymentStatus',
      'recognitionPaymentStatus',
      'paymentStatus'
    );
    setMergeAlias(
      'RecognitionDocumentReference',
      'recognitionDocumentReference'
    );
    setMergeAlias('SignatureStatus', 'signatureStatus');
    setMergeAlias('AssignorName', 'assignorName', 'transferorName');
    setMergeAlias('AssigneeName', 'assigneeName', 'transfereeName');
    setMergeAlias('VestingInstrumentReference', 'vestingInstrumentReference');
    setMergeAlias('ConsentDecision', 'consentDecision');
    setMergeAlias('CourtName', 'courtName');
    setMergeAlias('CaseNumber', 'caseNumber');
    setMergeAlias('CourtProcessType', 'courtProcessType');
    setMergeAlias('ServiceDate', 'serviceDate');
    setMergeAlias('ResponseDeadline', 'responseDeadline');
    setMergeAlias('FilingReference', 'filingReference');

    setIsGeneratingDocument(true);
    setError(null);
    try {
      const result =
        await documentManagementService.generateDocumentFromTemplate({
          templateCode: selectedGenerationTemplate,
          sourceModule: module,
          sourceLabel: generatedDocumentSourceLabel,
          sourceEntityType: entityType,
          sourceRecordReference:
            selectedCase.referenceNumber || selectedCase.title,
          sourceRecordId: selectedCase.id,
          caseTitle: selectedCase.title,
          caseReference: selectedCase.referenceNumber || selectedCase.title,
          applicantName: selectedCase.applicantName || undefined,
          preparedBy: generatedDocumentPreparedBy,
          purpose: selectedCase.currentStageName,
          mergeValues,
        });
      setGeneratedDocument(result);
      const refreshedCase = await procedureCaseService.getCase(selectedCase.id);
      setSelectedCase(refreshedCase);
      await loadCases();
      setIsGeneratedViewerOpen(true);
      toast({
        title: 'Document generated',
        description: 'The generated document is ready to review.',
        variant: 'success',
      });
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : `Unable to generate ${module} document.`
      );
    } finally {
      setIsGeneratingDocument(false);
    }
  };

  const completeStage = async () => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const fieldValues = Object.fromEntries(
        selectedCase.fields.map((field) => [field.key, field.value ?? null])
      );
      await procedureCaseService.updateFields(selectedCase.id, {
        fieldValues,
        referenceNumber: selectedCase.referenceNumber,
        applicantName: selectedCase.applicantName,
        sourceDepartment: selectedCase.sourceDepartment,
        receivedDate: selectedCase.receivedDate,
        description: selectedCase.description,
      });
      const updated = await procedureCaseService.completeStage(
        selectedCase.id,
        'Stage completed from workspace.'
      );
      setSelectedCase(updated);
      await loadCases();
      toast({
        title: 'Stage submitted',
        description:
          updated.status === 'Completed'
            ? 'This case is now completed.'
            : `Current stage: ${updated.currentStageName}.`,
        variant: 'success',
      });
    } catch (err) {
      setError(
        err instanceof Error ? err.message : 'Unable to submit current stage.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const syncLegalTransferFeePaymentStatus = async () => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated =
        await procedureCaseService.syncLegalTransferFeePaymentStatus(
          selectedCase.id
        );
      setSelectedCase(updated);
      await loadCases();
      const paymentStatus =
        updated.fields.find((field) => field.key === 'paymentStatus')?.value ??
        'Pending';
      const receiptReference = updated.fields.find(
        (field) => field.key === 'paymentReceiptReference'
      )?.value;
      toast({
        title:
          paymentStatus === 'Paid'
            ? 'Transfer fee payment synced'
            : 'Transfer fee still pending',
        description: receiptReference
          ? `Receipt: ${receiptReference}`
          : undefined,
        variant: paymentStatus === 'Paid' ? 'success' : 'default',
      });
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Unable to sync transfer fee payment status.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  const signLegalTransferExecutedForm = async (
    document: ProcedureCaseDocument
  ) => {
    if (!selectedCase) {
      return;
    }

    const signatureRole =
      LEGAL_TRANSFER_SIGNATURE_STAGE_ROLES[selectedCase.currentStageName];
    if (!signatureRole) {
      return;
    }

    setSigningDocumentId(document.id);
    setError(null);
    try {
      const updated =
        await procedureCaseService.signLegalTransferExecutedDocument(
          selectedCase.id,
          document.id,
          {
            signatureRole,
            notes: `${signatureRole} signed from Legal transfer workspace.`,
          }
        );
      setSelectedCase(updated);
      await loadCases();
      toast({
        title: 'Transfer document signed',
        description: `${signatureRole} signature recorded on the executed transfer form.`,
        variant: 'success',
      });
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Unable to sign the transfer document.'
      );
    } finally {
      setSigningDocumentId(null);
    }
  };

  const renderField = (field: ProcedureCaseDetail['fields'][number]) => {
    const isCalculated = CALCULATED_PROCEDURE_FIELD_KEYS.has(field.key);
    const isLinkedLegalReadonly =
      isLinkedLegalMatter && !linkedLegalEditableFields.has(field.key);
    const isDisabled =
      !canEditProcedureField(field.key) ||
      isCalculated ||
      isLinkedLegalReadonly;
    const isRequiredForStage = legalTransferRequiredFieldKeys.has(field.key);
    const fieldType = field.fieldType.toLowerCase();
    const fieldId = `procedure-field-${field.id}`;
    const label = (
      <label
        htmlFor={fieldId}
        className="text-xs font-medium text-muted-foreground"
      >
        {field.label}
        {isRequiredForStage ? (
          <span className="ml-1 text-destructive">*</span>
        ) : null}
      </label>
    );

    if (fieldType === 'select' && field.options?.length) {
      return (
        <div key={field.id} className="space-y-1.5">
          {label}
          <Select
            value={field.value ?? undefined}
            disabled={isDisabled}
            onValueChange={(value) => updateFieldValue(field.key, value)}
          >
            <SelectTrigger id={fieldId}>
              <SelectValue placeholder={field.label} />
            </SelectTrigger>
            <SelectContent>
              {field.options.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      );
    }

    if (fieldType === 'textarea') {
      return (
        <div key={field.id} className="space-y-1.5 md:col-span-2">
          {label}
          <Textarea
            id={fieldId}
            placeholder={field.label}
            value={field.value ?? ''}
            disabled={isDisabled}
            onChange={(event) =>
              updateFieldValue(field.key, event.target.value)
            }
          />
        </div>
      );
    }

    return (
      <div key={field.id} className="space-y-1.5">
        {label}
        <Input
          id={fieldId}
          type={
            fieldType === 'date'
              ? 'date'
              : fieldType === 'number' || fieldType === 'currency'
                ? 'number'
                : 'text'
          }
          placeholder={field.label}
          value={field.value ?? ''}
          disabled={isDisabled}
          step={
            fieldType === 'currency' || fieldType === 'number'
              ? '0.01'
              : undefined
          }
          onChange={(event) => updateFieldValue(field.key, event.target.value)}
        />
      </div>
    );
  };

  const renderCreateCaseForm = () => (
    <div className="space-y-3">
      <Input
        value={newCase.title}
        onChange={(event) =>
          setNewCase({ ...newCase, title: event.target.value })
        }
      />
      <Input
        placeholder="Applicant / party name"
        value={newCase.applicantName}
        onChange={(event) =>
          setNewCase({
            ...newCase,
            applicantName: event.target.value,
          })
        }
      />
      <Select
        value={newCase.organizationUnitId || undefined}
        disabled={isLoadingDepartments || departmentOptions.length === 0}
        onValueChange={(value) => {
          const department = departmentOptions.find((item) => item.id === value);
          setNewCase({
            ...newCase,
            sourceDepartment: department?.name ?? '',
            organizationLevelId: department?.organizationLevelId ?? '',
            organizationUnitId: department?.id ?? '',
          });
        }}
      >
        <SelectTrigger>
          <SelectValue
            placeholder={
              isLoadingDepartments
                ? 'Loading departments'
                : 'Select source department'
            }
          />
        </SelectTrigger>
        <SelectContent>
          {departmentOptions.map((department) => (
            <SelectItem key={department.id} value={department.id}>
              {department.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {!isLoadingDepartments && departmentOptions.length === 0 ? (
        <p className="text-xs text-destructive">
          No active Department-level HR organization units are configured.
        </p>
      ) : null}
      <Input
        type="date"
        value={newCase.receivedDate}
        onChange={(event) =>
          setNewCase({
            ...newCase,
            receivedDate: event.target.value,
          })
        }
      />
      <Textarea
        placeholder="Description"
        value={newCase.description}
        onChange={(event) =>
          setNewCase({
            ...newCase,
            description: event.target.value,
          })
        }
      />
      <Button
        className="w-full gap-2"
        onClick={() => void createCase()}
        disabled={isSaving || !selectedNewCaseDepartment}
      >
        {isSaving ? (
          <Loader2 className="h-4 w-4 animate-spin" />
        ) : (
          <Plus className="h-4 w-4" />
        )}
        {terminology.createLabel}
      </Button>
    </div>
  );

  const caseDetailHref = (id: string) => {
    const base = caseBasePath || pathname;
    return `${base.replace(/\/$/, '')}/cases/${encodeURIComponent(id)}`;
  };

  return (
    <>
      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle>{terminology.title}</CardTitle>
            </div>
            <Badge
              variant={
                selectedCase?.usesConfiguredWorkflow ? 'default' : 'outline'
              }
            >
              {selectedCase?.usesConfiguredWorkflow
                ? 'Administration workflow'
                : 'Procedure stages'}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {error ? (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {error}
            </div>
          ) : null}
          <div className="space-y-4">
              {!detailOnly ? (
              <div className="rounded-md border border-border bg-background p-4">
                <div className="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <h2 className="text-sm font-semibold">
                    {terminology.collectionLabel}
                  </h2>
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="flex items-center gap-2 text-xs text-muted-foreground">
                      {isLoading ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : null}
                      <span>
                        {caseTotalCount} case
                        {caseTotalCount === 1 ? '' : 's'}
                      </span>
                    </div>
                    {allowsManualCaseCreation ? (
                      <Button
                        type="button"
                        size="sm"
                        className="gap-2"
                        onClick={() => setIsCreateDialogOpen(true)}
                      >
                        <Plus className="h-4 w-4" />
                        {terminology.createLabel}
                      </Button>
                    ) : null}
                  </div>
                </div>
                {cases.length === 0 && !isLoading ? (
                  <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                    {terminology.emptyMessage}
                  </p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[760px] text-sm">
                      <thead className="border-b bg-muted/40 text-left text-xs uppercase tracking-wide text-muted-foreground">
                        <tr>
                          <th className="px-3 py-2 font-medium">Reference</th>
                          <th className="px-3 py-2 font-medium">Applicant</th>
                          <th className="px-3 py-2 font-medium">Stage</th>
                          <th className="px-3 py-2 font-medium">Assigned</th>
                          <th className="px-3 py-2 font-medium">Status</th>
                          <th className="px-3 py-2 text-right font-medium">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y">
                        {cases.map((procedureCase) => (
                          <tr
                            key={procedureCase.id}
                            className="bg-background"
                          >
                            <td className="px-3 py-3 align-top">
                              <div className="font-medium">
                                {procedureCase.referenceNumber ||
                                  procedureCase.title}
                              </div>
                              <div className="mt-1 max-w-[22rem] truncate text-xs text-muted-foreground">
                                {procedureCase.title}
                              </div>
                            </td>
                            <td className="px-3 py-3 align-top text-muted-foreground">
                              {procedureCase.applicantName || 'Not set'}
                            </td>
                            <td className="px-3 py-3 align-top">
                              <Badge variant="secondary">
                                {procedureCase.currentStageName}
                              </Badge>
                            </td>
                            <td className="px-3 py-3 align-top text-muted-foreground">
                              {procedureCase.currentAssignedRole ||
                                'Unassigned'}
                            </td>
                            <td className="px-3 py-3 align-top">
                              <Badge
                                variant="outline"
                                className={getStatusBadgeClassName(
                                  procedureCase.status
                                )}
                              >
                                {procedureCase.status}
                              </Badge>
                            </td>
                            <td className="px-3 py-3 text-right align-top">
                              <Button asChild size="sm" variant="outline">
                                <Link
                                  href={caseDetailHref(procedureCase.id)}
                                  className="gap-2"
                                >
                                  <Eye className="h-4 w-4" />
                                  View
                                </Link>
                              </Button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
                {caseTotalCount > casePageSize ? (
                  <Pagination
                    currentPage={casePage}
                    totalPages={caseTotalPages}
                    totalItems={caseTotalCount}
                    pageSize={casePageSize}
                    onPageChange={setCasePage}
                    onPageSizeChange={(nextPageSize) => {
                      setCasePageSize(nextPageSize);
                      setCasePage(1);
                    }}
                  />
                ) : null}
              </div>
              ) : null}

              {allowsManualCaseCreation && !registerOnly && !detailOnly ? (
                <div className="rounded-md border border-border bg-background p-4">
                  <h2 className="text-sm font-semibold">
                    {terminology.createHeading}
                  </h2>
                  <div className="mt-3">{renderCreateCaseForm()}</div>
                </div>
              ) : null}
            {!registerOnly && selectedCase ? (
              <div className="space-y-4">
                <div className="rounded-md border border-border bg-background p-4">
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div>
                      <h2 className="text-base font-semibold">
                        {selectedCase.referenceNumber || selectedCase.title}
                      </h2>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {selectedCase.currentStageName}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge
                        variant="outline"
                        className={getStatusBadgeClassName(
                          selectedCase.status
                        )}
                      >
                        {selectedCase.status}
                      </Badge>
                      {selectedCase.currentAssignedRole ? (
                        <Badge>{selectedCase.currentAssignedRole}</Badge>
                      ) : null}
                      <Badge
                        variant={
                          selectedCase.canEditCurrentStage
                            ? 'secondary'
                            : 'outline'
                        }
                      >
                        {selectedCase.canEditCurrentStage
                          ? 'Editable'
                          : 'Read only'}
                      </Badge>
                      {module === 'Legal' && originatingPropertyCaseId ? (
                        <Button asChild size="sm" variant="outline">
                          <Link
                            href={`/estate/property-management/EstatePropertyManagementListingApplication?caseId=${encodeURIComponent(originatingPropertyCaseId)}`}
                          >
                            <ExternalLink className="mr-2 h-4 w-4" />
                            Source property record
                          </Link>
                        </Button>
                      ) : null}
                    </div>
                  </div>
                </div>

                <Tabs defaultValue="stage" className="space-y-4">
                  <TabsList className="flex h-auto flex-wrap justify-start">
                    <TabsTrigger value="stage">Stage details</TabsTrigger>
                    <TabsTrigger value="documents">Documents</TabsTrigger>
                    <TabsTrigger value="submit">Submit</TabsTrigger>
                  </TabsList>
                  <TabsContent value="stage" className="space-y-4">
                <div className="rounded-md border border-border bg-background p-4">
                  <div className="mb-3 flex items-center justify-between gap-2">
                    <h2 className="text-sm font-semibold">Intake</h2>
                    <Button
                      size="sm"
                      variant="outline"
                      className="gap-2"
                      onClick={() => void saveIntake()}
                      disabled={!selectedCase.canEditCurrentStage || isSaving}
                    >
                      <Save className="h-4 w-4" />
                      Save
                    </Button>
                  </div>
                  <div className="grid gap-3 md:grid-cols-2">
                    <div className="space-y-1.5">
                      <label
                        htmlFor="procedure-reference-number"
                        className="text-xs font-medium text-muted-foreground"
                      >
                        Reference number
                      </label>
                      <Input
                        id="procedure-reference-number"
                        value={selectedCase.referenceNumber ?? ''}
                        disabled
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label
                        htmlFor="procedure-applicant-name"
                        className="text-xs font-medium text-muted-foreground"
                      >
                        Applicant / party name
                      </label>
                      <Input
                        id="procedure-applicant-name"
                        value={selectedCase.applicantName ?? ''}
                        disabled={
                          !canEditProcedureField('applicantName') ||
                          isLinkedLegalMatter
                        }
                        onChange={(event) =>
                          setSelectedCase({
                            ...selectedCase,
                            applicantName: event.target.value,
                          })
                        }
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label
                        htmlFor="procedure-source-department"
                        className="text-xs font-medium text-muted-foreground"
                      >
                        Source department
                      </label>
                      <Select
                        value={selectedCaseDepartmentValue || undefined}
                        disabled={
                          !canEditProcedureField('sourceDepartment') ||
                          isLinkedLegalMatter ||
                          isLoadingDepartments ||
                          departmentOptions.length === 0
                        }
                        onValueChange={(value) => {
                          const department = departmentOptions.find(
                            (item) => item.id === value
                          );
                          setSelectedCase({
                            ...selectedCase,
                            sourceDepartment: department?.name ?? '',
                            organizationLevelId:
                              department?.organizationLevelId ?? null,
                            organizationUnitId: department?.id ?? null,
                            organizationLevelName:
                              department?.levelName ?? null,
                            organizationUnitName: department?.name ?? null,
                          });
                        }}
                      >
                        <SelectTrigger id="procedure-source-department">
                          <SelectValue
                            placeholder={
                              isLoadingDepartments
                                ? 'Loading departments'
                                : 'Select source department'
                            }
                          />
                        </SelectTrigger>
                        <SelectContent>
                          {departmentOptions.map((department) => (
                            <SelectItem key={department.id} value={department.id}>
                              {department.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1.5">
                      <label
                        htmlFor="procedure-received-date"
                        className="text-xs font-medium text-muted-foreground"
                      >
                        Received date
                      </label>
                      <Input
                        id="procedure-received-date"
                        type="date"
                        value={selectedCase.receivedDate?.slice(0, 10) ?? ''}
                        disabled={
                          !canEditProcedureField('receivedDate') ||
                          isLinkedLegalMatter
                        }
                        onChange={(event) =>
                          setSelectedCase({
                            ...selectedCase,
                            receivedDate: event.target.value,
                          })
                        }
                      />
                    </div>
                    {selectedCase.fields
                      .filter(
                        (field) =>
                          entityType !== 'LegalTransfer' ||
                          !LEGAL_TRANSFER_FINANCE_FIELD_KEYS.has(field.key)
                      )
                      .map((field) => renderField(field))}
                  </div>
                  <div className="mt-3 space-y-1.5">
                    <label
                      htmlFor="procedure-description"
                      className="text-xs font-medium text-muted-foreground"
                    >
                      Description
                    </label>
                    <Textarea
                      id="procedure-description"
                      value={selectedCase.description ?? ''}
                      disabled={
                        !canEditProcedureField('description') ||
                        isLinkedLegalMatter
                      }
                      onChange={(event) =>
                        setSelectedCase({
                          ...selectedCase,
                          description: event.target.value,
                        })
                      }
                    />
                  </div>
                  <div className="mt-4 flex justify-end border-t border-border pt-4">
                    <Button
                      size="sm"
                      variant="outline"
                      className="gap-2"
                      onClick={() => void saveIntake()}
                      disabled={!selectedCase.canEditCurrentStage || isSaving}
                    >
                      <Save className="h-4 w-4" />
                      Save
                    </Button>
                  </div>
                </div>

                {legalTransferFinanceSnapshot ? (
                  <div className="rounded-md border border-border bg-background p-4">
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                      <div>
                        <h2 className="text-sm font-semibold">
                          Finance transfer fee
                        </h2>
                        <p className="mt-1 text-xs text-muted-foreground">
                          Read-only invoice and payment status synced from
                          Finance AR.
                        </p>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Badge
                          variant={getFinanceStatusBadgeVariant(
                            legalTransferFinanceSnapshot.invoiceStatus
                          )}
                          className={getStatusBadgeClassName(
                            formatFinanceStatus(
                              legalTransferFinanceSnapshot.invoiceStatus
                            )
                          )}
                        >
                          Invoice{' '}
                          {formatFinanceStatus(
                            legalTransferFinanceSnapshot.invoiceStatus
                          )}
                        </Badge>
                        <Badge
                          variant={getFinanceStatusBadgeVariant(
                            legalTransferFinanceSnapshot.paymentStatus
                          )}
                          className={getStatusBadgeClassName(
                            formatFinanceStatus(
                              legalTransferFinanceSnapshot.paymentStatus,
                              'Pending'
                            )
                          )}
                        >
                          Payment{' '}
                          {formatFinanceStatus(
                            legalTransferFinanceSnapshot.paymentStatus,
                            'Pending'
                          )}
                        </Badge>
                      </div>
                    </div>

                    <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Transfer fee payable
                        </div>
                        <div className="mt-1 text-sm font-semibold">
                          {formatGhsAmount(
                            legalTransferFinanceSnapshot.transferFeePayable
                          )}
                        </div>
                      </div>
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Invoice reference
                        </div>
                        <div className="mt-1 break-words text-sm font-semibold">
                          {legalTransferFinanceSnapshot.invoiceReference ||
                            'Not generated'}
                        </div>
                        {legalTransferFinanceSnapshot.paymentRequestReference ? (
                          <div className="mt-1 break-words text-xs text-muted-foreground">
                            {
                              legalTransferFinanceSnapshot.paymentRequestReference
                            }
                          </div>
                        ) : null}
                      </div>
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Invoice total
                        </div>
                        <div className="mt-1 text-sm font-semibold">
                          {formatGhsAmount(
                            legalTransferFinanceSnapshot.invoiceAmount
                          )}
                        </div>
                      </div>
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Receipt reference
                        </div>
                        <div className="mt-1 break-words text-sm font-semibold">
                          {legalTransferFinanceSnapshot.receiptReference ||
                            'Awaiting payment'}
                        </div>
                      </div>
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Amount paid
                        </div>
                        <div className="mt-1 text-sm font-semibold">
                          {formatGhsAmount(
                            legalTransferFinanceSnapshot.invoicePaidAmount
                          )}
                        </div>
                      </div>
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Balance
                        </div>
                        <div className="mt-1 text-sm font-semibold">
                          {formatGhsAmount(
                            legalTransferFinanceSnapshot.invoiceBalance
                          )}
                        </div>
                      </div>
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Customer notification
                        </div>
                        <div className="mt-1 text-sm font-semibold">
                          {legalTransferFinanceSnapshot.customerNotificationStatus ||
                            'Not notified'}
                        </div>
                      </div>
                      <div className="rounded-md border border-border bg-card p-3">
                        <div className="text-xs font-medium text-muted-foreground">
                          Finance check
                        </div>
                        <div className="mt-1 text-sm font-semibold">
                          {legalTransferFinanceSnapshot.paymentCheckStatus ||
                            'Awaiting Finance sync'}
                        </div>
                      </div>
                    </div>

                    {isLegalTransferClientPaymentStage ? (
                      <div className="mt-4 flex justify-end">
                        <Button
                          className="gap-2"
                          variant="outline"
                          onClick={() =>
                            void syncLegalTransferFeePaymentStatus()
                          }
                          disabled={
                            !selectedCase.canEditCurrentStage || isSaving
                          }
                        >
                          {isSaving ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <RefreshCw className="h-4 w-4" />
                          )}
                          Refresh Finance payment
                        </Button>
                      </div>
                    ) : null}
                  </div>
                ) : null}

                {supportsGeneratedDocuments ? (
                  <div className="rounded-md border border-border bg-background p-4">
                    <div className="mb-3 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                      <h2 className="text-sm font-semibold">
                        Generated Documents
                      </h2>
                      <div className="flex flex-col gap-2 sm:flex-row">
                        <Select
                          value={selectedGenerationTemplate || undefined}
                          onValueChange={setSelectedGenerationTemplate}
                        >
                          <SelectTrigger className="w-full sm:w-[260px]">
                            <SelectValue placeholder="Select template" />
                          </SelectTrigger>
                          <SelectContent>
                            {generationTemplates.map((template) => (
                              <SelectItem
                                key={template.templateCode}
                                value={template.templateCode}
                              >
                                {template.title}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <Button
                          className="gap-2"
                          onClick={() => void generateProcedureDocument()}
                          disabled={
                            !selectedCase.canEditCurrentStage ||
                            !selectedGenerationTemplate ||
                            isGeneratingDocument
                          }
                        >
                          {isGeneratingDocument ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <BookTemplate className="h-4 w-4" />
                          )}
                          Generate draft
                        </Button>
                      </div>
                    </div>

                    {generatedDocument ? (
                      <div className="space-y-3 rounded-md border border-border bg-card p-3">
                        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                          <div>
                            <div className="text-sm font-medium">
                              {generatedDocument.record.title}
                            </div>
                            <div className="mt-1 text-xs text-muted-foreground">
                              {generatedDocument.dmsReference} ·{' '}
                              {generatedDocument.sourceLabel}
                            </div>
                            <div className="mt-2 flex flex-wrap gap-2">
                              <Badge variant="outline">
                                {generatedDocument.record.lifecycleStatus}
                              </Badge>
                              <Badge variant="secondary">
                                {generatedDocument.record.versionStatus}
                              </Badge>
                              {generatedDocument.template.requiresApproval ? (
                                <Badge variant="outline">
                                  {generatedDocument.template.approvalRole ||
                                    'Approval required'}
                                </Badge>
                              ) : null}
                            </div>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            <Button
                              size="sm"
                              variant="outline"
                              className="gap-2"
                              onClick={() => setIsGeneratedViewerOpen(true)}
                            >
                              <Eye className="h-4 w-4" />
                              View
                            </Button>
                          </div>
                        </div>
                      </div>
                    ) : null}
                  </div>
                ) : null}

                <CentralDocumentViewerDialog
                  open={isGeneratedViewerOpen}
                  onOpenChange={setIsGeneratedViewerOpen}
                  file={
                    generatedDocument
                      ? {
                          documentRecordId: generatedDocument.record.id,
                          versionId: generatedDocument.version.id,
                          title: generatedDocument.record.title,
                          fileName: generatedDocument.version.fileName,
                          repositoryPath: generatedDocument.pdfUrl,
                          renditionPath: generatedDocument.pdfUrl,
                          contentType: generatedDocument.version.contentType,
                          sourceLabel: generatedDocument.sourceLabel,
                          version: generatedDocument.version.versionNumber,
                        }
                      : null
                  }
                />

                <div className="rounded-md border border-border bg-background p-4">
                  <h2 className="text-sm font-semibold">
                    Current Stage Checklist
                  </h2>
                  <div className="mt-3 space-y-3">
                    {currentStageItems.map((item) => (
                      <label
                        key={item.id}
                        className="flex items-start gap-3 rounded-md border border-border bg-card p-3 text-sm"
                      >
                        <Checkbox
                          checked={item.isCompleted}
                          disabled={
                            !selectedCase.canEditCurrentStage || isSaving
                          }
                          onCheckedChange={(checked) =>
                            void toggleChecklist(item.id, checked === true)
                          }
                        />
                        <span
                          className={
                            item.isCompleted
                              ? 'text-muted-foreground line-through'
                              : ''
                          }
                        >
                          {item.text}
                        </span>
                      </label>
                    ))}
                  </div>
                </div>
                  </TabsContent>

                  <TabsContent value="documents" className="space-y-4">
                <div className="rounded-md border border-border bg-background p-4">
                  <h2 className="text-sm font-semibold">Documents</h2>
                  <div className="mt-3 grid gap-3 md:grid-cols-2">
                    {visibleDocuments.map((document) => {
                      const isDmsDocument =
                        isDocumentManagementDocument(document);
                      const canModifyDocument =
                        document.canUploadAtCurrentStage &&
                        selectedCase.canEditCurrentStage;
                      const documentStageLabel = document.requiredFrom
                        ? `Required at ${document.requiredFrom}`
                        : 'Current stage document';
                      const sourceLabel =
                        document.providedBy &&
                        document.providedBy !== 'Internal'
                          ? document.providedBy
                          : document.requiredFrom;
                      const isReadOnlyProvidedDocument =
                        Boolean(document.fileName) &&
                        !canModifyDocument &&
                        (isDmsDocument ||
                          (document.providedBy &&
                            document.providedBy !== 'Internal'));
                      const isExternalProviderDocument =
                        Boolean(document.providedBy) &&
                        document.providedBy !== 'Internal' &&
                        document.providedBy !== 'Legal Admin Assistant' &&
                        document.providedBy !== 'Legal Department';
                      const isLegalTransferFeeReceiptDocument =
                        entityType === 'LegalTransfer' &&
                        document.name === 'Transfer fee payment receipt';
                      const legalTransferReceiptIsSynced =
                        isLegalTransferFeeReceiptDocument &&
                        Boolean(
                          legalTransferFinanceSnapshot?.receiptReference ||
                            legalTransferFinanceSnapshot?.paymentStatus?.toLowerCase() ===
                              'paid'
                        );
                      const signatureRole = selectedCase
                        ? LEGAL_TRANSFER_SIGNATURE_STAGE_ROLES[
                            selectedCase.currentStageName
                          ]
                        : undefined;
                      const legalTransferStageSignatureRecorded =
                        entityType === 'LegalTransfer' &&
                        document.name === 'Executed transfer form' &&
                        Boolean(signatureRole) &&
                        isLegalTransferStageSignatureRecorded(
                          selectedCase,
                          document
                        );
                      const canSignLegalTransferExecutedForm =
                        entityType === 'LegalTransfer' &&
                        document.name === 'Executed transfer form' &&
                        !isDmsDocument &&
                        Boolean(document.fileUrl) &&
                        Boolean(signatureRole) &&
                        selectedCase.canEditCurrentStage &&
                        !legalTransferStageSignatureRecorded;
                      return (
                        <div
                          key={document.id}
                          className="rounded-md border border-border bg-card p-3"
                        >
                          <div className="flex items-start justify-between gap-2">
                            <div>
                              <div className="text-sm font-medium">
                                {document.name}
                              </div>
                              <div className="mt-1 text-xs text-muted-foreground">
                                {sourceLabel || documentStageLabel}
                              </div>
                            </div>
                            <Badge
                              variant={
                                document.isMandatory ? 'default' : 'outline'
                              }
                            >
                              {document.isMandatory ? 'Required' : 'Optional'}
                            </Badge>
                          </div>
                          <div className="mt-3 space-y-2">
                            {document.fileName ? (
                              <div className="rounded-md border border-border bg-background p-2 text-xs">
                                <div className="font-medium text-foreground">
                                  {document.fileName}
                                </div>
                                {document.fileUrl ? (
                                  <div className="mt-2 flex flex-wrap gap-2">
                                    {isDmsDocument ? (
                                      <button
                                        type="button"
                                        className="inline-flex items-center gap-1 text-primary hover:underline"
                                        onClick={() =>
                                          setDmsViewerDocumentId(document.id)
                                        }
                                      >
                                        <Eye className="h-3 w-3" />
                                        View / annotate
                                      </button>
                                    ) : (
                                      <button
                                        type="button"
                                        className="inline-flex items-center gap-1 text-primary hover:underline"
                                        onClick={() =>
                                          void openDocument(document)
                                        }
                                      >
                                        <ExternalLink className="h-3 w-3" />
                                        Open uploaded file
                                      </button>
                                    )}
                                    {!isDmsDocument && isPdfDocument(document) ? (
                                      <button
                                        type="button"
                                        className="inline-flex items-center gap-1 text-primary hover:underline"
                                        onClick={() =>
                                          setPreviewDocumentId(document.id)
                                        }
                                      >
                                        <Eye className="h-3 w-3" />
                                        View PDF
                                      </button>
                                    ) : null}
                                    {canSignLegalTransferExecutedForm ? (
                                      <button
                                        type="button"
                                        className="inline-flex items-center gap-1 text-primary hover:underline disabled:cursor-not-allowed disabled:opacity-60"
                                        disabled={Boolean(signingDocumentId)}
                                        onClick={() =>
                                          void signLegalTransferExecutedForm(
                                            document
                                          )
                                        }
                                      >
                                        {signingDocumentId === document.id ? (
                                          <Loader2 className="h-3 w-3 animate-spin" />
                                        ) : (
                                          <FilePenLine className="h-3 w-3" />
                                        )}
                                        Sign document
                                      </button>
                                    ) : null}
                                    {legalTransferStageSignatureRecorded ? (
                                      <span className="inline-flex items-center gap-1 text-muted-foreground">
                                        <CheckCircle2 className="h-3 w-3" />
                                        Signed
                                      </span>
                                    ) : null}
                                  </div>
                                ) : null}
                                {signingDocumentId === document.id ? (
                                  <div className="mt-2 flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-2 text-amber-950">
                                    <Loader2 className="mt-0.5 h-3.5 w-3.5 shrink-0 animate-spin" />
                                    <div>
                                      <div className="font-semibold">
                                        Digitally signing transfer document
                                      </div>
                                      <p className="mt-0.5">
                                        This can take up to a minute while the
                                        PDF is signed and saved. Wait for the
                                        success message before submitting the
                                        stage.
                                      </p>
                                    </div>
                                  </div>
                                ) : null}
                              </div>
                            ) : null}
                            {legalTransferReceiptIsSynced ? (
                              <div className="space-y-2 rounded-md border border-border bg-background p-2 text-xs">
                                <div className="font-medium text-foreground">
                                  {legalTransferFinanceSnapshot?.receiptReference ||
                                    'Payment confirmed in Finance'}
                                </div>
                                <div className="text-muted-foreground">
                                  Finance has confirmed the transfer-fee payment
                                  for this legal matter.
                                </div>
                                {legalTransferFinanceSnapshot?.invoiceReference ? (
                                  <div className="text-muted-foreground">
                                    Invoice{' '}
                                    {
                                      legalTransferFinanceSnapshot.invoiceReference
                                    }
                                    {legalTransferFinanceSnapshot.invoicePaidAmount
                                      ? ` · Paid ${formatGhsAmount(legalTransferFinanceSnapshot.invoicePaidAmount)}`
                                      : ''}
                                  </div>
                                ) : null}
                              </div>
                            ) : isReadOnlyProvidedDocument ? (
                              <div className="rounded-md border border-border bg-background px-2 py-1.5 text-xs text-muted-foreground">
                                Submitted from {document.providedBy || 'DMS'}.
                              </div>
                            ) : isExternalProviderDocument ? (
                              <div className="rounded-md border border-border bg-background px-2 py-1.5 text-xs text-muted-foreground">
                                Awaiting {document.providedBy}.
                              </div>
                            ) : !canModifyDocument ? (
                              <div className="rounded-md border border-border bg-background px-2 py-1.5 text-xs text-muted-foreground">
                                {document.fileName
                                  ? `${documentStageLabel}. This document is view-only outside its owning stage.`
                                  : `${documentStageLabel}. Upload is available only when the case reaches that stage.`}
                              </div>
                            ) : (
                              <>
                                <Input
                                  type="file"
                                  accept=".pdf,.doc,.docx,.txt,.rtf,.jpg,.jpeg,.png,.gif,.bmp,.svg,.webp,.ico"
                                  disabled={
                                    !canModifyDocument ||
                                    isSaving
                                  }
                                  onChange={(event) =>
                                    selectDocumentFile(
                                      document.id,
                                      event.target.files?.[0] ?? null
                                    )
                                  }
                                />
                                {documentFiles[document.id] ? (
                                  <div className="text-xs text-muted-foreground">
                                    Selected: {documentFiles[document.id]?.name}
                                  </div>
                                ) : null}
                                {!isDmsDocument ? (
                                  <Textarea
                                    placeholder="Notes"
                                    value={document.notes ?? ''}
                                    disabled={!canModifyDocument}
                                    onChange={(event) =>
                                      updateDocumentNotes(
                                        document.id,
                                        event.target.value
                                      )
                                    }
                                  />
                                ) : (
                                  <div className="rounded-md border border-border bg-background px-2 py-1.5 text-xs text-muted-foreground">
                                    Upload an edited Word/PDF copy here to make
                                    it the current DMS version.
                                  </div>
                                )}
                                <Button
                                  size="sm"
                                  variant="outline"
                                  className="w-full gap-2"
                                  disabled={
                                    !canModifyDocument ||
                                    isSaving ||
                                    (isDmsDocument &&
                                      !documentFiles[document.id])
                                  }
                                  onClick={() => void saveDocument(document)}
                                >
                                  <FileUp className="h-4 w-4" />
                                  {isDmsDocument
                                    ? 'Upload edited version'
                                    : documentFiles[document.id]
                                    ? 'Upload document'
                                    : 'Save notes'}
                                </Button>
                              </>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
                  </TabsContent>

                  <TabsContent value="submit" className="space-y-4">
                <div className="flex flex-col gap-3 rounded-md border border-border bg-background p-4 md:flex-row md:items-center md:justify-between">
                  <div className="flex items-start gap-2 text-sm text-muted-foreground">
                    <CheckCircle2 className="mt-0.5 h-4 w-4 text-primary" />
                    <span>
                      {legalTransferRequiredFieldMessages.length > 0
                        ? legalTransferRequiredFieldMessages[0]
                        : facilitiesMaintenanceCloseoutBlocker
                          ? facilitiesMaintenanceCloseoutBlocker
                        : isLegalTransferClientPaymentStage &&
                            !legalTransferPaymentReady
                          ? 'Finance payment must be synced before this stage can be submitted.'
                          : 'All current-stage checklist items must be complete before submission.'}
                    </span>
                  </div>
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                    <Button
                      className="gap-2"
                      onClick={() => void completeStage()}
                      disabled={isStageSubmitDisabled}
                    >
                      <Send className="h-4 w-4" />
                      Submit stage
                    </Button>
                  </div>
                </div>
                  </TabsContent>
                </Tabs>
              </div>
            ) : (
              <div className="rounded-md border border-border bg-background p-8 text-center text-sm text-muted-foreground">
                {terminology.selectMessage}
              </div>
            )}
          </div>
        </CardContent>
      </Card>
      <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{terminology.createHeading}</DialogTitle>
            <DialogDescription>
              Source department is selected from HR organization units at the
              Department level. The reference number is generated by the
              system.
            </DialogDescription>
          </DialogHeader>
          {renderCreateCaseForm()}
        </DialogContent>
      </Dialog>
      <CentralDocumentViewerDialog
        open={Boolean(dmsViewerDocument)}
        onOpenChange={(open) => {
          if (!open) {
            setDmsViewerDocumentId(null);
          }
        }}
        file={toDmsViewerFile(dmsViewerDocument)}
        enableAnnotations={Boolean(
          dmsViewerDocument?.canUploadAtCurrentStage &&
            selectedCase?.canEditCurrentStage
        )}
        onGenerateRendition={generateDmsRendition}
        onDownload={downloadDmsVersion}
        onSaveAnnotations={saveDmsAnnotations}
      />
      <Dialog
        open={Boolean(previewDocument && previewDocumentUrl)}
        onOpenChange={(open) => {
          if (!open) {
            setPreviewDocumentId(null);
          }
        }}
      >
        <DialogContent className="h-[94vh] max-h-[94vh] max-w-[96vw] overflow-hidden p-0 xl:max-w-7xl">
          <DialogHeader className="border-b border-border px-5 py-4">
            <DialogTitle>
              {previewDocument?.name ?? 'Document preview'}
            </DialogTitle>
            <DialogDescription>
              {previewDocument?.fileName ?? 'PDF document'}
            </DialogDescription>
          </DialogHeader>
          {previewDocument && previewDocumentUrl ? (
            <div className="h-[calc(94vh-92px)] overflow-hidden px-5 pb-5">
              <ProcedurePdfViewer
                fileUrl={previewDocumentUrl}
                fileName={previewDocument.fileName}
                height="calc(94vh - 150px)"
              />
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
