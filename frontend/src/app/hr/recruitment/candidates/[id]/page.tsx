'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Pencil, Star } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { GatedPhoto, PhotoDialog } from '@/components/hr/common/PhotoDialog';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CandidateDocumentsPanel } from '@/components/hr/recruitment/CandidateDocumentsPanel';
import { CandidateNotesPanel } from '@/components/hr/recruitment/CandidateNotesPanel';
import { EngagementTimelinePanel } from '@/components/hr/recruitment/EngagementTimelinePanel';
import { TalentPoolPanel } from '@/components/hr/recruitment/TalentPoolPanel';
import {
  CandidateInterestsTab,
  CandidateLanguagesTab,
  CandidateQualificationsTab,
  CandidateRefereesTab,
  CandidateSkillsTab,
  CandidateWorkHistoryTab,
} from '@/components/hr/recruitment/CandidateSubResourceTabs';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import { jobCandidateService } from '@/services/hr/recruitment-pipeline.service';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

export default function CandidateDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const { toast } = useToast();
  const { hasAnyPermission, hasPermission } = useAuth();
  // G-2.3's shape (2026-09-15): this page held a role gate and a permission gate side by side,
  // asking two different questions about the same user. Both now ask the permission.
  const canRecruit = hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin']);
  const isHr = canRecruit;
  const canRecruitAdmin = hasPermission('HR.Recruitment.Admin');
  const [tab, setTab] = useState('overview');
  const [photoOpen, setPhotoOpen] = useState(false);
  const [photoVersion, setPhotoVersion] = useState(0);
  const queryClient = useQueryClient();

  const { data: c, isLoading, isError } = useQuery({
    queryKey: ['hr', 'candidate-detail', id],
    queryFn: () => jobCandidateService.getDetail(id),
    enabled: !!id,
  });

  const downloadCv = async () => {
    try {
      await hrDocumentService.download(`/job-candidates/${id}/cv`, `cv-${c?.candidateNumber ?? id}`);
    } catch (e: any) {
      toast({
        title: 'No CV available',
        description: e?.message ?? 'This candidate has not uploaded a CV.',
        variant: 'destructive',
      });
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !c) {
    return <EmptyState title="Candidate not found" description="It may have been removed." />;
  }

  const salary =
    c.expectedSalaryMin || c.expectedSalaryMax
      ? `${formatMoney(c.expectedSalaryMin)} – ${formatMoney(c.expectedSalaryMax)} ${c.expectedSalaryCurrency ?? ''}`.trim()
      : null;

  return (
    <div className="space-y-6">
      <PageHeader
        title={c.fullName}
        description={`${c.candidateNumber} · ${c.email}`}
        backHref="/hr/recruitment/candidates"
        actions={
          <div className="flex items-center gap-2">
            {/* Round 3, lane C2 (register row R-2): the photograph the HR view never rendered. The
                download route existed since the documents commit with nothing calling it. */}
            <button
              type="button"
              className="rounded-full ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2"
              title={c.hasPhoto ? 'View or replace the photograph' : 'Add a photograph'}
              onClick={() => setPhotoOpen(true)}
            >
              <GatedPhoto
                endpoint={jobCandidateService.photoUrl(id)}
                enabled={!!c.hasPhoto}
                version={photoVersion}
                alt={c.fullName}
                className="h-10 w-10"
              />
            </button>
            <Button variant="outline" onClick={downloadCv}>
              <Download className="mr-2 h-4 w-4" />
              CV
            </Button>
            {/* Pool membership is managed on its own tab — the rich endpoints record the
                source, reason and review date the old one-click toggle silently dropped. */}
            <Button variant="outline" onClick={() => setTab('talent-pool')}>
              <Star className="mr-2 h-4 w-4" />
              Talent pool
            </Button>
            {isHr && (
              <Button asChild>
                <Link href={`/hr/recruitment/candidates/${id}/edit`}>
                  <Pencil className="mr-2 h-4 w-4" />
                  Edit
                </Link>
              </Button>
            )}
          </div>
        }
      />

      <PhotoDialog
        open={photoOpen}
        onOpenChange={setPhotoOpen}
        title={`Photograph — ${c.fullName}`}
        description="Shown on the candidate list and beside every application they make."
        endpoint={jobCandidateService.photoUrl(id)}
        hasPhoto={!!c.hasPhoto}
        upload={(file) => jobCandidateService.uploadPhoto(id, file)}
        onUploaded={() => {
          setPhotoVersion((v) => v + 1);
          void queryClient.invalidateQueries({ queryKey: ['hr', 'candidate-detail', id] });
          void queryClient.invalidateQueries({ queryKey: ['hr', 'candidates'] });
        }}
        subjectLabel={c.fullName}
        canWrite={canRecruit}
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList className="flex-wrap">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="applications">Applications ({c.applications.length})</TabsTrigger>
          <TabsTrigger value="talent-pool">Talent pool</TabsTrigger>
          <TabsTrigger value="engagement">Engagement</TabsTrigger>
          <TabsTrigger value="qualifications">Qualifications</TabsTrigger>
          <TabsTrigger value="work">Work history</TabsTrigger>
          <TabsTrigger value="referees">Referees</TabsTrigger>
          <TabsTrigger value="skills">Skills</TabsTrigger>
          <TabsTrigger value="languages">Languages</TabsTrigger>
          <TabsTrigger value="interests">Interests</TabsTrigger>
          <TabsTrigger value="documents">Documents</TabsTrigger>
          <TabsTrigger value="notes">Notes</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="mt-4 space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Personal</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-x-6">
                <InfoRow label="Date of birth" value={formatDate(c.dateOfBirth)} />
                <InfoRow label="Gender" value={humanizeEnum(c.gender)} />
                <InfoRow label="Nationality" value={c.nationality} />
                <InfoRow label="Country" value={c.countryName} />
                <InfoRow label="City" value={c.city} />
                <InfoRow label="Digital address" value={c.digitalAddress} />
                <InfoRow label="Postal address" value={c.postalAddress} />
                {/* The national-ID trio (round 3, lane C1; register row R-3a). */}
                <InfoRow
                  label="Identity document"
                  value={
                    c.nationalIdTypeName || c.nationalIdNumber
                      ? [c.nationalIdTypeName, c.nationalIdNumber].filter(Boolean).join(' · ')
                      : null
                  }
                />
                <InfoRow
                  label="Document expiry"
                  value={c.nationalIdExpiryDate ? formatDate(c.nationalIdExpiryDate) : null}
                />
                <InfoRow
                  label="Talent pool"
                  value={
                    c.isInTalentPool ? (
                      <StatusBadge status="Active" />
                    ) : (
                      <span className="text-muted-foreground">Not in the pool</span>
                    )
                  }
                />
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Contact</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-x-6">
                <InfoRow label="Email" value={c.email} />
                <InfoRow label="Phone" value={c.phone} />
                <InfoRow label="Alternate phone" value={c.alternatePhone} />
                <InfoRow
                  label="LinkedIn"
                  value={
                    c.linkedInProfile ? (
                      <a
                        href={c.linkedInProfile}
                        target="_blank"
                        rel="noreferrer noopener"
                        className="text-primary hover:underline"
                      >
                        Profile
                      </a>
                    ) : null
                  }
                />
                <InfoRow label="Portfolio" value={c.portfolioUrl} />
                <InfoRow label="GitHub" value={c.gitHubUrl} />
              </CardContent>
            </Card>

            {/*
              Candidate-supplied and read-only from HR: these fields are not on the create/update
              payloads, so they are shown but never edited here.
            */}
            <Card className="md:col-span-2">
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Professional profile</CardTitle>
                <p className="text-xs text-muted-foreground">
                  Supplied by the candidate. Not editable from the ERP side.
                </p>
              </CardHeader>
              <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
                <InfoRow label="Headline" value={c.headline} />
                <InfoRow label="Current role" value={c.currentJobTitle} />
                <InfoRow label="Current employer" value={c.currentEmployer} />
                <InfoRow label="Total experience" value={c.totalYearsExperience ? `${c.totalYearsExperience} yrs` : null} />
                <InfoRow label="Notice period" value={c.noticePeriodDays ? `${c.noticePeriodDays} days` : null} />
                <InfoRow label="Available from" value={c.availableFrom ? formatDate(c.availableFrom) : null} />
                <InfoRow label="Work preference" value={humanizeEnum(c.preferredWorkArrangement)} />
                <InfoRow label="Work authorization" value={humanizeEnum(c.workAuthorizationStatus)} />
                <InfoRow label="Expected salary" value={salary} />
                {c.professionalSummary && (
                  <div className="col-span-2 py-1.5 md:col-span-3">
                    <span className="text-xs text-muted-foreground">Summary</span>
                    <p className="whitespace-pre-wrap text-sm">{c.professionalSummary}</p>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="applications" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {c.applications.length === 0 ? (
                <EmptyState
                  title="No applications"
                  description="This candidate has not applied for a vacancy yet."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-40">Number</TableHead>
                      <TableHead>Vacancy</TableHead>
                      <TableHead className="w-36">Applied</TableHead>
                      <TableHead className="w-40">Status</TableHead>
                      <TableHead className="w-40">Stage</TableHead>
                      <TableHead className="w-24 text-right">Score</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {c.applications.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-mono text-xs">
                          <Link href={`/hr/recruitment/applications/${a.id}`} className="hover:underline">
                            {a.applicationNumber}
                          </Link>
                        </TableCell>
                        <TableCell>{a.jobTitle || a.vacancyNumber}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(a.applicationDate)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={a.statusName} />
                        </TableCell>
                        <TableCell className="text-sm">{a.currentStageName ?? '—'}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {a.autoScore != null ? a.autoScore.toFixed(1) : '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="talent-pool" className="mt-4">
          <TalentPoolPanel candidateId={id} canManage={canRecruit} />
        </TabsContent>
        <TabsContent value="engagement" className="mt-4">
          <EngagementTimelinePanel candidateId={id} canManage={canRecruit} canAdmin={canRecruitAdmin} />
        </TabsContent>
        <TabsContent value="qualifications" className="mt-4">
          <CandidateQualificationsTab candidateId={id} />
        </TabsContent>
        <TabsContent value="work" className="mt-4">
          <CandidateWorkHistoryTab candidateId={id} />
        </TabsContent>
        <TabsContent value="referees" className="mt-4">
          <CandidateRefereesTab candidateId={id} />
        </TabsContent>
        <TabsContent value="skills" className="mt-4">
          <CandidateSkillsTab candidateId={id} />
        </TabsContent>
        <TabsContent value="languages" className="mt-4">
          <CandidateLanguagesTab candidateId={id} />
        </TabsContent>
        <TabsContent value="interests" className="mt-4">
          <CandidateInterestsTab candidateId={id} />
        </TabsContent>
        <TabsContent value="documents" className="mt-4">
          <CandidateDocumentsPanel
            candidateId={id}
            canEdit={isHr}
            identityLabel={[c.nationalIdTypeName, c.nationalIdNumber].filter(Boolean).join(' ') || null}
            referees={c.referees.map((r) => ({ id: r.id, fullName: r.fullName }))}
          />
        </TabsContent>
        <TabsContent value="notes" className="mt-4">
          <CandidateNotesPanel candidateId={id} canEdit={isHr} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
