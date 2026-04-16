import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { ProjectCommercialSummaryDto } from '@/services/projectService';

type ProjectCommercialTabProps = {
  commercialSummary: ProjectCommercialSummaryDto | null;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
};

const getAlertTone = (severity?: string) => {
  switch ((severity || '').toLowerCase()) {
    case 'high':
    case 'critical':
      return 'border-red-200 bg-red-50 text-red-700';
    case 'medium':
      return 'border-amber-200 bg-amber-50 text-amber-700';
    default:
      return 'border-slate-200 bg-slate-50 text-slate-700';
  }
};

export function ProjectCommercialTab({ commercialSummary, formatMoney }: ProjectCommercialTabProps) {
  if (!commercialSummary) {
    return (
      <div className="space-y-6">
        <Card>
          <CardContent className="py-10 text-sm text-muted-foreground">
            Commercial summary is not available for this project yet.
          </CardContent>
        </Card>
      </div>
    );
  }

  const currency = commercialSummary.currency;

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Currency Basis</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Project Base Currency</div>
              <div className="mt-2 text-2xl font-semibold">{currency}</div>
              <div className="text-sm text-muted-foreground">Commercial totals are reported in this currency.</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Work Component Totals</div>
              <div className="mt-2 font-medium">{commercialSummary.packageConversionBasis}</div>
              <div className="text-sm text-muted-foreground">Planning and live work component controls use the latest active rates.</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Document Totals</div>
              <div className="mt-2 font-medium">{commercialSummary.documentConversionBasis}</div>
              <div className="text-sm text-muted-foreground">Variations, valuations, certificates, and final account use document dates.</div>
            </div>
          </div>

          <div className={`rounded-lg border p-4 text-sm ${commercialSummary.hasConversionGaps ? 'border-amber-200 bg-amber-50 text-amber-800' : 'border-emerald-200 bg-emerald-50 text-emerald-800'}`}>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">Missing Exchange Rates</Badge>
              <span className="font-medium">{commercialSummary.missingExchangeRateCount}</span>
            </div>
            <div className="mt-2">
              {commercialSummary.hasConversionGaps
                ? 'Some commercial source amounts could not be converted into the project base currency and remain included at source value until rates are configured.'
                : 'All commercial amounts were converted successfully into the project base currency.'}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Commercial Snapshot</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Forecast</div>
              <div className="text-2xl font-semibold">{formatMoney(commercialSummary.packageForecastAmount, currency)}</div>
              <div className="text-sm text-muted-foreground">{commercialSummary.packageCount} work component(s)</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Budget</div>
              <div className="text-2xl font-semibold">{formatMoney(commercialSummary.packageBudgetAmount, currency)}</div>
              <div className="text-sm text-muted-foreground">Commercial budget baseline</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Variance</div>
              <div className="text-2xl font-semibold">{formatMoney(commercialSummary.forecastVarianceAmount, currency)}</div>
              <div className="text-sm text-muted-foreground">Forecast versus budget</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Actual</div>
              <div className="text-2xl font-semibold">{formatMoney(commercialSummary.packageActualAmount, currency)}</div>
              <div className="text-sm text-muted-foreground">{commercialSummary.boqItemCount} BOQ line(s)</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Committed</div>
              <div className="text-2xl font-semibold">{formatMoney(commercialSummary.packageCommittedAmount, currency)}</div>
              <div className="text-sm text-muted-foreground">Procurement exposure</div>
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Tender-linked</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.tenderLinkedPackageCount}</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Contract-linked</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.contractLinkedPackageCount}</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">PR-linked</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.purchaseRequisitionLinkedPackageCount}</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">PO-linked</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.purchaseOrderLinkedPackageCount}</div>
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Variation Orders</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.variationOrderCount}</div>
              <div className="text-sm text-muted-foreground">{formatMoney(commercialSummary.approvedVariationAmount, currency)}</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Interim Valuations</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.interimValuationCount}</div>
              <div className="text-sm text-muted-foreground">{formatMoney(commercialSummary.netValuationAmount, currency)}</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Certificates</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.paymentCertificateCount}</div>
              <div className="text-sm text-muted-foreground">{formatMoney(commercialSummary.netCertifiedAmount, currency)}</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Retention Held</div>
              <div className="mt-2 text-2xl font-semibold">{formatMoney(commercialSummary.retentionHeldAmount, currency)}</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Approved EOT</div>
              <div className="mt-2 text-2xl font-semibold">{commercialSummary.extensionOfTimeCount}</div>
              <div className="text-sm text-muted-foreground">{commercialSummary.approvedExtensionDays} day(s)</div>
            </div>
            <div className="rounded-lg border p-4 text-sm">
              <div className="text-muted-foreground">Final Account</div>
              <div className="mt-2 text-2xl font-semibold">{formatMoney(commercialSummary.finalAccountValue, currency)}</div>
              <div className="text-sm text-muted-foreground">{commercialSummary.finalAccountStatus || 'Not started'}</div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Commercial Alerts</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {commercialSummary.alerts.length === 0 ? (
            <div className="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">
              <CheckCircle2 className="h-4 w-4" />
              No commercial alerts are currently flagged.
            </div>
          ) : (
            commercialSummary.alerts.map((alert, index) => (
              <div key={`${alert.severity}-${index}`} className={`flex items-start gap-3 rounded-lg border p-4 text-sm ${getAlertTone(alert.severity)}`}>
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <div className="space-y-1">
                  <div className="font-medium">{alert.severity}</div>
                  <div>{alert.message}</div>
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Phase Rollup</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {commercialSummary.phaseRollups.length === 0 ? (
            <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
              No work component phases have been costed yet.
            </div>
          ) : (
            commercialSummary.phaseRollups.map((phase) => (
              <div key={phase.projectPhaseId || phase.phaseName} className="rounded-lg border p-4 space-y-3">
                <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="font-medium">{phase.phaseName}</div>
                    <Badge variant="outline">{phase.packageCount} work component(s)</Badge>
                    <Badge variant="secondary">{phase.boqItemCount} BOQ item(s)</Badge>
                  </div>
                  <div className="text-sm text-muted-foreground">Variance {formatMoney(phase.varianceAmount, currency)}</div>
                </div>
                <div className="grid gap-4 md:grid-cols-4">
                  <div>
                    <div className="text-sm text-muted-foreground">Budget</div>
                    <div className="text-lg font-semibold">{formatMoney(phase.budgetAmount, currency)}</div>
                  </div>
                  <div>
                    <div className="text-sm text-muted-foreground">Committed</div>
                    <div className="text-lg font-semibold">{formatMoney(phase.committedAmount, currency)}</div>
                  </div>
                  <div>
                    <div className="text-sm text-muted-foreground">Actual</div>
                    <div className="text-lg font-semibold">{formatMoney(phase.actualAmount, currency)}</div>
                  </div>
                  <div>
                    <div className="text-sm text-muted-foreground">Forecast</div>
                    <div className="text-lg font-semibold">{formatMoney(phase.forecastAmount, currency)}</div>
                  </div>
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
