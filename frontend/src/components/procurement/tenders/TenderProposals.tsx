'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { FileText, Upload, X, CheckCircle, Loader2, Download, Trash2 } from 'lucide-react';
import { type TenderFormData } from '@/app/procurement/tenders/new/page';
import { tenderService, TenderDocumentDto } from '@/services/tenderService';
import { toast } from 'sonner';
import { format } from 'date-fns';

interface TenderProposalsProps {
  formData: TenderFormData;
  updateFormData: (data: Partial<TenderFormData>) => void;
  tenderId: string | null;
  isEditMode?: boolean;
}

export default function TenderProposals({
  formData,
  updateFormData,
  tenderId,
  isEditMode = false,
}: TenderProposalsProps) {
  const [uploading, setUploading] = useState(false);
  const [uploadedProposalTemplate, setUploadedProposalTemplate] = useState<TenderDocumentDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  // Load existing uploaded template when tenderId is available
  useEffect(() => {
    if (tenderId) {
      loadUploadedTemplate();
    }
  }, [tenderId]);

  const loadUploadedTemplate = async () => {
    if (!tenderId) return;

    try {
      const tender = await tenderService.getTenderById(tenderId);
      if (tender.documents) {
        // Look for the new ProposalTemplate type, fall back to old TechnicalProposalTemplate for backward compatibility
        const proposalTemplate = tender.documents.find(doc => doc.documentType === 'ProposalTemplate')
          || tender.documents.find(doc => doc.documentType === 'TechnicalProposalTemplate');
        setUploadedProposalTemplate(proposalTemplate || null);
      }
    } catch (error) {
      console.error('Error loading uploaded template:', error);
    }
  };

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
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
    const allowedExtensions = ['pdf', 'doc', 'docx', 'xls', 'xlsx'];
    const fileExtension = file.name.split('.').pop()?.toLowerCase() || '';
    if (!allowedExtensions.includes(fileExtension)) {
      toast.error('Only PDF, DOC, DOCX, XLS, and XLSX files are allowed');
      return;
    }

    updateFormData({
      proposalTemplateFile: file,
      proposalTemplateName: file.name
    });
  };

  const handleUpload = async () => {
    if (!formData.proposalTemplateFile || !tenderId) {
      toast.error('Please select a file and ensure tender is saved');
      return;
    }

    try {
      setUploading(true);
      const uploadedDoc = await tenderService.uploadTenderDocument(
        tenderId,
        formData.proposalTemplateFile,
        'ProposalTemplate',
        formData.proposalTemplateFile.name,
        true // isPublic - so bidders can download
      );
      toast.success('Proposal template uploaded successfully');
      // Clear the file from form data and set the uploaded document
      updateFormData({ proposalTemplateFile: null, proposalTemplateName: '' });
      setUploadedProposalTemplate(uploadedDoc);
    } catch (error) {
      console.error('Error uploading proposal template:', error);
      toast.error('Failed to upload proposal template');
    } finally {
      setUploading(false);
    }
  };

  const handleDeleteTemplate = async () => {
    if (!uploadedProposalTemplate || !tenderId) return;

    try {
      setDeleting(true);
      await tenderService.deleteTenderDocument(tenderId, uploadedProposalTemplate.id);
      toast.success('Proposal template deleted');
      setUploadedProposalTemplate(null);
    } catch (error) {
      console.error('Error deleting proposal template:', error);
      toast.error('Failed to delete proposal template');
    } finally {
      setDeleting(false);
    }
  };

  const handleRemoveFile = () => {
    updateFormData({
      proposalTemplateFile: null,
      proposalTemplateName: ''
    });
  };

  return (
    <div className="space-y-6">
      {/* Info Banner */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <p className="text-sm text-blue-800">
          <strong>Proposal Template:</strong> Upload a template document that bidders can download and use as a guide when preparing their technical and commercial proposals. Bidders will be required to upload separate technical and commercial proposal documents based on this template.
        </p>
      </div>

      {/* Proposal Template */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5 text-blue-600" />
            Proposal Template
          </CardTitle>
          <CardDescription>
            Upload a template document for bidders to use when preparing their proposals. Bidders will submit separate technical and commercial proposals based on this template.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Show uploaded template from server */}
          {uploadedProposalTemplate && (
            <div className="flex items-center justify-between p-3 bg-blue-50 border border-blue-200 rounded-lg">
              <div className="flex items-center gap-2">
                <CheckCircle className="h-5 w-5 text-blue-600" />
                <div>
                  <p className="text-sm font-medium text-blue-900">
                    {uploadedProposalTemplate.documentName}
                  </p>
                  <p className="text-xs text-blue-700">
                    Uploaded {uploadedProposalTemplate.uploadedDate ? format(new Date(uploadedProposalTemplate.uploadedDate), 'PPp') : ''}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => window.open(`${process.env.NEXT_PUBLIC_API_URL?.replace('/api', '')}${uploadedProposalTemplate.filePath}`, '_blank')}
                >
                  <Download className="h-4 w-4 mr-1" />
                  Download
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleDeleteTemplate}
                  disabled={deleting}
                  className="text-red-600 hover:text-red-700 hover:bg-red-50"
                >
                  {deleting ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Trash2 className="h-4 w-4" />
                  )}
                </Button>
              </div>
            </div>
          )}

          {/* Show file selection / pending upload area */}
          {!uploadedProposalTemplate && (
            <div className="border-2 border-dashed border-gray-300 rounded-lg p-4 bg-gray-50">
              {formData.proposalTemplateFile ? (
                <div className="flex items-center justify-between p-3 bg-green-50 border border-green-200 rounded-lg">
                  <div className="flex items-center gap-2">
                    <CheckCircle className="h-5 w-5 text-green-600" />
                    <div>
                      <p className="text-sm font-medium text-green-900">
                        {formData.proposalTemplateFile.name}
                      </p>
                      <p className="text-xs text-green-700">
                        {(formData.proposalTemplateFile.size / 1024 / 1024).toFixed(2)} MB - Ready to upload
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleUpload}
                      disabled={uploading || !tenderId}
                    >
                      {uploading ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
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
                      onClick={handleRemoveFile}
                      className="text-red-600 hover:text-red-700 hover:bg-red-50"
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="text-center">
                  <Upload className="h-8 w-8 mx-auto text-gray-400 mb-2" />
                  <Label htmlFor="proposal-template-file" className="cursor-pointer">
                    <span className="text-sm text-blue-600 hover:text-blue-700 font-medium">
                      Click to select proposal template
                    </span>
                  </Label>
                  <p className="text-xs text-gray-500 mt-1">
                    PDF, DOC, DOCX, XLS, XLSX (Max 50MB)
                  </p>
                  <Input
                    id="proposal-template-file"
                    type="file"
                    accept=".pdf,.doc,.docx,.xls,.xlsx"
                    onChange={handleFileSelect}
                    className="hidden"
                  />
                </div>
              )}
              {!tenderId && formData.proposalTemplateFile && (
                <p className="text-xs text-amber-600 mt-2">
                  Save the tender as draft first to upload the template
                </p>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
