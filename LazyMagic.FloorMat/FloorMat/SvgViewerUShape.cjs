/**
 * U-Shape Layout Inscribed Rectangle Algorithm
 *
 * Finds the largest U-shape-style layout that fits within a polygon.
 *
 * U-Shape constraints:
 * - Minimum dimensions: 18.5 ft × 14 ft (3 tables in U configuration)
 *   - Base: 2 tables back-to-back (6ft wide × 2.5ft each = 5ft deep including 1ft spacing)
 *   - Plus 1 bookend table (6ft × 2.5ft)
 *   - Plus 4ft spacing on all sides
 *   - Result: (6ft + 2.5ft + 8ft spacing) × (6ft + 8ft spacing) = 16.5ft × 14ft
 *   - Actual minimum accounting for layout: 18.5ft × 14ft
 * - Two degrees of freedom:
 *   - lengthRun: Tables along the two parallel sides (extends in 6ft increments, adds 2 tables)
 *   - depthRun: Tables along the single bookend side (extends in 6ft increments, adds 1 table)
 * - Can be rotated to any angle
 * - Opening on one side (missing 4th side compared to hollow square)
 *
 * Returns the U-shape layout with maximum area
 */

// Point-in-polygon test using winding number algorithm
function isPointInPolygon(point, polygon) {
    let winding = 0;

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        if (p1.y <= point.y) {
            if (p2.y > point.y) {
                const cross = (p2.x - p1.x) * (point.y - p1.y) - (point.x - p1.x) * (p2.y - p1.y);
                if (cross > 0) {
                    winding++;
                }
            }
        } else {
            if (p2.y <= point.y) {
                const cross = (p2.x - p1.x) * (point.y - p1.y) - (point.x - p1.x) * (p2.y - p1.y);
                if (cross < 0) {
                    winding--;
                }
            }
        }
    }

    return winding !== 0;
}

// Check if two line segments intersect
function segmentsIntersect(p1, p2, p3, p4) {
    const denominator = (p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y);

    // Lines are parallel
    if (Math.abs(denominator) < 1e-10) {
        return false;
    }

    const ua = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x)) / denominator;
    const ub = ((p2.x - p1.x) * (p1.y - p3.y) - (p2.y - p1.y) * (p1.x - p3.x)) / denominator;

    // Check if intersection point is within both line segments
    // Use a small epsilon to avoid false positives at shared endpoints
    const epsilon = 1e-6;
    return (ua > epsilon && ua < 1 - epsilon && ub > epsilon && ub < 1 - epsilon);
}

// Check if all corners of a rectangle are inside the polygon
function isRectangleInsidePolygon(corners, polygon) {
    // Check all 4 corners
    for (const corner of corners) {
        if (!isPointInPolygon(corner, polygon)) {
            return false;
        }
    }

    // Check that rectangle edges don't intersect polygon edges
    // This catches cases where corners are inside but edges breach the boundary
    for (let i = 0; i < corners.length; i++) {
        const rectStart = corners[i];
        const rectEnd = corners[(i + 1) % corners.length];

        for (let j = 0; j < polygon.length; j++) {
            const polyStart = polygon[j];
            const polyEnd = polygon[(j + 1) % polygon.length];

            if (segmentsIntersect(rectStart, rectEnd, polyStart, polyEnd)) {
                return false;
            }
        }
    }

    return true;
}

// Calculate rectangle corners given centroid, dimensions, and angle
function calculateRectangleCorners(centroid, width, height, angleDegrees) {
    const angleRad = angleDegrees * Math.PI / 180;
    const cos = Math.cos(angleRad);
    const sin = Math.sin(angleRad);

    const halfWidth = width / 2;
    const halfHeight = height / 2;

    // Start with unrotated rectangle centered at origin
    const localCorners = [
        { x: -halfWidth, y: -halfHeight },  // Bottom-left
        { x:  halfWidth, y: -halfHeight },  // Bottom-right
        { x:  halfWidth, y:  halfHeight },  // Top-right
        { x: -halfWidth, y:  halfHeight }   // Top-left
    ];

    // Rotate and translate to centroid
    const corners = localCorners.map(corner => ({
        x: centroid.x + (corner.x * cos - corner.y * sin),
        y: centroid.y + (corner.x * sin + corner.y * cos)
    }));

    return corners;
}

// Calculate polygon centroid
function calculatePolygonCentroid(polygon) {
    let cx = 0, cy = 0, area = 0;

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];
        const cross = p1.x * p2.y - p2.x * p1.y;
        area += cross;
        cx += (p1.x + p2.x) * cross;
        cy += (p1.y + p2.y) * cross;
    }

    area /= 2;
    cx /= (6 * area);
    cy /= (6 * area);

    return { x: cx, y: cy };
}

// Get bounding box of polygon
function getPolygonBounds(polygon) {
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity, maxY = -Infinity;

    for (const point of polygon) {
        minX = Math.min(minX, point.x);
        maxX = Math.max(maxX, point.x);
        minY = Math.min(minY, point.y);
        maxY = Math.max(maxY, point.y);
    }

    return { minX, maxX, minY, maxY };
}

/**
 * Calculate number of tables in a U-shape configuration
 *
 * @param {number} lengthRun - Number of tables on EACH of the two parallel sides (0-based)
 * @param {number} depthRun - Number of tables on the single bookend side (0-based)
 * @returns {number} Total number of tables
 */
function calculateUShapeTables(lengthRun, depthRun) {
    // lengthRun tables on each of 2 parallel sides + depthRun tables on the bookend side
    return (lengthRun * 2) + depthRun;
}

/**
 * Calculate dimensions from table runs
 * Based on C# FitHollowU implementation:
 * - Width: (lengthRun * 6) + 2.5 + 8 = lengthRun * 6 + 10.5
 * - Height: (depthRun * 6) + 8 = depthRun * 6 + 8
 *
 * @param {number} lengthRun - Number of tables on each parallel side (0-based, 0 = 1 table per side)
 * @param {number} depthRun - Number of tables on bookend side (0-based, 0 = 1 table)
 * @returns {Object} {width, height} in feet
 */
function calculateUShapeDimensions(lengthRun, depthRun) {
    const tableLength = 6;    // 6 ft per table
    const tableDepth = 2.5;   // 2.5 ft table depth
    const spacer = 4;         // 4 ft spacing on each edge

    // Width calculation: tables + table depth + spacer on each side
    // From C#: (lengthRun * TableLength + spacer * 2 + TableDepth)
    // lengthRun represents number of 6ft tables, so (lengthRun + 1) total tables per side
    const width = (lengthRun + 1) * tableLength + tableDepth + (spacer * 2);

    // Height calculation: tables + spacer on each side
    // From C#: (depthRun * TableLength + 8.0)
    // depthRun represents number of 6ft tables, so (depthRun + 1) total tables
    // Note: C# uses 8.0 instead of spacer * 2, matching spacer * 2 = 8
    const height = (depthRun + 1) * tableLength + (spacer * 2);

    return { width, height };
}

/**
 * Find the largest U-shape layout that fits in a polygon
 *
 * @param {Array} polygon - Array of {x, y} points defining the polygon
 * @param {Object} options - Configuration options
 * @returns {Object} U-shape layout with corners, dimensions, area, angle, tables
 */
function findUShapeLayout(polygon, options = {}) {
    // Calculate polygon bounding box to determine dynamic limits
    const polyBounds = getPolygonBounds(polygon);
    const polyWidth = polyBounds.maxX - polyBounds.minX;
    const polyHeight = polyBounds.maxY - polyBounds.minY;

    // Calculate maximum possible runs based on polygon dimensions
    // For lengthRun: minimum width is 6 + 2.5 + 8 = 16.5ft, each additional run adds 6ft
    // For depthRun: minimum height is 6 + 8 = 14ft, each additional run adds 6ft
    const maxPossibleLengthRun = Math.ceil((Math.max(polyWidth, polyHeight) - 16.5) / 6);
    const maxPossibleDepthRun = Math.ceil((Math.max(polyWidth, polyHeight) - 14) / 6);

    const {
        maxLengthRun = maxPossibleLengthRun,  // Dynamic maximum based on polygon size
        maxDepthRun = maxPossibleDepthRun,    // Dynamic maximum based on polygon size
        angleSamples = 12,                     // Number of angles to test (0-180°)
        centroidSamples = 23,                  // Grid density for testing centroids (23×23 = 529 positions)
        debugMode = false
    } = options;

    if (debugMode) {
        console.log('[ushape] Starting U-shape layout search');
        console.log(`[ushape] Polygon vertices: ${polygon.length}`);
        console.log(`[ushape] Max lengthRun: ${maxLengthRun}, Max depthRun: ${maxDepthRun}`);
    }

    // Calculate polygon centroid and bounds
    const polygonCentroid = calculatePolygonCentroid(polygon);
    const bounds = getPolygonBounds(polygon);

    if (debugMode) {
        console.log(`[ushape] Polygon centroid: (${polygonCentroid.x.toFixed(2)}, ${polygonCentroid.y.toFixed(2)})`);
        console.log(`[ushape] Bounds: x=[${bounds.minX.toFixed(2)}, ${bounds.maxX.toFixed(2)}], y=[${bounds.minY.toFixed(2)}, ${bounds.maxY.toFixed(2)}]`);
    }

    let bestLayout = null;
    let bestArea = 0;

    // Test different angles (0° to 180°, since rectangles are symmetric)
    const angleStep = 180 / angleSamples;

    for (let angleIndex = 0; angleIndex < angleSamples; angleIndex++) {
        const angle = angleIndex * angleStep;

        // Generate test centroids in a grid pattern across the bounding box
        const xStep = (bounds.maxX - bounds.minX) / (centroidSamples - 1);
        const yStep = (bounds.maxY - bounds.minY) / (centroidSamples - 1);

        for (let xi = 0; xi < centroidSamples; xi++) {
            for (let yi = 0; yi < centroidSamples; yi++) {
                const testCentroid = {
                    x: bounds.minX + xi * xStep,
                    y: bounds.minY + yi * yStep
                };

                // For this angle and centroid, test all combinations of lengthRun and depthRun
                // Start from minimum (0, 0) and expand in both dimensions
                for (let lengthRun = 0; lengthRun <= maxLengthRun; lengthRun++) {
                    for (let depthRun = 0; depthRun <= maxDepthRun; depthRun++) {
                        const dims = calculateUShapeDimensions(lengthRun, depthRun);

                        // Try both orientations
                        // Orientation 1: width × height
                        const corners1 = calculateRectangleCorners(testCentroid, dims.width, dims.height, angle);
                        if (isRectangleInsidePolygon(corners1, polygon)) {
                            const area = dims.width * dims.height;
                            if (area > bestArea) {
                                bestArea = area;
                                bestLayout = {
                                    corners: corners1,
                                    width: dims.width,
                                    height: dims.height,
                                    area: area,
                                    angle: angle,
                                    centroid: testCentroid,
                                    lengthRun: lengthRun,
                                    depthRun: depthRun,
                                    tables: calculateUShapeTables(lengthRun + 1, depthRun + 1),
                                    orientation: 'width×height'
                                };
                            }
                        }

                        // Orientation 2: height × width (swap dimensions)
                        const corners2 = calculateRectangleCorners(testCentroid, dims.height, dims.width, angle);
                        if (isRectangleInsidePolygon(corners2, polygon)) {
                            const area = dims.width * dims.height;
                            if (area > bestArea) {
                                bestArea = area;
                                bestLayout = {
                                    corners: corners2,
                                    width: dims.height,
                                    height: dims.width,
                                    area: area,
                                    angle: angle,
                                    centroid: testCentroid,
                                    lengthRun: depthRun,  // Swapped
                                    depthRun: lengthRun,  // Swapped
                                    tables: calculateUShapeTables(lengthRun + 1, depthRun + 1),
                                    orientation: 'height×width'
                                };
                            }
                        }
                    }
                }
            }
        }
    }

    if (debugMode && bestLayout) {
        console.log('[ushape] Best layout found:');
        console.log(`  Area: ${bestLayout.area.toFixed(2)} sq ft`);
        console.log(`  Dimensions: ${bestLayout.width.toFixed(2)} × ${bestLayout.height.toFixed(2)} ft`);
        console.log(`  LengthRun: ${bestLayout.lengthRun}, DepthRun: ${bestLayout.depthRun}`);
        console.log(`  Tables: ${bestLayout.tables}`);
        console.log(`  Angle: ${bestLayout.angle.toFixed(1)}°`);
        console.log(`  Orientation: ${bestLayout.orientation}`);
    }

    return bestLayout;
}

/**
 * Wrapper for unified algorithm - provides backward compatibility
 * Uses the new findInscribedRectangle with U-shape constraints
 *
 * @param {Array} polygon - Array of {x, y} points defining the polygon
 * @param {Object} options - Configuration options (legacy format)
 * @returns {Object} U-shape layout
 */
function findUShapeLayoutUnified(polygon, options = {}) {
    // Load unified algorithm if available
    if (typeof findInscribedRectangle === 'undefined') {
        // Fall back to legacy implementation if unified algorithm not loaded
        return findUShapeLayout(polygon, options);
    }

    const {
        maxLengthRun,
        maxDepthRun,
        angleSamples = 12,
        centroidSamples = 25,
        maxTime = null,
        debugMode = false
    } = options;

    // Configure U-shape constraints using unified algorithm
    const unifiedOptions = {
        width: {
            mode: 'discrete',
            base: 16.5,         // Base: 6ft + 2.5ft + 2×4ft = 16.5ft
            increment: 6,       // Each lengthRun adds 6ft
            minIncrements: 0,   // Start with base (0 additional runs)
            maxIncrements: maxLengthRun  // Optional limit
        },
        height: {
            mode: 'discrete',
            base: 14,           // Base: 6ft + 2×4ft = 14ft
            increment: 6,       // Each depthRun adds 6ft
            minIncrements: 0,   // Start with base (0 additional runs)
            maxIncrements: maxDepthRun   // Optional limit
        },
        angleSamples: angleSamples,
        centroidSamples: centroidSamples,
        maxTime: maxTime,
        debugMode: debugMode
    };

    const result = findInscribedRectangle(polygon, unifiedOptions);

    if (!result) {
        return null;
    }

    // Convert unified result to U-shape format
    // Calculate lengthRun and depthRun from dimensions
    const lengthRun = Math.round((result.width - 16.5) / 6);
    const depthRun = Math.round((result.height - 14) / 6);

    return {
        corners: result.corners,
        width: result.width,
        height: result.height,
        area: result.area,
        angle: result.angle,
        centroid: result.centroid,
        lengthRun: lengthRun,
        depthRun: depthRun,
        tables: calculateUShapeTables(lengthRun + 1, depthRun + 1)
    };
}

// Export for Node.js testing
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        findUShapeLayout,
        findUShapeLayoutUnified,
        calculateUShapeTables,
        calculateUShapeDimensions,
        isPointInPolygon,
        isRectangleInsidePolygon,
        calculateRectangleCorners,
        calculatePolygonCentroid
    };
}
