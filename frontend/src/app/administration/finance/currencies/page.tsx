'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { 
  Plus,
  Search,
  Edit,
  Trash2,
  DollarSign,
  Globe,
  Settings
} from 'lucide-react';
import {
  currencyService,
  CurrencyListDto as CurrencyDto,
  CreateCurrencyDto,
  UpdateCurrencyDto
} from '@/services/financeCommonService';
import { toast } from 'sonner';

export default function CurrenciesPage() {
  const [currencies, setCurrencies] = useState<CurrencyDto[]>([]);
  const [filteredCurrencies, setFilteredCurrencies] = useState<CurrencyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
 
  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedCurrency, setSelectedCurrency] = useState<CurrencyDto | null>(null);
 
  // Form state
  const [formData, setFormData] = useState<CreateCurrencyDto>({
    code: '',
    name: '',
    symbol: '',
    decimalPlaces: 2,
    exchangeRate: 1,
    exchangeRateDate: new Date(),
    isBaseCurrency: false,
    isActive: true,
    displayOrder: 0,
    formatString: '',
    country: ''
  });

  // Fetch currencies
  const fetchCurrencies = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await currencyService.getAll();
      setCurrencies(data);
    } catch (err: any) {
      console.error('Error fetching currencies:', err);
      setError('Failed to load currencies');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCurrencies();
  }, []);

  // Filter currencies
  useEffect(() => {
    if (!currencies) {
      setFilteredCurrencies([]);
      return;
    }

    // Filter out any undefined/null values first
    let filtered = currencies.filter((c): c is CurrencyDto => c != null);

    if (searchTerm) {
      filtered = filtered.filter(c =>
        c.code?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        c.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        c.country?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(c =>
        statusFilter === 'active' ? c.isActive : !c.isActive
      );
    }

    setFilteredCurrencies(filtered);
  }, [searchTerm, statusFilter, currencies]);

  const handleCreate = async () => {
    try {
      const newCurrency = await currencyService.create(formData);
      setCurrencies(prev => [...(prev || []), newCurrency]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (err: any) {
      console.error('Error creating currency:', err);
      toast.error('Failed to create currency');
    }
  };

  const handleEdit = (currency: CurrencyDto) => {
    setSelectedCurrency(currency);
    setFormData({
      code: currency.code,
      name: currency.name,
      symbol: currency.symbol,
      decimalPlaces: currency.decimalPlaces,
      exchangeRate: currency.exchangeRate,
      exchangeRateDate: currency.exchangeRateDate ? new Date(currency.exchangeRateDate) : new Date(),
      isBaseCurrency: currency.isBaseCurrency,
      isActive: currency.isActive,
      displayOrder: currency.displayOrder,
      formatString: currency.formatString || '',
      country: currency.country || ''
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedCurrency) return;
    try {
      const updateData: UpdateCurrencyDto = {
        id: selectedCurrency.id,
        code: selectedCurrency.code,
        name: formData.name,
        symbol: formData.symbol,
        decimalPlaces: formData.decimalPlaces,
        exchangeRate: formData.exchangeRate,
        exchangeRateDate: formData.exchangeRateDate,
        isBaseCurrency: formData.isBaseCurrency,
        isActive: formData.isActive,
        displayOrder: formData.displayOrder,
        formatString: formData.formatString,
        country: formData.country
      };
      const updated = await currencyService.update(selectedCurrency.id, updateData);
      setCurrencies(prev => (prev || []).map(c => c.id === selectedCurrency.id ? updated : c));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (err: any) {
      console.error('Error updating currency:', err);
      toast.error('Failed to update currency');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this currency?')) return;
    try {
      await currencyService.delete(id);
      setCurrencies(prev => (prev || []).filter(c => c.id !== id));
    } catch (err: any) {
      console.error('Error deleting currency:', err);
      toast.error('Failed to delete currency');
    }
  };

  const resetForm = () => {
    setFormData({
      code: '',
      name: '',
      symbol: '',
      decimalPlaces: 2,
      exchangeRate: 1,
      exchangeRateDate: new Date(),
      isBaseCurrency: false,
      isActive: true,
      displayOrder: 0,
      formatString: '',
      country: ''
    });
    setSelectedCurrency(null);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Currencies</h1>
          <p className="text-muted-foreground">
            Manage currencies and exchange rates for financial transactions
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Currency</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[500px]">
            <DialogHeader>
              <DialogTitle>Add Currency</DialogTitle>
              <DialogDescription>Create a new currency configuration.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="code">Code</Label>
                  <Input id="code" value={formData.code} onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})} placeholder="e.g., USD, EUR" />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="symbol">Symbol</Label>
                  <Input id="symbol" value={formData.symbol} onChange={(e) => setFormData({...formData, symbol: e.target.value})} placeholder="e.g., $, €" />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="name">Name</Label>
                <Input id="name" value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} placeholder="e.g., US Dollar, Euro" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="country">Country</Label>
                <Input id="country" value={formData.country} onChange={(e) => setFormData({...formData, country: e.target.value})} placeholder="e.g., United States, Germany" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="decimalPlaces">Decimal Places</Label>
                  <Input id="decimalPlaces" type="number" value={formData.decimalPlaces} onChange={(e) => setFormData({...formData, decimalPlaces: parseInt(e.target.value) || 0})} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="displayOrder">Display Order</Label>
                  <Input id="displayOrder" type="number" value={formData.displayOrder} onChange={(e) => setFormData({...formData, displayOrder: parseInt(e.target.value) || 0})} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="exchangeRate">Exchange Rate</Label>
                  <Input id="exchangeRate" type="number" step="0.000001" value={formData.exchangeRate} onChange={(e) => setFormData({...formData, exchangeRate: parseFloat(e.target.value) || 1})} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="exchangeRateDate">Rate Date</Label>
                  <Input id="exchangeRateDate" type="date" value={formData.exchangeRateDate instanceof Date ? formData.exchangeRateDate.toISOString().split('T')[0] : new Date().toISOString().split('T')[0]} onChange={(e) => setFormData({...formData, exchangeRateDate: new Date(e.target.value)})} />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="formatString">Format String</Label>
                <Input id="formatString" value={formData.formatString} onChange={(e) => setFormData({...formData, formatString: e.target.value})} placeholder="e.g., {0:N2}" />
              </div>
              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch id="isBaseCurrency" checked={formData.isBaseCurrency} onCheckedChange={(v) => setFormData({...formData, isBaseCurrency: v})} />
                  <Label htmlFor="isBaseCurrency">Base Currency</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch id="isActive" checked={formData.isActive} onCheckedChange={(v) => setFormData({...formData, isActive: v})} />
                  <Label htmlFor="isActive">Active</Label>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>Cancel</Button>
              <Button onClick={handleCreate}>Create</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/administration">Administration</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Currencies</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{(currencies || []).filter(c => c != null).length}</p>
                <p className="text-sm text-muted-foreground">Total Currencies</p>
              </div>
              <DollarSign className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{(currencies || []).filter(c => c != null && c.isActive).length}</p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <Settings className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{(currencies || []).filter(c => c != null && c.isBaseCurrency).length}</p>
                <p className="text-sm text-muted-foreground">Base Currencies</p>
              </div>
              <Globe className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search currencies..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="inactive">Inactive</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Currencies List */}
      <Card>
        <CardHeader>
          <CardTitle>Currencies</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${(filteredCurrencies || []).length} currency/currencies found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {filteredCurrencies.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No currencies found.</div>
              ) : (
                filteredCurrencies.map((currency) => (
                  <div key={currency.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-blue-100 flex items-center justify-center">
                          <span className="text-lg font-bold text-blue-600">{currency.symbol || currency.code || ''}</span>
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{currency.name || ''}</h3>
                            <Badge variant="outline">{currency.code || ''}</Badge>
                            <Badge className={currency.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                              {currency.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                            {currency.isBaseCurrency && <Badge className="bg-purple-100 text-purple-800">Base</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">Exchange Rate: {currency.exchangeRate ?? 1} | Decimal Places: {currency.decimalPlaces ?? 2}</p>
                          {currency.country && <p className="text-sm text-muted-foreground">{currency.country}</p>}
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleEdit(currency)}><Edit className="h-4 w-4" /></Button>
                        <Button size="sm" variant="outline" className="text-red-600" onClick={() => handleDelete(currency.id)}><Trash2 className="h-4 w-4" /></Button>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[500px]">
          <DialogHeader>
            <DialogTitle>Edit Currency</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Code</Label>
                <Input value={formData.code} disabled className="bg-muted" />
              </div>
              <div className="space-y-2">
                <Label>Symbol</Label>
                <Input value={formData.symbol} onChange={(e) => setFormData({...formData, symbol: e.target.value})} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} />
            </div>
            <div className="space-y-2">
              <Label>Country</Label>
              <Input value={formData.country} onChange={(e) => setFormData({...formData, country: e.target.value})} />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Decimal Places</Label>
                <Input type="number" value={formData.decimalPlaces} onChange={(e) => setFormData({...formData, decimalPlaces: parseInt(e.target.value) || 0})} />
              </div>
              <div className="space-y-2">
                <Label>Display Order</Label>
                <Input type="number" value={formData.displayOrder} onChange={(e) => setFormData({...formData, displayOrder: parseInt(e.target.value) || 0})} />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Exchange Rate</Label>
                <Input type="number" step="0.000001" value={formData.exchangeRate} onChange={(e) => setFormData({...formData, exchangeRate: parseFloat(e.target.value) || 1})} />
              </div>
              <div className="space-y-2">
                <Label>Rate Date</Label>
                <Input type="date" value={formData.exchangeRateDate instanceof Date ? formData.exchangeRateDate.toISOString().split('T')[0] : new Date().toISOString().split('T')[0]} onChange={(e) => setFormData({...formData, exchangeRateDate: new Date(e.target.value)})} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Format String</Label>
              <Input value={formData.formatString} onChange={(e) => setFormData({...formData, formatString: e.target.value})} />
            </div>
            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <Switch checked={formData.isBaseCurrency} onCheckedChange={(v) => setFormData({...formData, isBaseCurrency: v})} />
                <Label>Base Currency</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch checked={formData.isActive} onCheckedChange={(v) => setFormData({...formData, isActive: v})} />
                <Label>Active</Label>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleUpdate}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
