/**
 * Unified Inscribed Rectangle Algorithm
 *
 * Finds the largest rectangle that fits within a polygon with configurable constraints.
 *
 * Supports three dimension modes for width and height independently:
 * - Fixed: Single unchanging dimension (e.g., boardroom width = 13 ft)
 * - Discrete: Base + integer increments (e.g., 14 ft base, +6 ft per increment)
 * - Continuous: Any value within range, sampled or via aspect ratios
 *
 * @example
 * // Boardroom layout (fixed width, discrete length)
 * findInscribedRectangle(polygon, {
 *   width: { mode: 'fixed', value: 13 },
 *   height: { mode: 'discrete', base: 14, increment: 6, minIncrements: 0 }
 * })
 *
 * @example
 * // Hollow square (discrete width and height)
 * findInscribedRectangle(polygon, {
 *   width: { mode: 'discrete', base: 19, increment: 6, minIncrements: 0 },
 *   height: { mode: 'discrete', base: 14, increment: 6, minIncrements: 0 }
 * })
 *
 * @example
 * // Max rectangle (continuous both dimensions)
 * findInscribedRectangle(polygon, {
 *   width: { mode: 'continuous', min: 1, max: 500, searchSteps: 20 },
 *   height: { mode: 'continuous', min: 1, max: 500, searchSteps: 20 },
 *   aspectRatios: [0.5, 1.0, 1.5, 2.0]
 * })
 */

// ============================================================================
// SHARED UTILITY FUNCTIONS
// ============================================================================

/**
 * Point-in-polygon test using winding number algorithm
 * @param {Object} point - {x, y} coordinate
 * @param {Array} polygon - Array of {x, y} points
 * @returns {boolean} True if point is inside polygon
 */
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

/**
 * Check if two line segments intersect
 * @param {Object} p1 - First point of segment 1
 * @param {Object} p2 - Second point of segment 1
 * @param {Object} p3 - First point of segment 2
 * @param {Object} p4 - Second point of segment 2
 * @returns {boolean} True if segments intersect
 */
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

/**
 * Check if all corners of a rectangle are inside the polygon
 * @param {Array} corners - Array of 4 {x, y} corner points
 * @param {Array} polygon - Array of {x, y} polygon vertices
 * @returns {boolean} True if rectangle is fully contained
 */
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

/**
 * Calculate rectangle corners given centroid, dimensions, and angle
 * @param {Object} centroid - {x, y} center point
 * @param {number} width - Rectangle width
 * @param {number} height - Rectangle height
 * @param {number} angleDegrees - Rotation angle in degrees
 * @returns {Array} Array of 4 corner points [BL, BR, TR, TL]
 */
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

/**
 * Calculate polygon centroid using signed area method
 * @param {Array} polygon - Array of {x, y} points
 * @returns {Object} {x, y} centroid point
 */
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

/**
 * Get bounding box of polygon
 * @param {Array} polygon - Array of {x, y} points
 * @returns {Object} {minX, maxX, minY, maxY}
 */
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

// ============================================================================
// DIMENSION GENERATOR FUNCTIONS
// ============================================================================

/**
 * Generate dimension values based on constraint mode
 * @param {Object} constraint - Dimension constraint configuration
 * @param {number} polygonSize - Maximum polygon dimension for dynamic limits
 * @returns {Array} Array of dimension values to test
 */
function generateDimensionValues(constraint, polygonSize) {
    if (constraint.mode === 'fixed') {
        return [constraint.value];
    }

    if (constraint.mode === 'discrete') {
        const base = constraint.base;
        const increment = constraint.increment;
        const minIncrements = constraint.minIncrements || 0;

        // Calculate max increments from polygon size if not specified
        let maxIncrements = constraint.maxIncrements;
        if (maxIncrements === undefined) {
            maxIncrements = Math.ceil((polygonSize - base) / increment);
        }

        const values = [];
        for (let i = minIncrements; i <= maxIncrements; i++) {
            values.push(base + (i * increment));
        }
        return values;
    }

    if (constraint.mode === 'continuous') {
        const min = constraint.min;
        const max = Math.min(constraint.max, polygonSize);
        const steps = constraint.searchSteps || 20;

        const values = [];
        const stepSize = (max - min) / (steps - 1);
        for (let i = 0; i < steps; i++) {
            values.push(min + (i * stepSize));
        }
        return values;
    }

    throw new Error(`Unknown dimension mode: ${constraint.mode}`);
}

/**
 * Generate dimension pairs for testing
 * If aspect ratios are provided, uses those for continuous mode
 * Otherwise generates all combinations of width/height values
 *
 * @param {Array} widthValues - Array of width values
 * @param {Array} heightValues - Array of height values
 * @param {Array} aspectRatios - Optional aspect ratios for continuous mode
 * @param {Object} widthConstraint - Width constraint config
 * @param {Object} heightConstraint - Height constraint config
 * @returns {Array} Array of {width, height} pairs
 */
function generateDimensionPairs(widthValues, heightValues, aspectRatios, widthConstraint, heightConstraint) {
    const pairs = [];

    // If both dimensions are continuous and aspect ratios provided, use aspect ratio sampling
    if (widthConstraint.mode === 'continuous' &&
        heightConstraint.mode === 'continuous' &&
        aspectRatios && aspectRatios.length > 0) {

        // For each aspect ratio, sample widths and calculate heights
        for (const ratio of aspectRatios) {
            for (const width of widthValues) {
                const height = width / ratio;
                // Only include if height is within valid range
                if (height >= heightConstraint.min && height <= heightConstraint.max) {
                    pairs.push({ width, height });
                }
            }
        }
    } else {
        // Standard combinatorial approach
        for (const width of widthValues) {
            for (const height of heightValues) {
                pairs.push({ width, height });
            }
        }
    }

    return pairs;
}

// ============================================================================
// MAIN SEARCH FUNCTION
// ============================================================================

/**
 * Find the largest inscribed rectangle within a polygon
 *
 * @param {Array} polygon - Array of {x, y} points defining the polygon
 * @param {Object} options - Configuration options
 * @param {Object} options.width - Width constraint
 * @param {string} options.width.mode - 'fixed' | 'discrete' | 'continuous'
 * @param {number} options.width.value - For fixed mode
 * @param {number} options.width.base - For discrete mode (base dimension)
 * @param {number} options.width.increment - For discrete mode (increment per step)
 * @param {number} options.width.minIncrements - For discrete mode (minimum increments, default 0)
 * @param {number} options.width.maxIncrements - For discrete mode (optional, computed from polygon)
 * @param {number} options.width.min - For continuous mode
 * @param {number} options.width.max - For continuous mode
 * @param {number} options.width.searchSteps - For continuous mode (default 20)
 * @param {Object} options.height - Height constraint (same structure as width)
 * @param {Array} options.aspectRatios - Optional aspect ratios for continuous mode
 * @param {number} options.angleSamples - Number of angles to test (default 12)
 * @param {number} options.centroidSamples - Grid density for centroids (default 25)
 * @param {boolean} options.debugMode - Enable debug logging (default false)
 * @returns {Object|null} Best layout or null if none found
 */
function findInscribedRectangle(polygon, options = {}) {
    const {
        width: widthConstraint,
        height: heightConstraint,
        aspectRatios = null,
        angleSamples = 12,
        centroidSamples = 25,
        maxTime = null,
        debugMode = false
    } = options;

    if (!widthConstraint || !heightConstraint) {
        throw new Error('Both width and height constraints must be specified');
    }

    if (debugMode) {
        console.log('[inscribed-rect] Starting inscribed rectangle search');
        console.log(`[inscribed-rect] Width mode: ${widthConstraint.mode}`);
        console.log(`[inscribed-rect] Height mode: ${heightConstraint.mode}`);
        console.log(`[inscribed-rect] Polygon vertices: ${polygon.length}`);
    }

    // Calculate polygon bounds for dynamic limits
    const bounds = getPolygonBounds(polygon);
    const polyWidth = bounds.maxX - bounds.minX;
    const polyHeight = bounds.maxY - bounds.minY;
    const maxPolyDim = Math.max(polyWidth, polyHeight);

    if (debugMode) {
        console.log(`[inscribed-rect] Polygon bounds: ${polyWidth.toFixed(1)} × ${polyHeight.toFixed(1)} ft`);
    }

    // Generate dimension values to test
    const widthValues = generateDimensionValues(widthConstraint, maxPolyDim);
    const heightValues = generateDimensionValues(heightConstraint, maxPolyDim);

    if (debugMode) {
        console.log(`[inscribed-rect] Testing ${widthValues.length} width values × ${heightValues.length} height values`);
    }

    // Generate dimension pairs (either combinatorial or aspect-ratio based)
    const dimensionPairs = generateDimensionPairs(
        widthValues,
        heightValues,
        aspectRatios,
        widthConstraint,
        heightConstraint
    );

    if (debugMode) {
        console.log(`[inscribed-rect] Generated ${dimensionPairs.length} dimension pairs to test`);
    }

    // Calculate centroid grid
    const xStep = (bounds.maxX - bounds.minX) / (centroidSamples - 1);
    const yStep = (bounds.maxY - bounds.minY) / (centroidSamples - 1);

    let bestLayout = null;
    let bestArea = 0;

    // Track start time for maxTime enforcement
    const startTime = maxTime ? performance.now() : null;

    // Main search loop: angles × centroids × dimensions
    const angleStep = 180 / angleSamples;

    outerLoop:
    for (let angleIndex = 0; angleIndex < angleSamples; angleIndex++) {
        const angle = angleIndex * angleStep;

        for (let xi = 0; xi < centroidSamples; xi++) {
            for (let yi = 0; yi < centroidSamples; yi++) {
                const testCentroid = {
                    x: bounds.minX + xi * xStep,
                    y: bounds.minY + yi * yStep
                };

                // Check if we've exceeded maxTime
                if (maxTime && (performance.now() - startTime) > maxTime) {
                    if (debugMode) {
                        console.log(`[inscribed-rect] ⏱️ Timeout after ${maxTime}ms - returning best result found so far`);
                    }
                    break outerLoop;
                }

                // Test all dimension pairs at this centroid and angle
                for (const dims of dimensionPairs) {
                    const corners = calculateRectangleCorners(
                        testCentroid,
                        dims.width,
                        dims.height,
                        angle
                    );

                    if (isRectangleInsidePolygon(corners, polygon)) {
                        const area = dims.width * dims.height;
                        if (area > bestArea) {
                            bestArea = area;
                            bestLayout = {
                                corners: corners,
                                width: dims.width,
                                height: dims.height,
                                area: area,
                                angle: angle,
                                centroid: testCentroid
                            };

                            if (debugMode) {
                                console.log(`[inscribed-rect] New best: ${dims.width.toFixed(1)}×${dims.height.toFixed(1)} ft, ` +
                                          `area: ${area.toFixed(1)} sq ft, angle: ${angle.toFixed(1)}°`);
                            }
                        }
                    }
                }
            }
        }
    }

    if (debugMode) {
        if (bestLayout) {
            console.log('[inscribed-rect] Search complete');
            console.log(`[inscribed-rect] Best: ${bestLayout.width.toFixed(1)}×${bestLayout.height.toFixed(1)} ft`);
            console.log(`[inscribed-rect] Area: ${bestLayout.area.toFixed(1)} sq ft`);
            console.log(`[inscribed-rect] Angle: ${bestLayout.angle.toFixed(1)}°`);
        } else {
            console.log('[inscribed-rect] No valid rectangle found');
        }
    }

    return bestLayout;
}

// ============================================================================
// HYBRID APPROACH (Boundary-Based + Unified)
// ============================================================================

/**
 * Hybrid inscribed rectangle finder that runs both algorithms and picks the best result
 *
 * This function:
 * 1. Runs boundary-based algorithm first (fast, edge-aligned search)
 * 2. Runs unified algorithm with dimension constraints
 * 3. Applies dimension constraints to boundary result if applicable
 * 4. Compares areas and returns the best result
 *
 * @param {Array} polygon - Array of {x, y} points
 * @param {Object} options - Configuration options (same as findInscribedRectangle)
 * @returns {Object} Best rectangle layout with hybrid metadata
 */
function findInscribedRectangleHybrid(polygon, options = {}) {
    const {
        debugMode = false,
        // Dimension constraints
        width = { mode: 'continuous', min: 1, max: 1000, searchSteps: 20 },
        height = { mode: 'continuous', min: 1, max: 1000, searchSteps: 20 }
    } = options;

    if (!polygon || polygon.length < 3) {
        console.error('[hybrid-unified] Invalid polygon');
        return null;
    }

    const startTime = performance.now();

    // Step 1: Try boundary-based algorithm (requires SvgViewerBoundaryBased.js)
    let boundaryResult = null;
    let boundaryTime = 0;

    if (typeof boundaryBasedInscribedRectangle !== 'undefined') {
        if (debugMode) {
            console.log('[hybrid-unified] Step 1: Running boundary-based algorithm...');
        }

        const boundaryStart = performance.now();
        const boundaryRaw = boundaryBasedInscribedRectangle(polygon, {
            debugMode: debugMode,
            maxAngles: 6,
            angleTolerance: 5,
            testPerpendicular: true
        });
        boundaryTime = performance.now() - boundaryStart;

        if (boundaryRaw) {
            // Check if boundary result satisfies dimension constraints
            const meetsWidthConstraint = checkDimensionConstraint(boundaryRaw.width, width);
            const meetsHeightConstraint = checkDimensionConstraint(boundaryRaw.height, height);

            if (meetsWidthConstraint && meetsHeightConstraint) {
                boundaryResult = boundaryRaw;
                boundaryResult.type = 'boundary-based';
                if (debugMode) {
                    console.log(`[hybrid-unified] Boundary result: ${boundaryResult.width.toFixed(1)}×${boundaryResult.height.toFixed(1)} = ${boundaryResult.area.toFixed(1)} sq ft (✓ meets constraints)`);
                }
            } else {
                if (debugMode) {
                    console.log(`[hybrid-unified] Boundary result: ${boundaryRaw.width.toFixed(1)}×${boundaryRaw.height.toFixed(1)} (✗ doesn't meet dimension constraints)`);
                }
            }
        }
    } else {
        if (debugMode) {
            console.log('[hybrid-unified] Boundary-based algorithm not available, skipping...');
        }
    }

    // Step 2: Run unified algorithm with dimension constraints
    if (debugMode) {
        console.log('[hybrid-unified] Step 2: Running unified algorithm...');
    }

    const unifiedStart = performance.now();
    const unifiedResult = findInscribedRectangle(polygon, options);
    const unifiedTime = performance.now() - unifiedStart;

    if (unifiedResult) {
        unifiedResult.type = 'unified';
        if (debugMode) {
            console.log(`[hybrid-unified] Unified result: ${unifiedResult.width.toFixed(1)}×${unifiedResult.height.toFixed(1)} = ${unifiedResult.area.toFixed(1)} sq ft`);
        }
    }

    // Step 3: Compare and select best result
    const endTime = performance.now();
    let bestResult = null;

    if (!unifiedResult && !boundaryResult) {
        console.log('[hybrid-unified] ✗ Both algorithms failed');
        return null;
    } else if (!unifiedResult) {
        console.log('[hybrid-unified] ✓ Using boundary-based (unified failed)');
        bestResult = boundaryResult;
    } else if (!boundaryResult) {
        console.log('[hybrid-unified] ✓ Using unified (boundary-based failed or doesn\'t meet constraints)');
        bestResult = unifiedResult;
    } else {
        // Both succeeded - compare areas
        if (debugMode) {
            console.log(`[hybrid-unified] Comparing: boundary=${boundaryResult.area.toFixed(1)} vs unified=${unifiedResult.area.toFixed(1)}`);
        }

        if (boundaryResult.area >= unifiedResult.area) {
            bestResult = boundaryResult;
            console.log(`[hybrid-unified] ✓ Using boundary-based (${boundaryResult.area.toFixed(1)} ≥ ${unifiedResult.area.toFixed(1)})`);
        } else {
            bestResult = unifiedResult;
            console.log(`[hybrid-unified] ✓ Using unified (${unifiedResult.area.toFixed(1)} > ${boundaryResult.area.toFixed(1)})`);
        }
    }

    // Add hybrid metadata
    bestResult.hybridTotalTime = endTime - startTime;
    bestResult.usedBoundaryBased = boundaryResult !== null;
    bestResult.usedUnified = unifiedResult !== null;
    bestResult.selectedAlgorithm = bestResult.type;
    bestResult.boundaryTime = boundaryTime;
    bestResult.unifiedTime = unifiedTime;

    if (debugMode) {
        console.log(`[hybrid-unified] Total time: ${bestResult.hybridTotalTime.toFixed(1)}ms (boundary: ${boundaryTime.toFixed(1)}ms, unified: ${unifiedTime.toFixed(1)}ms)`);
    }

    return bestResult;
}

/**
 * Helper: Check if a dimension value meets the constraint
 */
function checkDimensionConstraint(value, constraint) {
    if (constraint.mode === 'fixed') {
        // Must exactly match the fixed value (with small tolerance)
        return Math.abs(value - constraint.value) < 0.1;
    } else if (constraint.mode === 'discrete') {
        // Must be base + n*increment for some integer n >= minIncrements
        const minIncrements = constraint.minIncrements || 0;
        const minValue = constraint.base + (minIncrements * constraint.increment);

        if (value < minValue - 0.1) return false;

        const n = Math.round((value - constraint.base) / constraint.increment);
        const expectedValue = constraint.base + (n * constraint.increment);
        return Math.abs(value - expectedValue) < 0.1;
    } else if (constraint.mode === 'continuous') {
        // Must be within min/max range
        return value >= constraint.min && value <= constraint.max;
    }

    return false;
}

// ============================================================================
// EXPORTS
// ============================================================================

// Export for Node.js testing
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        findInscribedRectangle,
        findInscribedRectangleHybrid,
        generateDimensionValues,
        generateDimensionPairs,
        isPointInPolygon,
        isRectangleInsidePolygon,
        segmentsIntersect,
        calculateRectangleCorners,
        calculatePolygonCentroid,
        getPolygonBounds
    };
}
