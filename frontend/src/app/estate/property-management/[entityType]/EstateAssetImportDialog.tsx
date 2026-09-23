'use client';

import React from 'react';
import { Download, FileSpreadsheet, Loader2, Upload } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  estateLandManagementService,
  type EstateAssetImportInspection,
  type EstateAssetImportPreview,
} from '@/services/estate-land-management.service';

interface EstateAssetImportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onImported: () => void;
}

export function EstateAssetImportDialog({ open, onOpenChange, onImported }: EstateAssetImportDialogProps) {
  const [file, setFile] = React.useState<File | null>(null);
  const [inspection, setInspection] = React.useState<EstateAssetImportInspection | null>(null);
  const [mapping, setMapping] = React.useState<Record<string, string>>({});
  const [preview, setPreview] = React.useState<EstateAssetImportPreview | null>(null);
  const [busy, setBusy] = React.useState(false);

  const reset = () => {
    setFile(null);
    setInspection(null);
    setMapping({});
    setPreview(null);
  };

  const chooseFile = async (selected: File | null) => {
    reset();
    if (!selected) return;
    setBusy(true);
    try {
      const result = await estateLandManagementService.inspectAssetImport(selected);
      setFile(selected);
      setInspection(result);
      setMapping(result.suggestions);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to read the workbook.');
    } finally {
      setBusy(false);
    }
  };

  const previewImport = async () => {
    if (!file) return;
    setBusy(true);
    try {
      setPreview(await estateLandManagementService.previewAssetImport(file, mapping));
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to preview the import.');
    } finally {
      setBusy(false);
    }
  };

  const importRecords = async () => {
    if (!file || !preview?.success || preview.rowCount === 0) return;
    setBusy(true);
    try {
      const count = await estateLandManagementService.commitAssetImport(file, mapping);
      toast.success(`${count} record${count === 1 ? '' : 's'} imported.`);
      reset();
      onOpenChange(false);
      onImported();
    } catch (error) {
      setPreview(null);
      toast.error(error instanceof Error ? error.message : 'Unable to import the workbook.');
    } finally {
      setBusy(false);
    }
  };

  const downloadTemplate = async (type: 'land' | 'property') => {
    try {
      const blob = await estateLandManagementService.downloadAssetImportTemplate(type);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = type === 'land' ? 'land-bank-import-template.xlsx' : 'property-import-template.xlsx';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to download the template.');
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) reset(); onOpenChange(next); }}>
      <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2"><FileSpreadsheet className="h-5 w-5" /> Import land and properties</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => void downloadTemplate('land')}>
              <Download className="mr-2 h-4 w-4" /> Land template
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => void downloadTemplate('property')}>
              <Download className="mr-2 h-4 w-4" /> Property template
            </Button>
          </div>
          <div>
            <label htmlFor="estate-asset-import-file" className="mb-1.5 block text-sm font-medium">Excel workbook</label>
            <Input id="estate-asset-import-file" type="file" accept=".xlsx" disabled={busy}
              onChange={(event) => void chooseFile(event.target.files?.[0] ?? null)} />
            {file ? <div className="mt-1 text-xs text-muted-foreground">{file.name}</div> : null}
          </div>
          {inspection ? (
            <div className="space-y-2">
              <div className="text-sm font-medium">Column mapping</div>
              <div className="grid max-h-72 gap-x-4 gap-y-2 overflow-y-auto border-y py-3 sm:grid-cols-2">
                {inspection.fields.map((field) => (
                  <div key={field.key} className="grid grid-cols-[minmax(8rem,1fr)_minmax(9rem,1fr)] items-center gap-2 text-sm">
                    <label htmlFor={`import-${field.key}`} className="truncate" title={field.label}>{field.label}</label>
                    <Select value={mapping[field.key] || '__none'} onValueChange={(value) => {
                      setMapping((current) => ({ ...current, [field.key]: value === '__none' ? '' : value }));
                      setPreview(null);
                    }}>
                      <SelectTrigger id={`import-${field.key}`} className="h-8"><SelectValue placeholder="Not mapped" /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="__none">Not mapped</SelectItem>
                        {inspection.headers.map((header) => <SelectItem key={header} value={header}>{header}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                ))}
              </div>
            </div>
          ) : null}
          {preview ? (
            <div className={`border p-3 text-sm ${preview.success ? 'border-emerald-300 bg-emerald-50 text-emerald-950' : 'border-destructive/40 bg-destructive/5'}`}>
              <div className="font-medium">{preview.success
                ? `${preview.rowCount} records ready to import`
                : `${preview.errors.length} errors found. Correct the spreadsheet and upload it again.`}</div>
              {preview.errors.length > 0 ? (
                <ul className="mt-2 max-h-40 list-disc space-y-1 overflow-y-auto pl-5">
                  {preview.errors.map((error, index) => <li key={`${index}-${error}`}>{error}</li>)}
                </ul>
              ) : null}
            </div>
          ) : null}
        </div>
        <DialogFooter className="gap-2 sm:gap-0">
          <Button type="button" variant="outline" onClick={() => { reset(); onOpenChange(false); }}>Cancel</Button>
          <Button type="button" variant="outline" disabled={!file || busy} onClick={() => void previewImport()}>
            {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null} Preview
          </Button>
          <Button type="button" disabled={!preview?.success || !preview.rowCount || busy} onClick={() => void importRecords()}>
            <Upload className="mr-2 h-4 w-4" /> Import
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
