import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { FacilitiesDutyLookup } from './FacilitiesDutyLookup';

describe('FacilitiesDutyLookup', () => {
  it('searches on demand and returns the selected HR record', async () => {
    const search = vi.fn(async () => [
      { id: 'employee-1', staffName: 'Efua Admin', employeeNumber: 'LA-LEG-005' },
    ]);
    const onSelect = vi.fn();
    render(
      <FacilitiesDutyLookup
        label="Cleaner / staff name"
        search={search}
        describe={(staff) => ({ title: `${staff.staffName} (${staff.employeeNumber})` })}
        onSelect={onSelect}
      />
    );

    expect(search).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('combobox', { name: 'Cleaner / staff name' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Search cleaner / staff name' }), {
      target: { value: 'Efua' },
    });
    await waitFor(() => expect(search).toHaveBeenCalledWith('Efua'));
    fireEvent.click(await screen.findByRole('option', { name: /Efua Admin/ }));
    expect(onSelect).toHaveBeenCalledWith({
      id: 'employee-1', staffName: 'Efua Admin', employeeNumber: 'LA-LEG-005',
    });
  });

  it('keeps the unit picker unavailable until a property is selected', () => {
    render(
      <FacilitiesDutyLookup
        label="Unit / parcel"
        disabled
        search={async () => [] as Array<{ id: string }>}
        describe={() => ({ title: '' })}
        onSelect={() => undefined}
      />
    );
    expect(screen.getByRole('combobox', { name: 'Unit / parcel' })).toBeDisabled();
  });
});
