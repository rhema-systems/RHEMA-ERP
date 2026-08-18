'use client';

import dynamic from 'next/dynamic';
import React from 'react';
import { useSearchParams } from 'next/navigation';
import { BookTemplate, CheckCircle2, ExternalLink, Eye, FileText, FileUp, Loader2, PenLine, Plus, Save, Send, ShieldCheck, Truck } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { CentralDocumentViewerDialog } from '@/components/document-management/CentralDocumentViewerDialog';
import {
  documentManagementService,
  type CentralDocumentGenerationTemplate,
  type GeneratedCentralDocumentResult,
} from '@/services/document-management.service';
import {
  procedureCaseService,
  type ProcedureCaseDetail,
  type ProcedureCaseDocument,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';
import { getProcedureWorkspaceTerminology } from '@/lib/procedure-workspace';

const ProcedurePdfViewer = dynamic(() => import('@/components/procedures/ProcedurePdfViewer'), {
  ssr: false,
  loading: () => (
    <div className="rounded-md border border-border bg-background p-4 text-sm text-muted-foreground">
      Loading PDF viewer...
    </div>
  ),
});

interface ProcedureCaseWorkspaceProps {
  module: 'Legal' | 'Estate' | 'Facilities' | 'PropertyManagement' | 'Planning';
  entityType: string;
  defaultTitle: string;
  workspaceType?: string;
}

const LAND_FEE_ENTITY_TYPES = new Set([
  'EstateLandsPartiallyServiced',
  'EstateTraditionalLands',
  'EstateTenancyRegularisation',
]);

const CHANGE_OF_USE_ENTITY_TYPES = new Set(['EstateChangeOfUse']);

const GENERATED_DOCUMENT_MODULES = new Set(['Estate', 'Legal']);

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

const formatCalculatedAmount = (value: number | null, decimals = 2): string => {
  if (value === null || !Number.isFinite(value)) {
    return '';
  }

  return value.toFixed(decimals);
};

const PORTAL_RECIPIENT_FIELD_TOKENS = [
  'portalrecipient',
  'portalidentity',
  'portalaccount',
  'externalaccount',
  'applicantemail',
  'customeremail',
  'clientemail',
  'email',
  'applicantusername',
  'customerusername',
  'username',
];

const isStablePortalIdentity = (value: string, fieldIdentity: string): boolean => {
  if (value.includes('@')) {
    return true;
  }

  const looksLikeUsernameField =
    fieldIdentity.includes('username') ||
    fieldIdentity.includes('portal') ||
    fieldIdentity.includes('externalaccount');

  return looksLikeUsernameField && !/\s/.test(value);
};

const resolveProcedurePortalRecipient = (procedureCase: ProcedureCaseDetail): string => {
  for (const field of procedureCase.fields) {
    const value = field.value?.trim();
    if (!value) {
      continue;
    }

    const fieldIdentity = `${field.key} ${field.label}`.replace(/[^a-z0-9]/gi, '').toLowerCase();
    if (
      PORTAL_RECIPIENT_FIELD_TOKENS.some((token) => fieldIdentity.includes(token)) &&
      isStablePortalIdentity(value, fieldIdentity)
    ) {
      return value;
    }
  }

  const applicantName = procedureCase.applicantName?.trim() ?? '';
  return applicantName.includes('@') ? applicantName : '';
};

export function ProcedureCaseWorkspace({
  module,
  entityType,
  defaultTitle,
  workspaceType,
}: ProcedureCaseWorkspaceProps) {
  const searchParams = useSearchParams();
  const terminology = getProcedureWorkspaceTerminology(workspaceType);
  const requestedCaseId = searchParams.get('caseId');
  const prefillSignature = searchParams.toString();
  const prefilledCase = React.useMemo(() => ({
    title: searchParams.get('title') || defaultTitle,
    referenceNumber: searchParams.get('referenceNumber') || '',
    applicantName: searchParams.get('applicantName') || '',
    sourceDepartment: searchParams.get('sourceDepartment') || '',
    receivedDate: searchParams.get('receivedDate') || '',
    description: searchParams.get('description') || '',
  }), [defaultTitle, prefillSignature, searchParams]);
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
  const [selectedCase, setSelectedCase] = React.useState<ProcedureCaseDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [documentFiles, setDocumentFiles] = React.useState<Record<string, File | null>>({});
  const [previewDocumentId, setPreviewDocumentId] = React.useState<string | null>(null);
  const [generationTemplates, setGenerationTemplates] = React.useState<CentralDocumentGenerationTemplate[]>([]);
  const [selectedGenerationTemplate, setSelectedGenerationTemplate] = React.useState<string>('');
  const [generatedDocument, setGeneratedDocument] = React.useState<GeneratedCentralDocumentResult | null>(null);
  const [isGeneratingDocument, setIsGeneratingDocument] = React.useState(false);
  const [isUpdatingDocumentWorkflow, setIsUpdatingDocumentWorkflow] = React.useState(false);
  const [isGeneratedViewerOpen, setIsGeneratedViewerOpen] = React.useState(false);
  const [dispatchDetails, setDispatchDetails] = React.useState({
    channel: 'Email / Print',
    recipient: '',
    reference: '',
    notes: '',
  });
  const [newCase, setNewCase] = React.useState({
    title: prefilledCase.title,
    referenceNumber: prefilledCase.referenceNumber,
    applicantName: prefilledCase.applicantName,
    sourceDepartment: prefilledCase.sourceDepartment,
    receivedDate: prefilledCase.receivedDate,
    description: prefilledCase.description,
  });
  const appliedPrefillSignatureRef = React.useRef('');
  const supportsGeneratedDocuments = GENERATED_DOCUMENT_MODULES.has(module);
  const generatedDocumentSourceLabel =
    module === 'Legal'
      ? 'Source: Legal Department -> Central DMS'
      : 'Source: Estate / Facility -> Central DMS';
  const generatedDocumentPreparedBy =
    selectedCase?.sourceDepartment || (module === 'Legal' ? 'Legal Department' : 'Estate Section');

  const currentStageItems = React.useMemo(
    () => selectedCase?.checklistItems.filter((item) => item.stageIndex === selectedCase.currentStageIndex) ?? [],
    [selectedCase]
  );

  const isPdfDocument = (document: ProcedureCaseDocument) => {
    const value = `${document.fileName ?? ''} ${document.fileUrl ?? ''}`.toLowerCase();
    return value.includes('.pdf');
  };

  const loadCases = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await procedureCaseService.listCases(module, entityType);
      setCases(data);

      // Notifications and handoff links pass caseId so reviewers land on the exact Estate procedure case.
      const targetCaseId = requestedCaseId && data.some((item) => item.id === requestedCaseId)
        ? requestedCaseId
        : data[0]?.id;

      if (targetCaseId && selectedCase?.id !== targetCaseId) {
        const detail = await procedureCaseService.getCase(targetCaseId);
        setSelectedCase(detail);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load workspace records.');
    } finally {
      setIsLoading(false);
    }
  }, [entityType, module, requestedCaseId, selectedCase?.id]);

  React.useEffect(() => {
    void loadCases();
  }, [loadCases]);

  React.useEffect(() => {
    if (requestedCaseId || appliedPrefillSignatureRef.current === prefillSignature) {
      return;
    }

    const hasPrefill =
      Boolean(prefilledCase.referenceNumber) ||
      Boolean(prefilledCase.applicantName) ||
      Boolean(prefilledCase.sourceDepartment) ||
      Boolean(prefilledCase.receivedDate) ||
      Boolean(prefilledCase.description) ||
      Object.keys(prefilledFieldValues).length > 0;
    if (!hasPrefill) {
      return;
    }

    appliedPrefillSignatureRef.current = prefillSignature;
    setNewCase(prefilledCase);
  }, [
    prefillSignature,
    prefilledCase,
    prefilledFieldValues,
    requestedCaseId,
  ]);

  React.useEffect(() => {
    if (!supportsGeneratedDocuments) {
      return;
    }

    let mounted = true;
    const loadTemplates = async () => {
      try {
        const templates = await documentManagementService.getGenerationTemplates(module);
        if (!mounted) {
          return;
        }

        setGenerationTemplates(templates);
        setSelectedGenerationTemplate((current) => current || templates[0]?.templateCode || '');
      } catch (err) {
        if (mounted) {
          setError(err instanceof Error ? err.message : `Unable to load ${module} document templates.`);
        }
      }
    };

    void loadTemplates();

    return () => {
      mounted = false;
    };
  }, [module, supportsGeneratedDocuments]);

  React.useEffect(() => {
    if (!selectedCase || (!LAND_FEE_ENTITY_TYPES.has(entityType) && !CHANGE_OF_USE_ENTITY_TYPES.has(entityType))) {
      return;
    }

    setSelectedCase((current) => {
      if (!current || current.id !== selectedCase.id) {
        return current;
      }

      const values = new Map(current.fields.map((field) => [field.key, field.value ?? '']));
      const plotSizeAcres = parseAmount(values.get('plotSizeAcres'));
      const lmfRatePerAcre = parseAmount(values.get('lmfRatePerAcre'));
      const groundRentRatePerAcre = parseAmount(values.get('groundRentRatePerAcre'));
      const existingLmfRatePerAcre = parseAmount(values.get('existingLmfRatePerAcre'));
      const newLmfRatePerAcre = parseAmount(values.get('newLmfRatePerAcre'));
      const newGroundRentRatePerAcre = parseAmount(values.get('newGroundRentRatePerAcre'));

      const calculatedValues = new Map<string, string>();
      const plotSizeHectares = plotSizeAcres === null ? null : plotSizeAcres * 0.40468564224;
      calculatedValues.set('plotSizeHectares', formatCalculatedAmount(plotSizeHectares, 4));

      // Estate manuals require these calculations before proposal letters are generated and sent for payment.
      const landManagementFee = plotSizeAcres !== null && lmfRatePerAcre !== null
        ? plotSizeAcres * lmfRatePerAcre
        : null;
      calculatedValues.set('landManagementFeePayable', formatCalculatedAmount(landManagementFee));

      const groundRentComputed = plotSizeAcres !== null && groundRentRatePerAcre !== null
        ? plotSizeAcres * groundRentRatePerAcre
        : null;
      calculatedValues.set('groundRentComputed', formatCalculatedAmount(groundRentComputed, 3));
      calculatedValues.set(
        'groundRentPayable',
        formatCalculatedAmount(groundRentComputed === null ? null : Math.ceil(groundRentComputed))
      );

      const changeOfUseFee = plotSizeAcres !== null && existingLmfRatePerAcre !== null && newLmfRatePerAcre !== null
        ? Math.max((newLmfRatePerAcre - existingLmfRatePerAcre) * plotSizeAcres, 0)
        : null;
      calculatedValues.set('changeOfUseFeePayable', formatCalculatedAmount(changeOfUseFee));

      const newGroundRent = plotSizeAcres !== null && newGroundRentRatePerAcre !== null
        ? plotSizeAcres * newGroundRentRatePerAcre
        : null;
      calculatedValues.set('newGroundRentPayable', formatCalculatedAmount(newGroundRent === null ? null : Math.ceil(newGroundRent)));

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
      setError(err instanceof Error ? err.message : 'Unable to open the workspace record.');
    } finally {
      setIsSaving(false);
    }
  };

  const createCase = async () => {
    setIsSaving(true);
    setError(null);
    try {
      const created = await procedureCaseService.createCase({
        module,
        entityType,
        ...newCase,
        fieldValues: prefilledFieldValues,
      });
      setSelectedCase(created);
      setNewCase({
        title: defaultTitle,
        referenceNumber: '',
        applicantName: '',
        sourceDepartment: '',
        receivedDate: '',
        description: '',
      });
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create the workspace record.');
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
        fields: current.fields.map((field) => (field.key === key ? { ...field, value } : field)),
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
      const fieldValues = Object.fromEntries(selectedCase.fields.map((field) => [field.key, field.value ?? null]));
      const updated = await procedureCaseService.updateFields(selectedCase.id, {
        fieldValues,
        referenceNumber: selectedCase.referenceNumber,
        applicantName: selectedCase.applicantName,
        sourceDepartment: selectedCase.sourceDepartment,
        receivedDate: selectedCase.receivedDate,
        description: selectedCase.description,
      });
      setSelectedCase(updated);
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to save intake fields.');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleChecklist = async (checklistItemId: string, isCompleted: boolean) => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.updateChecklistItem(selectedCase.id, checklistItemId, isCompleted);
      setSelectedCase(updated);
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to update checklist.');
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
      const updated = selectedFile
        ? await procedureCaseService.uploadDocument(selectedCase.id, document.id, selectedFile, document.notes)
        : await procedureCaseService.attachDocument(selectedCase.id, document.id, {
            fileName: document.fileName,
            fileUrl: document.fileUrl,
            notes: document.notes,
          });

      setSelectedCase(updated);
      setDocumentFiles((current) => ({ ...current, [document.id]: null }));
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to save document.');
    } finally {
      setIsSaving(false);
    }
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
      const blob = await procedureCaseService.downloadDocumentContent(selectedCase.id, document.id);
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
    const mergeValueByKey = new Map(selectedCase.fields.map((field) => [field.key, field.value ?? '']));
    const setMergeAlias = (alias: string, ...keys: string[]) => {
      const value = keys.map((key) => mergeValueByKey.get(key)).find((item) => item);
      if (value) {
        mergeValues[alias] = value;
      }
    };

    setMergeAlias('ApplicantName', 'applicantName');
    setMergeAlias('PropertyNumber', 'propertyNumber', 'housePlotShopNumber', 'unitNumber');
    setMergeAlias('HousePlotShopNumber', 'housePlotShopNumber', 'propertyNumber', 'unitNumber');
    setMergeAlias('TransferorName', 'transferorName', 'oldLesseeName');
    setMergeAlias('TransfereeName', 'transfereeName', 'newLesseeName');
    setMergeAlias('NewLesseeAddress', 'newLesseeAddress', 'addressOnRecord');
    setMergeAlias('TransferEffectiveDate', 'transferEffectiveDate');
    setMergeAlias('TransferDeclarationReference', 'transferDeclarationReference');
    setMergeAlias('VoluntaryVacationReference', 'voluntaryVacationReference');
    setMergeAlias('HosFormReference', 'hosFormReference');
    setMergeAlias('HouseType', 'houseType');
    setMergeAlias('PurchaseAmount', 'purchaseAmount', 'sellingPrice', 'considerationAmount');
    setMergeAlias('PurchaseDate', 'purchaseDate');
    setMergeAlias('SopSectionReference', 'sopSectionReference');
    setMergeAlias('ApprovedFeeScheduleReference', 'approvedFeeScheduleReference', 'approvedRateReference');
    setMergeAlias('DocumentTemplateReference', 'documentTemplateReference');
    setMergeAlias('FinanceReference', 'financeReference', 'feeReference');
    setMergeAlias('LegalReference', 'legalReference');
    setMergeAlias('RecordsReference', 'estateRecordsReference', 'recordsUpdateReference', 'registerReference');
    setMergeAlias('ReportReference', 'reportingReference', 'quarterlyReportReference', 'boardSubmissionReference');
    setMergeAlias('OfferLetterReference', 'offerLetterReference');
    setMergeAlias('RightOfEntryReference', 'rightOfEntryReference');
    setMergeAlias('LeaseRequestFormReference', 'leaseRequestFormReference');
    setMergeAlias('RegisteredLeaseReference', 'registeredLeaseReference');
    setMergeAlias('LandUse', 'landUse');
    setMergeAlias('Premium', 'landManagementFeePayable', 'renewalPremium', 'transferFeePayable');
    setMergeAlias('GroundRent', 'groundRentPayable', 'improvedGroundRent');
    setMergeAlias('PaymentFrequency', 'paymentFrequency');
    setMergeAlias('LeaseTerm', 'leaseTerm', 'leaseTermYears');
    setMergeAlias('MoveInDate', 'moveInDate', 'dateOfTenancy', 'leaseCommencementDate');
    setMergeAlias('OriginalLeaseReference', 'originalLeaseReference', 'registeredLeaseReference');
    setMergeAlias('VariationReason', 'variationReason', 'leaseVariationReason');
    setMergeAlias('VendorName', 'vendorName', 'ownerName');
    setMergeAlias('AgreedAmount', 'agreedAmount', 'considerationAmount', 'purchaseAmount');
    setMergeAlias('PaymentBasis', 'paymentBasis', 'vendorPaymentMethod');
    setMergeAlias('ApprovalReference', 'approvalReference', 'mdApprovalReference');
    setMergeAlias('OfferExpiryDate', 'offerExpiryDate', 'paymentDeadline');
    setMergeAlias('CaseReference', 'referenceNumber', 'fileReference');
    setMergeAlias('InstrumentType', 'instrumentType', 'transferProcessType', 'mortgageType', 'housingRequestType');
    setMergeAlias('ScheduleReference', 'scheduleReference');
    setMergeAlias('ClientExecutionDate', 'clientExecutionDate');
    setMergeAlias('MortgageeName', 'mortgageeName');
    setMergeAlias('PaymentReceiptReference', 'paymentReceiptReference', 'transferFeeReceipt', 'feeReference');
    setMergeAlias('MortgageLetterReference', 'mortgageLetterReference');
    setMergeAlias('MdApprovalReference', 'mdApprovalReference', 'approvalReference');
    setMergeAlias('TransferFeeReceipt', 'transferFeeReceipt', 'paymentReceiptReference');
    setMergeAlias('TerminationReason', 'terminationReason');
    setMergeAlias('SiteReportReference', 'siteReportReference');
    setMergeAlias('NoticePostingStartDate', 'noticePostingStartDate');
    setMergeAlias('NoticePostingEndDate', 'noticePostingEndDate');
    setMergeAlias('RecognitionApplicantName', 'recognitionApplicantName', 'applicantName');
    setMergeAlias('RecognitionPaymentStatus', 'recognitionPaymentStatus', 'paymentStatus');
    setMergeAlias('RecognitionDocumentReference', 'recognitionDocumentReference');
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
      const result = await documentManagementService.generateDocumentFromTemplate({
        templateCode: selectedGenerationTemplate,
        sourceModule: module,
        sourceLabel: generatedDocumentSourceLabel,
        sourceEntityType: entityType,
        sourceRecordReference: selectedCase.referenceNumber || selectedCase.title,
        sourceRecordId: selectedCase.id,
        caseTitle: selectedCase.title,
        caseReference: selectedCase.referenceNumber || selectedCase.title,
        applicantName: selectedCase.applicantName || undefined,
        preparedBy: generatedDocumentPreparedBy,
        purpose: selectedCase.currentStageName,
        mergeValues,
      });
      setGeneratedDocument(result);
      setDispatchDetails((current) => ({
        ...current,
        channel: result.template.defaultDispatchChannel || current.channel,
        recipient: resolveProcedurePortalRecipient(selectedCase) || current.recipient,
      }));
      setIsGeneratedViewerOpen(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : `Unable to generate ${module} document.`);
    } finally {
      setIsGeneratingDocument(false);
    }
  };

  const updateGeneratedDocumentWorkflow = async (
    action: 'SubmitForApproval' | 'Approve' | 'Sign' | 'Dispatch' | 'Return'
  ) => {
    if (!generatedDocument) {
      return;
    }

    if (action === 'Dispatch' && !dispatchDetails.recipient.trim()) {
      setError('Enter the portal recipient email or username before dispatching this document.');
      return;
    }

    setIsUpdatingDocumentWorkflow(true);
    setError(null);
    try {
      const result = await documentManagementService.updateGeneratedDocumentWorkflow(
        generatedDocument.record.id,
        {
          action,
          notes: dispatchDetails.notes,
          signatureRole: generatedDocument.template.signatureRole || undefined,
          dispatchChannel: dispatchDetails.channel,
          dispatchedTo: dispatchDetails.recipient,
          dispatchReference: dispatchDetails.reference,
        }
      );
      setGeneratedDocument((current) =>
        current
          ? {
              ...current,
              record: result.record,
              version: result.version ?? current.version,
            }
          : current
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to update generated document workflow.');
    } finally {
      setIsUpdatingDocumentWorkflow(false);
    }
  };

  const completeStage = async () => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.completeStage(selectedCase.id, 'Stage completed from workspace.');
      setSelectedCase(updated);
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to submit current stage.');
    } finally {
      setIsSaving(false);
    }
  };

  const renderField = (field: ProcedureCaseDetail['fields'][number]) => {
    const isCalculated = CALCULATED_PROCEDURE_FIELD_KEYS.has(field.key);
    const isDisabled = !selectedCase?.canEditCurrentStage || isCalculated;
    const fieldType = field.fieldType.toLowerCase();

    if (fieldType === 'select' && field.options?.length) {
      return (
        <Select
          key={field.id}
          value={field.value ?? undefined}
          disabled={isDisabled}
          onValueChange={(value) => updateFieldValue(field.key, value)}
        >
          <SelectTrigger>
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
      );
    }

    if (fieldType === 'textarea') {
      return (
        <Textarea
          key={field.id}
          placeholder={field.label}
          value={field.value ?? ''}
          disabled={isDisabled}
          onChange={(event) => updateFieldValue(field.key, event.target.value)}
        />
      );
    }

    return (
      <Input
        key={field.id}
        type={fieldType === 'date' ? 'date' : fieldType === 'number' || fieldType === 'currency' ? 'number' : 'text'}
        placeholder={field.label}
        value={field.value ?? ''}
        disabled={isDisabled}
        step={fieldType === 'currency' || fieldType === 'number' ? '0.01' : undefined}
        onChange={(event) => updateFieldValue(field.key, event.target.value)}
      />
    );
  };

  return (
    <Card className="border-border bg-card text-card-foreground">
      <CardHeader>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <CardTitle>{terminology.title}</CardTitle>
          </div>
          <Badge variant={selectedCase?.usesConfiguredWorkflow ? 'default' : 'outline'}>
            {selectedCase?.usesConfiguredWorkflow ? 'Administration workflow' : 'Procedure stages'}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {error ? (
          <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
            {error}
          </div>
        ) : null}

        <div className="grid gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
          <div className="space-y-4">
            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3 flex items-center justify-between gap-2">
                <h2 className="text-sm font-semibold">
                  {terminology.collectionLabel}
                </h2>
                {isLoading ? <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" /> : null}
              </div>
              <div className="space-y-2">
                {cases.length === 0 && !isLoading ? (
                  <p className="text-sm text-muted-foreground">
                    {terminology.emptyMessage}
                  </p>
                ) : null}
                {cases.map((procedureCase) => (
                  <button
                    key={procedureCase.id}
                    type="button"
                    className={`w-full rounded-md border p-3 text-left text-sm transition-colors ${
                      selectedCase?.id === procedureCase.id
                        ? 'border-primary bg-primary/10'
                        : 'border-border bg-card hover:bg-muted'
                    }`}
                    onClick={() => void selectCase(procedureCase.id)}
                  >
                    <div className="font-medium">{procedureCase.referenceNumber || procedureCase.title}</div>
                    <div className="mt-1 text-xs text-muted-foreground">{procedureCase.currentStageName}</div>
                    <div className="mt-2 flex flex-wrap gap-1">
                      <Badge variant="outline">{procedureCase.status}</Badge>
                      {procedureCase.currentAssignedRole ? (
                        <Badge variant="secondary">{procedureCase.currentAssignedRole}</Badge>
                      ) : null}
                    </div>
                  </button>
                ))}
              </div>
            </div>

            <div className="rounded-md border border-border bg-background p-4">
              <h2 className="text-sm font-semibold">
                {terminology.createHeading}
              </h2>
              <div className="mt-3 space-y-3">
                <Input value={newCase.title} onChange={(event) => setNewCase({ ...newCase, title: event.target.value })} />
                <Input placeholder="Reference number" value={newCase.referenceNumber} onChange={(event) => setNewCase({ ...newCase, referenceNumber: event.target.value })} />
                <Input placeholder="Applicant / party name" value={newCase.applicantName} onChange={(event) => setNewCase({ ...newCase, applicantName: event.target.value })} />
                <Input placeholder="Source department" value={newCase.sourceDepartment} onChange={(event) => setNewCase({ ...newCase, sourceDepartment: event.target.value })} />
                <Input type="date" value={newCase.receivedDate} onChange={(event) => setNewCase({ ...newCase, receivedDate: event.target.value })} />
                <Textarea placeholder="Description" value={newCase.description} onChange={(event) => setNewCase({ ...newCase, description: event.target.value })} />
                <Button className="w-full gap-2" onClick={() => void createCase()} disabled={isSaving}>
                  <Plus className="h-4 w-4" />
                  {terminology.createLabel}
                </Button>
              </div>
            </div>
          </div>

          {selectedCase ? (
            <div className="space-y-4">
              <div className="rounded-md border border-border bg-background p-4">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div>
                    <h2 className="text-base font-semibold">{selectedCase.referenceNumber || selectedCase.title}</h2>
                    <p className="mt-1 text-sm text-muted-foreground">{selectedCase.currentStageName}</p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant="outline">{selectedCase.status}</Badge>
                    {selectedCase.currentAssignedRole ? <Badge>{selectedCase.currentAssignedRole}</Badge> : null}
                    <Badge variant={selectedCase.canEditCurrentStage ? 'secondary' : 'outline'}>
                      {selectedCase.canEditCurrentStage ? 'Editable' : 'Read only'}
                    </Badge>
                  </div>
                </div>
              </div>

              <div className="rounded-md border border-border bg-background p-4">
                <div className="mb-3 flex items-center justify-between gap-2">
                  <h2 className="text-sm font-semibold">Intake</h2>
                  <Button size="sm" variant="outline" className="gap-2" onClick={() => void saveIntake()} disabled={!selectedCase.canEditCurrentStage || isSaving}>
                    <Save className="h-4 w-4" />
                    Save
                  </Button>
                </div>
                <div className="grid gap-3 md:grid-cols-2">
                  <Input placeholder="Reference number" value={selectedCase.referenceNumber ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, referenceNumber: event.target.value })} />
                  <Input placeholder="Applicant / party name" value={selectedCase.applicantName ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, applicantName: event.target.value })} />
                  <Input placeholder="Source department" value={selectedCase.sourceDepartment ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, sourceDepartment: event.target.value })} />
                  <Input type="date" value={selectedCase.receivedDate?.slice(0, 10) ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, receivedDate: event.target.value })} />
                  {selectedCase.fields.map((field) => renderField(field))}
                </div>
                <Textarea
                  className="mt-3"
                  placeholder="Description"
                  value={selectedCase.description ?? ''}
                  disabled={!selectedCase.canEditCurrentStage}
                  onChange={(event) => setSelectedCase({ ...selectedCase, description: event.target.value })}
                />
              </div>

              {supportsGeneratedDocuments ? (
                <div className="rounded-md border border-border bg-background p-4">
                  <div className="mb-3 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                    <h2 className="text-sm font-semibold">Generated Documents</h2>
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
                            <SelectItem key={template.templateCode} value={template.templateCode}>
                              {template.title}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <Button
                        className="gap-2"
                        onClick={() => void generateProcedureDocument()}
                        disabled={!selectedGenerationTemplate || isGeneratingDocument}
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
                            {generatedDocument.dmsReference} · {generatedDocument.sourceLabel}
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
                                {generatedDocument.template.approvalRole || 'Approval required'}
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
                          <Button asChild size="sm" variant="outline" className="gap-2">
                            <a href={`/document-management/records/${generatedDocument.record.id}`}>
                              <FileText className="h-4 w-4" />
                              DMS record
                            </a>
                          </Button>
                        </div>
                      </div>

                      <div className="grid gap-3 md:grid-cols-3">
                        <Input
                          placeholder="Dispatch channel"
                          value={dispatchDetails.channel}
                          onChange={(event) =>
                            setDispatchDetails((current) => ({
                              ...current,
                              channel: event.target.value,
                            }))
                          }
                        />
                        <Input
                          placeholder="Portal email / username"
                          value={dispatchDetails.recipient}
                          onChange={(event) =>
                            setDispatchDetails((current) => ({
                              ...current,
                              recipient: event.target.value,
                            }))
                          }
                        />
                        <Input
                          placeholder="Dispatch reference"
                          value={dispatchDetails.reference}
                          onChange={(event) =>
                            setDispatchDetails((current) => ({
                              ...current,
                              reference: event.target.value,
                            }))
                          }
                        />
                      </div>
                      <Textarea
                        placeholder="Approval, signature, or dispatch notes"
                        value={dispatchDetails.notes}
                        onChange={(event) =>
                          setDispatchDetails((current) => ({
                            ...current,
                            notes: event.target.value,
                          }))
                        }
                      />
                      <div className="flex flex-wrap gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          className="gap-2"
                          disabled={isUpdatingDocumentWorkflow}
                          onClick={() => void updateGeneratedDocumentWorkflow('SubmitForApproval')}
                        >
                          <Send className="h-4 w-4" />
                          Submit approval
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className="gap-2"
                          disabled={isUpdatingDocumentWorkflow}
                          onClick={() => void updateGeneratedDocumentWorkflow('Approve')}
                        >
                          <ShieldCheck className="h-4 w-4" />
                          Approve
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className="gap-2"
                          disabled={isUpdatingDocumentWorkflow}
                          onClick={() => void updateGeneratedDocumentWorkflow('Sign')}
                        >
                          <PenLine className="h-4 w-4" />
                          Sign
                        </Button>
                        <Button
                          size="sm"
                          className="gap-2"
                          disabled={isUpdatingDocumentWorkflow}
                          onClick={() => void updateGeneratedDocumentWorkflow('Dispatch')}
                        >
                          <Truck className="h-4 w-4" />
                          Dispatch
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={isUpdatingDocumentWorkflow}
                          onClick={() => void updateGeneratedDocumentWorkflow('Return')}
                        >
                          Return
                        </Button>
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
                        title: generatedDocument.record.title,
                        fileName: generatedDocument.version.fileName,
                        repositoryPath: generatedDocument.version.repositoryPath || generatedDocument.pdfUrl,
                        renditionPath: generatedDocument.version.renditionPath || generatedDocument.pdfUrl,
                        contentType: generatedDocument.version.contentType,
                        sourceLabel: generatedDocument.sourceLabel,
                        version: generatedDocument.version.versionNumber,
                      }
                    : null
                }
              />

              <div className="rounded-md border border-border bg-background p-4">
                <h2 className="text-sm font-semibold">Current Stage Checklist</h2>
                <div className="mt-3 space-y-3">
                  {currentStageItems.map((item) => (
                    <label key={item.id} className="flex items-start gap-3 rounded-md border border-border bg-card p-3 text-sm">
                      <Checkbox
                        checked={item.isCompleted}
                        disabled={!selectedCase.canEditCurrentStage || isSaving}
                        onCheckedChange={(checked) => void toggleChecklist(item.id, checked === true)}
                      />
                      <span className={item.isCompleted ? 'text-muted-foreground line-through' : ''}>{item.text}</span>
                    </label>
                  ))}
                </div>
              </div>

              <div className="rounded-md border border-border bg-background p-4">
                <h2 className="text-sm font-semibold">Documents</h2>
                <div className="mt-3 grid gap-3 md:grid-cols-2">
                  {selectedCase.documents.map((document) => (
                    <div key={document.id} className="rounded-md border border-border bg-card p-3">
                      <div className="flex items-start justify-between gap-2">
                        <div>
                          <div className="text-sm font-medium">{document.name}</div>
                          <div className="mt-1 text-xs text-muted-foreground">{document.requiredFrom}</div>
                        </div>
                        <Badge variant={document.isMandatory ? 'default' : 'outline'}>
                          {document.isMandatory ? 'Required' : 'Optional'}
                        </Badge>
                      </div>
                      <div className="mt-3 space-y-2">
                        {document.fileName ? (
                          <div className="rounded-md border border-border bg-background p-2 text-xs">
                            <div className="font-medium text-foreground">{document.fileName}</div>
                            {document.fileUrl ? (
                              <div className="mt-2 flex flex-wrap gap-2">
                                <button
                                  type="button"
                                  className="inline-flex items-center gap-1 text-primary hover:underline"
                                  onClick={() => void openDocument(document)}
                                >
                                  <ExternalLink className="h-3 w-3" />
                                  Open uploaded file
                                </button>
                                {isPdfDocument(document) ? (
                                  <button
                                    type="button"
                                    className="inline-flex items-center gap-1 text-primary hover:underline"
                                    onClick={() =>
                                      setPreviewDocumentId((current) => (current === document.id ? null : document.id))
                                    }
                                  >
                                    <Eye className="h-3 w-3" />
                                    {previewDocumentId === document.id ? 'Hide preview' : 'View inline'}
                                  </button>
                                ) : null}
                              </div>
                            ) : null}
                          </div>
                        ) : null}
                        {previewDocumentId === document.id && document.fileUrl && isPdfDocument(document) ? (
                          <ProcedurePdfViewer fileUrl={document.fileUrl} fileName={document.fileName} />
                        ) : null}
                        <Input
                          type="file"
                          accept=".pdf,.doc,.docx,.txt,.rtf,.jpg,.jpeg,.png,.gif,.bmp,.svg,.webp,.ico"
                          disabled={!selectedCase.canEditCurrentStage || isSaving}
                          onChange={(event) => selectDocumentFile(document.id, event.target.files?.[0] ?? null)}
                        />
                        {documentFiles[document.id] ? (
                          <div className="text-xs text-muted-foreground">
                            Selected: {documentFiles[document.id]?.name}
                          </div>
                        ) : null}
                        <Textarea placeholder="Notes" value={document.notes ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => updateDocumentNotes(document.id, event.target.value)} />
                        <Button size="sm" variant="outline" className="w-full gap-2" disabled={!selectedCase.canEditCurrentStage || isSaving} onClick={() => void saveDocument(document)}>
                          <FileUp className="h-4 w-4" />
                          {documentFiles[document.id] ? 'Upload document' : 'Save notes'}
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div className="flex flex-col gap-3 rounded-md border border-border bg-background p-4 md:flex-row md:items-center md:justify-between">
                <div className="flex items-start gap-2 text-sm text-muted-foreground">
                  <CheckCircle2 className="mt-0.5 h-4 w-4 text-primary" />
                  <span>All current-stage checklist items must be complete before submission.</span>
                </div>
                <Button className="gap-2" onClick={() => void completeStage()} disabled={!selectedCase.canEditCurrentStage || isSaving || currentStageItems.some((item) => !item.isCompleted)}>
                  <Send className="h-4 w-4" />
                  Submit stage
                </Button>
              </div>
            </div>
          ) : (
            <div className="rounded-md border border-border bg-background p-8 text-center text-sm text-muted-foreground">
              {terminology.selectMessage}
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
