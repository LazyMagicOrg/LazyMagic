# Embedding Precomputed Rectangle Data in SVG Files

This document describes the process for generating, extracting, and embedding precomputed inscribed rectangle data into SVG floor plan files for the LazyMagic.BlazorSvg project.

## Quick Start - Commands

Run these commands in order to regenerate and embed the precomputed rectangle data:

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
```

```bash
node test-runner.js
```

```bash
node extract-precomputed-rectangles.js
```

```bash
node embed-rectangles-in-svg.js
```

### Boardroom Layouts (Optional)

```bash
node test-runner-boardroom.js
```

```bash
node extract-precomputed-boardroom.js
```

```bash
node embed-boardroom-in-svg.js
```

### Hollow Square Layouts (Optional)

```bash
node test-runner-hollowsquare.js
```

```bash
node extract-precomputed-hollowsquare.js
```

```bash
node embed-hollowsquare-in-svg.js
```

### Run Application

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic"
```

```bash
dotnet run --project BlazorTest.WASM/BlazorTest.WASM.csproj
```

**Total time:**
- Max-inscribed only: ~20 minutes
- All three systems: ~60 minutes (15-20 min each)

---

## Overview

The LazyMagic.BlazorSvg project provides interactive SVG floor plans with geometric layout calculations. To improve performance and avoid runtime computation, we precompute layouts for all valid path combinations and embed this data directly into the SVG files.

### Three Parallel Systems

1. **Max-Inscribed Rectangles** - Largest rectangle that fits in any polygon
2. **Boardroom Layouts** - Fixed-width table arrangements (13 ft × variable length)
3. **Hollow Square Layouts** - Perimeter table arrangements with open center

All three systems follow the same pipeline: generate → extract → embed.

### What Gets Embedded

For each of the 251 valid path combinations, the embedded data includes:

**Max-Inscribed Rectangles:**
- **Rectangle Visualization Data**
  - Corners (4 points with x, y coordinates)
  - Width and height
  - Angle of rotation
  - Centroid position
  - Algorithm type used (boundary-based or optimized)
- **Area Measurements** (in square SVG inches)
  - Polygon area: Total area of the combined polygon
  - Rectangle area: Area of the inscribed rectangle
- **Performance Metrics**
  - Computation time in milliseconds

**Boardroom Layouts:**
- **Layout Data**
  - Corners, width (13 ft fixed), variable height
  - Number of sets and total tables
  - Angle and centroid
- **Area Measurements**
  - Polygon area and boardroom area
- **Performance Metrics**

**Hollow Square Layouts:**
- **Layout Data**
  - Outer rectangle corners, width, height
  - Inner clearance dimensions
  - Angle and centroid
- **Area Measurements**
  - Polygon area, outer area, inner area
- **Performance Metrics**

### Key Technical Details

#### SVG Coordinate System
- Scale: 1/12 architectural scale where 1 SVG inch = 1 real-world foot
- Layer Transform: All SVG paths are transformed with scale factor 48.345845
- User Units: 96 user units = 1 SVG inch

#### Area Calculation
Areas are calculated in **square SVG inches** (not real-world inches):

```
area_svg_inches = (area_pretransform × scaleFactor²) / (96²)

where:
  area_pretransform = polygon/rectangle area before transform
  scaleFactor = 48.345845
  96 = user units per inch
```

The shoelace formula is used to calculate polygon areas from vertices.

## Process Steps

### Prerequisites

Ensure you have Node.js installed and the following files are present:

- `LazyMagic.BlazorSvg/test-harness/test-runner.js` - Main test harness
- `LazyMagic.BlazorSvg/test-harness/extract-precomputed-rectangles.js` - Data extraction script
- `LazyMagic.BlazorSvg/test-harness/embed-rectangles-in-svg.js` - Data embedding script
- `LazyMagic.BlazorSvg/test-harness/valid-combinations.json` - Valid path combinations
- `BlazorTest.WASM/wwwroot/Level1.svg` - Target SVG file

### Step 1: Run Test Harness

Generate 251 SVG test result files with inscribed rectangle visualizations and embedded area data.

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
node test-runner.js
```

**Duration:** 15-20 minutes

**Output:**
- 251 SVG files in `LazyMagic.BlazorSvg/TestResults/MaxInscribedResults/` directory
- Each file named `Combo_XXXX.svg` (e.g., Combo_0001.svg)
- Each file contains XML comments with area data:
  ```xml
  <!-- Area Data (Square SVG Inches) -->
  <!-- polygonArea: 2822.8891 -->
  <!-- rectangleArea: 2830.9513 -->
  ```

**What It Does:**
- Loads all valid path combinations from `valid-combinations.json`
- For each combination:
  - Parses SVG paths and creates merged polygon
  - Calculates inscribed rectangles using both boundary-based and optimized algorithms
  - Determines which algorithm produced the best result
  - Calculates polygon and rectangle areas in square SVG inches
  - Generates comparison visualization SVG with embedded area data
  - Saves result to TestResults/MaxInscribedResults directory

### Step 2: Extract Precomputed Rectangles

Parse the generated SVG files and extract all rectangle data into a JSON file.

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
node extract-precomputed-rectangles.js
```

**Duration:** ~30 seconds

**Output:**
- `LazyMagic.BlazorSvg/test-harness/precomputed-rectangles.json`
- File size: ~148 KB (269 KB on disk)

**What It Does:**
- Loads valid combinations from `valid-combinations.json`
- For each combination:
  - Reads corresponding SVG file from TestResults/MaxInscribedResults directory
  - Extracts area data from XML comments using regex:
    - `<!-- polygonArea: ([0-9.]+) -->`
    - `<!-- rectangleArea: ([0-9.]+) -->`
  - Extracts winning algorithm's rectangle data (corners, dimensions, centroid)
  - Extracts performance metrics (computation time)
- Generates JSON file with complete dataset

**JSON Structure:**
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
  ]
}
```

### Step 3: Embed Data into Level1.svg

Embed the precomputed rectangle data directly into the Level1.svg file.

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
node embed-rectangles-in-svg.js
```

**Duration:** ~5 seconds

**Output:**
- Updates `BlazorTest.WASM/wwwroot/Level1.svg` in place
- File size: ~225 KB

**What It Does:**
- Loads `precomputed-rectangles.json`
- Reads Level1.svg
- Removes any existing embedded data (from previous runs)
- Embeds the entire JSON dataset as a `<script>` element in SVG `<defs>`:
  ```xml
  <defs>
    <script type="application/json" id="precomputed-rectangles"><![CDATA[
  {"generatedAt":"2025-10-18T17:08:02.240Z","totalCombinations":251,...}
    ]]></script>
  </defs>
  ```
- Writes modified SVG back to file

### Step 4: Test the Application

Run the Blazor WebAssembly application to verify the embedded data works:

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic"
dotnet run --project BlazorTest.WASM/BlazorTest.WASM.csproj
```

The application should:
- Load Level1.svg
- Extract precomputed rectangle data from embedded `<script>` element
- Display inscribed rectangles without runtime calculation
- Show polygon and rectangle area measurements

## File Locations

### Source Files

**Max-Inscribed Rectangles:**
- Test Runner: `test-harness/test-runner.js`
- Extractor: `test-harness/extract-precomputed-rectangles.js`
- Embedder: `test-harness/embed-rectangles-in-svg.js`

**Boardroom Layouts:**
- Test Runner: `test-harness/test-runner-boardroom.js`
- Extractor: `test-harness/extract-precomputed-boardroom.js`
- Embedder: `test-harness/embed-boardroom-in-svg.js`

**Hollow Square Layouts:**
- Test Runner: `test-harness/test-runner-hollowsquare.js`
- Extractor: `test-harness/extract-precomputed-hollowsquare.js`
- Embedder: `test-harness/embed-hollowsquare-in-svg.js`

**Common:**
- Valid Combinations: `test-harness/valid-combinations.json`

### Generated Files
- **Max-Inscribed Test Results**: `TestResults/MaxInscribedResults/Combo_XXXX.svg` (251 files)
- **Boardroom Test Results**: `TestResults/BoardroomResults/Combo_XXXX.svg` (251 files)
- **Hollow Square Test Results**: `TestResults/HollowSquareResults/Combo_XXXX.svg` (251 files)
- **Precomputed Rectangles JSON**: `test-harness/precomputed-rectangles.json`
- **Precomputed Boardroom JSON**: `test-harness/precomputed-boardroom.json`
- **Precomputed Hollow Square JSON**: `test-harness/precomputed-hollowsquare.json`
- **Target SVG**: `BlazorTest.WASM/wwwroot/Level1.svg`

## Troubleshooting

### Test Harness Fails
- Ensure `valid-combinations.json` exists and contains 251 combinations
- Check that TestResults directories exist (create if missing):
  - `TestResults/MaxInscribedResults/`
  - `TestResults/BoardroomResults/`
  - `TestResults/HollowSquareResults/`
- Verify all required dependencies are installed (`npm install`)

### Extraction Returns Null Areas
- This means the SVG files don't have area comment data
- Re-run the test harness to regenerate SVG files with area comments
- Check that Combo_0001.svg contains lines like:
  ```xml
  <!-- polygonArea: 2822.8891 -->
  <!-- rectangleArea: 2830.9513 -->
  ```

### Embedding Fails
- Ensure `precomputed-rectangles.json` exists in test-harness directory
- Check that Level1.svg exists in `BlazorTest.WASM/wwwroot/`
- Verify the SVG file is well-formed XML

### Areas Don't Display in Application
- Check browser console for JavaScript errors
- Verify that Level1.svg contains embedded script elements:
  - `<script id="precomputed-rectangles">`
  - `<script id="precomputed-boardroom">`
  - `<script id="precomputed-hollowsquare">`
- Ensure the application's SVG loader is looking for embedded data

## Performance Notes

### Timing (Per System)
- **Test Harness**: 15-20 minutes (computing all 251 combinations)
- **Extraction**: ~30 seconds (parsing 251 SVG files)
- **Embedding**: ~5 seconds (writing JSON to SVG)
- **Total Process**: ~20 minutes per system
- **All Three Systems**: ~60 minutes total

### File Sizes
- Each test result SVG: ~3-5 KB
- Precomputed JSON (each): ~150 KB
- Level1.svg original: ~225 KB
- Level1.svg with all three datasets: ~262.5 KB
- Total increase: ~37.5 KB (17%)

### Runtime Benefits
By precomputing and embedding the data:
- Eliminates runtime computation for all three systems
- Instant lookup and display (< 1ms)
- Improves user experience with immediate visualization
- No need for external JSON HTTP requests
- All data embedded in single SVG file

## Code Modifications

### Adding Area Calculations (test-runner.js)

The area calculation functions use the shoelace formula and proper scale conversion:

```javascript
function calculatePolygonAreaInSvgInches(polygon, scaleFactor = 48.345845) {
    const UNITS_PER_INCH = 96;

    // Calculate area in pre-transform coordinates using shoelace formula
    let areaPreTransform = 0;
    for (let i = 0; i < polygon.length; i++) {
        const j = (i + 1) % polygon.length;
        areaPreTransform += polygon[i].x * polygon[j].y;
        areaPreTransform -= polygon[j].x * polygon[i].y;
    }
    areaPreTransform = Math.abs(areaPreTransform) / 2;

    // Convert to square user units by scaling up
    const areaUserUnits = areaPreTransform * (scaleFactor * scaleFactor);

    // Convert to square SVG inches
    const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);

    return areaSvgInches;
}
```

### Embedding in SVG (test-runner.js)

Area data is embedded as XML comments in the generated SVG:

```javascript
return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vbX} ${vbY} ${vbWidth} ${vbHeight}">
  <title>${testCase.name}</title>

  <!-- Area Data (Square SVG Inches) -->
  <!-- polygonArea: ${polygonArea.toFixed(4)} -->
  <!-- rectangleArea: ${rectangleArea.toFixed(4)} -->

  <!-- Rest of SVG content -->
</svg>`;
```

### Extracting from Comments (extract-precomputed-rectangles.js)

Regular expressions extract the area values:

```javascript
let polygonArea = null;
let rectangleArea = null;
const polygonAreaMatch = svgContent.match(/<!--\s*polygonArea:\s*([0-9.]+)\s*-->/);
const rectangleAreaMatch = svgContent.match(/<!--\s*rectangleArea:\s*([0-9.]+)\s*-->/);
if (polygonAreaMatch) polygonArea = parseFloat(polygonAreaMatch[1]);
if (rectangleAreaMatch) rectangleArea = parseFloat(rectangleAreaMatch[1]);
```

## Future Enhancements

### Potential Improvements
1. **Incremental Updates**: Only regenerate changed combinations instead of all 251
2. **Parallel Processing**: Use worker threads to speed up test harness
3. **Compression**: Compress JSON data before embedding to reduce SVG file size
4. **Validation**: Add validation step to verify embedded data integrity
5. **Multiple SVGs**: Support embedding into multiple target SVG files (Level2.svg, etc.)
6. **Area Display**: Add UI component to display polygon/rectangle areas on hover

### Configuration Options
Consider adding a config file (`embed-config.json`) to specify:
- Target SVG files to process
- Scale factor (if different from 48.345845)
- Output directories
- Compression settings
- Validation rules

## Version History

### V3.0.0 (2025-10-18)
- Added polygon and rectangle area calculations in square SVG inches
- Embedded area data as XML comments in test result SVGs
- Updated extraction script to parse area values from comments
- Enhanced JSON structure to include `polygonArea` and `rectangleArea` fields
- Improved embedding script to handle larger JSON payloads

### Initial Implementation
- Test harness for generating 251 combination tests
- Extraction of rectangle visualization data
- Embedding of precomputed data into SVG files
