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
  Search,
  Eye,
  MoreHorizontal,
  Users,
  Award,
  BookOpen,
  Zap,
  Settings,
  Wrench,
  Gauge,
  Star,
  Info,
  UserCheck
} from 'lucide-react';

// Mock data for technical skills
const technicalSkillsData = [
  {
    id: 1,
    name: 'Electrical Systems',
    code: 'ELEC-001',
    description: 'Installation, maintenance, and repair of electrical systems and components',
    category: 'Electrical',
    skillLevel: 'Advanced',
    isActive: true,
    prerequisites: ['Basic Electrical Safety', 'Circuit Analysis'],
    certifications: ['Electrical License', 'NFPA 70E'],
    estimatedLearningHours: 120,
    complexity: 'High',
    riskLevel: 'High',
    toolsRequired: ['Multimeter', 'Wire strippers', 'Electrical tester', 'Oscilloscope'],
    safetyRequirements: 'Lockout/Tagout procedures, Personal protective equipment, Arc flash protection',
    competencyAreas: ['Wiring', 'Motor control', 'Panel installation', 'Troubleshooting'],
    relatedMaintenanceTypes: ['Preventive Electrical', 'Emergency Electrical Repair'],
    createdBy: 'John Supervisor',
    createdDate: '2024-01-15',
    techniciansCount: 8,
    averageRating: 4.2
  },
  {
    id: 2,
    name: 'HVAC Maintenance',
    code: 'HVAC-001',
    description: 'Heating, ventilation, and air conditioning systems maintenance and repair',
    category: 'HVAC',
    skillLevel: 'Intermediate',
    isActive: true,
    prerequisites: ['Basic Mechanical Systems', 'Refrigeration Principles'],
    certifications: ['EPA 608', 'HVAC Excellence'],
    estimatedLearningHours: 80,
    complexity: 'Medium',
    riskLevel: 'Medium',
    toolsRequired: ['Gauges', 'Recovery machine', 'Leak detectors', 'Thermometers'],
    safetyRequirements: 'Refrigerant handling safety, Confined space entry, Fall protection',
    competencyAreas: ['Filter replacement', 'Coil cleaning', 'Refrigerant recovery', 'System diagnostics'],
    relatedMaintenanceTypes: ['HVAC Preventive', 'Emergency HVAC'],
    createdBy: 'Sarah Manager',
    createdDate: '2024-01-20',
    techniciansCount: 12,
    averageRating: 4.5
  },
  {
    id: 3,
    name: 'Plumbing Systems',
    code: 'PLUMB-001',
    description: 'Installation and maintenance of water, sewer, and gas piping systems',
    category: 'Plumbing',
    skillLevel: 'Intermediate',
    isActive: true,
    prerequisites: ['Basic Hand Tools', 'Pipe Fitting Basics'],
    certifications: ['Plumbing License', 'Backflow Certification'],
    estimatedLearningHours: 60,
    complexity: 'Medium',
    riskLevel: 'Medium',
    toolsRequired: ['Pipe wrenches', 'Torch', 'Snake', 'Pressure tester'],
    safetyRequirements: 'Eye protection, Proper ventilation, Hot work permits',
    competencyAreas: ['Pipe joining', 'Leak repair', 'Fixture installation', 'Drain cleaning'],
    relatedMaintenanceTypes: ['Plumbing Preventive', 'Emergency Plumbing'],
    createdBy: 'Mike Lead',
    createdDate: '2024-02-01',
    techniciansCount: 6,
    averageRating: 4.0
  },
  {
    id: 4,
    name: 'Industrial Automation',
    code: 'AUTO-001',
    description: 'Programmable logic controllers, sensors, and automated control systems',
    category: 'Automation',
    skillLevel: 'Expert',
    isActive: true,
    prerequisites: ['Electrical Systems', 'Computer Programming Basics', 'Control Theory'],
    certifications: ['PLC Programming', 'HMI Design', 'Industrial Networks'],
    estimatedLearningHours: 200,
    complexity: 'Very High',
    riskLevel: 'High',
    toolsRequired: ['Programming software', 'Logic analyzer', 'Network tester', 'Laptop computer'],
    safetyRequirements: 'System lockout procedures, Software backup protocols, Change control procedures',
    competencyAreas: ['PLC programming', 'HMI development', 'Network configuration', 'System commissioning'],
    relatedMaintenanceTypes: ['Automation Preventive', 'Control System Upgrades'],
    createdBy: 'Lisa Engineer',
    createdDate: '2024-02-10',
    techniciansCount: 4,
    averageRating: 4.8
  },
  {
    id: 5,
    name: 'Mechanical Systems',
    code: 'MECH-001',
    description: 'Mechanical equipment maintenance including pumps, motors, and conveyors',
    category: 'Mechanical',
    skillLevel: 'Basic',
    isActive: true,
    prerequisites: ['Basic Tool Usage', 'Safety Training'],
    certifications: ['Mechanical Maintenance Certificate'],
    estimatedLearningHours: 40,
    complexity: 'Low',
    riskLevel: 'Low',
    toolsRequired: ['Hand tools', 'Grease gun', 'Torque wrench', 'Alignment tools'],
    safetyRequirements: 'Lockout/Tagout, Personal protective equipment, Lifting safety',
    competencyAreas: ['Lubrication', 'Belt replacement', 'Bearing maintenance', 'Basic alignment'],
    relatedMaintenanceTypes: ['Mechanical Preventive', 'Lubrication'],
    createdBy: 'Tom Supervisor',
    createdDate: '2024-02-15',
    techniciansCount: 15,
    averageRating: 3.8
  }
];

export default function TechnicalSkillsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [levelFilter, setLevelFilter] = useState('all');
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [selectedSkill, setSelectedSkill] = useState(null);
  const [filteredData, setFilteredData] = useState(technicalSkillsData);
  const [isLoading, setIsLoading] = useState(false);
  
  // Simulate fetching data from HR Skills module
  useEffect(() => {
    const fetchSkillsFromHR = async () => {
      setIsLoading(true);
      try {
        // Simulate API call to HR Skills module
        // In real implementation, this would call /api/hr/skills?category=technical
        setTimeout(() => {
          setFilteredData(technicalSkillsData);
          setIsLoading(false);
        }, 1000);
      } catch (error) {
        console.error('Failed to fetch technical skills from HR Skills module:', error);
        // Fallback to mock data
        setFilteredData(technicalSkillsData);
        setIsLoading(false);
      }
    };
    
    fetchSkillsFromHR();
  }, []);
  
  // Form state
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    description: '',
    category: 'Electrical',
    skillLevel: 'Basic',
    isActive: true,
    prerequisites: [],
    certifications: [],
    estimatedLearningHours: 40,
    complexity: 'Low',
    riskLevel: 'Low',
    toolsRequired: [],
    safetyRequirements: '',
    competencyAreas: [],
    relatedMaintenanceTypes: []
  });

  useEffect(() => {
    let filtered = technicalSkillsData;

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

    if (levelFilter !== 'all') {
      filtered = filtered.filter(item => item.skillLevel === levelFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, categoryFilter, levelFilter]);

  const handleCreate = () => {
    console.log('Creating technical skill:', formData);
    setIsCreateDialogOpen(false);
    resetForm();
  };

  const handleEdit = (skill: any) => {
    setSelectedSkill(skill);
    setFormData({
      name: skill.name,
      code: skill.code,
      description: skill.description,
      category: skill.category,
      skillLevel: skill.skillLevel,
      isActive: skill.isActive,
      prerequisites: skill.prerequisites,
      certifications: skill.certifications,
      estimatedLearningHours: skill.estimatedLearningHours,
      complexity: skill.complexity,
      riskLevel: skill.riskLevel,
      toolsRequired: skill.toolsRequired,
      safetyRequirements: skill.safetyRequirements,
      competencyAreas: skill.competencyAreas,
      relatedMaintenanceTypes: skill.relatedMaintenanceTypes
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = () => {
    console.log('Updating technical skill:', selectedSkill?.id, formData);
    setIsEditDialogOpen(false);
    resetForm();
  };

  const handleView = (skill: any) => {
    setSelectedSkill(skill);
    setIsViewDialogOpen(true);
  };

  const handleDelete = (id: number) => {
    console.log('Deleting technical skill:', id);
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      category: 'Electrical',
      skillLevel: 'Basic',
      isActive: true,
      prerequisites: [],
      certifications: [],
      estimatedLearningHours: 40,
      complexity: 'Low',
      riskLevel: 'Low',
      toolsRequired: [],
      safetyRequirements: '',
      competencyAreas: [],
      relatedMaintenanceTypes: []
    });
    setSelectedSkill(null);
  };

  const getSkillLevelColor = (level: string) => {
    switch (level) {
      case 'Expert': return 'bg-purple-100 text-purple-800';
      case 'Advanced': return 'bg-blue-100 text-blue-800';
      case 'Intermediate': return 'bg-yellow-100 text-yellow-800';
      case 'Basic': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const getComplexityColor = (complexity: string) => {
    switch (complexity) {
      case 'Very High': return 'bg-red-100 text-red-800';
      case 'High': return 'bg-orange-100 text-orange-800';
      case 'Medium': return 'bg-yellow-100 text-yellow-800';
      case 'Low': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const getRiskColor = (risk: string) => {
    switch (risk) {
      case 'High': return 'bg-red-100 text-red-800';
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
          <h1 className="text-3xl font-bold tracking-tight">Technical Skills</h1>
          <p className="text-muted-foreground">
            View technician skills and competency requirements
          </p>
        </div>
      </div>
      
      {/* HR Skills Integration Notice */}
      <Card className="border-blue-200 bg-blue-50">
        <CardContent className="pt-6">
          <div className="flex items-start space-x-3">
            <Info className="h-5 w-5 text-blue-600 mt-0.5" />
            <div>
              <h3 className="font-semibold text-blue-900">HR Skills Module Integration</h3>
              <p className="text-sm text-blue-800 mt-1">
                Technical skills data is synchronized from the HR Skills module. To add, modify, or manage 
                skill definitions, certifications, and competency requirements, please use the 
                <strong> HR Skills Management</strong> section. Changes made there will automatically 
                appear here within a few minutes.
              </p>
              <div className="mt-2">
                <Button variant="outline" size="sm" className="text-blue-700 border-blue-300 hover:bg-blue-100">
                  <UserCheck className="mr-2 h-4 w-4" />
                  Go to HR Skills Management
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
      
      {isLoading && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
              <span className="ml-3 text-muted-foreground">Loading skills from HR module...</span>
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
            <BreadcrumbPage>Technical Skills</BreadcrumbPage>
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
                <p className="text-sm text-muted-foreground">Total Skills</p>
              </div>
              <Award className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(s => s.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <BookOpen className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.reduce((sum, s) => sum + s.techniciansCount, 0)}
                </p>
                <p className="text-sm text-muted-foreground">Skilled Technicians</p>
              </div>
              <Users className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {Math.round((filteredData.reduce((sum, s) => sum + s.averageRating, 0) / filteredData.length) * 10) / 10 || 0}
                </p>
                <p className="text-sm text-muted-foreground">Avg Rating</p>
              </div>
              <Star className="h-8 w-8 text-purple-500" />
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
                placeholder="Search skills..."
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
                <SelectItem value="Electrical">Electrical</SelectItem>
                <SelectItem value="HVAC">HVAC</SelectItem>
                <SelectItem value="Plumbing">Plumbing</SelectItem>
                <SelectItem value="Mechanical">Mechanical</SelectItem>
                <SelectItem value="Automation">Automation</SelectItem>
              </SelectContent>
            </Select>

            <Select value={levelFilter} onValueChange={setLevelFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Skill Level" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Levels</SelectItem>
                <SelectItem value="Basic">Basic</SelectItem>
                <SelectItem value="Intermediate">Intermediate</SelectItem>
                <SelectItem value="Advanced">Advanced</SelectItem>
                <SelectItem value="Expert">Expert</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Skills List */}
      <Card>
        <CardHeader>
          <CardTitle>Technical Skills</CardTitle>
          <CardDescription>
            {filteredData.length} skill{filteredData.length === 1 ? '' : 's'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {filteredData.map((skill) => (
              <div key={skill.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      <Award className="h-5 w-5 text-blue-500" />
                      <h3 className="font-semibold">{skill.name}</h3>
                      <Badge variant="outline">{skill.code}</Badge>
                      <Badge className={getSkillLevelColor(skill.skillLevel)}>
                        {skill.skillLevel}
                      </Badge>
                      <Badge className={getComplexityColor(skill.complexity)}>
                        {skill.complexity} Complexity
                      </Badge>
                      <Badge className={getRiskColor(skill.riskLevel)}>
                        {skill.riskLevel} Risk
                      </Badge>
                      <Badge className={skill.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                        {skill.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div>
                        <span className="font-medium">Category:</span> {skill.category}
                      </div>
                      <div>
                        <span className="font-medium">Learning Hours:</span> {skill.estimatedLearningHours}h
                      </div>
                      <div>
                        <span className="font-medium">Technicians:</span> {skill.techniciansCount}
                      </div>
                      <div>
                        <span className="font-medium">Rating:</span> {skill.averageRating}/5.0 ⭐
                      </div>
                      <div>
                        <span className="font-medium">Prerequisites:</span> {skill.prerequisites.length}
                      </div>
                      <div>
                        <span className="font-medium">Certifications:</span> {skill.certifications.length}
                      </div>
                      <div>
                        <span className="font-medium">Tools:</span> {skill.toolsRequired.length}
                      </div>
                      <div>
                        <span className="font-medium">Competencies:</span> {skill.competencyAreas.length}
                      </div>
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{skill.description}</p>
                    
                    {skill.safetyRequirements && (
                      <div className="text-sm">
                        <span className="font-medium text-red-600">Safety Requirements:</span>
                        <p className="text-muted-foreground mt-1">{skill.safetyRequirements}</p>
                      </div>
                    )}
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    <Button size="sm" variant="outline" onClick={() => handleView(skill)}>
                      <Eye className="h-4 w-4" />
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

      {/* View Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="sm:max-w-[800px]">
          <DialogHeader>
            <DialogTitle>Skill Details: {selectedSkill?.name}</DialogTitle>
            <DialogDescription>
              Complete technical skill information and requirements
            </DialogDescription>
          </DialogHeader>
          {selectedSkill && (
            <div className="space-y-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Code</Label>
                  <p className="text-sm text-muted-foreground">{selectedSkill.code}</p>
                </div>
                <div>
                  <Label>Category</Label>
                  <p className="text-sm text-muted-foreground">{selectedSkill.category}</p>
                </div>
                <div>
                  <Label>Skill Level</Label>
                  <Badge className={getSkillLevelColor(selectedSkill.skillLevel)}>
                    {selectedSkill.skillLevel}
                  </Badge>
                </div>
                <div>
                  <Label>Learning Hours</Label>
                  <p className="text-sm text-muted-foreground">{selectedSkill.estimatedLearningHours}h</p>
                </div>
              </div>
              
              <div>
                <Label>Description</Label>
                <p className="text-sm text-muted-foreground">{selectedSkill.description}</p>
              </div>

              <div>
                <Label>Prerequisites ({selectedSkill.prerequisites.length})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {selectedSkill.prerequisites.map((prereq, index) => (
                    <Badge key={index} variant="outline" className="text-xs">
                      {prereq}
                    </Badge>
                  ))}
                </div>
              </div>

              <div>
                <Label>Certifications ({selectedSkill.certifications.length})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {selectedSkill.certifications.map((cert, index) => (
                    <Badge key={index} variant="outline" className="text-xs bg-blue-50 text-blue-700">
                      {cert}
                    </Badge>
                  ))}
                </div>
              </div>

              <div>
                <Label>Tools Required ({selectedSkill.toolsRequired.length})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {selectedSkill.toolsRequired.map((tool, index) => (
                    <Badge key={index} variant="secondary" className="text-xs">
                      {tool}
                    </Badge>
                  ))}
                </div>
              </div>

              <div>
                <Label>Competency Areas ({selectedSkill.competencyAreas.length})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {selectedSkill.competencyAreas.map((area, index) => (
                    <Badge key={index} variant="outline" className="text-xs bg-green-50 text-green-700">
                      {area}
                    </Badge>
                  ))}
                </div>
              </div>

              {selectedSkill.safetyRequirements && (
                <div>
                  <Label className="text-red-600">Safety Requirements</Label>
                  <p className="text-sm text-muted-foreground mt-1">{selectedSkill.safetyRequirements}</p>
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