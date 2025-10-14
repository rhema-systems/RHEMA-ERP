import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { CheckCircle } from 'lucide-react';

interface EndNodeData {
  label: string;
}

export const EndNode = memo<NodeProps<EndNodeData>>(({ data }) => {
  return (
    <div className="px-4 py-2 shadow-md rounded-md bg-red-500 border-2 border-red-600 text-white min-w-[120px]">
      <div className="flex items-center justify-center">
        <CheckCircle className="h-4 w-4 mr-2" />
        <div className="text-sm font-medium text-center">{data.label || 'End'}</div>
      </div>
      <Handle
        type="target"
        position={Position.Top}
        className="w-3 h-3 !bg-red-400"
      />
    </div>
  );
});

EndNode.displayName = 'EndNode';