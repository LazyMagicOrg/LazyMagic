# Hollow Square Layout Pipeline Documentation

## Overview

This document describes the parallel precomputation system for **hollow square-style inscribed rectangles** in SVG floor plan combinations. This system runs alongside the existing largest-rectangle and boardroom systems and follows the same architectural patterns.

**Related Documentation:**
- [InscribedRectangle-Guide.md](./InscribedRectangle-Guide.md) - Complete system architecture
- [MaxInscribedLayoutPipeline.md](./MaxInscribedLayoutPipeline.md) - Max-inscribed rectangle system
- [BoardroomLayoutPipeline.md](./BoardroomLayoutPipeline.md) - Boardroom layout system

**Created:** 2025-10-25

---

## Table of Contents

1. [Hollow Square Layout Constraints](#hollow-square-layout-constraints)
2. [Architecture Overview](#architecture-overview)
3. [Pipeline Components](#pipeline-components)
4. [Running the Pipeline](#running-the-pipeline)
5. [Data Structures](#data-structures)
6. [Algorithm Details](#algorithm-details)
7. [Integration with Existing System](#integration-with-existing-system)
8. [Performance Considerations](#performance-considerations)

---

## Hollow Square Layout Constraints

### What is a Hollow Square Layout?

A hollow square layout consists of tables arranged in a square perimeter with an open center:

```
┌─────────────────────┐
│                     │
│  Tables (2.5' deep) │
│                     │
│    ┌─────────┐     │
│    │  Open   │     │
│    │ Center  │     │
│    └─────────┘     │
│                     │
│  Tables (2.5' deep) │
│                     │
└─────────────────────┘
```

### Fixed Constraints

- **Fixed Table Depth:** 2.5 ft (tables face inward and outward)
- **Minimum Dimensions:**
  - Minimum outer width: 14 ft
  - Minimum outer height: 14 ft
  - Minimum inner clearance: 4 ft × 4 ft (center opening)
- **Total Depth:** 5 ft (2.5 ft × 2 tables on each side)
- **Variable Size:** Both width and height can increase independently
- **Rotation:** Can be oriented at any angle within the polygon

### Layout Dimensions

**Outer Rectangle:**
- Minimum: 14 ft × 14 ft
- Maximum: Constrained by polygon boundary

**Inner Rectangle (Open Center):**
- Calculated as: outer dimensions - (2 × 5 ft)
- Example: 14 ft × 14 ft outer → 4 ft × 4 ft inner
- Example: 24 ft × 20 ft outer → 14 ft × 10 ft inner

### Key Terminology

- **Hollow Square:** The complete layout including outer perimeter tables and inner open space
- **Outer Rectangle:** The bounding rectangle of the table arrangement
- **Inner Rectangle:** The usable open space in the center
- **Table Depth:** 2.5 ft on each side, 5 ft total depth

---

## Architecture Overview

The hollow square pipeline is a **parallel system** that mirrors the existing largest-rectangle and boardroom infrastructure:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           PARALLEL SYSTEMS                                │
├──────────────────────┬──────────────────────┬─────────────────────────────┤
│  Largest Rectangles  │  Boardroom Layouts   │  Hollow Square Layouts      │
├──────────────────────┼──────────────────────┼─────────────────────────────┤
│ test-runner.js       │ test-runner-         │ test-runner-hollowsquare.js │
│                      │   boardroom.js       │                             │
│ extract-precomputed- │ extract-precomputed- │ extract-precomputed-        │
│   rectangles.js      │   boardroom.js       │   hollowsquare.js           │
│ embed-rectangles-    │ embed-boardroom-     │ embed-hollowsquare-in-      │
│   in-svg.js          │   in-svg.js          │   svg.js                    │
│ precomputed-         │ precomputed-         │ precomputed-                │
│   rectangles.json    │   boardroom.json     │   hollowsquare.json         │
│ TestResults/         │ TestResults/         │ TestResults/                │
│   MaxInscribedResults│   BoardroomResults   │   HollowSquareResults       │
│ <script id=          │ <script id=          │ <script id=                 │
│   "precomputed-      │   "precomputed-      │   "precomputed-             │
│   rectangles">       │   boardroom">        │   hollowsquare">            │
└──────────────────────┴──────────────────────┴─────────────────────────────┘
```

All three systems can run **independently** and are **embedded together** in the same SVG file.

**See Also:**
- [MaxInscribedLayoutPipeline.md](./MaxInscribedLayoutPipeline.md) for the max-inscribed system
- [BoardroomLayoutPipeline.md](./BoardroomLayoutPipeline.md) for the boardroom system

---

## Pipeline Components

### 1. Hollow Square Algorithm (`SvgViewerHollowSquare.js`)

**Location:** `LazyMagic.BlazorSvg/wwwroot/SvgViewerHollowSquare.js`

**Purpose:** Core algorithm for finding the largest hollow square layout that fits in a polygon.

**Key Functions:**

```javascript
findHollowSquareLayout(polygon, options)
// Returns: { corners, width, height, area, angle, centroid, innerWidth, innerHeight, type }

isPointInPolygon(point, polygon)
// Returns: boolean

isRectangleInsidePolygon(corners, polygon)
// Returns: boolean

calculateRectangleCorners(centroid, width, height, angleDegrees)
// Returns: [corner1, corner2, corner3, corner4]
```

**Algorithm Strategy:**

1. Test multiple rotation angles (0° - 180°, default 36 samples = every 5°)
2. For each angle, test a grid of centroid positions (default 3×3 = 9 points)
3. For each angle/centroid combination:
   - Start with minimum dimensions (14 ft × 14 ft)
   - Expand width and height independently using binary search
   - Test if outer rectangle fits in polygon
   - Verify minimum inner clearance (4 ft × 4 ft)
   - Continue until maximum size found
4. Return the layout with maximum area

**Options:**

```javascript
{
  minOuterSize: 14,           // Minimum outer dimension (ft)
  tableDepth: 2.5,            // Table depth (ft)
  minInnerClearance: 4,       // Minimum inner opening (ft)
  angleSamples: 36,           // Number of angles to test
  centroidSamples: 9,         // Grid density (3×3)
  debugMode: false            // Enable detailed logging
}
```

### 2. Test Runner (`test-runner-hollowsquare.js`)

**Location:** `LazyMagic.BlazorSvg/FloorMat/test-runner-hollowsquare.js`

**Purpose:** Generate hollow square layouts for all 251 valid combinations.

**Usage:**

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
node test-runner-hollowsquare.js
```

**Output:**

- Directory: `LazyMagic.BlazorSvg/TestResults/HollowSquareResults/`
- Files: `Combo_0001.svg` through `Combo_0251.svg`
- Summary: `hollowsquare-summary.json`

**Each SVG contains:**

```xml
<!-- Area Data (Square SVG Inches) -->
<!-- polygonArea: 2822.8891 -->
<!-- hollowSquareArea: 910.0000 -->
<!-- outerWidth: 26.0 -->
<!-- outerHeight: 35.0 -->
<!-- innerWidth: 16.0 -->
<!-- innerHeight: 25.0 -->
<!-- computationTimeMs: 145.7 -->
```

**Process for each combination:**

1. Load SVG and extract path data
2. Parse paths to line segments
3. Merge coincident points
4. Find unified boundary polygon
5. Calculate polygon area
6. Run `findHollowSquareLayout()` algorithm
7. Generate visualization SVG
8. Save to `TestResults/HollowSquareResults/`

### 3. Data Extraction (`extract-precomputed-hollowsquare.js`)

**Location:** `LazyMagic.BlazorSvg/FloorMat/extract-precomputed-hollowsquare.js`

**Purpose:** Parse generated SVG files and extract hollow square data into JSON.

**Usage:**

```bash
node extract-precomputed-hollowsquare.js
```

**Output:** `precomputed-hollowsquare.json`

**Data Structure:**

```json
{
  "generatedAt": "2025-10-25T12:00:00.000Z",
  "totalCombinations": 251,
  "successfulComputations": 251,
  "failedComputations": 0,
  "totalComputationTimeMs": 36550,
  "averageComputationTimeMs": 145.6,
  "hollowSquareLayouts": [
    {
      "key": "Ballroom_Room_1",
      "sections": ["Ballroom_Room_1"],
      "hollowSquareLayout": {
        "corners": [
          {"x": 280.5, "y": 40.2},
          {"x": 306.5, "y": 40.2},
          {"x": 306.5, "y": 120.2},
          {"x": 280.5, "y": 120.2}
        ],
        "width": 26.0,
        "height": 80.0,
        "area": 2080.0,
        "angle": 0,
        "centroid": {"x": 293.5, "y": 80.2},
        "innerWidth": 16.0,
        "innerHeight": 70.0,
        "innerArea": 1120.0,
        "type": "hollowsquare"
      },
      "polygonArea": 2822.8891,
      "hollowSquareArea": 527.5234,
      "computationTimeMs": 145.6
    }
    // ... 250 more entries
  ]
}
```

**Extraction Process:**

1. Load `valid-combinations.json` (251 combos)
2. For each combination:
   - Read `TestResults/HollowSquareResults/Combo_XXXX.svg`
   - Extract area data from XML comments
   - Parse hollow square rectangle path
   - Calculate dimensions and properties
   - Build layout object
3. Write `precomputed-hollowsquare.json`

### 4. SVG Embedding (`embed-hollowsquare-in-svg.js`)

**Location:** `LazyMagic.BlazorSvg/FloorMat/embed-hollowsquare-in-svg.js`

**Purpose:** Embed hollow square data directly into Level1.svg.

**Usage:**

```bash
node embed-hollowsquare-in-svg.js
```

**Embedded Structure:**

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="...">
  <defs>
    <!-- Existing largest rectangles data -->
    <script type="application/json" id="precomputed-rectangles"><![CDATA[
      {...}
    ]]></script>

    <!-- Existing boardroom data -->
    <script type="application/json" id="precomputed-boardroom"><![CDATA[
      {...}
    ]]></script>

    <!-- NEW: Hollow square layouts data -->
    <script type="application/json" id="precomputed-hollowsquare"><![CDATA[
      {"generatedAt":"2025-10-25T12:00:00.000Z","totalCombinations":251,...}
    ]]></script>
  </defs>
  <!-- Rest of SVG content -->
</svg>
```

**File Size Impact:**

- Original Level1.svg: ~225 KB
- With rectangles data: ~237.5 KB (+12.5 KB)
- With boardroom data: ~250 KB (+12.5 KB)
- With hollow square data: ~262.5 KB (+12.5 KB more)
- **Total overhead: ~37.5 KB** (17% increase)

---

## Running the Pipeline

### Complete Build Process (New Multi-Project Workflow)

FloorMat now supports processing multiple SVG projects automatically:

```bash
# 1. Navigate to FloorMat directory
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.FloorMat\FloorMat"

# 2. Place your SVG and combinations file in input/:
#    - input/YourProject.svg
#    - input/YourProject-combinations.json

# 3. Run the complete pipeline (auto-processes all SVGs in input/)
npm run process

# This will:
# - Auto-detect all .svg files in input/
# - Generate all three layout types (max-inscribed, boardroom, hollow square)
# - Extract precomputed data for each project
# - Embed data into output/[ProjectName]-output/[ProjectName].svg
# - Preserve your original SVG in input/

# 4. Navigate back to root
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic"

# 5. Copy embedded SVG to application (if needed)
# The processed SVG is in: FloorMat/output/[ProjectName]-output/[ProjectName].svg

# 6. Run application
dotnet run --project BlazorTest.WASM/BlazorTest.WASM.csproj
```

**Total Time:** ~20 minutes per project

### New Directory Structure

```
FloorMat/
├── input/                          # Your original files (preserved)
│   ├── Level1.svg
│   ├── Level1-combinations.json
│   ├── Level2.svg                  # You can have multiple projects
│   └── Level2-combinations.json
├── output/                         # Auto-generated outputs
│   ├── Level1-output/
│   │   ├── Level1.svg              # SVG with embedded data
│   │   ├── Level1-rectangles.json
│   │   ├── Level1-boardroom.json
│   │   ├── Level1-hollowsquare.json
│   │   └── TestResults/
│   └── Level2-output/
│       └── ...
├── process-all.js                  # Main orchestrator
├── extract-precomputed-project.js  # Project-aware extraction
└── embed-project.js                # Project-aware embedding
```

### Single Command Workflow

The new pipeline consolidates everything into a single command:

```bash
npm run process
```

**What happens:**
1. Scans `input/` for all .svg files
2. For each SVG (e.g., `Level1.svg`):
   - Loads `Level1-combinations.json`
   - Generates configs for all three algorithms
   - Runs test-runner for max-inscribed, boardroom, and hollow square
   - Extracts data to `Level1-rectangles.json`, `Level1-boardroom.json`, `Level1-hollowsquare.json`
   - Embeds all data into `output/Level1-output/Level1.svg`
   - Preserves original `input/Level1.svg`

### Independent Operation

The pipeline still supports processing individual projects:

- Each SVG creates its own isolated output directory
- No file conflicts between projects
- Original SVGs are never modified
- Can re-run pipeline without losing source files

### Processing Multiple Projects

If you have multiple SVGs in `input/`:

```bash
npm run process
```

Processes all of them sequentially:
- `Level1.svg` → `output/Level1-output/`
- `Level2.svg` → `output/Level2-output/`
- `CustomProject.svg` → `output/CustomProject-output/`

**Total Time:** ~20 minutes × number of projects

---

## Data Structures

### Hollow Square Layout Object

```javascript
{
  corners: [
    {x: 280.5, y: 40.2},  // Bottom-left
    {x: 306.5, y: 40.2},  // Bottom-right
    {x: 306.5, y: 120.2}, // Top-right
    {x: 280.5, y: 120.2}  // Top-left
  ],
  width: 26.0,            // Outer width (ft)
  height: 80.0,           // Outer height (ft)
  area: 2080.0,           // Outer area (pre-transform)
  angle: 45.0,            // Rotation in degrees (0-180°)
  centroid: {x: 293.5, y: 80.2},
  innerWidth: 16.0,       // Inner clearance width (ft)
  innerHeight: 70.0,      // Inner clearance height (ft)
  innerArea: 1120.0,      // Inner area (pre-transform)
  type: 'hollowsquare'    // Type identifier
}
```

### Comparison: Hollow Square vs Other Layouts

| Property | Largest Rectangle | Boardroom Layout | Hollow Square Layout |
|----------|------------------|------------------|---------------------|
| **Width** | Variable | **13 ft (fixed)** | Variable (min 14 ft) |
| **Height** | Variable | **14, 20, 26... ft** | Variable (min 14 ft) |
| **Inner Space** | None | None | **4+ ft clearance** |
| **Goal** | Maximize area | Maximize sets | Maximize outer area with inner clearance |
| **Rotation** | 0-180° | 0-180° | 0-180° |
| **Algorithm** | Boundary-based + Optimized | Grid search with incremental sizing | Binary search expansion |
| **Type** | 'boundary-based' or 'optimized' | 'boardroom' | 'hollowsquare' |
| **Extra Data** | None | `sets`, `tables` | `innerWidth`, `innerHeight`, `innerArea` |

---

## Algorithm Details

### Search Strategy

The hollow square algorithm uses **binary search expansion** with **multi-angle testing**:

1. **Angle Iteration:** Test 36 angles (every 5°) from 0-180°
2. **Centroid Grid:** For each angle, test 9 centroids in a 3×3 grid
3. **Binary Search Expansion:** For each angle/centroid:
   - Start with minimum dimensions (14 ft × 14 ft)
   - Binary search to find maximum width (while maintaining height)
   - Binary search to find maximum height (while maintaining width)
   - Iterate expansion until convergence
   - Verify inner clearance constraint (4 ft × 4 ft minimum)
4. **Validation:** Ensure all corners inside polygon and inner space ≥ minimum

**Total Tests per Combo:**

- 36 angles × 9 centroids × ~20 average expansion iterations
- ≈ **6,480 rectangle fit tests** per combination
- Fast due to simple point-in-polygon checks

### Expansion Algorithm

```javascript
function expandHollowSquare(centroid, angle, polygon, options) {
    let width = options.minOuterSize;
    let height = options.minOuterSize;

    // Binary search for maximum width
    let low = width, high = polygonWidth;
    while (high - low > 0.1) {
        let mid = (low + high) / 2;
        if (fitsInPolygon(centroid, mid, height, angle, polygon)) {
            width = mid;
            low = mid;
        } else {
            high = mid;
        }
    }

    // Binary search for maximum height
    low = height, high = polygonHeight;
    while (high - low > 0.1) {
        let mid = (low + high) / 2;
        if (fitsInPolygon(centroid, width, mid, angle, polygon)) {
            height = mid;
            low = mid;
        } else {
            high = mid;
        }
    }

    // Verify inner clearance
    const innerWidth = width - (2 * options.tableDepth * 2);
    const innerHeight = height - (2 * options.tableDepth * 2);

    if (innerWidth < options.minInnerClearance ||
        innerHeight < options.minInnerClearance) {
        return null;  // Doesn't meet minimum clearance
    }

    return { width, height, innerWidth, innerHeight };
}
```

### Rectangle Validation

Same as boardroom: Checks **5 points** for each rectangle candidate:

1. All 4 corners must be inside polygon
2. Midpoint of each edge must be inside polygon (catches edge crossings)

This ensures the rectangle is fully contained without intersecting polygon edges.

### Performance Optimizations

1. **Binary Search:** O(log n) convergence for size expansion
2. **Early Termination:** Stop if inner clearance constraint violated
3. **Symmetric Angles:** Only test 0-180° (rectangles symmetric at 180°)
4. **Grid Sampling:** Test sparse centroid grid instead of entire area

---

## Integration with Existing System

### JavaScript Loading (SvgViewer.js)

Add hollow square data loading alongside rectangle and boardroom loading:

```javascript
async loadPrecomputedHollowSquares() {
    if (this.precomputedHollowSquares) {
        return this.precomputedHollowSquares;
    }

    try {
        let data = null;

        // 1. Try to load from embedded script element
        const svgDoc = this.svg.node.ownerDocument;
        const scriptElement = svgDoc.getElementById('precomputed-hollowsquare');

        if (scriptElement) {
            const jsonText = scriptElement.textContent;
            data = JSON.parse(jsonText);
            console.log('[hollowsquare] Loaded from embedded SVG');
        } else {
            // 2. Fallback to external JSON file
            const response = await fetch('precomputed-hollowsquare.json');
            data = await response.json();
            console.log('[hollowsquare] Loaded from external JSON');
        }

        // 3. Build lookup map
        const lookup = new Map();
        for (const layout of data.hollowSquareLayouts) {
            lookup.set(layout.key, {
                hollowSquareLayout: layout.hollowSquareLayout,
                polygonArea: layout.polygonArea,
                hollowSquareArea: layout.hollowSquareArea,
                computationTimeMs: layout.computationTimeMs
            });
        }

        this.precomputedHollowSquares = {
            layouts: data.hollowSquareLayouts,
            lookup: lookup,
            metadata: {
                generatedAt: data.generatedAt,
                totalCombinations: data.totalCombinations,
                successfulComputations: data.successfulComputations
            }
        };

        return this.precomputedHollowSquares;
    } catch (error) {
        console.warn('[hollowsquare] Failed to load:', error.message);
        this.precomputedHollowSquares = {
            layouts: [],
            lookup: new Map(),
            metadata: {}
        };
        return this.precomputedHollowSquares;
    }
}

async lookupHollowSquareLayout(pathIds) {
    if (!pathIds || pathIds.length === 0) {
        return null;
    }

    await this.loadPrecomputedHollowSquares();

    // Create sorted key to match precomputed format
    const sortedKey = pathIds.slice().sort().join('_');

    const layoutData = this.precomputedHollowSquares.lookup.get(sortedKey);
    if (layoutData) {
        console.log(`[hollowsquare] ✓ Found layout: ${layoutData.hollowSquareLayout.width}×${layoutData.hollowSquareLayout.height}`);
        return layoutData.hollowSquareLayout;
    }

    console.debug(`[hollowsquare] ✗ No data for: ${sortedKey}`);
    return null;
}
```

### C# Interop (New AreaData Properties)

Extend `AreaData` class to include hollow square information:

```csharp
public class AreaData
{
    // Existing properties
    [JsonPropertyName("polygonArea")]
    public double? PolygonArea { get; set; }

    [JsonPropertyName("rectangleArea")]
    public double? RectangleArea { get; set; }

    [JsonPropertyName("boardroomArea")]
    public double? BoardroomArea { get; set; }

    [JsonPropertyName("boardroomSets")]
    public int? BoardroomSets { get; set; }

    [JsonPropertyName("boardroomTables")]
    public int? BoardroomTables { get; set; }

    // NEW: Hollow square properties
    [JsonPropertyName("hollowSquareArea")]
    public double? HollowSquareArea { get; set; }

    [JsonPropertyName("hollowSquareInnerArea")]
    public double? HollowSquareInnerArea { get; set; }

    [JsonPropertyName("hollowSquareOuterWidth")]
    public double? HollowSquareOuterWidth { get; set; }

    [JsonPropertyName("hollowSquareOuterHeight")]
    public double? HollowSquareOuterHeight { get; set; }

    [JsonPropertyName("computationTimeMs")]
    public double? ComputationTimeMs { get; set; }
}
```

### UI Display (SVGTestPage.razor)

Add hollow square display alongside rectangle and boardroom display:

```razor
@if (areaData != null && areaData.PolygonArea.HasValue)
{
    var comboArea = areaData.PolygonArea.Value;

    <div class="mt-2">
        <strong>Area Measurements (Square SVG Inches):</strong>
        <div class="ms-3">
            <div>Combo Area: <code>@($"{comboArea:F2}")</code> sq in</div>

            @if (areaData.RectangleArea.HasValue)
            {
                var rectArea = areaData.RectangleArea.Value;
                if (rectArea > comboArea) rectArea = comboArea;
                <div>Largest Rectangle: <code>@($"{rectArea:F2}")</code> sq in</div>
            }

            @if (areaData.BoardroomArea.HasValue && areaData.BoardroomSets.HasValue)
            {
                var boardroomArea = areaData.BoardroomArea.Value;
                if (boardroomArea > comboArea) boardroomArea = comboArea;
                <div>Boardroom Layout: <code>@($"{boardroomArea:F2}")</code> sq in
                     (@areaData.BoardroomSets sets, @areaData.BoardroomTables tables)</div>
            }

            @if (areaData.HollowSquareArea.HasValue)
            {
                var hsArea = areaData.HollowSquareArea.Value;
                if (hsArea > comboArea) hsArea = comboArea;
                <div>Hollow Square: <code>@($"{hsArea:F2}")</code> sq in
                     (@($"{areaData.HollowSquareOuterWidth:F1}")×@($"{areaData.HollowSquareOuterHeight:F1}") ft,
                      inner: <code>@($"{areaData.HollowSquareInnerArea:F2}")</code> sq in)</div>
            }
        </div>
    </div>
}
```

---

## Performance Considerations

### Build Time

| Stage | Duration |
|-------|----------|
| test-runner-hollowsquare.js | 15-20 minutes |
| extract-precomputed-hollowsquare.js | 30 seconds |
| embed-hollowsquare-in-svg.js | 5 seconds |
| **Total** | **~20 minutes** |

**Computation Statistics (Estimated):**

- Total combinations: 251
- Average computation time: ~145 ms per combo
- Total computation time: ~36 seconds
- But SVG I/O overhead adds significant time

### Runtime Performance

| Operation | Time |
|-----------|------|
| Load embedded hollow square data | <100 ms (one-time) |
| Lookup hollow square layout | <1 ms (Map.get) |
| Draw hollow square rectangle | <1 ms |
| **Total user experience** | **Instant** ✅ |

### Memory Usage

| Component | Size |
|-----------|------|
| precomputed-hollowsquare.json | ~150 KB |
| In-memory lookup Map | ~150 KB |
| Total hollow square overhead | **~300 KB** |

Combined with all layouts:

- Largest rectangles: ~300 KB
- Boardroom layouts: ~300 KB
- Hollow square layouts: ~300 KB
- **Total precomputed data: ~900 KB**

### File Size Impact

| File | Size |
|------|------|
| Original Level1.svg | ~225 KB |
| + rectangles data | ~237.5 KB (+12.5 KB) |
| + boardroom data | ~250 KB (+12.5 KB) |
| + hollow square data | ~262.5 KB (+12.5 KB) |
| **Total increase** | **+37.5 KB (17%)** |

---

## Best Practices

### 1. Run Pipelines Independently

- Don't need to regenerate all three every time
- Hollow square constraints are separate from other layouts
- Update only what changed

### 2. Validate Embedded Data

After embedding, verify the SVG contains all three datasets:

```bash
grep -c "precomputed-rectangles" Level1.svg   # Should be 1
grep -c "precomputed-boardroom" Level1.svg    # Should be 1
grep -c "precomputed-hollowsquare" Level1.svg # Should be 1
```

### 3. Keep Test Results

Don't delete `TestResults/HollowSquareResults/` after extraction:

- Useful for debugging
- Visual verification of layouts
- Can regenerate JSON if needed

### 4. Monitor Computation Times

Check `hollowsquare-summary.json` for outliers:

- Most combos should be <200 ms
- If any are >1000 ms, investigate polygon complexity
- Consider optimizing algorithm parameters

### 5. Version Control

Add to `.gitignore`:

```
LazyMagic.BlazorSvg/TestResults/HollowSquareResults/
LazyMagic.BlazorSvg/FloorMat/precomputed-hollowsquare.json
```

Commit to repository:

```
LazyMagic.BlazorSvg/wwwroot/SvgViewerHollowSquare.js
LazyMagic.BlazorSvg/FloorMat/test-runner-hollowsquare.js
LazyMagic.BlazorSvg/FloorMat/extract-precomputed-hollowsquare.js
LazyMagic.BlazorSvg/FloorMat/embed-hollowsquare-in-svg.js
BlazorTest.WASM/wwwroot/Level1.svg  (with embedded data)
```

---

## Troubleshooting

### No hollow square layouts found

**Symptom:** Many/all combinations return null layouts

**Possible Causes:**

1. Polygon too small for minimum 14×14 ft layout
2. Inner clearance constraint too strict (< 4×4 ft available)
3. Algorithm parameters too restrictive
4. Incorrect coordinate scaling

**Solutions:**

- Check polygon areas in `hollowsquare-summary.json`
- Increase `angleSamples` and `centroidSamples` in algorithm options
- Reduce `minInnerClearance` if appropriate
- Verify dimensions are in correct units

### Computation takes too long

**Symptom:** Test runner takes >30 minutes

**Possible Causes:**

1. Too many angle/centroid samples
2. Binary search not converging efficiently
3. Complex polygons

**Solutions:**

- Reduce `angleSamples` (try 18 instead of 36)
- Reduce `centroidSamples` (try 4 instead of 9)
- Adjust binary search precision tolerance

### Hollow square area exceeds polygon area

**Symptom:** `hollowSquareArea > polygonArea` in output

**Cause:** Algorithm numerical precision or incorrect area calculation

**Solution:**

- Cap hollow square area to polygon area in display logic
- Verify area calculation units are consistent
- Check scale factor application

### Embedded data not loading

**Symptom:** JavaScript can't find hollow square data

**Checklist:**

1. Verify `<script id="precomputed-hollowsquare">` exists in SVG
2. Check browser console for JSON parse errors
3. Ensure `loadPrecomputedHollowSquares()` is called before lookup
4. Hard refresh browser (Ctrl+Shift+R) to clear cache

---

## Future Enhancements

### 1. Variable Table Depths

Support different table sizes:

```javascript
{
  "standard": { tableDepth: 2.5, minInner: 4 },
  "compact": { tableDepth: 2.0, minInner: 3 },
  "spacious": { tableDepth: 3.0, minInner: 6 }
}
```

### 2. Non-Square Layouts

Extend to rectangular hollow layouts:

- Different widths and heights
- Asymmetric table arrangements
- L-shaped or U-shaped variants

### 3. Multiple Inner Clearances

Precompute layouts with different center space requirements:

- Small center (4×4 ft)
- Medium center (8×8 ft)
- Large center (12×12 ft)

### 4. Mixed Layouts

Combine hollow square with other constraints:

- Hollow square + stage area
- Hollow square + head table
- Hollow square + specific aspect ratio

---

## Conclusion

The hollow square layout pipeline provides a **parallel, independent system** for finding hollow perimeter table arrangements in floor plan combinations. Key achievements:

✅ **Independent Operation:** Runs separately from other pipelines
✅ **Same Architecture:** Follows proven precomputation patterns
✅ **Efficient Runtime:** Instant lookups, no computation at runtime
✅ **Embedded Data:** Single SVG file contains all three datasets
✅ **Scalable Design:** Easy to add more constraint types

The system demonstrates how to extend the precomputation architecture with **additional layout constraints** while maintaining **clean separation** and **independent operation** of each pipeline.

---

## Quick Reference

### Commands (New Multi-Project Pipeline)

```bash
# Navigate to FloorMat
cd FloorMat

# Process all projects in input/
npm run process

# That's it! The pipeline handles everything automatically.
```

### File Locations

- **Algorithm**: `wwwroot/SvgViewerHollowSquare.js`
- **Orchestrator**: `FloorMat/process-all.js`
- **Project Extractor**: `FloorMat/extract-precomputed-project.js`
- **Project Embedder**: `FloorMat/embed-project.js`
- **Input Files**: `FloorMat/input/[ProjectName].svg` and `FloorMat/input/[ProjectName]-combinations.json`
- **Output Directory**: `FloorMat/output/[ProjectName]-output/`
  - Embedded SVG: `[ProjectName].svg`
  - Hollow Square JSON: `[ProjectName]-hollowsquare.json`
  - Test Results: `TestResults/[ProjectName]-HollowSquareResults/*.svg`

### Key Parameters

- Minimum outer size: **14 ft × 14 ft**
- Table depth: **2.5 ft** (5 ft total on each side)
- Minimum inner clearance: **4 ft × 4 ft**
- Angle samples: **36** (every 5°)
- Centroid grid: **3×3 = 9 points**
