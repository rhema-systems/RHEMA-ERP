import React from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ReceiptInspectionBadge } from './ReceiptInspectionBadge';

afterEach(cleanup);

describe('PO receipt inspection badge', () => {
  it('shows completion from saved evidence even though inspection remains required on the record', () => {
    render(<ReceiptInspectionBadge requiresInspection inspectionDate="2026-09-09T17:50:00Z" inspectionResult="Accepted" className="mt-1" />);
    expect(screen.getByText('Inspection Complete')).toHaveClass('bg-green-50', 'text-green-800', 'mt-1');
    expect(screen.queryByText(/Requires Inspection|Inspection Required/)).not.toBeInTheDocument();
  });

  it.each([
    { inspectionDate: undefined, inspectionResult: undefined },
    { inspectionDate: undefined, inspectionResult: 'Accepted' },
    { inspectionDate: '2026-09-09', inspectionResult: undefined },
    { inspectionDate: '2026-09-09', inspectionResult: '' },
    { inspectionDate: '2026-09-09', inspectionResult: '   ' },
    { inspectionDate: '2026-09-09', inspectionResult: 'Pending' },
    { inspectionDate: '2026-09-09', inspectionResult: ' PENDING ' },
  ])('keeps incomplete inspection outstanding: %j', evidence => {
    render(<ReceiptInspectionBadge requiresInspection {...evidence} />);
    expect(screen.getByText('Inspection Required')).toHaveClass('text-amber-800');
    expect(screen.queryByText('Inspection Complete')).not.toBeInTheDocument();
  });

  it('does not introduce an inspection requirement when none exists', () => {
    const { container } = render(<ReceiptInspectionBadge requiresInspection={false} />);
    expect(container).toBeEmptyDOMElement();
  });
});
