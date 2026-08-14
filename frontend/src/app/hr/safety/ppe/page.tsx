'use client';

import { useState } from 'react';
import Link from 'next/link';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { PackagePlus, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyPpeService } from '@/services/hr/safety-ppe.service';
import type { PpeInventory } from '@/types/hr/safety-ppe';

/**
 * The PPE stock register (FR-SHE-131) — inventory items per type with stock, reorder levels and
 * restocking. Items at or below their reorder level are flagged here, counted on the SHE
 * dashboard, and raise a weekly automatic reminder (slice-13 engine). Item codes are
 * immutable; stock arrives through Restock, not by editing the quantity.
 */
const inventorySchema = z.object({
  ppeTypeId: z.string().min(1, 'A PPE type is required'),
  itemCode: z.string().min(1, 'An item code is required').max(50),
  brand: z.string().min(1, 'A brand is required').max(100),
  model: z.string().min(1, 'A model is required').max(100),
  size: z.string().max(20).optional().or(z.literal('')),
  quantityInStock: z.coerce.number().min(0),
  minimumStockLevel: z.coerce.number().min(0),
  reorderLevel: z.coerce.number().min(0),
  storageLocation: z.string().max(200).optional().or(z.literal('')),
  unitCost: z.coerce.number().min(0).optional(),
  supplier: z.string().max(200).optional().or(z.literal('')),
});

type InventoryForm = z.input<typeof inventorySchema>;

const emptyInventory: InventoryForm = {
  ppeTypeId: '',
  itemCode: '',
  brand: '',
  model: '',
  size: '',
  quantityInStock: 0,
  minimumStockLevel: 0,
  reorderLevel: 0,
  storageLocation: '',
  unitCost: undefined,
  supplier: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

/** Records a delivery against an inventory item — the only way stock goes up outside an edit. */
function RestockCard() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [inventoryId, setInventoryId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [restockedById, setRestockedById] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: items = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'inventory'],
    queryFn: () => safetyPpeService.getInventory(),
  });

  const submit = async () => {
    if (!inventoryId || !quantity || !restockedById) return;
    setBusy(true);
    try {
      await safetyPpeService.restock(inventoryId, {
        ppeInventoryId: inventoryId,
        quantity: Number(quantity),
        restockedById,
        restockDate: new Date().toISOString(),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-ppe'] });
      toast({ title: 'Stock replenished' });
      setInventoryId('');
      setQuantity('');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Restock failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <PackagePlus className="h-4 w-4" /> Restock
        </CardTitle>
        <CardDescription>
          Record a delivery against an item — quantity adds to stock and the restocker is kept on
          the item.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex flex-wrap items-end gap-3">
          <div className="min-w-64 space-y-2">
            <Label>Inventory item</Label>
            <Select value={inventoryId} onValueChange={setInventoryId}>
              <SelectTrigger>
                <SelectValue placeholder="Choose an item" />
              </SelectTrigger>
              <SelectContent>
                {items.map((i) => (
                  <SelectItem key={i.id} value={i.id}>
                    {`${i.itemCode} — ${i.ppeTypeName} (${i.brand} ${i.model})`}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="w-32 space-y-2">
            <Label htmlFor="restock-qty">Quantity</Label>
            <Input
              id="restock-qty"
              type="number"
              min={1}
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
            />
          </div>
          <div className="min-w-64 space-y-2">
            <Label>Restocked by</Label>
            <EmployeePicker value={restockedById} onChange={(id) => setRestockedById(id)} />
          </div>
          <Button disabled={busy || !inventoryId || !quantity || !restockedById} onClick={submit}>
            Restock
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/** The standing shortfall list — every item at or below its reorder level. */
function BelowReorderCard() {
  const { data: below = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'inventory', 'below-reorder'],
    queryFn: () => safetyPpeService.getBelowReorderLevel(),
  });

  if (below.length === 0) return null;

  return (
    <Card className="border-destructive/50">
      <CardHeader>
        <CardTitle className="text-base text-destructive">
          Below reorder level ({below.length})
        </CardTitle>
        <CardDescription>
          These items also raise a weekly automatic reminder until restocked. Restock them here.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-2">
        {below.map((i) => (
          <Badge key={i.id} variant="outline" className="border-destructive/50">
            {i.itemCode} · {i.ppeTypeName}: {i.quantityInStock} in stock (reorder at{' '}
            {i.reorderLevel})
          </Badge>
        ))}
      </CardContent>
    </Card>
  );
}

export default function SafetyPpeInventoryPage() {
  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'safety-ppe', 'types', 'active'],
    queryFn: () => safetyPpeService.getTypes(true),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="PPE Stock"
        description="Inventory per PPE type — stock on hand, reorder levels and restocking. Issuance to employees lives on the issuance register."
        backHref="/hr/safety"
        actions={
          <Button variant="outline" asChild>
            <Link href="/hr/safety/ppe/issuances">
              <Users className="mr-2 h-4 w-4" /> Issuance register
            </Link>
          </Button>
        }
      />

      <BelowReorderCard />

      <ResourceListPanel<PpeInventory, InventoryForm>
        title="inventory items"
        singular="inventory item"
        queryKey={['hr', 'safety-ppe', 'inventory']}
        dialogHint="The item code and PPE type are fixed once created. Stock normally moves through Restock, not edits."
        list={() => safetyPpeService.getInventory()}
        create={(values) => {
          const v = inventorySchema.parse(values);
          return safetyPpeService.createInventory({
            ppeTypeId: v.ppeTypeId,
            itemCode: v.itemCode,
            brand: v.brand,
            model: v.model,
            size: blank(v.size),
            quantityInStock: v.quantityInStock,
            minimumStockLevel: v.minimumStockLevel,
            reorderLevel: v.reorderLevel,
            storageLocation: blank(v.storageLocation),
            unitCost: v.unitCost ?? null,
            supplier: blank(v.supplier),
          });
        }}
        update={(id, values) => {
          const v = inventorySchema.parse(values);
          return safetyPpeService.updateInventory(id, {
            id,
            brand: v.brand,
            model: v.model,
            size: blank(v.size),
            quantityInStock: v.quantityInStock,
            minimumStockLevel: v.minimumStockLevel,
            reorderLevel: v.reorderLevel,
            storageLocation: blank(v.storageLocation),
            unitCost: v.unitCost ?? null,
            supplier: blank(v.supplier),
          });
        }}
        remove={(id) => safetyPpeService.removeInventory(id)}
        getId={(i) => i.id}
        emptyDescription="No stock recorded yet. Add the inventory items PPE issuance will draw from."
        columns={[
          { header: 'Item code', cell: (i) => <span className="font-mono">{i.itemCode}</span> },
          { header: 'PPE type', cell: (i) => i.ppeTypeName },
          {
            header: 'Brand / model',
            cell: (i) => (
              <span>
                {i.brand} {i.model}
                {i.size ? ` · ${i.size}` : ''}
              </span>
            ),
          },
          {
            header: 'In stock',
            cell: (i) =>
              i.isBelowReorderLevel ? (
                <Badge variant="destructive">{i.quantityInStock}</Badge>
              ) : (
                <span className="font-medium">{i.quantityInStock}</span>
              ),
          },
          { header: 'Reorder at', cell: (i) => i.reorderLevel },
          {
            header: 'Location',
            cell: (i) =>
              i.storageLocation ?? <span className="text-muted-foreground">—</span>,
          },
          {
            header: 'Last restock',
            cell: (i) =>
              i.lastRestockDate ? (
                <span className="text-sm">
                  {new Date(i.lastRestockDate).toLocaleDateString()}
                  {i.lastRestockedByName ? ` · ${i.lastRestockedByName}` : ''}
                </span>
              ) : (
                <span className="text-muted-foreground">—</span>
              ),
          },
        ]}
        schema={inventorySchema}
        emptyForm={emptyInventory}
        toForm={(i) => ({
          ppeTypeId: i.ppeTypeId,
          itemCode: i.itemCode,
          brand: i.brand,
          model: i.model,
          size: i.size ?? '',
          quantityInStock: i.quantityInStock,
          minimumStockLevel: i.minimumStockLevel,
          reorderLevel: i.reorderLevel,
          storageLocation: i.storageLocation ?? '',
          unitCost: i.unitCost ?? undefined,
          supplier: i.supplier ?? '',
        })}
        renderFields={(form, editing) => (
          <div className="space-y-4">
            {editing ? (
              <p className="text-muted-foreground text-sm">
                Item <span className="font-mono">{form.getValues('itemCode')}</span> — the code and
                PPE type are fixed at creation.
              </p>
            ) : (
              <FieldRow>
                <SelectField
                  form={form}
                  name="ppeTypeId"
                  label="PPE type"
                  required
                  options={types.map((t) => ({ value: t.id, label: `${t.code} — ${t.name}` }))}
                />
                <TextField form={form} name="itemCode" label="Item code" required />
              </FieldRow>
            )}
            <FieldRow>
              <TextField form={form} name="brand" label="Brand" required />
              <TextField form={form} name="model" label="Model" required />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="size" label="Size" />
              <NumberField form={form} name="quantityInStock" label="Quantity in stock" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="minimumStockLevel" label="Minimum stock level" />
              <NumberField form={form} name="reorderLevel" label="Reorder level" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="storageLocation" label="Storage location" />
              <NumberField form={form} name="unitCost" label="Unit cost" />
            </FieldRow>
            <TextField form={form} name="supplier" label="Supplier" />
          </div>
        )}
      />

      <RestockCard />
    </div>
  );
}
