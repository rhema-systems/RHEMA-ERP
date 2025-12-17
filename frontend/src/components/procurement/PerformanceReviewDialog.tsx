'use client';

import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Slider } from '@/components/ui/slider';
import { Checkbox } from '@/components/ui/checkbox';
import { toast } from 'sonner';
import { performanceTrackingService, type PerformanceReviewDto } from '@/services/performanceTrackingService';
import { Save, Send } from 'lucide-react';

interface PerformanceReviewDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  businessPartnerId: string;
  review?: PerformanceReviewDto | null;
  onSuccess: () => void;
}

export function PerformanceReviewDialog({
  open,
  onOpenChange,
  businessPartnerId,
  review,
  onSuccess
}: PerformanceReviewDialogProps) {
  const [loading, setLoading] = useState(false);
  const [reviewPeriod, setReviewPeriod] = useState('Monthly');
  const [reviewYear, setReviewYear] = useState(new Date().getFullYear());
  const [reviewMonth, setReviewMonth] = useState<number | undefined>(new Date().getMonth() + 1);
  const [reviewQuarter, setReviewQuarter] = useState<number | undefined>(undefined);
  
  // Scores (0-5 scale)
  const [deliveryScore, setDeliveryScore] = useState(3);
  const [qualityScore, setQualityScore] = useState(3);
  const [costScore, setCostScore] = useState(3);
  const [serviceScore, setServiceScore] = useState(3);
  const [complianceScore, setComplianceScore] = useState(3);
  const [innovationScore, setInnovationScore] = useState(3);
  
  // Text fields
  const [strengths, setStrengths] = useState('');
  const [weaknesses, setWeaknesses] = useState('');
  const [recommendations, setRecommendations] = useState('');
  const [actionItems, setActionItems] = useState('');
  
  // Follow-up
  const [requiresFollowUp, setRequiresFollowUp] = useState(false);
  const [followUpDate, setFollowUpDate] = useState('');

  useEffect(() => {
    if (review) {
      setReviewPeriod(review.reviewPeriod);
      setReviewYear(review.reviewYear ?? new Date().getFullYear());
      setReviewMonth(review.reviewMonth);
      setReviewQuarter(review.reviewQuarter);
      setDeliveryScore(review.deliveryPerformanceScore ?? 3);
      setQualityScore(review.qualityScore ?? 3);
      setCostScore(review.costCompetitivenessScore ?? 3);
      setServiceScore(review.customerServiceScore ?? 3);
      setComplianceScore(review.complianceScore ?? 3);
      setInnovationScore(review.innovationScore ?? 3);
      setStrengths(review.strengths || '');
      setWeaknesses(review.areasForImprovement || '');
      setRecommendations(review.recommendations || '');
      setActionItems(review.actionItems || '');
      setRequiresFollowUp(review.requiresFollowUp ?? false);
      setFollowUpDate(review.followUpDate ? new Date(review.followUpDate).toISOString().split('T')[0] : '');
    } else {
      resetForm();
    }
  }, [review, open]);

  const resetForm = () => {
    setReviewPeriod('Monthly');
    setReviewYear(new Date().getFullYear());
    setReviewMonth(new Date().getMonth() + 1);
    setReviewQuarter(undefined);
    setDeliveryScore(3);
    setQualityScore(3);
    setCostScore(3);
    setServiceScore(3);
    setComplianceScore(3);
    setInnovationScore(3);
    setStrengths('');
    setWeaknesses('');
    setRecommendations('');
    setActionItems('');
    setRequiresFollowUp(false);
    setFollowUpDate('');
  };

  const calculatePeriodDates = () => {
    const year = reviewYear;
    let startDate: Date;
    let endDate: Date;

    if (reviewPeriod === 'Monthly') {
      startDate = new Date(year, reviewMonth - 1, 1);
      endDate = new Date(year, reviewMonth, 0); // Last day of month
    } else if (reviewPeriod === 'Quarterly') {
      const quarterStartMonth = (reviewQuarter - 1) * 3;
      startDate = new Date(year, quarterStartMonth, 1);
      endDate = new Date(year, quarterStartMonth + 3, 0); // Last day of quarter
    } else {
      // Yearly
      startDate = new Date(year, 0, 1);
      endDate = new Date(year, 11, 31);
    }

    return { startDate, endDate };
  };

  const handleSaveDraft = async () => {
    try {
      setLoading(true);
      const { startDate, endDate } = calculatePeriodDates();

      const reviewData = {
        businessPartnerId,
        reviewDate: new Date().toISOString(),
        reviewPeriod,
        periodStartDate: startDate.toISOString(),
        periodEndDate: endDate.toISOString(),
        deliveryPerformanceScore: deliveryScore,
        qualityScore: qualityScore,
        costCompetitivenessScore: costScore,
        customerServiceScore: serviceScore,
        complianceScore: complianceScore,
        innovationScore: innovationScore,
        strengths: strengths || '',
        areasForImprovement: weaknesses || '', // Map weaknesses to areasForImprovement
        recommendations: recommendations || '',
        actionItems: actionItems || '',
        requiresFollowUp: requiresFollowUp,
        followUpDate: followUpDate ? new Date(followUpDate).toISOString() : undefined
      };

      if (review) {
        await performanceTrackingService.updateReview(review.id, reviewData);
        toast.success('Review draft updated successfully');
      } else {
        await performanceTrackingService.createReview(reviewData);
        toast.success('Review draft saved successfully');
      }

      onSuccess();
      onOpenChange(false);
    } catch (error: any) {
      console.error('Error saving review:', error);
      const errorMessage = error?.message || 'Failed to save review draft';
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async () => {
    try {
      setLoading(true);
      const { startDate, endDate } = calculatePeriodDates();

      // First save/update the review
      let reviewId = review?.id;
      if (!reviewId) {
        const reviewData = {
          businessPartnerId,
          reviewDate: new Date().toISOString(),
          reviewPeriod,
          periodStartDate: startDate.toISOString(),
          periodEndDate: endDate.toISOString(),
          deliveryPerformanceScore: deliveryScore,
          qualityScore: qualityScore,
          costCompetitivenessScore: costScore,
          customerServiceScore: serviceScore,
          complianceScore: complianceScore,
          innovationScore: innovationScore,
          strengths: strengths || '',
          areasForImprovement: weaknesses || '',
          recommendations: recommendations || '',
          actionItems: actionItems || '',
          requiresFollowUp: requiresFollowUp,
          followUpDate: followUpDate ? new Date(followUpDate).toISOString() : undefined
        };
        const created = await performanceTrackingService.createReview(reviewData);
        reviewId = created.id;
      }

      // Then submit it
      await performanceTrackingService.submitReview(reviewId);
      toast.success('Review submitted successfully');
      onSuccess();
      onOpenChange(false);
    } catch (error: any) {
      console.error('Error submitting review:', error);
      const errorMessage = error?.message || 'Failed to submit review';
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const overallScore = ((deliveryScore + qualityScore + costScore + serviceScore + complianceScore + innovationScore) / 6).toFixed(1);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{review ? 'Edit Performance Review' : 'Create Performance Review'}</DialogTitle>
          <DialogDescription>
            Evaluate supplier performance across multiple dimensions
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 py-4">
          {/* Period Selection */}
          <div className="grid grid-cols-3 gap-4">
            <div>
              <Label>Review Period</Label>
              <Select value={reviewPeriod} onValueChange={setReviewPeriod}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Monthly">Monthly</SelectItem>
                  <SelectItem value="Quarterly">Quarterly</SelectItem>
                  <SelectItem value="Yearly">Yearly</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Year</Label>
              <Input
                type="number"
                value={reviewYear}
                onChange={(e) => setReviewYear(parseInt(e.target.value))}
                min={2020}
                max={2030}
              />
            </div>
            {reviewPeriod === 'Monthly' && (
              <div>
                <Label>Month</Label>
                <Select value={reviewMonth?.toString()} onValueChange={(v) => setReviewMonth(parseInt(v))}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                      <SelectItem key={m} value={m.toString()}>
                        {new Date(2000, m - 1).toLocaleString('default', { month: 'long' })}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {reviewPeriod === 'Quarterly' && (
              <div>
                <Label>Quarter</Label>
                <Select value={reviewQuarter?.toString()} onValueChange={(v) => setReviewQuarter(parseInt(v))}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">Q1</SelectItem>
                    <SelectItem value="2">Q2</SelectItem>
                    <SelectItem value="3">Q3</SelectItem>
                    <SelectItem value="4">Q4</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>

          {/* Performance Scores */}
          <div className="space-y-4">
            <h3 className="font-semibold text-sm">Performance Scores (0-5 scale)</h3>

            <div className="space-y-3">
              <div>
                <div className="flex justify-between mb-2">
                  <Label>Delivery Performance</Label>
                  <span className="text-sm font-medium">{deliveryScore.toFixed(1)}</span>
                </div>
                <Slider
                  value={[deliveryScore]}
                  onValueChange={(values: number[]) => setDeliveryScore(values[0])}
                  min={0}
                  max={5}
                  step={0.5}
                  className="w-full"
                />
              </div>

              <div>
                <div className="flex justify-between mb-2">
                  <Label>Quality Score</Label>
                  <span className="text-sm font-medium">{qualityScore.toFixed(1)}</span>
                </div>
                <Slider
                  value={[qualityScore]}
                  onValueChange={(values: number[]) => setQualityScore(values[0])}
                  min={0}
                  max={5}
                  step={0.5}
                  className="w-full"
                />
              </div>

              <div>
                <div className="flex justify-between mb-2">
                  <Label>Cost Competitiveness</Label>
                  <span className="text-sm font-medium">{costScore.toFixed(1)}</span>
                </div>
                <Slider
                  value={[costScore]}
                  onValueChange={(values: number[]) => setCostScore(values[0])}
                  min={0}
                  max={5}
                  step={0.5}
                  className="w-full"
                />
              </div>

              <div>
                <div className="flex justify-between mb-2">
                  <Label>Customer Service</Label>
                  <span className="text-sm font-medium">{serviceScore.toFixed(1)}</span>
                </div>
                <Slider
                  value={[serviceScore]}
                  onValueChange={(values: number[]) => setServiceScore(values[0])}
                  min={0}
                  max={5}
                  step={0.5}
                  className="w-full"
                />
              </div>

              <div>
                <div className="flex justify-between mb-2">
                  <Label>Compliance Score</Label>
                  <span className="text-sm font-medium">{complianceScore.toFixed(1)}</span>
                </div>
                <Slider
                  value={[complianceScore]}
                  onValueChange={(values: number[]) => setComplianceScore(values[0])}
                  min={0}
                  max={5}
                  step={0.5}
                  className="w-full"
                />
              </div>

              <div>
                <div className="flex justify-between mb-2">
                  <Label>Innovation Score</Label>
                  <span className="text-sm font-medium">{innovationScore.toFixed(1)}</span>
                </div>
                <Slider
                  value={[innovationScore]}
                  onValueChange={(values: number[]) => setInnovationScore(values[0])}
                  min={0}
                  max={5}
                  step={0.5}
                  className="w-full"
                />
              </div>

              <div className="pt-2 border-t">
                <div className="flex justify-between">
                  <Label className="font-semibold">Overall Score</Label>
                  <span className="text-lg font-bold text-blue-600">{overallScore}</span>
                </div>
              </div>
            </div>
          </div>

          {/* Text Fields */}
          <div className="space-y-4">
            <div>
              <Label>Strengths</Label>
              <Textarea
                value={strengths}
                onChange={(e) => setStrengths(e.target.value)}
                placeholder="What did the supplier do well?"
                rows={3}
              />
            </div>

            <div>
              <Label>Areas for Improvement</Label>
              <Textarea
                value={weaknesses}
                onChange={(e) => setWeaknesses(e.target.value)}
                placeholder="What needs improvement?"
                rows={3}
              />
            </div>

            <div>
              <Label>Recommendations</Label>
              <Textarea
                value={recommendations}
                onChange={(e) => setRecommendations(e.target.value)}
                placeholder="Recommendations for the supplier"
                rows={3}
              />
            </div>

            <div>
              <Label>Action Items</Label>
              <Textarea
                value={actionItems}
                onChange={(e) => setActionItems(e.target.value)}
                placeholder="Specific actions to be taken"
                rows={3}
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={loading}>
            Cancel
          </Button>
          <Button variant="outline" onClick={handleSaveDraft} disabled={loading}>
            <Save className="w-4 h-4 mr-2" />
            Save Draft
          </Button>
          <Button onClick={handleSubmit} disabled={loading}>
            <Send className="w-4 h-4 mr-2" />
            Submit Review
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

