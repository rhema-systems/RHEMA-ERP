import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { UserCheck, Users, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface ApprovalNodeData {
  label: string;
  approvers?: string[];
  approvalType?: 'any' | 'all' | 'sequence';
  escalationTimeout?: number;
}

export const ApprovalNode = memo<NodeProps<ApprovalNodeData>>(({ data }) => {
  const getApprovalTypeColor = (type: string) => {
    switch (type) {
      case 'any': return 'bg-blue-100 text-blue-800';
      case 'all': return 'bg-purple-100 text-purple-800';
      case 'sequence': return 'bg-orange-100 text-orange-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const getApprovalTypeLabel = (type: string) => {
    switch (type) {
      case 'any': return 'ANY';
      case 'all': return 'ALL';
      case 'sequence': return 'SEQ';
      default: return 'ANY';
    }
  };

  return (
    <div className="px-3 py-2 shadow-md rounded-md bg-white border-2 border-purple-300 min-w-[160px]">
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-purple-400"
      />
      
      <div className="flex items-center justify-center mb-1">
        <UserCheck className="h-4 w-4 mr-2 text-purple-600" />
        <div className="text-sm font-medium text-center text-gray-800">
          {data.label || 'Approval'}
        </div>
      </div>
      
      {data.approvalType && (
        <div className="flex justify-center mb-1">
          <Badge variant="outline" className={`text-xs ${getApprovalTypeColor(data.approvalType)}`}>
            {getApprovalTypeLabel(data.approvalType)}
          </Badge>
        </div>
      )}
      
      {data.approvers && data.approvers.length > 0 && (
        <div className="flex items-center justify-center text-xs text-gray-600 mb-1">
          <Users className="h-3 w-3 mr-1" />
          <span>{data.approvers.length} approver{data.approvers.length > 1 ? 's' : ''}</span>
        </div>
      )}
      
      {data.escalationTimeout && (
        <div className="flex items-center justify-center text-xs text-gray-600">
          <Clock className="h-3 w-3 mr-1" />
          <span>{data.escalationTimeout}h timeout</span>
        </div>
      )}
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-purple-400"
        id="approved"
      />
      <Handle
        type="source"
        position={Position.Right}
        className="w-3 h-3 !bg-red-400"
        id="rejected"
      />
    </div>
  );
});

ApprovalNode.displayName = 'ApprovalNode';