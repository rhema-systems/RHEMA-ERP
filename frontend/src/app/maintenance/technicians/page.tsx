'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Search, Eye, Calendar, Clock, User, Star, MapPin, Info, UserCheck } from 'lucide-react';
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

interface Technician {
  id: string;
  name: string;
  email: string;
  phone: string;
  specialization: string;
  certifications: string[];
  status: 'Available' | 'Busy' | 'On Break' | 'Off Duty';
  currentAssignment?: string;
  skillLevel: 'Junior' | 'Senior' | 'Lead' | 'Expert';
  rating: number;
  totalWorkOrders: number;
  completedThisMonth: number;
  location: string;
  shiftStart: string;
  shiftEnd: string;
}

interface Assignment {
  id: string;
  technicianId: string;
  workOrderId: string;
  workOrderTitle: string;
  assetName: string;
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  scheduledDate: string;
  estimatedHours: number;
  status: 'Scheduled' | 'In Progress' | 'Completed' | 'Cancelled';
}

const mockTechnicians: Technician[] = [
  {
    id: '1',
    name: 'John Smith',
    email: 'john.smith@company.com',
    phone: '+1 (555) 123-4567',
    specialization: 'HVAC',
    certifications: ['EPA 608', 'NATE Certified', 'OSHA 10'],
    status: 'Available',
    skillLevel: 'Senior',
    rating: 4.8,
    totalWorkOrders: 156,
    completedThisMonth: 12,
    location: 'Building A',
    shiftStart: '08:00',
    shiftEnd: '17:00',
  },
  {
    id: '2',
    name: 'Mike Johnson',
    email: 'mike.johnson@company.com',
    phone: '+1 (555) 234-5678',
    specialization: 'Electrical',
    certifications: ['Licensed Electrician', 'OSHA 30', 'Arc Flash Safety'],
    status: 'Busy',
    currentAssignment: 'Emergency Generator Repair',
    skillLevel: 'Expert',
    rating: 4.9,
    totalWorkOrders: 203,
    completedThisMonth: 18,
    location: 'Building B',
    shiftStart: '07:00',
    shiftEnd: '16:00',
  },
  {
    id: '3',
    name: 'Sarah Davis',
    email: 'sarah.davis@company.com',
    phone: '+1 (555) 345-6789',
    specialization: 'Plumbing',
    certifications: ['Master Plumber', 'Backflow Prevention', 'Water Quality'],
    status: 'Available',
    skillLevel: 'Lead',
    rating: 4.7,
    totalWorkOrders: 134,
    completedThisMonth: 9,
    location: 'Building C',
    shiftStart: '09:00',
    shiftEnd: '18:00',
  },
  {
    id: '4',
    name: 'Tom Wilson',
    email: 'tom.wilson@company.com',
    phone: '+1 (555) 456-7890',
    specialization: 'General Maintenance',
    certifications: ['OSHA 10', 'First Aid/CPR'],
    status: 'On Break',
    skillLevel: 'Junior',
    rating: 4.3,
    totalWorkOrders: 67,
    completedThisMonth: 8,
    location: 'Building A',
    shiftStart: '08:30',
    shiftEnd: '17:30',
  },
];

const mockAssignments: Assignment[] = [
  {
    id: '1',
    technicianId: '2',
    workOrderId: 'WO-2024-001',
    workOrderTitle: 'Emergency Generator Repair',
    assetName: 'Backup Generator 1',
    priority: 'Critical',
    scheduledDate: '2024-01-19',
    estimatedHours: 6,
    status: 'In Progress',
  },
  {
    id: '2',
    technicianId: '1',
    workOrderId: 'WO-2024-002',
    workOrderTitle: 'HVAC Filter Replacement',
    assetName: 'Building A - HVAC Unit 1',
    priority: 'Medium',
    scheduledDate: '2024-01-20',
    estimatedHours: 2,
    status: 'Scheduled',
  },
  {
    id: '3',
    technicianId: '3',
    workOrderId: 'WO-2024-003',
    workOrderTitle: 'Water Pump Inspection',
    assetName: 'Main Water Pump',
    priority: 'Medium',
    scheduledDate: '2024-01-21',
    estimatedHours: 4,
    status: 'Scheduled',
  },
];

export default function TechniciansPage() {
  const [technicians, setTechnicians] = useState<Technician[]>(mockTechnicians);
  const [filteredTechnicians, setFilteredTechnicians] = useState<Technician[]>(mockTechnicians);
  const [assignments] = useState<Assignment[]>(mockAssignments);
  const [searchTerm, setSearchTerm] = useState('');
  const [specializationFilter, setSpecializationFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [selectedTechnician, setSelectedTechnician] = useState<Technician | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  
  // Simulate fetching data from HR module
  useEffect(() => {
    const fetchTechniciansFromHR = async () => {
      setIsLoading(true);
      try {
        // Simulate API call to HR module
        // In real implementation, this would call /api/hr/employees?role=technician
        setTimeout(() => {
          setTechnicians(mockTechnicians);
          setIsLoading(false);
        }, 1000);
      } catch (error) {
        console.error('Failed to fetch technicians from HR module:', error);
        // Fallback to mock data
        setTechnicians(mockTechnicians);
        setIsLoading(false);
      }
    };
    
    fetchTechniciansFromHR();
  }, []);

  useEffect(() => {
    let filtered = technicians;

    if (searchTerm) {
      filtered = filtered.filter(tech => 
        tech.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        tech.specialization.toLowerCase().includes(searchTerm.toLowerCase()) ||
        tech.location.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (specializationFilter && specializationFilter !== 'all') {
      filtered = filtered.filter(tech => tech.specialization === specializationFilter);
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter(tech => tech.status === statusFilter);
    }

    setFilteredTechnicians(filtered);
  }, [technicians, searchTerm, specializationFilter, statusFilter]);

  const getStatusBadge = (status: Technician['status']) => {
    const colors = {
      'Available': 'bg-green-100 text-green-800',
      'Busy': 'bg-red-100 text-red-800',
      'On Break': 'bg-yellow-100 text-yellow-800',
      'Off Duty': 'bg-gray-100 text-gray-800',
    };

    return (
      <Badge className={colors[status]}>
        {status}
      </Badge>
    );
  };

  const getSkillLevelBadge = (skillLevel: Technician['skillLevel']) => {
    const colors = {
      'Junior': 'bg-blue-100 text-blue-800',
      'Senior': 'bg-purple-100 text-purple-800',
      'Lead': 'bg-orange-100 text-orange-800',
      'Expert': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[skillLevel]}>
        {skillLevel}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: Assignment['priority']) => {
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

  const getTechnicianAssignments = (technicianId: string) => {
    return assignments.filter(assignment => assignment.technicianId === technicianId);
  };

  const renderStarRating = (rating: number) => {
    return (
      <div className="flex items-center space-x-1">
        {[1, 2, 3, 4, 5].map((star) => (
          <Star
            key={star}
            className={`h-4 w-4 ${
              star <= rating ? 'fill-yellow-400 text-yellow-400' : 'text-gray-300'
            }`}
          />
        ))}
        <span className="text-sm text-muted-foreground ml-1">{rating}</span>
      </div>
    );
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Technician Management</h1>
          <p className="text-muted-foreground">
            View technician schedules, assignments, and performance
          </p>
        </div>
      </div>
      
      {/* HR Integration Notice */}
      <Card className="border-blue-200 bg-blue-50">
        <CardContent className="pt-6">
          <div className="flex items-start space-x-3">
            <Info className="h-5 w-5 text-blue-600 mt-0.5" />
            <div>
              <h3 className="font-semibold text-blue-900">HR Module Integration</h3>
              <p className="text-sm text-blue-800 mt-1">
                Technician data is synchronized from the HR module. To add, modify, or manage technician information, 
                please use the <strong>HR Employee Management</strong> section. Changes made there will automatically 
                appear here within a few minutes.
              </p>
              <div className="mt-2">
                <Button variant="outline" size="sm" className="text-blue-700 border-blue-300 hover:bg-blue-100">
                  <UserCheck className="mr-2 h-4 w-4" />
                  Go to HR Management
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

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
            <BreadcrumbPage>Technicians</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Technician Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Technicians</CardTitle>
            <User className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{technicians.length}</div>
            <p className="text-xs text-muted-foreground">
              Active technicians
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Available</CardTitle>
            <User className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">
              {technicians.filter(t => t.status === 'Available').length}
            </div>
            <p className="text-xs text-muted-foreground">
              Ready for assignments
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Currently Working</CardTitle>
            <Clock className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">
              {technicians.filter(t => t.status === 'Busy').length}
            </div>
            <p className="text-xs text-muted-foreground">
              On active assignments
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Rating</CardTitle>
            <Star className="h-4 w-4 text-yellow-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-yellow-600">
              {(technicians.reduce((sum, t) => sum + t.rating, 0) / technicians.length).toFixed(1)}
            </div>
            <p className="text-xs text-muted-foreground">
              Team performance
            </p>
          </CardContent>
        </Card>
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
                  placeholder="Search technicians..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="specialization-filter" className="text-sm">Specialization</Label>
              <Select value={specializationFilter} onValueChange={setSpecializationFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Specializations" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Specializations</SelectItem>
                  <SelectItem value="HVAC">HVAC</SelectItem>
                  <SelectItem value="Electrical">Electrical</SelectItem>
                  <SelectItem value="Plumbing">Plumbing</SelectItem>
                  <SelectItem value="General Maintenance">General</SelectItem>
                  <SelectItem value="Safety">Safety</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="status-filter" className="text-sm">Status</Label>
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="Available">Available</SelectItem>
                  <SelectItem value="Busy">Busy</SelectItem>
                  <SelectItem value="On Break">On Break</SelectItem>
                  <SelectItem value="Off Duty">Off Duty</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Technicians Table */}
      <Card>
        <CardHeader>
          <CardTitle>Technicians ({filteredTechnicians.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Specialization</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Skill Level</TableHead>
                <TableHead>Rating</TableHead>
                <TableHead>Location</TableHead>
                <TableHead>This Month</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredTechnicians.map((technician) => (
                <TableRow key={technician.id}>
                  <TableCell>
                    <div>
                      <p className="font-medium">{technician.name}</p>
                      <p className="text-sm text-muted-foreground">{technician.email}</p>
                    </div>
                  </TableCell>
                  <TableCell>{technician.specialization}</TableCell>
                  <TableCell>
                    <div>
                      {getStatusBadge(technician.status)}
                      {technician.currentAssignment && (
                        <p className="text-xs text-muted-foreground mt-1">
                          {technician.currentAssignment}
                        </p>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>{getSkillLevelBadge(technician.skillLevel)}</TableCell>
                  <TableCell>{renderStarRating(technician.rating)}</TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-1">
                      <MapPin className="h-3 w-3 text-muted-foreground" />
                      <span className="text-sm">{technician.location}</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="text-sm">
                      <p className="font-medium">{technician.completedThisMonth}</p>
                      <p className="text-muted-foreground">completed</p>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedTechnician(technician);
                          setIsViewDialogOpen(true);
                        }}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                      >
                        <Calendar className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* View Technician Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Technician Details</DialogTitle>
            <DialogDescription>
              Complete information and assignments for this technician
            </DialogDescription>
          </DialogHeader>
          {selectedTechnician && (
            <Tabs defaultValue="details" className="space-y-4">
              <TabsList>
                <TabsTrigger value="details">Personal Details</TabsTrigger>
                <TabsTrigger value="assignments">Current Assignments</TabsTrigger>
                <TabsTrigger value="schedule">Schedule</TabsTrigger>
                <TabsTrigger value="performance">Performance</TabsTrigger>
              </TabsList>
              
              <TabsContent value="details" className="space-y-4">
                <div className="grid grid-cols-2 gap-6">
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Name</Label>
                      <p className="text-sm font-medium">{selectedTechnician.name}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Email</Label>
                      <p className="text-sm">{selectedTechnician.email}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Phone</Label>
                      <p className="text-sm">{selectedTechnician.phone}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Specialization</Label>
                      <p className="text-sm">{selectedTechnician.specialization}</p>
                    </div>
                  </div>
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                      <div className="pt-1">{getStatusBadge(selectedTechnician.status)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Skill Level</Label>
                      <div className="pt-1">{getSkillLevelBadge(selectedTechnician.skillLevel)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Rating</Label>
                      <div className="pt-1">{renderStarRating(selectedTechnician.rating)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Location</Label>
                      <p className="text-sm">{selectedTechnician.location}</p>
                    </div>
                  </div>
                </div>
                
                <div className="border-t pt-4">
                  <h4 className="text-sm font-medium mb-4">Certifications</h4>
                  <div className="flex flex-wrap gap-2">
                    {selectedTechnician.certifications.map((cert, index) => (
                      <Badge key={index} variant="outline">
                        {cert}
                      </Badge>
                    ))}
                  </div>
                </div>
              </TabsContent>
              
              <TabsContent value="assignments">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Current Assignments</h4>
                  <div className="space-y-3">
                    {getTechnicianAssignments(selectedTechnician.id).map((assignment) => (
                      <div key={assignment.id} className="border rounded-lg p-4">
                        <div className="flex justify-between items-start">
                          <div>
                            <p className="font-medium text-sm">{assignment.workOrderTitle}</p>
                            <p className="text-sm text-muted-foreground">{assignment.assetName}</p>
                            <div className="flex items-center space-x-4 mt-2 text-xs text-muted-foreground">
                              <span>Due: {new Date(assignment.scheduledDate).toLocaleDateString()}</span>
                              <span>Est: {assignment.estimatedHours}h</span>
                              <span>WO: {assignment.workOrderId}</span>
                            </div>
                          </div>
                          <div className="text-right">
                            {getPriorityBadge(assignment.priority)}
                            <p className="text-xs text-muted-foreground mt-1">{assignment.status}</p>
                          </div>
                        </div>
                      </div>
                    ))}
                    {getTechnicianAssignments(selectedTechnician.id).length === 0 && (
                      <p className="text-sm text-muted-foreground text-center py-8">
                        No current assignments for this technician.
                      </p>
                    )}
                  </div>
                </div>
              </TabsContent>
              
              <TabsContent value="schedule">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Work Schedule</h4>
                  <div className="border rounded-lg p-4">
                    <div className="grid grid-cols-2 gap-4">
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Shift Start</Label>
                        <p className="text-sm">{selectedTechnician.shiftStart}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Shift End</Label>
                        <p className="text-sm">{selectedTechnician.shiftEnd}</p>
                      </div>
                    </div>
                    <div className="mt-4 flex space-x-2">
                      <Button size="sm" variant="outline">
                        <Calendar className="h-4 w-4 mr-2" />
                        View Full Schedule
                      </Button>
                      <Button size="sm" variant="outline">
                        Assign Work Order
                      </Button>
                    </div>
                  </div>
                </div>
              </TabsContent>
              
              <TabsContent value="performance">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Performance Metrics</h4>
                  <div className="grid grid-cols-3 gap-4">
                    <div className="border rounded-lg p-4 text-center">
                      <p className="text-2xl font-bold">{selectedTechnician.totalWorkOrders}</p>
                      <p className="text-sm text-muted-foreground">Total Work Orders</p>
                    </div>
                    <div className="border rounded-lg p-4 text-center">
                      <p className="text-2xl font-bold">{selectedTechnician.completedThisMonth}</p>
                      <p className="text-sm text-muted-foreground">Completed This Month</p>
                    </div>
                    <div className="border rounded-lg p-4 text-center">
                      <p className="text-2xl font-bold">{selectedTechnician.rating}</p>
                      <p className="text-sm text-muted-foreground">Average Rating</p>
                    </div>
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
    </div>
  );
}