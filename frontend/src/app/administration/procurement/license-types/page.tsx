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
import { Plus, Search, Edit, Trash2, FileCheck, Shield } from 'lucide-react';
import { licenseTypeService, LicenseTypeDto, CreateLicenseTypeDto, UpdateLicenseTypeDto } from '@/services/partnerConfigService';
import { useToast } from '@/components/ui/use-toast';

export default function LicenseTypesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [applicableToFilter, setApplicableToFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedLicenseType, setSelectedLicenseType] = useState<LicenseTypeDto | null>(null);
  const [licenseTypes, setLicenseTypes] = useState<LicenseTypeDto[]>([]);
  const [filteredData, setFilteredData] = useState<LicenseTypeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { toast } = useToast();
  
  // Form state for create
  const [formData, setFormData] = useState<CreateLicenseTypeDto>({
    licenseName: '',
    licenseCode: '',
    description: '',
    applicableTo: 'Both',
    isMandatory: false,
    validityPeriodMonths: undefined
  });

  // Form state for edit
  const [editFormData, setEditFormData] = useState<UpdateLicenseTypeDto>({
    licenseName: '',
    description: '',
    isMandatory: false,
    validityPeriodMonths: undefined,
    requiresRenewal: false,
    renewalReminderDays: 30,
    isActive: true
  });

  // Fetch license types
  const fetchLicenseTypes = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await licenseTypeService.getAll();
      setLicenseTypes(data);
    } catch (error: any) {
      console.error('Error fetching license types:', error);
      setError(error.message || 'Failed to load license types');
      toast({
        title: 'Error',
        description: 'Failed to load license types',
        variant: 'destructive'
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchLicenseTypes();
  }, []);

  // Filter license types
  useEffect(() => {
    let filtered = licenseTypes;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.licenseName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.licenseCode?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item =>
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    if (applicableToFilter !== 'all') {
      filtered = filtered.filter(item =>
        item.applicableTo === applicableToFilter || item.applicableTo === 'Both'
      );
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, applicableToFilter, licenseTypes]);

  const handleCreate = async () => {
    try {
      await licenseTypeService.create(formData);
      toast({
        title: 'Success',
        description: 'License type created successfully'
      });
      setIsCreateDialogOpen(false);
      resetForm();
      fetchLicenseTypes();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to create license type',
        variant: 'destructive'
      });
    }
  };

  const handleEdit = (licenseType: LicenseTypeDto) => {
    setSelectedLicenseType(licenseType);
    setEditFormData({
      licenseName: licenseType.licenseName,
      description: licenseType.description || '',
      isMandatory: licenseType.isMandatory,
      validityPeriodMonths: licenseType.validityPeriodMonths,
      requiresRenewal: licenseType.requiresRenewal || false,
      renewalReminderDays: licenseType.renewalReminderDays || 30,
      isActive: licenseType.isActive
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedLicenseType?.id) return;

    try {
      await licenseTypeService.update(selectedLicenseType.id, editFormData);
      toast({
        title: 'Success',
        description: 'License type updated successfully'
      });
      setIsEditDialogOpen(false);
      resetForm();
      fetchLicenseTypes();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to update license type',
        variant: 'destructive'
      });
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this license type?')) {
      return;
    }

    try {
      await licenseTypeService.delete(id);
      toast({
        title: 'Success',
        description: 'License type deleted successfully'
      });
      fetchLicenseTypes();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to delete license type',
        variant: 'destructive'
      });
    }
  };

  const resetForm = () => {
    setFormData({
      licenseName: '',
      licenseCode: '',
      description: '',
      applicableTo: 'Both',
      isMandatory: false,
      validityPeriodMonths: undefined
    });
    setEditFormData({
      licenseName: '',
      description: '',
      isMandatory: false,
      validityPeriodMonths: undefined,
      requiresRenewal: false,
      renewalReminderDays: 30,
      isActive: true
    });
    setSelectedLicenseType(null);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">License Types</h1>
          <p className="text-muted-foreground">
            Manage license and certification requirements for business partners
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add License Type
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add License Type</DialogTitle>
              <DialogDescription>
                Create a new license or certification type for business partners.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">License Name</Label>
                  <Input
                    id="name"
                    value={formData.licenseName}
                    onChange={(e) => setFormData({...formData, licenseName: e.target.value})}
                    placeholder="e.g., Trade License"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Code</Label>
                  <Input
                    id="code"
                    value={formData.licenseCode}
                    onChange={(e) => setFormData({...formData, licenseCode: e.target.value.toUpperCase()})}
                    placeholder="e.g., TL, VAT"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this license type..."
                  rows={3}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="applicableTo">Applicable To</Label>
                  <Select value={formData.applicableTo} onValueChange={(value) => setFormData({...formData, applicableTo: value})}>
                    <SelectTrigger id="applicableTo">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Supplier">Supplier</SelectItem>
                      <SelectItem value="Contractor">Contractor</SelectItem>
                      <SelectItem value="Both">Both</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="validityPeriod">Validity Period (Months)</Label>
                  <Input
                    id="validityPeriod"
                    type="number"
                    value={formData.validityPeriodMonths || ''}
                    onChange={(e) => setFormData({...formData, validityPeriodMonths: e.target.value ? parseInt(e.target.value) : undefined})}
                    placeholder="e.g., 12, 24"
                  />
                </div>
              </div>

              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    id="isMandatory"
                    checked={formData.isMandatory}
                    onChange={(e) => setFormData({...formData, isMandatory: e.target.checked})}
                    className="rounded border-gray-300"
                  />
                  <Label htmlFor="isMandatory" className="text-sm font-medium">Mandatory</Label>
                </div>

                <div className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    id="isActive"
                    checked={formData.isActive}
                    onChange={(e) => setFormData({...formData, isActive: e.target.checked})}
                    className="rounded border-gray-300"
                  />
                  <Label htmlFor="isActive" className="text-sm font-medium">Active</Label>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Add License Type
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
            <BreadcrumbPage>License Types</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{filteredData.length}</p>
                <p className="text-sm text-muted-foreground">Total License Types</p>
              </div>
              <FileCheck className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(lt => lt.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <Shield className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(lt => lt.isMandatory).length}
                </p>
                <p className="text-sm text-muted-foreground">Mandatory</p>
              </div>
              <Shield className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(lt => lt.applicableTo === 'Both').length}
                </p>
                <p className="text-sm text-muted-foreground">Universal</p>
              </div>
              <FileCheck className="h-8 w-8 text-orange-500" />
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
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search license types..."
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

            <Select value={applicableToFilter} onValueChange={setApplicableToFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Applicable To" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="Supplier">Supplier</SelectItem>
                <SelectItem value="Contractor">Contractor</SelectItem>
                <SelectItem value="Both">Both</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* License Types List */}
      <Card>
        <CardHeader>
          <CardTitle>License Types</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : error ? 'Error loading data' : `${filteredData.length} license type${filteredData.length === 1 ? '' : 's'} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="flex items-center justify-center py-8">
              <div className="text-muted-foreground">Loading license types...</div>
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
                  No license types found. {searchTerm || statusFilter !== 'all' || applicableToFilter !== 'all' ? 'Try adjusting your filters.' : 'Create your first license type to get started.'}
                </div>
              ) : (
                filteredData.map((licenseType) => (
                  <div key={licenseType.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-start justify-between">
                      <div className="space-y-2 flex-1">
                        <div className="flex items-center space-x-3">
                          <h3 className="font-semibold">{licenseType.licenseName}</h3>
                          <Badge variant="outline">{licenseType.licenseCode}</Badge>
                          <Badge className={licenseType.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                            {licenseType.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                          {licenseType.isMandatory && (
                            <Badge className="bg-red-100 text-red-800">Mandatory</Badge>
                          )}
                          <Badge variant="secondary">{licenseType.applicableTo}</Badge>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-3 gap-2 text-sm text-muted-foreground">
                          <div>
                            <span className="font-medium">Validity:</span> {licenseType.validityPeriodMonths ? `${licenseType.validityPeriodMonths} months` : 'N/A'}
                          </div>
                          <div>
                            <span className="font-medium">Type:</span> {licenseType.isMandatory ? 'Mandatory' : 'Optional'}
                          </div>
                          {licenseType.createdAt && (
                            <div>
                              <span className="font-medium">Created:</span> {new Date(licenseType.createdAt).toLocaleDateString()}
                            </div>
                          )}
                        </div>

                        {licenseType.description && (
                          <p className="text-sm text-muted-foreground">{licenseType.description}</p>
                        )}
                      </div>

                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleEdit(licenseType)}>
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleDelete(licenseType.id)}
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
            <DialogTitle>Edit License Type</DialogTitle>
            <DialogDescription>
              Update the license type information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="edit-name">License Name</Label>
              <Input
                id="edit-name"
                value={editFormData.licenseName}
                onChange={(e) => setEditFormData({...editFormData, licenseName: e.target.value})}
                placeholder="e.g., Trade License"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={editFormData.description}
                onChange={(e) => setEditFormData({...editFormData, description: e.target.value})}
                placeholder="Describe this license type..."
                rows={3}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-validityPeriod">Validity Period (Months)</Label>
              <Input
                id="edit-validityPeriod"
                type="number"
                value={editFormData.validityPeriodMonths || ''}
                onChange={(e) => setEditFormData({...editFormData, validityPeriodMonths: e.target.value ? parseInt(e.target.value) : undefined})}
                placeholder="e.g., 12, 24"
              />
            </div>

            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  id="edit-isMandatory"
                  checked={editFormData.isMandatory}
                  onChange={(e) => setEditFormData({...editFormData, isMandatory: e.target.checked})}
                  className="rounded border-gray-300"
                />
                <Label htmlFor="edit-isMandatory" className="text-sm font-medium">Mandatory</Label>
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
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>
              Update License Type
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}