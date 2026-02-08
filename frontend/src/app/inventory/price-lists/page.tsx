'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Plus, Search, Edit, Trash2, Eye, Copy, DollarSign, Filter, MoreHorizontal } from 'lucide-react';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { toast } from 'sonner';
import { format } from 'date-fns';
import { priceListService, PriceListDto, CreatePriceListDto, PriceListType, PriceListStatus, PriceListApprovalStatus, getPriceListTypeLabel, getPriceListStatusLabel, getPriceListApprovalStatusLabel, CustomerGroupDto, SupplierGroupDto } from '@/services/priceListService';

const PriceListTypes = [
  { value: PriceListType.Sales, label: 'Sales' },
  { value: PriceListType.Purchase, label: 'Purchase' },
  { value: PriceListType.Transfer, label: 'Transfer' },
  { value: PriceListType.Contract, label: 'Contract' }
];

export default function PriceListsPage() {
  const router = useRouter();
  const [priceLists, setPriceLists] = useState<PriceListDto[]>([]);
  const [filteredPriceLists, setFilteredPriceLists] = useState<PriceListDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isCopyDialogOpen, setIsCopyDialogOpen] = useState(false);
  const [selectedPriceList, setSelectedPriceList] = useState<PriceListDto | null>(null);
  const [customerGroups, setCustomerGroups] = useState<CustomerGroupDto[]>([]);
  const [supplierGroups, setSupplierGroups] = useState<SupplierGroupDto[]>([]);
  const [formData, setFormData] = useState<CreatePriceListDto>({
    priceListCode: '', name: '', description: '', type: PriceListType.Sales,
    currency: 'USD', effectiveDate: new Date().toISOString().split('T')[0],
    expirationDate: undefined, isDefault: false, priority: 0, customerGroupId: undefined, supplierGroupId: undefined, notes: ''
  });
  const [copyFormData, setCopyFormData] = useState({ newCode: '', newName: '' });

  useEffect(() => { loadPriceLists(); loadGroups(); }, []);
  useEffect(() => { filterPriceLists(); }, [priceLists, searchTerm, typeFilter, statusFilter]);

  const loadPriceLists = async () => {
    try {
      setLoading(true);
      const data = await priceListService.getAllPriceLists();
      setPriceLists(data);
    } catch (error) {
      console.error('Error loading price lists:', error);
      toast.error('Failed to load price lists');
    } finally {
      setLoading(false);
    }
  };

  const loadGroups = async () => {
    try {
      const [custGroups, suppGroups] = await Promise.all([
        priceListService.getActiveCustomerGroups(),
        priceListService.getActiveSupplierGroups()
      ]);
      setCustomerGroups(custGroups);
      setSupplierGroups(suppGroups);
    } catch (error) {
      console.error('Error loading groups:', error);
    }
  };

  const filterPriceLists = () => {
    let filtered = [...priceLists];
    if (searchTerm) {
      const term = searchTerm.toLowerCase();
      filtered = filtered.filter(pl => pl.priceListCode.toLowerCase().includes(term) || pl.name.toLowerCase().includes(term) || pl.description?.toLowerCase().includes(term));
    }
    if (typeFilter !== 'all') filtered = filtered.filter(pl => pl.type === parseInt(typeFilter));
    if (statusFilter !== 'all') filtered = filtered.filter(pl => pl.status === parseInt(statusFilter));
    setFilteredPriceLists(filtered);
  };

  const handleCreate = async () => {
    try {
      await priceListService.createPriceList(formData);
      toast.success('Price list created successfully');
      setIsCreateDialogOpen(false);
      resetForm();
      loadPriceLists();
    } catch (error) {
      console.error('Error creating price list:', error);
      toast.error('Failed to create price list');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this price list?')) return;
    try {
      await priceListService.deletePriceList(id);
      toast.success('Price list deleted successfully');
      loadPriceLists();
    } catch (error) {
      console.error('Error deleting price list:', error);
      toast.error('Failed to delete price list');
    }
  };

  const handleCopy = async () => {
    if (!selectedPriceList) return;
    try {
      await priceListService.copyPriceList(selectedPriceList.id, copyFormData.newCode, copyFormData.newName);
      toast.success('Price list copied successfully');
      setIsCopyDialogOpen(false);
      setCopyFormData({ newCode: '', newName: '' });
      loadPriceLists();
    } catch (error) {
      console.error('Error copying price list:', error);
      toast.error('Failed to copy price list');
    }
  };

  const resetForm = () => {
    setFormData({
      priceListCode: '', name: '', description: '', type: PriceListType.Sales,
      currency: 'USD', effectiveDate: new Date().toISOString().split('T')[0],
      expirationDate: undefined, isDefault: false, priority: 0, notes: ''
    });
  };

  const getStatusBadge = (status: PriceListStatus) => {
    const variants: Record<PriceListStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      [PriceListStatus.Draft]: 'secondary', [PriceListStatus.Active]: 'default',
      [PriceListStatus.Expired]: 'outline', [PriceListStatus.Superseded]: 'outline', [PriceListStatus.Cancelled]: 'destructive'
    };
    return <Badge variant={variants[status]}>{getPriceListStatusLabel(status)}</Badge>;
  };

  const openCopyDialog = (priceList: PriceListDto) => {
    setSelectedPriceList(priceList);
    setCopyFormData({ newCode: `${priceList.priceListCode}-COPY`, newName: `${priceList.name} (Copy)` });
    setIsCopyDialogOpen(true);
  };

  return (
    <div className="space-y-6">
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Price Lists</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Price Lists</h1>
          <p className="text-muted-foreground">Manage pricing structures for sales, purchases, and contracts</p>
        </div>
        <Button onClick={() => setIsCreateDialogOpen(true)}><Plus className="mr-2 h-4 w-4" />New Price List</Button>
      </div>

      <Card>
        <CardContent className="pt-6">
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex-1 min-w-[200px]">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input placeholder="Search price lists..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="pl-10" />
              </div>
            </div>
            <Select value={typeFilter} onValueChange={setTypeFilter}>
              <SelectTrigger className="w-[150px]"><Filter className="mr-2 h-4 w-4" /><SelectValue placeholder="Type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {PriceListTypes.map(t => <SelectItem key={t.value} value={t.value.toString()}>{t.label}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger className="w-[150px]"><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value={PriceListStatus.Draft.toString()}>Draft</SelectItem>
                <SelectItem value={PriceListStatus.Active.toString()}>Active</SelectItem>
                <SelectItem value={PriceListStatus.Expired.toString()}>Expired</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Price Lists Table */}
      <Card>
        <CardHeader>
          <CardTitle>Price Lists</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${filteredPriceLists.length} price list(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8 text-muted-foreground">Loading price lists...</div>
          ) : filteredPriceLists.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">No price lists found. Create your first price list to get started.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Name</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Currency</TableHead>
                  <TableHead>Effective Date</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Lines</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredPriceLists.map((priceList) => (
                  <TableRow key={priceList.id} className="cursor-pointer hover:bg-muted/50" onClick={() => router.push(`/inventory/price-lists/${priceList.id}`)}>
                    <TableCell className="font-medium">{priceList.priceListCode}</TableCell>
                    <TableCell>{priceList.name}</TableCell>
                    <TableCell><Badge variant="outline">{getPriceListTypeLabel(priceList.type)}</Badge></TableCell>
                    <TableCell>{priceList.currency}</TableCell>
                    <TableCell>{priceList.effectiveDate ? format(new Date(priceList.effectiveDate), 'dd MMM yyyy') : '-'}</TableCell>
                    <TableCell>{getStatusBadge(priceList.status)}</TableCell>
                    <TableCell>{priceList.lineCount}</TableCell>
                    <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild><Button variant="ghost" size="icon"><MoreHorizontal className="h-4 w-4" /></Button></DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => router.push(`/inventory/price-lists/${priceList.id}`)}><Eye className="mr-2 h-4 w-4" />View Details</DropdownMenuItem>
                          <DropdownMenuItem onClick={() => router.push(`/inventory/price-lists/${priceList.id}/edit`)}><Edit className="mr-2 h-4 w-4" />Edit</DropdownMenuItem>
                          <DropdownMenuItem onClick={() => openCopyDialog(priceList)}><Copy className="mr-2 h-4 w-4" />Copy</DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem className="text-destructive" onClick={() => handleDelete(priceList.id)}><Trash2 className="mr-2 h-4 w-4" />Delete</DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Create Dialog */}
      <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Create Price List</DialogTitle>
            <DialogDescription>Create a new price list for your products and services.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="priceListCode">Price List Code *</Label>
                <Input id="priceListCode" value={formData.priceListCode} onChange={(e) => setFormData({...formData, priceListCode: e.target.value})} placeholder="e.g., PL-2024-001" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="type">Type *</Label>
                <Select value={formData.type.toString()} onValueChange={(v) => setFormData({...formData, type: parseInt(v)})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{PriceListTypes.map(t => <SelectItem key={t.value} value={t.value.toString()}>{t.label}</SelectItem>)}</SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="name">Name *</Label>
              <Input id="name" value={formData.name} onChange={(e) => setFormData({...formData, name: e.target.value})} placeholder="Price list name" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" value={formData.description || ''} onChange={(e) => setFormData({...formData, description: e.target.value})} placeholder="Optional description" rows={2} />
            </div>
            <div className="grid grid-cols-3 gap-4">
              <div className="space-y-2">
                <Label htmlFor="currency">Currency</Label>
                <Select value={formData.currency} onValueChange={(v) => setFormData({...formData, currency: v})}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="USD">USD</SelectItem><SelectItem value="EUR">EUR</SelectItem>
                    <SelectItem value="GBP">GBP</SelectItem><SelectItem value="ZAR">ZAR</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="effectiveDate">Effective Date *</Label>
                <Input id="effectiveDate" type="date" value={formData.effectiveDate} onChange={(e) => setFormData({...formData, effectiveDate: e.target.value})} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="expirationDate">Expiration Date</Label>
                <Input id="expirationDate" type="date" value={formData.expirationDate || ''} onChange={(e) => setFormData({...formData, expirationDate: e.target.value || undefined})} />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="customerGroupId">Customer Group</Label>
                <Select value={formData.customerGroupId ? formData.customerGroupId : '__all__'} onValueChange={(v) => setFormData({...formData, customerGroupId: v === '__all__' ? undefined : v})}>
                  <SelectTrigger><SelectValue placeholder="All Customers" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__all__">All Customers</SelectItem>
                    {customerGroups.map(g => <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="supplierGroupId">Supplier Group</Label>
                <Select value={formData.supplierGroupId ? formData.supplierGroupId : '__all__'} onValueChange={(v) => setFormData({...formData, supplierGroupId: v === '__all__' ? undefined : v})}>
                  <SelectTrigger><SelectValue placeholder="All Suppliers" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__all__">All Suppliers</SelectItem>
                    {supplierGroups.map(g => <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="notes">Notes</Label>
              <Textarea id="notes" value={formData.notes || ''} onChange={(e) => setFormData({...formData, notes: e.target.value})} placeholder="Additional notes" rows={2} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleCreate} disabled={!formData.priceListCode || !formData.name}>Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Copy Dialog */}
      <Dialog open={isCopyDialogOpen} onOpenChange={setIsCopyDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Copy Price List</DialogTitle>
            <DialogDescription>Create a copy of "{selectedPriceList?.name}" with a new code and name.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="newCode">New Price List Code *</Label>
              <Input id="newCode" value={copyFormData.newCode} onChange={(e) => setCopyFormData({...copyFormData, newCode: e.target.value})} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="newName">New Name *</Label>
              <Input id="newName" value={copyFormData.newName} onChange={(e) => setCopyFormData({...copyFormData, newName: e.target.value})} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCopyDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleCopy} disabled={!copyFormData.newCode || !copyFormData.newName}>Copy</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

