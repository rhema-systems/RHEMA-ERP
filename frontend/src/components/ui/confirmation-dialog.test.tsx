import React from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ConfirmationDialog } from './confirmation-dialog';

describe('ConfirmationDialog', () => {
  it('shows environment context on destructive confirmations', () => {
    render(
      <ConfirmationDialog
        open
        onOpenChange={vi.fn()}
        title="Delete records"
        description="This cannot be undone."
        variant="destructive"
        onConfirm={vi.fn()}
      />
    );

    expect(screen.getByText('Environment')).toBeInTheDocument();
    expect(screen.getByLabelText('Environment: Unknown Environment')).toBeInTheDocument();
  });

  it('submits only once while an asynchronous confirmation is in flight', async () => {
    let finish!: () => void;
    const pending = new Promise<void>((resolve) => {
      finish = resolve;
    });
    const onConfirm = vi.fn(() => pending);

    render(
      <ConfirmationDialog
        open
        onOpenChange={vi.fn()}
        title="Create tender"
        description="Create the tender now."
        confirmText="Create tender"
        onConfirm={onConfirm}
      />
    );

    const button = screen.getByRole('button', { name: 'Create tender' });
    fireEvent.click(button);
    fireEvent.click(button);

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(
      screen.getByRole('button', { name: 'Please wait...' })
    ).toBeDisabled();

    await act(async () => {
      finish();
      await pending;
    });
  });
  it('calls the explicit No callback without treating dismissal as a decline', () => {
    const onCancel = vi.fn();
    const onOpenChange = vi.fn();
    const { rerender } = render(
      <ConfirmationDialog
        open
        title="Apply WHT?"
        description="Choose for this invoice."
        confirmText="Yes"
        cancelText="No"
        onConfirm={vi.fn()}
        onCancel={onCancel}
        onOpenChange={onOpenChange}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(onCancel).not.toHaveBeenCalled();
    rerender(
      <ConfirmationDialog
        open
        title="Apply WHT?"
        description="Choose for this invoice."
        confirmText="Yes"
        cancelText="No"
        onConfirm={vi.fn()}
        onCancel={onCancel}
        onOpenChange={onOpenChange}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'No' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
