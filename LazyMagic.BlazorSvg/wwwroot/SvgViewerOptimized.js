// Optimized SVG processing functions for SvgViewer
// Uses spatial hashing and early exit strategies for better performance

// Spatial hash for fast point lookups
class SpatialHash {
    constructor(cellSize = 10) {
        this.cellSize = cellSize;
        this.buckets = new Map();
    }

    _getKey(x, y) {
        const gx = Math.floor(x / this.cellSize);
        const gy = Math.floor(y / this.cellSize);
        return `${gx},${gy}`;
    }

    add(point, data) {
        const key = this._getKey(point.x, point.y);
        if (!this.buckets.has(key)) {
            this.buckets.set(key, []);
        }
        this.buckets.get(key).push({ point, data });
    }

    findNear(point, radius) {
        const results = [];
        const radiusSq = radius * radius;

        // Check cells within radius
        const cellRadius = Math.ceil(radius / this.cellSize);
        const cx = Math.floor(point.x / this.cellSize);
        const cy = Math.floor(point.y / this.cellSize);

        for (let dx = -cellRadius; dx <= cellRadius; dx++) {
            for (let dy = -cellRadius; dy <= cellRadius; dy++) {
                const key = `${cx + dx},${cy + dy}`;
                const bucket = this.buckets.get(key);
                if (bucket) {
                    for (const item of bucket) {
                        const distSq = Math.pow(item.point.x - point.x, 2) +
                                       Math.pow(item.point.y - point.y, 2);
                        if (distSq <= radiusSq) {
                            results.push({ ...item, distance: Math.sqrt(distSq) });
                        }
                    }
                }
            }
        }

        return results.sort((a, b) => a.distance - b.distance);
    }
}

// Fast winding algorithm using spatial hashing
function fastWindingAlgorithm(segments, tolerance = 5) {
    const startTime = performance.now();

    // Build spatial hash of segment endpoints
    const spatialHash = new SpatialHash(tolerance * 2);
    const pointToSegments = new Map();

    for (const seg of segments) {
        const startKey = `${seg.start.x.toFixed(1)},${seg.start.y.toFixed(1)}`;
        const endKey = `${seg.end.x.toFixed(1)},${seg.end.y.toFixed(1)}`;

        // Add to spatial hash
        spatialHash.add(seg.start, { segment: seg, isStart: true });
        spatialHash.add(seg.end, { segment: seg, isStart: false });

        // Track segments at each point
        if (!pointToSegments.has(startKey)) {
            pointToSegments.set(startKey, []);
        }
        if (!pointToSegments.has(endKey)) {
            pointToSegments.set(endKey, []);
        }
        pointToSegments.get(startKey).push(seg);
        pointToSegments.get(endKey).push(seg);
    }

    // Find connected segments using spatial hash
    const connections = new Map();
    const visited = new Set();

    for (const seg of segments) {
        const segId = seg.id;
        if (visited.has(segId)) continue;

        // Find connections at both endpoints
        const startNear = spatialHash.findNear(seg.start, tolerance);
        const endNear = spatialHash.findNear(seg.end, tolerance);

        for (const near of [...startNear, ...endNear]) {
            if (near.data.segment.id !== segId && near.distance < tolerance) {
                if (!connections.has(segId)) {
                    connections.set(segId, new Set());
                }
                connections.get(segId).add(near.data.segment.id);
            }
        }

        visited.add(segId);
    }

    // Build outer boundary using rightmost turn strategy
    const boundary = [];
    const usedSegments = new Set();

    // Find leftmost point
    let leftmost = null;
    let leftmostSeg = null;
    for (const seg of segments) {
        if (!leftmost || seg.start.x < leftmost.x ||
            (seg.start.x === leftmost.x && seg.start.y > leftmost.y)) {
            leftmost = seg.start;
            leftmostSeg = seg;
        }
        if (!leftmost || seg.end.x < leftmost.x ||
            (seg.end.x === leftmost.x && seg.end.y > leftmost.y)) {
            leftmost = seg.end;
            leftmostSeg = seg;
        }
    }

    // Traverse boundary
    let current = leftmost;
    let currentSeg = leftmostSeg;
    const maxIterations = segments.length * 2;
    let iterations = 0;

    while (iterations++ < maxIterations) {
        boundary.push(current);
        usedSegments.add(currentSeg.id);

        // Get next point
        const next = currentSeg.start.x === current.x && currentSeg.start.y === current.y
            ? currentSeg.end : currentSeg.start;

        // Find connected segments at next point
        const nextKey = `${next.x.toFixed(1)},${next.y.toFixed(1)}`;
        const nextSegments = pointToSegments.get(nextKey) || [];

        // Choose segment with rightmost turn (excluding used segments)
        let bestSeg = null;
        let bestAngle = -Infinity;

        for (const seg of nextSegments) {
            if (usedSegments.has(seg.id)) continue;

            const segNext = seg.start.x === next.x && seg.start.y === next.y
                ? seg.end : seg.start;

            const angle = Math.atan2(segNext.y - next.y, segNext.x - next.x);
            if (angle > bestAngle) {
                bestAngle = angle;
                bestSeg = seg;
            }
        }

        if (!bestSeg) break;

        current = next;
        currentSeg = bestSeg;

        // Check if we've returned to start
        if (Math.abs(current.x - leftmost.x) < tolerance &&
            Math.abs(current.y - leftmost.y) < tolerance &&
            boundary.length > 2) {
            break;
        }
    }

    const elapsed = performance.now() - startTime;
    console.log(`[fast-winding] Completed in ${elapsed.toFixed(1)}ms with ${boundary.length} points`);

    return boundary;
}

// ENHANCED rectangle validation with dense edge sampling for complex polygons
function fastRectangleValidation(corners, polygon, samples = 32) {
    // CRITICAL: Use much higher sampling density to prevent boundary violations

    // Test all 4 corners
    for (const corner of corners) {
        if (!isPointInPolygonSlow(corner, polygon)) {
            return false;
        }
    }

    // ENHANCED: Test many more points along each edge for complex polygons
    const pointsPerEdge = Math.max(8, Math.floor(samples / 4)); // Minimum 8 points per edge
    for (let edgeIdx = 0; edgeIdx < 4; edgeIdx++) {
        const start = corners[edgeIdx];
        const end = corners[(edgeIdx + 1) % 4];

        // Test points including endpoints to catch edge cases
        for (let i = 0; i <= pointsPerEdge; i++) {
            const t = i / pointsPerEdge;
            const testPoint = {
                x: start.x + t * (end.x - start.x),
                y: start.y + t * (end.y - start.y)
            };

            if (!isPointInPolygonSlow(testPoint, polygon)) {
                return false;
            }
        }
    }

    // Test center point
    const centerX = (corners[0].x + corners[1].x + corners[2].x + corners[3].x) / 4;
    const centerY = (corners[0].y + corners[1].y + corners[2].y + corners[3].y) / 4;

    if (!isPointInPolygonSlow({x: centerX, y: centerY}, polygon)) {
        return false;
    }

    // ENHANCED: Test additional interior points for better coverage
    const interiorSamples = 4;
    for (let i = 1; i <= interiorSamples; i++) {
        for (let j = 1; j <= interiorSamples; j++) {
            const u = i / (interiorSamples + 1);
            const v = j / (interiorSamples + 1);

            // Bilinear interpolation within rectangle
            const x = (1 - u) * (1 - v) * corners[0].x + u * (1 - v) * corners[1].x +
                      u * v * corners[2].x + (1 - u) * v * corners[3].x;
            const y = (1 - u) * (1 - v) * corners[0].y + u * (1 - v) * corners[1].y +
                      u * v * corners[2].y + (1 - u) * v * corners[3].y;

            if (!isPointInPolygonSlow({x, y}, polygon)) {
                return false;
            }
        }
    }

    return true;
}

// EXACT copy of the working point-in-polygon from slow algorithm
function isPointInPolygonSlow(point, polygon) {
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

// Line segment intersection test
function lineSegmentsIntersect(p1, p2, p3, p4) {
    const x1 = p1.x, y1 = p1.y;
    const x2 = p2.x, y2 = p2.y;
    const x3 = p3.x, y3 = p3.y;
    const x4 = p4.x, y4 = p4.y;

    const denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
    if (Math.abs(denom) < 1e-10) return false; // Parallel lines

    const t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
    const u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

    return t >= 0 && t <= 1 && u >= 0 && u <= 1;
}

// Correct point-in-polygon test using ray-casting algorithm
function isPointInPolygonFast(point, polygon) {
    let inside = false;
    const x = point.x;
    const y = point.y;

    for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
        const xi = polygon[i].x, yi = polygon[i].y;
        const xj = polygon[j].x, yj = polygon[j].y;

        // Check if ray from point intersects with edge
        if (((yi > y) !== (yj > y)) &&
            (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) {
            inside = !inside;
        }
    }

    return inside;
}

// More robust point-in-polygon test that handles edge cases
function isPointInPolygonRobust(point, polygon) {
    const x = point.x;
    const y = point.y;
    let inside = false;

    for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
        const xi = polygon[i].x, yi = polygon[i].y;
        const xj = polygon[j].x, yj = polygon[j].y;

        // Handle horizontal edges and edge cases
        if (yi === yj) continue; // Skip horizontal edges

        // Check if point is exactly on a vertex
        if ((xi === x && yi === y) || (xj === x && yj === y)) {
            return true; // Point on vertex is inside
        }

        // Check if ray crosses this edge
        if ((yi <= y && y < yj) || (yj <= y && y < yi)) {
            // Calculate intersection x-coordinate
            const intersectX = xi + (y - yi) * (xj - xi) / (yj - yi);

            // Check if point is exactly on the edge
            if (Math.abs(intersectX - x) < 1e-10) {
                return true; // Point on edge is inside
            }

            // Count intersection if ray crosses to the right
            if (intersectX > x) {
                inside = !inside;
            }
        }
    }

    return inside;
}

// Test if polygon is convex using cross product method
function isPolygonConvex(polygon) {
    if (polygon.length < 3) return false;

    let sign = 0;
    const n = polygon.length;

    for (let i = 0; i < n; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % n];
        const p3 = polygon[(i + 2) % n];

        // Calculate cross product
        const cross = (p2.x - p1.x) * (p3.y - p2.y) - (p2.y - p1.y) * (p3.x - p2.x);

        if (Math.abs(cross) > 1e-10) { // Ignore near-zero cross products
            const currentSign = cross > 0 ? 1 : -1;
            if (sign === 0) {
                sign = currentSign;
            } else if (sign !== currentSign) {
                return false; // Found a turn in the opposite direction
            }
        }
    }

    return true;
}

// Calculate true geometric centroid using shoelace formula
function getPolygonAreaCentroid(polygon, minX, maxX, minY, maxY) {
    let area = 0;
    let cx = 0;
    let cy = 0;

    // Shoelace formula for area and centroid
    for (let i = 0; i < polygon.length; i++) {
        const j = (i + 1) % polygon.length;
        const cross = polygon[i].x * polygon[j].y - polygon[j].x * polygon[i].y;
        area += cross;
        cx += (polygon[i].x + polygon[j].x) * cross;
        cy += (polygon[i].y + polygon[j].y) * cross;
    }

    area = Math.abs(area) / 2;

    if (area < 1e-10) {
        // Degenerate polygon, use bounding box center
        return {
            x: (minX + maxX) / 2,
            y: (minY + maxY) / 2
        };
    }

    cx = cx / (6 * (area * Math.sign(area)));
    cy = cy / (6 * (area * Math.sign(area)));

    // Fallback to bounding box center if centroid is outside reasonable bounds
    if (cx < minX || cx > maxX || cy < minY || cy > maxY) {
        console.debug('[fast-rectangle] Area centroid outside bounds, using bounding box center');
        return {
            x: (minX + maxX) / 2,
            y: (minY + maxY) / 2
        };
    }

    return { x: cx, y: cy };
}

// Get centroids using systematic grid search to find global maximum
function getMultipleCentroids(polygon, minX, maxX, minY, maxY, options = {}) {
    const { debugMode = false } = options;
    const centroids = [];

    const width = maxX - minX;
    const height = maxY - minY;

    // OPTIMIZED: Use strategic multi-resolution grid search
    // Start with coarser grid but add high-density samples around promising areas
    const gridResolution = 25; // 25x25 = 625 potential centroids (much faster)
    const stepX = width / (gridResolution + 1);
    const stepY = height / (gridResolution + 1);

    if (debugMode) console.debug(`[multi-centroid] Performing ${gridResolution}x${gridResolution} systematic grid search`);

    // Sample every grid point inside the polygon
    for (let i = 1; i <= gridResolution; i++) {
        for (let j = 1; j <= gridResolution; j++) {
            const candidate = {
                x: minX + i * stepX,
                y: minY + j * stepY
            };

            if (isPointInPolygonSlow(candidate, polygon)) {
                centroids.push(candidate);
            }
        }
    }

    // Always include standard centroids as additional candidates
    const standardCentroids = [];

    // Vertex centroid
    let vertexCentroidX = 0, vertexCentroidY = 0;
    for (const vertex of polygon) {
        vertexCentroidX += vertex.x;
        vertexCentroidY += vertex.y;
    }
    const vertexCentroid = {
        x: vertexCentroidX / polygon.length,
        y: vertexCentroidY / polygon.length
    };
    if (isPointInPolygonSlow(vertexCentroid, polygon)) {
        standardCentroids.push(vertexCentroid);
    }

    // Area-weighted centroid
    const areaCentroid = getPolygonAreaCentroid(polygon, minX, maxX, minY, maxY);
    if (isPointInPolygonSlow(areaCentroid, polygon)) {
        standardCentroids.push(areaCentroid);
    }

    // Bounding box center
    const bboxCenter = { x: (minX + maxX) / 2, y: (minY + maxY) / 2 };
    if (isPointInPolygonSlow(bboxCenter, polygon)) {
        standardCentroids.push(bboxCenter);
    }

    // ENHANCED: Strategic centroid patterns optimized for different shape types
    const strategicPatterns = [
        { x: 0.50, y: 0.48, name: "optimal pattern" },
        { x: 0.33, y: 0.33, name: "lower-left focus" },
        { x: 0.67, y: 0.33, name: "lower-right focus" },
        { x: 0.33, y: 0.67, name: "upper-left focus" },
        { x: 0.67, y: 0.67, name: "upper-right focus" },
        { x: 0.40, y: 0.60, name: "off-center pattern" },
        { x: 0.60, y: 0.40, name: "diagonal pattern" },
        // Additional patterns for parallelogram shapes
        { x: 0.45, y: 0.50, name: "left-center bias" },
        { x: 0.55, y: 0.50, name: "right-center bias" },
        { x: 0.50, y: 0.35, name: "lower-center bias" },
        { x: 0.50, y: 0.65, name: "upper-center bias" },
        { x: 0.35, y: 0.45, name: "lower-left offset" },
        { x: 0.65, y: 0.55, name: "upper-right offset" }
    ];

    for (const pattern of strategicPatterns) {
        const candidate = {
            x: minX + width * pattern.x,
            y: minY + height * pattern.y
        };
        if (isPointInPolygonSlow(candidate, polygon)) {
            standardCentroids.push(candidate);
            if (debugMode) console.debug(`[multi-centroid] Added ${pattern.name} centroid (${(pattern.x*100).toFixed(0)}%, ${(pattern.y*100).toFixed(0)}%) at (${candidate.x.toFixed(1)}, ${candidate.y.toFixed(1)})`);
        }
    }

    // Add standard centroids to the front of the list
    centroids.unshift(...standardCentroids);

    // Remove duplicates (standard centroids might overlap with grid points)
    const uniqueCentroids = [];
    const tolerance = Math.min(stepX, stepY) / 2;

    for (const centroid of centroids) {
        const isDuplicate = uniqueCentroids.some(existing =>
            Math.abs(existing.x - centroid.x) < tolerance &&
            Math.abs(existing.y - centroid.y) < tolerance
        );
        if (!isDuplicate) {
            uniqueCentroids.push(centroid);
        }
    }

    if (debugMode) console.debug(`[multi-centroid] Generated ${uniqueCentroids.length} unique centroids from systematic search`);
    return uniqueCentroids;
}

// Find the best centroid for concave polygons using improved sampling
function findBestCentroid(polygon, minX, maxX, minY, maxY, options = {}) {
    const { debugMode = false } = options;

    // First try the area-weighted centroid
    const areaCentroid = getPolygonAreaCentroid(polygon, minX, maxX, minY, maxY);

    // Test if area centroid is inside the polygon
    if (isPointInPolygonSlow(areaCentroid, polygon)) {
        if (debugMode) console.debug('[adaptive-centroid] Area centroid is inside polygon, using it');
        return areaCentroid;
    }

    if (debugMode) console.debug('[adaptive-centroid] Area centroid outside polygon, trying improved sampling');

    const candidates = [];

    // Strategy 1: Multi-scale grid sampling to ensure we don't miss good centroids
    const scales = [0.8, 0.6, 0.4]; // Sample at different scales from bounding box

    for (const scale of scales) {
        const margin = (1 - scale) / 2;
        const scaledMinX = minX + (maxX - minX) * margin;
        const scaledMaxX = maxX - (maxX - minX) * margin;
        const scaledMinY = minY + (maxY - minY) * margin;
        const scaledMaxY = maxY - (maxY - minY) * margin;

        if (scaledMaxX > scaledMinX && scaledMaxY > scaledMinY) {
            const gridSize = scale === 0.8 ? 5 : 3; // Denser grid for larger scale
            const stepX = (scaledMaxX - scaledMinX) / (gridSize + 1);
            const stepY = (scaledMaxY - scaledMinY) / (gridSize + 1);

            for (let i = 1; i <= gridSize; i++) {
                for (let j = 1; j <= gridSize; j++) {
                    const candidate = {
                        x: scaledMinX + i * stepX,
                        y: scaledMinY + j * stepY
                    };

                    if (isPointInPolygonSlow(candidate, polygon)) {
                        candidates.push(candidate);
                    }
                }
            }
        }
    }

    // Strategy 2: Add high-density samples along cross patterns
    const midX = (minX + maxX) / 2;
    const midY = (minY + maxY) / 2;

    // Dense horizontal line through center
    for (let i = 1; i <= 8; i++) {
        const x = minX + i * (maxX - minX) / 9;
        const candidate = { x, y: midY };
        if (isPointInPolygonSlow(candidate, polygon)) {
            candidates.push(candidate);
        }
    }

    // Dense vertical line through center
    for (let i = 1; i <= 8; i++) {
        const y = minY + i * (maxY - minY) / 9;
        const candidate = { x: midX, y };
        if (isPointInPolygonSlow(candidate, polygon)) {
            candidates.push(candidate);
        }
    }

    // Strategy 3: Add diagonal samples
    for (let i = 1; i <= 6; i++) {
        const t = i / 7;
        // Main diagonal
        const diag1 = {
            x: minX + t * (maxX - minX),
            y: minY + t * (maxY - minY)
        };
        if (isPointInPolygonSlow(diag1, polygon)) {
            candidates.push(diag1);
        }

        // Anti-diagonal
        const diag2 = {
            x: minX + t * (maxX - minX),
            y: maxY - t * (maxY - minY)
        };
        if (isPointInPolygonSlow(diag2, polygon)) {
            candidates.push(diag2);
        }
    }

    // Strategy 4: Add bounding box center as fallback
    const bboxCenter = { x: (minX + maxX) / 2, y: (minY + maxY) / 2 };
    if (isPointInPolygonSlow(bboxCenter, polygon)) {
        candidates.push(bboxCenter);
    }

    if (candidates.length === 0) {
        if (debugMode) console.warn('[adaptive-centroid] No interior points found, using bounding box center');
        return bboxCenter;
    }

    if (debugMode) console.debug(`[adaptive-centroid] Generated ${candidates.length} candidate centroids`);

    // Test each candidate with multiple angles for better evaluation
    let bestCentroid = candidates[0];
    let bestArea = 0;
    const testAngles = [0, 30, 60, 90]; // Test 4 angles per centroid

    for (const candidate of candidates) {
        let maxAreaForCandidate = 0;

        for (const angle of testAngles) {
            const testRect = tryRectangleAtAngle(polygon, angle, candidate, false);
            if (testRect && testRect.area > maxAreaForCandidate) {
                maxAreaForCandidate = testRect.area;
            }
        }

        if (maxAreaForCandidate > bestArea) {
            bestArea = maxAreaForCandidate;
            bestCentroid = candidate;
        }
    }

    if (debugMode) {
        console.debug(`[adaptive-centroid] Best centroid at (${bestCentroid.x.toFixed(1)}, ${bestCentroid.y.toFixed(1)}) with max area ${bestArea.toFixed(1)}`);
    }

    return bestCentroid;
}

// Fast inscribed rectangle finder with adaptive centroid selection
function fastInscribedRectangle(polygon, options = {}) {
    const startTime = performance.now();
    const {
        angleStep = 12,        // OPTIMIZED: Slightly finer initial angle step
        refinementStep = 2,    // OPTIMIZED: Finer refinement step
        maxTime = 100,         // Max time in ms
        debugMode = false      // Enable debug logging
    } = options;

    // Calculate bounds
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity, maxY = -Infinity;

    for (const p of polygon) {
        minX = Math.min(minX, p.x);
        maxX = Math.max(maxX, p.x);
        minY = Math.min(minY, p.y);
        maxY = Math.max(maxY, p.y);
    }

    // Adaptive centroid selection based on polygon shape
    const isConvex = isPolygonConvex(polygon);
    let centroids = [];

    if (isConvex) {
        // For convex polygons, use area-weighted centroid
        const areaCentroid = getPolygonAreaCentroid(polygon, minX, maxX, minY, maxY);
        centroids = [areaCentroid];
        if (debugMode) console.debug('[fast-rectangle] Convex polygon detected, using area centroid');
    } else {
        // For concave polygons, try multiple strategic centroids
        centroids = getMultipleCentroids(polygon, minX, maxX, minY, maxY, { debugMode });
        if (debugMode) console.debug(`[fast-rectangle] Concave polygon detected, testing ${centroids.length} centroids`);
    }

    // OPTIMIZED: Test each centroid with early exit for poor performers
    let bestRect = null;
    let bestArea = 0;
    let bestCentroid = null;

    // Sort centroids by distance from polygon center for better early candidates
    const polygonCenter = { x: (minX + maxX) / 2, y: (minY + maxY) / 2 };
    centroids.sort((a, b) => {
        const distA = Math.sqrt(Math.pow(a.x - polygonCenter.x, 2) + Math.pow(a.y - polygonCenter.y, 2));
        const distB = Math.sqrt(Math.pow(b.x - polygonCenter.x, 2) + Math.pow(b.y - polygonCenter.y, 2));
        return distA - distB;
    });

    for (let i = 0; i < centroids.length; i++) {
        const centroid = centroids[i];
        if (performance.now() - startTime > maxTime) break;

        // OPTIMIZED: Early exit only after testing sufficient centroids for parallelogram shapes
        if (i > 50 && bestArea > 0) {
            if (debugMode) console.debug(`[fast-rectangle] Early exit after testing ${i} centroids with area ${bestArea.toFixed(1)}`);
            break;
        }

        let bestAngleForCentroid = 0;
        let bestRectForCentroid = null;
        let bestAreaForCentroid = 0;

        // AUTOMATIC: Calculate the parallelogram's natural edge angles for optimal alignment
        const edgeAngles = [];
        for (let i = 0; i < polygon.length; i++) {
            const p1 = polygon[i];
            const p2 = polygon[(i + 1) % polygon.length];
            const angle = Math.atan2(p2.y - p1.y, p2.x - p1.x) * 180 / Math.PI;
            const normalizedAngle = ((angle % 180) + 180) % 180; // Normalize to 0-180
            edgeAngles.push(normalizedAngle);
        }

        // Get unique edge angles and their perpendiculars
        const uniqueAngles = [...new Set(edgeAngles.map(a => Math.round(a)))];
        const perpAngles = uniqueAngles.map(a => (a + 90) % 180);
        const naturalAngles = [...uniqueAngles, ...perpAngles];

        // ENHANCED: Strategic angles with natural parallelogram angles prioritized
        const strategicAngles = [
            ...naturalAngles, // Natural angles first (highest priority)
            0, 8, 16, 24, 32, 40, 48, 56, 64, 72, 76, 80, 82, 84, 86, 88, 90, 92, 94, 96, 98, 104, 112, 120, 128, 136, 144, 152, 160, 168, 176
        ].filter((angle, index, array) => array.indexOf(angle) === index); // Remove duplicates

        if (debugMode) console.debug(`[fast-rectangle] Natural edge angles: ${uniqueAngles.join(', ')}°, testing ${strategicAngles.length} total angles`);

        // First pass: Test strategic angles
        for (const angle of strategicAngles) {
            if (performance.now() - startTime > maxTime) break;

            const rect = tryRectangleAtAngle(polygon, angle, centroid, false);
            if (rect && rect.area > bestAreaForCentroid) {
                bestAreaForCentroid = rect.area;
                bestAngleForCentroid = angle;
                bestRectForCentroid = rect;
            }
        }

        // Second pass: Fine-tune around best strategic angle
        if (bestRectForCentroid) {
            for (let angle = bestAngleForCentroid - 8; angle <= bestAngleForCentroid + 8; angle += 2) {
                if (performance.now() - startTime > maxTime) break;
                if (strategicAngles.includes(angle)) continue; // Skip already tested angles

                const rect = tryRectangleAtAngle(polygon, angle, centroid, false);
                if (rect && rect.area > bestAreaForCentroid) {
                    bestAreaForCentroid = rect.area;
                    bestAngleForCentroid = angle;
                    bestRectForCentroid = rect;
                }
            }
        }


        // Check if this centroid produced the best result overall
        if (bestRectForCentroid && bestAreaForCentroid > bestArea) {
            bestArea = bestAreaForCentroid;
            bestRect = bestRectForCentroid;
            bestCentroid = centroid;
        }

        if (debugMode && bestRectForCentroid) {
            console.debug(`[fast-rectangle] Centroid (${centroid.x.toFixed(1)}, ${centroid.y.toFixed(1)}) produced area ${bestAreaForCentroid.toFixed(1)} at angle ${bestAngleForCentroid}°`);
        }
    }

    const elapsed = performance.now() - startTime;

    // TESTING: Reduced final validation to see if it allows larger rectangles
    let rejectedByValidation = false;
    if (bestRect && bestRect.corners) {
        const finalValidation = fastRectangleValidation(bestRect.corners, polygon, 20); // Reduced validation for testing
        if (!finalValidation) {
            console.warn(`[fast-rectangle] REJECTED: Final validation failed for rectangle ${bestRect.width.toFixed(1)}x${bestRect.height.toFixed(1)} - boundary violation detected`);
            rejectedByValidation = true;
            bestRect = null;
        }
    }

    // ALWAYS show timing information regardless of debug mode
    if (bestRect && bestCentroid) {
        console.log(`✅ [fast-rectangle] Found rectangle in ${elapsed.toFixed(1)}ms: ${bestRect.width.toFixed(1)}x${bestRect.height.toFixed(1)} at ${bestRect.angle}° using centroid (${bestCentroid.x.toFixed(1)}, ${bestCentroid.y.toFixed(1)})`);
    } else if (rejectedByValidation) {
        console.warn(`❌ [fast-rectangle] No valid rectangle found in ${elapsed.toFixed(1)}ms (rejected by boundary validation)`);
    } else {
        console.warn(`❌ [fast-rectangle] No valid rectangle found in ${elapsed.toFixed(1)}ms`);
    }

    return bestRect;
}

// Try to fit a rectangle at a specific angle
function tryRectangleAtAngle(polygon, angleDeg, centroid, debugMode = false) {
    const angleRad = (angleDeg * Math.PI) / 180;
    const cos = Math.cos(angleRad);
    const sin = Math.sin(angleRad);

    // Debug rotation matrix validation for critical angles
    if (debugMode && (angleDeg === 0 || angleDeg === 90 || angleDeg === 160)) {
        console.debug(`[rotation-test] Testing angle ${angleDeg}°: cos=${cos.toFixed(3)}, sin=${sin.toFixed(3)}`);

        // Test point (1, 0) rotation
        const testPoint = { x: 1, y: 0 };
        const rotated = {
            x: testPoint.x * cos - testPoint.y * sin,
            y: testPoint.x * sin + testPoint.y * cos
        };
        const backRotated = {
            x: rotated.x * cos + rotated.y * sin,
            y: rotated.x * sin - rotated.y * cos
        };
        console.debug(`[rotation-test] (1,0) -> (${rotated.x.toFixed(3)}, ${rotated.y.toFixed(3)}) -> (${backRotated.x.toFixed(3)}, ${backRotated.y.toFixed(3)})`);
    }

    // Rotate polygon (forward rotation by angle)
    const rotated = polygon.map(p => {
        const dx = p.x - centroid.x;
        const dy = p.y - centroid.y;
        return {
            x: centroid.x + dx * cos - dy * sin,
            y: centroid.y + dx * sin + dy * cos
        };
    });

    // Find axis-aligned bounds
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity, maxY = -Infinity;

    for (const p of rotated) {
        minX = Math.min(minX, p.x);
        maxX = Math.max(maxX, p.x);
        minY = Math.min(minY, p.y);
        maxY = Math.max(maxY, p.y);
    }

    // CRITICAL FIX: Use the rotated polygon bounds for proper scaling
    // The rotated bounds give the correct available space for the rectangle
    const rotatedWidth = maxX - minX;
    const rotatedHeight = maxY - minY;

    // OPTIMIZED: Test multiple strategic aspect ratios for better results
    let bestArea = 0;
    let bestResult = null;

    // ENHANCED: Test wider range of aspect ratios for parallelogram shapes
    const aspectRatios = [0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0]; // Much wider range including very wide rectangles

    for (const aspectRatio of aspectRatios) {
        let validScale = 0;
        let low = 0, high = 5.0; // AGGRESSIVE: Start with much larger potential rectangles

        // ENHANCED: More iterations for better precision
        const maxIterations = 20;
        for (let iter = 0; iter < maxIterations; iter++) {
            const scale = (low + high) / 2;

            // RADICAL NEW APPROACH: Scale based on polygon's maximum extents, not just bounding box
            // For parallelograms, the optimal rectangle can be much larger than the simple bounds
            const maxDimension = Math.max(rotatedWidth, rotatedHeight);
            let testWidth = maxDimension * scale;
            let testHeight = maxDimension * scale;

            // Adjust for aspect ratio
            if (aspectRatio > 1.0) {
                // Wider rectangle
                testWidth = testWidth * aspectRatio;
            } else {
                // Taller rectangle
                testHeight = testHeight / aspectRatio;
            }

            const testMinX = centroid.x - testWidth / 2;
            const testMinY = centroid.y - testHeight / 2;

            // Create test rectangle corners
            const corners = [
                { x: testMinX, y: testMinY },
                { x: testMinX + testWidth, y: testMinY },
                { x: testMinX + testWidth, y: testMinY + testHeight },
                { x: testMinX, y: testMinY + testHeight }
            ];

            // Rotate back to original orientation (inverse rotation by -angle)
            const originalCorners = corners.map(c => {
                const dx = c.x - centroid.x;
                const dy = c.y - centroid.y;
                return {
                    x: centroid.x + dx * cos + dy * sin,
                    y: centroid.y + dx * sin - dy * cos
                };
            });

            // TESTING: Use lower validation density to see if it allows larger rectangles
            const validationSamples = 12; // Reduced density for testing
            if (fastRectangleValidation(originalCorners, polygon, validationSamples)) {
                validScale = scale;
                low = scale;
            } else {
                high = scale;
            }
        }

        // Check if this aspect ratio produced a better result
        if (validScale > 0) {
            const maxDimension = Math.max(rotatedWidth, rotatedHeight);
            let finalWidth = maxDimension * validScale;
            let finalHeight = maxDimension * validScale;

            // Apply aspect ratio to final dimensions
            if (aspectRatio > 1.0) {
                finalWidth = finalWidth * aspectRatio;
            } else {
                finalHeight = finalHeight / aspectRatio;
            }

            const area = finalWidth * finalHeight;

            if (area > bestArea) {
                bestArea = area;

                const finalMinX = centroid.x - finalWidth / 2;
                const finalMinY = centroid.y - finalHeight / 2;

                const finalCorners = [
                    { x: finalMinX, y: finalMinY },
                    { x: finalMinX + finalWidth, y: finalMinY },
                    { x: finalMinX + finalWidth, y: finalMinY + finalHeight },
                    { x: finalMinX, y: finalMinY + finalHeight }
                ].map(c => {
                    const dx = c.x - centroid.x;
                    const dy = c.y - centroid.y;
                    return {
                        x: centroid.x + dx * cos + dy * sin,
                        y: centroid.y + dx * sin - dy * cos
                    };
                });

                bestResult = {
                    corners: finalCorners,
                    area: area,
                    width: finalWidth,
                    height: finalHeight,
                    angle: angleDeg
                };

                if (debugMode) console.debug(`[fast-rectangle] Angle ${angleDeg}°: Better result found with aspect ${aspectRatio.toFixed(1)}, scale ${validScale.toFixed(3)}, area ${area.toFixed(1)}`);
            }
        }
    }

    if (bestResult) {
        return bestResult;
    } else {
        if (debugMode) console.debug(`[fast-rectangle] Angle ${angleDeg}°: No valid rectangle found`);
        return null;
    }
}

// Export functions for use in SvgViewer
// For browser environment, attach to window object
if (typeof window !== 'undefined') {
    window.SpatialHash = SpatialHash;
    window.fastWindingAlgorithm = fastWindingAlgorithm;
    window.fastRectangleValidation = fastRectangleValidation;
    window.isPointInPolygonFast = isPointInPolygonFast;
    window.fastInscribedRectangle = fastInscribedRectangle;
    window.isPolygonConvex = isPolygonConvex;
    window.findBestCentroid = findBestCentroid;
    window.getMultipleCentroids = getMultipleCentroids;
}

// For Node.js environment
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        SpatialHash,
        fastWindingAlgorithm,
        fastRectangleValidation,
        isPointInPolygonFast,
        fastInscribedRectangle,
        isPolygonConvex,
        findBestCentroid,
        getMultipleCentroids
    };
}