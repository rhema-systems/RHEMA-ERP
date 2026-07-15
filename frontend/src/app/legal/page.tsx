'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  BadgeCheck,
  BookOpen,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  Loader2,
  Scale,
  ShieldCheck,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  legalProcedureService,
  type LegalProcedure,
} from '@/services/legal-procedure.service';

const fallbackLegalProcedures: LegalProcedure[] = [
  {
    title: 'Legal Department Procedure Manual',
    entityType: 'LegalProcedure',
    source: 'Procedure manual',
    summary:
      'General legal intake, review, drafting, approval, execution, and record keeping workflow.',
    icon: 'BookOpen',
    stageCount: 6,
    accent: 'slate',
  },
  {
    title: 'Mortgages',
    entityType: 'LegalMortgage',
    source: 'Mortgage SOP',
    summary:
      'Mortgage request review, document preparation, execution support, and completion tracking.',
    icon: 'FileSignature',
    stageCount: 7,
    accent: 'cyan',
  },
  {
    title: 'Mortgage In Principle',
    entityType: 'LegalMortgageInPrinciple',
    source: 'Mortgage in principle SOP',
    summary:
      'Initial mortgage review, legal checks, recommendation, and approval routing.',
    icon: 'FileCheck2',
    stageCount: 5,
    accent: 'emerald',
  },
  {
    title: 'Court Processes',
    entityType: 'LegalCourtProcess',
    source: 'Court process SOP',
    summary:
      'Court process receipt, review, response preparation, filing, hearing, and follow-up.',
    icon: 'Scale',
    stageCount: 7,
    accent: 'violet',
  },
  {
    title: 'Other Court Processes',
    entityType: 'LegalOtherCourtProcess',
    source: 'Other court process SOP',
    summary:
      'Non-standard court matters routed for legal action, evidence handling, and closure.',
    icon: 'Gavel',
    stageCount: 6,
    accent: 'purple',
  },
  {
    title: 'Termination / Recognition',
    entityType: 'LegalTerminationRecognition',
    source: 'Termination-recognition SOP',
    summary:
      'Termination and recognition requests reviewed through legal validation and approval stages.',
    icon: 'ShieldCheck',
    stageCount: 7,
    accent: 'amber',
  },
  {
    title: 'Assignment / Sublease / Vesting',
    entityType: 'LegalAssignmentSubleaseVesting',
    source: 'Assignment, sublease, and vesting SOP',
    summary:
      'Instrument review, party verification, drafting, consent checks, and completion workflow.',
    icon: 'Landmark',
    stageCount: 8,
    accent: 'teal',
  },
  {
    title: 'Leases / Deed of Variation / Renewal / Sublease',
    entityType: 'LegalLeaseVariationRenewalSublease',
    source: 'Lease and variation SOP',
    summary:
      'Lease drafting, variation, renewal, sublease review, approval, execution, and filing.',
    icon: 'FileText',
    stageCount: 8,
    accent: 'sky',
  },
  {
    title: 'Transfers',
    entityType: 'LegalTransfer',
    source: 'Transfer SOP',
    summary:
      'Transfer request validation, document review, approval, execution, registration, and records.',
    icon: 'BadgeCheck',
    stageCount: 7,
    accent: 'blue',
  },
];

const procedureIcons: Record<
  string,
  React.ComponentType<{ className?: string }>
> = {
  BadgeCheck,
  BookOpen,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  Scale,
  ShieldCheck,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  blue: 'text-blue-700 dark:text-blue-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  purple: 'text-purple-700 dark:text-purple-300',
  sky: 'text-sky-700 dark:text-sky-300',
  slate: 'text-slate-700 dark:text-slate-200',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
};

export default function LegalProceduresPage() {
  const router = useRouter();
  const [procedures, setProcedures] = React.useState<LegalProcedure[]>(
    fallbackLegalProcedures
  );
  const [isLoading, setIsLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedures = async () => {
      try {
        const data = await legalProcedureService.getProcedures();
        if (mounted && data.length > 0) {
          setProcedures(data);
        }
      } catch {
        if (mounted) {
          setProcedures(fallbackLegalProcedures);
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
          <Badge variant="outline" className="w-fit">
            Legal procedures
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Legal Procedures
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
              Procedure workspaces for legal instruments, court processes,
              mortgages, transfers, leases, and recognition matters.
            </p>
          </div>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
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
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                      <Icon className={`h-5 w-5 ${accent}`} />
                    </div>
                    <Badge variant="secondary">
                      {procedure.stageCount} stages
                    </Badge>
                  </div>
                  <div>
                    <CardTitle className="text-base leading-6">
                      {procedure.title}
                    </CardTitle>
                    <CardDescription className="mt-1">
                      {procedure.source}
                    </CardDescription>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <p className="text-sm leading-6 text-muted-foreground">
                    {procedure.summary}
                  </p>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{procedure.entityType}</Badge>
                    <Badge variant="outline">Procedure workspace</Badge>
                  </div>
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
