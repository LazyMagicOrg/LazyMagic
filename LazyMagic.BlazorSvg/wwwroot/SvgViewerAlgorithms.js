// Pure algorithm functions for inscribed rectangle calculation
// This file contains no DOM or Blazor dependencies and can be used in both browser and Node.js

(function(global) {
    'use strict';

    const SvgViewerAlgorithms = {

        // ===== Polygon Utility Functions =====

        calculatePolygonArea(points) {
            let area = 0;
            for (let i = 0; i < points.length; i++) {
                const current = points[i];
                const next = points[(i + 1) % points.length];
                area += current.x * next.y - next.x * current.y;
            }
            return Math.abs(area) / 2;
        },

        getPolygonBounds(points) {
            let minX = Infinity, maxX = -Infinity;
            let minY = Infinity, maxY = -Infinity;

            for (const point of points) {
                minX = Math.min(minX, point.x);
                maxX = Math.max(maxX, point.x);
                minY = Math.min(minY, point.y);
                maxY = Math.max(maxY, point.y);
            }

            return { minX, maxX, minY, maxY };
        },

        basicPolygonCleanup(points) {
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
                const bounds = this.getPolygonBounds(cleaned);
                const boundingArea = (bounds.maxX - bounds.minX) * (bounds.maxY - bounds.minY);
                const polygonArea = this.calculatePolygonArea(cleaned);
                const areaRatio = polygonArea / boundingArea;

                if (areaRatio < 0.8) {
                    console.warn(`[winding] Detected corrupted 6-vertex polygon (area ratio: ${areaRatio.toFixed(3)}), forcing failure to trigger convex hull`);
                    return null; // Force failure to trigger convex hull fallback
                }
            }

            return cleaned;
        },

        // ===== Rectangle/Geometric Helper Functions =====

        isAxisAligned(corners) {
            // Simple check: all x-coordinates should be one of two values
            // and all y-coordinates should be one of two values
            const xValues = [...new Set(corners.map(p => Math.round(p.x)))];
            const yValues = [...new Set(corners.map(p => Math.round(p.y)))];
            return xValues.length === 2 && yValues.length === 2;
        },

        getRectangleRotation(corners) {
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
        },

        getBounds(corners) {
            return {
                minX: Math.min(...corners.map(p => p.x)),
                maxX: Math.max(...corners.map(p => p.x)),
                minY: Math.min(...corners.map(p => p.y)),
                maxY: Math.max(...corners.map(p => p.y))
            };
        },

        areRectanglesAdjacent(pathCorners) {
            if (pathCorners.length !== 2) return false; // For now, only handle 2 rectangles

            const rect1 = this.getBounds(pathCorners[0]);
            const rect2 = this.getBounds(pathCorners[1]);

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
        },

        rectanglesFormSimpleUnion(rect1, rect2) {
            const tolerance = 5;

            // Check if they align on top/bottom edges (side-by-side with same height range)
            const sameHeight = Math.abs(rect1.minY - rect2.minY) < tolerance &&
                              Math.abs(rect1.maxY - rect2.maxY) < tolerance;

            // Check if they align on left/right edges (stacked with same width range)
            const sameWidth = Math.abs(rect1.minX - rect2.minX) < tolerance &&
                             Math.abs(rect1.maxX - rect2.maxX) < tolerance;

            return sameHeight || sameWidth;
        },

        findSharedVertices(pathBoundaryPoints) {
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
        },

        // ===== Point & Distance Math =====

        calculateDistance(p1, p2) {
            return Math.sqrt(Math.pow(p2.x - p1.x, 2) + Math.pow(p2.y - p1.y, 2));
        },

        removeDuplicates(points, tolerance) {
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
        },

        // ===== Geometric Algorithms =====

        // Orientation helper function (used by line intersection and convex hull)
        orientation(p, q, r) {
            const val = (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y);
            if (val === 0) return 0; // Collinear
            return (val > 0) ? 1 : 2; // Clockwise or Counterclockwise
        },

        // Check if two line segments intersect
        doLinesIntersect(p1, p2, p3, p4) {
            const o1 = this.orientation(p1, p2, p3);
            const o2 = this.orientation(p1, p2, p4);
            const o3 = this.orientation(p3, p4, p1);
            const o4 = this.orientation(p3, p4, p2);

            // General case
            if (o1 !== o2 && o3 !== o4) return true;
            return false; // No intersection
        },

        // Check for self-intersection in hull
        hasSelfintersection(hull) {
            for (let i = 0; i < hull.length; i++) {
                const edge1Start = hull[i];
                const edge1End = hull[(i + 1) % hull.length];

                for (let j = i + 2; j < hull.length; j++) {
                    if (j === hull.length - 1 && i === 0) continue; // Skip adjacent edges

                    const edge2Start = hull[j];
                    const edge2End = hull[(j + 1) % hull.length];

                    if (this.doLinesIntersect(edge1Start, edge1End, edge2Start, edge2End)) {
                        return true; // Has self-intersection
                    }
                }
            }

            return false; // No self-intersection
        },

        // Point-in-polygon test using ray casting algorithm (pure version without spatial grid)
        isPointInPolygon(point, polygon) {
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
        },

        // Simple convex hull (gift wrapping algorithm)
        simpleConvexHull(points) {
            if (points.length < 3) return points;

            const pts = this.removeDuplicates(points, 2.0);
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
                    if (this.orientation(pts[current], pts[i], pts[next]) === 2) {
                        next = i;
                    }
                }

                current = next;
            } while (current !== leftmost);

            return hull;
        },

        distanceToLineSegment(point, segStart, segEnd) {
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
        },

        minDistToSet(pt, set) {
            let best = Infinity;
            for (let i = 0; i < set.length; i++) {
                const dx = set[i].x - pt.x, dy = set[i].y - pt.y;
                const d = Math.hypot(dx, dy);
                if (d < best) best = d;
            }
            return best;
        },

        downsamplePoints(points, everyN = 2) {
            if (everyN <= 1) return points;
            const out = [];
            for (let i = 0; i < points.length; i += everyN) out.push(points[i]);
            return out;
        },

        pointsMatch(p1, p2, tolerance) {
            const dx = p1.x - p2.x;
            const dy = p1.y - p2.y;
            return Math.sqrt(dx * dx + dy * dy) <= tolerance;
        },

        // ===== SVG Path Parsing =====

        extractPathPoints(pathData) {
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
        },

        parsePathToLineSegments(pathData, pathIdx) {
            const segments = [];

            try {
                // Extract only the actual SVG command points (4 points per path)
                const boundaryPoints = this.extractPathPoints(pathData);

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
        },

        // ===== Orientation Detection =====

        detectPolygonOrientation(polygon) {
            if (!polygon || polygon.length < 3) return 0;

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

            let bestAngle = 0;
            let maxWidth = 0;
            const testedAngles = new Set();

            for (const edge of edgeAngles) {
                let testAngle = edge.angle;
                while (testAngle < 0) testAngle += 180;
                while (testAngle >= 180) testAngle -= 180;

                const angleKey = Math.round(testAngle * 10) / 10;
                if (testedAngles.has(angleKey)) continue;
                testedAngles.add(angleKey);

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
        },

        calculateParallelogramRectangle(polygon, angle) {
            console.warn(`🔍 [PARALLELOGRAM-ENTRY] Function called with angle parameter = ${angle}°`);
            if (polygon.length !== 4) return null;

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

            const isParallelogram =
                Math.abs(edges[0].length - edges[2].length) < 5 &&
                Math.abs(edges[1].length - edges[3].length) < 5;

            if (!isParallelogram) {
                console.debug(`[parallelogram] Not a parallelogram - edges don't match`);
                return null;
            }

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
                const widthDiff = Math.abs(edges[0].length - edges[2].length);
                const heightDiff = Math.abs(edges[1].length - edges[3].length);

                width = widthDiff > 1
                    ? Math.min(edges[0].length, edges[2].length)
                    : (edges[0].length + edges[2].length) / 2;

                if (widthDiff > 1) {
                    console.debug(`[parallelogram] Width edges differ by ${widthDiff.toFixed(1)}px, using minimum (${width.toFixed(1)}px) for safe fit`);
                }

                height = heightDiff > 1
                    ? Math.min(edges[1].length, edges[3].length)
                    : (edges[1].length + edges[3].length) / 2;

                if (heightDiff > 1) {
                    console.debug(`[parallelogram] Height edges differ by ${heightDiff.toFixed(1)}px, using minimum (${height.toFixed(1)}px) for safe fit`);
                }

                console.debug(`[parallelogram] Edges 0,2 (${edges[0].angle.toFixed(1)}°) align with width direction`);
            } else {
                const widthDiff = Math.abs(edges[1].length - edges[3].length);
                const heightDiff = Math.abs(edges[0].length - edges[2].length);

                width = widthDiff > 1
                    ? Math.min(edges[1].length, edges[3].length)
                    : (edges[1].length + edges[3].length) / 2;

                if (widthDiff > 1) {
                    console.debug(`[parallelogram] Width edges differ by ${widthDiff.toFixed(1)}px, using minimum (${width.toFixed(1)}px) for safe fit`);
                }

                height = heightDiff > 1
                    ? Math.min(edges[0].length, edges[2].length)
                    : (edges[0].length + edges[2].length) / 2;

                if (heightDiff > 1) {
                    console.debug(`[parallelogram] Height edges differ by ${heightDiff.toFixed(1)}px, using minimum (${height.toFixed(1)}px) for safe fit`);
                }

                console.debug(`[parallelogram] Edges 1,3 (${edges[1].angle.toFixed(1)}°) align with width direction`);
            }

            console.debug(`[parallelogram] Calculated rectangle: ${width.toFixed(1)}×${height.toFixed(1)} at ${angle.toFixed(1)}°`);

            const centroid = {
                x: (polygon[0].x + polygon[1].x + polygon[2].x + polygon[3].x) / 4,
                y: (polygon[0].y + polygon[1].y + polygon[2].y + polygon[3].y) / 4
            };

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
        },

        calculateTrapezoidRectangle(polygon, angle) {
            console.warn(`🔍 [TRAPEZOID-ENTRY] Function called with angle parameter = ${angle}°`);
            if (polygon.length !== 4) return null;

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

            const normalizeAngle = (a) => {
                let normalized = a;
                while (normalized < 0) normalized += 180;
                while (normalized >= 180) normalized -= 180;
                return normalized;
            };

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

            const width = Math.min(bestPair.length1, bestPair.length2);

            const parallelAngleRad = edges[bestPair.edge1].angle * Math.PI / 180;
            const perpAngleRad = parallelAngleRad + Math.PI / 2;

            const perpProjections = polygon.map(p => p.x * Math.cos(perpAngleRad) + p.y * Math.sin(perpAngleRad));
            const minPerp = Math.min(...perpProjections);
            const maxPerp = Math.max(...perpProjections);
            const height = maxPerp - minPerp;

            console.debug(`[trapezoid] Parallel edges: ${bestPair.length1.toFixed(1)}px and ${bestPair.length2.toFixed(1)}px`);
            console.debug(`[trapezoid] Rectangle width (shorter parallel edge): ${width.toFixed(1)}px`);
            console.debug(`[trapezoid] Rectangle height (perpendicular distance): ${height.toFixed(1)}px`);

            const shorterEdgeIdx = bestPair.length1 < bestPair.length2 ? bestPair.edge1 : bestPair.edge2;
            const shorterEdge = edges[shorterEdgeIdx];
            const shorterEdgeMidpoint = {
                x: (shorterEdge.p1.x + shorterEdge.p2.x) / 2,
                y: (shorterEdge.p1.y + shorterEdge.p2.y) / 2
            };

            const meanPerp = (minPerp + maxPerp) / 2;
            const shorterMidPerp = shorterEdgeMidpoint.x * Math.cos(perpAngleRad) + shorterEdgeMidpoint.y * Math.sin(perpAngleRad);
            const perpOffset = meanPerp - shorterMidPerp;

            const centroid = {
                x: shorterEdgeMidpoint.x + perpOffset * Math.cos(perpAngleRad),
                y: shorterEdgeMidpoint.y + perpOffset * Math.sin(perpAngleRad)
            };

            console.debug(`[trapezoid] Rectangle centered at (${centroid.x.toFixed(1)}, ${centroid.y.toFixed(1)})`);

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
        },

        calculateInscribedRectangle(polygon) {
            const orientation = this.detectPolygonOrientation(polygon);

            if (polygon.length === 4) {
                const parallelogram = this.calculateParallelogramRectangle(polygon, orientation);
                if (parallelogram) return parallelogram;

                const trapezoid = this.calculateTrapezoidRectangle(polygon, orientation);
                if (trapezoid) return trapezoid;
            }

            return null;
        },

        // ===== Network/Graph Algorithms =====

        // Mark shared/internal segments (edges shared between different paths)
        markSharedSegments(allSegments, tolerance = 0.01) {
            console.debug(`[winding] Marking shared/internal segments with exact coordinate matching (tolerance ${tolerance}px)`);

            // Create a copy of segments with isInternal flag
            const markedSegments = allSegments.map(seg => ({
                ...seg,
                isInternal: false
            }));

            let sharedCount = 0;

            // Compare all segment pairs from different paths
            for (let i = 0; i < markedSegments.length; i++) {
                const seg1 = markedSegments[i];

                for (let j = i + 1; j < markedSegments.length; j++) {
                    const seg2 = markedSegments[j];

                    // Only check segments from different paths
                    if (seg1.pathIdx === seg2.pathIdx) continue;

                    // Check if segments overlap (same endpoints, opposite directions)
                    const sameDirection =
                        this.pointsMatch(seg1.start, seg2.start, tolerance) &&
                        this.pointsMatch(seg1.end, seg2.end, tolerance);

                    const oppositeDirection =
                        this.pointsMatch(seg1.start, seg2.end, tolerance) &&
                        this.pointsMatch(seg1.end, seg2.start, tolerance);

                    if (sameDirection || oppositeDirection) {
                        // Mark both segments as internal (shared edge)
                        if (!seg1.isInternal) {
                            seg1.isInternal = true;
                            sharedCount++;
                            console.debug(`[winding] - Segment ${seg1.pathIdx}_${seg1.segmentIdx} marked as INTERNAL (shared with ${seg2.pathIdx}_${seg2.segmentIdx})`);
                        }
                        if (!seg2.isInternal) {
                            seg2.isInternal = true;
                            sharedCount++;
                            console.debug(`[winding] - Segment ${seg2.pathIdx}_${seg2.segmentIdx} marked as INTERNAL (shared with ${seg1.pathIdx}_${seg1.segmentIdx})`);
                        }
                    }
                }
            }

            console.debug(`[winding] Marked ${sharedCount} segments as internal (shared between paths)`);
            return markedSegments;
        },

        // Join paths into a network graph structure
        joinPathsIntoNetwork(segments) {
            console.debug('[winding] Joining paths into network');

            // Ensure segments have IDs
            const segmentsWithIds = segments.map(seg => {
                if (!seg.id && seg.pathIdx !== undefined && seg.segmentIdx !== undefined) {
                    return { ...seg, id: `${seg.pathIdx}_${seg.segmentIdx}` };
                }
                return seg;
            });

            // Create adjacency map: point -> [segments that touch this point]
            const pointToSegments = new Map();

            for (const segment of segmentsWithIds) {
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

            console.debug(`[winding] Created network with ${pointToSegments.size} nodes and ${segmentsWithIds.length} edges`);

            // Debug: show all points in the network
            for (const [pointKey, connections] of pointToSegments) {
                const [x, y] = pointKey.split('_').map(Number);
                console.debug(`[winding] Network point (${x.toFixed(1)}, ${y.toFixed(1)}) connects to ${connections.length} segments: ${connections.map(c => c.segment.id).join(', ')}`);
            }

            return {
                segments: segmentsWithIds,
                pointToSegments: pointToSegments
            };
        },

        // Traverse outer edge using winding algorithm
        traverseOuterEdge(pathNetwork) {
            console.debug('[winding] Traversing outer edge with winding algorithm');

            const { segments, pointToSegments } = pathNetwork;

            if (segments.length === 0) {
                console.warn('[winding] No segments to traverse');
                return [];
            }

            // Calculate centroid of all points for outer-facing detection
            let centroidX = 0, centroidY = 0, pointCount = 0;
            for (const [pointKey, _] of pointToSegments) {
                const [x, y] = pointKey.split('_').map(Number);
                centroidX += x;
                centroidY += y;
                pointCount++;
            }
            const centroid = { x: centroidX / pointCount, y: centroidY / pointCount };
            console.debug(`[winding] Shape centroid: (${centroid.x.toFixed(1)}, ${centroid.y.toFixed(1)})`);

            // Find the leftmost point as starting point (guaranteed to be on outer edge)
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

            console.debug(`[winding] Starting traversal from leftmost point: (${startPoint.x.toFixed(1)}, ${startPoint.y.toFixed(1)})`);

            // Traverse the outer edge by always taking the most clockwise turn (rightmost/outward)
            const outerEdgePoints = [startPoint];
            const visitedSegments = new Set();
            let currentPoint = startPoint;
            let currentKey = startKey;
            let incomingAngle = null; // Angle we came from

            const maxIterations = segments.length * 2; // Prevent infinite loops
            let iterations = 0;

            while (iterations < maxIterations) {
                iterations++;

                const connectedSegments = pointToSegments.get(currentKey) || [];
                const internalSegments = connectedSegments.filter(conn => conn.segment.isInternal);
                const availableSegments = connectedSegments.filter(conn =>
                    !visitedSegments.has(conn.segment.id) && !conn.segment.isInternal
                );

                console.debug(`[winding] Iteration ${iterations}: At point (${currentPoint.x.toFixed(1)}, ${currentPoint.y.toFixed(1)})`);
                console.debug(`[winding] - Connected segments: ${connectedSegments.length}, Internal: ${internalSegments.length}, Available: ${availableSegments.length}`);
                if (internalSegments.length > 0) {
                    console.debug(`[winding] - Excluding internal segments: ${internalSegments.map(conn => conn.segment.id).join(', ')}`);
                }
                console.debug(`[winding] - Visited segments: [${Array.from(visitedSegments).join(', ')}]`);
                if (availableSegments.length > 0) {
                    console.debug(`[winding] - Available segment destinations: ${availableSegments.map(conn => {
                        const nextPt = conn.isStart ? conn.segment.end : conn.segment.start;
                        return `${conn.segment.id}→(${nextPt.x.toFixed(1)},${nextPt.y.toFixed(1)})`;
                    }).join(', ')}`);
                }

                // FAILURE DETECTION: Check if we're being forced into a potentially internal path
                if (availableSegments.length === 1) {
                    const forcedSegment = availableSegments[0].segment;
                    const nextPoint = availableSegments[0].isStart ? forcedSegment.end : forcedSegment.start;
                    const nextKey = `${nextPoint.x.toFixed(2)}_${nextPoint.y.toFixed(2)}`;
                    const nextConnections = pointToSegments.get(nextKey) || [];

                    console.debug(`[winding] - FORCED CHOICE: Only 1 segment available, going to (${nextPoint.x.toFixed(1)}, ${nextPoint.y.toFixed(1)}) with ${nextConnections.length} connections`);

                    // Flag potential internal segment: going to a high-degree vertex (merge point)
                    if (nextConnections.length >= 3) {
                        console.debug(`[winding] - ⚠️  POTENTIAL INTERNAL SEGMENT: Forced to high-degree vertex (${nextConnections.length} connections)`);
                    }
                }

                if (availableSegments.length === 0) {
                    console.debug('[winding] No more available segments, traversal complete');
                    console.debug(`[winding] - Total segments in network: ${segments.length}`);
                    console.debug(`[winding] - Segments visited: ${visitedSegments.size}`);
                    break;
                }

                // Choose the segment that represents the most clockwise turn (smallest angle = most outward)
                let bestSegment = null;
                let bestAngle = null;
                let bestConn = null;

                for (const conn of availableSegments) {
                    const segment = conn.segment;
                    const nextPoint = conn.isStart ? segment.end : segment.start;
                    const outgoingAngle = Math.atan2(nextPoint.y - currentPoint.y, nextPoint.x - currentPoint.x);

                    // Calculate the turn angle relative to incoming direction
                    let turnAngle = outgoingAngle;
                    if (incomingAngle !== null) {
                        turnAngle = outgoingAngle - incomingAngle;
                        // Normalize to [0, 2π)
                        while (turnAngle < 0) turnAngle += 2 * Math.PI;
                        while (turnAngle >= 2 * Math.PI) turnAngle -= 2 * Math.PI;
                    }

                    // Check if this segment leads away from centroid (outer-facing)
                    const fromCentroidX = currentPoint.x - centroid.x;
                    const fromCentroidY = currentPoint.y - centroid.y;
                    const toNextX = nextPoint.x - currentPoint.x;
                    const toNextY = nextPoint.y - currentPoint.y;

                    // Dot product: positive means pointing away from centroid (outer-facing)
                    const dotProduct = fromCentroidX * toNextX + fromCentroidY * toNextY;
                    const isOuterFacing = dotProduct > 0;

                    console.debug(`[winding] - Candidate segment ${segment.id}: to (${nextPoint.x.toFixed(1)}, ${nextPoint.y.toFixed(1)}), turn angle: ${(turnAngle * 180 / Math.PI).toFixed(1)}°, outer-facing: ${isOuterFacing}`);

                    // For outer edge, we want the SMALLEST turn angle (most clockwise = most outward)
                    // Treat angles near 360° as small angles (e.g., 350° -> 10°)
                    let normalizedAngle = turnAngle;
                    if (turnAngle > Math.PI) {
                        normalizedAngle = 2 * Math.PI - turnAngle;
                    }

                    let priority = normalizedAngle;

                    // CRITICAL: At high-degree vertices, strongly prefer outer-facing segments
                    const currentPointKey = `${currentPoint.x.toFixed(2)}_${currentPoint.y.toFixed(2)}`;
                    const currentConnections = pointToSegments.get(currentPointKey) || [];
                    if (currentConnections.length >= 4 && isOuterFacing) {
                        priority = -3; // Even higher priority than collinear for outer-facing at high-degree vertices
                        console.debug(`[winding] - HIGH-DEGREE VERTEX: Prioritizing outer-facing segment at ${currentConnections.length}-connection vertex`);
                    }

                    // Special case: If this is a straight continuation (collinear), give it highest priority
                    if (incomingAngle !== null) {
                        const COLLINEAR_THRESHOLD = 0.1; // Close to 0° (< ~6 degrees)
                        const isNearZero = Math.abs(turnAngle) < COLLINEAR_THRESHOLD;

                        if (isNearZero) {
                            priority = -2; // Higher priority than even sharp turns
                            console.debug(`[winding] - COLLINEAR: Straight continuation detected (turn angle: ${(turnAngle * 180 / Math.PI).toFixed(1)}°), highest priority`);
                        }
                    }

                    let bestPriority = bestAngle !== null ?
                        (bestAngle > Math.PI ? 2 * Math.PI - bestAngle : bestAngle) : Infinity;
                    const COLLINEAR_THRESHOLD = 0.1; // ~6 degrees
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
                    console.debug('[winding] No best segment found, stopping traversal');
                    break;
                }

                // Move to next point
                console.debug(`[winding] - Selected segment ${bestSegment.id} with turn angle ${(bestAngle * 180 / Math.PI).toFixed(1)}°`);
                visitedSegments.add(bestSegment.id);
                const nextPoint = bestConn.isStart ? bestSegment.end : bestSegment.start;
                console.debug(`[winding] - Moving to next point: (${nextPoint.x.toFixed(1)}, ${nextPoint.y.toFixed(1)})`);

                // Update incoming angle for next iteration
                incomingAngle = Math.atan2(nextPoint.y - currentPoint.y, nextPoint.x - currentPoint.x);
                const nextKey = `${nextPoint.x.toFixed(2)}_${nextPoint.y.toFixed(2)}`;

                // Check if we've returned to start
                if (nextKey === startKey && outerEdgePoints.length > 2) {
                    console.debug('[winding] Returned to start point, outer edge complete');
                    break;
                }

                outerEdgePoints.push(nextPoint);
                currentPoint = nextPoint;
                currentKey = nextKey;
            }

            console.debug(`[winding] Traversal completed in ${iterations} iterations, found ${outerEdgePoints.length} outer edge points`);

            // VALIDATION: Basic polygon cleanup before returning
            const validatedPoints = this.basicPolygonCleanup(outerEdgePoints);
            console.debug(`[winding] Polygon validation: ${outerEdgePoints.length} → ${validatedPoints?.length || 0} points`);

            return validatedPoints || outerEdgePoints;
        },

        // Merge coincident points from different paths
        mergeCoincidentPoints(allSegments, tolerance) {
            console.debug(`[winding] Merging adjacent points with ${tolerance}px tolerance (different paths only)`);

            // First, find ALL potential connections within tolerance
            const potentialConnections = [];

            // Find adjacent point pairs between different paths only
            for (let i = 0; i < allSegments.length; i++) {
                const seg1 = allSegments[i];
                for (let j = i + 1; j < allSegments.length; j++) {
                    const seg2 = allSegments[j];

                    // Never merge points from the same path
                    if (seg1.pathIdx === seg2.pathIdx) {
                        continue;
                    }

                    // Check all endpoint combinations
                    const combinations = [
                        { point1: seg1.start, point2: seg2.start, seg1, seg2, end1: 'start', end2: 'start' },
                        { point1: seg1.start, point2: seg2.end, seg1, seg2, end1: 'start', end2: 'end' },
                        { point1: seg1.end, point2: seg2.start, seg1, seg2, end1: 'end', end2: 'start' },
                        { point1: seg1.end, point2: seg2.end, seg1, seg2, end1: 'end', end2: 'end' }
                    ];

                    for (const combo of combinations) {
                        // Check distance for potential connection
                        const distance = this.calculateDistance(combo.point1, combo.point2);

                        console.debug(`[winding] - Checking seg${seg1.pathIdx}_${seg1.segmentIdx}.${combo.end1} (${combo.point1.x.toFixed(1)}, ${combo.point1.y.toFixed(1)}) vs seg${seg2.pathIdx}_${seg2.segmentIdx}.${combo.end2} (${combo.point2.x.toFixed(1)}, ${combo.point2.y.toFixed(1)}) = ${distance.toFixed(1)}px`);

                        if (distance <= tolerance) {
                            const point1Key = `${seg1.pathIdx}_${seg1.segmentIdx}_${combo.end1}`;
                            const point2Key = `${seg2.pathIdx}_${seg2.segmentIdx}_${combo.end2}`;

                            // Create a geometric key to deduplicate same point pairs
                            const geomKey1 = `${combo.point1.x.toFixed(2)}_${combo.point1.y.toFixed(2)}`;
                            const geomKey2 = `${combo.point2.x.toFixed(2)}_${combo.point2.y.toFixed(2)}`;
                            const pairKey = geomKey1 < geomKey2 ? `${geomKey1}-${geomKey2}` : `${geomKey2}-${geomKey1}`;

                            // Only add if we haven't seen this geometric point pair before
                            if (!potentialConnections.some(conn => {
                                const connGeomKey1 = `${conn.point1.x.toFixed(2)}_${conn.point1.y.toFixed(2)}`;
                                const connGeomKey2 = `${conn.point2.x.toFixed(2)}_${conn.point2.y.toFixed(2)}`;
                                const connPairKey = connGeomKey1 < connGeomKey2 ? `${connGeomKey1}-${connGeomKey2}` : `${connGeomKey2}-${connGeomKey1}`;
                                return connPairKey === pairKey;
                            })) {
                                console.debug(`[winding] - POTENTIAL CONNECTION: ${distance.toFixed(1)}px (new geometric pair)`);
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
                            } else {
                                console.debug(`[winding] - DUPLICATE geometric pair: ${distance.toFixed(1)}px (skipping)`);
                            }
                        }
                    }
                }
            }

            console.debug(`[winding] Found ${potentialConnections.length} potential connections`);

            // Cluster all points within tolerance using union-find
            return this.clusterAndMergePoints(allSegments, potentialConnections);
        },

        // Cluster points using union-find and merge them
        clusterAndMergePoints(allSegments, potentialConnections) {
            // Create union-find data structure for clustering
            const unionFind = new Map();
            const pointCoords = new Map();

            // Initialize each point as its own cluster
            for (const segment of allSegments) {
                const startKey = `${segment.pathIdx}_${segment.segmentIdx}_start`;
                const endKey = `${segment.pathIdx}_${segment.segmentIdx}_end`;

                unionFind.set(startKey, startKey);
                unionFind.set(endKey, endKey);
                pointCoords.set(startKey, segment.start);
                pointCoords.set(endKey, segment.end);
            }

            // Union-find helper functions
            const find = (key) => {
                if (unionFind.get(key) !== key) {
                    unionFind.set(key, find(unionFind.get(key))); // Path compression
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

            // Apply unions for all potential connections
            let unionsApplied = 0;
            for (const connection of potentialConnections) {
                if (union(connection.point1Key, connection.point2Key)) {
                    unionsApplied++;
                }
            }

            // Group points by their root cluster
            const clusters = new Map();
            for (const [pointKey, _] of unionFind) {
                const root = find(pointKey);
                if (!clusters.has(root)) {
                    clusters.set(root, []);
                }
                clusters.get(root).push(pointKey);
            }

            // Create super-nodes for clusters with multiple points
            const superNodes = new Map();
            const pointToSuperNode = new Map();

            for (const [root, pointKeys] of clusters) {
                if (pointKeys.length > 1) {
                    // Calculate centroid for super-node
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

            // Apply super-node coordinates to segments
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
        },

        // ===== KD-Tree Implementation =====
        // Optimized for SVG path processing and spatial operations

        KDNode: class {
            constructor(point, left = null, right = null, axis = 0) {
                this.point = point;
                this.left = left;
                this.right = right;
                this.axis = axis; // 0 for x, 1 for y
            }
        },

        KDTree: class {
            constructor(points = [], dimensions = 2) {
                this.dimensions = dimensions;
                this.root = null;

                if (points.length > 0) {
                    this.root = this.build(points, 0);
                }
            }

            build(points, depth = 0) {
                if (points.length === 0) return null;
                if (points.length === 1) return new SvgViewerAlgorithms.KDNode(points[0], null, null, depth % this.dimensions);

                const axis = depth % this.dimensions;

                const sorted = [...points].sort((a, b) => {
                    const aVal = axis === 0 ? a.x : a.y;
                    const bVal = axis === 0 ? b.x : b.y;
                    return aVal - bVal;
                });

                const medianIdx = Math.floor(sorted.length / 2);
                const median = sorted[medianIdx];

                const leftPoints = sorted.slice(0, medianIdx);
                const rightPoints = sorted.slice(medianIdx + 1);

                return new SvgViewerAlgorithms.KDNode(
                    median,
                    this.build(leftPoints, depth + 1),
                    this.build(rightPoints, depth + 1),
                    axis
                );
            }

            insert(point) {
                if (!this.root) {
                    this.root = new SvgViewerAlgorithms.KDNode(point, null, null, 0);
                    return;
                }

                this._insertNode(this.root, point, 0);
            }

            _insertNode(node, point, depth) {
                const axis = depth % this.dimensions;
                const nodeVal = axis === 0 ? node.point.x : node.point.y;
                const pointVal = axis === 0 ? point.x : point.y;

                if (pointVal < nodeVal) {
                    if (node.left === null) {
                        node.left = new SvgViewerAlgorithms.KDNode(point, null, null, (depth + 1) % this.dimensions);
                    } else {
                        this._insertNode(node.left, point, depth + 1);
                    }
                } else {
                    if (node.right === null) {
                        node.right = new SvgViewerAlgorithms.KDNode(point, null, null, (depth + 1) % this.dimensions);
                    } else {
                        this._insertNode(node.right, point, depth + 1);
                    }
                }
            }

            nearest(target, maxDistance = Infinity) {
                if (!this.root) return null;

                let best = { point: null, distance: maxDistance };
                this._nearestSearch(this.root, target, best);

                return best.point;
            }

            _nearestSearch(node, target, best) {
                if (!node) return;

                const distance = this._distance(node.point, target);

                if (distance < best.distance) {
                    best.point = node.point;
                    best.distance = distance;
                }

                const axis = node.axis;
                const diff = axis === 0
                    ? target.x - node.point.x
                    : target.y - node.point.y;

                const nearChild = diff < 0 ? node.left : node.right;
                const farChild = diff < 0 ? node.right : node.left;

                this._nearestSearch(nearChild, target, best);

                if (Math.abs(diff) < best.distance) {
                    this._nearestSearch(farChild, target, best);
                }
            }

            kNearest(target, k, maxDistance = Infinity) {
                if (!this.root || k <= 0) return [];

                const neighbors = [];
                this._kNearestSearch(this.root, target, k, neighbors, maxDistance);

                return neighbors
                    .sort((a, b) => a.distance - b.distance)
                    .slice(0, k)
                    .map(n => n.point);
            }

            _kNearestSearch(node, target, k, neighbors, maxDistance) {
                if (!node) return;

                const distance = this._distance(node.point, target);

                if (distance < maxDistance) {
                    neighbors.push({ point: node.point, distance });
                    neighbors.sort((a, b) => a.distance - b.distance);

                    if (neighbors.length > k) {
                        neighbors.pop();
                    }
                }

                const axis = node.axis;
                const diff = axis === 0
                    ? target.x - node.point.x
                    : target.y - node.point.y;

                const nearChild = diff < 0 ? node.left : node.right;
                const farChild = diff < 0 ? node.right : node.left;

                this._kNearestSearch(nearChild, target, k, neighbors, maxDistance);

                const worstDistance = neighbors.length < k
                    ? maxDistance
                    : neighbors[neighbors.length - 1].distance;

                if (Math.abs(diff) < worstDistance) {
                    this._kNearestSearch(farChild, target, k, neighbors, maxDistance);
                }
            }

            rangeSearch(minX, minY, maxX, maxY) {
                const results = [];
                this._rangeSearchNode(this.root, minX, minY, maxX, maxY, results);
                return results;
            }

            _rangeSearchNode(node, minX, minY, maxX, maxY, results) {
                if (!node) return;

                const point = node.point;

                if (point.x >= minX && point.x <= maxX &&
                    point.y >= minY && point.y <= maxY) {
                    results.push(point);
                }

                const axis = node.axis;
                const splitVal = axis === 0 ? point.x : point.y;
                const rangeMin = axis === 0 ? minX : minY;
                const rangeMax = axis === 0 ? maxX : maxY;

                if (rangeMin <= splitVal) {
                    this._rangeSearchNode(node.left, minX, minY, maxX, maxY, results);
                }

                if (rangeMax >= splitVal) {
                    this._rangeSearchNode(node.right, minX, minY, maxX, maxY, results);
                }
            }

            radiusSearch(center, radius) {
                const results = [];
                const radiusSq = radius * radius;
                this._radiusSearchNode(this.root, center, radiusSq, results);
                return results;
            }

            _radiusSearchNode(node, center, radiusSq, results) {
                if (!node) return;

                const distSq = this._distanceSquared(node.point, center);

                if (distSq <= radiusSq) {
                    results.push({ point: node.point, distance: Math.sqrt(distSq) });
                }

                const axis = node.axis;
                const diff = axis === 0
                    ? center.x - node.point.x
                    : center.y - node.point.y;

                const nearChild = diff < 0 ? node.left : node.right;
                const farChild = diff < 0 ? node.right : node.left;

                this._radiusSearchNode(nearChild, center, radiusSq, results);

                if (diff * diff <= radiusSq) {
                    this._radiusSearchNode(farChild, center, radiusSq, results);
                }
            }

            _distance(a, b) {
                return Math.sqrt(this._distanceSquared(a, b));
            }

            _distanceSquared(a, b) {
                const dx = a.x - b.x;
                const dy = a.y - b.y;
                return dx * dx + dy * dy;
            }

            depth() {
                return this._getDepth(this.root);
            }

            _getDepth(node) {
                if (!node) return 0;
                return 1 + Math.max(this._getDepth(node.left), this._getDepth(node.right));
            }

            size() {
                return this._countNodes(this.root);
            }

            _countNodes(node) {
                if (!node) return 0;
                return 1 + this._countNodes(node.left) + this._countNodes(node.right);
            }
        },

        // ===== Spatial Grid for Accelerated Point-in-Polygon Tests =====

        SpatialGrid: class {
            constructor(polygon, cellSize = 10) {
                this.polygon = polygon;
                this.cellSize = cellSize;

                this.bounds = this._calculateBounds(polygon);

                this.cols = Math.ceil((this.bounds.maxX - this.bounds.minX) / cellSize);
                this.rows = Math.ceil((this.bounds.maxY - this.bounds.minY) / cellSize);

                this.grid = new Array(this.rows);
                for (let i = 0; i < this.rows; i++) {
                    this.grid[i] = new Array(this.cols);
                }

                this._classifyCells();
            }

            _calculateBounds(polygon) {
                let minX = Infinity, minY = Infinity;
                let maxX = -Infinity, maxY = -Infinity;

                for (const point of polygon) {
                    minX = Math.min(minX, point.x);
                    minY = Math.min(minY, point.y);
                    maxX = Math.max(maxX, point.x);
                    maxY = Math.max(maxY, point.y);
                }

                return { minX, minY, maxX, maxY };
            }

            _classifyCells() {
                for (let row = 0; row < this.rows; row++) {
                    for (let col = 0; col < this.cols; col++) {
                        const cellBounds = this._getCellBounds(row, col);
                        this.grid[row][col] = this._classifyCell(cellBounds);
                    }
                }
            }

            _getCellBounds(row, col) {
                const minX = this.bounds.minX + col * this.cellSize;
                const minY = this.bounds.minY + row * this.cellSize;
                const maxX = Math.min(minX + this.cellSize, this.bounds.maxX);
                const maxY = Math.min(minY + this.cellSize, this.bounds.maxY);

                return { minX, minY, maxX, maxY };
            }

            _classifyCell(cellBounds) {
                const corners = [
                    { x: cellBounds.minX, y: cellBounds.minY },
                    { x: cellBounds.maxX, y: cellBounds.minY },
                    { x: cellBounds.maxX, y: cellBounds.maxY },
                    { x: cellBounds.minX, y: cellBounds.maxY }
                ];

                let insideCount = 0;
                for (const corner of corners) {
                    if (this._slowPointInPolygon(corner)) {
                        insideCount++;
                    }
                }

                if (insideCount === 4) {
                    return 'inside';
                } else if (insideCount === 0) {
                    return 'outside';
                } else {
                    return 'boundary';
                }
            }

            _slowPointInPolygon(point) {
                let inside = false;
                const { x, y } = point;
                const polygon = this.polygon;

                for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
                    const xi = polygon[i].x, yi = polygon[i].y;
                    const xj = polygon[j].x, yj = polygon[j].y;

                    const intersect = ((yi > y) !== (yj > y))
                        && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);

                    if (intersect) inside = !inside;
                }

                return inside;
            }

            containsPoint(point) {
                if (point.x < this.bounds.minX || point.x > this.bounds.maxX ||
                    point.y < this.bounds.minY || point.y > this.bounds.maxY) {
                    return false;
                }

                const col = Math.floor((point.x - this.bounds.minX) / this.cellSize);
                const row = Math.floor((point.y - this.bounds.minY) / this.cellSize);

                if (row < 0 || row >= this.rows || col < 0 || col >= this.cols) {
                    return false;
                }

                const cellClass = this.grid[row][col];

                if (cellClass === 'inside') {
                    return true;
                } else if (cellClass === 'outside') {
                    return false;
                } else {
                    return this._slowPointInPolygon(point);
                }
            }

            containsRectangle(minX, minY, maxX, maxY) {
                if (minX < this.bounds.minX || maxX > this.bounds.maxX ||
                    minY < this.bounds.minY || maxY > this.bounds.maxY) {
                    return false;
                }

                const colMin = Math.floor((minX - this.bounds.minX) / this.cellSize);
                const colMax = Math.floor((maxX - this.bounds.minX) / this.cellSize);
                const rowMin = Math.floor((minY - this.bounds.minY) / this.cellSize);
                const rowMax = Math.floor((maxY - this.bounds.minY) / this.cellSize);

                for (let row = rowMin; row <= rowMax; row++) {
                    for (let col = colMin; col <= colMax; col++) {
                        if (row < 0 || row >= this.rows || col < 0 || col >= this.cols) {
                            return false;
                        }

                        const cellClass = this.grid[row][col];
                        if (cellClass === 'outside') {
                            return false;
                        } else if (cellClass === 'boundary') {
                            return this._slowRectangleInPolygon(minX, minY, maxX, maxY);
                        }
                    }
                }

                return true;
            }

            _slowRectangleInPolygon(minX, minY, maxX, maxY) {
                const corners = [
                    { x: minX, y: minY },
                    { x: maxX, y: minY },
                    { x: maxX, y: maxY },
                    { x: minX, y: maxY }
                ];

                for (const corner of corners) {
                    if (!this._slowPointInPolygon(corner)) {
                        return false;
                    }
                }

                const edgeTests = 3;
                for (let i = 1; i <= edgeTests; i++) {
                    const t = i / (edgeTests + 1);

                    if (!this._slowPointInPolygon({ x: minX + t * (maxX - minX), y: minY })) return false;
                    if (!this._slowPointInPolygon({ x: minX + t * (maxX - minX), y: maxY })) return false;

                    if (!this._slowPointInPolygon({ x: minX, y: minY + t * (maxY - minY) })) return false;
                    if (!this._slowPointInPolygon({ x: maxX, y: minY + t * (maxY - minY) })) return false;
                }

                return true;
            }
        }
    };

    // Make classes globally available for instances to use
    if (typeof window !== 'undefined') {
        window.SpatialGrid = SvgViewerAlgorithms.SpatialGrid;
        window.KDTree = SvgViewerAlgorithms.KDTree;
    }

    // Export for browser
    if (typeof window !== 'undefined') {
        window.SvgViewerAlgorithms = SvgViewerAlgorithms;
    }

    // Export for Node.js
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = SvgViewerAlgorithms;
    }

})(typeof window !== 'undefined' ? window : global);
