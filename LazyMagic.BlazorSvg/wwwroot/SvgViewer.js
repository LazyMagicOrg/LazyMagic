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


    // --------- OUTLINE GENERATION (smart concave, gap-aware) ---------

    // Generate tight-fitting concave outline for a set of paths.
    // Includes diagnostic logging to reveal whether concave or convex was used.
    generateGroupOutline(pathIds, options = {}) {
        if (!this.s || !pathIds || pathIds.length === 0) return null;

        const {
            gapHopPx = 3,
            kStart = 2,
            kMax = 3,
            maxEdgePx = 10,
            sampleStride = 1,
            downsampleEveryN = 1,
            minContainment = 0.5
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
            const union = this.unionTransformedBBoxes(groupPaths);
            if (!union) return null;
            const tri = [
                { x: union.x, y: union.y },
                { x: union.x + union.width, y: union.y },
                { x: union.x + union.width, y: union.y + union.height }
            ];
            const dTri = `M ${tri[0].x} ${tri[0].y} L ${tri[1].x} ${tri[1].y} L ${tri[2].x} ${tri[2].y} Z`;
            console.debug('[outline] fallback triangle (insufficient points)');
            return dTri;
        }

        // Seed tiny bridges between selected shapes only (keep enabled for baseline behavior)
        const bridges = this._seedBridgePoints(pathInfos, gapHopPx, Math.max(4, sampleStride * 2));

        // Final point cloud (optionally downsampled)
        const cloud = (downsampleEveryN > 1)
            ? this._downsamplePoints(allBoundaryPoints.concat(bridges), downsampleEveryN)
            : allBoundaryPoints.concat(bridges);

        // --- Diagnostic: show cloud size before hull ---
        console.debug('[outline] cloud points:', cloud.length, 'bridges:', bridges.length);

        // Try concave hull first
        let hull = this._concaveHull(cloud, kStart, kMax, maxEdgePx);
        let hullType = 'concave';

        // If concave failed, do an initial convex fallback
        if (!hull || hull.length < 3) {
            hull = this.simpleConvexHull(cloud);
            hullType = 'convex_fallback_initial';
        }

        // Optional containment gate — if set and score is too low, force convex
        let score = 1;
        try {
            score = this._validateContainmentScore(hull, pathInfos);
            if (typeof minContainment === 'number' && score < minContainment) {
                hull = this.simpleConvexHull(cloud);
                hullType = 'convex_fallback_containment';
            }
        } catch (e) {
            console.warn('[outline] containment scoring error:', e);
        }

        // --- Diagnostic: log final hull characteristics ---
        console.debug('[outline] hullType:', hullType,
            'hullVerts:', hull ? hull.length : 0,
            'containmentScore:', score.toFixed ? score.toFixed(3) : score,
            { kStart, kMax, maxEdgePx, gapHopPx, sampleStride, downsampleEveryN, minContainment });

        if (!hull || hull.length < 3) return null;

        let d = `M ${hull[0].x} ${hull[0].y}`;
        for (let i = 1; i < hull.length; i++) d += ` L ${hull[i].x} ${hull[i].y}`;
        d += " Z";
        return d;
    }

    // Extract boundary points along the actual path outline,
    // then PROJECT them into the current scope's coordinate system
    // so the outline lands exactly where it should.
    extractPathBoundaryPoints(path, bbox) {
        const scope = this.scope();
        const node = path && path.node ? path.node : path; // native SVG element
        const localPts = [];

        // 1) Sample along the path in the element's LOCAL coords
        try {
            if (node && typeof node.getTotalLength === "function" && typeof node.getPointAtLength === "function") {
                const len = node.getTotalLength();
                if (isFinite(len) && len > 0) {
                    // ~every 3–8 px depending on length
                    const step = Math.max(3, Math.min(8, Math.floor(len / 250) || 4));
                    for (let d = 0; d <= len; d += step) {
                        const p = node.getPointAtLength(d);
                        if (isFinite(p.x) && isFinite(p.y)) localPts.push({ x: p.x, y: p.y });
                    }
                }
            }
        } catch (_) {
            // ignore; we'll use bbox fallbacks below if needed
        }

        // 2) If too few points, add a light bbox ring (still in element-local terms)
        if (localPts.length < 8 && bbox) {
            const margin = 1.5;
            const n = 16;
            for (let i = 0; i < n; i++) {
                const t = i / n;
                // top
                localPts.push({ x: bbox.x + bbox.width * t, y: bbox.y - margin });
                // right
                localPts.push({ x: bbox.x + bbox.width + margin, y: bbox.y + bbox.height * t });
                // bottom
                localPts.push({ x: bbox.x + bbox.width * (1 - t), y: bbox.y + bbox.height + margin });
                // left
                localPts.push({ x: bbox.x - margin, y: bbox.y + bbox.height * (1 - t) });
            }
        }

        // 3) Absolute last resort: corners
        if (localPts.length === 0 && bbox) {
            localPts.push({ x: bbox.x, y: bbox.y });
            localPts.push({ x: bbox.x + bbox.width, y: bbox.y });
            localPts.push({ x: bbox.x + bbox.width, y: bbox.y + bbox.height });
            localPts.push({ x: bbox.x, y: bbox.y + bbox.height });
        }

        // 4) Project those points from element-local → scope-local coordinates
        try {
            const svgEl =
                (this.rootSvg() && this.rootSvg().node) ||
                (scope && scope.node && scope.node.ownerSVGElement) ||
                (node && node.ownerSVGElement) ||
                null;

            const svgPoint = svgEl && svgEl.createSVGPoint ? svgEl.createSVGPoint() : null;
            const nodeCTM = node && node.getCTM ? node.getCTM() : null;
            const scopeCTM = scope && scope.node && scope.node.getCTM ? scope.node.getCTM() : null;

            if (svgPoint && nodeCTM) {
                // Matrix that maps element-local → scope-local
                // M = inverse(scopeCTM) * nodeCTM
                const toScope = scopeCTM ? scopeCTM.inverse().multiply(nodeCTM) : nodeCTM;

                const out = [];
                for (const p of localPts) {
                    svgPoint.x = p.x;
                    svgPoint.y = p.y;
                    const sp = svgPoint.matrixTransform(toScope);
                    out.push({ x: sp.x, y: sp.y });
                }
                return out;
            }
        } catch (e) {
            console.warn('[outline] CTM projection failed; using unprojected points', e);
        }

        // If we couldn't project, return what we have (may misalign if transforms exist)
        return localPts;
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

    // Fast-ish nearest distance from a point to a set of points (linear scan is fine for our sizes)
    _minDistToSet(pt, set) {
        let best = Infinity;
        for (let i = 0; i < set.length; i++) {
            const dx = set[i].x - pt.x, dy = set[i].y - pt.y;
            const d = Math.hypot(dx, dy);
            if (d < best) best = d;
        }
        return best;
    }

    // Ensure the edge stays close to actual geometry: sample the segment and require
    // that *most* sample points lie within `maxAwayPx` of the boundary cloud.
    _edgeHugsBoundary(a, b, boundaryCloud, maxAwayPx = 8, samples = 12, requireRatio = 0.75) {
        if (!boundaryCloud || boundaryCloud.length === 0) return true; // nothing to judge against
        let ok = 0;
        for (let i = 0; i <= samples; i++) {
            const t = i / samples;
            const x = a.x + (b.x - a.x) * t;
            const y = a.y + (b.y - a.y) * t;
            const d = this._minDistToSet({ x, y }, boundaryCloud);
            if (d <= maxAwayPx) ok++;
        }
        return (ok / (samples + 1)) >= requireRatio;
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

    // Concave hull (k-nearest) with self-intersection + max-edge + edge-hug guardrails
    _concaveHull(points, kStart = 3, kMax = 20, maxEdge = Infinity, edgeHugPx = 10) {
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

        // Use the sampled boundary cloud itself as the geometry to "hug"
        const boundaryCloud = pts;

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
                    // 1) Edge too long? (prevents big bridges)
                    if (this.calculateDistance(current, cand) > maxEdge) continue;

                    // 2) Already used? (except for closing back to start)
                    const key = `${cand.x.toFixed(2)}_${cand.y.toFixed(2)}`;
                    if (used.has(key) && !(cand.x === start.x && cand.y === start.y)) continue;

                    // 3) Would self-intersect?
                    const tmp = hull.concat([cand]);
                    if (tmp.length >= 4 && this.hasSelfintersection(tmp)) continue;

                    // 4) NEW: Does the segment "hug" the actual boundary,
                    // or does it cross empty space (like the inside of an L)?
                    if (!this._edgeHugsBoundary(current, cand, boundaryCloud, edgeHugPx, 12, 0.7)) {
                        continue;
                    }

                    next = cand;
                    break;
                }

                if (!next) {
                    // Try close to start if legal
                    if (hull.length > 2 && this.calculateDistance(current, start) <= maxEdge) {
                        // also require the closing edge to hug
                        if (this._edgeHugsBoundary(current, start, boundaryCloud, edgeHugPx, 12, 0.7)) {
                            const tmp = hull.concat([start]);
                            if (!this.hasSelfintersection(tmp)) {
                                hull = tmp;
                                closed = true;
                                break;
                            }
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
                gapHopPx: 3,          // only hop over hairline gaps
                kStart: 2,            // start with very concave
                kMax: 3,             // don’t go very convex
                maxEdgePx: 10,        // forbid any long spans at all
                sampleStride: 1,      // full-fidelity sampling
                downsampleEveryN: 1,
                minContainment: 0.5  // reject hulls that don’t cover 95% of points
            });

            if (selectionPathData) {
                const selectionOutline = scope.path(selectionPathData);
                selectionOutline.attr({
                    stroke: '#FFA500',              // orange for live selection
                    strokeWidth: 3,
                    fill: 'none',
                    strokeDasharray: '10 5',
                    'stroke-opacity': .9,
                    'vector-effect': 'non-scaling-stroke',
                    'pointer-events': 'none'
                });
                selectionOutline.addClass("group-outline"); // easy cleanup
            }
        }

        // 2) Reset path colors to original (unless selected)
        scope.selectAll("path").forEach(path => {
            // ⬅️ Skip the live/group outline we just drew
            if (path.hasClass("group-outline")) return;

            const id = path.attr("id");

            if (!path.data("isSelected")) {
                // Reset to default stroke
                path.attr({
                    stroke: path.data("originalStroke") || "#000",
                    strokeWidth: path.data("originalStrokeWidth") || 1,
                    'stroke-opacity': 1
                });
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

        if (isSelected) {
            this.unselectPath(id);
            this.dotNetObjectReference.invokeMethodAsync("OnPathUnselected", id);
        } else {
            this.selectPath(id);
            this.dotNetObjectReference.invokeMethodAsync("OnPathSelected", id);
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
