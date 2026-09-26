'use client';

import { useEffect, useState } from 'react';
import { businessPartnerService } from '@/services/businessPartnerService';

export function SupplierPortalAccount() {
  const [account, setAccount] = useState<{ partnerCode: string; partnerName: string } | null>(null);
  const [unavailable, setUnavailable] = useState(false);
  useEffect(() => {
    let active = true;
    businessPartnerService.getMyAccount().then(value => { if (active) setAccount(value); })
      .catch(() => { if (active) setUnavailable(true); });
    return () => { active = false; };
  }, []);
  if (unavailable) return <p role="status" className="text-sm">Your business partner account details could not be loaded.</p>;
  if (!account) return null;
  return <div className="rounded-lg border bg-card p-4">
    <p className="text-sm text-muted-foreground">Business Partner Account Number</p>
    <p className="font-semibold">{account.partnerCode}</p>
    <p className="text-sm">{account.partnerName}</p>
  </div>;
}
