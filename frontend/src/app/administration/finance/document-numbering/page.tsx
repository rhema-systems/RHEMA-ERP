'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { Edit, RefreshCw, RotateCcw, Save, Search, Settings2 } from 'lucide-react';
import { toast } from 'sonner';
import { documentNumberingService } from '@/services/document-numbering.service';
import type {
  DocumentNumberingModule,
  DocumentSequenceDefinition,
  DocumentSequenceResetPolicy,
  UpdateDocumentSequenceDefinition,
} from '@/types/document-numbering';

const modules: DocumentNumberingModule[] = ['Finance', 'Sales'];
const resetPolicies: DocumentSequenceResetPolicy[] = ['Never', 'Yearly', 'Monthly'];

type SequenceFormState = {
  name: string;
  format: string;
  nextNumber: string;
  startNumber: string;
  minimumDigits: string;
  resetPolicy: string;
  isContinuous: boolean;
  allowManualEntry: boolean;
  isActive: boolean;
  isDefault: boolean;
  effectiveFrom: string;
  effectiveTo: string;
  description: string;
};

function toDateInputValue(value?: string | null) {
  return value ? value.slice(0, 10) : '';
}

function toFormState(sequence: DocumentSequenceDefinition): SequenceFormState {
  return {
    name: sequence.name ?? '',
    format: sequence.format ?? '',
    nextNumber: String(sequence.nextNumber ?? 1),
    startNumber: String(sequence.startNumber ?? 1),
    minimumDigits: String(sequence.minimumDigits ?? 1),
    resetPolicy: sequence.resetPolicy ?? 'Never',
    isContinuous: Boolean(sequence.isContinuous),
    allowManualEntry: Boolean(sequence.allowManualEntry),
    isActive: Boolean(sequence.isActive),
    isDefault: Boolean(sequence.isDefault),
    effectiveFrom: toDateInputValue(sequence.effectiveFrom),
    effectiveTo: toDateInputValue(sequence.effectiveTo),
    description: sequence.description ?? '',
  };
}

function padSequence(value: number, length: number) {
  return String(value).padStart(Math.max(length, 1), '0');
}

function formatSample(sequence: Pick<DocumentSequenceDefinition, 'format' | 'nextNumber' | 'minimumDigits'>) {
  const now = new Date();
  const yyyy = String(now.getFullYear());
  const yy = yyyy.slice(-2);
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  const sequenceNumber = Number(sequence.nextNumber || 1);

  return sequence.format
    .replace(/\{YYYY\}/gi, yyyy)
    .replace(/\{YY\}/gi, yy)
    .replace(/\{MM\}/gi, mm)
    .replace(/\{DD\}/gi, dd)
    .replace(/\{SEQ\}/gi, padSequence(sequenceNumber, sequence.minimumDigits || 1))
    .replace(/\{(#+)\}/g, (_match, hashes: string) => padSequence(sequenceNumber, hashes.length));
}

function moduleBadgeClass(module: string) {
  return module === 'Finance'
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : 'border-sky-200 bg-sky-50 text-sky-700';
}

export default function DocumentNumberingPage() {
  const [activeModule, setActiveModule] = useState<DocumentNumberingModule>('Finance');
  const [sequences, setSequences] = useState<DocumentSequenceDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [search, setSearch] = useState('');
  const [editingSequence, setEditingSequence] = useState<DocumentSequenceDefinition | null>(null);
  const [formData, setFormData] = useState<SequenceFormState | null>(null);

  const loadSequences = async (module = activeModule) => {
    try {
      setLoading(true);
      const data = await documentNumberingService.getDefinitions(module);
      setSequences(data);
    } catch (error) {
      console.error('Failed to load document sequences:', error);
      toast.error('Failed to load document numbering settings');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSequences(activeModule);
  }, [activeModule]);

  const filteredSequences = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return sequences;

    return sequences.filter((sequence) =>
      sequence.name.toLowerCase().includes(query) ||
      sequence.documentType.toLowerCase().includes(query) ||
      sequence.format.toLowerCase().includes(query)
    );
  }, [search, sequences]);

  const openEditDialog = (sequence: DocumentSequenceDefinition) => {
    setEditingSequence(sequence);
    setFormData(toFormState(sequence));
  };

  const closeEditDialog = () => {
    setEditingSequence(null);
    setFormData(null);
  };

  const ensureDefaults = async () => {
    try {
      setLoading(true);
      await documentNumberingService.ensureDefaults();
      await loadSequences(activeModule);
      toast.success('Default document sequences checked');
    } catch (error) {
      console.error('Failed to ensure default document sequences:', error);
      toast.error('Failed to check default sequences');
    } finally {
      setLoading(false);
    }
  };

  const updateForm = <K extends keyof SequenceFormState>(key: K, value: SequenceFormState[K]) => {
    setFormData((current) => current ? { ...current, [key]: value } : current);
  };

  const saveSequence = async () => {
    if (!editingSequence || !formData) return;

    const nextNumber = Number(formData.nextNumber);
    const startNumber = Number(formData.startNumber);
    const minimumDigits = Number(formData.minimumDigits);

    if (!formData.name.trim() || !formData.format.trim()) {
      toast.error('Name and format are required');
      return;
    }

    if (!Number.isFinite(nextNumber) || nextNumber < 1 || !Number.isInteger(nextNumber)) {
      toast.error('Next number must be a whole number greater than zero');
      return;
    }

    if (!Number.isFinite(startNumber) || startNumber < 1 || !Number.isInteger(startNumber)) {
      toast.error('Start number must be a whole number greater than zero');
      return;
    }

    if (!Number.isFinite(minimumDigits) || minimumDigits < 1 || minimumDigits > 12 || !Number.isInteger(minimumDigits)) {
      toast.error('Minimum digits must be between 1 and 12');
      return;
    }

    const payload: UpdateDocumentSequenceDefinition = {
      name: formData.name.trim(),
      format: formData.format.trim(),
      nextNumber,
      startNumber,
      minimumDigits,
      resetPolicy: formData.resetPolicy,
      isContinuous: formData.isContinuous,
      allowManualEntry: formData.allowManualEntry,
      isActive: formData.isActive,
      isDefault: formData.isDefault,
      effectiveFrom: formData.effectiveFrom || null,
      effectiveTo: formData.effectiveTo || null,
      description: formData.description.trim() || null,
    };

    try {
      setSaving(true);
      const updated = await documentNumberingService.updateDefinition(editingSequence.id, payload);
      setSequences((current) => current.map((sequence) => sequence.id === updated.id ? updated : sequence));
      toast.success('Document sequence updated');
      closeEditDialog();
    } catch (error) {
      console.error('Failed to update document sequence:', error);
      toast.error('Failed to update document sequence');
    } finally {
      setSaving(false);
    }
  };

  const currentSample = formData
    ? formatSample({
        format: formData.format,
        nextNumber: Number(formData.nextNumber) || 1,
        minimumDigits: Number(formData.minimumDigits) || 1,
      })
    : '';

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink href="/administration">Administration</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbLink href="/administration/finance">Finance</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage>Document Numbering</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>

        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Document Numbering</h1>
            <p className="text-muted-foreground">Sequence configuration for Finance and Sales documents</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" onClick={ensureDefaults} disabled={loading}>
              <RotateCcw className="mr-2 h-4 w-4" />
              Check Defaults
            </Button>
            <Button variant="outline" onClick={() => loadSequences(activeModule)} disabled={loading}>
              <RefreshCw className="mr-2 h-4 w-4" />
              Refresh
            </Button>
          </div>
        </div>
      </div>

      <Card>
        <CardHeader className="gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Settings2 className="h-5 w-5" />
              Sequence Definitions
            </CardTitle>
            <CardDescription>{filteredSequences.length} configured sequences</CardDescription>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <Tabs value={activeModule} onValueChange={(value) => setActiveModule(value as DocumentNumberingModule)}>
              <TabsList>
                {modules.map((module) => (
                  <TabsTrigger key={module} value={module}>{module}</TabsTrigger>
                ))}
              </TabsList>
            </Tabs>
            <div className="relative min-w-64">
              <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                className="pl-9"
                placeholder="Search sequences"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Document</TableHead>
                <TableHead>Format</TableHead>
                <TableHead>Next</TableHead>
                <TableHead>Reset</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading ? (
                <TableRow>
                  <TableCell colSpan={6} className="h-24 text-center text-muted-foreground">
                    Loading sequences...
                  </TableCell>
                </TableRow>
              ) : filteredSequences.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="h-24 text-center text-muted-foreground">
                    No matching sequences
                  </TableCell>
                </TableRow>
              ) : filteredSequences.map((sequence) => (
                <TableRow key={sequence.id}>
                  <TableCell>
                    <div className="space-y-1">
                      <div className="font-medium">{sequence.name}</div>
                      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                        <Badge variant="outline" className={moduleBadgeClass(sequence.module)}>{sequence.module}</Badge>
                        <span>{sequence.documentType}</span>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="space-y-1">
                      <code className="rounded bg-muted px-2 py-1 text-xs">{sequence.format}</code>
                      <div className="text-xs text-muted-foreground">{formatSample(sequence)}</div>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="font-medium tabular-nums">{sequence.nextNumber}</div>
                    <div className="text-xs text-muted-foreground">Start {sequence.startNumber}</div>
                  </TableCell>
                  <TableCell>
                    <div>{sequence.resetPolicy}</div>
                    <div className="text-xs text-muted-foreground">{sequence.lastResetPeriodKey || 'No period'}</div>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={sequence.isActive ? 'default' : 'secondary'}>
                        {sequence.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      {sequence.allowManualEntry && <Badge variant="outline">Manual</Badge>}
                      {sequence.isContinuous && <Badge variant="outline">Continuous</Badge>}
                    </div>
                  </TableCell>
                  <TableCell className="text-right">
                    <Button variant="ghost" size="sm" onClick={() => openEditDialog(sequence)}>
                      <Edit className="mr-2 h-4 w-4" />
                      Edit
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={Boolean(editingSequence && formData)} onOpenChange={(open) => !open && closeEditDialog()}>
        <DialogContent className="grid max-h-[90dvh] w-[calc(100vw-2rem)] max-w-3xl grid-rows-[auto_minmax(0,1fr)_auto] gap-0 overflow-hidden p-0">
          <DialogHeader className="border-b px-6 py-4 pr-12">
            <DialogTitle>Edit Document Sequence</DialogTitle>
            <DialogDescription>{editingSequence?.module} / {editingSequence?.documentType}</DialogDescription>
          </DialogHeader>

          {formData && (
            <div className="grid min-h-0 gap-5 overflow-y-auto px-6 py-4">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="sequence-name">Name</Label>
                  <Input id="sequence-name" value={formData.name} onChange={(event) => updateForm('name', event.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="sequence-reset">Reset Policy</Label>
                  <Select value={formData.resetPolicy} onValueChange={(value) => updateForm('resetPolicy', value)}>
                    <SelectTrigger id="sequence-reset">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {resetPolicies.map((policy) => (
                        <SelectItem key={policy} value={policy}>{policy}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="sequence-format">Format</Label>
                <Input id="sequence-format" value={formData.format} onChange={(event) => updateForm('format', event.target.value.toUpperCase())} />
                <div className="rounded-md bg-muted px-3 py-2 text-sm">
                  <span className="text-muted-foreground">Sample: </span>
                  <code>{currentSample}</code>
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-3">
                <div className="space-y-2">
                  <Label htmlFor="sequence-next">Next Number</Label>
                  <Input id="sequence-next" type="number" min={1} value={formData.nextNumber} onChange={(event) => updateForm('nextNumber', event.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="sequence-start">Start Number</Label>
                  <Input id="sequence-start" type="number" min={1} value={formData.startNumber} onChange={(event) => updateForm('startNumber', event.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="sequence-digits">Minimum Digits</Label>
                  <Input id="sequence-digits" type="number" min={1} max={12} value={formData.minimumDigits} onChange={(event) => updateForm('minimumDigits', event.target.value)} />
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="sequence-from">Effective From</Label>
                  <Input id="sequence-from" type="date" value={formData.effectiveFrom} onChange={(event) => updateForm('effectiveFrom', event.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="sequence-to">Effective To</Label>
                  <Input id="sequence-to" type="date" value={formData.effectiveTo} onChange={(event) => updateForm('effectiveTo', event.target.value)} />
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="flex items-center justify-between rounded-md border p-3">
                  <Label htmlFor="sequence-active">Active</Label>
                  <Switch id="sequence-active" checked={formData.isActive} onCheckedChange={(checked) => updateForm('isActive', checked)} />
                </div>
                <div className="flex items-center justify-between rounded-md border p-3">
                  <Label htmlFor="sequence-default">Default</Label>
                  <Switch id="sequence-default" checked={formData.isDefault} onCheckedChange={(checked) => updateForm('isDefault', checked)} />
                </div>
                <div className="flex items-center justify-between rounded-md border p-3">
                  <Label htmlFor="sequence-manual">Manual Entry</Label>
                  <Switch id="sequence-manual" checked={formData.allowManualEntry} onCheckedChange={(checked) => updateForm('allowManualEntry', checked)} />
                </div>
                <div className="flex items-center justify-between rounded-md border p-3">
                  <Label htmlFor="sequence-continuous">Continuous</Label>
                  <Switch id="sequence-continuous" checked={formData.isContinuous} onCheckedChange={(checked) => updateForm('isContinuous', checked)} />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="sequence-description">Description</Label>
                <Textarea id="sequence-description" value={formData.description} onChange={(event) => updateForm('description', event.target.value)} rows={3} />
              </div>
            </div>
          )}

          <DialogFooter className="border-t bg-background px-6 py-4">
            <Button variant="outline" onClick={closeEditDialog} disabled={saving}>Cancel</Button>
            <Button onClick={saveSequence} disabled={saving}>
              <Save className="mr-2 h-4 w-4" />
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
