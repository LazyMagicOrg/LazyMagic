// Load optimization libraries if available
let KDTree, SpatialGrid;
let SpatialHash, fastWindingAlgorithm, fastRectangleValidation, fastInscribedRectangle;

// Function to dynamically load optimization libraries
async function loadOptimizationLibraries() {
    const loadPromises = [];

    // Load SvgViewerAlgorithms library (required)
    if (typeof window.SvgViewerAlgorithms === 'undefined') {
        const algoScript = document.createElement('script');
        algoScript.src = './_content/LazyMagic.BlazorSvg/SvgViewerAlgorithms.js';
        const algoPromise = new Promise((resolve) => {
            algoScript.onload = () => {
                console.log('[algorithms] SvgViewerAlgorithms library loaded successfully');
                resolve(true);
            };
            algoScript.onerror = () => {
                console.error('[algorithms] Failed to load SvgViewerAlgorithms.js - inscribed rectangles will not work!');
                resolve(false);
            };
        });
        document.head.appendChild(algoScript);
        loadPromises.push(algoPromise);
    }

    // Load KD-Tree library
    if (typeof window.KDTree === 'undefined') {
        const kdScript = document.createElement('script');
        kdScript.src = './_content/LazyMagic.BlazorSvg/kdtree.js';
        const kdPromise = new Promise((resolve) => {
            kdScript.onload = () => {
                KDTree = window.KDTree;
                SpatialGrid = window.SpatialGrid;
                console.log('[kdtree] Library loaded successfully');
                resolve(true);
            };
            kdScript.onerror = () => {
                console.warn('[kdtree] Failed to load library');
                resolve(false);
            };
        });
        document.head.appendChild(kdScript);
        loadPromises.push(kdPromise);
    } else {
        KDTree = window.KDTree;
        SpatialGrid = window.SpatialGrid;
    }

    // Load optimized algorithms
    if (typeof window.SpatialHash === 'undefined') {
        console.warn('[LOADING-DEBUG] Attempting to load SvgViewerOptimized.js...');
        const optScript = document.createElement('script');
        optScript.src = './_content/LazyMagic.BlazorSvg/SvgViewerOptimized.js';
        console.warn('[LOADING-DEBUG] Script src set to:', optScript.src);
        const optPromise = new Promise((resolve) => {
            optScript.onload = () => {
                SpatialHash = window.SpatialHash;
                fastWindingAlgorithm = window.fastWindingAlgorithm;
                fastRectangleValidation = window.fastRectangleValidation;
                fastInscribedRectangle = window.fastInscribedRectangle;
                console.log('[optimized] Fast algorithms with adaptive centroid loaded successfully');
                resolve(true);
            };
            optScript.onerror = (error) => {
                console.warn('[LOADING-DEBUG] Failed to load SvgViewerOptimized.js, error:', error);
                console.warn('[optimized] Failed to load fast algorithms');
                resolve(false);
            };
        });
        document.head.appendChild(optScript);
        loadPromises.push(optPromise);
    } else {
        SpatialHash = window.SpatialHash;
        fastWindingAlgorithm = window.fastWindingAlgorithm;
        fastRectangleValidation = window.fastRectangleValidation;
        fastInscribedRectangle = window.fastInscribedRectangle;
    }

    if (loadPromises.length > 0) {
        await Promise.all(loadPromises);
    }
    return true;
}

// Initialize optimization libraries
loadOptimizationLibraries().then(() => {
    console.log('[optimization] Library loading complete');
    console.log('[optimization] fastInscribedRectangle:', typeof window.fastInscribedRectangle !== 'undefined' ? '✅ Available' : '❌ Not available');
});

// Global debug function to check optimization status
window.checkSvgOptimizationStatus = function() {
    console.log('=== SVG Optimization Status ===');
    console.log('SvgViewerAlgorithms:', typeof window.SvgViewerAlgorithms !== 'undefined' ? '✅ Loaded' : '❌ Not loaded');
    console.log('fastWindingAlgorithm:', typeof window.fastWindingAlgorithm !== 'undefined' ? '✅ Loaded' : '❌ Not loaded');
    console.log('fastInscribedRectangle:', typeof window.fastInscribedRectangle !== 'undefined' ? '✅ Loaded' : '❌ Not loaded');
    console.log('SpatialHash:', typeof window.SpatialHash !== 'undefined' ? '✅ Loaded' : '❌ Not loaded');
    console.log('KDTree:', typeof window.KDTree !== 'undefined' ? '✅ Loaded' : '❌ Not loaded');
    console.log('SpatialGrid:', typeof window.SpatialGrid !== 'undefined' ? '✅ Loaded' : '❌ Not loaded');

    // Check if SvgViewer instances exist
    if (typeof window.svgViewerInstances !== 'undefined' && window.svgViewerInstances.size > 0) {
        console.log('\nSvgViewer Instances:');
        window.svgViewerInstances.forEach((instance, id) => {
            console.log(`  Instance ${id}:`, {
                useFastMode: instance.useFastMode,
                verboseLogging: instance.verboseLogging,
                useKDTree: instance.useKDTree
            });
        });
    }
    console.log('================================');
};

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

        // Visual configuration
        this.showOutlines = false;  // Toggle for orange selection outlines
        this.showBoundingBox = false;  // Toggle for blue bounding box

        // Spatial acceleration structures
        this.boundaryKDTree = null;  // KD-Tree for boundary points
        this.spatialGrid = null;  // Spatial grid for point-in-polygon tests
        this.useKDTree = typeof KDTree !== 'undefined';  // Enable if library is loaded

        // Performance mode - use fast algorithms when available
        this.useFastMode = true;  // Enable fast algorithms by default
        this.verboseLogging = false;  // Reduce logging in fast mode
    }

    // Return the inner <svg> if present, otherwise the paper itself
    rootSvg() {
        return this.s ? (this.s.select("svg") || this.s) : null;
    }

    // Check and load optimization libraries
    async ensureOptimizationsLoaded() {
        if (typeof window.fastWindingAlgorithm === 'undefined') {
            console.log('[optimization] Loading optimization libraries...');
            await loadOptimizationLibraries();

            // Update local references
            if (typeof window.fastWindingAlgorithm !== 'undefined') {
                console.log('[optimization] Fast algorithms now available');
                this.useFastMode = true;
                return true;
            } else {
                console.warn('[optimization] Fast algorithms could not be loaded');
                this.useFastMode = false;
                return false;
            }
        }
        return true;
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
            const multiPathStartTime = performance.now();
            const debugOutline = false; // Set to true to debug outline generation
            if (debugOutline) console.debug('[outline] Multi-path optimization: Creating unified path from', groupPaths.length, 'paths');
            const result = this._generateOptimizedMultiPathOutline(groupPaths, options);
            const multiPathTime = performance.now() - multiPathStartTime;
            if (debugOutline) console.debug(`[outline] Multi-path processing completed in ${multiPathTime.toFixed(1)}ms`);
            return result;
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

    // Point-in-polygon test using ray casting algorithm with spatial grid acceleration
    isPointInPolygon(point, polygon, useGrid = true) {
        // Use spatial grid if available and enabled
        if (useGrid && this.spatialGrid && polygon === this.spatialGrid.polygon) {
            return this.spatialGrid.containsPoint(point);
        }
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

    // Initialize spatial acceleration structures for a polygon
    initializeSpatialStructures(polygon) {
        if (!this.useKDTree || !polygon || polygon.length < 3) {
            return;
        }

        console.time('[spatial] Structure initialization');

        // Create KD-Tree for boundary points (for nearest neighbor queries)
        if (typeof KDTree !== 'undefined') {
            this.boundaryKDTree = new KDTree(polygon);
            console.debug(`[spatial] KD-Tree created with ${polygon.length} boundary points`);
        }

        // Create spatial grid for point-in-polygon tests
        if (typeof SpatialGrid !== 'undefined') {
            // Calculate appropriate cell size based on polygon bounds
            let minX = Infinity, maxX = -Infinity;
            let minY = Infinity, maxY = -Infinity;
            for (const p of polygon) {
                minX = Math.min(minX, p.x);
                maxX = Math.max(maxX, p.x);
                minY = Math.min(minY, p.y);
                maxY = Math.max(maxY, p.y);
            }

            // Use 50x50 grid approximately
            const cellSize = Math.max((maxX - minX) / 50, (maxY - minY) / 50);
            this.spatialGrid = new SpatialGrid(polygon, cellSize);
            console.debug(`[spatial] Spatial grid created with cell size ${cellSize.toFixed(1)}`);
        }

        console.timeEnd('[spatial] Structure initialization');
    }

    // Find the largest inscribed rectangle that fits inside a polygon
    // Tests multiple rotation angles to find optimal orientation
    findLargestInscribedRectangle(polygon, options = {}) {
        console.warn(`🔍 [FUNCTION-DEBUG] findLargestInscribedRectangle CALLED with ${polygon?.length || 0} vertices`);
        const rectangleStartTime = performance.now();
        const {
            gridSize = 20,           // Number of grid divisions per axis
            minArea = 100,           // Minimum rectangle area to consider
            debugLog = false         // Enable debug logging
        } = options;

        if (!polygon || polygon.length < 3) {
            console.warn('[rectangle] Invalid polygon for rectangle fitting');
            return null;
        }

        // Use fast algorithm
        if (typeof window.fastInscribedRectangle === 'undefined') {
            console.error('❌ Fast inscribed rectangle algorithm not loaded');
            return null;
        }

        const debugRect = debugLog;
        if (debugRect) console.debug('[rectangle] Using fast inscribed rectangle algorithm');

        const fastInscribedRectangle = window.fastInscribedRectangle;

        // If a hint angle is provided, test angles around it with higher precision
        const algorithmOptions = {
            angleStep: options.hintAngle !== undefined ? 1 : 10,  // Use 1° steps if we have a hint
            refinementStep: 0.5,  // Finer refinement for better precision
            maxTime: 5000,
            debugMode: true
        };

        // If hint angle provided, also test angles around the hint
        if (options.hintAngle !== undefined) {
            console.debug(`[rectangle] Using hint angle: ${options.hintAngle.toFixed(1)}°`);
            algorithmOptions.startAngle = options.hintAngle - 5;  // Test ±5° around hint
            algorithmOptions.endAngle = options.hintAngle + 5;
        }

        const result = fastInscribedRectangle(polygon, algorithmOptions);

        if (debugRect) console.debug(`[rectangle] Fast algorithm result:`, result);
        if (result) {
            if (debugRect) console.log(`[rectangle] Fast algorithm found ${result.width.toFixed(1)}x${result.height.toFixed(1)} rectangle at ${result.angle}°`);

            // FINAL DEBUG: Show polygon characteristics after area calculation
            const timeInfo = result.elapsed ? `${result.elapsed.toFixed(1)}ms` : 'N/A';
            console.log(`🔍 [SHAPE-DEBUG] FINAL: ${polygon.length} vertices, AREA: ${result.area.toFixed(0)}, TIME: ${timeInfo}`);

            return result;
        }

        console.error('❌ Fast algorithm returned null');
        return null;
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

        const debugOutline = false; // Set to true to debug outline generation
        if (debugOutline) console.debug(`[outline] Multi-path merge: Created unified path from ${groupPaths.length} paths in ${(performance.now() - startTime).toFixed(1)}ms`);

        // Step 2: Use the unified path directly as it already represents the correct combined shape
        if (debugOutline) console.debug('[outline] Multi-path: Using unified path directly without hull algorithms to preserve true shape');

        const unifiedPathData = unifiedPath.attr('d');

        // Only remove the unified path if not in debug mode (debug mode keeps it visible)
        if (!debugShowUnifiedPath) {
            unifiedPath.remove();
        }

        if (debugOutline) console.debug(`[outline] Multi-path direct path: Using original unified path data in ${(performance.now() - startTime).toFixed(1)}ms total`);

        if (!unifiedPathData) {
            console.warn('[outline] Unified path has no data');
            return null;
        }

        // Return the unified path data directly - it already represents the exact shape we want
        if (debugOutline) console.debug('[outline] Multi-path unified result: direct_path', `total: ${(performance.now() - startTime).toFixed(1)}ms`);
        if (debugOutline) console.debug(`[outline] Returning unified path SVG data: ${unifiedPathData.substring(0, 100)}...`);
        return unifiedPathData;
    }

    // Merge path boundaries by making coincident points and removing internal segments
    _mergePathBoundaries(groupPaths, allBoundaryPoints) {
        try {
            console.debug('[merge] Starting geometric path boundary merging');

            // Step 1: Extract individual path segments with path association
            const pathSegments = [];
            let segmentId = 0;
            const originalPathData = [];

            for (let pathIdx = 0; pathIdx < groupPaths.length; pathIdx++) {
                const path = groupPaths[pathIdx];
                const bbox = path.getBBox();
                const pathPoints = this.extractPathBoundaryPoints(path, bbox);

                // Store original path data for debugging
                originalPathData.push({
                    pathIdx: pathIdx,
                    pointCount: pathPoints.length,
                    bounds: {
                        minX: Math.min(...pathPoints.map(p => p.x)).toFixed(1),
                        maxX: Math.max(...pathPoints.map(p => p.x)).toFixed(1),
                        minY: Math.min(...pathPoints.map(p => p.y)).toFixed(1),
                        maxY: Math.max(...pathPoints.map(p => p.y)).toFixed(1)
                    },
                    samplePoints: pathPoints.slice(0, 5).map(p => `(${p.x.toFixed(1)}, ${p.y.toFixed(1)})`).join(', ') + '...'
                });

                // Create segments for this path (connecting consecutive points)
                for (let i = 0; i < pathPoints.length; i++) {
                    const nextIdx = (i + 1) % pathPoints.length; // Wrap around for closed path
                    pathSegments.push({
                        id: segmentId++,
                        pathIdx: pathIdx,
                        start: { ...pathPoints[i] },
                        end: { ...pathPoints[nextIdx] },
                        removed: false
                    });
                }
            }

            console.debug(`[merge] Original path data:`, originalPathData);
            console.debug(`[merge] Created ${pathSegments.length} path segments from ${groupPaths.length} paths`);

            // Step 2: Make coincident points - smarter connection detection
            let coincidentPairCount = 0;
            const processedPairs = new Set(); // Avoid duplicate processing

            // Find the closest point pairs between different paths
            const connectionCandidates = [];

            for (let i = 0; i < pathSegments.length; i++) {
                for (let j = i + 1; j < pathSegments.length; j++) {
                    const seg1 = pathSegments[i];
                    const seg2 = pathSegments[j];

                    // Skip if same path (they should already be connected)
                    if (seg1.pathIdx === seg2.pathIdx) continue;

                    // Check all endpoint combinations
                    const combinations = [
                        { dist: this.calculateDistance(seg1.start, seg2.start), p1: seg1.start, p2: seg2.start, desc: 'start-start' },
                        { dist: this.calculateDistance(seg1.start, seg2.end), p1: seg1.start, p2: seg2.end, desc: 'start-end' },
                        { dist: this.calculateDistance(seg1.end, seg2.start), p1: seg1.end, p2: seg2.start, desc: 'end-start' },
                        { dist: this.calculateDistance(seg1.end, seg2.end), p1: seg1.end, p2: seg2.end, desc: 'end-end' }
                    ];

                    // Find closest pair between these two segments
                    const closest = combinations.reduce((min, curr) => curr.dist < min.dist ? curr : min);

                    connectionCandidates.push({
                        pathPair: `${seg1.pathIdx}-${seg2.pathIdx}`,
                        distance: closest.dist,
                        p1: closest.p1,
                        p2: closest.p2,
                        desc: closest.desc
                    });
                }
            }

            // Sort by distance and take only the closest connections between each path pair
            connectionCandidates.sort((a, b) => a.distance - b.distance);

            const pathPairConnections = new Map();
            const strictThreshold = 8.0; // Allow slightly larger gaps for genuine connections

            for (const candidate of connectionCandidates) {
                if (candidate.distance <= strictThreshold) {
                    if (!pathPairConnections.has(candidate.pathPair)) {
                        pathPairConnections.set(candidate.pathPair, candidate);
                    }
                }
            }

            console.debug(`[merge] Found ${pathPairConnections.size} potential path pair connections`);

            // Make the closest points between each path pair coincident
            for (const connection of pathPairConnections.values()) {
                // Create unique key to avoid processing same point pair multiple times
                const pairKey = `${Math.min(connection.p1.x, connection.p2.x)}_${Math.min(connection.p1.y, connection.p2.y)}_${Math.max(connection.p1.x, connection.p2.x)}_${Math.max(connection.p1.y, connection.p2.y)}`;

                if (!processedPairs.has(pairKey)) {
                    processedPairs.add(pairKey);

                    // Make points coincident at midpoint
                    const midpoint = {
                        x: (connection.p1.x + connection.p2.x) / 2,
                        y: (connection.p1.y + connection.p2.y) / 2
                    };

                    console.debug(`[merge] Connecting paths ${connection.pathPair}: (${connection.p1.x.toFixed(1)}, ${connection.p1.y.toFixed(1)}) and (${connection.p2.x.toFixed(1)}, ${connection.p2.y.toFixed(1)}) -> (${midpoint.x.toFixed(1)}, ${midpoint.y.toFixed(1)}) [dist: ${connection.distance.toFixed(1)}px]`);

                    // Update both points to midpoint
                    connection.p1.x = midpoint.x;
                    connection.p1.y = midpoint.y;
                    connection.p2.x = midpoint.x;
                    connection.p2.y = midpoint.y;

                    coincidentPairCount++;
                }
            }

            console.debug(`[merge] Made ${coincidentPairCount} point pairs coincident`);
            console.debug(`[merge] Connection threshold: ${strictThreshold}px`);

            // Step 3: Simplified approach - only remove segments between touching paths
            let removedCount = 0;

            console.debug('[merge] Using simplified internal segment removal');

            // Only remove segments that are very close to other paths (truly internal)
            for (let i = 0; i < pathSegments.length; i++) {
                const segment = pathSegments[i];
                if (segment.removed) continue;

                // Check if this segment is very close to other paths
                let isInternal = false;
                for (let pathIdx = 0; pathIdx < groupPaths.length; pathIdx++) {
                    if (pathIdx === segment.pathIdx) continue;

                    const otherPath = groupPaths[pathIdx];
                    const otherBBox = otherPath.getBBox();
                    const otherPoints = this.extractPathBoundaryPoints(otherPath, otherBBox);

                    // Check distance from segment midpoint to other path
                    const midpoint = {
                        x: (segment.start.x + segment.end.x) / 2,
                        y: (segment.start.y + segment.end.y) / 2
                    };

                    const distToOtherPath = this._minDistToSet(midpoint, otherPoints);

                    // Only remove if very close (truly between paths)
                    if (distToOtherPath < 2.0) {
                        isInternal = true;
                        console.debug(`[merge] Removed internal segment at (${midpoint.x.toFixed(1)}, ${midpoint.y.toFixed(1)}) from path ${segment.pathIdx}, dist to other path: ${distToOtherPath.toFixed(1)}px`);
                        break;
                    }
                }

                if (isInternal) {
                    segment.removed = true;
                    removedCount++;
                }
            }

            console.debug(`[merge] Removed ${removedCount} internal segments`);

            // Step 4: Build unified boundary from remaining segments
            const remainingSegments = pathSegments.filter(seg => !seg.removed);

            if (remainingSegments.length === 0) {
                console.warn('[merge] No segments remaining after internal removal');
                return null;
            }

            console.debug(`[merge] Attempting to build boundary from ${remainingSegments.length} remaining segments`);

            // Group remaining segments by path
            const segmentsByPath = new Map();
            for (const segment of remainingSegments) {
                if (!segmentsByPath.has(segment.pathIdx)) {
                    segmentsByPath.set(segment.pathIdx, []);
                }
                segmentsByPath.get(segment.pathIdx).push(segment);
            }

            console.debug(`[merge] Segments by path:`, Array.from(segmentsByPath.entries()).map(([pathIdx, segs]) => `Path${pathIdx}: ${segs.length} segments`));

            // Start with first segment from first path
            const unifiedBoundary = [remainingSegments[0].start, remainingSegments[0].end];
            remainingSegments[0].used = true;
            console.debug(`[merge] Starting boundary with segment from path ${remainingSegments[0].pathIdx}: (${remainingSegments[0].start.x.toFixed(1)}, ${remainingSegments[0].start.y.toFixed(1)}) -> (${remainingSegments[0].end.x.toFixed(1)}, ${remainingSegments[0].end.y.toFixed(1)})`);

            // Try to connect segments into a continuous boundary
            let attempts = 0;
            const maxAttempts = remainingSegments.length * 2; // Prevent infinite loops

            while (attempts < maxAttempts) {
                attempts++;
                const lastPoint = unifiedBoundary[unifiedBoundary.length - 1];
                let foundConnection = false;

                // Look for closest unused segment endpoint
                let bestMatch = null;
                let bestDistance = Infinity;

                for (const segment of remainingSegments) {
                    if (segment.used) continue;

                    const distToStart = this.calculateDistance(lastPoint, segment.start);
                    const distToEnd = this.calculateDistance(lastPoint, segment.end);

                    if (distToStart < bestDistance) {
                        bestDistance = distToStart;
                        bestMatch = { segment, useStart: true, distance: distToStart };
                    }
                    if (distToEnd < bestDistance) {
                        bestDistance = distToEnd;
                        bestMatch = { segment, useStart: false, distance: distToEnd };
                    }
                }

                // Use best match if it's close enough
                const POINT_MERGE_TOLERANCE = 5.0;
                if (bestMatch && bestMatch.distance <= POINT_MERGE_TOLERANCE) {
                    const nextPoint = bestMatch.useStart ? bestMatch.segment.end : bestMatch.segment.start;
                    unifiedBoundary.push(nextPoint);
                    bestMatch.segment.used = true;
                    foundConnection = true;

                    console.debug(`[merge] Connected to path ${bestMatch.segment.pathIdx} segment at distance ${bestMatch.distance.toFixed(1)}px -> (${nextPoint.x.toFixed(1)}, ${nextPoint.y.toFixed(1)})`);
                } else {
                    console.debug(`[merge] No close connections found (best: ${bestMatch ? bestMatch.distance.toFixed(1) : 'none'}px), stopping boundary building`);
                }

                if (!foundConnection) break;
            }

            console.debug(`[merge] Boundary building completed after ${attempts} attempts`);

            console.debug(`[merge] Built unified boundary with ${unifiedBoundary.length} points`);

            // Debug: Final unified boundary data
            if (unifiedBoundary.length > 0) {
                const finalBounds = {
                    minX: Math.min(...unifiedBoundary.map(p => p.x)).toFixed(1),
                    maxX: Math.max(...unifiedBoundary.map(p => p.x)).toFixed(1),
                    minY: Math.min(...unifiedBoundary.map(p => p.y)).toFixed(1),
                    maxY: Math.max(...unifiedBoundary.map(p => p.y)).toFixed(1),
                    width: (Math.max(...unifiedBoundary.map(p => p.x)) - Math.min(...unifiedBoundary.map(p => p.x))).toFixed(1),
                    height: (Math.max(...unifiedBoundary.map(p => p.y)) - Math.min(...unifiedBoundary.map(p => p.y))).toFixed(1)
                };

                const sampleFinalPoints = unifiedBoundary.slice(0, 8).map(p => `(${p.x.toFixed(1)}, ${p.y.toFixed(1)})`).join(', ');

                console.debug(`[merge] Final unified boundary bounds:`, finalBounds);
                console.debug(`[merge] Final boundary sample points: ${sampleFinalPoints}${unifiedBoundary.length > 8 ? '...' : ''}`);

                // Compare original vs final coverage
                console.debug(`[merge] COMPARISON:`);
                console.debug(`[merge]   Original total bounds: ${originalPathData.map(p => `Path${p.pathIdx}: ${p.bounds.width}x${p.bounds.height}`).join(', ')}`);
                console.debug(`[merge]   Final unified bounds: ${finalBounds.width}x${finalBounds.height}`);
                console.debug(`[merge]   Original total points: ${originalPathData.reduce((sum, p) => sum + p.pointCount, 0)} -> Final points: ${unifiedBoundary.length}`);
            }

            return unifiedBoundary;

        } catch (error) {
            console.warn('[merge] Error in geometric boundary merging:', error);
            return null;
        }
    }

    // Create outer edge path using line segments and winding algorithm
    _createOverlappingPathMerge(groupPaths, allBoundaryPoints) {
        const overallStartTime = performance.now();
        try {
            // Use original working winding algorithm
            const debugWinding = false; // Set to true to debug winding algorithm
        if (debugWinding) console.debug('[winding] Creating outer edge path using winding algorithm');

            // Step 1: Convert all paths to line segments only (no curves)
            const step1StartTime = performance.now();
            const lineSegmentPaths = this._convertPathsToLineSegments(groupPaths);
            const step1Time = performance.now() - step1StartTime;
            if (debugWinding) console.debug(`[winding] Step 1 (convert to line segments): ${step1Time.toFixed(1)}ms`);

            // Step 2: Join coincident points using tolerance (only between different paths)
            const step2StartTime = performance.now();
            const POINT_MERGE_TOLERANCE = 5.0;
            const joinedPaths = this._joinCoincidentPoints(lineSegmentPaths, POINT_MERGE_TOLERANCE);
            const step2Time = performance.now() - step2StartTime;
            if (debugWinding) console.debug(`[winding] Step 2 (join coincident points): ${step2Time.toFixed(1)}ms`);

            // Step 2.5: Mark shared/internal segments (segments that overlap between paths)
            const step25StartTime = performance.now();
            const markedPaths = this._markSharedSegments(joinedPaths, POINT_MERGE_TOLERANCE);
            const step25Time = performance.now() - step25StartTime;
            if (debugWinding) console.debug(`[winding] Step 2.5 (mark shared segments): ${step25Time.toFixed(1)}ms`);

            // Debug: Print complete segment list after merging and marking (only if verbose logging enabled)
            if (this.verboseLogging) {
                console.debug('[winding] === COMPLETE SEGMENT LIST AFTER MERGING ===');
                for (const seg of markedPaths) {
                    console.debug(`[winding]   Segment ${seg.pathIdx}_${seg.segmentIdx}: (${seg.start.x.toFixed(1)}, ${seg.start.y.toFixed(1)}) → (${seg.end.x.toFixed(1)}, ${seg.end.y.toFixed(1)}) [internal: ${seg.isInternal}]`);
                }
                console.debug('[winding] === END SEGMENT LIST ===');
            }

            // Step 3: Join the paths together into a network
            const step3StartTime = performance.now();
            const pathNetwork = this._joinPathsIntoNetwork(markedPaths);
            const step3Time = performance.now() - step3StartTime;
            if (debugWinding) console.debug(`[winding] Step 3 (join paths into network): ${step3Time.toFixed(1)}ms`);

            // Step 4: Use winding algorithm to traverse outer edge
            const step4StartTime = performance.now();
            const outerEdgePoints = this._traverseOuterEdge(pathNetwork);
            const step4Time = performance.now() - step4StartTime;
            if (debugWinding) console.debug(`[winding] Step 4 (traverse outer edge): ${step4Time.toFixed(1)}ms`);

            const overallTime = performance.now() - overallStartTime;
            if (debugWinding) console.debug(`[winding] Created outer edge with ${outerEdgePoints.length} points in ${overallTime.toFixed(1)}ms total`);

            return outerEdgePoints;

        } catch (error) {
            console.warn('[winding] Error in winding algorithm merge:', error);
            return null;
        }
    }

    // Step 1: Convert paths to line segments only (no curves)
    _convertPathsToLineSegments(groupPaths) {
        console.debug('[winding] Converting paths to line segments only');

        const lineSegmentPaths = [];

        for (let i = 0; i < groupPaths.length; i++) {
            const path = groupPaths[i];
            const pathData = path.attr('d');

            if (!pathData) {
                console.warn(`[winding] Path ${i} has no 'd' attribute`);
                continue;
            }

            // Parse path data and convert curves to line segments
            const lineSegments = this._parsePathToLineSegments(pathData, path, i);
            lineSegmentPaths.push({
                pathIdx: i,
                segments: lineSegments,
                originalPath: path
            });

            console.debug(`[winding] Path ${i}: converted to ${lineSegments.length} line segments`);
        }

        return lineSegmentPaths;
    }

    // Convert path to simple polygon using boundary point extraction
    _parsePathToLineSegments(pathData, path, pathIdx) {
        const segments = [];

        try {
            // Extract only the actual SVG command points (4 points per path)
            const boundaryPoints = this._extractPathPoints(pathData);

            console.debug(`[winding] Path ${pathIdx} original points: ${boundaryPoints.map(p => `(${p.x.toFixed(1)}, ${p.y.toFixed(1)})`).join(', ')}`);

            if (boundaryPoints.length < 2) {
                console.warn(`[winding] Path ${pathIdx} has insufficient boundary points: ${boundaryPoints.length}`);
                return segments;
            }

            // Create line segments between consecutive boundary points
            for (let i = 1; i < boundaryPoints.length; i++) {
                segments.push({
                    start: { x: boundaryPoints[i-1].x, y: boundaryPoints[i-1].y },
                    end: { x: boundaryPoints[i].x, y: boundaryPoints[i].y },
                    pathIdx: pathIdx,
                    segmentIdx: segments.length
                });
            }

            // Add closing segment back to start point (complete the polygon)
            if (boundaryPoints.length > 2) {
                segments.push({
                    start: { x: boundaryPoints[boundaryPoints.length-1].x, y: boundaryPoints[boundaryPoints.length-1].y },
                    end: { x: boundaryPoints[0].x, y: boundaryPoints[0].y },
                    pathIdx: pathIdx,
                    segmentIdx: segments.length
                });
            }

        } catch (error) {
            console.warn(`[winding] Error parsing path ${pathIdx}:`, error);
        }

        return segments;
    }

    // Extract actual points from SVG path data (preserves sharp corners)
    _extractPathPoints(pathData) {
        const points = [];

        console.debug(`[winding] Parsing path data: ${pathData}`);

        // Simple regex-based parser for common SVG commands
        const commands = pathData.match(/[MmLlHhVvCcSsQqTtAaZz][^MmLlHhVvCcSsQqTtAaZz]*/g) || [];

        let currentPoint = { x: 0, y: 0 };

        for (const cmdStr of commands) {
            const cmd = cmdStr[0];
            const params = cmdStr.slice(1).trim().split(/[\s,]+/).map(Number).filter(n => !isNaN(n));

            console.debug(`[winding] Processing command: ${cmd} with params: [${params.join(', ')}]`);

            switch (cmd.toLowerCase()) {
                case 'm': // Move to
                    if (cmd === 'M') {
                        currentPoint = { x: params[0], y: params[1] };
                    } else {
                        currentPoint = { x: currentPoint.x + params[0], y: currentPoint.y + params[1] };
                    }
                    points.push({ ...currentPoint });

                    // Handle implicit line commands after move (extra coordinate pairs)
                    for (let i = 2; i < params.length; i += 2) {
                        if (i + 1 < params.length) {
                            if (cmd === 'M') {
                                currentPoint = { x: params[i], y: params[i + 1] };
                            } else {
                                currentPoint = { x: currentPoint.x + params[i], y: currentPoint.y + params[i + 1] };
                            }
                            points.push({ ...currentPoint });
                        }
                    }
                    break;

                case 'l': // Line to
                    // Handle multiple coordinate pairs (each pair is a line segment)
                    for (let i = 0; i < params.length; i += 2) {
                        if (i + 1 < params.length) {
                            if (cmd === 'L') {
                                currentPoint = { x: params[i], y: params[i + 1] };
                            } else {
                                currentPoint = { x: currentPoint.x + params[i], y: currentPoint.y + params[i + 1] };
                            }
                            points.push({ ...currentPoint });
                        }
                    }
                    break;

                case 'h': // Horizontal line
                    if (cmd === 'H') {
                        currentPoint.x = params[0];
                    } else {
                        currentPoint.x += params[0];
                    }
                    points.push({ ...currentPoint });
                    break;

                case 'v': // Vertical line
                    if (cmd === 'V') {
                        currentPoint.y = params[0];
                    } else {
                        currentPoint.y += params[0];
                    }
                    points.push({ ...currentPoint });
                    break;

                case 'c': // Cubic Bezier curve - extract end point only
                    if (cmd === 'C') {
                        currentPoint = { x: params[4], y: params[5] };
                    } else {
                        currentPoint = { x: currentPoint.x + params[4], y: currentPoint.y + params[5] };
                    }
                    points.push({ ...currentPoint });
                    break;

                case 'q': // Quadratic Bezier curve - extract end point only
                    if (cmd === 'Q') {
                        currentPoint = { x: params[2], y: params[3] };
                    } else {
                        currentPoint = { x: currentPoint.x + params[2], y: currentPoint.y + params[3] };
                    }
                    points.push({ ...currentPoint });
                    break;

                case 'z': // Close path
                    // Don't add duplicate point for close
                    break;
            }
        }

        return points;
    }

    // Step 2: Join coincident points using tolerance
    _joinCoincidentPoints(lineSegmentPaths, tolerance) {
        console.debug(`[winding] Joining coincident points with ${tolerance}px tolerance`);

        // Create a flat list of all segments with unique IDs
        const allSegments = [];
        for (const pathData of lineSegmentPaths) {
            for (const segment of pathData.segments) {
                allSegments.push({
                    ...segment,
                    id: `${segment.pathIdx}_${segment.segmentIdx}`
                });
            }
        }

        console.debug(`[winding] Processing ${allSegments.length} total segments`);

        // Use optimized spatial hash approach if available
        if (typeof window.SpatialHash !== 'undefined' && this.useFastMode) {
            // Optimized spatial hash algorithm in use
            const mergedSegments = this._mergeCoincidentPointsOptimized(allSegments, tolerance);
            // After optimized merging: ${mergedSegments.length} segments
            return mergedSegments;
        }

        // Fall back to original algorithm
        const mergedSegments = this._mergeCoincidentPoints(allSegments, tolerance);
        console.debug(`[winding] After coincident point merging: ${mergedSegments.length} segments`);

        return mergedSegments;
    }

    // Merge adjacent points between different paths only (Rules 5-9)
    _mergeCoincidentPoints(allSegments, tolerance) {
        console.debug(`[winding] Merging adjacent points with ${tolerance}px tolerance (different paths only)`);

        // First, find ALL potential connections within tolerance
        const potentialConnections = [];

        // Find adjacent point pairs between different paths only (Rule 5, 6)
        for (let i = 0; i < allSegments.length; i++) {
            const seg1 = allSegments[i];
            for (let j = i + 1; j < allSegments.length; j++) {
                const seg2 = allSegments[j];

                // Rule 5: NEVER merge points from the same path
                if (seg1.pathIdx === seg2.pathIdx) {
                    continue;
                }

                // Check all endpoint combinations
                const combinations = [
                    { point1: seg1.start, point2: seg2.start, seg1, seg2, end1: 'start', end2: 'start' },
                    { point1: seg1.start, point2: seg2.end, seg1, seg2, end1: 'start', end2: 'end' },
                    { point1: seg1.end, point2: seg2.start, seg1, seg2, end1: 'end', end2: 'start' },
                    { point1: seg1.end, point2: seg2.end, seg1, seg2, end1: 'end', end2: 'end' }
                ];

                for (const combo of combinations) {
                    // Check distance for potential connection
                    const distance = this.calculateDistance(combo.point1, combo.point2);

                    console.debug(`[winding] - Checking seg${seg1.pathIdx}_${seg1.segmentIdx}.${combo.end1} (${combo.point1.x.toFixed(1)}, ${combo.point1.y.toFixed(1)}) vs seg${seg2.pathIdx}_${seg2.segmentIdx}.${combo.end2} (${combo.point2.x.toFixed(1)}, ${combo.point2.y.toFixed(1)}) = ${distance.toFixed(1)}px`);

                    if (distance <= tolerance) {
                        const point1Key = `${seg1.pathIdx}_${seg1.segmentIdx}_${combo.end1}`;
                        const point2Key = `${seg2.pathIdx}_${seg2.segmentIdx}_${combo.end2}`;

                        // Create a geometric key to deduplicate same point pairs
                        const geomKey1 = `${combo.point1.x.toFixed(2)}_${combo.point1.y.toFixed(2)}`;
                        const geomKey2 = `${combo.point2.x.toFixed(2)}_${combo.point2.y.toFixed(2)}`;
                        const pairKey = geomKey1 < geomKey2 ? `${geomKey1}-${geomKey2}` : `${geomKey2}-${geomKey1}`;

                        // Only add if we haven't seen this geometric point pair before
                        if (!potentialConnections.some(conn => {
                            const connGeomKey1 = `${conn.point1.x.toFixed(2)}_${conn.point1.y.toFixed(2)}`;
                            const connGeomKey2 = `${conn.point2.x.toFixed(2)}_${conn.point2.y.toFixed(2)}`;
                            const connPairKey = connGeomKey1 < connGeomKey2 ? `${connGeomKey1}-${connGeomKey2}` : `${connGeomKey2}-${connGeomKey1}`;
                            return connPairKey === pairKey;
                        })) {
                            console.debug(`[winding] - POTENTIAL CONNECTION: ${distance.toFixed(1)}px (new geometric pair)`);
                            potentialConnections.push({
                                distance,
                                seg1: combo.seg1,
                                seg2: combo.seg2,
                                end1: combo.end1,
                                end2: combo.end2,
                                point1: combo.point1,
                                point2: combo.point2,
                                point1Key,
                                point2Key
                            });
                        } else {
                            console.debug(`[winding] - DUPLICATE geometric pair: ${distance.toFixed(1)}px (skipping)`);
                        }
                    }
                }
            }
        }

        console.debug(`[winding] Found ${potentialConnections.length} potential connections, sorted by distance: ${potentialConnections.map(p => `${p.distance.toFixed(1)}px`).join(', ')}`);

        // NEW APPROACH: Cluster all points within tolerance using union-find
        // This allows 3+ points to merge into a single super-node
        console.debug(`[winding] Clustering points using union-find algorithm`);
        
        // Create a map of all unique points
        const pointMap = new Map();
        for (const segment of allSegments) {
            const startKey = `${segment.pathIdx}_${segment.segmentIdx}_start`;
            const endKey = `${segment.pathIdx}_${segment.segmentIdx}_end`;
            pointMap.set(startKey, { point: segment.start, key: startKey });
            pointMap.set(endKey, { point: segment.end, key: endKey });
        }

        // Union-Find data structure
        const parent = new Map();
        const rank = new Map();
        
        for (const key of pointMap.keys()) {
            parent.set(key, key);
            rank.set(key, 0);
        }

        function find(x) {
            if (parent.get(x) !== x) {
                parent.set(x, find(parent.get(x)));
            }
            return parent.get(x);
        }

        function union(x, y) {
            const rootX = find(x);
            const rootY = find(y);
            
            if (rootX === rootY) return;
            
            const rankX = rank.get(rootX);
            const rankY = rank.get(rootY);
            
            if (rankX < rankY) {
                parent.set(rootX, rootY);
            } else if (rankX > rankY) {
                parent.set(rootY, rootX);
            } else {
                parent.set(rootY, rootX);
                rank.set(rootX, rankX + 1);
            }
        }

        // Union all points that are within tolerance and from different paths
        let connectionsApplied = 0;
        for (const connection of potentialConnections) {
            // Get the segments these points belong to
            const seg1Path = connection.point1Key.split('_')[0];
            const seg2Path = connection.point2Key.split('_')[0];
            
            if (seg1Path !== seg2Path) {
                union(connection.point1Key, connection.point2Key);
                connectionsApplied++;
                console.debug(`[winding] - Unioning ${connection.point1Key} and ${connection.point2Key} (${connection.distance.toFixed(1)}px)`);
            }
        }

        console.debug(`[winding] Applied ${connectionsApplied} unions`);

        // Group points by their root (cluster)
        const clusters = new Map();
        for (const [key, pointData] of pointMap) {
            const root = find(key);
            if (!clusters.has(root)) {
                clusters.set(root, []);
            }
            clusters.get(root).push(pointData);
        }

        console.debug(`[winding] Created ${clusters.size} clusters from ${pointMap.size} points`);

        // For each cluster with 2+ points, create a super-node at their average position
        const superNodeMap = new Map(); // Maps original point key to super-node location
        
        for (const [root, clusterPoints] of clusters) {
            if (clusterPoints.length > 1) {
                // Check if cluster has points from multiple paths
                const paths = new Set(clusterPoints.map(p => p.key.split('_')[0]));
                if (paths.size > 1) {
                    const superNode = {
                        x: clusterPoints.reduce((sum, p) => sum + p.point.x, 0) / clusterPoints.length,
                        y: clusterPoints.reduce((sum, p) => sum + p.point.y, 0) / clusterPoints.length
                    };
                    // Super-node created
                    
                    for (const pointData of clusterPoints) {
                        superNodeMap.set(pointData.key, superNode);
                    }
                }
            }
        }

        // Created point mappings to super-nodes

        // Additional pass: Map same-path points that share coordinates with super-node points
        // This handles cases where segment endpoints from the same path have the same coordinates
        // as points that were merged across paths (e.g., 1_1_start at same location as 1_0_end)
        const EXACT_MATCH_TOLERANCE = 0.1;
        for (const [mappedKey, superNode] of superNodeMap) {
            const mappedPoint = pointMap.get(mappedKey).point;
            
            // Find all other points at the same location
            for (const [otherKey, otherData] of pointMap) {
                if (!superNodeMap.has(otherKey)) {
                    const dist = this.calculateDistance(mappedPoint, otherData.point);
                    if (dist < EXACT_MATCH_TOLERANCE) {
                        superNodeMap.set(otherKey, superNode);
                        console.debug(`[winding] - Including same-path point ${otherKey} in super-node (distance: ${dist.toFixed(2)}px)`);
                    }
                }
            }
        }

        console.debug(`[winding] Final super-node mappings: ${superNodeMap.size}`);

        // Rule 10: Preserve segment count, update endpoints to super-nodes
        const modifiedSegments = allSegments.map(seg => {
            const startKey = `${seg.pathIdx}_${seg.segmentIdx}_start`;
            const endKey = `${seg.pathIdx}_${seg.segmentIdx}_end`;
            
            return {
                ...seg,
                start: superNodeMap.has(startKey) ? superNodeMap.get(startKey) : { ...seg.start },
                end: superNodeMap.has(endKey) ? superNodeMap.get(endKey) : { ...seg.end }
            };
        });

        return modifiedSegments;
    }

    // Optimized merge using spatial hash for O(n log n) performance instead of O(n²)
    _mergeCoincidentPointsOptimized(allSegments, tolerance) {
        // Timing: Spatial hash merge
        // Using optimized spatial hash merging

        // Create spatial hash for fast point lookup
        const spatialHash = new window.SpatialHash(tolerance * 2);
        const pointIndex = new Map(); // Maps point key to point data

        // Build spatial index of all segment endpoints
        for (const segment of allSegments) {
            const startKey = `${segment.pathIdx}_${segment.segmentIdx}_start`;
            const endKey = `${segment.pathIdx}_${segment.segmentIdx}_end`;

            const startData = {
                point: segment.start,
                segment,
                end: 'start',
                key: startKey
            };

            const endData = {
                point: segment.end,
                segment,
                end: 'end',
                key: endKey
            };

            pointIndex.set(startKey, startData);
            pointIndex.set(endKey, endData);

            spatialHash.add(segment.start, startData);
            spatialHash.add(segment.end, endData);
        }

        // Built spatial index

        // Find potential connections using spatial indexing
        const potentialConnections = [];
        const processedPairs = new Set();

        for (const [pointKey, pointData] of pointIndex) {
            const neighbors = spatialHash.findNear(pointData.point, tolerance);

            for (const neighbor of neighbors) {
                if (pointKey === neighbor.data.key) continue;

                // Skip same path (Rule 5)
                if (pointData.segment.pathIdx === neighbor.data.segment.pathIdx) {
                    continue;
                }

                // Create consistent pair key to avoid duplicates
                const pairKey = pointKey < neighbor.data.key ?
                    `${pointKey}:${neighbor.data.key}` :
                    `${neighbor.data.key}:${pointKey}`;

                if (processedPairs.has(pairKey)) continue;
                processedPairs.add(pairKey);

                if (neighbor.distance <= tolerance) {
                    potentialConnections.push({
                        distance: neighbor.distance,
                        seg1: pointData.segment,
                        seg2: neighbor.data.segment,
                        end1: pointData.end,
                        end2: neighbor.data.end,
                        point1: pointData.point,
                        point2: neighbor.data.point,
                        point1Key: pointKey,
                        point2Key: neighbor.data.key
                    });
                }
            }
        }

        // Found potential connections using spatial hash
        // Spatial hash merge completed

        // Use existing union-find clustering logic
        return this._clusterAndMergePoints(allSegments, potentialConnections);
    }

    // Union-find clustering logic extracted for reuse
    _clusterAndMergePoints(allSegments, potentialConnections) {
        // Timing: Union-find clustering

        // Create union-find data structure for clustering
        const unionFind = new Map();
        const pointCoords = new Map();

        // Initialize each point as its own cluster
        for (const segment of allSegments) {
            const startKey = `${segment.pathIdx}_${segment.segmentIdx}_start`;
            const endKey = `${segment.pathIdx}_${segment.segmentIdx}_end`;

            unionFind.set(startKey, startKey);
            unionFind.set(endKey, endKey);
            pointCoords.set(startKey, segment.start);
            pointCoords.set(endKey, segment.end);
        }

        // Union-find helper functions
        const find = (key) => {
            if (unionFind.get(key) !== key) {
                unionFind.set(key, find(unionFind.get(key))); // Path compression
            }
            return unionFind.get(key);
        };

        const union = (key1, key2) => {
            const root1 = find(key1);
            const root2 = find(key2);
            if (root1 !== root2) {
                unionFind.set(root2, root1);
                return true;
            }
            return false;
        };

        // Apply unions for all potential connections
        let unionsApplied = 0;
        for (const connection of potentialConnections) {
            if (union(connection.point1Key, connection.point2Key)) {
                unionsApplied++;
            }
        }

        // Applied unions

        // Group points by their root cluster
        const clusters = new Map();
        for (const [pointKey, _] of unionFind) {
            const root = find(pointKey);
            if (!clusters.has(root)) {
                clusters.set(root, []);
            }
            clusters.get(root).push(pointKey);
        }

        // Created clusters from points

        // Create super-nodes for clusters with multiple points
        const superNodes = new Map();
        const pointToSuperNode = new Map();

        for (const [root, pointKeys] of clusters) {
            if (pointKeys.length > 1) {
                // Calculate centroid for super-node
                let sumX = 0, sumY = 0;
                for (const pointKey of pointKeys) {
                    const coords = pointCoords.get(pointKey);
                    sumX += coords.x;
                    sumY += coords.y;
                }
                const centroid = { x: sumX / pointKeys.length, y: sumY / pointKeys.length };

                superNodes.set(root, { point: centroid, members: pointKeys });

                for (const pointKey of pointKeys) {
                    pointToSuperNode.set(pointKey, root);
                }

                // Super-node created
            }
        }

        // Created point mappings to super-nodes
        // Union-find clustering completed

        // Apply super-node coordinates to segments
        const mergedSegments = allSegments.map(segment => {
            const startKey = `${segment.pathIdx}_${segment.segmentIdx}_start`;
            const endKey = `${segment.pathIdx}_${segment.segmentIdx}_end`;

            const newSegment = { ...segment };

            if (pointToSuperNode.has(startKey)) {
                const superNode = superNodes.get(pointToSuperNode.get(startKey));
                newSegment.start = superNode.point;
            }

            if (pointToSuperNode.has(endKey)) {
                const superNode = superNodes.get(pointToSuperNode.get(endKey));
                newSegment.end = superNode.point;
            }

            return newSegment;
        });

        return mergedSegments;
    }

    // Mark shared/internal segments that should not be part of the outer boundary
    // A segment is "shared" if it overlaps with a segment from a different path going in the opposite direction
    // NOTE: This should use exact coordinate matching (0.01px) because we only want to mark segments
    // whose endpoints were actually merged to the same location in the point merging step
    _markSharedSegments(allSegments, tolerance = 0.01) {
        console.debug(`[winding] Marking shared/internal segments with exact coordinate matching (tolerance ${tolerance}px)`);

        // Create a copy of segments with isInternal flag
        const markedSegments = allSegments.map(seg => ({
            ...seg,
            isInternal: false
        }));

        let sharedCount = 0;

        // Compare all segment pairs from different paths
        for (let i = 0; i < markedSegments.length; i++) {
            const seg1 = markedSegments[i];
            
            for (let j = i + 1; j < markedSegments.length; j++) {
                const seg2 = markedSegments[j];

                // Only check segments from different paths
                if (seg1.pathIdx === seg2.pathIdx) continue;

                // Check if segments overlap (same endpoints, opposite directions)
                const sameDirection = 
                    this._pointsMatch(seg1.start, seg2.start, tolerance) &&
                    this._pointsMatch(seg1.end, seg2.end, tolerance);

                const oppositeDirection = 
                    this._pointsMatch(seg1.start, seg2.end, tolerance) &&
                    this._pointsMatch(seg1.end, seg2.start, tolerance);

                if (sameDirection || oppositeDirection) {
                    // Mark both segments as internal (shared edge)
                    if (!seg1.isInternal) {
                        seg1.isInternal = true;
                        sharedCount++;
                        console.debug(`[winding] - Segment ${seg1.pathIdx}_${seg1.segmentIdx} marked as INTERNAL (shared with ${seg2.pathIdx}_${seg2.segmentIdx})`);
                    }
                    if (!seg2.isInternal) {
                        seg2.isInternal = true;
                        sharedCount++;
                        console.debug(`[winding] - Segment ${seg2.pathIdx}_${seg2.segmentIdx} marked as INTERNAL (shared with ${seg1.pathIdx}_${seg1.segmentIdx})`);
                    }
                }
            }
        }

        console.debug(`[winding] Marked ${sharedCount} segments as internal (shared between paths)`);
        return markedSegments;
    }

    // Helper: check if two points match within tolerance
    _pointsMatch(p1, p2, tolerance) {
        const dx = p1.x - p2.x;
        const dy = p1.y - p2.y;
        return Math.sqrt(dx * dx + dy * dy) <= tolerance;
    }

    // Step 3: Join paths into a single network
    _joinPathsIntoNetwork(segments) {
        console.debug('[winding] Joining paths into network');

        // Create adjacency map: point -> [segments that touch this point]
        const pointToSegments = new Map();

        for (const segment of segments) {
            const startKey = `${segment.start.x.toFixed(2)}_${segment.start.y.toFixed(2)}`;
            const endKey = `${segment.end.x.toFixed(2)}_${segment.end.y.toFixed(2)}`;

            if (!pointToSegments.has(startKey)) {
                pointToSegments.set(startKey, []);
            }
            if (!pointToSegments.has(endKey)) {
                pointToSegments.set(endKey, []);
            }

            pointToSegments.get(startKey).push({ segment, isStart: true });
            pointToSegments.get(endKey).push({ segment, isStart: false });
        }

        console.debug(`[winding] Created network with ${pointToSegments.size} nodes and ${segments.length} edges`);

        // Debug: show all points in the network
        for (const [pointKey, connections] of pointToSegments) {
            const [x, y] = pointKey.split('_').map(Number);
            console.debug(`[winding] Network point (${x.toFixed(1)}, ${y.toFixed(1)}) connects to ${connections.length} segments: ${connections.map(c => c.segment.id).join(', ')}`);
        }

        return {
            segments: segments,
            pointToSegments: pointToSegments
        };
    }

    // Step 4: Traverse outer edge using winding algorithm
    _traverseOuterEdge(pathNetwork) {
        console.debug('[winding] Traversing outer edge with winding algorithm');

        const { segments, pointToSegments } = pathNetwork;

        if (segments.length === 0) {
            console.warn('[winding] No segments to traverse');
            return [];
        }

        // Calculate centroid of all points for outer-facing detection
        let centroidX = 0, centroidY = 0, pointCount = 0;
        for (const [pointKey, _] of pointToSegments) {
            const [x, y] = pointKey.split('_').map(Number);
            centroidX += x;
            centroidY += y;
            pointCount++;
        }
        const centroid = { x: centroidX / pointCount, y: centroidY / pointCount };
        console.debug(`[winding] Shape centroid: (${centroid.x.toFixed(1)}, ${centroid.y.toFixed(1)})`);

        // Find the leftmost point as starting point (guaranteed to be on outer edge)
        let startPoint = null;
        let startKey = null;
        for (const [pointKey, _] of pointToSegments) {
            const [x, y] = pointKey.split('_').map(Number);
            const point = { x, y };

            if (!startPoint || x < startPoint.x || (x === startPoint.x && y < startPoint.y)) {
                startPoint = point;
                startKey = pointKey;
            }
        }

        console.debug(`[winding] Starting traversal from leftmost point: (${startPoint.x.toFixed(1)}, ${startPoint.y.toFixed(1)})`);

        // Traverse the outer edge by always taking the most clockwise turn (rightmost/outward)
        const outerEdgePoints = [startPoint];
        const visitedSegments = new Set();
        let currentPoint = startPoint;
        let currentKey = startKey;
        let incomingAngle = null; // Angle we came from

        const maxIterations = segments.length * 2; // Prevent infinite loops
        let iterations = 0;

        while (iterations < maxIterations) {
            iterations++;

            const connectedSegments = pointToSegments.get(currentKey) || [];
            const internalSegments = connectedSegments.filter(conn => conn.segment.isInternal);
            const availableSegments = connectedSegments.filter(conn => 
                !visitedSegments.has(conn.segment.id) && !conn.segment.isInternal
            );

            console.debug(`[winding] Iteration ${iterations}: At point (${currentPoint.x.toFixed(1)}, ${currentPoint.y.toFixed(1)})`);
            console.debug(`[winding] - Connected segments: ${connectedSegments.length}, Internal: ${internalSegments.length}, Available: ${availableSegments.length}`);
            if (internalSegments.length > 0) {
                console.debug(`[winding] - Excluding internal segments: ${internalSegments.map(conn => conn.segment.id).join(', ')}`);
            }
            console.debug(`[winding] - Visited segments: [${Array.from(visitedSegments).join(', ')}]`);
            if (availableSegments.length > 0) {
                console.debug(`[winding] - Available segment destinations: ${availableSegments.map(conn => {
                    const nextPt = conn.isStart ? conn.segment.end : conn.segment.start;
                    return `${conn.segment.id}→(${nextPt.x.toFixed(1)},${nextPt.y.toFixed(1)})`;
                }).join(', ')}`);
            }

            // FAILURE DETECTION: Check if we're being forced into a potentially internal path
            if (availableSegments.length === 1) {
                const forcedSegment = availableSegments[0].segment;
                const nextPoint = availableSegments[0].isStart ? forcedSegment.end : forcedSegment.start;
                const nextKey = `${nextPoint.x.toFixed(2)}_${nextPoint.y.toFixed(2)}`;
                const nextConnections = pointToSegments.get(nextKey) || [];

                console.debug(`[winding] - FORCED CHOICE: Only 1 segment available, going to (${nextPoint.x.toFixed(1)}, ${nextPoint.y.toFixed(1)}) with ${nextConnections.length} connections`);

                // Flag potential internal segment: going to a high-degree vertex (merge point)
                if (nextConnections.length >= 3) {
                    console.debug(`[winding] - ⚠️  POTENTIAL INTERNAL SEGMENT: Forced to high-degree vertex (${nextConnections.length} connections)`);
                }
            }

            if (availableSegments.length === 0) {
                console.debug('[winding] No more available segments, traversal complete');
                console.debug(`[winding] - Total segments in network: ${segments.length}`);
                console.debug(`[winding] - Segments visited: ${visitedSegments.size}`);
                break;
            }

            // Choose the segment that represents the most clockwise turn (smallest angle = most outward)
            let bestSegment = null;
            let bestAngle = null;
            let bestConn = null;

            for (const conn of availableSegments) {
                const segment = conn.segment;
                const nextPoint = conn.isStart ? segment.end : segment.start;
                const outgoingAngle = Math.atan2(nextPoint.y - currentPoint.y, nextPoint.x - currentPoint.x);

                // Calculate the turn angle relative to incoming direction
                let turnAngle = outgoingAngle;
                if (incomingAngle !== null) {
                    turnAngle = outgoingAngle - incomingAngle;
                    // Normalize to [0, 2π)
                    while (turnAngle < 0) turnAngle += 2 * Math.PI;
                    while (turnAngle >= 2 * Math.PI) turnAngle -= 2 * Math.PI;
                }

                // Check if this segment leads away from centroid (outer-facing)
                const fromCentroidX = currentPoint.x - centroid.x;
                const fromCentroidY = currentPoint.y - centroid.y;
                const toNextX = nextPoint.x - currentPoint.x;
                const toNextY = nextPoint.y - currentPoint.y;

                // Dot product: positive means pointing away from centroid (outer-facing)
                const dotProduct = fromCentroidX * toNextX + fromCentroidY * toNextY;
                const isOuterFacing = dotProduct > 0;

                console.debug(`[winding] - Candidate segment ${segment.id}: to (${nextPoint.x.toFixed(1)}, ${nextPoint.y.toFixed(1)}), turn angle: ${(turnAngle * 180 / Math.PI).toFixed(1)}°, outer-facing: ${isOuterFacing}`);

                // For outer edge, we want the SMALLEST turn angle (most clockwise = most outward)
                // Treat angles near 360° as small angles (e.g., 350° -> 10°)
                let normalizedAngle = turnAngle;
                if (turnAngle > Math.PI) {
                    normalizedAngle = 2 * Math.PI - turnAngle;
                }

                // For outer edge, we want the SMALLEST turn angle (most clockwise = most outward)
                // Treat angles near 360° as small angles (e.g., 350° -> 10°)
                let priority = normalizedAngle;


                // CRITICAL: At high-degree vertices, strongly prefer outer-facing segments
                const currentPointKey = `${currentPoint.x.toFixed(2)}_${currentPoint.y.toFixed(2)}`;
                const currentConnections = pointToSegments.get(currentPointKey) || [];
                if (currentConnections.length >= 4 && isOuterFacing) {
                    priority = -3; // Even higher priority than collinear for outer-facing at high-degree vertices
                    console.debug(`[winding] - HIGH-DEGREE VERTEX: Prioritizing outer-facing segment at ${currentConnections.length}-connection vertex`);
                }

                // Special case: If this is a straight continuation (collinear), give it highest priority
                // Only check if the turn angle is close to 0° (true straight continuation)
                // Note: 360° is NOT straight - it's a full circle back, which is a sharp inward turn
                if (incomingAngle !== null) {
                    const COLLINEAR_THRESHOLD = 0.1; // Close to 0° (< ~6 degrees)
                    const isNearZero = Math.abs(turnAngle) < COLLINEAR_THRESHOLD;

                    if (isNearZero) { // Only true straight continuation
                        priority = -2; // Higher priority than even sharp turns
                        console.debug(`[winding] - COLLINEAR: Straight continuation detected (turn angle: ${(turnAngle * 180 / Math.PI).toFixed(1)}°), highest priority`);
                    }
                }

                let bestPriority = bestAngle !== null ?
                    (bestAngle > Math.PI ? 2 * Math.PI - bestAngle : bestAngle) : Infinity;
                const COLLINEAR_THRESHOLD = 0.1; // ~6 degrees
                if (Math.abs(bestAngle) < COLLINEAR_THRESHOLD || Math.abs(bestAngle - 2 * Math.PI) < COLLINEAR_THRESHOLD) {
                    bestPriority = -1;
                }

                if (bestSegment === null || priority < bestPriority) {
                    bestSegment = segment;
                    bestAngle = turnAngle;
                    bestConn = conn;
                }
            }

            if (!bestSegment) {
                console.debug('[winding] No best segment found, stopping traversal');
                break;
            }

            // Move to next point
            console.debug(`[winding] - Selected segment ${bestSegment.id} with turn angle ${(bestAngle * 180 / Math.PI).toFixed(1)}°`);
            visitedSegments.add(bestSegment.id);
            const nextPoint = bestConn.isStart ? bestSegment.end : bestSegment.start;
            console.debug(`[winding] - Moving to next point: (${nextPoint.x.toFixed(1)}, ${nextPoint.y.toFixed(1)})`);

            // Update incoming angle for next iteration
            incomingAngle = Math.atan2(nextPoint.y - currentPoint.y, nextPoint.x - currentPoint.x);
            const nextKey = `${nextPoint.x.toFixed(2)}_${nextPoint.y.toFixed(2)}`;

            // Check if we've returned to start
            if (nextKey === startKey && outerEdgePoints.length > 2) {
                console.debug('[winding] Returned to start point, outer edge complete');
                break;
            }

            outerEdgePoints.push(nextPoint);
            incomingAngle = Math.atan2(nextPoint.y - currentPoint.y, nextPoint.x - currentPoint.x);
            currentPoint = nextPoint;
            currentKey = nextKey;
        }

        console.debug(`[winding] Traversal completed in ${iterations} iterations, found ${outerEdgePoints.length} outer edge points`);

        // VALIDATION: Basic polygon cleanup before returning
        const validatedPoints = this._basicPolygonCleanup(outerEdgePoints);
        console.debug(`[winding] Polygon validation: ${outerEdgePoints.length} → ${validatedPoints.length} points`);

        return validatedPoints;
    }

    // Minimal polygon cleanup - detect corrupted results and force failure
    _basicPolygonCleanup(points) {
        if (!points || points.length < 3) {
            return points;
        }

        // Step 1: Remove duplicate consecutive points
        const cleaned = [];
        const tolerance = 0.1;

        for (let i = 0; i < points.length; i++) {
            const current = points[i];
            const next = points[(i + 1) % points.length];

            // Only add if not duplicate of next point
            if (Math.abs(current.x - next.x) > tolerance || Math.abs(current.y - next.y) > tolerance) {
                cleaned.push(current);
            }
        }

        console.debug(`[winding] Removed ${points.length - cleaned.length} duplicate points`);

        // Step 2: Detect corrupted polygon structures
        if (cleaned.length === 6) {
            // For simple rectangular arrangements, 6 vertices often indicates corruption
            // Calculate polygon area vs bounding box area to detect issues
            const bounds = this._getPolygonBounds(cleaned);
            const boundingArea = (bounds.maxX - bounds.minX) * (bounds.maxY - bounds.minY);
            const polygonArea = this._calculatePolygonArea(cleaned);
            const areaRatio = polygonArea / boundingArea;

            if (areaRatio < 0.8) {
                console.warn(`[winding] Detected corrupted 6-vertex polygon (area ratio: ${areaRatio.toFixed(3)}), forcing failure to trigger convex hull`);
                return null; // Force failure to trigger convex hull fallback
            }
        }

        return cleaned;
    }

    // Calculate polygon area using shoelace formula
    _calculatePolygonArea(points) {
        let area = 0;
        for (let i = 0; i < points.length; i++) {
            const current = points[i];
            const next = points[(i + 1) % points.length];
            area += current.x * next.y - next.x * current.y;
        }
        return Math.abs(area) / 2;
    }

    // Get polygon bounding box
    _getPolygonBounds(points) {
        let minX = Infinity, maxX = -Infinity;
        let minY = Infinity, maxY = -Infinity;

        for (const point of points) {
            minX = Math.min(minX, point.x);
            maxX = Math.max(maxX, point.x);
            minY = Math.min(minY, point.y);
            maxY = Math.max(maxY, point.y);
        }

        return { minX, maxX, minY, maxY };
    }

    // Calculate exact inscribed rectangle for a 4-vertex parallelogram
    _calculateParallelogramRectangle(polygon, angle) {
        return window.SvgViewerAlgorithms.calculateParallelogramRectangle(polygon, angle);
    }

    // Calculate exact inscribed rectangle for a 4-vertex trapezoid
    _calculateTrapezoidRectangle(polygon, angle) {
        return window.SvgViewerAlgorithms.calculateTrapezoidRectangle(polygon, angle);
    }

    // Detect the dominant orientation angle of a polygon (for rectangles/parallelograms)
    _detectPolygonOrientation(polygon) {
        return window.SvgViewerAlgorithms.detectPolygonOrientation(polygon);
    }

    // Smart rectangular boundary detection using actual boundary points
    _detectAndCreateRectangularBoundaryFromPoints(pathBoundaryPoints) {
        console.debug(`[smart-rect] Testing ${pathBoundaryPoints.length} paths using actual boundary points`);

        // Only handle small numbers of paths that are likely rectangular
        if (pathBoundaryPoints.length < 2 || pathBoundaryPoints.length > 6) {
            console.debug(`[smart-rect] Rejected: too many/few paths (${pathBoundaryPoints.length})`);
            return null;
        }

        // Check if each path has exactly 4 points (rectangle vertices)
        for (let i = 0; i < pathBoundaryPoints.length; i++) {
            const points = pathBoundaryPoints[i];
            console.debug(`[smart-rect] Path ${i}: ${points.length} boundary points`);

            if (points.length !== 4) {
                console.debug(`[smart-rect] Rejected: Path ${i} has ${points.length} points, not 4`);
                return null;
            }

            console.debug(`[smart-rect] Path ${i} vertices: ${points.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);
        }

        // Check if rectangles share vertices (are adjacent)
        const sharedVertices = this._findSharedVertices(pathBoundaryPoints);
        console.debug(`[smart-rect] Found ${sharedVertices.length} shared vertices`);

        if (sharedVertices.length >= 1) {
            console.debug(`[smart-rect] Rectangles share vertices - creating combined boundary`);

            // For 2 rectangles sharing vertices, extract only the unique outer vertices
            // Shared vertices should be excluded to create a clean 4-vertex boundary
            const allPoints = pathBoundaryPoints.flat();
            console.debug(`[smart-rect] All ${allPoints.length} points before filtering: ${allPoints.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);

            // Create a map to count how many times each vertex appears
            const vertexCounts = new Map();
            for (const point of allPoints) {
                const key = `${point.x.toFixed(1)},${point.y.toFixed(1)}`;
                vertexCounts.set(key, (vertexCounts.get(key) || 0) + 1);
            }

            // Keep only vertices that appear exactly once (outer corners)
            const outerVertices = [];
            const seenKeys = new Set();
            for (const point of allPoints) {
                const key = `${point.x.toFixed(1)},${point.y.toFixed(1)}`;
                if (vertexCounts.get(key) === 1 && !seenKeys.has(key)) {
                    outerVertices.push(point);
                    seenKeys.add(key);
                }
            }

            console.debug(`[smart-rect] Filtered to ${outerVertices.length} outer vertices: ${outerVertices.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);

            // Remove near-duplicate vertices (tolerance 2px) that may have survived the initial filter
            const dedupedVertices = [];
            for (const v of outerVertices) {
                const isDuplicate = dedupedVertices.some(existing => {
                    const dist = Math.sqrt((existing.x - v.x) ** 2 + (existing.y - v.y) ** 2);
                    return dist < 2.0;
                });
                if (!isDuplicate) {
                    dedupedVertices.push(v);
                }
            }

            if (dedupedVertices.length !== outerVertices.length) {
                console.debug(`[smart-rect] Removed ${outerVertices.length - dedupedVertices.length} near-duplicate vertices, now have ${dedupedVertices.length}`);
            }

            const finalVertices = dedupedVertices;

            // For 4 outer vertices, sort them by angle around centroid to ensure proper ordering
            if (finalVertices.length === 4) {
                // Calculate centroid
                const cx = finalVertices.reduce((sum, p) => sum + p.x, 0) / finalVertices.length;
                const cy = finalVertices.reduce((sum, p) => sum + p.y, 0) / finalVertices.length;

                // Sort by angle from centroid
                finalVertices.sort((a, b) => {
                    const angleA = Math.atan2(a.y - cy, a.x - cx);
                    const angleB = Math.atan2(b.y - cy, b.x - cx);
                    return angleA - angleB;
                });

                console.debug(`[smart-rect] Sorted 4 vertices by angle around centroid (${cx.toFixed(1)}, ${cy.toFixed(1)})`);
                console.debug(`[smart-rect] Combined boundary has ${finalVertices.length} points: ${finalVertices.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);

                return finalVertices;
            }

            // For other cases, use convex hull
            const hull = this.simpleConvexHull(finalVertices);
            console.debug(`[smart-rect] Combined boundary has ${hull.length} points: ${hull.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);

            return hull;
        }

        console.debug(`[smart-rect] Rejected: Rectangles don't share vertices`);
        return null;
    }

    // Find vertices that are shared between rectangles
    _findSharedVertices(pathBoundaryPoints) {
        const sharedVertices = [];
        const tolerance = 1.0; // pixels

        for (let i = 0; i < pathBoundaryPoints.length; i++) {
            for (let j = i + 1; j < pathBoundaryPoints.length; j++) {
                const points1 = pathBoundaryPoints[i];
                const points2 = pathBoundaryPoints[j];

                for (const p1 of points1) {
                    for (const p2 of points2) {
                        const distance = Math.sqrt((p1.x - p2.x) ** 2 + (p1.y - p2.y) ** 2);
                        if (distance < tolerance) {
                            sharedVertices.push({ p1, p2, distance });
                            console.debug(`[smart-rect] Shared vertex: (${p1.x.toFixed(1)},${p1.y.toFixed(1)}) ≈ (${p2.x.toFixed(1)},${p2.y.toFixed(1)}) dist=${distance.toFixed(1)}`);
                        }
                    }
                }
            }
        }

        return sharedVertices;
    }

    // Smart rectangular boundary detection for simple rectangular arrangements
    _detectAndCreateRectangularBoundary(groupPaths) {
        console.debug(`[smart-rect] Testing ${groupPaths.length} paths for rectangular arrangement`);

        // Only handle small numbers of paths that are likely rectangular
        if (groupPaths.length < 2 || groupPaths.length > 6) {
            console.debug(`[smart-rect] Rejected: too many/few paths (${groupPaths.length})`);
            return null;
        }

        // Get the actual path boundaries (corners) for each path
        const pathCorners = [];
        for (let i = 0; i < groupPaths.length; i++) {
            const path = groupPaths[i];
            const corners = this._extractPathCorners(path);
            const bbox = path.getBBox();
            console.debug(`[smart-rect] Path ${i}: bbox(${bbox.x.toFixed(1)}, ${bbox.y.toFixed(1)}, ${bbox.width.toFixed(1)}×${bbox.height.toFixed(1)}), corners: ${corners ? corners.length : 'null'}`);

            if (!corners || corners.length !== 4) {
                console.debug(`[smart-rect] Rejected: Path ${i} not a simple rectangle`);
                return null;
            }
            pathCorners.push(corners);
        }

        // Check if all rectangles have the same orientation (axis-aligned OR consistently rotated)
        let commonRotation = null;
        for (let i = 0; i < pathCorners.length; i++) {
            const corners = pathCorners[i];
            const isAligned = this._isAxisAligned(corners);
            const rotation = this._getRectangleRotation(corners);

            console.debug(`[smart-rect] Path ${i} axis-aligned: ${isAligned}, rotation: ${rotation.toFixed(1)}°`);

            if (commonRotation === null) {
                commonRotation = rotation;
            } else if (Math.abs(commonRotation - rotation) > 5) {
                console.debug(`[smart-rect] Rejected: Path ${i} has different rotation (${rotation.toFixed(1)}° vs ${commonRotation.toFixed(1)}°)`);
                return null; // Mixed rotations
            }
        }

        // Combine all corners and find the actual boundary
        const allCorners = pathCorners.flat();

        // For axis-aligned rectangles, the boundary should be the union of rectangles
        // Find extreme points in each direction
        const minX = Math.min(...allCorners.map(p => p.x));
        const maxX = Math.max(...allCorners.map(p => p.x));
        const minY = Math.min(...allCorners.map(p => p.y));
        const maxY = Math.max(...allCorners.map(p => p.y));

        // For 2 side-by-side rectangles, we need to find the actual combined shape
        // Check if rectangles are adjacent (touching or overlapping)
        const areAdjacent = this._areRectanglesAdjacent(pathCorners);
        console.debug(`[smart-rect] Rectangles adjacent: ${areAdjacent}`);

        if (areAdjacent) {
            console.debug(`[smart-rect] Creating combined rectangular boundary`);
            // Create the actual boundary following the combined shape
            return this._createCombinedRectangularBoundary(pathCorners);
        }

        console.debug(`[smart-rect] Rejected: Not a simple adjacent rectangular arrangement`);
        return null; // Not a simple adjacent rectangular arrangement
    }

    // Extract the actual corners from path data (for both axis-aligned and rotated rectangles)
    _extractPathCorners(path) {
        try {
            // Get the path data string - handle different path object types
            let pathData = null;
            if (path.getAttribute) {
                pathData = path.getAttribute('d');
            } else if (path.d) {
                pathData = path.d;
            } else if (path.pathData) {
                pathData = path.pathData;
            }

            console.debug(`[smart-rect] Path object type: ${typeof path}, has getAttribute: ${!!path.getAttribute}, pathData: ${pathData ? pathData.substring(0, 50) + '...' : 'null'}`);

            if (!pathData) {
                console.debug(`[smart-rect] No path data found, using bbox fallback`);
                const bbox = path.getBBox();
                return [
                    { x: bbox.x, y: bbox.y },
                    { x: bbox.x + bbox.width, y: bbox.y },
                    { x: bbox.x + bbox.width, y: bbox.y + bbox.height },
                    { x: bbox.x, y: bbox.y + bbox.height }
                ];
            }

            // Parse the path to extract vertices
            const points = [];
            const commands = pathData.match(/[MLHVCSQTAZ][^MLHVCSQTAZ]*/gi);

            let currentX = 0, currentY = 0;
            let firstX = 0, firstY = 0;

            for (const command of commands) {
                const type = command[0].toUpperCase();
                const coords = command.slice(1).trim().split(/[\s,]+/).map(Number).filter(n => !isNaN(n));

                if (type === 'M') {
                    // Move to
                    currentX = coords[0];
                    currentY = coords[1];
                    firstX = currentX;
                    firstY = currentY;
                    points.push({ x: currentX, y: currentY });

                    // Handle additional coordinates as line-to commands
                    for (let i = 2; i < coords.length; i += 2) {
                        currentX = coords[i];
                        currentY = coords[i + 1];
                        points.push({ x: currentX, y: currentY });
                    }
                } else if (type === 'L') {
                    // Line to
                    for (let i = 0; i < coords.length; i += 2) {
                        currentX = coords[i];
                        currentY = coords[i + 1];
                        points.push({ x: currentX, y: currentY });
                    }
                } else if (type === 'Z') {
                    // Close path - don't add duplicate of first point
                    break;
                }
            }

            // Should have exactly 4 points for a rectangle
            if (points.length === 4) {
                return points;
            }

            // Fallback to bbox if we don't get 4 points
            console.debug(`[smart-rect] Path parsing got ${points.length} points, using bbox fallback`);
            const bbox = path.getBBox();
            return [
                { x: bbox.x, y: bbox.y },
                { x: bbox.x + bbox.width, y: bbox.y },
                { x: bbox.x + bbox.width, y: bbox.y + bbox.height },
                { x: bbox.x, y: bbox.y + bbox.height }
            ];

        } catch (error) {
            console.debug(`[smart-rect] Error parsing path corners: ${error.message}`);
            // Fallback to bbox
            const bbox = path.getBBox();
            return [
                { x: bbox.x, y: bbox.y },
                { x: bbox.x + bbox.width, y: bbox.y },
                { x: bbox.x + bbox.width, y: bbox.y + bbox.height },
                { x: bbox.x, y: bbox.y + bbox.height }
            ];
        }
    }

    // Check if corners represent an axis-aligned rectangle
    _isAxisAligned(corners) {
        // Simple check: all x-coordinates should be one of two values
        // and all y-coordinates should be one of two values
        const xValues = [...new Set(corners.map(p => Math.round(p.x)))];
        const yValues = [...new Set(corners.map(p => Math.round(p.y)))];
        return xValues.length === 2 && yValues.length === 2;
    }

    // Get the rotation angle of a rectangle from its corners
    _getRectangleRotation(corners) {
        // Calculate the angle of the first edge (from corner 0 to corner 1)
        const dx = corners[1].x - corners[0].x;
        const dy = corners[1].y - corners[0].y;
        let angle = Math.atan2(dy, dx) * 180 / Math.PI;

        // Normalize to 0-360 degrees
        if (angle < 0) angle += 360;

        // For rectangles, we care about orientation, so normalize to 0-90 degrees
        // (since a rectangle rotated 90° is the same as rotated 0°)
        angle = angle % 90;

        return angle;
    }

    // Check if rectangles are adjacent (side-by-side or stacked)
    _areRectanglesAdjacent(pathCorners) {
        if (pathCorners.length !== 2) return false; // For now, only handle 2 rectangles

        const rect1 = this._getBounds(pathCorners[0]);
        const rect2 = this._getBounds(pathCorners[1]);

        const tolerance = 5; // pixels

        // Check if they're side-by-side (sharing a vertical edge)
        const verticallyAdjacent = (
            Math.abs(rect1.maxX - rect2.minX) < tolerance ||
            Math.abs(rect2.maxX - rect1.minX) < tolerance
        ) && (
            !(rect1.maxY < rect2.minY - tolerance || rect2.maxY < rect1.minY - tolerance) // overlapping in Y
        );

        // Check if they're stacked (sharing a horizontal edge)
        const horizontallyAdjacent = (
            Math.abs(rect1.maxY - rect2.minY) < tolerance ||
            Math.abs(rect2.maxY - rect1.minY) < tolerance
        ) && (
            !(rect1.maxX < rect2.minX - tolerance || rect2.maxX < rect1.minX - tolerance) // overlapping in X
        );

        return verticallyAdjacent || horizontallyAdjacent;
    }

    _getBounds(corners) {
        return {
            minX: Math.min(...corners.map(p => p.x)),
            maxX: Math.max(...corners.map(p => p.x)),
            minY: Math.min(...corners.map(p => p.y)),
            maxY: Math.max(...corners.map(p => p.y))
        };
    }

    // Create the actual combined boundary for adjacent rectangles
    _createCombinedRectangularBoundary(pathCorners) {
        if (pathCorners.length === 2) {
            console.debug(`[smart-rect] Creating boundary for 2 rectangles`);

            // For rotated rectangles, we need to find the actual combined outline
            // Use convex hull of all corners, which should give us the correct boundary
            const allCorners = pathCorners.flat();
            console.debug(`[smart-rect] All corners: ${allCorners.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);

            // Use convex hull to find the outer boundary
            const hull = this.simpleConvexHull(allCorners);
            console.debug(`[smart-rect] Convex hull: ${hull.length} points: ${hull.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);

            return hull;
        }

        return null;
    }

    // Check if two rectangles form a simple rectangular union
    _rectanglesFormSimpleUnion(rect1, rect2) {
        const tolerance = 5;

        // Check if they align on top/bottom edges (side-by-side with same height range)
        const sameHeight = Math.abs(rect1.minY - rect2.minY) < tolerance &&
                          Math.abs(rect1.maxY - rect2.maxY) < tolerance;

        // Check if they align on left/right edges (stacked with same width range)
        const sameWidth = Math.abs(rect1.minX - rect2.minX) < tolerance &&
                         Math.abs(rect1.maxX - rect2.maxX) < tolerance;

        return sameHeight || sameWidth;
    }

    // Create boundary for complex rectangular arrangements (like L-shapes)
    _createComplexRectangularBoundary(rect1, rect2) {
        // This would create the actual traced boundary for L-shaped arrangements
        // For now, fallback to simple union
        const minX = Math.min(rect1.minX, rect2.minX);
        const maxX = Math.max(rect1.maxX, rect2.maxX);
        const minY = Math.min(rect1.minY, rect2.minY);
        const maxY = Math.max(rect1.maxY, rect2.maxY);

        return [
            { x: minX, y: minY },
            { x: maxX, y: minY },
            { x: maxX, y: maxY },
            { x: minX, y: maxY }
        ];
    }

    // Combine original SVG path data from multiple paths
    _combineOriginalPathData(groupPaths) {
        try {
            console.debug('[overlap] Combining original SVG path data from paths');

            let combinedData = '';

            for (let i = 0; i < groupPaths.length; i++) {
                const path = groupPaths[i];
                const pathData = path.attr('d');

                if (!pathData) {
                    console.warn(`[overlap] Path ${i} has no 'd' attribute`);
                    continue;
                }

                console.debug(`[overlap] Path ${i} data: ${pathData.substring(0, 50)}...`);

                // Add path data to combined string
                if (combinedData.length > 0) {
                    // Ensure proper spacing between path data
                    combinedData += ' ';
                }
                combinedData += pathData;
            }

            if (combinedData.length === 0) {
                console.warn('[overlap] No valid path data found');
                return null;
            }

            console.debug(`[overlap] Combined path data length: ${combinedData.length} characters`);
            return combinedData;

        } catch (error) {
            console.warn('[overlap] Error combining path data:', error);
            return null;
        }
    }


    // Clean up any existing debug unified paths and inscribed rectangles
    _cleanupDebugPaths(scope) {
        const debugCleanup = false; // Set to true to debug cleanup operations
        if (debugCleanup) console.debug('[cleanup] Starting debug path cleanup');

        // Remove unified paths by class
        const classPaths = scope.selectAll(".debug-unified-path");
        if (debugCleanup) console.debug(`[cleanup] Found ${classPaths.length} paths with debug-unified-path class`);
        classPaths.remove();

        // Remove inscribed rectangles by class
        const classRects = scope.selectAll(".debug-inscribed-rectangle");
        if (debugCleanup) console.debug(`[cleanup] Found ${classRects.length} rectangles with debug-inscribed-rectangle class`);
        classRects.remove();

        // Also remove any leftover debug paths by color attributes
        const allPaths = scope.selectAll("path");
        if (debugCleanup) console.debug(`[cleanup] Checking ${allPaths.length} total paths for magenta colors`);
        let removedCount = 0;
        allPaths.forEach(path => {
            const fill = path.attr("fill");
            if (fill && (fill.includes("255, 0, 255") || fill.includes("magenta"))) {
                if (debugCleanup) console.debug(`[cleanup] Removing path with fill: ${fill}`);
                path.remove();
                removedCount++;
            }
        });
        if (debugCleanup) console.debug(`[cleanup] Removed ${removedCount} paths by color attributes`);
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

            // Create unified path by modifying paths to overlap and then merging
            let unifiedBoundary = this._createOverlappingPathMerge(groupPaths, allBoundaryPoints);
            let hull = null;

            if (!unifiedBoundary || unifiedBoundary.length < 3) {
                console.warn('[outline] Overlapping path merge failed, trying smart rectangular detection');

                // Extract the actual 4-vertex boundary points for each path
                // Use the same linearization that the winding algorithm uses
                const lineSegmentPaths = this._convertPathsToLineSegments(groupPaths);
                console.debug(`[smart-rect] Converted ${lineSegmentPaths.length} paths to line segments`);

                const pathBoundaryPoints = [];
                for (const lineSegPath of lineSegmentPaths) {
                    const segments = lineSegPath.segments;
                    if (segments.length === 0) continue;

                    // Extract unique vertices from segments
                    const vertices = [];
                    vertices.push(segments[0].start);
                    for (const seg of segments) {
                        vertices.push(seg.end);
                    }

                    // Remove duplicate last vertex (closes the path)
                    if (vertices.length > 1) {
                        const first = vertices[0];
                        const last = vertices[vertices.length - 1];
                        if (Math.abs(first.x - last.x) < 0.1 && Math.abs(first.y - last.y) < 0.1) {
                            vertices.pop();
                        }
                    }

                    pathBoundaryPoints.push(vertices);
                    console.debug(`[smart-rect] Path ${lineSegPath.pathIdx}: ${vertices.length} vertices: ${vertices.map(p => `(${p.x.toFixed(1)},${p.y.toFixed(1)})`).join(', ')}`);
                }

                if (pathBoundaryPoints.length === 0) {
                    console.debug(`[smart-rect] No vertices extracted, falling back to convex hull`);
                    hull = this.simpleConvexHull(allBoundaryPoints);
                }

                // Try to detect simple rectangular arrangements and create proper boundaries
                if (!hull && pathBoundaryPoints.length > 0) {
                    const rectangularBoundary = this._detectAndCreateRectangularBoundaryFromPoints(pathBoundaryPoints);
                    if (rectangularBoundary) {
                        console.debug('[outline] Using smart rectangular boundary detection');
                        unifiedBoundary = rectangularBoundary;
                    } else {
                        console.warn('[outline] Smart rectangular detection failed, falling back to convex hull');
                        hull = this.simpleConvexHull(allBoundaryPoints);
                    }
                } else if (!hull) {
                    console.warn('[outline] Path data extraction failed, falling back to convex hull');
                    hull = this.simpleConvexHull(allBoundaryPoints);
                }
            }

            // Create SVG path data from the result
            let pathData;
            if (hull) {
                pathData = `M ${hull[0].x} ${hull[0].y}`;
                for (let i = 1; i < hull.length; i++) {
                    pathData += ` L ${hull[i].x} ${hull[i].y}`;
                }
                pathData += ' Z';
                console.debug(`[outline] Fallback: Created convex hull unified path from ${hull.length} hull points`);
            } else {
                // Convert merged boundary to SVG path data
                pathData = `M ${unifiedBoundary[0].x} ${unifiedBoundary[0].y}`;
                for (let i = 1; i < unifiedBoundary.length; i++) {
                    pathData += ` L ${unifiedBoundary[i].x} ${unifiedBoundary[i].y}`;
                }
                pathData += ' Z';
                console.debug(`[outline] Created overlapping merge unified path from ${unifiedBoundary.length} boundary points`);
            }

            // Find largest inscribed rectangle
            const polygon = unifiedBoundary || hull;
            let largestRect = null;
            if (polygon && polygon.length >= 3) {
                // Detect the actual orientation of the polygon for better angle estimation
                const orientationAngle = this._detectPolygonOrientation(polygon);
                console.debug(`[outline] Detected polygon orientation: ${orientationAngle.toFixed(1)}°`);

                // Special case: For 4-vertex polygons (parallelograms), calculate exact dimensions
                if (polygon.length === 4) {
                    console.warn(`🔍 [EXACT-RECT-DEBUG] About to call _calculateParallelogramRectangle with orientationAngle=${orientationAngle.toFixed(1)}°`);
                    const exactRect = this._calculateParallelogramRectangle(polygon, orientationAngle);
                    console.warn(`🔍 [EXACT-RECT-DEBUG] _calculateParallelogramRectangle returned:`, exactRect);
                    if (exactRect) {
                        console.warn(`🔍 [EXACT-RECT-DEBUG] exactRect is truthy, should use it!`);
                        console.debug(`[outline] Using exact parallelogram rectangle: ${exactRect.width.toFixed(1)}×${exactRect.height.toFixed(1)} at ${exactRect.angle}°`);
                        largestRect = exactRect;
                    } else {
                        console.warn(`🔍 [EXACT-RECT-DEBUG] exactRect is null/falsy, trying trapezoid calculation`);

                        // Try trapezoid calculation
                        const trapezoidRect = this._calculateTrapezoidRectangle(polygon, orientationAngle);
                        if (trapezoidRect) {
                            console.warn(`🔍 [TRAPEZOID-DEBUG] Trapezoid calculation succeeded!`);
                            console.debug(`[outline] Using trapezoid rectangle: ${trapezoidRect.width.toFixed(1)}×${trapezoidRect.height.toFixed(1)} at ${trapezoidRect.angle}°`);
                            largestRect = trapezoidRect;
                        } else {
                            console.warn(`🔍 [TRAPEZOID-DEBUG] Trapezoid calculation also failed, falling back to optimized algorithm`);
                        }
                    }
                }

                // Fallback to general inscribed rectangle algorithm if exact calculation failed
                if (!largestRect) {
                    largestRect = this.findLargestInscribedRectangle(polygon, {
                        gridSize: 20,
                        minArea: 100,
                        debugLog: debugVisible,
                        hintAngle: orientationAngle
                    });
                }
            }

            // Create the merged path element
            const unifiedPath = scope.path(pathData);

            if (debugVisible) {
                // Debug mode: Make it visible with distinctive styling
                unifiedPath.attr({
                    fill: 'rgba(255, 0, 255, 0.3)',     // Semi-transparent magenta fill
                    stroke: '#FF00FF',                   // Magenta border
                    strokeWidth: 1,
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

                // Visualize the largest inscribed rectangle if found
                if (largestRect) {
                    let rectPath;
                    
                    if (largestRect.corners) {
                        // Rotated rectangle - create a polygon from corners
                        const corners = largestRect.corners;
                        const pathData = `M ${corners[0].x} ${corners[0].y} ` +
                                       `L ${corners[1].x} ${corners[1].y} ` +
                                       `L ${corners[2].x} ${corners[2].y} ` +
                                       `L ${corners[3].x} ${corners[3].y} Z`;
                        rectPath = scope.path(pathData);
                        console.debug(`[outline] Debug mode: Visualized ROTATED inscribed rectangle (${largestRect.width.toFixed(1)}x${largestRect.height.toFixed(1)}) at ${largestRect.angle}°`);
                        console.debug(`[outline] Rectangle path data:`, pathData);
                    } else {
                        // Axis-aligned rectangle (fallback for old format)
                        rectPath = scope.rect(largestRect.x, largestRect.y, largestRect.width, largestRect.height);
                        console.debug(`[outline] Debug mode: Visualized AXIS-ALIGNED inscribed rectangle (${largestRect.width.toFixed(1)}x${largestRect.height.toFixed(1)})`);
                    }
                    
                    rectPath.attr({
                        fill: 'rgba(255, 165, 0, 0.2)',    // Semi-transparent orange fill
                        stroke: '#FF8800',                  // Orange border
                        strokeWidth: 3,
                        'fill-opacity': 0.2,
                        'stroke-opacity': 1.0,
                        'pointer-events': 'none',
                        'stroke-dasharray': '5,5'          // Dashed border
                    });

                    // Also set style attribute directly to ensure it's not overridden
                    rectPath.node.setAttribute('style',
                        'stroke: #FF8800 !important; ' +
                        'stroke-width: 3px !important; ' +
                        'stroke-dasharray: 5,5 !important; ' +
                        'fill: rgba(255, 165, 0, 0.2) !important; ' +
                        'pointer-events: none !important;'
                    );

                    rectPath.addClass("debug-inscribed-rectangle");
                    parentScope.appendChild(rectPath.node);
                }
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

        // Remove existing outlines (selection + groups + debug paths including rectangles)
        scope.selectAll(".group-outline").remove();
        this._cleanupDebugPaths(scope);

        // 1) Live selection perimeter (even when not grouped)
        this.getPaths();
        const selectedIds = Array.from(this.selectedIds || []);
        if (selectedIds.length > 0) {
            // Always generate the outline path data to create the unified path (purple)
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

            // Only show the orange outline if showOutlines is enabled
            if (selectionPathData && this.showOutlines) {
                const selectionOutline = scope.path(selectionPathData);
                selectionOutline.attr({
                    stroke: '#FFA500',              // orange for live selection
                    strokeWidth: 1,
                    fill: 'none',
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

            if (this.showBoundingBox) {
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
            }

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

export function setShowOutlines(containerId, show) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    instance.showOutlines = show;
    return true;
}

export function setShowBoundingBox(containerId, show) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    instance.showBoundingBox = show;
    return true;
}

export function disposeInstance(containerId) {
    instances.delete(containerId);
}
