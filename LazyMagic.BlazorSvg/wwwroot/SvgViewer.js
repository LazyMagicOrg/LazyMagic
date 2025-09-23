// SvgViewer class to handle multiple instances
class SvgViewerInstance {
    constructor(containerId, dotNetObjectReference, disableSelection = false) {
        this.containerId = containerId;
        this.dotNetObjectReference = dotNetObjectReference;
        this.disableSelection = disableSelection;
        this.s = null;
        this.fillColor = "#f00";
        this.boundingBoxRect = null;
        this.isUpdating = false;
        this.boundingBoxPathIds = new Set();
        this.selectedIds = null;
        this.selectedPaths = null;
        this.layers = {};
        this.activeLayerKey = null;
        this.pathGroups = new Map(); // Map of groupId -> Set of pathIds
        this.pathToGroup = new Map(); // Map of pathId -> groupId
        this.groupColors = ['#FF6B6B', '#4ECDC4', '#45B7D1', '#96CEB4', '#FFEAA7', '#DDA0DD', '#98D8C8', '#F7DC6F'];
        this.nextGroupId = 1;
    }

    // Return the inner <svg> if present, otherwise the paper itself
    rootSvg() {
        return this.s ? (this.s.select("svg") || this.s) : null;
    }

    // Active scope = current layer group (or inner <svg>/paper if none detected)
    scope() {
        const root = this.rootSvg();
        return (this.activeLayerKey && this.layers[this.activeLayerKey])
            ? this.layers[this.activeLayerKey]
            : root;
    }

    // Find nearest ancestor that is an Inkscape layer; return its label/id key
    findLayerKeyFromNode(node) {
        let el = node;
        while (el && el.nodeType === 1) {
            const gm = el.getAttribute('inkscape:groupmode');
            if (gm === 'layer') {
                const label = el.getAttribute('inkscape:label');
                const id = el.getAttribute('id');
                const key = (label || id || '').trim();
                return key || null;
            }
            el = el.parentNode;
        }
        return null;
    }

    // TRUE if a node is inside the active layer (or no layers detected)
    isInActiveLayer(domNode) {
        if (!this.activeLayerKey || !this.layers[this.activeLayerKey]) return true;
        return this.findLayerKeyFromNode(domNode) === this.activeLayerKey;
    }

    // Discover layers by Inkscape attributes
    bootstrapLayers() {
        this.layers = {};
        const nodes = this.s.selectAll('g[inkscape\\:groupmode="layer"]');
        nodes.forEach(g => {
            const label = g.attr('inkscape:label') || g.attr('id');
            if (!label) return;
            const key = String(label).trim();
            if (key && !this.layers[key]) this.layers[key] = g;
        });
    }

    // Activate a layer by key (label/id). Clears selection to avoid cross-layer mixes.
    activateLayer(name) {
        if (!this.s) return false;
        if (!this.layers[name]) return false;

        this.unselectAllPaths();
        this.activeLayerKey = name;

        // Visual cue: dim non-active layers
        Object.entries(this.layers).forEach(([k, g]) => {
            g.attr({ opacity: k === this.activeLayerKey ? 1 : 0.6 });
        });

        return true;
    }

    // Path Grouping Functions

    // Calculate distance between two path bounding boxes
    calculatePathDistance(path1, path2) {
        const bbox1 = path1.getBBox();
        const bbox2 = path2.getBBox();

        // Calculate center points
        const center1 = { x: bbox1.x + bbox1.width / 2, y: bbox1.y + bbox1.height / 2 };
        const center2 = { x: bbox2.x + bbox2.width / 2, y: bbox2.y + bbox2.height / 2 };

        // Calculate Euclidean distance between centers
        return Math.sqrt(Math.pow(center2.x - center1.x, 2) + Math.pow(center2.y - center1.y, 2));
    }

    // Group currently selected paths together
    groupSelectedPaths() {
        if (!this.s) return null;

        this.getPaths();
        const selectedIds = Array.from(this.selectedIds);

        if (selectedIds.length < 2) {
            console.log("Need at least 2 selected paths to create a group");
            return null;
        }

        // Clear all existing groups first (only allow one group at a time)
        this.clearAllGroups();

        const groupId = `selected_group_${this.nextGroupId++}`;
        this.createGroup(groupId, selectedIds);
        return groupId;
    }

    // Create a new group
    createGroup(groupId, pathIds) {
        if (this.pathGroups.has(groupId)) return false;

        const pathSet = new Set(pathIds);
        this.pathGroups.set(groupId, pathSet);

        // Update path-to-group mapping
        pathIds.forEach(pathId => {
            this.pathToGroup.set(pathId, groupId);
        });

        this.visualizeGroups();
        return true;
    }

    // Remove a group
    removeGroup(groupId) {
        if (!this.pathGroups.has(groupId)) return false;

        const pathIds = this.pathGroups.get(groupId);
        pathIds.forEach(pathId => {
            this.pathToGroup.delete(pathId);
        });

        this.pathGroups.delete(groupId);
        this.visualizeGroups();
        return true;
    }

    // Add path to existing group
    addPathToGroup(pathId, groupId) {
        if (!this.pathGroups.has(groupId)) return false;
        if (this.pathToGroup.has(pathId)) return false; // Already in a group

        this.pathGroups.get(groupId).add(pathId);
        this.pathToGroup.set(pathId, groupId);
        this.visualizeGroups();
        return true;
    }

    // Remove path from its group
    removePathFromGroup(pathId) {
        const groupId = this.pathToGroup.get(pathId);
        if (!groupId) return false;

        this.pathGroups.get(groupId).delete(pathId);
        this.pathToGroup.delete(pathId);

        // Remove group if it has less than 2 paths
        if (this.pathGroups.get(groupId).size < 2) {
            this.removeGroup(groupId);
        } else {
            this.visualizeGroups();
        }
        return true;
    }

    // Select entire group when one path in group is selected
    selectGroup(groupId) {
        if (!this.pathGroups.has(groupId)) return false;

        const pathIds = Array.from(this.pathGroups.get(groupId));
        this.selectPaths(pathIds);
        return true;
    }

    // Get group information
    getGroupInfo(pathId) {
        const groupId = this.pathToGroup.get(pathId);
        if (!groupId) return null;

        return {
            groupId: groupId,
            pathIds: Array.from(this.pathGroups.get(groupId)),
            color: this.getGroupColor(groupId)
        };
    }

    // Get color for a group
    getGroupColor(groupId) {
        const groupIndex = Array.from(this.pathGroups.keys()).indexOf(groupId);
        return this.groupColors[groupIndex % this.groupColors.length];
    }

    // --------- OUTLINE GENERATION (smart concave, gap-aware) ---------

    // Generate tight-fitting concave outline for a set of paths.
    // This ALWAYS returns a path (falls back to convex), so we always draw something.
    generateGroupOutline(pathIds, options = {}) {
        if (!this.s || !pathIds || pathIds.length === 0) return null;

        const {
            gapHopPx = 8,
            sampleStride = 1,
            kStart = 4,
            kMax = 24,
            maxEdgePx = 200,
            downsampleEveryN = 1,
            minContainment = 0.75   // draw if at least 75% of sampled points are inside
        } = options;

        const scope = this.scope();
        const groupPaths = [];
        pathIds.forEach(id => {
            const p = scope.select("#" + id);
            if (p) groupPaths.push(p);
        });
        if (groupPaths.length === 0) return null;

        const allBoundaryPoints = [];
        const pathInfos = [];
        groupPaths.forEach(p => {
            const bbox = p.getBBox();
            const center = { x: bbox.x + bbox.width / 2, y: bbox.y + bbox.height / 2 };
            const raw = this.extractPathBoundaryPoints(p, bbox);
            const sampled = sampleStride > 1 ? this._downsamplePoints(raw, sampleStride) : raw;

            pathInfos.push({ bbox, center, boundaryPoints: sampled });
            allBoundaryPoints.push(...sampled);
        });

        if (allBoundaryPoints.length < 3) {
            // Last resort: triangle around union bbox
            const union = this.unionTransformedBBoxes(groupPaths);
            if (!union) return null;
            const tri = [
                { x: union.x, y: union.y },
                { x: union.x + union.width, y: union.y },
                { x: union.x + union.width, y: union.y + union.height }
            ];
            return `M ${tri[0].x} ${tri[0].y} L ${tri[1].x} ${tri[1].y} L ${tri[2].x} ${tri[2].y} Z`;
        }

        // Seed tiny bridges between selected shapes only
        const bridges = this._seedBridgePoints(pathInfos, gapHopPx, Math.max(4, sampleStride * 2));
        const cloud = (downsampleEveryN > 1)
            ? this._downsamplePoints(allBoundaryPoints.concat(bridges), downsampleEveryN)
            : allBoundaryPoints.concat(bridges);

        // Try concave hull
        let hull = this._concaveHull(cloud, kStart, kMax, maxEdgePx);
        if (!hull || hull.length < 3) {
            // Fallback to convex
            hull = this.simpleConvexHull(cloud);
        }

        // Soft containment gate: draw anyway if threshold met; else convex fallback
        const score = this._validateContainmentScore(hull, pathInfos);
        if (score < minContainment) {
            hull = this.simpleConvexHull(cloud);
        }

        if (!hull || hull.length < 3) return null;

        let d = `M ${hull[0].x} ${hull[0].y}`;
        for (let i = 1; i < hull.length; i++) d += ` L ${hull[i].x} ${hull[i].y}`;
        d += " Z";
        return d;
    }

    // Extract boundary points that follow the actual path edges closely
    // Uses native SVGPathElement APIs via path.node with solid fallbacks.
    extractPathBoundaryPoints(path, bbox) {
        const pts = [];
        try {
            const node = path && path.node ? path.node : path;
            if (node && typeof node.getTotalLength === "function" && typeof node.getPointAtLength === "function") {
                const len = node.getTotalLength();
                if (isFinite(len) && len > 0) {
                    // Sample ~every 3–8 px, capped for very long paths
                    const step = Math.max(3, Math.min(8, Math.floor(len / 250) || 4));
                    for (let d = 0; d <= len; d += step) {
                        const p = node.getPointAtLength(d);
                        if (isFinite(p.x) && isFinite(p.y)) pts.push({ x: p.x, y: p.y });
                    }
                }
            }
        } catch (e) {
            // ignore and fall back below
        }

        // If too few samples, add a light bbox “ring”
        if (pts.length < 8 && bbox) {
            const margin = 1.5;
            const n = 16;
            for (let i = 0; i < n; i++) {
                const t = i / n;
                // top
                pts.push({ x: bbox.x + bbox.width * t, y: bbox.y - margin });
                // right
                pts.push({ x: bbox.x + bbox.width + margin, y: bbox.y + bbox.height * t });
                // bottom
                pts.push({ x: bbox.x + bbox.width * (1 - t), y: bbox.y + bbox.height + margin });
                // left
                pts.push({ x: bbox.x - margin, y: bbox.y + bbox.height * (1 - t) });
            }
        }

        // Absolute last resort: the 4 corners
        if (pts.length === 0 && bbox) {
            pts.push({ x: bbox.x, y: bbox.y });
            pts.push({ x: bbox.x + bbox.width, y: bbox.y });
            pts.push({ x: bbox.x + bbox.width, y: bbox.y + bbox.height });
            pts.push({ x: bbox.x, y: bbox.y + bbox.height });
        }
        return pts;
    }

    // Check if two line segments intersect
    doLinesIntersect(p1, p2, p3, p4) {
        const orientation = (p, q, r) => {
            const val = (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);
            if (val === 0) return 0; // Collinear
            return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
        };

        const o1 = orientation(p1, p2, p3);
        const o2 = orientation(p1, p2, p4);
        const o3 = orientation(p3, p4, p1);
        const o4 = orientation(p3, p4, p2);

        // General case
        if (o1 !== o2 && o3 !== o4) return true;

        return false; // No intersection
    }

    // Point-in-polygon test using ray casting algorithm
    isPointInPolygon(point, polygon) {
        let inside = false;
        const x = point.x;
        const y = point.y;

        for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
            const xi = polygon[i].x;
            const yi = polygon[i].y;
            const xj = polygon[j].x;
            const yj = polygon[j].y;

            if (((yi > y) !== (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) {
                inside = !inside;
            }
        }

        return inside;
    }

    // Check for self-intersection in hull
    hasSelfintersection(hull) {
        for (let i = 0; i < hull.length; i++) {
            const edge1Start = hull[i];
            const edge1End = hull[(i + 1) % hull.length];

            for (let j = i + 2; j < hull.length; j++) {
                if (j === hull.length - 1 && i === 0) continue; // Skip adjacent edges

                const edge2Start = hull[j];
                const edge2End = hull[(j + 1) % hull.length];

                if (this.doLinesIntersect(edge1Start, edge1End, edge2Start, edge2End)) {
                    return true; // Has self-intersection
                }
            }
        }

        return false; // No self-intersection
    }

    // Smart containment scoring (0..1)
    _validateContainmentScore(hull, pathInfos) {
        let ok = 0, total = 0;

        for (const pathInfo of pathInfos) {
            const bp = pathInfo.boundaryPoints || [];
            if (bp.length === 0) continue;

            const sampleSize = Math.min(bp.length, 16);
            const step = Math.max(1, Math.floor(bp.length / sampleSize));

            for (let i = 0; i < bp.length; i += step) {
                total++;
                if (this.isPointInPolygon(bp[i], hull)) ok++;
            }

            // 4 corners of bbox
            const b = pathInfo.bbox;
            const corners = [
                { x: b.x, y: b.y },
                { x: b.x + b.width, y: b.y },
                { x: b.x + b.width, y: b.y + b.height },
                { x: b.x, y: b.y + b.height }
            ];
            for (const c of corners) {
                total++;
                if (this.isPointInPolygon(c, hull)) ok++;
            }
        }

        return total > 0 ? ok / total : 1;
    }

    // Remove duplicate points
    removeDuplicates(points, tolerance) {
        const unique = [];
        for (const point of points) {
            const isDuplicate = unique.some(up =>
                Math.abs(up.x - point.x) < tolerance && Math.abs(up.y - point.y) < tolerance
            );
            if (!isDuplicate) {
                unique.push(point);
            }
        }
        return unique;
    }

    // Simple convex hull (gift wrapping)
    simpleConvexHull(points) {
        if (points.length < 3) return points;

        const pts = this.removeDuplicates(points, 0.5);
        if (pts.length < 3) return pts;

        // Find leftmost point
        let leftmost = 0;
        for (let i = 1; i < pts.length; i++) {
            if (pts[i].x < pts[leftmost].x) {
                leftmost = i;
            }
        }

        const hull = [];
        let current = leftmost;

        const orientation = (p, q, r) => {
            const val = (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);
            if (val === 0) return 0; // Collinear
            return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
        };

        do {
            hull.push(pts[current]);
            let next = (current + 1) % pts.length;

            for (let i = 0; i < pts.length; i++) {
                if (orientation(pts[current], pts[i], pts[next]) === 2) {
                    next = i;
                }
            }

            current = next;
        } while (current !== leftmost && hull.length < pts.length);

        return hull;
    }

    // Orientation helper for convex hull (kept for callers)
    orientation(p, q, r) {
        const val = (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);
        if (val === 0) return 0; // Collinear
        return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
    }

    // Calculate distance between two points
    calculateDistance(p1, p2) {
        return Math.sqrt(Math.pow(p2.x - p1.x, 2) + Math.pow(p2.y - p1.y, 2));
    }

    // Downsample helper
    _downsamplePoints(points, everyN = 2) {
        if (everyN <= 1) return points;
        const out = [];
        for (let i = 0; i < points.length; i += everyN) out.push(points[i]);
        return out;
    }

    // Seed midpoints across tiny gaps between selected shapes only
    _seedBridgePoints(pathInfos, maxGap = 10, sampleStride = 8) {
        const bridgePts = [];
        for (let i = 0; i < pathInfos.length; i++) {
            const A = pathInfos[i].boundaryPoints;
            if (!A || A.length === 0) continue;
            for (let j = i + 1; j < pathInfos.length; j++) {
                const B = pathInfos[j].boundaryPoints;
                if (!B || B.length === 0) continue;

                // quick center distance prune
                const c1 = pathInfos[i].center, c2 = pathInfos[j].center;
                if (this.calculateDistance(c1, c2) > maxGap * 6) continue;

                let best = { d: Infinity, p: null, q: null };
                for (let a = 0; a < A.length; a += sampleStride) {
                    const p = A[a];
                    for (let b = 0; b < B.length; b += sampleStride) {
                        const q = B[b];
                        const d = this.calculateDistance(p, q);
                        if (d < best.d) best = { d, p, q };
                    }
                }
                if (best.p && best.d > 1.5 && best.d <= maxGap) {
                    bridgePts.push({ x: (best.p.x + best.q.x) * 0.5, y: (best.p.y + best.q.y) * 0.5 });
                }
            }
        }
        return bridgePts;
    }

    // Concave hull (k-nearest) with self-intersection + max-edge guardrails
    _concaveHull(points, kStart = 3, kMax = 20, maxEdge = Infinity) {
        if (!points || points.length < 3) return null;

        const pts = this.removeDuplicates(points, 0.5);
        if (pts.length < 3) return null;

        const angle = (o, a, b) => {
            const v1x = a.x - o.x, v1y = a.y - o.y;
            const v2x = b.x - o.x, v2y = b.y - o.y;
            const ang1 = Math.atan2(v1y, v1x);
            const ang2 = Math.atan2(v2y, v2x);
            let ang = ang2 - ang1;
            if (ang <= -Math.PI) ang += 2 * Math.PI;
            if (ang > Math.PI) ang -= 2 * Math.PI;
            return ang;
        };

        // lowest Y (then lowest X)
        let start = pts.reduce((acc, p) =>
            (p.y < acc.y || (p.y === acc.y && p.x < acc.x)) ? p : acc, pts[0]);

        for (let k = Math.max(3, kStart); k <= Math.max(kStart, kMax); k++) {
            let hull = [start];
            let current = start;
            let prev = { x: start.x - 1, y: start.y };
            const used = new Set([`${start.x.toFixed(2)}_${start.y.toFixed(2)}`]);

            let safety = 0, closed = false;
            while (safety++ < pts.length * 10) {
                const neighbors = pts
                    .map(p => ({ p, d: this.calculateDistance(p, current) }))
                    .filter(e => e.d > 0.0001)
                    .sort((a, b) => a.d - b.d)
                    .slice(0, k)
                    .map(e => e.p);

                neighbors.sort((a, b) => {
                    const A = angle(current, prev, a);
                    const B = angle(current, prev, b);
                    return A - B;
                });

                let next = null;
                for (const cand of neighbors) {
                    if (this.calculateDistance(current, cand) > maxEdge) continue;
                    const key = `${cand.x.toFixed(2)}_${cand.y.toFixed(2)}`;
                    if (used.has(key) && !(cand.x === start.x && cand.y === start.y)) continue;

                    const tmp = hull.concat([cand]);
                    if (tmp.length >= 4 && this.hasSelfintersection(tmp)) continue;

                    next = cand;
                    break;
                }

                if (!next) {
                    if (hull.length > 2 && this.calculateDistance(current, start) <= maxEdge) {
                        const tmp = hull.concat([start]);
                        if (!this.hasSelfintersection(tmp)) {
                            hull = tmp;
                            closed = true;
                            break;
                        }
                    }
                    break;
                }

                hull.push(next);
                used.add(`${next.x.toFixed(2)}_${next.y.toFixed(2)}`);
                prev = current;
                current = next;

                if (current === start && hull.length >= 4) {
                    closed = true;
                    break;
                }
            }

            if (closed) {
                // trim duplicate start if present
                const last = hull[hull.length - 1];
                if (Math.abs(last.x - start.x) < 0.001 && Math.abs(last.y - start.y) < 0.001) hull.pop();
                if (!this.hasSelfintersection(hull)) return hull;
            }
        }

        // Fallback to convex hull if concave fails
        return this.simpleConvexHull(pts);
    }

    // --------- SELECTION / RENDERING ---------

    // Clear all groups
    clearAllGroups() {
        this.pathGroups.clear();
        this.pathToGroup.clear();
        this.nextGroupId = 1;
        this.visualizeGroups();
    }

    // Visualize groups AND the live selection perimeter directly on the SVG
    visualizeGroups() {
        if (!this.s) return;

        const scope = this.scope();

        // Remove existing outlines (selection + groups)
        scope.selectAll(".group-outline").remove();

        // 1) Live selection perimeter (even when not grouped)
        this.getPaths();
        const selectedIds = Array.from(this.selectedIds || []);
        if (selectedIds.length > 0) {
            const selectionPathData = this.generateGroupOutline(selectedIds, {
                gapHopPx: 8,        // tiny gaps only
                kStart: 4,
                kMax: 24,
                maxEdgePx: 200,     // avoids long jumps
                sampleStride: 1,
                downsampleEveryN: 1
            });

            if (selectionPathData) {
                const selectionOutline = scope.path(selectionPathData);
                selectionOutline.attr({
                    stroke: '#00AEEF',              // cyan for live selection
                    strokeWidth: 3,
                    fill: 'none',
                    strokeDasharray: '10 5',
                    'stroke-opacity': 0.9,
                    'vector-effect': 'non-scaling-stroke',
                    'pointer-events': 'none'
                });
                selectionOutline.addClass("group-outline"); // easy cleanup
            }
        }

        // 2) Reset path colors to original (unless selected) + draw group outlines
        scope.selectAll("path").forEach(path => {
            const id = path.attr("id");
            const groupId = this.pathToGroup.get(id);

            if (!path.data("isSelected")) {
                if (groupId) {
                    // Apply group color as stroke
                    const groupColor = this.getGroupColor(groupId);
                    path.attr({
                        stroke: groupColor,
                        strokeWidth: 3,
                        'stroke-opacity': 0.7
                    });
                } else {
                    // Reset to default stroke
                    path.attr({
                        stroke: path.data("originalStroke") || "#000",
                        strokeWidth: path.data("originalStrokeWidth") || 1,
                        'stroke-opacity': 1
                    });
                }
            }
        });

        // 3) Create accurate group outlines (if any groups)
        this.pathGroups.forEach((pathIds, groupId) => {
            if (pathIds.size > 1) {
                const pathIdsArray = Array.from(pathIds);
                const outlinePathData = this.generateGroupOutline(pathIdsArray, {
                    gapHopPx: 8,
                    kStart: 4,
                    kMax: 24,
                    maxEdgePx: 200,
                    sampleStride: 1,
                    downsampleEveryN: 1
                });

                if (outlinePathData) {
                    const groupColor = this.getGroupColor(groupId);
                    const groupOutline = scope.path(outlinePathData);

                    groupOutline.attr({
                        stroke: groupColor,
                        strokeWidth: 3,
                        fill: 'none',
                        strokeDasharray: '10 5',
                        'stroke-opacity': 0.8,
                        'vector-effect': 'non-scaling-stroke',
                        "pointer-events": "none"
                    });

                    groupOutline.addClass("group-outline");

                    // Add group label at the center of the outline
                    const bbox = this.unionTransformedBBoxes(pathIdsArray.map(id => scope.select("#" + id)).filter(p => p));
                    if (bbox) {
                        const label = scope.text(
                            bbox.x + bbox.width / 2,
                            bbox.y - 15,
                            `Group: ${groupId.replace(/^selected_group_/, 'G')}`
                        );

                        label.attr({
                            'text-anchor': 'middle',
                            'font-size': '14px',
                            'font-family': 'Arial, sans-serif',
                            fill: groupColor,
                            'font-weight': 'bold',
                            "pointer-events": "none"
                        });

                        label.addClass("group-outline");
                    }
                }
            }
        });
    }

    async loadSvgAsync(svgContent) {
        if (typeof Snap === 'undefined') {
            throw new Error('Snap.svg is not loaded. Please call initAsync first.');
        }

        if (this.s) {
            this.s.selectAll("path").forEach((path) => {
                path.node.removeEventListener("click", this.handleSelection.bind(this));
            });
            this.boundingBoxRect = null;
            this.boundingBoxPathIds.clear();
        }

        let svgElement = document.querySelector(`#${this.containerId}`);
        if (!svgElement) {
            throw new Error(`SVG element with ID "${this.containerId}" not found.`);
        }

        const response = await fetch(svgContent);
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
        const svgText = await response.text();

        const fragment = Snap.parse(svgText);
        if (fragment) {
            this.s = Snap(`#${this.containerId}`);
            this.s.append(fragment);

            // Make SVG responsive
            const svgEl = this.s.select("svg");
            if (svgEl) {
                svgEl.attr({
                    width: "100%",
                    height: "100%",
                    preserveAspectRatio: "xMidYMid meet"
                });
            }

            // Initialize per-path data + click handler (only if selection is enabled)
            this.s.selectAll("path").forEach((path) => {
                if (!this.disableSelection) {
                    path.node.addEventListener("click", this.handleSelection.bind(this));
                }
                path.data("isSelected", false);
                path.data("originalColor", path.attr("fill"));
                path.data("originalStroke", path.attr("stroke"));
                path.data("originalStrokeWidth", path.attr("stroke-width"));
            });

            // Discover Inkscape layers
            this.bootstrapLayers();
            const keys = Object.keys(this.layers);
            if (keys.length > 0) {
                this.activateLayer(keys[0]);
            } else {
                this.activeLayerKey = null;
            }
        } else {
            throw new Error('Svg could not be parsed');
        }
    }

    handleSelection(event) {
        // Do nothing if selection is disabled
        if (this.disableSelection) return;

        const path = Snap(event.target);

        // Auto-activate layer if clicked
        const targetKey = this.findLayerKeyFromNode(path.node);
        if (targetKey && this.layers[targetKey] && targetKey !== this.activeLayerKey) {
            this.activateLayer(targetKey);
        }

        if (!this.isInActiveLayer(path.node)) return;

        const id = path.attr("id");
        const isSelected = path.data("isSelected");
        const groupId = this.pathToGroup.get(id);

        // Check if Ctrl key is held for group selection
        const selectWholeGroup = event.ctrlKey && groupId;

        if (isSelected) {
            if (selectWholeGroup) {
                // Unselect entire group
                const pathIds = Array.from(this.pathGroups.get(groupId));
                pathIds.forEach(pathId => {
                    this.unselectPath(pathId);
                    this.dotNetObjectReference.invokeMethodAsync("OnPathUnselected", pathId);
                });
            } else {
                this.unselectPath(id);
                this.dotNetObjectReference.invokeMethodAsync("OnPathUnselected", id);
            }
        } else {
            if (selectWholeGroup) {
                // Select entire group
                const pathIds = Array.from(this.pathGroups.get(groupId));
                pathIds.forEach(pathId => {
                    this.selectPath(pathId);
                    this.dotNetObjectReference.invokeMethodAsync("OnPathSelected", pathId);
                });
            } else {
                this.selectPath(id);
                this.dotNetObjectReference.invokeMethodAsync("OnPathSelected", id);
            }
        }

        this.getPaths();
        const mySelectedIds = Array.from(this.selectedIds);
        this.dotNetObjectReference.invokeMethodAsync("OnPathsChanged", mySelectedIds);
    }

    // Compute union of transformed bboxes (no cloning, includes transforms)
    unionTransformedBBoxes(nodes) {
        if (!nodes || nodes.length === 0) return null;
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

        nodes.forEach(p => {
            const b = p.getBBox();
            minX = Math.min(minX, b.x);
            minY = Math.min(minY, b.y);
            maxX = Math.max(maxX, b.x + b.width);
            maxY = Math.max(maxY, b.y + b.height);
        });

        return { x: minX, y: minY, width: (maxX - minX), height: (maxY - minY) };
    }

    computeIdsInsideBoundingBox(overlapThreshold = 0.5) {
        const inside = new Set();
        if (!this.boundingBoxRect || !this.s) return inside;

        // Use transformed bboxes for both rect and paths to compare consistently
        const bbox = this.boundingBoxRect.getBBox();
        const allPaths = this.scope().selectAll("path");

        allPaths.forEach(p => {
            const b = p.getBBox();

            const ix1 = Math.max(b.x, bbox.x);
            const iy1 = Math.max(b.y, bbox.y);
            const ix2 = Math.min(b.x + b.width, bbox.x + bbox.width);
            const iy2 = Math.min(b.y + b.height, bbox.y + bbox.height);

            const iw = Math.max(0, ix2 - ix1);
            const ih = Math.max(0, iy2 - iy1);
            const inter = iw * ih;
            const area = b.width * b.height;

            if (area > 0 && inter / area >= overlapThreshold) {
                const id = p.attr("id");
                if (id) inside.add(id);
            }
        });

        return inside;
    }

    autoSelectInBoundingBox() {
        if (!this.boundingBoxRect || !this.s) return;

        const insideIds = this.computeIdsInsideBoundingBox(0.5);
        const previouslyUpdating = this.isUpdating;
        this.isUpdating = true;

        insideIds.forEach(id => {
            const path = this.rootSvg().select("#" + id) || this.s.select("#" + id);
            if (path && !path.data("isSelected") && this.isInActiveLayer(path.node)) {
                this.selectPath(id);
            }
        });

        this.isUpdating = previouslyUpdating;
    }

    updateGlobalBoundingBox() {
        if (this.boundingBoxRect != null) {
            this.boundingBoxRect.remove();
            this.boundingBoxRect = null;
        }

        const layer = this.scope(); // active layer or inner <svg> (root)
        if (!layer) return;

        const selectedPaths = layer.selectAll(".is-selected");

        if (selectedPaths && selectedPaths.length > 0) {
            // Union of transformed bboxes (robust even with huge scales)
            const bbox = this.unionTransformedBBoxes(selectedPaths);
            if (!bbox) {
                this.boundingBoxPathIds.clear();
                this.highlight();
                return;
            }

            this.boundingBoxRect = layer.rect(bbox.x, bbox.y, bbox.width, bbox.height);
            this.boundingBoxRect.attr({
                stroke: '#00F',
                strokeWidth: 2,
                fill: 'none',
                strokeDasharray: '4 2',
                'vector-effect': 'non-scaling-stroke',
                "pointer-events": "none"
            });
            layer.append(this.boundingBoxRect);

            this.boundingBoxPathIds = this.computeIdsInsideBoundingBox(0.5);

            if (!this.isUpdating)
                this.autoSelectInBoundingBox();

            this.highlight();
        } else {
            this.boundingBoxPathIds.clear();
            this.highlight();
        }
    }

    highlight() {
        this.getPaths();
        const allInsideSelected = [...this.boundingBoxPathIds].every(id => this.selectedIds.has(id));

        this.selectedPaths.forEach(path => {
            path.attr({ fill: allInsideSelected ? "#00FF00" : "#f00" });
        });

        // Report the allInsideSelected state to Blazor
        if (!this.disableSelection) {
            this.dotNetObjectReference.invokeMethodAsync("OnAllInsideSelectedChanged", allInsideSelected);
        }

        // Re-render outlines onto the SVG (selection + groups)
        this.visualizeGroups();
    }

    getPaths() {
        const scope = this.scope();
        this.selectedPaths = (this.s && scope) ? scope.selectAll(".is-selected") : [];
        this.selectedIds = new Set();
        this.selectedPaths.forEach(p => this.selectedIds.add(p.attr("id")));
    }

    selectPath(pathId) {
        if (!this.s) return false;
        let path = this.rootSvg().select("#" + pathId) || this.s.select("#" + pathId);
        if (!path) return false;

        if (!this.isInActiveLayer(path.node)) return false;
        if (path.data("isSelected") === true) return false;

        path.data("isSelected", true);
        path.attr({ fill: this.fillColor });
        path.addClass("is-selected");

        if (!this.isUpdating) {
            this.updateGlobalBoundingBox();
        } else {
            this.highlight();
        }
        return true;
    }

    selectPaths(paths) {
        if (!this.s || !Array.isArray(paths)) return;
        this.isUpdating = true;
        const ids = paths
            .filter((id) => id != null && String(id).trim() !== "")
            .map((id) => String(id).trim());

        for (const id of ids) {
            const path = this.rootSvg().select("#" + id) || this.s.select("#" + id);
            if (!path) continue;
            if (path.data("isSelected") === true) continue;

            path.data("isSelected", true);
            path.attr({ fill: this.fillColor });
            path.addClass("is-selected");
        }
        this.updateGlobalBoundingBox();
        this.isUpdating = false;

        // Report initial selection state
        this.getPaths();
        const mySelectedIds = Array.from(this.selectedIds);
        if (!this.disableSelection && mySelectedIds.length > 0) {
            this.dotNetObjectReference.invokeMethodAsync("OnPathsChanged", mySelectedIds);
        }

        return true;
    }

    unselectPath(pathId) {
        if (!this.s) return false;
        let path = this.rootSvg().select("#" + pathId) || this.s.select("#" + pathId);
        if (!path) return false;

        if (!this.isInActiveLayer(path.node)) return false;
        if (path.data("isSelected") === false) return false;

        const prevIsUpdating = this.isUpdating;
        this.isUpdating = true;

        let originalColor = path.data("originalColor");
        path.data("isSelected", false);
        path.attr({ fill: originalColor });
        path.removeClass("is-selected");

        this.updateGlobalBoundingBox();

        this.isUpdating = prevIsUpdating;
        this.highlight();
        return true;
    }

    unselectAllPaths() {
        if (!this.s) return;
        const scope = this.scope() || this.rootSvg() || this.s;
        scope.selectAll(".is-selected").forEach((path) => {
            let originalColor = path.data("originalColor");
            path.data("isSelected", false);
            path.attr({ fill: originalColor });
            path.removeClass("is-selected");
        });
        if (this.boundingBoxRect) {
            this.boundingBoxRect.remove();
            this.boundingBoxRect = null;
        }
        this.boundingBoxPathIds.clear();
        this.highlight();
    }
}

// Global instance management
const instances = new Map();
let snapLoadingPromise = null;

export function initAsync(containerId, dotNetObjectReference, disableSelection = false) {
    let url = './_content/LazyMagic.BlazorSvg/snap.svg.js';

    // If already loading, return the existing promise
    if (snapLoadingPromise) {
        return snapLoadingPromise.then(() => {
            const instance = new SvgViewerInstance(containerId, dotNetObjectReference, disableSelection);
            instances.set(containerId, instance);
            return containerId;
        });
    }

    // Check if already loaded
    if (typeof Snap !== 'undefined') {
        const instance = new SvgViewerInstance(containerId, dotNetObjectReference, disableSelection);
        instances.set(containerId, instance);
        return Promise.resolve(containerId);
    }

    // Check if script tag exists but Snap might still be loading
    if (document.querySelector('script[src="' + url + '"]')) {
        snapLoadingPromise = new Promise((resolve) => {
            const checkSnap = setInterval(() => {
                if (typeof Snap !== 'undefined') {
                    clearInterval(checkSnap);
                    snapLoadingPromise = null;
                    resolve();
                }
            }, 50);
        });
        return snapLoadingPromise.then(() => {
            const instance = new SvgViewerInstance(containerId, dotNetObjectReference, disableSelection);
            instances.set(containerId, instance);
            return containerId;
        });
    }

    // Create and load the script
    snapLoadingPromise = new Promise((resolve, reject) => {
        let script = document.createElement('script');
        script.src = url;
        script.onload = () => {
            snapLoadingPromise = null;
            resolve();
        };
        script.onerror = () => {
            snapLoadingPromise = null;
            reject('Snap.svg could not be loaded');
        };
        document.head.appendChild(script);
    });

    return snapLoadingPromise.then(() => {
        const instance = new SvgViewerInstance(containerId, dotNetObjectReference, disableSelection);
        instances.set(containerId, instance);
        return containerId;
    });
}

export async function loadSvgAsync(containerId, svgContent) {
    const instance = instances.get(containerId);
    if (!instance) throw new Error(`No instance found for container ${containerId}`);
    await instance.loadSvgAsync(svgContent);
}

export function selectPath(containerId, pathId) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.selectPath(pathId);
}

export function selectPaths(containerId, paths) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.selectPaths(paths);
}

export function unselectPath(containerId, pathId) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.unselectPath(pathId);
}

export function unselectAllPaths(containerId) {
    const instance = instances.get(containerId);
    if (!instance) return;
    instance.unselectAllPaths();
}

export function activateLayer(containerId, name) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.activateLayer(name);
}

export function disposeInstance(containerId) {
    instances.delete(containerId);
}

// Path Grouping Exports
export function groupSelectedPaths(containerId) {
    const instance = instances.get(containerId);
    if (!instance) return null;
    return instance.groupSelectedPaths();
}

export function createGroup(containerId, groupId, pathIds) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.createGroup(groupId, pathIds);
}

export function removeGroup(containerId, groupId) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.removeGroup(groupId);
}

export function selectGroup(containerId, groupId) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.selectGroup(groupId);
}

export function getGroupInfo(containerId, pathId) {
    const instance = instances.get(containerId);
    if (!instance) return null;
    return instance.getGroupInfo(pathId);
}

export function getAllGroups(containerId) {
    const instance = instances.get(containerId);
    if (!instance) return null;

    const groups = {};
    instance.pathGroups.forEach((pathIds, groupId) => {
        groups[groupId] = {
            pathIds: Array.from(pathIds),
            color: instance.getGroupColor(groupId)
        };
    });
    return groups;
}

export function clearAllGroups(containerId) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    instance.clearAllGroups();
    return true;
}
