"use client"

import React, { useEffect, useMemo, useRef, useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Code, Copy, Eye, FileText, Layout, Link2, Pencil, Plus, RefreshCw, Save, Search, Trash2, Wand2 } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../ui/dialog'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Switch } from '../ui/switch'
import { Textarea } from '../ui/textarea'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { RichTextEditor, type RichTextEditorRef } from '../ui/rich-text-editor'
import { ConfirmationDialog } from '../ui/confirmation-dialog'
import { Badge } from '../ui/badge'
import { useToast } from '../ui/use-toast'
import { apiService } from '../../services/api.service'
import authService from '../../services/auth'

interface NotificationTemplate {
  id: string
  name: string
  type: string
  subject: string
  htmlTemplate: string
  textTemplate?: string | null
  linkedEntityType?: string | null
  description?: string | null
  variables?: string[] | null
  isActive: boolean
  createdBy: string
  createdAt: string
  usageCount: number
}

interface WorkflowEntityTypeInfo {
  id?: string
  code?: string
  name?: string
  description?: string | null
}

interface SelectOption {
  value: string
  label: string
  description?: string | null
}

type TemplateInsertTarget = 'subject' | 'htmlTemplate' | 'textTemplate'
type BodyBuilderTab = 'design' | 'code' | 'preview'

const notificationTypeOptions: SelectOption[] = [
  { value: 'General', label: 'General' },
  { value: 'Payroll', label: 'Payroll / HR' },
  { value: 'Workflow', label: 'Workflow' },
  { value: 'Procurement', label: 'Procurement' },
  { value: 'Maintenance', label: 'Maintenance' },
  { value: 'Finance', label: 'Finance' },
  { value: 'Inventory', label: 'Inventory' },
  { value: 'Projects', label: 'Projects' },
  { value: 'Sales', label: 'Sales / CRM' },
  { value: 'Helpdesk', label: 'Helpdesk' },
]

const defaultEntityOptions: SelectOption[] = [
  { value: 'General', label: 'General' },
  { value: 'PayrollPayslipEmail', label: 'Payroll Payslip Email' },
  { value: 'PayrollRun', label: 'Payroll Run' },
  { value: 'PayrollException', label: 'Payroll Exception' },
  { value: 'PayrollSalaryAdvance', label: 'Payroll Salary Advance' },
  { value: 'PayrollBonusSetup', label: 'Payroll Bonus Setup' },
  { value: 'PayrollBackpaySetup', label: 'Payroll Backpay Setup' },
  { value: 'Employee', label: 'Employee' },
  { value: 'PurchaseOrder', label: 'Purchase Order' },
  { value: 'PurchaseRequisition', label: 'Purchase Requisition' },
  { value: 'WorkOrder', label: 'Work Order' },
  { value: 'JobCard', label: 'Job Card' },
  { value: 'InventoryTransfer', label: 'Inventory Transfer' },
  { value: 'Project', label: 'Project' },
  { value: 'ServiceRequest', label: 'Service Request' },
]

const placeholderCatalog: Record<string, SelectOption[]> = {
  General: [
    { value: 'entity_number', label: 'Entity Number' },
    { value: 'entity_name', label: 'Entity Name' },
    { value: 'status', label: 'Status' },
    { value: 'requested_by', label: 'Requested By' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PayrollPayslipEmail: [
    { value: 'employee_id', label: 'Employee ID' },
    { value: 'employee_number', label: 'Employee Number' },
    { value: 'employee_name', label: 'Employee Name' },
    { value: 'employee_email', label: 'Employee Email' },
    { value: 'department', label: 'Department' },
    { value: 'section', label: 'Section' },
    { value: 'position', label: 'Position' },
    { value: 'pay_period', label: 'Pay Period' },
    { value: 'run_number', label: 'Run Number' },
    { value: 'payslip_number', label: 'Payslip Number' },
    { value: 'company_name', label: 'Company Name' },
    { value: 'company_address', label: 'Company Address' },
    { value: 'currency_code', label: 'Currency Code' },
    { value: 'gross_amount', label: 'Gross Amount' },
    { value: 'total_earnings', label: 'Total Earnings' },
    { value: 'total_deductions', label: 'Total Deductions' },
    { value: 'net_salary', label: 'Net Salary' },
    { value: 'taxable_income', label: 'Taxable Income' },
    { value: 'income_tax', label: 'Income Tax' },
    { value: 'normal_income_tax', label: 'Normal Income Tax' },
    { value: 'bonus_income_tax', label: 'Bonus Income Tax' },
    { value: 'employee_contribution', label: 'Employee Contribution' },
    { value: 'employer_contribution', label: 'Employer Contribution' },
    { value: 'message', label: 'Message' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PayrollRun: [
    { value: 'run_number', label: 'Run Number' },
    { value: 'pay_period', label: 'Pay Period' },
    { value: 'employee_count', label: 'Employee Count' },
    { value: 'gross_amount', label: 'Gross Amount' },
    { value: 'net_amount', label: 'Net Amount' },
    { value: 'tax_amount', label: 'Tax Amount' },
    { value: 'status', label: 'Status' },
    { value: 'requested_by', label: 'Requested By' },
    { value: 'approved_by', label: 'Approved By' },
    { value: 'approved_at', label: 'Approved At' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PayrollException: [
    { value: 'exception_type', label: 'Exception Type' },
    { value: 'exception_number', label: 'Exception Number' },
    { value: 'employee_number', label: 'Employee Number' },
    { value: 'employee_name', label: 'Employee Name' },
    { value: 'amount', label: 'Amount' },
    { value: 'percentage', label: 'Percentage' },
    { value: 'pay_period', label: 'Pay Period' },
    { value: 'requested_by', label: 'Requested By' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PayrollSalaryAdvance: [
    { value: 'employee_number', label: 'Employee Number' },
    { value: 'employee_name', label: 'Employee Name' },
    { value: 'advance_number', label: 'Advance Number' },
    { value: 'amount', label: 'Amount' },
    { value: 'pay_period', label: 'Pay Period' },
    { value: 'requested_by', label: 'Requested By' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PayrollBonusSetup: [
    { value: 'bonus_code', label: 'Bonus Code' },
    { value: 'bonus_name', label: 'Bonus Name' },
    { value: 'employee_number', label: 'Employee Number' },
    { value: 'employee_name', label: 'Employee Name' },
    { value: 'amount', label: 'Amount' },
    { value: 'percentage', label: 'Percentage' },
    { value: 'pay_period', label: 'Pay Period' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PayrollBackpaySetup: [
    { value: 'backpay_code', label: 'Backpay Code' },
    { value: 'employee_number', label: 'Employee Number' },
    { value: 'employee_name', label: 'Employee Name' },
    { value: 'amount', label: 'Amount' },
    { value: 'effective_date', label: 'Effective Date' },
    { value: 'pay_period', label: 'Pay Period' },
    { value: 'action_url', label: 'Action URL' },
  ],
  Employee: [
    { value: 'employee_number', label: 'Employee Number' },
    { value: 'employee_name', label: 'Employee Name' },
    { value: 'employee_email', label: 'Employee Email' },
    { value: 'department', label: 'Department' },
    { value: 'position', label: 'Position' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PurchaseOrder: [
    { value: 'purchase_order_number', label: 'Purchase Order Number' },
    { value: 'po_number', label: 'PO Number' },
    { value: 'supplier_name', label: 'Supplier Name' },
    { value: 'vendor_name', label: 'Vendor Name' },
    { value: 'total_amount', label: 'Total Amount' },
    { value: 'currency_code', label: 'Currency Code' },
    { value: 'requested_by', label: 'Requested By' },
    { value: 'approved_by', label: 'Approved By' },
    { value: 'action_url', label: 'Action URL' },
  ],
  PurchaseRequisition: [
    { value: 'requisition_number', label: 'Requisition Number' },
    { value: 'department', label: 'Department' },
    { value: 'total_amount', label: 'Total Amount' },
    { value: 'requested_by', label: 'Requested By' },
    { value: 'action_url', label: 'Action URL' },
  ],
  WorkOrder: [
    { value: 'work_order_number', label: 'Work Order Number' },
    { value: 'asset_name', label: 'Asset Name' },
    { value: 'priority', label: 'Priority' },
    { value: 'assigned_to', label: 'Assigned To' },
    { value: 'action_url', label: 'Action URL' },
  ],
  JobCard: [
    { value: 'job_card_number', label: 'Job Card Number' },
    { value: 'work_order_number', label: 'Work Order Number' },
    { value: 'technician_name', label: 'Technician Name' },
    { value: 'status', label: 'Status' },
    { value: 'action_url', label: 'Action URL' },
  ],
  InventoryTransfer: [
    { value: 'transfer_number', label: 'Transfer Number' },
    { value: 'from_location', label: 'From Location' },
    { value: 'to_location', label: 'To Location' },
    { value: 'item_count', label: 'Item Count' },
    { value: 'requested_by', label: 'Requested By' },
    { value: 'action_url', label: 'Action URL' },
  ],
  Project: [
    { value: 'project_code', label: 'Project Code' },
    { value: 'project_name', label: 'Project Name' },
    { value: 'manager_name', label: 'Manager Name' },
    { value: 'status', label: 'Status' },
    { value: 'action_url', label: 'Action URL' },
  ],
  ServiceRequest: [
    { value: 'request_number', label: 'Request Number' },
    { value: 'requester_name', label: 'Requester Name' },
    { value: 'service_name', label: 'Service Name' },
    { value: 'status', label: 'Status' },
    { value: 'action_url', label: 'Action URL' },
  ],
}

const insertTargetOptions: SelectOption[] = [
  { value: 'htmlTemplate', label: 'HTML Template' },
  { value: 'subject', label: 'Subject' },
  { value: 'textTemplate', label: 'Text Template' },
]

const mergeOptions = (...groups: SelectOption[][]) => {
  const map = new Map<string, SelectOption>()
  groups.flat().forEach(option => {
    if (!option.value) return
    const key = option.value.toLowerCase()
    if (!map.has(key)) map.set(key, option)
  })
  return Array.from(map.values()).sort((a, b) => a.label.localeCompare(b.label))
}

const normalizeVariableName = (value: string) => value.trim().replace(/^\{\{/, '').replace(/\}\}$/, '').trim()

const variableCsvToArray = (variablesCsv: string) => variablesCsv
  .split(',')
  .map(normalizeVariableName)
  .filter(Boolean)

const variablesToCsv = (variables: string[]) => Array.from(new Set(variables.map(normalizeVariableName).filter(Boolean))).join(', ')

const extractWorkflowEntityTypes = (response: any): WorkflowEntityTypeInfo[] => {
  if (Array.isArray(response)) return response
  if (Array.isArray(response?.data)) return response.data
  if (Array.isArray(response?.items)) return response.items
  return []
}

const NotificationTemplates: React.FC = () => {
  const { toast } = useToast()
  const [templates, setTemplates] = useState<NotificationTemplate[]>([])
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<NotificationTemplate | null>(null)
  const [editingTemplate, setEditingTemplate] = useState<NotificationTemplate | null>(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [entityTypeOptions, setEntityTypeOptions] = useState<SelectOption[]>(defaultEntityOptions)
  const [placeholderTarget, setPlaceholderTarget] = useState<TemplateInsertTarget>('htmlTemplate')
  const [bodyBuilderOpen, setBodyBuilderOpen] = useState(false)
  const [bodyBuilderTab, setBodyBuilderTab] = useState<BodyBuilderTab>('design')
  const [bodyBuilderHtml, setBodyBuilderHtml] = useState('')
  const bodyBuilderEditorRef = useRef<RichTextEditorRef>(null)
  const bodyBuilderCodeRef = useRef<HTMLTextAreaElement>(null)

  const [form, setForm] = useState({
    name: '',
    type: 'General',
    linkedEntityType: 'General',
    description: '',
    subject: '',
    htmlTemplate: '',
    textTemplate: '',
    variablesCsv: '',
    isActive: true,
  })

  const isAdmin = useMemo(() => authService.hasAnyRole(['SuperAdmin', 'TenantAdmin']), [])
  const helperVariables = useMemo(
    () => variableCsvToArray(form.variablesCsv),
    [form.variablesCsv]
  )
  const selectedEntityPlaceholderOptions = useMemo(
    () => placeholderCatalog[form.linkedEntityType] || placeholderCatalog.General,
    [form.linkedEntityType]
  )
  const availablePlaceholderOptions = useMemo(
    () => mergeOptions(
      selectedEntityPlaceholderOptions,
      helperVariables.map(variable => ({ value: variable, label: variable }))
    ),
    [helperVariables, selectedEntityPlaceholderOptions]
  )
  const typeOptions = useMemo(() => mergeOptions(
    notificationTypeOptions,
    templates
      .map(template => template.type)
      .filter(Boolean)
      .map(type => ({ value: type, label: type })),
    form.type ? [{ value: form.type, label: form.type }] : []
  ), [form.type, templates])
  const linkedEntityOptions = useMemo(() => mergeOptions(
    entityTypeOptions,
    templates
      .map(template => template.linkedEntityType || '')
      .filter(Boolean)
      .map(entityType => ({ value: entityType, label: entityType })),
    form.linkedEntityType ? [{ value: form.linkedEntityType, label: form.linkedEntityType }] : []
  ), [entityTypeOptions, form.linkedEntityType, templates])
  const filteredTemplates = useMemo(() => {
    const query = searchTerm.trim().toLowerCase()
    if (!query) return templates

    return templates.filter(template => {
      const searchable = [
        template.name,
        template.type,
        template.linkedEntityType,
        template.subject,
        template.description,
        template.htmlTemplate,
        template.textTemplate,
        ...(template.variables || [])
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()

      return searchable.includes(query)
    })
  }, [searchTerm, templates])

  const resetForm = () => {
    setEditingTemplate(null)
    setPlaceholderTarget('htmlTemplate')
    setForm({
      name: '',
      type: 'General',
      linkedEntityType: 'General',
      description: '',
      subject: '',
      htmlTemplate: '',
      textTemplate: '',
      variablesCsv: '',
      isActive: true,
    })
  }

  const handleLinkedEntityChange = (value: string) => {
    const entityType = value || 'General'
    const defaults = (placeholderCatalog[entityType] || placeholderCatalog.General).map(option => option.value)

    setForm(p => ({
      ...p,
      linkedEntityType: entityType,
      variablesCsv: p.variablesCsv.trim() ? p.variablesCsv : variablesToCsv(defaults),
    }))
  }

  const addPlaceholdersToVariableList = (variables = availablePlaceholderOptions.map(option => option.value)) => {
    setForm(p => ({
      ...p,
      variablesCsv: variablesToCsv([...variableCsvToArray(p.variablesCsv), ...variables]),
    }))
  }

  const openCreate = () => {
    resetForm()
    setCreateOpen(true)
  }

  const openEdit = (template: NotificationTemplate) => {
    setEditingTemplate(template)
    setForm({
      name: template.name,
      type: template.type || 'General',
      linkedEntityType: template.linkedEntityType || 'General',
      description: template.description || '',
      subject: template.subject || '',
      htmlTemplate: template.htmlTemplate || '',
      textTemplate: template.textTemplate || '',
      variablesCsv: (template.variables || []).join(', '),
      isActive: template.isActive,
    })
    setCreateOpen(true)
  }

  const openCopy = (template: NotificationTemplate) => {
    setEditingTemplate(null)
    setForm({
      name: `${template.name} Copy`,
      type: template.type || 'General',
      linkedEntityType: template.linkedEntityType || 'General',
      description: template.description || '',
      subject: template.subject || '',
      htmlTemplate: template.htmlTemplate || '',
      textTemplate: template.textTemplate || '',
      variablesCsv: (template.variables || []).join(', '),
      isActive: true,
    })
    setCreateOpen(true)
  }

  const openBodyBuilder = () => {
    setBodyBuilderHtml(form.htmlTemplate || '<p></p>')
    setBodyBuilderTab('design')
    setBodyBuilderOpen(true)
  }

  const applyBodyBuilder = () => {
    setForm(p => ({ ...p, htmlTemplate: bodyBuilderHtml }))
    setBodyBuilderOpen(false)
  }

  const insertIntoBodyBuilder = (variable: string) => {
    const normalized = normalizeVariableName(variable)
    if (!normalized) return

    const placeholder = `{{${normalized}}}`

    setForm(p => ({
      ...p,
      variablesCsv: variablesToCsv([...variableCsvToArray(p.variablesCsv), normalized]),
    }))

    if (bodyBuilderTab === 'design') {
      bodyBuilderEditorRef.current?.insertText(placeholder)
      return
    }

    if (bodyBuilderTab === 'code' && bodyBuilderCodeRef.current) {
      const textarea = bodyBuilderCodeRef.current
      const start = textarea.selectionStart
      const end = textarea.selectionEnd
      const nextHtml = `${bodyBuilderHtml.slice(0, start)}${placeholder}${bodyBuilderHtml.slice(end)}`
      setBodyBuilderHtml(nextHtml)
      setTimeout(() => {
        textarea.focus()
        textarea.setSelectionRange(start + placeholder.length, start + placeholder.length)
      }, 0)
      return
    }

    setBodyBuilderHtml(current => {
      const separator = current && !/\s$/.test(current) ? ' ' : ''
      return `${current}${separator}${placeholder}`
    })
  }

  const insertPlaceholder = (variable: string) => {
    const normalized = normalizeVariableName(variable)
    if (!normalized) return

    const placeholder = `{{${normalized}}}`

    setForm(p => {
      const next = {
        ...p,
        variablesCsv: variablesToCsv([...variableCsvToArray(p.variablesCsv), normalized]),
      }
      const currentValue = next[placeholderTarget] || ''
      const separator = currentValue && !/\s$/.test(currentValue) ? ' ' : ''
      next[placeholderTarget] = `${currentValue}${separator}${placeholder}`
      return next
    })
  }

  const loadTemplates = async () => {
    if (!isAdmin) {
      setTemplates([])
      setLoadError(null)
      return
    }

    setLoading(true)
    setLoadError(null)

    try {
      const result = await apiService.request<NotificationTemplate[]>('/notifications/templates')
      setTemplates(result || [])
    } catch (e: any) {
      setLoadError(e?.message || 'Failed to load templates')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadTemplates()
     
  }, [])

  useEffect(() => {
    if (!isAdmin) return

    const loadEntityTypes = async () => {
      try {
        const seeded = await apiService.request<any>('/workflow/entity-types/seed', { method: 'POST' })
        const types = extractWorkflowEntityTypes(seeded)
        const next = types.length
          ? types.map(type => ({
              value: type.name || type.code || '',
              label: type.name || type.code || '',
              description: type.description
            })).filter(option => option.value)
          : []
        setEntityTypeOptions(mergeOptions(defaultEntityOptions, next))
      } catch {
        try {
          const response = await apiService.request<any>('/workflow/entity-types')
          const next = extractWorkflowEntityTypes(response)
            .map(type => ({
              value: type.name || type.code || '',
              label: type.name || type.code || '',
              description: type.description
            }))
            .filter(option => option.value)
          setEntityTypeOptions(mergeOptions(defaultEntityOptions, next))
        } catch {
          setEntityTypeOptions(defaultEntityOptions)
        }
      }
    }

    loadEntityTypes()
  }, [isAdmin])

  const onSave = async () => {
    try {
      const variables = form.variablesCsv
        .split(',')
        .map(v => v.trim())
        .filter(Boolean)

      await apiService.request(editingTemplate ? `/notifications/templates/${editingTemplate.id}` : '/notifications/templates', {
        method: 'POST',
        body: JSON.stringify({
          name: form.name,
          type: form.type,
          linkedEntityType: form.linkedEntityType || null,
          description: form.description || null,
          subject: form.subject,
          htmlTemplate: form.htmlTemplate,
          textTemplate: form.textTemplate || null,
          variables: variables.length ? variables : null,
          isActive: form.isActive,
        }),
      })

      setCreateOpen(false)
      resetForm()
      toast({ title: editingTemplate ? 'Template updated' : 'Template created', description: 'Notification template saved successfully.' })
      await loadTemplates()
    } catch (e: any) {
      toast({
        title: editingTemplate ? 'Failed to update template' : 'Failed to create template',
        description: e?.message || 'Please check your inputs and try again.',
        variant: 'destructive',
      })
    }
  }

  const onDelete = async () => {
    if (!deleteTarget) return

    try {
      await apiService.request(`/notifications/templates/${deleteTarget.id}`, {
        method: 'DELETE',
      })
      toast({ title: 'Template deleted', description: `Deleted "${deleteTarget.name}".` })
      setDeleteOpen(false)
      setDeleteTarget(null)
      await loadTemplates()
    } catch (e: any) {
      toast({
        title: 'Failed to delete template',
        description: e?.message || 'Please try again.',
        variant: 'destructive',
      })
    }
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <div className="flex justify-between items-center">
            <div>
              <CardTitle className="flex items-center gap-2">
                <FileText className="h-5 w-5" />
                Notification Templates
              </CardTitle>
              <CardDescription>Admin-managed templates for system notifications</CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Button variant="outline" onClick={loadTemplates} disabled={loading || !isAdmin}>
                <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
                Refresh
              </Button>
              <Button onClick={openCreate} disabled={!isAdmin}>
                <Plus className="h-4 w-4 mr-2" />
                Create Template
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {isAdmin ? (
            <div className="relative">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={searchTerm}
                onChange={e => setSearchTerm(e.target.value)}
                placeholder="Search templates, linked entity, subject, or placeholders..."
                className="pl-9"
              />
            </div>
          ) : null}

          {!isAdmin ? (
            <div className="text-center py-8 text-muted-foreground">
              <FileText className="h-12 w-12 mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">Admin only</h3>
              <p>Templates are managed by Tenant Admin / Super Admin users.</p>
            </div>
          ) : loadError ? (
            <div className="text-center py-8 text-muted-foreground">
              <FileText className="h-12 w-12 mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">Unable to load templates</h3>
              <p className="max-w-xl mx-auto">{loadError}</p>
            </div>
          ) : templates.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">
              <FileText className="h-12 w-12 mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">No templates yet</h3>
              <p>Create your first notification template to get started.</p>
            </div>
          ) : filteredTemplates.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">
              <Search className="h-12 w-12 mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">No templates found</h3>
              <p>Adjust the search text and try again.</p>
            </div>
          ) : (
            <div className="space-y-3">
              {filteredTemplates.map(t => (
                <div key={t.id} className="flex items-start justify-between gap-4 border rounded-lg p-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <div className="font-medium truncate">{t.name}</div>
                      {t.type ? <Badge variant="secondary">{t.type}</Badge> : null}
                      {t.linkedEntityType ? (
                        <Badge variant="outline" className="gap-1">
                          <Link2 className="h-3 w-3" />
                          {t.linkedEntityType}
                        </Badge>
                      ) : null}
                      <Badge variant={t.isActive ? 'default' : 'outline'}>{t.isActive ? 'Active' : 'Inactive'}</Badge>
                    </div>
                    <div className="text-sm text-muted-foreground mt-1 truncate">
                      <span className="font-medium">Subject:</span> {t.subject}
                    </div>
                    {t.description ? (
                      <div className="text-sm text-muted-foreground mt-1 line-clamp-2">{t.description}</div>
                    ) : null}
                    {t.variables?.length ? (
                      <div className="text-sm text-muted-foreground mt-2 flex flex-wrap gap-2">
                        {t.variables.slice(0, 8).map(v => (
                          <Badge key={v} variant="outline">{`{{${v}}}`}</Badge>
                        ))}
                        {t.variables.length > 8 ? <span className="text-xs">+{t.variables.length - 8} more</span> : null}
                      </div>
                    ) : null}
                  </div>

                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="sm" onClick={() => openCopy(t)}>
                      <Copy className="h-4 w-4 mr-2" />
                      Copy
                    </Button>
                    <Button variant="outline" size="sm" onClick={() => openEdit(t)}>
                      <Pencil className="h-4 w-4 mr-2" />
                      Edit
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        setDeleteTarget(t)
                        setDeleteOpen(true)
                      }}
                    >
                      <Trash2 className="h-4 w-4 mr-2" />
                      Delete
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog
        open={createOpen}
        onOpenChange={(open) => {
          setCreateOpen(open)
          if (!open) resetForm()
        }}
      >
        <DialogContent className="max-w-5xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingTemplate ? 'Edit Notification Template' : 'Create Notification Template'}</DialogTitle>
            <DialogDescription>
              Templates are used for consistent messaging across the ERP.
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={e => setForm(p => ({ ...p, name: e.target.value }))} placeholder="e.g. PR Submitted" />
            </div>
            <div className="space-y-2">
              <Label>Type</Label>
              <Select value={form.type} onValueChange={value => setForm(p => ({ ...p, type: value }))}>
                <SelectTrigger>
                  <SelectValue placeholder="Select template type" />
                </SelectTrigger>
                <SelectContent>
                  {typeOptions.map(option => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Linked Entity</Label>
              <Select value={form.linkedEntityType} onValueChange={handleLinkedEntityChange}>
                <SelectTrigger>
                  <SelectValue placeholder="Select linked entity" />
                </SelectTrigger>
                <SelectContent>
                  {linkedEntityOptions.map(option => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Input
                value={form.description}
                onChange={e => setForm(p => ({ ...p, description: e.target.value }))}
                placeholder="Optional internal description"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label>Subject</Label>
            <Input value={form.subject} onChange={e => setForm(p => ({ ...p, subject: e.target.value }))} placeholder="Email subject (optional for in-app)" />
          </div>

          <div className="space-y-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <Label>HTML Template</Label>
              <Button type="button" variant="outline" size="sm" onClick={openBodyBuilder}>
                <Wand2 className="h-4 w-4 mr-2" />
                Body Builder
              </Button>
            </div>
            <Textarea value={form.htmlTemplate} onChange={e => setForm(p => ({ ...p, htmlTemplate: e.target.value }))} rows={8} className="font-mono" />
          </div>

          <div className="space-y-2">
            <Label>Text Template (optional)</Label>
            <Textarea value={form.textTemplate} onChange={e => setForm(p => ({ ...p, textTemplate: e.target.value }))} rows={4} className="font-mono" />
          </div>

          <div className="space-y-2">
            <Label>Variables (comma-separated)</Label>
            <Input
              value={form.variablesCsv}
              onChange={e => setForm(p => ({ ...p, variablesCsv: e.target.value }))}
              placeholder="e.g. user_name, requisition_number, total_amount"
            />
          </div>

          <div className="rounded-md border p-3 space-y-3">
            <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
              <div className="space-y-1">
                <Label>Available Placeholders</Label>
                <div className="text-xs text-muted-foreground">{form.linkedEntityType || 'General'}</div>
              </div>
              <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                <div className="space-y-2 sm:w-48">
                  <Label>Insert Into</Label>
                  <Select value={placeholderTarget} onValueChange={value => setPlaceholderTarget(value as TemplateInsertTarget)}>
                    <SelectTrigger>
                      <SelectValue placeholder="Target field" />
                    </SelectTrigger>
                    <SelectContent>
                      {insertTargetOptions.map(option => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <Button type="button" variant="outline" size="sm" onClick={() => addPlaceholdersToVariableList()}>
                  <Plus className="h-4 w-4 mr-2" />
                  Add All
                </Button>
              </div>
            </div>

            {availablePlaceholderOptions.length ? (
              <div className="flex flex-wrap gap-2">
                {availablePlaceholderOptions.map(option => (
                  <Button
                    key={option.value}
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-7 px-2 font-mono text-xs"
                    onClick={() => insertPlaceholder(option.value)}
                    title={option.label}
                  >
                    {`{{${option.value}}}`}
                  </Button>
                ))}
              </div>
            ) : (
              <div className="text-sm text-muted-foreground">No placeholders configured for this entity.</div>
            )}
          </div>

          <div className="flex items-center gap-2">
            <Switch checked={form.isActive} onCheckedChange={checked => setForm(p => ({ ...p, isActive: checked }))} />
            <Label>Active</Label>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button onClick={onSave} disabled={!form.name.trim() || !form.htmlTemplate.trim()}>
              {editingTemplate ? 'Save Template' : 'Create Template'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={bodyBuilderOpen} onOpenChange={setBodyBuilderOpen}>
        <DialogContent className="max-w-6xl max-h-[92vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Wand2 className="h-5 w-5" />
              HTML Body Builder
            </DialogTitle>
            <DialogDescription>{form.name || 'Notification template'}</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_280px] gap-4">
            <Tabs value={bodyBuilderTab} onValueChange={value => setBodyBuilderTab(value as BodyBuilderTab)} className="min-w-0">
              <TabsList className="grid w-full grid-cols-3">
                <TabsTrigger value="design" className="gap-2">
                  <Layout className="h-4 w-4" />
                  Design
                </TabsTrigger>
                <TabsTrigger value="code" className="gap-2">
                  <Code className="h-4 w-4" />
                  HTML
                </TabsTrigger>
                <TabsTrigger value="preview" className="gap-2">
                  <Eye className="h-4 w-4" />
                  Preview
                </TabsTrigger>
              </TabsList>

              <TabsContent value="design" className="mt-4">
                <RichTextEditor
                  ref={bodyBuilderEditorRef}
                  value={bodyBuilderHtml}
                  onChange={setBodyBuilderHtml}
                  minHeight="420px"
                />
              </TabsContent>

              <TabsContent value="code" className="mt-4">
                <Textarea
                  ref={bodyBuilderCodeRef}
                  value={bodyBuilderHtml}
                  onChange={e => setBodyBuilderHtml(e.target.value)}
                  rows={18}
                  spellCheck={false}
                  className="min-h-[420px] font-mono text-sm"
                />
              </TabsContent>

              <TabsContent value="preview" className="mt-4">
                <div className="min-h-[420px] rounded-md border bg-white p-4 overflow-auto text-slate-950">
                  {bodyBuilderHtml.trim() ? (
                    <div
                      className="prose prose-sm max-w-none"
                      dangerouslySetInnerHTML={{ __html: bodyBuilderHtml }}
                    />
                  ) : (
                    <div className="flex min-h-[380px] items-center justify-center text-sm text-muted-foreground">
                      Empty body
                    </div>
                  )}
                </div>
              </TabsContent>
            </Tabs>

            <div className="rounded-md border p-3 space-y-3">
              <div className="space-y-1">
                <Label>Placeholders</Label>
                <div className="text-xs text-muted-foreground">{form.linkedEntityType || 'General'}</div>
              </div>

              <div className="flex max-h-[520px] flex-wrap content-start gap-2 overflow-y-auto pr-1">
                {availablePlaceholderOptions.length ? availablePlaceholderOptions.map(option => (
                  <Button
                    key={option.value}
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-7 px-2 font-mono text-xs"
                    onClick={() => insertIntoBodyBuilder(option.value)}
                    title={option.label}
                  >
                    {`{{${option.value}}}`}
                  </Button>
                )) : (
                  <div className="text-sm text-muted-foreground">No placeholders configured.</div>
                )}
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setBodyBuilderOpen(false)}>Cancel</Button>
            <Button type="button" onClick={applyBodyBuilder}>
              <Save className="h-4 w-4 mr-2" />
              Inject Body
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete Template?"
        description={deleteTarget ? `This will delete "${deleteTarget.name}". This action can’t be undone.` : ''}
        confirmText="Delete"
        variant="destructive"
        onConfirm={onDelete}
      />
    </div>
  )
}

export default NotificationTemplates
