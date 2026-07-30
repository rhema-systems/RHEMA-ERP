'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  AlertCircle,
  CheckCircle2,
  FileText,
  Loader2,
  RefreshCw,
  Upload,
  X,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  businessPartnerRegistrationService,
  type RegistrationFormData,
} from '@/services/businessPartnerRegistrationService';
import { fileUploadService } from '@/services/file-upload.service';
import { procurementSupplierEvidencePackService } from '@/services/procurement-supplier-evidence-pack.service';

interface DocumentUploadProps {
  formData: RegistrationFormData;
  updateFormData: (data: Partial<RegistrationFormData>) => void;
  registrationId: string | null;
}

export default function DocumentUpload({
  formData,
  updateFormData,
  registrationId,
}: DocumentUploadProps) {
  const [selectedRequirementCode, setSelectedRequirementCode] = useState('');
  const [classificationCode, setClassificationCode] = useState('');
  const [issueDate, setIssueDate] = useState('');
  const [expiryDate, setExpiryDate] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const documents = formData.documents || [];

  const readiness = useQuery({
    queryKey: ['supplier-registration-evidence-readiness', registrationId],
    queryFn: () => {
      if (!registrationId) throw new Error('Registration id is required.');
      return procurementSupplierEvidencePackService.registrationReadiness(
        registrationId
      );
    },
    enabled: Boolean(registrationId),
  });
  const selectedRequirement = useMemo(
    () =>
      readiness.data?.requirements.find(
        (item) => item.requirementCode === selectedRequirementCode
      ),
    [readiness.data?.requirements, selectedRequirementCode]
  );

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    const maxMegabytes = selectedRequirement
      ? selectedRequirement.maxFileSizeBytes / 1024 / 1024
      : 10;
    const validation = fileUploadService.validateFile(file, maxMegabytes);
    if (!validation.valid) {
      toast.error(validation.error || 'Invalid file');
      event.target.value = '';
      return;
    }
    if (
      selectedRequirement?.allowedMimeTypes.length &&
      !selectedRequirement.allowedMimeTypes.some(
        (value) => value.toLowerCase() === file.type.toLowerCase()
      )
    ) {
      toast.error(
        `Allowed types: ${selectedRequirement.allowedMimeTypes.join(', ')}`
      );
      event.target.value = '';
      return;
    }
    setSelectedFile(file);
  };

  const upload = async () => {
    if (!registrationId || !selectedRequirement || !selectedFile) {
      toast.error('Select a pack requirement and file after saving the Draft.');
      return;
    }
    if (selectedRequirement.kind !== 'Document' && !classificationCode.trim()) {
      toast.error('Select the required classification.');
      return;
    }
    if (selectedRequirement.validityMode !== 'NotApplicable' && !expiryDate) {
      toast.error('Expiry date is required by this evidence pack.');
      return;
    }
    try {
      setUploading(true);
      await businessPartnerRegistrationService.uploadDocument(
        registrationId,
        selectedFile,
        selectedRequirement.documentType ?? selectedRequirement.name,
        {
          requirementCode: selectedRequirement.requirementCode,
          classificationCode: classificationCode.trim() || undefined,
          issueDate: issueDate || undefined,
          expiryDate: expiryDate || undefined,
        }
      );
      updateFormData({
        documents: [
          ...documents,
          {
            documentType:
              selectedRequirement.documentType ?? selectedRequirement.name,
            documentName: selectedFile.name,
            file: selectedFile,
            fileSize: selectedFile.size,
          },
        ],
      });
      setSelectedFile(null);
      setSelectedRequirementCode('');
      setClassificationCode('');
      setIssueDate('');
      setExpiryDate('');
      const input = document.getElementById(
        'supplier-evidence-file'
      ) as HTMLInputElement | null;
      if (input) input.value = '';
      await readiness.refetch();
      toast.success('Controlled registration evidence uploaded');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Upload failed');
    } finally {
      setUploading(false);
    }
  };

  const removeLocal = (index: number) => {
    updateFormData({
      documents: documents.filter((_, itemIndex) => itemIndex !== index),
    });
  };

  return (
    <div className="space-y-6" data-testid="supplier-evidence-upload">
      {!registrationId && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Save the registration Draft first</AlertTitle>
          <AlertDescription>
            The server resolves the exact Published{' '}
            {formData.registrationCategory} pack after the Draft has an
            application number.
          </AlertDescription>
        </Alert>
      )}

      {registrationId && readiness.isError && (
        <Alert variant="destructive">
          <AlertTitle>Evidence requirements unavailable</AlertTitle>
          <AlertDescription>
            No unambiguous Published and effective{' '}
            {formData.registrationCategory} pack could be resolved. Submission
            will remain blocked until Procurement publishes one.
          </AlertDescription>
        </Alert>
      )}

      {readiness.data && (
        <Card>
          <CardContent className="space-y-4 pt-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h3 className="font-semibold">
                  {readiness.data.packCode}/v{readiness.data.packVersion}
                </h3>
                <p className="text-sm text-muted-foreground">
                  {readiness.data.category} ·{' '}
                  {readiness.data.isBound
                    ? 'immutably bound to this application'
                    : 'current effective pack; bound on submission'}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant={readiness.data.isReady ? 'default' : 'outline'}>
                  {readiness.data.isReady
                    ? 'Ready to submit'
                    : 'Evidence incomplete'}
                </Badge>
                <Button
                  size="icon"
                  variant="ghost"
                  aria-label="Refresh evidence readiness"
                  onClick={() => void readiness.refetch()}
                >
                  <RefreshCw className="h-4 w-4" />
                </Button>
              </div>
            </div>
            <div className="grid gap-2 md:grid-cols-2">
              {readiness.data.requirements.map((requirement) => (
                <div
                  key={requirement.requirementCode}
                  className="rounded-md border p-3"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="text-sm font-medium">
                        {requirement.requirementCode} · {requirement.name}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        Step {requirement.approvalStepOrder}:{' '}
                        {requirement.approvalStepName}
                      </p>
                    </div>
                    {requirement.isSatisfied ? (
                      <CheckCircle2 className="h-5 w-5 text-green-600" />
                    ) : (
                      <AlertCircle className="h-5 w-5 text-amber-600" />
                    )}
                  </div>
                  {requirement.issues.length > 0 && (
                    <p className="mt-2 text-xs text-amber-700">
                      {requirement.issues.join('; ')}
                    </p>
                  )}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="space-y-4 pt-6">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Evidence requirement</Label>
              <Select
                value={selectedRequirementCode}
                onValueChange={(value) => {
                  setSelectedRequirementCode(value);
                  setClassificationCode('');
                  setIssueDate('');
                  setExpiryDate('');
                }}
                disabled={!readiness.data}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select a server-derived requirement" />
                </SelectTrigger>
                <SelectContent>
                  {(readiness.data?.requirements ?? []).map((requirement) => (
                    <SelectItem
                      key={requirement.requirementCode}
                      value={requirement.requirementCode}
                    >
                      {requirement.requirementCode} · {requirement.name}
                      {requirement.isMandatory ? ' *' : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="supplier-evidence-file">Evidence file</Label>
              <Input
                id="supplier-evidence-file"
                type="file"
                onChange={handleFileSelect}
                disabled={!selectedRequirement}
                accept={selectedRequirement?.allowedMimeTypes.join(',')}
              />
              {selectedRequirement && (
                <p className="text-xs text-muted-foreground">
                  Maximum{' '}
                  {fileUploadService.formatFileSize(
                    selectedRequirement.maxFileSizeBytes
                  )}
                  ; {selectedRequirement.allowedMimeTypes.join(', ')}
                </p>
              )}
            </div>
            {selectedRequirement?.kind !== 'Document' && (
              <div className="space-y-2">
                <Label>{selectedRequirement?.classificationScheme}</Label>
                <Select
                  value={classificationCode}
                  onValueChange={setClassificationCode}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select classification" />
                  </SelectTrigger>
                  <SelectContent>
                    {(selectedRequirement?.allowedClassifications ?? []).map(
                      (value) => (
                        <SelectItem key={value} value={value}>
                          {value}
                        </SelectItem>
                      )
                    )}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="space-y-2">
              <Label>Issue date</Label>
              <Input
                type="date"
                value={issueDate}
                onChange={(event) => setIssueDate(event.target.value)}
              />
            </div>
            {selectedRequirement?.validityMode !== 'NotApplicable' && (
              <div className="space-y-2">
                <Label>Expiry date</Label>
                <Input
                  type="date"
                  value={expiryDate}
                  onChange={(event) => setExpiryDate(event.target.value)}
                />
                {selectedRequirement?.minimumRemainingDays && (
                  <p className="text-xs text-muted-foreground">
                    Must remain valid for at least{' '}
                    {selectedRequirement.minimumRemainingDays} days.
                  </p>
                )}
              </div>
            )}
          </div>

          {selectedFile && (
            <div className="flex items-center gap-3 rounded-md bg-muted p-3">
              <FileText className="h-5 w-5" />
              <div className="flex-1">
                <p className="text-sm font-medium">{selectedFile.name}</p>
                <p className="text-xs text-muted-foreground">
                  {fileUploadService.formatFileSize(selectedFile.size)}
                </p>
              </div>
              <Button
                size="icon"
                variant="ghost"
                onClick={() => setSelectedFile(null)}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          )}

          <Button
            className="w-full"
            disabled={
              !registrationId ||
              !selectedRequirement ||
              !selectedFile ||
              uploading
            }
            onClick={() => void upload()}
          >
            {uploading ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Upload controlled evidence
          </Button>
        </CardContent>
      </Card>

      {documents.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-lg font-semibold">
            Uploaded in this session ({documents.length})
          </h3>
          {documents.map((document, index) => (
            <Card key={`${document.documentName}-${index}`}>
              <CardContent className="flex items-center gap-3 p-4">
                <CheckCircle2 className="h-5 w-5 text-green-600" />
                <div className="flex-1">
                  <p className="font-medium">{document.documentName}</p>
                  <p className="text-xs text-muted-foreground">
                    {document.documentType} ·{' '}
                    {fileUploadService.formatFileSize(
                      document.file?.size || document.fileSize || 0
                    )}
                  </p>
                </div>
                <Button
                  size="icon"
                  variant="ghost"
                  onClick={() => removeLocal(index)}
                >
                  <X className="h-4 w-4" />
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
