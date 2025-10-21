# Algorithm Implementation Reference

This document provides detailed implementation specifications for all geometric algorithms used in the inscribed rectangle system. Use this as a reference when implementing or debugging low-level algorithmic components.

**Related Documentation:**
- [InscribedRectangle-Guide.md](./InscribedRectangle-Guide.md) - High-level architecture and usage
- [BoardroomLayoutPipeline.md](./BoardroomLayoutPipeline.md) - Boardroom-specific implementations

**Last Updated:** 2025-10-21

---

## Table of Contents

1. [Overview](#overview)
2. [Geometric Primitives](#geometric-primitives)
3. [Bounding Box Algorithms](#bounding-box-algorithms)
4. [Rectangle Inscription](#rectangle-inscription)
5. [SVG Path Parsing](#svg-path-parsing)
6. [Edge Cases and Numerical Precision](#edge-cases-and-numerical-precision)
7. [Performance Optimizations](#performance-optimizations)
8. [References](#references)

---

## Overview

### Purpose

This document bridges the gap between high-level algorithm descriptions and actual implementation. It provides:
- Complete, production-ready algorithm implementations
- Mathematical foundations and proofs
- Edge case handling strategies
- Numerical precision considerations
- Performance optimization techniques

### Scope

**Covered:**
- Core geometric primitives (point-in-polygon, line intersection)
- Bounding box calculations (axis-aligned and oriented)
- Rectangle inscription algorithms
- SVG path parsing and transforms
- Precision and edge case handling

**Not Covered:**
- High-level system architecture (see InscribedRectangle-Guide.md)
- Deployment and testing workflows (see other documentation)

---

## Geometric Primitives

### Point-in-Polygon Test (Winding Number Algorithm)

**Purpose:** Determine if a point lies inside a polygon.

**Algorithm:** Winding number - counts how many times the polygon winds around the point.

**Complete Implementation:**

```javascript
/**
 * Test if a point is inside a polygon using the winding number algorithm.
 * @param {Object} point - Point with {x, y} coordinates
 * @param {Array} polygon - Array of points [{x, y}, ...]
 * @returns {boolean} true if point is inside polygon
 */
function isPointInPolygon(point, polygon) {
    let windingNumber = 0;

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        if (p1.y <= point.y) {
            if (p2.y > point.y) {
                // Upward crossing
                if (isLeft(p1, p2, point) > 0) {
                    windingNumber++;
                }
            }
        } else {
            if (p2.y <= point.y) {
                // Downward crossing
                if (isLeft(p1, p2, point) < 0) {
                    windingNumber--;
                }
            }
        }
    }

    return windingNumber !== 0;
}

/**
 * Test if a point is left/on/right of an infinite line.
 * @param {Object} p0 - First line point
 * @param {Object} p1 - Second line point
 * @param {Object} p2 - Point to test
 * @returns {number} >0 for p2 left of line, =0 for on line, <0 for right
 */
function isLeft(p0, p1, p2) {
    return ((p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y));
}
```

**How it works:**
1. Cast a ray from the point to infinity (implicitly upward)
2. Count edge crossings:
   - Upward edge crossing (p1.y ≤ point.y < p2.y): increment if point is left of edge
   - Downward edge crossing (p1.y > point.y ≥ p2.y): decrement if point is right of edge
3. If winding number ≠ 0, point is inside

**Edge Cases:**
- Point exactly on edge: `isLeft()` returns 0, not counted as crossing
- Point at vertex: Only one adjacent edge counts (due to ≤ vs < comparison)
- Horizontal edges: Never count as crossings (p1.y = p2.y)

**Alternative: Ray Casting Algorithm**

Simpler but less robust for edge cases:

```javascript
function isPointInPolygonRayCast(point, polygon) {
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
```

**When to use:**
- **Winding Number:** Concave polygons, points on edges
- **Ray Casting:** Simple convex polygons, performance-critical code

---

### Line Segment Intersection

**Purpose:** Determine if two line segments intersect (not including endpoints).

**Algorithm:** Parametric line equations with cross products.

**Complete Implementation:**

```javascript
/**
 * Test if two line segments intersect (excluding endpoints).
 * @param {Object} p1 - Start of first segment
 * @param {Object} p2 - End of first segment
 * @param {Object} p3 - Start of second segment
 * @param {Object} p4 - End of second segment
 * @returns {boolean} true if segments intersect in their interiors
 */
function segmentsIntersect(p1, p2, p3, p4) {
    // Compute denominator (cross product of direction vectors)
    const denominator = (p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y);

    // Parallel or coincident lines
    if (Math.abs(denominator) < 1e-10) {
        return false;
    }

    // Compute parametric intersection points
    const ua = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x)) / denominator;
    const ub = ((p2.x - p1.x) * (p1.y - p3.y) - (p2.y - p1.y) * (p1.x - p3.x)) / denominator;

    // Use epsilon to exclude endpoints
    const epsilon = 1e-6;

    // Check if intersection is in interior of both segments
    return (ua > epsilon && ua < 1 - epsilon && ub > epsilon && ub < 1 - epsilon);
}
```

**Mathematical Foundation:**

Line segments:
- Segment 1: `P(ua) = p1 + ua * (p2 - p1)` where `0 ≤ ua ≤ 1`
- Segment 2: `P(ub) = p3 + ub * (p4 - p3)` where `0 ≤ ub ≤ 1`

At intersection: `p1 + ua * (p2 - p1) = p3 + ub * (p4 - p3)`

Solve for `ua` and `ub`:
```
ua = [(p4.x - p3.x)(p1.y - p3.y) - (p4.y - p3.y)(p1.x - p3.x)] / denominator
ub = [(p2.x - p1.x)(p1.y - p3.y) - (p2.y - p1.y)(p1.x - p3.x)] / denominator

denominator = (p4.y - p3.y)(p2.x - p1.x) - (p4.x - p3.x)(p2.y - p1.y)
```

**Epsilon Usage:**
- `epsilon = 1e-6`: Excludes endpoints to avoid false positives when segments share vertices
- Interior intersection: `epsilon < ua < 1 - epsilon` AND `epsilon < ub < 1 - epsilon`
- To include endpoints: use `0 ≤ ua ≤ 1` instead

**Edge Cases:**
- Parallel lines: `denominator ≈ 0` → return false
- Coincident lines: Same as parallel (no unique intersection)
- Shared endpoint: Excluded by epsilon
- T-junction: Excluded by epsilon (one segment endpoint touches other segment)

**Variant: Get Intersection Point**

```javascript
function getIntersectionPoint(p1, p2, p3, p4) {
    const denominator = (p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y);

    if (Math.abs(denominator) < 1e-10) {
        return null; // No intersection
    }

    const ua = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x)) / denominator;

    // Check if intersection is on segment 1
    if (ua < 0 || ua > 1) {
        return null;
    }

    // Calculate intersection point
    return {
        x: p1.x + ua * (p2.x - p1.x),
        y: p1.y + ua * (p2.y - p1.y)
    };
}
```

---

### Polygon Area (Shoelace Formula)

**Purpose:** Calculate the signed area of a polygon.

**Algorithm:** Shoelace formula (Gauss's area formula).

**Complete Implementation:**

```javascript
/**
 * Calculate signed area of a polygon using the shoelace formula.
 * @param {Array} polygon - Array of points [{x, y}, ...]
 * @returns {number} Signed area (positive = counterclockwise, negative = clockwise)
 */
function calculatePolygonArea(polygon) {
    let area = 0;

    for (let i = 0; i < polygon.length; i++) {
        const current = polygon[i];
        const next = polygon[(i + 1) % polygon.length];

        // Cross product of consecutive edge vectors
        area += current.x * next.y - next.x * current.y;
    }

    return area / 2;
}

/**
 * Calculate unsigned area of a polygon.
 * @param {Array} polygon - Array of points
 * @returns {number} Absolute area value
 */
function calculatePolygonAreaAbs(polygon) {
    return Math.abs(calculatePolygonArea(polygon));
}
```

**Mathematical Foundation:**

For polygon with vertices `(x₁, y₁), (x₂, y₂), ..., (xₙ, yₙ)`:

```
Area = ½ |Σ(xᵢ * yᵢ₊₁ - xᵢ₊₁ * yᵢ)|
```

**Signed Area:**
- Positive: Counterclockwise (CCW) vertex order
- Negative: Clockwise (CW) vertex order
- Use signed area to detect polygon orientation

**Coordinate System Transformation:**

For SVG coordinates with scale factor `S`:

```javascript
function calculateAreaInSvgInches(polygon, scaleFactor = 48.345845) {
    // 1. Calculate area in pre-transform coordinates
    const areaPreTransform = calculatePolygonAreaAbs(polygon);

    // 2. Apply scale factor (area scales by S²)
    const areaUserUnits = areaPreTransform * (scaleFactor * scaleFactor);

    // 3. Convert to SVG inches (96 user units per inch)
    const UNITS_PER_INCH = 96;
    const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);

    return areaSvgInches;
}
```

---

## Bounding Box Algorithms

### Axis-Aligned Bounding Box (AABB)

**Purpose:** Find smallest rectangle aligned with coordinate axes that contains a polygon.

**Complete Implementation:**

```javascript
/**
 * Calculate axis-aligned bounding box for a polygon.
 * @param {Array} polygon - Array of points
 * @returns {Object} {minX, minY, maxX, maxY, width, height, area}
 */
function getAxisAlignedBoundingBox(polygon) {
    if (polygon.length === 0) {
        return null;
    }

    let minX = Infinity, minY = Infinity;
    let maxX = -Infinity, maxY = -Infinity;

    for (const point of polygon) {
        minX = Math.min(minX, point.x);
        minY = Math.min(minY, point.y);
        maxX = Math.max(maxX, point.x);
        maxY = Math.max(maxY, point.y);
    }

    return {
        minX,
        minY,
        maxX,
        maxY,
        width: maxX - minX,
        height: maxY - minY,
        area: (maxX - minX) * (maxY - minY),
        center: {
            x: (minX + maxX) / 2,
            y: (minY + maxY) / 2
        }
    };
}
```

**Use Cases:**
- Initial bounds check
- Grid generation bounds
- Quick area estimation
- Spatial partitioning

---

### Oriented Bounding Box (Rotating Calipers)

**Purpose:** Find smallest rectangle at any angle that contains a polygon.

**Algorithm:** Rotating calipers on convex hull.

**Complete Implementation:**

```javascript
/**
 * Calculate minimum area oriented bounding box.
 * @param {Array} polygon - Array of points (must be convex hull)
 * @returns {Object} {corners, width, height, area, angle, center}
 */
function getOrientedBoundingBox(polygon) {
    if (polygon.length < 3) {
        return null;
    }

    let minArea = Infinity;
    let bestBox = null;

    // Test each edge of the convex hull
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        // Calculate edge angle
        const angle = Math.atan2(p2.y - p1.y, p2.x - p1.x);

        // Rotate polygon to align edge with X-axis
        const rotated = polygon.map(p => rotatePoint(p, -angle));

        // Get AABB of rotated polygon
        const aabb = getAxisAlignedBoundingBox(rotated);

        if (aabb.area < minArea) {
            minArea = aabb.area;

            // Rotate corners back to original orientation
            const corners = [
                {x: aabb.minX, y: aabb.minY},
                {x: aabb.maxX, y: aabb.minY},
                {x: aabb.maxX, y: aabb.maxY},
                {x: aabb.minX, y: aabb.maxY}
            ].map(p => rotatePoint(p, angle));

            bestBox = {
                corners,
                width: aabb.width,
                height: aabb.height,
                area: aabb.area,
                angle: angle * 180 / Math.PI, // Convert to degrees
                center: rotatePoint(aabb.center, angle)
            };
        }
    }

    return bestBox;
}

/**
 * Rotate a point around the origin.
 * @param {Object} point - Point to rotate
 * @param {number} angle - Angle in radians
 * @returns {Object} Rotated point
 */
function rotatePoint(point, angle) {
    const cos = Math.cos(angle);
    const sin = Math.sin(angle);

    return {
        x: point.x * cos - point.y * sin,
        y: point.x * sin + point.y * cos
    };
}
```

**Optimization:** For non-convex polygons, first compute convex hull:

```javascript
/**
 * Compute convex hull using Graham scan.
 * @param {Array} points - Array of points
 * @returns {Array} Points forming convex hull (CCW order)
 */
function convexHull(points) {
    if (points.length < 3) return points;

    // Sort points by x-coordinate (and y if x is equal)
    const sorted = points.slice().sort((a, b) =>
        a.x !== b.x ? a.x - b.x : a.y - b.y
    );

    // Build lower hull
    const lower = [];
    for (const point of sorted) {
        while (lower.length >= 2 &&
               crossProduct(lower[lower.length - 2], lower[lower.length - 1], point) <= 0) {
            lower.pop();
        }
        lower.push(point);
    }

    // Build upper hull
    const upper = [];
    for (let i = sorted.length - 1; i >= 0; i--) {
        const point = sorted[i];
        while (upper.length >= 2 &&
               crossProduct(upper[upper.length - 2], upper[upper.length - 1], point) <= 0) {
            upper.pop();
        }
        upper.push(point);
    }

    // Remove duplicate endpoints
    lower.pop();
    upper.pop();

    return lower.concat(upper);
}

function crossProduct(o, a, b) {
    return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
}
```

---

## Rectangle Inscription

### Binary Search for Rectangle Expansion

**Purpose:** Find maximum scale factor for a rectangle that fits inside a polygon.

**Complete Implementation:**

```javascript
/**
 * Find maximum scale for a rectangle centered at a point.
 * @param {Object} center - Center point {x, y}
 * @param {number} angle - Rotation angle in radians
 * @param {number} aspectRatio - Width/height ratio
 * @param {Array} polygon - Polygon to fit rectangle in
 * @param {number} maxScale - Maximum scale to test
 * @returns {number} Maximum valid scale factor
 */
function findMaxRectangleScale(center, angle, aspectRatio, polygon, maxScale = 1000) {
    const precision = 0.0001; // Scale precision
    const maxIterations = 20;

    let low = 0;
    let high = maxScale;
    let bestScale = 0;

    for (let iter = 0; iter < maxIterations; iter++) {
        const mid = (low + high) / 2;

        // Create rectangle at this scale
        const rect = createRectangle(center, mid, aspectRatio, angle);

        // Check if rectangle fits inside polygon
        if (rectangleFitsInPolygon(rect, polygon)) {
            bestScale = mid;
            low = mid; // Try larger scale
        } else {
            high = mid; // Try smaller scale
        }

        // Stop if precision reached
        if (high - low < precision) {
            break;
        }
    }

    return bestScale;
}

/**
 * Create rectangle corners given center, scale, aspect ratio, and angle.
 * @param {Object} center - Center point
 * @param {number} scale - Scale factor
 * @param {number} aspectRatio - Width/height ratio
 * @param {number} angle - Rotation angle in radians
 * @returns {Array} Four corners of rectangle
 */
function createRectangle(center, scale, aspectRatio, angle) {
    const halfWidth = scale * aspectRatio / 2;
    const halfHeight = scale / 2;

    // Create corners in local coordinate system
    const localCorners = [
        {x: -halfWidth, y: -halfHeight},
        {x:  halfWidth, y: -halfHeight},
        {x:  halfWidth, y:  halfHeight},
        {x: -halfWidth, y:  halfHeight}
    ];

    // Rotate and translate to world coordinates
    return localCorners.map(corner => {
        const rotated = rotatePoint(corner, angle);
        return {
            x: center.x + rotated.x,
            y: center.y + rotated.y
        };
    });
}

/**
 * Check if rectangle fits inside polygon.
 * @param {Array} rectCorners - Four corners of rectangle
 * @param {Array} polygon - Polygon boundary
 * @returns {boolean} true if rectangle fits
 */
function rectangleFitsInPolygon(rectCorners, polygon) {
    // 1. Check all corners are inside polygon
    for (const corner of rectCorners) {
        if (!isPointInPolygon(corner, polygon)) {
            return false;
        }
    }

    // 2. Check no rectangle edges intersect polygon edges
    for (let i = 0; i < rectCorners.length; i++) {
        const r1 = rectCorners[i];
        const r2 = rectCorners[(i + 1) % rectCorners.length];

        for (let j = 0; j < polygon.length; j++) {
            const p1 = polygon[j];
            const p2 = polygon[(j + 1) % polygon.length];

            if (segmentsIntersect(r1, r2, p1, p2)) {
                return false;
            }
        }
    }

    return true;
}
```

**Why Binary Search:**
- **Continuous:** Scale is continuous (not discrete)
- **Monotonic:** If scale S doesn't fit, S+ε also doesn't fit
- **Efficient:** O(log(maxScale/precision)) iterations vs. O(maxScale) linear search

**Precision Trade-offs:**
- `precision = 0.0001`: Excellent accuracy, ~17 iterations
- `precision = 0.001`: Good accuracy, ~14 iterations
- `precision = 0.01`: Fast, ~10 iterations

---

### Centroid Grid Generation

**Purpose:** Generate candidate centroid positions for rectangle testing.

**Complete Implementation:**

```javascript
/**
 * Generate grid of candidate centroids within a polygon's bounding box.
 * @param {Array} polygon - Polygon to generate centroids for
 * @param {number} gridStep - Spacing between grid points (pixels)
 * @returns {Array} Array of candidate centroid points
 */
function generateCentroidGrid(polygon, gridStep = 8.0) {
    const aabb = getAxisAlignedBoundingBox(polygon);
    const centroids = [];

    // Generate uniform grid
    for (let x = aabb.minX; x <= aabb.maxX; x += gridStep) {
        for (let y = aabb.minY; y <= aabb.maxY; y += gridStep) {
            const point = {x, y};

            // Only include points inside polygon
            if (isPointInPolygon(point, polygon)) {
                centroids.push(point);
            }
        }
    }

    return centroids;
}

/**
 * Generate centroids using multiple strategies.
 * @param {Array} polygon - Polygon boundary
 * @param {Object} options - Generation options
 * @returns {Array} Combined set of candidate centroids
 */
function getMultipleCentroids(polygon, options = {}) {
    const {
        gridStep = 8.0,
        includePolylabel = true,
        includeGeometric = true,
        includeAABBCenter = true
    } = options;

    const centroids = [];

    // 1. Uniform grid
    centroids.push(...generateCentroidGrid(polygon, gridStep));

    // 2. Geometric centroid (average of vertices)
    if (includeGeometric) {
        const geometric = {
            x: polygon.reduce((sum, p) => sum + p.x, 0) / polygon.length,
            y: polygon.reduce((sum, p) => sum + p.y, 0) / polygon.length
        };
        if (isPointInPolygon(geometric, polygon)) {
            centroids.push(geometric);
        }
    }

    // 3. AABB center
    if (includeAABBCenter) {
        const aabb = getAxisAlignedBoundingBox(polygon);
        if (isPointInPolygon(aabb.center, polygon)) {
            centroids.push(aabb.center);
        }
    }

    // 4. Pole of inaccessibility (polylabel algorithm)
    if (includePolylabel) {
        const pole = polylabel(polygon, 0.5);
        centroids.push(pole);
    }

    // Remove duplicates (within tolerance)
    return removeDuplicateCentroids(centroids, 0.1);
}

/**
 * Remove duplicate centroids within tolerance.
 * @param {Array} centroids - Array of points
 * @param {number} tolerance - Distance tolerance for duplicates
 * @returns {Array} Deduplicated centroids
 */
function removeDuplicateCentroids(centroids, tolerance) {
    const unique = [];

    for (const centroid of centroids) {
        let isDuplicate = false;

        for (const existing of unique) {
            const dist = Math.sqrt(
                Math.pow(centroid.x - existing.x, 2) +
                Math.pow(centroid.y - existing.y, 2)
            );

            if (dist < tolerance) {
                isDuplicate = true;
                break;
            }
        }

        if (!isDuplicate) {
            unique.push(centroid);
        }
    }

    return unique;
}
```

**Polylabel Algorithm (Pole of Inaccessibility):**

The point inside the polygon that is farthest from any edge:

```javascript
/**
 * Find pole of inaccessibility (simplified version).
 * @param {Array} polygon - Polygon boundary
 * @param {number} precision - Search precision
 * @returns {Object} Best centroid point {x, y}
 */
function polylabel(polygon, precision = 1.0) {
    const bbox = getAxisAlignedBoundingBox(polygon);

    // Start with grid of cells
    let cellSize = Math.min(bbox.width, bbox.height);
    let h = cellSize / 2;

    // Priority queue of cells (sorted by potential)
    const cellQueue = [];

    // Cover polygon with initial cells
    for (let x = bbox.minX; x < bbox.maxX; x += cellSize) {
        for (let y = bbox.minY; y < bbox.maxY; y += cellSize) {
            const cell = {
                x: x + h,
                y: y + h,
                h: h,
                dist: pointToPolygonDist({x: x + h, y: y + h}, polygon)
            };
            cellQueue.push(cell);
        }
    }

    // Best cell so far
    let bestCell = {x: bbox.center.x, y: bbox.center.y, dist: 0};

    // Iteratively refine
    while (cellQueue.length > 0) {
        // Get cell with highest potential
        const cell = cellQueue.shift();

        // Update best cell if this is better
        if (cell.dist > bestCell.dist) {
            bestCell = cell;
        }

        // Stop if cell can't possibly be better
        if (cell.h < precision) continue;

        // Split cell into four
        h = cell.h / 2;
        cellQueue.push(
            {x: cell.x - h, y: cell.y - h, h, dist: pointToPolygonDist({x: cell.x - h, y: cell.y - h}, polygon)},
            {x: cell.x + h, y: cell.y - h, h, dist: pointToPolygonDist({x: cell.x + h, y: cell.y - h}, polygon)},
            {x: cell.x - h, y: cell.y + h, h, dist: pointToPolygonDist({x: cell.x - h, y: cell.y + h}, polygon)},
            {x: cell.x + h, y: cell.y + h, h, dist: pointToPolygonDist({x: cell.x + h, y: cell.y + h}, polygon)}
        );
    }

    return {x: bestCell.x, y: bestCell.y};
}

function pointToPolygonDist(point, polygon) {
    let minDist = Infinity;

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];
        const dist = pointToSegmentDist(point, p1, p2);
        minDist = Math.min(minDist, dist);
    }

    return isPointInPolygon(point, polygon) ? minDist : -minDist;
}

function pointToSegmentDist(p, a, b) {
    const dx = b.x - a.x;
    const dy = b.y - a.y;

    if (dx === 0 && dy === 0) {
        return Math.sqrt((p.x - a.x) ** 2 + (p.y - a.y) ** 2);
    }

    const t = Math.max(0, Math.min(1,
        ((p.x - a.x) * dx + (p.y - a.y) * dy) / (dx * dx + dy * dy)
    ));

    const closest = {
        x: a.x + t * dx,
        y: a.y + t * dy
    };

    return Math.sqrt((p.x - closest.x) ** 2 + (p.y - closest.y) ** 2);
}
```

---

## SVG Path Parsing

### Path Command Parsing

**Purpose:** Parse SVG path `d` attribute into polygon vertices.

**Complete Implementation:**

```javascript
/**
 * Parse SVG path data into array of points.
 * @param {string} pathData - SVG path d attribute
 * @returns {Array} Array of {x, y} points
 */
function parseSvgPath(pathData) {
    const points = [];
    let currentX = 0, currentY = 0;
    let startX = 0, startY = 0;

    // Split path into commands
    const commands = pathData.match(/[MmLlHhVvCcSsQqTtAaZz][^MmLlHhVvCcSsQqTtAaZz]*/g);

    if (!commands) return points;

    for (const cmd of commands) {
        const type = cmd[0];
        const args = cmd.slice(1).trim().split(/[\s,]+/).map(Number);

        switch (type) {
            case 'M': // Move to (absolute)
                currentX = args[0];
                currentY = args[1];
                startX = currentX;
                startY = currentY;
                points.push({x: currentX, y: currentY});
                break;

            case 'm': // Move to (relative)
                currentX += args[0];
                currentY += args[1];
                startX = currentX;
                startY = currentY;
                points.push({x: currentX, y: currentY});
                break;

            case 'L': // Line to (absolute)
                for (let i = 0; i < args.length; i += 2) {
                    currentX = args[i];
                    currentY = args[i + 1];
                    points.push({x: currentX, y: currentY});
                }
                break;

            case 'l': // Line to (relative)
                for (let i = 0; i < args.length; i += 2) {
                    currentX += args[i];
                    currentY += args[i + 1];
                    points.push({x: currentX, y: currentY});
                }
                break;

            case 'H': // Horizontal line (absolute)
                for (const x of args) {
                    currentX = x;
                    points.push({x: currentX, y: currentY});
                }
                break;

            case 'h': // Horizontal line (relative)
                for (const dx of args) {
                    currentX += dx;
                    points.push({x: currentX, y: currentY});
                }
                break;

            case 'V': // Vertical line (absolute)
                for (const y of args) {
                    currentY = y;
                    points.push({x: currentX, y: currentY});
                }
                break;

            case 'v': // Vertical line (relative)
                for (const dy of args) {
                    currentY += dy;
                    points.push({x: currentX, y: currentY});
                }
                break;

            case 'Z': // Close path
            case 'z':
                // Don't add point - already have start point
                currentX = startX;
                currentY = startY;
                break;

            // Curves - approximate with line segments
            case 'C': // Cubic Bezier (absolute)
                for (let i = 0; i < args.length; i += 6) {
                    const bezier = approximateCubicBezier(
                        {x: currentX, y: currentY},
                        {x: args[i], y: args[i+1]},
                        {x: args[i+2], y: args[i+3]},
                        {x: args[i+4], y: args[i+5]},
                        10 // number of segments
                    );
                    points.push(...bezier.slice(1)); // Skip first point (duplicate)
                    currentX = args[i+4];
                    currentY = args[i+5];
                }
                break;

            case 'Q': // Quadratic Bezier (absolute)
                for (let i = 0; i < args.length; i += 4) {
                    const bezier = approximateQuadraticBezier(
                        {x: currentX, y: currentY},
                        {x: args[i], y: args[i+1]},
                        {x: args[i+2], y: args[i+3]},
                        10 // number of segments
                    );
                    points.push(...bezier.slice(1));
                    currentX = args[i+2];
                    currentY = args[i+3];
                }
                break;

            // Note: Full implementation would include c, s, q, t, a commands
            // For ballroom floor plans, M, L, H, V, Z are typically sufficient
        }
    }

    return points;
}

/**
 * Approximate cubic Bezier curve with line segments.
 */
function approximateCubicBezier(p0, p1, p2, p3, segments) {
    const points = [];

    for (let i = 0; i <= segments; i++) {
        const t = i / segments;
        const t1 = 1 - t;

        const x = t1**3 * p0.x + 3 * t1**2 * t * p1.x +
                  3 * t1 * t**2 * p2.x + t**3 * p3.x;
        const y = t1**3 * p0.y + 3 * t1**2 * t * p1.y +
                  3 * t1 * t**2 * p2.y + t**3 * p3.y;

        points.push({x, y});
    }

    return points;
}

/**
 * Approximate quadratic Bezier curve with line segments.
 */
function approximateQuadraticBezier(p0, p1, p2, segments) {
    const points = [];

    for (let i = 0; i <= segments; i++) {
        const t = i / segments;
        const t1 = 1 - t;

        const x = t1**2 * p0.x + 2 * t1 * t * p1.x + t**2 * p2.x;
        const y = t1**2 * p0.y + 2 * t1 * t * p1.y + t**2 * p2.y;

        points.push({x, y});
    }

    return points;
}
```

### Transform Matrix Application

**Purpose:** Apply SVG transform matrices to coordinates.

**Complete Implementation:**

```javascript
/**
 * Apply transform matrix to a point.
 * @param {Object} point - Point {x, y}
 * @param {Array} matrix - Transform matrix [a, b, c, d, e, f]
 * @returns {Object} Transformed point
 */
function applyTransformMatrix(point, matrix) {
    const [a, b, c, d, e, f] = matrix;

    return {
        x: a * point.x + c * point.y + e,
        y: b * point.x + d * point.y + f
    };
}

/**
 * Parse SVG transform attribute into matrix.
 * @param {string} transformAttr - SVG transform attribute
 * @returns {Array} Transform matrix [a, b, c, d, e, f]
 */
function parseTransform(transformAttr) {
    // Default identity matrix
    let matrix = [1, 0, 0, 1, 0, 0];

    if (!transformAttr) return matrix;

    // Match transform functions
    const transforms = transformAttr.match(/(\w+)\s*\([^)]*\)/g);

    if (!transforms) return matrix;

    for (const transform of transforms) {
        const match = transform.match(/(\w+)\s*\(([^)]*)\)/);
        if (!match) continue;

        const type = match[1];
        const args = match[2].split(/[\s,]+/).map(Number);

        switch (type) {
            case 'matrix':
                matrix = multiplyMatrices(matrix, args);
                break;

            case 'translate':
                const tx = args[0] || 0;
                const ty = args[1] || 0;
                matrix = multiplyMatrices(matrix, [1, 0, 0, 1, tx, ty]);
                break;

            case 'scale':
                const sx = args[0];
                const sy = args[1] || sx;
                matrix = multiplyMatrices(matrix, [sx, 0, 0, sy, 0, 0]);
                break;

            case 'rotate':
                const angle = args[0] * Math.PI / 180;
                const cos = Math.cos(angle);
                const sin = Math.sin(angle);

                if (args.length === 1) {
                    // Rotate around origin
                    matrix = multiplyMatrices(matrix, [cos, sin, -sin, cos, 0, 0]);
                } else {
                    // Rotate around point (cx, cy)
                    const cx = args[1], cy = args[2];
                    matrix = multiplyMatrices(matrix, [1, 0, 0, 1, cx, cy]);
                    matrix = multiplyMatrices(matrix, [cos, sin, -sin, cos, 0, 0]);
                    matrix = multiplyMatrices(matrix, [1, 0, 0, 1, -cx, -cy]);
                }
                break;

            case 'skewX':
                const skewXAngle = args[0] * Math.PI / 180;
                matrix = multiplyMatrices(matrix, [1, 0, Math.tan(skewXAngle), 1, 0, 0]);
                break;

            case 'skewY':
                const skewYAngle = args[0] * Math.PI / 180;
                matrix = multiplyMatrices(matrix, [1, Math.tan(skewYAngle), 0, 1, 0, 0]);
                break;
        }
    }

    return matrix;
}

/**
 * Multiply two transform matrices.
 * @param {Array} m1 - First matrix [a, b, c, d, e, f]
 * @param {Array} m2 - Second matrix
 * @returns {Array} Product matrix
 */
function multiplyMatrices(m1, m2) {
    return [
        m1[0] * m2[0] + m1[2] * m2[1],
        m1[1] * m2[0] + m1[3] * m2[1],
        m1[0] * m2[2] + m1[2] * m2[3],
        m1[1] * m2[2] + m1[3] * m2[3],
        m1[0] * m2[4] + m1[2] * m2[5] + m1[4],
        m1[1] * m2[4] + m1[3] * m2[5] + m1[5]
    ];
}
```

---

## Edge Cases and Numerical Precision

### Epsilon Values

**Purpose:** Handle floating-point precision errors in geometric computations.

**Common Epsilon Values:**

```javascript
const EPSILON = {
    // Point comparison (are two points the same?)
    POINT: 1e-6,

    // Line parallel test (is denominator zero?)
    PARALLEL: 1e-10,

    // Segment endpoint exclusion
    SEGMENT: 1e-6,

    // Area calculation (is area zero?)
    AREA: 1e-8,

    // Distance comparison
    DISTANCE: 1e-4
};
```

**Usage Examples:**

```javascript
// Point equality with epsilon
function pointsEqual(p1, p2, epsilon = EPSILON.POINT) {
    return Math.abs(p1.x - p2.x) < epsilon &&
           Math.abs(p1.y - p2.y) < epsilon;
}

// Value approximately zero
function isZero(value, epsilon = EPSILON.PARALLEL) {
    return Math.abs(value) < epsilon;
}

// Value comparison with tolerance
function approximatelyEqual(a, b, epsilon = EPSILON.DISTANCE) {
    return Math.abs(a - b) < epsilon;
}
```

### Degenerate Polygon Handling

**Purpose:** Detect and handle invalid or degenerate polygons.

**Complete Implementation:**

```javascript
/**
 * Validate polygon and detect degenerate cases.
 * @param {Array} polygon - Polygon to validate
 * @returns {Object} {valid, issues[]}
 */
function validatePolygon(polygon) {
    const issues = [];

    // Minimum vertices
    if (polygon.length < 3) {
        issues.push('Polygon must have at least 3 vertices');
        return {valid: false, issues};
    }

    // Check for duplicate consecutive vertices
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        if (pointsEqual(p1, p2, EPSILON.POINT)) {
            issues.push(`Duplicate vertex at index ${i}: (${p1.x}, ${p1.y})`);
        }
    }

    // Check for zero area (collinear points)
    const area = Math.abs(calculatePolygonArea(polygon));
    if (area < EPSILON.AREA) {
        issues.push(`Polygon has zero or near-zero area: ${area}`);
    }

    // Check for self-intersection
    if (hasSelfIntersection(polygon)) {
        issues.push('Polygon has self-intersecting edges');
    }

    return {
        valid: issues.length === 0,
        issues,
        area
    };
}

/**
 * Detect if polygon has self-intersecting edges.
 * @param {Array} polygon - Polygon to check
 * @returns {boolean} true if self-intersecting
 */
function hasSelfIntersection(polygon) {
    // Check all edge pairs
    for (let i = 0; i < polygon.length; i++) {
        const e1p1 = polygon[i];
        const e1p2 = polygon[(i + 1) % polygon.length];

        // Start at i+2 to skip adjacent edges
        for (let j = i + 2; j < polygon.length; j++) {
            // Don't check edge against itself or adjacent edges
            if (j === (i - 1 + polygon.length) % polygon.length) continue;

            const e2p1 = polygon[j];
            const e2p2 = polygon[(j + 1) % polygon.length];

            if (segmentsIntersect(e1p1, e1p2, e2p1, e2p2)) {
                return true;
            }
        }
    }

    return false;
}

/**
 * Clean polygon by removing duplicate vertices and collinear points.
 * @param {Array} polygon - Polygon to clean
 * @returns {Array} Cleaned polygon
 */
function cleanPolygon(polygon) {
    const cleaned = [];

    for (let i = 0; i < polygon.length; i++) {
        const prev = polygon[(i - 1 + polygon.length) % polygon.length];
        const curr = polygon[i];
        const next = polygon[(i + 1) % polygon.length];

        // Skip if duplicate of next point
        if (pointsEqual(curr, next, EPSILON.POINT)) {
            continue;
        }

        // Skip if collinear with prev and next
        if (areCollinear(prev, curr, next, EPSILON.AREA)) {
            continue;
        }

        cleaned.push(curr);
    }

    return cleaned;
}

/**
 * Check if three points are collinear.
 * @param {Object} p1 - First point
 * @param {Object} p2 - Second point
 * @param {Object} p3 - Third point
 * @param {number} epsilon - Tolerance
 * @returns {boolean} true if collinear
 */
function areCollinear(p1, p2, p3, epsilon = EPSILON.AREA) {
    // Cross product should be zero for collinear points
    const cross = (p2.x - p1.x) * (p3.y - p1.y) -
                  (p2.y - p1.y) * (p3.x - p1.x);

    return Math.abs(cross) < epsilon;
}
```

### Numerical Stability

**Common Issues and Solutions:**

```javascript
/**
 * Safe division with check for near-zero denominator.
 */
function safeDivide(numerator, denominator, epsilon = EPSILON.PARALLEL) {
    if (Math.abs(denominator) < epsilon) {
        return null; // Indicate division by zero
    }
    return numerator / denominator;
}

/**
 * Stable angle calculation avoiding atan2 issues.
 */
function calculateAngle(p1, p2) {
    const dx = p2.x - p1.x;
    const dy = p2.y - p1.y;

    // Handle zero-length vector
    if (Math.abs(dx) < EPSILON.POINT && Math.abs(dy) < EPSILON.POINT) {
        return 0;
    }

    return Math.atan2(dy, dx);
}

/**
 * Stable distance calculation (avoid overflow/underflow).
 */
function distance(p1, p2) {
    const dx = p2.x - p1.x;
    const dy = p2.y - p1.y;

    // Use hypot for numerical stability
    return Math.hypot(dx, dy);
}

/**
 * Normalize angle to [0, 2π) range.
 */
function normalizeAngle(angle) {
    const TWO_PI = 2 * Math.PI;
    angle = angle % TWO_PI;
    return angle < 0 ? angle + TWO_PI : angle;
}

/**
 * Compare angles with wraparound handling.
 */
function anglesEqual(angle1, angle2, epsilon = 0.01) {
    const diff = Math.abs(normalizeAngle(angle1) - normalizeAngle(angle2));
    return diff < epsilon || diff > (2 * Math.PI - epsilon);
}
```

---

## Performance Optimizations

### Spatial Hash Grid

**Purpose:** Accelerate point-in-polygon tests by partitioning space.

**Complete Implementation:**

```javascript
/**
 * Spatial hash grid for fast point-in-polygon tests.
 */
class SpatialHashGrid {
    constructor(polygon, cellSize = 10) {
        this.cellSize = cellSize;
        this.grid = new Map();
        this.polygon = polygon;
        this.bbox = getAxisAlignedBoundingBox(polygon);

        // Precompute polygon containment for grid cells
        this.buildGrid();
    }

    buildGrid() {
        for (let x = this.bbox.minX; x <= this.bbox.maxX; x += this.cellSize) {
            for (let y = this.bbox.minY; y <= this.bbox.maxY; y += this.cellSize) {
                const cellCenter = {
                    x: x + this.cellSize / 2,
                    y: y + this.cellSize / 2
                };

                const key = this.getCellKey(cellCenter);
                const inside = isPointInPolygon(cellCenter, this.polygon);

                this.grid.set(key, inside);
            }
        }
    }

    getCellKey(point) {
        const cellX = Math.floor(point.x / this.cellSize);
        const cellY = Math.floor(point.y / this.cellSize);
        return `${cellX},${cellY}`;
    }

    isPointInside(point) {
        const key = this.getCellKey(point);

        // Fast lookup
        if (this.grid.has(key)) {
            return this.grid.get(key);
        }

        // Fallback to exact test
        return isPointInPolygon(point, this.polygon);
    }
}

// Usage
const grid = new SpatialHashGrid(polygon, 10);

for (const testPoint of manyPoints) {
    if (grid.isPointInside(testPoint)) {
        // Process point...
    }
}
```

**Performance:** O(1) lookup instead of O(n) for each point.

### Early Termination

**Purpose:** Stop computation as soon as result is known.

```javascript
/**
 * Rectangle inscription with early termination.
 */
function findBestRectangleWithEarlyTermination(polygon, targetArea, threshold = 0.99) {
    let bestRect = null;
    let bestArea = 0;

    // Generate candidate angles and centroids
    const angles = generateAngles(polygon);
    const centroids = getMultipleCentroids(polygon);

    for (const angle of angles) {
        for (const centroid of centroids) {
            const rect = findMaxRectangleAt(centroid, angle, polygon);

            if (rect.area > bestArea) {
                bestArea = rect.area;
                bestRect = rect;

                // Early termination: Good enough?
                if (bestArea >= targetArea * threshold) {
                    console.log(`[early-stop] Reached ${threshold * 100}% of target area`);
                    return bestRect;
                }
            }
        }
    }

    return bestRect;
}
```

### Caching Strategies

```javascript
/**
 * Cache expensive polygon operations.
 */
class PolygonCache {
    constructor(polygon) {
        this.polygon = polygon;
        this.cache = {
            area: null,
            bbox: null,
            convexHull: null,
            centroid: null
        };
    }

    getArea() {
        if (this.cache.area === null) {
            this.cache.area = calculatePolygonAreaAbs(this.polygon);
        }
        return this.cache.area;
    }

    getBoundingBox() {
        if (this.cache.bbox === null) {
            this.cache.bbox = getAxisAlignedBoundingBox(this.polygon);
        }
        return this.cache.bbox;
    }

    getConvexHull() {
        if (this.cache.convexHull === null) {
            this.cache.convexHull = convexHull(this.polygon);
        }
        return this.cache.convexHull;
    }

    getCentroid() {
        if (this.cache.centroid === null) {
            this.cache.centroid = {
                x: this.polygon.reduce((sum, p) => sum + p.x, 0) / this.polygon.length,
                y: this.polygon.reduce((sum, p) => sum + p.y, 0) / this.polygon.length
            };
        }
        return this.cache.centroid;
    }
}
```

---

## References

### Academic Papers

1. **Winding Number Algorithm**
   - Hormann & Agathos (2001): "The point in polygon problem for arbitrary polygons"
   - URL: http://geomalgorithms.com/a03-_inclusion.html

2. **Rotating Calipers**
   - Shamos (1978): "Computational Geometry"
   - Toussaint (1983): "Solving geometric problems with the rotating calipers"

3. **Convex Hull**
   - Graham (1972): "An efficient algorithm for determining the convex hull of a finite planar set"
   - Andrew (1979): "Another efficient algorithm for convex hulls in two dimensions"

4. **Polylabel (Pole of Inaccessibility)**
   - Mapbox: https://github.com/mapbox/polylabel
   - Garcia-Castellanos & Lombardo (2007): "Poles of inaccessibility"

### Online Resources

1. **Geometric Algorithms**
   - https://geomalgorithms.com/
   - Computational Geometry in C (O'Rourke)

2. **SVG Path Specifications**
   - https://www.w3.org/TR/SVG/paths.html
   - MDN SVG Path Reference

3. **Numerical Precision**
   - Goldberg (1991): "What every computer scientist should know about floating-point arithmetic"
   - https://floating-point-gui.de/

### Implementation References

For actual implementations in this project, see:
- `SvgViewerAlgorithms.js` - Winding number, convex hull, utilities
- `SvgViewerBoundaryBased.js` - Boundary-based rectangle inscription
- `SvgViewerOptimized.js` - Optimized rectangle inscription with spatial grids
- `SvgViewerBoardroom.js` - Boardroom layout with fixed-width constraints

---

**End of Algorithm Implementation Reference**

For high-level architecture and system usage, see [InscribedRectangle-Guide.md](./InscribedRectangle-Guide.md).
