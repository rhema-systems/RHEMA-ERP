'use client';

import { useState, useMemo } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { FileText, Upload, X, CheckCircle, Download, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { type CreateTenderBidDto, type TenderBidDocumentDto } from '@/services/tenderBidService';
import { type TenderDetailDto, type TenderDocumentRequirement } from '@/services/tenderService';

interface BidDocumentsStepProps {
  bidData: CreateTenderBidDto;
  updateBidData: (updates: Partial<CreateTenderBidDto>) => void;
  bidId?: string; // Bid ID if already created (for draft)
  uploadedDocuments?: TenderBidDocumentDto[]; // Already uploaded documents
  onDocumentUpload?: (file: File, documentType: string) => Promise<void>;
  onDocumentDelete?: (documentId: string) => Promise<void>;
  tender?: TenderDetailDto | null; // Tender details with requirements
}

export default function BidDocumentsStep({
  bidData,
  updateBidData,
  bidId,
  uploadedDocuments = [],
  onDocumentUpload,
  onDocumentDelete,
  tender
}: BidDocumentsStepProps) {
  const [uploading, setUploading] = useState<string | null>(null);
  const [selectedFiles, setSelectedFiles] = useState<Record<string, File>>({});

  // Parse tender requirements or use default
  const documentRequirements = useMemo<TenderDocumentRequirement[]>(() => {
    if (tender?.requiredDocuments) {
      try {
        return JSON.parse(tender.requiredDocuments) as TenderDocumentRequirement[];
      } catch (error) {
        console.error('Error parsing tender requirements:', error);
      }
    }

    // Default requirements if tender doesn't specify
    return [
      {
        documentType: 'CompanyRegistration',
        documentName: 'Company Registration Certificate',
        isRequired: true,
        description: 'Valid company registration certificate',
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF,JPG,PNG'
      },
      {
        documentType: 'TaxClearance',
        documentName: 'Tax Clearance Certificate',
        isRequired: true,
        description: 'Current tax clearance certificate',
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF,JPG,PNG'
      },
      {
        documentType: 'FinancialStatements',
        documentName: 'Financial Statements (Last 2 Years)',
        isRequired: true,
        description: 'Audited financial statements for the last 2 years',
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF'
      },
      {
        documentType: 'CompanyProfile',
        documentName: 'Company Profile',
        isRequired: false,
        description: 'Company profile and capabilities',
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF,DOC,DOCX'
      },
      {
        documentType: 'ProductBrochures',
        documentName: 'Product Brochures/Catalogs',
        isRequired: false,
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF'
      },
      {
        documentType: 'QualityCertifications',
        documentName: 'Quality Certifications (ISO, etc.)',
        isRequired: false,
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF,JPG,PNG'
      },
      {
        documentType: 'References',
        documentName: 'References/Past Performance',
        isRequired: false,
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF,DOC,DOCX'
      },
      {
        documentType: 'Insurance',
        documentName: 'Insurance Certificates',
        isRequired: false,
        maxFileSizeMB: 20,
        allowedFileTypes: 'PDF,JPG,PNG'
      },
    ];
  }, [tender]);

  const handleFileSelect = (documentType: string, event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Find the requirement for this document type
    const requirement = documentRequirements.find(req => req.documentType === documentType);

    // Validate file size
    const maxSizeMB = requirement?.maxFileSizeMB || 20;
    const maxSizeBytes = maxSizeMB * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      toast.error(`File size must be less than ${maxSizeMB}MB`);
      return;
    }

    // Validate file type
    if (requirement?.allowedFileTypes) {
      const allowedExtensions = requirement.allowedFileTypes.split(',').map(ext => ext.trim().toLowerCase());
      const fileExtension = file.name.split('.').pop()?.toLowerCase() || '';

      if (!allowedExtensions.includes(fileExtension)) {
        toast.error(`Only ${requirement.allowedFileTypes} files are allowed`);
        return;
      }
    } else {
      // Default validation
      const allowedTypes = [
        'application/pdf',
        'application/msword',
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        'image/jpeg',
        'image/png'
      ];
      if (!allowedTypes.includes(file.type)) {
        toast.error('Only PDF, DOC, DOCX, JPG, and PNG files are allowed');
        return;
      }
    }

    setSelectedFiles(prev => ({ ...prev, [documentType]: file }));
  };

  const handleUpload = async (documentType: string) => {
    const file = selectedFiles[documentType];
    if (!file || !onDocumentUpload) {
      toast.error('Please select a file first');
      return;
    }

    try {
      setUploading(documentType);
      await onDocumentUpload(file, documentType);
      setSelectedFiles(prev => {
        const newFiles = { ...prev };
        delete newFiles[documentType];
        return newFiles;
      });
      toast.success('Document uploaded successfully');
    } catch (error) {
      console.error('Error uploading document:', error);
      toast.error('Failed to upload document');
    } finally {
      setUploading(null);
    }
  };

  const handleDelete = async (documentId: string) => {
    if (!onDocumentDelete) return;

    if (!confirm('Are you sure you want to delete this document?')) {
      return;
    }

    try {
      await onDocumentDelete(documentId);
      toast.success('Document deleted successfully');
    } catch (error) {
      console.error('Error deleting document:', error);
      toast.error('Failed to delete document');
    }
  };

  const getUploadedDocument = (documentType: string) => {
    return uploadedDocuments.find(doc => doc.documentType === documentType);
  };

  const isDocumentUploaded = (documentType: string) => {
    return uploadedDocuments.some(doc => doc.documentType === documentType);
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Supporting Documents
          </CardTitle>
          <CardDescription>
            Upload required and supporting documents for your bid
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {documentRequirements.map((doc) => {
              const uploadedDoc = getUploadedDocument(doc.documentType);
              const selectedFile = selectedFiles[doc.documentType];
              const isUploading = uploading === doc.documentType;

              return (
                <div
                  key={doc.documentType}
                  className={`p-4 border rounded-lg ${
                    uploadedDoc ? 'bg-green-50 border-green-200' : 'hover:bg-gray-50'
                  }`}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex items-start gap-3 flex-1">
                      {uploadedDoc ? (
                        <CheckCircle className="h-5 w-5 text-green-600 mt-0.5" />
                      ) : (
                        <FileText className="h-5 w-5 text-gray-400 mt-0.5" />
                      )}
                      <div className="flex-1">
                        <p className="font-medium">
                          {doc.documentName}
                          {doc.isRequired && <span className="text-red-500 ml-1">*</span>}
                        </p>
                        <p className="text-sm text-gray-500">
                          {doc.isRequired ? 'Required' : 'Optional'}
                          {doc.allowedFileTypes && ` • Allowed: ${doc.allowedFileTypes}`}
                          {doc.maxFileSizeMB && ` • Max: ${doc.maxFileSizeMB}MB`}
                        </p>
                        {doc.description && (
                          <p className="text-xs text-gray-500 mt-1">{doc.description}</p>
                        )}

                        {uploadedDoc && (
                          <div className="mt-2 flex items-center gap-2">
                            <p className="text-sm text-green-700">
                              ✓ {uploadedDoc.documentName}
                            </p>
                            <p className="text-xs text-gray-500">
                              ({((uploadedDoc.fileSize ?? 0) / 1024).toFixed(1)} KB)
                            </p>
                          </div>
                        )}

                        {selectedFile && !uploadedDoc && (
                          <p className="text-sm text-blue-600 mt-2">
                            Selected: {selectedFile.name} ({(selectedFile.size / 1024).toFixed(1)} KB)
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      {uploadedDoc ? (
                        <>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => handleDelete(uploadedDoc.id)}
                          >
                            <X className="h-4 w-4 mr-1" />
                            Remove
                          </Button>
                        </>
                      ) : (
                        <>
                          <Input
                            type="file"
                            id={`file-${doc.documentType}`}
                            className="hidden"
                            accept={doc.allowedFileTypes ?
                              doc.allowedFileTypes.split(',').map(ext => `.${ext.trim().toLowerCase()}`).join(',') :
                              '.pdf,.doc,.docx,.jpg,.jpeg,.png'
                            }
                            onChange={(e) => handleFileSelect(doc.documentType, e)}
                            disabled={isUploading}
                          />
                          <Button
                            variant="outline"
                            size="sm"
                            disabled={!bidId || isUploading}
                            onClick={() => {
                              if (!bidId) {
                                toast.error('Please save the bid as draft first before uploading documents');
                                return;
                              }
                              document.getElementById(`file-${doc.documentType}`)?.click();
                            }}
                          >
                            <Upload className="h-4 w-4 mr-1" />
                            Choose File
                          </Button>
                          {selectedFile && (
                            <Button
                              size="sm"
                              onClick={() => handleUpload(doc.documentType)}
                              disabled={isUploading}
                            >
                              {isUploading ? (
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
                          )}
                        </>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Upload Guidelines */}
      <Card className="bg-yellow-50 border-yellow-200">
        <CardHeader>
          <CardTitle className="text-yellow-900">Document Upload Guidelines</CardTitle>
        </CardHeader>
        <CardContent>
          <ul className="space-y-2 text-sm text-yellow-800">
            <li>• All documents marked with * are required</li>
            <li>• Follow the file type and size restrictions for each document</li>
            <li>• Ensure all documents are clear and legible</li>
            <li>• Documents must be current and valid</li>
            <li>• Scanned copies must be in color</li>
            <li>• All documents should be properly labeled</li>
            <li>• Required documents: {documentRequirements.filter(d => d.isRequired).length} of {documentRequirements.length}</li>
          </ul>
        </CardContent>
      </Card>

      {/* Note about saving draft first */}
      {!bidId && (
        <Card className="bg-blue-50 border-blue-200">
          <CardContent className="pt-6">
            <div className="flex items-start gap-3">
              <CheckCircle className="h-5 w-5 text-blue-600 mt-0.5" />
              <div>
                <p className="font-medium text-blue-900">Save Draft First</p>
                <p className="text-sm text-blue-700 mt-1">
                  Please save your bid as a draft first before uploading documents. You can upload documents
                  after the bid is created.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
