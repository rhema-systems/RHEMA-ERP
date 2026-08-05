'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { Loader2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { bulkImportService } from '@/services/hr/attendance.service';
import { IMPORT_SOURCE_TYPE_OPTIONS } from '@/types/hr/attendance';
import type {
  AttendanceImportSourceType,
  CreateBulkImportRow,
} from '@/types/hr/attendance';

/**
 * Staging a bulk import.
 *
 * The API takes rows as data, not a file upload — `rawData` per row plus optional parsed
 * fields — so the CSV is parsed in the browser and each line is sent both raw (for the audit
 * trail and for the server to re-parse) and pre-split where the columns are recognisable.
 *
 * Employee matching is left to the server: a row carries the identifier as typed, and the
 * import reports per-row which ones could not be resolved.
 */

const EXPECTED_COLUMNS = 'employeeNumber, date (YYYY-MM-DD), checkIn (HH:mm), checkOut (HH:mm)';

interface ParsedRow extends CreateBulkImportRow {
  employeeRef?: string;
}

/** Splits pasted CSV into rows, tolerating a header line and blank lines. */
function parseCsv(text: string): ParsedRow[] {
  const lines = text
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean);
  if (lines.length === 0) return [];

  // Drop a header if the first cell clearly isn't data.
  const firstCell = lines[0].split(',')[0]?.trim().toLowerCase() ?? '';
  const body = /employee|staff|number|id/.test(firstCell) ? lines.slice(1) : lines;

  return body.map((line, index) => {
    const cells = line.split(',').map((c) => c.trim());
    const [employeeRef, date, checkIn, checkOut] = cells;
    const isDate = /^\d{4}-\d{2}-\d{2}$/.test(date ?? '');
    const asTime = (v?: string) =>
      v && /^\d{1,2}:\d{2}$/.test(v) ? `${v.padStart(5, '0')}:00` : null;

    return {
      rowNumber: index + 1,
      rawData: line,
      employeeRef,
      employeeId: null,
      attendanceDate: isDate ? date : null,
      checkInTime: asTime(checkIn),
      checkOutTime: asTime(checkOut),
    };
  });
}

export default function NewBulkImportPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [sourceType, setSourceType] = useState<AttendanceImportSourceType>('CSV');
  const [sourceFileName, setSourceFileName] = useState('');
  const [notes, setNotes] = useState('');
  const [csv, setCsv] = useState('');
  const [saving, setSaving] = useState(false);

  const parsed = useMemo(() => parseCsv(csv), [csv]);

  const handleFile = async (file?: File | null) => {
    if (!file) return;
    setSourceFileName(file.name);
    setCsv(await file.text());
  };

  const stage = async () => {
    if (parsed.length === 0) {
      toast({ title: 'Nothing to import', description: 'Paste or upload some rows first.', variant: 'destructive' });
      return;
    }
    setSaving(true);
    try {
      const created = await bulkImportService.initiate({
        // The importing user is resolved from the token; this field is required but ignored.
        importedById: '00000000-0000-0000-0000-000000000000',
        sourceFileName: sourceFileName || null,
        sourceType,
        notes: notes.trim() || null,
        rows: parsed.map(({ employeeRef, ...row }) => row),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'bulk-imports'] });
      toast({
        title: 'Staged',
        description: `${created.importReference} staged with ${parsed.length} rows. Review, then process it.`,
      });
      router.push(`/hr/attendance/imports/${created.id}`);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to stage the import.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New Bulk Import"
        description="Stage attendance rows for review. Nothing is written until the batch is processed."
        backHref="/hr/attendance/imports"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Source</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="sourceType">Source type</Label>
              <Select
                value={sourceType}
                onValueChange={(v) => setSourceType(v as AttendanceImportSourceType)}
              >
                <SelectTrigger id="sourceType">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {IMPORT_SOURCE_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="file">CSV file</Label>
              <Input
                id="file"
                type="file"
                accept=".csv,text/csv,text/plain"
                onChange={(e) => handleFile(e.target.files?.[0])}
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="notes">Notes</Label>
            <Textarea
              id="notes"
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Optional — where this batch came from"
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Rows</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="csv">Paste rows</Label>
            <Textarea
              id="csv"
              rows={8}
              value={csv}
              onChange={(e) => setCsv(e.target.value)}
              className="font-mono text-xs"
              placeholder={`${EXPECTED_COLUMNS}\nEMP001,2026-08-03,08:05,17:10`}
            />
            <p className="text-xs text-muted-foreground">
              Expected columns: {EXPECTED_COLUMNS}. A header row is detected and skipped. Each line
              is also sent verbatim, so the server can re-parse anything this preview misreads.
            </p>
          </div>

          {parsed.length > 0 && (
            <div className="space-y-2">
              <p className="text-sm text-muted-foreground">
                {parsed.length} {parsed.length === 1 ? 'row' : 'rows'} parsed — showing the first 10.
              </p>
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[60px]">#</TableHead>
                      <TableHead>Employee ref</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>In</TableHead>
                      <TableHead>Out</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {parsed.slice(0, 10).map((row) => (
                      <TableRow key={row.rowNumber}>
                        <TableCell>{row.rowNumber}</TableCell>
                        <TableCell>{row.employeeRef || '—'}</TableCell>
                        <TableCell
                          className={row.attendanceDate ? undefined : 'text-red-600'}
                        >
                          {row.attendanceDate || 'unparsed'}
                        </TableCell>
                        <TableCell>{row.checkInTime?.slice(0, 5) || '—'}</TableCell>
                        <TableCell>{row.checkOutTime?.slice(0, 5) || '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          onClick={() => router.push('/hr/attendance/imports')}
          disabled={saving}
        >
          Cancel
        </Button>
        <Button onClick={stage} disabled={saving || parsed.length === 0}>
          {saving ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Upload className="mr-2 h-4 w-4" />
          )}
          Stage {parsed.length || ''} {parsed.length === 1 ? 'row' : 'rows'}
        </Button>
      </div>
    </div>
  );
}
