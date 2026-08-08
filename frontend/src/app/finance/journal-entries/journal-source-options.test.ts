import { describe, expect, it } from 'vitest';
import { journalSourceOptions } from './journal-source-options';

describe('journalSourceOptions', () => {
    it('exposes the canonical Procurement posting source in the Finance journal filter', () => {
        const procurement = journalSourceOptions.find(option => option.label === 'Procurement');

        // Procurement persists SourceModule="Procurement" while the page compares
        // normalized uppercase values. Guarding the exact value prevents the
        // integration journals from becoming undiscoverable again.
        expect(procurement).toEqual({ value: 'PROCUREMENT', label: 'Procurement' });
    });

    it('does not expose duplicate source values', () => {
        const values = journalSourceOptions.map(option => option.value);
        expect(new Set(values).size).toBe(values.length);
    });
});

