'use client';

import Link from 'next/link';
import dynamic from 'next/dynamic';
import React from 'react';
import { usePathname, useSearchParams } from 'next/navigation';
import { toast } from 'sonner';
import {
  CheckCircle2,
  ClipboardCheck,
  Download,
  Eye,
  FileText,
  FileSignature,
  Gavel,
  Loader2,
  PenLine,
  RefreshCw,
  Save,
  Send,
  ShieldCheck,
  Settings2,
  Undo2,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { CentralDocumentViewerDialog } from '@/components/document-management/CentralDocumentViewerDialog';
import { useAuth } from '@/hooks/use-auth';
import { getStatusBadgeClassName } from '@/lib/status-badge';
import {
  procedureCaseService,
  type ProcedureCaseDetail,
  type ProcedureCaseDocument,
  type ProcedureCaseField,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';
import { estatePropertyManagementService } from '@/services/estate-property-management.service';
import {
  documentManagementService,
  type CentralDocumentRecord,
  type CentralDocumentGenerationTemplate,
  type GeneratedCentralDocumentResult,
} from '@/services/document-management.service';
import {
  isLegalAgreementReviewCompleteForSigningLocation,
  isLegalAgreementReviewSigned,
  propertyListingCompletionRequirements,
} from './property-workspace-utils';

const ENTITY_TYPE = 'EstatePropertyManagementListingApplication';
const REQUESTS_PER_PAGE = 10;
const SALE_CLOSEOUT_COMPLETED_QUEUE_KEY =
  'property-management.sale-closeout-completed-case-ids';

const SYSTEM_MANAGED_AGREEMENT_FIELD_KEYS = new Set([
  'signedAgreementReference',
  'agreementExecutionStatus',
  'internalApprovalStatus',
  'internalSignatureStatus',
  'finalSignedAgreementReference',
]);

const SYSTEM_MANAGED_PREMIUM_FIELD_KEYS = new Set([
  'premiumChargeRequired',
  'premiumChargeAmount',
  'premiumChargeInvoiceId',
  'premiumChargeInvoiceReference',
  'premiumChargeInvoiceStatus',
  'premiumChargePaidAmount',
  'premiumChargeBalance',
  'premiumChargePaymentStatus',
]);

const ProcedurePdfViewer = dynamic(
  () => import('@/components/procedures/ProcedurePdfViewer'),
  {
    ssr: false,
    loading: () => (
      <div className="rounded-md border p-4 text-sm text-muted-foreground">
        Loading PDF viewer...
      </div>
    ),
  }
);

const SUMMARY_FIELD_KEYS = [
  'applicationReference',
  'customerName',
  'customerAccountReference',
  'sourceReference',
  'propertyUnit',
  'listingReference',
  'requestType',
  'listingType',
  'listingPrice',
  'offerAmount',
  'currency',
  'salesAmountPaid',
  'salesPaymentReference',
  'estateRemainingAmount',
  'premiumChargeRequired',
  'premiumChargeAmount',
  'premiumChargeInvoiceId',
  'premiumChargeInvoiceReference',
  'premiumChargeInvoiceStatus',
  'premiumChargePaidAmount',
  'premiumChargeBalance',
  'premiumChargePaymentStatus',
  'requestedLeaseTerm',
  'requestMessage',
  'decisionStatus',
  'customerNotificationStatus',
  'customerAcceptanceStatus',
  'agreementTemplateReference',
  'generatedAgreementReference',
  'signedAgreementReference',
  'agreementExecutionStatus',
  'internalApprovalStatus',
  'internalSignatureStatus',
  'finalSignedAgreementReference',
  'finalSignedAgreementVersion',
  'moveInDate',
  'moveInEffectiveStatus',
  'billingStartDate',
  'billingStartStatus',
  'receivedDate',
];

const errorMessage = (error: unknown, fallback: string) =>
  error instanceof Error ? error.message : fallback;

const formatValue = (field: ProcedureCaseField): string => {
  if (!field.value) return 'Not provided';
  if (field.fieldType !== 'date') return field.value;

  const date = new Date(field.value);
  return Number.isNaN(date.getTime())
    ? field.value
    : new Intl.DateTimeFormat('en-GB').format(date);
};

const formatMoney = (amount: number, currencyCode = 'GHS') =>
  Number.isFinite(amount)
    ? new Intl.NumberFormat('en-GH', {
        style: 'currency',
        currency: currencyCode || 'GHS',
      }).format(amount)
    : 'Not recorded';

const caseFieldValue = (procedureCase: ProcedureCaseDetail, key: string) =>
  procedureCase.fields.find((field) => field.key === key)?.value?.trim() || '';

const caseMoneyValue = (procedureCase: ProcedureCaseDetail, key: string) => {
  const value = caseFieldValue(procedureCase, key).replace(/[^\d.-]/g, '');
  return value ? Number(value) : Number.NaN;
};

const isApprovedDecision = (value: string) => {
  const normalized = value.toLowerCase();
  return normalized === 'approved' || normalized.startsWith('approved ');
};

const containsAny = (value: string, tokens: string[]) => {
  const normalized = value.toLowerCase();
  return tokens.some((token) => normalized.includes(token.toLowerCase()));
};

const isSalePaymentSatisfied = (procedureCase: ProcedureCaseDetail) => {
  if (
    caseFieldValue(procedureCase, 'salePaymentStatus').toLowerCase() ===
    'paid in full'
  ) {
    return true;
  }

  const estateRemainingAmount = caseMoneyValue(
    procedureCase,
    'estateRemainingAmount'
  );
  if (Number.isFinite(estateRemainingAmount) && estateRemainingAmount <= 0) {
    return true;
  }

  const saleInvoiceBalance = caseMoneyValue(
    procedureCase,
    'saleInvoiceBalance'
  );
  return (
    caseFieldValue(procedureCase, 'saleInvoiceStatus').toLowerCase() ===
      'paid' &&
    Number.isFinite(saleInvoiceBalance) &&
    saleInvoiceBalance <= 0
  );
};

const isPremiumChargeRequired = (procedureCase: ProcedureCaseDetail) => {
  const value = caseFieldValue(procedureCase, 'premiumChargeRequired')
    .trim()
    .toLowerCase();
  return value === 'yes' || value === 'true' || value === 'required';
};

const isPremiumChargeSettled = (procedureCase: ProcedureCaseDetail) => {
  const status = caseFieldValue(procedureCase, 'premiumChargePaymentStatus')
    .trim()
    .toLowerCase();
  return status === 'paid';
};

const PREMIUM_FIELD_KEYS = new Set([
  'premiumChargeRequired',
  'premiumChargeAmount',
  'premiumChargeInvoiceId',
  'premiumChargeInvoiceReference',
  'premiumChargeInvoiceStatus',
  'premiumChargePaidAmount',
  'premiumChargeBalance',
  'premiumChargePaymentStatus',
]);

const terminalCaseStatuses = [
  'completed',
  'archived',
  'rejected',
  'cancelled',
  'canceled',
  'closed',
];

const isCompletedCase = (procedureCase: ProcedureCaseDetail) =>
  terminalCaseStatuses.includes(procedureCase.status.trim().toLowerCase());

const summaryFieldValue = (procedureCase: ProcedureCaseSummary, key: string) =>
  procedureCase.fieldValues?.[key]?.trim() || '';

const requestTypeLabel = (procedureCase: ProcedureCaseSummary) => {
  const rawType = firstNonBlank(
    summaryFieldValue(procedureCase, 'requestType'),
    summaryFieldValue(procedureCase, 'listingType'),
    summaryFieldValue(procedureCase, 'transactionType'),
    summaryFieldValue(procedureCase, 'applicationType')
  );
  const combined = `${rawType} ${procedureCase.title}`.toLowerCase();

  if (combined.includes('lease')) return 'Lease';
  if (combined.includes('rent') || combined.includes('rental')) return 'Rent';
  if (
    combined.includes('sale') ||
    combined.includes('purchase') ||
    combined.includes('buy')
  ) {
    return 'Sale';
  }

  return rawType || 'Request';
};

const isSaleSummary = (procedureCase: ProcedureCaseSummary) => {
  const requestType = summaryFieldValue(
    procedureCase,
    'requestType'
  ).toLowerCase();
  const title = procedureCase.title.toLowerCase();
  return (
    requestType.includes('purchase') ||
    requestType.includes('sale') ||
    title.startsWith('purchase bid') ||
    title.startsWith('sale request')
  );
};

const isSaleBusinessComplete = (procedureCase: ProcedureCaseSummary) => {
  const ownershipTransferStatus = summaryFieldValue(
    procedureCase,
    'ownershipTransferStatus'
  ).toLowerCase();
  const applicationStatus = summaryFieldValue(
    procedureCase,
    'applicationStatus'
  ).toLowerCase();

  return (
    ownershipTransferStatus.startsWith('completed') ||
    applicationStatus === 'sale completed'
  );
};

const isArchivedQueueCase = (
  procedureCase: ProcedureCaseSummary,
  completedSaleOwnershipCaseIds: Set<string>
) => {
  const status = procedureCase.status.trim().toLowerCase();
  if (!terminalCaseStatuses.includes(status)) {
    return false;
  }

  if (status === 'completed' && isSaleSummary(procedureCase)) {
    return (
      completedSaleOwnershipCaseIds.has(procedureCase.id) ||
      isSaleBusinessComplete(procedureCase)
    );
  }

  return true;
};

const queueStatusLabel = (procedureCase: ProcedureCaseSummary) =>
  procedureCase.status.trim();

const isRentalApplication = (procedureCase: ProcedureCaseDetail) => {
  const requestType = caseFieldValue(
    procedureCase,
    'requestType'
  ).toLowerCase();
  if (requestType.includes('lease') || requestType.includes('rent'))
    return true;
  if (requestType.includes('purchase') || requestType.includes('sale'))
    return false;
  const listingType = caseFieldValue(
    procedureCase,
    'listingType'
  ).toLowerCase();
  const title = procedureCase.title.toLowerCase();
  const transactionText = `${requestType} ${listingType} ${title}`;
  return !(
    transactionText.includes('purchase') ||
    transactionText.includes('sale') ||
    title.startsWith('purchase bid') ||
    title.startsWith('purchase enquiry') ||
    title.startsWith('sale request')
  );
};

const isLeaseApplication = (procedureCase: ProcedureCaseDetail) => {
  const requestType = caseFieldValue(
    procedureCase,
    'requestType'
  ).toLowerCase();
  const listingType = caseFieldValue(
    procedureCase,
    'listingType'
  ).toLowerCase();
  return (
    requestType.includes('lease') ||
    (!requestType && listingType.includes('lease'))
  );
};

const isDecisionStage = (procedureCase: ProcedureCaseDetail) => {
  const stageName = procedureCase.currentStageName.toLowerCase();
  return (
    procedureCase.currentStageFieldKeys.includes('decisionStatus') ||
    stageName.includes('approval') ||
    stageName.includes('decision')
  );
};

const editableFieldKeys = (procedureCase: ProcedureCaseDetail) => {
  const keys = new Set(
    procedureCase.currentStageFieldKeys.filter(
      (key) =>
        !SYSTEM_MANAGED_AGREEMENT_FIELD_KEYS.has(key) &&
        !SYSTEM_MANAGED_PREMIUM_FIELD_KEYS.has(key)
    )
  );
  if (isDecisionStage(procedureCase)) {
    keys.add('decisionStatus');
    if (isRentalApplication(procedureCase)) {
      keys.add('moveInDate');
    }
    if (isLeaseApplication(procedureCase)) {
      keys.add('requestedLeaseTerm');
    }
  }
  return keys;
};

const requiredStageFieldKeys = (procedureCase: ProcedureCaseDetail) => {
  const approved = isApprovedDecision(
    caseFieldValue(procedureCase, 'decisionStatus')
  );
  const rental = isRentalApplication(procedureCase);

  switch (procedureCase.currentStageIndex) {
    case 0:
      return ['customerValidationStatus', 'listingValidationStatus'];
    case 1:
      return [
        'availabilityCheck',
        'commercialReviewStatus',
        'reservationStatus',
      ];
    case 2:
      return rental && approved
        ? isLeaseApplication(procedureCase)
          ? [
              'decisionStatus',
              'agreementSigningLocation',
              'requestedLeaseTerm',
              'moveInDate',
            ]
          : ['decisionStatus', 'agreementSigningLocation', 'moveInDate']
        : ['decisionStatus', 'agreementSigningLocation'];
    case 3:
      return ['legalAgreementReviewStatus'];
    case 4:
      // These values are populated by the customer portal and DMS workflow.
      // Stage completion still verifies them below with action-specific guidance.
      return [];
    case 5:
      return rental && !isLeaseApplication(procedureCase)
        ? [
            'salePaymentStatus',
            'salePaymentCheckStatus',
            'billingStartDate',
            'billingStartStatus',
          ]
        : ['salePaymentStatus', 'salePaymentCheckStatus'];
    case 6:
      return rental
        ? isLeaseApplication(procedureCase)
          ? ['legalConveyanceStatus', 'moveInEffectiveStatus']
          : ['moveInEffectiveStatus']
        : ['legalConveyanceStatus', 'ownershipTransferStatus'];
    case 7:
    default:
      return ['customerNotificationStatus', 'applicationStatus'];
  }
};

const isStageFieldComplete = (
  procedureCase: ProcedureCaseDetail,
  key: string
) => {
  const value = caseFieldValue(procedureCase, key);
  if (!value) return false;
  if (key === 'moveInDate' || key === 'billingStartDate') {
    return /^\d{4}-\d{2}-\d{2}$/.test(value);
  }
  return true;
};

const fieldLabel = (procedureCase: ProcedureCaseDetail, key: string) =>
  procedureCase.fields.find((field) => field.key === key)?.label || key;

const firstNonBlank = (...values: Array<string | null | undefined>) =>
  values.find((value) => value?.trim())?.trim() || '';

const buildAgreementMergeValues = (
  procedureCase: ProcedureCaseDetail,
  rentalApplication: boolean,
  moveInDate: string
) => {
  const leaseApplication = isLeaseApplication(procedureCase);
  const monthlyRental = rentalApplication && !leaseApplication;
  const fields = Object.fromEntries(
    procedureCase.fields.map((field) => [field.key, field.value ?? null])
  );
  const caseReference = firstNonBlank(
    procedureCase.referenceNumber,
    procedureCase.title
  );
  const customerName = firstNonBlank(
    fields.customerName,
    procedureCase.applicantName
  );
  const customerReference = firstNonBlank(
    fields.customerAccountReference,
    fields.sourceReference,
    fields.applicationReference
  );
  const propertyNumber = firstNonBlank(
    fields.propertyUnit,
    fields.listingReference
  );
  const propertyReference = firstNonBlank(
    fields.listingReference,
    fields.propertyUnit
  );
  const paymentAmount = rentalApplication
    ? firstNonBlank(fields.listingPrice, fields.offerAmount)
    : firstNonBlank(fields.offerAmount, fields.listingPrice);
  const generatedAgreementReference = firstNonBlank(
    fields.generatedAgreementReference
  );
  const premiumCharge =
    fields.premiumChargeRequired === 'Yes'
      ? firstNonBlank(fields.premiumChargeAmount, 'To be confirmed')
      : 'Not applicable';
  const annualGroundRent =
    fields.groundRentRequired === 'Yes'
      ? firstNonBlank(
          fields.groundRentPayable,
          fields.groundRentComputed,
          'To be confirmed'
        )
      : 'Not applicable';

  return {
    ...fields,
    AgreementReference: caseReference,
    AgreementDate: new Date().toISOString().slice(0, 10),
    ApplicantName: customerName,
    CaseReference: caseReference,
    PropertyNumber: propertyNumber,
    PropertyReference: propertyReference,
    PropertyLocation: fields.listingLocation || '',
    PropertyArea: [fields.listingArea, fields.listingAreaUnit]
      .filter(Boolean)
      .join(' '),
    SourceReference: firstNonBlank(
      fields.sourceReference,
      fields.listingReference,
      caseReference
    ),
    ListingReference: fields.listingReference || '',
    TenantName: customerName,
    CustomerName: customerName,
    GranteeName: customerName,
    CustomerReference: customerReference,
    RequestType: fields.requestType || '',
    RentAmount: monthlyRental ? paymentAmount : '',
    PurchasePrice: rentalApplication ? '' : paymentAmount,
    FullTermLeaseAmount: leaseApplication ? paymentAmount : '',
    MonthlyRent: monthlyRental ? paymentAmount : '',
    PaymentAmount: paymentAmount,
    PaymentType: leaseApplication
      ? 'Full term lease amount'
      : monthlyRental
        ? 'Monthly rent'
        : 'Purchase price',
    PaymentSchedule: monthlyRental ? 'Monthly' : 'As agreed',
    PremiumCharge: premiumCharge,
    PremiumChargeRequired: fields.premiumChargeRequired || 'No',
    AnnualGroundRent: annualGroundRent,
    AnnualGroundRentRequired: fields.groundRentRequired || 'No',
    Currency: fields.currency || '',
    LeaseTerm: fields.requestedLeaseTerm || '',
    MoveInDate: moveInDate,
    AgreementStartDate: moveInDate,
    BillingStartDate: rentalApplication ? moveInDate : '',
    CustomerApprovalStatus: fields.customerAcceptanceStatus || '',
    CustomerApprovalDate: fields.customerAcceptanceDate || '',
    WorkflowReference: procedureCase.workflowInstanceId || '',
    NextApproverRole: procedureCase.currentAssignedRole || '',
    AgreementDmsReference: generatedAgreementReference,
    GeneratedAgreementReference: generatedAgreementReference,
  };
};

export function ListingApplicationWorkspace() {
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const { hasAnyRole } = useAuth();
  const requestedCaseId = searchParams.get('caseId');
  const routeCaseIdMatch = pathname.match(/\/cases\/([^/?#]+)/i);
  const routeCaseId = routeCaseIdMatch
    ? decodeURIComponent(routeCaseIdMatch[1])
    : null;
  const targetCaseId = routeCaseId || requestedCaseId;
  const detailOnly = Boolean(targetCaseId);
  const registerOnly = !routeCaseId && !requestedCaseId;
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [queuePage, setQueuePage] = React.useState(1);
  const [caseTotalCount, setCaseTotalCount] = React.useState(0);
  const [caseTotalPages, setCaseTotalPages] = React.useState(1);
  const [selectedCase, setSelectedCase] =
    React.useState<ProcedureCaseDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [completionNotes, setCompletionNotes] = React.useState('');
  const [dirtyStageFieldKeys, setDirtyStageFieldKeys] = React.useState<
    Set<string>
  >(() => new Set());
  const [saleCloseoutChecklist, setSaleCloseoutChecklist] = React.useState({
    customerStatus: false,
    auditReferences: false,
  });
  const [reviewAction, setReviewAction] = React.useState<
    'Reject' | 'RequestClarification' | null
  >(null);
  const [reviewReason, setReviewReason] = React.useState('');
  const [generationTemplates, setGenerationTemplates] = React.useState<
    CentralDocumentGenerationTemplate[]
  >([]);
  const [selectedTemplateCode, setSelectedTemplateCode] = React.useState('');
  const [generatedAgreement, setGeneratedAgreement] =
    React.useState<GeneratedCentralDocumentResult | null>(null);
  const [isAgreementViewerOpen, setIsAgreementViewerOpen] =
    React.useState(false);
  const [previewDocumentId, setPreviewDocumentId] = React.useState<
    string | null
  >(null);
  const [agreementRecord, setAgreementRecord] =
    React.useState<CentralDocumentRecord | null>(null);
  const [agreementWorkflowNotes, setAgreementWorkflowNotes] =
    React.useState('');
  const [isUpdatingAgreementWorkflow, setIsUpdatingAgreementWorkflow] =
    React.useState(false);
  const [activeAgreementWorkflowAction, setActiveAgreementWorkflowAction] =
    React.useState<'SubmitForApproval' | 'Approve' | 'Sign' | 'Return' | null>(
      null
    );
  const [agreementWorkflowStatus, setAgreementWorkflowStatus] = React.useState<{
    tone: 'progress' | 'success' | 'error';
    message: string;
  } | null>(null);
  const [selectedLegalMatterType, setSelectedLegalMatterType] =
    React.useState('termination');
  const [pendingLegalMatterType, setPendingLegalMatterType] = React.useState<
    string | null
  >(null);
  const [completedSaleOwnershipCaseIds, setCompletedSaleOwnershipCaseIds] =
    React.useState<Set<string>>(() => {
      if (typeof window === 'undefined') {
        return new Set();
      }

      try {
        const stored = window.localStorage.getItem(
          SALE_CLOSEOUT_COMPLETED_QUEUE_KEY
        );
        return new Set(stored ? (JSON.parse(stored) as string[]) : []);
      } catch {
        return new Set();
      }
    });
  const pendingLegalMatterRef = React.useRef<string | null>(null);
  const selectedCaseId = selectedCase?.id || '';
  const selectedAgreementReference = selectedCase
    ? caseFieldValue(selectedCase, 'generatedAgreementReference')
    : '';
  const rememberSaleOwnershipCompletion = React.useCallback(
    (caseId: string) => {
      setCompletedSaleOwnershipCaseIds((current) => {
        if (current.has(caseId)) {
          return current;
        }

        const next = new Set(current);
        next.add(caseId);
        if (typeof window !== 'undefined') {
          window.localStorage.setItem(
            SALE_CLOSEOUT_COMPLETED_QUEUE_KEY,
            JSON.stringify(Array.from(next))
          );
        }
        return next;
      });
    },
    []
  );

  const loadCases = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      if (detailOnly && targetCaseId) {
        const detail = await procedureCaseService.getCase(targetCaseId);
        setCases([]);
        setCaseTotalCount(0);
        setCaseTotalPages(1);
        setSelectedCase(detail);
        return;
      }

      const page = await procedureCaseService.listCasesPage(
        'PropertyManagement',
        ENTITY_TYPE,
        queuePage,
        REQUESTS_PER_PAGE
      );
      const data = page.items;
      setCases(data);
      setCaseTotalCount(page.totalCount);
      setCaseTotalPages(Math.max(1, page.totalPages || 1));
      const targetId = targetCaseId || (registerOnly ? null : data[0]?.id);
      setSelectedCase(
        targetId ? await procedureCaseService.getCase(targetId) : null
      );
    } catch (loadError) {
      setError(errorMessage(loadError, 'Unable to load property requests.'));
    } finally {
      setIsLoading(false);
    }
  }, [
    completedSaleOwnershipCaseIds,
    detailOnly,
    rememberSaleOwnershipCompletion,
    queuePage,
    registerOnly,
    targetCaseId,
  ]);

  React.useEffect(() => {
    void loadCases();
  }, [loadCases]);

  React.useEffect(() => {
    setSaleCloseoutChecklist({
      customerStatus: false,
      auditReferences: false,
    });
    setDirtyStageFieldKeys(new Set());
  }, [selectedCaseId]);

  React.useEffect(() => {
    let mounted = true;

    const loadAgreementRecord = async () => {
      if (!selectedCaseId) {
        setAgreementRecord(null);
        return;
      }

      try {
        const records = await documentManagementService.getRecords('Estate');
        if (!mounted) return;
        const matchingRecords = records.filter(
          (record) =>
            record.sourceRecordId === selectedCaseId ||
            record.documentReference === selectedAgreementReference
        );
        setAgreementRecord(
          matchingRecords.sort((left, right) =>
            (right.updatedAt || right.createdAt).localeCompare(
              left.updatedAt || left.createdAt
            )
          )[0] || null
        );
      } catch {
        if (mounted) setAgreementRecord(null);
      }
    };

    void loadAgreementRecord();
    return () => {
      mounted = false;
    };
  }, [selectedAgreementReference, selectedCaseId]);

  React.useEffect(() => {
    if (!selectedCase) {
      setGenerationTemplates([]);
      setSelectedTemplateCode('');
      return;
    }

    let mounted = true;

    const loadTemplates = async () => {
      try {
        const templates =
          await documentManagementService.getGenerationTemplates('Estate');
        if (!mounted) return;
        const rental = isRentalApplication(selectedCase);
        const lease = isLeaseApplication(selectedCase);
        const applicableTemplates = templates.filter((template) => {
          const value =
            `${template.templateCode} ${template.title} ${template.documentType}`.toLowerCase();
          if (!value.includes('agreement')) return false;
          return lease
            ? value.includes('lease')
            : rental
              ? (value.includes('tenancy') || value.includes('rent')) &&
                !value.includes('lease')
              : value.includes('sale') || value.includes('purchase');
        });
        setGenerationTemplates(applicableTemplates);
        const preferredCode = lease
          ? 'EST-LEASE-AGREEMENT'
          : rental
            ? 'EST-RENT-AGREEMENT'
            : 'EST-SALE-AGREEMENT';
        setSelectedTemplateCode((current) =>
          applicableTemplates.some(
            (template) => template.templateCode === current
          )
            ? current
            : applicableTemplates.find(
                (template) => template.templateCode === preferredCode
              )?.templateCode ||
              applicableTemplates[0]?.templateCode ||
              ''
        );
      } catch {
        setGenerationTemplates([]);
      }
    };

    void loadTemplates();
    return () => {
      mounted = false;
    };
  }, [selectedCase?.id]);

  React.useEffect(() => {
    if (!selectedCase || generationTemplates.length === 0) {
      return;
    }

    const savedTemplateCode = caseFieldValue(
      selectedCase,
      'agreementTemplateReference'
    );
    if (
      savedTemplateCode &&
      generationTemplates.some(
        (template) => template.templateCode === savedTemplateCode
      )
    ) {
      setSelectedTemplateCode(savedTemplateCode);
    }
  }, [generationTemplates, selectedCase]);

  React.useEffect(() => {
    setQueuePage((current) => Math.min(current, caseTotalPages));
  }, [caseTotalPages]);

  const selectCase = async (caseId: string) => {
    setIsSaving(true);
    setError(null);
    try {
      const detail = await procedureCaseService.getCase(caseId);
      setSelectedCase(detail);
      setCompletionNotes('');
    } catch (selectError) {
      setError(
        errorMessage(selectError, 'Unable to open the property request.')
      );
    } finally {
      setIsSaving(false);
    }
  };

  const updateField = (key: string, value: string) => {
    setDirtyStageFieldKeys((current) => {
      const next = new Set(current);
      next.add(key);
      return next;
    });
    setSelectedCase((current) =>
      current
        ? {
            ...current,
            fields: current.fields.map((field) =>
              field.key === key ? { ...field, value } : field
            ),
          }
        : current
    );
  };

  const saveCurrentStage = async () => {
    if (!selectedCase) return;

    setIsSaving(true);
    setError(null);
    try {
      const editableKeys = editableFieldKeys(selectedCase);
      const fieldValues = Object.fromEntries(
        selectedCase.fields
          .filter((field) => editableKeys.has(field.key))
          .map((field) => [field.key, field.value ?? null])
      );
      setSelectedCase(
        await procedureCaseService.updateFields(selectedCase.id, {
          fieldValues,
          referenceNumber: selectedCase.referenceNumber,
          applicantName: selectedCase.applicantName,
          sourceDepartment: selectedCase.sourceDepartment,
          receivedDate: selectedCase.receivedDate,
          description: selectedCase.description,
        })
      );
      setDirtyStageFieldKeys(new Set());
      toast.success('Stage updates saved.');
    } catch (saveError) {
      const message = errorMessage(
        saveError,
        'Unable to save the current stage.'
      );
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const updateChecklist = async (itemId: string, checked: boolean) => {
    if (!selectedCase) return;

    setIsSaving(true);
    setError(null);
    try {
      setSelectedCase(
        await procedureCaseService.updateChecklistItem(
          selectedCase.id,
          itemId,
          checked
        )
      );
    } catch (checkError) {
      setError(
        errorMessage(checkError, 'Unable to update stage confirmation.')
      );
    } finally {
      setIsSaving(false);
    }
  };

  const completeStage = async () => {
    if (!selectedCase) return;

    if (dirtyStageFieldKeys.size > 0) {
      setError(
        'Save the current stage updates before routing this request forward.'
      );
      return;
    }

    const missingStageFields = requiredStageFieldKeys(selectedCase)
      .filter((key) => !isStageFieldComplete(selectedCase, key))
      .map((key) => fieldLabel(selectedCase, key));
    if (missingStageFields.length > 0) {
      setError(
        `Complete the required current-stage field(s) before routing forward: ${missingStageFields
          .slice(0, 4)
          .join(
            ', '
          )}${missingStageFields.length > 4 ? ` and ${missingStageFields.length - 4} more` : ''}.`
      );
      return;
    }

    if (
      isPremiumChargeRequired(selectedCase) &&
      selectedCase.currentStageIndex >= 1 &&
      !(caseMoneyValue(selectedCase, 'premiumChargeAmount') > 0)
    ) {
      setError(
        'Enter and save the premium charge amount before routing this request forward.'
      );
      return;
    }

    if (
      isPremiumChargeRequired(selectedCase) &&
      selectedCase.currentStageIndex >= 2 &&
      !isPremiumChargeSettled(selectedCase)
    ) {
      setError(
        'Premium charge payment is pending. Wait for Finance payment confirmation before routing this request forward.'
      );
      return;
    }

    const approved = isApprovedDecision(
      caseFieldValue(selectedCase, 'decisionStatus')
    );
    const stageName = selectedCase.currentStageName.trim().toLowerCase();
    const agreementSigningLocation = caseFieldValue(
      selectedCase,
      'agreementSigningLocation'
    );
    const legalAgreementReviewComplete =
      isLegalAgreementReviewCompleteForSigningLocation(
        caseFieldValue(selectedCase, 'legalAgreementReviewStatus'),
        agreementSigningLocation
      );
    if (
      stageName === 'commercial and availability review' &&
      isPremiumChargeRequired(selectedCase) &&
      !(caseMoneyValue(selectedCase, 'premiumChargeAmount') > 0)
    ) {
      setError('Enter the premium charge amount before continuing.');
      return;
    }
    if (
      stageName === 'estate decision and agreement' &&
      approved &&
      isRentalApplication(selectedCase) &&
      !caseFieldValue(selectedCase, 'moveInDate')
    ) {
      setError(
        'Set the approved move-in date before generating the agreement.'
      );
      return;
    }
    if (
      stageName === 'estate decision and agreement' &&
      approved &&
      isLeaseApplication(selectedCase) &&
      !caseFieldValue(selectedCase, 'requestedLeaseTerm')
    ) {
      setError('Set the approved lease term before generating the agreement.');
      return;
    }
    if (
      stageName === 'estate decision and agreement' &&
      approved &&
      isPremiumChargeRequired(selectedCase) &&
      !isPremiumChargeSettled(selectedCase)
    ) {
      setError(
        'The premium charge must be paid in Finance before agreement generation can continue.'
      );
      return;
    }
    if (
      (stageName === 'management decision' ||
        stageName === 'estate decision and agreement') &&
      approved &&
      !caseFieldValue(selectedCase, 'generatedAgreementReference')
    ) {
      setError('Generate the agreement before routing to Legal review.');
      return;
    }
    if (
      stageName === 'legal agreement review' &&
      !caseFieldValue(selectedCase, 'generatedAgreementReference')
    ) {
      setError('Generate the agreement before completing Legal review.');
      return;
    }
    if (
      (stageName === 'management decision' ||
        stageName === 'legal agreement review') &&
      approved &&
      !legalAgreementReviewComplete
    ) {
      setError(
        agreementSigningLocation.toLowerCase().includes('estate')
          ? 'Legal must approve and release the agreement before Estate can handle customer execution.'
          : 'Legal must receive the customer-signed agreement and complete the Head of Legal signature before Estate can continue.'
      );
      return;
    }
    if (
      stageName === 'customer agreement execution' &&
      !caseFieldValue(selectedCase, 'signedAgreementReference')
    ) {
      setError('The customer must sign and submit the agreement first.');
      return;
    }
    if (
      stageName === 'customer agreement execution' &&
      (caseFieldValue(
        selectedCase,
        'agreementExecutionStatus'
      ).toLowerCase() !== 'fully executed' ||
        !caseFieldValue(selectedCase, 'finalSignedAgreementReference'))
    ) {
      setError(
        'Complete internal approval and digital signature in DMS before leaving customer agreement execution.'
      );
      return;
    }
    if (stageName === 'payment, billing and finance check') {
      if (
        isRentalApplication(selectedCase) &&
        !isLeaseApplication(selectedCase)
      ) {
        if (!isSalePaymentSatisfied(selectedCase)) {
          setError(
            'Collect and confirm the remaining first month rent before completing this stage.'
          );
          return;
        }
        if (
          !caseFieldValue(selectedCase, 'billingStartDate') ||
          !containsAny(caseFieldValue(selectedCase, 'billingStartStatus'), [
            'Ready for billing',
            'Billing active',
            'Rent billing activated',
          ])
        ) {
          setError(
            'Confirm rent billing readiness before completing this stage.'
          );
          return;
        }
      } else if (!isSalePaymentSatisfied(selectedCase)) {
        setError(
          'Complete the Estate sale or lease balance payment check before moving to Legal conveyance.'
        );
        return;
      }
    }
    if (stageName === 'legal conveyance or lease follow-up') {
      if (isRentalApplication(selectedCase)) {
        if (
          !containsAny(caseFieldValue(selectedCase, 'moveInEffectiveStatus'), [
            'Effective',
            'Move-in complete',
            'Handover complete',
          ])
        ) {
          setError(
            'Confirm the executed lease and move-in readiness before Estate closeout.'
          );
          return;
        }
      } else {
        if (
          caseFieldValue(
            selectedCase,
            'legalConveyanceStatus'
          ).toLowerCase() !== 'completed by legal'
        ) {
          setError(
            'Legal must complete conveyance and registration before Estate closeout.'
          );
          return;
        }

        if (
          !containsAny(
            caseFieldValue(selectedCase, 'ownershipTransferStatus'),
            ['Completed']
          )
        ) {
          setError('Complete ownership transfer before Estate closeout.');
          return;
        }
      }
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.completeStage(
        selectedCase.id,
        completionNotes || null
      );
      setSelectedCase(updated);
      setCompletionNotes('');
      setCases(
        await procedureCaseService.listCases('PropertyManagement', ENTITY_TYPE)
      );
      toast.success(
        updated.status.trim().toLowerCase() === 'completed'
          ? 'Application workflow completed.'
          : `Stage routed to ${updated.currentStageName}.`
      );
    } catch (completeError) {
      const message = errorMessage(
        completeError,
        'Unable to complete the stage.'
      );
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const applyReviewAction = async () => {
    if (!selectedCase || !reviewAction || !reviewReason.trim()) return;

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.applyReviewAction(
        selectedCase.id,
        reviewAction,
        reviewReason.trim()
      );
      setSelectedCase(updated);
      setCases(
        await procedureCaseService.listCases('PropertyManagement', ENTITY_TYPE)
      );
      toast.success(
        reviewAction === 'Reject'
          ? 'Application rejected and customer notified.'
          : 'Application returned for clarification and customer notified.'
      );
      setReviewAction(null);
      setReviewReason('');
    } catch (reviewError) {
      setError(errorMessage(reviewError, 'Unable to apply the review action.'));
    } finally {
      setIsSaving(false);
    }
  };

  const generateAgreement = async () => {
    if (!selectedCase || !selectedTemplateCode) return;

    if (!isApprovedDecision(caseFieldValue(selectedCase, 'decisionStatus'))) {
      setError(
        'Set the decision status to Approved before generating the agreement.'
      );
      return;
    }

    const rentalApplication = isRentalApplication(selectedCase);
    const leaseApplication = isLeaseApplication(selectedCase);
    const moveInDate = caseFieldValue(selectedCase, 'moveInDate');
    const requestedLeaseTerm = caseFieldValue(
      selectedCase,
      'requestedLeaseTerm'
    );
    if (rentalApplication && !moveInDate) {
      setError(
        'Set the approved move-in date before generating the rental agreement.'
      );
      return;
    }
    if (leaseApplication && !requestedLeaseTerm) {
      setError(
        'Set the approved lease term before generating the lease agreement.'
      );
      return;
    }
    if (
      isPremiumChargeRequired(selectedCase) &&
      !isPremiumChargeSettled(selectedCase)
    ) {
      setError(
        'The premium charge must be paid in Finance before generating the agreement.'
      );
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const result =
        await documentManagementService.generateDocumentFromTemplate({
          templateCode: selectedTemplateCode,
          sourceModule: 'Estate',
          sourceLabel: 'Property Management listing application',
          sourceEntityType: selectedCase.entityType,
          sourceRecordReference:
            selectedCase.referenceNumber || selectedCase.title,
          sourceRecordId: selectedCase.id,
          caseTitle: selectedCase.title,
          caseReference: selectedCase.referenceNumber || '',
          applicantName: selectedCase.applicantName || '',
          purpose: rentalApplication
            ? 'Lease / tenancy agreement generation'
            : 'Property sale agreement generation',
          mergeValues: buildAgreementMergeValues(
            selectedCase,
            rentalApplication,
            moveInDate
          ),
        });

      const editableKeys = editableFieldKeys(selectedCase);
      const updatedFields = selectedCase.fields.map((field) => {
        if (field.key === 'agreementTemplateReference') {
          return { ...field, value: selectedTemplateCode };
        }
        if (field.key === 'generatedAgreementReference') {
          return { ...field, value: result.dmsReference };
        }
        if (field.key === 'billingStartDate' && rentalApplication) {
          return { ...field, value: moveInDate };
        }
        if (field.key === 'billingStartStatus') {
          return {
            ...field,
            value: rentalApplication
              ? 'Blocked - signature pending'
              : 'Not applicable',
          };
        }
        return field;
      });
      const fieldValues = Object.fromEntries(
        updatedFields
          .filter(
            (field) =>
              editableKeys.has(field.key) ||
              field.key === 'agreementTemplateReference' ||
              field.key === 'generatedAgreementReference' ||
              field.key === 'billingStartDate' ||
              field.key === 'billingStartStatus'
          )
          .map((field) => [field.key, field.value ?? null])
      );
      const updated = await procedureCaseService.updateFields(selectedCase.id, {
        fieldValues,
        referenceNumber: selectedCase.referenceNumber,
        applicantName: selectedCase.applicantName,
        sourceDepartment: selectedCase.sourceDepartment,
        receivedDate: selectedCase.receivedDate,
        description: selectedCase.description,
      });
      setGeneratedAgreement(result);
      setAgreementRecord(result.record);
      setSelectedCase(updated);
    } catch (generateError) {
      setError(
        errorMessage(generateError, 'Unable to generate the agreement.')
      );
    } finally {
      setIsSaving(false);
    }
  };

  const downloadAgreementPdf = async () => {
    if (!generatedAgreement) return;

    setIsSaving(true);
    setError(null);
    try {
      const blob = await documentManagementService.downloadVersionFile(
        generatedAgreement.record.id,
        generatedAgreement.version.id,
        'pdf'
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${generatedAgreement.dmsReference}-agreement.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      setError(
        errorMessage(downloadError, 'Unable to download the agreement PDF.')
      );
    } finally {
      setIsSaving(false);
    }
  };

  const downloadCaseDocument = async (document: ProcedureCaseDocument) => {
    if (!selectedCase || !document.fileUrl) return;

    setIsSaving(true);
    setError(null);
    try {
      const blob = await procedureCaseService.downloadDocumentContent(
        selectedCase.id,
        document.id
      );
      const url = URL.createObjectURL(blob);
      const link = window.document.createElement('a');
      link.href = url;
      link.download = document.fileName || 'signed-agreement.pdf';
      window.document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      setError(
        errorMessage(downloadError, 'Unable to download the uploaded document.')
      );
    } finally {
      setIsSaving(false);
    }
  };

  const updateAgreementWorkflow = async (
    action: 'SubmitForApproval' | 'Approve' | 'Sign' | 'Return'
  ) => {
    if (!selectedCase || !agreementRecord) return;

    const canManageApproval = hasAnyRole([
      'Property Manager',
      'Estate Manager',
      'Head of Estate',
    ]);
    const canSignAgreement = hasAnyRole([
      'Executive Approver',
      'Authorised Signatory',
      'Managing Director',
    ]);
    if ((action === 'Approve' || action === 'Return') && !canManageApproval) {
      toast.error('A Property or Estate Manager must perform this action.');
      return;
    }
    if (action === 'Sign' && !canSignAgreement) {
      toast.error(
        'Log in as an Executive Approver to digitally sign the agreement.'
      );
      return;
    }

    setIsUpdatingAgreementWorkflow(true);
    setActiveAgreementWorkflowAction(action);
    setAgreementWorkflowStatus({
      tone: 'progress',
      message:
        action === 'Sign'
          ? 'Digitally signing the agreement. This may take up to a minute.'
          : 'Updating the agreement workflow...',
    });
    setError(null);
    try {
      const template = generationTemplates.find(
        (item) =>
          item.templateCode ===
          caseFieldValue(selectedCase, 'agreementTemplateReference')
      );
      const result =
        await documentManagementService.updateGeneratedDocumentWorkflow(
          agreementRecord.id,
          {
            action,
            notes: agreementWorkflowNotes.trim() || undefined,
            signatureRole:
              template?.signatureRole || 'Authorised Property Signatory',
          }
        );
      setAgreementRecord(result.record);
      setSelectedCase(await procedureCaseService.getCase(selectedCase.id));
      setAgreementWorkflowNotes('');
      const successMessage =
        action === 'Approve'
          ? 'Agreement approved. Executive signature is now required.'
          : action === 'Sign'
            ? 'Agreement digitally signed and released to the customer.'
            : action === 'Return'
              ? 'Agreement returned for correction.'
              : 'Agreement submitted for approval.';
      setAgreementWorkflowStatus({ tone: 'success', message: successMessage });
      toast.success(successMessage);
    } catch (workflowError) {
      const message = errorMessage(
        workflowError,
        'Unable to update the agreement approval workflow.'
      );
      setError(message);
      setAgreementWorkflowStatus({ tone: 'error', message });
      toast.error(message);
    } finally {
      setIsUpdatingAgreementWorkflow(false);
      setActiveAgreementWorkflowAction(null);
    }
  };

  const lodgeLegalMatter = async (matterType: string) => {
    if (!selectedCase) return;
    if (pendingLegalMatterRef.current) return;
    pendingLegalMatterRef.current = matterType;
    setIsSaving(true);
    setPendingLegalMatterType(matterType);
    setError(null);
    try {
      const legalMatter = await procedureCaseService.createLinkedLegalMatter(
        selectedCase.id,
        matterType
      );
      const updated = await procedureCaseService.getCase(selectedCase.id);
      setSelectedCase(updated);
      toast.success(
        `Legal matter ${legalMatter.referenceNumber || legalMatter.title} was lodged.`
      );
    } catch (legalError) {
      const message = errorMessage(
        legalError,
        'Unable to lodge the matter with Legal.'
      );
      setError(message);
      toast.error(message);
    } finally {
      pendingLegalMatterRef.current = null;
      setIsSaving(false);
      setPendingLegalMatterType(null);
    }
  };

  const createSaleInvoice = async () => {
    if (!selectedCase) return;
    setIsSaving(true);
    setError(null);
    try {
      const result = await estatePropertyManagementService.createSaleInvoice(
        selectedCase.id
      );
      setSelectedCase(await procedureCaseService.getCase(selectedCase.id));
      toast.success(result.message);
    } catch (saleError) {
      const message = errorMessage(
        saleError,
        selectedCase &&
          isRentalApplication(selectedCase) &&
          !isLeaseApplication(selectedCase)
          ? 'Unable to create the first month rent balance invoice.'
          : 'Unable to create the sale invoice.'
      );
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const syncSalePaymentStatus = async () => {
    if (!selectedCase) return;
    setIsSaving(true);
    setError(null);
    try {
      const result =
        await estatePropertyManagementService.syncSalePaymentStatus(
          selectedCase.id
        );
      setSelectedCase(await procedureCaseService.getCase(selectedCase.id));
      toast.success(result.message);
    } catch (saleError) {
      const message = errorMessage(
        saleError,
        selectedCase &&
          isRentalApplication(selectedCase) &&
          !isLeaseApplication(selectedCase)
          ? 'Unable to refresh the first month rent payment status.'
          : 'Unable to refresh the sale payment status.'
      );
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const createPremiumChargeInvoice = async () => {
    if (!selectedCase) return;
    setIsSaving(true);
    setError(null);
    try {
      const result =
        await estatePropertyManagementService.createPremiumChargeInvoice(
          selectedCase.id
        );
      setSelectedCase(await procedureCaseService.getCase(selectedCase.id));
      toast.success(result.message);
    } catch (premiumError) {
      const message = errorMessage(
        premiumError,
        'Unable to create the premium charge invoice.'
      );
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const syncPremiumChargePaymentStatus = async () => {
    if (!selectedCase) return;
    setIsSaving(true);
    setError(null);
    try {
      const result =
        await estatePropertyManagementService.syncPremiumChargePaymentStatus(
          selectedCase.id
        );
      setSelectedCase(await procedureCaseService.getCase(selectedCase.id));
      toast.success(result.message);
    } catch (premiumError) {
      const message = errorMessage(
        premiumError,
        'Unable to refresh the premium charge payment status.'
      );
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const completeSaleOwnership = async () => {
    if (!selectedCase) return;
    setIsSaving(true);
    setError(null);
    try {
      const result =
        await estatePropertyManagementService.completeSaleOwnership(
          selectedCase.id
        );
      setSelectedCase(await procedureCaseService.getCase(selectedCase.id));
      toast.success(result.message);
    } catch (saleError) {
      const message = errorMessage(
        saleError,
        'Unable to complete the ownership transfer.'
      );
      setError(message);
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  const completeSaleCloseout = () => {
    if (!selectedCase) return;
    rememberSaleOwnershipCompletion(selectedCase.id);
    toast.success('Sale closeout completed.');
  };

  if (isLoading) {
    return (
      <Card>
        <CardContent className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading property requests
        </CardContent>
      </Card>
    );
  }

  const summaryFields = selectedCase?.fields.filter(
    (field) =>
      SUMMARY_FIELD_KEYS.includes(field.key) &&
      (field.key !== 'requestedLeaseTerm' || isLeaseApplication(selectedCase))
  );
  const editableFields = selectedCase
    ? selectedCase.fields.filter(
        (field) =>
          editableFieldKeys(selectedCase).has(field.key) &&
          (field.key !== 'moveInDate' || isRentalApplication(selectedCase)) &&
          (!PREMIUM_FIELD_KEYS.has(field.key) ||
            selectedCase.currentStageIndex >= 1)
      )
    : [];
  const stageItems =
    selectedCase?.checklistItems.filter(
      (item) => item.stageIndex === selectedCase.currentStageIndex
    ) ?? [];
  const stages = Array.from(
    new Map(
      (selectedCase?.checklistItems ?? [])
        .sort((left, right) => left.stageIndex - right.stageIndex)
        .map((item) => [item.stageIndex, item.stageName])
    ).entries()
  );
  const stageConfirmed = stageItems.every((item) => item.isCompleted);
  const caseIsCompleted = selectedCase ? isCompletedCase(selectedCase) : false;
  const requiredStageKeys = selectedCase
    ? requiredStageFieldKeys(selectedCase)
    : [];
  const missingRequiredStageFields = selectedCase
    ? requiredStageKeys
        .filter((key) => !isStageFieldComplete(selectedCase, key))
        .map((key) => fieldLabel(selectedCase, key))
    : [];
  const hasUnsavedStageUpdates = dirtyStageFieldKeys.size > 0;
  const approvedDecision = selectedCase
    ? isApprovedDecision(caseFieldValue(selectedCase, 'decisionStatus'))
    : false;
  const completionRequirements = propertyListingCompletionRequirements(
    selectedCase?.currentStageName
  );
  const rentalApplication = selectedCase
    ? isRentalApplication(selectedCase)
    : false;
  const leaseApplication = selectedCase
    ? isLeaseApplication(selectedCase)
    : false;
  const missingApprovedMoveInDate = Boolean(
    selectedCase &&
      approvedDecision &&
      rentalApplication &&
      completionRequirements.requiresApprovedRentTerms &&
      !caseFieldValue(selectedCase, 'moveInDate')
  );
  const missingApprovedRentTerm = Boolean(
    selectedCase &&
      approvedDecision &&
      leaseApplication &&
      completionRequirements.requiresApprovedRentTerms &&
      !caseFieldValue(selectedCase, 'requestedLeaseTerm')
  );
  const premiumChargeRequired = Boolean(
    selectedCase && isPremiumChargeRequired(selectedCase)
  );
  const premiumChargeSettled = Boolean(
    selectedCase && isPremiumChargeSettled(selectedCase)
  );
  const premiumChargeAmount = selectedCase
    ? caseMoneyValue(selectedCase, 'premiumChargeAmount')
    : Number.NaN;
  const premiumChargeBalance = selectedCase
    ? caseMoneyValue(selectedCase, 'premiumChargeBalance')
    : Number.NaN;
  const premiumChargeInvoiceId = selectedCase
    ? caseFieldValue(selectedCase, 'premiumChargeInvoiceId')
    : '';
  const premiumChargeInvoiceReference = selectedCase
    ? caseFieldValue(selectedCase, 'premiumChargeInvoiceReference')
    : '';
  const premiumChargePaymentStatus = selectedCase
    ? caseFieldValue(selectedCase, 'premiumChargePaymentStatus') ||
      (premiumChargeInvoiceId ? 'Payment pending' : 'Pending invoice')
    : '';
  const premiumChargeCurrency = selectedCase
    ? caseFieldValue(selectedCase, 'currency') || 'GHS'
    : 'GHS';
  const missingPremiumChargePayment = Boolean(
    selectedCase &&
      premiumChargeRequired &&
      selectedCase.currentStageIndex >= 2 &&
      !premiumChargeSettled
  );
  const missingPremiumChargeAmount = Boolean(
    selectedCase &&
      premiumChargeRequired &&
      selectedCase.currentStageIndex >= 1 &&
      !(premiumChargeAmount > 0)
  );
  const missingApprovedAgreement = Boolean(
    selectedCase &&
      approvedDecision &&
      completionRequirements.requiresGeneratedAgreement &&
      !caseFieldValue(selectedCase, 'generatedAgreementReference')
  );
  const agreementAlreadyGenerated = Boolean(
    selectedCase &&
      (caseFieldValue(selectedCase, 'generatedAgreementReference') ||
        agreementRecord)
  );
  const showAgreementGeneration = Boolean(
    selectedCase &&
      approvedDecision &&
      (selectedCase.currentStageIndex >= 2 || agreementAlreadyGenerated)
  );
  const legalAgreementReviewStatus = selectedCase
    ? caseFieldValue(selectedCase, 'legalAgreementReviewStatus')
    : '';
  const agreementSigningLocation = selectedCase
    ? caseFieldValue(selectedCase, 'agreementSigningLocation')
    : '';
  const customerAgreementSigningInLegal = !agreementSigningLocation
    .toLowerCase()
    .includes('estate');
  const legalAgreementReviewStarted = Boolean(
    selectedCase &&
      (caseFieldValue(selectedCase, 'legalAgreementReviewCaseId') ||
        caseFieldValue(selectedCase, 'legalAgreementReviewReference') ||
        legalAgreementReviewStatus)
  );
  const legalAgreementReviewSubmitting =
    pendingLegalMatterType === 'agreementReview';
  const legalAgreementReleasedForCustomer = isLegalAgreementReviewSigned(
    legalAgreementReviewStatus
  );
  const legalAgreementReviewComplete =
    isLegalAgreementReviewCompleteForSigningLocation(
      legalAgreementReviewStatus,
      agreementSigningLocation
    );
  const customerAgreementAccepted = Boolean(
    selectedCase &&
      caseFieldValue(selectedCase, 'customerAcceptanceStatus').toLowerCase() ===
        'accepted'
  );
  const missingLegalAgreementReview = Boolean(
    selectedCase &&
      approvedDecision &&
      completionRequirements.requiresLegalAgreementReview &&
      !legalAgreementReviewComplete
  );
  const fullyExecuted = Boolean(
    selectedCase &&
      caseFieldValue(selectedCase, 'agreementExecutionStatus').toLowerCase() ===
        'fully executed'
  );
  const legalConveyanceStarted = Boolean(
    selectedCase && caseFieldValue(selectedCase, 'legalConveyanceCaseId')
  );
  const legalConveyanceCompleted = Boolean(
    selectedCase &&
      caseFieldValue(selectedCase, 'legalConveyanceStatus').toLowerCase() ===
        'completed by legal'
  );
  const saleInvoiceId = selectedCase
    ? caseFieldValue(selectedCase, 'saleInvoiceId')
    : '';
  const salePaymentStatus = selectedCase
    ? caseFieldValue(selectedCase, 'salePaymentStatus').toLowerCase()
    : '';
  const saleInvoiceBalance = selectedCase
    ? caseMoneyValue(selectedCase, 'saleInvoiceBalance')
    : Number.NaN;
  const estateRemainingAmount = selectedCase
    ? caseMoneyValue(selectedCase, 'estateRemainingAmount')
    : Number.NaN;
  const saleFullyPaidInSales = Boolean(
    selectedCase &&
      salePaymentStatus === 'paid in full' &&
      Number.isFinite(estateRemainingAmount) &&
      estateRemainingAmount <= 0
  );
  const saleInvoicePaid = Boolean(
    selectedCase &&
      (saleFullyPaidInSales ||
        (salePaymentStatus === 'paid in full' &&
          Number.isFinite(saleInvoiceBalance) &&
          saleInvoiceBalance <= 0))
  );
  const canCompleteSaleOwnership = saleInvoicePaid && legalConveyanceCompleted;
  const ownershipTransferCompleted = Boolean(
    selectedCase &&
      caseFieldValue(selectedCase, 'ownershipTransferStatus')
        .toLowerCase()
        .startsWith('completed')
  );
  const saleWorkflowCompleted = Boolean(
    selectedCase && caseIsCompleted && !rentalApplication
  );
  const saleCloseoutArchived = Boolean(
    selectedCase && completedSaleOwnershipCaseIds.has(selectedCase.id)
  );
  const saleCloseoutConfirmed =
    saleCloseoutChecklist.customerStatus &&
    saleCloseoutChecklist.auditReferences;
  const selectedStatusLabel =
    selectedCase &&
    selectedCase.status.trim().toLowerCase() === 'completed' &&
    !rentalApplication &&
    !saleCloseoutArchived
      ? ownershipTransferCompleted
        ? 'Ready for sale closeout'
        : 'Pending ownership transfer'
      : selectedCase?.status;
  const hasCustomerSignedAgreement = Boolean(
    selectedCase?.documents.some(
      (document) =>
        document.fileUrl &&
        document.name.trim().toLowerCase() === 'signed property agreement'
    )
  );
  const hasActiveCustomerSignedAgreement = Boolean(
    hasCustomerSignedAgreement &&
      selectedCase &&
      caseFieldValue(selectedCase, 'signedAgreementReference')
  );
  const agreementLifecycle =
    agreementRecord?.lifecycleStatus.trim().toLowerCase() || '';
  const canManageAgreementApproval = hasAnyRole([
    'Property Manager',
    'Estate Manager',
    'Head of Estate',
  ]);
  const canDigitallySignAgreement = hasAnyRole([
    'Executive Approver',
    'Authorised Signatory',
    'Managing Director',
  ]);

  return (
    <>
      <AlertDialog
        open={reviewAction !== null}
        onOpenChange={(open) => {
          if (!open && !isSaving) {
            setReviewAction(null);
            setReviewReason('');
          }
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {reviewAction === 'Reject'
                ? 'Reject property application?'
                : 'Return application for clarification?'}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {reviewAction === 'Reject'
                ? 'This closes the application and informs the customer.'
                : 'This pauses the current review stage and asks the customer for additional information.'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <Textarea
            value={reviewReason}
            onChange={(event) => setReviewReason(event.target.value)}
            placeholder="Reason required"
            disabled={isSaving}
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isSaving}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className={
                reviewAction === 'Reject'
                  ? 'bg-red-600 text-white hover:bg-red-700'
                  : undefined
              }
              disabled={isSaving || !reviewReason.trim()}
              onClick={(event) => {
                event.preventDefault();
                void applyReviewAction();
              }}
            >
              {isSaving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              {reviewAction === 'Reject'
                ? 'Reject application'
                : 'Request clarification'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <CentralDocumentViewerDialog
        open={isAgreementViewerOpen}
        onOpenChange={setIsAgreementViewerOpen}
        enableAnnotations={false}
        file={
          generatedAgreement
            ? {
                documentRecordId: generatedAgreement.record.id,
                versionId: generatedAgreement.version.id,
                fileUploadRecordId:
                  generatedAgreement.version.fileUploadRecordId,
                title: generatedAgreement.record.title,
                fileName: `${generatedAgreement.dmsReference}-agreement.pdf`,
                repositoryPath: generatedAgreement.version.repositoryPath,
                renditionPath:
                  generatedAgreement.version.renditionPath ||
                  generatedAgreement.pdfUrl,
                contentType: 'application/pdf',
                sourceLabel: generatedAgreement.sourceLabel,
                version: generatedAgreement.version.versionNumber,
              }
            : null
        }
      />
      <div className="space-y-4">
        {error ? (
          <div className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        ) : null}

        <div className="space-y-4">
          {!detailOnly ? (
            <Card>
              <CardHeader>
                <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                  <CardTitle className="flex items-center gap-2 text-base">
                    <ClipboardCheck className="h-4 w-4" />
                    Request queue
                  </CardTitle>
                  <Badge variant="outline">
                    {caseTotalCount} case{caseTotalCount === 1 ? '' : 's'}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-2">
                {cases.length === 0 ? (
                  <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                    Customer bids and rental requests will appear here after
                    they are submitted from a published listing.
                  </p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[900px] text-sm">
                      <thead className="border-b bg-muted/40 text-left text-xs uppercase tracking-wide text-muted-foreground">
                        <tr>
                          <th className="px-3 py-2 font-medium">Reference</th>
                          <th className="px-3 py-2 font-medium">Customer</th>
                          <th className="px-3 py-2 font-medium">Request</th>
                          <th className="px-3 py-2 font-medium">Stage</th>
                          <th className="px-3 py-2 font-medium">Status</th>
                          <th className="px-3 py-2 text-right font-medium">
                            Action
                          </th>
                        </tr>
                      </thead>
                      <tbody className="divide-y">
                        {cases.map((item) => (
                          <tr key={item.id} className="bg-background">
                            <td className="px-3 py-3 align-top">
                              <div className="font-medium">
                                {item.referenceNumber || item.title}
                              </div>
                              <div className="mt-1 max-w-[20rem] truncate text-xs text-muted-foreground">
                                {summaryFieldValue(item, 'listingReference') ||
                                  summaryFieldValue(item, 'propertyUnit') ||
                                  item.title}
                              </div>
                            </td>
                            <td className="px-3 py-3 align-top text-muted-foreground">
                              {item.applicantName ||
                                summaryFieldValue(item, 'customerName') ||
                                'Customer'}
                            </td>
                            <td className="px-3 py-3 align-top">
                              <Badge variant="secondary">
                                {requestTypeLabel(item)}
                              </Badge>
                            </td>
                            <td className="px-3 py-3 align-top">
                              <Badge variant="outline">
                                {item.currentStageName}
                              </Badge>
                            </td>
                            <td className="px-3 py-3 align-top">
                              <div className="flex flex-wrap gap-1">
                                <Badge
                                  variant="outline"
                                  className={getStatusBadgeClassName(
                                    queueStatusLabel(item)
                                  )}
                                >
                                  {queueStatusLabel(item)}
                                </Badge>
                                {!item.usesConfiguredWorkflow ? (
                                  <Badge variant="secondary">Manual</Badge>
                                ) : null}
                              </div>
                            </td>
                            <td className="px-3 py-3 text-right align-top">
                              <Button asChild size="sm" variant="outline">
                                <Link
                                  href={`/estate/property-management/${ENTITY_TYPE}/cases/${encodeURIComponent(item.id)}`}
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
                {caseTotalCount > REQUESTS_PER_PAGE ? (
                  <Pagination
                    currentPage={queuePage}
                    totalPages={caseTotalPages}
                    totalItems={caseTotalCount}
                    pageSize={REQUESTS_PER_PAGE}
                    onPageChange={setQueuePage}
                  />
                ) : null}
              </CardContent>
            </Card>
          ) : null}

          {registerOnly ? null : !selectedCase ? (
            <Card>
              <CardContent className="py-16 text-center text-sm text-muted-foreground">
                The selected property request could not be opened.
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-4">
              {!selectedCase.usesConfiguredWorkflow ? (
                <Card className="border-amber-400/50 bg-amber-50/70 dark:bg-amber-950/20">
                  <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <p className="font-medium">Manual request flow</p>
                      <p className="text-sm text-muted-foreground">
                        No active published workflow is configured for this
                        request type. Estate can continue it through the manual
                        review stages while Workflow Setup is completed.
                      </p>
                    </div>
                    <Button asChild variant="outline" size="sm">
                      <Link href="/administration/workflow?entityType=EstatePropertyManagementListingApplication">
                        <Settings2 className="mr-2 h-4 w-4" />
                        Open workflow setup
                      </Link>
                    </Button>
                  </CardContent>
                </Card>
              ) : null}
              <Card>
                <CardHeader className="space-y-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <CardTitle className="text-lg">
                        {selectedCase.referenceNumber || selectedCase.title}
                      </CardTitle>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {selectedCase.applicantName || 'Customer request'}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge
                        variant="outline"
                        className={getStatusBadgeClassName(selectedStatusLabel)}
                      >
                        {selectedStatusLabel}
                      </Badge>
                      <Badge variant="outline">
                        {selectedCase.currentAssignedRole || 'Unassigned'}
                      </Badge>
                    </div>
                  </div>

                  {stages.length > 0 ? (
                    <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-5">
                      {stages.map(([index, name], position) => {
                        const isCurrent =
                          index === selectedCase.currentStageIndex;
                        const isComplete =
                          index < selectedCase.currentStageIndex ||
                          selectedCase.status === 'Completed';
                        return (
                          <div
                            key={`${index}-${name}`}
                            className={`rounded-md border px-3 py-2 text-xs ${
                              isCurrent
                                ? 'border-primary bg-primary/5 text-foreground'
                                : 'border-border text-muted-foreground'
                            }`}
                          >
                            <div className="mb-1 flex items-center gap-1 font-medium">
                              {isComplete ? (
                                <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
                              ) : null}
                              Stage {position + 1}
                            </div>
                            {name}
                          </div>
                        );
                      })}
                    </div>
                  ) : null}
                </CardHeader>
              </Card>

              <Tabs defaultValue="request" className="space-y-4">
                <TabsList className="flex h-auto flex-wrap justify-start">
                  <TabsTrigger value="request">Request</TabsTrigger>
                  <TabsTrigger value="documents">Documents</TabsTrigger>
                  <TabsTrigger value="agreement">Agreement & legal</TabsTrigger>
                  <TabsTrigger value="complete">Complete</TabsTrigger>
                </TabsList>
                <TabsContent value="request" className="space-y-4">
                  <div className="grid gap-4 xl:grid-cols-2">
                    <Card>
                      <CardHeader>
                        <CardTitle className="text-base">
                          Submitted request
                        </CardTitle>
                      </CardHeader>
                      <CardContent className="grid gap-3 sm:grid-cols-2">
                        {summaryFields?.map((field) => (
                          <div
                            key={field.id}
                            className={
                              field.fieldType === 'textarea'
                                ? 'sm:col-span-2'
                                : ''
                            }
                          >
                            <p className="text-xs font-medium text-muted-foreground">
                              {field.label}
                            </p>
                            <p className="mt-1 whitespace-pre-wrap text-sm">
                              {formatValue(field)}
                            </p>
                          </div>
                        ))}
                      </CardContent>
                    </Card>

                    <Card>
                      <CardHeader>
                        <CardTitle className="text-base">
                          Current stage: {selectedCase.currentStageName}
                        </CardTitle>
                        <p className="text-sm text-muted-foreground">
                          Only the fields needed by this stage are shown.
                        </p>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        {editableFields?.length ? (
                          editableFields.map((field) => (
                            <div key={field.id} className="space-y-1.5">
                              <label
                                className="text-sm font-medium"
                                htmlFor={field.id}
                              >
                                {field.label}
                                {requiredStageKeys.includes(field.key)
                                  ? ' *'
                                  : ''}
                              </label>
                              {field.fieldType === 'select' &&
                              field.options?.length ? (
                                <Select
                                  value={field.value || undefined}
                                  onValueChange={(value) =>
                                    updateField(field.key, value)
                                  }
                                  disabled={
                                    isSaving ||
                                    caseIsCompleted ||
                                    !selectedCase.canEditCurrentStage
                                  }
                                >
                                  <SelectTrigger id={field.id}>
                                    <SelectValue placeholder="Select" />
                                  </SelectTrigger>
                                  <SelectContent>
                                    {field.options.map((option) => (
                                      <SelectItem key={option} value={option}>
                                        {option}
                                      </SelectItem>
                                    ))}
                                  </SelectContent>
                                </Select>
                              ) : field.fieldType === 'textarea' ? (
                                <Textarea
                                  id={field.id}
                                  value={field.value ?? ''}
                                  onChange={(event) =>
                                    updateField(field.key, event.target.value)
                                  }
                                  onInput={(event) =>
                                    updateField(
                                      field.key,
                                      event.currentTarget.value
                                    )
                                  }
                                  disabled={
                                    isSaving ||
                                    caseIsCompleted ||
                                    !selectedCase.canEditCurrentStage
                                  }
                                />
                              ) : (
                                <Input
                                  id={field.id}
                                  type={
                                    field.fieldType === 'date' ? 'date' : 'text'
                                  }
                                  value={field.value ?? ''}
                                  onChange={(event) =>
                                    updateField(field.key, event.target.value)
                                  }
                                  disabled={
                                    isSaving ||
                                    caseIsCompleted ||
                                    !selectedCase.canEditCurrentStage
                                  }
                                />
                              )}
                            </div>
                          ))
                        ) : (
                          <p className="text-sm text-muted-foreground">
                            This workflow stage has no configured fields.
                          </p>
                        )}

                        {editableFields?.length ? (
                          <Button
                            type="button"
                            variant="outline"
                            className="gap-2"
                            onClick={() => void saveCurrentStage()}
                            disabled={
                              isSaving ||
                              caseIsCompleted ||
                              !selectedCase.canEditCurrentStage
                            }
                          >
                            {isSaving ? (
                              <Loader2 className="h-4 w-4 animate-spin" />
                            ) : (
                              <Save className="h-4 w-4" />
                            )}
                            Save stage updates
                          </Button>
                        ) : null}
                      </CardContent>
                    </Card>
                  </div>
                </TabsContent>

                <TabsContent value="documents" className="space-y-4">
                  <Card>
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2 text-base">
                        <FileText className="h-4 w-4" />
                        Customer-submitted documents
                      </CardTitle>
                      <p className="text-sm text-muted-foreground">
                        {agreementAlreadyGenerated
                          ? 'Review the application evidence and any signed agreement returned by the customer.'
                          : 'Review the identity, eligibility, offer, and financing evidence submitted with this application.'}
                      </p>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      {selectedCase.documents.length === 0 ? (
                        <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                          No customer documents have been uploaded for this
                          request.
                        </p>
                      ) : (
                        selectedCase.documents.map((document) => (
                          <div
                            key={document.id}
                            className="rounded-md border p-3"
                          >
                            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                              <div className="min-w-0">
                                <p className="text-sm font-medium">
                                  {document.name}
                                </p>
                                <p className="mt-1 break-all text-xs text-muted-foreground">
                                  {document.fileName || 'File not attached'}
                                </p>
                                {document.uploadedAt ? (
                                  <p className="mt-1 text-xs text-muted-foreground">
                                    Uploaded{' '}
                                    {new Intl.DateTimeFormat('en-GB', {
                                      dateStyle: 'medium',
                                      timeStyle: 'short',
                                    }).format(new Date(document.uploadedAt))}
                                  </p>
                                ) : null}
                                {document.notes ? (
                                  <p className="mt-2 whitespace-pre-wrap text-sm">
                                    {document.notes}
                                  </p>
                                ) : null}
                              </div>
                              {document.fileUrl ? (
                                <div className="flex shrink-0 flex-wrap gap-2">
                                  <Button
                                    type="button"
                                    size="sm"
                                    variant="outline"
                                    onClick={() =>
                                      setPreviewDocumentId((current) =>
                                        current === document.id
                                          ? null
                                          : document.id
                                      )
                                    }
                                  >
                                    <Eye className="mr-2 h-4 w-4" />
                                    {previewDocumentId === document.id
                                      ? 'Hide PDF'
                                      : 'View PDF'}
                                  </Button>
                                  <Button
                                    type="button"
                                    size="sm"
                                    variant="outline"
                                    disabled={isSaving}
                                    onClick={() =>
                                      void downloadCaseDocument(document)
                                    }
                                  >
                                    <Download className="mr-2 h-4 w-4" />
                                    Download PDF
                                  </Button>
                                </div>
                              ) : null}
                            </div>
                            {previewDocumentId === document.id &&
                            document.fileUrl ? (
                              <div className="mt-3">
                                <ProcedurePdfViewer
                                  fileUrl={document.fileUrl}
                                  fileName={document.fileName}
                                />
                              </div>
                            ) : null}
                          </div>
                        ))
                      )}
                    </CardContent>
                  </Card>
                </TabsContent>

                <TabsContent value="agreement" className="space-y-4">
                  {premiumChargeRequired ? (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-base">
                          <Send className="h-4 w-4" />
                          Premium charge
                        </CardTitle>
                        <p className="text-sm text-muted-foreground">
                          Estate initiates the premium charge invoice, then
                          waits for Finance payment confirmation before the
                          agreement can be generated.
                        </p>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        <div className="grid gap-3 text-sm md:grid-cols-4">
                          <div>
                            <div className="text-xs text-muted-foreground">
                              Amount
                            </div>
                            <div className="mt-1 font-medium">
                              {formatMoney(
                                premiumChargeAmount,
                                premiumChargeCurrency
                              )}
                            </div>
                          </div>
                          <div>
                            <div className="text-xs text-muted-foreground">
                              Invoice
                            </div>
                            <div className="mt-1 font-medium">
                              {premiumChargeInvoiceReference || 'Not created'}
                            </div>
                          </div>
                          <div>
                            <div className="text-xs text-muted-foreground">
                              Payment
                            </div>
                            <Badge
                              variant={
                                premiumChargeSettled ? 'secondary' : 'outline'
                              }
                              className="mt-1"
                            >
                              {premiumChargePaymentStatus || 'Pending invoice'}
                            </Badge>
                          </div>
                          <div>
                            <div className="text-xs text-muted-foreground">
                              Balance
                            </div>
                            <div className="mt-1 font-medium">
                              {Number.isFinite(premiumChargeBalance)
                                ? formatMoney(
                                    premiumChargeBalance,
                                    premiumChargeCurrency
                                  )
                                : premiumChargeInvoiceId
                                  ? 'Awaiting Finance'
                                  : 'Not invoiced'}
                            </div>
                          </div>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          {premiumChargeInvoiceId ? (
                            <Button
                              type="button"
                              variant="outline"
                              className="gap-2"
                              disabled={isSaving || premiumChargeSettled}
                              onClick={() =>
                                void syncPremiumChargePaymentStatus()
                              }
                            >
                              {isSaving ? (
                                <Loader2 className="h-4 w-4 animate-spin" />
                              ) : (
                                <RefreshCw className="h-4 w-4" />
                              )}
                              {premiumChargeSettled
                                ? 'Premium paid'
                                : 'Refresh Finance payment'}
                            </Button>
                          ) : (
                            <Button
                              type="button"
                              className="gap-2"
                              disabled={
                                isSaving ||
                                premiumChargeSettled ||
                                !(premiumChargeAmount > 0)
                              }
                              onClick={() => void createPremiumChargeInvoice()}
                            >
                              {isSaving ? (
                                <Loader2 className="h-4 w-4 animate-spin" />
                              ) : (
                                <Send className="h-4 w-4" />
                              )}
                              Create premium invoice
                            </Button>
                          )}
                          {premiumChargeSettled ? (
                            <div className="flex items-center gap-2 text-sm font-medium text-emerald-700">
                              <CheckCircle2 className="h-4 w-4" />
                              Agreement can continue
                            </div>
                          ) : null}
                        </div>
                      </CardContent>
                    </Card>
                  ) : null}

                  {showAgreementGeneration ? (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-base">
                          <FileSignature className="h-4 w-4" />
                          Agreement generation
                        </CardTitle>
                        <p className="text-sm text-muted-foreground">
                          Set the approval details, generate the agreement, and
                          route it through Legal before customer execution.
                        </p>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        <div className="grid gap-3 md:grid-cols-[1fr_auto]">
                          <Select
                            value={selectedTemplateCode || undefined}
                            onValueChange={setSelectedTemplateCode}
                            disabled={
                              isSaving || generationTemplates.length === 0
                            }
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select agreement template" />
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
                            type="button"
                            className="gap-2"
                            onClick={() => void generateAgreement()}
                            disabled={
                              isSaving ||
                              generationTemplates.length === 0 ||
                              !selectedTemplateCode ||
                              !approvedDecision ||
                              missingApprovedMoveInDate ||
                              missingApprovedRentTerm ||
                              missingPremiumChargePayment ||
                              agreementAlreadyGenerated ||
                              caseIsCompleted
                            }
                          >
                            {isSaving ? (
                              <Loader2 className="h-4 w-4 animate-spin" />
                            ) : (
                              <FileSignature className="h-4 w-4" />
                            )}
                            {agreementAlreadyGenerated
                              ? 'Agreement generated'
                              : 'Generate agreement'}
                          </Button>
                        </div>
                        {generationTemplates.length === 0 ? (
                          <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                            No {rentalApplication ? 'lease' : 'sale'} agreement
                            template is available. Upload or activate the
                            correct transaction template in Central DMS first.
                          </p>
                        ) : null}
                        {missingApprovedRentTerm ? (
                          <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                            Enter the approved rental term on the current stage
                            before generating the agreement.
                          </p>
                        ) : null}
                        {missingPremiumChargePayment ? (
                          <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                            Initiate the premium charge invoice and wait for
                            Finance payment confirmation before generating the
                            lease agreement.
                          </p>
                        ) : null}
                        {generatedAgreement ? (
                          <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
                            Agreement generated with DMS reference{' '}
                            <span className="font-semibold">
                              {generatedAgreement.dmsReference}
                            </span>
                            <Button
                              type="button"
                              variant="link"
                              size="sm"
                              className="ml-2 h-auto px-0 text-emerald-900"
                              onClick={() => setIsAgreementViewerOpen(true)}
                            >
                              <Eye className="mr-1 h-3.5 w-3.5" />
                              View PDF
                            </Button>
                            <Button
                              type="button"
                              variant="link"
                              size="sm"
                              className="ml-2 h-auto px-0 text-emerald-900"
                              disabled={isSaving}
                              onClick={() => void downloadAgreementPdf()}
                            >
                              <Download className="mr-1 h-3.5 w-3.5" />
                              Download PDF
                            </Button>
                          </div>
                        ) : null}
                        {agreementAlreadyGenerated ? (
                          <div className="flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
                            <div>
                              <div className="flex items-center gap-2 font-medium">
                                <Gavel className="h-4 w-4" />
                                Legal review and customer release
                              </div>
                              <p className="mt-1 text-sm text-muted-foreground">
                                {legalAgreementReviewSubmitting
                                  ? 'Submitting draft to Legal...'
                                  : legalAgreementReviewStatus ||
                                    'The draft has not yet been lodged with Legal.'}
                              </p>
                            </div>
                            <Button
                              type="button"
                              variant={
                                legalAgreementReleasedForCustomer
                                  ? 'outline'
                                  : 'default'
                              }
                              className="gap-2"
                              disabled={
                                isSaving ||
                                legalAgreementReviewSubmitting ||
                                legalAgreementReviewStarted ||
                                legalAgreementReleasedForCustomer
                              }
                              onClick={() =>
                                void lodgeLegalMatter('agreementReview')
                              }
                            >
                              {legalAgreementReviewSubmitting ? (
                                <Loader2 className="h-4 w-4 animate-spin" />
                              ) : legalAgreementReleasedForCustomer ? (
                                <CheckCircle2 className="h-4 w-4" />
                              ) : (
                                <Send className="h-4 w-4" />
                              )}
                              {legalAgreementReviewSubmitting
                                ? 'Submitting to Legal...'
                                : legalAgreementReleasedForCustomer
                                  ? 'Released to customer'
                                  : legalAgreementReviewStarted
                                    ? 'Under Legal review'
                                    : 'Submit draft to Legal'}
                            </Button>
                          </div>
                        ) : null}
                      </CardContent>
                    </Card>
                  ) : null}

                  {agreementAlreadyGenerated &&
                  legalAgreementReleasedForCustomer ? (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-base">
                          <FileSignature className="h-4 w-4" />
                          Customer agreement execution
                        </CardTitle>
                        <p className="text-sm text-muted-foreground">
                          {customerAgreementSigningInLegal
                            ? `${rentalApplication ? 'Rent and lease' : 'Sale'} agreements follow the Legal release, customer signature, final Legal signature, and conveyance process.`
                            : `${rentalApplication ? 'Rent and lease' : 'Sale'} agreements follow the Legal release, customer signature, Estate approval, authorised digital signature, and conveyance process.`}
                        </p>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        <div className="divide-y rounded-md border">
                          <div className="flex items-start gap-3 p-3">
                            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-700" />
                            <div className="min-w-0 flex-1">
                              <p className="text-sm font-medium">
                                Legal release for customer signature
                              </p>
                              <p className="text-sm text-muted-foreground">
                                Legal approved the draft and sent it to the
                                customer portal.
                              </p>
                            </div>
                            <Badge variant="outline">Complete</Badge>
                          </div>
                          <div className="flex items-start gap-3 p-3">
                            {hasActiveCustomerSignedAgreement ? (
                              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-700" />
                            ) : (
                              <Send className="mt-0.5 h-4 w-4 shrink-0 text-blue-700" />
                            )}
                            <div className="min-w-0 flex-1">
                              <p className="text-sm font-medium">
                                Customer signature
                              </p>
                              <p className="text-sm text-muted-foreground">
                                {hasActiveCustomerSignedAgreement
                                  ? 'The signed customer agreement has been received.'
                                  : customerAgreementAccepted
                                    ? 'Customer accepted the agreement; waiting for the signed upload.'
                                    : 'Customer has been notified; waiting for review, acceptance, and signed upload.'}
                              </p>
                            </div>
                            <Badge
                              variant={
                                hasActiveCustomerSignedAgreement
                                  ? 'outline'
                                  : 'secondary'
                              }
                            >
                              {hasActiveCustomerSignedAgreement
                                ? 'Received'
                                : 'Waiting'}
                            </Badge>
                          </div>
                          <div className="flex items-start gap-3 p-3">
                            {fullyExecuted ? (
                              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-700" />
                            ) : (
                              <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                            )}
                            <div className="min-w-0 flex-1">
                              <p className="text-sm font-medium">
                                {customerAgreementSigningInLegal
                                  ? 'Final Legal signature and internal execution'
                                  : 'Estate approval and digital signature'}
                              </p>
                              <p className="text-sm text-muted-foreground">
                                {fullyExecuted
                                  ? customerAgreementSigningInLegal
                                    ? 'The final agreement has been digitally signed by Head of Legal.'
                                    : 'The final agreement has been approved and digitally signed by the authorised Estate signatory.'
                                  : hasActiveCustomerSignedAgreement
                                    ? customerAgreementSigningInLegal
                                      ? 'Complete the final Head of Legal signature.'
                                      : 'Complete Estate approval and the authorised digital signature.'
                                    : 'This begins after the customer returns the signed agreement.'}
                              </p>
                            </div>
                            <Badge
                              variant={fullyExecuted ? 'outline' : 'secondary'}
                            >
                              {fullyExecuted ? 'Complete' : 'Pending'}
                            </Badge>
                          </div>
                        </div>
                      </CardContent>
                    </Card>
                  ) : null}

                  {agreementRecord &&
                  hasActiveCustomerSignedAgreement &&
                  !customerAgreementSigningInLegal ? (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-base">
                          <ShieldCheck className="h-4 w-4" />
                          Agreement approval and signature
                        </CardTitle>
                        <p className="text-sm text-muted-foreground">
                          {rentalApplication
                            ? 'The approved move-in date remains inactive until the customer copy is internally approved and digitally signed.'
                            : 'The sale agreement must be internally approved and digitally signed before conveyance continues.'}
                        </p>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        <div className="flex flex-wrap items-center gap-2 text-sm">
                          <Badge variant="outline">
                            {agreementRecord.lifecycleStatus}
                          </Badge>
                          <span className="text-muted-foreground">
                            DMS reference {agreementRecord.documentReference}
                          </span>
                        </div>
                        {agreementWorkflowStatus ? (
                          <div
                            role="status"
                            aria-live="polite"
                            className={`flex items-center gap-2 rounded-md border px-3 py-2 text-sm ${
                              agreementWorkflowStatus.tone === 'error'
                                ? 'border-destructive/40 bg-destructive/5 text-destructive'
                                : agreementWorkflowStatus.tone === 'success'
                                  ? 'border-emerald-300 bg-emerald-50 text-emerald-800'
                                  : 'border-blue-300 bg-blue-50 text-blue-800'
                            }`}
                          >
                            {agreementWorkflowStatus.tone === 'progress' ? (
                              <Loader2 className="h-4 w-4 shrink-0 animate-spin" />
                            ) : agreementWorkflowStatus.tone === 'success' ? (
                              <CheckCircle2 className="h-4 w-4 shrink-0" />
                            ) : null}
                            <span>{agreementWorkflowStatus.message}</span>
                          </div>
                        ) : null}
                        <Textarea
                          value={agreementWorkflowNotes}
                          onChange={(event) =>
                            setAgreementWorkflowNotes(event.target.value)
                          }
                          placeholder="Approval or signature notes (optional)"
                          disabled={isUpdatingAgreementWorkflow}
                        />
                        <div className="flex flex-wrap gap-2">
                          {[
                            'draft',
                            'generated',
                            'returned for action',
                          ].includes(agreementLifecycle) ? (
                            <Button
                              type="button"
                              className="gap-2"
                              disabled={isUpdatingAgreementWorkflow}
                              onClick={() =>
                                void updateAgreementWorkflow(
                                  'SubmitForApproval'
                                )
                              }
                            >
                              {isUpdatingAgreementWorkflow ? (
                                <Loader2 className="h-4 w-4 animate-spin" />
                              ) : (
                                <Send className="h-4 w-4" />
                              )}
                              Submit for approval
                            </Button>
                          ) : null}
                          {agreementLifecycle === 'pending approval' ? (
                            <>
                              <Button
                                type="button"
                                className="gap-2"
                                disabled={
                                  isUpdatingAgreementWorkflow ||
                                  !canManageAgreementApproval
                                }
                                title={
                                  canManageAgreementApproval
                                    ? undefined
                                    : 'Property or Estate Manager approval required'
                                }
                                onClick={() =>
                                  void updateAgreementWorkflow('Approve')
                                }
                              >
                                <ShieldCheck className="h-4 w-4" />
                                Approve agreement
                              </Button>
                              <Button
                                type="button"
                                variant="outline"
                                disabled={
                                  isUpdatingAgreementWorkflow ||
                                  !canManageAgreementApproval
                                }
                                onClick={() =>
                                  void updateAgreementWorkflow('Return')
                                }
                              >
                                Return for correction
                              </Button>
                            </>
                          ) : null}
                          {agreementLifecycle === 'approved' ? (
                            <>
                              <Button
                                type="button"
                                className="gap-2"
                                disabled={
                                  isUpdatingAgreementWorkflow ||
                                  !canDigitallySignAgreement
                                }
                                title={
                                  canDigitallySignAgreement
                                    ? undefined
                                    : 'Executive Approver signature required'
                                }
                                onClick={() =>
                                  void updateAgreementWorkflow('Sign')
                                }
                              >
                                {activeAgreementWorkflowAction === 'Sign' ? (
                                  <Loader2 className="h-4 w-4 animate-spin" />
                                ) : (
                                  <PenLine className="h-4 w-4" />
                                )}
                                {activeAgreementWorkflowAction === 'Sign'
                                  ? 'Signing...'
                                  : 'Digitally sign'}
                              </Button>
                              <Button
                                type="button"
                                variant="outline"
                                disabled={
                                  isUpdatingAgreementWorkflow ||
                                  !canManageAgreementApproval
                                }
                                onClick={() =>
                                  void updateAgreementWorkflow('Return')
                                }
                              >
                                Return for correction
                              </Button>
                            </>
                          ) : null}
                          {agreementLifecycle === 'signed' ? (
                            <div className="flex items-center gap-2 text-sm font-medium text-emerald-700">
                              <CheckCircle2 className="h-4 w-4" />
                              {rentalApplication
                                ? leaseApplication
                                  ? 'Final agreement signed; lease conveyance is next'
                                  : 'Final agreement signed; move-in is now effective'
                                : 'Final agreement signed; conveyance and registration has started'}
                            </div>
                          ) : null}
                        </div>
                      </CardContent>
                    </Card>
                  ) : null}

                  {agreementAlreadyGenerated &&
                  legalAgreementReleasedForCustomer &&
                  (!rentalApplication || leaseApplication) ? (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-base">
                          <Gavel className="h-4 w-4" />
                          Legal transfer and conveyance
                        </CardTitle>
                        <p className="text-sm text-muted-foreground">
                          {!fullyExecuted
                            ? 'This next Legal matter opens after the customer-signed agreement is internally approved and digitally signed.'
                            : leaseApplication
                              ? 'Confirm the full lease payment after signing, then start Legal conveyance. Ground rent remains separate from the full-term lease amount.'
                              : saleFullyPaidInSales
                                ? 'Sales has collected the full purchase amount, so Estate only completes Legal conveyance and ownership transfer.'
                                : 'Complete the Estate balance invoice, customer payment, and Legal conveyance before ownership transfer is marked complete.'}
                        </p>
                      </CardHeader>
                      <CardContent className="space-y-3">
                        {!rentalApplication || leaseApplication ? (
                          <div className="space-y-3">
                            <div className="flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
                              <div>
                                <p className="font-medium">
                                  {leaseApplication
                                    ? 'Lease balance invoice and payment'
                                    : 'Purchase invoice and payment'}
                                </p>
                                <p className="text-sm text-muted-foreground">
                                  {saleFullyPaidInSales
                                    ? 'Sales recorded full payment; no Estate invoice is required.'
                                    : caseFieldValue(
                                          selectedCase,
                                          'saleInvoiceReference'
                                        )
                                      ? `${caseFieldValue(selectedCase, 'saleInvoiceReference')} · ${caseFieldValue(selectedCase, 'salePaymentStatus') || caseFieldValue(selectedCase, 'saleInvoiceStatus')}`
                                      : 'Create the Finance AR invoice for the remaining Estate balance after the agreement is fully signed.'}
                                </p>
                                {caseFieldValue(
                                  selectedCase,
                                  'salePaymentCheckStatus'
                                ) ? (
                                  <p className="mt-1 text-xs text-muted-foreground">
                                    {caseFieldValue(
                                      selectedCase,
                                      'salePaymentCheckStatus'
                                    )}
                                  </p>
                                ) : null}
                              </div>
                              <div className="flex flex-wrap gap-2 sm:justify-end">
                                {saleInvoiceId ? (
                                  <Button
                                    type="button"
                                    variant="outline"
                                    className="gap-2"
                                    disabled={isSaving}
                                    onClick={() => void syncSalePaymentStatus()}
                                  >
                                    <RefreshCw className="h-4 w-4" />
                                    Refresh payment status
                                  </Button>
                                ) : null}
                                <Button
                                  type="button"
                                  variant="outline"
                                  className="gap-2"
                                  disabled={
                                    isSaving ||
                                    !fullyExecuted ||
                                    Boolean(saleInvoiceId) ||
                                    saleFullyPaidInSales
                                  }
                                  onClick={() => void createSaleInvoice()}
                                >
                                  {saleFullyPaidInSales ? (
                                    <CheckCircle2 className="h-4 w-4" />
                                  ) : (
                                    <Send className="h-4 w-4" />
                                  )}
                                  {saleFullyPaidInSales
                                    ? 'No Estate invoice required'
                                    : saleInvoiceId
                                      ? 'Invoice created'
                                      : 'Create balance invoice'}
                                </Button>
                              </div>
                            </div>
                            <div className="flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
                              <div>
                                <p className="font-medium">
                                  Conveyance and registration
                                </p>
                                <p className="text-sm text-muted-foreground">
                                  {caseFieldValue(
                                    selectedCase,
                                    'legalConveyanceStatus'
                                  ) ||
                                    'Required after internal agreement execution before Estate completes the transaction.'}
                                </p>
                              </div>
                              <Button
                                type="button"
                                className="gap-2"
                                disabled={
                                  isSaving ||
                                  !fullyExecuted ||
                                  !saleInvoicePaid ||
                                  legalConveyanceStarted
                                }
                                onClick={() =>
                                  void lodgeLegalMatter(
                                    'conveyanceRegistration'
                                  )
                                }
                              >
                                <Send className="h-4 w-4" />
                                {legalConveyanceStarted
                                  ? 'With Legal'
                                  : 'Start conveyance'}
                              </Button>
                            </div>
                            {!leaseApplication ? (
                              <div className="flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
                                <div>
                                  <p className="font-medium">
                                    Complete ownership transfer
                                  </p>
                                  <p className="text-sm text-muted-foreground">
                                    {caseFieldValue(
                                      selectedCase,
                                      'ownershipTransferStatus'
                                    ) ||
                                      'Requires a fully paid invoice and completed Legal conveyance.'}
                                  </p>
                                </div>
                                <Button
                                  type="button"
                                  className="gap-2"
                                  disabled={
                                    isSaving ||
                                    ownershipTransferCompleted ||
                                    !canCompleteSaleOwnership
                                  }
                                  title={
                                    canCompleteSaleOwnership ||
                                    ownershipTransferCompleted
                                      ? undefined
                                      : saleFullyPaidInSales
                                        ? 'Requires completed Legal conveyance'
                                        : 'Requires full Estate balance payment and completed Legal conveyance'
                                  }
                                  onClick={() => void completeSaleOwnership()}
                                >
                                  <CheckCircle2 className="h-4 w-4" />
                                  {ownershipTransferCompleted
                                    ? 'Ownership transferred'
                                    : !saleInvoicePaid
                                      ? 'Awaiting payment'
                                      : !legalConveyanceCompleted
                                        ? 'Awaiting Legal'
                                        : 'Complete ownership transfer'}
                                </Button>
                              </div>
                            ) : null}
                          </div>
                        ) : null}
                        {leaseApplication ? (
                          <div className="space-y-3">
                            <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
                              <Select
                                value={selectedLegalMatterType}
                                onValueChange={setSelectedLegalMatterType}
                                disabled={isSaving || !fullyExecuted}
                              >
                                <SelectTrigger>
                                  <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="termination">
                                    Termination / recognition
                                  </SelectItem>
                                  <SelectItem value="leaseRenewal">
                                    Lease renewal
                                  </SelectItem>
                                  <SelectItem value="leaseVariation">
                                    Deed of variation
                                  </SelectItem>
                                  <SelectItem value="sublease">
                                    Sublease
                                  </SelectItem>
                                  <SelectItem value="assignment">
                                    Assignment / vesting
                                  </SelectItem>
                                  <SelectItem value="mortgage">
                                    Consent to mortgage
                                  </SelectItem>
                                  <SelectItem value="mortgageInPrinciple">
                                    Mortgage in principle
                                  </SelectItem>
                                  <SelectItem value="disputeAdvisory">
                                    Property dispute / advisory
                                  </SelectItem>
                                  <SelectItem value="courtProcess">
                                    Court process
                                  </SelectItem>
                                  <SelectItem value="otherCourtProcess">
                                    Other court process
                                  </SelectItem>
                                </SelectContent>
                              </Select>
                              <Button
                                type="button"
                                variant="outline"
                                className="gap-2"
                                disabled={isSaving || !fullyExecuted}
                                onClick={() =>
                                  void lodgeLegalMatter(selectedLegalMatterType)
                                }
                              >
                                <Gavel className="h-4 w-4" />
                                Lodge other Legal matter
                              </Button>
                            </div>
                          </div>
                        ) : null}
                      </CardContent>
                    </Card>
                  ) : null}
                  {agreementAlreadyGenerated &&
                  legalAgreementReleasedForCustomer &&
                  rentalApplication &&
                  !leaseApplication ? (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-base">
                          <FileText className="h-4 w-4" />
                          First month rent and billing
                        </CardTitle>
                        <p className="text-sm text-muted-foreground">
                          {saleFullyPaidInSales
                            ? 'Sales recorded the full first month rent, so Estate only confirms the move-in billing start.'
                            : 'Create and confirm the Finance AR invoice for the remaining first month rent before completing the billing check.'}
                        </p>
                      </CardHeader>
                      <CardContent className="space-y-3">
                        <div className="flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
                          <div>
                            <p className="font-medium">
                              First month rent balance
                            </p>
                            <p className="text-sm text-muted-foreground">
                              {saleFullyPaidInSales
                                ? 'No Estate invoice is required for the first month.'
                                : saleInvoiceId
                                  ? `${caseFieldValue(selectedCase, 'saleInvoiceReference')} · ${caseFieldValue(selectedCase, 'salePaymentStatus') || caseFieldValue(selectedCase, 'saleInvoiceStatus')}`
                                  : 'Create the Finance AR invoice for the remaining first month rent after the agreement is fully signed.'}
                            </p>
                            {caseFieldValue(
                              selectedCase,
                              'salePaymentCheckStatus'
                            ) ? (
                              <p className="mt-1 text-xs text-muted-foreground">
                                {caseFieldValue(
                                  selectedCase,
                                  'salePaymentCheckStatus'
                                )}
                              </p>
                            ) : null}
                          </div>
                          <div className="flex flex-wrap gap-2 sm:justify-end">
                            {saleInvoiceId ? (
                              <Button
                                type="button"
                                variant="outline"
                                className="gap-2"
                                disabled={isSaving}
                                onClick={() => void syncSalePaymentStatus()}
                              >
                                <RefreshCw className="h-4 w-4" />
                                Refresh payment status
                              </Button>
                            ) : null}
                            <Button
                              type="button"
                              variant="outline"
                              className="gap-2"
                              disabled={
                                isSaving ||
                                !fullyExecuted ||
                                Boolean(saleInvoiceId) ||
                                saleFullyPaidInSales
                              }
                              onClick={() => void createSaleInvoice()}
                            >
                              {saleFullyPaidInSales ? (
                                <CheckCircle2 className="h-4 w-4" />
                              ) : (
                                <Send className="h-4 w-4" />
                              )}
                              {saleFullyPaidInSales
                                ? 'No Estate invoice required'
                                : saleInvoiceId
                                  ? 'Invoice created'
                                  : 'Create rent balance invoice'}
                            </Button>
                          </div>
                        </div>
                      </CardContent>
                    </Card>
                  ) : null}
                </TabsContent>

                <TabsContent value="complete" className="space-y-4">
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-base">
                        Complete this stage
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      {!caseIsCompleted && !selectedCase.canEditCurrentStage ? (
                        <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          This stage is assigned to{' '}
                          <span className="font-medium">
                            {selectedCase.currentAssignedRole ||
                              selectedCase.currentStageOwner ||
                              'another workflow role'}
                          </span>
                          . Sign in as a user with that role to update, reject,
                          or route the request.
                        </div>
                      ) : null}
                      {saleWorkflowCompleted ? (
                        <>
                          <label className="flex items-start gap-3 rounded-md border p-3 text-sm">
                            <Checkbox
                              checked={saleCloseoutChecklist.customerStatus}
                              onCheckedChange={(checked) =>
                                setSaleCloseoutChecklist((current) => ({
                                  ...current,
                                  customerStatus: checked === true,
                                }))
                              }
                              disabled={isSaving || saleCloseoutArchived}
                            />
                            <span>
                              Customer-facing request status reflects the final
                              outcome
                            </span>
                          </label>
                          <label className="flex items-start gap-3 rounded-md border p-3 text-sm">
                            <Checkbox
                              checked={saleCloseoutChecklist.auditReferences}
                              onCheckedChange={(checked) =>
                                setSaleCloseoutChecklist((current) => ({
                                  ...current,
                                  auditReferences: checked === true,
                                }))
                              }
                              disabled={isSaving || saleCloseoutArchived}
                            />
                            <span>
                              Request is closed with its decision and
                              transaction audit references
                            </span>
                          </label>
                        </>
                      ) : (
                        stageItems.map((item) => (
                          <label
                            key={item.id}
                            className="flex items-start gap-3 rounded-md border p-3 text-sm"
                          >
                            <Checkbox
                              checked={item.isCompleted}
                              onCheckedChange={(checked) =>
                                void updateChecklist(item.id, checked === true)
                              }
                              disabled={
                                isSaving ||
                                caseIsCompleted ||
                                !selectedCase.canEditCurrentStage
                              }
                            />
                            <span>{item.text}</span>
                          </label>
                        ))
                      )}
                      <Textarea
                        value={completionNotes}
                        onChange={(event) =>
                          setCompletionNotes(event.target.value)
                        }
                        placeholder="Stage completion notes (optional)"
                        disabled={
                          saleWorkflowCompleted
                            ? isSaving || saleCloseoutArchived
                            : isSaving ||
                              caseIsCompleted ||
                              !selectedCase.canEditCurrentStage
                        }
                      />
                      <Button
                        type="button"
                        className="gap-2"
                        onClick={() =>
                          saleWorkflowCompleted
                            ? completeSaleCloseout()
                            : void completeStage()
                        }
                        disabled={
                          saleWorkflowCompleted
                            ? isSaving ||
                              saleCloseoutArchived ||
                              !ownershipTransferCompleted ||
                              !saleCloseoutConfirmed
                            : isSaving ||
                              caseIsCompleted ||
                              !selectedCase.canEditCurrentStage ||
                              hasUnsavedStageUpdates ||
                              missingRequiredStageFields.length > 0 ||
                              !stageConfirmed ||
                              missingPremiumChargeAmount ||
                              missingApprovedMoveInDate ||
                              missingApprovedRentTerm ||
                              missingPremiumChargePayment ||
                              missingApprovedAgreement ||
                              missingLegalAgreementReview
                        }
                      >
                        {isSaving ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Send className="h-4 w-4" />
                        )}
                        {saleWorkflowCompleted
                          ? saleCloseoutArchived
                            ? 'Sale closeout archived'
                            : ownershipTransferCompleted
                              ? 'Complete sale closeout'
                              : 'Pending ownership transfer'
                          : caseIsCompleted
                            ? 'Case completed'
                            : selectedCase.usesConfiguredWorkflow
                              ? 'Complete stage and route forward'
                              : 'Complete manual stage'}
                      </Button>
                      {hasUnsavedStageUpdates ? (
                        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          Save the current stage updates before routing this
                          request forward.
                        </p>
                      ) : null}
                      {missingRequiredStageFields.length > 0 ? (
                        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          Complete the required current-stage field(s):{' '}
                          {missingRequiredStageFields.slice(0, 4).join(', ')}
                          {missingRequiredStageFields.length > 4
                            ? ` and ${missingRequiredStageFields.length - 4} more`
                            : ''}
                          .
                        </p>
                      ) : null}
                      {missingLegalAgreementReview &&
                      !missingApprovedAgreement ? (
                        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          {customerAgreementSigningInLegal
                            ? 'Submit the generated agreement to Legal, wait for the customer to return it, and wait for the Head of Legal signature before routing this stage forward.'
                            : 'Submit the generated agreement to Legal and wait for Legal approval and release back to Estate before routing this stage forward.'}
                        </p>
                      ) : null}
                      {missingApprovedAgreement ? (
                        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          Generate the agreement before routing this stage
                          forward.
                        </p>
                      ) : null}
                      {missingApprovedRentTerm ? (
                        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          Enter the approved rental term before routing this
                          rental case forward.
                        </p>
                      ) : null}
                      {missingPremiumChargeAmount ? (
                        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          Enter and save the premium charge amount before
                          routing this request forward.
                        </p>
                      ) : null}
                      {missingPremiumChargePayment ? (
                        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                          Premium charge payment is still pending. Refresh the
                          Finance payment status after Finance receives payment.
                        </p>
                      ) : null}
                      {!caseIsCompleted && selectedCase.canEditCurrentStage ? (
                        <div className="flex flex-wrap gap-2 border-t pt-4">
                          {selectedCase.currentStageIndex > 0 ? (
                            <Button
                              type="button"
                              variant="outline"
                              className="gap-2"
                              disabled={isSaving}
                              onClick={() =>
                                setReviewAction('RequestClarification')
                              }
                            >
                              <Undo2 className="h-4 w-4" />
                              Return for clarification
                            </Button>
                          ) : null}
                          <Button
                            type="button"
                            variant="outline"
                            className="gap-2 border-red-300 text-red-700 hover:bg-red-50 hover:text-red-800"
                            disabled={isSaving}
                            onClick={() => setReviewAction('Reject')}
                          >
                            <XCircle className="h-4 w-4" />
                            Reject application
                          </Button>
                        </div>
                      ) : null}
                    </CardContent>
                  </Card>
                </TabsContent>
              </Tabs>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
