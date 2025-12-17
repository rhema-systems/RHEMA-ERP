'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
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
  Clock
} from 'lucide-react';
import { toast } from 'sonner';
import * as tenderAwardService from '@/services/tenderAwardService';
import { type TenderAwardDto, type CancelAwardDto } from '@/services/tenderAwardService';
import { format } from 'date-fns';

export default function AwardDetailPage() {
  const params = useParams();
  const router = useRouter();
  const awardId = params.id as string;

  const [award, setAward] = useState<TenderAwardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

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

      await tenderAwardService.sendAwardNotifications(award.tenderId, notificationData);
      toast.success('Award notifications sent successfully');
    } catch (error) {
      console.error('Error sending notifications:', error);
      toast.error('Failed to send notifications');
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
            <div className="flex items-center gap-4">
              <Button onClick={handleSendNotifications}>
                <Send className="h-4 w-4 mr-2" />
                Send Notifications
              </Button>
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
    </div>
  );
}

