'use client';

import { useState, useEffect } from 'react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { ArrowLeft, ArrowRight, Save, FileText, Package, Upload, DollarSign, Users, CheckCircle2, ClipboardList } from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type TenderDetailDto } from '@/services/tenderService';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { buildUpdateTenderDto } from '@/lib/tender-form-payload';

// Import step components
import BasicInformation from '@/components/procurement/tenders/BasicInformation';
import TenderLots from '@/components/procurement/tenders/TenderLots';
import TenderDocuments from '@/components/procurement/tenders/TenderDocuments';
import TenderFees from '@/components/procurement/tenders/TenderFees';
import TenderInvitations from '@/components/procurement/tenders/TenderInvitations';
import TenderReview from '@/components/procurement/tenders/TenderReview';
import TenderProposals from '@/components/procurement/tenders/TenderProposals';
import { TenderFormData, DocumentRequirement } from '../../new/page';

const STEPS = [
  { id: 1, name: 'Basic Information', icon: FileText, component: 'BasicInformation' },
  { id: 2, name: 'Tender Lots', icon: Package, component: 'TenderLots' },
  { id: 3, name: 'Proposal', icon: ClipboardList, component: 'TenderProposals' },
  { id: 4, name: 'Documents', icon: Upload, component: 'TenderDocuments' },
  { id: 5, name: 'Fees', icon: DollarSign, component: 'TenderFees' },
  { id: 6, name: 'Invitations', icon: Users, component: 'TenderInvitations' },
  { id: 7, name: 'Review & Submit', icon: CheckCircle2, component: 'TenderReview' },
];

export default function EditTenderPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const tenderId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const fromRequisitionId = searchParams?.get('fromRequisitionId') ?? null;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [currentStep, setCurrentStep] = useState(1);
  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);

  const [formData, setFormData] = useState<TenderFormData>({
    title: '',
    description: '',
    tenderType: 'RFQ',
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
    // QCBS Evaluation defaults
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

  useEffect(() => {
    if (tenderId) {
      loadTender();
    }
  }, [tenderId]);

  // Refresh tender documents when entering review step
  useEffect(() => {
    const refreshTenderForReview = async () => {
      if (tenderId && currentStep === 7) {
        try {
          const data = await tenderService.getTenderById(tenderId);
          setTender(data);
        } catch (error) {
          console.error('Error refreshing tender data:', error);
        }
      }
    };
    refreshTenderForReview();
  }, [tenderId, currentStep]);

  const loadTender = async () => {
    try {
      setLoading(true);
      const data = await tenderService.getTenderById(tenderId);
      setTender(data);

      // Parse document requirements
      let documentRequirements: DocumentRequirement[] = [];
      if (data.requiredDocuments) {
        try {
          documentRequirements = JSON.parse(data.requiredDocuments);
        } catch (e) {
          console.error('Error parsing document requirements:', e);
        }
      }

      // Populate form data from loaded tender
      setFormData({
        title: data.title,
        description: data.description || '',
        tenderType: data.tenderType,
        submissionDeadline: data.submissionDeadline ? new Date(data.submissionDeadline).toISOString().slice(0, 16) : '',
        openingDate: data.openingDate ? new Date(data.openingDate).toISOString().slice(0, 16) : '',
        estimatedValue: data.estimatedValue || null,
        currency: data.currency || 'USD',
        minimumPerformanceRating: data.minimumPerformanceRating || null,
        requiresPrequalification: data.requiresPrequalification,
        allowPartialBids: data.allowPartialBids,
        priceWeightage: data.priceWeightage,
        qualityWeightage: data.qualityWeightage,
        deliveryWeightage: data.deliveryWeightage,
        experienceWeightage: data.experienceWeightage,
        evaluationCriteriaJson: data.evaluationCriteriaJson || '',
        notes: data.notes || '',
        termsAndConditions: data.termsAndConditions || '',
        // QCBS Evaluation fields
        useQCBSEvaluation: data.useQCBSEvaluation || false,
        technicalWeight: data.technicalWeight ?? 80,
        financialWeight: data.financialWeight ?? 20,
        minimumTechnicalScore: data.minimumTechnicalScore ?? 70,
        evaluationTemplateId: data.evaluationTemplateId || null,
        evaluationTemplateName: data.evaluationTemplateName || '',
        lots: (data.lots || []).map((lot, lotIndex) => ({
          id: lot.id,
          lotNumber: lot.lotNumber || lotIndex + 1,
          lotCode: lot.lotCode || `LOT-${String(lotIndex + 1).padStart(3, '0')}`,
          title: lot.title,
          description: lot.description || '',
          estimatedValue: lot.estimatedValue,
          requiredDeliveryDate: lot.requiredDeliveryDate || '',
          deliveryLocation: lot.deliveryLocation || '',
          displayOrder: lot.displayOrder || lotIndex,
          items: (lot.items || []).map((item, itemIndex) => ({
            id: item.id,
            lineNumber: item.lineNumber || itemIndex + 1,
            itemCode: item.itemCode || '',
            description: item.description,
            quantity: item.quantity,
            unitOfMeasure: item.unitOfMeasure || '',
            specifications: item.specifications || '',
            requiredDeliveryDate: item.requiredDeliveryDate || '',
            deliveryLocation: item.deliveryLocation || '',
          })),
        })),
        items: data.items.map((item, itemIndex) => ({
          id: item.id,
          lineNumber: item.lineNumber || itemIndex + 1,
          itemCode: item.itemCode || '',
          description: item.description,
          quantity: item.quantity,
          unitOfMeasure: item.unitOfMeasure || '',
          specifications: item.specifications || '',
          requiredDeliveryDate: item.requiredDeliveryDate || '',
          deliveryLocation: item.deliveryLocation || '',
        })),
        documentRequirements,
        requiresAcceptanceDeclaration: data.requiresAcceptanceDeclaration || false,
        acceptanceDeclarationFile: null, // File is already uploaded, not in form state
        acceptanceDeclarationDocumentName: data.acceptanceDeclarationDocumentName || '',
        proposalTemplateFile: null,
        proposalTemplateName: data.documents.find((doc) => doc.documentType === 'ProposalTemplate')?.documentName || '',
        documents: [],
        fees: (data.fees || []).map(fee => ({
          id: fee.id,
          feeType: fee.feeType,
          amount: fee.amount,
          currency: fee.currency || 'USD',
          paymentMethod: fee.paymentMethod || '',
          isMandatory: fee.isMandatory,
          dueDate: fee.dueDate ? new Date(fee.dueDate).toISOString().slice(0, 16) : '',
          description: fee.description || '',
          bankAccountDetails: fee.bankAccountDetails || '',
        })),
        invitations: (data.invitations || []).map(inv => ({
          id: inv.id,
          businessPartnerId: inv.businessPartnerId,
          businessPartnerName: inv.businessPartnerName,
          invitedDate: inv.invitedDate,
          status: inv.status,
          viewedDate: inv.viewedDate,
          responseDate: inv.responseDate,
          declineReason: inv.declineReason,
        })),
        invitedBusinessPartnerIds: (data.invitations || []).map((inv) => inv.businessPartnerId),
        sendNotifications: false,
      });

      // Determine the last step with data and navigate to it
      let lastStep = 1; // Default to step 1 (Basic Information)

      if (data.invitations && data.invitations.length > 0) {
        lastStep = 6; // Invitations step
      } else if (data.fees && data.fees.length > 0) {
        lastStep = 5; // Fees step
      } else if (documentRequirements.length > 0) {
        lastStep = 3; // Documents step (check document requirements, not uploaded documents)
      } else if (data.items && data.items.length > 0) {
        lastStep = 2; // Items step
      }

      setCurrentStep(lastStep);
      console.log('Loaded tender, navigating to step:', lastStep);
    } catch (error) {
      console.error('Error loading tender:', error);
      toast.error('Failed to load tender details');
      router.push('/procurement/tenders');
    } finally {
      setLoading(false);
    }
  };

  const updateFormData = (data: Partial<TenderFormData>) => {
    setFormData(prev => ({ ...prev, ...data }));
  };



  const handleSaveDraft = async () => {
    try {
      setSaving(true);

      console.log('TenderEdit - handleSaveDraft called');
      console.log('TenderEdit - formData.documentRequirements:', formData.documentRequirements);

      const updateDto = buildUpdateTenderDto(formData);

      console.log('TenderEdit - updateDto:', updateDto);

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
          setFormData(prev => ({
            ...prev,
            acceptanceDeclarationFile: null,
          }));
        } catch (error) {
          console.error('Error uploading acceptance declaration:', error);
          // Don't fail the whole operation if just the file upload fails
        }
      }

      toast.success('Draft saved successfully');
    } catch (error: any) {
      console.error('Error updating tender:', error);
      const errorMessage = error?.message || 'Failed to update tender';
      toast.error(`Error saving draft: ${errorMessage}`);
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = () => {
    console.log('🔵 handleSubmit called (Edit Page)');
    console.log('🔵 formData:', formData);
    console.log('🔵 tenderId:', tenderId);

    // Show confirmation dialog
    setShowConfirmDialog(true);
  };

  const confirmSubmit = async () => {
    console.log('🔵 User confirmed tender save');

    try {
      setSaving(true);
      console.log('🔵 Starting tender update...');

      const updateDto = buildUpdateTenderDto(formData);

      console.log('🔵 updateDto:', updateDto);

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
        } catch (error) {
          console.error('Error uploading acceptance declaration:', error);
          // Don't fail the whole operation if just the file upload fails
        }
      }

      toast.success('✅ Tender saved successfully!');
      console.log('🔵 Redirecting to tender details page...');
      router.push(`/procurement/tenders/${tenderId}`);
    } catch (error) {
      console.error('❌ Error saving tender:', error);
      toast.error('Failed to save tender');
    } finally {
      setSaving(false);
      console.log('🔵 handleSubmit completed');
    }
  };

  const handleNext = () => {
    setCurrentStep(prev => Math.min(STEPS.length, prev + 1));
  };

  const handlePrevious = () => {
    setCurrentStep(prev => Math.max(1, prev - 1));
  };

  const renderStepContent = () => {
    switch (currentStep) {
      case 1:
        return <BasicInformation formData={formData} updateFormData={updateFormData} />;
      case 2:
        return <TenderLots formData={formData} updateFormData={updateFormData} tenderId={tenderId} />;
      case 3:
        return <TenderProposals formData={formData} updateFormData={updateFormData} tenderId={tenderId} isEditMode={true} />;
      case 4:
        return <TenderDocuments formData={formData} updateFormData={updateFormData} tenderId={tenderId} isEditMode={true} />;
      case 5:
        return <TenderFees formData={formData} updateFormData={updateFormData} tenderId={tenderId} />;
      case 6:
        return <TenderInvitations formData={formData} updateFormData={updateFormData} tenderId={tenderId} fromRequisitionId={fromRequisitionId} />;
      case 7:
        return <TenderReview formData={formData} onSubmit={handleSubmit} loading={saving} tenderDocuments={tender?.documents || []} />;
      default:
        return null;
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading tender...</p>
        </div>
      </div>
    );
  }

  if (!tender) {
    return (
      <div className="text-center py-12">
        <p className="text-gray-600">Tender not found</p>
        <Button onClick={() => router.push('/procurement/tenders')} className="mt-4">
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to Tenders
        </Button>
      </div>
    );
  }

  const calculateProgress = () => {
    return (currentStep / STEPS.length) * 100;
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push('/procurement/tenders')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Edit Tender</h1>
            <p className="text-muted-foreground">
              Step {currentStep} of {STEPS.length}: {STEPS[currentStep - 1].name}
            </p>
          </div>
        </div>
        <Button onClick={handleSaveDraft} variant="outline" disabled={saving}>
          <Save className="h-4 w-4 mr-2" />
          {saving ? 'Saving...' : 'Save Draft'}
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

      {/* Navigation */}
      <div className="flex justify-between">
        <Button
          variant="outline"
          onClick={handlePrevious}
          disabled={currentStep === 1}
        >
          <ArrowLeft className="h-4 w-4 mr-2" />
          Previous
        </Button>
        <Button
          onClick={handleNext}
          disabled={currentStep === STEPS.length}
        >
          Next
          <ArrowRight className="h-4 w-4 ml-2" />
        </Button>
      </div>

      {/* Confirmation Dialog */}
      <ConfirmationDialog
        open={showConfirmDialog}
        onOpenChange={setShowConfirmDialog}
        title="Save Tender"
        description={
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              Are you sure you want to save this tender? Please review the details below:
            </p>
            <div className="rounded-lg border bg-muted/50 p-4 space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium">Title:</span>
                <span className="text-sm text-muted-foreground">{formData.title}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium">Type:</span>
                <span className="text-sm text-muted-foreground">{formData.tenderType}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm font-medium">Estimated Value:</span>
                <span className="text-sm text-muted-foreground">
                  {formData.currency} {formData.estimatedValue?.toLocaleString() || 'N/A'}
                </span>
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              The tender will be saved and you can publish it later.
            </p>
          </div>
        }
        confirmText="Save Tender"
        cancelText="Cancel"
        variant="default"
        onConfirm={confirmSubmit}
        isLoading={saving}
      />
    </div>
  );
}
