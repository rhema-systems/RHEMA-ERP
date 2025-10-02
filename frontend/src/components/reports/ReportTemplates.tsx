"use client"

import React, { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Input } from '../ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { reportsService, ReportTemplate } from '../../services/reports'
import { useToast } from '../../hooks/use-toast'
import { useIsClient } from '../../lib/ssr-utils'
import { 
  FileText, 
  Search, 
  Plus, 
  Eye, 
  Edit, 
  Trash2, 
  Copy, 
  Star,
  StarOff,
  Filter,
  BarChart3,
  PieChart,
  LineChart,
  Table2,
  Calendar,
  Users,
  DollarSign,
  Package,
  TrendingUp,
  Loader2
} from 'lucide-react'

interface ReportTemplatesProps {
  onCreateFromTemplate?: (templateId: string) => void;
}

const ReportTemplates: React.FC<ReportTemplatesProps> = ({ onCreateFromTemplate }) => {
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedCategory, setSelectedCategory] = useState('all')
  const [selectedType, setSelectedType] = useState('all')
  const [showFavoritesOnly, setShowFavoritesOnly] = useState(false)
  const isClient = useIsClient()
  
  const { toast } = useToast()

  // Fetch templates from API
  const {
    data: templates = [],
    isLoading,
    error
  } = useQuery({
    queryKey: ['reportTemplates', selectedCategory !== 'all' ? selectedCategory : undefined],
    queryFn: () => reportsService.getReportTemplates(
      selectedCategory !== 'all' ? selectedCategory : undefined
    ),
    refetchOnWindowFocus: false,
  })


  const categories = [
    { value: 'all', label: 'All Categories' },
    { value: 'financial', label: 'Financial' },
    { value: 'sales', label: 'Sales' },
    { value: 'users', label: 'Users' },
    { value: 'tenants', label: 'Tenants' },
    { value: 'performance', label: 'Performance' }
  ]

  const types = [
    { value: 'all', label: 'All Types' },
    { value: 'table', label: 'Table Reports' },
    { value: 'chart', label: 'Charts' },
    { value: 'dashboard', label: 'Dashboards' }
  ]

  const getTypeIcon = (type: string, chartType?: string) => {
    switch (type) {
      case 'table':
        return <Table2 className="h-4 w-4" />
      case 'chart':
        if (chartType === 'pie') return <PieChart className="h-4 w-4" />
        if (chartType === 'line') return <LineChart className="h-4 w-4" />
        return <BarChart3 className="h-4 w-4" />
      case 'dashboard':
        return <TrendingUp className="h-4 w-4" />
      default:
        return <FileText className="h-4 w-4" />
    }
  }

  const getCategoryColor = (category: string) => {
    switch (category) {
      case 'financial': return 'bg-green-100 text-green-800'
      case 'sales': return 'bg-blue-100 text-blue-800'
      case 'users': return 'bg-purple-100 text-purple-800'
      case 'tenants': return 'bg-orange-100 text-orange-800'
      case 'performance': return 'bg-red-100 text-red-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  }

  const filteredTemplates = templates.filter(template => {
    const matchesSearch = template.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         template.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         (template.tags && template.tags.some(tag => tag.toLowerCase().includes(searchQuery.toLowerCase())))
    
    const matchesCategory = selectedCategory === 'all' || template.category === selectedCategory
    const matchesType = selectedType === 'all' || template.type === selectedType
    // Note: API template doesn't have isFavorite, so we'll skip favorites filtering for now
    const matchesFavorites = !showFavoritesOnly // || template.isFavorite

    return matchesSearch && matchesCategory && matchesType && matchesFavorites
  })

  const toggleFavorite = (templateId: string) => {
    // TODO: Implement favorite toggle
    console.log('Toggle favorite for template:', templateId)
  }

  const duplicateTemplate = (templateId: string) => {
    // TODO: Implement template duplication
    console.log('Duplicate template:', templateId)
  }

  const deleteTemplate = (templateId: string) => {
    // TODO: Implement template deletion
    console.log('Delete template:', templateId)
  }

  const previewTemplate = (templateId: string) => {
    // TODO: Implement template preview
    console.log('Preview template:', templateId)
  }

  const editTemplate = (templateId: string) => {
    // TODO: Implement template editing
    console.log('Edit template:', templateId)
  }

  const useTemplate = (templateId: string) => {
    try {
      toast({
        title: 'Creating Report from Template',
        description: 'Opening report builder with selected template...',
      })
      
      // Call the callback to open report builder with template
      if (onCreateFromTemplate) {
        onCreateFromTemplate(templateId);
      }
    } catch (error) {
      toast({
        title: 'Error',
        description: 'Failed to create report from template',
        variant: 'destructive',
      })
    }
  }

  if (error) {
    return (
      <div className="text-center py-8">
        <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
        <h3 className="text-lg font-medium mb-2">Error Loading Templates</h3>
        <p className="text-muted-foreground mb-4">
          Failed to load report templates. Please try again.
        </p>
        <Button onClick={() => window.location.reload()}>
          Retry
        </Button>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h2 className="text-2xl font-bold">Report Templates</h2>
          <p className="text-muted-foreground">Pre-built and custom report templates</p>
        </div>
        
        <div className="flex items-center gap-3">
          <Button>
            <Plus className="h-4 w-4 mr-2" />
            Create Template
          </Button>
        </div>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="p-6">
          <div className="flex flex-col lg:flex-row gap-4">
            {/* Search */}
            <div className="flex-1">
              <div className="relative">
                <Search className="h-4 w-4 absolute left-3 top-3 text-muted-foreground" />
                <Input
                  placeholder="Search templates..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-10"
                />
              </div>
            </div>
            
            {/* Category Filter */}
            <Select value={selectedCategory} onValueChange={setSelectedCategory}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {categories.map(category => (
                  <SelectItem key={category.value} value={category.value}>
                    {category.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            
            {/* Type Filter */}
            <Select value={selectedType} onValueChange={setSelectedType}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {types.map(type => (
                  <SelectItem key={type.value} value={type.value}>
                    {type.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            
            {/* Favorites Toggle */}
            <Button
              variant={showFavoritesOnly ? "default" : "outline"}
              onClick={() => setShowFavoritesOnly(!showFavoritesOnly)}
            >
              <Star className={`h-4 w-4 mr-2 ${showFavoritesOnly ? 'fill-current' : ''}`} />
              Favorites
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Templates Grid */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {[...Array(6)].map((_, i) => (
            <Card key={i} className="animate-pulse">
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-2">
                    <div className="w-6 h-6 bg-gray-200 rounded"></div>
                    <div>
                      <div className="h-5 bg-gray-200 rounded w-32 mb-2"></div>
                      <div className="h-4 bg-gray-200 rounded w-20"></div>
                    </div>
                  </div>
                  <div className="w-6 h-6 bg-gray-200 rounded"></div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="h-4 bg-gray-200 rounded w-full"></div>
                <div className="h-4 bg-gray-200 rounded w-3/4"></div>
                <div className="h-8 bg-gray-200 rounded w-full"></div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredTemplates.map(template => (
          <Card key={template.id} className="hover:shadow-md transition-shadow">
            <CardHeader className="pb-3">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-2">
                  {getTypeIcon(template.type, template.chartType)}
                  <div>
                    <CardTitle className="text-lg">{template.name}</CardTitle>
                    <div className="flex items-center gap-2 mt-1">
                      <Badge 
                        variant="secondary" 
                        className={getCategoryColor(template.category)}
                      >
                        {template.category}
                      </Badge>
                      {template.isCustom && (
                        <Badge variant="outline">Custom</Badge>
                      )}
                    </div>
                  </div>
                </div>
                
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => toggleFavorite(template.id)}
                >
                  <StarOff className="h-4 w-4" />
                </Button>
              </div>
            </CardHeader>
            
            <CardContent className="space-y-4">
              <CardDescription className="text-sm">
                {template.description}
              </CardDescription>
              
              {/* Template Stats */}
              <div className="flex items-center justify-between text-sm text-muted-foreground">
                <span>Used {template.usageCount} times</span>
                {template.lastUsed && (
                  <span>Last used: {isClient ? new Date(template.lastUsed).toLocaleDateString() : new Date(template.lastUsed).toISOString().split('T')[0]}</span>
                )}
              </div>
              
              {/* Tags */}
              {template.tags && template.tags.length > 0 && (
                <div className="flex flex-wrap gap-1">
                  {template.tags.map(tag => (
                    <Badge key={tag} variant="outline" className="text-xs">
                      {tag}
                    </Badge>
                  ))}
                </div>
              )}
              
              {/* Actions */}
              <div className="flex items-center justify-between pt-4 border-t">
                <div className="flex items-center gap-2">
                  <Button 
                    variant="outline" 
                    size="sm"
                    onClick={() => previewTemplate(template.id)}
                  >
                    <Eye className="h-3 w-3 mr-1" />
                    Preview
                  </Button>
                  
                  <Button 
                    variant="outline" 
                    size="sm"
                    onClick={() => duplicateTemplate(template.id)}
                  >
                    <Copy className="h-3 w-3 mr-1" />
                    Clone
                  </Button>
                </div>
                
                <div className="flex items-center gap-2">
                  {template.isCustom && (
                    <>
                      <Button 
                        variant="ghost" 
                        size="sm"
                        onClick={() => editTemplate(template.id)}
                      >
                        <Edit className="h-3 w-3" />
                      </Button>
                      <Button 
                        variant="ghost" 
                        size="sm"
                        onClick={() => deleteTemplate(template.id)}
                        className="text-red-600 hover:text-red-700"
                      >
                        <Trash2 className="h-3 w-3" />
                      </Button>
                    </>
                  )}
                  
                  <Button 
                    size="sm"
                    onClick={() => useTemplate(template.id)}
                  >
                    Use Template
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
          ))}
        </div>
      )}
      
      {filteredTemplates.length === 0 && (
        <Card>
          <CardContent className="p-12 text-center">
            <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
            <h3 className="text-lg font-medium mb-2">No templates found</h3>
            <p className="text-muted-foreground mb-4">
              Try adjusting your filters or create a new template to get started.
            </p>
            <Button>
              <Plus className="h-4 w-4 mr-2" />
              Create Your First Template
            </Button>
          </CardContent>
        </Card>
      )}
      
      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">{templates.length}</div>
            <div className="text-sm text-muted-foreground">Total Templates</div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">
              {templates.filter(t => t.isCustom).length}
            </div>
            <div className="text-sm text-muted-foreground">Custom Templates</div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">
              {templates.filter(t => t.isFavorite).length}
            </div>
            <div className="text-sm text-muted-foreground">Favorites</div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="p-4 text-center">
            <div className="text-2xl font-bold">
              {templates.length > 0 ? Math.round(templates.reduce((sum, t) => sum + t.usageCount, 0) / templates.length) : 0}
            </div>
            <div className="text-sm text-muted-foreground">Avg. Usage</div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

export default ReportTemplates