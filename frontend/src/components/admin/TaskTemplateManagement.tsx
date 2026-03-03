/* eslint-disable @typescript-eslint/no-non-null-assertion */
'use client';

import React, { useState, useEffect } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
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
  Grid,
  Switch,
  FormControlLabel,
  Tooltip,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  Divider,
  Alert,
  Snackbar
} from '@mui/material';
import {
  Add as AddIcon,
  Edit as EditIcon,
  Delete as DeleteIcon,
  ContentCopy as CopyIcon,
  Upload as UploadIcon,
  Download as DownloadIcon,
  ExpandMore as ExpandMoreIcon,
  DragIndicator as DragIcon,
  Build as BuildIcon,
  Assignment as TaskIcon,
  Settings as SettingsIcon,
  Visibility as ViewIcon,
  Save as SaveIcon,
  Cancel as CancelIcon
} from '@mui/icons-material';

// Types
interface Asset {
  id: string;
  name: string;
  assetNumber: string;
  assetTypeId: string;
  assetTypeName: string;
}

interface AssetType {
  id: string;
  name: string;
  category: string;
}

interface MaintenanceType {
  id: string;
  name: string;
  code: string;
  category: string;
}

interface TaskTemplate {
  id?: string;
  taskName: string;
  description: string;
  sequence: number;
  estimatedHours: number;
  isRequired: boolean;
  instructions?: string;
  safetyRequirements?: string;
  requiredTools?: string;
  requiredParts?: string;
  isActive: boolean;
}

interface AssetTaskTemplate extends TaskTemplate {
  assetId: string;
  assetName: string;
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

interface AssetTypeTaskTemplate extends TaskTemplate {
  assetTypeId: string;
  assetTypeName: string;
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

interface MaintenanceTaskTemplate extends TaskTemplate {
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

const TaskTemplateManagement: React.FC = () => {
  const [activeTab, setActiveTab] = useState(0);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [assetTypes, setAssetTypes] = useState<AssetType[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<MaintenanceType[]>([]);
  
  // Asset-specific templates
  const [assetTemplates, setAssetTemplates] = useState<AssetTaskTemplate[]>([]);
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null);
  const [selectedAssetMaintenanceType, setSelectedAssetMaintenanceType] = useState<string>('');
  
  // Asset type templates
  const [assetTypeTemplates, setAssetTypeTemplates] = useState<AssetTypeTaskTemplate[]>([]);
  const [selectedAssetType, setSelectedAssetType] = useState<AssetType | null>(null);
  const [selectedAssetTypeMaintenanceType, setSelectedAssetTypeMaintenanceType] = useState<string>('');
  
  // Maintenance type templates
  const [maintenanceTemplates, setMaintenanceTemplates] = useState<MaintenanceTaskTemplate[]>([]);
  const [selectedMaintenanceType, setSelectedMaintenanceType] = useState<string>('');
  
  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<TaskTemplate | null>(null);
  const [dialogType, setDialogType] = useState<'asset' | 'assetType' | 'maintenanceType'>('asset');
  
  // Form state
  const [formData, setFormData] = useState<TaskTemplate>({
    taskName: '',
    description: '',
    sequence: 1,
    estimatedHours: 0,
    isRequired: true,
    instructions: '',
    safetyRequirements: '',
    requiredTools: '',
    requiredParts: '',
    isActive: true
  });
  
  // UI states
  const [loading, setLoading] = useState(true);
  const [snackbar, setSnackbar] = useState({ open: false, message: '', severity: 'success' as 'success' | 'error' });

  useEffect(() => {
    loadInitialData();
  }, []);

  const loadInitialData = async () => {
    setLoading(true);
    try {
      // Mock data - replace with actual API calls
      setAssets([
        { id: '1', name: 'HVAC Unit 1', assetNumber: 'HVAC-001', assetTypeId: '1', assetTypeName: 'HVAC System' },
        { id: '2', name: 'Elevator Unit 1', assetNumber: 'ELEV-001', assetTypeId: '2', assetTypeName: 'Elevator' },
        { id: '3', name: 'Generator Unit 1', assetNumber: 'GEN-001', assetTypeId: '3', assetTypeName: 'Generator' },
      ]);
      
      setAssetTypes([
        { id: '1', name: 'HVAC System', category: 'Climate Control' },
        { id: '2', name: 'Elevator', category: 'Transportation' },
        { id: '3', name: 'Generator', category: 'Power Systems' },
      ]);
      
      setMaintenanceTypes([
        { id: '1', name: 'Preventive', code: 'PREV', category: 'Scheduled' },
        { id: '2', name: 'Corrective', code: 'CORR', category: 'Reactive' },
        { id: '3', name: 'Emergency', code: 'EMER', category: 'Critical' },
        { id: '4', name: 'Inspection', code: 'INSP', category: 'Compliance' },
      ]);
      
      // Load existing templates
      loadAssetTemplates();
      loadAssetTypeTemplates();
      loadMaintenanceTemplates();
    } catch (error) {
      console.error('Failed to load initial data:', error);
      showSnackbar('Failed to load data', 'error');
    } finally {
      setLoading(false);
    }
  };

  const loadAssetTemplates = async () => {
    // Mock data - replace with API call
    setAssetTemplates([
      {
        id: '1',
        assetId: '1',
        assetName: 'HVAC Unit 1',
        maintenanceTypeId: '1',
        maintenanceTypeName: 'Preventive',
        taskName: 'Replace Air Filters',
        description: 'Replace all air filters with MERV 13 rated filters',
        sequence: 1,
        estimatedHours: 0.5,
        isRequired: true,
        instructions: 'Turn off system, remove old filters, install new filters with airflow arrows pointing correct direction',
        safetyRequirements: 'Wear dust mask and gloves',
        requiredTools: 'Screwdriver',
        requiredParts: 'MERV 13 Air Filters x4',
        isActive: true
      },
      {
        id: '2',
        assetId: '1',
        assetName: 'HVAC Unit 1',
        maintenanceTypeId: '1',
        maintenanceTypeName: 'Preventive',
        taskName: 'Check Refrigerant Levels',
        description: 'Inspect and top up refrigerant if necessary',
        sequence: 2,
        estimatedHours: 1.0,
        isRequired: true,
        instructions: 'Connect gauges, check pressures, add refrigerant if needed',
        safetyRequirements: 'EPA certification required, wear safety glasses',
        requiredTools: 'Manifold gauges, refrigerant recovery unit',
        requiredParts: 'R410A refrigerant (as needed)',
        isActive: true
      }
    ]);
  };

  const loadAssetTypeTemplates = async () => {
    // Mock data - replace with API call
    setAssetTypeTemplates([
      {
        id: '1',
        assetTypeId: '1',
        assetTypeName: 'HVAC System',
        maintenanceTypeId: '1',
        maintenanceTypeName: 'Preventive',
        taskName: 'System Inspection',
        description: 'General HVAC system inspection',
        sequence: 1,
        estimatedHours: 2.0,
        isRequired: true,
        instructions: 'Check all components for proper operation',
        safetyRequirements: 'Lock out electrical power',
        isActive: true
      }
    ]);
  };

  const loadMaintenanceTemplates = async () => {
    // Mock data - replace with API call  
    setMaintenanceTemplates([
      {
        id: '1',
        maintenanceTypeId: '1',
        maintenanceTypeName: 'Preventive',
        taskName: 'Pre-Work Safety Check',
        description: 'Perform safety assessment before starting work',
        sequence: 1,
        estimatedHours: 0.25,
        isRequired: true,
        instructions: 'Review safety procedures, check PPE, verify lockout/tagout',
        safetyRequirements: 'All required PPE must be worn',
        isActive: true
      }
    ]);
  };

  const handleCreateTemplate = () => {
    setFormData({
      taskName: '',
      description: '',
      sequence: getNextSequence(),
      estimatedHours: 0,
      isRequired: true,
      instructions: '',
      safetyRequirements: '',
      requiredTools: '',
      requiredParts: '',
      isActive: true
    });
    setDialogType(activeTab === 0 ? 'asset' : activeTab === 1 ? 'assetType' : 'maintenanceType');
    setIsCreateDialogOpen(true);
  };

  const handleEditTemplate = (template: TaskTemplate, type: 'asset' | 'assetType' | 'maintenanceType') => {
    setEditingTemplate(template);
    setFormData({ ...template });
    setDialogType(type);
    setIsEditDialogOpen(true);
  };

  const handleSaveTemplate = async () => {
    try {
      if (editingTemplate) {
        // Update existing template
        await updateTemplate();
      } else {
        // Create new template
        await createTemplate();
      }
      
      showSnackbar('Template saved successfully', 'success');
      setIsCreateDialogOpen(false);
      setIsEditDialogOpen(false);
      setEditingTemplate(null);
      
      // Reload templates
      if (dialogType === 'asset') loadAssetTemplates();
      else if (dialogType === 'assetType') loadAssetTypeTemplates();
      else loadMaintenanceTemplates();
    } catch (error) {
      showSnackbar('Failed to save template', 'error');
    }
  };

  const createTemplate = async () => {
    // Mock implementation - replace with API call
    const newTemplate = { ...formData, id: Date.now().toString() };
    
    if (dialogType === 'asset' && selectedAsset) {
      const assetTemplate: AssetTaskTemplate = {
        ...newTemplate,
        assetId: selectedAsset.id,
        assetName: selectedAsset.name,
        maintenanceTypeId: selectedAssetMaintenanceType,
        maintenanceTypeName: maintenanceTypes.find(mt => mt.id === selectedAssetMaintenanceType)?.name || ''
      };
      setAssetTemplates(prev => [...prev, assetTemplate]);
    } else if (dialogType === 'assetType' && selectedAssetType) {
      const assetTypeTemplate: AssetTypeTaskTemplate = {
        ...newTemplate,
        assetTypeId: selectedAssetType.id,
        assetTypeName: selectedAssetType.name,
        maintenanceTypeId: selectedAssetTypeMaintenanceType,
        maintenanceTypeName: maintenanceTypes.find(mt => mt.id === selectedAssetTypeMaintenanceType)?.name || ''
      };
      setAssetTypeTemplates(prev => [...prev, assetTypeTemplate]);
    } else if (dialogType === 'maintenanceType') {
      const maintenanceTemplate: MaintenanceTaskTemplate = {
        ...newTemplate,
        maintenanceTypeId: selectedMaintenanceType,
        maintenanceTypeName: maintenanceTypes.find(mt => mt.id === selectedMaintenanceType)?.name || ''
      };
      setMaintenanceTemplates(prev => [...prev, maintenanceTemplate]);
    }
  };

  const updateTemplate = async () => {
    // Mock implementation - replace with API call
    if (dialogType === 'asset') {
      setAssetTemplates(prev => prev.map(t => t.id === editingTemplate?.id ? { ...t, ...formData } : t));
    } else if (dialogType === 'assetType') {
      setAssetTypeTemplates(prev => prev.map(t => t.id === editingTemplate?.id ? { ...t, ...formData } : t));
    } else {
      setMaintenanceTemplates(prev => prev.map(t => t.id === editingTemplate?.id ? { ...t, ...formData } : t));
    }
  };

  const handleDeleteTemplate = async (templateId: string, type: 'asset' | 'assetType' | 'maintenanceType') => {
    if (!confirm('Are you sure you want to delete this template?')) return;
    
    try {
      // Mock implementation - replace with API call
      if (type === 'asset') {
        setAssetTemplates(prev => prev.filter(t => t.id !== templateId));
      } else if (type === 'assetType') {
        setAssetTypeTemplates(prev => prev.filter(t => t.id !== templateId));
      } else {
        setMaintenanceTemplates(prev => prev.filter(t => t.id !== templateId));
      }
      
      showSnackbar('Template deleted successfully', 'success');
    } catch (error) {
      showSnackbar('Failed to delete template', 'error');
    }
  };

  const getNextSequence = () => {
    if (activeTab === 0) {
      const filtered = assetTemplates.filter(t => 
        t.assetId === selectedAsset?.id && 
        t.maintenanceTypeId === selectedAssetMaintenanceType
      );
      return filtered.length > 0 ? Math.max(...filtered.map(t => t.sequence)) + 1 : 1;
    } else if (activeTab === 1) {
      const filtered = assetTypeTemplates.filter(t => 
        t.assetTypeId === selectedAssetType?.id && 
        t.maintenanceTypeId === selectedAssetTypeMaintenanceType
      );
      return filtered.length > 0 ? Math.max(...filtered.map(t => t.sequence)) + 1 : 1;
    } else {
      const filtered = maintenanceTemplates.filter(t => 
        t.maintenanceTypeId === selectedMaintenanceType
      );
      return filtered.length > 0 ? Math.max(...filtered.map(t => t.sequence)) + 1 : 1;
    }
  };

  const showSnackbar = (message: string, severity: 'success' | 'error') => {
    setSnackbar({ open: true, message, severity });
  };

  const getFilteredAssetTemplates = () => {
    return assetTemplates.filter(t => 
      (!selectedAsset || t.assetId === selectedAsset.id) &&
      (!selectedAssetMaintenanceType || t.maintenanceTypeId === selectedAssetMaintenanceType)
    ).sort((a, b) => a.sequence - b.sequence);
  };

  const getFilteredAssetTypeTemplates = () => {
    return assetTypeTemplates.filter(t => 
      (!selectedAssetType || t.assetTypeId === selectedAssetType.id) &&
      (!selectedAssetTypeMaintenanceType || t.maintenanceTypeId === selectedAssetTypeMaintenanceType)
    ).sort((a, b) => a.sequence - b.sequence);
  };

  const getFilteredMaintenanceTemplates = () => {
    return maintenanceTemplates.filter(t => 
      (!selectedMaintenanceType || t.maintenanceTypeId === selectedMaintenanceType)
    ).sort((a, b) => a.sequence - b.sequence);
  };

  const renderTemplateDialog = () => (
    <Dialog open={isCreateDialogOpen || isEditDialogOpen} onClose={() => {
      setIsCreateDialogOpen(false);
      setIsEditDialogOpen(false);
    }} maxWidth="md" fullWidth>
      <DialogTitle>
        {editingTemplate ? 'Edit' : 'Create'} Task Template
      </DialogTitle>
      <DialogContent>
        <Grid container spacing={3} sx={{ mt: 1 }}>
          <Grid item xs={12} md={6}>
            <TextField
              fullWidth
              label="Task Name"
              value={formData.taskName}
              onChange={(e) => setFormData(prev => ({ ...prev, taskName: e.target.value }))}
              required
            />
          </Grid>
          <Grid item xs={12} md={3}>
            <TextField
              fullWidth
              label="Sequence"
              type="number"
              value={formData.sequence}
              onChange={(e) => setFormData(prev => ({ ...prev, sequence: parseInt(e.target.value) || 1 }))}
              required
            />
          </Grid>
          <Grid item xs={12} md={3}>
            <TextField
              fullWidth
              label="Estimated Hours"
              type="number"
              step="0.25"
              value={formData.estimatedHours}
              onChange={(e) => setFormData(prev => ({ ...prev, estimatedHours: parseFloat(e.target.value) || 0 }))}
              required
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              fullWidth
              label="Description"
              multiline
              rows={2}
              value={formData.description}
              onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              fullWidth
              label="Instructions"
              multiline
              rows={3}
              value={formData.instructions}
              onChange={(e) => setFormData(prev => ({ ...prev, instructions: e.target.value }))}
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              fullWidth
              label="Safety Requirements"
              multiline
              rows={2}
              value={formData.safetyRequirements}
              onChange={(e) => setFormData(prev => ({ ...prev, safetyRequirements: e.target.value }))}
            />
          </Grid>
          {(dialogType === 'asset' || dialogType === 'assetType') && (
            <>
              <Grid item xs={12} md={6}>
                <TextField
                  fullWidth
                  label="Required Tools"
                  value={formData.requiredTools}
                  onChange={(e) => setFormData(prev => ({ ...prev, requiredTools: e.target.value }))}
                />
              </Grid>
              <Grid item xs={12} md={6}>
                <TextField
                  fullWidth
                  label="Required Parts"
                  value={formData.requiredParts}
                  onChange={(e) => setFormData(prev => ({ ...prev, requiredParts: e.target.value }))}
                />
              </Grid>
            </>
          )}
          <Grid item xs={12} md={6}>
            <FormControlLabel
              control={
                <Switch
                  checked={formData.isRequired}
                  onChange={(e) => setFormData(prev => ({ ...prev, isRequired: e.target.checked }))}
                />
              }
              label="Required Task"
            />
          </Grid>
          <Grid item xs={12} md={6}>
            <FormControlLabel
              control={
                <Switch
                  checked={formData.isActive}
                  onChange={(e) => setFormData(prev => ({ ...prev, isActive: e.target.checked }))}
                />
              }
              label="Active"
            />
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => {
          setIsCreateDialogOpen(false);
          setIsEditDialogOpen(false);
        }}>
          Cancel
        </Button>
        <Button onClick={handleSaveTemplate} variant="contained" startIcon={<SaveIcon />}>
          Save
        </Button>
      </DialogActions>
    </Dialog>
  );

  const renderTemplateTable = (
    templates: TaskTemplate[], 
    type: 'asset' | 'assetType' | 'maintenanceType'
  ) => (
    <TableContainer component={Paper}>
      <Table>
        <TableHead>
          <TableRow>
            <TableCell>Seq</TableCell>
            <TableCell>Task Name</TableCell>
            <TableCell>Description</TableCell>
            <TableCell>Hours</TableCell>
            <TableCell>Required</TableCell>
            <TableCell>Active</TableCell>
            <TableCell>Actions</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {templates.map((template) => (
            <TableRow key={template.id}>
              <TableCell>{template.sequence}</TableCell>
              <TableCell>{template.taskName}</TableCell>
              <TableCell>{template.description}</TableCell>
              <TableCell>{template.estimatedHours}h</TableCell>
              <TableCell>
                <Chip 
                  label={template.isRequired ? 'Required' : 'Optional'} 
                  color={template.isRequired ? 'primary' : 'default'} 
                  size="small" 
                />
              </TableCell>
              <TableCell>
                <Chip 
                  label={template.isActive ? 'Active' : 'Inactive'} 
                  color={template.isActive ? 'success' : 'default'} 
                  size="small" 
                />
              </TableCell>
              <TableCell>
                <Tooltip title="Edit">
                  <IconButton 
                    size="small" 
                    onClick={() => handleEditTemplate(template, type)}
                  >
                    <EditIcon />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Delete">
                  <IconButton 
                    size="small" 
                    onClick={() => handleDeleteTemplate(template.id!, type)}
                  >
                    <DeleteIcon />
                  </IconButton>
                </Tooltip>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );

  return (
    <Box sx={{ p: 3 }}>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4" fontWeight="bold">
          Task Template Management
        </Typography>
        <Box display="flex" gap={2}>
          <Button variant="outlined" startIcon={<UploadIcon />}>
            Import Templates
          </Button>
          <Button variant="outlined" startIcon={<DownloadIcon />}>
            Export Templates
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleCreateTemplate}>
            Create Template
          </Button>
        </Box>
      </Box>

      <Card>
        <CardContent>
          <Tabs value={activeTab} onChange={(e, v) => setActiveTab(v)} sx={{ mb: 3 }}>
            <Tab icon={<BuildIcon />} label="Asset-Specific Templates" />
            <Tab icon={<SettingsIcon />} label="Asset Type Templates" />
            <Tab icon={<TaskIcon />} label="Generic Templates" />
          </Tabs>

          {/* Asset-Specific Templates */}
          {activeTab === 0 && (
            <Box>
              <Grid container spacing={3} sx={{ mb: 3 }}>
                <Grid item xs={12} md={6}>
                  <FormControl fullWidth>
                    <InputLabel>Select Asset</InputLabel>
                    <Select
                      value={selectedAsset?.id || ''}
                      onChange={(e) => setSelectedAsset(assets.find(a => a.id === e.target.value) || null)}
                    >
                      <MenuItem value="">All Assets</MenuItem>
                      {assets.map((asset) => (
                        <MenuItem key={asset.id} value={asset.id}>
                          {asset.name} ({asset.assetNumber})
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
                <Grid item xs={12} md={6}>
                  <FormControl fullWidth>
                    <InputLabel>Maintenance Type</InputLabel>
                    <Select
                      value={selectedAssetMaintenanceType}
                      onChange={(e) => setSelectedAssetMaintenanceType(e.target.value)}
                    >
                      <MenuItem value="">All Types</MenuItem>
                      {maintenanceTypes.map((type) => (
                        <MenuItem key={type.id} value={type.id}>
                          {type.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
              </Grid>
              
              {renderTemplateTable(getFilteredAssetTemplates(), 'asset')}
            </Box>
          )}

          {/* Asset Type Templates */}
          {activeTab === 1 && (
            <Box>
              <Grid container spacing={3} sx={{ mb: 3 }}>
                <Grid item xs={12} md={6}>
                  <FormControl fullWidth>
                    <InputLabel>Select Asset Type</InputLabel>
                    <Select
                      value={selectedAssetType?.id || ''}
                      onChange={(e) => setSelectedAssetType(assetTypes.find(at => at.id === e.target.value) || null)}
                    >
                      <MenuItem value="">All Asset Types</MenuItem>
                      {assetTypes.map((assetType) => (
                        <MenuItem key={assetType.id} value={assetType.id}>
                          {assetType.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
                <Grid item xs={12} md={6}>
                  <FormControl fullWidth>
                    <InputLabel>Maintenance Type</InputLabel>
                    <Select
                      value={selectedAssetTypeMaintenanceType}
                      onChange={(e) => setSelectedAssetTypeMaintenanceType(e.target.value)}
                    >
                      <MenuItem value="">All Types</MenuItem>
                      {maintenanceTypes.map((type) => (
                        <MenuItem key={type.id} value={type.id}>
                          {type.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
              </Grid>
              
              {renderTemplateTable(getFilteredAssetTypeTemplates(), 'assetType')}
            </Box>
          )}

          {/* Generic Maintenance Type Templates */}
          {activeTab === 2 && (
            <Box>
              <Grid container spacing={3} sx={{ mb: 3 }}>
                <Grid item xs={12} md={6}>
                  <FormControl fullWidth>
                    <InputLabel>Maintenance Type</InputLabel>
                    <Select
                      value={selectedMaintenanceType}
                      onChange={(e) => setSelectedMaintenanceType(e.target.value)}
                    >
                      <MenuItem value="">All Types</MenuItem>
                      {maintenanceTypes.map((type) => (
                        <MenuItem key={type.id} value={type.id}>
                          {type.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
              </Grid>
              
              {renderTemplateTable(getFilteredMaintenanceTemplates(), 'maintenanceType')}
            </Box>
          )}
        </CardContent>
      </Card>

      {renderTemplateDialog()}
      
      <Snackbar
        open={snackbar.open}
        autoHideDuration={6000}
        onClose={() => setSnackbar(prev => ({ ...prev, open: false }))}
      >
        <Alert 
          onClose={() => setSnackbar(prev => ({ ...prev, open: false }))} 
          severity={snackbar.severity}
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
};

export default TaskTemplateManagement;