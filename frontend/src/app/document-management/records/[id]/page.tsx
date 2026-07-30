'use client';

import React from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import {
  AlertCircle,
  ArrowLeft,
  CalendarClock,
  CheckCircle2,
  Eye,
  ExternalLink,
  FileText,
  GitBranch,
  Loader2,
  MessageSquare,
  ShieldCheck,
  Upload,
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
import {
  CentralDocumentViewerDialog,
  type CentralDocumentViewerFile,
} from '@/components/document-management/CentralDocumentViewerDialog';
import {
  documentManagementService,
  type CentralDocumentRecordDetail,
  type CentralDocumentVersionDownloadFormat,
} from '@/services/document-management.service';

function formatDate(value?: string | null) {
  if (!value) return 'Not set';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Not set';
  return date.toLocaleDateString();
}

function metadataBadgeVariant(status?: string | null) {
  if (status === 'Complete') return 'default';
  if (status === 'Missing template') return 'destructive';
  return 'outline';
}

function formatFileSize(value?: number | null) {
  if (!value) return 'Not recorded';
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
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

export default function CentralDocumentRecordDetailPage() {
  const params = useParams<{ id: string }>();
  const id = decodeURIComponent(params.id);
  const [detail, setDetail] =
    React.useState<CentralDocumentRecordDetail | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isUploadingVersion, setIsUploadingVersion] = React.useState(false);
  const [uploadError, setUploadError] = React.useState<string | null>(null);
  const [viewerFile, setViewerFile] =
    React.useState<CentralDocumentViewerFile | null>(null);
  const versionUploadInputRef = React.useRef<HTMLInputElement | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const data = await documentManagementService.getRecord(id);
        if (mounted) {
          setDetail(data);
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
  }, [id]);

  if (isLoading) {
    return (
      <div className="flex min-h-[320px] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading DMS record
        </div>
      </div>
    );
  }

  if (!detail) {
    return (
      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <CardTitle>DMS record not found</CardTitle>
        </CardHeader>
        <CardContent>
          <Button asChild variant="outline">
            <Link href="/document-management/records">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to register
            </Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  const { record, versions, annotationReviews } = detail;
  const metadataCompleteness = record.metadataCompleteness;
  const accessRetentionCompliance = record.accessRetentionCompliance;
  const recordRepositoryUrl =
    record.repositoryPath && /^https?:\/\//i.test(record.repositoryPath)
      ? record.repositoryPath
      : null;
  const currentVersionRecord =
    versions.find(
      (version) =>
        version.status === 'Current' ||
        version.versionNumber === record.currentVersion
    ) || versions[0];

  const openViewer = (file: CentralDocumentViewerFile | null | undefined) => {
    if (file) {
      setViewerFile(file);
    }
  };

  const generateRendition = async (file: CentralDocumentViewerFile) => {
    if (!file.documentRecordId || !file.versionId) {
      return null;
    }

    const version = await documentManagementService.generateVersionRendition(
      file.documentRecordId,
      file.versionId
    );

    setDetail((current) =>
      current
        ? {
            ...current,
            versions: current.versions.map((item) =>
              item.id === version.id ? version : item
            ),
          }
        : current
    );

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
    const baseName =
      file.fileName?.replace(/\.[^.]+$/, '') ||
      file.title ||
      record.documentReference;

    triggerBlobDownload(
      blob,
      safeDownloadName(`${baseName}-${file.version || 'version'}.${extension}`)
    );
  };

  const uploadEditedVersion = async (file: File) => {
    setIsUploadingVersion(true);
    setUploadError(null);
    try {
      const version = await documentManagementService.uploadVersionFile(
        record.id,
        {
          file,
          status: 'Submitted',
          changeSummary: 'Edited workflow copy uploaded as a new DMS version.',
        }
      );

      setDetail((current) =>
        current
          ? {
              ...current,
              record: {
                ...current.record,
                currentVersion: version.versionNumber,
                versionStatus: version.status,
                repositoryPath:
                  version.repositoryPath || current.record.repositoryPath,
                repositoryStatus: 'Linked',
                annotationStatus: version.renditionPath
                  ? 'PDF preview ready'
                  : current.record.annotationStatus,
              },
              versions: [version, ...current.versions],
            }
          : current
      );
    } catch (error) {
      setUploadError(
        error instanceof Error
          ? error.message
          : 'Unable to upload the edited document version.'
      );
    } finally {
      setIsUploadingVersion(false);
      if (versionUploadInputRef.current) {
        versionUploadInputRef.current.value = '';
      }
    }
  };

  const currentFile: CentralDocumentViewerFile = {
    documentRecordId: record.id,
    versionId: currentVersionRecord?.id,
    fileUploadRecordId: currentVersionRecord?.fileUploadRecordId,
    title: record.title,
    fileName:
      currentVersionRecord?.fileName ||
      record.currentVersion ||
      record.documentReference,
    repositoryPath:
      currentVersionRecord?.repositoryPath || record.repositoryPath,
    renditionPath: currentVersionRecord?.renditionPath,
    externalDocumentUrl: record.externalDocumentUrl,
    contentType: currentVersionRecord?.contentType,
    sourceLabel: record.sourceLabel,
    version: currentVersionRecord?.versionNumber || record.currentVersion,
  };

  return (
    <>
      <CentralDocumentViewerDialog
        file={viewerFile}
        open={Boolean(viewerFile)}
        enableAnnotations={false}
        onGenerateRendition={generateRendition}
        onDownload={downloadVersion}
        onOpenChange={(open) => {
          if (!open) setViewerFile(null);
        }}
      />

      <div className="space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2">
            <Badge variant="outline" className="w-fit">
              {record.sourceLabel}
            </Badge>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="secondary">{record.documentReference}</Badge>
                <Badge variant="outline">{record.sourceModule}</Badge>
              </div>
              <h1 className="mt-3 text-2xl font-semibold tracking-normal sm:text-3xl">
                {record.title}
              </h1>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link href="/document-management/records">
                <ArrowLeft className="mr-2 h-4 w-4" />
                Register
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link href="/document-management">Central DMS</Link>
            </Button>
            <Button variant="outline" onClick={() => openViewer(currentFile)}>
              <Eye className="mr-2 h-4 w-4" />
              View current file
            </Button>
            {recordRepositoryUrl ? (
              <Button asChild variant="outline">
                <a
                  href={recordRepositoryUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <ExternalLink className="mr-2 h-4 w-4" />
                  Open current file
                </a>
              </Button>
            ) : null}
            {record.externalDocumentUrl ? (
              <Button asChild variant="outline">
                <a
                  href={record.externalDocumentUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <ExternalLink className="mr-2 h-4 w-4" />
                  Open external link
                </a>
              </Button>
            ) : null}
          </div>
        </div>

        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {[
            ['Repository', record.repositoryStatus],
            ['Version', record.currentVersion || record.versionStatus],
            ['Annotation', record.annotationStatus],
            ['Retention', record.retentionStatus],
          ].map(([label, value]) => (
            <Card
              key={label}
              className="border-border bg-card text-card-foreground"
            >
              <CardHeader className="space-y-1 pb-2">
                <CardDescription>{label}</CardDescription>
                <CardTitle className="text-xl">{value}</CardTitle>
              </CardHeader>
            </Card>
          ))}
        </div>

        <div className="grid gap-4 xl:grid-cols-[minmax(0,1.1fr)_minmax(340px,0.9fr)]">
          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <div className="flex items-center gap-2">
                <FileText className="h-5 w-5 text-primary" />
                <CardTitle>Source & Repository</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="grid gap-3 md:grid-cols-2">
              {[
                ['Source module', record.sourceModule],
                ['Source entity', record.sourceEntityType || 'Not set'],
                [
                  'Source record',
                  record.sourceRecordReference ||
                    record.sourceRecordId ||
                    'Not recorded',
                ],
                ['Template', record.metadataTemplateCode || 'Not assigned'],
                ['Repository path', record.repositoryPath || 'Not linked'],
                ['External URL', record.externalDocumentUrl || 'Not linked'],
              ].map(([label, value]) => (
                <div
                  key={label}
                  className="rounded-md border bg-background p-3"
                >
                  <div className="text-xs font-medium uppercase text-muted-foreground">
                    {label}
                  </div>
                  <div className="mt-2 break-words text-sm">{value}</div>
                </div>
              ))}
            </CardContent>
          </Card>

          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <div className="flex items-center gap-2">
                <ShieldCheck className="h-5 w-5 text-primary" />
                <CardTitle>Governance</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {[
                ['Access profile', record.accessProfile],
                ['Lifecycle status', record.lifecycleStatus],
                ['Comment status', record.commentStatus],
                ['Effective date', formatDate(record.effectiveDate)],
                ['Expiry date', formatDate(record.expiryDate)],
                ['Review date', formatDate(record.reviewDate)],
                ['Published', formatDate(record.publishedAt)],
              ].map(([label, value]) => (
                <div
                  key={label}
                  className="flex items-start justify-between gap-3 rounded-md border bg-background p-3"
                >
                  <span className="text-sm text-muted-foreground">{label}</span>
                  <span className="text-right text-sm font-medium">
                    {value}
                  </span>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>

        {accessRetentionCompliance ? (
          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                <div className="flex items-center gap-2">
                  <ShieldCheck className="h-5 w-5 text-primary" />
                  <CardTitle>Access & Retention Compliance</CardTitle>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline">
                    Access: {accessRetentionCompliance.accessStatus}
                  </Badge>
                  <Badge
                    variant={
                      accessRetentionCompliance.retentionNeedsAction
                        ? 'destructive'
                        : 'outline'
                    }
                  >
                    Retention: {accessRetentionCompliance.retentionPolicyStatus}
                  </Badge>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                {[
                  [
                    'Access profile',
                    `${record.accessProfile} / ${accessRetentionCompliance.accessRuleCount} rule(s)`,
                  ],
                  [
                    'Retention status',
                    accessRetentionCompliance.currentRetentionStatus,
                  ],
                  [
                    'Review date',
                    formatDate(accessRetentionCompliance.reviewDate),
                  ],
                  [
                    'Expiry date',
                    formatDate(accessRetentionCompliance.expiryDate),
                  ],
                ].map(([label, value]) => (
                  <div
                    key={label}
                    className="rounded-md border bg-background p-3"
                  >
                    <div className="text-xs font-medium uppercase text-muted-foreground">
                      {label}
                    </div>
                    <div className="mt-2 break-words text-sm font-medium">
                      {value}
                    </div>
                  </div>
                ))}
              </div>

              <div className="grid gap-4 xl:grid-cols-2">
                <div className="space-y-3">
                  <div className="text-sm font-medium">
                    Access rule coverage
                  </div>
                  {accessRetentionCompliance.accessRules.length === 0 ? (
                    <div className="rounded-md border bg-background p-3 text-sm text-muted-foreground">
                      No active access rules match this access profile and
                      source module.
                    </div>
                  ) : (
                    accessRetentionCompliance.accessRules.map((rule) => (
                      <div
                        key={`${rule.roleName}-${rule.permissionKey || 'none'}`}
                        className="rounded-md border bg-background p-3"
                      >
                        <div className="text-sm font-medium">
                          {rule.roleName}
                        </div>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {rule.permissionKey || 'No permission key'} /{' '}
                          {rule.module || 'All modules'}
                        </p>
                        <div className="mt-3 flex flex-wrap gap-2">
                          {[
                            ['View', rule.canView],
                            ['Upload', rule.canUpload],
                            ['Annotate', rule.canAnnotate],
                            ['Approve', rule.canApprove],
                            ['Archive', rule.canArchive],
                          ].map(([label, enabled]) => (
                            <Badge
                              key={label as string}
                              variant={enabled ? 'default' : 'outline'}
                            >
                              {label}
                            </Badge>
                          ))}
                        </div>
                      </div>
                    ))
                  )}
                </div>

                <div className="space-y-3">
                  <div className="text-sm font-medium">Retention policy</div>
                  {accessRetentionCompliance.retentionPolicy ? (
                    <div className="rounded-md border bg-background p-3">
                      <div className="text-sm font-medium">
                        {accessRetentionCompliance.retentionPolicy.policyCode} /{' '}
                        {accessRetentionCompliance.retentionPolicy.name}
                      </div>
                      <div className="mt-3 grid gap-2 text-sm text-muted-foreground">
                        <div>
                          Duration:{' '}
                          {
                            accessRetentionCompliance.retentionPolicy
                              .retentionDays
                          }{' '}
                          days
                        </div>
                        <div>
                          Legal hold review:{' '}
                          {accessRetentionCompliance.retentionPolicy
                            .requiresLegalHoldReview
                            ? 'Required'
                            : 'Not required'}
                        </div>
                        <div>
                          Archive:{' '}
                          {accessRetentionCompliance.retentionPolicy
                            .allowArchive
                            ? 'Allowed'
                            : 'Blocked'}{' '}
                          / Destruction:{' '}
                          {accessRetentionCompliance.retentionPolicy
                            .allowDestruction
                            ? 'Allowed'
                            : 'Blocked'}
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="rounded-md border bg-background p-3 text-sm text-muted-foreground">
                      No active retention policy matched this source module and
                      document type.
                    </div>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>
        ) : null}

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div className="flex items-center gap-2">
                <FileText className="h-5 w-5 text-primary" />
                <CardTitle>Metadata Completeness</CardTitle>
              </div>
              <Badge
                variant={metadataBadgeVariant(metadataCompleteness.status)}
              >
                {metadataCompleteness.status} {metadataCompleteness.percentage}%
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 md:grid-cols-3">
              {[
                [
                  'Template',
                  metadataCompleteness.templateCode ||
                    record.metadataTemplateCode ||
                    'Missing template',
                ],
                [
                  'Document type',
                  metadataCompleteness.documentType || 'Not matched',
                ],
                [
                  'Captured fields',
                  `${metadataCompleteness.capturedCount} of ${metadataCompleteness.requiredCount}`,
                ],
              ].map(([label, value]) => (
                <div
                  key={label}
                  className="rounded-md border bg-background p-3"
                >
                  <div className="text-xs font-medium uppercase text-muted-foreground">
                    {label}
                  </div>
                  <div className="mt-2 break-words text-sm font-medium">
                    {value}
                  </div>
                </div>
              ))}
            </div>

            <div className="h-2 overflow-hidden rounded-full bg-muted">
              <div
                className="h-full bg-primary transition-all"
                style={{
                  width: `${Math.max(
                    0,
                    Math.min(100, metadataCompleteness.percentage)
                  )}%`,
                }}
              />
            </div>

            {metadataCompleteness.items.length === 0 ? (
              <div className="rounded-md border bg-background p-4">
                <div className="flex items-start gap-2">
                  <AlertCircle className="mt-0.5 h-4 w-4 text-destructive" />
                  <div>
                    <div className="text-sm font-medium">
                      No active template requirements matched
                    </div>
                  </div>
                </div>
              </div>
            ) : (
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                {metadataCompleteness.items.map((item) => (
                  <div
                    key={item.field}
                    className="rounded-md border bg-background p-3"
                  >
                    <div className="flex items-start gap-2">
                      {item.captured ? (
                        <CheckCircle2 className="mt-0.5 h-4 w-4 text-primary" />
                      ) : (
                        <AlertCircle className="mt-0.5 h-4 w-4 text-destructive" />
                      )}
                      <div className="min-w-0">
                        <div className="text-sm font-medium">{item.field}</div>
                        <p className="mt-1 break-words text-xs text-muted-foreground">
                          {item.captured ? item.value || 'Captured' : 'Missing'}
                        </p>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <div className="grid gap-4 xl:grid-cols-2">
          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-2">
                  <GitBranch className="h-5 w-5 text-primary" />
                  <CardTitle>Version History</CardTitle>
                </div>
                <div>
                  <input
                    ref={versionUploadInputRef}
                    type="file"
                    className="hidden"
                    onChange={(event) => {
                      const file = event.target.files?.[0];
                      if (file) {
                        void uploadEditedVersion(file);
                      }
                    }}
                  />
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    disabled={isUploadingVersion}
                    onClick={() => versionUploadInputRef.current?.click()}
                  >
                    {isUploadingVersion ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Upload className="mr-2 h-4 w-4" />
                    )}
                    Upload new version
                  </Button>
                </div>
              </div>
              {uploadError ? (
                <p className="text-sm text-destructive">{uploadError}</p>
              ) : null}
            </CardHeader>
            <CardContent className="space-y-3">
              {versions.length === 0 ? (
                <div className="rounded-md border bg-background p-4 text-sm text-muted-foreground">
                  No version history recorded.
                </div>
              ) : null}
              {versions.map((version) => {
                const isCurrent =
                  version.status === 'Current' ||
                  record.currentVersion === version.versionNumber;
                const versionUrl =
                  version.repositoryPath &&
                  /^https?:\/\//i.test(version.repositoryPath)
                    ? version.repositoryPath
                    : null;

                return (
                  <div
                    key={version.id}
                    className="rounded-md border bg-background p-4"
                  >
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <div className="font-medium">
                            {version.versionNumber}
                          </div>
                          {isCurrent ? <Badge>Current</Badge> : null}
                        </div>
                        <p className="mt-1 text-sm text-muted-foreground">
                          {version.fileName || 'No file name recorded'} /{' '}
                          {formatFileSize(version.fileSize)}
                        </p>
                      </div>
                      <Badge variant="outline">{version.status}</Badge>
                    </div>

                    <p className="mt-3 break-words text-xs text-muted-foreground">
                      {version.repositoryPath || 'No repository path linked'}
                    </p>
                    {version.renditionPath ? (
                      <p className="mt-2 break-words text-xs text-muted-foreground">
                        PDF rendition: {version.renditionPath}
                      </p>
                    ) : null}
                    {version.changeSummary ? (
                      <p className="mt-3 text-sm text-muted-foreground">
                        {version.changeSummary}
                      </p>
                    ) : null}

                    <div className="mt-4 flex flex-wrap gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() =>
                          openViewer({
                            documentRecordId: record.id,
                            versionId: version.id,
                            fileUploadRecordId: version.fileUploadRecordId,
                            title: `${record.title} / ${version.versionNumber}`,
                            fileName: version.fileName || record.title,
                            repositoryPath:
                              version.repositoryPath || record.repositoryPath,
                            renditionPath: version.renditionPath,
                            externalDocumentUrl: record.externalDocumentUrl,
                            contentType: version.contentType,
                            sourceLabel: record.sourceLabel,
                            version: version.versionNumber,
                          })
                        }
                      >
                        <Eye className="mr-2 h-4 w-4" />
                        View
                      </Button>
                      {versionUrl ? (
                        <Button asChild size="sm" variant="outline">
                          <a
                            href={versionUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                          >
                            <ExternalLink className="mr-2 h-4 w-4" />
                            Open file
                          </a>
                        </Button>
                      ) : null}
                    </div>
                  </div>
                );
              })}
            </CardContent>
          </Card>

          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <div className="flex items-center gap-2">
                <MessageSquare className="h-5 w-5 text-primary" />
                <CardTitle>Viewer Review History</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {annotationReviews.length === 0 ? (
                <div className="rounded-md border bg-background p-4">
                  <div className="text-sm font-medium">
                    No annotation review recorded
                  </div>
                </div>
              ) : null}
              {annotationReviews.map((review) => {
                const linkedVersion = versions.find(
                  (version) => version.id === review.documentVersionId
                );

                return (
                  <div
                    key={review.id}
                    className="rounded-md border bg-background p-4"
                  >
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <div className="font-medium">{review.reviewTitle}</div>
                        <p className="mt-1 text-sm text-muted-foreground">
                          {review.syncfusionAnnotationStatus}
                        </p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          Version:{' '}
                          {linkedVersion
                            ? `${linkedVersion.versionNumber} / ${linkedVersion.status}`
                            : 'Not linked'}
                        </p>
                      </div>
                      <Badge variant="outline">{review.status}</Badge>
                    </div>
                    <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
                      <CalendarClock className="h-3.5 w-3.5" />
                      Due {formatDate(review.dueDate)}
                    </div>
                    {review.reviewNotes ? (
                      <p className="mt-3 text-sm text-muted-foreground">
                        {review.reviewNotes}
                      </p>
                    ) : null}
                  </div>
                );
              })}
            </CardContent>
          </Card>
        </div>

        {record.notes ? (
          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <CardTitle>Document Control Notes</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm leading-6 text-muted-foreground">
                {record.notes}
              </p>
            </CardContent>
          </Card>
        ) : null}
      </div>
    </>
  );
}
