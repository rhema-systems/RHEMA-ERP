'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { toast } from 'sonner';
import {
  ArrowLeft,
  ArrowRight,
  Users,
  User,
  UserCheck,
  FileText,
  DollarSign,
  CheckCircle,
  XCircle,
  Clock,
  Download,
  AlertCircle,
} from 'lucide-react';
import * as tenderService from '@/services/tenderService';
import { TenderDetailDto, TenderFeeDto } from '@/services/tenderService';
import * as businessPartnerUserService from '@/services/businessPartnerUserService';
import { type BusinessPartnerUserDto } from '@/services/businessPartnerUserService';
import * as tenderAssignmentService from '@/services/tenderAssignmentService';
import * as tenderBidService from '@/services/tenderBidService';
import {
  isPositiveMandatoryFee,
  nextTenderBidInitiationStep,
  type TenderBidInitiationStatus,
} from '@/lib/tender-bid-initiation';

export default function InitiateBidPage() {
  const router = useRouter();
  const params = useParams();
  const tenderId = Array.isArray(params?.id)
    ? params.id[0]
    : (params?.id ?? '');

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [currentStep, setCurrentStep] = useState(1);
  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [initiationStatus, setInitiationStatus] =
    useState<TenderBidInitiationStatus | null>(null);
  const [paymentReferences, setPaymentReferences] = useState<
    Record<string, string>
  >({});
  const [businessPartnerId, setBusinessPartnerId] = useState<string>('');
  const [availableUsers, setAvailableUsers] = useState<
    BusinessPartnerUserDto[]
  >([]);
  const [loadingUsers, setLoadingUsers] = useState(false);

  // Form data
  const [associationType, setAssociationType] = useState<
    'AllUsers' | 'Self' | 'SelectedUsers'
  >('Self');
  const [selectedUsers, setSelectedUsers] = useState<string[]>([]);
  const [acceptedDeclaration, setAcceptedDeclaration] = useState(false);

  // Initiation status
  const [hasExistingAssignment, setHasExistingAssignment] = useState(false);

  const STEPS = [
    { number: 1, title: 'Type of Association', icon: Users },
    { number: 2, title: 'Accept Agreement', icon: FileText },
    { number: 3, title: 'Payment Verification', icon: DollarSign },
  ];

  useEffect(() => {
    if (tenderId) {
      loadTender();
      checkInitiationStatus();
    }
  }, [tenderId]);

  useEffect(() => {
    if (tenderId && businessPartnerId) {
      loadExistingAssignment();
    }
  }, [tenderId, businessPartnerId]);

  useEffect(() => {
    if (businessPartnerId && associationType === 'SelectedUsers') {
      loadUsers();
    }
  }, [businessPartnerId, associationType]);

  const loadTender = async () => {
    try {
      setLoading(true);
      const data = await tenderService.getTenderById(tenderId);
      setTender(data);
    } catch (error) {
      console.error('Error loading tender:', error);
      toast.error('Failed to load tender details');
    } finally {
      setLoading(false);
    }
  };

  const loadUsers = async () => {
    if (!businessPartnerId) return;

    try {
      setLoadingUsers(true);
      const users =
        await businessPartnerUserService.getUsersByBusinessPartnerId(
          businessPartnerId
        );
      // Filter out inactive users and the main account owner
      const activeUsers = users.filter(
        (u) => u.isActive && u.notes !== 'Main account owner'
      );
      setAvailableUsers(activeUsers);
    } catch (error) {
      console.error('Error loading users:', error);
      toast.error('Failed to load users');
    } finally {
      setLoadingUsers(false);
    }
  };

  const loadExistingAssignment = async () => {
    if (!tenderId || !businessPartnerId) return;

    try {
      const assignments =
        await tenderAssignmentService.getAssignmentsByTenderId(tenderId);
      const myAssignments = assignments.filter(
        (a) => a.businessPartnerId === businessPartnerId
      );

      if (myAssignments.length > 0) {
        const firstAssignment = myAssignments[0];
        const assignType = firstAssignment.assignmentType as
          | 'AllUsers'
          | 'Self'
          | 'SelectedUsers';
        setAssociationType(assignType);

        // If it's SelectedUsers, load the assigned user IDs
        if (assignType === 'SelectedUsers') {
          const assignedUserIds = myAssignments
            .map((assignment) => assignment.assignedToUserId)
            .filter((assignedUserId): assignedUserId is string =>
              Boolean(assignedUserId)
            );
          setSelectedUsers(assignedUserIds);
        }
      }
    } catch (error) {
      console.error('Error loading existing assignment:', error);
    }
  };

  const checkInitiationStatus = async () => {
    if (!tenderId) return;

    try {
      const data = await tenderBidService.getInitiationStatus(tenderId);
      setInitiationStatus(data);
      setBusinessPartnerId(data.businessPartnerId);
      setHasExistingAssignment(data.hasAssignment);
      setAcceptedDeclaration(data.declarationAccepted);
      const nextStep = nextTenderBidInitiationStep(data);
      if (nextStep === 'complete') {
        router.push(`/external-portal/tenders/${tenderId}/submit-bid`);
        return;
      }
      setCurrentStep(nextStep);
    } catch (error) {
      console.error('Error checking initiation status:', error);
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to load bid initiation status'
      );
    }
  };

  const toggleUserSelection = (userId: string) => {
    setSelectedUsers((prev) => {
      if (prev.includes(userId)) {
        return prev.filter((id) => id !== userId);
      } else {
        return [...prev, userId];
      }
    });
  };

  const getMandatoryFees = () =>
    (tender?.fees || []).filter(isPositiveMandatoryFee);

  const ensureDraftBid = async (declarationAccepted: boolean) => {
    const existingDraft =
      await tenderBidService.getMyDraftBidByTenderId(tenderId);
    if (existingDraft) {
      const draftItems = existingDraft.items ?? [];
      await tenderBidService.updateBid(existingDraft.id, {
        deliveryDays: existingDraft.deliveryDays,
        paymentTerms: existingDraft.paymentTerms || '',
        warrantyTerms: existingDraft.warrantyTerms || '',
        technicalProposal: existingDraft.technicalProposal || '',
        commercialProposal: existingDraft.commercialProposal || '',
        acceptedDeclaration: declarationAccepted,
        items: draftItems.map((item) => ({
          tenderItemId: item.tenderItemId,
          offeredQuantity: item.offeredQuantity,
          unitPrice: item.unitPrice,
          deliveryDays: item.deliveryDays,
          brand: item.brand || '',
          model: item.model || '',
          specifications: item.specifications || '',
          technicalDetails: item.technicalDetails || '',
        })),
      });
      return existingDraft.id;
    }

    const created = await tenderBidService.createBid({
      tenderId,
      paymentTerms: '',
      warrantyTerms: '',
      technicalProposal: '',
      commercialProposal: '',
      associationType,
      acceptedDeclaration: declarationAccepted,
      selectedLotIds: [],
      items: [],
    });
    return created.id;
  };

  const handleNext = async () => {
    if (currentStep === 1) {
      if (!associationType) {
        toast.error('Please select an association type');
        return;
      }
      if (associationType === 'SelectedUsers' && selectedUsers.length === 0) {
        toast.error('Please select at least one user');
        return;
      }

      let assignmentComplete = hasExistingAssignment;
      if (!hasExistingAssignment && businessPartnerId) {
        try {
          setSubmitting(true);
          await tenderAssignmentService.createAssignment({
            tenderId: tenderId,
            businessPartnerId: businessPartnerId,
            assignmentType: associationType,
            assignedUserIds:
              associationType === 'SelectedUsers' ? selectedUsers : undefined,
            notes: `Bid initiation - ${associationType}`,
          });
          setHasExistingAssignment(true);
          assignmentComplete = true;
          toast.success('Association saved.');
        } catch (error: any) {
          console.error('Error creating tender assignment:', error);
          toast.error(
            error.message || 'Failed to save association. Please try again.'
          );
          return;
        } finally {
          setSubmitting(false);
        }
      }

      if (!assignmentComplete) return;
      if (tender?.requiresAcceptanceDeclaration) {
        setCurrentStep(2);
        return;
      }
      if (getMandatoryFees().length > 0) {
        try {
          setSubmitting(true);
          await ensureDraftBid(false);
          setCurrentStep(3);
        } catch (error: any) {
          toast.error(
            error.message || 'Failed to prepare the bid payment step.'
          );
        } finally {
          setSubmitting(false);
        }
        return;
      }
      await handleProceedToBid(assignmentComplete);
      return;
    }

    if (currentStep === 2) {
      if (tender?.requiresAcceptanceDeclaration && !acceptedDeclaration) {
        toast.error('You must accept the declaration to proceed');
        return;
      }

      try {
        setSubmitting(true);
        await ensureDraftBid(acceptedDeclaration);
        toast.success('Declaration acceptance saved.');
      } catch (error: any) {
        console.error('Error saving declaration acceptance:', error);
        toast.error(
          error.message ||
            'Failed to save declaration acceptance. Please try again.'
        );
        return;
      } finally {
        setSubmitting(false);
      }
      if (getMandatoryFees().length > 0) {
        setCurrentStep(3);
      } else {
        await handleProceedToBid(true);
      }
      return;
    }

    if (currentStep === 3) {
      try {
        const status = await tenderBidService.getInitiationStatus(tenderId);
        setInitiationStatus(status);
        if (!status.paymentEvidenceAccepted) {
          toast.error(
            'Provide valid payment evidence for every mandatory tender fee before submitting the sealed bid.'
          );
          return;
        }
      } catch (error: any) {
        toast.error(error.message || 'Failed to verify tender fee payment.');
        return;
      }
      await handleProceedToBid(true);
    }
  };

  const handlePrevious = () => {
    if (currentStep > 1) {
      setCurrentStep((prev) => prev - 1);
    }
  };

  const handleProceedToBid = async (
    assignmentComplete = hasExistingAssignment
  ) => {
    if (!businessPartnerId) {
      toast.error('Business partner information not found');
      return;
    }

    try {
      setSubmitting(true);

      // Assignment should already be created in Step 1
      // Just verify it exists
      if (!assignmentComplete) {
        toast.error('Please complete Step 1 (Association Type) first');
        setCurrentStep(1);
        return;
      }

      // Store the initiation data in session storage
      const initiationData = {
        associationType,
        selectedUsers,
        acceptedDeclaration,
      };
      sessionStorage.setItem(
        'bidInitiationData',
        JSON.stringify(initiationData)
      );

      // Navigate to the bid submission wizard
      toast.success('Proceeding to bid submission');
      router.push(`/external-portal/tenders/${tenderId}/submit-bid`);
    } catch (error: any) {
      console.error('Error proceeding to bid submission:', error);
      toast.error(error.message || 'Failed to create tender assignment');
    } finally {
      setSubmitting(false);
    }
  };

  const handleDownloadDeclaration = () => {
    if (!tender) return;

    // Find the acceptance declaration document
    const acceptanceDoc = tender.documents?.find(
      (doc) => doc.documentType === 'AcceptanceDeclaration'
    );

    if (acceptanceDoc) {
      try {
        tenderService.downloadTenderDocument(
          tenderId,
          acceptanceDoc.id,
          acceptanceDoc.documentName
        );
        toast.success('Downloading acceptance declaration...');
      } catch (error) {
        console.error('Error downloading acceptance declaration:', error);
        toast.error('Failed to download document');
      }
    } else {
      toast.error('Acceptance declaration document not found');
    }
  };

  const handlePayment = async (fee: TenderFeeDto) => {
    const transactionId = paymentReferences[fee.id]?.trim();
    if (!transactionId) {
      toast.error(
        'Enter the bank, receipt, or transaction reference for this payment.'
      );
      return;
    }
    try {
      setSubmitting(true);
      const draftBidId =
        initiationStatus?.draftBidId ||
        (await tenderBidService.getMyDraftBidByTenderId(tenderId))?.id;
      if (!draftBidId) {
        throw new Error(
          'The bid draft is unavailable. Return to the previous step and try again.'
        );
      }
      await tenderBidService.recordBidPayment(draftBidId, {
        tenderFeeId: fee.id,
        amount: fee.amount,
        currency: fee.currency,
        paymentMethod: fee.paymentMethod,
        transactionId,
        paymentProof: transactionId,
      });
      const status = await tenderBidService.getInitiationStatus(tenderId);
      setInitiationStatus(status);
      toast.success('Payment recorded and awaiting verification.');
    } catch (error: any) {
      toast.error(error.message || 'Failed to record tender fee payment.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
          <p className="mt-4 text-muted-foreground">
            Loading tender details...
          </p>
        </div>
      </div>
    );
  }

  if (!tender) {
    return (
      <div className="container mx-auto py-8">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Tender not found</AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 max-w-4xl">
      {/* Header */}
      <div className="mb-6">
        <Button
          variant="ghost"
          onClick={() => router.push(`/external-portal/tenders/${tenderId}`)}
          className="mb-4"
        >
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Tender Details
        </Button>
        <h1 className="text-3xl font-bold">Initiate Bid Submission</h1>
        <p className="text-muted-foreground mt-2">
          {tender.tenderNumber} - {tender.title}
        </p>
      </div>

      {/* Progress Steps */}
      <div className="mb-8">
        <div className="flex items-center justify-between">
          {STEPS.map((step, index) => {
            const StepIcon = step.icon;
            const isActive = currentStep === step.number;
            const isCompleted = currentStep > step.number;

            return (
              <div key={step.number} className="flex items-center flex-1">
                <div className="flex flex-col items-center flex-1">
                  <div
                    className={`
                      w-12 h-12 rounded-full flex items-center justify-center border-2 transition-colors
                      ${isActive ? 'bg-primary text-primary-foreground border-primary' : ''}
                      ${isCompleted ? 'bg-green-500 text-white border-green-500' : ''}
                      ${!isActive && !isCompleted ? 'bg-muted text-muted-foreground border-muted' : ''}
                    `}
                  >
                    {isCompleted ? (
                      <CheckCircle className="h-6 w-6" />
                    ) : (
                      <StepIcon className="h-6 w-6" />
                    )}
                  </div>
                  <p
                    className={`mt-2 text-sm font-medium text-center ${isActive ? 'text-primary' : 'text-muted-foreground'}`}
                  >
                    {step.title}
                  </p>
                </div>
                {index < STEPS.length - 1 && (
                  <div
                    className={`h-0.5 flex-1 mx-4 ${isCompleted ? 'bg-green-500' : 'bg-muted'}`}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Step Content */}
      <Card>
        <CardHeader>
          <CardTitle>{STEPS[currentStep - 1].title}</CardTitle>
          <CardDescription>
            {currentStep === 1 &&
              'Choose how to associate users with this tender'}
            {currentStep === 2 && 'Review and accept the supplier declaration'}
            {currentStep === 3 && 'Verify payment status before proceeding'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {/* Step 1: Type of Association */}
          {currentStep === 1 && (
            <div className="space-y-6">
              {hasExistingAssignment && (
                <Alert>
                  <CheckCircle className="h-4 w-4" />
                  <AlertDescription>
                    You have already created a tender assignment for this
                    tender. You can proceed to the next step.
                  </AlertDescription>
                </Alert>
              )}
              <RadioGroup
                value={associationType}
                onValueChange={(value: any) => setAssociationType(value)}
                disabled={hasExistingAssignment}
              >
                <div className="flex items-start space-x-3 p-4 border rounded-lg hover:bg-accent cursor-pointer">
                  <RadioGroupItem value="AllUsers" id="all-users" />
                  <div className="flex-1">
                    <Label htmlFor="all-users" className="cursor-pointer">
                      <div className="flex items-center gap-2 mb-1">
                        <Users className="h-5 w-5 text-primary" />
                        <span className="font-semibold">
                          Associate all users of this Supplier with this Tender
                        </span>
                      </div>
                      <p className="text-sm text-muted-foreground">
                        All users in your organization will have access to work
                        on this tender
                      </p>
                    </Label>
                  </div>
                </div>

                <div className="flex items-start space-x-3 p-4 border rounded-lg hover:bg-accent cursor-pointer">
                  <RadioGroupItem value="Self" id="self" />
                  <div className="flex-1">
                    <Label htmlFor="self" className="cursor-pointer">
                      <div className="flex items-center gap-2 mb-1">
                        <User className="h-5 w-5 text-primary" />
                        <span className="font-semibold">
                          Associate only myself with this Tender
                        </span>
                      </div>
                      <p className="text-sm text-muted-foreground">
                        Only you will have access to work on this tender
                      </p>
                    </Label>
                  </div>
                </div>

                <div className="flex items-start space-x-3 p-4 border rounded-lg hover:bg-accent cursor-pointer">
                  <RadioGroupItem value="SelectedUsers" id="selected-users" />
                  <div className="flex-1">
                    <Label htmlFor="selected-users" className="cursor-pointer">
                      <div className="flex items-center gap-2 mb-1">
                        <UserCheck className="h-5 w-5 text-primary" />
                        <span className="font-semibold">
                          Pick users from list to associate with this Tender
                        </span>
                      </div>
                      <p className="text-sm text-muted-foreground">
                        Select specific users who will have access to work on
                        this tender
                      </p>
                    </Label>
                  </div>
                </div>
              </RadioGroup>

              {associationType === 'SelectedUsers' && (
                <Card className="mt-4">
                  <CardHeader>
                    <CardTitle className="text-base">Select Users</CardTitle>
                    <CardDescription>
                      Choose which users from your organization should have
                      access to this tender
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    {loadingUsers ? (
                      <div className="flex items-center justify-center py-8">
                        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
                      </div>
                    ) : availableUsers.length === 0 ? (
                      <Alert>
                        <AlertCircle className="h-4 w-4" />
                        <AlertDescription>
                          No other users found in your organization. Please add
                          users in the User Management page first.
                        </AlertDescription>
                      </Alert>
                    ) : (
                      <div className="space-y-3">
                        {availableUsers.map((user) => (
                          <div
                            key={user.id}
                            className="flex items-center space-x-3 p-3 border rounded-lg hover:bg-accent cursor-pointer"
                            onClick={() => toggleUserSelection(user.userId)}
                          >
                            <Checkbox
                              id={user.id}
                              checked={selectedUsers.includes(user.userId)}
                              onCheckedChange={() =>
                                toggleUserSelection(user.userId)
                              }
                            />
                            <div className="flex-1">
                              <Label
                                htmlFor={user.id}
                                className="cursor-pointer"
                              >
                                <div className="font-medium">
                                  {user.userFullName || user.userEmail}
                                </div>
                                <div className="text-sm text-muted-foreground">
                                  {user.userEmail}
                                </div>
                                <div className="text-xs text-muted-foreground mt-1">
                                  Role: {user.role} •{' '}
                                  {user.phoneNumber || 'No phone'}
                                </div>
                              </Label>
                            </div>
                            <Badge
                              variant={
                                selectedUsers.includes(user.userId)
                                  ? 'default'
                                  : 'outline'
                              }
                            >
                              {selectedUsers.includes(user.userId)
                                ? 'Selected'
                                : 'Not Selected'}
                            </Badge>
                          </div>
                        ))}
                        {selectedUsers.length > 0 && (
                          <Alert>
                            <CheckCircle className="h-4 w-4" />
                            <AlertDescription>
                              {selectedUsers.length} user
                              {selectedUsers.length > 1 ? 's' : ''} selected
                            </AlertDescription>
                          </Alert>
                        )}
                      </div>
                    )}
                  </CardContent>
                </Card>
              )}
            </div>
          )}

          {/* Step 2: Accept Agreement */}
          {currentStep === 2 && (
            <div className="space-y-6">
              {tender.requiresAcceptanceDeclaration ? (
                <>
                  <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                    <div className="flex items-start gap-3">
                      <FileText className="h-5 w-5 text-blue-600 mt-0.5" />
                      <div className="flex-1">
                        <h4 className="font-semibold text-blue-900 mb-2">
                          Supplier Declaration Document
                        </h4>
                        <p className="text-sm text-blue-800 mb-3">
                          Please download and review the supplier declaration
                          document before proceeding.
                        </p>
                        {tender.acceptanceDeclarationDocumentName && (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={handleDownloadDeclaration}
                            className="border-blue-300 text-blue-700 hover:bg-blue-100"
                          >
                            <Download className="h-4 w-4 mr-2" />
                            {tender.acceptanceDeclarationDocumentName}
                          </Button>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="flex items-start space-x-3 p-4 border-2 rounded-lg">
                    <Checkbox
                      id="accept-declaration"
                      checked={acceptedDeclaration}
                      onCheckedChange={(checked) =>
                        setAcceptedDeclaration(checked as boolean)
                      }
                    />
                    <Label
                      htmlFor="accept-declaration"
                      className="cursor-pointer text-sm leading-relaxed"
                    >
                      I have read and understood the supplier declaration
                      document and agree to comply with all terms and conditions
                      stated therein.
                    </Label>
                  </div>
                </>
              ) : (
                <Alert>
                  <CheckCircle className="h-4 w-4" />
                  <AlertDescription>
                    No acceptance declaration is required for this tender. You
                    may proceed to the next step.
                  </AlertDescription>
                </Alert>
              )}
            </div>
          )}

          {/* Step 3: Payment Evidence and Verification */}
          {currentStep === 3 && (
            <div className="space-y-6">
              {initiationStatus?.paymentSatisfied &&
                initiationStatus.paymentRequired && (
                  <Alert>
                    <CheckCircle className="h-4 w-4" />
                    <AlertDescription>
                      Payment has already been completed for this tender. You
                      can proceed to bid submission.
                    </AlertDescription>
                  </Alert>
                )}
              {tender.fees && tender.fees.length > 0 ? (
                <>
                  {tender.fees
                    .filter((fee) => fee.amount > 0)
                    .map((fee) => {
                      const paymentStatus =
                        initiationStatus?.fees.find(
                          (item) => item.tenderFeeId === fee.id
                        )?.status || 'NotPaid';
                      const paymentVerified = paymentStatus === 'Verified';
                      const paymentPending = paymentStatus === 'Pending';
                      const manualPayment =
                        /bank|cash|cheque|check|deposit|manual/i.test(
                          fee.paymentMethod || ''
                        );
                      return (
                        <Card key={fee.id} className="border-2">
                          <CardHeader className="pb-3">
                            <div className="flex items-start justify-between">
                              <div className="space-y-1">
                                <div className="flex items-center gap-2">
                                  <Badge className="text-sm">
                                    {fee.feeType}
                                  </Badge>
                                  {fee.isMandatory ? (
                                    <Badge variant="destructive">
                                      Required
                                    </Badge>
                                  ) : (
                                    <Badge variant="outline">Optional</Badge>
                                  )}
                                </div>
                                <CardTitle className="text-xl">
                                  {fee.description}
                                </CardTitle>
                              </div>
                              <div className="text-right">
                                <p className="text-2xl font-bold text-primary">
                                  {fee.currency}{' '}
                                  {fee.amount.toLocaleString('en-US', {
                                    minimumFractionDigits: 2,
                                    maximumFractionDigits: 2,
                                  })}
                                </p>
                              </div>
                            </div>
                          </CardHeader>
                          <CardContent>
                            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                              <div className="flex items-center gap-2 mb-3">
                                <DollarSign className="h-5 w-5 text-blue-600" />
                                <h4 className="font-semibold text-blue-900">
                                  Payment Details
                                </h4>
                              </div>
                              <div className="space-y-2">
                                <div className="flex items-start gap-2">
                                  <span className="text-sm font-medium text-blue-900 min-w-[120px]">
                                    Payment Kind:
                                  </span>
                                  <span className="text-sm text-blue-800 font-semibold">
                                    Tender Submission
                                  </span>
                                </div>
                                <div className="flex items-start gap-2">
                                  <span className="text-sm font-medium text-blue-900 min-w-[120px]">
                                    Payment Method:
                                  </span>
                                  <span className="text-sm text-blue-800 font-semibold">
                                    {fee.paymentMethod}
                                  </span>
                                </div>
                                {fee.bankAccountDetails && (
                                  <div className="flex items-start gap-2">
                                    <span className="text-sm font-medium text-blue-900 min-w-[120px]">
                                      Payment Details:
                                    </span>
                                    <div className="text-sm text-blue-800 whitespace-pre-wrap flex-1 bg-white p-3 rounded border border-blue-200 font-mono">
                                      {fee.bankAccountDetails}
                                    </div>
                                  </div>
                                )}
                                <div className="flex items-start gap-2">
                                  <span className="text-sm font-medium text-blue-900 min-w-[120px]">
                                    Status:
                                  </span>
                                  <div>
                                    {paymentVerified ? (
                                      <Badge className="bg-green-100 text-green-800">
                                        <CheckCircle className="h-3 w-3 mr-1" />
                                        Verified
                                      </Badge>
                                    ) : paymentPending ? (
                                      <Badge className="bg-yellow-100 text-yellow-800">
                                        <Clock className="h-3 w-3 mr-1" />
                                        {manualPayment
                                          ? 'Evidence awaiting verification'
                                          : 'Provider confirmation pending'}
                                      </Badge>
                                    ) : paymentStatus === 'Rejected' ? (
                                      <Badge variant="destructive">
                                        <XCircle className="h-3 w-3 mr-1" />
                                        Rejected
                                      </Badge>
                                    ) : (
                                      <Badge className="bg-orange-100 text-orange-800">
                                        <XCircle className="h-3 w-3 mr-1" />
                                        Not Paid
                                      </Badge>
                                    )}
                                  </div>
                                </div>
                              </div>
                            </div>

                            {!paymentVerified && !paymentPending && (
                              <div className="mt-4 space-y-3">
                                <Label htmlFor={`payment-reference-${fee.id}`}>
                                  Bank, receipt, or transaction reference
                                </Label>
                                <Input
                                  id={`payment-reference-${fee.id}`}
                                  value={paymentReferences[fee.id] || ''}
                                  onChange={(event) =>
                                    setPaymentReferences((current) => ({
                                      ...current,
                                      [fee.id]: event.target.value,
                                    }))
                                  }
                                  placeholder="Enter the payment reference"
                                />
                                <Button
                                  onClick={() => handlePayment(fee)}
                                  className="w-full"
                                  disabled={submitting}
                                >
                                  <DollarSign className="h-4 w-4 mr-2" />
                                  Submit Payment for Verification
                                </Button>
                              </div>
                            )}
                            {paymentPending && (
                              <Alert className="mt-4">
                                <Clock className="h-4 w-4" />
                                <AlertDescription>
                                  {manualPayment
                                    ? 'Your manual payment evidence is awaiting verification. You may submit the sealed bid now, but it cannot be opened or evaluated until an authorised procurement user verifies the payment.'
                                    : 'Online payment provider confirmation is still pending. You may submit the sealed bid now, but it cannot be opened or evaluated until confirmation is received.'}
                                </AlertDescription>
                              </Alert>
                            )}
                          </CardContent>
                        </Card>
                      );
                    })}

                  {initiationStatus?.paymentSatisfied && (
                    <Alert className="bg-green-50 border-green-200">
                      <CheckCircle className="h-4 w-4 text-green-600" />
                      <AlertDescription className="text-green-800">
                        Payment verified successfully. You may now proceed to
                        submit your bid.
                      </AlertDescription>
                    </Alert>
                  )}

                  {initiationStatus?.paymentPendingVerification && (
                    <Alert className="bg-amber-50 border-amber-200">
                      <Clock className="h-4 w-4 text-amber-700" />
                      <AlertDescription className="text-amber-900">
                        Payment evidence has been recorded, so sealed bid
                        submission is allowed. Payment remains unresolved and
                        the bid stays excluded from opening and evaluation until
                        an authorised procurement user verifies it.
                      </AlertDescription>
                    </Alert>
                  )}

                  {!initiationStatus?.paymentSatisfied &&
                    !initiationStatus?.paymentEvidenceAccepted &&
                    getMandatoryFees().length > 0 && (
                      <Alert variant="destructive">
                        <AlertCircle className="h-4 w-4" />
                        <AlertDescription>
                          Provide valid manual payment evidence, or wait for
                          online provider confirmation, before bid submission.
                        </AlertDescription>
                      </Alert>
                    )}
                </>
              ) : (
                <Alert>
                  <CheckCircle className="h-4 w-4" />
                  <AlertDescription>
                    No fees are required for this tender. You may proceed to
                    submit your bid.
                  </AlertDescription>
                </Alert>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Navigation Buttons */}
      <div className="flex justify-between mt-6">
        <Button
          variant="outline"
          onClick={handlePrevious}
          disabled={currentStep === 1}
        >
          <ArrowLeft className="h-4 w-4 mr-2" />
          Previous
        </Button>

        <Button onClick={handleNext} disabled={submitting}>
          {currentStep === STEPS.length ? 'Proceed to Bid Submission' : 'Next'}
          <ArrowRight className="h-4 w-4 ml-2" />
        </Button>
      </div>
    </div>
  );
}
