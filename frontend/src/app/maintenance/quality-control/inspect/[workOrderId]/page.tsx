'use client';

import React, { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Progress } from '@/components/ui/progress';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { CheckCircle, XCircle, AlertTriangle, ArrowLeft, FileText } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

interface ChecklistItem {
  item: string;
  description: string;
  weight: number;
}

interface InspectionData {
  inspection: {
    id: string;
    workOrderId: string;
    checklistId: string;
    status: string;
    checkResults: any[];
  };
  workOrder: {
    id: string;
    workOrderNumber: string;
    title: string;
    assetName: string;
    assetLocation?: string;
    priority: string;
  };
  checklist: {
    id: string;
    name: string;
    minimumPassingScore: number;
    items: ChecklistItem[];
  };
}

interface ItemResult {
  itemId: string;
  result: string;
  notes?: string;
}

export default function InspectionExecutionPage() {
  const { toast } = useToast();
  const router = useRouter();
  const params = useParams();
  const workOrderId = params.workOrderId as string;

  const [loading, setLoading] = useState(true);
  const [inspectionData, setInspectionData] = useState<InspectionData | null>(null);
  const [itemResults, setItemResults] = useState<Map<string, ItemResult>>(new Map());
  const [currentItemIndex, setCurrentItemIndex] = useState(0);
  const [generalNotes, setGeneralNotes] = useState('');
  
  // Dialogs
  const [showCompleteDialog, setShowCompleteDialog] = useState(false);
  const [showRejectDialog, setShowRejectDialog] = useState(false);
  const [showCertificateDialog, setShowCertificateDialog] = useState(false);
  const [certificateBlobUrl, setCertificateBlobUrl] = useState<string | null>(null);
  const [completionResult, setCompletionResult] = useState<{ overallResult: string; score: number } | null>(null);
  const [rejectionReason, setRejectionReason] = useState('');
  const [correctiveActions, setCorrectiveActions] = useState('');
  const [severity, setSeverity] = useState('Medium');

  useEffect(() => {
    loadInspectionData();
  }, [workOrderId]);

  const getAuthHeaders = () => {
    const token = localStorage.getItem('authToken');
    return {
      'Authorization': token ? `Bearer ${token}` : '',
      'Content-Type': 'application/json'
    };
  };

  const loadInspectionData = async () => {
    try {
      setLoading(true);
      const response = await axios.get(`${API_URL}/maintenance/quality-control/inspection-by-workorder/${workOrderId}`, {
        headers: getAuthHeaders()
      });
      
      setInspectionData(response.data);
      
      // Initialize item results from existing check results
      const existingResults = new Map<string, ItemResult>();
      if (response.data.inspection.checkResults && response.data.inspection.checkResults.length > 0) {
        response.data.inspection.checkResults.forEach((result: any) => {
          existingResults.set(result.itemId, {
            itemId: result.itemId,
            result: result.result,
            notes: result.notes
          });
        });
      }
      setItemResults(existingResults);
    } catch (error) {
      console.error('Error loading inspection:', error);
      toast({
        title: "Error",
        description: "Failed to load inspection data",
        variant: "destructive"
      });
      router.back();
    } finally {
      setLoading(false);
    }
  };

  const handleItemResult = async (itemIndex: number, result: 'Pass' | 'Fail' | 'N/A', notes?: string) => {
    if (!inspectionData) return;

    const itemId = `item-${itemIndex}`;
    const newResult: ItemResult = { itemId, result, notes };
    
    try {
      // Update backend
      await axios.post(`${API_URL}/maintenance/quality-control/update-item-result`, {
        qualityCheckId: inspectionData.inspection.id,
        itemId,
        result,
        notes
      }, {
        headers: getAuthHeaders()
      });

      // Update local state
      const updatedResults = new Map(itemResults);
      updatedResults.set(itemId, newResult);
      setItemResults(updatedResults);

      toast({
        title: "Saved",
        description: `Item marked as ${result}`,
      });

      // Auto-advance to next item
      if (itemIndex < inspectionData.checklist.items.length - 1) {
        setCurrentItemIndex(itemIndex + 1);
      }
    } catch (error) {
      console.error('Error updating item result:', error);
      toast({
        title: "Error",
        description: "Failed to save result",
        variant: "destructive"
      });
    }
  };

  const handleComplete = async () => {
    if (!inspectionData) return;

    try {
      const response = await axios.post(`${API_URL}/maintenance/quality-control/complete-inspection`, {
        qualityCheckId: inspectionData.inspection.id,
        notes: generalNotes
      }, {
        headers: getAuthHeaders()
      });

      setCompletionResult({
        overallResult: response.data.overallResult,
        score: response.data.score
      });

      toast({
        title: "Inspection Completed",
        description: `Result: ${response.data.overallResult}, Score: ${response.data.score}%`,
      });

      setShowCompleteDialog(false);
      
      // If passed, show certificate preview
      if (response.data.overallResult === 'Pass') {
        await previewCertificate();
      } else {
        router.push('/maintenance/quality-control');
      }
    } catch (error) {
      console.error('Error completing inspection:', error);
      toast({
        title: "Error",
        description: "Failed to complete inspection",
        variant: "destructive"
      });
    }
  };

  const previewCertificate = async () => {
    if (!inspectionData) return;

    try {
      const response = await axios.get(
        `${API_URL}/maintenance/quality-control/certificate/${inspectionData.inspection.id}`,
        {
          headers: getAuthHeaders(),
          responseType: 'blob'
        }
      );

      const blobUrl = URL.createObjectURL(response.data);
      setCertificateBlobUrl(blobUrl);
      setShowCertificateDialog(true);
    } catch (error) {
      console.error('Error loading certificate:', error);
      toast({
        title: "Error",
        description: "Failed to load certificate preview",
        variant: "destructive"
      });
    }
  };

  const downloadCertificate = () => {
    if (!certificateBlobUrl || !inspectionData) return;

    const link = document.createElement('a');
    link.href = certificateBlobUrl;
    link.download = `QC-Certificate-${inspectionData.inspection.id}.pdf`;
    link.click();
  };

  const closeCertificatePreview = () => {
    if (certificateBlobUrl) {
      URL.revokeObjectURL(certificateBlobUrl);
      setCertificateBlobUrl(null);
    }
    setShowCertificateDialog(false);
    router.push('/maintenance/quality-control');
  };

  const handleReject = async () => {
    if (!inspectionData || !rejectionReason.trim()) {
      toast({
        title: "Validation Error",
        description: "Please provide rejection reason",
        variant: "destructive"
      });
      return;
    }

    try {
      await axios.post(`${API_URL}/maintenance/quality-control/reject-for-rework`, {
        qualityCheckId: inspectionData.inspection.id,
        rejectionReason,
        correctiveActions,
        severity
      }, {
        headers: getAuthHeaders()
      });

      toast({
        title: "Work Order Rejected",
        description: "Rework has been created",
      });

      router.push('/maintenance/quality-control');
    } catch (error) {
      console.error('Error rejecting inspection:', error);
      toast({
        title: "Error",
        description: "Failed to reject work order",
        variant: "destructive"
      });
    }
  };

  if (loading) {
    return <div className="p-6">Loading inspection...</div>;
  }

  if (!inspectionData) {
    return <div className="p-6">No inspection data found</div>;
  }

  const { workOrder, checklist, inspection } = inspectionData;
  const currentItem = checklist.items[currentItemIndex];
  const itemId = `item-${currentItemIndex}`;
  const currentResult = itemResults.get(itemId);
  const progress = (itemResults.size / checklist.items.length) * 100;
  const passedCount = Array.from(itemResults.values()).filter(r => r.result === 'Pass').length;
  const failedCount = Array.from(itemResults.values()).filter(r => r.result === 'Fail').length;

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Quality Inspection</h1>
          <p className="text-muted-foreground">{workOrder.workOrderNumber} - {workOrder.title}</p>
        </div>
        <Button variant="outline" onClick={() => router.back()}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back
        </Button>
      </div>

      {/* Work Order Info */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Work Order Information
          </CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-4 gap-4">
          <div>
            <p className="text-sm text-muted-foreground">Asset</p>
            <p className="font-medium">{workOrder.assetName}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Location</p>
            <p className="font-medium">{workOrder.assetLocation || 'N/A'}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Priority</p>
            <Badge>{workOrder.priority}</Badge>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Checklist</p>
            <p className="font-medium">{checklist.name}</p>
          </div>
        </CardContent>
      </Card>

      {/* Progress */}
      <Card>
        <CardContent className="pt-6">
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span>Progress: {itemResults.size} / {checklist.items.length} items</span>
              <span className="font-medium">{progress.toFixed(0)}%</span>
            </div>
            <Progress value={progress} />
            <div className="flex gap-4 text-sm">
              <span className="text-green-600">✓ Passed: {passedCount}</span>
              <span className="text-red-600">✗ Failed: {failedCount}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Current Item */}
      <Card>
        <CardHeader>
          <CardTitle>Item {currentItemIndex + 1} of {checklist.items.length}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <h3 className="font-semibold text-lg">{currentItem.item}</h3>
            <p className="text-muted-foreground">{currentItem.description}</p>
            <Badge variant="outline" className="mt-2">Weight: {currentItem.weight}</Badge>
          </div>

          {currentResult && (
            <div className="p-3 bg-muted rounded">
              <p className="text-sm font-medium">Current Result: {currentResult.result}</p>
              {currentResult.notes && <p className="text-sm text-muted-foreground">Notes: {currentResult.notes}</p>}
            </div>
          )}

          <div className="flex gap-2">
            <Button 
              onClick={() => handleItemResult(currentItemIndex, 'Pass')}
              className="flex-1 bg-green-600 hover:bg-green-700"
            >
              <CheckCircle className="mr-2 h-4 w-4" />
              Pass
            </Button>
            <Button 
              onClick={() => handleItemResult(currentItemIndex, 'Fail')}
              className="flex-1 bg-red-600 hover:bg-red-700"
            >
              <XCircle className="mr-2 h-4 w-4" />
              Fail
            </Button>
            <Button 
              onClick={() => handleItemResult(currentItemIndex, 'N/A')}
              variant="outline"
              className="flex-1"
            >
              N/A
            </Button>
          </div>

          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => setCurrentItemIndex(Math.max(0, currentItemIndex - 1))}
              disabled={currentItemIndex === 0}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              onClick={() => setCurrentItemIndex(Math.min(checklist.items.length - 1, currentItemIndex + 1))}
              disabled={currentItemIndex === checklist.items.length - 1}
              className="flex-1"
            >
              Next
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* General Notes */}
      <Card>
        <CardHeader>
          <CardTitle>General Notes</CardTitle>
        </CardHeader>
        <CardContent>
          <Textarea
            placeholder="Add general inspection notes..."
            value={generalNotes}
            onChange={(e) => setGeneralNotes(e.target.value)}
            rows={4}
          />
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex gap-4">
        <Button
          onClick={() => setShowCompleteDialog(true)}
          disabled={itemResults.size < checklist.items.length}
          className="flex-1"
        >
          Complete Inspection
        </Button>
        <Button
          onClick={() => setShowRejectDialog(true)}
          variant="destructive"
          className="flex-1"
        >
          <AlertTriangle className="mr-2 h-4 w-4" />
          Reject for Rework
        </Button>
      </div>

      {/* Complete Dialog */}
      <Dialog open={showCompleteDialog} onOpenChange={setShowCompleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete Inspection</DialogTitle>
            <DialogDescription>
              Are you sure you want to complete this inspection? This will calculate the final score and update the work order status.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCompleteDialog(false)}>Cancel</Button>
            <Button onClick={handleComplete}>Complete</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reject Dialog */}
      <Dialog open={showRejectDialog} onOpenChange={setShowRejectDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject for Rework</DialogTitle>
            <DialogDescription>
              Provide details about why this work order is being rejected.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div>
              <Label>Rejection Reason *</Label>
              <Textarea
                placeholder="Describe the issues found..."
                value={rejectionReason}
                onChange={(e) => setRejectionReason(e.target.value)}
                rows={3}
              />
            </div>
            <div>
              <Label>Corrective Actions</Label>
              <Textarea
                placeholder="What needs to be done..."
                value={correctiveActions}
                onChange={(e) => setCorrectiveActions(e.target.value)}
                rows={3}
              />
            </div>
            <div>
              <Label>Severity</Label>
              <Select value={severity} onValueChange={setSeverity}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Medium">Medium</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Critical">Critical</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowRejectDialog(false)}>Cancel</Button>
            <Button variant="destructive" onClick={handleReject}>Reject</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Certificate Preview Dialog */}
      <Dialog open={showCertificateDialog} onOpenChange={setShowCertificateDialog}>
        <DialogContent className="max-w-4xl h-[90vh]">
          <DialogHeader>
            <DialogTitle>Quality Inspection Certificate</DialogTitle>
            <DialogDescription>
              {completionResult && (
                <span className="font-semibold">
                  Result: {completionResult.overallResult} | Score: {completionResult.score}%
                </span>
              )}
            </DialogDescription>
          </DialogHeader>
          
          <div className="flex-1 overflow-hidden">
            {certificateBlobUrl && (
              <iframe
                src={certificateBlobUrl}
                className="w-full h-[calc(90vh-200px)] border rounded"
                title="Certificate Preview"
              />
            )}
          </div>

          <DialogFooter className="flex gap-2">
            <Button variant="outline" onClick={closeCertificatePreview}>
              Close
            </Button>
            <Button onClick={downloadCertificate}>
              <FileText className="mr-2 h-4 w-4" />
              Download Certificate
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
