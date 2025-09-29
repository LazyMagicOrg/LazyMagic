// Optimized SVG processing functions for SvgViewer
// Uses spatial hashing and early exit strategies for better performance

// ============================================================================
// POLYLABEL - Pole of Inaccessibility Algorithm
// Finds the point inside a polygon that is furthest from all edges
// Based on Mapbox's polylabel: https://github.com/mapbox/polylabel
// ============================================================================

class PolylabelCell {
    constructor(x, y, h, polygon) {
        this.x = x; // cell center x
        this.y = y; // cell center y
        this.h = h; // half the cell size

        // Distance from cell center to polygon outline
        this.d = pointToPolygonDist(x, y, polygon);

        // Maximum distance from any point in the cell to polygon outline
        this.max = this.d + this.h * Math.SQRT2;
    }
}

// Calculate distance from point to polygon outline (signed)
function pointToPolygonDist(x, y, polygon) {
    let inside = false;
    let minDistSq = Infinity;

    for (let i = 0, len = polygon.length, j = len - 1; i < len; j = i++) {
        const a = polygon[i];
        const b = polygon[j];

        if ((a.y > y) !== (b.y > y) &&
            (x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)) {
            inside = !inside;
        }

        minDistSq = Math.min(minDistSq, getSegDistSq(x, y, a, b));
    }

    return (inside ? 1 : -1) * Math.sqrt(minDistSq);
}

// Get squared distance from point to a segment
function getSegDistSq(px, py, a, b) {
    let x = a.x;
    let y = a.y;
    let dx = b.x - x;
    let dy = b.y - y;

    if (dx !== 0 || dy !== 0) {
        const t = ((px - x) * dx + (py - y) * dy) / (dx * dx + dy * dy);

        if (t > 1) {
            x = b.x;
            y = b.y;
        } else if (t > 0) {
            x += dx * t;
            y += dy * t;
        }
    }

    dx = px - x;
    dy = py - y;

    return dx * dx + dy * dy;
}

// Main polylabel function
function polylabel(polygon, precision = 1.0, debug = false) {
    // Find bounding box
    let minX = Infinity, minY = Infinity;
    let maxX = -Infinity, maxY = -Infinity;

    for (let i = 0; i < polygon.length; i++) {
        const p = polygon[i];
        if (p.x < minX) minX = p.x;
        if (p.y < minY) minY = p.y;
        if (p.x > maxX) maxX = p.x;
        if (p.y > maxY) maxY = p.y;
    }

    const width = maxX - minX;
    const height = maxY - minY;
    const cellSize = Math.min(width, height);
    let h = cellSize / 2;

    if (cellSize === 0) {
        return { x: minX, y: minY, distance: 0 };
    }

    // Priority queue of cells in order of their "potential" (max distance to polygon edge)
    const cellQueue = [];

    // Cover polygon with initial cells
    for (let x = minX; x < maxX; x += cellSize) {
        for (let y = minY; y < maxY; y += cellSize) {
            cellQueue.push(new PolylabelCell(x + h, y + h, h, polygon));
        }
    }

    // Take centroid as initial best guess
    let bestCell = getCentroidCell(polygon);

    // Special case for rectangular-ish polygons
    const bboxCell = new PolylabelCell(minX + width / 2, minY + height / 2, 0, polygon);
    if (bboxCell.d > bestCell.d) bestCell = bboxCell;

    let numProbes = cellQueue.length;

    while (cellQueue.length) {
        // Pick the most promising cell
        const cell = cellQueue.shift();

        // Update the best cell if we found a better one
        if (cell.d > bestCell.d) {
            bestCell = cell;
            if (debug) console.debug(`[polylabel] Found better cell: distance ${cell.d.toFixed(2)} at (${cell.x.toFixed(1)}, ${cell.y.toFixed(1)})`);
        }

        // If this cell can't possibly have a better solution, skip it
        if (cell.max - bestCell.d <= precision) continue;

        // Split the cell into four cells
        h = cell.h / 2;
        if (h === 0) continue;

        cellQueue.push(new PolylabelCell(cell.x - h, cell.y - h, h, polygon));
        cellQueue.push(new PolylabelCell(cell.x + h, cell.y - h, h, polygon));
        cellQueue.push(new PolylabelCell(cell.x - h, cell.y + h, h, polygon));
        cellQueue.push(new PolylabelCell(cell.x + h, cell.y + h, h, polygon));
        numProbes += 4;

        // Sort queue by max potential distance
        cellQueue.sort((a, b) => b.max - a.max);
    }

    if (debug) {
        console.debug(`[polylabel] Num probes: ${numProbes}`);
        console.debug(`[polylabel] Best distance: ${bestCell.d.toFixed(2)}`);
    }

    return {
        x: bestCell.x,
        y: bestCell.y,
        distance: bestCell.d
    };
}

function getCentroidCell(polygon) {
    let area = 0;
    let x = 0;
    let y = 0;

    for (let i = 0, len = polygon.length, j = len - 1; i < len; j = i++) {
        const a = polygon[i];
        const b = polygon[j];
        const f = a.x * b.y - b.x * a.y;
        x += (a.x + b.x) * f;
        y += (a.y + b.y) * f;
        area += f * 3;
    }

    if (area === 0) {
        return new PolylabelCell(polygon[0].x, polygon[0].y, 0, polygon);
    }

    return new PolylabelCell(x / area, y / area, 0, polygon);
}

// ============================================================================
// END POLYLABEL
// ============================================================================

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

// Create spatial grid for polygon validation (cached)
let polygonSpatialGridCache = new WeakMap();

function getSpatialGridForPolygon(polygon) {
    if (polygonSpatialGridCache.has(polygon)) {
        return polygonSpatialGridCache.get(polygon);
    }

    // Calculate polygon bounds for better cell size estimation
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const point of polygon) {
        minX = Math.min(minX, point.x);
        minY = Math.min(minY, point.y);
        maxX = Math.max(maxX, point.x);
        maxY = Math.max(maxY, point.y);
    }

    const width = maxX - minX;
    const height = maxY - minY;
    const avgDimension = (width + height) / 2;

    // Use much smaller cell size for higher precision (2-3% of average dimension)
    const cellSize = Math.max(2, Math.min(8, avgDimension * 0.025));

    const spatialGrid = new SpatialGrid(polygon, cellSize);
    polygonSpatialGridCache.set(polygon, spatialGrid);
    return spatialGrid;
}

// ENHANCED validation using SpatialGrid from KDTree (SILENT - no logging during search)
function fastRectangleValidation(corners, polygon, samples = 16) {
    const spatialGrid = getSpatialGridForPolygon(polygon);

    // Test all 4 corners
    for (const corner of corners) {
        if (!spatialGrid.containsPoint(corner)) {
            return false;
        }
    }

    // Test center point
    const centerX = (corners[0].x + corners[1].x + corners[2].x + corners[3].x) / 4;
    const centerY = (corners[0].y + corners[1].y + corners[2].y + corners[3].y) / 4;
    if (!spatialGrid.containsPoint({x: centerX, y: centerY})) {
        return false;
    }

    // Use SpatialGrid's rectangle validation for ultimate accuracy
    const minX = Math.min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
    const maxX = Math.max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
    const minY = Math.min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
    const maxY = Math.max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);

    // For axis-aligned rectangles, use the optimized rectangle test
    const isAxisAligned = (
        Math.abs(corners[0].y - corners[1].y) < 0.01 &&
        Math.abs(corners[2].y - corners[3].y) < 0.01 &&
        Math.abs(corners[0].x - corners[3].x) < 0.01 &&
        Math.abs(corners[1].x - corners[2].x) < 0.01
    );

    if (isAxisAligned) {
        return spatialGrid.containsRectangle(minX, minY, maxX, maxY);
    }

    // For rotated rectangles, test edge points more carefully
    const pointsPerEdge = 16; // Increased density for better accuracy
    for (let edgeIdx = 0; edgeIdx < 4; edgeIdx++) {
        const start = corners[edgeIdx];
        const end = corners[(edgeIdx + 1) % 4];

        for (let i = 1; i < pointsPerEdge; i++) {
            const t = i / pointsPerEdge;
            const testPoint = {
                x: start.x + t * (end.x - start.x),
                y: start.y + t * (end.y - start.y)
            };

            if (!spatialGrid.containsPoint(testPoint)) {
                return false;
            }
        }
    }

    // Additional validation: Test interior points for rotated rectangles
    for (let i = 1; i <= 3; i++) {
        for (let j = 1; j <= 3; j++) {
            const u = i / 4; // 0.25, 0.5, 0.75
            const v = j / 4;

            // Bilinear interpolation within the quadrilateral
            const interiorPoint = {
                x: (1-u)*(1-v)*corners[0].x + u*(1-v)*corners[1].x + u*v*corners[2].x + (1-u)*v*corners[3].x,
                y: (1-u)*(1-v)*corners[0].y + u*(1-v)*corners[1].y + u*v*corners[2].y + (1-u)*v*corners[3].y
            };

            if (!spatialGrid.containsPoint(interiorPoint)) {
                return false;
            }
        }
    }

    return true;
}

// ENHANCED point-in-polygon test using SpatialGrid when available, fallback to winding number
function isPointInPolygonSlow(point, polygon) {
    if (!point || !polygon || polygon.length < 3) {
        return false;
    }

    // Try to use cached SpatialGrid for better accuracy
    if (polygonSpatialGridCache.has(polygon)) {
        const spatialGrid = polygonSpatialGridCache.get(polygon);
        return spatialGrid.containsPoint(point);
    }

    // Fallback to winding number algorithm for backwards compatibility
    const x = point.x;
    const y = point.y;
    let windingNumber = 0;

    for (let i = 0; i < polygon.length; i++) {
        const j = (i + 1) % polygon.length;
        const xi = polygon[i].x;
        const yi = polygon[i].y;
        const xj = polygon[j].x;
        const yj = polygon[j].y;

        // Check if point is exactly on a vertex (with tolerance)
        const tolerance = 0.1; // Small tolerance for floating point
        if ((Math.abs(xi - x) <= tolerance && Math.abs(yi - y) <= tolerance) ||
            (Math.abs(xj - x) <= tolerance && Math.abs(yj - y) <= tolerance)) {
            return false; // Conservative: point on vertex = outside
        }

        // Check for upward crossing
        if (yi <= y) {
            if (yj > y) { // Upward crossing
                const crossProduct = (xj - xi) * (y - yi) - (x - xi) * (yj - yi);
                if (crossProduct > 0) { // Point is left of edge
                    windingNumber++;
                }
            }
        } else {
            // Downward crossing
            if (yj <= y) {
                const crossProduct = (xj - xi) * (y - yi) - (x - xi) * (yj - yi);
                if (crossProduct < 0) { // Point is right of edge
                    windingNumber--;
                }
            }
        }
    }

    // Point is inside if winding number is non-zero
    return windingNumber !== 0;
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

// Get centroids using hybrid approach: Polylabel + strategic sampling
// Polylabel finds the deepest interior point, but we need more coverage for optimal rectangles
function getMultipleCentroids(polygon, minX, maxX, minY, maxY, options = {}) {
    const { debugMode = false } = options;
    const centroids = [];

    const width = maxX - minX;
    const height = maxY - minY;

    // 1. Use Polylabel to find THE pole of inaccessibility (deepest interior point)
    const precision = 1.0;
    const pole = polylabel(polygon, precision, debugMode);

    if (debugMode) {
        console.debug(`[multi-centroid] Polylabel found pole at (${pole.x.toFixed(1)}, ${pole.y.toFixed(1)}) with distance ${pole.distance.toFixed(1)}`);
    }

    // Always add the pole first
    centroids.push({ x: pole.x, y: pole.y });

    // 2. Add standard geometric centroids
    const standardCentroids = [];

    // Area-weighted centroid
    const areaCentroid = getPolygonAreaCentroid(polygon, minX, maxX, minY, maxY);
    if (isPointInPolygonSlow(areaCentroid, polygon)) {
        standardCentroids.push(areaCentroid);
    }

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

    // Bounding box center
    const bboxCenter = { x: (minX + maxX) / 2, y: (minY + maxY) / 2 };
    if (isPointInPolygonSlow(bboxCenter, polygon)) {
        standardCentroids.push(bboxCenter);
    }

    // 3. Add strategic grid sampling with FIXED step size and ALIGNED origins
    // Use fixed pixel spacing with grid aligned to absolute coordinates
    // This ensures Shape 1, 2, 3 all test the SAME grid points in overlapping regions
    const stepSize = 12.0; // Fixed 12-pixel step balances coverage with performance

    // Start grid from multiples of stepSize (aligned to origin 0,0)
    const gridStartX = Math.ceil(minX / stepSize) * stepSize;
    const gridStartY = Math.ceil(minY / stepSize) * stepSize;
    const gridEndX = Math.floor(maxX / stepSize) * stepSize;
    const gridEndY = Math.floor(maxY / stepSize) * stepSize;

    for (let x = gridStartX; x <= gridEndX; x += stepSize) {
        for (let y = gridStartY; y <= gridEndY; y += stepSize) {
            const candidate = { x, y };

            if (isPointInPolygonSlow(candidate, polygon)) {
                standardCentroids.push(candidate);
            }
        }
    }

    const gridCountX = Math.floor((gridEndX - gridStartX) / stepSize) + 1;
    const gridCountY = Math.floor((gridEndY - gridStartY) / stepSize) + 1;

    // 4. Add strategic offset patterns from pole and other key points
    // Sample points around the pole to handle cases where rectangle center isn't at pole
    const offsets = [
        { dx: 0.1, dy: 0 },
        { dx: -0.1, dy: 0 },
        { dx: 0, dy: 0.1 },
        { dx: 0, dy: -0.1 },
        { dx: 0.2, dy: 0 },
        { dx: -0.2, dy: 0 },
        { dx: 0, dy: 0.2 },
        { dx: 0, dy: -0.2 },
        { dx: 0.15, dy: 0.15 },
        { dx: -0.15, dy: 0.15 },
        { dx: 0.15, dy: -0.15 },
        { dx: -0.15, dy: -0.15 },
        { dx: 0.1, dy: 0.1 },
        { dx: -0.1, dy: 0.1 },
        { dx: 0.1, dy: -0.1 },
        { dx: -0.1, dy: -0.1 }
    ];

    // Add offsets from pole
    for (const offset of offsets) {
        const candidate = {
            x: pole.x + offset.dx * width,
            y: pole.y + offset.dy * height
        };
        if (isPointInPolygonSlow(candidate, polygon)) {
            standardCentroids.push(candidate);
        }
    }

    // Also add offsets from area centroid if it's different from pole
    if (isPointInPolygonSlow(areaCentroid, polygon)) {
        const dist = Math.sqrt(
            Math.pow(areaCentroid.x - pole.x, 2) +
            Math.pow(areaCentroid.y - pole.y, 2)
        );
        if (dist > 10) {
            for (const offset of offsets) {
                const candidate = {
                    x: areaCentroid.x + offset.dx * width,
                    y: areaCentroid.y + offset.dy * height
                };
                if (isPointInPolygonSlow(candidate, polygon)) {
                    standardCentroids.push(candidate);
                }
            }
        }
    }

    // Add all standard centroids
    centroids.push(...standardCentroids);

    // Snap all centroids to match the base grid for consistency across shape extensions
    // This ensures Shape 1, 2, 3 test EXACTLY the same centroid coordinates
    // CRITICAL: snap size must equal grid step size for perfect alignment
    const snapSize = stepSize; // Use same 12-pixel snap as grid step
    const snappedCentroids = centroids.map(c => ({
        x: Math.round(c.x / snapSize) * snapSize,
        y: Math.round(c.y / snapSize) * snapSize
    }));

    // Remove duplicates after snapping
    const uniqueCentroids = [];
    const seenKeys = new Set();

    for (const centroid of snappedCentroids) {
        const key = `${centroid.x},${centroid.y}`;
        if (!seenKeys.has(key)) {
            seenKeys.add(key);
            uniqueCentroids.push(centroid);
        }
    }

    if (debugMode) console.debug(`[multi-centroid] Using ${uniqueCentroids.length} centroids (pole + ${standardCentroids.length} strategic)`);

    // DEBUG: Log polygon characteristics with RECT-DEBUG prefix for easy filtering
    console.log(`🔷 [RECT-DEBUG] Polygon: ${polygon.length} vertices, bounds: (${minX.toFixed(1)}, ${minY.toFixed(1)}) to (${maxX.toFixed(1)}, ${maxY.toFixed(1)}), size: ${width.toFixed(1)}x${height.toFixed(1)}`);
    console.log(`🔷 [RECT-DEBUG] Grid: ${stepSize.toFixed(1)}px step, aligned from (${gridStartX.toFixed(1)}, ${gridStartY.toFixed(1)}) to (${gridEndX.toFixed(1)}, ${gridEndY.toFixed(1)}), snap=${snapSize.toFixed(1)}px`);
    console.log(`🔷 [RECT-DEBUG] Centroids: ${uniqueCentroids.length} total (grid: ${gridCountX}x${gridCountY})`);
    console.log(`🔷 [RECT-DEBUG] Pole: (${pole.x.toFixed(1)}, ${pole.y.toFixed(1)}), Area centroid: (${areaCentroid.x.toFixed(1)}, ${areaCentroid.y.toFixed(1)})`);

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
            const gridSize = scale === 0.8 ? 15 : 10; // Much denser grid for better coverage
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

    // DEBUG: Show polygon characteristics IMMEDIATELY
    if (!polygon || polygon.length < 3) {
        console.error('[polygon-debug] Invalid polygon data');
        return null;
    }

    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const point of polygon) {
        minX = Math.min(minX, point.x);
        minY = Math.min(minY, point.y);
        maxX = Math.max(maxX, point.x);
        maxY = Math.max(maxY, point.y);
    }

    const polygonWidth = maxX - minX;
    const polygonHeight = maxY - minY;
    console.log(`🔍 [POLYGON-DEBUG] ${polygon.length} vertices, bounds (${minX.toFixed(1)}, ${minY.toFixed(1)}) to (${maxX.toFixed(1)}, ${maxY.toFixed(1)}), size ${polygonWidth.toFixed(1)}x${polygonHeight.toFixed(1)}`);

    const {
        angleStep = 12,        // OPTIMIZED: Slightly finer initial angle step
        refinementStep = 2,    // OPTIMIZED: Finer refinement step
        maxTime = 100,         // Max time in ms
        debugMode = false      // Enable debug logging
    } = options;

    // Bounds already calculated above

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
    let previousBestArea = 0;

    // Sort centroids by distance from polygon center for better early candidates
    // Use stable sorting with coordinate tiebreaker for deterministic results
    const polygonCenter = { x: (minX + maxX) / 2, y: (minY + maxY) / 2 };
    centroids.sort((a, b) => {
        const distA = Math.sqrt(Math.pow(a.x - polygonCenter.x, 2) + Math.pow(a.y - polygonCenter.y, 2));
        const distB = Math.sqrt(Math.pow(b.x - polygonCenter.x, 2) + Math.pow(b.y - polygonCenter.y, 2));

        // Primary sort: distance from center
        if (Math.abs(distA - distB) > 0.1) {
            return distA - distB;
        }

        // Tiebreaker: x coordinate, then y coordinate for stable sorting
        if (Math.abs(a.x - b.x) > 0.1) {
            return a.x - b.x;
        }
        return a.y - b.y;
    });

    for (let i = 0; i < centroids.length; i++) {
        const centroid = centroids[i];
        if (performance.now() - startTime > maxTime) break;

        // Log when we test the critical centroid
        if (centroid.x === 552 && centroid.y === 228) {
            console.log(`🔷 [RECT-DEBUG] *** Testing critical centroid (552, 228) for ${polygon.length}-vertex polygon ***`);
        }

        // DETERMINISTIC: Test ALL centroids for truly optimal result
        // Early exit disabled to ensure consistent results

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

        // Get unique edge angles and their perpendiculars - DETERMINISTIC ORDER
        const uniqueAngles = edgeAngles
            .map(a => Math.round(a))
            .filter((angle, index, array) => array.indexOf(angle) === index) // Remove duplicates while preserving order
            .sort((a, b) => a - b); // Sort for consistent ordering

        const perpAngles = uniqueAngles.map(a => (a + 90) % 180).sort((a, b) => a - b);

        // ENHANCED: Strategic angles with natural parallelogram angles prioritized
        // Use deterministic ordering by combining and sorting all angles
        const baseStrategicAngles = [0, 8, 16, 24, 32, 40, 48, 56, 64, 72, 76, 80, 82, 84, 86, 88, 90, 92, 94, 96, 98, 104, 112, 120, 128, 136, 144, 152, 160, 168, 176];

        const allAngles = [...uniqueAngles, ...perpAngles, ...baseStrategicAngles];
        const strategicAngles = allAngles
            .filter((angle, index, array) => array.indexOf(angle) === index) // Remove duplicates
            .sort((a, b) => {
                // Prioritize natural angles, then sort by value
                const aIsNatural = uniqueAngles.includes(a) || perpAngles.includes(a);
                const bIsNatural = uniqueAngles.includes(b) || perpAngles.includes(b);

                if (aIsNatural && !bIsNatural) return -1;
                if (!aIsNatural && bIsNatural) return 1;
                return a - b;
            });

        // Special debug flag for critical centroid - use exact comparison since we're snapping to grid
        const isCriticalCentroid = (centroid.x === 552 && centroid.y === 228);

        if (isCriticalCentroid) {
            console.log(`🔷 [RECT-DEBUG] Testing ${strategicAngles.length} angles at (552, 228), angle list: [${strategicAngles.slice(0, 15).join(', ')}...]`);
        }

        // First pass: Test strategic angles
        for (const angle of strategicAngles) {
            if (performance.now() - startTime > maxTime) break;

            // Special debug logging for angle 22° at centroid (552, 228)
            const isCriticalTest = isCriticalCentroid && angle === 22;

            const rect = tryRectangleAtAngle(polygon, angle, centroid, isCriticalTest);

            if (isCriticalTest) {
                if (rect) {
                    console.log(`🔷 [RECT-DEBUG] Angle 22° result: ${polygon.length}-vertex polygon: Found ${rect.width.toFixed(1)}×${rect.height.toFixed(1)}, AREA=${rect.area.toFixed(0)}, current best: ${bestAreaForCentroid.toFixed(0)}`);
                } else {
                    console.log(`🔷 [RECT-DEBUG] Angle 22° result: ${polygon.length}-vertex polygon: NO VALID RECTANGLE FOUND`);
                }
            }

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

            console.log(`🔷 [RECT-DEBUG] New best: centroid (${centroid.x.toFixed(1)}, ${centroid.y.toFixed(1)}) → area ${bestAreaForCentroid.toFixed(0)} at angle ${bestAngleForCentroid}°`);
        }
    }

    const elapsed = performance.now() - startTime;

    // FINAL CHECK: Test the winning rectangle for boundary violations
    let rejectedByValidation = false;
    if (bestRect && bestRect.corners) {
        // Test if ANY point violates boundaries
        let violationFound = false;
        const testPoints = [...bestRect.corners];

        // Add center point
        const centerX = (bestRect.corners[0].x + bestRect.corners[1].x + bestRect.corners[2].x + bestRect.corners[3].x) / 4;
        const centerY = (bestRect.corners[0].y + bestRect.corners[1].y + bestRect.corners[2].y + bestRect.corners[3].y) / 4;
        testPoints.push({x: centerX, y: centerY});

        for (const point of testPoints) {
            if (!isPointInPolygonSlow(point, polygon)) {
                console.error(`🚨 FINAL CHECK: Rectangle has boundary violation at (${point.x.toFixed(1)}, ${point.y.toFixed(1)})`);
                violationFound = true;
                break;
            }
        }

        if (violationFound) {
            console.error(`🚨 BOUNDARY VIOLATION: Rectangle ${bestRect.width.toFixed(1)}x${bestRect.height.toFixed(1)} rejected`);
            bestRect = null;
            rejectedByValidation = true;
        }
    }

    // DEBUG: Show polygon characteristics first
    // polygonWidth and polygonHeight already declared above
    console.log(`[polygon-debug] Shape: ${polygon.length} vertices, bounds (${minX.toFixed(1)}, ${minY.toFixed(1)}) to (${maxX.toFixed(1)}, ${maxY.toFixed(1)}), size ${polygonWidth.toFixed(1)}x${polygonHeight.toFixed(1)}`);

    // ALWAYS show timing information regardless of debug mode
    if (bestRect && bestCentroid) {
        console.log(`🔷 [RECT-DEBUG] Result: ${bestRect.width.toFixed(1)}x${bestRect.height.toFixed(1)}, AREA=${bestRect.area.toFixed(0)}, angle=${bestRect.angle}°, centroid=(${bestCentroid.x.toFixed(1)}, ${bestCentroid.y.toFixed(1)}), time=${elapsed.toFixed(1)}ms, tested=${centroids.length} centroids`);

        // FINAL DEBUG: Show polygon characteristics after area calculation
        console.log(`🔍 [SHAPE-DEBUG] FINAL: ${polygon.length} vertices, bounds (${minX.toFixed(1)}, ${minY.toFixed(1)}) to (${maxX.toFixed(1)}, ${maxY.toFixed(1)}), size ${polygonWidth.toFixed(1)}x${polygonHeight.toFixed(1)}, AREA: ${bestRect.area.toFixed(0)}, TIME: ${elapsed.toFixed(1)}ms`);
    } else if (rejectedByValidation) {
        console.warn(`❌ [fast-rectangle] No valid rectangle found in ${elapsed.toFixed(1)}ms (rejected by boundary validation)`);
        console.log(`🔍 [SHAPE-DEBUG] FAILED: ${polygon.length} vertices, bounds (${minX.toFixed(1)}, ${minY.toFixed(1)}) to (${maxX.toFixed(1)}, ${maxY.toFixed(1)}), size ${polygonWidth.toFixed(1)}x${polygonHeight.toFixed(1)}, AREA: 0, TIME: ${elapsed.toFixed(1)}ms`);
    } else {
        console.warn(`❌ [fast-rectangle] No valid rectangle found in ${elapsed.toFixed(1)}ms`);
        console.log(`🔍 [SHAPE-DEBUG] FAILED: ${polygon.length} vertices, bounds (${minX.toFixed(1)}, ${minY.toFixed(1)}) to (${maxX.toFixed(1)}, ${maxY.toFixed(1)}), size ${polygonWidth.toFixed(1)}x${polygonHeight.toFixed(1)}, AREA: 0, TIME: ${elapsed.toFixed(1)}ms`);
    }

    // Add elapsed time to result
    if (bestRect) {
        bestRect.elapsed = elapsed;
    }

    return bestRect;
}

// Helper: Calculate point to line segment distance
function pointToSegmentDistance(point, p1, p2) {
    const dx = p2.x - p1.x;
    const dy = p2.y - p1.y;
    const lengthSq = dx * dx + dy * dy;

    if (lengthSq === 0) return Math.hypot(point.x - p1.x, point.y - p1.y);

    // Project point onto line segment
    let t = ((point.x - p1.x) * dx + (point.y - p1.y) * dy) / lengthSq;
    t = Math.max(0, Math.min(1, t)); // Clamp to segment

    const projX = p1.x + t * dx;
    const projY = p1.y + t * dy;

    return Math.hypot(point.x - projX, point.y - projY);
}

// Calculate minimum distance from centroid to any polygon edge
function getMinDistanceToEdge(polygon, centroid) {
    // Build spatial hash of polygon edges
    const spatialHash = new SpatialHash(20); // 20px cells

    // Add edge midpoints to spatial hash for fast lookup
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];
        const midpoint = { x: (p1.x + p2.x) / 2, y: (p1.y + p2.y) / 2 };
        spatialHash.add(midpoint, { edge: { p1, p2 }, index: i });
    }

    // Start with a conservative search radius
    let searchRadius = 150; // Larger initial radius to catch edges
    let minDistance = Infinity;

    // Find nearby edges using spatial hash
    const nearbyEdges = spatialHash.findNear(centroid, searchRadius);

    // If we found edges, calculate precise distance to each
    for (const item of nearbyEdges) {
        const { p1, p2 } = item.data.edge;
        const dist = pointToSegmentDistance(centroid, p1, p2);
        minDistance = Math.min(minDistance, dist);
    }

    // If no edges found in initial radius, expand search (fallback)
    if (nearbyEdges.length === 0) {
        // Brute force fallback - check all edges
        for (let i = 0; i < polygon.length; i++) {
            const p1 = polygon[i];
            const p2 = polygon[(i + 1) % polygon.length];
            const dist = pointToSegmentDistance(centroid, p1, p2);
            minDistance = Math.min(minDistance, dist);
        }
    }

    return minDistance;
}

// Try to fit a rectangle at a specific angle
function tryRectangleAtAngle(polygon, angleDeg, centroid, debugMode = false) {
    const angleRad = (angleDeg * Math.PI) / 180;
    const cos = Math.cos(angleRad);
    const sin = Math.sin(angleRad);

    // Enhanced debug logging for angle 22°
    if (debugMode && angleDeg === 22) {
        console.log(`🔷 [RECT-DEBUG] >> Starting angle 22° test for ${polygon.length}-vertex polygon at centroid (${centroid.x.toFixed(1)}, ${centroid.y.toFixed(1)})`);
    }

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

    // Find axis-aligned bounds (keep for reference/debugging)
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity, maxY = -Infinity;

    for (const p of rotated) {
        minX = Math.min(minX, p.x);
        maxX = Math.max(maxX, p.x);
        minY = Math.min(minY, p.y);
        maxY = Math.max(maxY, p.y);
    }

    const polygonWidth = maxX - minX;
    const polygonHeight = maxY - minY;

    // NEW: Use distance to edge for scaling (monotonic approach)
    // Calculate minimum distance from centroid to any edge in rotated space
    const distanceToEdge = getMinDistanceToEdge(rotated, centroid);
    const baseDimension = distanceToEdge * 1.8; // Diameter with conservative factor

    // Debug logging for angle 22°
    if (debugMode && angleDeg === 22) {
        console.log(`🔷 [RECT-DEBUG] >> Angle 22° rotated bounds: size ${polygonWidth.toFixed(1)}×${polygonHeight.toFixed(1)}, distToEdge=${distanceToEdge.toFixed(1)}, baseDim=${baseDimension.toFixed(1)}`);
    }

    // OPTIMIZED: Test multiple strategic aspect ratios for better results
    let bestArea = 0;
    let bestResult = null;

    // ENHANCED: Test wider range of aspect ratios for parallelogram shapes
    const aspectRatios = [0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0]; // Much wider range including very wide rectangles

    for (const aspectRatio of aspectRatios) {
        let validScale = 0;
        let low = 0, high = 1.5; // Conservative but reasonable scaling limit

        // CONSERVATIVE: Use polygon bounds with moderate scaling
        const maxIterations = 15;
        const precision = 0.001; // Binary search precision

        for (let iter = 0; iter < maxIterations && (high - low) > precision; iter++) {
            const scale = Math.round((low + high) * 500) / 1000; // Round to 3 decimal places for determinism

            // NEW: Scale based on distance to edge (monotonic approach)
            let testWidth = baseDimension * scale;
            let testHeight = baseDimension * scale;

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

            // MINIMAL: Use basic validation only
            const validationSamples = 8; // Minimal for testing
            if (fastRectangleValidation(originalCorners, polygon, validationSamples)) {
                validScale = scale;
                low = scale;
            } else {
                high = scale;
            }
        }

        // Check if this aspect ratio produced a better result
        if (validScale > 0) {
            let finalWidth = baseDimension * validScale;
            let finalHeight = baseDimension * validScale;

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

                if (debugMode) {
                    if (angleDeg === 22) {
                        console.log(`🔷 [RECT-DEBUG] >> Angle 22° better result: aspect ${aspectRatio.toFixed(1)}, dims ${finalWidth.toFixed(1)}×${finalHeight.toFixed(1)}, AREA=${area.toFixed(0)}`);
                    } else {
                        console.debug(`[fast-rectangle] Angle ${angleDeg}°: Better result found with aspect ${aspectRatio.toFixed(1)}, scale ${validScale.toFixed(3)}, area ${area.toFixed(1)}`);
                    }
                }
            }
        }
    }

    if (bestResult) {
        if (debugMode && angleDeg === 22) {
            console.log(`🔷 [RECT-DEBUG] >> Angle 22° FINAL: Returning ${bestResult.width.toFixed(1)}×${bestResult.height.toFixed(1)}, AREA=${bestResult.area.toFixed(0)}`);
        }
        return bestResult;
    } else {
        if (debugMode) {
            if (angleDeg === 22) {
                console.log(`🔷 [RECT-DEBUG] >> Angle 22° FINAL: No valid rectangle found`);
            } else {
                console.debug(`[fast-rectangle] Angle ${angleDeg}°: No valid rectangle found`);
            }
        }
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