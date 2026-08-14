'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { FireExtinguisher, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { safetyEquipmentService } from '@/services/hr/safety-equipment.service';
import {
  SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS,
} from '@/types/hr/safety-equipment';
import type { SafetyEquipmentSummary } from '@/types/hr/safety-equipment';

/**
 * The safety-equipment register (FRD §10) — extinguishers, AEDs, detectors and the rest. The
 * due/expiring tabs are polled queries, not alerts: nothing reminds anyone automatically until
 * the slice-13 job engine, so these views ARE the reminder.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const typeLabel = (v: string) =>
  SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

function EquipmentTable({ items, emptyText }: { items: SafetyEquipmentSummary[]; emptyText: string }) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={emptyText} icon={FireExtinguisher} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Number</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Location</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Next inspection</TableHead>
              <TableHead>Certification expiry</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((e) => {
              const inspectionOverdue =
                !!e.nextInspectionDueDate && new Date(e.nextInspectionDueDate) < new Date();
              const certExpired =
                !!e.certificationExpiryDate && new Date(e.certificationExpiryDate) < new Date();
              return (
                <TableRow key={e.id}>
                  <TableCell>
                    <Link
                      href={`/hr/safety/equipment/${e.id}`}
                      className="font-mono text-primary hover:underline"
                    >
                      {e.equipmentNumber}
                    </Link>
                  </TableCell>
                  <TableCell className="font-medium">{e.name}</TableCell>
                  <TableCell>{typeLabel(e.type)}</TableCell>
                  <TableCell>{e.locationName}</TableCell>
                  <TableCell>
                    <StatusBadge status={e.statusName} />
                  </TableCell>
                  <TableCell>
                    {inspectionOverdue ? (
                      <Badge variant="destructive">Overdue {fmtDate(e.nextInspectionDueDate)}</Badge>
                    ) : (
                      <span className="text-sm">{fmtDate(e.nextInspectionDueDate)}</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {certExpired ? (
                      <Badge variant="destructive">Expired {fmtDate(e.certificationExpiryDate)}</Badge>
                    ) : (
                      <span className="text-sm">{fmtDate(e.certificationExpiryDate)}</span>
                    )}
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

export default function SafetyEquipmentRegisterPage() {
  const [tab, setTab] = useState('all');

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'safety-equipment', 'all'],
    queryFn: () => safetyEquipmentService.getAll(),
  });
  const { data: dueInspection = [] } = useQuery({
    queryKey: ['hr', 'safety-equipment', 'due-inspection'],
    queryFn: () => safetyEquipmentService.getDueForInspection(30),
  });
  const { data: dueMaintenance = [] } = useQuery({
    queryKey: ['hr', 'safety-equipment', 'due-maintenance'],
    queryFn: () => safetyEquipmentService.getDueForMaintenance(30),
  });
  const { data: expiringCert = [] } = useQuery({
    queryKey: ['hr', 'safety-equipment', 'expiring-cert'],
    queryFn: () => safetyEquipmentService.getExpiringCertification(30),
  });
  const { data: outOfService = [] } = useQuery({
    queryKey: ['hr', 'safety-equipment', 'out-of-service'],
    queryFn: () => safetyEquipmentService.getOutOfService(),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Safety Equipment"
        description="The fire-safety and emergency equipment register — inspections, maintenance and certification. Due dates are checked here manually; no automatic reminders fire yet."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/equipment/new">
              <Plus className="mr-2 h-4 w-4" /> Register equipment
            </Link>
          </Button>
        }
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="all">All ({all.length})</TabsTrigger>
          <TabsTrigger value="due-inspection">Due inspection ({dueInspection.length})</TabsTrigger>
          <TabsTrigger value="due-maintenance">Due maintenance ({dueMaintenance.length})</TabsTrigger>
          <TabsTrigger value="expiring-cert">Expiring certification ({expiringCert.length})</TabsTrigger>
          <TabsTrigger value="out-of-service">Out of service ({outOfService.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="all" className="mt-4">
          <EquipmentTable items={all} emptyText="No equipment registered yet." />
        </TabsContent>
        <TabsContent value="due-inspection" className="mt-4">
          <EquipmentTable
            items={dueInspection}
            emptyText="Nothing is due for inspection in the next 30 days."
          />
        </TabsContent>
        <TabsContent value="due-maintenance" className="mt-4">
          <EquipmentTable
            items={dueMaintenance}
            emptyText="Nothing is due for maintenance in the next 30 days."
          />
        </TabsContent>
        <TabsContent value="expiring-cert" className="mt-4">
          <EquipmentTable
            items={expiringCert}
            emptyText="No certifications expire in the next 30 days."
          />
        </TabsContent>
        <TabsContent value="out-of-service" className="mt-4">
          <EquipmentTable items={outOfService} emptyText="Nothing is out of service." />
        </TabsContent>
      </Tabs>
    </div>
  );
}
