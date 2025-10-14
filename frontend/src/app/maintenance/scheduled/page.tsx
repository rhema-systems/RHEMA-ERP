'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Calendar, DateRange } from '@/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { maintenanceDataService, Employee, Asset, MaintenanceType } from '@/services/maintenanceDataService';
import { 
  Calendar as CalendarIcon,
  Clock,
  Plus,
  Search,
  Filter,
  MoreHorizontal,
  Edit,
  Trash2,
  CheckCircle,
  AlertTriangle,
  Users,
  Package,
  RefreshCw
} from 'lucide-react';
import { format, addDays } from 'date-fns';
import { cn } from '@/lib/utils';

// Mock data for scheduled maintenance
const scheduledMaintenanceData = [
  {
    id: 1,
    title: 'HVAC System Quarterly Inspection',
    assetId: 'HVAC-001',
    assetName: 'Main Building HVAC Unit A',
    type: 'Preventive',
    frequency: 'Quarterly',
    nextDue: '2024-02-15',
    lastCompleted: '2023-11-15',
    assignedTechnician: 'John Smith',
    priority: 'Medium',
    status: 'Scheduled',
    estimatedHours: 4,
    description: 'Complete inspection of HVAC system including filters, coils, and electrical components'
  },
  {
    id: 2,
    title: 'Generator Load Test',
    assetId: 'GEN-002',
    assetName: 'Backup Generator B2',
    type: 'Safety',
    frequency: 'Monthly',
    nextDue: '2024-02-10',
    lastCompleted: '2024-01-10',
    assignedTechnician: 'Mike Johnson',
    priority: 'High',
    status: 'Overdue',
    estimatedHours: 2,
    description: 'Monthly load test to ensure generator operates at full capacity'
  },
  {
    id: 3,
    title: 'Elevator Safety Inspection',
    assetId: 'ELEV-001',
    assetName: 'Main Elevator C1',
    type: 'Safety',
    frequency: 'Bi-Annual',
    nextDue: '2024-03-01',
    lastCompleted: '2023-09-01',
    assignedTechnician: 'Sarah Davis',
    priority: 'Critical',
    status: 'Scheduled',
    estimatedHours: 6,
    description: 'Comprehensive safety inspection of elevator systems and components'
  },
  {
    id: 4,
    title: 'Fire Suppression System Check',
    assetId: 'FIRE-001',
    assetName: 'Building Fire System',
    type: 'Safety',
    frequency: 'Monthly',
    nextDue: '2024-02-20',
    lastCompleted: '2024-01-20',
    assignedTechnician: 'Tom Wilson',
    priority: 'High',
    status: 'In Progress',
    estimatedHours: 3,
    description: 'Test fire suppression systems, check pressure levels, and inspect control panels'
  }
];

export default function ScheduledMaintenancePage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [filteredData, setFilteredData] = useState(scheduledMaintenanceData);
  
  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<MaintenanceType[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  
  // Form state for creating new scheduled maintenance
  const [formData, setFormData] = useState({
    title: '',
    assetId: '',
    type: '',
    frequency: '',
    nextDue: '',
    assignedTechnician: '',
    priority: 'Medium',
    estimatedHours: '',
    description: ''
  });

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      try {
        const [techniciansList, assetsList, maintenanceTypesList] = await Promise.all([
          maintenanceDataService.getTechnicians(),
          maintenanceDataService.getAssets(),
          maintenanceDataService.getMaintenanceTypes()
        ]);
        
        setTechnicians(techniciansList);
        setAssets(assetsList);
        setMaintenanceTypes(maintenanceTypesList);
      } catch (error) {
        console.error('Error loading data:', error);
      } finally {
        setLoadingData(false);
      }
    };
    
    loadData();
  }, []);

  useEffect(() => {
    let filtered = scheduledMaintenanceData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assetName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assignedTechnician.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => item.status.toLowerCase() === statusFilter);
    }

    if (priorityFilter !== 'all') {
      filtered = filtered.filter(item => item.priority.toLowerCase() === priorityFilter);
    }

    if (typeFilter !== 'all') {
      filtered = filtered.filter(item => item.type.toLowerCase() === typeFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, priorityFilter, typeFilter]);

  const getStatusBadge = (status: string) => {
    const colors = {
      'Scheduled': 'bg-blue-100 text-blue-800',
      'In Progress': 'bg-yellow-100 text-yellow-800',
      'Completed': 'bg-green-100 text-green-800',
      'Overdue': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[status] || 'bg-gray-100 text-gray-800'}>
        {status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>
        {priority}
      </Badge>
    );
  };

  const handleCreateSchedule = () => {
    // Implementation for creating new scheduled maintenance
    console.log('Creating new scheduled maintenance:', formData);
    setIsCreateDialogOpen(false);
    // Reset form
    setFormData({
      title: '',
      assetId: '',
      type: '',
      frequency: '',
      nextDue: '',
      assignedTechnician: '',
      priority: 'Medium',
      estimatedHours: '',
      description: ''
    });
  };

  const handleCompleteSchedule = (id: number) => {
    console.log('Completing scheduled maintenance:', id);
    // Implementation for completing scheduled maintenance
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Scheduled Maintenance</h1>
          <p className="text-muted-foreground">
            Manage preventive and scheduled maintenance tasks
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Schedule Maintenance
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Schedule New Maintenance</DialogTitle>
              <DialogDescription>
                Create a new scheduled maintenance task for an asset.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Title</Label>
                  <Input
                    id="title"
                    value={formData.title}
                    onChange={(e) => setFormData({...formData, title: e.target.value})}
                    placeholder="Maintenance task title"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="assetId">Asset</Label>
                  <Select value={formData.assetId} onValueChange={(value) => setFormData({...formData, assetId: value})} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading assets..." : "Select asset"} />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.assetName} ({asset.assetCode})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="type">Type</Label>
                  <Select value={formData.type} onValueChange={(value) => setFormData({...formData, type: value})} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading types..." : "Select type"} />
                    </SelectTrigger>
                    <SelectContent>
                      {maintenanceTypes.map((type) => (
                        <SelectItem key={type.id} value={type.name}>
                          {type.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="frequency">Frequency</Label>
                  <Select value={formData.frequency} onValueChange={(value) => setFormData({...formData, frequency: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select frequency" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Daily">Daily</SelectItem>
                      <SelectItem value="Weekly">Weekly</SelectItem>
                      <SelectItem value="Monthly">Monthly</SelectItem>
                      <SelectItem value="Quarterly">Quarterly</SelectItem>
                      <SelectItem value="Bi-Annual">Bi-Annual</SelectItem>
                      <SelectItem value="Annual">Annual</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
                  <Select value={formData.priority} onValueChange={(value) => setFormData({...formData, priority: value})}>
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
              </div>
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="nextDue">Next Due Date</Label>
                  <Input
                    id="nextDue"
                    type="date"
                    value={formData.nextDue}
                    onChange={(e) => setFormData({...formData, nextDue: e.target.value})}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="technician">Assigned Technician</Label>
                  <Select value={formData.assignedTechnician} onValueChange={(value) => setFormData({...formData, assignedTechnician: value})} disabled={loadingData}>
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
                  <Label htmlFor="estimatedHours">Estimated Hours</Label>
                  <Input
                    id="estimatedHours"
                    type="number"
                    value={formData.estimatedHours}
                    onChange={(e) => setFormData({...formData, estimatedHours: e.target.value})}
                    placeholder="Hours"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Detailed description of the maintenance task..."
                  rows={3}
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateSchedule}>Schedule Maintenance</Button>
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
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Scheduled Maintenance</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search..."
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
                <SelectItem value="scheduled">Scheduled</SelectItem>
                <SelectItem value="in progress">In Progress</SelectItem>
                <SelectItem value="completed">Completed</SelectItem>
                <SelectItem value="overdue">Overdue</SelectItem>
              </SelectContent>
            </Select>

            <Select value={priorityFilter} onValueChange={setPriorityFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Priority" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Priorities</SelectItem>
                <SelectItem value="low">Low</SelectItem>
                <SelectItem value="medium">Medium</SelectItem>
                <SelectItem value="high">High</SelectItem>
                <SelectItem value="critical">Critical</SelectItem>
              </SelectContent>
            </Select>

            <Select value={typeFilter} onValueChange={setTypeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="preventive">Preventive</SelectItem>
                <SelectItem value="safety">Safety</SelectItem>
                <SelectItem value="inspection">Inspection</SelectItem>
                <SelectItem value="calibration">Calibration</SelectItem>
              </SelectContent>
            </Select>

            <Popover>
              <PopoverTrigger asChild>
                <Button variant="outline" className="justify-start text-left font-normal">
                  <CalendarIcon className="mr-2 h-4 w-4" />
                  {dateRange?.from ? (
                    dateRange.to ? (
                      <>
                        {format(dateRange.from, "LLL dd, y")} -{" "}
                        {format(dateRange.to, "LLL dd, y")}
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
                <Calendar
                  initialFocus
                  mode="range"
                  defaultMonth={dateRange?.from}
                  selected={dateRange}
                  onSelect={setDateRange}
                  numberOfMonths={2}
                />
              </PopoverContent>
            </Popover>
          </div>
        </CardContent>
      </Card>

      {/* Scheduled Maintenance List */}
      <Card>
        <CardHeader>
          <CardTitle>Scheduled Maintenance Tasks</CardTitle>
          <CardDescription>
            {filteredData.length} scheduled maintenance task(s)
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {filteredData.map((item) => (
              <div key={item.id} className="border rounded-lg p-4">
                <div className="flex items-start justify-between">
                  <div className="space-y-2 flex-1">
                    <div className="flex items-center space-x-2">
                      <h3 className="font-semibold">{item.title}</h3>
                      {getPriorityBadge(item.priority)}
                      {getStatusBadge(item.status)}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div className="flex items-center space-x-2">
                        <Package className="h-4 w-4" />
                        <span>{item.assetName}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Users className="h-4 w-4" />
                        <span>{item.assignedTechnician}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CalendarIcon className="h-4 w-4" />
                        <span>Due: {new Date(item.nextDue).toLocaleDateString()}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Clock className="h-4 w-4" />
                        <span>{item.estimatedHours} hours</span>
                      </div>
                    </div>
                    
                    <div className="text-sm">
                      <span className="font-medium">Type:</span> {item.type} | 
                      <span className="font-medium"> Frequency:</span> {item.frequency} |
                      <span className="font-medium"> Last Done:</span> {new Date(item.lastCompleted).toLocaleDateString()}
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{item.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    {item.status === 'Scheduled' && (
                      <Button size="sm" onClick={() => handleCompleteSchedule(item.id)}>
                        <CheckCircle className="mr-2 h-4 w-4" />
                        Start
                      </Button>
                    )}
                    <Button variant="outline" size="sm">
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button variant="outline" size="sm">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

    </div>
  );
}