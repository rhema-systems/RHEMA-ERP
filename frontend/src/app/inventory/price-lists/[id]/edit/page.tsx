'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Save } from 'lucide-react';
import { toast } from 'sonner';
import { priceListService, PriceListDto, UpdatePriceListDto, getPriceListTypeLabel } from '@/services/priceListService';

export default function EditPriceListPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [priceList, setPriceList] = useState<PriceListDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [formData, setFormData] = useState<UpdatePriceListDto>({
    name: '', description: '', currency: 'USD', effectiveDate: '', expirationDate: undefined,
    isDefault: false, priority: 0, customerGroupId: undefined, supplierGroupId: undefined, notes: ''
  });

  useEffect(() => { loadPriceList(); }, [id]);

  const loadPriceList = async () => {
    try {
      setLoading(true);
      const data = await priceListService.getPriceListById(id);
      setPriceList(data);
      setFormData({
        name: data.name,
        description: data.description || '',
        currency: data.currency,
        effectiveDate: data.effectiveDate ? data.effectiveDate.split('T')[0] : new Date().toISOString().split('T')[0],
        expirationDate: data.expirationDate ? data.expirationDate.split('T')[0] : undefined,
        isDefault: data.isDefault,
        priority: data.priority,
        customerGroupId: data.customerGroupId,
        supplierGroupId: data.supplierGroupId,
        notes: data.notes || ''
      });
    } catch (error) {
      console.error('Error loading price list:', error);
      toast.error('Failed to load price list');
      router.push('/inventory/price-lists');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      setSaving(true);
      await priceListService.updatePriceList(id, formData);
      toast.success('Price list updated successfully');
      router.push(`/inventory/price-lists/${id}`);
    } catch (error) {
      console.error('Error updating price list:', error);
      toast.error('Failed to update price list');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <div className="flex items-center justify-center h-64">Loading...</div>;
  if (!priceList) return <div className="flex items-center justify-center h-64">Price list not found</div>;

  return (
    <div className="space-y-6">
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory/price-lists">Price Lists</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href={`/inventory/price-lists/${id}`}>{priceList.priceListCode}</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Edit</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.push(`/inventory/price-lists/${id}`)}><ArrowLeft className="h-4 w-4" /></Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Edit Price List</h1>
            <p className="text-muted-foreground">{priceList.priceListCode} • {getPriceListTypeLabel(priceList.type)}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => router.push(`/inventory/price-lists/${id}`)}>Cancel</Button>
          <Button onClick={handleSave} disabled={saving || !formData.name}><Save className="mr-2 h-4 w-4" />{saving ? 'Saving...' : 'Save Changes'}</Button>
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Basic Information</CardTitle><CardDescription>Update the price list details</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name *</Label>
              <Input id="name" value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" value={formData.description || ''} onChange={(e) => setFormData({...formData, description: e.target.value})} rows={3} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="currency">Currency</Label>
              <Select value={formData.currency} onValueChange={(v) => setFormData({...formData, currency: v})}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="USD">USD</SelectItem>
                  <SelectItem value="EUR">EUR</SelectItem>
                  <SelectItem value="GBP">GBP</SelectItem>
                  <SelectItem value="ZAR">ZAR</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Validity Period</CardTitle><CardDescription>Set when this price list is effective</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="effectiveDate">Effective Date *</Label>
              <Input id="effectiveDate" type="date" value={formData.effectiveDate} onChange={(e) => setFormData({...formData, effectiveDate: e.target.value})} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="expirationDate">Expiration Date</Label>
              <Input id="expirationDate" type="date" value={formData.expirationDate || ''} onChange={(e) => setFormData({...formData, expirationDate: e.target.value || undefined})} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="priority">Priority</Label>
              <Input id="priority" type="number" value={formData.priority} onChange={(e) => setFormData({...formData, priority: parseInt(e.target.value) || 0})} />
              <p className="text-sm text-muted-foreground">Higher priority lists take precedence when multiple apply</p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Settings</CardTitle><CardDescription>Additional configuration options</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center justify-between">
              <div><Label>Default Price List</Label><p className="text-sm text-muted-foreground">Use as the default for this type</p></div>
              <Switch checked={formData.isDefault} onCheckedChange={(v) => setFormData({...formData, isDefault: v})} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Notes</CardTitle><CardDescription>Additional information</CardDescription></CardHeader>
          <CardContent>
            <Textarea value={formData.notes || ''} onChange={(e) => setFormData({...formData, notes: e.target.value})} rows={4} placeholder="Enter any additional notes..." />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

