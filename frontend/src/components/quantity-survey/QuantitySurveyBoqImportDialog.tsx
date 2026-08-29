import { useRef, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Download,
  FileSpreadsheet,
  Loader2,
  ShieldCheck,
  Upload,
} from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  projectService,
  type QuantitySurveyBoqImportPreviewDto,
} from '@/services/projectService';

type QuantitySurveyBoqImportActionsProps = {
  projectId: string;
  projectCode: string;
  onImportCompleted: () => Promise<void> | void;
  canImport: boolean;
};

const saveBlob = (blob: Blob, fileName: string) => {
  const url = window.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(url);
};

const safeFilePart = (value: string) =>
  value.replace(/[^a-z0-9_-]+/gi, '-').replace(/^-|-$/g, '') || 'project';

export function QuantitySurveyBoqImportActions({
  projectId,
  projectCode,
  onImportCompleted,
  canImport,
}: QuantitySurveyBoqImportActionsProps) {
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] =
    useState<QuantitySurveyBoqImportPreviewDto | null>(null);
  const [reconciliationConfirmed, setReconciliationConfirmed] = useState(false);
  const [downloading, setDownloading] = useState<
    'template' | 'export' | 'errors' | null
  >(null);
  const [validating, setValidating] = useState(false);
  const [posting, setPosting] = useState(false);
  const idempotencyKey = useRef<string | null>(null);

  const resetStaging = () => {
    setFile(null);
    setPreview(null);
    setReconciliationConfirmed(false);
    idempotencyKey.current = null;
  };

  const handleOpenChange = (value: boolean) => {
    if (!value && !posting) resetStaging();
    setOpen(value);
  };

  const downloadTemplate = async () => {
    setDownloading('template');
    try {
      const blob =
        await projectService.downloadProjectBoqImportTemplate(projectId);
      saveBlob(blob, `${safeFilePart(projectCode)}-boq-import-template.xlsx`);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to download the BoQ import template.'
      );
    } finally {
      setDownloading(null);
    }
  };

  const exportBoq = async () => {
    setDownloading('export');
    try {
      const blob = await projectService.exportProjectBoqWorkbook(projectId);
      saveBlob(blob, `${safeFilePart(projectCode)}-boq.xlsx`);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to export the project BoQ.'
      );
    } finally {
      setDownloading(null);
    }
  };

  const validateWorkbook = async () => {
    if (!file) {
      toast.error('Select the completed .xlsx workbook first.');
      return;
    }

    setValidating(true);
    setPreview(null);
    setReconciliationConfirmed(false);
    idempotencyKey.current = null;
    try {
      const result = await projectService.previewProjectBoqImport(
        projectId,
        file
      );
      setPreview(result);
      idempotencyKey.current = crypto.randomUUID();
      if (result.errorCount > 0) {
        toast.error(
          `Validation found ${result.errorCount} error(s). Nothing has been posted.`
        );
      } else {
        toast.success(
          `${result.lineCount} BoQ line(s) passed server validation.`
        );
      }
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to validate the BoQ workbook.'
      );
    } finally {
      setValidating(false);
    }
  };

  const downloadErrors = async () => {
    if (!preview) return;
    setDownloading('errors');
    try {
      const blob = await projectService.downloadProjectBoqValidationReport(
        projectId,
        preview.sessionId
      );
      saveBlob(blob, `${safeFilePart(projectCode)}-boq-validation-report.xlsx`);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to download the validation report.'
      );
    } finally {
      setDownloading(null);
    }
  };

  const postStagedBoq = async () => {
    if (
      !preview ||
      preview.errorCount > 0 ||
      !reconciliationConfirmed ||
      !idempotencyKey.current
    )
      return;
    setPosting(true);
    try {
      const result = await projectService.commitProjectBoqImport(
        projectId,
        preview.sessionId,
        preview.previewToken,
        idempotencyKey.current
      );
      await onImportCompleted();
      toast.success(
        `${result.committedLineCount} BoQ line(s) posted successfully.`
      );
      setOpen(false);
      resetStaging();
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to post the staged BoQ.'
      );
    } finally {
      setPosting(false);
    }
  };

  return (
    <>
      <Button
        variant="outline"
        className="gap-2"
        onClick={() => void downloadTemplate()}
        disabled={downloading !== null}
      >
        {downloading === 'template' ? (
          <Loader2 className="h-4 w-4 animate-spin" />
        ) : (
          <Download className="h-4 w-4" />
        )}
        Template
      </Button>
      <Button
        variant="outline"
        className="gap-2"
        onClick={() => void exportBoq()}
        disabled={downloading !== null}
      >
        {downloading === 'export' ? (
          <Loader2 className="h-4 w-4 animate-spin" />
        ) : (
          <FileSpreadsheet className="h-4 w-4" />
        )}
        Export BoQ
      </Button>
      {canImport ? (
        <Button
          variant="outline"
          className="gap-2"
          onClick={() => setOpen(true)}
        >
          <Upload className="h-4 w-4" />
          Import BoQ
        </Button>
      ) : null}

      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent className="max-h-[92vh] max-w-5xl overflow-hidden p-0">
          <DialogHeader className="border-b px-6 py-5 pr-12">
            <DialogTitle>Stage BoQ spreadsheet import</DialogTitle>
            <DialogDescription>
              Validate the protected project template before any BoQ lines are
              posted. The original workbook is retained in the central document
              repository.
            </DialogDescription>
          </DialogHeader>

          <ScrollArea className="max-h-[calc(92vh-170px)] px-6 py-5">
            <div className="space-y-5 pr-3">
              <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
                <div className="space-y-2">
                  <Label htmlFor="quantity-survey-boq-workbook">
                    Completed BoQ workbook
                  </Label>
                  <Input
                    id="quantity-survey-boq-workbook"
                    type="file"
                    accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    disabled={validating || posting}
                    onChange={(event) => {
                      setFile(event.target.files?.[0] ?? null);
                      setPreview(null);
                      setReconciliationConfirmed(false);
                      idempotencyKey.current = null;
                    }}
                  />
                </div>
                <Button
                  onClick={() => void validateWorkbook()}
                  disabled={!file || validating || posting}
                  className="gap-2"
                >
                  {validating ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <ShieldCheck className="h-4 w-4" />
                  )}
                  Validate workbook
                </Button>
              </div>

              <Alert>
                <FileSpreadsheet className="h-4 w-4" />
                <AlertTitle>Protected template controls</AlertTitle>
                <AlertDescription>
                  Codes, units, quantities, duplicates, formulas, locked cells,
                  tenant/project identity, file security, and current catalogue
                  references are checked on the server.
                </AlertDescription>
              </Alert>

              {preview ? (
                <>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{preview.lineCount} line(s)</Badge>
                    <Badge
                      variant={
                        preview.errorCount > 0 ? 'destructive' : 'secondary'
                      }
                    >
                      {preview.errorCount} error(s)
                    </Badge>
                    <Badge variant="outline">
                      Expires {new Date(preview.expiresAt).toLocaleString()}
                    </Badge>
                    {preview.centralDocumentRecordId ? (
                      <Badge variant="outline">DMS evidence retained</Badge>
                    ) : null}
                  </div>

                  {preview.errorCount > 0 ? (
                    <Alert variant="destructive">
                      <AlertCircle className="h-4 w-4" />
                      <AlertTitle>Posting is blocked</AlertTitle>
                      <AlertDescription>
                        Correct the validation errors in the template and upload
                        it again. No project BoQ records have been changed.
                      </AlertDescription>
                    </Alert>
                  ) : (
                    <Alert className="border-emerald-200 bg-emerald-50/70 text-emerald-900">
                      <CheckCircle2 className="h-4 w-4 text-emerald-700" />
                      <AlertTitle>Workbook is ready to post</AlertTitle>
                      <AlertDescription>
                        The staged lines will be revalidated inside the posting
                        transaction.
                      </AlertDescription>
                    </Alert>
                  )}

                  {preview.issues.length > 0 ? (
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <h3 className="text-sm font-semibold">
                          Validation findings
                        </h3>
                        <Button
                          variant="outline"
                          size="sm"
                          className="gap-2"
                          onClick={() => void downloadErrors()}
                          disabled={downloading !== null}
                        >
                          {downloading === 'errors' ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <Download className="h-4 w-4" />
                          )}
                          Download report
                        </Button>
                      </div>
                      <div className="max-h-56 overflow-auto rounded-md border">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Row</TableHead>
                              <TableHead>Field</TableHead>
                              <TableHead>Code</TableHead>
                              <TableHead>Finding</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {preview.issues.map((issue, index) => (
                              <TableRow
                                key={`${issue.rowNumber ?? 0}-${issue.code}-${index}`}
                              >
                                <TableCell>
                                  {issue.rowNumber ?? 'File'}
                                </TableCell>
                                <TableCell>
                                  {issue.field || 'Workbook'}
                                </TableCell>
                                <TableCell className="font-mono text-xs">
                                  {issue.code}
                                </TableCell>
                                <TableCell className="min-w-80 whitespace-normal">
                                  {issue.message}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                    </div>
                  ) : null}

                  {preview.lines.length > 0 ? (
                    <div className="space-y-2">
                      <h3 className="text-sm font-semibold">
                        Staged line preview
                      </h3>
                      <div className="max-h-64 overflow-auto rounded-md border">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Row</TableHead>
                              <TableHead>Package</TableHead>
                              <TableHead>Line / item</TableHead>
                              <TableHead>Description</TableHead>
                              <TableHead className="text-right">
                                Quantity
                              </TableHead>
                              <TableHead>Unit</TableHead>
                              <TableHead className="text-right">Rate</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {preview.lines.slice(0, 100).map((line) => (
                              <TableRow
                                key={`${line.rowNumber}-${line.clientLineKey}`}
                              >
                                <TableCell>{line.rowNumber}</TableCell>
                                <TableCell>{line.packageCode}</TableCell>
                                <TableCell>
                                  {[line.lineNumber, line.itemCode]
                                    .filter(Boolean)
                                    .join(' / ') || '—'}
                                </TableCell>
                                <TableCell className="min-w-64 whitespace-normal">
                                  {line.description}
                                </TableCell>
                                <TableCell className="text-right">
                                  {line.quantity.toLocaleString()}
                                </TableCell>
                                <TableCell>{line.unitOfMeasure}</TableCell>
                                <TableCell className="text-right">
                                  {line.unitRate == null
                                    ? '—'
                                    : `${line.currency} ${line.unitRate.toLocaleString()}`}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                      {preview.lines.length > 100 ? (
                        <p className="text-xs text-muted-foreground">
                          Showing the first 100 of {preview.lines.length}{' '}
                          validated lines.
                        </p>
                      ) : null}
                    </div>
                  ) : null}

                  {preview.errorCount === 0 ? (
                    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-blue-200 bg-blue-50/60 p-4">
                      <Checkbox
                        className="mt-0.5"
                        checked={reconciliationConfirmed}
                        onCheckedChange={(checked) =>
                          setReconciliationConfirmed(checked === true)
                        }
                        disabled={posting}
                      />
                      <span className="text-sm leading-6 text-blue-950">
                        I confirm that the staged line count, classifications,
                        quantities, units, rates, and totals reconcile to the
                        authorised source workbook. My authenticated user
                        identity and confirmation time will be recorded with
                        this posting.
                      </span>
                    </label>
                  ) : null}
                </>
              ) : null}
            </div>
          </ScrollArea>

          <DialogFooter className="border-t px-6 py-4">
            <Button
              variant="outline"
              onClick={() => handleOpenChange(false)}
              disabled={posting}
            >
              Cancel
            </Button>
            <Button
              onClick={() => void postStagedBoq()}
              disabled={
                !preview ||
                preview.errorCount > 0 ||
                !reconciliationConfirmed ||
                posting
              }
              className="gap-2"
            >
              {posting ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <ShieldCheck className="h-4 w-4" />
              )}
              Sign reconciliation & post
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
