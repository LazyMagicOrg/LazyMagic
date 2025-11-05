# FloorMat Directory Map

**Last Updated:** 2025-11-05
**Status:** ✅ **FULLY DOCUMENTED** - All 21 JavaScript files comprehensively documented

---

## Overview

The FloorMat directory contains a **unified pipeline** for generating valid room combinations and precomputing geometric layout data for SVG floor plans. The system now supports multi-project processing with automatic project detection.

### Three Layout Systems

The pipeline supports three parallel layout algorithms:
1. **MaxInscribed** - Largest possible rectangles with no constraints
2. **Boardroom** - Fixed 13ft width, variable length tables
3. **Hollow Square** - Perimeter table arrangements with 4ft+ inner clearance

---

## Current Files

### Algorithm Files (7 CommonJS modules)

All algorithm files are CommonJS modules (`.cjs`) designed for Node.js execution via Playwright. They are loaded by `run-tests.js` using `createRequire()`. Browser versions exist as ES6 modules in `LazyMagic.BlazorSvg/wwwroot/`.

| File | Line Count | Purpose |
|------|------------|---------|
| **SvgViewerBoundaryBased.cjs** | 2,406 | Boundary-based inscribed rectangle with hybrid centroid sampling |
| **SvgViewerOptimized.cjs** | 1,745 | Grid-based centroid sampling with polylabel, binary search expansion |
| **SvgViewerAlgorithms.cjs** | 1,340 | SVG path parsing, geometric primitives, point-in-polygon, convex hull |
| **SvgViewerInscribedRect.cjs** | 629 | Unified inscribed rectangle (dispatches to boundary/optimized) |
| **SvgViewerBoardroom.cjs** | 569 | Boardroom layout (fixed 13ft width, discrete length) |
| **SvgViewerHollowSquare.cjs** | 403 | Hollow square layout (discrete width & height) |
| **kdtree.cjs** | 462 | Spatial data structures (KDTree, SpatialGrid for acceleration) |

#### Algorithm Details

**SvgViewerBoundaryBased.cjs** (2,406 lines)
- **Purpose**: Finds largest inscribed rectangle using boundary-segment orientation sampling
- **Key Functions**:
  - `boundaryBasedInscribedRectangle()` - Main entry point
  - `selectCentroidStrategy()` - Chooses "uniform" grid or "hybrid" edge-focused sampling based on path count, vertex count, and polygon area
  - `removeCollinearPoints()` - Simplifies polygon by removing redundant vertices
- **Exports**: `boundaryBasedInscribedRectangle` (for use by InscribedRect)

**SvgViewerOptimized.cjs** (1,745 lines)
- **Purpose**: Fast inscribed rectangle finder using grid-based centroid sampling + binary search
- **Key Components**:
  - `polylabel()` - Pole of inaccessibility algorithm (Mapbox-based) finds optimal centroid
  - `SpatialHash` - Grid-based spatial structure for O(1) point-in-polygon tests
  - `fastInscribedRectangle()` - Main algorithm with multi-centroid sampling
- **Algorithm**: Samples centroids on grid, tries all aspect ratios, binary search expands best candidates
- **Exports**: `fastInscribedRectangle`, `SpatialHash`

**SvgViewerAlgorithms.cjs** (1,340 lines)
- **Purpose**: Core geometric utilities used by all algorithms
- **Key Functions**:
  - `parseSvgPath()` - Parses SVG path d="" attribute into polygon coordinates (handles M/L/H/V/C/S/Q/T/A/Z commands)
  - `calculatePolygonArea()` - Shoelace formula
  - `isPointInPolygon()` - Winding number algorithm
  - `segmentsIntersect()` - Line segment intersection test
  - `convexHull()` - Graham scan algorithm
  - `basicPolygonCleanup()` - Removes duplicate/collinear points
- **Exports**: Object with all utility functions

**SvgViewerInscribedRect.cjs** (629 lines)
- **Purpose**: Unified API for inscribed rectangle algorithms with dimension mode support
- **Dimension Modes**:
  - **Fixed**: Single value (e.g., boardroom width = 13ft)
  - **Discrete**: Base + integer increments (e.g., 14ft + n×6ft)
  - **Continuous**: Range with sampling or aspect ratios
- **Hybrid Algorithm Selection**:
  - Runs both boundary-based and optimized algorithms in parallel
  - Uses heuristics (coverage threshold, max time) to select best result
  - Falls back if one algorithm fails
- **Exports**: `findInscribedRectangle(polygon, options)`

**SvgViewerBoardroom.cjs** (569 lines)
- **Purpose**: Specialized layout for boardroom-style table arrangements
- **Constraints**:
  - Fixed width: 13ft (2 tables back-to-back @ 2.5ft each + 4ft spacing)
  - Variable length: 14ft + n×6ft (n = number of additional table sets)
  - Rotates to any angle for optimal fit
- **Algorithm**: Samples angles and centroids, expands length in 6ft increments
- **Exports**: `findBoardroomLayout(polygon, options)`

**SvgViewerHollowSquare.cjs** (403 lines)
- **Purpose**: Hollow square perimeter table arrangements with 4ft+ inner clearance
- **Constraints**:
  - Minimum: 14ft × 19ft (4 tables in hollow square)
  - Both dimensions expand in 6ft increments independently
  - Width: 14ft, 20ft, 26ft, ... (lengthRun increments)
  - Height: 19ft, 25ft, 31ft, ... (depthRun increments)
- **Algorithm**: Samples angles/centroids, tries all dimension combinations
- **Exports**: `findHollowSquareLayout(polygon, options)`

**kdtree.cjs** (462 lines)
- **Purpose**: Spatial data structures for accelerated geometric queries
- **Data Structures**:
  - `KDTree` - K-dimensional tree for nearest neighbor search, range queries, radius search
  - `SpatialGrid` - Grid-based polygon containment cache (precomputes cell classifications)
- **Operations**:
  - `nearest()` - Find closest point
  - `kNearest()` - Find k nearest neighbors
  - `rangeSearch()` - Find points in bounding box
  - `radiusSearch()` - Find points within radius
  - `containsPoint()` - Fast O(1) point-in-polygon test
  - `containsRectangle()` - Fast rectangle containment test
- **Exports**: `KDTree`, `SpatialGrid`

### Core Pipeline Scripts (11 ES6 modules)

All pipeline scripts are ES6 modules (`.js`) with `import`/`export` syntax, enabled by `package.json` `"type": "module"`.

| File | Lines | Purpose |
|------|-------|---------|
| **process-all.js** | 362 | Multi-project orchestrator (FloorMat/input/ directory) |
| **process-external.js** | 315 | Multi-project orchestrator (external directories) |
| **run-tests.js** | 1,275 | Layout algorithm test runner with Playwright |
| **compute-all-combinations.js** | 583 | Valid combination generator (10 validation rules) |
| **extract-all-precomputed.js** | 227 | Extract layout data (legacy single-project) |
| **extract-precomputed-project.js** | 195 | Extract layout data (per-project) |
| **embed-all-in-svg.js** | 205 | Embed datasets in SVG (legacy single-project) |
| **embed-project.js** | 170 | Embed datasets in SVG (per-project) |
| **embed-path-metadata.js** | 198 | Embed room metadata as floormat:* attributes |
| **calculate-polygon-areas.js** | 334 | Calculate polygon areas from SVG paths |
| **check-dependencies.js** | 53 | Auto-install missing npm dependencies |

#### Pipeline Script Details

**process-all.js** (362 lines)
- **Purpose**: Multi-project orchestration for FloorMat/input/ directory
- **Workflow**:
  1. Auto-detects all SVG files in `input/` directory
  2. Finds matching `{prefix}-data.json` files
  3. Generates valid combinations via `compute-all-combinations.js`
  4. Creates temporary test configs for MaxInscribed, Boardroom, Hollow Square
  5. Runs all three algorithms in parallel for ~3x speedup
  6. Extracts precomputed data via `extract-precomputed-project.js`
  7. Calculates polygon areas via `calculate-polygon-areas.js`
  8. Embeds path metadata via `embed-path-metadata.js`
  9. Embeds layout data via `embed-project.js`
  10. Cleans up intermediate files
- **Output**: `output/{prefix}-output/{prefix}-output.svg` (final SVG with all embedded data)
- **Usage**: `npm run process` or `node process-all.js`

**process-external.js** (315 lines)
- **Purpose**: Multi-project orchestration for external directory structures
- **Differences from process-all.js**:
  - Takes target directory as command-line argument
  - Creates `input/` and `output/` directories if missing
  - Processes first SVG found in external `input/` directory
  - Outputs to external `output/` directory
- **Workflow**: Same as process-all.js but with external paths
- **Output**: `<target-dir>/output/{prefix}-output.svg`
- **Usage**: `npm run process-external "C:\path\to\venue"` (must run from FloorMat directory)

**run-tests.js** (1,275 lines)
- **Purpose**: Generic test runner for layout algorithm execution
- **Key Responsibilities**:
  - Loads CommonJS algorithm modules (`.cjs`) via `createRequire()`
  - Parses SVG files using JSDOM to extract path geometry
  - Handles SVG transform matrices (translate, scale, rotate, matrix)
  - Applies transforms to merge multi-path polygons into unified coordinate space
  - Launches Playwright headless browser for each test case
  - Executes layout algorithms with configured options
  - Generates JSON and SVG output files for each test
  - Collects performance metrics (runtime, fill ratio, etc.)
- **Key Functions**:
  - `parseTransformMatrix()` - Parses SVG transform strings into 2D matrix
  - `extractPathData()` - Extracts path `d=""` attribute and parent transforms
  - `applyMatrixToPath()` - Applies transformation matrix to SVG path coordinates
  - `parseSvgCombination()` - Merges multiple paths into single polygon
- **Input**: JSON config file specifying algorithm, SVG path, combinations, options
- **Output**: Per-test JSON/SVG files in configured output directory
- **Usage**: `node run-tests.js <config-file.json>`

**compute-all-combinations.js** (583 lines)
- **Purpose**: Generates all valid room section combinations based on graph connectivity
- **Validation Rules** (10 rules):
  1. Adjacency Constraint - Shared aisles must be selected
  2. Single section must be Room (not Aisle/Crossing)
  3. Aisle can include single room
  4. Crossing requires ≥2 aisles
  5. Three or more aisles require crossing
  6. U-Shape aisle requirement
  7. All sections must be connected (single component)
  8. Must include at least one room
  9. Aisle must have at least one connected room
  10. Crossing cannot be only bridge (articulation point)
- **Key Functions**:
  - `buildGraph()` - Builds adjacency graph from sections and joins
  - `isValidCombination()` - Applies all 10 validation rules
  - `isConnectedGraph()` - BFS to verify single connected component
  - `checkLayoutRestrictions()` - Filters by LayoutRestriction field
- **Input**: `{prefix}-data.json` (level data with Rooms[], RoomSections[], Joins[])
- **Output**: `{prefix}-valid-combinations.json` (251 valid combinations for Level1)
- **Usage**: `node compute-all-combinations.js <prefix> [inputDir] [outputDir]`

**calculate-polygon-areas.js** (334 lines)
- **Purpose**: Calculates polygon areas from SVG path geometries and adds them to level data JSON
- **Algorithm**: Shoelace formula applied to parsed path coordinates
- **SVG Path Support**: M/L/H/V/C/S/Q/T/A/Z commands (absolute and relative)
- **Key Functions**:
  - `parseSvgPath()` - Comprehensive SVG path parser handling all command types
  - `calculatePolygonArea()` - Shoelace formula implementation
- **Curve Handling**: Approximates curves (C/S/Q/T/A) by using endpoints
- **Input**: SVG file, level data JSON
- **Output**: Level data JSON with `PolygonArea` added to each section
- **Usage**: `node calculate-polygon-areas.js <svgPath> <dataJsonPath> <outputJsonPath>`

**embed-path-metadata.js** (198 lines)
- **Purpose**: Embeds room section metadata from level data JSON into SVG as custom `floormat:*` attributes
- **Namespace**: `http://lazymagic.com/floormat`
- **Attributes Embedded**:
  - `floormat:section-type` (Room/Aisle/Crossing)
  - `floormat:layout-restriction` (allowed/warning/restricted)
  - `floormat:polygon-area` (calculated area in sq ft)
  - Plus any custom properties from section data (Area, Width, Depth overrides)
- **Property Naming**: Converts camelCase to kebab-case (e.g., `layoutRestriction` → `layout-restriction`)
- **Formatting**: Uses xml-beautify for readable output
- **Input**: Prefix, SVG file, level data JSON
- **Output**: SVG with embedded floormat:* attributes on all path elements
- **Usage**: `node embed-path-metadata.js <prefix> <svgPath> <dataJsonPath> <outputSvgPath>`

**extract-all-precomputed.js** (227 lines)
- **Purpose**: Legacy single-project extraction from TestResults to precomputed JSON files
- **Process**:
  - Scans `../TestResults/MaxInscribedResults/`, `BoardroomResults/`, `HollowSquareResults/`
  - Extracts layout data from individual test JSON files
  - Aggregates into three consolidated files
  - Calculates statistics (total time, average time, min/max/avg area)
- **Output Files**:
  - `precomputed-rectangles.json` (MaxInscribed data)
  - `precomputed-boardroom.json` (Boardroom layouts)
  - `precomputed-hollowsquare.json` (Hollow Square layouts)
- **Usage**: `npm run extract` or `node extract-all-precomputed.js`

**extract-precomputed-project.js** (195 lines)
- **Purpose**: Per-project extraction from ComputedLayouts to project-specific JSON files
- **Differences from extract-all-precomputed.js**:
  - Takes project prefix as parameter
  - Reads from `output/ComputedLayouts/{prefix}-*Results/`
  - Outputs to `output/{prefix}-rectangles.json`, etc.
  - Uses combination IDs from project-specific combinations file
- **Process**: Same extraction logic as legacy script but with project-specific paths
- **Usage**: `node extract-precomputed-project.js <prefix> <testResultsDir> <combinationsPath> <outputDir>`

**embed-all-in-svg.js** (205 lines)
- **Purpose**: Legacy single-project embedding into BlazorTest.WASM/wwwroot/Level1.svg
- **Process**:
  - Loads all three precomputed JSON files (`precomputed-*.json`)
  - Removes any existing embedded `<script>` elements with matching IDs
  - Embeds data as `<script type="application/json" id="precomputed-*"><![CDATA[...]]></script>`
  - Inserts into SVG `<defs>` section (creates if missing)
- **Target**: Hardcoded path to `../../BlazorTest.WASM/wwwroot/Level1.svg`
- **Usage**: `npm run embed` or `node embed-all-in-svg.js`

**embed-project.js** (170 lines)
- **Purpose**: Per-project embedding into project-specific output SVG
- **Differences from embed-all-in-svg.js**:
  - Takes project prefix and paths as parameters
  - Embeds `{prefix}-rectangles.json`, `{prefix}-boardroom.json`, `{prefix}-hollowsquare.json`
  - Outputs to `{prefix}-output.svg` in project output directory
  - Uses xml-beautify for formatted output
- **Process**: Same embedding logic as legacy script but parameterized
- **Usage**: `node embed-project.js <prefix> <svgPath> <outputDir>`

**check-dependencies.js** (53 lines)
- **Purpose**: Validates and auto-installs required npm dependencies before pipeline execution
- **Required Dependencies**: `jsdom`, `xml-beautify`, `xmldom`
- **Process**:
  - Checks if each package exists in `node_modules/`
  - Runs `npm install` if any dependencies are missing
  - Exits with error code 1 if installation fails
- **Usage**: Automatically invoked by pipeline scripts (not run directly)

### Configuration Files (8 files)

| File | Purpose |
|------|---------|
| **package.json** | NPM configuration with pipeline scripts (type: "module") |
| **package-lock.json** | NPM dependency lock file |
| **test-configs/maxinscribed-full.json** | Config for all 251 max-inscribed tests |
| **test-configs/maxinscribed-sample.json** | Config for sample max-inscribed tests |
| **test-configs/boardroom-full.json** | Config for all 251 boardroom tests |
| **test-configs/boardroom-sample.json** | Config for sample boardroom tests |
| **test-configs/hollowsquare-full.json** | Config for all 251 hollow square tests |
| **test-configs/hollowsquare-sample.json** | Config for sample hollow square tests |

### Source Data Files (4 files + input/ + output/ directories)

| File | Purpose | Used By |
|------|---------|---------|
| **valid-combinations.json** | Master list of 251 valid room combinations | All *-full.json configs |
| **valid-combinations-maxinscribed-sample.json** | Sample combos for max-inscribed | maxinscribed-sample.json |
| **valid-combinations-boardroom-sample.json** | Sample combos for boardroom | boardroom-sample.json |
| **valid-combinations-hollowsquare-sample.json** | Sample combos for hollow square | hollowsquare-sample.json |

### Input Directory (input/)

Project-specific source files for multi-project processing:

| File | Purpose | Used By |
|------|---------|---------|
| **Level1.svg** | Source SVG file for Level1 project | process-all.js |
| **Level1-data.json** | Level data with graph connectivity (sections and joins) | compute-all-combinations.js, embed-path-metadata.js |

### Output Directory (output/)

Generated output organized by project:

```
output/
└── Level1-output/
    ├── Level1-valid-combinations.json   (generated from Level1-data.json)
    ├── ComputedLayouts/
    │   ├── Level1-MaxInscribedResults/
    │   ├── Level1-BoardroomResults/
    │   └── Level1-HollowSquareResults/
    ├── Level1-rectangles.json           (extracted precomputed data)
    ├── Level1-boardroom.json
    ├── Level1-hollowsquare.json
    └── Level1-output.svg                (final SVG with embedded data)
```

### Generated Data Files (3 files - Legacy)

These files are generated by the legacy single-project pipeline:

| File | Purpose | Generated By |
|------|---------|--------------|
| **precomputed-rectangles.json** | Max-inscribed layout data | extract-all-precomputed.js |
| **precomputed-boardroom.json** | Boardroom layout data | extract-all-precomputed.js |
| **precomputed-hollowsquare.json** | Hollow square layout data | extract-all-precomputed.js |

### Utility Files (3 files)

| File | Lines | Purpose |
|------|-------|---------|
| **SvgViewer.js** | 3,515 | Browser component reference copy (active version in BlazorSvg) |
| **check-limits.cjs** | 26 | Debug tool for hollow square performance testing |
| **FloorMat-map.md** | 609+ | This comprehensive documentation file |

#### Utility File Details

**SvgViewer.js** (3,515 lines)
- **Purpose**: Interactive SVG viewer component for browser (Blazor WebAssembly)
- **Status**: Reference copy for comparison/debugging
- **Active Version**: `LazyMagic.BlazorSvg/wwwroot/SvgViewer.js` (used by Blazor runtime)
- **Key Responsibilities**:
  - Interactive pan/zoom controls with mouse and touch support
  - Path selection and multi-selection with auto-select logic
  - Precomputed layout data lookup and visualization
  - Rectangle overlay rendering for MaxInscribed/Boardroom/Hollow Square
  - SVG path parsing and geometric calculations (client-side)
  - Integration with Blazor via DotNetObjectReference callbacks
- **Key Features**:
  - `getPaths()` - Extracts path IDs from SVG
  - `selectPath()` / `unselectPath()` - Path selection with auto-select chain
  - `visualizeGroups()` - Renders layout overlay rectangles
  - `loadPrecomputedData()` - Loads embedded `<script>` data from SVG
  - Guard flags to prevent multiple concurrent visualizations
- **Module Type**: ES6 module (for browser)
- **Dependencies**: Snap.svg for SVG manipulation
- **Note**: This is NOT used by FloorMat pipeline (Node.js uses `.cjs` algorithm files directly)

**check-limits.cjs** (26 lines)
- **Purpose**: Simple debug script for testing hollow square layout algorithm performance
- **Test Polygon**: 223×96 rectangle (Combo_0004 dimensions)
- **Configuration**: `angleSamples=12`, `centroidSamples=5`
- **Output**: Elapsed time and result (table count, dimensions)
- **Usage**: `node check-limits.cjs`
- **Dependencies**: Requires `SvgViewerHollowSquare.cjs`
- **Note**: Minimal script for quick performance checks during algorithm development

---

## Total File Count

- **Algorithm Files:** 7 CommonJS modules (.cjs) - **11,882 lines total**
- **Core Pipeline Scripts:** 11 ES6 modules (.js) - **3,917 lines total**
- **Configuration Files:** 2 base + 6 test configs in test-configs/
- **Source Data Files:** 4 sample combination files (legacy)
- **Generated Data Files:** 3 precomputed files (legacy single-project)
- **Utility Files:** 3 files - **3,609+ lines total** (SvgViewer, check-limits, this doc)
- **Input Directory:** 2 files per project (Level1.svg, Level1-data.json)
- **Output Directory:** Generated per project
- **Total Essential Files:** ~29 source files (excluding node_modules and generated output)
- **Total Lines of Code:** **~19,408 lines** across all JavaScript/documentation files

### File Type Breakdown

**CommonJS Modules (.cjs)** - 7 files, 11,882 lines:
- SvgViewerBoundaryBased.cjs (2,406)
- SvgViewerOptimized.cjs (1,745)
- SvgViewerAlgorithms.cjs (1,340)
- SvgViewerInscribedRect.cjs (629)
- SvgViewerBoardroom.cjs (569)
- kdtree.cjs (462)
- SvgViewerHollowSquare.cjs (403)

**ES6 Pipeline Scripts (.js)** - 11 files, 3,917 lines:
- run-tests.js (1,275) - Test execution engine
- compute-all-combinations.js (583) - Combination validation
- process-all.js (362) - Multi-project orchestrator
- calculate-polygon-areas.js (334) - Area calculator
- process-external.js (315) - External directory processor
- extract-all-precomputed.js (227) - Legacy data extraction
- embed-all-in-svg.js (205) - Legacy SVG embedding
- embed-path-metadata.js (198) - Metadata embedder
- extract-precomputed-project.js (195) - Per-project extraction
- embed-project.js (170) - Per-project SVG embedding
- check-dependencies.js (53) - Dependency checker

**Utility Files** - 3 files, 3,609+ lines:
- SvgViewer.js (3,515) - Browser component reference
- FloorMat-map.md (609+) - Comprehensive documentation
- check-limits.cjs (26) - Debug script

---

## Pipeline Workflows

### Multi-Project Pipeline (NEW - Recommended)

The new multi-project pipeline automatically detects and processes all projects in the `input/` directory:

```bash
# Process projects in FloorMat/input/ directory
npm run process
# Internally runs: node process-all.js

# Process projects in external directory
npm run process-external "C:\path\to\venue"
# Internally runs: node process-external.js "C:\path\to\venue"

# This will:
# 1. Find all SVG files in input/ (e.g., Level1.svg)
# 2. Look for matching data files (e.g., Level1-data.json)
# 3. Generate valid combinations (251 combos based on 10 validation rules)
# 4. Generate test configs for MaxInscribed, Boardroom, and Hollow Square
# 5. Run all layout tests
# 6. Extract precomputed data
# 7. Embed data into output SVG (output/Level1-output/Level1-output.svg)
```

**Project Structure (Internal - FloorMat/input/):**
```
input/
├── {ProjectName}.svg                    (User provides: Source SVG)
└── {ProjectName}-data.json              (User provides: Level data with graph connectivity)

output/
└── {ProjectName}-output/
    ├── {ProjectName}-valid-combinations.json  (Generated: 251 valid combos)
    ├── ComputedLayouts/                       (Generated: Layout test results)
    │   ├── {ProjectName}-MaxInscribedResults/
    │   ├── {ProjectName}-BoardroomResults/
    │   └── {ProjectName}-HollowSquareResults/
    ├── {ProjectName}-rectangles.json          (Generated: Precomputed data)
    ├── {ProjectName}-boardroom.json
    ├── {ProjectName}-hollowsquare.json
    └── {ProjectName}-output.svg               (Generated: Final SVG with embedded data)
```

**Project Structure (External Directory):**
```
C:\path\to\venue/
├── input/
│   ├── {ProjectName}.svg                    (User provides: Source SVG)
│   └── {ProjectName}-data.json              (User provides: Level data with graph connectivity)
└── output/
    └── {ProjectName}-output/
        ├── {ProjectName}-valid-combinations.json  (Generated: 251 valid combos)
        ├── ComputedLayouts/                       (Generated: Layout test results)
        │   ├── {ProjectName}-MaxInscribedResults/
        │   ├── {ProjectName}-BoardroomResults/
        │   └── {ProjectName}-HollowSquareResults/
        ├── {ProjectName}-rectangles.json          (Generated: Precomputed data)
        ├── {ProjectName}-boardroom.json
        ├── {ProjectName}-hollowsquare.json
        └── {ProjectName}-output.svg               (Generated: Final SVG with embedded data)
```

**Note:** For external processing, you must run `npm run process-external` from the FloorMat directory (C:\...\LazyMagic.FloorMat\FloorMat\) since that's where all the processing scripts are located.

### Generating Valid Combinations (Manual)

If you want to generate valid combinations separately (the multi-project pipeline does this automatically):

```bash
# Manually generate valid combinations for a specific project
node compute-all-combinations.js <prefix> [inputDir] [outputDir]

# Example:
node compute-all-combinations.js Level1
# Reads: input/Level1-data.json
# Writes: output/Level1-output/Level1-valid-combinations.json

# Or with custom paths:
node compute-all-combinations.js Level1 ./input ./output/Level1-output
```

**Note:** The `npm run process` command automatically generates valid combinations, so you typically don't need to run this manually.

**Validation Rules Applied:**
1. Adjacency Constraint (shared aisles must be selected)
2. Single section must be Room
3. Aisle can include single room
4. Crossing requires ≥2 aisles
5. Three or more aisles require crossing
6. U-Shape aisle requirement
7. All sections must be connected
8. Must include at least one room
9. Aisle must have at least one connected room
10. Crossing cannot be only bridge

### Legacy Single-Project Pipeline

The original pipeline is still available for backward compatibility:

```bash
# Step 1: Run all tests (generates ../TestResults/*/*.json)
npm run test:all
# Or individually:
npm run test:maxinscribed
npm run test:boardroom
npm run test:hollowsquare

# Step 2: Extract precomputed data (generates precomputed-*.json)
npm run extract

# Step 3: Embed into SVG (updates Level1.svg in BlazorTest.WASM)
npm run embed

# Or run entire pipeline:
npm run pipeline
```

---

## Directory Structure

```
FloorMat/
├── Algorithm Files (7 CommonJS modules)
│   ├── SvgViewerBoundaryBased.cjs       (Boundary-based + hybrid algorithm, 2,407 lines)
│   ├── SvgViewerOptimized.cjs           (Grid-based centroid sampling, 1,746 lines)
│   ├── SvgViewerInscribedRect.cjs       (Unified inscribed rectangle, 630 lines)
│   ├── SvgViewerBoardroom.cjs           (Boardroom layout, 570 lines)
│   ├── SvgViewerHollowSquare.cjs        (Hollow square layout, 550 lines)
│   ├── kdtree.cjs                       (Spatial data structures, 462 lines)
│   └── SvgViewerAlgorithms.cjs          (SVG path parsing, 1,340 lines)
│
├── Core Pipeline Scripts (9 ES6 modules)
│   ├── compute-all-combinations.js      (Generate valid combinations)
│   ├── process-all.js                   (Multi-project orchestrator - internal)
│   ├── process-external.js              (Multi-project orchestrator - external)
│   ├── run-tests.js                     (Layout algorithm test runner, loads .cjs)
│   ├── extract-all-precomputed.js       (Legacy extraction)
│   ├── extract-precomputed-project.js   (Per-project extraction)
│   ├── embed-all-in-svg.js              (Legacy embedding)
│   ├── embed-project.js                 (Per-project embedding)
│   └── embed-path-metadata.js           (Path metadata embedder)
│
├── Configuration (2 files + 1 directory)
│   ├── package.json
│   ├── package-lock.json
│   └── test-configs/
│       ├── maxinscribed-full.json
│       ├── maxinscribed-sample.json
│       ├── boardroom-full.json
│       ├── boardroom-sample.json
│       ├── hollowsquare-full.json
│       └── hollowsquare-sample.json
│
├── Source Data (4 sample combination files)
│   ├── valid-combinations.json
│   ├── valid-combinations-maxinscribed-sample.json
│   ├── valid-combinations-boardroom-sample.json
│   └── valid-combinations-hollowsquare-sample.json
│
├── Input Directory (input/)
│   ├── Rooms.json                       (Graph connectivity data)
│   ├── Level1.svg                       (Project SVG source)
│   └── Level1-combinations.json         (Project valid combinations)
│
├── Output Directory (output/)
│   └── Level1-output/                   (Per-project outputs)
│       ├── TestResults/
│       │   ├── MaxInscribedResults/
│       │   ├── BoardroomResults/
│       │   └── HollowSquareResults/
│       ├── Level1-rectangles.json
│       ├── Level1-boardroom.json
│       ├── Level1-hollowsquare.json
│       └── Level1-output.svg
│
├── Generated Data (3 legacy files)
│   ├── precomputed-rectangles.json
│   ├── precomputed-boardroom.json
│   └── precomputed-hollowsquare.json
│
├── Utilities (3 files)
│   ├── SvgViewer.js                     (Browser component reference, 161 KB)
│   ├── check-limits.cjs                 (Debug tool)
│   └── FloorMat-map.md                  (This documentation)
│
└── External Dependencies
    └── node_modules/
        ├── jsdom (SVG parsing)
        ├── xmldom (XML parsing)
        └── xml-beautify (SVG formatting)

Total: ~28 essential files (excluding generated outputs)
```

### Module System Architecture

**ES6 Modules (`.js` files):**
- `package.json` specifies `"type": "module"`
- Pipeline scripts use `import`/`export` syntax
- Modern Node.js execution

**CommonJS Modules (`.cjs` files):**
- Algorithm files explicitly use `.cjs` extension
- Loaded by `run-tests.js` using `createRequire(import.meta.url)`
- Use `module.exports` syntax
- Required for compatibility with existing algorithm code

**Why this hybrid approach:**
- Pipeline scripts benefit from ES6 async/await syntax
- Algorithm files were originally CommonJS
- `.cjs` extension allows both to coexist in same directory
- `run-tests.js` bridges the two module systems
```

---

## NPM Scripts

```json
{
  "scripts": {
    "process": "node process-all.js",
    "process-external": "node process-external.js"
  }
}
```

**Multi-Project Scripts:**
- `npm run process` - Automatically detects and processes all projects in FloorMat/input/ directory
- `npm run process-external "C:\path\to\venue"` - Process projects in external directory (must run from FloorMat directory)

**Legacy Scripts (still available via direct node commands):**
- `node run-tests.js test-configs/maxinscribed-full.json`
- `node run-tests.js test-configs/boardroom-full.json`
- `node run-tests.js test-configs/hollowsquare-full.json`
- `node extract-all-precomputed.js`
- `node embed-all-in-svg.js`

**Combination Generation:**
- `node compute-all-combinations.js <prefix>` - Generate all 251 valid combinations from [prefix]-data.json
- Example: `node compute-all-combinations.js Level1`

---

## Data Flow

### Multi-Project Pipeline Flow

```
1. Project Auto-Detection
   process-all.js scans input/ directory
   ├─→ Finds: Level1.svg
   └─→ Finds: Level1-data.json

2. Combination Generation (per project)
   compute-all-combinations.js
   ├─→ Reads: input/Level1-data.json (level data with graph connectivity)
   └─→ Generates: output/Level1-output/Level1-valid-combinations.json (251 combos)

3. Dynamic Config Generation
   process-all.js creates temp configs
   ├─→ temp-Level1-maxinscribed.json
   ├─→ temp-Level1-boardroom.json
   └─→ temp-Level1-hollowsquare.json

4. Test Execution (per project)
   run-tests.js + temp configs
   └─→ Generates: output/Level1-output/ComputedLayouts/
       ├── Level1-MaxInscribedResults/*.json
       ├── Level1-BoardroomResults/*.json
       └── Level1-HollowSquareResults/*.json

5. Data Extraction (per project)
   extract-precomputed-project.js
   ├─→ Reads: output/Level1-output/ComputedLayouts/*/*.json
   └─→ Generates: output/Level1-output/
       ├── Level1-rectangles.json
       ├── Level1-boardroom.json
       └── Level1-hollowsquare.json

6. SVG Embedding (per project)
   embed-project.js
   ├─→ Reads: output/Level1-output/Level1-*.json (3 files)
   ├─→ Reads: input/Level1.svg
   └─→ Generates: output/Level1-output/Level1-output.svg
       (embeds all 3 datasets as <script> elements in <defs>)
```

### Legacy Single-Project Flow

```
1. Source Data
   valid-combinations.json (251 combos)
   └─→ Referenced by test-configs/*.json

2. Test Execution
   run-tests.js + test-configs/maxinscribed-full.json
   └─→ Generates: ../TestResults/MaxInscribedResults/*.json

3. Data Extraction
   extract-all-precomputed.js
   ├─→ Reads: ../TestResults/*/*.json
   └─→ Generates: precomputed-*.json (3 files)

4. SVG Embedding
   embed-all-in-svg.js
   ├─→ Reads: precomputed-*.json (3 files)
   └─→ Updates: ../../BlazorTest.WASM/wwwroot/Level1.svg
```

---

## File History

### Recent Additions (2025-10-27)

**New Files Added:**
1. `compute-all-combinations.js` - Restored and updated with all 10 validation rules
2. `input/Level1.svg` - Project source SVG
3. `input/Level1-data.json` - Project-specific level data with graph connectivity
4. `process-all.js` - Multi-project orchestrator (internal)
5. `process-external.js` - Multi-project orchestrator (external)
6. `extract-precomputed-project.js` - Per-project extraction
7. `embed-project.js` - Per-project embedding
8. `embed-path-metadata.js` - Embeds room section metadata as floormat:* attributes

### Previous Cleanup (2025-10-25)

**Before:** ~55 files
**After:** 14 files
**Reduction:** 81%

**Files Deleted:**
- `algorithms.js` - Never imported, experimental
- `algorithm-params.js` - Never used, replaced by JSON configs
- `analyze-breaches.js` - Dependencies don't exist
- `analyze-discrepancies.js` - References deleted directories
- `analyze-rotation-issues.md` - Documents obsolete workflow
- `browser-test-runner.js` - Incomplete, wrong paths
- `test-page.html` - Incomplete browser tests
- `compare-results.js` - References deleted directories
- `debug-iterations.js` - Broken, duplicate of check-limits.cjs
- `create-hollowsquare-samples.js` - One-time utility
- `run-hollowsquare-samples.js` - Superseded by unified pipeline
- `test-config.js` - 3,627 lines, hardcoded paths, broken
- `testharness.md` - 90% obsolete documentation
- `test-runner.js` - Broken (imports deleted file)

Note: Original `compute-all-combinations.js` was deleted in 2025-10-25 cleanup (marked as "deprecated") but has been restored and updated with complete validation rules in 2025-10-27.

---

## Key Features

### Combination Generation with 10 Validation Rules

The `compute-all-combinations.js` script generates all 251 valid room combinations from graph connectivity data:

**Input:** `input/{prefix}-data.json` - Level data with graph (sections: rooms/aisles/crossings and joins)
**Output:** `{prefix}-valid-combinations.json` - 251 valid combinations
**Rules Applied:**
1. Adjacency Constraint - Shared aisles must be selected
2. Single section must be Room
3. Aisle can include single room
4. Crossing requires ≥2 aisles
5. Three or more aisles require crossing
6. U-Shape aisle requirement
7. All sections must be connected
8. Must include at least one room
9. Aisle must have at least one connected room
10. Crossing cannot be only bridge (articulation point check)

### Multi-Project Support

The new `process-all.js` orchestrator:
- Auto-detects all SVG files in `input/` directory
- Finds matching combination files (e.g., `Level1-combinations.json`)
- Generates temporary configs dynamically
- Processes each project independently
- Outputs to organized `output/{ProjectName}-output/` directories

### JSON Configuration

All test configuration is in clean JSON files (`test-configs/*.json`):
```json
{
  "testName": "Boardroom Full Tests",
  "algorithm": "boardroom",
  "svgPath": "../../BlazorTest.WASM/wwwroot/Level1-normal.svg",
  "outputDir": "../TestResults/BoardroomResults",
  "algorithmOptions": { ... },
  "combinationsFile": "../valid-combinations.json",
  "outputOptions": { ... }
}
```

### Three Datasets in One SVG

Both embedding scripts embed all three layout types simultaneously:
```xml
<svg>
  <defs>
    <script type="application/json" id="precomputed-rectangles"><![CDATA[...]]></script>
    <script type="application/json" id="precomputed-boardroom"><![CDATA[...]]></script>
    <script type="application/json" id="precomputed-hollowsquare"><![CDATA[...]]></script>
  </defs>
  <!-- SVG content -->
</svg>
```

---

## Related Documentation

For detailed algorithm documentation, see:
- **../Documentation/InscribedRectangle-Guide.md** - System architecture overview
- **../Documentation/MaxInscribedLayoutPipeline.md** - Max-inscribed algorithm details
- **../Documentation/BoardroomLayoutPipeline.md** - Boardroom layout system
- **../Documentation/HollowSquareLayoutPipeline.md** - Hollow square layout system
- **../Documentation/EmbedData-QuickRef.md** - Quick reference for data regeneration

---

## FloorMat Status ✅

FloorMat has evolved into a comprehensive multi-project pipeline:

### Version 1.0 (2025-10-25)
- ✅ All legacy test runners removed (test-runner*.js)
- ✅ All broken/obsolete analysis tools removed
- ✅ All hardcoded configs replaced with JSON
- ✅ Unified pipeline tested and working
- ✅ 81% file reduction achieved
- ✅ Rebranded from "test-harness" to "FloorMat"

### Version 2.0 (2025-10-27)
- ✅ Combination generation system implemented
- ✅ All 10 validation rules documented and working
- ✅ Multi-project support via `process-all.js`
- ✅ Input/output directory structure
- ✅ Project auto-detection
- ✅ Per-project extraction and embedding scripts
- ✅ Graph connectivity validation (articulation point detection)

### Version 2.1 (2025-10-28)
- ✅ External directory processing via `process-external.js`
- ✅ Flexible project location support
- ✅ Automatic input/output folder creation
- ✅ Graceful error handling for missing SVG files
- ✅ Fixed file naming mismatch in extraction (MaxInscribed vs Level1-MaxInscribed)

### Version 3.0 (2025-11-03) - Module System Refactor
- ✅ Converted algorithm files to CommonJS (`.cjs` extension)
- ✅ Fixed module loading issues (ES6 + CommonJS coexistence)
- ✅ Updated `run-tests.js` to use `createRequire()` for .cjs files
- ✅ Set `package.json` `"type": "module"` for ES6 pipeline scripts
- ✅ Cleaned up 5 temporary/debug files (analyze-winding.js, etc.)
- ✅ Updated all documentation to reflect `.cjs` file locations
- ✅ Clarified separation: FloorMat (build-time .cjs) vs BlazorSvg (runtime .js)
- ✅ Fixed `SpatialHash` loading from correct module (optimized.cjs)

**Module Architecture:**
- Algorithm files: CommonJS (`.cjs`) - 7 files, ~7,700 lines
- Pipeline scripts: ES6 modules (`.js`) - 9 files
- Hybrid approach allows optimal syntax for each use case

**FloorMat is now a complete, self-contained, multi-project layout generation system ready for production use.**

---

**End of FloorMat Map**
