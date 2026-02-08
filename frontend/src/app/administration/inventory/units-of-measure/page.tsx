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
  Ruler,
  ArrowRightLeft,
  Settings
} from 'lucide-react';
import { 
  inventoryManagementService, 
  UnitOfMeasureDto, 
  CreateUnitOfMeasureDto, 
  UpdateUnitOfMeasureDto,
  UnitOfMeasureConversionDto,
  CreateUnitOfMeasureConversionDto
} from '@/services/inventoryManagementService';
import { toast } from 'sonner';

export default function UnitsOfMeasurePage() {
  const [units, setUnits] = useState<UnitOfMeasureDto[]>([]);
  const [filteredUnits, setFilteredUnits] = useState<UnitOfMeasureDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  
  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isConversionDialogOpen, setIsConversionDialogOpen] = useState(false);
  const [selectedUnit, setSelectedUnit] = useState<UnitOfMeasureDto | null>(null);
  const [conversions, setConversions] = useState<UnitOfMeasureConversionDto[]>([]);
  
  // Form state
  const [formData, setFormData] = useState<CreateUnitOfMeasureDto>({
    code: '',
    name: '',
    symbol: '',
    category: 'Quantity',
    isBaseUnit: false,
    sortOrder: 0
  });

  const [conversionForm, setConversionForm] = useState<CreateUnitOfMeasureConversionDto>({
    fromUnitId: '',
    toUnitId: '',
    conversionFactor: 1
  });

  const categories = ['Quantity', 'Weight', 'Volume', 'Length', 'Area', 'Time', 'Temperature'];

  // Fetch units
  const fetchUnits = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await inventoryManagementService.getUnitsOfMeasure();
      setUnits(data);
    } catch (err: any) {
      console.error('Error fetching units:', err);
      setError('Failed to load units of measure');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchUnits();
  }, []);

  // Filter units
  useEffect(() => {
    let filtered = units;

    if (searchTerm) {
      filtered = filtered.filter(u =>
        u.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        u.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        u.symbol?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (categoryFilter !== 'all') {
      filtered = filtered.filter(u => u.category === categoryFilter);
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(u => 
        statusFilter === 'active' ? u.isActive : !u.isActive
      );
    }

    setFilteredUnits(filtered);
  }, [searchTerm, categoryFilter, statusFilter, units]);

  const handleCreate = async () => {
    try {
      const newUnit = await inventoryManagementService.createUnitOfMeasure(formData);
      setUnits(prev => [...prev, newUnit]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (err: any) {
      console.error('Error creating unit:', err);
      toast.error('Failed to create unit of measure');
    }
  };

  const handleEdit = (unit: UnitOfMeasureDto) => {
    setSelectedUnit(unit);
    setFormData({
      code: unit.code,
      name: unit.name,
      symbol: unit.symbol || '',
      category: unit.category || 'Quantity',
      isBaseUnit: unit.isBaseUnit,
      sortOrder: unit.sortOrder
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedUnit) return;
    try {
      const updateData: UpdateUnitOfMeasureDto = {
        name: formData.name,
        symbol: formData.symbol,
        category: formData.category,
        isBaseUnit: formData.isBaseUnit,
        isActive: selectedUnit.isActive,
        sortOrder: formData.sortOrder
      };
      const updated = await inventoryManagementService.updateUnitOfMeasure(selectedUnit.id, updateData);
      setUnits(prev => prev.map(u => u.id === selectedUnit.id ? updated : u));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (err: any) {
      console.error('Error updating unit:', err);
      toast.error('Failed to update unit of measure');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this unit of measure?')) return;
    try {
      await inventoryManagementService.deleteUnitOfMeasure(id);
      setUnits(prev => prev.filter(u => u.id !== id));
    } catch (err: any) {
      console.error('Error deleting unit:', err);
      toast.error('Failed to delete unit of measure');
    }
  };

  const resetForm = () => {
    setFormData({
      code: '',
      name: '',
      symbol: '',
      category: 'Quantity',
      isBaseUnit: false,
      sortOrder: 0
    });
    setSelectedUnit(null);
  };

  const openConversions = async (unit: UnitOfMeasureDto) => {
    setSelectedUnit(unit);
    try {
      const convs = await inventoryManagementService.getUnitConversions(unit.id);
      setConversions(convs);
      setConversionForm({ fromUnitId: unit.id, toUnitId: '', conversionFactor: 1 });
      setIsConversionDialogOpen(true);
    } catch (err) {
      console.error('Error fetching conversions:', err);
    }
  };

  const handleCreateConversion = async () => {
    try {
      const newConv = await inventoryManagementService.createUnitConversion(conversionForm);
      setConversions(prev => [...prev, newConv]);
      setConversionForm({ ...conversionForm, toUnitId: '', conversionFactor: 1 });
    } catch (err: any) {
      console.error('Error creating conversion:', err);
      toast.error('Failed to create conversion');
    }
  };

  const handleDeleteConversion = async (id: string) => {
    try {
      await inventoryManagementService.deleteUnitConversion(id);
      setConversions(prev => prev.filter(c => c.id !== id));
    } catch (err: any) {
      console.error('Error deleting conversion:', err);
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Units of Measure</h1>
          <p className="text-muted-foreground">
            Manage units of measure and conversions for inventory items
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Unit</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[500px]">
            <DialogHeader>
              <DialogTitle>Add Unit of Measure</DialogTitle>
              <DialogDescription>Create a new unit of measure for inventory items.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="code">Code</Label>
                  <Input id="code" value={formData.code} onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})} placeholder="e.g., EA, KG" />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="symbol">Symbol</Label>
                  <Input id="symbol" value={formData.symbol} onChange={(e) => setFormData({...formData, symbol: e.target.value})} placeholder="e.g., kg, m" />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="name">Name</Label>
                <Input id="name" value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} placeholder="e.g., Each, Kilogram" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="category">Category</Label>
                  <Select value={formData.category} onValueChange={(v) => setFormData({...formData, category: v})}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {categories.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="sortOrder">Sort Order</Label>
                  <Input id="sortOrder" type="number" value={formData.sortOrder} onChange={(e) => setFormData({...formData, sortOrder: parseInt(e.target.value) || 0})} />
                </div>
              </div>
              <div className="flex items-center space-x-2">
                <Switch id="isBaseUnit" checked={formData.isBaseUnit} onCheckedChange={(v) => setFormData({...formData, isBaseUnit: v})} />
                <Label htmlFor="isBaseUnit">Base Unit (for this category)</Label>
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
          <BreadcrumbItem><BreadcrumbPage>Units of Measure</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{units.length}</p>
                <p className="text-sm text-muted-foreground">Total Units</p>
              </div>
              <Ruler className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{units.filter(u => u.isActive).length}</p>
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
                <p className="text-2xl font-bold">{units.filter(u => u.isBaseUnit).length}</p>
                <p className="text-sm text-muted-foreground">Base Units</p>
              </div>
              <Ruler className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{new Set(units.map(u => u.category)).size}</p>
                <p className="text-sm text-muted-foreground">Categories</p>
              </div>
              <ArrowRightLeft className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search units..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger><SelectValue placeholder="Category" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                {categories.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}
              </SelectContent>
            </Select>
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

      {/* Units List */}
      <Card>
        <CardHeader>
          <CardTitle>Units of Measure</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${filteredUnits.length} unit(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {filteredUnits.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No units found.</div>
              ) : (
                filteredUnits.map((unit) => (
                  <div key={unit.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-blue-100 flex items-center justify-center">
                          <span className="text-lg font-bold text-blue-600">{unit.symbol || unit.code}</span>
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{unit.name}</h3>
                            <Badge variant="outline">{unit.code}</Badge>
                            <Badge className={unit.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                              {unit.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                            {unit.isBaseUnit && <Badge className="bg-purple-100 text-purple-800">Base Unit</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">Category: {unit.category}</p>
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => openConversions(unit)}>
                          <ArrowRightLeft className="h-4 w-4 mr-1" />Conversions
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => handleEdit(unit)}><Edit className="h-4 w-4" /></Button>
                        <Button size="sm" variant="outline" className="text-red-600" onClick={() => handleDelete(unit.id)}><Trash2 className="h-4 w-4" /></Button>
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
            <DialogTitle>Edit Unit of Measure</DialogTitle>
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
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Category</Label>
                <Select value={formData.category} onValueChange={(v) => setFormData({...formData, category: v})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{categories.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Sort Order</Label>
                <Input type="number" value={formData.sortOrder} onChange={(e) => setFormData({...formData, sortOrder: parseInt(e.target.value) || 0})} />
              </div>
            </div>
            <div className="flex items-center space-x-2">
              <Switch checked={formData.isBaseUnit} onCheckedChange={(v) => setFormData({...formData, isBaseUnit: v})} />
              <Label>Base Unit</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleUpdate}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Conversions Dialog */}
      <Dialog open={isConversionDialogOpen} onOpenChange={setIsConversionDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Unit Conversions - {selectedUnit?.name}</DialogTitle>
            <DialogDescription>Manage conversions from {selectedUnit?.code} to other units</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-2 items-end">
              <div className="space-y-2">
                <Label>To Unit</Label>
                <Select value={conversionForm.toUnitId} onValueChange={(v) => setConversionForm({...conversionForm, toUnitId: v})}>
                  <SelectTrigger><SelectValue placeholder="Select unit" /></SelectTrigger>
                  <SelectContent>
                    {units.filter(u => u.id !== selectedUnit?.id && u.category === selectedUnit?.category).map(u => (
                      <SelectItem key={u.id} value={u.id}>{u.name} ({u.code})</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Factor (1 {selectedUnit?.code} = X)</Label>
                <Input type="number" step="0.000001" value={conversionForm.conversionFactor} onChange={(e) => setConversionForm({...conversionForm, conversionFactor: parseFloat(e.target.value) || 1})} />
              </div>
              <Button onClick={handleCreateConversion} disabled={!conversionForm.toUnitId}><Plus className="h-4 w-4 mr-1" />Add</Button>
            </div>
            <div className="border rounded-lg divide-y max-h-60 overflow-y-auto">
              {conversions.length === 0 ? (
                <div className="p-4 text-center text-muted-foreground">No conversions defined</div>
              ) : (
                conversions.map(c => (
                  <div key={c.id} className="p-3 flex items-center justify-between">
                    <span>1 {c.fromUnitCode} = {c.conversionFactor} {c.toUnitCode}</span>
                    <Button size="sm" variant="ghost" className="text-red-600" onClick={() => handleDeleteConversion(c.id)}><Trash2 className="h-4 w-4" /></Button>
                  </div>
                ))
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsConversionDialogOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
