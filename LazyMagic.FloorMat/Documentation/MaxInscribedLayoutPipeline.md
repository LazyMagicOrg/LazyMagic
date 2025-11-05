# Max-Inscribed Rectangle Layout Pipeline Documentation

## Overview

This document describes the parallel precomputation system for **maximum inscribed rectangles** in SVG floor plan combinations. This system finds the largest rectangle (by area) that fits within any polygon, without constraints on dimensions. It runs alongside the boardroom and hollow square systems and follows the same architectural patterns.

**Related Documentation:**
- [InscribedRectangle-Guide.md](./InscribedRectangle-Guide.md) - Complete system architecture
- [BoardroomLayoutPipeline.md](./BoardroomLayoutPipeline.md) - Boardroom layout system
- [HollowSquareLayoutPipeline.md](./HollowSquareLayoutPipeline.md) - Hollow square layout system

**Created:** 2025-10-25
**Updated:** 2025-11-05 (Updated for unified pipeline)

---

## Table of Contents

1. [Max-Inscribed Rectangle Constraints](#max-inscribed-rectangle-constraints)
2. [Architecture Overview](#architecture-overview)
3. [Pipeline Components](#pipeline-components)
4. [Running the Pipeline](#running-the-pipeline)
5. [Data Structures](#data-structures)
6. [Algorithm Details](#algorithm-details)
7. [Integration with Existing System](#integration-with-existing-system)
8. [Performance Considerations](#performance-considerations)

---

## Max-Inscribed Rectangle Constraints

### What is a Max-Inscribed Rectangle?

A max-inscribed rectangle is the largest rectangle (by area) that can fit completely inside a polygon, at any rotation angle:

```
┌─────────────────────────────────────┐
│                                     │
│    ╔═══════════════════════════╗   │
│    ║                           ║   │
│    ║   Max-Inscribed Rectangle ║   │
│    ║   (largest possible area) ║   │
│    ║                           ║   │
│    ╚═══════════════════════════╝   │
│                                     │
└─────────────────────────────────────┘
```

### Constraints

**Goal:** Maximize rectangle area with NO dimension constraints

- **Width:** Variable (no limits)
- **Height:** Variable (no limits)
- **Aspect Ratio:** Variable (optimized for area, not shape)
- **Rotation:** 0-180° tested
- **Constraint:** Must fit entirely within the polygon boundary

### Key Differences from Other Layouts

| Aspect | Max-Inscribed | Boardroom | Hollow Square |
|--------|---------------|-----------|---------------|
| **Primary Goal** | Maximize area | Maximize table sets | Maximize outer area with clearance |
| **Constraints** | None (pure optimization) | Fixed width (13 ft) | Minimum inner clearance (4 ft) |
| **Use Case** | General space utilization | Conference tables | Perimeter seating |

### Key Terminology

- **Inscribed Rectangle:** A rectangle completely contained within the polygon
- **Max-Inscribed:** The inscribed rectangle with the largest area
- **Hybrid Algorithm:** Combination of fast and accurate algorithms for best results
- **Precomputed:** Results calculated at build-time and embedded in SVG

---

## Architecture Overview

The max-inscribed pipeline is a **parallel system** that runs alongside the boardroom and hollow square infrastructures:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           PARALLEL SYSTEMS                                │
├──────────────────────┬──────────────────────┬─────────────────────────────┤
│  Max-Inscribed       │  Boardroom Layouts   │  Hollow Square Layouts      │
│  Rectangles          │                      │                             │
├──────────────────────┼──────────────────────┼─────────────────────────────┤
│ run-tests.js       │ test-runner-         │ test-runner-hollowsquare.js │
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
- [BoardroomLayoutPipeline.md](./BoardroomLayoutPipeline.md) for the boardroom system
- [HollowSquareLayoutPipeline.md](./HollowSquareLayoutPipeline.md) for the hollow square system

---

## Pipeline Components

### 1. Max-Inscribed Algorithms

**Location:** Multiple JavaScript files in `LazyMagic.FloorMat/FloorMat/`

**Algorithm Files:**

1. **SvgViewerBoundaryBased.cjs** - Fast boundary-based algorithm + hybrid
2. **SvgViewerOptimized.cjs** - Grid-based centroid sampling with binary search
3. **SvgViewerInscribedRect.cjs** - Unified inscribed rectangle algorithm

**Key Functions:**

```javascript
// Hybrid algorithm (recommended)
hybridInscribedRectangle(polygon, options)
// Returns: { corners, width, height, area, angle, centroid, type }

// Fast algorithm
boundaryBasedInscribedRectangle(polygon, options)
// Returns: Rectangle optimized for simple shapes

// Accurate algorithm
optimizedInscribedRectangle(polygon, options)
// Returns: Rectangle with exhaustive search
```

### Algorithm Selection Strategy

The **hybrid algorithm** automatically selects the best approach:

```javascript
1. Try boundary-based first (fast, 100-200ms)
2. Check if result meets quality threshold
3. If not, try optimized algorithm (slow, 1-3 seconds)
4. Compare results and return the better one
```

**Boundary-Based Algorithm:**
- **Best for:** Simple shapes (rectangles, L-shapes, T-shapes)
- **Strategy:**
  - Extract polygon boundary
  - Analyze edge directions
  - Test candidate angles based on dominant edges
  - Grid search with centroid sampling
- **Performance:** 100-200ms
- **Accuracy:** Good for shapes with clear edge structure

**Optimized Algorithm:**
- **Best for:** Complex concave shapes with many vertices
- **Strategy:**
  - Dense angle sampling (36 angles)
  - Adaptive centroid search with spatial hash grid
  - Multi-pass expansion with binary search
  - Edge-aware expansion respecting boundaries
- **Performance:** 1-3 seconds
- **Accuracy:** Excellent for complex geometries

**Hybrid Selection Logic:**
```javascript
const improvementPercent = (optimizedArea - boundaryArea) / boundaryArea * 100;

if (improvementPercent >= 5%) {
    return optimizedResult;  // Significantly better
} else {
    return boundaryResult;   // Good enough, save time
}
```

### 2. Unified Test Runner (`run-tests.js`)

**Location:** `LazyMagic.FloorMat/FloorMat/run-tests.js` (1,275 lines)

**Purpose:** Execute max-inscribed rectangle tests for all valid combinations using Playwright headless browser.

**Usage:**

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.FloorMat\FloorMat"
# Max-inscribed tests are run automatically via process-all.js
# Or run manually with a max-inscribed test config:
node run-tests.js test-configs/maxinscribed-full.json
```

**Key Responsibilities:**
- Loads CommonJS algorithm modules (SvgViewerInscribedRect.cjs) via `createRequire()`
- Parses SVG files using JSDOM, handles transforms
- Merges multi-path polygons into unified coordinate space
- Launches Playwright for each test case
- Executes hybrid algorithm (boundary-based + optimized)
- Generates JSON and SVG output files

**Output:**

- Directory: `output/[Project]-output/ComputedLayouts/[Project]-MaxInscribedResults/`
- Files: `MaxInscribed_[PathId].svg` and `MaxInscribed_[PathId].json` for each combination
- Files: `Combo_0001.svg` through `Combo_0251.svg`
- Summary: `results.txt` (line-delimited JSON)

**Each SVG contains:**

```xml
<!-- Area Data (Square SVG Inches) -->
<!-- polygonArea: 2822.8891 -->
<!-- rectangleArea: 2830.9513 -->
<!-- computationTimeMs: 125.3 -->
<!-- algorithmType: boundary-based -->
```

**Process for each combination:**

1. Load SVG and extract path data
2. Parse paths to line segments
3. Merge coincident points
4. Find unified boundary polygon
5. Calculate polygon area
6. Run hybrid algorithm (boundary-based → optimized if needed)
7. Calculate rectangle area
8. Generate visualization SVG
9. Save to `TestResults/MaxInscribedResults/`

**Console Output:**
```
Running 251 tests...
✓ Combo 0001 (Ballroom_Room_1): 116.5×95.8 at 90° = 11162 sq px in 125ms (boundary-based)
✓ Combo 0002 (Ballroom_Room_2): 98.2×87.3 at 0° = 8573 sq px in 142ms (boundary-based)
✓ Combo 0045 (Room_3+Room_5+Aisles): 217.5×181.2 at 96° = 39413 sq px in 2415ms (optimized)
...
251 passed (15.2m)
```

### 3. Data Extraction (`extract-precomputed-project.js`)

**Location:** `LazyMagic.FloorMat/FloorMat/extract-precomputed-project.js` (195 lines)

**Purpose:** Parse generated JSON files and extract max-inscribed rectangle data into consolidated precomputed format.

**Usage:**

```bash
# Automatically called by process-all.js
# Or run manually:
node extract-precomputed-project.js "Level1" "output/Level1-output/ComputedLayouts" "output/Level1-output/Level1-valid-combinations.json" "output/Level1-output"
```

**Output:** `output/[Project]-output/[Project]-rectangles.json`

**Data Structure:**

```json
{
  "generatedAt": "2025-10-25T12:00:00.000Z",
  "totalCombinations": 251,
  "successfulComputations": 251,
  "failedComputations": 0,
  "totalComputationTimeMs": 460578,
  "averageComputationTimeMs": 1842.3,
  "statistics": {
    "boundaryBasedCount": 38,
    "optimizedCount": 213
  },
  "rectangles": [
    {
      "key": "Ballroom_Room_1",
      "sections": ["Ballroom_Room_1"],
      "rectangle": {
        "corners": [
          {"x": 234.48, "y": 32.26},
          {"x": 350.97, "y": 32.26},
          {"x": 350.97, "y": 128.08},
          {"x": 234.48, "y": 128.08}
        ],
        "width": 116.5,
        "height": 95.8,
        "area": 11162.4,
        "angle": 90.0,
        "centroid": {"x": 292.73, "y": 80.17},
        "type": "boundary-based"
      },
      "polygonArea": 2822.8891,
      "rectangleArea": 2830.9513,
      "computationTimeMs": 125.3
    }
    // ... 250 more entries
  ]
}
```

**Extraction Process:**

1. Load `valid-data.json` (251 combos)
2. For each combination:
   - Read `TestResults/MaxInscribedResults/Combo_XXXX.svg`
   - Extract area data from XML comments
   - Parse rectangle polygon element
   - Extract algorithm type from legend text
   - Calculate dimensions from corners
   - Build rectangle object
3. Calculate statistics (boundary vs optimized count)
4. Write `precomputed-rectangles.json`

**Algorithm Type Distribution:**
- Boundary-based: ~15% (simple shapes)
- Optimized: ~85% (complex shapes where boundary-based was insufficient)

### 4. SVG Embedding (`embed-project.js`)

**Location:** `LazyMagic.FloorMat/FloorMat/embed-project.js` (170 lines)

**Purpose:** Embed all three layout datasets (MaxInscribed, Boardroom, Hollow Square) into project-specific output SVG.

**Usage:**

```bash
# Automatically called by process-all.js
# Or run manually:
node embed-project.js "Level1" "input/Level1.svg" "output/Level1-output"
```

**Output:** `output/[Project]-output/[Project]-output.svg`

**Embedded Structure:**

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="...">
  <defs>
    <!-- Max-inscribed rectangles data -->
    <script type="application/json" id="precomputed-rectangles"><![CDATA[
      {"generatedAt":"2025-10-25T12:00:00.000Z","totalCombinations":251,...}
    ]]></script>

    <!-- Boardroom layouts data -->
    <script type="application/json" id="precomputed-boardroom"><![CDATA[
      {...}
    ]]></script>

    <!-- Hollow square layouts data -->
    <script type="application/json" id="precomputed-hollowsquare"><![CDATA[
      {...}
    ]]></script>
  </defs>
  <!-- Rest of SVG content -->
</svg>
```

**File Size Impact:**

- Original Level1.svg: ~225 KB
- With max-inscribed data: ~237.5 KB (+12.5 KB)
- With all three systems: ~262.5 KB (+37.5 KB total)

---

## Running the Pipeline

### Complete Build Process (New Multi-Project Workflow)

FloorMat now supports processing multiple SVG projects automatically:

```bash
# 1. Navigate to FloorMat directory
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.FloorMat\FloorMat"

# 2. Place your SVG and combinations file in input/:
#    - input/YourProject.svg
#    - input/YourProject-data.json

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
│   ├── Level1-data.json
│   ├── Level2.svg                  # You can have multiple projects
│   └── Level2-data.json
├── output/                         # Auto-generated outputs
│   ├── Level1-output/
│   │   ├── Level1-output.svg       # ← FINAL FILE (SVG with embedded data)
│   │   ├── Level1-valid-combinations.json  # Generated (251 combos)
│   │   ├── Level1-rectangles.json
│   │   ├── Level1-boardroom.json
│   │   ├── Level1-hollowsquare.json
│   │   └── ComputedLayouts/
│   └── Level2-output/
│       └── ...
├── process-all.js                  # Main orchestrator (362 lines)
├── compute-all-combinations.js     # Combination generator (583 lines)
├── calculate-polygon-areas.js      # Area calculator (334 lines)
├── run-tests.js                    # Test runner with Playwright (1,275 lines)
├── extract-precomputed-project.js  # Project-aware extraction (195 lines)
├── embed-path-metadata.js          # Metadata embedder (198 lines)
├── embed-project.js                # Project-aware embedding (170 lines)
└── Algorithm modules (.cjs):       # CommonJS for Node.js
    ├── SvgViewerInscribedRect.cjs  # Unified API (629 lines)
    ├── SvgViewerBoundaryBased.cjs  # (2,406 lines)
    ├── SvgViewerOptimized.cjs      # (1,745 lines)
    └── ... (other algorithms)
```

### Single Command Workflow

The new pipeline consolidates everything into a single command:

```bash
npm run process
```

**What happens:**
1. Scans `input/` for all .svg files
2. For each SVG (e.g., `Level1.svg`):
   - Loads `Level1-data.json`
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

### Rectangle Layout Object

```javascript
{
  corners: [
    {x: 234.48, y: 32.26},  // Bottom-left
    {x: 350.97, y: 32.26},  // Bottom-right
    {x: 350.97, y: 128.08}, // Top-right
    {x: 234.48, y: 128.08}  // Top-left
  ],
  width: 116.5,            // Rectangle width (pre-transform)
  height: 95.8,            // Rectangle height (pre-transform)
  area: 11162.4,           // Rectangle area (pre-transform)
  angle: 90.0,             // Rotation in degrees (0-180°)
  centroid: {x: 292.73, y: 80.17},
  type: 'boundary-based'   // or 'optimized'
}
```

### Comparison: Max-Inscribed vs Other Layouts

| Property | Max-Inscribed Rectangle | Boardroom Layout | Hollow Square Layout |
|----------|------------------------|------------------|---------------------|
| **Width** | Variable | **13 ft (fixed)** | Variable (min 14 ft) |
| **Height** | Variable | **14, 20, 26... ft** | Variable (min 14 ft) |
| **Inner Space** | None | None | **4+ ft clearance** |
| **Goal** | Maximize area | Maximize table sets | Maximize outer area with inner clearance |
| **Rotation** | 0-180° | 0-180° | 0-180° |
| **Algorithm** | Boundary-based + Optimized (hybrid) | Grid search with incremental sizing | Binary search expansion |
| **Type** | 'boundary-based' or 'optimized' | 'boardroom' | 'hollowsquare' |
| **Extra Data** | None | `sets`, `tables` | `innerWidth`, `innerHeight`, `innerArea` |

---

## Algorithm Details

### Boundary-Based Algorithm

**Strategy:** Analyze polygon edge structure to find candidate angles

1. **Extract Boundary:** Merge all paths into unified polygon
2. **Analyze Edges:** Group edges by angle (±5° tolerance)
3. **Find Dominant Directions:** Identify most common edge angles
4. **Generate Candidates:** Test perpendicular pairs and individual edge angles
5. **Grid Search:** For each angle, test grid of centroids (326 points default)
6. **Expand Rectangles:** Binary search from each centroid until boundary hit
7. **Validate:** Ensure all corners inside polygon
8. **Return Best:** Rectangle with maximum area

**Performance Characteristics:**
- Simple shapes (4-6 vertices): 80-120ms
- Moderate shapes (7-12 vertices): 100-200ms
- Complex shapes (13+ vertices): 150-250ms (may be suboptimal)

**Strategy Selection:**
```javascript
if (pathCount === 1) {
    strategy = "SINGLE_PATH";  // Minimal angles, very fast
} else if (vertices <= 6) {
    strategy = "SIMPLE";       // Fewer angles, fast
} else {
    strategy = "HYBRID";       // Full angle search
}
```

**Console Output:**
```
[boundary-based] Strategy: HYBRID (7 paths, 13 vertices)
[boundary-based] Found 5 angle groups:
  1. 0.0° (4 edges, 436.9px total)
  2. 90.0° (4 edges, 322.7px total)
  3. 106.5° (2 edges, 167.8px total)
[boundary-based] Testing 10 candidate angles
[boundary-based] Best: 154.8×221.2 at 0° = 34248 sq px in 119.9ms
```

### Optimized Algorithm

**Strategy:** Exhaustive search with adaptive refinement

1. **Dense Angle Sampling:** Test 36 angles (every 5°) from 0-175°
2. **Spatial Hash Grid:** Build grid for fast point-in-polygon tests
3. **Adaptive Centroid Search:**
   - Generate uniform grid of candidates
   - Add geometric centroid
   - Add AABB center
   - Add pole of inaccessibility (polylabel)
4. **Multi-Pass Expansion:**
   - For each angle and centroid
   - Binary search to find maximum rectangle size
   - Test multiple aspect ratios
   - Track best result
5. **Validation:** Winding number algorithm for containment
6. **Return Best:** Rectangle with absolute maximum area

**Performance Characteristics:**
- Simple shapes: 800-1200ms (overkill, but accurate)
- Moderate shapes: 1500-2500ms (thorough search)
- Complex shapes: 2000-4000ms (necessary for accuracy)

**Console Output:**
```
[optimized] Testing 36 rotation angles
[optimized] Angle 0°: 1250 centroid candidates
[optimized] Angle 96°: Best so far = 217.5×181.2 = 39413 sq px
[optimized] Found optimal: 217.5×181.2 at 96° = 39413 sq px in 2415ms
```

### Hybrid Algorithm Decision Logic

```javascript
async hybridInscribedRectangle(polygon, options) {
    console.log('[hybrid] Step 1: Trying boundary-based...');
    const boundaryResult = await boundaryBasedInscribedRectangle(polygon, options);
    const boundaryArea = boundaryResult.area;

    // If no target or threshold > 0, try optimized too
    console.log('[hybrid] Step 2: Trying optimized...');
    const optimizedResult = await optimizedInscribedRectangle(polygon, options);
    const optimizedArea = optimizedResult.area;

    // Compare results
    const improvement = (optimizedArea - boundaryArea) / boundaryArea * 100;

    if (improvement >= 5%) {
        console.log(`[hybrid] Optimized is ${improvement.toFixed(1)}% better`);
        return optimizedResult;
    } else {
        console.log(`[hybrid] Boundary-based is sufficient (<5% improvement)`);
        return boundaryResult;
    }
}
```

**When Optimized is Used:**
- Improvement ≥ 5% → Use optimized result
- Complex concave shapes where boundary-based misses optimal angle
- Shapes with non-axis-aligned dominant directions

**When Boundary-Based is Used:**
- Improvement < 5% → Use faster result
- Simple rectangular or L-shaped polygons
- Shapes with clear axis-aligned edges

---

## Integration with Existing System

### JavaScript Loading (SvgViewer.js)

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
            const response = await fetch('precomputed-rectangles.json');
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
            metadata: {
                generatedAt: data.generatedAt,
                totalCombinations: data.totalCombinations,
                successfulComputations: data.successfulComputations,
                statistics: data.statistics
            }
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
        return rectData.rectangle;
    }

    console.debug(`[precomputed] ✗ No data for: ${sortedKey}`);
    return null;
}
```

### C# Interop (AreaData)

```csharp
public class AreaData
{
    [JsonPropertyName("polygonArea")]
    public double? PolygonArea { get; set; }

    [JsonPropertyName("rectangleArea")]
    public double? RectangleArea { get; set; }

    [JsonPropertyName("computationTimeMs")]
    public double? ComputationTimeMs { get; set; }

    // Boardroom properties
    [JsonPropertyName("boardroomArea")]
    public double? BoardroomArea { get; set; }

    [JsonPropertyName("boardroomSets")]
    public int? BoardroomSets { get; set; }

    [JsonPropertyName("boardroomTables")]
    public int? BoardroomTables { get; set; }

    // Hollow square properties
    [JsonPropertyName("hollowSquareArea")]
    public double? HollowSquareArea { get; set; }

    [JsonPropertyName("hollowSquareInnerArea")]
    public double? HollowSquareInnerArea { get; set; }

    [JsonPropertyName("hollowSquareOuterWidth")]
    public double? HollowSquareOuterWidth { get; set; }

    [JsonPropertyName("hollowSquareOuterHeight")]
    public double? HollowSquareOuterHeight { get; set; }
}
```

### UI Display (SVGTestPage.razor)

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
                <div>Max-Inscribed Rectangle: <code>@($"{rectArea:F2}")</code> sq in</div>
            }

            @if (areaData.BoardroomArea.HasValue)
            {
                // Boardroom display...
            }

            @if (areaData.HollowSquareArea.HasValue)
            {
                // Hollow square display...
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
| run-tests.js | 15-20 minutes |
| extract-precomputed-project.js | 30 seconds |
| embed-project.js | 5 seconds |
| **Total** | **~20 minutes** |

**Computation Statistics:**

- Total combinations: 251
- Average computation time: ~1,842 ms per combo
- Total computation time: ~7.7 minutes
- Boundary-based: ~38 combos (15%)
- Optimized: ~213 combos (85%)

**Time Distribution:**
- Simple shapes: 100-200ms (boundary-based sufficient)
- Complex shapes: 2000-3000ms (optimized required)
- SVG I/O overhead: Adds ~10 minutes to total time

### Runtime Performance

| Operation | Time |
|-----------|------|
| Load embedded rectangle data | <100 ms (one-time) |
| Lookup rectangle | <1 ms (Map.get) |
| Draw rectangle visualization | <1 ms |
| **Total user experience** | **Instant** ✅ |

### Memory Usage

| Component | Size |
|-----------|------|
| precomputed-rectangles.json | ~150 KB |
| In-memory lookup Map | ~150 KB |
| Total max-inscribed overhead | **~300 KB** |

Combined with all layouts:

- Max-inscribed rectangles: ~300 KB
- Boardroom layouts: ~300 KB
- Hollow square layouts: ~300 KB
- **Total precomputed data: ~900 KB**

### Algorithm Performance Comparison

| Combination Type | Boundary-Based | Optimized | Improvement | Selected |
|-----------------|----------------|-----------|-------------|----------|
| Single room (rectangular) | 125ms, 11,162 sq px | N/A | N/A | Boundary |
| Two rooms (L-shape) | 118ms, 19,385 sq px | N/A | N/A | Boundary |
| Complex (7 sections) | 120ms, 34,248 sq px | 2,415ms, 39,413 sq px | +15.1% | Optimized |
| All 15 sections | 187ms, 48,220 sq px | 3,842ms, 52,105 sq px | +8.1% | Optimized |

**Key Insight:** Optimized algorithm adds 20-40x computation time but improves area by 5-15% for complex shapes.

---

## Best Practices

### 1. Run Pipelines Independently

- Don't need to regenerate all three every time
- Max-inscribed is the most computationally expensive
- Update only what changed

### 2. Validate Embedded Data

After embedding, verify the SVG contains the data:

```bash
grep -c "precomputed-rectangles" Level1.svg   # Should be 1
```

### 3. Keep Test Results

Don't delete `TestResults/MaxInscribedResults/` after extraction:

- Useful for debugging algorithm results
- Visual verification of rectangles
- Can regenerate JSON if needed
- Good for comparing boundary-based vs optimized

### 4. Monitor Computation Times

Check `results.txt` for outliers:

- Most combos should be <2000 ms
- If any are >5000 ms, investigate polygon complexity
- Consider optimizing algorithm parameters

### 5. Algorithm Selection

The hybrid algorithm automatically selects the best approach, but you can force one:

```javascript
// Force boundary-based (faster)
const result = await boundaryBasedInscribedRectangle(polygon, options);

// Force optimized (more accurate)
const result = await optimizedInscribedRectangle(polygon, options);

// Automatic selection (recommended)
const result = await hybridInscribedRectangle(polygon, options);
```

---

## Troubleshooting

### No rectangles found

**Symptom:** Many/all combinations return null rectangles

**Possible Causes:**

1. Polygon too small or invalid
2. Algorithm parameters too restrictive
3. Incorrect coordinate scaling
4. Self-intersecting polygon

**Solutions:**

- Check polygon areas in test results
- Verify polygon validity (no self-intersections)
- Increase grid resolution in algorithm options
- Check scale factor application

### Computation takes too long

**Symptom:** Test runner takes >30 minutes

**Possible Causes:**

1. Too many optimized algorithm invocations
2. High angle/centroid sampling
3. Complex polygons with many vertices

**Solutions:**

- Use boundary-based only for simple shapes
- Reduce angle samples in optimized (try 18 instead of 36)
- Reduce centroid grid density
- Profile specific slow combinations

### Rectangle area exceeds polygon area

**Symptom:** `rectangleArea > polygonArea` in output

**Cause:** Numerical precision in area calculation or polygon validation

**Solution:**

- Cap rectangle area to polygon area in display logic:
  ```csharp
  if (rectArea > comboArea) {
      rectArea = comboArea;
  }
  ```
- Verify area calculation units are consistent
- Check that rectangle corners are truly inside polygon

### Boundary-based vs Optimized discrepancies

**Symptom:** Large differences between algorithms for the same shape

**Expected Behavior:**
- Simple shapes: <1% difference
- Complex shapes: 5-15% difference is normal

**Investigation:**
- Check TestResults SVGs to visualize both results
- Look at angle selection (boundary-based may miss optimal angle)
- Verify edge analysis is finding dominant directions

### Embedded data not loading

**Symptom:** JavaScript can't find rectangle data

**Checklist:**

1. Verify `<script id="precomputed-rectangles">` exists in SVG
2. Check browser console for JSON parse errors
3. Ensure `loadPrecomputedRectangles()` is called before lookup
4. Hard refresh browser (Ctrl+Shift+R) to clear cache
5. Check that SVG file was actually updated (check file timestamp)

---

## Future Enhancements

### 1. Adaptive Algorithm Selection

Instead of always trying both, predict which algorithm to use:

```javascript
function selectAlgorithm(polygon) {
    const vertices = polygon.length;
    const hasRightAngles = analyzeEdgeAngles(polygon);

    if (vertices <= 6 && hasRightAngles) {
        return 'boundary-based';  // Skip optimized
    }
    return 'hybrid';  // Try both
}
```

### 2. Parallel Processing

Use worker threads to compute multiple combinations simultaneously:

```javascript
const { Worker } = require('worker_threads');

// Distribute 251 combinations across 4 workers
const workers = createWorkerPool(4);
await Promise.all(combinations.map(combo =>
    assignToWorker(combo, workers)
));
```

Could reduce total time from 20 minutes to ~5-7 minutes.

### 3. Incremental Updates

Only recompute changed combinations:

```javascript
// Compare old and new valid-data.json
const changed = findChangedCombinations(oldCombos, newCombos);

// Recompute only changed
for (const combo of changed) {
    await computeRectangle(combo);
}

// Merge with existing precomputed-rectangles.json
```

### 4. Multiple Aspect Ratios

Precompute rectangles with specific aspect ratio constraints:

```javascript
{
  "key": "Ballroom_Room_1",
  "rectangles": {
    "maxArea": { width: 116.5, height: 95.8, ... },
    "square": { width: 105.2, height: 105.2, ... },
    "widescreen": { width: 130.0, height: 73.1, ... }  // 16:9 ratio
  }
}
```

---

## Conclusion

The max-inscribed rectangle pipeline provides the **foundation** for geometric layout analysis in floor plan visualization. Key achievements:

✅ **Hybrid Algorithm:** Automatically balances speed and accuracy
✅ **Independent Operation:** Runs separately from constrained layouts
✅ **Same Architecture:** Follows proven precomputation patterns
✅ **Efficient Runtime:** Instant lookups, no computation at runtime
✅ **Embedded Data:** Single SVG file contains all three datasets
✅ **Scalable Design:** Easy to add more layout types

The system demonstrates how to **maximize area utilization** without constraints, serving as the baseline for more specialized layouts like boardroom and hollow square arrangements.

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

- **Algorithms**:
  - `FloorMat/SvgViewerBoundaryBased.cjs` - Boundary-based + hybrid
  - `FloorMat/SvgViewerOptimized.cjs` - Grid-based optimization
  - `FloorMat/SvgViewerInscribedRect.cjs` - Unified inscribed rectangle
  - `FloorMat/SvgViewerAlgorithms.cjs` - SVG path parsing utilities
  - `FloorMat/kdtree.cjs` - Spatial data structures
- **Test Runner**: `FloorMat/run-tests.js`
- **Orchestrator**: `FloorMat/process-all.js` or `FloorMat/process-external.js`
- **Project Extractor**: `FloorMat/extract-precomputed-project.js`
- **Project Embedder**: `FloorMat/embed-project.js`
- **Input Files**: `FloorMat/input/[ProjectName].svg` and `FloorMat/input/[ProjectName]-data.json`
- **Output Directory**: `FloorMat/output/[ProjectName]-output/`
  - Embedded SVG: `[ProjectName].svg`
  - Rectangles JSON: `[ProjectName]-rectangles.json`
  - Test Results: `TestResults/[ProjectName]-MaxInscribedResults/*.svg`

### Algorithm Types

- **boundary-based**: Fast (100-200ms), good for simple shapes
- **optimized**: Slow (1-3s), excellent for complex shapes
- **hybrid**: Automatic selection (recommended)

### Key Statistics

- Total combinations: 251
- Average time: ~1,842ms per combination
- Boundary-based used: ~15%
- Optimized used: ~85%
- File size: ~150 KB
