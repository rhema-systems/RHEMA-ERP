'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  BadgeCheck,
  BarChart3,
  BookOpen,
  Briefcase,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  Loader2,
  MessageSquare,
  Scale,
  ShieldCheck,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  legalProcedureService,
  type LegalProcedure,
} from '@/services/legal-procedure.service';

const procedureIcons: Record<
  string,
  React.ComponentType<{ className?: string }>
> = {
  BadgeCheck,
  BookOpen,
  Briefcase,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  MessageSquare,
  Scale,
  ShieldCheck,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  blue: 'text-blue-700 dark:text-blue-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  indigo: 'text-indigo-700 dark:text-indigo-300',
  purple: 'text-purple-700 dark:text-purple-300',
  sky: 'text-sky-700 dark:text-sky-300',
  slate: 'text-slate-700 dark:text-slate-200',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
  zinc: 'text-zinc-700 dark:text-zinc-300',
};

export default function LegalProceduresPage() {
  const router = useRouter();
  const [procedures, setProcedures] = React.useState<LegalProcedure[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedures = async () => {
      try {
        const data = await legalProcedureService.getProcedures();
        if (mounted) {
          setProcedures(data);
          setLoadError(data.length === 0 ? 'No legal procedures were returned by the API.' : null);
        }
      } catch {
        if (mounted) {
          setProcedures([]);
          setLoadError('Unable to load legal procedures from the API.');
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadProcedures();

    return () => {
      mounted = false;
    };
  }, []);

  const openWorkspace = (entityType: string) => {
    router.push(`/legal/${encodeURIComponent(entityType)}`);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Legal Matters
            </h1>
          </div>
        </div>
        <Button
          variant="outline"
          className="w-fit gap-2"
          onClick={() => router.push('/legal/dashboard')}
        >
          <BarChart3 className="h-4 w-4" />
          Legal dashboard
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {!isLoading && loadError ? (
          <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
            <CardContent className="py-12 text-center text-sm text-muted-foreground">
              {loadError}
            </CardContent>
          </Card>
        ) : null}
        {isLoading ? (
          <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
            <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading legal procedures
            </CardContent>
          </Card>
        ) : null}
        {!isLoading &&
          procedures.map((procedure) => {
            const Icon = procedureIcons[procedure.icon] || FileText;
            const accent =
              accentClasses[procedure.accent] || accentClasses.slate;
            return (
              <Card
                key={procedure.entityType}
                className="border-border bg-card text-card-foreground"
              >
                <CardHeader className="space-y-3">
                  <div className="flex items-start gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                      <Icon className={`h-5 w-5 ${accent}`} />
                    </div>
                  </div>
                  <div>
                    <CardTitle className="text-base leading-6">
                      {procedure.title}
                    </CardTitle>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <Button
                    variant="outline"
                    className="w-full justify-between"
                    onClick={() => openWorkspace(procedure.entityType)}
                  >
                    Open workspace
                    <ArrowRight className="h-4 w-4" />
                  </Button>
                </CardContent>
              </Card>
            );
          })}
      </div>
    </div>
  );
}
