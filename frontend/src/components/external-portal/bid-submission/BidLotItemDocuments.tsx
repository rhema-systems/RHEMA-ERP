'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { downloadBidDocument, type TenderBidDocumentDto, type CreateTenderBidDto } from '@/services/tenderBidService';
import type { TenderDetailDto } from '@/services/tenderService';
import { bidDocumentAccept, validateBidDocumentFile } from './bid-document-files';

interface Props {
  tender?: TenderDetailDto | null;
  bidData: CreateTenderBidDto;
  bidId?: string;
  documents: TenderBidDocumentDto[];
  onUpload?: (file: File, documentType: string, tenderItemId?: string) => Promise<void>;
  onDelete?: (documentId: string) => void;
}

export function BidLotItemDocuments({ tender, bidData, bidId, documents, onUpload, onDelete }: Props) {
  const [selected, setSelected] = useState<Record<string, File>>({});
  const [uploading, setUploading] = useState<string | null>(null);
  const selectedLots = new Set(bidData.selectedLotIds ?? []);
  const lots = (tender?.lots ?? []).filter(lot => selectedLots.has(lot.id));
  if (!lots.length) return null;

  const download = async (document: TenderBidDocumentDto) => {
    if (!bidId) return;
    try {
      await downloadBidDocument(bidId, document.id, document.documentName);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not download the item document');
    }
  };

  const upload = async (itemId: string) => {
    const file = selected[itemId];
    if (!file || !onUpload || !bidId || uploading) return;
    setUploading(itemId);
    try {
      await onUpload(file, 'TechnicalItemSupportingDocument', itemId);
      setSelected(previous => {
        const next = { ...previous };
        delete next[itemId];
        return next;
      });
      toast.success('Item document uploaded');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Could not upload the item document');
    } finally {
      setUploading(null);
    }
  };

  return <Card>
    <CardHeader>
      <CardTitle>Lot item supporting documents</CardTitle>
      <CardDescription>Attach specifications, certificates or product images to each item in your saved bid draft.</CardDescription>
    </CardHeader>
    <CardContent className="space-y-6">
      {lots.map(lot => <section key={lot.id} className="space-y-3" aria-label={`Lot ${lot.lotNumber}`}>
        <h3 className="font-semibold">Lot {lot.lotNumber}: {lot.title}</h3>
        {(lot.items ?? []).map(item => {
          const files = documents.filter(document => document.tenderItemId === item.id);
          return <div key={item.id} className="rounded-md border p-4 space-y-3">
            <h4 className="font-medium">{item.description}</h4>
            {files.map(document => <div key={document.id} className="flex flex-wrap items-center gap-3 text-sm">
              <span className="flex-1">{document.documentName}</span>
              <Button type="button" size="sm" variant="outline" disabled={!bidId}
                onClick={() => download(document)}>Download</Button>
              {onDelete && <Button type="button" size="sm" variant="outline"
                onClick={() => onDelete(document.id)}>Remove</Button>}
            </div>)}
            <div className="flex flex-wrap gap-3 items-center">
              <Input type="file" accept={bidDocumentAccept} className="max-w-sm"
                aria-label={`Supporting file for ${item.description}`} disabled={!bidId || !onUpload || uploading !== null}
                onChange={event => {
                  const file = event.target.files?.[0];
                  event.target.value = '';
                  if (!file) return;
                  const error = validateBidDocumentFile(file);
                  if (error) { toast.error(error); return; }
                  setSelected(previous => ({ ...previous, [item.id]: file }));
                }} />
              <Button type="button" size="sm" disabled={!bidId || !onUpload || !selected[item.id] || uploading !== null}
                aria-label={`Upload file for ${item.description}`} onClick={() => upload(item.id)}>
                {uploading === item.id ? 'Uploading…' : 'Upload'}
              </Button>
              {selected[item.id] && <span className="text-sm text-muted-foreground">{selected[item.id].name}</span>}
            </div>
          </div>;
        })}
      </section>)}
    </CardContent>
  </Card>;
}
