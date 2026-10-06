'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
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
  X,
} from 'lucide-react';
import {
  businessPartnerRegistrationService,
  type CreateBusinessPartnerRegistrationDto,
  type RegistrationFormData,
  type UpdateBusinessPartnerRegistrationDto,
} from '@/services/businessPartnerRegistrationService';
import { toast } from 'sonner';

// Import step components
import CompanyInformation from './CompanyInformation';
import ContactInformation from './ContactInformation';
import DocumentUpload from './DocumentUpload';
import LicenseInformation from './LicenseInformation';
import RegistrationSummary from './RegistrationSummary';

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

interface RegistrationWizardDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export default function RegistrationWizardDialog({
  open,
  onOpenChange,
}: RegistrationWizardDialogProps) {
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [registrationId, setRegistrationId] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [formData, setFormData] = useState<RegistrationFormData>({
    companyName: '',
    partnerType: 'Supplier',
    registrationCategory: 'Goods',
    email: '',
    phone: '',
  });

  // Validation functions
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
      case 4:
        errors = [];
        break;
      default:
        errors = [];
    }
    setValidationErrors(errors);
    return errors.length === 0;
  };

  const buildRegistrationDraftPayload =
    (): CreateBusinessPartnerRegistrationDto => ({
      partnerType: formData.partnerType,
      companyName: formData.companyName,
      registrationNumber: formData.registrationNumber,
      ssnitNumber: formData.ssnitNumber,
      email: formData.email,
      phone: formData.phone,
      registrationData: JSON.stringify(formData),
    });

  const handleSaveDraft = async () => {
    try {
      setSaving(true);
      if (registrationId) {
        const updatePayload: UpdateBusinessPartnerRegistrationDto = {
          companyName: formData.companyName,
          registrationNumber: formData.registrationNumber,
          ssnitNumber: formData.ssnitNumber,
          email: formData.email,
          phone: formData.phone,
          registrationData: JSON.stringify(formData),
          completionPercentage:
            businessPartnerRegistrationService.calculateCompletionPercentage(
              formData
            ),
        };
        await businessPartnerRegistrationService.update(
          registrationId,
          updatePayload
        );
        toast.success('Draft saved successfully');
      } else {
        const created = await businessPartnerRegistrationService.create(
          buildRegistrationDraftPayload()
        );
        setRegistrationId(created.id);
        toast.success('Draft created successfully');
      }
    } catch (error: any) {
      toast.error(error.message || 'Failed to save draft');
    } finally {
      setSaving(false);
    }
  };

  const handleNext = async () => {
    setValidationErrors([]);
    if (!validateCurrentStep()) {
      return;
    }
    await handleSaveDraft();
    if (currentStep < STEPS.length) {
      setCurrentStep(currentStep + 1);
    }
  };

  const handlePrevious = () => {
    setValidationErrors([]);
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
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
      onOpenChange(false);
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

  const renderStepContent = () => {
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
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-6xl max-h-[90vh] overflow-y-auto p-0">
        <div className="sticky top-0 bg-white z-10 border-b px-6 py-4">
          <DialogHeader>
            <DialogTitle className="text-2xl font-bold">
              Business Partner Registration
            </DialogTitle>
            <p className="text-sm text-gray-600 mt-1">
              Complete the registration process to become an approved business
              partner
            </p>
          </DialogHeader>
        </div>

        <div className="px-6 pb-6">
          {/* Validation Errors */}
          {validationErrors.length > 0 && (
            <Alert variant="destructive" className="mb-6">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                <div className="font-semibold mb-1">
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
          <Card className="mb-6">
            <CardContent className="pt-8 pb-6">
              {/* Step Indicators with Connected Lines */}
              <div className="relative">
                {/* Connection Lines */}
                <div className="absolute top-6 left-0 right-0 flex items-center px-8">
                  <div className="flex-1 flex items-center">
                    {STEPS.map((step, index) => {
                      if (index === STEPS.length - 1) return null;
                      const isCompleted = currentStep > step.id;
                      return (
                        <div
                          key={`line-${step.id}`}
                          className={`flex-1 h-1 mx-2 transition-all duration-300 ${
                            isCompleted ? 'bg-blue-500' : 'bg-gray-300'
                          }`}
                        />
                      );
                    })}
                  </div>
                </div>

                {/* Step Circles and Labels */}
                <div className="relative flex justify-between items-start">
                  {STEPS.map((step, index) => {
                    const StepIcon = step.icon;
                    const isActive = currentStep === step.id;
                    const isCompleted = currentStep > step.id;

                    return (
                      <div
                        key={step.id}
                        className="flex flex-col items-center"
                        style={{ flex: '1' }}
                      >
                        {/* Circle with Number/Icon */}
                        <div
                          className={`relative z-10 w-12 h-12 rounded-full flex items-center justify-center font-semibold text-lg transition-all duration-300 ${
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
                        <div className="mt-3 text-center max-w-[120px]">
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
                              Please fill the {step.name.toLowerCase()}
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Progress Percentage */}
              <div className="mt-6 pt-4 border-t border-gray-200">
                <div className="flex justify-between items-center">
                  <span className="text-sm font-medium text-gray-600">
                    Overall Progress
                  </span>
                  <span className="text-sm font-semibold text-blue-600">
                    {completionPercentage}% Complete
                  </span>
                </div>
                <Progress value={completionPercentage} className="h-2 mt-2" />
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
            <CardContent>{renderStepContent()}</CardContent>
          </Card>

          {/* Navigation Buttons */}
          <div className="flex justify-between items-center mt-6">
            <Button
              variant="outline"
              onClick={handlePrevious}
              disabled={currentStep === 1}
            >
              <ArrowLeft className="mr-2 h-4 w-4" />
              Previous
            </Button>

            <div className="flex gap-2">
              <Button
                variant="outline"
                onClick={handleSaveDraft}
                disabled={saving}
              >
                <Save className="mr-2 h-4 w-4" />
                {saving ? 'Saving...' : 'Save Draft'}
              </Button>

              {currentStep < STEPS.length ? (
                <Button onClick={handleNext}>
                  Next
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Button>
              ) : (
                <Button onClick={handleSubmit} disabled={loading}>
                  {loading ? 'Submitting...' : 'Submit Registration'}
                </Button>
              )}
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
