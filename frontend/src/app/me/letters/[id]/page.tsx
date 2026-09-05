'use client';

import { useEffect } from 'react';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { myLettersService, LETTER_TYPE_LABEL } from '@/services/hr/my-letters.service';

/**
 * An issued letter, as it was frozen at issue (area 25 slice 12b).
 *
 * ⚠ This renders server-produced HTML. It is safe here for a specific reason worth stating:
 * the document is composed entirely by the backend from an HR-authored template plus tokens the
 * server supplies — no employee-supplied field is ever merged into it as markup. The one piece
 * of employee text that reaches a letter, `purpose`, goes through the same token merge as every
 * other value.
 *
 * Printing reuses the established body-class pattern (`printing-hr-letter` +
 * `.hr-letter-print-root`), the sibling of payroll's payslip family — but the letter is not
 * pinned to a fixed A4 box, because prose may legitimately run onto a second page and clipping
 * it would cut the signature off the bottom.
 */
export default function MyLetterPage() {
  const params = useParams();
  const id = String(params?.id ?? '');

  const { data: letter, isLoading, isError } = useQuery({
    queryKey: ['me', 'letters', id, 'document'],
    queryFn: () => myLettersService.getDocument(id),
    enabled: id !== '',
  });

  const print = () => {
    const done = () => {
      window.removeEventListener('afterprint', done);
      document.body.classList.remove('printing-hr-letter');
    };
    window.addEventListener('afterprint', done);
    document.body.classList.add('printing-hr-letter');
    window.print();
  };

  // Leaving the page mid-print must not strand the class on <body>.
  useEffect(() => () => document.body.classList.remove('printing-hr-letter'), []);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-96" />
      </div>
    );
  }

  if (isError || !letter) {
    return (
      <div className="space-y-6">
        <PageHeader title="Letter" backHref="/me/letters" />
        <p className="text-sm text-muted-foreground">
          This letter is not available. It may not have been issued yet — your letters page shows
          where the request has got to.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="hr-letter-no-print">
        <PageHeader
          title={LETTER_TYPE_LABEL[letter.letterType] ?? letter.letterTypeName}
          description={
            letter.letterNumber
              ? `Reference ${letter.letterNumber}${letter.issuedByName ? ` · issued by ${letter.issuedByName}` : ''}`
              : undefined
          }
          backHref="/me/letters"
          actions={
            <Button onClick={print}>
              <Printer className="mr-1 h-4 w-4" /> Print
            </Button>
          }
        />
      </div>

      <div className="hr-letter-print-root rounded-lg border bg-white p-2 shadow-sm dark:bg-white">
        {/* The frozen document. Rendered in a light surface regardless of theme: a letter is a
            piece of paper, and a dark-mode letterhead is not what gets printed or photographed. */}
        <div
          className="text-black [&_a]:text-black"
          dangerouslySetInnerHTML={{ __html: letter.html }}
        />
      </div>
    </div>
  );
}
