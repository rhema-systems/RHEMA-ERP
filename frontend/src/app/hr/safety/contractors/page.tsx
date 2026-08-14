'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { HardHat, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { safetyContractorService } from '@/services/hr/safety-contractor.service';
import { SHE_CONTRACTOR_STATUS_OPTIONS } from '@/types/hr/safety-contractors';
import type { SheContractorSummary } from '@/types/hr/safety-contractors';

/**
 * Contractor SHE register (FRD §7): every contractor with their pre-qualification standing and
 * open non-compliance count, plus the standing strips for overdue notices and unverified /
 * expiring documents. Expiries are polled queries — nothing alerts automatically until the
 * slice-13 job engine, so this screen IS the chase list.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const statusLabel = (v: string) =>
  SHE_CONTRACTOR_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

const statusVariant = (v: string): 'default' | 'secondary' | 'destructive' | 'outline' => {
  if (v === 'Approved') return 'secondary';
  if (v === 'Suspended' || v === 'Revoked' || v === 'Blacklisted') return 'destructive';
  return 'outline';
};

function ContractorTable({
  items,
  emptyText,
}: {
  items: SheContractorSummary[];
  emptyText: string;
}) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={emptyText} icon={HardHat} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Company</TableHead>
              <TableHead>Contact</TableHead>
              <TableHead>SHE status</TableHead>
              <TableHead>Pre-qual score</TableHead>
              <TableHead>Pre-qual expiry</TableHead>
              <TableHead>Open notices</TableHead>
              <TableHead>Active</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((c) => {
              const expiryPassed =
                c.preQualificationExpiryDate &&
                new Date(c.preQualificationExpiryDate) < new Date();
              return (
                <TableRow key={c.id}>
                  <TableCell>
                    <Link
                      href={`/hr/safety/contractors/${c.id}`}
                      className="font-mono text-primary hover:underline"
                    >
                      {c.contractorCode}
                    </Link>
                  </TableCell>
                  <TableCell className="font-medium">{c.companyName}</TableCell>
                  <TableCell>{c.primaryContactName ?? '—'}</TableCell>
                  <TableCell>
                    <Badge variant={statusVariant(c.sheStatus)}>{statusLabel(c.sheStatus)}</Badge>
                  </TableCell>
                  <TableCell>
                    {c.preQualificationScore != null ? `${c.preQualificationScore} / 100` : '—'}
                  </TableCell>
                  <TableCell>
                    {expiryPassed ? (
                      <Badge variant="destructive">
                        Expired {fmtDate(c.preQualificationExpiryDate)}
                      </Badge>
                    ) : (
                      <span className="text-sm">{fmtDate(c.preQualificationExpiryDate)}</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {c.openNonComplianceCount > 0 ? (
                      <Badge variant="destructive">{c.openNonComplianceCount}</Badge>
                    ) : (
                      <span className="text-muted-foreground text-sm">0</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={c.isActive ? 'Active' : 'Inactive'} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function ContractorsPage() {
  const [tab, setTab] = useState('all');

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'safety-contractors'],
    queryFn: () => safetyContractorService.getAll(),
  });
  const { data: expiringPreQual = [] } = useQuery({
    queryKey: ['hr', 'safety-contractors', 'expiring-prequalification'],
    queryFn: () => safetyContractorService.getExpiringPreQualification(30),
  });
  const { data: withOpenNCs = [] } = useQuery({
    queryKey: ['hr', 'safety-contractors', 'with-open-non-compliances'],
    queryFn: () => safetyContractorService.getWithOpenNonCompliances(),
  });
  const { data: overdueNotices = [] } = useQuery({
    queryKey: ['hr', 'safety-contractors', 'non-compliances', 'overdue'],
    queryFn: () => safetyContractorService.getOverdueNonCompliances(),
  });
  const { data: unverifiedDocs = [] } = useQuery({
    queryKey: ['hr', 'safety-contractors', 'documents', 'unverified'],
    queryFn: () => safetyContractorService.getUnverifiedDocuments(),
  });
  const { data: expiringDocs = [] } = useQuery({
    queryKey: ['hr', 'safety-contractors', 'documents', 'expiring'],
    queryFn: () => safetyContractorService.getExpiringDocuments(30),
  });

  const active = all.filter((c) => c.isActive);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Contractor SHE Management"
        description="Contractor register with pre-qualification, inductions, SHE inspections, non-compliance notices and documents. Scores are hand-entered — no computed ranking yet. Expiries are checked here manually; no automatic reminders fire."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/contractors/new">
              <Plus className="mr-2 h-4 w-4" /> New contractor
            </Link>
          </Button>
        }
      />

      {overdueNotices.length > 0 && (
        <Card className="border-destructive/50">
          <CardHeader>
            <CardTitle className="text-base text-destructive">
              Overdue non-compliance notices ({overdueNotices.length})
            </CardTitle>
            <CardDescription>
              The rectification deadline has passed and the notice is still open.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {overdueNotices.map((n) => (
              <Link key={n.id} href={`/hr/safety/contractors/${n.contractorId}`}>
                <Badge variant="outline" className="border-destructive/50 hover:bg-accent">
                  {n.noticeNumber} · {n.severityName} · due {fmtDate(n.rectificationDeadline)}
                </Badge>
              </Link>
            ))}
          </CardContent>
        </Card>
      )}

      {expiringDocs.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Contractor documents expiring ({expiringDocs.length})
            </CardTitle>
            <CardDescription>
              Within 30 days — insurance, competency certificates and other dated papers.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {expiringDocs.map((d) => (
              <Link key={d.id} href={`/hr/safety/contractors/${d.contractorId}`}>
                <Badge variant="secondary" className="hover:bg-accent">
                  {d.title || d.fileName} · {d.documentTypeName} · {fmtDate(d.expiryDate)}
                </Badge>
              </Link>
            ))}
          </CardContent>
        </Card>
      )}

      {unverifiedDocs.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Documents awaiting verification ({unverifiedDocs.length})
            </CardTitle>
            <CardDescription>Uploaded but not yet checked by anyone.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {unverifiedDocs.map((d) => (
              <Link key={d.id} href={`/hr/safety/contractors/${d.contractorId}`}>
                <Badge variant="outline" className="hover:bg-accent">
                  {d.title || d.fileName} · uploaded {fmtDate(d.uploadedDate)}
                </Badge>
              </Link>
            ))}
          </CardContent>
        </Card>
      )}

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="all">All ({all.length})</TabsTrigger>
          <TabsTrigger value="active">Active ({active.length})</TabsTrigger>
          <TabsTrigger value="expiring">
            Pre-qual expiring ({expiringPreQual.length})
          </TabsTrigger>
          <TabsTrigger value="open-ncs">Open notices ({withOpenNCs.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="all" className="mt-4">
          <ContractorTable
            items={all}
            emptyText="No contractors yet. Register a contractor before they start work on site."
          />
        </TabsContent>
        <TabsContent value="active" className="mt-4">
          <ContractorTable items={active} emptyText="No active contractors." />
        </TabsContent>
        <TabsContent value="expiring" className="mt-4">
          <ContractorTable
            items={expiringPreQual}
            emptyText="No approved contractor's pre-qualification expires in the next 30 days."
          />
        </TabsContent>
        <TabsContent value="open-ncs" className="mt-4">
          <ContractorTable
            items={withOpenNCs}
            emptyText="No contractor has an open non-compliance notice."
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
