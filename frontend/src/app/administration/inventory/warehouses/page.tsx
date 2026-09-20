'use client';

import React, { useState, useEffect } from 'react';
import axios from 'axios';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Plus, Search, Edit, Trash2, Building2, MapPin, Package, 
  ChevronRight, Layers, MoreHorizontal
} from 'lucide-react';
import { 
  inventoryManagementService, 
  WarehouseDto, CreateWarehouseDto, UpdateWarehouseDto,
  WarehouseLocationDto, CreateWarehouseLocationDto, UpdateWarehouseLocationDto
} from '@/services/inventoryManagementService';
import { toast } from 'sonner';

const locationErrorMessage = (error: unknown, fallback: string) => {
  const data = axios.isAxiosError(error) ? error.response?.data : undefined;
  const message = typeof data === 'string' ? data : data?.detail || data?.message || data?.title || (error instanceof Error ? error.message : fallback);
  const code = typeof data === 'object' ? data?.code || data?.extensions?.code : undefined;
  return code ? `${message} (${code})` : message;
};

export default function WarehousesPage() {
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [filteredWarehouses, setFilteredWarehouses] = useState<WarehouseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selectedWarehouse, setSelectedWarehouse] = useState<WarehouseDto | null>(null);
  
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isLocationDialogOpen, setIsLocationDialogOpen] = useState(false);
  const [isEditLocationDialogOpen, setIsEditLocationDialogOpen] = useState(false);
  const [editingLocation, setEditingLocation] = useState<WarehouseLocationDto | null>(null);
  const [locationEditForm, setLocationEditForm] = useState<UpdateWarehouseLocationDto | null>(null);
  const [reclassifyingLocationId, setReclassifyingLocationId] = useState<string | null>(null);
  const [savingLocation, setSavingLocation] = useState(false);
  
  const [formData, setFormData] = useState<CreateWarehouseDto>({
    name: '', code: '', description: '', address: '', city: '', state: '', 
    zipCode: '', country: '', warehouseType: 'Standard', contactPerson: '', 
    phone: '', email: '', isDefault: false, isConsignmentWarehouse: false
  });

  const [locationForm, setLocationForm] = useState<CreateWarehouseLocationDto>({
    warehouseId: '', locationCode: '', name: '', description: '',
    locationType: 'Bin', isPickingLocation: true, isReceivingLocation: true,
    isConsignmentBin: false, consignmentWarehouseId: null, isDefault: false
  });

  const warehouseTypes = ['Standard', 'Distribution', 'Manufacturing', 'Quarantine', 'Transit'];
  const locationTypes = ['Zone', 'Aisle', 'Shelf', 'Bin', 'Floor', 'Dock'];

  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [warehouseData, locationData] = await Promise.all([
        inventoryManagementService.getWarehouses(),
        inventoryManagementService.getWarehouseLocations()
      ]);
      setWarehouses(warehouseData);
      setLocations(locationData);
    } catch (err: any) {
      console.error('Error fetching data:', err);
      setError('Failed to load warehouses');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchData(); }, []);

  useEffect(() => {
    let filtered = warehouses;
    if (searchTerm) {
      filtered = filtered.filter(w =>
        w.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        w.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        w.city?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }
    if (typeFilter !== 'all') filtered = filtered.filter(w => w.warehouseType === typeFilter);
    if (statusFilter !== 'all') filtered = filtered.filter(w => statusFilter === 'active' ? w.isActive : !w.isActive);
    setFilteredWarehouses(filtered);
  }, [searchTerm, typeFilter, statusFilter, warehouses]);

  const handleCreate = async () => {
    try {
      const newWarehouse = await inventoryManagementService.createWarehouse(formData);
      setWarehouses(prev => [...prev, newWarehouse]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (err) {
      console.error('Error creating warehouse:', err);
      toast.error('Failed to create warehouse');
    }
  };

  const handleEdit = (warehouse: WarehouseDto) => {
    setSelectedWarehouse(warehouse);
    setFormData({
      name: warehouse.name, code: warehouse.code, description: warehouse.description || '',
      address: warehouse.address || '', city: warehouse.city || '', state: warehouse.state || '',
      zipCode: warehouse.zipCode || '', country: warehouse.country || '', 
      warehouseType: warehouse.warehouseType, contactPerson: warehouse.contactPerson || '',
      phone: warehouse.phone || '', email: warehouse.email || '', isDefault: warehouse.isDefault,
      isConsignmentWarehouse: warehouse.isConsignmentWarehouse
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedWarehouse) return;
    try {
      const updateData: UpdateWarehouseDto = { ...formData, isActive: selectedWarehouse.isActive };
      const updated = await inventoryManagementService.updateWarehouse(selectedWarehouse.id, updateData);
      setWarehouses(prev => prev.map(w => w.id === selectedWarehouse.id ? updated : w));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (err) {
      console.error('Error updating warehouse:', err);
      toast.error('Failed to update warehouse');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this warehouse?')) return;
    try {
      await inventoryManagementService.deleteWarehouse(id);
      setWarehouses(prev => prev.filter(w => w.id !== id));
    } catch (err) {
      console.error('Error deleting warehouse:', err);
      toast.error('Failed to delete warehouse');
    }
  };

  const openLocations = (warehouse: WarehouseDto) => {
    setSelectedWarehouse(warehouse);
    setLocationForm({ warehouseId: warehouse.id, locationCode: '', name: '', description: '', locationType: 'Bin', isPickingLocation: true, isReceivingLocation: true, isConsignmentBin: false, consignmentWarehouseId: null, isDefault: false });
    setIsLocationDialogOpen(true);
  };

  const retainSavedLocation = (saved: WarehouseLocationDto) => {
    setLocations(previous => {
      const updated = previous.map(location => location.id === saved.id ? saved :
        saved.isDefault && location.warehouseId === saved.warehouseId ? { ...location, isDefault: false } : location);
      return previous.some(location => location.id === saved.id) ? updated : [...updated, saved];
    });
  };

  const handleCreateLocation = async () => {
    if (savingLocation || !locationForm.locationCode.trim()) return;
    try {
      setSavingLocation(true);
      const newLocation = await inventoryManagementService.createWarehouseLocation(locationForm);
      retainSavedLocation(newLocation);
      setLocationForm({ ...locationForm, locationCode: '', name: '', description: '', isDefault: false });
      toast.success('Location added');
    } catch (err) {
      console.error('Error creating location:', err);
      toast.error(locationErrorMessage(err, 'Failed to create location'));
    } finally {
      setSavingLocation(false);
    }
  };

  const openEditLocation = (loc: WarehouseLocationDto) => {
    setEditingLocation(loc);
    setLocationEditForm({
      warehouseId: loc.warehouseId,
      locationCode: loc.locationCode,
      name: loc.name ?? '',
      description: loc.description ?? '',
      locationType: loc.locationType,
      parentLocationId: loc.parentLocationId,
      isPickingLocation: loc.isPickingLocation,
      isReceivingLocation: loc.isReceivingLocation,
      isConsignmentBin: !!loc.isConsignmentBin,
      consignmentWarehouseId: loc.consignmentWarehouseId ?? null,
      maxWeight: loc.maxWeight,
      maxVolume: loc.maxVolume,
      maxItems: loc.maxItems,
      isActive: loc.isActive,
      isDefault: !!loc.isDefault
    });
    setIsEditLocationDialogOpen(true);
  };

  const handleUpdateLocation = async () => {
    if (!editingLocation || !locationEditForm || savingLocation || (locationEditForm.isDefault && !locationEditForm.isActive)) return;
    try {
      setSavingLocation(true);
      const updated = await inventoryManagementService.updateWarehouseLocation(editingLocation.id, locationEditForm);
      retainSavedLocation(updated);
      setIsEditLocationDialogOpen(false);
      setEditingLocation(null);
      setLocationEditForm(null);
      toast.success('Location updated');
    } catch (err: any) {
      console.error('Error updating location:', err);
      toast.error(locationErrorMessage(err, 'Failed to update location'));
    } finally {
      setSavingLocation(false);
    }
  };

  const handleReclassify = async (locationId: string) => {
    try {
      setReclassifyingLocationId(locationId);
      const res = await inventoryManagementService.reclassifyExistingStockToConsignment(locationId);
      const movedLines = Number(res?.movedLines ?? 0);
      const totalQty = Number(res?.totalQuantityMoved ?? 0);
      toast.success(`Reclassified ${totalQty} across ${movedLines} item(s).`);
    } catch (err: any) {
      console.error('Error reclassifying stock:', err);
      toast.error(err?.response?.data || err?.message || 'Failed to reclassify stock');
    } finally {
      setReclassifyingLocationId(null);
    }
  };

  const resetForm = () => {
    setFormData({
      name: '', code: '', description: '', address: '', city: '', state: '',
      zipCode: '', country: '', warehouseType: 'Standard', contactPerson: '',
      phone: '', email: '', isDefault: false, isConsignmentWarehouse: false
    });
    setSelectedWarehouse(null);
  };

  const getLocationCount = (warehouseId: string) => locations.filter(l => l.warehouseId === warehouseId).length;

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Warehouse Management</h1>
          <p className="text-muted-foreground">Manage warehouses, zones, and storage locations</p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Warehouse</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[700px] max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Add Warehouse</DialogTitle>
              <DialogDescription>Create a new warehouse or storage facility.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Name *</Label>
                  <Input value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} placeholder="Main Warehouse" />
                </div>
                <div className="space-y-2">
                  <Label>Code *</Label>
                  <Input value={formData.code} onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})} placeholder="WH-001" />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea value={formData.description} onChange={(e) => setFormData({...formData, description: e.target.value})} rows={2} />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Type</Label>
                  <Select value={formData.warehouseType} onValueChange={(v) => setFormData({...formData, warehouseType: v})}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{warehouseTypes.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Address</Label>
                  <Input value={formData.address} onChange={(e) => setFormData({...formData, address: e.target.value})} />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label>City</Label>
                  <Input value={formData.city} onChange={(e) => setFormData({...formData, city: e.target.value})} />
                </div>
                <div className="space-y-2">
                  <Label>State</Label>
                  <Input value={formData.state} onChange={(e) => setFormData({...formData, state: e.target.value})} />
                </div>
                <div className="space-y-2">
                  <Label>Zip Code</Label>
                  <Input value={formData.zipCode} onChange={(e) => setFormData({...formData, zipCode: e.target.value})} />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label>Contact Person</Label>
                  <Input value={formData.contactPerson} onChange={(e) => setFormData({...formData, contactPerson: e.target.value})} />
                </div>
                <div className="space-y-2">
                  <Label>Phone</Label>
                  <Input value={formData.phone} onChange={(e) => setFormData({...formData, phone: e.target.value})} />
                </div>
                <div className="space-y-2">
                  <Label>Email</Label>
                  <Input value={formData.email} onChange={(e) => setFormData({...formData, email: e.target.value})} />
                </div>
              </div>
              <div className="flex items-center space-x-2">
                <Switch checked={formData.isDefault} onCheckedChange={(v) => setFormData({...formData, isDefault: v})} />
                <Label>Default Warehouse</Label>
              </div>
              <div className="flex items-start space-x-2">
                <Switch
                  checked={!!formData.isConsignmentWarehouse}
                  onCheckedChange={(v) => setFormData({ ...formData, isConsignmentWarehouse: v })}
                />
                <div className="space-y-1">
                  <Label>Consignment Warehouse</Label>
                  <p className="text-xs text-muted-foreground">
                    Stock issued/transferred/sold/consumed from this warehouse is treated as consignment and will create settlement records.
                  </p>
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
          <BreadcrumbItem><BreadcrumbPage>Warehouses</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{warehouses.length}</p><p className="text-sm text-muted-foreground">Total Warehouses</p></div>
            <Building2 className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{warehouses.filter(w => w.isActive).length}</p><p className="text-sm text-muted-foreground">Active</p></div>
            <Building2 className="h-8 w-8 text-green-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{locations.length}</p><p className="text-sm text-muted-foreground">Total Locations</p></div>
            <MapPin className="h-8 w-8 text-purple-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{new Set(warehouses.map(w => w.warehouseType)).size}</p><p className="text-sm text-muted-foreground">Types</p></div>
            <Layers className="h-8 w-8 text-orange-500" />
          </div>
        </CardContent></Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="Search warehouses..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={typeFilter} onValueChange={setTypeFilter}>
              <SelectTrigger><SelectValue placeholder="Type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {warehouseTypes.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}
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

      {/* Warehouse List */}
      <Card>
        <CardHeader>
          <CardTitle>Warehouses</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${filteredWarehouses.length} warehouse(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {filteredWarehouses.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No warehouses found.</div>
              ) : (
                filteredWarehouses.map((warehouse) => (
                  <div key={warehouse.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-blue-100 flex items-center justify-center">
                          <Building2 className="h-6 w-6 text-blue-600" />
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{warehouse.name}</h3>
                            <Badge variant="outline">{warehouse.code}</Badge>
                            <Badge className={warehouse.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                              {warehouse.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                            <Badge variant="secondary">{warehouse.warehouseType}</Badge>
                            {warehouse.isDefault && <Badge className="bg-purple-100 text-purple-800">Default</Badge>}
                            {warehouse.isConsignmentWarehouse && <Badge className="bg-amber-100 text-amber-900">Consignment</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">
                            {[warehouse.city, warehouse.state, warehouse.country].filter(Boolean).join(', ') || 'No address'}
                            {' • '}{getLocationCount(warehouse.id)} locations
                            {' • '}Default bin: {locations.find(location => location.warehouseId === warehouse.id && location.isDefault && location.isActive)?.locationCode || 'Not set'}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => openLocations(warehouse)}>
                          <MapPin className="h-4 w-4 mr-1" />Locations
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => handleEdit(warehouse)}><Edit className="h-4 w-4" /></Button>
                        <Button size="sm" variant="outline" className="text-red-600" onClick={() => handleDelete(warehouse.id)}><Trash2 className="h-4 w-4" /></Button>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog - same as Create but for edit */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[700px] max-h-[90vh] overflow-y-auto">
          <DialogHeader><DialogTitle>Edit Warehouse</DialogTitle></DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Name</Label><Input value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} /></div>
              <div className="space-y-2"><Label>Code</Label><Input value={formData.code} disabled className="bg-muted" /></div>
            </div>
            <div className="space-y-2"><Label>Description</Label><Textarea value={formData.description} onChange={(e) => setFormData({...formData, description: e.target.value})} rows={2} /></div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Type</Label>
                <Select value={formData.warehouseType} onValueChange={(v) => setFormData({...formData, warehouseType: v})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{warehouseTypes.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="space-y-2"><Label>Address</Label><Input value={formData.address} onChange={(e) => setFormData({...formData, address: e.target.value})} /></div>
            </div>
            <div className="grid grid-cols-3 gap-4">
              <div className="space-y-2"><Label>City</Label><Input value={formData.city} onChange={(e) => setFormData({...formData, city: e.target.value})} /></div>
              <div className="space-y-2"><Label>State</Label><Input value={formData.state} onChange={(e) => setFormData({...formData, state: e.target.value})} /></div>
              <div className="space-y-2"><Label>Phone</Label><Input value={formData.phone} onChange={(e) => setFormData({...formData, phone: e.target.value})} /></div>
            </div>
            <div className="flex items-center space-x-2">
              <Switch checked={formData.isDefault} onCheckedChange={(v) => setFormData({...formData, isDefault: v})} />
              <Label>Default Warehouse</Label>
            </div>
            <div className="flex items-start space-x-2">
              <Switch
                checked={!!formData.isConsignmentWarehouse}
                onCheckedChange={(v) => setFormData({ ...formData, isConsignmentWarehouse: v })}
              />
              <div className="space-y-1">
                <Label>Consignment Warehouse</Label>
                <p className="text-xs text-muted-foreground">
                  Stock issued/transferred/sold/consumed from this warehouse is treated as consignment and will create settlement records.
                </p>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleUpdate}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Locations Dialog */}
      <Dialog open={isLocationDialogOpen} onOpenChange={setIsLocationDialogOpen}>
        <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Locations - {selectedWarehouse?.name}</DialogTitle>
            <DialogDescription>Manage storage locations. Set one active default bin for item assignments and count rows without a location.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-4 gap-2 items-end">
              <div className="space-y-2"><Label htmlFor="new-location-code">Code</Label>
                <Input id="new-location-code" value={locationForm.locationCode} onChange={(e) => setLocationForm({...locationForm, locationCode: e.target.value.toUpperCase()})} placeholder="A-01-001" disabled={savingLocation} />
              </div>
              <div className="space-y-2"><Label>Name</Label>
                <Input value={locationForm.name} onChange={(e) => setLocationForm({...locationForm, name: e.target.value})} />
              </div>
              <div className="space-y-2"><Label>Type</Label>
                <Select value={locationForm.locationType} disabled={savingLocation} onValueChange={(v) => setLocationForm({...locationForm, locationType: v, isDefault: v === 'Bin' && locationForm.isDefault})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{locationTypes.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <Button onClick={handleCreateLocation} disabled={!locationForm.locationCode.trim() || savingLocation}><Plus className="h-4 w-4 mr-1" />{savingLocation ? 'Saving...' : 'Add'}</Button>
            </div>
            <div className="flex items-center gap-2"><Switch id="new-location-default" checked={!!locationForm.isDefault} onCheckedChange={value => setLocationForm({ ...locationForm, isDefault: value })} disabled={savingLocation || locationForm.locationType !== 'Bin'} /><Label htmlFor="new-location-default">Use as default bin</Label></div>
            <div className="border rounded-lg divide-y max-h-60 overflow-y-auto">
              {locations.filter(l => l.warehouseId === selectedWarehouse?.id).length === 0 ? (
                <div className="p-4 text-center text-muted-foreground">No locations defined</div>
              ) : (
                locations.filter(l => l.warehouseId === selectedWarehouse?.id).map(loc => (
                  <div key={loc.id} className="p-3 flex items-center justify-between">
                    <div className="flex items-center space-x-3">
                      <Badge variant="outline">{loc.locationType}</Badge>
                      <span className="font-medium">{loc.locationCode}</span>
                      <span className="text-muted-foreground">{loc.name}</span>
                      {loc.isDefault && <Badge className="bg-blue-100 text-blue-800">Default bin</Badge>}
                      {!loc.isActive && <Badge variant="secondary">Inactive</Badge>}
                      {loc.isConsignmentBin && (
                        <Badge className="bg-amber-100 text-amber-900">Consignment Bin</Badge>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      {loc.isConsignmentBin && (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={reclassifyingLocationId === loc.id}
                          onClick={() => handleReclassify(loc.id)}
                        >
                          Reclassify stock
                        </Button>
                      )}
                      <Button size="sm" variant="ghost" aria-label={`Edit location ${loc.locationCode}`} onClick={() => openEditLocation(loc)}><Edit className="h-4 w-4" /></Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-red-600"
                        onClick={() =>
                          inventoryManagementService
                            .deleteWarehouseLocation(loc.id)
                            .then(() => setLocations(prev => prev.filter(l => l.id !== loc.id)))
                            .catch(error => toast.error(locationErrorMessage(error, 'Failed to delete location')))
                        }
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setIsLocationDialogOpen(false)}>Close</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Location Dialog */}
      <Dialog
        open={isEditLocationDialogOpen}
        onOpenChange={(o) => {
          if (savingLocation) return;
          setIsEditLocationDialogOpen(o);
          if (!o) {
            setEditingLocation(null);
            setLocationEditForm(null);
          }
        }}
      >
        <DialogContent className="sm:max-w-[700px] max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Location</DialogTitle>
            <DialogDescription>{editingLocation?.locationCode}</DialogDescription>
          </DialogHeader>

          {locationEditForm && (
            <div className="grid gap-4 py-2">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Code</Label>
                  <Input value={locationEditForm.locationCode} disabled className="bg-muted" />
                </div>
                <div className="space-y-2">
                  <Label>Type</Label>
                  <Select value={locationEditForm.locationType} disabled={savingLocation} onValueChange={(v) => setLocationEditForm({ ...locationEditForm, locationType: v, isDefault: v === 'Bin' && locationEditForm.isDefault })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{locationTypes.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Name</Label>
                  <Input value={locationEditForm.name ?? ''} onChange={(e) => setLocationEditForm({ ...locationEditForm, name: e.target.value })} />
                </div>
                <div className="flex items-center space-x-2 pt-7">
                  <Switch id="edit-location-active" checked={locationEditForm.isActive} disabled={savingLocation} onCheckedChange={(v) => setLocationEditForm({ ...locationEditForm, isActive: v })} />
                  <Label htmlFor="edit-location-active">Active</Label>
                </div>
              </div>

              <div className="space-y-2"><div className="flex items-center gap-2"><Switch id="edit-location-default" checked={!!locationEditForm.isDefault} disabled={savingLocation || !locationEditForm.isActive || !!locationEditForm.isConsignmentBin || locationEditForm.locationType !== 'Bin'} onCheckedChange={value => setLocationEditForm({ ...locationEditForm, isDefault: value })} /><Label htmlFor="edit-location-default">Default bin</Label></div><p className="text-xs text-muted-foreground">Replaces the current default for this warehouse. Existing stock assignments stay unchanged.</p>{locationEditForm.isDefault && !locationEditForm.isActive && <p role="alert" className="text-sm text-red-600">The default bin must remain active. Select a replacement default before deactivating it.</p>}</div>

              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea value={locationEditForm.description ?? ''} onChange={(e) => setLocationEditForm({ ...locationEditForm, description: e.target.value })} rows={3} />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="flex items-center space-x-2">
                  <Switch checked={locationEditForm.isPickingLocation} onCheckedChange={(v) => setLocationEditForm({ ...locationEditForm, isPickingLocation: v })} />
                  <Label>Picking Location</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch checked={locationEditForm.isReceivingLocation} onCheckedChange={(v) => setLocationEditForm({ ...locationEditForm, isReceivingLocation: v })} />
                  <Label>Receiving Location</Label>
                </div>
              </div>

              <div className="border rounded-md p-3 space-y-3">
                <div className="flex items-start space-x-2">
                  <Switch
                    checked={!!locationEditForm.isConsignmentBin}
                    disabled={savingLocation || !!locationEditForm.isDefault}
                    onCheckedChange={(v) => setLocationEditForm({
                      ...locationEditForm,
                      isConsignmentBin: v,
                      consignmentWarehouseId: v ? (locationEditForm.consignmentWarehouseId ?? null) : null
                    })}
                  />
                  <div className="space-y-1">
                    <Label>Consignment Bin</Label>
                    <p className="text-xs text-muted-foreground">
                      When enabled, stock in this bin is attributed to the selected consignment warehouse and excluded from owned/main inventory.
                    </p>
                  </div>
                </div>

                {locationEditForm.isConsignmentBin && (
                  <div className="grid gap-2">
                    <Label>Consignment Warehouse</Label>
                    <Select
                      value={locationEditForm.consignmentWarehouseId ?? 'none'}
                      onValueChange={(v) => setLocationEditForm({ ...locationEditForm, consignmentWarehouseId: v === 'none' ? null : v })}
                    >
                      <SelectTrigger><SelectValue placeholder="Select consignment warehouse" /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">Select consignment warehouse</SelectItem>
                        {warehouses.filter(w => w.isConsignmentWarehouse).map(w => (
                          <SelectItem key={w.id} value={w.id}>
                            {w.name} ({w.code})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>

                    <div className="flex items-center justify-between pt-1">
                      <p className="text-xs text-muted-foreground">
                        If this bin already had stock before toggling, use the reclassify action to convert ownership.
                      </p>
                      {editingLocation?.isConsignmentBin && (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={reclassifyingLocationId === editingLocation.id}
                          onClick={() => handleReclassify(editingLocation.id)}
                        >
                          Reclassify existing stock
                        </Button>
                      )}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" disabled={savingLocation} onClick={() => setIsEditLocationDialogOpen(false)}>Cancel</Button>
            <Button
              onClick={handleUpdateLocation}
              disabled={
                !locationEditForm || savingLocation || (locationEditForm.isDefault && !locationEditForm.isActive) ||
                (locationEditForm.isConsignmentBin && (!locationEditForm.consignmentWarehouseId || locationEditForm.consignmentWarehouseId === 'none'))
              }
            >
              Save Changes
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
