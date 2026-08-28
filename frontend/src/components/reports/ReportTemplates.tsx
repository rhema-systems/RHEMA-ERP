'use client'

import React, { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, Copy, Download, FileText, Pencil, Play, Plus, Send, Trash2 } from 'lucide-react'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Card, CardContent } from '../ui/card'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../ui/dialog'
import { Input } from '../ui/input'
import { Label } from '../ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Textarea } from '../ui/textarea'
import { useToast } from '../../hooks/use-toast'
import {
  CreateReportTemplateDto,
  ReportResult,
  ReportTemplate,
  reportsService,
  UpdateReportTemplateDto,
} from '../../services/reports'

interface ReportTemplatesProps {
  onCreateFromTemplate?: (templateId: string) => void
}

type OutputFormat = 'Online' | 'XLSX' | 'PDF'
type FormState = CreateReportTemplateDto & {
  savedFiltersJson: string
  generationMetadataJson: string
}

const audiences = ['PPA/GHANEPS', 'Finance', 'Audit', 'Board'] as const
const cadences = ['Monthly', 'Quarterly', 'AdHoc'] as const
const formats: OutputFormat[] = ['Online', 'XLSX', 'PDF']

const emptyForm = (): FormState => ({
  reportId: '',
  templateKey: '',
  name: '',
  description: '',
  category: 'Procurement',
  type: 'Table',
  audience: 'Finance',
  cadence: 'AdHoc',
  defaultOutputFormat: 'Online',
  outputFormats: ['Online', 'XLSX', 'PDF'],
  savedFiltersJson: '{}',
  generationMetadataJson: '{\n  "title": "",\n  "preparedFor": ""\n}',
})

function parseObject(value: string, label: string): Record<string, unknown> | undefined {
  if (!value.trim()) return undefined
  const parsed = JSON.parse(value)
  if (!parsed || Array.isArray(parsed) || typeof parsed !== 'object') {
    throw new Error(`${label} must be a JSON object.`)
  }
  return parsed as Record<string, unknown>
}

function toForm(template: ReportTemplate): FormState {
  return {
    reportId: template.reportId ?? '',
    templateKey: template.templateKey,
    name: template.name,
    description: template.description,
    category: template.category,
    type: template.type,
    audience: template.audience,
    cadence: template.cadence,
    defaultOutputFormat: template.defaultOutputFormat,
    outputFormats: template.outputFormats,
    savedFiltersJson: JSON.stringify(template.savedFilters ?? {}, null, 2),
    generationMetadataJson: JSON.stringify(template.generationMetadata ?? {}, null, 2),
  }
}

export default function ReportTemplates(_props: ReportTemplatesProps) {
  const queryClient = useQueryClient()
  const { toast } = useToast()
  const [audience, setAudience] = useState('all')
  const [cadence, setCadence] = useState('all')
  const [status, setStatus] = useState('all')
  const [search, setSearch] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editing, setEditing] = useState<ReportTemplate | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [onlineResult, setOnlineResult] = useState<ReportResult | null>(null)
  const [onlineTemplate, setOnlineTemplate] = useState<ReportTemplate | null>(null)

  const templateQuery = useQuery({
    queryKey: ['report-templates', audience, cadence, status],
    queryFn: () => reportsService.getReportTemplates({
      audience: audience === 'all' ? undefined : audience,
      cadence: cadence === 'all' ? undefined : cadence,
      status: status === 'all' ? undefined : status,
    }),
  })
  const reportQuery = useQuery({
    queryKey: ['report-template-definitions'],
    queryFn: () => reportsService.getReportsForAdmin(undefined, 'published'),
  })

  const templates = useMemo(() => {
    const needle = search.trim().toLowerCase()
    if (!needle) return templateQuery.data ?? []
    return (templateQuery.data ?? []).filter((template) =>
      [template.name, template.templateKey, template.reportName, template.category]
        .some((value) => value?.toLowerCase().includes(needle)))
  }, [search, templateQuery.data])

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['report-templates'] })
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      const { savedFiltersJson, generationMetadataJson, ...templateFields } = form
      const payload: CreateReportTemplateDto = {
        ...templateFields,
        savedFilters: parseObject(savedFiltersJson, 'Saved filters'),
        generationMetadata: parseObject(generationMetadataJson, 'Generation metadata'),
      }
      if (editing) {
        return reportsService.updateReportTemplate(editing.id, {
          ...payload,
          rowVersion: editing.rowVersion,
        } as UpdateReportTemplateDto)
      }
      return reportsService.createReportTemplate(payload)
    },
    onSuccess: async () => {
      setDialogOpen(false)
      await refresh()
      toast({ title: editing ? 'Template updated' : 'Draft template created' })
    },
    onError: (error: Error) => toast({ title: 'Unable to save template', description: error.message, variant: 'destructive' }),
  })

  const actionMutation = useMutation({
    mutationFn: async ({ template, action }: { template: ReportTemplate; action: string }) => {
      if (action === 'publish') return reportsService.publishReportTemplate(template)
      if (action === 'archive') return reportsService.archiveReportTemplate(template)
      if (action === 'clone') return reportsService.cloneReportTemplate(template)
      if (action === 'delete') {
        await reportsService.deleteReportTemplate(template)
        return null
      }
      throw new Error('Unsupported template action.')
    },
    onSuccess: async (_result, variables) => {
      await refresh()
      toast({ title: `Template ${variables.action} completed` })
    },
    onError: (error: Error) => toast({ title: 'Template action failed', description: error.message, variant: 'destructive' }),
  })

  const generateMutation = useMutation({
    mutationFn: async (template: ReportTemplate) => {
      if (template.defaultOutputFormat === 'Online') {
        const result = await reportsService.executeReportTemplate(template.id, {
          format: 'Online', page: 1, pageSize: 100,
        })
        setOnlineTemplate(template)
        setOnlineResult(result)
        return `${result.totalRows} row(s) generated online.`
      }
      const download = await reportsService.exportReportTemplate(template.id, {
        format: template.defaultOutputFormat,
      })
      const url = URL.createObjectURL(download.blob)
      const link = document.createElement('a')
      link.href = url
      link.download = download.fileName
      link.click()
      URL.revokeObjectURL(url)
      return `${download.fileName} downloaded.`
    },
    onSuccess: async (message) => {
      await refresh()
      toast({ title: 'Report generated', description: message })
    },
    onError: (error: Error) => toast({ title: 'Generation failed', description: error.message, variant: 'destructive' }),
  })

  const openCreate = () => {
    setEditing(null)
    setForm(emptyForm())
    setDialogOpen(true)
  }
  const openEdit = (template: ReportTemplate) => {
    setEditing(template)
    setForm(toForm(template))
    setDialogOpen(true)
  }
  const toggleFormat = (format: OutputFormat) => {
    setForm((current) => {
      const selected = current.outputFormats.includes(format)
        ? current.outputFormats.filter((item) => item !== format)
        : [...current.outputFormats, format]
      return {
        ...current,
        outputFormats: selected,
        defaultOutputFormat: selected.includes(current.defaultOutputFormat)
          ? current.defaultOutputFormat
          : selected[0] ?? 'Online',
      }
    })
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <Input className="min-w-56 flex-1" placeholder="Search templates" value={search} onChange={(event) => setSearch(event.target.value)} />
        <FilterSelect value={audience} onChange={setAudience} all="All audiences" values={audiences} />
        <FilterSelect value={cadence} onChange={setCadence} all="All cadences" values={cadences} />
        <FilterSelect value={status} onChange={setStatus} all="All statuses" values={['Draft', 'Published', 'Archived']} />
        <Button onClick={openCreate}><Plus className="mr-2 h-4 w-4" />New template</Button>
      </div>

      {templateQuery.isLoading ? (
        <div className="py-10 text-center text-sm text-muted-foreground">Loading templates…</div>
      ) : templates.length === 0 ? (
        <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">No templates match the selected criteria.</CardContent></Card>
      ) : (
        <div className="grid gap-3 xl:grid-cols-2">
          {templates.map((template) => (
            <Card key={template.id}>
              <CardContent className="flex flex-col gap-3 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <FileText className="h-4 w-4 text-primary" />
                      <h3 className="truncate font-semibold">{template.name}</h3>
                      <Badge variant={template.status === 'Published' ? 'default' : 'secondary'}>{template.status}</Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {template.templateKey} · v{template.version} · {template.reportName ?? 'Unlinked report'}
                    </p>
                  </div>
                  <div className="flex flex-wrap justify-end gap-1">
                    {template.status === 'Draft' && <IconButton label="Edit" onClick={() => openEdit(template)} icon={<Pencil />} />}
                    {template.status === 'Draft' && <IconButton label="Publish" onClick={() => actionMutation.mutate({ template, action: 'publish' })} icon={<Send />} />}
                    {template.status === 'Draft' && <IconButton label="Delete" onClick={() => actionMutation.mutate({ template, action: 'delete' })} icon={<Trash2 />} danger />}
                    {template.status !== 'Draft' && <IconButton label="Clone revision" onClick={() => actionMutation.mutate({ template, action: 'clone' })} icon={<Copy />} />}
                    {template.status === 'Published' && <IconButton label="Archive" onClick={() => actionMutation.mutate({ template, action: 'archive' })} icon={<Archive />} />}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2 text-xs">
                  <Badge variant="outline">{template.audience}</Badge>
                  <Badge variant="outline">{template.cadence}</Badge>
                  {template.outputFormats.map((format) => <Badge key={format} variant="outline">{format}</Badge>)}
                </div>
                <div className="flex items-center justify-between gap-3 border-t pt-3">
                  <span className="text-xs text-muted-foreground">
                    {template.lastGeneratedAt ? `Last generated ${new Date(template.lastGeneratedAt).toLocaleString()}` : 'Not generated yet'}
                  </span>
                  {template.status === 'Published' && (
                    <Button size="sm" onClick={() => generateMutation.mutate(template)} disabled={generateMutation.isPending}>
                      {template.defaultOutputFormat === 'Online' ? <Play className="mr-2 h-4 w-4" /> : <Download className="mr-2 h-4 w-4" />}
                      Generate {template.defaultOutputFormat}
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader><DialogTitle>{editing ? 'Edit draft report template' : 'New report template'}</DialogTitle></DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Template name"><Input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></Field>
            <Field label="Template key"><Input disabled={Boolean(editing)} placeholder="BOARD-QUARTERLY-SPEND" value={form.templateKey} onChange={(event) => setForm({ ...form, templateKey: event.target.value.toUpperCase() })} /></Field>
            <Field label="Report definition">
              <Select disabled={Boolean(editing)} value={form.reportId} onValueChange={(reportId) => setForm({ ...form, reportId })}>
                <SelectTrigger><SelectValue placeholder="Select a published report" /></SelectTrigger>
                <SelectContent>{(reportQuery.data ?? []).map((report) => <SelectItem key={report.id} value={report.id}>{report.name}</SelectItem>)}</SelectContent>
              </Select>
            </Field>
            <Field label="Category"><Input value={form.category} onChange={(event) => setForm({ ...form, category: event.target.value })} /></Field>
            <Field label="Audience"><ValueSelect value={form.audience} onChange={(value) => setForm({ ...form, audience: value as FormState['audience'] })} values={audiences} /></Field>
            <Field label="Cadence"><ValueSelect value={form.cadence} onChange={(value) => setForm({ ...form, cadence: value as FormState['cadence'] })} values={cadences} /></Field>
            <Field label="Default output"><ValueSelect value={form.defaultOutputFormat} onChange={(value) => setForm({ ...form, defaultOutputFormat: value as OutputFormat })} values={form.outputFormats} /></Field>
            <Field label="Enabled outputs">
              <div className="flex h-10 items-center gap-4 rounded-md border px-3">
                {formats.map((format) => <label key={format} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.outputFormats.includes(format)} onChange={() => toggleFormat(format)} />{format}</label>)}
              </div>
            </Field>
            <Field label="Description" wide><Textarea rows={2} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></Field>
            <Field label="Saved filters (JSON)" wide><Textarea className="font-mono text-xs" rows={6} value={form.savedFiltersJson} onChange={(event) => setForm({ ...form, savedFiltersJson: event.target.value })} /></Field>
            <Field label="Generation metadata (JSON)" wide><Textarea className="font-mono text-xs" rows={6} value={form.generationMetadataJson} onChange={(event) => setForm({ ...form, generationMetadataJson: event.target.value })} /></Field>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button>
            <Button disabled={saveMutation.isPending || !form.reportId || form.outputFormats.length === 0} onClick={() => saveMutation.mutate()}>{saveMutation.isPending ? 'Saving…' : 'Save draft'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(onlineResult)} onOpenChange={(open) => { if (!open) setOnlineResult(null) }}>
        <DialogContent className="max-h-[90vh] max-w-6xl overflow-hidden">
          <DialogHeader><DialogTitle>{onlineTemplate?.name ?? 'Template report result'}</DialogTitle></DialogHeader>
          <div className="max-h-[70vh] overflow-auto rounded-md border">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-muted">
                <tr>{onlineResult?.columns.filter((column) => column.isVisible).map((column) => <th key={column.name} className="whitespace-nowrap px-3 py-2 text-left font-medium">{column.displayName ?? column.name}</th>)}</tr>
              </thead>
              <tbody>
                {onlineResult?.data.map((row, rowIndex) => (
                  <tr key={rowIndex} className="border-t">
                    {onlineResult.columns.filter((column) => column.isVisible).map((column) => <td key={column.name} className="whitespace-nowrap px-3 py-2">{String(row[column.name] ?? '')}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <DialogFooter><span className="mr-auto text-xs text-muted-foreground">{onlineResult?.totalRows ?? 0} row(s)</span><Button variant="outline" onClick={() => setOnlineResult(null)}>Close</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function Field({ label, wide, children }: { label: string; wide?: boolean; children: React.ReactNode }) {
  return <div className={wide ? 'space-y-1.5 md:col-span-2' : 'space-y-1.5'}><Label>{label}</Label>{children}</div>
}

function ValueSelect({ value, onChange, values }: { value: string; onChange: (value: string) => void; values: readonly string[] }) {
  return <Select value={value} onValueChange={onChange}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{values.map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select>
}

function FilterSelect({ value, onChange, all, values }: { value: string; onChange: (value: string) => void; all: string; values: readonly string[] }) {
  return <Select value={value} onValueChange={onChange}><SelectTrigger className="w-44"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{all}</SelectItem>{values.map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select>
}

function IconButton({ label, onClick, icon, danger }: { label: string; onClick: () => void; icon: React.ReactElement; danger?: boolean }) {
  return <Button type="button" size="icon" variant="ghost" className={danger ? 'text-destructive' : ''} title={label} aria-label={label} onClick={onClick}>{icon && <span className="[&>svg]:h-4 [&>svg]:w-4">{icon}</span>}</Button>
}
