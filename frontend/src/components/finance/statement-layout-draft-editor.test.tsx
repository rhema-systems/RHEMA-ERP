import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { StatementLayoutDraftEditor } from './statement-layout-draft-editor';
import type { FinancialStatementRowInputDto } from '@/types/finance';

const row: FinancialStatementRowInputDto = {
  rowCode: 'ASSETS',
  label: 'Assets',
  rowType: 'Account',
  displayOrder: 10,
  signMultiplier: 1,
  isVisible: true,
  suppressIfZero: false,
  showAccountDetails: false,
  isBold: true,
  isItalic: false,
  isUnderlined: false,
  indentLevel: 0,
  mappings: [
    {
      mappingType: 'Classification',
      accountClassificationId: 'class-1',
      accountClassificationCode: 'ASSETS',
      includeClassificationDescendants: true,
    },
  ],
};
describe('in-app layout draft designer', () => {
  it('stages edits and preserves mappings until explicit save', () => {
    const save = vi.fn().mockResolvedValue(true);
    render(
      <StatementLayoutDraftEditor
        initialRows={[row]}
        classifications={[]}
        busy={false}
        onSave={save}
        onCancel={vi.fn()}
      />
    );
    fireEvent.change(screen.getByLabelText('Row 1 label'), {
      target: { value: 'Total assets' },
    });
    expect(save).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Save draft design' }));
    expect(save).toHaveBeenCalledWith([{ ...row, label: 'Total assets' }]);
    expect(row.label).toBe('Assets');
  });
  it('requires a unique code and label for new rows and resequences their order', () => {
    const save = vi.fn().mockResolvedValue(true);
    render(
      <StatementLayoutDraftEditor
        initialRows={[row]}
        classifications={[]}
        busy={false}
        onSave={save}
        onCancel={vi.fn()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Add row' }));
    expect(
      screen.getByRole('button', { name: 'Save draft design' })
    ).toBeDisabled();
    fireEvent.change(screen.getByLabelText('Row 2 code'), {
      target: { value: 'TITLE' },
    });
    fireEvent.change(screen.getByLabelText('Row 2 label'), {
      target: { value: 'Statement' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Move row 2 up' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save draft design' }));
    expect(save).toHaveBeenCalledWith([
      expect.objectContaining({ rowCode: 'TITLE', displayOrder: 10 }),
      expect.objectContaining({ rowCode: 'ASSETS', displayOrder: 20 }),
    ]);
  });
});
