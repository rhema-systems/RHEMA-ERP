'use client';

import { useEffect, useState } from 'react';
import { FileImage, FileSpreadsheet, FileText, Loader2, Printer, Table2 } from 'lucide-react';
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
import { Label } from '@/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import type { OrganogramDimension } from '@/types/hr/organogram';
import type { ExportFormat, PdfPage } from './export';

/**
 * Choose what leaves the screen and how. The scope is the decision that matters: "what I can see"
 * is a picture of a moment, "this branch" is a document about one part of the organisation, and
 * "everything" is the whole establishment — which on live data can be thousands of boxes, so the
 * dialog refuses it past a threshold instead of hanging the tab.
 */

export type ExportScope = 'view' | 'branch' | 'whole';

export interface ExportOptions {
  format: ExportFormat;
  scope: ExportScope;
  page: PdfPage;
  legend: boolean;
}

export interface ExportScopeInfo {
  /** Cards each scope would render. `branch` is null when nothing is selected. */
  view: number;
  branch: number | null;
  whole: number;
  branchName: string | null;
}

/** Above this many cards an image export is refused; the table exports still run. */
export const IMAGE_CARD_LIMIT = 2500;

export interface OrgExportDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  dimension: OrganogramDimension;
  scopes: ExportScopeInfo;
  /** Pre-select the branch scope (from a card's menu). */
  initialScope?: ExportScope;
  onRun: (options: ExportOptions) => Promise<void>;
}

const FORMATS: { key: ExportFormat; label: string; hint: string; icon: typeof FileImage; image: boolean }[] = [
  { key: 'png', label: 'PNG image', hint: 'High-resolution picture of the chart', icon: FileImage, image: true },
  { key: 'pdf', label: 'PDF', hint: 'Fitted to a page, or one page the size of the chart', icon: FileText, image: true },
  { key: 'print', label: 'Print', hint: 'Opens the browser’s print dialog', icon: Printer, image: true },
  { key: 'xlsx', label: 'Excel workbook', hint: 'One row per box with its full path', icon: FileSpreadsheet, image: false },
  { key: 'csv', label: 'CSV', hint: 'The same table as plain text', icon: Table2, image: false },
];

export function OrgExportDialog({ open, onOpenChange, dimension, scopes, initialScope, onRun }: OrgExportDialogProps) {
  const [format, setFormat] = useState<ExportFormat>('png');
  const [scope, setScope] = useState<ExportScope>(initialScope ?? 'view');
  const [page, setPage] = useState<PdfPage>('a3');
  const [legend, setLegend] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setScope(initialScope ?? (scopes.branch !== null ? 'branch' : 'view'));
      setError(null);
    }
  }, [open, initialScope, scopes.branch]);

  const isImage = FORMATS.find((f) => f.key === format)?.image ?? true;
  const cardsFor = (s: ExportScope) => (s === 'view' ? scopes.view : s === 'branch' ? scopes.branch ?? 0 : scopes.whole);
  const tooBig = isImage && cardsFor(scope) > IMAGE_CARD_LIMIT;

  const run = async () => {
    setRunning(true);
    setError(null);
    try {
      await onRun({ format, scope, page, legend });
      onOpenChange(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'The export failed.');
    } finally {
      setRunning(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !running && onOpenChange(o)}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Print or export the organogram</DialogTitle>
          <DialogDescription>
            Images carry a title block with the organisation, the view, and when it was generated. Tables
            carry one row per box with its full path, ready to filter.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-5 sm:grid-cols-[1fr_1fr]">
          <fieldset className="space-y-2">
            <legend className="mb-2 text-sm font-medium">Format</legend>
            <RadioGroup value={format} onValueChange={(v) => setFormat(v as ExportFormat)} className="gap-1.5">
              {FORMATS.map((f) => (
                <label
                  key={f.key}
                  className={cn(
                    'flex cursor-pointer items-start gap-3 rounded-md border p-2.5 transition-colors hover:bg-accent',
                    format === f.key && 'border-primary bg-accent/60',
                  )}
                >
                  <RadioGroupItem value={f.key} className="mt-0.5" />
                  <f.icon className="text-muted-foreground mt-0.5 h-4 w-4 shrink-0" />
                  <span className="min-w-0">
                    <span className="block text-sm font-medium">{f.label}</span>
                    <span className="text-muted-foreground block text-xs">{f.hint}</span>
                  </span>
                </label>
              ))}
            </RadioGroup>
          </fieldset>

          <div className="space-y-4">
            <fieldset className="space-y-2">
              <legend className="mb-2 text-sm font-medium">What to include</legend>
              <RadioGroup value={scope} onValueChange={(v) => setScope(v as ExportScope)} className="gap-1.5">
                <ScopeOption value="view" title="What is on screen" hint={`${scopes.view.toLocaleString()} boxes, as currently expanded`} />
                <ScopeOption
                  value="branch"
                  title={scopes.branchName ? `${scopes.branchName} and everything below` : 'Selected branch'}
                  hint={scopes.branch === null ? 'Select a box first' : `${scopes.branch.toLocaleString()} boxes, fully expanded`}
                  disabled={scopes.branch === null}
                />
                <ScopeOption value="whole" title="The whole chart" hint={`${scopes.whole.toLocaleString()} boxes, fully expanded`} />
              </RadioGroup>
            </fieldset>

            {format === 'pdf' && (
              <div className="space-y-1.5">
                <Label htmlFor="org-export-page">Page</Label>
                <Select value={page} onValueChange={(v) => setPage(v as PdfPage)}>
                  <SelectTrigger id="org-export-page">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="a4">A4 landscape, fitted</SelectItem>
                    <SelectItem value="a3">A3 landscape, fitted</SelectItem>
                    <SelectItem value="actual">Actual size, one page</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            )}

            {isImage && (
              <label className="flex items-center gap-2 text-sm">
                <Checkbox checked={legend} onCheckedChange={(v) => setLegend(!!v)} />
                Include the legend
              </label>
            )}

            {dimension === 'people' && !isImage && (
              <p className="text-muted-foreground text-xs">
                Email addresses are never included in organogram exports. Use the employee register for contact lists.
              </p>
            )}
          </div>
        </div>

        {tooBig && (
          <p className="rounded-md border border-amber-300 bg-amber-50 p-2.5 text-xs text-amber-900 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
            {cardsFor(scope).toLocaleString()} boxes is too many for one image (the limit is {IMAGE_CARD_LIMIT.toLocaleString()}). Export a
            branch, collapse the chart to what you need, or choose Excel or CSV for the full list.
          </p>
        )}
        {error && <p className="text-destructive text-sm">{error}</p>}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={running}>
            Cancel
          </Button>
          <Button onClick={run} disabled={running || tooBig || (scope === 'branch' && scopes.branch === null)}>
            {running && <Loader2 className="h-4 w-4 animate-spin" />}
            {format === 'print' ? 'Print' : 'Export'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ScopeOption({ value, title, hint, disabled }: { value: ExportScope; title: string; hint: string; disabled?: boolean }) {
  return (
    <label className={cn('flex cursor-pointer items-start gap-3 rounded-md border p-2.5 hover:bg-accent', disabled && 'cursor-not-allowed opacity-50')}>
      <RadioGroupItem value={value} disabled={disabled} className="mt-0.5" />
      <span className="min-w-0">
        <span className="block truncate text-sm font-medium">{title}</span>
        <span className="text-muted-foreground block text-xs">{hint}</span>
      </span>
    </label>
  );
}
