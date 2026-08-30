'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ClipboardCheck, RefreshCw, Send, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringDevelopmentApprovalFileService } from '@/services/civil-engineering-development-approval-file.service';
import { civilEngineeringPermittingEngineeringReviewService } from '@/services/civil-engineering-permitting-engineering-review.service';
import type { CivilEngineeringDevelopmentApprovalFile } from '@/types/civil-engineering-development-approval-file';
import type { CivilEngineeringPermittingEngineeringReview, CivilEngineeringPermittingEngineeringReviewLookups, CivilEngineeringPermittingOutcome } from '@/types/civil-engineering-permitting-engineering-review';

const managePermission = 'civil-engineering.permitting.manage';
const readPermission = 'civil-engineering.workspace.read';
const none = '__select__';
const outcomeLabels: Record<CivilEngineeringPermittingOutcome, string> = {
  RecommendApproval: 'Recommend approval',
  RecommendApprovalWithConditions: 'Recommend approval with conditions',
  ReturnForCorrection: 'Return for correction',
  RecommendRejection: 'Recommend rejection',
};
const stageLabels: Record<string, string> = {
  CorrectionRequested: 'Correction requested',
  PendingHodDecision: 'Pending HOD decision',
  HodApproved: 'HOD approved',
  HodRejected: 'HOD rejected',
  HodReturned: 'HOD returned',
};
const formatDate = (value: string) => new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  const detail = value.response?.detail || value.message || fallback;
  return `${detail}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export default function CivilEngineeringPermittingEngineeringReviewsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission(managePermission);
  const canRead = hasPermission(readPermission) || canManage;
  const [files, setFiles] = useState<CivilEngineeringDevelopmentApprovalFile[]>([]);
  const [fileId, setFileId] = useState('');
  const [lookups, setLookups] = useState<CivilEngineeringPermittingEngineeringReviewLookups>();
  const [reviews, setReviews] = useState<CivilEngineeringPermittingEngineeringReview[]>([]);
  const [commentCategoryId, setCommentCategoryId] = useState(none);
  const [recommendedOutcome, setRecommendedOutcome] = useState<CivilEngineeringPermittingOutcome | ''>('');
  const [documentVersionId, setDocumentVersionId] = useState(none);
  const [reviewComment, setReviewComment] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const selectedFile = useMemo(() => files.find((item) => item.id === fileId), [files, fileId]);
  const selectedDocument = useMemo(() => lookups?.documents.find((item) => item.centralDocumentVersionId === documentVersionId), [documentVersionId, lookups]);

  const load = useCallback(async (selectedId?: string) => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    try {
      const source = await civilEngineeringDevelopmentApprovalFileService.list();
      setFiles(source);
      const id = selectedId || fileId || source[0]?.id || '';
      setFileId(id);
      if (!id) { setLookups(undefined); setReviews([]); return; }
      const history = await civilEngineeringPermittingEngineeringReviewService.list(id);
      setReviews(history);
      setLookups(canManage ? await civilEngineeringPermittingEngineeringReviewService.lookups(id) : undefined);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Engineering review could not be loaded', description: errorText(error, 'Refresh and try again.') });
    } finally { setLoading(false); }
  }, [canManage, canRead, fileId, toast]);

  useEffect(() => { void load(); }, [load]);

  const changeFile = (id: string) => {
    setCommentCategoryId(none); setRecommendedOutcome(''); setDocumentVersionId(none); setReviewComment('');
    void load(id === none ? '' : id);
  };

  const submit = async () => {
    if (!fileId || commentCategoryId === none || !recommendedOutcome || !selectedDocument || reviewComment.trim().length < 5) {
      toast({ variant: 'destructive', title: 'Complete the engineering review', description: 'Select a configured category, recommendation and current Published DMS evidence, then enter the engineering comment.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringPermittingEngineeringReviewService.submit(fileId, {
        clientRequestId: crypto.randomUUID(), commentCategoryId, reviewComment: reviewComment.trim(), recommendedOutcome,
        centralDocumentRecordId: selectedDocument.centralDocumentRecordId, centralDocumentVersionId: selectedDocument.centralDocumentVersionId,
      });
      setCommentCategoryId(none); setRecommendedOutcome(''); setDocumentVersionId(none); setReviewComment('');
      toast({ title: 'Engineering review submitted', description: recommendedOutcome === 'ReturnForCorrection' ? 'The correction request is recorded. Route the file back through Development-file handoffs.' : 'The governed HOD decision workflow is now pending.' });
      await load(fileId);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Engineering review was not submitted', description: errorText(error, 'Confirm that this file is currently assigned to you as the SCE and that the selected evidence is current Published.') });
    } finally { setSaving(false); }
  };

  if (!canRead) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil permitting access required</AlertTitle><AlertDescription>You do not have access to engineering reviews.</AlertDescription></Alert>;

  return <div className="space-y-5">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
      <div><h1 className="flex items-center gap-2 text-2xl font-semibold"><ClipboardCheck className="h-6 w-6" />SCE engineering reviews</h1><p className="mt-1 text-sm text-muted-foreground">Structured drawing review and recommendation for files currently handed to Civil Engineering.</p></div>
      <Button variant="outline" size="sm" onClick={() => void load(fileId)} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
    </div>
    <Card><CardHeader><CardTitle className="text-base">Development approval file</CardTitle></CardHeader><CardContent>
      <Select value={fileId || none} onValueChange={changeFile}><SelectTrigger className="max-w-2xl"><SelectValue placeholder="Select file" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select file</SelectItem>{files.map((item) => <SelectItem key={item.id} value={item.id}>{item.fileNumber} · {item.applicationReference} · {item.applicantName}</SelectItem>)}</SelectContent></Select>
      {selectedFile ? <p className="mt-2 text-sm text-muted-foreground">{selectedFile.projectLabel} · {selectedFile.propertyLabel} · {selectedFile.status}</p> : null}
    </CardContent></Card>
    {canManage && selectedFile && lookups ? <Card><CardHeader><CardTitle className="text-base">Submit SCE recommendation</CardTitle><CardDescription>Categories, outcomes and evidence are constrained by the frozen policy on this file. HOD makes the final decision in the following stage.</CardDescription></CardHeader><CardContent className="space-y-4">
      <div className="grid gap-3 md:grid-cols-3"><div className="space-y-1"><Label>Engineering comment category</Label><Select value={commentCategoryId} onValueChange={setCommentCategoryId}><SelectTrigger><SelectValue placeholder="Select category" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select category</SelectItem>{lookups.commentCategories.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Recommendation</Label><Select value={recommendedOutcome || none} onValueChange={(value) => setRecommendedOutcome(value === none ? '' : value as CivilEngineeringPermittingOutcome)}><SelectTrigger><SelectValue placeholder="Select outcome" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select outcome</SelectItem>{lookups.allowedOutcomes.map((item) => <SelectItem key={item} value={item}>{outcomeLabels[item]}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Current Published DMS evidence</Label><Select value={documentVersionId} onValueChange={setDocumentVersionId}><SelectTrigger><SelectValue placeholder="Select evidence" /></SelectTrigger><SelectContent><SelectItem value={none} disabled>Select evidence</SelectItem>{lookups.documents.map((item) => <SelectItem key={item.centralDocumentVersionId} value={item.centralDocumentVersionId}>{item.documentReference} · v{item.versionNumber} · {item.title}</SelectItem>)}</SelectContent></Select></div></div>
      <div className="space-y-1"><Label>Engineering review comment</Label><Textarea value={reviewComment} maxLength={4000} rows={5} placeholder="Describe the drawing review, required corrections or recommendation basis." onChange={(event) => setReviewComment(event.target.value)} /><p className="text-right text-xs text-muted-foreground">{reviewComment.length}/4000</p></div>
      <div className="flex justify-end"><Button disabled={saving || selectedFile.status !== 'SiteInspectionCompleted'} onClick={() => void submit()}><Send className="mr-2 h-4 w-4" />Submit recommendation</Button></div>
    </CardContent></Card> : null}
    <Card><CardHeader><CardTitle className="text-base">Engineering review history</CardTitle></CardHeader><CardContent className="space-y-3">{loading ? <p className="text-sm text-muted-foreground">Loading engineering reviews…</p> : null}{!loading && fileId && !reviews.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No SCE engineering review has been recorded for this file.</p> : null}{reviews.map((item) => <article key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-2 md:flex-row md:justify-between"><div><div className="flex flex-wrap items-center gap-2"><Badge variant="outline">{stageLabels[item.stage] ?? item.stage}</Badge><span className="font-medium">{outcomeLabels[item.recommendedOutcome]}</span></div><p className="mt-2 text-sm">{item.reviewerName} · {item.commentCategoryLabel}</p><p className="mt-2 whitespace-pre-wrap break-words text-sm text-muted-foreground">{item.reviewComment}</p>{item.documentReference ? <p className="mt-2 text-xs text-muted-foreground">DMS evidence: {item.documentReference}</p> : null}</div><p className="text-xs text-muted-foreground">{formatDate(item.reviewedAt)}</p></div></article>)}</CardContent></Card>
  </div>;
}
