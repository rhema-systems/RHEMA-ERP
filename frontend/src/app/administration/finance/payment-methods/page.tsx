'use client';

import { useCallback, useEffect, useState } from 'react';
import { CreditCard, Edit, Plus, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { PaymentMethod, PaymentMethodType } from '@/types/cash-management';

type PaymentMethodForm = {
  name: string;
  code: string;
  type: PaymentMethodType;
  description: string;
  isActive: boolean;
  requiresBankAccount: boolean;
  requiresReference: boolean;
  defaultGLAccountId?: string;
};

const emptyForm: PaymentMethodForm = {
  name: '',
  code: '',
  type: PaymentMethodType.Cash,
  description: '',
  isActive: true,
  requiresBankAccount: false,
  requiresReference: false,
};

const paymentMethodTypes = Object.values(PaymentMethodType);

export default function PaymentMethodsPage() {
  const [methods, setMethods] = useState<PaymentMethod[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingMethod, setEditingMethod] = useState<PaymentMethod | null>(null);
  const [form, setForm] = useState<PaymentMethodForm>(emptyForm);

  const loadMethods = useCallback(async () => {
    try {
      setLoading(true);
      setMethods(await cashManagementDataService.getPaymentMethods());
    } catch (error) {
      console.error('Failed to load payment methods:', error);
      toast.error('Failed to load payment methods');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadMethods();
  }, [loadMethods]);

  const openCreateDialog = () => {
    setEditingMethod(null);
    setForm(emptyForm);
    setDialogOpen(true);
  };

  const openEditDialog = (method: PaymentMethod) => {
    setEditingMethod(method);
    setForm({
      name: method.name,
      code: method.code ?? '',
      type: method.type,
      description: method.description ?? '',
      isActive: method.isActive,
      requiresBankAccount: method.requiresBankAccount,
      requiresReference: method.requiresReference,
      defaultGLAccountId: method.defaultGLAccountId,
    });
    setDialogOpen(true);
  };

  const saveMethod = async () => {
    if (!form.name.trim()) {
      toast.error('Payment method name is required');
      return;
    }

    try {
      setSaving(true);
      const payload = {
        ...form,
        name: form.name.trim(),
        code: form.code.trim() || undefined,
        description: form.description.trim() || undefined,
      };

      if (editingMethod) {
        await cashManagementDataService.updatePaymentMethod(editingMethod.id, payload);
        toast.success('Payment method updated');
      } else {
        await cashManagementDataService.createPaymentMethod(payload);
        toast.success('Payment method created');
      }

      setDialogOpen(false);
      await loadMethods();
    } catch (error) {
      console.error('Failed to save payment method:', error);
      toast.error('Failed to save payment method');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
            <CreditCard className="h-8 w-8" />
            Payment Methods
          </h1>
          <p className="text-muted-foreground">Configure payment options used by cash, receivables, and payables.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="icon" onClick={() => void loadMethods()} disabled={loading} title="Refresh payment methods">
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          </Button>
          <Button onClick={openCreateDialog}>
            <Plus className="mr-2 h-4 w-4" />
            New Payment Method
          </Button>
        </div>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/administration">Administration</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Payment Methods</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <Card>
        <CardHeader>
          <CardTitle>Configured Methods ({methods.length})</CardTitle>
          <CardDescription>Reference and bank-account requirements are enforced when these methods are selected.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full">
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="p-3 text-left font-medium">Code</th>
                  <th className="p-3 text-left font-medium">Name</th>
                  <th className="p-3 text-left font-medium">Type</th>
                  <th className="p-3 text-left font-medium">Requirements</th>
                  <th className="p-3 text-left font-medium">Status</th>
                  <th className="p-3 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr><td colSpan={6} className="p-8 text-center text-muted-foreground">Loading payment methods...</td></tr>
                ) : methods.length === 0 ? (
                  <tr><td colSpan={6} className="p-8 text-center text-muted-foreground">No payment methods configured.</td></tr>
                ) : methods.map((method) => (
                  <tr key={method.id} className="border-b last:border-0">
                    <td className="p-3 font-mono">{method.code || '-'}</td>
                    <td className="p-3">
                      <div className="font-medium">{method.name}</div>
                      {method.description && <div className="text-sm text-muted-foreground">{method.description}</div>}
                    </td>
                    <td className="p-3">{method.type}</td>
                    <td className="p-3">
                      <div className="flex flex-wrap gap-1">
                        {method.requiresBankAccount && <Badge variant="outline">Bank account</Badge>}
                        {method.requiresReference && <Badge variant="outline">Reference</Badge>}
                        {!method.requiresBankAccount && !method.requiresReference && <span className="text-muted-foreground">None</span>}
                      </div>
                    </td>
                    <td className="p-3"><Badge variant={method.isActive ? 'default' : 'secondary'}>{method.isActive ? 'Active' : 'Inactive'}</Badge></td>
                    <td className="p-3 text-right">
                      <Button variant="ghost" size="icon" onClick={() => openEditDialog(method)} title={`Edit ${method.name}`}>
                        <Edit className="h-4 w-4" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingMethod ? 'Edit Payment Method' : 'New Payment Method'}</DialogTitle>
            <DialogDescription>Define how the method is identified and which supporting details are mandatory.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="payment-method-name">Name</Label>
                <Input id="payment-method-name" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="payment-method-code">Code</Label>
                <Input id="payment-method-code" value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value.toUpperCase() })} />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="payment-method-type">Type</Label>
              <Select value={form.type} onValueChange={(value: PaymentMethodType) => setForm({ ...form, type: value })}>
                <SelectTrigger id="payment-method-type"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {paymentMethodTypes.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="payment-method-description">Description</Label>
              <Textarea id="payment-method-description" value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} rows={3} />
            </div>
            <div className="flex items-center justify-between gap-4 rounded-md border p-3">
              <Label htmlFor="payment-method-active">Active</Label>
              <Switch id="payment-method-active" checked={form.isActive} onCheckedChange={(checked) => setForm({ ...form, isActive: checked })} />
            </div>
            <div className="flex items-center justify-between gap-4 rounded-md border p-3">
              <Label htmlFor="requires-bank-account">Require bank account</Label>
              <Switch id="requires-bank-account" checked={form.requiresBankAccount} onCheckedChange={(checked) => setForm({ ...form, requiresBankAccount: checked })} />
            </div>
            <div className="flex items-center justify-between gap-4 rounded-md border p-3">
              <Label htmlFor="requires-reference">Require transaction reference</Label>
              <Switch id="requires-reference" checked={form.requiresReference} onCheckedChange={(checked) => setForm({ ...form, requiresReference: checked })} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button onClick={() => void saveMethod()} disabled={saving}>{saving ? 'Saving...' : 'Save Payment Method'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
