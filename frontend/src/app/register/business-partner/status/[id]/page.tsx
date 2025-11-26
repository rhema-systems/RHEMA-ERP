'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { 
  CheckCircle2, 
  Clock, 
  XCircle, 
  FileText, 
  ArrowLeft,
  AlertCircle
} from 'lucide-react';
import { 
  businessPartnerRegistrationService, 
  type BusinessPartnerRegistrationDetailDto 
} from '@/services/businessPartnerRegistrationService';

const STATUS_CONFIG = {
  Draft: { color: 'bg-gray-100 text-gray-800', icon: FileText },
  Submitted: { color: 'bg-blue-100 text-blue-800', icon: Clock },
  UnderReview: { color: 'bg-yellow-100 text-yellow-800', icon: AlertCircle },
  Approved: { color: 'bg-green-100 text-green-800', icon: CheckCircle2 },
  Rejected: { color: 'bg-red-100 text-red-800', icon: XCircle },
};

export default function RegistrationStatusPage() {
  const params = useParams();
  const router = useRouter();
  const registrationId = params.id as string;
  const [registration, setRegistration] = useState<BusinessPartnerRegistrationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (registrationId) {
      loadRegistration();
    }
  }, [registrationId]);

  const loadRegistration = async () => {
    try {
      const data = await businessPartnerRegistrationService.getById(registrationId);
      setRegistration(data);
    } catch (error: any) {
      setError(error.message || 'Failed to load registration');
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading registration status...</p>
        </div>
      </div>
    );
  }

  if (error || !registration) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <Card className="max-w-md">
          <CardHeader>
            <CardTitle className="text-red-600">Error</CardTitle>
            <CardDescription>{error || 'Registration not found'}</CardDescription>
          </CardHeader>
          <CardContent>
            <Button onClick={() => router.push('/')} variant="outline">
              <ArrowLeft className="w-4 h-4 mr-2" />
              Go Back
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  const statusConfig = STATUS_CONFIG[registration.status as keyof typeof STATUS_CONFIG] || STATUS_CONFIG.Draft;
  const StatusIcon = statusConfig.icon;

  return (
    <div className="min-h-screen bg-gray-50 py-12">
      <div className="max-w-4xl mx-auto px-4">
        {/* Header */}
        <div className="mb-6">
          <Button variant="ghost" onClick={() => router.back()} className="mb-4">
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <h1 className="text-3xl font-bold text-gray-900">Registration Status</h1>
          <p className="text-gray-600 mt-2">Track your business partner registration application</p>
        </div>

        {/* Status Card */}
        <Card className="mb-6">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Application #{registration.applicationNumber}</CardTitle>
                <CardDescription>{registration.companyName}</CardDescription>
              </div>
              <Badge className={statusConfig.color}>
                <StatusIcon className="w-4 h-4 mr-1" />
                {registration.status}
              </Badge>
            </div>
          </CardHeader>

          <CardContent className="space-y-6">
            {/* Application Details */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <p className="text-sm text-gray-600">Partner Type</p>
                <p className="font-semibold">{registration.partnerType}</p>
              </div>
              <div>
                <p className="text-sm text-gray-600">Registration Number</p>
                <p className="font-semibold">{registration.registrationNumber || 'N/A'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-600">Email</p>
                <p className="font-semibold">{registration.email || 'N/A'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-600">Phone</p>
                <p className="font-semibold">{registration.phone || 'N/A'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-600">Submitted Date</p>
                <p className="font-semibold">
                  {registration.submittedDate 
                    ? new Date(registration.submittedDate).toLocaleDateString()
                    : 'Not submitted'
                  }
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-600">Completion</p>
                <p className="font-semibold">{registration.completionPercentage}%</p>
              </div>
            </div>

            {/* Review Information */}
            {registration.status === 'Approved' && registration.approvedDate && (
              <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                <h3 className="font-semibold text-green-900 mb-2">✓ Application Approved</h3>
                <p className="text-sm text-green-800">
                  Your application was approved on {new Date(registration.approvedDate).toLocaleDateString()}
                  {registration.approvedBy && ` by ${registration.approvedBy}`}
                </p>
              </div>
            )}

            {registration.status === 'Rejected' && registration.rejectionReason && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-4">
                <h3 className="font-semibold text-red-900 mb-2">✗ Application Rejected</h3>
                <p className="text-sm text-red-800">{registration.rejectionReason}</p>
              </div>
            )}

            {registration.reviewNotes && (
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <h3 className="font-semibold text-blue-900 mb-2">Review Notes</h3>
                <p className="text-sm text-blue-800">{registration.reviewNotes}</p>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Status History */}
        {registration.statusHistory && registration.statusHistory.length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle>Status History</CardTitle>
              <CardDescription>Timeline of your application status changes</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {registration.statusHistory.map((history, index) => (
                  <div key={history.id} className="flex items-start">
                    <div className="flex-shrink-0 w-2 h-2 bg-blue-600 rounded-full mt-2 mr-4"></div>
                    <div className="flex-1">
                      <div className="flex items-center justify-between">
                        <p className="font-semibold text-sm">
                          {history.fromStatus} → {history.toStatus}
                        </p>
                        <p className="text-xs text-gray-500">
                          {new Date(history.changedAt).toLocaleString()}
                        </p>
                      </div>
                      {history.notes && (
                        <p className="text-sm text-gray-600 mt-1">{history.notes}</p>
                      )}
                      {history.changedBy && (
                        <p className="text-xs text-gray-500 mt-1">Changed by: {history.changedBy}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}

