'use client';

import React, { useState } from 'react';
import { type TenderFormData, type DocumentRequirement } from '@/app/procurement/tenders/new/page';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Plus, Trash2, FileText, AlertCircle, CheckCircle2, Copy, Upload, X } from 'lucide-react';
import { toast } from 'sonner';

interface TenderDocumentsProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
  tenderId: string | null;
  isEditMode?: boolean; // Optional prop to hide template button on edit page
}

// Predefined document types for quick selection
const COMMON_DOCUMENT_TYPES = [
  { value: 'CompanyRegistration', label: 'Company Registration Certificate' },
  { value: 'TaxClearance', label: 'Tax Clearance Certificate' },
  { value: 'FinancialStatements', label: 'Financial Statements' },
  { value: 'CompanyProfile', label: 'Company Profile' },
  { value: 'ProductBrochures', label: 'Product Brochures/Catalogs' },
  { value: 'QualityCertifications', label: 'Quality Certifications (ISO, etc.)' },
  { value: 'References', label: 'References/Past Performance' },
  { value: 'Insurance', label: 'Insurance Certificates' },
  { value: 'TechnicalProposal', label: 'Technical Proposal' },
  { value: 'CommercialProposal', label: 'Commercial Proposal' },
  { value: 'TenderSecurity', label: 'Tender Security / Bid Bond' },
  { value: 'Custom', label: 'Custom Document Type' },
];

// Templates for common tender types
const DOCUMENT_TEMPLATES = {
  RFQ: [
    { documentType: 'CompanyRegistration', documentName: 'Company Registration Certificate', isRequired: true, description: 'Valid company registration certificate', maxFileSizeMB: 10, allowedFileTypes: 'PDF,JPG,PNG' },
    { documentType: 'TaxClearance', documentName: 'Tax Clearance Certificate', isRequired: true, description: 'Current tax clearance certificate', maxFileSizeMB: 10, allowedFileTypes: 'PDF' },
    { documentType: 'ProductBrochures', documentName: 'Product Brochures/Catalogs', isRequired: false, description: 'Product information and specifications', maxFileSizeMB: 20, allowedFileTypes: 'PDF' },
  ],
  RFP: [
    { documentType: 'CompanyRegistration', documentName: 'Company Registration Certificate', isRequired: true, description: 'Valid company registration certificate', maxFileSizeMB: 10, allowedFileTypes: 'PDF,JPG,PNG' },
    { documentType: 'TaxClearance', documentName: 'Tax Clearance Certificate', isRequired: true, description: 'Current tax clearance certificate', maxFileSizeMB: 10, allowedFileTypes: 'PDF' },
    { documentType: 'FinancialStatements', documentName: 'Audited Financial Statements', isRequired: true, description: 'Last 2 years audited financial statements', maxFileSizeMB: 20, allowedFileTypes: 'PDF' },
    { documentType: 'CompanyProfile', documentName: 'Company Profile', isRequired: true, description: 'Detailed company profile and capabilities', maxFileSizeMB: 15, allowedFileTypes: 'PDF,DOC,DOCX' },
    { documentType: 'TechnicalProposal', documentName: 'Technical Proposal', isRequired: true, description: 'Detailed technical proposal', maxFileSizeMB: 30, allowedFileTypes: 'PDF,DOC,DOCX' },
    { documentType: 'References', documentName: 'References/Past Performance', isRequired: true, description: 'At least 3 references from similar projects', maxFileSizeMB: 15, allowedFileTypes: 'PDF,DOC,DOCX' },
    { documentType: 'QualityCertifications', documentName: 'Quality Certifications', isRequired: false, description: 'ISO or other quality certifications', maxFileSizeMB: 10, allowedFileTypes: 'PDF' },
    { documentType: 'TenderSecurity', documentName: 'Tender Security / Bid Bond', isRequired: false, description: 'Tender security or bid bond if required', maxFileSizeMB: 10, allowedFileTypes: 'PDF' },
  ],
  ITB: [
    { documentType: 'CompanyRegistration', documentName: 'Company Registration Certificate', isRequired: true, description: 'Valid company registration certificate', maxFileSizeMB: 10, allowedFileTypes: 'PDF,JPG,PNG' },
    { documentType: 'TaxClearance', documentName: 'Tax Clearance Certificate', isRequired: true, description: 'Current tax clearance certificate', maxFileSizeMB: 10, allowedFileTypes: 'PDF' },
    { documentType: 'FinancialStatements', documentName: 'Audited Financial Statements', isRequired: true, description: 'Last 2 years audited financial statements', maxFileSizeMB: 20, allowedFileTypes: 'PDF' },
    { documentType: 'Insurance', documentName: 'Insurance Certificates', isRequired: true, description: 'Valid insurance certificates', maxFileSizeMB: 10, allowedFileTypes: 'PDF' },
    { documentType: 'References', documentName: 'References/Past Performance', isRequired: true, description: 'References from similar projects', maxFileSizeMB: 15, allowedFileTypes: 'PDF,DOC,DOCX' },
    { documentType: 'TenderSecurity', documentName: 'Tender Security / Bid Bond', isRequired: true, description: 'Tender security or bid bond as per tender requirements', maxFileSizeMB: 10, allowedFileTypes: 'PDF' },
  ],
  EOI: [
    { documentType: 'CompanyRegistration', documentName: 'Company Registration Certificate', isRequired: true, description: 'Valid company registration certificate', maxFileSizeMB: 10, allowedFileTypes: 'PDF,JPG,PNG' },
    { documentType: 'CompanyProfile', documentName: 'Company Profile', isRequired: true, description: 'Detailed company profile', maxFileSizeMB: 15, allowedFileTypes: 'PDF,DOC,DOCX' },
    { documentType: 'References', documentName: 'References/Past Performance', isRequired: false, description: 'References from past projects', maxFileSizeMB: 15, allowedFileTypes: 'PDF,DOC,DOCX' },
  ],
};

export default function TenderDocuments({ formData, updateFormData, tenderId, isEditMode = false }: TenderDocumentsProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [currentRequirement, setCurrentRequirement] = useState<DocumentRequirement>({
    documentType: '',
    documentName: '',
    isRequired: true,
    description: '',
    maxFileSizeMB: 20,
    allowedFileTypes: 'PDF',
  });

  const handleAddRequirement = () => {
    if (!currentRequirement.documentType || !currentRequirement.documentName) {
      toast.error('Document type and name are required');
      return;
    }

    console.log('TenderDocuments - handleAddRequirement called');
    console.log('TenderDocuments - currentRequirement:', currentRequirement);
    console.log('TenderDocuments - editingIndex:', editingIndex);

    if (editingIndex !== null) {
      // Update existing requirement
      const updated = [...formData.documentRequirements];
      updated[editingIndex] = currentRequirement;
      console.log('TenderDocuments - Updating requirement, new array:', updated);
      updateFormData({ documentRequirements: updated });
      toast.success('Document requirement updated');
      setEditingIndex(null);
    } else {
      // Add new requirement
      const newRequirements = [...formData.documentRequirements, currentRequirement];
      console.log('TenderDocuments - Adding requirement, new array:', newRequirements);
      updateFormData({
        documentRequirements: newRequirements
      });
      toast.success('Document requirement added');
    }

    // Reset form
    setCurrentRequirement({
      documentType: '',
      documentName: '',
      isRequired: true,
      description: '',
      maxFileSizeMB: 20,
      allowedFileTypes: 'PDF',
    });
  };

  const handleEditRequirement = (index: number) => {
    setCurrentRequirement(formData.documentRequirements[index]);
    setEditingIndex(index);
  };

  const handleDeleteRequirement = (index: number) => {
    const updated = formData.documentRequirements.filter((_, i) => i !== index);
    updateFormData({ documentRequirements: updated });
    toast.success('Document requirement removed');

    if (editingIndex === index) {
      setEditingIndex(null);
      setCurrentRequirement({
        documentType: '',
        documentName: '',
        isRequired: true,
        description: '',
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF',
      });
    }
  };

  const handleCancelEdit = () => {
    setEditingIndex(null);
    setCurrentRequirement({
      documentType: '',
      documentName: '',
      isRequired: true,
      description: '',
      maxFileSizeMB: 20,
      allowedFileTypes: 'PDF',
    });
  };

  const handleLoadTemplate = () => {
    const template = DOCUMENT_TEMPLATES[formData.tenderType as keyof typeof DOCUMENT_TEMPLATES];
    if (template) {
      updateFormData({ documentRequirements: template });
      toast.success(`Loaded ${formData.tenderType} template with ${template.length} document requirements`);
    } else {
      toast.error('No template available for this tender type');
    }
  };

  const handleDocumentTypeChange = (value: string) => {
    const commonDoc = COMMON_DOCUMENT_TYPES.find(d => d.value === value);
    if (commonDoc && value !== 'Custom') {
      setCurrentRequirement({
        ...currentRequirement,
        documentType: value,
        documentName: commonDoc.label,
      });
    } else {
      setCurrentRequirement({
        ...currentRequirement,
        documentType: value,
      });
    }
  };

  const handleAcceptanceDeclarationFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate file size (max 20MB)
    const maxSizeBytes = 20 * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      toast.error('File size must be less than 20MB');
      return;
    }

    updateFormData({
      acceptanceDeclarationFile: file,
      acceptanceDeclarationDocumentName: file.name,
    });
    toast.success('Acceptance declaration document selected');
  };

  const handleRemoveAcceptanceDeclaration = () => {
    updateFormData({
      acceptanceDeclarationFile: null,
      acceptanceDeclarationDocumentName: '',
    });
    toast.success('Acceptance declaration document removed');
  };

  return (
    <div className="space-y-6">
      {/* Acceptance Declaration Section */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <FileText className="h-5 w-5 text-purple-600" />
            Supplier Acceptance Declaration (Optional)
          </CardTitle>
          <CardDescription>
            Upload a declaration document that bidders must review and accept before submitting their bid
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-start space-x-3">
            <Checkbox
              id="requiresAcceptanceDeclaration"
              checked={formData.requiresAcceptanceDeclaration}
              onCheckedChange={(checked) => updateFormData({ requiresAcceptanceDeclaration: checked as boolean })}
            />
            <div className="flex-1">
              <Label htmlFor="requiresAcceptanceDeclaration" className="cursor-pointer font-medium">
                Require bidders to accept a declaration document
              </Label>
              <p className="text-sm text-gray-500 mt-1">
                Bidders will be required to download, review, and accept this document before they can proceed with their bid submission
              </p>
            </div>
          </div>

          {formData.requiresAcceptanceDeclaration && (
            <div className="mt-4 p-4 border-2 border-dashed rounded-lg bg-gray-50">
              {!formData.acceptanceDeclarationFile && !formData.acceptanceDeclarationDocumentName ? (
                <div className="text-center">
                  <Upload className="h-8 w-8 mx-auto text-gray-400 mb-2" />
                  <Label htmlFor="acceptanceDeclarationFile" className="cursor-pointer">
                    <span className="text-sm text-blue-600 hover:text-blue-700 font-medium">
                      Click to upload declaration document
                    </span>
                  </Label>
                  <p className="text-xs text-gray-500 mt-1">
                    PDF, DOC, DOCX (Max 20MB)
                  </p>
                  <Input
                    id="acceptanceDeclarationFile"
                    type="file"
                    accept=".pdf,.doc,.docx"
                    onChange={handleAcceptanceDeclarationFileChange}
                    className="hidden"
                  />
                </div>
              ) : (
                <div className="space-y-2">
                  <div className="flex items-center justify-between p-3 border rounded-lg bg-white">
                    <div className="flex items-center gap-2">
                      <FileText className="h-5 w-5 text-green-600" />
                      <div>
                        <p className="text-sm font-medium">{formData.acceptanceDeclarationDocumentName}</p>
                        {formData.acceptanceDeclarationFile && (
                          <p className="text-xs text-gray-500">
                            {(formData.acceptanceDeclarationFile.size / 1024 / 1024).toFixed(2)} MB
                          </p>
                        )}
                        {!formData.acceptanceDeclarationFile && (
                          <p className="text-xs text-green-600">
                            ✓ Already uploaded
                          </p>
                        )}
                      </div>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={handleRemoveAcceptanceDeclaration}
                    >
                      <X className="h-4 w-4 text-red-500" />
                    </Button>
                  </div>
                  {!formData.acceptanceDeclarationFile && (
                    <div className="text-center">
                      <Label htmlFor="acceptanceDeclarationFileReplace" className="cursor-pointer">
                        <span className="text-xs text-blue-600 hover:text-blue-700">
                          Click to replace with a new file
                        </span>
                      </Label>
                      <Input
                        id="acceptanceDeclarationFileReplace"
                        type="file"
                        accept=".pdf,.doc,.docx"
                        onChange={handleAcceptanceDeclarationFileChange}
                        className="hidden"
                      />
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Header with template button */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">Document Requirements</h3>
          <p className="text-sm text-gray-500">
            Specify which documents bidders must upload when submitting their bids
          </p>
        </div>
        {/* Existing requirements must not be overwritten from edit mode. */}
        {(!isEditMode || formData.documentRequirements.length === 0) && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={handleLoadTemplate}
          >
            <Copy className="h-4 w-4 mr-2" />
            Load Template
          </Button>
        )}
      </div>

      {/* Add/Edit Document Requirement Form */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {editingIndex !== null ? 'Edit Document Requirement' : 'Add Document Requirement'}
          </CardTitle>
          <CardDescription>
            Define the documents that bidders must submit with their bids
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            {/* Document Type */}
            <div className="space-y-2">
              <Label htmlFor="documentType">Document Type *</Label>
              <Select
                value={currentRequirement.documentType}
                onValueChange={handleDocumentTypeChange}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select document type" />
                </SelectTrigger>
                <SelectContent>
                  {COMMON_DOCUMENT_TYPES.map((type) => (
                    <SelectItem key={type.value} value={type.value}>
                      {type.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Document Name */}
            <div className="space-y-2">
              <Label htmlFor="documentName">Document Name *</Label>
              <Input
                id="documentName"
                value={currentRequirement.documentName}
                onChange={(e) => setCurrentRequirement({ ...currentRequirement, documentName: e.target.value })}
                placeholder="e.g., Company Registration Certificate"
              />
            </div>
          </div>

          {/* Description */}
          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              value={currentRequirement.description}
              onChange={(e) => setCurrentRequirement({ ...currentRequirement, description: e.target.value })}
              placeholder="Provide additional details about this document requirement"
              rows={2}
            />
          </div>

          <div className="grid grid-cols-3 gap-4">
            {/* Required/Optional */}
            <div className="space-y-2">
              <Label htmlFor="isRequired">Requirement Status *</Label>
              <Select
                value={currentRequirement.isRequired ? 'required' : 'optional'}
                onValueChange={(value) => setCurrentRequirement({ ...currentRequirement, isRequired: value === 'required' })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="required">Required</SelectItem>
                  <SelectItem value="optional">Optional</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Max File Size */}
            <div className="space-y-2">
              <Label htmlFor="maxFileSizeMB">Max File Size (MB) *</Label>
              <Input
                id="maxFileSizeMB"
                type="number"
                min="1"
                max="100"
                value={currentRequirement.maxFileSizeMB}
                onChange={(e) => setCurrentRequirement({ ...currentRequirement, maxFileSizeMB: parseInt(e.target.value) || 20 })}
              />
            </div>

            {/* Allowed File Types */}
            <div className="space-y-2">
              <Label htmlFor="allowedFileTypes">Allowed File Types *</Label>
              <Input
                id="allowedFileTypes"
                value={currentRequirement.allowedFileTypes}
                onChange={(e) => setCurrentRequirement({ ...currentRequirement, allowedFileTypes: e.target.value })}
                placeholder="e.g., PDF,DOC,DOCX"
              />
            </div>
          </div>

          {/* Action Buttons */}
          <div className="flex gap-2">
            <Button
              type="button"
              onClick={handleAddRequirement}
            >
              <Plus className="h-4 w-4 mr-2" />
              {editingIndex !== null ? 'Update Requirement' : 'Add Requirement'}
            </Button>
            {editingIndex !== null && (
              <Button
                type="button"
                variant="outline"
                onClick={handleCancelEdit}
              >
                Cancel
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* List of Document Requirements */}
      {formData.documentRequirements.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Document Requirements List ({formData.documentRequirements.length})
            </CardTitle>
            <CardDescription>
              {formData.documentRequirements.filter(r => r.isRequired).length} required, {formData.documentRequirements.filter(r => !r.isRequired).length} optional
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {formData.documentRequirements.map((req, index) => (
                <div
                  key={index}
                  className={`p-4 border rounded-lg ${
                    editingIndex === index ? 'border-blue-500 bg-blue-50' : 'hover:bg-gray-50'
                  }`}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex items-start gap-3 flex-1">
                      {req.isRequired ? (
                        <AlertCircle className="h-5 w-5 text-red-500 mt-0.5" />
                      ) : (
                        <CheckCircle2 className="h-5 w-5 text-gray-400 mt-0.5" />
                      )}
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <p className="font-medium">{req.documentName}</p>
                          <span className={`text-xs px-2 py-0.5 rounded-full ${
                            req.isRequired
                              ? 'bg-red-100 text-red-700'
                              : 'bg-gray-100 text-gray-700'
                          }`}>
                            {req.isRequired ? 'Required' : 'Optional'}
                          </span>
                        </div>
                        <p className="text-sm text-gray-600 mt-1">
                          Type: <span className="font-medium">{req.documentType}</span>
                        </p>
                        {req.description && (
                          <p className="text-sm text-gray-500 mt-1">{req.description}</p>
                        )}
                        <div className="flex gap-4 mt-2 text-xs text-gray-500">
                          <span>Max Size: {req.maxFileSizeMB}MB</span>
                          <span>Allowed: {req.allowedFileTypes}</span>
                        </div>
                      </div>
                    </div>
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => handleEditRequirement(index)}
                      >
                        <FileText className="h-4 w-4" />
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDeleteRequirement(index)}
                      >
                        <Trash2 className="h-4 w-4 text-red-500" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      ) : (
        <Card className="bg-gray-50">
          <CardContent className="py-12">
            <div className="text-center text-gray-500">
              <FileText className="h-12 w-12 mx-auto mb-3 text-gray-400" />
              <p className="font-medium">No document requirements added yet</p>
              <p className="text-sm mt-1">
                Add document requirements above or load a template to get started
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Info Box */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <div className="flex gap-3">
          <AlertCircle className="h-5 w-5 text-blue-600 flex-shrink-0 mt-0.5" />
          <div className="text-sm text-blue-800">
            <p className="font-medium mb-1">About Document Requirements</p>
            <ul className="space-y-1 list-disc list-inside">
              <li>Document requirements are optional - you can skip this step if not needed</li>
              <li>Required documents must be uploaded by bidders before they can submit their bid</li>
              <li>Optional documents can be skipped by bidders</li>
              <li>File type restrictions help ensure bidders upload compatible formats</li>
              <li>You can edit or remove requirements at any time before publishing the tender</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
