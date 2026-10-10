import { describe, expect, it } from 'vitest';
import {
  buildProjectPlanningPath,
  getProjectWorkspaceTabs,
} from './projectWorkspaceTabs';

describe('project workspace tabs', () => {
  it.each([
    undefined,
    { deliveryStructure: 'MultiUnit', developmentType: 'Residential' },
    { deliveryStructure: 'SingleUnit', developmentType: 'Commercial' },
    { deliveryStructure: 'WholeDevelopment', developmentType: 'Renovation' },
  ])('keeps Planning immediately after Work Components for %o', (context) => {
    const tabs = getProjectWorkspaceTabs(context);
    const workComponentsIndex = tabs.indexOf('packages');

    expect(workComponentsIndex).toBeGreaterThanOrEqual(0);
    expect(tabs[workComponentsIndex + 1]).toBe('plan');
  });

  it('builds an encoded Planning deep link for the selected work component', () => {
    expect(buildProjectPlanningPath('project/one', 'component two')).toBe(
      '/development/projects/project%2Fone/plan?workComponent=component%20two'
    );
  });
});
