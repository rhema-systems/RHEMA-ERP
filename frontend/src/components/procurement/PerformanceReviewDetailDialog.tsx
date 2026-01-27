'use client';

import { useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { toast } from 'sonner';
import { performanceTrackingService, type PerformanceReviewDto } from '@/services/performanceTrackingService';
import { format } from 'date-fns';
import { Send, CheckCircle, Trash2, Star } from 'lucide-react';

interface PerformanceReviewDetailDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  review: PerformanceReviewDto | null;
  onSuccess: () => void;
  onEdit?: () => void;
}

export function PerformanceReviewDetailDialog({
  open,
  onOpenChange,
  review,
  onSuccess,
  onEdit
}: PerformanceReviewDetailDialogProps) {
  const [loading, setLoading] = useState(false);
  const [supplierComments, setSupplierComments] = useState('');

  if (!review) return null;

  const handleSubmit = async () => {
    try {
      setLoading(true);
      await performanceTrackingService.submitReview(review.id);
      toast.success('Review submitted successfully');
      onSuccess();
      onOpenChange(false);
    } catch (error) {
      console.error('Error submitting review:', error);
      toast.error('Failed to submit review');
    } finally {
      setLoading(false);
    }
  };

  const handleAcknowledge = async () => {
    try {
      setLoading(true);
      await performanceTrackingService.acknowledgeReview(review.id);
      toast.success('Review acknowledged successfully');
      onSuccess();
      onOpenChange(false);
    } catch (error) {
      console.error('Error acknowledging review:', error);
      toast.error('Failed to acknowledge review');
    } finally {
      setLoading(false);
    }
  };

  const handleFinalize = async () => {
    try {
      setLoading(true);
      await performanceTrackingService.finalizeReview(review.id);
      toast.success('Review finalized successfully');
      onSuccess();
      onOpenChange(false);
    } catch (error) {
      console.error('Error finalizing review:', error);
      toast.error('Failed to finalize review');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!confirm('Are you sure you want to delete this review?')) return;
    
    try {
      setLoading(true);
      await performanceTrackingService.deleteReview(review.id);
      toast.success('Review deleted successfully');
      onSuccess();
      onOpenChange(false);
    } catch (error) {
      console.error('Error deleting review:', error);
      toast.error('Failed to delete review');
    } finally {
      setLoading(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const variants: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', label: string }> = {
      'Draft': { variant: 'secondary', label: 'Draft' },
      'Submitted': { variant: 'default', label: 'Submitted' },
      'Acknowledged': { variant: 'outline', label: 'Acknowledged' },
      'Finalized': { variant: 'default', label: 'Finalized' }
    };
    const config = variants[status] || { variant: 'secondary' as const, label: status };
    return <Badge variant={config.variant}>{config.label}</Badge>;
  };

  const getGradeBadge = (score: number) => {
    let grade = 'F';
    let color = 'bg-red-100 text-red-800';
    
    if (score >= 4.5) { grade = 'A+'; color = 'bg-green-100 text-green-800'; }
    else if (score >= 4.0) { grade = 'A'; color = 'bg-green-100 text-green-800'; }
    else if (score >= 3.5) { grade = 'B+'; color = 'bg-blue-100 text-blue-800'; }
    else if (score >= 3.0) { grade = 'B'; color = 'bg-blue-100 text-blue-800'; }
    else if (score >= 2.5) { grade = 'C+'; color = 'bg-yellow-100 text-yellow-800'; }
    else if (score >= 2.0) { grade = 'C'; color = 'bg-yellow-100 text-yellow-800'; }
    else if (score >= 1.5) { grade = 'D'; color = 'bg-orange-100 text-orange-800'; }
    
    return <Badge className={color}>{grade}</Badge>;
  };

  const renderStars = (score: number | undefined) => {
    const safeScore = score ?? 0;
    return (
      <div className="flex gap-1">
        {Array.from({ length: 5 }, (_, i) => (
          <Star
            key={i}
            className={`w-4 h-4 ${i < Math.floor(safeScore) ? 'fill-yellow-400 text-yellow-400' : 'text-gray-300'}`}
          />
        ))}
        <span className="ml-2 text-sm font-medium">{safeScore.toFixed(1)}</span>
      </div>
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center justify-between">
            <DialogTitle>Performance Review Details</DialogTitle>
            {getStatusBadge(review.status)}
          </div>
          <DialogDescription>
            Review #{review.reviewNumber} • {review.reviewPeriod} {review.reviewYear}
            {review.reviewMonth && ` - ${new Date(review.reviewYear, review.reviewMonth - 1).toLocaleString('default', { month: 'long' })}`}
            {review.reviewQuarter && ` - Q${review.reviewQuarter}`}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 py-4">
          {/* Overall Score */}
          <div className="text-center p-6 bg-gradient-to-br from-blue-50 to-indigo-50 rounded-lg">
            <div className="text-5xl font-bold text-blue-600 mb-2">{(review.overallScore ?? 0).toFixed(1)}</div>
            <div className="text-lg font-semibold mb-2">Overall Score</div>
            {getGradeBadge(review.overallScore ?? 0)}
          </div>

          {/* Individual Scores */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="p-4 border rounded-lg">
              <Label className="text-sm text-gray-600">Delivery Performance</Label>
              {renderStars(review.deliveryPerformanceScore ?? 0)}
            </div>
            <div className="p-4 border rounded-lg">
              <Label className="text-sm text-gray-600">Quality Score</Label>
              {renderStars(review.qualityScore ?? 0)}
            </div>
            <div className="p-4 border rounded-lg">
              <Label className="text-sm text-gray-600">Cost Competitiveness</Label>
              {renderStars(review.costCompetitivenessScore ?? 0)}
            </div>
            <div className="p-4 border rounded-lg">
              <Label className="text-sm text-gray-600">Customer Service</Label>
              {renderStars(review.customerServiceScore ?? 0)}
            </div>
            <div className="p-4 border rounded-lg">
              <Label className="text-sm text-gray-600">Compliance Score</Label>
              {renderStars(review.complianceScore ?? 0)}
            </div>
            <div className="p-4 border rounded-lg">
              <Label className="text-sm text-gray-600">Innovation Score</Label>
              {renderStars(review.innovationScore ?? 0)}
            </div>
          </div>

          {/* Review Content */}
          {review.strengths && (
            <div>
              <Label className="font-semibold">Strengths</Label>
              <p className="mt-2 text-sm text-gray-700 whitespace-pre-wrap">{review.strengths}</p>
            </div>
          )}

          {review.weaknesses && (
            <div>
              <Label className="font-semibold">Areas for Improvement</Label>
              <p className="mt-2 text-sm text-gray-700 whitespace-pre-wrap">{review.weaknesses}</p>
            </div>
          )}

          {review.recommendations && (
            <div>
              <Label className="font-semibold">Recommendations</Label>
              <p className="mt-2 text-sm text-gray-700 whitespace-pre-wrap">{review.recommendations}</p>
            </div>
          )}

          {review.actionItems && (
            <div>
              <Label className="font-semibold">Action Items</Label>
              <p className="mt-2 text-sm text-gray-700 whitespace-pre-wrap">{review.actionItems}</p>
            </div>
          )}

          {/* Review Metadata */}
          <div className="pt-4 border-t space-y-2 text-sm text-gray-600">
            <div>
              <strong>Review Date:</strong> {format(new Date(review.reviewDate), 'MMM dd, yyyy')}
            </div>
            {review.reviewedByName && (
              <div>
                <strong>Reviewed By:</strong> {review.reviewedByName}
              </div>
            )}
            {review.acknowledgedAt && (
              <div>
                <strong>Acknowledged:</strong> {format(new Date(review.acknowledgedAt), 'MMM dd, yyyy')}
              </div>
            )}
            {review.finalizedAt && (
              <div>
                <strong>Finalized:</strong> {format(new Date(review.finalizedAt), 'MMM dd, yyyy')}
              </div>
            )}
          </div>
        </div>

        <DialogFooter className="flex justify-between">
          <div>
            {review.status === 'Draft' && (
              <Button variant="destructive" onClick={handleDelete} disabled={loading}>
                <Trash2 className="w-4 h-4 mr-2" />
                Delete
              </Button>
            )}
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)} disabled={loading}>
              Close
            </Button>
            {review.status === 'Draft' && onEdit && (
              <Button variant="outline" onClick={() => { onEdit(); onOpenChange(false); }} disabled={loading}>
                Edit
              </Button>
            )}
            {review.status === 'Draft' && (
              <Button onClick={handleSubmit} disabled={loading}>
                <Send className="w-4 h-4 mr-2" />
                Submit
              </Button>
            )}
            {review.status === 'Submitted' && (
              <Button onClick={handleAcknowledge} disabled={loading}>
                <CheckCircle className="w-4 h-4 mr-2" />
                Acknowledge
              </Button>
            )}
            {review.status === 'Acknowledged' && (
              <Button onClick={handleFinalize} disabled={loading}>
                <CheckCircle className="w-4 h-4 mr-2" />
                Finalize
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

