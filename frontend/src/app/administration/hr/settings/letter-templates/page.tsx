'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Eye, FileText, Loader2, RotateCcw, Save, Search, Send, Undo2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { letterTemplateService } from '@/services/hr/letter-template.service';
import type { HrLetterTemplateSummary, HrLetterTemplateToken } from '@/types/hr/letter-templates';

const keyOf = (t: { module: string; eventKey: string }) => `${t.module}/${t.eventKey}`;

/** What a test send did, in words — "false" from a mail call says nothing. */
const OUTCOME: Record<string, string> = {
  Sent: 'Sent — check your inbox.',
  NoMailServer: 'No mail server is configured, so nothing could be sent. Set one up under email settings.',
  NoAddress: 'Your record has no email address to send to.',
  Failed: 'The mail server refused it.',
  TimedOut: 'The mail server did not answer in time.',
};

/**
 * HR letter & email templates (round 4, lane N): every email the HR modules send and every document
 * they print — recruitment, orientation and onboarding, probation, staff assets, HR letters, the
 * company schedule, interview and test papers — listed from the modules' own catalogues, and reworded
 * per tenant.
 *
 * The ported solution had this screen and the port did not. Saving is refused, with every reason, for
 * a broken {{#if}}, a token the email does not supply, or {{{Token}}} (unescaped) on text a person
 * typed; the preview shows those problems as you type. Reset brings the shipped wording back.
 */
export default function LetterTemplatesPage() {
  const searchParams = useSearchParams();
  const [search, setSearch] = useState('');
  // ?t=Module/EventKey opens one email directly — for links from the screens that send it.
  const [selected, setSelected] = useState<string | null>(searchParams?.get('t') || null);

  const { data: templates = [], isLoading } = useQuery({
    queryKey: ['hr', 'letter-templates'],
    queryFn: () => letterTemplateService.list(),
  });

  const groups = useMemo(() => {
    const term = search.trim().toLowerCase();
    const shown = term
      ? templates.filter((t) =>
          [t.name, t.description, t.category, t.eventKey].some((v) => v.toLowerCase().includes(term)))
      : templates;
    const byCategory = new Map<string, HrLetterTemplateSummary[]>();
    for (const t of shown) byCategory.set(t.category, [...(byCategory.get(t.category) ?? []), t]);
    return Array.from(byCategory.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [templates, search]);

  const edited = templates.filter((t) => t.state === 'Edited').length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Letter & Email Templates"
        description="Every email and printed document the HR modules produce. Reword any of them for this organisation — the shipped wording is kept, and Reset brings it back."
        backHref="/administration/hr/settings"
      />

      <Alert>
        <FileText className="h-4 w-4" />
        <AlertTitle>
          {templates.length} templates{edited > 0 ? ` · ${edited} reworded` : ''}
        </AlertTitle>
        <AlertDescription>
          The emails HR sends — offers, interview invitations, onboarding and orientation notices,
          reminders — and the documents it prints: HR letters, interview papers, test papers. In-app
          notifications, and the platform&apos;s own email designer, are separate and are not changed
          here.
        </AlertDescription>
      </Alert>

      <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
        <Card className="h-fit">
          <CardContent className="space-y-3 p-3">
            <div className="relative">
              <Search className="text-muted-foreground absolute left-2.5 top-2.5 h-4 w-4" />
              <Input
                placeholder="Search emails…"
                className="pl-8"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            {isLoading ? (
              <p className="text-muted-foreground p-2 text-sm">Loading…</p>
            ) : groups.length === 0 ? (
              <p className="text-muted-foreground p-2 text-sm">No email matches.</p>
            ) : (
              <div className="max-h-[70vh] space-y-4 overflow-y-auto pr-1">
                {groups.map(([category, items]) => (
                  <div key={category}>
                    <div className="text-muted-foreground mb-1 px-1 text-xs font-medium uppercase tracking-wide">
                      {category}
                    </div>
                    <ul className="space-y-0.5">
                      {items.map((t) => (
                        <li key={keyOf(t)}>
                          <button
                            type="button"
                            onClick={() => setSelected(keyOf(t))}
                            className={`hover:bg-muted w-full rounded-md px-2 py-1.5 text-left text-sm ${
                              selected === keyOf(t) ? 'bg-muted font-medium' : ''
                            }`}
                          >
                            <span className="flex items-center justify-between gap-2">
                              <span className="truncate">{t.name}</span>
                              {t.state === 'Edited' && (
                                <Badge variant="secondary" className="shrink-0 text-[10px]">
                                  Edited
                                </Badge>
                              )}
                            </span>
                          </button>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {selected ? (
          <TemplateEditor key={selected} templateKey={selected} />
        ) : (
          <Card>
            <CardContent className="p-0">
              <EmptyState
                icon={FileText}
                title="Choose an email"
                description="Pick one on the left to see its wording, the details it can include, and a preview."
              />
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}

function TemplateEditor({ templateKey }: { templateKey: string }) {
  const [module, eventKey] = templateKey.split('/');
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [loaded, setLoaded] = useState(false);
  const [debounced, setDebounced] = useState({ subject: '', body: '' });
  const [confirmReset, setConfirmReset] = useState(false);
  const lastField = useRef<'subject' | 'body'>('body');
  const subjectRef = useRef<HTMLInputElement>(null);
  const bodyRef = useRef<HTMLTextAreaElement>(null);

  const { data: template, isLoading } = useQuery({
    queryKey: ['hr', 'letter-templates', templateKey],
    queryFn: () => letterTemplateService.get(module, eventKey),
  });

  useEffect(() => {
    if (template && !loaded) {
      setSubject(template.subject);
      setBody(template.htmlBody);
      setDebounced({ subject: template.subject, body: template.htmlBody });
      setLoaded(true);
    }
  }, [template, loaded]);

  // The preview follows the editor half a second behind, so typing is not a request per key.
  useEffect(() => {
    const handle = setTimeout(() => setDebounced({ subject, body }), 500);
    return () => clearTimeout(handle);
  }, [subject, body]);

  const { data: preview, isFetching: previewing } = useQuery({
    queryKey: ['hr', 'letter-templates', templateKey, 'preview', debounced.subject, debounced.body],
    queryFn: () => letterTemplateService.preview(module, eventKey, debounced.subject, debounced.body),
    enabled: loaded,
    placeholderData: (previous) => previous,
  });

  const dirty = !!template && (subject !== template.subject || body !== template.htmlBody);
  const problems = preview?.problems ?? [];

  const refresh = (next: { subject: string; htmlBody: string }) => {
    setSubject(next.subject);
    setBody(next.htmlBody);
    queryClient.invalidateQueries({ queryKey: ['hr', 'letter-templates'] });
  };

  const save = useMutation({
    mutationFn: () => letterTemplateService.save(module, eventKey, subject, body),
    onSuccess: (saved) => {
      queryClient.setQueryData(['hr', 'letter-templates', templateKey], saved);
      refresh(saved);
      toast({ title: 'Saved', description: `${saved.name} now goes out in your wording.` });
    },
    onError: (error: any) => toast({ title: 'Not saved', description: error?.message, variant: 'destructive' }),
  });

  const reset = useMutation({
    mutationFn: () => letterTemplateService.reset(module, eventKey),
    onSuccess: (fresh) => {
      queryClient.setQueryData(['hr', 'letter-templates', templateKey], fresh);
      refresh(fresh);
      toast({ title: 'Reset', description: `${fresh.name} goes out in the shipped wording again.` });
    },
    onError: (error: any) => toast({ title: 'Not reset', description: error?.message, variant: 'destructive' }),
  });

  const testSend = useMutation({
    mutationFn: () => letterTemplateService.testSend(module, eventKey, subject, body),
    onSuccess: (result) =>
      toast({
        title: result.outcome === 'Sent' ? `Test sent to ${result.sentTo}` : 'Test not sent',
        description: OUTCOME[result.outcome] ?? result.outcome,
        variant: result.outcome === 'Sent' ? undefined : 'destructive',
      }),
    onError: (error: any) => toast({ title: 'Test not sent', description: error?.message, variant: 'destructive' }),
  });

  /** Puts a token where the cursor was, in whichever field was last in use. */
  const insert = (token: HrLetterTemplateToken) => {
    const text = token.mayBeRaw ? `{{{${token.token}}}}` : `{{${token.token}}}`;
    if (lastField.current === 'subject') {
      const el = subjectRef.current;
      const at = el?.selectionStart ?? subject.length;
      setSubject(subject.slice(0, at) + text + subject.slice(el?.selectionEnd ?? at));
    } else {
      const el = bodyRef.current;
      const at = el?.selectionStart ?? body.length;
      setBody(body.slice(0, at) + text + body.slice(el?.selectionEnd ?? at));
    }
  };

  if (isLoading || !template) {
    return (
      <Card>
        <CardContent className="flex items-center justify-center p-10">
          <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="space-y-1">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <CardTitle className="text-base">{template.name}</CardTitle>
            <Badge variant={template.state === 'Edited' ? 'secondary' : 'outline'}>
              {template.state === 'Edited'
                ? template.matchesDefault
                  ? 'Edited — same as shipped'
                  : 'Edited'
                : 'Shipped wording'}
            </Badge>
          </div>
          <p className="text-muted-foreground text-sm">{template.description}</p>
          {template.state === 'Edited' && (
            <p className="text-muted-foreground text-xs">
              Reworded{template.editedBy ? ` by ${template.editedBy}` : ''}
              {template.editedAt ? ` on ${new Date(template.editedAt).toLocaleString()}` : ''}
            </p>
          )}
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="tmpl-subject">Subject</Label>
            <Input
              id="tmpl-subject"
              ref={subjectRef}
              value={subject}
              maxLength={200}
              onFocus={() => (lastField.current = 'subject')}
              onChange={(e) => setSubject(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="tmpl-body">Body (HTML)</Label>
            <Textarea
              id="tmpl-body"
              ref={bodyRef}
              rows={16}
              className="font-mono text-xs"
              value={body}
              onFocus={() => (lastField.current = 'body')}
              onChange={(e) => setBody(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label>Details this email can include — click to insert</Label>
            <div className="flex flex-wrap gap-1.5">
              {[...template.tokens, ...template.commonTokens].map((t) => (
                <button
                  key={t.token}
                  type="button"
                  title={`${t.description}${t.sampleValue ? ` — e.g. ${t.sampleValue}` : ''}`}
                  onClick={() => insert(t)}
                  className="hover:bg-muted rounded border px-2 py-0.5 font-mono text-xs"
                >
                  {t.mayBeRaw ? `{{{${t.token}}}}` : `{{${t.token}}}`}
                </button>
              ))}
            </div>
            <p className="text-muted-foreground text-xs">
              Wrap a sentence in <code>{'{{#if Token}}'}</code> … <code>{'{{/if}}'}</code> to leave it out
              when that detail is empty.
            </p>
          </div>

          {problems.length > 0 && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>This wording cannot be saved yet</AlertTitle>
              <AlertDescription>
                <ul className="list-disc space-y-1 pl-4">
                  {problems.map((p) => (
                    <li key={p}>{p}</li>
                  ))}
                </ul>
              </AlertDescription>
            </Alert>
          )}

          <div className="flex flex-wrap gap-2">
            <Button onClick={() => save.mutate()} disabled={!dirty || problems.length > 0 || save.isPending}>
              {save.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
              Save
            </Button>
            <Button
              variant="outline"
              onClick={() => { setSubject(template.subject); setBody(template.htmlBody); }}
              disabled={!dirty}
            >
              <Undo2 className="mr-2 h-4 w-4" />
              Undo changes
            </Button>
            <Button
              variant="outline"
              onClick={() => { setSubject(template.defaultSubject); setBody(template.defaultHtmlBody); }}
            >
              Start from the shipped wording
            </Button>
            <Button variant="outline" onClick={() => testSend.mutate()} disabled={problems.length > 0 || testSend.isPending}>
              {testSend.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
              Send a test to me
            </Button>
            {template.state === 'Edited' && (
              <Button variant="outline" className="text-red-600" onClick={() => setConfirmReset(true)}>
                <RotateCcw className="mr-2 h-4 w-4" />
                Reset to shipped
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Eye className="h-4 w-4" />
            Preview, with sample details
            {previewing && <Loader2 className="text-muted-foreground h-4 w-4 animate-spin" />}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <div className="text-sm">
            <span className="text-muted-foreground">Subject: </span>
            <span className="font-medium">{preview?.subject ?? ''}</span>
          </div>
          {/* Sandboxed: an email body is somebody's HTML, and the preview must not run anything. */}
          <iframe
            title="Email preview"
            sandbox=""
            srcDoc={preview?.htmlBody ?? ''}
            className="h-[520px] w-full rounded-md border bg-white"
          />
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={confirmReset}
        onOpenChange={setConfirmReset}
        title={`Reset “${template.name}”?`}
        description="Your wording is set aside and the shipped wording goes out again. You can reword it again at any time."
        confirmText="Reset to shipped"
        variant="destructive"
        isLoading={reset.isPending}
        onConfirm={async () => {
          // onError has already said why; keep the dialog open rather than throw out of it.
          try {
            await reset.mutateAsync();
            return true;
          } catch {
            return false;
          }
        }}
      />
    </div>
  );
}
