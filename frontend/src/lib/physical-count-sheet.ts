import * as XLSX from 'xlsx';
import type { PhysicalCountItemDto, PhysicalCountItemExportDto } from '@/services/inventoryManagementService';

export const COUNT_SHEET_HEADERS = ['Item Code', 'Item Name', 'UOM', 'Location', 'Counted Qty'];
export const COUNT_SHEET_HEADERS_WITHOUT_LOCATION = ['Item Code', 'Item Name', 'UOM', 'Counted Qty'];
export const hasCountSheetLocations = (items: { locationName?: string }[]) => items.some(item => item.locationName?.trim());

export function createCountSheet(items: PhysicalCountItemExportDto[]) {
  const includeLocation = hasCountSheetLocations(items);
  // Deliberate allow-list: never put snapshots, saved counts or variances in a blind sheet.
  const sheet = XLSX.utils.aoa_to_sheet([includeLocation ? COUNT_SHEET_HEADERS : COUNT_SHEET_HEADERS_WITHOUT_LOCATION, ...items.map(item => [
    item.itemCode, item.itemName, item.unitOfMeasure, ...(includeLocation ? [item.locationName || ''] : []), '',
  ])]);
  sheet['!cols'] = [{ wch: 44 }, { wch: 44 }, { wch: 10 }, ...(includeLocation ? [{ wch: 24 }] : []), { wch: 16 }];
  sheet['!autofilter'] = { ref: sheet['!ref']! };
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, sheet, 'Count Sheet');
  return workbook;
}

export type CountSheetRow = { item: PhysicalCountItemDto; quantity: number; row: number };
const text = (value: unknown) => String(value ?? '').trim();
const key = (value: unknown) => text(value).toLocaleLowerCase('en');

export function parseCountSheet(workbook: XLSX.WorkBook, items: PhysicalCountItemDto[]) {
  if (workbook.SheetNames.length !== 1) throw new Error('Use the downloaded count sheet with one worksheet.');
  const sheet = workbook.Sheets[workbook.SheetNames[0]];
  const range = XLSX.utils.decode_range(sheet['!ref'] || 'A1');
  if (range.e.r > 10000 || ![3, 4].includes(range.e.c) || range.s.r !== 0 || range.s.c !== 0) {
    throw new Error('Use the downloaded four or five columns only: Item Code, Item Name, UOM, Location (if included), Counted Qty.');
  }
  for (const [address, cell] of Object.entries(sheet)) {
    if (!address.startsWith('!') && (cell as XLSX.CellObject).f) {
      throw new Error(`Cell ${address}: enter a value, not a formula.`);
    }
  }
  const data = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: '', raw: true, blankrows: true });
  const includeLocation = range.e.c === 4;
  const headers = includeLocation ? COUNT_SHEET_HEADERS : COUNT_SHEET_HEADERS_WITHOUT_LOCATION;
  if (headers.some((header, index) => text(data[0]?.[index]) !== header)) {
    throw new Error('The column headings have changed. Download a new count sheet.');
  }
  if (!includeLocation && items.some(item => item.locationId || text(item.locationName))) {
    throw new Error('This count has saved locations. Download the current sheet and keep its Location column.');
  }
  const rows: CountSheetRow[] = [];
  const seen = new Set<string>();
  let blankCount = 0;
  for (let index = 1; index < data.length; index++) {
    const cells = data[index];
    if (cells.every(cell => !text(cell))) continue;
    const row = index + 1;
    const matches = items.filter(item => key(item.itemCode) === key(cells[0]) && key(item.locationName) === key(includeLocation ? cells[3] : ''));
    if (matches.length !== 1) throw new Error(`Row ${row}: item code and location must match exactly one saved count line. Download the current sheet; do not change its item or location.`);
    const item = matches[0];
    if (key(item.itemName) !== key(cells[1]) || key(item.unitOfMeasure) !== key(cells[2])) {
      throw new Error(`Row ${row}: item name or UOM differs from the saved count line.`);
    }
    if (seen.has(item.id)) throw new Error(`Row ${row}: this item and location appear more than once.`);
    seen.add(item.id);
    const value = cells[includeLocation ? 4 : 3];
    if (!text(value)) { blankCount++; continue; }
    const quantity = typeof value === 'number' ? value : /^\d+(\.\d+)?$/.test(text(value)) ? Number(value) : NaN;
    if (!Number.isFinite(quantity) || quantity < 0 || quantity > 99999999999999.9999 || Math.abs(quantity * 10000 - Math.round(quantity * 10000)) > 0.00001) {
      throw new Error(`Row ${row}: Counted Qty must be zero or a positive number with at most four decimal places.`);
    }
    rows.push({ item, quantity, row });
  }
  return { rows, blankCount };
}
