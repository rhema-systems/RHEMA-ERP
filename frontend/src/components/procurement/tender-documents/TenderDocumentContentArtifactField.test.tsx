import React, { type ReactNode } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const queryResult = vi.hoisted(() => ({
  data: [
    {
      id: 'approved-evidence',
      documentName: 'Approved ITB',
      fileName: 'approved-itb.docx',
      filePath: 'workflow-evidence/default/approved-itb.docx',
      sha256: 'a'.repeat(64),
      version: 2,
      isCurrent: true,
      verificationStatus: 1,
      malwareScanStatus: 1,
      workflowInstanceId: 'instance-1',
      workflowName: 'Document control',
      entityType: 'TenderDocument',
      entityId: 'entity-1',
      stepName: 'Verify content',
    },
    {
      id: 'pending-evidence',
      documentName: 'Pending ITB',
      fileName: 'pending-itb.docx',
      filePath: 'workflow-evidence/default/pending-itb.docx',
      sha256: 'b'.repeat(64),
      version: 1,
      isCurrent: true,
      verificationStatus: 0,
      malwareScanStatus: 0,
      workflowInstanceId: 'instance-2',
      workflowName: 'Document control',
      entityType: 'TenderDocument',
      entityId: 'entity-2',
      stepName: 'Verify content',
    },
  ],
  isLoading: false,
  isError: false,
  isFetching: false,
  refetch: vi.fn(),
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: () => queryResult,
}));

vi.mock('@/components/ui/select', () => ({
  Select: ({
    value,
    onValueChange,
    disabled,
    children,
  }: {
    value?: string;
    onValueChange: (value: string) => void;
    disabled?: boolean;
    children: ReactNode;
  }) => (
    <select
      aria-label="Controlled workflow evidence document"
      value={value ?? ''}
      disabled={disabled}
      onChange={(event) => onValueChange(event.target.value)}
    >
      <option value="" />
      {children}
    </select>
  ),
  SelectTrigger: ({ children }: { children: ReactNode }) => <>{children}</>,
  SelectValue: () => null,
  SelectContent: ({ children }: { children: ReactNode }) => <>{children}</>,
  SelectItem: ({
    value,
    disabled,
    children,
  }: {
    value: string;
    disabled?: boolean;
    children: ReactNode;
  }) => (
    <option value={value} disabled={disabled}>
      {children}
    </option>
  ),
}));

import { TenderDocumentContentArtifactField } from './TenderDocumentContentArtifactField';

describe('TenderDocumentContentArtifactField', () => {
  it('only selects verified scan-clean evidence and presents locked backend values', () => {
    const onSelect = vi.fn();
    render(
      <TenderDocumentContentArtifactField
        contentReference="workflow-evidence/default/existing.docx"
        checksumSha256={'c'.repeat(64)}
        onSelect={onSelect}
      />
    );

    const select = screen.getByRole('combobox', {
      name: 'Controlled workflow evidence document',
    });
    expect(screen.getByRole('option', { name: /Pending ITB/ })).toBeDisabled();

    fireEvent.change(select, { target: { value: 'approved-evidence' } });

    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({
        id: 'approved-evidence',
        filePath: 'workflow-evidence/default/approved-itb.docx',
        sha256: 'a'.repeat(64),
      })
    );
    expect(screen.getByTestId('controlled-content-reference')).toHaveValue(
      'workflow-evidence/default/existing.docx'
    );
    expect(screen.getByTestId('controlled-content-reference')).toHaveAttribute(
      'readonly'
    );
    expect(screen.getByTestId('controlled-content-checksum')).toHaveValue(
      'c'.repeat(64)
    );
    expect(screen.getByTestId('controlled-content-checksum')).toHaveAttribute(
      'readonly'
    );
  });
});
