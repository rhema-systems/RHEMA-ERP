'use client';

import { useEffect, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Download,
  FileSpreadsheet,
  Upload,
} from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  quantitySurveyTenderBoqService,
  saveTenderBoqTemplate,
  type TenderBoqContext,
  type TenderBoqLine,
  type TenderBoqSubmission,
} from '@/services/quantity-survey-tender-boq.service';

interface Props {
  tenderBidId: string;
  onCommitted: (lines: TenderBoqLine[]) => void;
}

const errorMessage = (error: unknown, fallback: string) =>
  error instanceof Error && error.message ? error.message : fallback;

export function QuantitySurveyTenderBoqSubmissionPanel({
  tenderBidId,
  onCommitted,
}: Props) {
  const [context, setContext] = useState<TenderBoqContext>();
  const [submission, setSubmission] = useState<TenderBoqSubmission>();
  const [file, setFile] = useState<File>();
  const [accepted, setAccepted] = useState(false);
  const [signatoryName, setSignatoryName] = useState('');
  const [busy, setBusy] = useState(false);
  const [notApplicable, setNotApplicable] = useState(false);
  const [loadError, setLoadError] = useState('');

  useEffect(() => {
    let active = true;
    Promise.all([
      quantitySurveyTenderBoqService.context(tenderBidId),
      quantitySurveyTenderBoqService.latest(tenderBidId).catch(() => undefined),
    ])
      .then(([nextContext, latest]) => {
        if (!active) return;
        setContext(nextContext);
        setSubmission(latest);
        if (latest?.status === 'Committed' || latest?.status === 'Vetted')
          onCommitted(latest.lines);
      })
      .catch((error: unknown) => {
        if (!active) return;
        const message = errorMessage(
          error,
          'The project tender BoQ control is unavailable.'
        );
        if (message.includes('not linked to a project purchase requisition'))
          setNotApplicable(true);
        else setLoadError(message);
      });
    return () => {
      active = false;
    };
  }, [tenderBidId, onCommitted]);

  if (notApplicable) return null;
  if (!context)
    return loadError ? (
      <Alert variant="destructive" className="mb-4">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>{loadError}</AlertDescription>
      </Alert>
    ) : null;

  const downloadTemplate = async () => {
    try {
      setBusy(true);
      const blob =
        await quantitySurveyTenderBoqService.downloadTemplate(tenderBidId);
      saveTenderBoqTemplate(blob, context.tenderNumber);
    } catch (error) {
      toast.error(
        errorMessage(error, 'Could not download the protected BoQ template.')
      );
    } finally {
      setBusy(false);
    }
  };

  const preview = async () => {
    if (!file)
      return toast.error('Select the completed protected .xlsx workbook.');
    if (!file.name.toLowerCase().endsWith('.xlsx'))
      return toast.error(
        'Only the protected .xlsx tender BoQ template is accepted.'
      );
    if (file.size > context.maximumFileSizeMb * 1024 * 1024)
      return toast.error(
        `The workbook exceeds the ${context.maximumFileSizeMb} MB limit.`
      );
    try {
      setBusy(true);
      setAccepted(false);
      setSubmission(
        await quantitySurveyTenderBoqService.preview(tenderBidId, file)
      );
    } catch (error) {
      toast.error(
        errorMessage(error, 'The tender BoQ could not be validated.')
      );
    } finally {
      setBusy(false);
    }
  };

  const commit = async () => {
    if (!submission?.previewToken) return;
    if (!accepted)
      return toast.error('Confirm the reconciliation declaration first.');
    if (context.requiresSignature && !signatoryName.trim())
      return toast.error('Enter the authorized signatory name.');
    try {
      setBusy(true);
      const committed = await quantitySurveyTenderBoqService.commit(
        tenderBidId,
        submission.id,
        {
          previewToken: submission.previewToken,
          reconciliationDeclaration: context.reconciliationDeclaration,
          signatoryName: signatoryName.trim() || undefined,
        }
      );
      setSubmission(committed);
      onCommitted(committed.lines);
      toast.success('Tender BoQ validated and applied to the bid items.');
    } catch (error) {
      toast.error(
        errorMessage(error, 'The validated tender BoQ could not be committed.')
      );
    } finally {
      setBusy(false);
    }
  };

  const committed =
    submission?.status === 'Committed' || submission?.status === 'Vetted';
  const validPreview =
    submission?.status === 'Validated' && submission.errorCount === 0;

  return (
    <Card className="mb-4 border-blue-200">
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2 text-base">
              <FileSpreadsheet className="h-4 w-4 text-blue-600" />
              Protected tender BoQ
            </CardTitle>
            <CardDescription>
              {context.projectCode} · approved BoQ v
              {context.tenderBoqVersionNumber} · {context.lineCount} selected
              line(s)
            </CardDescription>
          </div>
          {submission && (
            <Badge
              variant={
                committed
                  ? 'default'
                  : submission.errorCount
                    ? 'destructive'
                    : 'secondary'
              }
            >
              {submission.status}
            </Badge>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Download this server-generated workbook. Do not change locked
            identities, quantities, units, formulas, or the hidden control
            sheet; enter only offered quantity and unit rate.
          </AlertDescription>
        </Alert>

        <div className="flex flex-wrap items-end gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={downloadTemplate}
            disabled={busy}
          >
            <Download className="mr-2 h-4 w-4" /> Download template
          </Button>
          <div className="min-w-[260px] flex-1">
            <Label htmlFor="tender-boq-file" className="text-xs">
              Completed workbook
            </Label>
            <Input
              id="tender-boq-file"
              type="file"
              accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              onChange={(event) => setFile(event.target.files?.[0])}
              disabled={busy}
            />
          </div>
          <Button type="button" onClick={preview} disabled={busy || !file}>
            <Upload className="mr-2 h-4 w-4" /> Validate
          </Button>
        </div>

        {submission && (
          <>
            <div className="flex flex-wrap gap-2 text-sm">
              <Badge variant="outline">{submission.lineCount} line(s)</Badge>
              <Badge
                variant={submission.errorCount ? 'destructive' : 'outline'}
              >
                {submission.errorCount} error(s)
              </Badge>
              <Badge variant="outline">
                {submission.warningCount} warning(s)
              </Badge>
              <Badge variant="outline">
                {context.currency}{' '}
                {submission.submittedTotal.toLocaleString(undefined, {
                  minimumFractionDigits: 2,
                })}
              </Badge>
            </div>

            {submission.issues.length > 0 && (
              <div className="max-h-40 overflow-auto rounded-md border p-3 text-sm">
                {submission.issues.map((issue, index) => (
                  <div
                    key={`${issue.code}-${issue.rowNumber ?? 'file'}-${index}`}
                    className="mb-1"
                  >
                    <span className="font-medium">
                      {issue.rowNumber ? `Row ${issue.rowNumber}: ` : ''}
                    </span>
                    {issue.message}
                  </div>
                ))}
              </div>
            )}

            {submission.lines.length > 0 && (
              <div className="max-h-72 overflow-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Item</TableHead>
                      <TableHead>Qty</TableHead>
                      <TableHead>Rate</TableHead>
                      <TableHead>Total</TableHead>
                      <TableHead>Check</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {submission.lines.map((line) => (
                      <TableRow key={line.tenderItemId}>
                        <TableCell>
                          <div className="font-medium">
                            {line.itemCode || line.lineNumber || '—'}
                          </div>
                          <div className="max-w-[360px] truncate text-xs text-muted-foreground">
                            {line.description}
                          </div>
                        </TableCell>
                        <TableCell>{line.offeredQuantity}</TableCell>
                        <TableCell>{line.unitPrice.toLocaleString()}</TableCell>
                        <TableCell>
                          {line.calculatedLineTotal.toLocaleString()}
                        </TableCell>
                        <TableCell>
                          {Math.abs(line.arithmeticDifference) <= 0.01 ? (
                            <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                          ) : (
                            <AlertCircle className="h-4 w-4 text-red-600" />
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}

            {validPreview && (
              <div className="space-y-3 rounded-md border bg-muted/30 p-3">
                <label className="flex items-start gap-2 text-sm">
                  <Checkbox
                    checked={accepted}
                    onCheckedChange={(value) => setAccepted(value === true)}
                  />
                  <span>{context.reconciliationDeclaration}</span>
                </label>
                {context.requiresSignature && (
                  <div>
                    <Label htmlFor="tender-boq-signatory">
                      Authorized signatory
                    </Label>
                    <Input
                      id="tender-boq-signatory"
                      value={signatoryName}
                      onChange={(event) => setSignatoryName(event.target.value)}
                      maxLength={200}
                    />
                  </div>
                )}
                <Button
                  type="button"
                  onClick={commit}
                  disabled={busy || !accepted}
                >
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Apply validated BoQ
                </Button>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
