import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

const NODE_W = 180;
const NODE_H = 70;
const GRID = 20;

function getTransition(trans, key) {
  return trans[key] || {};
}
function getEdgeTarget(t, kind) {
  if (!t) return null;
  return t[kind] || t[kind.charAt(0).toLowerCase() + kind.slice(1)] || null;
}
function setEdgeTarget(t, kind, value) {
  const next = { ...(t || {}) };
  next[kind] = value;
  const lc = kind.charAt(0).toLowerCase() + kind.slice(1);
  if (lc in next) delete next[lc];
  return next;
}
function autoLayout(keys, startId, transitions) {
  const depth = {};
  const seen = new Set();
  const walk = (id, d) => {
    if (!id || !keys.includes(id) || seen.has(id)) return;
    seen.add(id);
    depth[id] = Math.max(depth[id] ?? 0, d);
    const t = transitions[id];
    if (t) {
      walk(getEdgeTarget(t, 'OnSuccess'), d + 1);
      walk(getEdgeTarget(t, 'OnFailure'), d + 1);
      walk(getEdgeTarget(t, 'OnException'), d + 1);
    }
    seen.delete(id);
  };
  walk(startId || keys[0], 0);
  for (const k of keys) if (depth[k] === undefined) depth[k] = 0;
  const byDepth = {};
  for (const k of keys) {
    (byDepth[depth[k]] = byDepth[depth[k]] || []).push(k);
  }
  const positions = {};
  const sortedDepths = Object.keys(byDepth).map(Number).sort((a, b) => a - b);
  sortedDepths.forEach((d) => {
    const col = byDepth[d];
    col.forEach((id, i) => {
      positions[id] = { x: 60 + d * (NODE_W + 100), y: 60 + i * (NODE_H + 40) };
    });
  });
  return positions;
}

function portY(kind) {
  switch (kind) {
    case 'OnSuccess': return 20;
    case 'OnFailure': return NODE_H / 2;
    case 'OnException': return NODE_H - 20;
    default: return NODE_H / 2;
  }
}

function DataFlowGraphEditor({ transitions, startStepId, onChange, readOnly = false }) {
  const [positions, setPositions] = useState(() => autoLayout(Object.keys(transitions || {}), startStepId, transitions || {}));
  const [selectedNode, setSelectedNode] = useState(null);
  const [drag, setDrag] = useState(null);
  const [connecting, setConnecting] = useState(null);
  const [newStepName, setNewStepName] = useState('');
  const svgRef = useRef(null);

  const keys = Object.keys(transitions || {});

  useEffect(() => {
    setPositions((prev) => {
      const next = { ...prev };
      const existingKeys = Object.keys(next);
      const newKeys = keys.filter((k) => !existingKeys.includes(k));
      if (newKeys.length === 0) {
        for (const k of existingKeys) if (!keys.includes(k)) delete next[k];
        return next;
      }
      const layout = autoLayout(keys, startStepId, transitions);
      for (const k of newKeys) next[k] = layout[k];
      for (const k of existingKeys) if (!keys.includes(k)) delete next[k];
      return next;
    });
  }, [keys.join('|'), startStepId]);

  const svgMouseCoords = useCallback((evt) => {
    const svg = svgRef.current;
    if (!svg) return { x: 0, y: 0 };
    const pt = svg.createSVGPoint();
    pt.x = evt.clientX;
    pt.y = evt.clientY;
    const ctm = svg.getScreenCTM();
    if (!ctm) return { x: pt.x, y: pt.y };
    return pt.matrixTransform(ctm.inverse());
  }, []);

  const handleMouseMove = useCallback((e) => {
    if (drag) {
      const { x, y } = svgMouseCoords(e);
      setPositions((p) => ({
        ...p,
        [drag.id]: {
          x: Math.max(0, Math.round((x - drag.offsetX) / GRID) * GRID),
          y: Math.max(0, Math.round((y - drag.offsetY) / GRID) * GRID)
        }
      }));
    } else if (connecting) {
      const { x, y } = svgMouseCoords(e);
      setConnecting((c) => ({ ...c, x, y }));
    }
  }, [drag, connecting, svgMouseCoords]);

  const handleMouseUp = useCallback(() => {
    setDrag(null);
    setConnecting(null);
  }, []);

  const handleNodeMouseDown = useCallback((e, id) => {
    if (readOnly || e.button !== 0) return;
    if (e.target.dataset && e.target.dataset.port) return;
    const pos = positions[id];
    if (!pos) return;
    const { x, y } = svgMouseCoords(e);
    setDrag({ id, offsetX: x - pos.x, offsetY: y - pos.y });
    setSelectedNode(id);
    e.stopPropagation();
  }, [positions, svgMouseCoords, readOnly]);

  const handlePortMouseDown = useCallback((e, source, kind) => {
    if (readOnly) return;
    e.stopPropagation();
    const { x, y } = svgMouseCoords(e);
    setConnecting({ source, kind, x, y });
  }, [svgMouseCoords, readOnly]);

  const handleNodeMouseUp = useCallback((e, target) => {
    if (!connecting || readOnly) return;
    e.stopPropagation();
    if (connecting.source === target) { setConnecting(null); return; }
    const next = { ...(transitions || {}) };
    next[connecting.source] = setEdgeTarget(next[connecting.source], connecting.kind, target);
    if (onChange) onChange(next);
    setConnecting(null);
  }, [connecting, transitions, onChange, readOnly]);

  const addStep = () => {
    const name = newStepName.trim();
    if (!name || readOnly) return;
    if (keys.includes(name)) return;
    const next = { ...(transitions || {}) };
    next[name] = {};
    if (onChange) onChange(next);
    setNewStepName('');
  };

  const removeStep = (id) => {
    if (readOnly) return;
    const next = { ...(transitions || {}) };
    delete next[id];
    for (const k of Object.keys(next)) {
      const t = next[k] || {};
      for (const kind of ['OnSuccess', 'OnFailure', 'OnException', 'onSuccess', 'onFailure', 'onException']) {
        if (t[kind] === id) {
          const copy = { ...t };
          copy[kind] = null;
          next[k] = copy;
        }
      }
    }
    if (onChange) onChange(next);
    if (selectedNode === id) setSelectedNode(null);
  };

  const clearEdge = (source, kind) => {
    if (readOnly) return;
    const next = { ...(transitions || {}) };
    next[source] = setEdgeTarget(next[source], kind, null);
    if (onChange) onChange(next);
  };

  const viewBox = useMemo(() => {
    const pts = Object.values(positions);
    const maxX = Math.max(900, ...pts.map((p) => p.x + NODE_W + 60));
    const maxY = Math.max(480, ...pts.map((p) => p.y + NODE_H + 60));
    return '0 0 ' + maxX + ' ' + maxY;
  }, [positions]);

  const selectedTrans = selectedNode ? getTransition(transitions, selectedNode) : null;

  return (
    <div>
      {!readOnly && (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 'var(--spacing-sm)' }}>
          <input
            placeholder="New step id (e.g. validate)"
            value={newStepName}
            onChange={(e) => setNewStepName(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addStep(); } }}
            style={{ maxWidth: 260 }}
          />
          <button type="button" className="button-secondary" onClick={addStep}>+ Add step</button>
          <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', marginLeft: 'auto' }}>
            Drag nodes to move. Drag from a colored port to connect. Green=success, amber=failure, red=exception.
          </div>
        </div>
      )}

      <div className="flow-graph-wrapper">
        <svg
          className="flow-graph"
          ref={svgRef}
          viewBox={viewBox}
          preserveAspectRatio="xMinYMin meet"
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onMouseLeave={handleMouseUp}
        >
          <defs>
            <marker id="arr-success" markerWidth="10" markerHeight="10" refX="8" refY="5" orient="auto"><path d="M0,0 L10,5 L0,10 Z" fill="var(--color-success)" /></marker>
            <marker id="arr-failure" markerWidth="10" markerHeight="10" refX="8" refY="5" orient="auto"><path d="M0,0 L10,5 L0,10 Z" fill="var(--color-warning)" /></marker>
            <marker id="arr-exception" markerWidth="10" markerHeight="10" refX="8" refY="5" orient="auto"><path d="M0,0 L10,5 L0,10 Z" fill="var(--color-danger)" /></marker>
          </defs>

          {keys.flatMap((sourceId) => {
            const t = transitions[sourceId];
            const from = positions[sourceId];
            if (!t || !from) return [];
            return ['OnSuccess', 'OnFailure', 'OnException'].flatMap((kind) => {
              const target = getEdgeTarget(t, kind);
              if (!target) return [];
              const to = positions[target];
              if (!to) return [];
              const kindClass = kind === 'OnSuccess' ? 'success' : kind === 'OnFailure' ? 'failure' : 'exception';
              const x1 = from.x + NODE_W;
              const y1 = from.y + portY(kind);
              const x2 = to.x;
              const y2 = to.y + NODE_H / 2;
              const mid = (x1 + x2) / 2;
              const path = 'M ' + x1 + ' ' + y1 + ' C ' + mid + ' ' + y1 + ' ' + mid + ' ' + y2 + ' ' + x2 + ' ' + y2;
              return [
                <g key={sourceId + ':' + kind}>
                  <path className={'flow-edge ' + kindClass} d={path} markerEnd={'url(#arr-' + kindClass + ')'} />
                  {!readOnly && (
                    <circle cx={(x1 + x2) / 2} cy={(y1 + y2) / 2} r={6}
                      fill="var(--color-surface)" stroke="var(--color-border)" strokeWidth="1"
                      onClick={() => clearEdge(sourceId, kind)} style={{ cursor: 'pointer' }}>
                      <title>Remove edge</title>
                    </circle>
                  )}
                </g>
              ];
            });
          })}

          {keys.map((id) => {
            const pos = positions[id] || { x: 0, y: 0 };
            const isStart = id === startStepId;
            const isSelected = selectedNode === id;
            return (
              <g
                key={id}
                className={'flow-node' + (isStart ? ' start' : '') + (isSelected ? ' selected' : '')}
                transform={'translate(' + pos.x + ',' + pos.y + ')'}
                onMouseDown={(e) => handleNodeMouseDown(e, id)}
                onMouseUp={(e) => handleNodeMouseUp(e, id)}
                style={{ cursor: readOnly ? 'default' : 'move' }}
              >
                <rect width={NODE_W} height={NODE_H} rx="10" ry="10" />
                <text x={NODE_W / 2} y={26} textAnchor="middle" style={{ fontWeight: 700 }}>{id}</text>
                <text x={NODE_W / 2} y={44} textAnchor="middle" style={{ fill: 'var(--color-text-secondary)', fontSize: 11 }}>
                  {(transitions[id]?.StepType || transitions[id]?.stepType || 'Code').toString()}
                </text>

                <circle cx={NODE_W} cy={portY('OnSuccess')} r={6} fill="var(--color-success)" data-port="OnSuccess"
                  onMouseDown={(e) => handlePortMouseDown(e, id, 'OnSuccess')} style={{ cursor: 'crosshair' }}>
                  <title>OnSuccess · drag to connect</title>
                </circle>
                <circle cx={NODE_W} cy={portY('OnFailure')} r={6} fill="var(--color-warning)" data-port="OnFailure"
                  onMouseDown={(e) => handlePortMouseDown(e, id, 'OnFailure')} style={{ cursor: 'crosshair' }}>
                  <title>OnFailure · drag to connect</title>
                </circle>
                <circle cx={NODE_W} cy={portY('OnException')} r={6} fill="var(--color-danger)" data-port="OnException"
                  onMouseDown={(e) => handlePortMouseDown(e, id, 'OnException')} style={{ cursor: 'crosshair' }}>
                  <title>OnException · drag to connect</title>
                </circle>

                {!readOnly && isSelected && (
                  <g onClick={(e) => { e.stopPropagation(); removeStep(id); }} style={{ cursor: 'pointer' }}>
                    <circle cx={NODE_W - 10} cy={10} r={8} fill="var(--color-danger)" />
                    <text x={NODE_W - 10} y={14} textAnchor="middle" fill="white" fontSize="12" fontWeight="700">×</text>
                  </g>
                )}
              </g>
            );
          })}

          {connecting && positions[connecting.source] && (
            <path
              className={'flow-edge ' + (connecting.kind === 'OnSuccess' ? 'success' : connecting.kind === 'OnFailure' ? 'failure' : 'exception')}
              d={'M ' + (positions[connecting.source].x + NODE_W) + ' ' + (positions[connecting.source].y + portY(connecting.kind)) + ' L ' + connecting.x + ' ' + connecting.y}
              strokeDasharray="4 4"
            />
          )}
        </svg>
      </div>

      {selectedNode && selectedTrans && !readOnly && (
        <div className="card" style={{ marginTop: 'var(--spacing-sm)' }}>
          <div className="card-title" style={{ marginBottom: 'var(--spacing-sm)' }}>Step: {selectedNode}</div>
          <div className="grid-2">
            <div className="form-row">
              <label>On success → </label>
              <select value={getEdgeTarget(selectedTrans, 'OnSuccess') || ''} onChange={(e) => onChange({ ...transitions, [selectedNode]: setEdgeTarget(selectedTrans, 'OnSuccess', e.target.value || null) })}>
                <option value="">(terminate)</option>
                {keys.filter((k) => k !== selectedNode).map((k) => <option key={k} value={k}>{k}</option>)}
              </select>
            </div>
            <div className="form-row">
              <label>On failure → </label>
              <select value={getEdgeTarget(selectedTrans, 'OnFailure') || ''} onChange={(e) => onChange({ ...transitions, [selectedNode]: setEdgeTarget(selectedTrans, 'OnFailure', e.target.value || null) })}>
                <option value="">(terminate)</option>
                {keys.filter((k) => k !== selectedNode).map((k) => <option key={k} value={k}>{k}</option>)}
              </select>
            </div>
            <div className="form-row">
              <label>On exception → </label>
              <select value={getEdgeTarget(selectedTrans, 'OnException') || ''} onChange={(e) => onChange({ ...transitions, [selectedNode]: setEdgeTarget(selectedTrans, 'OnException', e.target.value || null) })}>
                <option value="">(terminate)</option>
                {keys.filter((k) => k !== selectedNode).map((k) => <option key={k} value={k}>{k}</option>)}
              </select>
            </div>
            <div className="form-row">
              <label>Max transitions (0 = unlimited)</label>
              <input type="number" min="0" value={selectedTrans.MaxTransitions ?? selectedTrans.maxTransitions ?? 0} onChange={(e) => {
                const copy = { ...selectedTrans };
                copy.MaxTransitions = Math.max(0, parseInt(e.target.value || '0', 10));
                onChange({ ...transitions, [selectedNode]: copy });
              }} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default DataFlowGraphEditor;
