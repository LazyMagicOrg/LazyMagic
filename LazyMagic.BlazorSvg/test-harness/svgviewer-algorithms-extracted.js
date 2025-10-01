// Extracted pure algorithm functions from SvgViewer.js
// These are standalone, testable versions without Blazor/DOM dependencies

// ===== Polygon Utility Functions =====

export function calculatePolygonArea(points) {
    let area = 0;
    for (let i = 0; i < points.length; i++) {
        const current = points[i];
        const next = points[(i + 1) % points.length];
        area += current.x * next.y - next.x * current.y;
    }
    return Math.abs(area) / 2;
}

export function getPolygonBounds(points) {
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

export function basicPolygonCleanup(points) {
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
        const bounds = getPolygonBounds(cleaned);
        const boundingArea = (bounds.maxX - bounds.minX) * (bounds.maxY - bounds.minY);
        const polygonArea = calculatePolygonArea(cleaned);
        const areaRatio = polygonArea / boundingArea;

        if (areaRatio < 0.8) {
            console.warn(`[winding] Detected corrupted 6-vertex polygon (area ratio: ${areaRatio.toFixed(3)}), forcing failure to trigger convex hull`);
            return null; // Force failure to trigger convex hull fallback
        }
    }

    return cleaned;
}

// ===== Rectangle/Geometric Helper Functions =====

export function isAxisAligned(corners) {
    // Simple check: all x-coordinates should be one of two values
    // and all y-coordinates should be one of two values
    const xValues = [...new Set(corners.map(p => Math.round(p.x)))];
    const yValues = [...new Set(corners.map(p => Math.round(p.y)))];
    return xValues.length === 2 && yValues.length === 2;
}

export function getRectangleRotation(corners) {
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

export function getBounds(corners) {
    return {
        minX: Math.min(...corners.map(p => p.x)),
        maxX: Math.max(...corners.map(p => p.x)),
        minY: Math.min(...corners.map(p => p.y)),
        maxY: Math.max(...corners.map(p => p.y))
    };
}

export function areRectanglesAdjacent(pathCorners) {
    if (pathCorners.length !== 2) return false; // For now, only handle 2 rectangles

    const rect1 = getBounds(pathCorners[0]);
    const rect2 = getBounds(pathCorners[1]);

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

export function rectanglesFormSimpleUnion(rect1, rect2) {
    const tolerance = 5;

    // Check if they align on top/bottom edges (side-by-side with same height range)
    const sameHeight = Math.abs(rect1.minY - rect2.minY) < tolerance &&
                      Math.abs(rect1.maxY - rect2.maxY) < tolerance;

    // Check if they align on left/right edges (stacked with same width range)
    const sameWidth = Math.abs(rect1.minX - rect2.minX) < tolerance &&
                     Math.abs(rect1.maxX - rect2.maxX) < tolerance;

    return sameHeight || sameWidth;
}

export function findSharedVertices(pathBoundaryPoints) {
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

// ===== Point & Distance Math =====

// ===== Geometric Algorithms =====

// Orientation helper function (used by line intersection and convex hull)
export function orientation(p, q, r) {
    const val = (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);
    if (val === 0) return 0; // Collinear
    return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
}

// Check if two line segments intersect
export function doLinesIntersect(p1, p2, p3, p4) {
    const o1 = orientation(p1, p2, p3);
    const o2 = orientation(p1, p2, p4);
    const o3 = orientation(p3, p4, p1);
    const o4 = orientation(p3, p4, p2);

    // General case
    if (o1 !== o2 && o3 !== o4) return true;
    return false; // No intersection
}

// Check for self-intersection in hull
export function hasSelfintersection(hull) {
    for (let i = 0; i < hull.length; i++) {
        const edge1Start = hull[i];
        const edge1End = hull[(i + 1) % hull.length];

        for (let j = i + 2; j < hull.length; j++) {
            if (j === hull.length - 1 && i === 0) continue; // Skip adjacent edges

            const edge2Start = hull[j];
            const edge2End = hull[(j + 1) % hull.length];

            if (doLinesIntersect(edge1Start, edge1End, edge2Start, edge2End)) {
                return true; // Has self-intersection
            }
        }
    }

    return false; // No self-intersection
}

// Point-in-polygon test using ray casting algorithm (pure version without spatial grid)
export function isPointInPolygon(point, polygon) {
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

// Calculate distance between two points
export function calculateDistance(p1, p2) {
    return Math.sqrt(Math.pow(p2.x - p1.x, 2) + Math.pow(p2.y - p1.y, 2));
}

// Remove duplicate points
export function removeDuplicates(points, tolerance) {
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

// Simple convex hull (gift wrapping algorithm)
export function simpleConvexHull(points) {
    if (points.length < 3) return points;

    const pts = removeDuplicates(points, 2.0);
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

    do {
        hull.push(pts[current]);
        let next = (current + 1) % pts.length;

        for (let i = 0; i < pts.length; i++) {
            if (orientation(pts[current], pts[i], pts[next]) === 2) {
                next = i;
            }
        }

        current = next;
    } while (current !== leftmost);

    return hull;
}

export function distanceToLineSegment(point, segStart, segEnd) {
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

export function minDistToSet(pt, set) {
    let best = Infinity;
    for (let i = 0; i < set.length; i++) {
        const dx = set[i].x - pt.x, dy = set[i].y - pt.y;
        const d = Math.hypot(dx, dy);
        if (d < best) best = d;
    }
    return best;
}

export function downsamplePoints(points, everyN = 2) {
    if (everyN <= 1) return points;
    const out = [];
    for (let i = 0; i < points.length; i += everyN) out.push(points[i]);
    return out;
}

export function pointsMatch(p1, p2, tolerance) {
    const dx = p1.x - p2.x;
    const dy = p1.y - p2.y;
    return Math.sqrt(dx * dx + dy * dy) <= tolerance;
}

// ===== SVG Path Parsing =====

export function extractPathPoints(pathData) {
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

export function parsePathToLineSegments(pathData, pathIdx) {
    const segments = [];

    try {
        // Extract only the actual SVG command points (4 points per path)
        const boundaryPoints = extractPathPoints(pathData);

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

// ===== Orientation Detection =====

export function detectPolygonOrientation(polygon) {
    if (!polygon || polygon.length < 3) return 0;

    // For parallelograms/rectangles, we want the angle that maximizes the width
    // Test all edge angles and their perpendiculars to find which gives the widest bounding box
    const edgeAngles = [];

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];
        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const length = Math.sqrt(dx * dx + dy * dy);
        const angle = Math.atan2(dy, dx) * 180 / Math.PI;
        edgeAngles.push({ angle, length });
    }

    // Test each unique angle to see which gives the maximum width
    let bestAngle = 0;
    let maxWidth = 0;

    const testedAngles = new Set();
    for (const edge of edgeAngles) {
        // Normalize angle to 0-180 range (direction doesn't matter for width)
        let testAngle = edge.angle;
        while (testAngle < 0) testAngle += 180;
        while (testAngle >= 180) testAngle -= 180;

        // Skip if we've already tested this angle
        const angleKey = Math.round(testAngle * 10) / 10;
        if (testedAngles.has(angleKey)) continue;
        testedAngles.add(angleKey);

        // Calculate rotated bounding box width at this angle
        const angleRad = testAngle * Math.PI / 180;
        const cos = Math.cos(angleRad);
        const sin = Math.sin(angleRad);

        let minRotX = Infinity, maxRotX = -Infinity;
        for (const p of polygon) {
            const rotX = p.x * cos + p.y * sin;
            minRotX = Math.min(minRotX, rotX);
            maxRotX = Math.max(maxRotX, rotX);
        }

        const width = maxRotX - minRotX;
        console.debug(`[orientation] Testing angle ${testAngle.toFixed(1)}°: width=${width.toFixed(1)}px`);

        if (width > maxWidth) {
            maxWidth = width;
            bestAngle = testAngle;
        }
    }

    console.debug(`[orientation] Best orientation: ${bestAngle.toFixed(1)}° with width ${maxWidth.toFixed(1)}px`);

    return bestAngle;
}

export function calculateParallelogramRectangle(polygon, angle) {
    console.warn(`🔍 [PARALLELOGRAM-ENTRY] Function called with angle parameter = ${angle}°`);
    if (polygon.length !== 4) return null;

    // Calculate edge lengths
    const edges = [];
    for (let i = 0; i < 4; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % 4];
        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const length = Math.sqrt(dx * dx + dy * dy);
        const edgeAngle = Math.atan2(dy, dx) * 180 / Math.PI;
        edges.push({ length, angle: edgeAngle, p1, p2 });
    }

    console.debug(`[parallelogram] Edge lengths: ${edges.map(e => e.length.toFixed(1)).join(', ')}`);
    console.debug(`[parallelogram] Edge angles: ${edges.map(e => e.angle.toFixed(1)).join(', ')}°`);
    console.warn(`🔍 [PARALLELOGRAM-ANGLE] Input angle param = ${angle}°, will use for rectangle rotation`);

    // For a parallelogram, opposite edges should be roughly equal
    const isParallelogram =
        Math.abs(edges[0].length - edges[2].length) < 5 &&
        Math.abs(edges[1].length - edges[3].length) < 5;

    if (!isParallelogram) {
        console.debug(`[parallelogram] Not a parallelogram - edges don't match`);
        return null;
    }

    // The inscribed rectangle at the parallelogram's angle should have dimensions
    // equal to the parallelogram's edge lengths
    // Determine which edges align with the width (orientation angle) vs height (perpendicular)

    // Normalize angles to 0-180 for comparison
    const normalizeAngle = (a) => {
        let normalized = a;
        while (normalized < 0) normalized += 180;
        while (normalized >= 180) normalized -= 180;
        return normalized;
    };

    const targetAngle = normalizeAngle(angle);
    const edge0Angle = normalizeAngle(edges[0].angle);
    const edge1Angle = normalizeAngle(edges[1].angle);

    const diff0 = Math.min(
        Math.abs(edge0Angle - targetAngle),
        Math.abs(edge0Angle - targetAngle + 180),
        Math.abs(edge0Angle - targetAngle - 180)
    );
    const diff1 = Math.min(
        Math.abs(edge1Angle - targetAngle),
        Math.abs(edge1Angle - targetAngle + 180),
        Math.abs(edge1Angle - targetAngle - 180)
    );

    let width, height;
    if (diff0 < diff1) {
        // Edges 0 and 2 align with the orientation angle (width)
        // Check if opposite edges differ significantly (trapezoid-like)
        const widthDiff = Math.abs(edges[0].length - edges[2].length);
        const heightDiff = Math.abs(edges[1].length - edges[3].length);

        if (widthDiff > 1) {
            // Use minimum of opposite edges for trapezoid-like shapes
            width = Math.min(edges[0].length, edges[2].length);
            console.debug(`[parallelogram] Width edges differ by ${widthDiff.toFixed(1)}px, using minimum (${width.toFixed(1)}px) for safe fit`);
        } else {
            width = (edges[0].length + edges[2].length) / 2;
        }

        if (heightDiff > 1) {
            height = Math.min(edges[1].length, edges[3].length);
            console.debug(`[parallelogram] Height edges differ by ${heightDiff.toFixed(1)}px, using minimum (${height.toFixed(1)}px) for safe fit`);
        } else {
            height = (edges[1].length + edges[3].length) / 2;
        }

        console.debug(`[parallelogram] Edges 0,2 (${edges[0].angle.toFixed(1)}°) align with width direction`);
    } else {
        // Edges 1 and 3 align with the orientation angle (width)
        const widthDiff = Math.abs(edges[1].length - edges[3].length);
        const heightDiff = Math.abs(edges[0].length - edges[2].length);

        if (widthDiff > 1) {
            width = Math.min(edges[1].length, edges[3].length);
            console.debug(`[parallelogram] Width edges differ by ${widthDiff.toFixed(1)}px, using minimum (${width.toFixed(1)}px) for safe fit`);
        } else {
            width = (edges[1].length + edges[3].length) / 2;
        }

        if (heightDiff > 1) {
            height = Math.min(edges[0].length, edges[2].length);
            console.debug(`[parallelogram] Height edges differ by ${heightDiff.toFixed(1)}px, using minimum (${height.toFixed(1)}px) for safe fit`);
        } else {
            height = (edges[0].length + edges[2].length) / 2;
        }

        console.debug(`[parallelogram] Edges 1,3 (${edges[1].angle.toFixed(1)}°) align with width direction`);
    }

    console.debug(`[parallelogram] Calculated rectangle: ${width.toFixed(1)}×${height.toFixed(1)} at ${angle.toFixed(1)}°`);

    // Calculate centroid
    const centroid = {
        x: (polygon[0].x + polygon[1].x + polygon[2].x + polygon[3].x) / 4,
        y: (polygon[0].y + polygon[1].y + polygon[2].y + polygon[3].y) / 4
    };

    // Calculate corners of the rectangle
    const angleRad = angle * Math.PI / 180;
    const cos = Math.cos(angleRad);
    const sin = Math.sin(angleRad);

    const halfWidth = width / 2;
    const halfHeight = height / 2;

    const corners = [
        {
            x: centroid.x + (-halfWidth * cos - (-halfHeight) * sin),
            y: centroid.y + (-halfWidth * sin + (-halfHeight) * cos)
        },
        {
            x: centroid.x + (halfWidth * cos - (-halfHeight) * sin),
            y: centroid.y + (halfWidth * sin + (-halfHeight) * cos)
        },
        {
            x: centroid.x + (halfWidth * cos - halfHeight * sin),
            y: centroid.y + (halfWidth * sin + halfHeight * cos)
        },
        {
            x: centroid.x + (-halfWidth * cos - halfHeight * sin),
            y: centroid.y + (-halfWidth * sin + halfHeight * cos)
        }
    ];

    return {
        type: 'parallelogram',
        width: width,
        height: height,
        angle: angle,
        area: width * height,
        corners: corners,
        centroid: centroid
    };
}

export function calculateTrapezoidRectangle(polygon, angle) {
    console.warn(`🔍 [TRAPEZOID-ENTRY] Function called with angle parameter = ${angle}°`);
    if (polygon.length !== 4) return null;

    // Calculate edge lengths and angles
    const edges = [];
    for (let i = 0; i < 4; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % 4];
        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const length = Math.sqrt(dx * dx + dy * dy);
        const edgeAngle = Math.atan2(dy, dx) * 180 / Math.PI;
        edges.push({ length, angle: edgeAngle, p1, p2 });
    }

    console.debug(`[trapezoid] Edge lengths: ${edges.map(e => e.length.toFixed(1)).join(', ')}`);
    console.debug(`[trapezoid] Edge angles: ${edges.map(e => e.angle.toFixed(1)).join(', ')}°`);

    // Normalize angles to 0-180 for comparison
    const normalizeAngle = (a) => {
        let normalized = a;
        while (normalized < 0) normalized += 180;
        while (normalized >= 180) normalized -= 180;
        return normalized;
    };

    // Find pairs of parallel edges (angles within 5 degrees)
    const parallelPairs = [];
    for (let i = 0; i < 4; i++) {
        for (let j = i + 1; j < 4; j++) {
            const angle1 = normalizeAngle(edges[i].angle);
            const angle2 = normalizeAngle(edges[j].angle);
            const angleDiff = Math.min(
                Math.abs(angle1 - angle2),
                Math.abs(angle1 - angle2 + 180),
                Math.abs(angle1 - angle2 - 180)
            );
            if (angleDiff < 5) {
                parallelPairs.push({ edge1: i, edge2: j, length1: edges[i].length, length2: edges[j].length });
            }
        }
    }

    if (parallelPairs.length === 0) {
        console.debug(`[trapezoid] No parallel edges found, not a trapezoid`);
        return null;
    }

    console.debug(`[trapezoid] Found ${parallelPairs.length} parallel edge pair(s)`);

    // For a trapezoid, we want the pair of parallel edges that align with the orientation angle
    const targetAngle = normalizeAngle(angle);
    let bestPair = null;
    let bestAngleDiff = Infinity;

    for (const pair of parallelPairs) {
        const edgeAngle = normalizeAngle(edges[pair.edge1].angle);
        const diff = Math.min(
            Math.abs(edgeAngle - targetAngle),
            Math.abs(edgeAngle - targetAngle + 180),
            Math.abs(edgeAngle - targetAngle - 180)
        );
        if (diff < bestAngleDiff) {
            bestAngleDiff = diff;
            bestPair = pair;
        }
    }

    if (!bestPair) {
        console.debug(`[trapezoid] Could not find parallel edges matching orientation`);
        return null;
    }

    // The inscribed rectangle width is the length of the SHORTER parallel edge
    const width = Math.min(bestPair.length1, bestPair.length2);

    // Calculate height as the TRUE perpendicular distance between the two parallel edges
    // Get the parallel edge angle
    const parallelAngleRad = edges[bestPair.edge1].angle * Math.PI / 180;
    const perpAngleRad = parallelAngleRad + Math.PI / 2;

    // Project all 4 vertices onto the perpendicular axis
    const perpProjections = polygon.map(p => p.x * Math.cos(perpAngleRad) + p.y * Math.sin(perpAngleRad));
    const minPerp = Math.min(...perpProjections);
    const maxPerp = Math.max(...perpProjections);
    const height = maxPerp - minPerp;

    console.debug(`[trapezoid] Parallel edges: ${bestPair.length1.toFixed(1)}px and ${bestPair.length2.toFixed(1)}px`);
    console.debug(`[trapezoid] Rectangle width (shorter parallel edge): ${width.toFixed(1)}px`);
    console.debug(`[trapezoid] Rectangle height (perpendicular distance): ${height.toFixed(1)}px`);

    // Calculate the center point of the inscribed rectangle
    // For a trapezoid, the rectangle should be centered along the width direction
    // and positioned at the geometric center in the height direction

    // Find the midpoint of the shorter parallel edge
    const shorterEdgeIdx = bestPair.length1 < bestPair.length2 ? bestPair.edge1 : bestPair.edge2;
    const shorterEdge = edges[shorterEdgeIdx];
    const shorterEdgeMidpoint = {
        x: (shorterEdge.p1.x + shorterEdge.p2.x) / 2,
        y: (shorterEdge.p1.y + shorterEdge.p2.y) / 2
    };

    // Reuse the perpendicular projections already calculated for height
    // The mean perpendicular position is the center of the trapezoid in the height direction
    const meanPerp = (minPerp + maxPerp) / 2;

    // Calculate where the shorter edge's midpoint projects onto the perpendicular axis
    const shorterMidPerp = shorterEdgeMidpoint.x * Math.cos(perpAngleRad) + shorterEdgeMidpoint.y * Math.sin(perpAngleRad);

    // Offset needed to center the rectangle
    const perpOffset = meanPerp - shorterMidPerp;

    const centroid = {
        x: shorterEdgeMidpoint.x + perpOffset * Math.cos(perpAngleRad),
        y: shorterEdgeMidpoint.y + perpOffset * Math.sin(perpAngleRad)
    };

    console.debug(`[trapezoid] Rectangle centered at (${centroid.x.toFixed(1)}, ${centroid.y.toFixed(1)})`);

    // Calculate corners of the rectangle
    const angleRad = angle * Math.PI / 180;
    const cos = Math.cos(angleRad);
    const sin = Math.sin(angleRad);

    const halfWidth = width / 2;
    const halfHeight = height / 2;

    const corners = [
        {
            x: centroid.x + (-halfWidth * cos - (-halfHeight) * sin),
            y: centroid.y + (-halfWidth * sin + (-halfHeight) * cos)
        },
        {
            x: centroid.x + (halfWidth * cos - (-halfHeight) * sin),
            y: centroid.y + (halfWidth * sin + (-halfHeight) * cos)
        },
        {
            x: centroid.x + (halfWidth * cos - halfHeight * sin),
            y: centroid.y + (halfWidth * sin + halfHeight * cos)
        },
        {
            x: centroid.x + (-halfWidth * cos - halfHeight * sin),
            y: centroid.y + (-halfWidth * sin + halfHeight * cos)
        }
    ];

    console.debug(`[trapezoid] Calculated inscribed rectangle: ${width.toFixed(1)}×${height.toFixed(1)} at ${angle.toFixed(1)}°`);

    return {
        type: 'trapezoid',
        width: width,
        height: height,
        angle: angle,
        area: width * height,
        corners: corners,
        centroid: centroid
    };
}

export function calculateInscribedRectangle(polygon) {
    const orientation = detectPolygonOrientation(polygon);

    // Try parallelogram first for 4-vertex polygons
    if (polygon.length === 4) {
        const parallelogram = calculateParallelogramRectangle(polygon, orientation);
        if (parallelogram) return parallelogram;

        const trapezoid = calculateTrapezoidRectangle(polygon, orientation);
        if (trapezoid) return trapezoid;
    }

    // For other shapes, return null (would need general algorithm)
    return null;
}

// ===== Network/Graph Algorithms =====

export function markSharedSegments(allSegments, tolerance = 0.01) {
    const markedSegments = allSegments.map(seg => ({
        ...seg,
        isInternal: false
    }));

    let sharedCount = 0;

    for (let i = 0; i < markedSegments.length; i++) {
        const seg1 = markedSegments[i];

        for (let j = i + 1; j < markedSegments.length; j++) {
            const seg2 = markedSegments[j];

            if (seg1.pathIdx === seg2.pathIdx) continue;

            const sameDirection =
                pointsMatch(seg1.start, seg2.start, tolerance) &&
                pointsMatch(seg1.end, seg2.end, tolerance);

            const oppositeDirection =
                pointsMatch(seg1.start, seg2.end, tolerance) &&
                pointsMatch(seg1.end, seg2.start, tolerance);

            if (sameDirection || oppositeDirection) {
                if (!seg1.isInternal) {
                    seg1.isInternal = true;
                    sharedCount++;
                }
                if (!seg2.isInternal) {
                    seg2.isInternal = true;
                    sharedCount++;
                }
            }
        }
    }

    return markedSegments;
}

export function joinPathsIntoNetwork(segments) {
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

    return {
        segments: segments,
        pointToSegments: pointToSegments
    };
}

export function traverseOuterEdge(pathNetwork) {
    const { segments, pointToSegments } = pathNetwork;

    if (segments.length === 0) {
        return [];
    }

    let centroidX = 0, centroidY = 0, pointCount = 0;
    for (const [pointKey, _] of pointToSegments) {
        const [x, y] = pointKey.split('_').map(Number);
        centroidX += x;
        centroidY += y;
        pointCount++;
    }
    const centroid = { x: centroidX / pointCount, y: centroidY / pointCount };

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

    const outerEdgePoints = [startPoint];
    const visitedSegments = new Set();
    let currentPoint = startPoint;
    let currentKey = startKey;
    let incomingAngle = null;

    const maxIterations = segments.length * 2;
    let iterations = 0;

    while (iterations < maxIterations) {
        iterations++;

        const connectedSegments = pointToSegments.get(currentKey) || [];
        const availableSegments = connectedSegments.filter(conn =>
            !visitedSegments.has(conn.segment.id) && !conn.segment.isInternal
        );

        if (availableSegments.length === 0) {
            break;
        }

        let bestSegment = null;
        let bestAngle = null;
        let bestConn = null;

        for (const conn of availableSegments) {
            const segment = conn.segment;
            const nextPoint = conn.isStart ? segment.end : segment.start;
            const outgoingAngle = Math.atan2(nextPoint.y - currentPoint.y, nextPoint.x - currentPoint.x);

            let turnAngle = outgoingAngle;
            if (incomingAngle !== null) {
                turnAngle = outgoingAngle - incomingAngle;
                while (turnAngle < 0) turnAngle += 2 * Math.PI;
                while (turnAngle >= 2 * Math.PI) turnAngle -= 2 * Math.PI;
            }

            const fromCentroidX = currentPoint.x - centroid.x;
            const fromCentroidY = currentPoint.y - centroid.y;
            const toNextX = nextPoint.x - currentPoint.x;
            const toNextY = nextPoint.y - currentPoint.y;

            const dotProduct = fromCentroidX * toNextX + fromCentroidY * toNextY;
            const isOuterFacing = dotProduct > 0;

            let normalizedAngle = turnAngle;
            if (turnAngle > Math.PI) {
                normalizedAngle = 2 * Math.PI - turnAngle;
            }

            let priority = normalizedAngle;

            const currentPointKey = `${currentPoint.x.toFixed(2)}_${currentPoint.y.toFixed(2)}`;
            const currentConnections = pointToSegments.get(currentPointKey) || [];
            if (currentConnections.length >= 4 && isOuterFacing) {
                priority = -3;
            }

            if (incomingAngle !== null) {
                const COLLINEAR_THRESHOLD = 0.1;
                const isNearZero = Math.abs(turnAngle) < COLLINEAR_THRESHOLD;

                if (isNearZero) {
                    priority = -2;
                }
            }

            let bestPriority = bestAngle !== null ?
                (bestAngle > Math.PI ? 2 * Math.PI - bestAngle : bestAngle) : Infinity;
            const COLLINEAR_THRESHOLD = 0.1;
            if (bestAngle !== null && (Math.abs(bestAngle) < COLLINEAR_THRESHOLD || Math.abs(bestAngle - 2 * Math.PI) < COLLINEAR_THRESHOLD)) {
                bestPriority = -1;
            }

            if (bestSegment === null || priority < bestPriority) {
                bestSegment = segment;
                bestAngle = turnAngle;
                bestConn = conn;
            }
        }

        if (!bestSegment) {
            break;
        }

        visitedSegments.add(bestSegment.id);
        const nextPoint = bestConn.isStart ? bestSegment.end : bestSegment.start;

        incomingAngle = Math.atan2(nextPoint.y - currentPoint.y, nextPoint.x - currentPoint.x);
        const nextKey = `${nextPoint.x.toFixed(2)}_${nextPoint.y.toFixed(2)}`;

        if (nextKey === startKey && outerEdgePoints.length > 2) {
            break;
        }

        outerEdgePoints.push(nextPoint);
        currentPoint = nextPoint;
        currentKey = nextKey;
    }

    const validatedPoints = basicPolygonCleanup(outerEdgePoints);
    return validatedPoints || outerEdgePoints;
}

export function mergeCoincidentPoints(allSegments, tolerance) {
    const potentialConnections = [];

    for (let i = 0; i < allSegments.length; i++) {
        const seg1 = allSegments[i];
        for (let j = i + 1; j < allSegments.length; j++) {
            const seg2 = allSegments[j];

            if (seg1.pathIdx === seg2.pathIdx) {
                continue;
            }

            const combinations = [
                { point1: seg1.start, point2: seg2.start, seg1, seg2, end1: 'start', end2: 'start' },
                { point1: seg1.start, point2: seg2.end, seg1, seg2, end1: 'start', end2: 'end' },
                { point1: seg1.end, point2: seg2.start, seg1, seg2, end1: 'end', end2: 'start' },
                { point1: seg1.end, point2: seg2.end, seg1, seg2, end1: 'end', end2: 'end' }
            ];

            for (const combo of combinations) {
                const distance = calculateDistance(combo.point1, combo.point2);

                if (distance <= tolerance) {
                    const point1Key = `${seg1.pathIdx}_${seg1.segmentIdx}_${combo.end1}`;
                    const point2Key = `${seg2.pathIdx}_${seg2.segmentIdx}_${combo.end2}`;

                    const geomKey1 = `${combo.point1.x.toFixed(2)}_${combo.point1.y.toFixed(2)}`;
                    const geomKey2 = `${combo.point2.x.toFixed(2)}_${combo.point2.y.toFixed(2)}`;
                    const pairKey = geomKey1 < geomKey2 ? `${geomKey1}-${geomKey2}` : `${geomKey2}-${geomKey1}`;

                    if (!potentialConnections.some(conn => {
                        const connGeomKey1 = `${conn.point1.x.toFixed(2)}_${conn.point1.y.toFixed(2)}`;
                        const connGeomKey2 = `${conn.point2.x.toFixed(2)}_${conn.point2.y.toFixed(2)}`;
                        const connPairKey = connGeomKey1 < connGeomKey2 ? `${connGeomKey1}-${connGeomKey2}` : `${connGeomKey2}-${connGeomKey1}`;
                        return connPairKey === pairKey;
                    })) {
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
                    }
                }
            }
        }
    }

    return clusterAndMergePoints(allSegments, potentialConnections);
}

export function clusterAndMergePoints(allSegments, potentialConnections) {
    const unionFind = new Map();
    const pointCoords = new Map();

    for (const segment of allSegments) {
        const startKey = `${segment.pathIdx}_${segment.segmentIdx}_start`;
        const endKey = `${segment.pathIdx}_${segment.segmentIdx}_end`;

        unionFind.set(startKey, startKey);
        unionFind.set(endKey, endKey);
        pointCoords.set(startKey, segment.start);
        pointCoords.set(endKey, segment.end);
    }

    const find = (key) => {
        if (unionFind.get(key) !== key) {
            unionFind.set(key, find(unionFind.get(key)));
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

    let unionsApplied = 0;
    for (const connection of potentialConnections) {
        if (union(connection.point1Key, connection.point2Key)) {
            unionsApplied++;
        }
    }

    const clusters = new Map();
    for (const [pointKey, _] of unionFind) {
        const root = find(pointKey);
        if (!clusters.has(root)) {
            clusters.set(root, []);
        }
        clusters.get(root).push(pointKey);
    }

    const superNodes = new Map();
    const pointToSuperNode = new Map();

    for (const [root, pointKeys] of clusters) {
        if (pointKeys.length > 1) {
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
        }
    }

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
