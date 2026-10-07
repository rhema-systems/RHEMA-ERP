'use client';

import { Badge } from '../ui/badge';
import { Button } from '../ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import {
  Building,
  Calendar,
  CircleDollarSign,
  Clock,
  Globe,
  Mail,
  MapPin,
  Palette,
  Phone,
  Settings,
  ShieldCheck,
  UserRoundCheck,
  Wifi,
  WifiOff,
} from 'lucide-react';
import type { Tenant } from '../../services/admin-api.service';

interface TenantOverviewProps {
  tenant: Tenant;
  isOpen: boolean;
  onClose: () => void;
  onEdit?: (tenant: Tenant) => void;
}

const formatDate = (value?: Date) => value
  ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(value)
  : 'Not set';

const formatDateOnly = (value?: Date) => value
  ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(value)
  : 'Not set';

export function TenantOverview({ tenant, isOpen, onClose, onEdit }: TenantOverviewProps) {
  if (!isOpen) return null;

  const audience = tenant.userAudience === 1
    ? 'Internal users'
    : tenant.userAudience === 2
      ? 'External users'
      : 'Internal and external users';

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
        <DialogHeader className="border-b pb-4">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 items-center justify-center overflow-hidden rounded-lg border bg-muted">
                {tenant.logoUrl
                  ? <img src={tenant.logoUrl} alt="" className="h-full w-full object-contain" />
                  : <Building className="h-6 w-6 text-muted-foreground" />}
              </div>
              <div>
                <DialogTitle className="text-xl">{tenant.name}</DialogTitle>
                <DialogDescription className="mt-1 flex items-center gap-2">
                  <span className="font-mono">{tenant.code}</span>
                  <Badge variant={tenant.isActive ? 'default' : 'secondary'}>{tenant.status}</Badge>
                </DialogDescription>
              </div>
            </div>
            {onEdit && (
              <Button variant="outline" size="sm" onClick={() => onEdit(tenant)}>
                <Settings className="mr-2 h-4 w-4" />Edit tenant
              </Button>
            )}
          </div>
        </DialogHeader>

        <div className="space-y-6 pt-2">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Card>
              <CardContent className="p-4">
                <ShieldCheck className="mb-3 h-6 w-6 text-blue-600" />
                <p className="text-sm text-muted-foreground">Tenant status</p>
                <p className="mt-1 font-semibold">{tenant.status}</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4">
                <Calendar className="mb-3 h-6 w-6 text-violet-600" />
                <p className="text-sm text-muted-foreground">Subscription ends</p>
                <p className="mt-1 font-semibold">{formatDateOnly(tenant.subscriptionEndDate)}</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4">
                <CircleDollarSign className="mb-3 h-6 w-6 text-emerald-600" />
                <p className="text-sm text-muted-foreground">Base currency</p>
                <p className="mt-1 font-semibold">{tenant.baseCurrency || 'Not set'}</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4">
                {tenant.ldapEnabled
                  ? <Wifi className="mb-3 h-6 w-6 text-green-600" />
                  : <WifiOff className="mb-3 h-6 w-6 text-slate-400" />}
                <p className="text-sm text-muted-foreground">Directory login</p>
                <p className="mt-1 font-semibold">{tenant.ldapEnabled ? 'Enabled' : 'Disabled'}</p>
              </CardContent>
            </Card>
          </div>

          <div className="grid gap-6 lg:grid-cols-2">
            <Card>
              <CardHeader><CardTitle className="flex items-center gap-2 text-base"><Building className="h-5 w-5" />Tenant details</CardTitle></CardHeader>
              <CardContent className="space-y-3 text-sm">
                <Detail label="Description" value={tenant.description || 'Not provided'} />
                <Detail label="Domain" value={tenant.domain || 'Not set'} icon={<Globe className="h-4 w-4" />} />
                <Detail label="Created" value={formatDate(tenant.createdAt)} icon={<Clock className="h-4 w-4" />} />
                <Detail label="Last updated" value={formatDate(tenant.updatedAt)} icon={<Clock className="h-4 w-4" />} />
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="flex items-center gap-2 text-base"><Mail className="h-5 w-5" />Contact information</CardTitle></CardHeader>
              <CardContent className="space-y-3 text-sm">
                <Detail label="Email" value={tenant.contactEmail || 'Not provided'} icon={<Mail className="h-4 w-4" />} />
                <Detail label="Phone" value={tenant.contactPhone || 'Not provided'} icon={<Phone className="h-4 w-4" />} />
                <Detail label="Address" value={tenant.address || 'Not provided'} icon={<MapPin className="h-4 w-4" />} />
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="flex items-center gap-2 text-base"><UserRoundCheck className="h-5 w-5" />Access and registration</CardTitle></CardHeader>
              <CardContent className="grid gap-4 text-sm sm:grid-cols-2">
                <Setting label="Audience" value={audience} />
                <Setting label="Self registration" value={tenant.allowSelfRegistration ? 'Enabled' : 'Disabled'} />
                <Setting label="Email verification" value={tenant.requireEmailVerification ? 'Required' : 'Optional'} />
                <Setting label="Auto selection" value={tenant.enableAutoSelection ? 'Enabled' : 'Disabled'} />
                <Setting label="Default for public users" value={tenant.isDefaultForPublicUsers ? 'Yes' : 'No'} />
                <Setting label="Default for internal users" value={tenant.isDefaultForInternalUsers ? 'Yes' : 'No'} />
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="flex items-center gap-2 text-base"><Palette className="h-5 w-5" />Branding and finance</CardTitle></CardHeader>
              <CardContent className="grid gap-4 text-sm sm:grid-cols-2">
                <Setting label="Primary colour" value={tenant.primaryColor || 'Not set'} />
                <Setting label="Secondary colour" value={tenant.secondaryColor || 'Not set'} />
                <Setting label="Currency name" value={tenant.baseCurrencyName || 'Not set'} />
                <Setting label="Currency symbol" value={tenant.currencySymbol || 'Not set'} />
                <Setting label="Decimal places" value={String(tenant.currencyDecimalPlaces ?? 2)} />
                <Setting label="Welcome message" value={tenant.welcomeMessage || 'Not set'} />
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader><CardTitle className="flex items-center gap-2 text-base"><Calendar className="h-5 w-5" />Subscription window</CardTitle></CardHeader>
            <CardContent className="grid gap-4 text-sm sm:grid-cols-2">
              <Setting label="Starts" value={formatDateOnly(tenant.subscriptionStartDate)} />
              <Setting label="Ends" value={formatDateOnly(tenant.subscriptionEndDate)} />
            </CardContent>
          </Card>
          <p className="text-xs text-muted-foreground">
            This view contains persisted tenant configuration only. Operational usage, storage, uptime, and activity are omitted until authoritative telemetry is available.
          </p>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function Detail({ label, value, icon }: { label: string; value: string; icon?: React.ReactNode }) {
  return (
    <div className="flex items-start gap-3">
      <span className="mt-0.5 text-muted-foreground">{icon}</span>
      <div><div className="text-muted-foreground">{label}</div><div className="break-words font-medium">{value}</div></div>
    </div>
  );
}

function Setting({ label, value }: { label: string; value: string }) {
  return <div><div className="text-muted-foreground">{label}</div><div className="mt-1 font-medium">{value}</div></div>;
}

export default TenantOverview;
