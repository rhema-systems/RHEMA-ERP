import { describe, expect, it } from 'vitest';

import { clientFacingAccountNumber } from './initialization-evidence-pack';

describe('clientFacingAccountNumber', () => {
    it('removes the active tenant prefix from the on-screen account number', () => {
        expect(clientFacingAccountNumber('DEFAULT-1000', 'DEFAULT')).toBe('1000');
    });

    it('matches the active tenant prefix without case sensitivity', () => {
        expect(clientFacingAccountNumber('default-1010', 'DEFAULT')).toBe('1010');
    });

    it('preserves account numbers that do not carry the active tenant prefix', () => {
        expect(clientFacingAccountNumber('001-FIN-1500', 'DEFAULT')).toBe('001-FIN-1500');
    });
});
