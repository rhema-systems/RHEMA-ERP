'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Loader2, Calculator, Award, XCircle, TrendingUp, AlertTriangle } from 'lucide-react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  QCBSEvaluationResultDto,
  QCBSBidScoreDto,
  evaluateQCBS,
  getQCBSEvaluationResults,
} from '@/services/tenderService';
import { format } from 'date-fns';

interface QCBSEvaluationPanelProps {
  tenderId: string;
  useQCBSEvaluation: boolean;
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  tenderStatus: string;
}

export default function QCBSEvaluationPanel({
  tenderId,
  useQCBSEvaluation,
  technicalWeight,
  financialWeight,
  minimumTechnicalScore,
  tenderStatus,
}: QCBSEvaluationPanelProps) {
  const [evaluationResult, setEvaluationResult] = useState<QCBSEvaluationResultDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [evaluating, setEvaluating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load existing evaluation results
  useEffect(() => {
    const loadResults = async () => {
      if (!useQCBSEvaluation) return;
      
      try {
        setLoading(true);
        const result = await getQCBSEvaluationResults(tenderId);
        setEvaluationResult(result);
      } catch (err) {
        // No results yet is not an error
        console.log('No QCBS results yet');
      } finally {
        setLoading(false);
      }
    };
    loadResults();
  }, [tenderId, useQCBSEvaluation]);

  const handleRunEvaluation = async () => {
    try {
      setEvaluating(true);
      setError(null);
      const result = await evaluateQCBS(tenderId);
      setEvaluationResult(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to run QCBS evaluation');
    } finally {
      setEvaluating(false);
    }
  };

  if (!useQCBSEvaluation) {
    return (
      <Card className="bg-gray-50">
        <CardContent className="pt-6">
          <div className="flex items-center space-x-2 text-muted-foreground">
            <AlertTriangle className="h-5 w-5" />
            <span>QCBS Evaluation is not enabled for this tender.</span>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (loading) {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-8 w-8 animate-spin text-primary" />
            <span className="ml-2">Loading evaluation results...</span>
          </div>
        </CardContent>
      </Card>
    );
  }

  const canEvaluate = tenderStatus === 'Closed' || tenderStatus === 'UnderEvaluation';

  return (
    <div className="space-y-4">
      {/* QCBS Configuration Summary */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center space-x-2">
            <Calculator className="h-5 w-5" />
            <span>QCBS Evaluation Configuration</span>
          </CardTitle>
          <CardDescription>
            Quality and Cost-Based Selection evaluation settings
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="p-4 bg-blue-50 rounded-lg">
              <p className="text-sm text-muted-foreground">Technical Weight</p>
              <p className="text-2xl font-bold text-blue-600">{technicalWeight}%</p>
            </div>
            <div className="p-4 bg-green-50 rounded-lg">
              <p className="text-sm text-muted-foreground">Financial Weight</p>
              <p className="text-2xl font-bold text-green-600">{financialWeight}%</p>
            </div>
            <div className="p-4 bg-orange-50 rounded-lg">
              <p className="text-sm text-muted-foreground">Min. Technical Score</p>
              <p className="text-2xl font-bold text-orange-600">{minimumTechnicalScore}%</p>
            </div>
            <div className="flex items-center justify-center">
              <Button
                onClick={handleRunEvaluation}
                disabled={!canEvaluate || evaluating}
                className="w-full"
              >
                {evaluating ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Evaluating...
                  </>
                ) : (
                  <>
                    <Calculator className="h-4 w-4 mr-2" />
                    Run QCBS Evaluation
                  </>
                )}
              </Button>
            </div>
          </div>
          {!canEvaluate && (
            <p className="text-sm text-muted-foreground mt-4">
              QCBS evaluation can only be run when tender is Closed or Under Evaluation.
            </p>
          )}
          {error && (
            <div className="mt-4 p-3 bg-red-50 border border-red-200 rounded-lg text-red-600 text-sm">
              {error}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Evaluation Results */}
      {evaluationResult && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center space-x-2">
              <TrendingUp className="h-5 w-5" />
              <span>QCBS Evaluation Results</span>
            </CardTitle>
            <CardDescription>
              Evaluated on {format(new Date(evaluationResult.evaluationDate), 'dd MMM yyyy HH:mm')} by {evaluationResult.calculatedByName}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {/* Summary Stats */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
              <div className="p-4 bg-gray-50 rounded-lg">
                <p className="text-sm text-muted-foreground">Total Bids</p>
                <p className="text-2xl font-bold">{evaluationResult.totalBidsEvaluated}</p>
              </div>
              <div className="p-4 bg-green-50 rounded-lg">
                <p className="text-sm text-muted-foreground">Qualified Bids</p>
                <p className="text-2xl font-bold text-green-600">{evaluationResult.qualifiedBidsCount}</p>
              </div>
              <div className="p-4 bg-red-50 rounded-lg">
                <p className="text-sm text-muted-foreground">Disqualified Bids</p>
                <p className="text-2xl font-bold text-red-600">{evaluationResult.disqualifiedBidsCount}</p>
              </div>
              <div className="p-4 bg-blue-50 rounded-lg">
                <p className="text-sm text-muted-foreground">Lowest Bid Amount</p>
                <p className="text-2xl font-bold text-blue-600">
                  {evaluationResult.lowestBidAmount.toLocaleString()}
                </p>
              </div>
            </div>

            {/* Bid Scores Table */}
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-12">Rank</TableHead>
                  <TableHead>Bidder</TableHead>
                  <TableHead className="text-right">Bid Amount</TableHead>
                  <TableHead className="text-center">Technical Score</TableHead>
                  <TableHead className="text-center">Financial Score</TableHead>
                  <TableHead className="text-center">Combined Score</TableHead>
                  <TableHead className="text-center">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {evaluationResult.bidScores.map((bid: QCBSBidScoreDto) => (
                  <TableRow
                    key={bid.bidId}
                    className={bid.isRecommendedForAward ? 'bg-green-50' : ''}
                  >
                    <TableCell className="font-medium">
                      {bid.isQualifiedTechnically ? (
                        <span className="flex items-center justify-center w-8 h-8 rounded-full bg-primary text-primary-foreground text-sm font-bold">
                          {bid.rank}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <div>
                        <p className="font-medium">{bid.businessPartnerName}</p>
                        <p className="text-sm text-muted-foreground">{bid.bidNumber}</p>
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      {bid.currency} {bid.totalBidAmount.toLocaleString()}
                    </TableCell>
                    <TableCell className="text-center">
                      <Badge variant={bid.technicalScore >= minimumTechnicalScore ? 'default' : 'destructive'}>
                        {bid.technicalScore.toFixed(1)}%
                      </Badge>
                    </TableCell>
                    <TableCell className="text-center">
                      <span className="font-medium">{bid.financialScore.toFixed(1)}%</span>
                    </TableCell>
                    <TableCell className="text-center">
                      <span className="text-lg font-bold">{bid.combinedScore.toFixed(2)}</span>
                    </TableCell>
                    <TableCell className="text-center">
                      {bid.isRecommendedForAward ? (
                        <Badge className="bg-green-600">
                          <Award className="h-3 w-3 mr-1" />
                          Recommended
                        </Badge>
                      ) : bid.isQualifiedTechnically ? (
                        <Badge variant="secondary">Qualified</Badge>
                      ) : (
                        <div className="flex flex-col items-center">
                          <Badge variant="destructive">
                            <XCircle className="h-3 w-3 mr-1" />
                            Disqualified
                          </Badge>
                          {bid.disqualificationReason && (
                            <span className="text-xs text-red-600 mt-1">
                              {bid.disqualificationReason}
                            </span>
                          )}
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

