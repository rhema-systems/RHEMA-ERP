import React from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogTitle,
} from './dialog';

function TestDialog({
  onInteractOutside,
  onEscapeKeyDown,
}: {
  onInteractOutside?: React.ComponentProps<
    typeof DialogContent
  >['onInteractOutside'];
  onEscapeKeyDown?: React.ComponentProps<
    typeof DialogContent
  >['onEscapeKeyDown'];
}) {
  const [open, setOpen] = React.useState(true);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent
        aria-describedby={undefined}
        onInteractOutside={onInteractOutside}
        onEscapeKeyDown={onEscapeKeyDown}
      >
        <DialogTitle>Large form</DialogTitle>
        {Array.from({ length: 30 }, (_, index) => (
          <label key={index}>
            Field {index + 1}
            <input />
          </label>
        ))}
        <DialogFooter>
          <DialogClose>Cancel</DialogClose>
          <button type="button">Save</button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

describe('DialogContent', () => {
  it('constrains a tall form to the dynamic viewport and makes it vertically scrollable at large font size', () => {
    document.documentElement.dataset.fontSize = 'large';

    render(<TestDialog />);

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveClass('max-h-[calc(100dvh-2rem)]');
    expect(dialog).toHaveClass('overflow-y-auto');
    expect(dialog).toHaveClass('overscroll-y-contain');
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeInTheDocument();

    delete document.documentElement.dataset.fontSize;
  });

  it('keeps the dialog open after backdrop interaction and Escape, then allows an explicit close', async () => {
    const onInteractOutside = vi.fn();
    const onEscapeKeyDown = vi.fn();
    render(
      <TestDialog
        onInteractOutside={onInteractOutside}
        onEscapeKeyDown={onEscapeKeyDown}
      />
    );

    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 0));
    });

    const dialog = screen.getByRole('dialog');
    const overlay = dialog.previousElementSibling as HTMLElement;
    fireEvent.pointerDown(overlay);
    expect(onInteractOutside).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onEscapeKeyDown).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
