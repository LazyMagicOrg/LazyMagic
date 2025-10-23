# Test Harness Documentation

## Overview

This test harness validates inscribed rectangle algorithms for complex polygon shapes extracted from SVG files. It tests multiple algorithm implementations and tracks their performance, accuracy, and coverage metrics.

## ⚠️ Understanding the Test Runner Architecture (Important!)

**Read this section first if you're confused about which test runner to use!**

The test harness has a confusing architecture that creates ambiguity about which test set will run:

### The Core Issue

1. **Single Test Runner Implementation**: There is only ONE functional test runner: `test-runner.js`
2. **Config File Swapping**: To run different test sets, the system swaps out the config file that `test-runner.js` imports
3. **Misleading File Names**: The presence of `test-runner-sample.js` suggests a parallel runner, but it's just a stub

### File Structure

```
test-harness/
├── test-runner.js              # The ONLY functional test runner (imports test-config.js)
├── test-runner-sample.js       # STUB - doesn't actually run tests
├── test-config.js              # Generated file with 251 test cases (full suite)
├── test-config-sample.js       # Hardcoded file with 6 test cases (NOT used by runners)
├── valid-combinations.json     # Source data for full suite (251 combos)
├── valid-combinations-sample.json  # Source data for sample tests (7 combos)
└── compute-sample.js           # Script that swaps configs and runs test-runner.js
```

### The Actual Workflow

#### Running Full Test Suite
```bash
node test-runner.js
# Directly imports and uses test-config.js (251 tests)
```

#### Running Sample Test Suite
```bash
node compute-sample.js
# 1. Loads valid-combinations-sample.json (7 tests)
# 2. Backs up test-config.js → test-config.js.backup
# 3. Generates temporary test-config.js with 7 tests
# 4. Runs test-runner.js (which now loads the 7-test config)
# 5. Restores test-config.js from backup
```

### Why This Is Confusing

1. **File naming implies parallelism**: Having both `test-runner.js` and `test-runner-sample.js` suggests two independent runners
2. **Config swapping is hidden**: The mechanism of temporarily overwriting `test-config.js` is not obvious
3. **Unused files**: `test-config-sample.js` exists but isn't actually used by any runner
4. **Indirect execution**: To run sample tests, you don't run `test-runner-sample.js`, you run `compute-sample.js`

### The Correct Mental Model

Think of it as:
- **ONE test runner** (`test-runner.js`)
- **ONE config slot** (`test-config.js`)
- **TWO ways to populate the config slot**:
  - Default: Contains 251 tests from `valid-combinations.json`
  - Temporary: `compute-sample.js` swaps in 7 tests from `valid-combinations-sample.json`

### How to Avoid Confusion

**Before running any tests, ask yourself**:
1. Do I want to run ALL 251 tests or just the sample?
2. If sample → use `compute-sample.js` (not `test-runner-sample.js`)
3. If full suite → use `test-runner.js`

**To modify sample tests**:
1. Edit `valid-combinations-sample.json` (add/remove combinations)
2. Run `node compute-sample.js` (it will regenerate `test-config.js` temporarily)

**Never**:
- Run `test-runner-sample.js` directly (it's a stub)
- Manually edit `test-config.js` (it's auto-generated)
- Edit `test-config-sample.js` (it's not used)

### Improvement Suggestion

The architecture could be clearer if:
1. `test-runner-sample.js` was deleted (it's misleading)
2. `test-config-sample.js` was deleted (it's unused)
3. `compute-sample.js` was renamed to `run-sample-tests.js` (clearer intent)
4. OR: Make `test-runner.js` accept a `--config` parameter to specify which config to use

## Quick Start

### Running Tests

```bash
# Navigate to test harness directory
cd test-harness

# Run full test suite (251 tests)
node test-runner.js

# Run sample tests only (7 tests)
node compute-sample.js

# Compare boundary-based vs optimized algorithms
node test-boundary-based.js

# Run parameter optimization sweep
node parameter-sweep.js
```

### Output Locations

- **Console**: Pass/fail results, coverage percentages, timing
- **TestResults/MaxInscribedResults/*.svg**: Visual representations of max-inscribed rectangle results
- **TestResults/BoardroomResults/*.svg**: Visual representations of boardroom layout results
- **TestResults/MaxInscribedResults/test-output.txt**: Full console log for max-inscribed tests
- **sweep-results-*.md**: Parameter sweep reports

## Algorithm Implementations

The test harness compares three algorithm approaches:

1. **Boundary-Based Algorithm** (`SvgViewerBoundaryBased.js`)
   - Analyzes polygon edges to identify dominant angles
   - Groups edges by similar orientations (within 5° tolerance)
   - Tests rectangles at boundary-derived angles
   - Fast (~40-70ms) but less accurate on complex shapes
   - Best for simple polygons with clear rectangular structure

2. **Optimized Algorithm** (`SvgViewerOptimized.js`)
   - Grid-based centroid sampling with polylabel refinement
   - Tests multiple aspect ratios at each angle
   - Uses binary search for optimal rectangle sizing
   - Thorough (~1000ms) and accurate
   - Best for complex, irregular polygons

3. **Hybrid Algorithm** (`SvgViewerBoundaryBased.js:hybridInscribedRectangle`)
   - Tries boundary-based first (fast path)
   - Falls back to optimized if coverage < 95% threshold
   - Returns the best result from both approaches
   - Balances speed and accuracy

## Core Components

### `test-runner.js`
Main test execution script that:
- Loads test cases from `test-config.js`
- Extracts SVG path data and builds boundary polygons
- Runs the hybrid algorithm on each test case
- Generates SVG visualizations in `TestResults/MaxInscribedResults/` folder
- Reports coverage, timing, and validation results

### `test-config.js`
Defines test cases and configuration:
```javascript
{
  name: "Test01",
  description: "Two ballrooms side by side",
  paths: ["Ballroom_1", "Ballroom_3"],
  validationThreshold: 0.98  // 98% coverage target
}
```

### `test-boundary-based.js`
Comparison runner that:
- Tests both boundary-based and optimized algorithms
- Generates detailed performance comparisons
- Reports speed improvements and coverage differences

### `parameter-sweep.js`
Automated parameter optimization tool:
- Tests multiple algorithm configurations
- Generates markdown reports with results
- Helps identify optimal parameter values

### `algorithm-params.js`
Predefined parameter configuration sets for testing different optimization strategies.

### `test-history.md`
Log of all test runs with observations and findings.

### `OPTIMIZATION_SUMMARY.md`
Executive summary of optimization work and results.

## How the Algorithms Work

### Polygon Extraction

1. **Parse SVG paths**: Extract path data from SVG elements (handles `<path>` and `<rect>`)
2. **Convert to line segments**: Parse SVG path commands into line segments
3. **Merge coincident points**: Identify shared vertices between paths
4. **Build network**: Create graph structure of connected segments
5. **Traverse outer edge**: Use winding algorithm to find exterior boundary
6. **Create polygon**: Generate ordered list of boundary vertices

### Boundary-Based Algorithm

```javascript
// 1. Extract and analyze edges
const edges = extractBoundaryEdges(polygon);
// Returns: [{ p1, p2, length, angle, dx, dy }]

// 2. Group by similar angles (±5° tolerance)
const angleGroups = findDominantAngles(edges);
// Returns: [{ angle, edges[], totalLength }]

// 3. Identify perpendicular pairs
// Finds angle groups that are ~90° apart

// 4. Test rectangles at candidate angles
for (const angle of anglesToTest) {
  const rect = findMaxRectangleAtAngle(polygon, angle);
  // - Rotates polygon to align with angle
  // - Tests 15×15 grid of centroids + polygon centroid
  // - For each centroid, tests 13 aspect ratios
  // - Binary search for maximum scale at each aspect ratio
}
```

**Key insight**: Uses polygon geometry to guide angle selection, reducing search space.

### Optimized Algorithm

```javascript
// 1. Generate candidate centroids
const centroids = getMultipleCentroids(polygon, {
  gridStep: 8.0,              // 8px grid spacing
  polylabelPrecision: 0.5     // Pole of inaccessibility
});
// Typically 300-400 centroids

// 2. Test angles (0°, dominant angles, perpendiculars)
for (const angle of [0, 9, 22, 99, ...]) {
  // 3. For each centroid
  for (const centroid of centroids) {
    // 4. Calculate directional distances
    const distances = getDirectionalDistancesToEdge(polygon, centroid);
    const maxWidth = distances.E + distances.W;
    const maxHeight = distances.N + distances.S;

    // 5. Test 13 aspect ratios
    for (const aspectRatio of aspectRatios) {
      // 6. Binary search for maximum scale
      // Test if rectangle fits inside polygon
    }
  }
}
```

**Key insight**: Exhaustive search with spatial optimization finds global optimum.

### Hybrid Algorithm Logic

```javascript
// Step 1: Try boundary-based (fast)
const boundaryResult = boundaryBasedInscribedRectangle(polygon);

// Step 2: Check if good enough
if (boundaryResult.area / targetArea >= 0.95) {
  return boundaryResult;  // Skip optimized (fast path)
}

// Step 3: Run optimized (thorough)
const optimizedResult = fastInscribedRectangle(polygon);

// Step 4: Return best result
return boundaryResult.area > optimizedResult.area
  ? boundaryResult
  : optimizedResult;
```

## Understanding Results

### SVG Visualizations

Generated SVG files show:
- **Blue polygon**: Boundary polygon (outer edge)
- **Red rectangle**: Inscribed rectangle found by algorithm
- **Blue circles (b0-bN)**: Boundary vertices
- **Red circles (r0-r3)**: Rectangle corners
- **Red dot**: Rectangle centroid
- **Legend**: Algorithm type, dimensions, area, angle, time, coverage

### Coverage Metrics

**Coverage = (Rectangle Area / Target Area) × 100%**

- **≥98%**: ✓ PASSED - Excellent fit
- **90-97%**: Acceptable but room for improvement
- **<90%**: ❌ FAILED - Significant gap

### Performance Metrics

- **Boundary-based**: 40-80ms (fast)
- **Optimized**: 1000ms (thorough)
- **Hybrid**: Variable (64ms if boundary-based sufficient, 1050ms if needs optimized)

## Test Cases

### Test01: Two Adjacent Ballrooms
- **Paths**: `Ballroom_1`, `Ballroom_3`
- **Shape**: Simple parallelogram (6 vertices)
- **Optimal angle**: 9.4°
- **Expected coverage**: 98%+
- **Result**: Boundary-based achieves 98.9% in 64ms

### Test02: L-Shaped Ballrooms
- **Paths**: `Ballroom_1`, `Ballroom_Aisle_12`, `Ballroom_2`
- **Shape**: L-shape (8 vertices)
- **Optimal angle**: 99.4°
- **Challenge**: Requires precise centroid positioning
- **Result**: Optimized achieves 89.4% in 1050ms

### Test03: Complex Multi-Room
- **Paths**: `Ballroom_1`, `Ballroom_Aisle_12`, `Ballroom_2`, `Ballroom_3`
- **Shape**: Complex polygon (10 vertices)
- **Optimal angle**: Varies
- **Result**: Boundary-based achieves 93.4% in 1048ms

## Key Algorithm Parameters

### Boundary-Based
```javascript
{
  maxAngles: 8,              // Test top 8 dominant angles
  angleTolerance: 5,         // Group angles within 5°
  testPerpendicular: true,   // Test 90° rotations
  gridSteps: 15,             // 15×15 centroid grid + polygon centroid
  aspectRatios: [            // 13 aspect ratios tested
    0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4,
    1.7, 2.0, 2.3, 2.5, 2.8, 3.0
  ]
}
```

### Optimized
```javascript
{
  maxTime: 1000,                 // 1 second time limit
  gridStep: 8.0,                 // 8px centroid grid
  polylabelPrecision: 0.5,       // Polylabel refinement
  aspectRatios: [...],           // Same 13 ratios
  binarySearchPrecision: 0.0001, // Scale search precision
  binarySearchMaxIterations: 20  // Max binary search steps
}
```

### Hybrid
```javascript
{
  coverageThreshold: 0.95,  // Skip optimized if boundary ≥95%
  targetArea: 21352.7,      // For coverage calculation
  // ... includes all boundary-based and optimized params
}
```

## Coordinate Space

The test harness handles multiple coordinate spaces:

1. **SVG Space**: Original SVG coordinates with transforms
2. **Ballroom Space**: Scaled coordinates (÷48.345845 for ballroom paths)
3. **Target Space**: Transform matrices applied to target rectangles

The `extractPathData()` function handles:
- Matrix transforms: `transform="matrix(a,b,c,d,e,f)"`
- Rotation transforms: `transform="rotate(angle)"`
- Scaling to ballroom coordinate space

## Common Issues

### Grid Sampling Problem
**Symptom**: Changing grid resolution (15×15 vs 16×16) causes large result changes

**Cause**: Uniform grids miss optimal centroids between grid points

**Solution**: Always test polygon geometric centroid in addition to grid (implemented)

### Multi-Segment Edges
**Symptom**: Algorithm misses optimal angle on shapes with small edge segments

**Cause**: Small "kinks" in boundaries create noise in angle detection

**Current behavior**: Algorithm correctly groups edges by total length, prioritizing dominant angles

### L-Shaped Polygons
**Symptom**: Boundary-based underperforms on L-shapes

**Cause**: Optimal rectangle requires precise centroid positioning in corner of "L"

**Solution**: Hybrid algorithm falls back to optimized for these cases

## Optimization History

See `OPTIMIZATION_SUMMARY.md` and `test-history.md` for:
- Parameter tuning results
- Performance improvements over time
- Coverage progression: 80.4% → 89.7% → 93.9% (current)
- Identified bottlenecks and solutions

## Future Improvements

### To Reach 98% Target on All Tests

1. **Adaptive Grid Refinement**
   - Start with coarse grid
   - Refine around best results
   - Iteratively improve

2. **Polylabel Integration for Boundary-Based**
   - Add pole of inaccessibility to test centroids
   - Maintain speed advantage while improving accuracy

3. **Edge-Aligned Rectangles**
   - Force one rectangle edge to align with polygon edge
   - Optimize the perpendicular dimension
   - Better for truly rectangular rooms

4. **Shape-Adaptive Strategy**
   - Detect simple vs complex polygons
   - Use boundary-based for simple (fast)
   - Use optimized for complex (accurate)

5. **Multi-Rectangle Solutions**
   - Decompose complex shapes
   - Find multiple rectangles
   - Useful for truly irregular polygons

## Complete File Structure

```
test-harness/
├── testharness.md              # This file - complete documentation
├── test-runner.js              # Main test executor (imports test-config.js)
├── test-runner-sample.js       # STUB - not functional
├── compute-sample.js           # Sample test runner (swaps config)
├── test-boundary-based.js      # Algorithm comparison
├── parameter-sweep.js          # Parameter optimization
├── test-config.js              # Auto-generated test cases (251 tests)
├── test-config-sample.js       # NOT USED - legacy file
├── valid-combinations.json     # Source data for full suite
├── valid-combinations-sample.json  # Source data for sample tests
├── algorithm-params.js         # Parameter configurations
├── test-history.md             # Test execution log
└── OPTIMIZATION_SUMMARY.md     # Optimization findings

../wwwroot/
├── SvgViewerAlgorithms.js      # Core polygon processing
├── SvgViewerOptimized.js       # Optimized algorithm
└── SvgViewerBoundaryBased.js   # Boundary & hybrid algorithms

../TestResults/                 # Generated output directory
├── MaxInscribedResults/        # Max-inscribed rectangle test results
│   ├── Combo_0001.svg
│   ├── Combo_0002.svg
│   ├── ...
│   └── test-output.txt
└── BoardroomResults/           # Boardroom layout test results
    ├── Combo_0001.svg
    ├── Combo_0002.svg
    └── boardroom-summary.json
```

## Contributing

When modifying algorithms:

1. Run full test suite: `node test-runner.js`
2. Compare algorithms: `node test-boundary-based.js`
3. Document changes in `test-history.md`
4. Update this documentation if behavior changes
5. Regenerate `OPTIMIZATION_SUMMARY.md` for significant improvements
