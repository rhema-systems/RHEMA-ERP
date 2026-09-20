'use client';

import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Loader2, CheckCircle2, XCircle, AlertCircle, ClipboardCheck, Building2, MinusCircle, Award } from 'lucide-react';
import { formatProcurementMoney } from '@/lib/procurement-currency';
import { getAwardBidderReviewStatus, isAwardVerificationClosed } from '@/lib/award-verification-status';
import { hasAwardVerificationEvidence } from '@/lib/award-verification-evidence';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { toast } from 'sonner';
import {
  awardVerificationService,
  AwardVerificationChecklistTemplate,
  TenderAwardVerification,
  TenderAwardVerificationBidder,
  TenderAwardVerificationItemDocument,
  VerifyItemDto
} from '@/services/awardVerificationService';
import { VerificationItemDocuments } from './VerificationItemDocuments';

interface BidderInfo {
  bidId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  bidNumber: string;
  totalBidAmount: number;
  currency?: string;
}

interface AwardVerificationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenderId: string;
  tenderCurrency?: string;
  bidders: BidderInfo[];
  onVerificationComplete?: (verification: TenderAwardVerification) => void;
  onAwardBidder?: (bidId: string, bidAmount: number) => void;
}

interface ItemVerification {
  itemResultId: string;
  checklistItemId: string;
  itemText: string;
  description?: string;
  isRequired: boolean;
  requiresDocument?: boolean;
  isVerified: boolean;
  status: string;
  comments: string;
  documents: TenderAwardVerificationItemDocument[];
}

interface BidderVerification {
  bidderId: string;
  bidId: string;
  businessPartnerId: string;
  businessPartnerName: string;
  bidNumber: string;
  overallComments: string;
  items: ItemVerification[];
  status: 'pending' | 'verified' | 'failed';
}

export function AwardVerificationDialog({
  open,
  onOpenChange,
  tenderId,
  tenderCurrency,
  bidders,
  onVerificationComplete,
  onAwardBidder,
}: AwardVerificationDialogProps) {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [template, setTemplate] = useState<AwardVerificationChecklistTemplate | null>(null);
  const [verification, setVerification] = useState<TenderAwardVerification | null>(null);
  const [bidderVerifications, setBidderVerifications] = useState<BidderVerification[]>([]);
  const [activeTab, setActiveTab] = useState<string>('');
  const readOnly = isAwardVerificationClosed(verification?.status);

  useEffect(() => {
    if (open && tenderId && bidders.length > 0) {
      loadVerificationData();
    }
  }, [open, tenderId, bidders]);

  const loadVerificationData = async () => {
    try {
      setLoading(true);
      setLoadError(null);
      setVerification(null);
      setBidderVerifications([]);
      
      // Check for existing verification
      let existingVerification = await awardVerificationService.getVerificationByTender(tenderId);
      
      if (!existingVerification) {
        // Get default template
        const defaultTemplate = await awardVerificationService.getDefaultTemplate();
        if (!defaultTemplate) {
          toast.error('No verification checklist template found. Please create one in Administration.');
          onOpenChange(false);
          return;
        }
        setTemplate(defaultTemplate);
        
        // Start new verification
        existingVerification = await awardVerificationService.startVerification({
          tenderId,
          templateId: defaultTemplate.id,
          selectedBidIds: bidders.map(b => b.bidId),
        });
      }
      
      setVerification(existingVerification);
      
      // Initialize bidder verifications from existing data or template
      const initialBidderVerifications: BidderVerification[] = bidders.map(bidder => {
        const existingBidder = existingVerification?.bidders?.find(
          b => b.tenderBidId === bidder.bidId
        );
        
        if (existingBidder) {
          return {
            bidderId: existingBidder.id,
            bidId: bidder.bidId,
            businessPartnerId: bidder.businessPartnerId,
            businessPartnerName: bidder.businessPartnerName,
            bidNumber: bidder.bidNumber,
            overallComments: existingBidder.overallComments || '',
            status: getAwardBidderReviewStatus(existingBidder.status),
            items: existingBidder.itemResults?.map(item => ({
              itemResultId: item.id,
              checklistItemId: item.checklistItemId,
              itemText: item.checklistItemText || item.itemText || '',
              description: item.itemDescription || '',
              isRequired: item.isRequired ?? true,
              requiresDocument: item.requiresDocument ?? false,
              isVerified: item.isVerified,
              status: item.status,
              comments: item.comments || '',
              documents: item.documents || [],
            })) || [],
          };
        }

        // New bidder - use template items (this shouldn't happen as StartVerification creates item results)
        const templateToUse = template || existingVerification?.bidders?.[0]?.itemResults?.map(i => ({
          id: i.checklistItemId,
          itemText: i.checklistItemText || i.itemText || '',
          description: i.itemDescription || '',
          isRequired: i.isRequired ?? true,
        }));

        return {
          bidderId: '',
          bidId: bidder.bidId,
          businessPartnerId: bidder.businessPartnerId,
          businessPartnerName: bidder.businessPartnerName,
          bidNumber: bidder.bidNumber,
          overallComments: '',
          status: 'pending' as const,
          items: (template?.items || []).map(item => ({
            itemResultId: '', // Will be populated when verification starts
            checklistItemId: item.id,
            itemText: item.itemText,
            description: item.description,
            isRequired: item.isRequired,
            requiresDocument: item.requiresDocument ?? false,
            isVerified: false,
            status: 'Pending',
            comments: '',
            documents: [],
          })),
        };
      });
      
      setBidderVerifications(initialBidderVerifications);
      if (initialBidderVerifications.length > 0) {
        setActiveTab(initialBidderVerifications[0].bidId);
      }
    } catch (error: any) {
      console.error('Error loading verification data:', error);
      let problem: unknown = error;
      try { problem = JSON.parse(error?.message); } catch { /* Retain non-JSON errors. */ }
      const message = getProcurementProblemMessage(problem, 'Failed to load verification data');
      setLoadError(message);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  const handleItemChange = (bidId: string, itemId: string, field: 'isVerified' | 'comments' | 'status', value: any) => {
    setBidderVerifications(prev => prev.map(bv => {
      if (bv.bidId !== bidId) return bv;
      return {
        ...bv,
        items: bv.items.map(item => {
          if (item.checklistItemId !== itemId) return item;
          // If setting status, also update isVerified
          if (field === 'status') {
            return {
              ...item,
              status: value,
              isVerified: value === 'Passed' || value === 'Failed' || value === 'NotApplicable'
            };
          }
          return { ...item, [field]: value };
        }),
      };
    }));
  };

  const handleVerifyItem = (bidId: string, itemId: string, status: 'Passed' | 'Failed' | 'NotApplicable') => {
    handleItemChange(bidId, itemId, 'status', status);
  };

  const handleOverallCommentsChange = (bidId: string, comments: string) => {
    setBidderVerifications(prev => prev.map(bv =>
      bv.bidId === bidId ? { ...bv, overallComments: comments } : bv
    ));
  };

  const handleDocumentsChange = (bidId: string, itemId: string, documents: TenderAwardVerificationItemDocument[]) => {
    setBidderVerifications(prev => prev.map(bv => {
      if (bv.bidId !== bidId) return bv;
      return {
        ...bv,
        items: bv.items.map(item => {
          if (item.checklistItemId !== itemId) return item;
          return { ...item, documents };
        }),
      };
    }));
  };

  const handleVerifyBidder = async (bidId: string) => {
    if (!verification || readOnly || loading || loadError || saving) return;

    const bidderVerification = bidderVerifications.find(bv => bv.bidId === bidId);
    if (!bidderVerification || bidderVerification.status === 'verified') return;

    // Check required items - must be Passed or Failed (not Pending or N/A)
    const unverifiedRequired = bidderVerification.items.filter(
      item => item.isRequired && item.status !== 'Passed' && item.status !== 'Failed'
    );

    if (unverifiedRequired.length > 0) {
      toast.error(`Please verify all required items (${unverifiedRequired.length} remaining)`);
      return;
    }

    const missingEvidence = bidderVerification.items.filter(item => !hasAwardVerificationEvidence(item));
    if (missingEvidence.length > 0) {
      toast.error(missingEvidence.map(item => `${item.itemText}: attach the required document`).join('; '));
      return;
    }

    // Check if all items have been actioned
    const pendingItems = bidderVerification.items.filter(item => item.status === 'Pending');
    if (pendingItems.length > 0) {
      toast.error(`Please verify all items. ${pendingItems.length} item(s) still pending.`);
      return;
    }

    try {
      setSaving(true);

      const itemResults: VerifyItemDto[] = bidderVerification.items.map(item => ({
        checklistItemId: item.checklistItemId,
        isVerified: item.status === 'Passed' || item.status === 'NotApplicable',
        status: item.status,
        comments: item.comments || undefined,
      }));

      const existingBidder = verification.bidders?.find(b => b.tenderBidId === bidId);
      if (!existingBidder) {
        toast.error('Bidder not found in verification');
        return;
      }

      const savedBidder = await awardVerificationService.verifyBidder(verification.id, {
        bidderId: existingBidder.id,
        overallComments: bidderVerification.overallComments || undefined,
        itemResults,
      });

      // Use the persisted outcome: a failed review must not turn green locally.
      const savedStatus = getAwardBidderReviewStatus(savedBidder.status);
      setBidderVerifications(prev => prev.map(bv =>
        bv.bidId === bidId ? { ...bv, status: savedStatus } : bv
      ));

      if (savedStatus === 'verified') {
        toast.success(`${bidderVerification.businessPartnerName} verified successfully`);
      } else {
        toast.warning(`${bidderVerification.businessPartnerName}: ${savedBidder.status}`);
      }
    } catch (error: any) {
      console.error('Error verifying bidder:', error);
      let problem: unknown = error;
      try { problem = JSON.parse(error?.message); } catch { /* Keep plain errors. */ }
      toast.error(getProcurementProblemMessage(problem, 'Failed to verify bidder'));
    } finally {
      setSaving(false);
    }
  };

  const handleCompleteVerification = async () => {
    if (!verification || readOnly || loading || loadError || saving || bidderVerifications.length === 0) return;
    if (bidderVerifications.some(bidder => bidder.items.some(item => !hasAwardVerificationEvidence(item)))) {
      toast.error('Required evidence is missing. Review the checklist before completing verification.');
      return;
    }

    const unverifiedBidders = bidderVerifications.filter(bv => bv.status !== 'verified');
    if (unverifiedBidders.length > 0) {
      toast.error(`Please verify all bidders (${unverifiedBidders.length} remaining)`);
      return;
    }

    try {
      setSaving(true);
      const completed = await awardVerificationService.completeVerification(
        verification.id,
        'Verification completed for all selected bidders'
      );
      setVerification(completed);
      toast.success('Verification completed successfully');
      onVerificationComplete?.(completed);
      onOpenChange(false);
    } catch (error: any) {
      console.error('Error completing verification:', error);
      let problem: unknown = error;
      try { problem = JSON.parse(error?.message); } catch { /* Keep plain errors. */ }
      toast.error(getProcurementProblemMessage(problem, 'Failed to complete verification'));
    } finally {
      setSaving(false);
    }
  };

  const getVerificationProgress = () => {
    const verified = bidderVerifications.filter(bv => bv.status === 'verified').length;
    return { verified, total: bidderVerifications.length };
  };

  const progress = getVerificationProgress();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[900px] max-h-[90vh]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ClipboardCheck className="h-5 w-5" />
            Award Verification Checklist
          </DialogTitle>
          <DialogDescription>
            Verify background checks for selected bidders before awarding the tender.
            Progress: {progress.verified} of {progress.total} bidders verified.
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        ) : loadError ? (
          <div className="space-y-4">
            <p role="alert" className="text-sm text-destructive">{loadError}</p>
            <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
          </div>
        ) : (
          <div className="space-y-4">
            <Tabs value={activeTab} onValueChange={setActiveTab}>
              <TabsList className="w-full justify-start overflow-x-auto">
                {bidderVerifications.map((bv) => (
                  <TabsTrigger key={bv.bidId} value={bv.bidId} className="flex items-center gap-2">
                    <Building2 className="h-4 w-4" />
                    <span className="max-w-[150px] truncate">{bv.businessPartnerName}</span>
                    {bv.status === 'verified' && <CheckCircle2 className="h-4 w-4 text-green-600" />}
                    {bv.status === 'failed' && <XCircle className="h-4 w-4 text-red-600" />}
                    {bv.status === 'pending' && <AlertCircle className="h-4 w-4 text-yellow-600" />}
                  </TabsTrigger>
                ))}
              </TabsList>

              {bidderVerifications.map((bv) => (
                <TabsContent key={bv.bidId} value={bv.bidId} className="mt-4">
                  <Card>
                    <CardHeader className="pb-3">
                      <div className="flex items-center justify-between">
                        <CardTitle className="text-lg">{bv.businessPartnerName}</CardTitle>
                        <Badge variant={bv.status === 'verified' ? 'default' : 'outline'}>
                          {bv.status === 'verified' ? 'Verified' : bv.status === 'failed' ? 'Failed' : 'Pending'}
                        </Badge>
                      </div>
                      <p className="text-sm text-muted-foreground">Bid: {bv.bidNumber}</p>
                    </CardHeader>
                    <CardContent>
                      <ScrollArea className="h-[350px] pr-4">
                        <div className="space-y-4">
                          {bv.items.map((item, index) => (
                            <div key={item.checklistItemId} className={`border rounded-lg p-3 space-y-3 ${
                              item.status === 'Passed' ? 'border-green-200 bg-green-50/50' :
                              item.status === 'Failed' ? 'border-red-200 bg-red-50/50' :
                              item.status === 'NotApplicable' ? 'border-gray-200 bg-gray-50/50' :
                              ''
                            }`}>
                              {/* Header with item text and status badge */}
                              <div className="flex items-start justify-between gap-3">
                                <div className="flex-1">
                                  <div className="flex items-center gap-2">
                                    <span className="font-medium">
                                      {index + 1}. {item.itemText}
                                    </span>
                                    {item.isRequired && <span className="text-red-500">*</span>}
                                    {item.status && item.status !== 'Pending' && (
                                      <Badge
                                        variant={item.status === 'Passed' ? 'default' : item.status === 'Failed' ? 'destructive' : 'secondary'}
                                        className={item.status === 'Passed' ? 'bg-green-600' : ''}
                                      >
                                        {item.status === 'Passed' && <CheckCircle2 className="h-3 w-3 mr-1" />}
                                        {item.status === 'Failed' && <XCircle className="h-3 w-3 mr-1" />}
                                        {item.status === 'NotApplicable' && <MinusCircle className="h-3 w-3 mr-1" />}
                                        {item.status === 'NotApplicable' ? 'N/A' : item.status}
                                      </Badge>
                                    )}
                                  </div>
                                  {item.description && (
                                    <p className="text-sm text-muted-foreground mt-1">{item.description}</p>
                                  )}
                                </div>
                              </div>

                              {/* Verify Buttons */}
                              <div className="flex items-center gap-2">
                                <span className="text-sm text-muted-foreground mr-2">Verify:</span>
                                <Button
                                  size="sm"
                                  variant={item.status === 'Passed' ? 'default' : 'outline'}
                                  className={item.status === 'Passed' ? 'bg-green-600 hover:bg-green-700' : ''}
                                  onClick={() => handleVerifyItem(bv.bidId, item.checklistItemId, 'Passed')}
                                  disabled={readOnly || saving || bv.status === 'verified'}
                                >
                                  <CheckCircle2 className="h-4 w-4 mr-1" />
                                  Pass
                                </Button>
                                <Button
                                  size="sm"
                                  variant={item.status === 'Failed' ? 'destructive' : 'outline'}
                                  onClick={() => handleVerifyItem(bv.bidId, item.checklistItemId, 'Failed')}
                                  disabled={readOnly || saving || bv.status === 'verified'}
                                >
                                  <XCircle className="h-4 w-4 mr-1" />
                                  Fail
                                </Button>
                                <Button
                                  size="sm"
                                  variant={item.status === 'NotApplicable' ? 'secondary' : 'outline'}
                                  onClick={() => handleVerifyItem(bv.bidId, item.checklistItemId, 'NotApplicable')}
                                  disabled={readOnly || saving || bv.status === 'verified' || item.isRequired}
                                  title={item.isRequired ? 'Required items cannot be marked as N/A' : ''}
                                >
                                  <MinusCircle className="h-4 w-4 mr-1" />
                                  N/A
                                </Button>
                              </div>

                              {/* Comments */}
                              <Label htmlFor={`review-comments-${bv.bidId}-${item.checklistItemId}`}>
                                Review comments (optional)
                              </Label>
                              <Textarea
                                id={`review-comments-${bv.bidId}-${item.checklistItemId}`}
                                placeholder="Add context if needed"
                                value={item.comments}
                                onChange={(e) =>
                                  handleItemChange(bv.bidId, item.checklistItemId, 'comments', e.target.value)
                                }
                                disabled={readOnly || saving || bv.status === 'verified'}
                                rows={2}
                              />
                              <p className="text-xs text-muted-foreground">
                                {item.requiresDocument
                                  ? 'Document required: retain the supporting file for this check.'
                                  : 'Comments and attachments are optional. Click Verify Bidder to save your decisions.'}
                              </p>

                              {/* Document Attachments */}
                              {item.itemResultId && (
                                <VerificationItemDocuments
                                  itemResultId={item.itemResultId}
                                  documents={item.documents}
                                  disabled={readOnly || saving || bv.status === 'verified'}
                                  onDocumentsChange={(docs) =>
                                    handleDocumentsChange(bv.bidId, item.checklistItemId, docs)
                                  }
                                />
                              )}
                            </div>
                          ))}

                          <div className="border-t pt-4 mt-4">
                            <Label>Overall comments (optional)</Label>
                            <Textarea
                              placeholder="Overall verification comments for this bidder..."
                              value={bv.overallComments}
                              onChange={(e) => handleOverallCommentsChange(bv.bidId, e.target.value)}
                              disabled={readOnly || saving || bv.status === 'verified'}
                              className="mt-2"
                              rows={3}
                            />
                          </div>
                        </div>
                      </ScrollArea>

                      <div className="flex justify-between items-center mt-4 pt-4 border-t">
                        {/* Show bid amount */}
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                          <span>Bid Amount: <strong className="text-foreground">{formatProcurementMoney(
                            bidders.find(b => b.bidId === bv.bidId)?.totalBidAmount ?? 0,
                            bidders.find(b => b.bidId === bv.bidId)?.currency || tenderCurrency
                          )}</strong></span>
                        </div>

                        <div className="flex gap-2">
                          {!readOnly && bv.status !== 'verified' && (
                            <Button onClick={() => handleVerifyBidder(bv.bidId)} disabled={saving}>
                              {saving ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <CheckCircle2 className="h-4 w-4 mr-2" />}
                              Verify Bidder
                            </Button>
                          )}

                          {/* Award button - enabled only when bidder is verified */}
                          {onAwardBidder && verification?.status === 'Completed' && (
                            <Button
                              onClick={() => {
                                const bidAmount = bidders.find(b => b.bidId === bv.bidId)?.totalBidAmount || 0;
                                onAwardBidder(bv.bidId, bidAmount);
                                onOpenChange(false);
                              }}
                              disabled={bv.status !== 'verified'}
                              variant={bv.status === 'verified' ? 'default' : 'outline'}
                              title={bv.status !== 'verified' ? 'Bidder must be verified before awarding' : ''}
                            >
                              <Award className="h-4 w-4 mr-2" />
                              Award
                            </Button>
                          )}
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </TabsContent>
              ))}
            </Tabs>

            <div className="flex items-center justify-between pt-4 border-t">
              <div className="text-sm text-muted-foreground">
                {readOnly ? (
                  <span className="font-medium">Verification {verification?.status.toLowerCase()} · read-only</span>
                ) : progress.total > 0 && progress.verified === progress.total ? (
                  <span className="text-green-600 font-medium">All bidders verified!</span>
                ) : (
                  <span>{progress.total - progress.verified} bidder(s) remaining</span>
                )}
              </div>
              <div className="flex gap-2">
                <Button variant="outline" onClick={() => onOpenChange(false)}>{readOnly ? 'Close' : 'Cancel'}</Button>
                {!readOnly && <Button
                  onClick={handleCompleteVerification}
                  disabled={saving || !verification || progress.total === 0 || progress.verified !== progress.total ||
                    bidderVerifications.some(bidder => bidder.items.some(item => !hasAwardVerificationEvidence(item)))}
                >
                  {saving ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : null}
                  Complete Verification
                </Button>}
              </div>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

