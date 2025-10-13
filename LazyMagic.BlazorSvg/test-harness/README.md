# SVG Inscribed Rectangle Test Harness

Node.js-based iterative test harness for testing and refining inscribed rectangle algorithms.

## Setup

1. Make sure you have Node.js installed (v18 or higher recommended)
2. Navigate to this directory:
   ```bash
   cd test-harness
   ```

## Configuration

Edit `test-config.js` to:
- Configure the path to your SVG file (`svgPath`)
- Add/modify test cases with specific path combinations
- Adjust validation thresholds

Current test cases:
1. **Two Ballrooms**: `Ballroom_1` + `Ballroom_3`
2. **Two Ballrooms with Aisle**: `Ballroom_1` + `Ballroom_Aisle_12` + `Ballroom_2`
3. **Complex**: `Ballroom_1` + `Ballroom_Aisle_12` + `Ballroom_2` + `Ballroom_4`

## Running Tests

### Main Test Scripts

```bash
# Run browser-based test runner (opens browser, generates visual SVG output)
node browser-test-runner.js

# Run all 251 combination tests (comprehensive test suite)
node compute-inscribed-rectangles.js

# Test a specific combination
node compute-sample.js

# Generate all valid combinations
node compute-all-combinations.js

# Run parameter sweep for algorithm tuning
node parameter-sweep.js
```

### Precomputed Rectangles

```bash
# Generate precomputed rectangles JSON
node precompute-rectangles.js

# Extract precomputed rectangles from test results
node extract-precomputed-rectangles.js
```

### Legacy npm Scripts

```bash
# Run all tests
npm test

# Run with auto-reload on file changes
npm run test:watch
```

## Output

The test runner will:
1. Load the SVG file
2. Extract the specified paths by ID
3. Parse path data into vertices
4. Find the outer boundary by filtering shared vertices
5. Calculate the maximum inscribed rectangle
6. Display results including:
   - Boundary polygon vertices
   - Rectangle type (parallelogram/trapezoid)
   - Dimensions, area, and angle
   - Corner coordinates

## Files

### Core Files
- `package.json` - Node.js project configuration
- `test-config.js` - Test cases and configuration
- `algorithms.js` - Inscribed rectangle algorithms
- `README.md` - This file

### Test Runners
- `browser-test-runner.js` - Browser-based test runner with visual output
- `compute-inscribed-rectangles.js` - Comprehensive test suite for all 251 combinations
- `compute-sample.js` - Test a specific combination
- `test-runner.js` - Legacy test harness

### Data Generation
- `compute-all-combinations.js` - Generate all valid room combinations
- `precompute-rectangles.js` - Generate precomputed rectangles JSON
- `extract-precomputed-rectangles.js` - Extract precomputed data from test results

### Analysis Tools
- `parameter-sweep.js` - Algorithm parameter tuning
- `analyze-breaches.js` - Analyze rectangle breaches
- `analyze-goals.js` - Analyze test goals
- `debug_collinear.js` - Debug collinear point detection

### Algorithm Modules
- `svgviewer-algorithms-extracted.js` - Core algorithms extracted from SvgViewerAlgorithms.js
- `algorithm-params.js` - Algorithm parameters
- `svgviewer-wrapper.js` - Wrapper for browser algorithms

### Output
- `valid-combinations.json` - All valid room combinations (251 total)
- `precomputed-rectangles.json` - Precomputed inscribed rectangles for all combinations
- `../TestResults/*.svg` - Visual output for each test case
- `../TestResults/results.txt` - Consolidated test results

## Algorithm Details

### Boundary Polygon Construction
1. **Winding Algorithm**: Traverses path segments to find outer boundary
2. **Duplicate Point Removal**: Removes consecutive duplicate vertices (tolerance: 0.1px)
3. **Collinear Point Removal**: Removes redundant points on straight lines using cross product detection (tolerance: 0.1)
4. **Polygon Validation**: Ensures minimum 3 vertices for valid polygon

### Inscribed Rectangle Calculation

The system uses adaptive algorithm selection based on polygon complexity:

#### Boundary-Based Algorithm (≤10 vertices)
- Fast, exact calculation for simpler polygons
- Uses edge-aligned rectangle fitting
- Typical execution time: 20-150ms

#### Optimized Algorithm (>10 vertices)
- Comprehensive search for complex polygons
- Grid-based sampling with angle rotation
- Typical execution time: 1000-3500ms

### Performance Optimization
- **Collinear point removal** reduces vertex count, triggering faster boundary-based algorithm
- 112 of 251 test cases use boundary-based (44.6%)
- 139 of 251 test cases use optimized (55.4%)

## Test Results Summary

- **Total combinations**: 251
- **All tests passing**: ✓
- **Average computation time**: 1194.6ms
- **Fastest**: 17.4ms
- **Slowest**: 3418.7ms

## Workflow

### Initial Setup
1. Generate valid combinations: `node compute-all-combinations.js`
2. Run comprehensive tests: `node browser-test-runner.js`
3. Extract precomputed rectangles: `node extract-precomputed-rectangles.js`

### Development Iteration
1. Modify algorithms in `SvgViewerAlgorithms.js`
2. Run tests: `node browser-test-runner.js`
3. Review visual output in `../TestResults/*.svg`
4. Check consolidated results in `../TestResults/results.txt`

### Deployment
1. Copy `precomputed-rectangles.json` to application wwwroot
2. Copy `valid-combinations.json` to application wwwroot
3. Rebuild and test live application
