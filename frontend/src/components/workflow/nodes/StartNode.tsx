import React, { memo } from 'react';
import { Handle, Position, NodeProps } from 'reactflow';
import { Play } from 'lucide-react';

interface StartNodeData {
  label: string;
}

export const StartNode = memo<NodeProps<StartNodeData>>(({ data }) => {
  return (
    <div className="px-4 py-2 shadow-md rounded-md bg-green-500 border-2 border-green-600 text-white min-w-[120px]">
      <div className="flex items-center justify-center">
        <Play className="h-4 w-4 mr-2" />
        <div className="text-sm font-medium text-center">{data.label || 'Start'}</div>
      </div>
      <Handle
        type="source"
        position={Position.Bottom}
        className="w-3 h-3 !bg-green-400"
      />
    </div>
  );
});

StartNode.displayName = 'StartNode';