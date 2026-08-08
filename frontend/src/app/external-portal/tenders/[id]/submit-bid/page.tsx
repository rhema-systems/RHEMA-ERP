'use client';

import { useCallback, useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import {
  ArrowLeft,
  ArrowRight,
  Save,
  Send,
  CheckCircle,
  AlertCircle,
  Package,
} from 'lucide-react';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { toast } from 'sonner';
import { tenderService, type TenderDetailDto } from '@/services/tenderService';
import * as tenderBidService from '@/services/tenderBidService';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  type CreateTenderBidDto,
  type CreateTenderBidItemDto,
  type TenderBidDocumentDto,
} from '@/services/tenderBidService';

// Import step components
import BidItemsStep from '@/components/external-portal/bid-submission/BidItemsStep';
import BidProposalsStep from '@/components/external-portal/bid-submission/BidProposalsStep';
import BidDocumentsStep from '@/components/external-portal/bid-submission/BidDocumentsStep';
import BidReviewStep from '@/components/external-portal/bid-submission/BidReviewStep';
import { QuantitySurveyTenderBoqSubmissionPanel } from '@/components/quantity-survey/QuantitySurveyTenderBoqSubmissionPanel';
import type { TenderBoqLine } from '@/services/quantity-survey-tender-boq.service';

const STEPS = [
  { id: 1, name: 'Select Lots', description: 'Choose lots to bid for' },
  { id: 2, name: 'Bid Lots', description: 'Enter pricing for each lot' },
  { id: 3, name: 'Proposals', description: 'Technical & commercial proposals' },
  { id: 4, name: 'Documents', description: 'Upload supporting documents' },
  { id: 5, name: 'Review', description: 'Review and submit' },
];

type BidFormData = CreateTenderBidDto & {
  associationType?: 'AllUsers' | 'Self' | 'SelectedUsers';
};

export default function SubmitBidPage() {
  const params = useParams();
  const router = useRouter();
  const tenderId = Array.isArray(params?.id)
    ? params.id[0]
    : (params?.id ?? '');

  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [currentStep, setCurrentStep] = useState(1);
  const [createdBidId, setCreatedBidId] = useState<string | null>(null);
  const [uploadedDocuments, setUploadedDocuments] = useState<
    TenderBidDocumentDto[]
  >([]);
  const [showSubmitConfirmDialog, setShowSubmitConfirmDialog] = useState(false);
  const [selectedLotIds, setSelectedLotIds] = useState<string[]>([]);

  // Bid form data
  const [bidData, setBidData] = useState<BidFormData>({
    tenderId: tenderId,
    deliveryDays: undefined,
    paymentTerms: '',
    warrantyTerms: '',
    technicalProposal: '',
    commercialProposal: '',
    items: [],
    associationType: undefined,
    acceptedDeclaration: false,
  });

  const applyTenderBoqLines = useCallback((lines: TenderBoqLine[]) => {
    const byTenderItem = new Map(
      lines.map((line) => [line.tenderItemId, line])
    );
    setBidData((current) => ({
      ...current,
      items: current.items.map((item) => {
        const line = byTenderItem.get(item.tenderItemId);
        return line
          ? {
              ...item,
              offeredQuantity: line.offeredQuantity,
              unitPrice: line.unitPrice,
            }
          : item;
      }),
    }));
  }, []);

  useEffect(() => {
    if (tenderId) {
      loadTender();
      loadInitiationData();
    }
  }, [tenderId]);

  useEffect(() => {
    if (createdBidId) {
      loadDocuments();
    }
  }, [createdBidId]);

  // Re-initialize items when selected lots change
  useEffect(() => {
    if (tender && selectedLotIds.length > 0) {
      // Get all items from selected lots
      const selectedLots =
        tender.lots?.filter((lot) => selectedLotIds.includes(lot.id)) || [];
      const itemsToInclude = selectedLots.flatMap((lot) => lot.items || []);

      // Only initialize items that don't already exist in bidData
      const newItems: CreateTenderBidItemDto[] = [];

      itemsToInclude.forEach((item) => {
        const existingItem = bidData.items.find(
          (bi) => bi.tenderItemId === item.id
        );
        if (existingItem) {
          // Keep existing item data
          newItems.push(existingItem);
        } else {
          // Create new item with default values
          newItems.push({
            tenderItemId: item.id,
            offeredQuantity: item.quantity,
            unitPrice: 0,
            deliveryDays: undefined,
            specifications: '',
            brand: '',
            model: '',
            technicalDetails: '',
          });
        }
      });

      // Only update if items have changed
      if (
        JSON.stringify(newItems.map((i) => i.tenderItemId).sort()) !==
        JSON.stringify(bidData.items.map((i) => i.tenderItemId).sort())
      ) {
        setBidData((prev) => ({ ...prev, items: newItems }));
      }
    }
  }, [selectedLotIds, tender]);

  const loadInitiationData = () => {
    try {
      const storedData = sessionStorage.getItem('bidInitiationData');
      if (storedData) {
        const initiationData = JSON.parse(storedData);
        setBidData((prev) => ({
          ...prev,
          associationType: initiationData.associationType,
          acceptedDeclaration: initiationData.acceptedDeclaration,
        }));

        // Clear the session storage after loading
        sessionStorage.removeItem('bidInitiationData');
      }
    } catch (error) {
      console.error('Error loading initiation data:', error);
    }
  };

  const loadTender = async () => {
    try {
      setLoading(true);
      const data = await tenderService.getTenderById(tenderId);
      setTender(data);

      // Check if there's an existing draft bid for this tender
      const draftBid = await tenderBidService.getMyDraftBidByTenderId(tenderId);
      if (draftBid) {
        const draftItems = draftBid.items ?? [];
        // Load existing draft bid
        setCreatedBidId(draftBid.id);
        setBidData({
          tenderId: draftBid.tenderId,
          deliveryDays: draftBid.deliveryDays,
          paymentTerms: draftBid.paymentTerms || '',
          warrantyTerms: draftBid.warrantyTerms || '',
          technicalProposal: draftBid.technicalProposal || '',
          commercialProposal: draftBid.commercialProposal || '',
          items: draftItems.map((item) => ({
            tenderItemId: item.tenderItemId,
            offeredQuantity: item.offeredQuantity,
            unitPrice: item.unitPrice,
            deliveryDays: item.deliveryDays,
            brand: item.brand,
            model: item.model,
            specifications: item.specifications,
            technicalDetails: item.technicalDetails,
          })),
        });

        // Extract selected lot IDs from the draft bid items
        // Map bid items back to their lot IDs by finding the tender items
        const selectedLotIdsFromDraft = new Set<string>();
        draftItems.forEach((bidItem) => {
          // Find the tender item to get its lotId
          const tenderItem = data.items?.find(
            (ti) => ti.id === bidItem.tenderItemId
          );
          if (tenderItem?.lotId) {
            selectedLotIdsFromDraft.add(tenderItem.lotId);
          }
        });
        setSelectedLotIds(Array.from(selectedLotIdsFromDraft));

        // Determine which step to start on based on progress
        let startStep = 1;

        // If lots are selected, move to step 2
        if (selectedLotIdsFromDraft.size > 0) {
          startStep = 2;
        }

        // If any item has pricing, move to step 3 (Proposals)
        const hasPricing = draftItems.some((item) => item.unitPrice > 0);
        if (hasPricing) {
          startStep = 3;
        }

        // If proposals are filled, move to step 4 (Documents)
        if (draftBid.technicalProposal || draftBid.commercialProposal) {
          startStep = 4;
        }

        // If documents are uploaded, move to step 5 (Review)
        if (draftBid.documents && draftBid.documents.length > 0) {
          startStep = 5;
        }

        setCurrentStep(startStep);

        // Documents will be loaded by the useEffect that watches createdBidId
      }
    } catch (error) {
      console.error('Error loading tender:', error);
      toast.error('Failed to load tender details');
      router.push('/external-portal/tenders');
    } finally {
      setLoading(false);
    }
  };

  const updateBidData = (updates: Partial<BidFormData>) => {
    setBidData((prev) => ({ ...prev, ...updates }));
  };

  const validateStep = (step: number): boolean => {
    switch (step) {
      case 1: // Select Lots
        if (selectedLotIds.length === 0) {
          toast.error('Please select at least one lot to bid for');
          return false;
        }
        return true;
      case 2: // Bid Items
        if (bidData.items.length === 0) {
          toast.error('Please add at least one bid item');
          return false;
        }
        const hasInvalidItems = bidData.items.some(
          (item) => item.unitPrice <= 0 || item.offeredQuantity <= 0
        );
        if (hasInvalidItems) {
          toast.error('All items must have valid unit price and quantity');
          return false;
        }
        return true;

      case 3: // Proposals
        // Check if technical proposal is provided (either as text or document)
        const hasTechnicalProposalDoc = uploadedDocuments.some(
          (doc) => doc.documentType === 'TechnicalProposal'
        );
        const hasTechnicalProposalText =
          bidData.technicalProposal &&
          bidData.technicalProposal.trim().length >= 100;

        if (!hasTechnicalProposalDoc && !hasTechnicalProposalText) {
          toast.error(
            'Technical proposal is required (either upload a document or type at least 100 characters)'
          );
          return false;
        }

        // Check if commercial proposal is provided (either as text or document)
        const hasCommercialProposalDoc = uploadedDocuments.some(
          (doc) => doc.documentType === 'CommercialProposal'
        );
        const hasCommercialProposalText =
          bidData.commercialProposal &&
          bidData.commercialProposal.trim().length >= 100;

        if (!hasCommercialProposalDoc && !hasCommercialProposalText) {
          toast.error(
            'Commercial proposal is required (either upload a document or type at least 100 characters)'
          );
          return false;
        }

        return true;

      case 4: // Documents
        // Validate required documents are uploaded
        if (tender?.requiredDocuments) {
          try {
            const requirements = JSON.parse(tender.requiredDocuments);
            const requiredDocs = requirements.filter(
              (req: any) => req.isRequired
            );

            for (const req of requiredDocs) {
              const uploaded = uploadedDocuments.find(
                (doc) => doc.documentType === req.documentType
              );
              if (!uploaded) {
                toast.error(`Required document missing: ${req.documentName}`);
                return false;
              }
            }
          } catch (error) {
            console.error('Error validating documents:', error);
          }
        }
        return true;

      case 4: // Review
        return true;

      default:
        return true;
    }
  };

  const handleNext = async () => {
    if (!validateStep(currentStep)) {
      return;
    }

    // Auto-save when moving from Step 1 (lot selection) to Step 2
    if (currentStep === 1 && !createdBidId) {
      try {
        setSubmitting(true);

        // Create initial bid items from selected lots
        const selectedLots =
          tender?.lots?.filter((lot) => selectedLotIds.includes(lot.id)) || [];
        const itemsToInclude = selectedLots.flatMap((lot) => lot.items || []);
        const initialItems: CreateTenderBidItemDto[] = itemsToInclude.map(
          (item) => ({
            tenderItemId: item.id,
            offeredQuantity: item.quantity,
            unitPrice: 0,
            deliveryDays: undefined,
            specifications: '',
            brand: '',
            model: '',
            technicalDetails: '',
          })
        );

        const draftData = {
          ...bidData,
          items: initialItems,
        };

        // Create the bid as draft
        const createdBid = await tenderBidService.createBid(draftData);
        setCreatedBidId(createdBid.id);
        setBidData((prev) => ({ ...prev, items: initialItems }));
        toast.success(
          'Lot selection saved. You can now enter pricing details.'
        );
      } catch (error: any) {
        console.error('Error saving lot selection:', error);
        toast.error(
          error.message || 'Failed to save lot selection. Please try again.'
        );
        return; // Don't proceed if save failed
      } finally {
        setSubmitting(false);
      }
    }

    // Auto-save when moving from step 2 (Bid Items) or step 3 (Proposals) if bid items have changed
    if ((currentStep === 2 || currentStep === 3) && createdBidId) {
      try {
        setSubmitting(true);

        // Validate that items have offeredQuantity
        if (bidData.items.length === 0) {
          toast.error('Please add at least one bid item before proceeding');
          return;
        }

        // Update the existing draft
        const updateDto = {
          deliveryDays: bidData.deliveryDays,
          paymentTerms: bidData.paymentTerms,
          warrantyTerms: bidData.warrantyTerms,
          technicalProposal: bidData.technicalProposal,
          commercialProposal: bidData.commercialProposal,
          items: bidData.items.map((item) => ({
            tenderItemId: item.tenderItemId,
            offeredQuantity: item.offeredQuantity,
            unitPrice: item.unitPrice,
            deliveryDays: item.deliveryDays,
            specifications: item.specifications,
            brand: item.brand,
            model: item.model,
            technicalDetails: item.technicalDetails,
          })),
        };

        await tenderBidService.updateBid(createdBidId, updateDto);
        toast.success('Progress saved.');
      } catch (error: any) {
        console.error('Error auto-saving bid:', error);
        toast.error(
          error.message || 'Failed to save progress. Please try again.'
        );
        return; // Don't proceed if save failed
      } finally {
        setSubmitting(false);
      }
    }

    setCurrentStep((prev) => Math.min(STEPS.length, prev + 1));
  };

  const handlePrevious = () => {
    setCurrentStep((prev) => Math.max(1, prev - 1));
  };

  const handleSaveDraft = async () => {
    try {
      setSubmitting(true);

      // Step 1: Save lot selection
      if (currentStep === 1) {
        if (selectedLotIds.length === 0) {
          toast.error('Please select at least one lot before saving');
          return;
        }

        if (!createdBidId) {
          // Create initial bid items from selected lots
          const selectedLots =
            tender?.lots?.filter((lot) => selectedLotIds.includes(lot.id)) ||
            [];
          const itemsToInclude = selectedLots.flatMap((lot) => lot.items || []);
          const initialItems: CreateTenderBidItemDto[] = itemsToInclude.map(
            (item) => ({
              tenderItemId: item.id,
              offeredQuantity: item.quantity,
              unitPrice: 0,
              deliveryDays: undefined,
              specifications: '',
              brand: '',
              model: '',
              technicalDetails: '',
            })
          );

          const draftData = {
            ...bidData,
            items: initialItems,
          };

          const createdBid = await tenderBidService.createBid(draftData);
          setCreatedBidId(createdBid.id);
          setBidData((prev) => ({ ...prev, items: initialItems }));
          toast.success('Lot selection saved as draft');
        } else {
          toast.success('Lot selection already saved');
        }
        return;
      }

      // Steps 2-5: Save bid data
      if (bidData.items.length === 0) {
        toast.error('Please add at least one bid item before saving');
        return;
      }

      if (createdBidId) {
        // Update existing draft
        const updateDto = {
          deliveryDays: bidData.deliveryDays,
          paymentTerms: bidData.paymentTerms,
          warrantyTerms: bidData.warrantyTerms,
          technicalProposal: bidData.technicalProposal,
          commercialProposal: bidData.commercialProposal,
          items: bidData.items.map((item) => ({
            tenderItemId: item.tenderItemId,
            offeredQuantity: item.offeredQuantity,
            unitPrice: item.unitPrice,
            deliveryDays: item.deliveryDays,
            specifications: item.specifications,
            brand: item.brand,
            model: item.model,
            technicalDetails: item.technicalDetails,
          })),
        };
        await tenderBidService.updateBid(createdBidId, updateDto);
        toast.success('Bid draft updated successfully');
      } else {
        // Create new draft (shouldn't happen if lot selection was done properly)
        const createdBid = await tenderBidService.createBid(bidData);
        setCreatedBidId(createdBid.id);
        toast.success('Bid saved as draft successfully');
      }
    } catch (error: any) {
      console.error('Error saving draft:', error);
      toast.error(error.message || 'Failed to save draft');
    } finally {
      setSubmitting(false);
    }
  };

  const handleDocumentUpload = async (file: File, documentType: string) => {
    if (!createdBidId) {
      toast.error('Please save the bid as draft first');
      return;
    }

    try {
      const uploadedDoc = await tenderBidService.uploadBidDocument(
        createdBidId,
        file,
        documentType,
        file.name
      );
      setUploadedDocuments((prev) => [...prev, uploadedDoc]);
    } catch (error) {
      console.error('Error uploading document:', error);
      throw error;
    }
  };

  const handleDocumentDelete = async (documentId: string) => {
    if (!createdBidId) return;

    try {
      await tenderBidService.deleteBidDocument(createdBidId, documentId);
      setUploadedDocuments((prev) =>
        prev.filter((doc) => doc.id !== documentId)
      );
    } catch (error) {
      console.error('Error deleting document:', error);
      throw error;
    }
  };

  const loadDocuments = async () => {
    if (!createdBidId) return;

    try {
      const docs = await tenderBidService.getBidDocuments(createdBidId);
      setUploadedDocuments(docs);
    } catch (error) {
      console.error('Error loading documents:', error);
    }
  };

  const handleSubmitClick = () => {
    // Validate all steps including documents
    if (!validateStep(1) || !validateStep(2) || !validateStep(3)) {
      toast.error(
        'Please complete all required steps and upload required documents'
      );
      return;
    }

    // Show confirmation dialog
    setShowSubmitConfirmDialog(true);
  };

  const handleConfirmSubmit = async () => {
    try {
      setSubmitting(true);

      let bidId = createdBidId;

      if (!bidId) {
        // Create the bid if not already created
        const createdBid = await tenderBidService.createBid(bidData);
        bidId = createdBid.id;
      } else {
        // Update existing draft
        await tenderBidService.updateBid(bidId, bidData);
      }

      // Submit the bid
      await tenderBidService.submitBid(bidId, { confirmSubmission: true });

      toast.success('Bid submitted successfully!');
      setShowSubmitConfirmDialog(false);
      router.push(`/external-portal/my-bids/${bidId}`);
    } catch (error) {
      console.error('Error submitting bid:', error);
      toast.error('Failed to submit bid');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="h-12 w-12 animate-spin mx-auto mb-4 border-4 border-blue-500 border-t-transparent rounded-full" />
          <p className="text-lg text-gray-600">Loading tender details...</p>
        </div>
      </div>
    );
  }

  if (!tender) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <AlertCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Tender not found</p>
          <Button
            onClick={() => router.push('/external-portal/tenders')}
            className="mt-4"
          >
            Back to Tenders
          </Button>
        </div>
      </div>
    );
  }

  const progress = (currentStep / STEPS.length) * 100;

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            onClick={() => router.push(`/external-portal/tenders/${tenderId}`)}
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Tender
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Submit Bid</h1>
            <p className="text-gray-500">
              {tender.tenderNumber} - {tender.title}
            </p>
          </div>
        </div>
      </div>

      {/* Progress */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between mb-2">
            <CardTitle>
              Step {currentStep} of {STEPS.length}
            </CardTitle>
            <Badge>{STEPS[currentStep - 1].name}</Badge>
          </div>
          <Progress value={progress} className="h-2" />
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-5 gap-4">
            {STEPS.map((step) => (
              <div
                key={step.id}
                className={`text-center p-3 rounded-lg border ${
                  step.id === currentStep
                    ? 'border-blue-500 bg-blue-50'
                    : step.id < currentStep
                      ? 'border-green-500 bg-green-50'
                      : 'border-gray-200'
                }`}
              >
                <div className="flex items-center justify-center mb-2">
                  {step.id < currentStep ? (
                    <CheckCircle className="h-6 w-6 text-green-600" />
                  ) : (
                    <div
                      className={`h-6 w-6 rounded-full flex items-center justify-center ${
                        step.id === currentStep
                          ? 'bg-blue-600 text-white'
                          : 'bg-gray-200 text-gray-600'
                      }`}
                    >
                      {step.id}
                    </div>
                  )}
                </div>
                <p className="font-medium text-sm">{step.name}</p>
                <p className="text-xs text-gray-500">{step.description}</p>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Step Content */}
      <div className="min-h-[400px]">
        {/* Step 1: Select Lots */}
        {currentStep === 1 && (
          <Card>
            <CardHeader>
              <CardTitle>Select Lots to Bid For</CardTitle>
              <CardDescription>
                Choose which lots you want to include in your bid. Each lot
                contains one or more items.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              {tender.lots && tender.lots.length > 0 ? (
                <>
                  <Alert>
                    <Package className="h-4 w-4" />
                    <AlertDescription>
                      Select the lots you want to bid for. You must provide
                      pricing for all items within each selected lot.
                    </AlertDescription>
                  </Alert>

                  <div className="space-y-4">
                    {tender.lots.map((lot) => (
                      <div
                        key={lot.id}
                        className={`border rounded-lg overflow-hidden cursor-pointer transition-colors ${
                          selectedLotIds.includes(lot.id)
                            ? 'border-primary bg-primary/5'
                            : 'hover:bg-accent'
                        }`}
                        onClick={() => {
                          setSelectedLotIds((prev) => {
                            if (prev.includes(lot.id)) {
                              return prev.filter((id) => id !== lot.id);
                            } else {
                              return [...prev, lot.id];
                            }
                          });
                        }}
                      >
                        {/* Lot Header */}
                        <div className="flex items-start space-x-3 p-4 bg-gray-50">
                          <Checkbox
                            id={lot.id}
                            checked={selectedLotIds.includes(lot.id)}
                            onCheckedChange={() => {
                              setSelectedLotIds((prev) => {
                                if (prev.includes(lot.id)) {
                                  return prev.filter((id) => id !== lot.id);
                                } else {
                                  return [...prev, lot.id];
                                }
                              });
                            }}
                            onClick={(e) => e.stopPropagation()}
                          />
                          <div className="flex-1">
                            <Label htmlFor={lot.id} className="cursor-pointer">
                              <div className="font-medium text-base mb-1">
                                <span className="text-blue-600 mr-2">
                                  [{lot.lotCode}]
                                </span>
                                {lot.title}
                              </div>
                              {lot.description && (
                                <div className="text-sm text-muted-foreground">
                                  {lot.description}
                                </div>
                              )}
                            </Label>
                          </div>
                          <div className="text-right">
                            {lot.estimatedValue && (
                              <div className="font-medium text-sm">
                                {lot.currency || 'USD'}{' '}
                                {lot.estimatedValue.toLocaleString()}
                              </div>
                            )}
                            <Badge
                              variant={
                                selectedLotIds.includes(lot.id)
                                  ? 'default'
                                  : 'outline'
                              }
                            >
                              {selectedLotIds.includes(lot.id)
                                ? 'Selected'
                                : 'Not Selected'}
                            </Badge>
                          </div>
                        </div>
                        {/* Lot Items */}
                        {lot.items && lot.items.length > 0 && (
                          <div className="px-4 py-3 border-t bg-white">
                            <div className="text-xs font-medium text-muted-foreground mb-2">
                              {lot.items.length} item(s) in this lot:
                            </div>
                            <div className="space-y-1">
                              {lot.items.map((item, idx) => (
                                <div
                                  key={item.id}
                                  className="text-sm flex justify-between items-center py-1 px-2 bg-muted/50 rounded"
                                >
                                  <span>
                                    <span className="text-muted-foreground mr-1">
                                      {idx + 1}.
                                    </span>
                                    {item.description}
                                  </span>
                                  <span className="text-muted-foreground">
                                    {item.quantity}{' '}
                                    {item.unitOfMeasure || 'units'}
                                  </span>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>

                  {selectedLotIds.length > 0 && (
                    <Alert>
                      <CheckCircle className="h-4 w-4" />
                      <AlertDescription>
                        {selectedLotIds.length} lot
                        {selectedLotIds.length > 1 ? 's' : ''} selected with{' '}
                        {tender.lots
                          .filter((lot) => selectedLotIds.includes(lot.id))
                          .reduce(
                            (sum, lot) => sum + (lot.items?.length || 0),
                            0
                          )}{' '}
                        total item(s)
                      </AlertDescription>
                    </Alert>
                  )}
                </>
              ) : (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    No lots available for this tender.
                  </AlertDescription>
                </Alert>
              )}
            </CardContent>
          </Card>
        )}

        {/* Step 2: Bid Items */}
        {currentStep === 2 && (
          <>
            {selectedLotIds.length > 0 &&
              selectedLotIds.length < (tender.lots?.length || 0) && (
                <Alert className="mb-4">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    You are bidding for {selectedLotIds.length} out of{' '}
                    {tender.lots?.length || 0} lots. Only items from selected
                    lots are shown below.
                  </AlertDescription>
                </Alert>
              )}
            {createdBidId && (
              <QuantitySurveyTenderBoqSubmissionPanel
                tenderBidId={createdBidId}
                onCommitted={applyTenderBoqLines}
              />
            )}
            <BidItemsStep
              tender={tender}
              bidData={bidData}
              updateBidData={updateBidData}
              selectedLotIds={selectedLotIds}
            />
          </>
        )}

        {/* Step 3: Proposals */}
        {currentStep === 3 && (
          <BidProposalsStep
            bidData={bidData}
            updateBidData={updateBidData}
            bidId={createdBidId || undefined}
            uploadedDocuments={uploadedDocuments}
            tenderDocuments={tender?.documents || []}
            onDocumentUpload={handleDocumentUpload}
            onDocumentDelete={handleDocumentDelete}
          />
        )}

        {/* Step 4: Documents */}
        {currentStep === 4 && (
          <BidDocumentsStep
            bidData={bidData}
            updateBidData={updateBidData}
            bidId={createdBidId || undefined}
            uploadedDocuments={uploadedDocuments}
            onDocumentUpload={handleDocumentUpload}
            onDocumentDelete={handleDocumentDelete}
            tender={tender}
          />
        )}

        {/* Step 5: Review */}
        {currentStep === 5 && (
          <BidReviewStep
            tender={tender}
            bidData={bidData}
            uploadedDocuments={uploadedDocuments}
            selectedLotIds={selectedLotIds}
          />
        )}
      </div>

      {/* Navigation */}
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div>
              {currentStep > 1 && (
                <Button
                  variant="outline"
                  onClick={handlePrevious}
                  disabled={submitting}
                >
                  <ArrowLeft className="h-4 w-4 mr-2" />
                  Previous
                </Button>
              )}
            </div>
            <div className="flex gap-2">
              <Button
                variant="outline"
                onClick={handleSaveDraft}
                disabled={submitting}
              >
                <Save className="h-4 w-4 mr-2" />
                Save Draft
              </Button>
              {currentStep < STEPS.length ? (
                <Button onClick={handleNext} disabled={submitting}>
                  Next
                  <ArrowRight className="h-4 w-4 ml-2" />
                </Button>
              ) : (
                <Button onClick={handleSubmitClick} disabled={submitting}>
                  <Send className="h-4 w-4 mr-2" />
                  {submitting ? 'Submitting...' : 'Submit Bid'}
                </Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Submit Confirmation Dialog */}
      <ConfirmationDialog
        open={showSubmitConfirmDialog}
        onOpenChange={setShowSubmitConfirmDialog}
        title="Submit Bid"
        description="Are you sure you want to submit this bid? Once submitted, you will not be able to modify your bid. Please ensure all information is accurate and complete."
        confirmText="Yes, Submit Bid"
        cancelText="Cancel"
        variant="default"
        onConfirm={handleConfirmSubmit}
        isLoading={submitting}
      />
    </div>
  );
}
