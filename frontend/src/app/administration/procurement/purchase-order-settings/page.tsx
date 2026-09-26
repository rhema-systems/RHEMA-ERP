'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { Loader2, Save, Settings as SettingsIcon } from 'lucide-react';
import procurementSettingsService, { ProcurementSettingsDto, UpdateProcurementSettingsDto } from '@/services/procurementSettingsService';

export default function PurchaseOrderSettingsPage() {
  const { toast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [settings, setSettings] = useState<ProcurementSettingsDto | null>(null);
  const [formData, setFormData] = useState<UpdateProcurementSettingsDto>({
    autoCloseTenders: false,
    enforceSegregationOfDuties: true,
    autoCreateInventoryItems: false,
    autoCreateSupplierItems: false,
    allowNonInventoryItems: true,
    defaultValuationMethod: 'FIFO',
    purchaseRequisitionNumberFormat: 'PR-{YYYY}-{####}',
    purchaseOrderNumberFormat: 'PO-{YYYY}-{####}',
    purchaseOrderReceiptNumberFormat: 'REC{YY}{####}',
    requireApprovalForPO: true,
    allowBackorders: true,
    requireDeliveryDate: true,
    enforceSupplierCatalog: false,
    allowMultipleSuppliersPerItem: true,
    validateBudgetBeforePO: false,
    requireContractForPO: false,
  });

  useEffect(() => {
    loadSettings();
  }, []);

  const loadSettings = async () => {
    try {
      setLoading(true);
      const data = await procurementSettingsService.getSettings();
      setSettings(data);
      setFormData({
        autoCloseTenders: data.autoCloseTenders ?? false,
        enforceSegregationOfDuties: data.enforceSegregationOfDuties ?? true,
        autoCreateInventoryItems: data.autoCreateInventoryItems,
        autoCreateSupplierItems: data.autoCreateSupplierItems,
        allowNonInventoryItems: data.allowNonInventoryItems,
        defaultItemCategoryId: data.defaultItemCategoryId,
        defaultUnitOfMeasureId: data.defaultUnitOfMeasureId,
        defaultValuationMethod: data.defaultValuationMethod || 'FIFO',
        purchaseRequisitionNumberFormat: data.purchaseRequisitionNumberFormat || 'PR-{YYYY}-{####}',
        purchaseOrderNumberFormat: data.purchaseOrderNumberFormat || 'PO-{YYYY}-{####}',
        purchaseOrderReceiptNumberFormat: data.purchaseOrderReceiptNumberFormat || 'REC{YY}{####}',
        requireApprovalForPO: data.requireApprovalForPO,
        autoApprovalThreshold: data.autoApprovalThreshold,
        allowBackorders: data.allowBackorders,
        requireDeliveryDate: data.requireDeliveryDate,
        enforceSupplierCatalog: data.enforceSupplierCatalog,
        allowMultipleSuppliersPerItem: data.allowMultipleSuppliersPerItem,
        validateBudgetBeforePO: data.validateBudgetBeforePO,
        requireContractForPO: data.requireContractForPO,
        notes: data.notes,
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to load procurement settings',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      setSaving(true);
      
      // Validate settings
      if (formData.autoCreateSupplierItems && !formData.autoCreateInventoryItems) {
        toast({
          title: 'Validation Error',
          description: 'Auto-create supplier items can only be enabled when auto-create inventory items is also enabled',
          variant: 'destructive',
        });
        return;
      }

      if (!formData.allowNonInventoryItems && !formData.autoCreateInventoryItems) {
        toast({
          title: 'Validation Error',
          description: 'When non-inventory items are not allowed, auto-create inventory items must be enabled',
          variant: 'destructive',
        });
        return;
      }

      if (formData.autoApprovalThreshold && formData.autoApprovalThreshold <= 0) {
        toast({
          title: 'Validation Error',
          description: 'Auto-approval threshold must be a positive value',
          variant: 'destructive',
        });
        return;
      }

      const updated = await procurementSettingsService.updateSettings(formData);
      setSettings(updated);
      
      toast({
        title: 'Success',
        description: 'Procurement settings updated successfully',
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to update procurement settings',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <SettingsIcon className="h-8 w-8" />
            Procurement Settings
          </h1>
          <p className="text-muted-foreground mt-2">
            Configure tenders, items and purchase orders
          </p>
        </div>
        <Button onClick={handleSave} disabled={saving}>
          {saving ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Save Settings
            </>
          )}
        </Button>
      </div>

      <Card>
        <CardHeader><CardTitle>Segregation of duties</CardTitle></CardHeader>
        <CardContent className="flex items-start justify-between gap-4">
          <div>
            <Label htmlFor="procurement-sod">Enforce segregation of duties for procurement transactions</Label>
            <p className="text-sm text-muted-foreground mt-1">Requires separate participants for procurement, receiving, and procurement-backed invoices and payments. Permissions, approval stages, inspection requirements, and audit records always apply.</p>
            <p className="text-sm text-muted-foreground mt-1">Manual Finance AP, mixed payment batches, HR, and settings-change approval retain their existing controls.</p>
          </div>
          <Switch id="procurement-sod" checked={formData.enforceSegregationOfDuties ?? true}
            onCheckedChange={enforceSegregationOfDuties => setFormData(previous => ({ ...previous, enforceSegregationOfDuties }))} />
        </CardContent>
      </Card>

      {/* Item Creation Settings */}
      <Card>
        <CardHeader><CardTitle>Tender closing</CardTitle></CardHeader>
        <CardContent className="flex items-start justify-between gap-4">
          <div>
            <Label htmlFor="auto-close-tenders">Automatically close tenders when the closing date is reached</Label>
            <p className="text-sm text-muted-foreground mt-1">Checks the submission deadline every minute. Bid opening and evaluation still follow their approval controls.</p>
          </div>
          <Switch id="auto-close-tenders" checked={formData.autoCloseTenders ?? false}
            onCheckedChange={autoCloseTenders => setFormData(previous => ({ ...previous, autoCloseTenders }))} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Item Creation Settings</CardTitle>
          <CardDescription>
            Configure how the system handles new items during purchase order creation
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="flex items-center justify-between space-x-4">
            <div className="flex-1 space-y-1">
              <Label htmlFor="autoCreateInventoryItems">Auto-create inventory items</Label>
              <p className="text-sm text-muted-foreground">
                When enabled, prompt users to create inventory items for new item names
              </p>
            </div>
            <Switch
              id="autoCreateInventoryItems"
              checked={formData.autoCreateInventoryItems}
              onCheckedChange={(checked) =>
                setFormData({ ...formData, autoCreateInventoryItems: checked })
              }
            />
          </div>

          <div className="flex items-center justify-between space-x-4">
            <div className="flex-1 space-y-1">
              <Label htmlFor="autoCreateSupplierItems">Auto-create supplier items</Label>
              <p className="text-sm text-muted-foreground">
                Also create supplier catalog entries (requires auto-create inventory items)
              </p>
            </div>
            <Switch
              id="autoCreateSupplierItems"
              checked={formData.autoCreateSupplierItems}
              onCheckedChange={(checked) =>
                setFormData({ ...formData, autoCreateSupplierItems: checked })
              }
              disabled={!formData.autoCreateInventoryItems}
            />
          </div>

          <div className="flex items-center justify-between space-x-4">
            <div className="flex-1 space-y-1">
              <Label htmlFor="allowNonInventoryItems">Allow non-inventory items</Label>
              <p className="text-sm text-muted-foreground">
                Allow PO items without inventory tracking (one-time purchases)
              </p>
            </div>
            <Switch
              id="allowNonInventoryItems"
              checked={formData.allowNonInventoryItems}
              onCheckedChange={(checked) =>
                setFormData({ ...formData, allowNonInventoryItems: checked })
              }
            />
          </div>
        </CardContent>
      </Card>

      {/* Default Values and Number Format Settings - Side by Side */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Default Values */}
        <Card>
          <CardHeader>
            <CardTitle>Default Values</CardTitle>
            <CardDescription>
              Default values for newly created inventory items
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="defaultValuationMethod">Default Valuation Method</Label>
              <Select
                value={formData.defaultValuationMethod || 'FIFO'}
                onValueChange={(value) =>
                  setFormData({ ...formData, defaultValuationMethod: value })
                }
              >
                <SelectTrigger id="defaultValuationMethod">
                  <SelectValue placeholder="Select valuation method" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="FIFO">FIFO (First In, First Out)</SelectItem>
                  <SelectItem value="WAC">WAC (Weighted Average Cost)</SelectItem>
                  <SelectItem value="Standard">Standard Cost</SelectItem>
                  <SelectItem value="LIFO">LIFO (Last In, First Out)</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>

        {/* Number Format Settings */}
        <Card>
          <CardHeader>
            <CardTitle>Number Format Settings</CardTitle>
            <CardDescription>
              Configure document number formats
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="purchaseRequisitionNumberFormat">PR Number Format</Label>
              <p className="text-sm text-muted-foreground mb-2">
                Use {'{YYYY}'} for year, {'{MM}'} for month, {'{####}'} for sequence
              </p>
              <Input
                id="purchaseRequisitionNumberFormat"
                type="text"
                placeholder="e.g., PR-{YYYY}-{####}"
                value={formData.purchaseRequisitionNumberFormat || ''}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    purchaseRequisitionNumberFormat: e.target.value,
                  })
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="purchaseOrderNumberFormat">PO Number Format</Label>
              <p className="text-sm text-muted-foreground mb-2">
                Use {'{YYYY}'} for year, {'{MM}'} for month, {'{####}'} for sequence
              </p>
              <Input
                id="purchaseOrderNumberFormat"
                type="text"
                placeholder="e.g., PO-{YYYY}-{####}"
                value={formData.purchaseOrderNumberFormat || ''}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    purchaseOrderNumberFormat: e.target.value,
                  })
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="purchaseOrderReceiptNumberFormat">PO Receipt (GRN) Number Format</Label>
              <p className="text-sm text-muted-foreground mb-2">
                Use {'{YYYY}'} / {'{YY}'} / {'{MM}'} / {'{DD}'} and {'{####}'} for sequence
              </p>
              <Input
                id="purchaseOrderReceiptNumberFormat"
                type="text"
                placeholder="e.g., REC{YY}{####} or REC-{YY}-{####}"
                value={formData.purchaseOrderReceiptNumberFormat || ''}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    purchaseOrderReceiptNumberFormat: e.target.value,
                  })
                }
              />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Purchase Order Settings */}
      <Card>
        <CardHeader>
          <CardTitle>Purchase Order Settings</CardTitle>
          <CardDescription>
            General purchase order configuration
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="flex items-center justify-between space-x-4">
            <div className="flex-1 space-y-1">
              <Label htmlFor="requireApprovalForPO">Require approval for PO</Label>
              <p className="text-sm text-muted-foreground">
                All purchase orders must be approved before sending to supplier
              </p>
            </div>
            <Switch
              id="requireApprovalForPO"
              checked={formData.requireApprovalForPO}
              onCheckedChange={(checked) =>
                setFormData({ ...formData, requireApprovalForPO: checked })
              }
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="autoApprovalThreshold">Auto-approval threshold</Label>
            <p className="text-sm text-muted-foreground mb-2">
              Automatically approve POs below this amount (leave empty to disable)
            </p>
            <Input
              id="autoApprovalThreshold"
              type="number"
              step="0.01"
              min="0"
              placeholder="e.g., 10000.00"
              value={formData.autoApprovalThreshold || ''}
              onChange={(e) =>
                setFormData({
                  ...formData,
                  autoApprovalThreshold: e.target.value ? parseFloat(e.target.value) : undefined,
                })
              }
            />
          </div>

          <div className="flex items-center justify-between space-x-4">
            <div className="flex-1 space-y-1">
              <Label htmlFor="allowBackorders">Allow backorders</Label>
              <p className="text-sm text-muted-foreground">
                Allow receiving more than ordered quantity
              </p>
            </div>
            <Switch
              id="allowBackorders"
              checked={formData.allowBackorders}
              onCheckedChange={(checked) =>
                setFormData({ ...formData, allowBackorders: checked })
              }
            />
          </div>

          <div className="flex items-center justify-between space-x-4">
            <div className="flex-1 space-y-1">
              <Label htmlFor="requireDeliveryDate">Require delivery date</Label>
              <p className="text-sm text-muted-foreground">
                Make delivery date mandatory on purchase orders
              </p>
            </div>
            <Switch
              id="requireDeliveryDate"
              checked={formData.requireDeliveryDate}
              onCheckedChange={(checked) =>
                setFormData({ ...formData, requireDeliveryDate: checked })
              }
            />
          </div>
        </CardContent>
      </Card>

      {/* Supplier Settings and Validation Settings - Side by Side */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Supplier Settings */}
        <Card>
          <CardHeader>
            <CardTitle>Supplier Settings</CardTitle>
            <CardDescription>
              Configure supplier-related options
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="flex items-center justify-between space-x-4">
              <div className="flex-1 space-y-1">
                <Label htmlFor="enforceSupplierCatalog">Enforce supplier catalog</Label>
                <p className="text-sm text-muted-foreground">
                  Only allow items from supplier's catalog
                </p>
              </div>
              <Switch
                id="enforceSupplierCatalog"
                checked={formData.enforceSupplierCatalog}
                onCheckedChange={(checked) =>
                  setFormData({ ...formData, enforceSupplierCatalog: checked })
                }
              />
            </div>

            <div className="flex items-center justify-between space-x-4">
              <div className="flex-1 space-y-1">
                <Label htmlFor="allowMultipleSuppliersPerItem">Allow multiple suppliers per item</Label>
                <p className="text-sm text-muted-foreground">
                  Allow multiple suppliers for the same inventory item
                </p>
              </div>
              <Switch
                id="allowMultipleSuppliersPerItem"
                checked={formData.allowMultipleSuppliersPerItem}
                onCheckedChange={(checked) =>
                  setFormData({ ...formData, allowMultipleSuppliersPerItem: checked })
                }
              />
            </div>
          </CardContent>
        </Card>

        {/* Validation Settings */}
        <Card>
          <CardHeader>
            <CardTitle>Validation Settings</CardTitle>
            <CardDescription>
              Configure validation rules for purchase orders
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="flex items-center justify-between space-x-4">
              <div className="flex-1 space-y-1">
                <Label htmlFor="validateBudgetBeforePO">Validate budget before PO</Label>
                <p className="text-sm text-muted-foreground">
                  Check budget availability before creating purchase orders
                </p>
              </div>
              <Switch
                id="validateBudgetBeforePO"
                checked={formData.validateBudgetBeforePO}
                onCheckedChange={(checked) =>
                  setFormData({ ...formData, validateBudgetBeforePO: checked })
                }
              />
            </div>

            <div className="flex items-center justify-between space-x-4">
              <div className="flex-1 space-y-1">
                <Label htmlFor="requireContractForPO">Require contract for PO</Label>
                <p className="text-sm text-muted-foreground">
                  Require contract reference for all purchase orders
                </p>
              </div>
              <Switch
                id="requireContractForPO"
                checked={formData.requireContractForPO}
                onCheckedChange={(checked) =>
                  setFormData({ ...formData, requireContractForPO: checked })
                }
              />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Notes */}
      <Card>
        <CardHeader>
          <CardTitle>Notes</CardTitle>
          <CardDescription>
            Additional notes or comments about these settings
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Textarea
            placeholder="Enter any additional notes..."
            value={formData.notes || ''}
            onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
            rows={4}
          />
        </CardContent>
      </Card>

      {/* Save Button (Bottom) */}
      <div className="flex justify-end">
        <Button onClick={handleSave} disabled={saving} size="lg">
          {saving ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Save Settings
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
