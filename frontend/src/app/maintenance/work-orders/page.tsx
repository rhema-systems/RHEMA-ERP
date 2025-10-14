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
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, CheckCircle, Clock } from 'lucide-react';
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
import { maintenanceDataService, Employee, Asset, WorkOrderType, PriorityLevel } from '@/services/maintenanceDataService';

interface WorkOrder {
  id: string;
  title: string;
  description: string;
  assetName: string;
  assignedTechnician: string;
  status: 'Open' | 'InProgress' | 'OnHold' | 'Completed' | 'Cancelled';
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  createdDate: string;
  dueDate: string;
  completedDate?: string;
  workOrderType: string;
  estimatedHours?: number;
  actualHours?: number;
}

const mockWorkOrders: WorkOrder[] = [
  {
    id: '1',
    title: 'HVAC System Maintenance',
    description: 'Replace air filters and check refrigerant levels',
    assetName: 'Building A - HVAC Unit 1',
    assignedTechnician: 'John Smith',
    status: 'InProgress',
    priority: 'Medium',
    createdDate: '2024-01-15',
    dueDate: '2024-01-20',
    workOrderType: 'Preventive',
    estimatedHours: 4,
    actualHours: 2.5,
  },
  {
    id: '2',
    title: 'Emergency Plumbing Repair',
    description: 'Repair burst water pipe in basement',
    assetName: 'Building B - Plumbing System',
    assignedTechnician: 'Mike Johnson',
    status: 'Open',
    priority: 'Critical',
    createdDate: '2024-01-16',
    dueDate: '2024-01-16',
    workOrderType: 'Emergency',
    estimatedHours: 8,
  },
];

export default function WorkOrdersPage() {
  const [workOrders, setWorkOrders] = useState<WorkOrder[]>(mockWorkOrders);
  const [filteredOrders, setFilteredOrders] = useState<WorkOrder[]>(mockWorkOrders);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [priorityFilter, setPriorityFilter] = useState<string>('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState<WorkOrder | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  
  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [workOrderTypes, setWorkOrderTypes] = useState<WorkOrderType[]>([]);
  const [priorityLevels, setPriorityLevels] = useState<PriorityLevel[]>([]);
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
  });

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      try {
        const [techniciansList, assetsList, workOrderTypesList, priorityLevelsList] = await Promise.all([
          maintenanceDataService.getTechnicians(),
          maintenanceDataService.getAssets(),
          maintenanceDataService.getWorkOrderTypes(),
          maintenanceDataService.getPriorityLevels()
        ]);
        
        setTechnicians(techniciansList);
        setAssets(assetsList);
        setWorkOrderTypes(workOrderTypesList);
        setPriorityLevels(priorityLevelsList);
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
        order.assetName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        order.assignedTechnician.toLowerCase().includes(searchTerm.toLowerCase())
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

  const handleCreateWorkOrder = () => {
    const workOrder: WorkOrder = {
      id: (workOrders.length + 1).toString(),
      ...newWorkOrder,
      status: 'Open',
      createdDate: new Date().toISOString().split('T')[0],
    };
    
    setWorkOrders([...workOrders, workOrder]);
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
    });
  };

  const getStatusBadge = (status: WorkOrder['status']) => {
    const variants = {
      'Open': 'default',
      'InProgress': 'secondary',
      'OnHold': 'outline',
      'Completed': 'default',
      'Cancelled': 'destructive',
    } as const;

    const colors = {
      'Open': 'bg-blue-100 text-blue-800',
      'InProgress': 'bg-yellow-100 text-yellow-800',
      'OnHold': 'bg-gray-100 text-gray-800',
      'Completed': 'bg-green-100 text-green-800',
      'Cancelled': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[status]}>
        {status === 'InProgress' ? 'In Progress' : status === 'OnHold' ? 'On Hold' : status}
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

  const updateWorkOrderStatus = (orderId: string, newStatus: WorkOrder['status']) => {
    setWorkOrders(prev => prev.map(order => 
      order.id === orderId 
        ? { 
            ...order, 
            status: newStatus,
            completedDate: newStatus === 'Completed' ? new Date().toISOString().split('T')[0] : undefined
          }
        : order
    ));
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
                        <SelectItem key={asset.id} value={asset.assetName}>
                          {asset.assetName} ({asset.assetCode})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
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
                      {technicians.map((technician) => (
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
                      {priorityLevels.map((priority) => (
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
                      {workOrderTypes.map((type) => (
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
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="Open">Open</SelectItem>
                  <SelectItem value="InProgress">In Progress</SelectItem>
                  <SelectItem value="OnHold">On Hold</SelectItem>
                  <SelectItem value="Completed">Completed</SelectItem>
                  <SelectItem value="Cancelled">Cancelled</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="priority-filter" className="text-sm">Priority</Label>
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
                  <TableCell>{order.assignedTechnician}</TableCell>
                  <TableCell>{getStatusBadge(order.status)}</TableCell>
                  <TableCell>{getPriorityBadge(order.priority)}</TableCell>
                  <TableCell>{new Date(order.dueDate).toLocaleDateString()}</TableCell>
                  <TableCell>{order.workOrderType}</TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedOrder(order);
                          setIsViewDialogOpen(true);
                        }}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                      {order.status === 'Open' && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => updateWorkOrderStatus(order.id, 'InProgress')}
                        >
                          Start
                        </Button>
                      )}
                      {order.status === 'InProgress' && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => updateWorkOrderStatus(order.id, 'Completed')}
                        >
                          Complete
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
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Work Order Details</DialogTitle>
            <DialogDescription>
              View and manage work order information
            </DialogDescription>
          </DialogHeader>
          {selectedOrder && (
            <div className="space-y-4">
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
              <div>
                <Label className="text-sm font-medium text-muted-foreground">Description</Label>
                <p className="text-sm">{selectedOrder.description}</p>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Assigned Technician</Label>
                  <p className="text-sm">{selectedOrder.assignedTechnician}</p>
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
                  <p className="text-sm">{selectedOrder.workOrderType}</p>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Estimated Hours</Label>
                  <p className="text-sm">{selectedOrder.estimatedHours} hrs</p>
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Created Date</Label>
                  <p className="text-sm">{new Date(selectedOrder.createdDate).toLocaleDateString()}</p>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Due Date</Label>
                  <p className="text-sm">{new Date(selectedOrder.dueDate).toLocaleDateString()}</p>
                </div>
                {selectedOrder.completedDate && (
                  <div>
                    <Label className="text-sm font-medium text-muted-foreground">Completed Date</Label>
                    <p className="text-sm">{new Date(selectedOrder.completedDate).toLocaleDateString()}</p>
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
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}