'use client';

import dynamic from 'next/dynamic';
import React from 'react';
import { CheckCircle2, ExternalLink, Eye, FileUp, Loader2, Plus, Save, Send } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  procedureCaseService,
  type ProcedureCaseDetail,
  type ProcedureCaseDocument,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';

const ProcedurePdfViewer = dynamic(() => import('@/components/procedures/ProcedurePdfViewer'), {
  ssr: false,
  loading: () => (
    <div className="rounded-md border border-border bg-background p-4 text-sm text-muted-foreground">
      Loading PDF viewer...
    </div>
  ),
});

interface ProcedureCaseWorkspaceProps {
  module: 'Legal' | 'Facilities';
  entityType: string;
  defaultTitle: string;
}

export function ProcedureCaseWorkspace({ module, entityType, defaultTitle }: ProcedureCaseWorkspaceProps) {
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [selectedCase, setSelectedCase] = React.useState<ProcedureCaseDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [documentFiles, setDocumentFiles] = React.useState<Record<string, File | null>>({});
  const [previewDocumentId, setPreviewDocumentId] = React.useState<string | null>(null);
  const [newCase, setNewCase] = React.useState({
    title: defaultTitle,
    referenceNumber: '',
    applicantName: '',
    sourceDepartment: '',
    receivedDate: '',
    description: '',
  });

  const currentStageItems = React.useMemo(
    () => selectedCase?.checklistItems.filter((item) => item.stageIndex === selectedCase.currentStageIndex) ?? [],
    [selectedCase]
  );

  const isPdfDocument = (document: ProcedureCaseDocument) => {
    const value = `${document.fileName ?? ''} ${document.fileUrl ?? ''}`.toLowerCase();
    return value.includes('.pdf');
  };

  const loadCases = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await procedureCaseService.listCases(module, entityType);
      setCases(data);
      if (!selectedCase && data.length > 0) {
        const detail = await procedureCaseService.getCase(data[0].id);
        setSelectedCase(detail);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load procedure cases.');
    } finally {
      setIsLoading(false);
    }
  }, [entityType, module, selectedCase]);

  React.useEffect(() => {
    void loadCases();
  }, [loadCases]);

  const selectCase = async (id: string) => {
    setIsSaving(true);
    setError(null);
    try {
      const detail = await procedureCaseService.getCase(id);
      setSelectedCase(detail);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to open procedure case.');
    } finally {
      setIsSaving(false);
    }
  };

  const createCase = async () => {
    setIsSaving(true);
    setError(null);
    try {
      const created = await procedureCaseService.createCase({
        module,
        entityType,
        ...newCase,
        fieldValues: {},
      });
      setSelectedCase(created);
      setNewCase({
        title: defaultTitle,
        referenceNumber: '',
        applicantName: '',
        sourceDepartment: '',
        receivedDate: '',
        description: '',
      });
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create procedure case.');
    } finally {
      setIsSaving(false);
    }
  };

  const updateFieldValue = (key: string, value: string) => {
    setSelectedCase((current) => {
      if (!current) {
        return current;
      }

      return {
        ...current,
        fields: current.fields.map((field) => (field.key === key ? { ...field, value } : field)),
      };
    });
  };

  const saveIntake = async () => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const fieldValues = Object.fromEntries(selectedCase.fields.map((field) => [field.key, field.value ?? null]));
      const updated = await procedureCaseService.updateFields(selectedCase.id, {
        fieldValues,
        referenceNumber: selectedCase.referenceNumber,
        applicantName: selectedCase.applicantName,
        sourceDepartment: selectedCase.sourceDepartment,
        receivedDate: selectedCase.receivedDate,
        description: selectedCase.description,
      });
      setSelectedCase(updated);
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to save intake fields.');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleChecklist = async (checklistItemId: string, isCompleted: boolean) => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.updateChecklistItem(selectedCase.id, checklistItemId, isCompleted);
      setSelectedCase(updated);
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to update checklist.');
    } finally {
      setIsSaving(false);
    }
  };

  const saveDocument = async (document: ProcedureCaseDocument) => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const selectedFile = documentFiles[document.id];
      const updated = selectedFile
        ? await procedureCaseService.uploadDocument(selectedCase.id, document.id, selectedFile, document.notes)
        : await procedureCaseService.attachDocument(selectedCase.id, document.id, {
            fileName: document.fileName,
            fileUrl: document.fileUrl,
            notes: document.notes,
          });

      setSelectedCase(updated);
      setDocumentFiles((current) => ({ ...current, [document.id]: null }));
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to save document.');
    } finally {
      setIsSaving(false);
    }
  };

  const updateDocumentNotes = (documentId: string, value: string) => {
    setSelectedCase((current) => {
      if (!current) {
        return current;
      }

      return {
        ...current,
        documents: current.documents.map((document) =>
          document.id === documentId ? { ...document, notes: value } : document
        ),
      };
    });
  };

  const selectDocumentFile = (documentId: string, file: File | null) => {
    setDocumentFiles((current) => ({ ...current, [documentId]: file }));
  };

  const completeStage = async () => {
    if (!selectedCase) {
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const updated = await procedureCaseService.completeStage(selectedCase.id, 'Stage completed from workspace.');
      setSelectedCase(updated);
      await loadCases();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to submit current stage.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Card className="border-border bg-card text-card-foreground">
      <CardHeader>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <CardTitle>Live Case Workspace</CardTitle>
            <CardDescription>Create a procedure case, perform assigned stage work, and submit it onward.</CardDescription>
          </div>
          <Badge variant={selectedCase?.usesConfiguredWorkflow ? 'default' : 'outline'}>
            {selectedCase?.usesConfiguredWorkflow ? 'Administration workflow' : 'Procedure stages'}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {error ? (
          <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
            {error}
          </div>
        ) : null}

        <div className="grid gap-4 lg:grid-cols-[320px_minmax(0,1fr)]">
          <div className="space-y-4">
            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3 flex items-center justify-between gap-2">
                <h2 className="text-sm font-semibold">Cases</h2>
                {isLoading ? <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" /> : null}
              </div>
              <div className="space-y-2">
                {cases.length === 0 && !isLoading ? (
                  <p className="text-sm text-muted-foreground">No cases opened for this procedure yet.</p>
                ) : null}
                {cases.map((procedureCase) => (
                  <button
                    key={procedureCase.id}
                    type="button"
                    className={`w-full rounded-md border p-3 text-left text-sm transition-colors ${
                      selectedCase?.id === procedureCase.id
                        ? 'border-primary bg-primary/10'
                        : 'border-border bg-card hover:bg-muted'
                    }`}
                    onClick={() => void selectCase(procedureCase.id)}
                  >
                    <div className="font-medium">{procedureCase.referenceNumber || procedureCase.title}</div>
                    <div className="mt-1 text-xs text-muted-foreground">{procedureCase.currentStageName}</div>
                    <div className="mt-2 flex flex-wrap gap-1">
                      <Badge variant="outline">{procedureCase.status}</Badge>
                      {procedureCase.currentAssignedRole ? (
                        <Badge variant="secondary">{procedureCase.currentAssignedRole}</Badge>
                      ) : null}
                    </div>
                  </button>
                ))}
              </div>
            </div>

            <div className="rounded-md border border-border bg-background p-4">
              <h2 className="text-sm font-semibold">Open Case</h2>
              <div className="mt-3 space-y-3">
                <Input value={newCase.title} onChange={(event) => setNewCase({ ...newCase, title: event.target.value })} />
                <Input placeholder="Reference number" value={newCase.referenceNumber} onChange={(event) => setNewCase({ ...newCase, referenceNumber: event.target.value })} />
                <Input placeholder="Applicant / party name" value={newCase.applicantName} onChange={(event) => setNewCase({ ...newCase, applicantName: event.target.value })} />
                <Input placeholder="Source department" value={newCase.sourceDepartment} onChange={(event) => setNewCase({ ...newCase, sourceDepartment: event.target.value })} />
                <Input type="date" value={newCase.receivedDate} onChange={(event) => setNewCase({ ...newCase, receivedDate: event.target.value })} />
                <Textarea placeholder="Description" value={newCase.description} onChange={(event) => setNewCase({ ...newCase, description: event.target.value })} />
                <Button className="w-full gap-2" onClick={() => void createCase()} disabled={isSaving}>
                  <Plus className="h-4 w-4" />
                  Create case
                </Button>
              </div>
            </div>
          </div>

          {selectedCase ? (
            <div className="space-y-4">
              <div className="rounded-md border border-border bg-background p-4">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div>
                    <h2 className="text-base font-semibold">{selectedCase.referenceNumber || selectedCase.title}</h2>
                    <p className="mt-1 text-sm text-muted-foreground">{selectedCase.currentStageName}</p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant="outline">{selectedCase.status}</Badge>
                    {selectedCase.currentAssignedRole ? <Badge>{selectedCase.currentAssignedRole}</Badge> : null}
                    <Badge variant={selectedCase.canEditCurrentStage ? 'secondary' : 'outline'}>
                      {selectedCase.canEditCurrentStage ? 'Editable' : 'Read only'}
                    </Badge>
                  </div>
                </div>
              </div>

              <div className="rounded-md border border-border bg-background p-4">
                <div className="mb-3 flex items-center justify-between gap-2">
                  <h2 className="text-sm font-semibold">Intake</h2>
                  <Button size="sm" variant="outline" className="gap-2" onClick={() => void saveIntake()} disabled={!selectedCase.canEditCurrentStage || isSaving}>
                    <Save className="h-4 w-4" />
                    Save
                  </Button>
                </div>
                <div className="grid gap-3 md:grid-cols-2">
                  <Input placeholder="Reference number" value={selectedCase.referenceNumber ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, referenceNumber: event.target.value })} />
                  <Input placeholder="Applicant / party name" value={selectedCase.applicantName ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, applicantName: event.target.value })} />
                  <Input placeholder="Source department" value={selectedCase.sourceDepartment ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, sourceDepartment: event.target.value })} />
                  <Input type="date" value={selectedCase.receivedDate?.slice(0, 10) ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => setSelectedCase({ ...selectedCase, receivedDate: event.target.value })} />
                  {selectedCase.fields.map((field) => (
                    <Input
                      key={field.id}
                      placeholder={field.label}
                      value={field.value ?? ''}
                      disabled={!selectedCase.canEditCurrentStage}
                      onChange={(event) => updateFieldValue(field.key, event.target.value)}
                    />
                  ))}
                </div>
                <Textarea
                  className="mt-3"
                  placeholder="Description"
                  value={selectedCase.description ?? ''}
                  disabled={!selectedCase.canEditCurrentStage}
                  onChange={(event) => setSelectedCase({ ...selectedCase, description: event.target.value })}
                />
              </div>

              <div className="rounded-md border border-border bg-background p-4">
                <h2 className="text-sm font-semibold">Current Stage Checklist</h2>
                <div className="mt-3 space-y-3">
                  {currentStageItems.map((item) => (
                    <label key={item.id} className="flex items-start gap-3 rounded-md border border-border bg-card p-3 text-sm">
                      <Checkbox
                        checked={item.isCompleted}
                        disabled={!selectedCase.canEditCurrentStage || isSaving}
                        onCheckedChange={(checked) => void toggleChecklist(item.id, checked === true)}
                      />
                      <span className={item.isCompleted ? 'text-muted-foreground line-through' : ''}>{item.text}</span>
                    </label>
                  ))}
                </div>
              </div>

              <div className="rounded-md border border-border bg-background p-4">
                <h2 className="text-sm font-semibold">Documents</h2>
                <div className="mt-3 grid gap-3 md:grid-cols-2">
                  {selectedCase.documents.map((document) => (
                    <div key={document.id} className="rounded-md border border-border bg-card p-3">
                      <div className="flex items-start justify-between gap-2">
                        <div>
                          <div className="text-sm font-medium">{document.name}</div>
                          <div className="mt-1 text-xs text-muted-foreground">{document.requiredFrom}</div>
                        </div>
                        <Badge variant={document.isMandatory ? 'default' : 'outline'}>
                          {document.isMandatory ? 'Required' : 'Optional'}
                        </Badge>
                      </div>
                      <div className="mt-3 space-y-2">
                        {document.fileName ? (
                          <div className="rounded-md border border-border bg-background p-2 text-xs">
                            <div className="font-medium text-foreground">{document.fileName}</div>
                            {document.fileUrl ? (
                              <div className="mt-2 flex flex-wrap gap-2">
                                <a
                                  className="inline-flex items-center gap-1 text-primary hover:underline"
                                  href={document.fileUrl}
                                  target="_blank"
                                  rel="noreferrer"
                                >
                                  <ExternalLink className="h-3 w-3" />
                                  Open uploaded file
                                </a>
                                {isPdfDocument(document) ? (
                                  <button
                                    type="button"
                                    className="inline-flex items-center gap-1 text-primary hover:underline"
                                    onClick={() =>
                                      setPreviewDocumentId((current) => (current === document.id ? null : document.id))
                                    }
                                  >
                                    <Eye className="h-3 w-3" />
                                    {previewDocumentId === document.id ? 'Hide preview' : 'View inline'}
                                  </button>
                                ) : null}
                              </div>
                            ) : null}
                          </div>
                        ) : null}
                        {previewDocumentId === document.id && document.fileUrl && isPdfDocument(document) ? (
                          <ProcedurePdfViewer fileUrl={document.fileUrl} fileName={document.fileName} />
                        ) : null}
                        <Input
                          type="file"
                          accept=".pdf,.doc,.docx,.txt,.rtf,.jpg,.jpeg,.png,.gif,.bmp,.svg,.webp,.ico"
                          disabled={!selectedCase.canEditCurrentStage || isSaving}
                          onChange={(event) => selectDocumentFile(document.id, event.target.files?.[0] ?? null)}
                        />
                        {documentFiles[document.id] ? (
                          <div className="text-xs text-muted-foreground">
                            Selected: {documentFiles[document.id]?.name}
                          </div>
                        ) : null}
                        <Textarea placeholder="Notes" value={document.notes ?? ''} disabled={!selectedCase.canEditCurrentStage} onChange={(event) => updateDocumentNotes(document.id, event.target.value)} />
                        <Button size="sm" variant="outline" className="w-full gap-2" disabled={!selectedCase.canEditCurrentStage || isSaving} onClick={() => void saveDocument(document)}>
                          <FileUp className="h-4 w-4" />
                          {documentFiles[document.id] ? 'Upload document' : 'Save notes'}
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div className="flex flex-col gap-3 rounded-md border border-border bg-background p-4 md:flex-row md:items-center md:justify-between">
                <div className="flex items-start gap-2 text-sm text-muted-foreground">
                  <CheckCircle2 className="mt-0.5 h-4 w-4 text-primary" />
                  <span>All current-stage checklist items must be complete before submission.</span>
                </div>
                <Button className="gap-2" onClick={() => void completeStage()} disabled={!selectedCase.canEditCurrentStage || isSaving || currentStageItems.some((item) => !item.isCompleted)}>
                  <Send className="h-4 w-4" />
                  Submit stage
                </Button>
              </div>
            </div>
          ) : (
            <div className="rounded-md border border-border bg-background p-8 text-center text-sm text-muted-foreground">
              Select or create a case to start work.
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
