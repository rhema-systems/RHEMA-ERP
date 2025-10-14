import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { GitBranch } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface ConditionNodeData {
  label: string;
  conditions?: string[];
  operator?: 'AND' | 'OR';
}

export const ConditionNode = memo<NodeProps<ConditionNodeData>>(({ data }) => {
  return (
    <div className="relative">
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-yellow-400"
        style={{ top: -6 }}
      />
      
      {/* Diamond shape using CSS transforms */}
      <div 
        className="w-20 h-20 bg-yellow-400 border-2 border-yellow-500 transform rotate-45 shadow-md"
        style={{ 
          clipPath: 'polygon(50% 0%, 100% 50%, 50% 100%, 0% 50%)'
        }}
      >
        <div className="absolute inset-0 flex items-center justify-center transform -rotate-45">
          <div className="text-center">
            <GitBranch className="h-4 w-4 mx-auto mb-1 text-yellow-800" />
            <div className="text-xs font-medium text-yellow-900 leading-tight">
              {data.label || 'Condition'}
            </div>
            {data.operator && (
              <Badge variant="outline" className="text-xs bg-yellow-100 text-yellow-800 mt-1">
                {data.operator}
              </Badge>
            )}
          </div>
        </div>
      </div>
      
      <Handle
        type="source"
        position={Position.Right}
        className="w-3 h-3 !bg-green-400"
        id="true"
        style={{ right: -6 }}
      />
      <Handle
        type="source"
        position={Position.Left}
        className="w-3 h-3 !bg-red-400"
        id="false"
        style={{ left: -6 }}
      />
    </div>
  );
});

ConditionNode.displayName = 'ConditionNode';