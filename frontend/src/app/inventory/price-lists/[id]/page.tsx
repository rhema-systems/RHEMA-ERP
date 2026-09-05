'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Plus, Edit, Trash2, ArrowLeft, DollarSign, Package, CheckCircle, XCircle, Send, MoreHorizontal, Search, Eye } from 'lucide-react';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { toast } from 'sonner';
import { format } from 'date-fns';
import { priceListService, PriceListDto, PriceListLineDto, CreatePriceListLineDto, PriceListType, PriceListStatus, PriceListApprovalStatus, getPriceListTypeLabel, getPriceListStatusLabel, getPriceListApprovalStatusLabel } from '@/services/priceListService';
import { inventoryManagementService, InventoryItemDto } from '@/services/inventoryManagementService';

type ProblemDetailsPayload = {
  detail?: string;
  title?: string;
  code?: string;
  extensions?: { code?: string };
};

const getErrorMessage = (error: unknown, fallback: string) => {
  const problem = (error as { response?: { data?: ProblemDetailsPayload } })?.response?.data;
  const detail = problem?.detail || problem?.title || (error instanceof Error ? error.message : fallback);
  const code = problem?.code || problem?.extensions?.code;
  return code ? `${detail} (${code})` : detail;
};

interface InventoryItemOption {
  id: string;
  itemCode: string;
  name: string;
  unitOfMeasure: string;
  listPrice: number;
}

type PriceListLineFormData = Omit<
  CreatePriceListLineDto,
  'discountPercent' | 'minQuantity' | 'unitOfMeasure'
> & {
  discountPercent: number;
  minQuantity: number;
  unitOfMeasure: string;
};

export default function PriceListDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [priceList, setPriceList] = useState<PriceListDto | null>(null);
  const [lines, setLines] = useState<PriceListLineDto[]>([]);
  const [filteredLines, setFilteredLines] = useState<PriceListLineDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [isAddLineDialogOpen, setIsAddLineDialogOpen] = useState(false);
  const [isEditLineDialogOpen, setIsEditLineDialogOpen] = useState(false);
  const [isViewLineDialogOpen, setIsViewLineDialogOpen] = useState(false);
  const [isBulkUpdateDialogOpen, setIsBulkUpdateDialogOpen] = useState(false);
  const [selectedLine, setSelectedLine] = useState<PriceListLineDto | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<PriceListLineDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [bulkPercentage, setBulkPercentage] = useState<number>(0);
  const [lineFormData, setLineFormData] = useState<PriceListLineFormData>({
    inventoryItemId: '', unitOfMeasure: 'EA', basePrice: 0, discountPercent: 0, minQuantity: 1
  });

  // Inventory items for selection
  const [inventoryItems, setInventoryItems] = useState<InventoryItemOption[]>([]);
  const [itemSearchTerm, setItemSearchTerm] = useState('');
  const [isItemSelectorOpen, setIsItemSelectorOpen] = useState(false);
  const [selectedItem, setSelectedItem] = useState<InventoryItemOption | null>(null);

  useEffect(() => { loadPriceList(); loadInventoryItems(); }, [id]);
  useEffect(() => { filterLines(); }, [lines, searchTerm]);

  const loadPriceList = async () => {
    try {
      setLoading(true);
      const [priceListData, linesData] = await Promise.all([
        priceListService.getPriceListById(id),
        priceListService.getPriceListLines(id)
      ]);
      setPriceList(priceListData);
      setLines(linesData);
    } catch (error) {
      console.error('Error loading price list:', error);
      toast.error('Failed to load price list');
      router.push('/inventory/price-lists');
    } finally {
      setLoading(false);
    }
  };

  const loadInventoryItems = async () => {
    try {
      const items = await inventoryManagementService.getInventoryItems({ isActive: true });
      setInventoryItems(items.map(item => ({
        id: item.id,
        itemCode: item.itemCode,
        name: item.name,
        unitOfMeasure: item.unitOfMeasure,
        listPrice: item.listPrice
      })));
    } catch (error) {
      console.error('Error loading inventory items:', error);
    }
  };

  // Filter items based on search and exclude already added items
  const availableItems = useMemo(() => {
    const addedItemIds = new Set(lines.map(l => l.inventoryItemId));
    let items = inventoryItems.filter(item => !addedItemIds.has(item.id));
    if (itemSearchTerm) {
      const term = itemSearchTerm.toLowerCase();
      items = items.filter(item =>
        item.itemCode.toLowerCase().includes(term) ||
        item.name.toLowerCase().includes(term)
      );
    }
    return items.slice(0, 50); // Limit to 50 items for performance
  }, [inventoryItems, lines, itemSearchTerm]);

  const filterLines = () => {
    if (!searchTerm) { setFilteredLines(lines); return; }
    const term = searchTerm.toLowerCase();
    setFilteredLines(lines.filter(l => l.itemCode?.toLowerCase().includes(term) || l.itemName?.toLowerCase().includes(term)));
  };

  const handleSelectItem = (item: InventoryItemOption) => {
    setSelectedItem(item);
    setLineFormData({
      ...lineFormData,
      inventoryItemId: item.id,
      unitOfMeasure: item.unitOfMeasure,
      basePrice: item.listPrice
    });
    setIsItemSelectorOpen(false);
    setItemSearchTerm('');
  };

  const handleAddLine = async () => {
    if (!lineFormData.inventoryItemId) {
      toast.error('Please select an item');
      return;
    }
    try {
      await priceListService.createPriceListLine(id, lineFormData);
      toast.success('Line added successfully');
      setIsAddLineDialogOpen(false);
      resetLineForm();
      loadPriceList();
    } catch (error) {
      console.error('Error adding line:', error);
      toast.error('Failed to add line');
    }
  };

  const handleUpdateLine = async () => {
    if (!selectedLine) return;
    try {
      await priceListService.updatePriceListLine(id, selectedLine.id, { ...lineFormData, isActive: selectedLine.isActive });
      toast.success('Line updated successfully');
      setIsEditLineDialogOpen(false);
      loadPriceList();
    } catch (error) {
      console.error('Error updating line:', error);
      toast.error('Failed to update line');
    }
  };

  const confirmDeleteLine = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await priceListService.deletePriceListLine(id, deleteTarget.id);
      toast.success('Line deleted successfully');
      setDeleteTarget(null);
      await loadPriceList();
      return true;
    } catch (error) {
      console.error('Error deleting line:', error);
      toast.error(getErrorMessage(error, 'Failed to delete line'));
      return false;
    } finally {
      setDeleting(false);
    }
  };

  const handleBulkUpdate = async () => {
    try {
      const count = await priceListService.bulkUpdatePrices(id, bulkPercentage);
      toast.success(`${count} prices updated successfully`);
      setIsBulkUpdateDialogOpen(false);
      setBulkPercentage(0);
      loadPriceList();
    } catch (error) {
      console.error('Error bulk updating prices:', error);
      toast.error('Failed to bulk update prices');
    }
  };

  const handleSubmitForApproval = async () => {
    try {
      await priceListService.submitForApproval(id);
      toast.success('Submitted for approval');
      loadPriceList();
    } catch (error) {
      console.error('Error submitting for approval:', error);
      toast.error('Failed to submit for approval');
    }
  };

  const handleApprove = async () => {
    try {
      await priceListService.approvePriceList(id);
      toast.success('Price list approved');
      loadPriceList();
    } catch (error) {
      console.error('Error approving:', error);
      toast.error('Failed to approve');
    }
  };

  const handleActivate = async () => {
    try {
      await priceListService.activatePriceList(id);
      toast.success('Price list activated');
      loadPriceList();
    } catch (error) {
      console.error('Error activating:', error);
      toast.error('Failed to activate');
    }
  };

  const resetLineForm = () => {
    setLineFormData({ inventoryItemId: '', unitOfMeasure: 'EA', basePrice: 0, discountPercent: 0, minQuantity: 1 });
    setSelectedItem(null);
    setItemSearchTerm('');
  };

  const openEditLineDialog = (line: PriceListLineDto) => {
    setSelectedLine(line);
    // Set the selected item for display
    const item = inventoryItems.find(i => i.id === line.inventoryItemId);
    setSelectedItem(item || { id: line.inventoryItemId, itemCode: line.itemCode || '', name: line.itemName || '', unitOfMeasure: line.unitOfMeasure, listPrice: line.basePrice });
    setLineFormData({
      inventoryItemId: line.inventoryItemId, unitOfMeasure: line.unitOfMeasure,
      basePrice: line.basePrice, discountPercent: line.discountPercent, minQuantity: line.minQuantity
    });
    setIsEditLineDialogOpen(true);
  };

  const openViewLineDialog = (line: PriceListLineDto) => {
    setSelectedLine(line);
    setIsViewLineDialogOpen(true);
  };

  const getStatusBadge = (status: PriceListStatus) => {
    const variants: Record<PriceListStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      [PriceListStatus.Draft]: 'secondary', [PriceListStatus.Active]: 'default',
      [PriceListStatus.Expired]: 'outline', [PriceListStatus.Superseded]: 'outline', [PriceListStatus.Cancelled]: 'destructive'
    };
    return <Badge variant={variants[status]}>{getPriceListStatusLabel(status)}</Badge>;
  };

  const getApprovalBadge = (status: PriceListApprovalStatus) => {
    const variants: Record<PriceListApprovalStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      [PriceListApprovalStatus.Draft]: 'secondary', [PriceListApprovalStatus.PendingApproval]: 'outline',
      [PriceListApprovalStatus.Approved]: 'default', [PriceListApprovalStatus.Rejected]: 'destructive'
    };
    return <Badge variant={variants[status]}>{getPriceListApprovalStatusLabel(status)}</Badge>;
  };

  if (loading) return <div className="flex items-center justify-center h-64">Loading...</div>;
  if (!priceList) return <div className="flex items-center justify-center h-64">Price list not found</div>;

  return (
    <div className="space-y-6">
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory/price-lists">Price Lists</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>{priceList.priceListCode}</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.push('/inventory/price-lists')}><ArrowLeft className="h-4 w-4" /></Button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-3xl font-bold tracking-tight">{priceList.name}</h1>
              {getStatusBadge(priceList.status)}
              {getApprovalBadge(priceList.approvalStatus)}
            </div>
            <p className="text-muted-foreground">{priceList.priceListCode} • {getPriceListTypeLabel(priceList.type)} • {priceList.currency}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {priceList.approvalStatus === PriceListApprovalStatus.Draft && (
            <Button variant="outline" onClick={handleSubmitForApproval}><Send className="mr-2 h-4 w-4" />Submit for Approval</Button>
          )}
          {priceList.approvalStatus === PriceListApprovalStatus.PendingApproval && (
            <Button onClick={handleApprove}><CheckCircle className="mr-2 h-4 w-4" />Approve</Button>
          )}
          {priceList.status === PriceListStatus.Draft && priceList.approvalStatus === PriceListApprovalStatus.Approved && (
            <Button onClick={handleActivate}><CheckCircle className="mr-2 h-4 w-4" />Activate</Button>
          )}
          <Button variant="outline" onClick={() => router.push(`/inventory/price-lists/${id}/edit`)}><Edit className="mr-2 h-4 w-4" />Edit</Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Total Lines</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold">{lines.length}</div></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Effective Date</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold">{priceList.effectiveDate ? format(new Date(priceList.effectiveDate), 'dd MMM yyyy') : '-'}</div></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Expiration</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold">{priceList.expirationDate ? format(new Date(priceList.expirationDate), 'dd MMM yyyy') : 'No expiration'}</div></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Priority</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-bold">{priceList.priority}</div></CardContent></Card>
      </div>

      <Tabs defaultValue="lines" className="space-y-4">
        <TabsList><TabsTrigger value="lines">Price Lines</TabsTrigger><TabsTrigger value="details">Details</TabsTrigger></TabsList>

        <TabsContent value="lines" className="space-y-4">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div><CardTitle>Price Lines</CardTitle><CardDescription>{filteredLines.length} item(s)</CardDescription></div>
                <div className="flex items-center gap-2">
                  <div className="relative w-64">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input placeholder="Search items..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="pl-10" />
                  </div>
                  <Button variant="outline" onClick={() => setIsBulkUpdateDialogOpen(true)}>Bulk Update</Button>
                  <Button onClick={() => setIsAddLineDialogOpen(true)}><Plus className="mr-2 h-4 w-4" />Add Line</Button>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              {filteredLines.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No price lines found. Add items to this price list.</div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Item Code</TableHead><TableHead>Item Name</TableHead><TableHead>UoM</TableHead>
                      <TableHead className="text-right">Base Price</TableHead><TableHead className="text-right">Discount %</TableHead>
                      <TableHead className="text-right">Net Price</TableHead><TableHead>Min Qty</TableHead><TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {filteredLines.map((line) => (
                      <TableRow key={line.id}>
                        <TableCell className="font-medium">{line.itemCode}</TableCell>
                        <TableCell>{line.itemName}</TableCell>
                        <TableCell>{line.unitOfMeasure}</TableCell>
                        <TableCell className="text-right">{priceList.currency} {line.basePrice.toFixed(2)}</TableCell>
                        <TableCell className="text-right">{line.discountPercent}%</TableCell>
                        <TableCell className="text-right font-medium">{priceList.currency} {line.netPrice.toFixed(2)}</TableCell>
                        <TableCell>{line.minQuantity}</TableCell>
                        <TableCell className="text-right">
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild><Button variant="ghost" size="icon"><MoreHorizontal className="h-4 w-4" /></Button></DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => openViewLineDialog(line)}><Eye className="mr-2 h-4 w-4" />View</DropdownMenuItem>
                              <DropdownMenuItem onClick={() => openEditLineDialog(line)}><Edit className="mr-2 h-4 w-4" />Edit</DropdownMenuItem>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem className="text-destructive" onClick={() => setDeleteTarget(line)}><Trash2 className="mr-2 h-4 w-4" />Delete</DropdownMenuItem>
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
        </TabsContent>

        <TabsContent value="details">
          <Card>
            <CardHeader><CardTitle>Price List Details</CardTitle></CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div><Label className="text-muted-foreground">Description</Label><p>{priceList.description || 'No description'}</p></div>
              <div><Label className="text-muted-foreground">Is Default</Label><p>{priceList.isDefault ? 'Yes' : 'No'}</p></div>
              <div><Label className="text-muted-foreground">Customer Group</Label><p>{priceList.customerGroupName || 'All Customers'}</p></div>
              <div><Label className="text-muted-foreground">Supplier Group</Label><p>{priceList.supplierGroupName || 'All Suppliers'}</p></div>
              <div><Label className="text-muted-foreground">Created</Label><p>{format(new Date(priceList.createdAt), 'dd MMM yyyy HH:mm')}</p></div>
              <div><Label className="text-muted-foreground">Last Updated</Label><p>{priceList.updatedAt ? format(new Date(priceList.updatedAt), 'dd MMM yyyy HH:mm') : 'Never'}</p></div>
              {priceList.notes && <div className="md:col-span-2"><Label className="text-muted-foreground">Notes</Label><p>{priceList.notes}</p></div>}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Add Line Dialog */}
      <Dialog open={isAddLineDialogOpen} onOpenChange={(open) => { setIsAddLineDialogOpen(open); if (!open) resetLineForm(); }}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Add Price Line</DialogTitle><DialogDescription>Select an inventory item and set its price for this price list.</DialogDescription></DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label>Select Item *</Label>
              <Popover open={isItemSelectorOpen} onOpenChange={setIsItemSelectorOpen}>
                <PopoverTrigger asChild>
                  <Button variant="outline" role="combobox" aria-expanded={isItemSelectorOpen} className="w-full justify-between">
                    {selectedItem ? (
                      <span className="truncate">{selectedItem.itemCode} - {selectedItem.name}</span>
                    ) : (
                      <span className="text-muted-foreground">Search and select an item...</span>
                    )}
                    <Package className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-[400px] p-0" align="start">
                  <div className="p-2 border-b">
                    <Input
                      placeholder="Search by code or name..."
                      value={itemSearchTerm}
                      onChange={(e) => setItemSearchTerm(e.target.value)}
                      className="h-9"
                    />
                  </div>
                  <div className="max-h-[300px] overflow-y-auto">
                    {availableItems.length === 0 ? (
                      <div className="p-4 text-center text-muted-foreground text-sm">No items found.</div>
                    ) : (
                      <div className="p-1">
                        {availableItems.map((item) => (
                          <div
                            key={item.id}
                            className="flex items-center justify-between p-2 hover:bg-accent rounded-md cursor-pointer"
                            onClick={() => handleSelectItem(item)}
                          >
                            <div className="flex flex-col">
                              <span className="font-medium text-sm">{item.itemCode}</span>
                              <span className="text-xs text-muted-foreground">{item.name}</span>
                            </div>
                            <span className="text-xs text-muted-foreground">{item.unitOfMeasure} | {priceList?.currency} {item.listPrice.toFixed(2)}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </PopoverContent>
              </Popover>
              {selectedItem && (
                <p className="text-sm text-muted-foreground">Default price: {priceList?.currency} {selectedItem.listPrice.toFixed(2)} | UoM: {selectedItem.unitOfMeasure}</p>
              )}
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Base Price *</Label><Input type="number" step="0.01" value={lineFormData.basePrice} onChange={(e) => setLineFormData({...lineFormData, basePrice: parseFloat(e.target.value) || 0})} /></div>
              <div className="space-y-2"><Label>Discount %</Label><Input type="number" step="0.1" value={lineFormData.discountPercent} onChange={(e) => setLineFormData({...lineFormData, discountPercent: parseFloat(e.target.value) || 0})} /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Min Quantity</Label><Input type="number" value={lineFormData.minQuantity} onChange={(e) => setLineFormData({...lineFormData, minQuantity: parseInt(e.target.value) || 1})} /></div>
              <div className="space-y-2"><Label>Unit of Measure</Label><Input value={lineFormData.unitOfMeasure} onChange={(e) => setLineFormData({...lineFormData, unitOfMeasure: e.target.value})} /></div>
            </div>
            {lineFormData.basePrice > 0 && lineFormData.discountPercent > 0 && (
              <div className="p-3 bg-muted rounded-lg">
                <Label className="text-muted-foreground">Net Price</Label>
                <p className="text-xl font-bold text-primary">{priceList?.currency} {(lineFormData.basePrice * (1 - lineFormData.discountPercent / 100)).toFixed(2)}</p>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsAddLineDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleAddLine} disabled={!selectedItem || lineFormData.basePrice <= 0}>Add Item</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Line Dialog */}
      <Dialog open={isEditLineDialogOpen} onOpenChange={setIsEditLineDialogOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Edit Price Line</DialogTitle><DialogDescription>Update the pricing for this item.</DialogDescription></DialogHeader>
          <div className="grid gap-4 py-4">
            {selectedItem && (
              <div className="p-3 bg-muted rounded-lg">
                <div className="flex items-center gap-2">
                  <Package className="h-4 w-4 text-muted-foreground" />
                  <span className="font-medium">{selectedItem.itemCode}</span>
                  <span className="text-muted-foreground">-</span>
                  <span>{selectedItem.name}</span>
                </div>
              </div>
            )}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Base Price *</Label><Input type="number" step="0.01" value={lineFormData.basePrice} onChange={(e) => setLineFormData({...lineFormData, basePrice: parseFloat(e.target.value) || 0})} /></div>
              <div className="space-y-2"><Label>Discount %</Label><Input type="number" step="0.1" value={lineFormData.discountPercent} onChange={(e) => setLineFormData({...lineFormData, discountPercent: parseFloat(e.target.value) || 0})} /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Min Quantity</Label><Input type="number" value={lineFormData.minQuantity} onChange={(e) => setLineFormData({...lineFormData, minQuantity: parseInt(e.target.value) || 1})} /></div>
              <div className="space-y-2"><Label>Unit of Measure</Label><Input value={lineFormData.unitOfMeasure} onChange={(e) => setLineFormData({...lineFormData, unitOfMeasure: e.target.value})} /></div>
            </div>
            {lineFormData.basePrice > 0 && (
              <div className="p-3 bg-muted rounded-lg">
                <Label className="text-muted-foreground">Net Price</Label>
                <p className="text-xl font-bold text-primary">{priceList?.currency} {(lineFormData.basePrice * (1 - lineFormData.discountPercent / 100)).toFixed(2)}</p>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditLineDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleUpdateLine}>Save Changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* View Line Dialog */}
      <Dialog open={isViewLineDialogOpen} onOpenChange={setIsViewLineDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Price Line Details</DialogTitle><DialogDescription>View pricing information for this item.</DialogDescription></DialogHeader>
          {selectedLine && (
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div><Label className="text-muted-foreground">Item Code</Label><p className="font-medium">{selectedLine.itemCode || '-'}</p></div>
                <div><Label className="text-muted-foreground">Item Name</Label><p className="font-medium">{selectedLine.itemName || '-'}</p></div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div><Label className="text-muted-foreground">Base Price</Label><p className="font-medium">{priceList?.currency} {selectedLine.basePrice.toFixed(2)}</p></div>
                <div><Label className="text-muted-foreground">Discount</Label><p className="font-medium">{selectedLine.discountPercent}%</p></div>
                <div><Label className="text-muted-foreground">Net Price</Label><p className="font-medium text-primary">{priceList?.currency} {selectedLine.netPrice.toFixed(2)}</p></div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div><Label className="text-muted-foreground">Unit of Measure</Label><p className="font-medium">{selectedLine.unitOfMeasure}</p></div>
                <div><Label className="text-muted-foreground">Min Quantity</Label><p className="font-medium">{selectedLine.minQuantity}</p></div>
                <div><Label className="text-muted-foreground">Max Quantity</Label><p className="font-medium">{selectedLine.maxQuantity || 'Unlimited'}</p></div>
              </div>
              {selectedLine.previousPrice && (
                <div className="grid grid-cols-2 gap-4">
                  <div><Label className="text-muted-foreground">Previous Price</Label><p className="font-medium">{priceList?.currency} {selectedLine.previousPrice.toFixed(2)}</p></div>
                  <div><Label className="text-muted-foreground">Price Change</Label><p className={`font-medium ${(selectedLine.priceChangePercent || 0) > 0 ? 'text-green-600' : 'text-red-600'}`}>{selectedLine.priceChangePercent?.toFixed(2)}%</p></div>
                </div>
              )}
              {selectedLine.supplierItemCode && (
                <div className="grid grid-cols-3 gap-4">
                  <div><Label className="text-muted-foreground">Supplier Item Code</Label><p className="font-medium">{selectedLine.supplierItemCode}</p></div>
                  <div><Label className="text-muted-foreground">Min Order Qty</Label><p className="font-medium">{selectedLine.minimumOrderQuantity || '-'}</p></div>
                  <div><Label className="text-muted-foreground">Lead Time</Label><p className="font-medium">{selectedLine.leadTimeDays ? `${selectedLine.leadTimeDays} days` : '-'}</p></div>
                </div>
              )}
              {selectedLine.notes && (
                <div><Label className="text-muted-foreground">Notes</Label><p className="font-medium">{selectedLine.notes}</p></div>
              )}
              <div className="flex items-center gap-2">
                <Label className="text-muted-foreground">Status:</Label>
                <Badge variant={selectedLine.isActive ? 'default' : 'secondary'}>{selectedLine.isActive ? 'Active' : 'Inactive'}</Badge>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewLineDialogOpen(false)}>Close</Button>
            <Button onClick={() => { setIsViewLineDialogOpen(false); if (selectedLine) openEditLineDialog(selectedLine); }}>Edit</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Bulk Update Dialog */}
      <Dialog open={isBulkUpdateDialogOpen} onOpenChange={setIsBulkUpdateDialogOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Bulk Update Prices</DialogTitle><DialogDescription>Apply a percentage change to all prices in this list.</DialogDescription></DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label>Percentage Change</Label>
              <Input type="number" value={bulkPercentage} onChange={(e) => setBulkPercentage(parseFloat(e.target.value) || 0)} placeholder="e.g., 5 for 5% increase, -10 for 10% decrease" />
              <p className="text-sm text-muted-foreground">Positive values increase prices, negative values decrease prices.</p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsBulkUpdateDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleBulkUpdate} disabled={bulkPercentage === 0}>Apply</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => { if (!open && !deleting) setDeleteTarget(null); }}
        title="Delete price-list line?"
        description={`Remove ${deleteTarget?.itemCode || deleteTarget?.itemName || 'this item'} from ${priceList?.name || 'the price list'}? This action cannot be undone.`}
        confirmText="Delete line"
        variant="destructive"
        onConfirm={confirmDeleteLine}
        isLoading={deleting}
      />
    </div>
  );
}

