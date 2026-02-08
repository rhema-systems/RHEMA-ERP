'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { rfqService, type RfqDto } from '@/services/rfqService';
import { toast } from 'sonner';
import { Loader2, FileText } from 'lucide-react';

export default function SupplierRfqsPage() {
  const [loading, setLoading] = useState(true);
  const [rfqs, setRfqs] = useState<RfqDto[]>([]);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const list = await rfqService.getMyRfqs();
        setRfqs(list || []);
      } catch (e: any) {
        console.error(e);
        toast.error(e.message || 'Failed to load RFQs');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">My RFQs</h1>
        <p className="text-sm text-muted-foreground">RFQs you have been invited to quote on.</p>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Invitations</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rfqs.length === 0 ? (
            <div className="py-10 text-center text-sm text-muted-foreground">No RFQs found.</div>
          ) : (
            <div className="divide-y">
              {rfqs.map((r) => (
                <div key={r.id} className="flex items-center justify-between gap-4 py-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <FileText className="h-4 w-4 text-muted-foreground" />
                      <div className="font-medium truncate">{r.rfqNumber}</div>
                      <Badge variant="outline">{r.status}</Badge>
                    </div>
                    <div className="text-sm text-muted-foreground truncate">{r.title}</div>
                  </div>
                  <Button asChild size="sm">
                    <Link href={`/external-portal/rfqs/${r.id}`}>Open</Link>
                  </Button>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

