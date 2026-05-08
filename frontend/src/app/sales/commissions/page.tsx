'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Award, Search, RefreshCw, Plus, CheckCircle, DollarSign, FileText } from 'lucide-react';
import { toast } from 'sonner';
import { commissionService, type CommissionRuleSummary, type CommissionStatementSummary } from '@/services/commissionService';
import { format } from 'date-fns';

const RULE_TYPE_CONFIG: Record<string, { className: string }> = {
  FlatPercentage: { className: 'bg-blue-100 text-blue-800' },
  Tiered: { className: 'bg-purple-100 text-purple-800' },
  RuleBased: { className: 'bg-amber-100 text-amber-800' },
};
const STMT_STATUS_CONFIG: Record<string, { className: string }> = {
  Draft: { className: 'bg-gray-100 text-gray-800' },
  Calculated: { className: 'bg-blue-100 text-blue-800' },
  Approved: { className: 'bg-green-100 text-green-800' },
  Paid: { className: 'bg-emerald-100 text-emerald-800' },
  Disputed: { className: 'bg-red-100 text-red-800' },
};

export default function CommissionsPage() {
  const [rules, setRules] = useState<CommissionRuleSummary[]>([]);
  const [statements, setStatements] = useState<CommissionStatementSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [stmtPage, setStmtPage] = useState(1);
  const [stmtTotalCount, setStmtTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadRules(); }, [page]);
  useEffect(() => { loadStatements(); }, [stmtPage]);

  const loadRules = async () => {
    try {
      setLoading(true);
      const data = await commissionService.getRules(page, pageSize, searchTerm || undefined);
      setRules(data.data.items); setTotalCount(data.data.totalCount);
    } catch { toast.error('Failed to load rules'); }
    finally { setLoading(false); }
  };
  const loadStatements = async () => {
    try {
      const data = await commissionService.getStatements(stmtPage, pageSize);
      setStatements(data.data.items); setStmtTotalCount(data.data.totalCount);
    } catch { toast.error('Failed to load statements'); }
  };
  const handleApprove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await commissionService.approveStatement(id); toast.success('Statement approved'); loadStatements(); } catch (err: any) { toast.error(err.message); }
  };
  const handlePay = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try { await commissionService.markAsPaid(id); toast.success('Statement marked as paid'); loadStatements(); } catch (err: any) { toast.error(err.message); }
  };

  const formatDate = (d?: string) => { if (!d) return '-'; try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; } };
  const ruleTotalPages = Math.ceil(totalCount / pageSize);
  const stmtTotalPages = Math.ceil(stmtTotalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Award className="h-8 w-8 text-amber-600" />Commission Management</h1>
          <p className="text-gray-500">Configure commission rules and manage statements</p>
        </div>
      </div>

      <Tabs defaultValue="rules">
        <TabsList><TabsTrigger value="rules">Commission Rules</TabsTrigger><TabsTrigger value="statements">Statements</TabsTrigger></TabsList>

        <TabsContent value="rules" className="space-y-4">
          <div className="flex gap-2">
            <Input placeholder="Search rules..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && loadRules()} className="max-w-sm" />
            <Button onClick={loadRules}><Search className="h-4 w-4" /></Button>
            <Button variant="outline" onClick={loadRules}><RefreshCw className="h-4 w-4" /></Button>
          </div>
          <Card>
            <CardHeader><CardTitle>Rules</CardTitle><CardDescription>Showing {rules.length} of {totalCount}</CardDescription></CardHeader>
            <CardContent>
              {loading ? (
                <div className="text-center py-8"><Award className="h-12 w-12 animate-pulse mx-auto mb-4 text-amber-500" /><p className="text-gray-500">Loading...</p></div>
              ) : rules.length === 0 ? (
                <div className="text-center py-8"><Award className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No rules found</p></div>
              ) : (
                <Table>
                  <TableHeader><TableRow>
                    <TableHead>Name</TableHead><TableHead>Type</TableHead><TableHead>Rate</TableHead>
                    <TableHead>Min Sale</TableHead><TableHead>Max Commission</TableHead><TableHead>Active</TableHead><TableHead>Effective</TableHead>
                  </TableRow></TableHeader>
                  <TableBody>
                    {rules.map((r) => (
                      <TableRow key={r.id} className="cursor-pointer hover:bg-gray-50">
                        <TableCell className="font-medium">{r.name}</TableCell>
                        <TableCell><Badge className={RULE_TYPE_CONFIG[r.commissionType]?.className || ''}>{r.commissionType}</Badge></TableCell>
                        <TableCell>{r.rate}%</TableCell>
                        <TableCell>${r.minimumSaleAmount.toLocaleString()}</TableCell>
                        <TableCell>${r.maximumCommission.toLocaleString()}</TableCell>
                        <TableCell><Badge variant={r.isActive ? 'default' : 'secondary'}>{r.isActive ? 'Active' : 'Inactive'}</Badge></TableCell>
                        <TableCell className="text-sm">{formatDate(r.effectiveFrom)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
              {ruleTotalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <p className="text-sm text-gray-600">Page {page} of {ruleTotalPages}</p>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}>Previous</Button>
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.min(ruleTotalPages, p + 1))} disabled={page === ruleTotalPages}>Next</Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="statements" className="space-y-4">
          <Card>
            <CardHeader><CardTitle>Commission Statements</CardTitle><CardDescription>Showing {statements.length} of {stmtTotalCount}</CardDescription></CardHeader>
            <CardContent>
              {statements.length === 0 ? (
                <div className="text-center py-8"><FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No statements found</p></div>
              ) : (
                <Table>
                  <TableHeader><TableRow>
                    <TableHead>Statement #</TableHead><TableHead>Sales Rep</TableHead><TableHead>Period</TableHead>
                    <TableHead>Total Sales</TableHead><TableHead>Commission</TableHead><TableHead>Net</TableHead><TableHead>Status</TableHead><TableHead>Actions</TableHead>
                  </TableRow></TableHeader>
                  <TableBody>
                    {statements.map((s) => (
                      <TableRow key={s.id}>
                        <TableCell className="font-mono">{s.statementNumber}</TableCell>
                        <TableCell>{s.salesRepName}</TableCell>
                        <TableCell className="text-sm">{formatDate(s.periodStart)} – {formatDate(s.periodEnd)}</TableCell>
                        <TableCell>${s.totalSales.toLocaleString()}</TableCell>
                        <TableCell className="text-green-600 font-semibold">${s.totalCommission.toLocaleString()}</TableCell>
                        <TableCell className="font-bold">${s.netCommission.toLocaleString()}</TableCell>
                        <TableCell><Badge className={STMT_STATUS_CONFIG[s.status]?.className || ''}>{s.status}</Badge></TableCell>
                        <TableCell>
                          <div className="flex gap-1">
                            {(s.status === 'Calculated' || s.status === 'Draft') && <Button variant="outline" size="sm" onClick={(e) => handleApprove(s.id, e)}><CheckCircle className="h-3 w-3 mr-1" />Approve</Button>}
                            {s.status === 'Approved' && <Button variant="outline" size="sm" onClick={(e) => handlePay(s.id, e)}><DollarSign className="h-3 w-3 mr-1" />Pay</Button>}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
              {stmtTotalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <p className="text-sm text-gray-600">Page {stmtPage} of {stmtTotalPages}</p>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => setStmtPage(p => Math.max(1, p - 1))} disabled={stmtPage === 1}>Previous</Button>
                    <Button variant="outline" size="sm" onClick={() => setStmtPage(p => Math.min(stmtTotalPages, p + 1))} disabled={stmtPage === stmtTotalPages}>Next</Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
