'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Receipt, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import { useToast } from '@/hooks/use-toast';
import { medicalSelfServiceClaimService } from '@/services/hr/medical-claims.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { MEDICAL_EXPENSE_TYPE_OPTIONS } from '@/types/hr/medical';
import type { MedicalExpenseType, OwnMedicalClaimSummary } from '@/types/hr/medical';

/**
 * An employee's own medical claims — file one, follow it, attach the receipt.
 *
 * Everything here is scoped to the signed-in employee by the API; no id on this screen identifies
 * anybody else, and a claim belonging to someone else simply does not resolve. Approval, payment
 * and adjudicator notes live on the HR caseload and have no counterpart here.
 */
const money = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const typeLabel = (v: string) =>
  MEDICAL_EXPENSE_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

function StatusBadgeFor({ status }: { status: string }) {
  if (status === 'Paid') return <Badge variant="secondary">Paid</Badge>;
  if (status === 'Approved') return <Badge variant="secondary">Approved</Badge>;
  if (status === 'Rejected') return <Badge variant="destructive">Rejected</Badge>;
  return <Badge variant="outline">{status}</Badge>;
}

export default function MyMedicalClaimsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [open, setOpen] = useState(false);
  const [serviceDate, setServiceDate] = useState('');
  const [expenseType, setExpenseType] = useState<MedicalExpenseType>('Consultation');
  const [facilityId, setFacilityId] = useState('');
  const [description, setDescription] = useState('');
  const [amount, setAmount] = useState('');
  const [selected, setSelected] = useState<OwnMedicalClaimSummary | null>(null);

  const { data: claims = [] } = useQuery({
    queryKey: ['hr', 'my-medical-claims'],
    queryFn: () => medicalSelfServiceClaimService.getMine(),
  });

  // Facility reads are open to any authenticated user precisely so this form can work.
  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'medical-facilities', 'active'],
    queryFn: () => medicalFacilityService.getActiveFacilities(),
  });

  const file = useMutation({
    mutationFn: () =>
      medicalSelfServiceClaimService.file({
        serviceDate: new Date(serviceDate).toISOString(),
        expenseType,
        description,
        facilityId,
        isEmergency: false,
        requiredHospitalization: false,
        totalAmount: Number(amount),
        amountRequested: Number(amount),
      }),
    onSuccess: () => {
      toast({ title: 'Claim filed', description: 'Attach your receipt so HR can assess it.' });
      setOpen(false);
      setServiceDate('');
      setDescription('');
      setAmount('');
      setFacilityId('');
      queryClient.invalidateQueries({ queryKey: ['hr', 'my-medical-claims'] });
    },
    onError: (error) =>
      toast({
        variant: 'destructive',
        title: 'Could not file the claim',
        description: error instanceof Error ? error.message : 'Unexpected error',
      }),
  });

  const canSubmit = serviceDate && facilityId && description.trim() && Number(amount) > 0;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="My Medical Claims"
        description="Claim back medical costs you have paid for yourself. Attach the receipt — HR cannot assess a claim without it."
        actions={
          <Button onClick={() => setOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> File a claim
          </Button>
        }
      />

      {claims.length === 0 ? (
        <EmptyState
          title="No claims yet"
          description="When you pay for treatment yourself, file it here and attach the receipt."
          icon={Receipt}
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Claim</TableHead>
                  <TableHead>Service date</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Facility</TableHead>
                  <TableHead className="text-right">Requested</TableHead>
                  <TableHead className="text-right">Approved</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {claims.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-mono text-sm">{c.claimNumber}</TableCell>
                    <TableCell>{fmtDate(c.serviceDate)}</TableCell>
                    <TableCell>{typeLabel(c.expenseType)}</TableCell>
                    <TableCell>{c.facilityName}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {money(c.amountRequested)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {money(c.amountApproved)}
                    </TableCell>
                    <TableCell>
                      <StatusBadgeFor status={c.status} />
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" onClick={() => setSelected(c)}>
                        Receipts
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {selected && (
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              Receipts for {selected.claimNumber}
            </p>
            <Button variant="ghost" size="sm" onClick={() => setSelected(null)}>
              Close
            </Button>
          </div>
          <AttachmentsPanel
            title="Receipts"
            note="Only you and the HR team assessing your claim can open these."
            queryKey={['hr', 'my-medical-claims', selected.id, 'documents']}
            list={() => medicalSelfServiceClaimService.getDocuments(selected.id)}
            upload={(f, desc) =>
              medicalSelfServiceClaimService.uploadDocument(selected.id, f, 'Receipt', desc)
            }
            download={(doc) => medicalSelfServiceClaimService.downloadDocument(selected.id, doc)}
            emptyDescription="No receipts attached yet."
          />
        </div>
      )}

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>File a medical claim</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="service-date">Date of treatment</Label>
              <Input
                id="service-date"
                type="date"
                value={serviceDate}
                onChange={(e) => setServiceDate(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Type of expense</Label>
              <Select
                value={expenseType}
                onValueChange={(v) => setExpenseType(v as MedicalExpenseType)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEDICAL_EXPENSE_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Where were you treated?</Label>
              <Select value={facilityId} onValueChange={setFacilityId}>
                <SelectTrigger>
                  <SelectValue placeholder="Choose a facility" />
                </SelectTrigger>
                <SelectContent>
                  {facilities.map((f) => (
                    <SelectItem key={f.id} value={f.id}>
                      {f.facilityName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="claim-description">What was it for?</Label>
              <Textarea
                id="claim-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={3}
                placeholder="e.g. Consultation and prescription for malaria"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="claim-amount">Amount you paid</Label>
              <Input
                id="claim-amount"
                type="number"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button disabled={!canSubmit || file.isPending} onClick={() => file.mutate()}>
              File claim
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
