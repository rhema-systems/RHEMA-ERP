'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import {
  Swords, ArrowLeft, Plus, Trophy, XCircle, Clock, Globe, Building,
  Shield, Target, DollarSign, BookOpen, CheckCircle, AlertTriangle
} from 'lucide-react';
import { toast } from 'sonner';
import { competitorService, type CompetitorDetail, type CompetitorDeal, type CreateCompetitorDeal } from '@/services/competitorService';
import { format } from 'date-fns';

const THREAT_CONFIG: Record<string, { className: string; icon: React.ReactNode }> = {
  Low: { className: 'bg-green-100 text-green-800', icon: <Shield className="h-4 w-4" /> },
  Medium: { className: 'bg-yellow-100 text-yellow-800', icon: <AlertTriangle className="h-4 w-4" /> },
  High: { className: 'bg-orange-100 text-orange-800', icon: <AlertTriangle className="h-4 w-4" /> },
  Critical: { className: 'bg-red-100 text-red-800', icon: <Swords className="h-4 w-4" /> },
};

const OUTCOME_CONFIG: Record<string, { className: string }> = {
  InProgress: { className: 'bg-blue-100 text-blue-800' },
  Won: { className: 'bg-green-100 text-green-800' },
  Lost: { className: 'bg-red-100 text-red-800' },
};

const EMPTY_DEAL: CreateCompetitorDeal = {
  competitorId: '', threatLevel: 'Medium', dealValue: 0,
};

export default function CompetitorDetailPage() {
  const params = useParams();
  const router = useRouter();
  const competitorId = params.id as string;

  const [competitor, setCompetitor] = useState<CompetitorDetail | null>(null);
  const [loading, setLoading] = useState(true);

  // Track Deal dialog state
  const [showTrackDeal, setShowTrackDeal] = useState(false);
  const [tracking, setTracking] = useState(false);
  const [dealForm, setDealForm] = useState<CreateCompetitorDeal>({ ...EMPTY_DEAL, competitorId });

  // Record Outcome dialog state
  const [showOutcome, setShowOutcome] = useState(false);
  const [outcomeTarget, setOutcomeTarget] = useState<CompetitorDeal | null>(null);
  const [outcomeValue, setOutcomeValue] = useState('Won');
  const [lessonsLearned, setLessonsLearned] = useState('');
  const [recording, setRecording] = useState(false);

  useEffect(() => { loadCompetitor(); }, [competitorId]);

  const loadCompetitor = async () => {
    try {
      setLoading(true);
      const result = await competitorService.getCompetitorById(competitorId);
      setCompetitor(result.data);
    } catch (error) {
      console.error(error);
      toast.error('Failed to load competitor');
    } finally { setLoading(false); }
  };

  const handleTrackDeal = async () => {
    if (!dealForm.dealValue) { toast.error('Deal value is required'); return; }
    try {
      setTracking(true);
      await competitorService.trackDeal({ ...dealForm, competitorId });
      toast.success('Deal tracked successfully');
      setShowTrackDeal(false);
      setDealForm({ ...EMPTY_DEAL, competitorId });
      loadCompetitor();
    } catch (error: any) { toast.error(error.message || 'Failed to track deal'); }
    finally { setTracking(false); }
  };

  const openOutcomeDialog = (deal: CompetitorDeal) => {
    setOutcomeTarget(deal);
    setOutcomeValue('Won');
    setLessonsLearned('');
    setShowOutcome(true);
  };

  const handleRecordOutcome = async () => {
    if (!outcomeTarget) return;
    try {
      setRecording(true);
      await competitorService.updateDealOutcome(outcomeTarget.id, outcomeValue, lessonsLearned || undefined);
      toast.success(`Deal marked as ${outcomeValue}`);
      setShowOutcome(false);
      loadCompetitor();
    } catch (error: any) { toast.error(error.message || 'Failed to record outcome'); }
    finally { setRecording(false); }
  };

  const formatDate = (d?: string) => {
    if (!d) return '-';
    try { return format(new Date(d), 'dd MMM yyyy'); } catch { return d; }
  };

  if (loading) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-16">
          <Swords className="h-16 w-16 animate-pulse mx-auto mb-4 text-red-500" />
          <p className="text-gray-500 text-lg">Loading competitor profile...</p>
        </div>
      </div>
    );
  }

  if (!competitor) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-16">
          <Swords className="h-16 w-16 mx-auto mb-4 text-gray-400" />
          <p className="text-gray-500 text-lg">Competitor not found</p>
          <Button variant="outline" className="mt-4" onClick={() => router.push('/sales/competitors')}>
            <ArrowLeft className="h-4 w-4 mr-2" />Back to Competitors
          </Button>
        </div>
      </div>
    );
  }

  const inProgressDeals = competitor.deals.filter(d => d.outcome === 'InProgress');
  const resolvedDeals = competitor.deals.filter(d => d.outcome !== 'InProgress');
  const threatCfg = THREAT_CONFIG[competitor.threatLevel] || THREAT_CONFIG.Medium;

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" onClick={() => router.push('/sales/competitors')}>
            <ArrowLeft className="h-4 w-4 mr-2" />Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold flex items-center gap-2">
              <Swords className="h-8 w-8 text-red-600" />{competitor.name}
            </h1>
            <div className="flex items-center gap-3 mt-1">
              {competitor.industry && <span className="text-gray-500 flex items-center gap-1"><Building className="h-4 w-4" />{competitor.industry}</span>}
              {competitor.website && <a href={competitor.website} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:underline flex items-center gap-1"><Globe className="h-4 w-4" />{competitor.website}</a>}
              <Badge className={threatCfg.className}>{threatCfg.icon} {competitor.threatLevel} Threat</Badge>
            </div>
          </div>
        </div>
        <Button className="bg-blue-600 hover:bg-blue-700" onClick={() => setShowTrackDeal(true)}>
          <Plus className="h-4 w-4 mr-2" />Track Deal
        </Button>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Market Share</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{competitor.estimatedMarketShare}%</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Deals</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600"><Clock className="h-5 w-5 inline" /> {competitor.dealCount}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">We Won</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600"><Trophy className="h-5 w-5 inline" /> {competitor.wonDeals}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">We Lost</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-red-600"><XCircle className="h-5 w-5 inline" /> {competitor.lostDeals}</p></CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Win Rate</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-purple-600">
            {competitor.dealCount > 0 ? Math.round((competitor.wonDeals / (competitor.wonDeals + competitor.lostDeals || 1)) * 100) : 0}%
          </p></CardContent></Card>
      </div>

      {/* Intelligence Profile */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {(competitor.strengths || competitor.weaknesses) && (
          <Card>
            <CardHeader><CardTitle className="flex items-center gap-2"><Target className="h-5 w-5 text-amber-500" />SWOT Analysis</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {competitor.strengths && (
                <div><Label className="text-green-600 font-semibold">Strengths</Label>
                  <p className="text-sm mt-1 bg-green-50 p-3 rounded-lg">{competitor.strengths}</p></div>
              )}
              {competitor.weaknesses && (
                <div><Label className="text-red-600 font-semibold">Weaknesses</Label>
                  <p className="text-sm mt-1 bg-red-50 p-3 rounded-lg">{competitor.weaknesses}</p></div>
              )}
            </CardContent>
          </Card>
        )}

        {(competitor.keyProducts || competitor.pricingStrategy || competitor.description) && (
          <Card>
            <CardHeader><CardTitle className="flex items-center gap-2"><BookOpen className="h-5 w-5 text-blue-500" />Market Intelligence</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {competitor.keyProducts && (
                <div><Label className="font-semibold">Key Products</Label>
                  <p className="text-sm mt-1 bg-gray-50 p-3 rounded-lg">{competitor.keyProducts}</p></div>
              )}
              {competitor.pricingStrategy && (
                <div><Label className="font-semibold">Pricing Strategy</Label>
                  <p className="text-sm mt-1 bg-gray-50 p-3 rounded-lg">{competitor.pricingStrategy}</p></div>
              )}
              {competitor.description && (
                <div><Label className="font-semibold">Description</Label>
                  <p className="text-sm mt-1 text-gray-600">{competitor.description}</p></div>
              )}
            </CardContent>
          </Card>
        )}
      </div>

      {/* Active Deals (In Progress) */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><Clock className="h-5 w-5 text-blue-500" />Active Deals ({inProgressDeals.length})</CardTitle>
          <CardDescription>Deals where this competitor is currently involved</CardDescription>
        </CardHeader>
        <CardContent>
          {inProgressDeals.length === 0 ? (
            <div className="text-center py-6 text-gray-500">
              <Clock className="h-10 w-10 mx-auto mb-2 text-gray-300" />
              <p>No active competitive encounters. Use "Track Deal" to log one.</p>
            </div>
          ) : (
            <Table>
              <TableHeader><TableRow>
                <TableHead>Opportunity</TableHead><TableHead>Customer</TableHead><TableHead>Threat</TableHead>
                <TableHead>Deal Value</TableHead><TableHead>Their Proposal</TableHead><TableHead>Our Edge</TableHead>
                <TableHead>Reported</TableHead><TableHead>Actions</TableHead>
              </TableRow></TableHeader>
              <TableBody>
                {inProgressDeals.map((deal) => (
                  <TableRow key={deal.id}>
                    <TableCell className="font-medium">{deal.opportunityName || '-'}</TableCell>
                    <TableCell>{deal.customerName || '-'}</TableCell>
                    <TableCell><Badge className={THREAT_CONFIG[deal.threatLevel]?.className || ''}>{deal.threatLevel}</Badge></TableCell>
                    <TableCell className="font-semibold">${deal.dealValue.toLocaleString()}</TableCell>
                    <TableCell className="max-w-[200px] truncate text-sm">{deal.competitorProposal || '-'}</TableCell>
                    <TableCell className="max-w-[200px] truncate text-sm text-green-700">{deal.ourDifferentiator || '-'}</TableCell>
                    <TableCell className="text-sm">{formatDate(deal.reportedDate)}</TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        <Button variant="outline" size="sm" className="text-green-600" onClick={() => { openOutcomeDialog(deal); setOutcomeValue('Won'); }}>
                          <Trophy className="h-3 w-3 mr-1" />Won
                        </Button>
                        <Button variant="outline" size="sm" className="text-red-600" onClick={() => { openOutcomeDialog(deal); setOutcomeValue('Lost'); }}>
                          <XCircle className="h-3 w-3 mr-1" />Lost
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Resolved Deals (Won/Lost History) */}
      {resolvedDeals.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><BookOpen className="h-5 w-5 text-purple-500" />Deal History ({resolvedDeals.length})</CardTitle>
            <CardDescription>Past encounters and lessons learned</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader><TableRow>
                <TableHead>Outcome</TableHead><TableHead>Opportunity</TableHead><TableHead>Customer</TableHead>
                <TableHead>Deal Value</TableHead><TableHead>Their Proposal</TableHead><TableHead>Our Edge</TableHead>
                <TableHead>Lessons Learned</TableHead><TableHead>Date</TableHead>
              </TableRow></TableHeader>
              <TableBody>
                {resolvedDeals.map((deal) => (
                  <TableRow key={deal.id}>
                    <TableCell>
                      <Badge className={OUTCOME_CONFIG[deal.outcome]?.className || ''}>
                        {deal.outcome === 'Won' ? <Trophy className="h-3 w-3 mr-1 inline" /> : <XCircle className="h-3 w-3 mr-1 inline" />}
                        {deal.outcome}
                      </Badge>
                    </TableCell>
                    <TableCell className="font-medium">{deal.opportunityName || '-'}</TableCell>
                    <TableCell>{deal.customerName || '-'}</TableCell>
                    <TableCell className="font-semibold">${deal.dealValue.toLocaleString()}</TableCell>
                    <TableCell className="max-w-[150px] truncate text-sm">{deal.competitorProposal || '-'}</TableCell>
                    <TableCell className="max-w-[150px] truncate text-sm text-green-700">{deal.ourDifferentiator || '-'}</TableCell>
                    <TableCell className="max-w-[200px] text-sm italic text-amber-700">{deal.lessonsLearned || '-'}</TableCell>
                    <TableCell className="text-sm">{formatDate(deal.reportedDate)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Track Deal Dialog */}
      <Dialog open={showTrackDeal} onOpenChange={setShowTrackDeal}>
        <DialogContent className="sm:max-w-[550px]">
          <DialogHeader>
            <DialogTitle>Track Competitive Deal</DialogTitle>
            <DialogDescription>Log an encounter with {competitor.name} on a deal.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Opportunity Name</Label>
                <Input value={dealForm.opportunityName || ''} onChange={(e) => setDealForm(f => ({ ...f, opportunityName: e.target.value }))} placeholder="Enterprise Deal Q1" /></div>
              <div className="space-y-2"><Label>Customer Name</Label>
                <Input value={dealForm.customerName || ''} onChange={(e) => setDealForm(f => ({ ...f, customerName: e.target.value }))} placeholder="Acme Corp" /></div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2"><Label>Threat Level</Label>
                <Select value={dealForm.threatLevel} onValueChange={(v) => setDealForm(f => ({ ...f, threatLevel: v }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Low">🟢 Low</SelectItem><SelectItem value="Medium">🟡 Medium</SelectItem>
                    <SelectItem value="High">🟠 High</SelectItem><SelectItem value="Critical">🔴 Critical</SelectItem>
                  </SelectContent>
                </Select></div>
              <div className="space-y-2"><Label>Deal Value *</Label>
                <Input type="number" value={dealForm.dealValue || ''} onChange={(e) => setDealForm(f => ({ ...f, dealValue: Number(e.target.value) || 0 }))} placeholder="50000" /></div>
            </div>
            <div className="space-y-2"><Label>Their Proposal / Pitch</Label>
              <Textarea rows={2} value={dealForm.competitorProposal || ''} onChange={(e) => setDealForm(f => ({ ...f, competitorProposal: e.target.value }))} placeholder="What is the competitor offering? Price, terms, features..." /></div>
            <div className="space-y-2"><Label>Our Differentiator / Counter</Label>
              <Textarea rows={2} value={dealForm.ourDifferentiator || ''} onChange={(e) => setDealForm(f => ({ ...f, ourDifferentiator: e.target.value }))} placeholder="How do we win this? Our advantage, unique value prop..." /></div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowTrackDeal(false); setDealForm({ ...EMPTY_DEAL, competitorId }); }}>Cancel</Button>
            <Button className="bg-blue-600 hover:bg-blue-700" onClick={handleTrackDeal} disabled={tracking || !dealForm.dealValue}>
              {tracking ? 'Tracking...' : 'Track Deal'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Record Outcome Dialog */}
      <Dialog open={showOutcome} onOpenChange={setShowOutcome}>
        <DialogContent className="sm:max-w-[450px]">
          <DialogHeader>
            <DialogTitle>Record Deal Outcome</DialogTitle>
            <DialogDescription>
              {outcomeTarget && <>Record the result for "{outcomeTarget.opportunityName || 'this deal'}" (${outcomeTarget?.dealValue.toLocaleString()})</>}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2"><Label>Outcome</Label>
              <Select value={outcomeValue} onValueChange={setOutcomeValue}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Won">🏆 We Won — beat this competitor</SelectItem>
                  <SelectItem value="Lost">❌ We Lost — competitor won</SelectItem>
                </SelectContent>
              </Select></div>
            <div className="space-y-2"><Label>Lessons Learned</Label>
              <Textarea rows={4} value={lessonsLearned} onChange={(e) => setLessonsLearned(e.target.value)}
                placeholder={outcomeValue === 'Won'
                  ? "What worked? What messaging resonated? What sealed the deal?"
                  : "Why did we lose? What could we improve? What did the competitor do better?"
                } /></div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowOutcome(false)}>Cancel</Button>
            <Button
              className={outcomeValue === 'Won' ? 'bg-green-600 hover:bg-green-700' : 'bg-red-600 hover:bg-red-700'}
              onClick={handleRecordOutcome}
              disabled={recording}
            >
              {recording ? 'Recording...' : outcomeValue === 'Won' ? '🏆 Record Win' : '❌ Record Loss'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
