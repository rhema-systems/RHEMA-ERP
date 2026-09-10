'use client';

/**
 * A team's minute book — what it met about, and what it decided.
 *
 * ⚠ **The decision row's "Create task" is the point of this tab.** An action item that lives only in
 * minutes is one nobody chases; raising a task links the two and puts it on the board. The raised
 * task takes its assignee and due date from the DECISION, so the minute and the board cannot say
 * different things about who owns it.
 *
 * ⚠ **A held meeting cannot be edited.** Its record is what happened. The screen offers Cancel and
 * a follow-up rather than an Edit that the server would refuse.
 *
 * Round 2, lane F2 (plan § 6.6).
 */

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  DateTimeField,
  FieldRow,
  SelectField,
  TextField,
  TextareaField,
  fromIsoInstant,
  toIsoInstant,
} from '@/components/hr/employee/tabs/fields';
import { teamService } from '@/services/hr/team.service';
import { teamMeetingService } from '@/services/hr/team-meeting.service';
import {
  TEAM_MEETING_KIND_OPTIONS,
  TEAM_MEETING_STATUS_LABELS,
  type TeamMeeting,
} from '@/types/hr/team-meeting';
import { TeamMeetingDetailDialog } from './TeamMeetingDetailDialog';

const schema = z.object({
  kind: z.enum(['Meeting', 'Workshop', 'SiteVisit', 'Other']),
  title: z.string().min(1, 'A title is required').max(300),
  scheduledAt: z.string().min(1, 'A date and time are required'),
  venue: z.string().max(300).optional().or(z.literal('')),
  agenda: z.string().max(4000).optional().or(z.literal('')),
  chairMemberId: z.string().optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  kind: 'Meeting',
  title: '',
  scheduledAt: '',
  venue: '',
  agenda: '',
  chairMemberId: '',
};

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'outline' | 'destructive'> = {
  Scheduled: 'secondary',
  Held: 'default',
  Cancelled: 'outline',
};

export function TeamMeetingsTab({ teamId }: { teamId: string }) {
  const queryClient = useQueryClient();
  const [openMeeting, setOpenMeeting] = useState<TeamMeeting | null>(null);

  const { data: members = [] } = useQuery({
    queryKey: ['hr', 'teams', teamId, 'members'],
    queryFn: () => teamService.getMembers(teamId),
  });
  const current = members.filter((m) => m.isCurrent);
  const memberOptions = current.map((m) => ({
    value: m.id,
    label: m.employeeName || m.employeeNumber || m.id,
  }));

  /**
   * ⚠ Everyone currently on the team is invited by default.
   *
   * A committee meeting invites the committee — that is what a committee is. Making the user tick
   * seven names to schedule a routine monthly meeting would be a form fighting its own purpose.
   * The invitee list is editable afterwards, and the update REPLACES it.
   */
  const toPayload = (v: FormValues) => ({
    kind: v.kind,
    title: v.title,
    scheduledAt: toIsoInstant(v.scheduledAt)!,
    venue: v.venue || null,
    agenda: v.agenda || null,
    chairMemberId: v.chairMemberId || null,
    attendeeMemberIds: current.map((m) => m.id),
  });

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'meetings'] });
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'dashboard'] });
    // Raising a task from a decision puts it on the board.
    void queryClient.invalidateQueries({ queryKey: ['hr', 'teams', teamId, 'tasks'] });
  };

  return (
    <div className="space-y-4">
      <ResourceCollectionTab<TeamMeeting, FormValues>
        parentId={teamId}
        title="meetings"
        singular="meeting"
        queryKey={['hr', 'teams', teamId, 'meetings']}
        invalidateKeys={[['hr', 'teams', teamId, 'dashboard']]}
        dialogHint="Everyone currently on the team is invited. Open the meeting after saving to add decisions, mark the register and attach the signed minutes."
        dialogClassName="sm:max-w-[640px]"
        getId={(m) => m.id}
        list={() => teamMeetingService.getMeetings(teamId)}
        create={(_id, values) => teamMeetingService.createMeeting(teamId, toPayload(values))}
        update={(_id, id, values) => teamMeetingService.updateMeeting(id, toPayload(values))}
        remove={(_id, id) => teamMeetingService.deleteMeeting(id)}
        loadForEdit={async (m) => {
          const full = await teamMeetingService.getMeeting(m.id);
          return {
            kind: full.kind,
            title: full.title,
            scheduledAt: fromIsoInstant(full.scheduledAt),
            venue: full.venue ?? '',
            agenda: full.agenda ?? '',
            chairMemberId: full.chairMemberId ?? '',
          };
        }}
        actions={[
          {
            label: 'Open…',
            run: async (m) => {
              setOpenMeeting(m);
            },
          },
          {
            label: 'Cancel meeting',
            visible: (m) => m.status === 'Scheduled',
            run: (m) =>
              teamMeetingService.cancelMeeting(m.id, 'Cancelled from the meetings tab'),
            destructive: true,
            confirm: {
              title: 'Cancel this meeting?',
              description: 'It stays in the record as cancelled rather than being removed.',
            },
          },
        ]}
        columns={[
          { header: 'Meeting', cell: (m) => <span className="font-medium">{m.title}</span> },
          { header: 'Kind', cell: (m) => m.kind === 'SiteVisit' ? 'Site visit' : m.kind },
          {
            header: 'When',
            cell: (m) =>
              new Date(m.heldAt ?? m.scheduledAt).toLocaleString(undefined, {
                day: 'numeric',
                month: 'short',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              }),
          },
          { header: 'Chair', cell: (m) => m.chairName || '—' },
          {
            header: 'Attendance',
            // ⚠ Before a meeting is held every attendance is null, so this reads "0 of 7" — which
            // is "nobody has come yet", not "nobody came". The status badge beside it disambiguates.
            cell: (m) =>
              m.status === 'Held' ? (
                <span className="text-xs">
                  {m.attendedCount}/{m.attendeeCount}
                </span>
              ) : (
                <span className="text-muted-foreground text-xs">{m.attendeeCount} invited</span>
              ),
          },
          {
            header: 'Decisions',
            cell: (m) =>
              m.decisionCount === 0 ? (
                <span className="text-muted-foreground text-xs">—</span>
              ) : (
                <span className="text-xs">
                  {m.decisionCount}
                  {/* Decisions that became tasks vs decisions still only in the minutes. */}
                  {m.decisionsWithTaskCount < m.decisionCount && (
                    <span className="text-amber-600">
                      {' '}· {m.decisionCount - m.decisionsWithTaskCount} unactioned
                    </span>
                  )}
                </span>
              ),
          },
          {
            header: 'Status',
            cell: (m) => (
              <Badge variant={STATUS_VARIANT[m.status] ?? 'outline'}>
                {TEAM_MEETING_STATUS_LABELS[m.status]}
              </Badge>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={() => empty}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="title" label="Title" required />
              <SelectField
                form={form}
                name="kind"
                label="Kind"
                required
                options={TEAM_MEETING_KIND_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <DateTimeField form={form} name="scheduledAt" label="Scheduled for" required />
              <SelectField
                form={form}
                name="chairMemberId"
                label="Chair"
                options={memberOptions}
                allowEmpty
                emptyLabel="Not decided"
              />
            </FieldRow>
            <TextField form={form} name="venue" label="Venue" />
            <TextareaField form={form} name="agenda" label="Agenda" rows={4} />
          </>
        )}
      />

      <TeamMeetingDetailDialog
        meetingId={openMeeting?.id ?? null}
        onOpenChange={(open) => !open && setOpenMeeting(null)}
        onChanged={refresh}
      />
    </div>
  );
}
