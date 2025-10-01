# SvgViewer.js Function Dependency Analysis

This document analyzes each private function in SvgViewer.js and categorizes its dependencies.

**Dependency Categories:**
- **PURE**: No external dependencies, pure algorithm (can be moved to SvgViewerAlgorithms.js)
- **SNAP**: Uses Snap.svg library (path.attr(), path.getBBox(), etc.)
- **DOM**: Uses DOM APIs (document, SVGPathElement, etc.)
- **SNAP+DOM**: Uses both Snap and DOM
- **INSTANCE**: Uses instance properties (this.snap, this.selectedPaths, etc.)
- **MIXED**: Combination of dependencies that make extraction complex

---

## Function Analysis

### ✅ Already Moved to SvgViewerAlgorithms.js

| Function | Dependency | Status |
|----------|------------|--------|
| `detectPolygonOrientation(polygon)` | **PURE** | ✅ Moved |
| `calculateParallelogramRectangle(polygon, angle)` | **PURE** | ✅ Moved |
| `calculateTrapezoidRectangle(polygon, angle)` | **PURE** | ✅ Moved |
| `calculatePolygonArea(points)` | **PURE** | ✅ Moved |
| `getPolygonBounds(points)` | **PURE** | ✅ Moved |
| `basicPolygonCleanup(points)` | **PURE** | ✅ Moved |
| `isAxisAligned(corners)` | **PURE** | ✅ Moved |
| `getRectangleRotation(corners)` | **PURE** | ✅ Moved |
| `getBounds(corners)` | **PURE** | ✅ Moved |
| `areRectanglesAdjacent(pathCorners)` | **PURE** | ✅ Moved |
| `rectanglesFormSimpleUnion(rect1, rect2)` | **PURE** | ✅ Moved |
| `findSharedVertices(pathBoundaryPoints)` | **PURE** | ✅ Moved |
| `calculateDistance(p1, p2)` | **PURE** | ✅ Moved |
| `removeDuplicates(points, tolerance)` | **PURE** | ✅ Moved |
| `orientation(p, q, r)` | **PURE** | ✅ Moved |
| `doLinesIntersect(p1, p2, p3, p4)` | **PURE** | ✅ Moved |
| `hasSelfintersection(hull)` | **PURE** | ✅ Moved |
| `isPointInPolygon(point, polygon)` | **PURE** | ✅ Moved (refactored) |
| `simpleConvexHull(points)` | **PURE** | ✅ Moved |

---

### 📐 Pure Geometry & Math Functions (All Moved)

| Function | Dependency | Status |
|----------|------------|--------|
| `distanceToLineSegment(point, segStart, segEnd)` | **PURE** | ✅ Moved |
| `minDistToSet(pt, set)` | **PURE** | ✅ Moved |
| `downsamplePoints(points, everyN)` | **PURE** | ✅ Moved |
| `pointsMatch(p1, p2, tolerance)` | **PURE** | ✅ Moved |
| `calculateDistance(p1, p2)` | **PURE** | ✅ Moved |
| `removeDuplicates(points, tolerance)` | **PURE** | ✅ Moved |
| `orientation(p, q, r)` | **PURE** | ✅ Moved |
| `doLinesIntersect(p1, p2, p3, p4)` | **PURE** | ✅ Moved |
| `hasSelfintersection(hull)` | **PURE** | ✅ Moved |
| `isPointInPolygon(point, polygon)` | **PURE** | ✅ Moved (refactored to remove grid) |
| `simpleConvexHull(points)` | **PURE** | ✅ Moved |

---

### 🔧 Functions Using Instance Methods (Needs refactoring)

| Function | Dependency | DOM/Snap Usage | Notes |
|----------|------------|----------------|-------|
| `_edgeHugsBoundary(a, b, boundaryCloud, ...)` | **INSTANCE** | Uses `this.calculateDistance()` | Calls instance method, otherwise pure |
| `_validateContainmentScore(hull, pathInfos)` | **INSTANCE** | Uses `this.isPointInPolygon()` and `path.getBBox()` | Validation logic using Snap getBBox |

---

### 📊 Path Parsing & SVG Data Extraction

| Function | Dependency | DOM/Snap Usage | Status |
|----------|------------|----------------|--------|
| `_convertPathsToLineSegments(groupPaths)` | **SNAP** | `path.attr('d')` | Stays (needs Snap) |
| `parsePathToLineSegments(pathData, pathIdx)` | **PURE** | None (takes string input) | ✅ Moved |
| `extractPathPoints(pathData)` | **PURE** | None (parses string) | ✅ Moved |
| `_extractPathCorners(path)` | **SNAP** | `path.attr('d')` | Stays (needs Snap) |

---

### 🔗 Point Merging & Network Algorithms (Mostly pure)

| Function | Dependency | DOM/Snap Usage | Notes |
|----------|------------|----------------|-------|
| `_joinCoincidentPoints(lineSegmentPaths, tolerance)` | **PURE** | None | **Can be moved** - geometric algorithm |
| `_mergeCoincidentPoints(allSegments, tolerance)` | **PURE** | None | **Can be moved** - point clustering |
| `_mergeCoincidentPointsOptimized(allSegments, tolerance)` | **PURE** | None | **Can be moved** - spatial grid optimization |
| `_clusterAndMergePoints(allSegments, potentialConnections)` | **PURE** | None | **Can be moved** - graph clustering |
| `_markSharedSegments(allSegments, tolerance)` | **PURE** | None | **Can be moved** - segment marking |
| `_joinPathsIntoNetwork(segments)` | **PURE** | None | **Can be moved** - network construction |
| `_traverseOuterEdge(pathNetwork)` | **PURE** | None | **Can be moved** - graph traversal |

---

### 🎨 Concave Hull & Boundary Generation (Mostly pure)

| Function | Dependency | DOM/Snap Usage | Notes |
|----------|------------|----------------|-------|
| `_seedBridgePoints(pathInfos, maxGap, sampleStride)` | **SNAP** | Uses `path.node.getPointAtLength()` | DOM SVGPathElement method |
| `_concaveHull(points, kStart, kMax, maxEdge, edgeHugPx)` | **INSTANCE** | Calls `this._edgeHugsBoundary()` | Complex algorithm with instance calls |

---

### 🔄 Path Merging & Outline Generation (SNAP+DOM)

| Function | Dependency | DOM/Snap Usage | Notes |
|----------|------------|----------------|-------|
| `_generateOptimizedMultiPathOutline(groupPaths, options)` | **SNAP+INSTANCE** | Orchestrates multiple path operations | High-level coordinator |
| `_mergePathBoundaries(groupPaths, allBoundaryPoints)` | **SNAP+INSTANCE** | Uses Snap, calls instance methods | Complex merging logic |
| `_createOverlappingPathMerge(groupPaths, allBoundaryPoints)` | **SNAP** | Uses `path.node.getPointAtLength()` | Snap path operations |

---

### 📦 Rectangular Boundary Detection (SNAP dependent)

| Function | Dependency | DOM/Snap Usage | Notes |
|----------|------------|----------------|-------|
| `_detectAndCreateRectangularBoundaryFromPoints(pathBoundaryPoints)` | **INSTANCE** | Calls `this.simpleConvexHull()` | Uses instance convex hull |
| `_detectAndCreateRectangularBoundary(groupPaths)` | **SNAP+INSTANCE** | `path.getBBox()`, calls helper methods | Complex detection logic |
| `_createCombinedRectangularBoundary(pathCorners)` | **INSTANCE** | Calls `this.simpleConvexHull()` | Convex hull wrapper |
| `_createComplexRectangularBoundary(rect1, rect2)` | **PURE** | None (stub) | Not implemented |

---

### 🛠️ Path Creation & DOM Manipulation (SNAP+DOM)

| Function | Dependency | DOM/Snap Usage | Notes |
|----------|------------|----------------|-------|
| `_combineOriginalPathData(groupPaths)` | **SNAP** | `path.attr('d')`, `snap.path()` | Creates new Snap paths |
| `_cleanupDebugPaths(scope)` | **SNAP** | `snap.selectAll()`, `element.remove()` | DOM manipulation |
| `_createUnifiedPath(groupPaths, scope, debugVisible)` | **SNAP+DOM+INSTANCE** | Full Snap/DOM usage | Main path creation logic |
| `_createFallbackUnifiedPath(groupPaths, scope, debugVisible)` | **SNAP+DOM+INSTANCE** | Full Snap/DOM usage | Fallback creation logic |

---

## Summary Statistics

| Category | Count | Status |
|----------|-------|--------|
| Moved to Library | 25 | ✅ Complete |
| Can Still Move (Pure) | 2 | ⚠️ Future work |
| Needs Refactoring | 2 | ⚠️ Future work |
| SNAP Dependent | 5 | ❌ Keep in SvgViewer.js |
| Complex/Mixed | 12 | ❌ Keep in SvgViewer.js |

**Total Functions Analyzed**: 46
**Moved in this session**: 7 new geometric functions (+ 18 from previous = 25 total)

---

## Recommended Next Steps

### High Priority - Easy Wins (Pure functions)
These functions can still be moved to `SvgViewerAlgorithms.js`:

1. **Network/Graph Algorithms** (9 functions remaining)
   - `_joinCoincidentPoints()`
   - `_mergeCoincidentPoints()`
   - `_mergeCoincidentPointsOptimized()`
   - `_clusterAndMergePoints()`
   - `_markSharedSegments()`
   - `_joinPathsIntoNetwork()`
   - `_traverseOuterEdge()`

### Medium Priority - Needs Refactoring
These need instance methods extracted (2 functions):

- `_edgeHugsBoundary()` - Change `this.calculateDistance()` to direct call (now that calculateDistance is in library)
- `_validateContainmentScore()` - Uses `path.getBBox()` and `this.isPointInPolygon()` - partially refactorable

### Low Priority - Keep in SvgViewer.js
These require Snap.svg or DOM and should stay:

- All path creation functions
- DOM manipulation functions
- Functions using `path.attr()`, `path.node`, `getBBox()`, etc.

---

## Testing Strategy

For each function moved to the library:
1. Add exports to `svgviewer-algorithms-extracted.js`
2. Update wrapper in `SvgViewer.js` to call `window.SvgViewerAlgorithms.functionName()`
3. Add test cases to test harness
4. Generate SVG visualizations to verify correctness
