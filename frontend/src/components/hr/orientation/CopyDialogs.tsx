'use client';

import { useState, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { toIsoInstant } from '@/components/hr/employee/tabs/fields';
import { onboardingPlanTemplateService } from '@/services/hr/onboarding.service';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import { orientationSessionService } from '@/services/hr/orientation-session.service';

/**
 * Round 4, lane J4 — the three "make another one like this" dialogs: copy an onboarding template,
 * copy a programme, run a session again. Each is opened from its list AND from the record's own
 * page: the sessions list shows only upcoming, open and published runs, so a finished session —
 * the one most often run again — is reachable from its own page only.
 *
 * Each dialog is open while `source` is set, lands on the copy's page when it succeeds, and keeps
 * the server's refusal (a name or code already taken, a programme no longer active) inside the
 * dialog so it can be corrected there.
 *
 * ⚠ The fields live in an inner form mounted once per opening (keyed on the source id), seeded by
 * `useState` initialisers. A detail page passes a fresh `source` object on every render, so
 * re-seeding from an effect on `source` would wipe what was typed whenever a query refetched.
 */

interface CopyDialogShellProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description: ReactNode;
  submitLabel: string;
  busy: boolean;
  error: string | null;
  onSubmit: () => void;
  children: ReactNode;
}

function CopyDialogShell({
  open,
  onClose,
  title,
  description,
  submitLabel,
  busy,
  error,
  onSubmit,
  children,
}: CopyDialogShellProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && !busy && onClose()}>
      <DialogContent className="sm:max-w-[520px]">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            onSubmit();
          }}
          className="space-y-4"
        >
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription asChild>
              <div className="space-y-2 text-sm">{description}</div>
            </DialogDescription>
          </DialogHeader>
          {children}
          {error && <p className="text-destructive text-sm">{error}</p>}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={busy}>
              Cancel
            </Button>
            <Button type="submit" disabled={busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

const messageOf = (error: unknown, fallback: string) =>
  (error as { message?: string } | null)?.message || fallback;

// ── Onboarding template ───────────────────────────────────────────────────────

export interface TemplateCopySource {
  id: string;
  name: string;
}

export function CopyOnboardingTemplateDialog({
  source,
  onClose,
}: {
  source: TemplateCopySource | null;
  onClose: () => void;
}) {
  return source ? <CopyTemplateForm key={source.id} source={source} onClose={onClose} /> : null;
}

function CopyTemplateForm({ source, onClose }: { source: TemplateCopySource; onClose: () => void }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [name, setName] = useState(() => `${source.name} (copy)`);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!name.trim()) {
      setError('Give the copy a name.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const copy = await onboardingPlanTemplateService.clone(source.id, { newName: name.trim() });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-templates'] });
      toast({
        title: 'Template copied',
        description: `${copy.taskTemplates.length} task${copy.taskTemplates.length === 1 ? '' : 's'} copied into "${copy.name}".`,
      });
      onClose();
      router.push(`/administration/hr/orientation/onboarding-templates/${copy.id}`);
    } catch (e) {
      setError(messageOf(e, 'The template could not be copied.'));
      setBusy(false);
    }
  };

  return (
    <CopyDialogShell
      open
      onClose={onClose}
      title="Copy template"
      description={
        <>
          <p>The copy gets every task, with the same timing and owners.</p>
          <p>
            It is not the default, and it is not given the original&apos;s audience — so no new hire
            receives it until you choose it by hand or give it an audience of its own.
          </p>
        </>
      }
      submitLabel="Copy template"
      busy={busy}
      error={error}
      onSubmit={submit}
    >
      <div className="space-y-2">
        <Label htmlFor="copy-template-name">Name of the copy</Label>
        <Input
          id="copy-template-name"
          value={name}
          maxLength={200}
          onChange={(e) => setName(e.target.value)}
          autoFocus
        />
      </div>
    </CopyDialogShell>
  );
}

// ── Orientation programme ─────────────────────────────────────────────────────

export interface ProgramCopySource {
  id: string;
  title: string;
  programCode: string;
}

export function CopyOrientationProgramDialog({
  source,
  onClose,
}: {
  source: ProgramCopySource | null;
  onClose: () => void;
}) {
  return source ? <CopyProgramForm key={source.id} source={source} onClose={onClose} /> : null;
}

function CopyProgramForm({ source, onClose }: { source: ProgramCopySource; onClose: () => void }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [title, setTitle] = useState(() => `${source.title} (copy)`);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!title.trim()) {
      setError('Give the copy a title.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const copy = await orientationProgramService.clone(source.id, {
        newName: title.trim(),
        newCode: code.trim() || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-programs'] });
      toast({
        title: 'Programme copied',
        description: `${copy.programCode} is a draft. Publish it when it is ready.`,
      });
      onClose();
      router.push(`/administration/hr/orientation/programs/${copy.id}`);
    } catch (e) {
      setError(messageOf(e, 'The programme could not be copied.'));
      setBusy(false);
    }
  };

  return (
    <CopyDialogShell
      open
      onClose={onClose}
      title={`Copy ${source.programCode}`}
      description={
        <>
          <p>
            The copy gets the modules and their content, the quiz, the prerequisites and the rules
            for who takes it. Sessions and enrolments stay with the original.
          </p>
          <p>
            It starts as a draft, so its rules enrol nobody yet. Once you publish it they enrol people
            just as the original&apos;s do — if the copy replaces the original, retire the original
            at the same time, or both will enrol the same people.
          </p>
        </>
      }
      submitLabel="Copy programme"
      busy={busy}
      error={error}
      onSubmit={submit}
    >
      <div className="space-y-2">
        <Label htmlFor="copy-program-title">Title of the copy</Label>
        <Input
          id="copy-program-title"
          value={title}
          maxLength={300}
          onChange={(e) => setTitle(e.target.value)}
          autoFocus
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="copy-program-code">Programme code</Label>
        <Input
          id="copy-program-code"
          value={code}
          maxLength={50}
          placeholder="Leave blank to number it automatically"
          onChange={(e) => setCode(e.target.value)}
        />
      </div>
    </CopyDialogShell>
  );
}

// ── Orientation session ───────────────────────────────────────────────────────

export interface SessionCopySource {
  id: string;
  title: string;
  scheduledStartAt?: string | null;
  /** Absent from the list's summary rows; the hint then cannot state the length, but it is still kept. */
  scheduledEndAt?: string | null;
}

/** "2 h 30 min", "45 min" — the original's length, for the hint under the end field. */
function lengthOf(start?: string | null, end?: string | null): string | null {
  if (!start || !end) return null;
  const minutes = Math.round((new Date(end).getTime() - new Date(start).getTime()) / 60000);
  if (!Number.isFinite(minutes) || minutes <= 0) return null;
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const mins = minutes % 60;
  return [days && `${days} d`, hours && `${hours} h`, mins && `${mins} min`].filter(Boolean).join(' ');
}

export function RunSessionAgainDialog({
  source,
  onClose,
}: {
  source: SessionCopySource | null;
  onClose: () => void;
}) {
  return source ? <RunAgainForm key={source.id} source={source} onClose={onClose} /> : null;
}

function RunAgainForm({ source, onClose }: { source: SessionCopySource; onClose: () => void }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [title, setTitle] = useState(() => source.title);
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const originalLength = lengthOf(source.scheduledStartAt, source.scheduledEndAt);

  const submit = async () => {
    const startIso = toIsoInstant(start);
    if (!startIso) {
      setError('Choose when the new run starts.');
      return;
    }
    const endIso = toIsoInstant(end);
    if (endIso && new Date(endIso) <= new Date(startIso)) {
      setError('The new run must end after it starts.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const copy = await orientationSessionService.clone(source.id, {
        scheduledStartAt: startIso,
        scheduledEndAt: endIso,
        title: title.trim() || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-sessions'] });
      toast({
        title: 'Session scheduled',
        description: `${copy.sessionCode} is a draft. Publish it once the facilitators confirm.`,
      });
      onClose();
      router.push(`/hr/orientation/sessions/${copy.id}`);
    } catch (e) {
      setError(messageOf(e, 'The session could not be scheduled.'));
      setBusy(false);
    }
  };

  return (
    <CopyDialogShell
      open
      onClose={onClose}
      title="Run this session again"
      description={
        <>
          <p>
            Same programme, venue, joining link, capacity and facilitators. The new run starts as a
            draft with nobody enrolled, and the facilitators are asked to confirm the new date.
          </p>
          <p>The enrolment deadline moves with the date, keeping the same notice.</p>
        </>
      }
      submitLabel="Schedule the new run"
      busy={busy}
      error={error}
      onSubmit={submit}
    >
      <div className="space-y-2">
        <Label htmlFor="rerun-title">Title</Label>
        <Input
          id="rerun-title"
          value={title}
          maxLength={300}
          onChange={(e) => setTitle(e.target.value)}
        />
        <p className="text-muted-foreground text-xs">
          Change it if it names the old date or month.
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor="rerun-start">
            Starts <span className="text-destructive">*</span>
          </Label>
          <Input
            id="rerun-start"
            type="datetime-local"
            value={start}
            onChange={(e) => setStart(e.target.value)}
            autoFocus
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="rerun-end">Ends</Label>
          <Input
            id="rerun-end"
            type="datetime-local"
            value={end}
            onChange={(e) => setEnd(e.target.value)}
          />
          <p className="text-muted-foreground text-xs">
            {originalLength
              ? `Leave blank to keep the same length (${originalLength}).`
              : "Leave blank to keep the original's length."}
          </p>
        </div>
      </div>
    </CopyDialogShell>
  );
}
