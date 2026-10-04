'use client';

import { useParams } from 'next/navigation';
import { InvoiceFormPage } from '../../new/page';

export default function EditCustomerInvoicePage() {
    const params = useParams<{ id: string }>();
    return <InvoiceFormPage editInvoiceId={params.id} />;
}
