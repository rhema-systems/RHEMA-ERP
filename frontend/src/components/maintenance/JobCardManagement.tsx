'use client';

import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, CheckCircle, Clock, Send, XCircle } from 'lucide-react';
import { format } from 'date-fns';
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
import jobCardService, { JobCard as JobCardType, JobCardDetails, CreateJobCardRequest, JobCardApprovalAction } from '@/services/jobCardService';
import workOrderService, { WorkOrder } from '@/services/workOrderService';
import qualityControlService from '@/services/qualityControlService';
import workflowApiService from '@/services/workflow-api.service';
import { notificationService } from '@/services/notificationService';
import ClientOnly from '@/components/ui/client-only';
import { useToast } from '@/hooks/use-toast';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

// Use the JobCard type from service instead of local interface
interface JobCard {
  id: string;
  jobCardNumber: string;
  title: string;
  description?: string;
  problemDescription?: string;
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
  const searchParams = useSearchParams();
  const [jobCards, setJobCards] = useState<JobCard[]>([]);
  const [filteredCards, setFilteredCards] = useState<JobCard[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [priorityFilter, setPriorityFilter] = useState<string>('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [selectedCard, setSelectedCard] = useState<JobCard | null>(null);
  const [selectedCardDetails, setSelectedCardDetails] = useState<JobCardDetails | null>(null);
  const [selectedWorkOrder, setSelectedWorkOrder] = useState<WorkOrder | null>(null);
  const [selectedQCInspection, setSelectedQCInspection] = useState<any | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [loadingDetails, setLoadingDetails] = useState(false);
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
              problemDescription: card.problemDescription,
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
        problemDescription: card.problemDescription,
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

  // Handle opening job card from URL parameter
  useEffect(() => {
    const jobCardId = searchParams.get('id');
    if (jobCardId && jobCards.length > 0 && !isViewDialogOpen && !loadingData) {
      const jobCard = jobCards.find(jc => jc.id === jobCardId);
      if (jobCard) {
        console.log('Opening job card from URL:', jobCard);
        // Use the same logic as the View button
        setSelectedCard(jobCard);
        setIsViewDialogOpen(true);
        setLoadingDetails(true);
        
        // Load job card details asynchronously
        (async () => {
          try {
            const details = await jobCardService.getJobCardById(jobCard.id);
            setSelectedCardDetails(details);
            
            // Load work order if it exists
            if (details.generatedWorkOrderId) {
              try {
                const workOrder = await workOrderService.getWorkOrderById(details.generatedWorkOrderId);
                setSelectedWorkOrder(workOrder);
                
                // Load QC inspection if work order is completed
                if (workOrder.status === 'Completed') {
                  try {
                    const inspections = await qualityControlService.getCompletedInspections();
                    const qcInspection = inspections.find((insp: any) => insp.workOrderId === workOrder.id);
                    setSelectedQCInspection(qcInspection || null);
                  } catch (qcError) {
                    console.log('No QC inspection found');
                  }
                }
              } catch (woError) {
                console.log('Work order not found or not accessible');
              }
            }
          } catch (error) {
            console.error('Error loading job card details:', error);
            toast({
              title: "Error",
              description: "Failed to load job card details",
              variant: "destructive",
            });
          } finally {
            setLoadingDetails(false);
          }
        })();
      } else {
        console.warn('Job card not found with ID:', jobCardId);
        toast({
          title: 'Job Card not found',
          description: 'The requested job card could not be found.',
          variant: 'destructive'
        });
      }
    }
  }, [jobCards, searchParams, isViewDialogOpen, loadingData, toast]);

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
        
        toast({
          title: "Success",
          description: `Job card ${createdJobCard.jobCardNumber} created successfully`,
          className: "bg-green-50 border-green-200",
        });
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
      let errorMessage = 'Failed to create job card. Please try again.';
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as any;
        console.error('Response status:', axiosError.response?.status);
        console.error('Response data:', axiosError.response?.data);
        console.error('Response headers:', axiosError.response?.headers);
        errorMessage = axiosError.response?.data?.message || axiosError.response?.data || errorMessage;
      }
      
      toast({
        title: "Error",
        description: errorMessage,
        variant: "destructive",
      });
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
      
      toast({
        title: "Success",
        description: `Job card ${jobCard?.jobCardNumber || ''} submitted for approval successfully`,
        className: "bg-green-50 border-green-200",
      });
    } catch (error) {
      console.error('Error submitting job card:', error);
      toast({
        title: "Error",
        description: "Failed to submit job card. Please try again.",
        variant: "destructive",
      });
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
      
      toast({
        title: "Success",
        description: `Job card ${jobCard?.jobCardNumber || ''} approved successfully`,
        className: "bg-green-50 border-green-200",
      });
    } catch (error) {
      console.error('Error approving job card:', error);
      toast({
        title: "Error",
        description: "Failed to approve job card. Please try again.",
        variant: "destructive",
      });
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
      
      toast({
        title: "Job Card Rejected",
        description: `Job card ${jobCard?.jobCardNumber || ''} has been rejected`,
        className: "bg-yellow-50 border-yellow-200",
      });
    } catch (error) {
      console.error('Error rejecting job card:', error);
      toast({
        title: "Error",
        description: "Failed to reject job card. Please try again.",
        variant: "destructive",
      });
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
      
      toast({
        title: "Success",
        description: `Work order generated successfully from job card ${jobCards.find(c => c.id === cardId)?.jobCardNumber || ''}`,
        className: "bg-green-50 border-green-200",
      });
    } catch (error) {
      console.error('Error generating work order:', error);
      toast({
        title: "Error",
        description: "Failed to generate work order. Please try again.",
        variant: "destructive",
      });
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
                        onClick={async () => {
                          setSelectedCard(card);
                          setIsViewDialogOpen(true);
                          setLoadingDetails(true);
                          try {
                            const details = await jobCardService.getJobCardById(card.id);
                            setSelectedCardDetails(details);
                            
                            // Load work order if it exists
                            if (details.generatedWorkOrderId) {
                              try {
                                const workOrder = await workOrderService.getWorkOrderById(details.generatedWorkOrderId);
                                setSelectedWorkOrder(workOrder);
                                
                                // Load QC inspection if work order is completed
                                if (workOrder.status === 'Completed') {
                                  try {
                                    const inspections = await qualityControlService.getCompletedInspections();
                                    const qcInspection = inspections.find((insp: any) => insp.workOrderId === workOrder.id);
                                    setSelectedQCInspection(qcInspection || null);
                                  } catch (qcError) {
                                    console.log('No QC inspection found');
                                  }
                                }
                              } catch (woError) {
                                console.log('Work order not found or not accessible');
                              }
                            }
                          } catch (error) {
                            console.error('Error loading job card details:', error);
                            toast({
                              title: "Error",
                              description: "Failed to load job card details",
                              variant: "destructive",
                            });
                          } finally {
                            setLoadingDetails(false);
                          }
                        }}
                        title="View job card details"
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
                                problemDescription: card.problemDescription || '',
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
            <DialogTitle>Edit Job Card {selectedCard?.jobCardNumber ? `- ${selectedCard.jobCardNumber}` : ''}</DialogTitle>
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
                  description: `Job card ${selectedCard.jobCardNumber} updated successfully`,
                  className: "bg-green-50 border-green-200",
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
      <Dialog open={isViewDialogOpen} onOpenChange={(open) => {
        setIsViewDialogOpen(open);
        if (!open) {
          setSelectedCardDetails(null);
          setSelectedWorkOrder(null);
          setSelectedQCInspection(null);
        }
      }}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Job Card Details</DialogTitle>
            <DialogDescription>
              {selectedCard?.jobCardNumber || 'Loading...'}
            </DialogDescription>
          </DialogHeader>
          {loadingDetails ? (
            <div className="flex items-center justify-center py-8">
              <Clock className="h-6 w-6 animate-spin" />
              <span className="ml-2">Loading details...</span>
            </div>
          ) : selectedCardDetails && (
            <Tabs defaultValue="overview" className="w-full">
              <TabsList className="grid w-full grid-cols-4">
                <TabsTrigger value="overview">Overview</TabsTrigger>
                <TabsTrigger value="workorder" disabled={!selectedWorkOrder}>Work Order</TabsTrigger>
                <TabsTrigger value="qc" disabled={!selectedQCInspection}>QC Inspection</TabsTrigger>
                <TabsTrigger value="workflow">Workflow</TabsTrigger>
              </TabsList>

              {/* Overview Tab */}
              <TabsContent value="overview" className="space-y-6 mt-4">
                {/* Key Information Card */}
                <Card>
                  <CardHeader>
                    <CardTitle>{selectedCardDetails.title}</CardTitle>
                    <CardDescription>Job Card #{selectedCardDetails.jobCardNumber}</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="grid grid-cols-3 gap-4">
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                        <div className="pt-1">{getStatusBadge(selectedCardDetails.jobCardStatus)}</div>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Approval Status</Label>
                        <div className="pt-1">
                          <Badge style={selectedCardDetails.approvalStatus === 'Approved' ? { backgroundColor: '#d1fae5', color: '#065f46' } : selectedCardDetails.approvalStatus === 'Rejected' ? { backgroundColor: '#fee2e2', color: '#991b1b' } : { backgroundColor: '#fef3c7', color: '#92400e' }}>
                            {selectedCardDetails.approvalStatus}
                          </Badge>
                        </div>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Priority</Label>
                        <div className="pt-1">{getPriorityBadge(selectedCardDetails.priority)}</div>
                      </div>
                    </div>

                    <div className="grid grid-cols-2 gap-4 border-t pt-4">
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Requested By</Label>
                        <p className="text-sm font-medium">{selectedCardDetails.requestedBy}</p>
                        <p className="text-xs text-muted-foreground">{format(new Date(selectedCardDetails.requestedDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                      </div>
                      {selectedCardDetails.approvedBy && (
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">Approved By</Label>
                          <p className="text-sm font-medium">{selectedCardDetails.approvedBy}</p>
                          {selectedCardDetails.approvedAt && (
                            <p className="text-xs text-muted-foreground">{format(new Date(selectedCardDetails.approvedAt), 'MMM dd, yyyy HH:mm:ss')}</p>
                          )}
                        </div>
                      )}
                    </div>
                  </CardContent>
                </Card>

                {/* Asset Information */}
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">Asset Information</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="grid grid-cols-2 gap-4">
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Asset Name</Label>
                        <p className="text-sm font-medium">{selectedCardDetails.assetName}</p>
                        <p className="text-xs text-muted-foreground">{selectedCardDetails.assetCode}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Asset Category/Type</Label>
                        <p className="text-sm">{selectedCardDetails.assetType || selectedCardDetails.maintenanceCategory || 'N/A'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Location</Label>
                        <p className="text-sm">{selectedCardDetails.assetLocation || selectedCardDetails.maintenanceLocation || 'N/A'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Maintenance Type</Label>
                        <p className="text-sm">{selectedCardDetails.maintenanceType}</p>
                      </div>
                    </div>
                  </CardContent>
                </Card>

                {/* Work Description Card */}
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">Work Description</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {selectedCardDetails.description && (
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Description</Label>
                        <p className="text-sm whitespace-pre-wrap">{selectedCardDetails.description}</p>
                      </div>
                    )}
                    {selectedCardDetails.problemDescription && (
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Problem Description</Label>
                        <p className="text-sm whitespace-pre-wrap bg-red-50 p-3 rounded">{selectedCardDetails.problemDescription}</p>
                      </div>
                    )}
                  </CardContent>
                </Card>

              {/* Comments */}
              {selectedCardDetails.comments && selectedCardDetails.comments.length > 0 && (
                <div className="border-t pt-4">
                  <h3 className="text-lg font-semibold mb-3">Comments</h3>
                  <div className="space-y-2">
                    {selectedCardDetails.comments.map((comment) => (
                      <div key={comment.id} className="border rounded-lg p-3">
                        <div className="flex items-center justify-between mb-1">
                          <span className="text-sm font-medium">{comment.commentBy}</span>
                            <div className="flex items-center space-x-2">
                            {comment.isInternal && <Badge variant="outline" className="text-xs">Internal</Badge>}
                            <span className="text-xs text-muted-foreground">{format(new Date(comment.commentDate), 'MMM dd, yyyy HH:mm:ss')}</span>
                          </div>
                        </div>
                        <p className="text-sm">{comment.comment}</p>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Documents */}
              {selectedCardDetails.documents && selectedCardDetails.documents.length > 0 && (
                <div className="border-t pt-4">
                  <h3 className="text-lg font-semibold mb-3">Documents</h3>
                  <div className="space-y-2">
                    {selectedCardDetails.documents.map((doc) => (
                      <div key={doc.id} className="flex items-center justify-between border rounded-lg p-3">
                        <div>
                          <p className="text-sm font-medium">{doc.fileName}</p>
                          <p className="text-xs text-muted-foreground">
                            {doc.documentType} • {(doc.fileSize / 1024).toFixed(2)} KB • Uploaded by {doc.uploadedBy}
                          </p>
                        </div>
                        <Button size="sm" variant="outline">
                          Download
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              )}
              </TabsContent>

              {/* WORKFLOW TAB */}
              <TabsContent value="workflow" className="space-y-4 mt-4">
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">Complete Workflow Timeline</CardTitle>
                    <CardDescription>Track the journey from job card request to work completion</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-6">
                    {/* Job Card Creation */}
                    <div className="flex gap-4">
                      <div className="flex flex-col items-center">
                        <div className="w-10 h-10 rounded-full bg-blue-100 flex items-center justify-center">
                          <Calendar className="h-5 w-5 text-blue-600" />
                        </div>
                        <div className="w-0.5 h-full bg-blue-200 mt-2"></div>
                      </div>
                      <div className="flex-1 pb-6">
                        <h4 className="font-semibold text-base">Job Card Created</h4>
                        <p className="text-sm text-muted-foreground mt-1">Requested by {selectedCardDetails.requestedBy}</p>
                        <p className="text-xs text-muted-foreground">{format(new Date(selectedCardDetails.requestedDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                      </div>
                    </div>

                    {/* Approval Steps */}
                    {selectedCardDetails.approvalSteps && selectedCardDetails.approvalSteps.length > 0 && selectedCardDetails.approvalSteps.map((step, index) => (
                      <div key={step.id} className="flex gap-4">
                        <div className="flex flex-col items-center">
                          <div className={`w-10 h-10 rounded-full flex items-center justify-center ${
                            step.status === 'Approved' ? 'bg-green-100' : 
                            step.status === 'Rejected' ? 'bg-red-100' : 'bg-yellow-100'
                          }`}>
                            {step.status === 'Approved' ? <CheckCircle className="h-5 w-5 text-green-600" /> : 
                             step.status === 'Rejected' ? <XCircle className="h-5 w-5 text-red-600" /> : 
                             <Clock className="h-5 w-5 text-yellow-600" />}
                          </div>
                          {(selectedCardDetails.generatedWorkOrderId || index < selectedCardDetails.approvalSteps.length - 1) && (
                            <div className="w-0.5 h-full bg-gray-200 mt-2"></div>
                          )}
                        </div>
                        <div className="flex-1 pb-6">
                          <div className="flex items-center gap-2">
                            <h4 className="font-semibold text-base">{step.stepName}</h4>
                            <Badge style={step.status === 'Approved' ? { backgroundColor: '#d1fae5', color: '#065f46' } : step.status === 'Rejected' ? { backgroundColor: '#fee2e2', color: '#991b1b' } : { backgroundColor: '#fef3c7', color: '#92400e' }}>
                              {step.status}
                            </Badge>
                          </div>
                          {step.approverName && (
                            <p className="text-sm text-muted-foreground mt-1">Approver: {step.approverName}</p>
                          )}
                          {step.actionDate && (
                            <p className="text-xs text-muted-foreground">{format(new Date(step.actionDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                          )}
                          {step.comments && (
                            <p className="text-sm mt-2 bg-gray-50 p-2 rounded">{step.comments}</p>
                          )}
                        </div>
                      </div>
                    ))}

                    {/* Work Order Generation */}
                    {selectedCardDetails.generatedWorkOrderId && (
                      <div className="flex gap-4">
                        <div className="flex flex-col items-center">
                          <div className="w-10 h-10 rounded-full bg-purple-100 flex items-center justify-center">
                            <CheckCircle className="h-5 w-5 text-purple-600" />
                          </div>
                          {selectedWorkOrder && (
                            <div className="w-0.5 h-full bg-purple-200 mt-2"></div>
                          )}
                        </div>
                        <div className="flex-1 pb-6">
                          <h4 className="font-semibold text-base">Work Order Generated</h4>
                          <p className="text-sm font-mono mt-1">{selectedCardDetails.generatedWorkOrderNumber || selectedCardDetails.generatedWorkOrderId}</p>
                          {selectedCardDetails.workOrderGeneratedAt && (
                            <p className="text-xs text-muted-foreground">{format(new Date(selectedCardDetails.workOrderGeneratedAt), 'MMM dd, yyyy HH:mm:ss')}</p>
                          )}
                        </div>
                      </div>
                    )}

                    {/* Work Order Execution */}
                    {selectedWorkOrder && (
                      <>
                        <div className="flex gap-4">
                          <div className="flex flex-col items-center">
                            <div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center">
                              <AlertCircle className="h-5 w-5 text-orange-600" />
                            </div>
                            {selectedWorkOrder.status === 'Completed' && (
                              <div className="w-0.5 h-full bg-orange-200 mt-2"></div>
                            )}
                          </div>
                          <div className="flex-1 pb-6">
                            <div className="flex items-center gap-2">
                              <h4 className="font-semibold text-base">Work Order Execution</h4>
                              <Badge>{selectedWorkOrder.status}</Badge>
                            </div>
                            {selectedWorkOrder.assignedTechnicianName && (
                              <p className="text-sm text-muted-foreground mt-1">Assigned to: {selectedWorkOrder.assignedTechnicianName}</p>
                            )}
                            {selectedWorkOrder.actualStartDate && (
                              <p className="text-xs text-muted-foreground">Started: {format(new Date(selectedWorkOrder.actualStartDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                            )}
                            {selectedWorkOrder.actualCompletionDate && (
                              <p className="text-xs text-muted-foreground">Completed: {format(new Date(selectedWorkOrder.actualCompletionDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                            )}
                          </div>
                        </div>

                        {/* QC Inspection */}
                        {selectedWorkOrder.status === 'Completed' && (
                          <div className="flex gap-4">
                            <div className="flex flex-col items-center">
                              <div className={`w-10 h-10 rounded-full flex items-center justify-center ${
                                selectedQCInspection ? 'bg-green-100' : 'bg-gray-100'
                              }`}>
                                {selectedQCInspection ? 
                                  <CheckCircle className="h-5 w-5 text-green-600" /> : 
                                  <Clock className="h-5 w-5 text-gray-400" />
                                }
                              </div>
                            </div>
                            <div className="flex-1">
                              <h4 className="font-semibold text-base">Quality Control Inspection</h4>
                              {selectedQCInspection ? (
                                <>
                                  <div className="flex items-center gap-2 mt-1">
                                    <Badge style={selectedQCInspection.overallResult === 'Pass' ? { backgroundColor: '#d1fae5', color: '#065f46' } : { backgroundColor: '#fee2e2', color: '#991b1b' }}>
                                      {selectedQCInspection.overallResult}
                                    </Badge>
                                    <span className="text-sm font-semibold">Score: {selectedQCInspection.score}%</span>
                                  </div>
                                  <p className="text-xs text-muted-foreground">{format(new Date(selectedQCInspection.inspectionDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                                  {selectedQCInspection.overallResult === 'Pass' && (
                                    <p className="text-sm mt-2 text-green-600 font-semibold">✓ Certificate Generated</p>
                                  )}
                                </>
                              ) : (
                                <p className="text-sm text-muted-foreground mt-1">Pending inspection</p>
                              )}
                            </div>
                          </div>
                        )}
                      </>
                    )}
                  </CardContent>
                </Card>
              </TabsContent>

              {/* WORK ORDER TAB */}
              <TabsContent value="workorder" className="space-y-4 mt-4">
                {selectedWorkOrder ? (
                  <>
                    <Card>
                      <CardHeader>
                        <CardTitle className="text-lg">Work Order Information</CardTitle>
                        <CardDescription>WO# {selectedWorkOrder.workOrderNumber}</CardDescription>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        <div className="grid grid-cols-3 gap-4">
                          <div>
                            <Label className="text-sm font-semibold">Status</Label>
                            <Badge className="mt-1">{selectedWorkOrder.status}</Badge>
                          </div>
                          <div>
                            <Label className="text-sm font-semibold">Assigned To</Label>
                            <p className="text-base mt-1">{selectedWorkOrder.assignedTechnicianName || 'Unassigned'}</p>
                          </div>
                          <div>
                            <Label className="text-sm font-semibold">Completion</Label>
                            <p className="text-base mt-1">{selectedWorkOrder.completionPercentage || 0}%</p>
                          </div>
                        </div>

                        <div className="grid grid-cols-2 gap-4 border-t pt-4">
                          <div>
                            <Label className="text-sm font-semibold">Estimated Hours</Label>
                            <p className="text-xl font-bold mt-1">{selectedWorkOrder.estimatedHours}</p>
                          </div>
                          <div>
                            <Label className="text-sm font-semibold">Actual Hours</Label>
                            <p className="text-xl font-bold mt-1">{selectedWorkOrder.actualHours}</p>
                          </div>
                          <div>
                            <Label className="text-sm font-semibold">Estimated Cost</Label>
                            <p className="text-xl font-bold mt-1">${selectedWorkOrder.estimatedCost.toFixed(2)}</p>
                          </div>
                          <div>
                            <Label className="text-sm font-semibold">Actual Cost</Label>
                            <p className="text-xl font-bold mt-1">${selectedWorkOrder.actualCost.toFixed(2)}</p>
                          </div>
                        </div>

                        {selectedWorkOrder.actualStartDate && (
                          <div className="border-t pt-4">
                            <Label className="text-sm font-semibold">Actual Start Date</Label>
                            <p className="text-base mt-1">{format(new Date(selectedWorkOrder.actualStartDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                          </div>
                        )}

                        {selectedWorkOrder.actualCompletionDate && (
                          <div>
                            <Label className="text-sm font-semibold">Actual Completion Date</Label>
                            <p className="text-base mt-1">{format(new Date(selectedWorkOrder.actualCompletionDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                          </div>
                        )}
                      </CardContent>
                    </Card>

                    {selectedWorkOrder.tasks && selectedWorkOrder.tasks.length > 0 && (
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-lg">Work Order Tasks</CardTitle>
                        </CardHeader>
                        <CardContent>
                          <div className="space-y-2">
                            {selectedWorkOrder.tasks.map((task) => (
                              <div key={task.id} className="flex items-center justify-between border rounded-lg p-3">
                                <div className="flex-1">
                                  <p className="font-medium text-sm">{task.taskName}</p>
                                  {task.description && (
                                    <p className="text-xs text-muted-foreground">{task.description}</p>
                                  )}
                                </div>
                                <Badge>{task.status}</Badge>
                              </div>
                            ))}
                          </div>
                        </CardContent>
                      </Card>
                    )}
                  </>
                ) : (
                  <Card>
                    <CardContent className="py-8 text-center text-muted-foreground">
                      No work order has been generated yet.
                    </CardContent>
                  </Card>
                )}
              </TabsContent>

              {/* QC INSPECTION TAB */}
              <TabsContent value="qc" className="space-y-4 mt-4">
                {selectedQCInspection ? (
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">Quality Control Inspection Results</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="grid grid-cols-3 gap-4">
                        <div>
                          <Label className="text-sm font-semibold">Overall Result</Label>
                          <div className="mt-1">
                            <Badge style={selectedQCInspection.overallResult === 'Pass' ? { backgroundColor: '#d1fae5', color: '#065f46', fontSize: '16px', padding: '8px 12px' } : { backgroundColor: '#fee2e2', color: '#991b1b', fontSize: '16px', padding: '8px 12px' }}>
                              {selectedQCInspection.overallResult}
                            </Badge>
                          </div>
                        </div>
                        <div>
                          <Label className="text-sm font-semibold">Quality Score</Label>
                          <p className="text-3xl font-bold mt-1">{selectedQCInspection.score}%</p>
                        </div>
                        <div>
                          <Label className="text-sm font-semibold">Inspection Date</Label>
                          <p className="text-base mt-1">{format(new Date(selectedQCInspection.inspectionDate), 'MMM dd, yyyy HH:mm:ss')}</p>
                        </div>
                      </div>

                      <div className="border-t pt-4">
                        <Label className="text-sm font-semibold">Inspector</Label>
                        <p className="text-base mt-1">{selectedQCInspection.inspectorName || 'Unknown'}</p>
                      </div>

                      {selectedQCInspection.notes && (
                        <div className="border-t pt-4">
                          <Label className="text-sm font-semibold">Inspection Notes</Label>
                          <p className="text-base mt-1 bg-gray-50 p-3 rounded">{selectedQCInspection.notes}</p>
                        </div>
                      )}

                      {selectedQCInspection.overallResult === 'Pass' && (
                        <div className="border-t pt-4">
                          <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                            <div className="flex items-center justify-between">
                              <div className="flex items-center gap-2">
                                <CheckCircle className="h-6 w-6 text-green-600" />
                                <div>
                                  <p className="font-semibold text-green-900">Quality Certificate Generated</p>
                                  <p className="text-sm text-green-700">This work order has passed quality inspection and a certificate has been generated.</p>
                                </div>
                              </div>
                              <Button
                                variant="outline"
                                size="sm"
                                className="bg-white hover:bg-green-50"
                                onClick={async () => {
                                  try {
                                    // Open certificate in new window
                                    const baseUrl = 'http://localhost:5000';
                                    window.open(`${baseUrl}/api/maintenance/quality-control/certificate/${selectedQCInspection.id}`, '_blank');
                                    toast({
                                      title: "Opening Certificate",
                                      description: "Certificate is opening in a new window",
                                      className: "bg-green-50 border-green-200",
                                    });
                                  } catch (error) {
                                    console.error('Error opening certificate:', error);
                                    toast({
                                      title: "Error",
                                      description: "Failed to open certificate",
                                      variant: "destructive",
                                    });
                                  }
                                }}
                              >
                                <Eye className="h-4 w-4 mr-2" />
                                View Certificate
                              </Button>
                            </div>
                          </div>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                ) : (
                  <Card>
                    <CardContent className="py-8 text-center text-muted-foreground">
                      QC inspection not yet performed.
                    </CardContent>
                  </Card>
                )}
              </TabsContent>
            </Tabs>
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

