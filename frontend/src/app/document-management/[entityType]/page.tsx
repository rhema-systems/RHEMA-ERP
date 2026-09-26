'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  ArrowRight,
  BookTemplate,
  CheckCircle2,
  Clock,
  Eye,
  FileText,
  GitBranch,
  Loader2,
  RotateCcw,
  ShieldCheck,
  Workflow,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Pagination } from '@/components/ui/pagination';
import { usePaginatedItems } from '@/hooks/use-paginated-items';
import {
  CentralDocumentViewerDialog,
  type CentralDocumentViewerFile,
} from '@/components/document-management/CentralDocumentViewerDialog';
import {
  documentManagementService,
  type CentralDocumentIntegrationQueueItem,
  type CentralDocumentMetadataTemplate,
  type CentralDocumentRecord,
  type CentralDocumentVersionDownloadFormat,
  type CentralDocumentVersionQueueItem,
  type CentralDocumentWorkspace,
} from '@/services/document-management.service';

const iconMap: Record<string, React.ComponentType<{ className?: string }>> = {
  BookTemplate,
  FileText,
  GitBranch,
  ShieldCheck,
  Workflow,
};

function viewerFileFromRecord(
  record: CentralDocumentRecord
): CentralDocumentViewerFile {
  return {
    title: record.title,
    fileName: record.currentVersion || record.documentReference,
    repositoryPath: record.repositoryPath,
    externalDocumentUrl: record.externalDocumentUrl,
    sourceLabel: record.sourceLabel,
    version: record.currentVersion,
  };
}

function safeDownloadName(value: string) {
  return value.replace(/[\\/:*?"<>|]+/g, '-').trim() || 'dms-document';
}

function triggerBlobDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export default function DocumentManagementWorkspacePage() {
  const params = useParams<{ entityType: string }>();
  const router = useRouter();
  const entityType = decodeURIComponent(params.entityType);
  const isRetiredAnnotationWorkspace =
    entityType.toLowerCase() === 'centraldocumentannotation';
  const [workspace, setWorkspace] =
    React.useState<CentralDocumentWorkspace | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [queueItems, setQueueItems] = React.useState<
    CentralDocumentIntegrationQueueItem[]
  >([]);
  const [versionItems, setVersionItems] = React.useState<
    CentralDocumentVersionQueueItem[]
  >([]);
  const [governanceItems, setGovernanceItems] = React.useState<
    CentralDocumentRecord[]
  >([]);
  const [metadataTemplateItems, setMetadataTemplateItems] = React.useState<
    CentralDocumentMetadataTemplate[]
  >([]);
  const [viewerFile, setViewerFile] =
    React.useState<CentralDocumentViewerFile | null>(null);
  const [isQueueLoading, setIsQueueLoading] = React.useState(false);
  const [versionActionId, setVersionActionId] = React.useState<string | null>(
    null
  );
  const [uploadActionId, setUploadActionId] = React.useState<string | null>(
    null
  );
  const versionPages = usePaginatedItems(versionItems, 10);
  const governancePages = usePaginatedItems(governanceItems, 10);
  const templatePages = usePaginatedItems(metadataTemplateItems, 10);
  const integrationPages = usePaginatedItems(queueItems, 10);

  const refreshQueues = React.useCallback(async () => {
    setIsQueueLoading(true);
    try {
      const [integrationQueue, versionQueue, records, templates] =
        await Promise.all([
          documentManagementService.getIntegrationQueue(),
          documentManagementService.getVersionQueue(),
          documentManagementService.getRecords(),
          documentManagementService.getMetadataTemplates(),
        ]);
      setQueueItems(integrationQueue);
      setVersionItems(versionQueue);
      setGovernanceItems(
        records.filter(
          (record) =>
            record.retentionStatus !== 'Current' ||
            record.lifecycleStatus === 'Legal hold' ||
            record.lifecycleStatus === 'Archive due' ||
            Boolean(record.reviewDate)
        )
      );
      setMetadataTemplateItems(templates);
    } finally {
      setIsQueueLoading(false);
    }
  }, []);

  const publishVersion = async (
    item: CentralDocumentVersionQueueItem,
    status: string
  ) => {
    setVersionActionId(`${item.id}-${status}`);
    try {
      await documentManagementService.updateVersionStatus(
        item.documentRecordId,
        item.id,
        { status }
      );
      await refreshQueues();
    } finally {
      setVersionActionId(null);
    }
  };

  const uploadVersionFile = async (
    recordId: string,
    file: File | undefined
  ) => {
    if (!file) return;
    setUploadActionId(recordId);
    try {
      await documentManagementService.uploadVersionFile(recordId, {
        file,
        status: 'Submitted',
        changeSummary: 'Uploaded from DMS version workspace',
      });
      await refreshQueues();
    } finally {
      setUploadActionId(null);
    }
  };

  const generateRendition = async (file: CentralDocumentViewerFile) => {
    if (!file.documentRecordId || !file.versionId) {
      return null;
    }

    const version = await documentManagementService.generateVersionRendition(
      file.documentRecordId,
      file.versionId,
      Boolean(file.renditionPath)
    );
    await refreshQueues();

    return {
      ...file,
      renditionPath: version.renditionPath,
      repositoryPath: version.repositoryPath || file.repositoryPath,
      contentType: version.contentType || file.contentType,
      fileName: version.fileName || file.fileName,
    };
  };

  const downloadVersion = async (
    file: CentralDocumentViewerFile,
    format: CentralDocumentVersionDownloadFormat
  ) => {
    if (!file.documentRecordId || !file.versionId) {
      throw new Error('This DMS version cannot be downloaded.');
    }

    const blob = await documentManagementService.downloadVersionFile(
      file.documentRecordId,
      file.versionId,
      format
    );
    const extension = format === 'pdf' ? 'pdf' : 'docx';
    const baseName = file.fileName?.replace(/\.[^.]+$/, '') || file.title;
    triggerBlobDownload(
      blob,
      safeDownloadName(`${baseName}-${file.version || 'version'}.${extension}`)
    );
  };

  const saveAnnotations = async (
    file: CentralDocumentViewerFile,
    annotationStateJson: string | null,
    annotatedPdfBlob: Blob | null
  ) => {
    if (!file.documentRecordId || !file.versionId) {
      throw new Error('This DMS version cannot save annotations.');
    }

    const sourcePdf =
      annotatedPdfBlob ||
      (await documentManagementService.downloadVersionFile(
        file.documentRecordId,
        file.versionId,
        'pdf'
      ));
    const baseName = file.fileName?.replace(/\.[^.]+$/, '') || file.title;
    const annotatedFile = new File(
      [sourcePdf],
      safeDownloadName(`${baseName}-annotated.pdf`),
      { type: 'application/pdf' }
    );
    const version = await documentManagementService.uploadVersionFile(
      file.documentRecordId,
      {
        file: annotatedFile,
        status: 'Current',
        changeSummary:
          'PDF annotations, comments, and signatures saved from version control.',
      }
    );

    await documentManagementService.addAnnotationReview(file.documentRecordId, {
      documentVersionId: version.id,
      reviewTitle: `${file.title} annotation save`,
      status: 'Open',
      syncfusionAnnotationStatus: 'Annotations saved',
      reviewNotes:
        'Annotations, comments, and signature marks were saved from the PDF viewer.',
      annotationStateJson: annotationStateJson || '{}',
    });
    await refreshQueues();

    return {
      ...file,
      versionId: version.id,
      fileUploadRecordId: version.fileUploadRecordId,
      fileName: version.fileName || file.fileName,
      repositoryPath: version.repositoryPath || file.repositoryPath,
      renditionPath: version.renditionPath || version.repositoryPath,
      contentType: version.contentType || 'application/pdf',
      version: version.versionNumber,
      annotationStateJson: annotationStateJson || '{}',
    };
  };

  const updateGovernanceStatus = async (
    record: CentralDocumentRecord,
    status: 'Current' | 'Legal hold' | 'Archived'
  ) => {
    await documentManagementService.updateLifecycleControls(record.id, {
      retentionStatus:
        status === 'Current' ? 'Current' : record.retentionStatus,
      lifecycleStatus: status,
    });
    await refreshQueues();
  };

  const pendingQueueCount = queueItems.filter(
    (item) => item.status === 'Pending Review'
  ).length;
  const pendingVersionCount = versionItems.filter(
    (item) => item.status === 'Pending Publication'
  ).length;
  const pendingGovernanceCount = governanceItems.filter(
    (item) => item.retentionStatus !== 'Current'
  ).length;
  const draftTemplateCount = metadataTemplateItems.filter(
    (item) => !item.isActive
  ).length;

  React.useEffect(() => {
    if (isRetiredAnnotationWorkspace) {
      router.replace('/document-management/records');
      return;
    }

    let mounted = true;

    const load = async () => {
      try {
        const data = await documentManagementService.getWorkspace(entityType);
        if (mounted) {
          setWorkspace(data);
        }
        await refreshQueues();
      } catch {
        if (mounted) {
          setWorkspace(null);
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, [entityType, isRetiredAnnotationWorkspace, refreshQueues, router]);

  if (isRetiredAnnotationWorkspace) {
    return (
      <div className="flex min-h-[320px] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Opening Document Register
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex min-h-[320px] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading DMS workspace
        </div>
      </div>
    );
  }

  if (!workspace) {
    return (
      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <CardTitle>DMS workspace not found</CardTitle>
          <CardDescription>
            The requested Central Document Management workspace could not be
            loaded.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild variant="outline">
            <Link href="/document-management">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to Central DMS
            </Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  const Icon = iconMap[workspace.workspace.icon] || FileText;

  return (
    <>
      <CentralDocumentViewerDialog
        file={viewerFile}
        open={Boolean(viewerFile)}
        onGenerateRendition={generateRendition}
        onDownload={downloadVersion}
        onSaveAnnotations={saveAnnotations}
        onOpenChange={(open) => {
          if (!open) setViewerFile(null);
        }}
      />

      <div className="space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2">
            <div>
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-md border bg-muted">
                  <Icon className="h-5 w-5 text-primary" />
                </div>
                <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                  {workspace.workspace.title}
                </h1>
              </div>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link href="/document-management">
                <ArrowLeft className="mr-2 h-4 w-4" />
                Central DMS
              </Link>
            </Button>
          </div>
        </div>

        {workspace.workspace.entityType === 'CentralDocumentVersion' ? (
          <section className="space-y-3">
            <header>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-base font-semibold">
                    Version Publication Queue
                  </h2>
                </div>
                <Badge variant="secondary" className="w-fit">
                  {pendingVersionCount} pending
                </Badge>
              </div>
            </header>
            <div className="space-y-3">
              {versionItems.length > versionPages.pageSize ? (
                <Pagination
                  currentPage={versionPages.currentPage}
                  totalPages={versionPages.totalPages}
                  totalItems={versionPages.totalItems}
                  pageSize={versionPages.pageSize}
                  onPageChange={versionPages.setCurrentPage}
                />
              ) : null}
              <div className="overflow-x-auto">
                <table className="w-full min-w-[920px] text-sm">
                  <thead className="border-b bg-muted/40 text-left text-muted-foreground">
                    <tr>
                      <th className="px-3 py-2 font-medium">Document</th>
                      <th className="px-3 py-2 font-medium">Version</th>
                      <th className="px-3 py-2 font-medium">Status</th>
                      <th className="px-3 py-2 font-medium">Reason</th>
                      <th className="px-3 py-2 text-right font-medium">
                        Actions
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {versionPages.items.map((item) => {
                      const isPending =
                        item.status === 'Pending Publication' ||
                        item.status === 'Submitted' ||
                        item.status === 'Published';
                      const versionFile: CentralDocumentViewerFile = {
                        documentRecordId: item.documentRecordId,
                        versionId: item.version.id,
                        fileUploadRecordId: item.version.fileUploadRecordId,
                        title: `${item.title} / ${item.requestedVersion}`,
                        fileName: item.version.fileName || item.title,
                        repositoryPath:
                          item.version.repositoryPath ||
                          item.document.repositoryPath,
                        renditionPath: item.version.renditionPath,
                        externalDocumentUrl: item.document.externalDocumentUrl,
                        contentType: item.version.contentType,
                        sourceLabel: item.sourceLabel,
                        version: item.requestedVersion,
                      };

                      return (
                        <tr key={item.id}>
                          <td className="px-3 py-2.5">
                            <div className="font-medium">{item.title}</div>
                            <div className="text-xs text-muted-foreground">
                              {item.documentReference}
                            </div>
                          </td>
                          <td className="px-3 py-2">
                            {item.currentVersion} to {item.requestedVersion}
                          </td>
                          <td className="px-3 py-2">
                            <Badge
                              variant={isPending ? 'secondary' : 'default'}
                            >
                              {item.status}
                            </Badge>
                          </td>
                          <td className="max-w-[200px] px-3 py-2 text-muted-foreground">
                            {item.reason}
                          </td>
                          <td className="px-3 py-2">
                            <div className="flex flex-wrap justify-end gap-2">
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => setViewerFile(versionFile)}
                              >
                                <Eye className="mr-2 h-4 w-4" />
                                View Document
                              </Button>
                              {item.workflow &&
                              item.workflow.status !== 'Completed' ? (
                                <Button asChild size="sm" variant="outline">
                                  <Link href="/workflow/inbox">
                                    <Workflow className="mr-2 h-4 w-4" />
                                    Workflow task
                                  </Link>
                                </Button>
                              ) : null}
                              <label className="inline-flex h-9 cursor-pointer items-center justify-center rounded-md border border-input bg-background px-3 text-sm font-medium hover:bg-accent hover:text-accent-foreground">
                                {uploadActionId === item.documentRecordId ? (
                                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                ) : null}
                                Upload edited Word
                                <input
                                  type="file"
                                  accept=".doc,.docx,.rtf,.pdf"
                                  className="sr-only"
                                  disabled={
                                    uploadActionId === item.documentRecordId
                                  }
                                  onChange={(event) => {
                                    void uploadVersionFile(
                                      item.documentRecordId,
                                      event.target.files?.[0]
                                    );
                                    event.target.value = '';
                                  }}
                                />
                              </label>
                              <Button
                                size="sm"
                                onClick={() =>
                                  void publishVersion(item, 'Current')
                                }
                                disabled={
                                  !isPending ||
                                  Boolean(
                                    item.workflow &&
                                      item.workflow.status !== 'Completed'
                                  )
                                }
                              >
                                {versionActionId === `${item.id}-Current` ? (
                                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                ) : (
                                  <CheckCircle2 className="mr-2 h-4 w-4" />
                                )}
                                Final publish
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() =>
                                  void publishVersion(item, 'Returned')
                                }
                                disabled={!isPending}
                              >
                                {versionActionId === `${item.id}-Returned` ? (
                                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                ) : (
                                  <RotateCcw className="mr-2 h-4 w-4" />
                                )}
                                Return
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() =>
                                  void publishVersion(item, 'Superseded')
                                }
                                disabled={!isPending}
                              >
                                Supersede
                              </Button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              {isQueueLoading ? (
                <div className="flex items-center gap-2 rounded-md border bg-background p-3 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Refreshing version queue
                </div>
              ) : null}
              <Button asChild variant="outline">
                <Link href="/document-management/records">
                  Open Document Register
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              </Button>
            </div>
          </section>
        ) : null}

        {workspace.workspace.entityType === 'CentralDocumentGovernance' ? (
          <section className="space-y-3">
            <header>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-base font-semibold">
                    Access & Retention Review Queue
                  </h2>
                </div>
                <Badge variant="secondary" className="w-fit">
                  {pendingGovernanceCount} pending
                </Badge>
              </div>
            </header>
            <div className="space-y-3">
              {governanceItems.length > governancePages.pageSize ? (
                <Pagination
                  currentPage={governancePages.currentPage}
                  totalPages={governancePages.totalPages}
                  totalItems={governancePages.totalItems}
                  pageSize={governancePages.pageSize}
                  onPageChange={governancePages.setCurrentPage}
                />
              ) : null}
              <div className="overflow-x-auto">
                <table className="w-full min-w-[760px] text-sm">
                  <thead className="border-b bg-muted/40 text-left text-muted-foreground">
                    <tr>
                      <th className="px-3 py-2 font-medium">Document</th>
                      <th className="px-3 py-2 font-medium">Access</th>
                      <th className="px-3 py-2 font-medium">Review date</th>
                      <th className="px-3 py-2 font-medium">Retention</th>
                      <th className="px-3 py-2 text-right font-medium">
                        Actions
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {governancePages.items.map((item) => {
                      const isPending = item.retentionStatus !== 'Current';

                      return (
                        <tr key={item.id}>
                          <td className="px-3 py-2.5">
                            <div className="font-medium">{item.title}</div>
                            <div className="text-xs text-muted-foreground">
                              {item.documentReference}
                            </div>
                          </td>
                          <td className="px-3 py-2">{item.accessProfile}</td>
                          <td className="px-3 py-2">
                            {item.reviewDate
                              ? new Date(item.reviewDate).toLocaleDateString()
                              : 'Not set'}
                          </td>
                          <td className="px-3 py-2">
                            <Badge
                              variant={isPending ? 'secondary' : 'default'}
                            >
                              {item.retentionStatus}
                            </Badge>
                          </td>
                          <td className="px-3 py-2">
                            <div className="flex flex-wrap justify-end gap-2">
                              <Button
                                size="sm"
                                onClick={() =>
                                  void updateGovernanceStatus(item, 'Current')
                                }
                                disabled={!isPending}
                              >
                                <CheckCircle2 className="mr-2 h-4 w-4" />
                                Approve
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() =>
                                  void updateGovernanceStatus(
                                    item,
                                    'Legal hold'
                                  )
                                }
                                disabled={!isPending}
                              >
                                Legal Hold
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() =>
                                  void updateGovernanceStatus(item, 'Archived')
                                }
                                disabled={!isPending}
                              >
                                Archive
                              </Button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              <Button asChild variant="outline">
                <Link href="/administration/document-management/access-retention">
                  Manage Access & Retention Setup
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              </Button>
            </div>
          </section>
        ) : null}

        {workspace.workspace.entityType ===
        'CentralDocumentMetadataTemplate' ? (
          <section className="space-y-3">
            <header>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-base font-semibold">
                    Metadata Template Approval Queue
                  </h2>
                </div>
                <Badge variant="secondary" className="w-fit">
                  {draftTemplateCount} drafts
                </Badge>
              </div>
            </header>
            <div className="space-y-3">
              {metadataTemplateItems.length > templatePages.pageSize ? (
                <Pagination
                  currentPage={templatePages.currentPage}
                  totalPages={templatePages.totalPages}
                  totalItems={templatePages.totalItems}
                  pageSize={templatePages.pageSize}
                  onPageChange={templatePages.setCurrentPage}
                />
              ) : null}
              <div className="overflow-x-auto">
                <table className="w-full min-w-[620px] text-sm">
                  <thead className="border-b bg-muted/40 text-left text-muted-foreground">
                    <tr>
                      <th className="px-3 py-2 font-medium">Template</th>
                      <th className="px-3 py-2 font-medium">Module</th>
                      <th className="px-3 py-2 font-medium">Required fields</th>
                      <th className="px-3 py-2 font-medium">Status</th>
                      <th className="px-3 py-2 text-right font-medium">
                        Action
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {templatePages.items.map((item) => {
                      const isDraft = !item.isActive;

                      return (
                        <tr key={item.id || item.templateCode}>
                          <td className="px-3 py-2.5">
                            <div className="font-medium">
                              {item.documentType}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {item.templateCode}
                            </div>
                          </td>
                          <td className="px-3 py-2">{item.module}</td>
                          <td className="px-3 py-2 tabular-nums">
                            {item.requiredFields.length}
                          </td>
                          <td className="px-3 py-2">
                            <Badge variant={isDraft ? 'secondary' : 'default'}>
                              {item.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                          </td>
                          <td className="px-3 py-2">
                            <div className="flex justify-end">
                              <Button asChild size="sm" variant="outline">
                                <Link href="/administration/document-management/metadata-templates">
                                  Open Setup
                                </Link>
                              </Button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              <Button asChild variant="outline">
                <Link href="/administration/document-management/metadata-templates">
                  Open Metadata Setup
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              </Button>
            </div>
          </section>
        ) : null}

        {workspace.workspace.entityType ===
        'CentralDocumentIntegrationQueue' ? (
          <section className="space-y-3">
            <header>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-base font-semibold">
                    Incoming Module Packages
                  </h2>
                </div>
                <Badge variant="secondary" className="w-fit">
                  {pendingQueueCount} pending
                </Badge>
              </div>
            </header>
            <div className="space-y-3">
              {queueItems.length > integrationPages.pageSize ? (
                <Pagination
                  currentPage={integrationPages.currentPage}
                  totalPages={integrationPages.totalPages}
                  totalItems={integrationPages.totalItems}
                  pageSize={integrationPages.pageSize}
                  onPageChange={integrationPages.setCurrentPage}
                />
              ) : null}
              <div className="overflow-x-auto">
                <table className="w-full min-w-[780px] text-sm">
                  <thead className="border-b bg-muted/40 text-left text-muted-foreground">
                    <tr>
                      <th className="px-3 py-2 font-medium">Document</th>
                      <th className="px-3 py-2 font-medium">Module</th>
                      <th className="px-3 py-2 font-medium">Received</th>
                      <th className="px-3 py-2 font-medium">Status</th>
                      <th className="px-3 py-2 text-right font-medium">
                        Actions
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {integrationPages.items.map((item) => {
                      const isPending = item.status === 'Pending Review';

                      return (
                        <tr key={item.id}>
                          <td className="px-3 py-2.5">
                            <div className="font-medium">
                              {item.documentType}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              {item.sourceRecord}
                            </div>
                          </td>
                          <td className="px-3 py-2">{item.sourceModule}</td>
                          <td className="px-3 py-2">
                            {new Date(item.receivedAt).toLocaleDateString()}
                          </td>
                          <td className="px-3 py-2">
                            <Badge
                              variant={isPending ? 'secondary' : 'default'}
                            >
                              {item.status}
                            </Badge>
                          </td>
                          <td className="px-3 py-2">
                            <div className="flex flex-wrap justify-end gap-2">
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() =>
                                  setViewerFile(
                                    viewerFileFromRecord(item.document)
                                  )
                                }
                              >
                                <Eye className="mr-2 h-4 w-4" />
                                View Document
                              </Button>
                              <Button asChild size="sm" variant="outline">
                                <Link
                                  href={`/document-management/records/${item.document.id}`}
                                >
                                  Open Record
                                </Link>
                              </Button>
                              <Button
                                size="sm"
                                onClick={() => void refreshQueues()}
                                disabled={!isPending}
                              >
                                <CheckCircle2 className="mr-2 h-4 w-4" />
                                Refresh
                              </Button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              <div className="flex flex-wrap gap-2 pt-2">
                <Button asChild variant="outline">
                  <Link href="/document-management/records">
                    Open Document Register
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Link>
                </Button>
                <Button asChild variant="outline">
                  <Link href="/administration/document-management/metadata-templates">
                    Manage Metadata Templates
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Link>
                </Button>
              </div>
            </div>
          </section>
        ) : null}
      </div>
    </>
  );
}
