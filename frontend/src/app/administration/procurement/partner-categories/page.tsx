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
import { Plus, Search, Edit, Trash2, Tag, Settings } from 'lucide-react';
import { partnerCategoryService, PartnerCategoryDto, CreatePartnerCategoryDto, UpdatePartnerCategoryDto } from '@/services/partnerConfigService';
import { useToast } from '@/components/ui/use-toast';

export default function PartnerCategoriesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<PartnerCategoryDto | null>(null);
  const [categories, setCategories] = useState<PartnerCategoryDto[]>([]);
  const [filteredData, setFilteredData] = useState<PartnerCategoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { toast } = useToast();
  
  // Form state for create
  const [formData, setFormData] = useState<CreatePartnerCategoryDto>({
    categoryName: '',
    categoryCode: '',
    description: '',
    categoryType: 'Supplier',
    displayOrder: 0
  });

  // Form state for edit
  const [editFormData, setEditFormData] = useState<UpdatePartnerCategoryDto>({
    categoryName: '',
    description: '',
    isActive: true,
    displayOrder: 0
  });

  // Fetch categories
  const fetchCategories = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await partnerCategoryService.getAll();
      setCategories(data);
    } catch (error: any) {
      console.error('Error fetching categories:', error);
      setError(error.message || 'Failed to load categories');
      toast({
        title: 'Error',
        description: 'Failed to load partner categories',
        variant: 'destructive'
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCategories();
  }, []);

  // Filter categories
  useEffect(() => {
    let filtered = categories;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.categoryName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.categoryCode?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => 
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, categories]);

  const handleCreate = async () => {
    try {
      await partnerCategoryService.create(formData);
      toast({
        title: 'Success',
        description: 'Partner category created successfully'
      });
      setIsCreateDialogOpen(false);
      resetForm();
      fetchCategories();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to create category',
        variant: 'destructive'
      });
    }
  };

  const handleEdit = (category: PartnerCategoryDto) => {
    setSelectedCategory(category);
    setEditFormData({
      categoryName: category.categoryName,
      description: category.description || '',
      isActive: category.isActive,
      displayOrder: category.displayOrder
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedCategory?.id) return;

    try {
      await partnerCategoryService.update(selectedCategory.id, editFormData);
      toast({
        title: 'Success',
        description: 'Partner category updated successfully'
      });
      setIsEditDialogOpen(false);
      resetForm();
      fetchCategories();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to update category',
        variant: 'destructive'
      });
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this partner category?')) {
      return;
    }

    try {
      await partnerCategoryService.delete(id);
      toast({
        title: 'Success',
        description: 'Partner category deleted successfully'
      });
      fetchCategories();
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to delete category',
        variant: 'destructive'
      });
    }
  };

  const resetForm = () => {
    setFormData({
      categoryName: '',
      categoryCode: '',
      description: '',
      categoryType: 'Supplier',
      displayOrder: 0
    });
    setEditFormData({
      categoryName: '',
      description: '',
      isActive: true,
      displayOrder: 0
    });
    setSelectedCategory(null);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Partner Categories</h1>
          <p className="text-muted-foreground">
            Manage business partner categories for suppliers and contractors
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Category
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Partner Category</DialogTitle>
              <DialogDescription>
                Create a new category to classify business partners.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="categoryName">Category Name</Label>
                  <Input
                    id="categoryName"
                    value={formData.categoryName}
                    onChange={(e) => setFormData({...formData, categoryName: e.target.value})}
                    placeholder="Enter category name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="categoryCode">Category Code</Label>
                  <Input
                    id="categoryCode"
                    value={formData.categoryCode}
                    onChange={(e) => setFormData({...formData, categoryCode: e.target.value.toUpperCase()})}
                    placeholder="e.g., CONST, ELEC"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="categoryType">Category Type</Label>
                <select
                  id="categoryType"
                  value={formData.categoryType}
                  onChange={(e) => setFormData({...formData, categoryType: e.target.value})}
                  className="w-full rounded-md border border-input bg-background px-3 py-2"
                >
                  <option value="Supplier">Supplier</option>
                  <option value="Contractor">Contractor</option>
                  <option value="Both">Both</option>
                </select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this category..."
                  rows={3}
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Add Category
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
            <BreadcrumbPage>Partner Categories</BreadcrumbPage>
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
                <p className="text-sm text-muted-foreground">Total Categories</p>
              </div>
              <Tag className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(cat => cat.isActive).length}
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
                  {filteredData.reduce((sum, cat) => sum + (cat.partnerCount || 0), 0)}
                </p>
                <p className="text-sm text-muted-foreground">Total Partners</p>
              </div>
              <Tag className="h-8 w-8 text-orange-500" />
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
                placeholder="Search categories..."
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

      {/* Categories List */}
      <Card>
        <CardHeader>
          <CardTitle>Partner Categories</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : error ? 'Error loading data' : `${filteredData.length} categor${filteredData.length === 1 ? 'y' : 'ies'} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="flex items-center justify-center py-8">
              <div className="text-muted-foreground">Loading partner categories...</div>
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
                  No partner categories found. {searchTerm || statusFilter !== 'all' ? 'Try adjusting your filters.' : 'Create your first category to get started.'}
                </div>
              ) : (
                filteredData.map((category) => (
                  <div key={category.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-start justify-between">
                      <div className="space-y-2 flex-1">
                        <div className="flex items-center space-x-3">
                          <h3 className="font-semibold">{category.categoryName}</h3>
                          <Badge variant="outline">{category.categoryCode}</Badge>
                          <Badge variant="outline">{category.categoryType}</Badge>
                          <Badge className={category.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                            {category.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm text-muted-foreground">
                          <div>
                            <span className="font-medium">Partners:</span> {category.partnerCount || 0}
                          </div>
                          {category.createdAt && (
                            <div>
                              <span className="font-medium">Created:</span> {new Date(category.createdAt).toLocaleDateString()}
                            </div>
                          )}
                        </div>

                        {category.description && (
                          <p className="text-sm text-muted-foreground">{category.description}</p>
                        )}
                      </div>

                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleEdit(category)}>
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleDelete(category.id)}
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
            <DialogTitle>Edit Partner Category</DialogTitle>
            <DialogDescription>
              Update the partner category information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="edit-categoryName">Category Name</Label>
              <Input
                id="edit-categoryName"
                value={editFormData.categoryName}
                onChange={(e) => setEditFormData({...editFormData, categoryName: e.target.value})}
                placeholder="Enter category name"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={editFormData.description}
                onChange={(e) => setEditFormData({...editFormData, description: e.target.value})}
                placeholder="Describe this category..."
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

            <div className="space-y-2">
              <Label htmlFor="edit-displayOrder">Display Order</Label>
              <Input
                id="edit-displayOrder"
                type="number"
                value={editFormData.displayOrder}
                onChange={(e) => setEditFormData({...editFormData, displayOrder: parseInt(e.target.value) || 0})}
                placeholder="0"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>
              Update Category
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
