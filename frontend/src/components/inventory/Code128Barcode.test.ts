import { describe, expect, it } from 'vitest';
import { encodeCode128B } from './Code128Barcode';

describe('encodeCode128B', () => {
  it('creates start, data, checksum, and stop symbols', () => {
    const symbols = encodeCode128B('AB12');

    expect(symbols).toHaveLength(7);
    expect(symbols[0]).toBe('211214');
    expect(symbols.at(-1)).toBe('2331112');
  });

  it('rejects active or non-printable payload characters', () => {
    expect(() => encodeCode128B('LOT\n01')).toThrow(/printable ASCII/);
    expect(() => encodeCode128B('')).toThrow(/printable ASCII/);
  });
});
