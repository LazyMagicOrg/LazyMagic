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
            maxEdgePx = 100,
            sampleStride = 1,
            downsampleEveryN = 1,
            minContainment = 0.0
        } = options;

        const scope = this.scope();
        const groupPaths = [];
        pathIds.forEach(id => {
            const p = scope.select("#" + id);
            if (p) groupPaths.push(p);
        });
        if (groupPaths.length === 0) return null;

        // Performance optimization: Create unified path for multiple selections
        if (groupPaths.length > 1) {
            console.debug('[outline] Multi-path optimization: Creating unified path from', groupPaths.length, 'paths');
            return this._generateOptimizedMultiPathOutline(groupPaths, options);
        }

        // Single path - use original algorithm
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
    extractPathBoundaryPoints(path, bbox, customSampleStep = null) {
        const scope = this.scope();
        const node = path && path.node ? path.node : path; // native SVG element
        const localPts = [];
        // 1) Sample along the path in the element's LOCAL coords
        try {
            if (node && typeof node.getTotalLength === "function" && typeof node.getPointAtLength === "function") {
                const len = node.getTotalLength();
                if (isFinite(len) && len > 0) {
                    // Use custom step for multi-path optimization, otherwise default adaptive sampling
                    const step = customSampleStep || Math.max(3, Math.min(8, Math.floor(len / 250) || 4));
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

    // Smart containment scoring (0..1) - tests interior points of selected shapes
    _validateContainmentScore(hull, pathInfos) {
        let ok = 0, total = 0;

        for (const pathInfo of pathInfos) {
            // Skip boundary points - they should be ON the hull, not inside it
            // Instead, generate a grid of interior test points within each shape's bounding box
            const b = pathInfo.bbox;

            // Create a 5x5 grid of interior test points
            for (let row = 0; row < 5; row++) {
                for (let col = 0; col < 5; col++) {
                    const x = b.x + (col + 0.5) * (b.width / 5);  // +0.5 to avoid edges
                    const y = b.y + (row + 0.5) * (b.height / 5);
                    const testPoint = { x, y };

                    total++;
                    if (this.isPointInPolygon(testPoint, hull)) {
                        ok++;
                    } else {
                        // Log the first few points that are outside the hull for debugging
                        if (total - ok <= 3) {
                            console.debug(`[containment] Interior point outside hull: (${testPoint.x.toFixed(1)}, ${testPoint.y.toFixed(1)})`);
                        }
                    }
                }
            }
        }

        const score = total > 0 ? ok / total : 1;
        console.debug(`[containment] Interior score: ${ok}/${total} = ${score.toFixed(3)}`);
        return score;
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

        const pts = this.removeDuplicates(points, 2.0);
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

    // Calculate distance from a point to a line segment
    _distanceToLineSegment(point, segStart, segEnd) {
        const A = point.x - segStart.x;
        const B = point.y - segStart.y;
        const C = segEnd.x - segStart.x;
        const D = segEnd.y - segStart.y;

        const dot = A * C + B * D;
        const lenSq = C * C + D * D;

        if (lenSq === 0) return Math.sqrt(A * A + B * B); // degenerate segment

        const param = dot / lenSq;

        let xx, yy;
        if (param < 0) {
            xx = segStart.x;
            yy = segStart.y;
        } else if (param > 1) {
            xx = segEnd.x;
            yy = segEnd.y;
        } else {
            xx = segStart.x + param * C;
            yy = segStart.y + param * D;
        }

        const dx = point.x - xx;
        const dy = point.y - yy;
        return Math.sqrt(dx * dx + dy * dy);
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
        let maxDist = 0;
        let avgDist = 0;
        const edgeDist = this.calculateDistance(a, b);

        for (let i = 0; i <= samples; i++) {
            const t = i / samples;
            const x = a.x + (b.x - a.x) * t;
            const y = a.y + (b.y - a.y) * t;
            const d = this._minDistToSet({ x, y }, boundaryCloud);
            avgDist += d;
            maxDist = Math.max(maxDist, d);
            if (d <= maxAwayPx) ok++;
        }
        avgDist /= (samples + 1);

        const ratio = ok / (samples + 1);
        const passes = ratio >= requireRatio;

        // Enhanced debug logging for edge hugging failures
        if (!passes && edgeDist > 20) { // Only debug longer edges
            console.debug(`[edgeHug] REJECT edge (${a.x.toFixed(1)},${a.y.toFixed(1)}) → (${b.x.toFixed(1)},${b.y.toFixed(1)})`);
            console.debug(`[edgeHug] Length: ${edgeDist.toFixed(1)}px, Ratio: ${ratio.toFixed(2)}/${requireRatio}, AvgDist: ${avgDist.toFixed(1)}px, MaxDist: ${maxDist.toFixed(1)}px`);
            console.debug(`[edgeHug] ${ok}/${samples + 1} sample points within ${maxAwayPx}px of boundary`);
        }
        return passes;
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

        const pts = this.removeDuplicates(points, 2.0);
        if (pts.length < 3) return null;

        console.debug('[concave] Starting concave hull with', pts.length, 'points, k range:', kStart, '-', kMax, 'maxEdge:', maxEdge);
        console.debug('[concave] Input bounds:', {
            minX: Math.min(...pts.map(p => p.x)).toFixed(1),
            maxX: Math.max(...pts.map(p => p.x)).toFixed(1),
            minY: Math.min(...pts.map(p => p.y)).toFixed(1),
            maxY: Math.max(...pts.map(p => p.y)).toFixed(1),
            width: (Math.max(...pts.map(p => p.x)) - Math.min(...pts.map(p => p.x))).toFixed(1),
            height: (Math.max(...pts.map(p => p.y)) - Math.min(...pts.map(p => p.y))).toFixed(1)
        });

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

        console.debug('[concave] Starting point:', start);

        for (let k = Math.max(3, kStart); k <= Math.max(kStart, kMax); k++) {
            console.debug('[concave] Trying k =', k);
            let hull = [start];
            let current = start;
            let prev = { x: start.x - 1, y: start.y };
            const used = new Set([`${start.x.toFixed(2)}_${start.y.toFixed(2)}`]);

            let safety = 0, closed = false;
            let stepCount = 0;
            while (safety++ < pts.length * 10) {
                stepCount++;
                // Get all potential neighbors within reasonable distance
                const maxNeighborDist = Math.min(maxEdge, 40); // Reasonable max distance
                const neighbors = pts
                    .filter(p => {
                        const dist = this.calculateDistance(p, current);
                        return dist > 0.0001 && dist <= maxNeighborDist;
                    })
                    // Sort by distance first, but we'll validate edges don't cut through interior
                    .sort((a, b) => this.calculateDistance(a, current) - this.calculateDistance(b, current))
                    .slice(0, k * 2) // Get more candidates for edge validation
                    // Now filter out edges that would cut through the selected area
                    .filter(p => {
                        // Quick check: if the edge would be very short, it's probably safe
                        const dist = this.calculateDistance(p, current);
                        if (dist < 8) return true;

                        // For longer edges, check if the path stays along the boundary
                        // Sample points along the edge and ensure they're near boundary points
                        const samples = Math.max(3, Math.floor(dist / 10));
                        for (let i = 1; i < samples; i++) {
                            const t = i / samples;
                            const midX = current.x + t * (p.x - current.x);
                            const midY = current.y + t * (p.y - current.y);

                            // Find closest boundary point to this midpoint
                            let closestDist = Infinity;
                            for (const bp of pts) {
                                const d = Math.sqrt((bp.x - midX) ** 2 + (bp.y - midY) ** 2);
                                if (d < closestDist) closestDist = d;
                            }

                            // If midpoint is too far from any boundary point, this edge cuts through interior
                            // Relax this threshold - was too strict at 15
                            if (closestDist > 25) {
                                return false;
                            }
                        }
                        return true;
                    })
                    .slice(0, k); // Take final k candidates

                // Neighbors are already sorted by angle/distance above

                // Debug the neighbor selection process
                console.debug(`[concave] Step ${stepCount}, k=${k}: At point (${current.x.toFixed(1)}, ${current.y.toFixed(1)}), ${neighbors.length} neighbors:`);
                neighbors.forEach((n, i) => {
                    const dist = this.calculateDistance(current, n);
                    const angle = Math.atan2(n.y - current.y, n.x - current.x) * 180 / Math.PI;
                    const key = `${n.x.toFixed(2)}_${n.y.toFixed(2)}`;
                    const isUsed = used.has(key) && !(n.x === start.x && n.y === start.y);
                    console.debug(`  neighbor ${i}: (${n.x.toFixed(1)}, ${n.y.toFixed(1)}) dist=${dist.toFixed(1)} angle=${angle.toFixed(0)}° used=${isUsed}`);
                });

                let next = null;
                let rejectionReasons = [];
                for (let i = 0; i < neighbors.length; i++) {
                    const cand = neighbors[i];
                    const dist = this.calculateDistance(current, cand);

                    // 1) Edge too long? (prevents big bridges)
                    if (dist > maxEdge) {
                        rejectionReasons.push(`neighbor ${i}: too long (${dist.toFixed(1)} > ${maxEdge})`);
                        continue;
                    }

                    // 2) Already used? (except for closing back to start)
                    const key = `${cand.x.toFixed(2)}_${cand.y.toFixed(2)}`;
                    if (used.has(key) && !(cand.x === start.x && cand.y === start.y)) {
                        rejectionReasons.push(`neighbor ${i}: already used`);
                        continue;
                    }

                    // No backtracking prevention - let angle-based sorting handle direction



                    next = cand;
                    console.debug(`[concave] Step ${stepCount}: Selected neighbor ${i} at (${cand.x.toFixed(1)}, ${cand.y.toFixed(1)}), dist=${dist.toFixed(1)}`);
                    break;
                }

                if (!next) {
                    console.debug(`[concave] Step ${stepCount}: No valid neighbor found. Rejections:`, rejectionReasons);
                    console.debug(`[concave] Step ${stepCount}: Hull so far has ${hull.length} points`);

                    // Only close if we've explored a substantial portion of the boundary
                    if (hull.length >= 30 && this.calculateDistance(current, start) <= maxEdge) {
                        console.debug(`[concave] Step ${stepCount}: Attempting to close hull (dist to start: ${this.calculateDistance(current, start).toFixed(1)})`);
                        const tmp = hull.concat([start]);
                        hull = tmp;
                        closed = true;
                        console.debug(`[concave] Step ${stepCount}: Successfully closed hull with ${hull.length} points`);
                        break;
                    }
                    console.debug(`[concave] Step ${stepCount}: Cannot close, terminating with ${hull.length} points`);
                    break;
                }

                hull.push(next);
                used.add(`${next.x.toFixed(2)}_${next.y.toFixed(2)}`);
                prev = current;
                current = next;

                if (current === start && hull.length >= 100) {
                    closed = true;
                    console.debug(`[concave] Step ${stepCount}: Closed by returning to start`);
                    break;
                }

                // Debug every 10 steps to avoid spam
                if (stepCount % 10 === 0) {
                    console.debug(`[concave] Step ${stepCount}: Hull progress - ${hull.length} points so far`);
                }
            }

            console.debug(`[concave] k=${k} completed: closed=${closed}, hull points=${hull.length}, steps=${stepCount}`);

            if (closed) {
                // trim duplicate start if present
                const last = hull[hull.length - 1];
                if (Math.abs(last.x - start.x) < 0.001 && Math.abs(last.y - start.y) < 0.001) hull.pop();
                console.debug(`[concave] SUCCESS: Returning valid concave hull with ${hull.length} points`);
                console.debug(`[concave] Hull bounds check:`, {
                    minX: Math.min(...hull.map(p => p.x)).toFixed(1),
                    maxX: Math.max(...hull.map(p => p.x)).toFixed(1),
                    minY: Math.min(...hull.map(p => p.y)).toFixed(1),
                    maxY: Math.max(...hull.map(p => p.y)).toFixed(1),
                    width: (Math.max(...hull.map(p => p.x)) - Math.min(...hull.map(p => p.x))).toFixed(1),
                    height: (Math.max(...hull.map(p => p.y)) - Math.min(...hull.map(p => p.y))).toFixed(1)
                });
                return hull;
                // }
                // console.debug(`[concave] Hull has self-intersection, trying next k value`);
            }
        }

        console.debug('[concave] All k values failed, falling back to convex hull');
        // Fallback to convex hull if concave fails
        return this.simpleConvexHull(pts);
    }

    // Optimized multi-path outline generation - creates a single merged SVG path then outlines it
    _generateOptimizedMultiPathOutline(groupPaths, options) {
        const { kStart, kMax, maxEdgePx, minContainment, sampleStride = 1, debugShowUnifiedPath = false } = options;
        const scope = this.scope();

        // Clean up any existing debug paths first
        this._cleanupDebugPaths(scope);

        const startTime = performance.now();

        // Step 1: Create a single unified SVG path from all selected paths
        const unifiedPath = this._createUnifiedPath(groupPaths, scope, debugShowUnifiedPath);
        if (!unifiedPath) {
            console.warn('[outline] Failed to create unified path');
            return null;
        }

        console.debug(`[outline] Multi-path merge: Created unified path from ${groupPaths.length} paths in ${(performance.now() - startTime).toFixed(1)}ms`);

        // Step 2: Extract boundary points from the single unified path (much more efficient)
        const unifiedBBox = unifiedPath.getBBox();
        const boundaryPoints = this.extractPathBoundaryPoints(unifiedPath, unifiedBBox);
        const sampledPoints = sampleStride > 1 ? this._downsamplePoints(boundaryPoints, sampleStride) : boundaryPoints;

        // Only remove the unified path if not in debug mode (debug mode keeps it visible)
        if (!debugShowUnifiedPath) {
            unifiedPath.remove();
        }

        console.debug(`[outline] Multi-path unified boundary: ${sampledPoints.length} points from unified path in ${(performance.now() - startTime).toFixed(1)}ms total`);

        if (sampledPoints.length < 3) {
            const unionBBox = this.unionTransformedBBoxes(groupPaths);
            if (!unionBBox) return null;
            const tri = [
                { x: unionBBox.x, y: unionBBox.y },
                { x: unionBBox.x + unionBBox.width, y: unionBBox.y },
                { x: unionBBox.x + unionBBox.width, y: unionBBox.y + unionBBox.height }
            ];
            const dTri = `M ${tri[0].x} ${tri[0].y} L ${tri[1].x} ${tri[1].y} L ${tri[2].x} ${tri[2].y} Z`;
            console.debug('[outline] Multi-path fallback triangle');
            return dTri;
        }

        // Step 3: Run concave hull on the unified boundary to create detailed outline
        // Use the original parameters since we now have a guaranteed complete coverage base
        const hullStartTime = performance.now();
        let hull = this._concaveHull(sampledPoints, kStart, kMax, maxEdgePx);
        let hullType = 'concave';

        if (!hull || hull.length < 3) {
            console.debug('[outline] Concave hull failed on unified boundary, using convex hull');
            hull = this.simpleConvexHull(sampledPoints);
            hullType = 'convex_fallback';
        }

        console.debug('[outline] Multi-path: Running full concave hull on unified boundary');

        console.debug(`[outline] Multi-path hull generation: ${(performance.now() - hullStartTime).toFixed(1)}ms`);

        // Step 4: Validate containment using original path bboxes for compatibility
        let score = 1;
        try {
            const pathInfos = groupPaths.map(p => ({
                bbox: p.getBBox(),
                center: { x: p.getBBox().x + p.getBBox().width / 2, y: p.getBBox().y + p.getBBox().height / 2 },
                boundaryPoints: [] // Not needed for containment validation
            }));

            score = this._validateContainmentScore(hull, pathInfos);
            if (typeof minContainment === 'number' && score < minContainment) {
                hull = this.simpleConvexHull(sampledPoints);
                hullType = 'convex_fallback_containment';
            }
        } catch (e) {
            console.warn('[outline] Multi-path containment scoring error:', e);
        }

        console.debug('[outline] Multi-path unified result:', hullType, 'hullVerts:', hull ? hull.length : 0, 'containmentScore:', score.toFixed ? score.toFixed(3) : score, `total: ${(performance.now() - startTime).toFixed(1)}ms`);

        if (!hull || hull.length < 3) return null;

        let d = `M ${hull[0].x} ${hull[0].y}`;
        for (let i = 1; i < hull.length; i++) d += ` L ${hull[i].x} ${hull[i].y}`;
        d += " Z";
        console.debug(`[outline] Returning multi-path outline SVG data: ${d.substring(0, 100)}...`);
        return d;
    }

    // Clean up any existing debug unified paths
    _cleanupDebugPaths(scope) {
        console.debug('[cleanup] Starting debug path cleanup');

        // Remove by class
        const classPaths = scope.selectAll(".debug-unified-path");
        console.debug(`[cleanup] Found ${classPaths.length} paths with debug-unified-path class`);
        classPaths.remove();

        // Also remove any leftover debug paths by color attributes
        const allPaths = scope.selectAll("path");
        console.debug(`[cleanup] Checking ${allPaths.length} total paths for magenta colors`);
        let removedCount = 0;
        allPaths.forEach(path => {
            const fill = path.attr("fill");
            if (fill && (fill.includes("255, 0, 255") || fill.includes("magenta"))) {
                console.debug(`[cleanup] Removing path with fill: ${fill}`);
                path.remove();
                removedCount++;
            }
        });
        console.debug(`[cleanup] Removed ${removedCount} paths by color attributes`);
    }

    // Create a single merged SVG path by combining all selected path data
    _createUnifiedPath(groupPaths, scope, debugVisible = false) {
        try {
            console.debug('[outline] Creating merged path by combining selected path data');

            // Extract boundary points from all selected paths
            const allBoundaryPoints = [];
            for (const path of groupPaths) {
                const bbox = path.getBBox();
                const boundaryPoints = this.extractPathBoundaryPoints(path, bbox);
                allBoundaryPoints.push(...boundaryPoints);
                console.debug(`[outline] Extracted ${boundaryPoints.length} boundary points from path`);
            }

            if (allBoundaryPoints.length === 0) {
                console.warn('[outline] No boundary points extracted from selected paths');
                return null;
            }

            console.debug(`[outline] Total extracted boundary points: ${allBoundaryPoints.length}`);

            // Create a convex hull around all boundary points to create a single unified shape
            const hull = this.simpleConvexHull(allBoundaryPoints);

            // Convert hull to SVG path data
            let pathData = `M ${hull[0].x} ${hull[0].y}`;
            for (let i = 1; i < hull.length; i++) {
                pathData += ` L ${hull[i].x} ${hull[i].y}`;
            }
            pathData += ' Z';

            console.debug(`[outline] Created unified path from ${hull.length} hull points`);

            // Create the merged path element
            const unifiedPath = scope.path(pathData);

            if (debugVisible) {
                // Debug mode: Make it visible with distinctive styling
                unifiedPath.attr({
                    fill: 'rgba(255, 0, 255, 0.3)',     // Semi-transparent magenta fill
                    stroke: '#FF00FF',                   // Magenta border
                    strokeWidth: 2,
                    'stroke-dasharray': '5 5',           // Dashed line
                    'fill-opacity': 0.3,
                    'stroke-opacity': 0.8,
                    'pointer-events': 'none',            // Allow clicks to pass through
                    'fill-rule': 'nonzero'               // Fill rule to merge overlapping areas
                });
                // Add the class for cleanup
                unifiedPath.addClass("debug-unified-path");
                // Ensure it appears on top
                const parentScope = unifiedPath.node.parentNode;
                parentScope.appendChild(unifiedPath.node);
                console.debug('[outline] Debug mode: Merged SVG path made visible with magenta styling (non-interactive)');
            } else {
                // Normal mode: Hidden
                unifiedPath.attr({
                    fill: '#000000',
                    stroke: 'none',
                    visibility: 'hidden',
                    'fill-rule': 'nonzero'               // Fill rule to merge overlapping areas
                });
            }

            console.debug(`[outline] Created merged SVG path from ${groupPaths.length} original paths`);
            return unifiedPath;

        } catch (error) {
            console.warn('[outline] Error creating merged path:', error);
            console.warn('[outline] Error details:', error.message || error);

            // No fallback - return null to let the caller handle it
            return null;
        }
    }

    // Fallback method: Create unified path from convex hull of all path bounding boxes
    _createFallbackUnifiedPath(groupPaths, scope, debugVisible = false) {
        const allCorners = [];

        // Collect all bounding box corners
        for (const path of groupPaths) {
            const bbox = path.getBBox();
            allCorners.push(
                { x: bbox.x, y: bbox.y },
                { x: bbox.x + bbox.width, y: bbox.y },
                { x: bbox.x + bbox.width, y: bbox.y + bbox.height },
                { x: bbox.x, y: bbox.y + bbox.height }
            );
        }

        // Create convex hull of all corners
        const hull = this.simpleConvexHull(allCorners);
        if (!hull || hull.length < 3) return null;

        // Build path data from hull
        let pathData = `M ${hull[0].x} ${hull[0].y}`;
        for (let i = 1; i < hull.length; i++) {
            pathData += ` L ${hull[i].x} ${hull[i].y}`;
        }
        pathData += ' Z';

        const fallbackPath = scope.path(pathData);

        if (debugVisible) {
            // Debug mode: Make it visible with distinctive styling
            fallbackPath.attr({
                fill: 'rgba(255, 255, 0, 0.3)',     // Semi-transparent yellow fill (different from primary)
                stroke: '#FFFF00',                   // Yellow border
                strokeWidth: 2,
                'stroke-dasharray': '10 5',          // Different dash pattern
                'fill-opacity': 0.3,
                'stroke-opacity': 0.8,
                'pointer-events': 'none'             // Allow clicks to pass through
            });
            // Ensure it appears on top
            const scope = fallbackPath.node.parentNode;
            scope.appendChild(fallbackPath.node);
            console.debug('[outline] Debug mode: Fallback unified path made visible with yellow styling (non-interactive)');
        } else {
            // Normal mode: Hidden
            fallbackPath.attr({
                fill: '#000000',
                stroke: 'none',
                visibility: 'hidden'
            });
        }

        console.debug('[outline] Created fallback unified path from bounding box convex hull');
        return fallbackPath;
    }


    // --------- SELECTION / RENDERING ---------

    // Visualize groups AND the live selection perimeter directly on the SVG
    visualizeGroups() {
        if (!this.s) return;

        const scope = this.scope();

        // Remove existing outlines (selection + groups + debug paths)
        scope.selectAll(".group-outline").remove();
        scope.selectAll(".debug-unified-path").remove();

        // Also remove any leftover debug unified paths that didn't get the class
        scope.selectAll("path").forEach(path => {
            const fill = path.attr("fill");
            if (fill && (fill.includes("255, 0, 255") || fill.includes("255, 255, 0"))) {
                path.remove();
            }
        });

        // 1) Live selection perimeter (even when not grouped)
        this.getPaths();
        const selectedIds = Array.from(this.selectedIds || []);
        if (selectedIds.length > 0) {
            const selectionPathData = this.generateGroupOutline(selectedIds, {
                gapHopPx: 3,          // only hop over hairline gaps
                kStart: 10,           // start with more neighbor options to avoid trapping
                kMax: 25,             // allow even more neighbor options
                maxEdgePx: 100,        // forbid any long spans at all
                sampleStride: 1,      // full-fidelity sampling
                downsampleEveryN: 1,
                minContainment: 0.0,  // disable containment validation to debug core algorithm
                debugShowUnifiedPath: true  // Set to true to see the unified path in magenta
            });

            console.debug(`[visualize] generateGroupOutline returned:`, selectionPathData ? `${selectionPathData.substring(0, 100)}...` : 'null');

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
