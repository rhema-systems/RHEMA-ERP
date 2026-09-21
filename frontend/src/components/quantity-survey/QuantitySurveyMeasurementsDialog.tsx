'use client';

import { useCallback, useMemo, useRef, useState } from 'react';
import {
  ClipboardList,
  FileUp,
  History,
  Plus,
  Save,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { MeasurementSiteLocationPicker, measurementSiteOptions } from './MeasurementSiteLocationPicker';
import {
  quantitySurveyMeasurementService as service,
  type MeasurementEvidenceType,
  type MeasurementFormulaType,
  type MeasurementLineInput,
  type MeasurementLookups,
  type MeasurementRevision,
  type MeasurementSheet,
  type MeasurementSourceType,
} from '@/services/quantity-survey-measurement.service';

type Props = { projectId: string; projectSite?: string };
type RequestState = { fingerprint: string; id: string };

const newLine = (sequence: number): MeasurementLineInput => ({
  clientLineKey: crypto.randomUUID(),
  sequence,
  description: '',
  formulaType: 'Count',
  timesing: 1,
  isDeduction: false,
});
const formulaTypes: MeasurementFormulaType[] = [
  'Count',
  'Length',
  'Area',
  'Volume',
];
const evidenceTypes: MeasurementEvidenceType[] = [
  'DrawingMarkup',
  'SitePhoto',
  'MeasurementWorkbook',
  'SupportingDocument',
];
const sourceLabel = (value: MeasurementSourceType | number) =>
  value === 'Design' || value === 0 ? 'Design' : 'Site';
const formulaLabel = (value: MeasurementFormulaType) =>
  value === 'Count'
    ? 'Count'
    : value === 'Length'
      ? 'Length'
      : value === 'Area'
        ? 'Area'
        : 'Volume';

export function QuantitySurveyMeasurementsDialog({ projectId, projectSite }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const canManage = hasPermission('quantity-survey.measurements.manage');
  const canAudit = hasPermission('quantity-survey.audit.read');
  const requests = useRef<Record<string, RequestState>>({});
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [lookups, setLookups] = useState<MeasurementLookups>({
    approvedBoqLines: [],
    approvedDrawings: [],
  });
  const [sheets, setSheets] = useState<MeasurementSheet[]>([]);
  const [selected, setSelected] = useState<MeasurementSheet>();
  const [editingId, setEditingId] = useState<string>();
  const [boqLineId, setBoqLineId] = useState('');
  const [drawingId, setDrawingId] = useState('');
  const [sourceType, setSourceType] = useState<MeasurementSourceType>('Design');
  const [title, setTitle] = useState('');
  const [measurementDate, setMeasurementDate] = useState(
    new Date().toISOString().slice(0, 10)
  );
  const defaultSite = measurementSiteOptions([projectSite])[0] ?? '';
  const [siteLocation, setSiteLocation] = useState(defaultSite);
  const [lines, setLines] = useState<MeasurementLineInput[]>([newLine(1)]);
  const [evidenceTitle, setEvidenceTitle] = useState('');
  const [evidenceType, setEvidenceType] =
    useState<MeasurementEvidenceType>('SupportingDocument');
  const [file, setFile] = useState<File>();
  const [history, setHistory] = useState<MeasurementRevision[]>([]);

  const requestId = (key: string, payload: unknown) => {
    const fingerprint = JSON.stringify(payload);
    const current = requests.current[key];
    if (current?.fingerprint === fingerprint) return current.id;
    const next = { fingerprint, id: crypto.randomUUID() };
    requests.current[key] = next;
    return next.id;
  };
  const completeRequest = (key: string) => delete requests.current[key];

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [lookupResult, page] = await Promise.all([
        service.lookups(projectId),
        service.list({ projectId, pageSize: 100 }),
      ]);
      setLookups(lookupResult);
      setSheets(page.items);
      setSelected((current) =>
        current
          ? page.items.find((value) => value.id === current.id)
          : page.items[0]
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to load measurement sheets'
      );
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  const total = useMemo(
    () =>
      lines.reduce((sum, line) => {
        const raw =
          line.timesing *
          (line.formulaType === 'Count' ? 1 : line.length || 0) *
          (line.formulaType === 'Area' || line.formulaType === 'Volume'
            ? line.width || 0
            : 1) *
          (line.formulaType === 'Volume' ? line.height || 0 : 1);
        return sum + (line.isDeduction ? -raw : raw);
      }, 0),
    [lines]
  );

  const reset = () => {
    setEditingId(undefined);
    setBoqLineId('');
    setDrawingId('');
    setSourceType('Design');
    setTitle('');
    setMeasurementDate(new Date().toISOString().slice(0, 10));
    setSiteLocation(defaultSite);
    setLines([newLine(1)]);
  };

  const edit = (sheet: MeasurementSheet) => {
    if (sheet.status !== 'Draft') return;
    setEditingId(sheet.id);
    setSelected(sheet);
    setBoqLineId(sheet.projectBoqVersionLineId);
    setDrawingId(sheet.projectDrawingId ?? '');
    setSourceType(sourceLabel(sheet.sourceType) as MeasurementSourceType);
    setTitle(sheet.title);
    setMeasurementDate(sheet.measurementDate.slice(0, 10));
    setSiteLocation(sheet.siteLocation ?? '');
    setLines(
      sheet.lines.map((line) => ({
        clientLineKey: line.clientLineKey,
        sequence: line.sequence,
        description: line.description,
        formulaType: line.formulaType,
        timesing: line.timesing,
        length: line.length,
        width: line.width,
        height: line.height,
        isDeduction: line.isDeduction,
        notes: line.notes,
      }))
    );
  };

  const save = async () => {
    if (!boqLineId || title.trim().length < 3 || !measurementDate) {
      toast.error(
        'Select an approved BoQ item and enter the title and measurement date.'
      );
      return;
    }
    if (sourceType === 'Design' && !drawingId) {
      toast.error('Design measurements require an approved drawing revision.');
      return;
    }
    if (sourceType === 'Site' && !siteLocation.trim()) {
      toast.error('Site measurements require a site location.');
      return;
    }
    if (
      lines.some((line) => !line.description.trim() || line.timesing <= 0) ||
      total <= 0
    ) {
      toast.error(
        'Complete the typed dimension rows; the net measured quantity must be positive.'
      );
      return;
    }
    const payload = {
      projectId,
      projectBoqVersionLineId: boqLineId,
      projectDrawingId: drawingId || undefined,
      sourceType,
      title: title.trim(),
      measurementDate: new Date(`${measurementDate}T00:00:00Z`).toISOString(),
      siteLocation: siteLocation.trim() || undefined,
      lines,
    };
    const key = editingId ? `update:${editingId}` : 'create';
    setLoading(true);
    try {
      const saved = editingId
        ? await service.update(editingId, {
            ...payload,
            clientRequestId: requestId(key, payload),
            rowVersion: selected?.rowVersion,
          })
        : await service.create({
            ...payload,
            clientRequestId: requestId(key, payload),
          });
      completeRequest(key);
      toast.success(
        editingId ? 'Draft measurement updated' : 'Measurement sheet created'
      );
      await load();
      setSelected(saved);
      reset();
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to save measurement sheet'
      );
    } finally {
      setLoading(false);
    }
  };

  const attach = async () => {
    if (!selected || !file || evidenceTitle.trim().length < 3) {
      toast.error('Select a draft sheet, evidence file, and title.');
      return;
    }
    const fingerprint = {
      sheet: selected.id,
      evidenceType,
      title: evidenceTitle.trim(),
      file: `${file.name}:${file.size}:${file.lastModified}`,
    };
    const key = `attach:${selected.id}`;
    setLoading(true);
    try {
      await service.addAttachment(selected.id, {
        clientRequestId: requestId(key, fingerprint),
        evidenceType,
        title: evidenceTitle.trim(),
        file,
      });
      completeRequest(key);
      toast.success('Clean-scanned evidence linked through the central DMS');
      setEvidenceTitle('');
      setFile(undefined);
      await load();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to attach evidence'
      );
    } finally {
      setLoading(false);
    }
  };

  const record = async () => {
    if (!selected || selected.status !== 'Draft') return;
    const key = `record:${selected.id}`;
    const payload = { id: selected.id, rowVersion: selected.rowVersion };
    setLoading(true);
    try {
      const recorded = await service.record(selected.id, {
        clientRequestId: requestId(key, payload),
        rowVersion: selected.rowVersion,
      });
      completeRequest(key);
      toast.success('Measurement sheet recorded and frozen');
      await load();
      setSelected(recorded);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to record measurement'
      );
    } finally {
      setLoading(false);
    }
  };

  if (!canRead) return null;
  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        setOpen(value);
        if (value) void load();
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" className="gap-2">
          <ClipboardList className="h-4 w-4" /> Measurements
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[94vh] max-w-[96vw] overflow-y-auto xl:max-w-7xl">
        <DialogHeader>
          <DialogTitle>Taking-off and measurement sheets</DialogTitle>
        </DialogHeader>
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.7fr)]">
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="font-semibold">Project sheets</h3>
              {canManage ? (
                <Button size="sm" variant="outline" onClick={reset}>
                  <Plus className="mr-1 h-4 w-4" />
                  New
                </Button>
              ) : null}
            </div>
            {sheets.length === 0 ? (
              <p className="rounded border p-4 text-sm text-muted-foreground">
                No measurement sheets yet.
              </p>
            ) : (
              sheets.map((sheet) => (
                <button
                  key={sheet.id}
                  type="button"
                  onClick={() => setSelected(sheet)}
                  className={`w-full rounded-lg border p-3 text-left ${selected?.id === sheet.id ? 'border-primary bg-primary/5' : ''}`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium">{sheet.sheetReference}</span>
                    <Badge
                      variant={
                        sheet.status === 'Recorded' ? 'default' : 'secondary'
                      }
                    >
                      {sheet.status}
                    </Badge>
                  </div>
                  <div className="mt-1 text-sm">{sheet.title}</div>
                  <div className="mt-1 text-xs text-muted-foreground">
                    {sheet.boqLineLabel} ·{' '}
                    {sheet.totalMeasuredQuantity.toFixed(4)}{' '}
                    {sheet.unitOfMeasure ?? ''}
                  </div>
                </button>
              ))
            )}
            {selected ? (
              <div className="space-y-3 rounded-lg border p-3 text-sm">
                <div className="flex flex-wrap gap-2">
                  {selected.status === 'Draft' && canManage ? (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => edit(selected)}
                    >
                      Edit draft
                    </Button>
                  ) : null}
                  {selected.status === 'Draft' && canManage ? (
                    <Button
                      size="sm"
                      onClick={() => void record()}
                      disabled={loading}
                    >
                      Record & freeze
                    </Button>
                  ) : null}
                  {canAudit ? (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={async () =>
                        setHistory(await service.history(selected.id))
                      }
                    >
                      <History className="mr-1 h-4 w-4" />
                      History
                    </Button>
                  ) : null}
                </div>
                <div>
                  <span className="text-muted-foreground">Source:</span>{' '}
                  {sourceLabel(selected.sourceType)}{' '}
                  {selected.drawingLabel ? `· ${selected.drawingLabel}` : ''}
                </div>
                <div>
                  <span className="text-muted-foreground">Evidence:</span>{' '}
                  {selected.attachments.length} central-DMS file(s)
                </div>
                {history.map((item) => (
                  <div key={item.id} className="rounded bg-muted p-2">
                    <b>{item.action}</b> · {item.actorName}
                    <div className="text-xs text-muted-foreground">
                      {new Date(item.createdAt).toLocaleString()} ·{' '}
                      {item.correlationId}
                    </div>
                  </div>
                ))}
              </div>
            ) : null}
          </div>

          <div className="space-y-4">
            {canManage ? (
              <div className="space-y-4 rounded-lg border p-4">
                <div className="grid gap-3 md:grid-cols-2">
                  <div>
                    <Label>Approved BoQ item</Label>
                    <Select
                      value={boqLineId}
                      onValueChange={setBoqLineId}
                      disabled={Boolean(editingId)}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select approved item" />
                      </SelectTrigger>
                      <SelectContent>
                        {lookups.approvedBoqLines.map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.group} · {item.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label>Measurement source</Label>
                    <Select
                      value={sourceType}
                      onValueChange={(value) =>
                        setSourceType(value as MeasurementSourceType)
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Design">Design</SelectItem>
                        <SelectItem value="Site">Site</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label>Title</Label>
                    <Input
                      value={title}
                      onChange={(event) => setTitle(event.target.value)}
                    />
                  </div>
                  <div>
                    <Label>Measurement date</Label>
                    <Input
                      type="date"
                      value={measurementDate}
                      onChange={(event) =>
                        setMeasurementDate(event.target.value)
                      }
                    />
                  </div>
                  <div>
                    <Label>
                      Approved drawing revision{' '}
                      {sourceType === 'Design' ? '*' : '(optional)'}
                    </Label>
                    <Select
                      value={drawingId || 'none'}
                      onValueChange={(value) =>
                        setDrawingId(value === 'none' ? '' : value)
                      }
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select approved drawing" />
                      </SelectTrigger>
                      <SelectContent>
                        {sourceType === 'Site' ? (
                          <SelectItem value="none">No drawing</SelectItem>
                        ) : null}
                        {lookups.approvedDrawings.map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.group} · {item.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <MeasurementSiteLocationPicker
                    value={siteLocation}
                    suggestions={measurementSiteOptions([defaultSite, ...sheets.map((sheet) => sheet.siteLocation)])}
                    required={sourceType === 'Site'}
                    onChange={setSiteLocation}
                  />
                </div>
                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <h4 className="font-medium">
                      Server-calculated dimension rows
                    </h4>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() =>
                        setLines((current) => [
                          ...current,
                          newLine(current.length + 1),
                        ])
                      }
                    >
                      <Plus className="mr-1 h-4 w-4" />
                      Row
                    </Button>
                  </div>
                  {lines.map((line, index) => (
                    <div
                      key={line.clientLineKey}
                      className="grid gap-2 rounded border p-3 md:grid-cols-6"
                    >
                      <Input
                        className="md:col-span-2"
                        placeholder="Description"
                        value={line.description}
                        onChange={(event) =>
                          setLines((current) =>
                            current.map((value, position) =>
                              position === index
                                ? { ...value, description: event.target.value }
                                : value
                            )
                          )
                        }
                      />
                      <Select
                        value={line.formulaType}
                        onValueChange={(value) =>
                          setLines((current) =>
                            current.map((item, position) =>
                              position === index
                                ? {
                                    ...item,
                                    formulaType:
                                      value as MeasurementFormulaType,
                                    length: undefined,
                                    width: undefined,
                                    height: undefined,
                                  }
                                : item
                            )
                          )
                        }
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {formulaTypes.map((value) => (
                            <SelectItem key={value} value={value}>
                              {formulaLabel(value)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <Input
                        type="number"
                        min="0.0001"
                        step="0.0001"
                        aria-label="Timesing"
                        value={line.timesing}
                        onChange={(event) =>
                          setLines((current) =>
                            current.map((item, position) =>
                              position === index
                                ? {
                                    ...item,
                                    timesing: Number(event.target.value),
                                  }
                                : item
                            )
                          )
                        }
                      />
                      {line.formulaType !== 'Count' ? (
                        <Input
                          type="number"
                          min="0.0001"
                          step="0.0001"
                          placeholder="Length"
                          value={line.length ?? ''}
                          onChange={(event) =>
                            setLines((current) =>
                              current.map((item, position) =>
                                position === index
                                  ? {
                                      ...item,
                                      length:
                                        Number(event.target.value) || undefined,
                                    }
                                  : item
                              )
                            )
                          }
                        />
                      ) : (
                        <div />
                      )}
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        disabled={lines.length === 1}
                        onClick={() =>
                          setLines((current) =>
                            current
                              .filter((_, position) => position !== index)
                              .map((item, position) => ({
                                ...item,
                                sequence: position + 1,
                              }))
                          )
                        }
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                      {line.formulaType === 'Area' ||
                      line.formulaType === 'Volume' ? (
                        <Input
                          type="number"
                          min="0.0001"
                          step="0.0001"
                          placeholder="Width"
                          value={line.width ?? ''}
                          onChange={(event) =>
                            setLines((current) =>
                              current.map((item, position) =>
                                position === index
                                  ? {
                                      ...item,
                                      width:
                                        Number(event.target.value) || undefined,
                                    }
                                  : item
                              )
                            )
                          }
                        />
                      ) : null}
                      {line.formulaType === 'Volume' ? (
                        <Input
                          type="number"
                          min="0.0001"
                          step="0.0001"
                          placeholder="Height"
                          value={line.height ?? ''}
                          onChange={(event) =>
                            setLines((current) =>
                              current.map((item, position) =>
                                position === index
                                  ? {
                                      ...item,
                                      height:
                                        Number(event.target.value) || undefined,
                                    }
                                  : item
                              )
                            )
                          }
                        />
                      ) : null}
                      <label className="flex items-center gap-2 text-sm">
                        <input
                          type="checkbox"
                          checked={line.isDeduction}
                          onChange={(event) =>
                            setLines((current) =>
                              current.map((item, position) =>
                                position === index
                                  ? {
                                      ...item,
                                      isDeduction: event.target.checked,
                                    }
                                  : item
                              )
                            )
                          }
                        />
                        Deduction
                      </label>
                      <Textarea
                        className="md:col-span-3"
                        placeholder="Notes (optional)"
                        value={line.notes ?? ''}
                        onChange={(event) =>
                          setLines((current) =>
                            current.map((item, position) =>
                              position === index
                                ? { ...item, notes: event.target.value }
                                : item
                            )
                          )
                        }
                      />
                    </div>
                  ))}
                  <div className="flex items-center justify-between">
                    <span className="font-medium">
                      Calculated net quantity: {total.toFixed(4)}
                    </span>
                    <Button onClick={() => void save()} disabled={loading}>
                      <Save className="mr-1 h-4 w-4" />
                      {editingId ? 'Update draft' : 'Create sheet'}
                    </Button>
                  </div>
                </div>
              </div>
            ) : null}
            {selected?.status === 'Draft' && canManage ? (
              <div className="space-y-3 rounded-lg border p-4">
                <h4 className="font-medium">Central-DMS evidence</h4>
                <div className="grid gap-3 md:grid-cols-3">
                  <Select
                    value={evidenceType}
                    onValueChange={(value) =>
                      setEvidenceType(value as MeasurementEvidenceType)
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {evidenceTypes.map((value) => (
                        <SelectItem key={value} value={value}>
                          {value}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Input
                    placeholder="Evidence title"
                    value={evidenceTitle}
                    onChange={(event) => setEvidenceTitle(event.target.value)}
                  />
                  <Input
                    type="file"
                    onChange={(event) => setFile(event.target.files?.[0])}
                  />
                </div>
                <Button
                  variant="outline"
                  onClick={() => void attach()}
                  disabled={loading}
                >
                  <FileUp className="mr-1 h-4 w-4" />
                  Scan & attach evidence
                </Button>
              </div>
            ) : null}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
