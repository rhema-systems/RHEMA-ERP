'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { toast } from 'sonner';
import {
  toolCheckoutService,
  type MaintenanceToolDto,
  type ToolCheckoutDto,
  type CheckoutToolDto,
  type ReturnToolDto,
} from '@/services/toolCheckoutService';
import maintenanceApiService, { type WorkOrder } from '@/services/maintenanceApiService';
import workOrderPartService, { type InventoryItemDto } from '@/services/workOrderPartService';
import { maintenanceDataService, type Employee } from '@/services/maintenanceDataService';
import {
  Package,
  CheckCircle2,
  XCircle,
  Clock,
  Search,
  RefreshCw,
  AlertTriangle,
  History,
  Wrench
} from 'lucide-react';

export default function ToolManagementPage() {
  const [tools, setTools] = useState<MaintenanceToolDto[]>([]);
  const [activeCheckouts, setActiveCheckouts] = useState<ToolCheckoutDto[]>([]);
  const [overdueCheckouts, setOverdueCheckouts] = useState<ToolCheckoutDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedTool, setSelectedTool] = useState<MaintenanceToolDto | null>(null);
  const [selectedCheckout, setSelectedCheckout] = useState<ToolCheckoutDto | null>(null);
  const [workOrdersById, setWorkOrdersById] = useState<Record<string, WorkOrder>>({});
  const [toolInventoryByCode, setToolInventoryByCode] = useState<Record<string, InventoryItemDto>>({});

  // Technicians (from Users table)
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [selectedTechnicianId, setSelectedTechnicianId] = useState<string>('');

  // Dialogs
  const [checkoutDialogOpen, setCheckoutDialogOpen] = useState(false);
  const [returnDialogOpen, setReturnDialogOpen] = useState(false);

  // Checkout form
  const [checkoutForm, setCheckoutForm] = useState<CheckoutToolDto>({
    conditionOnCheckout: 'Good',
    checkoutNotes: '',
    expectedReturnDate: '',
  });

  // Return form
  const [returnForm, setReturnForm] = useState<ReturnToolDto>({
    conditionOnReturn: 'Good',
    returnNotes: '',
    damageReported: false,
    damageDescription: '',
    damageCost: undefined,
  });

  useEffect(() => {
    loadData();
    loadTechnicians();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [toolsData, activeData, overdueData, workOrdersPaged, toolInventory] = await Promise.all([
        toolCheckoutService.getAllTools(),
        toolCheckoutService.getActiveCheckouts(),
        toolCheckoutService.getOverdueCheckouts(),
        maintenanceApiService.getWorkOrders(1, 500),
        workOrderPartService.getToolInventoryItems(),
      ]);
      setTools(toolsData);
      setActiveCheckouts(activeData);
      setOverdueCheckouts(overdueData);
      const workOrderMap: Record<string, WorkOrder> = {};
      for (const wo of workOrdersPaged.items) {
        workOrderMap[wo.id] = wo;
      }
      setWorkOrdersById(workOrderMap);
      const inventoryMap: Record<string, InventoryItemDto> = {};
      for (const item of toolInventory) {
        if (item.itemCode) {
          inventoryMap[item.itemCode] = item;
        }
      }
      setToolInventoryByCode(inventoryMap);
    } catch (error: any) {
      toast.error('Failed to load tool data', {
        description: error.response?.data?.message || error.message,
      });
    } finally {
      setLoading(false);
    }
  };

  const loadTechnicians = async () => {
    try {
      const techs = await maintenanceDataService.getTechnicians();
      setTechnicians(Array.isArray(techs) ? techs : []);
    } catch (error) {
      console.error('Error loading technicians for tool checkout:', error);
    }
  };

  const handleCheckout = async () => {
    if (!selectedTool || !selectedTechnicianId) {
      toast.error('Please select a technician');
      return;
    }

    if (!checkoutForm.checkoutNotes || checkoutForm.checkoutNotes.trim().length === 0) {
      toast.error('Please enter notes for this checkout');
      return;
    }

    try {
      await toolCheckoutService.checkoutTool(selectedTool.id, selectedTechnicianId, checkoutForm);
      toast.success('Tool checked out successfully');
      setCheckoutDialogOpen(false);
      resetCheckoutForm();
      loadData();
    } catch (error: any) {
      toast.error('Failed to checkout tool', {
        description: error.response?.data?.message || error.message,
      });
    }
  };

  const handleReturn = async () => {
    if (!selectedCheckout) return;

    try {
      const result = await toolCheckoutService.returnTool(selectedCheckout.id, returnForm);

      let message = 'Tool returned successfully';
      if (result.isOverdue) {
        message += ` (${result.overdueDays} days overdue)`;
      }

      toast.success(message);
      setReturnDialogOpen(false);
      resetReturnForm();
      loadData();
    } catch (error: any) {
      toast.error('Failed to return tool', {
        description: error.response?.data?.message || error.message,
      });
    }
  };

  const resetCheckoutForm = () => {
    setCheckoutForm({
      conditionOnCheckout: 'Good',
      checkoutNotes: '',
      expectedReturnDate: '',
    });
    setSelectedTechnicianId('');
    setSelectedTool(null);
  };

  const resetReturnForm = () => {
    setReturnForm({
      conditionOnReturn: 'Good',
      returnNotes: '',
      damageReported: false,
      damageDescription: '',
      damageCost: undefined,
    });
    setSelectedCheckout(null);
  };

  const getStatusBadge = (status: string) => {
    const variants: Record<string, { className: string; label: string }> = {
      Available: { className: 'bg-green-100 text-green-800 hover:bg-green-100', label: 'Available' },
      InUse: { className: 'bg-orange-100 text-orange-800 hover:bg-orange-100 font-semibold', label: 'In Use' },
      Maintenance: { className: 'bg-red-100 text-red-800 hover:bg-red-100', label: 'Maintenance' },
      OutOfService: { className: 'bg-gray-100 text-gray-800 hover:bg-gray-100', label: 'Out of Service' },
    };
    const config = variants[status] || { className: 'bg-gray-100 text-gray-800', label: status };
    return <Badge className={config.className}>{config.label}</Badge>;
  };

  // Helper: find an open checkout (active or overdue) for a given tool by tool code
  const getOpenCheckoutForTool = (tool: MaintenanceToolDto): ToolCheckoutDto | undefined => {
    if (!tool.toolCode) return undefined;
    return (
      activeCheckouts.find((c) => c.toolCode === tool.toolCode && !c.actualReturnDate) ??
      overdueCheckouts.find((c) => c.toolCode === tool.toolCode && !c.actualReturnDate)
    );
  };

  // Derive effective tool status from checkouts to avoid stale InUse flags
  const getDerivedToolStatus = (tool: MaintenanceToolDto): string => {
    const checkout = getOpenCheckoutForTool(tool);

    if (checkout) {
      return 'InUse';
    }

    if (tool.status === 'InUse') {
      // Backend says InUse but there is no open checkout; treat as Available in UI
      return 'Available';
    }

    return tool.status;
  };

  const filteredTools = tools.filter((tool) =>
    tool.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    tool.toolCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
    tool.category.toLowerCase().includes(searchTerm.toLowerCase())
  );

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <RefreshCw className="h-8 w-8 animate-spin text-gray-400" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Tool Management</h1>
          <p className="text-muted-foreground">Manage tool inventory and checkouts</p>
        </div>
        <Button onClick={loadData} variant="outline">
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Tools</CardTitle>
            <Package className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{tools.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Available</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">
              {tools.filter((t) => getDerivedToolStatus(t) === 'Available').length}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Checkouts</CardTitle>
            <Clock className="h-4 w-4 text-blue-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">{activeCheckouts.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Overdue</CardTitle>
            <AlertTriangle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{overdueCheckouts.length}</div>
          </CardContent>
        </Card>
      </div>

      {/* Main Content */}
      <Tabs defaultValue="tools" className="space-y-4">
        <TabsList>
          <TabsTrigger value="tools">All Tools</TabsTrigger>
          <TabsTrigger value="active">Active Checkouts</TabsTrigger>
          <TabsTrigger value="overdue">Overdue</TabsTrigger>
        </TabsList>

        {/* All Tools Tab */}
        <TabsContent value="tools" className="space-y-4">
          <div className="flex items-center space-x-2">
            <Search className="h-4 w-4 text-gray-400" />
            <Input
              placeholder="Search tools..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="max-w-sm"
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {filteredTools.map((tool) => {
              const checkoutForTool = getOpenCheckoutForTool(tool);
              const workOrderNumber = checkoutForTool?.workOrderId
                ? workOrdersById[checkoutForTool.workOrderId]?.workOrderNumber
                : checkoutForTool?.workOrderNumber ?? undefined;
              const derivedStatus = getDerivedToolStatus(tool);
              const inventory = toolInventoryByCode[tool.toolCode];
              const checkoutNotes = checkoutForTool?.checkoutNotes || undefined;

              return (
                <Card
                  key={tool.id}
                  className={`hover:shadow-md transition-shadow ${
                    derivedStatus === 'InUse' ? 'border-l-4 border-l-orange-500' : ''
                  }`}
                >
                  <CardHeader>
                    <div className="flex items-start justify-between">
                      <div className="space-y-1">
                        <CardTitle className="text-lg">{tool.name}</CardTitle>
                        <CardDescription>{tool.toolCode}</CardDescription>
                      </div>
                      {getStatusBadge(derivedStatus)}
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    <div className="text-sm">
                      {inventory ? (
                        <>
                          <p className="text-muted-foreground">
                            Inventory Category: {inventory.categoryName ?? 'N/A'}
                          </p>
                          <p className="text-muted-foreground">
                            Unit: {inventory.unitOfMeasure ?? 'N/A'}
                          </p>
                          <p className="text-muted-foreground">
                            Stock: {(inventory.availableStock ?? 0).toLocaleString()} available / {(inventory.currentStock ?? 0).toLocaleString()} total
                          </p>
                          <p className="text-muted-foreground">
                            Unit Cost: {typeof inventory.unitCost === 'number' ? `$${inventory.unitCost.toFixed(2)}` : 'N/A'}
                          </p>
                        </>
                      ) : (
                        <p className="text-muted-foreground">
                          No inventory data found for this tool code.
                        </p>
                      )}
                      {derivedStatus === 'InUse' && workOrderNumber && (
                        <p className="text-muted-foreground mt-1">
                          In use on{' '}
                          <Link
                            href={`/maintenance/work-orders?workOrderNumber=${encodeURIComponent(
                              workOrderNumber,
                            )}`}
                            className="font-mono text-blue-600 hover:underline"
                          >
                            WO {workOrderNumber}
                          </Link>
                        </p>
                      )}
                      {derivedStatus === 'InUse' && !workOrderNumber && checkoutNotes && (
                        <p className="text-muted-foreground mt-1">
                          Notes: {checkoutNotes}
                        </p>
                      )}
                    </div>
                    {derivedStatus === 'Available' ? (
                      <Button
                        onClick={() => {
                          setSelectedTool(tool);
                          setCheckoutDialogOpen(true);
                        }}
                        className="w-full mt-2"
                      >
                        Checkout Tool
                      </Button>
                    ) : derivedStatus === 'InUse' && checkoutForTool && !checkoutForTool.workOrderId && !checkoutForTool.workOrderNumber ? (
                      <Button
                        onClick={() => {
                          setSelectedCheckout(checkoutForTool);
                          setReturnDialogOpen(true);
                        }}
                        className="w-full mt-2"
                      >
                        Return Tool
                      </Button>
                    ) : null}
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </TabsContent>

        {/* Active Checkouts Tab */}
        <TabsContent value="active" className="space-y-4">
          <div className="grid grid-cols-1 gap-4">
            {activeCheckouts.map((checkout) => (
              <Card key={checkout.id}>
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div>
                      <CardTitle>{checkout.toolName}</CardTitle>
                      <CardDescription>
                        Checked out by: {checkout.checkedOutByName}
                      </CardDescription>
                    </div>
                    <Badge variant="secondary">
                      {checkout.daysOut} days out
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-2">
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <p className="text-muted-foreground">Checkout Date:</p>
                      <p className="font-medium">
                        {new Date(checkout.checkoutDate).toLocaleDateString()}
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Expected Return:</p>
                      <p className="font-medium">
                        {checkout.expectedReturnDate
                          ? new Date(checkout.expectedReturnDate).toLocaleDateString()
                          : 'Not specified'}
                      </p>
                    </div>
                  </div>
                  {(() => {
                    const numberFromMap = checkout.workOrderId
                      ? workOrdersById[checkout.workOrderId]?.workOrderNumber
                      : undefined;
                    const number = numberFromMap ?? checkout.workOrderNumber ?? undefined;
                    return number ? (
                      <div className="flex items-center gap-2 text-sm">
                        <p className="text-muted-foreground">Work Order:</p>
                        <Link
                          href={`/maintenance/work-orders?workOrderNumber=${encodeURIComponent(number)}`}
                          className="font-mono text-blue-600 hover:underline"
                        >
                          {number}
                        </Link>
                      </div>
                    ) : null;
                  })()}
                  <Button
                    onClick={() => {
                      setSelectedCheckout(checkout);
                      setReturnDialogOpen(true);
                    }}
                    className="w-full"
                  >
                    Return Tool
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        {/* Overdue Tab */}
        <TabsContent value="overdue" className="space-y-4">
          {overdueCheckouts.length === 0 ? (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-12">
                <CheckCircle2 className="h-12 w-12 text-green-500 mb-4" />
                <p className="text-lg font-medium">No overdue tools!</p>
                <p className="text-sm text-muted-foreground">All tools are returned on time</p>
              </CardContent>
            </Card>
          ) : (
            <div className="grid grid-cols-1 gap-4">
              {overdueCheckouts.map((checkout) => (
                <Card key={checkout.id} className="border-red-200">
                  <CardHeader>
                    <div className="flex items-start justify-between">
                      <div>
                        <CardTitle className="text-red-600">{checkout.toolName}</CardTitle>
                        <CardDescription>
                          Checked out by: {checkout.checkedOutByName}
                        </CardDescription>
                      </div>
                      <Badge variant="destructive">
                        {checkout.daysOut} days overdue
                      </Badge>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    <div className="grid grid-cols-2 gap-4 text-sm">
                      <div>
                        <p className="text-muted-foreground">Checkout Date:</p>
                        <p className="font-medium">
                          {new Date(checkout.checkoutDate).toLocaleDateString()}
                        </p>
                      </div>
                      <div>
                        <p className="text-muted-foreground">Expected Return:</p>
                        <p className="font-medium text-red-600">
                          {checkout.expectedReturnDate
                            ? new Date(checkout.expectedReturnDate).toLocaleDateString()
                            : 'Not specified'}
                        </p>
                      </div>
                    </div>
                    <Button
                      onClick={() => {
                        setSelectedCheckout(checkout);
                        setReturnDialogOpen(true);
                      }}
                      variant="destructive"
                      className="w-full"
                    >
                      Return Tool Now
                    </Button>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      {/* Checkout Dialog */}
      <Dialog open={checkoutDialogOpen} onOpenChange={setCheckoutDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Checkout Tool</DialogTitle>
            <DialogDescription>
              {selectedTool?.name} ({selectedTool?.toolCode})
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="technicianId">Technician *</Label>
              <Select
                value={selectedTechnicianId}
                onValueChange={setSelectedTechnicianId}
              >
                <SelectTrigger id="technicianId">
                  <SelectValue placeholder="Select technician" />
                </SelectTrigger>
                <SelectContent>
                  {technicians.map((tech) => (
                    <SelectItem key={tech.id} value={tech.id}>
                      {tech.firstName} {tech.lastName} ({tech.email})
                    </SelectItem>
                  ))}
                  {technicians.length === 0 && (
                    <div className="px-2 py-2 text-sm text-muted-foreground">
                      No technicians available.
                    </div>
                  )}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="condition">Condition *</Label>
              <Select
                value={checkoutForm.conditionOnCheckout}
                onValueChange={(value) =>
                  setCheckoutForm({ ...checkoutForm, conditionOnCheckout: value })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Good">Good</SelectItem>
                  <SelectItem value="Fair">Fair</SelectItem>
                  <SelectItem value="Damaged">Damaged</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="expectedReturn">Expected Return Date</Label>
              <Input
                id="expectedReturn"
                type="datetime-local"
                value={checkoutForm.expectedReturnDate}
                onChange={(e) =>
                  setCheckoutForm({ ...checkoutForm, expectedReturnDate: e.target.value })
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="notes">Notes *</Label>
              <Textarea
                id="notes"
                value={checkoutForm.checkoutNotes}
                onChange={(e) =>
                  setCheckoutForm({ ...checkoutForm, checkoutNotes: e.target.value })
                }
                placeholder="Describe where/with whom this tool is being used..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCheckoutDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleCheckout}>Checkout</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Return Dialog */}
      <Dialog open={returnDialogOpen} onOpenChange={setReturnDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Return Tool</DialogTitle>
            <DialogDescription>
              {selectedCheckout?.toolName} ({selectedCheckout?.toolCode})
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="returnCondition">Condition *</Label>
              <Select
                value={returnForm.conditionOnReturn}
                onValueChange={(value) =>
                  setReturnForm({ ...returnForm, conditionOnReturn: value })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Good">Good</SelectItem>
                  <SelectItem value="Fair">Fair</SelectItem>
                  <SelectItem value="Damaged">Damaged</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center space-x-2">
              <input
                type="checkbox"
                id="damaged"
                checked={returnForm.damageReported}
                onChange={(e) =>
                  setReturnForm({ ...returnForm, damageReported: e.target.checked })
                }
              />
              <Label htmlFor="damaged">Report Damage</Label>
            </div>
            {returnForm.damageReported && (
              <>
                <div className="space-y-2">
                  <Label htmlFor="damageDesc">Damage Description *</Label>
                  <Textarea
                    id="damageDesc"
                    value={returnForm.damageDescription}
                    onChange={(e) =>
                      setReturnForm({ ...returnForm, damageDescription: e.target.value })
                    }
                    placeholder="Describe the damage..."
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="damageCost">Estimated Cost</Label>
                  <Input
                    id="damageCost"
                    type="number"
                    step="0.01"
                    value={returnForm.damageCost || ''}
                    onChange={(e) =>
                      setReturnForm({
                        ...returnForm,
                        damageCost: e.target.value ? parseFloat(e.target.value) : undefined,
                      })
                    }
                    placeholder="0.00"
                  />
                </div>
              </>
            )}
            <div className="space-y-2">
              <Label htmlFor="returnNotes">Return Notes</Label>
              <Textarea
                id="returnNotes"
                value={returnForm.returnNotes}
                onChange={(e) =>
                  setReturnForm({ ...returnForm, returnNotes: e.target.value })
                }
                placeholder="Any additional notes..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReturnDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleReturn}>Return Tool</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
