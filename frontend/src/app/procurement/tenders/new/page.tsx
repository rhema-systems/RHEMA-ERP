'use client';

import { useState, useEffect, Suspense } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { ArrowLeft, ArrowRight, Save, FileText, Package, Upload, DollarSign, Users, CheckCircle2, ClipboardList } from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type CreateTenderItemDto, type TenderDocumentDto, type CreateTenderLotDto, type TenderLotDto } from '@/services/tenderService';
import { purchasingService, type PurchaseRequisitionDetailDto } from '@/services/purchasingService';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  buildCreateTenderDto,
  buildUpdateTenderDto,
} from '@/lib/tender-form-payload';

// Import step components (we'll create these)
import BasicInformation from '@/components/procurement/tenders/BasicInformation';
import TenderLots from '@/components/procurement/tenders/TenderLots';
import TenderDocuments from '@/components/procurement/tenders/TenderDocuments';
import TenderFees from '@/components/procurement/tenders/TenderFees';
import TenderInvitations from '@/components/procurement/tenders/TenderInvitations';
import TenderReview from '@/components/procurement/tenders/TenderReview';
import TenderProposals from '@/components/procurement/tenders/TenderProposals';

const STEPS = [
  { id: 1, name: 'Basic Information', icon: FileText, component: 'BasicInformation' },
  { id: 2, name: 'Tender Lots', icon: Package, component: 'TenderLots' },
  { id: 3, name: 'Proposal', icon: ClipboardList, component: 'TenderProposals' },
  { id: 4, name: 'Documents', icon: Upload, component: 'TenderDocuments' },
  { id: 5, name: 'Fees', icon: DollarSign, component: 'TenderFees' },
  { id: 6, name: 'Invitations', icon: Users, component: 'TenderInvitations' },
  { id: 7, name: 'Review & Submit', icon: CheckCircle2, component: 'TenderReview' },
];

export interface DocumentRequirement {
  documentType: string;
  documentName: string;
  isRequired: boolean;
  description: string;
  maxFileSizeMB: number;
  allowedFileTypes: string;
}

// Interface for LOT data in the form (includes items within the LOT)
export interface TenderLotFormData {
  id?: string;  // Set after saving to backend
  lotNumber: number;
  lotCode: string;
  title: string;
  description?: string;
  estimatedValue?: number;
  currency?: string;
  requiredDeliveryDate?: string;
  deliveryLocation?: string;
  specifications?: string;
  notes?: string;
  displayOrder: number;
  // Items within this LOT
  items: TenderLotItemFormData[];
}

// Interface for item data within a LOT
export interface TenderLotItemFormData {
  id?: string;  // Set after saving to backend
  lineNumber: number;
  itemCode?: string;
  description: string;
  quantity: number;
  unitOfMeasure?: string;
  specifications?: string;
  requiredDeliveryDate?: string;
  deliveryLocation?: string;
}

export interface TenderFormItem extends CreateTenderItemDto {
  id?: string;
}

export interface TenderFormData {
  // Basic Information
  title: string;
  description: string;
  tenderType: string;
  submissionDeadline: string;
  openingDate: string;
  estimatedValue: number | null;
  currency: string;
  minimumPerformanceRating: number | null;
  requiresPrequalification: boolean;
  allowPartialBids: boolean;
  priceWeightage: number;
  qualityWeightage: number;
  deliveryWeightage: number;
  experienceWeightage: number;
  evaluationCriteriaJson: string;
  notes: string;
  termsAndConditions: string;

  // QCBS Evaluation Settings
  useQCBSEvaluation: boolean;
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;

  // Evaluation Template
  evaluationTemplateId: string | null;
  evaluationTemplateName: string;

  // Tender LOTs (groups of items that must be bid together)
  lots: TenderLotFormData[];

  // Tender Items (for backward compatibility, items not in any LOT)
  items: TenderFormItem[];

  // Document Requirements (for bidders to upload)
  documentRequirements: DocumentRequirement[];

  // Acceptance Declaration
  requiresAcceptanceDeclaration: boolean;
  acceptanceDeclarationFile: File | null;
  acceptanceDeclarationDocumentName: string;

  // Proposal Template (for bidders to download - single template for both technical & commercial proposals)
  proposalTemplateFile: File | null;
  proposalTemplateName: string;

  // Documents (will be uploaded separately)
  documents: Array<{
    documentType: string;
    documentName: string;
    file: File;
  }>;

  // Fees
  fees: Array<{
    id?: string;
    feeType: string;
    amount: number;
    currency: string;
    paymentMethod: string;
    isMandatory: boolean;
    dueDate: string;
    description: string;
    bankAccountDetails?: string;
    receivingAccountId?: string;
    revenueAccountId?: string;
  }>;

  // Invitations
  invitations: Array<{
    id?: string;
    businessPartnerId: string;
    businessPartnerName: string;
    invitedDate: string;
    status: string;
    viewedDate?: string;
    responseDate?: string;
    declineReason?: string;
  }>;
  invitedBusinessPartnerIds?: string[];
  sendNotifications?: boolean;
}

// Validation functions for each step
const validateStep1 = (formData: TenderFormData): string[] => {
  const errors: string[] = [];
  if (!formData.title?.trim()) errors.push('Title is required');
  if (!formData.tenderType) errors.push('Tender Type is required');
  if (!formData.submissionDeadline) errors.push('Submission Deadline is required');

  // Validate evaluation template is selected
  if (!formData.evaluationTemplateId) {
    errors.push('Evaluation Template is required');
  }

  return errors;
};

const validateStep2 = (formData: TenderFormData): string[] => {
  const errors: string[] = [];
  if ((!formData.lots || formData.lots.length === 0) && (!formData.items || formData.items.length === 0)) {
    errors.push('At least one LOT with items is required');
  }
  // Check that each LOT has at least one item
  if (formData.lots && formData.lots.length > 0) {
    formData.lots.forEach((lot, index) => {
      if (!lot.items || lot.items.length === 0) {
        errors.push(`LOT ${lot.lotCode || index + 1} must have at least one item`);
      }
    });
  }
  return errors;
};

const validateStep3 = (formData: TenderFormData): string[] => {
  // Documents are optional
  return [];
};

const validateStep4 = (formData: TenderFormData): string[] => {
  // Proposals are optional
  return [];
};

const validateStep5 = (formData: TenderFormData): string[] => {
  // Fees are optional
  return [];
};

const validateStep6 = (formData: TenderFormData): string[] => {
  // Invitations are optional (can be sent later)
  return [];
};

function NewTenderPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const fromRequisitionId = searchParams?.get('fromRequisitionId');
  const [currentStep, setCurrentStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [savingDraft, setSavingDraft] = useState(false);
  const [tenderId, setTenderId] = useState<string | null>(null);
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [tenderDocuments, setTenderDocuments] = useState<TenderDocumentDto[]>([]);
  const [sourceRequisition, setSourceRequisition] = useState<PurchaseRequisitionDetailDto | null>(null);
  const [prefilledFromRequisition, setPrefilledFromRequisition] = useState(false);
  const [autoCreatedLotsFromRequisition, setAutoCreatedLotsFromRequisition] = useState(false);

  // Load tenderId from URL query parameter if it exists
  useEffect(() => {
    const tenderIdFromUrl = searchParams?.get('tenderId');
    if (tenderIdFromUrl) {
      console.log('Loading tenderId from URL:', tenderIdFromUrl);
      setTenderId(tenderIdFromUrl);
    }
  }, [searchParams]);

  useEffect(() => {
    const prefillFromRequisition = async () => {
      if (!fromRequisitionId || prefilledFromRequisition || tenderId) return;

      try {
        const pr = await purchasingService.getPurchaseRequisitionById(fromRequisitionId);
        setSourceRequisition(pr);

        const mappedLotItems: TenderLotItemFormData[] = (pr.items || []).map((i, idx) => ({
          lineNumber: idx + 1,
          itemCode: i.itemCode || undefined,
          description: i.itemDescription || i.itemName || '',
          quantity: Number(i.quantity ?? 0),
          unitOfMeasure: i.unitOfMeasure || undefined,
          specifications: i.specifications || undefined,
          requiredDeliveryDate: i.requiredDate || '',
          deliveryLocation: '',
        }));
        const sourceCurrency = pr.currency?.trim().toUpperCase() || 'USD';

        const suggestedLot: TenderLotFormData = {
          lotNumber: 1,
          lotCode: 'LOT-001',
          title: `Lot 1 (${pr.requisitionNumber})`,
          description: `Auto-created from Purchase Requisition ${pr.requisitionNumber}`,
          estimatedValue: pr.totalAmount ?? undefined,
          currency: sourceCurrency,
          requiredDeliveryDate: pr.requiredDate || '',
          deliveryLocation: '',
          specifications: '',
          notes: '',
          displayOrder: 0,
          items: mappedLotItems,
        };

        setFormData((prev) => ({
          ...prev,
          title: prev.title?.trim() ? prev.title : `Tender for ${pr.requisitionNumber}`,
          description: prev.description?.trim() ? prev.description : (pr.justification || pr.notes || ''),
          estimatedValue: prev.estimatedValue ?? pr.totalAmount ?? null,
          currency: sourceCurrency,
          lots: (prev.lots && prev.lots.length > 0)
            ? prev.lots.map((lot) => ({ ...lot, currency: sourceCurrency }))
            : [suggestedLot],
          items: [],
          notes: prev.notes?.trim() ? prev.notes : `Source PR: ${pr.requisitionNumber}`,
        }));

        setPrefilledFromRequisition(true);
        setAutoCreatedLotsFromRequisition(false);
      } catch (error: any) {
        console.error('Error prefilling tender from requisition:', error);
        toast.error(error.message || 'Failed to load purchase requisition for Tender');
      }
    };

    prefillFromRequisition();
  }, [fromRequisitionId, prefilledFromRequisition, tenderId]);

  const autoCreateLotsAndItemsFromFormData = async (createdTenderId: string): Promise<boolean> => {
    if (!fromRequisitionId) return true;
    if (autoCreatedLotsFromRequisition) return true;
    if (!formData.lots || formData.lots.length === 0) return true;
    if (formData.lots.every((l) => !!l.id)) {
      setAutoCreatedLotsFromRequisition(true);
      return true;
    }

    try {
      const createdLots: TenderLotFormData[] = [];

      for (const lot of formData.lots) {
        if (lot.id) {
          createdLots.push(lot);
          continue;
        }

        const lotDto: CreateTenderLotDto = {
          lotNumber: lot.lotNumber || 1,
          lotCode: lot.lotCode?.trim() ? lot.lotCode : 'LOT-001',
          title: lot.title,
          description: lot.description,
          estimatedValue: lot.estimatedValue,
          currency: lot.currency || formData.currency,
          requiredDeliveryDate: lot.requiredDeliveryDate || undefined,
          deliveryLocation: lot.deliveryLocation,
          specifications: lot.specifications,
          notes: lot.notes,
          displayOrder: lot.displayOrder,
        };

        const savedLot = await tenderService.addTenderLot(createdTenderId, lotDto);

        const savedItems: TenderLotItemFormData[] = [];
        for (const item of lot.items || []) {
          const itemDto: CreateTenderItemDto = {
            lineNumber: item.lineNumber,
            lotId: savedLot.id,
            itemCode: item.itemCode,
            description: item.description,
            quantity: item.quantity,
            unitOfMeasure: item.unitOfMeasure,
            specifications: item.specifications,
            requiredDeliveryDate: item.requiredDeliveryDate ? item.requiredDeliveryDate : null,
            deliveryLocation: item.deliveryLocation,
          };

          const savedItem = await tenderService.addTenderItem(createdTenderId, itemDto);
          savedItems.push({
            id: savedItem.id,
            lineNumber: savedItem.lineNumber,
            itemCode: savedItem.itemCode || undefined,
            description: savedItem.description,
            quantity: savedItem.quantity,
            unitOfMeasure: savedItem.unitOfMeasure || undefined,
            specifications: savedItem.specifications || undefined,
            requiredDeliveryDate: savedItem.requiredDeliveryDate || '',
            deliveryLocation: savedItem.deliveryLocation || '',
          });
        }

        createdLots.push({
          ...lot,
          id: savedLot.id,
          lotNumber: savedLot.lotNumber ?? lot.lotNumber,
          lotCode: savedLot.lotCode ?? lot.lotCode,
          title: savedLot.title ?? lot.title,
          description: savedLot.description ?? lot.description,
          currency: savedLot.currency ?? lot.currency,
          items: savedItems,
        });
      }

      updateFormData({ lots: createdLots, items: [] });
      setAutoCreatedLotsFromRequisition(true);
      toast.success('LOT and items auto-created from PR');
      return true;
    } catch (error: any) {
      console.error('Error auto-creating lot/items from PR:', error);
      toast.error(error?.message || 'Failed to auto-create LOT/items from PR. You can still add them manually.');
      return false;
    }
  };

  // Fetch tender documents when entering review step
  useEffect(() => {
    const fetchTenderDocuments = async () => {
      if (tenderId && currentStep === 7) {
        try {
          const tender = await tenderService.getTenderById(tenderId);
          if (tender.documents) {
            setTenderDocuments(tender.documents);
          }
        } catch (error) {
          console.error('Error fetching tender documents:', error);
        }
      }
    };
    fetchTenderDocuments();
  }, [tenderId, currentStep]);
  
  const [formData, setFormData] = useState<TenderFormData>({
    title: '',
    description: '',
    tenderType: 'ITB',
    submissionDeadline: '',
    openingDate: '',
    estimatedValue: null,
    currency: 'USD',
    minimumPerformanceRating: null,
    requiresPrequalification: false,
    allowPartialBids: false,
    priceWeightage: 40,
    qualityWeightage: 30,
    deliveryWeightage: 20,
    experienceWeightage: 10,
    evaluationCriteriaJson: '',
    notes: '',
    termsAndConditions: '',
    // QCBS defaults
    useQCBSEvaluation: false,
    technicalWeight: 80,
    financialWeight: 20,
    minimumTechnicalScore: 70,
    evaluationTemplateId: null,
    evaluationTemplateName: '',
    lots: [],
    items: [],
    documentRequirements: [],
    requiresAcceptanceDeclaration: false,
    acceptanceDeclarationFile: null,
    acceptanceDeclarationDocumentName: '',
    proposalTemplateFile: null,
    proposalTemplateName: '',
    documents: [],
    fees: [],
    invitations: [],
    invitedBusinessPartnerIds: [],
    sendNotifications: false,
  });

  const updateFormData = (data: Partial<TenderFormData>) => {
    setFormData(prev => ({ ...prev, ...data }));
  };

  const handleNext = async () => {
    // Validate current step
    let errors: string[] = [];
    switch (currentStep) {
      case 1:
        errors = validateStep1(formData);
        break;
      case 2:
        errors = validateStep2(formData);
        break;
      case 3:
        errors = validateStep3(formData);
        break;
      case 4:
        errors = validateStep4(formData);
        break;
      case 5:
        errors = validateStep5(formData);
        break;
      case 6:
        errors = validateStep6(formData);
        break;
    }

    if (errors.length > 0) {
      errors.forEach(error => toast.error(error));
      return;
    }

    // Auto-save when moving from step 1 to step 2 (create the tender if it doesn't exist)
    if (currentStep === 1 && !tenderId) {
      try {
        setSavingDraft(true);
        const createDto = buildCreateTenderDto(
          formData,
          fromRequisitionId || '',
          !fromRequisitionId
        );

        const result = await tenderService.createTender(createDto);
        setTenderId(result.id);
        console.log('Tender created with ID:', result.id);

        // Upload acceptance declaration file if provided
        if (formData.acceptanceDeclarationFile && formData.requiresAcceptanceDeclaration) {
          console.log('Uploading acceptance declaration document...');
          try {
            await tenderService.uploadTenderDocument(
              result.id,
              formData.acceptanceDeclarationFile,
              'AcceptanceDeclaration',
              formData.acceptanceDeclarationDocumentName || formData.acceptanceDeclarationFile.name,
              true // isPublic
            );
            console.log('Acceptance declaration uploaded successfully');
            // Clear the file from state after successful upload to prevent re-uploading
            updateFormData({
              acceptanceDeclarationFile: null,
            });
          } catch (error) {
            console.error('Error uploading acceptance declaration:', error);
            // Don't fail the whole operation if just the file upload fails
          }
        }

        const autoOk = await autoCreateLotsAndItemsFromFormData(result.id);
        if (fromRequisitionId && !autoOk) {
          return;
        }
        toast.success('Draft saved automatically');
      } catch (error: any) {
        console.error('Error auto-saving tender:', error);
        toast.error(error?.message || 'Failed to save tender. Please try again.');
        return; // Don't proceed to next step if save failed
      } finally {
        setSavingDraft(false);
      }
    }

    if (currentStep < STEPS.length) {
      setCurrentStep(currentStep + 1);
    }
  };

  const handlePrevious = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
    }
  };

  const handleSaveDraft = async () => {
    try {
      setSavingDraft(true);

      if (tenderId) {
        // Update existing tender - basic information
        const updateDto = buildUpdateTenderDto(formData);

        console.log('Saving draft for tender:', tenderId);
        console.log('Current items in form state:', formData.items);

        await tenderService.updateTender(tenderId, updateDto);

        // Upload acceptance declaration file if provided and not already uploaded
        if (formData.acceptanceDeclarationFile && formData.requiresAcceptanceDeclaration) {
          console.log('Uploading acceptance declaration document...');
          try {
            await tenderService.uploadTenderDocument(
              tenderId,
              formData.acceptanceDeclarationFile,
              'AcceptanceDeclaration',
              formData.acceptanceDeclarationDocumentName || formData.acceptanceDeclarationFile.name,
              true // isPublic
            );
            console.log('Acceptance declaration uploaded successfully');
            // Clear the file from state after successful upload to prevent re-uploading
            updateFormData({
              acceptanceDeclarationFile: null,
            });
          } catch (error) {
            console.error('Error uploading acceptance declaration:', error);
            toast.error('Draft saved but failed to upload acceptance declaration');
          }
        }

        // Save any items that don't have an ID (not yet saved to backend)
        const unsavedItems = formData.items.filter((item: any) => !item.id);
        console.log('Unsaved items to be added:', unsavedItems);

        for (const item of unsavedItems) {
          console.log('Adding item to tender:', item);
          const savedItem = await tenderService.addTenderItem(tenderId, item);
          console.log('Item saved with ID:', savedItem.id);
        }

        // Save any fees that don't have an ID (not yet saved to backend)
        const unsavedFees = formData.fees.filter((fee: any) => !fee.id);
        for (const fee of unsavedFees) {
          await tenderService.addTenderFee(tenderId, fee);
        }

        // Send invitations if there are any new ones
        if (formData.invitedBusinessPartnerIds && formData.invitedBusinessPartnerIds.length > 0) {
          await tenderService.inviteTenderers(tenderId, {
            businessPartnerIds: formData.invitedBusinessPartnerIds,
            sendNotifications: formData.sendNotifications,
          });
        }

        toast.success('Draft updated successfully');
      } else {
        // Create new tender with items
        const createDto = buildCreateTenderDto(
          formData,
          fromRequisitionId || '',
          !fromRequisitionId
        );

        const result = await tenderService.createTender(createDto);
        setTenderId(result.id);
        const autoOk = await autoCreateLotsAndItemsFromFormData(result.id);
        if (fromRequisitionId && !autoOk) {
          toast.error('Draft saved, but failed to auto-create LOT/items from PR. Please try again.');
          return;
        }

        // Upload acceptance declaration file if provided
        if (formData.acceptanceDeclarationFile && formData.requiresAcceptanceDeclaration) {
          console.log('Uploading acceptance declaration document...');
          try {
            await tenderService.uploadTenderDocument(
              result.id,
              formData.acceptanceDeclarationFile,
              'AcceptanceDeclaration',
              formData.acceptanceDeclarationDocumentName || formData.acceptanceDeclarationFile.name,
              true // isPublic
            );
            console.log('Acceptance declaration uploaded successfully');
            // Clear the file from state after successful upload to prevent re-uploading
            updateFormData({
              acceptanceDeclarationFile: null,
            });
          } catch (error) {
            console.error('Error uploading acceptance declaration:', error);
            toast.error('Draft saved but failed to upload acceptance declaration');
          }
        }

        toast.success('Draft saved successfully. You can now add items, fees, and invitations.');
      }
    } catch (error: any) {
      console.error('Error saving draft:', error);
      const errorMessage = error?.message || 'Failed to save draft';
      toast.error(`Error saving draft: ${errorMessage}`);
    } finally {
      setSavingDraft(false);
    }
  };

  const handleSubmit = () => {
    console.log('🔵 handleSubmit called');
    console.log('🔵 formData:', formData);
    console.log('🔵 tenderId:', tenderId);

    // Validate all steps
    const allErrors = [
      ...validateStep1(formData),
      ...validateStep2(formData),
    ];

    console.log('🔵 Validation errors:', allErrors);

    if (allErrors.length > 0) {
      allErrors.forEach(error => toast.error(error));
      return;
    }

    // Show confirmation dialog
    setShowConfirmDialog(true);
  };

  const getTenderTypeLabel = (type: string) => {
    const typeMap: Record<string, string> = {
      'RFQ': 'Request for Quotation',
      'RFP': 'Request for Proposal',
      'ITB': 'Invitation to Bid',
      'EOI': 'Expression of Interest',
    };
    return typeMap[type] || type;
  };

  const confirmSubmit = async () => {
    console.log('🔵 User confirmed tender creation');

    try {
      setLoading(true);
      console.log('🔵 Starting tender creation...');

      let finalTenderId = tenderId;

      // Create tender if not already created
      if (!finalTenderId) {
        console.log('🔵 No tender ID yet, creating new tender...');
        const createDto = buildCreateTenderDto(
          formData,
          fromRequisitionId || '',
          !fromRequisitionId
        );

        console.log('🔵 createDto:', createDto);
        const result = await tenderService.createTender(createDto);
        console.log('🔵 Tender created:', result);
        finalTenderId = result.id;
        const autoOk = await autoCreateLotsAndItemsFromFormData(finalTenderId);
        if (fromRequisitionId && !autoOk) {
          toast.error('Tender created, but failed to auto-create LOT/items from PR. Please try again.');
          return;
        }

        // Upload acceptance declaration file if provided
        if (formData.acceptanceDeclarationFile && formData.requiresAcceptanceDeclaration) {
          console.log('🔵 Uploading acceptance declaration document...');
          try {
            await tenderService.uploadTenderDocument(
              finalTenderId,
              formData.acceptanceDeclarationFile,
              'AcceptanceDeclaration',
              formData.acceptanceDeclarationDocumentName || formData.acceptanceDeclarationFile.name,
              true // isPublic
            );
            console.log('🔵 Acceptance declaration uploaded successfully');
          } catch (error) {
            console.error('❌ Error uploading acceptance declaration:', error);
            toast.error('Tender created but failed to upload acceptance declaration');
          }
        }
      } else {
        console.log('🔵 Tender already exists with ID:', finalTenderId);
      }

      toast.success('✅ Tender created successfully! You can now add invitations and publish it.');
      console.log('🔵 Redirecting to tender details page...');
      router.push(`/procurement/tenders/${finalTenderId}`);
    } catch (error) {
      console.error('❌ Error creating tender:', error);
      toast.error('Failed to create tender');
    } finally {
      setLoading(false);
      console.log('🔵 handleSubmit completed');
    }
  };

  const calculateProgress = () => {
    return ((currentStep - 1) / (STEPS.length - 1)) * 100;
  };

  const renderStepContent = () => {
    console.log('🔵 renderStepContent - currentStep:', currentStep);
    console.log('🔵 renderStepContent - loading:', loading);
    console.log('🔵 renderStepContent - handleSubmit type:', typeof handleSubmit);

    switch (currentStep) {
      case 1:
        return <BasicInformation
          formData={formData}
          updateFormData={updateFormData}
          procurementCategory={sourceRequisition?.procurementCategory}
          sourceCurrency={sourceRequisition?.currency}
        />;
      case 2:
        return <TenderLots formData={formData} updateFormData={updateFormData} tenderId={tenderId} />;
      case 3:
        return <TenderProposals formData={formData} updateFormData={updateFormData} tenderId={tenderId} />;
      case 4:
        return <TenderDocuments formData={formData} updateFormData={updateFormData} tenderId={tenderId} />;
      case 5:
        return <TenderFees formData={formData} updateFormData={updateFormData} tenderId={tenderId} />;
      case 6:
        return <TenderInvitations formData={formData} updateFormData={updateFormData} tenderId={tenderId} />;
      case 7:
        console.log('🔵 Rendering TenderReview with onSubmit:', handleSubmit, 'loading:', loading);
        return <TenderReview formData={formData} onSubmit={handleSubmit} loading={loading} tenderDocuments={tenderDocuments} />;
      default:
        return null;
    }
  };

  if (!fromRequisitionId && !tenderId) {
    return (
      <div className="mx-auto max-w-3xl space-y-6">
        <Card className="border-amber-200 bg-amber-50">
          <CardHeader>
            <CardTitle>Sourcing release required</CardTitle>
            <CardDescription>
              New tenders must start from an Approved purchase requisition with a current immutable sourcing release.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-3">
            <Link href="/procurement/purchase-requisitions">
              <Button>Open purchase requisitions</Button>
            </Link>
            <Link href="/procurement/tenders">
              <Button variant="outline">Back to tenders</Button>
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-6xl mx-auto">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.back()}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Create New Tender</h1>
            <p className="text-muted-foreground">
              Step {currentStep} of {STEPS.length}: {STEPS[currentStep - 1].name}
            </p>
            {sourceRequisition && (
              <p className="text-sm text-muted-foreground mt-1">
                From PR:{' '}
                <Link className="underline" href={`/procurement/purchase-requisitions/${sourceRequisition.id}`}>
                  {sourceRequisition.requisitionNumber}
                </Link>
              </p>
            )}
          </div>
        </div>
        <Button onClick={handleSaveDraft} variant="outline" disabled={savingDraft || !formData.title}>
          <Save className="h-4 w-4 mr-2" />
          {savingDraft ? 'Saving...' : 'Save Draft'}
        </Button>
      </div>

      {/* Progress Bar */}
      <div className="space-y-2">
        <Progress value={calculateProgress()} className="h-2" />
        <div className="flex justify-between">
          {STEPS.map((step) => {
            const Icon = step.icon;
            const isActive = step.id === currentStep;
            const isCompleted = step.id < currentStep;

            return (
              <div
                key={step.id}
                className={`flex flex-col items-center gap-2 ${
                  isActive ? 'text-blue-600' : isCompleted ? 'text-green-600' : 'text-gray-400'
                }`}
              >
                <div
                  className={`w-10 h-10 rounded-full flex items-center justify-center ${
                    isActive
                      ? 'bg-blue-100 border-2 border-blue-600'
                      : isCompleted
                      ? 'bg-green-100 border-2 border-green-600'
                      : 'bg-gray-100 border-2 border-gray-300'
                  }`}
                >
                  <Icon className="h-5 w-5" />
                </div>
                <span className="text-xs font-medium hidden md:block">{step.name}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Step Content */}
      <Card>
        <CardHeader>
          <CardTitle>{STEPS[currentStep - 1].name}</CardTitle>
          <CardDescription>
            {currentStep === 1 && 'Enter the basic information about the tender'}
            {currentStep === 2 && 'Add items to the tender'}
            {currentStep === 3 && 'Upload tender documents (optional)'}
            {currentStep === 4 && 'Configure tender fees (optional)'}
            {currentStep === 5 && 'Invite business partners (optional)'}
            {currentStep === 6 && 'Review and submit the tender'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {renderStepContent()}
        </CardContent>
      </Card>

      {/* Navigation Buttons */}
      <div className="flex justify-between">
        <Button
          variant="outline"
          onClick={handlePrevious}
          disabled={currentStep === 1}
        >
          <ArrowLeft className="h-4 w-4 mr-2" />
          Previous
        </Button>

        {currentStep < STEPS.length ? (
          <Button onClick={handleNext}>
            Next
            <ArrowRight className="h-4 w-4 ml-2" />
          </Button>
        ) : (
          <Button
            onClick={() => {
              console.log('🟢 Create Tender button clicked!');
              console.log('🟢 currentStep:', currentStep);
              console.log('🟢 STEPS.length:', STEPS.length);
              console.log('🟢 loading:', loading);
              handleSubmit();
            }}
            disabled={loading}
          >
            {loading ? 'Creating...' : 'Create Tender'}
          </Button>
        )}
      </div>

      {/* Confirmation Dialog */}
      <ConfirmationDialog
        open={showConfirmDialog}
        onOpenChange={setShowConfirmDialog}
        title="Create Tender"
        description={
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              Are you sure you want to create this tender? Please review the details below:
            </p>
            <div className="rounded-lg border bg-muted/50 p-4 space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium">Title:</span>
                <span className="text-sm text-muted-foreground">{formData.title}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium">Type:</span>
                <span className="text-sm text-muted-foreground">{getTenderTypeLabel(formData.tenderType)}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium">Estimated Value:</span>
                <span className="text-sm text-muted-foreground">
                  {formData.currency} {formData.estimatedValue?.toLocaleString() || 'N/A'}
                </span>
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              The tender will be created as a draft and you can publish it later.
            </p>
          </div>
        }
        confirmText="Create Tender"
        cancelText="Cancel"
        variant="default"
        onConfirm={confirmSubmit}
        isLoading={loading}
      />
    </div>
  );
}

export default function NewTenderPage() {
  return (
    <Suspense fallback={<div className="flex items-center justify-center h-screen">Loading...</div>}>
      <NewTenderPageContent />
    </Suspense>
  );
}
