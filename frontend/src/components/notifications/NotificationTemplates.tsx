"use client"

import React, { useEffect, useMemo, useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Button } from '../ui/button'
import { Plus, FileText, Trash2, RefreshCw } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../ui/dialog'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Switch } from '../ui/switch'
import { Textarea } from '../ui/textarea'
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
  variables?: string[] | null
  isActive: boolean
  createdBy: string
  createdAt: string
  usageCount: number
}

const NotificationTemplates: React.FC = () => {
  const { toast } = useToast()
  const [templates, setTemplates] = useState<NotificationTemplate[]>([])
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<NotificationTemplate | null>(null)

  const [form, setForm] = useState({
    name: '',
    type: 'General',
    subject: '',
    htmlTemplate: '',
    textTemplate: '',
    variablesCsv: '',
    isActive: true,
  })

  const isAdmin = useMemo(() => authService.hasAnyRole(['SuperAdmin', 'TenantAdmin']), [])

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

  const onCreate = async () => {
    try {
      const variables = form.variablesCsv
        .split(',')
        .map(v => v.trim())
        .filter(Boolean)

      await apiService.request('/notifications/templates', {
        method: 'POST',
        body: JSON.stringify({
          name: form.name,
          type: form.type,
          subject: form.subject,
          htmlTemplate: form.htmlTemplate,
          textTemplate: form.textTemplate || null,
          variables: variables.length ? variables : null,
          isActive: form.isActive,
        }),
      })

      setCreateOpen(false)
      setForm({
        name: '',
        type: 'General',
        subject: '',
        htmlTemplate: '',
        textTemplate: '',
        variablesCsv: '',
        isActive: true,
      })
      toast({ title: 'Template created', description: 'Notification template saved successfully.' })
      await loadTemplates()
    } catch (e: any) {
      toast({
        title: 'Failed to create template',
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
              <Button onClick={() => setCreateOpen(true)} disabled={!isAdmin}>
                <Plus className="h-4 w-4 mr-2" />
                Create Template
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
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
          ) : (
            <div className="space-y-3">
              {templates.map(t => (
                <div key={t.id} className="flex items-start justify-between gap-4 border rounded-lg p-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <div className="font-medium truncate">{t.name}</div>
                      {t.type ? <Badge variant="secondary">{t.type}</Badge> : null}
                      <Badge variant={t.isActive ? 'default' : 'outline'}>{t.isActive ? 'Active' : 'Inactive'}</Badge>
                    </div>
                    <div className="text-sm text-muted-foreground mt-1 truncate">
                      <span className="font-medium">Subject:</span> {t.subject}
                    </div>
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

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Create Notification Template</DialogTitle>
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
              <Input value={form.type} onChange={e => setForm(p => ({ ...p, type: e.target.value }))} placeholder="e.g. Procurement" />
            </div>
          </div>

          <div className="space-y-2">
            <Label>Subject</Label>
            <Input value={form.subject} onChange={e => setForm(p => ({ ...p, subject: e.target.value }))} placeholder="Email subject (optional for in-app)" />
          </div>

          <div className="space-y-2">
            <Label>HTML Template</Label>
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

          <div className="flex items-center gap-2">
            <Switch checked={form.isActive} onCheckedChange={checked => setForm(p => ({ ...p, isActive: checked }))} />
            <Label>Active</Label>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button onClick={onCreate} disabled={!form.name.trim() || !form.htmlTemplate.trim()}>
              Create Template
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
