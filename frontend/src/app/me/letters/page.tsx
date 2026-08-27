'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, Loader2, Mail, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import {
  myLettersService,
  LETTER_TYPES,
  LETTER_TYPE_LABEL,
  type HrLetterRequest,
  type HrLetterRequestStatus,
  type HrLetterType,
} from '@/services/hr/my-letters.service';
import { cn } from '@/lib/utils';

/**
 * My Letters (area 25 slice 12b) — ask HR for a letter, and collect it when it is issued.
 *
 * HR fulfils a request one of two ways and the row branches on `fulfilment`: a GENERATED letter
 * opens as a document to read and print; an UPLOADED signed scan downloads as a file. Offering
 * both buttons regardless would give the employee one that answers 404.
 */

const fmtDateTime = (v?: string | null) =>
  v ? new Date(v).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';

const STATUS_STYLE: Record<HrLetterRequestStatus, string> = {
  Pending: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
  Issued: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  Rejected: 'bg-red-100 text-red-900 dark:bg-red-950/50 dark:text-red-200',
  Cancelled: 'bg-muted text-muted-foreground',
};

export default function MyLettersPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [open, setOpen] = useState(false);
  const [letterType, setLetterType] = useState<HrLetterType | ''>('');
  const [purpose, setPurpose] = useState('');
  const [addressedTo, setAddressedTo] = useState('');

  const { data: letters = [], isLoading, isError } = useQuery({
    queryKey: ['me', 'letters'],
    queryFn: () => myLettersService.getMine(),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['me', 'letters'] });

  const create = useMutation({
    mutationFn: ({ type }: { type: HrLetterType }) =>
      myLettersService.create({
        letterType: type,
        purpose,
        addressedTo: addressedTo.trim() === '' ? null : addressedTo.trim(),
      }),
    onSuccess: (created) => {
      refresh();
      setOpen(false);
      setLetterType('');
      setPurpose('');
      setAddressedTo('');
      toast({
        title: `Request ${created.requestNumber} sent to HR`,
        description: 'You will be able to collect the letter here once it is issued.',
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Request not sent',
        description: e?.message || 'The request could not be filed.',
        variant: 'destructive',
      }),
  });

  const cancel = useMutation({
    mutationFn: (id: string) => myLettersService.cancel(id),
    onSuccess: () => {
      refresh();
      toast({ title: 'Withdrawn', description: 'The request is no longer with HR.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Not withdrawn',
        description: e?.message || 'The request could not be withdrawn.',
        variant: 'destructive',
      }),
  });

  const downloadFile = async (letter: HrLetterRequest) => {
    try {
      await hrDocumentService.download(
        myLettersService.fileEndpoint(letter.id),
        letter.fileName ?? `${letter.letterNumber ?? letter.requestNumber}.pdf`,
      );
    } catch {
      toast({
        title: 'Could not download the letter',
        description: 'Please try again, or ask HR to re-send it.',
        variant: 'destructive',
      });
    }
  };

  const selected = LETTER_TYPES.find((t) => t.value === letterType);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Letters"
        description="Ask HR for a letter about your employment, and collect it here when it is ready."
        backHref="/me"
        actions={
          <Button onClick={() => setOpen(true)}>
            <Plus className="mr-1 h-4 w-4" /> Request a letter
          </Button>
        }
      />

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Your letters could not be loaded right now. Try again in a moment.
        </p>
      ) : letters.length === 0 ? (
        <EmptyState
          icon={Mail}
          title="No letters yet"
          description="Banks, landlords and embassies often ask for a letter confirming your employment. Request one here and HR will prepare it."
          action={<Button onClick={() => setOpen(true)}>Request a letter</Button>}
        />
      ) : (
        <div className="space-y-3">
          {letters.map((l) => (
            <Card key={l.id}>
              <CardContent className="space-y-3 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{LETTER_TYPE_LABEL[l.letterType] ?? l.letterTypeName}</span>
                  <Badge className={cn('border-0', STATUS_STYLE[l.status])}>{l.statusName}</Badge>
                  {l.letterNumber && (
                    <Badge variant="outline" className="font-mono">{l.letterNumber}</Badge>
                  )}
                  <span className="ml-auto font-mono text-xs text-muted-foreground">
                    {l.requestNumber}
                  </span>
                </div>

                <div className="text-sm text-muted-foreground">
                  For {l.purpose}
                  {l.addressedTo ? ` · addressed to ${l.addressedTo}` : ''}
                </div>

                {l.decisionComments && (
                  <div className="rounded-md border-l-2 border-primary/50 bg-muted/30 px-3 py-2 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">
                      HR{l.issuedByName ? ` · ${l.issuedByName}` : ''}
                    </div>
                    <p className="mt-0.5">{l.decisionComments}</p>
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2">
                  {/* Exactly one collection route answers — see the file's note. */}
                  {l.status === 'Issued' && l.fulfilment === 'Generated' && (
                    <Button size="sm" asChild>
                      <Link href={`/me/letters/${l.id}`}>
                        <FileText className="mr-1 h-3.5 w-3.5" /> Open letter
                      </Link>
                    </Button>
                  )}
                  {l.status === 'Issued' && l.fulfilment === 'Uploaded' && (
                    <Button size="sm" onClick={() => downloadFile(l)}>
                      <Download className="mr-1 h-3.5 w-3.5" />
                      {l.fileName ?? 'Download letter'}
                    </Button>
                  )}
                  {l.status === 'Pending' && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => cancel.mutate(l.id)}
                      disabled={cancel.isPending}
                    >
                      {cancel.isPending && <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />}
                      Withdraw
                    </Button>
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    asked {fmtDateTime(l.requestedAt)}
                    {l.issuedAt ? ` · issued ${fmtDateTime(l.issuedAt)}` : ''}
                  </span>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* ── Request dialog ────────────────────────────────────────────── */}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request a letter</DialogTitle>
            <DialogDescription>
              HR prepares these. Tell them which letter you need and what it is for — that is what
              decides the wording.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="letterType">Letter</Label>
              <Select value={letterType} onValueChange={(v) => setLetterType(v as HrLetterType)}>
                <SelectTrigger id="letterType">
                  <SelectValue placeholder="Choose a letter" />
                </SelectTrigger>
                <SelectContent>
                  {LETTER_TYPES.map((t) => (
                    <SelectItem key={t.value} value={t.value}>
                      {t.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {selected && <p className="text-xs text-muted-foreground">{selected.hint}</p>}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="purpose">What do you need it for?</Label>
              <Textarea
                id="purpose"
                rows={2}
                value={purpose}
                onChange={(e) => setPurpose(e.target.value)}
                placeholder="e.g. a mortgage application with Ghana Commercial Bank"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="addressedTo">
                Addressed to <span className="font-normal text-muted-foreground">(optional)</span>
              </Label>
              <Input
                id="addressedTo"
                value={addressedTo}
                onChange={(e) => setAddressedTo(e.target.value)}
                placeholder="Leave blank for “To whom it may concern”"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => letterType && create.mutate({ type: letterType })}
              disabled={create.isPending || letterType === '' || purpose.trim() === ''}
            >
              {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Send to HR
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
