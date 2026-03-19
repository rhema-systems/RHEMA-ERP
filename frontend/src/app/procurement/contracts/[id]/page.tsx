/* eslint-disable @typescript-eslint/no-non-null-assertion */
'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ArrowLeft, FileSignature, Calendar, DollarSign, Building, CheckCircle, Clock, FileText, Edit, Plus, Trash2, Play, XCircle, Loader2, CheckCircle2, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import { contractService, type ContractDto, type ContractMilestoneDto, type ContractAmendmentDto, type CreateContractMilestoneDto, type CreateContractAmendmentDto, type UpdateContractDto } from '@/services/contractService';
import { format } from 'date-fns';

export default function ContractDetailPage() {
  const params = useParams();
  const router = useRouter();
  const contractId = Array.isArray(params?.id) ? params.id[0] : params?.id ?? '';
  const [contract, setContract] = useState<ContractDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Edit contract dialog
  const [showEditDialog, setShowEditDialog] = useState(false);
  const [editData, setEditData] = useState<UpdateContractDto>({});

  // Milestone dialog
  const [showMilestoneDialog, setShowMilestoneDialog] = useState(false);
  const [milestoneData, setMilestoneData] = useState<CreateContractMilestoneDto>({
    milestoneName: '', sequenceNumber: 1, paymentPercentage: 0
  });

  // Amendment dialog
  const [showAmendmentDialog, setShowAmendmentDialog] = useState(false);
  const [amendmentData, setAmendmentData] = useState<CreateContractAmendmentDto>({
    amendmentType: 'ValueChange'
  });

  // Document dialog
  const [showDocumentDialog, setShowDocumentDialog] = useState(false);
  const [documentFile, setDocumentFile] = useState<File | null>(null);
  const [documentType, setDocumentType] = useState('Contract');
  const [documentDescription, setDocumentDescription] = useState('');

  // Status dialogs
  const [showActivateDialog, setShowActivateDialog] = useState(false);
  const [showTerminateDialog, setShowTerminateDialog] = useState(false);
  const [terminateReason, setTerminateReason] = useState('');
  const [activateData, setActivateData] = useState({ signedByName: '', contractorSignatoryName: '' });

  useEffect(() => {
    if (contractId) loadContract(contractId);
  }, [contractId]);

  const loadContract = async (id: string) => {
    try {
      setLoading(true);
      const data = await contractService.getContractById(id);
      setContract(data);
    } catch (error) {
      console.error('Error loading contract:', error);
      toast.error('Failed to load contract');
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try { return format(new Date(dateString), 'dd MMM yyyy'); } catch { return dateString; }
  };

  const formatCurrency = (amount: number, currency: string = 'USD') => {
    return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
      'PendingSignature': { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
      'Active': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Completed': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Terminated': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Suspended': { variant: 'secondary', className: 'bg-orange-100 text-orange-800' },
      'Pending': { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
      'Approved': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Invoiced': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Paid': { variant: 'default', className: 'bg-emerald-100 text-emerald-800' },
    };
    const c = config[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  // Edit contract
  const handleEditContract = () => {
    if (!contract) return;
    setEditData({
      contractTitle: contract.contractTitle,
      contractType: contract.contractType,
      contractValue: contract.contractValue,
      paymentTerms: contract.paymentTerms,
      retentionPercentage: contract.retentionPercentage,
      startDate: contract.startDate?.split('T')[0],
      endDate: contract.endDate?.split('T')[0],
      durationDays: contract.durationDays,
      warrantyPeriodDays: contract.warrantyPeriodDays,
      scopeOfWork: contract.scopeOfWork,
      deliverables: contract.deliverables,
      specialConditions: contract.specialConditions,
      penaltyClause: contract.penaltyClause,
      notes: contract.notes,
    });
    setShowEditDialog(true);
  };

  const handleSaveContract = async () => {
    if (!contract) return;
    try {
      setSaving(true);
      await contractService.updateContract(contract.id, editData);
      toast.success('Contract updated successfully');
      setShowEditDialog(false);
      loadContract(contract.id);
    } catch (error: any) {
      toast.error(error.message || 'Failed to update contract');
    } finally {
      setSaving(false);
    }
  };

  // Milestones
  const handleAddMilestone = async () => {
    if (!contract) return;
    if (!milestoneData.milestoneName.trim()) {
      toast.error('Milestone name is required');
      return;
    }
    try {
      setSaving(true);
      await contractService.addMilestone(contract.id, milestoneData);
      toast.success('Milestone added successfully');
      setShowMilestoneDialog(false);
      setMilestoneData({ milestoneName: '', sequenceNumber: contract.milestones.length + 1, paymentPercentage: 0 });
      loadContract(contract.id);
    } catch (error: any) {
      toast.error(error.message || 'Failed to add milestone');
    } finally {
      setSaving(false);
    }
  };

  const handleCompleteMilestone = async (milestone: ContractMilestoneDto) => {
    if (!contract) return;
    try {
      const contractId = contract.id;
      setSaving(true);
      await contractService.updateMilestoneStatus(milestone.id, { status: 'Completed', actualDate: new Date().toISOString() });
      toast.success('Milestone marked as completed');
      loadContract(contractId);
    } catch (error: any) {
      toast.error(error.message || 'Failed to complete milestone');
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteMilestone = async (milestoneId: string) => {
    if (!confirm('Are you sure you want to delete this milestone?')) return;
    if (!contract) return;
    try {
      const contractId = contract.id;
      setSaving(true);
      await contractService.deleteMilestone(milestoneId);
      toast.success('Milestone deleted');
      loadContract(contractId);
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete milestone');
    } finally {
      setSaving(false);
    }
  };

  // Amendments
  const handleCreateAmendment = async () => {
    if (!contract) return;
    try {
      setSaving(true);
      await contractService.createAmendment(contract.id, amendmentData);
      toast.success('Amendment created successfully');
      setShowAmendmentDialog(false);
      setAmendmentData({ amendmentType: 'ValueChange' });
      loadContract(contract.id);
    } catch (error: any) {
      toast.error(error.message || 'Failed to create amendment');
    } finally {
      setSaving(false);
    }
  };

  const handleProcessAmendment = async (amendmentId: string, approved: boolean) => {
    if (!contract) return;
    try {
      const contractId = contract.id;
      setSaving(true);
      await contractService.processAmendment(amendmentId, { approved });
      toast.success(approved ? 'Amendment approved' : 'Amendment rejected');
      loadContract(contractId);
    } catch (error: any) {
      toast.error(error.message || 'Failed to process amendment');
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteAmendment = async (amendmentId: string) => {
    if (!confirm('Are you sure you want to delete this amendment?')) return;
    if (!contract) return;
    try {
      const contractId = contract.id;
      setSaving(true);
      await contractService.deleteAmendment(amendmentId);
      toast.success('Amendment deleted');
      loadContract(contractId);
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete amendment');
    } finally {
      setSaving(false);
    }
  };

  // Contract status actions
  const handleActivateContract = async () => {
    if (!contract) return;
    try {
      setSaving(true);
      await contractService.activateContract(contract.id, activateData);
      toast.success('Contract activated successfully');
      setShowActivateDialog(false);
      loadContract(contract.id);
    } catch (error: any) {
      toast.error(error.message || 'Failed to activate contract');
    } finally {
      setSaving(false);
    }
  };

  const handleCompleteContract = async () => {
    if (!contract || !confirm('Are you sure you want to mark this contract as completed?')) return;
    try {
      setSaving(true);
      await contractService.completeContract(contract.id);
      toast.success('Contract completed successfully');
      loadContract(contract.id);
    } catch (error: any) {
      toast.error(error.message || 'Failed to complete contract');
    } finally {
      setSaving(false);
    }
  };

  const handleTerminateContract = async () => {
    if (!contract || !terminateReason.trim()) {
      toast.error('Please provide a reason for termination');
      return;
    }
    try {
      setSaving(true);
      await contractService.terminateContract(contract.id, terminateReason);
      toast.success('Contract terminated');
      setShowTerminateDialog(false);
      loadContract(contract.id);
    } catch (error: any) {
      toast.error(error.message || 'Failed to terminate contract');
    } finally {
      setSaving(false);
    }
  };

  // Document actions
  const handleUploadDocument = async () => {
    if (!contract || !documentFile) {
      toast.error('Please select a file to upload');
      return;
    }
    try {
      setSaving(true);
      await contractService.uploadDocument(contract.id, documentFile, documentType, documentDescription || undefined);
      toast.success('Document uploaded successfully');
      setShowDocumentDialog(false);
      setDocumentFile(null);
      setDocumentType('Contract');
      setDocumentDescription('');
      loadContract(contract.id);
    } catch (error: any) {
      toast.error(error.message || 'Failed to upload document');
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteDocument = async (documentId: string) => {
    if (!confirm('Are you sure you want to delete this document?')) return;
    if (!contract) return;
    try {
      const contractId = contract.id;
      setSaving(true);
      await contractService.deleteDocument(documentId);
      toast.success('Document deleted');
      loadContract(contractId);
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete document');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-12">
          <FileSignature className="h-12 w-12 animate-pulse mx-auto mb-4 text-blue-500" />
          <p className="text-gray-500">Loading contract...</p>
        </div>
      </div>
    );
  }

  if (!contract) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-12">
          <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
          <p className="text-gray-500">Contract not found</p>
          <Button variant="outline" className="mt-4" onClick={() => router.push('/procurement/contracts')}>
            <ArrowLeft className="h-4 w-4 mr-2" />Back to Contracts
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" onClick={() => router.push('/procurement/contracts')}>
            <ArrowLeft className="h-4 w-4 mr-2" />Back
          </Button>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <FileSignature className="h-6 w-6 text-blue-600" />
              {contract.contractNumber}
            </h1>
            <p className="text-gray-500">{contract.contractTitle}</p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {getStatusBadge(contract.status)}
          <Badge variant="outline">{contract.contractType}</Badge>

          {/* Action Buttons */}
          {(contract.status === 'Draft' || contract.status === 'Active') && (
            <Button variant="outline" size="sm" onClick={handleEditContract} disabled={saving}>
              <Edit className="h-4 w-4 mr-1" />Edit
            </Button>
          )}
          {contract.status === 'Draft' && (
            <Button size="sm" className="bg-green-600 hover:bg-green-700" onClick={() => setShowActivateDialog(true)} disabled={saving}>
              <Play className="h-4 w-4 mr-1" />Activate
            </Button>
          )}
          {contract.status === 'Active' && (
            <>
              <Button size="sm" className="bg-blue-600 hover:bg-blue-700" onClick={handleCompleteContract} disabled={saving}>
                <CheckCircle2 className="h-4 w-4 mr-1" />Complete
              </Button>
              <Button size="sm" variant="destructive" onClick={() => setShowTerminateDialog(true)} disabled={saving}>
                <XCircle className="h-4 w-4 mr-1" />Terminate
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm text-gray-500 flex items-center gap-2"><DollarSign className="h-4 w-4" />Contract Value</CardTitle></CardHeader>
          <CardContent><p className="text-xl font-bold text-green-600">{formatCurrency(contract.contractValue, contract.currency)}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm text-gray-500 flex items-center gap-2"><Building className="h-4 w-4" />Business Partner</CardTitle></CardHeader>
          <CardContent><p className="text-lg font-semibold">{contract.businessPartnerName}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm text-gray-500 flex items-center gap-2"><Calendar className="h-4 w-4" />Period</CardTitle></CardHeader>
          <CardContent><p className="text-sm">{formatDate(contract.startDate)} - {formatDate(contract.endDate)}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm text-gray-500 flex items-center gap-2"><CheckCircle className="h-4 w-4" />Milestones</CardTitle></CardHeader>
          <CardContent><p className="text-xl font-bold">{contract.completedMilestones} / {contract.totalMilestones}</p></CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="details" className="space-y-4">
        <TabsList>
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="milestones">Milestones ({contract.milestones.length})</TabsTrigger>
          <TabsTrigger value="amendments">Amendments ({contract.amendments.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({contract.documents.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="details">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <Card>
              <CardHeader><CardTitle>Contract Information</CardTitle></CardHeader>
              <CardContent className="space-y-3">
                <InfoRow label="Contract Number" value={contract.contractNumber} />
                <InfoRow label="Title" value={contract.contractTitle} />
                <InfoRow label="Type" value={contract.contractType} />
                <InfoRow label="Tender Number" value={contract.tenderNumber} />
                <InfoRow label="Created" value={formatDate(contract.createdAt)} />
                <InfoRow label="Created By" value={contract.createdByName || '-'} />
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle>Financial Details</CardTitle></CardHeader>
              <CardContent className="space-y-3">
                <InfoRow label="Contract Value" value={formatCurrency(contract.contractValue, contract.currency)} />
                <InfoRow label="Payment Terms" value={contract.paymentTerms || '-'} />
                <InfoRow label="Retention %" value={`${contract.retentionPercentage}%`} />
                <InfoRow label="Total Paid" value={formatCurrency(contract.totalPaidAmount, contract.currency)} />
                <InfoRow label="Remaining" value={formatCurrency(contract.remainingAmount, contract.currency)} />
              </CardContent>
            </Card>
            {contract.scopeOfWork && (
              <Card className="md:col-span-2">
                <CardHeader><CardTitle>Scope of Work</CardTitle></CardHeader>
                <CardContent><p className="whitespace-pre-wrap">{contract.scopeOfWork}</p></CardContent>
              </Card>
            )}
          </div>
        </TabsContent>

        <TabsContent value="milestones">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Contract Milestones</CardTitle>
              {(contract.status === 'Draft' || contract.status === 'Active') && (
                <Button size="sm" onClick={() => { setMilestoneData({ milestoneName: '', sequenceNumber: contract.milestones.length + 1, paymentPercentage: 0 }); setShowMilestoneDialog(true); }}>
                  <Plus className="h-4 w-4 mr-1" />Add Milestone
                </Button>
              )}
            </CardHeader>
            <CardContent>
              {contract.milestones.length === 0 ? (
                <p className="text-center py-4 text-gray-500">No milestones defined</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>#</TableHead>
                      <TableHead>Milestone</TableHead>
                      <TableHead>Planned Date</TableHead>
                      <TableHead>Payment %</TableHead>
                      <TableHead>Amount</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {contract.milestones.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell>{m.sequenceNumber}</TableCell>
                        <TableCell><p className="font-medium">{m.milestoneName}</p><p className="text-xs text-gray-500">{m.description}</p></TableCell>
                        <TableCell>{formatDate(m.plannedDate)}</TableCell>
                        <TableCell>{m.paymentPercentage}%</TableCell>
                        <TableCell>{formatCurrency(m.paymentAmount, contract.currency)}</TableCell>
                        <TableCell>{getStatusBadge(m.status)}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            {m.status === 'Pending' && contract.status === 'Active' && (
                              <Button size="sm" variant="outline" className="h-7 px-2" onClick={() => handleCompleteMilestone(m)} disabled={saving}>
                                <CheckCircle2 className="h-3 w-3" />
                              </Button>
                            )}
                            {(contract.status === 'Draft' || (contract.status === 'Active' && m.status === 'Pending')) && (
                              <Button size="sm" variant="ghost" className="h-7 px-2 text-red-600 hover:text-red-700" onClick={() => handleDeleteMilestone(m.id)} disabled={saving}>
                                <Trash2 className="h-3 w-3" />
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="amendments">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Contract Amendments</CardTitle>
              {(contract.status === 'Draft' || contract.status === 'Active') && (
                <Button size="sm" onClick={() => setShowAmendmentDialog(true)}>
                  <Plus className="h-4 w-4 mr-1" />Request Amendment
                </Button>
              )}
            </CardHeader>
            <CardContent>
              {contract.amendments.length === 0 ? (
                <p className="text-center py-4 text-gray-500">No amendments</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Amendment #</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Reason</TableHead>
                      <TableHead>Value Change</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {contract.amendments.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-mono">{a.amendmentNumber}</TableCell>
                        <TableCell>{a.amendmentType}</TableCell>
                        <TableCell>{a.reason || '-'}</TableCell>
                        <TableCell>{a.valueChange ? formatCurrency(a.valueChange, contract.currency) : '-'}</TableCell>
                        <TableCell>{getStatusBadge(a.status)}</TableCell>
                        <TableCell>{formatDate(a.requestedDate)}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            {a.status === 'Pending' && (
                              <>
                                <Button size="sm" variant="outline" className="h-7 px-2 text-green-600" onClick={() => handleProcessAmendment(a.id, true)} disabled={saving}>
                                  <CheckCircle2 className="h-3 w-3" />
                                </Button>
                                <Button size="sm" variant="outline" className="h-7 px-2 text-red-600" onClick={() => handleProcessAmendment(a.id, false)} disabled={saving}>
                                  <XCircle className="h-3 w-3" />
                                </Button>
                              </>
                            )}
                            {a.status === 'Pending' && (
                              <Button size="sm" variant="ghost" className="h-7 px-2 text-red-600 hover:text-red-700" onClick={() => handleDeleteAmendment(a.id)} disabled={saving}>
                                <Trash2 className="h-3 w-3" />
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="documents">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Contract Documents</CardTitle>
              {(contract.status === 'Draft' || contract.status === 'Active') && (
                <Button size="sm" onClick={() => setShowDocumentDialog(true)}>
                  <Plus className="h-4 w-4 mr-1" />Upload Document
                </Button>
              )}
            </CardHeader>
            <CardContent>
              {contract.documents.length === 0 ? (
                <p className="text-center py-4 text-gray-500">No documents uploaded</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Document</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Size</TableHead>
                      <TableHead>Uploaded</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {contract.documents.map((d) => (
                      <TableRow key={d.id}>
                        <TableCell className="font-medium">{d.fileName}</TableCell>
                        <TableCell>{d.documentType}</TableCell>
                        <TableCell>{d.fileSize ? `${(d.fileSize / 1024).toFixed(1)} KB` : '-'}</TableCell>
                        <TableCell>{formatDate(d.createdAt)}</TableCell>
                        <TableCell className="text-right">
                          <Button size="sm" variant="ghost" className="h-7 px-2 text-red-600 hover:text-red-700" onClick={() => handleDeleteDocument(d.id)} disabled={saving}>
                            <Trash2 className="h-3 w-3" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Edit Contract Dialog */}
      <Dialog open={showEditDialog} onOpenChange={setShowEditDialog}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Contract</DialogTitle>
            <DialogDescription>Update contract details</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-4 gap-4 py-4">
            <div className="col-span-3">
              <Label>Contract Title</Label>
              <Input value={editData.contractTitle || ''} onChange={(e) => setEditData({ ...editData, contractTitle: e.target.value })} />
            </div>
            <div>
              <Label>Contract Type</Label>
              <Select value={editData.contractType || ''} onValueChange={(v) => setEditData({ ...editData, contractType: v })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="FixedPrice">Fixed Price</SelectItem>
                  <SelectItem value="TimeAndMaterials">Time & Materials</SelectItem>
                  <SelectItem value="CostPlus">Cost Plus</SelectItem>
                  <SelectItem value="UnitPrice">Unit Price</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Contract Value ({contract?.currency || 'USD'})</Label>
              <Input type="number" step="0.01" value={editData.contractValue || ''} onChange={(e) => setEditData({ ...editData, contractValue: parseFloat(e.target.value) || 0 })} />
            </div>
            <div>
              <Label>Start Date</Label>
              <Input type="date" value={editData.startDate || ''} onChange={(e) => {
                const newStartDate = e.target.value;
                const duration = newStartDate && editData.endDate
                  ? Math.ceil((new Date(editData.endDate).getTime() - new Date(newStartDate).getTime()) / (1000 * 60 * 60 * 24))
                  : editData.durationDays;
                setEditData({ ...editData, startDate: newStartDate, durationDays: duration && duration > 0 ? duration : undefined });
              }} />
            </div>
            <div>
              <Label>End Date</Label>
              <Input type="date" value={editData.endDate || ''} onChange={(e) => {
                const newEndDate = e.target.value;
                const duration = editData.startDate && newEndDate
                  ? Math.ceil((new Date(newEndDate).getTime() - new Date(editData.startDate).getTime()) / (1000 * 60 * 60 * 24))
                  : editData.durationDays;
                setEditData({ ...editData, endDate: newEndDate, durationDays: duration && duration > 0 ? duration : undefined });
              }} />
            </div>
            <div>
              <Label>Duration (Days)</Label>
              <Input type="number" value={editData.durationDays || ''} readOnly className="bg-gray-100" />
            </div>
            <div>
              <Label>Warranty (Days)</Label>
              <Input type="number" value={editData.warrantyPeriodDays || ''} onChange={(e) => setEditData({ ...editData, warrantyPeriodDays: parseInt(e.target.value) || 0 })} />
            </div>
            <div>
              <Label>Retention %</Label>
              <Input type="number" step="0.01" min="0" max="100" value={editData.retentionPercentage || ''} onChange={(e) => setEditData({ ...editData, retentionPercentage: parseFloat(e.target.value) || 0 })} />
            </div>
            <div className="col-span-2">
              <Label>Payment Terms</Label>
              <Input value={editData.paymentTerms || ''} onChange={(e) => setEditData({ ...editData, paymentTerms: e.target.value })} placeholder="e.g., Net 30, Net 60" />
            </div>
            <div className="col-span-2">
              <Label>Scope of Work</Label>
              <Textarea rows={3} value={editData.scopeOfWork || ''} onChange={(e) => setEditData({ ...editData, scopeOfWork: e.target.value })} placeholder="Describe the scope of work..." />
            </div>
            <div className="col-span-2">
              <Label>Deliverables</Label>
              <Textarea rows={3} value={editData.deliverables || ''} onChange={(e) => setEditData({ ...editData, deliverables: e.target.value })} placeholder="List the deliverables..." />
            </div>
            <div className="col-span-2">
              <Label>Special Conditions</Label>
              <Textarea rows={2} value={editData.specialConditions || ''} onChange={(e) => setEditData({ ...editData, specialConditions: e.target.value })} placeholder="Any special terms..." />
            </div>
            <div className="col-span-2">
              <Label>Penalty Clause</Label>
              <Textarea rows={2} value={editData.penaltyClause || ''} onChange={(e) => setEditData({ ...editData, penaltyClause: e.target.value })} placeholder="Penalties for non-compliance..." />
            </div>
            <div className="col-span-4">
              <Label>Notes</Label>
              <Textarea rows={2} value={editData.notes || ''} onChange={(e) => setEditData({ ...editData, notes: e.target.value })} placeholder="Additional notes..." />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowEditDialog(false)}>Cancel</Button>
            <Button onClick={handleSaveContract} disabled={saving}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}Save Changes
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add Milestone Dialog */}
      <Dialog open={showMilestoneDialog} onOpenChange={setShowMilestoneDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Milestone</DialogTitle>
            <DialogDescription>Add a new milestone to the contract</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div>
              <Label>Milestone Name *</Label>
              <Input value={milestoneData.milestoneName} onChange={(e) => setMilestoneData({ ...milestoneData, milestoneName: e.target.value })} />
            </div>
            <div>
              <Label>Description</Label>
              <Textarea value={milestoneData.description || ''} onChange={(e) => setMilestoneData({ ...milestoneData, description: e.target.value })} />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Sequence #</Label>
                <Input type="number" value={milestoneData.sequenceNumber} onChange={(e) => setMilestoneData({ ...milestoneData, sequenceNumber: parseInt(e.target.value) || 1 })} />
              </div>
              <div>
                <Label>Payment %</Label>
                <Input type="number" value={milestoneData.paymentPercentage} onChange={(e) => setMilestoneData({ ...milestoneData, paymentPercentage: parseFloat(e.target.value) || 0 })} />
              </div>
            </div>
            <div>
              <Label>Planned Date</Label>
              <Input type="date" value={milestoneData.plannedDate || ''} onChange={(e) => setMilestoneData({ ...milestoneData, plannedDate: e.target.value })} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowMilestoneDialog(false)}>Cancel</Button>
            <Button onClick={handleAddMilestone} disabled={saving}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}Add Milestone
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create Amendment Dialog */}
      <Dialog open={showAmendmentDialog} onOpenChange={setShowAmendmentDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request Amendment</DialogTitle>
            <DialogDescription>Request a contract amendment</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div>
              <Label>Amendment Type</Label>
              <Select value={amendmentData.amendmentType} onValueChange={(v) => setAmendmentData({ ...amendmentData, amendmentType: v })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="ValueChange">Value Change</SelectItem>
                  <SelectItem value="ScopeChange">Scope Change</SelectItem>
                  <SelectItem value="TimeExtension">Time Extension</SelectItem>
                  <SelectItem value="TermsChange">Terms Change</SelectItem>
                  <SelectItem value="Other">Other</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Reason</Label>
              <Textarea value={amendmentData.reason || ''} onChange={(e) => setAmendmentData({ ...amendmentData, reason: e.target.value })} />
            </div>
            {amendmentData.amendmentType === 'ValueChange' && (
              <div>
                <Label>New Value</Label>
                <Input type="number" value={amendmentData.newValue || ''} onChange={(e) => setAmendmentData({ ...amendmentData, newValue: parseFloat(e.target.value) || 0 })} />
              </div>
            )}
            {amendmentData.amendmentType === 'TimeExtension' && (
              <div>
                <Label>New End Date</Label>
                <Input type="date" value={amendmentData.newEndDate || ''} onChange={(e) => setAmendmentData({ ...amendmentData, newEndDate: e.target.value })} />
              </div>
            )}
            {amendmentData.amendmentType === 'ScopeChange' && (
              <div>
                <Label>Scope Changes</Label>
                <Textarea value={amendmentData.scopeChanges || ''} onChange={(e) => setAmendmentData({ ...amendmentData, scopeChanges: e.target.value })} />
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowAmendmentDialog(false)}>Cancel</Button>
            <Button onClick={handleCreateAmendment} disabled={saving}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}Submit Amendment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Activate Contract Dialog */}
      <Dialog open={showActivateDialog} onOpenChange={setShowActivateDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Activate Contract</DialogTitle>
            <DialogDescription>Enter signatory information to activate the contract</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div>
              <Label>Signed By (Your Organization)</Label>
              <Input value={activateData.signedByName} onChange={(e) => setActivateData({ ...activateData, signedByName: e.target.value })} placeholder="Name of signatory" />
            </div>
            <div>
              <Label>Contractor Signatory</Label>
              <Input value={activateData.contractorSignatoryName} onChange={(e) => setActivateData({ ...activateData, contractorSignatoryName: e.target.value })} placeholder="Contractor representative name" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowActivateDialog(false)}>Cancel</Button>
            <Button className="bg-green-600 hover:bg-green-700" onClick={handleActivateContract} disabled={saving}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}Activate Contract
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Terminate Contract Dialog */}
      <Dialog open={showTerminateDialog} onOpenChange={setShowTerminateDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-red-600">
              <AlertCircle className="h-5 w-5" />Terminate Contract
            </DialogTitle>
            <DialogDescription>This action cannot be undone. Please provide a reason for termination.</DialogDescription>
          </DialogHeader>
          <div className="py-4">
            <Label>Reason for Termination *</Label>
            <Textarea rows={3} value={terminateReason} onChange={(e) => setTerminateReason(e.target.value)} placeholder="Explain why this contract is being terminated..." />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowTerminateDialog(false)}>Cancel</Button>
            <Button variant="destructive" onClick={handleTerminateContract} disabled={saving || !terminateReason.trim()}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}Terminate Contract
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Upload Document Dialog */}
      <Dialog open={showDocumentDialog} onOpenChange={setShowDocumentDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Upload Document</DialogTitle>
            <DialogDescription>Upload a document to this contract</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div>
              <Label>Document Type</Label>
              <Select value={documentType} onValueChange={setDocumentType}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Contract">Contract</SelectItem>
                  <SelectItem value="Amendment">Amendment</SelectItem>
                  <SelectItem value="Addendum">Addendum</SelectItem>
                  <SelectItem value="Specification">Specification</SelectItem>
                  <SelectItem value="Certificate">Certificate</SelectItem>
                  <SelectItem value="Invoice">Invoice</SelectItem>
                  <SelectItem value="Receipt">Receipt</SelectItem>
                  <SelectItem value="Correspondence">Correspondence</SelectItem>
                  <SelectItem value="Other">Other</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>File *</Label>
              <Input
                type="file"
                onChange={(e) => setDocumentFile(e.target.files?.[0] || null)}
                accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
              />
              <p className="text-xs text-gray-500 mt-1">Supported: PDF, Word, Excel, Images (max 20MB)</p>
            </div>
            <div>
              <Label>Description</Label>
              <Textarea
                value={documentDescription}
                onChange={(e) => setDocumentDescription(e.target.value)}
                placeholder="Optional description of the document..."
                rows={2}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowDocumentDialog(false); setDocumentFile(null); }}>Cancel</Button>
            <Button onClick={handleUploadDocument} disabled={saving || !documentFile}>
              {saving && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}Upload Document
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between">
      <span className="text-gray-500">{label}</span>
      <span className="font-medium">{value}</span>
    </div>
  );
}
