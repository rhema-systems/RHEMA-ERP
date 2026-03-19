'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { HelpCircle, Pencil, Plus, Trash2 } from 'lucide-react';

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
  type CreateEhcFaqCategoryAdmin,
  type CreateEhcFaqItemAdmin,
  type EhcFaqCategoryAdmin,
  type EhcFaqItemAdmin,
} from '@/services/ehcAdminService';

type Mode = 'items' | 'categories';
type TableCellProps<T> = {
  row: {
    original: T;
  };
};

export default function AdminHelpdeskFaqsPage() {
  const qc = useQueryClient();
  const { toast } = useToast();
  const [mode, setMode] = useState<Mode>('items');

  const { data: categories = [], isLoading: catsLoading } = useQuery({
    queryKey: ['ehc', 'admin', 'faq', 'categories'],
    queryFn: () => ehcAdminService.listFaqCategories(),
  });

  const { data: items = [], isLoading: itemsLoading } = useQuery({
    queryKey: ['ehc', 'admin', 'faq', 'items'],
    queryFn: () => ehcAdminService.listFaqItems(),
  });

  const categoryLabelById = useMemo(() => new Map(categories.map((c) => [c.id, `${c.code} • ${c.name}`])), [categories]);

  // Category dialog
  const [categoryOpen, setCategoryOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<EhcFaqCategoryAdmin | null>(null);
  const [categoryForm, setCategoryForm] = useState<CreateEhcFaqCategoryAdmin>({
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
      const payload: CreateEhcFaqCategoryAdmin = {
        code: categoryForm.code,
        name: categoryForm.name,
        description: categoryForm.description || null,
        isActive: categoryForm.isActive,
      };
      if (editingCategory) await ehcAdminService.updateFaqCategory(editingCategory.id, payload);
      else await ehcAdminService.createFaqCategory(payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'faq', 'categories'] });
      toast({ title: 'Saved', description: 'FAQ category saved.', variant: 'success' });
      setCategoryOpen(false);
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  const deleteCategory = useMutation({
    mutationFn: async (id: string) => ehcAdminService.deleteFaqCategory(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'faq', 'categories'] });
      toast({ title: 'Deleted', description: 'FAQ category deleted.', variant: 'success' });
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  // FAQ item dialog
  const [itemOpen, setItemOpen] = useState(false);
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [itemForm, setItemForm] = useState<CreateEhcFaqItemAdmin>({
    question: '',
    answer: '',
    categoryId: null,
    isPublished: true,
    isInternalOnly: true,
    sortOrder: 0,
  });

  useEffect(() => {
    if (!itemOpen) {
      setEditingItemId(null);
      setItemForm({ question: '', answer: '', categoryId: null, isPublished: true, isInternalOnly: true, sortOrder: 0 });
    }
  }, [itemOpen]);

  const loadItem = useMutation({
    mutationFn: async (id: string) => ehcAdminService.getFaqItem(id),
    onSuccess: (i) => {
      if (!i) return;
      setItemForm({
        question: i.question,
        answer: i.answer,
        categoryId: i.categoryId ?? null,
        isPublished: i.isPublished,
        isInternalOnly: i.isInternalOnly,
        sortOrder: i.sortOrder ?? 0,
      });
    },
  });

  const saveItem = useMutation({
    mutationFn: async () => {
      const payload: CreateEhcFaqItemAdmin = {
        question: itemForm.question,
        answer: itemForm.answer,
        categoryId: itemForm.categoryId || null,
        isPublished: itemForm.isPublished,
        isInternalOnly: itemForm.isInternalOnly,
        sortOrder: Number.isFinite(itemForm.sortOrder) ? itemForm.sortOrder : 0,
      };
      if (editingItemId) await ehcAdminService.updateFaqItem(editingItemId, payload);
      else await ehcAdminService.createFaqItem(payload);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'faq', 'items'] });
      toast({ title: 'Saved', description: 'FAQ saved.', variant: 'success' });
      setItemOpen(false);
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  const deleteItem = useMutation({
    mutationFn: async (id: string) => ehcAdminService.deleteFaqItem(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'faq', 'items'] });
      toast({ title: 'Deleted', description: 'FAQ deleted.', variant: 'success' });
    },
    onError: (err) => toast({ title: 'Error', description: err instanceof Error ? err.message : 'Failed', variant: 'destructive' }),
  });

  const categoryColumns = useMemo<Array<DataTableColumn<EhcFaqCategoryAdmin>>>(() => {
    return [
      { id: 'code', header: 'Code', accessorKey: 'code' },
      { id: 'name', header: 'Name', accessorKey: 'name' },
      { id: 'isActive', header: 'Active', accessorKey: 'isActive', cell: ({ row }: TableCellProps<EhcFaqCategoryAdmin>) => (row.original.isActive ? 'Yes' : 'No') },
      {
        id: 'actions',
        header: '',
        accessorKey: 'id',
        enableHiding: false,
        cell: ({ row }: TableCellProps<EhcFaqCategoryAdmin>) => (
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

  const itemColumns = useMemo<Array<DataTableColumn<EhcFaqItemAdmin>>>(() => {
    return [
      { id: 'sortOrder', header: '#', accessorKey: 'sortOrder' },
      { id: 'question', header: 'Question', accessorKey: 'question' },
      {
        id: 'categoryName',
        header: 'Category',
        accessorFn: (r: EhcFaqItemAdmin) => r.categoryName || (r.categoryId ? categoryLabelById.get(r.categoryId) : '') || '—',
      },
      { id: 'isPublished', header: 'Published', accessorKey: 'isPublished', cell: ({ row }: TableCellProps<EhcFaqItemAdmin>) => (row.original.isPublished ? 'Yes' : 'No') },
      { id: 'isInternalOnly', header: 'Internal Only', accessorKey: 'isInternalOnly', cell: ({ row }: TableCellProps<EhcFaqItemAdmin>) => (row.original.isInternalOnly ? 'Yes' : 'No') },
      { id: 'viewCount', header: 'Views', accessorKey: 'viewCount' },
      {
        id: 'actions',
        header: '',
        accessorKey: 'id',
        enableHiding: false,
        cell: ({ row }: TableCellProps<EhcFaqItemAdmin>) => (
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="icon"
              onClick={async () => {
                setEditingItemId(row.original.id);
                setItemOpen(true);
                await loadItem.mutateAsync(row.original.id);
              }}
              title="Edit"
            >
              <Pencil className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon" onClick={() => deleteItem.mutate(row.original.id)} title="Delete" disabled={deleteItem.isPending}>
              <Trash2 className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        ),
      },
    ];
  }, [categoryLabelById, deleteItem.isPending, loadItem]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <HelpCircle className="h-7 w-7" />
            FAQs
          </h1>
          <p className="text-slate-600 mt-1">Create and manage FAQs for the helpdesk module.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant={mode === 'items' ? 'default' : 'outline'} onClick={() => setMode('items')}>
            FAQs
          </Button>
          <Button variant={mode === 'categories' ? 'default' : 'outline'} onClick={() => setMode('categories')}>
            Categories
          </Button>
          <Button
            onClick={() => {
              if (mode === 'categories') setCategoryOpen(true);
              else setItemOpen(true);
            }}
          >
            <Plus className="h-4 w-4 mr-2" />
            New
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader className="p-4 pb-2">
          <CardTitle>{mode === 'categories' ? 'FAQ Categories' : 'FAQs'}</CardTitle>
          <CardDescription className="text-xs">{mode === 'categories' ? 'Organize FAQs by category.' : 'Published FAQs appear in the Helpdesk FAQ page.'}</CardDescription>
        </CardHeader>
        <CardContent className="p-4 pt-0">
          {mode === 'categories' ? (
            <DataTable
              compact
              data={categories}
              columns={categoryColumns}
              loading={catsLoading}
              enableColumnFilters={false}
              enableExport={true}
              exportFormats={['csv', 'excel']}
              exportFileName={`faq-categories-${new Date().toISOString().slice(0, 10)}`}
            />
          ) : (
            <DataTable
              compact
              data={items}
              columns={itemColumns}
              loading={itemsLoading}
              enableColumnFilters={false}
              enableExport={true}
              exportFormats={['csv', 'excel']}
              exportFileName={`faqs-${new Date().toISOString().slice(0, 10)}`}
              initialColumnVisibility={{ isInternalOnly: false, viewCount: false }}
            />
          )}
        </CardContent>
      </Card>

      <Dialog open={categoryOpen} onOpenChange={setCategoryOpen}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{editingCategory ? 'Edit Category' : 'New Category'}</DialogTitle>
            <DialogDescription>Use a short code and a friendly name.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-3">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label>Code</Label>
                <Input value={categoryForm.code} onChange={(e) => setCategoryForm((s) => ({ ...s, code: e.target.value }))} placeholder="IT" />
              </div>
              <div className="space-y-1">
                <Label>Name</Label>
                <Input value={categoryForm.name} onChange={(e) => setCategoryForm((s) => ({ ...s, name: e.target.value }))} placeholder="IT Support" />
              </div>
            </div>

            <div className="space-y-1">
              <Label>Description</Label>
              <Textarea value={categoryForm.description || ''} onChange={(e) => setCategoryForm((s) => ({ ...s, description: e.target.value }))} rows={3} />
            </div>

            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <div className="font-medium">Active</div>
                <div className="text-xs text-slate-500">Inactive categories won’t show in the Helpdesk FAQ filters.</div>
              </div>
              <Switch checked={categoryForm.isActive} onCheckedChange={(v) => setCategoryForm((s) => ({ ...s, isActive: v }))} />
            </div>

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setCategoryOpen(false)}>
                Cancel
              </Button>
              <Button onClick={() => saveCategory.mutate()} disabled={saveCategory.isPending}>
                Save
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={itemOpen} onOpenChange={setItemOpen}>
        <DialogContent className="sm:max-w-[820px] max-h-[85vh] overflow-auto">
          <DialogHeader>
            <DialogTitle>{editingItemId ? 'Edit FAQ' : 'New FAQ'}</DialogTitle>
            <DialogDescription>Keep answers concise. Use categories to make it easier to find.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-3">
            <div className="grid grid-cols-1 md:grid-cols-12 gap-3">
              <div className="md:col-span-7 space-y-1">
                <Label>Category</Label>
                <Select value={itemForm.categoryId || '__none'} onValueChange={(v) => setItemForm((s) => ({ ...s, categoryId: v === '__none' ? null : v }))}>
                  <SelectTrigger>
                    <SelectValue placeholder="No category" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none">No category</SelectItem>
                    {categories.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.code} • {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="md:col-span-5 space-y-1">
                <Label>Sort Order</Label>
                <Input
                  type="number"
                  value={String(itemForm.sortOrder ?? 0)}
                  onChange={(e) => setItemForm((s) => ({ ...s, sortOrder: parseInt(e.target.value || '0', 10) }))}
                  placeholder="0"
                />
              </div>
            </div>

            <div className="space-y-1">
              <Label>Question</Label>
              <Input value={itemForm.question} onChange={(e) => setItemForm((s) => ({ ...s, question: e.target.value }))} placeholder="How do I reset my password?" />
            </div>

            <div className="space-y-1">
              <Label>Answer</Label>
              <Textarea value={itemForm.answer} onChange={(e) => setItemForm((s) => ({ ...s, answer: e.target.value }))} rows={8} />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <div className="font-medium">Published</div>
                  <div className="text-xs text-slate-500">Unpublished FAQs are hidden from the Helpdesk FAQ page.</div>
                </div>
                <Switch checked={itemForm.isPublished} onCheckedChange={(v) => setItemForm((s) => ({ ...s, isPublished: v }))} />
              </div>
              <div className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <div className="font-medium">Internal Only</div>
                  <div className="text-xs text-slate-500">When off, this FAQ can be reused in external views later.</div>
                </div>
                <Switch checked={itemForm.isInternalOnly} onCheckedChange={(v) => setItemForm((s) => ({ ...s, isInternalOnly: v }))} />
              </div>
            </div>

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setItemOpen(false)}>
                Cancel
              </Button>
              <Button onClick={() => saveItem.mutate()} disabled={saveItem.isPending}>
                Save
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
