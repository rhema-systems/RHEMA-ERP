/* eslint-disable @typescript-eslint/no-non-null-assertion */
"use client"

import React, { useEffect, useMemo, useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Plus, Trash2, RefreshCw, Tags, Pencil, Database, Shield } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../ui/dialog'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Switch } from '../ui/switch'
import { Textarea } from '../ui/textarea'
import { ConfirmationDialog } from '../ui/confirmation-dialog'
import { Badge } from '../ui/badge'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../ui/tabs'
import { useToast } from '../ui/use-toast'
import { apiService, type TenantUserMapping } from '../../services/api.service'
import authService from '../../services/auth'

type RecipientKind =
  | 'User'
  | 'Role'
  | 'UserFromData'
  | 'UserFromEmployeeIdData'
  | 'UsersFromData'
  | 'RoleFromData'
  | 'BusinessPartner'
  | 'BusinessPartnerFromData'
  | 'EmailFromData'
  | 'DepartmentType'
type TopicActivity = string
type TopicAudience = string

interface NotificationTemplate {
  id: string
  name: string
  type: string
  subject: string
  isActive: boolean
}

interface RoleDto {
  id: string
  name: string
  description?: string | null
}

interface BusinessPartnerDto {
  id: string
  partnerName?: string | null
  partnerCode?: string | null
  primaryEmail?: string | null
  partnerType?: string | null
}

interface WorkflowEntityTypeInfoDto {
  id: string
  code: string
  name: string
  description?: string | null
  isActive?: boolean
}

interface NotificationTopicRecipientDto {
  id?: string
  recipientKind: RecipientKind | string
  recipientValue: string
  isSystem?: boolean
  sendInApp: boolean
  sendEmail: boolean
  sendSms: boolean
}

interface NotificationTopicDto {
  id: string
  key: string
  activity?: string | null
  audience?: string | null
  name: string
  description?: string | null
  entityType?: string | null
  isSystem?: boolean
  isRequired?: boolean
  isActive: boolean
  enableInApp: boolean
  enableEmail: boolean
  enableSms: boolean
  inAppTitleTemplate?: string | null
  inAppBodyTemplate?: string | null
  smsBodyTemplate?: string | null
  actionUrlTemplate?: string | null
  emailTemplateId?: string | null
  recipients: NotificationTopicRecipientDto[]
}

const emptyForm = {
  id: '' as string,
  key: '',
  activity: 'Created' as TopicActivity,
  audience: 'Internal' as TopicAudience,
  name: '',
  description: '',
  entityType: '',
  isSystem: false,
  isRequired: false,
  isActive: true,
  enableInApp: true,
  enableEmail: true,
  enableSms: false,
  inAppTitleTemplate: '',
  inAppBodyTemplate: '',
  smsBodyTemplate: '',
  actionUrlTemplate: '',
  emailTemplateId: '' as string,
  recipients: [] as NotificationTopicRecipientDto[],
}

const kindOptions: { value: RecipientKind; label: string; hint: string }[] = [
  { value: 'User', label: 'User', hint: 'RecipientValue = UserId (GUID)' },
  { value: 'Role', label: 'Role', hint: 'RecipientValue = Role name (e.g. TenantAdmin)' },
  { value: 'UserFromData', label: 'User (from event data)', hint: 'RecipientValue = data key (e.g. TargetUserId)' },
  { value: 'UserFromEmployeeIdData', label: 'User (from employeeId data)', hint: 'RecipientValue = data key (e.g. EmployeeId)' },
  { value: 'UsersFromData', label: 'Users (from event data)', hint: 'RecipientValue = data key (e.g. TargetUserIds)' },
  { value: 'RoleFromData', label: 'Role (from event data)', hint: 'RecipientValue = data key (e.g. ApproverRole)' },
  { value: 'DepartmentType', label: 'Department', hint: 'RecipientValue = DepartmentType (e.g. Maintenance)' },
  { value: 'BusinessPartner', label: 'Business Partner', hint: 'RecipientValue = BusinessPartnerId' },
  { value: 'BusinessPartnerFromData', label: 'Business Partner (from event data)', hint: 'RecipientValue = data key (e.g. BusinessPartnerId)' },
  { value: 'EmailFromData', label: 'Email(s) (from event data)', hint: 'RecipientValue = data key (e.g. SupplierEmail or Emails)' },
]

const activityOptions: string[] = [
  'Created',
  'Submitted',
  'Published',
  'Assigned',
  'Started',
  'Completed',
  'Approved',
  'Rejected',
  'Evaluated',
  'Awarded',
  'Cancelled',
  'Updated',
  'QuoteSubmitted',
  'QuoteUpdated',
  'PartiallyAwarded',
  'FullyAwarded',
  'WorkflowSubmitted',
  'WorkflowStepAssignment',
  'WorkflowApprovalRequest',
  'WorkflowStepEscalated',
  'WorkflowCompleted',
  'WorkflowRejected',
  'WorkflowStepOverdue',
]

const audienceOptions: string[] = [
  'Internal',
  'Supplier',
  'Customer',
  'General',
]

const businessPartnerIdDataKeys: string[] = [
  'BusinessPartnerId',
  'SupplierBusinessPartnerId',
  'CustomerBusinessPartnerId',
]

const userIdDataKeys: string[] = [
  'TargetUserId',
  'AssignedToUserId',
  'ApproverUserId',
  'InitiatedByUserId',
  'SubmittedByUserId',
  'CreatedByUserId',
  'UserId',
]

const userIdsDataKeys: string[] = [
  'TargetUserIds',
  'UserIds',
]

const employeeIdDataKeys: string[] = [
  'EmployeeId',
  'DriverEmployeeId',
  'TechnicianEmployeeId',
  'InspectorEmployeeId',
]

const roleDataKeys: string[] = [
  'ApproverRole',
  'TargetRole',
  'Role',
]

const emailDataKeys: string[] = [
  'Email',
  'Emails',
  'SupplierEmail',
  'CustomerEmail',
]

const departmentTypeOptions: { value: string; label: string }[] = [
  { value: 'Operations', label: 'Operations' },
  { value: 'Administration', label: 'Administration' },
  { value: 'HumanResources', label: 'Human Resources' },
  { value: 'Finance', label: 'Finance' },
  { value: 'IT', label: 'IT' },
  { value: 'Maintenance', label: 'Maintenance' },
  { value: 'Safety', label: 'Safety' },
  { value: 'QualityAssurance', label: 'Quality Assurance' },
  { value: 'RnD', label: 'R&D' },
  { value: 'Marketing', label: 'Marketing' },
  { value: 'Sales', label: 'Sales' },
]

const workflowEventKeysByActivity: Record<string, string[]> = {
  WorkflowSubmitted: [
    'workflowInstanceId',
    'workflowDefinitionId',
    'definitionName',
    'currentStepName',
    'InitiatedByUserId',
    'TargetUserId',
  ],
  WorkflowStepAssignment: [
    'stepInstanceId',
    'workflowInstanceId',
    'workflowStepId',
    'AssignedToUserId',
    'TargetUserId',
  ],
  WorkflowApprovalRequest: [
    'approvalId',
    'stepInstanceId',
    'workflowInstanceId',
    'workflowStepId',
    'ApproverUserId',
    'ApproverRole',
    'TargetUserId',
    'TargetRole',
  ],
  WorkflowStepEscalated: [
    'stepInstanceId',
    'workflowInstanceId',
    'workflowStepId',
    'escalationReason',
    'TargetUserIds',
  ],
  WorkflowCompleted: [
    'workflowInstanceId',
    'workflowDefinitionId',
    'InitiatedByUserId',
    'TargetUserId',
  ],
  WorkflowRejected: [
    'workflowInstanceId',
    'workflowDefinitionId',
    'rejectedById',
    'comments',
    'InitiatedByUserId',
    'TargetUserId',
  ],
  WorkflowStepOverdue: [
    'stepInstanceId',
    'workflowInstanceId',
    'workflowStepId',
    'dueDate',
    'AssignedToUserId',
    'TargetUserId',
  ],
}

const workflowCommonEventKeys: string[] = [
  'EntityType',
  'EntityId',
  'EntityNumber',
  'ActionUrl',
  'Title',
  'Message',
]

const isWorkflowActivity = (activity: string) => (activity || '').startsWith('Workflow')

const getWorkflowEventKeys = (activity: string) => {
  const specific = workflowEventKeysByActivity[activity] || []
  return Array.from(new Set([...workflowCommonEventKeys, ...specific]))
}

const normalizeKeySegment = (value: string) => (value || '').replace(/[^A-Za-z0-9]/g, '').trim()
const buildTopicKey = (entityType: string, activity: string, audience: string) => {
  const et = normalizeKeySegment(entityType)
  const act = normalizeKeySegment(activity)
  const aud = normalizeKeySegment(audience)
  if (!et || !act || !aud) return ''
  return `${et}.${act}.${aud}`
}

const NotificationTopics: React.FC = () => {
  const { toast } = useToast()
  const isAdmin = useMemo(() => authService.hasAnyRole(['SuperAdmin', 'TenantAdmin']), [])

  const [topics, setTopics] = useState<NotificationTopicDto[]>([])
  const [templates, setTemplates] = useState<NotificationTemplate[]>([])
  const [tenantUsers, setTenantUsers] = useState<TenantUserMapping[]>([])
  const [roles, setRoles] = useState<RoleDto[]>([])
  const [businessPartners, setBusinessPartners] = useState<BusinessPartnerDto[]>([])
  const [entityTypes, setEntityTypes] = useState<WorkflowEntityTypeInfoDto[]>([])

  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [seedingEntityTypes, setSeedingEntityTypes] = useState(false)
  const [seedingSystemTopics, setSeedingSystemTopics] = useState(false)

  const [createOpen, setCreateOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<NotificationTopicDto | null>(null)

  const [form, setForm] = useState({ ...emptyForm })
  const [search, setSearch] = useState('')

  const loadAll = async () => {
    if (!isAdmin) {
      setTopics([])
      setTemplates([])
      setTenantUsers([])
      setRoles([])
      setBusinessPartners([])
      setEntityTypes([])
      setLoadError(null)
      return
    }

    setLoading(true)
    setLoadError(null)
    try {
      // Always resolve tenant context from the server (localStorage may be unset on first load).
      let tenantId: string | undefined = authService.getCurrentTenant()?.id || undefined
      if (!tenantId) {
        try {
          const me = await apiService.getCurrentUser()
          tenantId = me?.currentTenantId || me?.tenantId || undefined
        } catch {
          tenantId = undefined
        }
      }

      const [topicList, templateList, roleList, partnerList, entityTypesResponse] = await Promise.all([
        apiService.request<NotificationTopicDto[]>('/notification-topics'),
        apiService.request<NotificationTemplate[]>('/notifications/templates'),
        apiService.request<RoleDto[]>('/role'),
        apiService.request<BusinessPartnerDto[]>('/procurement/business-partners/active'),
        apiService.request<any>('/workflow/entity-types'),
      ])

      setTopics(topicList || [])
      setTemplates(templateList || [])
      setRoles(roleList || [])
      setBusinessPartners(partnerList || [])
      setEntityTypes((entityTypesResponse?.data || []) as WorkflowEntityTypeInfoDto[])

      if (tenantId) {
        try {
          const users = await apiService.getTenantUsers(tenantId)
          const active = (users || []).filter(m => !!m?.isActive && !!m?.user?.isActive)
          setTenantUsers(active)
        } catch {
          setTenantUsers([])
        }
      } else {
        setTenantUsers([])
      }
    } catch (e: any) {
      setLoadError(e?.message || 'Failed to load notification topics')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadAll()
     
  }, [])

  const seedEntityTypes = async () => {
    if (!isAdmin) return

    try {
      setSeedingEntityTypes(true)
      const seeded = await apiService.request<any>('/workflow/entity-types/seed', { method: 'POST' })
      const count = Array.isArray(seeded) ? seeded.length : (seeded?.length ?? 0)
      toast({
        title: 'Entity types seeded',
        description: count ? `${count} entity type(s) are now available.` : 'Entity types are now available.',
      })
      await loadAll()
    } catch (e: any) {
      toast({
        title: 'Failed to seed entity types',
        description: e?.message || 'Unable to seed workflow entity types',
        variant: 'destructive',
      })
    } finally {
      setSeedingEntityTypes(false)
    }
  }

  const seedSystemTopics = async () => {
    if (!isAdmin) return

    try {
      setSeedingSystemTopics(true)
      const res = await apiService.request<any>('/notification-topics/seed-system', { method: 'POST' })
      const created = res?.createdTopics ?? 0
      const updated = res?.updatedTopics ?? 0
      const recipients = res?.createdRecipients ?? 0

      toast({
        title: 'System topics seeded',
        description: `Created ${created}, updated ${updated}, added ${recipients} system recipient rule(s).`,
      })
      await loadAll()
    } catch (e: any) {
      toast({
        title: 'Failed to seed system topics',
        description: e?.message || 'Unable to seed system notification topics',
        variant: 'destructive',
      })
    } finally {
      setSeedingSystemTopics(false)
    }
  }

  const filteredTopics = useMemo(() => {
    const s = search.trim().toLowerCase()
    if (!s) return topics
    return topics.filter(t =>
      (t.key || '').toLowerCase().includes(s) ||
      (t.name || '').toLowerCase().includes(s) ||
      (t.entityType || '').toLowerCase().includes(s)
    )
  }, [topics, search])

  const openCreate = () => {
    setForm({ ...emptyForm })
    setCreateOpen(true)
  }

  const openEdit = (t: NotificationTopicDto) => {
    const parts = (t.key || '').split('.').filter(Boolean)
    const entityType = t.entityType || parts[0] || ''
    const activity = t.activity || parts[1] || 'Updated'
    const audience = t.audience || parts[2] || 'Internal'
    setForm({
      id: t.id,
      key: t.key || '',
      activity,
      audience,
      name: t.name || '',
      description: t.description || '',
      entityType,
      isSystem: !!t.isSystem,
      isRequired: !!t.isRequired,
      isActive: !!t.isActive,
      enableInApp: !!t.enableInApp,
      enableEmail: !!t.enableEmail,
      inAppTitleTemplate: t.inAppTitleTemplate || '',
      inAppBodyTemplate: t.inAppBodyTemplate || '',
      actionUrlTemplate: t.actionUrlTemplate || '',
      emailTemplateId: t.emailTemplateId || '',
      recipients: (t.recipients || []).map(r => ({
        id: r.id,
        recipientKind: (r.recipientKind as any) || 'Role',
        recipientValue: r.recipientValue || '',
        isSystem: !!r.isSystem,
        sendInApp: !!r.sendInApp,
        sendEmail: !!r.sendEmail,
        sendSms: !!(r as any).sendSms,
      })),
    })
    setEditOpen(true)
  }

  const upsertPayload = () => ({
    key: buildTopicKey(form.entityType, form.activity, form.audience),
    activity: form.activity,
    audience: form.audience,
    name: form.name.trim(),
    description: form.description?.trim() || null,
    entityType: form.entityType?.trim() || null,
    isActive: !!form.isActive,
    enableInApp: !!form.enableInApp,
    enableEmail: !!form.enableEmail,
    enableSms: !!form.enableSms,
    inAppTitleTemplate: form.inAppTitleTemplate?.trim() || null,
    inAppBodyTemplate: form.inAppBodyTemplate?.trim() || null,
    smsBodyTemplate: form.smsBodyTemplate?.trim() || null,
    actionUrlTemplate: form.actionUrlTemplate?.trim() || null,
    emailTemplateId: form.emailTemplateId ? form.emailTemplateId : null,
    recipients: (form.recipients || [])
      .filter(r => !r.isSystem)
      .map(r => ({
        recipientKind: (r.recipientKind || '').trim(),
        recipientValue: (r.recipientValue || '').trim(),
        sendInApp: !!r.sendInApp,
        sendEmail: !!r.sendEmail,
        sendSms: !!r.sendSms,
      }))
      .filter(r => r.recipientKind && r.recipientValue),
  })

  const onCreate = async () => {
    try {
      await apiService.request('/notification-topics', {
        method: 'POST',
        body: JSON.stringify(upsertPayload()),
      })
      setCreateOpen(false)
      toast({ title: 'Topic created', description: 'Notification topic saved successfully.' })
      await loadAll()
    } catch (e: any) {
      toast({
        title: 'Failed to create topic',
        description: e?.message || 'Please check your inputs and try again.',
        variant: 'destructive',
      })
    }
  }

  const onUpdate = async () => {
    try {
      await apiService.request(`/notification-topics/${form.id}`, {
        method: 'PUT',
        body: JSON.stringify(upsertPayload()),
      })
      setEditOpen(false)
      toast({ title: 'Topic updated', description: 'Notification topic updated successfully.' })
      await loadAll()
    } catch (e: any) {
      toast({
        title: 'Failed to update topic',
        description: e?.message || 'Please check your inputs and try again.',
        variant: 'destructive',
      })
    }
  }

  const onDelete = async () => {
    if (!deleteTarget) return
    try {
      await apiService.request(`/notification-topics/${deleteTarget.id}`, { method: 'DELETE' })
      toast({ title: 'Topic deleted', description: `Deleted "${deleteTarget.name}".` })
      setDeleteOpen(false)
      setDeleteTarget(null)
      await loadAll()
    } catch (e: any) {
      toast({
        title: 'Failed to delete topic',
        description: e?.message || 'Please try again.',
        variant: 'destructive',
      })
    }
  }

  const addRecipient = () => {
    setForm(f => ({
      ...f,
      recipients: [
        ...(f.recipients || []),
        { recipientKind: 'Role', recipientValue: '', sendInApp: true, sendEmail: true, sendSms: false },
      ],
    }))
  }

  const updateRecipient = (index: number, patch: Partial<NotificationTopicRecipientDto>) => {
    setForm(f => {
      const copy = [...(f.recipients || [])]
      copy[index] = { ...copy[index], ...patch }
      return { ...f, recipients: copy }
    })
  }

  const removeRecipient = (index: number) => {
    setForm(f => ({ ...f, recipients: (f.recipients || []).filter((_, i) => i !== index) }))
  }

  const templateNameById = useMemo(() => {
    const map = new Map<string, string>()
    templates.forEach(t => map.set(t.id, t.name))
    return map
  }, [templates])

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <div className="flex justify-between items-center gap-3">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Tags className="h-5 w-5" />
                Notification Topics / Groups
              </CardTitle>
              <CardDescription>
                Configure reusable notification topics per entity type (email + in-app) and publish by topic key.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Button variant="outline" onClick={loadAll} disabled={loading || !isAdmin}>
                <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
                Refresh
              </Button>
              <Button variant="outline" onClick={seedEntityTypes} disabled={!isAdmin || seedingEntityTypes}>
                <Database className={`h-4 w-4 mr-2 ${seedingEntityTypes ? 'animate-spin' : ''}`} />
                {seedingEntityTypes ? 'Seeding...' : 'Seed Entity Types'}
              </Button>
              <Button variant="outline" onClick={seedSystemTopics} disabled={!isAdmin || seedingSystemTopics}>
                <Shield className={`h-4 w-4 mr-2 ${seedingSystemTopics ? 'animate-spin' : ''}`} />
                {seedingSystemTopics ? 'Seeding...' : 'Seed System Topics'}
              </Button>
              <Button onClick={openCreate} disabled={!isAdmin}>
                <Plus className="h-4 w-4 mr-2" />
                Create Topic
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {!isAdmin ? (
            <div className="text-center py-8 text-muted-foreground">
              <Tags className="h-12 w-12 mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">Admin only</h3>
              <p>Topics are managed by Tenant Admin / Super Admin users.</p>
            </div>
          ) : loadError ? (
            <div className="text-center py-8 text-muted-foreground">
              <Tags className="h-12 w-12 mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">Unable to load topics</h3>
              <p className="max-w-xl mx-auto">{loadError}</p>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
                <div className="flex items-center gap-2">
                  <Input
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    placeholder="Search by key, name, entity type..."
                    className="w-full sm:w-80"
                  />
                </div>
                <div className="text-sm text-muted-foreground">
                  {filteredTopics.length.toLocaleString()} topic{filteredTopics.length === 1 ? '' : 's'}
                </div>
              </div>

              {filteredTopics.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  <Tags className="h-12 w-12 mx-auto mb-4" />
                  <h3 className="text-lg font-medium mb-2">No topics yet</h3>
                  <p>Create your first notification topic to get started.</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {filteredTopics.map(t => (
                    <div key={t.id} className="flex items-start justify-between gap-4 border rounded-lg p-4">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <div className="font-medium truncate">{t.name}</div>
                          {t.entityType ? <Badge variant="secondary">{t.entityType}</Badge> : null}
                          {t.isSystem ? <Badge variant="outline">System</Badge> : null}
                          {t.isRequired ? <Badge variant="secondary">Required</Badge> : null}
                          <Badge variant={t.isActive ? 'default' : 'outline'}>{t.isActive ? 'Active' : 'Inactive'}</Badge>
                          <Badge variant={t.enableInApp ? 'secondary' : 'outline'}>In-app</Badge>
                          <Badge variant={t.enableEmail ? 'secondary' : 'outline'}>Email</Badge>
                        </div>
                        <div className="text-sm text-muted-foreground mt-1 truncate">
                          <span className="font-medium">Key:</span> {t.key}
                        </div>
                        {t.description ? (
                          <div className="text-sm text-muted-foreground mt-2 line-clamp-2">{t.description}</div>
                        ) : null}
                        <div className="text-xs text-muted-foreground mt-2 flex flex-wrap gap-2">
                          <span>Recipients: {(t.recipients || []).length}</span>
                          {t.emailTemplateId ? (
                            <span>
                              Email template: <span className="font-medium">{templateNameById.get(t.emailTemplateId) || 'Selected'}</span>
                            </span>
                          ) : (
                            <span>Email template: —</span>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Button variant="outline" size="sm" onClick={() => openEdit(t)}>
                          <Pencil className="h-4 w-4 mr-2" />
                          Edit
                        </Button>
                        <Button
                          variant="destructive"
                          size="sm"
                          disabled={!!t.isSystem}
                          onClick={() => {
                            setDeleteTarget(t)
                            setDeleteOpen(true)
                          }}
                        >
                          <Trash2 className="h-4 w-4 mr-2" />
                          {t.isSystem ? 'System' : 'Delete'}
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-6xl w-[95vw] max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Create Notification Topic</DialogTitle>
            <DialogDescription>
              Topics are reusable notification definitions. Publish by <span className="font-mono">TopicKey</span>.
            </DialogDescription>
          </DialogHeader>

          <TopicForm
            form={form}
            setForm={setForm}
            templates={templates}
            tenantUsers={tenantUsers}
            entityTypes={entityTypes}
            roles={roles}
            businessPartners={businessPartners}
            isEditMode={false}
            onAddRecipient={addRecipient}
            onUpdateRecipient={updateRecipient}
            onRemoveRecipient={removeRecipient}
          />

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button onClick={onCreate} disabled={!buildTopicKey(form.entityType, form.activity, form.audience) || !form.name.trim()}>Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-w-6xl w-[95vw] max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Notification Topic</DialogTitle>
            <DialogDescription>Update topic configuration and recipients.</DialogDescription>
          </DialogHeader>

          <TopicForm
            form={form}
            setForm={setForm}
            templates={templates}
            tenantUsers={tenantUsers}
            entityTypes={entityTypes}
            roles={roles}
            businessPartners={businessPartners}
            isEditMode={true}
            onAddRecipient={addRecipient}
            onUpdateRecipient={updateRecipient}
            onRemoveRecipient={removeRecipient}
          />

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)}>Cancel</Button>
            <Button onClick={onUpdate} disabled={!form.name.trim()}>Save changes</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete Topic"
        description={deleteTarget ? `This will delete "${deleteTarget.name}". This cannot be undone.` : 'This will delete the topic.'}
        confirmText="Delete"
        cancelText="Cancel"
        onConfirm={onDelete}
        variant="destructive"
      />
    </div>
  )
}

const TopicForm: React.FC<{
  form: typeof emptyForm
  setForm: React.Dispatch<React.SetStateAction<typeof emptyForm>>
  templates: NotificationTemplate[]
  tenantUsers: TenantUserMapping[]
  entityTypes: WorkflowEntityTypeInfoDto[]
  roles: RoleDto[]
  businessPartners: BusinessPartnerDto[]
  isEditMode: boolean
  onAddRecipient: () => void
  onUpdateRecipient: (index: number, patch: Partial<NotificationTopicRecipientDto>) => void
  onRemoveRecipient: (index: number) => void
}> = ({ form, setForm, templates, tenantUsers, entityTypes, roles, businessPartners, isEditMode, onAddRecipient, onUpdateRecipient, onRemoveRecipient }) => {
  const update = (patch: Partial<typeof emptyForm>) => setForm(f => ({ ...f, ...patch }))

  const userOptions = useMemo(() => {
    return (tenantUsers || [])
      .filter(m => !!m?.isActive && !!m?.user?.isActive)
      .map(m => m.user)
      .sort((a, b) => (a.fullName || a.username).localeCompare(b.fullName || b.username))
  }, [tenantUsers])

  const entityTypeOptions = useMemo(() => {
    const list = (entityTypes || [])
      .filter(et => !!(et?.name || '').trim())
      .map(et => ({ value: et.name!.trim(), label: et.code ? `${et.name} (${et.code})` : et.name! }))
      .sort((a, b) => a.label.localeCompare(b.label))
    return list
  }, [entityTypes])

  const computedKey = useMemo(
    () => buildTopicKey(form.entityType, form.activity, form.audience),
    [form.entityType, form.activity, form.audience]
  )

  return (
    <Tabs defaultValue="definition" className="space-y-4">
      <TabsList className="grid w-full grid-cols-3">
        <TabsTrigger value="definition">Definition</TabsTrigger>
        <TabsTrigger value="templates">Templates</TabsTrigger>
        <TabsTrigger value="recipients">Recipients</TabsTrigger>
      </TabsList>

      <TabsContent value="definition" className="space-y-4">
        <div className="grid grid-cols-1 lg:grid-cols-4 gap-4">
          <div className="space-y-2">
            <Label htmlFor="topic-entity">Entity Type</Label>
            <Select
              value={form.entityType ? form.entityType : '__none__'}
              onValueChange={v => update({ entityType: v === '__none__' ? '' : v })}
              disabled={isEditMode}
            >
              <SelectTrigger id="topic-entity">
                <SelectValue placeholder="Select entity type..." />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">Select entity type...</SelectItem>
                {entityTypeOptions.map(o => (
                  <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Activity</Label>
            <Select value={form.activity} onValueChange={v => update({ activity: v })} disabled={isEditMode}>
              <SelectTrigger>
                <SelectValue placeholder="Select activity..." />
              </SelectTrigger>
              <SelectContent>
                {activityOptions.map(a => (
                  <SelectItem key={a} value={a}>{a}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Audience</Label>
            <Select value={form.audience} onValueChange={v => update({ audience: v })} disabled={isEditMode}>
              <SelectTrigger>
                <SelectValue placeholder="Select audience..." />
              </SelectTrigger>
              <SelectContent>
                {audienceOptions.map(a => (
                  <SelectItem key={a} value={a}>{a}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Key (system)</Label>
            <Input value={computedKey} readOnly />
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-4 gap-4 items-end">
          <div className="lg:col-span-2 space-y-2">
            <Label htmlFor="topic-name">Name</Label>
            <Input
              id="topic-name"
              value={form.name}
              onChange={e => update({ name: e.target.value })}
              placeholder="Human-friendly name"
            />
          </div>

          <div className="lg:col-span-2 flex flex-wrap items-center gap-x-6 gap-y-2 pt-2">
            <div className="flex items-center gap-2">
              <Switch checked={form.isActive} onCheckedChange={v => update({ isActive: v })} disabled={isEditMode && !!form.isRequired} />
              <Label>Active</Label>
            </div>
            <div className="flex items-center gap-2">
              <Switch checked={form.enableInApp} onCheckedChange={v => update({ enableInApp: v })} />
              <Label>In-app</Label>
            </div>
            <div className="flex items-center gap-2">
              <Switch checked={form.enableEmail} onCheckedChange={v => update({ enableEmail: v })} />
              <Label>Email</Label>
            </div>
            <div className="flex items-center gap-2">
              <Switch checked={form.enableSms} onCheckedChange={v => update({ enableSms: v })} />
              <Label>SMS</Label>
            </div>
          </div>
        </div>

        {isEditMode && form.isSystem ? (
          <div className="text-sm text-muted-foreground">
            This is a <span className="font-medium">system topic</span>. It can’t be deleted, and system recipient rules are protected.
          </div>
        ) : null}

        <div className="space-y-2">
          <Label htmlFor="topic-desc">Description (optional)</Label>
          <Textarea
            id="topic-desc"
            value={form.description}
            onChange={e => update({ description: e.target.value })}
            placeholder="What does this topic represent?"
          />
        </div>
      </TabsContent>

      <TabsContent value="templates" className="space-y-4">
        {isWorkflowActivity(form.activity) ? (
          <div className="rounded-lg border p-4 space-y-2">
            <div className="font-medium">Workflow event keys</div>
            <div className="text-sm text-muted-foreground">
              These keys are published automatically for workflow activities and can be used in templates and recipient routing (from event data).
            </div>
            <div className="flex flex-wrap gap-2 pt-1">
              {getWorkflowEventKeys(form.activity).map(k => (
                <Badge key={k} variant="secondary">{k}</Badge>
              ))}
            </div>
          </div>
        ) : null}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          <div className="space-y-2">
            <Label htmlFor="topic-inapp-title">In-app Title Template (optional)</Label>
            <Input
              id="topic-inapp-title"
              value={form.inAppTitleTemplate}
              onChange={e => update({ inAppTitleTemplate: e.target.value })}
              placeholder="e.g. Quote submitted for {{RfqNumber}}"
            />
          </div>
          <div className="space-y-2 lg:col-span-2">
            <Label htmlFor="topic-action-url">Action URL Template (optional)</Label>
            <Input
              id="topic-action-url"
              value={form.actionUrlTemplate}
              onChange={e => update({ actionUrlTemplate: e.target.value })}
              placeholder="e.g. /rfq/{{RfqId}}/quotes/{{QuoteId}}"
            />
          </div>
        </div>

        <div className="space-y-2">
          <Label htmlFor="topic-inapp-body">In-app Body Template (optional)</Label>
          <Textarea
            id="topic-inapp-body"
            value={form.inAppBodyTemplate}
            onChange={e => update({ inAppBodyTemplate: e.target.value })}
            placeholder="e.g. Supplier {{SupplierName}} submitted a quote for {{RfqNumber}}."
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="topic-sms-body">SMS Body Template (optional)</Label>
          <Textarea
            id="topic-sms-body"
            value={form.smsBodyTemplate}
            onChange={e => update({ smsBodyTemplate: e.target.value })}
            placeholder="e.g. {{Title}} - {{Message}}"
          />
          <div className="text-xs text-muted-foreground">Used when the topic and recipient have SMS enabled.</div>
        </div>

        <div className="space-y-2">
          <Label>Email Template (optional)</Label>
          <Select
            value={form.emailTemplateId ? form.emailTemplateId : '__none__'}
            onValueChange={v => update({ emailTemplateId: v === '__none__' ? '' : v })}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select an email template..." />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__none__">None</SelectItem>
              {(templates || []).filter(t => !!(t?.id || '').trim()).map(t => (
                <SelectItem key={t.id} value={t.id}>
                  {t.name}{t.isActive ? '' : ' (inactive)'}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </TabsContent>

      <TabsContent value="recipients" className="space-y-4">
        <div className="border rounded-lg p-4 space-y-3">
          <div className="flex items-center justify-between">
            <div>
              <div className="font-medium">Recipients</div>
              <div className="text-sm text-muted-foreground">
                Recipient value is always selected from a dropdown (role/user/business partner/data key).
              </div>
            </div>
            <Button variant="outline" size="sm" onClick={onAddRecipient}>
              <Plus className="h-4 w-4 mr-2" />
              Add Recipient
            </Button>
          </div>

          {(form.recipients || []).length === 0 ? (
            <div className="text-sm text-muted-foreground">No recipients configured.</div>
          ) : (
            <div className="space-y-3">
              {form.recipients.map((r, idx) => {
                const hint = kindOptions.find(k => k.value === r.recipientKind)?.hint
                const kind = (r.recipientKind as string) || 'Role'
                const isSystemRule = !!r.isSystem

                const roleOptions = (roles || [])
                  .filter(rr => !!(rr?.name || '').trim())
                  .map(rr => ({ value: rr.name.trim(), label: rr.name.trim() }))

                const userSelectOptions = (userOptions || [])
                  .filter(u => !!(u?.id || '').trim())
                  .map(u => ({
                    value: u.id,
                    label: `${u.fullName || u.username} (${u.email})`,
                  }))

                const partnerSelectOptions = (businessPartners || [])
                  .filter(bp => !!(bp?.id || '').trim())
                  .map(bp => ({
                    value: bp.id,
                    label: `${bp.partnerName || bp.partnerCode || bp.id}${bp.primaryEmail ? ` (${bp.primaryEmail})` : ''}`,
                  }))

                return (
                  <div key={`${r.recipientKind}-${idx}`} className="grid grid-cols-1 lg:grid-cols-12 gap-3 items-start border rounded-md p-3">
                    <div className="lg:col-span-3 space-y-2">
                      <Label>Kind</Label>
                      <Select
                        value={kind}
                        onValueChange={v => onUpdateRecipient(idx, { recipientKind: v as any, recipientValue: '' })}
                        disabled={isSystemRule}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select kind..." />
                        </SelectTrigger>
                        <SelectContent>
                          {kindOptions.map(k => (
                            <SelectItem key={k.value} value={k.value}>{k.label}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      {isSystemRule ? <Badge variant="outline">System</Badge> : null}
                      {hint ? <div className="text-xs text-muted-foreground">{hint}</div> : null}
                    </div>

                    <div className="lg:col-span-6 space-y-2">
                      <Label>Value</Label>
                      {kind === 'Role' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a role..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a role...</SelectItem>
                            {roleOptions.map(o => (
                              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'User' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a user..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a user...</SelectItem>
                            {userSelectOptions.map(o => (
                              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'UserFromData' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a data key..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a data key...</SelectItem>
                            {userIdDataKeys.map(k => (
                              <SelectItem key={k} value={k}>{k}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'UserFromEmployeeIdData' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a data key..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a data key...</SelectItem>
                            {employeeIdDataKeys.map(k => (
                              <SelectItem key={k} value={k}>{k}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'UsersFromData' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a data key..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a data key...</SelectItem>
                            {userIdsDataKeys.map(k => (
                              <SelectItem key={k} value={k}>{k}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'RoleFromData' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a data key..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a data key...</SelectItem>
                            {roleDataKeys.map(k => (
                              <SelectItem key={k} value={k}>{k}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'DepartmentType' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a department..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a department...</SelectItem>
                            {departmentTypeOptions.map(o => (
                              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'BusinessPartner' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a business partner..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a business partner...</SelectItem>
                            {partnerSelectOptions.map(o => (
                              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : kind === 'BusinessPartnerFromData' ? (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a data key..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a data key...</SelectItem>
                            {businessPartnerIdDataKeys.map(k => (
                              <SelectItem key={k} value={k}>{k}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      ) : (
                        <Select
                          value={r.recipientValue ? r.recipientValue : '__none__'}
                          onValueChange={v => onUpdateRecipient(idx, { recipientValue: v === '__none__' ? '' : v })}
                          disabled={isSystemRule}
                        >
                          <SelectTrigger>
                            <SelectValue placeholder="Select a data key..." />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__none__">Select a data key...</SelectItem>
                            {emailDataKeys.map(k => (
                              <SelectItem key={k} value={k}>{k}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      )}
                    </div>

                    <div className="lg:col-span-2 space-y-2">
                      <Label>Channels</Label>
                      <div className="flex items-center gap-3 pt-1">
                        <div className="flex items-center gap-2">
                          <Switch checked={!!r.sendInApp} onCheckedChange={v => onUpdateRecipient(idx, { sendInApp: v })} disabled={isSystemRule} />
                          <span className="text-sm">In-app</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <Switch checked={!!r.sendEmail} onCheckedChange={v => onUpdateRecipient(idx, { sendEmail: v })} disabled={isSystemRule} />
                          <span className="text-sm">Email</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <Switch checked={!!r.sendSms} onCheckedChange={v => onUpdateRecipient(idx, { sendSms: v })} disabled={isSystemRule} />
                          <span className="text-sm">SMS</span>
                        </div>
                      </div>
                    </div>

                    <div className="lg:col-span-1 flex justify-end pt-7">
                      <Button variant="ghost" size="icon" onClick={() => onRemoveRecipient(idx)} title="Remove" disabled={isSystemRule}>
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </TabsContent>
    </Tabs>
  )
}

export default NotificationTopics
