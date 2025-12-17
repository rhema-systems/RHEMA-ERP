'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { format } from 'date-fns';
import {
  ArrowLeft,
  CheckCircle,
  XCircle,
  AlertCircle,
  Building2,
  Mail,
  Phone,
  MapPin,
  FileText,
  Award,
  Clock,
  User,
  Download,
  Eye,
  X
} from 'lucide-react';
import { toast } from 'sonner';
import { registrationReviewService, type RegistrationDetailDto } from '@/services/registrationReviewService';
import { businessPartnerRegistrationService } from '@/services/businessPartnerRegistrationService';
import { licenseTypeService, type LicenseTypeDto } from '@/services/partnerConfigService';

export default function RegistrationDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params.id as string;

  const [registration, setRegistration] = useState<RegistrationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('details');
  const [licenseTypes, setLicenseTypes] = useState<LicenseTypeDto[]>([]);
  const [parsedLicenses, setParsedLicenses] = useState<any[]>([]);

  // Document dialog states
  const [verifyDocDialogOpen, setVerifyDocDialogOpen] = useState(false);
  const [rejectDocDialogOpen, setRejectDocDialogOpen] = useState(false);
  const [selectedDocument, setSelectedDocument] = useState<{ id: string; name: string } | null>(null);
  const [docRejectionReason, setDocRejectionReason] = useState('');

  // Dialog states
  const [approveDialogOpen, setApproveDialogOpen] = useState(false);
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [moreInfoDialogOpen, setMoreInfoDialogOpen] = useState(false);

  const [approvalNotes, setApprovalNotes] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [moreInfoNotes, setMoreInfoNotes] = useState('');

  useEffect(() => {
    loadRegistration();
    loadLicenseTypes();
  }, [id]);

  const loadRegistration = async () => {
    try {
      setLoading(true);
      const data = await registrationReviewService.getById(id);
      setRegistration(data);

      // Parse licenses from registrationData if available
      if ((data as any).registrationData) {
        try {
          // First parse - gets the outer object
          const firstParse = JSON.parse((data as any).registrationData);

          // Check if there's a nested RegistrationData property that needs second parse
          let finalData = firstParse;
          if (firstParse.RegistrationData && typeof firstParse.RegistrationData === 'string') {
            // Second parse - gets the actual form data
            finalData = JSON.parse(firstParse.RegistrationData);
          }

          // Set parsed licenses
          if (finalData.licenses && Array.isArray(finalData.licenses)) {
            setParsedLicenses(finalData.licenses);
          }
        } catch (e) {
          console.error('Failed to parse registration data:', e);
        }
      }
    } catch (error) {
      console.error('Error loading registration:', error);
      toast.error('Failed to load registration details');
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

  const validateDocumentsBeforeApproval = (): boolean => {
    // Check if there are uploaded documents
    if (registration?.documents && registration.documents.length > 0) {
      // Check for unverified documents (not verified and not rejected)
      const unverifiedDocs = registration.documents.filter(
        (doc) => !doc.isVerified && !doc.isRejected
      );

      if (unverifiedDocs.length > 0) {
        const docNames = unverifiedDocs.map((doc) => doc.documentName).join(', ');
        toast.error(
          `Cannot approve registration. Please verify or reject all documents first.\n\nUnverified documents: ${docNames}`,
          { duration: 8000 }
        );
        return false;
      }

      // Check for rejected documents
      const rejectedDocs = registration.documents.filter((doc) => doc.isRejected);

      if (rejectedDocs.length > 0) {
        const docNames = rejectedDocs.map((doc) => doc.documentName).join(', ');
        toast.error(
          `Cannot approve registration. The following documents have been rejected: ${docNames}.\n\nPlease request the applicant to re-upload these documents.`,
          { duration: 8000 }
        );
        return false;
      }
    }
    return true;
  };

  const handleApproveClick = () => {
    if (validateDocumentsBeforeApproval()) {
      setApproveDialogOpen(true);
    }
  };

  const handleApprove = async () => {
    try {
      setActionLoading(true);
      await registrationReviewService.approve(id, approvalNotes || undefined);
      toast.success('Registration approved successfully');
      setApproveDialogOpen(false);
      router.push('/administration/procurement/registrations');
    } catch (error: any) {
      console.error('Error approving registration:', error);
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to approve registration';
      toast.error(errorMessage, { duration: 6000 });
    } finally {
      setActionLoading(false);
    }
  };

  const handleReject = async () => {
    if (!rejectionReason.trim()) {
      toast.error('Please provide a reason for rejection');
      return;
    }

    try {
      setActionLoading(true);
      await registrationReviewService.reject(id, rejectionReason);
      toast.success('Registration rejected');
      setRejectDialogOpen(false);
      router.push('/administration/procurement/registrations');
    } catch (error) {
      console.error('Error rejecting registration:', error);
      toast.error('Failed to reject registration');
    } finally {
      setActionLoading(false);
    }
  };

  const handleRequestMoreInfo = async () => {
    if (!moreInfoNotes.trim()) {
      toast.error('Please provide details about what information is needed');
      return;
    }

    try {
      setActionLoading(true);
      await registrationReviewService.requestMoreInfo(id, moreInfoNotes);
      toast.success('More information requested');
      setMoreInfoDialogOpen(false);
      loadRegistration();
    } catch (error) {
      console.error('Error requesting more info:', error);
      toast.error('Failed to request more information');
    } finally {
      setActionLoading(false);
    }
  };

  const openVerifyDocDialog = (documentId: string, documentName: string) => {
    setSelectedDocument({ id: documentId, name: documentName });
    setVerifyDocDialogOpen(true);
  };

  const openRejectDocDialog = (documentId: string, documentName: string) => {
    setSelectedDocument({ id: documentId, name: documentName });
    setDocRejectionReason('');
    setRejectDocDialogOpen(true);
  };

  const handleVerifyDocument = async () => {
    if (!selectedDocument) return;

    try {
      await registrationReviewService.verifyDocument(id, selectedDocument.id);
      toast.success('Document verified successfully');
      setVerifyDocDialogOpen(false);
      setSelectedDocument(null);
      loadRegistration();
    } catch (error) {
      console.error('Error verifying document:', error);
      toast.error('Failed to verify document');
    }
  };

  const handleRejectDocument = async () => {
    if (!selectedDocument) return;

    if (!docRejectionReason || docRejectionReason.trim() === '') {
      toast.error('Rejection reason is required');
      return;
    }

    try {
      await registrationReviewService.rejectDocument(id, selectedDocument.id, docRejectionReason);
      toast.success('Document rejected');
      setRejectDocDialogOpen(false);
      setSelectedDocument(null);
      setDocRejectionReason('');
      loadRegistration();
    } catch (error) {
      console.error('Error rejecting document:', error);
      toast.error('Failed to reject document');
    }
  };

  const handleRevertDocumentRejection = async (documentId: string, documentName: string) => {
    try {
      await registrationReviewService.revertDocumentRejection(id, documentId);
      toast.success(`Rejection reverted for ${documentName}`);
      loadRegistration();
    } catch (error) {
      console.error('Error reverting document rejection:', error);
      toast.error('Failed to revert document rejection');
    }
  };

  const handleDownloadDocument = async (documentId: string, documentName: string) => {
    try {
      await registrationReviewService.downloadDocument(id, documentId, documentName);
      toast.success('Document downloaded');
    } catch (error) {
      console.error('Error downloading document:', error);
      toast.error('Failed to download document');
    }
  };

  const handleViewDocument = async (documentId: string, documentName: string) => {
    try {
      await registrationReviewService.viewDocument(id, documentId, documentName);
    } catch (error) {
      console.error('Error viewing document:', error);
      toast.error('Failed to open document');
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { label: string; variant: 'default' | 'secondary' | 'destructive' | 'outline' }> = {
      Draft: { label: 'Draft', variant: 'secondary' },
      Submitted: { label: 'Submitted', variant: 'default' },
      UnderReview: { label: 'Under Review', variant: 'outline' },
      Approved: { label: 'Approved', variant: 'default' },
      Rejected: { label: 'Rejected', variant: 'destructive' },
      MoreInfoRequired: { label: 'More Info Required', variant: 'outline' },
    };

    const config = statusConfig[status] || { label: status, variant: 'secondary' };
    return <Badge variant={config.variant}>{config.label}</Badge>;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
          <p>Loading registration details...</p>
        </div>
      </div>
    );
  }

  if (!registration) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 text-red-600 mx-auto mb-4" />
          <p>Registration not found</p>
          <Button onClick={() => router.back()} className="mt-4">
            Go Back
          </Button>
        </div>
      </div>
    );
  }

  const canApprove = registration.status === 'Submitted' || registration.status === 'UnderReview';
  const canReject = registration.status === 'Submitted' || registration.status === 'UnderReview';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.back()}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{registration.companyName}</h1>
            <div className="text-gray-600 mt-1">
              Application #{registration.applicationNumber} • {getStatusBadge(registration.status)}
            </div>
          </div>
        </div>

        <div className="flex gap-2">
          {canApprove && (
            <Button onClick={handleApproveClick} className="bg-green-600 hover:bg-green-700">
              <CheckCircle className="w-4 h-4 mr-2" />
              Approve
            </Button>
          )}
          {canReject && (
            <Button onClick={() => setRejectDialogOpen(true)} variant="destructive">
              <XCircle className="w-4 h-4 mr-2" />
              Reject
            </Button>
          )}
          {(registration.status === 'Submitted' || registration.status === 'UnderReview') && (
            <Button onClick={() => setMoreInfoDialogOpen(true)} variant="outline" className="border-yellow-600 text-yellow-600 hover:bg-yellow-50">
              <AlertCircle className="w-4 h-4 mr-2" />
              Request More Info
            </Button>
          )}
        </div>
      </div>

      {/* Content Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
        <TabsList>
          <TabsTrigger value="details">Company Details</TabsTrigger>
          <TabsTrigger value="documents">Documents ({registration.documents?.length || 0})</TabsTrigger>
          <TabsTrigger value="licenses">Licenses ({parsedLicenses.length})</TabsTrigger>
          <TabsTrigger value="history">Status History</TabsTrigger>
        </TabsList>

        {/* Company Details Tab */}
        <TabsContent value="details" className="space-y-4">
          {/* Approval Progress Indicator */}
          <Card>
            <CardHeader>
              <CardTitle>Approval Progress</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                <div className="flex justify-between items-center">
                  <p className="text-sm font-medium text-gray-700">Current Status</p>
                  <p className="text-sm font-semibold text-gray-900">{getApprovalProgress(registration.status, registration.documents).label}</p>
                </div>
                <div className="w-full bg-gray-200 rounded-full h-3">
                  <div
                    className={`h-3 rounded-full transition-all duration-500 ${getApprovalProgress(registration.status, registration.documents).color}`}
                    style={{ width: `${getApprovalProgress(registration.status, registration.documents).percentage}%` }}
                  />
                </div>
                <div className="flex justify-between text-xs text-gray-500">
                  <span>Draft</span>
                  <span>Submitted</span>
                  <span>Under Review</span>
                  <span>Completed</span>
                </div>
                <div className="grid grid-cols-2 gap-4 pt-3 border-t">
                  <div>
                    <p className="text-sm text-gray-600">Form Completion</p>
                    <p className="font-semibold">{registration.completionPercentage}%</p>
                  </div>
                  <div>
                    <p className="text-sm text-gray-600">Submitted Date</p>
                    <p className="font-semibold">
                      {registration.submittedDate
                        ? new Date(registration.submittedDate).toLocaleDateString('en-US', {
                            month: 'short',
                            day: '2-digit',
                            year: 'numeric'
                          })
                        : 'Not submitted'}
                    </p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Company Information */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Building2 className="w-5 h-5" />
                Company Information
              </CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label className="text-gray-600">Company Name</Label>
                <p className="font-semibold">{registration.companyName}</p>
              </div>
              {registration.tradingName && (
                <div>
                  <Label className="text-gray-600">Trading Name</Label>
                  <p className="font-semibold">{registration.tradingName}</p>
                </div>
              )}
              <div>
                <Label className="text-gray-600">Partner Type</Label>
                <div><Badge variant="outline">{registration.partnerType}</Badge></div>
              </div>
              {registration.registrationNumber && (
                <div>
                  <Label className="text-gray-600">Company Registration Number</Label>
                  <p className="font-semibold">{registration.registrationNumber}</p>
                </div>
              )}
              {registration.taxNumber && (
                <div>
                  <Label className="text-gray-600">Tax Number</Label>
                  <p className="font-semibold">{registration.taxNumber}</p>
                </div>
              )}
              {registration.vatNumber && (
                <div>
                  <Label className="text-gray-600">VAT Number</Label>
                  <p className="font-semibold">{registration.vatNumber}</p>
                </div>
              )}
              {registration.industryType && (
                <div>
                  <Label className="text-gray-600">Industry Type</Label>
                  <p className="font-semibold">{registration.industryType}</p>
                </div>
              )}
              {registration.yearsInBusiness && (
                <div>
                  <Label className="text-gray-600">Years in Business</Label>
                  <p className="font-semibold">{registration.yearsInBusiness} years</p>
                </div>
              )}
              {registration.numberOfEmployees && (
                <div>
                  <Label className="text-gray-600">Number of Employees</Label>
                  <p className="font-semibold">{registration.numberOfEmployees}</p>
                </div>
              )}
              {registration.annualRevenue && (
                <div>
                  <Label className="text-gray-600">Annual Revenue (USD)</Label>
                  <p className="font-semibold">${registration.annualRevenue.toLocaleString()}</p>
                </div>
              )}
              {registration.website && (
                <div className="md:col-span-2">
                  <Label className="text-gray-600">Website</Label>
                  <p className="font-semibold">
                    <a href={registration.website} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline">
                      {registration.website}
                    </a>
                  </p>
                </div>
              )}
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
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label className="text-gray-600">Email</Label>
                <p className="font-semibold">{registration.email}</p>
              </div>
              <div>
                <Label className="text-gray-600">Phone</Label>
                <p className="font-semibold">{registration.phone}</p>
              </div>
              {registration.alternatePhone && (
                <div>
                  <Label className="text-gray-600">Alternate Phone</Label>
                  <p className="font-semibold">{registration.alternatePhone}</p>
                </div>
              )}
              {registration.physicalAddress && (
                <div className="md:col-span-2">
                  <Label className="text-gray-600">Physical Address</Label>
                  <p className="font-semibold">{registration.physicalAddress}</p>
                </div>
              )}
              {(registration.city || registration.postalCode || registration.country) && (
                <div className="md:col-span-2 grid grid-cols-1 md:grid-cols-3 gap-4">
                  {registration.city && (
                    <div>
                      <Label className="text-gray-600">City</Label>
                      <p className="font-semibold">{registration.city}</p>
                    </div>
                  )}
                  {registration.postalCode && (
                    <div>
                      <Label className="text-gray-600">Postal Code</Label>
                      <p className="font-semibold">{registration.postalCode}</p>
                    </div>
                  )}
                  {registration.country && (
                    <div>
                      <Label className="text-gray-600">Country</Label>
                      <p className="font-semibold">{registration.country}</p>
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Primary Contact Person */}
          {(registration.contactPersonName || registration.contactPersonEmail || registration.contactPersonPhone) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <User className="w-5 h-5" />
                  Primary Contact Person
                </CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {registration.contactPersonName && (
                  <div>
                    <Label className="text-gray-600">Name</Label>
                    <p className="font-semibold">{registration.contactPersonName}</p>
                  </div>
                )}
                {registration.contactPersonTitle && (
                  <div>
                    <Label className="text-gray-600">Title</Label>
                    <p className="font-semibold">{registration.contactPersonTitle}</p>
                  </div>
                )}
                {registration.contactPersonEmail && (
                  <div>
                    <Label className="text-gray-600">Email</Label>
                    <p className="font-semibold">{registration.contactPersonEmail}</p>
                  </div>
                )}
                {registration.contactPersonPhone && (
                  <div>
                    <Label className="text-gray-600">Phone</Label>
                    <p className="font-semibold">{registration.contactPersonPhone}</p>
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Banking Information */}
          {(registration.bankName || registration.bankAccountNumber || registration.bankBranchCode) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Building2 className="w-5 h-5" />
                  Banking Information
                </CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {registration.bankName && (
                  <div>
                    <Label className="text-gray-600">Bank Name</Label>
                    <p className="font-semibold">{registration.bankName}</p>
                  </div>
                )}
                {registration.bankAccountNumber && (
                  <div>
                    <Label className="text-gray-600">Account Number</Label>
                    <p className="font-semibold">{registration.bankAccountNumber}</p>
                  </div>
                )}
                {registration.bankBranchCode && (
                  <div>
                    <Label className="text-gray-600">Branch Code</Label>
                    <p className="font-semibold">{registration.bankBranchCode}</p>
                  </div>
                )}
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Documents Tab */}
        <TabsContent value="documents">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <FileText className="w-5 h-5" />
                Uploaded Documents
              </CardTitle>
              <CardDescription>Review and verify uploaded documents</CardDescription>
            </CardHeader>
            <CardContent>
              {!registration.documents || registration.documents.length === 0 ? (
                <p className="text-center text-gray-500 py-8">No documents uploaded</p>
              ) : (
                <div className="space-y-3">
                  {registration.documents.map((doc) => (
                    <div key={doc.id} className="flex items-center justify-between p-4 border rounded-lg">
                      <div className="flex items-center gap-3">
                        <FileText className="w-5 h-5 text-blue-600" />
                        <div>
                          <p className="font-medium">{doc.documentType}</p>
                          <p className="text-sm text-gray-600">{doc.documentName}</p>
                          <p className="text-xs text-gray-500">
                            Uploaded: {new Date(doc.uploadedAt).toLocaleDateString()}
                          </p>
                          {doc.isRejected && doc.rejectionReason && (
                            <p className="text-xs text-red-600 mt-1">
                              <strong>Rejection Reason:</strong> {doc.rejectionReason}
                            </p>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleViewDocument(doc.id, doc.documentName)}
                          title="View document"
                        >
                          <Eye className="w-4 h-4" />
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleDownloadDocument(doc.id, doc.documentName)}
                          title="Download document"
                        >
                          <Download className="w-4 h-4" />
                        </Button>
                        {doc.isVerified ? (
                          <Badge className="bg-green-600">Verified</Badge>
                        ) : doc.isRejected ? (
                          <>
                            <Badge variant="destructive">Rejected</Badge>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleRevertDocumentRejection(doc.id, doc.documentName)}
                              title={`Revert rejection: ${doc.rejectionReason}`}
                            >
                              <CheckCircle className="w-4 h-4 mr-1" />
                              Revert
                            </Button>
                          </>
                        ) : (
                          <>
                            <Button
                              size="sm"
                              onClick={() => openVerifyDocDialog(doc.id, doc.documentName)}
                              className="bg-green-600 hover:bg-green-700"
                            >
                              <CheckCircle className="w-4 h-4 mr-1" />
                              Verify
                            </Button>
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() => openRejectDocDialog(doc.id, doc.documentName)}
                            >
                              <X className="w-4 h-4 mr-1" />
                              Reject
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Licenses Tab */}
        <TabsContent value="licenses">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Award className="w-5 h-5" />
                Licenses & Certifications ({parsedLicenses.length})
              </CardTitle>
            </CardHeader>
            <CardContent>
              {parsedLicenses.length === 0 ? (
                <p className="text-center text-gray-500 py-8">No licenses provided</p>
              ) : (
                <div className="space-y-3">
                  {parsedLicenses.map((license, index) => (
                    <div key={index} className="border-l-4 border-blue-500 pl-4 py-3 bg-blue-50 rounded-r-lg">
                      <div className="flex items-start gap-3">
                        <Award className="w-6 h-6 text-blue-600 mt-0.5 flex-shrink-0" />
                        <div className="flex-1">
                          <p className="font-semibold text-lg text-blue-900 mb-3">
                            {getLicenseTypeName(license.licenseTypeId)}
                          </p>
                          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                            <div>
                              <Label className="text-gray-600 text-xs">License Number</Label>
                              <p className="font-semibold">{license.licenseNumber}</p>
                            </div>
                            <div>
                              <Label className="text-gray-600 text-xs">Issuing Authority</Label>
                              <p className="font-semibold">{license.issuingAuthority}</p>
                            </div>
                            <div>
                              <Label className="text-gray-600 text-xs">Issue Date</Label>
                              <p className="font-semibold">{format(new Date(license.issueDate), 'MMM dd, yyyy')}</p>
                            </div>
                            {license.expiryDate && (
                              <div>
                                <Label className="text-gray-600 text-xs">Expiry Date</Label>
                                <p className="font-semibold">{format(new Date(license.expiryDate), 'MMM dd, yyyy')}</p>
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
            </Card>
          </TabsContent>

        {/* Status History Tab */}
        <TabsContent value="history">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Clock className="w-5 h-5" />
                Status History
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!registration.statusHistory || registration.statusHistory.length === 0 ? (
                <p className="text-center text-gray-500 py-8">No status history available</p>
              ) : (
                <div className="space-y-3">
                  {registration.statusHistory.map((history) => (
                    <div key={history.id} className="flex items-start gap-3 p-4 border-l-4 border-blue-600">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                          {getStatusBadge(history.status)}
                          <span className="text-sm text-gray-600">
                            {new Date(history.changedAt).toLocaleString()}
                          </span>
                        </div>
                        {history.changedBy && (
                          <p className="text-sm text-gray-600 flex items-center gap-1">
                            <User className="w-3 h-3" />
                            Changed by: {history.changedBy}
                          </p>
                        )}
                        {history.notes && (
                          <p className="text-sm mt-2 bg-gray-50 p-2 rounded">{history.notes}</p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Approve Dialog */}
      <Dialog open={approveDialogOpen} onOpenChange={setApproveDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Approve Registration</DialogTitle>
            <DialogDescription>
              This will approve the registration and create a business partner record.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label htmlFor="approvalNotes">Approval Notes (Optional)</Label>
              <Textarea
                id="approvalNotes"
                value={approvalNotes}
                onChange={(e) => setApprovalNotes(e.target.value)}
                placeholder="Add any notes about the approval..."
                rows={4}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleApprove} disabled={actionLoading} className="bg-green-600 hover:bg-green-700">
              {actionLoading ? 'Approving...' : 'Approve Registration'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reject Dialog */}
      <Dialog open={rejectDialogOpen} onOpenChange={setRejectDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Registration</DialogTitle>
            <DialogDescription>
              Please provide a reason for rejecting this registration.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label htmlFor="rejectionReason">Rejection Reason *</Label>
              <Textarea
                id="rejectionReason"
                value={rejectionReason}
                onChange={(e) => setRejectionReason(e.target.value)}
                placeholder="Explain why this registration is being rejected..."
                rows={4}
                required
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleReject} disabled={actionLoading} variant="destructive">
              {actionLoading ? 'Rejecting...' : 'Reject Registration'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Request More Info Dialog */}
      <Dialog open={moreInfoDialogOpen} onOpenChange={setMoreInfoDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request More Information</DialogTitle>
            <DialogDescription>
              Specify what additional information is needed from the applicant.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label htmlFor="moreInfoNotes">Information Needed *</Label>
              <Textarea
                id="moreInfoNotes"
                value={moreInfoNotes}
                onChange={(e) => setMoreInfoNotes(e.target.value)}
                placeholder="Describe what information is needed..."
                rows={4}
                required
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setMoreInfoDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleRequestMoreInfo} disabled={actionLoading}>
              {actionLoading ? 'Sending...' : 'Request Information'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Verify Document Dialog */}
      <Dialog open={verifyDocDialogOpen} onOpenChange={setVerifyDocDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Verify Document</DialogTitle>
            <DialogDescription>
              Are you sure you want to verify the document "{selectedDocument?.name}"?
              This action confirms that the document has been reviewed and is acceptable.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setVerifyDocDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleVerifyDocument} className="bg-green-600 hover:bg-green-700">
              <CheckCircle className="w-4 h-4 mr-2" />
              Verify Document
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reject Document Dialog */}
      <Dialog open={rejectDocDialogOpen} onOpenChange={setRejectDocDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Document</DialogTitle>
            <DialogDescription>
              Please provide a reason for rejecting the document "{selectedDocument?.name}".
              The applicant will be notified of this rejection.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="docRejectionReason">Rejection Reason *</Label>
              <Textarea
                id="docRejectionReason"
                value={docRejectionReason}
                onChange={(e) => setDocRejectionReason(e.target.value)}
                placeholder="Explain why this document is being rejected..."
                rows={4}
                required
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectDocDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleRejectDocument}
              disabled={!docRejectionReason.trim()}
            >
              <X className="w-4 h-4 mr-2" />
              Reject Document
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

