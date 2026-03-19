"use client"

import React, { useState, useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Button } from '../ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Textarea } from '../ui/textarea'
import { Separator } from '../ui/separator'
import { Checkbox } from '../ui/checkbox'
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
  ChevronRight,
  Loader2
} from 'lucide-react'
import { CreateReportDto, CreateReportTemplateDto, reportsService } from '../../services/reports'
import { useToast } from '../../hooks/use-toast'
import { API_CONFIG } from '../../config/api'

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
  onReportCreated?: () => void;
  onTemplateCreated?: () => void;
  editingReportId?: string;
  editingReport?: any;
}

interface DataSourceSchemaColumn {
  name: string
  dataType: string
}

interface DataSourceSchemaEntity {
  name: string
  schema?: string
  rowCount?: number | null
  columns: DataSourceSchemaColumn[]
}

interface DataSourceSchema {
  tables: DataSourceSchemaEntity[]
  views: DataSourceSchemaEntity[]
}

interface AvailableField {
  id: string
  name: string
  type: string
  table?: string
}

interface AvailableDataSource {
  name: string
  schema?: string
  type: 'table' | 'view'
  rowCount: number | null
  columnCount: number
}

const ReportBuilder: React.FC<ReportBuilderProps> = ({ onClose, onReportCreated, onTemplateCreated, editingReportId, editingReport }) => {
  const [reportName, setReportName] = useState('')
  const [reportDescription, setReportDescription] = useState('')
  const [selectedTableOrView, setSelectedTableOrView] = useState('')
  const [selectedFields, setSelectedFields] = useState<SelectedField[]>([])
  const [filters, setFilters] = useState<FilterCondition[]>([])
  const [chartType, setChartType] = useState('table')
  const [isCreatingReport, setIsCreatingReport] = useState(false)
  const [isCreatingTemplate, setIsCreatingTemplate] = useState(false)
  const [expandedSections, setExpandedSections] = useState({
    tableView: true,
    fields: true,
    filters: true,
    visualization: true
  })
  
  const { toast } = useToast()
  
  // Populate form when editing existing report
  useEffect(() => {
    if (editingReport && editingReportId) {
      setReportName(editingReport.name || '')
      setReportDescription(editingReport.description || '')
      setChartType(editingReport.type || 'table')
      
      // If the report has existing data, we'll populate basic info
      // Note: Complex table/field selection would require more sophisticated parsing
      // For now, we'll just populate the basic metadata
    }
  }, [editingReport, editingReportId])

  // Fetch ERP database schema directly (simplified approach)
  const {
    data: dataSourceSchema,
    isLoading: schemaLoading,
    error: schemaError
  } = useQuery<DataSourceSchema, Error>({
    queryKey: ['erp-schema'],
    queryFn: async () => {
      const baseUrl = API_CONFIG.BASE_URL.replace('/api', '');
      const response = await fetch(`${baseUrl}/api/datasources/schema`);
      if (!response.ok) {
        throw new Error('Failed to fetch schema');
      }
      return await response.json() as DataSourceSchema;
    },
    refetchOnWindowFocus: false,
  })

  // Get available fields from the data source schema - filtered by selected table/view
  const getAvailableFields = (): AvailableField[] => {
    if (!dataSourceSchema || !selectedTableOrView) return []
    
    const fields: AvailableField[] = []
    
    // Find the selected table or view and return only its columns
    const selectedTable = dataSourceSchema.tables.find((table) => table.name === selectedTableOrView)
    const selectedView = dataSourceSchema.views.find((view) => view.name === selectedTableOrView)
    
    if (selectedTable) {
      selectedTable.columns.forEach(column => {
        fields.push({
          id: `${selectedTable.name}.${column.name}`,
          name: `${selectedTable.name}.${column.name}`,
          type: column.dataType.toLowerCase(),
          table: selectedTable.name
        })
      })
    } else if (selectedView) {
      selectedView.columns.forEach(column => {
        fields.push({
          id: `${selectedView.name}.${column.name}`,
          name: `${selectedView.name}.${column.name}`,
          type: column.dataType.toLowerCase(),
          table: selectedView.name
        })
      })
    }
    
    return fields
  }

  // Get available tables and views for selection
  const getAvailableTablesAndViews = (): { tables: AvailableDataSource[]; views: AvailableDataSource[] } => {
    if (!dataSourceSchema) return { tables: [], views: [] }
    
    return {
      tables: dataSourceSchema.tables.map((table) => ({
        name: table.name,
        schema: table.schema,
        type: 'table',
        rowCount: table.rowCount ?? null,
        columnCount: table.columns.length
      })),
      views: dataSourceSchema.views.map((view) => ({
        name: view.name,
        schema: view.schema,
        type: 'view',
        rowCount: null,
        columnCount: view.columns.length
      }))
    }
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

  const addField = (field: AvailableField) => {
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

  const saveAsTemplate = async () => {
    if (!reportName.trim()) {
      toast({
        title: 'Validation Error',
        description: 'Please enter a report name for the template',
        variant: 'destructive'
      })
      return
    }
    
    
    if (!selectedTableOrView) {
      toast({
        title: 'Validation Error',
        description: 'Please select a table or view',
        variant: 'destructive'
      })
      return
    }
    
    if (selectedFields.length === 0) {
      toast({
        title: 'Validation Error',
        description: 'Please select at least one field',
        variant: 'destructive'
      })
      return
    }
    
    setIsCreatingTemplate(true)
    
    try {
      const templateData: CreateReportTemplateDto = {
        name: `${reportName} Template`,
        description: reportDescription || `Template for ${reportName} reports`,
        category: 'custom',
        type: chartType,
        chartType: chartType !== 'table' ? chartType : undefined,
        isCustom: true,
        tags: ['custom', chartType, 'user-created'],
        configuration: {
          tableOrView: selectedTableOrView,
          fields: selectedFields.map(f => ({
            name: f.name,
            type: f.type,
            aggregation: f.aggregation
          })),
          filters: filters,
          visualization: {
            type: chartType,
            settings: {}
          }
        }
      }
      
      await reportsService.createReportTemplate(templateData)
      
      toast({
        title: 'Template Created',
        description: `Template "${templateData.name}" has been saved successfully`,
      })
      
      if (onTemplateCreated) {
        onTemplateCreated()
      }
      
    } catch (error: any) {
      console.error('Error saving template:', error)
      toast({
        title: 'Error Creating Template',
        description: error.response?.data?.message || 'Failed to save template. Please try again.',
        variant: 'destructive'
      })
    } finally {
      setIsCreatingTemplate(false)
    }
  }

  const generateReport = async () => {
    if (!reportName.trim()) {
      toast({
        title: 'Validation Error',
        description: 'Please enter a report name',
        variant: 'destructive'
      })
      return
    }
    
    
    if (!selectedTableOrView) {
      toast({
        title: 'Validation Error',
        description: 'Please select a table or view',
        variant: 'destructive'
      })
      return
    }
    
    if (selectedFields.length === 0) {
      toast({
        title: 'Validation Error',
        description: 'Please select at least one field',
        variant: 'destructive'
      })
      return
    }
    
    setIsCreatingReport(true)
    
    try {
      // Get schema info for the selected table/view to build proper table reference
      const { tables, views } = getAvailableTablesAndViews()
      const selectedEntity = tables.find(t => t.name === selectedTableOrView) || views.find(v => v.name === selectedTableOrView)
      const fullTableName = selectedEntity?.schema 
        ? `${selectedEntity.schema}.${selectedTableOrView}` 
        : selectedTableOrView
      
      // Build SQL query with proper field references
      const selectFields = selectedFields.map(field => {
        const fieldName = field.name.includes('.') ? field.name : `${selectedTableOrView}.${field.name}`
        
        if (field.aggregation && field.aggregation !== 'none') {
          const alias = field.name.replace(/[^a-zA-Z0-9_]/g, '_')
          return `${field.aggregation.toUpperCase()}(${fieldName}) AS ${alias}_${field.aggregation}`
        }
        return fieldName
      })
      
      const whereClause = filters.length > 0 
        ? 'WHERE ' + filters.map(f => {
            const filterField = f.field.includes('.') ? f.field : `${selectedTableOrView}.${f.field}`
            const operator = f.operator.replace('_', ' ').toUpperCase()
            return `${filterField} ${operator} '${f.value}'`
          }).join(' AND ')
        : ''
      
      const query = `SELECT ${selectFields.join(', ')} FROM ${fullTableName} ${whereClause}`.trim()
      
      const reportData: CreateReportDto = {
        name: reportName,
        description: reportDescription || `Report generated from ERP database`,
        type: chartType,
        query: query,
        columns: selectedFields.map((field, index) => ({
          name: field.name.replace('.', '_'),
          displayName: field.name,
          dataType: field.type,
          isVisible: true,
          order: index,
          aggregationType: field.aggregation !== 'none' ? field.aggregation : undefined
        })),
        visualization: {
          type: chartType,
          chartType: chartType !== 'table' ? chartType : undefined,
          configuration: {}
        },
        parameters: filters.reduce((acc, filter, index) => {
          acc[`param_${index}`] = {
            name: filter.field,
            type: 'string',
            defaultValue: filter.value,
            required: true
          }
          return acc
        }, {} as Record<string, any>),
        tags: ['generated', chartType, 'erp-database']
      }
      
      if (editingReportId) {
        // Update existing report
        await reportsService.updateReport(editingReportId, {
          name: reportData.name,
          description: reportData.description,
          query: reportData.query,
          columns: reportData.columns,
          visualization: reportData.visualization,
          tags: reportData.tags
        })
        
        toast({
          title: 'Report Updated',
          description: `Report "${reportName}" has been updated successfully`,
        })
      } else {
        // Create new report
        await reportsService.createReport(reportData)
        
        toast({
          title: 'Report Created',
          description: `Report "${reportName}" has been created successfully`,
        })
      }
      
      if (onReportCreated) {
        onReportCreated()
      }
      
      // Close dialog after creating report
      if (onClose) {
        onClose()
      }
      
    } catch (error: any) {
      console.error('Error creating report:', error)
      toast({
        title: 'Error Creating Report',
        description: error.response?.data?.message || 'Failed to create report. Please try again.',
        variant: 'destructive'
      })
    } finally {
      setIsCreatingReport(false)
    }
  }

  const availableFields = getAvailableFields()

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

      {/* Table/View Selection */}
        <Card>
          <CardHeader 
            className="cursor-pointer" 
            onClick={() => toggleSection('tableView')}
          >
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <Table2 className="h-5 w-5" />
                  Select Table or View
                </CardTitle>
                <CardDescription>Choose a specific table or view to build your report from</CardDescription>
              </div>
              {expandedSections.tableView ? 
                <ChevronDown className="h-4 w-4" /> : 
                <ChevronRight className="h-4 w-4" />
              }
            </div>
          </CardHeader>
          {expandedSections.tableView && (
            <CardContent>
              {schemaLoading ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="h-4 w-4 animate-spin mr-2" />
                  <span className="text-sm">Loading schema...</span>
                </div>
              ) : schemaError ? (
                <div className="text-center py-4 text-destructive text-sm">
                  <p>Failed to load data source schema</p>
                  <p className="text-muted-foreground mt-1">Please check the data source connection</p>
                </div>
              ) : (
                <div className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="table-view-select">Select Table or View</Label>
                    <Select 
                      value={selectedTableOrView} 
                      onValueChange={(value) => {
                        setSelectedTableOrView(value)
                        setSelectedFields([]) // Clear fields when changing table/view
                        setFilters([]) // Clear filters when changing table/view
                      }}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Choose a table or view" />
                      </SelectTrigger>
                      <SelectContent>
                        {(() => {
                          const { tables, views } = getAvailableTablesAndViews()
                          return (
                            <>
                              {/* Tables */}
                              {tables.length > 0 && (
                                <>
                                  <div className="px-2 py-1.5 text-sm font-semibold text-muted-foreground">
                                    Tables
                                  </div>
                                  {tables.map(table => (
                                    <SelectItem key={`table-${table.name}`} value={table.name}>
                                      <div className="flex items-center gap-2">
                                        <Table2 className="h-4 w-4" />
                                        <span>{table.name}</span>
                                        {table.schema && (
                                          <span className="text-xs text-muted-foreground">({table.schema})</span>
                                        )}
                                        <Badge variant="secondary" className="ml-auto text-xs">
                                          {table.columnCount} cols
                                        </Badge>
                                      </div>
                                    </SelectItem>
                                  ))}
                                </>
                              )}
                              
                              {/* Views */}
                              {views.length > 0 && (
                                <>
                                  {tables.length > 0 && <div className="h-px bg-border my-1" />}
                                  <div className="px-2 py-1.5 text-sm font-semibold text-muted-foreground">
                                    Views
                                  </div>
                                  {views.map(view => (
                                    <SelectItem key={`view-${view.name}`} value={view.name}>
                                      <div className="flex items-center gap-2">
                                        <Database className="h-4 w-4" />
                                        <span>{view.name}</span>
                                        {view.schema && (
                                          <span className="text-xs text-muted-foreground">({view.schema})</span>
                                        )}
                                        <Badge variant="outline" className="ml-auto text-xs">
                                          {view.columnCount} cols
                                        </Badge>
                                      </div>
                                    </SelectItem>
                                  ))}
                                </>
                              )}
                              
                              {tables.length === 0 && views.length === 0 && (
                                <div className="px-2 py-8 text-center text-muted-foreground text-sm">
                                  No tables or views available
                                </div>
                              )}
                            </>
                          )
                        })()}
                      </SelectContent>
                    </Select>
                    
                    {/* Selected table/view info */}
                    {selectedTableOrView && (() => {
                      const { tables, views } = getAvailableTablesAndViews()
                      const selectedEntity = tables.find(t => t.name === selectedTableOrView) || views.find(v => v.name === selectedTableOrView)
                      return selectedEntity ? (
                        <div className="text-sm text-muted-foreground bg-muted/30 p-3 rounded-lg">
                          <div className="flex items-center gap-2 mb-1">
                            {tables.find(t => t.name === selectedTableOrView) ? (
                              <><Table2 className="h-4 w-4" /> Table: <strong>{selectedTableOrView}</strong></>
                            ) : (
                              <><Database className="h-4 w-4" /> View: <strong>{selectedTableOrView}</strong></>
                            )}
                          </div>
                          <div className="space-y-1">
                            {selectedEntity.schema && (
                              <p><span className="font-medium">Schema:</span> {selectedEntity.schema}</p>
                            )}
                            <p><span className="font-medium">Columns:</span> {selectedEntity.columnCount}</p>
                            {selectedEntity.rowCount !== null && (
                              <p><span className="font-medium">Rows:</span> {selectedEntity.rowCount.toLocaleString()}</p>
                            )}
                          </div>
                        </div>
                      ) : null
                    })()}
                  </div>
                </div>
              )}
            </CardContent>
          )}
        </Card>

      {/* Field Selection */}
      {selectedTableOrView && (
        <Card>
          <CardHeader 
            className="cursor-pointer" 
            onClick={() => toggleSection('fields')}
          >
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Select Fields</CardTitle>
                <CardDescription>Choose which fields from {selectedTableOrView} to include in your report</CardDescription>
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
                <Label className="text-sm font-medium">Select Fields ({selectedFields.length} selected)</Label>
                {schemaLoading ? (
                  <div className="flex items-center justify-center py-8">
                    <Loader2 className="h-4 w-4 animate-spin mr-2" />
                    <span className="text-sm">Loading schema...</span>
                  </div>
                ) : schemaError ? (
                  <div className="text-center py-4 text-destructive text-sm">
                    <p>Failed to load data source schema</p>
                    <p className="text-muted-foreground mt-1">Please check the data source connection</p>
                  </div>
                ) : availableFields.length === 0 ? (
                  <div className="text-center py-4 text-muted-foreground text-sm">
                    <p>No fields available</p>
                    <p className="mt-1">Select a table or view first</p>
                  </div>
                ) : (
                  <div className="space-y-3 mt-3">
                    <div className="flex items-center gap-2 mb-3">
                      <Checkbox 
                        id="select-all-fields"
                        checked={selectedFields.length === availableFields.length && availableFields.length > 0}
                        onCheckedChange={(checked) => {
                          if (checked) {
                            // Select all fields
                            const newFields = availableFields.filter(field => 
                              !selectedFields.some(sf => sf.name === field.name)
                            )
                            newFields.forEach(field => addField(field))
                          } else {
                            // Deselect all fields
                            setSelectedFields([])
                          }
                        }}
                      />
                      <Label htmlFor="select-all-fields" className="text-sm font-medium cursor-pointer">
                        Select All Fields
                      </Label>
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3 max-h-60 overflow-y-auto border rounded-lg p-3 bg-muted/20">
                      {availableFields.map(field => {
                        const isSelected = selectedFields.some(f => f.name === field.name)
                        return (
                          <div key={field.id} className="flex items-center space-x-3 p-2 rounded-md hover:bg-muted/50">
                            <Checkbox 
                              id={`field-${field.id}`}
                              checked={isSelected}
                              onCheckedChange={(checked) => {
                                if (checked) {
                                  addField(field)
                                } else {
                                  const selectedField = selectedFields.find(sf => sf.name === field.name)
                                  if (selectedField) {
                                    removeField(selectedField.id)
                                  }
                                }
                              }}
                            />
                            <Label htmlFor={`field-${field.id}`} className="flex-1 cursor-pointer">
                              <div className="flex items-center justify-between">
                                <span className="font-medium text-sm">{field.name.split('.').pop()}</span>
                                <Badge variant="secondary" className="text-xs ml-2">
                                  {field.type}
                                </Badge>
                              </div>
                              <div className="text-xs text-muted-foreground mt-1">
                                {field.name}
                              </div>
                            </Label>
                          </div>
                        )
                      })}
                    </div>
                  </div>
                )}
              </div>

              {/* Field Configuration - only show if fields are selected */}
              {selectedFields.length > 0 && (
                <>
                  <Separator />
                  <div>
                    <Label className="text-sm font-medium">Field Configuration</Label>
                    <div className="text-xs text-muted-foreground mb-3">Configure aggregations and settings for numeric fields</div>
                    <div className="space-y-2">
                      {selectedFields.map(field => {
                        const showAggregation = field.type === 'number' || field.type === 'currency' || field.type === 'int' || field.type === 'decimal' || field.type === 'float'
                        return (
                          <div key={field.id} className="flex items-center gap-3 p-2 bg-muted/30 rounded-lg">
                            <div className="flex-1 min-w-0">
                              <div className="font-medium text-sm truncate">{field.name.split('.').pop()}</div>
                              <div className="text-xs text-muted-foreground truncate">{field.name}</div>
                            </div>
                            <Badge variant="outline" className="text-xs flex-shrink-0">{field.type}</Badge>
                            
                            {showAggregation ? (
                              <Select 
                                value={field.aggregation} 
                                onValueChange={(value) => updateFieldAggregation(field.id, value)}
                              >
                                <SelectTrigger className="w-28 h-8">
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
                              <div className="w-28 text-xs text-muted-foreground text-center">No aggregation</div>
                            )}
                            
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => removeField(field.id)}
                              className="h-8 w-8 p-0 flex-shrink-0"
                            >
                              <X className="h-3 w-3" />
                            </Button>
                          </div>
                        )
                      })}
                    </div>
                  </div>
                </>
              )}
            </CardContent>
          )}
        </Card>
      )}

      {/* Filters */}
      {selectedTableOrView && (
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
          <Button 
            variant="outline"
            onClick={saveAsTemplate}
            disabled={!reportName || !selectedTableOrView || selectedFields.length === 0 || isCreatingTemplate}
          >
            {isCreatingTemplate && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Save as Template
          </Button>
          <Button 
            onClick={generateReport}
            disabled={!reportName || !selectedTableOrView || selectedFields.length === 0 || isCreatingReport}
          >
            {isCreatingReport && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            {editingReportId ? 'Update Report' : 'Generate Report'}
          </Button>
        </div>
      </div>
    </div>
  )
}

export default ReportBuilder
