'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, CheckCircle, Clock, Send } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import maintenanceApiService, { Employee, Asset, WorkOrderType, PriorityLevel } from '@/services/maintenanceApiService';
import jobCardService, { JobCard as JobCardType, CreateJobCardRequest, JobCardApprovalAction } from '@/services/jobCardService';
import workflowApiService from '@/services/workflow-api.service';
import { notificationService } from '@/services/notificationService';
import ClientOnly from '@/components/ui/client-only';
import { useToast } from '@/hooks/use-toast';

// Use the JobCard type from service instead of local interface
interface JobCard {
  id: string;
  jobCardNumber: string;
  title: string;
  description?: string;
  assetId: string;
  assetName: string;
  assetCode: string;
  maintenanceTypeId: string;
  maintenanceType: string;
  priorityLevelId: string;
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  priorityColor: string;
  jobCardStatus: 'Draft' | 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Cancelled';
  approvalStatus: 'NotStarted' | 'Pending' | 'Approved' | 'Rejected' | 'ChangesRequested';
  requestedBy: string;
  requestedDate: string;
  requiredCompletionDate?: string;
  estimatedHours: number;
  estimatedCost: number;
  requiresShutdown: boolean;
  requiresSafetyPermit: boolean;
  generatedWorkOrderId?: string;
  workOrderGeneratedAt?: string;
  createdAt: string;
}

export default function JobCardsPage() {
  const { toast } = useToast();
  const [jobCards, setJobCards] = useState<JobCard[]>([]);
  const [filteredCards, setFilteredCards] = useState<JobCard[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [priorityFilter, setPriorityFilter] = useState<string>('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [selectedCard, setSelectedCard] = useState<JobCard | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  
  // Testing mode - set to true to use mock data and bypass API calls
  const TESTING_MODE = false; // Using real API calls
  
  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [priorityLevels, setPriorityLevels] = useState<PriorityLevel[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<any[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  
  const [newJobCard, setNewJobCard] = useState({
    title: '',
    description: '',
    assetName: '',
    priority: 'Medium' as const,
    maintenanceType: 'Preventive',
    estimatedHours: 0,
    estimatedCost: 0,
    problemDescription: '',
  });
  
  const [editJobCard, setEditJobCard] = useState({
    title: '',
    description: '',
    assetName: '',
    assetId: '',
    priority: 'Medium' as const,
    maintenanceType: '',
    maintenanceTypeId: '',
    priorityLevelId: '',
    estimatedHours: 0,
    estimatedCost: 0,
    problemDescription: '',
  });

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      
      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Using mock data');
        
        // Mock data for testing
        const mockAssets = [
          { id: '1', name: 'HVAC Unit 1', assetNumber: 'HVAC-001' },
          { id: '2', name: 'Elevator Unit 1', assetNumber: 'ELEV-001' },
          { id: '3', name: 'Generator Unit 1', assetNumber: 'GEN-001' },
          { id: '4', name: 'Fire Pump System', assetNumber: 'FP-001' }
        ];
        
        const mockPriorityLevels = [
          { id: '1', name: 'Low' },
          { id: '2', name: 'Medium' },
          { id: '3', name: 'High' },
          { id: '4', name: 'Critical' }
        ];
        
        const mockMaintenanceTypes = [
          { id: '1', name: 'Preventive' },
          { id: '2', name: 'Corrective' },
          { id: '3', name: 'Emergency' },
          { id: '4', name: 'Inspection' }
        ];
        
        const mockJobCards: JobCard[] = [
          {
            id: '1',
            jobCardNumber: 'JC-2024-001',
            title: 'HVAC Filter Replacement',
            description: 'Replace air filters and inspect system',
            assetName: 'HVAC Unit 1',
            requestedBy: 'John Doe',
            jobCardStatus: 'Draft',
            approvalStatus: 'NotStarted',
            priority: 'Medium',
            createdAt: new Date().toISOString(),
            estimatedHours: 4,
            estimatedCost: 250,
            maintenanceType: 'Preventive',
            assetId: '1',
            maintenanceTypeId: '1',
            priorityLevelId: '2'
          },
          {
            id: '2',
            jobCardNumber: 'JC-2024-002',
            title: 'Elevator Emergency Repair',
            description: 'Elevator malfunction - immediate attention required',
            assetName: 'Elevator Unit 1',
            requestedBy: 'Jane Smith',
            jobCardStatus: 'Submitted',
            approvalStatus: 'Pending',
            priority: 'Critical',
            createdAt: new Date(Date.now() - 86400000).toISOString(), // Yesterday
            estimatedHours: 8,
            estimatedCost: 1200,
            maintenanceType: 'Emergency',
            assetId: '2',
            maintenanceTypeId: '3',
            priorityLevelId: '4'
          }
        ];
        
        // Simulate loading delay
        await new Promise(resolve => setTimeout(resolve, 1000));
        
        setAssets(mockAssets);
        setPriorityLevels(mockPriorityLevels);
        setMaintenanceTypes(mockMaintenanceTypes);
        setJobCards(mockJobCards);
        setFilteredCards(mockJobCards);
        
        console.log('✅ Mock data loaded successfully');
      } else {
        // Debug authentication token
        const authToken = localStorage.getItem('authToken');
        console.log('🔑 Authentication token:', authToken ? 'Present' : 'Missing');
        console.log('🔑 Token preview:', authToken ? `${authToken.substring(0, 20)}...` : 'N/A');
        
        try {
          const [assetsResponse, priorityLevelsList, maintenanceTypesList] = await Promise.all([
            maintenanceApiService.getAssets(),
            maintenanceApiService.getPriorityLevels(),
            maintenanceApiService.getMaintenanceTypes()
          ]);
          
          console.log('Loaded assets:', assetsResponse);
          console.log('Loaded priority levels:', priorityLevelsList);
          console.log('Loaded maintenance types:', maintenanceTypesList);
          
          setAssets(assetsResponse.items || []);
          setPriorityLevels(priorityLevelsList || []);
          setMaintenanceTypes(maintenanceTypesList || []);
          
          // Load job cards from real API
          try {
            const jobCardsResponse = await jobCardService.getJobCards();
            const mappedJobCards = jobCardsResponse.items.map((card: JobCardType) => ({
              id: card.id,
              jobCardNumber: card.jobCardNumber,
              title: card.title,
              description: card.description,
              assetName: card.assetName,
              requestedBy: card.requestedBy,
              jobCardStatus: card.jobCardStatus as JobCard['jobCardStatus'],
              approvalStatus: card.approvalStatus as JobCard['approvalStatus'],
              priority: card.priority as JobCard['priority'],
              createdAt: card.createdAt,
              estimatedHours: card.estimatedHours,
              estimatedCost: card.estimatedCost,
              maintenanceType: card.maintenanceType,
              generatedWorkOrderId: card.generatedWorkOrderId,
              assetId: card.assetId,
              maintenanceTypeId: card.maintenanceTypeId,
              priorityLevelId: card.priorityLevelId
            }));
            setJobCards(mappedJobCards);
            setFilteredCards(mappedJobCards);
          } catch (jobCardError) {
            console.error('Error loading job cards:', jobCardError);
            setJobCards([]);
            setFilteredCards([]);
          }
        } catch (error) {
          console.error('❌ Error loading data:', error);
          console.error('Error details:', error instanceof Error ? error.message : error);
          
          // Check if it's an authentication error
          if (error instanceof Error && (error.message.includes('401') || error.message.includes('Unauthorized'))) {
            console.error('🚨 Authentication error detected. Please check your login status.');
          }
          
          setAssets([]);
          setPriorityLevels([]);
          setMaintenanceTypes([]);
          setJobCards([]);
          setFilteredCards([]);
        }
      }
      
      setLoadingData(false);
    };
    
    loadData();
  }, []);
  
  const refreshJobCards = async () => {
    if (TESTING_MODE) {
      console.log('🧪 TESTING MODE: Skipping job card refresh');
      return;
    }
    
    console.log('🔄 Refreshing job cards from API...');
    
    try {
      const jobCardsResponse = await jobCardService.getJobCards();
      const mappedJobCards = jobCardsResponse.items.map((card: JobCardType) => ({
        id: card.id,
        jobCardNumber: card.jobCardNumber,
        title: card.title,
        description: card.description,
        assetName: card.assetName,
        requestedBy: card.requestedBy,
        jobCardStatus: card.jobCardStatus as JobCard['jobCardStatus'],
        approvalStatus: card.approvalStatus as JobCard['approvalStatus'],
        priority: card.priority as JobCard['priority'],
        createdAt: card.createdAt,
        estimatedHours: card.estimatedHours,
        estimatedCost: card.estimatedCost,
        maintenanceType: card.maintenanceType,
        generatedWorkOrderId: card.generatedWorkOrderId,
        assetId: card.assetId,
        maintenanceTypeId: card.maintenanceTypeId,
        priorityLevelId: card.priorityLevelId
      }));
      setJobCards(mappedJobCards);
      setFilteredCards(mappedJobCards);
      console.log('✅ Job cards refreshed successfully:', mappedJobCards.length, 'cards loaded');
    } catch (error) {
      console.error('❌ Error refreshing job cards:', error);
      console.error('Error details:', error instanceof Error ? error.message : error);
    }
  };

  useEffect(() => {
    let filtered = jobCards;

    if (searchTerm) {
      filtered = filtered.filter(card => 
        card.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        (card.assetName && card.assetName.toLowerCase().includes(searchTerm.toLowerCase())) ||
        card.jobCardNumber.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter(card => card.jobCardStatus === statusFilter);
    }

    if (priorityFilter && priorityFilter !== 'all') {
      filtered = filtered.filter(card => card.priority === priorityFilter);
    }

    setFilteredCards(filtered);
  }, [jobCards, searchTerm, statusFilter, priorityFilter]);

  const handleCreateJobCard = async () => {
    try {
      // Find selected asset and other data
      const selectedAsset = assets.find(a => a.name === newJobCard.assetName);
      const selectedPriority = priorityLevels.find(pl => pl.name === newJobCard.priority);
      const selectedMaintenanceType = maintenanceTypes.find(mt => mt.name === newJobCard.maintenanceType);
      
      if (!selectedAsset || !selectedPriority || !selectedMaintenanceType) {
        console.error('Missing required selections');
        return;
      }

      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Creating mock job card');
        
        // Create mock job card
        const newMockCard: JobCard = {
          id: `mock-${Date.now()}`,
          jobCardNumber: `JC-${new Date().getFullYear()}-${String(jobCards.length + 1).padStart(3, '0')}`,
          title: newJobCard.title,
          description: newJobCard.description,
          assetName: selectedAsset.name,
          requestedBy: 'Current User',
          jobCardStatus: 'Draft',
          approvalStatus: 'NotStarted',
          priority: newJobCard.priority as JobCard['priority'],
          createdAt: new Date().toISOString(),
          estimatedHours: newJobCard.estimatedHours,
          estimatedCost: newJobCard.estimatedCost,
          maintenanceType: newJobCard.maintenanceType,
          assetId: selectedAsset.id,
          maintenanceTypeId: selectedMaintenanceType.id,
          priorityLevelId: selectedPriority.id
        };
        
        setJobCards(prev => [newMockCard, ...prev]);
        setFilteredCards(prev => [newMockCard, ...prev]);
        console.log('✅ Mock job card created:', newMockCard.jobCardNumber);
      } else {
        // Create job card using real API
        const createRequest: CreateJobCardRequest = {
          title: newJobCard.title,
          description: newJobCard.description,
          problemDescription: newJobCard.problemDescription,
          assetId: selectedAsset.id,
          maintenanceTypeId: selectedMaintenanceType.id,
          priorityLevelId: selectedPriority.id,
          maintenanceLocation: 'Internal',
          estimatedHours: newJobCard.estimatedHours,
          estimatedCost: newJobCard.estimatedCost,
          requiresSpecialTools: false,
          requiresShutdown: false,
          requiresSafetyPermit: false
        };

        console.log('📤 Creating job card with request:', createRequest);
        const createdJobCard = await jobCardService.createJobCard(createRequest);
        console.log('✅ Job card created successfully:', createdJobCard);
        
        // Refresh job cards list
        await refreshJobCards();
      }
      
      setIsCreateDialogOpen(false);
      setNewJobCard({
        title: '',
        description: '',
        assetName: '',
        priority: 'Medium',
        maintenanceType: 'Preventive',
        estimatedHours: 0,
        estimatedCost: 0,
        problemDescription: '',
      });
    } catch (error) {
      console.error('❌ Error creating job card:', error);
      
      // Log additional error details for debugging
      if (error instanceof Error) {
        console.error('Error message:', error.message);
        console.error('Error stack:', error.stack);
      }
      
      // Check if it's an Axios error with response data
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as any;
        console.error('Response status:', axiosError.response?.status);
        console.error('Response data:', axiosError.response?.data);
        console.error('Response headers:', axiosError.response?.headers);
      }
    }
  };

  const getStatusBadge = (jobCardStatus: JobCard['jobCardStatus'], approvalStatus: JobCard['approvalStatus']) => {
    const statusColors = {
      'Draft': 'bg-gray-100 text-gray-800',
      'Submitted': 'bg-blue-100 text-blue-800',
      'UnderReview': 'bg-yellow-100 text-yellow-800',
      'Approved': 'bg-green-100 text-green-800',
      'Rejected': 'bg-red-100 text-red-800',
      'Cancelled': 'bg-red-100 text-red-800',
    };

    const displayStatus = jobCardStatus === 'Submitted' && approvalStatus === 'Pending' ? 'Under Review' : 
                         jobCardStatus === 'Approved' && approvalStatus === 'ChangesRequested' ? 'Changes Requested' :
                         jobCardStatus;

    return (
      <Badge className={statusColors[jobCardStatus] || 'bg-gray-100 text-gray-800'}>
        {displayStatus}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: JobCard['priority']) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[priority]}>
        {priority}
      </Badge>
    );
  };

  const submitJobCard = async (cardId: string) => {
    try {
      const jobCard = jobCards.find(card => card.id === cardId);
      
      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Submitting mock job card');
        
        // Update mock job card status
        setJobCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, jobCardStatus: 'Submitted', approvalStatus: 'Pending' }
            : card
        ));
        setFilteredCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, jobCardStatus: 'Submitted', approvalStatus: 'Pending' }
            : card
        ));
        
        console.log('✅ Mock job card submitted for approval');
        return;
      }
      
      await jobCardService.submitJobCard(cardId, { confirmReadiness: true });
      
      // Send notification for job card submission
      if (jobCard) {
        await notificationService.notifyJobCardSubmission(cardId, jobCard.jobCardNumber);
      }
      
      // Refresh job cards list
      await refreshJobCards();
    } catch (error) {
      console.error('Error submitting job card:', error);
    }
  };

  const approveJobCard = async (cardId: string) => {
    try {
      const jobCard = jobCards.find(card => card.id === cardId);
      
      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Approving mock job card');
        
        // Update mock job card status
        setJobCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, jobCardStatus: 'Approved', approvalStatus: 'Approved' }
            : card
        ));
        setFilteredCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, jobCardStatus: 'Approved', approvalStatus: 'Approved' }
            : card
        ));
        
        console.log('✅ Mock job card approved - ready for work order generation');
        return;
      }
      
      const approvalAction: JobCardApprovalAction = {
        action: 'Approve',
        comments: 'Approved via job card management'
      };
      await jobCardService.processApproval(cardId, approvalAction);
      
      // Send notification for job card approval
      if (jobCard) {
        await notificationService.notifyJobCardApproval(cardId, jobCard.jobCardNumber, 'requester-user-id', true);
      }
      
      // Refresh job cards list
      await refreshJobCards();
    } catch (error) {
      console.error('Error approving job card:', error);
    }
  };

  const rejectJobCard = async (cardId: string) => {
    try {
      const jobCard = jobCards.find(card => card.id === cardId);
      
      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Rejecting mock job card');
        
        // Update mock job card status
        setJobCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, jobCardStatus: 'Rejected', approvalStatus: 'Rejected' }
            : card
        ));
        setFilteredCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, jobCardStatus: 'Rejected', approvalStatus: 'Rejected' }
            : card
        ));
        
        console.log('❌ Mock job card rejected');
        return;
      }
      
      const approvalAction: JobCardApprovalAction = {
        action: 'Reject',
        comments: 'Rejected via job card management'
      };
      await jobCardService.processApproval(cardId, approvalAction);
      
      // Send notification for job card rejection
      if (jobCard) {
        await notificationService.notifyJobCardApproval(cardId, jobCard.jobCardNumber, 'requester-user-id', false);
      }
      
      // Refresh job cards list
      await refreshJobCards();
    } catch (error) {
      console.error('Error rejecting job card:', error);
    }
  };

  const generateWorkOrder = async (cardId: string) => {
    try {
      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Generating mock work order');
        
        // Generate mock work order ID
        const mockWorkOrderId = `WO-${new Date().getFullYear()}-${String(Math.floor(Math.random() * 1000)).padStart(3, '0')}`;
        
        // Update job card to show work order generated
        setJobCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, generatedWorkOrderId: mockWorkOrderId, generatedWorkOrderNumber: mockWorkOrderId }
            : card
        ));
        setFilteredCards(prev => prev.map(card => 
          card.id === cardId 
            ? { ...card, generatedWorkOrderId: mockWorkOrderId, generatedWorkOrderNumber: mockWorkOrderId }
            : card
        ));
        
        // Store work order in localStorage so WorkOrderManagement can access it
        const newWorkOrder = {
          id: `mock-wo-${Date.now()}`,
          workOrderNumber: mockWorkOrderId,
          title: `Work Order for: ${jobCard?.title}`,
          description: `Generated from Job Card: ${jobCard?.jobCardNumber}`,
          jobCardId: cardId,
          assetId: jobCard?.assetId || '1',
          assetName: jobCard?.assetName || 'Unknown Asset',
          workOrderType: 'Scheduled',
          maintenanceType: jobCard?.maintenanceType || 'Preventive',
          priority: jobCard?.priority || 'Medium',
          status: 'Assigned',
          assignedTechnician: 'Auto Assigned',
          estimatedHours: jobCard?.estimatedHours || 0,
          actualHours: 0,
          estimatedCost: jobCard?.estimatedCost || 0,
          actualCost: 0,
          completionPercentage: 0,
          safetyIncident: false,
          qualityCheckRequired: true,
          admissionStatus: 'NotStarted',
          requiresAdmission: true,
          createdAt: new Date().toISOString()
        };
        
        // Get existing work orders from localStorage
        const existingWorkOrders = JSON.parse(localStorage.getItem('mockWorkOrders') || '[]');
        existingWorkOrders.push(newWorkOrder);
        localStorage.setItem('mockWorkOrders', JSON.stringify(existingWorkOrders));
        
        console.log(`✅ Mock work order generated: ${mockWorkOrderId}`);
        console.log('🏥 Work order stored in localStorage for WorkOrderManagement!');
        return;
      }
      
      const result = await jobCardService.generateWorkOrder(cardId);
      console.log('Work order generated:', result);
      // Refresh job cards list to show updated status
      await refreshJobCards();
    } catch (error) {
      console.error('Error generating work order:', error);
    }
  };


  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Job Cards</h1>
          <p className="text-muted-foreground">
            Create and manage maintenance job cards
          </p>
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
            <BreadcrumbPage>Job Cards</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>
      
      <div className="flex items-center justify-between">
        <div></div>
        <ClientOnly fallback={<div className="h-10 w-32 bg-gray-100 rounded animate-pulse"></div>}>
          <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                Create Job Card
              </Button>
            </DialogTrigger>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Create New Job Card</DialogTitle>
              <DialogDescription>
                Fill in the details to create a new maintenance job card.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Title</Label>
                  <Input
                    id="title"
                    value={newJobCard.title}
                    onChange={(e) => setNewJobCard(prev => ({ ...prev, title: e.target.value }))}
                    placeholder="Job card title"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="assetName">Asset</Label>
                  <Select value={newJobCard.assetName} onValueChange={(value) => setNewJobCard(prev => ({ ...prev, assetName: value }))} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={
                        loadingData ? "Loading assets..." : 
                        assets.length === 0 ? "No assets available" :
                        "Select asset"
                      } />
                    </SelectTrigger>
                    <SelectContent>
                      {loadingData ? (
                        <SelectItem value="loading" disabled>
                          Loading assets...
                        </SelectItem>
                      ) : assets.length === 0 ? (
                        <SelectItem value="no-assets" disabled>
                          No assets found. Check console for errors.
                        </SelectItem>
                      ) : (
                        assets.map((asset) => (
                          <SelectItem key={asset.id} value={asset.name}>
                            {asset.name} ({asset.assetNumber})
                          </SelectItem>
                        ))
                      )}
                    </SelectContent>
                  </Select>
                  {!loadingData && assets.length === 0 && (
                    <p className="text-sm text-red-600">
                      No assets loaded. Check browser console for API errors.
                    </p>
                  )}
                  {!loadingData && assets.length > 0 && (
                    <p className="text-sm text-green-600">
                      {assets.length} assets loaded successfully
                    </p>
                  )}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="maintenanceType">Maintenance Type</Label>
                <Select value={newJobCard.maintenanceType} onValueChange={(value) => setNewJobCard(prev => ({ ...prev, maintenanceType: value }))} disabled={loadingData}>
                  <SelectTrigger>
                    <SelectValue placeholder={loadingData ? "Loading types..." : "Select maintenance type"} />
                  </SelectTrigger>
                  <SelectContent>
                    {maintenanceTypes.map((type) => (
                      <SelectItem key={type.id} value={type.name}>
                        {type.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={newJobCard.description}
                  onChange={(e) => setNewJobCard(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Detailed description of the maintenance needed"
                  rows={2}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="problemDescription">Problem Description</Label>
                <Textarea
                  id="problemDescription"
                  value={newJobCard.problemDescription}
                  onChange={(e) => setNewJobCard(prev => ({ ...prev, problemDescription: e.target.value }))}
                  placeholder="Specific problem or issue identified"
                  rows={2}
                />
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
                  <Select value={newJobCard.priority} onValueChange={(value: any) => setNewJobCard(prev => ({ ...prev, priority: value }))} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading..." : "Select priority"} />
                    </SelectTrigger>
                    <SelectContent>
                      {priorityLevels.map((priority) => (
                        <SelectItem key={priority.id} value={priority.name}>
                          {priority.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="estimatedHours">Estimated Hours</Label>
                  <Input
                    id="estimatedHours"
                    type="number"
                    min="0"
                    step="0.5"
                    value={newJobCard.estimatedHours}
                    onChange={(e) => setNewJobCard(prev => ({ ...prev, estimatedHours: parseFloat(e.target.value) || 0 }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="estimatedCost">Estimated Cost</Label>
                  <Input
                    id="estimatedCost"
                    type="number"
                    min="0"
                    step="0.01"
                    value={newJobCard.estimatedCost}
                    onChange={(e) => setNewJobCard(prev => ({ ...prev, estimatedCost: parseFloat(e.target.value) || 0 }))}
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateJobCard}>
                Create Job Card
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        </ClientOnly>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="p-4">
          <div className="flex items-center space-x-4">
            <div className="flex-1 max-w-sm">
              <Label htmlFor="search" className="sr-only">Search</Label>
              <div className="relative">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  id="search"
                  placeholder="Search job cards..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
            </div>
            <ClientOnly fallback={<div className="w-[140px] h-10 bg-gray-100 rounded animate-pulse"></div>}>
              <div className="space-y-1">
                <Label htmlFor="status-filter" className="text-sm">Status</Label>
                <Select value={statusFilter} onValueChange={setStatusFilter}>
                  <SelectTrigger className="w-[140px]">
                    <SelectValue placeholder="All Status" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Status</SelectItem>
                    <SelectItem value="Draft">Draft</SelectItem>
                    <SelectItem value="Submitted">Submitted</SelectItem>
                    <SelectItem value="UnderReview">Under Review</SelectItem>
                    <SelectItem value="Approved">Approved</SelectItem>
                    <SelectItem value="Rejected">Rejected</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </ClientOnly>
            <ClientOnly fallback={<div className="w-[140px] h-10 bg-gray-100 rounded animate-pulse"></div>}>
              <div className="space-y-1">
                <Label htmlFor="priority-filter" className="text-sm">Priority</Label>
                <Select value={priorityFilter} onValueChange={setPriorityFilter}>
                  <SelectTrigger className="w-[140px]">
                    <SelectValue placeholder="All Priority" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Priority</SelectItem>
                    <SelectItem value="Low">Low</SelectItem>
                    <SelectItem value="Medium">Medium</SelectItem>
                    <SelectItem value="High">High</SelectItem>
                    <SelectItem value="Critical">Critical</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </ClientOnly>
          </div>
        </CardContent>
      </Card>

      {/* Job Cards Table */}
      <Card>
        <CardHeader>
          <CardTitle>Job Cards ({filteredCards.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Job Card #</TableHead>
                <TableHead>Title</TableHead>
                <TableHead>Asset</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Priority</TableHead>
                <TableHead>Requested By</TableHead>
                <TableHead>Created</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredCards.map((card) => (
                <TableRow key={card.id}>
                  <TableCell className="font-medium">{card.jobCardNumber}</TableCell>
                  <TableCell>{card.title}</TableCell>
                  <TableCell>{card.assetName}</TableCell>
                  <TableCell>{getStatusBadge(card.jobCardStatus, card.approvalStatus)}</TableCell>
                  <TableCell>{getPriorityBadge(card.priority)}</TableCell>
                  <TableCell>{card.requestedBy}</TableCell>
                  <TableCell>{new Date(card.createdAt).toLocaleDateString()}</TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedCard(card);
                          setIsViewDialogOpen(true);
                        }}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                      
                      {/* Draft status actions */}
                      {card.jobCardStatus === 'Draft' && (
                        <>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setSelectedCard(card);
                              // Populate edit form with card data
                              setEditJobCard({
                                title: card.title,
                                description: card.description || '',
                                assetName: card.assetName,
                                assetId: card.assetId,
                                priority: card.priority,
                                maintenanceType: card.maintenanceType,
                                maintenanceTypeId: card.maintenanceTypeId,
                                priorityLevelId: card.priorityLevelId,
                                estimatedHours: card.estimatedHours,
                                estimatedCost: card.estimatedCost,
                                problemDescription: '',
                              });
                              setIsEditDialogOpen(true);
                            }}
                            title="Edit job card"
                          >
                            <Edit className="h-4 w-4" />
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => submitJobCard(card.id)}
                            title="Submit for approval"
                          >
                            <Send className="h-4 w-4" />
                          </Button>
                        </>
                      )}
                      
                      {/* Submitted/Pending approval actions */}
                      {card.jobCardStatus === 'Submitted' && card.approvalStatus === 'Pending' && (
                        <>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => approveJobCard(card.id)}
                            className="bg-green-50 hover:bg-green-100"
                            title="Approve job card"
                          >
                            <CheckCircle className="h-4 w-4 text-green-600" />
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => rejectJobCard(card.id)}
                            className="bg-red-50 hover:bg-red-100"
                            title="Reject job card"
                          >
                            <AlertCircle className="h-4 w-4 text-red-600" />
                          </Button>
                        </>
                      )}
                      
                      {/* Approved status actions */}
                      {card.jobCardStatus === 'Approved' && !card.generatedWorkOrderId && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => generateWorkOrder(card.id)}
                          className="bg-blue-50 hover:bg-blue-100"
                          title="Generate work order"
                        >
                          Generate WO
                        </Button>
                      )}
                      
                      {/* Show work order link if generated */}
                      {card.generatedWorkOrderId && (
                        <Button
                          size="sm"
                          variant="outline"
                          className="bg-gray-50"
                          disabled
                          title="Work order already generated"
                        >
                          WO Generated
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Edit Job Card Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Edit Job Card</DialogTitle>
            <DialogDescription>
              Update job card details
            </DialogDescription>
          </DialogHeader>
          {selectedCard && (
            <div className="grid gap-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-title">Title</Label>
                  <Input
                    id="edit-title"
                    value={editJobCard.title}
                    onChange={(e) => setEditJobCard(prev => ({ ...prev, title: e.target.value }))}
                    placeholder="Job card title"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-asset">Asset</Label>
                  <Select value={editJobCard.assetId} onValueChange={(value) => {
                    const asset = assets.find(a => a.id === value);
                    setEditJobCard(prev => ({ 
                      ...prev, 
                      assetId: value,
                      assetName: asset?.name || ''
                    }));
                  }}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select asset" />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.name} ({asset.assetNumber})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-maintenanceType">Maintenance Type</Label>
                <Select value={editJobCard.maintenanceTypeId} onValueChange={(value) => {
                  const type = maintenanceTypes.find(t => t.id === value);
                  setEditJobCard(prev => ({ 
                    ...prev, 
                    maintenanceTypeId: value,
                    maintenanceType: type?.name || ''
                  }));
                }}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select maintenance type" />
                  </SelectTrigger>
                  <SelectContent>
                    {maintenanceTypes.map((type) => (
                      <SelectItem key={type.id} value={type.id}>
                        {type.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-description">Description</Label>
                <Textarea
                  id="edit-description"
                  value={editJobCard.description}
                  onChange={(e) => setEditJobCard(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Detailed description of the maintenance needed"
                  rows={2}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-problemDescription">Problem Description</Label>
                <Textarea
                  id="edit-problemDescription"
                  value={editJobCard.problemDescription}
                  onChange={(e) => setEditJobCard(prev => ({ ...prev, problemDescription: e.target.value }))}
                  placeholder="Specific problem or issue identified"
                  rows={2}
                />
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-priority">Priority</Label>
                  <Select value={editJobCard.priorityLevelId} onValueChange={(value) => {
                    const priority = priorityLevels.find(p => p.id === value);
                    setEditJobCard(prev => ({ 
                      ...prev, 
                      priorityLevelId: value,
                      priority: priority?.name as any
                    }));
                  }}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select priority" />
                    </SelectTrigger>
                    <SelectContent>
                      {priorityLevels.map((priority) => (
                        <SelectItem key={priority.id} value={priority.id}>
                          {priority.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-hours">Estimated Hours</Label>
                  <Input
                    id="edit-hours"
                    type="number"
                    min="0"
                    step="0.5"
                    value={editJobCard.estimatedHours}
                    onChange={(e) => setEditJobCard(prev => ({ ...prev, estimatedHours: parseFloat(e.target.value) || 0 }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-cost">Estimated Cost</Label>
                  <Input
                    id="edit-cost"
                    type="number"
                    min="0"
                    step="0.01"
                    value={editJobCard.estimatedCost}
                    onChange={(e) => setEditJobCard(prev => ({ ...prev, estimatedCost: parseFloat(e.target.value) || 0 }))}
                  />
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={async () => {
              if (!selectedCard?.id) return;
              
              try {
                console.log('Updating job card:', selectedCard.id);
                console.log('Job card status:', selectedCard.jobCardStatus);
                console.log('Update data:', editJobCard);
                
                const updateData = {
                  title: editJobCard.title,
                  description: editJobCard.description,
                  problemDescription: editJobCard.problemDescription,
                  maintenanceTypeId: editJobCard.maintenanceTypeId,
                  priorityLevelId: editJobCard.priorityLevelId,
                  estimatedHours: editJobCard.estimatedHours,
                  estimatedCost: editJobCard.estimatedCost,
                  maintenanceLocation: 'Internal',
                  requiresSpecialTools: false,
                  requiresShutdown: false,
                  requiresSafetyPermit: false
                };
                
                console.log('Sending update request...', updateData);
                const response = await jobCardService.updateJobCard(selectedCard.id, updateData);
                console.log('Update response:', response);
                
                // Refresh the job cards list
                const result = await jobCardService.getJobCards({ page: 1, pageSize: 100 });
                setJobCards(result.items);
                setFilteredCards(result.items);
                
                setIsEditDialogOpen(false);
                toast({
                  title: "Success",
                  description: "Job card updated successfully",
                  variant: "success",
                });
              } catch (error: any) {
                console.error('Error updating job card:', error);
                console.error('Error response:', error.response);
                const errorMsg = error.response?.data?.message || error.response?.data || error.message;
                toast({
                  title: "Error",
                  description: `Failed to update job card: ${errorMsg}`,
                  variant: "destructive",
                });
              }
            }}>
              Save Changes
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      
      {/* View Job Card Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Job Card Details</DialogTitle>
          </DialogHeader>
          {selectedCard && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Job Card #</Label>
                  <p className="text-sm">{selectedCard.jobCardNumber}</p>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Asset</Label>
                  <p className="text-sm">{selectedCard.assetName}</p>
                </div>
              </div>
              <div>
                <Label className="text-sm font-medium text-muted-foreground">Title</Label>
                <p className="text-sm">{selectedCard.title}</p>
              </div>
              <div>
                <Label className="text-sm font-medium text-muted-foreground">Description</Label>
                <p className="text-sm">{selectedCard.description}</p>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                  <div className="pt-1">{getStatusBadge(selectedCard.status)}</div>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Priority</Label>
                  <div className="pt-1">{getPriorityBadge(selectedCard.priority)}</div>
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

