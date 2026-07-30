'use client';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
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
import { useState, useEffect } from 'react';
import { format } from 'date-fns';
import {
  Building2,
  Mail,
  Phone,
  MapPin,
  FileText,
  Award,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  Send,
  CheckCircle,
} from 'lucide-react';
import { type RegistrationFormData } from '@/services/businessPartnerRegistrationService';
import {
  licenseTypeService,
  type LicenseTypeDto,
} from '@/services/partnerConfigService';
import { settingsService } from '@/services/settings';

interface RegistrationSummaryProps {
  formData: RegistrationFormData;
  onSubmit: () => void;
  loading: boolean;
}

export default function RegistrationSummary({
  formData,
  onSubmit,
  loading,
}: RegistrationSummaryProps) {
  const [agreedToTerms, setAgreedToTerms] = useState(false);
  const [agreedToAccuracy, setAgreedToAccuracy] = useState(false);
  const [licenseTypes, setLicenseTypes] = useState<LicenseTypeDto[]>([]);
  const [termsOfServiceUrl, setTermsOfServiceUrl] = useState<string | null>(
    null
  );
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);

  const canSubmit = agreedToTerms && agreedToAccuracy && !loading;

  const handleSubmitClick = () => {
    if (!canSubmit) return;
    setShowConfirmDialog(true);
  };

  const handleConfirmSubmit = () => {
    setShowConfirmDialog(false);
    onSubmit();
  };

  useEffect(() => {
    // Load license types
    const loadLicenseTypes = async () => {
      try {
        const data = await licenseTypeService.getActive();
        setLicenseTypes(data);
      } catch (error) {
        console.error('Error loading license types:', error);
      }
    };

    // Load security settings for Terms of Service URL
    const loadSecuritySettings = async () => {
      try {
        const settings = await settingsService.getPublicSecuritySettings();
        setTermsOfServiceUrl(settings.termsOfServiceUrl || null);
      } catch (error) {
        console.error('Error loading security settings:', error);
      }
    };

    loadLicenseTypes();
    loadSecuritySettings();
  }, []);

  const getLicenseTypeName = (licenseTypeId: string): string => {
    const licenseType = licenseTypes.find((lt) => lt.id === licenseTypeId);
    return licenseType?.licenseName || 'Unknown License Type';
  };

  return (
    <div className="space-y-6">
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <h3 className="font-semibold text-blue-900 mb-2">
          Review Your Information
        </h3>
        <p className="text-sm text-blue-800">
          Please review all the information you've provided before submitting
          your registration. You can go back to previous steps to make any
          changes.
        </p>
      </div>

      {/* Company Information */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Building2 className="w-5 h-5" />
            Company Information
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <div>
              <p className="text-gray-600">Company Name</p>
              <p className="font-semibold">
                {formData.companyName || 'Not provided'}
              </p>
            </div>
            <div>
              <p className="text-gray-600">Partner Type</p>
              <Badge variant="outline">{formData.partnerType}</Badge>
            </div>
            <div>
              <p className="text-gray-600">Registration Category</p>
              <Badge variant="outline">
                {formData.registrationCategory || 'Not selected'}
              </Badge>
            </div>
            {formData.tradingName && (
              <div>
                <p className="text-gray-600">Trading Name</p>
                <p className="font-semibold">{formData.tradingName}</p>
              </div>
            )}
            {formData.registrationNumber && (
              <div>
                <p className="text-gray-600">Registration Number</p>
                <p className="font-semibold">{formData.registrationNumber}</p>
              </div>
            )}
            {formData.taxNumber && (
              <div>
                <p className="text-gray-600">Tax Number</p>
                <p className="font-semibold">{formData.taxNumber}</p>
              </div>
            )}
            {formData.industryType && (
              <div>
                <p className="text-gray-600">Industry Type</p>
                <p className="font-semibold">{formData.industryType}</p>
              </div>
            )}
            {formData.yearsInBusiness && (
              <div>
                <p className="text-gray-600">Years in Business</p>
                <p className="font-semibold">
                  {formData.yearsInBusiness} years
                </p>
              </div>
            )}
            {formData.numberOfEmployees && (
              <div>
                <p className="text-gray-600">Number of Employees</p>
                <p className="font-semibold">{formData.numberOfEmployees}</p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Contact Information */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Mail className="w-5 h-5" />
            Contact Information
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <div className="flex items-center gap-2">
              <Mail className="w-4 h-4 text-gray-500" />
              <div>
                <p className="text-gray-600">Email</p>
                <p className="font-semibold">{formData.email}</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Phone className="w-4 h-4 text-gray-500" />
              <div>
                <p className="text-gray-600">Phone</p>
                <p className="font-semibold">{formData.phone}</p>
              </div>
            </div>
            {formData.physicalAddress && (
              <div className="flex items-start gap-2 md:col-span-2">
                <MapPin className="w-4 h-4 text-gray-500 mt-1" />
                <div>
                  <p className="text-gray-600">Physical Address</p>
                  <p className="font-semibold">
                    {formData.physicalAddress}
                    {formData.city && `, ${formData.city}`}
                    {formData.country && `, ${formData.country}`}
                  </p>
                </div>
              </div>
            )}
            {formData.contactPersonName && (
              <div className="md:col-span-2">
                <p className="text-gray-600">Primary Contact Person</p>
                <p className="font-semibold">
                  {formData.contactPersonName}
                  {formData.contactPersonTitle &&
                    ` - ${formData.contactPersonTitle}`}
                </p>
                {formData.contactPersonEmail && (
                  <p className="text-sm text-gray-600">
                    {formData.contactPersonEmail}
                  </p>
                )}
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Documents */}
      {formData.documents && formData.documents.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileText className="w-5 h-5" />
              Documents ({formData.documents.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {formData.documents.map((doc, index) => (
                <div key={index} className="flex items-center gap-2 text-sm">
                  <CheckCircle2 className="w-4 h-4 text-green-600" />
                  <span className="font-medium">{doc.documentType}</span>
                  <span className="text-gray-500">- {doc.documentName}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Licenses */}
      {formData.licenses && formData.licenses.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Award className="w-5 h-5" />
              Licenses ({formData.licenses.length})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {formData.licenses.map((license, index) => (
                <div
                  key={index}
                  className="border-l-4 border-green-500 pl-4 py-2"
                >
                  <div className="flex items-start gap-2">
                    <Award className="w-5 h-5 text-green-600 mt-0.5" />
                    <div className="flex-1">
                      <p className="font-semibold text-sm">
                        {getLicenseTypeName(license.licenseTypeId)}
                      </p>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-2 mt-2 text-sm text-gray-600">
                        <div>
                          <span className="font-medium">License #:</span>{' '}
                          {license.licenseNumber}
                        </div>
                        <div>
                          <span className="font-medium">Issued:</span>{' '}
                          {format(new Date(license.issueDate), 'MMM dd, yyyy')}
                        </div>
                        {license.expiryDate && (
                          <div>
                            <span className="font-medium">Expires:</span>{' '}
                            {format(
                              new Date(license.expiryDate),
                              'MMM dd, yyyy'
                            )}
                          </div>
                        )}
                        <div
                          className={license.expiryDate ? '' : 'md:col-span-2'}
                        >
                          <span className="font-medium">
                            Issuing Authority:
                          </span>{' '}
                          {license.issuingAuthority}
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Terms and Conditions */}
      <Card className="border-2 border-blue-200">
        <CardContent className="pt-6 space-y-4">
          <div className="flex items-start gap-3">
            <Checkbox
              id="terms"
              checked={agreedToTerms}
              onCheckedChange={(checked) =>
                setAgreedToTerms(checked as boolean)
              }
            />
            <Label
              htmlFor="terms"
              className="text-sm cursor-pointer leading-relaxed"
            >
              I agree to the{' '}
              {termsOfServiceUrl ? (
                <a
                  href={termsOfServiceUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-blue-600 hover:underline inline-flex items-center gap-1"
                  onClick={(e) => e.stopPropagation()}
                >
                  terms and conditions
                  <ExternalLink className="w-3 h-3" />
                </a>
              ) : (
                <span className="font-medium">terms and conditions</span>
              )}{' '}
              of the business partner registration process. I understand that my
              application will be reviewed and I will be notified of the
              outcome.
            </Label>
          </div>

          <div className="flex items-start gap-3">
            <Checkbox
              id="accuracy"
              checked={agreedToAccuracy}
              onCheckedChange={(checked) =>
                setAgreedToAccuracy(checked as boolean)
              }
            />
            <Label
              htmlFor="accuracy"
              className="text-sm cursor-pointer leading-relaxed"
            >
              I certify that all information provided in this registration is
              accurate and complete to the best of my knowledge. I understand
              that providing false information may result in rejection or
              termination of business partnership.
            </Label>
          </div>
        </CardContent>
      </Card>

      {/* Warning if not agreed */}
      {(!agreedToTerms || !agreedToAccuracy) && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4 flex items-start gap-3">
          <AlertCircle className="w-5 h-5 text-yellow-600 flex-shrink-0 mt-0.5" />
          <div>
            <h3 className="font-semibold text-yellow-900">Action Required</h3>
            <p className="text-sm text-yellow-800">
              Please review and accept both declarations above to submit your
              registration.
            </p>
          </div>
        </div>
      )}

      {/* Submit Button */}
      <div className="flex justify-center pt-4">
        <Button
          onClick={handleSubmitClick}
          disabled={!canSubmit}
          size="lg"
          className="min-w-[200px]"
        >
          <Send className="w-4 h-4 mr-2" />
          Submit Registration
        </Button>
      </div>

      {/* Confirmation Dialog */}
      <AlertDialog open={showConfirmDialog} onOpenChange={setShowConfirmDialog}>
        <AlertDialogContent className="sm:max-w-[700px]">
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2 text-xl">
              <CheckCircle className="w-6 h-6 text-blue-600" />
              Submit Business Partner Registration
            </AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-4 text-left pt-2">
                <p className="text-base text-gray-700">
                  You are about to submit your business partner registration for
                  review. Please confirm that:
                </p>
                <ul className="space-y-2 text-sm text-gray-600">
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-4 h-4 text-green-600 mt-0.5 flex-shrink-0" />
                    <span>
                      All information provided is accurate and complete
                    </span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-4 h-4 text-green-600 mt-0.5 flex-shrink-0" />
                    <span>All required documents have been uploaded</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <CheckCircle2 className="w-4 h-4 text-green-600 mt-0.5 flex-shrink-0" />
                    <span>
                      You have reviewed and accepted the terms and conditions
                    </span>
                  </li>
                </ul>
                <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 mt-4">
                  <p className="text-sm text-blue-900">
                    <strong>What happens next?</strong>
                  </p>
                  <p className="text-sm text-blue-800 mt-1">
                    Once submitted, your application will be reviewed by our
                    team. You will be notified of the outcome via email and
                    in-app notifications.
                  </p>
                </div>
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={loading}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmSubmit}
              disabled={loading}
              className="bg-blue-600 hover:bg-blue-700"
            >
              {loading ? (
                <>
                  <span className="animate-spin mr-2">⏳</span>
                  Submitting...
                </>
              ) : (
                <>
                  <Send className="w-4 h-4 mr-2" />
                  Yes, Submit Registration
                </>
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
