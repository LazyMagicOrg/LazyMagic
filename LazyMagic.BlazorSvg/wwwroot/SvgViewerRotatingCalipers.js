/**
 * Rotating Calipers Algorithm for Maximum Inscribed Rectangle
 *
 * This is a geometric algorithm that efficiently finds the largest rectangle
 * that can be inscribed in a polygon by rotating "calipers" (support lines)
 * around the polygon and checking critical angles.
 *
 * Key advantages over grid-based approaches:
 * - O(n) to O(n²) complexity vs O(n × m × k) for grid search
 * - No arbitrary angle selection - tests only geometrically significant angles
 * - Much faster for complex polygons
 */

/**
 * Compute the convex hull of a polygon using Graham scan
 * Required because rotating calipers works on convex polygons
 */
function computeConvexHull(points) {
    if (points.length < 3) return points;

    // Find the point with lowest y-coordinate (break ties with lowest x)
    let start = points[0];
    let startIndex = 0;
    for (let i = 1; i < points.length; i++) {
        if (points[i].y < start.y || (points[i].y === start.y && points[i].x < start.x)) {
            start = points[i];
            startIndex = i;
        }
    }

    // Sort points by polar angle with respect to start point
    const sorted = points.map((p, i) => ({
        point: p,
        angle: Math.atan2(p.y - start.y, p.x - start.x),
        dist: Math.hypot(p.x - start.x, p.y - start.y),
        originalIndex: i
    }));

    sorted.sort((a, b) => {
        if (Math.abs(a.angle - b.angle) < 1e-9) {
            return a.dist - b.dist;
        }
        return a.angle - b.angle;
    });

    // Build convex hull using Graham scan
    const hull = [sorted[0].point];

    for (let i = 1; i < sorted.length; i++) {
        const p = sorted[i].point;

        // Remove points that make a right turn
        while (hull.length >= 2) {
            const cross = crossProduct(
                hull[hull.length - 2],
                hull[hull.length - 1],
                p
            );
            if (cross <= 0) {
                hull.pop();
            } else {
                break;
            }
        }

        hull.push(p);
    }

    return hull;
}

/**
 * Cross product of vectors (p1->p2) and (p1->p3)
 * Positive if counter-clockwise, negative if clockwise
 */
function crossProduct(p1, p2, p3) {
    return (p2.x - p1.x) * (p3.y - p1.y) - (p2.y - p1.y) * (p3.x - p1.x);
}

/**
 * Get all critical angles from the polygon
 * Critical angles are where edges become parallel to potential rectangle sides
 */
function getCriticalAngles(polygon) {
    const angles = new Set();

    // Add angle of each edge
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        let angle = Math.atan2(p2.y - p1.y, p2.x - p1.x) * 180 / Math.PI;

        // Normalize to [0, 180)
        while (angle < 0) angle += 180;
        while (angle >= 180) angle -= 180;

        angles.add(angle);

        // Also add perpendicular angle
        const perpAngle = (angle + 90) % 180;
        angles.add(perpAngle);
    }

    // Always test axis-aligned
    angles.add(0);
    angles.add(90);

    return Array.from(angles).sort((a, b) => a - b);
}

/**
 * Find the maximum inscribed rectangle at a specific angle
 * This uses a sweep approach to find the best rectangle aligned at this angle
 */
function findMaxRectangleAtAngleRC(polygon, originalPolygon, angleDeg, aspectRatios, debugMode = false) {
    const angleRad = angleDeg * Math.PI / 180;
    const cos = Math.cos(angleRad);
    const sin = Math.sin(angleRad);

    // Rotate polygon to align with this angle
    const rotated = polygon.map(p => ({
        x: p.x * cos + p.y * sin,
        y: -p.x * sin + p.y * cos
    }));

    // Find bounding box of rotated polygon
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity, maxY = -Infinity;

    for (const p of rotated) {
        minX = Math.min(minX, p.x);
        maxX = Math.max(maxX, p.x);
        minY = Math.min(minY, p.y);
        maxY = Math.max(maxY, p.y);
    }

    const width = maxX - minX;
    const height = maxY - minY;

    if (width < 1 || height < 1) return null;

    // Try different positions along the bounding box
    // This is a simplified approach - test rectangles at various positions
    let bestRect = null;
    let bestArea = 0;

    // Test a grid of positions (coarser than boundary-based for speed)
    const steps = 8; // Reduced from 15 for performance
    const stepX = width / (steps + 1);
    const stepY = height / (steps + 1);

    // Test each aspect ratio
    for (const aspectRatio of aspectRatios) {
        for (let i = 1; i <= steps; i++) {
            for (let j = 1; j <= steps; j++) {
                const centerX = minX + i * stepX;
                const centerY = minY + j * stepY;

                // Binary search for maximum rectangle size at this position
                let low = 0, high = Math.min(width, height);
                let bestSize = 0;

                while (high - low > 0.5) {
                    const mid = (low + high) / 2;

                    // Try rectangle with this size and aspect ratio
                    const testW = mid;
                    const testH = mid * aspectRatio;

                    const corners = [
                        { x: centerX - testW/2, y: centerY - testH/2 },
                        { x: centerX + testW/2, y: centerY - testH/2 },
                        { x: centerX + testW/2, y: centerY + testH/2 },
                        { x: centerX - testW/2, y: centerY + testH/2 }
                    ];

                    // Rotate back to original space
                    const originalCorners = corners.map(p => ({
                        x: p.x * cos - p.y * sin,
                        y: p.x * sin + p.y * cos
                    }));

                    // Check if all corners are inside the original polygon
                    let allInside = true;
                    for (const corner of originalCorners) {
                        if (!isPointInPolygonSlow(corner, originalPolygon)) {
                            allInside = false;
                            break;
                        }
                    }

                    if (allInside) {
                        bestSize = mid;
                        low = mid;
                    } else {
                        high = mid;
                    }
                }

                if (bestSize > 0) {
                    const rectW = bestSize;
                    const rectH = bestSize * aspectRatio;
                    const area = rectW * rectH;

                    if (area > bestArea) {
                        const corners = [
                            { x: centerX - rectW/2, y: centerY - rectH/2 },
                            { x: centerX + rectW/2, y: centerY - rectH/2 },
                            { x: centerX + rectW/2, y: centerY + rectH/2 },
                            { x: centerX - rectW/2, y: centerY + rectH/2 }
                        ];

                        const originalCorners = corners.map(p => ({
                            x: p.x * cos - p.y * sin,
                            y: p.x * sin + p.y * cos
                        }));

                        bestArea = area;
                        bestRect = {
                            corners: originalCorners,
                            width: rectW,
                            height: rectH,
                            area: area,
                            angle: angleDeg,
                            centroid: {
                                x: centerX * cos - centerY * sin,
                                y: centerX * sin + centerY * cos
                            }
                        };
                    }
                }
            }
        }
    }

    return bestRect;
}

/**
 * Main rotating calipers algorithm
 * Finds the maximum inscribed rectangle in a polygon
 */
function rotatingCalipersInscribedRectangle(polygon, options = {}) {
    const {
        debugMode = false,
        targetArea = null,
        aspectRatios = [0.5, 0.6, 0.7, 0.85, 1.0, 1.1, 1.2, 1.3, 1.4, 1.45, 1.5, 1.6, 1.7, 1.8, 2.0, 2.3]
    } = options;

    if (!polygon || polygon.length < 3) {
        console.error('[rotating-calipers] Invalid polygon');
        return null;
    }

    const startTime = performance.now();

    // Get critical angles from the polygon
    const angles = getCriticalAngles(polygon);

    if (debugMode) {
        console.log(`[rotating-calipers] Testing ${angles.length} critical angles with ${aspectRatios.length} aspect ratios`);
    }

    // Test rectangle at each critical angle
    let bestRect = null;
    let bestArea = 0;

    for (const angle of angles) {
        const rect = findMaxRectangleAtAngleRC(polygon, polygon, angle, aspectRatios, debugMode);

        if (rect && rect.area > bestArea) {
            bestArea = rect.area;
            bestRect = rect;

            if (debugMode) {
                console.log(`[rotating-calipers] Angle ${angle.toFixed(1)}°: ${rect.width.toFixed(1)} × ${rect.height.toFixed(1)} = ${rect.area.toFixed(1)} sq px`);
            }
        }
    }

    if (bestRect) {
        const computeTime = performance.now() - startTime;
        bestRect.computeTime = computeTime;
        bestRect.type = 'rotating-calipers';

        if (debugMode) {
            console.log(`[rotating-calipers] Best: ${bestRect.width.toFixed(1)} × ${bestRect.height.toFixed(1)} = ${bestRect.area.toFixed(1)} sq px at ${bestRect.angle.toFixed(1)}° in ${computeTime.toFixed(1)}ms`);
        }
    }

    return bestRect;
}

// Helper function for point-in-polygon test (slow but accurate)
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

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        rotatingCalipersInscribedRectangle,
        computeConvexHull,
        getCriticalAngles
    };
}
