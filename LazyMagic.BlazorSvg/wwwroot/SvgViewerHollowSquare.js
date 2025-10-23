/**
 * Hollow Square Layout Inscribed Rectangle Algorithm
 *
 * Finds the largest hollow square-style layout that fits within a polygon.
 *
 * Hollow Square constraints:
 * - Minimum dimensions: 14 ft × 19 ft (4 tables in hollow square configuration)
 *   - Base: 2 tables back-to-back (6ft wide × 2.5ft each = 6ft deep including 1ft spacing)
 *   - Plus 2 bookend tables (6ft × 2.5ft each)
 *   - Plus 4ft spacing on all sides
 *   - Result: (6ft + 8ft spacing) × (6ft + 1ft + 2.5ft×2 + 8ft spacing) = 14ft × 19ft
 * - Both dimensions can expand independently in 6ft increments:
 *   - Width: 14ft, 20ft, 26ft, 32ft, ...  (adds 6ft per lengthRun increment)
 *   - Height: 19ft, 25ft, 31ft, 37ft, ... (adds 6ft per depthRun increment)
 * - Can be rotated to any angle
 *
 * Returns the hollow square layout with maximum area
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
 * Calculate number of tables in a hollow square configuration
 *
 * @param {number} lengthRun - Number of tables on the length sides (0-based)
 * @param {number} depthRun - Number of tables on the depth sides (0-based)
 * @returns {number} Total number of tables
 */
function calculateHollowSquareTables(lengthRun, depthRun) {
    // lengthRun tables on each of 2 sides + depthRun tables on each of 2 sides
    return (lengthRun * 2) + (depthRun * 2);
}

/**
 * Calculate dimensions from table runs
 *
 * @param {number} lengthRun - Number of tables on length sides (0-based, 0 = 1 table)
 * @param {number} depthRun - Number of tables on depth sides (0-based, 0 = 1 table)
 * @returns {Object} {width, height} in feet
 */
function calculateHollowSquareDimensions(lengthRun, depthRun) {
    const tableLength = 6;    // 6 ft per table
    const tableDepth = 2.5;   // 2.5 ft table depth
    const spacer = 4;         // 4 ft spacing on each edge

    // Width calculation: base width (1 table + 2 table depths + spacer on each side) + additional tables
    // Base: 6ft + 2×2.5ft + 2×4ft = 6 + 5 + 8 = 19ft
    // Each additional lengthRun adds 6ft
    const width = (lengthRun + 1) * tableLength + (tableDepth * 2) + (spacer * 2);

    // Height calculation: base height (1 table + spacer on each side) + additional tables
    // Base: 6ft + 2×4ft = 14ft
    // Each additional depthRun adds 6ft
    const height = (depthRun + 1) * tableLength + (spacer * 2);

    return { width, height };
}

/**
 * Find the largest hollow square layout that fits in a polygon
 *
 * @param {Array} polygon - Array of {x, y} points defining the polygon
 * @param {Object} options - Configuration options
 * @returns {Object} Hollow square layout with corners, dimensions, area, angle, tables
 */
function findHollowSquareLayout(polygon, options = {}) {
    // Calculate polygon bounding box to determine dynamic limits
    const polyBounds = getPolygonBounds(polygon);
    const polyWidth = polyBounds.maxX - polyBounds.minX;
    const polyHeight = polyBounds.maxY - polyBounds.minY;

    // Calculate maximum possible runs based on polygon dimensions
    // For lengthRun: base width is 19ft, each additional run adds 6ft
    // For depthRun: base height is 14ft, each additional run adds 6ft
    const maxPossibleLengthRun = Math.ceil((Math.max(polyWidth, polyHeight) - 19) / 6) + 5; // +5 for safety margin
    const maxPossibleDepthRun = Math.ceil((Math.max(polyWidth, polyHeight) - 14) / 6) + 5;  // +5 for safety margin

    const {
        maxLengthRun = maxPossibleLengthRun,  // Dynamic maximum based on polygon size
        maxDepthRun = maxPossibleDepthRun,    // Dynamic maximum based on polygon size
        angleSamples = 12,                     // Number of angles to test (0-180°) - reduced from 24 for performance
        centroidSamples = 5,                   // Grid density for testing centroids (reduced from 9 for performance)
        debugMode = false
    } = options;

    if (debugMode) {
        console.log('[hollowsquare] Starting hollow square layout search');
        console.log(`[hollowsquare] Polygon vertices: ${polygon.length}`);
        console.log(`[hollowsquare] Max lengthRun: ${maxLengthRun}, Max depthRun: ${maxDepthRun}`);
    }

    // Calculate polygon centroid and bounds
    const polygonCentroid = calculatePolygonCentroid(polygon);
    const bounds = getPolygonBounds(polygon);

    if (debugMode) {
        console.log(`[hollowsquare] Polygon centroid: (${polygonCentroid.x.toFixed(2)}, ${polygonCentroid.y.toFixed(2)})`);
        console.log(`[hollowsquare] Bounds: x=[${bounds.minX.toFixed(2)}, ${bounds.maxX.toFixed(2)}], y=[${bounds.minY.toFixed(2)}, ${bounds.maxY.toFixed(2)}]`);
    }

    let bestLayout = null;
    let bestArea = 0;

    // Test different angles (0° to 180°, since rectangles are symmetric)
    const angleStep = 180 / angleSamples;

    for (let angleIndex = 0; angleIndex < angleSamples; angleIndex++) {
        const angle = angleIndex * angleStep;

        // For each angle, test different centroid positions in a grid
        const xStep = (bounds.maxX - bounds.minX) / (centroidSamples + 1);
        const yStep = (bounds.maxY - bounds.minY) / (centroidSamples + 1);

        for (let xIndex = 1; xIndex <= centroidSamples; xIndex++) {
            for (let yIndex = 1; yIndex <= centroidSamples; yIndex++) {
                const testCentroid = {
                    x: bounds.minX + xIndex * xStep,
                    y: bounds.minY + yIndex * yStep
                };

                // For this angle and centroid, test all combinations of lengthRun and depthRun
                // Start from minimum (0, 0) and expand in both dimensions
                for (let lengthRun = 0; lengthRun <= maxLengthRun; lengthRun++) {
                    for (let depthRun = 0; depthRun <= maxDepthRun; depthRun++) {
                        const dims = calculateHollowSquareDimensions(lengthRun, depthRun);

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
                                    tables: calculateHollowSquareTables(lengthRun + 1, depthRun + 1),
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
                                    tables: calculateHollowSquareTables(lengthRun + 1, depthRun + 1),
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
        console.log('[hollowsquare] Best layout found:');
        console.log(`  Area: ${bestLayout.area.toFixed(2)} sq ft`);
        console.log(`  Dimensions: ${bestLayout.width.toFixed(2)} × ${bestLayout.height.toFixed(2)} ft`);
        console.log(`  LengthRun: ${bestLayout.lengthRun}, DepthRun: ${bestLayout.depthRun}`);
        console.log(`  Tables: ${bestLayout.tables}`);
        console.log(`  Angle: ${bestLayout.angle.toFixed(1)}°`);
        console.log(`  Orientation: ${bestLayout.orientation}`);
    }

    return bestLayout;
}

// Export for Node.js testing
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        findHollowSquareLayout,
        calculateHollowSquareTables,
        calculateHollowSquareDimensions,
        isPointInPolygon,
        isRectangleInsidePolygon,
        calculateRectangleCorners,
        calculatePolygonCentroid
    };
}
