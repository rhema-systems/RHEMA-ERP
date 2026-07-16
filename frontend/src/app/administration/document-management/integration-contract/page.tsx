'use client';

import Link from 'next/link';
import {
  BookTemplate,
  CheckCircle2,
  FileText,
  GitBranch,
  ShieldCheck,
  Workflow,
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

const requiredPackage = [
  'Source module',
  'Source label',
  'Source entity type',
  'Source record ID',
  'Source record reference',
  'Document title',
  'Document type',
  'Repository path or file reference',
  'Metadata template code',
  'Version number',
  'Annotation requirement',
  'Access profile',
  'Retention policy',
];

const sourceContracts = [
  {
    module: 'Estate / Facilities',
    sourceLabel: 'Source: Estate / Facilities -> Central DMS',
    status: 'Reference pattern active',
    ownerAction:
      'Estate and Facilities publish uploaded source documents into Central DMS and retain their business record ownership.',
    injectionNeeded: 'None for current Estate land document flow.',
  },
  {
    module: 'Estate / Property Management',
    sourceLabel: 'Source: Estate / Property Management -> Central DMS',
    status: 'Our side to prepare',
    ownerAction:
      'Property and unit documents should publish from Estate/Property records after project handover is received.',
    injectionNeeded:
      'Project Management only needs to provide property/unit handover document references later.',
  },
  {
    module: 'Project Management',
    sourceLabel: 'Source: Project Management -> Central DMS',
    status: 'External module contract',
    ownerAction:
      'Project keeps project delivery ownership and sends approved handover packages or deliverable documents to Central DMS.',
    injectionNeeded:
      'Expose project ID, deliverable/unit reference, document type, file reference, completion approval, and receiving module.',
  },
  {
    module: 'Maintenance Management',
    sourceLabel: 'Source: Maintenance Management -> Central DMS',
    status: 'External module contract',
    ownerAction:
      'Maintenance owns job cards, work orders, inspections, parts, and technical closure; DMS receives evidence and approved documents.',
    injectionNeeded:
      'Expose job card/work order reference, asset/location, completion status, inspection result, document type, and file reference.',
  },
  {
    module: 'Helpdesk / Complaint Management',
    sourceLabel: 'Source: Helpdesk / Complaint Management -> Central DMS',
    status: 'External module contract',
    ownerAction:
      'Helpdesk owns ticket SLA, investigation, escalation, resolution, and closure; DMS receives ticket evidence and correspondence.',
    injectionNeeded:
      'Expose ticket reference, complainant/context, evidence type, resolution status, document type, and file reference.',
  },
  {
    module: 'Finance AR / AP',
    sourceLabel: 'Source: Finance -> Central DMS',
    status: 'External module contract',
    ownerAction:
      'Finance owns invoices, receipts, payments, statements, postings, and balances; DMS receives approved financial evidence.',
    injectionNeeded:
      'Expose invoice/receipt/payment reference, customer/supplier, posting reference, period, amount context, document type, and file reference.',
  },
  {
    module: 'HR / Payroll',
    sourceLabel: 'Source: HR / Payroll -> Central DMS',
    status: 'External module contract',
    ownerAction:
      'HR owns employee records, payroll documents, and staff lifecycle; DMS receives controlled employee document references.',
    injectionNeeded:
      'Expose employee reference, document category, confidentiality profile, effective date, retention rule, and file reference.',
  },
  {
    module: 'Procurement / Legal',
    sourceLabel: 'Source: Procurement / Legal -> Central DMS',
    status: 'External module contract',
    ownerAction:
      'Procurement owns supplier purchasing records; Legal owns instruments, matters, legal holds, and executed documents.',
    injectionNeeded:
      'Expose supplier/matter/contract reference, execution or approval status, legal hold flag, document type, and file reference.',
  },
];

const lifecycleSteps = [
  {
    title: 'Source Module Owns The Business Record',
    detail:
      'The owning module keeps its workflow, approvals, status, and business rules.',
  },
  {
    title: 'Source Publishes A Document Package',
    detail:
      'The module sends document metadata, source reference, and file/repository reference to Central DMS.',
  },
  {
    title: 'Central DMS Creates Or Reuses A Record',
    detail:
      'DMS checks duplicates, assigns document reference, applies template, access, retention, and version state.',
  },
  {
    title: 'DMS Returns The Reference',
    detail:
      'The source module stores the DMS record ID/reference for future viewing, annotation, comments, and audit.',
  },
];

export default function DmsIntegrationContractPage() {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Administration / DMS setup
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              DMS Integration Contract
            </h1>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/administration/document-management/metadata-templates">
              <BookTemplate className="mr-2 h-4 w-4" />
              Metadata setup
            </Link>
          </Button>
          <Button asChild>
            <Link href="/document-management/CentralDocumentIntegrationQueue">
              <Workflow className="mr-2 h-4 w-4" />
              Integration queue
            </Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
              <GitBranch className="h-5 w-5 text-primary" />
            </div>
            <CardTitle>Required Publish Package</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-2 sm:grid-cols-2">
              {requiredPackage.map((item) => (
                <div
                  key={item}
                  className="flex items-center gap-2 rounded-md border bg-background px-3 py-2 text-sm"
                >
                  <CheckCircle2 className="h-4 w-4 text-primary" />
                  {item}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
              <ShieldCheck className="h-5 w-5 text-primary" />
            </div>
            <CardTitle>Ownership</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            <Badge variant="secondary">DMS owns controls</Badge>
            <Badge variant="outline">Modules own records</Badge>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        {sourceContracts.map((contract) => (
          <Card
            key={contract.module}
            className="border-border bg-card text-card-foreground"
          >
            <CardHeader>
              <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <CardTitle className="text-base">{contract.module}</CardTitle>
                  <Badge variant="outline" className="mt-2 w-fit">
                    {contract.sourceLabel}
                  </Badge>
                </div>
                <Badge
                  variant={
                    contract.status === 'Reference pattern active'
                      ? 'default'
                      : 'outline'
                  }
                  className="w-fit"
                >
                  {contract.status}
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="rounded-md border bg-background p-3">
                <div className="flex items-center gap-2 text-sm font-medium">
                  <FileText className="h-4 w-4" />
                  Owner action
                </div>
                <p className="mt-2 text-sm">{contract.ownerAction}</p>
              </div>
              <div className="rounded-md border bg-background p-3">
                <div className="flex items-center gap-2 text-sm font-medium">
                  <Workflow className="h-4 w-4" />
                  Injection needed later
                </div>
                <p className="mt-2 text-sm">{contract.injectionNeeded}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
