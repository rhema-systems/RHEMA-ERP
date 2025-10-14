'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Calendar, DateRange } from '@/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  History,
  Search,
  Filter,
  Calendar as CalendarIcon,
  Clock,
  Users,
  Package,
  FileText,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Eye,
  Download,
  BarChart3,
  TrendingUp,
  DollarSign,
  Wrench,
  MapPin
} from 'lucide-react';
import { format, subDays, subMonths } from 'date-fns';
import { cn } from '@/lib/utils';

// Mock data for maintenance history
const maintenanceHistoryData = [
  {
    id: 1,
    workOrderId: 'WO-2024-001',
    title: 'HVAC Filter Replacement',
    type: 'Preventive',
    assetId: 'HVAC-001',
    assetName: 'Main Building HVAC Unit A',
    location: 'Building A - Roof',
    technician: 'John Smith',
    startDate: '2024-01-15T08:00:00Z',
    completedDate: '2024-01-15T12:30:00Z',
    status: 'Completed',
    priority: 'Medium',
    cost: 450.00,
    laborHours: 4.5,
    description: 'Replaced air filters and cleaned HVAC system',
    partsUsed: ['Air Filter HVAC-F001 x2', 'Cleaning Solution x1'],
    notes: 'System running efficiently after filter replacement',
    rating: 5,
    downtime: 4.5,
    category: 'HVAC'
  },
  {
    id: 2,
    workOrderId: 'WO-2024-002',
    title: 'Elevator Emergency Repair',
    type: 'Emergency',
    assetId: 'ELEV-001',
    assetName: 'Main Elevator A1',
    location: 'Building A - All Floors',
    technician: 'Mike Johnson',
    startDate: '2024-01-14T14:20:00Z',
    completedDate: '2024-01-14T18:45:00Z',
    status: 'Completed',
    priority: 'Critical',
    cost: 1250.00,
    laborHours: 4.25,
    description: 'Emergency repair of elevator door mechanism',
    partsUsed: ['Door Motor Assembly x1', 'Safety Sensor x2'],
    notes: 'Elevator fully operational, passed safety inspection',
    rating: 4,
    downtime: 4.25,
    category: 'Elevator'
  },
  {
    id: 3,
    workOrderId: 'WO-2024-003',
    title: 'Plumbing Leak Fix',
    type: 'Corrective',
    assetId: 'PLUMB-002',
    assetName: 'Water Line B3F',
    location: 'Building B - 3rd Floor',
    technician: 'Sarah Davis',
    startDate: '2024-01-13T09:15:00Z',
    completedDate: '2024-01-13T16:30:00Z',
    status: 'Completed',
    priority: 'High',
    cost: 320.00,
    laborHours: 7.25,
    description: 'Fixed water leak in ceiling pipe',
    partsUsed: ['Pipe Fitting 3/4" x4', 'Sealant x2'],
    notes: 'No further leakage detected after 24hr observation',
    rating: 5,
    downtime: 0,
    category: 'Plumbing'
  },
  {
    id: 4,
    workOrderId: 'WO-2024-004',
    title: 'Generator Routine Maintenance',
    type: 'Preventive',
    assetId: 'GEN-001',
    assetName: 'Backup Generator B2',
    location: 'Building B - Generator Room',
    technician: 'Tom Wilson',
    startDate: '2024-01-12T07:00:00Z',
    completedDate: '2024-01-12T11:00:00Z',
    status: 'Completed',
    priority: 'Medium',
    cost: 275.00,
    laborHours: 4.0,
    description: 'Routine maintenance and load testing',
    partsUsed: ['Engine Oil 5L x1', 'Oil Filter x1', 'Fuel Filter x1'],
    notes: 'Generator tested successfully at full load',
    rating: 5,
    downtime: 0,
    category: 'Electrical'
  },
  {
    id: 5,
    workOrderId: 'WO-2024-005',
    title: 'Fire System Inspection',
    type: 'Inspection',
    assetId: 'FIRE-001',
    assetName: 'Fire Suppression System',
    location: 'Building A - All Areas',
    technician: 'Lisa Brown',
    startDate: '2024-01-11T08:30:00Z',
    completedDate: '2024-01-11T15:00:00Z',
    status: 'Completed',
    priority: 'High',
    cost: 180.00,
    laborHours: 6.5,
    description: 'Comprehensive fire safety system inspection',
    partsUsed: [],
    notes: 'All systems passed inspection, certificate issued',
    rating: 5,
    downtime: 0,
    category: 'Safety'
  }
];

export default function MaintenanceHistoryPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [technicianFilter, setTechnicianFilter] = useState('all');
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [filteredData, setFilteredData] = useState(maintenanceHistoryData);
  const [selectedRecord, setSelectedRecord] = useState(null);
  const [isDetailDialogOpen, setIsDetailDialogOpen] = useState(false);

  useEffect(() => {
    let filtered = maintenanceHistoryData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.workOrderId.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assetName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.technician.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (typeFilter !== 'all') {
      filtered = filtered.filter(item => item.type.toLowerCase() === typeFilter);
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => item.status.toLowerCase() === statusFilter);
    }

    if (categoryFilter !== 'all') {
      filtered = filtered.filter(item => item.category.toLowerCase() === categoryFilter);
    }

    if (technicianFilter !== 'all') {
      filtered = filtered.filter(item => item.technician === technicianFilter);
    }

    if (dateRange?.from && dateRange?.to) {
      filtered = filtered.filter(item => {
        const itemDate = new Date(item.completedDate);
        return itemDate >= dateRange.from! && itemDate <= dateRange.to!;
      });
    }

    setFilteredData(filtered);
  }, [searchTerm, typeFilter, statusFilter, categoryFilter, technicianFilter, dateRange]);

  const getTypeBadge = (type: string) => {
    const colors = {
      'Preventive': 'bg-blue-100 text-blue-800',
      'Corrective': 'bg-yellow-100 text-yellow-800',
      'Emergency': 'bg-red-100 text-red-800',
      'Inspection': 'bg-purple-100 text-purple-800',
    } as any;

    return (
      <Badge className={colors[type] || 'bg-gray-100 text-gray-800'}>
        {type}
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

  const getRatingStars = (rating: number) => {
    return '★'.repeat(rating) + '☆'.repeat(5 - rating);
  };

  const calculateDuration = (start: string, end: string) => {
    const startDate = new Date(start);
    const endDate = new Date(end);
    const diffMs = endDate.getTime() - startDate.getTime();
    const diffHours = Math.round(diffMs / (1000 * 60 * 60) * 10) / 10;
    return diffHours;
  };

  const getTotalCost = () => {
    return filteredData.reduce((sum, item) => sum + item.cost, 0);
  };

  const getTotalLaborHours = () => {
    return filteredData.reduce((sum, item) => sum + item.laborHours, 0);
  };

  const getAverageRating = () => {
    const totalRating = filteredData.reduce((sum, item) => sum + item.rating, 0);
    return Math.round((totalRating / filteredData.length) * 10) / 10;
  };

  const handleViewDetails = (record: any) => {
    setSelectedRecord(record);
    setIsDetailDialogOpen(true);
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Maintenance History</h1>
          <p className="text-muted-foreground">
            Complete record of all maintenance activities and work orders
          </p>
        </div>
        <Button variant="outline">
          <Download className="mr-2 h-4 w-4" />
          Export Report
        </Button>
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
            <BreadcrumbPage>History</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Summary Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{filteredData.length}</p>
                <p className="text-sm text-muted-foreground">Total Records</p>
              </div>
              <History className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">${getTotalCost().toFixed(0)}</p>
                <p className="text-sm text-muted-foreground">Total Cost</p>
              </div>
              <DollarSign className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{getTotalLaborHours().toFixed(1)}h</p>
                <p className="text-sm text-muted-foreground">Labor Hours</p>
              </div>
              <Clock className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{getAverageRating()}/5</p>
                <p className="text-sm text-muted-foreground">Avg Rating</p>
              </div>
              <TrendingUp className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(item => item.status === 'Completed').length}
                </p>
                <p className="text-sm text-muted-foreground">Completed</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="history" className="space-y-4">
        <TabsList>
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="analytics">Analytics</TabsTrigger>
        </TabsList>

        <TabsContent value="history" className="space-y-4">
          {/* Filters */}
          <Card>
            <CardHeader>
              <CardTitle>Filters</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6 gap-4">
                <div className="relative">
                  <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    placeholder="Search history..."
                    className="pl-8"
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                  />
                </div>
                
                <Select value={typeFilter} onValueChange={setTypeFilter}>
                  <SelectTrigger>
                    <SelectValue placeholder="Type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Types</SelectItem>
                    <SelectItem value="preventive">Preventive</SelectItem>
                    <SelectItem value="corrective">Corrective</SelectItem>
                    <SelectItem value="emergency">Emergency</SelectItem>
                    <SelectItem value="inspection">Inspection</SelectItem>
                  </SelectContent>
                </Select>

                <Select value={categoryFilter} onValueChange={setCategoryFilter}>
                  <SelectTrigger>
                    <SelectValue placeholder="Category" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Categories</SelectItem>
                    <SelectItem value="hvac">HVAC</SelectItem>
                    <SelectItem value="electrical">Electrical</SelectItem>
                    <SelectItem value="plumbing">Plumbing</SelectItem>
                    <SelectItem value="elevator">Elevator</SelectItem>
                    <SelectItem value="safety">Safety</SelectItem>
                  </SelectContent>
                </Select>

                <Select value={technicianFilter} onValueChange={setTechnicianFilter}>
                  <SelectTrigger>
                    <SelectValue placeholder="Technician" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Technicians</SelectItem>
                    <SelectItem value="John Smith">John Smith</SelectItem>
                    <SelectItem value="Mike Johnson">Mike Johnson</SelectItem>
                    <SelectItem value="Sarah Davis">Sarah Davis</SelectItem>
                    <SelectItem value="Tom Wilson">Tom Wilson</SelectItem>
                    <SelectItem value="Lisa Brown">Lisa Brown</SelectItem>
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
                        <span>Date range</span>
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

                <Button 
                  variant="outline" 
                  onClick={() => {
                    setSearchTerm('');
                    setTypeFilter('all');
                    setCategoryFilter('all');
                    setTechnicianFilter('all');
                    setDateRange(undefined);
                  }}
                >
                  Clear Filters
                </Button>
              </div>
            </CardContent>
          </Card>

          {/* History Records */}
          <Card>
            <CardHeader>
              <CardTitle>Maintenance Records</CardTitle>
              <CardDescription>
                {filteredData.length} record(s) found
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {filteredData.map((record) => (
                  <div key={record.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-start justify-between">
                      <div className="space-y-3 flex-1">
                        <div className="flex items-center space-x-3">
                          <h3 className="font-semibold">{record.title}</h3>
                          <Badge variant="outline">{record.workOrderId}</Badge>
                          {getTypeBadge(record.type)}
                          {getPriorityBadge(record.priority)}
                        </div>
                        
                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                          <div className="flex items-center space-x-2">
                            <Package className="h-4 w-4" />
                            <span>{record.assetName}</span>
                          </div>
                          <div className="flex items-center space-x-2">
                            <MapPin className="h-4 w-4" />
                            <span>{record.location}</span>
                          </div>
                          <div className="flex items-center space-x-2">
                            <Users className="h-4 w-4" />
                            <span>{record.technician}</span>
                          </div>
                          <div className="flex items-center space-x-2">
                            <CalendarIcon className="h-4 w-4" />
                            <span>{new Date(record.completedDate).toLocaleDateString()}</span>
                          </div>
                        </div>
                        
                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm">
                          <div>
                            <span className="font-medium">Duration:</span> {record.laborHours}h
                          </div>
                          <div>
                            <span className="font-medium">Cost:</span> ${record.cost.toFixed(2)}
                          </div>
                          <div>
                            <span className="font-medium">Rating:</span> {getRatingStars(record.rating)}
                          </div>
                          <div>
                            <span className="font-medium">Downtime:</span> {record.downtime}h
                          </div>
                        </div>
                        
                        <p className="text-sm text-muted-foreground">{record.description}</p>
                        
                        {record.partsUsed.length > 0 && (
                          <div className="text-sm">
                            <span className="font-medium">Parts Used:</span> {record.partsUsed.join(', ')}
                          </div>
                        )}
                      </div>
                      
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleViewDetails(record)}>
                          <Eye className="mr-2 h-4 w-4" />
                          View Details
                        </Button>
                        <Button size="sm" variant="outline">
                          <Download className="mr-2 h-4 w-4" />
                          Report
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="analytics" className="space-y-4">
          {/* Analytics would go here - placeholder for now */}
          <Card>
            <CardHeader>
              <CardTitle>Maintenance Analytics</CardTitle>
              <CardDescription>Performance metrics and trends</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="flex items-center justify-center h-64 text-muted-foreground">
                <div className="text-center">
                  <BarChart3 className="h-12 w-12 mx-auto mb-4" />
                  <p>Analytics dashboard coming soon</p>
                  <p className="text-sm">Charts and metrics for maintenance performance</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Detail Dialog */}
      {selectedRecord && (
        <Dialog open={isDetailDialogOpen} onOpenChange={setIsDetailDialogOpen}>
          <DialogContent className="sm:max-w-[700px]">
            <DialogHeader>
              <DialogTitle>{selectedRecord.title}</DialogTitle>
              <DialogDescription>
                Work Order: {selectedRecord.workOrderId}
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-4">
                  <div>
                    <h4 className="font-semibold mb-2">Asset Information</h4>
                    <p><span className="font-medium">Asset:</span> {selectedRecord.assetName}</p>
                    <p><span className="font-medium">Location:</span> {selectedRecord.location}</p>
                    <p><span className="font-medium">Category:</span> {selectedRecord.category}</p>
                  </div>
                  
                  <div>
                    <h4 className="font-semibold mb-2">Work Details</h4>
                    <p><span className="font-medium">Type:</span> {selectedRecord.type}</p>
                    <p><span className="font-medium">Priority:</span> {selectedRecord.priority}</p>
                    <p><span className="font-medium">Technician:</span> {selectedRecord.technician}</p>
                  </div>
                </div>
                
                <div className="space-y-4">
                  <div>
                    <h4 className="font-semibold mb-2">Timeline</h4>
                    <p><span className="font-medium">Started:</span> {new Date(selectedRecord.startDate).toLocaleString()}</p>
                    <p><span className="font-medium">Completed:</span> {new Date(selectedRecord.completedDate).toLocaleString()}</p>
                    <p><span className="font-medium">Duration:</span> {selectedRecord.laborHours} hours</p>
                  </div>
                  
                  <div>
                    <h4 className="font-semibold mb-2">Cost & Performance</h4>
                    <p><span className="font-medium">Total Cost:</span> ${selectedRecord.cost.toFixed(2)}</p>
                    <p><span className="font-medium">Labor Hours:</span> {selectedRecord.laborHours}h</p>
                    <p><span className="font-medium">Rating:</span> {getRatingStars(selectedRecord.rating)}</p>
                  </div>
                </div>
              </div>
              
              <div>
                <h4 className="font-semibold mb-2">Description</h4>
                <p className="text-sm text-muted-foreground">{selectedRecord.description}</p>
              </div>
              
              {selectedRecord.partsUsed.length > 0 && (
                <div>
                  <h4 className="font-semibold mb-2">Parts Used</h4>
                  <ul className="text-sm text-muted-foreground">
                    {selectedRecord.partsUsed.map((part, idx) => (
                      <li key={idx}>• {part}</li>
                    ))}
                  </ul>
                </div>
              )}
              
              <div>
                <h4 className="font-semibold mb-2">Notes</h4>
                <p className="text-sm text-muted-foreground">{selectedRecord.notes}</p>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsDetailDialogOpen(false)}>
                Close
              </Button>
              <Button>
                <Download className="mr-2 h-4 w-4" />
                Export Details
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}