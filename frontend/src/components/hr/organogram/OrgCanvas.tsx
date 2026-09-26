'use client';

import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
  type RefObject,
} from 'react';
import { cn } from '@/lib/utils';
import type { LayoutResult, PlacedItem } from './layout';

/**
 * The pan-and-zoom surface. Cards are HTML so they can truncate, theme and carry buttons; edges are
 * one SVG under them. A single CSS transform moves both, and only what intersects the viewport is
 * mounted — on TDC's people dimension the layout can hold thousands of cards while the DOM holds a
 * few hundred.
 *
 * Strokes use `vector-effect: non-scaling-stroke` so a 2px line is 2px at every zoom level. The
 * demo complaint was hairlines that disappeared at 65%; this is the fix that survives zooming out.
 */

export interface Transform {
  x: number;
  y: number;
  k: number;
}

export const ZOOM_MIN = 0.15;
export const ZOOM_MAX = 2.5;

export interface Rect {
  x: number;
  y: number;
  w: number;
  h: number;
}

const clampK = (k: number) => Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, k));

export function useViewport(containerRef: RefObject<HTMLDivElement | null>) {
  const [t, setT] = useState<Transform>({ x: 0, y: 0, k: 1 });
  const [size, setSize] = useState({ w: 0, h: 0 });

  useLayoutEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const update = () => setSize({ w: el.clientWidth, h: el.clientHeight });
    update();
    const ro = new ResizeObserver(update);
    ro.observe(el);
    return () => ro.disconnect();
  }, [containerRef]);

  const fit = useCallback(
    (rect: Rect | null, padding = 48) => {
      if (!rect || !size.w || !size.h) return;
      const k = clampK(Math.min((size.w - padding * 2) / rect.w, (size.h - padding * 2) / rect.h, 1.25));
      setT({
        k,
        x: (size.w - rect.w * k) / 2 - rect.x * k,
        y: (size.h - rect.h * k) / 2 - rect.y * k,
      });
    },
    [size],
  );

  const centerOn = useCallback(
    (rect: Rect, k?: number) => {
      if (!size.w) return;
      setT((prev) => {
        const kk = k !== undefined ? clampK(k) : prev.k;
        return {
          k: kk,
          x: size.w / 2 - (rect.x + rect.w / 2) * kk,
          y: size.h / 2 - (rect.y + rect.h / 2) * kk,
        };
      });
    },
    [size],
  );

  const zoomBy = useCallback(
    (factor: number, anchor?: { x: number; y: number }) => {
      setT((prev) => {
        const k = clampK(prev.k * factor);
        const ax = anchor?.x ?? size.w / 2;
        const ay = anchor?.y ?? size.h / 2;
        return { k, x: ax - ((ax - prev.x) * k) / prev.k, y: ay - ((ay - prev.y) * k) / prev.k };
      });
    },
    [size],
  );

  const zoomTo = useCallback(
    (k: number) => {
      setT((prev) => {
        const kk = clampK(k);
        const ax = size.w / 2;
        const ay = size.h / 2;
        return { k: kk, x: ax - ((ax - prev.x) * kk) / prev.k, y: ay - ((ay - prev.y) * kk) / prev.k };
      });
    },
    [size],
  );

  /** World-space rectangle currently on screen. */
  const worldRect = useMemo<Rect>(
    () => ({ x: -t.x / t.k, y: -t.y / t.k, w: size.w / t.k, h: size.h / t.k }),
    [t, size],
  );

  const isInView = useCallback(
    (rect: Rect, margin = 0) =>
      rect.x >= worldRect.x + margin &&
      rect.y >= worldRect.y + margin &&
      rect.x + rect.w <= worldRect.x + worldRect.w - margin &&
      rect.y + rect.h <= worldRect.y + worldRect.h - margin,
    [worldRect],
  );

  return { t, setT, size, fit, centerOn, zoomBy, zoomTo, worldRect, isInView };
}

export interface OrgCanvasProps {
  layout: LayoutResult;
  transform: Transform;
  onTransform: (next: Transform | ((prev: Transform) => Transform)) => void;
  containerRef: RefObject<HTMLDivElement | null>;
  worldRect: Rect;
  renderCard: (placed: PlacedItem) => ReactNode;
  /** Node ids on the selected node's ancestor path; edges between two of them are emphasised. */
  pathIds: Set<string> | null;
  /** Node ids drawn dimmed; edges into them dim too. */
  dimmedIds: Set<string> | null;
  onBackgroundClick: () => void;
  onKeyDown?: (e: React.KeyboardEvent<HTMLDivElement>) => void;
  className?: string;
}

const CULL_MARGIN = 240;

const intersects = (a: Rect, b: Rect) =>
  a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;

export function OrgCanvas({
  layout,
  transform,
  onTransform,
  containerRef,
  worldRect,
  renderCard,
  pathIds,
  dimmedIds,
  onBackgroundClick,
  onKeyDown,
  className,
}: OrgCanvasProps) {
  const drag = useRef<{
    pointers: Map<number, { x: number; y: number }>;
    start: Transform;
    origin: { x: number; y: number } | null;
    pinchDist: number | null;
    moved: boolean;
  }>({ pointers: new Map(), start: transform, origin: null, pinchDist: null, moved: false });
  const suppressClick = useRef(false);
  const [dragging, setDragging] = useState(false);

  // Wheel must be non-passive to stop the page scrolling under the chart; React's onWheel is not.
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const rect = el.getBoundingClientRect();
      const ax = e.clientX - rect.left;
      const ay = e.clientY - rect.top;
      if (e.ctrlKey || e.metaKey || !e.shiftKey) {
        // Zoom around the cursor. Trackpad pinches arrive as ctrl+wheel; a mouse wheel just zooms.
        const factor = Math.exp(-e.deltaY * (e.ctrlKey ? 0.01 : 0.0018));
        onTransform((prev) => {
          const k = clampK(prev.k * factor);
          return { k, x: ax - ((ax - prev.x) * k) / prev.k, y: ay - ((ay - prev.y) * k) / prev.k };
        });
      } else {
        // Shift+wheel pans sideways, the way every canvas tool does.
        onTransform((prev) => ({ ...prev, x: prev.x - e.deltaY }));
      }
    };
    el.addEventListener('wheel', onWheel, { passive: false });
    return () => el.removeEventListener('wheel', onWheel);
  }, [containerRef, onTransform]);

  const onPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    if (e.button !== 0 && e.pointerType === 'mouse') return;
    const d = drag.current;
    suppressClick.current = false;
    d.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    d.start = transform;
    d.moved = false;
    if (d.pointers.size === 1) {
      d.origin = { x: e.clientX, y: e.clientY };
      d.pinchDist = null;
    } else if (d.pointers.size === 2) {
      const [a, b] = [...d.pointers.values()];
      d.pinchDist = Math.hypot(a.x - b.x, a.y - b.y);
      // A pinch never ends in a click, so both pointers can be captured at once.
      for (const id of d.pointers.keys()) e.currentTarget.setPointerCapture(id);
    }
    // ⚠ Not captured yet. A captured pointer's click is dispatched to the capturing element, which
    // would swallow every click on a card's buttons. Capture starts in onPointerMove, once the
    // pointer has actually travelled — a click never gets that far.
  };

  const onPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    const d = drag.current;
    if (!d.pointers.has(e.pointerId)) return;
    d.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

    if (d.pointers.size >= 2 && d.pinchDist) {
      const [a, b] = [...d.pointers.values()];
      const dist = Math.hypot(a.x - b.x, a.y - b.y);
      const rect = e.currentTarget.getBoundingClientRect();
      const mx = (a.x + b.x) / 2 - rect.left;
      const my = (a.y + b.y) / 2 - rect.top;
      const factor = dist / d.pinchDist;
      d.pinchDist = dist;
      d.moved = true;
      onTransform((prev) => {
        const k = clampK(prev.k * factor);
        return { k, x: mx - ((mx - prev.x) * k) / prev.k, y: my - ((my - prev.y) * k) / prev.k };
      });
      return;
    }

    if (!d.origin) return;
    const dx = e.clientX - d.origin.x;
    const dy = e.clientY - d.origin.y;
    if (!d.moved && Math.hypot(dx, dy) < 4) return;
    if (!d.moved) {
      d.moved = true;
      setDragging(true);
      e.currentTarget.setPointerCapture(e.pointerId);
    }
    onTransform({ k: d.start.k, x: d.start.x + dx, y: d.start.y + dy });
  };

  const endPointer = (e: React.PointerEvent<HTMLDivElement>) => {
    const d = drag.current;
    d.pointers.delete(e.pointerId);
    // The click that follows a drag must not select or deselect anything. The flag is cleared by
    // the click handler that consumes it, and by the next pointerdown — never on a timer, because
    // a timer can fire before the browser dispatches the click.
    if (d.moved) suppressClick.current = true;
    if (d.pointers.size === 0) {
      d.origin = null;
      d.pinchDist = null;
      d.moved = false;
      setDragging(false);
    } else if (d.pointers.size === 1) {
      const [only] = [...d.pointers.values()];
      d.origin = { x: only.x, y: only.y };
      d.start = transform;
      d.pinchDist = null;
    }
  };

  const onClickCapture = (e: React.MouseEvent<HTMLDivElement>) => {
    if (suppressClick.current) {
      suppressClick.current = false;
      e.stopPropagation();
      e.preventDefault();
    }
  };

  const onClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target === e.currentTarget || (e.target as HTMLElement).dataset.canvasBackground === 'true') {
      onBackgroundClick();
    }
  };

  const cullRect: Rect = {
    x: worldRect.x - CULL_MARGIN,
    y: worldRect.y - CULL_MARGIN,
    w: worldRect.w + CULL_MARGIN * 2,
    h: worldRect.h + CULL_MARGIN * 2,
  };
  const visibleCards = layout.placed.filter((p) => intersects(p, cullRect));
  const visibleEdges = layout.edges.filter((e) =>
    intersects({ x: e.bbox.x - 4, y: e.bbox.y - 4, w: e.bbox.w + 8, h: e.bbox.h + 8 }, cullRect),
  );

  return (
    <div
      ref={containerRef}
      className={cn('org-canvas', dragging && 'org-canvas-dragging', className)}
      tabIndex={0}
      role="application"
      aria-label="Organogram chart. Drag to pan, scroll to zoom, arrow keys to move between boxes."
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={endPointer}
      onPointerCancel={endPointer}
      onClickCapture={onClickCapture}
      onClick={onClick}
      onKeyDown={onKeyDown}
      data-canvas-background="true"
    >
      <div
        className="org-world"
        style={{
          transform: `translate(${transform.x}px, ${transform.y}px) scale(${transform.k})`,
          width: layout.width,
          height: layout.height,
        }}
        data-canvas-background="true"
      >
        <svg
          className="org-edges"
          width={layout.width}
          height={layout.height}
          viewBox={`0 0 ${layout.width} ${layout.height}`}
          aria-hidden
          data-canvas-background="true"
        >
          {visibleEdges.map((e) => {
            const onPath = !!pathIds && pathIds.has(e.from) && pathIds.has(e.to);
            const dim = !!dimmedIds && dimmedIds.has(e.to);
            return (
              <path
                key={e.id}
                d={e.d}
                className={cn('org-edge', onPath && 'org-edge-path', dim && 'org-edge-dim', e.dotted && 'org-edge-dotted')}
                vectorEffect="non-scaling-stroke"
              />
            );
          })}
        </svg>
        {visibleCards.map((p) => renderCard(p))}
      </div>
    </div>
  );
}
