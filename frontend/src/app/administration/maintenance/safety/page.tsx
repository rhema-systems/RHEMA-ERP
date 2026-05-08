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
import { Switch } from '@/components/ui/switch';
import {
  Plus,
  Search,
  Edit,
  Trash2,
  MoreHorizontal,
  Shield,
  AlertTriangle,
  CheckCircle,
  FileText,
  Users,
  Clock,
  Eye,
  Download
} from 'lucide-react';

// Safety protocols interface
interface SafetyProtocol {
  id: string | number;
  name: string;
  code: string;
  description: string;
  category: string;
  riskLevel: string;
  isActive: boolean;
  isMandatory: boolean;
  version: string;
  lastUpdated: string;
  createdBy?: string;
  approvedBy?: string;
  approvalDate?: string;
  reviewFrequency: string;
  nextReviewDate: string;
  applicableAreas: string[];
  requiredTraining: string[];
  estimatedTime: number;
  steps: string[];
  requiredPPE: string[];
  emergencyContacts: string[];
  documents: string[];
  trainingRecords: number;
  complianceRate: number;
  incidentsLastYear: number;
}

interface SafetyProtocolFormData {
  name: string;
  code: string;
  description: string;
  category: string;
  riskLevel: string;
  isActive: boolean;
  isMandatory: boolean;
  version: string;
  reviewFrequency: string;
  applicableAreas: string[];
  requiredTraining: string[];
  estimatedTime: number;
  steps: string[];
  requiredPPE: string[];
  emergencyContacts: string[];
  documents: string[];
}

const mockSafetyProtocolsData = [
  {
    id: 1,
    name: 'Lockout/Tagout (LOTO) Procedure',
    code: 'LOTO-001',
    description: 'Standard procedure for isolating energy sources before maintenance work',
    category: 'Energy Isolation',
    riskLevel: 'High',
    isActive: true,
    isMandatory: true,
    version: '3.2',
    lastUpdated: '2024-02-15',
    createdBy: 'Safety Manager',
    approvedBy: 'Plant Manager',
    approvalDate: '2024-02-20',
    reviewFrequency: 'Annual',
    nextReviewDate: '2025-02-20',
    applicableAreas: ['Electrical', 'Mechanical', 'HVAC', 'Production'],
    requiredTraining: ['LOTO Certification', 'Energy Sources Training'],
    estimatedTime: 15,
    steps: [
      'Notify affected personnel',
      'Identify energy sources',
      'Shut down equipment',
      'Apply locks and tags',
      'Verify isolation',
      'Test equipment'
    ],
    requiredPPE: ['Safety glasses', 'Hard hat', 'Work gloves'],
    emergencyContacts: ['Safety Officer: 555-0001', 'Supervisor: 555-0002'],
    documents: ['LOTO_Procedure_v3.2.pdf', 'Energy_Source_Chart.pdf'],
    trainingRecords: 45,
    complianceRate: 98.5,
    incidentsLastYear: 0
  },
  {
    id: 2,
    name: 'Confined Space Entry',
    code: 'CSE-001',
    description: 'Safety protocol for entering and working in confined spaces',
    category: 'Confined Space',
    riskLevel: 'Critical',
    isActive: true,
    isMandatory: true,
    version: '2.1',
    lastUpdated: '2024-01-30',
    createdBy: 'Safety Engineer',
    approvedBy: 'Safety Director',
    approvalDate: '2024-02-05',
    reviewFrequency: 'Annual',
    nextReviewDate: '2025-02-05',
    applicableAreas: ['Tanks', 'Vessels', 'Utility Tunnels', 'Boilers'],
    requiredTraining: ['Confined Space Entry', 'Gas Detection', 'Rescue Procedures'],
    estimatedTime: 45,
    steps: [
      'Obtain entry permit',
      'Test atmosphere',
      'Implement ventilation',
      'Assign attendant',
      'Emergency preparation',
      'Continuous monitoring'
    ],
    requiredPPE: ['Full body harness', 'Respiratory protection', 'Gas monitor', 'Communication device'],
    emergencyContacts: ['Emergency Response: 911', 'Safety Officer: 555-0001', 'Medical: 555-0003'],
    documents: ['Confined_Space_Entry_v2.1.pdf', 'Permit_Template.pdf', 'Gas_Detection_Log.pdf'],
    trainingRecords: 28,
    complianceRate: 100.0,
    incidentsLastYear: 0
  },
  {
    id: 3,
    name: 'Hot Work Permit System',
    code: 'HWP-001',
    description: 'Safety procedures for welding, cutting, and other hot work operations',
    category: 'Fire Safety',
    riskLevel: 'High',
    isActive: true,
    isMandatory: true,
    version: '1.8',
    lastUpdated: '2024-03-01',
    createdBy: 'Fire Safety Officer',
    approvedBy: 'Plant Manager',
    approvalDate: '2024-03-05',
    reviewFrequency: 'Bi-Annual',
    nextReviewDate: '2024-09-05',
    applicableAreas: ['All Areas', 'Especially near flammables'],
    requiredTraining: ['Hot Work Safety', 'Fire Prevention', 'Fire Extinguisher Use'],
    estimatedTime: 30,
    steps: [
      'Complete hot work permit',
      'Remove combustibles',
      'Install fire barriers',
      'Post fire watch',
      'Begin work',
      'Post-work inspection'
    ],
    requiredPPE: ['Welding helmet', 'Leather gloves', 'Flame-resistant clothing', 'Safety glasses'],
    emergencyContacts: ['Fire Department: 911', 'Safety Officer: 555-0001', 'Plant Security: 555-0004'],
    documents: ['Hot_Work_Permit_v1.8.pdf', 'Fire_Watch_Checklist.pdf'],
    trainingRecords: 35,
    complianceRate: 95.2,
    incidentsLastYear: 1
  },
  {
    id: 4,
    name: 'Fall Protection System',
    code: 'FALL-001',
    description: 'Guidelines for working at heights and fall protection requirements',
    category: 'Fall Protection',
    riskLevel: 'High',
    isActive: true,
    isMandatory: true,
    version: '2.3',
    lastUpdated: '2024-02-10',
    createdBy: 'Safety Coordinator',
    approvedBy: 'Safety Director',
    approvalDate: '2024-02-15',
    reviewFrequency: 'Annual',
    nextReviewDate: '2025-02-15',
    applicableAreas: ['Rooftops', 'Elevated Platforms', 'Ladders', 'Scaffolding'],
    requiredTraining: ['Fall Protection', 'Harness Inspection', 'Ladder Safety'],
    estimatedTime: 20,
    steps: [
      'Assess work area',
      'Inspect equipment',
      'Don safety harness',
      'Secure anchor points',
      'Connect lifeline',
      'Begin work safely'
    ],
    requiredPPE: ['Full body harness', 'Hard hat', 'Non-slip footwear', 'Lifeline'],
    emergencyContacts: ['Emergency: 911', 'Safety Officer: 555-0001', 'Supervisor: 555-0002'],
    documents: ['Fall_Protection_v2.3.pdf', 'Harness_Inspection_Log.pdf'],
    trainingRecords: 52,
    complianceRate: 97.1,
    incidentsLastYear: 0
  },
  {
    id: 5,
    name: 'Chemical Handling Safety',
    code: 'CHEM-001',
    description: 'Safe handling, storage, and disposal of hazardous chemicals',
    category: 'Chemical Safety',
    riskLevel: 'Medium',
    isActive: true,
    isMandatory: false,
    version: '1.5',
    lastUpdated: '2024-01-15',
    createdBy: 'Environmental Officer',
    approvedBy: 'Safety Manager',
    approvalDate: '2024-01-20',
    reviewFrequency: 'Annual',
    nextReviewDate: '2025-01-20',
    applicableAreas: ['Chemical Storage', 'Laboratory', 'Maintenance Shop'],
    requiredTraining: ['HAZCOM Training', 'SDS Understanding', 'Spill Response'],
    estimatedTime: 25,
    steps: [
      'Review SDS',
      'Select proper PPE',
      'Prepare spill kit',
      'Handle carefully',
      'Proper disposal',
      'Document usage'
    ],
    requiredPPE: ['Chemical gloves', 'Safety goggles', 'Lab coat', 'Closed-toe shoes'],
    emergencyContacts: ['Poison Control: 1-800-222-1222', 'Safety Officer: 555-0001', 'Medical: 555-0003'],
    documents: ['Chemical_Safety_v1.5.pdf', 'SDS_Library.pdf', 'Spill_Response_Plan.pdf'],
    trainingRecords: 38,
    complianceRate: 92.8,
    incidentsLastYear: 2
  }
];

export default function SafetyProtocolsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [riskFilter, setRiskFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [selectedProtocol, setSelectedProtocol] = useState<SafetyProtocol | null>(null);
  const [safetyProtocolsData, setSafetyProtocolsData] = useState<SafetyProtocol[]>([]);
  const [filteredData, setFilteredData] = useState<SafetyProtocol[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [formData, setFormData] = useState<SafetyProtocolFormData>({
    name: '',
    code: '',
    description: '',
    category: 'Energy Isolation',
    riskLevel: 'Medium',
    isActive: true,
    isMandatory: false,
    version: '1.0',
    reviewFrequency: 'Annual',
    applicableAreas: [] as string[],
    requiredTraining: [] as string[],
    estimatedTime: 15,
    steps: [] as string[],
    requiredPPE: [] as string[],
    emergencyContacts: [] as string[],
    documents: [] as string[]
  });

  // Fetch safety protocols from API
  const fetchSafetyProtocols = async () => {
    try {
      setLoading(true);
      setError(null);
      const token = localStorage.getItem('token');

      if (!token) {
        setError('Authentication required. Please log in to access this page.');
        setSafetyProtocolsData([]);
        return;
      }

      const response = await fetch('http://localhost:5000/api/maintenance/safety-protocols?pageSize=1000', {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        if (response.status === 401) {
          setError('Authentication failed. Please log in again.');
        } else if (response.status === 403) {
          setError('Access denied. You do not have permission to view safety protocols.');
        } else {
          const errorText = await response.text();
          setError(`Failed to load safety protocols: ${errorText}`);
        }
        setSafetyProtocolsData([]);
        return;
      }

      const result = await response.json();
      console.log('Safety protocols API response:', result);
      const protocols = result.data || result.items || result || [];
      setSafetyProtocolsData(protocols);
    } catch (error) {
      console.error('Error fetching safety protocols:', error);
      setError('Network error occurred while fetching safety protocols.');
      setSafetyProtocolsData([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSafetyProtocols();
  }, []);

  useEffect(() => {
    let filtered = safetyProtocolsData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.category.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item =>
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    if (categoryFilter !== 'all') {
      filtered = filtered.filter(item => item.category === categoryFilter);
    }

    if (riskFilter !== 'all') {
      filtered = filtered.filter(item => item.riskLevel === riskFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, categoryFilter, riskFilter]);

  const handleCreate = async () => {
    try {
      const token = localStorage.getItem('token');

      if (!token) {
        setError('Authentication required. Please log in to create safety protocols.');
        return;
      }

      // Map form data to CreateSafetyProtocolDto structure
      const createDto = {
        name: formData.name,
        code: formData.code,
        description: formData.description || '',
        category: formData.category,
        riskLevel: formData.riskLevel,
        isActive: formData.isActive,
        isMandatory: formData.isMandatory,
        version: formData.version,
        reviewFrequency: formData.reviewFrequency,
        applicableAreas: formData.applicableAreas,
        requiredTraining: formData.requiredTraining,
        estimatedTime: formData.estimatedTime,
        steps: formData.steps,
        requiredPPE: formData.requiredPPE,
        emergencyContacts: formData.emergencyContacts,
        documents: formData.documents
      };

      console.log('Sending create request:', createDto);

      const response = await fetch('http://localhost:5000/api/maintenance/safety-protocols', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(createDto)
      });

      if (!response.ok) {
        const errorText = await response.text();
        setError(`Failed to create safety protocol: ${errorText}`);
        return;
      }

      // Refresh the list
      await fetchSafetyProtocols();
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error creating safety protocol:', error);
      setError('Failed to create safety protocol. Please try again.');
    }
  };

  const handleEdit = (protocol: SafetyProtocol) => {
    setSelectedProtocol(protocol);
    setFormData({
      name: protocol.name,
      code: protocol.code,
      description: protocol.description,
      category: protocol.category,
      riskLevel: protocol.riskLevel,
      isActive: protocol.isActive,
      isMandatory: protocol.isMandatory,
      version: protocol.version,
      reviewFrequency: protocol.reviewFrequency,
      applicableAreas: protocol.applicableAreas,
      requiredTraining: protocol.requiredTraining,
      estimatedTime: protocol.estimatedTime,
      steps: protocol.steps,
      requiredPPE: protocol.requiredPPE,
      emergencyContacts: protocol.emergencyContacts,
      documents: protocol.documents
    });
    setIsEditDialogOpen(true);
  };

  const handleView = (protocol: SafetyProtocol) => {
    setSelectedProtocol(protocol);
    setIsViewDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedProtocol?.id) return;

    try {
      const token = localStorage.getItem('token');

      if (!token) {
        setError('Authentication required. Please log in to update safety protocols.');
        return;
      }

      // Map form data to UpdateSafetyProtocolDto structure
      const updateDto = {
        name: formData.name,
        code: formData.code,
        description: formData.description || '',
        category: formData.category,
        riskLevel: formData.riskLevel,
        isActive: formData.isActive,
        isMandatory: formData.isMandatory,
        version: formData.version,
        reviewFrequency: formData.reviewFrequency,
        applicableAreas: formData.applicableAreas,
        requiredTraining: formData.requiredTraining,
        estimatedTime: formData.estimatedTime,
        steps: formData.steps,
        requiredPPE: formData.requiredPPE,
        emergencyContacts: formData.emergencyContacts,
        documents: formData.documents
      };

      console.log('Sending update request:', updateDto);

      const response = await fetch(`http://localhost:5000/api/maintenance/safety-protocols/${selectedProtocol.id}`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(updateDto)
      });

      if (!response.ok) {
        const errorText = await response.text();
        setError(`Failed to update safety protocol: ${errorText}`);
        return;
      }

      // Refresh the list
      await fetchSafetyProtocols();
      setIsEditDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error updating safety protocol:', error);
      setError('Failed to update safety protocol. Please try again.');
    }
  };

  const handleDelete = async (id: string | number) => {
    if (!confirm('Are you sure you want to delete this safety protocol?')) return;

    try {
      const token = localStorage.getItem('token');

      if (!token) {
        setError('Authentication required. Please log in to delete safety protocols.');
        return;
      }

      const response = await fetch(`http://localhost:5000/api/maintenance/safety-protocols/${id}`, {
        method: 'DELETE',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        const errorText = await response.text();
        setError(`Failed to delete safety protocol: ${errorText}`);
        return;
      }

      // Refresh the list
      await fetchSafetyProtocols();
    } catch (error) {
      console.error('Error deleting safety protocol:', error);
      setError('Failed to delete safety protocol. Please try again.');
    }
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      category: 'Energy Isolation',
      riskLevel: 'Medium',
      isActive: true,
      isMandatory: false,
      version: '1.0',
      reviewFrequency: 'Annual',
      applicableAreas: [],
      requiredTraining: [],
      estimatedTime: 15,
      steps: [],
      requiredPPE: [],
      emergencyContacts: [],
      documents: []
    });
    setSelectedProtocol(null);
  };

  const getRiskColor = (risk: string) => {
    switch (risk) {
      case 'Critical': return 'bg-red-100 text-red-800';
      case 'High': return 'bg-orange-100 text-orange-800';
      case 'Medium': return 'bg-yellow-100 text-yellow-800';
      case 'Low': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Safety Protocols</h1>
          <p className="text-muted-foreground">
            Manage maintenance safety procedures and compliance requirements
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Protocol
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[700px]">
            <DialogHeader>
              <DialogTitle>Add Safety Protocol</DialogTitle>
              <DialogDescription>
                Create a new safety protocol for maintenance operations.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Protocol Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                    placeholder="Enter protocol name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Protocol Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                    placeholder="e.g., LOTO-001"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  placeholder="Describe this safety protocol..."
                  rows={3}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="category">Category</Label>
                  <Select value={formData.category} onValueChange={(value) => setFormData({ ...formData, category: value })}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select category" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Energy Isolation">Energy Isolation</SelectItem>
                      <SelectItem value="Confined Space">Confined Space</SelectItem>
                      <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                      <SelectItem value="Fall Protection">Fall Protection</SelectItem>
                      <SelectItem value="Chemical Safety">Chemical Safety</SelectItem>
                      <SelectItem value="Electrical Safety">Electrical Safety</SelectItem>
                      <SelectItem value="Machine Safety">Machine Safety</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="riskLevel">Risk Level</Label>
                  <Select value={formData.riskLevel} onValueChange={(value) => setFormData({ ...formData, riskLevel: value })}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select risk level" />
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

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="version">Version</Label>
                  <Input
                    id="version"
                    value={formData.version}
                    onChange={(e) => setFormData({ ...formData, version: e.target.value })}
                    placeholder="1.0"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="estimatedTime">Estimated Time (minutes)</Label>
                  <Input
                    id="estimatedTime"
                    type="number"
                    value={formData.estimatedTime}
                    onChange={(e) => setFormData({ ...formData, estimatedTime: parseInt(e.target.value) || 15 })}
                    placeholder="15"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="reviewFrequency">Review Frequency</Label>
                <Select value={formData.reviewFrequency} onValueChange={(value) => setFormData({ ...formData, reviewFrequency: value })}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select frequency" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Quarterly">Quarterly</SelectItem>
                    <SelectItem value="Bi-Annual">Bi-Annual</SelectItem>
                    <SelectItem value="Annual">Annual</SelectItem>
                    <SelectItem value="Bi-Annual">Bi-Annual</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="mandatory"
                    checked={formData.isMandatory}
                    onCheckedChange={(checked) => setFormData({ ...formData, isMandatory: checked })}
                  />
                  <Label htmlFor="mandatory">Mandatory</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="active"
                    checked={formData.isActive}
                    onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
                  />
                  <Label htmlFor="active">Active</Label>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Add Protocol
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {/* Error Display */}
      {error && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center space-x-2 text-red-700">
              <AlertTriangle className="h-5 w-5" />
              <p className="font-medium">{error}</p>
            </div>
          </CardContent>
        </Card>
      )}

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
            <BreadcrumbLink href="/administration/maintenance">Maintenance Setup</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Safety Protocols</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{filteredData.length}</p>
                <p className="text-sm text-muted-foreground">Total Protocols</p>
              </div>
              <Shield className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(p => p.isMandatory).length}
                </p>
                <p className="text-sm text-muted-foreground">Mandatory</p>
              </div>
              <AlertTriangle className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {Math.round((filteredData.reduce((sum, p) => sum + p.complianceRate, 0) / filteredData.length) * 10) / 10 || 0}%
                </p>
                <p className="text-sm text-muted-foreground">Avg Compliance</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.reduce((sum, p) => sum + p.trainingRecords, 0)}
                </p>
                <p className="text-sm text-muted-foreground">Training Records</p>
              </div>
              <Users className="h-8 w-8 text-purple-500" />
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
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search protocols..."
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
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="inactive">Inactive</SelectItem>
              </SelectContent>
            </Select>

            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                <SelectItem value="Energy Isolation">Energy Isolation</SelectItem>
                <SelectItem value="Confined Space">Confined Space</SelectItem>
                <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                <SelectItem value="Fall Protection">Fall Protection</SelectItem>
                <SelectItem value="Chemical Safety">Chemical Safety</SelectItem>
              </SelectContent>
            </Select>

            <Select value={riskFilter} onValueChange={setRiskFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Risk Level" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Risk Levels</SelectItem>
                <SelectItem value="Critical">Critical</SelectItem>
                <SelectItem value="High">High</SelectItem>
                <SelectItem value="Medium">Medium</SelectItem>
                <SelectItem value="Low">Low</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Protocols List */}
      <Card>
        <CardHeader>
          <CardTitle>Safety Protocols</CardTitle>
          <CardDescription>
            {filteredData.length} protocol{filteredData.length === 1 ? '' : 's'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center justify-center py-8">
              <div className="text-muted-foreground">Loading safety protocols...</div>
            </div>
          ) : error ? (
            <div className="text-center py-8 text-red-600">{error}</div>
          ) : filteredData.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">No safety protocols found</div>
          ) : (
            <div className="space-y-4">
              {filteredData.map((protocol) => (
                <div key={protocol.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                  <div className="flex items-start justify-between">
                    <div className="space-y-3 flex-1">
                      <div className="flex items-center space-x-3">
                        <Shield className="h-5 w-5 text-blue-500" />
                        <h3 className="font-semibold">{protocol.name}</h3>
                        <Badge variant="outline">{protocol.code}</Badge>
                        <Badge className={getRiskColor(protocol.riskLevel)}>
                          {protocol.riskLevel} Risk
                        </Badge>
                        <Badge className={protocol.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                          {protocol.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                        {protocol.isMandatory && (
                          <Badge className="bg-red-100 text-red-800">Mandatory</Badge>
                        )}
                      </div>

                      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                        <div>
                          <span className="font-medium">Category:</span> {protocol.category}
                        </div>
                        <div>
                          <span className="font-medium">Version:</span> {protocol.version}
                        </div>
                        <div>
                          <span className="font-medium">Est. Time:</span> {protocol.estimatedTime}min
                        </div>
                        <div>
                          <span className="font-medium">Compliance:</span> {protocol.complianceRate}%
                        </div>
                        <div>
                          <span className="font-medium">Last Updated:</span> {protocol.lastUpdated}
                        </div>
                        <div>
                          <span className="font-medium">Next Review:</span> {protocol.nextReviewDate}
                        </div>
                        <div>
                          <span className="font-medium">Training Records:</span> {protocol.trainingRecords}
                        </div>
                        <div>
                          <span className="font-medium">Incidents:</span> {protocol.incidentsLastYear} last year
                        </div>
                      </div>

                      <p className="text-sm text-muted-foreground">{protocol.description}</p>

                      <div className="flex flex-wrap gap-2 text-xs">
                        <span className="font-medium">Applicable Areas:</span>
                        {protocol.applicableAreas.slice(0, 3).map((area, index) => (
                          <Badge key={index} variant="secondary" className="text-xs">
                            {area}
                          </Badge>
                        ))}
                        {protocol.applicableAreas.length > 3 && (
                          <Badge variant="secondary" className="text-xs">
                            +{protocol.applicableAreas.length - 3} more
                          </Badge>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center space-x-2">
                      <Button size="sm" variant="outline" onClick={() => handleView(protocol)}>
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => console.log('Download', protocol.id)}>
                        <Download className="h-4 w-4" />
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => handleEdit(protocol)}>
                        <Edit className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => handleDelete(protocol.id)}
                        className="text-red-600 hover:text-red-700"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                      <Button variant="outline" size="sm">
                        <MoreHorizontal className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* View Protocol Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="sm:max-w-[800px]">
          <DialogHeader>
            <DialogTitle>Protocol Details: {selectedProtocol?.name}</DialogTitle>
            <DialogDescription>
              Complete safety protocol information and procedures
            </DialogDescription>
          </DialogHeader>
          {selectedProtocol && (
            <div className="space-y-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Code</Label>
                  <p className="text-sm text-muted-foreground">{selectedProtocol.code}</p>
                </div>
                <div>
                  <Label>Version</Label>
                  <p className="text-sm text-muted-foreground">{selectedProtocol.version}</p>
                </div>
                <div>
                  <Label>Category</Label>
                  <p className="text-sm text-muted-foreground">{selectedProtocol.category}</p>
                </div>
                <div>
                  <Label>Risk Level</Label>
                  <Badge className={getRiskColor(selectedProtocol.riskLevel)}>
                    {selectedProtocol.riskLevel}
                  </Badge>
                </div>
              </div>

              <div>
                <Label>Description</Label>
                <p className="text-sm text-muted-foreground">{selectedProtocol.description}</p>
              </div>

              <div>
                <Label>Safety Steps ({selectedProtocol.steps.length})</Label>
                <div className="space-y-2 mt-2">
                  {selectedProtocol.steps.map((step, index) => (
                    <div key={index} className="flex items-center space-x-2 p-2 border rounded">
                      <span className="text-sm font-medium">{index + 1}.</span>
                      <span className="text-sm flex-1">{step}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div>
                <Label>Required PPE</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {selectedProtocol.requiredPPE.map((ppe, index) => (
                    <Badge key={index} variant="outline" className="text-xs">
                      {ppe}
                    </Badge>
                  ))}
                </div>
              </div>

              <div>
                <Label>Emergency Contacts</Label>
                <div className="space-y-1 mt-2">
                  {selectedProtocol.emergencyContacts.map((contact, index) => (
                    <p key={index} className="text-sm text-muted-foreground">{contact}</p>
                  ))}
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Dialog - Same fields as create */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[700px]">
          <DialogHeader>
            <DialogTitle>Edit Safety Protocol</DialogTitle>
            <DialogDescription>
              Update the safety protocol information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Protocol Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  placeholder="Enter protocol name"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Protocol Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                  placeholder="e.g., LOTO-001"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                placeholder="Describe this safety protocol..."
                rows={3}
              />
            </div>

            <div className="flex items-center space-x-2">
              <Switch
                id="edit-active"
                checked={formData.isActive}
                onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
              />
              <Label htmlFor="edit-active">Active</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>Update Protocol</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
