'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Download,
  ExternalLink,
  FileArchive,
  Loader2,
  RefreshCw,
  ShieldCheck,
  Upload,
} from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import {
  procurementDocumentManagementService,
  type ProcurementDocumentFamily,
  type ProcurementDocumentFamilyCode,
  type ProcurementDocumentSourceOption,
  type ProcurementManagedDocument,
} from '@/services/procurement-document-management.service';

function message(error: unknown): string {
  return error instanceof Error
    ? error.message
    : 'The procurement document request failed.';
}

function date(value: string): string {
  return new Date(value).toLocaleString();
}

function StatusBadge({
  ok,
  children,
}: {
  ok: boolean;
  children: React.ReactNode;
}) {
  return (
    <Badge
      variant={ok ? 'default' : 'destructive'}
      className="whitespace-nowrap"
    >
      {children}
    </Badge>
  );
}

export function ProcurementDocumentManagementWorkspace() {
  const [families, setFamilies] = useState<ProcurementDocumentFamily[]>([]);
  const [familyCode, setFamilyCode] = useState<
    ProcurementDocumentFamilyCode | ''
  >('');
  const [sources, setSources] = useState<ProcurementDocumentSourceOption[]>([]);
  const [sourceId, setSourceId] = useState('');
  const [classification, setClassification] = useState('');
  const [title, setTitle] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [records, setRecords] = useState<ProcurementManagedDocument[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const family = useMemo(
    () => families.find((item) => item.code === familyCode),
    [families, familyCode]
  );

  const loadCatalogue = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const value = await procurementDocumentManagementService.catalogue();
      setFamilies(value);
      setFamilyCode((current) => current || value[0]?.code || '');
    } catch (loadError) {
      setError(message(loadError));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadFamily = useCallback(
    async (
      selectedFamily: ProcurementDocumentFamilyCode,
      selectedSource?: string
    ) => {
      setLoading(true);
      setError(null);
      try {
        const [sourceOptions, documentRecords] = await Promise.all([
          procurementDocumentManagementService.sources(selectedFamily),
          procurementDocumentManagementService.records(
            selectedFamily,
            selectedSource
          ),
        ]);
        setSources(sourceOptions);
        setRecords(documentRecords);
      } catch (loadError) {
        setError(message(loadError));
      } finally {
        setLoading(false);
      }
    },
    []
  );

  useEffect(() => {
    void loadCatalogue();
  }, [loadCatalogue]);

  useEffect(() => {
    if (!familyCode) return;
    setSourceId('');
    setClassification('');
    setFile(null);
    setTitle('');
    void loadFamily(familyCode);
  }, [familyCode, loadFamily]);

  const selectSource = (value: string) => {
    setSourceId(value === '__all__' ? '' : value);
    if (familyCode)
      void loadFamily(familyCode, value === '__all__' ? undefined : value);
  };

  const upload = async () => {
    if (!familyCode || !sourceId || !classification || !file) return;
    setBusy(true);
    setError(null);
    try {
      await procurementDocumentManagementService.upload(
        familyCode,
        sourceId,
        classification,
        file,
        title
      );
      toast.success('Document scanned and registered in the central DMS.');
      setFile(null);
      setTitle('');
      if (fileRef.current) fileRef.current.value = '';
      await loadFamily(familyCode, sourceId);
    } catch (uploadError) {
      const detail = message(uploadError);
      setError(detail);
      toast.error(detail);
    } finally {
      setBusy(false);
    }
  };

  const download = async (record: ProcurementManagedDocument) => {
    setBusy(true);
    try {
      const blob = await procurementDocumentManagementService.download(
        record.documentRecordId,
        record.documentVersionId
      );
      const url = URL.createObjectURL(blob);
      const anchor = window.document.createElement('a');
      anchor.href = url;
      anchor.download = record.title;
      window.document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      toast.error(message(downloadError));
    } finally {
      setBusy(false);
    }
  };

  if (loading && families.length === 0) {
    return (
      <div className="flex min-h-[240px] items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold">
            <FileArchive className="h-6 w-6" /> Procurement documents
          </h1>
          <p className="text-sm text-muted-foreground">
            Governed procurement evidence stored, scanned and retained by the
            central DMS.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() =>
            familyCode && loadFamily(familyCode, sourceId || undefined)
          }
          disabled={loading || busy || !familyCode}
        >
          <RefreshCw className="mr-2 h-4 w-4" /> Refresh
        </Button>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">
            Register controlled evidence
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3 lg:grid-cols-6">
          <div className="space-y-1.5 lg:col-span-2">
            <Label>Document family</Label>
            <Select
              value={familyCode}
              onValueChange={(value) =>
                setFamilyCode(value as ProcurementDocumentFamilyCode)
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="Select family" />
              </SelectTrigger>
              <SelectContent>
                {families.map((item) => (
                  <SelectItem key={item.code} value={item.code}>
                    {item.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5 lg:col-span-2">
            <Label>Source record</Label>
            <Select value={sourceId || '__all__'} onValueChange={selectSource}>
              <SelectTrigger>
                <SelectValue placeholder="Select governed source" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all__">All source records</SelectItem>
                {sources.map((item) => (
                  <SelectItem key={item.id} value={item.id}>
                    {item.label} · {item.status}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5 lg:col-span-2">
            <Label>Classification</Label>
            <Select
              value={classification}
              onValueChange={setClassification}
              disabled={!family}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select classification" />
              </SelectTrigger>
              <SelectContent>
                {family?.classifications.map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5 lg:col-span-2">
            <Label>Document file</Label>
            <Input
              ref={fileRef}
              type="file"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
              disabled={!family?.canUpload}
            />
          </div>
          <div className="space-y-1.5 lg:col-span-3">
            <Label>Display title (optional)</Label>
            <Input
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              maxLength={250}
              placeholder={file?.name || 'Defaults to the file name'}
              disabled={!family?.canUpload}
            />
          </div>
          <div className="flex items-end lg:col-span-1">
            <Button
              className="w-full"
              onClick={upload}
              disabled={
                busy ||
                !family?.canUpload ||
                !sourceId ||
                !classification ||
                !file
              }
            >
              {busy ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Upload className="mr-2 h-4 w-4" />
              )}
              Scan & register
            </Button>
          </div>
          {family && !family.canUpload && (
            <p className="text-sm text-amber-700 lg:col-span-6">
              You have read access to this family but not upload authority.
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0 pb-3">
          <CardTitle className="text-base">Document register</CardTitle>
          <span className="text-sm text-muted-foreground">
            {records.length} record(s)
          </span>
        </CardHeader>
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Document</TableHead>
                  <TableHead>Source</TableHead>
                  <TableHead>Governance</TableHead>
                  <TableHead>Lifecycle</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {records.map((record) => (
                  <TableRow key={record.documentRecordId}>
                    <TableCell className="min-w-[230px]">
                      <div className="font-medium">{record.title}</div>
                      <div className="text-xs text-muted-foreground">
                        {record.documentReference} · {record.classification} ·{' '}
                        {record.versionNumber}
                      </div>
                    </TableCell>
                    <TableCell className="min-w-[150px]">
                      {record.sourceReference || '—'}
                    </TableCell>
                    <TableCell className="min-w-[270px]">
                      <div className="flex flex-wrap gap-1">
                        <StatusBadge ok={record.malwareStatus === 'Clean'}>
                          {record.malwareStatus}
                        </StatusBadge>
                        <StatusBadge ok={record.metadataComplete}>
                          Metadata
                        </StatusBadge>
                        <StatusBadge ok={record.accessCovered}>
                          Access
                        </StatusBadge>
                        <StatusBadge ok={record.retentionCovered}>
                          Retention
                        </StatusBadge>
                        {record.legalHold && (
                          <Badge variant="secondary">Legal hold</Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{record.versionStatus}</div>
                      <div className="text-xs text-muted-foreground">
                        {record.lifecycleStatus} · {record.retentionStatus}
                      </div>
                    </TableCell>
                    <TableCell className="min-w-[160px]">
                      <div>{date(record.createdAtUtc)}</div>
                      <div className="text-xs text-muted-foreground">
                        {record.createdBy}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => download(record)}
                          disabled={busy}
                          title="Download through central DMS"
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          asChild
                          title="Open central DMS record"
                        >
                          <Link
                            href={`/document-management/records/${record.documentRecordId}`}
                          >
                            <ExternalLink className="h-4 w-4" />
                          </Link>
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {!loading && records.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="h-24 text-center text-muted-foreground"
                    >
                      No governed documents match the selected family and
                      source.
                    </TableCell>
                  </TableRow>
                )}
                {loading && (
                  <TableRow>
                    <TableCell colSpan={6} className="h-24 text-center">
                      <Loader2 className="mx-auto h-5 w-5 animate-spin" />
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <div className="flex items-center gap-2 text-xs text-muted-foreground">
        <ShieldCheck className="h-4 w-4" /> Source records, classifications,
        access, metadata and retention are resolved from controlled tenant
        catalogues.
      </div>
    </div>
  );
}
