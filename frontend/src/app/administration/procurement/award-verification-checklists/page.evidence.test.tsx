import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ list: vi.fn(), create: vi.fn() }));
vi.mock('@/services/awardVerificationService', () => ({ awardVerificationService: {
  getTemplates: mocks.list, createTemplate: mocks.create,
} }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
import Page from './page';

beforeEach(() => { vi.stubGlobal('React', React); vi.clearAllMocks(); mocks.list.mockResolvedValue([]); mocks.create.mockResolvedValue({}); });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('checklist document requirement configuration', () => {
  it.each([false, true])('saves mandatory review with independent document required=%s', async (requiresDocument) => {
    render(<Page />);
    await screen.findByText('No templates found.');
    fireEvent.click(screen.getByRole('button', { name: 'Add Template' }));
    fireEvent.change(screen.getByPlaceholderText('e.g., Standard Award Verification'), { target: { value: 'Evidence policy test' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add Item' }));
    fireEvent.change(screen.getByPlaceholderText('Checklist item text *'), { target: { value: 'Review supplier' } });
    const toggle = screen.getByRole('switch', { name: 'Document required for check 1' });
    expect(toggle).not.toBeChecked();
    if (requiresDocument) fireEvent.click(toggle);
    fireEvent.click(screen.getByRole('button', { name: 'Create Template' }));
    await waitFor(() => expect(mocks.create).toHaveBeenCalledTimes(1));
    expect(mocks.create.mock.calls[0][0].items[0]).toMatchObject({ isRequired: true, requiresDocument });
  });
});
