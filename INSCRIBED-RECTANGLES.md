# Inscribed Rectangle System Documentation

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [SvgViewer Component](#svgviewer-component)
4. [Inscribed Rectangle Algorithms](#inscribed-rectangle-algorithms)
5. [Test Harness](#test-harness)
6. [Precomputed Rectangles](#precomputed-rectangles)
7. [Deployment Pipeline](#deployment-pipeline)
8. [Troubleshooting](#troubleshooting)

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
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                          Data Sources                            │
│  • Level1.svg (SVG geometry from S3 or local)                   │
│  • precomputed-rectangles.json (250 precomputed results)        │
│  • Rooms.json (graph connectivity for validation)               │
└─────────────────────────────────────────────────────────────────┘
```

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

## Inscribed Rectangle Algorithms

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

**Console output:**
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

## Test Harness

**Location:** `C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness\`

### Purpose
Pre-compute inscribed rectangles for all valid ballroom path combinations to enable instant lookup in production.

### Workflow

#### 1. **Generate Valid Combinations**

**Script:** `compute-all-combinations.js`

```bash
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness
node compute-all-combinations.js
```

**What it does:**
- Loads `Rooms.json` (graph connectivity: rooms, aisles, crossings)
- Generates all possible path combinations (2^15 = 32,768 possibilities)
- Validates each combination against rules:
  - **Rule 0**: Single section must be Room (not Aisle/Crossing)
  - **Rule 1**: Each aisle connects to ≥1 room
  - **Rule 2**: Each crossing has ≥2 sections total
  - **Rule 3**: Each crossing has ≥2 connected aisles present
  - **Rule 4**: If 3+ aisles connect to crossing, crossing must be present
  - **Rule 5**: Aisle can't be missing if 2+ connected rooms present (U-shape)
  - **Rule 6**: Crossings can't be only bridge between components
- Outputs: `valid-combinations.json` (251 valid combinations)
- Generates: `test-config.js` with test cases

**Output files:**
- `valid-combinations.json`: Metadata about valid combinations
- `test-config.js`: Test configuration for Playwright

**Statistics:**
```
Processed 15 sections
Generated 32,768 total combinations
Found 251 valid combinations (0.8%)
Saved to valid-combinations.json
```

#### 2. **Compute Inscribed Rectangles**

**Script:** `run-tests.ps1`

```bash
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness
.\run-tests.ps1
```

**What it does:**
- Starts local web server: `http://localhost:3000`
- Serves: `test.html` (SvgViewer test page)
- Launches Playwright browser automation
- For each of 251 combinations:
  1. Load SVG with selected paths
  2. Trigger inscribed rectangle calculation
  3. Capture result (corners, width, height, area, angle, type, time)
  4. Save visualization as SVG in `TestResults/Combo_XXXX.svg`
  5. Append result to `results.txt`
- Shuts down web server

**Console output:**
```
Starting web server on http://localhost:3000...
Web server started (PID: 12345)
Running tests with npx playwright test...

Running 251 tests using 1 worker

  ✓ [chromium] › inscribed-rectangle.spec.js:7:6 › Combo 0001 (Ballroom_Room_1)
  ✓ [chromium] › inscribed-rectangle.spec.js:7:6 › Combo 0002 (Ballroom_Room_2)
  ...
  ✓ [chromium] › inscribed-rectangle.spec.js:7:6 › Combo 0251 (All sections)

251 passed (15.2m)
```

**Test output:**
- `TestResults/Combo_XXXX.svg`: Visual representation with legend
- `TestResults/results.txt`: Line-delimited JSON results

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
</svg>
```

#### 3. **Extract Precomputed Data**

**Script:** `extract-precomputed-rectangles.js`

```bash
node extract-precomputed-rectangles.js
```

**What it does:**
- Reads all 251 SVG files from `TestResults/`
- Parses each SVG to extract:
  - Rectangle corners (from `<polygon>` element)
  - Width, height, area, angle (from `<text>` legend)
  - Centroid (from `<circle>` marker)
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

Step 2: Parsing SVG files from TestResults...
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

---

## Precomputed Rectangles

### Data Flow

```
Test Harness                    Live Test                  Full App
─────────────                  ──────────                ──────────

compute-all-combinations.js
  ↓
valid-combinations.json
  ↓
run-tests.ps1 (Playwright)
  ↓
TestResults/*.svg
TestResults/results.txt
  ↓
extract-precomputed-rectangles.js
  ↓
precomputed-rectangles.json ──────→ Copy to BlazorTest.WASM/wwwroot
                                              ↓
                                    Test with dotnet run
                                    Verify rectangles load
                                              ↓
                           Copy to BCTenancies/bcs-cerulean/base/SetsCmp/data/
                                              ↓
                                    Deploy-AssetsAws (to S3)
                                              ↓
                           https://s3.../base/SetsCmp/data/precomputed-rectangles.json
                                              ↓
                                    SetsApp (Production)
                                    Loads from S3 automatically
```

### File Locations

**Test Harness:**
```
C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness\
├── compute-all-combinations.js      # Generate valid combinations
├── run-tests.ps1                    # Run Playwright tests
├── inscribed-rectangle.spec.js      # Playwright test specs
├── extract-precomputed-rectangles.js # Extract data from SVGs
├── test.html                        # Test page HTML
├── valid-combinations.json          # 251 valid combinations (OUTPUT)
├── test-config.js                   # Test configuration (OUTPUT)
├── precomputed-rectangles.json      # Final precomputed data (OUTPUT)
└── TestResults/
    ├── Combo_0001.svg              # Visual result for combo 1
    ├── Combo_0002.svg
    ├── ...
    ├── Combo_0251.svg
    └── results.txt                 # Raw test results
```

**Live Test Version:**
```
C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\BlazorTest.WASM\wwwroot\
├── Level1.svg                       # SVG with new naming convention
├── precomputed-rectangles.json      # Copied from test harness
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

## Deployment Pipeline

### 1. **Rebuild LazyMagic.BlazorSvg Package**

After changes to SvgViewer.js or algorithms:

```powershell
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic
dotnet build LazyMagic.BlazorSvg\LazyMagic.BlazorSvg.csproj -c Release
```

**What happens:**
- Builds package: `LazyMagic.BlazorSvg.3.0.1.nupkg`
- Copies to: `C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\Packages\`
- Clears NuGet cache: `%USERPROFILE%\.nuget\packages\lazymagic.blazorsvg\3.0.1\`

### 2. **Update Assets in BCTenancies**

After regenerating precomputed data:

```powershell
# Copy SVG
Copy-Item "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\BlazorTest.WASM\wwwroot\Level1.svg" `
          "C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies\bcs-cerulean\base\SetsCmp\data\Level1.svg" -Force

# Copy precomputed rectangles
Copy-Item "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness\precomputed-rectangles.json" `
          "C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies\bcs-cerulean\base\SetsCmp\data\precomputed-rectangles.json" -Force
```

### 3. **Deploy Assets to S3**

```powershell
cd C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies
pwsh -Command "& { . ./Import-LzAws.ps1; Deploy-AssetsAws }"
```

**What happens:**
- Syncs all assets from `BCTenancies` to S3 bucket: `bcs---assets-4933-b260`
- Uploads to: `s3://bcs---assets-4933-b260/base/SetsCmp/data/`
- Only uploads changed files (idempotent)
- Processes all tenants and subtenants (system, bcs, free, cerulean, walv, conventionplanit)

### 4. **Rebuild SetsApp**

```powershell
cd C:\Users\noaht\source\repos\_Dev\BCProjects\SetsApp
dotnet build -c Release
```

**What happens:**
- References updated LazyMagic.BlazorSvg package (3.0.1)
- Builds WASMApp with new SvgViewer.js changes

### 5. **Test Locally**

```powershell
cd WASMApp
dotnet run
```

**Verify:**
- Browser console shows: `[precomputed] Loading precomputed rectangles from: https://s3.../precomputed-rectangles.json`
- Selection shows: `[precomputed] ✓ Found precomputed rectangle for X sections`
- Rectangle matches test harness results (larger, better fit)

### 6. **Deploy to Production**

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
   .\run-tests.ps1
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

---

## Future Improvements

### Algorithm Enhancements
- **WebAssembly port** of optimized algorithm (10x faster)
- **Multi-threaded computation** using Web Workers
- **Machine learning** to predict best algorithm without trying both
- **Edge caching** to avoid re-parsing path data

### Precomputed Data
- **Compression:** gzip precomputed-rectangles.json (253 KB → ~60 KB)
- **Progressive loading:** Load common combinations first, lazy-load rare ones
- **Embed in SVG:** Store in `<metadata>` tag for single-file deployment
- **Versioning:** Track precomputed data version vs. algorithm version

### UI Enhancements
- **Live preview** while dragging/selecting
- **Animation** when rectangle appears
- **Comparison view:** Show boundary vs. optimized side-by-side
- **Export:** Save selection + rectangle as new SVG file

### Testing
- **Visual regression tests:** Compare rendered rectangles to reference images
- **Performance tests:** Ensure no degradation in algorithm speed
- **Integration tests:** E2E tests in actual SetsApp environment

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
- `test-harness/compute-all-combinations.js`: Validation + combination generator
- `test-harness/run-tests.ps1`: PowerShell test runner
- `test-harness/inscribed-rectangle.spec.js`: Playwright test specs
- `test-harness/extract-precomputed-rectangles.js`: SVG parser + JSON generator

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

---

**Last Updated:** 2025-10-08
**Version:** LazyMagic.BlazorSvg 3.0.1
**Author:** LazyMagic Development Team
