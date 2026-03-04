'use client';

import React from 'react';
import Link from 'next/link';
import { ExternalLink } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';

import fleetService, { FleetFuelTransactionDto, FleetVehicleListDto } from '@/services/fleetService';
import { formatFleetDateTime } from '@/lib/date-format';

export default function FleetFuelPage() {
  const { toast } = useToast();

  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [vehicleId, setVehicleId] = React.useState<string>('');

  const [loading, setLoading] = React.useState(false);
  const [items, setItems] = React.useState<FleetFuelTransactionDto[]>([]);

  const loadVehicles = React.useCallback(async () => {
    try {
      const res = await fleetService.getVehicles({ page: 1, pageSize: 200 });
      setVehicles(res.items || []);
    } catch (e: any) {
      toast({ title: 'Failed to load vehicles', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const loadFuel = React.useCallback(async () => {
    if (!vehicleId) return;
    setLoading(true);
    try {
      const res = await fleetService.getFuel(vehicleId, 1, 200);
      setItems(res.items || []);
    } catch (e: any) {
      toast({ title: 'Failed to load fuel transactions', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [vehicleId, toast]);

  React.useEffect(() => {
    loadVehicles();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  React.useEffect(() => {
    loadFuel();
  }, [loadFuel]);

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Fuel (Reports)</h1>
          <p className="text-muted-foreground">Fuel is captured from Trips. This page is read-only.</p>
        </div>

        <Button asChild variant="outline">
          <Link href="/maintenance/fleet/trips">
            <ExternalLink className="mr-2 h-4 w-4" />
            Go to Trips
          </Link>
        </Button>
      </div>

      <Card>
        <CardHeader className="space-y-3">
          <CardTitle>Vehicle</CardTitle>
          <Select value={vehicleId || undefined} onValueChange={(v) => setVehicleId(v)}>
            <SelectTrigger className="w-full md:w-[420px]">
              <SelectValue placeholder="Select vehicle" />
            </SelectTrigger>
            <SelectContent>
              {vehicles.map((v) => (
                <SelectItem key={v.id} value={v.id}>
                  {v.assetNumber} — {v.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40">
                  <TableHead>Trip</TableHead>
                  <TableHead>Fuelled At</TableHead>
                  <TableHead>Qty</TableHead>
                  <TableHead>Unit Cost</TableHead>
                  <TableHead>Total</TableHead>
                  <TableHead>Vendor</TableHead>
                  <TableHead>Receipt</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {!vehicleId ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      Select a vehicle to view fuel transactions
                    </TableCell>
                  </TableRow>
                ) : loading ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                      No fuel transactions
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((i, idx) => (
                    <TableRow key={i.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                      <TableCell>
                        {i.fleetTripId ? (
                          <Button asChild variant="link" size="sm" className="h-auto p-0">
                            <Link href={`/maintenance/fleet/trips?id=${i.fleetTripId}`}>View trip</Link>
                          </Button>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>{formatFleetDateTime(i.fuelledAt)}</TableCell>
                      <TableCell>
                        {i.quantity} {i.unit}
                      </TableCell>
                      <TableCell>{i.unitCost ?? '—'}</TableCell>
                      <TableCell>{i.totalCost ?? '—'}</TableCell>
                      <TableCell>{i.vendorName || '—'}</TableCell>
                      <TableCell>{i.receiptReference || '—'}</TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

