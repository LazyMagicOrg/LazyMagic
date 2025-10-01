# SvgViewer.js Remaining Functions Analysis

This document analyzes all remaining functions in SvgViewer.js after the geometric algorithm extraction to determine which functions can be made pure and moved to SvgViewerAlgorithms.js.

## Legend
- ✅ **ALREADY PURE WRAPPER** - Delegates to SvgViewerAlgorithms.js
- 🟢 **CAN BE PURE** - No DOM/Snap dependencies, can be extracted
- 🟡 **CAN BE REFACTORED** - Has instance dependencies that can be parameterized
- 🔴 **MUST STAY** - Requires DOM/Snap/instance state

---

## Core Infrastructure (MUST STAY)
| Function | Line | Type | Reason |
|----------|------|------|--------|
| `constructor()` | 118 | 🔴 MUST STAY | Class constructor, initializes Snap.svg |
| `rootSvg()` | 147 | 🔴 MUST STAY | Returns Snap.svg root element |
| `scope()` | 172 | 🔴 MUST STAY | Returns Snap.svg layer scope |
| `findLayerKeyFromNode()` | 180 | 🔴 MUST STAY | DOM traversal |
| `isInActiveLayer()` | 196 | 🔴 MUST STAY | Instance state check |
| `bootstrapLayers()` | 202 | 🔴 MUST STAY | Snap.svg layer initialization |
| `activateLayer()` | 214 | 🔴 MUST STAY | Instance state management |

---

## Already Extracted (PURE WRAPPERS)
| Function | Line | Status | Notes |
|----------|------|--------|-------|
| `doLinesIntersect()` | 425 | ✅ WRAPPER | → SvgViewerAlgorithms.doLinesIntersect |
| `isPointInPolygon()` | 430 | ✅ WRAPPER | → SvgViewerAlgorithms.isPointInPolygon (with spatial grid optimization) |
| `hasSelfintersection()` | 441 | ✅ WRAPPER | → SvgViewerAlgorithms.hasSelfintersection |
| `removeDuplicates()` | 480 | ✅ WRAPPER | → SvgViewerAlgorithms.removeDuplicates |
| `simpleConvexHull()` | 485 | ✅ WRAPPER | → SvgViewerAlgorithms.simpleConvexHull |
| `orientation()` | 490 | ✅ WRAPPER | → SvgViewerAlgorithms.orientation |
| `calculateDistance()` | 495 | ✅ WRAPPER | → SvgViewerAlgorithms.calculateDistance |
| `_distanceToLineSegment()` | 594 | ✅ WRAPPER | → SvgViewerAlgorithms.distanceToLineSegment |
| `_minDistToSet()` | 599 | ✅ WRAPPER | → SvgViewerAlgorithms.minDistToSet |
| `_downsamplePoints()` | 637 | ✅ WRAPPER | → SvgViewerAlgorithms.downsamplePoints |
| `_pointsMatch()` | 1761 | ✅ WRAPPER | → SvgViewerAlgorithms.pointsMatch |
| `_parsePathToLineSegments()` | 1267 | ✅ WRAPPER | → SvgViewerAlgorithms.parsePathToLineSegments |
| `_extractPathPoints()` | 1272 | ✅ WRAPPER | → SvgViewerAlgorithms.extractPathPoints |
| `_basicPolygonCleanup()` | 2007 | ✅ WRAPPER | → SvgViewerAlgorithms.basicPolygonCleanup |
| `_calculatePolygonArea()` | 2012 | ✅ WRAPPER | → SvgViewerAlgorithms.calculatePolygonArea |
| `_getPolygonBounds()` | 2017 | ✅ WRAPPER | → SvgViewerAlgorithms.getPolygonBounds |
| `_calculateParallelogramRectangle()` | 2022 | ✅ WRAPPER | → SvgViewerAlgorithms.calculateParallelogramRectangle |
| `_calculateTrapezoidRectangle()` | 2027 | ✅ WRAPPER | → SvgViewerAlgorithms.calculateTrapezoidRectangle |
| `_detectPolygonOrientation()` | 2032 | ✅ WRAPPER | → SvgViewerAlgorithms.detectPolygonOrientation |
| `_isAxisAligned()` | 2304 | ✅ WRAPPER | → SvgViewerAlgorithms.isAxisAligned |
| `_getRectangleRotation()` | 2309 | ✅ WRAPPER | → SvgViewerAlgorithms.getRectangleRotation |
| `_getBounds()` | 2318 | ✅ WRAPPER | → SvgViewerAlgorithms.getBounds |
| `_areRectanglesAdjacent()` | 2314 | ✅ WRAPPER | → SvgViewerAlgorithms.areRectanglesAdjacent |
| `_rectanglesFormSimpleUnion()` | 2343 | ✅ WRAPPER | → SvgViewerAlgorithms.rectanglesFormSimpleUnion |
| `_findSharedVertices()` | 2140 | ✅ WRAPPER | → SvgViewerAlgorithms.findSharedVertices |

---

## Functions Using Snap.svg/DOM (MUST STAY)
| Function | Line | Type | Dependencies | Notes |
|----------|------|------|--------------|-------|
| `calculatePathDistance()` | 229 | 🔴 MUST STAY | `path.getBBox()` | Snap.svg bounding box |
| `extractPathBoundaryPoints()` | 346 | 🔴 MUST STAY | `path.node.getTotalLength()`, `getPointAtLength()` | DOM SVGPathElement methods |
| `_convertPathsToLineSegments()` | 1238 | 🔴 MUST STAY | `path.attr('d')` | Snap.svg path data access |
| `_extractPathCorners()` | 2212 | 🔴 MUST STAY | `path.attr('d')` | Snap.svg path data access |
| `_combineOriginalPathData()` | 2365 | 🔴 MUST STAY | `path.attr()`, `snap.path()` | Creates Snap paths |
| `_cleanupDebugPaths()` | 2406 | 🔴 MUST STAY | `snap.selectAll()`, `element.remove()` | DOM manipulation |

---

## High-Level Orchestrators (MUST STAY)
These functions coordinate multiple operations, use instance state, or delegate to Snap/DOM functions:

| Function | Line | Type | Reason |
|----------|------|------|--------|
| `generateGroupOutline()` | 244 | 🔴 ORCHESTRATOR | Uses `this.s`, calls Snap methods, coordinates outline generation |
| `findLargestInscribedRectangle()` | 536 | 🔴 ORCHESTRATOR | Uses `window.fastInscribedRectangle` (external lib), performance timing |
| `_edgeHugsBoundary()` | 605 | 🟡 REFACTORABLE | Uses `this.calculateDistance()` and `this._minDistToSet()` (wrappers) |
| `_seedBridgePoints()` | 642 | 🟡 REFACTORABLE | Uses `this.calculateDistance()` (wrapper) |
| `_concaveHull()` | 673 | 🔴 ORCHESTRATOR | Complex algorithm using instance methods |
| `_generateOptimizedMultiPathOutline()` | 862 | 🔴 ORCHESTRATOR | Snap/DOM operations |
| `_mergePathBoundaries()` | 905 | 🔴 ORCHESTRATOR | Snap operations |
| `_createOverlappingPathMerge()` | 1179 | 🔴 MUST STAY | Uses `path.node.getPointAtLength()` |
| `_detectAndCreateRectangularBoundaryFromPoints()` | 2037 | 🟡 REFACTORABLE | Uses `this.simpleConvexHull()` (wrapper) |
| `_detectAndCreateRectangularBoundary()` | 2145 | 🔴 MUST STAY | Uses `path.getBBox()` |
| `_createCombinedRectangularBoundary()` | 2323 | 🟡 REFACTORABLE | Uses `this.simpleConvexHull()` (wrapper) |
| `_createComplexRectangularBoundary()` | 2348 | 🟢 PURE STUB | Not implemented, returns null |
| `_validateContainmentScore()` | 446 | 🟡 REFACTORABLE | Uses `pathInfo.bbox` from Snap, but algorithm is pure |

---

## Network/Graph Algorithms (CAN BE EXTRACTED)
These functions are algorithmically pure but use wrapper methods. Could be refactored to accept helper functions as parameters:

| Function | Line | Type | Current Dependencies | Can Extract? |
|----------|------|------|---------------------|--------------|
| `_joinCoincidentPoints()` | 1277 | 🟡 ORCHESTRATOR | Calls other merge functions, uses `window.SpatialHash` | Partially |
| `_mergeCoincidentPoints()` | 1309 | 🟢 CAN EXTRACT | Uses `this.calculateDistance()` (wrapper) | **YES** |
| `_mergeCoincidentPointsOptimized()` | 1514 | 🟡 MIXED | Uses `window.SpatialHash` (external lib) | Only if we include lib |
| `_clusterAndMergePoints()` | 1597 | 🟢 CAN EXTRACT | Uses `this.calculateDistance()` (wrapper) | **YES** |
| `_markSharedSegments()` | 1710 | 🟢 CAN EXTRACT | Uses `this._pointsMatch()` (wrapper) | **YES** |
| `_joinPathsIntoNetwork()` | 1766 | 🟢 **PURE** | No dependencies! | **YES - HIGH PRIORITY** |
| `_traverseOuterEdge()` | 1802 | 🟢 **PURE** | No dependencies! | **YES - HIGH PRIORITY** |

---

## Selection & UI Functions (MUST STAY)
| Function | Line | Type | Reason |
|----------|------|------|--------|
| `visualizeGroups()` | 2722 | 🔴 MUST STAY | DOM/Snap manipulation |
| `handleSelection()` | 2843 | 🔴 MUST STAY | Instance state, Snap operations |
| `computeIdsInsideBoundingBox()` | 2889 | 🔴 MUST STAY | Uses Snap.svg paths |
| `autoSelectInBoundingBox()` | 2919 | 🔴 MUST STAY | Instance state manipulation |
| `updateGlobalBoundingBox()` | 2936 | 🔴 MUST STAY | Snap.svg bounding boxes |
| `highlight()` | 2981 | 🔴 MUST STAY | Snap.svg attributes |
| `getPaths()` | 2998 | 🔴 MUST STAY | Snap.svg selection |
| `selectPath()` | 3005 | 🔴 MUST STAY | DOM classes |
| `selectPaths()` | 3025 | 🔴 MUST STAY | DOM classes |
| `unselectPath()` | 3054 | 🔴 MUST STAY | DOM classes |
| `unselectAllPaths()` | 3077 | 🔴 MUST STAY | DOM classes |
| `initializeSpatialStructures()` | 500 | 🔴 MUST STAY | Instance state initialization |

---

## Recommendations

### High Priority - Can Extract Immediately (Pure Functions)
1. **`_joinPathsIntoNetwork()`** (1766) - No dependencies
2. **`_traverseOuterEdge()`** (1802) - No dependencies

### Medium Priority - Need Minimal Refactoring
3. **`_markSharedSegments()`** (1710) - Replace `this._pointsMatch()` with direct call
4. **`_mergeCoincidentPoints()`** (1309) - Replace `this.calculateDistance()` with direct call
5. **`_clusterAndMergePoints()`** (1597) - Replace `this.calculateDistance()` with direct call

### Low Priority - Refactor for Testing
6. **`_edgeHugsBoundary()`** (605) - Could accept distance functions as parameters
7. **`_seedBridgePoints()`** (642) - Could accept distance function as parameter
8. **`_validateContainmentScore()`** (446) - Could accept bboxes as plain objects

### Keep as Orchestrators
- `generateGroupOutline()`, `_concaveHull()`, `findLargestInscribedRectangle()` - High-level coordination functions

---

## Summary

| Category | Count |
|----------|-------|
| Already Extracted (Wrappers) | 25 |
| Must Stay (Snap/DOM/State) | 28 |
| Can Extract (High Priority) | 2 |
| Can Extract (Medium Priority) | 3 |
| Can Refactor (Low Priority) | 3 |

**Total Remaining Functions**: 61
**Potential for Extraction**: 5-8 functions

The remaining extractable functions are primarily network/graph algorithms that would benefit the most from being in a testable library.
