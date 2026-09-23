'use client';

import React from 'react';
import { ExternalLink, FileText, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { estateLandManagementService } from '@/services/estate-land-management.service';
import { procedureCaseService, type LegalPropertyCaseContext } from '@/services/procedure-case.service';

interface Props {
  legalCaseId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function showDate(value?: string | null) {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function Detail({ label, value }: { label: string; value?: React.ReactNode }) {
  if (value == null || value === '') return null;
  return <div className="min-w-0"><div className="text-xs text-muted-foreground">{label}</div><div className="break-words text-sm">{value}</div></div>;
}

export function LegalPropertyCaseContextDialog({ legalCaseId, open, onOpenChange }: Props) {
  const [context, setContext] = React.useState<LegalPropertyCaseContext | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!open) return;
    let active = true;
    setLoading(true);
    setError(null);
    void procedureCaseService.getLegalPropertyContext(legalCaseId)
      .then((data) => { if (active) setContext(data); })
      .catch((caught) => { if (active) setError(caught instanceof Error ? caught.message : 'Unable to load case details.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [legalCaseId, open]);

  const viewFile = async (download: () => Promise<Blob>) => {
    try {
      const blob = await download();
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (caught) {
      toast.error(caught instanceof Error ? caught.message : 'Unable to open document.');
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="flex max-h-[90vh] max-w-5xl flex-col overflow-hidden">
        <DialogHeader><DialogTitle>Property case details</DialogTitle></DialogHeader>
        {loading ? <div className="flex items-center gap-2 py-12 text-sm"><Loader2 className="h-4 w-4 animate-spin" /> Loading case details</div> : null}
        {error ? <div className="py-8 text-sm text-destructive">{error}</div> : null}
        {!loading && !error && context ? (
          <Tabs defaultValue="overview" className="flex min-h-0 flex-col">
            <TabsList className="w-fit">
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="documents">Documents</TabsTrigger>
              <TabsTrigger value="history">History</TabsTrigger>
            </TabsList>
            <div className="min-h-0 overflow-y-auto pr-2">
              <TabsContent value="overview" className="space-y-5">
                <section className="space-y-2 border-b pb-4">
                  <h3 className="text-sm font-semibold">Estate case</h3>
                  <div className="grid gap-3 sm:grid-cols-3">
                    <Detail label="Reference" value={context.sourceCase.referenceNumber} />
                    <Detail label="Title" value={context.sourceCase.title} />
                    <Detail label="Status" value={context.sourceCase.status} />
                    <Detail label="Current stage" value={context.sourceCase.currentStageName} />
                    <Detail label="Customer" value={context.sourceCase.applicantName} />
                    <Detail label="Description" value={context.sourceCase.description} />
                  </div>
                </section>
                {context.asset ? <section className="space-y-2 border-b pb-4">
                  <h3 className="text-sm font-semibold">Parcel or property</h3>
                  <div className="grid gap-3 sm:grid-cols-3">
                    <Detail label="Asset code" value={context.asset.assetCode} />
                    <Detail label="Name" value={context.asset.name} />
                    <Detail label="Status" value={String(context.asset.status)} />
                    <Detail label="Location" value={context.asset.location} />
                    <Detail label="Region / district / town" value={[context.asset.region, context.asset.district, context.asset.town].filter(Boolean).join(', ')} />
                    <Detail label="Area" value={context.asset.areaValue != null ? `${context.asset.areaValue} ${context.asset.areaUnit || ''}` : null} />
                    <Detail label="Survey plan" value={context.asset.surveyPlanNumber} />
                    <Detail label="Property file" value={context.asset.propertyFileReference} />
                    <Detail label="Land bank value" value={context.asset.valuationAmount != null ? `${context.asset.currency} ${context.asset.valuationAmount.toLocaleString()}` : null} />
                  </div>
                </section> : null}
                {context.inquiry.ticket ? <section className="space-y-2 border-b pb-4">
                  <h3 className="text-sm font-semibold">Customer inquiry</h3>
                  <div className="grid gap-3 sm:grid-cols-3">
                    <Detail label="Ticket" value={context.inquiry.ticket.ticketNumber} />
                    <Detail label="Subject" value={context.inquiry.ticket.subject} />
                    <Detail label="Status" value={String(context.inquiry.ticket.status)} />
                    <Detail label="Received" value={showDate(context.inquiry.ticket.createdAt)} />
                    <Detail label="Inquiry" value={context.inquiry.ticket.description} />
                    <Detail label="Resolution" value={context.inquiry.ticket.resolutionSummary} />
                  </div>
                </section> : null}
                {context.opportunity ? <section className="space-y-2 border-b pb-4">
                  <h3 className="text-sm font-semibold">Sales opportunity</h3>
                  <div className="grid gap-3 sm:grid-cols-3">
                    <Detail label="Opportunity" value={context.opportunity.name} />
                    <Detail label="Stage" value={context.opportunity.stage} />
                    <Detail label="Amount" value={`${context.opportunity.currency} ${context.opportunity.amount.toLocaleString()}`} />
                    <Detail label="Closed" value={showDate(context.opportunity.actualCloseDate)} />
                    <Detail label="Description" value={context.opportunity.description} />
                    <Detail label="Notes" value={context.opportunity.notes} />
                  </div>
                </section> : null}
                <section className="space-y-2">
                  <h3 className="text-sm font-semibold">Transaction details</h3>
                  <div className="grid gap-3 sm:grid-cols-3">
                    {context.sourceCase.fields.map((field, index) => <Detail key={`${field.label}-${index}`} label={field.label} value={field.value} />)}
                  </div>
                </section>
              </TabsContent>
              <TabsContent value="documents" className="space-y-5">
                <section className="space-y-2">
                  <h3 className="text-sm font-semibold">Estate case documents</h3>
                  {context.sourceCase.documents.length === 0 ? <p className="text-sm text-muted-foreground">No documents attached.</p> : null}
                  {context.sourceCase.documents.map((document) => <div key={document.id} className="flex items-center justify-between gap-3 border-b py-2 text-sm">
                    <div className="min-w-0"><div className="font-medium">{document.name}</div><div className="truncate text-xs text-muted-foreground">{document.fileName}</div></div>
                    <Button size="sm" variant="outline" onClick={() => document.dmsUrl
                      ? window.open(document.dmsUrl, '_blank', 'noopener,noreferrer')
                      : void viewFile(() => procedureCaseService.downloadLegalSourceDocument(legalCaseId, document.id))}>
                      <ExternalLink className="mr-2 h-4 w-4" /> View
                    </Button>
                  </div>)}
                </section>
                <section className="space-y-2">
                  <h3 className="text-sm font-semibold">Parcel and property documents</h3>
                  {context.assetDocuments.length === 0 ? <p className="text-sm text-muted-foreground">No documents attached.</p> : null}
                  {context.assetDocuments.map((document) => <div key={document.id} className="flex items-center justify-between gap-3 border-b py-2 text-sm">
                    <div className="min-w-0"><div className="font-medium">{document.documentName || document.documentType}</div><div className="truncate text-xs text-muted-foreground">{document.fileName}{document.centralDocumentReference ? ` · ${document.centralDocumentReference}` : ''}</div></div>
                    {context.asset ? <Button size="sm" variant="outline" onClick={() => document.centralDocumentRecordId
                      ? window.open(`/document-management/records/${document.centralDocumentRecordId}`, '_blank', 'noopener,noreferrer')
                      : void viewFile(() => estateLandManagementService.downloadDocument(context.asset?.id ?? '', document.id))}>
                      <ExternalLink className="mr-2 h-4 w-4" /> View
                    </Button> : null}
                  </div>)}
                </section>
                <section className="space-y-2">
                  <h3 className="text-sm font-semibold">Inquiry attachments</h3>
                  {context.inquiry.ticketAttachments.length === 0 ? <p className="text-sm text-muted-foreground">No attachments.</p> : null}
                  {context.inquiry.ticketAttachments.map((attachment) => <div key={attachment.id} className="flex items-center justify-between gap-3 border-b py-2 text-sm">
                    <div className="flex min-w-0 items-center gap-2"><FileText className="h-4 w-4 shrink-0" /><span className="truncate">{attachment.fileName}</span></div>
                    <Button size="sm" variant="outline" onClick={() => void viewFile(() => procedureCaseService.downloadLegalInquiryAttachment(legalCaseId, attachment.id))}>
                      <ExternalLink className="mr-2 h-4 w-4" /> View
                    </Button>
                  </div>)}
                </section>
              </TabsContent>
              <TabsContent value="history" className="space-y-5">
                <section className="space-y-2"><h3 className="text-sm font-semibold">Estate case activity</h3>
                  {context.sourceCase.activities.map((activity, index) => <div key={index} className="border-b py-2 text-sm"><div className="font-medium">{activity.action} · {showDate(activity.performedAt)}</div><div className="text-muted-foreground">{activity.stageName} {activity.details}</div></div>)}
                </section>
                <section className="space-y-2"><h3 className="text-sm font-semibold">Sales activity</h3>
                  {context.salesActivities.map((activity) => <div key={activity.id} className="border-b py-2 text-sm"><div className="font-medium">{activity.subject} · {showDate(activity.activityDate)}</div><div className="text-muted-foreground">{activity.activityType} · {activity.activityStatus} · {activity.description || activity.notes}</div></div>)}
                </section>
                <section className="space-y-2"><h3 className="text-sm font-semibold">Inquiry messages and status</h3>
                  {context.inquiry.messages.map((message) => <div key={message.id} className="border-b py-2 text-sm"><div className="text-xs text-muted-foreground">{showDate(message.createdAt)}{message.isInternal ? ' · Internal' : ''}</div><div>{message.body}</div></div>)}
                  {context.inquiry.ticketHistory.map((event, index) => <div key={index} className="border-b py-2 text-sm"><div className="font-medium">{String(event.toStatus)} · {showDate(event.createdAt)}</div><div className="text-muted-foreground">{event.notes}</div></div>)}
                </section>
              </TabsContent>
            </div>
          </Tabs>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
