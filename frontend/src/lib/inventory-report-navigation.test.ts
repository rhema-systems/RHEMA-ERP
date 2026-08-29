import { describe, expect, it } from 'vitest';

import {
  INVENTORY_MOVEMENT_REPORT_PATH,
  INVENTORY_VALUATION_REPORT_PATH,
} from './inventory-report-navigation';

describe('inventory statutory report navigation', () => {
  it('opens the published Inventory movement register', () => {
    expect(INVENTORY_MOVEMENT_REPORT_PATH).toBe(
      '/reports/inventory/movement-register'
    );
  });

  it('opens the Inventory valuation to Finance reconciliation register', () => {
    expect(INVENTORY_VALUATION_REPORT_PATH).toBe(
      '/reports/inventory/valuation-gl-register'
    );
  });
});
