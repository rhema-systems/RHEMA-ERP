import { type Dispatch, type SetStateAction } from 'react';
import { format } from 'date-fns';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type {
  AttachProjectDocumentDto,
  CreateProjectCommentDto,
  ProjectDetailDto,
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
  onDeleteDocument: (documentId: string) => void;
  comment: CreateProjectCommentDto;
  setComment: Dispatch<SetStateAction<CreateProjectCommentDto>>;
  flatWorkItems: FlatWorkItem[];
  onPostComment: () => void;
};

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
          </div>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="grid gap-2">
              <Label>Upload File</Label>
              <Input type="file" onChange={(event) => setDocFile(event.target.files?.[0] || null)} />
              {docFile ? <div className="text-xs text-muted-foreground">{docFile.name}</div> : null}
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
          {project.documents.map((document) => (
            <div key={document.id} className="flex items-center justify-between rounded-lg border p-4">
              <div>
                <div className="font-medium">{document.documentName}</div>
                <div className="text-sm text-muted-foreground">
                  {document.category} | {document.documentType} | {document.isExternalVisible ? 'External' : 'Internal'}
                </div>
              </div>
              <div className="flex gap-2">
                <a className="text-sm underline" href={document.publicUrl || document.filePath} target="_blank" rel="noreferrer">
                  Open
                </a>
                <Button variant="ghost" size="sm" onClick={() => onDeleteDocument(document.id)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Comments</CardTitle>
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
          {project.comments.map((projectComment) => (
            <div key={projectComment.id} className="rounded-lg border p-4">
              <div className="flex items-center justify-between">
                <div className="font-medium">{projectComment.createdBy || 'System'}</div>
                <div className="text-xs text-muted-foreground">{format(new Date(projectComment.createdAt), 'MMM dd, yyyy HH:mm')}</div>
              </div>
              <div className="mt-2 text-sm">{projectComment.body}</div>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
