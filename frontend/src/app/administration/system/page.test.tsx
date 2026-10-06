import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import SystemInformationPage from './page';

describe('SystemInformationPage', () => {
  it('shows the safe environment identity and an isolation warning for unknown configuration', () => {
    render(<SystemInformationPage />);

    expect(screen.getByRole('heading', { name: 'System Information' })).toBeInTheDocument();
    expect(screen.getByLabelText('Environment: Unknown Environment')).toBeInTheDocument();
    expect(screen.getByText(/Production data isolation has not been confirmed/i)).toBeInTheDocument();
    expect(screen.queryByText(/connection string/i)).not.toBeInTheDocument();
  });
});
