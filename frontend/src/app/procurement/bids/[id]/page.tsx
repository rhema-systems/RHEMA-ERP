'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ArrowLeft, FileText, Package, Upload, Award, MessageSquare, Clock, XCircle, CheckCircle2, CheckCircle, Download, DollarSign, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderBidService from '@/services/tenderBidService';
import { evaluationTemplateService, type EvaluationTemplate } from '@/services/evaluationTemplateService';
import { type TenderBidDetailDto, type TenderPaymentDto } from '@/services/tenderBidService';
import { format } from 'date-fns';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';

// Interface for criteria scores stored in evaluationCriteriaJson
interface CriteriaScore {
  criterionId: string;
  criterionName: string;
  criterionCode: string;
  score: number;
  weight: number;
  maxScore: number;
  weightedScore: number;
}

export default function BidDetailPage() {
  const params = useParams();
  const router = useRouter();
  const bidId = params.id as string;

  const [bid, setBid] = useState<TenderBidDetailDto | null>(null);
  const [payments, setPayments] = useState<TenderPaymentDto[]>([]);
  const [template, setTemplate] = useState<EvaluationTemplate | null>(null);
  const [loading, setLoading] = useState(true);
  const [opening, setOpening] = useState(false);
  const [showOpenDialog, setShowOpenDialog] = useState(false);
  const [activeTab, setActiveTab] = useState('overview');

  useEffect(() => {
    if (bidId) {
      loadBidDetails();
    }
  }, [bidId]);

  const loadBidDetails = async () => {
    try {
      setLoading(true);
      const data = await tenderBidService.getBidById(bidId);
      setBid(data);

      // Load evaluation template if assigned
      if (data.evaluationTemplateId) {
        try {
          const templateData = await evaluationTemplateService.getById(data.evaluationTemplateId);
          setTemplate(templateData);
        } catch (templateError) {
          console.error('Error loading evaluation template:', templateError);
        }
      }

      // Load payments
      const paymentsData = await tenderBidService.getBidPayments(bidId);
      setPayments(paymentsData);
    } catch (error) {
      console.error('Error loading bid details:', error);
      toast.error('Failed to load bid details');
    } finally {
      setLoading(false);
    }
  };

  const handleOpenBid = () => {
    setShowOpenDialog(true);
  };

  const confirmOpenBid = async () => {
    if (!bid) return;

    try {
      setOpening(true);
      const updatedBid = await tenderBidService.openBid(bidId);
      setBid(updatedBid);
      setShowOpenDialog(false);
      toast.success('Bid marked as opened');
    } catch (error) {
      console.error('Error opening bid:', error);
      toast.error('Failed to open bid');
    } finally {
      setOpening(false);
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Submitted': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Opened': { variant: 'default', className: 'bg-purple-100 text-purple-800' },
      'UnderEvaluation': { variant: 'default', className: 'bg-yellow-100 text-yellow-800' },
      'Accepted': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Withdrawn': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
    };

    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return (
      <Badge variant={config.variant} className={config.className}>
        {status}
      </Badge>
    );
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP p');
    } catch {
      return dateString;
    }
  };

  const formatCurrency = (amount?: number, currency?: string) => {
    if (amount === undefined || amount === null) return 'N/A';
    return `${currency || 'USD'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading bid details...</p>
        </div>
      </div>
    );
  }

  if (!bid) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Bid not found</p>
          <Button onClick={() => router.push('/procurement/bids')} className="mt-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Bids
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
          <Button variant="ghost" onClick={() => router.push('/procurement/bids')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{bid.businessPartnerName}</h1>
            <p className="text-gray-500">Bid #{bid.bidNumber}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {bid.status === 'Submitted' && (
            <Button onClick={handleOpenBid} disabled={opening}>
              <CheckCircle className="h-4 w-4 mr-2" />
              {opening ? 'Opening...' : 'Mark as Opened'}
            </Button>
          )}
          {getStatusBadge(bid.status)}
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Bid Amount</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{formatCurrency(bid.totalBidAmount, bid.currency)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Score</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{bid.totalScore !== undefined && bid.totalScore !== null ? `${bid.totalScore.toFixed(2)}%` : 'Not Evaluated'}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Rank</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{bid.rank ? `#${bid.rank}` : 'N/A'}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Compliance</CardTitle>
          </CardHeader>
          <CardContent>
            {bid.isCompliant ? (
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-6 w-6 text-green-500" />
                <span className="text-lg font-semibold text-green-700">Compliant</span>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <XCircle className="h-6 w-6 text-red-500" />
                <span className="text-lg font-semibold text-red-700">Non-Compliant</span>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
        <TabsList className="grid w-full grid-cols-6">
          <TabsTrigger value="overview">
            <FileText className="h-4 w-4 mr-2" />
            Overview
          </TabsTrigger>
          <TabsTrigger value="items">
            <Package className="h-4 w-4 mr-2" />
            Lots ({bid.items?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="proposals">
            <FileText className="h-4 w-4 mr-2" />
            Proposals
          </TabsTrigger>
          <TabsTrigger value="documents">
            <Upload className="h-4 w-4 mr-2" />
            Documents ({bid.documents?.filter(doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal').length || 0})
          </TabsTrigger>
          <TabsTrigger value="evaluation">
            <Award className="h-4 w-4 mr-2" />
            Evaluation ({bid.evaluations?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="interviews">
            <MessageSquare className="h-4 w-4 mr-2" />
            Interviews ({bid.interviews?.length || 0})
          </TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Bid Information</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <p className="text-sm text-gray-500">Bid Number</p>
                <p className="font-semibold">{bid.bidNumber}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Tender</p>
                <p className="font-semibold">{bid.tenderTitle}</p>
                <p className="text-sm text-gray-500">{bid.tenderNumber}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Business Partner</p>
                <p className="font-semibold">{bid.businessPartnerName}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Status</p>
                {getStatusBadge(bid.status)}
              </div>
              <div>
                <p className="text-sm text-gray-500">Total Bid Amount</p>
                <p className="font-semibold">{formatCurrency(bid.totalBidAmount, bid.currency)}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Submitted Date</p>
                <p className="font-semibold">{formatDate(bid.submittedDate)}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Delivery Days</p>
                <p className="font-semibold">{bid.deliveryDays ? `${bid.deliveryDays} days` : 'N/A'}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">Compliance Status</p>
                {bid.isCompliant ? (
                  <Badge variant="default" className="bg-green-100 text-green-800">Compliant</Badge>
                ) : (
                  <Badge variant="destructive">Non-Compliant</Badge>
                )}
              </div>
              {bid.paymentTerms && (
                <div className="md:col-span-2">
                  <p className="text-sm text-gray-500">Payment Terms</p>
                  <p className="mt-1">{bid.paymentTerms}</p>
                </div>
              )}
              {bid.warrantyTerms && (
                <div className="md:col-span-2">
                  <p className="text-sm text-gray-500">Warranty Terms</p>
                  <p className="mt-1">{bid.warrantyTerms}</p>
                </div>
              )}
            </CardContent>
          </Card>

          {bid.technicalProposal && (
            <Card>
              <CardHeader>
                <CardTitle>Technical Proposal</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap">{bid.technicalProposal}</p>
              </CardContent>
            </Card>
          )}

          {bid.commercialProposal && (
            <Card>
              <CardHeader>
                <CardTitle>Commercial Proposal</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="whitespace-pre-wrap">{bid.commercialProposal}</p>
              </CardContent>
            </Card>
          )}

          {!bid.isCompliant && bid.nonComplianceReasons && (
            <Card className="border-red-200 bg-red-50">
              <CardHeader>
                <CardTitle className="text-red-800">Non-Compliance Reasons</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-red-700">{bid.nonComplianceReasons}</p>
              </CardContent>
            </Card>
          )}

          {/* Evaluation Scores */}
          {template && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  Evaluation Scores
                  <Badge variant="outline">{template.templateName}</Badge>
                </CardTitle>
                <CardDescription>
                  Scoring Method: {template.scoringMethod} | Passing Score: {template.passingScore}%
                  {bid.evaluations && bid.evaluations.filter(e => e.status === 'Submitted').length > 0 && (
                    <span className="ml-2">| {bid.evaluations.filter(e => e.status === 'Submitted').length} evaluator(s)</span>
                  )}
                </CardDescription>
              </CardHeader>
              <CardContent>
                {(() => {
                  // Calculate average scores from all submitted evaluations
                  const submittedEvaluations = bid.evaluations?.filter(e => e.status === 'Submitted' && e.evaluationCriteriaJson) || [];
                  const aggregatedScores: Record<string, { totalScore: number; count: number; maxScore: number; weight: number }> = {};

                  // Parse all evaluation criteria and aggregate
                  submittedEvaluations.forEach(evaluation => {
                    try {
                      const scores: CriteriaScore[] = JSON.parse(evaluation.evaluationCriteriaJson || '[]');
                      scores.forEach(score => {
                        if (!aggregatedScores[score.criterionId]) {
                          aggregatedScores[score.criterionId] = { totalScore: 0, count: 0, maxScore: score.maxScore, weight: score.weight };
                        }
                        aggregatedScores[score.criterionId].totalScore += score.score;
                        aggregatedScores[score.criterionId].count += 1;
                      });
                    } catch (e) {
                      console.error('Error parsing evaluation criteria:', e);
                    }
                  });

                  const hasScores = Object.keys(aggregatedScores).length > 0;
                  const colors = [
                    'text-blue-600', 'text-green-600', 'text-orange-600',
                    'text-purple-600', 'text-red-600', 'text-cyan-600',
                    'text-pink-600', 'text-indigo-600', 'text-teal-600'
                  ];

                  return (
                    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
                      {template.criteria
                        ?.sort((a, b) => a.displayOrder - b.displayOrder)
                        .map((criterion, index) => {
                          const colorClass = colors[index % colors.length];
                          const scoreData = aggregatedScores[criterion.evaluationCriterionId];
                          const avgScore = scoreData ? (scoreData.totalScore / scoreData.count) : null;

                          return (
                            <div key={criterion.evaluationCriterionId} className="p-3 bg-gray-50 rounded-lg">
                              <div className="flex items-center justify-between mb-1">
                                <p className="text-sm font-medium text-gray-700">{criterion.criterionName}</p>
                                {criterion.isMandatory && (
                                  <Badge variant="destructive" className="text-xs">Required</Badge>
                                )}
                              </div>
                              {avgScore !== null ? (
                                <div className="flex items-baseline gap-1 mb-1">
                                  <span className={`text-xl font-bold ${colorClass}`}>
                                    {avgScore.toFixed(1)}
                                  </span>
                                  <span className="text-sm text-gray-400">/ {criterion.maxScore}</span>
                                </div>
                              ) : (
                                <p className="text-sm text-gray-400 mb-1">Not scored</p>
                              )}
                              <div className="flex items-center justify-between">
                                <span className="text-xs text-gray-500">Weight: {criterion.weight}%</span>
                                {avgScore !== null && (
                                  <span className="text-xs text-gray-500">
                                    {((avgScore / criterion.maxScore) * criterion.weight).toFixed(1)}% weighted
                                  </span>
                                )}
                              </div>
                            </div>
                          );
                        })}
                    </div>
                  );
                })()}
                {bid.totalScore !== undefined && bid.totalScore !== null && (
                  <div className="mt-4 pt-4 border-t flex items-center justify-between">
                    <span className="text-sm font-medium">Total Score</span>
                    <div className="flex items-center gap-2">
                      <span className={`text-2xl font-bold ${
                        bid.totalScore >= template.passingScore ? 'text-green-600' : 'text-red-600'
                      }`}>
                        {bid.totalScore.toFixed(2)}%
                      </span>
                      {bid.totalScore >= template.passingScore ? (
                        <Badge variant="default" className="bg-green-600">Pass</Badge>
                      ) : (
                        <Badge variant="destructive">Fail</Badge>
                      )}
                    </div>
                  </div>
                )}
                {bid.evaluatedByName && (
                  <div className="mt-4 pt-4 border-t">
                    <p className="text-sm text-gray-500">
                      Evaluated by <span className="font-semibold">{bid.evaluatedByName}</span> on {formatDate(bid.evaluatedDate)}
                    </p>
                    {bid.evaluationNotes && (
                      <p className="mt-2 text-sm">{bid.evaluationNotes}</p>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Fallback: Show message if no template but bid has been evaluated */}
          {!template && bid.evaluationTemplateId && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <AlertCircle className="h-5 w-5 text-yellow-500" />
                  Evaluation Template
                </CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-yellow-700">
                  This tender has an evaluation template assigned but it could not be loaded.
                </p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Lots Tab */}
        <TabsContent value="items" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Bid Lots</CardTitle>
              <CardDescription>{bid.items?.length || 0} lot(s)</CardDescription>
            </CardHeader>
            <CardContent>
              {!bid.items || bid.items.length === 0 ? (
                <p className="text-center py-8 text-gray-500">No lots in this bid</p>
              ) : (
                <>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Lot Description</TableHead>
                        <TableHead>Requested Qty</TableHead>
                        <TableHead>Offered Qty</TableHead>
                        <TableHead>Unit Price</TableHead>
                        <TableHead>Total Price</TableHead>
                        <TableHead>Brand/Model</TableHead>
                        <TableHead>Delivery Days</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {bid.items.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell>
                            <div>
                              <p className="font-medium">{item.tenderItemDescription}</p>
                              {item.specifications && (
                                <p className="text-sm text-gray-500 whitespace-pre-wrap">{item.specifications}</p>
                              )}
                            </div>
                          </TableCell>
                          <TableCell>
                            {item.requestedQuantity}
                            {item.unitOfMeasure && <span className="text-gray-500 text-sm ml-1">{item.unitOfMeasure}</span>}
                          </TableCell>
                          <TableCell className="font-semibold">
                            {item.offeredQuantity}
                            {item.unitOfMeasure && <span className="text-gray-500 text-sm ml-1">{item.unitOfMeasure}</span>}
                          </TableCell>
                          <TableCell>{formatCurrency(item.unitPrice, bid.currency)}</TableCell>
                          <TableCell className="font-semibold">{formatCurrency(item.totalPrice, bid.currency)}</TableCell>
                          <TableCell>
                            {item.brand || item.model ? `${item.brand || ''} ${item.model || ''}`.trim() : '-'}
                          </TableCell>
                          <TableCell>{item.deliveryDays ? `${item.deliveryDays} days` : '-'}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>

                  <div className="mt-4 flex justify-end">
                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 min-w-64 max-w-md">
                      <div className="flex items-center justify-between gap-4">
                        <span className="text-lg font-medium whitespace-nowrap">Total Bid Amount:</span>
                        <span className="text-2xl font-bold text-blue-600 break-all text-right">
                          {formatCurrency(bid.totalBidAmount, bid.currency)}
                        </span>
                      </div>
                    </div>
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Proposals Tab */}
        <TabsContent value="proposals" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Technical Proposal</CardTitle>
            </CardHeader>
            <CardContent>
              {bid.status === 'Submitted' ? (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-yellow-800">
                    <Clock className="h-5 w-5" />
                    <span className="font-medium">Documents will be available after the bid is opened</span>
                  </div>
                </div>
              ) : bid.documents?.find(doc => doc.documentType === 'TechnicalProposal') ? (
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <CheckCircle className="h-5 w-5 text-green-600" />
                    <span className="font-medium text-green-700">Technical Proposal Uploaded</span>
                  </div>
                  {(() => {
                    const doc = bid.documents.find(doc => doc.documentType === 'TechnicalProposal');
                    return doc ? (
                      <div className="bg-gray-50 border rounded-lg p-4">
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <p className="font-medium">{doc.documentName}</p>
                            <p className="text-sm text-gray-500 font-mono">{doc.fileName}</p>
                            <p className="text-sm text-gray-500 mt-1">
                              Uploaded: {formatDate(doc.uploadedAt)} • Size: {(doc.fileSize / 1024).toFixed(2)} KB
                            </p>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => tenderBidService.downloadBidDocument(bid.id, doc.id, doc.documentName)}
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                        </div>
                      </div>
                    ) : null;
                  })()}
                </div>
              ) : bid.technicalProposal ? (
                <div className="bg-gray-50 border rounded-lg p-4">
                  <p className="whitespace-pre-wrap">{bid.technicalProposal}</p>
                </div>
              ) : (
                <p className="text-center py-8 text-gray-500">No technical proposal provided</p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Commercial Proposal</CardTitle>
            </CardHeader>
            <CardContent>
              {bid.status === 'Submitted' ? (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-yellow-800">
                    <Clock className="h-5 w-5" />
                    <span className="font-medium">Documents will be available after the bid is opened</span>
                  </div>
                </div>
              ) : bid.documents?.find(doc => doc.documentType === 'CommercialProposal') ? (
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <CheckCircle className="h-5 w-5 text-green-600" />
                    <span className="font-medium text-green-700">Commercial Proposal Uploaded</span>
                  </div>
                  {(() => {
                    const doc = bid.documents.find(doc => doc.documentType === 'CommercialProposal');
                    return doc ? (
                      <div className="bg-gray-50 border rounded-lg p-4">
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <p className="font-medium">{doc.documentName}</p>
                            <p className="text-sm text-gray-500 font-mono">{doc.fileName}</p>
                            <p className="text-sm text-gray-500 mt-1">
                              Uploaded: {formatDate(doc.uploadedAt)} • Size: {(doc.fileSize / 1024).toFixed(2)} KB
                            </p>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => tenderBidService.downloadBidDocument(bid.id, doc.id, doc.documentName)}
                          >
                            <Download className="h-4 w-4 mr-2" />
                            Download
                          </Button>
                        </div>
                      </div>
                    ) : null;
                  })()}
                </div>
              ) : bid.commercialProposal ? (
                <div className="bg-gray-50 border rounded-lg p-4">
                  <p className="whitespace-pre-wrap">{bid.commercialProposal}</p>
                </div>
              ) : (
                <p className="text-center py-8 text-gray-500">No commercial proposal provided</p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Documents Tab */}
        <TabsContent value="documents" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Required Documents</CardTitle>
              <CardDescription>
                {bid.documents?.filter(doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal').length || 0} document(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              {bid.status === 'Submitted' ? (
                <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                  <div className="flex items-center gap-2 text-yellow-800">
                    <Clock className="h-5 w-5" />
                    <span className="font-medium">Documents will be available after the bid is opened</span>
                  </div>
                </div>
              ) : !bid.documents || bid.documents.filter(doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal').length === 0 ? (
                <p className="text-center py-8 text-gray-500">No required documents uploaded</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Document Type</TableHead>
                      <TableHead>Document Name</TableHead>
                      <TableHead>File Size</TableHead>
                      <TableHead>Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {bid.documents
                      .filter(doc => doc.documentType !== 'TechnicalProposal' && doc.documentType !== 'CommercialProposal')
                      .map((doc) => (
                        <TableRow key={doc.id}>
                          <TableCell><Badge variant="outline">{doc.documentType}</Badge></TableCell>
                          <TableCell>{doc.documentName}</TableCell>
                          <TableCell>{(doc.fileSize / 1024).toFixed(2)} KB</TableCell>
                          <TableCell>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => tenderBidService.downloadBidDocument(bid.id, doc.id, doc.documentName)}
                            >
                              <Download className="h-4 w-4 mr-2" />
                              Download
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Evaluation Tab */}
        <TabsContent value="evaluation" className="space-y-4">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>Evaluations</CardTitle>
                  <CardDescription>{bid.evaluations?.length || 0} evaluation(s)</CardDescription>
                </div>
                <Button
                  onClick={() => router.push(`/procurement/evaluations/create?bidId=${bidId}`)}
                  className="gap-2"
                >
                  <Award className="h-4 w-4" />
                  Create Evaluation
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {!bid.evaluations || bid.evaluations.length === 0 ? (
                <div className="text-center py-8">
                  <Award className="h-12 w-12 mx-auto mb-4 text-gray-300" />
                  <p className="text-gray-500 mb-4">No evaluations yet</p>
                  <Button
                    onClick={() => router.push(`/procurement/evaluations/create?bidId=${bidId}`)}
                    variant="outline"
                  >
                    <Award className="h-4 w-4 mr-2" />
                    Create First Evaluation
                  </Button>
                </div>
              ) : (
                <div className="space-y-2">
                  {bid.evaluations.map((evaluation) => (
                    <Card key={evaluation.id} className="border">
                      <CardHeader className="pb-2 pt-3 px-4">
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex-1 min-w-0">
                            <CardTitle className="text-sm font-semibold">{evaluation.evaluatorName || 'Unknown Evaluator'}</CardTitle>
                            <CardDescription className="text-xs">{formatDate(evaluation.evaluationDate)}</CardDescription>
                          </div>
                          <div className="flex items-center gap-1 flex-shrink-0">
                            <Badge
                              variant={evaluation.status === 'Submitted' ? 'default' : 'outline'}
                              className={`text-xs ${evaluation.status === 'Submitted' ? 'bg-green-600' : ''}`}
                            >
                              {evaluation.status === 'Submitted' ? 'Completed' : evaluation.status}
                            </Badge>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="h-7 px-2 text-xs"
                              onClick={() => router.push(`/procurement/evaluations/${evaluation.id}`)}
                            >
                              View
                            </Button>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent className="pt-2 pb-3 px-4 space-y-2">
                        {/* Dynamic Criteria Scores */}
                        {evaluation.evaluationCriteriaJson ? (
                          (() => {
                            try {
                              const criteriaScores: CriteriaScore[] = JSON.parse(evaluation.evaluationCriteriaJson);
                              const colors = [
                                'text-blue-600', 'text-green-600', 'text-orange-600',
                                'text-purple-600', 'text-red-600', 'text-cyan-600',
                                'text-pink-600', 'text-indigo-600', 'text-teal-600'
                              ];
                              return (
                                <div className="grid grid-cols-3 md:grid-cols-4 lg:grid-cols-6 gap-2">
                                  {criteriaScores.map((criteria, index) => (
                                    <div key={criteria.criterionId}>
                                      <p className="text-xs text-gray-500">{criteria.criterionName}</p>
                                      <div className="flex items-baseline gap-1">
                                        <p className={`text-sm font-bold ${colors[index % colors.length]}`}>
                                          {criteria.score}
                                        </p>
                                        <span className="text-xs text-gray-400">/ {criteria.maxScore}</span>
                                      </div>
                                      <p className="text-xs text-gray-400">
                                        Weighted: {criteria.weightedScore.toFixed(1)}%
                                      </p>
                                    </div>
                                  ))}
                                  <div className="border-l pl-2">
                                    <p className="text-xs text-gray-500 font-medium">Total</p>
                                    <p className="text-sm font-bold text-indigo-600">
                                      {evaluation.totalScore !== undefined && evaluation.totalScore !== null
                                        ? `${evaluation.totalScore.toFixed(1)}%`
                                        : criteriaScores.reduce((sum, c) => sum + c.weightedScore, 0).toFixed(1) + '%'
                                      }
                                    </p>
                                  </div>
                                </div>
                              );
                            } catch {
                              return <p className="text-xs text-gray-500">Could not parse evaluation criteria</p>;
                            }
                          })()
                        ) : (
                          <p className="text-xs text-gray-500">No detailed criteria scores available</p>
                        )}
                        {(evaluation.technicalComments || evaluation.commercialComments || evaluation.recommendation) && (
                          <div className="text-xs space-y-1 pt-1 border-t">
                            {evaluation.technicalComments && (
                              <div>
                                <p className="font-medium text-gray-600">Technical:</p>
                                <p className="text-gray-700 line-clamp-2">{evaluation.technicalComments}</p>
                              </div>
                            )}
                            {evaluation.commercialComments && (
                              <div>
                                <p className="font-medium text-gray-600">Commercial:</p>
                                <p className="text-gray-700 line-clamp-2">{evaluation.commercialComments}</p>
                              </div>
                            )}
                            {evaluation.recommendation && (
                              <div className="bg-blue-50 p-2 rounded">
                                <p className="font-medium text-blue-900">Recommendation:</p>
                                <p className="text-blue-800 line-clamp-2">{evaluation.recommendation}</p>
                              </div>
                            )}
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Interviews Tab */}
        <TabsContent value="interviews" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Interviews</CardTitle>
              <CardDescription>{bid.interviews?.length || 0} interview(s)</CardDescription>
            </CardHeader>
            <CardContent>
              {!bid.interviews || bid.interviews.length === 0 ? (
                <p className="text-center py-8 text-gray-500">No interviews scheduled</p>
              ) : (
                <div className="space-y-4">
                  {bid.interviews.map((interview) => (
                    <Card key={interview.id}>
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div>
                            <CardTitle className="text-base">Interview</CardTitle>
                            <CardDescription>{formatDate(interview.interviewDate)}</CardDescription>
                          </div>
                          <Badge variant={interview.status === 'Completed' ? 'default' : 'outline'}>
                            {interview.status}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-2">
                        {interview.location && (
                          <div>
                            <p className="text-sm text-gray-500">Location</p>
                            <p className="font-medium">{interview.location}</p>
                          </div>
                        )}
                        {interview.interviewerNames && (
                          <div>
                            <p className="text-sm text-gray-500">Interviewers</p>
                            <p className="font-medium">{interview.interviewerNames}</p>
                          </div>
                        )}
                        {interview.notes && (
                          <div>
                            <p className="text-sm text-gray-500">Notes</p>
                            <p className="text-sm mt-1">{interview.notes}</p>
                          </div>
                        )}
                        {interview.outcome && (
                          <div className="bg-blue-50 p-3 rounded-lg">
                            <p className="text-sm font-medium text-blue-900">Outcome:</p>
                            <p className="text-sm mt-1 text-blue-800">{interview.outcome}</p>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Open Bid Confirmation Dialog */}
      <ConfirmationDialog
        open={showOpenDialog}
        onOpenChange={setShowOpenDialog}
        title="Mark Bid as Opened"
        description="Are you sure you want to mark this bid as opened? The supplier will be notified that their bid has been opened."
        confirmText="Mark as Opened"
        cancelText="Cancel"
        variant="default"
        onConfirm={confirmOpenBid}
        isLoading={opening}
      />
    </div>
  );
}


