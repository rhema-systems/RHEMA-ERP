import { describe, expect, it } from 'vitest';
import * as XLSX from 'xlsx';
import { COUNT_SHEET_HEADERS, COUNT_SHEET_HEADERS_WITHOUT_LOCATION, createCountSheet, parseCountSheet } from './physical-count-sheet';
import type { PhysicalCountItemDto, PhysicalCountItemExportDto } from '@/services/inventoryManagementService';

const items = [
  { id: 'a', itemCode: 'PVC', itemName: 'Pipe', unitOfMeasure: 'EACH', locationName: 'LOC-001' },
  { id: 'b', itemCode: 'KIT', itemName: 'Kit', unitOfMeasure: 'EA', locationName: '' },
] as PhysicalCountItemDto[];
const book = (rows: unknown[][], headers = COUNT_SHEET_HEADERS) => {
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([headers, ...rows]), 'Count Sheet');
  return workbook;
};
const row = (quantity: unknown) => ['PVC', 'Pipe', 'EACH', 'LOC-001', quantity];

describe('blind physical count workbook', () => {
  it('omits the location column when no count lines have saved locations', () => {
    const unlocated = items.map(item => ({ ...item, locationName: undefined }));
    const bytes = XLSX.write(createCountSheet(unlocated as PhysicalCountItemExportDto[]), { type: 'array', bookType: 'xlsx' });
    const saved = XLSX.read(bytes, { type: 'array' });
    expect(XLSX.utils.sheet_to_json(saved.Sheets['Count Sheet'], { header: 1, defval: '' })).toEqual([
      COUNT_SHEET_HEADERS_WITHOUT_LOCATION, ['PVC', 'Pipe', 'EACH', '', '', ''], ['KIT', 'Kit', 'EA', '', '', ''],
    ]);
    expect(saved.Sheets['Count Sheet']['!ref']).toBe('A1:F3');
    expect(parseCountSheet(saved, unlocated)).toEqual({ rows: [], blankCount: 2 });
    saved.Sheets['Count Sheet'].D2 = { t: 'n', v: 0 };
    saved.Sheets['Count Sheet'].D3 = { t: 'n', v: 23 };
    expect(parseCountSheet(saved, unlocated).rows.map(result => result.quantity)).toEqual([0, 23]);
  });
  it('keeps older five-column sheets usable for counts without locations', () => {
    expect(parseCountSheet(book([['KIT', 'Kit', 'EA', '', 50]]), [items[1]]).rows[0].quantity).toBe(50);
  });
  it('does not let a four-column sheet bypass saved locations or ambiguous item identities', () => {
    const sheet = book([['PVC', 'Pipe', 'EACH', 2]], COUNT_SHEET_HEADERS_WITHOUT_LOCATION);
    expect(() => parseCountSheet(sheet, items)).toThrow(/saved locations/);
    expect(() => parseCountSheet(sheet, [{ ...items[0], locationName: '', locationId: 'location' }])).toThrow(/saved locations/);
    const unlocated = { ...items[0], locationName: '' };
    expect(() => parseCountSheet(sheet, [unlocated, { ...unlocated, id: 'duplicate' }])).toThrow(/saved count line/);
    sheet.Sheets['Count Sheet'].D2.f = '1+1';
    expect(() => parseCountSheet(sheet, [unlocated])).toThrow(/formula/);
  });
  it('exports seven columns and blanks, even for previously counted items', () => {
    const workbook = createCountSheet(items.map(item => ({ ...item, systemQuantity: 8675309,
      countedQuantity: 8675308, varianceQuantity: -1, varianceValue: -999, notes: 'secret', isCounted: true,
    })) as PhysicalCountItemExportDto[]);
    const bytes = XLSX.write(workbook, { type: 'array', bookType: 'xlsx' });
    const saved = XLSX.read(bytes, { type: 'array' });
    expect(saved.SheetNames).toEqual(['Count Sheet']);
    expect(XLSX.utils.sheet_to_json(saved.Sheets['Count Sheet'], { header: 1, defval: '' })).toEqual([
      COUNT_SHEET_HEADERS, [...row(''), '', ''], ['KIT', 'Kit', 'EA', '', '', '', ''],
    ]);
    expect(saved.Sheets['Count Sheet']['!ref']).toBe('A1:G3');
    expect(saved.Workbook?.Names).toEqual([{ Name: '_xlnm._FilterDatabase', Sheet: 0, Ref: "'Count Sheet'!A1:G3" }]);
    expect(JSON.stringify(saved)).not.toMatch(/8675309|8675308|secret|System Qty|Variance/);
    expect(parseCountSheet(saved, items)).toEqual({ rows: [], blankCount: 2 });
  });
  it('skips a blank but saves explicit zero, and matches by code/location rather than row order', () => {
    const result = parseCountSheet(book([['KIT', 'Kit', 'EA', '', ''], row(0)]), items);
    expect(result.blankCount).toBe(1);
    expect(result.rows).toEqual([{ item: items[0], quantity: 0, defectiveQuantity: 0, defectiveNotes: '', row: 3 }]);
  });
  it.each([2, '2', 1.2345])('accepts actual quantity %s', quantity => {
    expect(parseCountSheet(book([row(quantity)]), items).rows[0].quantity).toBe(Number(quantity));
  });
  it.each([-1, 'abc', true, '1,000', 'NaN', 1.23456, Infinity])('rejects invalid quantity %s', quantity => {
    expect(() => parseCountSheet(book([row(quantity)]), items)).toThrow(/Counted Qty/);
  });
  it('rejects formulas, extra columns, duplicate or unknown lines', () => {
    const formula = book([row(2)]); formula.Sheets['Count Sheet'].E2.f = '1+1';
    expect(() => parseCountSheet(formula, items)).toThrow(/formula/);
    expect(() => parseCountSheet(book([[...row(2), 0, '', 999]]), items)).toThrow(/count-sheet columns/);
    expect(() => parseCountSheet(book([row(2), row('')]), items)).toThrow(/more than once/);
    expect(() => parseCountSheet(book([['OTHER', 'Other', 'EA', '', 2]]), items)).toThrow(/saved count line/);
  });
  it('rejects changed UOM, headers, location or ambiguous saved lines', () => {
    expect(() => parseCountSheet(book([['PVC', 'Pipe', 'BOX', 'LOC-001', 2]]), items)).toThrow(/UOM/);
    expect(() => parseCountSheet(book([['PVC', 'Pipe', 'EACH', '', 2]]), items)).toThrow(/saved count line/);
    expect(() => parseCountSheet(book([row(2)]), [...items, { ...items[0], id: 'c' }])).toThrow(/saved count line/);
    const renamed = book([row(2)]); renamed.Sheets['Count Sheet'].E1.v = 'System Qty';
    expect(() => parseCountSheet(renamed, items)).toThrow(/headings/);
  });
});


describe('defective stock observations', () => {
  it('retains defects separately from physical quantity', () => {
    const result = parseCountSheet(book([[...row(100), 5, 'Damaged packaging']]), items).rows[0];
    expect(result.quantity).toBe(100);
    expect(result.defectiveQuantity).toBe(5);
    expect(result.defectiveNotes).toBe('Damaged packaging');
  });
  it.each([-1, 101, 1.00001, 'bad'])('rejects invalid defective quantity %s', defective => {
    expect(() => parseCountSheet(book([[...row(100), defective, '']]), items)).toThrow(/Defective Qty/);
  });
  it('preserves saved defects when importing a legacy workbook without defect columns', () => {
    const saved = [{ ...items[0], defectiveQuantity: 2, defectiveNotes: 'Existing damage' }];
    const result = parseCountSheet(book([row(10)], COUNT_SHEET_HEADERS.slice(0, -2)), saved).rows[0];
    expect(result.defectiveQuantity).toBe(2);
    expect(result.defectiveNotes).toBe('Existing damage');
  });
  it('requires a physical quantity before defective details', () => {
    expect(() => parseCountSheet(book([[...row(''), 1, 'Damage']]), items)).toThrow(/Counted Qty before/);
  });
});
