'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Upload, FileText, X, CheckCircle2 } from 'lucide-react';
import { type RegistrationFormData } from '@/services/businessPartnerRegistrationService';
import { toast } from 'sonner';

interface DocumentUploadProps {
  formData: RegistrationFormData;
  updateFormData: (data: Partial<RegistrationFormData>) => void;
  registrationId: string | null;
}

const DOCUMENT_TYPES = [
  'Company Registration Certificate',
  'Tax Clearance Certificate',
  'VAT Registration Certificate',
  'Business License',
  'Insurance Certificate',
  'Bank Statement',
  'Company Profile',
  'Other',
];

export default function DocumentUpload({ formData, updateFormData, registrationId }: DocumentUploadProps) {
  const [selectedDocumentType, setSelectedDocumentType] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);

  const documents = formData.documents || [];

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      // Validate file size (max 10MB)
      if (file.size > 10 * 1024 * 1024) {
        toast.error('File size must be less than 10MB');
        return;
      }
      setSelectedFile(file);
    }
  };

  const handleAddDocument = () => {
    if (!selectedDocumentType) {
      toast.error('Please select a document type');
      return;
    }

    if (!selectedFile) {
      toast.error('Please select a file');
      return;
    }

    // Add document to the list
    const newDocument = {
      documentType: selectedDocumentType,
      documentName: selectedFile.name,
      file: selectedFile,
    };

    updateFormData({
      documents: [...documents, newDocument],
    });

    // Reset form
    setSelectedDocumentType('');
    setSelectedFile(null);
    toast.success('Document added successfully');
  };

  const handleRemoveDocument = (index: number) => {
    const updatedDocuments = documents.filter((_, i) => i !== index);
    updateFormData({ documents: updatedDocuments });
    toast.success('Document removed');
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  };

  return (
    <div className="space-y-6">
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <h3 className="font-semibold text-blue-900 mb-2">Document Requirements</h3>
        <ul className="text-sm text-blue-800 space-y-1">
          <li>• Upload relevant business documents to support your registration</li>
          <li>• Accepted formats: PDF, JPG, PNG, DOC, DOCX</li>
          <li>• Maximum file size: 10MB per document</li>
          <li>• Recommended: Company registration, tax certificates, business licenses</li>
        </ul>
      </div>

      {/* Upload Form */}
      <Card>
        <CardContent className="pt-6">
          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="documentType">Document Type</Label>
                <Select
                  value={selectedDocumentType}
                  onValueChange={setSelectedDocumentType}
                >
                  <SelectTrigger id="documentType">
                    <SelectValue placeholder="Select document type" />
                  </SelectTrigger>
                  <SelectContent>
                    {DOCUMENT_TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {type}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="file">Select File</Label>
                <Input
                  id="file"
                  type="file"
                  onChange={handleFileSelect}
                  accept=".pdf,.jpg,.jpeg,.png,.doc,.docx"
                />
              </div>
            </div>

            {selectedFile && (
              <div className="flex items-center gap-2 p-3 bg-gray-50 rounded-lg">
                <FileText className="w-5 h-5 text-gray-600" />
                <div className="flex-1">
                  <p className="text-sm font-medium">{selectedFile.name}</p>
                  <p className="text-xs text-gray-500">{formatFileSize(selectedFile.size)}</p>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setSelectedFile(null)}
                >
                  <X className="w-4 h-4" />
                </Button>
              </div>
            )}

            <Button
              onClick={handleAddDocument}
              disabled={!selectedDocumentType || !selectedFile}
              className="w-full"
            >
              <Upload className="w-4 h-4 mr-2" />
              Add Document
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Uploaded Documents List */}
      {documents.length > 0 && (
        <div>
          <h3 className="text-lg font-semibold mb-3">Uploaded Documents ({documents.length})</h3>
          <div className="space-y-2">
            {documents.map((doc, index) => (
              <Card key={index}>
                <CardContent className="p-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
                        <CheckCircle2 className="w-5 h-5 text-green-600" />
                      </div>
                      <div>
                        <p className="font-medium">{doc.documentName}</p>
                        <div className="flex items-center gap-2 mt-1">
                          <Badge variant="outline" className="text-xs">
                            {doc.documentType}
                          </Badge>
                          {doc.file && (
                            <span className="text-xs text-gray-500">
                              {formatFileSize(doc.file.size)}
                            </span>
                          )}
                        </div>
                      </div>
                    </div>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleRemoveDocument(index)}
                    >
                      <X className="w-4 h-4" />
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      )}

      {documents.length === 0 && (
        <div className="text-center py-8 text-gray-500">
          <FileText className="w-12 h-12 mx-auto mb-3 text-gray-400" />
          <p>No documents uploaded yet</p>
          <p className="text-sm">Add documents using the form above</p>
        </div>
      )}
    </div>
  );
}

