'use client';

import { useParams } from 'next/navigation';
import { VendorInvoiceFormPage } from '../../create/page';

export default function EditVendorInvoicePage() {
    const params = useParams<{ id: string }>();
    return <VendorInvoiceFormPage editInvoiceId={params.id} />;
}
