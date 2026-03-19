'use client';

import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { FileText, DollarSign, Shield, Upload, X, CheckCircle, Loader2, Download } from 'lucide-react';
import { type CreateTenderBidDto, type TenderBidDocumentDto } from '@/services/tenderBidService';
import { type TenderDocumentDto } from '@/services/tenderService';
import { toast } from 'sonner';

interface BidProposalsStepProps {
  bidData: CreateTenderBidDto;
  updateBidData: (updates: Partial<CreateTenderBidDto>) => void;
  bidId?: string;
  uploadedDocuments?: TenderBidDocumentDto[];
  tenderDocuments?: TenderDocumentDto[];
  onDocumentUpload?: (file: File, documentType: string) => Promise<void>;
  onDocumentDelete?: (documentId: string) => Promise<void>;
}

export default function BidProposalsStep({
  bidData,
  updateBidData,
  bidId,
  uploadedDocuments = [],
  tenderDocuments = [],
  onDocumentUpload,
  onDocumentDelete
}: BidProposalsStepProps) {
  const [selectedTechnicalFile, setSelectedTechnicalFile] = useState<File | null>(null);
  const [selectedCommercialFile, setSelectedCommercialFile] = useState<File | null>(null);
  const [uploadingTechnical, setUploadingTechnical] = useState(false);
  const [uploadingCommercial, setUploadingCommercial] = useState(false);

  // Get uploaded proposal documents
  const technicalProposalDoc = uploadedDocuments.find(doc => doc.documentType === 'TechnicalProposal');
  const commercialProposalDoc = uploadedDocuments.find(doc => doc.documentType === 'CommercialProposal');

  // Get proposal template from tender documents (single template for both proposals)
  // Support both new 'ProposalTemplate' and legacy 'TechnicalProposalTemplate' for backward compatibility
  const proposalTemplate = tenderDocuments.find(doc => doc.documentType === 'ProposalTemplate')
    || tenderDocuments.find(doc => doc.documentType === 'TechnicalProposalTemplate');

  const handleDownloadTemplate = (documentUrl: string, documentName: string) => {
    const link = document.createElement('a');
    link.href = documentUrl;
    link.download = documentName;
    link.target = '_blank';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleTechnicalFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Validate file size (max 50MB)
    const maxSizeMB = 50;
    const maxSizeBytes = maxSizeMB * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      toast.error(`File size must be less than ${maxSizeMB}MB`);
      return;
    }

    // Validate file type
    const allowedExtensions = ['pdf', 'doc', 'docx'];
    const fileExtension = file.name.split('.').pop()?.toLowerCase() || '';
    if (!allowedExtensions.includes(fileExtension)) {
      toast.error('Only PDF, DOC, and DOCX files are allowed');
      return;
    }

    setSelectedTechnicalFile(file);
  };

  const handleCommercialFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Validate file size (max 50MB)
    const maxSizeMB = 50;
    const maxSizeBytes = maxSizeMB * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      toast.error(`File size must be less than ${maxSizeMB}MB`);
      return;
    }

    // Validate file type
    const allowedExtensions = ['pdf', 'doc', 'docx'];
    const fileExtension = file.name.split('.').pop()?.toLowerCase() || '';
    if (!allowedExtensions.includes(fileExtension)) {
      toast.error('Only PDF, DOC, and DOCX files are allowed');
      return;
    }

    setSelectedCommercialFile(file);
  };

  const handleUploadTechnical = async () => {
    if (!selectedTechnicalFile || !onDocumentUpload) {
      toast.error('Please select a file first');
      return;
    }

    try {
      setUploadingTechnical(true);
      await onDocumentUpload(selectedTechnicalFile, 'TechnicalProposal');
      setSelectedTechnicalFile(null);
      toast.success('Technical proposal uploaded successfully');
    } catch (error) {
      console.error('Error uploading technical proposal:', error);
      toast.error('Failed to upload technical proposal');
    } finally {
      setUploadingTechnical(false);
    }
  };

  const handleUploadCommercial = async () => {
    if (!selectedCommercialFile || !onDocumentUpload) {
      toast.error('Please select a file first');
      return;
    }

    try {
      setUploadingCommercial(true);
      await onDocumentUpload(selectedCommercialFile, 'CommercialProposal');
      setSelectedCommercialFile(null);
      toast.success('Commercial proposal uploaded successfully');
    } catch (error) {
      console.error('Error uploading commercial proposal:', error);
      toast.error('Failed to upload commercial proposal');
    } finally {
      setUploadingCommercial(false);
    }
  };

  const handleDeleteTechnical = async () => {
    if (!technicalProposalDoc || !onDocumentDelete) return;

    try {
      await onDocumentDelete(technicalProposalDoc.id);
      toast.success('Technical proposal deleted');
    } catch (error) {
      console.error('Error deleting technical proposal:', error);
      toast.error('Failed to delete technical proposal');
    }
  };

  const handleDeleteCommercial = async () => {
    if (!commercialProposalDoc || !onDocumentDelete) return;

    try {
      await onDocumentDelete(commercialProposalDoc.id);
      toast.success('Commercial proposal deleted');
    } catch (error) {
      console.error('Error deleting commercial proposal:', error);
      toast.error('Failed to delete commercial proposal');
    }
  };
  return (
    <div className="space-y-6">
      {/* Technical Proposal */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5 text-blue-600" />
            Technical Proposal
          </CardTitle>
          <CardDescription>
            Upload a document or type your technical approach, methodology, and capabilities
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Template Download Section */}
          {proposalTemplate && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <FileText className="h-4 w-4 text-blue-600" />
                  <div>
                    <p className="text-sm font-medium text-blue-900">Proposal Template Available</p>
                    <p className="text-xs text-blue-700">{proposalTemplate.documentName}</p>
                  </div>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => handleDownloadTemplate(proposalTemplate.filePath, proposalTemplate.documentName)}
                  className="text-blue-600 border-blue-300 hover:bg-blue-100"
                >
                  <Download className="h-4 w-4 mr-1" />
                  Download Template
                </Button>
              </div>
            </div>
          )}

          {/* File Upload Section */}
          <div className="border-2 border-dashed border-gray-300 rounded-lg p-4 bg-gray-50">
            <Label className="text-sm font-medium mb-2 block">
              Upload Technical Proposal Document (Optional)
            </Label>
            <p className="text-xs text-gray-500 mb-3">
              Upload a PDF, DOC, or DOCX file (max 50MB)
            </p>

            {technicalProposalDoc ? (
              <div className="flex items-center justify-between p-3 bg-green-50 border border-green-200 rounded-lg">
                <div className="flex items-center gap-2">
                  <CheckCircle className="h-5 w-5 text-green-600" />
                  <div>
                    <p className="text-sm font-medium text-green-900">{technicalProposalDoc.documentName}</p>
                    <p className="text-xs text-green-700">
                      {((technicalProposalDoc.fileSize ?? 0) / 1024 / 1024).toFixed(2)} MB
                    </p>
                  </div>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleDeleteTechnical}
                  className="text-red-600 hover:text-red-700 hover:bg-red-50"
                >
                  <X className="h-4 w-4" />
                </Button>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <Input
                  type="file"
                  id="technical-proposal-file"
                  className="hidden"
                  accept=".pdf,.doc,.docx"
                  onChange={handleTechnicalFileSelect}
                  disabled={uploadingTechnical}
                />
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!bidId || uploadingTechnical}
                  onClick={() => {
                    if (!bidId) {
                      toast.error('Please save the bid as draft first before uploading documents');
                      return;
                    }
                    document.getElementById('technical-proposal-file')?.click();
                  }}
                >
                  <Upload className="h-4 w-4 mr-1" />
                  Choose File
                </Button>
                {selectedTechnicalFile && (
                  <>
                    <span className="text-sm text-gray-600">{selectedTechnicalFile.name}</span>
                    <Button
                      size="sm"
                      onClick={handleUploadTechnical}
                      disabled={uploadingTechnical}
                    >
                      {uploadingTechnical ? (
                        <>
                          <Loader2 className="h-4 w-4 mr-1 animate-spin" />
                          Uploading...
                        </>
                      ) : (
                        <>
                          <Upload className="h-4 w-4 mr-1" />
                          Upload
                        </>
                      )}
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedTechnicalFile(null)}
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </>
                )}
              </div>
            )}
          </div>

          {/* Text Input Section */}
          <div>
            <Label htmlFor="technicalProposal">
              Or Type Technical Proposal {!technicalProposalDoc && '*'}
            </Label>
            <Textarea
              id="technicalProposal"
              value={bidData.technicalProposal || ''}
              onChange={(e) => updateBidData({ technicalProposal: e.target.value })}
              placeholder="Describe your technical approach, methodology, experience, team qualifications, quality assurance processes, etc."
              rows={8}
              className="mt-2"
              disabled={!!technicalProposalDoc}
            />
            <p className="text-sm text-gray-500 mt-1">
              {technicalProposalDoc
                ? 'Document uploaded. Text input is disabled.'
                : 'Minimum 100 characters required if no document is uploaded'}
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Commercial Proposal */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <DollarSign className="h-5 w-5 text-green-600" />
            Commercial Proposal
          </CardTitle>
          <CardDescription>
            Upload a document or type your commercial terms, payment conditions, and pricing justification
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Template Download Section */}
          {proposalTemplate && (
            <div className="bg-green-50 border border-green-200 rounded-lg p-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <DollarSign className="h-4 w-4 text-green-600" />
                  <div>
                    <p className="text-sm font-medium text-green-900">Proposal Template Available</p>
                    <p className="text-xs text-green-700">{proposalTemplate.documentName}</p>
                  </div>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => handleDownloadTemplate(proposalTemplate.filePath, proposalTemplate.documentName)}
                  className="text-green-600 border-green-300 hover:bg-green-100"
                >
                  <Download className="h-4 w-4 mr-1" />
                  Download Template
                </Button>
              </div>
            </div>
          )}

          {/* File Upload Section */}
          <div className="border-2 border-dashed border-gray-300 rounded-lg p-4 bg-gray-50">
            <Label className="text-sm font-medium mb-2 block">
              Upload Commercial Proposal Document (Optional)
            </Label>
            <p className="text-xs text-gray-500 mb-3">
              Upload a PDF, DOC, or DOCX file (max 50MB)
            </p>

            {commercialProposalDoc ? (
              <div className="flex items-center justify-between p-3 bg-green-50 border border-green-200 rounded-lg">
                <div className="flex items-center gap-2">
                  <CheckCircle className="h-5 w-5 text-green-600" />
                  <div>
                    <p className="text-sm font-medium text-green-900">{commercialProposalDoc.documentName}</p>
                    <p className="text-xs text-green-700">
                      {((commercialProposalDoc.fileSize ?? 0) / 1024 / 1024).toFixed(2)} MB
                    </p>
                  </div>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleDeleteCommercial}
                  className="text-red-600 hover:text-red-700 hover:bg-red-50"
                >
                  <X className="h-4 w-4" />
                </Button>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <Input
                  type="file"
                  id="commercial-proposal-file"
                  className="hidden"
                  accept=".pdf,.doc,.docx"
                  onChange={handleCommercialFileSelect}
                  disabled={uploadingCommercial}
                />
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!bidId || uploadingCommercial}
                  onClick={() => {
                    if (!bidId) {
                      toast.error('Please save the bid as draft first before uploading documents');
                      return;
                    }
                    document.getElementById('commercial-proposal-file')?.click();
                  }}
                >
                  <Upload className="h-4 w-4 mr-1" />
                  Choose File
                </Button>
                {selectedCommercialFile && (
                  <>
                    <span className="text-sm text-gray-600">{selectedCommercialFile.name}</span>
                    <Button
                      size="sm"
                      onClick={handleUploadCommercial}
                      disabled={uploadingCommercial}
                    >
                      {uploadingCommercial ? (
                        <>
                          <Loader2 className="h-4 w-4 mr-1 animate-spin" />
                          Uploading...
                        </>
                      ) : (
                        <>
                          <Upload className="h-4 w-4 mr-1" />
                          Upload
                        </>
                      )}
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedCommercialFile(null)}
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </>
                )}
              </div>
            )}
          </div>

          {/* Text Input Section */}
          <div>
            <Label htmlFor="commercialProposal">
              Or Type Commercial Proposal {!commercialProposalDoc && '*'}
            </Label>
            <Textarea
              id="commercialProposal"
              value={bidData.commercialProposal || ''}
              onChange={(e) => updateBidData({ commercialProposal: e.target.value })}
              placeholder="Describe your pricing structure, payment terms, discounts, value-added services, cost breakdown, etc."
              rows={8}
              className="mt-2"
              disabled={!!commercialProposalDoc}
            />
            <p className="text-sm text-gray-500 mt-1">
              {commercialProposalDoc
                ? 'Document uploaded. Text input is disabled.'
                : 'Minimum 100 characters required if no document is uploaded'}
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Terms & Conditions */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Shield className="h-5 w-5 text-purple-600" />
            Terms & Conditions
          </CardTitle>
          <CardDescription>
            Specify delivery, payment, and warranty terms
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <Label htmlFor="deliveryDays">Delivery Period (Days)</Label>
              <Input
                id="deliveryDays"
                type="number"
                min="0"
                value={bidData.deliveryDays || ''}
                onChange={(e) => updateBidData({ deliveryDays: parseInt(e.target.value) || undefined })}
                placeholder="e.g., 30"
                className="mt-2"
              />
            </div>
          </div>

          <div>
            <Label htmlFor="paymentTerms">Payment Terms</Label>
            <Textarea
              id="paymentTerms"
              value={bidData.paymentTerms || ''}
              onChange={(e) => updateBidData({ paymentTerms: e.target.value })}
              placeholder="e.g., 30% advance, 70% on delivery; Net 30 days; etc."
              rows={3}
              className="mt-2"
            />
          </div>

          <div>
            <Label htmlFor="warrantyTerms">Warranty Terms</Label>
            <Textarea
              id="warrantyTerms"
              value={bidData.warrantyTerms || ''}
              onChange={(e) => updateBidData({ warrantyTerms: e.target.value })}
              placeholder="e.g., 12 months manufacturer warranty; 24 months parts and labor; etc."
              rows={3}
              className="mt-2"
            />
          </div>
        </CardContent>
      </Card>

      {/* Guidelines */}
      <Card className="bg-blue-50 border-blue-200">
        <CardHeader>
          <CardTitle className="text-blue-900">Proposal Guidelines</CardTitle>
        </CardHeader>
        <CardContent>
          <ul className="space-y-2 text-sm text-blue-800">
            <li>• Be specific and detailed in your technical and commercial proposals</li>
            <li>• Highlight your unique value propositions and competitive advantages</li>
            <li>• Include relevant certifications, accreditations, and past performance</li>
            <li>• Clearly state all assumptions and exclusions</li>
            <li>• Ensure pricing is competitive and justified</li>
            <li>• Provide realistic delivery timelines</li>
            <li>• Specify any special terms or conditions</li>
          </ul>
        </CardContent>
      </Card>
    </div>
  );
}
