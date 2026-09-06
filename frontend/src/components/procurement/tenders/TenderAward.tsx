'use client';

import Link from 'next/link';
import { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { Award, TrendingUp, CheckCircle2, AlertCircle, Trophy, FileText, ClipboardCheck } from 'lucide-react';
import { toast } from 'sonner';
import { format } from 'date-fns';
import * as tenderAwardService from '@/services/tenderAwardService';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { AwardVerificationDialog } from './AwardVerificationDialog';
import { TenderAwardVerification, awardVerificationService } from '@/services/awardVerificationService';
import { procurementAwardReadinessService } from '@/services/procurement-award-readiness.service';
import type { ProcurementAwardReadinessDecision } from '@/types/procurement-award-readiness';
import { hasAwardReadinessAction } from '@/lib/procurement-award-readiness';
import { useAuth } from '@/hooks/use-auth';
import { isFinalTenderAward } from '@/lib/tender-award-lifecycle';
import { formatProcurementMoney, getTenderBidCurrency, normalizeProcurementCurrency } from '@/lib/procurement-currency';

interface TenderAwardProps {
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  tenderCurrency?: string;
  bids?: ReadonlyArray<{ id: string; currency?: string }>;
  onAwardCreated?: () => void;
}

export function TenderAward({ tenderId, tenderNumber, tenderTitle, tenderCurrency, bids, onAwardCreated }: TenderAwardProps) {
  const { hasPermission } = useAuth();
  const getBidCurrency = (bidId?: string) => getTenderBidCurrency(bidId, bids, tenderCurrency);
  const canSubmitAwardRecommendation = hasPermission('procurement.tender.administer');
  const [loading, setLoading] = useState(true);
  const [recommendation, setRecommendation] = useState<tenderAwardService.AwardRecommendationDto | null>(null);
  const [existingAward, setExistingAward] = useState<tenderAwardService.TenderAwardDto | null>(null);
  const [showAwardDialog, setShowAwardDialog] = useState(false);
  const [selectedBidId, setSelectedBidId] = useState<string>('');
  const [awardAmount, setAwardAmount] = useState<string>('');
  const [awardJustification, setAwardJustification] = useState('');
  const [submitting, setSubmitting] = useState(false);

  // Verification selection state
  const [selectedBidsForVerification, setSelectedBidsForVerification] = useState<string[]>([]);
  const [showVerificationDialog, setShowVerificationDialog] = useState(false);
  const [existingVerification, setExistingVerification] = useState<TenderAwardVerification | null>(null);
  const [readinessDecision, setReadinessDecision] =
    useState<ProcurementAwardReadinessDecision | null>(null);

  useEffect(() => {
    loadAwardData();
  }, [tenderId]);

  const loadAwardData = async () => {
    try {
      setLoading(true);

      // Check if award already exists
      const award = await tenderAwardService.getAwardByTenderId(tenderId);
      if (award && award.status !== 'Rejected') {
        setExistingAward(award);
      } else {
        setExistingAward(award);
        // Generate recommendation
        const rec = await tenderAwardService.generateAwardRecommendation(tenderId);
        setRecommendation(rec);

        // Pre-select recommended bid
        if (rec.recommendedBidId) {
          setSelectedBidId(rec.recommendedBidId);
          setAwardAmount(rec.recommendedAmount.toString());
        }

        // Check for existing verification
        const verification = await awardVerificationService.getVerificationByTender(tenderId);
        setExistingVerification(verification);

        try {
          const readiness = await procurementAwardReadinessService.latest(
            'Tender',
            tenderId
          );
          setReadinessDecision(readiness);
        } catch {
          setReadinessDecision(null);
        }
      }
    } catch (error: any) {
      console.error('Error loading award data:', error);
      toast.error(error.message || 'Failed to load award data');
    } finally {
      setLoading(false);
    }
  };

  const handleCreateAward = async () => {
    if (!selectedBidId) {
      toast.error('Please select a bid to award');
      return false;
    }

    if (!awardAmount || parseFloat(awardAmount) <= 0) {
      toast.error('Please enter a valid award amount');
      return false;
    }

    if (!readinessAllowsBid(selectedBidId)) {
      toast.error(
        'A current server-derived Ready decision for this recommended bid is required.'
      );
      return false;
    }

    try {
      setSubmitting(true);
      
      const dto: tenderAwardService.CreateAwardDto = {
        tenderId,
        tenderBidId: selectedBidId,
        awardedAmount: parseFloat(awardAmount),
        awardJustification: awardJustification.trim() || undefined,
        awardDate: new Date().toISOString(),
      };

      await tenderAwardService.createAward(dto);
      toast.success('Award recommendation submitted for independent approval');
      setShowAwardDialog(false);
      loadAwardData();
      onAwardCreated?.();
    } catch (error: any) {
      console.error('Error creating award:', error);
      toast.error(error.message || 'Failed to submit award recommendation');
      return false;
    } finally {
      setSubmitting(false);
    }
  };

  const handleSelectBid = (bidId: string, amount: number) => {
    if (!readinessAllowsBid(bidId)) {
      toast.error(
        'This bid cannot be awarded until the award-readiness gate is current and Ready.'
      );
      return;
    }
    setSelectedBidId(bidId);
    setAwardAmount(amount.toString());
    setShowAwardDialog(true);
  };

  // Toggle bid selection for verification
  const toggleBidForVerification = (bidId: string) => {
    setSelectedBidsForVerification(prev =>
      prev.includes(bidId)
        ? prev.filter(id => id !== bidId)
        : [...prev, bidId]
    );
  };

  // Handle verification dialog open
  const handleStartVerification = () => {
    // If no bidders selected but there's an existing verification, open to view it
    if (selectedBidsForVerification.length === 0 && !existingVerification) {
      toast.error('Please select at least one bidder to verify');
      return;
    }
    setShowVerificationDialog(true);
  };

  // Handle verification complete
  const handleVerificationComplete = (verification: TenderAwardVerification) => {
    toast.success(
      'Verification completed. Re-evaluate server award readiness before awarding.'
    );
    // Optionally reload data to reflect verification status
    setExistingVerification(verification);
    loadAwardData();
  };

  // Get selected bidders info for the verification dialog
  const getSelectedBiddersInfo = () => {
    if (!recommendation) return [];

    // If no bidders selected but there's an existing verification, return verified bidders
    if (selectedBidsForVerification.length === 0 && existingVerification) {
      return existingVerification.bidders?.map(bidder => {
        const bid = recommendation.bidRecommendations.find(b => b.bidId === bidder.tenderBidId);
        return {
          bidId: bidder.tenderBidId,
          businessPartnerId: bidder.businessPartnerId,
          businessPartnerName: bidder.businessPartnerName || bid?.businessPartnerName || '',
          bidNumber: bid?.bidNumber || '',
          totalBidAmount: bid?.totalBidAmount || 0,
          currency: getBidCurrency(bidder.tenderBidId),
        };
      }) || [];
    }

    return recommendation.bidRecommendations
      .filter(bid => selectedBidsForVerification.includes(bid.bidId))
      .map(bid => ({
        bidId: bid.bidId,
        businessPartnerId: bid.businessPartnerId || '',
        businessPartnerName: bid.businessPartnerName,
        bidNumber: bid.bidNumber,
        totalBidAmount: bid.totalBidAmount,
        currency: getBidCurrency(bid.bidId),
      }));
  };

  const readinessAllowsBid = (bidId: string) =>
    Boolean(
      bidId &&
        readinessDecision?.isReady &&
        readinessDecision.isCurrent &&
        hasAwardReadinessAction(
          readinessDecision.allowedActions,
          'RecordAward'
        ) &&
        canSubmitAwardRecommendation &&
        readinessDecision.recommendation.subjectIds.includes(bidId)
    );

  if (loading) {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (existingAward &&
      !isFinalTenderAward(existingAward.status) &&
      existingAward.status !== 'Rejected') {
    const isPendingApproval = existingAward.status === 'PendingApproval';
    return (
      <Card className={isPendingApproval ? 'border-amber-200 bg-amber-50' : 'border-red-200 bg-red-50'}>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle>
                {isPendingApproval
                  ? 'Award recommendation awaiting approval'
                  : 'Award cancelled'}
              </CardTitle>
              <CardDescription>
                {isPendingApproval
                  ? 'An independent user with tender approval permission must approve or reject this recommendation.'
                  : 'This award is no longer active.'}
              </CardDescription>
            </div>
            <Badge variant={isPendingApproval ? 'outline' : 'destructive'}>
              {existingAward.status}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <Label className="text-muted-foreground">Recommended supplier</Label>
              <p className="font-medium">{existingAward.businessPartnerName}</p>
            </div>
            <div>
              <Label className="text-muted-foreground">Recommended amount</Label>
              <p className="font-medium">
                {formatProcurementMoney(existingAward.awardedAmount, existingAward.currency || tenderCurrency)}
              </p>
            </div>
          </div>
          <Button asChild variant="outline">
            <Link href={`/procurement/awards/${existingAward.id}`}>View recommendation details</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  // Show existing award - Fancy celebration display
  if (existingAward && isFinalTenderAward(existingAward.status)) {
    return (
      <div className="space-y-6">
        {/* Hero Award Banner */}
        <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-emerald-500 via-green-500 to-teal-600 p-8 text-white shadow-2xl">
          {/* Decorative elements */}
          <div className="absolute top-0 right-0 -mt-4 -mr-4 h-40 w-40 rounded-full bg-white/10 blur-3xl" />
          <div className="absolute bottom-0 left-0 -mb-8 -ml-8 h-32 w-32 rounded-full bg-white/10 blur-2xl" />
          <div className="absolute top-1/2 right-10 h-20 w-20 rounded-full bg-yellow-400/20 blur-xl" />

          {/* Sparkle/confetti decorations */}
          <div className="absolute top-4 left-8 text-4xl animate-bounce" style={{ animationDelay: '0s' }}>✨</div>
          <div className="absolute top-8 right-20 text-3xl animate-bounce" style={{ animationDelay: '0.2s' }}>🎉</div>
          <div className="absolute bottom-6 right-8 text-3xl animate-bounce" style={{ animationDelay: '0.4s' }}>⭐</div>
          <div className="absolute bottom-12 left-16 text-2xl animate-bounce" style={{ animationDelay: '0.3s' }}>🎊</div>

          <div className="relative z-10 text-center">
            {/* Large trophy */}
            <div className="mx-auto mb-4 flex h-24 w-24 items-center justify-center rounded-full bg-gradient-to-br from-yellow-300 to-yellow-500 shadow-lg ring-4 ring-white/30">
              <Trophy className="h-14 w-14 text-yellow-900" />
            </div>

            <h2 className="text-3xl font-bold tracking-tight mb-2">
              Tender Awarded!
            </h2>
            <p className="text-lg text-white/90 mb-1">Contract Successfully Awarded</p>

            <Badge className="mt-3 bg-white/20 text-white border-white/30 hover:bg-white/30 text-sm px-4 py-1">
              <CheckCircle2 className="h-4 w-4 mr-2" />
              {existingAward.status}
            </Badge>
          </div>
        </div>

        {/* Winner Card */}
        <Card className="border-2 border-emerald-200 shadow-lg overflow-hidden">
          <div className="bg-gradient-to-r from-emerald-50 to-teal-50 px-6 py-4 border-b border-emerald-100">
            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-gradient-to-br from-yellow-400 to-amber-500 shadow-md">
                <Trophy className="h-6 w-6 text-white" />
              </div>
              <div>
                <p className="text-sm font-medium text-emerald-700">Awarded To</p>
                <h3 className="text-xl font-bold text-emerald-900">
                  {existingAward.businessPartnerName}
                </h3>
              </div>
            </div>
          </div>

          <CardContent className="p-6">
            {/* Award Amount - Highlighted */}
            <div className="mb-6 rounded-xl bg-gradient-to-r from-amber-50 via-yellow-50 to-amber-50 border-2 border-amber-200 p-6 text-center">
              <p className="text-sm font-medium text-amber-700 uppercase tracking-wider mb-1">Award Amount</p>
              <p className="text-4xl font-bold text-amber-900">
                {formatProcurementMoney(existingAward.awardedAmount, existingAward.currency || tenderCurrency)}
              </p>
            </div>

            {/* Award Details Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="rounded-lg bg-gray-50 p-4 border">
                <div className="flex items-center gap-2 mb-1">
                  <div className="h-8 w-8 rounded-full bg-blue-100 flex items-center justify-center">
                    <FileText className="h-4 w-4 text-blue-600" />
                  </div>
                  <p className="text-sm text-gray-500">Award Date</p>
                </div>
                <p className="text-lg font-semibold text-gray-900 ml-10">
                  {format(new Date(existingAward.awardDate), 'MMMM dd, yyyy')}
                </p>
              </div>

              <div className="rounded-lg bg-gray-50 p-4 border">
                <div className="flex items-center gap-2 mb-1">
                  <div className="h-8 w-8 rounded-full bg-purple-100 flex items-center justify-center">
                    <CheckCircle2 className="h-4 w-4 text-purple-600" />
                  </div>
                  <p className="text-sm text-gray-500">Awarded By</p>
                </div>
                <p className="text-lg font-semibold text-gray-900 ml-10">
                  {existingAward.awardedByName || 'N/A'}
                </p>
              </div>
            </div>

            {/* Justification */}
            {existingAward.awardJustification && (
              <div className="mt-6 rounded-lg border border-emerald-200 bg-emerald-50/50 p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="h-8 w-8 rounded-full bg-emerald-100 flex items-center justify-center">
                    <FileText className="h-4 w-4 text-emerald-600" />
                  </div>
                  <Label className="text-emerald-800 font-semibold">Award Justification</Label>
                </div>
                <p className="text-gray-700 whitespace-pre-wrap leading-relaxed pl-10">
                  {existingAward.awardJustification}
                </p>
              </div>
            )}

            {/* Notes */}
            {existingAward.notes && (
              <div className="mt-4 rounded-lg border bg-gray-50 p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="h-8 w-8 rounded-full bg-gray-200 flex items-center justify-center">
                    <FileText className="h-4 w-4 text-gray-600" />
                  </div>
                  <Label className="text-gray-700 font-semibold">Additional Notes</Label>
                </div>
                <p className="text-gray-600 whitespace-pre-wrap leading-relaxed pl-10">
                  {existingAward.notes}
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    );
  }

  // Show recommendation and award form
  if (!recommendation) {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="text-center py-8 text-gray-500">
            <AlertCircle className="h-12 w-12 mx-auto mb-4 opacity-50" />
            <p>No award recommendation available.</p>
            <p className="text-sm mt-2">Ensure bids have been evaluated before awarding.</p>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <>
      <div className="space-y-6">
        {existingAward?.status === 'Rejected' && (
          <Card className="border-red-200 bg-red-50">
            <CardHeader>
              <CardTitle>Award recommendation rejected</CardTitle>
              <CardDescription>
                Review the recorded decision, then submit a corrected recommendation for independent approval.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Button asChild variant="outline">
                <Link href={`/procurement/awards/${existingAward.id}`}>View rejection details</Link>
              </Button>
            </CardContent>
          </Card>
        )}

        <Card
          className={
            readinessDecision?.isReady && readinessDecision.isCurrent
              ? 'border-emerald-300 bg-emerald-50/50'
              : 'border-amber-300 bg-amber-50/50'
          }
          data-testid="tender-award-readiness-gate"
        >
          <CardHeader>
            <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <ClipboardCheck className="h-5 w-5" />
                  Server award-readiness gate
                </CardTitle>
                <CardDescription>
                  {readinessDecision
                    ? `${readinessDecision.status}${
                        readinessDecision.isCurrent ? ' · current' : ' · stale'
                      } · decision #${readinessDecision.decisionSequence}`
                    : 'No immutable readiness decision has been retained.'}
                </CardDescription>
              </div>
              <Button asChild variant="outline">
                <Link
                  href={`/procurement/tenders/${tenderId}/award-readiness`}
                >
                  Review award readiness
                </Link>
              </Button>
            </div>
          </CardHeader>
          {readinessDecision?.blockedReasons.length ? (
            <CardContent>
              <ul className="list-disc space-y-1 pl-5 text-sm text-amber-900">
                {readinessDecision.blockedReasons.map((reason, index) => (
                  <li key={`${reason}-${index}`}>{reason}</li>
                ))}
              </ul>
            </CardContent>
          ) : null}
        </Card>

        {/* Recommendation Summary */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <TrendingUp className="h-5 w-5 text-blue-600" />
              Award Recommendation
            </CardTitle>
            <CardDescription>
              Based on evaluation scores and bid amounts
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                <p className="text-sm text-gray-600">Total Bids</p>
                <p className="text-2xl font-bold text-blue-900">{recommendation.totalBids}</p>
              </div>
              <div className="p-4 bg-green-50 border border-green-200 rounded-lg">
                <p className="text-sm text-gray-600">Evaluated Bids</p>
                <p className="text-2xl font-bold text-green-900">{recommendation.evaluatedBids}</p>
              </div>
              <div className="p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
                <p className="text-sm text-gray-600">Recommended Amount</p>
                <p className="text-2xl font-bold text-yellow-900">
                  {formatProcurementMoney(recommendation.recommendedAmount, getBidCurrency(recommendation.recommendedBidId))}
                </p>
              </div>
            </div>

            {recommendation.recommendedBidId && (
              <div className="p-4 bg-green-50 border-2 border-green-300 rounded-lg">
                <div className="flex items-center gap-2 mb-2">
                  <Trophy className="h-5 w-5 text-green-600" />
                  <span className="font-semibold text-green-900">Recommended Winner</span>
                </div>
                <p className="text-lg font-bold text-green-900">{recommendation.recommendedBusinessPartner}</p>
                <p className="text-sm text-gray-600">
                  Bid: {recommendation.recommendedBidNumber} | Score: {recommendation.recommendedScore.toFixed(2)}
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Bid Comparison */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Bid Comparison</CardTitle>
                <CardDescription>Select bidders to verify before awarding the tender</CardDescription>
              </div>
              {(selectedBidsForVerification.length > 0 || existingVerification) && (
                <Button onClick={handleStartVerification} variant="outline">
                  <ClipboardCheck className="h-4 w-4 mr-2" />
                  {existingVerification && selectedBidsForVerification.length === 0
                    ? `View Verification (${existingVerification.bidders?.length || 0})`
                    : `Verify Selected (${selectedBidsForVerification.length})`}
                </Button>
              )}
            </div>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-12">
                    <Checkbox
                      checked={selectedBidsForVerification.length === recommendation.bidRecommendations.length && recommendation.bidRecommendations.length > 0}
                      onCheckedChange={(checked) => {
                        if (checked) {
                          setSelectedBidsForVerification(recommendation.bidRecommendations.map(b => b.bidId));
                        } else {
                          setSelectedBidsForVerification([]);
                        }
                      }}
                    />
                  </TableHead>
                  <TableHead>Rank</TableHead>
                  <TableHead>Business Partner</TableHead>
                  <TableHead>Bid Number</TableHead>
                  <TableHead>Bid Amount</TableHead>
                  <TableHead>Avg. Score</TableHead>
                  <TableHead>Evaluations</TableHead>
                  <TableHead>Recommendations</TableHead>
                  <TableHead>Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {recommendation.bidRecommendations
                  .sort((a, b) => b.averageScore - a.averageScore)
                  .map((bid, index) => (
                    <TableRow key={bid.bidId} className={bid.bidId === recommendation.recommendedBidId ? 'bg-green-50' : ''}>
                      <TableCell>
                        <Checkbox
                          checked={selectedBidsForVerification.includes(bid.bidId)}
                          onCheckedChange={() => toggleBidForVerification(bid.bidId)}
                        />
                      </TableCell>
                      <TableCell>
                        {index === 0 && <Trophy className="h-5 w-5 text-yellow-600" />}
                        {index > 0 && <span className="text-gray-500">#{index + 1}</span>}
                      </TableCell>
                      <TableCell className="font-medium">{bid.businessPartnerName}</TableCell>
                      <TableCell>{bid.bidNumber}</TableCell>
                      <TableCell>{formatProcurementMoney(bid.totalBidAmount, getBidCurrency(bid.bidId))}</TableCell>
                      <TableCell>
                        <Badge variant={bid.averageScore >= 80 ? 'default' : bid.averageScore >= 60 ? 'secondary' : 'outline'}>
                          {bid.averageScore.toFixed(2)}
                        </Badge>
                      </TableCell>
                      <TableCell>{bid.evaluationCount}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          {bid.recommendationCount > 0 ? (
                            <Badge variant="default" className="bg-green-600 hover:bg-green-700">
                              <CheckCircle2 className="h-3 w-3 mr-1" />
                              {bid.recommendationCount} / {bid.totalEvaluators}
                            </Badge>
                          ) : (
                            <Badge variant="outline" className="text-gray-500">
                              0 / {bid.totalEvaluators}
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        {canSubmitAwardRecommendation ? (
                          <Button
                            size="sm"
                            onClick={() => handleSelectBid(bid.bidId, bid.totalBidAmount)}
                            variant={bid.bidId === recommendation.recommendedBidId ? 'default' : 'outline'}
                            disabled={!readinessAllowsBid(bid.bidId)}
                          >
                            <Award className="h-4 w-4 mr-2" />
                            Recommend
                          </Button>
                        ) : (
                          <span className="text-sm text-muted-foreground">View only</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

      {/* Award Confirmation Dialog */}
      <ConfirmationDialog
        open={showAwardDialog}
        onOpenChange={setShowAwardDialog}
        title="Submit Award Recommendation"
        description="Confirm the recommendation details for independent approval."
        onConfirm={handleCreateAward}
        confirmText={submitting ? 'Submitting...' : 'Submit Recommendation'}
        confirmDisabled={submitting || !readinessAllowsBid(selectedBidId)}
        maxWidth="600px"
      >
        <div className="space-y-4">
          <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
            <p className="text-sm text-gray-600">Tender</p>
            <p className="font-semibold">{tenderNumber} - {tenderTitle}</p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="awardAmount">Award Amount ({normalizeProcurementCurrency(tenderCurrency)}) *</Label>
            <div className="flex items-center gap-2">
              <Input
                id="awardAmount"
                type="number"
                step="0.01"
                value={awardAmount}
                onChange={(e) => setAwardAmount(e.target.value)}
                placeholder="Enter award amount"
                disabled={submitting}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="justification">Award Justification</Label>
            <Textarea
              id="justification"
              value={awardJustification}
              onChange={(e) => setAwardJustification(e.target.value)}
              placeholder="Provide justification for this award decision..."
              rows={4}
              disabled={submitting}
            />
          </div>

          <div className="p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
            <div className="flex items-start gap-2">
              <AlertCircle className="h-5 w-5 text-yellow-600 mt-0.5" />
              <div className="text-sm text-yellow-800">
                <p className="font-semibold">Important:</p>
                <p>This submits a recommendation only. The tender and bids are updated after an independent approver accepts it.</p>
              </div>
            </div>
          </div>
        </div>
      </ConfirmationDialog>

      {/* Award Verification Dialog */}
      <AwardVerificationDialog
        open={showVerificationDialog}
        onOpenChange={setShowVerificationDialog}
        tenderId={tenderId}
        tenderCurrency={tenderCurrency}
        bidders={getSelectedBiddersInfo()}
        onVerificationComplete={handleVerificationComplete}
        onAwardBidder={(bidId, bidAmount) => handleSelectBid(bidId, bidAmount)}
      />
    </>
  );
}

