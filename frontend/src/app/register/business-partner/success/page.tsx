'use client';

import { useEffect, useState } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CheckCircle2, FileText, Home, Search } from 'lucide-react';
import { businessPartnerRegistrationService, type BusinessPartnerRegistrationDetailDto } from '@/services/businessPartnerRegistrationService';

export default function RegistrationSuccessPage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const registrationId = searchParams.get('id');
  const [registration, setRegistration] = useState<BusinessPartnerRegistrationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (registrationId) {
      loadRegistration();
    }
  }, [registrationId]);

  const loadRegistration = async () => {
    try {
      const data = await businessPartnerRegistrationService.getById(registrationId!);
      setRegistration(data);
    } catch (error) {
      console.error('Error loading registration:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 py-12">
      <div className="max-w-3xl mx-auto px-4">
        <Card>
          <CardHeader className="text-center">
            <div className="mx-auto w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mb-4">
              <CheckCircle2 className="w-10 h-10 text-green-600" />
            </div>
            <CardTitle className="text-2xl">Registration Submitted Successfully!</CardTitle>
            <CardDescription>
              Your business partner registration has been submitted for review
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-6">
            {/* Application Details */}
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <h3 className="font-semibold text-blue-900 mb-3">Application Details</h3>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-gray-600">Application Number</p>
                  <p className="font-semibold text-gray-900">{registration?.applicationNumber || 'N/A'}</p>
                </div>
                <div>
                  <p className="text-gray-600">Company Name</p>
                  <p className="font-semibold text-gray-900">{registration?.companyName || 'N/A'}</p>
                </div>
                <div>
                  <p className="text-gray-600">Partner Type</p>
                  <Badge variant="outline">{registration?.partnerType || 'N/A'}</Badge>
                </div>
                <div>
                  <p className="text-gray-600">Status</p>
                  <Badge className="bg-yellow-100 text-yellow-800 hover:bg-yellow-100">
                    {registration?.status || 'Submitted'}
                  </Badge>
                </div>
                <div>
                  <p className="text-gray-600">Submitted Date</p>
                  <p className="font-semibold text-gray-900">
                    {registration?.submittedDate 
                      ? new Date(registration.submittedDate).toLocaleDateString()
                      : new Date().toLocaleDateString()
                    }
                  </p>
                </div>
                <div>
                  <p className="text-gray-600">Completion</p>
                  <p className="font-semibold text-gray-900">{registration?.completionPercentage || 0}%</p>
                </div>
              </div>
            </div>

            {/* Next Steps */}
            <div>
              <h3 className="font-semibold text-gray-900 mb-3">What Happens Next?</h3>
              <ol className="space-y-3 text-sm text-gray-600">
                <li className="flex items-start">
                  <span className="flex-shrink-0 w-6 h-6 bg-blue-100 text-blue-600 rounded-full flex items-center justify-center mr-3 mt-0.5 text-xs font-semibold">
                    1
                  </span>
                  <span>Our team will review your application within 3-5 business days</span>
                </li>
                <li className="flex items-start">
                  <span className="flex-shrink-0 w-6 h-6 bg-blue-100 text-blue-600 rounded-full flex items-center justify-center mr-3 mt-0.5 text-xs font-semibold">
                    2
                  </span>
                  <span>You will receive an email notification once the review is complete</span>
                </li>
                <li className="flex items-start">
                  <span className="flex-shrink-0 w-6 h-6 bg-blue-100 text-blue-600 rounded-full flex items-center justify-center mr-3 mt-0.5 text-xs font-semibold">
                    3
                  </span>
                  <span>If approved, you will be added to our business partner database</span>
                </li>
                <li className="flex items-start">
                  <span className="flex-shrink-0 w-6 h-6 bg-blue-100 text-blue-600 rounded-full flex items-center justify-center mr-3 mt-0.5 text-xs font-semibold">
                    4
                  </span>
                  <span>You can track your application status using your application number</span>
                </li>
              </ol>
            </div>

            {/* Important Information */}
            <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
              <h3 className="font-semibold text-yellow-900 mb-2">Important Information</h3>
              <ul className="space-y-1 text-sm text-yellow-800">
                <li>• Please save your application number for future reference</li>
                <li>• Check your email regularly for updates on your application</li>
                <li>• You can check your application status at any time using the link below</li>
              </ul>
            </div>

            {/* Action Buttons */}
            <div className="flex flex-col sm:flex-row gap-3 pt-4">
              <Button
                variant="outline"
                className="flex-1"
                onClick={() => router.push(`/register/business-partner/status/${registrationId}`)}
              >
                <Search className="w-4 h-4 mr-2" />
                Check Status
              </Button>
              <Button
                variant="outline"
                className="flex-1"
                onClick={() => window.print()}
              >
                <FileText className="w-4 h-4 mr-2" />
                Print Confirmation
              </Button>
              <Button
                className="flex-1"
                onClick={() => router.push('/')}
              >
                <Home className="w-4 h-4 mr-2" />
                Go to Home
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

