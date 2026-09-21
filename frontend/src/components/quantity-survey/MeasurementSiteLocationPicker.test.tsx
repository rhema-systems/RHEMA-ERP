import React, { useState } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { MeasurementSiteLocationPicker, measurementSiteOptions } from './MeasurementSiteLocationPicker';

afterEach(cleanup);

describe('measurement site location', () => {
  it('keeps the project site first, removes duplicates, and excludes invalid suggestions', () => {
    expect(measurementSiteOptions(['Project site', ' project SITE ', null, '', 'Block B', 'x'.repeat(301)]))
      .toEqual(['Project site', 'Block B']);
  });

  it('preserves historical custom text when suggestions change', () => {
    const view = render(<MeasurementSiteLocationPicker value="Legacy north corner / trench 7" suggestions={['New project site']} required onChange={() => {}} />);
    expect(screen.getByRole('textbox', { name: 'Site location details' })).toHaveValue('Legacy north corner / trench 7');
    view.rerender(<MeasurementSiteLocationPicker value="Legacy north corner / trench 7" suggestions={['Another site']} required onChange={() => {}} />);
    expect(screen.getByRole('textbox', { name: 'Site location details' })).toHaveValue('Legacy north corner / trench 7');
  });

  it('allows precise custom details without forcing a catalogue match', () => {
    function Editor() {
      const [value, setValue] = useState('Project site');
      return <MeasurementSiteLocationPicker value={value} suggestions={['Project site']} required onChange={setValue} />;
    }
    render(<Editor />);
    fireEvent.change(screen.getByRole('textbox', { name: 'Site location details' }), { target: { value: 'Project site - north trench' } });
    expect(screen.getByRole('combobox', { name: 'Choose site location' })).toHaveTextContent('Project site - north trench');
  });
});
