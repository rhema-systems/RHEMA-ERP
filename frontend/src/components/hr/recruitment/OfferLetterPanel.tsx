'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Download, FileText, Loader2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useToast } from '@/hooks/use-toast';
import { jobOfferService } from '@/services/hr/offers.service';
import type { JobOffer } from '@/types/hr/offers';

/**
 * The offer letter — a rendered preview, and the two stored files.
 *
 * ⚠ **Preview and download are different resources**, and this used to be a single dead route: the
 * preview is rendered on demand from the HR-editable "OfferLetter" template plus the offer's terms
 * (nothing is stored by viewing it); the download streams whichever file was actually issued or
 * countersigned. Do not collapse them back into one control.
 *
 * There is no reliable "a letter exists" flag on the offer — the legacy path field only ever
 * populates for records predating the upload gate. A missing file therefore surfaces as a 404 on
 * download, handled here with a plain toast rather than a permanent disabled state.
 */
export function OfferLetterPanel({ offer, canManage }: { offer: JobOffer; canManage: boolean }) {
  const { toast } = useToast();
  const [previewOpen, setPreviewOpen] = useState(false);
  const issuedInputRef = useRef<HTMLInputElement>(null);
  const signedInputRef = useRef<HTMLInputElement>(null);

  const preview = useQuery({
    queryKey: ['hr', 'offer-letter-preview', offer.id],
    queryFn: () => jobOfferService.getLetterPreview(offer.id),
    enabled: previewOpen,
  });

  const uploadIssued = useMutation({
    mutationFn: (file: File) => jobOfferService.uploadLetter(offer.id, file),
    onSuccess: () => {
      toast({ title: 'Letter uploaded' });
      if (issuedInputRef.current) issuedInputRef.current.value = '';
    },
    onError: (e: any) =>
      toast({ title: 'Upload refused', description: e?.message, variant: 'destructive' }),
  });

  const uploadSigned = useMutation({
    mutationFn: (file: File) => jobOfferService.uploadSignedLetter(offer.id, file),
    onSuccess: () => {
      toast({ title: 'Countersigned letter uploaded' });
      if (signedInputRef.current) signedInputRef.current.value = '';
    },
    onError: (e: any) =>
      toast({ title: 'Upload refused', description: e?.message, variant: 'destructive' }),
  });

  const downloadIssued = async () => {
    try {
      await jobOfferService.downloadLetter(offer.id, offer.offerNumber);
    } catch (e: any) {
      if (e?.status === 404) {
        toast({ title: 'No letter has been uploaded yet' });
      } else {
        toast({ title: 'Download failed', description: e?.message, variant: 'destructive' });
      }
    }
  };

  const downloadSigned = async () => {
    try {
      await jobOfferService.downloadSignedLetter(offer.id, offer.offerNumber);
    } catch (e: any) {
      if (e?.status === 404) {
        toast({ title: 'No countersigned letter has been uploaded yet' });
      } else {
        toast({ title: 'Download failed', description: e?.message, variant: 'destructive' });
      }
    }
  };

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Offer letter</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <Button variant="outline" onClick={() => setPreviewOpen(true)}>
          <FileText className="mr-2 h-4 w-4" />
          Preview
        </Button>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1.5 rounded-md border p-3">
            <p className="text-sm font-medium">Issued letter</p>
            <div className="flex flex-wrap gap-2">
              <Button size="sm" variant="ghost" onClick={downloadIssued}>
                <Download className="mr-2 h-4 w-4" /> Download
              </Button>
              {canManage && (
                <>
                  <input
                    ref={issuedInputRef}
                    type="file"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) uploadIssued.mutate(file);
                    }}
                  />
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => issuedInputRef.current?.click()}
                    disabled={uploadIssued.isPending}
                  >
                    {uploadIssued.isPending ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Upload className="mr-2 h-4 w-4" />
                    )}
                    Upload
                  </Button>
                </>
              )}
            </div>
          </div>

          <div className="space-y-1.5 rounded-md border p-3">
            <p className="text-sm font-medium">Countersigned letter</p>
            <div className="flex flex-wrap gap-2">
              <Button size="sm" variant="ghost" onClick={downloadSigned}>
                <Download className="mr-2 h-4 w-4" /> Download
              </Button>
              {canManage && (
                <>
                  <input
                    ref={signedInputRef}
                    type="file"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) uploadSigned.mutate(file);
                    }}
                  />
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => signedInputRef.current?.click()}
                    disabled={uploadSigned.isPending}
                  >
                    {uploadSigned.isPending ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Upload className="mr-2 h-4 w-4" />
                    )}
                    Upload
                  </Button>
                </>
              )}
            </div>
            <p className="text-xs text-muted-foreground">The signed copy the candidate returned.</p>
          </div>
        </div>
      </CardContent>

      <Dialog open={previewOpen} onOpenChange={setPreviewOpen}>
        <DialogContent className="max-h-[85vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{preview.data?.subject ?? 'Offer letter preview'}</DialogTitle>
          </DialogHeader>
          {preview.isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : preview.isError ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              The letter could not be rendered.
            </p>
          ) : (
            <div
              className="prose prose-sm max-w-none rounded-md border bg-card p-6"
              // The template is HR-authored and the terms are the offer's own — same trust boundary
              // as the rest of this HR-only screen.
              dangerouslySetInnerHTML={{ __html: preview.data?.htmlBody ?? '' }}
            />
          )}
        </DialogContent>
      </Dialog>
    </Card>
  );
}
