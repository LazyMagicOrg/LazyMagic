# Boardroom Layout Pipeline Documentation

## Overview

This document describes the parallel precomputation system for **boardroom-style inscribed rectangles** in SVG floor plan combinations. This system runs alongside the existing largest-rectangle and hollow square systems and follows the same architectural patterns.

**Related Documentation:**
- [InscribedRectangle-Guide.md](./InscribedRectangle-Guide.md) - Complete system architecture
- [MaxInscribedLayoutPipeline.md](./MaxInscribedLayoutPipeline.md) - Max-inscribed rectangle system
- [HollowSquareLayoutPipeline.md](./HollowSquareLayoutPipeline.md) - Hollow square layout system

**Created:** 2025-10-20
**Updated:** 2025-10-25

---

## Table of Contents

1. [Boardroom Layout Constraints](#boardroom-layout-constraints)
2. [Architecture Overview](#architecture-overview)
3. [Pipeline Components](#pipeline-components)
4. [Running the Pipeline](#running-the-pipeline)
5. [Data Structures](#data-structures)
6. [Algorithm Details](#algorithm-details)
7. [Integration with Existing System](#integration-with-existing-system)
8. [Performance Considerations](#performance-considerations)

---

## Boardroom Layout Constraints

### What is a Boardroom Layout?

A boardroom layout consists of tables arranged back-to-back in a conference room setup:

```
┌─────────────────────┐
│                     │  ← 4 ft spacing
├─────────────────────┤
│     Tables (2.5')   │  ← Tables back-to-back
├─────────────────────┤
│                     │  ← 4 ft spacing
└─────────────────────┘
```

### Fixed Constraints

- **Fixed Width:** 13 ft
  - 2.5 ft (table depth) × 2 (back-to-back) = 5 ft
  - 4 ft spacing on each side = 8 ft
  - **Total: 13 ft** (never changes)

- **Variable Length:** Increases in 6 ft increments
  - **Minimum:** 14 ft (1 set = 2 tables)
    - 6 ft (table width) + 4 ft spacing on each side = 14 ft
  - **Additional sets:** Add 6 ft per set
    - 2 sets (4 tables): 20 ft
    - 3 sets (6 tables): 26 ft
    - 4 sets (8 tables): 32 ft
    - n sets (2n tables): 14 + (n-1) × 6 ft

- **Rotation:** Can be oriented at any angle within the polygon

### Key Terminology

- **Set:** One unit of boardroom layout = 2 tables back-to-back
- **Tables:** Total number of individual tables = sets × 2
- **Layout:** The complete boardroom rectangle with fixed width and variable length

---

## Architecture Overview

The boardroom pipeline is a **parallel system** that runs alongside the largest-rectangle and hollow square infrastructures:

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
- [HollowSquareLayoutPipeline.md](./HollowSquareLayoutPipeline.md) for the hollow square system

---

## Pipeline Components

### 1. Boardroom Algorithm (`SvgViewerBoardroom.js`)

**Location:** `LazyMagic.BlazorSvg/wwwroot/SvgViewerBoardroom.js`

**Purpose:** Core algorithm for finding the largest boardroom layout that fits in a polygon.

**Key Functions:**

```javascript
findBoardroomLayout(polygon, options)
// Returns: { corners, width, height, area, angle, centroid, sets, tables, type }

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
   - Start with minimum length (14 ft = 1 set)
   - Increment length by 6 ft (add another set)
   - Test both orientations (13×length and length×13)
   - Continue until layout no longer fits
4. Return the layout with maximum area (most sets)

**Options:**

```javascript
{
  boardroomWidth: 13,         // Fixed width in feet
  minLength: 14,              // Minimum length (1 set)
  lengthIncrement: 6,         // Length increment per set
  angleSamples: 36,           // Number of angles to test
  centroidSamples: 9,         // Grid density (3×3)
  debugMode: false            // Enable detailed logging
}
```

### 2. Test Runner (`test-runner-boardroom.js`)

**Location:** `LazyMagic.BlazorSvg/FloorMat/test-runner-boardroom.js`

**Purpose:** Generate boardroom layouts for all 251 valid combinations.

**Usage:**

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
node test-runner-boardroom.js
```

**Output:**

- Directory: `LazyMagic.BlazorSvg/TestResults/BoardroomResults/`
- Files: `Combo_0001.svg` through `Combo_0251.svg`
- Summary: `boardroom-summary.json`

**Each SVG contains:**

```xml
<!-- Area Data (Square SVG Inches) -->
<!-- polygonArea: 2822.8891 -->
<!-- boardroomArea: 338.0000 -->
<!-- sets: 2 -->
<!-- tables: 4 -->
<!-- computationTimeMs: 125.3 -->
```

**Process for each combination:**

1. Load SVG and extract path data
2. Parse paths to line segments
3. Merge coincident points
4. Find unified boundary polygon
5. Calculate polygon area
6. Run `findBoardroomLayout()` algorithm
7. Generate visualization SVG
8. Save to `TestResults/BoardroomResults/`

### 3. Data Extraction (`extract-precomputed-boardroom.js`)

**Location:** `LazyMagic.BlazorSvg/FloorMat/extract-precomputed-boardroom.js`

**Purpose:** Parse generated SVG files and extract boardroom data into JSON.

**Usage:**

```bash
node extract-precomputed-boardroom.js
```

**Output:** `precomputed-boardroom.json`

**Data Structure:**

```json
{
  "generatedAt": "2025-10-20T12:00:00.000Z",
  "totalCombinations": 251,
  "successfulComputations": 251,
  "failedComputations": 0,
  "totalComputationTimeMs": 31450,
  "averageComputationTimeMs": 125.3,
  "boardroomLayouts": [
    {
      "key": "Ballroom_Room_1",
      "sections": ["Ballroom_Room_1"],
      "boardroomLayout": {
        "corners": [
          {"x": 285.5, "y": 45.2},
          {"x": 298.5, "y": 45.2},
          {"x": 298.5, "y": 115.2},
          {"x": 285.5, "y": 115.2}
        ],
        "width": 13,
        "height": 70,
        "area": 910,
        "angle": 0,
        "centroid": {"x": 292.0, "y": 80.2},
        "sets": 10,
        "tables": 20,
        "type": "boardroom"
      },
      "polygonArea": 2822.8891,
      "boardroomArea": 230.5234,
      "computationTimeMs": 125.3
    }
    // ... 250 more entries
  ]
}
```

**Extraction Process:**

1. Load `valid-combinations.json` (251 combos)
2. For each combination:
   - Read `TestResults/BoardroomResults/Combo_XXXX.svg`
   - Extract area data from XML comments
   - Parse boardroom rectangle path
   - Calculate dimensions and properties
   - Build layout object
3. Write `precomputed-boardroom.json`

### 4. SVG Embedding (`embed-boardroom-in-svg.js`)

**Location:** `LazyMagic.BlazorSvg/FloorMat/embed-boardroom-in-svg.js`

**Purpose:** Embed boardroom data directly into Level1.svg.

**Usage:**

```bash
node embed-boardroom-in-svg.js
```

**Embedded Structure:**

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="...">
  <defs>
    <!-- Existing largest rectangles data -->
    <script type="application/json" id="precomputed-rectangles"><![CDATA[
      {...}
    ]]></script>

    <!-- Boardroom layouts data -->
    <script type="application/json" id="precomputed-boardroom"><![CDATA[
      {"generatedAt":"2025-10-20T12:00:00.000Z","totalCombinations":251,...}
    ]]></script>

    <!-- Hollow square layouts data -->
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
- With hollow square data: ~262.5 KB (+12.5 KB)
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

**Total Time:** ~20 minutes per project (same as before)

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

### Boardroom Layout Object

```javascript
{
  corners: [
    {x: 285.5, y: 45.2},  // Bottom-left
    {x: 298.5, y: 45.2},  // Bottom-right
    {x: 298.5, y: 115.2}, // Top-right
    {x: 285.5, y: 115.2}  // Top-left
  ],
  width: 13,              // Always 13 ft
  height: 70,             // 14 + (n-1) × 6 ft
  area: 910,              // width × height (pre-transform)
  angle: 45.0,            // Rotation in degrees (0-180°)
  centroid: {x: 292.0, y: 80.2},
  sets: 10,               // Number of boardroom sets
  tables: 20,             // Number of individual tables (sets × 2)
  type: 'boardroom'       // Type identifier
}
```

### Comparison: Boardroom vs Other Layouts

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

The boardroom algorithm uses a **brute-force grid search** with **incremental sizing**:

1. **Angle Iteration:** Test 36 angles (every 5°) from 0-180°
2. **Centroid Grid:** For each angle, test 9 centroids in a 3×3 grid
3. **Size Increment:** For each angle/centroid:
   - Start with 1 set (14 ft)
   - Test if rectangle fits in polygon
   - If fits, try next set size (add 6 ft)
   - Continue until doesn't fit
   - Keep track of largest valid size
4. **Orientation:** Test both 13×L and L×13 orientations

**Total Tests per Combo:**

- 36 angles × 9 centroids × ~10 average sizes × 2 orientations
- ≈ **6,480 rectangle fit tests** per combination
- Fast due to simple point-in-polygon checks

### Point-in-Polygon Test

Uses **winding number algorithm** for robust containment checking:

```javascript
function isPointInPolygon(point, polygon) {
    let winding = 0;

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        if (p1.y <= point.y) {
            if (p2.y > point.y) {
                const cross = (p2.x - p1.x) * (point.y - p1.y)
                            - (point.x - p1.x) * (p2.y - p1.y);
                if (cross > 0) winding++;
            }
        } else {
            if (p2.y <= point.y) {
                const cross = (p2.x - p1.x) * (point.y - p1.y)
                            - (point.x - p1.x) * (p2.y - p1.y);
                if (cross < 0) winding--;
            }
        }
    }

    return winding !== 0;
}
```

### Rectangle Validation

Checks **5 points** for each rectangle candidate:

1. All 4 corners must be inside polygon
2. Midpoint of each edge must be inside polygon (catches edge crossings)

This ensures the rectangle is fully contained without intersecting polygon edges.

### Performance Optimizations

1. **Early Termination:** Stop incrementing size when first failure occurs
2. **Symmetric Angles:** Only test 0-180° (rectangles symmetric at 180°)
3. **Grid Sampling:** Test sparse centroid grid instead of entire area
4. **Safety Limit:** Cap at 50 sets to prevent infinite loops

---

## Integration with Existing System

### JavaScript Loading (SvgViewer.js)

Add boardroom data loading alongside rectangle loading:

```javascript
async loadPrecomputedBoardroom() {
    if (this.precomputedBoardroom) {
        return this.precomputedBoardroom;
    }

    try {
        let data = null;

        // 1. Try to load from embedded script element
        const svgDoc = this.svg.node.ownerDocument;
        const scriptElement = svgDoc.getElementById('precomputed-boardroom');

        if (scriptElement) {
            const jsonText = scriptElement.textContent;
            data = JSON.parse(jsonText);
            console.log('[boardroom] Loaded from embedded SVG');
        } else {
            // 2. Fallback to external JSON file
            const response = await fetch('precomputed-boardroom.json');
            data = await response.json();
            console.log('[boardroom] Loaded from external JSON');
        }

        // 3. Build lookup map
        const lookup = new Map();
        for (const layout of data.boardroomLayouts) {
            lookup.set(layout.key, {
                boardroomLayout: layout.boardroomLayout,
                polygonArea: layout.polygonArea,
                boardroomArea: layout.boardroomArea,
                computationTimeMs: layout.computationTimeMs
            });
        }

        this.precomputedBoardroom = {
            layouts: data.boardroomLayouts,
            lookup: lookup,
            metadata: {
                generatedAt: data.generatedAt,
                totalCombinations: data.totalCombinations,
                successfulComputations: data.successfulComputations
            }
        };

        return this.precomputedBoardroom;
    } catch (error) {
        console.warn('[boardroom] Failed to load:', error.message);
        this.precomputedBoardroom = {
            layouts: [],
            lookup: new Map(),
            metadata: {}
        };
        return this.precomputedBoardroom;
    }
}

async lookupBoardroomLayout(pathIds) {
    if (!pathIds || pathIds.length === 0) {
        return null;
    }

    await this.loadPrecomputedBoardroom();

    // Create sorted key to match precomputed format
    const sortedKey = pathIds.slice().sort().join('_');

    const layoutData = this.precomputedBoardroom.lookup.get(sortedKey);
    if (layoutData) {
        console.log(`[boardroom] ✓ Found layout: ${layoutData.boardroomLayout.sets} sets`);
        return layoutData.boardroomLayout;
    }

    console.debug(`[boardroom] ✗ No data for: ${sortedKey}`);
    return null;
}
```

### C# Interop (New AreaData Properties)

Extend `AreaData` class to include boardroom information:

```csharp
public class AreaData
{
    // Existing properties
    [JsonPropertyName("polygonArea")]
    public double? PolygonArea { get; set; }

    [JsonPropertyName("rectangleArea")]
    public double? RectangleArea { get; set; }

    [JsonPropertyName("computationTimeMs")]
    public double? ComputationTimeMs { get; set; }

    // NEW: Boardroom properties
    [JsonPropertyName("boardroomArea")]
    public double? BoardroomArea { get; set; }

    [JsonPropertyName("boardroomSets")]
    public int? BoardroomSets { get; set; }

    [JsonPropertyName("boardroomTables")]
    public int? BoardroomTables { get; set; }
}
```

### UI Display (SVGTestPage.razor)

Add boardroom display alongside rectangle display:

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
        </div>
    </div>
}
```

---

## Performance Considerations

### Build Time

| Stage | Duration |
|-------|----------|
| test-runner-boardroom.js | 15-20 minutes |
| extract-precomputed-boardroom.js | 30 seconds |
| embed-boardroom-in-svg.js | 5 seconds |
| **Total** | **~20 minutes** |

**Computation Statistics (Estimated):**

- Total combinations: 251
- Average computation time: ~125 ms per combo
- Total computation time: ~31 seconds
- But SVG I/O overhead adds significant time

### Runtime Performance

| Operation | Time |
|-----------|------|
| Load embedded boardroom data | <100 ms (one-time) |
| Lookup boardroom layout | <1 ms (Map.get) |
| Draw boardroom rectangle | <1 ms |
| **Total user experience** | **Instant** ✅ |

### Memory Usage

| Component | Size |
|-----------|------|
| precomputed-boardroom.json | ~150 KB |
| In-memory lookup Map | ~150 KB |
| Total boardroom overhead | **~300 KB** |

Combined with largest rectangles:

- Largest rectangles: ~300 KB
- Boardroom layouts: ~300 KB
- **Total precomputed data: ~600 KB**

### File Size Impact

| File | Size |
|------|------|
| Original Level1.svg | ~225 KB |
| + rectangles data | ~237.5 KB (+12.5 KB) |
| + boardroom data | ~250 KB (+12.5 KB) |
| **Total increase** | **+25 KB (11%)** |

---

## Best Practices

### 1. Run Pipelines Independently

- Don't need to regenerate both every time
- Boardroom constraints are separate from largest rectangle
- Update only what changed

### 2. Validate Embedded Data

After embedding, verify the SVG contains both datasets:

```bash
grep -c "precomputed-rectangles" Level1.svg  # Should be 1
grep -c "precomputed-boardroom" Level1.svg   # Should be 1
```

### 3. Keep Test Results

Don't delete `TestResults/BoardroomResults/` after extraction:

- Useful for debugging
- Visual verification of layouts
- Can regenerate JSON if needed

### 4. Monitor Computation Times

Check `boardroom-summary.json` for outliers:

- Most combos should be <200 ms
- If any are >1000 ms, investigate polygon complexity
- Consider optimizing algorithm parameters

### 5. Version Control

Add to `.gitignore`:

```
LazyMagic.BlazorSvg/TestResults/BoardroomResults/
LazyMagic.BlazorSvg/FloorMat/precomputed-boardroom.json
```

Commit to repository:

```
LazyMagic.BlazorSvg/wwwroot/SvgViewerBoardroom.js
LazyMagic.BlazorSvg/FloorMat/test-runner-boardroom.js
LazyMagic.BlazorSvg/FloorMat/extract-precomputed-boardroom.js
LazyMagic.BlazorSvg/FloorMat/embed-boardroom-in-svg.js
BlazorTest.WASM/wwwroot/Level1.svg  (with embedded data)
```

---

## Troubleshooting

### No boardroom layouts found

**Symptom:** Many/all combinations return null layouts

**Possible Causes:**

1. Polygon too small for minimum 13×14 ft layout
2. Algorithm parameters too restrictive
3. Incorrect coordinate scaling

**Solutions:**

- Check polygon areas in `boardroom-summary.json`
- Increase `angleSamples` and `centroidSamples` in algorithm options
- Verify `boardroomWidth` and `minLength` are in correct units

### Computation takes too long

**Symptom:** Test runner takes >30 minutes

**Possible Causes:**

1. Too many angle/centroid samples
2. Large number of sets being tested
3. Complex polygons

**Solutions:**

- Reduce `angleSamples` (try 18 instead of 36)
- Reduce `centroidSamples` (try 4 instead of 9)
- Lower safety limit from 50 sets to 20

### Boardroom area exceeds polygon area

**Symptom:** `boardroomArea > polygonArea` in output

**Cause:** Algorithm numerical precision or incorrect area calculation

**Solution:**

- Cap boardroom area to polygon area in display logic (like rectangles)
- Verify area calculation units are consistent

### Embedded data not loading

**Symptom:** JavaScript can't find boardroom data

**Checklist:**

1. Verify `<script id="precomputed-boardroom">` exists in SVG
2. Check browser console for JSON parse errors
3. Ensure `loadPrecomputedBoardroom()` is called before lookup
4. Hard refresh browser (Ctrl+Shift+R) to clear cache

---

## Future Enhancements

### 1. Multiple Boardroom Configurations

Support different table sizes:

```javascript
{
  "standard": { width: 13, minLength: 14, increment: 6 },
  "narrow": { width: 10, minLength: 12, increment: 5 },
  "wide": { width: 16, minLength: 16, increment: 8 }
}
```

### 2. U-Shaped and Hollow-Square Layouts

Extend algorithm to support:

- U-shaped: 3 sides of tables
- Hollow square: 4 sides with center space

### 3. Mixed Layouts

Combine boardroom with other constraints:

- Boardroom + minimum aisle width
- Boardroom + stage area
- Boardroom + specific aspect ratio

### 4. Incremental Updates

Only recompute changed combinations:

```javascript
// Compare old and new valid-combinations.json
// Recompute only differences
// Merge into existing precomputed-boardroom.json
```

### 5. Compression

Reduce file size with compression:

```javascript
const compressed = pako.gzip(JSON.stringify(data));
const base64 = btoa(String.fromCharCode(...compressed));
// Decompress at runtime
```

Could reduce boardroom data from ~150 KB to ~40 KB.

---

## Conclusion

The boardroom layout pipeline provides a **parallel, independent system** for finding constrained inscribed rectangles in floor plan combinations. Key achievements:

✅ **Independent Operation:** Runs separately from largest-rectangle pipeline
✅ **Same Architecture:** Follows proven precomputation patterns
✅ **Efficient Runtime:** Instant lookups, no computation at runtime
✅ **Embedded Data:** Single SVG file contains both datasets
✅ **Scalable Design:** Easy to add more constraint types

The system demonstrates how to extend the precomputation architecture with **additional constraints** while maintaining **clean separation** and **independent operation** of each pipeline.

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

- **Algorithm**: `wwwroot/SvgViewerBoardroom.js`
- **Orchestrator**: `FloorMat/process-all.js`
- **Project Extractor**: `FloorMat/extract-precomputed-project.js`
- **Project Embedder**: `FloorMat/embed-project.js`
- **Input Files**: `FloorMat/input/[ProjectName].svg` and `FloorMat/input/[ProjectName]-combinations.json`
- **Output Directory**: `FloorMat/output/[ProjectName]-output/`
  - Embedded SVG: `[ProjectName].svg`
  - Boardroom JSON: `[ProjectName]-boardroom.json`
  - Test Results: `TestResults/[ProjectName]-BoardroomResults/*.svg`

### Key Parameters

- Fixed width: **13 ft**
- Min length: **14 ft** (1 set)
- Length increment: **6 ft** per set
- Angle samples: **36** (every 5°)
- Centroid grid: **3×3 = 9 points**
- Tables per set: **2**
