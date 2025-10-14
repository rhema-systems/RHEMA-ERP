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
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, Settings, History, MapPin } from 'lucide-react';
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

interface Asset {
  id: string;
  name: string;
  description: string;
  category: string;
  status: 'Operational' | 'Maintenance Required' | 'Out of Service' | 'Under Maintenance';
  location: string;
  manufacturer: string;
  model: string;
  serialNumber: string;
  purchaseDate: string;
  warrantyExpiry: string;
  lastMaintenanceDate: string;
  nextMaintenanceDate: string;
  condition: 'Excellent' | 'Good' | 'Fair' | 'Poor';
  criticality: 'Low' | 'Medium' | 'High' | 'Critical';
  value: number;
}

interface MaintenanceHistory {
  id: string;
  assetId: string;
  workOrderId: string;
  date: string;
  type: string;
  technician: string;
  description: string;
  status: string;
  cost: number;
}

const mockAssets: Asset[] = [
  {
    id: '1',
    name: 'HVAC Unit - Building A',
    description: 'Central air conditioning system for Building A',
    category: 'HVAC',
    status: 'Operational',
    location: 'Building A - Rooftop',
    manufacturer: 'Trane',
    model: 'XR15',
    serialNumber: 'TR-2023-001',
    purchaseDate: '2023-03-15',
    warrantyExpiry: '2028-03-15',
    lastMaintenanceDate: '2024-01-10',
    nextMaintenanceDate: '2024-04-10',
    condition: 'Good',
    criticality: 'High',
    value: 25000,
  },
  {
    id: '2',
    name: 'Emergency Generator',
    description: 'Backup power generator for critical systems',
    category: 'Electrical',
    status: 'Maintenance Required',
    location: 'Building B - Basement',
    manufacturer: 'Generac',
    model: 'RG048',
    serialNumber: 'GN-2022-015',
    purchaseDate: '2022-08-20',
    warrantyExpiry: '2027-08-20',
    lastMaintenanceDate: '2023-12-15',
    nextMaintenanceDate: '2024-01-15',
    condition: 'Fair',
    criticality: 'Critical',
    value: 45000,
  },
  {
    id: '3',
    name: 'Water Pump System',
    description: 'Main water circulation pump for building water supply',
    category: 'Plumbing',
    status: 'Under Maintenance',
    location: 'Building C - Mechanical Room',
    manufacturer: 'Grundfos',
    model: 'CR64-2',
    serialNumber: 'WP-2021-008',
    purchaseDate: '2021-11-12',
    warrantyExpiry: '2026-11-12',
    lastMaintenanceDate: '2024-01-18',
    nextMaintenanceDate: '2024-07-18',
    condition: 'Good',
    criticality: 'High',
    value: 8500,
  },
];

const mockMaintenanceHistory: MaintenanceHistory[] = [
  {
    id: '1',
    assetId: '1',
    workOrderId: 'WO-2024-001',
    date: '2024-01-10',
    type: 'Preventive',
    technician: 'John Smith',
    description: 'Filter replacement and system inspection',
    status: 'Completed',
    cost: 450,
  },
  {
    id: '2',
    assetId: '1',
    workOrderId: 'WO-2023-087',
    date: '2023-10-10',
    type: 'Preventive',
    technician: 'Mike Johnson',
    description: 'Quarterly maintenance check',
    status: 'Completed',
    cost: 320,
  },
  {
    id: '3',
    assetId: '2',
    workOrderId: 'WO-2023-098',
    date: '2023-12-15',
    type: 'Corrective',
    technician: 'Sarah Davis',
    description: 'Battery replacement and load testing',
    status: 'Completed',
    cost: 1200,
  },
];

export default function AssetsPage() {
  const [assets, setAssets] = useState<Asset[]>(mockAssets);
  const [filteredAssets, setFilteredAssets] = useState<Asset[]>(mockAssets);
  const [maintenanceHistory] = useState<MaintenanceHistory[]>(mockMaintenanceHistory);
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  
  const [newAsset, setNewAsset] = useState({
    name: '',
    description: '',
    category: 'HVAC',
    location: '',
    manufacturer: '',
    model: '',
    serialNumber: '',
    purchaseDate: '',
    warrantyExpiry: '',
    criticality: 'Medium' as const,
    value: 0,
  });

  useEffect(() => {
    let filtered = assets;

    if (searchTerm) {
      filtered = filtered.filter(asset => 
        asset.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        asset.location.toLowerCase().includes(searchTerm.toLowerCase()) ||
        asset.manufacturer.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (categoryFilter && categoryFilter !== 'all') {
      filtered = filtered.filter(asset => asset.category === categoryFilter);
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter(asset => asset.status === statusFilter);
    }

    setFilteredAssets(filtered);
  }, [assets, searchTerm, categoryFilter, statusFilter]);

  const handleCreateAsset = () => {
    const asset: Asset = {
      id: (assets.length + 1).toString(),
      ...newAsset,
      status: 'Operational',
      lastMaintenanceDate: '',
      nextMaintenanceDate: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString().split('T')[0], // 90 days from now
      condition: 'Good',
    };
    
    setAssets([...assets, asset]);
    setIsCreateDialogOpen(false);
    setNewAsset({
      name: '',
      description: '',
      category: 'HVAC',
      location: '',
      manufacturer: '',
      model: '',
      serialNumber: '',
      purchaseDate: '',
      warrantyExpiry: '',
      criticality: 'Medium',
      value: 0,
    });
  };

  const getStatusBadge = (status: Asset['status']) => {
    const colors = {
      'Operational': 'bg-green-100 text-green-800',
      'Maintenance Required': 'bg-yellow-100 text-yellow-800',
      'Out of Service': 'bg-red-100 text-red-800',
      'Under Maintenance': 'bg-blue-100 text-blue-800',
    };

    return (
      <Badge className={colors[status]}>
        {status}
      </Badge>
    );
  };

  const getConditionBadge = (condition: Asset['condition']) => {
    const colors = {
      'Excellent': 'bg-green-100 text-green-800',
      'Good': 'bg-blue-100 text-blue-800',
      'Fair': 'bg-yellow-100 text-yellow-800',
      'Poor': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[condition]}>
        {condition}
      </Badge>
    );
  };

  const getCriticalityBadge = (criticality: Asset['criticality']) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[criticality]}>
        {criticality}
      </Badge>
    );
  };

  const getAssetMaintenanceHistory = (assetId: string) => {
    return maintenanceHistory.filter(record => record.assetId === assetId);
  };

  const isMaintenanceOverdue = (nextMaintenanceDate: string) => {
    if (!nextMaintenanceDate) return false;
    return new Date(nextMaintenanceDate) < new Date();
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Asset Management</h1>
          <p className="text-muted-foreground">
            Manage and track all maintenance assets and their conditions
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
            <BreadcrumbPage>Assets</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>
      
      <div className="flex items-center justify-between">
        <div></div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Asset
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Add New Asset</DialogTitle>
              <DialogDescription>
                Register a new asset for maintenance management.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Asset Name</Label>
                  <Input
                    id="name"
                    value={newAsset.name}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, name: e.target.value }))}
                    placeholder="Asset name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="category">Category</Label>
                  <Select value={newAsset.category} onValueChange={(value) => setNewAsset(prev => ({ ...prev, category: value }))}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="HVAC">HVAC</SelectItem>
                      <SelectItem value="Electrical">Electrical</SelectItem>
                      <SelectItem value="Plumbing">Plumbing</SelectItem>
                      <SelectItem value="Safety">Safety</SelectItem>
                      <SelectItem value="Mechanical">Mechanical</SelectItem>
                      <SelectItem value="IT">IT Equipment</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={newAsset.description}
                  onChange={(e) => setNewAsset(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Detailed description of the asset"
                  rows={3}
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="location">Location</Label>
                  <Input
                    id="location"
                    value={newAsset.location}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, location: e.target.value }))}
                    placeholder="Asset location"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="criticality">Criticality</Label>
                  <Select value={newAsset.criticality} onValueChange={(value: any) => setNewAsset(prev => ({ ...prev, criticality: value }))}>
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
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="manufacturer">Manufacturer</Label>
                  <Input
                    id="manufacturer"
                    value={newAsset.manufacturer}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, manufacturer: e.target.value }))}
                    placeholder="Manufacturer"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="model">Model</Label>
                  <Input
                    id="model"
                    value={newAsset.model}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, model: e.target.value }))}
                    placeholder="Model number"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="serialNumber">Serial Number</Label>
                  <Input
                    id="serialNumber"
                    value={newAsset.serialNumber}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, serialNumber: e.target.value }))}
                    placeholder="Serial number"
                  />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="purchaseDate">Purchase Date</Label>
                  <Input
                    id="purchaseDate"
                    type="date"
                    value={newAsset.purchaseDate}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, purchaseDate: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="warrantyExpiry">Warranty Expiry</Label>
                  <Input
                    id="warrantyExpiry"
                    type="date"
                    value={newAsset.warrantyExpiry}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, warrantyExpiry: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="value">Asset Value ($)</Label>
                  <Input
                    id="value"
                    type="number"
                    min="0"
                    step="100"
                    value={newAsset.value}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, value: parseFloat(e.target.value) || 0 }))}
                    placeholder="0"
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateAsset}>
                Add Asset
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {/* Asset Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Assets</CardTitle>
            <Settings className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{assets.length}</div>
            <p className="text-xs text-muted-foreground">
              Registered assets
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Operational</CardTitle>
            <Settings className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">
              {assets.filter(a => a.status === 'Operational').length}
            </div>
            <p className="text-xs text-muted-foreground">
              Currently operational
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Needs Maintenance</CardTitle>
            <AlertCircle className="h-4 w-4 text-yellow-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-yellow-600">
              {assets.filter(a => a.status === 'Maintenance Required' || isMaintenanceOverdue(a.nextMaintenanceDate)).length}
            </div>
            <p className="text-xs text-muted-foreground">
              Requires attention
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Out of Service</CardTitle>
            <AlertCircle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">
              {assets.filter(a => a.status === 'Out of Service').length}
            </div>
            <p className="text-xs text-muted-foreground">
              Not operational
            </p>
          </CardContent>
        </Card>
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
                  placeholder="Search assets..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="category-filter" className="text-sm">Category</Label>
              <Select value={categoryFilter} onValueChange={setCategoryFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Categories" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Categories</SelectItem>
                  <SelectItem value="HVAC">HVAC</SelectItem>
                  <SelectItem value="Electrical">Electrical</SelectItem>
                  <SelectItem value="Plumbing">Plumbing</SelectItem>
                  <SelectItem value="Safety">Safety</SelectItem>
                  <SelectItem value="Mechanical">Mechanical</SelectItem>
                  <SelectItem value="IT">IT Equipment</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="status-filter" className="text-sm">Status</Label>
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="Operational">Operational</SelectItem>
                  <SelectItem value="Maintenance Required">Maintenance Required</SelectItem>
                  <SelectItem value="Under Maintenance">Under Maintenance</SelectItem>
                  <SelectItem value="Out of Service">Out of Service</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Assets Table */}
      <Card>
        <CardHeader>
          <CardTitle>Assets ({filteredAssets.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Location</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Condition</TableHead>
                <TableHead>Criticality</TableHead>
                <TableHead>Next Maintenance</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredAssets.map((asset) => (
                <TableRow key={asset.id}>
                  <TableCell>
                    <div>
                      <p className="font-medium">{asset.name}</p>
                      <p className="text-sm text-muted-foreground">{asset.manufacturer} {asset.model}</p>
                    </div>
                  </TableCell>
                  <TableCell>{asset.category}</TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-1">
                      <MapPin className="h-3 w-3 text-muted-foreground" />
                      <span className="text-sm">{asset.location}</span>
                    </div>
                  </TableCell>
                  <TableCell>{getStatusBadge(asset.status)}</TableCell>
                  <TableCell>{getConditionBadge(asset.condition)}</TableCell>
                  <TableCell>{getCriticalityBadge(asset.criticality)}</TableCell>
                  <TableCell>
                    <div className="text-sm">
                      {asset.nextMaintenanceDate ? (
                        <div className={isMaintenanceOverdue(asset.nextMaintenanceDate) ? 'text-red-600 font-medium' : ''}>
                          {new Date(asset.nextMaintenanceDate).toLocaleDateString()}
                          {isMaintenanceOverdue(asset.nextMaintenanceDate) && (
                            <div className="text-xs">Overdue</div>
                          )}
                        </div>
                      ) : (
                        'Not scheduled'
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedAsset(asset);
                          setIsViewDialogOpen(true);
                        }}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                      >
                        <Edit className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* View Asset Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Asset Details</DialogTitle>
            <DialogDescription>
              Complete information and maintenance history for this asset
            </DialogDescription>
          </DialogHeader>
          {selectedAsset && (
            <Tabs defaultValue="details" className="space-y-4">
              <TabsList>
                <TabsTrigger value="details">Asset Details</TabsTrigger>
                <TabsTrigger value="history">Maintenance History</TabsTrigger>
                <TabsTrigger value="schedule">Maintenance Schedule</TabsTrigger>
              </TabsList>
              
              <TabsContent value="details" className="space-y-4">
                <div className="grid grid-cols-2 gap-6">
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Asset Name</Label>
                      <p className="text-sm font-medium">{selectedAsset.name}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Description</Label>
                      <p className="text-sm">{selectedAsset.description}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Location</Label>
                      <p className="text-sm">{selectedAsset.location}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Category</Label>
                      <p className="text-sm">{selectedAsset.category}</p>
                    </div>
                  </div>
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                      <div className="pt-1">{getStatusBadge(selectedAsset.status)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Condition</Label>
                      <div className="pt-1">{getConditionBadge(selectedAsset.condition)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Criticality</Label>
                      <div className="pt-1">{getCriticalityBadge(selectedAsset.criticality)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Asset Value</Label>
                      <p className="text-sm">${selectedAsset.value.toLocaleString()}</p>
                    </div>
                  </div>
                </div>
                
                <div className="border-t pt-4">
                  <h4 className="text-sm font-medium mb-4">Technical Information</h4>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Manufacturer</Label>
                      <p className="text-sm">{selectedAsset.manufacturer}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Model</Label>
                      <p className="text-sm">{selectedAsset.model}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Serial Number</Label>
                      <p className="text-sm">{selectedAsset.serialNumber}</p>
                    </div>
                  </div>
                </div>

                <div className="border-t pt-4">
                  <h4 className="text-sm font-medium mb-4">Dates & Warranty</h4>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Purchase Date</Label>
                      <p className="text-sm">{new Date(selectedAsset.purchaseDate).toLocaleDateString()}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Warranty Expiry</Label>
                      <p className="text-sm">{new Date(selectedAsset.warrantyExpiry).toLocaleDateString()}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Last Maintenance</Label>
                      <p className="text-sm">
                        {selectedAsset.lastMaintenanceDate 
                          ? new Date(selectedAsset.lastMaintenanceDate).toLocaleDateString()
                          : 'Never'
                        }
                      </p>
                    </div>
                  </div>
                </div>
              </TabsContent>
              
              <TabsContent value="history">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Maintenance History</h4>
                  <div className="space-y-3">
                    {getAssetMaintenanceHistory(selectedAsset.id).map((record) => (
                      <div key={record.id} className="border rounded-lg p-4">
                        <div className="flex justify-between items-start">
                          <div>
                            <p className="font-medium text-sm">{record.description}</p>
                            <div className="flex items-center space-x-4 mt-2 text-xs text-muted-foreground">
                              <span>{new Date(record.date).toLocaleDateString()}</span>
                              <span>{record.type}</span>
                              <span>{record.technician}</span>
                              <span>WO: {record.workOrderId}</span>
                            </div>
                          </div>
                          <div className="text-right">
                            <Badge variant="outline" className="mb-1">{record.status}</Badge>
                            <p className="text-sm font-medium">${record.cost}</p>
                          </div>
                        </div>
                      </div>
                    ))}
                    {getAssetMaintenanceHistory(selectedAsset.id).length === 0 && (
                      <p className="text-sm text-muted-foreground text-center py-8">
                        No maintenance history found for this asset.
                      </p>
                    )}
                  </div>
                </div>
              </TabsContent>
              
              <TabsContent value="schedule">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Maintenance Schedule</h4>
                  <div className="border rounded-lg p-4">
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="font-medium text-sm">Next Scheduled Maintenance</p>
                        <p className="text-sm text-muted-foreground">
                          {selectedAsset.nextMaintenanceDate 
                            ? new Date(selectedAsset.nextMaintenanceDate).toLocaleDateString()
                            : 'Not scheduled'
                          }
                        </p>
                      </div>
                      <div className="flex space-x-2">
                        <Button size="sm" variant="outline">
                          <Calendar className="h-4 w-4 mr-2" />
                          Schedule Maintenance
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>
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