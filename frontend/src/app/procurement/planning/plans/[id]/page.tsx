'use client';

import { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { ArrowLeft, Edit, FileText, Package, Calendar, Clock, CheckCircle, XCircle, AlertCircle, Send, Loader2, Plus, Trash2, Search, Users, ThumbsUp, ThumbsDown } from 'lucide-react';
import { toast } from 'sonner';
import { procurementPlanService, procurementBudgetService, commonService, type ProcurementPlanDetailDto, type CreateProcurementPlanItemDto, type InventoryItemDto, type ProcurementPlanItemDto, type CreateProcurementPlanItemSupplierDto, type ApproveProcurementPlanDto, type ProcurementBudgetDto } from '@/services/procurementPlanningService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { format } from 'date-fns';
import { FileCheck, ShoppingCart } from 'lucide-react';
import { Checkbox } from '@/components/ui/checkbox';

export default function ProcurementPlanDetailPage() {
  const params = useParams();
  const router = useRouter();
  const planId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';

  const [plan, setPlan] = useState<ProcurementPlanDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);

  // Add Item Dialog State
  const [addItemDialogOpen, setAddItemDialogOpen] = useState(false);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [loadingInventory, setLoadingInventory] = useState(false);
  const [inventorySearchTerm, setInventorySearchTerm] = useState('');
  const [selectedInventoryItem, setSelectedInventoryItem] = useState<InventoryItemDto | null>(null);
  const [addingItem, setAddingItem] = useState(false);
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);
  const [newItemForm, setNewItemForm] = useState<CreateProcurementPlanItemDto>({
    itemDescription: '',
    specifications: '',
    itemCategory: '',
    estimatedQuantity: 1,
    unitOfMeasure: 'EA',
    estimatedUnitPrice: 0,
    priority: 'Medium',
    isCritical: false,
    requiredDate: '',
    plannedQuarter: '',
    justification: '',
    procurementMethod: 'DirectPurchase',
    notes: '',
    itemSuppliers: [],
  });

  // Supplier selection state
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [loadingSuppliers, setLoadingSuppliers] = useState(false);
  const [supplierSearchTerm, setSupplierSearchTerm] = useState('');
  const [selectedItemSuppliers, setSelectedItemSuppliers] = useState<CreateProcurementPlanItemSupplierDto[]>([]);

  // Approval dialog state
  const [approvalDialogOpen, setApprovalDialogOpen] = useState(false);
  const [approvalAction, setApprovalAction] = useState<'approve' | 'reject'>('approve');
  const [approvalComments, setApprovalComments] = useState('');
  const [approvedBudget, setApprovedBudget] = useState<number | undefined>(undefined);
  const [approvalLoading, setApprovalLoading] = useState(false);
  const [autoGenerateSchedules, setAutoGenerateSchedules] = useState(true);
  const [autoLinkBudget, setAutoLinkBudget] = useState(true);
  const [selectedBudgetId, setSelectedBudgetId] = useState<string | undefined>(undefined);
  const [availableBudgets, setAvailableBudgets] = useState<{ id: string; budgetCode: string; allocatedAmount: number; remainingAmount: number }[]>([]);
  const [loadingBudgets, setLoadingBudgets] = useState(false);

  // Conversion dialog state
  const [conversionDialogOpen, setConversionDialogOpen] = useState(false);
  const [conversionType, setConversionType] = useState<'tender' | 'rfq' | 'purchaseOrder'>('tender');
  const [selectedItemForConversion, setSelectedItemForConversion] = useState<ProcurementPlanItemDto | null>(null);
  const [conversionLoading, setConversionLoading] = useState(false);
  const [budgetValidation, setBudgetValidation] = useState<{
    isValid: boolean;
    hasBudget: boolean;
    remainingAmount: number;
    requestedAmount: number;
    message?: string;
    warnings: string[];
    controlLevel?: string;
  } | null>(null);
  const [validatingBudget, setValidatingBudget] = useState(false);
  const [tenderForm, setTenderForm] = useState({
    tenderTitle: '',
    tenderDescription: '',
    tenderType: 'ITB',
    submissionDeadline: '',
    openingDate: '',
    notes: '',
    createSchedule: true,
  });
  const [poForm, setPoForm] = useState({
    supplierId: '',
    requiredDate: '',
    paymentTerms: '',
    shippingTerms: '',
    deliveryAddress: '',
    deliveryInstructions: '',
    notes: '',
    createSchedule: true,
  });

  useEffect(() => {
    if (planId) {
      loadPlanDetails();
    }
  }, [planId]);

  const loadPlanDetails = async () => {
    try {
      setLoading(true);
      const data = await procurementPlanService.getPlanById(planId);
      setPlan(data);
    } catch (error) {
      console.error('Error loading plan details:', error);
      toast.error('Failed to load procurement plan details');
    } finally {
      setLoading(false);
    }
  };

  const loadInventoryItems = async () => {
    try {
      setLoadingInventory(true);
      const items = await commonService.getInventoryItems();
      setInventoryItems(items);
    } catch (error) {
      console.error('Error loading inventory items:', error);
      toast.error('Failed to load inventory items');
    } finally {
      setLoadingInventory(false);
    }
  };

  const loadSuppliers = async () => {
    try {
      setLoadingSuppliers(true);
      const response = await businessPartnerService.getPartners({
        partnerType: 'Supplier',
        approvalStatus: 'Approved',
        pageSize: 1000
      });
      setSuppliers(response.items || []);
    } catch (error) {
      console.error('Error loading suppliers:', error);
      toast.error('Failed to load suppliers');
    } finally {
      setLoadingSuppliers(false);
    }
  };

  const handleOpenAddItemDialog = () => {
    setAddItemDialogOpen(true);
    loadInventoryItems();
    loadSuppliers();
    setSelectedInventoryItem(null);
    setSelectedItemSuppliers([]);
    setSupplierSearchTerm('');
    setNewItemForm({
      itemDescription: '',
      specifications: '',
      itemCategory: '',
      estimatedQuantity: 1,
      unitOfMeasure: 'EA',
      estimatedUnitPrice: 0,
      priority: 'Medium',
      isCritical: false,
      requiredDate: '',
      plannedQuarter: '',
      justification: '',
      procurementMethod: 'DirectPurchase',
      notes: '',
      itemSuppliers: [],
    });
  };

  const handleAddSupplierToItem = (supplier: BusinessPartnerDto) => {
    if (selectedItemSuppliers.some(s => s.supplierId === supplier.id)) {
      toast.error('Supplier already added');
      return;
    }
    const newSupplier: CreateProcurementPlanItemSupplierDto = {
      supplierId: supplier.id,
      isPreferred: selectedItemSuppliers.length === 0,
      priority: selectedItemSuppliers.length + 1,
    };
    const updatedSuppliers = [...selectedItemSuppliers, newSupplier];
    setSelectedItemSuppliers(updatedSuppliers);
    setNewItemForm({ ...newItemForm, itemSuppliers: updatedSuppliers });
  };

  const handleRemoveSupplierFromItem = (supplierId: string) => {
    const updatedSuppliers = selectedItemSuppliers
      .filter(s => s.supplierId !== supplierId)
      .map((s, index) => ({ ...s, priority: index + 1 }));
    setSelectedItemSuppliers(updatedSuppliers);
    setNewItemForm({ ...newItemForm, itemSuppliers: updatedSuppliers });
  };

  const handleUpdateItemSupplier = (supplierId: string, field: keyof CreateProcurementPlanItemSupplierDto, value: unknown) => {
    const updatedSuppliers = selectedItemSuppliers.map(s => {
      if (s.supplierId === supplierId) {
        if (field === 'isPreferred' && value === true) {
          return { ...s, [field]: value };
        }
        return { ...s, [field]: value };
      }
      if (field === 'isPreferred' && value === true) {
        return { ...s, isPreferred: false };
      }
      return s;
    });
    setSelectedItemSuppliers(updatedSuppliers);
    setNewItemForm({ ...newItemForm, itemSuppliers: updatedSuppliers });
  };

  const filteredSuppliers = suppliers.filter(s =>
    s.partnerName.toLowerCase().includes(supplierSearchTerm.toLowerCase()) ||
    s.partnerCode.toLowerCase().includes(supplierSearchTerm.toLowerCase())
  );

  const handleSelectInventoryItem = (item: InventoryItemDto) => {
    setSelectedInventoryItem(item);
    setNewItemForm({
      ...newItemForm,
      inventoryItemId: item.id,
      itemDescription: item.name,
      specifications: item.description || '',
      itemCategory: item.categoryName || '',
      unitOfMeasure: item.unitOfMeasure || 'EA',
      estimatedUnitPrice: item.standardCost || item.averageCost || 0,
    });
  };

  const handleAddItem = async () => {
    if (!newItemForm.itemDescription.trim()) {
      toast.error('Item description is required');
      return;
    }
    if (newItemForm.estimatedQuantity <= 0) {
      toast.error('Quantity must be greater than 0');
      return;
    }

    try {
      setAddingItem(true);
      await procurementPlanService.addItem(planId, newItemForm);
      toast.success('Item added successfully');
      setAddItemDialogOpen(false);
      loadPlanDetails(); // Refresh plan data
    } catch (error) {
      console.error('Error adding item:', error);
      toast.error('Failed to add item');
    } finally {
      setAddingItem(false);
    }
  };

  const handleDeleteItem = async (itemId: string) => {
    if (!confirm('Are you sure you want to delete this item?')) return;

    try {
      setDeletingItemId(itemId);
      await procurementPlanService.removeItem(planId, itemId);
      toast.success('Item deleted successfully');
      loadPlanDetails(); // Refresh plan data
    } catch (error) {
      console.error('Error deleting item:', error);
      toast.error('Failed to delete item');
    } finally {
      setDeletingItemId(null);
    }
  };

  const filteredInventoryItems = inventoryItems.filter(item =>
    item.name?.toLowerCase().includes(inventorySearchTerm.toLowerCase()) ||
    item.itemCode?.toLowerCase().includes(inventorySearchTerm.toLowerCase()) ||
    item.categoryName?.toLowerCase().includes(inventorySearchTerm.toLowerCase())
  );

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; icon: React.ReactNode }> = {
      Draft: { variant: 'secondary', icon: <FileText className="h-3 w-3 mr-1" /> },
      Submitted: { variant: 'outline', icon: <Send className="h-3 w-3 mr-1" /> },
      UnderReview: { variant: 'default', icon: <Clock className="h-3 w-3 mr-1" /> },
      Approved: { variant: 'default', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
      Rejected: { variant: 'destructive', icon: <XCircle className="h-3 w-3 mr-1" /> },
      Active: { variant: 'default', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
      Completed: { variant: 'secondary', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
      Cancelled: { variant: 'destructive', icon: <XCircle className="h-3 w-3 mr-1" /> },
    };
    const config = statusConfig[status] || { variant: 'secondary' as const, icon: null };
    return (
      <Badge variant={config.variant} className="flex items-center w-fit">
        {config.icon}
        {status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const colors: Record<string, string> = {
      Low: 'bg-gray-100 text-gray-800',
      Medium: 'bg-blue-100 text-blue-800',
      High: 'bg-orange-100 text-orange-800',
      Critical: 'bg-red-100 text-red-800',
    };
    return <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>{priority}</Badge>;
  };

  const formatCurrency = (amount: number, currency: string = 'USD') => {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try {
      return format(new Date(dateString), 'MMM dd, yyyy');
    } catch {
      return dateString;
    }
  };

  const handleSubmitForApproval = async () => {
    if (!plan) return;
    try {
      setActionLoading(true);
      await procurementPlanService.submitForApproval(planId, { comments: '' });
      toast.success('Plan submitted for approval');
      loadPlanDetails();
    } catch (error) {
      console.error('Error submitting plan:', error);
      toast.error('Failed to submit plan for approval');
    } finally {
      setActionLoading(false);
    }
  };

  const handleOpenApprovalDialog = async (action: 'approve' | 'reject') => {
    setApprovalAction(action);
    setApprovalComments('');
    setApprovedBudget(plan?.totalEstimatedBudget);
    setAutoLinkBudget(true);
    setSelectedBudgetId(undefined);
    setApprovalDialogOpen(true);

    // Load available budgets for linking when approving
    if (action === 'approve' && plan) {
      try {
        setLoadingBudgets(true);
        const budgets = await procurementBudgetService.getAvailableBudgetsForLinking(plan.departmentId, plan.fiscalYear);
        setAvailableBudgets(budgets.map(b => ({
          id: b.id,
          budgetCode: b.budgetCode,
          allocatedAmount: b.allocatedAmount,
          remainingAmount: b.remainingAmount
        })));
      } catch (error) {
        console.error('Error loading available budgets:', error);
        setAvailableBudgets([]);
      } finally {
        setLoadingBudgets(false);
      }
    }
  };

  const handleApprovalSubmit = async () => {
    if (!plan) return;
    try {
      setApprovalLoading(true);
      const dto: ApproveProcurementPlanDto = {
        isApproved: approvalAction === 'approve',
        approvedBudget: approvalAction === 'approve' ? approvedBudget : undefined,
        comments: approvalComments || undefined,
        autoGenerateSchedules: approvalAction === 'approve' ? autoGenerateSchedules : undefined,
        autoLinkBudget: approvalAction === 'approve' ? autoLinkBudget : undefined,
        budgetId: approvalAction === 'approve' && !autoLinkBudget ? selectedBudgetId : undefined,
      };
      await procurementPlanService.approvePlan(planId, dto);
      toast.success(approvalAction === 'approve' ? 'Plan approved successfully' : 'Plan rejected');
      setApprovalDialogOpen(false);
      loadPlanDetails();
    } catch (error) {
      console.error('Error processing approval:', error);
      toast.error(`Failed to ${approvalAction} plan`);
    } finally {
      setApprovalLoading(false);
    }
  };

  // Conversion handlers
  const handleOpenConversionDialog = async (item: ProcurementPlanItemDto, type: 'tender' | 'rfq' | 'purchaseOrder') => {
    setSelectedItemForConversion(item);
    setConversionType(type);
    setBudgetValidation(null);

    // Validate budget before opening dialog
    try {
      setValidatingBudget(true);
      const validation = await procurementPlanService.validateBudgetForItem(item.id);
      setBudgetValidation(validation);
    } catch (error) {
      console.error('Error validating budget:', error);
      // Continue without budget validation
    } finally {
      setValidatingBudget(false);
    }

    if (type === 'tender') {
      setTenderForm({
        tenderTitle: `Tender for ${item.itemDescription}`,
        tenderDescription: item.specifications || '',
        tenderType: 'ITB',
        submissionDeadline: item.requiredDate ? format(new Date(item.requiredDate), 'yyyy-MM-dd') : '',
        openingDate: '',
        notes: '',
        createSchedule: true,
      });
    } else if (type === 'rfq') {
      setTenderForm({
        tenderTitle: `RFQ for ${item.itemDescription}`,
        tenderDescription: item.specifications || '',
        tenderType: 'RFQ',
        submissionDeadline: item.requiredDate ? format(new Date(item.requiredDate), 'yyyy-MM-dd') : '',
        openingDate: '',
        notes: '',
        createSchedule: true,
      });
    } else {
      setPoForm({
        supplierId: item.preferredSupplierId || '',
        requiredDate: item.requiredDate ? format(new Date(item.requiredDate), 'yyyy-MM-dd') : '',
        paymentTerms: '',
        shippingTerms: '',
        deliveryAddress: '',
        deliveryInstructions: '',
        notes: '',
        createSchedule: true,
      });
      loadSuppliers();
    }
    setConversionDialogOpen(true);
  };

  const handleConvertToTender = async () => {
    if (!selectedItemForConversion) return;

    // Check budget validation for strict control
    if (budgetValidation && !budgetValidation.isValid && budgetValidation.controlLevel === 'Strict') {
      toast.error(budgetValidation.message || 'Insufficient budget');
      return;
    }

    try {
      setConversionLoading(true);
      const result = await procurementPlanService.convertItemToTender({
        planItemId: selectedItemForConversion.id,
        tenderTitle: tenderForm.tenderTitle,
        tenderDescription: tenderForm.tenderDescription || undefined,
        tenderType: tenderForm.tenderType,
        submissionDeadline: tenderForm.submissionDeadline || undefined,
        openingDate: tenderForm.openingDate || undefined,
        notes: tenderForm.notes || undefined,
        createSchedule: tenderForm.createSchedule,
      });
      toast.success(result.message);
      setConversionDialogOpen(false);
      loadPlanDetails();
    } catch (error) {
      console.error('Error converting to tender:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to convert to tender');
    } finally {
      setConversionLoading(false);
    }
  };

  const handleConvertToRfq = async () => {
    if (!selectedItemForConversion) return;

    // Check budget validation for strict control
    if (budgetValidation && !budgetValidation.isValid && budgetValidation.controlLevel === 'Strict') {
      toast.error(budgetValidation.message || 'Insufficient budget');
      return;
    }

    try {
      setConversionLoading(true);
      const result = await procurementPlanService.convertItemToRfq({
        planItemId: selectedItemForConversion.id,
        tenderTitle: tenderForm.tenderTitle,
        tenderDescription: tenderForm.tenderDescription || undefined,
        tenderType: 'RFQ',
        submissionDeadline: tenderForm.submissionDeadline || undefined,
        openingDate: tenderForm.openingDate || undefined,
        notes: tenderForm.notes || undefined,
        createSchedule: tenderForm.createSchedule,
      });
      toast.success(result.message);
      setConversionDialogOpen(false);
      loadPlanDetails();
    } catch (error) {
      console.error('Error converting to RFQ:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to convert to RFQ');
    } finally {
      setConversionLoading(false);
    }
  };

  const handleConvertToPurchaseOrder = async () => {
    if (!selectedItemForConversion) return;
    if (!poForm.supplierId) {
      toast.error('Please select a supplier');
      return;
    }

    // Check budget validation for strict control
    if (budgetValidation && !budgetValidation.isValid && budgetValidation.controlLevel === 'Strict') {
      toast.error(budgetValidation.message || 'Insufficient budget');
      return;
    }

    try {
      setConversionLoading(true);
      const result = await procurementPlanService.convertItemToPurchaseOrder({
        planItemId: selectedItemForConversion.id,
        supplierId: poForm.supplierId,
        requiredDate: poForm.requiredDate || undefined,
        paymentTerms: poForm.paymentTerms || undefined,
        shippingTerms: poForm.shippingTerms || undefined,
        deliveryAddress: poForm.deliveryAddress || undefined,
        deliveryInstructions: poForm.deliveryInstructions || undefined,
        notes: poForm.notes || undefined,
        createSchedule: poForm.createSchedule,
      });
      toast.success(result.message);
      setConversionDialogOpen(false);
      loadPlanDetails();
    } catch (error) {
      console.error('Error converting to purchase order:', error);
      toast.error(error instanceof Error ? error.message : 'Failed to convert to purchase order');
    } finally {
      setConversionLoading(false);
    }
  };

  const canConvertItem = (item: ProcurementPlanItemDto) => {
    return plan?.status === 'Approved' &&
           (item.status === 'Approved' || item.status === 'Planned') &&
           !item.tenderId &&
           !item.purchaseOrderId;
  };

  const getItemStatusBadge = (status: string) => {
    switch (status) {
      case 'Planned':
        return <Badge variant="outline" className="bg-gray-100">Planned</Badge>;
      case 'Approved':
        return <Badge variant="outline" className="bg-blue-100 text-blue-800">Approved</Badge>;
      case 'InProgress':
        return <Badge variant="outline" className="bg-yellow-100 text-yellow-800">In Progress</Badge>;
      case 'Procured':
        return <Badge variant="outline" className="bg-green-100 text-green-800">Procured</Badge>;
      case 'Cancelled':
        return <Badge variant="outline" className="bg-red-100 text-red-800">Cancelled</Badge>;
      default:
        return <Badge variant="outline">{status}</Badge>;
    }
  };

  const getProcurementMethodBadge = (method?: string) => {
    switch (method) {
      case 'Tender':
        return <Badge variant="outline" className="bg-purple-100 text-purple-800">Tender</Badge>;
      case 'RFQ':
        return <Badge variant="outline" className="bg-indigo-100 text-indigo-800">RFQ</Badge>;
      case 'DirectPurchase':
        return <Badge variant="outline" className="bg-teal-100 text-teal-800">Direct Purchase</Badge>;
      case 'Contract':
        return <Badge variant="outline" className="bg-orange-100 text-orange-800">Contract</Badge>;
      default:
        return <Badge variant="outline">{method || 'N/A'}</Badge>;
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <Loader2 className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-lg text-gray-600">Loading procurement plan details...</p>
        </div>
      </div>
    );
  }

  if (!plan) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center">
          <AlertCircle className="h-12 w-12 mx-auto mb-4 text-red-500" />
          <p className="text-lg text-gray-600">Procurement plan not found</p>
          <Button onClick={() => router.push('/procurement/planning/plans')} className="mt-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Plans
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push('/procurement/planning/plans')}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{plan.title}</h1>
            <p className="text-gray-500">Fiscal Year: {plan.fiscalYear}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {getStatusBadge(plan.status)}
          {plan.status === 'Draft' && (
            <>
              <Button variant="outline" onClick={() => router.push(`/procurement/planning/plans/${planId}/edit`)}>
                <Edit className="h-4 w-4 mr-2" />
                Edit
              </Button>
              <Button onClick={handleSubmitForApproval} disabled={actionLoading}>
                {actionLoading ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Send className="h-4 w-4 mr-2" />}
                Submit for Approval
              </Button>
            </>
          )}
          {(plan.status === 'Submitted' || plan.status === 'UnderReview') && (
            <>
              <Button
                onClick={() => handleOpenApprovalDialog('approve')}
                disabled={approvalLoading}
                className="bg-green-600 hover:bg-green-700"
              >
                <ThumbsUp className="h-4 w-4 mr-2" />
                Approve
              </Button>
              <Button
                onClick={() => handleOpenApprovalDialog('reject')}
                disabled={approvalLoading}
                variant="destructive"
              >
                <ThumbsDown className="h-4 w-4 mr-2" />
                Reject
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Budget</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(plan.totalEstimatedBudget, plan.currency)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Plan Items</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{plan.itemCount || plan.items?.length || 0}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Duration</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{plan.planDurationYears} Year(s)</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Department</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-lg font-medium truncate">{plan.departmentName || '-'}</div>
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="details" className="w-full">
        <TabsList>
          <TabsTrigger value="details">
            <FileText className="h-4 w-4 mr-2" />
            Details
          </TabsTrigger>
          <TabsTrigger value="items">
            <Package className="h-4 w-4 mr-2" />
            Items ({plan.items?.length || 0})
          </TabsTrigger>
          <TabsTrigger value="schedule">
            <Calendar className="h-4 w-4 mr-2" />
            Schedule
          </TabsTrigger>
        </TabsList>

        <TabsContent value="details" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Plan Information</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm font-medium text-gray-500">Title</label>
                <p className="mt-1">{plan.title}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Department</label>
                <p className="mt-1">{plan.departmentName || '-'}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Fiscal Year</label>
                <p className="mt-1">{plan.fiscalYear}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Status</label>
                <div className="mt-1">{getStatusBadge(plan.status)}</div>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Start Date</label>
                <p className="mt-1">{formatDate(plan.planStartDate)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">End Date</label>
                <p className="mt-1">{formatDate(plan.planEndDate)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Total Estimated Budget</label>
                <p className="mt-1">{formatCurrency(plan.totalEstimatedBudget, plan.currency)}</p>
              </div>
              <div>
                <label className="text-sm font-medium text-gray-500">Currency</label>
                <p className="mt-1">{plan.currency}</p>
              </div>
              <div className="col-span-2">
                <label className="text-sm font-medium text-gray-500">Description</label>
                <p className="mt-1">{plan.description || '-'}</p>
              </div>
              {plan.notes && (
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">Notes</label>
                  <p className="mt-1">{plan.notes}</p>
                </div>
              )}
            </CardContent>
          </Card>

          {plan.reviewComments && (
            <Card>
              <CardHeader>
                <CardTitle>Review Information</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-4">
                <div>
                  <label className="text-sm font-medium text-gray-500">Reviewed By</label>
                  <p className="mt-1">{plan.reviewedByName || '-'}</p>
                </div>
                <div>
                  <label className="text-sm font-medium text-gray-500">Review Date</label>
                  <p className="mt-1">{formatDate(plan.reviewedDate)}</p>
                </div>
                <div className="col-span-2">
                  <label className="text-sm font-medium text-gray-500">Comments</label>
                  <p className="mt-1">{plan.reviewComments}</p>
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="items">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle>Plan Items</CardTitle>
                <CardDescription>Items included in this procurement plan</CardDescription>
              </div>
              {plan.status === 'Draft' && (
                <Button onClick={handleOpenAddItemDialog}>
                  <Plus className="h-4 w-4 mr-2" />
                  Add Item
                </Button>
              )}
            </CardHeader>
            <CardContent>
              {plan.items && plan.items.length > 0 ? (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Item Description</TableHead>
                      <TableHead>Quantity</TableHead>
                      <TableHead>Unit Cost</TableHead>
                      <TableHead>Total</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Required By</TableHead>
                      <TableHead className="w-[150px]">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {plan.items.map((item) => (
                      <TableRow key={item.id}>
                        <TableCell className="font-medium">
                          <div>
                            {item.itemDescription}
                            {item.tenderId && (
                              <div className="text-xs text-blue-600 mt-1">
                                → Tender created
                              </div>
                            )}
                            {item.purchaseOrderId && (
                              <div className="text-xs text-green-600 mt-1">
                                → PO created
                              </div>
                            )}
                          </div>
                        </TableCell>
                        <TableCell>{item.estimatedQuantity} {item.unitOfMeasure}</TableCell>
                        <TableCell>{formatCurrency(item.estimatedUnitPrice, plan.currency)}</TableCell>
                        <TableCell>{formatCurrency(item.estimatedTotalCost, plan.currency)}</TableCell>
                        <TableCell>{getItemStatusBadge(item.status)}</TableCell>
                        <TableCell>{getPriorityBadge(item.priority)}</TableCell>
                        <TableCell>{formatDate(item.requiredDate)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1">
                            {plan.status === 'Draft' && (
                              <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => handleDeleteItem(item.id)}
                                disabled={deletingItemId === item.id}
                                title="Delete item"
                              >
                                {deletingItemId === item.id ? (
                                  <Loader2 className="h-4 w-4 animate-spin" />
                                ) : (
                                  <Trash2 className="h-4 w-4 text-red-500" />
                                )}
                              </Button>
                            )}
                            {canConvertItem(item) && (
                              <>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  onClick={() => handleOpenConversionDialog(item, 'tender')}
                                  title="Create Tender"
                                >
                                  <FileCheck className="h-4 w-4 text-purple-600" />
                                </Button>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  onClick={() => handleOpenConversionDialog(item, 'rfq')}
                                  title="Create RFQ"
                                >
                                  <FileText className="h-4 w-4 text-blue-600" />
                                </Button>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  onClick={() => handleOpenConversionDialog(item, 'purchaseOrder')}
                                  title="Create Purchase Order"
                                >
                                  <ShoppingCart className="h-4 w-4 text-green-600" />
                                </Button>
                              </>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <div className="text-center py-8 text-gray-500">
                  <Package className="h-12 w-12 mx-auto mb-2 opacity-50" />
                  <p>No items added to this plan yet</p>
                  {plan.status === 'Draft' && (
                    <Button onClick={handleOpenAddItemDialog} variant="outline" className="mt-4">
                      <Plus className="h-4 w-4 mr-2" />
                      Add First Item
                    </Button>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="schedule">
          <Card>
            <CardHeader>
              <CardTitle>Procurement Schedule</CardTitle>
              <CardDescription>Timeline and milestones for this plan</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="flex items-center gap-4 p-4 border rounded-lg">
                  <Calendar className="h-8 w-8 text-blue-500" />
                  <div>
                    <p className="font-medium">Plan Period</p>
                    <p className="text-sm text-gray-500">
                      {formatDate(plan.planStartDate)} - {formatDate(plan.planEndDate)}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-4 p-4 border rounded-lg">
                  <Clock className="h-8 w-8 text-green-500" />
                  <div>
                    <p className="font-medium">Duration</p>
                    <p className="text-sm text-gray-500">{plan.planDurationYears} Year(s)</p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Add Item Dialog */}
      <Dialog open={addItemDialogOpen} onOpenChange={setAddItemDialogOpen}>
        <DialogContent className="max-w-6xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add Item to Procurement Plan</DialogTitle>
            <DialogDescription>
              Select an item from inventory or enter details manually
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-6">
            {/* Left: Inventory Selection */}
            <div className="space-y-4 border-r pr-6">
              <h4 className="font-medium">Select from Inventory</h4>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                <Input
                  placeholder="Search inventory items..."
                  value={inventorySearchTerm}
                  onChange={(e) => setInventorySearchTerm(e.target.value)}
                  className="pl-10"
                />
              </div>
              <div className="h-[300px] overflow-y-auto border rounded-lg">
                {loadingInventory ? (
                  <div className="flex items-center justify-center h-full">
                    <Loader2 className="h-6 w-6 animate-spin" />
                  </div>
                ) : filteredInventoryItems.length === 0 ? (
                  <div className="flex items-center justify-center h-full text-gray-500">
                    No items found
                  </div>
                ) : (
                  <div className="divide-y">
                    {filteredInventoryItems.map((item) => (
                      <div
                        key={item.id}
                        className={`p-3 cursor-pointer hover:bg-gray-50 ${
                          selectedInventoryItem?.id === item.id ? 'bg-blue-50 border-l-4 border-blue-500' : ''
                        }`}
                        onClick={() => handleSelectInventoryItem(item)}
                      >
                        <div className="font-medium">{item.name}</div>
                        <div className="text-sm text-gray-500">
                          {item.itemCode} | {item.categoryName || 'No Category'} | {item.unitOfMeasure}
                        </div>
                        <div className="text-sm text-gray-500">
                          Cost: {formatCurrency(item.standardCost || item.averageCost, plan?.currency || 'USD')} | Stock: {item.availableStock}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Right: Item Details Form */}
            <div className="space-y-4">
              <h4 className="font-medium">Item Details</h4>
              <div className="space-y-3">
                <div className="space-y-1">
                  <Label htmlFor="itemDescription">Item Description *</Label>
                  <Input
                    id="itemDescription"
                    value={newItemForm.itemDescription}
                    onChange={(e) => setNewItemForm({ ...newItemForm, itemDescription: e.target.value })}
                    placeholder="Enter item description"
                  />
                </div>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="unitOfMeasure">Unit of Measure</Label>
                    <Input
                      id="unitOfMeasure"
                      value={newItemForm.unitOfMeasure || 'EA'}
                      onChange={(e) => setNewItemForm({ ...newItemForm, unitOfMeasure: e.target.value })}
                      placeholder="EA"
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="estimatedQuantity">Quantity *</Label>
                    <Input
                      id="estimatedQuantity"
                      type="number"
                      min={1}
                      value={newItemForm.estimatedQuantity}
                      onChange={(e) => setNewItemForm({ ...newItemForm, estimatedQuantity: parseFloat(e.target.value) || 0 })}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="estimatedUnitPrice">Unit Cost</Label>
                    <Input
                      id="estimatedUnitPrice"
                      type="number"
                      min={0}
                      step={0.01}
                      value={newItemForm.estimatedUnitPrice || 0}
                      onChange={(e) => setNewItemForm({ ...newItemForm, estimatedUnitPrice: parseFloat(e.target.value) || 0 })}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="priority">Priority</Label>
                    <Select
                      value={newItemForm.priority || 'Medium'}
                      onValueChange={(value) => setNewItemForm({ ...newItemForm, priority: value })}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select priority" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Low">Low</SelectItem>
                        <SelectItem value="Medium">Medium</SelectItem>
                        <SelectItem value="High">High</SelectItem>
                        <SelectItem value="Critical">Critical</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="requiredDate">Required By</Label>
                    <Input
                      id="requiredDate"
                      type="date"
                      value={newItemForm.requiredDate || ''}
                      onChange={(e) => setNewItemForm({ ...newItemForm, requiredDate: e.target.value })}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1">
                    <Label htmlFor="specifications">Specifications</Label>
                    <Textarea
                      id="specifications"
                      value={newItemForm.specifications || ''}
                      onChange={(e) => setNewItemForm({ ...newItemForm, specifications: e.target.value })}
                      placeholder="Enter specifications"
                      rows={2}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="justification">Justification</Label>
                    <Textarea
                      id="justification"
                      value={newItemForm.justification || ''}
                      onChange={(e) => setNewItemForm({ ...newItemForm, justification: e.target.value })}
                      placeholder="Enter justification"
                      rows={2}
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Preferred Suppliers Section */}
          <div className="border-t pt-4 mt-4">
            <div className="flex items-center justify-between mb-3">
              <h4 className="font-medium flex items-center gap-2">
                <Users className="h-4 w-4" />
                Preferred Suppliers
              </h4>
            </div>

            {/* Supplier Search and Add */}
            <div className="grid grid-cols-2 gap-4 mb-4">
              <div className="space-y-2">
                <Label>Search & Add Supplier</Label>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                  <Input
                    placeholder="Search suppliers..."
                    value={supplierSearchTerm}
                    onChange={(e) => setSupplierSearchTerm(e.target.value)}
                    className="pl-10"
                  />
                </div>
                <div className="h-[150px] overflow-y-auto border rounded-lg">
                  {loadingSuppliers ? (
                    <div className="flex items-center justify-center h-full">
                      <Loader2 className="h-5 w-5 animate-spin" />
                    </div>
                  ) : filteredSuppliers.length === 0 ? (
                    <div className="flex items-center justify-center h-full text-gray-500 text-sm">
                      No suppliers found
                    </div>
                  ) : (
                    <div className="divide-y">
                      {filteredSuppliers.slice(0, 20).map((supplier) => (
                        <div
                          key={supplier.id}
                          className="p-2 cursor-pointer hover:bg-gray-50 flex items-center justify-between"
                          onClick={() => handleAddSupplierToItem(supplier)}
                        >
                          <div>
                            <div className="font-medium text-sm">{supplier.partnerName}</div>
                            <div className="text-xs text-gray-500">{supplier.partnerCode}</div>
                          </div>
                          <Plus className="h-4 w-4 text-green-500" />
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>

              {/* Selected Suppliers Grid */}
              <div className="space-y-2">
                <Label>Selected Suppliers ({selectedItemSuppliers.length})</Label>
                <div className="h-[180px] overflow-y-auto border rounded-lg">
                  {selectedItemSuppliers.length === 0 ? (
                    <div className="flex items-center justify-center h-full text-gray-500 text-sm">
                      No suppliers selected
                    </div>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="text-xs">Supplier</TableHead>
                          <TableHead className="text-xs w-[60px]">Preferred</TableHead>
                          <TableHead className="text-xs w-[80px]">Quote</TableHead>
                          <TableHead className="text-xs w-[40px]"></TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {selectedItemSuppliers.map((itemSupplier) => {
                          const supplier = suppliers.find(s => s.id === itemSupplier.supplierId);
                          return (
                            <TableRow key={itemSupplier.supplierId}>
                              <TableCell className="py-1">
                                <div className="text-xs font-medium">{supplier?.partnerName || 'Unknown'}</div>
                                <div className="text-xs text-gray-500">{supplier?.partnerCode}</div>
                              </TableCell>
                              <TableCell className="py-1">
                                <input
                                  type="checkbox"
                                  checked={itemSupplier.isPreferred || false}
                                  onChange={(e) => handleUpdateItemSupplier(itemSupplier.supplierId, 'isPreferred', e.target.checked)}
                                  className="h-4 w-4"
                                />
                              </TableCell>
                              <TableCell className="py-1">
                                <Input
                                  type="number"
                                  min={0}
                                  step={0.01}
                                  value={itemSupplier.quotedUnitPrice || ''}
                                  onChange={(e) => handleUpdateItemSupplier(itemSupplier.supplierId, 'quotedUnitPrice', parseFloat(e.target.value) || undefined)}
                                  className="h-7 text-xs w-[70px]"
                                  placeholder="0.00"
                                />
                              </TableCell>
                              <TableCell className="py-1">
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  className="h-6 w-6"
                                  onClick={() => handleRemoveSupplierFromItem(itemSupplier.supplierId)}
                                >
                                  <Trash2 className="h-3 w-3 text-red-500" />
                                </Button>
                              </TableCell>
                            </TableRow>
                          );
                        })}
                      </TableBody>
                    </Table>
                  )}
                </div>
              </div>
            </div>
          </div>

          <DialogFooter className="mt-4">
            <Button variant="outline" onClick={() => setAddItemDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleAddItem} disabled={addingItem}>
              {addingItem ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Adding...
                </>
              ) : (
                <>
                  <Plus className="h-4 w-4 mr-2" />
                  Add Item
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Approval/Rejection Dialog */}
      <Dialog open={approvalDialogOpen} onOpenChange={setApprovalDialogOpen}>
        <DialogContent className="sm:max-w-[500px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              {approvalAction === 'approve' ? (
                <>
                  <ThumbsUp className="h-5 w-5 text-green-600" />
                  Approve Procurement Plan
                </>
              ) : (
                <>
                  <ThumbsDown className="h-5 w-5 text-red-600" />
                  Reject Procurement Plan
                </>
              )}
            </DialogTitle>
            <DialogDescription>
              {approvalAction === 'approve'
                ? 'Review and approve this procurement plan. You can optionally adjust the approved budget.'
                : 'Provide a reason for rejecting this procurement plan.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            {/* Plan Summary */}
            <div className="bg-gray-50 p-4 rounded-lg space-y-2">
              <div className="flex justify-between">
                <span className="text-sm text-gray-500">Plan Number:</span>
                <span className="font-medium">{plan?.planNumber}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-500">Title:</span>
                <span className="font-medium">{plan?.title}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-gray-500">Estimated Budget:</span>
                <span className="font-medium">
                  {plan && formatCurrency(plan.totalEstimatedBudget, plan.currency)}
                </span>
              </div>
            </div>

            {/* Approved Budget (only for approval) */}
            {approvalAction === 'approve' && (
              <div className="space-y-2">
                <Label htmlFor="approvedBudget">Approved Budget</Label>
                <Input
                  id="approvedBudget"
                  type="number"
                  step="0.01"
                  min="0"
                  value={approvedBudget || ''}
                  onChange={(e) => setApprovedBudget(e.target.value ? parseFloat(e.target.value) : undefined)}
                  placeholder="Enter approved budget amount"
                />
                <p className="text-xs text-gray-500">
                  Leave as is to approve the estimated budget, or adjust as needed.
                </p>
              </div>
            )}

            {/* Auto-generate schedules (only for approval) */}
            {approvalAction === 'approve' && (
              <div className="flex items-center space-x-2">
                <Checkbox
                  id="autoGenerateSchedules"
                  checked={autoGenerateSchedules}
                  onCheckedChange={(checked) => setAutoGenerateSchedules(checked === true)}
                />
                <Label htmlFor="autoGenerateSchedules" className="text-sm font-normal cursor-pointer">
                  Auto-generate procurement schedules for each plan item
                </Label>
              </div>
            )}

            {/* Budget Linking (only for approval) */}
            {approvalAction === 'approve' && (
              <div className="space-y-3 border-t pt-4">
                <div className="flex items-center space-x-2">
                  <Checkbox
                    id="autoLinkBudget"
                    checked={autoLinkBudget}
                    onCheckedChange={(checked) => {
                      setAutoLinkBudget(checked === true);
                      if (checked) setSelectedBudgetId(undefined);
                    }}
                  />
                  <Label htmlFor="autoLinkBudget" className="text-sm font-normal cursor-pointer">
                    Auto-link to matching budget (by department & fiscal year)
                  </Label>
                </div>

                {!autoLinkBudget && (
                  <div className="space-y-2">
                    <Label htmlFor="budgetSelect">Select Budget to Link</Label>
                    {loadingBudgets ? (
                      <div className="flex items-center gap-2 text-sm text-gray-500">
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Loading available budgets...
                      </div>
                    ) : availableBudgets.length === 0 ? (
                      <p className="text-sm text-amber-600">
                        No available budgets found for this department and fiscal year.
                      </p>
                    ) : (
                      <Select value={selectedBudgetId} onValueChange={setSelectedBudgetId}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select a budget..." />
                        </SelectTrigger>
                        <SelectContent>
                          {availableBudgets.map((budget) => (
                            <SelectItem key={budget.id} value={budget.id}>
                              {budget.budgetCode} - Remaining: {formatCurrency(budget.remainingAmount)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  </div>
                )}
              </div>
            )}

            {/* Comments */}
            <div className="space-y-2">
              <Label htmlFor="approvalComments">
                {approvalAction === 'approve' ? 'Comments (Optional)' : 'Reason for Rejection'}
              </Label>
              <Textarea
                id="approvalComments"
                value={approvalComments}
                onChange={(e) => setApprovalComments(e.target.value)}
                placeholder={approvalAction === 'approve'
                  ? 'Add any comments about this approval...'
                  : 'Please provide a reason for rejecting this plan...'}
                rows={4}
              />
              {approvalAction === 'reject' && !approvalComments && (
                <p className="text-xs text-amber-600">
                  Please provide a reason for rejection.
                </p>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setApprovalDialogOpen(false)}
              disabled={approvalLoading}
            >
              Cancel
            </Button>
            <Button
              onClick={handleApprovalSubmit}
              disabled={approvalLoading || (approvalAction === 'reject' && !approvalComments)}
              className={approvalAction === 'approve' ? 'bg-green-600 hover:bg-green-700' : ''}
              variant={approvalAction === 'reject' ? 'destructive' : 'default'}
            >
              {approvalLoading ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Processing...
                </>
              ) : approvalAction === 'approve' ? (
                <>
                  <CheckCircle className="h-4 w-4 mr-2" />
                  Confirm Approval
                </>
              ) : (
                <>
                  <XCircle className="h-4 w-4 mr-2" />
                  Confirm Rejection
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Conversion Dialog */}
      <Dialog open={conversionDialogOpen} onOpenChange={setConversionDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {conversionType === 'tender' ? 'Create Tender from Plan Item' :
               conversionType === 'rfq' ? 'Create RFQ from Plan Item' :
               'Create Purchase Order from Plan Item'}
            </DialogTitle>
            <DialogDescription>
              {selectedItemForConversion && (
                <span>
                  Converting: <strong>{selectedItemForConversion.itemDescription}</strong>
                  {' '}({selectedItemForConversion.estimatedQuantity} {selectedItemForConversion.unitOfMeasure})
                </span>
              )}
            </DialogDescription>
          </DialogHeader>

          {/* Budget Validation Warning */}
          {budgetValidation && (
            <div className={`p-3 rounded-lg border ${
              !budgetValidation.isValid && budgetValidation.controlLevel === 'Strict'
                ? 'bg-red-50 border-red-200'
                : budgetValidation.warnings.length > 0
                  ? 'bg-yellow-50 border-yellow-200'
                  : 'bg-green-50 border-green-200'
            }`}>
              <div className="flex items-start gap-2">
                {!budgetValidation.isValid && budgetValidation.controlLevel === 'Strict' ? (
                  <XCircle className="h-5 w-5 text-red-500 mt-0.5" />
                ) : budgetValidation.warnings.length > 0 ? (
                  <AlertCircle className="h-5 w-5 text-yellow-500 mt-0.5" />
                ) : (
                  <CheckCircle className="h-5 w-5 text-green-500 mt-0.5" />
                )}
                <div className="flex-1">
                  <p className="text-sm font-medium">
                    {budgetValidation.hasBudget ? 'Budget Status' : 'No Budget Allocated'}
                  </p>
                  <p className="text-sm text-gray-600">{budgetValidation.message}</p>
                  {budgetValidation.warnings.map((warning, idx) => (
                    <p key={idx} className="text-sm text-yellow-700 mt-1">⚠️ {warning}</p>
                  ))}
                  {budgetValidation.hasBudget && (
                    <p className="text-xs text-gray-500 mt-1">
                      Requested: {formatCurrency(budgetValidation.requestedAmount, plan?.currency)} |
                      Remaining: {formatCurrency(budgetValidation.remainingAmount, plan?.currency)}
                    </p>
                  )}
                </div>
              </div>
            </div>
          )}
          {validatingBudget && (
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" />
              Validating budget...
            </div>
          )}

          {(conversionType === 'tender' || conversionType === 'rfq') ? (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="col-span-2">
                  <Label htmlFor="tenderTitle">Tender Title *</Label>
                  <Input
                    id="tenderTitle"
                    value={tenderForm.tenderTitle}
                    onChange={(e) => setTenderForm({ ...tenderForm, tenderTitle: e.target.value })}
                    placeholder="Enter tender title"
                  />
                </div>
                <div className="col-span-2">
                  <Label htmlFor="tenderDescription">Description</Label>
                  <Textarea
                    id="tenderDescription"
                    value={tenderForm.tenderDescription}
                    onChange={(e) => setTenderForm({ ...tenderForm, tenderDescription: e.target.value })}
                    placeholder="Enter tender description"
                    rows={3}
                  />
                </div>
                {conversionType === 'tender' && (
                  <div>
                    <Label htmlFor="tenderType">Tender Type</Label>
                    <Select
                      value={tenderForm.tenderType}
                      onValueChange={(value) => setTenderForm({ ...tenderForm, tenderType: value })}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select type" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="ITB">Invitation to Bid (ITB)</SelectItem>
                        <SelectItem value="RFP">Request for Proposal (RFP)</SelectItem>
                        <SelectItem value="OpenTender">Open Tender</SelectItem>
                        <SelectItem value="RestrictedTender">Restricted Tender</SelectItem>
                        <SelectItem value="SingleSource">Single Source</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                )}
                {conversionType === 'rfq' && (
                  <div>
                    <Label>Type</Label>
                    <div className="mt-1 p-2 bg-blue-50 rounded border border-blue-200 text-blue-700 text-sm">
                      Request for Quotation (RFQ)
                    </div>
                  </div>
                )}
                <div>
                  <Label htmlFor="submissionDeadline">Submission Deadline</Label>
                  <Input
                    id="submissionDeadline"
                    type="date"
                    value={tenderForm.submissionDeadline}
                    onChange={(e) => setTenderForm({ ...tenderForm, submissionDeadline: e.target.value })}
                  />
                </div>
                <div>
                  <Label htmlFor="openingDate">Opening Date</Label>
                  <Input
                    id="openingDate"
                    type="date"
                    value={tenderForm.openingDate}
                    onChange={(e) => setTenderForm({ ...tenderForm, openingDate: e.target.value })}
                  />
                </div>
                <div className="col-span-2">
                  <Label htmlFor="tenderNotes">Notes</Label>
                  <Textarea
                    id="tenderNotes"
                    value={tenderForm.notes}
                    onChange={(e) => setTenderForm({ ...tenderForm, notes: e.target.value })}
                    placeholder="Additional notes..."
                    rows={2}
                  />
                </div>
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="col-span-2">
                  <Label htmlFor="supplierId">Supplier *</Label>
                  <Select
                    value={poForm.supplierId}
                    onValueChange={(value) => setPoForm({ ...poForm, supplierId: value })}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select supplier" />
                    </SelectTrigger>
                    <SelectContent>
                      {loadingSuppliers ? (
                        <div className="p-2 text-center text-gray-500">Loading suppliers...</div>
                      ) : (
                        suppliers.map((supplier) => (
                          <SelectItem key={supplier.id} value={supplier.id}>
                            {supplier.partnerName} ({supplier.partnerCode})
                          </SelectItem>
                        ))
                      )}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="requiredDate">Required Date</Label>
                  <Input
                    id="requiredDate"
                    type="date"
                    value={poForm.requiredDate}
                    onChange={(e) => setPoForm({ ...poForm, requiredDate: e.target.value })}
                  />
                </div>
                <div>
                  <Label htmlFor="paymentTerms">Payment Terms</Label>
                  <Input
                    id="paymentTerms"
                    value={poForm.paymentTerms}
                    onChange={(e) => setPoForm({ ...poForm, paymentTerms: e.target.value })}
                    placeholder="e.g., Net 30"
                  />
                </div>
                <div>
                  <Label htmlFor="shippingTerms">Shipping Terms</Label>
                  <Input
                    id="shippingTerms"
                    value={poForm.shippingTerms}
                    onChange={(e) => setPoForm({ ...poForm, shippingTerms: e.target.value })}
                    placeholder="e.g., FOB Destination"
                  />
                </div>
                <div className="col-span-2">
                  <Label htmlFor="deliveryAddress">Delivery Address</Label>
                  <Input
                    id="deliveryAddress"
                    value={poForm.deliveryAddress}
                    onChange={(e) => setPoForm({ ...poForm, deliveryAddress: e.target.value })}
                    placeholder="Enter delivery address"
                  />
                </div>
                <div className="col-span-2">
                  <Label htmlFor="deliveryInstructions">Delivery Instructions</Label>
                  <Textarea
                    id="deliveryInstructions"
                    value={poForm.deliveryInstructions}
                    onChange={(e) => setPoForm({ ...poForm, deliveryInstructions: e.target.value })}
                    placeholder="Special delivery instructions..."
                    rows={2}
                  />
                </div>
                <div className="col-span-2">
                  <Label htmlFor="poNotes">Notes</Label>
                  <Textarea
                    id="poNotes"
                    value={poForm.notes}
                    onChange={(e) => setPoForm({ ...poForm, notes: e.target.value })}
                    placeholder="Additional notes..."
                    rows={2}
                  />
                </div>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setConversionDialogOpen(false)}
              disabled={conversionLoading}
            >
              Cancel
            </Button>
            <Button
              onClick={
                conversionType === 'tender' ? handleConvertToTender :
                conversionType === 'rfq' ? handleConvertToRfq :
                handleConvertToPurchaseOrder
              }
              disabled={
                conversionLoading ||
                (conversionType === 'purchaseOrder' && !poForm.supplierId) ||
                Boolean(budgetValidation && !budgetValidation.isValid && budgetValidation.controlLevel === 'Strict')
              }
              className={
                conversionType === 'tender' ? 'bg-purple-600 hover:bg-purple-700' :
                conversionType === 'rfq' ? 'bg-blue-600 hover:bg-blue-700' :
                'bg-green-600 hover:bg-green-700'
              }
            >
              {conversionLoading ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Creating...
                </>
              ) : conversionType === 'tender' ? (
                <>
                  <FileCheck className="h-4 w-4 mr-2" />
                  Create Tender
                </>
              ) : conversionType === 'rfq' ? (
                <>
                  <FileText className="h-4 w-4 mr-2" />
                  Create RFQ
                </>
              ) : (
                <>
                  <ShoppingCart className="h-4 w-4 mr-2" />
                  Create Purchase Order
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
