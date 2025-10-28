'use client';

import React, { useState, useEffect, useRef } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Progress } from '@/components/ui/progress';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Camera,
  Upload,
  FileText,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Clock,
  Play,
  Save,
  Send,
  ArrowLeft,
  ArrowRight,
  Image as ImageIcon,
  Trash2,
  Download,
  Signature,
  MapPin,
  User,
  Calendar,
  Star,
  Target,
  AlertCircle
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useToast } from '@/hooks/use-toast';

// Services
import { 
  InspectionExecution, 
  ChecklistItemResponse, 
  StartInspectionRequest, 
  CompleteInspectionRequest,
  inspectionExecutionService,
  WorkOrderInfo,
  InspectionSignature
} from '@/services/inspectionExecutionService';
import { QualityChecklist, qualityChecklistService } from '@/services/qualityChecklistService';
import { UploadedFile, fileUploadService } from '@/services/fileUploadService';

interface FileUploadProgress {
  [filename: string]: number;
}

export default function InspectionPage() {
  const { toast } = useToast();
  const params = useParams();
  const router = useRouter();
  const workOrderId = params.workOrderId as string;
  const fileInputRef = useRef<HTMLInputElement>(null);
  const signatureCanvasRef = useRef<HTMLCanvasElement>(null);

  // Main state
  const [workOrder, setWorkOrder] = useState<WorkOrderInfo | null>(null);
  const [inspection, setInspection] = useState<InspectionExecution | null>(null);
  const [availableChecklists, setAvailableChecklists] = useState<QualityChecklist[]>([]);
  const [selectedChecklistId, setSelectedChecklistId] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Inspection state
  const [currentItemIndex, setCurrentItemIndex] = useState(0);
  const [itemResponses, setItemResponses] = useState<ChecklistItemResponse[]>([]);
  const [generalNotes, setGeneralNotes] = useState('');
  const [overallScore, setOverallScore] = useState(0);
  const [overallResult, setOverallResult] = useState<'Pass' | 'Fail' | 'Conditional Pass'>('Pass');

  // File upload state
  const [uploadProgress, setUploadProgress] = useState<FileUploadProgress>({});
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFile[]>([]);

  // Dialog states
  const [showStartDialog, setShowStartDialog] = useState(false);
  const [showCompleteDialog, setShowCompleteDialog] = useState(false);
  const [showPhotoDialog, setShowPhotoDialog] = useState(false);
  const [showSignatureDialog, setShowSignatureDialog] = useState(false);

  // Signature state
  const [isDrawing, setIsDrawing] = useState(false);
  const [signatures, setSignatures] = useState<InspectionSignature[]>([]);

  useEffect(() => {
    loadWorkOrderAndChecklists();
  }, [workOrderId]);

  useEffect(() => {
    if (inspection?.checklist) {
      calculateScore();
    }
  }, [itemResponses, inspection]);

  const loadWorkOrderAndChecklists = async () => {
    try {
      setLoading(true);
      const workOrderData = await inspectionExecutionService.getWorkOrderById(workOrderId);
      
      if (!workOrderData) {
        toast({
          title: "Error",
          description: "Work order not found",
          variant: "destructive"
        });
        router.back();
        return;
      }

      setWorkOrder(workOrderData);

      // Check if there's already an active inspection for this work order
      const existingInspections = await inspectionExecutionService.getInspectionsByWorkOrder(workOrderId);
      const activeInspection = existingInspections.find(i => i.status === 'In Progress' || i.status === 'Scheduled');

      if (activeInspection) {
        setInspection(activeInspection);
        setItemResponses(activeInspection.itemResponses);
        setGeneralNotes(activeInspection.generalNotes || '');
        
        // Load uploaded files
        const files = await fileUploadService.getFilesByEntity('inspection', activeInspection.id);
        setUploadedFiles(files);
      } else {
        // Load available checklists
        const checklists = await qualityChecklistService.getChecklistsForWorkOrder(
          workOrderData.workOrderType,
          'Vehicle', // This should be dynamic based on asset category
          workOrderData.maintenanceType
        );
        setAvailableChecklists(checklists);
        
        if (checklists.length === 1) {
          setSelectedChecklistId(checklists[0].id);
        }
        
        setShowStartDialog(true);
      }
    } catch (error) {
      console.error('Error loading work order:', error);
      toast({
        title: "Error",
        description: "Error loading work order. Please try again.",
        variant: "destructive"
      });
    } finally {
      setLoading(false);
    }
  };

  const startInspection = async () => {
    if (!selectedChecklistId) {
      toast({
        title: "Validation Error",
        description: "Please select a checklist",
        variant: "destructive"
      });
      return;
    }

    try {
      const request: StartInspectionRequest = {
        workOrderId,
        checklistId: selectedChecklistId,
        inspectorId: 'CURRENT_USER_ID', // This should come from authentication
        scheduledDate: new Date().toISOString(),
        notes: ''
      };

      const newInspection = await inspectionExecutionService.startInspection(request);
      
      // Get the full checklist data
      const checklist = await qualityChecklistService.getChecklistById(selectedChecklistId);
      if (checklist) {
        newInspection.checklist = checklist;
      }

      setInspection(newInspection);
      setShowStartDialog(false);

      // Initialize empty responses
      if (checklist) {
        const initialResponses: ChecklistItemResponse[] = checklist.items.map(item => ({
          itemId: item.id,
          result: item.responseType === 'Score' ? 'Score' : 'Pass',
          score: item.responseType === 'Score' ? item.minScore : undefined,
          comments: '',
          inspectedAt: new Date().toISOString(),
          inspectedBy: 'CURRENT_USER_ID'
        }));
        setItemResponses(initialResponses);
      }
    } catch (error) {
      console.error('Error starting inspection:', error);
      toast({
        title: "Error",
        description: "Error starting inspection. Please try again.",
        variant: "destructive"
      });
    }
  };

  const updateItemResponse = async (itemId: string, updates: Partial<ChecklistItemResponse>) => {
    const updatedResponses = itemResponses.map(response =>
      response.itemId === itemId 
        ? { ...response, ...updates, inspectedAt: new Date().toISOString() }
        : response
    );
    
    setItemResponses(updatedResponses);

    // Auto-save progress
    if (inspection) {
      try {
        await inspectionExecutionService.updateInspectionProgress(inspection.id, updatedResponses);
      } catch (error) {
        console.error('Error saving progress:', error);
      }
    }
  };

  const calculateScore = () => {
    if (!inspection?.checklist) return;

    const score = inspectionExecutionService.calculateInspectionScore(inspection.checklist, itemResponses);
    setOverallScore(score);

    const hasFailedCritical = inspectionExecutionService.hasFailedCriticalItems(inspection.checklist, itemResponses);
    const result = inspectionExecutionService.determineOverallResult(
      score, 
      inspection.checklist.minimumPassingScore, 
      hasFailedCritical
    );
    setOverallResult(result);
  };

  const handleFileUpload = async (files: FileList) => {
    if (!inspection) return;

    const fileArray = Array.from(files);
    
    try {
      const uploadedFiles = await fileUploadService.uploadMultipleFiles(
        fileArray,
        'inspection',
        inspection.id,
        (filename, progress) => {
          setUploadProgress(prev => ({
            ...prev,
            [filename]: progress
          }));
        }
      );

      setUploadedFiles(prev => [...prev, ...uploadedFiles]);
      setUploadProgress({});
    } catch (error) {
      console.error('Error uploading files:', error);
      toast({
        title: "Upload Error",
        description: "Error uploading files. Please try again.",
        variant: "destructive"
      });
    }
  };

  const removeFile = async (fileId: string) => {
    try {
      await fileUploadService.deleteFile(fileId);
      setUploadedFiles(prev => prev.filter(f => f.id !== fileId));
    } catch (error) {
      console.error('Error removing file:', error);
    }
  };

  const startDrawing = (e: React.MouseEvent<HTMLCanvasElement>) => {
    setIsDrawing(true);
    const canvas = signatureCanvasRef.current;
    if (canvas) {
      const rect = canvas.getBoundingClientRect();
      const ctx = canvas.getContext('2d');
      if (ctx) {
        ctx.beginPath();
        ctx.moveTo(e.clientX - rect.left, e.clientY - rect.top);
      }
    }
  };

  const draw = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!isDrawing) return;
    
    const canvas = signatureCanvasRef.current;
    if (canvas) {
      const rect = canvas.getBoundingClientRect();
      const ctx = canvas.getContext('2d');
      if (ctx) {
        ctx.lineTo(e.clientX - rect.left, e.clientY - rect.top);
        ctx.stroke();
      }
    }
  };

  const stopDrawing = () => {
    setIsDrawing(false);
  };

  const clearSignature = () => {
    const canvas = signatureCanvasRef.current;
    if (canvas) {
      const ctx = canvas.getContext('2d');
      if (ctx) {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
      }
    }
  };

  const saveSignature = () => {
    const canvas = signatureCanvasRef.current;
    if (canvas) {
      const dataUrl = canvas.toDataURL();
      const newSignature: InspectionSignature = {
        type: 'Inspector',
        signedBy: 'Current Inspector', // Should come from auth
        signedAt: new Date().toISOString(),
        signatureImage: dataUrl,
        required: true
      };
      
      setSignatures([...signatures, newSignature]);
      setShowSignatureDialog(false);
      clearSignature();
    }
  };

  const completeInspection = async (workflowAction: 'approve' | 'reject' | 'require_rework') => {
    if (!inspection) return;

    try {
      setSaving(true);
      
      const request: CompleteInspectionRequest = {
        inspectionId: inspection.id,
        overallResult,
        overallScore,
        itemResponses,
        generalNotes,
        signatures,
        workflowAction
      };

      await inspectionExecutionService.completeInspection(request);
      
      setShowCompleteDialog(false);
      
      // Navigate back to quality control dashboard
      router.push('/maintenance/quality-control');
    } catch (error) {
      console.error('Error completing inspection:', error);
      toast({
        title: "Error",
        description: "Error completing inspection. Please try again.",
        variant: "destructive"
      });
    } finally {
      setSaving(false);
    }
  };

  const getCurrentItem = () => {
    if (!inspection?.checklist.items || currentItemIndex >= inspection.checklist.items.length) {
      return null;
    }
    return inspection.checklist.items[currentItemIndex];
  };

  const getCurrentResponse = () => {
    const currentItem = getCurrentItem();
    if (!currentItem) return null;
    
    return itemResponses.find(r => r.itemId === currentItem.id);
  };

  const getItemStatusIcon = (itemId: string) => {
    const response = itemResponses.find(r => r.itemId === itemId);
    if (!response) return <Clock className="h-4 w-4 text-gray-400" />;
    
    if (response.result === 'Pass') return <CheckCircle className="h-4 w-4 text-green-500" />;
    if (response.result === 'Fail') return <XCircle className="h-4 w-4 text-red-500" />;
    if (response.result === 'Score') return <Star className="h-4 w-4 text-blue-500" />;
    
    return <AlertTriangle className="h-4 w-4 text-yellow-500" />;
  };

  const completedItems = itemResponses.filter(r => r.result !== 'N/A').length;
  const totalItems = inspection?.checklist.items.length || 0;
  const progressPercent = totalItems > 0 ? (completedItems / totalItems) * 100 : 0;

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-900 mx-auto mb-4"></div>
          <p>Loading inspection...</p>
        </div>
      </div>
    );
  }

  if (!workOrder) {
    return (
      <div className="text-center py-8">
        <p className="text-red-600">Work order not found.</p>
        <Button onClick={() => router.back()} className="mt-4">
          <ArrowLeft className="mr-2 h-4 w-4" />
          Go Back
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Quality Inspection</h1>
          <p className="text-muted-foreground">
            {workOrder.workOrderNumber} - {workOrder.title}
          </p>
        </div>
        
        <div className="flex items-center space-x-2">
          <Button variant="outline" onClick={() => router.back()}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back to Quality Control
          </Button>
          
          {inspection && (
            <Button onClick={() => setShowCompleteDialog(true)} disabled={completedItems < totalItems}>
              <Send className="mr-2 h-4 w-4" />
              Complete Inspection
            </Button>
          )}
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/maintenance/quality-control">Quality Control</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Inspection</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Work Order Info */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center space-x-2">
            <FileText className="h-5 w-5" />
            <span>Work Order Information</span>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="flex items-center space-x-2">
              <MapPin className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="text-sm font-medium">Asset</p>
                <p className="text-sm text-muted-foreground">{workOrder.assetName}</p>
              </div>
            </div>
            <div className="flex items-center space-x-2">
              <User className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="text-sm font-medium">Technician</p>
                <p className="text-sm text-muted-foreground">{workOrder.assignedTechnicianName}</p>
              </div>
            </div>
            <div className="flex items-center space-x-2">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="text-sm font-medium">Completed</p>
                <p className="text-sm text-muted-foreground">
                  {workOrder.completedDate ? new Date(workOrder.completedDate).toLocaleDateString() : 'N/A'}
                </p>
              </div>
            </div>
            <div className="flex items-center space-x-2">
              <Target className="h-4 w-4 text-muted-foreground" />
              <div>
                <p className="text-sm font-medium">Priority</p>
                <Badge variant="outline">{workOrder.priority}</Badge>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {inspection ? (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main Inspection Area */}
          <div className="lg:col-span-2 space-y-6">
            {/* Progress */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center justify-between">
                  <span>Inspection Progress</span>
                  <span className="text-sm font-normal">
                    {completedItems} of {totalItems} items completed
                  </span>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <Progress value={progressPercent} className="w-full" />
                <div className="flex items-center justify-between mt-2 text-sm text-muted-foreground">
                  <span>{Math.round(progressPercent)}% Complete</span>
                  <div className="flex items-center space-x-2">
                    <span>Score: {overallScore}%</span>
                    <Badge 
                      className={cn(
                        overallResult === 'Pass' ? 'bg-green-100 text-green-800' : 
                        overallResult === 'Fail' ? 'bg-red-100 text-red-800' : 
                        'bg-yellow-100 text-yellow-800'
                      )}
                    >
                      {overallResult}
                    </Badge>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Current Item */}
            {getCurrentItem() && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center justify-between">
                    <span>
                      Item {currentItemIndex + 1} of {totalItems}
                    </span>
                    <div className="flex items-center space-x-2">
                      <Button 
                        size="sm" 
                        variant="outline" 
                        onClick={() => setCurrentItemIndex(Math.max(0, currentItemIndex - 1))}
                        disabled={currentItemIndex === 0}
                      >
                        <ArrowLeft className="h-4 w-4" />
                      </Button>
                      <Button 
                        size="sm" 
                        variant="outline" 
                        onClick={() => setCurrentItemIndex(Math.min(totalItems - 1, currentItemIndex + 1))}
                        disabled={currentItemIndex === totalItems - 1}
                      >
                        <ArrowRight className="h-4 w-4" />
                      </Button>
                    </div>
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div>
                    <div className="flex items-center space-x-2 mb-2">
                      <h3 className="font-semibold">{getCurrentItem()?.text}</h3>
                      {getCurrentItem()?.required && <Badge variant="outline">Required</Badge>}
                      {getCurrentItem()?.critical && <Badge variant="destructive">Critical</Badge>}
                    </div>
                    {getCurrentItem()?.description && (
                      <p className="text-sm text-muted-foreground mb-4">{getCurrentItem()?.description}</p>
                    )}
                  </div>

                  {/* Response Input */}
                  <div className="space-y-4">
                    {getCurrentItem()?.responseType === 'Pass/Fail' && (
                      <div className="space-y-2">
                        <Label>Result</Label>
                        <div className="flex space-x-2">
                          <Button
                            variant={getCurrentResponse()?.result === 'Pass' ? 'default' : 'outline'}
                            onClick={() => updateItemResponse(getCurrentItem()!.id, { result: 'Pass' })}
                            className="flex-1"
                          >
                            <CheckCircle className="mr-2 h-4 w-4" />
                            Pass
                          </Button>
                          <Button
                            variant={getCurrentResponse()?.result === 'Fail' ? 'destructive' : 'outline'}
                            onClick={() => updateItemResponse(getCurrentItem()!.id, { result: 'Fail' })}
                            className="flex-1"
                          >
                            <XCircle className="mr-2 h-4 w-4" />
                            Fail
                          </Button>
                        </div>
                      </div>
                    )}

                    {getCurrentItem()?.responseType === 'Score' && (
                      <div className="space-y-2">
                        <Label>
                          Score ({getCurrentItem()?.minScore} - {getCurrentItem()?.maxScore})
                        </Label>
                        <Input
                          type="number"
                          min={getCurrentItem()?.minScore}
                          max={getCurrentItem()?.maxScore}
                          value={getCurrentResponse()?.score || getCurrentItem()?.minScore}
                          onChange={(e) => updateItemResponse(
                            getCurrentItem()!.id, 
                            { score: parseInt(e.target.value) || getCurrentItem()!.minScore, result: 'Score' }
                          )}
                        />
                      </div>
                    )}

                    <div className="space-y-2">
                      <Label>Comments</Label>
                      <Textarea
                        value={getCurrentResponse()?.comments || ''}
                        onChange={(e) => updateItemResponse(getCurrentItem()!.id, { comments: e.target.value })}
                        placeholder="Add comments about this inspection item..."
                        rows={3}
                      />
                    </div>

                    {/* Photo Upload */}
                    <div className="space-y-2">
                      <Label>Photos & Evidence</Label>
                      <div className="flex items-center space-x-2">
                        <Button
                          variant="outline"
                          onClick={() => setShowPhotoDialog(true)}
                          className="flex-1"
                        >
                          <Camera className="mr-2 h-4 w-4" />
                          Take Photo
                        </Button>
                        <Button
                          variant="outline"
                          onClick={() => fileInputRef.current?.click()}
                          className="flex-1"
                        >
                          <Upload className="mr-2 h-4 w-4" />
                          Upload Files
                        </Button>
                      </div>
                      <input
                        ref={fileInputRef}
                        type="file"
                        multiple
                        accept="image/*,.pdf"
                        className="hidden"
                        onChange={(e) => e.target.files && handleFileUpload(e.target.files)}
                      />
                      
                      {/* Upload Progress */}
                      {Object.keys(uploadProgress).length > 0 && (
                        <div className="space-y-2">
                          {Object.entries(uploadProgress).map(([filename, progress]) => (
                            <div key={filename} className="space-y-1">
                              <div className="flex items-center justify-between text-sm">
                                <span>{filename}</span>
                                <span>{progress}%</span>
                              </div>
                              <Progress value={progress} className="h-1" />
                            </div>
                          ))}
                        </div>
                      )}

                      {/* Uploaded Files */}
                      {uploadedFiles.length > 0 && (
                        <div className="grid grid-cols-2 gap-2 mt-2">
                          {uploadedFiles.map((file) => (
                            <div key={file.id} className="border rounded-lg p-2">
                              <div className="flex items-center justify-between">
                                <div className="flex items-center space-x-2">
                                  <ImageIcon className="h-4 w-4" />
                                  <span className="text-sm truncate">{file.originalName}</span>
                                </div>
                                <Button
                                  size="sm"
                                  variant="ghost"
                                  onClick={() => removeFile(file.id)}
                                  className="text-red-600"
                                >
                                  <Trash2 className="h-3 w-3" />
                                </Button>
                              </div>
                              {file.thumbnailUrl && (
                                <img
                                  src={file.thumbnailUrl}
                                  alt={file.originalName}
                                  className="w-full h-20 object-cover rounded mt-2"
                                />
                              )}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* General Notes */}
            <Card>
              <CardHeader>
                <CardTitle>General Notes</CardTitle>
              </CardHeader>
              <CardContent>
                <Textarea
                  value={generalNotes}
                  onChange={(e) => setGeneralNotes(e.target.value)}
                  placeholder="Add general notes about the overall inspection..."
                  rows={4}
                />
              </CardContent>
            </Card>
          </div>

          {/* Sidebar */}
          <div className="space-y-6">
            {/* Checklist Overview */}
            <Card>
              <CardHeader>
                <CardTitle>{inspection.checklist.name}</CardTitle>
                <CardDescription>
                  {inspection.checklist.description}
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span>Minimum Pass Score:</span>
                    <span>{inspection.checklist.minimumPassingScore}%</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span>Current Score:</span>
                    <span className="font-semibold">{overallScore}%</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span>Status:</span>
                    <Badge 
                      className={cn(
                        overallResult === 'Pass' ? 'bg-green-100 text-green-800' : 
                        overallResult === 'Fail' ? 'bg-red-100 text-red-800' : 
                        'bg-yellow-100 text-yellow-800'
                      )}
                    >
                      {overallResult}
                    </Badge>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Items List */}
            <Card>
              <CardHeader>
                <CardTitle>Checklist Items</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="space-y-2 max-h-64 overflow-y-auto">
                  {inspection.checklist.items.map((item, index) => (
                    <div
                      key={item.id}
                      className={cn(
                        "flex items-center space-x-3 p-2 rounded-lg cursor-pointer hover:bg-muted",
                        currentItemIndex === index && "bg-blue-50 border border-blue-200"
                      )}
                      onClick={() => setCurrentItemIndex(index)}
                    >
                      {getItemStatusIcon(item.id)}
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium truncate">{item.text}</p>
                        <p className="text-xs text-muted-foreground">{item.category}</p>
                      </div>
                      {item.critical && (
                        <AlertCircle className="h-4 w-4 text-red-500" />
                      )}
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>

            {/* Quick Actions */}
            <Card>
              <CardHeader>
                <CardTitle>Quick Actions</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                <Button
                  variant="outline"
                  onClick={() => setShowPhotoDialog(true)}
                  className="w-full"
                >
                  <Camera className="mr-2 h-4 w-4" />
                  Take Photo
                </Button>
                <Button
                  variant="outline"
                  onClick={() => setShowSignatureDialog(true)}
                  className="w-full"
                >
                  <Signature className="mr-2 h-4 w-4" />
                  Add Signature
                </Button>
                <Button
                  variant="outline"
                  onClick={() => {/* Save progress */}}
                  className="w-full"
                >
                  <Save className="mr-2 h-4 w-4" />
                  Save Progress
                </Button>
              </CardContent>
            </Card>
          </div>
        </div>
      ) : (
        <div className="text-center py-8">
          <p className="text-muted-foreground">No active inspection found for this work order.</p>
        </div>
      )}

      {/* Start Inspection Dialog */}
      <Dialog open={showStartDialog} onOpenChange={setShowStartDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Start Quality Inspection</DialogTitle>
            <DialogDescription>
              Select a checklist to begin the quality inspection for this work order.
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="checklist">Available Checklists</Label>
              <Select value={selectedChecklistId} onValueChange={setSelectedChecklistId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select a checklist" />
                </SelectTrigger>
                <SelectContent>
                  {availableChecklists.map((checklist) => (
                    <SelectItem key={checklist.id} value={checklist.id}>
                      {checklist.name} ({checklist.items.length} items)
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            
            {selectedChecklistId && (
              <div className="bg-muted p-3 rounded-lg">
                <p className="text-sm font-medium">
                  {availableChecklists.find(c => c.id === selectedChecklistId)?.description}
                </p>
                <p className="text-xs text-muted-foreground mt-1">
                  Minimum passing score: {availableChecklists.find(c => c.id === selectedChecklistId)?.minimumPassingScore}%
                </p>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => router.back()}>
              Cancel
            </Button>
            <Button onClick={startInspection} disabled={!selectedChecklistId}>
              <Play className="mr-2 h-4 w-4" />
              Start Inspection
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Complete Inspection Dialog */}
      <Dialog open={showCompleteDialog} onOpenChange={setShowCompleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete Inspection</DialogTitle>
            <DialogDescription>
              Review the inspection results and select the appropriate action.
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4">
            <div className="bg-muted p-4 rounded-lg">
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="font-medium">Overall Score:</span> {overallScore}%
                </div>
                <div>
                  <span className="font-medium">Result:</span>{' '}
                  <Badge 
                    className={cn(
                      overallResult === 'Pass' ? 'bg-green-100 text-green-800' : 
                      overallResult === 'Fail' ? 'bg-red-100 text-red-800' : 
                      'bg-yellow-100 text-yellow-800'
                    )}
                  >
                    {overallResult}
                  </Badge>
                </div>
                <div>
                  <span className="font-medium">Completed Items:</span> {completedItems}/{totalItems}
                </div>
                <div>
                  <span className="font-medium">Signatures:</span> {signatures.length}
                </div>
              </div>
            </div>

            {overallResult === 'Fail' && (
              <div className="bg-red-50 border border-red-200 p-3 rounded-lg">
                <p className="text-sm text-red-800">
                  This inspection has failed. Please review the failed items and consider requiring rework.
                </p>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCompleteDialog(false)}>
              Cancel
            </Button>
            <div className="flex space-x-2">
              {overallResult === 'Fail' && (
                <Button 
                  variant="destructive" 
                  onClick={() => completeInspection('require_rework')}
                  disabled={saving}
                >
                  Require Rework
                </Button>
              )}
              <Button 
                onClick={() => completeInspection('approve')}
                disabled={saving}
                className={overallResult === 'Pass' ? '' : 'bg-yellow-600 hover:bg-yellow-700'}
              >
                {overallResult === 'Pass' ? 'Approve' : 'Approve with Conditions'}
              </Button>
            </div>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Signature Dialog */}
      <Dialog open={showSignatureDialog} onOpenChange={setShowSignatureDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Digital Signature</DialogTitle>
            <DialogDescription>
              Please provide your signature to certify the inspection.
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4">
            <div className="border-2 border-dashed border-gray-300 rounded-lg p-4">
              <canvas
                ref={signatureCanvasRef}
                width={400}
                height={200}
                className="w-full h-32 border rounded"
                onMouseDown={startDrawing}
                onMouseMove={draw}
                onMouseUp={stopDrawing}
                onMouseLeave={stopDrawing}
                style={{ touchAction: 'none' }}
              />
            </div>
            
            <div className="flex space-x-2">
              <Button variant="outline" onClick={clearSignature} className="flex-1">
                Clear
              </Button>
              <Button onClick={saveSignature} className="flex-1">
                Save Signature
              </Button>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowSignatureDialog(false)}>
              Cancel
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}