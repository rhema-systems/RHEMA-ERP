'use client';

import React from 'react';
import { useParams } from 'next/navigation';
import { VendorInvoiceFormPage } from '@/components/finance/ap/VendorInvoiceFormPage';

export default function EditVendorInvoicePage() {
    const params = useParams<{ id: string }>();
    return <VendorInvoiceFormPage editInvoiceId={params.id} />;
}
