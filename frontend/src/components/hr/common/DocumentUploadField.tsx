'use client';

import { useRef, useState } from 'react';
import { Loader2, Paperclip, Upload, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import { describeHrUploadError, type HrDocumentUploadError } from '@/types/hr/document';

interface DocumentUploadFieldProps {
  label?: string;
  /** The area's gated upload route, e.g. `/Leaves/{id}/attachments`. */
  endpoint: string;
  /** Extra form fields the endpoint expects alongside the file. */
  fields?: Record<string, string | number | boolean | undefined | null>;
  /** A few endpoints name the file field something other than `file`. */
  fileField?: string;
  accept?: string;
  /** Client-side guard; the gate enforces its own limit server-side regardless. */
  maxSizeMb?: number;
  disabled?: boolean;
  onUploaded?: (result: unknown) => void;
  helpText?: string;
}

/**
 * File input for HR documents. Uploads through the controlled gate (scanning + central
 * DMS registration) and renders the gate's rejection reason inline, so an unsupported
 * file type or a failed virus scan reads as a clear message rather than a generic error.
 */
export function DocumentUploadField({
  label = 'Document',
  endpoint,
  fields,
  fileField,
  accept,
  maxSizeMb = 10,
  disabled,
  onUploaded,
  helpText,
}: DocumentUploadFieldProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const pick = (selected: File | null) => {
    setError(null);
    if (selected && selected.size > maxSizeMb * 1024 * 1024) {
      setError(`That file is larger than ${maxSizeMb} MB.`);
      setFile(null);
      return;
    }
    setFile(selected);
  };

  const upload = async () => {
    if (!file) return;
    setUploading(true);
    setError(null);
    try {
      const result = await hrDocumentService.upload(endpoint, file, fields, fileField);
      onUploaded?.(result);
      setFile(null);
      if (inputRef.current) inputRef.current.value = '';
    } catch (e) {
      setError(describeHrUploadError(e as HrDocumentUploadError));
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="space-y-2">
      <Label htmlFor="hr-document-upload">{label}</Label>

      <div className="flex items-center gap-2">
        <input
          ref={inputRef}
          id="hr-document-upload"
          type="file"
          accept={accept}
          disabled={disabled || uploading}
          className="hidden"
          onChange={(e) => pick(e.target.files?.[0] ?? null)}
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled || uploading}
          onClick={() => inputRef.current?.click()}
        >
          <Paperclip className="mr-2 h-4 w-4" />
          Choose file
        </Button>

        {file && (
          <>
            <span className="max-w-[220px] truncate text-sm text-muted-foreground">
              {file.name}
            </span>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-7 w-7"
              disabled={uploading}
              onClick={() => pick(null)}
            >
              <X className="h-4 w-4" />
              <span className="sr-only">Clear</span>
            </Button>
            <Button type="button" size="sm" disabled={uploading} onClick={upload}>
              {uploading ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Upload className="mr-2 h-4 w-4" />
              )}
              Upload
            </Button>
          </>
        )}
      </div>

      {error && <p className="text-sm text-red-500">{error}</p>}
      {!error && helpText && <p className="text-xs text-muted-foreground">{helpText}</p>}
    </div>
  );
}
