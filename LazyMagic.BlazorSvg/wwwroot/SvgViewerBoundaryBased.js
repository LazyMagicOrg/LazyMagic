// Boundary-based inscribed rectangle algorithm
// Uses polygon boundary segments to determine optimal rectangle orientation

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
 * Find the largest axis-aligned bounding box that fits inside the polygon
 * when rotated by the given angle
 */
function findMaxRectangleAtAngle(polygon, angleDeg, debugMode = false, targetArea = null) {
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

    // Collect all test centroids
    const testCentroids = [];

    // Add grid of center positions (coarser grid for speed)
    const gridSteps = 8;
    const stepX = width / (gridSteps + 1);
    const stepY = height / (gridSteps + 1);

    for (let i = 1; i <= gridSteps; i++) {
        for (let j = 1; j <= gridSteps; j++) {
            const centerX = minX + i * stepX;
            const centerY = minY + j * stepY;
            testCentroids.push({ x: centerX, y: centerY });
        }
    }

    // Always add the polygon centroid (ensures we don't miss it due to grid alignment)
    testCentroids.push({ x: centroidX, y: centroidY });

    // Also add centroids near each rotated polygon vertex (improves vertex-aligned rectangles)
    for (const p of rotated) {
        testCentroids.push({ x: p.x, y: p.y });
    }

    // If target area is provided, add focused grid in target region
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
            console.log(`[findMaxRect] Added ${targetGridSteps * targetGridSteps + 5} target-focused centroids`);
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
            // Test multiple aspect ratios (coarser for speed)
            const aspectRatios = [0.5, 0.7, 1.0, 1.3, 1.5, 1.8, 2.0, 2.5];

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

    // Sample along edges
    if (valid) {
        for (let i = 0; i < 4; i++) {
            const c1 = newCorners[i];
            const c2 = newCorners[(i + 1) % 4];
            for (let t = 0.1; t < 1.0; t += 0.1) {
                const sample = {
                    x: c1.x + (c2.x - c1.x) * t,
                    y: c1.y + (c2.y - c1.y) * t
                };
                if (!isPointInPolygonSlow(sample, polygon)) {
                    valid = false;
                    if (debugMode) console.log(`[edge-expansion] Edge sample is outside polygon`);
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

        // Also sample along the edge between these two vertices
        if (valid && vertexIndices.length === 2) {
            const v0 = {
                x: corners[vertexIndices[0]].x + normal.x * testDist,
                y: corners[vertexIndices[0]].y + normal.y * testDist
            };
            const v1 = {
                x: corners[vertexIndices[1]].x + normal.x * testDist,
                y: corners[vertexIndices[1]].y + normal.y * testDist
            };

            for (let t = 0.1; t < 1.0; t += 0.1) {
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
        maxAngles = 5,  // Test top N dominant angles (reduced for speed)
        angleTolerance = 5,  // Degrees tolerance for grouping angles
        testPerpendicular = true,  // Also test angles perpendicular to dominant edges
        targetArea = null  // Optional target area for focused search
    } = options;

    if (!polygon || polygon.length < 3) {
        console.error('[boundary-based] Invalid polygon');
        return null;
    }

    const startTime = performance.now();

    // Extract boundary edges
    const edges = extractBoundaryEdges(polygon);

    // Force debug for Test17 (15-vertex polygon)
    const forceDebug = debugMode || polygon.length === 15;

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

    // Add dominant angles
    for (let i = 0; i < Math.min(maxAngles, angleGroups.length); i++) {
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

    // Time-based cutoff: stop if we exceed 300ms (without goal seeker)
    const maxComputeTime = 300; // milliseconds

    for (const angle of angles) {
        // Check if we've exceeded time limit (only when no target area)
        if (!targetArea && (performance.now() - startTime) > maxComputeTime && bestRect) {
            if (forceDebug) {
                console.log(`[boundary-based] ⏱️ Time limit reached (${maxComputeTime}ms), stopping early with best result (${angles.length - angles.indexOf(angle)} angles remaining)`);
            }
            break;
        }

        const rect = findMaxRectangleAtAngle(polygon, angle, forceDebug, targetArea);

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
    if (bestRect && bestRect.area > 0) {
        const expandedRect = expandRectangleEdges(bestRect, polygon, debugMode);

        // Only use expanded rectangle if it's valid and larger
        if (expandedRect && expandedRect.area > bestRect.area) {
            bestRect = expandedRect;
        } else if (debugMode && expandedRect && expandedRect.area <= bestRect.area) {
            console.log(`[boundary-based] Edge expansion did not improve area, keeping original`);
        }
    }

    const endTime = performance.now();

    if (bestRect) {
        bestRect.type = 'boundary-based';
        bestRect.computeTime = endTime - startTime;

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
        maxAngles = 5,  // Reduced for speed
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
        debugMode: false,  // Suppress internal debug to reduce noise
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

    // Don't run optimized if no target area (no goal seeker) - boundary-based is fast enough
    if (!options.targetArea) {
        if (debugMode) {
            console.log('[hybrid] No target area provided, using boundary-based result only');
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
    } else {
        // Both succeeded, pick the better one
        bestResult = boundaryResult.area > optimizedResult.area ? boundaryResult : optimizedResult;
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
