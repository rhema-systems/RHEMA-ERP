'use client';

import { useEffect } from 'react';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';

/**
 * Prints the inspection's form. A Tailwind `print:hidden` on the page would hide the page and nothing
 * else — the body class + `.she-inspection-print-root` rules in globals.css are what lift the form
 * to the page origin and print a document rather than a screenshot of the app.
 */
export function InspectionPrintButton({ disabled }: { disabled?: boolean }) {
  const print = () => {
    const cleanup = () => {
      document.body.classList.remove('printing-she-inspection');
      window.removeEventListener('afterprint', cleanup);
    };
    document.body.classList.add('printing-she-inspection');
    window.addEventListener('afterprint', cleanup);
    window.print();
  };

  // Leaving the class behind would blank the next screen the user prints from.
  useEffect(() => () => document.body.classList.remove('printing-she-inspection'), []);

  return (
    <Button variant="outline" onClick={print} disabled={disabled}>
      <Printer className="mr-2 h-4 w-4" />
      Print form
    </Button>
  );
}
