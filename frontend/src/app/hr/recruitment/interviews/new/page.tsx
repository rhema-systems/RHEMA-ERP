'use client';

import { useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { CalendarPlus, Info, Loader2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { PanelAvailabilityPanel } from '@/components/hr/recruitment/PanelAvailabilityPanel';
import { PanelMemberPicker, type PanelSelection } from '@/components/hr/recruitment/PanelMemberPicker';
import { useToast } from '@/hooks/use-toast';
import { dateOffset, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService, interviewQuestionPresetService } from '@/services/hr/interviews.service';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { INTERVIEW_MODES, INTERVIEW_TYPES } from '@/types/hr/interviews';

const schema = z
  .object({
    jobVacancyId: z.string().min(1, 'Choose a vacancy'),
    round: z.coerce.number().int().min(1).max(20),
    type: z.string().min(1),
    mode: z.string().min(1),
    scheduledDate: z.string().min(1, 'A date is required'),
    startTime: z.string().min(1, 'A start time is required'),
    endTime: z.string().min(1, 'An end time is required'),
    locationOrLink: z.string().max(500).optional(),
    instructions: z.string().max(2000).optional(),
    questionPresetId: z.string().optional(),
    // Round 4, D3. Optional here and enforced by the SERVER, which is the only place that knows
    // whether a hard clash exists. A client-side `required` would have to duplicate the clash rules
    // and would be wrong the moment a source is added.
    panelClashOverrideReason: z.string().max(1000).optional(),
  })
  .refine((v) => v.endTime > v.startTime, {
    message: 'The session must end after it starts',
    path: ['endTime'],
  });

// `z.coerce.number()` takes `unknown` in and `number` out — the input/output split the other HR
// forms use (see WorkScheduleForm).
type FormValues = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

const NO_PRESET = '__none__';

/**
 * Scheduling an interview.
 *
 * ⚠ **An interview is opened from a vacancy**, never standalone — the candidates you can book are
 * that vacancy's applicants, and the server refuses an application belonging to any other vacancy.
 * Arriving with `?vacancyId=` preselects it.
 *
 * Applying a preset here does two things server-side in one call: it scaffolds a question plan per
 * section and draws the questions from the bank. Both are editable afterwards on the interview.
 */
export default function NewInterviewPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { toast } = useToast();
  // Revealed once the server refuses on a hard clash — see the create mutation's onError.
  const [clashRefused, setClashRefused] = useState(false);

  const preselectedVacancy = searchParams.get('vacancyId') ?? '';
  const [selectedApplications, setSelectedApplications] = useState<string[]>([]);
  const [panelEmployees, setPanelEmployees] = useState<PanelSelection[]>([]);
  const [panelExternals, setPanelExternals] = useState<PanelSelection[]>([]);

  const form = useForm<FormValues, any, FormOutput>({
    resolver: zodResolver(schema) as any,
    defaultValues: {
      jobVacancyId: preselectedVacancy,
      round: 1,
      type: 'Panel',
      mode: 'InPerson',
      scheduledDate: dateOffset(7),
      startTime: '09:00',
      endTime: '11:00',
      locationOrLink: '',
      instructions: '',
      questionPresetId: NO_PRESET,
    },
  });

  const vacancyId = form.watch('jobVacancyId');

  const vacancies = useQuery({
    queryKey: ['hr', 'vacancies', 'for-interview'],
    queryFn: () => jobVacancyService.getPaged(1, 100),
  });

  const applications = useQuery({
    queryKey: ['hr', 'applications', 'by-vacancy', vacancyId],
    queryFn: () => jobApplicationService.getByVacancy(vacancyId),
    enabled: !!vacancyId,
  });

  const presets = useQuery({
    queryKey: ['hr', 'interview-presets'],
    queryFn: () => interviewQuestionPresetService.getAll(),
  });
  const activePresets = (presets.data ?? []).filter((p) => p.isActive);

  const applicationRows = useMemo(() => applications.data ?? [], [applications.data]);

  const create = useMutation({
    mutationFn: (values: FormOutput) =>
      jobInterviewService.create({
        jobVacancyId: values.jobVacancyId,
        round: values.round,
        type: values.type as never,
        mode: values.mode as never,
        scheduledDate: values.scheduledDate,
        // The API takes TimeSpan; an <input type="time"> yields HH:mm.
        startTime: `${values.startTime}:00`,
        endTime: `${values.endTime}:00`,
        locationOrLink: values.locationOrLink?.trim() ? values.locationOrLink : null,
        instructions: values.instructions?.trim() ? values.instructions : null,
        applicationIds: selectedApplications,
        panelistEmployeeIds: panelEmployees.map((p) => p.id),
        externalPanelistAssociateIds: panelExternals.map((p) => p.id),
        questionPresetId: values.questionPresetId === NO_PRESET ? null : values.questionPresetId,
        // Sent whatever is in the box: the server discards it when there is no clash to override,
        // so there is nothing to guard against here.
        panelClashOverrideReason: values.panelClashOverrideReason?.trim() || null,
      }),
    onSuccess: (interview) => {
      toast({
        title: `Interview ${interview.interviewNumber} scheduled`,
        description: 'Invitations are not sent automatically — send them from the interview.',
      });
      router.push(`/hr/recruitment/interviews/${interview.id}`);
    },
    onError: (error: any) => {
      // ⚠ A clash refusal is not a generic failure. The server names who is committed and to what,
      // and the way past it is a box that is already on this form — so say so, and reveal it.
      const message: string = error?.message ?? '';
      if (/already committed at that time/i.test(message)) setClashRefused(true);
      toast({
        title: /already committed at that time/i.test(message)
          ? 'The panel is not free at that time'
          : 'Could not schedule the interview',
        description: message || 'Please check the details and try again.',
        variant: 'destructive',
      });
    },
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Schedule an interview"
        description="One session, one round — with its panel, its candidates and its question plan."
        backHref="/hr/recruitment/interviews"
      />

      <form className="space-y-6" onSubmit={form.handleSubmit((v) => create.mutate(v))}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">The session</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>
                Vacancy<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Select
                value={form.watch('jobVacancyId')}
                onValueChange={(v) => {
                  form.setValue('jobVacancyId', v, { shouldValidate: true });
                  setSelectedApplications([]);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Choose the vacancy being interviewed for" />
                </SelectTrigger>
                <SelectContent>
                  {(vacancies.data?.items ?? []).map((vacancy) => (
                    <SelectItem key={vacancy.id} value={vacancy.id}>
                      {vacancy.vacancyNumber} — {vacancy.jobTitle || 'Untitled'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {form.formState.errors.jobVacancyId && (
                <p className="text-sm text-red-500">{form.formState.errors.jobVacancyId.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="round">Round</Label>
              <Input id="round" type="number" min={1} max={20} {...form.register('round')} />
            </div>

            <div className="space-y-2">
              <Label>Format</Label>
              <Select value={form.watch('type')} onValueChange={(v) => form.setValue('type', v)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {INTERVIEW_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {humanizeEnum(t)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Held</Label>
              <Select value={form.watch('mode')} onValueChange={(v) => form.setValue('mode', v)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {INTERVIEW_MODES.map((m) => (
                    <SelectItem key={m} value={m}>
                      {humanizeEnum(m)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="scheduledDate">
                Date<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input id="scheduledDate" type="date" {...form.register('scheduledDate')} />
              {form.formState.errors.scheduledDate && (
                <p className="text-sm text-red-500">{form.formState.errors.scheduledDate.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="startTime">Starts</Label>
              <Input id="startTime" type="time" {...form.register('startTime')} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="endTime">Ends</Label>
              <Input id="endTime" type="time" {...form.register('endTime')} />
              {form.formState.errors.endTime && (
                <p className="text-sm text-red-500">{form.formState.errors.endTime.message}</p>
              )}
            </div>

            {clashRefused && (
              <div className="space-y-2 md:col-span-2 rounded-md border border-destructive/40 bg-destructive/5 p-3">
                <Label htmlFor="panelClashOverrideReason">
                  Reason for scheduling over the clash
                </Label>
                <Textarea
                  id="panelClashOverrideReason"
                  rows={2}
                  placeholder="e.g. Kofi has moved his 10:00 meeting to make room for this."
                  {...form.register('panelClashOverrideReason')}
                />
                <p className="text-xs text-muted-foreground">
                  A panelist is already committed at this time. Give a reason and the interview will
                  be scheduled anyway — the reason, who gave it and what it overrode are recorded on
                  the interview.
                </p>
              </div>
            )}

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="locationOrLink">Location or meeting link</Label>
              <Input
                id="locationOrLink"
                placeholder="Boardroom 2, or a video link"
                {...form.register('locationOrLink')}
              />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="instructions">Instructions for the candidate</Label>
              <Textarea
                id="instructions"
                rows={3}
                placeholder="What to bring, where to report, who to ask for."
                {...form.register('instructions')}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Candidates</CardTitle>
            <CardDescription>
              Only this vacancy&rsquo;s applicants can be booked in — an application for another role is
              refused.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {!vacancyId ? (
              <p className="text-sm text-muted-foreground">Choose a vacancy first.</p>
            ) : applications.isLoading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading applicants…
              </div>
            ) : applicationRows.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No applications on this vacancy yet.
              </p>
            ) : (
              <div className="max-h-72 space-y-1 overflow-y-auto rounded-md border p-2">
                {applicationRows.map((application) => (
                  <label
                    key={application.id}
                    className="flex cursor-pointer items-center gap-3 rounded-md px-2 py-2 hover:bg-muted"
                  >
                    <Checkbox
                      checked={selectedApplications.includes(application.id)}
                      onCheckedChange={(checked) =>
                        setSelectedApplications((prev) =>
                          checked ? [...prev, application.id] : prev.filter((id) => id !== application.id),
                        )
                      }
                    />
                    <span className="flex-1">
                      <span className="font-medium">{application.candidateName}</span>
                      <span className="ml-2 text-sm text-muted-foreground">
                        {application.applicationNumber} · {humanizeEnum(application.status)}
                      </span>
                    </span>
                  </label>
                ))}
              </div>
            )}
            {selectedApplications.length > 0 && (
              <p className="mt-3 text-sm text-muted-foreground">
                {selectedApplications.length} candidate{selectedApplications.length === 1 ? '' : 's'} will
                be booked in and moved to the interview stage. Per-candidate slot times can be set on the
                interview afterwards.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Panel</CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            <PanelMemberPicker
              employees={panelEmployees}
              externals={panelExternals}
              onEmployeesChange={setPanelEmployees}
              onExternalsChange={setPanelExternals}
            />
            <PanelAvailabilityPanel
              panelistIds={panelEmployees.map((p) => p.id)}
              externalPanelistIds={panelExternals.map((p) => p.id)}
              date={form.watch('scheduledDate')}
              start={`${form.watch('startTime')}:00`}
              end={`${form.watch('endTime')}:00`}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Questions</CardTitle>
            <CardDescription>
              A preset scaffolds the question plan and draws the questions from the bank in one step.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <Select
              value={form.watch('questionPresetId')}
              onValueChange={(v) => form.setValue('questionPresetId', v)}
            >
              <SelectTrigger className="md:w-[420px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_PRESET}>No preset — build the plan by hand</SelectItem>
                {activePresets.map((preset) => (
                  <SelectItem key={preset.id} value={preset.id}>
                    {preset.name} ({preset.itemCount} section{preset.itemCount === 1 ? '' : 's'})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                The drawn questions can be reviewed, re-drawn or chosen by hand on the interview before
                the panel sits.
              </AlertDescription>
            </Alert>
          </CardContent>
        </Card>

        <div className="flex justify-end gap-3">
          <Button type="button" variant="outline" onClick={() => router.back()}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <CalendarPlus className="mr-2 h-4 w-4" />
            )}
            Schedule
          </Button>
        </div>
      </form>
    </div>
  );
}
