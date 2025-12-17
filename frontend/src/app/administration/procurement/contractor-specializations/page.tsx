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
import { Plus, Search, Edit, Trash2, Wrench, Settings } from 'lucide-react';
import { contractorSpecializationService, ContractorSpecializationDto, CreateContractorSpecializationDto, UpdateContractorSpecializationDto } from '@/services/partnerConfigService';
import { useToast } from '@/components/ui/use-toast';

export default function ContractorSpecializationsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedSpecialization, setSelectedSpecialization] = useState<ContractorSpecializationDto | null>(null);
  const [specializations, setSpecializations] = useState<ContractorSpecializationDto[]>([]);
  const [filteredData, setFilteredData] = useState<ContractorSpecializationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { toast } = useToast();
  
  // Form state for create
  const [formData, setFormData] = useState<CreateContractorSpecializationDto>({
    specializationName: '',
    specializationCode: '',
    description: '',
    displayOrder: 0
  });

  // Form state for edit
  const [editFormData, setEditFormData] = useState<UpdateContractorSpecializationDto>({
    specializationName: '',
    description: '',
    isActive: true,
    displayOrder: 0
  });

  // Fetch specializations
  const fetchSpecializations = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await contractorSpecializationService.getAll();
      setSpecializations(data);
    } catch (error: any) {
      console.error('Error fetching specializations:', error);
      setError(error.message || 'Failed to load specializations');
      toast({
        title: 'Error',
        description: 'Failed to load contractor specializations',
        variant: 'destructive'
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSpecializations();
  }, []);

  // Filter specializations
  useEffect(() => {
    let filtered = specializations;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.specializationName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.specializationCode?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item =>
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, specializations]);

  const handleCreate = async () => {
    try {
      await contractorSpecializationService.create(formData);
      toast({
        title: 'Success',
        description: 'Contractor specialization created successfully'
      });
      setIsCreateDialogOpen(false);
      resetForm();
      fetchSpecializations();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to create specialization',
        variant: 'destructive'
      });
    }
  };

  const handleEdit = (specialization: ContractorSpecializationDto) => {
    setSelectedSpecialization(specialization);
    setEditFormData({
      specializationName: specialization.specializationName,
      description: specialization.description || '',
      isActive: specialization.isActive,
      displayOrder: specialization.displayOrder || 0
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedSpecialization?.id) return;

    try {
      await contractorSpecializationService.update(selectedSpecialization.id, editFormData);
      toast({
        title: 'Success',
        description: 'Contractor specialization updated successfully'
      });
      setIsEditDialogOpen(false);
      resetForm();
      fetchSpecializations();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to update specialization',
        variant: 'destructive'
      });
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this contractor specialization?')) {
      return;
    }

    try {
      await contractorSpecializationService.delete(id);
      toast({
        title: 'Success',
        description: 'Contractor specialization deleted successfully'
      });
      fetchSpecializations();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to delete specialization',
        variant: 'destructive'
      });
    }
  };

  const resetForm = () => {
    setFormData({
      specializationName: '',
      specializationCode: '',
      description: '',
      displayOrder: 0
    });
    setEditFormData({
      specializationName: '',
      description: '',
      isActive: true,
      displayOrder: 0
    });
    setSelectedSpecialization(null);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Contractor Specializations</h1>
          <p className="text-muted-foreground">
            Manage contractor specializations and trade categories
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Specialization
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Contractor Specialization</DialogTitle>
              <DialogDescription>
                Create a new specialization for contractor classification.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Specialization Name</Label>
                  <Input
                    id="name"
                    value={formData.specializationName}
                    onChange={(e) => setFormData({...formData, specializationName: e.target.value})}
                    placeholder="e.g., Electrical, Plumbing"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Code</Label>
                  <Input
                    id="code"
                    value={formData.specializationCode}
                    onChange={(e) => setFormData({...formData, specializationCode: e.target.value.toUpperCase()})}
                    placeholder="e.g., ELEC, PLUMB"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this specialization..."
                  rows={3}
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Add Specialization
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration">Administration</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Contractor Specializations</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{filteredData.length}</p>
                <p className="text-sm text-muted-foreground">Total Specializations</p>
              </div>
              <Wrench className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(spec => spec.isActive).length}
                </p>
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
                <p className="text-2xl font-bold">
                  {filteredData.reduce((sum, spec) => sum + (spec.contractorCount || 0), 0)}
                </p>
                <p className="text-sm text-muted-foreground">Total Contractors</p>
              </div>
              <Wrench className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search specializations..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>

            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="inactive">Inactive</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Specializations List */}
      <Card>
        <CardHeader>
          <CardTitle>Contractor Specializations</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : error ? 'Error loading data' : `${filteredData.length} specialization${filteredData.length === 1 ? '' : 's'} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="flex items-center justify-center py-8">
              <div className="text-muted-foreground">Loading contractor specializations...</div>
            </div>
          )}

          {error && (
            <div className="flex items-center justify-center py-8">
              <div className="text-red-600">{error}</div>
            </div>
          )}

          {!loading && !error && (
            <div className="space-y-4">
              {filteredData.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No contractor specializations found. {searchTerm || statusFilter !== 'all' ? 'Try adjusting your filters.' : 'Create your first specialization to get started.'}
                </div>
              ) : (
                filteredData.map((specialization) => (
                  <div key={specialization.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-start justify-between">
                      <div className="space-y-2 flex-1">
                        <div className="flex items-center space-x-3">
                          <h3 className="font-semibold">{specialization.specializationName}</h3>
                          <Badge variant="outline">{specialization.specializationCode}</Badge>
                          <Badge className={specialization.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                            {specialization.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm text-muted-foreground">
                          <div>
                            <span className="font-medium">Contractors:</span> {specialization.contractorCount || 0}
                          </div>
                          {specialization.createdAt && (
                            <div>
                              <span className="font-medium">Created:</span> {new Date(specialization.createdAt).toLocaleDateString()}
                            </div>
                          )}
                        </div>

                        {specialization.description && (
                          <p className="text-sm text-muted-foreground">{specialization.description}</p>
                        )}
                      </div>

                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleEdit(specialization)}>
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleDelete(specialization.id)}
                          className="text-red-600 hover:text-red-700"
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
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
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Edit Contractor Specialization</DialogTitle>
            <DialogDescription>
              Update the contractor specialization information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="edit-name">Specialization Name</Label>
              <Input
                id="edit-name"
                value={editFormData.specializationName}
                onChange={(e) => setEditFormData({...editFormData, specializationName: e.target.value})}
                placeholder="e.g., Electrical, Plumbing"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={editFormData.description}
                onChange={(e) => setEditFormData({...editFormData, description: e.target.value})}
                placeholder="Describe this specialization..."
                rows={3}
              />
            </div>

            <div className="flex items-center space-x-2">
              <input
                type="checkbox"
                id="edit-isActive"
                checked={editFormData.isActive}
                onChange={(e) => setEditFormData({...editFormData, isActive: e.target.checked})}
                className="rounded border-gray-300"
              />
              <Label htmlFor="edit-isActive" className="text-sm font-medium">Active</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>
              Update Specialization
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
