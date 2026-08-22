'use client';

import { useParams } from 'next/navigation';

import { SupplierDebitNoteForm } from '@/components/finance/ap/SupplierDebitNoteForm';

export default function EditSupplierDebitNotePage() {
  const { id } = useParams<{ id: string }>();
  return <SupplierDebitNoteForm noteId={id} />;
}
