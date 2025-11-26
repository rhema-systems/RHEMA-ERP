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
  User
} from 'lucide-react';
import { toast } from 'sonner';
import { registrationReviewService, type RegistrationDetailDto } from '@/services/registrationReviewService';

export default function RegistrationDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params.id as string;

  const [registration, setRegistration] = useState<RegistrationDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  
  // Dialog states
  const [approveDialogOpen, setApproveDialogOpen] = useState(false);
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [moreInfoDialogOpen, setMoreInfoDialogOpen] = useState(false);
  
  const [approvalNotes, setApprovalNotes] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [moreInfoNotes, setMoreInfoNotes] = useState('');

  useEffect(() => {
    loadRegistration();
  }, [id]);

  const loadRegistration = async () => {
    try {
      setLoading(true);
      const data = await registrationReviewService.getById(id);
      setRegistration(data);
    } catch (error) {
      console.error('Error loading registration:', error);
      toast.error('Failed to load registration details');
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async () => {
    try {
      setActionLoading(true);
      await registrationReviewService.approve(id, approvalNotes || undefined);
      toast.success('Registration approved successfully');
      setApproveDialogOpen(false);
      router.push('/administration/procurement/registrations');
    } catch (error) {
      console.error('Error approving registration:', error);
      toast.error('Failed to approve registration');
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

  const handleVerifyDocument = async (documentId: string) => {
    try {
      await registrationReviewService.verifyDocument(id, documentId);
      toast.success('Document verified');
      loadRegistration();
    } catch (error) {
      console.error('Error verifying document:', error);
      toast.error('Failed to verify document');
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
            <p className="text-gray-600 mt-1">
              Application #{registration.applicationNumber} • {getStatusBadge(registration.status)}
            </p>
          </div>
        </div>

        <div className="flex gap-2">
          {canApprove && (
            <Button onClick={() => setApproveDialogOpen(true)} className="bg-green-600 hover:bg-green-700">
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
            <Button onClick={() => setMoreInfoDialogOpen(true)} variant="outline">
              <AlertCircle className="w-4 h-4 mr-2" />
              Request More Info
            </Button>
          )}
        </div>
      </div>

      {/* Content Tabs */}
      <Tabs defaultValue="details" className="space-y-4">
        <TabsList>
          <TabsTrigger value="details">Company Details</TabsTrigger>
          <TabsTrigger value="documents">Documents ({registration.documents?.length || 0})</TabsTrigger>
          <TabsTrigger value="licenses">Licenses ({registration.licenses?.length || 0})</TabsTrigger>
          <TabsTrigger value="history">Status History</TabsTrigger>
        </TabsList>

        {/* Company Details Tab */}
        <TabsContent value="details" className="space-y-4">
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
                <p><Badge variant="outline">{registration.partnerType}</Badge></p>
              </div>
              {registration.registrationNumber && (
                <div>
                  <Label className="text-gray-600">Registration Number</Label>
                  <p className="font-semibold">{registration.registrationNumber}</p>
                </div>
              )}
              {registration.taxNumber && (
                <div>
                  <Label className="text-gray-600">Tax Number</Label>
                  <p className="font-semibold">{registration.taxNumber}</p>
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
            </CardContent>
          </Card>

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
              {registration.physicalAddress && (
                <div className="md:col-span-2">
                  <Label className="text-gray-600">Physical Address</Label>
                  <p className="font-semibold">
                    {registration.physicalAddress}
                    {registration.city && `, ${registration.city}`}
                    {registration.country && `, ${registration.country}`}
                  </p>
                </div>
              )}
              {registration.contactPersonName && (
                <div className="md:col-span-2">
                  <Label className="text-gray-600">Primary Contact Person</Label>
                  <p className="font-semibold">{registration.contactPersonName}</p>
                  {registration.contactPersonEmail && (
                    <p className="text-sm text-gray-600">{registration.contactPersonEmail}</p>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
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
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        {doc.isVerified ? (
                          <Badge className="bg-green-600">Verified</Badge>
                        ) : (
                          <Button
                            size="sm"
                            onClick={() => handleVerifyDocument(doc.id)}
                          >
                            <CheckCircle className="w-4 h-4 mr-1" />
                            Verify
                          </Button>
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
                Licenses & Certifications
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!registration.licenses || registration.licenses.length === 0 ? (
                <p className="text-center text-gray-500 py-8">No licenses provided</p>
              ) : (
                <div className="space-y-3">
                  {registration.licenses.map((license, index) => (
                    <div key={index} className="p-4 border rounded-lg">
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <Label className="text-gray-600">License Number</Label>
                          <p className="font-semibold">{license.licenseNumber}</p>
                        </div>
                        <div>
                          <Label className="text-gray-600">Issuing Authority</Label>
                          <p className="font-semibold">{license.issuingAuthority}</p>
                        </div>
                        <div>
                          <Label className="text-gray-600">Issue Date</Label>
                          <p className="font-semibold">{new Date(license.issueDate).toLocaleDateString()}</p>
                        </div>
                        {license.expiryDate && (
                          <div>
                            <Label className="text-gray-600">Expiry Date</Label>
                            <p className="font-semibold">{new Date(license.expiryDate).toLocaleDateString()}</p>
                          </div>
                        )}
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
    </div>
  );
}

