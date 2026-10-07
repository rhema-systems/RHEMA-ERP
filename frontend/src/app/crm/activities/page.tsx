'use client';

import { hasCustomerRole } from '@/lib/business-partner-roles';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import React, { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
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
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  crmService,
  type CreateCrmActivityDto,
  type CrmActivityDetailDto,
  type CrmActivityEmployeeAttendeeDto,
  type CrmActivityListItemDto,
  type CrmLeadListItemDto,
  type CrmOpportunityListItemDto,
  type PagedResult,
} from '@/services/crmService';
import {
  propertyEnquiryService,
  type PropertyEnquiryQueueItem,
} from '@/services/propertyEnquiryService';
import {
  Activity,
  AlertTriangle,
  Building2,
  CalendarClock,
  Clock3,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Target,
  Trash2,
  X,
} from 'lucide-react';
import { toast } from 'sonner';
import { resolveActivityPropertyEnquiryContext } from '@/lib/crm-activity-property-enquiry';

const ACTIVITY_STATUS_OPTIONS = [
  'Planned',
  'In Progress',
  'Completed',
  'Cancelled',
];
const ACTIVITY_TYPE_OPTIONS = ['Call', 'Meeting', 'Email', 'Task', 'Note'];
const PRIORITY_OPTIONS = [
  { value: 1, label: 'High' },
  { value: 2, label: 'Medium' },
  { value: 3, label: 'Low' },
  { value: 4, label: 'Very Low' },
];

const createEmptyActivityForm = (
  businessPartnerId?: string,
  leadId?: string,
  opportunityId?: string
): CreateCrmActivityDto => ({
  subject: '',
  activityType: 'Call',
  description: '',
  activityDate: new Date().toISOString().slice(0, 10),
  dueDate: '',
  activityStatus: 'Planned',
  priority: 2,
  duration: undefined,
  assignedToId: undefined,
  businessPartnerId: businessPartnerId || '',
  leadId: leadId || '',
  opportunityId: opportunityId || '',
  propertyEnquiryTicketId: '',
  location: '',
  externalAttendees: '',
  internalAttendeeEmployeeIds: [],
  outcome: '',
  notes: '',
  requiresFollowUp: false,
  nextFollowUpDate: '',
});

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString() : 'None';
const formatDateTime = (value?: string) =>
  value ? new Date(value).toLocaleString() : 'None';
const getMessage = (error: unknown, fallback: string) =>
  error instanceof Error ? error.message : fallback;
const toOptionalString = (value?: string) =>
  value?.trim() ? value.trim() : undefined;

const getPriorityLabel = (priority: number) =>
  PRIORITY_OPTIONS.find((option) => option.value === priority)?.label ||
  'Medium';

const getPriorityVariant = (
  priority: number
): 'default' | 'secondary' | 'outline' | 'destructive' => {
  if (priority <= 1) {
    return 'destructive';
  }

  if (priority === 2) {
    return 'secondary';
  }

  return 'outline';
};

const buildActivityPayload = (
  form: CreateCrmActivityDto
): CreateCrmActivityDto => ({
  subject: form.subject.trim(),
  activityType: form.activityType,
  description: toOptionalString(form.description),
  activityDate: form.activityDate,
  dueDate: form.dueDate || undefined,
  activityStatus: form.activityStatus,
  priority: Number(form.priority) || 2,
  duration:
    form.duration && form.duration > 0 ? Number(form.duration) : undefined,
  assignedToId: form.assignedToId || undefined,
  businessPartnerId: form.businessPartnerId || undefined,
  leadId: form.leadId || undefined,
  opportunityId: form.opportunityId || undefined,
  propertyEnquiryTicketId: form.propertyEnquiryTicketId || undefined,
  location: toOptionalString(form.location),
  externalAttendees: toOptionalString(form.externalAttendees),
  internalAttendeeEmployeeIds: form.internalAttendeeEmployeeIds || [],
  outcome: toOptionalString(form.outcome),
  notes: toOptionalString(form.notes),
  requiresFollowUp: form.requiresFollowUp,
  nextFollowUpDate:
    form.requiresFollowUp && form.nextFollowUpDate
      ? form.nextFollowUpDate
      : undefined,
});

const mapActivityToForm = (
  activity: CrmActivityDetailDto
): CreateCrmActivityDto => ({
  subject: activity.subject,
  activityType: activity.activityType || 'Call',
  description: activity.description || '',
  activityDate: activity.activityDate.slice(0, 10),
  dueDate: activity.dueDate?.slice(0, 10) || '',
  activityStatus: activity.activityStatus || 'Planned',
  priority: activity.priority || 2,
  duration: activity.duration,
  assignedToId: activity.assignedToId,
  businessPartnerId: activity.businessPartnerId || '',
  leadId: activity.leadId || '',
  opportunityId: activity.opportunityId || '',
  propertyEnquiryTicketId: activity.propertyEnquiryTicketId || '',
  location: activity.location || '',
  externalAttendees: activity.externalAttendees || activity.attendees || '',
  internalAttendeeEmployeeIds: (activity.internalAttendees ?? []).map(
    (attendee) => attendee.employeeId
  ),
  outcome: activity.outcome || '',
  notes: activity.notes || '',
  requiresFollowUp: activity.requiresFollowUp,
  nextFollowUpDate: activity.nextFollowUpDate?.slice(0, 10) || '',
});

const resolveContextLine = (
  activity: Pick<
    CrmActivityListItemDto,
    'businessPartnerName' | 'opportunityName' | 'leadName'
  >
) =>
  [activity.businessPartnerName, activity.opportunityName, activity.leadName]
    .filter(Boolean)
    .join(' | ') || 'General CRM context';

function ActivityDialog({
  open,
  title,
  description,
  form,
  saving,
  accounts,
  leads,
  opportunities,
  internalAttendees,
  propertyEnquiries,
  propertyEnquiryReadOnly,
  leadReadOnly,
  leadName,
  onOpenChange,
  onSubmit,
  onChange,
  onInternalAttendeesChange,
}: {
  open: boolean;
  title: string;
  description: string;
  form: CreateCrmActivityDto;
  saving: boolean;
  accounts: BusinessPartnerDto[];
  leads: CrmLeadListItemDto[];
  opportunities: CrmOpportunityListItemDto[];
  internalAttendees: CrmActivityEmployeeAttendeeDto[];
  propertyEnquiries: PropertyEnquiryQueueItem[];
  propertyEnquiryReadOnly: boolean;
  leadReadOnly: boolean;
  leadName?: string;
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onChange: <K extends keyof CreateCrmActivityDto>(
    field: K,
    value: CreateCrmActivityDto[K]
  ) => void;
  onInternalAttendeesChange: (
    attendees: CrmActivityEmployeeAttendeeDto[]
  ) => void;
}) {
  const {
    inheritedPropertyEnquiry,
    selectablePropertyEnquiries,
    showPropertyEnquirySelector,
  } = resolveActivityPropertyEnquiryContext(propertyEnquiries, form.leadId);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[85vh] max-w-4xl flex-col gap-0 overflow-hidden p-0">
        <DialogHeader className="shrink-0 border-b px-6 py-4 pr-12">
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto px-6">
          <div className="grid gap-3 py-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="activity-subject">Subject</Label>
              <Input
                id="activity-subject"
                value={form.subject}
                onChange={(event) => onChange('subject', event.target.value)}
                placeholder="Executive follow-up call"
              />
            </div>

            <div className="space-y-2">
              <Label>Activity Type</Label>
              <Select
                value={form.activityType}
                onValueChange={(value) => onChange('activityType', value)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select activity type" />
                </SelectTrigger>
                <SelectContent>
                  {ACTIVITY_TYPE_OPTIONS.map((option) => (
                    <SelectItem key={option} value={option}>
                      {option}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Status</Label>
              <Select
                value={form.activityStatus}
                onValueChange={(value) => onChange('activityStatus', value)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select activity status" />
                </SelectTrigger>
                <SelectContent>
                  {ACTIVITY_STATUS_OPTIONS.map((option) => (
                    <SelectItem key={option} value={option}>
                      {option}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>CRM Account</Label>
              <Select
                value={form.businessPartnerId || 'none'}
                onValueChange={(value) =>
                  onChange('businessPartnerId', value === 'none' ? '' : value)
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select account" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No direct account</SelectItem>
                  {accounts.map((account) => (
                    <SelectItem key={account.id} value={account.id}>
                      {account.partnerName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {leadReadOnly ? (
              <div className="space-y-2">
                <Label>Lead</Label>
                <div className="rounded-md border bg-muted/30 px-3 py-2 text-sm">
                  {leadName || 'Selected Lead'}
                </div>
                <p className="text-xs text-muted-foreground">
                  Linked automatically from the Lead page.
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                <Label>Lead</Label>
                <Select
                  value={form.leadId || 'none'}
                  onValueChange={(value) => {
                    const leadId = value === 'none' ? '' : value;
                    const linkedEnquiries = propertyEnquiries.filter(
                      (enquiry) => enquiry.crmLeadId === leadId
                    );
                    onChange('leadId', leadId);
                    onChange(
                      'propertyEnquiryTicketId',
                      linkedEnquiries.length === 1 ? linkedEnquiries[0].id : ''
                    );
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select lead" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No linked lead</SelectItem>
                    {leads.map((lead) => (
                      <SelectItem key={lead.leadId} value={lead.leadId}>
                        {lead.fullName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            <div className="space-y-2">
              <Label>Opportunity</Label>
              <Select
                value={form.opportunityId || 'none'}
                onValueChange={(value) =>
                  onChange('opportunityId', value === 'none' ? '' : value)
                }
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select opportunity" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No linked opportunity</SelectItem>
                  {opportunities.map((opportunity) => (
                    <SelectItem
                      key={opportunity.opportunityId}
                      value={opportunity.opportunityId}
                    >
                      {opportunity.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {inheritedPropertyEnquiry ? (
              <div className="space-y-2 md:col-span-2">
                <Label>Property Enquiry</Label>
                <div className="rounded-md border bg-muted/30 px-3 py-2 text-sm">
                  {inheritedPropertyEnquiry.ticketNumber} -{' '}
                  {inheritedPropertyEnquiry.subject}
                </div>
                <p className="text-xs text-muted-foreground">
                  Inherited automatically from the selected Lead.
                </p>
              </div>
            ) : showPropertyEnquirySelector ? (
              <div className="space-y-2 md:col-span-2">
                <Label>Property Enquiry</Label>
                <Select
                  disabled={propertyEnquiryReadOnly}
                  value={form.propertyEnquiryTicketId || 'none'}
                  onValueChange={(value) => {
                    const enquiry = propertyEnquiries.find(
                      (item) => item.id === value
                    );
                    onChange(
                      'propertyEnquiryTicketId',
                      value === 'none' ? '' : value
                    );
                    if (enquiry?.crmLeadId) {
                      onChange('leadId', enquiry.crmLeadId);
                    }
                    if (value !== 'none') {
                      onChange('businessPartnerId', '');
                      onChange('opportunityId', '');
                    }
                  }}
                >
                  <SelectTrigger aria-label="Property Enquiry">
                    <SelectValue placeholder="Select property enquiry" />
                  </SelectTrigger>
                  <SelectContent>
                    {!form.leadId ? (
                      <SelectItem value="none">
                        No linked property enquiry
                      </SelectItem>
                    ) : null}
                    {selectablePropertyEnquiries.map((enquiry) => (
                      <SelectItem key={enquiry.id} value={enquiry.id}>
                        {enquiry.ticketNumber} - {enquiry.subject}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {form.leadId
                    ? 'This Lead has multiple enquiries. Select the enquiry for this activity.'
                    : 'A completed Call, Meeting, or Email records Sales contact for the selected enquiry and enables qualification.'}
                </p>
              </div>
            ) : null}

            <div className="space-y-2">
              <Label>Priority</Label>
              <Select
                value={String(form.priority)}
                onValueChange={(value) => onChange('priority', Number(value))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select priority" />
                </SelectTrigger>
                <SelectContent>
                  {PRIORITY_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={String(option.value)}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="activity-date">Activity Date</Label>
              <Input
                id="activity-date"
                type="date"
                value={form.activityDate}
                onChange={(event) =>
                  onChange('activityDate', event.target.value)
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="activity-due-date">Due Date</Label>
              <Input
                id="activity-due-date"
                type="date"
                value={form.dueDate || ''}
                onChange={(event) => onChange('dueDate', event.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="activity-duration">Duration (minutes)</Label>
              <Input
                id="activity-duration"
                type="number"
                min={0}
                value={form.duration ?? ''}
                onChange={(event) =>
                  onChange(
                    'duration',
                    event.target.value ? Number(event.target.value) : undefined
                  )
                }
                placeholder="45"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="activity-location">Location</Label>
              <Input
                id="activity-location"
                value={form.location || ''}
                onChange={(event) => onChange('location', event.target.value)}
                placeholder="Client office or Teams"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="activity-outcome">Outcome</Label>
              <Input
                id="activity-outcome"
                value={form.outcome || ''}
                onChange={(event) => onChange('outcome', event.target.value)}
                placeholder="Left message, Successful, Reschedule"
              />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label>Internal attendees</Label>
              <EmployeePicker
                value={null}
                placeholder="Search employees to add..."
                onChange={(employeeId, displayName) => {
                  if (
                    !employeeId ||
                    !displayName ||
                    internalAttendees.some(
                      (attendee) => attendee.employeeId === employeeId
                    )
                  ) {
                    return;
                  }

                  onInternalAttendeesChange([
                    ...internalAttendees,
                    {
                      employeeId,
                      employeeNumber: '',
                      displayName,
                    },
                  ]);
                }}
              />
              {internalAttendees.length ? (
                <div className="flex flex-wrap gap-2">
                  {internalAttendees.map((attendee) => (
                    <Badge
                      key={attendee.employeeId}
                      variant="secondary"
                      className="gap-1 py-1 pl-2 pr-1"
                    >
                      {attendee.displayName}
                      <button
                        type="button"
                        className="rounded-sm p-0.5 hover:bg-muted"
                        aria-label={`Remove ${attendee.displayName}`}
                        onClick={() =>
                          onInternalAttendeesChange(
                            internalAttendees.filter(
                              (item) => item.employeeId !== attendee.employeeId
                            )
                          )
                        }
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </Badge>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-muted-foreground">
                  Search by employee name or number and select each attendee.
                </p>
              )}
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="activity-external-attendees">
                External attendees
              </Label>
              <Input
                id="activity-external-attendees"
                value={form.externalAttendees || ''}
                onChange={(event) =>
                  onChange('externalAttendees', event.target.value)
                }
                maxLength={1000}
                placeholder="Ama Boateng, client@example.com"
              />
              <p className="text-xs text-muted-foreground">
                Enter external names or email addresses separated by commas.
              </p>
            </div>

            <div className="space-y-3 rounded-lg border p-3 md:col-span-2">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <div className="font-medium">Requires Follow-Up</div>
                  <div className="text-sm text-muted-foreground">
                    Keep this activity in the CRM follow-up queue.
                  </div>
                </div>
                <Switch
                  checked={form.requiresFollowUp}
                  onCheckedChange={(checked) =>
                    onChange('requiresFollowUp', checked)
                  }
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="activity-next-follow-up">
                  Next Follow-Up Date
                </Label>
                <Input
                  id="activity-next-follow-up"
                  type="date"
                  value={form.nextFollowUpDate || ''}
                  onChange={(event) =>
                    onChange('nextFollowUpDate', event.target.value)
                  }
                  disabled={!form.requiresFollowUp}
                />
              </div>
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="activity-description">Description</Label>
              <Textarea
                id="activity-description"
                value={form.description || ''}
                onChange={(event) =>
                  onChange('description', event.target.value)
                }
                placeholder="Capture the purpose and customer context for this touchpoint."
                rows={2}
              />
            </div>

            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="activity-notes">Notes</Label>
              <Textarea
                id="activity-notes"
                value={form.notes || ''}
                onChange={(event) => onChange('notes', event.target.value)}
                placeholder="Add internal notes, follow-up actions, and next commitments."
                rows={3}
              />
            </div>
          </div>
        </div>

        <DialogFooter className="shrink-0 border-t bg-background px-6 py-3">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={onSubmit} disabled={saving}>
            {saving ? 'Saving...' : 'Save Activity'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function CrmActivitiesPage() {
  const searchParams = useSearchParams();
  const resolvedSearchParams = searchParams ?? new URLSearchParams();
  const scopedBusinessPartnerId =
    resolvedSearchParams.get('businessPartnerId') || '';
  const scopedLeadId = resolvedSearchParams.get('leadId') || '';
  const scopedOpportunityId = resolvedSearchParams.get('opportunityId') || '';
  const requestedActivityId = resolvedSearchParams.get('activityId') || '';

  const [search, setSearch] = useState(
    resolvedSearchParams.get('search') || ''
  );
  const [status, setStatus] = useState(
    resolvedSearchParams.get('status') || 'all'
  );
  const [activityType, setActivityType] = useState(
    resolvedSearchParams.get('activityType') || 'all'
  );
  const [followUpOnly, setFollowUpOnly] = useState(
    resolvedSearchParams.get('followUpOnly') === 'true'
  );
  const [page, setPage] = useState(1);

  const [result, setResult] =
    useState<PagedResult<CrmActivityListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedActivityId, setSelectedActivityId] =
    useState(requestedActivityId);
  const [selectedActivity, setSelectedActivity] =
    useState<CrmActivityDetailDto | null>(null);

  const [accounts, setAccounts] = useState<BusinessPartnerDto[]>([]);
  const [leads, setLeads] = useState<CrmLeadListItemDto[]>([]);
  const [opportunities, setOpportunities] = useState<
    CrmOpportunityListItemDto[]
  >([]);
  const [propertyEnquiries, setPropertyEnquiries] = useState<
    PropertyEnquiryQueueItem[]
  >([]);

  const [formOpen, setFormOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [form, setForm] = useState<CreateCrmActivityDto>(
    createEmptyActivityForm(
      scopedBusinessPartnerId,
      scopedLeadId,
      scopedOpportunityId
    )
  );
  const [internalAttendees, setInternalAttendees] = useState<
    CrmActivityEmployeeAttendeeDto[]
  >([]);
  const [saving, setSaving] = useState(false);
  const [newRequestHandled, setNewRequestHandled] = useState(false);

  const loadLookups = async () => {
    try {
      const [partnerData, leadData, opportunityData] = await Promise.all([
        businessPartnerService.getAllPartnersForDropdown(),
        crmService.getLeads({ page: 1, pageSize: 100 }),
        crmService.getOpportunities({ page: 1, pageSize: 100 }),
      ]);

      setAccounts(
        partnerData
          .filter((partner) => partner.status !== 'Inactive')
          .filter(
            (partner) =>
              hasCustomerRole(partner.partnerType) ||
              !!partner.customerType
          )
          .sort((left, right) =>
            left.partnerName.localeCompare(right.partnerName)
          )
      );
      setLeads(leadData.items);
      setOpportunities(opportunityData.items);
      try {
        setPropertyEnquiries(await propertyEnquiryService.listAll());
      } catch {
        // The selector is restricted to users who can access the Sales
        // property-enquiry register. Other CRM activity workflows remain usable.
        setPropertyEnquiries([]);
      }
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM activity lookups'));
    }
  };

  const loadActivities = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getActivities({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        activityType: activityType === 'all' ? undefined : activityType,
        followUpOnly,
        businessPartnerId: scopedBusinessPartnerId || undefined,
        leadId: scopedLeadId || undefined,
        opportunityId: scopedOpportunityId || undefined,
      });

      setResult(data);

      if (requestedActivityId && requestedPage === 1) {
        setSelectedActivityId(requestedActivityId);
        return;
      }

      if (
        selectedActivityId &&
        data.items.some((item) => item.activityId === selectedActivityId)
      ) {
        return;
      }

      setSelectedActivityId(data.items[0]?.activityId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM activities'));
    } finally {
      setLoading(false);
    }
  };

  const loadActivityDetail = async (activityId: string) => {
    if (!activityId) {
      setSelectedActivity(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedActivity(await crmService.getActivity(activityId));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM activity detail'));
      setSelectedActivity(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadLookups();
  }, []);

  useEffect(() => {
    void loadActivities(page);
  }, [
    page,
    status,
    activityType,
    followUpOnly,
    scopedBusinessPartnerId,
    scopedLeadId,
    scopedOpportunityId,
  ]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadActivities(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadActivityDetail(selectedActivityId);
  }, [selectedActivityId]);

  useEffect(() => {
    if (newRequestHandled || resolvedSearchParams.get('new') !== '1') {
      return;
    }

    setFormMode('create');
    setForm(
      createEmptyActivityForm(
        scopedBusinessPartnerId,
        scopedLeadId,
        scopedOpportunityId
      )
    );
    setInternalAttendees([]);
    setFormOpen(true);
    setNewRequestHandled(true);
  }, [
    newRequestHandled,
    resolvedSearchParams,
    scopedBusinessPartnerId,
    scopedLeadId,
    scopedOpportunityId,
  ]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Activities',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Filtered CRM touchpoints',
        icon: Activity,
      },
      {
        label: 'Follow-Up Queue',
        value: items
          .filter((item) => item.requiresFollowUp)
          .length.toLocaleString(),
        hint: 'Current page only',
        icon: CalendarClock,
      },
      {
        label: 'Overdue Items',
        value: items.filter((item) => item.isOverdue).length.toLocaleString(),
        hint: 'Past due and still open',
        icon: AlertTriangle,
      },
      {
        label: 'Account Coverage',
        value: new Set(
          items.map((item) => item.businessPartnerId).filter(Boolean)
        ).size.toLocaleString(),
        hint: 'Distinct CRM accounts on this page',
        icon: Building2,
      },
    ];
  }, [result]);

  const scopedAccountName = accounts.find(
    (account) => account.id === scopedBusinessPartnerId
  )?.partnerName;
  const scopedLeadName = leads.find(
    (lead) => lead.leadId === scopedLeadId
  )?.fullName;
  const scopedOpportunityName = opportunities.find(
    (opportunity) => opportunity.opportunityId === scopedOpportunityId
  )?.name;

  const openCreateDialog = () => {
    setFormMode('create');
    setForm(
      createEmptyActivityForm(
        scopedBusinessPartnerId,
        scopedLeadId,
        scopedOpportunityId
      )
    );
    setInternalAttendees([]);
    setFormOpen(true);
  };

  const openEditDialog = async (activityId?: string) => {
    const targetActivityId = activityId || selectedActivityId;
    if (!targetActivityId) {
      return;
    }

    try {
      const detail =
        targetActivityId === selectedActivity?.activityId && selectedActivity
          ? selectedActivity
          : await crmService.getActivity(targetActivityId);

      setSelectedActivity(detail);
      setSelectedActivityId(detail.activityId);
      setFormMode('edit');
      setForm(mapActivityToForm(detail));
      setInternalAttendees(detail.internalAttendees || []);
      setFormOpen(true);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM activity for editing'));
    }
  };

  const submitActivity = async () => {
    if (!form.subject.trim()) {
      toast.error('Activity subject is required.');
      return;
    }

    try {
      setSaving(true);
      const payload = buildActivityPayload(form);
      const activity =
        formMode === 'create'
          ? await crmService.createActivity(payload)
          : await crmService.updateActivity(selectedActivityId, payload);

      toast.success(
        formMode === 'create' ? 'Activity created.' : 'Activity updated.'
      );
      setFormOpen(false);
      setSelectedActivityId(activity.activityId);
      setPage(1);
      await loadActivities(1);
      await loadActivityDetail(activity.activityId);
    } catch (error: unknown) {
      toast.error(getMessage(error, `Failed to ${formMode} CRM activity`));
    } finally {
      setSaving(false);
    }
  };

  const deleteActivity = async () => {
    if (!selectedActivityId) {
      return;
    }

    try {
      setSaving(true);
      await crmService.deleteActivity(selectedActivityId);
      toast.success('Activity deleted.');
      setDeleteOpen(false);
      setSelectedActivity(null);
      setSelectedActivityId('');
      setPage(1);
      await loadActivities(1);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to delete CRM activity'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Activities</h1>
          <p className="text-muted-foreground">
            Manage the follow-up queue and customer touchpoints already flowing
            into the ERP-native CRM workspace.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadActivities(page)}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={openCreateDialog}>
            <Plus className="mr-2 h-4 w-4" />
            New Activity
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {metrics.map((metric) => {
          const Icon = metric.icon;

          return (
            <Card key={metric.label}>
              <CardHeader className="pb-2">
                <CardDescription className="flex items-center gap-2">
                  <Icon className="h-4 w-4" />
                  {metric.label}
                </CardDescription>
                <CardTitle>{metric.value}</CardTitle>
              </CardHeader>
              <CardContent className="pt-0 text-xs text-muted-foreground">
                {metric.hint}
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>
            Filter CRM activities by search, status, type, and follow-up
            urgency.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 lg:grid-cols-[1.4fr_0.8fr_0.8fr_auto]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search subject, notes, account, opportunity, or lead"
            />
          </div>

          <Select
            value={status}
            onValueChange={(value) => {
              setPage(1);
              setStatus(value);
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder="Filter by status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {ACTIVITY_STATUS_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={activityType}
            onValueChange={(value) => {
              setPage(1);
              setActivityType(value);
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder="Filter by type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All types</SelectItem>
              {ACTIVITY_TYPE_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            variant={followUpOnly ? 'default' : 'outline'}
            onClick={() => {
              setPage(1);
              setFollowUpOnly((current) => !current);
            }}
          >
            {followUpOnly ? 'Showing follow-up only' : 'Follow-up only'}
          </Button>
        </CardContent>
      </Card>

      {scopedAccountName || scopedLeadName || scopedOpportunityName ? (
        <Card>
          <CardContent className="flex flex-wrap gap-2 py-4 text-sm">
            {scopedAccountName ? (
              <Badge variant="outline">Account: {scopedAccountName}</Badge>
            ) : null}
            {scopedLeadName ? (
              <Badge variant="outline">Lead: {scopedLeadName}</Badge>
            ) : null}
            {scopedOpportunityName ? (
              <Badge variant="outline">
                Opportunity: {scopedOpportunityName}
              </Badge>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Activity Queue</CardTitle>
            <CardDescription>
              {result
                ? `${result.totalCount} CRM activities matched`
                : 'Loading CRM activities'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {loading ? (
              <div className="py-16 text-center text-muted-foreground">
                Loading activities...
              </div>
            ) : null}

            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">
                No CRM activities matched the current filters.
              </div>
            ) : null}

            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Activity</TableHead>
                    <TableHead>Context</TableHead>
                    <TableHead>Due</TableHead>
                    <TableHead>Priority</TableHead>
                    <TableHead className="w-[120px] text-right">
                      Actions
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((activity) => (
                    <TableRow
                      key={activity.activityId}
                      className={
                        selectedActivityId === activity.activityId
                          ? 'bg-muted/40'
                          : ''
                      }
                      onClick={() => setSelectedActivityId(activity.activityId)}
                    >
                      <TableCell>
                        <div className="font-medium">{activity.subject}</div>
                        <div className="text-xs text-muted-foreground">
                          {activity.activityType} | {activity.activityStatus}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="text-sm">
                          {resolveContextLine(activity)}
                        </div>
                        <div className="mt-1 flex flex-wrap gap-2">
                          {activity.requiresFollowUp ? (
                            <Badge variant="secondary">Follow-Up</Badge>
                          ) : null}
                          {activity.isOverdue ? (
                            <Badge variant="destructive">Overdue</Badge>
                          ) : null}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>
                          {formatDate(
                            activity.dueDate || activity.nextFollowUpDate
                          )}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          Activity {formatDate(activity.activityDate)}
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant={getPriorityVariant(activity.priority)}>
                          {getPriorityLabel(activity.priority)}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={(event) => {
                              event.stopPropagation();
                              void openEditDialog(activity.activityId);
                            }}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={(event) => {
                              event.stopPropagation();
                              setSelectedActivityId(activity.activityId);
                              setDeleteOpen(true);
                            }}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : null}

            <div className="flex items-center justify-between gap-3">
              <div className="text-sm text-muted-foreground">
                Page {result?.page || 1} of{' '}
                {Math.max(result?.totalPages || 1, 1)}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  disabled={!result || result.page <= 1 || loading}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  disabled={
                    !result || result.page >= result.totalPages || loading
                  }
                  onClick={() => setPage((current) => current + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Activity Detail</CardTitle>
            <CardDescription>
              Inspect the full follow-up context and related CRM records for the
              selected activity.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? (
              <div className="py-16 text-center text-muted-foreground">
                Loading activity detail...
              </div>
            ) : null}

            {!detailLoading && !selectedActivity ? (
              <div className="py-16 text-center text-muted-foreground">
                Select an activity to inspect and manage it.
              </div>
            ) : null}

            {!detailLoading && selectedActivity ? (
              <>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="text-xl font-semibold">
                      {selectedActivity.subject}
                    </div>
                    <div className="text-sm text-muted-foreground">
                      {resolveContextLine(selectedActivity)}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant="outline">
                      {selectedActivity.activityType}
                    </Badge>
                    <Badge
                      variant={
                        selectedActivity.activityStatus === 'Completed'
                          ? 'default'
                          : 'outline'
                      }
                    >
                      {selectedActivity.activityStatus}
                    </Badge>
                    <Badge
                      variant={getPriorityVariant(selectedActivity.priority)}
                    >
                      {getPriorityLabel(selectedActivity.priority)}
                    </Badge>
                    {selectedActivity.requiresFollowUp ? (
                      <Badge variant="secondary">Needs Follow-Up</Badge>
                    ) : null}
                    {selectedActivity.isOverdue ? (
                      <Badge variant="destructive">Overdue</Badge>
                    ) : null}
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">
                      Schedule
                    </div>
                    <div className="mt-2 space-y-2">
                      <div>
                        Activity Date:{' '}
                        {formatDateTime(selectedActivity.activityDate)}
                      </div>
                      <div>
                        Due Date: {formatDate(selectedActivity.dueDate)}
                      </div>
                      <div>
                        Next Follow-Up:{' '}
                        {formatDate(selectedActivity.nextFollowUpDate)}
                      </div>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">
                      Execution
                    </div>
                    <div className="mt-2 space-y-2">
                      <div>
                        Duration:{' '}
                        {selectedActivity.duration
                          ? `${selectedActivity.duration} min`
                          : 'Not captured'}
                      </div>
                      <div>
                        Location: {selectedActivity.location || 'Not captured'}
                      </div>
                      <div>
                        Created: {formatDateTime(selectedActivity.createdAt)}
                      </div>
                    </div>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button
                    variant="outline"
                    onClick={() => void openEditDialog()}
                  >
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit Activity
                  </Button>
                  <Button variant="outline" onClick={() => setDeleteOpen(true)}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Delete
                  </Button>
                  {selectedActivity.businessPartnerId ? (
                    <Button asChild>
                      <Link
                        href={`/crm/accounts/${selectedActivity.businessPartnerId}`}
                      >
                        Open Account
                      </Link>
                    </Button>
                  ) : null}
                  {selectedActivity.leadId ? (
                    <Button asChild variant="outline">
                      <Link
                        href={`/crm/leads?leadId=${selectedActivity.leadId}`}
                      >
                        Open Lead
                      </Link>
                    </Button>
                  ) : null}
                  {selectedActivity.opportunityId ? (
                    <Button asChild variant="outline">
                      <Link
                        href={`/crm/opportunities?opportunityId=${selectedActivity.opportunityId}`}
                      >
                        Open Opportunity
                      </Link>
                    </Button>
                  ) : null}
                  {selectedActivity.propertyEnquiryTicketId ? (
                    <Button asChild variant="outline">
                      <Link
                        href={`/sales/property-enquiries?id=${selectedActivity.propertyEnquiryTicketId}`}
                      >
                        Open Property Enquiry
                      </Link>
                    </Button>
                  ) : null}
                </div>

                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Account</div>
                    <div className="text-muted-foreground">
                      {selectedActivity.businessPartnerName || 'Not linked'}
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Lead</div>
                    <div className="text-muted-foreground">
                      {selectedActivity.leadName || 'Not linked'}
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Opportunity</div>
                    <div className="text-muted-foreground">
                      {selectedActivity.opportunityName || 'Not linked'}
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Property Enquiry</div>
                    <div className="text-muted-foreground">
                      {selectedActivity.propertyEnquiryTicketNumber
                        ? `${selectedActivity.propertyEnquiryTicketNumber}${
                            selectedActivity.propertyEnquirySubject
                              ? ` - ${selectedActivity.propertyEnquirySubject}`
                              : ''
                          }`
                        : 'Not linked'}
                    </div>
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Description</div>
                    <div className="text-muted-foreground">
                      {selectedActivity.description ||
                        'No description provided.'}
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Outcome</div>
                    <div className="text-muted-foreground">
                      {selectedActivity.outcome || 'No outcome recorded.'}
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border p-4 text-sm">
                  <div className="mb-2 flex items-center gap-2 font-medium">
                    <Clock3 className="h-4 w-4" />
                    Notes
                  </div>
                  <div className="text-muted-foreground">
                    {selectedActivity.notes || 'No notes recorded.'}
                  </div>
                </div>

                {(selectedActivity.internalAttendees?.length ?? 0) ||
                selectedActivity.externalAttendees ||
                selectedActivity.attendees ? (
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 flex items-center gap-2 font-medium">
                      <Target className="h-4 w-4" />
                      Attendees
                    </div>
                    <div className="space-y-2 text-muted-foreground">
                      {selectedActivity.internalAttendees?.length ? (
                        <div>
                          <span className="font-medium text-foreground">
                            Employees:{' '}
                          </span>
                          {selectedActivity.internalAttendees
                            .map((attendee) => attendee.displayName)
                            .join(', ')}
                        </div>
                      ) : null}
                      {selectedActivity.externalAttendees ||
                      selectedActivity.attendees ? (
                        <div>
                          <span className="font-medium text-foreground">
                            External:{' '}
                          </span>
                          {selectedActivity.externalAttendees ||
                            selectedActivity.attendees}
                        </div>
                      ) : null}
                    </div>
                  </div>
                ) : null}
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <ActivityDialog
        open={formOpen}
        title={
          formMode === 'create' ? 'Create CRM Activity' : 'Edit CRM Activity'
        }
        description="Track follow-ups, meetings, calls, and touchpoints against CRM records or a Sales property enquiry."
        form={form}
        saving={saving}
        accounts={accounts}
        leads={leads}
        opportunities={opportunities}
        internalAttendees={internalAttendees}
        propertyEnquiries={propertyEnquiries}
        propertyEnquiryReadOnly={formMode === 'edit'}
        leadReadOnly={Boolean(scopedLeadId)}
        leadName={scopedLeadName}
        onOpenChange={setFormOpen}
        onSubmit={() => void submitActivity()}
        onChange={(field, value) =>
          setForm((current) => ({ ...current, [field]: value }))
        }
        onInternalAttendeesChange={(attendees) => {
          setInternalAttendees(attendees);
          setForm((current) => ({
            ...current,
            internalAttendeeEmployeeIds: attendees.map(
              (attendee) => attendee.employeeId
            ),
          }));
        }}
      />

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete CRM Activity</DialogTitle>
            <DialogDescription>
              This removes the activity record from the CRM queue.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => void deleteActivity()}
              disabled={saving}
            >
              {saving ? 'Deleting...' : 'Delete Activity'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
