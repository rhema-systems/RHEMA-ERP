'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { useAuth } from '@/hooks/use-auth';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import type { JobFamily, JobLevel, JobSubFamily } from '@/types/hr/job-architecture';

/**
 * The job architecture taxonomy: families, the sub-families under them, and the career levels a
 * job description is placed on.
 *
 * ⚠ **`api/hr/job-architecture` had fifteen endpoints and no screen at all.** The three GETs were
 * called by the job-description form's pickers; the nine writes had no caller anywhere in the
 * product, so the vocabulary was whatever the starter seed left behind — it could not be corrected,
 * extended or retired, and a tenant whose seed skipped (the "is the table empty" probe was
 * satisfied by a harness's own rows) had no vocabulary and no way to make one.
 *
 * ⚠ **A code is required here even though the DTO does not mark it so.** The server's uniqueness
 * check compares codes among live rows, and two blank codes are equal — so the SECOND record saved
 * without one is refused with a message about a family "with code ''". Requiring it in the form is
 * what turns that into an intelligible rule.
 *
 * Permissions follow the rest of the area: Read to look, **Write** to add and edit, **Admin** to
 * remove (`JobArchitectureAdminPolicy` on all three DELETEs — HR does not hold it). The remove
 * affordance is hidden rather than offered and refused.
 */

const familySchema = z.object({
  code: z.string().min(1, 'A code is required').max(50),
  name: z.string().min(1, 'A name is required').max(150),
  description: z.string().max(1000).optional().or(z.literal('')),
  isActive: z.boolean(),
});
type FamilyForm = z.input<typeof familySchema>;
const emptyFamily: FamilyForm = { code: '', name: '', description: '', isActive: true };

const subFamilySchema = familySchema;
type SubFamilyForm = FamilyForm;
const emptySubFamily: SubFamilyForm = { ...emptyFamily };

const levelSchema = z.object({
  code: z.string().min(1, 'A code is required').max(50),
  name: z.string().min(1, 'A name is required').max(150),
  rank: z.coerce.number().int().min(1, 'Rank starts at 1').max(999),
  description: z.string().max(1000).optional().or(z.literal('')),
  salaryGradeId: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});
type LevelForm = z.input<typeof levelSchema>;
const emptyLevel: LevelForm = {
  code: '',
  name: '',
  rank: 1,
  description: '',
  salaryGradeId: '',
  isActive: true,
};

const blank = (v?: string) => (v && v.length > 0 ? v : undefined);

export default function JobArchitecturePage() {
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const [family, setFamily] = useState<JobFamily | null>(null);

  // Mirrors the server's own fallback (HrPermissions.RoleGrants): permissions resolve from the
  // database, so on a tenant provisioned before the seeder ran a user holds none at all.
  const canWrite =
    hasAnyPermission(['HR.JobArchitecture.Write', 'HR.JobArchitecture.Admin']) || hasAnyRole(HR_ROLES);
  const canDelete = hasAnyPermission(['HR.JobArchitecture.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  // Payroll owns the grade store; a career level only points at one. Inactive grades are left out
  // for the same reason the job-description form leaves out inactive unions.
  const { data: grades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'active'],
    queryFn: () => salaryGradeService.getActive(),
  });
  const gradeOptions = (grades ?? []).map((g) => ({ value: g.id, label: `${g.code} — ${g.name}` }));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Job Architecture"
        description="The vocabulary a job description is classified with: families, the sub-families under them, and the career-level ladder."
        backHref="/administration/hr"
      />

      <Tabs defaultValue="families">
        <TabsList>
          <TabsTrigger value="families">Families &amp; sub-families</TabsTrigger>
          <TabsTrigger value="levels">Career levels</TabsTrigger>
        </TabsList>

        <TabsContent value="families" className="space-y-6 pt-4">
          <ResourceListPanel<JobFamily, FamilyForm>
            title="job families"
            singular="job family"
            queryKey={['job-architecture', 'families', 'all']}
            // The prefix reaches the `active` read the job-description pickers use, so a family
            // added here is offered on the next form without a reload.
            invalidateKeys={[['job-architecture', 'families']]}
            dialogHint="A family is the broadest grouping — Engineering, Finance, Operations."
            list={() => jobArchitectureService.getJobFamilies()}
            create={(values) => {
              const v = familySchema.parse(values);
              return jobArchitectureService.createJobFamily({
                code: v.code,
                name: v.name,
                description: blank(v.description),
                isActive: v.isActive,
              });
            }}
            update={(id, values) => {
              const v = familySchema.parse(values);
              return jobArchitectureService.updateJobFamily(id, {
                id,
                code: v.code,
                name: v.name,
                description: blank(v.description),
                isActive: v.isActive,
              });
            }}
            /* ⚠ 409 when sub-families or job descriptions still hang off it — and the message names
               BOTH obstacles at once, so it is worth showing verbatim rather than summarising. */
            remove={canDelete ? (id) => jobArchitectureService.deleteJobFamily(id) : undefined}
            readOnly={!canWrite}
            getId={(f) => f.id}
            emptyDescription="No job families yet. Until one exists a job description cannot be classified at all."
            actions={[
              {
                label: 'Sub-families',
                run: async (f) => setFamily(f),
              },
            ]}
            columns={[
              { header: 'Code', cell: (f) => <span className="font-mono">{f.code}</span> },
              { header: 'Name', cell: (f) => <span className="font-medium">{f.name}</span> },
              {
                header: 'Description',
                cell: (f) => f.description || <span className="text-muted-foreground">—</span>,
              },
              {
                header: 'Sub-families',
                cell: (f) =>
                  f.subFamilyCount > 0 ? (
                    <Badge variant="outline">{f.subFamilyCount}</Badge>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  ),
              },
              {
                header: 'Status',
                cell: (f) => <StatusBadge status={f.isActive ? 'Active' : 'Inactive'} />,
              },
            ]}
            schema={familySchema}
            emptyForm={emptyFamily}
            toForm={(f) => ({
              code: f.code,
              name: f.name,
              description: f.description ?? '',
              isActive: f.isActive,
            })}
            renderFields={(form) => (
              <div className="space-y-4">
                <FieldRow>
                  <TextField form={form} name="code" label="Code" required />
                  <TextField form={form} name="name" label="Name" required />
                </FieldRow>
                <TextareaField form={form} name="description" label="Description" rows={2} />
                <SwitchField
                  form={form}
                  name="isActive"
                  label="Active"
                  description="An inactive family stays on the job descriptions already filed under it but is not offered for new ones."
                />
              </div>
            )}
          />

          {family ? (
            <ResourceCollectionTab<JobSubFamily, SubFamilyForm>
              key={family.id}
              parentId={family.id}
              title={`sub-families of ${family.name}`}
              singular="sub-family"
              queryKey={['job-architecture', 'sub-families', family.id]}
              // The family row shows a sub-family COUNT, so the families list is stale the moment
              // one is added or removed here.
              invalidateKeys={[
                ['job-architecture', 'families'],
                ['job-architecture', 'sub-families'],
              ]}
              // A sub-family cannot change families: re-filing one reclassifies every job
              // description under it. The API refuses the attempt outright, so the dialog says so
              // rather than offering a picker whose save would fail.
              dialogHint={`Added under ${family.name}. A sub-family cannot be moved to another family — reclassify the job descriptions instead.`}
              list={(familyId) => jobArchitectureService.getSubFamilies(familyId)}
              create={(familyId, values) => {
                const v = subFamilySchema.parse(values);
                return jobArchitectureService.createSubFamily(familyId, {
                  code: v.code,
                  name: v.name,
                  description: blank(v.description),
                  isActive: v.isActive,
                });
              }}
              update={(familyId, id, values) => {
                const v = subFamilySchema.parse(values);
                return jobArchitectureService.updateSubFamily(id, {
                  id,
                  jobFamilyId: familyId,
                  code: v.code,
                  name: v.name,
                  description: blank(v.description),
                  isActive: v.isActive,
                });
              }}
              remove={canDelete ? (_familyId, id) => jobArchitectureService.deleteSubFamily(id) : undefined}
              readOnly={!canWrite}
              getId={(s) => s.id}
              emptyDescription="No sub-families under this family yet. They are optional — a job description may sit on the family alone."
              columns={[
                { header: 'Code', cell: (s) => <span className="font-mono">{s.code}</span> },
                { header: 'Name', cell: (s) => <span className="font-medium">{s.name}</span> },
                {
                  header: 'Description',
                  cell: (s) => s.description || <span className="text-muted-foreground">—</span>,
                },
                {
                  header: 'Status',
                  cell: (s) => <StatusBadge status={s.isActive ? 'Active' : 'Inactive'} />,
                },
              ]}
              schema={subFamilySchema}
              emptyForm={emptySubFamily}
              toForm={(s) => ({
                code: s.code,
                name: s.name,
                description: s.description ?? '',
                isActive: s.isActive,
              })}
              renderFields={(form) => (
                <div className="space-y-4">
                  <FieldRow>
                    <TextField form={form} name="code" label="Code" required />
                    <TextField form={form} name="name" label="Name" required />
                  </FieldRow>
                  <TextareaField form={form} name="description" label="Description" rows={2} />
                  <SwitchField
                    form={form}
                    name="isActive"
                    label="Active"
                    description="An inactive sub-family stays on existing job descriptions but is not offered for new ones."
                  />
                </div>
              )}
            />
          ) : (
            <Card>
              <CardContent className="py-10 text-center text-sm text-muted-foreground">
                Choose <span className="font-medium">Sub-families</span> on a family above to see
                and maintain what sits under it.
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="levels" className="pt-4">
          <ResourceListPanel<JobLevel, LevelForm>
            title="career levels"
            singular="career level"
            queryKey={['job-architecture', 'levels', 'all']}
            invalidateKeys={[['job-architecture', 'levels']]}
            dialogHint="Rank orders the ladder — 1 is the most junior. Each rank may be held by one level only."
            list={() => jobArchitectureService.getJobLevels()}
            create={(values) => {
              const v = levelSchema.parse(values);
              return jobArchitectureService.createJobLevel({
                code: v.code,
                name: v.name,
                rank: v.rank,
                description: blank(v.description),
                salaryGradeId: blank(v.salaryGradeId) ?? null,
                isActive: v.isActive,
              });
            }}
            update={(id, values) => {
              const v = levelSchema.parse(values);
              return jobArchitectureService.updateJobLevel(id, {
                id,
                code: v.code,
                name: v.name,
                rank: v.rank,
                description: blank(v.description),
                salaryGradeId: blank(v.salaryGradeId) ?? null,
                isActive: v.isActive,
              });
            }}
            remove={canDelete ? (id) => jobArchitectureService.deleteJobLevel(id) : undefined}
            readOnly={!canWrite}
            getId={(l) => l.id}
            emptyDescription="No career levels yet. Without them a job description has no seniority and succession has no ladder to read."
            columns={[
              { header: 'Rank', cell: (l) => <span className="font-mono">{l.rank}</span> },
              { header: 'Code', cell: (l) => <span className="font-mono">{l.code}</span> },
              { header: 'Name', cell: (l) => <span className="font-medium">{l.name}</span> },
              {
                header: 'Salary grade',
                cell: (l) => l.salaryGradeName || <span className="text-muted-foreground">—</span>,
              },
              {
                header: 'Status',
                cell: (l) => <StatusBadge status={l.isActive ? 'Active' : 'Inactive'} />,
              },
            ]}
            schema={levelSchema}
            emptyForm={emptyLevel}
            toForm={(l) => ({
              code: l.code,
              name: l.name,
              rank: l.rank,
              description: l.description ?? '',
              salaryGradeId: l.salaryGradeId ?? '',
              isActive: l.isActive,
            })}
            renderFields={(form) => (
              <div className="space-y-4">
                <FieldRow>
                  <TextField form={form} name="code" label="Code" required />
                  <TextField form={form} name="name" label="Name" required />
                </FieldRow>
                <FieldRow>
                  {/* ⚠ Two levels at one rank make "more senior than" unanswerable, so the server
                      refuses a duplicate and names the level already holding it. */}
                  <NumberField form={form} name="rank" label="Rank" required />
                  <SelectField
                    form={form}
                    name="salaryGradeId"
                    label="Salary grade"
                    options={gradeOptions}
                    allowEmpty
                    emptyLabel="Not tied to a grade"
                    placeholder={gradeOptions.length ? 'Select…' : 'No active salary grades'}
                  />
                </FieldRow>
                <TextareaField form={form} name="description" label="Description" rows={2} />
                <SwitchField
                  form={form}
                  name="isActive"
                  label="Active"
                  description="An inactive level stays on existing job descriptions but is not offered for new ones."
                />
              </div>
            )}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
