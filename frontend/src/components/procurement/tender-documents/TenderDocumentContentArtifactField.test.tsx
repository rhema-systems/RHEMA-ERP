import React, { type ReactNode } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

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
    {
      id: 'policy-eligible-evidence',
      documentName: 'Policy-approved current',
      fileName: 'policy-terms.docx',
      filePath: 'workflow-evidence/default/policy-terms.docx',
      sha256: 'd'.repeat(64),
      version: 1,
      isCurrent: true,
      verificationStatus: 0,
      malwareScanStatus: 1,
      workflowInstanceId: 'instance-1',
      workflowName: 'Document control',
      entityType: 'TenderDocument',
      entityId: 'entity-1',
      stepName: 'Upload',
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

import {
  TenderDocumentContentArtifactField,
  isTenderDocumentContentArtifactEligible,
} from './TenderDocumentContentArtifactField';

afterEach(cleanup);

describe('server-controlled tender document eligibility', () => {
  const pendingFile = {
    id: 'file-1',
    isCurrent: true,
    verificationStatus: 0,
    malwareScanStatus: 1,
  };
  it('allows configured review satisfaction only for an explicit server-eligible ID', () => {
    expect(
      isTenderDocumentContentArtifactEligible(pendingFile, ['file-1'])
    ).toBe(true);
    expect(isTenderDocumentContentArtifactEligible(pendingFile, [])).toBe(
      false
    );
    expect(
      isTenderDocumentContentArtifactEligible(pendingFile, ['another-file'])
    ).toBe(false);
    expect(isTenderDocumentContentArtifactEligible(pendingFile)).toBe(false);
  });
  it('keeps strict verified and clean behavior when the property is absent', () => {
    expect(
      isTenderDocumentContentArtifactEligible({
        ...pendingFile,
        verificationStatus: 1,
      })
    ).toBe(true);
    expect(
      isTenderDocumentContentArtifactEligible(
        { ...pendingFile, verificationStatus: 1 },
        []
      )
    ).toBe(false);
  });
  it.each([
    { isCurrent: false },
    { malwareScanStatus: 0 },
    { malwareScanStatus: 2 },
    { malwareScanStatus: 3 },
    { malwareScanStatus: -1 },
    { verificationStatus: 2 },
    { verificationStatus: -1 },
  ])(
    'does not enable unsafe, rejected, unknown or superseded content even with an ID (%j)',
    (overrides) => {
      expect(
        isTenderDocumentContentArtifactEligible(
          { ...pendingFile, ...overrides },
          ['file-1']
        )
      ).toBe(false);
    }
  );
});

describe('TenderDocumentContentArtifactField', () => {
  it('retains the real attached document label in read-only mode after publication removes eligibility', () => {
    const onSelect = vi.fn();
    render(
      <TenderDocumentContentArtifactField
        workflowInstanceId="instance-1"
        value="approved-evidence"
        contentReference="workflow-evidence/default/approved-itb.docx"
        checksumSha256={'a'.repeat(64)}
        eligibleContentEvidenceDocumentIds={[]}
        disabled
        onSelect={onSelect}
      />
    );
    expect(screen.getByRole('combobox')).toBeDisabled();
    expect(screen.getByRole('combobox')).toHaveValue('approved-evidence');
    expect(
      screen.getByRole('option', { name: /Approved ITB/, selected: true })
    ).toBeInTheDocument();
    fireEvent.change(screen.getByRole('combobox'), {
      target: { value: 'approved-evidence' },
    });
    expect(onSelect).not.toHaveBeenCalled();
    expect(
      screen.queryByText(/selected file is no longer eligible/)
    ).not.toBeInTheDocument();
  });

  it('does not allow read-only selection changes even when the old eligibility response contains another file', () => {
    const onSelect = vi.fn();
    render(
      <TenderDocumentContentArtifactField
        workflowInstanceId="instance-1"
        value="approved-evidence"
        contentReference="workflow-evidence/default/approved-itb.docx"
        checksumSha256={'a'.repeat(64)}
        eligibleContentEvidenceDocumentIds={[
          'approved-evidence',
          'policy-eligible-evidence',
        ]}
        disabled
        onSelect={onSelect}
      />
    );
    fireEvent.change(screen.getByRole('combobox'), {
      target: { value: 'policy-eligible-evidence' },
    });
    expect(onSelect).not.toHaveBeenCalled();
  });
  it('enables policy-eligible clean content only when the exact server ID is present', () => {
    const onSelect = vi.fn();
    render(
      <TenderDocumentContentArtifactField
        workflowInstanceId="instance-1"
        contentReference=""
        checksumSha256=""
        eligibleContentEvidenceDocumentIds={['policy-eligible-evidence']}
        onSelect={onSelect}
      />
    );
    expect(
      screen.getByRole('option', { name: /Policy-approved current/ })
    ).toBeEnabled();
    expect(screen.getByRole('option', { name: /Approved ITB/ })).toBeDisabled();
    fireEvent.change(screen.getByRole('combobox'), {
      target: { value: 'policy-eligible-evidence' },
    });
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'policy-eligible-evidence' })
    );
  });
  it('refuses content from another workflow even if an ID is supplied', () => {
    const onSelect = vi.fn();
    render(
      <TenderDocumentContentArtifactField
        workflowInstanceId="another-workflow"
        contentReference=""
        checksumSha256=""
        eligibleContentEvidenceDocumentIds={['approved-evidence']}
        onSelect={onSelect}
      />
    );
    expect(screen.getByRole('option', { name: /Approved ITB/ })).toBeDisabled();
    fireEvent.change(screen.getByRole('combobox'), {
      target: { value: 'approved-evidence' },
    });
    expect(onSelect).not.toHaveBeenCalled();
  });
  it('invalidates the visible selection when server eligibility changes', () => {
    const onSelect = vi.fn();
    const props = {
      workflowInstanceId: 'instance-1',
      value: 'approved-evidence',
      contentReference: '',
      checksumSha256: '',
      onSelect,
    };
    const { rerender } = render(
      <TenderDocumentContentArtifactField
        {...props}
        eligibleContentEvidenceDocumentIds={['approved-evidence']}
      />
    );
    expect(screen.getByRole('combobox')).toHaveValue('approved-evidence');
    rerender(
      <TenderDocumentContentArtifactField
        {...props}
        eligibleContentEvidenceDocumentIds={[]}
      />
    );
    expect(screen.getByRole('combobox')).toHaveValue('');
    expect(
      screen.getByText(/selected file is no longer eligible/)
    ).toBeInTheDocument();
    fireEvent.change(screen.getByRole('combobox'), {
      target: { value: 'approved-evidence' },
    });
    expect(onSelect).not.toHaveBeenCalled();
  });
  it('only selects verified scan-clean evidence and presents locked backend values', () => {
    const onSelect = vi.fn();
    render(
      <TenderDocumentContentArtifactField
        workflowInstanceId="instance-1"
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
    expect(
      screen.getByText('Stored file details').closest('details')
    ).not.toHaveAttribute('open');
  });
});
