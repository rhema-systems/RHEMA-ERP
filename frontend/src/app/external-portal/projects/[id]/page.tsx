'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import { format } from 'date-fns';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { AttachProjectDocumentDto, CreateProjectCommentDto, ProjectCatalogEntryDto, ProjectExternalDetailDto, projectService, SubmitProjectDeliverableDto, UpdateProjectWorkItemProgressDto } from '@/services/projectService';

const commentInit: CreateProjectCommentDto = { body: '', commentType: 'ExternalUpdate' };
const DEFAULT_TASK_STATUSES = ['Assigned', 'InProgress', 'Blocked', 'PendingReview', 'Completed'];
const DEFAULT_DOCUMENT_CATEGORIES = ['ExternalSubmissions', 'Reports', 'General'];
const DEFAULT_DOCUMENT_TYPES = ['PortalAttachment', 'Evidence', 'Attachment'];
const documentInit: AttachProjectDocumentDto = { documentName: '', category: 'ExternalSubmissions', documentType: 'PortalAttachment', filePath: '', versionLabel: '1.0', status: 'Active', isExternalVisible: true };
const deliverableInit: SubmitProjectDeliverableDto = { notes: '' };
const buildProgressDraft = (status = 'InProgress', percentComplete = 0): UpdateProjectWorkItemProgressDto => ({
  status,
  percentComplete,
  notes: '',
});

const formatCatalogLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

const resolveCatalogOptions = (entries: ProjectCatalogEntryDto[], fallbackValues: string[], currentValue?: string) => {
  const configured = entries
    .filter((entry) => entry.isActive)
    .map((entry) => entry.name.trim())
    .filter(Boolean);

  const values = configured.length > 0 ? configured : fallbackValues;
  return currentValue && !values.includes(currentValue) ? [currentValue, ...values] : values;
};

export default function ExternalProjectDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;
  const [project, setProject] = useState<ProjectExternalDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('plan');
  const [focusedWorkItemId, setFocusedWorkItemId] = useState<string | null>(null);
  const [focusedDeliverableId, setFocusedDeliverableId] = useState<string | null>(null);
  const [comment, setComment] = useState<CreateProjectCommentDto>(commentInit);
  const [document, setDocument] = useState<AttachProjectDocumentDto>(documentInit);
  const [file, setFile] = useState<File | null>(null);
  const [deliverableFiles, setDeliverableFiles] = useState<Record<string, File | null>>({});
  const [progressDrafts, setProgressDrafts] = useState<Record<string, UpdateProjectWorkItemProgressDto>>({});
  const [deliverableDrafts, setDeliverableDrafts] = useState<Record<string, SubmitProjectDeliverableDto>>({});
  const [taskStatusCatalog, setTaskStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [documentCategoryCatalog, setDocumentCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [documentTypeCatalog, setDocumentTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);

  const documentCategoryOptions = useMemo(
    () => resolveCatalogOptions(documentCategoryCatalog, DEFAULT_DOCUMENT_CATEGORIES, document.category),
    [documentCategoryCatalog, document.category],
  );
  const documentTypeOptions = useMemo(
    () => resolveCatalogOptions(documentTypeCatalog, DEFAULT_DOCUMENT_TYPES, document.documentType),
    [documentTypeCatalog, document.documentType],
  );
  const documentLookup = useMemo(
    () => new Map((project?.documents || []).map((item) => [item.id, item])),
    [project],
  );

  const actionQueue = useMemo(() => {
    if (!project) return [];

    const workItemActions = project.workItems
      .filter((item) => item.canExternalUpdate)
      .map((item) => ({
        id: `work-${item.id}`,
        title: item.title,
        detail: `${item.percentComplete}% complete | ${item.status}`,
        type: 'Task update',
        tab: 'plan',
        targetId: item.id,
        actionLabel: 'Open task',
      }));

    const deliverableActions = project.deliverables
      .filter((item) => (item.externalSubmissionAllowed && item.canExternalSubmit && !['PendingApproval', 'Approved'].includes(item.status))
        || (item.externalSignOffRequired && item.canExternalApprove && item.status === 'PendingExternalSignOff'))
      .map((item) => ({
        id: `del-${item.id}`,
        title: item.title,
        detail: item.status,
        type: item.status === 'PendingExternalSignOff' ? 'Sign-off required' : 'Deliverable submission',
        tab: 'deliverables',
        targetId: item.id,
        actionLabel: item.status === 'PendingExternalSignOff' ? 'Open sign-off' : 'Open deliverable',
      }));

    return [...deliverableActions, ...workItemActions].slice(0, 8);
  }, [project]);

  const taskStatusOptionsByWorkItem = useMemo(() => {
    if (!project) return new Map<string, string[]>();

    return new Map(
      project.workItems.map((item) => [
        item.id,
        resolveCatalogOptions(taskStatusCatalog, DEFAULT_TASK_STATUSES, progressDrafts[item.id]?.status || item.status),
      ]),
    );
  }, [project, progressDrafts, taskStatusCatalog]);

  const load = async () => {
    if (!id) return;
    try {
      setLoading(true);
      const [detail, catalogResults] = await Promise.all([
        projectService.getExternalProjectById(id),
        Promise.allSettled([
          projectService.getCatalogEntries('task-statuses'),
          projectService.getCatalogEntries('document-categories'),
          projectService.getCatalogEntries('document-types'),
        ]),
      ]);
      setProject(detail);
      setTaskStatusCatalog(catalogResults[0].status === 'fulfilled' ? catalogResults[0].value : []);
      setDocumentCategoryCatalog(catalogResults[1].status === 'fulfilled' ? catalogResults[1].value : []);
      setDocumentTypeCatalog(catalogResults[2].status === 'fulfilled' ? catalogResults[2].value : []);
      setProgressDrafts(Object.fromEntries(
        detail.workItems.map((item) => [item.id, buildProgressDraft(item.status, item.percentComplete)]),
      ));
      setDeliverableDrafts(Object.fromEntries(
        detail.deliverables.map((item) => [item.id, { ...deliverableInit, submittedDocumentId: item.submittedDocumentId, notes: item.acceptanceNotes || '' }]),
      ));
      setDeliverableFiles({});
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [id]);

  const addComment = async () => {
    if (!project) return;
    try {
      await projectService.addExternalProjectComment(project.id, comment);
      setComment(commentInit);
      await load();
      toast.success('Project update posted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to post project update');
    }
  };

  const addDocument = async () => {
    if (!project) return;
    try {
      if (file) {
        await projectService.uploadExternalProjectDocument(project.id, file, {
          documentName: document.documentName || file.name,
          category: document.category,
          documentType: document.documentType,
          versionLabel: document.versionLabel,
          status: document.status,
          isExternalVisible: true,
        });
      } else if (document.filePath) {
        await projectService.attachExternalProjectDocument(project.id, document);
      } else {
        throw new Error('Choose a file or provide an existing file path');
      }

      setDocument(documentInit);
      setFile(null);
      await load();
      toast.success('Project document added');
    } catch (error: any) {
      toast.error(error.message || 'Failed to add project document');
    }
  };

  const updateProgress = async (workItemId: string) => {
    if (!project) return;
    const draft = progressDrafts[workItemId];
    if (!draft) return;

    try {
      await projectService.updateExternalProjectWorkItemProgress(project.id, workItemId, draft);
      await load();
      toast.success('Task progress updated');
    } catch (error: any) {
      toast.error(error.message || 'Failed to update task progress');
    }
  };

  const submitDeliverable = async (deliverableId: string) => {
    if (!project) return;
    try {
      const draft = { ...(deliverableDrafts[deliverableId] || deliverableInit) };
      const evidenceFile = deliverableFiles[deliverableId];
      if (evidenceFile) {
        const deliverable = project.deliverables.find((item) => item.id === deliverableId);
        const uploaded = await projectService.uploadExternalProjectDocument(project.id, evidenceFile, {
          documentName: deliverable ? `${deliverable.title} evidence` : evidenceFile.name,
          category: documentCategoryOptions[0] || DEFAULT_DOCUMENT_CATEGORIES[0],
          documentType: documentTypeOptions[0] || DEFAULT_DOCUMENT_TYPES[0],
          versionLabel: '1.0',
          status: 'Active',
          isExternalVisible: true,
        });
        draft.submittedDocumentId = uploaded.id;
      }

      await projectService.submitExternalDeliverable(project.id, deliverableId, draft);
      setDeliverableFiles((prev) => ({ ...prev, [deliverableId]: null }));
      await load();
      toast.success('Deliverable submitted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to submit deliverable');
    }
  };

  const approveDeliverable = async (deliverableId: string) => {
    if (!project) return;
    try {
      await projectService.approveExternalDeliverable(project.id, deliverableId, deliverableDrafts[deliverableId]?.notes);
      await load();
      toast.success('External sign-off recorded');
    } catch (error: any) {
      toast.error(error.message || 'Failed to record external sign-off');
    }
  };

  const openActionItem = (item: { tab: string; targetId: string }) => {
    setActiveTab(item.tab);
    setFocusedWorkItemId(item.tab === 'plan' ? item.targetId : null);
    setFocusedDeliverableId(item.tab === 'deliverables' ? item.targetId : null);
  };

  if (loading || !project) {
    return <div className="py-20 text-center text-muted-foreground">Loading project...</div>;
  }

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <div className="flex items-center gap-2">
          <h1 className="text-3xl font-bold tracking-tight">{project.title}</h1>
          <Badge variant="outline">{project.projectCode}</Badge>
          <Badge>{project.status}</Badge>
        </div>
        <p className="text-muted-foreground">{project.summary || 'No summary provided.'}</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Project Overview</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-4">
          <div><div className="text-sm text-muted-foreground">Methodology</div><div className="font-medium">{project.methodology}</div></div>
          <div><div className="text-sm text-muted-foreground">Start</div><div className="font-medium">{project.startDate ? format(new Date(project.startDate), 'MMM dd, yyyy') : 'N/A'}</div></div>
          <div><div className="text-sm text-muted-foreground">Target End</div><div className="font-medium">{project.targetEndDate ? format(new Date(project.targetEndDate), 'MMM dd, yyyy') : 'N/A'}</div></div>
          <div><div className="text-sm text-muted-foreground">Progress</div><div className="space-y-2"><div className="font-medium">{project.progressPercent}%</div><Progress value={project.progressPercent} /></div></div>
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2"><CardDescription>Shared Tasks</CardDescription><CardTitle>{project.actionableWorkItemCount}</CardTitle></CardHeader>
          <CardContent className="text-sm text-muted-foreground">Tasks available for update.</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription>Blocked Tasks</CardDescription><CardTitle>{project.blockedWorkItemCount}</CardTitle></CardHeader>
          <CardContent className="text-sm text-muted-foreground">Tasks currently flagged as blocked.</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription>Deliverables To Submit</CardDescription><CardTitle>{project.pendingExternalSubmissionCount}</CardTitle></CardHeader>
          <CardContent className="text-sm text-muted-foreground">Deliverables awaiting submission.</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardDescription>Sign-offs Pending</CardDescription><CardTitle>{project.pendingExternalSignOffCount}</CardTitle></CardHeader>
          <CardContent className="text-sm text-muted-foreground">Items awaiting review or sign-off.</CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Access Summary</CardTitle>
          <CardDescription>Current access and items requiring attention.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <Badge variant={project.canCollaborate ? 'default' : 'outline'}>{project.canCollaborate ? 'Can collaborate' : 'Read only'}</Badge>
            <Badge variant={project.canComment ? 'secondary' : 'outline'}>{project.canComment ? 'Can post updates' : 'No comment access'}</Badge>
            <Badge variant={project.canUploadDocuments ? 'secondary' : 'outline'}>{project.canUploadDocuments ? 'Can upload documents' : 'No upload access'}</Badge>
            <Badge variant={project.externalCollaborationEnabled ? 'secondary' : 'outline'}>{project.externalCollaborationEnabled ? 'Access enabled' : 'Limited access'}</Badge>
          </div>
          <div className="space-y-3">
            {actionQueue.length === 0 ? (
              <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No items require action right now.</div>
            ) : (
              actionQueue.map((item) => (
                <div key={item.id} className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <div className="font-medium">{item.title}</div>
                    <div className="text-sm text-muted-foreground">{item.detail}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">{item.type}</Badge>
                    <Button variant="outline" size="sm" onClick={() => openActionItem(item)}>
                      {item.actionLabel}
                    </Button>
                  </div>
                </div>
              ))
            )}
          </div>
        </CardContent>
      </Card>

      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <TabsList className="grid w-full grid-cols-5">
          <TabsTrigger value="plan">Plan</TabsTrigger>
          <TabsTrigger value="milestones">Milestones</TabsTrigger>
          <TabsTrigger value="deliverables">Deliverables</TabsTrigger>
          <TabsTrigger value="documents">Documents</TabsTrigger>
          <TabsTrigger value="updates">Updates</TabsTrigger>
        </TabsList>

        <TabsContent value="plan" className="space-y-4">
          {project.workItems.length === 0 ? (
            <Card><CardContent className="py-10 text-center text-muted-foreground">No work items are available.</CardContent></Card>
          ) : (
            project.workItems.map((item) => (
              <Card key={item.id} className={focusedWorkItemId === item.id ? 'ring-2 ring-blue-500' : undefined}>
                <CardContent className="grid gap-4 p-4 md:grid-cols-[1.5fr,0.9fr]">
                  <div className="space-y-2">
                    <div className="font-medium">{item.title}</div>
                    <div className="text-sm text-muted-foreground">{item.nodeType} | {item.status}</div>
                    {item.description ? <div className="text-sm text-muted-foreground">{item.description}</div> : null}
                    <div className="text-sm text-muted-foreground">{item.percentComplete}% complete</div>
                  </div>
                  <div className="space-y-3">
                    <div className="grid gap-3 md:grid-cols-2">
                      <div className="grid gap-2">
                        <Label>Status</Label>
                        <Select
                          disabled={!item.canExternalUpdate}
                          value={progressDrafts[item.id]?.status || item.status}
                          onValueChange={(value) => setProgressDrafts((prev) => ({
                            ...prev,
                            [item.id]: { ...buildProgressDraft(item.status, item.percentComplete), ...prev[item.id], status: value },
                          }))}
                        >
                          <SelectTrigger><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {(taskStatusOptionsByWorkItem.get(item.id) || DEFAULT_TASK_STATUSES).map((status) => (
                              <SelectItem key={status} value={status}>{formatCatalogLabel(status)}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="grid gap-2">
                        <Label>Percent Complete</Label>
                        <Input
                          type="number"
                          min={0}
                          max={100}
                          disabled={!item.canExternalUpdate}
                          value={progressDrafts[item.id]?.percentComplete ?? item.percentComplete}
                          onChange={(e) => setProgressDrafts((prev) => ({
                            ...prev,
                            [item.id]: {
                              ...buildProgressDraft(item.status, item.percentComplete),
                              ...prev[item.id],
                              percentComplete: Math.max(0, Math.min(100, Number(e.target.value || 0))),
                            },
                          }))}
                        />
                      </div>
                    </div>
                    {item.canExternalUpdate ? (
                      <>
                        <div className="grid gap-2">
                          <Label>Progress Note</Label>
                          <Textarea
                            rows={2}
                            value={progressDrafts[item.id]?.notes || ''}
                            onChange={(e) => setProgressDrafts((prev) => ({
                              ...prev,
                              [item.id]: { ...buildProgressDraft(item.status, item.percentComplete), ...prev[item.id], notes: e.target.value },
                            }))}
                          />
                        </div>
                        <Button onClick={() => updateProgress(item.id)}>Update Progress</Button>
                      </>
                    ) : project.externalCollaborationEnabled ? (
                      <div className="text-sm text-muted-foreground">Read-only task.</div>
                    ) : null}
                  </div>
                </CardContent>
              </Card>
            ))
          )}
        </TabsContent>

        <TabsContent value="milestones" className="space-y-4">
          {project.milestones.map((item) => (
            <Card key={item.id}>
              <CardContent className="flex items-start justify-between gap-4 p-4">
                <div>
                  <div className="font-medium">{item.title}</div>
                  <div className="text-sm text-muted-foreground">{format(new Date(item.targetDate), 'MMM dd, yyyy')}</div>
                </div>
                <Badge variant={item.status === 'Completed' ? 'outline' : 'default'}>{item.status}</Badge>
              </CardContent>
            </Card>
          ))}
        </TabsContent>

        <TabsContent value="deliverables" className="space-y-4">
          {!project.deliverables.length ? (
            <Card><CardContent className="py-10 text-center text-muted-foreground">No deliverables are available.</CardContent></Card>
          ) : (
            project.deliverables.map((item) => (
              <Card key={item.id} className={focusedDeliverableId === item.id ? 'ring-2 ring-blue-500' : undefined}>
                <CardHeader>
                  <CardTitle>{item.title}</CardTitle>
                  <CardDescription>
                    {item.status}
                    {item.targetDate ? ` | due ${format(new Date(item.targetDate), 'MMM dd, yyyy')}` : ''}
                    {item.externalSubmissionAllowed ? ' | submission available' : ''}
                    {item.externalSignOffRequired ? ' | sign-off required' : ''}
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  {item.description ? <div className="text-sm text-muted-foreground">{item.description}</div> : null}
                  {item.externalApprovedAt ? (
                    <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                      External sign-off recorded {format(new Date(item.externalApprovedAt), 'MMM dd, yyyy')}
                      {item.externalApprovalNotes ? ` | ${item.externalApprovalNotes}` : ''}
                    </div>
                  ) : null}
                  {item.status === 'PendingApproval' ? (
                    <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                      External sign-off is complete. Internal approval workflow is now in progress.
                    </div>
                  ) : null}
                  <div className="grid gap-2">
                    <Label>Submission / approval notes</Label>
                    <Textarea
                      rows={3}
                      disabled={!item.canExternalSubmit && !item.canExternalApprove}
                      value={deliverableDrafts[item.id]?.notes || ''}
                      onChange={(e) => setDeliverableDrafts((prev) => ({
                        ...prev,
                        [item.id]: { ...deliverableInit, ...prev[item.id], notes: e.target.value },
                      }))} 
                    />
                  </div>
                  {item.canExternalSubmit ? (
                    <div className="grid gap-3 md:grid-cols-2">
                      <div className="grid gap-2">
                        <Label>Supporting document</Label>
                        <Select
                          value={deliverableDrafts[item.id]?.submittedDocumentId || 'none'}
                          onValueChange={(value) => setDeliverableDrafts((prev) => ({
                            ...prev,
                            [item.id]: { ...deliverableInit, ...prev[item.id], submittedDocumentId: value === 'none' ? undefined : value },
                          }))}
                        >
                          <SelectTrigger><SelectValue placeholder="Select uploaded evidence" /></SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">No linked document</SelectItem>
                            {project.documents.map((documentItem) => (
                              <SelectItem key={documentItem.id} value={documentItem.id}>
                                {documentItem.documentName}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      {project.canUploadDocuments ? (
                        <div className="grid gap-2">
                          <Label>Upload evidence now</Label>
                          <Input type="file" onChange={(e) => setDeliverableFiles((prev) => ({ ...prev, [item.id]: e.target.files?.[0] || null }))} />
                        </div>
                      ) : null}
                    </div>
                  ) : null}
                  {deliverableDrafts[item.id]?.submittedDocumentId ? (
                    <div className="text-sm text-muted-foreground">
                      Linked evidence: {documentLookup.get(deliverableDrafts[item.id]?.submittedDocumentId ?? '')?.documentName || 'Selected document'}
                    </div>
                  ) : null}
                  <div className="flex flex-wrap gap-2">
                    {project.externalCollaborationEnabled && item.externalSubmissionAllowed ? (
                      <Button
                        disabled={!item.canExternalSubmit || item.status === 'PendingApproval' || item.status === 'Approved'}
                        onClick={() => submitDeliverable(item.id)}
                      >
                        Submit Deliverable
                      </Button>
                    ) : null}
                    {project.externalCollaborationEnabled && item.externalSignOffRequired ? (
                      <Button
                        variant="outline"
                        disabled={!item.canExternalApprove || item.status !== 'PendingExternalSignOff'}
                        onClick={() => approveDeliverable(item.id)}
                      >
                        Record Sign-off
                      </Button>
                    ) : null}
                  </div>
                  <div className="text-xs text-muted-foreground">
                    You can link an existing document or upload evidence during submission. Sign-off continues through the internal approval process.
                  </div>
                </CardContent>
              </Card>
            ))
          )}
        </TabsContent>

        <TabsContent value="documents" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Documents</CardTitle>
              <CardDescription>Only available documents are shown here.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {!project.documents.length ? (
                <div className="text-sm text-muted-foreground">No documents are available.</div>
              ) : (
                project.documents.map((item) => (
                  <div key={item.id} className="flex items-center justify-between rounded-md border p-3">
                    <div>
                      <div className="font-medium">{item.documentName}</div>
                      <div className="text-sm text-muted-foreground">{item.category} | {item.documentType}</div>
                    </div>
                    <a className="text-sm underline" href={item.publicUrl || item.filePath} target="_blank" rel="noreferrer">Open</a>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          {project.externalCollaborationEnabled && project.canUploadDocuments && (
            <Card>
              <CardHeader><CardTitle>Share Document</CardTitle></CardHeader>
              <CardContent className="space-y-4">
                <div className="grid gap-4 md:grid-cols-3">
                  <div className="grid gap-2"><Label>Name</Label><Input value={document.documentName} onChange={(e) => setDocument((p) => ({ ...p, documentName: e.target.value }))} /></div>
                  <div className="grid gap-2"><Label>Category</Label><Select value={document.category || documentCategoryOptions[0]} onValueChange={(value) => setDocument((p) => ({ ...p, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{documentCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                  <div className="grid gap-2"><Label>Type</Label><Select value={document.documentType || documentTypeOptions[0]} onValueChange={(value) => setDocument((p) => ({ ...p, documentType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{documentTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                </div>
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="grid gap-2"><Label>Upload File</Label><Input type="file" onChange={(e) => setFile(e.target.files?.[0] || null)} /></div>
                  <div className="grid gap-2"><Label>Existing File Path</Label><Input value={document.filePath || ''} onChange={(e) => setDocument((p) => ({ ...p, filePath: e.target.value }))} /></div>
                </div>
                <Button onClick={addDocument}>Add Document</Button>
              </CardContent>
            </Card>
          )}
          {project.externalCollaborationEnabled && !project.canUploadDocuments ? (
            <Card><CardContent className="py-6 text-sm text-muted-foreground">Document upload is not available for this account.</CardContent></Card>
          ) : null}
        </TabsContent>

        <TabsContent value="updates" className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Project Updates</CardTitle></CardHeader>
            <CardContent className="space-y-3">
              {!project.comments.length ? (
                <div className="text-sm text-muted-foreground">No updates are available.</div>
              ) : (
                project.comments.map((item) => (
                  <div key={item.id} className="rounded-md border p-3">
                    <div className="flex items-center justify-between">
                      <div className="font-medium">{item.createdBy || 'Portal user'}</div>
                      <div className="text-xs text-muted-foreground">{format(new Date(item.createdAt), 'MMM dd, yyyy HH:mm')}</div>
                    </div>
                    <div className="mt-2 text-sm">{item.body}</div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          {project.externalCollaborationEnabled && project.canComment && (
            <Card>
              <CardHeader><CardTitle>Post Update</CardTitle></CardHeader>
              <CardContent className="space-y-4">
                <div className="grid gap-2">
                  <Label>Related Work Item</Label>
                  <Select
                    value={comment.workItemId || 'none'}
                    onValueChange={(value) => setComment((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}
                  >
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Project update</SelectItem>
                      {project.workItems.filter((item) => item.canExternalComment).map((item) => (
                        <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Update</Label>
                  <Textarea rows={4} value={comment.body} onChange={(e) => setComment((p) => ({ ...p, body: e.target.value, commentType: 'ExternalUpdate' }))} />
                </div>
                <Button onClick={addComment} disabled={!comment.body.trim()}>Post Update</Button>
              </CardContent>
            </Card>
          )}
          {project.externalCollaborationEnabled && !project.canComment ? (
            <Card><CardContent className="py-6 text-sm text-muted-foreground">Posting updates is not available for this account.</CardContent></Card>
          ) : null}
        </TabsContent>
      </Tabs>
    </div>
  );
}
