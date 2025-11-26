'use client';

import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, CheckCircle, Clock, ChevronDown, User, ClipboardList, History, FileText, Wrench, Package, Trash2, Pencil, FlaskConical, CalendarClock, DollarSign, HelpCircle, ExternalLink, ArrowUp, ArrowDown, ArrowUpDown, CalendarIcon, ShieldCheck } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Calendar as CalendarComponent } from '@/components/ui/calendar';
import { DateRange } from '@/components/ui/calendar';
import { format } from 'date-fns';
import { useToast } from '@/hooks/use-toast';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import maintenanceApiService, { Employee, Asset, WorkOrderType, PriorityLevel, MaintenanceStaffSchedule, MaintenanceExpense } from '@/services/maintenanceApiService';
import { maintenanceDataService } from '@/services/maintenanceDataService';
import qualityControlService, { QualityValidationResult } from '@/services/qualityControlService';
import workOrderToolService, { WorkOrderToolDto, WorkOrderToolSummaryDto, AllocateWorkOrderToolDto, CheckoutWorkOrderToolDto, ReturnWorkOrderToolDto } from '@/services/workOrderToolService';
import { toolCheckoutService, MaintenanceToolDto } from '@/services/toolCheckoutService';
import workOrderPartService, { WorkOrderPartDto, CreateWorkOrderPartDto, InventoryItemDto, WarehouseLocationDto, WarehouseDto, WarehouseInventoryDto } from '@/services/workOrderPartService';
import { fileUploadService } from '@/services/fileUploadService';
import { ClientOnly } from '@/components/ClientOnly';

import assetAdmissionService from '@/services/assetAdmissionService';

interface WorkOrderTask {
  id: string;
  taskName: string;
  description?: string;
  status: string;
  assignedTechnicianId?: string;
  assignedTechnician?: {
    fullName: string;
  };
  estimatedHours: number;
  actualHours: number;
  completedAt?: string;
  isRequired: boolean;
}

interface WorkOrder {
  id: string;
  workOrderNumber: string;
  title: string;
  description?: string;
  assetName: string;
  assignedTechnicianName: string;
  assignedTechnicianId?: string;
  status: 'Draft' | 'Open' | 'Approved' | 'InProgress' | 'OnHold' | 'Completed' | 'Cancelled';
  priority: string;
  type: string; // Work order type name
  maintenanceLocation?: string; // Internal, External, Onsite, Offsite
  createdAt: string;
  requestedCompletionDate?: string;
  actualCompletionDate?: string;
  actualStartDate?: string;
  estimatedHours?: number;
  actualHours?: number;
  estimatedCost?: number;
  jobCardId?: string;
  jobCardNumber?: string;
  tasks?: WorkOrderTask[];
  // Additional backend properties
  assetId?: string;
  workOrderTypeId?: string;
  priorityLevelId?: string;
  maintenanceTypeId?: string;
}

interface JobCard {
  id: string;
  jobCardNumber: string;
  assetName: string;
  description: string;
  status: string;
}


export default function WorkOrdersPage() {
  const { toast } = useToast();
  const searchParams = useSearchParams();
  const [workOrders, setWorkOrders] = useState<WorkOrder[]>([]);
  const [filteredOrders, setFilteredOrders] = useState<WorkOrder[]>([]);
  const [workOrderSchedulesMap, setWorkOrderSchedulesMap] = useState<{[key: string]: MaintenanceStaffSchedule[]}>({});
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [priorityFilter, setPriorityFilter] = useState<string>('all');
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [sortColumn, setSortColumn] = useState<string>('createdAt');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState<WorkOrder | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [selectedOrderTasks, setSelectedOrderTasks] = useState<WorkOrderTask[]>([]);
  const [loadingTasks, setLoadingTasks] = useState(false);
  const [qualityValidation, setQualityValidation] = useState<{[key: string]: QualityValidationResult}>({});

  // Task completion dialog state
  const [isTaskCompletionDialogOpen, setIsTaskCompletionDialogOpen] = useState(false);
  const [selectedTask, setSelectedTask] = useState<WorkOrderTask | null>(null);
  const [taskActualHours, setTaskActualHours] = useState<number>(0);
  const [taskCompletionNotes, setTaskCompletionNotes] = useState<string>('');

  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [workOrderTypes, setWorkOrderTypes] = useState<WorkOrderType[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<any[]>([]);
  const [priorities, setPriorities] = useState<PriorityLevel[]>([]);
  const [priorityLevels, setPriorityLevels] = useState<PriorityLevel[]>([]);
  const [jobCards, setJobCards] = useState<JobCard[]>([]);
  const [loadingData, setLoadingData] = useState(true);

  // Tool management state
  const [availableTools, setAvailableTools] = useState<MaintenanceToolDto[]>([]);
  const [selectedTools, setSelectedTools] = useState<string[]>([]);
  const [workOrderTools, setWorkOrderTools] = useState<WorkOrderToolDto[]>([]);
  const [toolSummary, setToolSummary] = useState<WorkOrderToolSummaryDto | null>(null);
  const [loadingTools, setLoadingTools] = useState(false);
  const [loadingParts, setLoadingParts] = useState(false);
  const [workOrderLabor, setWorkOrderLabor] = useState<any[]>([]);
  const [toolCheckoutDialogOpen, setToolCheckoutDialogOpen] = useState(false);
  const [toolReturnDialogOpen, setToolReturnDialogOpen] = useState(false);
  const [isToolAllocationDialogOpen, setIsToolAllocationDialogOpen] = useState(false);
  const [selectedWorkOrderTool, setSelectedWorkOrderTool] = useState<WorkOrderToolDto | null>(null);

  // Cost summary cache for work orders list (to avoid recalculating on every render)
  const [workOrderCosts, setWorkOrderCosts] = useState<Record<string, number>>({});

  // Consumables state
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [workOrderParts, setWorkOrderParts] = useState<WorkOrderPartDto[]>([]);
  const [selectedInventoryItem, setSelectedInventoryItem] = useState<InventoryItemDto | null>(null);
  const [partQuantity, setPartQuantity] = useState<number>(1);
  const [partNotes, setPartNotes] = useState<string>('');
  const [editingPart, setEditingPart] = useState<WorkOrderPartDto | null>(null);
  const [warehouseLocations, setWarehouseLocations] = useState<WarehouseLocationDto[]>([]);
  const [selectedWarehouseLocationId, setSelectedWarehouseLocationId] = useState<string>('');
  const [pendingPartDeletes, setPendingPartDeletes] = useState<string[]>([]);

  // Warehouse-based inventory state
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [selectedWarehouse, setSelectedWarehouse] = useState<string>('');
  const [warehouseInventory, setWarehouseInventory] = useState<WarehouseInventoryDto[]>([]);
  const [warehouseTools, setWarehouseTools] = useState<WarehouseInventoryDto[]>([]);

  // Tool search state
  const [toolSearchTerm, setToolSearchTerm] = useState<string>('');

  // Consumable search state
  const [consumableSearchTerm, setConsumableSearchTerm] = useState<string>('');

  // Consume part dialog state
  const [isConsumePartDialogOpen, setIsConsumePartDialogOpen] = useState(false);
  const [partToConsume, setPartToConsume] = useState<WorkOrderPartDto | null>(null);
  const [quantityToConsume, setQuantityToConsume] = useState<number>(0);

  // Staff Schedule state
  const [staffSchedules, setStaffSchedules] = useState<MaintenanceStaffSchedule[]>([]);
  const [loadingSchedules, setLoadingSchedules] = useState(false);
  const [isScheduleDialogOpen, setIsScheduleDialogOpen] = useState(false);
  const [editingSchedule, setEditingSchedule] = useState<MaintenanceStaffSchedule | null>(null);
  const [pendingScheduleDeletes, setPendingScheduleDeletes] = useState<string[]>([]);
  const [scheduleForm, setScheduleForm] = useState({
    technicianId: '',
    startDateTime: '',
    endDateTime: '',
    scheduleType: 'Scheduled',
    workLocation: '',
    address: '',
    requiresTravel: false,
    transportationType: '',
    assignedVehicleId: '',
    notes: '',
  });

  // Expense state
  const [expenses, setExpenses] = useState<MaintenanceExpense[]>([]);
  const [loadingExpenses, setLoadingExpenses] = useState(false);
  const [isExpenseDialogOpen, setIsExpenseDialogOpen] = useState(false);
  const [editingExpense, setEditingExpense] = useState<MaintenanceExpense | null>(null);
  const [totalExpenses, setTotalExpenses] = useState<number>(0);
  const [availableVehicles, setAvailableVehicles] = useState<Asset[]>([]);
  const [expenseReceiptFile, setExpenseReceiptFile] = useState<File | null>(null);
  const [pendingExpenseDeletes, setPendingExpenseDeletes] = useState<string[]>([]);

  // Start work order confirmation dialog state
  const [isStartWorkOrderDialogOpen, setIsStartWorkOrderDialogOpen] = useState(false);
  const [workOrderToStart, setWorkOrderToStart] = useState<WorkOrder | null>(null);

  // Submit for QC confirmation dialog state
  const [isSubmitQCDialogOpen, setIsSubmitQCDialogOpen] = useState(false);
  const [workOrderToSubmitQC, setWorkOrderToSubmitQC] = useState<WorkOrder | null>(null);
  const [expenseForm, setExpenseForm] = useState({
    expenseType: 'Travel',
    description: '',
    amount: 0,
    expenseDate: new Date().toISOString().split('T')[0],
    mileageDriven: 0,
    mileageRate: 0,
    vehicleUsed: '',
    vendor: '',
    receiptNumber: '',
    receiptPath: '',
    notes: '',
  });

  const [newWorkOrder, setNewWorkOrder] = useState({
    title: '',
    description: '',
    assetName: '',
    assignedTechnician: '',
    priority: 'Medium' as const,
    maintenanceLocation: 'Internal',
    dueDate: '',
    workOrderType: 'Preventive',
    estimatedHours: 0,
    jobCardId: '',
    jobCardNumber: '',
    requiredTools: [] as string[],
  });

  // Deep-link handling state
  const [deepLinkHandled, setDeepLinkHandled] = useState(false);
  const [activeTab, setActiveTab] = useState<string>('details');

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      try {
        const [techniciansList, assetsResponse, workOrderTypesList, maintenanceTypesList, priorityLevelsList, toolsList] = await Promise.all([
          maintenanceDataService.getTechnicians(),
          maintenanceApiService.getAssets(),
          maintenanceApiService.getWorkOrderTypes(),
          maintenanceApiService.getMaintenanceTypes(),
          maintenanceApiService.getPriorityLevels(),
          toolCheckoutService.getAllTools()
        ]);

        setTechnicians(Array.isArray(techniciansList) ? techniciansList : []);
        setAssets(assetsResponse.items || []);
        setWorkOrderTypes(Array.isArray(workOrderTypesList) ? workOrderTypesList : []);
        setMaintenanceTypes(Array.isArray(maintenanceTypesList) ? maintenanceTypesList : []);
        setPriorities(Array.isArray(priorityLevelsList) ? priorityLevelsList : []);
        setPriorityLevels(Array.isArray(priorityLevelsList) ? priorityLevelsList : []);
        setAvailableTools(Array.isArray(toolsList) ? toolsList : []);

        // Load inventory items for consumables
        try {
          const items = await workOrderPartService.getInventoryItems();
          setInventoryItems(Array.isArray(items) ? items : []);
        } catch (error) {
          console.error('Error loading inventory items:', error);
          setInventoryItems([]);
        }

        // Load warehouse locations
        try {
          const locations = await workOrderPartService.getWarehouseLocations();
          setWarehouseLocations(Array.isArray(locations) ? locations : []);
        } catch (error) {
          console.error('Error loading warehouse locations:', error);
          setWarehouseLocations([]);
        }

        // Load warehouses for warehouse-based inventory
        try {
          const warehousesList = await workOrderPartService.getWarehouses();
          setWarehouses(Array.isArray(warehousesList) ? warehousesList : []);
          // Auto-select first warehouse if available
          if (warehousesList && warehousesList.length > 0) {
            setSelectedWarehouse(warehousesList[0].id);
          }
        } catch (error) {
          console.error('Error loading warehouses:', error);
          setWarehouses([]);
        }

        // Get approved job cards from API (with error handling)
        try {
          const jobCardsList = await maintenanceApiService.getApprovedJobCards();
          setJobCards(Array.isArray(jobCardsList) ? jobCardsList : []);
        } catch (error) {
          console.error('Error loading job cards:', error);
          setJobCards([]); // Set empty array on error
        }

        // Load available vehicles for expense tracking
        try {
          const vehiclesList = await maintenanceApiService.getAvailableVehicles();
          setAvailableVehicles(Array.isArray(vehiclesList) ? vehiclesList : []);
        } catch (error) {
          console.error('Error loading available vehicles:', error);
          setAvailableVehicles([]);
        }

        // Load work orders from API
        const workOrdersResponse = await maintenanceApiService.getWorkOrders();
        setWorkOrders(workOrdersResponse.items);
        setFilteredOrders(workOrdersResponse.items);

        // Load schedules for all work orders to get technician assignments
        const schedulesMap: {[key: string]: MaintenanceStaffSchedule[]} = {};
        for (const order of workOrdersResponse.items) {
          try {
            const schedules = await maintenanceApiService.getStaffSchedulesByWorkOrder(order.id);
            // Enrich schedules with full technician names from technicians list
            const enrichedSchedules = schedules.map(schedule => {
              const technician = techniciansList.find(t => t.id === schedule.technicianId);
              if (technician && !schedule.technicianFullName) {
                return {
                  ...schedule,
                  technicianFullName: `${technician.firstName} ${technician.lastName}`
                };
              }
              return schedule;
            });
            schedulesMap[order.id] = enrichedSchedules;
          } catch (error) {
            console.error(`Error loading schedules for work order ${order.id}:`, error);
            schedulesMap[order.id] = [];
          }
        }
        setWorkOrderSchedulesMap(schedulesMap);
      } catch (error) {
        console.error('Error loading data:', error);
      } finally {
        setLoadingData(false);
      }
    };

    loadData();
  }, []);

  // Handle URL parameters to auto-open a work order (by id or workOrderNumber)
  useEffect(() => {
    const urlWorkOrderId = searchParams.get('id');
    const urlWorkOrderNumber = searchParams.get('workOrderNumber');

    if (deepLinkHandled || (!urlWorkOrderId && !urlWorkOrderNumber) || workOrders.length === 0) {
      return;
    }

    let workOrder: WorkOrder | undefined;

    if (urlWorkOrderId) {
      workOrder = workOrders.find((wo) => wo.id === urlWorkOrderId);
    }

    if (!workOrder && urlWorkOrderNumber) {
      workOrder = workOrders.find((wo) => wo.workOrderNumber === urlWorkOrderNumber);
    }

    if (!workOrder) {
      setDeepLinkHandled(true);
      return;
    }

    // If we navigated via workOrderNumber, also filter the list and default to Tools tab
    if (urlWorkOrderNumber) {
      setSearchTerm(urlWorkOrderNumber);
      setActiveTab('tools');
    }

    setSelectedOrder(workOrder);
    setIsViewDialogOpen(true);
    setDeepLinkHandled(true);

    const fetchDetails = async () => {
      setLoadingTasks(true);
      setLoadingParts(true);
      setLoadingTools(true);
      setSelectedOrderTasks([]);
      setWorkOrderParts([]);
      setWorkOrderTools([]);
      setWorkOrderLabor([]);
      setStaffSchedules([]);
      setExpenses([]);

      try {
        const workOrderDetails = await maintenanceApiService.getWorkOrderById(workOrder!.id);

        if (workOrderDetails.labor && Array.isArray(workOrderDetails.labor)) {
          setWorkOrderLabor(workOrderDetails.labor);
        }

        if (workOrderDetails.tasks && Array.isArray(workOrderDetails.tasks)) {
          setSelectedOrderTasks(workOrderDetails.tasks);
        }

        if (workOrder.status === 'Completed' || workOrder.status === 'Cancelled') {
          if (workOrderDetails.parts && Array.isArray(workOrderDetails.parts)) {
            setWorkOrderParts(workOrderDetails.parts);
          }
          if (workOrderDetails.tools && Array.isArray(workOrderDetails.tools)) {
            setWorkOrderTools(workOrderDetails.tools);
          }
        } else {
          try {
            const parts = await workOrderPartService.getWorkOrderParts(workOrder.id);
            setWorkOrderParts(parts || []);
          } catch (error) {
            console.error('Error loading parts:', error);
          }

          try {
            const tools = await workOrderToolService.getWorkOrderTools(workOrder.id);
            setWorkOrderTools(tools || []);
          } catch (error) {
            console.error('Error loading tools:', error);
          }
        }

        try {
          const schedules = await maintenanceApiService.getStaffSchedulesByWorkOrder(workOrder.id);
          const enrichedSchedules = enrichSchedulesWithFullNames(schedules || []);
          setStaffSchedules(enrichedSchedules);
        } catch (error) {
          console.error('Error loading schedules:', error);
        }

        try {
          const expensesData = await maintenanceApiService.getExpensesByWorkOrder(workOrder.id);
          setExpenses(expensesData || []);
        } catch (error) {
          console.error('Error loading expenses:', error);
        }

        try {
          const validation = await qualityControlService.getValidationByWorkOrder(workOrder.id);
          if (validation) {
            setQualityValidation({ [workOrder.id]: validation });
          }
        } catch (error) {
          console.error('Error loading quality validation:', error);
        }
      } catch (error) {
        console.error('Error fetching work order details:', error);
      } finally {
        setLoadingTasks(false);
        setLoadingParts(false);
        setLoadingTools(false);
      }
    };

    fetchDetails();
  }, [searchParams, workOrders, deepLinkHandled]);

  useEffect(() => {
    let filtered = workOrders;

    if (searchTerm) {
      filtered = filtered.filter(order =>
        order.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        (order.assetName && order.assetName.toLowerCase().includes(searchTerm.toLowerCase())) ||
        order.workOrderNumber.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter(order => order.status === statusFilter);
    }

    if (priorityFilter && priorityFilter !== 'all') {
      filtered = filtered.filter(order => order.priority === priorityFilter);
    }

    if (dateRange?.from) {
      filtered = filtered.filter(order => new Date(order.createdAt) >= dateRange.from!);
    }

    if (dateRange?.to) {
      const end = new Date(dateRange.to);
      end.setHours(23, 59, 59, 999);
      filtered = filtered.filter(order => new Date(order.createdAt) <= end);
    }

    setFilteredOrders(filtered);
  }, [workOrders, searchTerm, statusFilter, priorityFilter, dateRange]);

  // Helper function to enrich schedules with full technician names
  const enrichSchedulesWithFullNames = (schedules: MaintenanceStaffSchedule[]): MaintenanceStaffSchedule[] => {
    return schedules.map(schedule => {
      const technician = technicians.find(t => t.id === schedule.technicianId);
      if (technician && !schedule.technicianFullName) {
        return {
          ...schedule,
          technicianFullName: `${technician.firstName} ${technician.lastName}`
        };
      }
      return schedule;
    });
  };

  // Helper function to get technician names from schedules
  const getTechnicianNamesFromSchedules = (workOrderId: string): string => {
    const schedules = workOrderSchedulesMap[workOrderId] || [];
    if (schedules.length === 0) {
      return 'No technicians assigned';
    }

    // Enrich schedules with full names
    const enrichedSchedules = enrichSchedulesWithFullNames(schedules);

    // Get unique technician names from schedules (prefer full name if available)
    const technicianNames = enrichedSchedules
      .map(s => s.technicianFullName || s.technicianName)
      .filter((name, index, self) => name && self.indexOf(name) === index);

    if (technicianNames.length === 0) {
      return 'No technicians assigned';
    }

    // Show first technician and count if more than one
    if (technicianNames.length === 1) {
      return technicianNames[0];
    }

    return `${technicianNames[0]} +${technicianNames.length - 1} more`;
  };

  // Load warehouse inventory when warehouse changes (consumables: itemType=1, tools: itemType=4)
  useEffect(() => {
    if (!selectedWarehouse) return;

    const loadWarehouseInventory = async () => {
      try {
        // Load consumables (ItemType.Consumable = 1)
        const inventory = await workOrderPartService.getInventoryByWarehouse(selectedWarehouse, 1);
        setWarehouseInventory(Array.isArray(inventory) ? inventory : []);

        // Load tools (ItemType.Tool = 4)
        const tools = await workOrderPartService.getInventoryByWarehouse(selectedWarehouse, 4);
        setWarehouseTools(Array.isArray(tools) ? tools : []);
      } catch (error) {
        console.error('Error loading warehouse inventory:', error);
        setWarehouseInventory([]);
        setWarehouseTools([]);
      }
    };

    loadWarehouseInventory();
  }, [selectedWarehouse]);

  const handleCreateWorkOrder = async () => {
    try {
      // Find selected asset and other data
      const selectedAsset = assets.find(a => a.name === newWorkOrder.assetName);
      const selectedWorkOrderType = (Array.isArray(workOrderTypes) ? workOrderTypes : []).find(wot => wot.name === newWorkOrder.workOrderType);
      const selectedPriority = (Array.isArray(priorityLevels) ? priorityLevels : []).find(pl => pl.name === newWorkOrder.priority);
      const selectedTechnician = technicians.find(t => `${t.firstName} ${t.lastName}` === newWorkOrder.assignedTechnician);

      if (!selectedAsset || !selectedWorkOrderType || !selectedPriority) {
        console.error('Missing required selections');
        return;
      }

      const createData = {
        title: newWorkOrder.title,
        description: newWorkOrder.description,
        assetId: selectedAsset.id,
        workOrderTypeId: selectedWorkOrderType.id,
        priorityLevelId: selectedPriority.id,
        assignedTechnicianId: selectedTechnician?.id,
        maintenanceLocation: newWorkOrder.maintenanceLocation || 'Internal',
        requestedCompletionDate: newWorkOrder.dueDate,
        estimatedHours: newWorkOrder.estimatedHours,
        jobCardId: newWorkOrder.jobCardId !== 'none' ? newWorkOrder.jobCardId : undefined,
      };

      const createdWorkOrder = await maintenanceApiService.createWorkOrder(createData);

      // Allocate selected tools to work order
      if (newWorkOrder.requiredTools.length > 0) {
        for (const toolId of newWorkOrder.requiredTools) {
          try {
            await workOrderToolService.allocateTool({
              workOrderId: createdWorkOrder.id,
              toolId,
              isRequired: true,
              notes: 'Allocated during work order creation'
            });
          } catch (error) {
            console.error('Error allocating tool:', error);
          }
        }
      }

      // Refresh work orders list
      const workOrdersResponse = await maintenanceApiService.getWorkOrders();
      setWorkOrders(workOrdersResponse.items);

      toast({
        title: "Success",
        description: `Work order ${createdWorkOrder.workOrderNumber || ''} created successfully`,
        className: "bg-green-50 border-green-200",
      });

      setIsCreateDialogOpen(false);
      setNewWorkOrder({
        title: '',
        description: '',
        assetName: '',
        assignedTechnician: '',
        priority: 'Medium',
        maintenanceLocation: 'Internal',
        dueDate: '',
        workOrderType: 'Preventive',
        estimatedHours: 0,
        jobCardId: '',
        jobCardNumber: '',
        requiredTools: [],
      });
    } catch (error) {
      console.error('Error creating work order:', error);
      toast({
        title: "Error",
        description: "Failed to create work order. Please try again.",
        variant: "destructive",
      });
    }
  };

  const handleEditWorkOrder = async () => {
    if (!selectedOrder) return;

    try {
      const updateData = {
        id: selectedOrder.id,
        title: selectedOrder.title,
        description: selectedOrder.description,
        workOrderTypeId: selectedOrder.workOrderTypeId,
        maintenanceTypeId: selectedOrder.maintenanceTypeId,
        priorityLevelId: selectedOrder.priorityLevelId,
        assignedTechnicianId: selectedOrder.assignedTechnicianId,
        maintenanceLocation: selectedOrder.maintenanceLocation || 'Internal',
        requestedCompletionDate: selectedOrder.requestedCompletionDate,
        estimatedHours: selectedOrder.estimatedHours || 0,
        estimatedCost: selectedOrder.estimatedCost || 0,
      };

      await maintenanceApiService.updateWorkOrder(selectedOrder.id, updateData);

      // Refresh work orders list
      const workOrdersResponse = await maintenanceApiService.getWorkOrders();
      setWorkOrders(workOrdersResponse.items);

      toast({
        title: "Success",
        description: `Work order ${selectedOrder.workOrderNumber} updated successfully`,
        className: "bg-green-50 border-green-200",
      });

      setIsEditDialogOpen(false);
      setSelectedOrder(null);
    } catch (error) {
      console.error('Error updating work order:', error);
      toast({
        title: "Error",
        description: "Failed to update work order. Please try again.",
        variant: "destructive",
      });
    }
  };

  const getStatusBadge = (status: WorkOrder['status']) => {
    const variants = {
      'Draft': 'outline',
      'Open': 'default',
      'Approved': 'default',
      'InProgress': 'secondary',
      'OnHold': 'outline',
      'Completed': 'default',
      'Cancelled': 'destructive',
    } as const;

    const colors = {
      'Draft': 'bg-gray-100 text-gray-800',
      'Open': 'bg-blue-100 text-blue-800',
      'Approved': 'bg-purple-100 text-purple-800',
      'InProgress': 'bg-yellow-100 text-yellow-800',
      'OnHold': 'bg-orange-100 text-orange-800',
      'Completed': 'bg-green-100 text-green-800',
      'Cancelled': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[status]}>
        {status === 'InProgress' ? 'In Progress' :
         status === 'OnHold' ? 'On Hold' :
         status === 'Draft' ? 'Draft' :
         status === 'Approved' ? 'Approved' :
         status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: WorkOrder['priority']) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    } as Record<string, string>;

    const display = priority || 'Not Set';
    const key = colors[priority] ? priority : undefined;

    return (
      <Badge className={key ? colors[key] : 'bg-gray-100 text-gray-800'}>
        {display}
      </Badge>
    );
  };

  // Calculate actual cost for a work order (for list display)
  const getWorkOrderActualCost = (orderId: string): number => {
    // Check cache first
    if (workOrderCosts[orderId] !== undefined) {
      return workOrderCosts[orderId];
    }
    return 0; // Will be calculated when work order details are loaded
  };

  const getCostBadge = (cost: number) => {
    if (cost === 0) {
      return <Badge variant="outline" className="text-xs text-muted-foreground">$0.00</Badge>;
    }

    // Color code based on cost magnitude
    const colorClass = cost > 10000
      ? 'bg-red-50 text-red-700 border-red-200'
      : cost > 5000
      ? 'bg-orange-50 text-orange-700 border-orange-200'
      : cost > 1000
      ? 'bg-yellow-50 text-yellow-700 border-yellow-200'
      : 'bg-green-50 text-green-700 border-green-200';

    return (
      <Badge variant="outline" className={`text-xs font-semibold ${colorClass}`}>
        ${cost.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
      </Badge>
    );
  };

  const updateWorkOrderStatus = async (orderId: string, newStatus: WorkOrder['status']) => {
    try {
      // If trying to complete work order, validate quality control first
      if (newStatus === 'Completed') {
        const validation = await validateQualityControl(orderId);
        if (!validation.canComplete) {
          toast({
            title: 'Cannot Complete Work Order',
            description: validation.validationFailures.join(', '),
            variant: 'destructive'
          });
          return;
        }

        if (validation.requiresInspectionOfficerApproval) {
          toast({
            title: 'Inspection Required',
            description: 'Work order requires inspection officer approval before completion.',
            variant: 'destructive'
          });
          return;
        }
      }

      await maintenanceApiService.updateWorkOrderStatus(orderId, newStatus);

      // Refresh work orders list
      const workOrdersResponse = await maintenanceApiService.getWorkOrders();
      setWorkOrders(workOrdersResponse.items);

      const statusMessages: Record<string, string> = {
        'Open': 'opened',
        'InProgress': 'started',
        'OnHold': 'paused',
        'Completed': 'completed',
        'Cancelled': 'cancelled'
      };

      toast({
        title: "Success",
        description: `Work order ${statusMessages[newStatus] || 'updated'} successfully`,
        className: "bg-green-50 border-green-200",
      });
    } catch (error) {
      console.error('Error updating work order status:', error);
      toast({
        title: 'Error',
        description: 'Failed to update work order status',
        variant: 'destructive'
      });
    }
  };

  const validateQualityControl = async (workOrderId: string): Promise<QualityValidationResult> => {
    try {
      const validation = await qualityControlService.validateWorkOrderCompletion(workOrderId);
      setQualityValidation(prev => ({ ...prev, [workOrderId]: validation }));
      return validation;
    } catch (error) {
      console.error('Error validating quality control:', error);
      // Return a default validation that blocks completion
      return {
        workOrderId,
        canComplete: false,
        validationDate: new Date().toISOString(),
        requiredInspections: [],
        validationMessages: [],
        validationFailures: ['Quality control validation failed'],
        requiresInspectionOfficerApproval: false
      };
    }
  };

  // Submit work order for QC inspection
  const handleSubmitForQCInspection = async (orderId: string) => {
    try {
      // Validate work order first
      const validation = await validateQualityControl(orderId);

      // Check if all required tasks are completed before QC submission
      if (validation.validationFailures.some(f => f.includes('task'))) {
        toast({
          title: 'Cannot Submit for QC',
          description: 'All required tasks must be completed before submitting for quality control inspection.',
          className: "bg-yellow-50 border-yellow-200",
        });
        return;
      }

      // Submit for QC inspection
      const qualityCheck = await qualityControlService.submitWorkOrderForInspection(orderId);

      // Refresh work orders list to show updated status
      const workOrdersResponse = await maintenanceApiService.getWorkOrders();
      setWorkOrders(workOrdersResponse.items);

      toast({
        title: "Submitted for QC Inspection",
        description: `Work order submitted for quality control inspection using checklist: ${qualityCheck.checklistName}`,
        className: "bg-blue-50 border-blue-200",
      });
    } catch (error: any) {
      console.error('Error submitting for QC inspection:', error);
      const errorMessage = error.response?.data?.message || error.response?.data || error.message || 'Failed to submit for QC inspection';
      toast({
        title: 'Error',
        description: errorMessage,
        variant: 'destructive'
      });
    }
  };


  const handleTaskStatusUpdate = async (taskId: string, newStatus: string, actualHours: number | null = null, completionNotes: string | null = null) => {
    try {
      // Update task status via API
      const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api'}/maintenance/work-orders/tasks/${taskId}/status`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('authToken') || ''}`
        },
        body: JSON.stringify({
          status: newStatus,
          actualHours: actualHours,
          completionNotes: completionNotes
        })
      });

      if (!response.ok) {
        throw new Error('Failed to update task status');
      }

      const updatedTask = await response.json();

      // Update local state with the response from the server
      setSelectedOrderTasks(prevTasks =>
        prevTasks.map(task =>
          task.id === taskId ? updatedTask : task
        )
      );

      toast({
        title: 'Success',
        description: `Task ${newStatus === 'Completed' ? 'completed' : 'updated'} successfully`,
        variant: 'success'
      });
    } catch (error) {
      console.error('Error updating task status:', error);
      toast({
        title: 'Error',
        description: 'Failed to update task status',
        variant: 'destructive'
      });
    }
  };

  const handleCompleteTaskClick = (task: WorkOrderTask) => {
    setSelectedTask(task);
    setTaskActualHours(task.actualHours || task.estimatedHours);
    setTaskCompletionNotes('');
    setIsTaskCompletionDialogOpen(true);
  };

  const handleCompleteTaskSubmit = async () => {
    if (!selectedTask) return;

    await handleTaskStatusUpdate(selectedTask.id, 'Completed', taskActualHours, taskCompletionNotes);
    setIsTaskCompletionDialogOpen(false);
    setSelectedTask(null);
    setTaskActualHours(0);
    setTaskCompletionNotes('');
  };

  const handleSort = (column: string) => {
    if (sortColumn === column) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(column);
      setSortDirection('asc');
    }
  };

  const getSortedOrders = () => {
    const sorted = [...filteredOrders].sort((a, b) => {
      let aVal: any = a[sortColumn as keyof WorkOrder];
      let bVal: any = b[sortColumn as keyof WorkOrder];

      if (aVal === null || aVal === undefined) aVal = '';
      if (bVal === null || bVal === undefined) bVal = '';

      if (typeof aVal === 'string') {
        aVal = aVal.toLowerCase();
        bVal = bVal.toLowerCase();
      }

      if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
      if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
      return 0;
    });
    return sorted;
  };

  const SortHeader = ({ label, column }: { label: string; column: string }) => {
    return (
      <TableHead className="cursor-pointer hover:bg-gray-100 select-none">
        <div
          className="flex items-center gap-1"
          onClick={() => handleSort(column)}
        >
          <span>{label}</span>
          {sortColumn === column ? (
            sortDirection === 'asc' ? (
              <ArrowUp className="h-4 w-4" />
            ) : (
              <ArrowDown className="h-4 w-4" />
            )
          ) : (
            <ArrowUpDown className="h-4 w-4 opacity-30" />
          )}
        </div>
      </TableHead>
    );
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Work Orders</h1>
          <p className="text-muted-foreground">
            Create and manage maintenance work orders
          </p>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Work Orders</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Create Work Order button hidden - work orders only originate from job card approval */}

      {/* Filters */}
      <Card>
        <CardContent className="p-4">
          <div className="flex items-center space-x-4 flex-wrap gap-4">
            <div className="flex-1 min-w-[200px] max-w-sm">
              <Label htmlFor="search" className="sr-only">Search</Label>
              <div className="relative">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  id="search"
                  placeholder="Search work orders..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
            </div>
            <ClientOnly>
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="Draft">Draft</SelectItem>
                  <SelectItem value="Open">Open</SelectItem>
                  <SelectItem value="Approved">Approved</SelectItem>
                  <SelectItem value="InProgress">In Progress</SelectItem>
                  <SelectItem value="OnHold">On Hold</SelectItem>
                  <SelectItem value="Completed">Completed</SelectItem>
                  <SelectItem value="Cancelled">Cancelled</SelectItem>
                </SelectContent>
              </Select>
            </ClientOnly>
            <ClientOnly>
              <Select value={priorityFilter} onValueChange={setPriorityFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Priority" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Priority</SelectItem>
                  {priorityLevels.map((priority) => (
                    <SelectItem key={priority.id} value={priority.name}>
                      {priority.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </ClientOnly>
            <ClientOnly>
              <Popover>
                <PopoverTrigger asChild>
                  <Button variant="outline" className="justify-start text-left font-normal w-[250px]">
                    <CalendarIcon className="mr-2 h-4 w-4" />
                    {dateRange?.from ? (
                      dateRange.to ? (
                        <>
                          {format(dateRange.from, "LLL dd, y")} - {format(dateRange.to, "LLL dd, y")}
                        </>
                      ) : (
                        format(dateRange.from, "LLL dd, y")
                      )
                    ) : (
                      <span>Pick a date range</span>
                    )}
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-auto p-0" align="start">
                  <CalendarComponent
                    initialFocus
                    mode="range"
                    defaultMonth={dateRange?.from}
                    selected={dateRange}
                    onSelect={setDateRange}
                    numberOfMonths={2}
                  />
                </PopoverContent>
              </Popover>
            </ClientOnly>
          </div>
        </CardContent>
      </Card>

      {/* Work Orders Table */}
      <Card>
        <CardHeader>
          <CardTitle>Work Orders ({filteredOrders.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <SortHeader label="Work Order #" column="workOrderNumber" />
                <SortHeader label="Created Date" column="createdAt" />
                <SortHeader label="Asset" column="assetName" />
                <TableHead>Job Card</TableHead>
                <TableHead>Technician</TableHead>
                <SortHeader label="Status" column="status" />
                <SortHeader label="Priority" column="priority" />
                <TableHead>Actual Cost</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {getSortedOrders().map((order) => (
                <TableRow key={order.id}>
                  <TableCell className="font-medium font-mono">{order.workOrderNumber}</TableCell>
                  <TableCell className="text-sm">{format(new Date(order.createdAt), 'MMM dd, yyyy HH:mm')}</TableCell>
                  <TableCell>
                    {order.assetId ? (
                      <button
                        onClick={() => window.open(`/maintenance/assets?id=${order.assetId}`, '_blank')}
                        className="text-blue-600 hover:text-blue-800 hover:underline flex items-center gap-1 transition-colors cursor-pointer"
                        title="Open asset in new tab"
                      >
                        {order.assetName}
                        <ExternalLink className="h-3 w-3" />
                      </button>
                    ) : (
                      <span>{order.assetName}</span>
                    )}
                      {order.assetId && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => window.open(`/maintenance/asset-admission?assetId=${order.assetId}&workOrderId=${order.id}`, '_blank')}
                          className="ml-2 text-xs"
                        >
                          Admit
                        </Button>
                      )}

                  </TableCell>
                  <TableCell>
                    {order.jobCardNumber && order.jobCardId ? (
                      <button
                        onClick={() => window.open(`/maintenance/job-cards?id=${order.jobCardId}`, '_blank')}
                        className="inline-flex items-center gap-1 hover:opacity-70 hover:scale-105 transition-all cursor-pointer"
                        title="Open job card in new tab"
                      >
                        <Badge variant="outline" className="text-xs hover:border-blue-400">
                          {order.jobCardNumber}
                        </Badge>
                        <ExternalLink className="h-3 w-3 text-muted-foreground" />
                      </button>
                    ) : order.jobCardNumber ? (
                      <Badge variant="outline" className="text-xs">
                        {order.jobCardNumber}
                      </Badge>
                    ) : (
                      <span className="text-muted-foreground text-sm">-</span>
                    )}
                  </TableCell>
                  <TableCell>{getTechnicianNamesFromSchedules(order.id)}</TableCell>
                  <TableCell>
                    <div className="flex flex-col gap-1">
                      {getStatusBadge(order.status)}
                      {qualityValidation[order.id] && (
                        <span className="text-[11px] font-medium text-muted-foreground">
                          {qualityValidation[order.id].canComplete
                            ? 'QC: All checks satisfied'
                            : 'QC: Action required'}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>{getPriorityBadge(order.priority)}</TableCell>
                  <TableCell>{getCostBadge(getWorkOrderActualCost(order.id))}</TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={async () => {
                          setSelectedOrder(order);
                          setIsViewDialogOpen(true);

                          // Fetch work order details (includes tasks, parts, and labor)
                          setLoadingTasks(true);
                          setLoadingParts(true);
                          setLoadingTools(true);
                          setSelectedOrderTasks([]); // Clear previous tasks
                          setWorkOrderParts([]);
                          setWorkOrderTools([]);
                          setWorkOrderLabor([]);
                          setStaffSchedules([]);
                          setExpenses([]);

                          try {
                            console.log('Fetching work order details for ID:', order.id);
                            const workOrderDetails = await maintenanceApiService.getWorkOrderById(order.id);
                            console.log('Work order details received:', workOrderDetails);
                            console.log('Tasks in response:', workOrderDetails.tasks);
                            console.log('Parts in response:', workOrderDetails.parts);
                            console.log('Labor in response:', workOrderDetails.labor);

                            // Update selectedOrder with full details to ensure assignedTechnicianId is available
                            setSelectedOrder(prev => prev ? { ...prev, ...workOrderDetails } : workOrderDetails);

                            // Set labor data
                            if (workOrderDetails.labor && Array.isArray(workOrderDetails.labor)) {
                              setWorkOrderLabor(workOrderDetails.labor);
                              console.log('Set labor with', workOrderDetails.labor.length, 'records');
                            } else {
                              console.warn('No labor array in response');
                              setWorkOrderLabor([]);
                            }

                            // Set tasks
                            if (workOrderDetails.tasks && Array.isArray(workOrderDetails.tasks)) {
                              setSelectedOrderTasks(workOrderDetails.tasks);
                              console.log('Set tasks state with', workOrderDetails.tasks.length, 'tasks');
                            } else {
                              console.warn('No tasks array in response');
                              setSelectedOrderTasks([]);
                            }

                            // If work order is completed, load historical data from work order details
                            // Otherwise, load current data from separate services
                            if (order.status === 'Completed' || order.status === 'Cancelled') {
                              console.log('Loading historical data for completed work order');

                              // Set parts from work order details (history)
                              if (workOrderDetails.parts && Array.isArray(workOrderDetails.parts)) {
                                setWorkOrderParts(workOrderDetails.parts);
                                console.log('Set parts history with', workOrderDetails.parts.length, 'parts');
                              } else {
                                console.warn('No parts array in response');
                                setWorkOrderParts([]);
                              }

                              // Set tools from work order details (history)
                              if (workOrderDetails.tools && Array.isArray(workOrderDetails.tools)) {
                                // Map minimal tool data to full tool format expected by UI
                                const toolsWithDetails = await Promise.all(
                                  workOrderDetails.tools.map(async (tool: any) => {
                                    // Try to fetch full tool details from service
                                    try {
                                      const fullTools = await workOrderToolService.getWorkOrderTools(order.id);
                                      const fullTool = fullTools.find(ft => ft.toolId === tool.toolId);
                                      return fullTool || tool;
                                    } catch {
                                      return tool;
                                    }
                                  })
                                );
                                setWorkOrderTools(toolsWithDetails);
                                console.log('Set tools history with', toolsWithDetails.length, 'tools');

                                // Try to get tool summary
                                try {
                                  const summaryData = await workOrderToolService.getToolSummary(order.id);
                                  setToolSummary(summaryData);
                                } catch (error) {
                                  console.error('Error loading tool summary:', error);
                                }
                              } else {
                                console.warn('No tools array in response');
                                setWorkOrderTools([]);
                              }
                            } else {
                              console.log('Loading current data for active work order');

                              // Load current parts from service
                              try {
                                const partsData = await workOrderPartService.getPartsByWorkOrder(order.id);
                                setWorkOrderParts(partsData);
                                console.log('Set current parts with', partsData.length, 'parts');
                              } catch (error) {
                                console.error('Error loading current parts:', error);
                                setWorkOrderParts([]);
                              }

                              // Load current tools from service
                              try {
                                const [toolsData, summaryData] = await Promise.all([
                                  workOrderToolService.getWorkOrderTools(order.id),
                                  workOrderToolService.getToolSummary(order.id)
                                ]);
                                setWorkOrderTools(toolsData);
                                setToolSummary(summaryData);
                                console.log('Set current tools with', toolsData.length, 'tools');
                              } catch (error) {
                              console.error('Error loading current tools:', error);
                              }
                            }

                            // Load schedules for this work order
                            try {
                              const schedulesData = await maintenanceApiService.getStaffSchedulesByWorkOrder(order.id);
                              const enrichedSchedules = enrichSchedulesWithFullNames(schedulesData || []);
                              setStaffSchedules(enrichedSchedules);
                              console.log('Set schedules with', enrichedSchedules.length, 'records');
                            } catch (error) {
                              console.error('Error loading schedules:', error);
                              setStaffSchedules([]);
                            }

                            // Load expenses for this work order
                            let expensesData: MaintenanceExpense[] = [];
                            try {
                              expensesData = await maintenanceApiService.getExpensesByWorkOrder(order.id);
                              setExpenses(expensesData);
                              console.log('Set expenses with', expensesData.length, 'records');
                            } catch (error) {
                              console.error('Error loading expenses:', error);
                              setExpenses([]);
                            }

                            // Calculate and cache total cost for this work order (after all data is loaded)
                            const laborCost = (workOrderDetails.labor || []).reduce((sum: number, l: any) => sum + (l.totalCost || 0), 0);
                            const partsCost = (workOrderDetails.parts || []).reduce((sum: number, p: any) => sum + (p.totalCost || 0), 0);
                            // Get tool cost from summary if available
                            let toolsCost = 0;
                            try {
                              const summary = await workOrderToolService.getToolSummary(order.id);
                              toolsCost = summary?.totalRentalCost || 0;
                            } catch {
                              toolsCost = 0;
                            }
                            const expensesCost = expensesData.reduce((sum, e) => sum + e.amount, 0);
                            const totalCost = laborCost + partsCost + toolsCost + expensesCost;

                            setWorkOrderCosts(prev => ({ ...prev, [order.id]: totalCost }));
                          } catch (error) {
                            console.error('Error fetching work order details:', error);
                            setSelectedOrderTasks([]);
                            setWorkOrderParts([]);
                          } finally {
                            setLoadingTasks(false);
                            setLoadingParts(false);
                            setLoadingTools(false);
                          }
                        }}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                      {/* Draft status actions */}
                      {order.status === 'Draft' && (
                        <>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => updateWorkOrderStatus(order.id, 'Open')}
                          >
                            Approve
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={async () => {
                              setSelectedOrder(order);
                              setIsEditDialogOpen(true);

                              // Load existing parts, tools, schedules, and expenses for this work order
                              if (order.id) {
                                // Load parts
                                try {
                                  const existingParts = await workOrderPartService.getPartsByWorkOrder(order.id);
                                  setWorkOrderParts(existingParts);
                                } catch (error) {
                                  console.error('Error loading consumables:', error);
                                  setWorkOrderParts([]);
                                }

                                // Load already allocated tools
                                try {
                                  const existingTools = await workOrderToolService.getWorkOrderTools(order.id);
                                  // Pre-select the already allocated tools
                                  const allocatedToolIds = existingTools.map(t => t.toolId);
                                  setSelectedTools(allocatedToolIds);
                                } catch (error) {
                                  console.error('Error loading work order tools:', error);
                                  setSelectedTools([]);
                                }

                                // Load existing schedules
                                try {
                                  const existingSchedules = await maintenanceApiService.getStaffSchedulesByWorkOrder(order.id);
                                  setStaffSchedules(existingSchedules);
                                } catch (error) {
                                  console.error('Error loading schedules:', error);
                                  setStaffSchedules([]);
                                }

                                // Load existing expenses
                                try {
                                  const existingExpenses = await maintenanceApiService.getExpensesByWorkOrder(order.id);
                                  setExpenses(existingExpenses);
                                } catch (error) {
                                  console.error('Error loading expenses:', error);
                                  setExpenses([]);
                                }
                              }
                            }}
                          >
                            <Edit className="h-4 w-4" />
                          </Button>
                        </>
                      )}
                      {/* Open/Approved status actions */}
                      {(order.status === 'Open' || order.status === 'Approved') && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setWorkOrderToStart(order);
                            setIsStartWorkOrderDialogOpen(true);
                          }}
                        >
                          Start
                        </Button>
                      )}
                      {/* In Progress status actions */}
                      {order.status === 'InProgress' && (
                        <>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setWorkOrderToSubmitQC(order);
                              setIsSubmitQCDialogOpen(true);
                            }}
                            className="bg-blue-50 hover:bg-blue-100"
                            title="Submit work order for quality control inspection"
                          >
                            Submit for QC
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => updateWorkOrderStatus(order.id, 'OnHold')}
                          >
                            Pause
                          </Button>
                        </>
                      )}
                      {/* On Hold status actions */}
                      {order.status === 'OnHold' && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => updateWorkOrderStatus(order.id, 'InProgress')}
                        >
                          Resume
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* View Work Order Dialog */}
      <ClientOnly>
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="max-w-6xl h-[85vh] flex flex-col">
          <DialogHeader>
            <DialogTitle>Work Order Details</DialogTitle>
            <DialogDescription>
              View and manage work order information
            </DialogDescription>
          </DialogHeader>
          {selectedOrder && (
            <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full flex flex-col flex-1 overflow-hidden">
              <TabsList className="grid w-full grid-cols-8 flex-shrink-0 bg-muted/50 p-1 rounded-lg gap-1">
                <TabsTrigger value="details">
                  <FileText className="h-4 w-4 mr-2" />
                  Details
                </TabsTrigger>
                <TabsTrigger value="quality">
                  <ShieldCheck className="h-4 w-4 mr-2" />
                  Quality
                </TabsTrigger>
                <TabsTrigger value="tasks">
                  <ClipboardList className="h-4 w-4 mr-2" />
                  Tasks ({selectedOrderTasks.length})
                </TabsTrigger>
                <TabsTrigger value="tools">
                  <Wrench className="h-4 w-4 mr-2" />
                  Tools ({toolSummary?.totalTools || 0})
                </TabsTrigger>
                <TabsTrigger value="parts">
                  <Package className="h-4 w-4 mr-2" />
                  Parts
                </TabsTrigger>
                <TabsTrigger value="schedule">
                  <CalendarClock className="h-4 w-4 mr-2" />
                  Schedule
                </TabsTrigger>
                <TabsTrigger value="expenses">
                  <DollarSign className="h-4 w-4 mr-2" />
                  Expenses
                </TabsTrigger>
                {/* Labor tab temporarily hidden as requested */}
                <TabsTrigger value="history">
                  <History className="h-4 w-4 mr-2" />
                  Workflow History
                </TabsTrigger>
              </TabsList>

              {/* Details Tab */}
              <TabsContent value="details" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4">
                  <div className="mb-4">
                    <Label className="text-sm font-medium text-muted-foreground">Work Order Number</Label>
                    <p className="text-lg font-mono">{selectedOrder.workOrderNumber}</p>
                  </div>

                  {/* High-level cost snapshot */}
                  <div className="grid grid-cols-2 gap-4">
                    <Card className="border-dashed">
                      <CardHeader className="pb-2">
                        <CardTitle className="text-sm">Estimated</CardTitle>
                        <CardDescription className="text-xs">Planned effort & cost</CardDescription>
                      </CardHeader>
                      <CardContent className="space-y-1">
                        <div className="flex justify-between text-xs text-muted-foreground">
                          <span>Estimated Hours</span>
                          <span className="font-medium">{selectedOrder.estimatedHours ?? 0} hrs</span>
                        </div>
                        <div className="flex justify-between text-xs text-muted-foreground">
                          <span>Estimated Cost</span>
                          <span className="font-medium">
                            ${((selectedOrder.estimatedCost ?? 0)).toFixed(2)}
                          </span>
                        </div>
                      </CardContent>
                    </Card>

                    <Card className="border-dashed">
                      <CardHeader className="pb-2">
                        <CardTitle className="text-sm">Actual to Date</CardTitle>
                        <CardDescription className="text-xs">Labor, parts, tools & expenses</CardDescription>
                      </CardHeader>
                      <CardContent className="space-y-1">
                        {(() => {
                          const laborCost = workOrderLabor?.reduce((sum: number, l: any) => sum + (l.totalCost || 0), 0) || 0;
                          const partsCost = workOrderParts?.reduce((sum, p) => sum + (p.totalCost || 0), 0) || 0;
                          const toolsCost = toolSummary?.totalRentalCost || 0;
                          const expensesCost = totalExpenses || 0;
                          const actualTotal = laborCost + partsCost + toolsCost + expensesCost;

                          return (
                            <>
                              <div className="flex justify-between text-xs text-muted-foreground">
                                <span>Labor</span>
                                <span className="font-medium">${laborCost.toFixed(2)}</span>
                              </div>
                              <div className="flex justify-between text-xs text-muted-foreground">
                                <span>Parts</span>
                                <span className="font-medium">${partsCost.toFixed(2)}</span>
                              </div>
                              <div className="flex justify-between text-xs text-muted-foreground">
                                <span>Tools</span>
                                <span className="font-medium">${toolsCost.toFixed(2)}</span>
                              </div>
                              <div className="flex justify-between text-xs text-muted-foreground">
                                <span>Expenses</span>
                                <span className="font-medium">${expensesCost.toFixed(2)}</span>
                              </div>
                              <div className="mt-2 border-t pt-2 flex justify-between text-xs">
                                <span className="font-semibold">Actual Total</span>
                                <span className="font-semibold">${actualTotal.toFixed(2)}</span>
                              </div>
                            </>
                          );
                        })()}
                      </CardContent>
                    </Card>
                  </div>

                  {/* Core meta */}
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Title</Label>
                      <p className="text-sm">{selectedOrder.title}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Asset</Label>
                      <p className="text-sm">{selectedOrder.assetName}</p>
                    </div>
                  </div>
                  {selectedOrder.jobCardNumber && (
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Job Card</Label>
                      <div className="pt-1">
                        <Badge variant="outline">{selectedOrder.jobCardNumber}</Badge>
                      </div>
                    </div>
                  )}
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Description</Label>
                    <p className="text-sm">{selectedOrder.description}</p>
                  </div>
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                    <div className="pt-1">{getStatusBadge(selectedOrder.status)}</div>
                  </div>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Priority</Label>
                      <div className="pt-1">{getPriorityBadge(selectedOrder.priority)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Type</Label>
                      <p className="text-sm">{selectedOrder.type}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Estimated Hours</Label>
                      <p className="text-sm">{selectedOrder.estimatedHours} hrs</p>
                    </div>
                  </div>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Created Date</Label>
                      <p className="text-sm">{format(new Date(selectedOrder.createdAt), 'PPP')}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Due Date</Label>
                      <p className="text-sm">{selectedOrder.requestedCompletionDate ? format(new Date(selectedOrder.requestedCompletionDate), 'PPP') : 'Not set'}</p>
                    </div>
                    {selectedOrder.actualCompletionDate && (
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Completed Date</Label>
                        <p className="text-sm">{format(new Date(selectedOrder.actualCompletionDate), 'PPP')}</p>
                      </div>
                    )}
                  </div>
                  {selectedOrder.actualHours && (
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Actual Hours</Label>
                      <p className="text-sm">{selectedOrder.actualHours} hrs</p>
                    </div>
                  )}
                </div>
              </TabsContent>

              {/* Quality Tab */}
              <TabsContent value="quality" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4">
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">QC Status</Label>
                      <div className="mt-1">
                        {qualityValidation[selectedOrder.id] ? (
                          <div className="space-y-1">
                            <p className="text-sm font-medium">
                              {qualityValidation[selectedOrder.id].canComplete
                                ? 'All QC checks satisfied for completion'
                                : 'QC checks outstanding before completion'}
                            </p>
                            {qualityValidation[selectedOrder.id].requiresInspectionOfficerApproval && (
                              <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded px-2 py-1">
                                Inspection officer approval is required before this work order can be completed.
                              </p>
                            )}
                          </div>
                        ) : (
                          <p className="text-sm text-muted-foreground">
                            QC validation has not been run yet for this work order.
                          </p>
                        )}
                      </div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">QC Actions</Label>
                      <div className="mt-2 flex flex-wrap gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => validateQualityControl(selectedOrder.id)}
                          disabled={selectedOrder.status === 'Completed' || selectedOrder.status === 'Cancelled'}
                        >
                          Refresh QC Status
                        </Button>
                        {selectedOrder.status === 'InProgress' && (
                          <Button
                            size="sm"
                            variant="outline"
                            className="bg-blue-50 hover:bg-blue-100"
                            onClick={() => handleSubmitForQCInspection(selectedOrder.id)}
                          >
                            Submit for QC
                          </Button>
                        )}
                      </div>
                    </div>
                  </div>

                  {qualityValidation[selectedOrder.id] && (
                    <>
                      {qualityValidation[selectedOrder.id].requiredInspections && qualityValidation[selectedOrder.id].requiredInspections.length > 0 && (
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">Required Inspections</Label>
                          <div className="mt-2 space-y-2">
                            {qualityValidation[selectedOrder.id].requiredInspections.map((inspection) => (
                              <div
                                key={inspection.inspectionTemplateId}
                                className="border rounded-md px-3 py-2 bg-muted/40 flex items-start justify-between gap-3"
                              >
                                <div>
                                  <p className="text-sm font-medium flex items-center gap-2">
                                    {inspection.inspectionName}
                                    {inspection.isRegulatory && (
                                      <span className="text-[10px] uppercase tracking-wide bg-red-50 text-red-700 border border-red-200 rounded px-1.5 py-0.5">
                                        Regulatory
                                      </span>
                                    )}
                                  </p>
                                  <p className="text-xs text-muted-foreground">{inspection.description}</p>
                                  <p className="text-xs text-muted-foreground mt-1">Type: {inspection.inspectionType}</p>
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      {qualityValidation[selectedOrder.id].validationFailures && qualityValidation[selectedOrder.id].validationFailures.length > 0 && (
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">Blocking Issues</Label>
                          <div className="mt-2 bg-red-50 border border-red-200 rounded-md px-3 py-2 space-y-1">
                            {qualityValidation[selectedOrder.id].validationFailures.map((failure, index) => (
                              <p key={index} className="text-xs text-red-800">
                                • {failure}
                              </p>
                            ))}
                          </div>
                        </div>
                      )}

                      {qualityValidation[selectedOrder.id].validationMessages && qualityValidation[selectedOrder.id].validationMessages.length > 0 && (
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">Informational Messages</Label>
                          <div className="mt-2 bg-blue-50 border border-blue-200 rounded-md px-3 py-2 space-y-1">
                            {qualityValidation[selectedOrder.id].validationMessages.map((message, index) => (
                              <p key={index} className="text-xs text-blue-800">
                                {message}
                              </p>
                            ))}
                          </div>
                        </div>
                      )}
                    </>
                  )}
                </div>
              </TabsContent>

              {/* Tasks Tab */}
              <TabsContent value="tasks" className="flex-1 overflow-y-auto mt-4">
                {loadingTasks ? (
                  <div className="flex items-center justify-center py-8">
                    <p className="text-sm text-muted-foreground">Loading tasks...</p>
                  </div>
                ) : selectedOrderTasks.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-8 text-center">
                    <ClipboardList className="h-12 w-12 text-muted-foreground/50 mb-2" />
                    <p className="text-sm text-muted-foreground">No tasks found for this work order</p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {selectedOrderTasks.map((task, index) => (
                      <div key={task.id} className="border rounded-lg p-4 hover:bg-accent/50 transition-colors">
                        <div className="flex items-start justify-between mb-3">
                          <div className="flex items-start gap-3 flex-1">
                            <div className="flex items-center justify-center w-8 h-8 rounded-full bg-primary/10 text-primary font-semibold text-sm flex-shrink-0">
                              {index + 1}
                            </div>
                            <div className="flex-1">
                              <h4 className="font-semibold text-base mb-1">{task.taskName}</h4>
                              {task.description && (
                                <p className="text-sm text-muted-foreground">{task.description}</p>
                              )}
                            </div>
                          </div>
                          <Badge
                            variant={task.status === 'Completed' ? 'default' : task.status === 'InProgress' ? 'secondary' : 'outline'}
                            className="ml-2 flex-shrink-0"
                          >
                            {task.status}
                          </Badge>
                        </div>
                        <div className="flex flex-wrap items-center gap-4 text-sm text-muted-foreground pl-11">
                          <div className="flex items-center gap-1.5">
                            <User className="h-4 w-4" />
                            <span className="font-medium">{task.assignedTechnician?.fullName || 'Unassigned'}</span>
                          </div>
                          <div className="flex items-center gap-1.5">
                            <Clock className="h-4 w-4" />
                            <span>{task.actualHours}h / {task.estimatedHours}h</span>
                          </div>
                          {task.isRequired && (
                            <Badge variant="outline" className="text-xs">Required</Badge>
                          )}
                        </div>
                        {/* Task action buttons */}
                        {selectedOrder.status !== 'Completed' && selectedOrder.status !== 'Cancelled' && (
                          <div className="flex gap-2 mt-3 pl-11">
                            {task.status === 'Pending' && (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => handleTaskStatusUpdate(task.id, 'InProgress')}
                              >
                                Start Task
                              </Button>
                            )}
                            {task.status === 'InProgress' && (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => handleCompleteTaskClick(task)}
                              >
                                Mark Complete
                              </Button>
                            )}
                            {task.status === 'Completed' && task.completedAt && (
                              <p className="text-xs text-muted-foreground flex items-center gap-1">
                                <CheckCircle className="h-3 w-3 text-green-600" />
                                Completed on {new Date(task.completedAt).toLocaleString()}
                              </p>
                            )}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </TabsContent>

              {/* Tools Tab */}
              <TabsContent value="tools" className="flex-1 overflow-y-auto mt-4">
                {loadingTools ? (
                  <div className="flex items-center justify-center py-8">
                    <div className="flex flex-col items-center gap-2">
                      <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
                      <p className="text-sm text-muted-foreground">Loading tools...</p>
                    </div>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {/* Tool Summary Stats */}
                    {toolSummary && (
                      <div className="grid grid-cols-4 gap-4">
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Total Tools</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <p className="text-2xl font-bold">{toolSummary.totalTools}</p>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Checked Out</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <p className="text-2xl font-bold text-orange-600">{toolSummary.checkedOutTools}</p>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Overdue</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <p className="text-2xl font-bold text-red-600">{toolSummary.overdueTools}</p>
                          </CardContent>
                        </Card>
                        <Card>
                          <CardHeader className="pb-2">
                            <CardTitle className="text-sm">Rental Cost</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <p className="text-2xl font-bold">${toolSummary.totalRentalCost.toFixed(2)}</p>
                          </CardContent>
                        </Card>
                      </div>
                    )}

                    {/* Add Tool Button */}
                    {selectedOrder.status !== 'Completed' && selectedOrder.status !== 'Cancelled' && selectedOrder.assignedTechnicianId && (
                      <div className="flex justify-end">
                        <Dialog open={isToolAllocationDialogOpen} onOpenChange={setIsToolAllocationDialogOpen}>
                          <DialogTrigger asChild>
                            <Button size="sm" onClick={() => setIsToolAllocationDialogOpen(true)}>
                              <Plus className="h-4 w-4 mr-2" />
                              Allocate Tool
                            </Button>
                          </DialogTrigger>
                          <DialogContent>
                            <DialogHeader>
                              <DialogTitle>Allocate Tool to Work Order</DialogTitle>
                              <DialogDescription>
                                Select a tool to allocate to this work order
                              </DialogDescription>
                            </DialogHeader>
                            <div className="space-y-4 py-4">
                              <div className="space-y-2">
                                <Label>Select Tool</Label>
                                <Select
                                  onValueChange={(value) => setSelectedTools([value])}
                                >
                                  <SelectTrigger>
                                    <SelectValue placeholder="Select a tool" />
                                  </SelectTrigger>
                                  <SelectContent>
                                    {availableTools
                                      .filter(tool => tool.status === 'Available' && !workOrderTools.some(wt => wt.toolId === tool.id))
                                      .map((tool) => (
                                        <SelectItem key={tool.id} value={tool.id}>
                                          {tool.toolCode} - {tool.name} ({tool.category})
                                        </SelectItem>
                                      ))}
                                  </SelectContent>
                                </Select>
                              </div>
                            </div>
                            <DialogFooter>
                              <Button
                                onClick={async () => {
                                  if (selectedTools.length > 0 && selectedOrder?.id) {
                                    try {
                                      await workOrderToolService.allocateTool({
                                        workOrderId: selectedOrder.id,
                                        toolId: selectedTools[0],
                                        isRequired: true,
                                      });
                                      toast({
                                        title: 'Success',
                                        description: 'Tool allocated successfully',
                                        className: 'bg-green-50 border-green-200',
                                      });
                                      // Reload tools
                                      const [toolsData, summaryData] = await Promise.all([
                                        workOrderToolService.getWorkOrderTools(selectedOrder.id),
                                        workOrderToolService.getToolSummary(selectedOrder.id),
                                      ]);
                                      setWorkOrderTools(toolsData);
                                      setToolSummary(summaryData);
                                      setSelectedTools([]);
                                      setIsToolAllocationDialogOpen(false);
                                    } catch (error: any) {
                                      toast({
                                        title: 'Error',
                                        description: error.response?.data?.message || 'Failed to allocate tool',
                                        variant: 'destructive',
                                      });
                                    }
                                  }
                                }}
                              >
                                Allocate
                              </Button>
                            </DialogFooter>
                          </DialogContent>
                        </Dialog>
                      </div>
                    )}

                    {/* Tools List */}
                    {workOrderTools.length === 0 ? (
                      <div className="flex flex-col items-center justify-center py-8 text-center border-2 border-dashed rounded-lg">
                        <Package className="h-12 w-12 text-muted-foreground/50 mb-2" />
                        <p className="text-sm text-muted-foreground">No tools allocated to this work order</p>
                      </div>
                    ) : (
                      <div className="space-y-3">
                        {workOrderTools.map((tool) => (
                          <div key={tool.id} className="border rounded-lg p-4 hover:bg-accent/50 transition-colors">
                            <div className="flex items-start justify-between mb-3">
                              <div className="flex items-start gap-3 flex-1">
                                <Package className="h-5 w-5 text-primary mt-0.5" />
                                <div className="flex-1">
                                  <h4 className="font-semibold text-base mb-1">
                                    {tool.toolCode} - {tool.toolName}
                                  </h4>
                                  <p className="text-sm text-muted-foreground">{tool.description}</p>
                                  <div className="flex items-center gap-2 mt-2">
                                    <Badge variant="outline" className="text-xs">
                                      {tool.category}
                                    </Badge>
                                    {tool.isRequired && (
                                      <Badge variant="default" className="text-xs">Required</Badge>
                                    )}
                                    {tool.requiresCertification && (
                                      <Badge variant="destructive" className="text-xs">Certification Required</Badge>
                                    )}
                                  </div>
                                </div>
                              </div>
                              <div className="flex flex-col gap-2 items-end">
                                {tool.actualReturnDate ? (
                                  <Badge className="bg-blue-100 text-blue-800">Returned</Badge>
                                ) : tool.isCheckedOut ? (
                                  <Badge className="bg-orange-100 text-orange-800">Checked Out</Badge>
                                ) : (
                                  <Badge className="bg-green-100 text-green-800">Allocated</Badge>
                                )}
                              </div>
                            </div>

                            {/* Tool Details */}
                            <div className="grid grid-cols-2 gap-4 text-sm mt-3 pl-8">
                              {tool.currentLocation && (
                                <div>
                                  <span className="text-muted-foreground">Location:</span>
                                  <span className="ml-2 font-medium">{tool.currentLocation}</span>
                                </div>
                              )}
                              <div>
                                <span className="text-muted-foreground">Daily Rate:</span>
                                <span className="ml-2 font-medium">${tool.dailyRentalRate.toFixed(2)}</span>
                              </div>
                              {tool.isCheckedOut && tool.checkedOutByName && (
                                <div>
                                  <span className="text-muted-foreground">Checked Out By:</span>
                                  <span className="ml-2 font-medium">{tool.checkedOutByName}</span>
                                </div>
                              )}
                              {tool.checkoutDate && (
                                <div>
                                  <span className="text-muted-foreground">Checkout Date & Time:</span>
                                  <span className="ml-2 font-medium">
                                    {(() => {
                                      const d = new Date(tool.checkoutDate);
                                      const day = d.getDate().toString().padStart(2, '0');
                                      const month = d.toLocaleString('en-GB', { month: 'short' });
                                      const year = d.getFullYear();
                                      const hours = d.getHours().toString().padStart(2, '0');
                                      const minutes = d.getMinutes().toString().padStart(2, '0');
                                      const seconds = d.getSeconds().toString().padStart(2, '0');
                                      return `${day}-${month}-${year} ${hours}:${minutes}:${seconds}`;
                                    })()}
                                  </span>
                                </div>
                              )}
                              {tool.actualReturnDate && (
                                <div>
                                  <span className="text-muted-foreground">Return Date & Time:</span>
                                  <span className="ml-2 font-medium text-green-600">
                                    {(() => {
                                      const d = new Date(tool.actualReturnDate);
                                      const day = d.getDate().toString().padStart(2, '0');
                                      const month = d.toLocaleString('en-GB', { month: 'short' });
                                      const year = d.getFullYear();
                                      const hours = d.getHours().toString().padStart(2, '0');
                                      const minutes = d.getMinutes().toString().padStart(2, '0');
                                      const seconds = d.getSeconds().toString().padStart(2, '0');
                                      return `${day}-${month}-${year} ${hours}:${minutes}:${seconds}`;
                                    })()}
                                  </span>
                                </div>
                              )}
                            </div>

                            {/* Action Buttons - Hide if tool has been returned */}
                            {selectedOrder.status !== 'Completed' && selectedOrder.status !== 'Cancelled' && !tool.actualReturnDate && (
                              <div className="flex gap-2 mt-3 pl-8">
                                {!tool.isCheckedOut && staffSchedules.length > 0 && (
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() => {
                                      setSelectedWorkOrderTool(tool);
                                      setToolCheckoutDialogOpen(true);
                                    }}
                                  >
                                    <CheckCircle className="h-4 w-4 mr-1" />
                                    Checkout
                                  </Button>
                                )}
                                {!tool.isCheckedOut && staffSchedules.length === 0 && (
                                  <div className="text-xs text-muted-foreground italic">
                                    Create a technician schedule for this work order before checking out tools.
                                  </div>
                                )}
                                {tool.isCheckedOut && (
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() => {
                                      setSelectedWorkOrderTool(tool);
                                      setToolReturnDialogOpen(true);
                                    }}
                                  >
                                    Return Tool
                                  </Button>
                                )}
                                {!tool.isCheckedOut && (
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    className="border-red-200 hover:bg-red-50"
                                    onClick={async () => {
                                      if (selectedOrder?.id) {
                                        try {
                                          await workOrderToolService.removeToolAllocation(
                                            selectedOrder.id,
                                            tool.toolId
                                          );
                                          toast({
                                            title: 'Success',
                                            description: 'Tool removed from work order',
                                            className: 'bg-green-50 border-green-200',
                                          });
                                          // Reload tools
                                          const [toolsData, summaryData] = await Promise.all([
                                            workOrderToolService.getWorkOrderTools(selectedOrder.id),
                                            workOrderToolService.getToolSummary(selectedOrder.id),
                                          ]);
                                          setWorkOrderTools(toolsData);
                                          setToolSummary(summaryData);
                                        } catch (error: any) {
                                          toast({
                                            title: 'Error',
                                            description: error.response?.data?.message || 'Failed to remove tool',
                                            variant: 'destructive',
                                          });
                                        }
                                      }
                                    }}
                                  >
                                    Remove
                                  </Button>
                                )}
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </TabsContent>

              {/* Parts/Consumables Tab */}
              <TabsContent value="parts" className="flex-1 overflow-y-auto mt-4">
                {loadingParts ? (
                  <div className="flex items-center justify-center py-8">
                    <div className="flex flex-col items-center gap-2">
                      <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
                      <p className="text-sm text-muted-foreground">Loading parts...</p>
                    </div>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {workOrderParts.length === 0 ? (
                    <div className="flex flex-col items-center justify-center py-8 text-center border-2 border-dashed rounded-lg">
                      <Package className="h-12 w-12 text-muted-foreground/50 mb-2" />
                      <p className="text-sm text-muted-foreground">No parts/consumables allocated to this work order</p>
                    </div>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>#</TableHead>
                          <TableHead>Item Code</TableHead>
                          <TableHead>Item Name</TableHead>
                          <TableHead>Qty Required</TableHead>
                          <TableHead>Qty Used</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Actions</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {workOrderParts.map((part, index) => (
                          <TableRow key={part.id}>
                            <TableCell className="text-muted-foreground">{index + 1}</TableCell>
                            <TableCell className="font-medium">{part.itemCode}</TableCell>
                            <TableCell>{part.itemName}</TableCell>
                            <TableCell>{part.quantityRequired}</TableCell>
                            <TableCell>
                              <span className={part.quantityUsed > 0 ? 'text-green-600 font-medium' : 'text-muted-foreground'}>
                                {part.quantityUsed} / {part.quantityRequired}
                              </span>
                            </TableCell>
                            <TableCell>
                              <Badge variant="outline">{part.status}</Badge>
                            </TableCell>
                            <TableCell>
                              <div className="flex gap-2">
                                {part.quantityUsed < part.quantityRequired && part.status !== 'Returned' && part.quantityReturned === 0 && (
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    className="gap-1"
                                    onClick={() => {
                                      setPartToConsume(part);
                                      setQuantityToConsume(part.quantityRequired - part.quantityUsed);
                                      setIsConsumePartDialogOpen(true);
                                    }}
                                  >
                                    <FlaskConical className="h-3 w-3" />
                                    Consume
                                  </Button>
                                )}
                                {part.quantityAllocated > part.quantityUsed && part.quantityUsed > 0 && part.status !== 'Returned' && part.quantityReturned === 0 && (
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    className="gap-1 border-blue-200 hover:bg-blue-50"
                                    onClick={async () => {
                                      try {
                                        await workOrderPartService.returnUnusedParts(part.id);
                                        toast({
                                          title: 'Success',
                                          description: `Returned ${part.quantityAllocated - part.quantityUsed} unused units to warehouse`,
                                          className: 'bg-green-50 border-green-200',
                                        });
                                        // Reload parts
                                        if (selectedOrder?.id) {
                                          const updatedParts = await workOrderPartService.getWorkOrderParts(selectedOrder.id);
                                          setWorkOrderParts(updatedParts);
                                        }
                                      } catch (error: any) {
                                        toast({
                                          title: 'Error',
                                          description: error.response?.data?.message || 'Failed to return parts',
                                          variant: 'destructive',
                                        });
                                      }
                                    }}
                                  >
                                    <Package className="h-3 w-3" />
                                    Return
                                  </Button>
                                )}
                              </div>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                    )}
                  </div>
                )}
              </TabsContent>

              {/* Schedule Tab (View Only) */}
              <TabsContent value="schedule" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-lg font-semibold flex items-center gap-2">
                      <CalendarClock className="h-5 w-5" />
                      Technician Schedule
                    </h3>
                  </div>

                  {staffSchedules.length === 0 ? (
                    <div className="text-center py-8 text-muted-foreground">
                      <CalendarClock className="h-12 w-12 mx-auto mb-2 opacity-20" />
                      <p>No schedules found for this work order</p>
                    </div>
                  ) : (
                    <div className="border rounded-md">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Technician</TableHead>
                            <TableHead>Schedule Type</TableHead>
                            <TableHead>Start Date/Time</TableHead>
                            <TableHead>End Date/Time</TableHead>
                            <TableHead>Location</TableHead>
                            <TableHead>Vehicle</TableHead>
                            <TableHead>Status</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {enrichSchedulesWithFullNames(staffSchedules).map((schedule, index) => (
                            <TableRow key={schedule.id || index}>
                              <TableCell className="font-medium">{schedule.technicianFullName || schedule.technicianName || 'N/A'}</TableCell>
                              <TableCell>
                                <Badge variant="outline">{schedule.scheduleType}</Badge>
                              </TableCell>
                              <TableCell>
                                {schedule.startDateTime ? format(new Date(schedule.startDateTime), 'PPp') : 'N/A'}
                              </TableCell>
                              <TableCell>
                                {schedule.endDateTime ? format(new Date(schedule.endDateTime), 'PPp') : 'N/A'}
                              </TableCell>
                              <TableCell>{schedule.workLocation || 'N/A'}</TableCell>
                              <TableCell>{schedule.assignedVehicleName || '-'}</TableCell>
                              <TableCell>
                                <Badge
                                  variant={schedule.status === 'Completed' ? 'default' : schedule.status === 'InProgress' ? 'secondary' : 'outline'}
                                >
                                  {schedule.status}
                                </Badge>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  )}
                </div>
              </TabsContent>

              {/* Expenses Tab (View Only) */}
              <TabsContent value="expenses" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-lg font-semibold flex items-center gap-2">
                      <DollarSign className="h-5 w-5" />
                      Expenses
                    </h3>
                  </div>

                  {expenses.length === 0 ? (
                    <div className="text-center py-8 text-muted-foreground">
                      <DollarSign className="h-12 w-12 mx-auto mb-2 opacity-20" />
                      <p>No expenses found for this work order</p>
                    </div>
                  ) : (
                    <>
                      <div className="border rounded-md">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Expense Type</TableHead>
                              <TableHead>Description</TableHead>
                              <TableHead>Date</TableHead>
                              <TableHead>Amount</TableHead>
                              <TableHead>Vendor</TableHead>
                              <TableHead>Status</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {expenses.map((expense, index) => (
                              <TableRow key={expense.id || index}>
                                <TableCell>
                                  <Badge variant="outline">{expense.expenseType}</Badge>
                                </TableCell>
                                <TableCell>{expense.description || 'N/A'}</TableCell>
                                <TableCell>
                                  {expense.expenseDate ? format(new Date(expense.expenseDate), 'PPP') : 'N/A'}
                                </TableCell>
                                <TableCell className="font-medium">${expense.amount.toFixed(2)}</TableCell>
                                <TableCell>{expense.vendor || '-'}</TableCell>
                                <TableCell>
                                  <Badge variant={expense.isApproved ? 'default' : 'secondary'}>
                                    {expense.isApproved ? 'Approved' : 'Pending'}
                                  </Badge>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                      <div className="bg-muted/50 rounded-lg p-4">
                        <div className="flex justify-between items-center">
                          <span className="text-sm font-medium">Total Expenses:</span>
                          <span className="text-2xl font-bold text-green-600">${expenses.reduce((sum, e) => sum + e.amount, 0).toFixed(2)}</span>
                        </div>
                      </div>
                    </>
                  )}
                </div>
              </TabsContent>

              {/* Labor Tab temporarily removed as requested */}

              {/* Workflow History Tab */}
              <TabsContent value="history" className="flex-1 overflow-y-auto mt-4">
                  <div className="space-y-4">
                    {/* Timeline of workflow events */}
                    <div className="relative border-l-2 border-muted pl-6 space-y-6">
                      {/* Status Change Events */}
                      <div className="relative">
                        <div className="absolute -left-[1.6rem] w-4 h-4 rounded-full bg-primary border-4 border-background" />
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <Badge variant="default">Current</Badge>
                            <span className="font-semibold text-sm">{selectedOrder.status}</span>
                          </div>
                          <p className="text-xs text-muted-foreground">
                            {new Date().toLocaleString()}
                          </p>
                        </div>
                      </div>

                      {selectedOrder.actualStartDate && (
                        <div className="relative">
                          <div className="absolute -left-[1.6rem] w-4 h-4 rounded-full bg-muted border-4 border-background" />
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <CheckCircle className="h-4 w-4 text-green-600" />
                              <span className="font-semibold text-sm">Work Started</span>
                            </div>
                            <p className="text-xs text-muted-foreground">
                              {new Date(selectedOrder.actualStartDate).toLocaleString()}
                            </p>
                            {selectedOrder.assignedTechnicianName && (
                              <p className="text-xs text-muted-foreground">
                                Started by: {selectedOrder.assignedTechnicianName}
                              </p>
                            )}
                          </div>
                        </div>
                      )}

                      {selectedOrder.status === 'Approved' || selectedOrder.status === 'InProgress' || selectedOrder.status === 'Completed' ? (
                        <div className="relative">
                          <div className="absolute -left-[1.6rem] w-4 h-4 rounded-full bg-muted border-4 border-background" />
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <CheckCircle className="h-4 w-4 text-green-600" />
                              <span className="font-semibold text-sm">Work Order Approved</span>
                            </div>
                            <p className="text-xs text-muted-foreground">
                              Approved for execution
                            </p>
                          </div>
                        </div>
                      ) : null}

                      <div className="relative">
                        <div className="absolute -left-[1.6rem] w-4 h-4 rounded-full bg-muted border-4 border-background" />
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <CheckCircle className="h-4 w-4 text-blue-600" />
                            <span className="font-semibold text-sm">Work Order Created</span>
                          </div>
                          <p className="text-xs text-muted-foreground">
                            {new Date(selectedOrder.createdAt).toLocaleString()}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            Work Order: {selectedOrder.workOrderNumber}
                          </p>
                        </div>
                      </div>

                      {selectedOrder.jobCardNumber && (
                        <div className="relative">
                          <div className="absolute -left-[1.6rem] w-4 h-4 rounded-full bg-muted border-4 border-background" />
                          <div className="space-y-1">
                            <div className="flex items-center gap-2">
                              <AlertCircle className="h-4 w-4 text-orange-600" />
                              <span className="font-semibold text-sm">Generated from Job Card</span>
                            </div>
                            <p className="text-xs text-muted-foreground">
                              Job Card: {selectedOrder.jobCardNumber}
                            </p>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
              </TabsContent>
            </Tabs>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Edit Work Order Dialog */}
      <ClientOnly>
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="max-w-6xl h-[85vh] flex flex-col">
          <DialogHeader>
            <DialogTitle>Edit Work Order - {selectedOrder?.workOrderNumber}</DialogTitle>
            <DialogDescription>
              Update work order details
            </DialogDescription>
          </DialogHeader>
          {selectedOrder && (
            <Tabs defaultValue="details" className="w-full flex-1 overflow-hidden flex flex-col">
              <TabsList className="grid w-full grid-cols-5 bg-muted/50 p-1 rounded-lg gap-1">
                <TabsTrigger value="details">Details</TabsTrigger>
                <TabsTrigger value="tools">Tools</TabsTrigger>
                <TabsTrigger value="consumables">Parts</TabsTrigger>
                <TabsTrigger value="schedule">Schedule</TabsTrigger>
                <TabsTrigger value="expenses">Expenses</TabsTrigger>
              </TabsList>

              {/* Details Tab */}
              <TabsContent value="details" className="flex-1 overflow-y-auto mt-4">
                <div className="grid gap-4 py-4">
                  <div className="space-y-2">
                    <Label htmlFor="edit-title">Title <span className="text-red-500">*</span></Label>
                    <Input
                      id="edit-title"
                      value={selectedOrder.title}
                      onChange={(e) => setSelectedOrder({ ...selectedOrder, title: e.target.value })}
                      placeholder="Work order title"
                      required
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="edit-description">Description</Label>
                    <Textarea
                      id="edit-description"
                      value={selectedOrder.description || ''}
                      onChange={(e) => setSelectedOrder({ ...selectedOrder, description: e.target.value })}
                      placeholder="Detailed description of the work to be performed"
                      rows={3}
                    />
                  </div>
                  <div className="grid grid-cols-4 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="edit-workOrderType">Work Order Type</Label>
                      <Select
                        value={selectedOrder.workOrderTypeId || 'none'}
                        onValueChange={(value) => setSelectedOrder({
                          ...selectedOrder,
                          workOrderTypeId: value === 'none' ? undefined : value
                        })}
                        disabled={loadingData}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder={loadingData ? "Loading..." : "Select type"} />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">Not Selected</SelectItem>
                          {workOrderTypes.map((type) => (
                            <SelectItem key={type.id} value={type.id}>
                              {type.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="edit-maintenanceType">Maintenance Type</Label>
                      <Select
                        value={selectedOrder.maintenanceTypeId || 'none'}
                        onValueChange={(value) => setSelectedOrder({
                          ...selectedOrder,
                          maintenanceTypeId: value === 'none' ? undefined : value
                        })}
                        disabled={loadingData}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder={loadingData ? "Loading..." : "Select type"} />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">Not Selected</SelectItem>
                          {maintenanceTypes.map((type) => (
                            <SelectItem key={type.id} value={type.id}>
                              {type.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="edit-priority">Priority Level</Label>
                      <Select
                        value={selectedOrder.priorityLevelId || 'none'}
                        onValueChange={(value) => setSelectedOrder({
                          ...selectedOrder,
                          priorityLevelId: value === 'none' ? undefined : value
                        })}
                        disabled={loadingData}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder={loadingData ? "Loading..." : "Select priority"} />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">Not Selected</SelectItem>
                          {priorities.map((priority) => (
                            <SelectItem key={priority.id} value={priority.id}>
                              {priority.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="edit-maintenanceLocation">Location</Label>
                      <Select
                        value={selectedOrder.maintenanceLocation || 'Internal'}
                        onValueChange={(value) => setSelectedOrder({
                          ...selectedOrder,
                          maintenanceLocation: value
                        })}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select location" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Internal">Internal</SelectItem>
                          <SelectItem value="External">External</SelectItem>
                          <SelectItem value="Onsite">Onsite</SelectItem>
                          <SelectItem value="Offsite">Offsite</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="edit-dueDate">Due Date</Label>
                      <Input
                        id="edit-dueDate"
                        type="date"
                        value={selectedOrder.requestedCompletionDate ? new Date(selectedOrder.requestedCompletionDate).toISOString().split('T')[0] : ''}
                        onChange={(e) => setSelectedOrder({ ...selectedOrder, requestedCompletionDate: e.target.value })}
                      />
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="edit-estimatedHours">Estimated Hours</Label>
                      <Input
                        id="edit-estimatedHours"
                        type="number"
                        min="0"
                        step="0.5"
                        value={selectedOrder.estimatedHours || 0}
                        onChange={(e) => setSelectedOrder({ ...selectedOrder, estimatedHours: parseFloat(e.target.value) || 0 })}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="edit-estimatedCost">Estimated Cost</Label>
                      <Input
                        id="edit-estimatedCost"
                        type="number"
                        min="0"
                        step="0.01"
                        value={selectedOrder.estimatedCost || 0}
                        onChange={(e) => setSelectedOrder({ ...selectedOrder, estimatedCost: parseFloat(e.target.value) || 0 })}
                      />
                    </div>
                  </div>
                </div>
              </TabsContent>

              {/* Tools Tab */}
              <TabsContent value="tools" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4 py-4">
                  <p className="text-sm text-muted-foreground mb-2">
                    Select tools to allocate to this work order. Only available tools are shown.
                  </p>

                  {/* Warehouse Selection */}
                  <div className="grid grid-cols-12 gap-4 mb-4">
                    <div className="col-span-4 space-y-2">
                      <Label htmlFor="tools-warehouse-select">Warehouse <span className="text-red-500">*</span></Label>
                      <Select
                        value={selectedWarehouse}
                        onValueChange={(value) => setSelectedWarehouse(value)}
                        required
                      >
                        <SelectTrigger id="tools-warehouse-select">
                          <SelectValue placeholder="Select warehouse" />
                        </SelectTrigger>
                        <SelectContent>
                          {warehouses.map((warehouse) => (
                            <SelectItem key={warehouse.id} value={warehouse.id}>
                              {warehouse.code} - {warehouse.name}
                            </SelectItem>
                          ))}
                          {warehouses.length === 0 && (
                            <div className="px-2 py-6 text-center text-sm text-muted-foreground">
                              No warehouses available
                            </div>
                          )}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="col-span-8">
                      <p className="text-sm text-muted-foreground mt-6">
                        {selectedWarehouse
                          ? `Showing tools available in ${warehouses.find(w => w.id === selectedWarehouse)?.name}.`
                          : 'Please select a warehouse to view available tools.'}
                      </p>
                    </div>
                  </div>

                  {/* Search Input */}
                  <div className="relative">
                    <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                    <Input
                      placeholder="Search tools by code, name, or category..."
                      value={toolSearchTerm}
                      onChange={(e) => setToolSearchTerm(e.target.value)}
                      className="pl-9"
                    />
                  </div>

                  {selectedWarehouse ? (
                  <div className="border rounded-md p-3 max-h-96 overflow-y-auto">
                    {warehouseTools
                      .filter(tool => {
                        if (!toolSearchTerm) return true;
                        const search = toolSearchTerm.toLowerCase();
                        return tool.itemCode.toLowerCase().includes(search) ||
                               tool.itemName.toLowerCase().includes(search) ||
                               (tool.categoryName && tool.categoryName.toLowerCase().includes(search));
                      }).length === 0 ? (
                      <p className="text-sm text-muted-foreground p-2">
                        {toolSearchTerm ? 'No tools match your search' : 'No tools available in this warehouse'}
                      </p>
                    ) : (
                      <div className="space-y-2">
                        {warehouseTools
                          .filter(tool => {
                            if (!toolSearchTerm) return true;
                            const search = toolSearchTerm.toLowerCase();
                            return tool.itemCode.toLowerCase().includes(search) ||
                                   tool.itemName.toLowerCase().includes(search) ||
                                   (tool.categoryName && tool.categoryName.toLowerCase().includes(search));
                          })
                          .map((tool) => (
                            <div key={tool.inventoryItemId} className="flex items-center space-x-3 p-2 hover:bg-accent rounded">
                              <input
                                type="checkbox"
                                id={`edit-tool-${tool.inventoryItemId}`}
                                checked={selectedTools.includes(tool.inventoryItemId)}
                                onChange={(e) => {
                                  if (e.target.checked) {
                                    setSelectedTools(prev => [...prev, tool.inventoryItemId]);
                                    // Clear search to show all tools after selection
                                    setToolSearchTerm('');
                                  } else {
                                    setSelectedTools(prev => prev.filter(id => id !== tool.inventoryItemId));
                                  }
                                }}
                                className="rounded"
                              />
                              <label htmlFor={`edit-tool-${tool.inventoryItemId}`} className="text-sm cursor-pointer flex-1">
                                <span className="font-medium">{tool.itemCode}</span> - {tool.itemName}
                                <span className="text-muted-foreground ml-2">({tool.categoryName || 'Uncategorized'})</span>
                                <span className="text-xs text-muted-foreground ml-2">| Stock: {tool.availableStock}</span>
                              </label>
                            </div>
                          ))}
                      </div>
                    )}
                  </div>
                  ) : (
                    <div className="border rounded-md p-6 text-center text-sm text-muted-foreground">
                      Please select a warehouse to view available tools
                    </div>
                  )}
                  {selectedTools.length > 0 && (
                    <p className="text-sm font-medium text-primary">
                      {selectedTools.length} tool(s) selected for allocation
                    </p>
                  )}
                </div>
              </TabsContent>

              {/* Consumables Tab */}
              <TabsContent value="consumables" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4 py-4">
                  <p className="text-sm text-muted-foreground mb-2">
                    Manage consumables for this work order. Only items with available stock are displayed.
                  </p>

                  <div className="space-y-4">
                    <div className="grid grid-cols-12 gap-4">
                      <div className="col-span-4 space-y-2">
                        <Label htmlFor="warehouse-select">Location <span className="text-red-500">*</span></Label>
                        <Select
                          value={selectedWarehouse}
                          onValueChange={(value) => setSelectedWarehouse(value)}
                          required
                        >
                          <SelectTrigger id="warehouse-select">
                            <SelectValue placeholder="Select location" />
                          </SelectTrigger>
                          <SelectContent>
                            {warehouses.map((warehouse) => (
                              <SelectItem key={warehouse.id} value={warehouse.id}>
                                {warehouse.code} - {warehouse.name}
                              </SelectItem>
                            ))}
                            {warehouses.length === 0 && (
                              <div className="px-2 py-6 text-center text-sm text-muted-foreground">
                                No locations available
                              </div>
                            )}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="col-span-8">
                        <p className="text-sm text-muted-foreground mt-6">
                          {selectedWarehouse
                            ? `Showing consumables available in ${warehouses.find(w => w.id === selectedWarehouse)?.name}.`
                            : 'Please select a location to view available consumables.'}
                        </p>
                      </div>
                    </div>

                    {/* Search input - moved below warehouse dropdown */}
                    <div className="relative">
                      <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                      <Input
                        placeholder="Search consumables by code or name..."
                        value={consumableSearchTerm}
                        onChange={(e) => setConsumableSearchTerm(e.target.value)}
                        className="pl-9"
                      />
                    </div>

                    {selectedWarehouse && (
                      <div className="grid grid-cols-12 gap-4">
                        <div className="col-span-4 space-y-2">
                          <Label htmlFor="consumable-select">Select Consumable <span className="text-red-500">*</span></Label>
                          <Select
                            value={selectedInventoryItem?.id || ''}
                            onValueChange={(value) => {
                              const item = warehouseInventory.find(i => i.inventoryItemId === value);
                              if (item) {
                                // Convert WarehouseInventoryDto to InventoryItemDto format
                                setSelectedInventoryItem({
                                  id: item.inventoryItemId,
                                  itemCode: item.itemCode,
                                  name: item.itemName,
                                  availableStock: item.availableStock,
                                  unitOfMeasure: item.unitOfMeasure,
                                  standardCost: item.unitCost,
                                  category: item.categoryName || ''
                                } as InventoryItemDto);
                              }
                              setConsumableSearchTerm(''); // Clear search after selection
                            }}
                            required
                          >
                            <SelectTrigger id="consumable-select">
                              <SelectValue placeholder="Select a consumable" />
                            </SelectTrigger>
                            <SelectContent className="max-h-[300px]">
                              {warehouseInventory
                                .filter(item => {
                                  if (!consumableSearchTerm) return true;
                                  const search = consumableSearchTerm.toLowerCase();
                                  return item.itemCode.toLowerCase().includes(search) ||
                                         item.itemName.toLowerCase().includes(search);
                                })
                                .map((item) => (
                                  <SelectItem key={item.inventoryItemId} value={item.inventoryItemId}>
                                    <div className="flex justify-between w-full">
                                      <span>{item.itemCode} - {item.itemName}</span>
                                      <span className="text-xs text-muted-foreground ml-2">
                                        Stock: {item.availableStock}
                                      </span>
                                    </div>
                                  </SelectItem>
                                ))}
                              {warehouseInventory.filter(item => {
                                if (!consumableSearchTerm) return true;
                                const search = consumableSearchTerm.toLowerCase();
                                return item.itemCode.toLowerCase().includes(search) ||
                                       item.itemName.toLowerCase().includes(search);
                              }).length === 0 && (
                                <div className="px-2 py-6 text-center text-sm text-muted-foreground">
                                  {consumableSearchTerm ? 'No consumables match your search' : 'No consumables available in this warehouse'}
                                </div>
                              )}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="col-span-2 space-y-2">
                          <Label>Available Stock</Label>
                          <Input
                            value={selectedInventoryItem?.availableStock ? `${selectedInventoryItem.availableStock} ${selectedInventoryItem.unitOfMeasure}` : ''}
                            disabled
                            className="bg-muted"
                          />
                        </div>
                    <div className="col-span-2 space-y-2">
                      <Label>Unit of Measure</Label>
                      <Input
                        value={selectedInventoryItem?.unitOfMeasure || ''}
                        disabled
                        className="bg-muted"
                      />
                    </div>
                    <div className="col-span-2 space-y-2">
                      <Label htmlFor="part-quantity">Quantity <span className="text-red-500">*</span></Label>
                      <Input
                        id="part-quantity"
                        type="number"
                        min="1"
                        step="1"
                        value={partQuantity}
                        onChange={(e) => setPartQuantity(parseFloat(e.target.value) || 1)}
                        required
                      />
                    </div>
                    <div className="col-span-2 flex items-end">
                      <Button
                        onClick={() => {
                          if (!selectedInventoryItem) return;
                          if (partQuantity < 1) return;
                          if (!selectedWarehouse) {
                            toast({
                              title: 'Error',
                              description: 'Please select a warehouse',
                              variant: 'destructive',
                            });
                            return;
                          }

                          if (editingPart) {
                            // Update existing part in local state
                            setWorkOrderParts(prev => prev.map(part =>
                              part.id === editingPart.id
                                ? {
                                    ...part,
                                    inventoryItemId: selectedInventoryItem.id,
                                    itemCode: selectedInventoryItem.itemCode,
                                    itemName: selectedInventoryItem.name,
                                    quantityRequired: partQuantity,
                                    unitCost: selectedInventoryItem.standardCost,
                                    totalCost: partQuantity * selectedInventoryItem.standardCost,
                                    notes: partNotes
                                  }
                                : part
                            ));
                            setEditingPart(null);
                          } else {
                            // Add new part to local state
                            const warehouse = warehouses.find(w => w.id === selectedWarehouse);
                            const newPart: WorkOrderPartDto = {
                              id: `temp-${Date.now()}`, // Temporary ID
                              workOrderId: selectedOrder?.id || '',
                              inventoryItemId: selectedInventoryItem.id,
                              itemCode: selectedInventoryItem.itemCode,
                              itemName: selectedInventoryItem.name,
                              quantityRequired: partQuantity,
                              quantityAllocated: 0,
                              quantityUsed: 0,
                              quantityReturned: 0,
                              unitCost: selectedInventoryItem.standardCost,
                              totalCost: partQuantity * selectedInventoryItem.standardCost,
                              status: 'Pending',
                              notes: partNotes,
                              createdAt: new Date().toISOString(),
                              warehouseId: selectedWarehouse,
                              warehouseName: warehouse?.name,
                              // Store the warehouse ID for saving
                              ...(selectedWarehouse && { _warehouseId: selectedWarehouse })
                            } as any;
                            setWorkOrderParts(prev => [...prev, newPart]);
                          }

                          // Reset form
                          setSelectedInventoryItem(null);
                          setPartQuantity(1);
                          setPartNotes('');
                          // Don't reset warehouse to allow multiple items from same warehouse
                          // setSelectedWarehouse('');
                        }}
                        className="w-full"
                      >
                        {editingPart ? 'Update' : 'Add'}
                      </Button>
                    </div>
                    <div className="col-span-12 space-y-2">
                      <Label htmlFor="part-notes">Notes (Optional)</Label>
                      <Textarea
                        id="part-notes"
                        value={partNotes}
                        onChange={(e) => setPartNotes(e.target.value)}
                        placeholder="Additional notes..."
                        rows={2}
                      />
                    </div>
                  </div>
                  )}

                  {/* Parts Grid */}
                  <div className="border rounded-md">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="w-16">#</TableHead>
                          <TableHead>Item Code</TableHead>
                          <TableHead>Item Name</TableHead>
                          <TableHead>Qty Required</TableHead>
                          <TableHead>Unit Cost</TableHead>
                          <TableHead>Total Cost</TableHead>
                          <TableHead>Actions</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {workOrderParts.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={7} className="text-center text-muted-foreground">
                              No consumables added yet
                            </TableCell>
                          </TableRow>
                        ) : (
                          workOrderParts.map((part, index) => (
                            <TableRow key={part.id}>
                              <TableCell className="text-muted-foreground">{index + 1}</TableCell>
                              <TableCell className="font-medium">{part.itemCode}</TableCell>
                              <TableCell>{part.itemName}</TableCell>
                              <TableCell>{part.quantityRequired}</TableCell>
                              <TableCell>${part.unitCost.toFixed(2)}</TableCell>
                              <TableCell>${part.totalCost.toFixed(2)}</TableCell>
                              <TableCell>
                                <div className="flex gap-2">
                                  <Button
                                    size="icon"
                                    variant="ghost"
                                    onClick={() => {
                                      setEditingPart(part);
                                      const item = inventoryItems.find(i => i.id === part.inventoryItemId);
                                      setSelectedInventoryItem(item || null);
                                      setPartQuantity(part.quantityRequired);
                                      setPartNotes(part.notes || '');
                                    }}
                                  >
                                    <Pencil className="h-4 w-4" />
                                  </Button>
                                  <Button
                                    size="icon"
                                    variant="ghost"
                                    onClick={() => {
                                      // If it's not a temp ID, mark for bulk deletion on save
                                      if (!part.id.startsWith('temp-')) {
                                        setPendingPartDeletes(prev => [...prev, part.id]);
                                      }
                                      // Remove from local state immediately for UI feedback
                                      setWorkOrderParts(prev => prev.filter(p => p.id !== part.id));
                                      toast({
                                        title: 'Success',
                                        description: 'Consumable marked for removal (will be saved on "Save Changes")',
                                        className: 'bg-green-50 border-green-200',
                                      });
                                    }}
                                  >
                                    <Trash2 className="h-4 w-4 text-destructive" />
                                  </Button>
                                </div>
                              </TableCell>
                            </TableRow>
                          ))
                        )}
                      </TableBody>
                      <tfoot>
                        <TableRow className="bg-muted/50 font-semibold">
                          <TableCell colSpan={5} className="text-right">Total:</TableCell>
                          <TableCell>${workOrderParts.reduce((sum, part) => sum + part.totalCost, 0).toFixed(2)}</TableCell>
                          <TableCell></TableCell>
                        </TableRow>
                      </tfoot>
                    </Table>
                  </div>
                </div>
              </div>
              </TabsContent>

              {/* Schedule Tab */}
              <TabsContent value="schedule" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-lg font-semibold flex items-center gap-2">
                      <CalendarClock className="h-5 w-5" />
                      Technician Schedule
                    </h3>
                    <div className="flex gap-2">
                      <Button
                        variant="outline"
                        onClick={async () => {
                          if (!selectedOrder?.id) return;
                          setLoadingSchedules(true);
                          try {
                            const schedules = await maintenanceApiService.getStaffSchedulesByWorkOrder(selectedOrder.id);
                            const enrichedSchedules = enrichSchedulesWithFullNames(schedules || []);
                            setStaffSchedules(enrichedSchedules);
                          } catch (error) {
                            console.error('Error loading schedules:', error);
                            toast({
                              title: 'Error',
                              description: 'Failed to load schedules',
                              variant: 'destructive',
                            });
                          } finally {
                            setLoadingSchedules(false);
                          }
                        }}
                        size="sm"
                      >
                        Refresh
                      </Button>
                      <Button
                        onClick={() => {
                          setEditingSchedule(null);
                          setScheduleForm({
                            technicianId: '',
                            startDateTime: '',
                            endDateTime: '',
                            scheduleType: 'Scheduled',
                            workLocation: '',
                            address: '',
                            requiresTravel: false,
                            transportationType: '',
                            assignedVehicleId: '',
                            notes: '',
                          });
                          setIsScheduleDialogOpen(true);
                        }}
                        size="sm"
                      >
                        <Plus className="h-4 w-4 mr-2" />
                        Add Schedule
                      </Button>
                    </div>
                  </div>

                  {loadingSchedules ? (
                    <div className="text-center py-8">
                      <Clock className="h-8 w-8 animate-spin mx-auto text-muted-foreground mb-2" />
                      <p className="text-sm text-muted-foreground">Loading schedules...</p>
                    </div>
                  ) : staffSchedules.length === 0 ? (
                    <div className="text-center py-8 text-muted-foreground">
                      <CalendarClock className="h-12 w-12 mx-auto mb-2 opacity-20" />
                      <p>No schedules found for this work order</p>
                      <p className="text-sm">Click "Load Schedules" to fetch data</p>
                    </div>
                  ) : (
                    <div className="border rounded-md">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Technician</TableHead>
                            <TableHead>Schedule Type</TableHead>
                            <TableHead>Start Date/Time</TableHead>
                            <TableHead>End Date/Time</TableHead>
                            <TableHead>Location</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead>Actions</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {enrichSchedulesWithFullNames(staffSchedules).map((schedule, index) => (
                            <TableRow key={schedule.id || index}>
                              <TableCell className="font-medium">{schedule.technicianFullName || schedule.technicianName || 'N/A'}</TableCell>
                              <TableCell>
                                <Badge variant="outline">{schedule.scheduleType}</Badge>
                              </TableCell>
                              <TableCell>
                                {schedule.startDateTime ? format(new Date(schedule.startDateTime), 'PPp') : 'N/A'}
                              </TableCell>
                              <TableCell>
                                {schedule.endDateTime ? format(new Date(schedule.endDateTime), 'PPp') : 'N/A'}
                              </TableCell>
                              <TableCell>{schedule.workLocation || 'N/A'}</TableCell>
                              <TableCell>
                                <Badge
                                  variant={schedule.status === 'Completed' ? 'default' : schedule.status === 'InProgress' ? 'secondary' : 'outline'}
                                >
                                  {schedule.status}
                                </Badge>
                              </TableCell>
                              <TableCell>
                                <div className="flex gap-2">
                                  <Button
                                    size="icon"
                                    variant="ghost"
                                    onClick={() => {
                                      setEditingSchedule(schedule);
                                      setScheduleForm({
                                        technicianId: schedule.technicianId,
                                        startDateTime: schedule.startDateTime.substring(0, 16),
                                        endDateTime: schedule.endDateTime.substring(0, 16),
                                        scheduleType: schedule.scheduleType,
                                        workLocation: schedule.workLocation || '',
                                        address: schedule.address || '',
                                        requiresTravel: schedule.requiresTravel,
                                        transportationType: schedule.transportationType || '',
                                        assignedVehicleId: schedule.assignedVehicleId || '',
                                        notes: schedule.notes || '',
                                      });
                                      setIsScheduleDialogOpen(true);
                                    }}
                                  >
                                    <Pencil className="h-4 w-4" />
                                  </Button>
                                  <Button
                                    size="icon"
                                    variant="ghost"
                                    onClick={() => {
                                      if (!schedule.id) return;
                                      // If it's not a temp ID, mark for bulk deletion on save
                                      if (!schedule.id.startsWith('temp-')) {
                                        setPendingScheduleDeletes(prev => [...prev, schedule.id!]);
                                      }
                                      // Remove from local state immediately for UI feedback
                                      setStaffSchedules(prev => prev.filter(s => s.id !== schedule.id));
                                      toast({
                                        title: 'Success',
                                        description: 'Schedule marked for removal (will be saved on "Save Changes")',
                                        className: 'bg-green-50 border-green-200',
                                      });
                                    }}
                                  >
                                    <Trash2 className="h-4 w-4 text-destructive" />
                                  </Button>
                                </div>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  )}
                </div>
              </TabsContent>

              {/* Expenses Tab */}
              <TabsContent value="expenses" className="flex-1 overflow-y-auto mt-4">
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-lg font-semibold flex items-center gap-2">
                      <DollarSign className="h-5 w-5" />
                      Expenses
                    </h3>
                    <div className="flex gap-2">
                      <Button
                        variant="outline"
                        onClick={async () => {
                          if (!selectedOrder?.id) return;
                          setLoadingExpenses(true);
                          try {
                            const [expensesList, total] = await Promise.all([
                              maintenanceApiService.getExpensesByWorkOrder(selectedOrder.id),
                              maintenanceApiService.getTotalExpensesByWorkOrder(selectedOrder.id)
                            ]);
                            setExpenses(expensesList);
                            setTotalExpenses(total);
                          } catch (error) {
                            console.error('Error loading expenses:', error);
                            toast({
                              title: 'Error',
                              description: 'Failed to load expenses',
                              variant: 'destructive',
                            });
                          } finally {
                            setLoadingExpenses(false);
                          }
                        }}
                        size="sm"
                      >
                        Refresh
                      </Button>
                      <Button
                        onClick={() => {
                          setEditingExpense(null);
                          setExpenseReceiptFile(null);
                          setExpenseForm({
                            expenseType: 'Travel',
                            description: '',
                            amount: 0,
                            expenseDate: new Date().toISOString().split('T')[0],
                            mileageDriven: 0,
                            mileageRate: 0,
                            vehicleUsed: '',
                            vendor: '',
                            receiptNumber: '',
                            receiptPath: '',
                            notes: '',
                          });
                          setIsExpenseDialogOpen(true);
                        }}
                        size="sm"
                      >
                        <Plus className="h-4 w-4 mr-2" />
                        Add Expense
                      </Button>
                    </div>
                  </div>

                  {totalExpenses > 0 && (
                    <div className="bg-muted p-4 rounded-md">
                      <div className="flex justify-between items-center">
                        <span className="text-sm font-medium">Total Expenses:</span>
                        <span className="text-xl font-bold">${totalExpenses.toFixed(2)}</span>
                      </div>
                    </div>
                  )}

                  {loadingExpenses ? (
                    <div className="text-center py-8">
                      <Clock className="h-8 w-8 animate-spin mx-auto text-muted-foreground mb-2" />
                      <p className="text-sm text-muted-foreground">Loading expenses...</p>
                    </div>
                  ) : expenses.length === 0 ? (
                    <div className="text-center py-8 text-muted-foreground">
                      <DollarSign className="h-12 w-12 mx-auto mb-2 opacity-20" />
                      <p>No expenses found for this work order</p>
                      <p className="text-sm">Click "Load Expenses" to fetch data</p>
                    </div>
                  ) : (
                    <div className="border rounded-md">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Date</TableHead>
                            <TableHead>Type</TableHead>
                            <TableHead>Description</TableHead>
                            <TableHead>Amount</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead>Actions</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {expenses.map((expense, index) => (
                            <TableRow key={expense.id || index}>
                              <TableCell>
                                {expense.expenseDate ? new Date(expense.expenseDate).toLocaleDateString() : 'N/A'}
                              </TableCell>
                              <TableCell>
                                <Badge variant="outline">{expense.expenseType}</Badge>
                              </TableCell>
                              <TableCell>{expense.description}</TableCell>
                              <TableCell className="font-semibold">${expense.amount.toFixed(2)}</TableCell>
                              <TableCell>
                                <Badge
                                  variant={expense.status === 'Approved' ? 'default' : expense.status === 'Pending' ? 'secondary' : 'outline'}
                                >
                                  {expense.status}
                                </Badge>
                              </TableCell>
                              <TableCell>
                                <div className="flex gap-2">
                                  {expense.status === 'Pending' && (
                                    <Button
                                      size="sm"
                                      variant="default"
                                      onClick={async () => {
                                        if (!expense.id) return;
                                        try {
                                          await maintenanceApiService.approveExpense(expense.id);
                                          // Refresh expenses
                                          if (selectedOrder?.id) {
                                            const [expensesList, total] = await Promise.all([
                                              maintenanceApiService.getExpensesByWorkOrder(selectedOrder.id),
                                              maintenanceApiService.getTotalExpensesByWorkOrder(selectedOrder.id)
                                            ]);
                                            setExpenses(expensesList);
                                            setTotalExpenses(total);
                                          }
                                          toast({
                                            title: 'Success',
                                            description: 'Expense approved successfully',
                                            className: 'bg-green-50 border-green-200',
                                          });
                                        } catch (error) {
                                          console.error('Error approving expense:', error);
                                          toast({
                                            title: 'Error',
                                            description: 'Failed to approve expense',
                                            variant: 'destructive',
                                          });
                                        }
                                      }}
                                    >
                                      <CheckCircle className="h-4 w-4 mr-1" />
                                      Approve
                                    </Button>
                                  )}
                                  {expense.status !== 'Approved' && (
                                    <>
                                      <Button
                                        size="icon"
                                        variant="ghost"
                                        onClick={() => {
                                          setEditingExpense(expense);
                                          setExpenseReceiptFile(null);
                                          setExpenseForm({
                                            expenseType: expense.expenseType,
                                            description: expense.description,
                                            amount: expense.amount,
                                            expenseDate: expense.expenseDate.split('T')[0],
                                            mileageDriven: expense.mileageDriven || 0,
                                            mileageRate: expense.mileageRate || 0,
                                            vehicleUsed: expense.vehicleUsed || '',
                                            vendor: expense.vendor || '',
                                            receiptNumber: expense.receiptNumber || '',
                                            receiptPath: expense.receiptPath || '',
                                            notes: expense.notes || '',
                                          });
                                          setIsExpenseDialogOpen(true);
                                        }}
                                      >
                                        <Pencil className="h-4 w-4" />
                                      </Button>
                                      <Button
                                        size="icon"
                                        variant="ghost"
                                        onClick={() => {
                                          if (!expense.id || !confirm('Delete this expense?')) return;
                                          // Mark for bulk deletion if not a temp item
                                          if (!expense.id.startsWith('temp-')) {
                                            setPendingExpenseDeletes((prev) => [...prev, expense.id]);
                                          }
                                          // Remove from local state
                                          setExpenses((prev) => prev.filter((e) => e.id !== expense.id));
                                          toast({
                                            title: 'Success',
                                            description: 'Expense deleted (will be removed on "Save Changes")',
                                            className: 'bg-green-50 border-green-200',
                                          });
                                        }}
                                      >
                                        <Trash2 className="h-4 w-4 text-destructive" />
                                      </Button>
                                    </>
                                  )}
                                </div>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                        <tfoot>
                          <TableRow className="bg-muted/50 font-semibold">
                            <TableCell colSpan={4} className="text-right">Total:</TableCell>
                            <TableCell className="font-bold">${expenses.reduce((sum, exp) => sum + exp.amount, 0).toFixed(2)}</TableCell>
                            <TableCell></TableCell>
                          </TableRow>
                        </tfoot>
                      </Table>
                    </div>
                  )}
                </div>
              </TabsContent>
            </Tabs>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => {
              setIsEditDialogOpen(false);
              setSelectedOrder(null);
              setSelectedTools([]);
              setPendingPartDeletes([]);
              setPendingScheduleDeletes([]);
              setPendingExpenseDeletes([]);
              setWorkOrderParts([]);
              setStaffSchedules([]);
              setExpenses([]);
            }}>
              Cancel
            </Button>
            <Button onClick={async () => {
              await handleEditWorkOrder();

              const successMessages = [];
              const errors = [];

              // Allocate selected tools in bulk (check for existing allocations first)
              if (selectedTools.length > 0 && selectedOrder?.id) {
                // Get existing allocated tools
                let existingTools: string[] = [];
                try {
                  const existingAllocations = await workOrderToolService.getWorkOrderTools(selectedOrder.id);
                  existingTools = existingAllocations.map(t => t.toolId);
                } catch (error) {
                  console.error('Error fetching existing tools:', error);
                }

                // Only allocate tools that aren't already allocated
                const toolsToAllocate = selectedTools.filter(toolId => !existingTools.includes(toolId));

                if (toolsToAllocate.length > 0) {
                  try {
                    if (!selectedWarehouse) {
                      errors.push('Warehouse must be selected for tool allocation');
                    } else {
                      const toolDtos = toolsToAllocate.map(toolId => ({
                        workOrderId: selectedOrder.id,
                        toolId,
                        warehouseId: selectedWarehouse,
                        isRequired: true,
                        notes: 'Allocated during work order setup'
                      }));

                      const allocated = await workOrderToolService.allocateToolsBulk(selectedOrder.id, toolDtos);
                      successMessages.push(`${allocated.length} tool(s) allocated`);
                    }
                  } catch (error) {
                    console.error('Error allocating tools:', error);
                    errors.push('Failed to allocate tools');
                  }
                }

                if (selectedTools.length > toolsToAllocate.length) {
                  const skippedCount = selectedTools.length - toolsToAllocate.length;
                  successMessages.push(`${skippedCount} tool(s) already allocated`);
                }
                setSelectedTools([]);
              }

              // Delete consumables marked for deletion using bulk endpoint
              if (pendingPartDeletes.length > 0) {
                try {
                  await workOrderPartService.deletePartsBulk(pendingPartDeletes);
                  successMessages.push(`${pendingPartDeletes.length} consumable(s) deleted`);
                  setPendingPartDeletes([]);
                } catch (error) {
                  console.error('Error deleting consumables:', error);
                  errors.push('Failed to delete consumables');
                }
              }

              // Save consumables to backend using bulk endpoint
              if (workOrderParts.length > 0 && selectedOrder?.id) {
                // Filter only new parts (with temp IDs)
                const newParts = workOrderParts.filter(part => part.id.startsWith('temp-'));

                if (newParts.length > 0) {
                  try {
                    const partsToSave = newParts.map((part: any) => ({
                      workOrderId: selectedOrder.id,
                      inventoryItemId: part.inventoryItemId,
                      quantityRequired: part.quantityRequired,
                      unitCost: part.unitCost,
                      warehouseId: part._warehouseId || part.warehouseId,
                      notes: part.notes
                    }));

                    await workOrderPartService.addPartsBulk(selectedOrder.id, partsToSave);
                    successMessages.push(`${newParts.length} consumable(s) saved`);
                  } catch (error) {
                    console.error('Error saving consumables:', error);
                    errors.push('Failed to save consumables');
                  }
                }
              }

              // Delete schedules marked for deletion
              console.log('🗓️ Pending schedule deletes:', pendingScheduleDeletes);
              if (pendingScheduleDeletes.length > 0) {
                try {
                  await Promise.all(pendingScheduleDeletes.map(id => maintenanceApiService.deleteStaffSchedule(id)));
                  successMessages.push(`${pendingScheduleDeletes.length} schedule(s) deleted`);
                  setPendingScheduleDeletes([]);
                } catch (error) {
                  console.error('Error deleting schedules:', error);
                  errors.push('Failed to delete schedules');
                }
              }

              // Save new and updated schedules to backend
              console.log('🗓️ All schedules in state:', staffSchedules);
              if (staffSchedules.length > 0 && selectedOrder?.id) {
                // New schedules (with temp IDs)
                const newSchedules = staffSchedules.filter(schedule => schedule.id.startsWith('temp-'));
                console.log('🗓️ New schedules to save (temp IDs):', newSchedules);

                // Existing schedules that were loaded from DB (need to update)
                const existingSchedules = staffSchedules.filter(schedule => !schedule.id.startsWith('temp-'));
                console.log('🗓️ Existing schedules to update:', existingSchedules);

                // Save new schedules
                if (newSchedules.length > 0) {
                  try {
                    const schedulesToSave = newSchedules.map(schedule => ({
                      workOrderId: selectedOrder.id,
                      technicianId: schedule.technicianId,
                      startDateTime: schedule.startDateTime,
                      endDateTime: schedule.endDateTime,
                      scheduleType: schedule.scheduleType,
                      workLocation: schedule.workLocation,
                      address: schedule.address,
                      requiresTravel: schedule.requiresTravel,
                      transportationType: schedule.transportationType,
                      assignedVehicleId: schedule.assignedVehicleId && schedule.assignedVehicleId !== 'none' ? schedule.assignedVehicleId : null,
                      notes: schedule.notes
                    }));
                    console.log('🗓️ Saving new schedules to backend:', schedulesToSave);

                    await Promise.all(schedulesToSave.map(s => maintenanceApiService.createStaffSchedule(s)));
                    successMessages.push(`${newSchedules.length} schedule(s) saved`);
                  } catch (error) {
                    console.error('Error saving schedules:', error);
                    errors.push('Failed to save schedules');
                  }
                }

                // Update existing schedules
                if (existingSchedules.length > 0) {
                  try {
                    const schedulesToUpdate = existingSchedules.map(schedule => ({
                      id: schedule.id,
                      workOrderId: selectedOrder.id,
                      technicianId: schedule.technicianId,
                      startDateTime: schedule.startDateTime,
                      endDateTime: schedule.endDateTime,
                      scheduleType: schedule.scheduleType,
                      workLocation: schedule.workLocation,
                      address: schedule.address,
                      requiresTravel: schedule.requiresTravel,
                      transportationType: schedule.transportationType,
                      assignedVehicleId: schedule.assignedVehicleId && schedule.assignedVehicleId !== 'none' ? schedule.assignedVehicleId : null,
                      notes: schedule.notes
                    }));
                    console.log('🗓️ Updating existing schedules in backend:', schedulesToUpdate);

                    await Promise.all(schedulesToUpdate.map(s => maintenanceApiService.updateStaffSchedule(s.id, s)));
                    successMessages.push(`${existingSchedules.length} schedule(s) updated`);
                  } catch (error) {
                    console.error('Error updating schedules:', error);
                    errors.push('Failed to update schedules');
                  }
                }
              } else {
                console.log('🗓️ No schedules to save. staffSchedules.length:', staffSchedules.length, 'selectedOrder?.id:', selectedOrder?.id);
              }

              // Delete expenses marked for deletion
              console.log('💰 Pending expense deletes:', pendingExpenseDeletes);
              if (pendingExpenseDeletes.length > 0) {
                try {
                  await Promise.all(pendingExpenseDeletes.map(id => maintenanceApiService.deleteExpense(id)));
                  successMessages.push(`${pendingExpenseDeletes.length} expense(s) deleted`);
                  setPendingExpenseDeletes([]);
                } catch (error) {
                  console.error('Error deleting expenses:', error);
                  errors.push('Failed to delete expenses');
                }
              }

              // Save new and updated expenses to backend
              console.log('💰 All expenses in state:', expenses);
              if (expenses.length > 0 && selectedOrder?.id) {
                // New expenses (with temp IDs)
                const newExpenses = expenses.filter(expense => expense.id.startsWith('temp-'));
                console.log('💰 New expenses to save (temp IDs):', newExpenses);

                // Existing expenses that were loaded from DB (need to update)
                const existingExpenses = expenses.filter(expense => !expense.id.startsWith('temp-'));
                console.log('💰 Existing expenses to update:', existingExpenses);

                // Save new expenses
                if (newExpenses.length > 0) {
                  try {
                    const expensesToSave = newExpenses.map(expense => ({
                      workOrderId: selectedOrder.id,
                      technicianId: expense.technicianId,
                      expenseType: expense.expenseType,
                      description: expense.description,
                      amount: expense.amount,
                      expenseDate: expense.expenseDate,
                      mileageDriven: expense.mileageDriven,
                      mileageRate: expense.mileageRate,
                      vehicleId: expense.vehicleUsed || null, // Send vehicleId to backend
                      vendorName: expense.vendor, // Backend expects vendorName
                      referenceNumber: expense.receiptNumber, // Backend expects referenceNumber
                      receiptPath: expense.receiptPath,
                      location: expense.notes // Backend uses location field, not notes
                    }));
                    console.log('💰 Saving new expenses to backend:', expensesToSave);

                    await Promise.all(expensesToSave.map(e => maintenanceApiService.createExpense(e)));
                    successMessages.push(`${newExpenses.length} expense(s) saved`);
                  } catch (error) {
                    console.error('Error saving expenses:', error);
                    errors.push('Failed to save expenses');
                  }
                }

                // Update existing expenses
                if (existingExpenses.length > 0) {
                  try {
                    const expensesToUpdate = existingExpenses.map(expense => ({
                      id: expense.id,
                      workOrderId: selectedOrder.id,
                      technicianId: expense.technicianId,
                      expenseType: expense.expenseType,
                      description: expense.description,
                      amount: expense.amount,
                      expenseDate: expense.expenseDate,
                      mileageDriven: expense.mileageDriven,
                      mileageRate: expense.mileageRate,
                      vehicleId: expense.vehicleUsed || null, // Send vehicleId to backend
                      vendorName: expense.vendor, // Backend expects vendorName
                      referenceNumber: expense.receiptNumber, // Backend expects referenceNumber
                      receiptPath: expense.receiptPath,
                      location: expense.notes // Backend uses location field, not notes
                    }));
                    console.log('💰 Updating existing expenses in backend:', expensesToUpdate);

                    await Promise.all(expensesToUpdate.map(e => maintenanceApiService.updateExpense(e.id, e)));
                    successMessages.push(`${existingExpenses.length} expense(s) updated`);
                  } catch (error) {
                    console.error('Error updating expenses:', error);
                    errors.push('Failed to update expenses');
                  }
                }
              } else {
                console.log('💰 No expenses to save. expenses.length:', expenses.length, 'selectedOrder?.id:', selectedOrder?.id);
              }

              // Clear local states
              setWorkOrderParts([]);
              setStaffSchedules([]);
              setExpenses([]);

              toast({
                title: errors.length > 0 ? 'Partial Success' : 'Success',
                description: successMessages.length > 0
                  ? `Work order updated. ${successMessages.join(', ')}${errors.length > 0 ? '. ' + errors.join(', ') : ''}`
                  : 'Work order updated successfully',
                className: errors.length > 0 ? 'bg-yellow-50 border-yellow-200' : 'bg-green-50 border-green-200',
              });
            }}>
              Save Changes
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Task Completion Dialog */}
      <ClientOnly>
      <Dialog open={isTaskCompletionDialogOpen} onOpenChange={setIsTaskCompletionDialogOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Complete Task</DialogTitle>
            <DialogDescription>
              Enter actual hours worked and completion notes
            </DialogDescription>
          </DialogHeader>
          {selectedTask && (
            <div className="space-y-4">
              <div>
                <Label className="text-sm font-medium">Task</Label>
                <p className="text-sm text-muted-foreground">{selectedTask.taskName}</p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="actualHours">Actual Hours</Label>
                <Input
                  id="actualHours"
                  type="number"
                  min="0"
                  step="0.25"
                  value={taskActualHours}
                  onChange={(e) => setTaskActualHours(parseFloat(e.target.value) || 0)}
                  placeholder="Enter actual hours worked"
                />
                <p className="text-xs text-muted-foreground">Estimated: {selectedTask.estimatedHours} hours</p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="completionNotes">Completion Notes (Optional)</Label>
                <Textarea
                  id="completionNotes"
                  value={taskCompletionNotes}
                  onChange={(e) => setTaskCompletionNotes(e.target.value)}
                  placeholder="Add any notes about the completed task..."
                  rows={3}
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsTaskCompletionDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleCompleteTaskSubmit}>
              Complete Task
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Tool Checkout Dialog */}
      <ClientOnly>
      <Dialog open={toolCheckoutDialogOpen} onOpenChange={setToolCheckoutDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Checkout Tool</DialogTitle>
            <DialogDescription>
              {selectedWorkOrderTool?.toolName} ({selectedWorkOrderTool?.toolCode})
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label>Expected Return Date</Label>
              <Input
                type="datetime-local"
                id="expectedReturnDate"
                onChange={(e) => {
                  if (selectedWorkOrderTool) {
                    setSelectedWorkOrderTool({
                      ...selectedWorkOrderTool,
                      expectedReturnDate: e.target.value
                    });
                  }
                }}
              />
            </div>
            <div className="space-y-2">
              <Label>Condition</Label>
              <Select defaultValue="Good">
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Good">Good</SelectItem>
                  <SelectItem value="Fair">Fair</SelectItem>
                  <SelectItem value="Damaged">Damaged</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea placeholder="Any notes about this checkout..." />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setToolCheckoutDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={async () => {
                if (selectedWorkOrderTool && selectedOrder?.id) {
                  try {
                    // Get technician ID from staff schedules (ApplicationUser.Id)
                    const technicianId = staffSchedules[0]?.technicianId;

                    if (!technicianId) {
                      toast({
                        title: 'Error',
                        description: 'No technician assigned to this work order',
                        variant: 'destructive',
                      });
                      return;
                    }

                    await workOrderToolService.checkoutTool({
                      workOrderId: selectedOrder.id,
                      toolId: selectedWorkOrderTool.toolId,
                      technicianId: technicianId,
                      expectedReturnDate: selectedWorkOrderTool.expectedReturnDate,
                      conditionOnCheckout: 'Good',
                    });
                    toast({
                      title: 'Success',
                      description: 'Tool checked out successfully',
                      className: 'bg-green-50 border-green-200',
                    });
                    // Reload tools
                    const [toolsData, summaryData] = await Promise.all([
                      workOrderToolService.getWorkOrderTools(selectedOrder.id),
                      workOrderToolService.getToolSummary(selectedOrder.id),
                    ]);
                    setWorkOrderTools(toolsData);
                    setToolSummary(summaryData);
                    setToolCheckoutDialogOpen(false);
                  } catch (error: any) {
                    toast({
                      title: 'Error',
                      description: error.response?.data?.message || 'Failed to checkout tool',
                      variant: 'destructive',
                    });
                  }
                }
              }}
            >
              Checkout
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Tool Return Dialog */}
      <ClientOnly>
      <Dialog open={toolReturnDialogOpen} onOpenChange={setToolReturnDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Return Tool</DialogTitle>
            <DialogDescription>
              {selectedWorkOrderTool?.toolName} ({selectedWorkOrderTool?.toolCode})
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label>Condition on Return</Label>
              <Select defaultValue="Good">
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Good">Good</SelectItem>
                  <SelectItem value="Fair">Fair</SelectItem>
                  <SelectItem value="Damaged">Damaged</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center space-x-2">
              <input
                type="checkbox"
                id="damageReported"
                className="rounded"
              />
              <Label htmlFor="damageReported">Report Damage</Label>
            </div>
            <div className="space-y-2">
              <Label>Return Notes</Label>
              <Textarea placeholder="Any notes about the return..." />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setToolReturnDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={async () => {
                if (selectedWorkOrderTool && selectedOrder?.id) {
                  try {
                    await workOrderToolService.returnTool(
                      selectedOrder.id,
                      selectedWorkOrderTool.toolId,
                      {
                        conditionOnReturn: 'Good',
                        returnNotes: '',
                        damageReported: false,
                      }
                    );
                    toast({
                      title: 'Success',
                      description: 'Tool returned successfully',
                      className: 'bg-green-50 border-green-200',
                    });
                    // Reload tools
                    const [toolsData, summaryData] = await Promise.all([
                      workOrderToolService.getWorkOrderTools(selectedOrder.id),
                      workOrderToolService.getToolSummary(selectedOrder.id),
                    ]);
                    setWorkOrderTools(toolsData);
                    setToolSummary(summaryData);
                    setToolReturnDialogOpen(false);
                  } catch (error: any) {
                    toast({
                      title: 'Error',
                      description: error.response?.data?.message || 'Failed to return tool',
                      variant: 'destructive',
                    });
                  }
                }
              }}
            >
              Return Tool
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Consume Part Dialog */}
      <ClientOnly>
      <Dialog open={isConsumePartDialogOpen} onOpenChange={setIsConsumePartDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Consume Part/Consumable</DialogTitle>
            <DialogDescription>
              {partToConsume?.itemName} ({partToConsume?.itemCode})
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="grid grid-cols-2 gap-4 p-3 bg-muted/50 rounded-lg">
              <div>
                <p className="text-xs text-muted-foreground">Allocated</p>
                <p className="text-lg font-semibold">{partToConsume?.quantityRequired || 0}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Already Used</p>
                <p className="text-lg font-semibold text-green-600">{partToConsume?.quantityUsed || 0}</p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Remaining</p>
                <p className="text-lg font-semibold text-blue-600">
                  {(partToConsume?.quantityRequired || 0) - (partToConsume?.quantityUsed || 0)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">Unit Cost</p>
                <p className="text-lg font-semibold">${partToConsume?.unitCost.toFixed(2) || '0.00'}</p>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="quantityToConsume">Quantity to Consume <span className="text-destructive">*</span></Label>
              <Input
                id="quantityToConsume"
                type="number"
                min={0.01}
                max={(partToConsume?.quantityRequired || 0) - (partToConsume?.quantityUsed || 0)}
                step={0.01}
                value={quantityToConsume}
                onChange={(e) => setQuantityToConsume(parseFloat(e.target.value) || 0)}
              />
              <p className="text-xs text-muted-foreground">
                Max: {(partToConsume?.quantityRequired || 0) - (partToConsume?.quantityUsed || 0)}
              </p>
            </div>

            <div className="p-3 bg-blue-50 border border-blue-200 rounded-lg">
              <p className="text-sm text-blue-900">
                <strong>Note:</strong> Consuming this part will reduce the inventory stock and cannot be undone.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => {
              setIsConsumePartDialogOpen(false);
              setPartToConsume(null);
              setQuantityToConsume(0);
            }}>
              Cancel
            </Button>
            <Button
              onClick={async () => {
                if (partToConsume && quantityToConsume > 0) {
                  try {
                    const newQuantityUsed = partToConsume.quantityUsed + quantityToConsume;

                    // Update via API
                    await workOrderPartService.updatePart(partToConsume.id, {
                      quantityRequired: partToConsume.quantityRequired,
                      quantityUsed: newQuantityUsed,
                      quantityReturned: partToConsume.quantityReturned,
                      unitCost: partToConsume.unitCost,
                      warehouseLocationId: undefined,
                      status: newQuantityUsed >= partToConsume.quantityRequired ? 'Consumed' : 'Partial',
                      notes: partToConsume.notes
                    });

                    toast({
                      title: 'Success',
                      description: `Consumed ${quantityToConsume} units of ${partToConsume.itemName}`,
                      className: 'bg-green-50 border-green-200',
                    });

                    // Reload parts if viewing a work order
                    if (selectedOrder?.id) {
                      const updatedParts = await workOrderPartService.getWorkOrderParts(selectedOrder.id);
                      setWorkOrderParts(updatedParts);
                    }

                    setIsConsumePartDialogOpen(false);
                    setPartToConsume(null);
                    setQuantityToConsume(0);
                  } catch (error: any) {
                    console.error('Error consuming part:', error);
                    toast({
                      title: 'Error',
                      description: error.response?.data?.message || 'Failed to consume part',
                      variant: 'destructive',
                    });
                  }
                }
              }}
              disabled={quantityToConsume <= 0 || quantityToConsume > ((partToConsume?.quantityRequired || 0) - (partToConsume?.quantityUsed || 0))}
            >
              <FlaskConical className="h-4 w-4 mr-2" />
              Consume {quantityToConsume > 0 ? quantityToConsume : ''} Units
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Schedule Dialog */}
      <ClientOnly>
      <Dialog open={isScheduleDialogOpen} onOpenChange={setIsScheduleDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editingSchedule ? 'Edit' : 'Add'} Schedule</DialogTitle>
            <DialogDescription>
              Schedule technician for {selectedOrder?.workOrderNumber}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="schedule-technician">Technician <span className="text-red-500">*</span></Label>
                <Select
                  value={scheduleForm.technicianId}
                  onValueChange={(value) => setScheduleForm({ ...scheduleForm, technicianId: value })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select technician" />
                  </SelectTrigger>
                  <SelectContent>
                    {technicians.map((tech) => (
                      <SelectItem key={tech.id} value={tech.id}>
                        {tech.firstName} {tech.lastName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="schedule-type">Schedule Type</Label>
                <Select
                  value={scheduleForm.scheduleType}
                  onValueChange={(value) => setScheduleForm({ ...scheduleForm, scheduleType: value })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Scheduled">Scheduled</SelectItem>
                    <SelectItem value="Emergency">Emergency</SelectItem>
                    <SelectItem value="OnCall">On Call</SelectItem>
                    <SelectItem value="Overtime">Overtime</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="start-datetime">Start Date/Time <span className="text-red-500">*</span></Label>
                <Input
                  id="start-datetime"
                  type="datetime-local"
                  value={scheduleForm.startDateTime}
                  onChange={(e) => setScheduleForm({ ...scheduleForm, startDateTime: e.target.value })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="end-datetime">End Date/Time <span className="text-red-500">*</span></Label>
                <Input
                  id="end-datetime"
                  type="datetime-local"
                  value={scheduleForm.endDateTime}
                  onChange={(e) => setScheduleForm({ ...scheduleForm, endDateTime: e.target.value })}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="work-location">Work Location</Label>
              <Input
                id="work-location"
                value={scheduleForm.workLocation}
                onChange={(e) => setScheduleForm({ ...scheduleForm, workLocation: e.target.value })}
                placeholder="e.g., Client Site A, Building 3"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="address">Address</Label>
              <Input
                id="address"
                value={scheduleForm.address}
                onChange={(e) => setScheduleForm({ ...scheduleForm, address: e.target.value })}
                placeholder="Full address for off-site work"
              />
            </div>
            <div className="flex items-center space-x-2">
              <input
                type="checkbox"
                id="requires-travel"
                checked={scheduleForm.requiresTravel}
                onChange={(e) => setScheduleForm({ ...scheduleForm, requiresTravel: e.target.checked })}
                className="rounded"
              />
              <Label htmlFor="requires-travel">Requires Travel</Label>
            </div>
            {scheduleForm.requiresTravel && (
              <>
                <div className="space-y-2">
                  <Label htmlFor="transportation-type">Transportation Type</Label>
                  <Select
                    value={scheduleForm.transportationType}
                    onValueChange={(value) => {
                      setScheduleForm({ ...scheduleForm, transportationType: value, assignedVehicleId: '' });
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select transportation" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Company Vehicle">Company Vehicle</SelectItem>
                      <SelectItem value="Personal Vehicle">Personal Vehicle</SelectItem>
                      <SelectItem value="Public Transport">Public Transport</SelectItem>
                      <SelectItem value="Other">Other</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                {scheduleForm.transportationType === 'Company Vehicle' && (
                  <div className="space-y-2">
                    <Label htmlFor="assigned-vehicle">Assigned Vehicle</Label>
                    <Select
                      value={scheduleForm.assignedVehicleId}
                      onValueChange={(value) => setScheduleForm({ ...scheduleForm, assignedVehicleId: value })}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select vehicle" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {availableVehicles.map((vehicle) => (
                          <SelectItem key={vehicle.id} value={vehicle.id}>
                            {vehicle.name} ({vehicle.assetNumber})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <p className="text-xs text-muted-foreground">
                      {availableVehicles.length} available vehicles
                    </p>
                  </div>
                )}
              </>
            )}
            <div className="space-y-2">
              <Label htmlFor="schedule-notes">Notes</Label>
              <Textarea
                id="schedule-notes"
                value={scheduleForm.notes}
                onChange={(e) => setScheduleForm({ ...scheduleForm, notes: e.target.value })}
                placeholder="Additional notes..."
                rows={3}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsScheduleDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => {
                if (!scheduleForm.technicianId || !scheduleForm.startDateTime || !scheduleForm.endDateTime) {
                  toast({
                    title: 'Error',
                    description: 'Please fill in all required fields',
                    variant: 'destructive',
                  });
                  return;
                }

                const technician = technicians.find(t => t.id === scheduleForm.technicianId);
                const vehicle = availableVehicles.find(v => v.id === scheduleForm.assignedVehicleId);
                const newSchedule: MaintenanceStaffSchedule = {
                  id: editingSchedule?.id || `temp-${Date.now()}`,
                  ...scheduleForm,
                  assignedVehicleId: scheduleForm.assignedVehicleId && scheduleForm.assignedVehicleId !== 'none' ? scheduleForm.assignedVehicleId : undefined,
                  vehicleName: vehicle?.name,
                  technicianName: technician ? `${technician.firstName} ${technician.lastName}` : '',
                  status: 'Scheduled',
                  workOrderId: selectedOrder?.id,
                };

                console.log('🗓️ Adding/updating schedule to state:', newSchedule);

                if (editingSchedule) {
                  // Update existing
                  setStaffSchedules(prev => {
                    const updated = prev.map(s => s.id === editingSchedule.id ? newSchedule : s);
                    console.log('🗓️ Updated schedules state:', updated);
                    return updated;
                  });
                } else {
                  // Add new
                  setStaffSchedules(prev => {
                    const updated = [...prev, newSchedule];
                    console.log('🗓️ Updated schedules state:', updated);
                    return updated;
                  });
                }

                toast({
                  title: 'Success',
                  description: `Schedule ${editingSchedule ? 'updated' : 'added'} (will be saved on "Save Changes")`,
                  className: 'bg-green-50 border-green-200',
                });
                setIsScheduleDialogOpen(false);
              }}
            >
              {editingSchedule ? 'Update' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Expense Dialog */}
      <ClientOnly>
      <Dialog open={isExpenseDialogOpen} onOpenChange={setIsExpenseDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editingExpense ? 'Edit' : 'Add'} Expense</DialogTitle>
            <DialogDescription>
              Track expenses for {selectedOrder?.workOrderNumber}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="expense-type">Expense Type <span className="text-red-500">*</span></Label>
                <Select
                  value={expenseForm.expenseType}
                  onValueChange={(value) => setExpenseForm({ ...expenseForm, expenseType: value })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Travel">Travel</SelectItem>
                    <SelectItem value="Fuel">Fuel</SelectItem>
                    <SelectItem value="Meals">Meals</SelectItem>
                    <SelectItem value="Accommodation">Accommodation</SelectItem>
                    <SelectItem value="Materials">Materials</SelectItem>
                    <SelectItem value="Equipment Rental">Equipment Rental</SelectItem>
                    <SelectItem value="Other">Other</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="expense-date">Expense Date <span className="text-red-500">*</span></Label>
                <Input
                  id="expense-date"
                  type="date"
                  value={expenseForm.expenseDate}
                  onChange={(e) => setExpenseForm({ ...expenseForm, expenseDate: e.target.value })}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description <span className="text-red-500">*</span></Label>
              <Input
                id="description"
                value={expenseForm.description}
                onChange={(e) => setExpenseForm({ ...expenseForm, description: e.target.value })}
                placeholder="Brief description of expense"
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="amount">Amount ($) <span className="text-red-500">*</span></Label>
                <Input
                  id="amount"
                  type="number"
                  step="0.01"
                  min="0"
                  value={expenseForm.amount}
                  onChange={(e) => setExpenseForm({ ...expenseForm, amount: parseFloat(e.target.value) || 0 })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="vendor">Vendor</Label>
                <Input
                  id="vendor"
                  value={expenseForm.vendor}
                  onChange={(e) => setExpenseForm({ ...expenseForm, vendor: e.target.value })}
                  placeholder="Vendor/supplier name"
                />
              </div>
            </div>
            {expenseForm.expenseType === 'Travel' && (
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="mileage">Mileage Driven</Label>
                  <Input
                    id="mileage"
                    type="number"
                    step="0.1"
                    min="0"
                    value={expenseForm.mileageDriven}
                    onChange={(e) => setExpenseForm({ ...expenseForm, mileageDriven: parseFloat(e.target.value) || 0 })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="mileage-rate">Mileage Rate ($/mile)</Label>
                  <Input
                    id="mileage-rate"
                    type="number"
                    step="0.01"
                    min="0"
                    value={expenseForm.mileageRate}
                    onChange={(e) => setExpenseForm({ ...expenseForm, mileageRate: parseFloat(e.target.value) || 0 })}
                  />
                </div>
              </div>
            )}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="vehicle-used">Vehicle Used</Label>
                <Select
                  value={expenseForm.vehicleUsed}
                  onValueChange={(value) => setExpenseForm({ ...expenseForm, vehicleUsed: value })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select vehicle" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">None</SelectItem>
                    {availableVehicles.map((vehicle) => (
                      <SelectItem key={vehicle.id} value={vehicle.id}>
                        {vehicle.name} ({vehicle.assetNumber})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {availableVehicles.length} available vehicles
                </p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="receipt-number">Receipt Number</Label>
                <Input
                  id="receipt-number"
                  value={expenseForm.receiptNumber}
                  onChange={(e) => setExpenseForm({ ...expenseForm, receiptNumber: e.target.value })}
                  placeholder="Receipt/invoice number"
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="receipt-file">Receipt/Evidence Upload</Label>
              <Input
                id="receipt-file"
                type="file"
                accept="image/*,.pdf,.doc,.docx,.xls,.xlsx"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) {
                    setExpenseReceiptFile(file);
                  }
                }}
              />
              {expenseReceiptFile && (
                <p className="text-xs text-muted-foreground">
                  Selected: {expenseReceiptFile.name} ({(expenseReceiptFile.size / 1024).toFixed(1)} KB)
                </p>
              )}
              {expenseForm.receiptPath && !expenseReceiptFile && (
                <p className="text-xs text-blue-600">
                  Current receipt: {expenseForm.receiptPath.split('/').pop()}
                </p>
              )}
              <p className="text-xs text-muted-foreground">
                Accepted: Images, PDF, Word, Excel (Max 10MB)
              </p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="expense-notes">Notes</Label>
              <Textarea
                id="expense-notes"
                value={expenseForm.notes}
                onChange={(e) => setExpenseForm({ ...expenseForm, notes: e.target.value })}
                placeholder="Additional notes..."
                rows={2}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsExpenseDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={async () => {
                if (!expenseForm.expenseType || !expenseForm.description || expenseForm.amount <= 0) {
                  toast({
                    title: 'Error',
                    description: 'Please fill in all required fields',
                    variant: 'destructive',
                  });
                  return;
                }

                // Validate file if present
                if (expenseReceiptFile) {
                  const validation = fileUploadService.validateFile(expenseReceiptFile, 10 * 1024 * 1024);
                  if (!validation.isValid) {
                    toast({
                      title: 'Error',
                      description: validation.error,
                      variant: 'destructive',
                    });
                    return;
                  }
                }

                try {
                  // Use local state pattern - don't save to database yet
                  if (editingExpense?.id) {
                    // Update existing expense in local state
                    const updatedExpense = { ...editingExpense, ...expenseForm, workOrderId: selectedOrder?.id || '' };
                    console.log('💰 Updating expense in state:', updatedExpense);
                    setExpenses((prev) => {
                      const updated = prev.map((expense) =>
                        expense.id === editingExpense.id ? updatedExpense : expense
                      );
                      console.log('💰 Updated expenses state:', updated);
                      return updated;
                    });
                  } else {
                    // Add new expense to local state with temp ID
                    const newExpense: MaintenanceExpense = {
                      id: `temp-${Date.now()}`,
                      ...expenseForm,
                      workOrderId: selectedOrder?.id || '',
                      technicianId: selectedOrder?.assignedTechnicianId || '',
                      technicianName: selectedOrder?.assignedTechnician || '',
                      isApproved: false,
                      approvedById: '',
                      approvedByName: '',
                      approvedDate: '',
                    };
                    console.log('💰 Adding new expense to state:', newExpense);
                    setExpenses((prev) => {
                      const updated = [...prev, newExpense];
                      console.log('💰 Updated expenses state:', updated);
                      return updated;
                    });
                  }

                  // Store file temporarily (will upload when saving work order)
                  // Note: File upload will happen in bulk save

                  toast({
                    title: 'Success',
                    description: `Expense ${editingExpense ? 'updated' : 'created'} (will be saved on 'Save Changes')`,
                    className: 'bg-green-50 border-green-200',
                  });
                  setExpenseReceiptFile(null);
                  setIsExpenseDialogOpen(false);
                } catch (error) {
                  console.error('Error saving expense:', error);
                  toast({
                    title: 'Error',
                    description: 'Failed to save expense',
                    variant: 'destructive',
                  });
                }
              }}
            >
              {editingExpense ? 'Update' : 'Create'} Expense
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </ClientOnly>

      {/* Start Work Order Confirmation Dialog */}
      <Dialog open={isStartWorkOrderDialogOpen} onOpenChange={setIsStartWorkOrderDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <HelpCircle className="h-5 w-5 text-blue-500" />
              Start Work Order
            </DialogTitle>
            <DialogDescription>
              Are you sure you want to start this work order?
            </DialogDescription>
          </DialogHeader>

          {workOrderToStart && (
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <p><strong>Work Order:</strong> {workOrderToStart.workOrderNumber}</p>
                <p><strong>Title:</strong> {workOrderToStart.title}</p>
                <p><strong>Asset:</strong> {workOrderToStart.assetName}</p>
                <p><strong>Assigned Technicians:</strong> {getTechnicianNamesFromSchedules(workOrderToStart.id)}</p>
                <p><strong>Priority:</strong> {workOrderToStart.priority}</p>
              </div>

              <div className="bg-blue-50 border border-blue-200 rounded-md p-3 space-y-1">
                <p className="text-sm font-medium text-blue-900">This will:</p>
                <ul className="text-sm text-blue-800 space-y-1 ml-4 list-disc">
                  <li>Mark the work order as "In Progress"</li>
                  <li>Set assigned vehicles to "In Use" status</li>
                  <li>Begin tracking actual hours and costs</li>
                  <li>Record the start timestamp</li>
                </ul>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setIsStartWorkOrderDialogOpen(false);
                setWorkOrderToStart(null);
              }}
            >
              Cancel
            </Button>
            <Button
              onClick={() => {
                if (workOrderToStart) {
                  updateWorkOrderStatus(workOrderToStart.id, 'InProgress');
                  setIsStartWorkOrderDialogOpen(false);
                  setWorkOrderToStart(null);
                }
              }}
            >
              Start Work Order
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Submit for QC Confirmation Dialog */}
      <Dialog open={isSubmitQCDialogOpen} onOpenChange={setIsSubmitQCDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <FlaskConical className="h-5 w-5 text-blue-500" />
              Submit for Quality Control
            </DialogTitle>
            <DialogDescription>
              Are you sure you want to submit this work order for QC inspection?
            </DialogDescription>
          </DialogHeader>

          {workOrderToSubmitQC && (
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <p><strong>Work Order:</strong> {workOrderToSubmitQC.workOrderNumber}</p>
                <p><strong>Title:</strong> {workOrderToSubmitQC.title}</p>
                <p><strong>Asset:</strong> {workOrderToSubmitQC.assetName}</p>
                <p><strong>Assigned Technicians:</strong> {getTechnicianNamesFromSchedules(workOrderToSubmitQC.id)}</p>
                <p><strong>Priority:</strong> {workOrderToSubmitQC.priority}</p>
              </div>

              <div className="bg-blue-50 border border-blue-200 rounded-md p-3 space-y-1">
                <p className="text-sm font-medium text-blue-900">This will:</p>
                <ul className="text-sm text-blue-800 space-y-1 ml-4 list-disc">
                  <li>Validate that all required tasks are completed</li>
                  <li>Submit the work order for quality control inspection</li>
                  <li>Assign the work order to a QC inspector</li>
                  <li>Require QC approval before the work order can be completed</li>
                </ul>
              </div>

              <div className="bg-yellow-50 border border-yellow-200 rounded-md p-3">
                <p className="text-sm font-medium text-yellow-900">Please ensure:</p>
                <ul className="text-sm text-yellow-800 space-y-1 ml-4 list-disc">
                  <li>All required tasks have been completed</li>
                  <li>All work has been properly documented</li>
                  <li>The work area has been cleaned up</li>
                </ul>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setIsSubmitQCDialogOpen(false);
                setWorkOrderToSubmitQC(null);
              }}
            >
              Cancel
            </Button>
            <Button
              onClick={() => {
                if (workOrderToSubmitQC) {
                  handleSubmitForQCInspection(workOrderToSubmitQC.id);
                  setIsSubmitQCDialogOpen(false);
                  setWorkOrderToSubmitQC(null);
                }
              }}
              className="bg-blue-600 hover:bg-blue-700"
            >
              Submit for QC
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

    </div>
  );
}
