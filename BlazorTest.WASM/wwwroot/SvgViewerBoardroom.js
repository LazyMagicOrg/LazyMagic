/**
 * Boardroom Layout Inscribed Rectangle Algorithm
 *
 * Finds the largest boardroom-style layout that fits within a polygon.
 *
 * Boardroom constraints:
 * - Fixed width: 13 ft (2 tables back-to-back @ 2.5ft each + 4ft spacing on each side)
 * - Variable length: 14 ft + (n × 6 ft) where n = number of additional table sets
 *   - 1 set (2 tables): 14 ft × 13 ft
 *   - 2 sets (4 tables): 20 ft × 13 ft
 *   - 3 sets (6 tables): 26 ft × 13 ft
 *   - etc.
 * - Can be rotated to any angle
 *
 * Returns the boardroom layout with maximum area (longest valid configuration)
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
 * Find the largest boardroom layout that fits in a polygon
 *
 * @param {Array} polygon - Array of {x, y} points defining the polygon
 * @param {Object} options - Configuration options
 * @returns {Object} Boardroom layout with corners, dimensions, area, angle, tables, sets
 */
function findBoardroomLayout(polygon, options = {}) {
    const {
        boardroomWidth = 13,        // Fixed width in feet
        minLength = 14,             // Minimum length (1 set)
        lengthIncrement = 6,        // Length increment per additional set
        angleSamples = 24,          // Number of angles to test (0-180°)
        centroidSamples = 9,        // Grid density for testing centroids (3x3 = 9 points)
        debugMode = false
    } = options;

    if (debugMode) {
        console.log('[boardroom] Starting boardroom layout search');
        console.log(`[boardroom] Polygon vertices: ${polygon.length}`);
        console.log(`[boardroom] Width: ${boardroomWidth} ft, Min length: ${minLength} ft, Increment: ${lengthIncrement} ft`);
    }

    // Calculate polygon centroid and bounds
    const polygonCentroid = calculatePolygonCentroid(polygon);
    const bounds = getPolygonBounds(polygon);

    if (debugMode) {
        console.log(`[boardroom] Polygon centroid: (${polygonCentroid.x.toFixed(2)}, ${polygonCentroid.y.toFixed(2)})`);
        console.log(`[boardroom] Bounds: x=[${bounds.minX.toFixed(2)}, ${bounds.maxX.toFixed(2)}], y=[${bounds.minY.toFixed(2)}, ${bounds.maxY.toFixed(2)}]`);
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

                // For this angle and centroid, find the longest boardroom layout that fits
                // Start with minimum length and increment until it doesn't fit
                let currentLength = minLength;
                let lastValidLayout = null;
                let setCount = 1;

                while (true) {
                    // Try boardroom in both orientations (width×length and length×width)
                    // since we're testing 0-180°, we need to try both to cover all possibilities

                    // Orientation 1: boardroomWidth × currentLength
                    const corners1 = calculateRectangleCorners(testCentroid, boardroomWidth, currentLength, angle);
                    if (isRectangleInsidePolygon(corners1, polygon)) {
                        const area = boardroomWidth * currentLength;
                        lastValidLayout = {
                            corners: corners1,
                            width: boardroomWidth,
                            height: currentLength,
                            area: area,
                            angle: angle,
                            centroid: testCentroid,
                            sets: setCount,
                            tables: setCount * 2,
                            orientation: 'width×length'
                        };
                    }

                    // Orientation 2: currentLength × boardroomWidth
                    const corners2 = calculateRectangleCorners(testCentroid, currentLength, boardroomWidth, angle);
                    if (isRectangleInsidePolygon(corners2, polygon)) {
                        const area = currentLength * boardroomWidth;
                        // Only use this if it's better than orientation 1
                        if (!lastValidLayout || area > lastValidLayout.area) {
                            lastValidLayout = {
                                corners: corners2,
                                width: currentLength,
                                height: boardroomWidth,
                                area: area,
                                angle: angle,
                                centroid: testCentroid,
                                sets: setCount,
                                tables: setCount * 2,
                                orientation: 'length×width'
                            };
                        }
                    }

                    // If neither orientation fits, we've found the maximum for this angle/centroid
                    if (!lastValidLayout) {
                        break;
                    }

                    // Try next set size
                    currentLength += lengthIncrement;
                    setCount++;

                    // Safety limit: don't try more than 100 sets
                    if (setCount > 100) {
                        break;
                    }
                }

                // Check if this is the best layout found so far
                if (lastValidLayout && lastValidLayout.area > bestArea) {
                    bestArea = lastValidLayout.area;
                    bestLayout = lastValidLayout;

                    if (debugMode) {
                        console.log(`[boardroom] New best: ${lastValidLayout.sets} sets (${lastValidLayout.tables} tables), ` +
                                  `${lastValidLayout.width.toFixed(1)}×${lastValidLayout.height.toFixed(1)} ft, ` +
                                  `area: ${lastValidLayout.area.toFixed(1)} sq ft, ` +
                                  `angle: ${angle.toFixed(1)}°`);
                    }
                }
            }
        }
    }

    if (!bestLayout) {
        if (debugMode) {
            console.log('[boardroom] No valid boardroom layout found');
        }
        return null;
    }

    // Add type identifier
    bestLayout.type = 'boardroom';

    if (debugMode) {
        console.log(`[boardroom] Final result: ${bestLayout.sets} sets (${bestLayout.tables} tables)`);
        console.log(`[boardroom] Dimensions: ${bestLayout.width.toFixed(1)}×${bestLayout.height.toFixed(1)} ft`);
        console.log(`[boardroom] Area: ${bestLayout.area.toFixed(1)} sq ft`);
        console.log(`[boardroom] Angle: ${bestLayout.angle.toFixed(1)}°`);
    }

    return bestLayout;
}

// Export for Node.js
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        findBoardroomLayout,
        isPointInPolygon,
        isRectangleInsidePolygon,
        segmentsIntersect,
        calculateRectangleCorners,
        calculatePolygonCentroid
    };
}
