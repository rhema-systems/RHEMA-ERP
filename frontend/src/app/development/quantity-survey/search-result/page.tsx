'use client';

import React, { Suspense, useEffect, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { apiService } from '@/services/api.service';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

type RecordData = Record<string, unknown>;
const owners: Record<string, { endpoint: string; label: string; title: string[] }> = {
  'rate-library': { endpoint: '/quantity-survey/rate-library', label: 'Rate library item', title: ['code', 'name'] },
  'price-index-imports': { endpoint: '/quantity-survey/price-index-imports', label: 'Price-index import', title: ['indexFamilyCode', 'originalFileName'] },
  'escalation-formulas': { endpoint: '/quantity-survey/escalation-formulas', label: 'Escalation formula', title: ['code', 'name'] },
  'civil-task': { endpoint: '/civil-engineering/direct-tasks', label: 'Civil direct task', title: ['title'] },
  measurements: { endpoint: '/quantity-survey/measurements', label: 'Measurement sheet', title: ['sheetReference', 'title'] },
  'valuation-worksheets': { endpoint: '/quantity-survey/valuation-worksheets', label: 'Valuation worksheet', title: ['interimValuationLabel'] },
  variations: { endpoint: '/quantity-survey/variations', label: 'Variation', title: ['referenceNumber', 'title'] },
  'payment-certificates': { endpoint: '/quantity-survey/payment-certificates', label: 'Payment certificate', title: ['certificateNumber', 'title'] },
  catalogues: { endpoint: '/quantity-survey/catalogues', label: 'QS catalogue entry', title: ['code', 'name'] },
};
const fields: [string, string][] = [
  ['status', 'Status'], ['approvalStatus', 'Approval'], ['projectName', 'Project'],
  ['unitOfMeasureCode', 'Unit'], ['version', 'Version'], ['contractClauseReference', 'Contract clause'],
  ['baseDate', 'Base date'], ['authorityRoleName', 'Authority'], ['indexFamilyName', 'Index family'],
  ['originalFileName', 'Source file'], ['importFormat', 'Format'], ['lineCount', 'Rows'], ['errorCount', 'Errors'],
  ['assignedToName', 'Assigned to'], ['assignedRoleName', 'Role'], ['dueDate', 'Due date'],
  ['measurementDate', 'Measurement date'], ['siteLocation', 'Site location'],
  ['contractNumber', 'Contract'], ['contractorName', 'Contractor'], ['consultantName', 'Consultant'],
  ['issueDate', 'Issue date'], ['currency', 'Currency'], ['grossAmount', 'Gross amount'],
  ['netAmount', 'Net amount'], ['retentionAmount', 'Retention'], ['totalValue', 'Value'],
  ['currentClaimedValue', 'Claimed value'], ['currentCertifiedValue', 'Certified value'],
  ['valuedAmount', 'Valued amount'], ['approvedAmount', 'Approved amount'],
  ['grossCertifiedAmount', 'Gross certified'], ['previouslyCertifiedAmount', 'Previously certified'],
  ['certifiedToDateAmount', 'Certified to date'], ['retentionHeldAmount', 'Retention held'],
  ['retentionReleasedAmount', 'Retention released'], ['advanceRecoveryAmount', 'Advance recovery'],
  ['materialDeductionAmount', 'Material deduction'], ['otherDeductionsAmount', 'Other deductions'],
  ['taxAmount', 'Tax'], ['netCertifiedAmount', 'Net certified'], ['paymentDueDate', 'Payment due'],
  ['paymentStatus', 'Payment status'], ['vendorInvoiceNumber', 'Supplier invoice'],
  ['catalogType', 'Catalogue'], ['standardCode', 'Standard'], ['defaultUnitOfMeasure', 'Unit'],
  ['effectiveFrom', 'Effective from'], ['effectiveTo', 'Effective to'], ['isActive', 'Active'],
];
const columns: [string, string][] = [
  ['description', 'Description'], ['descriptionSnapshot', 'Description'], ['label', 'Item'],
  ['lineReference', 'Reference'], ['lineReferenceSnapshot', 'Reference'],
  ['reference', 'Reference'], ['unit', 'Unit'],
  ['unitOfMeasure', 'Unit'], ['quantity', 'Quantity'], ['quantityChange', 'Quantity change'],
  ['length', 'Length'], ['width', 'Width'], ['height', 'Height'], ['measuredQuantity', 'Measured'],
  ['timesing', 'Timesing'], ['formula', 'Formula'], ['calculatedQuantity', 'Calculated quantity'],
  ['isDeduction', 'Deduction'],
  ['currentClaimedQuantity', 'Claimed'], ['currentCertifiedQuantity', 'Certified'],
  ['unitRate', 'Rate'], ['amount', 'Amount'], ['currentCertifiedValue', 'Certified value'],
];
function text(value: unknown): string {
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  return typeof value === 'string' || typeof value === 'number' ? String(value) : '';
}

export function CivilQsSearchResult() {
  const params = useSearchParams();
  const kind = params.get('kind') || '';
  const id = params.get('recordId') || '';
  const owner = owners[kind];
  const [record, setRecord] = useState<RecordData | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    setRecord(null); setError('');
    if (!owner || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) {
      setError('This record link is invalid.'); return;
    }
    apiService.get<RecordData>(`${owner.endpoint}/${encodeURIComponent(id)}`)
      .then(value => { if (active) setRecord(value); })
      .catch(failure => { if (active) setError(getProcurementProblemMessage(failure, 'Unable to open this record.')); });
    return () => { active = false; };
  }, [id, owner]);

  if (error) return <div role="alert" className="rounded-lg border border-destructive/30 p-4 text-destructive">{error}</div>;
  if (!record || !owner) return <div role="status" className="p-6">Loading record…</div>;
  const rowSource = kind === 'rate-library' ? record.rates : kind === 'price-index-imports' ? record.values : kind === 'escalation-formulas' ? record.components : record.lines;
  const lines = Array.isArray(rowSource) ? rowSource.filter((line): line is RecordData => Boolean(line) && typeof line === 'object') : [];
  const recordColumns: [string, string][] = kind === 'rate-library'
    ? [['version', 'Version'], ['unitRate', 'Rate'], ['currencyCode', 'Currency'], ['effectiveFrom', 'Effective from'], ['effectiveTo', 'Effective to'], ['lifecycleStatus', 'Status']]
    : kind === 'price-index-imports' ? [['indexPeriod', 'Period'], ['indexValue', 'Index'], ['publicationDate', 'Published'], ['sourceReference', 'Source'], ['status', 'Status']]
      : kind === 'escalation-formulas' ? [['component', 'Component'], ['coefficient', 'Coefficient'], ['indexFamilyCode', 'Index family'], ['indexFamilyName', 'Name']] : columns;
  const visibleColumns = recordColumns.filter(([key]) => lines.some(line => text(line[key]) !== ''));
  const projectId = text(record.projectId);
  const returnUrl = kind === 'catalogues' ? '/administration/project-management/quantity-survey-catalogues'
    : kind === 'rate-library' ? '/administration/project-management/quantity-survey-rate-library'
      : kind === 'price-index-imports' || kind === 'escalation-formulas' ? '/administration/project-management/quantity-survey-escalation'
    : kind === 'civil-task' ? '/development/civil-engineering/direct-tasks'
      : projectId ? `/development/projects/${encodeURIComponent(projectId)}/commercial-admin` : '/development/projects';
  return <div className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h1 className="text-2xl font-semibold">{owner.title.map(key => text(record[key])).filter(Boolean).join(' · ') || owner.label}</h1><p className="text-sm text-muted-foreground">{owner.label}</p></div>
      <Button variant="outline" asChild><Link href={returnUrl}>{kind === 'catalogues' ? 'Open catalogue' : kind === 'civil-task' ? 'Task workspace' : ['rate-library', 'price-index-imports', 'escalation-formulas'].includes(kind) ? 'Open register' : 'Project workspace'}</Link></Button>
    </div>
    <Card><CardHeader><CardTitle>Details</CardTitle></CardHeader><CardContent>
      <dl className="grid grid-cols-2 gap-4 md:grid-cols-4">{fields.filter(([key]) => text(record[key]) !== '').map(([key, label]) => <div key={key}><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 text-sm">{text(record[key])}</dd></div>)}</dl>
      {['instructions', 'description', 'reason', 'notes', 'measurementRule'].filter(key => text(record[key])).map(key => <p key={key} className="mt-4 whitespace-pre-wrap text-sm">{text(record[key])}</p>)}
    </CardContent></Card>
    {lines.length > 0 && visibleColumns.length > 0 && <Card><CardHeader><CardTitle>Lines</CardTitle></CardHeader><CardContent className="overflow-x-auto"><Table>
      <TableHeader><TableRow>{visibleColumns.map(([key, label]) => <TableHead key={key}>{label}</TableHead>)}</TableRow></TableHeader>
      <TableBody>{lines.map((line, index) => <TableRow key={text(line.id) || index}>{visibleColumns.map(([key]) => <TableCell className="whitespace-pre-wrap" key={key}>{text(line[key]) || '—'}</TableCell>)}</TableRow>)}</TableBody>
    </Table></CardContent></Card>}
  </div>;
}

export default function CivilQsSearchResultPage() {
  return <Suspense fallback={<div role="status">Loading record…</div>}><CivilQsSearchResult /></Suspense>;
}
