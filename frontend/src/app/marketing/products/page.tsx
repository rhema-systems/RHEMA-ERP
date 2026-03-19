'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Package, Search, RefreshCw, Plus, DollarSign } from 'lucide-react';
import { toast } from 'sonner';
import { campaignService, type ProductSummaryDto } from '@/services/campaignService';

export default function ProductsPage() {
  const [products, setProducts] = useState<ProductSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => { loadProducts(); }, [page]);

  const loadProducts = async () => {
    try {
      setLoading(true);
      const data = await campaignService.getProducts(page, pageSize, searchTerm || undefined);
      setProducts(data.items); setTotalCount(data.totalCount);
    } catch (error) { console.error(error); toast.error('Failed to load products'); }
    finally { setLoading(false); }
  };

  const handleSearch = () => { setPage(1); loadProducts(); };
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><Package className="h-8 w-8 text-blue-600" />Product Catalog</h1>
          <p className="text-gray-500">Manage your sales product catalog</p>
        </div>
        <Button><Plus className="h-4 w-4 mr-2" />Add Product</Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Products</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{totalCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{products.filter(p => p.isActive).length}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Avg Margin</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{products.length ? (products.reduce((s, p) => s + p.margin, 0) / products.length).toFixed(1) : 0}%</p></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle>Filter</CardTitle></CardHeader>
        <CardContent>
          <div className="flex gap-2 max-w-md">
            <Input placeholder="Search by name or SKU..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
            <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            <Button variant="outline" onClick={loadProducts}><RefreshCw className="h-4 w-4" /></Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Products</CardTitle><CardDescription>Showing {products.length} of {totalCount}</CardDescription></CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8"><Package className="h-12 w-12 animate-pulse mx-auto mb-4 text-blue-500" /><p className="text-gray-500">Loading...</p></div>
          ) : products.length === 0 ? (
            <div className="text-center py-8"><Package className="h-12 w-12 mx-auto mb-4 text-gray-400" /><p className="text-gray-500">No products found</p></div>
          ) : (
            <>
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Name</TableHead><TableHead>SKU</TableHead><TableHead>Category</TableHead>
                  <TableHead>Price</TableHead><TableHead>Cost</TableHead><TableHead>Margin</TableHead><TableHead>Status</TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {products.map((p) => (
                    <TableRow key={p.id} className="cursor-pointer hover:bg-gray-50">
                      <TableCell className="font-medium">{p.name}</TableCell>
                      <TableCell className="font-mono text-sm">{p.sku || '-'}</TableCell>
                      <TableCell>{p.category || '-'}</TableCell>
                      <TableCell className="font-semibold"><DollarSign className="h-3 w-3 inline" />{p.unitPrice.toLocaleString()}</TableCell>
                      <TableCell>${p.cost.toLocaleString()}</TableCell>
                      <TableCell className={p.margin > 0 ? 'text-green-600' : 'text-red-600'}>{p.margin.toFixed(1)}%</TableCell>
                      <TableCell><Badge className={p.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>{p.isActive ? 'Active' : 'Inactive'}</Badge></TableCell>
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
