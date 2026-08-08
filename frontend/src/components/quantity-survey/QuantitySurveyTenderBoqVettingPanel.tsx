'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  CheckCircle2,
  FileSpreadsheet,
  RefreshCw,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  quantitySurveyTenderBoqService,
  type TenderBoqSubmission,
} from '@/services/quantity-survey-tender-boq.service';

export function QuantitySurveyTenderBoqVettingPanel({
  tenderBidId,
}: {
  tenderBidId: string;
}) {
  const { hasPermission } = useAuth();
  const canManage = hasPermission('quantity-survey.transactions.approve');
  const [history, setHistory] = useState<TenderBoqSubmission[]>([]);
  const [selected, setSelected] = useState<TenderBoqSubmission>();
  const [note, setNote] = useState('');
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    if (!canManage) return;
    try {
      setLoading(true);
      const rows = await quantitySurveyTenderBoqService.history(tenderBidId);
      setHistory(rows);
      setSelected(
        (current) => rows.find((row) => row.id === current?.id) ?? rows[0]
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Could not load tender BoQ vetting history.'
      );
    } finally {
      setLoading(false);
    }
  }, [canManage, tenderBidId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (!canManage) return null;

  const vet = async (decision: 'Accepted' | 'Rejected') => {
    if (!selected) return;
    if (note.trim().length < 3)
      return toast.error('Enter a review note of at least 3 characters.');
    try {
      setLoading(true);
      await quantitySurveyTenderBoqService.vet(tenderBidId, selected.id, {
        decision,
        note: note.trim(),
        rowVersion: selected.rowVersion,
      });
      setNote('');
      toast.success(`Tender BoQ ${decision.toLowerCase()}.`);
      await load();
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'The tender BoQ review could not be saved.'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between gap-3">
            <div>
              <CardTitle className="flex items-center gap-2 text-base">
                <FileSpreadsheet className="h-4 w-4" /> Tender BoQ submissions
              </CardTitle>
              <CardDescription>
                Immutable approved-BoQ lineage, arithmetic checks, central
                document evidence, and QS review.
              </CardDescription>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={load}
              disabled={loading}
            >
              <RefreshCw className="mr-2 h-4 w-4" /> Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {history.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No project tender BoQ has been committed for this bid.
            </p>
          ) : (
            <div className="overflow-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Submitted</TableHead>
                    <TableHead>Channel</TableHead>
                    <TableHead>Lines</TableHead>
                    <TableHead>Total</TableHead>
                    <TableHead>Validation</TableHead>
                    <TableHead>Vetting</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {history.map((row) => (
                    <TableRow
                      key={row.id}
                      className={
                        selected?.id === row.id
                          ? 'bg-muted/60'
                          : 'cursor-pointer'
                      }
                      onClick={() => {
                        setSelected(row);
                        setNote('');
                      }}
                    >
                      <TableCell>
                        {new Date(row.submittedAt).toLocaleString()}
                      </TableCell>
                      <TableCell>{row.channel}</TableCell>
                      <TableCell>
                        {row.committedLineCount || row.lineCount}
                      </TableCell>
                      <TableCell>
                        {row.submittedTotal.toLocaleString(undefined, {
                          minimumFractionDigits: 2,
                        })}
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant={row.errorCount ? 'destructive' : 'outline'}
                        >
                          {row.errorCount
                            ? `${row.errorCount} error(s)`
                            : row.status}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <Badge variant="secondary">{row.vettingStatus}</Badge>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      {selected && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Line reconciliation</CardTitle>
            <CardDescription>
              Approved publication {selected.tenderBoqVersionId} ·{' '}
              {selected.originalFileName}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="max-h-80 overflow-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Line</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead>Tender qty</TableHead>
                    <TableHead>Offered qty</TableHead>
                    <TableHead>Rate</TableHead>
                    <TableHead>Total</TableHead>
                    <TableHead>Variance</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {selected.lines.map((line) => (
                    <TableRow key={line.tenderItemId}>
                      <TableCell>
                        {line.itemCode || line.lineNumber || '—'}
                      </TableCell>
                      <TableCell className="max-w-[360px] truncate">
                        {line.description}
                      </TableCell>
                      <TableCell>{line.tenderQuantity}</TableCell>
                      <TableCell>{line.offeredQuantity}</TableCell>
                      <TableCell>{line.unitPrice.toLocaleString()}</TableCell>
                      <TableCell>
                        {line.calculatedLineTotal.toLocaleString()}
                      </TableCell>
                      <TableCell>
                        {line.arithmeticDifference.toLocaleString()}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>

            {selected.status === 'Committed' &&
              selected.vettingStatus === 'Pending' && (
                <div className="space-y-2 rounded-md border p-3">
                  <Textarea
                    value={note}
                    onChange={(event) => setNote(event.target.value)}
                    placeholder="Record the QS review basis and any observed variance."
                    maxLength={1000}
                  />
                  <div className="flex gap-2">
                    <Button
                      onClick={() => vet('Accepted')}
                      disabled={loading || note.trim().length < 3}
                    >
                      <CheckCircle2 className="mr-2 h-4 w-4" /> Accept
                    </Button>
                    <Button
                      variant="destructive"
                      onClick={() => vet('Rejected')}
                      disabled={loading || note.trim().length < 3}
                    >
                      <XCircle className="mr-2 h-4 w-4" /> Reject
                    </Button>
                  </div>
                </div>
              )}
            {selected.vettingNote && (
              <p className="text-sm">
                <span className="font-medium">Review note:</span>{' '}
                {selected.vettingNote}
              </p>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
