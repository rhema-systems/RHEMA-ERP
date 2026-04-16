import { useMemo, useState, type Dispatch, type SetStateAction } from 'react';
import { format } from 'date-fns';
import { Download, ExternalLink, Eye, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type {
  AttachProjectDocumentDto,
  CreateProjectCommentDto,
  ProjectDetailDto,
  ProjectDocumentDto,
  ProjectWorkItemDto,
} from '@/services/projectService';

type FlatWorkItem = ProjectWorkItemDto & {
  depth: number;
  outline: string;
};

type ProjectDocumentsTabProps = {
  project: ProjectDetailDto;
  doc: AttachProjectDocumentDto;
  setDoc: Dispatch<SetStateAction<AttachProjectDocumentDto>>;
  docFile: File | null;
  setDocFile: Dispatch<SetStateAction<File | null>>;
  documentCategoryOptions: string[];
  documentTypeOptions: string[];
  boolValue: (value?: boolean) => string;
  formatCatalogLabel: (value: string) => string;
  onAttachDocument: () => void;
  onDeleteDocument: (documentId: string) => Promise<void> | void;
  comment: CreateProjectCommentDto;
  setComment: Dispatch<SetStateAction<CreateProjectCommentDto>>;
  flatWorkItems: FlatWorkItem[];
  onPostComment: () => void;
};

type DocumentRelationGroup = {
  label: string;
  options: Array<{ value: string; label: string }>;
};

type DocumentVisibilityFilter = 'all' | 'internal' | 'external';
type DocumentRelationFilter = 'all' | 'project' | 'phase' | 'package' | 'workitem';

const PROJECT_DOCUMENT_RELATION_VALUE = 'Project';
const ALL_DOCUMENT_FILTER_VALUE = 'all';

const flattenPhaseOptions = (phases: ProjectDetailDto['phases'], depth = 0): Array<{ id: string; label: string }> =>
  phases.flatMap((phase) => [
    { id: phase.id, label: `${' '.repeat(depth * 2)}${phase.code ? `${phase.code} - ` : ''}${phase.name}`.trimStart() },
    ...flattenPhaseOptions(phase.children || [], depth + 1),
  ]);

const encodeDocumentRelationValue = (artifactType?: string, artifactId?: string) =>
  artifactType && artifactType !== 'Project' && artifactId ? `${artifactType}:${artifactId}` : PROJECT_DOCUMENT_RELATION_VALUE;

const decodeDocumentRelationValue = (value: string): { artifactType: string; artifactId?: string } =>
  value === PROJECT_DOCUMENT_RELATION_VALUE
    ? { artifactType: 'Project' }
    : (() => {
      const [artifactType, artifactId] = value.split(':');
      return { artifactType, artifactId };
    })();

export function ProjectDocumentsTab({
  project,
  doc,
  setDoc,
  docFile,
  setDocFile,
  documentCategoryOptions,
  documentTypeOptions,
  boolValue,
  formatCatalogLabel,
  onAttachDocument,
  onDeleteDocument,
  comment,
  setComment,
  flatWorkItems,
  onPostComment,
}: ProjectDocumentsTabProps) {
  const [previewDocument, setPreviewDocument] = useState<ProjectDocumentDto | null>(null);
  const [pendingDeleteDocument, setPendingDeleteDocument] = useState<ProjectDocumentDto | null>(null);
  const [documentSearch, setDocumentSearch] = useState('');
  const [documentCategoryFilter, setDocumentCategoryFilter] = useState(ALL_DOCUMENT_FILTER_VALUE);
  const [documentRelationFilter, setDocumentRelationFilter] = useState<DocumentRelationFilter>(ALL_DOCUMENT_FILTER_VALUE);
  const [documentVisibilityFilter, setDocumentVisibilityFilter] = useState<DocumentVisibilityFilter>(ALL_DOCUMENT_FILTER_VALUE);
  const [commentSearch, setCommentSearch] = useState('');
  const documentRelationGroups = useMemo<DocumentRelationGroup[]>(
    () => [
      {
        label: 'Project',
        options: [{ value: PROJECT_DOCUMENT_RELATION_VALUE, label: `${project.projectCode} - ${project.title}` }],
      },
      {
        label: 'Phases',
        options: flattenPhaseOptions(project.phases).map((phase) => ({ value: `Phase:${phase.id}`, label: phase.label })),
      },
      {
        label: 'Work Components',
        options: [...project.packages]
          .sort((left, right) =>
            (left.projectPhaseName || '').localeCompare(right.projectPhaseName || '')
            || (left.code || left.name).localeCompare(right.code || right.name),
          )
          .map((projectPackage) => ({
            value: `Package:${projectPackage.id}`,
            label: projectPackage.projectPhaseName
              ? `${projectPackage.projectPhaseName} / ${projectPackage.code ? `${projectPackage.code} - ` : ''}${projectPackage.name}`
              : projectPackage.code ? `${projectPackage.code} - ${projectPackage.name}` : projectPackage.name,
          })),
      },
      {
        label: 'Work Items',
        options: flatWorkItems.map((item) => ({ value: `WorkItem:${item.id}`, label: `${' '.repeat(item.depth * 2)}${item.title}`.trimStart() })),
      },
    ].filter((group) => group.options.length > 0),
    [flatWorkItems, project.packages, project.phases, project.projectCode, project.title],
  );
  const workItemTitleLookup = useMemo(
    () => new Map(flatWorkItems.map((item) => [item.id, item.title])),
    [flatWorkItems],
  );
  const orderedDocuments = useMemo(
    () => [...project.documents].sort((left, right) =>
      new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
      || left.documentName.localeCompare(right.documentName),
    ),
    [project.documents],
  );
  const documentCategoryFilterOptions = useMemo(
    () => Array.from(new Set(orderedDocuments.map((document) => document.category).filter(Boolean))).sort((left, right) => left.localeCompare(right)),
    [orderedDocuments],
  );
  const filteredDocuments = useMemo(() => {
    const normalizedSearch = documentSearch.trim().toLowerCase();

    return orderedDocuments.filter((document) => {
      if (documentCategoryFilter !== ALL_DOCUMENT_FILTER_VALUE && document.category !== documentCategoryFilter) {
        return false;
      }

      const normalizedArtifactType = (document.artifactType || 'Project').toLowerCase() as DocumentRelationFilter;
      if (documentRelationFilter !== ALL_DOCUMENT_FILTER_VALUE && normalizedArtifactType !== documentRelationFilter) {
        return false;
      }

      if (documentVisibilityFilter === 'external' && !document.isExternalVisible) {
        return false;
      }

      if (documentVisibilityFilter === 'internal' && document.isExternalVisible) {
        return false;
      }

      if (!normalizedSearch) {
        return true;
      }

      return getDocumentSearchReference(document).includes(normalizedSearch);
    });
  }, [
    documentCategoryFilter,
    documentRelationFilter,
    documentSearch,
    documentVisibilityFilter,
    orderedDocuments,
  ]);
  const hasActiveDocumentFilters = Boolean(documentSearch.trim())
    || documentCategoryFilter !== ALL_DOCUMENT_FILTER_VALUE
    || documentRelationFilter !== ALL_DOCUMENT_FILTER_VALUE
    || documentVisibilityFilter !== ALL_DOCUMENT_FILTER_VALUE;
  const photoDocuments = useMemo(
    () => filteredDocuments.filter((document) => isImageDocument(document)).slice(0, 8),
    [filteredDocuments],
  );
  const orderedComments = useMemo(
    () => [...project.comments].sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()),
    [project.comments],
  );
  const filteredComments = useMemo(() => {
    const normalizedSearch = commentSearch.trim().toLowerCase();
    if (!normalizedSearch) {
      return orderedComments;
    }

    return orderedComments.filter((projectComment) =>
      `${projectComment.body} ${projectComment.createdBy || ''} ${workItemTitleLookup.get(projectComment.workItemId || '') || ''}`
        .toLowerCase()
        .includes(normalizedSearch));
  }, [commentSearch, orderedComments, workItemTitleLookup]);

  const handleDownloadDocument = (projectDocument: ProjectDocumentDto) => {
    const url = getDocumentUrl(projectDocument);
    if (!url) {
      return;
    }

    const link = globalThis.document.createElement('a');
    link.href = url;
    link.download = projectDocument.documentName || 'document';
    link.rel = 'noreferrer';
    globalThis.document.body.appendChild(link);
    link.click();
    link.remove();
  };

  const handleConfirmDocumentDelete = async () => {
    if (!pendingDeleteDocument) {
      return false;
    }

    if (previewDocument?.id === pendingDeleteDocument.id) {
      setPreviewDocument(null);
    }

    await onDeleteDocument(pendingDeleteDocument.id);
    setPendingDeleteDocument(null);
    return true;
  };

  const resetDocumentFilters = () => {
    setDocumentSearch('');
    setDocumentCategoryFilter(ALL_DOCUMENT_FILTER_VALUE);
    setDocumentRelationFilter(ALL_DOCUMENT_FILTER_VALUE);
    setDocumentVisibilityFilter(ALL_DOCUMENT_FILTER_VALUE);
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Documents</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="grid gap-2">
              <Label>Name</Label>
              <Input value={doc.documentName} onChange={(event) => setDoc((current) => ({ ...current, documentName: event.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Category</Label>
              <Select value={doc.category || documentCategoryOptions[0]} onValueChange={(value) => setDoc((current) => ({ ...current, category: value }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {documentCategoryOptions.map((item) => (
                    <SelectItem key={item} value={item}>
                      {formatCatalogLabel(item)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Type</Label>
              <Select value={doc.documentType || documentTypeOptions[0]} onValueChange={(value) => setDoc((current) => ({ ...current, documentType: value }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {documentTypeOptions.map((item) => (
                    <SelectItem key={item} value={item}>
                      {formatCatalogLabel(item)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Related To</Label>
              <Select
                value={encodeDocumentRelationValue(doc.artifactType, doc.artifactId)}
                onValueChange={(value) => {
                  const relation = decodeDocumentRelationValue(value);
                  setDoc((current) => ({
                    ...current,
                    artifactType: relation.artifactType,
                    artifactId: relation.artifactId,
                  }));
                }}
              >
                <SelectTrigger><SelectValue placeholder="Project" /></SelectTrigger>
                <SelectContent>
                  {documentRelationGroups.map((group) => (
                    <SelectGroup key={group.label}>
                      <SelectLabel>{group.label}</SelectLabel>
                      {group.options.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectGroup>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="grid gap-2">
              <Label>Upload File</Label>
              <Input type="file" onChange={(event) => setDocFile(event.target.files?.[0] || null)} />
              {docFile ? <div className="text-xs text-muted-foreground">{docFile.name}</div> : <div className="text-xs text-muted-foreground">Upload building photos, drawings, PDFs, or other attachments.</div>}
            </div>
            <div className="grid gap-2">
              <Label>Existing File Path</Label>
              <Input value={doc.filePath} onChange={(event) => setDoc((current) => ({ ...current, filePath: event.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Portal Visibility</Label>
              <Select value={boolValue(doc.isExternalVisible)} onValueChange={(value) => setDoc((current) => ({ ...current, isExternalVisible: value === 'true' }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="false">Internal only</SelectItem>
                  <SelectItem value="true">Visible externally</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="flex justify-end">
            <Button onClick={onAttachDocument}>
              <Plus className="mr-2 h-4 w-4" />
              Attach
            </Button>
          </div>
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs uppercase tracking-wide text-slate-500">Total Documents</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{project.documents.length}</div>
              <div className="mt-1 text-xs text-slate-500">Project records and attachments</div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs uppercase tracking-wide text-slate-500">Photos</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{project.documents.filter((document) => isImageDocument(document)).length}</div>
              <div className="mt-1 text-xs text-slate-500">Visual site and progress evidence</div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs uppercase tracking-wide text-slate-500">External Visible</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{project.documents.filter((document) => document.isExternalVisible).length}</div>
              <div className="mt-1 text-xs text-slate-500">Available for portal-facing collaboration</div>
            </div>
            <div className="rounded-xl border bg-slate-50/70 p-4">
              <div className="text-xs uppercase tracking-wide text-slate-500">Comments</div>
              <div className="mt-2 text-2xl font-semibold text-slate-900">{project.comments.length}</div>
              <div className="mt-1 text-xs text-slate-500">Conversation trail across the project</div>
            </div>
          </div>
          <div className="rounded-xl border bg-muted/20 p-4">
            <div className="grid gap-4 xl:grid-cols-[1.5fr,1fr,1fr,1fr,auto]">
              <div className="grid gap-2">
                <Label>Search Documents</Label>
                <Input
                  value={documentSearch}
                  onChange={(event) => setDocumentSearch(event.target.value)}
                  placeholder="Search by name, relation, type, path, or version"
                />
              </div>
              <div className="grid gap-2">
                <Label>Category Filter</Label>
                <Select value={documentCategoryFilter} onValueChange={setDocumentCategoryFilter}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_DOCUMENT_FILTER_VALUE}>All categories</SelectItem>
                    {documentCategoryFilterOptions.map((item) => (
                      <SelectItem key={item} value={item}>
                        {formatCatalogLabel(item)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Related To</Label>
                <Select value={documentRelationFilter} onValueChange={(value) => setDocumentRelationFilter(value as DocumentRelationFilter)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_DOCUMENT_FILTER_VALUE}>All relations</SelectItem>
                    <SelectItem value="project">Project</SelectItem>
                    <SelectItem value="phase">Phases</SelectItem>
                    <SelectItem value="package">Work components</SelectItem>
                    <SelectItem value="workitem">Work items</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Visibility</Label>
                <Select value={documentVisibilityFilter} onValueChange={(value) => setDocumentVisibilityFilter(value as DocumentVisibilityFilter)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_DOCUMENT_FILTER_VALUE}>All visibility</SelectItem>
                    <SelectItem value="internal">Internal only</SelectItem>
                    <SelectItem value="external">External visible</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex items-end justify-end">
                <Button variant="outline" onClick={resetDocumentFilters} disabled={!hasActiveDocumentFilters}>
                  Clear Filters
                </Button>
              </div>
            </div>
            <div className="mt-3 text-xs text-muted-foreground">
              Showing {filteredDocuments.length} of {orderedDocuments.length} document(s).
            </div>
          </div>
          {photoDocuments.length > 0 ? (
            <div className="space-y-3">
              <div>
                <div className="text-sm font-medium">{hasActiveDocumentFilters ? 'Filtered Photo Preview' : 'Photo Preview'}</div>
                <div className="text-xs text-muted-foreground">Quick preview of uploaded building photos in the current result set.</div>
              </div>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                {photoDocuments.map((projectDocument) => {
                  const url = getDocumentUrl(projectDocument);
                  if (!url) {
                    return null;
                  }

                  return (
                    <button
                      key={projectDocument.id}
                      type="button"
                      onClick={() => setPreviewDocument(projectDocument)}
                      className="overflow-hidden rounded-lg border text-left transition hover:border-primary hover:shadow-sm"
                    >
                      <div className="aspect-[4/3] bg-muted">
                        <img src={url} alt={projectDocument.documentName} className="h-full w-full object-cover" />
                      </div>
                      <div className="space-y-1 p-3">
                        <div className="line-clamp-1 text-sm font-medium">{projectDocument.documentName}</div>
                        <div className="line-clamp-1 text-xs text-muted-foreground">{projectDocument.category}</div>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          ) : null}
          {filteredDocuments.length === 0 ? (
            <div className="rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">
              No documents match the current filters.
            </div>
          ) : filteredDocuments.map((document) => {
            const documentUrl = getDocumentUrl(document);
            const previewable = documentUrl && canPreviewDocument(document);
            const relatedLabel = document.artifactLabel || 'Project';
            const linkedWorkItemTitle = document.artifactType === 'WorkItem' && document.artifactId
              ? workItemTitleLookup.get(document.artifactId)
              : undefined;

            return (
              <div key={document.id} className="rounded-xl border bg-white p-4 shadow-sm transition hover:border-slate-300">
                <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                  <div className="flex items-start gap-3">
                    {isImageDocument(document) && documentUrl ? (
                      <button
                        type="button"
                        onClick={() => setPreviewDocument(document)}
                        className="h-16 w-16 overflow-hidden rounded-md border bg-muted"
                        aria-label={`Preview ${document.documentName}`}
                      >
                        <img src={documentUrl} alt={document.documentName} className="h-full w-full object-cover" />
                      </button>
                    ) : (
                      <div className="flex h-16 w-16 items-center justify-center rounded-md border bg-muted/40 text-xs font-medium text-muted-foreground">
                        FILE
                      </div>
                    )}
                    <div className="space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        <div className="font-medium text-slate-900">{document.documentName}</div>
                        <Badge variant="outline">{formatCatalogLabel(document.category)}</Badge>
                        <Badge variant="secondary">{formatCatalogLabel(document.documentType)}</Badge>
                        <Badge variant={document.isExternalVisible ? 'default' : 'outline'}>
                          {document.isExternalVisible ? 'External' : 'Internal'}
                        </Badge>
                      </div>
                      <div className="flex flex-wrap gap-3 text-xs text-muted-foreground">
                        {document.versionLabel ? <span>Version {document.versionLabel}</span> : null}
                        {document.status ? <span>Status {formatCatalogLabel(document.status)}</span> : null}
                        {document.fileSize ? <span>{formatFileSize(document.fileSize)}</span> : null}
                        <span>Added {format(new Date(document.createdAt), 'MMM dd, yyyy HH:mm')}</span>
                      </div>
                      <div className="text-xs text-muted-foreground">
                        Related to {relatedLabel}{linkedWorkItemTitle ? ` · ${linkedWorkItemTitle}` : ''}
                      </div>
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2 xl:justify-end">
                    {previewable ? (
                      <Button variant="outline" size="sm" onClick={() => setPreviewDocument(document)}>
                        <Eye className="mr-2 h-4 w-4" />
                        Preview
                      </Button>
                    ) : null}
                    {documentUrl ? (
                      <Button variant="outline" size="sm" asChild>
                        <a href={documentUrl} target="_blank" rel="noreferrer">
                          <ExternalLink className="mr-2 h-4 w-4" />
                          Open
                        </a>
                      </Button>
                    ) : null}
                    {documentUrl ? (
                      <Button variant="outline" size="sm" onClick={() => handleDownloadDocument(document)}>
                        <Download className="mr-2 h-4 w-4" />
                        Download
                      </Button>
                    ) : null}
                    <Button variant="ghost" size="sm" onClick={() => setPendingDeleteDocument(document)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
          <div>
            <CardTitle>Comments</CardTitle>
            <div className="mt-1 text-sm text-muted-foreground">
              Capture coordination notes and work-item-specific project decisions.
            </div>
          </div>
          <div className="w-full xl:max-w-sm">
            <Label className="sr-only">Search Comments</Label>
            <Input
              value={commentSearch}
              onChange={(event) => setCommentSearch(event.target.value)}
              placeholder="Search comments by author, work item, or text"
            />
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-[1fr_220px]">
            <div className="grid gap-2">
              <Label>Comment</Label>
              <Textarea rows={3} value={comment.body} onChange={(event) => setComment((current) => ({ ...current, body: event.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Work Item</Label>
              <Select value={comment.workItemId || 'none'} onValueChange={(value) => setComment((current) => ({ ...current, workItemId: value === 'none' ? undefined : value }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">General</SelectItem>
                  {flatWorkItems.map((item) => (
                    <SelectItem key={item.id} value={item.id}>
                      {item.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="flex justify-end">
            <Button onClick={onPostComment}>
              <Plus className="mr-2 h-4 w-4" />
              Post
            </Button>
          </div>
          <div className="text-xs text-muted-foreground">
            Showing {filteredComments.length} of {orderedComments.length} comment(s).
          </div>
          {filteredComments.length === 0 ? (
            <div className="rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">
              No comments match the current search.
            </div>
          ) : filteredComments.map((projectComment) => (
            <div key={projectComment.id} className="rounded-lg border bg-white p-4">
              <div className="flex flex-col gap-2 xl:flex-row xl:items-start xl:justify-between">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="font-medium">{projectComment.createdBy || 'System'}</div>
                    {projectComment.workItemId ? (
                      <Badge variant="outline">
                        {workItemTitleLookup.get(projectComment.workItemId) || 'Linked work item'}
                      </Badge>
                    ) : (
                      <Badge variant="secondary">General</Badge>
                    )}
                  </div>
                  <div className="text-sm">{projectComment.body}</div>
                </div>
                <div className="text-xs text-muted-foreground">
                  {format(new Date(projectComment.createdAt), 'MMM dd, yyyy HH:mm')}
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
      <Dialog open={previewDocument !== null} onOpenChange={(open) => (!open ? setPreviewDocument(null) : undefined)}>
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>{previewDocument?.documentName || 'Document Preview'}</DialogTitle>
            <DialogDescription>
              {previewDocument ? `${previewDocument.category} | ${previewDocument.documentType}` : 'Preview uploaded project document'}
            </DialogDescription>
          </DialogHeader>
          {previewDocument ? (
            <div className="space-y-4">
              {isImageDocument(previewDocument) ? (
                <div className="max-h-[70vh] overflow-auto rounded-lg border bg-muted/30 p-2">
                  <img
                    src={getDocumentUrl(previewDocument)}
                    alt={previewDocument.documentName}
                    className="mx-auto max-h-[65vh] w-auto max-w-full object-contain"
                  />
                </div>
              ) : isPdfDocument(previewDocument) ? (
                <iframe
                  src={getDocumentUrl(previewDocument)}
                  title={previewDocument.documentName}
                  className="h-[70vh] w-full rounded-lg border"
                />
              ) : (
                <div className="rounded-lg border border-dashed p-6 text-sm text-muted-foreground">
                  Preview is available for images and PDFs. Use Open or Download for this file.
                </div>
              )}
              <div className="flex justify-end gap-2">
                {getDocumentUrl(previewDocument) ? (
                  <Button variant="outline" size="sm" asChild>
                    <a href={getDocumentUrl(previewDocument)} target="_blank" rel="noreferrer">
                      <ExternalLink className="mr-2 h-4 w-4" />
                      Open
                    </a>
                  </Button>
                ) : null}
                {getDocumentUrl(previewDocument) ? (
                  <Button size="sm" onClick={() => handleDownloadDocument(previewDocument)}>
                    <Download className="mr-2 h-4 w-4" />
                    Download
                  </Button>
                ) : null}
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
      <ConfirmationDialog
        open={pendingDeleteDocument !== null}
        onOpenChange={(open) => (!open ? setPendingDeleteDocument(null) : undefined)}
        title="Delete Document"
        description={pendingDeleteDocument ? `Delete ${pendingDeleteDocument.documentName}? This action cannot be undone.` : 'Delete this document?'}
        confirmText="Delete Document"
        variant="destructive"
        onConfirm={handleConfirmDocumentDelete}
      />
    </div>
  );
}

function getDocumentUrl(projectDocument: ProjectDocumentDto): string {
  return projectDocument.publicUrl || projectDocument.filePath || '';
}

function getDocumentSearchReference(projectDocument: ProjectDocumentDto): string {
  return [
    projectDocument.fileType || '',
    projectDocument.documentName,
    projectDocument.publicUrl || '',
    projectDocument.filePath,
    projectDocument.category,
    projectDocument.documentType,
    projectDocument.versionLabel,
    projectDocument.status,
    projectDocument.artifactType,
    projectDocument.artifactLabel || '',
  ].join(' ').toLowerCase();
}

function isImageDocument(projectDocument: ProjectDocumentDto): boolean {
  const reference = getDocumentSearchReference(projectDocument);
  return reference.includes('image/') || /\.(png|jpe?g|gif|bmp|webp|svg)$/.test(reference);
}

function isPdfDocument(projectDocument: ProjectDocumentDto): boolean {
  const reference = getDocumentSearchReference(projectDocument);
  return reference.includes('application/pdf') || /\.pdf$/.test(reference);
}

function canPreviewDocument(projectDocument: ProjectDocumentDto): boolean {
  return isImageDocument(projectDocument) || isPdfDocument(projectDocument);
}

function formatFileSize(fileSize: number): string {
  if (fileSize >= 1024 * 1024) {
    return `${(fileSize / (1024 * 1024)).toFixed(1)} MB`;
  }

  if (fileSize >= 1024) {
    return `${(fileSize / 1024).toFixed(1)} KB`;
  }

  return `${fileSize} B`;
}
