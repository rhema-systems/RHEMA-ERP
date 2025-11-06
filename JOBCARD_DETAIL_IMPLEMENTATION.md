# Job Card Detail Dialog - Complete Implementation

This document provides the complete replacement code for the job card detail view dialog in `JobCardManagement.tsx`.

## Replace the View Dialog Section (starting from line 1314)

Replace the entire `{/* View Job Card Dialog */}` section with the following comprehensive tabbed interface:

```tsx
      {/* View Job Card Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={(open) => {
        setIsViewDialogOpen(open);
        if (!open) {
          setSelectedCardDetails(null);
          setSelectedWorkOrder(null);
          setSelectedQCInspection(null);
        }
      }}>
        <DialogContent className="max-w-6xl max-h-[95vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="text-2xl">Job Card Details</DialogTitle>
            <DialogDescription>
              Complete job card information and workflow tracking
            </DialogDescription>
          </DialogHeader>
          
          {loadingDetails ? (
            <div className="flex items-center justify-center py-12">
              <Clock className="h-8 w-8 animate-spin mr-3" />
              <span className="text-lg">Loading comprehensive details...</span>
            </div>
          ) : selectedCardDetails && (
            <Tabs defaultValue="overview" className="w-full">
              <TabsList className="grid w-full grid-cols-4">
                <TabsTrigger value="overview">Overview</TabsTrigger>
                <TabsTrigger value="workflow">Complete Workflow</TabsTrigger>
                <TabsTrigger value="workorder" disabled={!selectedWorkOrder}>Work Order Details</TabsTrigger>
                <TabsTrigger value="qc" disabled={!selectedQCInspection}>QC & Certificate</TabsTrigger>
              </TabsList>

              {/* OVERVIEW TAB */}
              <TabsContent value="overview" className="space-y-4 mt-4">
                {/* Title and Status Card */}
                <Card>
                  <CardHeader>
                    <div className="flex items-start justify-between">
                      <div>
                        <CardTitle className="text-xl mb-1">{selectedCardDetails.title}</CardTitle>
                        <CardDescription className="text-base">Job Card #{selectedCardDetails.jobCardNumber}</CardDescription>
                      </div>
                      <div className="flex gap-2">
                        {getStatusBadge(selectedCardDetails.jobCardStatus)}
                        <Badge style={selectedCardDetails.approvalStatus === 'Approved' ? { backgroundColor: '#d1fae5', color: '#065f46' } : selectedCardDetails.approvalStatus === 'Rejected' ? { backgroundColor: '#fee2e2', color: '#991b1b' } : { backgroundColor: '#fef3c7', color: '#92400e' }}>
                          {selectedCardDetails.approvalStatus}
                        </Badge>
                        {getPriorityBadge(selectedCardDetails.priority)}
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="grid grid-cols-2 gap-6">
                      <div>
                        <Label className="text-sm font-semibold">Requested By</Label>
                        <p className="text-base font-medium mt-1">{selectedCardDetails.requestedBy}</p>
                        <p className="text-sm text-muted-foreground">{new Date(selectedCardDetails.requestedDate).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' })}</p>
                      </div>
                      {selectedCardDetails.approvedBy && (
                        <div>
                          <Label className="text-sm font-semibold">Approved By</Label>
                          <p className="text-base font-medium mt-1">{selectedCardDetails.approvedBy}</p>
                          {selectedCardDetails.approvedAt && (
                            <p className="text-sm text-muted-foreground">{new Date(selectedCardDetails.approvedAt).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' })}</p>
                          )}
                        </div>
                      )}
                    </div>
                  </CardContent>
                </Card>

                {/* Asset Information Card */}
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">Asset Information</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="grid grid-cols-2 gap-6">
                      <div>
                        <Label className="text-sm font-semibold">Asset Name</Label>
                        <p className="text-base font-medium mt-1">{selectedCardDetails.assetName}</p>
                        <p className="text-sm text-muted-foreground font-mono">{selectedCardDetails.assetCode}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-semibold">Asset Category/Type</Label>
                        <p className="text-base mt-1">{selectedCardDetails.assetType || selectedCardDetails.maintenanceCategory || 'N/A'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-semibold">Location</Label>
                        <p className="text-base mt-1">{selectedCardDetails.assetLocation || selectedCardDetails.maintenanceLocation || 'N/A'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-semibold">Maintenance Type</Label>
                        <p className="text-base mt-1">{selectedCardDetails.maintenanceType}</p>
                      </div>
                    </div>
                  </CardContent>
                </Card>

                {/* Work Description Card */}
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">Work Description</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    {selectedCardDetails.description && (
                      <div>
                        <Label className="text-sm font-semibold">Description</Label>
                        <p className="text-base mt-1 whitespace-pre-wrap">{selectedCardDetails.description}</p>
                      </div>
                    )}
                    {selectedCardDetails.problemDescription && (
                      <div>
                        <Label className="text-sm font-semibold">Problem Description</Label>
                        <p className="text-base mt-1 whitespace-pre-wrap bg-red-50 p-3 rounded-md border border-red-100">{selectedCardDetails.problemDescription}</p>
                      </div>
                    )}
                  </CardContent>
                </Card>

                {/* Estimates Card */}
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">Estimates & Requirements</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="grid grid-cols-4 gap-6">
                      <div>
                        <Label className="text-sm font-semibold">Estimated Hours</Label>
                        <p className="text-2xl font-bold mt-1">{selectedCardDetails.estimatedHours}</p>
                        <p className="text-xs text-muted-foreground">hours</p>
                      </div>
                      <div>
                        <Label className="text-sm font-semibold">Estimated Cost</Label>
                        <p className="text-2xl font-bold mt-1">${selectedCardDetails.estimatedCost.toFixed(2)}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-semibold">Requires Shutdown</Label>
                        <p className="text-lg font-semibold mt-1">{selectedCardDetails.requiresShutdown ? '✓ Yes' : '✗ No'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-semibold">Safety Permit</Label>
                        <p className="text-lg font-semibold mt-1">{selectedCardDetails.requiresSafetyPermit ? '✓ Required' : '✗ Not Required'}</p>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              {/* WORKFLOW TAB */}
              <TabsContent value="workflow" className="space-y-4 mt-4">
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">Complete Workflow Timeline</CardTitle>
                    <CardDescription>Track the complete journey from job card request to work completion</CardDescription>
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
                        <p className="text-xs text-muted-foreground">{new Date(selectedCardDetails.requestedDate).toLocaleString()}</p>
                      </div>
                    </div>

                    {/* Approval Steps */}
                    {selectedCardDetails.approvalSteps && selectedCardDetails.approvalSteps.length > 0 && (
                      selectedCardDetails.approvalSteps.map((step, index) => (
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
                            {index < selectedCardDetails.approvalSteps.length - 1 && (
                              <div className="w-0.5 h-full bg-gray-200 mt-2"></div>
                            )}
                          </div>
                          <div className="flex-1 pb-6">
                            <div className="flex items-center gap-2">
                              <h4 className="font-semibold text-base">{step.stepName}</h4>
                              <Badge style={step.status === 'Approved' ? { backgroundColor: '#d1fae5', color: '#065f46' } : step.status === 'Rejected' ? { backgroundColor: '#fee2e2', color: '#991b1b' } : { backgroundColor: '#fef3c7', color: '#92400e' }}>
                                {step.status}
                              </Badge>
                              {step.isRequired && <Badge variant="outline" className="text-xs">Required</Badge>}
                            </div>
                            {step.approverName && (
                              <p className="text-sm text-muted-foreground mt-1">Approver: {step.approverName}</p>
                            )}
                            {step.actionDate && (
                              <p className="text-xs text-muted-foreground">{new Date(step.actionDate).toLocaleString()}</p>
                            )}
                            {step.comments && (
                              <p className="text-sm mt-2 bg-gray-50 p-2 rounded">{step.comments}</p>
                            )}
                          </div>
                        </div>
                      ))
                    )}

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
                            <p className="text-xs text-muted-foreground">{new Date(selectedCardDetails.workOrderGeneratedAt).toLocaleString()}</p>
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
                            {selectedWorkOrder.assignedTechnician && (
                              <p className="text-sm text-muted-foreground mt-1">Assigned to: {selectedWorkOrder.assignedTechnician}</p>
                            )}
                            {selectedWorkOrder.actualStartDate && (
                              <p className="text-xs text-muted-foreground">Started: {new Date(selectedWorkOrder.actualStartDate).toLocaleString()}</p>
                            )}
                            {selectedWorkOrder.actualCompletionDate && (
                              <p className="text-xs text-muted-foreground">Completed: {new Date(selectedWorkOrder.actualCompletionDate).toLocaleString()}</p>
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
                                  <p className="text-xs text-muted-foreground">{new Date(selectedQCInspection.inspectionDate).toLocaleString()}</p>
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
                {selectedWorkOrder && (
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
                            <p className="text-base mt-1">{selectedWorkOrder.assignedTechnician || 'Unassigned'}</p>
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
                            <p className="text-base mt-1">{new Date(selectedWorkOrder.actualStartDate).toLocaleString()}</p>
                          </div>
                        )}

                        {selectedWorkOrder.actualCompletionDate && (
                          <div>
                            <Label className="text-sm font-semibold">Actual Completion Date</Label>
                            <p className="text-base mt-1">{new Date(selectedWorkOrder.actualCompletionDate).toLocaleString()}</p>
                          </div>
                        )}
                      </CardContent>
                    </Card>

                    {/* Tasks if available */}
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
                )}
              </TabsContent>

              {/* QC INSPECTION TAB */}
              <TabsContent value="qc" className="space-y-4 mt-4">
                {selectedQCInspection && (
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
                          <p className="text-base mt-1">{new Date(selectedQCInspection.inspectionDate).toLocaleString()}</p>
                        </div>
                      </div>

                      {selectedQCInspection.inspectorName && (
                        <div className="border-t pt-4">
                          <Label className="text-sm font-semibold">Inspector</Label>
                          <p className="text-base mt-1">{selectedQCInspection.inspectorName}</p>
                        </div>
                      )}

                      {selectedQCInspection.notes && (
                        <div className="border-t pt-4">
                          <Label className="text-sm font-semibold">Inspection Notes</Label>
                          <p className="text-base mt-1 bg-gray-50 p-3 rounded">{selectedQCInspection.notes}</p>
                        </div>
                      )}

                      {selectedQCInspection.overallResult === 'Pass' && (
                        <div className="border-t pt-4">
                          <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                            <div className="flex items-center gap-2">
                              <CheckCircle className="h-6 w-6 text-green-600" />
                              <div>
                                <p className="font-semibold text-green-900">Quality Certificate Generated</p>
                                <p className="text-sm text-green-700">This work order has passed quality inspection and a certificate has been generated.</p>
                              </div>
                            </div>
                          </div>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                )}
              </TabsContent>
            </Tabs>
          )}
          
          <DialogFooter className="mt-6">
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
```

## Key Features Implemented:

1. **Overview Tab**: Shows job title, requestor, approver, asset details (including category), descriptions, and estimates
2. **Workflow Tab**: Complete timeline from job card creation → approval steps → work order generation → execution → QC inspection → certificate
3. **Work Order Tab**: Full work order details including tasks, hours, costs, and completion status
4. **QC Inspection Tab**: Quality control results, inspector info, score, and certificate status

This provides a comprehensive view of the entire maintenance workflow from a single job card detail screen.
