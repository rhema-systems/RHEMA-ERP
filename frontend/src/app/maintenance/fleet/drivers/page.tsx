'use client';

import React from 'react';
import {
  AlertTriangle,
  BadgeCheck,
  Eye,
  RefreshCw,
  Search,
  ShieldCheck,
  Truck,
  UserCheck,
  Users,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/hooks/use-toast';
import fleetService, {
  FleetDriverDirectoryDto,
  FleetDriverDto,
  FleetDriverSummaryDto,
} from '@/services/fleetService';

const EMPTY_SUMMARY: FleetDriverSummaryDto = {
  totalDrivers: 0,
  validLicenses: 0,
  expiringLicenses: 0,
  expiredLicenses: 0,
  missingLicenses: 0,
  unverifiedLicenses: 0,
  assignedDrivers: 0,
  engagedDrivers: 0,
};

const PAGE_SIZE = 25;

function formatDate(value?: string | null) {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(date);
}

function formatDateTime(value?: string | null) {
  if (!value) return 'No trips recorded';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

function LicenseStatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase();
  if (normalized === 'valid') return <Badge className="bg-emerald-100 text-emerald-800 hover:bg-emerald-100">Valid</Badge>;
  if (normalized === 'expiring') return <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">Expiring</Badge>;
  if (normalized === 'expired') return <Badge className="bg-red-100 text-red-800 hover:bg-red-100">Expired</Badge>;
  return <Badge variant="outline" className="border-slate-300 text-slate-700">{status}</Badge>;
}

function AvailabilityBadge({ status }: { status: string }) {
  return status.toLowerCase() === 'engaged'
    ? <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">Engaged</Badge>
    : <Badge className="bg-emerald-100 text-emerald-800 hover:bg-emerald-100">Available</Badge>;
}

function SummaryCard({
  title,
  value,
  description,
  icon: Icon,
}: {
  title: string;
  value: number;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
}) {
  return (
    <Card>
      <CardContent className="flex items-start justify-between p-4">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{title}</p>
          <p className="mt-1 text-2xl font-semibold">{value}</p>
          <p className="mt-1 text-xs text-muted-foreground">{description}</p>
        </div>
        <div className="rounded-lg bg-muted p-2"><Icon className="h-5 w-5" /></div>
      </CardContent>
    </Card>
  );
}

export default function FleetDriversPage() {
  const { toast } = useToast();
  const [loading, setLoading] = React.useState(true);
  const [page, setPage] = React.useState(1);
  const [searchInput, setSearchInput] = React.useState('');
  const [searchTerm, setSearchTerm] = React.useState('');
  const [licenseStatus, setLicenseStatus] = React.useState('All');
  const [assignmentStatus, setAssignmentStatus] = React.useState('All');
  const [selectedDriver, setSelectedDriver] = React.useState<FleetDriverDto | null>(null);
  const [result, setResult] = React.useState<FleetDriverDirectoryDto>({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: PAGE_SIZE,
    summary: EMPTY_SUMMARY,
  });

  const loadDrivers = React.useCallback(async () => {
    setLoading(true);
    try {
      const data = await fleetService.getDrivers({
        page,
        pageSize: PAGE_SIZE,
        searchTerm: searchTerm || undefined,
        licenseStatus,
        assigned: assignmentStatus === 'Assigned' ? true : assignmentStatus === 'Unassigned' ? false : undefined,
      });
      setResult(data);
    } catch (error: any) {
      toast({
        title: 'Failed to load Fleet drivers',
        description: error?.message || String(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [assignmentStatus, licenseStatus, page, searchTerm, toast]);

  React.useEffect(() => {
    void loadDrivers();
  }, [loadDrivers]);

  const applySearch = () => {
    setPage(1);
    setSearchTerm(searchInput.trim());
  };

  const totalPages = Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE));

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Drivers</h1>
          <p className="text-sm text-muted-foreground">
            Operational view of HR driver records, licence readiness, vehicle assignments, and trip activity.
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            Employee profiles and driver-licence changes remain managed by Human Resources.
          </p>
        </div>
        <Button variant="outline" onClick={() => void loadDrivers()} disabled={loading}>
          <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <SummaryCard title="Drivers" value={result.summary.totalDrivers} description="HR driver records" icon={Users} />
        <SummaryCard title="Valid licences" value={result.summary.validLicenses} description="Current beyond 30 days" icon={ShieldCheck} />
        <SummaryCard title="Expiring" value={result.summary.expiringLicenses} description="Due within 30 days" icon={AlertTriangle} />
        <SummaryCard title="Not eligible" value={result.summary.expiredLicenses + result.summary.missingLicenses + result.summary.unverifiedLicenses} description="Dispatch will be blocked" icon={BadgeCheck} />
        <SummaryCard title="Assigned" value={result.summary.assignedDrivers} description="Currently assigned to vehicles" icon={Truck} />
        <SummaryCard title="Engaged" value={result.summary.engagedDrivers} description="On dispatched trips" icon={UserCheck} />
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Driver directory</CardTitle>
          <CardDescription>Read-only Fleet view sourced from HR and active Fleet records.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 md:grid-cols-[minmax(260px,1fr)_200px_180px_auto]">
            <div className="flex gap-2">
              <Input
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                onKeyDown={(event) => event.key === 'Enter' && applySearch()}
                placeholder="Search driver, employee number, email..."
              />
              <Button onClick={applySearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={licenseStatus} onValueChange={(value) => { setLicenseStatus(value); setPage(1); }}>
              <SelectTrigger><SelectValue placeholder="Licence status" /></SelectTrigger>
              <SelectContent>
                {['All', 'Valid', 'Expiring', 'Expired', 'Unverified', 'Missing', 'Missing Expiry'].map((value) => (
                  <SelectItem key={value} value={value}>{value === 'All' ? 'All licence statuses' : value}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={assignmentStatus} onValueChange={(value) => { setAssignmentStatus(value); setPage(1); }}>
              <SelectTrigger><SelectValue placeholder="Assignment" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All assignments</SelectItem>
                <SelectItem value="Assigned">Assigned</SelectItem>
                <SelectItem value="Unassigned">Unassigned</SelectItem>
              </SelectContent>
            </Select>
            <Button
              variant="ghost"
              onClick={() => {
                setSearchInput('');
                setSearchTerm('');
                setLicenseStatus('All');
                setAssignmentStatus('All');
                setPage(1);
              }}
            >
              Clear
            </Button>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Driver</TableHead>
                  <TableHead>Licence</TableHead>
                  <TableHead>Expiry</TableHead>
                  <TableHead>Verification</TableHead>
                  <TableHead>Assigned vehicle</TableHead>
                  <TableHead>Availability</TableHead>
                  <TableHead>Trips</TableHead>
                  <TableHead className="w-20 text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow><TableCell colSpan={8} className="h-28 text-center text-muted-foreground">Loading drivers...</TableCell></TableRow>
                ) : result.items.length === 0 ? (
                  <TableRow><TableCell colSpan={8} className="h-28 text-center text-muted-foreground">No drivers match the selected criteria.</TableCell></TableRow>
                ) : result.items.map((driver) => (
                  <TableRow key={driver.employeeId}>
                    <TableCell>
                      <div className="font-medium">{driver.fullName}</div>
                      <div className="text-xs text-muted-foreground">{driver.employeeNumber} · {driver.positionTitle || 'Driver'}</div>
                    </TableCell>
                    <TableCell>
                      <div className="font-mono text-xs">{driver.driverLicenseNumber || 'Not recorded'}</div>
                      <div className="mt-1"><LicenseStatusBadge status={driver.licenseStatus} /></div>
                    </TableCell>
                    <TableCell>
                      <div>{formatDate(driver.licenseExpiryDate)}</div>
                      {driver.daysUntilLicenseExpiry != null && driver.daysUntilLicenseExpiry >= 0 && (
                        <div className="text-xs text-muted-foreground">{driver.daysUntilLicenseExpiry} days remaining</div>
                      )}
                    </TableCell>
                    <TableCell>
                      {driver.isLicenseVerified
                        ? <Badge className="bg-blue-100 text-blue-800 hover:bg-blue-100">Verified</Badge>
                        : <Badge variant="outline">Unverified</Badge>}
                    </TableCell>
                    <TableCell>
                      {driver.isAssigned ? (
                        <>
                          <div className="font-medium">{driver.currentVehicleName}</div>
                          <div className="text-xs text-muted-foreground">{driver.currentVehicleAssetNumber}</div>
                        </>
                      ) : <span className="text-muted-foreground">Unassigned</span>}
                    </TableCell>
                    <TableCell>
                      <AvailabilityBadge status={driver.availabilityStatus} />
                      {driver.activeTripId && (
                        <div className="mt-1 text-xs text-muted-foreground">
                          {driver.activeTripVehicleAssetNumber || driver.activeTripVehicleName || 'Active trip'}
                        </div>
                      )}
                    </TableCell>
                    <TableCell>
                      <div>{driver.totalTripCount} total</div>
                      <div className="text-xs text-muted-foreground">{driver.activeTripCount} engaged</div>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="icon" onClick={() => setSelectedDriver(driver)} aria-label={`View ${driver.fullName}`}>
                        <Eye className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">{result.totalCount} matching driver{result.totalCount === 1 ? '' : 's'}</span>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</Button>
              <span>Page {page} of {totalPages}</span>
              <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage((value) => Math.min(totalPages, value + 1))}>Next</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={!!selectedDriver} onOpenChange={(open) => !open && setSelectedDriver(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2"><UserCheck className="h-5 w-5" />{selectedDriver?.fullName}</DialogTitle>
            <DialogDescription>{selectedDriver?.employeeNumber} · HR-managed driver profile</DialogDescription>
          </DialogHeader>
          {selectedDriver && (
            <div className="grid gap-5 md:grid-cols-2">
              <div className="space-y-3 rounded-md border p-4">
                <h3 className="font-medium">Employee</h3>
                <Detail label="Position" value={selectedDriver.positionTitle || 'Driver'} />
                <Detail label="Department" value={selectedDriver.departmentName || 'Not recorded'} />
                <Detail label="Email" value={selectedDriver.emailAddress || 'Not recorded'} />
                <Detail label="Phone" value={selectedDriver.phoneNumber || 'Not recorded'} />
                <Detail label="HR status" value={`${selectedDriver.staffStatus}${selectedDriver.isActive ? '' : ' · Inactive'}`} />
              </div>
              <div className="space-y-3 rounded-md border p-4">
                <div className="flex items-center justify-between gap-2">
                  <h3 className="font-medium">Driver licence</h3>
                  <LicenseStatusBadge status={selectedDriver.licenseStatus} />
                </div>
                <Detail label="Number" value={selectedDriver.driverLicenseNumber || 'Not recorded'} />
                <Detail label="Issued" value={formatDate(selectedDriver.licenseIssueDate)} />
                <Detail label="Expires" value={formatDate(selectedDriver.licenseExpiryDate)} />
                <Detail label="Authority" value={selectedDriver.licenseIssuingAuthority || 'Not recorded'} />
                <Detail label="Verification" value={selectedDriver.isLicenseVerified ? `Verified ${formatDate(selectedDriver.licenseVerifiedDate)}` : 'Unverified'} />
              </div>
              <div className="space-y-3 rounded-md border p-4 md:col-span-2">
                <h3 className="font-medium">Fleet activity</h3>
                <div className="grid gap-3 sm:grid-cols-3">
                  <Detail label="Current vehicle" value={selectedDriver.isAssigned ? `${selectedDriver.currentVehicleName} (${selectedDriver.currentVehicleAssetNumber})` : 'Unassigned'} />
                  <Detail label="Availability" value={selectedDriver.availabilityStatus === 'Engaged' ? `Engaged on ${selectedDriver.activeTripVehicleName || selectedDriver.activeTripVehicleAssetNumber || 'a dispatched trip'}` : 'Available'} />
                  <Detail label="Trips" value={`${selectedDriver.totalTripCount} total · ${selectedDriver.activeTripCount} engaged`} />
                  <Detail label="Last trip" value={formatDateTime(selectedDriver.lastTripAtUtc)} />
                </div>
              </div>
              <div className="rounded-md bg-muted p-3 text-xs text-muted-foreground md:col-span-2">
                Fleet presents this operational view. Update the employee profile or driver licence through Human Resources so every module uses the same record.
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className="mt-0.5 text-sm">{value}</div>
    </div>
  );
}
