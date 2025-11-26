'use client';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { useState } from 'react';
import { 
  Building2, 
  Mail, 
  Phone, 
  MapPin, 
  FileText, 
  Award,
  CheckCircle2,
  AlertCircle
} from 'lucide-react';
import { type RegistrationFormData } from '@/services/businessPartnerRegistrationService';

interface RegistrationSummaryProps {
  formData: RegistrationFormData;
  onSubmit: () => void;
  loading: boolean;
}

export default function RegistrationSummary({ formData, onSubmit, loading }: RegistrationSummaryProps) {
  const [agreedToTerms, setAgreedToTerms] = useState(false);
  const [agreedToAccuracy, setAgreedToAccuracy] = useState(false);

  const canSubmit = agreedToTerms && agreedToAccuracy && !loading;

  return (
    <div className="space-y-6">
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <h3 className="font-semibold text-blue-900 mb-2">Review Your Information</h3>
        <p className="text-sm text-blue-800">
          Please review all the information you've provided before submitting your registration.
          You can go back to previous steps to make any changes.
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
              <p className="font-semibold">{formData.companyName || 'Not provided'}</p>
            </div>
            <div>
              <p className="text-gray-600">Partner Type</p>
              <Badge variant="outline">{formData.partnerType}</Badge>
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
                <p className="font-semibold">{formData.yearsInBusiness} years</p>
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
                  {formData.contactPersonTitle && ` - ${formData.contactPersonTitle}`}
                </p>
                {formData.contactPersonEmail && (
                  <p className="text-sm text-gray-600">{formData.contactPersonEmail}</p>
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
            <div className="space-y-2">
              {formData.licenses.map((license, index) => (
                <div key={index} className="flex items-center gap-2 text-sm">
                  <CheckCircle2 className="w-4 h-4 text-green-600" />
                  <span className="font-medium">License #{license.licenseNumber}</span>
                  <span className="text-gray-500">
                    - Issued: {new Date(license.issueDate).toLocaleDateString()}
                  </span>
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
              onCheckedChange={(checked) => setAgreedToTerms(checked as boolean)}
            />
            <Label htmlFor="terms" className="text-sm cursor-pointer">
              I agree to the terms and conditions of the business partner registration process.
              I understand that my application will be reviewed and I will be notified of the outcome.
            </Label>
          </div>

          <div className="flex items-start gap-3">
            <Checkbox
              id="accuracy"
              checked={agreedToAccuracy}
              onCheckedChange={(checked) => setAgreedToAccuracy(checked as boolean)}
            />
            <Label htmlFor="accuracy" className="text-sm cursor-pointer">
              I certify that all information provided in this registration is accurate and complete
              to the best of my knowledge. I understand that providing false information may result
              in rejection or termination of business partnership.
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
              Please review and accept both declarations above to submit your registration.
            </p>
          </div>
        </div>
      )}

      {/* Submit Button */}
      <div className="flex justify-center pt-4">
        <Button
          onClick={onSubmit}
          disabled={!canSubmit}
          size="lg"
          className="min-w-[200px]"
        >
          {loading ? 'Submitting...' : 'Submit Registration'}
        </Button>
      </div>
    </div>
  );
}

