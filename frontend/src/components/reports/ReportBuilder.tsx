"use client"

import React, { useState } from 'react'
import { Button } from '../ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Textarea } from '../ui/textarea'
import { Separator } from '../ui/separator'
import { 
  Database, 
  Calendar, 
  Filter, 
  BarChart3, 
  PieChart, 
  LineChart, 
  Table2,
  Plus,
  X,
  ChevronDown,
  ChevronRight
} from 'lucide-react'

interface FilterCondition {
  id: string
  field: string
  operator: string
  value: string
}

interface SelectedField {
  id: string
  name: string
  type: string
  aggregation?: string
}

interface ReportBuilderProps {
  onClose?: () => void;
}

const ReportBuilder: React.FC<ReportBuilderProps> = ({ onClose }) => {
  const [reportName, setReportName] = useState('')
  const [reportDescription, setReportDescription] = useState('')
  const [selectedDataSource, setSelectedDataSource] = useState('')
  const [selectedFields, setSelectedFields] = useState<SelectedField[]>([])
  const [filters, setFilters] = useState<FilterCondition[]>([])
  const [chartType, setChartType] = useState('table')
  const [expandedSections, setExpandedSections] = useState({
    dataSource: true,
    fields: true,
    filters: true,
    visualization: true
  })

  // Mock data sources
  const dataSources = [
    { id: 'users', name: 'Users', description: 'User account information' },
    { id: 'transactions', name: 'Transactions', description: 'Financial transactions' },
    { id: 'products', name: 'Products', description: 'Product catalog data' },
    { id: 'orders', name: 'Orders', description: 'Customer orders' },
    { id: 'inventory', name: 'Inventory', description: 'Stock and inventory data' }
  ]

  // Mock available fields based on selected data source
  const getAvailableFields = (dataSourceId: string) => {
    const fieldSets: Record<string, Array<{id: string, name: string, type: string}>> = {
      users: [
        { id: 'id', name: 'User ID', type: 'number' },
        { id: 'name', name: 'Full Name', type: 'string' },
        { id: 'email', name: 'Email', type: 'string' },
        { id: 'created_at', name: 'Registration Date', type: 'date' },
        { id: 'status', name: 'Account Status', type: 'string' },
        { id: 'tenant_id', name: 'Tenant ID', type: 'number' }
      ],
      transactions: [
        { id: 'id', name: 'Transaction ID', type: 'number' },
        { id: 'amount', name: 'Amount', type: 'currency' },
        { id: 'type', name: 'Transaction Type', type: 'string' },
        { id: 'date', name: 'Transaction Date', type: 'date' },
        { id: 'user_id', name: 'User ID', type: 'number' },
        { id: 'status', name: 'Status', type: 'string' }
      ],
      products: [
        { id: 'id', name: 'Product ID', type: 'number' },
        { id: 'name', name: 'Product Name', type: 'string' },
        { id: 'category', name: 'Category', type: 'string' },
        { id: 'price', name: 'Price', type: 'currency' },
        { id: 'stock_quantity', name: 'Stock Quantity', type: 'number' },
        { id: 'created_at', name: 'Created Date', type: 'date' }
      ]
    }
    return fieldSets[dataSourceId] || []
  }

  const operators = [
    { value: 'equals', label: 'Equals' },
    { value: 'not_equals', label: 'Not Equals' },
    { value: 'contains', label: 'Contains' },
    { value: 'greater_than', label: 'Greater Than' },
    { value: 'less_than', label: 'Less Than' },
    { value: 'between', label: 'Between' },
    { value: 'in', label: 'In List' }
  ]

  const aggregations = [
    { value: 'none', label: 'No Aggregation' },
    { value: 'count', label: 'Count' },
    { value: 'sum', label: 'Sum' },
    { value: 'avg', label: 'Average' },
    { value: 'min', label: 'Minimum' },
    { value: 'max', label: 'Maximum' }
  ]

  const chartTypes = [
    { value: 'table', label: 'Table', icon: Table2 },
    { value: 'bar', label: 'Bar Chart', icon: BarChart3 },
    { value: 'line', label: 'Line Chart', icon: LineChart },
    { value: 'pie', label: 'Pie Chart', icon: PieChart }
  ]

  const toggleSection = (section: keyof typeof expandedSections) => {
    setExpandedSections(prev => ({
      ...prev,
      [section]: !prev[section]
    }))
  }

  const addField = (field: {id: string, name: string, type: string}) => {
    const newField: SelectedField = {
      id: `${field.id}_${Date.now()}`,
      name: field.name,
      type: field.type,
      aggregation: 'none'
    }
    setSelectedFields(prev => [...prev, newField])
  }

  const removeField = (fieldId: string) => {
    setSelectedFields(prev => prev.filter(f => f.id !== fieldId))
  }

  const updateFieldAggregation = (fieldId: string, aggregation: string) => {
    setSelectedFields(prev => prev.map(f => 
      f.id === fieldId ? { ...f, aggregation } : f
    ))
  }

  const addFilter = () => {
    const newFilter: FilterCondition = {
      id: `filter_${Date.now()}`,
      field: '',
      operator: 'equals',
      value: ''
    }
    setFilters(prev => [...prev, newFilter])
  }

  const updateFilter = (filterId: string, updates: Partial<FilterCondition>) => {
    setFilters(prev => prev.map(f => 
      f.id === filterId ? { ...f, ...updates } : f
    ))
  }

  const removeFilter = (filterId: string) => {
    setFilters(prev => prev.filter(f => f.id !== filterId))
  }

  const generateReport = async () => {
    if (!reportName.trim()) {
      alert('Please enter a report name');
      return;
    }
    
    if (!selectedDataSource) {
      alert('Please select a data source');
      return;
    }
    
    if (selectedFields.length === 0) {
      alert('Please select at least one field');
      return;
    }
    
    const reportConfig = {
      name: reportName,
      description: reportDescription,
      type: selectedDataSource, // Use data source as report type
      query: `SELECT ${selectedFields.map(f => f.name).join(', ')} FROM ${selectedDataSource}`,
      columns: selectedFields.map((field, index) => ({
        name: field.name,
        displayName: field.name,
        dataType: field.type,
        isVisible: true,
        order: index,
        aggregationType: field.aggregation !== 'none' ? field.aggregation : undefined
      })),
      parameters: {}
    }
    
    console.log('Creating Report Config:', reportConfig)
    
    try {
      // For now, we'll just show a success message
      // In a real implementation, you'd call the reports service
      alert(`Report "${reportName}" created successfully!\n\nThis would create a report with:\n- Data Source: ${selectedDataSource}\n- Fields: ${selectedFields.map(f => f.name).join(', ')}\n- Filters: ${filters.length} filter(s)`);
      
      // Close dialog after generating report
      if (onClose) {
        onClose();
      }
    } catch (error) {
      console.error('Error creating report:', error);
      alert('Failed to create report. Please try again.');
    }
  }

  const availableFields = selectedDataSource ? getAvailableFields(selectedDataSource) : []

  return (
    <div className="space-y-6 pb-20">
      {/* Report Basic Info */}
      <Card>
        <CardHeader>
          <CardTitle>Report Configuration</CardTitle>
          <CardDescription>Configure your custom report settings</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="report-name">Report Name</Label>
              <Input
                id="report-name"
                placeholder="Enter report name"
                value={reportName}
                onChange={(e) => setReportName(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="chart-type">Visualization Type</Label>
              <Select value={chartType} onValueChange={setChartType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {chartTypes.map(type => {
                    const Icon = type.icon
                    return (
                      <SelectItem key={type.value} value={type.value}>
                        <div className="flex items-center gap-2">
                          <Icon className="h-4 w-4" />
                          {type.label}
                        </div>
                      </SelectItem>
                    )
                  })}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="report-description">Description</Label>
            <Textarea
              id="report-description"
              placeholder="Describe what this report shows"
              value={reportDescription}
              onChange={(e) => setReportDescription(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      {/* Data Source Selection */}
      <Card>
        <CardHeader 
          className="cursor-pointer" 
          onClick={() => toggleSection('dataSource')}
        >
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Database className="h-5 w-5" />
                Data Source
              </CardTitle>
              <CardDescription>Select the primary data source for your report</CardDescription>
            </div>
            {expandedSections.dataSource ? 
              <ChevronDown className="h-4 w-4" /> : 
              <ChevronRight className="h-4 w-4" />
            }
          </div>
        </CardHeader>
        {expandedSections.dataSource && (
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              {dataSources.map(source => (
                <Card 
                  key={source.id}
                  className={`cursor-pointer transition-colors ${
                    selectedDataSource === source.id 
                      ? 'ring-2 ring-primary' 
                      : 'hover:bg-muted/50'
                  }`}
                  onClick={() => setSelectedDataSource(source.id)}
                >
                  <CardContent className="p-4">
                    <div className="flex items-start gap-3">
                      <Database className="h-5 w-5 text-muted-foreground mt-0.5" />
                      <div>
                        <h4 className="font-medium">{source.name}</h4>
                        <p className="text-sm text-muted-foreground">{source.description}</p>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </CardContent>
        )}
      </Card>

      {/* Field Selection */}
      {selectedDataSource && (
        <Card>
          <CardHeader 
            className="cursor-pointer" 
            onClick={() => toggleSection('fields')}
          >
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Select Fields</CardTitle>
                <CardDescription>Choose which fields to include in your report</CardDescription>
              </div>
              {expandedSections.fields ? 
                <ChevronDown className="h-4 w-4" /> : 
                <ChevronRight className="h-4 w-4" />
              }
            </div>
          </CardHeader>
          {expandedSections.fields && (
            <CardContent className="space-y-4">
              {/* Available Fields */}
              <div>
                <Label className="text-sm font-medium">Available Fields</Label>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2 mt-2">
                  {availableFields.map(field => (
                    <Button
                      key={field.id}
                      variant="outline"
                      size="sm"
                      onClick={() => addField(field)}
                      className="justify-start"
                      disabled={selectedFields.some(f => f.name === field.name)}
                    >
                      <Plus className="h-3 w-3 mr-2" />
                      {field.name}
                      <Badge variant="secondary" className="ml-auto text-xs">
                        {field.type}
                      </Badge>
                    </Button>
                  ))}
                </div>
              </div>

              <Separator />

              {/* Selected Fields */}
              <div>
                <Label className="text-sm font-medium">Selected Fields ({selectedFields.length})</Label>
                <div className="space-y-2 mt-2">
                  {selectedFields.map(field => (
                    <div key={field.id} className="flex items-center gap-3 p-3 bg-muted rounded-lg">
                      <div className="flex-1">
                        <div className="font-medium">{field.name}</div>
                        <Badge variant="outline" className="text-xs">{field.type}</Badge>
                      </div>
                      
                      {field.type === 'number' || field.type === 'currency' ? (
                        <Select 
                          value={field.aggregation} 
                          onValueChange={(value) => updateFieldAggregation(field.id, value)}
                        >
                          <SelectTrigger className="w-32">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {aggregations.map(agg => (
                              <SelectItem key={agg.value} value={agg.value}>
                                {agg.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : (
                        <div className="w-32 text-sm text-muted-foreground">No aggregation</div>
                      )}
                      
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => removeField(field.id)}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    </div>
                  ))}
                  
                  {selectedFields.length === 0 && (
                    <div className="text-center py-8 text-muted-foreground">
                      No fields selected. Add fields from the available list above.
                    </div>
                  )}
                </div>
              </div>
            </CardContent>
          )}
        </Card>
      )}

      {/* Filters */}
      {selectedDataSource && (
        <Card>
          <CardHeader 
            className="cursor-pointer" 
            onClick={() => toggleSection('filters')}
          >
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <Filter className="h-5 w-5" />
                  Filters
                </CardTitle>
                <CardDescription>Add conditions to filter your data</CardDescription>
              </div>
              {expandedSections.filters ? 
                <ChevronDown className="h-4 w-4" /> : 
                <ChevronRight className="h-4 w-4" />
              }
            </div>
          </CardHeader>
          {expandedSections.filters && (
            <CardContent className="space-y-4">
              <Button onClick={addFilter} variant="outline">
                <Plus className="h-4 w-4 mr-2" />
                Add Filter
              </Button>
              
              <div className="space-y-3">
                {filters.map(filter => (
                  <div key={filter.id} className="flex items-center gap-3 p-3 bg-muted rounded-lg">
                    <Select 
                      value={filter.field} 
                      onValueChange={(value) => updateFilter(filter.id, { field: value })}
                    >
                      <SelectTrigger className="w-48">
                        <SelectValue placeholder="Select field" />
                      </SelectTrigger>
                      <SelectContent>
                        {availableFields.map(field => (
                          <SelectItem key={field.id} value={field.id}>
                            {field.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    
                    <Select 
                      value={filter.operator} 
                      onValueChange={(value) => updateFilter(filter.id, { operator: value })}
                    >
                      <SelectTrigger className="w-32">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {operators.map(op => (
                          <SelectItem key={op.value} value={op.value}>
                            {op.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    
                    <Input
                      placeholder="Value"
                      value={filter.value}
                      onChange={(e) => updateFilter(filter.id, { value: e.target.value })}
                      className="flex-1"
                    />
                    
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => removeFilter(filter.id)}
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                ))}
                
                {filters.length === 0 && (
                  <div className="text-center py-4 text-muted-foreground">
                    No filters applied. Add filters to narrow down your data.
                  </div>
                )}
              </div>
            </CardContent>
          )}
        </Card>
      )}

      {/* Generate Report */}
      <div className="flex justify-between gap-3 pt-4 border-t bg-background sticky bottom-0 -mx-6 px-6 pb-4">
        <div>
          {onClose && (
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
          )}
        </div>
        <div className="flex gap-3">
          <Button variant="outline">Save as Template</Button>
          <Button 
            onClick={generateReport}
            disabled={!reportName || !selectedDataSource || selectedFields.length === 0}
          >
            Generate Report
          </Button>
        </div>
      </div>
    </div>
  )
}

export default ReportBuilder