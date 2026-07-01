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
import { Plus, Edit, Trash2 } from 'lucide-react';

// Skills interface
interface Skill {
  id: string | number;
  name: string;
  code: string;
  description?: string;
  category: string;
  skillLevel: string;
  isActive: boolean;
  prerequisites?: string[];
  certifications?: string[];
  estimatedLearningHours?: number;
  complexity?: string;
  riskLevel?: string;
  toolsRequired?: string[];
  safetyRequirements?: string;
  competencyAreas?: string[];
  relatedMaintenanceTypes?: string[];
  createdBy?: string;
  createdDate?: string;
  techniciansCount?: number;
  averageRating?: number;
}

interface TechnicalSkillFormData {
  name: string;
  code: string;
  description: string;
  category: string;
  skillLevel: string;
  isActive: boolean;
  prerequisites: string[];
  certifications: string[];
  estimatedLearningHours: number;
  complexity: string;
  riskLevel: string;
  toolsRequired: string[];
  safetyRequirements: string;
  competencyAreas: string[];
  relatedMaintenanceTypes: string[];
}

export default function TechnicalSkillsPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [levelFilter, setLevelFilter] = useState('all');
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedSkill, setSelectedSkill] = useState<Skill | null>(null);
  const [skillsData, setSkillsData] = useState<Skill[]>([]);
  const [filteredData, setFilteredData] = useState<Skill[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  
  // Fetch skills from API
  const fetchSkills = async () => {
    try {
      setIsLoading(true);
      setError(null);
      const token = localStorage.getItem('token');
      const response = await fetch('/api/maintenance/technical-skills?pageSize=1000', {
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        }
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch skills');
      }
      
      const data = await response.json();
      setSkillsData(data.data || data || []);
    } catch (error) {
      console.error('Error fetching skills:', error);
      setError('Failed to load skills. Please try again later.');
      setSkillsData([]);
    } finally {
      setIsLoading(false);
    }
  };
  
  useEffect(() => {
    fetchSkills();
  }, []);
  
  // Form state
  const [formData, setFormData] = useState<TechnicalSkillFormData>({
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
    let filtered = skillsData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        (item.description && item.description.toLowerCase().includes(searchTerm.toLowerCase())) ||
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
  }, [searchTerm, statusFilter, categoryFilter, levelFilter, skillsData]);

  const handleCreate = async () => {
    try {
      const token = localStorage.getItem('token');
      const response = await fetch('/api/maintenance/technical-skills', {
        method: 'POST',
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(formData)
      });
      
      if (!response.ok) {
        throw new Error('Failed to create skill');
      }
      
      // Refresh the list
      await fetchSkills();
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error creating skill:', error);
      setError('Failed to create skill. Please try again.');
    }
  };

  const handleEdit = (skill: Skill) => {
    setSelectedSkill(skill);
    setFormData({
      name: skill.name,
      code: skill.code,
      description: skill.description || '',
      category: skill.category,
      skillLevel: skill.skillLevel,
      isActive: skill.isActive,
      prerequisites: skill.prerequisites || [],
      certifications: skill.certifications || [],
      estimatedLearningHours: skill.estimatedLearningHours || 40,
      complexity: skill.complexity || 'Low',
      riskLevel: skill.riskLevel || 'Low',
      toolsRequired: skill.toolsRequired || [],
      safetyRequirements: skill.safetyRequirements || '',
      competencyAreas: skill.competencyAreas || [],
      relatedMaintenanceTypes: skill.relatedMaintenanceTypes || []
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedSkill?.id) return;
    
    try {
      const token = localStorage.getItem('token');
      const response = await fetch(`/api/maintenance/technical-skills/${selectedSkill.id}`, {
        method: 'PUT',
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(formData)
      });
      
      if (!response.ok) {
        throw new Error('Failed to update skill');
      }
      
      // Refresh the list
      await fetchSkills();
      setIsEditDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error updating skill:', error);
      setError('Failed to update skill. Please try again.');
    }
  };

  const handleView = (skill: Skill) => {
    setSelectedSkill(skill);
    setIsViewDialogOpen(true);
  };

  const handleDelete = async (id: string | number) => {
    if (!confirm('Are you sure you want to delete this skill?')) return;
    
    try {
      const token = localStorage.getItem('token');
      const response = await fetch(`/api/maintenance/technical-skills/${id}`, {
        method: 'DELETE',
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        }
      });
      
      if (!response.ok) {
        throw new Error('Failed to delete skill');
      }
      
      // Refresh the list
      await fetchSkills();
    } catch (error) {
      console.error('Error deleting skill:', error);
      setError('Failed to delete skill. Please try again.');
    }
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
            Manage technician skills and competency requirements
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Award className="mr-2 h-4 w-4" />
              Add Skill
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Technical Skill</DialogTitle>
              <DialogDescription>
                Create a new technical skill for maintenance activities.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Skill Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="e.g., Electrical Systems"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Skill Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                    placeholder="e.g., ELEC-001"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Description of the skill..."
                  rows={3}
                />
              </div>
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="category">Category</Label>
                  <Select value={formData.category} onValueChange={(value) => setFormData({...formData, category: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select category" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Electrical">Electrical</SelectItem>
                      <SelectItem value="HVAC">HVAC</SelectItem>
                      <SelectItem value="Plumbing">Plumbing</SelectItem>
                      <SelectItem value="Mechanical">Mechanical</SelectItem>
                      <SelectItem value="Automation">Automation</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="skillLevel">Skill Level</Label>
                  <Select value={formData.skillLevel} onValueChange={(value) => setFormData({...formData, skillLevel: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select level" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Basic">Basic</SelectItem>
                      <SelectItem value="Intermediate">Intermediate</SelectItem>
                      <SelectItem value="Advanced">Advanced</SelectItem>
                      <SelectItem value="Expert">Expert</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="hours">Learning Hours</Label>
                  <Input
                    id="hours"
                    type="number"
                    value={formData.estimatedLearningHours}
                    onChange={(e) => setFormData({...formData, estimatedLearningHours: parseInt(e.target.value)})}
                  />
                </div>
              </div>
              
              <div className="flex items-center space-x-2">
                <Switch
                  id="active"
                  checked={formData.isActive}
                  onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                />
                <Label htmlFor="active">Active</Label>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate}>
                Create Skill
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
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
                  {filteredData.reduce((sum, s) => sum + (s.techniciansCount ?? 0), 0)}
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
                  {Math.round((filteredData.reduce((sum, s) => sum + (s.averageRating ?? 0), 0) / filteredData.length) * 10) / 10 || 0}
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
          {isLoading ? (
            <div className="text-center py-4">Loading skills...</div>
          ) : error ? (
            <div className="text-center py-4 text-red-600">{error}</div>
          ) : filteredData.length === 0 ? (
            <div className="text-center py-4 text-muted-foreground">No skills found</div>
          ) : (
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
                      <Badge className={getComplexityColor(skill.complexity ?? 'Low')}>
                        {(skill.complexity ?? 'Low')} Complexity
                      </Badge>
                      <Badge className={getRiskColor(skill.riskLevel ?? 'Low')}>
                        {(skill.riskLevel ?? 'Low')} Risk
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
                        <span className="font-medium">Technicians:</span> {skill.techniciansCount ?? 0}
                      </div>
                      <div>
                        <span className="font-medium">Rating:</span> {skill.averageRating ?? 0}/5.0 ⭐
                      </div>
                      <div>
                        <span className="font-medium">Prerequisites:</span> {skill.prerequisites?.length ?? 0}
                      </div>
                      <div>
                        <span className="font-medium">Certifications:</span> {skill.certifications?.length ?? 0}
                      </div>
                      <div>
                        <span className="font-medium">Tools:</span> {skill.toolsRequired?.length ?? 0}
                      </div>
                      <div>
                        <span className="font-medium">Competencies:</span> {skill.competencyAreas?.length ?? 0}
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
                    <Button size="sm" variant="outline" onClick={() => handleEdit(skill)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button 
                      size="sm" 
                      variant="outline" 
                      className="text-red-600 hover:text-red-700"
                      onClick={() => handleDelete(skill.id)}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Edit Technical Skill</DialogTitle>
            <DialogDescription>
              Update the technical skill information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Skill Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="e.g., Electrical Systems"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Skill Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., ELEC-001"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Description of the skill..."
                rows={3}
              />
            </div>
            
            <div className="grid grid-cols-3 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-category">Category</Label>
                <Select value={formData.category} onValueChange={(value) => setFormData({...formData, category: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select category" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Electrical">Electrical</SelectItem>
                    <SelectItem value="HVAC">HVAC</SelectItem>
                    <SelectItem value="Plumbing">Plumbing</SelectItem>
                    <SelectItem value="Mechanical">Mechanical</SelectItem>
                    <SelectItem value="Automation">Automation</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-skillLevel">Skill Level</Label>
                <Select value={formData.skillLevel} onValueChange={(value) => setFormData({...formData, skillLevel: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select level" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Basic">Basic</SelectItem>
                    <SelectItem value="Intermediate">Intermediate</SelectItem>
                    <SelectItem value="Advanced">Advanced</SelectItem>
                    <SelectItem value="Expert">Expert</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-hours">Learning Hours</Label>
                <Input
                  id="edit-hours"
                  type="number"
                  value={formData.estimatedLearningHours}
                  onChange={(e) => setFormData({...formData, estimatedLearningHours: parseInt(e.target.value)})}
                />
              </div>
            </div>
            
            <div className="flex items-center space-x-2">
              <Switch
                id="edit-active"
                checked={formData.isActive}
                onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
              />
              <Label htmlFor="edit-active">Active</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate}>
              Update Skill
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
                <Label>Prerequisites ({selectedSkill.prerequisites?.length ?? 0})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {(selectedSkill.prerequisites ?? []).map((prereq, index) => (
                    <Badge key={index} variant="outline" className="text-xs">
                      {prereq}
                    </Badge>
                  ))}
                </div>
              </div>

              <div>
                <Label>Certifications ({selectedSkill.certifications?.length ?? 0})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {(selectedSkill.certifications ?? []).map((cert, index) => (
                    <Badge key={index} variant="outline" className="text-xs bg-blue-50 text-blue-700">
                      {cert}
                    </Badge>
                  ))}
                </div>
              </div>

              <div>
                <Label>Tools Required ({selectedSkill.toolsRequired?.length ?? 0})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {(selectedSkill.toolsRequired ?? []).map((tool, index) => (
                    <Badge key={index} variant="secondary" className="text-xs">
                      {tool}
                    </Badge>
                  ))}
                </div>
              </div>

              <div>
                <Label>Competency Areas ({selectedSkill.competencyAreas?.length ?? 0})</Label>
                <div className="flex flex-wrap gap-1 mt-2">
                  {(selectedSkill.competencyAreas ?? []).map((area, index) => (
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
