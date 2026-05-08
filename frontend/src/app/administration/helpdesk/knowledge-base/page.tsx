'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Plus, Trash2, Pencil } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  ehcAdminService,
  type EhcKnowledgeBaseCategoryAdmin,
  type EhcKnowledgeBaseArticleAdmin,
  type CreateEhcKnowledgeBaseArticleAdmin,
  type CreateEhcKnowledgeBaseCategoryAdmin,
} from '@/services/ehcAdminService';

type Mode = 'categories' | 'articles';
type TableCellProps<T> = {
  row: {
    original: T;
  };
};

export default function AdminHelpdeskKnowledgeBasePage() {
  const qc = useQueryClient();
  const { toast } = useToast();
  const [mode, setMode] = useState<Mode>('articles');

  const { data: categories = [], isLoading: catsLoading } = useQuery({
    queryKey: ['ehc', 'admin', 'kb', 'categories'],
    queryFn: () => ehcAdminService.listKbCategories(),
  });

  const { data: articles = [], isLoading: artLoading } = useQuery({
    queryKey: ['ehc', 'admin', 'kb', 'articles'],
    queryFn: () => ehcAdminService.listKbArticles(),
  });

  const categoryNameById = useMemo(() => new Map(categories.map((c) => [c.id, c.name])), [categories]);

  // Category dialog
  const [categoryOpen, setCategoryOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<EhcKnowledgeBaseCategoryAdmin | null>(null);
  const [categoryForm, setCategoryForm] = useState<CreateEhcKnowledgeBaseCategoryAdmin>({
    code: '',
    name: '',
    description: '',
    isActive: true,
  });

  useEffect(() => {
    if (!categoryOpen) {
      setEditingCategory(null);
      setCategoryForm({ code: '', name: '', description: '', isActive: true });
    }
  }, [categoryOpen]);

  const saveCategory = useMutation({
    mutationFn: async () => {
      const payload = {
        code: categoryForm.code,
        name: categoryForm.name,
        description: categoryForm.description || null,
        isActive: categoryForm.isActive,
      };
      if (editingCategory) await ehcAdminService.updateKbCategory(editingCategory.id, payload);
      else await ehcAdminService.createKbCategory(payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'kb', 'categories'] });
      toast({ title: 'Saved', description: 'Category saved.', variant: 'success' });
      setCategoryOpen(false);
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  const deleteCategory = useMutation({
    mutationFn: async (id: string) => ehcAdminService.deleteKbCategory(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'kb', 'categories'] });
      toast({ title: 'Deleted', description: 'Category deleted.', variant: 'success' });
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  // Article dialog
  const [articleOpen, setArticleOpen] = useState(false);
  const [editingArticleId, setEditingArticleId] = useState<string | null>(null);
  const [articleForm, setArticleForm] = useState<CreateEhcKnowledgeBaseArticleAdmin>({
    code: '',
    title: '',
    summary: '',
    body: '',
    categoryId: null,
    tagsCsv: '',
    isPublished: true,
    isInternalOnly: true,
  });

  useEffect(() => {
    if (!articleOpen) {
      setEditingArticleId(null);
      setArticleForm({ code: '', title: '', summary: '', body: '', categoryId: null, tagsCsv: '', isPublished: true, isInternalOnly: true });
    }
  }, [articleOpen]);

  const loadArticle = useMutation({
    mutationFn: async (id: string) => ehcAdminService.getKbArticle(id),
    onSuccess: (a) => {
      if (!a) return;
      setArticleForm({
        code: a.code,
        title: a.title,
        summary: a.summary ?? '',
        body: a.body ?? '',
        categoryId: a.categoryId ?? null,
        tagsCsv: a.tagsCsv ?? '',
        isPublished: a.isPublished,
        isInternalOnly: a.isInternalOnly,
      });
    },
  });

  const saveArticle = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcKnowledgeBaseArticleAdmin = {
        code: articleForm.code,
        title: articleForm.title,
        summary: articleForm.summary || null,
        body: articleForm.body,
        categoryId: articleForm.categoryId || null,
        tagsCsv: articleForm.tagsCsv || null,
        isPublished: articleForm.isPublished,
        isInternalOnly: articleForm.isInternalOnly,
      };
      if (editingArticleId) await ehcAdminService.updateKbArticle(editingArticleId, payload);
      else await ehcAdminService.createKbArticle(payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'kb', 'articles'] });
      toast({ title: 'Saved', description: 'Article saved.', variant: 'success' });
      setArticleOpen(false);
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  const deleteArticle = useMutation({
    mutationFn: async (id: string) => ehcAdminService.deleteKbArticle(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'kb', 'articles'] });
      toast({ title: 'Deleted', description: 'Article deleted.', variant: 'success' });
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  const categoryColumns = useMemo<Array<DataTableColumn<EhcKnowledgeBaseCategoryAdmin>>>(() => {
    return [
      { id: 'code', header: 'Code', accessorKey: 'code' },
      { id: 'name', header: 'Name', accessorKey: 'name' },
      { id: 'isActive', header: 'Active', accessorKey: 'isActive', cell: ({ row }: TableCellProps<EhcKnowledgeBaseCategoryAdmin>) => (row.original.isActive ? 'Yes' : 'No') },
      {
        id: 'actions',
        header: '',
        accessorKey: 'id',
        enableHiding: false,
        cell: ({ row }: TableCellProps<EhcKnowledgeBaseCategoryAdmin>) => (
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="icon"
              onClick={() => {
                setEditingCategory(row.original);
                setCategoryForm({
                  code: row.original.code,
                  name: row.original.name,
                  description: row.original.description ?? '',
                  isActive: row.original.isActive,
                });
                setCategoryOpen(true);
              }}
              title="Edit"
            >
              <Pencil className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" onClick={() => deleteCategory.mutate(row.original.id)} title="Delete" disabled={deleteCategory.isPending}>
              <Trash2 className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        ),
      },
    ];
  }, [deleteCategory.isPending]);

  const articleColumns = useMemo<Array<DataTableColumn<EhcKnowledgeBaseArticleAdmin>>>(() => {
    return [
      { id: 'code', header: 'Code', accessorKey: 'code' },
      { id: 'title', header: 'Title', accessorKey: 'title' },
      {
        id: 'categoryName',
        header: 'Category',
        accessorFn: (r: EhcKnowledgeBaseArticleAdmin) => r.categoryName || (r.categoryId ? categoryNameById.get(r.categoryId) : '') || '—',
      },
      { id: 'isPublished', header: 'Published', accessorKey: 'isPublished', cell: ({ row }: TableCellProps<EhcKnowledgeBaseArticleAdmin>) => (row.original.isPublished ? 'Yes' : 'No') },
      { id: 'views', header: 'Views', accessorKey: 'viewCount' },
      {
        id: 'actions',
        header: '',
        accessorKey: 'id',
        enableHiding: false,
        cell: ({ row }: TableCellProps<EhcKnowledgeBaseArticleAdmin>) => (
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="icon"
              onClick={async () => {
                setEditingArticleId(row.original.id);
                setArticleOpen(true);
                await loadArticle.mutateAsync(row.original.id);
              }}
              title="Edit"
            >
              <Pencil className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" onClick={() => deleteArticle.mutate(row.original.id)} title="Delete" disabled={deleteArticle.isPending}>
              <Trash2 className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        ),
      },
    ];
  }, [categoryNameById, deleteArticle.isPending, loadArticle]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <BookOpen className="h-7 w-7" />
            Knowledge Base
          </h1>
          <p className="text-slate-600 mt-1">Manage internal helpdesk articles and categories.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant={mode === 'articles' ? 'default' : 'outline'} onClick={() => setMode('articles')}>
            Articles
          </Button>
          <Button variant={mode === 'categories' ? 'default' : 'outline'} onClick={() => setMode('categories')}>
            Categories
          </Button>
          <Button
            onClick={() => {
              if (mode === 'categories') setCategoryOpen(true);
              else setArticleOpen(true);
            }}
          >
            <Plus className="h-4 w-4 mr-2" />
            New
          </Button>
        </div>
      </div>

      {mode === 'categories' ? (
        <DataTable
          compact
          title="KB Categories"
          data={categories}
          columns={categoryColumns}
          loading={catsLoading}
          enableSearch
          enableExport
          exportFileName={`ehc-kb-categories-${new Date().toISOString().slice(0, 10)}`}
          exportFormats={['csv', 'excel']}
        />
      ) : (
        <DataTable
          compact
          title="KB Articles"
          data={articles}
          columns={articleColumns}
          loading={artLoading}
          enableSearch
          enableExport
          exportFileName={`ehc-kb-articles-${new Date().toISOString().slice(0, 10)}`}
          exportFormats={['csv', 'excel']}
        />
      )}

      <Dialog open={categoryOpen} onOpenChange={setCategoryOpen}>
        <DialogContent className="sm:max-w-[650px]">
          <DialogHeader>
            <DialogTitle>{editingCategory ? 'Edit Category' : 'Create Category'}</DialogTitle>
            <DialogDescription>Used to group KB articles and power suggestions.</DialogDescription>
          </DialogHeader>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Details</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Code</Label>
                  <Input value={categoryForm.code} onChange={(e) => setCategoryForm((f) => ({ ...f, code: e.target.value }))} />
                </div>
                <div className="space-y-2">
                  <Label>Name</Label>
                  <Input value={categoryForm.name} onChange={(e) => setCategoryForm((f) => ({ ...f, name: e.target.value }))} />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea value={categoryForm.description ?? ''} onChange={(e) => setCategoryForm((f) => ({ ...f, description: e.target.value }))} rows={3} />
              </div>
              <div className="flex items-center justify-between rounded-md border bg-white p-3">
                <div>
                  <div className="font-medium text-slate-900">Active</div>
                  <div className="text-xs text-slate-500">Inactive categories are hidden from search.</div>
                </div>
                <Switch checked={categoryForm.isActive} onCheckedChange={(v) => setCategoryForm((f) => ({ ...f, isActive: v }))} />
              </div>
              <div className="flex justify-end">
                <Button onClick={() => saveCategory.mutate()} disabled={saveCategory.isPending}>
                  {saveCategory.isPending ? 'Saving…' : 'Save'}
                </Button>
              </div>
            </CardContent>
          </Card>
        </DialogContent>
      </Dialog>

      <Dialog open={articleOpen} onOpenChange={setArticleOpen}>
        <DialogContent className="sm:max-w-[900px] max-h-[85vh] overflow-auto">
          <DialogHeader>
            <DialogTitle>{editingArticleId ? 'Edit Article' : 'Create Article'}</DialogTitle>
            <DialogDescription>Markdown/plain text is supported (Phase 2 baseline).</DialogDescription>
          </DialogHeader>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Content</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Code</Label>
                  <Input value={articleForm.code} onChange={(e) => setArticleForm((f) => ({ ...f, code: e.target.value }))} />
                </div>
                <div className="space-y-2">
                  <Label>Title</Label>
                  <Input value={articleForm.title} onChange={(e) => setArticleForm((f) => ({ ...f, title: e.target.value }))} />
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Category</Label>
                  <Select value={articleForm.categoryId ? String(articleForm.categoryId) : '__none'} onValueChange={(v) => setArticleForm((f) => ({ ...f, categoryId: v === '__none' ? null : v }))}>
                    <SelectTrigger>
                      <SelectValue placeholder="None" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none">None</SelectItem>
                      {categories.map((c) => (
                        <SelectItem key={c.id} value={c.id}>
                          {c.code} • {c.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Tags (CSV)</Label>
                  <Input value={articleForm.tagsCsv ?? ''} onChange={(e) => setArticleForm((f) => ({ ...f, tagsCsv: e.target.value }))} placeholder="printer, login, timeout" />
                </div>
              </div>

              <div className="space-y-2">
                <Label>Summary</Label>
                <Textarea value={articleForm.summary ?? ''} onChange={(e) => setArticleForm((f) => ({ ...f, summary: e.target.value }))} rows={3} />
              </div>

              <div className="space-y-2">
                <Label>Body</Label>
                <Textarea value={articleForm.body} onChange={(e) => setArticleForm((f) => ({ ...f, body: e.target.value }))} rows={12} />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="flex items-center justify-between rounded-md border bg-white p-3">
                  <div>
                    <div className="font-medium text-slate-900">Published</div>
                    <div className="text-xs text-slate-500">If off, hidden from agent search.</div>
                  </div>
                  <Switch checked={articleForm.isPublished} onCheckedChange={(v) => setArticleForm((f) => ({ ...f, isPublished: v }))} />
                </div>
                <div className="flex items-center justify-between rounded-md border bg-white p-3">
                  <div>
                    <div className="font-medium text-slate-900">Internal-only</div>
                    <div className="text-xs text-slate-500">Phase 2 baseline: keep internal.</div>
                  </div>
                  <Switch checked={articleForm.isInternalOnly} onCheckedChange={(v) => setArticleForm((f) => ({ ...f, isInternalOnly: v }))} />
                </div>
              </div>

              <div className="flex justify-end">
                <Button onClick={() => saveArticle.mutate()} disabled={saveArticle.isPending}>
                  {saveArticle.isPending ? 'Saving…' : 'Save'}
                </Button>
              </div>
            </CardContent>
          </Card>
        </DialogContent>
      </Dialog>
    </div>
  );
}
