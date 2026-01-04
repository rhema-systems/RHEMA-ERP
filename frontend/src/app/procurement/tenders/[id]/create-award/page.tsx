'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ArrowLeft, Award, TrendingUp, Save, Clock, XCircle, ClipboardCheck, CheckCircle2 } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderAwardService from '@/services/tenderAwardService';
import * as tenderService from '@/services/tenderService';
import { type CreateAwardDto, type AwardRecommendationDto } from '@/services/tenderAwardService';
import { type TenderDetailDto } from '@/services/tenderService';
import { format } from 'date-fns';
import { AwardVerificationDialog } from '@/components/procurement/tenders/AwardVerificationDialog';
import { awardVerificationService, TenderAwardVerification } from '@/services/awardVerificationService';

export default function CreateAwardPage() {
  const params = useParams();
  const router = useRouter();
  const tenderId = params.id as string;

  const [tender, setTender] = useState<TenderDetailDto | null>(null);
  const [recommendation, setRecommendation] = useState<AwardRecommendationDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Form state
  const [selectedBidId, setSelectedBidId] = useState('');
  const [awardedAmount, setAwardedAmount] = useState('');
  const [currency, setCurrency] = useState('USD');
  const [awardDate, setAwardDate] = useState(new Date().toISOString().split('T')[0]);
  const [awardJustification, setAwardJustification] = useState('');
  const [notes, setNotes] = useState('');

  // Verification state
  const [showVerificationDialog, setShowVerificationDialog] = useState(false);
  const [verificationComplete, setVerificationComplete] = useState(false);
  const [existingVerification, setExistingVerification] = useState<TenderAwardVerification | null>(null);

  useEffect(() => {
    if (tenderId) {
      loadData();
    }
  }, [tenderId]);

  const loadData = async () => {
    try {
      setLoading(true);

      // Load tender details
      const tenderData = await tenderService.getTenderById(tenderId);
      setTender(tenderData);

      // Load award recommendation
      const recommendationData = await tenderAwardService.generateAwardRecommendation(tenderId);
      setRecommendation(recommendationData);

      // Pre-fill with recommended bid if available
      if (recommendationData.recommendedBidId) {
        setSelectedBidId(recommendationData.recommendedBidId);
        setAwardedAmount(recommendationData.recommendedAmount.toString());
        setAwardJustification(
          `Recommended based on evaluation scores. Average score: ${recommendationData.recommendedScore.toFixed(2)}%`
        );
      }

      // Check for existing verification
      try {
        const verification = await awardVerificationService.getVerificationByTender(tenderId);
        if (verification) {
          setExistingVerification(verification);
          setVerificationComplete(verification.status === 'Completed');
        }
      } catch {
        // No existing verification, that's fine
      }
    } catch (error) {
      console.error('Error loading data:', error);
      toast.error('Failed to load tender data');
    } finally {
      setLoading(false);
    }
  };

  const handleBidSelection = (bidId: string) => {
    setSelectedBidId(bidId);
    
    // Find the selected bid and pre-fill amount
    const selectedBid = recommendation?.bidRecommendations.find(b => b.bidId === bidId);
    if (selectedBid) {
      setAwardedAmount(selectedBid.totalBidAmount.toString());
    }
  };

  const handleSubmit = async () => {
    // Validation
    if (!selectedBidId) {
      toast.error('Please select a bid to award');
      return;
    }

    if (!awardedAmount || parseFloat(awardedAmount) <= 0) {
      toast.error('Please enter a valid awarded amount');
      return;
    }

    if (!awardJustification.trim()) {
      toast.error('Please provide award justification');
      return;
    }

    try {
      setSaving(true);

      const data: CreateAwardDto = {
        tenderId,
        tenderBidId: selectedBidId,
        awardedAmount: parseFloat(awardedAmount),
        currency,
        awardDate,
        awardJustification,
        notes,
      };

      const award = await tenderAwardService.createAward(data);
      toast.success('Award created successfully');
      router.push(`/procurement/awards/${award.id}`);
    } catch (error) {
      console.error('Error creating award:', error);
      toast.error('Failed to create award');
    } finally {
      setSaving(false);
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

  const handleVerificationComplete = (verification: TenderAwardVerification) => {
    setExistingVerification(verification);
    setVerificationComplete(true);
  };

  const getSelectedBidderForVerification = () => {
    if (!selectedBidId || !recommendation) return [];
    const selectedBid = recommendation.bidRecommendations.find(b => b.bidId === selectedBidId);
    if (!selectedBid) return [];
    return [{
      bidId: selectedBid.bidId,
      businessPartnerId: selectedBid.businessPartnerId || '',
      businessPartnerName: selectedBid.businessPartnerName,
      bidNumber: selectedBid.bidNumber,
      totalBidAmount: selectedBid.totalBidAmount,
    }];
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading tender data...</p>
        </div>
      </div>
    );
  }

  if (!tender || !recommendation) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Tender not found or no evaluations available</p>
          <Button onClick={() => router.push(`/procurement/tenders/${tenderId}`)} className="mt-4">
            Back to Tender
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
          <Button variant="ghost" onClick={() => router.push(`/procurement/tenders/${tenderId}`)}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold flex items-center gap-2">
              <Award className="h-8 w-8 text-green-600" />
              Create Award
            </h1>
            <p className="text-gray-500">{tender.tenderNumber} - {tender.title}</p>
          </div>
        </div>
      </div>

      {/* Tender Summary */}
      <Card>
        <CardHeader>
          <CardTitle>Tender Summary</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <Label className="text-gray-500">Tender Type</Label>
              <p className="font-medium">{tender.tenderType}</p>
            </div>
            <div>
              <Label className="text-gray-500">Total Bids</Label>
              <p className="font-medium">{recommendation.totalBids}</p>
            </div>
            <div>
              <Label className="text-gray-500">Evaluated Bids</Label>
              <p className="font-medium text-blue-600">{recommendation.evaluatedBids}</p>
            </div>
            <div>
              <Label className="text-gray-500">Evaluation Progress</Label>
              <p className="font-medium text-green-600">
                {recommendation.totalBids > 0
                  ? Math.round((recommendation.evaluatedBids / recommendation.totalBids) * 100)
                  : 0}%
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Recommended Bid */}
      {recommendation.recommendedBidId && (
        <Card className="border-green-200 bg-green-50">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-green-800">
              <TrendingUp className="h-5 w-5" />
              Recommended Bid
            </CardTitle>
            <CardDescription className="text-green-600">
              Based on evaluation scores and criteria
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div>
                <Label className="text-green-600">Bid Number</Label>
                <p className="font-mono font-bold text-green-800">{recommendation.recommendedBidNumber}</p>
              </div>
              <div>
                <Label className="text-green-600">Business Partner</Label>
                <p className="font-bold text-green-800">{recommendation.recommendedBusinessPartner}</p>
              </div>
              <div>
                <Label className="text-green-600">Bid Amount</Label>
                <p className="font-bold text-green-800">
                  {tender.currency} {recommendation.recommendedAmount.toLocaleString()}
                </p>
              </div>
              <div>
                <Label className="text-green-600">Average Score</Label>
                <p className="text-2xl font-bold text-green-800">
                  {recommendation.recommendedScore.toFixed(2)}%
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Bid Selection */}
      <Card>
        <CardHeader>
          <CardTitle>Select Winning Bid</CardTitle>
          <CardDescription>Choose the bid to award the tender to</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-3">
            {recommendation.bidRecommendations.map((bid) => (
              <div
                key={bid.bidId}
                className={`p-4 border-2 rounded-lg cursor-pointer transition-all ${
                  selectedBidId === bid.bidId
                    ? 'border-green-500 bg-green-50'
                    : 'border-gray-200 hover:border-gray-300'
                }`}
                onClick={() => handleBidSelection(bid.bidId)}
              >
                <div className="flex items-center justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-3">
                      <p className="font-mono font-medium">{bid.bidNumber}</p>
                      <p className="font-bold">{bid.businessPartnerName}</p>
                      {bid.bidId === recommendation.recommendedBidId && (
                        <Badge className="bg-green-600">Recommended</Badge>
                      )}
                    </div>
                    <div className="mt-2 grid grid-cols-3 gap-4 text-sm">
                      <div>
                        <span className="text-gray-500">Bid Amount:</span>
                        <span className="ml-2 font-medium">
                          {tender.currency} {bid.totalBidAmount.toLocaleString()}
                        </span>
                      </div>
                      <div>
                        <span className="text-gray-500">Average Score:</span>
                        <span className="ml-2 font-bold text-blue-600">
                          {bid.averageScore.toFixed(2)}%
                        </span>
                      </div>
                      <div>
                        <span className="text-gray-500">Evaluations:</span>
                        <span className="ml-2 font-medium">{bid.evaluationCount}</span>
                      </div>
                    </div>
                  </div>
                  <div>
                    {selectedBidId === bid.bidId && (
                      <Award className="h-6 w-6 text-green-600" />
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Award Details Form */}
      <Card>
        <CardHeader>
          <CardTitle>Award Details</CardTitle>
          <CardDescription>Provide award information</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="awardedAmount">Awarded Amount *</Label>
              <Input
                id="awardedAmount"
                type="number"
                step="0.01"
                placeholder="Enter awarded amount"
                value={awardedAmount}
                onChange={(e) => setAwardedAmount(e.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="currency">Currency</Label>
              <Select value={currency} onValueChange={setCurrency}>
                <SelectTrigger id="currency">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="USD">USD</SelectItem>
                  <SelectItem value="EUR">EUR</SelectItem>
                  <SelectItem value="GBP">GBP</SelectItem>
                  <SelectItem value="KES">KES</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="awardDate">Award Date *</Label>
              <Input
                id="awardDate"
                type="date"
                value={awardDate}
                onChange={(e) => setAwardDate(e.target.value)}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="awardJustification">Award Justification *</Label>
            <Textarea
              id="awardJustification"
              placeholder="Provide detailed justification for this award..."
              value={awardJustification}
              onChange={(e) => setAwardJustification(e.target.value)}
              rows={4}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="notes">Additional Notes</Label>
            <Textarea
              id="notes"
              placeholder="Any additional notes or comments..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={3}
            />
          </div>
        </CardContent>
      </Card>

      {/* Optional Verification */}
      {selectedBidId && (
        <Card className={verificationComplete ? 'border-green-200 bg-green-50' : 'border-blue-200 bg-blue-50'}>
          <CardHeader className="pb-3">
            <CardTitle className={`flex items-center gap-2 ${verificationComplete ? 'text-green-800' : 'text-blue-800'}`}>
              {verificationComplete ? (
                <>
                  <CheckCircle2 className="h-5 w-5" />
                  Verification Complete
                </>
              ) : (
                <>
                  <ClipboardCheck className="h-5 w-5" />
                  Background Verification (Optional)
                </>
              )}
            </CardTitle>
            <CardDescription className={verificationComplete ? 'text-green-600' : 'text-blue-600'}>
              {verificationComplete
                ? 'Background verification has been completed for the selected bidder.'
                : 'You can optionally verify the selected bidder before creating the award.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button
              variant={verificationComplete ? 'outline' : 'secondary'}
              onClick={() => setShowVerificationDialog(true)}
              disabled={!selectedBidId}
            >
              <ClipboardCheck className="h-4 w-4 mr-2" />
              {verificationComplete ? 'View Verification' : 'Verify Bidder'}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Actions */}
      <div className="flex items-center justify-end gap-4">
        <Button
          variant="outline"
          onClick={() => router.push(`/procurement/tenders/${tenderId}`)}
        >
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={saving || !selectedBidId || !awardedAmount || !awardJustification.trim()}
        >
          <Save className="h-4 w-4 mr-2" />
          {saving ? 'Creating Award...' : 'Create Award'}
        </Button>
      </div>

      {/* Verification Dialog */}
      <AwardVerificationDialog
        open={showVerificationDialog}
        onOpenChange={setShowVerificationDialog}
        tenderId={tenderId}
        bidders={getSelectedBidderForVerification()}
        onVerificationComplete={handleVerificationComplete}
      />
    </div>
  );
}

