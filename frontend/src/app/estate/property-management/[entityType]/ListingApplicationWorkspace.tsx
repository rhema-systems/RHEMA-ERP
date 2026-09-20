'use client';

import Link from 'next/link';
import dynamic from 'next/dynamic';
import React from 'react';
import { useSearchParams } from 'next/navigation';
import { toast } from 'sonner';
import {
  CheckCircle2,
  ChevronRight,
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
import { Textarea } from '@/components/ui/textarea';
import { CentralDocumentViewerDialog } from '@/components/document-management/CentralDocumentViewerDialog';
import { useAuth } from '@/hooks/use-auth';
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

const ENTITY_TYPE = 'EstatePropertyManagementListingApplication';
const REQUESTS_PER_PAGE = 10;
const SALE_CLOSEOUT_COMPLETED_QUEUE_KEY =
  'property-management.sale-closeout-completed-case-ids';

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

const caseFieldValue = (procedureCase: ProcedureCaseDetail, key: string) =>
  procedureCase.fields.find((field) => field.key === key)?.value?.trim() || '';

const isApprovedDecision = (value: string) => {
  const normalized = value.toLowerCase();
  return normalized === 'approved' || normalized.startsWith('approved ');
};

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

const queueStatusLabel = (
  procedureCase: ProcedureCaseSummary,
  queueView: 'active' | 'completed'
) => {
  const status = procedureCase.status.trim();
  if (status.toLowerCase() === 'completed' && queueView === 'active') {
    return 'Pending final closeout';
  }

  return status;
};

const isRentalApplication = (procedureCase: ProcedureCaseDetail) => {
  const requestType = caseFieldValue(
    procedureCase,
    'requestType'
  ).toLowerCase();
  const title = procedureCase.title.toLowerCase();
  return !(
    requestType.includes('purchase') ||
    requestType.includes('sale') ||
    title.startsWith('purchase bid') ||
    title.startsWith('sale request')
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
  const keys = new Set(procedureCase.currentStageFieldKeys);
  if (isDecisionStage(procedureCase)) {
    keys.add('decisionStatus');
    if (isRentalApplication(procedureCase)) {
      keys.add('moveInDate');
    }
  }
  return keys;
};

const firstNonBlank = (...values: Array<string | null | undefined>) =>
  values.find((value) => value?.trim())?.trim() || '';

const buildAgreementMergeValues = (
  procedureCase: ProcedureCaseDetail,
  rentalApplication: boolean,
  moveInDate: string
) => {
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

  return {
    ...fields,
    AgreementReference: caseReference,
    AgreementDate: new Date().toISOString().slice(0, 10),
    ApplicantName: customerName,
    CaseReference: caseReference,
    PropertyNumber: propertyNumber,
    PropertyReference: propertyReference,
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
    RentAmount: paymentAmount,
    PaymentAmount: paymentAmount,
    PaymentType: rentalApplication ? 'Monthly rent' : 'Purchase price',
    PaymentSchedule: rentalApplication ? 'Monthly' : 'As agreed',
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
  const { hasAnyRole } = useAuth();
  const requestedCaseId = searchParams.get('caseId');
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [queueView, setQueueView] = React.useState<'active' | 'completed'>(
    'active'
  );
  const [queuePage, setQueuePage] = React.useState(1);
  const [selectedCase, setSelectedCase] =
    React.useState<ProcedureCaseDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [completionNotes, setCompletionNotes] = React.useState('');
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
      const data = await procedureCaseService.listCases(
        'PropertyManagement',
        ENTITY_TYPE
      );
      setCases(data);
      const activeCases = data.filter(
        (item) => !isArchivedQueueCase(item, completedSaleOwnershipCaseIds)
      );
      const targetId =
        requestedCaseId && data.some((item) => item.id === requestedCaseId)
          ? requestedCaseId
          : activeCases[0]?.id;
      if (requestedCaseId) {
        const requested = data.find((item) => item.id === requestedCaseId);
        if (
          requested &&
          isArchivedQueueCase(requested, completedSaleOwnershipCaseIds)
        ) {
          setQueueView('completed');
        } else {
          setQueueView('active');
        }
      }
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
    rememberSaleOwnershipCompletion,
    requestedCaseId,
  ]);

  React.useEffect(() => {
    void loadCases();
  }, [loadCases]);

  React.useEffect(() => {
    setSaleCloseoutChecklist({
      customerStatus: false,
      auditReferences: false,
    });
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
        const applicableTemplates = templates.filter((template) => {
          const value =
            `${template.templateCode} ${template.title} ${template.documentType}`.toLowerCase();
          if (!value.includes('agreement')) return false;
          return rental
            ? value.includes('lease') ||
                value.includes('tenancy') ||
                value.includes('rent')
            : value.includes('sale') || value.includes('purchase');
        });
        setGenerationTemplates(applicableTemplates);
        setSelectedTemplateCode((current) =>
          applicableTemplates.some(
            (template) => template.templateCode === current
          )
            ? current
            : applicableTemplates[0]?.templateCode || ''
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
    const visibleCount = cases.filter((item) =>
      queueView === 'completed'
        ? isArchivedQueueCase(item, completedSaleOwnershipCaseIds)
        : !isArchivedQueueCase(item, completedSaleOwnershipCaseIds)
    ).length;
    const totalPages = Math.max(1, Math.ceil(visibleCount / REQUESTS_PER_PAGE));
    setQueuePage((current) => Math.min(current, totalPages));
  }, [cases, completedSaleOwnershipCaseIds, queueView]);

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

    const approved = isApprovedDecision(
      caseFieldValue(selectedCase, 'decisionStatus')
    );
    const transactionHandoffStage = selectedCase.currentStageIndex >= 2;
    if (
      transactionHandoffStage &&
      approved &&
      isRentalApplication(selectedCase) &&
      !caseFieldValue(selectedCase, 'moveInDate')
    ) {
      setError('Set the approved move-in date before final approval.');
      return;
    }
    if (
      transactionHandoffStage &&
      approved &&
      !caseFieldValue(selectedCase, 'generatedAgreementReference')
    ) {
      setError('Generate the agreement before completing final approval.');
      return;
    }
    if (
      transactionHandoffStage &&
      approved &&
      !caseFieldValue(selectedCase, 'legalAgreementReviewStatus')
        .toLowerCase()
        .includes('approved')
    ) {
      setError(
        'Legal must approve the generated agreement before completing final approval.'
      );
      return;
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
    const moveInDate = caseFieldValue(selectedCase, 'moveInDate');
    if (rentalApplication && !moveInDate) {
      setError(
        'Set the approved move-in date before generating the rental agreement.'
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
        'Unable to create the sale invoice.'
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
        'Unable to refresh the sale payment status.'
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
    setQueueView('completed');
    toast.success('Sale closeout completed and moved to Archive.');
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

  const summaryFields = selectedCase?.fields.filter((field) =>
    SUMMARY_FIELD_KEYS.includes(field.key)
  );
  const editableFields = selectedCase
    ? selectedCase.fields.filter(
        (field) =>
          editableFieldKeys(selectedCase).has(field.key) &&
          (field.key !== 'moveInDate' || isRentalApplication(selectedCase))
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
  const activeCases = cases.filter(
    (item) => !isArchivedQueueCase(item, completedSaleOwnershipCaseIds)
  );
  const completedCases = cases.filter((item) =>
    isArchivedQueueCase(item, completedSaleOwnershipCaseIds)
  );
  const visibleCases = queueView === 'active' ? activeCases : completedCases;
  const queueTotalPages = Math.max(
    1,
    Math.ceil(visibleCases.length / REQUESTS_PER_PAGE)
  );
  const pagedVisibleCases = visibleCases.slice(
    (queuePage - 1) * REQUESTS_PER_PAGE,
    queuePage * REQUESTS_PER_PAGE
  );

  const approvedDecision = selectedCase
    ? isApprovedDecision(caseFieldValue(selectedCase, 'decisionStatus'))
    : false;
  const rentalApplication = selectedCase
    ? isRentalApplication(selectedCase)
    : false;
  const missingApprovedMoveInDate = Boolean(
    selectedCase &&
      approvedDecision &&
      rentalApplication &&
      !caseFieldValue(selectedCase, 'moveInDate')
  );
  const missingApprovedAgreement = Boolean(
    selectedCase &&
      approvedDecision &&
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
  const legalAgreementReviewStarted = Boolean(
    selectedCase &&
      (caseFieldValue(selectedCase, 'legalAgreementReviewCaseId') ||
        caseFieldValue(selectedCase, 'legalAgreementReviewReference') ||
        legalAgreementReviewStatus)
  );
  const legalAgreementReviewSubmitting =
    pendingLegalMatterType === 'agreementReview';
  const legalAgreementReviewApproved = legalAgreementReviewStatus
    .toLowerCase()
    .includes('approved');
  const missingLegalAgreementReview = Boolean(
    selectedCase && approvedDecision && !legalAgreementReviewApproved
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
    ? Number(
        caseFieldValue(selectedCase, 'saleInvoiceBalance').replace(
          /[^\d.-]/g,
          ''
        )
      )
    : Number.NaN;
  const saleInvoicePaid = Boolean(
    selectedCase &&
      salePaymentStatus === 'paid in full' &&
      Number.isFinite(saleInvoiceBalance) &&
      saleInvoiceBalance <= 0
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

        <div className="grid gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
          <Card className="h-fit">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <ClipboardCheck className="h-4 w-4" />
                Request queue
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <div className="grid grid-cols-2 gap-1 rounded-md bg-muted p-1">
                <Button
                  type="button"
                  size="sm"
                  variant={queueView === 'active' ? 'secondary' : 'ghost'}
                  onClick={() => {
                    setQueueView('active');
                    setQueuePage(1);
                    if (
                      activeCases[0] &&
                      !activeCases.some((item) => item.id === selectedCase?.id)
                    ) {
                      void selectCase(activeCases[0].id);
                    }
                  }}
                >
                  Active ({activeCases.length})
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant={queueView === 'completed' ? 'secondary' : 'ghost'}
                  onClick={() => {
                    setQueueView('completed');
                    setQueuePage(1);
                    if (
                      completedCases[0] &&
                      !completedCases.some(
                        (item) => item.id === selectedCase?.id
                      )
                    ) {
                      void selectCase(completedCases[0].id);
                    }
                  }}
                >
                  Archive ({completedCases.length})
                </Button>
              </div>
              {visibleCases.length === 0 ? (
                <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                  {queueView === 'active'
                    ? 'Customer bids and rental requests will appear here after they are submitted from a published listing.'
                    : 'Completed requests will appear here for audit and reference.'}
                </p>
              ) : (
                pagedVisibleCases.map((item) => (
                  <button
                    type="button"
                    key={item.id}
                    onClick={() => void selectCase(item.id)}
                    className={`w-full rounded-md border p-3 text-left transition-colors hover:bg-muted/60 ${
                      selectedCase?.id === item.id
                        ? 'border-primary bg-primary/5'
                        : 'border-border'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <span className="text-sm font-medium">
                        {item.referenceNumber || item.title}
                      </span>
                      <ChevronRight className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {item.applicantName || 'Customer'}
                    </p>
                    <div className="mt-2 flex flex-wrap gap-1">
                      <Badge variant="secondary">{item.currentStageName}</Badge>
                      {!item.usesConfiguredWorkflow ? (
                        <Badge variant="secondary">Manual</Badge>
                      ) : null}
                      {item.status.trim().toLowerCase() === 'completed' ? (
                        <Badge>{queueStatusLabel(item, queueView)}</Badge>
                      ) : null}
                    </div>
                  </button>
                ))
              )}
              {visibleCases.length > REQUESTS_PER_PAGE ? (
                <Pagination
                  currentPage={queuePage}
                  totalPages={queueTotalPages}
                  totalItems={visibleCases.length}
                  pageSize={REQUESTS_PER_PAGE}
                  onPageChange={setQueuePage}
                />
              ) : null}
            </CardContent>
          </Card>

          {!selectedCase ? (
            <Card>
              <CardContent className="py-16 text-center text-sm text-muted-foreground">
                Select a property request to begin.
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
                      <Badge>{selectedStatusLabel}</Badge>
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
                          field.fieldType === 'textarea' ? 'sm:col-span-2' : ''
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
                            {field.key === 'moveInDate' &&
                            rentalApplication &&
                            isDecisionStage(selectedCase)
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
                      No customer documents have been uploaded for this request.
                    </p>
                  ) : (
                    selectedCase.documents.map((document) => (
                      <div key={document.id} className="rounded-md border p-3">
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
                                    current === document.id ? null : document.id
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

              {showAgreementGeneration ? (
                <Card>
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2 text-base">
                      <FileSignature className="h-4 w-4" />
                      Agreement generation
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">
                      Set the approval details, then generate the agreement
                      before routing the approved request to the customer.
                    </p>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="grid gap-3 md:grid-cols-[1fr_auto]">
                      <Select
                        value={selectedTemplateCode || undefined}
                        onValueChange={setSelectedTemplateCode}
                        disabled={isSaving || generationTemplates.length === 0}
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
                        template is available. Upload or activate the correct
                        transaction template in Central DMS first.
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
                            Legal agreement review
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
                            legalAgreementReviewApproved ? 'outline' : 'default'
                          }
                          className="gap-2"
                          disabled={
                            isSaving ||
                            legalAgreementReviewSubmitting ||
                            legalAgreementReviewStarted ||
                            legalAgreementReviewApproved
                          }
                          onClick={() =>
                            void lodgeLegalMatter('agreementReview')
                          }
                        >
                          {legalAgreementReviewSubmitting ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : legalAgreementReviewApproved ? (
                            <CheckCircle2 className="h-4 w-4" />
                          ) : (
                            <Send className="h-4 w-4" />
                          )}
                          {legalAgreementReviewSubmitting
                            ? 'Submitting to Legal...'
                            : legalAgreementReviewApproved
                              ? 'Approved by Legal'
                              : legalAgreementReviewStarted
                                ? 'Under Legal review'
                                : 'Submit draft to Legal'}
                        </Button>
                      </div>
                    ) : null}
                  </CardContent>
                </Card>
              ) : null}

              {agreementRecord && hasActiveCustomerSignedAgreement ? (
                <Card>
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2 text-base">
                      <ShieldCheck className="h-4 w-4" />
                      Agreement approval and signature
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">
                      The approved move-in date remains inactive until the
                      customer copy is internally approved and digitally signed.
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
                      {['draft', 'generated', 'returned for action'].includes(
                        agreementLifecycle
                      ) ? (
                        <Button
                          type="button"
                          className="gap-2"
                          disabled={isUpdatingAgreementWorkflow}
                          onClick={() =>
                            void updateAgreementWorkflow('SubmitForApproval')
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
                            onClick={() => void updateAgreementWorkflow('Sign')}
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
                            ? 'Final agreement signed; move-in is now effective'
                            : 'Final agreement signed; conveyance and registration has started'}
                        </div>
                      ) : null}
                    </div>
                  </CardContent>
                </Card>
              ) : null}

              {fullyExecuted ? (
                <Card>
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2 text-base">
                      <Gavel className="h-4 w-4" />
                      Legal follow-up
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">
                      {rentalApplication
                        ? 'Lodge later legal events from this property record. Legal receives and completes the matter in the Legal workspace.'
                        : 'Complete the sale invoice, customer payment, and Legal conveyance before ownership transfer is marked complete.'}
                    </p>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {!rentalApplication ? (
                      <div className="space-y-3">
                        <div className="flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
                          <div>
                            <p className="font-medium">
                              Purchase invoice and payment
                            </p>
                            <p className="text-sm text-muted-foreground">
                              {caseFieldValue(
                                selectedCase,
                                'saleInvoiceReference'
                              )
                                ? `${caseFieldValue(selectedCase, 'saleInvoiceReference')} · ${caseFieldValue(selectedCase, 'salePaymentStatus') || caseFieldValue(selectedCase, 'saleInvoiceStatus')}`
                                : 'Create the one-time Finance AR invoice for the approved purchase price.'}
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
                              disabled={isSaving || Boolean(saleInvoiceId)}
                              onClick={() => void createSaleInvoice()}
                            >
                              <Send className="h-4 w-4" />
                              {saleInvoiceId
                                ? 'Invoice created'
                                : 'Create sale invoice'}
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
                                'Required before Estate transfers ownership and marks the property sold.'}
                            </p>
                          </div>
                          <Button
                            type="button"
                            className="gap-2"
                            disabled={isSaving || legalConveyanceStarted}
                            onClick={() =>
                              void lodgeLegalMatter('conveyanceRegistration')
                            }
                          >
                            <Send className="h-4 w-4" />
                            {legalConveyanceStarted
                              ? 'With Legal'
                              : 'Start conveyance'}
                          </Button>
                        </div>
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
                                : 'Requires full payment and completed Legal conveyance'
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
                      </div>
                    ) : null}
                    {rentalApplication ? (
                      <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
                        <Select
                          value={selectedLegalMatterType}
                          onValueChange={setSelectedLegalMatterType}
                          disabled={isSaving}
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
                            <SelectItem value="sublease">Sublease</SelectItem>
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
                          disabled={isSaving}
                          onClick={() =>
                            void lodgeLegalMatter(selectedLegalMatterType)
                          }
                        >
                          <Gavel className="h-4 w-4" />
                          Lodge with Legal
                        </Button>
                      </div>
                    ) : null}
                  </CardContent>
                </Card>
              ) : null}

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
                      . Sign in as a user with that role to update, reject, or
                      route the request.
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
                          Request is closed with its decision and transaction
                          audit references
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
                    onChange={(event) => setCompletionNotes(event.target.value)}
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
                          !stageConfirmed ||
                          missingApprovedMoveInDate ||
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
                  {missingLegalAgreementReview && !missingApprovedAgreement ? (
                    <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                      Submit the generated agreement to Legal and wait for Legal
                      approval before routing this stage forward.
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
            </div>
          )}
        </div>
      </div>
    </>
  );
}
