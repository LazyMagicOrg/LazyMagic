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

- `package.json` - Node.js project configuration
- `test-config.js` - Test cases and configuration
- `test-runner.js` - Main test harness
- `algorithms.js` - Inscribed rectangle algorithms
- `README.md` - This file

## Algorithm Details

The harness tests these algorithms in order:

### 1. Parallelogram Detection (4 vertices)
- Checks if opposite edges are roughly equal (within 5px)
- Uses minimum edge length when edges differ >1px (trapezoid-like)
- Calculates exact rectangle dimensions from edge lengths

### 2. Trapezoid Detection (4 vertices)
- Finds parallel edge pairs (angles within 5°)
- Uses shorter parallel edge as rectangle width
- Calculates perpendicular height between parallel edges
- Positions rectangle centered on shorter edge

### 3. General Algorithm (any vertices)
- Not yet implemented - would use optimization approach

## Next Steps

1. Run tests to verify results match expected maximum rectangles
2. Add visual SVG output generation
3. Implement validation checks (gap/breach detection)
4. Add support for complex polygons (>4 vertices)
5. Generate comparison reports
