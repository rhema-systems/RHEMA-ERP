'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  Loader2,
  Megaphone,
  Pencil,
  Pin,
  Plus,
  Send,
  Trash2,
  Upload,
  Users,
  X,
} from 'lucide-react';
import { apiService } from '@/services/api.service';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { useToast } from '@/hooks/use-toast';
import {
  hrAnnouncementsService,
  ANNOUNCEMENT_CATEGORIES,
  AUDIENCE_TARGETS,
  type HrAnnouncement,
  type HrAnnouncementCategory,
  type HrAnnouncementStatus,
  type HrAudienceTargetType,
  type SaveHrAnnouncementRequest,
} from '@/services/hr/announcements.service';
import { cn } from '@/lib/utils';

/**
 * Staff announcements — the desk that writes and publishes them (area 25 slice 12c).
 *
 * The screen's job beyond CRUD is to stop somebody broadcasting blind: the audience builder
 * shows the LIVE reach of the rules as they are edited ("this will go to 412 people"), because
 * discovering that a notice went to four people, or to eight thousand, after the fact is the
 * failure this feature is most prone to.
 *
 * ⚠ The employee axis is not offered here. It exists in the API for exceptions, but choosing a
 * person needs an employee search gated on HR.Employee, which a HR.Company user need not hold.
 *
 * Lives at the HR top level rather than under administration settings: announcements are
 * operational — written and published continuously — whereas `/administration/hr/settings`
 * holds the tenant configuration that is set once and rarely touched.
 */

const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

const STATUS_STYLE: Record<HrAnnouncementStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  Published: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  Archived: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
};

interface AudienceRow {
  targetType: HrAudienceTargetType;
  targetId?: string | null;
  isExclusion: boolean;
}

const EMPTY_FORM: SaveHrAnnouncementRequest = {
  title: '',
  summary: '',
  body: '',
  category: 'General',
  isPinned: false,
  effectiveFrom: null,
  expiresOn: null,
  audiences: [],
};

/** The lookups the audience builder picks from. One fetch each, shared by every row. */
function useAudienceLookups() {
  const list = async <T,>(path: string): Promise<T[]> => {
    const r = await apiService.get<any>(path);
    return (r?.items ?? r ?? []) as T[];
  };
  const units = useQuery({
    queryKey: ['lookup', 'organization-units'],
    queryFn: () => list<{ id: string; name: string }>('/OrganizationUnit'),
    staleTime: 300_000,
  });
  const levels = useQuery({
    queryKey: ['lookup', 'organization-levels'],
    queryFn: () => list<{ id: string; name: string }>('/OrganizationLevel'),
    staleTime: 300_000,
  });
  const positions = useQuery({
    queryKey: ['lookup', 'employee-positions'],
    queryFn: () => list<{ id: string; title: string }>('/EmployeePositions'),
    staleTime: 300_000,
  });
  const locations = useQuery({
    queryKey: ['lookup', 'locations'],
    queryFn: () => list<{ id: string; name: string }>('/Location'),
    staleTime: 300_000,
  });

  return useMemo(() => ({
    OrganizationUnit: (units.data ?? []).map((u) => ({ id: u.id, label: u.name })),
    OrganizationLevel: (levels.data ?? []).map((l) => ({ id: l.id, label: l.name })),
    Position: (positions.data ?? []).map((p) => ({ id: p.id, label: p.title })),
    Location: (locations.data ?? []).map((l) => ({ id: l.id, label: l.name })),
    AllEmployees: [] as { id: string; label: string }[],
    Employee: [] as { id: string; label: string }[],
  }), [units.data, levels.data, positions.data, locations.data]);
}

export default function HrAnnouncementsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const lookups = useAudienceLookups();

  const [tab, setTab] = useState<HrAnnouncementStatus | 'all'>('Draft');
  const [editing, setEditing] = useState<HrAnnouncement | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<SaveHrAnnouncementRequest>(EMPTY_FORM);
  const [uploading, setUploading] = useState<HrAnnouncement | null>(null);

  const { data: announcements = [], isLoading, isError } = useQuery({
    queryKey: ['hr', 'announcements', tab],
    queryFn: () => hrAnnouncementsService.getAll(tab === 'all' ? undefined : tab),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'announcements'] });

  // The live reach, recomputed as the rules change — the whole point of the builder.
  const { data: reach, isFetching: reachLoading } = useQuery({
    queryKey: ['hr', 'announcements', 'reach', form.audiences],
    queryFn: () => hrAnnouncementsService.previewAudience(form.audiences),
    enabled: creating || editing !== null,
  });

  const open = (announcement: HrAnnouncement | null) => {
    if (announcement) {
      setEditing(announcement);
      setForm({
        title: announcement.title,
        summary: announcement.summary ?? '',
        body: announcement.body,
        category: announcement.category,
        isPinned: announcement.isPinned,
        effectiveFrom: announcement.effectiveFrom ?? null,
        expiresOn: announcement.expiresOn ?? null,
        audiences: announcement.audiences.map((a) => ({
          targetType: a.targetType,
          targetId: a.targetId ?? null,
          isExclusion: a.isExclusion,
        })),
      });
    } else {
      setCreating(true);
      setForm(EMPTY_FORM);
    }
  };

  const close = () => {
    setEditing(null);
    setCreating(false);
    setForm(EMPTY_FORM);
  };

  const save = useMutation({
    mutationFn: () =>
      editing
        ? hrAnnouncementsService.update(editing.id, form)
        : hrAnnouncementsService.create(form),
    onSuccess: () => {
      refresh();
      close();
      toast({ title: 'Saved', description: 'The draft is ready to publish when you are.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Not saved',
        description: e?.message || 'The announcement could not be saved.',
        variant: 'destructive',
      }),
  });

  const publish = useMutation({
    mutationFn: (id: string) => hrAnnouncementsService.publish(id),
    onSuccess: (a) => {
      refresh();
      toast({
        title: 'Published',
        description: `"${a.title}" is now visible to ${a.audienceCount ?? 'its'} recipient(s).`,
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Not published',
        description: e?.message || 'The announcement could not be published.',
        variant: 'destructive',
      }),
  });

  const archive = useMutation({
    mutationFn: (id: string) => hrAnnouncementsService.archive(id),
    onSuccess: () => {
      refresh();
      toast({ title: 'Archived', description: 'It no longer appears for employees.' });
    },
    onError: (e: any) =>
      toast({ title: 'Not archived', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => hrAnnouncementsService.deleteDraft(id),
    onSuccess: () => {
      refresh();
      toast({ title: 'Draft deleted' });
    },
    onError: (e: any) =>
      toast({ title: 'Not deleted', description: e?.message, variant: 'destructive' }),
  });

  const setAudience = (index: number, patch: Partial<AudienceRow>) =>
    setForm((f) => ({
      ...f,
      audiences: f.audiences.map((a, i) => (i === index ? { ...a, ...patch } : a)),
    }));

  const formOpen = creating || editing !== null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Staff Announcements"
        description="Notices published to staff. Drafts are invisible until published; a published notice is archived rather than edited, so what people were told stays findable."
        backHref="/hr"
        actions={
          <Button onClick={() => open(null)}>
            <Plus className="mr-1 h-4 w-4" /> New announcement
          </Button>
        }
      />

      <Tabs value={tab} onValueChange={(v) => setTab(v as HrAnnouncementStatus | 'all')}>
        <TabsList>
          <TabsTrigger value="Draft">Drafts</TabsTrigger>
          <TabsTrigger value="Published">Published</TabsTrigger>
          <TabsTrigger value="Archived">Archived</TabsTrigger>
          <TabsTrigger value="all">All</TabsTrigger>
        </TabsList>
      </Tabs>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Announcements could not be loaded right now. Try again in a moment.
        </p>
      ) : announcements.length === 0 ? (
        <EmptyState
          icon={Megaphone}
          title="Nothing here"
          description={
            tab === 'Draft'
              ? 'No drafts in progress. Start one when you have something to tell staff.'
              : 'No announcements with this status.'
          }
          action={tab === 'Draft' ? <Button onClick={() => open(null)}>New announcement</Button> : undefined}
        />
      ) : (
        <div className="space-y-3">
          {announcements.map((a) => (
            <Card key={a.id}>
              <CardContent className="space-y-3 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  {a.isPinned && <Pin className="h-3.5 w-3.5 text-primary" aria-label="Pinned" />}
                  <span className="font-medium">{a.title}</span>
                  <Badge variant="outline">{a.categoryName}</Badge>
                  <Badge className={cn('border-0', STATUS_STYLE[a.status])}>{a.statusName}</Badge>
                  {a.audienceCount !== null && a.audienceCount !== undefined && (
                    <Badge variant="secondary" className="gap-1">
                      <Users className="h-3 w-3" /> {a.audienceCount}
                    </Badge>
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    {a.publishedAt ? `published ${fmtDate(a.publishedAt)}` : 'not published'}
                    {a.expiresOn ? ` · until ${fmtDate(a.expiresOn)}` : ''}
                  </span>
                </div>

                <p className="line-clamp-2 whitespace-pre-line text-sm text-muted-foreground">
                  {a.summary || a.body}
                </p>

                <div className="flex flex-wrap items-center gap-2">
                  {a.audiences.map((rule, i) => (
                    <Badge
                      key={rule.id ?? i}
                      variant={rule.isExclusion ? 'destructive' : 'outline'}
                      className="font-normal"
                    >
                      {rule.isExclusion ? 'except ' : ''}
                      {rule.targetName ?? rule.targetType}
                    </Badge>
                  ))}
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  {a.status === 'Draft' && (
                    <>
                      <Button size="sm" onClick={() => publish.mutate(a.id)} disabled={publish.isPending}>
                        {publish.isPending ? (
                          <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Send className="mr-1 h-3.5 w-3.5" />
                        )}
                        Publish
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => open(a)}>
                        <Pencil className="mr-1 h-3.5 w-3.5" /> Edit
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setUploading(a)}>
                        <Upload className="mr-1 h-3.5 w-3.5" /> Attach
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => remove.mutate(a.id)}>
                        <Trash2 className="mr-1 h-3.5 w-3.5" /> Delete
                      </Button>
                    </>
                  )}
                  {a.status === 'Published' && (
                    <Button variant="outline" size="sm" onClick={() => archive.mutate(a.id)}>
                      <Archive className="mr-1 h-3.5 w-3.5" /> Archive
                    </Button>
                  )}
                  {a.hasAttachment && (
                    <span className="text-xs text-muted-foreground">📎 {a.fileName}</span>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* ── The editor ────────────────────────────────────────────────── */}
      <Dialog open={formOpen} onOpenChange={(o) => !o && close()}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit draft' : 'New announcement'}</DialogTitle>
            <DialogDescription>
              Drafts are invisible to staff. You choose who it goes to below, and the count
              updates as you go.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="title">Title</Label>
              <Input
                id="title"
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="category">Category</Label>
                <Select
                  value={form.category}
                  onValueChange={(v) => setForm({ ...form, category: v as HrAnnouncementCategory })}
                >
                  <SelectTrigger id="category">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ANNOUNCEMENT_CATEGORIES.map((c) => (
                      <SelectItem key={c.value} value={c.value}>
                        {c.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="expiresOn">Stops showing (optional)</Label>
                <Input
                  id="expiresOn"
                  type="date"
                  value={form.expiresOn ? String(form.expiresOn).slice(0, 10) : ''}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      expiresOn: e.target.value ? new Date(e.target.value).toISOString() : null,
                    })
                  }
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="summary">
                One-line summary <span className="font-normal text-muted-foreground">(shown on the dashboard)</span>
              </Label>
              <Input
                id="summary"
                value={form.summary ?? ''}
                onChange={(e) => setForm({ ...form, summary: e.target.value })}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="body">Announcement</Label>
              <Textarea
                id="body"
                rows={6}
                value={form.body}
                onChange={(e) => setForm({ ...form, body: e.target.value })}
                placeholder="Plain text. Line breaks are kept."
              />
            </div>

            <div className="flex items-center gap-2">
              <Checkbox
                id="isPinned"
                checked={form.isPinned}
                onCheckedChange={(c) => setForm({ ...form, isPinned: c === true })}
              />
              <Label htmlFor="isPinned" className="font-normal">
                Keep at the top of the list
              </Label>
            </div>

            {/* ── Audience builder ──────────────────────────────────── */}
            <div className="space-y-2 rounded-lg border p-3">
              <div className="flex items-center justify-between">
                <Label>Who gets this?</Label>
                <span className="text-sm">
                  {reachLoading ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <span
                      className={cn(
                        'font-medium',
                        (reach ?? 0) === 0 && 'text-destructive',
                      )}
                    >
                      {reach ?? 0} recipient{reach === 1 ? '' : 's'}
                    </span>
                  )}
                </span>
              </div>

              {form.audiences.length === 0 && (
                <p className="text-sm text-muted-foreground">
                  Nobody yet — add a rule below. An announcement addressed to nobody cannot be
                  published.
                </p>
              )}

              {form.audiences.map((rule, i) => {
                const spec = AUDIENCE_TARGETS.find((t) => t.value === rule.targetType);
                const options = lookups[rule.targetType] ?? [];
                return (
                  <div key={i} className="flex flex-wrap items-center gap-2">
                    <Select
                      value={rule.targetType}
                      onValueChange={(v) =>
                        setAudience(i, { targetType: v as HrAudienceTargetType, targetId: null })
                      }
                    >
                      <SelectTrigger className="w-44">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {AUDIENCE_TARGETS.filter((t) => t.value !== 'Employee').map((t) => (
                          <SelectItem key={t.value} value={t.value}>
                            {t.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>

                    {spec?.needsTarget && (
                      <Select
                        value={rule.targetId ?? ''}
                        onValueChange={(v) => setAudience(i, { targetId: v })}
                      >
                        <SelectTrigger className="w-56">
                          <SelectValue placeholder="Choose…" />
                        </SelectTrigger>
                        <SelectContent>
                          {options.map((o) => (
                            <SelectItem key={o.id} value={o.id}>
                              {o.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}

                    <label className="flex items-center gap-1.5 text-sm">
                      <Checkbox
                        checked={rule.isExclusion}
                        onCheckedChange={(c) => setAudience(i, { isExclusion: c === true })}
                      />
                      except
                    </label>

                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() =>
                        setForm((f) => ({ ...f, audiences: f.audiences.filter((_, j) => j !== i) }))
                      }
                    >
                      <X className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                );
              })}

              <Button
                variant="outline"
                size="sm"
                onClick={() =>
                  setForm((f) => ({
                    ...f,
                    audiences: [...f.audiences, { targetType: 'AllEmployees', targetId: null, isExclusion: false }],
                  }))
                }
              >
                <Plus className="mr-1 h-3.5 w-3.5" /> Add a rule
              </Button>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button
              onClick={() => save.mutate()}
              disabled={save.isPending || form.title.trim() === '' || form.body.trim() === ''}
            >
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Attachment ───────────────────────────────────────────────── */}
      <Dialog open={!!uploading} onOpenChange={(o) => !o && setUploading(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach a file</DialogTitle>
            <DialogDescription>
              The notice, form or circular staff are being pointed at.
            </DialogDescription>
          </DialogHeader>
          {uploading && (
            <DocumentUploadField
              label="Attachment"
              endpoint={hrAnnouncementsService.attachmentEndpoint(uploading.id)}
              onUploaded={() => {
                setUploading(null);
                refresh();
                toast({ title: 'Attached' });
              }}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
