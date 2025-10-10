// Boundary-based inscribed rectangle algorithm
// Uses polygon boundary segments to determine optimal rectangle orientation

/**
 * Select optimal centroid placement strategy based on detectable geometric properties
 * @param {number} pathCount - Number of input paths merged
 * @param {number} vertexCount - Number of boundary vertices in merged polygon
 * @param {number} polygonArea - Area of merged polygon (bounding box area is acceptable approximation)
 * @returns {string} - "uniform" or "hybrid"
 */
function selectCentroidStrategy(pathCount, vertexCount, polygonArea) {
    // Rule 1: Complex multi-room assemblies (PRIMARY INDICATOR)
    // 6+ paths always benefit from edge-focused hybrid approach
    if (pathCount >= 6) {
        return "hybrid";
    }

    // Rule 2: High boundary complexity (SECONDARY INDICATOR)
    // Complex boundaries (12+ vertices) benefit from edge sampling
    if (vertexCount >= 12) {
        return "hybrid";
    }

    // Rule 3: Very small areas (TERTIARY INDICATOR)
    // Both approaches tied on small areas, hybrid is faster
    if (polygonArea < 5000) {
        return "hybrid";
    }

    // Rule 4: Large areas with simple paths (TERTIARY INDICATOR)
    // Large assemblies benefit from edge-focused sampling
    if (polygonArea > 25000 && pathCount >= 3) {
        return "hybrid";
    }

    // Default: Medium-sized simple joins favor uniform grid
    // This handles most common cases (2-path joins, 10K-20K sq px)
    return "uniform";
}

/**
 * Extract significant edges from polygon boundary
 * Returns edges sorted by length (longest first)
 */
function extractBoundaryEdges(polygon) {
    const edges = [];

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const length = Math.sqrt(dx * dx + dy * dy);

        // Calculate angle of this edge (in degrees, 0-180 range)
        let angle = Math.atan2(dy, dx) * 180 / Math.PI;
        if (angle < 0) angle += 180; // Normalize to 0-180

        edges.push({
            p1,
            p2,
            length,
            angle,
            dx,
            dy,
            index: i
        });
    }

    // Sort by length (longest first)
    edges.sort((a, b) => b.length - a.length);

    return edges;
}

/**
 * Find dominant angles in the polygon
 * Groups similar angles and returns the most significant ones
 */
function findDominantAngles(edges, angleTolerance = 5) {
    const angleGroups = {};

    // Group edges by similar angles
    for (const edge of edges) {
        // Skip very short edges (likely noise)
        if (edge.length < 5) continue;

        const angle = edge.angle;

        // Find existing group within tolerance
        let foundGroup = false;
        for (const groupAngle in angleGroups) {
            const diff = Math.abs(angle - parseFloat(groupAngle));
            if (diff < angleTolerance || diff > (180 - angleTolerance)) {
                angleGroups[groupAngle].edges.push(edge);
                angleGroups[groupAngle].totalLength += edge.length;
                foundGroup = true;
                break;
            }
        }

        if (!foundGroup) {
            angleGroups[angle] = {
                angle: angle,
                edges: [edge],
                totalLength: edge.length
            };
        }
    }

    // Convert to array and sort by total length
    const groups = Object.values(angleGroups);
    groups.sort((a, b) => b.totalLength - a.totalLength);

    return groups;
}

/**
 * Check if two angles are perpendicular (within tolerance)
 */
function arePerpendicularAngles(angle1, angle2, tolerance = 5) {
    let diff = Math.abs(angle1 - angle2);
    if (diff > 180) diff = 360 - diff;

    return Math.abs(diff - 90) < tolerance || Math.abs(diff - 270) < tolerance;
}

/**
 * Calculate convex hull using Graham scan algorithm
 */
function calculateConvexHull(points) {
    if (points.length < 3) return points;

    // Find lowest point (and leftmost if tied)
    let lowest = 0;
    for (let i = 1; i < points.length; i++) {
        if (points[i].y < points[lowest].y ||
            (points[i].y === points[lowest].y && points[i].x < points[lowest].x)) {
            lowest = i;
        }
    }

    // Sort points by polar angle with respect to lowest point
    const pivot = points[lowest];
    const sorted = points.slice();
    sorted.splice(lowest, 1);

    sorted.sort((a, b) => {
        const angleA = Math.atan2(a.y - pivot.y, a.x - pivot.x);
        const angleB = Math.atan2(b.y - pivot.y, b.x - pivot.x);
        if (angleA !== angleB) return angleA - angleB;
        // If same angle, sort by distance
        const distA = (a.x - pivot.x) ** 2 + (a.y - pivot.y) ** 2;
        const distB = (b.x - pivot.x) ** 2 + (b.y - pivot.y) ** 2;
        return distA - distB;
    });

    // Build hull using Graham scan
    const hull = [pivot, sorted[0]];

    for (let i = 1; i < sorted.length; i++) {
        while (hull.length > 1) {
            const p1 = hull[hull.length - 2];
            const p2 = hull[hull.length - 1];
            const p3 = sorted[i];

            // Cross product to determine turn direction
            const cross = (p2.x - p1.x) * (p3.y - p1.y) - (p2.y - p1.y) * (p3.x - p1.x);

            if (cross <= 0) {
                hull.pop(); // Right turn or collinear, remove p2
            } else {
                break; // Left turn, keep going
            }
        }
        hull.push(sorted[i]);
    }

    return hull;
}

/**
 * Generate edge-offset grid for boundary-touching rectangles
 * Places points at various distances inward from polygon edges
 */
function generateEdgeOffsetGrid(polygon, rotated, numSamplesPerEdge = 8, offsetDistances = [10, 25]) {
    const points = [];

    for (let i = 0; i < rotated.length; i++) {
        const p1 = rotated[i];
        const p2 = rotated[(i + 1) % rotated.length];

        // Calculate edge normal (pointing inward)
        const edgeDx = p2.x - p1.x;
        const edgeDy = p2.y - p1.y;
        const edgeLen = Math.sqrt(edgeDx * edgeDx + edgeDy * edgeDy);

        if (edgeLen < 1) continue; // Skip very short edges

        // Perpendicular vector (inward normal)
        const normalX = -edgeDy / edgeLen;
        const normalY = edgeDx / edgeLen;

        // Sample along edge
        for (let s = 1; s < numSamplesPerEdge; s++) {
            const t = s / numSamplesPerEdge;
            const edgeX = p1.x + t * edgeDx;
            const edgeY = p1.y + t * edgeDy;

            // Place points at various offsets from edge (inside polygon)
            for (const offset of offsetDistances) {
                const pointX = edgeX + normalX * offset;
                const pointY = edgeY + normalY * offset;
                points.push({ x: pointX, y: pointY });
            }
        }
    }

    return points;
}

/**
 * Generate points in convex hull interior for open-region rectangles
 * Samples grid points inside the convex hull of the polygon
 */
function generateConvexHullInterior(convexHull, numSamples = 30) {
    if (convexHull.length < 3) return [];

    // Find bounding box of convex hull
    let minX = Infinity, minY = Infinity;
    let maxX = -Infinity, maxY = -Infinity;

    for (const p of convexHull) {
        minX = Math.min(minX, p.x);
        minY = Math.min(minY, p.y);
        maxX = Math.max(maxX, p.x);
        maxY = Math.max(maxY, p.y);
    }

    const points = [];
    const gridSize = Math.ceil(Math.sqrt(numSamples * 2)); // Oversample
    const stepX = (maxX - minX) / (gridSize + 1);
    const stepY = (maxY - minY) / (gridSize + 1);

    for (let i = 1; i <= gridSize && points.length < numSamples; i++) {
        for (let j = 1; j <= gridSize && points.length < numSamples; j++) {
            const x = minX + i * stepX;
            const y = minY + j * stepY;
            const point = { x, y };

            // Check if point is inside convex hull
            if (isPointInPolygonSlow(point, convexHull)) {
                points.push(point);
            }
        }
    }

    return points;
}

/**
 * Find the largest axis-aligned bounding box that fits inside the polygon
 * when rotated by the given angle
 */
function findMaxRectangleAtAngle(polygon, angleDeg, debugMode = false, targetArea = null, adaptiveGridSteps = 10, adaptiveAspectRatios = [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.5, 1.7, 2.0, 2.3], centroidStrategy = "uniform", pathCount = 2) {
    const angleRad = (angleDeg * Math.PI) / 180;
    const cos = Math.cos(angleRad);
    const sin = Math.sin(angleRad);

    // Force debug for Test17 at specific angles
    const forceDebug = debugMode || (polygon.length === 15 && (Math.abs(angleDeg - 25.9) < 1 || Math.abs(angleDeg - 9.4) < 0.1 || Math.abs(angleDeg - 99.4) < 0.1));

    // Rotate all polygon points to align with angle
    const rotated = polygon.map(p => ({
        x: p.x * cos + p.y * sin,
        y: -p.x * sin + p.y * cos
    }));

    // Find bounding box of rotated polygon
    let minX = Infinity, minY = Infinity;
    let maxX = -Infinity, maxY = -Infinity;

    for (const p of rotated) {
        minX = Math.min(minX, p.x);
        minY = Math.min(minY, p.y);
        maxX = Math.max(maxX, p.x);
        maxY = Math.max(maxY, p.y);
    }

    const width = maxX - minX;
    const height = maxY - minY;

    if (forceDebug) {
        console.log(`[findMaxRect] Angle ${angleDeg.toFixed(1)}°: Rotated bounding box = ${width.toFixed(1)} × ${height.toFixed(1)}`);
        console.log(`[findMaxRect]   Rotated bounds: X[${minX.toFixed(1)}, ${maxX.toFixed(1)}], Y[${minY.toFixed(1)}, ${maxY.toFixed(1)}]`);
    }

    // Calculate polygon centroid in rotated space
    let centroidX = 0, centroidY = 0;
    for (const p of rotated) {
        centroidX += p.x;
        centroidY += p.y;
    }
    centroidX /= rotated.length;
    centroidY /= rotated.length;

    // Test different rectangle sizes using a simpler approach
    // Try multiple aspect ratios and find the largest that fits
    let bestRect = null;
    let bestArea = 0;

    // Collect all test centroids using ADAPTIVE STRATEGY
    const testCentroids = [];

    // Always add the polygon centroid (ensures we don't miss it due to grid alignment)
    testCentroids.push({ x: centroidX, y: centroidY });

    // Add centroids near each rotated polygon vertex (improves vertex-aligned rectangles)
    for (const p of rotated) {
        testCentroids.push({ x: p.x, y: p.y });
    }

    // ADAPTIVE CENTROID PLACEMENT
    if (centroidStrategy === "hybrid") {
        // HYBRID STRATEGY: Edge-offset + sparse interior + convex hull
        // Best for complex multi-room assemblies (6+ paths) and large areas

        // 1. Edge-offset grid: ~120 points near polygon boundaries (2 offsets)
        //    Rectangles in complex assemblies often touch multiple edges
        const edgeOffsetPoints = generateEdgeOffsetGrid(polygon, rotated, 8, [10, 25]);
        for (const p of edgeOffsetPoints) {
            testCentroids.push(p);
        }

        // 2. Sparse interior grid: 10×10 = 100 points
        //    Provides basic interior coverage without excessive sampling
        const interiorGridSteps = 10;
        const interiorStepX = width / (interiorGridSteps + 1);
        const interiorStepY = height / (interiorGridSteps + 1);

        for (let i = 1; i <= interiorGridSteps; i++) {
            for (let j = 1; j <= interiorGridSteps; j++) {
                const centerX = minX + i * interiorStepX;
                const centerY = minY + j * interiorStepY;
                testCentroids.push({ x: centerX, y: centerY });
            }
        }

        // 3. Convex hull interior: ~30 points in most "open" region
        //    Large rectangles tend to fit well in convex regions
        const convexHull = calculateConvexHull(rotated);
        const hullInteriorPoints = generateConvexHullInterior(convexHull, 30);
        for (const p of hullInteriorPoints) {
            testCentroids.push(p);
        }

    } else {
        // UNIFORM GRID STRATEGY: Dense interior coverage
        // Best for simple 2-path joins and medium-sized areas (10K-20K sq px)

        // Adaptive base grid: simpler shapes get finer base grid
        const isSimpleShape = polygon.length <= 4;
        const baseGridSteps = isSimpleShape ? 12 : 8;

        // Base uniform grid
        const baseStepX = width / (baseGridSteps + 1);
        const baseStepY = height / (baseGridSteps + 1);

        for (let i = 1; i <= baseGridSteps; i++) {
            for (let j = 1; j <= baseGridSteps; j++) {
                const centerX = minX + i * baseStepX;
                const centerY = minY + j * baseStepY;
                testCentroids.push({ x: centerX, y: centerY });
            }
        }

        // Dense uniform grid: 20×20 for comprehensive interior coverage
        //    Critical for finding interior-optimal rectangles in horizontal joins
        const denseGridSteps = 20;
        const denseStepX = width / (denseGridSteps + 1);
        const denseStepY = height / (denseGridSteps + 1);

        for (let i = 1; i <= denseGridSteps; i++) {
            for (let j = 1; j <= denseGridSteps; j++) {
                const centerX = minX + i * denseStepX;
                const centerY = minY + j * denseStepY;
                testCentroids.push({ x: centerX, y: centerY });
            }
        }
    }

    // If target area is provided, add focused grid in target region (goal seeker mode)
    if (targetArea && targetArea.bounds) {
        // Rotate target bounds to same coordinate space
        const targetCorners = [
            { x: targetArea.bounds.minX, y: targetArea.bounds.minY },
            { x: targetArea.bounds.maxX, y: targetArea.bounds.minY },
            { x: targetArea.bounds.maxX, y: targetArea.bounds.maxY },
            { x: targetArea.bounds.minX, y: targetArea.bounds.maxY }
        ];

        const rotatedTarget = targetCorners.map(p => ({
            x: p.x * cos + p.y * sin,
            y: -p.x * sin + p.y * cos
        }));

        // Find bounding box of rotated target
        let targetMinX = Infinity, targetMinY = Infinity;
        let targetMaxX = -Infinity, targetMaxY = -Infinity;

        for (const p of rotatedTarget) {
            targetMinX = Math.min(targetMinX, p.x);
            targetMinY = Math.min(targetMinY, p.y);
            targetMaxX = Math.max(targetMaxX, p.x);
            targetMaxY = Math.max(targetMaxY, p.y);
        }

        const targetWidth = targetMaxX - targetMinX;
        const targetHeight = targetMaxY - targetMinY;

        // Add a denser grid focused on target region (20x20 grid)
        const targetGridSteps = 20;
        const targetStepX = targetWidth / (targetGridSteps + 1);
        const targetStepY = targetHeight / (targetGridSteps + 1);

        for (let i = 1; i <= targetGridSteps; i++) {
            for (let j = 1; j <= targetGridSteps; j++) {
                const centerX = targetMinX + i * targetStepX;
                const centerY = targetMinY + j * targetStepY;
                testCentroids.push({ x: centerX, y: centerY });
            }
        }

        // Also add target corners and center
        testCentroids.push({ x: (targetMinX + targetMaxX) / 2, y: (targetMinY + targetMaxY) / 2 });
        for (const p of rotatedTarget) {
            testCentroids.push({ x: p.x, y: p.y });
        }

        if (forceDebug) {
            console.log(`[findMaxRect] Added ${targetGridSteps * targetGridSteps + 5} target-focused centroids (goal seeker)`);
            console.log(`[findMaxRect] Target region (rotated): X[${targetMinX.toFixed(1)}, ${targetMaxX.toFixed(1)}], Y[${targetMinY.toFixed(1)}, ${targetMaxY.toFixed(1)}]`);
        }
    }

    if (forceDebug) {
        console.log(`[findMaxRect] Angle ${angleDeg.toFixed(1)}°: Testing ${testCentroids.length} centroid positions`);
    }

    // Test each centroid
    for (const center of testCentroids) {
            const centerX = center.x;
            const centerY = center.y;

            // For each center, try different rectangles
            // Test multiple aspect ratios (adaptive based on path count)
            const aspectRatios = adaptiveAspectRatios;

            for (const aspectRatio of aspectRatios) {
                // Binary search for maximum scale
                // Use the larger dimension as base to ensure we can capture elongated shapes
                const baseDim = Math.max(width, height);
                let low = 0, high = 1;
                let bestScale = 0;

                while (high - low > 0.001) {
                    const scale = (low + high) / 2;

                    // Calculate dimensions based on aspect ratio and available space
                    // Start with base dimension scaled, then adjust for aspect ratio
                    let testW = baseDim * scale;
                    let testH = baseDim * scale / aspectRatio;

                    // Cap dimensions at available bounding box space
                    testW = Math.min(testW, width);
                    testH = Math.min(testH, height);

                    // Check if this rectangle fits
                    const rectCorners = [
                        { x: centerX - testW/2, y: centerY - testH/2 },
                        { x: centerX + testW/2, y: centerY - testH/2 },
                        { x: centerX + testW/2, y: centerY + testH/2 },
                        { x: centerX - testW/2, y: centerY + testH/2 }
                    ];

                    // Rotate back to original coordinates
                    const originalCorners = rectCorners.map(p => ({
                        x: p.x * cos - p.y * sin,
                        y: p.x * sin + p.y * cos
                    }));

                    // Check if all corners are inside polygon
                    let allInside = true;
                    for (const corner of originalCorners) {
                        if (!isPointInPolygonSlow(corner, polygon)) {
                            allInside = false;
                            break;
                        }
                    }

                    // Also verify rectangle edges don't cross outside polygon (for concave shapes)
                    // Sample points along each edge to ensure they stay inside
                    if (allInside) {
                        const edgeSamples = 5; // Sample 5 points along each edge
                        const testArea = testW * testH;
                        const debugThis = Math.abs(angleDeg - 9.4) < 0.1 && testArea > 40000;

                        if (debugThis) {
                            console.log(`[EDGE-CHECK] Testing 9.4° rect ${testW.toFixed(1)}×${testH.toFixed(1)} (${testArea.toFixed(1)} sq px)`);
                        }

                        for (let edgeIdx = 0; edgeIdx < 4; edgeIdx++) {
                            const p1 = originalCorners[edgeIdx];
                            const p2 = originalCorners[(edgeIdx + 1) % 4];

                            for (let s = 1; s < edgeSamples; s++) {
                                const t = s / edgeSamples;
                                const samplePoint = {
                                    x: p1.x + t * (p2.x - p1.x),
                                    y: p1.y + t * (p2.y - p1.y)
                                };

                                if (!isPointInPolygonSlow(samplePoint, polygon)) {
                                    allInside = false;
                                    if (debugThis) {
                                        console.log(`[EDGE-REJECT] 9.4° rect ${testW.toFixed(1)}×${testH.toFixed(1)} (${(testW*testH).toFixed(1)} sq px) rejected: edge ${edgeIdx} sample ${s} at (${samplePoint.x.toFixed(1)}, ${samplePoint.y.toFixed(1)}) outside polygon`);
                                    }
                                    break;
                                }
                            }

                            if (!allInside) break;
                        }
                    }

                    if (allInside) {
                        bestScale = scale;
                        low = scale;
                    } else {
                        high = scale;
                    }
                }

                if (bestScale > 0) {
                    // Recalculate final dimensions with same logic as test
                    const baseDim = Math.max(width, height);
                    let finalW = baseDim * bestScale;
                    let finalH = baseDim * bestScale / aspectRatio;
                    finalW = Math.min(finalW, width);
                    finalH = Math.min(finalH, height);
                    const area = finalW * finalH;

                    // Debug successful rectangles at 9.4°
                    if (Math.abs(angleDeg - 9.4) < 0.1 && area > 40000) {
                        console.log(`[EDGE-ACCEPT] 9.4° rect ${finalW.toFixed(1)}×${finalH.toFixed(1)} (${area.toFixed(1)} sq px) passed all edge samples`);
                    }

                    if (area > bestArea) {
                        bestArea = area;

                        const rectCorners = [
                            { x: centerX - finalW/2, y: centerY - finalH/2 },
                            { x: centerX + finalW/2, y: centerY - finalH/2 },
                            { x: centerX + finalW/2, y: centerY + finalH/2 },
                            { x: centerX - finalW/2, y: centerY + finalH/2 }
                        ];

                        const originalCorners = rectCorners.map(p => ({
                            x: p.x * cos - p.y * sin,
                            y: p.x * sin + p.y * cos
                        }));

                        const origCentroid = {
                            x: centerX * cos - centerY * sin,
                            y: centerX * sin + centerY * cos
                        };

                        bestRect = {
                            corners: originalCorners,
                            width: finalW,
                            height: finalH,
                            area: area,
                            angle: angleDeg,
                            centroid: origCentroid
                        };

                        // Debug best rectangle updates at 9.4°
                        if (Math.abs(angleDeg - 9.4) < 0.1) {
                            console.log(`[NEW-BEST] 9.4° new best: ${finalW.toFixed(1)}×${finalH.toFixed(1)} = ${area.toFixed(1)} sq px, aspect=${aspectRatio.toFixed(2)}, centroid=(${origCentroid.x.toFixed(1)}, ${origCentroid.y.toFixed(1)})`);
                        }
                    }
                }
            }
    }

    // Special debug for problematic angles
    if (bestRect && (Math.abs(angleDeg - 9.4) < 0.1 || Math.abs(angleDeg - 25.9) < 0.1)) {
        console.log(`[ANGLE-DEBUG] Angle ${angleDeg.toFixed(1)}°: Found ${bestRect.width.toFixed(1)} × ${bestRect.height.toFixed(1)} = ${bestRect.area.toFixed(1)} sq px`);
        console.log(`[ANGLE-DEBUG]   Corners:`, bestRect.corners.map(c => `(${c.x.toFixed(1)}, ${c.y.toFixed(1)})`).join(', '));
    }

    if (forceDebug && bestRect) {
        console.log(`[findMaxRect] Angle ${angleDeg.toFixed(1)}°: Best rectangle = ${bestRect.width.toFixed(1)} × ${bestRect.height.toFixed(1)} = ${bestRect.area.toFixed(1)} sq px`);
        console.log(`[findMaxRect]   Corners in original space:`, bestRect.corners.map(c => `(${c.x.toFixed(1)}, ${c.y.toFixed(1)})`).join(', '));
    }

    return bestRect;
}

/**
 * Calculate convex hull using Graham scan algorithm
 * Returns vertices in counter-clockwise order
 */
function calculateConvexHull(points) {
    if (points.length < 3) return points;

    // Find lowest point (and leftmost if tied)
    let lowest = 0;
    for (let i = 1; i < points.length; i++) {
        if (points[i].y < points[lowest].y ||
            (points[i].y === points[lowest].y && points[i].x < points[lowest].x)) {
            lowest = i;
        }
    }

    // Sort points by polar angle with respect to lowest point
    const pivot = points[lowest];
    const sorted = points.slice();
    sorted.splice(lowest, 1);

    sorted.sort((a, b) => {
        const angleA = Math.atan2(a.y - pivot.y, a.x - pivot.x);
        const angleB = Math.atan2(b.y - pivot.y, b.x - pivot.x);
        if (angleA !== angleB) return angleA - angleB;
        // If same angle, sort by distance
        const distA = (a.x - pivot.x) ** 2 + (a.y - pivot.y) ** 2;
        const distB = (b.x - pivot.x) ** 2 + (b.y - pivot.y) ** 2;
        return distA - distB;
    });

    // Build hull
    const hull = [pivot, sorted[0]];

    for (let i = 1; i < sorted.length; i++) {
        while (hull.length > 1) {
            const p1 = hull[hull.length - 2];
            const p2 = hull[hull.length - 1];
            const p3 = sorted[i];

            // Cross product to determine turn direction
            const cross = (p2.x - p1.x) * (p3.y - p1.y) - (p2.y - p1.y) * (p3.x - p1.x);

            if (cross <= 0) {
                hull.pop(); // Right turn or collinear, remove p2
            } else {
                break; // Left turn, keep going
            }
        }
        hull.push(sorted[i]);
    }

    return hull;
}

/**
 * Generate edge-offset grid points near polygon boundaries
 * Places points at various distances from each polygon edge (inside the polygon)
 */
function generateEdgeOffsetGrid(polygon, rotated, numSamplesPerEdge = 15, offsetDistances = [5, 10, 20, 30]) {
    const points = [];

    for (let i = 0; i < rotated.length; i++) {
        const p1 = rotated[i];
        const p2 = rotated[(i + 1) % rotated.length];

        // Calculate edge normal (pointing inward)
        const edgeDx = p2.x - p1.x;
        const edgeDy = p2.y - p1.y;
        const edgeLen = Math.sqrt(edgeDx * edgeDx + edgeDy * edgeDy);

        if (edgeLen < 1) continue; // Skip very short edges

        // Perpendicular vector (inward normal)
        const normalX = -edgeDy / edgeLen;
        const normalY = edgeDx / edgeLen;

        // Sample along edge
        for (let s = 1; s < numSamplesPerEdge; s++) {
            const t = s / numSamplesPerEdge;
            const edgeX = p1.x + t * edgeDx;
            const edgeY = p1.y + t * edgeDy;

            // Place points at various offsets from edge (inside polygon)
            for (const offset of offsetDistances) {
                const pointX = edgeX + normalX * offset;
                const pointY = edgeY + normalY * offset;
                points.push({ x: pointX, y: pointY });
            }
        }
    }

    return points;
}

/**
 * Generate points within convex hull interior
 * Uses a grid sampling approach within the convex hull
 */
function generateConvexHullInterior(convexHull, numSamples = 50) {
    if (convexHull.length < 3) return [];

    // Find bounding box of convex hull
    let minX = Infinity, minY = Infinity;
    let maxX = -Infinity, maxY = -Infinity;

    for (const p of convexHull) {
        minX = Math.min(minX, p.x);
        minY = Math.min(minY, p.y);
        maxX = Math.max(maxX, p.x);
        maxY = Math.max(maxY, p.y);
    }

    const points = [];
    const gridSize = Math.ceil(Math.sqrt(numSamples * 2)); // Oversample to account for points outside hull
    const stepX = (maxX - minX) / (gridSize + 1);
    const stepY = (maxY - minY) / (gridSize + 1);

    for (let i = 1; i <= gridSize && points.length < numSamples; i++) {
        for (let j = 1; j <= gridSize && points.length < numSamples; j++) {
            const x = minX + i * stepX;
            const y = minY + j * stepY;
            const point = { x, y };

            // Check if point is inside convex hull
            if (isPointInPolygonSlow(point, convexHull)) {
                points.push(point);
            }
        }
    }

    return points;
}

/**
 * Simple point-in-polygon test
 */
function isPointInPolygonSlow(point, polygon) {
    let inside = false;
    for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
        const xi = polygon[i].x, yi = polygon[i].y;
        const xj = polygon[j].x, yj = polygon[j].y;

        const intersect = ((yi > point.y) !== (yj > point.y))
            && (point.x < (xj - xi) * (point.y - yi) / (yj - yi) + xi);
        if (intersect) inside = !inside;
    }
    return inside;
}

/**
 * Point-in-polygon test with small tolerance for edge cases
 * If a point is very close to the boundary (within tolerance), consider it inside
 */
function isPointInPolygonWithTolerance(point, polygon, tolerance = 0.5) {
    // First check if point is inside
    if (isPointInPolygonSlow(point, polygon)) {
        return true;
    }

    // If not inside, check if it's very close to any edge
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        // Calculate distance from point to line segment
        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const lengthSq = dx * dx + dy * dy;

        if (lengthSq === 0) continue; // Zero-length segment

        // Project point onto line segment
        const t = Math.max(0, Math.min(1, ((point.x - p1.x) * dx + (point.y - p1.y) * dy) / lengthSq));
        const projX = p1.x + t * dx;
        const projY = p1.y + t * dy;

        // Distance from point to projection
        const distX = point.x - projX;
        const distY = point.y - projY;
        const dist = Math.sqrt(distX * distX + distY * distY);

        if (dist <= tolerance) {
            return true; // Point is very close to boundary, consider it inside
        }
    }

    return false;
}

/**
 * Edge expansion: Push each edge of the rectangle outward until it touches the boundary
 * STRICT RULE: Rectangle must NEVER breach the polygon boundary
 *
 * Strategy: Expand opposite edges (top/bottom, left/right) to maintain rectangular shape
 */
function expandRectangleEdges(rect, polygon, debugMode = false) {
    if (!rect || !rect.corners || rect.corners.length !== 4) {
        if (debugMode) console.log('[edge-expansion] Invalid rectangle, skipping');
        return rect;
    }

    // Get original corners (deep copy)
    const origCorners = rect.corners.map(c => ({ x: c.x, y: c.y }));

    // Force debug for Test15 (angle near 99.4°)
    const forceDebug = debugMode || (rect.angle > 99.0 && rect.angle < 100.0);

    if (forceDebug) {
        console.log(`[edge-expansion] Starting expansion from ${rect.width.toFixed(1)} × ${rect.height.toFixed(1)} at angle ${rect.angle.toFixed(1)}°`);
        console.log(`[edge-expansion] Original corners:`, origCorners.map(c => `(${c.x.toFixed(1)}, ${c.y.toFixed(1)})`));
    }

    // Calculate edge vectors for the rectangle
    // Corners are ordered: [0] bottom-left, [1] bottom-right, [2] top-right, [3] top-left
    const widthVec = {
        x: origCorners[1].x - origCorners[0].x,
        y: origCorners[1].y - origCorners[0].y
    };
    const widthLen = Math.sqrt(widthVec.x * widthVec.x + widthVec.y * widthVec.y);
    const widthDir = { x: widthVec.x / widthLen, y: widthVec.y / widthLen };

    const heightVec = {
        x: origCorners[3].x - origCorners[0].x,
        y: origCorners[3].y - origCorners[0].y
    };
    const heightLen = Math.sqrt(heightVec.x * heightVec.x + heightVec.y * heightVec.y);
    const heightDir = { x: heightVec.x / heightLen, y: heightVec.y / heightLen };

    // Perpendicular directions (outward normals)
    const bottomNormal = { x: -heightDir.x, y: -heightDir.y };  // Opposite of height direction
    const topNormal = { x: heightDir.x, y: heightDir.y };       // Same as height direction
    const leftNormal = { x: -widthDir.x, y: -widthDir.y };      // Opposite of width direction
    const rightNormal = { x: widthDir.x, y: widthDir.y };       // Same as width direction

    // Find maximum expansion for each direction independently
    const expansions = {
        bottom: findMaxExpansion(origCorners, [0, 1], bottomNormal, polygon, forceDebug),
        top: findMaxExpansion(origCorners, [2, 3], topNormal, polygon, forceDebug),
        left: findMaxExpansion(origCorners, [0, 3], leftNormal, polygon, forceDebug),
        right: findMaxExpansion(origCorners, [1, 2], rightNormal, polygon, forceDebug)
    };

    if (forceDebug) {
        console.log(`[edge-expansion] Found expansions: bottom=${expansions.bottom.toFixed(2)}px, top=${expansions.top.toFixed(2)}px, left=${expansions.left.toFixed(2)}px, right=${expansions.right.toFixed(2)}px`);
        console.log(`[edge-expansion] Normals: bottom=(${bottomNormal.x.toFixed(3)}, ${bottomNormal.y.toFixed(3)}), top=(${topNormal.x.toFixed(3)}, ${topNormal.y.toFixed(3)}), left=(${leftNormal.x.toFixed(3)}, ${leftNormal.y.toFixed(3)}), right=(${rightNormal.x.toFixed(3)}, ${rightNormal.y.toFixed(3)})`);
    }

    // Apply all expansions simultaneously to create new corners
    const newCorners = [
        // Corner 0 (bottom-left): affected by bottom and left
        {
            x: origCorners[0].x + bottomNormal.x * expansions.bottom + leftNormal.x * expansions.left,
            y: origCorners[0].y + bottomNormal.y * expansions.bottom + leftNormal.y * expansions.left
        },
        // Corner 1 (bottom-right): affected by bottom and right
        {
            x: origCorners[1].x + bottomNormal.x * expansions.bottom + rightNormal.x * expansions.right,
            y: origCorners[1].y + bottomNormal.y * expansions.bottom + rightNormal.y * expansions.right
        },
        // Corner 2 (top-right): affected by top and right
        {
            x: origCorners[2].x + topNormal.x * expansions.top + rightNormal.x * expansions.right,
            y: origCorners[2].y + topNormal.y * expansions.top + rightNormal.y * expansions.right
        },
        // Corner 3 (top-left): affected by top and left
        {
            x: origCorners[3].x + topNormal.x * expansions.top + leftNormal.x * expansions.left,
            y: origCorners[3].y + topNormal.y * expansions.top + leftNormal.y * expansions.left
        }
    ];

    // Validate that all new corners and edge samples are inside polygon
    let valid = true;
    for (const corner of newCorners) {
        if (!isPointInPolygonSlow(corner, polygon)) {
            valid = false;
            if (debugMode) console.log(`[edge-expansion] Corner (${corner.x.toFixed(1)}, ${corner.y.toFixed(1)}) is outside polygon`);
            break;
        }
    }

    // Sample along edges with DENSE sampling (every 2%) to catch lines that exit polygon
    if (valid) {
        for (let i = 0; i < 4; i++) {
            const c1 = newCorners[i];
            const c2 = newCorners[(i + 1) % 4];
            for (let t = 0.02; t < 1.0; t += 0.02) {
                const sample = {
                    x: c1.x + (c2.x - c1.x) * t,
                    y: c1.y + (c2.y - c1.y) * t
                };
                if (!isPointInPolygonSlow(sample, polygon)) {
                    valid = false;
                    if (debugMode) console.log(`[edge-expansion] Edge sample at t=${t.toFixed(2)} is outside polygon`);
                    break;
                }
            }
            if (!valid) break;
        }
    }

    if (!valid) {
        if (debugMode) console.log('[edge-expansion] Expansion failed validation, keeping original');
        return rect;
    }

    const totalExpansion = expansions.bottom + expansions.top + expansions.left + expansions.right;

    if (totalExpansion < 0.1) {
        if (debugMode) console.log('[edge-expansion] No significant expansion found');
        return rect;
    }

    // Recalculate dimensions
    const newWidth = Math.sqrt(
        Math.pow(newCorners[1].x - newCorners[0].x, 2) +
        Math.pow(newCorners[1].y - newCorners[0].y, 2)
    );
    const newHeight = Math.sqrt(
        Math.pow(newCorners[3].x - newCorners[0].x, 2) +
        Math.pow(newCorners[3].y - newCorners[0].y, 2)
    );
    const newArea = newWidth * newHeight;
    const newCentroid = {
        x: (newCorners[0].x + newCorners[1].x + newCorners[2].x + newCorners[3].x) / 4,
        y: (newCorners[0].y + newCorners[1].y + newCorners[2].y + newCorners[3].y) / 4
    };

    if (debugMode) {
        console.log(`[edge-expansion] Expanded ${rect.width.toFixed(1)} × ${rect.height.toFixed(1)} to ${newWidth.toFixed(1)} × ${newHeight.toFixed(1)}`);
        console.log(`[edge-expansion] Area increased from ${rect.area.toFixed(1)} to ${newArea.toFixed(1)} sq px (+${(newArea - rect.area).toFixed(1)})`);
    }

    return {
        ...rect,
        corners: newCorners,
        width: newWidth,
        height: newHeight,
        area: newArea,
        centroid: newCentroid
    };
}

/**
 * Helper function: Find maximum expansion distance for an edge
 */
function findMaxExpansion(corners, vertexIndices, normal, polygon, debugMode) {
    let low = 0;
    let high = 200; // Max 200px expansion per edge
    let bestExpansion = 0;
    const precision = 0.1;
    let failureReason = null;

    while (high - low > precision) {
        const testDist = (low + high) / 2;

        // Test if moving these vertices outward by testDist is valid
        let valid = true;

        // Check the two vertices that would move
        for (const idx of vertexIndices) {
            const testPoint = {
                x: corners[idx].x + normal.x * testDist,
                y: corners[idx].y + normal.y * testDist
            };

            if (!isPointInPolygonSlow(testPoint, polygon)) {
                valid = false;
                if (debugMode && bestExpansion === 0 && testDist < 5) {
                    failureReason = `vertex ${idx} at (${testPoint.x.toFixed(1)}, ${testPoint.y.toFixed(1)}) outside polygon`;
                }
                break;
            }
        }

        // Also sample along the edge between these two vertices (DENSE sampling every 5%)
        if (valid && vertexIndices.length === 2) {
            const v0 = {
                x: corners[vertexIndices[0]].x + normal.x * testDist,
                y: corners[vertexIndices[0]].y + normal.y * testDist
            };
            const v1 = {
                x: corners[vertexIndices[1]].x + normal.x * testDist,
                y: corners[vertexIndices[1]].y + normal.y * testDist
            };

            for (let t = 0.05; t < 1.0; t += 0.05) {
                const sample = {
                    x: v0.x + (v1.x - v0.x) * t,
                    y: v0.y + (v1.y - v0.y) * t
                };

                if (!isPointInPolygonSlow(sample, polygon)) {
                    valid = false;
                    break;
                }
            }
        }

        if (valid) {
            bestExpansion = testDist;
            low = testDist;
        } else {
            high = testDist;
        }
    }

    if (debugMode && failureReason) {
        console.log(`[findMaxExpansion] Early failure: ${failureReason}`);
    }
    if (debugMode) {
        console.log(`[findMaxExpansion] vertices [${vertexIndices}], normal (${normal.x.toFixed(3)}, ${normal.y.toFixed(3)}), expansion: ${bestExpansion.toFixed(2)}px`);
    }

    return bestExpansion;
}

/**
 * Main boundary-based algorithm
 * Uses polygon edges to determine optimal rectangle orientation
 */
function boundaryBasedInscribedRectangle(polygon, options = {}) {
    const {
        debugMode = false,
        pathCount = undefined,  // Number of original paths (for adaptive tuning)
        maxAngles = 6,  // Test top N dominant angles (slightly more for better coverage)
        angleTolerance = 5,  // Degrees tolerance for grouping angles
        testPerpendicular = true,  // Also test angles perpendicular to dominant edges
        targetArea = null  // Optional target area for focused search
    } = options;

    // Adaptive parameters based on path complexity
    // 2-4 paths: finer settings for better accuracy
    // 5+ paths: balanced settings for quality/speed
    // 6+ paths: optimized for complex assemblies
    const isSimple = pathCount !== undefined && pathCount >= 2 && pathCount <= 4;
    const isComplex = pathCount !== undefined && pathCount >= 6;
    const adaptiveGridSteps = isSimple ? 12 : (isComplex ? 8 : 8);
    const adaptiveAspectRatios = isSimple
        ? [0.5, 0.6, 0.7, 0.85, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.7, 2.0, 2.3]
        : (isComplex ? [0.5, 0.7, 1.0, 1.3, 1.5, 1.8, 2.0, 2.5] : [0.5, 0.7, 1.0, 1.3, 1.5, 1.8, 2.0, 2.5]);
    const adaptiveMaxAngles = isSimple ? 8 : (isComplex ? 5 : 5);

    if (!polygon || polygon.length < 3) {
        console.error('[boundary-based] Invalid polygon');
        return null;
    }

    const startTime = performance.now();

    // Force debug for Test17 (15-vertex polygon)
    const forceDebug = debugMode || polygon.length === 15;

    // Calculate polygon bounding box area (approximation for strategy selection)
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const p of polygon) {
        minX = Math.min(minX, p.x);
        minY = Math.min(minY, p.y);
        maxX = Math.max(maxX, p.x);
        maxY = Math.max(maxY, p.y);
    }
    const boundingBoxArea = (maxX - minX) * (maxY - minY);

    // Select optimal centroid placement strategy
    const centroidStrategy = selectCentroidStrategy(
        pathCount || 2,           // Default to 2 if not provided
        polygon.length,            // Vertex count
        boundingBoxArea            // Approximate polygon area
    );

    if (forceDebug) {
        console.log(`[boundary-based] Strategy selection: ${centroidStrategy.toUpperCase()}`);
        console.log(`  - Path count: ${pathCount || 2}`);
        console.log(`  - Vertices: ${polygon.length}`);
        console.log(`  - Bounding box area: ${boundingBoxArea.toFixed(0)} sq px`);
    }

    // Extract boundary edges
    const edges = extractBoundaryEdges(polygon);

    if (forceDebug) {
        console.log(`[boundary-based] Polygon has ${polygon.length} vertices, ${edges.length} edges`);
        console.log(`[boundary-based] Longest edge: ${edges[0].length.toFixed(1)}px at ${edges[0].angle.toFixed(1)}°`);
        console.log(`[boundary-based] All edges (sorted by length):`);
        for (let i = 0; i < Math.min(10, edges.length); i++) {
            const e = edges[i];
            console.log(`  ${i+1}. Edge ${e.index}: ${e.length.toFixed(1)}px at ${e.angle.toFixed(1)}° from (${e.p1.x.toFixed(1)}, ${e.p1.y.toFixed(1)}) to (${e.p2.x.toFixed(1)}, ${e.p2.y.toFixed(1)})`);
        }
    }

    // Find dominant angles
    const angleGroups = findDominantAngles(edges, angleTolerance);

    if (forceDebug) {
        console.log(`[boundary-based] Found ${angleGroups.length} angle groups:`);
        for (let i = 0; i < Math.min(10, angleGroups.length); i++) {
            const g = angleGroups[i];
            console.log(`  ${i+1}. ${g.angle.toFixed(1)}° (${g.edges.length} edges, total ${g.totalLength.toFixed(1)}px)`);
        }
    }

    // Collect angles to test
    const anglesToTest = new Set();

    // Add dominant angles (use adaptive max angles based on path count)
    for (let i = 0; i < Math.min(adaptiveMaxAngles, angleGroups.length); i++) {
        anglesToTest.add(angleGroups[i].angle);

        // Also test perpendicular to dominant angles
        if (testPerpendicular) {
            const perpAngle = (angleGroups[i].angle + 90) % 180;
            anglesToTest.add(perpAngle);
        }
    }

    // Also add perpendicular pairs
    if (angleGroups.length >= 2 && testPerpendicular) {
        for (let i = 0; i < Math.min(3, angleGroups.length); i++) {
            for (let j = i + 1; j < Math.min(4, angleGroups.length); j++) {
                if (arePerpendicularAngles(angleGroups[i].angle, angleGroups[j].angle)) {
                    anglesToTest.add(angleGroups[i].angle);
                    anglesToTest.add(angleGroups[j].angle);
                    if (debugMode) {
                        console.log(`[boundary-based] Found perpendicular pair: ${angleGroups[i].angle.toFixed(1)}° and ${angleGroups[j].angle.toFixed(1)}°`);
                    }
                }
            }
        }
    }

    // Always test axis-aligned (0°)
    anglesToTest.add(0);

    const angles = Array.from(anglesToTest).sort((a, b) => a - b);

    if (forceDebug) {
        console.log(`[boundary-based] Testing ${angles.length} angles: ${angles.map(a => a.toFixed(1)).join(', ')}`);
    }

    // Test each angle
    let bestRect = null;
    let bestArea = 0;

    // Time-based cutoff: stop if we exceed 300ms
    const maxComputeTime = 300; // milliseconds

    for (const angle of angles) {
        // Check time limit
        const elapsedTime = performance.now() - startTime;
        if (elapsedTime > maxComputeTime && bestRect) {
            if (forceDebug) {
                console.log(`[boundary-based] ⏱️ Time limit reached (${maxComputeTime}ms), stopping with best result (${angles.length - angles.indexOf(angle)} angles remaining)`);
            }
            break;
        }

        const rect = findMaxRectangleAtAngle(polygon, angle, forceDebug, targetArea, adaptiveGridSteps, adaptiveAspectRatios, centroidStrategy, pathCount || 2);

        if (forceDebug) {
            if (rect) {
                console.log(`[boundary-based] Angle ${angle.toFixed(1)}°: ${rect.width.toFixed(1)} × ${rect.height.toFixed(1)} = ${rect.area.toFixed(1)} sq px`);
            } else {
                console.log(`[boundary-based] Angle ${angle.toFixed(1)}°: No valid rectangle found`);
            }
        }

        if (rect && rect.area > bestArea) {
            bestArea = rect.area;
            bestRect = rect;

            if (forceDebug) {
                console.log(`[boundary-based] ✓ New best at ${angle.toFixed(1)}°: ${rect.width.toFixed(1)} × ${rect.height.toFixed(1)} = ${rect.area.toFixed(1)} sq px`);
            }
        }
    }

    // Apply edge expansion to push rectangle to boundary limits
    // ONLY for simpler polygons (<=10 vertices) - complex polygons with concavities often fail edge expansion
    if (bestRect && bestRect.area > 0 && polygon.length <= 10) {
        const expandedRect = expandRectangleEdges(bestRect, polygon, debugMode);

        // Only use expanded rectangle if it's valid and larger
        if (expandedRect && expandedRect.area > bestRect.area) {
            bestRect = expandedRect;
        } else if (debugMode && expandedRect && expandedRect.area <= bestRect.area) {
            console.log(`[boundary-based] Edge expansion did not improve area, keeping original`);
        }
    } else if (debugMode && bestRect && polygon.length > 10) {
        console.log(`[boundary-based] Skipping edge expansion for complex polygon with ${polygon.length} vertices`);
    }

    const endTime = performance.now();

    if (bestRect) {
        // FINAL VALIDATION: Verify all corners AND edges are actually inside the polygon
        const corners = bestRect.corners;
        let isValid = true;

        // Log polygon for debugging
        if (debugMode) {
            console.log(`[boundary-based] Validating against polygon with ${polygon.length} vertices`);
            console.log(`[boundary-based] Polygon points:`, polygon.map(p => `(${p.x.toFixed(2)}, ${p.y.toFixed(2)})`).join(', '));
        }

        // Check corners
        for (let i = 0; i < corners.length; i++) {
            const isInside = isPointInPolygonSlow(corners[i], polygon);
            if (debugMode) {
                console.log(`[boundary-based] Corner ${i} at (${corners[i].x.toFixed(2)}, ${corners[i].y.toFixed(2)}) - inside: ${isInside}`);
            }
            if (!isInside) {
                isValid = false;
                console.error(`[boundary-based] VALIDATION FAILED: Corner ${i} at (${corners[i].x.toFixed(2)}, ${corners[i].y.toFixed(2)}) is OUTSIDE polygon!`);
            }
        }

        // Check edges with DENSE sampling (every 2%) to catch lines that exit polygon
        if (isValid) {
            for (let i = 0; i < corners.length; i++) {
                const c1 = corners[i];
                const c2 = corners[(i + 1) % corners.length];

                // Sample every 2% along edge
                for (let t = 0.02; t < 1.0; t += 0.02) {
                    const sample = {
                        x: c1.x + (c2.x - c1.x) * t,
                        y: c1.y + (c2.y - c1.y) * t
                    };

                    if (!isPointInPolygonSlow(sample, polygon)) {
                        isValid = false;
                        console.error(`[boundary-based] VALIDATION FAILED: Edge ${i}→${(i+1)%corners.length} at t=${t.toFixed(2)} point (${sample.x.toFixed(2)}, ${sample.y.toFixed(2)}) is OUTSIDE polygon!`);
                        break;
                    }
                }

                if (!isValid) break;
            }
        }

        if (!isValid) {
            console.warn(`[boundary-based] Rectangle validation failed - attempting to shrink rectangle`);

            // Try shrinking the rectangle by 10% increments until it fits
            for (let shrinkFactor = 0.9; shrinkFactor >= 0.5; shrinkFactor -= 0.1) {
                const shrunkWidth = bestRect.width * shrinkFactor;
                const shrunkHeight = bestRect.height * shrinkFactor;

                // Recalculate corners for shrunk rectangle
                const halfWidth = shrunkWidth / 2;
                const halfHeight = shrunkHeight / 2;
                const cosA = Math.cos(bestRect.angle);
                const sinA = Math.sin(bestRect.angle);

                const shrunkCorners = [
                    {
                        x: bestRect.x + (-halfWidth * cosA - (-halfHeight) * sinA),
                        y: bestRect.y + (-halfWidth * sinA + (-halfHeight) * cosA)
                    },
                    {
                        x: bestRect.x + (halfWidth * cosA - (-halfHeight) * sinA),
                        y: bestRect.y + (halfWidth * sinA + (-halfHeight) * cosA)
                    },
                    {
                        x: bestRect.x + (halfWidth * cosA - halfHeight * sinA),
                        y: bestRect.y + (halfWidth * sinA + halfHeight * cosA)
                    },
                    {
                        x: bestRect.x + (-halfWidth * cosA - halfHeight * sinA),
                        y: bestRect.y + (-halfWidth * sinA + halfHeight * cosA)
                    }
                ];

                // Validate shrunk rectangle
                let shrunkValid = true;
                for (const corner of shrunkCorners) {
                    if (!isPointInPolygonSlow(corner, polygon)) {
                        shrunkValid = false;
                        break;
                    }
                }

                // Check edges
                if (shrunkValid) {
                    for (let i = 0; i < shrunkCorners.length; i++) {
                        const c1 = shrunkCorners[i];
                        const c2 = shrunkCorners[(i + 1) % shrunkCorners.length];

                        for (let t = 0.02; t < 1.0; t += 0.02) {
                            const sample = {
                                x: c1.x + (c2.x - c1.x) * t,
                                y: c1.y + (c2.y - c1.y) * t
                            };

                            if (!isPointInPolygonSlow(sample, polygon)) {
                                shrunkValid = false;
                                break;
                            }
                        }

                        if (!shrunkValid) break;
                    }
                }

                if (shrunkValid) {
                    console.warn(`[boundary-based] Successfully shrunk rectangle to ${(shrinkFactor * 100).toFixed(0)}% of original size`);
                    bestRect.width = shrunkWidth;
                    bestRect.height = shrunkHeight;
                    bestRect.area = shrunkWidth * shrunkHeight;
                    bestRect.corners = shrunkCorners;
                    isValid = true;
                    break;
                }
            }

            if (!isValid) {
                console.error(`[boundary-based] Rectangle validation failed even after shrinking. Returning null.`);
                console.error(`[boundary-based] Polygon had ${polygon.length} vertices`);
                return null;
            }
        }

        bestRect.type = 'boundary-based';
        bestRect.computeTime = endTime - startTime;
        bestRect.centroidStrategy = centroidStrategy;  // Add centroid strategy to result

        if (debugMode) {
            console.log(`[boundary-based] Best result: ${bestRect.width.toFixed(1)} × ${bestRect.height.toFixed(1)} at ${bestRect.angle.toFixed(1)}° = ${bestRect.area.toFixed(1)} sq px in ${bestRect.computeTime.toFixed(1)}ms`);
        }
    }

    return bestRect;
}

/**
 * Hybrid algorithm: tries boundary-based first (fast), then falls back to optimized (thorough)
 * Returns the best result from either approach
 */
function hybridInscribedRectangle(polygon, options = {}) {
    const {
        debugMode = false,
        coverageThreshold = 0.96,  // If boundary-based achieves this, skip optimized
        // Boundary-based options
        maxAngles = 6,  // Slightly more for better coverage
        angleTolerance = 5,
        testPerpendicular = true,
        // Optimized algorithm options (from SvgViewerOptimized.js)
        maxTime = 1000,
        gridStep = 8.0,
        polylabelPrecision = 0.5,
        aspectRatios = [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0],
        binarySearchPrecision = 0.0001,
        binarySearchMaxIterations = 20
    } = options;

    if (!polygon || polygon.length < 3) {
        console.error('[hybrid] Invalid polygon');
        return null;
    }

    const startTime = performance.now();

    // Step 1: Try boundary-based algorithm (fast)
    if (debugMode) {
        console.log('[hybrid] Step 1: Trying boundary-based algorithm...');
    }

    const boundaryResult = boundaryBasedInscribedRectangle(polygon, {
        debugMode: debugMode,  // Pass debug mode through for validation logging
        pathCount: options.pathCount,  // Pass path count for adaptive tuning
        maxAngles,
        angleTolerance,
        testPerpendicular,
        targetArea: options.targetArea  // Pass target area for focused search
    });

    if (debugMode && boundaryResult) {
        console.log(`[hybrid] Boundary-based: ${boundaryResult.area.toFixed(1)} sq px in ${boundaryResult.computeTime.toFixed(1)}ms`);
    }

    // Step 2: Decide whether to run optimized algorithm
    let optimizedResult = null;
    let shouldRunOptimized = true;

    // CRITICAL: Always run optimized if boundary-based failed (returned null)
    // This ensures proper fallback for validation failures and edge cases
    if (!boundaryResult) {
        if (debugMode) {
            console.log('[hybrid] Boundary-based failed, forcing optimized algorithm as fallback');
        }
        shouldRunOptimized = true;
    }
    // Don't run optimized if no target area AND coverageThreshold > 0 AND boundary-based succeeded
    // (If coverageThreshold is 0, we always want to run optimized for max accuracy)
    else if (!options.targetArea && coverageThreshold > 0) {
        if (debugMode) {
            console.log('[hybrid] No target area provided and threshold > 0, using boundary-based result only');
        }
        shouldRunOptimized = false;
    }
    // Check if we have a target area to compare against (passed in options)
    else if (options.targetArea && boundaryResult) {
        // Handle both old format (number) and new format (object with value and bounds)
        const targetValue = typeof options.targetArea === 'object' ? options.targetArea.value : options.targetArea;
        const coverage = boundaryResult.area / targetValue;
        if (coverage >= coverageThreshold) {
            if (debugMode) {
                console.log(`[hybrid] Boundary-based achieved ${(coverage * 100).toFixed(1)}% coverage (threshold: ${(coverageThreshold * 100).toFixed(1)}%), skipping optimized`);
            }
            shouldRunOptimized = false;
        }
    }

    if (shouldRunOptimized) {
        if (debugMode) {
            console.log('[hybrid] Step 2: Running optimized algorithm...');
        }

        // Need to call the optimized algorithm from SvgViewerOptimized.js
        // This assumes fastInscribedRectangle is available in the same context
        if (typeof fastInscribedRectangle !== 'undefined') {
            try {
                optimizedResult = fastInscribedRectangle(polygon, {
                    debugLog: false,
                    maxTime,
                    gridStep,
                    polylabelPrecision,
                    aspectRatios,
                    binarySearchPrecision,
                    binarySearchMaxIterations
                });
            } catch (error) {
                console.error('[hybrid] Error running optimized algorithm:', error.message);
                optimizedResult = null;
            }

            if (debugMode && optimizedResult) {
                const time = optimizedResult.computeTime || optimizedResult.time || 0;
                console.log(`[hybrid] Optimized: ${optimizedResult.area.toFixed(1)} sq px in ${time.toFixed(1)}ms`);
            }
        } else if (debugMode) {
            console.warn('[hybrid] fastInscribedRectangle not available, using boundary-based result only');
        }
    }

    // Step 3: Return the best result
    const endTime = performance.now();
    let bestResult = null;

    if (!boundaryResult && !optimizedResult) {
        console.error('[hybrid] Both algorithms failed');
        return null;
    }

    if (!optimizedResult) {
        bestResult = boundaryResult;
    } else if (!boundaryResult) {
        bestResult = optimizedResult;
        // Optimized result doesn't have type field, add it
        if (!bestResult.type) {
            bestResult.type = 'optimized';
        }
    } else {
        // Both succeeded - ALWAYS prefer optimized result for pre-computation accuracy
        bestResult = optimizedResult;
        // Optimized result doesn't have type field, add it
        if (!bestResult.type) {
            bestResult.type = 'optimized';
        }
    }

    // Add hybrid metadata
    bestResult.hybridTotalTime = endTime - startTime;
    bestResult.usedBoundaryBased = boundaryResult !== null;
    bestResult.usedOptimized = optimizedResult !== null;
    bestResult.selectedAlgorithm = bestResult.type;

    if (debugMode) {
        console.log(`[hybrid] Selected: ${bestResult.type} with ${bestResult.area.toFixed(1)} sq px`);
        console.log(`[hybrid] Total time: ${bestResult.hybridTotalTime.toFixed(1)}ms`);
    }

    return bestResult;
}

// Export for use in Node.js and browser
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        boundaryBasedInscribedRectangle,
        extractBoundaryEdges,
        findDominantAngles,
        findMaxRectangleAtAngle,
        hybridInscribedRectangle
    };
}
