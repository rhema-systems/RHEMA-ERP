'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { format } from 'date-fns';
import {
  CheckCircle2,
  Clock,
  XCircle,
  FileText,
  ArrowLeft,
  AlertCircle,
  Download,
  Eye,
  Award
} from 'lucide-react';
import {
  businessPartnerRegistrationService,
  type BusinessPartnerRegistrationDetailDto
} from '@/services/businessPartnerRegistrationService';
import { licenseTypeService, type LicenseTypeDto } from '@/services/partnerConfigService';

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
  const registrationId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const [registration, setRegistration] = useState<BusinessPartnerRegistrationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [licenseTypes, setLicenseTypes] = useState<LicenseTypeDto[]>([]);
  const [registrationData, setRegistrationData] = useState<any>(null);

  useEffect(() => {
    if (registrationId) {
      loadRegistration();
      loadLicenseTypes();
    }
  }, [registrationId]);

  const loadRegistration = async () => {
    try {
      const data = await businessPartnerRegistrationService.getById(registrationId);
      setRegistration(data);

      // Parse registration data JSON if available
      if (data.registrationData) {
        try {
          // First parse - gets the outer object
          const firstParse = JSON.parse(data.registrationData);

          // Check if there's a nested RegistrationData property that needs second parse
          let finalData = firstParse;
          if (firstParse.RegistrationData && typeof firstParse.RegistrationData === 'string') {
            // Second parse - gets the actual form data
            finalData = JSON.parse(firstParse.RegistrationData);
          }

          setRegistrationData(finalData);
        } catch (e) {
          console.error('Failed to parse registration data:', e);
        }
      }
    } catch (error: any) {
      setError(error.message || 'Failed to load registration');
    } finally {
      setLoading(false);
    }
  };

  const loadLicenseTypes = async () => {
    try {
      const data = await licenseTypeService.getActive();
      setLicenseTypes(data);
    } catch (error) {
      console.error('Error loading license types:', error);
    }
  };

  const getLicenseTypeName = (licenseTypeId: string): string => {
    const licenseType = licenseTypes.find((lt) => lt.id === licenseTypeId);
    return licenseType?.licenseName || 'Unknown License Type';
  };

  // Format date as "MMM dd, yyyy HH:mm AM/PM"
  const formatDateTime = (dateString: string | undefined) => {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: '2-digit',
      year: 'numeric'
    }) + ' ' + date.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    });
  };

  // Calculate approval progress based on status and review activity
  const getApprovalProgress = (status: string, documents?: any[]) => {
    // Check if any documents have been reviewed (verified or rejected)
    const hasReviewActivity = documents?.some(doc => doc.isVerified || doc.isRejected) || false;

    switch (status) {
      case 'Draft':
        return { percentage: 0, label: 'Draft - Not Submitted', color: 'bg-gray-600' };
      case 'Submitted':
        // If documents have been reviewed, show as "Under Review" instead
        if (hasReviewActivity) {
          return { percentage: 50, label: 'Under Review', color: 'bg-yellow-600' };
        }
        return { percentage: 25, label: 'Submitted - Awaiting Review', color: 'bg-blue-600' };
      case 'UnderReview':
        return { percentage: 50, label: 'Under Review', color: 'bg-yellow-600' };
      case 'MoreInfoRequired':
        return { percentage: 40, label: 'More Information Required', color: 'bg-orange-600' };
      case 'Approved':
        return { percentage: 100, label: 'Approved', color: 'bg-green-600' };
      case 'Rejected':
        return { percentage: 100, label: 'Rejected', color: 'bg-red-600' };
      case 'Cancelled':
        return { percentage: 0, label: 'Cancelled', color: 'bg-gray-600' };
      default:
        return { percentage: 0, label: status, color: 'bg-gray-600' };
    }
  };

  const approvalProgress = registration ? getApprovalProgress(registration.status, registration.documents) : { percentage: 0, label: 'Unknown', color: 'bg-gray-600' };

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
      <div className="max-w-7xl mx-auto px-4">
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
                    ? formatDateTime(registration.submittedDate)
                    : 'Not submitted'
                  }
                </p>
              </div>
              <div>
                <p className="text-sm text-gray-600">Form Completion</p>
                <p className="font-semibold">{registration.completionPercentage}%</p>
              </div>
            </div>

            {/* Approval Progress Indicator */}
            <div>
              <div className="flex justify-between items-center mb-2">
                <p className="text-sm font-medium text-gray-700">Approval Progress</p>
                <p className="text-sm font-semibold text-gray-900">{approvalProgress.label}</p>
              </div>
              <div className="w-full bg-gray-200 rounded-full h-3">
                <div
                  className={`h-3 rounded-full transition-all duration-500 ${approvalProgress.color}`}
                  style={{ width: `${approvalProgress.percentage}%` }}
                />
              </div>
              <div className="flex justify-between mt-1 text-xs text-gray-500">
                <span>Draft</span>
                <span>Submitted</span>
                <span>Under Review</span>
                <span>Completed</span>
              </div>
            </div>

            {/* Review Information */}
            {registration.status === 'Approved' && registration.approvedDate && (
              <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                <h3 className="font-semibold text-green-900 mb-2">✓ Application Approved</h3>
                <p className="text-sm text-green-800">
                  Your application was approved on {formatDateTime(registration.approvedDate)}
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

            {registration.status === 'MoreInfoRequired' && (
              <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                <div className="flex items-start space-x-3">
                  <AlertCircle className="w-5 h-5 text-yellow-600 mt-0.5" />
                  <div className="flex-1">
                    <h3 className="font-semibold text-yellow-900 mb-2">Action Required - More Information Needed</h3>
                    {registration.reviewNotes && (
                      <div className="mb-3">
                        <p className="text-sm font-semibold text-yellow-900 mb-1">Reviewer Comments:</p>
                        <p className="text-sm text-yellow-800">{registration.reviewNotes}</p>
                      </div>
                    )}
                    <p className="text-sm text-yellow-800 mb-3">
                      Please review the feedback below and update your registration accordingly.
                      Check for rejected documents and any comments from the reviewer.
                    </p>
                    <Button
                      size="sm"
                      onClick={() => router.push(`/register/business-partner?id=${registrationId}`)}
                      className="bg-yellow-600 hover:bg-yellow-700"
                    >
                      Update Registration
                    </Button>
                  </div>
                </div>
              </div>
            )}

            {registration.reviewNotes && registration.status !== 'MoreInfoRequired' && (
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <h3 className="font-semibold text-blue-900 mb-2">Review Notes</h3>
                <p className="text-sm text-blue-800">{registration.reviewNotes}</p>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Documents and Licenses Section - Side by Side */}
        {((registration.documents && registration.documents.length > 0) ||
          (registrationData?.licenses && registrationData.licenses.length > 0 &&
           (registration.partnerType === 'Contractor' || registration.partnerType === 'Both'))) && (
          <div className={`grid grid-cols-1 gap-6 mb-6 ${
            // Only use 2 columns if both documents and licenses exist
            registration.documents && registration.documents.length > 0 &&
            registrationData?.licenses && registrationData.licenses.length > 0 &&
            (registration.partnerType === 'Contractor' || registration.partnerType === 'Both')
              ? 'lg:grid-cols-2'
              : ''
          }`}>
            {/* Documents Section */}
            {registration.documents && registration.documents.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <FileText className="w-5 h-5" />
                    Submitted Documents
                  </CardTitle>
                  <CardDescription>Review the status of your uploaded documents</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3 max-h-[500px] overflow-y-auto pr-2">
                    {registration.documents
                      .filter(doc => {
                        // Hide rejected documents if registration is approved
                        if (registration.status === 'Approved' && doc.isRejected) {
                          return false;
                        }
                        return true;
                      })
                      .map((doc) => (
                      <div
                        key={doc.id}
                        className={`p-3 border rounded-lg ${
                          doc.isRejected
                            ? 'bg-red-50 border-red-200'
                            : doc.isVerified
                            ? 'bg-green-50 border-green-200'
                            : 'bg-gray-50 border-gray-200'
                        }`}
                      >
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <div className="flex items-center space-x-2 mb-1">
                              <FileText className="w-4 h-4 text-gray-600" />
                              <span className="font-semibold text-sm">{doc.documentType}</span>
                            </div>
                            <p className="text-xs text-gray-600 mb-2 truncate" title={doc.documentName}>
                              {doc.documentName}
                            </p>

                            {/* Document Status */}
                            <div className="flex items-center space-x-2">
                              {doc.isVerified && (
                                <Badge className="bg-green-600 text-white text-xs">
                                  <CheckCircle2 className="w-3 h-3 mr-1" />
                                  Verified
                                </Badge>
                              )}
                              {doc.isRejected && (
                                <Badge variant="destructive" className="text-xs">
                                  <XCircle className="w-3 h-3 mr-1" />
                                  Rejected
                                </Badge>
                              )}
                              {!doc.isVerified && !doc.isRejected && (
                                <Badge variant="outline" className="text-xs">
                                  <Clock className="w-3 h-3 mr-1" />
                                  Pending
                                </Badge>
                              )}
                            </div>

                            {/* Rejection Reason */}
                            {doc.isRejected && doc.rejectionReason && (
                              <div className="mt-2 p-2 bg-red-100 border border-red-300 rounded">
                                <p className="text-xs font-semibold text-red-900 mb-1">Reason:</p>
                                <p className="text-xs text-red-800">{doc.rejectionReason}</p>
                                {doc.rejectedDate && (
                                  <p className="text-xs text-red-700 mt-1">
                                    {formatDateTime(doc.rejectedDate)}
                                  </p>
                                )}
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Licenses Section - Only for Contractors */}
            {registrationData?.licenses && registrationData.licenses.length > 0 &&
             (registration.partnerType === 'Contractor' || registration.partnerType === 'Both') && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <Award className="w-5 h-5" />
                    Licenses & Certifications
                  </CardTitle>
                  <CardDescription>Professional licenses submitted with your application</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3 max-h-[500px] overflow-y-auto pr-2">
                    {registrationData.licenses.map((license: any, index: number) => (
                      <div key={index} className="border-l-4 border-blue-500 pl-3 py-2 bg-blue-50 rounded-r-lg">
                        <div className="flex items-start gap-2">
                          <Award className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
                          <div className="flex-1">
                            <p className="font-semibold text-sm text-blue-900">
                              {getLicenseTypeName(license.licenseTypeId)}
                            </p>
                            <div className="grid grid-cols-1 gap-1 mt-2 text-xs text-gray-700">
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
                                  {format(new Date(license.expiryDate), 'MMM dd, yyyy')}
                                </div>
                              )}
                              <div>
                                <span className="font-medium">Authority:</span>{' '}
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
          </div>
        )}

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
                          {formatDateTime(history.changedAt)}
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

