import * as XLSX from 'xlsx';

import type {
  OpeningStockItemOption,
  OpeningStockWarehouseOption,
} from './opening-balance-governance';

export const INVENTORY_OPENING_TEMPLATE_HEADERS = [
  'itemCode',
  'locationCode',
  'quantity',
  'unitCost',
  'serialNumber',
  'lotNumber',
  'batchNumber',
  'manufactureDate',
  'expiryDate',
  'notes',
] as const;

export type ImportedInventoryOpeningRow = {
  inventoryItemId: string;
  locationId: string;
  quantity: string;
  unitCost: string;
  serialNumber: string;
  lotNumber: string;
  batchNumber: string;
  manufactureDate: string;
  expiryDate: string;
  notes: string;
};

export type InventoryOpeningImportResult = {
  rows: ImportedInventoryOpeningRow[];
  errors: string[];
  totalValue: number;
};

const normalizeHeader = (value: unknown) =>
  String(value ?? '').trim().toLowerCase().replace(/[^a-z0-9]/g, '');

const aliases: Record<(typeof INVENTORY_OPENING_TEMPLATE_HEADERS)[number], string[]> = {
  itemCode: ['itemCode', 'item', 'sku'],
  locationCode: ['locationCode', 'location', 'binCode', 'bin'],
  quantity: ['quantity', 'qty', 'openingQuantity'],
  unitCost: ['unitCost', 'cost', 'openingCost'],
  serialNumber: ['serialNumber', 'serial'],
  lotNumber: ['lotNumber', 'lot'],
  batchNumber: ['batchNumber', 'batch'],
  manufactureDate: ['manufactureDate', 'manufacturedDate'],
  expiryDate: ['expiryDate', 'expirationDate'],
  notes: ['notes', 'note', 'description'],
};

const getColumn = (value: unknown) => {
  const normalized = normalizeHeader(value);
  return INVENTORY_OPENING_TEMPLATE_HEADERS.find((column) =>
    aliases[column].some((alias) => normalizeHeader(alias) === normalized)
  ) ?? null;
};

const cell = (value: unknown) => String(value ?? '').trim();
const csvCell = (value: string) => `"${value.replace(/"/g, '""')}"`;

const normalizeDate = (value: string, label: string, rowNumber: number, errors: string[]) => {
  if (!value) return '';
  const match = value.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!match) {
    errors.push(`Row ${rowNumber}: ${label} must use YYYY-MM-DD.`);
    return '';
  }
  const parsed = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
  if (parsed.getUTCFullYear() !== Number(match[1]) || parsed.getUTCMonth() !== Number(match[2]) - 1 || parsed.getUTCDate() !== Number(match[3])) {
    errors.push(`Row ${rowNumber}: ${label} is not a valid calendar date.`);
    return '';
  }
  return value;
};

export function buildInventoryOpeningTemplateCsv(itemCode = 'ITEM-001', locationCode = 'MAIN') {
  return [
    INVENTORY_OPENING_TEMPLATE_HEADERS.map(csvCell).join(','),
    [itemCode, locationCode, '1', '1.00', '', '', '', '', '', 'Opening schedule evidence']
      .map(csvCell).join(','),
  ].join('\r\n');
}

export async function parseInventoryOpeningImportFile(
  file: File,
  items: OpeningStockItemOption[],
  warehouse: OpeningStockWarehouseOption
): Promise<InventoryOpeningImportResult> {
  const lowerName = file.name.toLowerCase();
  if (!lowerName.endsWith('.csv') && !lowerName.endsWith('.xlsx')) {
    return { rows: [], errors: ['Only CSV and XLSX inventory opening files are supported.'], totalValue: 0 };
  }

  let workbook: ReturnType<typeof XLSX.read>;
  try {
    workbook = XLSX.read(await file.arrayBuffer(), {
      type: 'array', raw: lowerName.endsWith('.csv'), cellDates: false, dateNF: 'yyyy-mm-dd',
    });
  } catch {
    return { rows: [], errors: ['The selected inventory opening file could not be read.'], totalValue: 0 };
  }
  const firstSheet = workbook.SheetNames[0];
  if (!firstSheet) return { rows: [], errors: ['The selected workbook contains no sheets.'], totalValue: 0 };
  const sheetRows = XLSX.utils.sheet_to_json<unknown[]>(workbook.Sheets[firstSheet], {
    header: 1, defval: '', blankrows: false, raw: false, dateNF: 'yyyy-mm-dd',
  });
  if (sheetRows.length > 10001) {
    return { rows: [], errors: ['An inventory opening import cannot exceed 10,000 data rows. Split the schedule into separately referenced governed batches.'], totalValue: 0 };
  }
  const headerIndex = sheetRows.findIndex((row) => row.some((value) => cell(value)));
  if (headerIndex < 0) return { rows: [], errors: ['The selected file is empty.'], totalValue: 0 };

  const columns = sheetRows[headerIndex].map(getColumn);
  const present = new Set(columns.filter(Boolean));
  const required = ['itemCode', 'locationCode', 'quantity', 'unitCost'] as const;
  const missing = required.filter((column) => !present.has(column));
  if (missing.length) return { rows: [], errors: [`Missing required column(s): ${missing.join(', ')}.`], totalValue: 0 };

  const itemByCode = new Map(items.map((item) => [item.itemCode.trim().toUpperCase(), item]));
  const locationByCode = new Map(warehouse.locations.map((location) => [location.code.trim().toUpperCase(), location]));
  const rows: ImportedInventoryOpeningRow[] = [];
  const errors: string[] = [];
  const identities = new Set<string>();
  let totalValue = 0;

  sheetRows.slice(headerIndex + 1).forEach((sourceRow, index) => {
    if (!sourceRow.some((value) => cell(value))) return;
    const rowNumber = headerIndex + index + 2;
    const values = {} as Record<(typeof INVENTORY_OPENING_TEMPLATE_HEADERS)[number], string>;
    columns.forEach((column, columnIndex) => { if (column) values[column] = cell(sourceRow[columnIndex]); });
    const rowErrors: string[] = [];
    const itemCode = (values.itemCode ?? '').toUpperCase();
    const locationCode = (values.locationCode ?? '').toUpperCase();
    const item = itemByCode.get(itemCode);
    const location = locationByCode.get(locationCode);
    const quantity = Number((values.quantity ?? '').replace(/,/g, ''));
    const unitCost = Number((values.unitCost ?? '').replace(/,/g, ''));
    if (!item) rowErrors.push(`Row ${rowNumber}: itemCode '${values.itemCode ?? ''}' is not an eligible active stock item.`);
    if (!location) rowErrors.push(`Row ${rowNumber}: locationCode '${values.locationCode ?? ''}' is not an eligible location in ${warehouse.code}.`);
    if (!Number.isFinite(quantity) || quantity <= 0) rowErrors.push(`Row ${rowNumber}: quantity must be a positive number.`);
    if (!Number.isFinite(unitCost) || unitCost <= 0) rowErrors.push(`Row ${rowNumber}: unitCost must be a positive number.`);
    if (item?.isSerialTracked && !(values.serialNumber ?? '').trim()) rowErrors.push(`Row ${rowNumber}: serialNumber is required for serial-tracked item ${item.itemCode}.`);
    if (item?.isSerialTracked && quantity !== 1) rowErrors.push(`Row ${rowNumber}: serial-tracked item ${item.itemCode} must have quantity 1 per row.`);
    if (item?.isLotTracked && !(values.lotNumber ?? '').trim()) rowErrors.push(`Row ${rowNumber}: lotNumber is required for lot-tracked item ${item.itemCode}.`);
    if (item?.isBatchTracked && !(values.batchNumber ?? '').trim()) rowErrors.push(`Row ${rowNumber}: batchNumber is required for batch-tracked item ${item.itemCode}.`);
    const manufactureDate = normalizeDate(values.manufactureDate ?? '', 'manufactureDate', rowNumber, rowErrors);
    const expiryDate = normalizeDate(values.expiryDate ?? '', 'expiryDate', rowNumber, rowErrors);
    if (manufactureDate && expiryDate && expiryDate < manufactureDate) rowErrors.push(`Row ${rowNumber}: expiryDate cannot be before manufactureDate.`);
    const identity = [item?.id, location?.id, values.serialNumber, values.lotNumber, values.batchNumber]
      .map((value) => (value ?? '').trim().toUpperCase()).join('|');
    if (item && location && identities.has(identity)) rowErrors.push(`Row ${rowNumber}: duplicate item/location/tracking identity in this file.`);
    identities.add(identity);
    if (rowErrors.length || !item || !location) { errors.push(...rowErrors); return; }
    rows.push({
      inventoryItemId: item.id, locationId: location.id,
      quantity: String(quantity), unitCost: String(unitCost),
      serialNumber: values.serialNumber ?? '', lotNumber: values.lotNumber ?? '', batchNumber: values.batchNumber ?? '',
      manufactureDate, expiryDate, notes: values.notes ?? '',
    });
    totalValue += quantity * unitCost;
  });

  if (!rows.length && !errors.length) errors.push('No inventory opening rows were found below the header row.');
  return { rows, errors, totalValue };
}
