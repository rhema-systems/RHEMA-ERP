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
  CreditCard,
  Settings
} from 'lucide-react';
import {
  paymentTermService,
  PaymentTermListDto as PaymentTermDto,
  CreatePaymentTermDto,
  UpdatePaymentTermDto
} from '@/services/financeCommonService';
import { toast } from 'sonner';

export default function PaymentTermsPage() {
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermDto[] | undefined>(undefined);
  const [filteredTerms, setFilteredTerms] = useState<PaymentTermDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [applicableToFilter, setApplicableToFilter] = useState('all');
 
  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedTerm, setSelectedTerm] = useState<PaymentTermDto | null>(null);
 
  // Form state
  const [formData, setFormData] = useState<CreatePaymentTermDto>({
    code: '',
    name: '',
    description: '',
    dueDays: 0,
    discountPercent: 0,
    discountDays: 0,
    isActive: true,
    isDefault: false,
    displayOrder: 0,
    applicableTo: 'All'
  });

  const applicableToOptions = ['All', 'Supplier', 'Customer', 'Contractor'];

  // Fetch payment terms
  const fetchPaymentTerms = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await paymentTermService.getAll();
      setPaymentTerms(data);
    } catch (err: any) {
      console.error('Error fetching payment terms:', err);
      setError('Failed to load payment terms');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPaymentTerms();
  }, []);

  // Filter payment terms
  useEffect(() => {
    if (!paymentTerms) {
      setFilteredTerms([]);
      return;
    }

    // Filter out any undefined/null values first
    let filtered = paymentTerms.filter((t): t is PaymentTermDto => t != null);

    if (searchTerm) {
      filtered = filtered.filter(t =>
        t.code?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        t.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        t.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(t =>
        statusFilter === 'active' ? t.isActive : !t.isActive
      );
    }

    if (applicableToFilter !== 'all') {
      filtered = filtered.filter(t => t.applicableTo === applicableToFilter);
    }

    setFilteredTerms(filtered);
  }, [searchTerm, statusFilter, applicableToFilter, paymentTerms]);

  const handleCreate = async () => {
    try {
      const newTerm = await paymentTermService.create(formData);
      setPaymentTerms(prev => [...(prev || []), newTerm]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (err: any) {
      console.error('Error creating payment term:', err);
      toast.error('Failed to create payment term');
    }
  };

  const handleEdit = (term: PaymentTermDto) => {
    setSelectedTerm(term);
    setFormData({
      code: term.code,
      name: term.name,
      description: term.description || '',
      dueDays: term.dueDays,
      discountPercent: term.discountPercent,
      discountDays: term.discountDays,
      isActive: term.isActive,
      isDefault: term.isDefault,
      displayOrder: term.displayOrder,
      applicableTo: term.applicableTo
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedTerm) return;
    try {
      const updateData: UpdatePaymentTermDto = {
        id: selectedTerm.id,
        code: selectedTerm.code,
        name: formData.name,
        description: formData.description,
        dueDays: formData.dueDays,
        discountPercent: formData.discountPercent,
        discountDays: formData.discountDays,
        isActive: formData.isActive,
        isDefault: formData.isDefault,
        displayOrder: formData.displayOrder,
        applicableTo: formData.applicableTo
      };
      const updated = await paymentTermService.update(selectedTerm.id, updateData);
      setPaymentTerms(prev => (prev || []).map(t => t.id === selectedTerm.id ? updated : t));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (err: any) {
      console.error('Error updating payment term:', err);
      toast.error('Failed to update payment term');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this payment term?')) return;
    try {
      await paymentTermService.delete(id);
      setPaymentTerms(prev => (prev || []).filter(t => t.id !== id));
    } catch (err: any) {
      console.error('Error deleting payment term:', err);
      toast.error('Failed to delete payment term');
    }
  };

  const resetForm = () => {
    setFormData({
      code: '',
      name: '',
      description: '',
      dueDays: 0,
      discountPercent: 0,
      discountDays: 0,
      isActive: true,
      isDefault: false,
      displayOrder: 0,
      applicableTo: 'All'
    });
    setSelectedTerm(null);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Payment Terms</h1>
          <p className="text-muted-foreground">
            Manage payment terms for business partners and transactions
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" />Add Payment Term</Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[500px]">
            <DialogHeader>
              <DialogTitle>Add Payment Term</DialogTitle>
              <DialogDescription>Create a new payment term for financial transactions.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="code">Code</Label>
                  <Input id="code" value={formData.code} onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})} placeholder="e.g., NET30, COD" />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="displayOrder">Display Order</Label>
                  <Input id="displayOrder" type="number" value={formData.displayOrder} onChange={(e) => setFormData({...formData, displayOrder: parseInt(e.target.value) || 0})} />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="name">Name</Label>
                <Input id="name" value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} placeholder="e.g., Net 30 Days, Cash on Delivery" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Input id="description" value={formData.description} onChange={(e) => setFormData({...formData, description: e.target.value})} placeholder="Optional description" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="dueDays">Due Days</Label>
                  <Input id="dueDays" type="number" value={formData.dueDays} onChange={(e) => setFormData({...formData, dueDays: parseInt(e.target.value) || 0})} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="discountPercent">Discount %</Label>
                  <Input id="discountPercent" type="number" step="0.01" value={formData.discountPercent} onChange={(e) => setFormData({...formData, discountPercent: parseFloat(e.target.value) || 0})} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="discountDays">Discount Days</Label>
                  <Input id="discountDays" type="number" value={formData.discountDays} onChange={(e) => setFormData({...formData, discountDays: parseInt(e.target.value) || 0})} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="applicableTo">Applicable To</Label>
                  <Select value={formData.applicableTo} onValueChange={(v) => setFormData({...formData, applicableTo: v})}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {applicableToOptions.map(opt => <SelectItem key={opt} value={opt}>{opt}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch id="isActive" checked={formData.isActive} onCheckedChange={(v) => setFormData({...formData, isActive: v})} />
                  <Label htmlFor="isActive">Active</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch id="isDefault" checked={formData.isDefault} onCheckedChange={(v) => setFormData({...formData, isDefault: v})} />
                  <Label htmlFor="isDefault">Default</Label>
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
          <BreadcrumbItem><BreadcrumbPage>Payment Terms</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{(paymentTerms || []).filter(t => t != null).length}</p>
                <p className="text-sm text-muted-foreground">Total Terms</p>
              </div>
              <CreditCard className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{(paymentTerms || []).filter(t => t != null && t.isActive).length}</p>
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
                <p className="text-2xl font-bold">{(paymentTerms || []).filter(t => t != null && t.isDefault).length}</p>
                <p className="text-sm text-muted-foreground">Default Terms</p>
              </div>
              <CreditCard className="h-8 w-8 text-purple-500" />
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
              <Input placeholder="Search payment terms..." className="pl-8" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
            </div>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="inactive">Inactive</SelectItem>
              </SelectContent>
            </Select>
            <Select value={applicableToFilter} onValueChange={setApplicableToFilter}>
              <SelectTrigger><SelectValue placeholder="Applicable To" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {applicableToOptions.map(opt => <SelectItem key={opt} value={opt}>{opt}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Payment Terms List */}
      <Card>
        <CardHeader>
          <CardTitle>Payment Terms</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${(filteredTerms || []).length} term(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {filteredTerms.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No payment terms found.</div>
              ) : (
                filteredTerms.map((term) => (
                  <div key={term.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-blue-100 flex items-center justify-center">
                          <span className="text-lg font-bold text-blue-600">{term.code || ''}</span>
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{term.name || ''}</h3>
                            <Badge variant="outline">{term.code || ''}</Badge>
                            <Badge className={term.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                              {term.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                            {term.isDefault && <Badge className="bg-purple-100 text-purple-800">Default</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">Due in {term.dueDays ?? 0} days</p>
                          {term.description && <p className="text-sm text-muted-foreground">{term.description}</p>}
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleEdit(term)}><Edit className="h-4 w-4" /></Button>
                        <Button size="sm" variant="outline" className="text-red-600" onClick={() => handleDelete(term.id)}><Trash2 className="h-4 w-4" /></Button>
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
            <DialogTitle>Edit Payment Term</DialogTitle>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Code</Label>
                <Input value={formData.code} disabled className="bg-muted" />
              </div>
              <div className="space-y-2">
                <Label>Display Order</Label>
                <Input type="number" value={formData.displayOrder} onChange={(e) => setFormData({...formData, displayOrder: parseInt(e.target.value) || 0})} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} />
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Input value={formData.description} onChange={(e) => setFormData({...formData, description: e.target.value})} />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Due Days</Label>
                <Input type="number" value={formData.dueDays} onChange={(e) => setFormData({...formData, dueDays: parseInt(e.target.value) || 0})} />
              </div>
              <div className="space-y-2">
                <Label>Discount %</Label>
                <Input type="number" step="0.01" value={formData.discountPercent} onChange={(e) => setFormData({...formData, discountPercent: parseFloat(e.target.value) || 0})} />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Discount Days</Label>
                <Input type="number" value={formData.discountDays} onChange={(e) => setFormData({...formData, discountDays: parseInt(e.target.value) || 0})} />
              </div>
              <div className="space-y-2">
                <Label>Applicable To</Label>
                <Select value={formData.applicableTo} onValueChange={(v) => setFormData({...formData, applicableTo: v})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {applicableToOptions.map(opt => <SelectItem key={opt} value={opt}>{opt}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <Switch checked={formData.isActive} onCheckedChange={(v) => setFormData({...formData, isActive: v})} />
                <Label>Active</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch checked={formData.isDefault} onCheckedChange={(v) => setFormData({...formData, isDefault: v})} />
                <Label>Default</Label>
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
