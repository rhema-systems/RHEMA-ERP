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
import { Checkbox } from '@/components/ui/checkbox';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  ClipboardCheck,
  Search,
  Plus,
  Calendar,
  Users,
  Package,
  FileText,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Eye,
  Edit,
  Download,
  Upload,
  MoreHorizontal,
  MapPin,
  Clock
} from 'lucide-react';
import { cn } from '@/lib/utils';

// Mock data for inspections
const inspectionsData = [
  {
    id: 1,
    title: 'Fire Safety Inspection - Building A',
    type: 'Safety',
    assetId: 'FIRE-001',
    assetName: 'Fire Safety System A',
    location: 'Building A - All Floors',
    inspector: 'Sarah Davis',
    scheduledDate: '2024-01-20',
    completedDate: '2024-01-20',
    status: 'Completed',
    result: 'Pass',
    score: 95,
    findings: [
      { item: 'Fire extinguisher placement', status: 'Pass', notes: 'All extinguishers properly mounted' },
      { item: 'Emergency exit signs', status: 'Pass', notes: 'All signs illuminated and visible' },
      { item: 'Smoke detector functionality', status: 'Pass', notes: 'All detectors tested and working' }
    ],
    recommendations: ['Replace fire extinguisher in Room 205 (expired)', 'Update evacuation plan poster in lobby'],
    nextInspection: '2024-07-20'
  },
  {
    id: 2,
    title: 'Elevator Safety Inspection',
    type: 'Safety',
    assetId: 'ELEV-001',
    assetName: 'Main Elevator A1',
    location: 'Building A - All Floors',
    inspector: 'Mike Johnson',
    scheduledDate: '2024-01-25',
    completedDate: null,
    status: 'Scheduled',
    result: null,
    score: null,
    findings: [],
    recommendations: [],
    nextInspection: '2024-04-25'
  },
  {
    id: 3,
    title: 'HVAC System Performance Check',
    type: 'Performance',
    assetId: 'HVAC-001',
    assetName: 'Central HVAC Unit',
    location: 'Building B - Roof',
    inspector: 'Tom Wilson',
    scheduledDate: '2024-01-18',
    completedDate: '2024-01-18',
    status: 'Completed',
    result: 'Conditional',
    score: 75,
    findings: [
      { item: 'Air filter condition', status: 'Fail', notes: 'Filters severely clogged, need immediate replacement' },
      { item: 'Temperature control accuracy', status: 'Pass', notes: 'Within acceptable range' },
      { item: 'Energy efficiency', status: 'Pass', notes: 'Operating at expected efficiency levels' }
    ],
    recommendations: ['Replace air filters immediately', 'Schedule quarterly filter replacement'],
    nextInspection: '2024-04-18'
  },
  {
    id: 4,
    title: 'Electrical System Compliance',
    type: 'Compliance',
    assetId: 'ELEC-001',
    assetName: 'Main Electrical Panel',
    location: 'Building A - Basement',
    inspector: 'John Smith',
    scheduledDate: '2024-01-30',
    completedDate: null,
    status: 'In Progress',
    result: null,
    score: null,
    findings: [],
    recommendations: [],
    nextInspection: '2024-07-30'
  }
];

const inspectionTemplates = [
  { id: 1, name: 'Fire Safety Inspection', type: 'Safety', items: 15 },
  { id: 2, name: 'Elevator Safety Check', type: 'Safety', items: 12 },
  { id: 3, name: 'HVAC Performance', type: 'Performance', items: 8 },
  { id: 4, name: 'Electrical Compliance', type: 'Compliance', items: 20 }
];

export default function InspectionsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [resultFilter, setResultFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [filteredData, setFilteredData] = useState(inspectionsData);
  const [selectedInspection, setSelectedInspection] = useState(null);
  
  // Form state for creating new inspection
  const [formData, setFormData] = useState({
    title: '',
    type: 'Safety',
    assetId: '',
    inspector: '',
    scheduledDate: '',
    templateId: '',
    notes: ''
  });

  useEffect(() => {
    let filtered = inspectionsData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assetName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.inspector.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (typeFilter !== 'all') {
      filtered = filtered.filter(item => item.type.toLowerCase() === typeFilter);
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => item.status.toLowerCase() === statusFilter);
    }

    if (resultFilter !== 'all') {
      filtered = filtered.filter(item => item.result?.toLowerCase() === resultFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, typeFilter, statusFilter, resultFilter]);

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

  const getResultBadge = (result: string | null) => {
    if (!result) return null;
    
    const colors = {
      'Pass': 'bg-green-100 text-green-800',
      'Conditional': 'bg-yellow-100 text-yellow-800',
      'Fail': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[result] || 'bg-gray-100 text-gray-800'}>
        {result === 'Pass' && <CheckCircle className="w-3 h-3 mr-1" />}
        {result === 'Conditional' && <AlertTriangle className="w-3 h-3 mr-1" />}
        {result === 'Fail' && <XCircle className="w-3 h-3 mr-1" />}
        {result}
      </Badge>
    );
  };

  const getScoreColor = (score: number | null) => {
    if (!score) return 'text-gray-500';
    if (score >= 90) return 'text-green-600';
    if (score >= 70) return 'text-yellow-600';
    return 'text-red-600';
  };

  const handleCreateInspection = () => {
    console.log('Creating new inspection:', formData);
    setIsCreateDialogOpen(false);
    // Reset form
    setFormData({
      title: '',
      type: 'Safety',
      assetId: '',
      inspector: '',
      scheduledDate: '',
      templateId: '',
      notes: ''
    });
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Inspections</h1>
          <p className="text-muted-foreground">
            Safety, compliance, and performance inspections for all assets
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Schedule Inspection
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Schedule New Inspection</DialogTitle>
              <DialogDescription>
                Create a new inspection for an asset using a predefined template.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Inspection Title</Label>
                  <Input
                    id="title"
                    value={formData.title}
                    onChange={(e) => setFormData({...formData, title: e.target.value})}
                    placeholder="Inspection name or description"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="type">Inspection Type</Label>
                  <Select value={formData.type} onValueChange={(value) => setFormData({...formData, type: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Safety">Safety</SelectItem>
                      <SelectItem value="Performance">Performance</SelectItem>
                      <SelectItem value="Compliance">Compliance</SelectItem>
                      <SelectItem value="Routine">Routine</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="assetId">Asset</Label>
                  <Select value={formData.assetId} onValueChange={(value) => setFormData({...formData, assetId: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select asset" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="FIRE-001">Fire Safety System A</SelectItem>
                      <SelectItem value="ELEV-001">Main Elevator A1</SelectItem>
                      <SelectItem value="HVAC-001">Central HVAC Unit</SelectItem>
                      <SelectItem value="ELEC-001">Main Electrical Panel</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="inspector">Inspector</Label>
                  <Select value={formData.inspector} onValueChange={(value) => setFormData({...formData, inspector: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select inspector" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Sarah Davis">Sarah Davis</SelectItem>
                      <SelectItem value="Mike Johnson">Mike Johnson</SelectItem>
                      <SelectItem value="Tom Wilson">Tom Wilson</SelectItem>
                      <SelectItem value="John Smith">John Smith</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="scheduledDate">Scheduled Date</Label>
                  <Input
                    id="scheduledDate"
                    type="date"
                    value={formData.scheduledDate}
                    onChange={(e) => setFormData({...formData, scheduledDate: e.target.value})}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="templateId">Inspection Template</Label>
                  <Select value={formData.templateId} onValueChange={(value) => setFormData({...formData, templateId: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select template" />
                    </SelectTrigger>
                    <SelectContent>
                      {inspectionTemplates.map((template) => (
                        <SelectItem key={template.id} value={template.id.toString()}>
                          {template.name} ({template.items} items)
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="notes">Notes</Label>
                <Textarea
                  id="notes"
                  value={formData.notes}
                  onChange={(e) => setFormData({...formData, notes: e.target.value})}
                  placeholder="Additional notes or special instructions..."
                  rows={3}
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateInspection}>Schedule Inspection</Button>
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
            <BreadcrumbPage>Inspections</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(item => item.status === 'Scheduled').length}
                </p>
                <p className="text-sm text-muted-foreground">Scheduled</p>
              </div>
              <Calendar className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(item => item.status === 'In Progress').length}
                </p>
                <p className="text-sm text-muted-foreground">In Progress</p>
              </div>
              <ClipboardCheck className="h-8 w-8 text-yellow-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(item => item.result === 'Pass').length}
                </p>
                <p className="text-sm text-muted-foreground">Passed</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">87%</p>
                <p className="text-sm text-muted-foreground">Pass Rate</p>
              </div>
              <FileText className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="inspections" className="space-y-4">
        <TabsList>
          <TabsTrigger value="inspections">Inspections</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
        </TabsList>

        <TabsContent value="inspections" className="space-y-4">
          {/* Filters */}
          <Card>
            <CardHeader>
              <CardTitle>Filters</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="relative">
                  <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    placeholder="Search inspections..."
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
                    <SelectItem value="safety">Safety</SelectItem>
                    <SelectItem value="performance">Performance</SelectItem>
                    <SelectItem value="compliance">Compliance</SelectItem>
                    <SelectItem value="routine">Routine</SelectItem>
                  </SelectContent>
                </Select>

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

                <Select value={resultFilter} onValueChange={setResultFilter}>
                  <SelectTrigger>
                    <SelectValue placeholder="Result" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Results</SelectItem>
                    <SelectItem value="pass">Pass</SelectItem>
                    <SelectItem value="conditional">Conditional</SelectItem>
                    <SelectItem value="fail">Fail</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </CardContent>
          </Card>

          {/* Inspections List */}
          <Card>
            <CardHeader>
              <CardTitle>Inspection Records</CardTitle>
              <CardDescription>
                {filteredData.length} inspection(s) found
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {filteredData.map((inspection) => (
                  <div key={inspection.id} className="border rounded-lg p-4">
                    <div className="flex items-start justify-between">
                      <div className="space-y-3 flex-1">
                        <div className="flex items-center space-x-3">
                          <h3 className="font-semibold">{inspection.title}</h3>
                          <Badge variant="outline">{inspection.type}</Badge>
                          {getStatusBadge(inspection.status)}
                          {getResultBadge(inspection.result)}
                        </div>
                        
                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                          <div className="flex items-center space-x-2">
                            <Package className="h-4 w-4" />
                            <span>{inspection.assetName}</span>
                          </div>
                          <div className="flex items-center space-x-2">
                            <MapPin className="h-4 w-4" />
                            <span>{inspection.location}</span>
                          </div>
                          <div className="flex items-center space-x-2">
                            <Users className="h-4 w-4" />
                            <span>{inspection.inspector}</span>
                          </div>
                          <div className="flex items-center space-x-2">
                            <Calendar className="h-4 w-4" />
                            <span>
                              {inspection.completedDate 
                                ? `Completed: ${new Date(inspection.completedDate).toLocaleDateString()}`
                                : `Scheduled: ${new Date(inspection.scheduledDate).toLocaleDateString()}`
                              }
                            </span>
                          </div>
                        </div>
                        
                        {inspection.score && (
                          <div className="text-sm">
                            <span className="font-medium">Score:</span> 
                            <span className={cn("font-bold ml-1", getScoreColor(inspection.score))}>
                              {inspection.score}/100
                            </span>
                          </div>
                        )}
                        
                        {inspection.findings.length > 0 && (
                          <div className="text-sm">
                            <span className="font-medium">Key Findings:</span>
                            <ul className="mt-1 space-y-1">
                              {inspection.findings.slice(0, 2).map((finding, idx) => (
                                <li key={idx} className="flex items-center space-x-2 text-muted-foreground">
                                  {finding.status === 'Pass' && <CheckCircle className="h-3 w-3 text-green-500" />}
                                  {finding.status === 'Fail' && <XCircle className="h-3 w-3 text-red-500" />}
                                  <span>{finding.item}: {finding.notes}</span>
                                </li>
                              ))}
                              {inspection.findings.length > 2 && (
                                <li className="text-xs text-muted-foreground">
                                  +{inspection.findings.length - 2} more items
                                </li>
                              )}
                            </ul>
                          </div>
                        )}
                      </div>
                      
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline">
                          <Eye className="mr-2 h-4 w-4" />
                          View
                        </Button>
                        {inspection.status === 'Completed' && (
                          <Button size="sm" variant="outline">
                            <Download className="mr-2 h-4 w-4" />
                            Report
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
        </TabsContent>

        <TabsContent value="templates" className="space-y-4">
          {/* Inspection Templates */}
          <Card>
            <CardHeader>
              <CardTitle>Inspection Templates</CardTitle>
              <CardDescription>Predefined inspection checklists and forms</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {inspectionTemplates.map((template) => (
                  <Card key={template.id} className="cursor-pointer hover:shadow-md transition-shadow">
                    <CardContent className="pt-6">
                      <div className="space-y-4">
                        <div className="flex items-center justify-between">
                          <ClipboardCheck className="h-8 w-8 text-blue-500" />
                          <Badge variant="outline">{template.type}</Badge>
                        </div>
                        <div>
                          <h3 className="font-semibold">{template.name}</h3>
                          <p className="text-sm text-muted-foreground">
                            {template.items} checklist items
                          </p>
                        </div>
                        <div className="flex space-x-2">
                          <Button size="sm" variant="outline">
                            <Eye className="mr-2 h-4 w-4" />
                            Preview
                          </Button>
                          <Button size="sm" variant="outline">
                            <Edit className="mr-2 h-4 w-4" />
                            Edit
                          </Button>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

    </div>
  );
}