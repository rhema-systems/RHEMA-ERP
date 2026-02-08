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
  WarehouseLocationDto, CreateWarehouseLocationDto
} from '@/services/inventoryManagementService';
import { toast } from 'sonner';

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
  
  const [formData, setFormData] = useState<CreateWarehouseDto>({
    name: '', code: '', description: '', address: '', city: '', state: '', 
    zipCode: '', country: '', warehouseType: 'Standard', contactPerson: '', 
    phone: '', email: '', isDefault: false
  });

  const [locationForm, setLocationForm] = useState<CreateWarehouseLocationDto>({
    warehouseId: '', locationCode: '', name: '', description: '',
    locationType: 'Bin', isPickingLocation: true, isReceivingLocation: true
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
      phone: warehouse.phone || '', email: warehouse.email || '', isDefault: warehouse.isDefault
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
    setLocationForm({ ...locationForm, warehouseId: warehouse.id });
    setIsLocationDialogOpen(true);
  };

  const handleCreateLocation = async () => {
    try {
      const newLocation = await inventoryManagementService.createWarehouseLocation(locationForm);
      setLocations(prev => [...prev, newLocation]);
      setLocationForm({ ...locationForm, locationCode: '', name: '', description: '' });
    } catch (err) {
      console.error('Error creating location:', err);
      toast.error('Failed to create location');
    }
  };

  const resetForm = () => {
    setFormData({
      name: '', code: '', description: '', address: '', city: '', state: '',
      zipCode: '', country: '', warehouseType: 'Standard', contactPerson: '',
      phone: '', email: '', isDefault: false
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
                          </div>
                          <p className="text-sm text-muted-foreground">
                            {[warehouse.city, warehouse.state, warehouse.country].filter(Boolean).join(', ') || 'No address'}
                            {' • '}{getLocationCount(warehouse.id)} locations
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
            <DialogDescription>Manage zones, aisles, shelves, and bins</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-4 gap-2 items-end">
              <div className="space-y-2"><Label>Code</Label>
                <Input value={locationForm.locationCode} onChange={(e) => setLocationForm({...locationForm, locationCode: e.target.value.toUpperCase()})} placeholder="A-01-001" />
              </div>
              <div className="space-y-2"><Label>Name</Label>
                <Input value={locationForm.name} onChange={(e) => setLocationForm({...locationForm, name: e.target.value})} />
              </div>
              <div className="space-y-2"><Label>Type</Label>
                <Select value={locationForm.locationType} onValueChange={(v) => setLocationForm({...locationForm, locationType: v})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{locationTypes.map(t => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <Button onClick={handleCreateLocation} disabled={!locationForm.locationCode}><Plus className="h-4 w-4 mr-1" />Add</Button>
            </div>
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
                    </div>
                    <Button size="sm" variant="ghost" className="text-red-600" onClick={() => inventoryManagementService.deleteWarehouseLocation(loc.id).then(() => setLocations(prev => prev.filter(l => l.id !== loc.id)))}><Trash2 className="h-4 w-4" /></Button>
                  </div>
                ))
              )}
            </div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setIsLocationDialogOpen(false)}>Close</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
