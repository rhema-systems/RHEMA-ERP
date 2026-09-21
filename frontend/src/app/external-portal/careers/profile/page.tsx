'use client';

import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, MailWarning, Paperclip, Plus, Save, Trash2 } from 'lucide-react';
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
import { CurrencyPicker } from '@/components/hr/common/CurrencyPicker';
import { PhotoPanel } from '@/components/hr/common/PhotoDialog';
import { AddressFields } from '@/components/reference/AddressFields';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { candidateService, publicCareersService } from '@/services/hr/careers.service';
import {
  EMPTY_GUID,
  LANGUAGE_PROFICIENCIES,
  type CandidateDocument,
  type CandidateProfile,
  type SaveCandidateProfilePayload,
} from '@/types/hr/careers';
import {
  GENDERS,
  PREFERRED_WORK_ARRANGEMENTS,
  PROFICIENCY_LEVELS,
  QUALIFICATION_TYPES,
  WORK_AUTHORIZATION_STATUSES,
} from '@/types/hr/recruitment-pipeline';

// The whole profile is one replace-set save: every child list is sent complete, and a row left
// out is deleted server-side. The editors below therefore work on local state and nothing is
// persisted until Save. Documents are the exception — each upload is its own call through the
// scanned gate, and the list here is read back from the profile.

type Row = Record<string, any>;
type Option = { value: string; label: string };

interface FieldSpec {
  name: string;
  label: string;
  type: 'text' | 'date' | 'number' | 'checkbox' | 'select' | 'textarea';
  /** Fixed enum members (humanised), or a list computed from the draft — a dependent dropdown. */
  options?: readonly string[] | ((draft: Row) => Option[]);
  required?: boolean;
  /** Shown only when this answers true for the draft (a certificate number under the tick). */
  visibleWhen?: (draft: Row) => boolean;
  /** Runs after the field changes and may rewrite the draft (mirror a catalogue name, clear a pick). */
  onChange?: (draft: Row, value: any) => Row;
  /** A blank choice at the top of a select, mapped to ''. */
  emptyLabel?: string;
}

const NONE = '__none__';

function CollectionEditor({
  title,
  hint,
  rows,
  onChange,
  fields,
  summarize,
  rowExtra,
}: {
  title: string;
  hint?: string;
  rows: Row[];
  onChange: (rows: Row[]) => void;
  fields: FieldSpec[];
  summarize: (row: Row) => string;
  /** Rendered under a saved row (id != EMPTY_GUID) — the referee's reference letter. */
  rowExtra?: (row: Row) => ReactNode;
}) {
  const blank = () =>
    Object.fromEntries([
      ['id', EMPTY_GUID],
      ...fields.map((f) => [f.name, f.type === 'checkbox' ? false : '']),
    ]);
  const [editing, setEditing] = useState<{ index: number | null; draft: Row } | null>(null);

  const commit = () => {
    if (!editing) return;
    const next = [...rows];
    if (editing.index === null) next.push(editing.draft);
    else next[editing.index] = editing.draft;
    onChange(next);
    setEditing(null);
  };

  const visible = (f: FieldSpec, draft: Row) => !f.visibleWhen || f.visibleWhen(draft);
  const missingRequired = editing
    ? fields.some(
        (f) => visible(f, editing.draft) && f.required && !String(editing.draft[f.name] ?? '').trim(),
      )
    : false;

  const setField = (f: FieldSpec, value: any) =>
    setEditing((e) => {
      if (!e) return e;
      const draft = { ...e.draft, [f.name]: value };
      return { ...e, draft: f.onChange ? f.onChange(draft, value) : draft };
    });

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">{title}</CardTitle>
          {hint && <CardDescription>{hint}</CardDescription>}
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setEditing({ index: null, draft: blank() })}
        >
          <Plus className="mr-1.5 h-4 w-4" />
          Add
        </Button>
      </CardHeader>
      <CardContent className="space-y-2">
        {rows.length === 0 && <p className="text-sm text-muted-foreground">Nothing added yet.</p>}
        {rows.map((row, index) => (
          <div key={index} className="rounded-md border px-3 py-2">
            <div className="flex items-center justify-between gap-3">
              <button
                type="button"
                className="flex-1 text-left text-sm hover:underline"
                onClick={() => setEditing({ index, draft: { ...row } })}
              >
                {summarize(row) || '—'}
              </button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label="Remove"
                onClick={() => onChange(rows.filter((_, i) => i !== index))}
              >
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
            {rowExtra && row.id && row.id !== EMPTY_GUID && rowExtra(row)}
          </div>
        ))}

        {editing && (
          <div className="space-y-3 rounded-lg border bg-muted/30 p-4">
            <div className="grid gap-3 sm:grid-cols-2">
              {fields
                .filter((f) => visible(f, editing.draft))
                .map((f) => (
                  <div key={f.name} className={f.type === 'textarea' ? 'sm:col-span-2' : ''}>
                    <Label className="text-xs">
                      {f.label}
                      {f.required && <span className="ml-0.5 text-red-500">*</span>}
                    </Label>
                    {f.type === 'select' ? (
                      <Select
                        value={editing.draft[f.name] || (f.emptyLabel ? NONE : '')}
                        onValueChange={(v) => setField(f, v === NONE ? '' : v)}
                      >
                        <SelectTrigger className="mt-1">
                          <SelectValue placeholder="Choose…" />
                        </SelectTrigger>
                        <SelectContent>
                          {f.emptyLabel && <SelectItem value={NONE}>{f.emptyLabel}</SelectItem>}
                          {(typeof f.options === 'function'
                            ? f.options(editing.draft)
                            : (f.options ?? []).map((o) => ({ value: o, label: humanizeEnum(o) }))
                          ).map((o) => (
                            <SelectItem key={o.value} value={o.value}>
                              {o.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    ) : f.type === 'checkbox' ? (
                      <label className="mt-2 flex items-center gap-2 text-sm">
                        <Checkbox
                          checked={editing.draft[f.name] === true}
                          onCheckedChange={(v) => setField(f, v === true)}
                        />
                        Yes
                      </label>
                    ) : f.type === 'textarea' ? (
                      <Textarea
                        className="mt-1"
                        value={editing.draft[f.name] ?? ''}
                        onChange={(e) => setField(f, e.target.value)}
                      />
                    ) : (
                      <Input
                        className="mt-1"
                        type={f.type}
                        value={editing.draft[f.name] ?? ''}
                        onChange={(e) => setField(f, e.target.value)}
                      />
                    )}
                  </div>
                ))}
            </div>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="ghost" size="sm" onClick={() => setEditing(null)}>
                Cancel
              </Button>
              <Button type="button" size="sm" disabled={missingRequired} onClick={commit}>
                {editing.index === null ? 'Add' : 'Update'}
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/** The description an ID scan carries (the HR panel writes the same shape and reads it back). */
const idScanDescription = (label: string | null) => (label ? `ID document — ${label}` : 'ID document');
/** The description a reference letter carries — the tie to the referee, this round. */
const referenceLetterDescription = (refereeName: string) => `Reference letter from ${refereeName}`;

/**
 * One upload button through the scanned gate for a fixed document type, plus the rows already on
 * file for it. Used for the ID scan (under the identity document) and each referee's letter.
 */
function AttachedDocuments({
  label,
  documentType,
  description,
  existing,
  onUploaded,
  compact,
}: {
  label: string;
  documentType: 'IdDocument' | 'ReferenceLetter';
  description: string;
  existing: CandidateDocument[];
  onUploaded: () => Promise<unknown>;
  compact?: boolean;
}) {
  const { toast } = useToast();
  const input = useRef<HTMLInputElement>(null);
  const upload = useMutation({
    mutationFn: (file: File) => candidateService.uploadDocument(file, documentType, description),
    onSuccess: async () => {
      await onUploaded();
      toast({ title: `${label} attached` });
    },
    onError: (e: any) => toast({ title: 'Could not upload', description: e?.message, variant: 'destructive' }),
  });
  const download = async (d: CandidateDocument) => {
    try {
      const blob = await candidateService.downloadDocument(d.id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = d.fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e: any) {
      toast({ title: 'Could not download', description: e?.message, variant: 'destructive' });
    }
  };
  return (
    <div className={compact ? 'mt-2 space-y-1 border-t pt-2' : 'space-y-2'}>
      {existing.map((d) => (
        <div key={d.id} className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
          <span className="truncate">
            {d.fileName} · {formatDate(d.uploadDate)}
          </span>
          <Button type="button" variant="ghost" size="icon" className="h-7 w-7" aria-label={`Download ${d.fileName}`} onClick={() => download(d)}>
            <Download className="h-3.5 w-3.5" />
          </Button>
        </div>
      ))}
      <input
        ref={input}
        type="file"
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) upload.mutate(file);
          e.target.value = '';
        }}
      />
      <Button type="button" variant="outline" size="sm" disabled={upload.isPending} onClick={() => input.current?.click()}>
        {upload.isPending ? <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" /> : <Paperclip className="mr-1.5 h-3.5 w-3.5" />}
        {existing.length > 0 ? `Attach another ${label.toLowerCase()}` : `Attach ${label.toLowerCase()}`}
      </Button>
    </div>
  );
}

export default function CandidateProfilePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const profileQuery = useQuery({
    queryKey: ['candidate', 'profile'],
    queryFn: () => candidateService.getProfile(),
  });

  const tenant = useQuery({
    queryKey: ['careers', 'tenant'],
    queryFn: () => publicCareersService.resolveTenant(),
    staleTime: Infinity,
  });
  const tenantId = tenant.data?.id ?? '';

  const countries = useQuery({
    queryKey: ['careers', 'countries', tenantId],
    queryFn: async () => {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || '/api'}/public/countries`,
        { headers: { 'X-Tenant-Id': tenantId } },
      );
      return (await res.json()) as { id: string; name: string }[];
    },
    enabled: !!tenantId,
  });

  // ⚠ The anonymous catalogues, never the HR-gated ones — a candidate token gets 403 there and
  // every picker would render empty for exactly its users (round 3, lanes C1/C2).
  const idTypes = useQuery({
    queryKey: ['careers', 'catalogue', 'identification-types', tenantId],
    queryFn: () => publicCareersService.getCatalogueIdentificationTypes(tenantId),
    enabled: !!tenantId,
    staleTime: 5 * 60 * 1000,
  });
  const qualificationCatalogue = useQuery({
    queryKey: ['careers', 'catalogue', 'qualifications', tenantId],
    queryFn: () => publicCareersService.getCatalogueQualifications(tenantId),
    enabled: !!tenantId,
    staleTime: 5 * 60 * 1000,
  });
  const skillCatalogue = useQuery({
    queryKey: ['careers', 'catalogue', 'skills', tenantId],
    queryFn: () => publicCareersService.getCatalogueSkills(tenantId),
    enabled: !!tenantId,
    staleTime: 5 * 60 * 1000,
  });
  const languageCatalogue = useQuery({
    queryKey: ['careers', 'catalogue', 'languages', tenantId],
    queryFn: () => publicCareersService.getCatalogueLanguages(tenantId),
    enabled: !!tenantId,
    staleTime: 5 * 60 * 1000,
  });

  const [form, setForm] = useState<Row | null>(null);
  const [children, setChildren] = useState<{
    workHistories: Row[];
    qualifications: Row[];
    referees: Row[];
    skills: Row[];
    languages: Row[];
    interests: Row[];
  }>({ workHistories: [], qualifications: [], referees: [], skills: [], languages: [], interests: [] });

  useEffect(() => {
    const p = profileQuery.data;
    if (!p || form) return;
    setForm({
      firstName: p.firstName ?? '',
      middleName: p.middleName ?? '',
      lastName: p.lastName ?? '',
      phone: p.phone ?? '',
      alternatePhone: p.alternatePhone ?? '',
      dateOfBirth: p.dateOfBirth ? p.dateOfBirth.slice(0, 10) : '',
      gender: p.gender ?? '',
      city: p.city ?? '',
      // Round 4, lane A — seeded so the cascade re-opens where the candidate actually is.
      geoAreaId: p.geoAreaId ?? '',
      countryId: p.countryId ?? '',
      postalAddress: p.postalAddress ?? '',
      digitalAddress: p.digitalAddress ?? '',
      linkedInProfile: p.linkedInProfile ?? '',
      portfolioUrl: p.portfolioUrl ?? '',
      gitHubUrl: p.gitHubUrl ?? '',
      headline: p.headline ?? '',
      professionalSummary: p.professionalSummary ?? '',
      currentJobTitle: p.currentJobTitle ?? '',
      currentEmployer: p.currentEmployer ?? '',
      totalYearsExperience: p.totalYearsExperience ?? '',
      noticePeriodDays: p.noticePeriodDays ?? '',
      availableFrom: p.availableFrom ? p.availableFrom.slice(0, 10) : '',
      preferredWorkArrangement: p.preferredWorkArrangement ?? 'Any',
      expectedSalaryMin: p.expectedSalaryMin ?? '',
      expectedSalaryMax: p.expectedSalaryMax ?? '',
      expectedSalaryCurrency: p.expectedSalaryCurrency ?? '',
      workAuthorizationStatus: p.workAuthorizationStatus ?? 'NotSpecified',
      nationalIdTypeId: p.nationalIdTypeId ?? '',
      nationalIdNumber: p.nationalIdNumber ?? '',
      nationalIdExpiryDate: p.nationalIdExpiryDate ? p.nationalIdExpiryDate.slice(0, 10) : '',
      isInTalentPool: p.isInTalentPool ?? false,
    });
    setChildren({
      workHistories: p.workHistories.map((w) => ({
        id: w.id,
        institutionName: w.institutionName,
        positionHeld: w.positionHeld,
        startDate: w.startDate?.slice(0, 10) ?? '',
        endDate: w.endDate?.slice(0, 10) ?? '',
        responsibilities: w.responsibilities ?? '',
        reasonForLeaving: w.reasonForLeaving ?? '',
      })),
      qualifications: p.qualifications.map((q) => ({
        id: q.id,
        qualificationType: q.qualificationType,
        qualificationId: q.qualificationId ?? '',
        qualificationName: q.qualificationName,
        institution: q.institution,
        dateAwarded: q.dateAwarded?.slice(0, 10) ?? '',
        grade: q.grade ?? '',
      })),
      referees: p.referees.map((r) => ({
        id: r.id,
        fullName: r.fullName,
        position: r.position,
        organization: r.organization,
        email: r.email,
        phone: r.phone,
        relationship: r.relationship ?? '',
        yearsKnown: r.yearsKnown ?? '',
      })),
      skills: p.skills.map((s) => ({
        id: s.id,
        skillId: s.skillId ?? '',
        skillName: s.skillName,
        proficiency: s.proficiency ?? '',
        yearsOfExperience: s.yearsOfExperience ?? '',
        isCertified: s.isCertified,
        certificationName: s.certificationName ?? '',
        certificationNumber: s.certificationNumber ?? '',
        certifyingBody: s.certifyingBody ?? '',
        certificationExpiryDate: s.certificationExpiryDate?.slice(0, 10) ?? '',
      })),
      languages: p.languages.map((l) => ({
        id: l.id,
        languageId: l.languageId ?? '',
        languageName: l.languageName ?? '',
        proficiency: l.proficiency,
      })),
      interests: p.interests.map((i) => ({ id: i.id, detail: i.detail })),
    });
  }, [profileQuery.data, form]);

  const save = useMutation({
    mutationFn: () => {
      const f = form as Row;
      const num = (v: any) => (v === '' || v == null ? null : Number(v));
      const str = (v: any) => (String(v ?? '').trim() === '' ? null : String(v).trim());
      const payload: SaveCandidateProfilePayload = {
        firstName: f.firstName,
        middleName: str(f.middleName),
        lastName: f.lastName,
        phone: f.phone,
        alternatePhone: str(f.alternatePhone),
        dateOfBirth: str(f.dateOfBirth),
        gender: f.gender || null,
        city: str(f.city),
        // '' does not bind to a Guid? — it is a 400 before the service runs. Null genuinely clears
        // the area; this payload replaces the address wholesale.
        geoAreaId: f.geoAreaId || null,
        countryId: f.countryId || null,
        postalAddress: str(f.postalAddress),
        digitalAddress: str(f.digitalAddress),
        linkedInProfile: str(f.linkedInProfile),
        portfolioUrl: str(f.portfolioUrl),
        gitHubUrl: str(f.gitHubUrl),
        headline: str(f.headline),
        professionalSummary: str(f.professionalSummary),
        currentJobTitle: str(f.currentJobTitle),
        currentEmployer: str(f.currentEmployer),
        totalYearsExperience: num(f.totalYearsExperience),
        noticePeriodDays: num(f.noticePeriodDays),
        availableFrom: str(f.availableFrom),
        preferredWorkArrangement: f.preferredWorkArrangement,
        expectedSalaryMin: num(f.expectedSalaryMin),
        expectedSalaryMax: num(f.expectedSalaryMax),
        expectedSalaryCurrency: str(f.expectedSalaryCurrency),
        workAuthorizationStatus: f.workAuthorizationStatus,
        nationalIdTypeId: f.nationalIdTypeId || null,
        nationalIdNumber: str(f.nationalIdNumber),
        nationalIdExpiryDate: str(f.nationalIdExpiryDate),
        isInTalentPool: f.isInTalentPool === true,
        workHistories: children.workHistories.map((w) => ({
          id: w.id,
          institutionName: w.institutionName,
          positionHeld: w.positionHeld,
          startDate: w.startDate,
          endDate: str(w.endDate),
          responsibilities: str(w.responsibilities),
          reasonForLeaving: str(w.reasonForLeaving),
        })),
        qualifications: children.qualifications.map((q) => ({
          id: q.id,
          qualificationType: q.qualificationType,
          // The server mirrors the catalogue name over anything sent when an id is present; the
          // typed name only counts when there is none.
          qualificationId: q.qualificationId || null,
          qualificationName: q.qualificationName,
          institution: q.institution,
          dateAwarded: q.dateAwarded,
          grade: str(q.grade),
        })),
        referees: children.referees.map((r) => ({
          id: r.id,
          fullName: r.fullName,
          position: r.position,
          organization: r.organization,
          email: r.email,
          phone: r.phone,
          relationship: r.relationship || '',
          yearsKnown: num(r.yearsKnown),
        })),
        skills: children.skills.map((s) => ({
          id: s.id,
          skillId: s.skillId || null,
          skillName: s.skillName,
          proficiency: s.proficiency || null,
          yearsOfExperience: num(s.yearsOfExperience),
          isCertified: s.isCertified === true,
          certificationName: s.isCertified ? str(s.certificationName) : null,
          certificationNumber: s.isCertified ? str(s.certificationNumber) : null,
          certifyingBody: s.isCertified ? str(s.certifyingBody) : null,
          certificationExpiryDate: s.isCertified ? str(s.certificationExpiryDate) : null,
        })),
        languages: children.languages.map((l) => ({
          id: l.id,
          languageId: l.languageId || null,
          languageName: l.languageId ? null : str(l.languageName),
          proficiency: l.proficiency || 'ProfessionalWorking',
        })),
        interests: children.interests.map((i) => ({ id: i.id, detail: i.detail })),
      };
      return candidateService.saveProfile(payload);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['candidate'] });
      // Re-seed the editors from the saved profile so new rows pick up their ids (a referee only
      // gets its letter button once it has one).
      setForm(null);
      toast({ title: 'Profile saved' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not save the profile', description: e?.message, variant: 'destructive' }),
  });

  const refreshProfile = () => queryClient.invalidateQueries({ queryKey: ['candidate', 'profile'] });

  if (profileQuery.isLoading || !form) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const p: CandidateProfile | undefined = profileQuery.data;
  const set = (name: string, value: any) => setForm((f) => (f ? { ...f, [name]: value } : f));
  const text = (name: string, label: string, required = false, type = 'text') => (
    <div className="space-y-1.5">
      <Label htmlFor={`pf-${name}`}>
        {label}
        {required && <span className="ml-0.5 text-red-500">*</span>}
      </Label>
      <Input id={`pf-${name}`} type={type} value={form[name] ?? ''} onChange={(e) => set(name, e.target.value)} />
    </div>
  );
  const select = (name: string, label: string, options: readonly string[], allowEmpty = false) => (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <Select value={form[name] || (allowEmpty ? '' : options[0])} onValueChange={(v) => set(name, v)}>
        <SelectTrigger>
          <SelectValue placeholder="Choose…" />
        </SelectTrigger>
        <SelectContent>
          {options.map((o) => (
            <SelectItem key={o} value={o}>
              {humanizeEnum(o)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );

  const documents = p?.documents ?? [];
  const idScans = documents.filter((d) => d.documentType === 'IdDocument');
  const idTypeName = (idTypes.data ?? []).find((t) => t.id === form.nationalIdTypeId)?.name ?? p?.nationalIdTypeName ?? null;
  const identityLabel = [idTypeName, form.nationalIdNumber].filter(Boolean).join(' ') || null;
  const qualifications = qualificationCatalogue.data ?? [];
  const skills = skillCatalogue.data ?? [];
  const languages = languageCatalogue.data ?? [];

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">My candidate profile</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            This profile travels with every application you submit. Save applies everything at once.
          </p>
        </div>
        <Button disabled={save.isPending || !form.firstName || !form.lastName || !form.phone} onClick={() => save.mutate()}>
          {save.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Save profile
        </Button>
      </div>

      {p && !p.isEmailVerified && (
        <Card>
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
            <div className="flex items-center gap-3 text-sm">
              <MailWarning className="h-5 w-5 text-amber-600" />
              <span>
                <span className="font-medium">Email not confirmed.</span>{' '}
                <span className="text-muted-foreground">
                  If a candidate record with your address already exists, confirming your email is
                  what lets this account claim it.
                </span>
              </span>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={async () => {
                try {
                  const r = await candidateService.sendEmailConfirmation();
                  toast({ title: r.message });
                } catch (e: any) {
                  toast({ title: 'Could not send', description: e?.message, variant: 'destructive' });
                }
              }}
            >
              Send confirmation link
            </Button>
          </CardContent>
        </Card>
      )}

      {/* The photograph (round 3, lane C2; register row R-2). Private: fetched through the gate
          with the bearer token, never an <img src>. Needs a saved profile — the upload writes onto
          the candidate row. */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Photograph</CardTitle>
          <CardDescription>
            {p?.candidateId
              ? 'Recruiters see it beside your name. A clear head-and-shoulders picture works best.'
              : 'Save your profile once, then you can add a photograph.'}
          </CardDescription>
        </CardHeader>
        {p?.candidateId && (
          <CardContent>
            <PhotoPanel
              endpoint={candidateService.photoUrl()}
              hasPhoto={!!p.hasPhoto}
              upload={(file) => candidateService.uploadPhoto(file)}
              onUploaded={() => void refreshProfile()}
              subjectLabel={`${p.firstName} ${p.lastName}`.trim() || 'Your photograph'}
            />
          </CardContent>
        )}
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">About you</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {text('firstName', 'First name', true)}
          {text('middleName', 'Middle name')}
          {text('lastName', 'Last name', true)}
          {text('phone', 'Phone', true)}
          {text('alternatePhone', 'Alternate phone')}
          {text('dateOfBirth', 'Date of birth', false, 'date')}
          {select('gender', 'Gender', GENDERS, true)}
          {/* Round 4, lane A. Country and City were two unrelated boxes; they are now the shared
              cascade, in PUBLIC mode — api/reference/geo is InternalOnly, which blocks the
              Candidate role, so this reads the anonymous careers catalogue instead. Where the
              chosen country has no scheme loaded (most of them), it falls back to the plain City
              box below and nothing changes for the candidate. */}
          <div className="sm:col-span-2">
            <AddressFields
              countryId={form.countryId || ''}
              onCountryChange={(v) => {
                set('countryId', v);
                set('geoAreaId', '');
              }}
              geoAreaId={form.geoAreaId || ''}
              onGeoAreaChange={(v) => set('geoAreaId', v)}
              publicTenantId={tenantId}
              countryOptions={countries.data ?? []}
              fallback={(schemeLoaded) => (
                <div className="mt-4 grid gap-4 sm:grid-cols-2">
                  <div className="space-y-1.5">
                    <Label htmlFor="city">City / Town</Label>
                    <Input
                      id="city"
                      value={form.city ?? ''}
                      disabled={schemeLoaded}
                      onChange={(e) => set('city', e.target.value)}
                    />
                    {schemeLoaded && (
                      <p className="text-xs text-muted-foreground">Set from the address above</p>
                    )}
                  </div>
                </div>
              )}
            />
          </div>
          {text('postalAddress', 'Postal address')}
          {text('digitalAddress', 'Digital address')}
          {text('linkedInProfile', 'LinkedIn URL')}
          {text('portfolioUrl', 'Portfolio URL')}
          {text('gitHubUrl', 'GitHub URL')}
        </CardContent>
      </Card>

      {/* The national-ID trio (round 3, lane C1; register row R-3a) and its scan (decision D-17).
          The type list is the employer's — "Ghana Card" is a row in it, not a field. */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Identity document</CardTitle>
          <CardDescription>
            The document you will be asked to show at hire. It becomes your first identification
            record when you join.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div className="space-y-1.5">
              <Label>Document type</Label>
              <Select
                value={form.nationalIdTypeId || NONE}
                onValueChange={(v) => set('nationalIdTypeId', v === NONE ? '' : v)}
              >
                <SelectTrigger id="pf-nationalIdTypeId">
                  <SelectValue placeholder="Choose…" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Not provided</SelectItem>
                  {(idTypes.data ?? []).map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {text('nationalIdNumber', 'Document number')}
            {text('nationalIdExpiryDate', 'Expiry date', false, 'date')}
          </div>
          {p?.candidateId ? (
            <AttachedDocuments
              label="ID scan"
              documentType="IdDocument"
              description={idScanDescription(identityLabel)}
              existing={idScans}
              onUploaded={refreshProfile}
            />
          ) : (
            <p className="text-xs text-muted-foreground">Save your profile once, then attach a scan of the document.</p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Professional profile</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {text('headline', 'Headline')}
          {text('currentJobTitle', 'Current role')}
          {text('currentEmployer', 'Current employer')}
          {text('totalYearsExperience', 'Total years of experience', false, 'number')}
          {text('noticePeriodDays', 'Notice period (days)', false, 'number')}
          {text('availableFrom', 'Available from', false, 'date')}
          {select('preferredWorkArrangement', 'Preferred work arrangement', PREFERRED_WORK_ARRANGEMENTS)}
          {select('workAuthorizationStatus', 'Work authorisation', WORK_AUTHORIZATION_STATUSES)}
          <div className="space-y-1.5 sm:col-span-2 lg:col-span-3">
            <Label htmlFor="pf-summary">Professional summary</Label>
            <Textarea
              id="pf-summary"
              rows={4}
              maxLength={4000}
              value={form.professionalSummary ?? ''}
              onChange={(e) => set('professionalSummary', e.target.value)}
            />
          </div>
          {text('expectedSalaryMin', 'Expected salary (min)', false, 'number')}
          {text('expectedSalaryMax', 'Expected salary (max)', false, 'number')}
          <div className="space-y-1.5">
            <Label htmlFor="pf-expectedSalaryCurrency">Salary currency</Label>
            {/* Finance's list through the anonymous catalogue (round 3, lane C2; register row R-3b). */}
            <CurrencyPicker
              id="pf-expectedSalaryCurrency"
              value={form.expectedSalaryCurrency || ''}
              onChange={(code) => set('expectedSalaryCurrency', code)}
              publicTenantId={tenantId || null}
              allowEmpty
              emptyLabel="Not stated"
            />
          </div>
          <label className="flex items-center gap-2 text-sm sm:col-span-2 lg:col-span-3">
            <Checkbox
              checked={form.isInTalentPool === true}
              onCheckedChange={(v) => set('isInTalentPool', v === true)}
            />
            Keep me in the talent pool for future opportunities
          </label>
        </CardContent>
      </Card>

      <CollectionEditor
        title="Work history"
        rows={children.workHistories}
        onChange={(rows) => setChildren((c) => ({ ...c, workHistories: rows }))}
        summarize={(w) => [w.positionHeld, w.institutionName].filter(Boolean).join(' · ')}
        fields={[
          { name: 'institutionName', label: 'Employer', type: 'text', required: true },
          { name: 'positionHeld', label: 'Position held', type: 'text', required: true },
          { name: 'startDate', label: 'Start date', type: 'date', required: true },
          { name: 'endDate', label: 'End date (blank = current)', type: 'date' },
          { name: 'responsibilities', label: 'Responsibilities', type: 'textarea' },
          { name: 'reasonForLeaving', label: 'Reason for leaving', type: 'text' },
        ]}
      />

      {/* Register row R-3c: the type narrows the employer's qualification list; the free-text name
          is asked for only when nothing listed fits. */}
      <CollectionEditor
        title="Qualifications"
        hint="Choose the kind first, then pick from the list. Name the award only if it is not listed."
        rows={children.qualifications}
        onChange={(rows) => setChildren((c) => ({ ...c, qualifications: rows }))}
        summarize={(q) => [q.qualificationName, q.institution].filter(Boolean).join(' · ')}
        fields={[
          {
            name: 'qualificationType',
            label: 'Type',
            type: 'select',
            options: QUALIFICATION_TYPES,
            required: true,
            // A pick made under another kind is dropped when the kind changes.
            onChange: (draft) => {
              const stillOffered = qualifications.some((q) => q.id === draft.qualificationId && q.type === draft.qualificationType);
              return stillOffered ? draft : { ...draft, qualificationId: '', qualificationName: draft.qualificationId ? '' : draft.qualificationName };
            },
          },
          {
            name: 'qualificationId',
            label: 'Qualification',
            type: 'select',
            emptyLabel: 'Not listed',
            options: (draft) => {
              const typed = qualifications.filter((q) => q.type === draft.qualificationType);
              return (typed.length > 0 ? typed : qualifications).map((q) => ({ value: q.id, label: q.name }));
            },
            onChange: (draft, value) => {
              const picked = qualifications.find((q) => q.id === value);
              return { ...draft, qualificationName: picked ? picked.name : '' };
            },
          },
          {
            name: 'qualificationName',
            label: 'Name of the qualification',
            type: 'text',
            required: true,
            visibleWhen: (draft) => !draft.qualificationId,
          },
          { name: 'institution', label: 'Institution', type: 'text', required: true },
          { name: 'dateAwarded', label: 'Date awarded', type: 'date', required: true },
          { name: 'grade', label: 'Grade', type: 'text' },
        ]}
      />

      {/* Register row R-3d: the skill from the employer's list, and the certificate details only
          once "Certified" is ticked. */}
      <CollectionEditor
        title="Skills"
        hint="Pick from the list, or name the skill if it is not listed. Certificate details appear once you tick Certified."
        rows={children.skills}
        onChange={(rows) => setChildren((c) => ({ ...c, skills: rows }))}
        summarize={(s) =>
          [s.skillName, s.proficiency && humanizeEnum(s.proficiency), s.isCertified && s.certificationName ? `cert. ${s.certificationName}` : null]
            .filter(Boolean)
            .join(' · ')
        }
        fields={[
          {
            name: 'skillId',
            label: 'Skill',
            type: 'select',
            emptyLabel: 'Not listed',
            options: () => skills.map((s) => ({ value: s.id, label: s.name })),
            onChange: (draft, value) => {
              const picked = skills.find((s) => s.id === value);
              return { ...draft, skillName: picked ? picked.name : '' };
            },
          },
          {
            name: 'skillName',
            label: 'Skill name',
            type: 'text',
            required: true,
            visibleWhen: (draft) => !draft.skillId,
          },
          { name: 'proficiency', label: 'Proficiency', type: 'select', options: PROFICIENCY_LEVELS },
          { name: 'yearsOfExperience', label: 'Years of experience', type: 'number' },
          { name: 'isCertified', label: 'Certified', type: 'checkbox' },
          { name: 'certificationName', label: 'Certification name', type: 'text', required: true, visibleWhen: (d) => d.isCertified === true },
          { name: 'certificationNumber', label: 'Certificate number', type: 'text', visibleWhen: (d) => d.isCertified === true },
          { name: 'certifyingBody', label: 'Certifying body', type: 'text', visibleWhen: (d) => d.isCertified === true },
          { name: 'certificationExpiryDate', label: 'Certificate expires', type: 'date', visibleWhen: (d) => d.isCertified === true },
        ]}
      />

      {/* Register row R-3e: the language from the employer's catalogue, or typed when not listed. */}
      <CollectionEditor
        title="Languages"
        hint="Pick from the list, or type the language if it is not listed."
        rows={children.languages}
        onChange={(rows) => setChildren((c) => ({ ...c, languages: rows }))}
        summarize={(l) => [l.languageName, l.proficiency && humanizeEnum(l.proficiency)].filter(Boolean).join(' · ')}
        fields={[
          {
            name: 'languageId',
            label: 'Language',
            type: 'select',
            emptyLabel: 'Not listed',
            options: () => languages.map((l) => ({ value: l.id, label: l.name })),
            onChange: (draft, value) => {
              const picked = languages.find((l) => l.id === value);
              return { ...draft, languageName: picked ? picked.name : '' };
            },
          },
          {
            name: 'languageName',
            label: 'Language name',
            type: 'text',
            required: true,
            visibleWhen: (draft) => !draft.languageId,
          },
          { name: 'proficiency', label: 'Proficiency', type: 'select', options: LANGUAGE_PROFICIENCIES, required: true },
        ]}
      />

      {/* Decision D-17: a reference letter is attached to the referee it comes from, once the
          referee has been saved (the letter needs the referee's name on it). */}
      <CollectionEditor
        title="Referees"
        hint="People we may contact about you, with their permission. Save the profile, then attach each referee's letter under their name."
        rows={children.referees}
        onChange={(rows) => setChildren((c) => ({ ...c, referees: rows }))}
        summarize={(r) => [r.fullName, r.organization].filter(Boolean).join(' · ')}
        fields={[
          { name: 'fullName', label: 'Full name', type: 'text', required: true },
          { name: 'position', label: 'Position', type: 'text', required: true },
          { name: 'organization', label: 'Organisation', type: 'text', required: true },
          { name: 'email', label: 'Email', type: 'text', required: true },
          { name: 'phone', label: 'Phone', type: 'text', required: true },
          { name: 'relationship', label: 'Relationship', type: 'text' },
          { name: 'yearsKnown', label: 'Years known', type: 'number' },
        ]}
        rowExtra={(r) => (
          <AttachedDocuments
            compact
            label="Reference letter"
            documentType="ReferenceLetter"
            description={referenceLetterDescription(r.fullName)}
            existing={documents.filter(
              (d) => d.documentType === 'ReferenceLetter' && d.description === referenceLetterDescription(r.fullName),
            )}
            onUploaded={refreshProfile}
          />
        )}
      />

      <CollectionEditor
        title="Interests"
        rows={children.interests}
        onChange={(rows) => setChildren((c) => ({ ...c, interests: rows }))}
        summarize={(i) => i.detail}
        fields={[{ name: 'detail', label: 'Interest', type: 'text', required: true }]}
      />

      <div className="flex justify-end">
        <Button disabled={save.isPending || !form.firstName || !form.lastName || !form.phone} onClick={() => save.mutate()}>
          {save.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Save profile
        </Button>
      </div>
    </div>
  );
}
