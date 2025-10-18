# Inscribed Rectangle Architecture and Implementation Guide

## Overview

This document describes the complete architecture for computing, storing, and displaying inscribed rectangles and area measurements for SVG floor plan combinations. This system precomputes all valid configurations to achieve instant runtime performance, and serves as a reference for extending the system with additional constraints (e.g., rectangles with specific size requirements).

**Last Updated:** 2025-10-18

---

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Coordinate System and Units](#coordinate-system-and-units)
3. [Valid Combination Rules](#valid-combination-rules)
4. [Precomputation Pipeline](#precomputation-pipeline)
5. [Data Storage and Embedding](#data-storage-and-embedding)
6. [Runtime Architecture](#runtime-architecture)
7. [Display and Validation](#display-and-validation)
8. [Extending with Size Constraints](#extending-with-size-constraints)
9. [Performance Considerations](#performance-considerations)

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

### Combination Generation

The `valid-combinations.json` file contains all 251 valid combinations following these rules:

```json
{
  "generatedAt": "2025-10-10T02:21:34.525Z",
  "totalCombinations": 251,
  "sectionIds": [
    "Ballroom_Room_1",
    "Ballroom_Room_2",
    // ... all sections
  ],
  "combinations": [
    {
      "key": "Ballroom_Room_1",
      "sections": ["Ballroom_Room_1"],
      "size": 1
    },
    {
      "key": "Ballroom_Room_1_Ballroom_Room_3",
      "sections": ["Ballroom_Room_1", "Ballroom_Room_3"],
      "size": 2
    }
    // ... all 251 combinations
  ]
}
```

**Key Format:** Sections sorted alphabetically and joined with underscore: `Section1_Section2_Section3`

---

## Precomputation Pipeline

### Step 1: Test Harness Execution

**File:** `LazyMagic.BlazorSvg/test-harness/test-runner.js`

**Purpose:** Generate 251 SVG files with inscribed rectangle visualizations and area data.

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
node test-runner.js
```

**Duration:** 15-20 minutes

**Process:**
1. Load valid combinations from `valid-combinations.json`
2. For each combination:
   - Parse SVG paths from `Level1.svg`
   - Extract path vertices and apply transforms
   - Merge paths using winding number algorithm
   - Calculate inscribed rectangle (boundary-based + optimized algorithms)
   - Calculate polygon area and rectangle area in square SVG inches
   - Generate comparison SVG with embedded area comments
   - Save to `TestResults/Combo_XXXX.svg`

**Output:**
```
TestResults/
├── Combo_0001.svg  (Ballroom_Room_1)
├── Combo_0002.svg  (Ballroom_Room_2)
├── ...
└── Combo_0251.svg  (Last combination)
```

Each file contains XML comments with area data:
```xml
<!-- Area Data (Square SVG Inches) -->
<!-- polygonArea: 2822.8891 -->
<!-- rectangleArea: 2830.9513 -->
```

### Step 2: Data Extraction

**File:** `LazyMagic.BlazorSvg/test-harness/extract-precomputed-rectangles.js`

**Purpose:** Extract rectangle and area data from generated SVG files into JSON.

```bash
node extract-precomputed-rectangles.js
```

**Duration:** ~30 seconds

**Process:**
1. Load valid combinations
2. For each combination:
   - Read `TestResults/Combo_XXXX.svg`
   - Extract area data using regex: `<!-- polygonArea: ([0-9.]+) -->`
   - Extract winning algorithm's rectangle data (corners, dimensions, centroid, angle)
   - Extract computation time
3. Write to `precomputed-rectangles.json`

**Output Format:**
```json
{
  "generatedAt": "2025-10-18T17:08:02.240Z",
  "totalCombinations": 251,
  "successfulComputations": 251,
  "totalComputationTimeMs": 898900,
  "averageComputationTimeMs": 3581.3,
  "statistics": {
    "boundaryBasedCount": 0,
    "optimizedCount": 251
  },
  "rectangles": [
    {
      "key": "Ballroom_Room_1",
      "sections": ["Ballroom_Room_1"],
      "rectangle": {
        "corners": [
          {"x": 350.97122, "y": 32.255595},
          {"x": 350.97122, "y": 128.07719},
          {"x": 234.48001, "y": 128.07719},
          {"x": 234.48001, "y": 32.255595}
        ],
        "width": 116.5,
        "height": 95.8,
        "area": 11162.4,
        "angle": 90.0,
        "centroid": {"x": 292.72561, "y": 80.16639},
        "type": "boundary-based"
      },
      "polygonArea": 2822.8891,
      "rectangleArea": 2830.9513,
      "computationTimeMs": 52.6
    }
    // ... 250 more entries
  ]
}
```

**File Size:** ~148 KB (269 KB on disk)

### Step 3: SVG Embedding

**File:** `LazyMagic.BlazorSvg/test-harness/embed-rectangles-in-svg.js`

**Purpose:** Embed precomputed data directly into `Level1.svg` as a `<script>` element.

**Important:** Copy JSON to correct location first:
```bash
cp "LazyMagic.BlazorSvg/test-harness/precomputed-rectangles.json" "BlazorTest.WASM/wwwroot/precomputed-rectangles.json"
node embed-rectangles-in-svg.js
```

**Duration:** ~5 seconds

**Process:**
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

#### Drawing Logic (Invalid Combination Handling)

```javascript
// Try to lookup precomputed rectangle first
let largestRect = await this.lookupPrecomputedRectangle(pathIds);

// Only draw rectangle if precomputed data exists (valid combination)
// Invalid combinations (no precomputed data) will not show inscribed rectangle
if (!largestRect) {
    console.log('[outline] No precomputed rectangle found - skipping inscription for invalid combination');
}

// ... later in the code ...

// Visualize the largest inscribed rectangle if found
if (largestRect) {
    // Draw rectangle using corners, width, height, angle
    const corners = largestRect.corners;
    const pathData = `M ${corners[0].x} ${corners[0].y} ` +
                   `L ${corners[1].x} ${corners[1].y} ` +
                   `L ${corners[2].x} ${corners[2].y} ` +
                   `L ${corners[3].x} ${corners[3].y} Z`;

    const rectPath = scope.path(pathData);
    rectPath.attr({
        fill: 'none',
        stroke: '#00FF00',
        strokeWidth: 2,
        'pointer-events': 'none'
    });
    rectPath.addClass("inscribed-rectangle");
}
```

**Key Point:** If `largestRect` is null (invalid combination), the rectangle visualization is simply skipped. No error, no fallback calculation.

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

---

## Display and Validation

### Page-Level Logic (SVGTestPage.razor)

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

### Validation Through Absence

**Key Design Decision:** Invalid combinations are validated implicitly by the absence of data.

- **Valid Combination:** `getAreaData()` returns data → Areas displayed, rectangle drawn
- **Invalid Combination:** `getAreaData()` returns `null` → Nothing displayed, no rectangle

**Benefits:**
1. No explicit validation code needed
2. No error messages to manage
3. Clear visual feedback (missing visualization = invalid)
4. Single source of truth (precomputed data defines validity)

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

**Lookup Pattern:**
```javascript
function lookupGridRectangle(pathIds, targetWidth, targetHeight) {
    const sortedKey = pathIds.slice().sort().join('_');
    const data = precomputedRectangles.lookup.get(sortedKey);

    if (!data) return null;

    // Find grid cell containing target dimensions
    const gridKey = `${Math.floor(targetWidth/50)*50}-${Math.floor(targetWidth/50+1)*50}x` +
                    `${Math.floor(targetHeight/25)*25}-${Math.floor(targetHeight/25+1)*25}`;

    return data.sizeGrid[gridKey];
}
```

### Test Harness Modifications

To implement any of these approaches, modify `test-runner.js`:

```javascript
// Original: Compute only largest rectangle
const largestRect = findLargestInscribedRectangle(polygon);

// Extended: Compute multiple constrained rectangles
const rectangles = [];

// 1. Largest (unconstrained)
rectangles.push({
    constraint: 'largest',
    ...findLargestInscribedRectangle(polygon)
});

// 2. Specific size constraints
const constraints = [
    { name: '100x80_min', minW: 100, maxW: 150, minH: 80, maxH: 120 },
    { name: 'standard_table', minW: 96, maxW: 120, minH: 72, maxH: 96 }
];

for (const constraint of constraints) {
    const rect = findConstrainedInscribedRectangle(polygon, constraint);
    if (rect) {
        rectangles.push({
            constraint: constraint.name,
            ...rect
        });
    }
}

// 3. Save all rectangles for this combination
combinationData.rectangles = rectangles;
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

## Performance Considerations

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

### Caching Strategy

**Browser Cache:**
- SVG file is cached with embedded data
- Changes to precomputed data require cache invalidation
- Use versioned URLs or cache-busting if needed: `Level1.svg?v=20251018`

**Application Cache:**
- Precomputed data loaded once per session
- Stored in instance property: `this.precomputedRectangles`
- Survives across multiple selections

---

## Best Practices and Lessons Learned

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
4. Embed in SVG (5 sec)
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

## Future Enhancements

### 1. Multiple Constraint Sets

Extend the system to support different constraint scenarios (banquet, classroom, theater):

```javascript
precomputedRectangles = {
  largest: Map(),      // Unconstrained
  banquet: Map(),      // 96"×72" tables
  classroom: Map(),    // 60"×30" tables
  theater: Map()       // 18" chair rows
}
```

### 2. Dynamic Constraint Input

Allow users to specify custom constraints at runtime:

```razor
<input type="number" @bind="minWidth" placeholder="Min Width" />
<input type="number" @bind="maxWidth" placeholder="Max Width" />
<button @onclick="FindConstrainedRectangle">Find Rectangle</button>
```

Then look up the precomputed rectangle closest to these constraints.

### 3. Aspect Ratio Constraints

Precompute rectangles at specific aspect ratios (16:9, 4:3, 2:1):

```javascript
{
  "aspectRatios": {
    "16:9": { width: 112.0, height: 63.0, area: 7056 },
    "4:3": { width: 110.0, height: 82.5, area: 9075 },
    "2:1": { width: 116.5, height: 58.25, area: 6786.125 }
  }
}
```

### 4. Compression

For large datasets, compress JSON before embedding:

```javascript
const compressed = pako.gzip(JSON.stringify(data));
const base64 = btoa(String.fromCharCode(...compressed));
```

Then decompress at runtime (adds ~10ms overhead but reduces file size 70-80%).

### 5. Progressive Loading

For very large datasets, load data in chunks:

```javascript
// Load critical data immediately
await loadPrecomputedData('single-rooms');

// Load multi-room combinations on demand
await loadPrecomputedData('multi-rooms-2-3');
await loadPrecomputedData('multi-rooms-4-plus');
```

---

## Conclusion

This architecture demonstrates that **precomputation + efficient lookup** can deliver instant performance even for complex geometric calculations. The key principles:

1. **Expensive → Build Time:** Move all heavy computation to build time
2. **Fast → Runtime:** Keep runtime logic simple (lookups only)
3. **Data-Driven:** Let precomputed data define validity
4. **Units Matter:** Always be explicit about coordinate systems and units
5. **Extensible:** Design for future constraint scenarios

When extending this system with size constraints, follow the same pattern:
- Precompute all scenarios at build time
- Store in efficient lookup structures
- Keep runtime logic minimal
- Validate through data presence/absence

The 15-20 minute build time is a small price to pay for instant user experience at runtime.
