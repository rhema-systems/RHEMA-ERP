'use client';

import { useRouter } from 'next/navigation';
import {
  AlertTriangle,
  ArrowLeft,
  Boxes,
  FileCheck2,
  Landmark,
  LockKeyhole,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export default function CreateSupplierReturnUnavailablePage() {
  const router = useRouter();

  return (
    <div className="mx-auto max-w-5xl space-y-6 p-8">
      <div className="flex items-center gap-4">
        <Button
          variant="outline"
          size="icon"
          onClick={() => router.push('/finance/ap/returns')}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
            <LockKeyhole className="h-8 w-8 text-amber-600" /> Post-Acceptance
            Return to Vendor
          </h1>
          <p className="mt-1 text-muted-foreground">
            Planned integration boundary - creation is unavailable.
          </p>
        </div>
      </div>

      <Alert variant="destructive">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>The legacy Finance return wizard is quarantined</AlertTitle>
        <AlertDescription>
          It previously allowed Finance to imply Procurement approval, Inventory
          movement, supplier acceptance, and accounting completion in one
          action. No new return can be created here until the agreed FIN-INT-012
          and FIN-INT-013 producer/consumer contracts are implemented and
          verified.
        </AlertDescription>
      </Alert>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <FileCheck2 className="h-5 w-5 text-blue-600" /> 1. Procurement
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            Owns the approved Return-to-Vendor request, supplier/PO/GRN/RMA
            lineage, dispatch authority, and immutable evidence.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Boxes className="h-5 w-5 text-indigo-600" /> 2. Inventory
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            Owns the posted outbound quantity movement, lot/serial identity,
            valuation layers, carrying cost, and compensating corrections.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Landmark className="h-5 w-5 text-emerald-600" /> 3. Finance
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            Consumes FIN-INT-012 dispatch evidence, then separately consumes
            FIN-INT-013 supplier credit, refund, replacement, repair, or
            rejection evidence for AP, tax, cash, and GL treatment.
          </CardContent>
        </Card>
      </div>

      <Card className="border-amber-300 dark:border-amber-900">
        <CardHeader>
          <CardTitle className="text-base">No manual workaround</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm text-muted-foreground">
          <p>
            Do not use a manual Inventory adjustment plus a standalone Finance
            debit note as proof that Return to Vendor works. Those documents
            would not preserve the governed dispatch-to-commercial-resolution
            lineage.
          </p>
          <p>
            Receipt-stage rejection remains a Procurement inspection scenario.
            Internal damage, loss, expiry, or obsolescence without a supplier
            claim remains an Inventory adjustment/write-off scenario.
          </p>
          <Button
            variant="outline"
            onClick={() => router.push('/finance/ap/returns')}
          >
            Open historical register
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
