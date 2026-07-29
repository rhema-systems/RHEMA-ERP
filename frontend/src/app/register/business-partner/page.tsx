'use client';

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Building2,
  Contact,
  FileText,
  Award,
  CheckCircle2,
  ArrowLeft,
  ArrowRight,
  Save,
  AlertCircle,
} from 'lucide-react';
import {
  businessPartnerRegistrationService,
  type RegistrationFormData,
  type BusinessPartnerRegistrationDetailDto,
} from '@/services/businessPartnerRegistrationService';
import { toast } from 'sonner';

// Import step components (we'll create these next)
import CompanyInformation from '@/components/procurement/registration/CompanyInformation';
import ContactInformation from '@/components/procurement/registration/ContactInformation';
import DocumentUpload from '@/components/procurement/registration/DocumentUpload';
import LicenseInformation from '@/components/procurement/registration/LicenseInformation';
import RegistrationSummary from '@/components/procurement/registration/RegistrationSummary';

const STEPS = [
  {
    id: 1,
    name: 'Company Information',
    icon: Building2,
    component: 'CompanyInformation',
  },
  {
    id: 2,
    name: 'Contact Information',
    icon: Contact,
    component: 'ContactInformation',
  },
  { id: 3, name: 'Documents', icon: FileText, component: 'DocumentUpload' },
  { id: 4, name: 'Licenses', icon: Award, component: 'LicenseInformation' },
  {
    id: 5,
    name: 'Review & Submit',
    icon: CheckCircle2,
    component: 'RegistrationSummary',
  },
];

// Validation functions for each step
const validateStep1 = (formData: RegistrationFormData): string[] => {
  const errors: string[] = [];
  if (!formData.companyName?.trim()) errors.push('Company Name is required');
  if (!formData.partnerType) errors.push('Partner Type is required');
  if (!formData.registrationCategory)
    errors.push('Registration Category is required');
  return errors;
};

const validateStep2 = (formData: RegistrationFormData): string[] => {
  const errors: string[] = [];
  if (!formData.email?.trim()) errors.push('Email is required');
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email))
    errors.push('Invalid email format');
  if (!formData.phone?.trim()) errors.push('Phone number is required');
  if (!formData.physicalAddress?.trim())
    errors.push('Physical address is required');
  if (!formData.city?.trim()) errors.push('City is required');
  if (!formData.country?.trim()) errors.push('Country is required');
  return errors;
};

const validateStep3 = (formData: RegistrationFormData): string[] => {
  const errors: string[] = [];
  // Documents are optional but we can add validation if needed
  return errors;
};

const validateStep4 = (formData: RegistrationFormData): string[] => {
  const errors: string[] = [];
  // Licenses are optional but we can add validation if needed
  return errors;
};

// Function to determine which step to show based on completed data
const determineCurrentStep = (formData: RegistrationFormData): number => {
  // Check if step 1 is complete
  const step1Errors = validateStep1(formData);
  if (step1Errors.length > 0) return 1;

  // Check if step 2 is complete
  const step2Errors = validateStep2(formData);
  if (step2Errors.length > 0) return 2;

  // Step 3 (documents) is optional, so check if step 4 has data
  // If step 4 has no data, stay on step 3
  if (!formData.licenses || formData.licenses.length === 0) return 3;

  // If all steps have data, go to step 4 (or could be step 5 for review)
  return 4;
};

export default function BusinessPartnerRegistrationPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [currentStep, setCurrentStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [registrationId, setRegistrationId] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [hasStartedRegistration, setHasStartedRegistration] = useState(false);
  const [formData, setFormData] = useState<RegistrationFormData>({
    companyName: '',
    partnerType: 'Supplier',
    registrationCategory: 'Goods',
    email: '',
    phone: '',
  });

  // Prevent browser back button during registration
  useEffect(() => {
    if (hasStartedRegistration) {
      const handleBeforeUnload = (e: BeforeUnloadEvent) => {
        e.preventDefault();
        e.returnValue =
          'You have unsaved changes. Are you sure you want to leave?';
        return e.returnValue;
      };

      const handlePopState = (e: PopStateEvent) => {
        if (
          window.confirm(
            'Are you sure you want to leave? Your progress will be saved as a draft.'
          )
        ) {
          // Allow navigation
          return;
        } else {
          // Prevent navigation
          window.history.pushState(null, '', window.location.href);
        }
      };

      // Push initial state
      window.history.pushState(null, '', window.location.href);

      window.addEventListener('beforeunload', handleBeforeUnload);
      window.addEventListener('popstate', handlePopState);

      return () => {
        window.removeEventListener('beforeunload', handleBeforeUnload);
        window.removeEventListener('popstate', handlePopState);
      };
    }
  }, [hasStartedRegistration]);

  // Load draft registration if exists
  useEffect(() => {
    loadDraftRegistration();
  }, []);

  const loadDraftRegistration = async () => {
    try {
      // Check if a specific registration ID is provided in the URL
      const urlRegistrationId = searchParams?.get('id');

      if (urlRegistrationId) {
        // Load the specific registration from the URL
        try {
          const detail =
            await businessPartnerRegistrationService.getById(urlRegistrationId);

          // Only allow editing if status is Draft or MoreInfoRequired
          if (
            detail.status === 'Draft' ||
            detail.status === 'MoreInfoRequired'
          ) {
            setRegistrationId(urlRegistrationId);
            setHasStartedRegistration(true);
            const parsedData =
              businessPartnerRegistrationService.parseRegistrationData(
                detail.registrationData
              );
            if (parsedData) {
              setFormData(parsedData);
              // Determine which step to show based on completed data
              const stepToShow = determineCurrentStep(parsedData);
              setCurrentStep(stepToShow);

              // Show notification if this is a MoreInfoRequired registration
              if (detail.status === 'MoreInfoRequired') {
                toast.info(
                  'Please update the required information and resubmit your registration.'
                );
              }
            }
          } else {
            toast.error('This registration cannot be edited.');
            router.push('/external-portal/business-partner');
          }
        } catch (detailError: any) {
          console.error('Error loading registration from URL:', detailError);
          toast.error('Failed to load the registration. Please try again.');
          router.push('/external-portal/business-partner');
        }
      } else {
        // No ID in URL - load the first editable registration (old behavior)
        const registrations =
          await businessPartnerRegistrationService.getMyRegistrations();
        // Load either Draft or MoreInfoRequired registrations for editing
        const editableRegistration = registrations.find(
          (r) => r.status === 'Draft' || r.status === 'MoreInfoRequired'
        );

        if (editableRegistration) {
          try {
            // Verify the registration still exists and is accessible
            const detail = await businessPartnerRegistrationService.getById(
              editableRegistration.id
            );
            setRegistrationId(editableRegistration.id);
            setHasStartedRegistration(true);
            const parsedData =
              businessPartnerRegistrationService.parseRegistrationData(
                detail.registrationData
              );
            if (parsedData) {
              setFormData(parsedData);
              // Determine which step to show based on completed data
              const stepToShow = determineCurrentStep(parsedData);
              setCurrentStep(stepToShow);

              // Show notification if this is a MoreInfoRequired registration
              if (editableRegistration.status === 'MoreInfoRequired') {
                toast.info(
                  'Please update the required information and resubmit your registration.'
                );
              }
            }
          } catch (detailError: any) {
            // If loading the registration detail fails, clear the state
            console.error('Error loading registration details:', detailError);
            setRegistrationId(null);
            setHasStartedRegistration(false);
            // Don't show toast on initial load to avoid spam
          }
        }
      }
    } catch (error) {
      console.error('Error loading registration:', error);
      // Clear state if we can't load registrations
      setRegistrationId(null);
      setHasStartedRegistration(false);
    }
  };

  const validateCurrentStep = (): boolean => {
    let errors: string[] = [];

    switch (currentStep) {
      case 1:
        errors = validateStep1(formData);
        break;
      default:
        errors = [];
    }

    setValidationErrors(errors);

    if (errors.length > 0) {
      toast.error('Please fix the validation errors before proceeding');
      return false;
    }

    return true;
  };

  // Run full validation across all steps before final submission
  const validateForSubmit = (): boolean => {
    const errors: string[] = [];

    errors.push(...validateStep1(formData));
    errors.push(...validateStep2(formData));
    errors.push(...validateStep3(formData));
    errors.push(...validateStep4(formData));

    setValidationErrors(errors);

    if (errors.length > 0) {
      toast.error('Please fix the validation errors before submitting');
      return false;
    }

    return true;
  };

  const handleSaveDraft = async (showToast: boolean = true) => {
    setSaving(true);
    try {
      setHasStartedRegistration(true);
      const completionPercentage =
        businessPartnerRegistrationService.calculateCompletionPercentage(
          formData
        );

      // Check if user already has an editable registration (Draft or MoreInfoRequired)
      const registrations =
        await businessPartnerRegistrationService.getMyRegistrations();
      const existingEditable = registrations.find(
        (r) => r.status === 'Draft' || r.status === 'MoreInfoRequired'
      );

      if (existingEditable) {
        // Update existing registration
        const updateDto =
          businessPartnerRegistrationService.convertFormDataToUpdateDto(
            formData,
            completionPercentage
          );
        await businessPartnerRegistrationService.update(
          existingEditable.id,
          updateDto
        );
        setRegistrationId(existingEditable.id);
        if (showToast) {
          toast.success(
            existingEditable.status === 'MoreInfoRequired'
              ? 'Information updated successfully'
              : 'Draft updated successfully'
          );
        }
      } else {
        // Create new draft
        const createDto =
          businessPartnerRegistrationService.convertFormDataToCreateDto(
            formData
          );
        const result =
          await businessPartnerRegistrationService.create(createDto);
        setRegistrationId(result.id);
        if (showToast) {
          toast.success('Draft saved successfully');
        }
      }
    } catch (error: any) {
      console.error('Save draft error:', error);
      // Clear state on error
      setRegistrationId(null);
      setHasStartedRegistration(false);
      if (showToast) {
        toast.error(error.message || 'Failed to save draft');
      }
    } finally {
      setSaving(false);
    }
  };

  const handleNext = async () => {
    // Clear previous validation errors
    setValidationErrors([]);

    // Validate current step
    if (!validateCurrentStep()) {
      return;
    }

    // Save draft before moving to next step (without showing toast)
    await handleSaveDraft(false);

    if (currentStep < STEPS.length) {
      setCurrentStep(currentStep + 1);
      // Scroll to top
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  const handlePrevious = () => {
    // Clear validation errors when going back
    setValidationErrors([]);

    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
      // Scroll to top
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  const handleSubmit = async () => {
    if (!registrationId) {
      toast.error('Please save your registration first');
      return;
    }

    // For final submission, validate all steps, not just the current one
    if (!validateForSubmit()) {
      return;
    }

    setLoading(true);
    try {
      await businessPartnerRegistrationService.submit(registrationId);
      toast.success('Registration submitted successfully!');
      router.push(`/register/business-partner/success?id=${registrationId}`);
    } catch (error: any) {
      toast.error(error.message || 'Failed to submit registration');
    } finally {
      setLoading(false);
    }
  };

  const updateFormData = (data: Partial<RegistrationFormData>) => {
    setFormData((prev) => ({ ...prev, ...data }));
  };

  const completionPercentage =
    businessPartnerRegistrationService.calculateCompletionPercentage(formData);

  const renderStepComponent = () => {
    switch (currentStep) {
      case 1:
        return (
          <CompanyInformation
            formData={formData}
            updateFormData={updateFormData}
          />
        );
      case 2:
        return (
          <ContactInformation
            formData={formData}
            updateFormData={updateFormData}
          />
        );
      case 3:
        return (
          <DocumentUpload
            formData={formData}
            updateFormData={updateFormData}
            registrationId={registrationId}
          />
        );
      case 4:
        return (
          <LicenseInformation
            formData={formData}
            updateFormData={updateFormData}
          />
        );
      case 5:
        return (
          <RegistrationSummary
            formData={formData}
            onSubmit={handleSubmit}
            loading={loading}
          />
        );
      default:
        return null;
    }
  };

  return (
    <div className="space-y-6">
      {/* Back Button */}
      <div>
        <Button
          variant="outline"
          onClick={() => router.push('/external-portal/business-partner')}
          className="mb-4"
        >
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to Dashboard
        </Button>
      </div>

      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900 mb-2">
          Business Partner Registration
        </h1>
        <p className="text-gray-600">
          Complete the registration process to become an approved business
          partner
        </p>
        {hasStartedRegistration && (
          <Alert className="mt-4">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Your progress is being saved automatically. Please make sure all
              required fields are completed before submitting your registration.
            </AlertDescription>
          </Alert>
        )}
      </div>

      {/* Validation Errors */}
      {validationErrors.length > 0 && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            <div className="font-semibold mb-2">
              Please fix the following errors:
            </div>
            <ul className="list-disc list-inside space-y-1">
              {validationErrors.map((error, index) => (
                <li key={index}>{error}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {/* Connected Progress Bar */}
      <Card>
        <CardContent className="pt-8 pb-6">
          {/* Step Indicators with Connected Lines */}
          <div className="relative">
            {/* Step Circles and Labels */}
            <div className="flex items-start justify-between">
              {STEPS.map((step, index) => {
                const StepIcon = step.icon;
                const isActive = currentStep === step.id;
                const isCompleted = currentStep > step.id;

                return (
                  <div key={step.id} className="flex items-center flex-1">
                    {/* Step Container */}
                    <div className="flex flex-col items-center flex-shrink-0">
                      {/* Circle with Number/Icon */}
                      <div
                        className={`w-12 h-12 rounded-full flex items-center justify-center font-semibold text-lg transition-all duration-300 ${
                          isActive
                            ? 'bg-blue-500 text-white shadow-lg scale-110'
                            : isCompleted
                              ? 'bg-blue-500 text-white'
                              : 'bg-white text-gray-400 border-2 border-gray-300'
                        }`}
                      >
                        {isCompleted ? (
                          <CheckCircle2 className="w-6 h-6" />
                        ) : (
                          <span>{step.id}</span>
                        )}
                      </div>

                      {/* Step Label */}
                      <div className="mt-3 text-center max-w-[100px]">
                        <div
                          className={`text-xs font-medium transition-colors duration-300 ${
                            isActive
                              ? 'text-blue-600'
                              : isCompleted
                                ? 'text-blue-500'
                                : 'text-gray-500'
                          }`}
                        >
                          {step.name}
                        </div>
                        {isActive && (
                          <div className="text-[10px] text-gray-500 mt-1">
                            Current step
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Connection Line */}
                    {index < STEPS.length - 1 && (
                      <div className="flex-1 h-0.5 mx-4 mb-16">
                        <div
                          className={`h-full transition-all duration-300 ${
                            currentStep > step.id
                              ? 'bg-blue-500'
                              : 'bg-gray-300'
                          }`}
                        />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Step Content */}
      <Card>
        <CardHeader>
          <CardTitle>{STEPS[currentStep - 1].name}</CardTitle>
          <CardDescription>
            Step {currentStep} of {STEPS.length}
          </CardDescription>
        </CardHeader>
        <CardContent>{renderStepComponent()}</CardContent>
      </Card>

      {/* Navigation Buttons */}
      <div className="flex justify-between items-center">
        <Button
          variant="outline"
          onClick={handlePrevious}
          disabled={currentStep === 1}
        >
          <ArrowLeft className="w-4 h-4 mr-2" />
          Previous
        </Button>

        <div className="flex gap-2">
          <Button
            variant="outline"
            onClick={() => {
              void handleSaveDraft();
            }}
            disabled={saving}
          >
            <Save className="w-4 h-4 mr-2" />
            {saving ? 'Saving...' : 'Save Draft'}
          </Button>

          {currentStep < STEPS.length ? (
            <Button onClick={handleNext} disabled={saving}>
              Next
              <ArrowRight className="w-4 h-4 ml-2" />
            </Button>
          ) : (
            <Button
              onClick={handleSubmit}
              disabled={loading || !registrationId}
            >
              {loading ? 'Submitting...' : 'Submit Registration'}
            </Button>
          )}
        </div>
      </div>

      {/* Registration ID Badge */}
      {registrationId && (
        <div className="mt-4 text-center">
          <Badge variant="outline">
            Registration ID: {registrationId.substring(0, 8)}...
          </Badge>
        </div>
      )}
    </div>
  );
}
