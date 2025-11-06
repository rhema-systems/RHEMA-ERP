'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Calendar, DateRange } from '@/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { maintenanceDataService, Employee, Asset, MaintenanceType } from '@/services/maintenanceDataService';
import maintenanceScheduleService, { MaintenanceSchedule, CreateMaintenanceScheduleDto } from '@/services/maintenanceScheduleService';
import { 
  Calendar as CalendarIcon,
  Clock,
  Plus,
  Search,
  Filter,
  MoreHorizontal,
  Edit,
  Trash2,
  CheckCircle,
  AlertTriangle,
  Users,
  Package,
  RefreshCw
} from 'lucide-react';
import { format, addDays } from 'date-fns';
import { cn } from '@/lib/utils';
import { useToast } from '@/hooks/use-toast';
import { ClientOnly } from '@/components/ClientOnly';

interface ScheduledMaintenanceItem {
  id: string;
  title: string;
  assetId: string;
  assetName: string;
  type: string;
  frequency: string;
  nextDue: string;
  lastCompleted: string;
  assignedTechnician: string; // Technician name for display
  assignedTechnicianId: string; // Technician ID for API calls
  priority: string;
  status: string;
  estimatedHours: number;
  description: string;
  // Trigger fields
  primaryTriggerType?: string;
  secondaryTriggerType?: string;
  triggerLogic?: string;
  mileageTrigger?: number;
  operatingHoursTrigger?: number;
  cycleTrigger?: number;
  conditionCriteria?: string;
  // Notification fields
  advanceNotificationDays?: number;
  notificationRecipients?: string;
  autoGenerateWorkOrders?: boolean;
}

export default function ScheduledMaintenancePage() {
  const { toast } = useToast();
  const [scheduledMaintenanceData, setScheduledMaintenanceData] = useState<ScheduledMaintenanceItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isConfirmDialogOpen, setIsConfirmDialogOpen] = useState(false);
  const [editingSchedule, setEditingSchedule] = useState<ScheduledMaintenanceItem | null>(null);
  const [scheduleToGenerate, setScheduleToGenerate] = useState<ScheduledMaintenanceItem | null>(null);
  const [filteredData, setFilteredData] = useState<ScheduledMaintenanceItem[]>([]);
  
  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<MaintenanceType[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  
  // Form state for creating new scheduled maintenance
  const [formData, setFormData] = useState({
    title: '',
    assetId: '',
    type: '',
    frequency: '',
    nextDue: '',
    assignedTechnician: '',
    priority: 'Medium',
    estimatedHours: '',
    description: '',
    // Trigger fields
    primaryTriggerType: 'Time',
    secondaryTriggerType: '',
    triggerLogic: 'OR',
    mileageTrigger: '',
    operatingHoursTrigger: '',
    cycleTrigger: '',
    conditionCriteria: '',
    // Notification fields
    advanceNotificationDays: '',
    notificationRecipients: '',
    autoGenerateWorkOrders: true
  });

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      setLoading(true);
      try {
        console.log('=== LOADING MAINTENANCE DATA ===');
        const [techniciansList, assetsList, maintenanceTypesList, scheduledDataResponse] = await Promise.all([
          maintenanceDataService.getTechnicians(),
          maintenanceDataService.getAssets(),
          maintenanceDataService.getMaintenanceTypes(),
          maintenanceScheduleService.getSchedules()
        ]);
        
        console.log('=== TECHNICIANS DEBUG ===');
        console.log('Technicians loaded:', techniciansList.length, techniciansList);
        console.log('Technicians type:', typeof techniciansList, Array.isArray(techniciansList));
        
        console.log('=== OTHER DATA DEBUG ===');
        console.log('Assets loaded:', assetsList.length, assetsList);
        console.log('Maintenance types loaded:', maintenanceTypesList.length, maintenanceTypesList);
        
        if (techniciansList.length === 0) {
          console.warn('⚠️ No technicians loaded - this may indicate an API endpoint issue');
          toast({
            title: "Employee API Not Available",
            description: "Unable to load employee data. The technician dropdown will be empty. Please check the console for endpoint details and contact your system administrator.",
            variant: "destructive",
            duration: 8000
          });
        }
        
        setTechnicians(techniciansList);
        setAssets(assetsList);
        setMaintenanceTypes(maintenanceTypesList);
        
        console.log('Raw scheduled data response:', scheduledDataResponse);
        
        // The service returns PagedResult<MaintenanceSchedule>
        const schedules = scheduledDataResponse.items || scheduledDataResponse.data || [];
        console.log('Extracted schedules:', schedules);
        
        // Map MaintenanceSchedule to ScheduledMaintenanceItem interface
        const mappedSchedules = schedules.map((schedule: any) => ({
          id: schedule.id,
          title: schedule.name || schedule.title,
          assetId: schedule.assetId,
          assetName: schedule.assetName || 'Unknown Asset',
          type: schedule.maintenanceType || schedule.maintenanceTypeName || schedule.type || 'Unknown Type',
          frequency: schedule.frequency,
          nextDue: schedule.nextDueDate || schedule.nextDue,
          lastCompleted: schedule.lastCompletedDate || schedule.lastCompleted || '',
          assignedTechnician: schedule.assignedTechnicianName || schedule.assignedTechnician || 'Unassigned',
          assignedTechnicianId: schedule.assignedTechnicianId || '',
          priority: schedule.priority,
          status: schedule.isActive ? 'Scheduled' : 'Inactive',
          estimatedHours: schedule.estimatedHours || 0,
          description: schedule.description || '',
          // Store trigger and notification data
          primaryTriggerType: schedule.primaryTriggerType,
          secondaryTriggerType: schedule.secondaryTriggerType,
          triggerLogic: schedule.triggerLogic,
          mileageTrigger: schedule.mileageTrigger,
          operatingHoursTrigger: schedule.operatingHoursTrigger,
          cycleTrigger: schedule.cycleTrigger,
          conditionCriteria: schedule.conditionCriteria,
          advanceNotificationDays: schedule.advanceNotificationDays,
          notificationRecipients: schedule.notificationRecipients,
          autoGenerateWorkOrders: schedule.autoGenerateWorkOrders
        }));
        
        console.log('Mapped scheduled maintenance items:', mappedSchedules);
        setScheduledMaintenanceData(mappedSchedules);
      } catch (error) {
        console.error('Error loading data:', error);
        setScheduledMaintenanceData([]);
      } finally {
        setLoadingData(false);
        setLoading(false);
      }
    };
    
    loadData();
  }, []);

  useEffect(() => {
    console.log('🔍 Filtering data...');
    console.log('scheduledMaintenanceData:', scheduledMaintenanceData.length, scheduledMaintenanceData);
    console.log('Filters:', { searchTerm, statusFilter, priorityFilter, typeFilter });
    
    let filtered = scheduledMaintenanceData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.title?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assetName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.assignedTechnician?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item => item.status?.toLowerCase() === statusFilter.toLowerCase());
    }

    if (priorityFilter !== 'all') {
      filtered = filtered.filter(item => item.priority?.toLowerCase() === priorityFilter.toLowerCase());
    }

    if (typeFilter !== 'all') {
      filtered = filtered.filter(item => item.type?.toLowerCase() === typeFilter.toLowerCase());
    }

    console.log('🔍 Filtered result:', filtered.length, filtered);
    setFilteredData(filtered);
  }, [scheduledMaintenanceData, searchTerm, statusFilter, priorityFilter, typeFilter]);

  const getStatusBadge = (status: string) => {
    const colors = {
      'Scheduled': 'bg-blue-100 text-blue-800',
      'In Progress': 'bg-yellow-100 text-yellow-800',
      'Completed': 'bg-green-100 text-green-800',
      'Overdue': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[status] || 'bg-gray-100 text-gray-800'}>
        {status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>
        {priority}
      </Badge>
    );
  };

  const handleCreateSchedule = async () => {
    try {
      console.log('=== FORM VALIDATION START ===');
      console.log('Form data:', formData);
      console.log('Available maintenance types:', maintenanceTypes.length, maintenanceTypes);
      console.log('Available assets:', assets.length);
      
      // Validate required fields before sending
      if (!formData.title) {
        console.log('❌ Validation failed: No title');
        toast({
          title: "Validation Error",
          description: "Please enter a title for the scheduled maintenance.",
          variant: "destructive"
        });
        return;
      }
      console.log('✅ Title validation passed:', formData.title);
      
      if (!formData.assetId) {
        console.log('❌ Validation failed: No assetId');
        toast({
          title: "Validation Error",
          description: "Please select an asset.",
          variant: "destructive"
        });
        return;
      }
      console.log('✅ AssetId validation passed:', formData.assetId);
      
      if (!formData.type) {
        console.log('❌ Validation failed: No type');
        toast({
          title: "Validation Error",
          description: "Please specify the maintenance type.",
          variant: "destructive"
        });
        return;
      }
      console.log('✅ Type validation passed:', formData.type);
      
      if (!formData.frequency) {
        console.log('❌ Validation failed: No frequency');
        toast({
          title: "Validation Error",
          description: "Please select a frequency.",
          variant: "destructive"
        });
        return;
      }
      console.log('✅ Frequency validation passed:', formData.frequency);
      
      if (maintenanceTypes.length === 0) {
        console.log('❌ Validation failed: No maintenance types available');
        toast({
          title: "Configuration Error",
          description: "No maintenance types available. Please contact your administrator.",
          variant: "destructive"
        });
        return;
      }
      console.log('✅ Maintenance types available:', maintenanceTypes.length);
      
      console.log('=== ALL VALIDATIONS PASSED, PROCEEDING WITH API CALL ===');
      
      const token = localStorage.getItem('authToken');
      
      // Transform frontend data to match CreateMaintenanceScheduleDto
      const createDto = {
        name: formData.title,
        code: `SCH-${Date.now().toString().slice(-8)}${Math.random().toString(36).substr(2, 3)}`, // Generate a unique code under 20 chars
        description: formData.description,
        assetId: formData.assetId || '00000000-0000-0000-0000-000000000000',
        maintenanceTypeId: (() => {
          // First try exact match
          let foundType = maintenanceTypes.find(type => type.name === formData.type);
          
          // If not found, try case-insensitive match
          if (!foundType) {
            foundType = maintenanceTypes.find(type => type.name.toLowerCase() === formData.type.toLowerCase());
          }
          
          // If still not found, try partial match
          if (!foundType) {
            foundType = maintenanceTypes.find(type => type.name.toLowerCase().includes(formData.type.toLowerCase()) || formData.type.toLowerCase().includes(type.name.toLowerCase()));
          }
          
          if (foundType) {
            console.log('Found maintenance type:', foundType);
            return foundType.id;
          }
          
          console.warn('No maintenance type found for:', formData.type);
          console.warn('Available maintenance types:', maintenanceTypes.map(t => ({ id: t.id, name: t.name })));
          
          // Use first available type as fallback, but only if one exists
          if (maintenanceTypes.length > 0) {
            console.warn('Using first available maintenance type as fallback:', maintenanceTypes[0]);
            return maintenanceTypes[0].id;
          }
          
          // If no maintenance types available, this will cause validation error - which is correct
          console.error('NO MAINTENANCE TYPES AVAILABLE - This will cause a validation error');
          return '00000000-0000-0000-0000-000000000000'; // Return empty GUID instead of null
        })(),
        maintenanceType: formData.type,
        priority: formData.priority,
        frequency: formData.frequency,
        frequencyValue: 1, // Default value
        frequencyUnit: 'Days', // Default unit
        frequencyInterval: formData.frequency === 'Daily' ? 1 : 
                          formData.frequency === 'Weekly' ? 7 : 
                          formData.frequency === 'Monthly' ? 30 : 
                          formData.frequency === 'Quarterly' ? 90 : 
                          formData.frequency === 'Semi-Annual' ? 180 : 
                          formData.frequency === 'Annual' ? 365 : 30,
        startDate: new Date().toISOString(),
        nextDueDate: formData.nextDue ? new Date(formData.nextDue).toISOString() : new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
        estimatedDuration: 60, // Default to 60 minutes
        estimatedHours: parseFloat(formData.estimatedHours) || 1,
        estimatedCost: 0,
        assignedTechnicianId: formData.assignedTechnician && formData.assignedTechnician.trim() !== '' && formData.assignedTechnician !== '__UNASSIGNED__' ? formData.assignedTechnician : null,
        assignedTeamId: null,
        assignedTeam: '',
        assetCategory: assets.find(asset => asset.id === formData.assetId)?.categoryName || '',
        instructions: formData.description,
        safetyNotes: '',
        requiredSkills: [],
        requiredTools: [],
        requiredParts: [],
        isActive: true,
        autoCreate: true,
        autoGenerateWorkOrders: formData.autoGenerateWorkOrders,
        leadTime: 5,
        advanceNotificationDays: formData.advanceNotificationDays ? parseInt(formData.advanceNotificationDays) : 7,
        notificationRecipients: formData.notificationRecipients || null,
        maxDelayDays: 3,
        notes: formData.description,
        // Trigger fields
        primaryTriggerType: formData.primaryTriggerType,
        secondaryTriggerType: formData.secondaryTriggerType || null,
        triggerLogic: formData.primaryTriggerType === 'Combined' ? formData.triggerLogic : null,
        mileageTrigger: formData.mileageTrigger ? parseFloat(formData.mileageTrigger) : null,
        operatingHoursTrigger: formData.operatingHoursTrigger ? parseFloat(formData.operatingHoursTrigger) : null,
        cycleTrigger: formData.cycleTrigger ? parseInt(formData.cycleTrigger) : null,
        conditionCriteria: formData.conditionCriteria || null
      };
      
      console.log('Sending createDto:', createDto);
      
      // Use service instead of fetch
      const responseData = await maintenanceScheduleService.createSchedule(createDto as CreateMaintenanceScheduleDto);
      console.log('Response data:', responseData);
      
      toast({
        title: "Success!",
        description: "Scheduled maintenance created successfully.",
        variant: "success"
      });
      
      // Refresh the data using service
      const scheduledDataResponse = await maintenanceScheduleService.getSchedules();
      const schedules = scheduledDataResponse.items || scheduledDataResponse.data || [];
      const mappedSchedules = schedules.map((schedule: any) => ({
        id: schedule.id,
        title: schedule.name || schedule.title,
        assetId: schedule.assetId,
        assetName: schedule.assetName || 'Unknown Asset',
        type: schedule.maintenanceType || schedule.maintenanceTypeName || schedule.type || 'Unknown Type',
        frequency: schedule.frequency,
        nextDue: schedule.nextDueDate || schedule.nextDue,
        lastCompleted: schedule.lastCompletedDate || schedule.lastCompleted || '',
        assignedTechnician: schedule.assignedTechnicianName || schedule.assignedTechnician || 'Unassigned',
        assignedTechnicianId: schedule.assignedTechnicianId || '',
        priority: schedule.priority,
        status: schedule.isActive ? 'Scheduled' : 'Inactive',
        estimatedHours: schedule.estimatedHours || 0,
        description: schedule.description || '',
        primaryTriggerType: schedule.primaryTriggerType,
        secondaryTriggerType: schedule.secondaryTriggerType,
        triggerLogic: schedule.triggerLogic,
        mileageTrigger: schedule.mileageTrigger,
        operatingHoursTrigger: schedule.operatingHoursTrigger,
        cycleTrigger: schedule.cycleTrigger,
        conditionCriteria: schedule.conditionCriteria,
        advanceNotificationDays: schedule.advanceNotificationDays,
        notificationRecipients: schedule.notificationRecipients,
        autoGenerateWorkOrders: schedule.autoGenerateWorkOrders
      }));
      
      setScheduledMaintenanceData(mappedSchedules);
      
      setIsCreateDialogOpen(false);
      // Reset form
      setFormData({
        title: '',
        assetId: '',
        type: '',
        frequency: '',
        nextDue: '',
        assignedTechnician: '',
        priority: 'Medium',
        estimatedHours: '',
        description: '',
        primaryTriggerType: 'Time',
        secondaryTriggerType: '',
        triggerLogic: 'OR',
        mileageTrigger: '',
        operatingHoursTrigger: '',
        cycleTrigger: '',
        conditionCriteria: '',
        advanceNotificationDays: '',
        notificationRecipients: '',
        autoGenerateWorkOrders: true
      });
    } catch (error) {
      console.error('Error creating scheduled maintenance:', error);
      toast({
        title: "Error",
        description: error instanceof Error ? error.message : "Failed to create schedule",
        variant: "destructive"
      });
    }
  };

  // Handle asset selection and auto-populate type
  const handleAssetSelection = async (value: string) => {
    console.log('=== Asset selected:', value);
    setFormData({...formData, assetId: value});
    
    try {
      const assetType = await maintenanceDataService.getMaintenanceTypeForAsset(value);
      console.log('Got asset type from service:', assetType);
      
      if (assetType) {
        console.log('Setting asset type:', assetType);
        setFormData(prev => ({...prev, assetId: value, type: assetType}));
      } else {
        console.log('No asset type found, just setting asset');
        setFormData(prev => ({...prev, assetId: value}));
      }
    } catch (error) {
      console.error('Error getting maintenance type for asset:', error);
      setFormData(prev => ({...prev, assetId: value}));
    }
  };

  const handleCompleteSchedule = (id: string) => {
    // Find the schedule to show in confirmation dialog
    const schedule = scheduledMaintenanceData.find(s => s.id === id);
    if (schedule) {
      setScheduleToGenerate(schedule);
      setIsConfirmDialogOpen(true);
    }
  };

  const handleConfirmGenerateWorkOrder = async () => {
    if (!scheduleToGenerate) return;
    
    try {
      console.log('Generating work order for schedule:', scheduleToGenerate.id);
      
      // Generate a work order using the service
      const workOrders = await maintenanceScheduleService.generateWorkOrder(scheduleToGenerate.id);
      console.log('Work orders generated:', workOrders);
      
      // Close confirmation dialog and reset state
      setIsConfirmDialogOpen(false);
      setScheduleToGenerate(null);
      
      toast({
        title: "Success!",
        description: `Work order(s) generated successfully! ${Array.isArray(workOrders) ? workOrders.length : 1} work order(s) created.`,
        variant: "success"
      });
      
      // Refresh the data using service
      const scheduledDataResponse = await maintenanceScheduleService.getSchedules();
      const schedules = scheduledDataResponse.items || scheduledDataResponse.data || [];
      const mappedSchedules = schedules.map((schedule: any) => ({
        id: schedule.id,
        title: schedule.name || schedule.title,
        assetId: schedule.assetId,
        assetName: schedule.assetName || 'Unknown Asset',
        type: schedule.maintenanceType || schedule.maintenanceTypeName || schedule.type || 'Unknown Type',
        frequency: schedule.frequency,
        nextDue: schedule.nextDueDate || schedule.nextDue,
        lastCompleted: schedule.lastCompletedDate || schedule.lastCompleted || '',
        assignedTechnician: schedule.assignedTechnicianName || schedule.assignedTechnician || 'Unassigned',
        assignedTechnicianId: schedule.assignedTechnicianId || '',
        priority: schedule.priority,
        status: schedule.isActive ? 'Scheduled' : 'Inactive',
        estimatedHours: schedule.estimatedHours || 0,
        description: schedule.description || '',
        primaryTriggerType: schedule.primaryTriggerType,
        secondaryTriggerType: schedule.secondaryTriggerType,
        triggerLogic: schedule.triggerLogic,
        mileageTrigger: schedule.mileageTrigger,
        operatingHoursTrigger: schedule.operatingHoursTrigger,
        cycleTrigger: schedule.cycleTrigger,
        conditionCriteria: schedule.conditionCriteria,
        advanceNotificationDays: schedule.advanceNotificationDays,
        notificationRecipients: schedule.notificationRecipients,
        autoGenerateWorkOrders: schedule.autoGenerateWorkOrders
      }));
      setScheduledMaintenanceData(mappedSchedules);
    } catch (error) {
      console.error('Error completing scheduled maintenance:', error);
      toast({
        title: "Error",
        description: error instanceof Error ? error.message : "Failed to generate work order",
        variant: "destructive"
      });
      // Close confirmation dialog and reset state on error too
      setIsConfirmDialogOpen(false);
      setScheduleToGenerate(null);
    }
  };

  const handleEditSchedule = (schedule: ScheduledMaintenanceItem) => {
    setEditingSchedule(schedule);
    setFormData({
      title: schedule.title,
      assetId: schedule.assetId,
      type: schedule.type,
      frequency: schedule.frequency,
      nextDue: schedule.nextDue ? schedule.nextDue.split('T')[0] : '', // Convert to YYYY-MM-DD format
      assignedTechnician: schedule.assignedTechnicianId || '__UNASSIGNED__', // Use the ID, or placeholder if unassigned
      priority: schedule.priority,
      estimatedHours: schedule.estimatedHours.toString(),
      description: schedule.description,
      // Populate trigger and notification fields
      primaryTriggerType: schedule.primaryTriggerType || 'Time',
      secondaryTriggerType: schedule.secondaryTriggerType || '',
      triggerLogic: schedule.triggerLogic || 'OR',
      mileageTrigger: schedule.mileageTrigger?.toString() || '',
      operatingHoursTrigger: schedule.operatingHoursTrigger?.toString() || '',
      cycleTrigger: schedule.cycleTrigger?.toString() || '',
      conditionCriteria: schedule.conditionCriteria || '',
      advanceNotificationDays: schedule.advanceNotificationDays?.toString() || '',
      notificationRecipients: schedule.notificationRecipients || '',
      autoGenerateWorkOrders: schedule.autoGenerateWorkOrders !== undefined ? schedule.autoGenerateWorkOrders : true
    });
    setIsEditDialogOpen(true);
  };

  const handleUpdateSchedule = async () => {
    if (!editingSchedule) return;
    
    try {
      console.log('=== UPDATING SCHEDULE ===');
      console.log('Editing schedule:', editingSchedule);
      console.log('Form data:', formData);
      
      const token = localStorage.getItem('authToken');
      
      // Validate assignedTechnician value and convert empty string or placeholder to null
      const assignedTechnicianId = formData.assignedTechnician && 
                                   formData.assignedTechnician.trim() !== '' && 
                                   formData.assignedTechnician !== '__UNASSIGNED__'
        ? formData.assignedTechnician 
        : null;
      
      console.log('assignedTechnician form value:', formData.assignedTechnician);
      console.log('processed assignedTechnicianId:', assignedTechnicianId);
      
      // Transform form data to match UpdateMaintenanceScheduleDto
      const updateDto = {
        // Required fields from CreateMaintenanceScheduleDto
        name: formData.title, // Maps to Name field
        code: editingSchedule.code || `SCHED-${Date.now()}`, // Generate code if missing
        description: formData.description,
        assetId: formData.assetId || '00000000-0000-0000-0000-000000000000',
        maintenanceTypeId: (() => {
          const foundType = maintenanceTypes.find(type => 
            type.name === formData.type || 
            type.name.toLowerCase() === formData.type.toLowerCase()
          );
          return foundType?.id || maintenanceTypes[0]?.id || '00000000-0000-0000-0000-000000000000';
        })(),
        maintenanceType: formData.type, // Maps to MaintenanceType field
        priority: formData.priority, // Maps to Priority field
        frequency: formData.frequency, // Maps to Frequency field
        frequencyValue: 1,
        frequencyUnit: 'Days', // Maps to FrequencyUnit field (required)
        frequencyInterval: formData.frequency === 'Daily' ? 1 : 
                          formData.frequency === 'Weekly' ? 7 : 
                          formData.frequency === 'Monthly' ? 30 : 
                          formData.frequency === 'Quarterly' ? 90 : 
                          formData.frequency === 'Bi-Annual' ? 180 : 
                          formData.frequency === 'Annual' ? 365 : 30,
        startDate: new Date().toISOString(),
        nextDueDate: formData.nextDue ? new Date(formData.nextDue).toISOString() : new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
        estimatedDuration: 60,
        estimatedHours: parseFloat(formData.estimatedHours) || 1,
        estimatedCost: 0,
        assignedTechnicianId: assignedTechnicianId,
        assignedTeamId: null,
        assignedTeam: '',
        assetCategory: assets.find(asset => asset.id === formData.assetId)?.categoryName || '',
        instructions: formData.description,
        safetyNotes: '',
        requiredSkills: [],
        requiredTools: [],
        requiredParts: [],
        isActive: true,
        autoCreate: true,
        autoGenerateWorkOrders: formData.autoGenerateWorkOrders,
        leadTime: 5,
        advanceNotificationDays: formData.advanceNotificationDays ? parseInt(formData.advanceNotificationDays) : 7,
        notificationRecipients: formData.notificationRecipients || null,
        maxDelayDays: 3,
        notes: formData.description,
        // Trigger fields
        primaryTriggerType: formData.primaryTriggerType,
        secondaryTriggerType: formData.secondaryTriggerType || null,
        triggerLogic: formData.primaryTriggerType === 'Combined' ? formData.triggerLogic : null,
        mileageTrigger: formData.mileageTrigger ? parseFloat(formData.mileageTrigger) : null,
        operatingHoursTrigger: formData.operatingHoursTrigger ? parseFloat(formData.operatingHoursTrigger) : null,
        cycleTrigger: formData.cycleTrigger ? parseInt(formData.cycleTrigger) : null,
        conditionCriteria: formData.conditionCriteria || null
      };
      
      console.log('Final request payload:', JSON.stringify(updateDto, null, 2));
      
      // Use service instead of fetch
      await maintenanceScheduleService.updateSchedule(editingSchedule.id, updateDto);
      
      toast({
        title: "Success!",
        description: "Scheduled maintenance updated successfully.",
        variant: "success"
      });
      
      // Refresh the data using service
      const scheduledDataResponse = await maintenanceScheduleService.getSchedules();
      const schedules = scheduledDataResponse.items || scheduledDataResponse.data || [];
      const mappedSchedules = schedules.map((schedule: any) => ({
        id: schedule.id,
        title: schedule.name || schedule.title,
        assetId: schedule.assetId,
        assetName: schedule.assetName || 'Unknown Asset',
        type: schedule.maintenanceType || schedule.maintenanceTypeName || schedule.type || 'Unknown Type',
        frequency: schedule.frequency,
        nextDue: schedule.nextDueDate || schedule.nextDue,
        lastCompleted: schedule.lastCompletedDate || schedule.lastCompleted || '',
        assignedTechnician: schedule.assignedTechnicianName || schedule.assignedTechnician || 'Unassigned',
        assignedTechnicianId: schedule.assignedTechnicianId || '',
        priority: schedule.priority,
        status: schedule.isActive ? 'Scheduled' : 'Inactive',
        estimatedHours: schedule.estimatedHours || 0,
        description: schedule.description || '',
        primaryTriggerType: schedule.primaryTriggerType,
        secondaryTriggerType: schedule.secondaryTriggerType,
        triggerLogic: schedule.triggerLogic,
        mileageTrigger: schedule.mileageTrigger,
        operatingHoursTrigger: schedule.operatingHoursTrigger,
        cycleTrigger: schedule.cycleTrigger,
        conditionCriteria: schedule.conditionCriteria,
        advanceNotificationDays: schedule.advanceNotificationDays,
        notificationRecipients: schedule.notificationRecipients,
        autoGenerateWorkOrders: schedule.autoGenerateWorkOrders
      }));
      setScheduledMaintenanceData(mappedSchedules);
      
      setIsEditDialogOpen(false);
      setEditingSchedule(null);
      // Reset form
      setFormData({
        title: '',
        assetId: '',
        type: '',
        frequency: '',
        nextDue: '',
        assignedTechnician: '',
        priority: 'Medium',
        estimatedHours: '',
        description: '',
        primaryTriggerType: 'Time',
        secondaryTriggerType: '',
        triggerLogic: 'OR',
        mileageTrigger: '',
        operatingHoursTrigger: '',
        cycleTrigger: '',
        conditionCriteria: '',
        advanceNotificationDays: '',
        notificationRecipients: '',
        autoGenerateWorkOrders: true
      });
    } catch (error) {
      console.error('Error updating scheduled maintenance:', error);
      toast({
        title: "Error",
        description: error instanceof Error ? error.message : "Failed to update schedule",
        variant: "destructive"
      });
    }
  };

  return (
    <div className="space-y-6" suppressHydrationWarning>
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Scheduled Maintenance</h1>
          <p className="text-muted-foreground">
            Manage preventive and scheduled maintenance tasks
          </p>
        </div>
        <ClientOnly>
          <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                Schedule Maintenance
              </Button>
            </DialogTrigger>
          <DialogContent className="sm:max-w-[900px] max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Schedule New Maintenance</DialogTitle>
              <DialogDescription>
                Create a new scheduled maintenance task for an asset.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="title">Title</Label>
                  <Input
                    id="title"
                    value={formData.title}
                    onChange={(e) => setFormData({...formData, title: e.target.value})}
                    placeholder="Maintenance task title"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="assetId">Asset</Label>
                  <Select value={formData.assetId} onValueChange={handleAssetSelection} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading assets..." : "Select asset"} />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.assetName} ({asset.assetCode})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="type">Maintenance Type</Label>
                  {formData.type ? (
                    <div className="flex space-x-2">
                      <Input
                        id="type"
                        value={formData.type}
                        readOnly
                        className="bg-muted"
                        placeholder="Maintenance type will be auto-populated"
                      />
                      <Button 
                        variant="outline" 
                        size="sm" 
                        type="button"
                        onClick={() => setFormData({...formData, type: ''})}
                        title="Clear to select manually"
                      >
                        ✕
                      </Button>
                    </div>
                  ) : (
                    <Select value={formData.type} onValueChange={(value) => setFormData({...formData, type: value})} disabled={loadingData}>
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
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="frequency">Frequency</Label>
                  <Select value={formData.frequency} onValueChange={(value) => setFormData({...formData, frequency: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select frequency" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Daily">Daily</SelectItem>
                      <SelectItem value="Weekly">Weekly</SelectItem>
                      <SelectItem value="Monthly">Monthly</SelectItem>
                      <SelectItem value="Quarterly">Quarterly</SelectItem>
                      <SelectItem value="Bi-Annual">Bi-Annual</SelectItem>
                      <SelectItem value="Annual">Annual</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
                  <Select value={formData.priority} onValueChange={(value) => setFormData({...formData, priority: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select priority" />
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
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="nextDue">Next Due Date</Label>
                  <Input
                    id="nextDue"
                    type="date"
                    value={formData.nextDue}
                    onChange={(e) => setFormData({...formData, nextDue: e.target.value})}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="technician">Assigned Technician</Label>
                  <Select value={formData.assignedTechnician} onValueChange={(value) => setFormData({...formData, assignedTechnician: value})} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading technicians..." : "Select technician"} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__UNASSIGNED__">None (Unassigned)</SelectItem>
                      {technicians.map((technician) => (
                        <SelectItem key={technician.id} value={technician.id}>
                          {technician.firstName} {technician.lastName} - {technician.position}
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
                    value={formData.estimatedHours}
                    onChange={(e) => setFormData({...formData, estimatedHours: e.target.value})}
                    placeholder="Hours"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Detailed description of the maintenance task..."
                  rows={3}
                />
              </div>

              {/* Trigger Configuration Section */}
              <div className="space-y-4 border-t pt-4">
                <Label className="text-base font-semibold">Trigger Configuration</Label>
                <Tabs value={formData.primaryTriggerType} onValueChange={(value) => setFormData({...formData, primaryTriggerType: value})}>
                  <TabsList className="grid w-full grid-cols-4">
                    <TabsTrigger value="Time">Time-Based</TabsTrigger>
                    <TabsTrigger value="Usage">Usage-Based</TabsTrigger>
                    <TabsTrigger value="Condition">Condition-Based</TabsTrigger>
                    <TabsTrigger value="Combined">Combined</TabsTrigger>
                  </TabsList>
                </Tabs>

                {/* Usage Trigger Fields */}
                {(formData.primaryTriggerType === 'Usage' || formData.primaryTriggerType === 'Combined') && (
                  <div className="space-y-3 bg-muted/50 p-4 rounded-md">
                    <Label className="text-sm font-semibold">Usage Thresholds</Label>
                    <div className="grid grid-cols-3 gap-3">
                      <div className="space-y-2">
                        <Label htmlFor="mileageTrigger" className="text-xs">Mileage (km)</Label>
                        <Input
                          id="mileageTrigger"
                          type="number"
                          value={formData.mileageTrigger}
                          onChange={(e) => setFormData({...formData, mileageTrigger: e.target.value})}
                          placeholder="e.g., 5000"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="operatingHoursTrigger" className="text-xs">Operating Hours</Label>
                        <Input
                          id="operatingHoursTrigger"
                          type="number"
                          value={formData.operatingHoursTrigger}
                          onChange={(e) => setFormData({...formData, operatingHoursTrigger: e.target.value})}
                          placeholder="e.g., 200"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="cycleTrigger" className="text-xs">Cycle Count</Label>
                        <Input
                          id="cycleTrigger"
                          type="number"
                          value={formData.cycleTrigger}
                          onChange={(e) => setFormData({...formData, cycleTrigger: e.target.value})}
                          placeholder="e.g., 1000"
                        />
                      </div>
                    </div>
                  </div>
                )}

                {/* Condition Trigger Fields */}
                {(formData.primaryTriggerType === 'Condition' || formData.primaryTriggerType === 'Combined') && (
                  <div className="space-y-2 bg-muted/50 p-4 rounded-md">
                    <Label htmlFor="conditionCriteria" className="text-sm font-semibold">Condition Criteria (JSON)</Label>
                    <Textarea
                      id="conditionCriteria"
                      value={formData.conditionCriteria}
                      onChange={(e) => setFormData({...formData, conditionCriteria: e.target.value})}
                      placeholder='{"parameter": "temperature", "operator": ">", "value": 80}'
                      rows={3}
                      className="font-mono text-xs"
                    />
                    <p className="text-xs text-muted-foreground">Supported operators: &gt;, &gt;=, &lt;, &lt;=, ==, !=</p>
                  </div>
                )}

                {/* Combined Trigger Logic */}
                {formData.primaryTriggerType === 'Combined' && (
                  <div className="space-y-2">
                    <Label htmlFor="triggerLogic">Trigger Logic</Label>
                    <Select value={formData.triggerLogic} onValueChange={(value) => setFormData({...formData, triggerLogic: value})}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select logic" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="AND">AND (All conditions must be met)</SelectItem>
                        <SelectItem value="OR">OR (Any condition triggers)</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                )}
              </div>

              {/* Notification Settings Section */}
              <div className="space-y-3 border-t pt-4">
                <Label className="text-base font-semibold">Notification Settings</Label>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="advanceNotificationDays">Advance Notification (days)</Label>
                    <Input
                      id="advanceNotificationDays"
                      type="number"
                      value={formData.advanceNotificationDays}
                      onChange={(e) => setFormData({...formData, advanceNotificationDays: e.target.value})}
                      placeholder="e.g., 7"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="autoGenerateWorkOrders">Auto-Generate Work Orders</Label>
                    <Select value={formData.autoGenerateWorkOrders.toString()} onValueChange={(value) => setFormData({...formData, autoGenerateWorkOrders: value === 'true'})}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="true">Yes</SelectItem>
                        <SelectItem value="false">No</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="notificationRecipients">Notification Recipients</Label>
                  <Input
                    id="notificationRecipients"
                    value={formData.notificationRecipients}
                    onChange={(e) => setFormData({...formData, notificationRecipients: e.target.value})}
                    placeholder="email1@example.com, email2@example.com"
                  />
                  <p className="text-xs text-muted-foreground">Separate multiple emails with commas</p>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateSchedule}>Schedule Maintenance</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        </ClientOnly>
        
        {/* Edit Scheduled Maintenance Dialog */}
        <ClientOnly>
          <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
          <DialogContent className="sm:max-w-[900px] max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Edit Scheduled Maintenance</DialogTitle>
              <DialogDescription>
                Update the scheduled maintenance task details.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-title">Title</Label>
                  <Input
                    id="edit-title"
                    value={formData.title}
                    onChange={(e) => setFormData({...formData, title: e.target.value})}
                    placeholder="Maintenance task title"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-assetId">Asset</Label>
                  <Select value={formData.assetId} onValueChange={handleAssetSelection} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading assets..." : "Select asset"} />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.assetName} ({asset.assetCode})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-type">Maintenance Type</Label>
                  {formData.type ? (
                    <div className="flex space-x-2">
                      <Input
                        id="edit-type"
                        value={formData.type}
                        readOnly
                        className="bg-muted"
                        placeholder="Maintenance type will be auto-populated"
                      />
                      <Button 
                        variant="outline" 
                        size="sm" 
                        type="button"
                        onClick={() => setFormData({...formData, type: ''})}
                        title="Clear to select manually"
                      >
                        ✕
                      </Button>
                    </div>
                  ) : (
                    <Select value={formData.type} onValueChange={(value) => setFormData({...formData, type: value})} disabled={loadingData}>
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
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-frequency">Frequency</Label>
                  <Select value={formData.frequency} onValueChange={(value) => setFormData({...formData, frequency: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select frequency" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Daily">Daily</SelectItem>
                      <SelectItem value="Weekly">Weekly</SelectItem>
                      <SelectItem value="Monthly">Monthly</SelectItem>
                      <SelectItem value="Quarterly">Quarterly</SelectItem>
                      <SelectItem value="Bi-Annual">Bi-Annual</SelectItem>
                      <SelectItem value="Annual">Annual</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-priority">Priority</Label>
                  <Select value={formData.priority} onValueChange={(value) => setFormData({...formData, priority: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select priority" />
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
              
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-nextDue">Next Due Date</Label>
                  <Input
                    id="edit-nextDue"
                    type="date"
                    value={formData.nextDue}
                    onChange={(e) => setFormData({...formData, nextDue: e.target.value})}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-technician">Assigned Technician</Label>
                  <Select value={formData.assignedTechnician} onValueChange={(value) => setFormData({...formData, assignedTechnician: value})} disabled={loadingData}>
                    <SelectTrigger>
                      <SelectValue placeholder={loadingData ? "Loading technicians..." : "Select technician"} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__UNASSIGNED__">None (Unassigned)</SelectItem>
                      {technicians.map((technician) => (
                        <SelectItem key={technician.id} value={technician.id}>
                          {technician.firstName} {technician.lastName} - {technician.position}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-estimatedHours">Estimated Hours</Label>
                  <Input
                    id="edit-estimatedHours"
                    type="number"
                    value={formData.estimatedHours}
                    onChange={(e) => setFormData({...formData, estimatedHours: e.target.value})}
                    placeholder="Hours"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="edit-description">Description</Label>
                <Textarea
                  id="edit-description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Detailed description of the maintenance task..."
                  rows={3}
                />
              </div>

              {/* Trigger Configuration Section */}
              <div className="space-y-4 border-t pt-4">
                <Label className="text-base font-semibold">Trigger Configuration</Label>
                <Tabs value={formData.primaryTriggerType} onValueChange={(value) => setFormData({...formData, primaryTriggerType: value})}>
                  <TabsList className="grid w-full grid-cols-4">
                    <TabsTrigger value="Time">Time-Based</TabsTrigger>
                    <TabsTrigger value="Usage">Usage-Based</TabsTrigger>
                    <TabsTrigger value="Condition">Condition-Based</TabsTrigger>
                    <TabsTrigger value="Combined">Combined</TabsTrigger>
                  </TabsList>
                </Tabs>

                {/* Usage Trigger Fields */}
                {(formData.primaryTriggerType === 'Usage' || formData.primaryTriggerType === 'Combined') && (
                  <div className="space-y-3 bg-muted/50 p-4 rounded-md">
                    <Label className="text-sm font-semibold">Usage Thresholds</Label>
                    <div className="grid grid-cols-3 gap-3">
                      <div className="space-y-2">
                        <Label htmlFor="edit-mileageTrigger" className="text-xs">Mileage (km)</Label>
                        <Input
                          id="edit-mileageTrigger"
                          type="number"
                          value={formData.mileageTrigger}
                          onChange={(e) => setFormData({...formData, mileageTrigger: e.target.value})}
                          placeholder="e.g., 5000"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="edit-operatingHoursTrigger" className="text-xs">Operating Hours</Label>
                        <Input
                          id="edit-operatingHoursTrigger"
                          type="number"
                          value={formData.operatingHoursTrigger}
                          onChange={(e) => setFormData({...formData, operatingHoursTrigger: e.target.value})}
                          placeholder="e.g., 200"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="edit-cycleTrigger" className="text-xs">Cycle Count</Label>
                        <Input
                          id="edit-cycleTrigger"
                          type="number"
                          value={formData.cycleTrigger}
                          onChange={(e) => setFormData({...formData, cycleTrigger: e.target.value})}
                          placeholder="e.g., 1000"
                        />
                      </div>
                    </div>
                  </div>
                )}

                {/* Condition Trigger Fields */}
                {(formData.primaryTriggerType === 'Condition' || formData.primaryTriggerType === 'Combined') && (
                  <div className="space-y-2 bg-muted/50 p-4 rounded-md">
                    <Label htmlFor="edit-conditionCriteria" className="text-sm font-semibold">Condition Criteria (JSON)</Label>
                    <Textarea
                      id="edit-conditionCriteria"
                      value={formData.conditionCriteria}
                      onChange={(e) => setFormData({...formData, conditionCriteria: e.target.value})}
                      placeholder='{"parameter": "temperature", "operator": ">", "value": 80}'
                      rows={3}
                      className="font-mono text-xs"
                    />
                    <p className="text-xs text-muted-foreground">Supported operators: &gt;, &gt;=, &lt;, &lt;=, ==, !=</p>
                  </div>
                )}

                {/* Combined Trigger Logic */}
                {formData.primaryTriggerType === 'Combined' && (
                  <div className="space-y-2">
                    <Label htmlFor="edit-triggerLogic">Trigger Logic</Label>
                    <Select value={formData.triggerLogic} onValueChange={(value) => setFormData({...formData, triggerLogic: value})}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select logic" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="AND">AND (All conditions must be met)</SelectItem>
                        <SelectItem value="OR">OR (Any condition triggers)</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                )}
              </div>

              {/* Notification Settings Section */}
              <div className="space-y-3 border-t pt-4">
                <Label className="text-base font-semibold">Notification Settings</Label>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="edit-advanceNotificationDays">Advance Notification (days)</Label>
                    <Input
                      id="edit-advanceNotificationDays"
                      type="number"
                      value={formData.advanceNotificationDays}
                      onChange={(e) => setFormData({...formData, advanceNotificationDays: e.target.value})}
                      placeholder="e.g., 7"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="edit-autoGenerateWorkOrders">Auto-Generate Work Orders</Label>
                    <Select value={formData.autoGenerateWorkOrders.toString()} onValueChange={(value) => setFormData({...formData, autoGenerateWorkOrders: value === 'true'})}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="true">Yes</SelectItem>
                        <SelectItem value="false">No</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-notificationRecipients">Notification Recipients</Label>
                  <Input
                    id="edit-notificationRecipients"
                    value={formData.notificationRecipients}
                    onChange={(e) => setFormData({...formData, notificationRecipients: e.target.value})}
                    placeholder="email1@example.com, email2@example.com"
                  />
                  <p className="text-xs text-muted-foreground">Separate multiple emails with commas</p>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => {
                setIsEditDialogOpen(false);
                setEditingSchedule(null);
                // Reset form
                setFormData({
                  title: '',
                  assetId: '',
                  type: '',
                  frequency: '',
                  nextDue: '',
                  assignedTechnician: '',
                  priority: 'Medium',
                  estimatedHours: '',
                  description: ''
                });
              }}>
                Cancel
              </Button>
              <Button onClick={handleUpdateSchedule}>Update Schedule</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        </ClientOnly>
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
            <BreadcrumbPage>Scheduled Maintenance</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <ClientOnly>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="scheduled">Scheduled</SelectItem>
                <SelectItem value="in progress">In Progress</SelectItem>
                <SelectItem value="completed">Completed</SelectItem>
                <SelectItem value="overdue">Overdue</SelectItem>
              </SelectContent>
            </Select>

            <Select value={priorityFilter} onValueChange={setPriorityFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Priority" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Priorities</SelectItem>
                <SelectItem value="low">Low</SelectItem>
                <SelectItem value="medium">Medium</SelectItem>
                <SelectItem value="high">High</SelectItem>
                <SelectItem value="critical">Critical</SelectItem>
              </SelectContent>
            </Select>

            <Select value={typeFilter} onValueChange={setTypeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="preventive">Preventive</SelectItem>
                <SelectItem value="safety">Safety</SelectItem>
                <SelectItem value="inspection">Inspection</SelectItem>
                <SelectItem value="calibration">Calibration</SelectItem>
              </SelectContent>
            </Select>

            <Popover>
              <PopoverTrigger asChild>
                <Button variant="outline" className="justify-start text-left font-normal">
                  <CalendarIcon className="mr-2 h-4 w-4" />
                  {dateRange?.from ? (
                    dateRange.to ? (
                      <>
                        {format(dateRange.from, "LLL dd, y")} -{" "}
                        {format(dateRange.to, "LLL dd, y")}
                      </>
                    ) : (
                      format(dateRange.from, "LLL dd, y")
                    )
                  ) : (
                    <span>Pick a date range</span>
                  )}
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-auto p-0" align="start">
                <Calendar
                  initialFocus
                  mode="range"
                  defaultMonth={dateRange?.from}
                  selected={dateRange}
                  onSelect={setDateRange}
                  numberOfMonths={2}
                />
              </PopoverContent>
            </Popover>
            </div>
          </ClientOnly>
        </CardContent>
      </Card>

      {/* Scheduled Maintenance List */}
      <Card>
        <CardHeader>
          <CardTitle>Scheduled Maintenance Tasks</CardTitle>
          <CardDescription>
            {filteredData.length} scheduled maintenance task(s)
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {console.log('📊 RENDERING GRID - filteredData length:', filteredData.length)}
            {console.log('📊 RENDERING GRID - filteredData:', filteredData)}
            {filteredData.length === 0 && (
              <div className="text-center py-8 text-muted-foreground">
                No scheduled maintenance tasks found.
                {scheduledMaintenanceData.length > 0 && (
                  <div className="mt-2 text-sm">
                    ({scheduledMaintenanceData.length} total tasks, but filtered out by current filters)
                  </div>
                )}
              </div>
            )}
            {filteredData.map((item) => (
              <div key={item.id} className="border rounded-lg p-4">
                <div className="flex items-start justify-between">
                  <div className="space-y-2 flex-1">
                    <div className="flex items-center space-x-2">
                      <h3 className="font-semibold">{item.title}</h3>
                      {getPriorityBadge(item.priority)}
                      {getStatusBadge(item.status)}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div className="flex items-center space-x-2">
                        <Package className="h-4 w-4" />
                        <span>{item.assetName}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Users className="h-4 w-4" />
                        <span>{item.assignedTechnician}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <CalendarIcon className="h-4 w-4" />
                        <span>Due: {new Date(item.nextDue).toLocaleDateString()}</span>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Clock className="h-4 w-4" />
                        <span>{item.estimatedHours} hours</span>
                      </div>
                    </div>
                    
                    <div className="text-sm">
                      <span className="font-medium">Type:</span> {item.type} | 
                      <span className="font-medium"> Frequency:</span> {item.frequency} |
                      <span className="font-medium"> Last Done:</span> {new Date(item.lastCompleted).toLocaleDateString()}
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{item.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    {item.status === 'Scheduled' && (
                      <Button size="sm" onClick={() => handleCompleteSchedule(item.id)}>
                        <CheckCircle className="mr-2 h-4 w-4" />
                        Generate Work Order
                      </Button>
                    )}
                    <Button variant="outline" size="sm" onClick={() => handleEditSchedule(item)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button variant="outline" size="sm">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Confirmation Dialog for Work Order Generation */}
      <Dialog open={isConfirmDialogOpen} onOpenChange={setIsConfirmDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Generate Work Order</DialogTitle>
            <DialogDescription>
              Are you sure you want to generate a work order for this scheduled maintenance task?
            </DialogDescription>
          </DialogHeader>
          
          {scheduleToGenerate && (
            <div className="space-y-4 py-4">
              <div className="space-y-2">
                <p><strong>Task:</strong> {scheduleToGenerate.title}</p>
                <p><strong>Asset:</strong> {scheduleToGenerate.assetName}</p>
                <p><strong>Type:</strong> {scheduleToGenerate.type}</p>
                <p><strong>Priority:</strong> {scheduleToGenerate.priority}</p>
                <p><strong>Due Date:</strong> {new Date(scheduleToGenerate.nextDue).toLocaleDateString('en-GB', { 
                  day: '2-digit', 
                  month: 'short', 
                  year: 'numeric' 
                }).replace(/ /g, '-')}</p>
                {scheduleToGenerate.assignedTechnician && scheduleToGenerate.assignedTechnician !== 'Unassigned' ? (
                  <p><strong>Assigned to:</strong> {scheduleToGenerate.assignedTechnician}</p>
                ) : (
                  <p><strong>Assigned to:</strong> <em>No technician assigned</em></p>
                )}
              </div>
            </div>
          )}
          
          <DialogFooter>
            <Button 
              variant="outline" 
              onClick={() => {
                setIsConfirmDialogOpen(false);
                setScheduleToGenerate(null);
              }}
            >
              Cancel
            </Button>
            <Button onClick={handleConfirmGenerateWorkOrder}>
              Generate Work Order
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

    </div>
  );
}
