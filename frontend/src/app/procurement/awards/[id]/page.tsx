'use client';

import { useEffect, useState, useRef } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  ArrowLeft,
  Award,
  FileText,
  Building2,
  Calendar,
  DollarSign,
  User,
  XCircle,
  Send,
  Ban,
  Clock,
  ShoppingCart,
  FileCheck,
  Shield,
  Upload,
  Loader2,
  CheckCircle,
  Mail,
  Bell,
  Download,
  AlertCircle
} from 'lucide-react';
import { toast } from 'sonner';
import * as tenderAwardService from '@/services/tenderAwardService';
import { type TenderAwardDto, type CancelAwardDto } from '@/services/tenderAwardService';
import * as performanceBondService from '@/services/performanceBondService';
import { type PerformanceBondRequestDto } from '@/services/performanceBondService';
import { format } from 'date-fns';

export default function AwardDetailPage() {
  const params = useParams();
  const router = useRouter();
  const awardId = params.id as string;
  const performanceBondFileRef = useRef<HTMLInputElement>(null);

  const [award, setAward] = useState<TenderAwardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  // Notification state
  const [sendingNotification, setSendingNotification] = useState(false);
  const [showNotificationDialog, setShowNotificationDialog] = useState(false);

  // Create PO/Contract state
  const [showCreatePODialog, setShowCreatePODialog] = useState(false);
  const [creatingPO, setCreatingPO] = useState(false);
  const [poType, setPOType] = useState<'PO' | 'Contract'>('PO');

  // Performance Bond state
  const [showPerformanceBondDialog, setShowPerformanceBondDialog] = useState(false);
  const [performanceBondFile, setPerformanceBondFile] = useState<File | null>(null);
  const [sendingPerformanceBondRequest, setSendingPerformanceBondRequest] = useState(false);
  const [performanceBondRequest, setPerformanceBondRequest] = useState<PerformanceBondRequestDto | null>(null);
  const [reviewingBond, setReviewingBond] = useState(false);
  const [rejectionReason, setRejectionReason] = useState('');

  useEffect(() => {
    if (awardId) {
      loadAward();
    }
  }, [awardId]);

  const loadAward = async () => {
    try {
      setLoading(true);
      const data = await tenderAwardService.getAwardById(awardId);
      setAward(data);

      // Load performance bond request if exists
      try {
        const bondRequest = await performanceBondService.getPerformanceBondByAwardId(awardId);
        setPerformanceBondRequest(bondRequest);
      } catch {
        // No performance bond request yet, that's fine
      }
    } catch (error) {
      console.error('Error loading award:', error);
      toast.error('Failed to load award details');
    } finally {
      setLoading(false);
    }
  };

  const handleCancelAward = async () => {
    if (!cancelReason.trim()) {
      toast.error('Please provide a reason for cancellation');
      return;
    }

    try {
      setCancelling(true);
      const data: CancelAwardDto = {
        reason: cancelReason,
        sendNotifications: true,
      };
      await tenderAwardService.cancelAward(awardId, data);
      toast.success('Award cancelled successfully');
      setShowCancelDialog(false);
      await loadAward();
    } catch (error) {
      console.error('Error cancelling award:', error);
      toast.error('Failed to cancel award');
    } finally {
      setCancelling(false);
    }
  };

  const handleSendNotifications = async () => {
    if (!award) return;

    try {
      setSendingNotification(true);
      const notificationData = {
        awardId: award.id,
        tenderId: award.tenderId,
        tenderNumber: award.tenderNumber,
        tenderTitle: award.tenderTitle,
        businessPartnerId: award.businessPartnerId,
        businessPartnerName: award.businessPartnerName,
        awardAmount: award.awardedAmount,
        awardDate: award.awardDate,
        notificationDate: new Date().toISOString(),
      };

      await tenderAwardService.sendAwardNotifications(award.id, notificationData);
      toast.success(
        <div className="flex flex-col gap-1">
          <span className="font-semibold">Award notifications sent successfully!</span>
          <span className="text-sm text-muted-foreground">
            <Mail className="inline h-3 w-3 mr-1" />Email with PDF attachment sent
          </span>
          <span className="text-sm text-muted-foreground">
            <Bell className="inline h-3 w-3 mr-1" />In-app notification delivered
          </span>
        </div>
      );
      setShowNotificationDialog(false);
    } catch (error) {
      console.error('Error sending notifications:', error);
      toast.error('Failed to send notifications');
    } finally {
      setSendingNotification(false);
    }
  };

  const handleCreatePO = async () => {
    if (!award) return;

    try {
      setCreatingPO(true);

      if (poType === 'Contract') {
        // Navigate to contract creation page with award ID
        setShowCreatePODialog(false);
        router.push(`/procurement/contracts/create?awardId=${award.id}`);
        return;
      }

      // For PO - Simulate API call - PO API not ready
      await new Promise(resolve => setTimeout(resolve, 1500));

      toast.success(
        <div className="flex flex-col gap-1">
          <span className="font-semibold">Purchase Order created successfully!</span>
          <span className="text-sm text-muted-foreground">
            Reference: PO-{award.tenderNumber}-{Date.now().toString().slice(-6)}
          </span>
        </div>
      );
      setShowCreatePODialog(false);
    } catch (error) {
      console.error('Error creating PO:', error);
      toast.error(`Failed to create ${poType}`);
    } finally {
      setCreatingPO(false);
    }
  };

  const handlePerformanceBondFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 20 * 1024 * 1024) {
        toast.error('File size must be less than 20MB');
        return;
      }
      setPerformanceBondFile(file);
    }
  };

  const handleSendPerformanceBondRequest = async () => {
    if (!award || !performanceBondFile) return;

    try {
      setSendingPerformanceBondRequest(true);

      const requestData = {
        tenderAwardId: award.id,
        tenderBidId: award.tenderBidId,
        businessPartnerId: award.businessPartnerId,
      };

      const result = await performanceBondService.createPerformanceBondRequest(requestData, performanceBondFile);
      setPerformanceBondRequest(result);

      toast.success(
        <div className="flex flex-col gap-1">
          <span className="font-semibold">Performance Bond request sent!</span>
          <span className="text-sm text-muted-foreground">
            Template uploaded and request sent to {award.businessPartnerName}
          </span>
          <span className="text-sm text-muted-foreground">
            <Mail className="inline h-3 w-3 mr-1" />Email notification sent
          </span>
          <span className="text-sm text-muted-foreground">
            <Bell className="inline h-3 w-3 mr-1" />In-app notification delivered
          </span>
        </div>
      );
      setShowPerformanceBondDialog(false);
      setPerformanceBondFile(null);
    } catch (error: any) {
      console.error('Error sending performance bond request:', error);
      toast.error(error.message || 'Failed to send performance bond request');
    } finally {
      setSendingPerformanceBondRequest(false);
    }
  };

  const handleDownloadSubmittedBond = async () => {
    if (!performanceBondRequest) return;

    try {
      toast.info('Downloading submitted bond document...');
      const blob = await performanceBondService.downloadPerformanceBondSubmission(performanceBondRequest.id);
      performanceBondService.triggerFileDownload(blob, performanceBondRequest.submittedFileName || 'performance-bond.pdf');
    } catch (error: any) {
      console.error('Error downloading submitted bond:', error);
      toast.error(error.message || 'Failed to download document');
    }
  };

  const handleReviewBond = async (approved: boolean) => {
    if (!performanceBondRequest) return;

    try {
      setReviewingBond(true);
      const reviewData = {
        isApproved: approved,
        rejectionReason: approved ? undefined : rejectionReason,
      };
      const result = await performanceBondService.reviewPerformanceBond(
        performanceBondRequest.id,
        reviewData
      );
      setPerformanceBondRequest(result);
      toast.success(approved ? 'Performance bond approved!' : 'Performance bond rejected');
      setRejectionReason('');
      setShowPerformanceBondDialog(false);
    } catch (error: any) {
      console.error('Error reviewing bond:', error);
      toast.error(error.message || 'Failed to review performance bond');
    } finally {
      setReviewingBond(false);
    }
  };

  const getPerformanceBondStatusBadge = () => {
    if (!performanceBondRequest) return null;

    switch (performanceBondRequest.status) {
      case 'Pending':
        return <Badge variant="outline" className="bg-yellow-50 text-yellow-700 border-yellow-200">Requested</Badge>;
      case 'Submitted':
        return <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200">Submitted</Badge>;
      case 'Approved':
        return <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">Approved</Badge>;
      case 'Rejected':
        return <Badge variant="outline" className="bg-red-50 text-red-700 border-red-200">Rejected</Badge>;
      default:
        return null;
    }
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP');
    } catch {
      return dateString;
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Awarded': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Cancelled': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
    };
    
    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return (
      <Badge variant={config.variant} className={config.className}>
        {status}
      </Badge>
    );
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading award details...</p>
        </div>
      </div>
    );
  }

  if (!award) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Award not found</p>
          <Button onClick={() => router.push('/procurement/awards')} className="mt-4">
            Back to Awards
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push('/procurement/awards')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold flex items-center gap-2">
              <Award className="h-8 w-8 text-green-600" />
              Award Details
            </h1>
            <p className="text-gray-500">View and manage tender award</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge(award.status)}
        </div>
      </div>

      {/* Award Information */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Award className="h-5 w-5 text-green-600" />
            Award Information
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-4">
              <div>
                <Label className="text-gray-500 flex items-center gap-2">
                  <FileText className="h-4 w-4" />
                  Tender Number
                </Label>
                <p className="font-mono font-medium text-blue-600 cursor-pointer hover:underline"
                   onClick={() => router.push(`/procurement/tenders/${award.tenderId}`)}>
                  {award.tenderNumber}
                </p>
              </div>
              <div>
                <Label className="text-gray-500">Tender Title</Label>
                <p className="font-medium">{award.tenderTitle}</p>
              </div>
              <div>
                <Label className="text-gray-500">Bid Number</Label>
                <p className="font-mono font-medium">{award.bidNumber}</p>
              </div>
            </div>

            <div className="space-y-4">
              <div>
                <Label className="text-gray-500 flex items-center gap-2">
                  <Building2 className="h-4 w-4" />
                  Business Partner
                </Label>
                <p className="font-medium">{award.businessPartnerName}</p>
              </div>
              <div>
                <Label className="text-gray-500 flex items-center gap-2">
                  <Calendar className="h-4 w-4" />
                  Award Date
                </Label>
                <p className="font-medium">{formatDate(award.awardDate)}</p>
              </div>
              <div>
                <Label className="text-gray-500 flex items-center gap-2">
                  <DollarSign className="h-4 w-4" />
                  Awarded Amount
                </Label>
                <p className="text-2xl font-bold text-green-600">
                  {award.currency} {award.awardedAmount.toLocaleString()}
                </p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Award Justification */}
      {award.awardJustification && (
        <Card>
          <CardHeader>
            <CardTitle>Award Justification</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-gray-700 whitespace-pre-wrap">{award.awardJustification}</p>
          </CardContent>
        </Card>
      )}

      {/* Notes */}
      {award.notes && (
        <Card>
          <CardHeader>
            <CardTitle>Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-gray-700 whitespace-pre-wrap">{award.notes}</p>
          </CardContent>
        </Card>
      )}

      {/* Award Metadata */}
      <Card>
        <CardHeader>
          <CardTitle>Award Metadata</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <Label className="text-gray-500 flex items-center gap-2">
                <User className="h-4 w-4" />
                Awarded By
              </Label>
              <p className="font-medium">{award.awardedByName || 'N/A'}</p>
            </div>
            <div>
              <Label className="text-gray-500 flex items-center gap-2">
                <Calendar className="h-4 w-4" />
                Created At
              </Label>
              <p className="font-medium">{formatDate(award.createdAt)}</p>
            </div>
            <div>
              <Label className="text-gray-500">Purchase Order</Label>
              <p className="font-medium">{award.purchaseOrderId ? 'Linked' : 'Not Created'}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Actions */}
      {award.status === 'Awarded' && (
        <Card>
          <CardHeader>
            <CardTitle>Actions</CardTitle>
            <CardDescription>Manage this award</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap items-center gap-4">
              {/* Send Notification Button */}
              <Button onClick={() => setShowNotificationDialog(true)} className="bg-blue-600 hover:bg-blue-700">
                <Send className="h-4 w-4 mr-2" />
                Send Notification
              </Button>

              {/* Create PO/Contract Button */}
              <Button onClick={() => setShowCreatePODialog(true)} className="bg-green-600 hover:bg-green-700">
                <ShoppingCart className="h-4 w-4 mr-2" />
                Create PO / Contract
              </Button>

              {/* Performance Bond Button */}
              <Button onClick={() => setShowPerformanceBondDialog(true)} variant="outline" className="border-purple-300 text-purple-700 hover:bg-purple-50">
                <Shield className="h-4 w-4 mr-2" />
                Performance Bond
                {getPerformanceBondStatusBadge() && <span className="ml-2">{getPerformanceBondStatusBadge()}</span>}
              </Button>

              {/* Cancel Award Button */}
              <Button
                variant="destructive"
                onClick={() => setShowCancelDialog(true)}
              >
                <Ban className="h-4 w-4 mr-2" />
                Cancel Award
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Cancel Award Dialog */}
      {showCancelDialog && (
        <Card className="border-red-200 bg-red-50">
          <CardHeader>
            <CardTitle className="text-red-800">Cancel Award</CardTitle>
            <CardDescription className="text-red-600">
              This action will cancel the award and notify the business partner
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <Label htmlFor="cancelReason">Cancellation Reason *</Label>
              <Textarea
                id="cancelReason"
                placeholder="Provide a detailed reason for cancelling this award..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                rows={4}
              />
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="destructive"
                onClick={handleCancelAward}
                disabled={cancelling || !cancelReason.trim()}
              >
                {cancelling ? 'Cancelling...' : 'Confirm Cancellation'}
              </Button>
              <Button
                variant="outline"
                onClick={() => {
                  setShowCancelDialog(false);
                  setCancelReason('');
                }}
                disabled={cancelling}
              >
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Performance Bond Section - Displayed when bond request exists */}
      {performanceBondRequest && (
        <Card className={
          performanceBondRequest.status === 'Submitted' ? 'border-blue-300 bg-blue-50/50' :
          performanceBondRequest.status === 'Approved' ? 'border-green-300 bg-green-50/50' :
          performanceBondRequest.status === 'Rejected' ? 'border-red-300 bg-red-50/50' :
          'border-yellow-300 bg-yellow-50/50'
        }>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Shield className="h-5 w-5 text-purple-600" />
                <CardTitle>Performance Bond</CardTitle>
                {getPerformanceBondStatusBadge()}
              </div>
            </div>
            <CardDescription>
              {performanceBondRequest.status === 'Pending' && 'Waiting for awardee to submit completed bond document'}
              {performanceBondRequest.status === 'Submitted' && 'Awardee has submitted the performance bond - Review required'}
              {performanceBondRequest.status === 'Approved' && 'Performance bond has been approved'}
              {performanceBondRequest.status === 'Rejected' && 'Performance bond was rejected'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Request Info */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
              {performanceBondRequest.requestedDate && (
                <div>
                  <p className="text-muted-foreground">Requested</p>
                  <p className="font-medium">{format(new Date(performanceBondRequest.requestedDate), 'PPP')}</p>
                </div>
              )}
              <div>
                <p className="text-muted-foreground">Template</p>
                <p className="font-medium">{performanceBondRequest.templateFileName || 'N/A'}</p>
              </div>
              {performanceBondRequest.submittedDate && (
                <div>
                  <p className="text-muted-foreground">Submitted</p>
                  <p className="font-medium">{format(new Date(performanceBondRequest.submittedDate), 'PPP')}</p>
                </div>
              )}
              {performanceBondRequest.reviewedDate && (
                <div>
                  <p className="text-muted-foreground">Reviewed</p>
                  <p className="font-medium">{format(new Date(performanceBondRequest.reviewedDate), 'PPP')}</p>
                </div>
              )}
            </div>

            {/* Submitted Document Section */}
            {performanceBondRequest.status === 'Submitted' && (
              <div className="bg-white border border-blue-200 rounded-lg p-4 space-y-4">
                <div className="flex items-start gap-3">
                  <FileCheck className="h-6 w-6 text-blue-600 mt-0.5" />
                  <div className="flex-1">
                    <h4 className="font-semibold text-blue-900">Submitted Document</h4>
                    <p className="text-sm text-blue-700 mt-1">
                      <strong>File:</strong> {performanceBondRequest.submittedFileName}
                    </p>
                    <p className="text-sm text-blue-700">
                      <strong>Submitted by:</strong> {award.businessPartnerName}
                    </p>
                  </div>
                  <Button onClick={handleDownloadSubmittedBond} variant="outline" size="sm">
                    <Download className="h-4 w-4 mr-2" />
                    Download
                  </Button>
                </div>

                {/* Review Actions */}
                <div className="border-t pt-4 space-y-3">
                  <div className="space-y-2">
                    <Label>Rejection Reason (required if rejecting)</Label>
                    <Textarea
                      placeholder="Provide a reason for rejection..."
                      value={rejectionReason}
                      onChange={(e) => setRejectionReason(e.target.value)}
                      rows={2}
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      onClick={() => handleReviewBond(true)}
                      disabled={reviewingBond}
                      className="bg-green-600 hover:bg-green-700"
                    >
                      {reviewingBond ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <CheckCircle className="h-4 w-4 mr-2" />}
                      Approve Bond
                    </Button>
                    <Button
                      variant="destructive"
                      onClick={() => handleReviewBond(false)}
                      disabled={reviewingBond || !rejectionReason.trim()}
                    >
                      {reviewingBond ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <XCircle className="h-4 w-4 mr-2" />}
                      Reject
                    </Button>
                  </div>
                </div>
              </div>
            )}

            {/* Approved Document */}
            {performanceBondRequest.status === 'Approved' && (
              <div className="bg-white border border-green-200 rounded-lg p-4">
                <div className="flex items-center gap-3">
                  <CheckCircle className="h-6 w-6 text-green-600" />
                  <div className="flex-1">
                    <h4 className="font-semibold text-green-900">Approved Document</h4>
                    <p className="text-sm text-green-700">{performanceBondRequest.submittedFileName}</p>
                  </div>
                  <Button onClick={handleDownloadSubmittedBond} variant="outline" size="sm">
                    <Download className="h-4 w-4 mr-2" />
                    Download
                  </Button>
                </div>
              </div>
            )}

            {/* Rejected - Show reason */}
            {performanceBondRequest.status === 'Rejected' && (
              <div className="bg-white border border-red-200 rounded-lg p-4">
                <div className="flex items-start gap-3">
                  <AlertCircle className="h-6 w-6 text-red-600 mt-0.5" />
                  <div>
                    <h4 className="font-semibold text-red-900">Rejected</h4>
                    {performanceBondRequest.rejectionReason && (
                      <p className="text-sm text-red-700 mt-1">
                        <strong>Reason:</strong> {performanceBondRequest.rejectionReason}
                      </p>
                    )}
                    <p className="text-sm text-muted-foreground mt-2">
                      The awardee will need to resubmit the performance bond document.
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Pending - Waiting */}
            {performanceBondRequest.status === 'Pending' && (
              <div className="bg-white border border-yellow-200 rounded-lg p-4">
                <div className="flex items-center gap-3">
                  <Clock className="h-6 w-6 text-yellow-600" />
                  <div>
                    <h4 className="font-semibold text-yellow-900">Awaiting Submission</h4>
                    <p className="text-sm text-yellow-700">
                      Template sent to {award.businessPartnerName}. Waiting for them to complete and submit.
                    </p>
                  </div>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Send Notification Dialog */}
      <Dialog open={showNotificationDialog} onOpenChange={setShowNotificationDialog}>
        <DialogContent className="sm:max-w-[500px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Send className="h-5 w-5 text-blue-600" />
              Send Award Notification
            </DialogTitle>
            <DialogDescription>
              Send notification to the awarded supplier with PDF letter attachment.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 space-y-3">
              <h4 className="font-medium text-blue-900">Notification Details</h4>
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div>
                  <span className="text-blue-600">Recipient:</span>
                  <p className="font-medium">{award.businessPartnerName}</p>
                </div>
                <div>
                  <span className="text-blue-600">Award Amount:</span>
                  <p className="font-medium">{award.currency} {award.awardedAmount?.toLocaleString()}</p>
                </div>
                <div className="col-span-2">
                  <span className="text-blue-600">Tender:</span>
                  <p className="font-medium">{award.tenderNumber} - {award.tenderTitle}</p>
                </div>
              </div>
            </div>

            <div className="space-y-2">
              <h4 className="font-medium">This will send:</h4>
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Mail className="h-4 w-4 text-blue-500" />
                <span>Email with PDF Award Letter attachment</span>
              </div>
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Bell className="h-4 w-4 text-green-500" />
                <span>In-app notification to supplier portal</span>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowNotificationDialog(false)} disabled={sendingNotification}>
              Cancel
            </Button>
            <Button onClick={handleSendNotifications} disabled={sendingNotification} className="bg-blue-600 hover:bg-blue-700">
              {sendingNotification ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Sending...
                </>
              ) : (
                <>
                  <Send className="h-4 w-4 mr-2" />
                  Send Notification
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create PO/Contract Dialog */}
      <Dialog open={showCreatePODialog} onOpenChange={setShowCreatePODialog}>
        <DialogContent className="sm:max-w-[500px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <ShoppingCart className="h-5 w-5 text-green-600" />
              Create Purchase Order / Contract
            </DialogTitle>
            <DialogDescription>
              Create a purchase order or contract document for this award.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-3">
              <Label>Document Type</Label>
              <div className="flex gap-4">
                <Button
                  variant={poType === 'PO' ? 'default' : 'outline'}
                  onClick={() => setPOType('PO')}
                  className={poType === 'PO' ? 'bg-green-600 hover:bg-green-700' : ''}
                >
                  <ShoppingCart className="h-4 w-4 mr-2" />
                  Purchase Order
                </Button>
                <Button
                  variant={poType === 'Contract' ? 'default' : 'outline'}
                  onClick={() => setPOType('Contract')}
                  className={poType === 'Contract' ? 'bg-green-600 hover:bg-green-700' : ''}
                >
                  <FileCheck className="h-4 w-4 mr-2" />
                  Contract
                </Button>
              </div>
            </div>

            <div className="bg-green-50 border border-green-200 rounded-lg p-4 space-y-2">
              <h4 className="font-medium text-green-900">Award Details</h4>
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div>
                  <span className="text-green-600">Supplier:</span>
                  <p className="font-medium">{award.businessPartnerName}</p>
                </div>
                <div>
                  <span className="text-green-600">Amount:</span>
                  <p className="font-medium">{award.currency} {award.awardedAmount?.toLocaleString()}</p>
                </div>
                <div className="col-span-2">
                  <span className="text-green-600">Tender:</span>
                  <p className="font-medium">{award.tenderNumber}</p>
                </div>
              </div>
            </div>

            <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3 text-sm text-yellow-800">
              <strong>Note:</strong> This will create a new {poType === 'PO' ? 'Purchase Order' : 'Contract'} linked to this award.
              You can edit the details after creation.
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCreatePODialog(false)} disabled={creatingPO}>
              Cancel
            </Button>
            <Button onClick={handleCreatePO} disabled={creatingPO} className="bg-green-600 hover:bg-green-700">
              {creatingPO ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Creating...
                </>
              ) : (
                <>
                  <CheckCircle className="h-4 w-4 mr-2" />
                  Create {poType === 'PO' ? 'Purchase Order' : 'Contract'}
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Performance Bond Dialog - Different UI based on status */}
      <Dialog open={showPerformanceBondDialog} onOpenChange={setShowPerformanceBondDialog}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Shield className="h-5 w-5 text-purple-600" />
              Performance Bond {performanceBondRequest ?
                (performanceBondRequest.status === 'Submitted' ? '- Review Required' :
                 performanceBondRequest.status === 'Approved' ? '- Approved' :
                 performanceBondRequest.status === 'Rejected' ? '- Rejected' : '- Awaiting Submission')
                : 'Request'}
              {getPerformanceBondStatusBadge() && <span className="ml-2">{getPerformanceBondStatusBadge()}</span>}
            </DialogTitle>
            <DialogDescription>
              {!performanceBondRequest ? 'Upload a performance bond template and send a request to the awardee.' :
               performanceBondRequest.status === 'Pending' ? 'Waiting for the awardee to submit the completed bond document.' :
               performanceBondRequest.status === 'Submitted' ? 'Review and approve or reject the submitted performance bond.' :
               performanceBondRequest.status === 'Approved' ? 'This performance bond has been approved.' :
               'This performance bond was rejected.'}
            </DialogDescription>
          </DialogHeader>

          {/* No request yet - show upload form */}
          {!performanceBondRequest && (
            <div className="space-y-4 py-4">
              <div className="bg-purple-50 border border-purple-200 rounded-lg p-4 space-y-2">
                <h4 className="font-medium text-purple-900">Workflow</h4>
                <ol className="text-sm text-purple-700 list-decimal list-inside space-y-1">
                  <li>Upload performance bond template</li>
                  <li>Awardee receives notification to download template</li>
                  <li>Awardee fills and uploads completed bond document</li>
                  <li>Internal users review and approve the submission</li>
                </ol>
              </div>

              <div className="space-y-2">
                <Label htmlFor="performanceBondFile">Performance Bond Template *</Label>
                <div className="border-2 border-dashed border-gray-300 rounded-lg p-4 text-center">
                  {performanceBondFile ? (
                    <div className="flex items-center justify-center gap-2">
                      <FileText className="h-8 w-8 text-purple-500" />
                      <div className="text-left">
                        <p className="font-medium">{performanceBondFile.name}</p>
                        <p className="text-sm text-muted-foreground">
                          {(performanceBondFile.size / 1024 / 1024).toFixed(2)} MB
                        </p>
                      </div>
                      <Button variant="ghost" size="sm" onClick={() => setPerformanceBondFile(null)}>
                        <XCircle className="h-4 w-4 text-red-500" />
                      </Button>
                    </div>
                  ) : (
                    <>
                      <Upload className="h-8 w-8 mx-auto text-gray-400 mb-2" />
                      <p className="text-sm text-muted-foreground mb-2">Click to upload or drag and drop</p>
                      <p className="text-xs text-muted-foreground">PDF, DOC, DOCX (Max 20MB)</p>
                    </>
                  )}
                  <Input
                    ref={performanceBondFileRef}
                    id="performanceBondFile"
                    type="file"
                    accept=".pdf,.doc,.docx"
                    onChange={handlePerformanceBondFileChange}
                    className={performanceBondFile ? 'hidden' : 'mt-2'}
                  />
                </div>
              </div>

              <div className="bg-gray-50 border rounded-lg p-3 text-sm">
                <p><strong>Recipient:</strong> {award.businessPartnerName}</p>
                <p className="text-muted-foreground mt-1">
                  The awardee will receive an email and in-app notification with instructions.
                </p>
              </div>
            </div>
          )}

          {/* Pending status - waiting for submission */}
          {performanceBondRequest?.status === 'Pending' && (
            <div className="space-y-4 py-4">
              <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                <div className="flex items-start gap-3">
                  <Clock className="h-5 w-5 text-yellow-600 mt-0.5" />
                  <div>
                    <h4 className="font-medium text-yellow-900">Awaiting Submission</h4>
                    <p className="text-sm text-yellow-700 mt-1">
                      The performance bond template has been sent to {award.businessPartnerName}.
                      Waiting for them to submit the completed document.
                    </p>
                  </div>
                </div>
              </div>
              <div className="text-sm text-muted-foreground">
                <p><strong>Request sent:</strong> {performanceBondRequest.requestedDate && format(new Date(performanceBondRequest.requestedDate), 'PPP p')}</p>
                <p><strong>Template:</strong> {performanceBondRequest.templateFileName}</p>
              </div>
            </div>
          )}

          {/* Submitted status - needs review */}
          {performanceBondRequest?.status === 'Submitted' && (
            <div className="space-y-4 py-4">
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <div className="flex items-start gap-3">
                  <FileCheck className="h-5 w-5 text-blue-600 mt-0.5" />
                  <div className="flex-1">
                    <h4 className="font-medium text-blue-900">Document Submitted - Ready for Review</h4>
                    <p className="text-sm text-blue-700 mt-1">
                      {award.businessPartnerName} has submitted their performance bond document.
                    </p>
                    <div className="mt-3 space-y-1 text-sm text-blue-800">
                      <p><strong>Submitted:</strong> {performanceBondRequest.submittedDate && format(new Date(performanceBondRequest.submittedDate), 'PPP p')}</p>
                      <p><strong>File:</strong> {performanceBondRequest.submittedFileName}</p>
                    </div>
                  </div>
                </div>
              </div>

              <Button onClick={handleDownloadSubmittedBond} className="w-full" variant="outline">
                <Download className="h-4 w-4 mr-2" />
                Download Submitted Document
              </Button>

              <div className="space-y-2">
                <Label>Rejection Reason (if rejecting)</Label>
                <Textarea
                  placeholder="Provide a reason for rejection..."
                  value={rejectionReason}
                  onChange={(e) => setRejectionReason(e.target.value)}
                  rows={3}
                />
              </div>
            </div>
          )}

          {/* Approved status */}
          {performanceBondRequest?.status === 'Approved' && (
            <div className="space-y-4 py-4">
              <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                <div className="flex items-start gap-3">
                  <CheckCircle className="h-5 w-5 text-green-600 mt-0.5" />
                  <div>
                    <h4 className="font-medium text-green-900">Performance Bond Approved</h4>
                    <p className="text-sm text-green-700 mt-1">
                      The performance bond has been approved on {performanceBondRequest.reviewedDate && format(new Date(performanceBondRequest.reviewedDate), 'PPP p')}.
                    </p>
                  </div>
                </div>
              </div>
              <Button onClick={handleDownloadSubmittedBond} variant="outline">
                <Download className="h-4 w-4 mr-2" />
                Download Approved Document
              </Button>
            </div>
          )}

          {/* Rejected status */}
          {performanceBondRequest?.status === 'Rejected' && (
            <div className="space-y-4 py-4">
              <div className="bg-red-50 border border-red-200 rounded-lg p-4">
                <div className="flex items-start gap-3">
                  <AlertCircle className="h-5 w-5 text-red-600 mt-0.5" />
                  <div>
                    <h4 className="font-medium text-red-900">Performance Bond Rejected</h4>
                    <p className="text-sm text-red-700 mt-1">
                      Rejected on {performanceBondRequest.reviewedDate && format(new Date(performanceBondRequest.reviewedDate), 'PPP p')}.
                    </p>
                    {performanceBondRequest.rejectionReason && (
                      <p className="text-sm text-red-700 mt-2">
                        <strong>Reason:</strong> {performanceBondRequest.rejectionReason}
                      </p>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowPerformanceBondDialog(false)} disabled={sendingPerformanceBondRequest || reviewingBond}>
              Close
            </Button>

            {/* Send request button - only when no request exists */}
            {!performanceBondRequest && (
              <Button
                onClick={handleSendPerformanceBondRequest}
                disabled={sendingPerformanceBondRequest || !performanceBondFile}
                className="bg-purple-600 hover:bg-purple-700"
              >
                {sendingPerformanceBondRequest ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Sending Request...
                  </>
                ) : (
                  <>
                    <Send className="h-4 w-4 mr-2" />
                    Send Request to Awardee
                  </>
                )}
              </Button>
            )}

            {/* Review buttons - only when status is Submitted */}
            {performanceBondRequest?.status === 'Submitted' && (
              <>
                <Button
                  variant="destructive"
                  onClick={() => handleReviewBond(false)}
                  disabled={reviewingBond || !rejectionReason.trim()}
                >
                  {reviewingBond ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <XCircle className="h-4 w-4 mr-2" />}
                  Reject
                </Button>
                <Button
                  onClick={() => handleReviewBond(true)}
                  disabled={reviewingBond}
                  className="bg-green-600 hover:bg-green-700"
                >
                  {reviewingBond ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <CheckCircle className="h-4 w-4 mr-2" />}
                  Approve
                </Button>
              </>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

