// Extracted pure algorithm functions from SvgViewer.js
// These are standalone, testable versions without Blazor/DOM dependencies

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
