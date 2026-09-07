import React, { type ReactNode } from 'react';
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  getPartners: vi.fn(),
  getPartnerContacts: vi.fn(),
  issue: vi.fn(),
  createChange: vi.fn(),
  bind: vi.fn(),
  changed: vi.fn(),
  success: vi.fn(),
  error: vi.fn(),
  warning: vi.fn(),
}));
vi.mock('@/services/businessPartnerService', () => ({
  businessPartnerService: {
    getPartners: api.getPartners,
    getPartnerContacts: api.getPartnerContacts,
  },
}));
vi.mock('@/services/procurement-tender-document.service', () => ({
  procurementTenderDocumentService: {
    issue: api.issue,
    createChange: api.createChange,
    bind: api.bind,
  },
}));
vi.mock('sonner', () => ({
  toast: { success: api.success, error: api.error, warning: api.warning },
}));
vi.mock('@/components/ui/select', () => ({
  Select: ({
    value,
    disabled,
    onValueChange,
    children,
  }: {
    value: string;
    disabled?: boolean;
    onValueChange: (value: string) => void;
    children: ReactNode;
  }) => (
    <select
      aria-label={
        value === 'saved' || value === 'new' || value === 'configured'
          ? 'Recipient type'
          : value === '' || value.includes('@')
            ? 'Configured external recipient'
            : 'Select option'
      }
      value={value}
      disabled={disabled}
      onChange={(event) => onValueChange(event.target.value)}
    >
      {children}
    </select>
  ),
  SelectTrigger: () => null,
  SelectValue: () => null,
  SelectContent: ({ children }: { children: ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children: ReactNode }) => (
    <option value={value}>{children}</option>
  ),
}));

import { TenderDocumentActionDialogs } from './TenderDocumentActionDialogs';
import { TenderDocumentRecipientSelector } from './TenderDocumentRecipientSelector';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type {
  ProcurementTenderDocumentReadiness,
  ProcurementTenderDocumentRegister,
} from '@/types/procurement-tender-document';
import { ProcurementMethodType } from '@/types/procurement-tender-control';

const supplier: BusinessPartnerDto = {
  id: 'partner-1',
  partnerName: 'Harbourline Goods Supply Ltd',
  partnerCode: 'SUP-001',
  partnerType: 'Supplier',
  status: 'Approved',
  isActive: true,
  approvalStatus: 'Pending',
  email: 'supplier@example.test',
  phone: '0302000000',
  isPreferred: false,
  isBlacklisted: false,
  createdAt: '2026-09-05T00:00:00Z',
};
const readiness = {
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TND-001',
  hasRegister: true,
  issuanceCount: 0,
  pendingAcknowledgementCount: 0,
  pendingChangeCount: 0,
  ready: true,
  blockedReasons: [],
  allowedActions: [],
} as ProcurementTenderDocumentReadiness;
const register = {
  isSourcePublished: true,
  sourceType: 'Tender',
  sourceId: 'tender-1',
  feeMode: 'Free',
  feeAmount: 0,
  rowVersion: 'version-1',
  allowedActions: ['Issue'],
  allowsNewRecipient: true,
} as ProcurementTenderDocumentRegister;

function renderDialog(
  overrides: Partial<ProcurementTenderDocumentRegister> = {}
) {
  render(
    <TenderDocumentActionDialogs
      readiness={readiness}
      register={{ ...register, ...overrides }}
      approvedTemplates={[]}
      workflows={[]}
      canManage
      onChanged={api.changed}
    />
  );
  fireEvent.click(screen.getByRole('button', { name: 'Issue document' }));
}
function completeReceipt() {
  fireEvent.change(screen.getByRole('textbox', { name: 'Receipt number' }), {
    target: { value: 'UAT-RECEIPT-1' },
  });
  fireEvent.change(
    screen.getByRole('textbox', {
      name: 'Shared issue/payment evidence reference',
    }),
    { target: { value: 'UAT issue evidence' } }
  );
}

describe('first-binding schedule approval', () => {
  const sourceDeadline = '2025-09-07T14:00:27.321Z';
  const sourceOpening = '2025-09-07T14:05:27.321Z';
  function mountBinding(
    actions = ['Bind', 'BindWithScheduleChange'],
    expired = true,
    savedDays?: number
  ) {
    const currentDeadline = expired
      ? sourceDeadline
      : '2098-09-07T14:00:27.321Z';
    render(
      <TenderDocumentActionDialogs
        readiness={{
          ...readiness,
          hasRegister: false,
          bidValidityPeriodDays: savedDays,
          effectiveSubmissionDeadlineUtc: currentDeadline,
          openingScheduledAtUtc: expired
            ? sourceOpening
            : '2098-09-07T14:05:27.321Z',
          effectiveTemplateVersionId: 'template',
          allowedActions: actions,
        }}
        approvedTemplates={[
          {
            id: 'template',
            templateCode: 'NCT',
            version: 1,
            documentTypeCode: 'TENDER',
          } as never,
        ]}
        workflows={[
          {
            id: 'review-workflow',
            name: 'Schedule reviewers',
            version: 1,
            lifecycleStatus: 'Published',
          },
        ]}
        canManage
        onChanged={api.changed}
      />
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind approved version' })
    );
    if (!savedDays) {
      fireEvent.change(
        screen.getByLabelText('Bid validity period (calendar days)'),
        { target: { value: '90' } }
      );
      fireEvent.change(
        screen.getByLabelText('Approved validity document/clause reference'),
        { target: { value: 'Approved NCT document clause 18' } }
      );
    }
    return currentDeadline;
  }
  function fillSchedule() {
    fireEvent.change(screen.getByLabelText('New submission deadline'), {
      target: { value: '2099-09-07T14:00' },
    });
    fireEvent.change(screen.getByLabelText('New opening scheduled'), {
      target: { value: '2099-09-07T14:05' },
    });
    const workflowSelect = screen.getByRole('option', {
      name: /Schedule reviewers/,
    }).parentElement;
    if (!workflowSelect) throw new Error('Workflow selector is missing');
    fireEvent.change(workflowSelect, { target: { value: 'review-workflow' } });
    fireEvent.change(screen.getByLabelText('Schedule evidence reference'), {
      target: { value: 'UAT-DELAY-001' },
    });
    fireEvent.change(screen.getByLabelText('Reason for new dates'), {
      target: { value: 'Document preparation delayed publication' },
    });
  }
  it('preserves exact original timestamps and sends one pending schedule request', async () => {
    mountBinding();
    expect(
      screen.getByLabelText('Original submission deadline')
    ).toHaveAttribute('readonly');
    expect(screen.getByLabelText('Original opening scheduled')).toHaveAttribute(
      'readonly'
    );
    expect(
      screen.getByRole('checkbox', { name: 'Request new dates for approval' })
    ).toBeChecked();
    fillSchedule();
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind and request schedule approval' })
    );
    await waitFor(() => expect(api.changed).toHaveBeenCalledOnce());
    expect(api.bind).toHaveBeenCalledExactlyOnceWith(
      expect.objectContaining({
        submissionDeadlineUtc: sourceDeadline,
        openingScheduledAtUtc: sourceOpening,
        bidValidityPeriodDays: 90,
        bidValidityTermsReference: 'Approved NCT document clause 18',
        bidValidityUntilUtc: '2099-12-06T14:00:00.000Z',
        scheduleChange: expect.objectContaining({
          workflowDefinitionId: 'review-workflow',
          reason: 'Document preparation delayed publication',
        }),
      })
    );
    expect(api.createChange).not.toHaveBeenCalled();
    expect(api.success).toHaveBeenCalledWith(
      expect.stringContaining('Tender dates have not changed')
    );
  });
  it('retains unchanged normal binding without a schedule request', async () => {
    const original = mountBinding(['Bind'], false);
    expect(
      screen.queryByRole('checkbox', { name: 'Request new dates for approval' })
    ).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind immutable version' })
    );
    await waitFor(() => expect(api.bind).toHaveBeenCalledOnce());
    expect(api.bind).toHaveBeenCalledWith(
      expect.objectContaining({
        submissionDeadlineUtc: original,
        scheduleChange: undefined,
      })
    );
  });
  it('does not send an incomplete schedule request', () => {
    mountBinding();
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind and request schedule approval' })
    );
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Schedule approval workflow'
    );
    expect(api.bind).not.toHaveBeenCalled();
  });
  it('does not permit expired ordinary binding when recovery is unavailable', () => {
    mountBinding(['Bind']);
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind immutable version' })
    );
    expect(screen.getByRole('alert')).toHaveTextContent('deadline has elapsed');
    expect(api.bind).not.toHaveBeenCalled();
  });
  it('uses saved terms as read-only and updates the expiry preview with proposed closing', async () => {
    mountBinding(['Bind', 'BindWithScheduleChange'], true, 30);
    expect(
      screen.getByLabelText('Bid validity period (calendar days)')
    ).toHaveAttribute('readonly');
    expect(
      screen.queryByLabelText('Approved validity document/clause reference')
    ).not.toBeInTheDocument();
    expect(
      screen.getByLabelText('Bid valid until (calculated)')
    ).toHaveAttribute('readonly');
    fillSchedule();
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind and request schedule approval' })
    );
    await waitFor(() => expect(api.bind).toHaveBeenCalledOnce());
    expect(api.bind).toHaveBeenCalledWith(
      expect.objectContaining({
        bidValidityPeriodDays: 30,
        bidValidityTermsReference: undefined,
        bidValidityUntilUtc: '2099-10-07T14:00:00.000Z',
      })
    );
  });
  it('identifies the missing approved-term reference without discarding entered values', () => {
    mountBinding(['Bind'], false);
    fireEvent.change(
      screen.getByLabelText('Approved validity document/clause reference'),
      { target: { value: '' } }
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind immutable version' })
    );
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Approved validity document/clause reference'
    );
    expect(
      screen.getByLabelText('Bid validity period (calendar days)')
    ).toHaveValue(90);
    expect(api.bind).not.toHaveBeenCalled();
  });
  it('preserves the dates and reason when the server rejects binding', async () => {
    api.bind.mockRejectedValue(
      new Error('Schedule source changed; reload before continuing.')
    );
    mountBinding();
    fillSchedule();
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind and request schedule approval' })
    );
    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Schedule source changed'
      )
    );
    expect(screen.getByLabelText('Reason for new dates')).toHaveValue(
      'Document preparation delayed publication'
    );
    expect(api.changed).not.toHaveBeenCalled();
  });
  it('refreshes rather than repeating a saved binding when its follow-up read fails', async () => {
    api.changed
      .mockRejectedValueOnce(new Error('Read unavailable'))
      .mockResolvedValue(undefined);
    mountBinding();
    fillSchedule();
    fireEvent.click(
      screen.getByRole('button', { name: 'Bind and request schedule approval' })
    );
    await screen.findByRole('button', { name: 'Refresh register' });
    expect(
      screen.queryByRole('button', {
        name: 'Bind and request schedule approval',
      })
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Refresh register' }));
    await waitFor(() => expect(api.changed).toHaveBeenCalledTimes(2));
    expect(api.bind).toHaveBeenCalledOnce();
  });
});

beforeEach(() => {
  vi.resetAllMocks();
  api.getPartners.mockImplementation(
    async ({ partnerType }: { partnerType: string }) => ({
      items: partnerType === 'Supplier' ? [supplier] : [],
      totalCount: partnerType === 'Supplier' ? 1 : 0,
    })
  );
  api.issue.mockResolvedValue({});
  api.createChange.mockResolvedValue({});
  api.getPartnerContacts.mockResolvedValue([]);
  api.changed.mockResolvedValue(undefined);
});
afterEach(cleanup);

describe('publication and pre-publication schedule controls', () => {
  it.each(['RequestForQuotation', 'Tender'] as const)(
    'preserves the RFQ configured-recipient path under its own server allowed actions (%s)',
    (sourceType) => {
      renderDialog({
        sourceType,
        method: ProcurementMethodType.RequestForQuotation,
        isSourcePublished: false,
        allowsNewRecipient: false,
        allowedExternalRecipientEmails: ['rfq@example.test'],
      });
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(
        screen.getByRole('option', { name: 'Configured external recipient' })
      ).toBeInTheDocument();
      expect(
        screen.queryByRole('option', { name: 'New interested supplier' })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByText(/Publish the tender before issuing/)
      ).not.toBeInTheDocument();
    }
  );

  it.each([false, undefined])(
    'does not offer document issuance before authoritative publication (%s)',
    (isSourcePublished) => {
      render(
        <TenderDocumentActionDialogs
          readiness={readiness}
          register={{ ...register, isSourcePublished }}
          approvedTemplates={[]}
          workflows={[]}
          canManage
          onChanged={api.changed}
        />
      );
      expect(
        screen.queryByRole('button', { name: 'Issue document' })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: 'Reschedule before publication' })
      ).not.toBeInTheDocument();
      if (isSourcePublished === false)
        expect(
          screen.getByText(
            /Publish the tender before issuing documents to suppliers/
          )
        ).toBeInTheDocument();
      expect(api.issue).not.toHaveBeenCalled();
    }
  );

  const showSchedule = () => {
    render(
      <TenderDocumentActionDialogs
        readiness={readiness}
        register={{
          ...register,
          isSourcePublished: false,
          allowedActions: ['RescheduleUnpublished', 'CreateChange'],
          effectiveSubmissionDeadlineUtc: '2030-09-05T17:00:00Z',
          openingScheduledAtUtc: '2030-09-05T17:05:00Z',
          effectiveBidValidityUntilUtc: '2030-10-05T17:00:00Z',
        }}
        approvedTemplates={[]}
        workflows={[
          {
            id: 'workflow-schedule',
            name: 'TDC Procurement Schedule',
            version: 1,
            lifecycleStatus: 'Published',
          },
        ]}
        canManage
        onChanged={api.changed}
      />
    );
    expect(
      screen.queryByRole('button', { name: 'Governed change' })
    ).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole('button', { name: 'Reschedule before publication' })
    );
    fireEvent.change(screen.getByRole('combobox'), {
      target: { value: 'workflow-schedule' },
    });
    fireEvent.change(screen.getByRole('textbox', { name: 'Governed reason' }), {
      target: { value: 'Correct unpublished UAT dates' },
    });
    fireEvent.change(
      screen.getByRole('textbox', {
        name: 'Shared approval evidence reference',
      }),
      { target: { value: 'Schedule review UAT-1' } }
    );
    fireEvent.change(screen.getByLabelText('New submission deadline'), {
      target: { value: '2030-09-06T17:00' },
    });
    fireEvent.change(screen.getByLabelText('New opening time'), {
      target: { value: '2030-09-06T17:05' },
    });
  };

  it('retains generic governed changes where the server offers no paired schedule replacement', () => {
    render(
      <TenderDocumentActionDialogs
        readiness={readiness}
        register={{
          ...register,
          method: ProcurementMethodType.RestrictedTendering,
          isSourcePublished: false,
          allowedActions: ['CreateChange'],
        }}
        approvedTemplates={[]}
        workflows={[]}
        canManage
        onChanged={api.changed}
      />
    );
    expect(
      screen.getByRole('button', { name: 'Governed change' })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Reschedule before publication' })
    ).not.toBeInTheDocument();
  });

  it.each(['PendingApproval', 'Approved', 'Rejected'] as const)(
    'hides generic governed change only while an existing change awaits approval (%s)',
    (status) => {
      render(
        <TenderDocumentActionDialogs
          readiness={readiness}
          register={{
            ...register,
            method: ProcurementMethodType.RestrictedTendering,
            isSourcePublished: false,
            allowedActions: ['CreateChange'],
            changes: [
              { status: 'Approved' },
              { status },
            ] as ProcurementTenderDocumentRegister['changes'],
          }}
          approvedTemplates={[]}
          workflows={[]}
          canManage
          onChanged={api.changed}
        />
      );
      if (status === 'PendingApproval') {
        expect(
          screen.queryByRole('button', { name: 'Governed change' })
        ).not.toBeInTheDocument();
      } else {
        expect(
          screen.getByRole('button', { name: 'Governed change' })
        ).toBeInTheDocument();
      }
      expect(api.createChange).not.toHaveBeenCalled();
    }
  );

  it('submits both dates to the existing governed workflow without publishing or issuing', async () => {
    showSchedule();
    expect(screen.getByRole('dialog')).toHaveTextContent(
      'dates change only after approval'
    );
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Issue document' })
    ).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole('button', { name: 'Submit governed change' })
    );
    await waitFor(() =>
      expect(api.createChange).toHaveBeenCalledWith(
        expect.objectContaining({
          changeType: 'UnpublishedScheduleReschedule',
          newValueUtc: new Date('2030-09-06T17:00').toISOString(),
          newOpeningScheduledAtUtc: new Date('2030-09-06T17:05').toISOString(),
          workflowDefinitionId: 'workflow-schedule',
          reason: 'Correct unpublished UAT dates',
          evidenceReference: 'Schedule review UAT-1',
          requiresAcknowledgement: false,
          registerRowVersion: 'version-1',
        })
      )
    );
    expect(api.issue).not.toHaveBeenCalled();
  });

  it('keeps the schedule dialog and both dates on rejection, displaying detail and code', async () => {
    api.createChange.mockRejectedValue(
      Object.assign(new Error('Rejected'), {
        response: {
          detail: 'The tender has now been published.',
          code: 'SCHEDULE_LOCKED',
        },
      })
    );
    showSchedule();
    fireEvent.click(
      screen.getByRole('button', { name: 'Submit governed change' })
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The tender has now been published. (SCHEDULE_LOCKED)'
    );
    expect(screen.getByLabelText('New submission deadline')).toHaveValue(
      '2030-09-06T17:00'
    );
    expect(screen.getByLabelText('New opening time')).toHaveValue(
      '2030-09-06T17:05'
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(api.changed).not.toHaveBeenCalled();
  });
});

describe('controlled document recipient selection', () => {
  it('fills missing company contact fields from the saved active primary contact deterministically', async () => {
    api.getPartners.mockResolvedValue({
      items: [{ ...supplier, email: '', phone: '' }],
      totalCount: 1,
    });
    api.getPartnerContacts.mockResolvedValue([
      {
        id: '0',
        isPrimary: true,
        isActive: false,
        email: 'inactive@example.test',
        phone: '111',
      },
      {
        id: 'b',
        isPrimary: true,
        isActive: true,
        email: 'second@example.test',
        phone: '222',
      },
      {
        id: 'a',
        isPrimary: true,
        isActive: true,
        email: 'primary@example.test',
        mobile: '0302111111',
      },
    ]);
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    await waitFor(() =>
      expect(
        screen.getByRole('textbox', { name: 'Recipient email' })
      ).toHaveValue('primary@example.test')
    );
    expect(
      screen.getByRole('textbox', { name: 'Recipient phone' })
    ).toHaveValue('0302111111');
    expect(api.getPartnerContacts).toHaveBeenCalledWith(supplier.id);
    expect(api.issue).not.toHaveBeenCalled();
  });

  it('preserves contact edits entered while the saved primary contact is still loading', async () => {
    let finishContacts: (contacts: object[]) => void = () => {};
    api.getPartners.mockResolvedValue({
      items: [{ ...supplier, email: '', phone: '' }],
      totalCount: 1,
    });
    api.getPartnerContacts.mockImplementation(
      () =>
        new Promise((resolve) => {
          finishContacts = resolve;
        })
    );
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    expect(
      screen.getByRole('button', { name: 'Record immutable issue' })
    ).toBeDisabled();
    fireEvent.change(screen.getByRole('textbox', { name: 'Recipient email' }), {
      target: { value: 'override@example.test' },
    });
    await act(async () => {
      finishContacts([
        {
          id: 'primary',
          isPrimary: true,
          isActive: true,
          email: 'primary@example.test',
          phone: '0302111111',
        },
      ]);
    });
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toHaveValue('override@example.test');
    expect(
      screen.getByRole('textbox', { name: 'Recipient phone' })
    ).toHaveValue('0302111111');
  });

  it('keeps primary-contact autofill when receipt input and the contact response arrive in the same batch', async () => {
    let finishContacts: (contacts: object[]) => void = () => {};
    api.getPartners.mockResolvedValue({
      items: [{ ...supplier, email: '', phone: '' }],
      totalCount: 1,
    });
    api.getPartnerContacts.mockImplementation(
      () =>
        new Promise((resolve) => {
          finishContacts = resolve;
        })
    );
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    await act(async () => {
      finishContacts([
        {
          id: 'primary',
          isPrimary: true,
          isActive: true,
          email: 'primary@example.test',
          phone: '0302111111',
        },
      ]);
      await Promise.resolve();
      fireEvent.change(
        screen.getByRole('textbox', { name: 'Receipt number' }),
        { target: { value: 'Concurrent receipt' } }
      );
    });
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toHaveValue('primary@example.test');
    expect(
      screen.getByRole('textbox', { name: 'Recipient phone' })
    ).toHaveValue('0302111111');
    expect(screen.getByRole('textbox', { name: 'Receipt number' })).toHaveValue(
      'Concurrent receipt'
    );
  });

  it('ignores a previous suppliers pending contact lookup after selecting a different saved supplier', async () => {
    let finishContacts: (contacts: object[]) => void = () => {};
    api.getPartners.mockResolvedValue({
      items: [
        { ...supplier, email: '', phone: '' },
        {
          ...supplier,
          id: 'other',
          partnerName: 'Other supplier',
          partnerCode: 'SUP-002',
        },
      ],
      totalCount: 2,
    });
    api.getPartnerContacts.mockImplementation(
      () =>
        new Promise((resolve) => {
          finishContacts = resolve;
        })
    );
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    fireEvent.click(screen.getByRole('button', { name: 'Change supplier' }));
    fireEvent.click(
      await screen.findByRole('button', { name: /Other supplier SUP-002/ })
    );
    await act(async () => {
      finishContacts([
        {
          id: 'primary',
          isPrimary: true,
          isActive: true,
          email: 'stale@example.test',
          phone: '0302333333',
        },
      ]);
    });
    expect(screen.getByRole('textbox', { name: 'Recipient name' })).toHaveValue(
      'Other supplier'
    );
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toHaveValue(supplier.email);
    expect(
      screen.getByRole('textbox', { name: 'Recipient phone' })
    ).toHaveValue(supplier.phone);
    expect(api.getPartnerContacts).toHaveBeenCalledTimes(1);
  });

  it('selects a saved record, hides its ID, autofills contacts and submits the linked identity', async () => {
    renderDialog();
    expect(screen.queryByText('Business-partner ID')).not.toBeInTheDocument();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    expect(screen.getByRole('textbox', { name: 'Recipient name' })).toHaveValue(
      supplier.partnerName
    );
    expect(
      screen.getByRole('textbox', { name: 'Recipient name' })
    ).toHaveAttribute('readonly');
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toHaveValue(supplier.email);
    expect(
      screen.getByRole('textbox', { name: 'Recipient phone' })
    ).toHaveValue(supplier.phone);
    expect(screen.queryByDisplayValue(supplier.id)).not.toBeInTheDocument();
    completeReceipt();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    await waitFor(() =>
      expect(api.issue).toHaveBeenCalledWith(
        expect.objectContaining({
          businessPartnerId: supplier.id,
          recipientName: supplier.partnerName,
          recipientEmail: supplier.email,
          recipientPhone: supplier.phone,
          registerRowVersion: 'version-1',
          amountPaid: 0,
        })
      )
    );
  });

  it.each([false, undefined])(
    'hides new recipient mode unless the server expressly allows it (%s)',
    (allowsNewRecipient) => {
      renderDialog({ allowsNewRecipient });
      expect(
        screen.queryByRole('combobox', { name: 'Recipient type' })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByText('New interested supplier')
      ).not.toBeInTheDocument();
      completeReceipt();
      fireEvent.click(
        screen.getByRole('button', { name: 'Record immutable issue' })
      );
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Select a saved supplier'
      );
      expect(api.issue).not.toHaveBeenCalled();
    }
  );

  it('preserves RFQ configured external recipients without permitting arbitrary new recipients', async () => {
    renderDialog({
      allowsNewRecipient: false,
      allowedExternalRecipientEmails: ['invited@example.test'],
    });
    expect(
      screen.queryByRole('option', { name: 'New interested supplier' })
    ).not.toBeInTheDocument();
    fireEvent.change(screen.getByRole('combobox', { name: 'Recipient type' }), {
      target: { value: 'configured' },
    });
    completeReceipt();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Select an external recipient already configured'
    );
    expect(api.issue).not.toHaveBeenCalled();
    fireEvent.change(
      screen.getByRole('combobox', { name: 'Configured external recipient' }),
      { target: { value: 'invited@example.test' } }
    );
    fireEvent.change(screen.getByRole('textbox', { name: 'Recipient name' }), {
      target: { value: 'Invited external supplier' },
    });
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toHaveAttribute('readonly');
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    await waitFor(() =>
      expect(api.issue).toHaveBeenCalledWith(
        expect.objectContaining({
          businessPartnerId: undefined,
          recipientEmail: 'invited@example.test',
          recipientName: 'Invited external supplier',
        })
      )
    );
  });

  it('clears the saved identity when explicitly changing to new, requires email, and does not imply approval', async () => {
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    fireEvent.change(screen.getByRole('combobox', { name: 'Recipient type' }), {
      target: { value: 'new' },
    });
    expect(screen.getByRole('textbox', { name: 'Recipient name' })).toHaveValue(
      ''
    );
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toHaveValue('');
    expect(
      screen.getByText(/does not create an approved vendor or waive GHANEPS/)
    ).toBeInTheDocument();
    fireEvent.change(screen.getByRole('textbox', { name: 'Recipient name' }), {
      target: { value: 'New interested supplier' },
    });
    completeReceipt();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Enter a valid email');
    expect(api.issue).not.toHaveBeenCalled();
    fireEvent.change(screen.getByRole('textbox', { name: 'Recipient email' }), {
      target: { value: 'new@example.test' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    await waitFor(() =>
      expect(api.issue).toHaveBeenCalledWith(
        expect.objectContaining({
          businessPartnerId: undefined,
          recipientName: 'New interested supplier',
          recipientEmail: 'new@example.test',
        })
      )
    );
  });

  it('keeps the selected identity and entered evidence when the server refuses the issue, with detail and code inline', async () => {
    api.issue.mockRejectedValue(
      Object.assign(new Error('HTTP 409'), {
        response: {
          detail: 'This supplier is restricted.',
          code: 'SUPPLIER_RESTRICTED',
        },
      })
    );
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    completeReceipt();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'This supplier is restricted. (SUPPLIER_RESTRICTED)'
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toHaveValue(supplier.email);
    expect(screen.getByRole('textbox', { name: 'Receipt number' })).toHaveValue(
      'UAT-RECEIPT-1'
    );
    expect(
      screen.getByRole('textbox', {
        name: 'Shared issue/payment evidence reference',
      })
    ).toHaveValue('UAT issue evidence');
    expect(api.changed).not.toHaveBeenCalled();
    expect(api.issue).toHaveBeenCalledTimes(1);
  });

  it('disables resubmission and recipient changes while an issue is pending', async () => {
    let finishIssue: (result: object) => void = () => {};
    api.issue.mockImplementation(
      () =>
        new Promise((resolve) => {
          finishIssue = resolve;
        })
    );
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    completeReceipt();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    expect(
      screen.getByRole('button', { name: 'Record immutable issue' })
    ).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Change supplier' })
    ).toBeDisabled();
    expect(
      screen.getByRole('combobox', { name: 'Recipient type' })
    ).toBeDisabled();
    expect(
      screen.getByRole('textbox', { name: 'Recipient email' })
    ).toBeDisabled();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    expect(api.issue).toHaveBeenCalledTimes(1);
    await act(async () => {
      finishIssue({});
    });
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    );
  });

  it('closes a successfully saved issue when refreshing fails and retries only the register read', async () => {
    api.changed.mockRejectedValueOnce(new Error('Read timed out.'));
    renderDialog();
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    completeReceipt();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Document issue was recorded, but the register could not be refreshed. Read timed out.'
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Issue document' })
    ).toBeDisabled();
    expect(api.success).toHaveBeenCalledWith(
      'Immutable document issue recorded'
    );
    expect(api.error).not.toHaveBeenCalled();
    expect(api.issue).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Refresh register' }));
    await waitFor(() =>
      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    );
    expect(api.changed).toHaveBeenCalledTimes(2);
    expect(api.issue).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Issue document' }));
    expect(screen.getByRole('textbox', { name: 'Recipient name' })).toHaveValue(
      ''
    );
    expect(screen.getByRole('textbox', { name: 'Receipt number' })).toHaveValue(
      ''
    );
  });

  it('keeps paid document payment requirements and disables payment inputs for free issue', async () => {
    renderDialog();
    expect(
      screen.getByRole('spinbutton', { name: 'Amount paid' })
    ).toBeDisabled();
    expect(
      screen.getByRole('textbox', { name: 'Payment reference' })
    ).toBeDisabled();
    cleanup();
    renderDialog({ feeMode: 'Paid', feeAmount: 50 });
    fireEvent.click(
      await screen.findByRole('button', {
        name: /Harbourline Goods Supply Ltd SUP-001/,
      })
    );
    completeReceipt();
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Paid documents require a payment reference'
    );
    expect(api.issue).not.toHaveBeenCalled();
    fireEvent.change(
      screen.getByRole('textbox', { name: 'Payment reference' }),
      { target: { value: 'PAYMENT-1' } }
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Record immutable issue' })
    );
    await waitFor(() =>
      expect(api.issue).toHaveBeenCalledWith(
        expect.objectContaining({
          amountPaid: 50,
          paymentReference: 'PAYMENT-1',
        })
      )
    );
  });
});

describe('saved recipient directory search', () => {
  it('does not replace current results with a delayed previous search response', async () => {
    const delayed: Array<
      (result: { items: BusinessPartnerDto[]; totalCount: number }) => void
    > = [];
    api.getPartners.mockImplementation(({ search }: { search?: string }) =>
      search
        ? Promise.resolve({
            items: [
              { ...supplier, id: 'current', partnerName: 'Current result' },
            ],
            totalCount: 1,
          })
        : new Promise((resolve) => delayed.push(resolve))
    );
    render(
      <TenderDocumentRecipientSelector value="" name="" onSelect={vi.fn()} />
    );
    await waitFor(() => expect(delayed).toHaveLength(3));
    fireEvent.change(
      screen.getByRole('textbox', { name: 'Search saved suppliers' }),
      { target: { value: 'Current' } }
    );
    await screen.findByRole('button', { name: /Current result/ });
    await act(async () => {
      delayed.forEach((resolve) =>
        resolve({ items: [supplier], totalCount: 1 })
      );
    });
    expect(
      screen.getByRole('button', { name: /Current result/ })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /Harbourline/ })
    ).not.toBeInTheDocument();
  });

  it('searches all recipient types without an approval-status gate and supports more than the first page', async () => {
    api.getPartners.mockImplementation(
      async ({ partnerType, page }: { partnerType: string; page: number }) => ({
        items:
          partnerType === 'Supplier'
            ? [
                {
                  ...supplier,
                  id: `partner-${page}`,
                  partnerCode: `SUP-00${page}`,
                },
              ]
            : [],
        totalCount: partnerType === 'Supplier' ? 11 : 0,
      })
    );
    render(
      <TenderDocumentRecipientSelector value="" name="" onSelect={vi.fn()} />
    );
    await screen.findByRole('button', { name: /SUP-001/ });
    fireEvent.change(
      screen.getByRole('textbox', { name: 'Search saved suppliers' }),
      { target: { value: 'Harbour' } }
    );
    await waitFor(() =>
      expect(api.getPartners).toHaveBeenCalledWith({
        page: 1,
        pageSize: 10,
        search: 'Harbour',
        partnerType: 'Contractor',
      })
    );
    fireEvent.click(
      await screen.findByRole('button', { name: 'Load more suppliers' })
    );
    await screen.findByRole('button', { name: /SUP-002/ });
    expect(screen.getByRole('button', { name: /SUP-001/ })).toBeInTheDocument();
    expect(
      api.getPartners.mock.calls.every(
        ([query]) => !('approvalStatus' in query)
      )
    ).toBe(true);
  });

  it('excludes inactive, blacklisted and customer records and offers retry on lookup failure', async () => {
    api.getPartners.mockRejectedValueOnce(new Error('Unavailable'));
    render(
      <TenderDocumentRecipientSelector value="" name="" onSelect={vi.fn()} />
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Saved suppliers could not be loaded'
    );
    api.getPartners.mockResolvedValue({
      items: [
        supplier,
        {
          ...supplier,
          id: 'bad',
          partnerName: 'Blacklisted',
          isBlacklisted: true,
        },
        {
          ...supplier,
          id: 'inactive',
          partnerName: 'Inactive',
          status: 'Active',
          isActive: false,
        },
        {
          ...supplier,
          id: 'customer',
          partnerName: 'Customer',
          partnerType: 'Customer',
        },
      ],
      totalCount: 4,
    });
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await screen.findByRole('button', { name: /Harbourline/ });
    expect(
      screen.queryByRole('button', { name: /Blacklisted|Inactive|Customer/ })
    ).not.toBeInTheDocument();
  });
});
