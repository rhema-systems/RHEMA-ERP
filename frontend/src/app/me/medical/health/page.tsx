'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { HeartPulse, Stethoscope } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { employeeHealthMeService } from '@/services/hr/medical-me.service';

/**
 * My Health Record (area 25 slice 8, specs #30 + #35) — the employee's own occupational-health
 * file over `api/employee-health/me`: profile, conditions, allergies, examinations, and the
 * health-surveillance history the SHE↔Medical boundary places on the medical side. The
 * surveillance detail shows the SUBJECT their full findings and any work restriction — the
 * natural-justice rule every subject-facing read in the module follows.
 *
 * Read-only by design: corrections to a medical record go through HR. The desk register at
 * /hr/medical/health is the HR-side write surface and stays where it is.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyHealthPage() {
  const [surveillanceId, setSurveillanceId] = useState<string | null>(null);

  // 404 = HR has not opened a profile yet; that is the empty state, not an error.
  const { data: profile, isLoading: profileLoading } = useQuery({
    queryKey: ['me', 'health', 'profile'],
    queryFn: () => employeeHealthMeService.getProfile(),
    retry: false,
  });
  const { data: conditions = [] } = useQuery({
    queryKey: ['me', 'health', 'conditions'],
    queryFn: () => employeeHealthMeService.getConditions(),
  });
  const { data: allergies = [] } = useQuery({
    queryKey: ['me', 'health', 'allergies'],
    queryFn: () => employeeHealthMeService.getAllergies(),
  });
  const { data: exams = [] } = useQuery({
    queryKey: ['me', 'health', 'exams'],
    queryFn: () => employeeHealthMeService.getExams(),
  });
  const { data: surveillance = [] } = useQuery({
    queryKey: ['me', 'health', 'surveillance'],
    queryFn: () => employeeHealthMeService.getSurveillance(),
  });
  const { data: surveillanceDetail } = useQuery({
    queryKey: ['me', 'health', 'surveillance', surveillanceId],
    queryFn: () => employeeHealthMeService.getSurveillanceRecord(surveillanceId as string),
    enabled: !!surveillanceId,
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Health Record"
        description="Your health profile, examinations and workplace health surveillance. Corrections go through HR — this record is read-only."
        backHref="/me/medical"
      />

      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">Profile</TabsTrigger>
          <TabsTrigger value="conditions">Conditions & allergies</TabsTrigger>
          <TabsTrigger value="exams">Examinations</TabsTrigger>
          <TabsTrigger value="surveillance">Surveillance</TabsTrigger>
        </TabsList>

        <TabsContent value="profile" className="mt-4">
          {profileLoading ? null : !profile ? (
            <EmptyState
              icon={HeartPulse}
              title="No health profile yet"
              description="HR has not opened a health profile for you. It appears here once created."
            />
          ) : (
            <Card>
              <CardContent className="grid gap-x-8 gap-y-3 p-6 text-sm sm:grid-cols-2">
                <div>
                  <p className="text-muted-foreground">Blood group</p>
                  <p>{profile.bloodGroup ?? '—'}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Height / weight</p>
                  <p>
                    {profile.heightCm ? `${profile.heightCm} cm` : '—'} ·{' '}
                    {profile.weightKg ? `${profile.weightKg} kg` : '—'}
                  </p>
                </div>
                <div className="sm:col-span-2">
                  <p className="text-muted-foreground">Emergency contact</p>
                  <p>
                    {profile.emergencyContactName ?? '—'}
                    {profile.emergencyContactRelationship
                      ? ` (${profile.emergencyContactRelationship})`
                      : ''}
                    {profile.emergencyContactPhone ? ` · ${profile.emergencyContactPhone}` : ''}
                  </p>
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="conditions" className="mt-4 space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Conditions</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {conditions.length === 0 ? (
                <p className="text-muted-foreground px-6 pb-6 text-sm">No conditions on record.</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Condition</TableHead>
                      <TableHead>Diagnosed</TableHead>
                      <TableHead>Severity</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {conditions.map((c) => (
                      <TableRow key={c.id}>
                        <TableCell className="font-medium">{c.conditionName}</TableCell>
                        <TableCell>{fmtDate(c.diagnosedDate)}</TableCell>
                        <TableCell>{c.severity ?? '—'}</TableCell>
                        <TableCell>{c.status ?? '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Allergies</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {allergies.length === 0 ? (
                <p className="text-muted-foreground px-6 pb-6 text-sm">No allergies on record.</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Allergen</TableHead>
                      <TableHead>Severity</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {allergies.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-medium">{a.allergen}</TableCell>
                        <TableCell>{a.severity ?? '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="exams" className="mt-4">
          {exams.length === 0 ? (
            <EmptyState
              icon={Stethoscope}
              title="No examinations"
              description="Medical examinations recorded for you will appear here."
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Exam date</TableHead>
                      <TableHead>Result</TableHead>
                      <TableHead>Next due</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {exams.map((e) => (
                      <TableRow key={e.id}>
                        <TableCell>{fmtDate(e.examDate)}</TableCell>
                        <TableCell>{e.result ?? '—'}</TableCell>
                        <TableCell>{fmtDate(e.nextExamDueDate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="surveillance" className="mt-4">
          {surveillance.length === 0 ? (
            <EmptyState
              icon={Stethoscope}
              title="No surveillance records"
              description="Workplace health surveillance (hearing tests, lung function, biological monitoring…) recorded for you will appear here."
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Examined</TableHead>
                      <TableHead>Next due</TableHead>
                      <TableHead>Result</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {surveillance.map((s) => (
                      <TableRow key={s.id}>
                        <TableCell className="font-mono text-sm">{s.surveillanceNumber}</TableCell>
                        <TableCell>{s.type}</TableCell>
                        <TableCell>{fmtDate(s.examinationDate)}</TableCell>
                        <TableCell>{fmtDate(s.nextExaminationDate)}</TableCell>
                        <TableCell>
                          {s.workRestrictionIssued ? (
                            <Badge variant="destructive">{s.result}</Badge>
                          ) : (
                            <Badge variant="secondary">{s.result}</Badge>
                          )}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="ghost" size="sm" onClick={() => setSurveillanceId(s.id)}>
                            Details
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>

      <Dialog open={!!surveillanceId} onOpenChange={(open) => !open && setSurveillanceId(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              Surveillance{' '}
              <span className="font-mono text-base">
                {surveillanceDetail?.surveillanceNumber ?? ''}
              </span>
            </DialogTitle>
          </DialogHeader>
          {surveillanceDetail && (
            <div className="space-y-3 text-sm">
              <div className="grid gap-x-6 gap-y-2 sm:grid-cols-2">
                <div>
                  <p className="text-muted-foreground">Type</p>
                  <p>{surveillanceDetail.type}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Result</p>
                  <p>{surveillanceDetail.result}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Examined</p>
                  <p>{fmtDate(surveillanceDetail.examinationDate)}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Next due</p>
                  <p>{fmtDate(surveillanceDetail.nextExaminationDate)}</p>
                </div>
                {surveillanceDetail.healthcareFacilityName && (
                  <div>
                    <p className="text-muted-foreground">Facility</p>
                    <p>{surveillanceDetail.healthcareFacilityName}</p>
                  </div>
                )}
                {surveillanceDetail.examiningPhysician && (
                  <div>
                    <p className="text-muted-foreground">Physician</p>
                    <p>{surveillanceDetail.examiningPhysician}</p>
                  </div>
                )}
              </div>
              {surveillanceDetail.exposureHazard && (
                <div>
                  <p className="text-muted-foreground">Exposure monitored</p>
                  <p>{surveillanceDetail.exposureHazard}</p>
                </div>
              )}
              {surveillanceDetail.findings && (
                <div>
                  <p className="text-muted-foreground">Findings</p>
                  <p>{surveillanceDetail.findings}</p>
                </div>
              )}
              {surveillanceDetail.recommendations && (
                <div>
                  <p className="text-muted-foreground">Recommendations</p>
                  <p>{surveillanceDetail.recommendations}</p>
                </div>
              )}
              {surveillanceDetail.workRestrictionIssued && (
                <div className="border-destructive/50 rounded-md border p-3">
                  <p className="text-destructive font-medium">Work restriction in force</p>
                  <p>{surveillanceDetail.workRestrictionDetails ?? 'See the SHE team for details.'}</p>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
