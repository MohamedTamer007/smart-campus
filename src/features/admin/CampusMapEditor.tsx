import { useRef, useState, type PointerEvent } from "react";
import { useApp } from "../../app/context";

type Position = { x: number; y: number };
type Building = { id: string; x: number | null; y: number | null };

export function CampusMapEditor({ enabled, position, buildings, onPosition }: {
  enabled: boolean;
  position: Position | null;
  buildings: Building[];
  onPosition: (position: Position) => void;
}) {
  const { t, language } = useApp();
  const [zoom, setZoom] = useState(1);
  const [panning, setPanning] = useState(false);
  const pan = useRef<{ id: number; x: number; y: number; left: number; top: number; moved: boolean } | null>(null);
  const dragging = useRef<number | null>(null);
  const suppressClick = useRef(false);
  const viewport = useRef<HTMLDivElement>(null);

  function point(clientX: number, clientY: number, target: HTMLButtonElement) {
    const rect = target.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return { x: 50, y: 28 };
    return {
      x: Math.max(0, Math.min(100, (clientX - rect.left) / rect.width * 100)),
      y: Math.max(0, Math.min(56, (clientY - rect.top) / rect.height * 56)),
    };
  }
  function endDrag(event: PointerEvent<HTMLButtonElement>) {
    if (dragging.current !== event.pointerId) return;
    dragging.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId))
      event.currentTarget.releasePointerCapture(event.pointerId);
  }
  function reset() {
    pan.current = null;
    setPanning(false);
    setZoom(1);
    viewport.current?.scrollTo?.({ left: 0, top: 0 });
  }

  return (
    <div className="map-editor-workspace">
      <div className="map-zoom-controls" role="group" aria-label={t("تكبير الخريطة", "Map zoom")}>
        <button type="button" className="button secondary" aria-label={t("تصغير الخريطة", "Zoom out")} disabled={zoom <= 1} onClick={() => setZoom(value => Math.max(1, value - .5))}>−</button>
        <output aria-live="polite">{new Intl.NumberFormat(language === "ar" ? "ar-EG" : "en-GB", { style: "percent" }).format(zoom)}</output>
        <button type="button" className="button secondary" aria-label={t("تكبير الخريطة", "Zoom in")} disabled={zoom >= 4} onClick={() => setZoom(value => Math.min(4, value + .5))}>+</button>
        <button type="button" className="button secondary" onClick={reset}>{t("إعادة العرض", "Reset view")}</button>
      </div>
      {zoom > 1 && <p className="map-pan-help">{t("اسحب الصورة للتنقل؛ واسحب العلامة لتغيير موضع المبنى.", "Drag the photo to pan; drag the marker to move the building.")}</p>}
      <div
        className={`map-editor-viewport${zoom > 1 ? " is-zoomed" : ""}${panning ? " is-panning" : ""}`}
        ref={viewport}
        dir="ltr"
        role="region"
        aria-label={t("التنقل داخل الخريطة المكبرة", "Pan the zoomed map")}
        tabIndex={zoom > 1 ? 0 : -1}
        onKeyDown={event => {
          if (event.target !== event.currentTarget || zoom <= 1) return;
          const movement: Record<string, Position> = {
            ArrowLeft: { x: -40, y: 0 }, ArrowRight: { x: 40, y: 0 },
            ArrowUp: { x: 0, y: -40 }, ArrowDown: { x: 0, y: 40 },
          };
          const delta = movement[event.key];
          if (!delta) return;
          event.preventDefault();
          event.currentTarget.scrollLeft += delta.x;
          event.currentTarget.scrollTop += delta.y;
        }}
        onPointerDown={event => {
          if (zoom <= 1 || event.button !== 0 || pan.current !== null || dragging.current !== null ||
            (event.target instanceof Element && event.target.closest("[data-map-marker]"))) return;
          suppressClick.current = false;
          pan.current = { id: event.pointerId, x: event.clientX, y: event.clientY,
            left: event.currentTarget.scrollLeft, top: event.currentTarget.scrollTop, moved: false };
        }}
        onPointerMove={event => {
          const current = pan.current;
          if (!current || current.id !== event.pointerId) return;
          const dx = event.clientX - current.x;
          const dy = event.clientY - current.y;
          if (!current.moved && Math.hypot(dx, dy) < 5) return;
          if (!current.moved) {
            current.moved = true;
            suppressClick.current = true;
            setPanning(true);
            event.currentTarget.setPointerCapture(event.pointerId);
          }
          event.preventDefault();
          event.currentTarget.scrollLeft = current.left - dx;
          event.currentTarget.scrollTop = current.top - dy;
        }}
        onPointerUp={event => {
          if (pan.current?.id !== event.pointerId) return;
          pan.current = null;
          setPanning(false);
          if (event.currentTarget.hasPointerCapture(event.pointerId))
            event.currentTarget.releasePointerCapture(event.pointerId);
        }}
        onPointerCancel={() => { pan.current = null; setPanning(false); }}
        onLostPointerCapture={event => {
          if (event.target !== event.currentTarget) return;
          pan.current = null;
          setPanning(false);
        }}
        onClickCapture={event => {
          if (!suppressClick.current) return;
          suppressClick.current = false;
          event.preventDefault();
          event.stopPropagation();
        }}
      >
        <button
          type="button"
          className="map-editor-canvas-button"
          style={{ width: `${zoom * 100}%` }}
          disabled={!enabled}
          aria-label={t("اختيار موضع المبنى على الصورة", "Set building position on the photo")}
          aria-describedby="map-editor-help"
          onKeyDown={event => {
            const movement: Record<string, Position> = {
              ArrowLeft: { x: -1, y: 0 }, ArrowRight: { x: 1, y: 0 },
              ArrowUp: { x: 0, y: -1 }, ArrowDown: { x: 0, y: 1 },
            };
            const delta = movement[event.key];
            if (!delta || !enabled) return;
            event.preventDefault();
            const current = position ?? { x: 50, y: 28 };
            const step = event.shiftKey ? 5 : 1;
            onPosition({ x: Math.max(0, Math.min(100, current.x + delta.x * step)),
              y: Math.max(0, Math.min(56, current.y + delta.y * step)) });
          }}
          onClick={event => {
            if (suppressClick.current) { suppressClick.current = false; return; }
            if (enabled) onPosition(point(event.clientX, event.clientY, event.currentTarget));
          }}
          onPointerDown={event => {
            suppressClick.current = false;
            if (!enabled || event.button !== 0 || !(event.target instanceof Element) || !event.target.closest("[data-map-marker]")) return;
            event.preventDefault();
            dragging.current = event.pointerId;
            suppressClick.current = true;
            event.currentTarget.setPointerCapture(event.pointerId);
          }}
          onPointerMove={event => {
            if (enabled && dragging.current === event.pointerId)
              onPosition(point(event.clientX, event.clientY, event.currentTarget));
          }}
          onPointerUp={endDrag}
          onPointerCancel={endDrag}
          onLostPointerCapture={() => { dragging.current = null; }}
        >
          <div className="campus-map-stage map-editor-canvas">
            <img className="campus-map-image" src="/campus-map.jpg" alt="" draggable={false} />
            <svg className="campus-map-overlay" viewBox="0 0 100 52.58" aria-hidden="true">
              {buildings.filter(b => b.x != null && b.y != null).map(b => <circle key={b.id} cx={b.x!} cy={b.y! * 52.58 / 56} r="1.1" fill="#144caa" stroke="white" strokeWidth=".35" />)}
              {position && <circle data-map-marker="" className="map-draft-marker" cx={position.x} cy={position.y * 52.58 / 56} r="1.8" fill="#c44300" stroke="white" strokeWidth=".5" />}
            </svg>
          </div>
        </button>
      </div>
    </div>
  );
}
