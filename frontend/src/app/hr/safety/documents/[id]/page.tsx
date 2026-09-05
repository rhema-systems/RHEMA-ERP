'use client';

import { useRef, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  Upload,
  Download,
  CheckCircle2,
  SearchCheck,
  Archive,
  Trash2,
  Pencil,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { safetyDocumentsService } from '@/services/hr/safety-documents.service';
import {
  SHE_DOCUMENT_CATEGORY_OPTIONS,
  type SheControlledDocument,
  type SheControlledDocumentCategory,
  type SheControlledDocumentStatus,
} from '@/types/hr/safety-documents';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtSize = (bytes?: number | null) => {
  if (bytes == null) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

function StatusBadge({ status }: { status: SheControlledDocumentStatus }) {
  switch (status) {
    case 'Active':
      return <Badge>Active</Badge>;
    case 'UnderReview':
      return <Badge variant="destructive">Under review</Badge>;
    case 'Archived':
      return <Badge variant="secondary">Archived</Badge>;
    default:
      return <Badge variant="outline">Draft</Badge>;
  }
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-2 border-b py-1 text-sm">
      <dt className="text-muted-foreground shrink-0">{label}</dt>
      <dd className="text-right font-medium">{value}</dd>
    </div>
  );
}

/**
 * One controlled SHE document: metadata, the FR-SHE-246 version history
 * (revisions through the scanned upload gate onto the central DMS), and the
 * approval lifecycle — activate (approve), send for review, re-approve,
 * archive. Archived documents refuse edits and new versions; a document with
 * history refuses deletion.
 */
export default function SheDocumentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [dialog, setDialog] = useState<'upload' | 'activate' | 'archive' | 'edit' | null>(null);
  const [busy, setBusy] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [text, setText] = useState('');
  const [effectiveDate, setEffectiveDate] = useState('');
  const [nextReviewDate, setNextReviewDate] = useState('');

  // Edit-dialog state (metadata only — versions and lifecycle have their own paths).
  const [editTitle, setEditTitle] = useState('');
  const [editCategory, setEditCategory] = useState<SheControlledDocumentCategory>('Policy');
  const [editDescription, setEditDescription] = useState('');
  const [editKeywords, setEditKeywords] = useState('');
  const [editOwnerId, setEditOwnerId] = useState<string | null>(null);
  const [editFrequency, setEditFrequency] = useState('');
  const [editNotes, setEditNotes] = useState('');

  const { data: doc, isLoading } = useQuery({
    queryKey: ['hr', 'safety-documents', 'detail', id],
    queryFn: () => safetyDocumentsService.getById(id),
    enabled: !!id,
  });

  const act = async (title: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-documents'] });
      toast({ title });
      setDialog(null);
      setFile(null);
      setText('');
    } catch (error: any) {
      toast({
        title: 'Refused',
        description: error?.message || 'The action failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }
  if (!doc) {
    return (
      <div className="p-6">
        <EmptyState title="Document not found" description="Check the register." />
      </div>
    );
  }

  const d: SheControlledDocument = doc;
  const editable = d.status !== 'Archived';

  const openEdit = () => {
    setEditTitle(d.title);
    setEditCategory(d.category);
    setEditDescription(d.description ?? '');
    setEditKeywords(d.keywords ?? '');
    setEditOwnerId(d.ownerId);
    setEditFrequency(d.reviewFrequencyMonths != null ? String(d.reviewFrequencyMonths) : '');
    setEditNotes(d.notes ?? '');
    setDialog('edit');
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${d.documentNumber} — ${d.title}`}
        description={`${d.categoryName} · owned by ${d.ownerName}${d.locationName ? ` · ${d.locationName}` : ''}`}
        backHref="/hr/safety/documents"
        actions={
          <div className="flex flex-wrap gap-2">
            {editable && (
              <>
                <Button variant="outline" onClick={openEdit} disabled={busy}>
                  <Pencil className="mr-2 h-4 w-4" />
                  Edit
                </Button>
                <Button onClick={() => { setFile(null); setText(''); setDialog('upload'); }} disabled={busy}>
                  <Upload className="mr-2 h-4 w-4" />
                  Upload version
                </Button>
              </>
            )}
            {(d.status === 'Draft' || d.status === 'UnderReview') && (
              <Button
                variant="default"
                onClick={() => {
                  setEffectiveDate(new Date().toISOString().slice(0, 10));
                  setNextReviewDate('');
                  setDialog('activate');
                }}
                disabled={busy}
              >
                <CheckCircle2 className="mr-2 h-4 w-4" />
                {d.status === 'UnderReview' ? 'Re-approve' : 'Approve & activate'}
              </Button>
            )}
            {d.status === 'Active' && (
              <Button
                variant="outline"
                onClick={() => void act('Sent for review', () => safetyDocumentsService.startReview(d.id))}
                disabled={busy}
              >
                <SearchCheck className="mr-2 h-4 w-4" />
                Send for review
              </Button>
            )}
            {editable && (
              <Button variant="outline" onClick={() => { setText(''); setDialog('archive'); }} disabled={busy}>
                <Archive className="mr-2 h-4 w-4" />
                Archive
              </Button>
            )}
            {d.status === 'Draft' && !d.documentRecordId && (
              <Button
                variant="destructive"
                onClick={() =>
                  void act('Draft deleted', async () => {
                    await safetyDocumentsService.remove(d.id);
                    router.push('/hr/safety/documents');
                  })
                }
                disabled={busy}
              >
                <Trash2 className="mr-2 h-4 w-4" />
                Delete draft
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={d.status} />
        {d.currentVersionLabel && (
          <Badge variant="outline" className="font-mono">
            {d.currentVersionLabel}
          </Badge>
        )}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Details</CardTitle>
          </CardHeader>
          <CardContent>
            <dl>
              <Row label="Category" value={d.categoryName} />
              <Row label="Owner" value={d.ownerName} />
              <Row label="Organisation unit" value={d.organizationUnitName ?? '—'} />
              <Row label="Location" value={d.locationName ?? '—'} />
              <Row label="Keywords" value={d.keywords ?? '—'} />
              <Row
                label="Review frequency"
                value={d.reviewFrequencyMonths != null ? `${d.reviewFrequencyMonths} months` : '—'}
              />
            </dl>
            {d.description && <p className="text-muted-foreground mt-3 text-sm">{d.description}</p>}
            {d.notes && <p className="text-muted-foreground mt-1 text-sm">Notes: {d.notes}</p>}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Approval & review</CardTitle>
          </CardHeader>
          <CardContent>
            <dl>
              <Row
                label="Approved"
                value={d.approvedByName ? `${d.approvedByName}, ${fmtDate(d.approvedDate)}` : '—'}
              />
              <Row label="Effective date" value={fmtDate(d.effectiveDate)} />
              <Row label="Next review" value={fmtDate(d.nextReviewDate)} />
              {d.status === 'Archived' && (
                <Row
                  label="Archived"
                  value={`${d.archivedByName ?? '—'}, ${fmtDate(d.archivedDate)}`}
                />
              )}
            </dl>
            {d.archiveReason && (
              <p className="text-muted-foreground mt-3 text-sm">Archive reason: {d.archiveReason}</p>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Version history{d.versions.length > 0 ? ` (${d.versions.length})` : ''}
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {d.versions.length === 0 ? (
            <EmptyState
              title="No versions yet"
              description="Upload the first version — the document cannot be approved without one."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Version</TableHead>
                  <TableHead>File</TableHead>
                  <TableHead>Size</TableHead>
                  <TableHead>Change summary</TableHead>
                  <TableHead>Uploaded</TableHead>
                  <TableHead>By</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {d.versions.map((v) => (
                  <TableRow key={v.id}>
                    <TableCell className="font-mono font-medium">
                      {v.versionNumber}
                      {v.versionNumber === d.currentVersionLabel && (
                        <Badge variant="outline" className="ml-2">
                          current
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="max-w-xs">
                      <span className="line-clamp-1">{v.fileName ?? '—'}</span>
                    </TableCell>
                    <TableCell className="tabular-nums">{fmtSize(v.fileSize)}</TableCell>
                    <TableCell className="max-w-sm">
                      <span className="line-clamp-1">{v.changeSummary ?? '—'}</span>
                    </TableCell>
                    <TableCell className="tabular-nums">{fmtDateTime(v.uploadedAt)}</TableCell>
                    <TableCell>{v.uploadedByName}</TableCell>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() =>
                          void safetyDocumentsService
                            .downloadVersion(d.id, v.id, v.fileName ?? d.documentNumber)
                            .catch((error: any) =>
                              toast({
                                title: 'Download failed',
                                description: error?.message ?? 'The file could not be retrieved.',
                                variant: 'destructive',
                              }),
                            )
                        }
                      >
                        <Download className="mr-1 h-4 w-4" />
                        Get
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* ── upload version ── */}
      <Dialog open={dialog === 'upload'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Upload version</DialogTitle>
            <DialogDescription>
              The file is scanned and stored in the central repository; the version number is
              assigned automatically ({d.currentVersionLabel ? 'next after ' + d.currentVersionLabel : 'v1.0'}).
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>File</Label>
              <Input
                ref={fileInputRef}
                type="file"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
            </div>
            <div className="space-y-2">
              <Label>Change summary</Label>
              <Textarea
                rows={2}
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder="What changed in this revision"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy || !file}
              onClick={() =>
                void act('Version uploaded', () =>
                  safetyDocumentsService.uploadVersion(d.id, file!, text.trim() || undefined),
                )
              }
            >
              Upload
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── activate (approve) ── */}
      <Dialog open={dialog === 'activate'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{d.status === 'UnderReview' ? 'Re-approve document' : 'Approve & activate'}</DialogTitle>
            <DialogDescription>
              Records you as the approver and makes this the controlled, effective document.
              {d.reviewFrequencyMonths != null &&
                ` Next review defaults to ${d.reviewFrequencyMonths} months after the effective date.`}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Effective date</Label>
              <Input type="date" value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Next review date (optional override)</Label>
              <Input type="date" value={nextReviewDate} onChange={(e) => setNextReviewDate(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={() =>
                void act('Document approved', () =>
                  safetyDocumentsService.activate(
                    d.id,
                    effectiveDate ? new Date(effectiveDate).toISOString() : null,
                    nextReviewDate ? new Date(nextReviewDate).toISOString() : null,
                  ),
                )
              }
            >
              Approve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── archive ── */}
      <Dialog open={dialog === 'archive'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Archive document</DialogTitle>
            <DialogDescription>
              The document and its full version history stay on the register, read-only. Archiving
              is how an obsolete controlled document leaves service — deletion is refused once
              history exists.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Reason</Label>
            <Textarea rows={2} value={text} onChange={(e) => setText(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={() =>
                void act('Document archived', () =>
                  safetyDocumentsService.archive(d.id, text.trim() || null),
                )
              }
            >
              Archive
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── edit metadata ── */}
      <Dialog open={dialog === 'edit'} onOpenChange={(v) => !v && setDialog(null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit document details</DialogTitle>
            <DialogDescription>
              Metadata only — versions are appended via upload and never edited.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Title</Label>
              <Input value={editTitle} onChange={(e) => setEditTitle(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Category</Label>
              <Select
                value={editCategory}
                onValueChange={(v) => setEditCategory(v as SheControlledDocumentCategory)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SHE_DOCUMENT_CATEGORY_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea rows={2} value={editDescription} onChange={(e) => setEditDescription(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Keywords</Label>
              <Input value={editKeywords} onChange={(e) => setEditKeywords(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Owner</Label>
              <EmployeePicker value={editOwnerId} onChange={setEditOwnerId} />
            </div>
            <div className="space-y-2">
              <Label>Review frequency (months)</Label>
              <Input value={editFrequency} onChange={(e) => setEditFrequency(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={editNotes} onChange={(e) => setEditNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy || editTitle.trim().length === 0 || !editOwnerId}
              onClick={() => {
                const frequency = editFrequency.trim() ? Number(editFrequency) : null;
                void act('Document updated', () =>
                  safetyDocumentsService.update(d.id, {
                    id: d.id,
                    title: editTitle.trim(),
                    category: editCategory,
                    description: editDescription.trim() || null,
                    keywords: editKeywords.trim() || null,
                    ownerId: editOwnerId!,
                    organizationUnitId: d.organizationUnitId ?? null,
                    locationId: d.locationId ?? null,
                    reviewFrequencyMonths:
                      frequency != null && Number.isFinite(frequency) && frequency > 0
                        ? frequency
                        : null,
                    notes: editNotes.trim() || null,
                  }),
                );
              }}
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
