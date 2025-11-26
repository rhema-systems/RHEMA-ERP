'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
  AlertCircle
} from 'lucide-react';
import {
  businessPartnerRegistrationService,
  type RegistrationFormData,
  type BusinessPartnerRegistrationDetailDto
} from '@/services/businessPartnerRegistrationService';
import { toast } from 'sonner';

// Import step components (we'll create these next)
import CompanyInformation from '@/components/procurement/registration/CompanyInformation';
import ContactInformation from '@/components/procurement/registration/ContactInformation';
import DocumentUpload from '@/components/procurement/registration/DocumentUpload';
import LicenseInformation from '@/components/procurement/registration/LicenseInformation';
import RegistrationSummary from '@/components/procurement/registration/RegistrationSummary';

const STEPS = [
  { id: 1, name: 'Company Information', icon: Building2, component: 'CompanyInformation' },
  { id: 2, name: 'Contact Information', icon: Contact, component: 'ContactInformation' },
  { id: 3, name: 'Documents', icon: FileText, component: 'DocumentUpload' },
  { id: 4, name: 'Licenses', icon: Award, component: 'LicenseInformation' },
  { id: 5, name: 'Review & Submit', icon: CheckCircle2, component: 'RegistrationSummary' },
];

// Validation functions for each step
const validateStep1 = (formData: RegistrationFormData): string[] => {
  const errors: string[] = [];
  if (!formData.companyName?.trim()) errors.push('Company Name is required');
  if (!formData.partnerType) errors.push('Partner Type is required');
  return errors;
};

const validateStep2 = (formData: RegistrationFormData): string[] => {
  const errors: string[] = [];
  if (!formData.email?.trim()) errors.push('Email is required');
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) errors.push('Invalid email format');
  if (!formData.phone?.trim()) errors.push('Phone number is required');
  if (!formData.physicalAddress?.trim()) errors.push('Physical address is required');
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

export default function BusinessPartnerRegistrationPage() {
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [registrationId, setRegistrationId] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [hasStartedRegistration, setHasStartedRegistration] = useState(false);
  const [formData, setFormData] = useState<RegistrationFormData>({
    companyName: '',
    partnerType: 'Supplier',
    email: '',
    phone: '',
  });

  // Prevent browser back button during registration
  useEffect(() => {
    if (hasStartedRegistration) {
      const handleBeforeUnload = (e: BeforeUnloadEvent) => {
        e.preventDefault();
        e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
        return e.returnValue;
      };

      const handlePopState = (e: PopStateEvent) => {
        if (window.confirm('Are you sure you want to leave? Your progress will be saved as a draft.')) {
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
      const registrations = await businessPartnerRegistrationService.getMyRegistrations();
      const draft = registrations.find(r => r.status === 'Draft');

      if (draft) {
        setRegistrationId(draft.id);
        setHasStartedRegistration(true);
        const detail = await businessPartnerRegistrationService.getById(draft.id);
        const parsedData = businessPartnerRegistrationService.parseRegistrationData(detail.registrationData);
        if (parsedData) {
          setFormData(parsedData);
        }
      }
    } catch (error) {
      console.error('Error loading draft:', error);
    }
  };

  const validateCurrentStep = (): boolean => {
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

  const handleSaveDraft = async () => {
    setSaving(true);
    try {
      setHasStartedRegistration(true);
      const completionPercentage = businessPartnerRegistrationService.calculateCompletionPercentage(formData);

      if (registrationId) {
        // Update existing draft
        const updateDto = businessPartnerRegistrationService.convertFormDataToUpdateDto(formData, completionPercentage);
        await businessPartnerRegistrationService.update(registrationId, updateDto);
        toast.success('Draft saved successfully');
      } else {
        // Create new draft
        const createDto = businessPartnerRegistrationService.convertFormDataToCreateDto(formData);
        const result = await businessPartnerRegistrationService.create(createDto);
        setRegistrationId(result.id);
        toast.success('Draft created successfully');
      }
    } catch (error: any) {
      toast.error(error.message || 'Failed to save draft');
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

    // Save draft before moving to next step
    await handleSaveDraft();

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
    setFormData(prev => ({ ...prev, ...data }));
  };

  const completionPercentage = businessPartnerRegistrationService.calculateCompletionPercentage(formData);

  const renderStepComponent = () => {
    switch (currentStep) {
      case 1:
        return <CompanyInformation formData={formData} updateFormData={updateFormData} />;
      case 2:
        return <ContactInformation formData={formData} updateFormData={updateFormData} />;
      case 3:
        return <DocumentUpload formData={formData} updateFormData={updateFormData} registrationId={registrationId} />;
      case 4:
        return <LicenseInformation formData={formData} updateFormData={updateFormData} />;
      case 5:
        return <RegistrationSummary formData={formData} onSubmit={handleSubmit} loading={loading} />;
      default:
        return null;
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-5xl mx-auto px-4">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">Business Partner Registration</h1>
          <p className="text-gray-600">Complete the registration process to become an approved business partner</p>
          {hasStartedRegistration && (
            <Alert className="mt-4">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                Your progress is being saved automatically. Please complete all required fields before proceeding to the next step.
              </AlertDescription>
            </Alert>
          )}
        </div>

        {/* Validation Errors */}
        {validationErrors.length > 0 && (
          <Alert variant="destructive" className="mb-6">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              <div className="font-semibold mb-2">Please fix the following errors:</div>
              <ul className="list-disc list-inside space-y-1">
                {validationErrors.map((error, index) => (
                  <li key={index}>{error}</li>
                ))}
              </ul>
            </AlertDescription>
          </Alert>
        )}

        {/* Progress Bar */}
        <Card className="mb-6">
          <CardContent className="pt-6">
            <div className="mb-4">
              <div className="flex justify-between items-center mb-2">
                <span className="text-sm font-medium text-gray-700">Registration Progress</span>
                <span className="text-sm font-medium text-gray-700">{completionPercentage}%</span>
              </div>
              <Progress value={completionPercentage} className="h-2" />
            </div>

            {/* Step Indicators */}
            <div className="flex justify-between">
              {STEPS.map((step, index) => {
                const StepIcon = step.icon;
                const isActive = currentStep === step.id;
                const isCompleted = currentStep > step.id;

                return (
                  <div key={step.id} className="flex flex-col items-center flex-1">
                    <div
                      className={`w-10 h-10 rounded-full flex items-center justify-center mb-2 ${
                        isActive
                          ? 'bg-blue-600 text-white'
                          : isCompleted
                          ? 'bg-green-600 text-white'
                          : 'bg-gray-200 text-gray-500'
                      }`}
                    >
                      <StepIcon className="w-5 h-5" />
                    </div>
                    <span className={`text-xs text-center ${isActive ? 'font-semibold text-blue-600' : 'text-gray-600'}`}>
                      {step.name}
                    </span>
                    {index < STEPS.length - 1 && (
                      <div className="hidden md:block absolute w-full h-0.5 bg-gray-200 top-5 left-1/2 -z-10" />
                    )}
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>

        {/* Step Content */}
        <Card className="mb-6">
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
              onClick={handleSaveDraft}
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
              <Button onClick={handleSubmit} disabled={loading || !registrationId}>
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
    </div>
  );
}

