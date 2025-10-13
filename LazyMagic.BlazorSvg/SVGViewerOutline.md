# SVG Viewer Outline Algorithm

## Overview

The SVG Viewer outline algorithm implements a 4-step winding algorithm to generate outer edge paths around multiple overlapping SVG shapes. This algorithm replaces convex hull approaches to better handle concave shapes and complex configurations like stacked rectangles.

## Core Approach

The algorithm creates a unified outer boundary by converting SVG paths to line segments, merging coincident points between different paths, building a connected network, and traversing the outer edge using a clockwise winding strategy.

## Algorithm Steps

### Step 1: Convert Paths to Line Segments

**Purpose**: Simplify all SVG paths to pure line segments for consistent processing.

**Process**:
1. Parse SVG path data using regex pattern: `/[MmLlHhVvCcSsQqTtAaZz][^MmLlHhVvCcSsQqTtAaZz]*/g`
2. Extract endpoint coordinates from all SVG commands:
   - `M/m` (Move): Start point or implicit line commands
   - `L/l` (Line): Direct line endpoints
   - `H/h` (Horizontal): Horizontal line endpoints
   - `V/v` (Vertical): Vertical line endpoints
   - `C/c` (Cubic Bezier): Extract final endpoint only (params[4], params[5])
   - `Q/q` (Quadratic Bezier): Extract final endpoint only (params[2], params[3])
   - `Z/z` (Close): Ignored to avoid duplicate points
3. Create line segments between consecutive boundary points
4. Add closing segment to complete polygon

**Key Implementation**:
```javascript
// Create line segments between consecutive boundary points
for (let i = 1; i < boundaryPoints.length; i++) {
    segments.push({
        start: { x: boundaryPoints[i-1].x, y: boundaryPoints[i-1].y },
        end: { x: boundaryPoints[i].x, y: boundaryPoints[i].y },
        pathIdx: pathIdx,
        segmentIdx: segments.length
    });
}
```

### Step 2: Join Coincident Points

**Purpose**: Merge points that are close together but only between different paths to create connection opportunities.

**Rules**:
- **Tolerance**: 20.0 pixels for point adjacency detection
- **Cross-Path Only**: Only merge points from different `pathIdx` values
- **Distance Calculation**: Euclidean distance: `Math.sqrt((dx * dx) + (dy * dy))`
- **Averaging**: Merged points use average coordinates of all coincident points

**Process**:
1. Build point map with tolerance-based grouping
2. For each segment endpoint, find closest points from other paths within tolerance
3. Replace coincident points with averaged coordinates
4. Preserve original path indices for segment tracking

### Step 3: Join Paths into Network

**Purpose**: Create a connected graph structure with adjacency maps for traversal.

**Data Structures**:
- **Segments Array**: All line segments with unique IDs
- **Point-to-Segments Map**: Maps coordinate keys to connected segments
- **Coordinate Keys**: Format `"x.xx_y.yy"` using 2 decimal precision

**Key Implementation**:
```javascript
// Build point-to-segments adjacency map
const pointToSegments = new Map();
for (const segment of segments) {
    const startKey = `${segment.start.x.toFixed(2)}_${segment.start.y.toFixed(2)}`;
    const endKey = `${segment.end.x.toFixed(2)}_${segment.end.y.toFixed(2)}`;

    if (!pointToSegments.has(startKey)) pointToSegments.set(startKey, []);
    if (!pointToSegments.has(endKey)) pointToSegments.set(endKey, []);

    pointToSegments.get(startKey).push({ segment, isStart: true });
    pointToSegments.get(endKey).push({ segment, isStart: false });
}
```

### Step 4: Traverse Outer Edge Using Winding Algorithm

**Purpose**: Navigate the network to find the outermost boundary using clockwise traversal preferences.

#### Starting Point Selection
- **Rule**: Choose leftmost point (minimum X, then minimum Y)
- **Guarantee**: Leftmost point is always on the outer edge

#### Traversal Strategy
The algorithm uses a priority-based selection system for choosing the next segment at each vertex:

#### Priority System (Lower values = Higher priority)

1. **Collinear Continuation** (Priority: -2)
   - **Detection**: Turn angle within 0.1 radians (~6 degrees) of 0°
   - **Rule**: Only true straight continuation (NOT 360° turns)
   - **Purpose**: Maintain straight line paths when possible

2. **Outer-Facing at High-Degree Vertices** (Priority: -3)
   - **Trigger**: Vertices with 4+ connections AND segment pointing away from centroid
   - **Detection**: Dot product of (point-to-centroid vector) and (segment direction) > 0
   - **Purpose**: Avoid internal segments at merge points

3. **Clockwise Turn Preference** (Priority: normalized turn angle)
   - **Calculation**: Smallest turn angle = most clockwise = most outward
   - **Normalization**: Angles > π are converted to 2π - angle
   - **Purpose**: Follow the right-hand rule for outer boundary traversal

#### Turn Angle Calculation
```javascript
// Calculate turn angle relative to incoming direction
let turnAngle = outgoingAngle - incomingAngle;
// Normalize to [0, 2π)
while (turnAngle < 0) turnAngle += 2 * Math.PI;
while (turnAngle >= 2 * Math.PI) turnAngle -= 2 * Math.PI;

// For priority, use smallest angle (most clockwise)
let priority = turnAngle > Math.PI ? 2 * Math.PI - turnAngle : turnAngle;
```

#### Outer-Facing Detection
```javascript
// Check if segment leads away from shape centroid
const fromCentroidX = currentPoint.x - centroid.x;
const fromCentroidY = currentPoint.y - centroid.y;
const toNextX = nextPoint.x - currentPoint.x;
const toNextY = nextPoint.y - currentPoint.y;

const dotProduct = fromCentroidX * toNextX + fromCentroidY * toNextY;
const isOuterFacing = dotProduct > 0; // Positive = pointing away from centroid
```

## Key Rules and Constraints

### Point Merging Rules
1. **Cross-Path Only**: Never merge points within the same path
2. **Tolerance-Based**: Use 20px tolerance for coincident point detection
3. **Coordinate Averaging**: Merged points use average coordinates
4. **Preserve Path Identity**: Maintain original `pathIdx` in segments

### Traversal Rules
1. **Leftmost Start**: Always begin from leftmost point
2. **Clockwise Preference**: Prefer smallest turn angles (most outward turns)
3. **Collinear Priority**: Straight continuation gets highest priority
4. **Outer-Facing Priority**: At high-degree vertices, prefer segments pointing away from centroid
5. **No Backtracking**: Never revisit used segments
6. **Loop Detection**: Stop when returning to start point

### Network Construction Rules
1. **Unique Segment IDs**: Each segment gets format `"path{pathIdx}_seg{segmentIdx}"`
2. **Bidirectional Adjacency**: Each segment endpoint maps to connected segments
3. **Precision Control**: Use 2 decimal places for coordinate keys
4. **Connection Tracking**: Track both segment reference and endpoint role (start/end)

### Termination Conditions
1. **Return to Start**: Primary success condition - closed loop found
2. **No Available Segments**: Backup condition when no unvisited segments remain
3. **Max Iterations**: Safety limit at `segments.length * 2` to prevent infinite loops

## Debugging Features

### Console Logging
- **Step-by-step traversal**: Point positions, available segments, turn angles
- **Priority calculations**: Why each segment received its priority
- **Decision explanations**: Which segment was chosen and why
- **Network statistics**: Segment counts, connection degrees, traversal progress

### Failure Detection
- **Forced Choices**: Warns when only one segment available (potential internal path)
- **High-Degree Vertices**: Flags vertices with 3+ connections as potential merge points
- **Internal Segment Warnings**: Identifies when forced to traverse to high-degree vertices

### Performance Monitoring
- **Iteration Counting**: Tracks traversal steps vs. total segments
- **Coverage Analysis**: Reports visited vs. total segments
- **Timing**: Logs completion time and efficiency

## Common Issues and Solutions

### Issue: Internal Segments in Output
**Cause**: Point merging loses distinction between outer boundary and internal connections
**Detection**: Algorithm forced to high-degree vertices (3+ connections)
**Current Status**: Known limitation - topological constraint of merged network

### Issue: Incomplete Shape Coverage
**Cause**: Overly aggressive prioritization making all segments equal priority
**Solution**: Balanced priority system with distinct precedence levels

### Issue: Wrong Turn Detection
**Cause**: Near-360° turns incorrectly classified as straight continuation
**Solution**: Only consider angles near 0° as collinear, not 360°

## Algorithm Complexity

- **Time Complexity**: O(n²) for point merging + O(n) for traversal = O(n²)
- **Space Complexity**: O(n) for segments and adjacency maps
- **Typical Performance**: Handles 3-4 overlapping shapes efficiently

## Future Improvements

1. **Boundary Preservation**: Maintain distinction between original boundary segments and internal connections after point merging
2. **Topological Analysis**: Use graph theory to identify true outer boundary vs. internal connections
3. **Multi-Component Handling**: Support for disconnected shape groups
4. **Curve Approximation**: Better line segment approximation for curved paths