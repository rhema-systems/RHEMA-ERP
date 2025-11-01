import React, { useState, useEffect } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Grid,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Chip,
  IconButton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Tabs,
  Tab,
  Badge,
  Tooltip,
  Avatar,
  AvatarGroup,
  LinearProgress,
  Stepper,
  Step,
  StepLabel,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  List,
  ListItem,
  ListItemText,
  ListItemAvatar,
  ListItemSecondaryAction,
  Divider
} from '@mui/material';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Visibility as ViewIcon,
  Assignment as AssignmentIcon,
  Schedule as ScheduleIcon,
  Person as PersonIcon,
  CheckCircle as CheckCircleIcon,
  Cancel as CancelIcon,
  PlayArrow as PlayIcon,
  Pause as PauseIcon,
  Stop as StopIcon,
  Timeline as TimelineIcon,
  Build as BuildIcon,
  Warning as WarningIcon,
  Info as InfoIcon,
  ExpandMore as ExpandMoreIcon,
  AttachFile as AttachFileIcon,
  Comment as CommentIcon,
  QrCode as QrCodeIcon,
  Print as PrintIcon,
  PhotoCamera as PhotoCameraIcon,
  Task as TaskIcon,
  Group as GroupIcon,
  Input as AdmitIcon,
  Output as DischargeIcon,
  LocalHospital as HospitalIcon,
  Assessment as AssessmentIcon,
  VerifiedUser as CertifiedIcon
} from '@mui/icons-material';
import { WorkflowStatus } from '../system/WorkflowStatus';
import { notificationService } from '../../services/notificationService';
import assetAdmissionService from '../../services/assetAdmissionService';
import { workOrderService } from '../../services/workOrderService';
import { useToast } from '../../hooks/use-toast';

interface WorkOrder {
  id: string;
  workOrderNumber: string;
  title: string;
  description: string;
  jobCardId?: string;
  jobCardNumber?: string;
  assetId: string;
  assetName: string;
  workOrderType: string;
  maintenanceType: string;
  priority: string;
  status: string;
  assignedTechnician: string;
  assignedTeam?: string;
  scheduledStartDate?: Date;
  scheduledEndDate?: Date;
  actualStartDate?: Date;
  actualEndDate?: Date;
  estimatedHours: number;
  actualHours: number;
  estimatedCost: number;
  actualCost: number;
  workflowInstanceId?: string;
  completionPercentage: number;
  safetyIncident: boolean;
  qualityCheckRequired: boolean;
  qualityCheckPassed?: boolean;
  // Asset Admission/Discharge workflow
  admissionId?: string;
  admissionNumber?: string;
  admissionStatus?: 'NotStarted' | 'Admitted' | 'InMaintenance' | 'Completed' | 'Discharged';
  admissionDate?: Date;
  dischargeId?: string;
  dischargeNumber?: string;
  dischargeDate?: Date;
  requiresAdmission?: boolean;
  assetConditionOnAdmission?: string;
  assetConditionOnDischarge?: string;
}

interface WorkOrderTask {
  id: string;
  taskName: string;
  description: string;
  status: string;
  assignedTechnician: string;
  estimatedHours: number;
  actualHours: number;
  completedAt?: Date;
  isRequired: boolean;
}

interface WorkOrderPart {
  id: string;
  itemCode: string;
  itemName: string;
  quantityRequired: number;
  quantityUsed: number;
  unitCost: number;
  totalCost: number;
  status: string;
}

const WorkOrderManagement: React.FC = () => {
  const [activeTab, setActiveTab] = useState(0);
  const [workOrders, setWorkOrders] = useState<WorkOrder[]>([]);
  const [selectedWorkOrder, setSelectedWorkOrder] = useState<WorkOrder | null>(null);
  const [openDialog, setOpenDialog] = useState(false);
  const [dialogMode, setDialogMode] = useState<'create' | 'edit' | 'view'>('view');
  const [workOrderTasks, setWorkOrderTasks] = useState<WorkOrderTask[]>([]);
  const [workOrderParts, setWorkOrderParts] = useState<WorkOrderPart[]>([]);
  
  // Filter states
  const [filterStatus, setFilterStatus] = useState('all');
  const [filterPriority, setFilterPriority] = useState('all');
  const [filterTechnician, setFilterTechnician] = useState('all');
  
  // Loading states for actions
  const [approveLoading, setApproveLoading] = useState<string | null>(null);
  
  // Toast notifications
  const { toast } = useToast();

  useEffect(() => {
    fetchWorkOrders();
  }, [activeTab, filterStatus, filterPriority, filterTechnician]);

  const fetchWorkOrders = async () => {
    try {
      // Load work orders from localStorage (generated from job cards)
      const generatedWorkOrders = JSON.parse(localStorage.getItem('mockWorkOrders') || '[]');
      
      // Mock data - replace with actual API calls
      const mockWorkOrders: WorkOrder[] = [
        {
          id: '1',
          workOrderNumber: 'WO-2024-001',
          title: 'HVAC System Preventive Maintenance',
          description: 'Quarterly maintenance of HVAC system including filter replacement and inspection',
          jobCardId: 'JC-2024-001',
          assetId: 'HVAC-001',
          assetName: 'Main Building HVAC Unit 1',
          workOrderType: 'Scheduled',
          maintenanceType: 'Preventive',
          priority: 'Medium',
          status: 'InProgress',
          assignedTechnician: 'John Smith',
          assignedTeam: 'HVAC Team',
          scheduledStartDate: new Date('2024-10-15T08:00:00'),
          scheduledEndDate: new Date('2024-10-15T12:00:00'),
          actualStartDate: new Date('2024-10-15T08:15:00'),
          estimatedHours: 4,
          actualHours: 2.5,
          estimatedCost: 250,
          actualCost: 180,
          workflowInstanceId: 'wf-inst-001',
          completionPercentage: 65,
          safetyIncident: false,
          qualityCheckRequired: true,
          qualityCheckPassed: undefined,
          // Hospital-like workflow
          admissionId: 'ADM-2024-001',
          admissionNumber: 'ADM-2024-001',
          admissionStatus: 'InMaintenance',
          admissionDate: new Date('2024-10-15T07:30:00'),
          requiresAdmission: true,
          assetConditionOnAdmission: 'Fair - filters dirty, minor wear'
        },
        {
          id: '2',
          workOrderNumber: 'WO-2024-002',
          title: 'Emergency Elevator Repair',
          description: 'Elevator stopped working, requires immediate inspection and repair',
          assetId: 'ELEV-001',
          assetName: 'Building A Elevator 1',
          workOrderType: 'Emergency',
          maintenanceType: 'Emergency',
          priority: 'Critical',
          status: 'Assigned',
          assignedTechnician: 'Mike Johnson',
          scheduledStartDate: new Date('2024-10-15T14:00:00'),
          estimatedHours: 8,
          actualHours: 0,
          estimatedCost: 1200,
          actualCost: 0,
          completionPercentage: 0,
          safetyIncident: false,
          qualityCheckRequired: true,
          // Hospital-like workflow
          admissionStatus: 'NotStarted',
          requiresAdmission: true
        },
        {
          id: '3',
          workOrderNumber: 'WO-2024-003',
          title: 'Generator Monthly Service',
          description: 'Monthly preventive maintenance of backup generator',
          assetId: 'GEN-001',
          assetName: 'Emergency Generator Unit 1',
          workOrderType: 'Scheduled',
          maintenanceType: 'Preventive',
          priority: 'Medium',
          status: 'Completed',
          assignedTechnician: 'Sarah Wilson',
          scheduledStartDate: new Date('2024-10-14T09:00:00'),
          scheduledEndDate: new Date('2024-10-14T11:00:00'),
          actualStartDate: new Date('2024-10-14T09:00:00'),
          actualEndDate: new Date('2024-10-14T10:45:00'),
          estimatedHours: 2,
          actualHours: 1.75,
          estimatedCost: 150,
          actualCost: 135,
          completionPercentage: 100,
          safetyIncident: false,
          qualityCheckRequired: true,
          qualityCheckPassed: true,
          // Hospital-like workflow - completed cycle
          admissionId: 'ADM-2024-002',
          admissionNumber: 'ADM-2024-002',
          admissionStatus: 'Completed',
          admissionDate: new Date('2024-10-14T08:30:00'),
          dischargeId: 'DIS-2024-001',
          dischargeNumber: 'DIS-2024-001',
          dischargeDate: new Date('2024-10-14T11:00:00'),
          requiresAdmission: true,
          assetConditionOnAdmission: 'Good - routine maintenance due',
          assetConditionOnDischarge: 'Excellent - all systems checked and serviced'
        }
      ];
      
      // Combine generated work orders from job cards with mock work orders
      const allWorkOrders = [...generatedWorkOrders, ...mockWorkOrders];
      setWorkOrders(allWorkOrders);
      
      console.log(`📋 Loaded ${allWorkOrders.length} work orders (${generatedWorkOrders.length} generated from job cards)`);
    } catch (error) {
      console.error('Failed to fetch work orders:', error);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'assigned': return 'info';
      case 'inprogress': return 'warning';
      case 'onhold': return 'secondary';
      case 'completed': return 'success';
      case 'cancelled': return 'error';
      // Admission/discharge statuses
      case 'pendingadmission': return 'info';
      case 'admitted': return 'primary';
      case 'inmaintenance': return 'warning';
      case 'discharged': return 'success';
      default: return 'default';
    }
  };
  
  const getAdmissionStatusColor = (admissionStatus?: string) => {
    if (!admissionStatus) return 'default';
    switch (admissionStatus.toLowerCase()) {
      case 'notstarted': return 'default';
      case 'admitted': return 'primary';
      case 'inmaintenance': return 'warning';
      case 'completed': return 'info';
      case 'discharged': return 'success';
      default: return 'default';
    }
  };
  
  const getWorkflowStep = (workOrder: WorkOrder): number => {
    // Hospital-like workflow steps:
    // 0: Work Order Created
    // 1: Asset Admitted
    // 2: Maintenance In Progress
    // 3: Work Completed
    // 4: Quality Check
    // 5: Asset Discharged
    // 6: Certificate Issued
    
    if (workOrder.admissionStatus === 'Discharged') {
      return workOrder.qualityCheckPassed ? 6 : 5;
    }
    if (workOrder.status === 'Completed') {
      return workOrder.qualityCheckRequired ? 4 : 3;
    }
    if (workOrder.status === 'InProgress') {
      return 2;
    }
    if (workOrder.admissionStatus === 'Admitted' || workOrder.admissionStatus === 'InMaintenance') {
      return 1;
    }
    return 0; // Work Order Created
  };

  const getPriorityColor = (priority: string) => {
    switch (priority.toLowerCase()) {
      case 'critical': return 'error';
      case 'high': return 'warning';
      case 'medium': return 'info';
      case 'low': return 'success';
      default: return 'default';
    }
  };

  const handleViewWorkOrder = (workOrder: WorkOrder) => {
    setSelectedWorkOrder(workOrder);
    setDialogMode('view');
    setOpenDialog(true);
    
    // Fetch work order details
    fetchWorkOrderDetails(workOrder.id);
  };

  const fetchWorkOrderDetails = async (workOrderId: string) => {
    try {
      // Fetch real work order data from API
      const workOrderData = await workOrderService.getWorkOrderById(workOrderId);
      
      // Map tasks from API response
      const tasks: WorkOrderTask[] = workOrderData.tasks?.map(task => ({
        id: task.id,
        taskName: task.taskName,
        description: task.description || '',
        status: task.status,
        assignedTechnician: task.assignedTechnician?.fullName || 'Unassigned',
        estimatedHours: task.estimatedHours,
        actualHours: task.actualHours,
        completedAt: task.completedAt ? new Date(task.completedAt) : undefined,
        isRequired: task.isRequired
      })) || [];
      
      setWorkOrderTasks(tasks);
    } catch (error) {
      console.error('Error fetching work order details:', error);
      toast({
        title: 'Error',
        description: 'Failed to load work order details',
        variant: 'destructive'
      });
      // Fallback to generating tasks if API fails
      const workOrder = workOrders.find(wo => wo.id === workOrderId);
      if (workOrder) {
        const tasks = generateTasksForWorkOrder(workOrder);
        setWorkOrderTasks(tasks);
      }
    }

    setWorkOrderParts([
      {
        id: '1',
        itemCode: 'FILT-001',
        itemName: 'HVAC Air Filter 20x20x1',
        quantityRequired: 4,
        quantityUsed: 4,
        unitCost: 15.50,
        totalCost: 62.00,
        status: 'Used'
      },
      {
        id: '2',
        itemCode: 'REF-R410A',
        itemName: 'R410A Refrigerant',
        quantityRequired: 2,
        quantityUsed: 0,
        unitCost: 45.00,
        totalCost: 90.00,
        status: 'Allocated'
      }
    });
  };
  
  const generateTasksForWorkOrder = (workOrder: WorkOrder): WorkOrderTask[] => {
    const baseTaskId = parseInt(workOrder.id) * 100; // Ensure unique IDs
    
    switch (workOrder.maintenanceType.toLowerCase()) {
      case 'preventive':
      case 'preventative':
        return [
          {
            id: `${baseTaskId + 1}`,
            taskName: 'Pre-Maintenance Safety Check',
            description: 'Perform safety assessment and lockout/tagout procedures if required',
            status: 'Completed',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.25,
            actualHours: 0.2,
            completedAt: new Date(),
            isRequired: true
          },
          {
            id: `${baseTaskId + 2}`,
            taskName: 'Equipment Inspection',
            description: `Inspect ${workOrder.assetName} for wear, damage, and proper operation`,
            status: workOrder.completionPercentage > 40 ? 'Completed' : 'InProgress',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 1.0,
            actualHours: workOrder.completionPercentage > 40 ? 0.9 : 0.6,
            completedAt: workOrder.completionPercentage > 40 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 3}`,
            taskName: 'Preventive Maintenance Tasks',
            description: 'Perform scheduled maintenance tasks as per manufacturer recommendations',
            status: workOrder.completionPercentage > 70 ? 'Completed' : workOrder.completionPercentage > 40 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: workOrder.estimatedHours * 0.6,
            actualHours: workOrder.completionPercentage > 70 ? workOrder.estimatedHours * 0.5 : workOrder.completionPercentage > 40 ? workOrder.estimatedHours * 0.3 : 0,
            completedAt: workOrder.completionPercentage > 70 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 4}`,
            taskName: 'Function Testing',
            description: 'Test equipment operation and verify all functions work properly',
            status: workOrder.completionPercentage > 90 ? 'Completed' : workOrder.completionPercentage > 70 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.5,
            actualHours: workOrder.completionPercentage > 90 ? 0.4 : workOrder.completionPercentage > 70 ? 0.2 : 0,
            completedAt: workOrder.completionPercentage > 90 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 5}`,
            taskName: 'Documentation and Cleanup',
            description: 'Complete maintenance records and clean work area',
            status: workOrder.status === 'Completed' ? 'Completed' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.25,
            actualHours: workOrder.status === 'Completed' ? 0.2 : 0,
            completedAt: workOrder.status === 'Completed' ? new Date() : undefined,
            isRequired: true
          }
        ];
        
      case 'corrective':
      case 'repair':
        return [
          {
            id: `${baseTaskId + 1}`,
            taskName: 'Problem Diagnosis',
            description: 'Diagnose the root cause of the reported problem',
            status: workOrder.completionPercentage > 20 ? 'Completed' : 'InProgress',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.5,
            actualHours: workOrder.completionPercentage > 20 ? 0.6 : 0.3,
            completedAt: workOrder.completionPercentage > 20 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 2}`,
            taskName: 'Repair Work',
            description: 'Perform necessary repairs to fix the identified problem',
            status: workOrder.completionPercentage > 70 ? 'Completed' : workOrder.completionPercentage > 20 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: workOrder.estimatedHours * 0.7,
            actualHours: workOrder.completionPercentage > 70 ? workOrder.estimatedHours * 0.6 : workOrder.completionPercentage > 20 ? workOrder.estimatedHours * 0.4 : 0,
            completedAt: workOrder.completionPercentage > 70 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 3}`,
            taskName: 'Repair Verification',
            description: 'Test and verify that the repair has resolved the problem',
            status: workOrder.completionPercentage > 90 ? 'Completed' : workOrder.completionPercentage > 70 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.25,
            actualHours: workOrder.completionPercentage > 90 ? 0.3 : workOrder.completionPercentage > 70 ? 0.1 : 0,
            completedAt: workOrder.completionPercentage > 90 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 4}`,
            taskName: 'Final Documentation',
            description: 'Document repair actions taken and update maintenance records',
            status: workOrder.status === 'Completed' ? 'Completed' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.25,
            actualHours: workOrder.status === 'Completed' ? 0.2 : 0,
            completedAt: workOrder.status === 'Completed' ? new Date() : undefined,
            isRequired: true
          }
        ];
        
      case 'emergency':
        return [
          {
            id: `${baseTaskId + 1}`,
            taskName: 'Emergency Response',
            description: 'Respond to emergency situation and ensure safety',
            status: workOrder.completionPercentage > 10 ? 'Completed' : 'InProgress',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.5,
            actualHours: workOrder.completionPercentage > 10 ? 0.4 : 0.2,
            completedAt: workOrder.completionPercentage > 10 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 2}`,
            taskName: 'Emergency Repair',
            description: 'Perform emergency repair to restore functionality',
            status: workOrder.completionPercentage > 80 ? 'Completed' : workOrder.completionPercentage > 10 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: workOrder.estimatedHours * 0.8,
            actualHours: workOrder.completionPercentage > 80 ? workOrder.estimatedHours * 0.7 : workOrder.completionPercentage > 10 ? workOrder.estimatedHours * 0.4 : 0,
            completedAt: workOrder.completionPercentage > 80 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 3}`,
            taskName: 'Safety Verification',
            description: 'Verify equipment is safe for operation and meets safety standards',
            status: workOrder.status === 'Completed' ? 'Completed' : workOrder.completionPercentage > 80 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.25,
            actualHours: workOrder.status === 'Completed' ? 0.3 : workOrder.completionPercentage > 80 ? 0.1 : 0,
            completedAt: workOrder.status === 'Completed' ? new Date() : undefined,
            isRequired: true
          }
        ];
        
      default: // Standard maintenance
        return [
          {
            id: `${baseTaskId + 1}`,
            taskName: 'Pre-Work Assessment',
            description: 'Assess work requirements and prepare necessary tools and materials',
            status: workOrder.completionPercentage > 15 ? 'Completed' : 'InProgress',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.25,
            actualHours: workOrder.completionPercentage > 15 ? 0.3 : 0.1,
            completedAt: workOrder.completionPercentage > 15 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 2}`,
            taskName: 'Maintenance Work',
            description: 'Perform the required maintenance tasks',
            status: workOrder.completionPercentage > 80 ? 'Completed' : workOrder.completionPercentage > 15 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: workOrder.estimatedHours * 0.7,
            actualHours: workOrder.completionPercentage > 80 ? workOrder.estimatedHours * 0.6 : workOrder.completionPercentage > 15 ? workOrder.estimatedHours * 0.3 : 0,
            completedAt: workOrder.completionPercentage > 80 ? new Date() : undefined,
            isRequired: true
          },
          {
            id: `${baseTaskId + 3}`,
            taskName: 'Quality Check',
            description: 'Verify work completion and quality standards',
            status: workOrder.status === 'Completed' ? 'Completed' : workOrder.completionPercentage > 80 ? 'InProgress' : 'Pending',
            assignedTechnician: workOrder.assignedTechnician,
            estimatedHours: 0.25,
            actualHours: workOrder.status === 'Completed' ? 0.2 : workOrder.completionPercentage > 80 ? 0.1 : 0,
            completedAt: workOrder.status === 'Completed' ? new Date() : undefined,
            isRequired: true
          }
        ];
    }
  };

  const handleStatusChange = async (workOrderId: string, newStatus: string) => {
    try {
      const workOrder = workOrders.find(wo => wo.id === workOrderId);
      
      // API call to update work order status
      const updatedWorkOrders = workOrders.map(wo => 
        wo.id === workOrderId ? { ...wo, status: newStatus } : wo
      );
      setWorkOrders(updatedWorkOrders);
      
      // Send appropriate notifications based on status change
      if (workOrder) {
        switch (newStatus) {
          case 'InProgress':
            // Notify that work order has started
            await notificationService.createNotification({
              roles: ['MaintenanceManager'],
              title: 'Work Order Started',
              message: `Work Order ${workOrder.workOrderNumber} for ${workOrder.assetName} has been started.`,
              type: 'Info',
              category: 'WorkOrder',
              entityId: workOrderId,
              entityType: 'WorkOrder',
              actionUrl: `/maintenance/work-orders?id=${workOrderId}`
            });
            break;
            
          case 'Completed':
            // Check if quality control is required
            if (workOrder.qualityCheckRequired) {
              await notificationService.notifyQualityControlRequired(
                workOrderId, 
                workOrder.workOrderNumber, 
                workOrder.assetName
              );
            } else {
              // Notify completion
              await notificationService.notifyWorkOrderCompletion(
                workOrderId,
                workOrder.workOrderNumber,
                workOrder.assetName
              );
            }
            break;
            
          case 'OnHold':
            // Notify work order is on hold
            await notificationService.createNotification({
              roles: ['MaintenanceManager', 'MaintenanceSupervisor'],
              title: 'Work Order On Hold',
              message: `Work Order ${workOrder.workOrderNumber} for ${workOrder.assetName} has been put on hold.`,
              type: 'Warning',
              category: 'WorkOrder',
              entityId: workOrderId,
              entityType: 'WorkOrder',
              actionUrl: `/maintenance/work-orders?id=${workOrderId}`
            });
            break;
        }
      }
    } catch (error) {
      console.error('Failed to update work order status:', error);
    }
  };

  const handleAssetAdmission = async (workOrder: WorkOrder) => {
    try {
      // Create asset admission
      const admissionData = {
        workOrderId: workOrder.id,
        assetId: workOrder.assetId,
        assetName: workOrder.assetName,
        admissionReason: `Maintenance required for Work Order ${workOrder.workOrderNumber}`,
        conditionOnAdmission: 'To be assessed',
        estimatedCompletionDate: workOrder.scheduledEndDate?.toISOString().split('T')[0] || '',
        assignedTechnicianId: 'tech-001', // In real app, get from user selection
        priority: workOrder.priority,
        requiresShutdown: workOrder.maintenanceType === 'Emergency',
        safetyRequirements: workOrder.safetyIncident ? 'High safety protocols required' : 'Standard safety protocols'
      };
      
      const admission = await assetAdmissionService.createAssetAdmission(admissionData);
      
      // Update work order with admission info
      const updatedWorkOrders = workOrders.map(wo => 
        wo.id === workOrder.id ? {
          ...wo, 
          admissionId: admission.id,
          admissionNumber: admission.admissionNumber,
          admissionStatus: 'Admitted' as const,
          admissionDate: new Date(admission.admissionDate)
        } : wo
      );
      setWorkOrders(updatedWorkOrders);
      
      // Send notification
      await notificationService.notifyAssetAdmission(
        admission.id,
        admission.admissionNumber,
        workOrder.assetName
      );
      
    } catch (error) {
      console.error('Failed to admit asset:', error);
    }
  };
  
  const handleAssetDischarge = async (workOrder: WorkOrder) => {
    try {
      if (!workOrder.admissionId) {
        console.error('No admission record found for this work order');
        return;
      }
      
      // Create asset discharge
      const dischargeData = {
        admissionId: workOrder.admissionId,
        conditionOnDischarge: 'Good - maintenance completed successfully',
        workCompletedSummary: `Work Order ${workOrder.workOrderNumber} completed successfully`,
        maintenanceCertificateRequired: workOrder.qualityCheckRequired,
        dischargingTechnicianId: 'tech-001', // In real app, get from current user
        finalInspectionNotes: 'All systems tested and operational',
        followUpRequired: false
      };
      
      const discharge = await assetAdmissionService.dischargeAsset(workOrder.admissionId, dischargeData);
      
      // Update work order with discharge info
      const updatedWorkOrders = workOrders.map(wo => 
        wo.id === workOrder.id ? {
          ...wo, 
          dischargeId: discharge.id,
          dischargeNumber: discharge.dischargeNumber,
          admissionStatus: 'Discharged' as const,
          dischargeDate: new Date(discharge.dischargeDate),
          assetConditionOnDischarge: dischargeData.conditionOnDischarge
        } : wo
      );
      setWorkOrders(updatedWorkOrders);
      
      // Send notification
      await notificationService.notifyAssetDischarge(
        discharge.id,
        discharge.dischargeNumber,
        workOrder.assetName
      );
      
    } catch (error) {
      console.error('Failed to discharge asset:', error);
    }
  };
  
  const handleApproveWorkOrder = async (workOrderId: string) => {
    setApproveLoading(workOrderId);
    try {
      await workOrderService.approveWorkOrder(workOrderId);
      
      // Update local state
      setWorkOrders(prevOrders => 
        prevOrders.map(wo => 
          wo.id === workOrderId 
            ? {
                ...wo,
                status: 'Approved'
              }
            : wo
        )
      );

      // Also update selected work order if it's the same one
      if (selectedWorkOrder?.id === workOrderId) {
        setSelectedWorkOrder({
          ...selectedWorkOrder,
          status: 'Approved'
        });
      }

      toast({
        title: 'Success',
        description: 'Work order approved successfully',
        variant: 'success'
      });
    } catch (error) {
      console.error('Failed to approve work order:', error);
      toast({
        title: 'Error',
        description: 'Failed to approve work order',
        variant: 'destructive'
      });
    } finally {
      setApproveLoading(null);
    }
  };

  const WorkOrderDetails = ({ workOrder }: { workOrder: WorkOrder }) => (
    <Box>
      <Grid container spacing={3}>
        {/* Work Order Info */}
        <Grid item xs={12} md={8}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Work Order Information
              </Typography>
              <Grid container spacing={2}>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Work Order #</Typography>
                  <Typography variant="body1">{workOrder.workOrderNumber}</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Asset</Typography>
                  <Typography variant="body1">{workOrder.assetName}</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Type</Typography>
                  <Typography variant="body1">{workOrder.workOrderType}</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Priority</Typography>
                  <Chip 
                    size="small" 
                    label={workOrder.priority} 
                    color={getPriorityColor(workOrder.priority)} 
                  />
                </Grid>
                <Grid item xs={12}>
                  <Typography variant="body2" color="text.secondary">Description</Typography>
                  <Typography variant="body1">{workOrder.description}</Typography>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Grid>

        {/* Status & Progress */}
        <Grid item xs={12} md={4}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Status & Progress
              </Typography>
              <Box mb={2}>
                <Typography variant="body2" color="text.secondary">Status</Typography>
                <Chip 
                  label={workOrder.status} 
                  color={getStatusColor(workOrder.status)}
                />
              </Box>
              <Box mb={2}>
                <Typography variant="body2" color="text.secondary">Progress</Typography>
                <LinearProgress 
                  variant="determinate" 
                  value={workOrder.completionPercentage} 
                  sx={{ mt: 1, mb: 1 }}
                />
                <Typography variant="body2" align="center">
                  {workOrder.completionPercentage}% Complete
                </Typography>
              </Box>
              {workOrder.workflowInstanceId && (
                <WorkflowStatus workflowInstanceId={workOrder.workflowInstanceId} />
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Assignment & Schedule */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Assignment & Schedule
              </Typography>
              <Grid container spacing={2}>
                <Grid item xs={12}>
                  <Typography variant="body2" color="text.secondary">Assigned Technician</Typography>
                  <Box display="flex" alignItems="center" gap={1}>
                    <Avatar sx={{ width: 24, height: 24 }}>
                      {workOrder.assignedTechnician.split(' ').map(n => n[0]).join('')}
                    </Avatar>
                    <Typography variant="body1">{workOrder.assignedTechnician}</Typography>
                  </Box>
                </Grid>
                {workOrder.assignedTeam && (
                  <Grid item xs={12}>
                    <Typography variant="body2" color="text.secondary">Team</Typography>
                    <Typography variant="body1">{workOrder.assignedTeam}</Typography>
                  </Grid>
                )}
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Scheduled Start</Typography>
                  <Typography variant="body1">
                    {workOrder.scheduledStartDate?.toLocaleString()}
                  </Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Scheduled End</Typography>
                  <Typography variant="body1">
                    {workOrder.scheduledEndDate?.toLocaleString()}
                  </Typography>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Grid>

        {/* Time & Cost */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Time & Cost Tracking
              </Typography>
              <Grid container spacing={2}>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Est. Hours</Typography>
                  <Typography variant="body1">{workOrder.estimatedHours}h</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Actual Hours</Typography>
                  <Typography variant="body1">{workOrder.actualHours}h</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Est. Cost</Typography>
                  <Typography variant="body1">${workOrder.estimatedCost}</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="body2" color="text.secondary">Actual Cost</Typography>
                  <Typography variant="body1">${workOrder.actualCost}</Typography>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Grid>
        
        {/* Hospital-like Admission/Discharge Information */}
        {workOrder.requiresAdmission && (
          <Grid item xs={12}>
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom display="flex" alignItems="center" gap={1}>
                  <HospitalIcon color="primary" />
                  Asset Admission & Discharge
                </Typography>
                <Grid container spacing={2}>
                  <Grid item xs={12} md={3}>
                    <Typography variant="body2" color="text.secondary">Admission Status</Typography>
                    <Chip 
                      size="small" 
                      label={workOrder.admissionStatus || 'Not Started'}
                      color={getAdmissionStatusColor(workOrder.admissionStatus)}
                      icon={<HospitalIcon />}
                    />
                  </Grid>
                  
                  {workOrder.admissionNumber && (
                    <Grid item xs={12} md={3}>
                      <Typography variant="body2" color="text.secondary">Admission #</Typography>
                      <Typography variant="body1">{workOrder.admissionNumber}</Typography>
                    </Grid>
                  )}
                  
                  {workOrder.admissionDate && (
                    <Grid item xs={12} md={3}>
                      <Typography variant="body2" color="text.secondary">Admission Date</Typography>
                      <Typography variant="body1">
                        {workOrder.admissionDate.toLocaleString()}
                      </Typography>
                    </Grid>
                  )}
                  
                  {workOrder.dischargeDate && (
                    <Grid item xs={12} md={3}>
                      <Typography variant="body2" color="text.secondary">Discharge Date</Typography>
                      <Typography variant="body1">
                        {workOrder.dischargeDate.toLocaleString()}
                      </Typography>
                    </Grid>
                  )}
                  
                  {workOrder.assetConditionOnAdmission && (
                    <Grid item xs={12} md={6}>
                      <Typography variant="body2" color="text.secondary">Condition on Admission</Typography>
                      <Typography variant="body1">{workOrder.assetConditionOnAdmission}</Typography>
                    </Grid>
                  )}
                  
                  {workOrder.assetConditionOnDischarge && (
                    <Grid item xs={12} md={6}>
                      <Typography variant="body2" color="text.secondary">Condition on Discharge</Typography>
                      <Typography variant="body1">{workOrder.assetConditionOnDischarge}</Typography>
                    </Grid>
                  )}
                  
                  {/* Workflow Status Visualization */}
                  <Grid item xs={12}>
                    <Typography variant="body2" color="text.secondary" gutterBottom>
                      Workflow Progress
                    </Typography>
                    <Stepper activeStep={getWorkflowStep(workOrder)} alternativeLabel>
                      <Step>
                        <StepLabel icon={<AssignmentIcon />}>Work Order Created</StepLabel>
                      </Step>
                      <Step>
                        <StepLabel icon={<AdmitIcon />}>Asset Admitted</StepLabel>
                      </Step>
                      <Step>
                        <StepLabel icon={<BuildIcon />}>Maintenance In Progress</StepLabel>
                      </Step>
                      <Step>
                        <StepLabel icon={<CheckCircleIcon />}>Work Completed</StepLabel>
                      </Step>
                      <Step>
                        <StepLabel icon={<AssessmentIcon />}>Quality Check</StepLabel>
                      </Step>
                      <Step>
                        <StepLabel icon={<DischargeIcon />}>Asset Discharged</StepLabel>
                      </Step>
                      <Step>
                        <StepLabel icon={<CertifiedIcon />}>Certificate Issued</StepLabel>
                      </Step>
                    </Stepper>
                  </Grid>
                </Grid>
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* Tasks */}
        <Grid item xs={12}>
          <Accordion defaultExpanded>
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="h6">
                Tasks ({workOrderTasks.length})
              </Typography>
            </AccordionSummary>
            <AccordionDetails>
              <List>
                {workOrderTasks.map((task) => (
                  <ListItem key={task.id} divider>
                    <ListItemAvatar>
                      <Avatar sx={{ 
                        bgcolor: task.status === 'Completed' ? 'success.main' : 
                               task.status === 'InProgress' ? 'warning.main' : 'grey.400'
                      }}>
                        <TaskIcon />
                      </Avatar>
                    </ListItemAvatar>
                    <ListItemText
                      primary={task.taskName}
                      secondary={
                        <Box>
                          <Typography variant="body2">{task.description}</Typography>
                          <Typography variant="caption" display="block">
                            {task.assignedTechnician} • {task.actualHours}h / {task.estimatedHours}h
                          </Typography>
                        </Box>
                      }
                    />
                    <ListItemSecondaryAction>
                      <Chip 
                        size="small" 
                        label={task.status}
                        color={getStatusColor(task.status)}
                      />
                    </ListItemSecondaryAction>
                  </ListItem>
                ))}
              </List>
            </AccordionDetails>
          </Accordion>
        </Grid>

        {/* Parts & Materials */}
        <Grid item xs={12}>
          <Accordion>
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="h6">
                Parts & Materials ({workOrderParts.length})
              </Typography>
            </AccordionSummary>
            <AccordionDetails>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Item Code</TableCell>
                      <TableCell>Description</TableCell>
                      <TableCell>Qty Required</TableCell>
                      <TableCell>Qty Used</TableCell>
                      <TableCell>Unit Cost</TableCell>
                      <TableCell>Total Cost</TableCell>
                      <TableCell>Status</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {workOrderParts.map((part) => (
                      <TableRow key={part.id}>
                        <TableCell>{part.itemCode}</TableCell>
                        <TableCell>{part.itemName}</TableCell>
                        <TableCell>{part.quantityRequired}</TableCell>
                        <TableCell>{part.quantityUsed}</TableCell>
                        <TableCell>${part.unitCost}</TableCell>
                        <TableCell>${part.totalCost}</TableCell>
                        <TableCell>
                          <Chip size="small" label={part.status} />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </AccordionDetails>
          </Accordion>
        </Grid>
      </Grid>
    </Box>
  );

  return (
    <Box sx={{ p: 3 }}>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4" fontWeight="bold">
          Work Order Management
        </Typography>
        <Box display="flex" gap={2}>
          <Button
            variant="outlined"
            startIcon={<QrCodeIcon />}
          >
            Scan QR Code
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
          >
            Create Work Order
          </Button>
        </Box>
      </Box>

      {/* Filters */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid item xs={12} sm={3}>
          <FormControl fullWidth size="small">
            <InputLabel>Status</InputLabel>
            <Select
              value={filterStatus}
              onChange={(e) => setFilterStatus(e.target.value)}
            >
              <MenuItem value="all">All Statuses</MenuItem>
              <MenuItem value="assigned">Assigned</MenuItem>
              <MenuItem value="inprogress">In Progress</MenuItem>
              <MenuItem value="onhold">On Hold</MenuItem>
              <MenuItem value="completed">Completed</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid item xs={12} sm={3}>
          <FormControl fullWidth size="small">
            <InputLabel>Priority</InputLabel>
            <Select
              value={filterPriority}
              onChange={(e) => setFilterPriority(e.target.value)}
            >
              <MenuItem value="all">All Priorities</MenuItem>
              <MenuItem value="critical">Critical</MenuItem>
              <MenuItem value="high">High</MenuItem>
              <MenuItem value="medium">Medium</MenuItem>
              <MenuItem value="low">Low</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid item xs={12} sm={3}>
          <FormControl fullWidth size="small">
            <InputLabel>Technician</InputLabel>
            <Select
              value={filterTechnician}
              onChange={(e) => setFilterTechnician(e.target.value)}
            >
              <MenuItem value="all">All Technicians</MenuItem>
              <MenuItem value="john">John Smith</MenuItem>
              <MenuItem value="mike">Mike Johnson</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid item xs={12} sm={3}>
          <TextField
            fullWidth
            size="small"
            placeholder="Search work orders..."
          />
        </Grid>
      </Grid>

      <Tabs value={activeTab} onChange={(e, v) => setActiveTab(v)} sx={{ mb: 3 }}>
        <Tab 
          label={
            <Badge badgeContent={workOrders.filter(wo => wo.status === 'Assigned').length} color="info">
              Assigned
            </Badge>
          } 
        />
        <Tab 
          label={
            <Badge badgeContent={workOrders.filter(wo => wo.status === 'InProgress').length} color="warning">
              In Progress
            </Badge>
          } 
        />
        <Tab 
          label={
            <Badge badgeContent={workOrders.filter(wo => wo.status === 'OnHold').length} color="error">
              On Hold
            </Badge>
          } 
        />
        <Tab 
          label={
            <Badge badgeContent={workOrders.filter(wo => wo.status === 'Completed').length} color="success">
              Completed
            </Badge>
          } 
        />
        <Tab label="All" />
      </Tabs>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Work Order #</TableCell>
              <TableCell>Title</TableCell>
              <TableCell>Asset</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Priority</TableCell>
              <TableCell>WO Status</TableCell>
              <TableCell>Admission Status</TableCell>
              <TableCell>Assigned To</TableCell>
              <TableCell>Progress</TableCell>
              <TableCell>Schedule</TableCell>
              <TableCell>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {workOrders.map((workOrder) => (
              <TableRow key={workOrder.id} hover>
                <TableCell>{workOrder.workOrderNumber}</TableCell>
                <TableCell>
                  <Typography variant="body2" fontWeight="medium">
                    {workOrder.title}
                  </Typography>
                  {workOrder.jobCardNumber && (
                    <Typography variant="caption" color="text.secondary">
                      From Job Card: {workOrder.jobCardNumber}
                    </Typography>
                  )}
                </TableCell>
                <TableCell>{workOrder.assetName}</TableCell>
                <TableCell>{workOrder.workOrderType}</TableCell>
                <TableCell>
                  <Chip 
                    size="small" 
                    label={workOrder.priority} 
                    color={getPriorityColor(workOrder.priority)} 
                  />
                </TableCell>
                <TableCell>
                  <Chip 
                    size="small" 
                    label={workOrder.status} 
                    color={getStatusColor(workOrder.status)} 
                  />
                </TableCell>
                <TableCell>
                  <Box display="flex" alignItems="center" gap={1}>
                    <Chip 
                      size="small" 
                      label={workOrder.admissionStatus || 'N/A'}
                      color={getAdmissionStatusColor(workOrder.admissionStatus)}
                      icon={workOrder.requiresAdmission ? <HospitalIcon /> : undefined}
                    />
                    {workOrder.admissionNumber && (
                      <Typography variant="caption" color="text.secondary">
                        {workOrder.admissionNumber}
                      </Typography>
                    )}
                  </Box>
                </TableCell>
                <TableCell>
                  <Box display="flex" alignItems="center" gap={1}>
                    <Avatar sx={{ width: 24, height: 24, fontSize: '0.75rem' }}>
                      {workOrder.assignedTechnician.split(' ').map(n => n[0]).join('')}
                    </Avatar>
                    <Typography variant="body2">{workOrder.assignedTechnician}</Typography>
                  </Box>
                </TableCell>
                <TableCell>
                  <Box display="flex" alignItems="center" gap={1}>
                    <LinearProgress 
                      variant="determinate" 
                      value={workOrder.completionPercentage} 
                      sx={{ width: 60, height: 6, borderRadius: 3 }}
                    />
                    <Typography variant="caption">
                      {workOrder.completionPercentage}%
                    </Typography>
                  </Box>
                </TableCell>
                <TableCell>
                  {workOrder.scheduledStartDate && (
                    <Typography variant="caption">
                      {workOrder.scheduledStartDate.toLocaleDateString()}
                    </Typography>
                  )}
                </TableCell>
                <TableCell>
                  <Box display="flex" gap={0.5}>
                    <Tooltip title="View Details">
                      <IconButton size="small" onClick={() => handleViewWorkOrder(workOrder)}>
                        <ViewIcon />
                      </IconButton>
                    </Tooltip>
                    
                    {/* Hospital-like Admission/Discharge Workflow */}
                    {workOrder.requiresAdmission && workOrder.admissionStatus === 'NotStarted' && (
                      <Tooltip title="Admit Asset">
                        <IconButton 
                          size="small" 
                          color="primary"
                          onClick={() => handleAssetAdmission(workOrder)}
                        >
                          <AdmitIcon />
                        </IconButton>
                      </Tooltip>
                    )}
                    
                    {workOrder.admissionStatus === 'Admitted' && workOrder.status === 'Assigned' && (
                      <Tooltip title="Start Maintenance">
                        <IconButton 
                          size="small" 
                          color="warning"
                          onClick={() => handleStatusChange(workOrder.id, 'InProgress')}
                        >
                          <PlayIcon />
                        </IconButton>
                      </Tooltip>
                    )}
                    
                    {workOrder.status === 'InProgress' && (
                      <Tooltip title="Complete Work">
                        <IconButton 
                          size="small"
                          color="success"
                          onClick={() => handleStatusChange(workOrder.id, 'Completed')}
                        >
                          <CheckCircleIcon />
                        </IconButton>
                      </Tooltip>
                    )}
                    
                    {workOrder.status === 'Completed' && workOrder.status !== 'Approved' && (
                      <Tooltip title="Approve Work Order">
                        <IconButton 
                          size="small"
                          color="primary"
                          disabled={approveLoading === workOrder.id}
                          onClick={() => handleApproveWorkOrder(workOrder.id)}
                        >
                          {approveLoading === workOrder.id ? (
                            <Box sx={{ display: 'flex', alignItems: 'center' }}>
                              <Typography variant="caption">Loading...</Typography>
                            </Box>
                          ) : (
                            <CheckCircleIcon />
                          )}
                        </IconButton>
                      </Tooltip>
                    )}
                    
                    {workOrder.status === 'Completed' && 
                     workOrder.admissionStatus !== 'Discharged' && 
                     workOrder.admissionId && (
                      <Tooltip title="Discharge Asset">
                        <IconButton 
                          size="small" 
                          color="success"
                          onClick={() => handleAssetDischarge(workOrder)}
                        >
                          <DischargeIcon />
                        </IconButton>
                      </Tooltip>
                    )}
                    
                    {workOrder.admissionStatus === 'Discharged' && (
                      <Tooltip title="Generate Certificate">
                        <IconButton size="small" color="info">
                          <CertifiedIcon />
                        </IconButton>
                      </Tooltip>
                    )}
                  </Box>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Work Order Details Dialog */}
      <Dialog 
        open={openDialog} 
        onClose={() => setOpenDialog(false)} 
        maxWidth="lg" 
        fullWidth
      >
        <DialogTitle>
          <Box display="flex" justifyContent="space-between" alignItems="center">
            <Typography variant="h6">
              {selectedWorkOrder?.workOrderNumber} - {selectedWorkOrder?.title}
            </Typography>
            <Box display="flex" gap={1}>
              <IconButton><PrintIcon /></IconButton>
              <IconButton><PhotoCameraIcon /></IconButton>
              <IconButton><AttachFileIcon /></IconButton>
            </Box>
          </Box>
        </DialogTitle>
        <DialogContent>
          {selectedWorkOrder && (
            <Tabs value={0}>
              <Tab label="Overview" />
              <Tab label="Admission/Discharge" icon={<HospitalIcon />} />
              <Tab label="Tasks & Progress" />
              <Tab label="Parts & Materials" />
              <Tab label="Quality & Safety" />
              <Tab label="Documents" />
              <Tab label="History" />
            </Tabs>
          )}
          {selectedWorkOrder && <WorkOrderDetails workOrder={selectedWorkOrder} />}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDialog(false)}>
            Close
          </Button>
          
          {/* Hospital-like Workflow Actions */}
          {selectedWorkOrder?.requiresAdmission && selectedWorkOrder?.admissionStatus === 'NotStarted' && (
            <Button 
              variant="contained" 
              color="primary"
              startIcon={<AdmitIcon />}
              onClick={() => handleAssetAdmission(selectedWorkOrder)}
            >
              Admit Asset
            </Button>
          )}
          
          {selectedWorkOrder?.admissionStatus === 'Admitted' && selectedWorkOrder?.status === 'Assigned' && (
            <Button 
              variant="contained" 
              color="warning"
              startIcon={<PlayIcon />}
              onClick={() => handleStatusChange(selectedWorkOrder.id, 'InProgress')}
            >
              Start Maintenance
            </Button>
          )}
          
          {selectedWorkOrder?.status === 'InProgress' && (
            <Button 
              variant="contained" 
              color="success"
              startIcon={<CheckCircleIcon />}
              onClick={() => handleStatusChange(selectedWorkOrder.id, 'Completed')}
            >
              Complete Work
            </Button>
          )}
          
          {selectedWorkOrder?.status === 'Completed' && selectedWorkOrder?.status !== 'Approved' && (
            <Button 
              variant="contained" 
              color="primary"
              disabled={approveLoading === selectedWorkOrder.id}
              startIcon={approveLoading === selectedWorkOrder.id ? undefined : <CheckCircleIcon />}
              onClick={() => handleApproveWorkOrder(selectedWorkOrder.id)}
            >
              {approveLoading === selectedWorkOrder.id ? 'Approving...' : 'Approve Work Order'}
            </Button>
          )}
          
          {selectedWorkOrder?.status === 'Completed' && 
           selectedWorkOrder?.admissionStatus !== 'Discharged' && 
           selectedWorkOrder?.admissionId && (
            <Button 
              variant="contained" 
              color="success"
              startIcon={<DischargeIcon />}
              onClick={() => handleAssetDischarge(selectedWorkOrder)}
            >
              Discharge Asset
            </Button>
          )}
          
          {selectedWorkOrder?.admissionStatus === 'Discharged' && (
            <Button 
              variant="outlined" 
              color="info"
              startIcon={<CertifiedIcon />}
            >
              Generate Certificate
            </Button>
          )}
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default WorkOrderManagement;