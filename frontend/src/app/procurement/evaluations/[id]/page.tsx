'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Slider } from '@/components/ui/slider';
import {
  AlertCircle,
  ArrowLeft,
  Clock,
  Save,
  Send,
  ShieldCheck,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import * as tenderEvaluationService from '@/services/tenderEvaluationService';
import * as tenderBidService from '@/services/tenderBidService';
import { evaluationTemplateService, type EvaluationTemplate } from '@/services/evaluationTemplateService';
import { type TenderEvaluationDto, type UpdateEvaluationDto } from '@/services/tenderEvaluationService';
import { type TenderBidDetailDto } from '@/services/tenderBidService';
import { createEvaluationIdempotencyKey } from '@/lib/procurement-evaluation-committee';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { useAuth } from '@/hooks/use-auth';

// Interface for storing criteria scores
interface CriteriaScore {
  criterionId: string;
  criterionName: string;
  criterionCode: string;
  score: number;
  weight: number;
  maxScore: number;
  weightedScore: number;
}

export default function EvaluationFormPage() {
  const params = useParams();
  const router = useRouter();
  const { hasPermission } = useAuth();
  const canEvaluate = hasPermission('procurement.tender.evaluate');
  const evaluationId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [evaluation, setEvaluation] = useState<TenderEvaluationDto | null>(null);
  const [bid, setBid] = useState<TenderBidDetailDto | null>(null);
  const [template, setTemplate] = useState<EvaluationTemplate | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Dynamic criteria scores - keyed by criterion ID
  const [criteriaScores, setCriteriaScores] = useState<Record<string, number>>({});

  // Comments and recommendation
  const [technicalComments, setTechnicalComments] = useState('');
  const [commercialComments, setCommercialComments] = useState('');
  const [overallComments, setOverallComments] = useState('');
  const [isRecommended, setIsRecommended] = useState(false);
  const [recommendation, setRecommendation] = useState('');
  const [submissionSignature, setSubmissionSignature] = useState('');
  const [submissionEvidence, setSubmissionEvidence] = useState('');

  useEffect(() => {
    if (evaluationId) {
      loadEvaluationData();
    }
  }, [evaluationId]);

  const loadEvaluationData = async () => {
    try {
      setLoading(true);
      const evalData = await tenderEvaluationService.getEvaluationById(evaluationId);
      setEvaluation(evalData);

      // Load bid details
      const bidData = await tenderBidService.getBidById(evalData.tenderBidId);
      setBid(bidData);

      // Load evaluation template if assigned
      if (bidData.evaluationTemplateId) {
        try {
          const templateData = await evaluationTemplateService.getById(bidData.evaluationTemplateId);
          setTemplate(templateData);

          // Initialize scores from existing evaluation data or default to 0
          const initialScores: Record<string, number> = {};

          // Try to parse existing criteria scores from evaluationCriteriaJson
          if (evalData.evaluationCriteriaJson) {
            try {
              const existingScores: CriteriaScore[] = JSON.parse(evalData.evaluationCriteriaJson);
              existingScores.forEach(score => {
                initialScores[score.criterionId] = score.score;
              });
            } catch (parseError) {
              console.error('Error parsing evaluation criteria JSON:', parseError);
            }
          }

          // Ensure all template criteria have a score (default to 0 if not found)
          templateData.criteria?.forEach(criterion => {
            if (!(criterion.evaluationCriterionId in initialScores)) {
              initialScores[criterion.evaluationCriterionId] = 0;
            }
          });

          setCriteriaScores(initialScores);
        } catch (templateError) {
          console.error('Error loading evaluation template:', templateError);
        }
      }

      // Populate form with existing data
      setTechnicalComments(evalData.technicalComments || '');
      setCommercialComments(evalData.commercialComments || '');
      setOverallComments(evalData.overallComments || '');
      setIsRecommended(evalData.isRecommended || false);
      setRecommendation(evalData.recommendation || '');
    } catch (error) {
      console.error('Error loading evaluation data:', error);
      toast.error(getProcurementProblemMessage(error, 'Failed to load evaluation data'));
    } finally {
      setLoading(false);
    }
  };

  const handleCriterionScoreChange = (criterionId: string, score: number) => {
    setCriteriaScores(prev => ({
      ...prev,
      [criterionId]: score
    }));
  };

  const calculateTotalScore = (): number => {
    if (!template?.criteria || template.criteria.length === 0) {
      return 0;
    }

    if (template.scoringMethod === 'WeightedAverage') {
      let totalWeightedScore = 0;
      let totalWeight = 0;

      template.criteria.forEach(criterion => {
        const score = criteriaScores[criterion.evaluationCriterionId] || 0;
        const normalizedScore = (score / criterion.maxScore) * 100;
        totalWeightedScore += normalizedScore * (criterion.weight / 100);
        totalWeight += criterion.weight;
      });

      return totalWeight > 0 ? totalWeightedScore : 0;
    } else if (template.scoringMethod === 'SimpleAverage') {
      let totalScore = 0;
      let count = 0;

      template.criteria.forEach(criterion => {
        const score = criteriaScores[criterion.evaluationCriterionId] || 0;
        const normalizedScore = (score / criterion.maxScore) * 100;
        totalScore += normalizedScore;
        count++;
      });

      return count > 0 ? totalScore / count : 0;
    } else {
      let allPass = true;
      template.criteria.forEach(criterion => {
        if (criterion.isMandatory) {
          const score = criteriaScores[criterion.evaluationCriterionId] || 0;
          if (criterion.minimumScore && score < criterion.minimumScore) {
            allPass = false;
          }
        }
      });
      return allPass ? 100 : 0;
    }
  };

  const buildEvaluationCriteriaJson = (): string => {
    if (!template?.criteria) return '[]';

    const scores: CriteriaScore[] = template.criteria.map(criterion => {
      const score = criteriaScores[criterion.evaluationCriterionId] || 0;
      const normalizedScore = (score / criterion.maxScore) * 100;
      const weightedScore = normalizedScore * (criterion.weight / 100);

      return {
        criterionId: criterion.evaluationCriterionId,
        criterionName: criterion.criterionName,
        criterionCode: criterion.criterionCode,
        score,
        weight: criterion.weight,
        maxScore: criterion.maxScore,
        weightedScore
      };
    });

    return JSON.stringify(scores);
  };

  const handleSaveDraft = async () => {
    try {
      setSaving(true);
      const data: UpdateEvaluationDto = {
        evaluationCriteriaJson: buildEvaluationCriteriaJson(),
        technicalComments,
        commercialComments,
        overallComments,
        isRecommended,
        recommendation,
      };

      await tenderEvaluationService.updateEvaluation(evaluationId, data);
      toast.success('Evaluation saved as draft');
      await loadEvaluationData();
    } catch (error) {
      console.error('Error saving evaluation:', error);
      toast.error(getProcurementProblemMessage(error, 'Failed to save evaluation'));
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = async () => {
    try {
      // Validate that at least some scores are provided
      const hasScores = Object.values(criteriaScores).some(score => score > 0);
      if (!hasScores) {
        toast.error('Please provide at least one score');
        return;
      }

      setSaving(true);

      // First save the evaluation
      const data: UpdateEvaluationDto = {
        evaluationCriteriaJson: buildEvaluationCriteriaJson(),
        technicalComments,
        commercialComments,
        overallComments,
        isRecommended,
        recommendation,
      };

      await tenderEvaluationService.updateEvaluation(evaluationId, data);

      // Then submit it
      await tenderEvaluationService.submitEvaluation(evaluationId, {
        confirmSubmission: true,
        signatureReference: submissionSignature,
        evidenceReference: submissionEvidence,
        idempotencyKey: createEvaluationIdempotencyKey('legacy-score-submit'),
      });

      toast.success('Evaluation submitted successfully');
      router.push('/procurement/evaluations');
    } catch (error) {
      console.error('Error submitting evaluation:', error);
      toast.error(getProcurementProblemMessage(error, 'Failed to submit evaluation'));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading evaluation...</p>
        </div>
      </div>
    );
  }

  if (!evaluation || !bid) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <XCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Evaluation not found</p>
          <Button onClick={() => router.push('/procurement/evaluations')} className="mt-4">
            Back to Evaluations
          </Button>
        </div>
      </div>
    );
  }

  const isReadOnly = evaluation.status !== 'Draft' || !canEvaluate;

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex flex-wrap items-center gap-4">
          <Button variant="ghost" onClick={() => router.push('/procurement/evaluations')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Bid Evaluation</h1>
            <p className="text-gray-500">Evaluate and score the bid</p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            onClick={() =>
              router.push(
                `/procurement/tenders/${bid.tenderId}/committee-controls`
              )
            }
          >
            <ShieldCheck className="mr-2 h-4 w-4" />
            Committee controls
          </Button>
          <Button
            variant="outline"
            onClick={() =>
              router.push(
                `/procurement/tenders/${bid.tenderId}/award-readiness`
              )
            }
          >
            <ShieldCheck className="mr-2 h-4 w-4" />
            Award readiness
          </Button>
          <Badge variant={evaluation.status === 'Draft' ? 'secondary' : 'default'}>
            {evaluation.status}
          </Badge>
        </div>
      </div>

      {/* Bid Information */}
      <Card>
        <CardHeader>
          <CardTitle>Bid Information</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <Label className="text-gray-500">Bid Number</Label>
              <p className="font-mono font-medium">{bid.bidNumber}</p>
            </div>
            <div>
              <Label className="text-gray-500">Business Partner</Label>
              <p className="font-medium">{bid.businessPartnerName}</p>
            </div>
            <div>
              <Label className="text-gray-500">Total Bid Amount</Label>
              <p className="text-lg font-bold text-blue-600">
                {bid.currency} {bid.totalBidAmount?.toLocaleString() || 'N/A'}
              </p>
            </div>
            <div>
              <Label className="text-gray-500">Submission Date</Label>
              <p>{bid.submittedDate ? new Date(bid.submittedDate).toLocaleDateString() : 'N/A'}</p>
            </div>
            <div>
              <Label className="text-gray-500">Bid Status</Label>
              <Badge>{bid.status}</Badge>
            </div>
            <div>
              <Label className="text-gray-500">Compliance Status</Label>
              <Badge variant={bid.isCompliant ? 'default' : 'destructive'}>
                {bid.isCompliant ? 'Compliant' : 'Non-Compliant'}
              </Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Evaluation Scores */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            Evaluation Scores
            {template && (
              <Badge variant="outline" className="ml-2">
                {template.templateName}
              </Badge>
            )}
          </CardTitle>
          {template && (
            <CardDescription>
              Scoring Method: {template.scoringMethod} | Passing Score: {template.passingScore}%
            </CardDescription>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          {!template ? (
            <div className="flex items-center gap-2 p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
              <AlertCircle className="h-5 w-5 text-yellow-600" />
              <p className="text-sm text-yellow-800">
                No evaluation template assigned to this tender.
              </p>
            </div>
          ) : template.criteria?.length === 0 ? (
            <div className="flex items-center gap-2 p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
              <AlertCircle className="h-5 w-5 text-yellow-600" />
              <p className="text-sm text-yellow-800">
                The evaluation template has no criteria defined.
              </p>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {template.criteria
                  ?.sort((a, b) => a.displayOrder - b.displayOrder)
                  .map((criterion, index) => {
                    const score = criteriaScores[criterion.evaluationCriterionId] || 0;
                    const colors = [
                      'text-blue-600', 'text-green-600', 'text-orange-600',
                      'text-purple-600', 'text-red-600', 'text-cyan-600',
                      'text-pink-600', 'text-indigo-600', 'text-teal-600'
                    ];
                    const colorClass = colors[index % colors.length];

                    return (
                      <div key={criterion.evaluationCriterionId} className="space-y-2">
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-2">
                            <Label htmlFor={criterion.evaluationCriterionId} className="text-sm font-medium">
                              {criterion.criterionName}
                            </Label>
                            {criterion.isMandatory && (
                              <Badge variant="destructive" className="text-xs">Required</Badge>
                            )}
                          </div>
                          <div className="flex items-center gap-2">
                            <span className={`text-lg font-bold ${colorClass}`}>
                              {score}
                            </span>
                            <span className="text-sm text-gray-500">/ {criterion.maxScore}</span>
                          </div>
                        </div>
                        <Slider
                          id={criterion.evaluationCriterionId}
                          min={0}
                          max={criterion.maxScore}
                          step={1}
                          value={[score]}
                          onValueChange={(value) => handleCriterionScoreChange(criterion.evaluationCriterionId, value[0])}
                          disabled={isReadOnly}
                          className="w-full"
                        />
                        <div className="flex justify-between text-xs text-gray-500">
                          <span>Weight: {criterion.weight}%</span>
                          {criterion.minimumScore && (
                            <span>Min Required: {criterion.minimumScore}</span>
                          )}
                        </div>
                        {criterion.criterionDescription && (
                          <p className="text-xs text-gray-400">{criterion.criterionDescription}</p>
                        )}
                      </div>
                    );
                  })}
              </div>

              {/* Total Score */}
              <div className="pt-4 border-t mt-4">
                <div className="flex items-center justify-between">
                  <div>
                    <span className="text-sm font-medium">Total Score</span>
                    {template.passingScore > 0 && (
                      <span className="text-xs text-gray-500 ml-2">
                        (Passing: {template.passingScore}%)
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`text-2xl font-bold ${
                      calculateTotalScore() >= template.passingScore
                        ? 'text-green-600'
                        : 'text-red-600'
                    }`}>
                      {calculateTotalScore().toFixed(2)}%
                    </span>
                    {calculateTotalScore() >= template.passingScore ? (
                      <Badge variant="default" className="bg-green-600">Pass</Badge>
                    ) : (
                      <Badge variant="destructive">Fail</Badge>
                    )}
                  </div>
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Comments */}
      <Card>
        <CardHeader>
          <CardTitle>Evaluation Comments</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="technicalComments">Technical Comments</Label>
            <Textarea
              id="technicalComments"
              placeholder="Provide technical evaluation comments..."
              value={technicalComments}
              onChange={(e) => setTechnicalComments(e.target.value)}
              disabled={isReadOnly}
              rows={4}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="commercialComments">Commercial Comments</Label>
            <Textarea
              id="commercialComments"
              placeholder="Provide commercial evaluation comments..."
              value={commercialComments}
              onChange={(e) => setCommercialComments(e.target.value)}
              disabled={isReadOnly}
              rows={4}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="overallComments">Overall Comments</Label>
            <Textarea
              id="overallComments"
              placeholder="Provide overall evaluation comments..."
              value={overallComments}
              onChange={(e) => setOverallComments(e.target.value)}
              disabled={isReadOnly}
              rows={4}
            />
          </div>
        </CardContent>
      </Card>

      {/* Recommendation */}
      <Card>
        <CardHeader>
          <CardTitle>Recommendation</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center space-x-2">
            <Checkbox
              id="isRecommended"
              checked={isRecommended}
              onCheckedChange={(checked) => setIsRecommended(checked as boolean)}
              disabled={isReadOnly}
            />
            <Label htmlFor="isRecommended" className="text-base font-medium">
              I recommend this bid for award
            </Label>
          </div>

          {isRecommended && (
            <div className="space-y-2">
              <Label htmlFor="recommendation">Recommendation Details</Label>
              <Textarea
                id="recommendation"
                placeholder="Provide detailed recommendation..."
                value={recommendation}
                onChange={(e) => setRecommendation(e.target.value)}
                disabled={isReadOnly}
                rows={4}
              />
            </div>
          )}
        </CardContent>
      </Card>

      {/* Actions */}
      {!isReadOnly && (
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Signed score-sheet lock</CardTitle>
              <CardDescription>
                Submission requires current committee acceptance, COI, signed
                attendance, and confirmed quorum. The exact score snapshot becomes
                immutable; correction requires controlled recall.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="submissionSignature">
                  Evaluator signature reference *
                </Label>
                <Input
                  id="submissionSignature"
                  value={submissionSignature}
                  onChange={(event) =>
                    setSubmissionSignature(event.target.value)
                  }
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="submissionEvidence">
                  Score-sheet evidence reference *
                </Label>
                <Input
                  id="submissionEvidence"
                  value={submissionEvidence}
                  onChange={(event) =>
                    setSubmissionEvidence(event.target.value)
                  }
                />
              </div>
            </CardContent>
          </Card>
          <div className="flex flex-wrap items-center justify-end gap-4">
            <Button
              variant="outline"
              onClick={handleSaveDraft}
              disabled={saving}
            >
              <Save className="h-4 w-4 mr-2" />
              Save Draft
            </Button>
            <Button
              onClick={handleSubmit}
              disabled={
                saving ||
                !submissionSignature.trim() ||
                !submissionEvidence.trim()
              }
            >
              <Send className="h-4 w-4 mr-2" />
              Submit and lock evaluation
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
