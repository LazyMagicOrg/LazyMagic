# Inscribed Rectangle System - Complete Guide

This comprehensive guide covers the architecture, implementation, deployment, and extension of the inscribed rectangle system for SVG floor plan visualization in LazyMagic.BlazorSvg.

**Related Documentation:**
- [Algorithms-Implementation.md](./Algorithms-Implementation.md) - Low-level algorithm implementations and mathematical foundations
- [MaxInscribedLayoutPipeline.md](./MaxInscribedLayoutPipeline.md) - Max-inscribed rectangle system
- [BoardroomLayoutPipeline.md](./BoardroomLayoutPipeline.md) - Boardroom-specific layout system
- [HollowSquareLayoutPipeline.md](./HollowSquareLayoutPipeline.md) - Hollow square layout system
- [EmbedData-QuickRef.md](./EmbedData-QuickRef.md) - Quick command reference for data regeneration

**Last Updated:** 2025-10-25
**Version:** LazyMagic.BlazorSvg 3.0.1

---

## Table of Contents

1. [Overview](#overview)
2. [Core Concepts](#core-concepts)
3. [Architecture](#architecture)
4. [Coordinate System and Units](#coordinate-system-and-units)
5. [Valid Combination Rules](#valid-combination-rules)
6. [Algorithm Implementations](#algorithm-implementations)
7. [SvgViewer Component](#svgviewer-component)
8. [Precomputation Pipeline](#precomputation-pipeline)
9. [Data Storage and Embedding](#data-storage-and-embedding)
10. [Runtime Architecture](#runtime-architecture)
11. [Deployment Pipeline](#deployment-pipeline)
12. [Troubleshooting](#troubleshooting)
13. [Performance Benchmarks](#performance-benchmarks)
14. [Extending with Size Constraints](#extending-with-size-constraints)
15. [Best Practices](#best-practices)
16. [References](#references)

---

## Overview

This system provides **interactive SVG path selection** with **automatic inscribed rectangle calculation** for ballroom floor plan visualization. The primary use case is selecting multiple ballroom sections (rooms, aisles, crossings) and displaying the largest rectangle that fits inside the combined selection.

### Key Features

- **Real-time path selection** with visual feedback (orange outlines)
- **Automatic inscribed rectangle calculation** using hybrid algorithms
- **Precomputed rectangles** for 250+ common combinations (instant < 1ms lookup)
- **Smart fallback** to computation when precomputed data unavailable
- **Multi-tenant S3 deployment** with asset versioning

### Performance Goals

- **Precomputed**: < 1ms (instant lookup)
- **Boundary-based**: 100-200ms (simple rectangular shapes)
- **Optimized**: 1-3 seconds (complex concave shapes)

---

## Core Concepts

### Architecture Philosophy

**Key Principle:** Precompute everything possible at build time to achieve instant runtime performance.

- **Build Time:** Generate all valid combinations, compute inscribed rectangles, calculate areas
- **Runtime:** Simple lookup by sorted key, no heavy computation
- **Invalid Combinations:** No precomputed data = no visualization = clear user feedback

### Why Precomputation?

1. **Performance:** Rectangle inscription algorithms are computationally expensive (15-20 minutes for 251 combinations)
2. **Determinism:** Same input always produces same output - perfect for precomputation
3. **User Experience:** Instant results when selecting room combinations
4. **Validation:** Missing data implicitly validates that a combination is invalid

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Blazor Component Layer                    │
│  SvgViewer.razor → SvgViewerJS.cs → SvgViewer.js (JavaScript)   │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                      Algorithm Libraries                         │
│  • SvgViewerAlgorithms.js (winding, convex hull, utilities)    │
│  • SvgViewerBoundaryBased.js (fast boundary-based rectangles)   │
│  • SvgViewerOptimized.js (slow but accurate optimization)       │
│  • SvgViewerBoardroom.js (boardroom table layouts)              │
│  • SvgViewerHollowSquare.js (hollow square table layouts)       │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                          Data Sources                            │
│  • Level1.svg (SVG geometry from S3 or local)                   │
│  • precomputed-rectangles.json (max-inscribed rectangles)       │
│  • precomputed-boardroom.json (boardroom layouts)               │
│  • precomputed-hollowsquare.json (hollow square layouts)        │
│  • Rooms.json (graph connectivity for validation)               │
└─────────────────────────────────────────────────────────────────┘
```

---

## Coordinate System and Units

### SVG Coordinate System

The floor plan uses a **1/12 architectural scale** where dimensions are represented in a specific coordinate space:

```
1 SVG inch = 1 real-world foot
1 SVG user unit = 1/96 SVG inch
```

### Layer Transform

All SVG paths are transformed with a scale factor:

```
Scale Factor = 48.345845
```

This scale is applied to all coordinates in the `<g>` layer containing the floor plan paths.

### Area Calculation Formula

Areas must be converted to **square SVG inches** for display:

```javascript
// 1. Calculate area in pre-transform coordinates using shoelace formula
let areaPreTransform = 0;
for (let i = 0; i < polygon.length; i++) {
    const j = (i + 1) % polygon.length;
    areaPreTransform += polygon[i].x * polygon[j].y;
    areaPreTransform -= polygon[j].x * polygon[i].y;
}
areaPreTransform = Math.abs(areaPreTransform) / 2;

// 2. Apply scale factor to get area in square user units
const areaUserUnits = areaPreTransform * (SCALE_FACTOR * SCALE_FACTOR);

// 3. Convert to square SVG inches
const UNITS_PER_INCH = 96;
const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);
```

**Critical:** Areas are in **square SVG inches**, not real-world square feet or square user units.

### Shoelace Formula Implementation

```javascript
function calculatePolygonArea(points) {
    let area = 0;
    for (let i = 0; i < points.length; i++) {
        const current = points[i];
        const next = points[(i + 1) % points.length];
        area += current.x * next.y - next.x * current.y;
    }
    return Math.abs(area) / 2;
}
```

This gives the signed area using the cross product of consecutive edge vectors.

---

## Valid Combination Rules

### Rule 1: Adjacency Constraint

**If two or more rooms that share an aisle are selected, the connecting aisle MUST also be selected.**

**Rationale:** Prevents concave indents in the combined polygon, which would create invalid conference room layouts.

**Example:**
- ✅ Valid: `Room_1 + Room_2 + Aisle_12`
- ✅ Valid: `Room_1 + Room_3` (no shared aisle)
- ❌ Invalid: `Room_1 + Room_2` (missing `Aisle_12`)
- ❌ Invalid: `Room_3 + Room_4 + Aisle_12` (has `Aisle_12` but missing `Aisle_34`)

### Rule 2: Aisle Inclusion

**An aisle can only be included if BOTH rooms it connects are selected.**

**Rationale:** An aisle without its connected rooms doesn't make sense as a conference space.

**Example:**
- ✅ Valid: `Room_1 + Room_2 + Aisle_12`
- ❌ Invalid: `Room_1 + Aisle_12` (missing `Room_2`)
- ❌ Invalid: `Aisle_12` alone

### Additional Rules from Test Harness

- **Rule 0**: Single section must be Room (not Aisle/Crossing)
- **Rule 3**: Each crossing has ≥2 connected aisles present
- **Rule 4**: If 3+ aisles connect to crossing, crossing must be present
- **Rule 5**: Aisle can't be missing if 2+ connected rooms present (U-shape)
- **Rule 6**: Crossings can't be only bridge between components

### Combination Generation

The `valid-combinations.json` file contains all 251 valid combinations following these rules.

**Key Format:** Sections sorted alphabetically and joined with underscore: `Section1_Section2_Section3`

---

## Algorithm Implementations

This section provides high-level descriptions and usage patterns for each algorithm. For detailed implementations including mathematical foundations, edge case handling, and code examples, see [Algorithms-Implementation.md](./Algorithms-Implementation.md).

### 1. **Precomputed Lookup** (Fastest: < 1ms)

**Location:** `SvgViewer.js:loadPrecomputedRectangles()`

**How it works:**
- Loads `precomputed-rectangles.json` from same path as SVG
- URL derived automatically: `Level1.svg` → `precomputed-rectangles.json`
- Creates Map lookup by sorted path ID key
- Example key: `"Ballroom_Aisle_35_Ballroom_Room_3_Ballroom_Room_5"`

**Data structure:**
```json
{
  "generatedAt": "2025-10-08T09:21:05.123Z",
  "totalCombinations": 251,
  "successfulComputations": 250,
  "rectangles": [
    {
      "key": "Ballroom_Room_1_Ballroom_Room_3",
      "sections": ["Ballroom_Room_1", "Ballroom_Room_3"],
      "rectangle": {
        "corners": [
          {"x": 478.1, "y": 32.3},
          {"x": 680.6, "y": 32.3},
          {"x": 680.6, "y": 128.1},
          {"x": 478.1, "y": 128.1}
        ],
        "width": 202.4,
        "height": 95.7,
        "area": 19384.8,
        "angle": 0,
        "centroid": {"x": 579.3, "y": 80.2},
        "type": "boundary-based"
      },
      "polygonArea": 2822.8891,
      "rectangleArea": 2830.9513,
      "computationTimeMs": 125.3
    }
  ]
}
```

**When used:**
- First choice for all selections
- Falls back to computation if no match found
- Console log: `[precomputed] ✓ Found precomputed rectangle for 7 sections`

---

### 2. **Boundary-Based Algorithm** (Fast: 100-200ms)

**Location:** `SvgViewerBoundaryBased.js:boundaryBasedInscribedRectangle()`

**Related Documentation:** [MaxInscribedLayoutPipeline.md](./MaxInscribedLayoutPipeline.md)

**Best for:**
- Simple shapes (rectangles, L-shapes, T-shapes)
- Shapes with clear dominant edge directions
- 4-12 vertices in boundary polygon

**How it works:**
1. **Extract polygon boundary** from merged paths
2. **Analyze edges** to find dominant directions
3. **Group edges** by angle (tolerance: ±5°)
4. **Find perpendicular pairs** (0°/90°, 45°/135°, etc.)
5. **Test candidate angles** (10 total: dominant pairs + edge angles)
6. **For each angle:**
   - Rotate polygon to align with axes
   - Create rotated bounding box
   - Test grid of centroid positions (326 points)
   - Expand rectangle from each centroid until hitting boundary
   - Track largest valid rectangle
7. **Validate** all corners inside polygon
8. **Return best result** across all angles

**Strategy selection:**
```javascript
if (pathCount === 1) {
    strategy = "SINGLE_PATH";  // Very fast, minimal angles
} else if (vertices <= 6) {
    strategy = "SIMPLE";       // Fast, fewer angles
} else {
    strategy = "HYBRID";       // Full angle search
}
```

**Console output:**
```
[boundary-based] Strategy selection: HYBRID
  - Path count: 7
  - Vertices: 13
  - Bounding box area: 88530 sq px
[boundary-based] Found 5 angle groups:
  1. 0.0° (4 edges, total 436.9px)
  2. 90.0° (4 edges, total 322.7px)
  3. 106.5° (2 edges, total 167.8px)
[boundary-based] Testing 10 angles: 0.0, 0.0, 16.5, 16.8, 69.4, 90.0...
[boundary-based] Best result: 154.8 × 221.2 at 0.0° = 34248.0 sq px in 119.9ms
```

---

### 3. **Optimized Algorithm** (Slow: 1-3 seconds)

**Location:** `SvgViewerOptimized.js:optimizedInscribedRectangle()`

**Best for:**
- Complex concave shapes
- Shapes with many vertices (> 12)
- When boundary-based finds suboptimal results

**How it works:**
1. **Dense angle sampling** (36 angles: 0° to 175° in 5° steps)
2. **Adaptive centroid search** using spatial hash grid
3. **Multi-pass expansion:**
   - Initial expansion from centroid
   - Iterative growth in small steps
   - Edge-aware expansion (respects polygon boundary)
4. **Validation** using winding number algorithm
5. **Local refinement** around best candidates

**Performance optimizations:**
- **Spatial hash grid** for fast point-in-polygon tests
- **Early termination** if target area reached
- **Adaptive sampling** (denser near polygon boundary)
- **Parallel candidate testing** (when supported)

**Console output:**
```
[optimized] Testing 36 rotation angles
[optimized] Angle 96.0°: Testing 1250 centroid candidates
[optimized] Angle 96.0°: Best rectangle = 217.5 × 181.2 = 39413 sq px
[optimized] Found optimal rectangle: 217.5 × 181.2 at 96.0° = 39413 sq px in 2415ms
```

---

### 4. **Hybrid Algorithm** (Smart: Automatic Selection)

**Location:** `SvgViewerBoundaryBased.js:hybridInscribedRectangle()`

**Strategy:**
```javascript
1. Try boundary-based first (fast)
2. Check if result meets threshold (default: 95% of target area)
3. If threshold met: Return boundary result
4. If threshold not met: Try optimized algorithm
5. Compare both results, return better one
```

**Decision logic:**
```javascript
const improvementPercent = (optimizedArea - boundaryArea) / boundaryArea * 100;

if (improvementPercent >= 5%) {
    console.log(`[hybrid] Optimized is ${improvementPercent.toFixed(1)}% better, using optimized`);
    return optimizedResult;
} else {
    console.log(`[hybrid] Boundary-based is sufficient (< 5% improvement)`);
    return boundaryResult;
}
```

**Console output (boundary sufficient):**
```
[hybrid] Step 1: Trying boundary-based algorithm...
[hybrid] Boundary-based: 34248.0 sq px in 119.9ms
[hybrid] No target area provided and threshold > 0, using boundary-based result only
[hybrid] Selected: boundary-based with 34248.0 sq px
[hybrid] Total time: 120.4ms
```

**When optimized is used:**
```
[hybrid] Step 1: Trying boundary-based algorithm...
[hybrid] Boundary-based: 34248.0 sq px in 120ms
[hybrid] Step 2: Trying optimized algorithm...
[hybrid] Optimized: 39413.0 sq px in 2415ms
[hybrid] Optimized is 15.1% better, using optimized
[hybrid] Total time: 2535ms
```

---

### 5. **Boardroom Layout Algorithm** (Constrained: 15-20 minutes build-time)

**Location:** `SvgViewerBoardroom.js:findBoardroomLayout()`

**Related Documentation:** [BoardroomLayoutPipeline.md](./BoardroomLayoutPipeline.md)

**Purpose:** Find the largest boardroom-style table arrangement (fixed width, variable length).

**Constraints:**
- **Fixed Width:** 13 ft (2.5 ft tables × 2 + 4 ft aisles × 2)
- **Variable Length:** 14 ft minimum, increases in 6 ft increments
- **Rotation:** 0-180° tested
- **Output:** Number of table sets and total tables

**Algorithm:**
- Grid search with angle testing (36 angles × 9 centroids)
- Incremental size expansion (add 6 ft per set)
- Test both orientations (13×L and L×13)
- Precomputed for all 251 combinations

**See:** BoardroomLayoutPipeline.md for complete documentation.

---

### 6. **Hollow Square Layout Algorithm** (Constrained: 15-20 minutes build-time)

**Location:** `SvgViewerHollowSquare.js:findHollowSquareLayout()`

**Related Documentation:** [HollowSquareLayoutPipeline.md](./HollowSquareLayoutPipeline.md)

**Purpose:** Find the largest hollow square table arrangement (perimeter tables with open center).

**Constraints:**
- **Minimum Outer:** 14 ft × 14 ft
- **Table Depth:** 2.5 ft (5 ft total on each side)
- **Minimum Inner:** 4 ft × 4 ft clearance
- **Variable Dimensions:** Both width and height expand independently
- **Rotation:** 0-180° tested

**Algorithm Strategy:**

1. **Angle & Centroid Sampling:** Test 36 angles × 9 centroids
2. **Binary Search Expansion:**
   ```javascript
   // Expand width while maintaining height
   let low = 14, high = maxWidth;
   while (high - low > 0.1) {
       let mid = (low + high) / 2;
       if (fitsInPolygon(mid, height)) {
           width = mid; low = mid;
       } else {
           high = mid;
       }
   }
   // Then expand height similarly
   ```
3. **Validate Inner Clearance:**
   ```javascript
   innerWidth = outerWidth - (2 × 5 ft)
   innerHeight = outerHeight - (2 × 5 ft)

   if (innerWidth < 4 ft || innerHeight < 4 ft) {
       return null;  // Doesn't meet minimum
   }
   ```
4. **Track Best Result:** Keep layout with maximum outer area

**Output Data:**
```javascript
{
  corners: [...],           // Outer rectangle corners
  width: 26.0,             // Outer width (ft)
  height: 80.0,            // Outer height (ft)
  area: 2080.0,            // Outer area (pre-transform)
  innerWidth: 16.0,        // Inner clearance width
  innerHeight: 70.0,       // Inner clearance height
  innerArea: 1120.0,       // Usable center space
  angle: 0,
  centroid: {x, y},
  type: 'hollowsquare'
}
```

**Performance:**
- Build-time: 15-20 minutes for 251 combinations
- Runtime: < 1ms (precomputed lookup)
- Average computation: ~145 ms per combination

**Console Output:**
```
[hollowsquare] Testing 36 angles...
[hollowsquare] Angle 0°: Expanding from 14×14...
[hollowsquare] Best: 26×80 ft (outer), 16×70 ft (inner) in 145ms
```

**See:** HollowSquareLayoutPipeline.md for complete documentation including:
- Detailed algorithm walkthrough
- Data extraction and embedding pipeline
- C# interop integration
- UI display examples

---

## SvgViewer Component

### Component Stack

**C# Layer (`SvgViewer.razor`)**
- Blazor component that wraps JavaScript interop
- Parameters:
  - `SvgUrl`: Path to SVG file (local or S3 URL)
  - `InitialPaths`: List of path IDs to select on load
  - `PathsChanged`: Callback when selection changes
  - `AllInsideSelected`: Binding for bounding box completeness detection
  - `DisableSelection`: Optional flag to disable interaction

**Interop Layer (`SvgViewerJS.cs`)**
- Manages JavaScript module lifecycle
- Provides strongly-typed C# methods:
  - `InitAsync(containerId, disableSelection)`: Initialize viewer
  - `LoadSvgAsync(svgUrl)`: Load SVG from URL
  - `SelectPath(pathId)`: Select single path
  - `SelectPaths(pathIds)`: Select multiple paths
  - `UnselectPath(pathId)`: Unselect path
  - `UnselectAllPaths()`: Clear selection
  - `GetAreaDataAsync()`: Retrieve area measurements
- Handles events via `[JSInvokable]` callbacks

**JavaScript Layer (`SvgViewer.js`)**
- Uses **Snap.svg** for SVG manipulation
- Instance-based design (multiple viewers on one page)
- Features:
  - Path selection/unselection with visual feedback
  - Bounding box calculation with overlap detection
  - Inscribed rectangle generation with multiple algorithms
  - Precomputed rectangle lookup with automatic URL derivation
  - Debug mode for visualization (magenta unified paths, rectangles)

### Key JavaScript Classes

**`SvgViewerInstance`**
- Main class managing one SVG viewer instance
- Properties:
  - `s`: Snap.svg instance
  - `selectedIds`: Set of currently selected path IDs
  - `boundingBoxRect`: Blue dashed bounding box (optional)
  - `precomputedRectangles`: Cached precomputed data
  - `svgUrl`: Resolved SVG URL (for deriving precomputed path)
  - `useFastMode`: Toggle for fast algorithms
  - `showOutlines`: Toggle for orange selection outlines
  - `showBoundingBox`: Toggle for blue bounding box

### Visual Feedback

**Selection States:**
- **Unselected**: Original color (white/gray)
- **Selected (complete)**: Green fill (all paths in bounding box selected)
- **Selected (incomplete)**: Red fill (some paths in bounding box not selected)
- **Orange outline**: Concave hull around selected paths (optional)
- **Blue dashed box**: Axis-aligned bounding box (optional)
- **Magenta path**: Debug visualization of unified merged path
- **Red rectangle**: Final inscribed rectangle (debug mode)

### Path Selection Flow

```
1. User clicks SVG path
   ↓
2. handleSelection(event) toggles selection
   ↓
3. selectPath(pathId) updates internal state
   ↓
4. updateGlobalBoundingBox() recalculates bounds
   ↓
5. highlight() updates visual state (green/red)
   ↓
6. visualizeGroups() generates outlines and rectangles
   ↓
7. generateGroupOutline(pathIds) creates concave hull
   ↓
8. lookupPrecomputedRectangle(pathIds) tries cache
   ↓
   If found: Use precomputed rectangle (< 1ms)
   If not found: Compute using hybrid algorithm (100ms - 3s)
   ↓
9. Render inscribed rectangle on SVG
```

---

## Precomputation Pipeline

### Overview

The test harness pre-computes inscribed rectangles for all valid ballroom path combinations to enable instant lookup in production.

**Location:** `C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness\`

### Step 1: Generate Valid Combinations

**Script:** `compute-all-combinations.js`

```bash
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness
node compute-all-combinations.js
```

**What it does:**
- Loads `Rooms.json` (graph connectivity: rooms, aisles, crossings)
- Generates all possible path combinations (2^15 = 32,768 possibilities)
- Validates each combination against 7 validation rules
- Outputs: `valid-combinations.json` (251 valid combinations)
- Generates: `test-config.js` with test cases

**Statistics:**
```
Processed 15 sections
Generated 32,768 total combinations
Found 251 valid combinations (0.8%)
Saved to valid-combinations.json
```

### Step 2: Compute Inscribed Rectangles

**Script:** `run-tests.ps1` or `test-runner.js`

```bash
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness
.\run-tests.ps1
# OR
node test-runner.js
```

**Duration:** 15-20 minutes

**What it does:**
- Starts local web server (if using run-tests.ps1): `http://localhost:3000`
- Serves: `test.html` (SvgViewer test page)
- Launches Playwright browser automation (if using run-tests.ps1)
- For each of 251 combinations:
  1. Load SVG with selected paths
  2. Trigger inscribed rectangle calculation
  3. Capture result (corners, width, height, area, angle, type, time)
  4. Calculate polygon area and rectangle area in square SVG inches
  5. Save visualization as SVG in `TestResults/MaxInscribedResults/Combo_XXXX.svg`
  6. Append result to `results.txt`
- Shuts down web server (if using run-tests.ps1)

**Console output:**
```
Running 251 tests using 1 worker

  ✓ [chromium] › inscribed-rectangle.spec.js:7:6 › Combo 0001 (Ballroom_Room_1)
  ✓ [chromium] › inscribed-rectangle.spec.js:7:6 › Combo 0002 (Ballroom_Room_2)
  ...
  ✓ [chromium] › inscribed-rectangle.spec.js:7:6 › Combo 0251 (All sections)

251 passed (15.2m)
```

**Test output:**
- `TestResults/MaxInscribedResults/Combo_XXXX.svg`: Visual representation with legend
- `TestResults/MaxInscribedResults/results.txt`: Line-delimited JSON results

**Example SVG output:**
```xml
<svg>
  <!-- Original paths (semi-transparent) -->
  <path id="Ballroom_Room_3" fill="rgba(200,200,200,0.3)" .../>

  <!-- Inscribed rectangle (red) -->
  <polygon points="478.1,32.3 680.6,32.3 680.6,128.1 478.1,128.1"
           fill="rgba(255,100,100,0.3)" stroke="#ff6666"/>

  <!-- Centroid marker -->
  <circle cx="579.3" cy="80.2" r="4" fill="#ff0000"/>

  <!-- Legend -->
  <text x="10" y="30">Type: boundary-based</text>
  <text x="10" y="50">Size: 202.4 × 95.7 px</text>
  <text x="10" y="70">Area: 19384.8 sq px</text>
  <text x="10" y="90">Angle: 0.0°</text>
  <text x="10" y="110">Time: 125.3 ms</text>

  <!-- Area Data (embedded as comments) -->
  <!-- polygonArea: 2822.8891 -->
  <!-- rectangleArea: 2830.9513 -->
</svg>
```

### Step 3: Extract Precomputed Data

**Script:** `extract-precomputed-rectangles.js`

```bash
node extract-precomputed-rectangles.js
```

**Duration:** ~30 seconds

**What it does:**
- Reads all 251 SVG files from `TestResults/MaxInscribedResults/`
- Parses each SVG to extract:
  - Rectangle corners (from `<polygon>` element)
  - Width, height, area, angle (from `<text>` legend)
  - Centroid (from `<circle>` marker)
  - Polygon area and rectangle area (from XML comments)
  - Algorithm type and computation time
- Combines into single JSON file with Map-friendly structure
- Outputs: `precomputed-rectangles.json`

**Console output:**
```
================================================================================
EXTRACTING PRE-COMPUTED RECTANGLES
================================================================================

Step 1: Loading valid combinations...
  Loaded 251 combinations

Step 2: Parsing SVG files from TestResults/MaxInscribedResults...
  Processed 50/251 SVG files...
  Processed 100/251 SVG files...
  Processed 150/251 SVG files...
  Processed 200/251 SVG files...
  Processed 250/251 SVG files...
  ✓ Successfully parsed 250 SVG files
  ⚠ Failed to parse 1 SVG files

Step 3: Calculating statistics...
  Total rectangles: 250
  Boundary-based: 38
  Optimized: 212
  Average computation time: 1842.3 ms
  Total computation time: 460.6 seconds

Step 4: Saving results...
  ✓ Results saved to: precomputed-rectangles.json
  File size: 253.1 KB

================================================================================
EXTRACTION COMPLETE!
Successfully extracted 250/251 pre-computed rectangles
================================================================================
```

**Output structure:**
```json
{
  "generatedAt": "2025-10-08T09:21:05.123Z",
  "totalCombinations": 251,
  "successfulComputations": 250,
  "failedComputations": 1,
  "totalComputationTimeMs": 460578,
  "averageComputationTimeMs": 1842.3,
  "statistics": {
    "boundaryBasedCount": 38,
    "optimizedCount": 212
  },
  "rectangles": [ /* 250 precomputed rectangles */ ]
}
```

### Step 4: Embed Data in SVG (Optional)

**Script:** `embed-rectangles-in-svg.js`

```bash
node embed-rectangles-in-svg.js
```

**Duration:** ~5 seconds

**What it does:**
1. Load `precomputed-rectangles.json`
2. Read `BlazorTest.WASM/wwwroot/Level1.svg`
3. Remove any existing `<script id="precomputed-rectangles">` element
4. Embed JSON as CDATA in `<defs>`:
```xml
<defs>
  <script type="application/json" id="precomputed-rectangles"><![CDATA[
{"generatedAt":"2025-10-18T17:08:02.240Z","totalCombinations":251,...}
  ]]></script>
</defs>
```
5. Write modified SVG back to file

**Output:** Level1.svg increases from ~225 KB to ~237.5 KB (+12.5 KB)

---

## Data Storage and Embedding

### Why Embed in SVG?

1. **Single File Deployment:** No separate JSON file to manage
2. **Atomic Updates:** SVG and data always in sync
3. **No Extra HTTP Request:** Data loads with the SVG
4. **Cache-Friendly:** Browser caches both together

### Data Structure in Memory (JavaScript)

```javascript
{
  rectangles: [/* array of 251 rectangle objects */],
  lookup: Map {
    "Ballroom_Room_1" => {
      rectangle: { corners, width, height, area, angle, centroid, type },
      polygonArea: 2822.8891,
      rectangleArea: 2830.9513,
      computationTimeMs: 52.6
    },
    "Ballroom_Room_1_Ballroom_Room_3" => { ... },
    // ... 249 more entries
  },
  metadata: {
    generatedAt: "2025-10-18T17:08:02.240Z",
    totalCombinations: 251,
    successfulComputations: 251
  }
}
```

**Lookup Key:** Alphabetically sorted section IDs joined with underscore

### File Locations

**Test Harness:**
```
C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness\
├── compute-all-combinations.js      # Generate valid combinations
├── run-tests.ps1                    # Run Playwright tests
├── test-runner.js                   # Node.js test runner
├── inscribed-rectangle.spec.js      # Playwright test specs
├── extract-precomputed-rectangles.js # Extract data from SVGs
├── embed-rectangles-in-svg.js       # Embed data in SVG
├── test.html                        # Test page HTML
├── valid-combinations.json          # 251 valid combinations (OUTPUT)
├── test-config.js                   # Test configuration (OUTPUT)
├── precomputed-rectangles.json      # Final precomputed data (OUTPUT)
└── TestResults/
    ├── MaxInscribedResults/        # Max-inscribed rectangle test results
    │   ├── Combo_0001.svg          # Visual result for combo 1
    │   ├── Combo_0002.svg
    │   ├── ...
    │   ├── Combo_0251.svg
    │   └── results.txt             # Raw test results
    └── BoardroomResults/           # Boardroom layout test results
        ├── Combo_XXXX.svg          # Visual boardroom layouts
        └── boardroom-summary.json  # Test summary
```

**Live Test Version:**
```
C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\BlazorTest.WASM\wwwroot\
├── Level1.svg                       # SVG with embedded data (optional)
├── precomputed-rectangles.json      # External JSON (fallback)
└── (other assets)
```

**Production Assets (Source):**
```
C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies\bcs-cerulean\base\SetsCmp\data\
├── Level1.svg                       # Production SVG
├── precomputed-rectangles.json      # Production precomputed data
└── Rooms.json                       # Graph connectivity
```

**Production Assets (S3):**
```
s3://bcs---assets-4933-b260/base/SetsCmp/data/
├── Level1.svg
├── precomputed-rectangles.json
└── Rooms.json
```

### Automatic URL Derivation

**SvgViewer.js logic:**
```javascript
async loadPrecomputedRectangles() {
    // Derive from SVG URL
    let precomputedUrl = 'precomputed-rectangles.json';  // Default

    if (this.svgUrl) {
        // Replace filename: "Level1.svg" → "precomputed-rectangles.json"
        const lastSlashIndex = this.svgUrl.lastIndexOf('/');
        if (lastSlashIndex >= 0) {
            precomputedUrl = this.svgUrl.substring(0, lastSlashIndex + 1)
                           + 'precomputed-rectangles.json';
        }
    }

    const response = await fetch(precomputedUrl);
    // ...
}
```

**Examples:**
- Local: `Level1.svg` → `precomputed-rectangles.json`
- Relative: `/data/Level1.svg` → `/data/precomputed-rectangles.json`
- S3: `https://s3.../base/SetsCmp/data/Level1.svg` → `https://s3.../base/SetsCmp/data/precomputed-rectangles.json`

---

## Runtime Architecture

### JavaScript Layer (SvgViewer.js)

#### Loading Precomputed Data

```javascript
async loadPrecomputedRectangles() {
    if (this.precomputedRectangles) {
        return this.precomputedRectangles;
    }

    try {
        let data = null;

        // 1. Try to load from embedded script element
        const svgDoc = this.svg.node.ownerDocument;
        const scriptElement = svgDoc.getElementById('precomputed-rectangles');

        if (scriptElement) {
            const jsonText = scriptElement.textContent;
            data = JSON.parse(jsonText);
            console.log('[precomputed] Loaded from embedded SVG');
        } else {
            // 2. Fallback to external JSON file
            const response = await fetch(precomputedUrl);
            data = await response.json();
            console.log('[precomputed] Loaded from external JSON');
        }

        // 3. Build lookup map
        const lookup = new Map();
        for (const rect of data.rectangles) {
            lookup.set(rect.key, {
                rectangle: rect.rectangle,
                polygonArea: rect.polygonArea,
                rectangleArea: rect.rectangleArea,
                computationTimeMs: rect.computationTimeMs
            });
        }

        this.precomputedRectangles = {
            rectangles: data.rectangles,
            lookup: lookup,
            metadata: { /* ... */ }
        };

        return this.precomputedRectangles;
    } catch (error) {
        console.warn('[precomputed] Failed to load:', error.message);
        this.precomputedRectangles = {
            rectangles: [],
            lookup: new Map(),
            metadata: {}
        };
        return this.precomputedRectangles;
    }
}
```

#### Looking Up Rectangle Data

```javascript
async lookupPrecomputedRectangle(pathIds) {
    if (!pathIds || pathIds.length === 0) {
        return null;
    }

    await this.loadPrecomputedRectangles();

    // Create sorted key to match precomputed format
    const sortedKey = pathIds.slice().sort().join('_');

    const rectData = this.precomputedRectangles.lookup.get(sortedKey);
    if (rectData) {
        console.log(`[precomputed] ✓ Found rectangle for ${pathIds.length} sections`);
        return rectData.rectangle;  // Return just the rectangle shape for drawing
    }

    console.debug(`[precomputed] ✗ No data for: ${sortedKey}`);
    return null;
}
```

#### Retrieving Area Data

```javascript
export async function getAreaData(containerId) {
    const instance = instances.get(containerId);
    if (!instance || !instance.selectedIds || instance.selectedIds.size === 0) {
        return null;
    }

    const selectedPaths = Array.from(instance.selectedIds);
    await instance.loadPrecomputedRectangles();

    const sortedKey = selectedPaths.slice().sort().join('_');
    const rectData = instance.precomputedRectangles.lookup.get(sortedKey);

    if (rectData) {
        return {
            polygonArea: rectData.polygonArea,
            rectangleArea: rectData.rectangleArea,
            computationTimeMs: rectData.computationTimeMs
        };
    }

    return null;  // Invalid combination
}
```

### C# Interop Layer (SvgViewerJS.cs)

```csharp
public class SvgViewerJS : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;

    public SvgViewerJS(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/LazyMagic.BlazorSvg/SvgViewer.js").AsTask());
    }

    public async ValueTask<AreaData?> GetAreaDataAsync()
    {
        if (containerId == null)
            throw new InvalidOperationException("InitAsync must be called first");

        var module = await moduleTask.Value;
        var result = await module.InvokeAsync<AreaData?>("getAreaData", containerId);
        return result;
    }
}

public class AreaData
{
    [System.Text.Json.Serialization.JsonPropertyName("polygonArea")]
    public double? PolygonArea { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("rectangleArea")]
    public double? RectangleArea { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("computationTimeMs")]
    public double? ComputationTimeMs { get; set; }
}
```

**Important:** JSON property names must match JavaScript (camelCase) using `JsonPropertyName` attributes.

### Blazor Component Layer (SvgViewer.razor)

```razor
@code {
    [Parameter] public EventCallback<List<string>> PathsChanged { get; set; }

    private SvgViewerJS? svgViewerJS;

    protected override async Task OnInitializedAsync()
    {
        svgViewerJS = new SvgViewerJS(JSRuntime);
        svgViewerJS.PathsChangedEvent += OnPathsChanged;
    }

    private void OnPathsChanged(List<string> paths)
    {
        PathsChanged.InvokeAsync(paths);
    }

    public async Task<AreaData?> GetAreaDataAsync() =>
        await svgViewerJS!.GetAreaDataAsync();
}
```

### Display and Validation

**Page-Level Logic (SVGTestPage.razor):**

```razor
<div class="mt-3">
    <div>Currently Selected: <code>@(string.Join(", ", currentPaths))</code></div>

    @if (areaData != null && areaData.PolygonArea.HasValue && areaData.RectangleArea.HasValue)
    {
        var comboArea = areaData.PolygonArea.Value;
        var rectArea = areaData.RectangleArea.Value;

        // If rectangle area is larger than combo area (algorithm error),
        // use combo area for both
        if (rectArea > comboArea)
        {
            rectArea = comboArea;
        }

        <div class="mt-2">
            <strong>Area Measurements (Square SVG Inches):</strong>
            <div class="ms-3">
                <div>Combo Area: <code>@($"{comboArea:F2}")</code> sq in</div>
                <div>Inscribed Rectangle Area: <code>@($"{rectArea:F2}")</code> sq in</div>
            </div>
        </div>
    }
</div>

@code {
    private List<string> currentPaths = new();
    private AreaData? areaData = null;

    private async void OnPathsChanged(List<string> paths)
    {
        currentPaths = paths;
        await UpdateAreaData();
        StateHasChanged();
    }

    private async Task UpdateAreaData()
    {
        if (svgViewer != null)
        {
            areaData = await svgViewer.GetAreaDataAsync();
        }
    }
}
```

**Validation Through Absence:**

**Key Design Decision:** Invalid combinations are validated implicitly by the absence of data.

- **Valid Combination:** `getAreaData()` returns data → Areas displayed, rectangle drawn
- **Invalid Combination:** `getAreaData()` returns `null` → Nothing displayed, no rectangle

**Benefits:**
1. No explicit validation code needed
2. No error messages to manage
3. Clear visual feedback (missing visualization = invalid)
4. Single source of truth (precomputed data defines validity)

---

## Deployment Pipeline

### 1. Rebuild LazyMagic.BlazorSvg Package

After changes to SvgViewer.js or algorithms:

```powershell
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic
dotnet build LazyMagic.BlazorSvg\LazyMagic.BlazorSvg.csproj -c Release
```

**What happens:**
- Builds package: `LazyMagic.BlazorSvg.3.0.1.nupkg`
- Copies to: `C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\Packages\`
- Clears NuGet cache: `%USERPROFILE%\.nuget\packages\lazymagic.blazorsvg\3.0.1\`

### 2. Update Assets in BCTenancies

After regenerating precomputed data:

```powershell
# Copy SVG
Copy-Item "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\BlazorTest.WASM\wwwroot\Level1.svg" `
          "C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies\bcs-cerulean\base\SetsCmp\data\Level1.svg" -Force

# Copy precomputed rectangles
Copy-Item "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness\precomputed-rectangles.json" `
          "C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies\bcs-cerulean\base\SetsCmp\data\precomputed-rectangles.json" -Force
```

### 3. Deploy Assets to S3

```powershell
cd C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies
pwsh -Command "& { . ./Import-LzAws.ps1; Deploy-AssetsAws }"
```

**What happens:**
- Syncs all assets from `BCTenancies` to S3 bucket: `bcs---assets-4933-b260`
- Uploads to: `s3://bcs---assets-4933-b260/base/SetsCmp/data/`
- Only uploads changed files (idempotent)
- Processes all tenants and subtenants (system, bcs, free, cerulean, walv, conventionplanit)

### 4. Rebuild SetsApp

```powershell
cd C:\Users\noaht\source\repos\_Dev\BCProjects\SetsApp
dotnet build -c Release
```

**What happens:**
- References updated LazyMagic.BlazorSvg package (3.0.1)
- Builds WASMApp with new SvgViewer.js changes

### 5. Test Locally

```powershell
cd WASMApp
dotnet run
```

**Verify:**
- Browser console shows: `[precomputed] Loading precomputed rectangles from: https://s3.../precomputed-rectangles.json`
- Selection shows: `[precomputed] ✓ Found precomputed rectangle for X sections`
- Rectangle matches test harness results (larger, better fit)

### 6. Deploy to Production

```powershell
cd WASMApp
.\deploywebapp.ps1 -AppName setsapp -Publish $true
```

**What happens:**
- Publishes WASMApp in Release mode
- Syncs to S3: `bcs---webapp-setsapp-4933-b260`
- Invalidates CloudFront cache
- Production app loads assets from CDN

---

## Troubleshooting

### Precomputed Rectangles Not Loading

**Symptom:**
```
[precomputed] ✗ No precomputed data for: Ballroom_Room_1_Ballroom_Room_3
[boundary-based] Best result: 154.8 × 221.2 = 34248.0 sq px
```

**Cause:** URL mismatch or file not deployed

**Check:**
1. Browser console: What URL is it trying?
   ```
   [precomputed] Loading precomputed rectangles from: <URL>
   ```
2. Does that URL exist? Open in browser tab
3. Check S3 bucket:
   ```powershell
   aws s3 ls s3://bcs---assets-4933-b260/base/SetsCmp/data/ --profile bc-dev
   ```

**Fix:**
- Re-run Deploy-AssetsAws to ensure file is on S3
- Check that SvgViewer.js stores `this.svgUrl` correctly
- Verify URL derivation logic (check for typos in path)

### Test Harness Fails

**Symptom:**
```
Error: page.goto: net::ERR_CONNECTION_REFUSED
```

**Cause:** Web server not running

**Fix:**
```powershell
# Kill any existing servers
Get-Process -Name node | Stop-Process -Force

# Restart test harness
.\run-tests.ps1
```

### Rectangle Too Small in Production

**Symptom:** Production shows 34,248 sq px but test harness shows 39,413 sq px

**Cause:** Precomputed data out of sync with SVG naming

**Fix:**
1. Check naming convention matches:
   - SVG IDs: `Ballroom_Room_1`, `Ballroom_Aisle_35`, etc.
   - Precomputed keys: `Ballroom_Room_1_Ballroom_Room_3`, etc.
2. Regenerate all:
   ```bash
   node compute-all-combinations.js
   node test-runner.js
   node extract-precomputed-rectangles.js
   ```
3. Redeploy assets to S3

### Double-Highlighting on Deselect

**Symptom:** Orange outline gets brighter when deselecting

**Cause:** `highlight()` called twice in `unselectPath()`

**Fix:** Already fixed in SvgViewer.js line 2602:
```javascript
this.updateGlobalBoundingBox();
this.isUpdating = prevIsUpdating;
// highlight() is already called by updateGlobalBoundingBox(), no need to call again
return true;
```

### SVG Not Loading from S3

**Symptom:** `HTTP error! status: 403` or `404`

**Check:**
1. S3 bucket permissions (public read for assets bucket)
2. CORS configuration on S3 bucket
3. CloudFront distribution settings
4. URL is correct (check browser network tab)

**Debug:**
```javascript
// In browser console
fetch('https://bcs---assets-4933-b260.s3.us-west-2.amazonaws.com/base/SetsCmp/data/Level1.svg')
  .then(r => console.log('Status:', r.status))
  .catch(e => console.error('Error:', e));
```

---

## Performance Benchmarks

### Algorithm Comparison

| Combination | Paths | Vertices | Boundary-Based | Optimized | Improvement | Used |
|-------------|-------|----------|----------------|-----------|-------------|------|
| Room 1 only | 1 | 4 | 125ms, 19,385 sq px | N/A | N/A | Boundary |
| Rooms 1+3 | 2 | 8 | 118ms, 19,385 sq px | N/A | N/A | Boundary |
| Rooms 3+5+Grand+Aisles | 7 | 13 | 120ms, 34,248 sq px | 2,415ms, 39,413 sq px | +15.1% | Optimized |
| All 15 sections | 15 | 42 | 187ms, 48,220 sq px | 3,842ms, 52,105 sq px | +8.1% | Optimized |

### Precomputed Statistics

- **Total combinations:** 251
- **Successful:** 250 (99.6%)
- **Failed:** 1 (complex shape, algorithm timeout)
- **Boundary-based:** 38 (15.2%)
- **Optimized:** 212 (84.8%)
- **Average time:** 1,842ms per combination
- **Total time:** 7.7 minutes for all 251
- **File size:** 253 KB (uncompressed)
- **Load time:** < 50ms (cached), < 200ms (first load from S3)

### Build Time Performance

**Current System:**
- Test Harness: 15-20 minutes (898,900ms total, 3,581ms average)
- Extraction: 30 seconds
- Embedding: 5 seconds
- **Total: ~20 minutes**

**Bottleneck:** Inscribed rectangle computation (boundary-based + optimized algorithms)

**Optimization Opportunities:**
1. Use Node.js worker threads for parallel computation
2. Implement incremental builds (only recompute changed combinations)
3. Cache intermediate results (merged polygons, oriented bounding boxes)

### Runtime Performance

**Current System:**
- Precomputed data load: <100ms (one-time, cached)
- Rectangle lookup: <1ms (Map.get() is O(1))
- Area data retrieval: <1ms
- **Total: Instant** ✅

**Memory Usage:**
- Precomputed data: ~148 KB in memory
- Lookup map: 251 entries × ~600 bytes = ~150 KB
- **Total: <500 KB**

### File Size Impact

**SVG File:**
- Original Level1.svg: ~225 KB
- With embedded data: ~237.5 KB
- **Overhead: +12.5 KB** (5.5% increase)

**Acceptable because:**
1. Single HTTP request (no external JSON)
2. Browser caching applies to combined file
3. Gzip compression reduces impact (JSON compresses well)

---

## Extending with Size Constraints

### Scenario: Rectangles with Specific Size Requirements

**Example:** Find the largest rectangle that fits within specific dimension constraints:
- Minimum width: 100 SVG inches
- Maximum width: 150 SVG inches
- Minimum height: 80 SVG inches
- Maximum height: 120 SVG inches

### Recommended Approach: Precompute Multiple Scenarios

#### Option 1: Store Multiple Rectangle Sizes Per Combination

Instead of storing just the largest rectangle, store an array of rectangles at different size intervals:

```javascript
{
  "key": "Ballroom_Room_1",
  "sections": ["Ballroom_Room_1"],
  "rectangles": [
    {
      "constraint": "largest",
      "width": 116.5,
      "height": 95.8,
      "area": 11162.4,
      "corners": [...],
      "angle": 90.0
    },
    {
      "constraint": "100x80_min",
      "width": 110.0,
      "height": 85.0,
      "area": 9350.0,
      "corners": [...],
      "angle": 90.0
    },
    {
      "constraint": "custom_aspect_ratio",
      "width": 105.0,
      "height": 90.0,
      "area": 9450.0,
      "corners": [...],
      "angle": 90.0
    }
  ],
  "polygonArea": 2822.8891
}
```

**Lookup Pattern:**
```javascript
function lookupConstrainedRectangle(pathIds, constraints) {
    const sortedKey = pathIds.slice().sort().join('_');
    const data = precomputedRectangles.lookup.get(sortedKey);

    if (!data) return null;

    // Find best matching rectangle for constraints
    return data.rectangles.find(rect =>
        rect.width >= constraints.minWidth &&
        rect.width <= constraints.maxWidth &&
        rect.height >= constraints.minHeight &&
        rect.height <= constraints.maxHeight
    );
}
```

#### Option 2: Separate Constraint-Specific Files

Create multiple precomputed files for different use cases:

```
precomputed-rectangles.json          (largest rectangle)
precomputed-rectangles-standard.json (standard conference sizes)
precomputed-rectangles-banquet.json  (banquet table layouts)
precomputed-rectangles-classroom.json (classroom seating)
```

Load the appropriate file based on user selection.

#### Option 3: Interval-Based Indexing

Precompute rectangles at specific size intervals and store in a grid structure:

```javascript
{
  "key": "Ballroom_Room_1",
  "sections": ["Ballroom_Room_1"],
  "sizeGrid": {
    "50-100x50-75": { width: 95, height: 70, area: 6650, corners: [...] },
    "100-150x75-100": { width: 116.5, height: 95.8, area: 11162.4, corners: [...] },
    "150-200x100-125": null  // No rectangle fits this constraint
  }
}
```

### Performance Considerations for Extended System

**Computation Time:**
- Original: 15-20 minutes for 251 combinations × 1 rectangle = ~3.6s per combination
- With 5 constraints: 251 × 5 = 75-100 minutes (still acceptable for build-time)
- With 10 constraints: 251 × 10 = 150-200 minutes (may need optimization)

**Data Size:**
- Original: 148 KB for 251 rectangles
- With 5 constraints: ~740 KB (still acceptable)
- With 10 constraints: ~1.5 MB (consider compression)

**Optimization Strategies:**
1. **Parallel Processing:** Use worker threads to compute multiple combinations simultaneously
2. **Incremental Updates:** Only recompute changed combinations
3. **Smart Constraints:** Eliminate impossible constraints early
4. **Data Compression:** Gzip JSON before embedding (browsers decompress automatically)

---

## Best Practices

### 1. Precompute Everything Possible

**Lesson:** Even expensive computations (15-20 minutes) are acceptable at build time if they enable instant runtime performance.

**Application:** Don't hesitate to precompute additional constraints if they provide value.

### 2. Use Sorted Keys for Lookups

**Lesson:** Combination keys must be deterministic regardless of selection order.

**Implementation:**
```javascript
const sortedKey = pathIds.slice().sort().join('_');
```

This ensures `["Room_1", "Room_3"]` and `["Room_3", "Room_1"]` produce the same key.

### 3. Validate Through Absence

**Lesson:** Missing precomputed data is a valid validation strategy.

**Benefits:**
- Single source of truth
- No duplicate validation logic
- Clear user feedback

### 4. Match JSON Property Names Between Languages

**Lesson:** JavaScript uses camelCase, C# uses PascalCase. Use `JsonPropertyName` attributes.

```csharp
[System.Text.Json.Serialization.JsonPropertyName("polygonArea")]
public double? PolygonArea { get; set; }
```

### 5. Handle Algorithm Imperfections Gracefully

**Lesson:** Inscribed rectangle area can occasionally exceed polygon area due to numerical precision.

**Solution:**
```csharp
if (rectArea > comboArea)
{
    rectArea = comboArea;  // Cap to polygon area
}
```

### 6. Embed Data in SVG for Simplicity

**Lesson:** Embedded data eliminates deployment complexity and synchronization issues.

**Advantages:**
- Single file to deploy
- Atomic updates
- No extra HTTP request
- Cache-friendly

### 7. Build Pipeline Matters

**Lesson:** The order of operations is critical:

1. Generate test SVGs (15-20 min)
2. Extract data to JSON (30 sec)
3. **Copy JSON to correct location** (often forgotten!)
4. Embed in SVG (5 sec) - optional
5. Rebuild Blazor app
6. Restart dev server
7. Hard refresh browser

Skipping any step causes old data to persist.

### 8. Use Clear Logging

**Lesson:** Console logs should clearly indicate success/failure:

```javascript
console.log('[precomputed] ✓ Found rectangle for 2 sections');
console.debug('[precomputed] ✗ No data for: Room_1_Room_5');
```

Use prefixes (`[precomputed]`, `[outline]`, `[winding]`) to filter logs.

---

## References

### Code Locations

**LazyMagic.BlazorSvg:**
- `SvgViewer.razor`: Blazor component
- `SvgViewerJS.cs`: C# interop wrapper
- `wwwroot/SvgViewer.js`: Main JavaScript (2,700+ lines)
- `wwwroot/SvgViewerAlgorithms.js`: Core algorithms (winding, hull, utils)
- `wwwroot/SvgViewerBoundaryBased.js`: Boundary-based + hybrid (1,500+ lines)
- `wwwroot/SvgViewerOptimized.js`: Optimized algorithm (800+ lines)

**Test Harness:**
- `FloorMat/compute-all-combinations.js`: Validation + combination generator
- `FloorMat/test-runner.js`: Node.js test runner
- `FloorMat/run-tests.ps1`: PowerShell test runner (Playwright)
- `FloorMat/inscribed-rectangle.spec.js`: Playwright test specs
- `FloorMat/extract-precomputed-rectangles.js`: SVG parser + JSON generator
- `FloorMat/embed-rectangles-in-svg.js`: SVG embedding script

**Production:**
- `BCProjects/SetsApp/SetsCmp/SetsCmp.csproj`: References LazyMagic.BlazorSvg
- `BCProjects/BCTenancies/bcs-cerulean/base/SetsCmp/data/`: Asset source
- S3 bucket: `bcs---assets-4933-b260`

### Key Algorithms

**Winding Number Algorithm:**
- Point-in-polygon test
- Reference: http://geomalgorithms.com/a03-_inclusion.html

**Convex Hull (Graham Scan):**
- Used for fallback boundary generation
- Time complexity: O(n log n)

**Rotating Calipers:**
- Minimum bounding box calculation
- Reference: Shamos (1978)

**Largest Inscribed Rectangle:**
- Grid-based exhaustive search with early termination
- Adaptive centroid sampling
- Reference: Custom hybrid approach

### Related Documentation

- **BoardroomLayoutPipeline.md**: Parallel boardroom layout system
- **EmbedData.md**: Quick reference for embedding workflow
- **FloorMat/testharness.md**: Complete test harness documentation

---

**Author:** LazyMagic Development Team
**For Support:** See test-harness documentation and troubleshooting section
