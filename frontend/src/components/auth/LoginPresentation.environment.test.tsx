import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LoginPresentation } from './LoginPresentation';

describe('LoginPresentation environment awareness', () => {
  it.each(['LightCorporate', 'DarkPremium'] as const)(
    'shows the pre-login environment indicator in %s',
    (style) => {
      render(<LoginPresentation style={style}><div>Sign in form</div></LoginPresentation>);
      expect(screen.getByTestId('login-shell')).toHaveAttribute('data-login-style', style);
      expect(screen.getByLabelText('Environment: Unknown Environment')).toBeInTheDocument();
    },
  );
});
