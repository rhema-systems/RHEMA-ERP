'use client';

import React from 'react';
import { Eye, FileText, Loader2 } from 'lucide-react';

import { CentralDocumentViewerDialog } from '@/components/document-management/CentralDocumentViewerDialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  externalEstateDocumentsService,
  type ExternalEstateDocument,
} from '@/services/external-estate-documents.service';

function formatDate(value?: string | null) {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'Not recorded' : date.toLocaleDateString();
}

export default function ExternalEstateDocumentsPage() {
  const [documents, setDocuments] = React.useState<ExternalEstateDocument[]>([]);
  const [selectedDocument, setSelectedDocument] =
    React.useState<ExternalEstateDocument | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const loadDocuments = async () => {
      setError(null);
      try {
        const data = await externalEstateDocumentsService.getMyDocuments();
        if (mounted) {
          setDocuments(data);
        }
      } catch {
        if (mounted) {
          setError('Could not load your Estate documents.');
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadDocuments();

    return () => {
      mounted = false;
    };
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">
            Estate Documents
          </h1>
        </div>
        {isLoading ? (
          <Badge variant="outline" className="w-fit">
            <Loader2 className="mr-2 h-3 w-3 animate-spin" />
            Loading
          </Badge>
        ) : (
          <Badge variant="outline" className="w-fit">
            {documents.length} document{documents.length === 1 ? '' : 's'}
          </Badge>
        )}
      </div>

      {error ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FileText className="h-5 w-5 text-blue-600" />
            Dispatched Letters
          </CardTitle>
        </CardHeader>
        <CardContent>
          {documents.length === 0 && !isLoading ? (
            <div className="rounded-md border border-dashed p-8 text-center text-sm text-slate-500">
              No dispatched Estate documents are available for your portal
              account yet.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[760px] text-left text-sm">
                <thead className="border-b text-xs uppercase text-slate-500">
                  <tr>
                    <th className="px-3 py-2 font-medium">Document</th>
                    <th className="px-3 py-2 font-medium">Reference</th>
                    <th className="px-3 py-2 font-medium">Dispatch</th>
                    <th className="px-3 py-2 font-medium">Status</th>
                    <th className="px-3 py-2 font-medium"></th>
                  </tr>
                </thead>
                <tbody>
                  {documents.map((document) => (
                    <tr key={document.id} className="border-b">
                      <td className="px-3 py-3 align-top">
                        <div className="font-medium text-slate-900">
                          {document.title}
                        </div>
                        <div className="mt-1 text-xs text-slate-500">
                          {document.sourceLabel}
                        </div>
                      </td>
                      <td className="px-3 py-3 align-top">
                        <div>{document.documentReference}</div>
                        <div className="mt-1 text-xs text-slate-500">
                          {document.sourceRecordReference || document.sourceEntityType}
                        </div>
                      </td>
                      <td className="px-3 py-3 align-top">
                        <div>{document.dispatchChannel || 'Dispatched'}</div>
                        <div className="mt-1 text-xs text-slate-500">
                          {document.dispatchReference || formatDate(document.dispatchedAt || document.publishedAt)}
                        </div>
                      </td>
                      <td className="px-3 py-3 align-top">
                        <div className="flex flex-wrap gap-1">
                          <Badge variant="secondary">
                            {document.lifecycleStatus}
                          </Badge>
                          {document.version ? (
                            <Badge variant="outline">{document.version}</Badge>
                          ) : null}
                        </div>
                      </td>
                      <td className="px-3 py-3 align-top">
                        <Button
                          size="sm"
                          variant="outline"
                          className="gap-2"
                          onClick={() => setSelectedDocument(document)}
                        >
                          <Eye className="h-4 w-4" />
                          View
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <CentralDocumentViewerDialog
        open={selectedDocument !== null}
        onOpenChange={(open) => {
          if (!open) {
            setSelectedDocument(null);
          }
        }}
        enableAnnotations={false}
        file={
          selectedDocument
            ? {
                title: selectedDocument.title,
                fileName: selectedDocument.fileName,
                repositoryPath: selectedDocument.repositoryPath,
                renditionPath: selectedDocument.renditionPath,
                contentType: selectedDocument.contentType,
                sourceLabel: selectedDocument.sourceLabel,
                version: selectedDocument.version,
              }
            : null
        }
      />
    </div>
  );
}
