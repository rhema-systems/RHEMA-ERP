'use client';

import { useState, useEffect, useMemo, useRef } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Loader2, FileText, CheckCircle } from 'lucide-react';
import { type TenderFormData } from '@/app/procurement/tenders/new/page';
import { evaluationTemplateService, EvaluationTemplate } from '@/services/evaluationTemplateService';
import { getTemplateEvaluationSettings } from '@/lib/tender-evaluation-configuration';

interface BasicInformationProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
  procurementCategory?: string;
  sourceCurrency?: string;
}

const CURRENCIES = ['USD', 'GHS', 'EUR', 'GBP'];

export default function BasicInformation({
  formData,
  updateFormData,
  procurementCategory,
  sourceCurrency,
}: BasicInformationProps) {
  const [evaluationTemplates, setEvaluationTemplates] = useState<EvaluationTemplate[]>([]);
  const [evaluationMethod, setEvaluationMethod] = useState<'Standard' | 'QCBS' | ''>(
    formData.useQCBSEvaluation ? 'QCBS' : formData.evaluationTemplateId ? 'Standard' : ''
  );
  const [loadingTemplates, setLoadingTemplates] = useState(true);
  const [selectedTemplateDetails, setSelectedTemplateDetails] = useState<EvaluationTemplate | null>(null);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [detailsError, setDetailsError] = useState<string | null>(null);
  const [detailsReloadKey, setDetailsReloadKey] = useState(0);
  const updateFormRef = useRef(updateFormData);
  updateFormRef.current = updateFormData;
  const [templateLoadError, setTemplateLoadError] = useState<string | null>(null);
  const [templateReloadKey, setTemplateReloadKey] = useState(0);

  // The existing active endpoint includes the methods needed to filter the list.
  // Never choose a default template before the user has chosen the evaluation method.
  useEffect(() => {
    let cancelled = false;

    const loadTemplates = async () => {
      try {
        setLoadingTemplates(true);
        setTemplateLoadError(null);
        const templates = await evaluationTemplateService.getActive();
        if (cancelled) return;
        setEvaluationTemplates(templates);
      } catch (error) {
        if (cancelled) return;
        console.error('Failed to load evaluation templates:', error);
        setEvaluationTemplates([]);
        setTemplateLoadError(error instanceof Error
          ? error.message
          : 'Evaluation templates could not be loaded.');
      } finally {
        if (!cancelled) setLoadingTemplates(false);
      }
    };
    loadTemplates();

    return () => { cancelled = true; };
  }, [templateReloadKey]);

  const matchingTemplates = useMemo(() => evaluationTemplates.filter(template =>
    template.isActive &&
    (!procurementCategory?.trim() || template.category.trim().toLowerCase() === procurementCategory.trim().toLowerCase()) &&
    template.tenderType.trim().toLowerCase() === formData.tenderType.trim().toLowerCase() &&
    (evaluationMethod === 'QCBS'
      ? template.scoringMethod === 'QCBS'
      : evaluationMethod === 'Standard' && ['WeightedAverage', 'SimpleAverage', 'PassFail'].includes(template.scoringMethod))
  ), [evaluationTemplates, procurementCategory, formData.tenderType, evaluationMethod]);
  const selectedTemplate = matchingTemplates.find(template => template.id === formData.evaluationTemplateId) ?? null;

  // Recheck existing draft selections when the source, type or loaded templates change.
  // Async loading never overrides a more recent method/template choice.
  useEffect(() => {
    if (loadingTemplates || templateLoadError || !formData.evaluationTemplateId) return;
    if (!selectedTemplate) {
      updateFormData({ evaluationTemplateId: null, evaluationTemplateName: '' });
    }
  }, [loadingTemplates, templateLoadError, selectedTemplate, formData.evaluationTemplateId, updateFormData]);

  // Only the detail endpoint includes criterion names. Discard a late response after
  // changing the method, selection or source; never let it restore the old settings.
  useEffect(() => {
    let cancelled = false;
    setSelectedTemplateDetails(null);
    setDetailsError(null);
    setLoadingDetails(Boolean(selectedTemplate));
    if (!selectedTemplate) return;
    const loadDetails = async () => {
      try {
        const details = await evaluationTemplateService.getById(selectedTemplate.id);
        if (cancelled) return;
        if (!details.isActive || details.id !== selectedTemplate.id ||
            details.scoringMethod !== selectedTemplate.scoringMethod ||
            details.category !== selectedTemplate.category || details.tenderType !== selectedTemplate.tenderType) {
          throw new Error('The template configuration changed. Reload templates and select a matching template.');
        }
        setSelectedTemplateDetails(details);
        updateFormRef.current(getTemplateEvaluationSettings(details));
      } catch (error) {
        if (!cancelled) setDetailsError(error instanceof Error ? error.message : 'Template details could not be loaded.');
      } finally {
        if (!cancelled) setLoadingDetails(false);
      }
    };
    void loadDetails();
    return () => { cancelled = true; };
  }, [selectedTemplate, detailsReloadKey]);

  const handleMethodChange = (method: string) => {
    if (method !== 'Standard' && method !== 'QCBS') return;
    if (method === evaluationMethod) return;
    setEvaluationMethod(method);
    updateFormData({
      useQCBSEvaluation: method === 'QCBS',
      evaluationTemplateId: null,
      evaluationTemplateName: '',
    });
  };

  const handleTemplateChange = (templateId: string) => {
    if (templateId === 'none') {
      updateFormData({
        evaluationTemplateId: null,
        evaluationTemplateName: ''
      });
    } else {
      const template = matchingTemplates.find(t => t.id === templateId);
      if (!template) return;
      updateFormData({
        evaluationTemplateId: templateId,
        evaluationTemplateName: template.templateName,
        ...getTemplateEvaluationSettings(template),
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
                {/* RFQ is now handled by the dedicated RFQ module. Keep legacy RFQ value for existing records only. */}
                {formData.tenderType === 'RFQ' && (
                  <SelectItem value="RFQ">RFQ (Legacy)</SelectItem>
                )}
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
              disabled={Boolean(sourceCurrency)}
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
              min={formData.submissionDeadline || undefined}
            />
            <p className="text-xs text-muted-foreground">
              Must be at or after the submission deadline.
            </p>
          </div>
        </div>
      </div>

      {/* Method first, then a compatible template. No independent QCBS switch. */}
      <div>
        <h3 className="text-lg font-semibold mb-4">
          Evaluation Setup <span className="text-red-500">*</span>
        </h3>
        <p className="text-sm text-muted-foreground mb-4">
          Choose the method first, then a matching template. Its scoring settings apply automatically.
        </p>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="evaluationMethod">1. Evaluation Method</Label>
            <Select value={evaluationMethod} onValueChange={handleMethodChange}>
              <SelectTrigger id="evaluationMethod">
                <SelectValue placeholder="Choose an evaluation method" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Standard">Standard (non-QCBS)</SelectItem>
                <SelectItem value="QCBS">QCBS — Quality and Cost-Based Selection</SelectItem>
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Changing the method clears the selected template.
            </p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="evaluationTemplate">2. Evaluation Template</Label>
            {loadingTemplates ? (
              <div className="flex items-center space-x-2 text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span>Loading templates...</span>
              </div>
            ) : templateLoadError ? (
              <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                <p>Evaluation templates could not be loaded.</p>
                <p className="mt-1 text-xs">{templateLoadError}</p>
                <button
                  type="button"
                  className="mt-2 font-medium underline"
                  onClick={() => setTemplateReloadKey(value => value + 1)}
                >
                  Retry
                </button>
              </div>
            ) : (
              <Select
                value={selectedTemplate?.id || 'none'}
                onValueChange={handleTemplateChange}
                disabled={!evaluationMethod || matchingTemplates.length === 0}
              >
                <SelectTrigger id="evaluationTemplate">
                  <SelectValue placeholder="Select an evaluation template" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">{evaluationMethod ? 'Select a matching template' : 'Choose the method first'}</SelectItem>
                  {matchingTemplates.map((template) => (
                    <SelectItem key={template.id} value={template.id}>
                      {template.templateName} ({template.templateCode})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
            {evaluationMethod && !loadingTemplates && !templateLoadError && matchingTemplates.length === 0 && (
              <p className="text-sm text-amber-700">
                No active {evaluationMethod === 'QCBS' ? 'QCBS' : 'non-QCBS'} template matches {procurementCategory || 'this category'} and {formData.tenderType}. Ask Procurement to configure a matching template.
              </p>
            )}
          </div>

          {/* Template Details Preview */}
          {loadingDetails && <p className="text-sm text-muted-foreground">Loading template details...</p>}
          {detailsError && (
            <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
              <p>{detailsError}</p>
              <button type="button" className="mt-2 font-medium underline" onClick={() => {
                setTemplateReloadKey(value => value + 1);
                setDetailsReloadKey(value => value + 1);
              }}>Reload templates</button>
            </div>
          )}
          {selectedTemplateDetails && selectedTemplateDetails.id === selectedTemplate?.id &&
            !loadingDetails && !loadingTemplates && !templateLoadError && !detailsError && (
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
            <Label htmlFor="bidValidityPeriodDays">Bid validity period (calendar days)</Label>
            <Input id="bidValidityPeriodDays" type="number" min={1} step={1}
              value={formData.bidValidityPeriodDays ?? ''}
              onChange={(event) => updateFormData({ bidValidityPeriodDays: event.target.value ? Number(event.target.value) : null })} />
            <p className="text-xs text-gray-500">Enter the period stated in the tender terms, counted from submission closing. It is reviewed with this tender and used to calculate expiry during document binding. No default is assumed.</p>
          </div>
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
