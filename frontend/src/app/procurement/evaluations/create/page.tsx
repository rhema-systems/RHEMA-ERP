'use client';

import { Suspense, useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Slider } from '@/components/ui/slider';
import { ArrowLeft, Save, Send, Clock, XCircle, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderEvaluationService from '@/services/tenderEvaluationService';
import * as tenderBidService from '@/services/tenderBidService';
import { evaluationTemplateService, type EvaluationTemplate, type EvaluationTemplateCriterion } from '@/services/evaluationTemplateService';
import { type CreateEvaluationDto } from '@/services/tenderEvaluationService';
import { type TenderBidDetailDto } from '@/services/tenderBidService';

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

export default function CreateEvaluationPage() {
  return (
    <Suspense fallback={
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading...</p>
        </div>
      </div>
    }>
      <CreateEvaluationContent />
    </Suspense>
  );
}

function CreateEvaluationContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const bidId = searchParams?.get('bidId') || '';

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

  useEffect(() => {
    if (bidId) {
      loadBidData();
    }
  }, [bidId]);

  const loadBidData = async () => {
    try {
      setLoading(true);
      const bidData = await tenderBidService.getBidById(bidId);
      setBid(bidData);

      // Load evaluation template if assigned
      if (bidData.evaluationTemplateId) {
        try {
          const templateData = await evaluationTemplateService.getById(bidData.evaluationTemplateId);
          setTemplate(templateData);

          // Initialize scores for each criterion to 0
          const initialScores: Record<string, number> = {};
          templateData.criteria?.forEach(criterion => {
            initialScores[criterion.evaluationCriterionId] = 0;
          });
          setCriteriaScores(initialScores);
        } catch (templateError) {
          console.error('Error loading evaluation template:', templateError);
          toast.error('Failed to load evaluation template');
        }
      }
    } catch (error) {
      console.error('Error loading bid data:', error);
      toast.error('Failed to load bid data');
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
      // Calculate weighted average
      let totalWeightedScore = 0;
      let totalWeight = 0;

      template.criteria.forEach(criterion => {
        const score = criteriaScores[criterion.evaluationCriterionId] || 0;
        const normalizedScore = (score / criterion.maxScore) * 100; // Normalize to 0-100
        totalWeightedScore += normalizedScore * (criterion.weight / 100);
        totalWeight += criterion.weight;
      });

      return totalWeight > 0 ? totalWeightedScore : 0;
    } else if (template.scoringMethod === 'SimpleAverage') {
      // Calculate simple average
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
      // PassFail - check if all mandatory criteria meet minimum
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
      const totalScore = calculateTotalScore();

      const data: CreateEvaluationDto = {
        tenderBidId: bidId,
        evaluationCriteriaJson: buildEvaluationCriteriaJson(),
        technicalComments,
        commercialComments,
        overallComments,
        isRecommended,
        recommendation,
      };

      const evaluation = await tenderEvaluationService.createEvaluation(data);
      toast.success('Evaluation created as draft');
      router.push(`/procurement/evaluations/${evaluation.id}`);
    } catch (error) {
      console.error('Error creating evaluation:', error);
      toast.error('Failed to create evaluation');
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = async () => {
    try {
      setSaving(true);
      const totalScore = calculateTotalScore();

      const data: CreateEvaluationDto = {
        tenderBidId: bidId,
        evaluationCriteriaJson: buildEvaluationCriteriaJson(),
        technicalComments,
        commercialComments,
        overallComments,
        isRecommended,
        recommendation,
      };

      const evaluation = await tenderEvaluationService.createEvaluation(data);
      toast.success('Evaluation submitted successfully');
      router.push(`/procurement/bids/${bidId}`);
    } catch (error) {
      console.error('Error submitting evaluation:', error);
      toast.error('Failed to submit evaluation');
    } finally {
      setSaving(false);
    }
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
          <Button onClick={() => router.back()} className="mt-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Go Back
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
          <Button variant="ghost" onClick={() => router.back()}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Create Bid Evaluation</h1>
            <p className="text-gray-500">Evaluate and score the bid</p>
          </div>
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
                {bid.currency} {bid.totalBidAmount?.toLocaleString()}
              </p>
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
                No evaluation template assigned to this tender. Please assign an evaluation template to the tender first.
              </p>
            </div>
          ) : template.criteria?.length === 0 ? (
            <div className="flex items-center gap-2 p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
              <AlertCircle className="h-5 w-5 text-yellow-600" />
              <p className="text-sm text-yellow-800">
                The evaluation template has no criteria defined. Please add criteria to the template.
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
          <CardTitle>Comments & Recommendation</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="technicalComments">Technical Comments</Label>
            <Textarea
              id="technicalComments"
              placeholder="Enter technical evaluation comments..."
              value={technicalComments}
              onChange={(e) => setTechnicalComments(e.target.value)}
              rows={4}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="commercialComments">Commercial Comments</Label>
            <Textarea
              id="commercialComments"
              placeholder="Enter commercial evaluation comments..."
              value={commercialComments}
              onChange={(e) => setCommercialComments(e.target.value)}
              rows={4}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="overallComments">Overall Comments</Label>
            <Textarea
              id="overallComments"
              placeholder="Enter overall evaluation comments..."
              value={overallComments}
              onChange={(e) => setOverallComments(e.target.value)}
              rows={4}
            />
          </div>

          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <Checkbox
                id="isRecommended"
                checked={isRecommended}
                onCheckedChange={(checked) => setIsRecommended(checked as boolean)}
              />
              <Label htmlFor="isRecommended" className="cursor-pointer">
                Recommend this bid for award
              </Label>
            </div>
          </div>

          {isRecommended && (
            <div className="space-y-2">
              <Label htmlFor="recommendation">Recommendation Details</Label>
              <Textarea
                id="recommendation"
                placeholder="Explain why this bid should be awarded..."
                value={recommendation}
                onChange={(e) => setRecommendation(e.target.value)}
                rows={4}
              />
            </div>
          )}
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex items-center justify-end gap-4">
        <Button
          variant="outline"
          onClick={() => router.back()}
          disabled={saving}
        >
          Cancel
        </Button>
        <Button
          variant="outline"
          onClick={handleSaveDraft}
          disabled={saving}
        >
          <Save className="h-4 w-4 mr-2" />
          Save as Draft
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={saving}
        >
          <Send className="h-4 w-4 mr-2" />
          {saving ? 'Submitting...' : 'Submit Evaluation'}
        </Button>
      </div>
    </div>
  );
}
