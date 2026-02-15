'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { rfqService, type RfqDto } from '@/services/rfqService';
import { toast } from 'sonner';
import { Loader2, Search, FileText } from 'lucide-react';

export default function RfqsPage() {
  const [loading, setLoading] = useState(true);
  const [rfqs, setRfqs] = useState<RfqDto[]>([]);
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search.trim()) return rfqs;
    const s = search.toLowerCase();
    return rfqs.filter((r) => r.rfqNumber.toLowerCase().includes(s) || r.title.toLowerCase().includes(s));
  }, [rfqs, search]);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const result = await rfqService.getRfqs({ page: 1, pageSize: 200 });
        setRfqs(result.items || []);
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
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">RFQs</h1>
          <p className="text-sm text-muted-foreground">Private requests for quotation sent to selected suppliers.</p>
        </div>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">All RFQs</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-2">
            <Search className="h-4 w-4 text-muted-foreground" />
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search RFQ number or title..." />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : filtered.length === 0 ? (
            <div className="py-10 text-center text-sm text-muted-foreground">No RFQs found.</div>
          ) : (
            <div className="divide-y">
              {filtered.map((r) => (
                <div key={r.id} className="flex items-center justify-between gap-4 py-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <FileText className="h-4 w-4 text-muted-foreground" />
                      <div className="font-medium truncate">{r.rfqNumber}</div>
                      <Badge variant="outline">{r.status}</Badge>
                      <Badge variant="secondary">
                        Suppliers: {r.supplierCount} · Quotes: {r.quoteCount}
                      </Badge>
                    </div>
                    <div className="text-sm text-muted-foreground truncate">{r.title}</div>
                  </div>

                  <div className="flex items-center gap-2">
                    <Button asChild variant="outline" size="sm">
                      <Link href={`/procurement/rfqs/${r.id}/edit`}>Open</Link>
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

