import React, { useState } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import PhoneInput from './phone-input';

beforeAll(() => {
  vi.stubGlobal(
    'ResizeObserver',
    class ResizeObserver {
      observe() {}
      unobserve() {}
      disconnect() {}
    }
  );
  Element.prototype.scrollIntoView = vi.fn();
});

function ControlledPhoneInput() {
  const [value, setValue] = useState('');
  return (
    <>
      <PhoneInput
        id="phone"
        value={value}
        onChange={setValue}
        countrySelectLabel="Phone country calling code"
      />
      <output aria-label="International phone value">{value}</output>
    </>
  );
}

describe('PhoneInput', () => {
  it('searches countries and enforces the selected country national length', () => {
    render(<ControlledPhoneInput />);

    fireEvent.click(
      screen.getByRole('combobox', { name: 'Phone country calling code' })
    );
    const search = screen.getByPlaceholderText(
      'Search country or calling code...'
    );
    fireEvent.change(search, { target: { value: 'United Kingdom' } });
    fireEvent.click(screen.getByText('United Kingdom'));

    const phone = screen.getByLabelText('International phone value');
    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: '07123456789' } });
    expect(phone).toHaveTextContent('+447123456789');
    expect(input).toHaveValue('7123456789');

    fireEvent.change(input, { target: { value: '71234567890' } });
    expect(phone).toHaveTextContent('+447123456789');
    expect(input).toHaveValue('7123456789');
  });
});
