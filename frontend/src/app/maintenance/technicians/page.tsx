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
  status: 'Available' | 'Assigned' | 'Busy' | 'On Break' | 'Off Duty';
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
  endDate?: string;
  estimatedHours: number;
  status: 'Scheduled' | 'In Progress' | 'Completed' | 'Cancelled';
}

interface WorkOrderActivity {
  id: string;
  workOrderNumber?: string;
  title?: string;
  assignedTechnicianId?: string;
  status?: string;
  actualCompletionDate?: string;
  actualEndDate?: string;
  updatedAt?: string;
  createdAt?: string;
}

const isBlockingAssignment = (assignment: Assignment) =>
  assignment.status !== 'Completed' &&
  assignment.status !== 'Cancelled' &&
  (!assignment.endDate || new Date(assignment.endDate) >= new Date());

const normalizeSkillLevel = (value?: string): Technician['skillLevel'] => {
  const normalized = (value || '').toLowerCase();
  if (normalized.includes('expert')) return 'Expert';
  if (normalized.includes('lead')) return 'Lead';
  if (normalized.includes('senior')) return 'Senior';
  return 'Junior';
};

const normalizeAssignmentStatus = (value?: string): Assignment['status'] => {
  const normalized = (value || 'Scheduled').replace(/\s/g, '').toLowerCase();
  if (normalized === 'inprogress') return 'In Progress';
  if (normalized === 'completed') return 'Completed';
  if (normalized === 'cancelled' || normalized === 'canceled') return 'Cancelled';
  return 'Scheduled';
};

const parseDate = (value?: string) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
};

const isCurrentMonth = (date: Date | null) => {
  if (!date) return false;
  const now = new Date();
  return date.getFullYear() === now.getFullYear() && date.getMonth() === now.getMonth();
};

const isCompletedWorkOrder = (status?: string) => {
  const normalized = (status || '').replace(/\s/g, '').toLowerCase();
  return normalized === 'completed' || normalized === 'closed';
};

const getWorkOrderCompletionDate = (workOrder: WorkOrderActivity) =>
  parseDate(workOrder.actualCompletionDate) ||
  parseDate(workOrder.actualEndDate) ||
  parseDate(workOrder.updatedAt) ||
  parseDate(workOrder.createdAt);

const deriveTechnicianMetrics = (
  technicianId: string,
  raw: any,
  assignments: Assignment[],
  workOrders: WorkOrderActivity[]
) => {
  const technicianAssignments = assignments.filter((assignment) => assignment.technicianId === technicianId);
  const assignedWorkOrderIds = new Set(
    technicianAssignments
      .map((assignment) => assignment.workOrderId)
      .filter(Boolean)
  );

  const matchedWorkOrders = workOrders.filter((workOrder) =>
    workOrder.assignedTechnicianId === technicianId || assignedWorkOrderIds.has(workOrder.id)
  );

  const totalWorkOrderIds = new Set<string>();
  matchedWorkOrders.forEach((workOrder) => totalWorkOrderIds.add(workOrder.id));
  assignedWorkOrderIds.forEach((workOrderId) => totalWorkOrderIds.add(workOrderId));

  const completedWorkOrderIds = new Set(
    matchedWorkOrders
      .filter((workOrder) => isCompletedWorkOrder(workOrder.status) && isCurrentMonth(getWorkOrderCompletionDate(workOrder)))
      .map((workOrder) => workOrder.id)
  );

  const completedScheduleOnlyCount = technicianAssignments.filter((assignment) =>
    assignment.status === 'Completed' &&
    isCurrentMonth(parseDate(assignment.endDate || assignment.scheduledDate)) &&
    !completedWorkOrderIds.has(assignment.workOrderId)
  ).length;

  const hasDerivedActivity = assignments.length > 0 || workOrders.length > 0;
  return {
    totalWorkOrders: hasDerivedActivity
      ? totalWorkOrderIds.size
      : Number(raw.totalWorkOrders ?? raw.activeWorkOrdersCount ?? raw.completedWorkOrders ?? 0),
    completedThisMonth: hasDerivedActivity
      ? completedWorkOrderIds.size + completedScheduleOnlyCount
      : Number(raw.completedThisMonth ?? raw.completedWorkOrders ?? 0),
  };
};

const normalizeTechnician = (raw: any, assignments: Assignment[], workOrders: WorkOrderActivity[]): Technician => {
  const id = raw.id || raw.employeeId || '';
  const activeAssignments = assignments
    .filter((assignment) => assignment.technicianId === id && isBlockingAssignment(assignment))
    .sort((a, b) => new Date(a.scheduledDate).getTime() - new Date(b.scheduledDate).getTime());
  const currentOrNextAssignment = activeAssignments[0];
  const isInProgress = activeAssignments.some((assignment) => assignment.status === 'In Progress');
  const metrics = deriveTechnicianMetrics(id, raw, assignments, workOrders);

  return {
    id,
    name: raw.name || raw.fullName || `${raw.firstName || ''} ${raw.lastName || ''}`.trim() || 'Unnamed Technician',
    email: raw.email || raw.emailAddress || '',
    phone: raw.phone || raw.mobileNumber || raw.telephoneNumber || '',
    specialization: raw.specialization || raw.positionTitle || raw.position || 'General Maintenance',
    certifications: Array.isArray(raw.certifications)
      ? raw.certifications
      : raw.certificationLevel
        ? [raw.certificationLevel]
        : [],
    status: isInProgress ? 'Busy' : currentOrNextAssignment ? 'Assigned' : 'Available',
    currentAssignment: currentOrNextAssignment
      ? `${currentOrNextAssignment.workOrderTitle} - ${new Date(currentOrNextAssignment.scheduledDate).toLocaleString()}`
      : undefined,
    skillLevel: normalizeSkillLevel(raw.skillLevel || raw.experienceLevel),
    rating: Number(raw.rating ?? raw.averageRating ?? raw.performanceRating ?? 0),
    totalWorkOrders: metrics.totalWorkOrders,
    completedThisMonth: metrics.completedThisMonth,
    location: raw.location || raw.departmentName || raw.department || 'Maintenance',
    shiftStart: raw.shiftStart || 'N/A',
    shiftEnd: raw.shiftEnd || 'N/A',
  };
};

const mapScheduleToAssignment = (schedule: any): Assignment => {
  const start = schedule.startDateTime || schedule.scheduledDate || new Date().toISOString();
  const end = schedule.endDateTime || schedule.endDate;
  const startTime = new Date(start).getTime();
  const endTime = end ? new Date(end).getTime() : startTime;

  return {
    id: schedule.id,
    technicianId: schedule.technicianId,
    workOrderId: schedule.workOrderId || schedule.jobCardId || schedule.id,
    workOrderTitle: schedule.workOrderNumber || schedule.jobCardNumber || schedule.scheduleType || 'Maintenance assignment',
    assetName: schedule.workLocation || schedule.teamName || schedule.address || 'Maintenance',
    priority: 'Medium',
    scheduledDate: start,
    endDate: end,
    estimatedHours: Math.max(0, (endTime - startTime) / (1000 * 60 * 60)),
    status: normalizeAssignmentStatus(schedule.status),
  };
};


export default function TechniciansPage() {
  const [technicians, setTechnicians] = useState<Technician[]>([]);
  const [filteredTechnicians, setFilteredTechnicians] = useState<Technician[]>([]);
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [specializationFilter, setSpecializationFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [selectedTechnician, setSelectedTechnician] = useState<Technician | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [technicianDialogTab, setTechnicianDialogTab] = useState('details');
  const [isLoading, setIsLoading] = useState(false);
  const specializationOptions = Array.from(new Set(technicians.map((tech) => tech.specialization).filter(Boolean))).sort();
  const assignedOrBusyCount = technicians.filter((tech) => tech.status === 'Assigned' || tech.status === 'Busy').length;
  const averageRating = technicians.length
    ? technicians.reduce((sum, tech) => sum + tech.rating, 0) / technicians.length
    : 0;
  
  // Load technicians from API
  useEffect(() => {
    const fetchTechniciansFromHR = async () => {
      setIsLoading(true);
      try {
        const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
        const token = localStorage.getItem('authToken');
        const rangeStart = new Date();
        rangeStart.setDate(rangeStart.getDate() - 30);
        const rangeEnd = new Date();
        rangeEnd.setDate(rangeEnd.getDate() + 180);

        const fetchWorkOrders = async (): Promise<WorkOrderActivity[]> => {
          const pageSize = 100;
          const allWorkOrders: WorkOrderActivity[] = [];

          for (let page = 1; page <= 10; page += 1) {
            const response = await fetch(`${API_URL}/maintenance/work-orders?page=${page}&pageSize=${pageSize}`, {
              headers: {
                'Authorization': token ? `Bearer ${token}` : '',
                'Content-Type': 'application/json'
              }
            });

            if (!response.ok) break;

            const data = await response.json();
            const items = data.items || data.data || data || [];
            allWorkOrders.push(...items);

            if (!data.hasNext && (!data.totalPages || page >= data.totalPages)) {
              break;
            }
          }

          return allWorkOrders;
        };

        const [techniciansResponse, assignmentsResponse, workOrderList] = await Promise.all([
          fetch(`${API_URL}/employees/maintenance-available`, {
            headers: {
              'Authorization': token ? `Bearer ${token}` : '',
              'Content-Type': 'application/json'
            }
          }),
          fetch(`${API_URL}/maintenance/staff-schedules/by-date-range?startDate=${encodeURIComponent(rangeStart.toISOString())}&endDate=${encodeURIComponent(rangeEnd.toISOString())}`, {
            headers: {
              'Authorization': token ? `Bearer ${token}` : '',
              'Content-Type': 'application/json'
            }
          }),
          fetchWorkOrders()
        ]);
        
        let assignmentList: Assignment[] = [];
        if (assignmentsResponse.ok) {
          const assignmentsData = await assignmentsResponse.json();
          assignmentList = (assignmentsData.data || assignmentsData.items || assignmentsData || [])
            .map(mapScheduleToAssignment);
          setAssignments(assignmentList);
        } else {
          setAssignments([]);
        }

        if (techniciansResponse.ok) {
          const techniciansData = await techniciansResponse.json();
          const technicianItems = techniciansData.data || techniciansData.items || techniciansData || [];
          setTechnicians(technicianItems.map((tech: any) => normalizeTechnician(tech, assignmentList, workOrderList)));
        }
      } catch (error) {
        console.error('Failed to fetch technicians from HR module:', error);
        setTechnicians([]);
        setAssignments([]);
      } finally {
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
      'Assigned': 'bg-blue-100 text-blue-800',
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
    return assignments
      .filter(assignment => assignment.technicianId === technicianId && isBlockingAssignment(assignment))
      .sort((a, b) => new Date(a.scheduledDate).getTime() - new Date(b.scheduledDate).getTime());
  };

  const getTechnicianSchedule = (technicianId: string) => {
    return assignments
      .filter(assignment => assignment.technicianId === technicianId)
      .sort((a, b) => new Date(a.scheduledDate).getTime() - new Date(b.scheduledDate).getTime());
  };

  const openTechnicianDialog = (technician: Technician, tab: string) => {
    setSelectedTechnician(technician);
    setTechnicianDialogTab(tab);
    setIsViewDialogOpen(true);
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
            View Maintenance department employees, schedules, assignments, and performance
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
                Technicians are employees assigned to the Maintenance department in HR. To add, modify, or manage technician information,
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
              Maintenance department employees
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
            <CardTitle className="text-sm font-medium">Assigned / Busy</CardTitle>
            <Clock className="h-4 w-4 text-blue-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">
              {assignedOrBusyCount}
            </div>
            <p className="text-xs text-muted-foreground">
              On scheduled assignments
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
              {averageRating.toFixed(1)}
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
                  {specializationOptions.map((specialization) => (
                    <SelectItem key={specialization} value={specialization}>
                      {specialization}
                    </SelectItem>
                  ))}
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
                  <SelectItem value="Assigned">Assigned</SelectItem>
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
                        onClick={() => openTechnicianDialog(technician, 'details')}
                        title="View technician details"
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => openTechnicianDialog(technician, 'schedule')}
                        title="View technician schedule"
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
      <Dialog
        open={isViewDialogOpen}
        onOpenChange={(open) => {
          setIsViewDialogOpen(open);
          if (!open) {
            setTechnicianDialogTab('details');
          }
        }}
      >
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Technician Details</DialogTitle>
            <DialogDescription>
              Complete information and assignments for this technician
            </DialogDescription>
          </DialogHeader>
          {selectedTechnician && (
            <Tabs value={technicianDialogTab} onValueChange={setTechnicianDialogTab} className="space-y-4">
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
                    <div className="mt-4 space-y-2">
                      {getTechnicianSchedule(selectedTechnician.id).map((assignment) => (
                        <div key={assignment.id} className="rounded-md border bg-muted/30 p-3">
                          <div className="flex items-start justify-between gap-3">
                            <div>
                              <p className="text-sm font-medium">{assignment.workOrderTitle}</p>
                              <p className="text-xs text-muted-foreground">{assignment.assetName}</p>
                              <p className="text-xs text-muted-foreground">
                                {new Date(assignment.scheduledDate).toLocaleString()}
                                {assignment.endDate ? ` - ${new Date(assignment.endDate).toLocaleString()}` : ''}
                              </p>
                            </div>
                            <Badge variant="outline">{assignment.status}</Badge>
                          </div>
                        </div>
                      ))}
                      {getTechnicianSchedule(selectedTechnician.id).length === 0 && (
                        <p className="text-sm text-muted-foreground text-center py-6">
                          No schedule entries found for this technician.
                        </p>
                      )}
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
