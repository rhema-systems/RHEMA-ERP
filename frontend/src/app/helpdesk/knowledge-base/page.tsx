'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { BookOpen, Search } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ehcInternalTicketService, type EhcKbArticleDetail, type EhcKbArticleListItem } from '@/services/ehcInternalTicketService';

export default function HelpdeskKnowledgeBasePage() {
  const [q, setQ] = useState('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [selectedArticleId, setSelectedArticleId] = useState<string | null>(null);

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'kb', 'categories'],
    queryFn: () => ehcInternalTicketService.kbListCategories(),
  });

  const { data: results, isLoading, error, refetch } = useQuery({
    queryKey: ['ehc', 'kb', 'search', q, categoryId],
    queryFn: () => ehcInternalTicketService.kbSearchArticles(q.trim(), categoryId || null, 30),
  });

  const { data: article } = useQuery({
    queryKey: ['ehc', 'kb', 'article', selectedArticleId],
    queryFn: () => (selectedArticleId ? ehcInternalTicketService.kbGetArticle(selectedArticleId) : Promise.resolve(null)),
    enabled: Boolean(selectedArticleId),
  });

  const trackView = useMutation({
    mutationFn: async (id: string) => ehcInternalTicketService.kbTrackView(id),
  });

  const openArticle = async (row: EhcKbArticleListItem) => {
    setSelectedArticleId(row.id);
    trackView.mutate(row.id);
  };

  const body = useMemo(() => {
    const a = (article ?? null) as EhcKbArticleDetail | null;
    if (!a) return '';
    return a.body || '';
  }, [article]);

  return (
    <div className="max-w-6xl mx-auto space-y-4">
      <div>
        <h1 className="text-3xl font-bold flex items-center gap-2">
          <BookOpen className="h-7 w-7" />
          Knowledge Base
        </h1>
        <p className="text-slate-600 mt-1">Internal support articles to help agents resolve tickets faster.</p>
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
              <Input className="pl-9" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search title, summary, tags, code..." />
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
          <CardDescription className="text-xs">{isLoading ? 'Loading…' : 'Click an article to open.'}</CardDescription>
        </CardHeader>
        <CardContent className="p-4 pt-0">
          {error ? <div className="text-sm text-red-600">Failed to load articles.</div> : null}
          {(results ?? []).length ? (
            <div className="divide-y rounded-md border bg-white">
              {(results ?? []).map((r) => (
                <button
                  key={r.id}
                  className="w-full text-left p-3 hover:bg-slate-50"
                  onClick={() => openArticle(r)}
                  type="button"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div className="min-w-0">
                      <div className="font-medium text-slate-900 truncate">{r.title}</div>
                      <div className="text-xs text-slate-500 mt-0.5">
                        {r.code}
                        {r.categoryName ? ` • ${r.categoryName}` : ''}
                        {r.tagsCsv ? ` • ${r.tagsCsv}` : ''}
                      </div>
                      {r.summary ? <div className="text-sm text-slate-700 mt-1 line-clamp-2">{r.summary}</div> : null}
                    </div>
                    <div className="text-xs text-slate-500 whitespace-nowrap">Views: {r.viewCount}</div>
                  </div>
                </button>
              ))}
            </div>
          ) : (
            <div className="text-sm text-slate-500">No articles found.</div>
          )}
        </CardContent>
      </Card>

      <Dialog open={Boolean(selectedArticleId)} onOpenChange={(v) => (v ? null : setSelectedArticleId(null))}>
        <DialogContent className="sm:max-w-[900px] max-h-[85vh] overflow-auto">
          <DialogHeader>
            <DialogTitle>{(article as any)?.title ?? 'Article'}</DialogTitle>
            <DialogDescription>
              {(article as any)?.code ? `${(article as any).code}${(article as any)?.categoryName ? ` • ${(article as any).categoryName}` : ''}` : ''}
            </DialogDescription>
          </DialogHeader>

          <div className="prose prose-slate max-w-none whitespace-pre-wrap">{body}</div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

