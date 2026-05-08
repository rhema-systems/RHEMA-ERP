'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { BookTemplate, Search, RefreshCw, Plus, CheckCircle, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { salesJournalTemplateService, type JournalTemplateSummary } from '@/services/salesJournalTemplateService';
import { format } from 'date-fns';

export default function JournalTemplatesPage() {
  const [templates, setTemplates] = useState<JournalTemplateSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadTemplates(); }, [page]);

  const loadTemplates = async () => {
    try {
      setLoading(true);
      const data = await salesJournalTemplateService.getTemplates(page, pageSize, searchTerm || undefined);
      setTemplates(data.data.items); setTotalCount(data.data.totalCount);
    } catch { toast.error('Failed to load templates'); }
    finally { setLoading(false); }
  };

  const handleActivate = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await salesJournalTemplateService.activateTemplate(id); toast.success('Template activated'); loadTemplates(); } catch (err: any) { toast.error(err.message); }
  };
  const handleDeactivate = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await salesJournalTemplateService.deactivateTemplate(id); toast.success('Template deactivated'); loadTemplates(); } catch (err: any) { toast.error(err.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><BookTemplate className="h-8 w-8 text-teal-600" />Sales Journal Templates</h1>
          <p className="text-gray-500">Configure GL posting templates for sales transactions</p>
        </div>
        <Button className="bg-teal-600 hover:bg-teal-700"><Plus className="h-4 w-4 mr-2" />New Template</Button>
      </div>

      <div className="flex gap-2">
        <Input placeholder="Search templates..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && loadTemplates()} className="max-w-sm" />
        <Button onClick={loadTemplates}><Search className="h-4 w-4" /></Button>
        <Button variant="outline" onClick={loadTemplates}><RefreshCw className="h-4 w-4" /></Button>
      </div>

      <Card>
        <CardHeader><CardTitle>Templates</CardTitle><CardDescription>Showing {templates.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><BookTemplate className="h-12 w-12 animate-pulse mx-auto mb-4 text-teal-500" /><p className="text-gray-500">Loading...</p></div>
          ) : templates.length === 0 ? (
            <div className="text-center py-8"><BookTemplate className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No templates found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Name</TableHead><TableHead>Transaction Type</TableHead><TableHead>Lines</TableHead>
                  <TableHead>Status</TableHead><TableHead>Created</TableHead><TableHead>Actions</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {templates.map((t) => (
                    <TableRow key={t.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell>
                        <div><span className="font-medium">{t.name}</span>{t.description && <p className="text-sm text-gray-500 truncate max-w-[300px]">{t.description}</p>}</div>
                      </TableCell>
                      <TableCell><Badge variant="outline">{t.transactionType}</Badge></TableCell>
                      <TableCell className="font-semibold">{t.lineCount}</TableCell>
                      <TableCell><Badge variant={t.isActive ? 'default' : 'secondary'}>{t.isActive ? 'Active' : 'Inactive'}</Badge></TableCell>
                      <TableCell className="text-sm">{formatDate(t.createdAt)}</TableCell>
                      <TableCell>
                        {t.isActive ?
                          <Button variant="outline" size="sm" onClick={(e) => handleDeactivate(t.id, e)}><XCircle className="h-3 w-3 mr-1" />Deactivate</Button> :
                          <Button variant="outline" size="sm" onClick={(e) => handleActivate(t.id, e)}><CheckCircle className="h-3 w-3 mr-1" />Activate</Button>
                        }
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              {totalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <p className="text-sm text-gray-600">Page {page} of {totalPages}</p>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}>Previous</Button>
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}>Next</Button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
