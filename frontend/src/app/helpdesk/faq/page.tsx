'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { HelpCircle, Search } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ehcInternalTicketService, type EhcFaqItem } from '@/services/ehcInternalTicketService';

export default function HelpdeskFaqPage() {
  const [q, setQ] = useState('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'faq', 'categories'],
    queryFn: () => ehcInternalTicketService.faqListCategories(),
  });

  const { data: items, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'faq', 'search', q, categoryId],
    queryFn: () => ehcInternalTicketService.faqSearch(q.trim(), categoryId || null, 200),
  });

  const trackView = useMutation({
    mutationFn: async (id: string) => ehcInternalTicketService.faqTrackView(id),
  });

  const categoryLabelById = useMemo(() => new Map((categories ?? []).map((c) => [c.id, `${c.code} • ${c.name}`])), [categories]);

  const rendered = useMemo(() => (items ?? []) as EhcFaqItem[], [items]);
  const selected = useMemo(() => rendered.find((i) => i.id === selectedItemId) ?? null, [rendered, selectedItemId]);

  const openItem = (i: EhcFaqItem) => {
    setSelectedItemId(i.id);
    trackView.mutate(i.id);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <HelpCircle className="h-7 w-7" />
          FAQs
          </h1>
          <p className="text-slate-600 mt-1">Quick answers to common questions for faster ticket resolution.</p>
        </div>
      </div>

      <Card>
        <CardHeader className="p-4 pb-2">
          <CardTitle>Search</CardTitle>
          <CardDescription className="text-xs">Filter by category and keywords.</CardDescription>
        </CardHeader>
        <CardContent className="p-4 pt-0 grid grid-cols-1 md:grid-cols-12 gap-3 items-end">
          <div className="md:col-span-7 space-y-1">
            <Label className="text-xs text-slate-600">Keywords</Label>
            <div className="relative">
              <Search className="h-4 w-4 text-slate-400 absolute left-3 top-3" />
              <Input className="pl-9" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search question, answer, category..." />
            </div>
          </div>
          <div className="md:col-span-4 space-y-1">
            <Label className="text-xs text-slate-600">Category</Label>
            <Select value={categoryId || '__all'} onValueChange={(v) => setCategoryId(v === '__all' ? '' : v)}>
              <SelectTrigger>
                <SelectValue placeholder="All categories" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">All</SelectItem>
                {(categories ?? []).map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.code} • {c.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="md:col-span-1 flex gap-2">
            <Button variant="outline" onClick={() => refetch()} disabled={isLoading}>
              Refresh
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="p-4 pb-2">
          <CardTitle>Results</CardTitle>
          <CardDescription className="text-xs">{isLoading ? 'Loading…' : 'Click a question to open the answer.'}</CardDescription>
        </CardHeader>
        <CardContent className="p-4 pt-0">
          {error ? <div className="text-sm text-red-600">Failed to load FAQs.</div> : null}
          {rendered.length ? (
            <div className="divide-y rounded-md border bg-white">
              {rendered.map((i) => {
                const cat = i.categoryId ? categoryLabelById.get(i.categoryId) : null;
                return (
                  <button key={i.id} className="w-full text-left p-3 hover:bg-slate-50" onClick={() => openItem(i)} type="button">
                    <div className="flex items-start justify-between gap-4">
                      <div className="min-w-0">
                        <div className="font-medium text-slate-900">{i.question}</div>
                        <div className="text-xs text-slate-500 mt-0.5 flex flex-wrap items-center gap-2">
                          {cat ? <Badge variant="outline">{cat}</Badge> : null}
                          {i.isInternalOnly ? (
                            <Badge className="bg-slate-700 text-white hover:bg-slate-700/90">Internal</Badge>
                          ) : (
                            <Badge className="bg-blue-600 text-white hover:bg-blue-600/90">External</Badge>
                          )}
                        </div>
                      </div>
                      <div className="text-xs text-slate-500 whitespace-nowrap">Views: {i.viewCount ?? 0}</div>
                    </div>
                  </button>
                );
              })}
            </div>
          ) : (
            <div className="text-sm text-slate-500">No FAQs found.</div>
          )}
        </CardContent>
      </Card>

      <Dialog open={Boolean(selectedItemId)} onOpenChange={(v) => (v ? null : setSelectedItemId(null))}>
        <DialogContent className="sm:max-w-[900px] max-h-[85vh] overflow-auto">
          <DialogHeader>
            <DialogTitle>{selected?.question ?? 'FAQ'}</DialogTitle>
            <DialogDescription>
              {selected?.categoryId ? categoryLabelById.get(selected.categoryId) : ''}
            </DialogDescription>
          </DialogHeader>

          <div className="text-xs text-slate-500 mb-2">Views: {selected?.viewCount ?? 0}</div>
          <div className="prose prose-slate max-w-none whitespace-pre-wrap">{selected?.answer ?? ''}</div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
