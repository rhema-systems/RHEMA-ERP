'use client';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CheckCircle2, FileText, DollarSign } from 'lucide-react';
import { type TenderFormData } from '@/app/procurement/tenders/new/page';
import { TenderDocumentDto } from '@/services/tenderService';

interface TenderReviewProps {
  formData: TenderFormData;
  onSubmit?: () => void;
  loading?: boolean;
  buttonText?: string;
  tenderDocuments?: TenderDocumentDto[];
}

export default function TenderReview({ formData, onSubmit, loading = false, buttonText = 'Create Tender', tenderDocuments = [] }: TenderReviewProps) {
  // Get proposal template from server documents (support both new and legacy types)
  const proposalTemplate = tenderDocuments.find(doc => doc.documentType === 'ProposalTemplate')
    || tenderDocuments.find(doc => doc.documentType === 'TechnicalProposalTemplate');
  // Get other uploaded documents (non-template documents)
  const uploadedDocuments = tenderDocuments.filter(doc =>
    doc.documentType !== 'ProposalTemplate' &&
    doc.documentType !== 'TechnicalProposalTemplate' &&
    doc.documentType !== 'CommercialProposalTemplate'
  );
  return (
    <div className="space-y-6">
      <div className="bg-green-50 border border-green-200 rounded-lg p-4">
        <div className="flex items-center gap-2">
          <CheckCircle2 className="h-5 w-5 text-green-600" />
          <p className="text-sm text-green-800 font-medium">
            Review your tender details before submitting
          </p>
        </div>
      </div>

      {/* Basic Information */}
      <div className="border rounded-lg p-4">
        <h3 className="text-lg font-semibold mb-4">Basic Information</h3>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <p className="text-sm text-gray-500">Title</p>
            <p className="font-medium">{formData.title}</p>
          </div>
          <div>
            <p className="text-sm text-gray-500">Type</p>
            <Badge>{formData.tenderType}</Badge>
          </div>
          <div>
            <p className="text-sm text-gray-500">Submission Deadline</p>
            <p className="font-medium">{formData.submissionDeadline || 'Not set'}</p>
          </div>
          <div>
            <p className="text-sm text-gray-500">Estimated Value</p>
            <p className="font-medium">
              {formData.estimatedValue 
                ? `${formData.currency} ${formData.estimatedValue.toLocaleString()}`
                : 'Not set'}
            </p>
          </div>
        </div>
        {formData.description && (
          <div className="mt-4">
            <p className="text-sm text-gray-500">Description</p>
            <p className="text-sm">{formData.description}</p>
          </div>
        )}
      </div>

      {/* Evaluation Template */}
      <div className="border rounded-lg p-4">
        <h3 className="text-lg font-semibold mb-4">Evaluation Template</h3>
        {formData.evaluationTemplateId ? (
          <div className="flex items-center space-x-2">
            <span className="text-sm text-gray-500">Selected Template:</span>
            <span className="font-medium">{formData.evaluationTemplateName || 'Template Selected'}</span>
          </div>
        ) : (
          <p className="text-sm text-yellow-600">⚠ No evaluation template selected</p>
        )}
      </div>

      {/* Tender Lots */}
      <div className="border rounded-lg p-4">
        <h3 className="text-lg font-semibold mb-4">Tender Lots</h3>
        <p className="text-sm text-gray-600">
          {formData.items.length} lot(s) added
        </p>
        {formData.items.length > 0 && (
          <div className="mt-4 space-y-2">
            {formData.items.slice(0, 3).map((item, index) => (
              <div key={index} className="text-sm border-l-2 border-blue-500 pl-3">
                <p className="font-medium">{item.description}</p>
                <p className="text-gray-500">
                  Quantity: {item.quantity} {item.unitOfMeasure || ''}
                </p>
              </div>
            ))}
            {formData.items.length > 3 && (
              <p className="text-sm text-gray-500">
                ... and {formData.items.length - 3} more lot(s)
              </p>
            )}
          </div>
        )}
      </div>

      {/* Document Requirements */}
      <div className="border rounded-lg p-4">
        <h3 className="text-lg font-semibold mb-4">Document Requirements</h3>
        <p className="text-sm text-gray-600">
          {formData.documentRequirements.length} document requirement(s) added
          {formData.documentRequirements.length > 0 && (
            <span className="ml-2">
              ({formData.documentRequirements.filter(r => r.isRequired).length} required, {formData.documentRequirements.filter(r => !r.isRequired).length} optional)
            </span>
          )}
        </p>
        {formData.documentRequirements.length > 0 && (
          <div className="mt-4 space-y-2">
            {formData.documentRequirements.slice(0, 5).map((req, index) => (
              <div key={index} className="text-sm border-l-2 border-green-500 pl-3">
                <div className="flex items-center gap-2">
                  <p className="font-medium">{req.documentName}</p>
                  <Badge variant={req.isRequired ? 'destructive' : 'secondary'} className="text-xs">
                    {req.isRequired ? 'Required' : 'Optional'}
                  </Badge>
                </div>
                <p className="text-gray-500 text-xs mt-1">
                  Max: {req.maxFileSizeMB}MB • Types: {req.allowedFileTypes}
                </p>
              </div>
            ))}
            {formData.documentRequirements.length > 5 && (
              <p className="text-sm text-gray-500">
                ... and {formData.documentRequirements.length - 5} more requirement(s)
              </p>
            )}
          </div>
        )}
      </div>

      {/* Uploaded Tender Documents */}
      <div className="border rounded-lg p-4">
        <h3 className="text-lg font-semibold mb-4">Uploaded Documents</h3>
        {uploadedDocuments.length > 0 ? (
          <div className="space-y-2">
            {uploadedDocuments.map((doc) => (
              <div key={doc.id} className="flex items-center gap-2 text-sm">
                <FileText className="h-4 w-4 text-blue-600" />
                <span className="font-medium">{doc.documentName}</span>
                <Badge variant="outline" className="text-xs">{doc.documentType}</Badge>
                <Badge variant="outline" className="text-xs">Uploaded</Badge>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-gray-500">No documents uploaded</p>
        )}
      </div>

      {/* Proposal Template */}
      <div className="border rounded-lg p-4">
        <h3 className="text-lg font-semibold mb-4">Proposal Template</h3>
        {(proposalTemplate || formData.proposalTemplateName) ? (
          <div className="space-y-2">
            {/* Show uploaded proposal template from server */}
            {proposalTemplate && (
              <div className="flex items-center gap-2 text-sm">
                <FileText className="h-4 w-4 text-blue-600" />
                <span className="font-medium">Proposal Template:</span>
                <span className="text-gray-600">{proposalTemplate.documentName}</span>
                <Badge variant="outline" className="text-xs">Uploaded</Badge>
              </div>
            )}
            {/* Show pending proposal template from formData */}
            {!proposalTemplate && formData.proposalTemplateName && (
              <div className="flex items-center gap-2 text-sm">
                <FileText className="h-4 w-4 text-blue-600" />
                <span className="font-medium">Proposal Template:</span>
                <span className="text-gray-600">{formData.proposalTemplateName}</span>
                <Badge variant="outline" className="text-xs text-amber-600">Pending Upload</Badge>
              </div>
            )}
          </div>
        ) : (
          <p className="text-sm text-gray-500">No proposal template uploaded</p>
        )}
      </div>

      {/* Options */}
      <div className="border rounded-lg p-4">
        <h3 className="text-lg font-semibold mb-4">Options</h3>
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.requiresPrequalification}
              disabled
              className="rounded"
            />
            <span className="text-sm">Requires Prequalification</span>
          </div>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.allowPartialBids}
              disabled
              className="rounded"
            />
            <span className="text-sm">Allow Partial Bids</span>
          </div>
        </div>
      </div>

      {/* Submit Button */}
      {onSubmit && (
        <div className="flex justify-center pt-4">
          <Button
            onClick={() => {
              console.log('🟢 TenderReview - Submit button clicked!');
              console.log('🟢 TenderReview - loading:', loading);
              console.log('🟢 TenderReview - formData:', formData);
              onSubmit();
            }}
            disabled={loading}
            size="lg"
            className="px-8"
          >
            {loading ? 'Saving...' : buttonText}
          </Button>
        </div>
      )}
    </div>
  );
}

