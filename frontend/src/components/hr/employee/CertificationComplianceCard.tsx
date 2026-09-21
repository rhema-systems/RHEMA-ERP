'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { certificationService } from '@/services/hr/certification.service';
import type { EmployeeCertificationComplianceLine } from '@/types/hr/certification';

const LINE_STYLES: Record<EmployeeCertificationComplianceLine['status'], string> = {
  Held: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  ExpiringSoon: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  Expired: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
  Revoked: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
  Missing: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

function lineSuffix(line: EmployeeCertificationComplianceLine): string {
  switch (line.status) {
    case 'Held':
      return line.daysUntilExpiry != null && line.daysUntilExpiry < 60
        ? ` · expires in ${line.daysUntilExpiry}d`
        : ' · held';
    case 'ExpiringSoon':
      return ` · expires in ${line.daysUntilExpiry ?? '?'}d`;
    case 'Expired':
      return ' · expired';
    case 'Revoked':
      return ' · revoked';
    default:
      return ' · not held';
  }
}

/**
 * What the employee's position requires against what they hold — required, held, expiring,
 * expired, missing — the same shape as the document compliance strip (demo feedback round 2,
 * lane C2). Shown on the Overview and at the top of the certification tab.
 *
 * Renders nothing when the position requires nothing and the employee holds nothing that is
 * expiring or expired: a strip that says "nothing to say" on every profile is noise.
 */
export function CertificationComplianceCard({ employeeId, compact }: { employeeId: string; compact?: boolean }) {
  const { data: compliance } = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'certification-compliance'],
    queryFn: () => certificationService.getCompliance(employeeId),
  });

  if (!compliance) return null;
  const hasRequirements = compliance.lines.length > 0;
  const hasWarnings = compliance.expiringSoonCount > 0 || compliance.expiredCount > 0;
  if (!hasRequirements && !hasWarnings) return null;

  const ok = compliance.isCompliant && compliance.expiredCount === 0;

  return (
    <Card className={ok ? undefined : 'border-amber-400'}>
      <CardHeader className={compact ? 'pb-2' : 'pb-3'}>
        <CardTitle className="flex items-center gap-2 text-base">
          {ok ? (
            <CheckCircle2 className="h-4 w-4 text-emerald-600" />
          ) : (
            <AlertTriangle className="h-4 w-4 text-amber-600" />
          )}
          {hasRequirements
            ? `Certifications required for ${compliance.positionTitle ?? 'this position'}`
            : 'Certifications'}
        </CardTitle>
        <CardDescription>
          {hasRequirements && compliance.mandatoryCount > 0 && (
            <>
              {compliance.mandatorySatisfiedCount} of {compliance.mandatoryCount} mandatory credential
              {compliance.mandatoryCount === 1 ? '' : 's'} held.{' '}
            </>
          )}
          {hasRequirements && compliance.mandatoryCount === 0 && 'Every requirement here is optional. '}
          {compliance.expiringSoonCount > 0 && (
            <>
              {compliance.expiringSoonCount} expiring soon.{' '}
            </>
          )}
          {compliance.expiredCount > 0 && <>{compliance.expiredCount} expired.</>}
        </CardDescription>
      </CardHeader>
      {hasRequirements && (
        <CardContent className="flex flex-wrap gap-2">
          {compliance.lines.map((line) => (
            <Badge key={line.certificationId} variant="secondary" className={LINE_STYLES[line.status]}>
              {line.certificationName}
              {lineSuffix(line)}
              {!line.isMandatory && ' · optional'}
            </Badge>
          ))}
        </CardContent>
      )}
    </Card>
  );
}
