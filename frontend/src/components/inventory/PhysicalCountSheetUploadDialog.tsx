'use client';

import React, { useState } from 'react';
import * as XLSX from 'xlsx';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { inventoryManagementService as service, type PhysicalCountDetailDto } from '@/services/inventoryManagementService';
import { COUNT_SHEET_HEADERS, COUNT_SHEET_HEADERS_WITHOUT_LOCATION, hasCountSheetLocations, parseCountSheet, type CountSheetRow } from '@/lib/physical-count-sheet';
import { toast } from 'sonner';

type Props = {
  count: PhysicalCountDetailDto;
  onClose: () => void;
  onSaved: () => Promise<void>;
  errorMessage: (error: unknown, fallback: string) => string;
};

export function PhysicalCountSheetUploadDialog({ count, onClose, onSaved, errorMessage }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [rows, setRows] = useState<CountSheetRow[]>([]);
  const [requestKey, setRequestKey] = useState('');
  const [blankCount, setBlankCount] = useState(0);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [started, setStarted] = useState(false);
  const includeLocation = hasCountSheetLocations(count.items);

  const selectFile = async (next: File | undefined) => {
    setFile(null); setRows([]); setRequestKey(''); setError(''); setBlankCount(0);
    if (!next) return;
    setBusy(true);
    try {
      if (!/\.xlsx$/i.test(next.name) || next.size > 10 * 1024 * 1024) throw new Error('Choose an .xlsx count sheet smaller than 10 MB.');
      const data = XLSX.read(await next.arrayBuffer(), { type: 'array', cellFormula: true });
      const preview = parseCountSheet(data, count.items);
      if (!preview.rows.length) throw new Error('No quantities entered. Fill Counted Qty in Excel; enter 0 only when none was found.');
      if (preview.rows.length + preview.blankCount !== count.items.length) throw new Error('Keep every item row in the count sheet.');
      if (count.items.some(item => item.isCounted && !preview.rows.some(row => row.item.id === item.id)))
        throw new Error('Enter a quantity for each previously counted item. A replacement cannot omit a saved count.');
      setRows(preview.rows); setBlankCount(preview.blankCount); setFile(next);
      setRequestKey(crypto.randomUUID());
    } catch (failure) { setError(errorMessage(failure, 'The count sheet could not be read.')); }
    finally { setBusy(false); }
  };

  const save = async () => {
    if (!file || !rows.length || busy || !['InProgress', 'UnderReview'].includes(count.status)) return;
    setBusy(true); setStarted(true); setError('');
    try {
      // Server scans/parses the actual file and saves all quantities + its source in one transaction.
      await service.importPhysicalCountSheet(count.id, file, count.rowVersion,
        count.items.map(item => ({ id: item.id, rowVersion: item.rowVersion })), requestKey);
    } catch (failure) {
      setError(`${errorMessage(failure, 'The count sheet could not be saved.')} Retry uses the same upload reference and will not duplicate a successful save. If the count changed, close and reopen it.`);
      setBusy(false);
      return;
    }
    try {
      await onSaved();
      toast.success(`${rows.length} quantities saved. This is now the current count sheet.`);
    } catch {
      toast.warning('Counts and evidence saved. Refresh the register before continuing.');
    }
    setBusy(false);
    onClose();
  };

  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }}>
    <DialogContent className="flex h-[600px] max-h-[90dvh] flex-col overflow-hidden sm:max-w-[800px]">
      <DialogHeader className="shrink-0">
        <DialogTitle>Upload count sheet</DialogTitle>
        <DialogDescription>{count.countNumber} · Fill Counted Qty and any defective details. Blank cells stay uncounted; 0 means none found.</DialogDescription>
      </DialogHeader>
      <div className="min-h-0 flex-1 space-y-4 overflow-y-auto">
        <div className="space-y-2"><Label htmlFor="count-sheet-file">Completed Excel count sheet</Label><Input id="count-sheet-file" type="file" accept=".xlsx" disabled={busy || started} onChange={event => void selectFile(event.target.files?.[0])} /></div>
        {error && <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}
        {rows.length > 0 && <>
          <p className="text-sm text-muted-foreground">{rows.length} quantities to save · {blankCount} blank rows skipped. Existing counts on the listed lines will be replaced.</p>
          <Table><TableHeader><TableRow>{(includeLocation ? COUNT_SHEET_HEADERS : COUNT_SHEET_HEADERS_WITHOUT_LOCATION).map(header => <TableHead key={header}>{header}</TableHead>)}</TableRow></TableHeader><TableBody>{rows.map(({ item, quantity, defectiveQuantity, defectiveNotes }) => <TableRow key={item.id}><TableCell>{item.itemCode}</TableCell><TableCell>{item.itemName}</TableCell><TableCell>{item.unitOfMeasure}</TableCell>{includeLocation && <TableCell>{item.locationName || '—'}</TableCell>}<TableCell className="text-right">{quantity}</TableCell><TableCell className="text-right">{defectiveQuantity}</TableCell><TableCell>{defectiveNotes}</TableCell></TableRow>)}</TableBody></Table>
          <p className="text-sm text-muted-foreground">Save uses this file for the count quantities and marks it as the current count sheet. Earlier files remain in history.</p>
        </>}
      </div>
      <DialogFooter className="shrink-0"><Button variant="outline" disabled={busy} onClick={onClose}>Close</Button><Button disabled={busy || !rows.length} onClick={() => void save()}>{busy ? 'Saving…' : started ? 'Retry save' : 'Save count sheet'}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}
