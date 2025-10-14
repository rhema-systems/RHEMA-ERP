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
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { 
  AlertTriangle,
  Clock,
  Plus,
  Search,
  Phone,
  Zap,
  Users,
  Package,
  MapPin,
  Timer,
  CheckCircle,
  XCircle,
  MoreHorizontal,
  Edit,
  RefreshCw
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { maintenanceDataService, Employee, Asset } from '@/services/maintenanceDataService';

// Mock data for emergency maintenance
const emergencyMaintenanceData = [
  {
    id: 1,
    title: 'Power Outage - Building A',
    assetId: 'ELEC-001',
    assetName: 'Main Electrical Panel A',
    location: 'Building A - Basement',
    severity: 'Critical',
    status: 'Active',
    reportedBy: 'Sarah Johnson',
    reportedAt: '2024-01-15T14:30:00Z',
    assignedTechnician: 'Mike Wilson',
    responseTime: '00:15:00',
    estimatedResolution: '2024-01-15T18:00:00Z',
    description: 'Complete power failure affecting entire Building A. Emergency generator activated.',
    impact: 'All operations in Building A affected, 200+ people impacted',
    priority: 'P1'
  },
  {
    id: 2,
    title: 'Water Leak - Floor 3',
    assetId: 'PLUMB-002',
    assetName: 'Main Water Line 3rd Floor',
    location: 'Building B - 3rd Floor',
    severity: 'High',
    status: 'In Progress',
    reportedBy: 'John Smith',
    reportedAt: '2024-01-15T16:45:00Z',
    assignedTechnician: 'Tom Davis',
    responseTime: '00:08:00',
    estimatedResolution: '2024-01-15T19:30:00Z',
    description: 'Major water leak in ceiling affecting multiple offices',
    impact: '15 offices affected, potential equipment damage',
    priority: 'P2'
  },
  {
    id: 3,
    title: 'HVAC System Failure',
    assetId: 'HVAC-003',
    assetName: 'Central Air Unit C',
    location: 'Building C - Roof',
    severity: 'Medium',
    status: 'Resolved',
    reportedBy: 'Lisa Brown',
    reportedAt: '2024-01-15T09:15:00Z',
    assignedTechnician: 'Sarah Davis',
    responseTime: '00:12:00',
    estimatedResolution: '2024-01-15T15:00:00Z',
    actualResolution: '2024-01-15T14:45:00Z',
    description: 'Complete HVAC failure in Building C, no heating/cooling',
    impact: '50 people affected, temperature control lost',
    priority: 'P3'
  },
  {
    id: 4,
    title: 'Elevator Stuck',
    assetId: 'ELEV-001',
    assetName: 'Main Elevator A1',
    location: 'Building A - Between Floors 5-6',
    severity: 'High',
    status: 'Active',
    reportedBy: 'Emergency Call',
    reportedAt: '2024-01-15T17:20:00Z',
    assignedTechnician: 'John Smith',
    responseTime: '00:03:00',
    estimatedResolution: '2024-01-15T18:30:00Z',
    description: 'Elevator stuck between floors with 4 people inside',
    impact: '4 people trapped, emergency rescue required',
    priority: 'P1'
  }
];

export default function EmergencyMaintenancePage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [severityFilter, setSeverityFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [filteredData, setFilteredData] = useState(emergencyMaintenanceData);
  
  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  
  // Form state for creating new emergency maintenance
  const [formData, setFormData] = useState({
    title: '',
    assetId: '',
    location: '',
    severity: 'High',
    reportedBy: '',
    description: '',
    impact: '',
    assignedTechnician: ''
  });

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      try {
        const [techniciansList, assetsList] = await Promise.all([
          maintenanceDataService.getTechnicians(),
          maintenanceDataService.getAssets()
        ]);
        
        setTechnicians(techniciansList);
        setAssets(assetsList);
      } catch (error) {
        console.error('Error loading data:', error);
      } finally {
        setLoadingData(false);
      }
    };
    
    loadData();
  }, []);

  useEffect(() => {
    let filtered = emergencyMaintenanceData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assetName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.location.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (severityFilter !== 'all') {
      filtered = filtered.filter(item => item.severity.toLowerCase() === severityFilter);
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => item.status.toLowerCase() === statusFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, severityFilter, statusFilter]);

  const getSeverityBadge = (severity: string) => {
    const colors = {
      'Critical': 'bg-red-100 text-red-800 border-red-200',
      'High': 'bg-orange-100 text-orange-800 border-orange-200',
      'Medium': 'bg-yellow-100 text-yellow-800 border-yellow-200',
      'Low': 'bg-green-100 text-green-800 border-green-200',
    } as any;

    return (
      <Badge className={colors[severity] || 'bg-gray-100 text-gray-800'}>
        <AlertTriangle className="w-3 h-3 mr-1" />
        {severity}
      </Badge>
    );
  };

  const getStatusBadge = (status: string) => {
    const colors = {
      'Active': 'bg-red-100 text-red-800',
      'In Progress': 'bg-yellow-100 text-yellow-800',
      'Resolved': 'bg-green-100 text-green-800',
      'Escalated': 'bg-purple-100 text-purple-800',
    } as any;

    return (
      <Badge className={colors[status] || 'bg-gray-100 text-gray-800'}>
        {status}
      </Badge>
    );
  };

  const getPriorityIcon = (priority: string) => {
    switch (priority) {
      case 'P1':
        return <Zap className="h-4 w-4 text-red-500" />;
      case 'P2':
        return <AlertTriangle className="h-4 w-4 text-orange-500" />;
      default:
        return <Clock className="h-4 w-4 text-yellow-500" />;
    }
  };

  const handleCreateEmergency = () => {
    console.log('Creating new emergency maintenance:', formData);
    setIsCreateDialogOpen(false);
    // Reset form
    setFormData({
      title: '',
      assetId: '',
      location: '',
      severity: 'High',
      reportedBy: '',
      description: '',
      impact: '',
      assignedTechnician: ''
    });
  };

  const formatResponseTime = (timeString: string) => {
    const parts = timeString.split(':');
    const hours = parseInt(parts[0]);
    const minutes = parseInt(parts[1]);
    
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  };

  const getTimeSinceReported = (reportedAt: string) => {
    const reported = new Date(reportedAt);
    const now = new Date();
    const diffMs = now.getTime() - reported.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
    const diffMinutes = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));
    
    if (diffHours > 0) {
      return `${diffHours}h ${diffMinutes}m ago`;
    }
    return `${diffMinutes}m ago`;
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Emergency Maintenance</h1>
          <p className="text-muted-foreground">
            Critical maintenance issues requiring immediate attention
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button className="bg-red-600 hover:bg-red-700">
              <AlertTriangle className="mr-2 h-4 w-4" />
              Report Emergency
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Report Emergency Maintenance</DialogTitle>
              <DialogDescription>
                Report a critical maintenance issue that requires immediate attention.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Emergency Title</Label>
                  <Input
                    id="title"
                    value={formData.title}
                    onChange={(e) => setFormData({...formData, title: e.target.value})}
                    placeholder="Brief description of emergency"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="severity">Severity Level</Label>
                  <Select value={formData.severity} onValueChange={(value) => setFormData({...formData, severity: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select severity" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Critical">Critical - Life/Safety Risk</SelectItem>
                      <SelectItem value="High">High - Major Impact</SelectItem>
                      <SelectItem value="Medium">Medium - Moderate Impact</SelectItem>
                      <SelectItem value="Low">Low - Minor Impact</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="assetId">Affected Asset</Label>
                  <Select value={formData.assetId} onValueChange={(value) => setFormData({...formData, assetId: value})} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading assets..." : "Select asset"} />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.assetCode}>
                          {asset.assetName} ({asset.assetCode})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="location">Location</Label>
                  <Input
                    id="location"
                    value={formData.location}
                    onChange={(e) => setFormData({...formData, location: e.target.value})}
                    placeholder="Specific location of emergency"
                  />
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="reportedBy">Reported By</Label>
                  <Input
                    id="reportedBy"
                    value={formData.reportedBy}
                    onChange={(e) => setFormData({...formData, reportedBy: e.target.value})}
                    placeholder="Your name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="technician">Assign Technician</Label>
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
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Emergency Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Detailed description of the emergency situation..."
                  rows={3}
                />
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="impact">Impact Assessment</Label>
                <Textarea
                  id="impact"
                  value={formData.impact}
                  onChange={(e) => setFormData({...formData, impact: e.target.value})}
                  placeholder="Describe the impact on operations, people, or facilities..."
                  rows={2}
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateEmergency} className="bg-red-600 hover:bg-red-700">
                Report Emergency
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
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Emergency Maintenance</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Emergency Alert */}
      <Alert className="border-red-200 bg-red-50">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>Emergency Response Active</AlertTitle>
        <AlertDescription>
          {filteredData.filter(item => item.status === 'Active').length} active emergencies requiring immediate attention.
          Average response time: 8 minutes.
        </AlertDescription>
      </Alert>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-red-600">
                  {filteredData.filter(item => item.status === 'Active').length}
                </p>
                <p className="text-sm text-muted-foreground">Active Emergencies</p>
              </div>
              <Zap className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-yellow-600">
                  {filteredData.filter(item => item.status === 'In Progress').length}
                </p>
                <p className="text-sm text-muted-foreground">In Progress</p>
              </div>
              <RefreshCw className="h-8 w-8 text-yellow-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-green-600">
                  {filteredData.filter(item => item.status === 'Resolved').length}
                </p>
                <p className="text-sm text-muted-foreground">Resolved Today</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">8m</p>
                <p className="text-sm text-muted-foreground">Avg Response Time</p>
              </div>
              <Timer className="h-8 w-8 text-blue-500" />
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
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search emergencies..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            
            <Select value={severityFilter} onValueChange={setSeverityFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Severity" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Severities</SelectItem>
                <SelectItem value="critical">Critical</SelectItem>
                <SelectItem value="high">High</SelectItem>
                <SelectItem value="medium">Medium</SelectItem>
                <SelectItem value="low">Low</SelectItem>
              </SelectContent>
            </Select>

            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="in progress">In Progress</SelectItem>
                <SelectItem value="resolved">Resolved</SelectItem>
                <SelectItem value="escalated">Escalated</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Emergency Maintenance List */}
      <Card>
        <CardHeader>
          <CardTitle>Emergency Incidents</CardTitle>
          <CardDescription>
            {filteredData.length} emergency maintenance incident(s)
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {filteredData.map((item) => (
              <div key={item.id} className={cn(
                "border rounded-lg p-4",
                item.severity === 'Critical' && "border-red-200 bg-red-50",
                item.severity === 'High' && "border-orange-200 bg-orange-50"
              )}>
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      {getPriorityIcon(item.priority)}
                      <h3 className="font-semibold">{item.title}</h3>
                      {getSeverityBadge(item.severity)}
                      {getStatusBadge(item.status)}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div className="flex items-center space-x-2">
                        <Package className="h-4 w-4" />
                        <span>{item.assetName}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <MapPin className="h-4 w-4" />
                        <span>{item.location}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Users className="h-4 w-4" />
                        <span>{item.assignedTechnician}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Clock className="h-4 w-4" />
                        <span>Response: {formatResponseTime(item.responseTime)}</span>
                      </div>
                    </div>
                    
                    <div className="text-sm">
                      <p><span className="font-medium">Reported by:</span> {item.reportedBy} • {getTimeSinceReported(item.reportedAt)}</p>
                      <p><span className="font-medium">Impact:</span> {item.impact}</p>
                      <p className="text-muted-foreground mt-1">{item.description}</p>
                    </div>

                    {item.estimatedResolution && (
                      <div className="text-sm">
                        <span className="font-medium">
                          {item.status === 'Resolved' ? 'Resolved:' : 'Estimated Resolution:'}
                        </span> {new Date(item.estimatedResolution).toLocaleString()}
                      </div>
                    )}
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    {item.status === 'Active' && (
                      <>
                        <Button size="sm" className="bg-red-600 hover:bg-red-700">
                          <Phone className="mr-2 h-4 w-4" />
                          Call
                        </Button>
                        <Button size="sm" variant="outline">
                          <CheckCircle className="mr-2 h-4 w-4" />
                          Resolve
                        </Button>
                      </>
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