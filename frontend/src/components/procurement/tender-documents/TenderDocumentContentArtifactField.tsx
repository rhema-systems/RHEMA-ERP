'use client';

import React, { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { isTenderDocumentContentArtifactApproved } from '@/lib/procurement-tender-document';
import { procurementTenderDocumentService } from '@/services/procurement-tender-document.service';
import type { ProcurementTenderDocumentContentArtifactOption } from '@/types/procurement-tender-document';

interface TenderDocumentContentArtifactFieldProps {
  value?: string;
  contentReference: string;
  checksumSha256: string;
  disabled?: boolean;
  onSelect: (artifact: ProcurementTenderDocumentContentArtifactOption) => void;
}

const verificationLabel = (status: number) =>
  status === 1
    ? 'Verified'
    : status === 2
      ? 'Rejected'
      : 'Pending verification';

const malwareLabel = (status: number) =>
  status === 1
    ? 'Scan clean'
    : status === 2
      ? 'Infected'
      : status === 3
        ? 'Scan failed'
        : 'Scan pending';

export function TenderDocumentContentArtifactField({
  value,
  contentReference,
  checksumSha256,
  disabled = false,
  onSelect,
}: TenderDocumentContentArtifactFieldProps) {
  const artifacts = useQuery({
    queryKey: ['procurement-tender-document-content-artifacts'],
    queryFn: procurementTenderDocumentService.contentArtifactOptions,
  });
  const selected = useMemo(
    () => artifacts.data?.find((artifact) => artifact.id === value),
    [artifacts.data, value]
  );

  return (
    <div
      className="space-y-3"
      data-testid="tender-document-content-artifact-field"
    >
      <div className="flex items-end gap-2">
        <div className="min-w-0 flex-1 space-y-2">
          <Label>Controlled workflow evidence document</Label>
          <Select
            disabled={disabled || artifacts.isLoading || artifacts.isError}
            value={value || undefined}
            onValueChange={(artifactId) => {
              const artifact = artifacts.data?.find(
                (candidate) => candidate.id === artifactId
              );
              if (
                artifact &&
                isTenderDocumentContentArtifactApproved(artifact)
              ) {
                onSelect(artifact);
              }
            }}
          >
            <SelectTrigger aria-label="Controlled workflow evidence document">
              <SelectValue
                placeholder={
                  artifacts.isLoading
                    ? 'Loading verified evidence…'
                    : 'Select verified, scan-clean evidence'
                }
              />
            </SelectTrigger>
            <SelectContent>
              {(artifacts.data ?? []).map((artifact) => {
                const approved =
                  isTenderDocumentContentArtifactApproved(artifact);
                return (
                  <SelectItem
                    key={artifact.id}
                    value={artifact.id}
                    disabled={!approved}
                  >
                    {artifact.documentName || artifact.fileName} · v
                    {artifact.version} · {artifact.workflowName} /{' '}
                    {artifact.stepName}
                    {!approved
                      ? ` · ${verificationLabel(artifact.verificationStatus)}, ${malwareLabel(artifact.malwareScanStatus)}`
                      : ''}
                  </SelectItem>
                );
              })}
            </SelectContent>
          </Select>
        </div>
        {!disabled && (
          <Button
            type="button"
            size="icon"
            variant="outline"
            title="Refresh controlled evidence"
            disabled={artifacts.isFetching}
            onClick={() => void artifacts.refetch()}
          >
            <RefreshCw
              className={`h-4 w-4 ${artifacts.isFetching ? 'animate-spin' : ''}`}
            />
          </Button>
        )}
      </div>

      {artifacts.isError && (
        <p className="text-sm text-destructive">
          Controlled workflow evidence could not be loaded. Refresh after
          checking the tenant session.
        </p>
      )}
      {!artifacts.isLoading &&
        !artifacts.isError &&
        !artifacts.data?.length && (
          <p className="text-sm text-muted-foreground">
            No current workflow evidence is available. Upload, malware-scan, and
            verify the source document in Workflow evidence governance first.
          </p>
        )}
      {selected && (
        <div className="flex flex-wrap gap-2">
          <Badge
            variant={
              selected.verificationStatus === 1 ? 'default' : 'secondary'
            }
          >
            {verificationLabel(selected.verificationStatus)}
          </Badge>
          <Badge
            variant={
              selected.malwareScanStatus === 1 ? 'default' : 'destructive'
            }
          >
            {malwareLabel(selected.malwareScanStatus)}
          </Badge>
          <Badge variant="outline">Current v{selected.version}</Badge>
        </div>
      )}

      <div className="grid gap-3 md:grid-cols-2">
        <div className="space-y-2">
          <Label>Backend-controlled content path</Label>
          <Input
            readOnly
            value={contentReference}
            placeholder="Derived from the selected evidence document"
            data-testid="controlled-content-reference"
          />
        </div>
        <div className="space-y-2">
          <Label>Backend-controlled SHA-256</Label>
          <Input
            readOnly
            value={checksumSha256}
            placeholder="Derived from the selected evidence document"
            data-testid="controlled-content-checksum"
          />
        </div>
      </div>
      <p className="text-xs text-muted-foreground">
        The stored path and checksum are copied from the selected current
        evidence version and cannot be edited independently.
      </p>
    </div>
  );
}
