'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ArrowLeft, Download, FileText, Award, TrendingUp, Clock, Trophy, Users, CheckCircle2, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderEvaluationService from '@/services/tenderEvaluationService';
import { type EvaluationReportDto, type ConsolidatedEvaluationDto, type BidEvaluationSummaryDto, type EvaluationScorecardDto } from '@/services/tenderEvaluationService';
import { format } from 'date-fns';

export default function EvaluationReportPage() {
  const params = useParams();
  const router = useRouter();
  const tenderId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [report, setReport] = useState<EvaluationReportDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (tenderId) {
      loadReport();
    }
  }, [tenderId]);

  const loadReport = async () => {
    try {
      setLoading(true);
      const data = await tenderEvaluationService.getEvaluationReport(tenderId);
      setReport(data);
    } catch (error) {
      console.error('Error loading evaluation report:', error);
      toast.error('Failed to load evaluation report');
    } finally {
      setLoading(false);
    }
  };

  const handleExportPDF = () => {
    toast.info('PDF export functionality coming soon');
  };

  const handleExportExcel = () => {
    toast.info('Excel export functionality coming soon');
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP');
    } catch {
      return dateString;
    }
  };

  const getRankBadge = (rank?: number) => {
    if (!rank) return null;
    
    const colors: Record<number, string> = {
      1: 'bg-yellow-100 text-yellow-800 border-yellow-300',
      2: 'bg-gray-100 text-gray-800 border-gray-300',
      3: 'bg-orange-100 text-orange-800 border-orange-300',
    };

    return (
      <Badge variant="outline" className={colors[rank] || 'bg-blue-100 text-blue-800'}>
        Rank #{rank}
      </Badge>
    );
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading evaluation report...</p>
        </div>
      </div>
    );
  }

  if (!report) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
          <p className="text-lg text-gray-600">No evaluation report available</p>
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
            <h1 className="text-3xl font-bold">Evaluation Report</h1>
            <p className="text-gray-500">{report.tenderNumber} - {report.tenderTitle}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={handleExportPDF}>
            <Download className="h-4 w-4 mr-2" />
            Export PDF
          </Button>
          <Button variant="outline" onClick={handleExportExcel}>
            <Download className="h-4 w-4 mr-2" />
            Export Excel
          </Button>
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
              <p className="text-sm text-gray-500">Tender Type</p>
              <p className="font-medium">{report.tenderType}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Estimated Value</p>
              <p className="font-medium">
                {report.currency} {report.estimatedValue?.toLocaleString() || 'N/A'}
              </p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Publish Date</p>
              <p className="font-medium">{formatDate(report.publishDate)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-500">Submission Deadline</p>
              <p className="font-medium">{formatDate(report.submissionDeadline)}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Evaluation Criteria */}
      <Card>
        <CardHeader>
          <CardTitle>Evaluation Criteria & Weightages</CardTitle>
          <CardDescription>
            {report.useQCBSEvaluation
              ? 'Quality and Cost-Based Selection (QCBS) evaluation method'
              : 'Criteria used for bid evaluation'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {report.useQCBSEvaluation ? (
            /* QCBS Evaluation Criteria */
            <div className="space-y-4">
              <div className="flex items-center gap-2 mb-4">
                <Badge className="bg-blue-600">QCBS Evaluation</Badge>
              </div>
              <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
                <div className="text-center p-4 bg-blue-50 rounded-lg">
                  <p className="text-sm text-gray-600">Technical Weight</p>
                  <p className="text-2xl font-bold text-blue-600">{report.technicalWeight}%</p>
                </div>
                <div className="text-center p-4 bg-green-50 rounded-lg">
                  <p className="text-sm text-gray-600">Financial Weight</p>
                  <p className="text-2xl font-bold text-green-600">{report.financialWeight}%</p>
                </div>
                <div className="text-center p-4 bg-orange-50 rounded-lg">
                  <p className="text-sm text-gray-600">Min. Technical Score</p>
                  <p className="text-2xl font-bold text-orange-600">{report.minimumTechnicalScore}%</p>
                </div>
              </div>
              {report.lowestBidAmount && (
                <div className="mt-4 p-3 bg-gray-50 rounded-lg text-sm">
                  <strong>Lowest Bid Amount:</strong> {report.currency} {report.lowestBidAmount.toLocaleString()}
                </div>
              )}
            </div>
          ) : (
            /* Standard Evaluation Criteria */
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="text-center p-4 bg-blue-50 rounded-lg">
                <p className="text-sm text-gray-600">Price</p>
                <p className="text-2xl font-bold text-blue-600">{report.priceWeightage}%</p>
              </div>
              <div className="text-center p-4 bg-green-50 rounded-lg">
                <p className="text-sm text-gray-600">Quality</p>
                <p className="text-2xl font-bold text-green-600">{report.qualityWeightage}%</p>
              </div>
              <div className="text-center p-4 bg-orange-50 rounded-lg">
                <p className="text-sm text-gray-600">Delivery</p>
                <p className="text-2xl font-bold text-orange-600">{report.deliveryWeightage}%</p>
              </div>
              <div className="text-center p-4 bg-purple-50 rounded-lg">
                <p className="text-sm text-gray-600">Experience</p>
                <p className="text-2xl font-bold text-purple-600">{report.experienceWeightage}%</p>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500 flex items-center gap-2">
              <FileText className="h-4 w-4" />
              Total Bids
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-3xl font-bold">{report.totalBidsReceived}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500 flex items-center gap-2">
              <CheckCircle2 className="h-4 w-4 text-green-500" />
              Compliant Bids
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-3xl font-bold text-green-600">{report.compliantBids}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500 flex items-center gap-2">
              <Users className="h-4 w-4 text-blue-500" />
              Evaluated Bids
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-3xl font-bold text-blue-600">{report.evaluatedBids}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500 flex items-center gap-2">
              <TrendingUp className="h-4 w-4 text-purple-500" />
              Evaluation Progress
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-3xl font-bold text-purple-600">
              {report.totalBidsReceived > 0 ? Math.round((report.evaluatedBids / report.totalBidsReceived) * 100) : 0}%
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Recommended Bid */}
      {report.recommendedBidId && (
        <Card className="border-green-200 bg-green-50">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-green-800">
              <Trophy className="h-5 w-5" />
              Recommended Bid for Award
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div>
                <p className="text-sm text-green-600">Bid Number</p>
                <p className="font-mono font-bold text-green-800">{report.recommendedBidNumber}</p>
              </div>
              <div>
                <p className="text-sm text-green-600">Business Partner</p>
                <p className="font-bold text-green-800">{report.recommendedBusinessPartnerName || 'N/A'}</p>
              </div>
              <div>
                <p className="text-sm text-green-600">Bid Amount</p>
                <p className="font-bold text-green-800">
                  {report.currency} {report.recommendedBidAmount?.toLocaleString() || 'N/A'}
                </p>
              </div>
              <div>
                <p className="text-sm text-green-600">Status</p>
                <Badge className="bg-green-600">
                  <Award className="h-3 w-3 mr-1" />
                  Recommended for Award
                </Badge>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Consolidated Bid Rankings Table */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Trophy className="h-5 w-5 text-yellow-500" />
            Consolidated Bid Rankings
          </CardTitle>
          <CardDescription>
            {report.useQCBSEvaluation
              ? 'All bids ranked by QCBS combined scores (Technical + Financial)'
              : 'All bids ranked by weighted average scores'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {report.bidEvaluations && report.bidEvaluations.length > 0 ? (
            report.useQCBSEvaluation ? (
              /* QCBS Rankings Table */
              <Table>
                <TableHeader>
                  <TableRow className="bg-gray-50">
                    <TableHead className="w-16 text-center">Rank</TableHead>
                    <TableHead>Business Partner</TableHead>
                    <TableHead>Bid Number</TableHead>
                    <TableHead className="text-right">Bid Amount</TableHead>
                    <TableHead className="text-center">Tech Qualified</TableHead>
                    <TableHead className="text-center">Technical Score</TableHead>
                    <TableHead className="text-center">Financial Score</TableHead>
                    <TableHead className="text-center font-bold">Combined Score</TableHead>
                    <TableHead className="text-center">Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {report.bidEvaluations
                    .sort((a, b) => (a.rank || 999) - (b.rank || 999))
                    .map((bid, index) => (
                      <TableRow
                        key={bid.bidId}
                        className={`
                          ${bid.isRecommended ? 'bg-green-50 hover:bg-green-100' : 'hover:bg-gray-50'}
                          ${index === 0 && bid.isQualifiedTechnically ? 'border-l-4 border-l-yellow-400' : ''}
                          ${!bid.isQualifiedTechnically ? 'opacity-60' : ''}
                        `}
                      >
                        <TableCell className="text-center">
                          {bid.isQualifiedTechnically ? getRankBadge(bid.rank) : '-'}
                        </TableCell>
                        <TableCell className="font-medium">
                          {bid.businessPartnerName || 'Unknown'}
                          {bid.isRecommended && (
                            <Badge className="ml-2 bg-green-600 text-xs">
                              <Award className="h-3 w-3 mr-1" />
                              Recommended
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell className="font-mono text-sm">{bid.bidNumber}</TableCell>
                        <TableCell className="text-right font-medium">
                          {report.currency} {bid.totalBidAmount.toLocaleString()}
                        </TableCell>
                        <TableCell className="text-center">
                          {bid.isQualifiedTechnically ? (
                            <CheckCircle2 className="h-5 w-5 text-green-500 mx-auto" />
                          ) : (
                            <div className="flex flex-col items-center">
                              <AlertCircle className="h-5 w-5 text-red-500" />
                              <span className="text-xs text-red-500 mt-1">
                                {bid.disqualificationReason || 'Below minimum'}
                              </span>
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-center">
                          <Badge variant={bid.technicalScore && bid.technicalScore >= report.minimumTechnicalScore ? 'default' : 'destructive'}>
                            {bid.technicalScore?.toFixed(1) || '-'}%
                          </Badge>
                        </TableCell>
                        <TableCell className="text-center">
                          <span className="text-green-600 font-medium">
                            {bid.financialScore?.toFixed(1) || '-'}%
                          </span>
                        </TableCell>
                        <TableCell className="text-center">
                          <span className="text-xl font-bold text-gray-800">
                            {bid.combinedScore?.toFixed(2) || '-'}
                          </span>
                        </TableCell>
                        <TableCell className="text-center">
                          <Badge variant={bid.bidStatus === 'Evaluated' ? 'default' : 'outline'}>
                            {bid.bidStatus || 'N/A'}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                </TableBody>
              </Table>
            ) : (
              /* Standard Rankings Table */
              <Table>
                <TableHeader>
                  <TableRow className="bg-gray-50">
                    <TableHead className="w-16 text-center">Rank</TableHead>
                    <TableHead>Business Partner</TableHead>
                    <TableHead>Bid Number</TableHead>
                    <TableHead className="text-right">Bid Amount</TableHead>
                    <TableHead className="text-center">Status</TableHead>
                    <TableHead className="text-center">Compliant</TableHead>
                    <TableHead className="text-center">Price</TableHead>
                    <TableHead className="text-center">Quality</TableHead>
                    <TableHead className="text-center">Delivery</TableHead>
                    <TableHead className="text-center">Experience</TableHead>
                    <TableHead className="text-center font-bold">Final Score</TableHead>
                    <TableHead className="text-center">Recommendations</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {report.bidEvaluations
                    .sort((a, b) => (a.rank || 999) - (b.rank || 999))
                    .map((bid, index) => (
                      <TableRow
                        key={bid.bidId}
                        className={`
                          ${bid.isRecommended ? 'bg-green-50 hover:bg-green-100' : 'hover:bg-gray-50'}
                          ${index === 0 ? 'border-l-4 border-l-yellow-400' : ''}
                        `}
                      >
                        <TableCell className="text-center">
                          {getRankBadge(bid.rank)}
                        </TableCell>
                        <TableCell className="font-medium">
                          {bid.businessPartnerName || 'Unknown'}
                          {bid.isRecommended && (
                            <Badge className="ml-2 bg-green-600 text-xs">
                              <Award className="h-3 w-3 mr-1" />
                              Recommended
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell className="font-mono text-sm">{bid.bidNumber}</TableCell>
                        <TableCell className="text-right font-medium">
                          {report.currency} {bid.totalBidAmount.toLocaleString()}
                        </TableCell>
                        <TableCell className="text-center">
                          <Badge variant={bid.bidStatus === 'Evaluated' ? 'default' : 'outline'}>
                            {bid.bidStatus || 'N/A'}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-center">
                          {bid.isCompliant ? (
                            <CheckCircle2 className="h-5 w-5 text-green-500 mx-auto" />
                          ) : (
                            <AlertCircle className="h-5 w-5 text-red-500 mx-auto" />
                          )}
                        </TableCell>
                        <TableCell className="text-center">
                          <span className="text-blue-600 font-medium">
                            {bid.weightedPriceScore?.toFixed(1) || '-'}
                          </span>
                        </TableCell>
                        <TableCell className="text-center">
                          <span className="text-green-600 font-medium">
                            {bid.weightedQualityScore?.toFixed(1) || '-'}
                          </span>
                        </TableCell>
                        <TableCell className="text-center">
                          <span className="text-orange-600 font-medium">
                            {bid.weightedDeliveryScore?.toFixed(1) || '-'}
                          </span>
                        </TableCell>
                        <TableCell className="text-center">
                          <span className="text-purple-600 font-medium">
                            {bid.weightedExperienceScore?.toFixed(1) || '-'}
                          </span>
                        </TableCell>
                        <TableCell className="text-center">
                          <span className="text-xl font-bold text-gray-800">
                            {bid.finalScore?.toFixed(1) || '-'}
                          </span>
                        </TableCell>
                        <TableCell className="text-center">
                          {bid.recommendationCount > 0 ? (
                            <Badge variant="outline" className="bg-green-50 text-green-700 border-green-300">
                              {bid.recommendationCount} evaluator{bid.recommendationCount > 1 ? 's' : ''}
                            </Badge>
                          ) : (
                            <span className="text-gray-400">-</span>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                </TableBody>
              </Table>
            )
          ) : (
            <div className="text-center py-8">
              <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No bid evaluations available yet</p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* QCBS Disqualified Bids Warning */}
      {report.useQCBSEvaluation && report.disqualifiedBidsCount && report.disqualifiedBidsCount > 0 && (
        <Card className="border-red-200 bg-red-50">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-red-800 text-lg">
              <AlertCircle className="h-5 w-5" />
              Technically Disqualified Bids ({report.disqualifiedBidsCount})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-red-600 mb-3">
              These bids did not meet the minimum technical score of {report.minimumTechnicalScore}% and are not eligible for financial evaluation.
            </p>
            <div className="space-y-2">
              {report.bidEvaluations
                .filter(bid => !bid.isQualifiedTechnically)
                .map(bid => (
                  <div key={bid.bidId} className="flex items-start gap-3 p-2 bg-white rounded border border-red-200">
                    <AlertCircle className="h-4 w-4 text-red-500 mt-0.5 flex-shrink-0" />
                    <div>
                      <p className="font-medium text-red-800">
                        {bid.businessPartnerName} ({bid.bidNumber})
                      </p>
                      <p className="text-sm text-red-600">
                        Technical Score: {bid.technicalScore?.toFixed(1)}% (Required: {report.minimumTechnicalScore}%)
                      </p>
                    </div>
                  </div>
                ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Non-Compliant Bids Warning */}
      {report.nonCompliantBids > 0 && (
        <Card className="border-orange-200 bg-orange-50">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-orange-800 text-lg">
              <AlertCircle className="h-5 w-5" />
              Non-Compliant Bids ({report.nonCompliantBids})
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {report.bidEvaluations
                .filter(bid => !bid.isCompliant)
                .map(bid => (
                  <div key={bid.bidId} className="flex items-start gap-3 p-2 bg-white rounded border border-orange-200">
                    <AlertCircle className="h-4 w-4 text-orange-500 mt-0.5 flex-shrink-0" />
                    <div>
                      <p className="font-medium text-orange-800">
                        {bid.businessPartnerName} ({bid.bidNumber})
                      </p>
                      {bid.nonComplianceReasons && (
                        <p className="text-sm text-orange-600">{bid.nonComplianceReasons}</p>
                      )}
                    </div>
                  </div>
                ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Report Footer */}
      <Card>
        <CardContent className="py-4">
          <div className="flex items-center justify-between text-sm text-gray-500">
            <p>Generated on: {formatDate(report.generatedDate)}</p>
            {report.generatedByName && <p>Generated by: {report.generatedByName}</p>}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
