import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ModuleReportLandingPage } from './ModuleReportLandingPage';
import { moduleReportLandings } from './module-report-landings';

describe('ModuleReportLandingPage', () => {
  it.each(Object.values(moduleReportLandings))('renders the dedicated $code module landing', definition => {
    render(<ModuleReportLandingPage definition={definition} />);

    expect(screen.getByRole('heading', { name: definition.title })).toBeInTheDocument();
    for (const group of definition.groups) {
      expect(screen.getByRole('heading', { name: group.title })).toBeInTheDocument();
      for (const item of group.items) {
        expect(screen.getByRole('link', { name: item.title })).toHaveAttribute('href', item.href);
      }
    }
  });
});
