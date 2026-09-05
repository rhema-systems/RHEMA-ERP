'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, MailWarning, Plus, Save, Trash2 } from 'lucide-react';
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
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { candidateService, publicCareersService } from '@/services/hr/careers.service';
import {
  EMPTY_GUID,
  LANGUAGE_PROFICIENCIES,
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
// persisted until Save.

type Row = Record<string, any>;

interface FieldSpec {
  name: string;
  label: string;
  type: 'text' | 'date' | 'number' | 'checkbox' | 'select' | 'textarea';
  options?: readonly string[];
  required?: boolean;
}

function CollectionEditor({
  title,
  hint,
  rows,
  onChange,
  fields,
  summarize,
}: {
  title: string;
  hint?: string;
  rows: Row[];
  onChange: (rows: Row[]) => void;
  fields: FieldSpec[];
  summarize: (row: Row) => string;
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

  const missingRequired = editing
    ? fields.some((f) => f.required && !String(editing.draft[f.name] ?? '').trim())
    : false;

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
          <div key={index} className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
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
        ))}

        {editing && (
          <div className="space-y-3 rounded-lg border bg-muted/30 p-4">
            <div className="grid gap-3 sm:grid-cols-2">
              {fields.map((f) => (
                <div key={f.name} className={f.type === 'textarea' ? 'sm:col-span-2' : ''}>
                  <Label className="text-xs">
                    {f.label}
                    {f.required && <span className="ml-0.5 text-red-500">*</span>}
                  </Label>
                  {f.type === 'select' ? (
                    <Select
                      value={editing.draft[f.name] || ''}
                      onValueChange={(v) =>
                        setEditing((e) => (e ? { ...e, draft: { ...e.draft, [f.name]: v } } : e))
                      }
                    >
                      <SelectTrigger className="mt-1">
                        <SelectValue placeholder="Choose…" />
                      </SelectTrigger>
                      <SelectContent>
                        {(f.options ?? []).map((o) => (
                          <SelectItem key={o} value={o}>
                            {humanizeEnum(o)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : f.type === 'checkbox' ? (
                    <label className="mt-2 flex items-center gap-2 text-sm">
                      <Checkbox
                        checked={editing.draft[f.name] === true}
                        onCheckedChange={(v) =>
                          setEditing((e) => (e ? { ...e, draft: { ...e.draft, [f.name]: v === true } } : e))
                        }
                      />
                      Yes
                    </label>
                  ) : f.type === 'textarea' ? (
                    <Textarea
                      className="mt-1"
                      value={editing.draft[f.name] ?? ''}
                      onChange={(e) =>
                        setEditing((ed) => (ed ? { ...ed, draft: { ...ed.draft, [f.name]: e.target.value } } : ed))
                      }
                    />
                  ) : (
                    <Input
                      className="mt-1"
                      type={f.type}
                      value={editing.draft[f.name] ?? ''}
                      onChange={(e) =>
                        setEditing((ed) => (ed ? { ...ed, draft: { ...ed.draft, [f.name]: e.target.value } } : ed))
                      }
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

  const countries = useQuery({
    queryKey: ['careers', 'countries', tenant.data?.id],
    queryFn: async () => {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || '/api'}/public/countries`,
        { headers: { 'X-Tenant-Id': tenant.data?.id ?? '' } },
      );
      return (await res.json()) as { id: string; name: string }[];
    },
    enabled: !!tenant.data?.id,
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
        skillName: s.skillName,
        proficiency: s.proficiency ?? '',
        yearsOfExperience: s.yearsOfExperience ?? '',
        isCertified: s.isCertified,
        certificationName: s.certificationName ?? '',
      })),
      languages: p.languages.map((l) => ({
        id: l.id,
        languageName: l.languageName,
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
        countryId: f.countryId || EMPTY_GUID,
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
          skillName: s.skillName,
          proficiency: s.proficiency || null,
          yearsOfExperience: num(s.yearsOfExperience),
          isCertified: s.isCertified === true,
          certificationName: str(s.certificationName),
        })),
        languages: children.languages.map((l) => ({
          id: l.id,
          languageName: l.languageName,
          proficiency: l.proficiency || 'ProfessionalWorking',
        })),
        interests: children.interests.map((i) => ({ id: i.id, detail: i.detail })),
      };
      return candidateService.saveProfile(payload);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['candidate'] });
      toast({ title: 'Profile saved' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not save the profile', description: e?.message, variant: 'destructive' }),
  });

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
          {text('city', 'City')}
          <div className="space-y-1.5">
            <Label>Country</Label>
            <Select value={form.countryId || ''} onValueChange={(v) => set('countryId', v)}>
              <SelectTrigger>
                <SelectValue placeholder="Choose…" />
              </SelectTrigger>
              <SelectContent>
                {(countries.data ?? []).map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {text('postalAddress', 'Postal address')}
          {text('digitalAddress', 'Digital address')}
          {text('linkedInProfile', 'LinkedIn URL')}
          {text('portfolioUrl', 'Portfolio URL')}
          {text('gitHubUrl', 'GitHub URL')}
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
          {text('expectedSalaryCurrency', 'Salary currency (e.g. GHS)')}
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

      <CollectionEditor
        title="Qualifications"
        rows={children.qualifications}
        onChange={(rows) => setChildren((c) => ({ ...c, qualifications: rows }))}
        summarize={(q) => [q.qualificationName, q.institution].filter(Boolean).join(' · ')}
        fields={[
          { name: 'qualificationType', label: 'Type', type: 'select', options: QUALIFICATION_TYPES, required: true },
          { name: 'qualificationName', label: 'Qualification', type: 'text', required: true },
          { name: 'institution', label: 'Institution', type: 'text', required: true },
          { name: 'dateAwarded', label: 'Date awarded', type: 'date', required: true },
          { name: 'grade', label: 'Grade', type: 'text' },
        ]}
      />

      <CollectionEditor
        title="Skills"
        rows={children.skills}
        onChange={(rows) => setChildren((c) => ({ ...c, skills: rows }))}
        summarize={(s) => [s.skillName, s.proficiency && humanizeEnum(s.proficiency)].filter(Boolean).join(' · ')}
        fields={[
          { name: 'skillName', label: 'Skill', type: 'text', required: true },
          { name: 'proficiency', label: 'Proficiency', type: 'select', options: PROFICIENCY_LEVELS },
          { name: 'yearsOfExperience', label: 'Years of experience', type: 'number' },
          { name: 'isCertified', label: 'Certified', type: 'checkbox' },
          { name: 'certificationName', label: 'Certification name', type: 'text' },
        ]}
      />

      <CollectionEditor
        title="Languages"
        rows={children.languages}
        onChange={(rows) => setChildren((c) => ({ ...c, languages: rows }))}
        summarize={(l) => [l.languageName, l.proficiency && humanizeEnum(l.proficiency)].filter(Boolean).join(' · ')}
        fields={[
          { name: 'languageName', label: 'Language', type: 'text', required: true },
          { name: 'proficiency', label: 'Proficiency', type: 'select', options: LANGUAGE_PROFICIENCIES, required: true },
        ]}
      />

      <CollectionEditor
        title="Referees"
        hint="People we may contact about you, with their permission."
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
