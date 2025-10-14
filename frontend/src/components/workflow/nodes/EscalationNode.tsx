import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { AlertTriangle, Clock, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface EscalationNodeData {
  label: string;
  timeout?: number;
  escalationLevels?: number;
}

export const EscalationNode = memo<NodeProps<EscalationNodeData>>(({ data }) => {
  return (
    <div className="px-3 py-2 shadow-md rounded-md bg-white border-2 border-red-400 min-w-[160px]">
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-red-500"
      />
      
      <div className="flex items-center justify-center mb-1">
        <AlertTriangle className="h-4 w-4 mr-2 text-red-600" />
        <div className="text-sm font-medium text-center text-gray-800">
          {data.label || 'Escalation'}
        </div>
      </div>
      
      <div className="flex justify-center space-x-1 mb-1">
        {data.timeout && (
          <div className="flex items-center px-1 py-0.5 bg-red-100 rounded text-xs text-red-800">
            <Clock className="h-3 w-3 mr-1" />
            <span>{data.timeout}h</span>
          </div>
        )}
        {data.escalationLevels && (
          <div className="flex items-center px-1 py-0.5 bg-orange-100 rounded text-xs text-orange-800">
            <Users className="h-3 w-3 mr-1" />
            <span>{data.escalationLevels} levels</span>
          </div>
        )}
      </div>
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-red-500"
      />
    </div>
  );
});

EscalationNode.displayName = 'EscalationNode';