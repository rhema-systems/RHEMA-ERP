'use client';

import React, { useCallback, useEffect, useState } from 'react';
import { CheckCircle2, CircleAlert, Landmark, RefreshCw } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringDesignService } from '@/services/civil-engineering-design.service';
import type {
  CivilEngineeringCommercialReadiness,
  CivilEngineeringDesignCase,
} from '@/types/civil-engineering-design';

const message = (error: unknown) => {
  const value = error as { response?: { detail?: string }; message?: string };
  return value.response?.detail || value.message || 'The commercial readiness view could not be loaded.';
};

const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : 'Not yet checked';

export function CivilEngineeringCommercialReadinessPanel({
  designCase,
}: {
  designCase: CivilEngineeringDesignCase;
}) {
  const { toast } = useToast();
  const [readiness, setReadiness] = useState<CivilEngineeringCommercialReadiness>();
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setReadiness(await civilEngineeringDesignService.commercialReadiness(designCase.id));
    } catch (error) {
      toast({
        title: 'Unable to load commercial readiness',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [designCase.id, toast]);

  useEffect(() => {
    setReadiness(undefined);
    void load();
  }, [load]);

  return (
    <div className="rounded-lg border p-4">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h4 className="flex items-center gap-2 font-medium">
            <Landmark className="h-4 w-4" /> Commercial readiness
          </h4>
          <p className="mt-1 text-sm text-muted-foreground">
            Live links to the project’s approved QS, budget, Procurement, contract and DMS records.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading}>
          <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          Revalidate
        </Button>
      </div>

      {!readiness && !loading ? (
        <p className="text-sm text-muted-foreground">Commercial readiness is unavailable.</p>
      ) : null}

      {readiness ? (
        <div className="space-y-4">
          <Alert variant={readiness.readyForExecution ? 'default' : 'destructive'}>
            {readiness.readyForExecution ? (
              <CheckCircle2 className="h-4 w-4" />
            ) : (
              <CircleAlert className="h-4 w-4" />
            )}
            <AlertTitle>
              {readiness.readyForExecution
                ? 'Commercial controls are ready for execution'
                : 'Commercial controls need attention'}
            </AlertTitle>
            <AlertDescription>
              Revalidated {formatDate(readiness.revalidatedAt)}. Civil does not copy or post these owner records.
            </AlertDescription>
          </Alert>

          <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
            {readiness.gates.map((gate) => (
              <div
                key={gate.code}
                className="rounded-md border p-3"
              >
                <div className="flex items-start justify-between gap-2">
                  <span className="font-medium text-sm">{gate.title}</span>
                  <Badge variant={gate.isSatisfied ? 'default' : 'destructive'}>
                    {gate.isSatisfied ? 'Current' : 'Action required'}
                  </Badge>
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{gate.detail}</p>
              </div>
            ))}
          </div>

          <div className="overflow-x-auto rounded-md border">
            <table className="w-full min-w-[720px] text-sm">
              <thead className="bg-muted/50 text-left text-xs text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 font-medium">Owner</th>
                  <th className="px-3 py-2 font-medium">Record</th>
                  <th className="px-3 py-2 font-medium">Reference</th>
                  <th className="px-3 py-2 font-medium">Status</th>
                  <th className="px-3 py-2 font-medium">Detail</th>
                </tr>
              </thead>
              <tbody>
                {readiness.links.map((link, index) => (
                  <tr key={`${link.recordType}-${link.recordId ?? index}`} className="border-t">
                    <td className="px-3 py-2 text-muted-foreground">{link.owner}</td>
                    <td className="px-3 py-2">{link.recordType}</td>
                    <td className="px-3 py-2 font-medium">{link.reference}</td>
                    <td className="px-3 py-2"><Badge variant="outline">{link.status}</Badge></td>
                    <td className="px-3 py-2 text-muted-foreground">{link.detail}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </div>
  );
}
