import type {
  CreatePurchaseOrderItemDto,
  ProcurementPurchaseOrderSourceLineDto,
} from '@/services/purchasingService';
import type { ItemUnitOfMeasureDto } from '@/services/inventoryManagementService';

export interface ApprovedPurchaseOrderItem extends CreatePurchaseOrderItemDto {
  tempId: string;
  approvedSourceLineId?: string;
  itemCode?: string;
  itemName?: string;
}

const normalize = (value?: string) => (value || '').trim().replace(/\s+/g, ' ').toUpperCase();
const emptyId = '00000000-0000-0000-0000-000000000000';

export function supportsApprovedPurchaseOrderUnit(
  line: ProcurementPurchaseOrderSourceLineDto,
  inventoryBaseUnit: string,
  units: ItemUnitOfMeasureDto[],
): boolean {
  const approvedUnit = normalize(line.unitOfMeasure || 'EA');
  return approvedUnit === normalize(inventoryBaseUnit) || units.some(unit =>
    normalize(unit.unitCode) === approvedUnit &&
    Boolean(unit.unitOfMeasureId) && unit.unitOfMeasureId !== emptyId,
  );
}

export function createApprovedPurchaseOrderItems(
  lines: ProcurementPurchaseOrderSourceLineDto[],
  expectedDeliveryDate = '',
): ApprovedPurchaseOrderItem[] {
  return lines.map((line, index) => ({
    tempId: `source-${line.sourceLineId || index}`,
    approvedSourceLineId: line.sourceLineId,
    inventoryItemId: line.inventoryItemId || '',
    itemCode: line.itemCode,
    itemName: line.description,
    itemDescription: line.description,
    orderedQuantity: line.quantity,
    unitOfMeasure: line.unitOfMeasure || 'EA',
    unitPrice: line.unitPrice,
    expectedDeliveryDate,
    notes: '',
  }));
}

export function findApprovedPurchaseOrderLine(
  lines: ProcurementPurchaseOrderSourceLineDto[],
  item: Pick<ApprovedPurchaseOrderItem, 'approvedSourceLineId' | 'itemDescription'> | null,
): ProcurementPurchaseOrderSourceLineDto | undefined {
  if (!item) return undefined;
  if (item.approvedSourceLineId) {
    return lines.find(line => line.sourceLineId === item.approvedSourceLineId);
  }
  // Existing/manual rows can be recovered only by an unambiguous approved
  // description. Never infer commercial identity from a catalogue name/code.
  if (!normalize(item.itemDescription)) return undefined;
  const matches = lines.filter(line => normalize(line.description) === normalize(item.itemDescription));
  return matches.length === 1 ? matches[0] : undefined;
}

export function retainApprovedPurchaseOrderTerms<T extends ApprovedPurchaseOrderItem>(
  item: T,
  line: ProcurementPurchaseOrderSourceLineDto | undefined,
  units: ItemUnitOfMeasureDto[] = [],
): T {
  if (!line) return item;
  const unit = line.unitOfMeasure || 'EA';
  const matchingUnit = units.find(candidate => normalize(candidate.unitCode) === normalize(unit));
  return {
    ...item,
    approvedSourceLineId: line.sourceLineId,
    itemDescription: line.description,
    unitOfMeasure: unit,
    unitPrice: line.unitPrice,
    // Do not attach EA's conversion record to an approved EACH line (or vice
    // versa). No unit aliases/conversions or new master data are invented here.
    itemUnitOfMeasureId: matchingUnit?.unitOfMeasureId && matchingUnit.unitOfMeasureId !== emptyId
      ? matchingUnit.id
      : undefined,
    priceListLineId: undefined,
  };
}
