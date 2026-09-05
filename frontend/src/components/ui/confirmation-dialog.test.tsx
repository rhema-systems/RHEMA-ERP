import React from 'react'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { ConfirmationDialog } from './confirmation-dialog'

describe('ConfirmationDialog', () => {
  it('submits only once while an asynchronous confirmation is in flight', async () => {
    let finish!: () => void
    const pending = new Promise<void>((resolve) => {
      finish = resolve
    })
    const onConfirm = vi.fn(() => pending)

    render(
      <ConfirmationDialog
        open
        onOpenChange={vi.fn()}
        title="Create tender"
        confirmText="Create tender"
        onConfirm={onConfirm}
      />
    )

    const button = screen.getByRole('button', { name: 'Create tender' })
    fireEvent.click(button)
    fireEvent.click(button)

    expect(onConfirm).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('button', { name: 'Please wait...' })).toBeDisabled()

    await act(async () => {
      finish()
      await pending
    })
  })
})
