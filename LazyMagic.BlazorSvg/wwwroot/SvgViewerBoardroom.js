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
 * Helper function: Given an angle and centroid, find the longest boardroom that fits
 * Uses incremental expansion to find maximum length at this position
 */
function findMaxBoardroomAtPosition(polygon, centroid, angle, boardroomWidth, minLength, lengthIncrement) {
    let currentLength = minLength;
    let bestLayout = null;
    let setCount = 1;

    while (setCount <= 100) {  // Safety limit
        let foundValidLayout = false;

        // Try orientation 1: boardroomWidth × currentLength
        const corners1 = calculateRectangleCorners(centroid, boardroomWidth, currentLength, angle);
        if (isRectangleInsidePolygon(corners1, polygon)) {
            const area = boardroomWidth * currentLength;
            bestLayout = {
                corners: corners1,
                width: boardroomWidth,
                height: currentLength,
                area: area,
                angle: angle,
                centroid: centroid,
                sets: setCount,
                tables: setCount * 2,
                orientation: 'width×length'
            };
            foundValidLayout = true;
        }

        // Try orientation 2: currentLength × boardroomWidth
        const corners2 = calculateRectangleCorners(centroid, currentLength, boardroomWidth, angle);
        if (isRectangleInsidePolygon(corners2, polygon)) {
            const area = currentLength * boardroomWidth;
            if (!bestLayout || area > bestLayout.area) {
                bestLayout = {
                    corners: corners2,
                    width: currentLength,
                    height: boardroomWidth,
                    area: area,
                    angle: angle,
                    centroid: centroid,
                    sets: setCount,
                    tables: setCount * 2,
                    orientation: 'length×width'
                };
                foundValidLayout = true;
            }
        }

        // If neither orientation fits, we've reached the maximum
        if (!foundValidLayout) {
            break;
        }

        // Try next increment
        currentLength += lengthIncrement;
        setCount++;
    }

    return bestLayout;
}

/**
 * Run custom dedicated boardroom algorithm
 * Searches across multiple angles and centroids specifically for boardroom constraints
 */
function runDedicatedBoardroom(polygon, options) {
    const { boardroomWidth, minLength, lengthIncrement, angleSamples, centroidSamples, debugMode } = options;

    const startTime = performance.now();

    // Get polygon centroid and bounds
    const centroid = calculatePolygonCentroid(polygon);
    const bounds = getPolygonBounds(polygon);
    const width = bounds.maxX - bounds.minX;
    const height = bounds.maxY - bounds.minY;

    let bestLayout = null;

    // Sample angles from 0 to 180 degrees
    const angleStep = 180 / angleSamples;

    for (let angleIndex = 0; angleIndex < angleSamples; angleIndex++) {
        const angle = angleIndex * angleStep;

        // Test at polygon centroid
        const layoutAtCentroid = findMaxBoardroomAtPosition(
            polygon,
            centroid,
            angle,
            boardroomWidth,
            minLength,
            lengthIncrement
        );

        if (layoutAtCentroid && (!bestLayout || layoutAtCentroid.area > bestLayout.area)) {
            bestLayout = layoutAtCentroid;
        }

        // Sample additional centroids in a grid pattern
        const gridStep = Math.min(width, height) / (centroidSamples + 1);

        for (let dx = -centroidSamples/2; dx <= centroidSamples/2; dx++) {
            for (let dy = -centroidSamples/2; dy <= centroidSamples/2; dy++) {
                if (dx === 0 && dy === 0) continue; // Already tested centroid

                const testCentroid = {
                    x: centroid.x + dx * gridStep,
                    y: centroid.y + dy * gridStep
                };

                // Only test if centroid is inside polygon
                if (!isPointInPolygon(testCentroid, polygon)) {
                    continue;
                }

                const layout = findMaxBoardroomAtPosition(
                    polygon,
                    testCentroid,
                    angle,
                    boardroomWidth,
                    minLength,
                    lengthIncrement
                );

                if (layout && (!bestLayout || layout.area > bestLayout.area)) {
                    bestLayout = layout;
                }
            }
        }
    }

    if (bestLayout) {
        bestLayout.type = 'boardroom';
        bestLayout.algorithm = 'Dedicated';
        bestLayout.computationTime = performance.now() - startTime;
    }

    if (debugMode && bestLayout) {
        console.log(`[boardroom-Dedicated] Found ${bestLayout.sets} sets (${bestLayout.tables} tables), area ${bestLayout.area.toFixed(1)} sq ft, angle ${bestLayout.angle.toFixed(1)}°`);
    }

    return bestLayout;
}

/**
 * Run Boundary-Based algorithm adapted for boardroom constraints
 */
function runBoundaryBasedBoardroom(polygon, options) {
    const { boardroomWidth, minLength, lengthIncrement, debugMode } = options;

    // Check if BB algorithm is available
    if (typeof boundaryBasedInscribedRectangle === 'undefined') {
        if (debugMode) {
            console.log('[boardroom-BB] Boundary-Based algorithm not available');
        }
        return null;
    }

    const startTime = performance.now();

    // Call BB to get dominant angles and suggested test positions
    // BB returns a rectangle, but we'll use its angle-finding logic
    const bbResult = boundaryBasedInscribedRectangle(polygon, {
        debugMode: false,  // Suppress BB's own logging
        pathCount: 1  // Treat as simple polygon
    });

    if (!bbResult) {
        if (debugMode) {
            console.log('[boardroom-BB] No result from Boundary-Based');
        }
        return null;
    }

    // Test boardroom at the angle BB found optimal
    const testAngle = bbResult.angle || 0;
    const testCentroid = bbResult.centroid || calculatePolygonCentroid(polygon);

    const layout = findMaxBoardroomAtPosition(
        polygon,
        testCentroid,
        testAngle,
        boardroomWidth,
        minLength,
        lengthIncrement
    );

    if (layout) {
        layout.type = 'boardroom';
        layout.algorithm = 'Boundary-Based';
        layout.computationTime = performance.now() - startTime;
    }

    return layout;
}

/**
 * Run Optimized algorithm adapted for boardroom constraints
 */
function runOptimizedBoardroom(polygon, options) {
    const { boardroomWidth, minLength, lengthIncrement, debugMode } = options;

    // Check if Optimized algorithm is available
    if (typeof fastInscribedRectangle === 'undefined') {
        if (debugMode) {
            console.log('[boardroom-Opt] Optimized algorithm not available');
        }
        return null;
    }

    const startTime = performance.now();

    // Call Optimized to get its best angle and centroid
    const optResult = fastInscribedRectangle(polygon, {
        debugMode: false  // Suppress Opt's own logging
    });

    if (!optResult) {
        if (debugMode) {
            console.log('[boardroom-Opt] No result from Optimized');
        }
        return null;
    }

    // Test boardroom at the angle/centroid Optimized found
    const testAngle = optResult.angle || 0;
    const testCentroid = optResult.centroid || calculatePolygonCentroid(polygon);

    const layout = findMaxBoardroomAtPosition(
        polygon,
        testCentroid,
        testAngle,
        boardroomWidth,
        minLength,
        lengthIncrement
    );

    if (layout) {
        layout.type = 'boardroom';
        layout.algorithm = 'Optimized';
        layout.computationTime = performance.now() - startTime;
    }

    return layout;
}

/**
 * Legacy grid-based boardroom search (fallback if BB/Opt not available)
 */
function runLegacyBoardroom(polygon, options) {
    const {
        boardroomWidth = 13,
        minLength = 14,
        lengthIncrement = 6,
        angleSamples = 24,
        centroidSamples = 9,
        debugMode = false
    } = options;

    const startTime = performance.now();
    const bounds = getPolygonBounds(polygon);

    let bestLayout = null;
    let bestArea = 0;

    // Test different angles (0° to 180°)
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

                const layout = findMaxBoardroomAtPosition(
                    polygon,
                    testCentroid,
                    angle,
                    boardroomWidth,
                    minLength,
                    lengthIncrement
                );

                if (layout && layout.area > bestArea) {
                    bestArea = layout.area;
                    bestLayout = layout;
                }
            }
        }
    }

    if (bestLayout) {
        bestLayout.type = 'boardroom';
        bestLayout.algorithm = 'Legacy';
        bestLayout.computationTime = performance.now() - startTime;
    }

    return bestLayout;
}

/**
 * Find the largest boardroom layout that fits in a polygon
 *
 * Runs both Boundary-Based and Optimized algorithms and picks the best result
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
        angleSamples = 24,          // Number of angles for legacy fallback
        centroidSamples = 9,        // Grid density for legacy fallback
        debugMode = false
    } = options;

    if (debugMode) {
        console.log('[boardroom] Starting dual-algorithm boardroom search');
        console.log(`[boardroom] Polygon vertices: ${polygon.length}`);
        console.log(`[boardroom] Width: ${boardroomWidth} ft, Min length: ${minLength} ft, Increment: ${lengthIncrement} ft`);
    }

    const boardroomOptions = {
        boardroomWidth,
        minLength,
        lengthIncrement,
        angleSamples,
        centroidSamples,
        debugMode
    };

    // Run all three algorithms
    const bbResult = runBoundaryBasedBoardroom(polygon, boardroomOptions);
    const optResult = runOptimizedBoardroom(polygon, boardroomOptions);
    const dedicatedResult = runDedicatedBoardroom(polygon, boardroomOptions);

    // Compare results from all algorithms
    let bestLayout = null;
    let winnerName = null;
    const results = [];

    if (bbResult) results.push({ name: 'Boundary-Based', result: bbResult });
    if (optResult) results.push({ name: 'Optimized', result: optResult });
    if (dedicatedResult) results.push({ name: 'Dedicated', result: dedicatedResult });

    if (results.length > 0) {
        // Find the best result
        bestLayout = results[0].result;
        winnerName = results[0].name;

        for (const { name, result } of results) {
            if (result.area > bestLayout.area) {
                bestLayout = result;
                winnerName = name;
            }
        }

        if (debugMode) {
            for (const { name, result } of results) {
                console.log(`[boardroom] ${name}: ${result.area.toFixed(1)} sq ft (${result.sets} sets) in ${result.computationTime.toFixed(1)}ms`);
            }
            console.log(`[boardroom] Winner: ${winnerName} with ${bestLayout.area.toFixed(1)} sq ft`);
        }
    } else {
        // All algorithms failed - fall back to legacy
        if (debugMode) {
            console.log('[boardroom] All algorithms failed - falling back to legacy grid search');
        }
        bestLayout = runLegacyBoardroom(polygon, boardroomOptions);
        winnerName = 'Legacy';
    }

    if (!bestLayout) {
        if (debugMode) {
            console.log('[boardroom] No valid boardroom layout found');
        }
        return null;
    }

    // Add winner metadata
    bestLayout.winningAlgorithm = winnerName;

    if (debugMode) {
        console.log(`[boardroom] Final result (${winnerName}): ${bestLayout.sets} sets (${bestLayout.tables} tables)`);
        console.log(`[boardroom] Dimensions: ${bestLayout.width.toFixed(1)}×${bestLayout.height.toFixed(1)} ft`);
        console.log(`[boardroom] Area: ${bestLayout.area.toFixed(1)} sq ft`);
        console.log(`[boardroom] Angle: ${bestLayout.angle.toFixed(1)}°`);
        console.log(`[boardroom] Computation time: ${bestLayout.computationTime.toFixed(1)}ms`);
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
