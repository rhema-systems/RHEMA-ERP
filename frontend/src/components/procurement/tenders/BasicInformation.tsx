'use client';

import { useState, useEffect } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Loader2, FileText, CheckCircle } from 'lucide-react';
import { type TenderFormData } from '@/app/procurement/tenders/new/page';
import { evaluationTemplateService, EvaluationTemplate, EvaluationTemplateListItem } from '@/services/evaluationTemplateService';

interface BasicInformationProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
}

const CURRENCIES = ['USD', 'GHS', 'EUR', 'GBP'];

export default function BasicInformation({ formData, updateFormData }: BasicInformationProps) {
  const [evaluationTemplates, setEvaluationTemplates] = useState<EvaluationTemplateListItem[]>([]);
  const [selectedTemplateDetails, setSelectedTemplateDetails] = useState<EvaluationTemplate | null>(null);
  const [loadingTemplates, setLoadingTemplates] = useState(true);
  const [loadingDetails, setLoadingDetails] = useState(false);

  // Load evaluation templates on mount
  useEffect(() => {
    const loadTemplates = async () => {
      try {
        setLoadingTemplates(true);
        const templates = await evaluationTemplateService.getForDropdown();
        setEvaluationTemplates(templates);
      } catch (error) {
        console.error('Failed to load evaluation templates:', error);
      } finally {
        setLoadingTemplates(false);
      }
    };
    loadTemplates();
  }, []);

  // Load template details when selection changes and auto-populate QCBS settings
  useEffect(() => {
    const loadTemplateDetails = async () => {
      if (!formData.evaluationTemplateId) {
        setSelectedTemplateDetails(null);
        return;
      }
      try {
        setLoadingDetails(true);
        const details = await evaluationTemplateService.getById(formData.evaluationTemplateId);
        setSelectedTemplateDetails(details);

        // Auto-populate QCBS settings if template uses QCBS scoring method
        if (details.scoringMethod === 'QCBS') {
          updateFormData({
            useQCBSEvaluation: true,
            technicalWeight: details.technicalWeight,
            financialWeight: details.financialWeight,
            minimumTechnicalScore: details.minimumTechnicalScore
          });
        }
      } catch (error) {
        console.error('Failed to load template details:', error);
        setSelectedTemplateDetails(null);
      } finally {
        setLoadingDetails(false);
      }
    };
    loadTemplateDetails();
  }, [formData.evaluationTemplateId]);

  const handleTemplateChange = (templateId: string) => {
    if (templateId === 'none') {
      updateFormData({
        evaluationTemplateId: null,
        evaluationTemplateName: ''
      });
    } else {
      const template = evaluationTemplates.find(t => t.id === templateId);
      updateFormData({
        evaluationTemplateId: templateId,
        evaluationTemplateName: template?.templateName || ''
      });
    }
  };

  return (
    <div className="space-y-6">
      {/* Required Fields Notice */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <p className="text-sm text-blue-800">
          <span className="text-red-500 font-bold">*</span> indicates required fields
        </p>
      </div>

      {/* Basic Details */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Basic Details</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Title */}
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="title">
              Tender Title <span className="text-red-500">*</span>
            </Label>
            <Input
              id="title"
              value={formData.title}
              onChange={(e) => updateFormData({ title: e.target.value })}
              placeholder="Enter tender title"
              required
            />
          </div>

          {/* Description */}
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              value={formData.description}
              onChange={(e) => updateFormData({ description: e.target.value })}
              placeholder="Enter tender description"
              rows={4}
            />
          </div>

          {/* Tender Type */}
          <div className="space-y-2">
            <Label htmlFor="tenderType">
              Tender Type <span className="text-red-500">*</span>
            </Label>
            <Select
              value={formData.tenderType}
              onValueChange={(value) => updateFormData({ tenderType: value })}
            >
              <SelectTrigger id="tenderType">
                <SelectValue placeholder="Select tender type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="RFQ">RFQ - Request for Quotation</SelectItem>
                <SelectItem value="RFP">RFP - Request for Proposal</SelectItem>
                <SelectItem value="ITB">ITB - Invitation to Bid</SelectItem>
                <SelectItem value="EOI">EOI - Expression of Interest</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Currency */}
          <div className="space-y-2">
            <Label htmlFor="currency">Currency</Label>
            <Select
              value={formData.currency}
              onValueChange={(value) => updateFormData({ currency: value })}
            >
              <SelectTrigger id="currency">
                <SelectValue placeholder="Select currency" />
              </SelectTrigger>
              <SelectContent>
                {CURRENCIES.map((curr) => (
                  <SelectItem key={curr} value={curr}>
                    {curr}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Estimated Value */}
          <div className="space-y-2">
            <Label htmlFor="estimatedValue">Estimated Value</Label>
            <Input
              id="estimatedValue"
              type="number"
              value={formData.estimatedValue || ''}
              onChange={(e) => updateFormData({ estimatedValue: e.target.value ? parseFloat(e.target.value) : null })}
              placeholder="Enter estimated value"
              min="0"
              step="0.01"
            />
          </div>

          {/* Minimum Performance Rating */}
          <div className="space-y-2">
            <Label htmlFor="minimumPerformanceRating">Minimum Performance Rating (0-5 stars)</Label>
            <Input
              id="minimumPerformanceRating"
              type="number"
              value={formData.minimumPerformanceRating || ''}
              onChange={(e) => updateFormData({ minimumPerformanceRating: e.target.value ? parseFloat(e.target.value) : null })}
              placeholder="Enter rating (0-5)"
              min="0"
              max="5"
              step="0.1"
            />
            <p className="text-xs text-gray-500">Minimum supplier rating required to bid (0 = no minimum, 5 = highest)</p>
          </div>
        </div>
      </div>

      {/* Dates */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Important Dates</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Submission Deadline */}
          <div className="space-y-2">
            <Label htmlFor="submissionDeadline">
              Submission Deadline <span className="text-red-500">*</span>
            </Label>
            <Input
              id="submissionDeadline"
              type="datetime-local"
              value={formData.submissionDeadline}
              onChange={(e) => updateFormData({ submissionDeadline: e.target.value })}
              required
            />
          </div>

          {/* Opening Date */}
          <div className="space-y-2">
            <Label htmlFor="openingDate">Opening Date</Label>
            <Input
              id="openingDate"
              type="datetime-local"
              value={formData.openingDate}
              onChange={(e) => updateFormData({ openingDate: e.target.value })}
            />
          </div>
        </div>
      </div>

      {/* Evaluation Template Selection */}
      <div>
        <h3 className="text-lg font-semibold mb-4">
          Evaluation Template <span className="text-red-500">*</span>
        </h3>
        <p className="text-sm text-muted-foreground mb-4">
          Select an evaluation template that defines the criteria and weights for bid evaluation.
        </p>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="evaluationTemplate">Select Template</Label>
            {loadingTemplates ? (
              <div className="flex items-center space-x-2 text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span>Loading templates...</span>
              </div>
            ) : (
              <Select
                value={formData.evaluationTemplateId || 'none'}
                onValueChange={handleTemplateChange}
              >
                <SelectTrigger id="evaluationTemplate">
                  <SelectValue placeholder="Select an evaluation template" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">-- No Template Selected --</SelectItem>
                  {evaluationTemplates.map((template) => (
                    <SelectItem key={template.id} value={template.id}>
                      {template.templateName} ({template.templateCode})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>

          {/* Template Details Preview */}
          {loadingDetails && (
            <div className="flex items-center space-x-2 text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span>Loading template details...</span>
            </div>
          )}

          {selectedTemplateDetails && !loadingDetails && (
            <Card className="bg-muted/50">
              <CardContent className="pt-4">
                <div className="flex items-start justify-between mb-4">
                  <div className="flex items-center space-x-2">
                    <FileText className="h-5 w-5 text-primary" />
                    <div>
                      <h4 className="font-semibold">{selectedTemplateDetails.templateName}</h4>
                      <p className="text-sm text-muted-foreground">{selectedTemplateDetails.templateCode}</p>
                    </div>
                  </div>
                  <div className="flex items-center space-x-2">
                    <Badge>{selectedTemplateDetails.category}</Badge>
                    <Badge variant="secondary">{selectedTemplateDetails.tenderType}</Badge>
                    <Badge variant="outline">{selectedTemplateDetails.scoringMethod}</Badge>
                  </div>
                </div>

                {selectedTemplateDetails.description && (
                  <p className="text-sm text-muted-foreground mb-4">{selectedTemplateDetails.description}</p>
                )}

                <div className="grid grid-cols-2 gap-4 mb-4">
                  <div className="text-sm">
                    <span className="font-medium">Passing Score:</span> {selectedTemplateDetails.passingScore}%
                  </div>
                  <div className="text-sm">
                    <span className="font-medium">Total Weight:</span> {selectedTemplateDetails.totalWeight}%
                  </div>
                </div>

                {selectedTemplateDetails.scoringMethod === 'QCBS' && (
                  <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 mb-4">
                    <h5 className="font-medium text-sm text-blue-800 mb-2">QCBS Configuration (Auto-applied)</h5>
                    <div className="grid grid-cols-3 gap-4 text-sm">
                      <div>
                        <span className="text-blue-700">Technical Weight:</span> {selectedTemplateDetails.technicalWeight}%
                      </div>
                      <div>
                        <span className="text-blue-700">Financial Weight:</span> {selectedTemplateDetails.financialWeight}%
                      </div>
                      <div>
                        <span className="text-blue-700">Min Technical Score:</span> {selectedTemplateDetails.minimumTechnicalScore}%
                      </div>
                    </div>
                  </div>
                )}

                <div className="space-y-2">
                  <h5 className="font-medium text-sm">Evaluation Criteria ({selectedTemplateDetails.criteriaCount})</h5>
                  <div className="grid gap-2">
                    {selectedTemplateDetails.criteria.map((criterion, index) => (
                      <div key={index} className="flex items-center justify-between bg-background rounded-md p-2 text-sm">
                        <div className="flex items-center space-x-2">
                          <CheckCircle className="h-4 w-4 text-green-500" />
                          <span>{criterion.criterionName}</span>
                          {criterion.isMandatory && (
                            <Badge variant="destructive" className="text-xs">Required</Badge>
                          )}
                        </div>
                        <div className="flex items-center space-x-4 text-muted-foreground">
                          <span>Weight: {criterion.weight}%</span>
                          <span>Max: {criterion.maxScore}</span>
                          {criterion.minimumScore !== undefined && criterion.minimumScore > 0 && (
                            <span>Min: {criterion.minimumScore}</span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {!formData.evaluationTemplateId && !loadingTemplates && (
            <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
              <p className="text-sm text-yellow-800">
                ⚠ Please select an evaluation template. This defines how bids will be scored and evaluated.
              </p>
            </div>
          )}
        </div>
      </div>

      {/* QCBS Evaluation Settings */}
      <div>
        <h3 className="text-lg font-semibold mb-4">QCBS Evaluation Settings</h3>
        <p className="text-sm text-muted-foreground mb-4">
          Quality and Cost-Based Selection (QCBS) evaluates bids based on both technical merit and financial proposal.
        </p>

        <div className="space-y-4">
          <div className="flex items-center space-x-2">
            <Checkbox
              id="useQCBSEvaluation"
              checked={formData.useQCBSEvaluation}
              onCheckedChange={(checked) => updateFormData({ useQCBSEvaluation: checked as boolean })}
            />
            <Label htmlFor="useQCBSEvaluation" className="cursor-pointer font-medium">
              Use QCBS Evaluation Method
            </Label>
          </div>

          {formData.useQCBSEvaluation && (
            <Card className="bg-blue-50/50 border-blue-200">
              <CardContent className="pt-4">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  {/* Technical Weight */}
                  <div className="space-y-2">
                    <Label htmlFor="technicalWeight">Technical Weight (%)</Label>
                    <Input
                      id="technicalWeight"
                      type="number"
                      value={formData.technicalWeight}
                      onChange={(e) => {
                        const techWeight = Math.min(100, Math.max(0, parseInt(e.target.value) || 0));
                        updateFormData({
                          technicalWeight: techWeight,
                          financialWeight: 100 - techWeight
                        });
                      }}
                      min="0"
                      max="100"
                      step="5"
                    />
                    <p className="text-xs text-muted-foreground">Weight for technical score (0-100)</p>
                  </div>

                  {/* Financial Weight */}
                  <div className="space-y-2">
                    <Label htmlFor="financialWeight">Financial Weight (%)</Label>
                    <Input
                      id="financialWeight"
                      type="number"
                      value={formData.financialWeight}
                      onChange={(e) => {
                        const finWeight = Math.min(100, Math.max(0, parseInt(e.target.value) || 0));
                        updateFormData({
                          financialWeight: finWeight,
                          technicalWeight: 100 - finWeight
                        });
                      }}
                      min="0"
                      max="100"
                      step="5"
                    />
                    <p className="text-xs text-muted-foreground">Weight for financial score (0-100)</p>
                  </div>

                  {/* Minimum Technical Score */}
                  <div className="space-y-2">
                    <Label htmlFor="minimumTechnicalScore">Minimum Technical Score (%)</Label>
                    <Input
                      id="minimumTechnicalScore"
                      type="number"
                      value={formData.minimumTechnicalScore}
                      onChange={(e) => updateFormData({ minimumTechnicalScore: parseInt(e.target.value) || 0 })}
                      min="0"
                      max="100"
                      step="5"
                    />
                    <p className="text-xs text-muted-foreground">Bids below this score are disqualified</p>
                  </div>
                </div>

                {/* Summary */}
                <div className="mt-4 p-3 bg-white rounded-lg border">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium">Weight Distribution:</span>
                    <span className="text-sm">
                      Technical: <strong>{formData.technicalWeight}%</strong> | Financial: <strong>{formData.financialWeight}%</strong>
                    </span>
                  </div>
                  {formData.technicalWeight + formData.financialWeight !== 100 && (
                    <p className="text-xs text-red-600 mt-1">
                      ⚠ Weights must total 100% (currently {formData.technicalWeight + formData.financialWeight}%)
                    </p>
                  )}
                </div>

                {/* QCBS Formula Info */}
                <div className="mt-4 p-3 bg-gray-50 rounded-lg text-xs text-muted-foreground">
                  <strong>QCBS Formula:</strong> Combined Score = (Technical Score × {formData.technicalWeight}%) + (Financial Score × {formData.financialWeight}%)
                  <br />
                  Financial Score is calculated as: (Lowest Bid / Bidder&apos;s Price) × 100
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      {/* Options */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Options</h3>
        <div className="space-y-4">
          <div className="flex items-center space-x-2">
            <Checkbox
              id="requiresPrequalification"
              checked={formData.requiresPrequalification}
              onCheckedChange={(checked) => updateFormData({ requiresPrequalification: checked as boolean })}
            />
            <Label htmlFor="requiresPrequalification" className="cursor-pointer">
              Requires Prequalification
            </Label>
          </div>

          <div className="flex items-center space-x-2">
            <Checkbox
              id="allowPartialBids"
              checked={formData.allowPartialBids}
              onCheckedChange={(checked) => updateFormData({ allowPartialBids: checked as boolean })}
            />
            <Label htmlFor="allowPartialBids" className="cursor-pointer">
              Allow Partial Bids
            </Label>
          </div>
        </div>
      </div>

      {/* Additional Information */}
      <div>
        <h3 className="text-lg font-semibold mb-4">Additional Information</h3>
        <div className="space-y-4">
          {/* Notes */}
          <div className="space-y-2">
            <Label htmlFor="notes">Internal Notes</Label>
            <Textarea
              id="notes"
              value={formData.notes}
              onChange={(e) => updateFormData({ notes: e.target.value })}
              placeholder="Enter internal notes (not visible to bidders)"
              rows={3}
            />
          </div>

          {/* Terms and Conditions */}
          <div className="space-y-2">
            <Label htmlFor="termsAndConditions">Terms and Conditions</Label>
            <Textarea
              id="termsAndConditions"
              value={formData.termsAndConditions}
              onChange={(e) => updateFormData({ termsAndConditions: e.target.value })}
              placeholder="Enter terms and conditions"
              rows={4}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
