'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandList } from '@/components/ui/command';
import { Calendar } from '@/components/ui/calendar';
import { FileText, Search, RefreshCw, Plus, Send, CheckCircle, XCircle, Trash2, ChevronsUpDown, Check, Calendar as CalendarIcon, Percent, DollarSign, ArrowRight } from 'lucide-react';
import { toast } from 'sonner';
import { crmService, type QuoteSummaryDto, type CreateQuoteDto, type CreateQuoteLineItemDto } from '@/services/crmService';
import { arService } from '@/services/ar-service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { financeService } from '@/services/finance.service';
import { cn } from '@/lib/utils';
import { format } from 'date-fns';

const STATUS_CONFIG: Record<string, { className: string }> = {
  Draft: { className: 'bg-slate-100 text-slate-800 border-slate-200' },
  Sent: { className: 'bg-sky-100 text-sky-800 border-sky-200' },
  Accepted: { className: 'bg-emerald-100 text-emerald-800 border-emerald-200' },
  Rejected: { className: 'bg-rose-100 text-rose-800 border-rose-200' },
  Expired: { className: 'bg-amber-100 text-amber-800 border-amber-200' },
  Revised: { className: 'bg-violet-100 text-violet-800 border-violet-200' },
};

const EMPTY_LINE: CreateQuoteLineItemDto = {
  description: '',
  quantity: 1,
  unitPrice: 0,
  discountPercentage: 0,
  taxGroupId: '',
};

const makeEmptyQuote = (): CreateQuoteDto => ({
  opportunityId: '00000000-0000-0000-0000-000000000000',
  businessPartnerId: '',
  quoteName: '',
  validUntil: new Date(Date.now() + 30 * 86400000).toISOString().split('T')[0],
  currency: 'GHS',
  exchangeRate: 1.0,
  taxGroupId: '',
  lineItems: [{ ...EMPTY_LINE }],
});

export default function QuotesPage() {
  const [quotes, setQuotes] = useState<QuoteSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<CreateQuoteDto>(makeEmptyQuote());

  // Customer combobox and tax group states
  const [customers, setCustomers] = useState<any[]>([]);
  const [taxGroups, setTaxGroups] = useState<any[]>([]);
  const [selectedCustomer, setSelectedCustomer] = useState<any>(null);
  const [customerComboOpen, setCustomerComboOpen] = useState(false);
  const [customerSearch, setCustomerSearch] = useState('');
  const [datePickerOpen, setDatePickerOpen] = useState(false);

  useEffect(() => {
    loadQuotes();
    loadCustomers();
    loadTaxGroups();
  }, [page, statusFilter]);

  const loadQuotes = async () => {
    try {
      setLoading(true);
      const data = await crmService.getQuotes(page, pageSize, searchTerm || undefined, statusFilter || undefined);
      setQuotes(data.items);
      setTotalCount(data.totalCount);
    } catch (error) {
      console.error(error);
      toast.error('Failed to load quotes');
    } finally {
      setLoading(false);
    }
  };

  const loadCustomers = async () => {
    try {
      const result = await arService.getCustomers({ pageSize: 100 });
      setCustomers(result.items || []);
    } catch (error) {
      console.error('Failed to load customers:', error);
    }
  };

  const loadTaxGroups = async () => {
    try {
      const groups = await taxDataService.getTaxGroups({ isActive: true, applicability: 'Sales' });
      setTaxGroups(groups || []);
    } catch (error) {
      console.error('Failed to load tax groups:', error);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadQuotes();
  };

  const handleSend = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await crmService.sendQuote(id);
      toast.success('Quote sent successfully');
      loadQuotes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to send quote');
    }
  };

  const handleAccept = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await crmService.acceptQuote(id);
      toast.success('Quote accepted successfully');
      loadQuotes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to accept quote');
    }
  };

  const handleReject = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await crmService.rejectQuote(id);
      toast.success('Quote rejected');
      loadQuotes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to reject quote');
    }
  };

  const handleConvertToOrder = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      const res = await crmService.convertToSalesOrder(id);
      toast.success('Successfully converted to Sales Order!');
      loadQuotes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to convert quote');
    }
  };

  const onCustomerChange = async (customerId: string) => {
    const customer = customers.find(c => c.id === customerId);
    setSelectedCustomer(customer);
    const currency = customer?.currencyCode || 'GHS';
    const isForeign = currency !== 'GHS';

    setForm(f => ({
      ...f,
      businessPartnerId: customerId,
      currency,
      exchangeRate: isForeign ? f.exchangeRate : 1.0,
    }));

    if (isForeign) {
      try {
        const rateObj = await financeService.getCurrentExchangeRate(currency);
        const rawRate = rateObj?.rate || rateObj?.currentExchangeRate || 1.0;
        const finalRate = rawRate < 1 ? Number((1 / rawRate).toFixed(4)) : rawRate;
        setForm(f => ({ ...f, exchangeRate: finalRate }));
      } catch (err) {
        console.error("Failed to fetch exchange rate for customer currency", err);
      }
    }
  };

  const handleCreate = async () => {
    if (!form.quoteName.trim()) {
      toast.error('Quote name is required');
      return;
    }
    if (!form.businessPartnerId) {
      toast.error('Please select a customer');
      return;
    }
    const validLines = form.lineItems.filter(l => l.description.trim() && l.quantity > 0);
    if (validLines.length === 0) {
      toast.error('Add at least one line item with description');
      return;
    }

    try {
      setCreating(true);
      await crmService.createQuote({
        ...form,
        taxGroupId: form.taxGroupId === 'none' ? undefined : (form.taxGroupId || undefined),
        lineItems: validLines.map(item => ({
          ...item,
          taxGroupId: item.taxGroupId === 'none' ? undefined : (item.taxGroupId || undefined),
        })),
      });
      toast.success('Quote created successfully');
      setShowCreate(false);
      setForm(makeEmptyQuote());
      setSelectedCustomer(null);
      loadQuotes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to create quote');
    } finally {
      setCreating(false);
    }
  };

  const updateLine = (idx: number, field: keyof CreateQuoteLineItemDto, value: any) => {
    setForm(f => {
      const lineItems = [...f.lineItems];
      lineItems[idx] = { ...lineItems[idx], [field]: value };
      return { ...f, lineItems };
    });
  };

  const addLine = () => setForm(f => ({ ...f, lineItems: [...f.lineItems, { ...EMPTY_LINE }] }));
  const removeLine = (idx: number) => setForm(f => ({ ...f, lineItems: f.lineItems.filter((_, i) => i !== idx) }));

  const formatDateString = (d?: string) => {
    if (!d) return '-';
    try {
      return format(new Date(d), 'dd MMM yyyy');
    } catch {
      return d;
    }
  };

  // Live calculation of subtotal, tax amount, discount, and grand totals
  const getTotals = () => {
    let subtotal = 0;
    let totalDiscount = 0;
    let totalTaxAmount = 0;
    const breakdowns: { [taxCode: string]: { name: string; rate: number; amount: number } } = {};

    form.lineItems.forEach((item) => {
      const qty = Number(item.quantity) || 0;
      const price = Number(item.unitPrice) || 0;
      const discPercent = Number(item.discountPercentage) || 0;

      const gross = qty * price;
      const discount = gross * (discPercent / 100);
      const netTaxable = gross - discount;

      subtotal += gross;
      totalDiscount += discount;

      // Resolve line tax group override or default to header
      const activeGroupId = item.taxGroupId || form.taxGroupId;
      const activeGroup = taxGroups?.find(tg => tg.id === activeGroupId);

      if (activeGroup && activeGroup.components) {
        let cumulativeBase = netTaxable;
        const sortedComponents = [...activeGroup.components].sort((a, b) => a.calculationOrder - b.calculationOrder);

        sortedComponents.forEach(comp => {
          if (comp.taxCategory === 'Withholding') return; // sales tax only

          let taxableBasis = netTaxable;
          if (comp.compoundBasis === 'Cumulative') {
            taxableBasis = cumulativeBase;
          }

          const taxAmt = taxableBasis * (Number(comp.taxRate) / 100);
          totalTaxAmount += taxAmt;

          if (comp.compoundBasis === 'Cumulative' || comp.compoundBasis === 'BaseOnly') {
            cumulativeBase += taxAmt;
          }

          if (breakdowns[comp.taxCode]) {
            breakdowns[comp.taxCode].amount += taxAmt;
          } else {
            breakdowns[comp.taxCode] = {
              name: comp.taxName,
              rate: comp.taxRate,
              amount: taxAmt
            };
          }
        });
      }
    });

    const grandTotal = (subtotal - totalDiscount) + totalTaxAmount;
    const exchangeRate = Number(form.exchangeRate) || 1.0;
    const baseGrandTotal = grandTotal * exchangeRate;

    return {
      subtotal,
      totalDiscount,
      totalTaxAmount,
      grandTotal,
      baseGrandTotal,
      taxList: Object.entries(breakdowns).map(([code, data]) => ({ code, ...data }))
    };
  };

  const totals = getTotals();
  const totalPages = Math.ceil(totalCount / pageSize);

  const filteredCustomers = customers.filter(c => {
    if (!customerSearch) return true;
    const search = customerSearch.toLowerCase();
    return c.customerName?.toLowerCase().includes(search) || c.customerCode?.toLowerCase().includes(search);
  });

  return (
    <div className="container mx-auto py-8 px-4 space-y-8 animate-in fade-in duration-500">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-6">
        <div>
          <h1 className="text-4xl font-extrabold tracking-tight flex items-center gap-3 text-slate-900">
            <FileText className="h-10 w-10 text-teal-600 animate-pulse" /> Quotes & Proposals
          </h1>
          <p className="text-slate-500 mt-2 text-base">Harden commercial documents with searchable customers, lines, default currencies, and live taxes.</p>
        </div>
        <Button className="bg-teal-600 hover:bg-teal-700 text-white font-semibold py-6 px-6 rounded-xl shadow-lg hover:shadow-teal-100 transition-all duration-300 transform hover:-translate-y-0.5" onClick={() => setShowCreate(true)}>
          <Plus className="h-5 w-5 mr-2" /> New Quote
        </Button>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
        <Card className="border-slate-100 shadow-sm hover:shadow-md transition-all rounded-2xl">
          <CardHeader className="pb-2"><CardDescription className="font-semibold text-slate-400 uppercase tracking-wider text-xs">Total Quotes</CardDescription></CardHeader>
          <CardContent><p className="text-3xl font-extrabold text-slate-800">{totalCount}</p></CardContent>
        </Card>
        <Card className="border-slate-100 shadow-sm hover:shadow-md transition-all rounded-2xl">
          <CardHeader className="pb-2"><CardDescription className="font-semibold text-slate-400 uppercase tracking-wider text-xs">Sent Proposals</CardDescription></CardHeader>
          <CardContent><p className="text-3xl font-extrabold text-sky-600">{quotes.filter(q => q.quoteStatus === 'Sent').length}</p></CardContent>
        </Card>
        <Card className="border-slate-100 shadow-sm hover:shadow-md transition-all rounded-2xl">
          <CardHeader className="pb-2"><CardDescription className="font-semibold text-slate-400 uppercase tracking-wider text-xs">Accepted Orders</CardDescription></CardHeader>
          <CardContent><p className="text-3xl font-extrabold text-emerald-600">{quotes.filter(q => q.quoteStatus === 'Accepted').length}</p></CardContent>
        </Card>
        <Card className="border-slate-100 shadow-sm hover:shadow-md transition-all rounded-2xl">
          <CardHeader className="pb-2"><CardDescription className="font-semibold text-slate-400 uppercase tracking-wider text-xs">Total Value (Base)</CardDescription></CardHeader>
          <CardContent><p className="text-3xl font-extrabold text-teal-600">₵{quotes.reduce((s, q) => s + (q.totalAmount * (q.exchangeRate || 1.0)), 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p></CardContent>
        </Card>
      </div>

      <Card className="border-slate-100 rounded-2xl shadow-sm">
        <CardHeader className="pb-4"><CardTitle className="text-lg font-bold text-slate-800">Filter Queue</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search quote # or customer..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleSearch()} className="rounded-xl border-slate-200" />
              <Button onClick={handleSearch} className="bg-slate-800 hover:bg-slate-900 rounded-xl"><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger className="rounded-xl border-slate-200"><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent className="rounded-xl">
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Sent">Sent</SelectItem>
                <SelectItem value="Accepted">Accepted</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Expired">Expired</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={loadQuotes} className="rounded-xl border-slate-200 hover:bg-slate-50"><RefreshCw className="h-4 w-4 mr-2" />Refresh Queue</Button>
          </div>
        </CardContent>
      </Card>

      <Card className="border-slate-100 rounded-2xl shadow-sm overflow-hidden">
        <CardContent className="p-0">
          {loading ? (
            <div className="text-center py-20"><FileText className="h-16 w-16 animate-bounce mx-auto mb-4 text-teal-600" /><p className="text-slate-500 font-medium">Fetching secure quotes...</p></div>
          ) : quotes.length === 0 ? (
            <div className="text-center py-20"><FileText className="h-16 w-16 mx-auto mb-4 text-slate-300" /><p className="text-slate-400 font-semibold text-lg">No quotes matching filters in tenant.</p></div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader className="bg-slate-50">
                  <TableRow>
                    <TableHead className="font-semibold text-slate-700 py-4 px-6">Quote #</TableHead>
                    <TableHead className="font-semibold text-slate-700 py-4">Quote Name</TableHead>
                    <TableHead className="font-semibold text-slate-700 py-4">Customer Name</TableHead>
                    <TableHead className="font-semibold text-slate-700 py-4 text-right">Value (Foreign)</TableHead>
                    <TableHead className="font-semibold text-slate-700 py-4 text-right">Value (GHS Base)</TableHead>
                    <TableHead className="font-semibold text-slate-700 py-4">Valid Until</TableHead>
                    <TableHead className="font-semibold text-slate-700 py-4">Status</TableHead>
                    <TableHead className="font-semibold text-slate-700 py-4 text-center px-6">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {quotes.map((q) => (
                    <TableRow key={q.id} className="hover:bg-slate-50/50 transition-colors border-b">
                      <TableCell className="font-mono text-sm text-teal-600 font-bold py-4 px-6">{q.documentNumber}</TableCell>
                      <TableCell className="font-semibold text-slate-800">{q.quoteName}</TableCell>
                      <TableCell className="text-slate-600 font-medium">{q.customerName || '-'}</TableCell>
                      <TableCell className="font-semibold text-slate-800 text-right">
                        {q.currency !== 'GHS' ? `${q.currency} ${q.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : '-'}
                      </TableCell>
                      <TableCell className="font-extrabold text-teal-600 text-right">
                        ₵{(q.totalAmount * (q.exchangeRate || 1.0)).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                      </TableCell>
                      <TableCell className="text-slate-500 font-medium">{formatDateString(q.validUntil)}</TableCell>
                      <TableCell><Badge className={cn("px-2.5 py-1 rounded-full text-xs font-bold border", STATUS_CONFIG[q.quoteStatus]?.className || '')}>{q.quoteStatus}</Badge></TableCell>
                      <TableCell className="py-4 px-6 text-center">
                        <div className="flex gap-2 justify-center">
                          {q.quoteStatus === 'Draft' && (
                            <Button variant="outline" size="sm" className="border-teal-200 text-teal-700 hover:bg-teal-50 hover:text-teal-800 font-bold" onClick={(e) => handleSend(q.id, e)}>
                              <Send className="h-3.5 w-3.5 mr-1" /> Send
                            </Button>
                          )}
                          {q.quoteStatus === 'Sent' && (
                            <>
                              <Button variant="outline" size="sm" className="border-emerald-200 text-emerald-700 hover:bg-emerald-50 hover:text-emerald-800 font-bold" onClick={(e) => handleAccept(q.id, e)}>
                                <CheckCircle className="h-3.5 w-3.5 mr-1" /> Accept
                              </Button>
                              <Button variant="outline" size="sm" className="border-rose-200 text-rose-700 hover:bg-rose-50 hover:text-rose-800 font-bold" onClick={(e) => handleReject(q.id, e)}>
                                <XCircle className="h-3.5 w-3.5 mr-1" /> Reject
                              </Button>
                            </>
                          )}
                          {q.quoteStatus === 'Accepted' && (
                            <Button size="sm" className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold" onClick={(e) => handleConvertToOrder(q.id, e)}>
                              <ArrowRight className="h-3.5 w-3.5 mr-1" /> Sales Order
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Create Quote Dialog */}
      <Dialog open={showCreate} onOpenChange={(open) => { setShowCreate(open); if(!open) { setForm(makeEmptyQuote()); setSelectedCustomer(null); } }}>
        <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto rounded-3xl border-slate-100 shadow-2xl p-8">
          <DialogHeader>
            <DialogTitle className="text-2xl font-black text-slate-800 flex items-center gap-2">
              <FileText className="h-6 w-6 text-teal-600" /> Assemble Sales Quotation
            </DialogTitle>
            <DialogDescription className="text-slate-500">Provide Business Partner context, terms, locked exchange rates, and line-level tax rules.</DialogDescription>
          </DialogHeader>

          <div className="grid gap-6 py-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-2">
                <Label className="font-bold text-slate-700">Quote Name / Subject *</Label>
                <Input value={form.quoteName} onChange={(e) => setForm(f => ({ ...f, quoteName: e.target.value }))} placeholder="E.g. Q1 Enterprise Software License" className="rounded-xl border-slate-200 py-5" />
              </div>

              <div className="space-y-2 flex flex-col">
                <Label className="font-bold text-slate-700">Customer (Searchable Debtors) *</Label>
                <Popover open={customerComboOpen} onOpenChange={setCustomerComboOpen}>
                  <PopoverTrigger asChild>
                    <Button variant="outline" role="combobox" aria-expanded={customerComboOpen} className="w-full justify-between rounded-xl border-slate-200 py-5 font-semibold text-slate-700 hover:bg-slate-50 text-left">
                      {selectedCustomer ? `${selectedCustomer.customerName} (${selectedCustomer.customerCode})` : "Select an active customer..."}
                      <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-[380px] p-0 rounded-2xl shadow-xl border-slate-100" align="start">
                    <Command shouldFilter={false}>
                      <CommandInput placeholder="Search active debtors by name..." value={customerSearch} onValueChange={setCustomerSearch} className="font-semibold text-sm" />
                      <CommandList>
                        <CommandEmpty className="py-6 text-center text-slate-500 text-sm font-medium">No debtors matching search.</CommandEmpty>
                        <CommandGroup className="p-2">
                          {filteredCustomers.map((customer) => (
                            <div key={customer.id} onClick={() => { onCustomerChange(customer.id); setCustomerComboOpen(false); setCustomerSearch(''); }} className="relative flex cursor-pointer select-none items-center rounded-xl px-3 py-2 text-sm text-slate-700 outline-none hover:bg-slate-50 font-semibold transition-colors">
                              <Check className={cn("mr-2 h-4 w-4 text-teal-600", selectedCustomer?.id === customer.id ? "opacity-100" : "opacity-0")} />
                              <div className="flex flex-col">
                                <span>{customer.customerName}</span>
                                <span className="text-xs text-slate-400 font-normal">{customer.customerCode} | default: {customer.currencyCode || 'GHS'}</span>
                              </div>
                            </div>
                          ))}
                        </CommandGroup>
                      </CommandList>
                    </Command>
                  </PopoverContent>
                </Popover>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-2">
                <Label className="font-bold text-slate-700">Valid Until *</Label>
                <Input type="date" value={form.validUntil} onChange={(e) => setForm(f => ({ ...f, validUntil: e.target.value }))} className="rounded-xl border-slate-200 py-5 font-bold" />
              </div>

              <div className="space-y-2">
                <Label className="font-bold text-slate-700">Default Tax Group (Header default for line inheritance)</Label>
                <Select value={form.taxGroupId || 'none'} onValueChange={(val) => setForm(f => ({ ...f, taxGroupId: val === 'none' ? '' : val }))}>
                  <SelectTrigger className="rounded-xl border-slate-200 py-5 font-bold text-slate-700">
                    <SelectValue placeholder="No Tax (Zero/Exempt)" />
                  </SelectTrigger>
                  <SelectContent className="rounded-xl font-semibold">
                    <SelectItem value="none">No Tax (Zero/Exempt)</SelectItem>
                    {taxGroups.map((tg) => (
                      <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6 bg-slate-50/50 p-5 rounded-2xl border border-slate-100">
              <div className="space-y-1">
                <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Transaction Currency</span>
                <p className="text-lg font-black text-slate-800 mt-1">{form.currency || 'GHS'}</p>
              </div>

              {form.currency !== 'GHS' && (
                <>
                  <div className="space-y-2 col-span-2">
                    <Label className="font-bold text-amber-600 flex items-center gap-1"><DollarSign className="h-4 w-4" /> Header-level Exchange Rate (locked if standard)</Label>
                    <div className="flex items-center gap-3">
                      <Input type="number" step="0.0001" min="0.0001" value={form.exchangeRate} onChange={(e) => setForm(f => ({ ...f, exchangeRate: Number(e.target.value) || 1.0 }))} className="rounded-xl border-slate-200 py-5 font-bold text-slate-700 bg-white" />
                      <span className="text-xs font-semibold text-slate-500 whitespace-nowrap">1 {form.currency} = {form.exchangeRate} GHS</span>
                    </div>
                  </div>
                </>
              )}
            </div>

            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Proposal Summary / Notes</Label>
              <Textarea rows={2} value={form.proposal || ''} onChange={(e) => setForm(f => ({ ...f, proposal: e.target.value }))} placeholder="Quote terms, validity conditions, or delivery details..." className="rounded-xl border-slate-200" />
            </div>

            <div className="space-y-4">
              <div className="flex items-center justify-between border-t pt-4">
                <Label className="text-lg font-black text-slate-800">Line Items Grid</Label>
                <Button variant="outline" size="sm" onClick={addLine} className="border-teal-200 text-teal-700 hover:bg-teal-50 rounded-xl font-bold">
                  <Plus className="h-4 w-4 mr-1" /> Add Quote Line
                </Button>
              </div>

              <div className="space-y-3">
                {form.lineItems.map((line, idx) => (
                  <div key={idx} className="grid grid-cols-12 gap-3 items-end border border-slate-100 rounded-2xl p-4 bg-slate-50/30 hover:bg-slate-50/50 transition-colors">
                    <div className="col-span-12 sm:col-span-4 space-y-1.5">
                      <Label className="text-xs font-bold text-slate-500">Description *</Label>
                      <Input value={line.description} onChange={(e) => updateLine(idx, 'description', e.target.value)} placeholder="Service, labor or item description" className="rounded-xl border-slate-200 bg-white" />
                    </div>

                    <div className="col-span-4 sm:col-span-2 space-y-1.5">
                      <Label className="text-xs font-bold text-slate-500">Qty</Label>
                      <Input type="number" min={1} value={line.quantity} onChange={(e) => updateLine(idx, 'quantity', Number(e.target.value) || 1)} className="rounded-xl border-slate-200 bg-white font-bold" />
                    </div>

                    <div className="col-span-4 sm:col-span-2 space-y-1.5">
                      <Label className="text-xs font-bold text-slate-500">Unit Price</Label>
                      <Input type="number" min={0} value={line.unitPrice} onChange={(e) => updateLine(idx, 'unitPrice', Number(e.target.value) || 0)} className="rounded-xl border-slate-200 bg-white font-bold" />
                    </div>

                    <div className="col-span-4 sm:col-span-2 space-y-1.5">
                      <Label className="text-xs font-bold text-amber-600 flex items-center gap-0.5"><Percent className="h-3 w-3" /> Disc Allowed %</Label>
                      <Input type="number" min={0} max={100} value={line.discountPercentage} onChange={(e) => updateLine(idx, 'discountPercentage', Number(e.target.value) || 0)} className="rounded-xl border-slate-200 bg-white font-bold" />
                    </div>

                    <div className="col-span-6 sm:col-span-2 space-y-1.5">
                      <Label className="text-xs font-bold text-slate-500">Tax Group</Label>
                      <Select value={line.taxGroupId || 'none'} onValueChange={(val) => updateLine(idx, 'taxGroupId', val === 'none' ? '' : val)}>
                        <SelectTrigger className="rounded-xl border-slate-200 bg-white font-semibold text-xs py-5">
                          <SelectValue placeholder="Inherit Header" />
                        </SelectTrigger>
                        <SelectContent className="rounded-xl font-semibold">
                          <SelectItem value="none">Inherit / None</SelectItem>
                          {taxGroups.map((tg) => (
                            <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="col-span-6 text-right sm:col-span-12 font-extrabold text-sm text-slate-700 py-3 pr-2">
                      Line Total: {form.currency} {((line.quantity * line.unitPrice) * (1 - (line.discountPercentage || 0)/100)).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                      {form.lineItems.length > 1 && (
                        <Button variant="ghost" size="sm" className="text-rose-500 hover:text-rose-600 hover:bg-rose-50 rounded-lg ml-3 p-1 h-8 w-8" onClick={() => removeLine(idx)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Premium glassmorphism totals summary card */}
            <div className="backdrop-blur-md bg-white/40 border border-slate-200/50 p-6 rounded-2xl shadow-xl space-y-4 relative overflow-hidden">
              <div className="absolute top-0 right-0 bg-gradient-to-bl from-teal-500/10 to-transparent w-36 h-36 rounded-full blur-2xl" />
              <CardDescription className="font-extrabold text-slate-600 uppercase tracking-wider text-xs flex items-center gap-2">Estimated Quotation Financial Summary</CardDescription>

              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm border-b pb-4">
                <div className="space-y-1">
                  <span className="text-slate-400 font-medium">Subtotal Gross:</span>
                  <p className="font-bold text-slate-700">{form.currency} {totals.subtotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
                <div className="space-y-1">
                  <span className="text-rose-500 font-medium">Discount Allowed:</span>
                  <p className="font-bold text-rose-600">-{form.currency} {totals.totalDiscount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
                <div className="space-y-1">
                  <span className="text-slate-400 font-medium">Estimated Taxes:</span>
                  <p className="font-bold text-slate-700">{form.currency} {totals.totalTaxAmount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
                <div className="space-y-1">
                  <span className="text-slate-400 font-medium">Exchange Rate:</span>
                  <p className="font-bold text-slate-700">{form.currency} 1.00 = ₵{form.exchangeRate}</p>
                </div>
              </div>

              {totals.taxList.length > 0 && (
                <div className="text-xs text-slate-500 space-y-1.5 bg-slate-50/50 p-3 rounded-xl border border-slate-100">
                  <span className="font-bold uppercase tracking-wider text-[10px] text-slate-400 block mb-1">Estimated Tax Breakdown:</span>
                  {totals.taxList.map((tax) => (
                    <div key={tax.code} className="flex justify-between font-semibold">
                      <span>{tax.name} ({tax.rate}%):</span>
                      <span>{form.currency} {tax.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                    </div>
                  ))}
                </div>
              )}

              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pt-2">
                <div className="space-y-0.5">
                  <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Grand Total (Foreign):</span>
                  <p className="text-2xl font-black text-slate-900">{form.currency} {totals.grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
                {form.currency !== 'GHS' && (
                  <div className="bg-gradient-to-r from-teal-50 to-emerald-50 border border-teal-100 p-4 rounded-xl space-y-0.5 text-right sm:min-w-[200px]">
                    <span className="text-[10px] font-black text-teal-800 uppercase tracking-wider">Equivalent Base Currency (GHS):</span>
                    <p className="text-xl font-extrabold text-teal-700">₵{totals.baseGrandTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                  </div>
                )}
              </div>
            </div>
          </div>

          <DialogFooter className="gap-2 sm:gap-0 mt-6 border-t pt-6">
            <Button variant="outline" onClick={() => { setShowCreate(false); setForm(makeEmptyQuote()); setSelectedCustomer(null); }} className="rounded-xl border-slate-200 font-bold py-6 px-6">Cancel</Button>
            <Button className="bg-teal-600 hover:bg-teal-700 text-white font-bold rounded-xl py-6 px-8 shadow-lg shadow-teal-100 transition-all duration-300" onClick={handleCreate} disabled={creating || !form.quoteName.trim() || !form.businessPartnerId}>
              {creating ? 'Assembling...' : 'Establish Quote'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
