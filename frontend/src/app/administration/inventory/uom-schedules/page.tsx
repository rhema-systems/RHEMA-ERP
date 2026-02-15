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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Plus, Search, Edit, Trash2, Layers, Calendar, Package, Save, ArrowRight } from 'lucide-react';
import { 
  inventoryManagementService, 
  UnitOfMeasureDto,
  UnitOfMeasureScheduleDto, 
  CreateUnitOfMeasureScheduleDto,
  UpdateUnitOfMeasureScheduleDto,
  CreateUnitOfMeasureScheduleDetailDto,
  CreateUnitOfMeasureDto
} from '@/services/inventoryManagementService';
import { toast } from 'sonner';

type DialogStep = 'base' | 'details';

export default function UomSchedulesPage() {
  const [schedules, setSchedules] = useState<UnitOfMeasureScheduleDto[]>([]);
  const [units, setUnits] = useState<UnitOfMeasureDto[]>([]);
  const [filteredSchedules, setFilteredSchedules] = useState<UnitOfMeasureScheduleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  
  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedSchedule, setSelectedSchedule] = useState<UnitOfMeasureScheduleDto | null>(null);
  const [dialogStep, setDialogStep] = useState<DialogStep>('base');
  const [isSavingBase, setIsSavingBase] = useState(false);
  const [newScheduleId, setNewScheduleId] = useState<string | null>(null);
  
  // Form state for base unit creation
  const [baseUnitForm, setBaseUnitForm] = useState({
    code: '',
    name: '',
    symbol: '',
    category: 'Quantity'
  });
  
  // Form state for schedule
  const [formData, setFormData] = useState<CreateUnitOfMeasureScheduleDto>({
    scheduleId: '',
    description: '',
    baseUnitOfMeasureId: '',
    quantityDecimals: 2,
    details: []
  });

  const [detailForm, setDetailForm] = useState<CreateUnitOfMeasureScheduleDetailDto>({
    unitOfMeasureId: '',
    baseQuantity: 1,
    sortOrder: 0
  });

  // New unit form for adding units inline
  const [newUnitForm, setNewUnitForm] = useState({
    code: '',
    name: '',
    symbol: '',
    conversionFactor: 1
  });
  const [isAddingNewUnit, setIsAddingNewUnit] = useState(false);

  const categories = ['Quantity', 'Weight', 'Volume', 'Length', 'Area', 'Time', 'Temperature'];

  // Fetch data
  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [schedulesData, unitsData] = await Promise.all([
        inventoryManagementService.getUomSchedules(),
        inventoryManagementService.getUnitsOfMeasure(true)
      ]);
      setSchedules(schedulesData);
      setUnits(unitsData);
    } catch (err: any) {
      console.error('Error fetching data:', err);
      setError('Failed to load data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchData(); }, []);

  // Filter schedules
  useEffect(() => {
    let filtered = schedules;
    if (searchTerm) {
      filtered = filtered.filter(s =>
        s.scheduleId.toLowerCase().includes(searchTerm.toLowerCase()) ||
        s.description.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }
    if (statusFilter !== 'all') {
      filtered = filtered.filter(s => statusFilter === 'active' ? s.isActive : !s.isActive);
    }
    setFilteredSchedules(filtered);
  }, [searchTerm, statusFilter, schedules]);

  // Step 1: Create base unit and schedule
  const handleCreateBaseUnit = async () => {
    try {
      setIsSavingBase(true);
      
      // First, create the base unit of measure
      const newUnit = await inventoryManagementService.createUnitOfMeasure({
        code: baseUnitForm.code.toUpperCase(),
        name: baseUnitForm.name,
        symbol: baseUnitForm.symbol,
        category: baseUnitForm.category,
        isBaseUnit: true,
        sortOrder: 0
      });
      
      // Refresh units list
      const updatedUnits = await inventoryManagementService.getUnitsOfMeasure(true);
      setUnits(updatedUnits);
      
      // Create the schedule with the new base unit
      const scheduleData: CreateUnitOfMeasureScheduleDto = {
        scheduleId: formData.scheduleId,
        description: formData.description,
        baseUnitOfMeasureId: newUnit.id,
        quantityDecimals: formData.quantityDecimals,
        details: []
      };
      
      const newSchedule = await inventoryManagementService.createUomSchedule(scheduleData);
      setNewScheduleId(newSchedule.id);
      
      // Update form data with the new base unit ID
      setFormData(prev => ({
        ...prev,
        baseUnitOfMeasureId: newUnit.id
      }));
      
      // Move to step 2
      setDialogStep('details');
      
      // Refresh schedules
      const updatedSchedules = await inventoryManagementService.getUomSchedules();
      setSchedules(updatedSchedules);
      
    } catch (err: any) {
      console.error('Error creating base unit and schedule:', err);
      toast.error('Failed to create base unit and schedule: ' + (err.response?.data || err.message));
    } finally {
      setIsSavingBase(false);
    }
  };

  // Add a new unit and conversion to the schedule
  const handleAddNewUnitWithConversion = async () => {
    if (!newScheduleId) return;
    
    try {
      // Create the new unit
      const newUnit = await inventoryManagementService.createUnitOfMeasure({
        code: newUnitForm.code.toUpperCase(),
        name: newUnitForm.name,
        symbol: newUnitForm.symbol,
        category: baseUnitForm.category,
        isBaseUnit: false,
        sortOrder: formData.details.length + 1
      });
      
      // Refresh units list
      const updatedUnits = await inventoryManagementService.getUnitsOfMeasure(true);
      setUnits(updatedUnits);
      
      // Add to details
      const newDetail: CreateUnitOfMeasureScheduleDetailDto = {
        unitOfMeasureId: newUnit.id,
        baseQuantity: newUnitForm.conversionFactor,
        sortOrder: formData.details.length + 1
      };
      
      const updatedDetails = [...formData.details, newDetail];
      setFormData(prev => ({ ...prev, details: updatedDetails }));
      
      // Update the schedule with new details
      await inventoryManagementService.updateUomSchedule(newScheduleId, {
        description: formData.description,
        baseUnitOfMeasureId: formData.baseUnitOfMeasureId,
        quantityDecimals: formData.quantityDecimals,
        isActive: true,
        details: updatedDetails
      });
      
      // Refresh schedules
      const updatedSchedules = await inventoryManagementService.getUomSchedules();
      setSchedules(updatedSchedules);
      
      // Reset new unit form
      setNewUnitForm({ code: '', name: '', symbol: '', conversionFactor: 1 });
      setIsAddingNewUnit(false);
      
    } catch (err: any) {
      console.error('Error adding unit:', err);
      toast.error('Failed to add unit: ' + (err.response?.data || err.message));
    }
  };

  // Add existing unit to schedule
  const addExistingUnitToSchedule = async () => {
    if (!detailForm.unitOfMeasureId || !newScheduleId) return;
    
    try {
      const newDetail: CreateUnitOfMeasureScheduleDetailDto = {
        unitOfMeasureId: detailForm.unitOfMeasureId,
        baseQuantity: detailForm.baseQuantity,
        sortOrder: formData.details.length + 1
      };
      
      const updatedDetails = [...formData.details, newDetail];
      setFormData(prev => ({ ...prev, details: updatedDetails }));
      
      // Update the schedule
      await inventoryManagementService.updateUomSchedule(newScheduleId, {
        description: formData.description,
        baseUnitOfMeasureId: formData.baseUnitOfMeasureId,
        quantityDecimals: formData.quantityDecimals,
        isActive: true,
        details: updatedDetails
      });
      
      // Refresh schedules
      const updatedSchedules = await inventoryManagementService.getUomSchedules();
      setSchedules(updatedSchedules);
      
      // Reset detail form
      setDetailForm({ unitOfMeasureId: '', baseQuantity: 1, sortOrder: 0 });
      
    } catch (err: any) {
      console.error('Error adding unit to schedule:', err);
      toast.error('Failed to add unit: ' + (err.response?.data || err.message));
    }
  };

  const handleEdit = (schedule: UnitOfMeasureScheduleDto) => {
    setSelectedSchedule(schedule);
    setFormData({
      scheduleId: schedule.scheduleId,
      description: schedule.description,
      baseUnitOfMeasureId: schedule.baseUnitOfMeasureId,
      quantityDecimals: schedule.quantityDecimals,
      details: schedule.details.map(d => ({
        unitOfMeasureId: d.unitOfMeasureId,
        baseQuantity: d.baseQuantity,
        sortOrder: d.sortOrder
      }))
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedSchedule) return;
    try {
      const updateData: UpdateUnitOfMeasureScheduleDto = {
        description: formData.description,
        baseUnitOfMeasureId: formData.baseUnitOfMeasureId,
        quantityDecimals: formData.quantityDecimals,
        isActive: selectedSchedule.isActive,
        details: formData.details
      };
      const updated = await inventoryManagementService.updateUomSchedule(selectedSchedule.id, updateData);
      setSchedules(prev => prev.map(s => s.id === selectedSchedule.id ? updated : s));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (err: any) {
      console.error('Error updating schedule:', err);
      toast.error('Failed to update UoM schedule');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this UoM schedule?')) return;
    try {
      await inventoryManagementService.deleteUomSchedule(id);
      setSchedules(prev => prev.filter(s => s.id !== id));
    } catch (err: any) {
      console.error('Error deleting schedule:', err);
      toast.error('Failed to delete UoM schedule');
    }
  };

  const resetForm = () => {
    setFormData({ scheduleId: '', description: '', baseUnitOfMeasureId: '', quantityDecimals: 2, details: [] });
    setDetailForm({ unitOfMeasureId: '', baseQuantity: 1, sortOrder: 0 });
    setBaseUnitForm({ code: '', name: '', symbol: '', category: 'Quantity' });
    setNewUnitForm({ code: '', name: '', symbol: '', conversionFactor: 1 });
    setSelectedSchedule(null);
    setDialogStep('base');
    setNewScheduleId(null);
    setIsAddingNewUnit(false);
  };

  const addDetail = () => {
    if (!detailForm.unitOfMeasureId) return;
    setFormData(prev => ({
      ...prev,
      details: [...prev.details, { ...detailForm, sortOrder: prev.details.length + 1 }]
    }));
    setDetailForm({ unitOfMeasureId: '', baseQuantity: 1, sortOrder: 0 });
  };

  const removeDetail = async (index: number) => {
    const updatedDetails = formData.details.filter((_, i) => i !== index);
    setFormData(prev => ({ ...prev, details: updatedDetails }));
    
    // If we're in create mode and have a schedule ID, update the schedule
    if (newScheduleId) {
      try {
        await inventoryManagementService.updateUomSchedule(newScheduleId, {
          description: formData.description,
          baseUnitOfMeasureId: formData.baseUnitOfMeasureId,
          quantityDecimals: formData.quantityDecimals,
          isActive: true,
          details: updatedDetails
        });
        
        const updatedSchedules = await inventoryManagementService.getUomSchedules();
        setSchedules(updatedSchedules);
      } catch (err) {
        console.error('Error removing unit from schedule:', err);
      }
    }
  };

  const getUnitName = (id: string) => units.find(u => u.id === id)?.name || id;
  const getUnitCode = (id: string) => units.find(u => u.id === id)?.code || '';

  const handleCloseCreateDialog = () => {
    setIsCreateDialogOpen(false);
    resetForm();
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Unit of Measure Schedules</h1>
          <p className="text-muted-foreground">Manage unit of measure schedules with base units and conversions</p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={(open) => {
          if (!open) handleCloseCreateDialog();
          else setIsCreateDialogOpen(true);
        }}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Schedule</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[700px]">
            <DialogHeader>
              <DialogTitle>
                {dialogStep === 'base' ? 'Step 1: Create Base Unit of Measure' : 'Step 2: Add Conversion Units'}
              </DialogTitle>
              <DialogDescription>
                {dialogStep === 'base' 
                  ? 'First, define the base unit of measure and schedule details. After saving, you can add more units with conversion factors.'
                  : 'Add additional units of measure with their conversion factors to the base unit.'}
              </DialogDescription>
            </DialogHeader>
            
            {dialogStep === 'base' ? (
              // Step 1: Base Unit and Schedule Creation
              <div className="grid gap-4 py-4 max-h-[60vh] overflow-y-auto">
                <div className="border rounded-lg p-4 bg-muted/30">
                  <h4 className="font-semibold mb-3">Schedule Information</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="scheduleId">Schedule ID *</Label>
                      <Input 
                        id="scheduleId" 
                        value={formData.scheduleId} 
                        onChange={(e) => setFormData({...formData, scheduleId: e.target.value.toUpperCase()})} 
                        placeholder="e.g., WEIGHT-01" 
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="quantityDecimals">Decimal Places *</Label>
                      <Select 
                        value={formData.quantityDecimals.toString()} 
                        onValueChange={(v) => setFormData({...formData, quantityDecimals: parseInt(v)})}
                      >
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {[0, 1, 2, 3, 4, 5].map(n => (
                            <SelectItem key={n} value={n.toString()}>{n}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                  <div className="space-y-2 mt-4">
                    <Label htmlFor="description">Description *</Label>
                    <Input 
                      id="description" 
                      value={formData.description} 
                      onChange={(e) => setFormData({...formData, description: e.target.value})} 
                      placeholder="e.g., Weight units schedule" 
                    />
                  </div>
                </div>
                
                <div className="border rounded-lg p-4 bg-blue-50/50">
                  <h4 className="font-semibold mb-3">Base Unit of Measure</h4>
                  <p className="text-sm text-muted-foreground mb-3">
                    This is the fundamental unit that all other units in this schedule will convert to.
                  </p>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="baseCode">Code *</Label>
                      <Input 
                        id="baseCode" 
                        value={baseUnitForm.code} 
                        onChange={(e) => setBaseUnitForm({...baseUnitForm, code: e.target.value.toUpperCase()})} 
                        placeholder="e.g., EA, KG, LB" 
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="baseSymbol">Symbol</Label>
                      <Input 
                        id="baseSymbol" 
                        value={baseUnitForm.symbol} 
                        onChange={(e) => setBaseUnitForm({...baseUnitForm, symbol: e.target.value})} 
                        placeholder="e.g., kg, lb" 
                      />
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-4 mt-4">
                    <div className="space-y-2">
                      <Label htmlFor="baseName">Name *</Label>
                      <Input 
                        id="baseName" 
                        value={baseUnitForm.name} 
                        onChange={(e) => setBaseUnitForm({...baseUnitForm, name: e.target.value})} 
                        placeholder="e.g., Each, Kilogram" 
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="baseCategory">Category</Label>
                      <Select 
                        value={baseUnitForm.category} 
                        onValueChange={(v) => setBaseUnitForm({...baseUnitForm, category: v})}
                      >
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {categories.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                </div>
              </div>
            ) : (
              // Step 2: Add Conversion Units
              <div className="grid gap-4 py-4 max-h-[60vh] overflow-y-auto">
                <div className="bg-green-50 border border-green-200 rounded-lg p-3">
                  <div className="text-sm text-green-800">
                    <strong>Schedule Created!</strong> Base unit: <Badge variant="outline">{baseUnitForm.code}</Badge> ({baseUnitForm.name})
                  </div>
                </div>
                
                <Tabs defaultValue="new" className="w-full">
                  <TabsList className="grid w-full grid-cols-2">
                    <TabsTrigger value="new">Create New Unit</TabsTrigger>
                    <TabsTrigger value="existing">Use Existing Unit</TabsTrigger>
                  </TabsList>
                  
                  <TabsContent value="new" className="border rounded-lg p-4 mt-2">
                    <p className="text-sm text-muted-foreground mb-3">
                      Create a new unit of measure and add it to this schedule with a conversion factor.
                    </p>
                    <div className="grid grid-cols-4 gap-2 items-end">
                      <div className="space-y-2">
                        <Label>Code</Label>
                        <Input 
                          value={newUnitForm.code} 
                          onChange={(e) => setNewUnitForm({...newUnitForm, code: e.target.value.toUpperCase()})} 
                          placeholder="e.g., BOX" 
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Name</Label>
                        <Input 
                          value={newUnitForm.name} 
                          onChange={(e) => setNewUnitForm({...newUnitForm, name: e.target.value})} 
                          placeholder="e.g., Box" 
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>1 unit = X {baseUnitForm.code}</Label>
                        <Input 
                          type="number" 
                          step="0.000001" 
                          value={newUnitForm.conversionFactor} 
                          onChange={(e) => setNewUnitForm({...newUnitForm, conversionFactor: parseFloat(e.target.value) || 1})} 
                        />
                      </div>
                      <Button 
                        onClick={handleAddNewUnitWithConversion} 
                        disabled={!newUnitForm.code || !newUnitForm.name}
                      >
                        <Plus className="h-4 w-4 mr-1" />Add
                      </Button>
                    </div>
                  </TabsContent>
                  
                  <TabsContent value="existing" className="border rounded-lg p-4 mt-2">
                    <p className="text-sm text-muted-foreground mb-3">
                      Select an existing unit of measure and specify its conversion factor.
                    </p>
                    <div className="grid grid-cols-3 gap-2 items-end">
                      <div className="space-y-2">
                        <Label>Unit</Label>
                        <Select 
                          value={detailForm.unitOfMeasureId} 
                          onValueChange={(v) => setDetailForm({...detailForm, unitOfMeasureId: v})}
                        >
                          <SelectTrigger><SelectValue placeholder="Select unit" /></SelectTrigger>
                          <SelectContent>
                            {units
                              .filter(u => u.id !== formData.baseUnitOfMeasureId && !formData.details.some(d => d.unitOfMeasureId === u.id))
                              .map(u => (
                                <SelectItem key={u.id} value={u.id}>{u.name} ({u.code})</SelectItem>
                              ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="space-y-2">
                        <Label>1 unit = X {baseUnitForm.code}</Label>
                        <Input 
                          type="number" 
                          step="0.000001" 
                          value={detailForm.baseQuantity} 
                          onChange={(e) => setDetailForm({...detailForm, baseQuantity: parseFloat(e.target.value) || 1})} 
                        />
                      </div>
                      <Button onClick={addExistingUnitToSchedule} disabled={!detailForm.unitOfMeasureId}>
                        <Plus className="h-4 w-4 mr-1" />Add
                      </Button>
                    </div>
                  </TabsContent>
                </Tabs>
                
                {/* Current units in schedule */}
                {formData.details.length > 0 && (
                  <div className="border rounded-lg p-4">
                    <h4 className="font-semibold mb-3">Units in Schedule</h4>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Unit</TableHead>
                          <TableHead>Conversion</TableHead>
                          <TableHead></TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {formData.details.map((d, i) => (
                          <TableRow key={i}>
                            <TableCell>{getUnitName(d.unitOfMeasureId)} ({getUnitCode(d.unitOfMeasureId)})</TableCell>
                            <TableCell>1 {getUnitCode(d.unitOfMeasureId)} = {d.baseQuantity} {baseUnitForm.code}</TableCell>
                            <TableCell>
                              <Button size="sm" variant="ghost" className="text-red-600" onClick={() => removeDetail(i)}>
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </div>
            )}
            
            <DialogFooter>
              {dialogStep === 'base' ? (
                <>
                  <Button variant="outline" onClick={handleCloseCreateDialog}>Cancel</Button>
                  <Button 
                    onClick={handleCreateBaseUnit} 
                    disabled={!formData.scheduleId || !formData.description || !baseUnitForm.code || !baseUnitForm.name || isSavingBase}
                  >
                    {isSavingBase ? 'Saving...' : 'Save & Continue'}
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </>
              ) : (
                <>
                  <Button variant="outline" onClick={handleCloseCreateDialog}>Done</Button>
                </>
              )}
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
          <BreadcrumbItem><BreadcrumbPage>UoM Schedules</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div><p className="text-2xl font-bold">{schedules.length}</p><p className="text-sm text-muted-foreground">Total Schedules</p></div>
              <Layers className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div><p className="text-2xl font-bold">{schedules.filter(s => s.isActive).length}</p><p className="text-sm text-muted-foreground">Active</p></div>
              <Calendar className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div><p className="text-2xl font-bold">{schedules.reduce((sum, s) => sum + s.details.length, 0)}</p><p className="text-sm text-muted-foreground">Total Conversions</p></div>
              <Package className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div><p className="text-2xl font-bold">{units.length}</p><p className="text-sm text-muted-foreground">Total Units</p></div>
              <Package className="h-8 w-8 text-orange-500" />
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
              <Input placeholder="Search schedules..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
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

      {/* Schedules List */}
      <Card>
        <CardHeader>
          <CardTitle>UoM Schedules</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${filteredSchedules.length} schedule(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {filteredSchedules.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No schedules found.</div>
              ) : (
                filteredSchedules.map((schedule) => (
                  <div key={schedule.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-blue-100 flex items-center justify-center">
                          <Layers className="h-6 w-6 text-blue-600" />
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{schedule.description}</h3>
                            <Badge variant="outline">{schedule.scheduleId}</Badge>
                            <Badge className={schedule.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>{schedule.isActive ? 'Active' : 'Inactive'}</Badge>
                          </div>
                          <p className="text-sm text-muted-foreground">
                            Base Unit: <strong>{schedule.baseUnitOfMeasureName}</strong> ({schedule.baseUnitOfMeasureCode}) | 
                            Decimals: {schedule.quantityDecimals} | 
                            {schedule.details.length} conversion(s)
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleEdit(schedule)}><Edit className="h-4 w-4" /></Button>
                        <Button size="sm" variant="outline" className="text-red-600" onClick={() => handleDelete(schedule.id)}><Trash2 className="h-4 w-4" /></Button>
                      </div>
                    </div>
                    {schedule.details.length > 0 && (
                      <div className="mt-3 pl-16">
                        <div className="flex flex-wrap gap-2">
                          {schedule.details.map(d => (
                            <Badge key={d.id} variant="secondary">
                              1 {d.unitOfMeasureCode} = {d.baseQuantity} {schedule.baseUnitOfMeasureCode}
                            </Badge>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Edit UoM Schedule</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 py-4 max-h-[60vh] overflow-y-auto">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Schedule ID</Label>
                <Input value={formData.scheduleId} disabled className="bg-muted" />
              </div>
              <div className="space-y-2">
                <Label>Decimal Places</Label>
                <Select 
                  value={formData.quantityDecimals.toString()} 
                  onValueChange={(v) => setFormData({...formData, quantityDecimals: parseInt(v)})}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {[0, 1, 2, 3, 4, 5].map(n => (
                      <SelectItem key={n} value={n.toString()}>{n}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Description</Label>
                <Input value={formData.description} onChange={(e) => setFormData({...formData, description: e.target.value})} />
              </div>
              <div className="space-y-2">
                <Label>Base Unit</Label>
                <Select value={formData.baseUnitOfMeasureId} onValueChange={(v) => setFormData({...formData, baseUnitOfMeasureId: v})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{units.map(u => <SelectItem key={u.id} value={u.id}>{u.name} ({u.code})</SelectItem>)}</SelectContent>
                </Select>
              </div>
            </div>
            <div className="border rounded-lg p-4 space-y-4">
              <Label className="font-semibold">Schedule Details</Label>
              <div className="grid grid-cols-3 gap-2 items-end">
                <div className="space-y-2">
                  <Label>Unit</Label>
                  <Select value={detailForm.unitOfMeasureId} onValueChange={(v) => setDetailForm({...detailForm, unitOfMeasureId: v})}>
                    <SelectTrigger><SelectValue placeholder="Select unit" /></SelectTrigger>
                    <SelectContent>{units.filter(u => u.id !== formData.baseUnitOfMeasureId && !formData.details.some(d => d.unitOfMeasureId === u.id)).map(u => <SelectItem key={u.id} value={u.id}>{u.name} ({u.code})</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Base Qty</Label>
                  <Input type="number" step="0.000001" value={detailForm.baseQuantity} onChange={(e) => setDetailForm({...detailForm, baseQuantity: parseFloat(e.target.value) || 1})} />
                </div>
                <Button onClick={addDetail} disabled={!detailForm.unitOfMeasureId}><Plus className="h-4 w-4 mr-1" />Add</Button>
              </div>
              {formData.details.length > 0 && (
                <Table>
                  <TableHeader><TableRow><TableHead>Unit</TableHead><TableHead>Base Quantity</TableHead><TableHead></TableHead></TableRow></TableHeader>
                  <TableBody>
                    {formData.details.map((d, i) => (
                      <TableRow key={i}>
                        <TableCell>{getUnitName(d.unitOfMeasureId)} ({getUnitCode(d.unitOfMeasureId)})</TableCell>
                        <TableCell>1 = {d.baseQuantity} base</TableCell>
                        <TableCell><Button size="sm" variant="ghost" className="text-red-600" onClick={() => removeDetail(i)}><Trash2 className="h-4 w-4" /></Button></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setIsEditDialogOpen(false); resetForm(); }}>Cancel</Button>
            <Button onClick={handleUpdate}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
