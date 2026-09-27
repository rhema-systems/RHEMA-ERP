'use client';

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/hooks/use-auth';
import { useTenant } from '@/contexts/TenantContext';
import { apiService } from '@/services/api.service';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

interface IdentitySummary { id: string; name: string; username?: string; description?: string | null; status: string }

export function IdentitySearchSummary({ kind }: { kind: 'users' | 'roles' }) {
  const params = useParams<{ id: string | string[] }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const { user, hasAnyRole } = useAuth();
  const { currentTenantCode } = useTenant();
  const allowed = hasAnyRole(['TenantAdmin', 'SuperAdmin']);
  const [record, setRecord] = useState<IdentitySummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let active = true;
    setRecord(null);
    setError(null);
    if (!allowed) return;
    const endpoint = kind === 'users'
      ? `/administration/user-tenant-mappings/current/users/${encodeURIComponent(id)}`
      : `/Role/summaries/${encodeURIComponent(id)}`;
    void apiService.get<IdentitySummary>(endpoint).then(value => {
      if (!active) return;
      if (!value || value.id !== id) setError('Record was not found.');
      else setRecord(value);
    }).catch(reason => {
      if (active) setError(reason instanceof Error ? reason.message : 'Unable to load this record.');
    });
    return () => { active = false; };
  }, [id, kind, allowed, user?.id, currentTenantCode]);

  return <div className="space-y-6 p-6">
    <h1 className="text-2xl font-semibold">{kind === 'users' ? 'Tenant user' : 'Role'}</h1>
    {!allowed ? <p role="alert">You do not have permission to view this record.</p> : error ?
      <p role="alert" className="text-destructive">{error}</p> : !record ? <p role="status">Loading…</p> :
        <Card>
          <CardHeader className="flex-row items-center justify-between gap-4">
            <CardTitle>{record.name || record.username}</CardTitle>
            <Badge variant="outline">{record.status}</Badge>
          </CardHeader>
          {(record.username || record.description) && <CardContent>
            {record.username && <p>{record.username}</p>}
            {record.description && <p className="whitespace-pre-wrap break-words">{record.description}</p>}
          </CardContent>}
        </Card>}
    <Link href={`/administration/identity-management/${kind}`} className="text-sm text-primary underline">Back to {kind}</Link>
  </div>;
}
