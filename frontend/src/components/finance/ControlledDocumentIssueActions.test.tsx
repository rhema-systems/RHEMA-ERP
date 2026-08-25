import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ControlledDocumentIssueActions } from './ControlledDocumentIssueActions';

const mocks = vi.hoisted(() => ({
  hasPermission: vi.fn(),
  getIssues: vi.fn(),
  issue: vi.fn(),
  download: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: mocks.hasPermission }),
}));

vi.mock('@/components/ui/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('@/services/document-output.service', () => ({
  documentOutputService: {
    getControlledDocumentIssues: (...args: unknown[]) => mocks.getIssues(...args),
    issueControlledDocument: (...args: unknown[]) => mocks.issue(...args),
    downloadRetainedControlledDocument: (...args: unknown[]) => mocks.download(...args),
  },
}));

const summary = {
  originalIssued: true,
  replacementCount: 0,
  totalIssued: 1,
  issues: [{
    id: 'issue-1',
    documentType: 'Finance.Tax.WhtCertificate',
    copyNumber: 1,
    copyType: 'Original' as const,
    issuedAtUtc: '2026-08-25T12:00:00Z',
    issuedByName: 'Ama Controller',
    contentSha256: 'a'.repeat(64),
    fileName: 'wht-original.pdf',
    retained: true,
    retainUntilUtc: '2033-08-25T12:00:00Z',
  }],
};

describe('ControlledDocumentIssueActions retained evidence', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockImplementation((permission: string) => permission === 'Finance.Tax.Configuration.Manage');
    mocks.getIssues.mockResolvedValue(summary);
    mocks.download.mockResolvedValue(undefined);
  });

  it('uses the supplied Finance permission and downloads exact retained history', async () => {
    render(
      <ControlledDocumentIssueActions
        documentType="Finance.Tax.WhtCertificate"
        entityId="certificate-1"
        documentLabel="WHT certificate PDF"
        issuePermission="Finance.Tax.Configuration.Manage"
        replacementPermission="Finance.Tax.Configuration.Manage"
      />
    );

    expect(await screen.findByRole('button', { name: 'Retained original' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Replacement Copy' })).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Retained original' }));
    await waitFor(() => expect(mocks.download).toHaveBeenCalledWith('issue-1'));
    expect(mocks.hasPermission).toHaveBeenCalledWith('Finance.Tax.Configuration.Manage');
  });

  it('shows archive downloads without offering a new issue for an old certificate version', async () => {
    render(
      <ControlledDocumentIssueActions
        documentType="Finance.Tax.WhtCertificate"
        entityId="superseded-certificate"
        documentLabel="WHT certificate v1"
        issuePermission="Finance.Tax.Configuration.Manage"
        replacementPermission="Finance.Tax.Configuration.Manage"
        readOnly
      />
    );

    expect(await screen.findByRole('button', { name: 'Retained original' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Replacement Copy' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Issue Original/ })).not.toBeInTheDocument();
  });

  it('fails closed when authoritative issue history cannot be loaded', async () => {
    mocks.getIssues.mockRejectedValue(new Error('offline'));
    render(
      <ControlledDocumentIssueActions
        documentType="Finance.Tax.WhtCertificate"
        entityId="certificate-unknown"
        documentLabel="WHT certificate PDF"
        issuePermission="Finance.Tax.Configuration.Manage"
        replacementPermission="Finance.Tax.Configuration.Manage"
      />
    );

    expect(await screen.findByText('Controlled issue history is unavailable.')).toBeVisible();
    expect(screen.queryByRole('button', { name: /Issue Original/ })).not.toBeInTheDocument();
  });
});
