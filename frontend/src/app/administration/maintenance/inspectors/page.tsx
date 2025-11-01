'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Users,
  Award,
  BookOpen,
  TrendingUp,
  Calendar,
  Clock,
  CheckCircle,
  AlertTriangle,
  Star,
  Target,
  Search,
  Filter,
  Eye,
  Download,
  Upload,
  Settings,
  Shield,
  FileText,
  BarChart3
} from 'lucide-react';
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

import { performanceAnalyticsService, InspectorPerformance } from '@/services/performanceAnalyticsService';

// Employee interface - from HR module
interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  employeeId: string;
  department: string;
  position: string;
  hireDate: string;
  status: 'Active' | 'Inactive' | 'On Leave' | 'Terminated';
  managerId?: string;
  avatar?: string;
  // HR module data
  skills: string[];
  certifications: EmployeeCertification[];
  trainings: EmployeeTraining[];
  roles: string[]; // e.g., ['Quality Inspector', 'Safety Inspector']
}

// HR module certification structure
interface EmployeeCertification {
  id: string;
  name: string;
  issuingAuthority: string;
  issueDate: string;
  expiryDate: string;
  certificationNumber: string;
  status: 'Valid' | 'Expired' | 'Expiring Soon' | 'Revoked';
  category: string; // e.g., 'Quality Control', 'Safety', 'Technical'
  attachments?: string[];
}

// HR module training structure
interface EmployeeTraining {
  id: string;
  title: string;
  description: string;
  provider: string;
  completionDate: string;
  expiryDate?: string;
  hoursCompleted: number;
  score?: number;
  status: 'Completed' | 'In Progress' | 'Failed' | 'Expired';
  category: string;
  certificateUrl?: string;
}

// Quality Control specific data (minimal - only what's not in HR)
interface QualityControlMetadata {
  employeeId: string;
  certificationLevel: 'Junior' | 'Intermediate' | 'Senior' | 'Expert';
  performanceMetrics: {
    totalInspections: number;
    averageScore: number;
    passRate: number;
    averageTimeToComplete: number;
  };
  isActiveInspector: boolean;
  dateAssignedAsInspector: string;
  inspectorNotes: string;
}

// Inspector view that combines HR data with QC-specific data
interface InspectorView extends Employee {
  qualityControlMetadata: QualityControlMetadata;
}

interface Certification {
  id: string;
  name: string;
  issuingAuthority: string;
  issueDate: string;
  expiryDate: string;
  certificationNumber: string;
  status: 'Valid' | 'Expired' | 'Expiring Soon' | 'Revoked';
  attachments: string[];
}

interface TrainingRecord {
  id: string;
  title: string;
  description: string;
  provider: string;
  completionDate: string;
  expiryDate?: string;
  hoursCompleted: number;
  score?: number;
  status: 'Completed' | 'In Progress' | 'Failed' | 'Expired';
  certificateUrl?: string;
}

// HR module service integration
interface HRService {
  getAllEmployees(): Promise<Employee[]>;
  getEmployeesByRole(role: string): Promise<Employee[]>;
  getEmployeeCertifications(employeeId: string): Promise<EmployeeCertification[]>;
  getEmployeeTraining(employeeId: string): Promise<EmployeeTraining[]>;
  addEmployeeRole(employeeId: string, role: string): Promise<void>;
  removeEmployeeRole(employeeId: string, role: string): Promise<void>;
  updateEmployeeCertification(employeeId: string, certification: EmployeeCertification): Promise<EmployeeCertification>;
  addEmployeeTraining(employeeId: string, training: EmployeeTraining): Promise<EmployeeTraining>;
}

// Quality Control specific service (minimal)
interface QualityControlService {
  getQualityControlMetadata(employeeId: string): Promise<QualityControlMetadata>;
  createQualityControlMetadata(employeeId: string, metadata: Partial<QualityControlMetadata>): Promise<QualityControlMetadata>;
  updateQualityControlMetadata(employeeId: string, metadata: Partial<QualityControlMetadata>): Promise<QualityControlMetadata>;
  removeQualityControlMetadata(employeeId: string): Promise<void>;
  getAllQualityControlMetadata(): Promise<QualityControlMetadata[]>;
}

export default function InspectorsPage() {
  const [loading, setLoading] = useState(true);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [inspectorMetadata, setInspectorMetadata] = useState<QualityControlMetadata[]>([]);
  const [inspectorViews, setInspectorViews] = useState<InspectorView[]>([]);
  const [inspectorPerformance, setInspectorPerformance] = useState<InspectorPerformance[]>([]);
  const [selectedInspector, setSelectedInspector] = useState<InspectorView | null>(null);
  const [availableEmployees, setAvailableEmployees] = useState<Employee[]>([]);
  
  const [isDetailDialogOpen, setIsDetailDialogOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [certificationFilter, setCertificationFilter] = useState('all');

  useEffect(() => {
    loadInspectorsData();
  }, []);

  const loadInspectorsData = async () => {
    try {
      setLoading(true);
      
      // Fetch employees from HR/Employees API filtered by Quality Control department
      const response = await fetch('/api/hr/employees?department=Quality Control');
      if (!response.ok) {
        throw new Error('Failed to fetch employees');
      }
      
      const employeesData = await response.json();
      
      // For now, use mock metadata until backend API is ready
      const mockInspectorMetadata = await getMockQualityControlMetadata();
      const performance = await performanceAnalyticsService.getInspectorPerformance();
      
      // Transform employee data to match Employee interface
      const employees: Employee[] = employeesData.map((emp: any) => ({
        id: emp.id,
        firstName: emp.firstName,
        lastName: emp.lastName,
        email: emp.emailAddress || emp.email,
        phone: emp.mobileNumber || emp.phone,
        employeeId: emp.employeeNumber || emp.employeeId,
        department: emp.department?.name || emp.department || 'Quality Control',
        position: emp.position?.title || emp.position || 'Inspector',
        hireDate: emp.dateEmployed || emp.hireDate,
        status: emp.isActive ? 'Active' : 'Inactive',
        skills: emp.skills || [],
        certifications: emp.certifications || [],
        trainings: emp.trainings || [],
        roles: ['Quality Inspector']
      }));
      
      // Merge employee data with inspector metadata
      const inspectorViews = employees
        .map(employee => {
          const metadata = mockInspectorMetadata.find(m => m.employeeId === employee.employeeId);
          if (metadata) {
            return {
              ...employee,
              qualityControlMetadata: metadata
            } as InspectorView;
          }
          // Create default metadata for employees without it
          return {
            ...employee,
            qualityControlMetadata: {
              employeeId: employee.employeeId,
              certificationLevel: 'Intermediate',
              performanceMetrics: {
                totalInspections: 0,
                averageScore: 0,
                passRate: 0,
                averageTimeToComplete: 0
              },
              isActiveInspector: true,
              dateAssignedAsInspector: employee.hireDate,
              inspectorNotes: ''
            }
          } as InspectorView;
        });
      
      // Get non-inspector employees for "assign as inspector" functionality
      const employeesWithoutInspectorRole = employees.filter(emp => 
        !mockInspectorMetadata.some(meta => meta.employeeId === emp.employeeId)
      );
      
      setEmployees(employees);
      setInspectorMetadata(mockInspectorMetadata);
      setInspectorViews(inspectorViews);
      setAvailableEmployees(employeesWithoutInspectorRole);
      setInspectorPerformance(performance);
      
    } catch (error) {
      console.error('Error loading inspectors data:', error);
      // Fallback to mock data on error
      const mockEmployees = await getMockEmployees();
      const mockInspectorMetadata = await getMockQualityControlMetadata();
      const performance = await performanceAnalyticsService.getInspectorPerformance();
      
      const inspectorViews = mockEmployees
        .map(employee => {
          const metadata = mockInspectorMetadata.find(m => m.employeeId === employee.employeeId);
          if (metadata) {
            return {
              ...employee,
              qualityControlMetadata: metadata
            } as InspectorView;
          }
          return null;
        })
        .filter(Boolean) as InspectorView[];
      
      setEmployees(mockEmployees);
      setInspectorMetadata(mockInspectorMetadata);
      setInspectorViews(inspectorViews);
      setAvailableEmployees([]);
      setInspectorPerformance(performance);
    } finally {
      setLoading(false);
    }
  };

  const getMockEmployees = async (): Promise<Employee[]> => {
    return [
      {
        id: '1',
        firstName: 'Emma',
        lastName: 'Wilson',
        email: 'emma.wilson@company.com',
        phone: '+1-555-0101',
        employeeId: 'EMP001',
        department: 'Quality Control',
        position: 'Quality Inspector',
        hireDate: '2020-03-15',
        status: 'Active',
        skills: ['Quality Assessment', 'HVAC Systems', 'Electrical Systems', 'Fire Safety'],
        certifications: [
          {
            id: '1',
            name: 'Certified Quality Inspector',
            issuingAuthority: 'National Quality Institute',
            issueDate: '2021-01-15',
            expiryDate: '2025-01-15',
            certificationNumber: 'CQI-2021-001',
            status: 'Valid',
            category: 'Quality Control'
          },
          {
            id: '2',
            name: 'HVAC Systems Specialist',
            issuingAuthority: 'HVAC Certification Board',
            issueDate: '2020-06-10',
            expiryDate: '2024-06-10',
            certificationNumber: 'HVAC-2020-245',
            status: 'Expiring Soon',
            category: 'Technical'
          }
        ],
        trainings: [
          {
            id: '1',
            title: 'Advanced Quality Control Techniques',
            description: 'Comprehensive training on modern quality control methodologies',
            provider: 'QC Training Institute',
            completionDate: '2023-11-15',
            hoursCompleted: 40,
            score: 92,
            status: 'Completed',
            category: 'Quality Control'
          }
        ],
        roles: ['Quality Inspector', 'HVAC Inspector']
      },
      {
        id: '2',
        firstName: 'David',
        lastName: 'Chen',
        email: 'david.chen@company.com',
        phone: '+1-555-0102',
        employeeId: 'EMP002',
        department: 'Quality Control',
        position: 'Senior Quality Inspector',
        hireDate: '2019-08-20',
        status: 'Active',
        skills: ['Electrical Systems', 'Mechanical Systems', 'Safety Protocols'],
        certifications: [
          {
            id: '3',
            name: 'Electrical Safety Inspector',
            issuingAuthority: 'Electrical Safety Council',
            issueDate: '2019-12-01',
            expiryDate: '2024-12-01',
            certificationNumber: 'ESI-2019-156',
            status: 'Valid',
            category: 'Safety'
          }
        ],
        trainings: [
          {
            id: '2',
            title: 'Mechanical Systems Inspection',
            description: 'Training focused on mechanical system quality assessment',
            provider: 'Mechanical Institute',
            completionDate: '2023-09-20',
            hoursCompleted: 32,
            score: 88,
            status: 'Completed',
            category: 'Technical'
          }
        ],
        roles: ['Quality Inspector', 'Electrical Inspector', 'Safety Inspector']
      },
      {
        id: '3',
        firstName: 'Sarah',
        lastName: 'Johnson',
        email: 'sarah.johnson@company.com',
        phone: '+1-555-0103',
        employeeId: 'EMP003',
        department: 'Quality Control',
        position: 'Quality Inspector',
        hireDate: '2022-01-10',
        status: 'Active',
        skills: ['Plumbing Systems', 'HVAC Systems', 'General Inspection'],
        certifications: [
          {
            id: '4',
            name: 'General Quality Inspector',
            issuingAuthority: 'Quality Assurance Board',
            issueDate: '2022-03-15',
            expiryDate: '2026-03-15',
            certificationNumber: 'GQI-2022-089',
            status: 'Valid',
            category: 'Quality Control'
          }
        ],
        trainings: [
          {
            id: '3',
            title: 'Plumbing Systems Quality Control',
            description: 'Specialized training in plumbing inspection techniques',
            provider: 'Plumbing Professionals Institute',
            completionDate: '2023-05-12',
            hoursCompleted: 24,
            score: 85,
            status: 'Completed',
            category: 'Technical'
          }
        ],
        roles: ['Quality Inspector']
      }
    ];
  };

  const getMockQualityControlMetadata = async (): Promise<QualityControlMetadata[]> => {
    return [
      {
        employeeId: 'EMP001',
        certificationLevel: 'Senior',
        performanceMetrics: {
          totalInspections: 78,
          averageScore: 94.2,
          passRate: 96.2,
          averageTimeToComplete: 45
        },
        isActiveInspector: true,
        dateAssignedAsInspector: '2020-04-01',
        inspectorNotes: 'Excellent performer with expertise in multiple specializations.'
      },
      {
        employeeId: 'EMP002',
        certificationLevel: 'Senior',
        performanceMetrics: {
          totalInspections: 65,
          averageScore: 91.8,
          passRate: 93.8,
          averageTimeToComplete: 52
        },
        isActiveInspector: true,
        dateAssignedAsInspector: '2019-10-15',
        inspectorNotes: 'Strong technical background with consistent performance.'
      },
      {
        employeeId: 'EMP003',
        certificationLevel: 'Intermediate',
        performanceMetrics: {
          totalInspections: 56,
          averageScore: 89.4,
          passRate: 91.1,
          averageTimeToComplete: 48
        },
        isActiveInspector: true,
        dateAssignedAsInspector: '2022-02-01',
        inspectorNotes: 'Developing inspector with good potential. Focus on building experience.'
      }
    ];
  };



  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Active':
        return 'bg-green-100 text-green-800';
      case 'Inactive':
        return 'bg-gray-100 text-gray-800';
      case 'On Leave':
        return 'bg-yellow-100 text-yellow-800';
      case 'Training':
        return 'bg-blue-100 text-blue-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const getCertificationLevelColor = (level: string) => {
    switch (level) {
      case 'Expert':
        return 'bg-purple-100 text-purple-800';
      case 'Senior':
        return 'bg-blue-100 text-blue-800';
      case 'Intermediate':
        return 'bg-green-100 text-green-800';
      case 'Junior':
        return 'bg-orange-100 text-orange-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const getCertificationStatusColor = (status: string) => {
    switch (status) {
      case 'Valid':
        return 'bg-green-100 text-green-800';
      case 'Expiring Soon':
        return 'bg-yellow-100 text-yellow-800';
      case 'Expired':
        return 'bg-red-100 text-red-800';
      case 'Revoked':
        return 'bg-gray-100 text-gray-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const filteredInspectors = inspectorViews.filter(inspector => {
    const matchesSearch = `${inspector.firstName} ${inspector.lastName}`.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         inspector.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         inspector.employeeId.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesStatus = statusFilter === 'all' || inspector.status === statusFilter;
    const matchesCertification = certificationFilter === 'all' || inspector.qualityControlMetadata.certificationLevel === certificationFilter;
    
    return matchesSearch && matchesStatus && matchesCertification;
  });

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-center h-64">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-gray-900 mx-auto"></div>
            <p className="mt-4 text-muted-foreground">Loading inspectors...</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Inspector Management</h1>
          <p className="text-muted-foreground">
            View inspector profiles, certifications, training, and performance data. Inspector information is managed through the HR module.
          </p>
        </div>
        
        <div className="flex items-center space-x-2">
          <Button variant="outline">
            <Download className="mr-2 h-4 w-4" />
            Export List
          </Button>
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
            <BreadcrumbLink href="/administration">Administration</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Inspectors</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Inspectors</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{inspectorViews.length}</div>
            <p className="text-xs text-muted-foreground">
              {inspectorViews.filter(i => i.status === 'Active').length} active
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Performance</CardTitle>
            <Target className="h-4 w-4 text-blue-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">
              {(inspectorViews.reduce((sum, i) => sum + i.qualityControlMetadata.performanceMetrics.averageScore, 0) / inspectorViews.length || 0).toFixed(1)}
            </div>
            <p className="text-xs text-muted-foreground">Quality score</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Certifications Due</CardTitle>
            <AlertTriangle className="h-4 w-4 text-yellow-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-yellow-600">
              {inspectorViews.reduce((count, inspector) => 
                count + inspector.certifications.filter(cert => cert.status === 'Expiring Soon').length, 0
              )}
            </div>
            <p className="text-xs text-muted-foreground">Expiring soon</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Training Hours</CardTitle>
            <BookOpen className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">
              {inspectorViews.reduce((total, inspector) => 
                total + inspector.trainings.reduce((sum, record) => sum + record.hoursCompleted, 0), 0
              )}
            </div>
            <p className="text-xs text-muted-foreground">This year</p>
          </CardContent>
        </Card>
      </div>

      {/* Main Content */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Inspector Directory</CardTitle>
              <CardDescription>All registered quality control inspectors</CardDescription>
            </div>
            
            <div className="flex items-center space-x-2">
              <div className="relative">
                <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search inspectors..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-9 w-64"
                />
              </div>
              
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-32">
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Inactive">Inactive</SelectItem>
                  <SelectItem value="On Leave">On Leave</SelectItem>
                  <SelectItem value="Training">Training</SelectItem>
                </SelectContent>
              </Select>
              
              <Select value={certificationFilter} onValueChange={setCertificationFilter}>
                <SelectTrigger className="w-36">
                  <SelectValue placeholder="Level" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Levels</SelectItem>
                  <SelectItem value="Expert">Expert</SelectItem>
                  <SelectItem value="Senior">Senior</SelectItem>
                  <SelectItem value="Intermediate">Intermediate</SelectItem>
                  <SelectItem value="Junior">Junior</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Inspector</TableHead>
                <TableHead>Employee ID</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Certification Level</TableHead>
                <TableHead>Skills</TableHead>
                <TableHead>Performance</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredInspectors.map((inspector) => (
                <TableRow key={inspector.id}>
                  <TableCell>
                    <div>
                      <p className="font-medium text-sm">{inspector.firstName} {inspector.lastName}</p>
                      <p className="text-xs text-muted-foreground">{inspector.email}</p>
                    </div>
                  </TableCell>
                  <TableCell className="font-mono text-sm">{inspector.employeeId}</TableCell>
                  <TableCell>
                    <Badge className={getStatusColor(inspector.status)}>
                      {inspector.status}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Badge className={getCertificationLevelColor(inspector.qualityControlMetadata.certificationLevel)}>
                      {inspector.qualityControlMetadata.certificationLevel}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      {inspector.skills.slice(0, 2).map((skill, index) => (
                        <Badge key={index} variant="secondary" className="text-xs">
                          {skill}
                        </Badge>
                      ))}
                      {inspector.skills.length > 2 && (
                        <Badge variant="secondary" className="text-xs">
                          +{inspector.skills.length - 2}
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <span className="text-sm font-medium">{inspector.qualityControlMetadata.performanceMetrics.averageScore.toFixed(1)}</span>
                      <div className="w-16">
                        <Progress value={inspector.qualityControlMetadata.performanceMetrics.averageScore} className="h-1" />
                      </div>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-1">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedInspector(inspector);
                          setIsDetailDialogOpen(true);
                        }}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>


      {/* Inspector Detail Dialog */}
      <Dialog open={isDetailDialogOpen} onOpenChange={setIsDetailDialogOpen}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Inspector Profile</DialogTitle>
            <DialogDescription>
              Complete profile and performance information
            </DialogDescription>
          </DialogHeader>
          {selectedInspector && (
            <Tabs defaultValue="profile" className="space-y-4">
              <TabsList className="grid w-full grid-cols-4">
                <TabsTrigger value="profile">Profile</TabsTrigger>
                <TabsTrigger value="certifications">Certifications</TabsTrigger>
                <TabsTrigger value="training">Training</TabsTrigger>
                <TabsTrigger value="performance">Performance</TabsTrigger>
              </TabsList>
              
              <TabsContent value="profile" className="space-y-4">
                <div className="grid grid-cols-2 gap-6">
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Name</Label>
                      <p className="text-lg font-medium">{selectedInspector.firstName} {selectedInspector.lastName}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Email</Label>
                      <p className="text-sm">{selectedInspector.email}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Phone</Label>
                      <p className="text-sm">{selectedInspector.phone}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Employee ID</Label>
                      <p className="text-sm font-mono">{selectedInspector.employeeId}</p>
                    </div>
                  </div>
                  
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Department</Label>
                      <p className="text-sm">{selectedInspector.department}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                      <div className="pt-1">
                        <Badge className={getStatusColor(selectedInspector.status)}>
                          {selectedInspector.status}
                        </Badge>
                      </div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Certification Level</Label>
                      <div className="pt-1">
                        <Badge className={getCertificationLevelColor(selectedInspector.qualityControlMetadata.certificationLevel)}>
                          {selectedInspector.qualityControlMetadata.certificationLevel}
                        </Badge>
                      </div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Hire Date</Label>
                      <p className="text-sm">{new Date(selectedInspector.hireDate).toLocaleDateString()}</p>
                    </div>
                  </div>
                </div>
                
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Skills & Roles</Label>
                  <div className="space-y-2 mt-2">
                    <div>
                      <Label className="text-xs font-medium text-muted-foreground">Skills:</Label>
                      <div className="flex flex-wrap gap-2 mt-1">
                        {selectedInspector.skills.map((skill, index) => (
                          <Badge key={index} variant="secondary">
                            {skill}
                          </Badge>
                        ))}
                      </div>
                    </div>
                    <div>
                      <Label className="text-xs font-medium text-muted-foreground">Roles:</Label>
                      <div className="flex flex-wrap gap-2 mt-1">
                        {selectedInspector.roles.map((role, index) => (
                          <Badge key={index} variant="outline">
                            {role}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
                
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Notes</Label>
                  <p className="text-sm">{selectedInspector.qualityControlMetadata.inspectorNotes || 'No additional notes.'}</p>
                </div>
              </TabsContent>
              
              <TabsContent value="certifications" className="space-y-4">
                <div className="flex items-center justify-between">
                  <h3 className="text-lg font-medium">Certifications</h3>
                </div>
                
                <div className="space-y-3">
                  {selectedInspector.certifications.map((cert) => (
                    <div key={cert.id} className="border rounded-lg p-4">
                      <div className="flex items-start justify-between">
                        <div>
                          <div className="flex items-center space-x-2">
                            <h4 className="font-medium">{cert.name}</h4>
                            <Badge className={getCertificationStatusColor(cert.status)}>
                              {cert.status}
                            </Badge>
                          </div>
                          <p className="text-sm text-muted-foreground mt-1">{cert.issuingAuthority}</p>
                          <p className="text-xs text-muted-foreground">#{cert.certificationNumber}</p>
                        </div>
                        <div className="text-right text-sm">
                          <p>Issued: {new Date(cert.issueDate).toLocaleDateString()}</p>
                          <p>Expires: {new Date(cert.expiryDate).toLocaleDateString()}</p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </TabsContent>
              
              <TabsContent value="training" className="space-y-4">
                <div className="flex items-center justify-between">
                  <h3 className="text-lg font-medium">Training Records</h3>
                </div>
                
                <div className="space-y-3">
                  {selectedInspector.trainings.map((training) => (
                    <div key={training.id} className="border rounded-lg p-4">
                      <div className="flex items-start justify-between">
                        <div className="flex-1">
                          <h4 className="font-medium">{training.title}</h4>
                          <p className="text-sm text-muted-foreground mt-1">{training.description}</p>
                          <p className="text-xs text-muted-foreground">Provider: {training.provider}</p>
                        </div>
                        <div className="text-right text-sm">
                          <p>Completed: {new Date(training.completionDate).toLocaleDateString()}</p>
                          <p>Hours: {training.hoursCompleted}</p>
                          {training.score && <p>Score: {training.score}%</p>}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </TabsContent>
              
              <TabsContent value="performance" className="space-y-4">
                <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
                  <Card>
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm font-medium">Total Inspections</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="text-2xl font-bold">{selectedInspector.qualityControlMetadata.performanceMetrics.totalInspections}</div>
                    </CardContent>
                  </Card>
                  
                  <Card>
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm font-medium">Average Score</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="text-2xl font-bold text-blue-600">{selectedInspector.qualityControlMetadata.performanceMetrics.averageScore.toFixed(1)}</div>
                    </CardContent>
                  </Card>
                  
                  <Card>
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm font-medium">Pass Rate</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="text-2xl font-bold text-green-600">{selectedInspector.qualityControlMetadata.performanceMetrics.passRate.toFixed(1)}%</div>
                    </CardContent>
                  </Card>
                  
                  <Card>
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm font-medium">Avg Time</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="text-2xl font-bold text-purple-600">{selectedInspector.qualityControlMetadata.performanceMetrics.averageTimeToComplete}m</div>
                    </CardContent>
                  </Card>
                </div>
              </TabsContent>
            </Tabs>
          )}
        </DialogContent>
      </Dialog>

    </div>
  );
}