'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, CheckCircle, Clock, ChevronDown, User, ClipboardList, History, FileText } from 'lucide-react';
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
import maintenanceApiService, { Employee, Asset, WorkOrderType, PriorityLevel } from '@/services/maintenanceApiService';
import qualityControlService, { QualityValidationResult } from '@/services/qualityControlService';
import { ClientOnly } from '@/components/ClientOnly';

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
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  type: string; // Work order type name
  createdAt: string;
  requestedCompletionDate?: string;
  actualCompletionDate?: string;
  estimatedHours?: number;
  actualHours?: number;
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
  const [workOrders, setWorkOrders] = useState<WorkOrder[]>([]);
  const [filteredOrders, setFilteredOrders] = useState<WorkOrder[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [priorityFilter, setPriorityFilter] = useState<string>('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState<WorkOrder | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [selectedOrderTasks, setSelectedOrderTasks] = useState<WorkOrderTask[]>([]);
  const [loadingTasks, setLoadingTasks] = useState(false);
  const [qualityValidation, setQualityValidation] = useState<{[key: string]: QualityValidationResult}>({});
  
  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [workOrderTypes, setWorkOrderTypes] = useState<WorkOrderType[]>([]);
  const [priorityLevels, setPriorityLevels] = useState<PriorityLevel[]>([]);
  const [jobCards, setJobCards] = useState<JobCard[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  
  const [newWorkOrder, setNewWorkOrder] = useState({
    title: '',
    description: '',
    assetName: '',
    assignedTechnician: '',
    priority: 'Medium' as const,
    dueDate: '',
    workOrderType: 'Preventive',
    estimatedHours: 0,
    jobCardId: '',
    jobCardNumber: '',
  });

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      try {
        const [techniciansList, assetsResponse, workOrderTypesList, priorityLevelsList] = await Promise.all([
          maintenanceApiService.getTechnicians(),
          maintenanceApiService.getAssets(),
          maintenanceApiService.getWorkOrderTypes(),
          maintenanceApiService.getPriorityLevels()
        ]);
        
        setTechnicians(Array.isArray(techniciansList) ? techniciansList : []);
        setAssets(assetsResponse.items || []);
        setWorkOrderTypes(Array.isArray(workOrderTypesList) ? workOrderTypesList : []);
        setPriorityLevels(Array.isArray(priorityLevelsList) ? priorityLevelsList : []);
        
        // Get approved job cards from API (with error handling)
        try {
          const jobCardsList = await maintenanceApiService.getApprovedJobCards();
          setJobCards(Array.isArray(jobCardsList) ? jobCardsList : []);
        } catch (error) {
          console.error('Error loading job cards:', error);
          setJobCards([]); // Set empty array on error
        }
        
        // Load work orders from API
        const workOrdersResponse = await maintenanceApiService.getWorkOrders();
        setWorkOrders(workOrdersResponse.items);
        setFilteredOrders(workOrdersResponse.items);
      } catch (error) {
        console.error('Error loading data:', error);
      } finally {
        setLoadingData(false);
      }
    };
    
    loadData();
  }, []);

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

    setFilteredOrders(filtered);
  }, [workOrders, searchTerm, statusFilter, priorityFilter]);

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
        requestedCompletionDate: newWorkOrder.dueDate,
        estimatedHours: newWorkOrder.estimatedHours,
        jobCardId: newWorkOrder.jobCardId !== 'none' ? newWorkOrder.jobCardId : undefined,
      };

      const createdWorkOrder = await maintenanceApiService.createWorkOrder(createData);
      
      // Refresh work orders list
      const workOrdersResponse = await maintenanceApiService.getWorkOrders();
      setWorkOrders(workOrdersResponse.items);
      
      setIsCreateDialogOpen(false);
      setNewWorkOrder({
        title: '',
        description: '',
        assetName: '',
        assignedTechnician: '',
        priority: 'Medium',
        dueDate: '',
        workOrderType: 'Preventive',
        estimatedHours: 0,
        jobCardId: '',
        jobCardNumber: '',
      });
    } catch (error) {
      console.error('Error creating work order:', error);
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
    };

    return (
      <Badge className={colors[priority]}>
        {priority}
      </Badge>
    );
  };

  const updateWorkOrderStatus = async (orderId: string, newStatus: WorkOrder['status']) => {
    try {
      // If trying to complete work order, validate quality control first
      if (newStatus === 'Completed') {
        const validation = await validateQualityControl(orderId);
        if (!validation.canComplete) {
          alert(`Cannot complete work order: ${validation.validationFailures.join(', ')}`);
          return;
        }
        
        if (validation.requiresInspectionOfficerApproval) {
          alert('Work order requires inspection officer approval before completion.');
          return;
        }
      }
      
      await maintenanceApiService.updateWorkOrderStatus(orderId, newStatus);
      
      // Refresh work orders list
      const workOrdersResponse = await maintenanceApiService.getWorkOrders();
      setWorkOrders(workOrdersResponse.items);
    } catch (error) {
      console.error('Error updating work order status:', error);
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
      
      <div className="flex items-center justify-between">
        <div></div>
        <ClientOnly>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Create Work Order
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Create New Work Order</DialogTitle>
              <DialogDescription>
                Fill in the details to create a new maintenance work order.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Title</Label>
                  <Input
                    id="title"
                    value={newWorkOrder.title}
                    onChange={(e) => setNewWorkOrder(prev => ({ ...prev, title: e.target.value }))}
                    placeholder="Work order title"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="assetName">Asset</Label>
                  <Select value={newWorkOrder.assetName} onValueChange={(value) => setNewWorkOrder(prev => ({ ...prev, assetName: value }))} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading assets..." : "Select asset"} />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.name}>
                          {asset.name} ({asset.assetNumber})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="jobCard">Job Card (Optional)</Label>
                <Select 
                  value={newWorkOrder.jobCardId || 'none'}
                  onValueChange={(value) => {
                    if (value === 'none') {
                      setNewWorkOrder(prev => ({
                        ...prev,
                        jobCardId: '',
                        jobCardNumber: ''
                      }));
                    } else {
                      const selectedJobCard = jobCards.find(jc => jc.id === value);
                      setNewWorkOrder(prev => ({
                        ...prev,
                        jobCardId: value,
                        jobCardNumber: selectedJobCard?.jobCardNumber || '',
                        title: value && !prev.title ? selectedJobCard?.description || '' : prev.title,
                        assetName: value && !prev.assetName ? selectedJobCard?.assetName || '' : prev.assetName,
                        description: value && !prev.description ? selectedJobCard?.description || '' : prev.description
                      }));
                    }
                  }}
                  disabled={loadingData}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={loadingData ? "Loading job cards..." : "Select job card (optional)"} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">None - Create standalone work order</SelectItem>
                    {jobCards.filter(jc => jc.status === 'Approved').map((jobCard) => (
                      <SelectItem key={jobCard.id} value={jobCard.id}>
                        {jobCard.jobCardNumber} - {jobCard.assetName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={newWorkOrder.description}
                  onChange={(e) => setNewWorkOrder(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Detailed description of the work to be performed"
                  rows={3}
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="assignedTechnician">Assigned Technician</Label>
                  <Select value={newWorkOrder.assignedTechnician} onValueChange={(value) => setNewWorkOrder(prev => ({ ...prev, assignedTechnician: value }))} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading technicians..." : "Select technician"} />
                    </SelectTrigger>
                    <SelectContent>
                      {(Array.isArray(technicians) ? technicians : []).map((technician) => (
                        <SelectItem key={technician.id} value={`${technician.firstName} ${technician.lastName}`}>
                          {technician.firstName} {technician.lastName} - {technician.position}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dueDate">Due Date</Label>
                  <Input
                    id="dueDate"
                    type="date"
                    value={newWorkOrder.dueDate}
                    onChange={(e) => setNewWorkOrder(prev => ({ ...prev, dueDate: e.target.value }))}
                  />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
                  <Select value={newWorkOrder.priority} onValueChange={(value: any) => setNewWorkOrder(prev => ({ ...prev, priority: value }))} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading..." : "Select priority"} />
                    </SelectTrigger>
                    <SelectContent>
                      {(Array.isArray(priorityLevels) ? priorityLevels : []).map((priority) => (
                        <SelectItem key={priority.id} value={priority.name}>
                          {priority.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="workOrderType">Type</Label>
                  <Select value={newWorkOrder.workOrderType} onValueChange={(value) => setNewWorkOrder(prev => ({ ...prev, workOrderType: value }))} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading..." : "Select type"} />
                    </SelectTrigger>
                    <SelectContent>
                      {(Array.isArray(workOrderTypes) ? workOrderTypes : []).map((type) => (
                        <SelectItem key={type.id} value={type.name}>
                          {type.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="estimatedHours">Estimated Hours</Label>
                  <Input
                    id="estimatedHours"
                    type="number"
                    min="0"
                    step="0.5"
                    value={newWorkOrder.estimatedHours}
                    onChange={(e) => setNewWorkOrder(prev => ({ ...prev, estimatedHours: parseFloat(e.target.value) || 0 }))}
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateWorkOrder}>
                Create Work Order
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        </ClientOnly>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="p-4">
          <div className="flex items-center space-x-4">
            <div className="flex-1 max-w-sm">
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
            <div className="space-y-1">
              <Label htmlFor="status-filter" className="text-sm">Status</Label>
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
            </div>
            <div className="space-y-1">
              <Label htmlFor="priority-filter" className="text-sm">Priority</Label>
              <ClientOnly>
              <Select value={priorityFilter} onValueChange={setPriorityFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Priority" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Priority</SelectItem>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Critical">Critical</SelectItem>
                </SelectContent>
              </Select>
              </ClientOnly>
            </div>
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
                <TableHead>Title</TableHead>
                <TableHead>Asset</TableHead>
                <TableHead>Job Card</TableHead>
                <TableHead>Technician</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Priority</TableHead>
                <TableHead>Due Date</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredOrders.map((order) => (
                <TableRow key={order.id}>
                  <TableCell className="font-medium">{order.title}</TableCell>
                  <TableCell>{order.assetName}</TableCell>
                  <TableCell>
                    {order.jobCardNumber ? (
                      <Badge variant="outline" className="text-xs">
                        {order.jobCardNumber}
                      </Badge>
                    ) : (
                      <span className="text-muted-foreground text-sm">-</span>
                    )}
                  </TableCell>
                  <TableCell>{order.assignedTechnicianName}</TableCell>
                  <TableCell>{getStatusBadge(order.status)}</TableCell>
                  <TableCell>{getPriorityBadge(order.priority)}</TableCell>
                  <TableCell>{order.requestedCompletionDate ? new Date(order.requestedCompletionDate).toLocaleDateString() : '-'}</TableCell>
                  <TableCell>{order.type}</TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={async () => {
                          setSelectedOrder(order);
                          setIsViewDialogOpen(true);
                          
                          // Fetch tasks for this work order
                          setLoadingTasks(true);
                          setSelectedOrderTasks([]); // Clear previous tasks
                          try {
                            console.log('Fetching work order details for ID:', order.id);
                            const workOrderDetails = await maintenanceApiService.getWorkOrderById(order.id);
                            console.log('Work order details received:', workOrderDetails);
                            console.log('Tasks in response:', workOrderDetails.tasks);
                            
                            if (workOrderDetails.tasks && Array.isArray(workOrderDetails.tasks)) {
                              setSelectedOrderTasks(workOrderDetails.tasks);
                              console.log('Set tasks state with', workOrderDetails.tasks.length, 'tasks');
                            } else {
                              console.warn('No tasks array in response');
                              setSelectedOrderTasks([]);
                            }
                          } catch (error) {
                            console.error('Error fetching work order tasks:', error);
                            setSelectedOrderTasks([]);
                          } finally {
                            setLoadingTasks(false);
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
                            onClick={() => {
                              // TODO: Add edit functionality
                              console.log('Edit work order:', order.id);
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
                          onClick={() => updateWorkOrderStatus(order.id, 'InProgress')}
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
                            onClick={() => validateQualityControl(order.id).then((validation) => {
                              if (validation.canComplete && !validation.requiresInspectionOfficerApproval) {
                                updateWorkOrderStatus(order.id, 'Completed');
                              } else {
                                alert(`Quality Control Required: ${validation.validationFailures.join(', ')}`);
                              }
                            })}
                            className={qualityValidation[order.id]?.canComplete ? 'bg-green-50 hover:bg-green-100' : 'bg-yellow-50 hover:bg-yellow-100'}
                            title={qualityValidation[order.id] ? 
                              qualityValidation[order.id].canComplete ? 'Ready for completion' : 'Quality control required' 
                              : 'Click to validate quality control'}
                          >
                            {qualityValidation[order.id]?.canComplete ? '✓ Complete' : 'QC & Complete'}
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
        <DialogContent className="max-w-5xl h-[85vh] flex flex-col">
          <DialogHeader>
            <DialogTitle>Work Order Details</DialogTitle>
            <DialogDescription>
              View and manage work order information
            </DialogDescription>
          </DialogHeader>
          {selectedOrder && (
            <Tabs defaultValue="details" className="w-full flex flex-col flex-1 overflow-hidden">
              <TabsList className="grid w-full grid-cols-3 flex-shrink-0">
                <TabsTrigger value="details">
                  <FileText className="h-4 w-4 mr-2" />
                  Details
                </TabsTrigger>
                <TabsTrigger value="tasks">
                  <ClipboardList className="h-4 w-4 mr-2" />
                  Tasks ({selectedOrderTasks.length})
                </TabsTrigger>
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
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Assigned Technician</Label>
                  <p className="text-sm">{selectedOrder.assignedTechnicianName || 'Unassigned'}</p>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                  <div className="pt-1">{getStatusBadge(selectedOrder.status)}</div>
                </div>
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
                  <p className="text-sm">{new Date(selectedOrder.createdAt).toLocaleDateString()}</p>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Due Date</Label>
                  <p className="text-sm">{selectedOrder.requestedCompletionDate ? new Date(selectedOrder.requestedCompletionDate).toLocaleDateString() : 'Not set'}</p>
                </div>
                {selectedOrder.actualCompletionDate && (
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Completed Date</Label>
                    <p className="text-sm">{new Date(selectedOrder.actualCompletionDate).toLocaleDateString()}</p>
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
                      </div>
                    ))}
                  </div>
                )}
              </TabsContent>
              
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
    </div>
  );
}
