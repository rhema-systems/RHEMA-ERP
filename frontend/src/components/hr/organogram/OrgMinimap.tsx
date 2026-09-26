'use client';

import { useRef } from 'react';
import type { LayoutResult } from './layout';
import type { Rect, Transform } from './OrgCanvas';

/**
 * A thumbnail of the whole layout with the viewport drawn over it. Click or drag to move the view.
 * Cards are drawn as rectangles in the level colour of their depth so the shape of the
 * organisation reads even when the boxes are a few pixels tall.
 */

const MAX_W = 220;
const MAX_H = 150;

export interface OrgMinimapProps {
  layout: LayoutResult;
  transform: Transform;
  worldRect: Rect;
  selectedId: string | null;
  onNavigate: (worldCenter: { x: number; y: number }) => void;
}

export function OrgMinimap({ layout, worldRect, selectedId, onNavigate }: OrgMinimapProps) {
  const svgRef = useRef<SVGSVGElement>(null);
  const dragging = useRef(false);

  if (!layout.width || !layout.height) return null;
  const s = Math.min(MAX_W / layout.width, MAX_H / layout.height);
  const w = Math.max(80, Math.round(layout.width * s));
  const h = Math.max(48, Math.round(layout.height * s));

  const toWorld = (e: React.PointerEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    return { x: (e.clientX - rect.left) / s, y: (e.clientY - rect.top) / s };
  };

  return (
    <svg
      ref={svgRef}
      className="org-minimap"
      width={w}
      height={h}
      viewBox={`0 0 ${layout.width} ${layout.height}`}
      role="img"
      aria-label="Overview of the whole chart. Click to move the view."
      onPointerDown={(e) => {
        dragging.current = true;
        e.currentTarget.setPointerCapture(e.pointerId);
        onNavigate(toWorld(e));
      }}
      onPointerMove={(e) => dragging.current && onNavigate(toWorld(e))}
      onPointerUp={() => (dragging.current = false)}
      onPointerCancel={() => (dragging.current = false)}
    >
      {layout.placed.map((p) =>
        p.item.kind === 'more' ? null : (
          <rect
            key={p.item.id}
            x={p.x}
            y={p.y}
            width={p.w}
            height={p.h}
            rx={Math.min(12, p.h / 4)}
            className={p.item.id === selectedId ? 'org-minimap-card-selected' : 'org-minimap-card'}
            style={{ fill: `var(--org-cat-${Math.min(p.item.depth, 7) + 1})` }}
          />
        ),
      )}
      <rect
        x={worldRect.x}
        y={worldRect.y}
        width={worldRect.w}
        height={worldRect.h}
        className="org-minimap-viewport"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
