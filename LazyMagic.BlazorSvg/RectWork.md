# Largest Inscribed Rectangle Algorithm - Work Log

## Problem Overview

The largest inscribed rectangle algorithm was not filling the available area in parallelogram-shaped polygons. The issue was identified through visual inspection showing a small rectangle inscribed in a long, rotated parallelogram when much more area was available.

## Root Cause Analysis

### Initial Implementation Issue

The algorithm was using **directional distance calculations** to determine rectangle sizing, but had a critical flaw in how it calculated the base dimension:

```javascript
// INCORRECT APPROACH (line 1351)
const baseDimension = dirDistances.maxDistance * 2.0; // Diameter of furthest extent
```

This assumed the rectangle could extend the maximum distance in **both directions** from the centroid, which is incorrect for non-symmetric shapes.

### Example from Debug Log

For a parallelogram with directional distances:
- East: 170.2
- West: 133.0
- South: 44.3
- North: 56.7

The old calculation gave:
- `baseDimension = 170.2 * 2.0 = 340.4`

This attempted to create rectangles up to 340.4 units wide, but the actual maximum width from the centroid is only:
- `maxWidth = East + West = 170.2 + 133.0 = 303.2`

The algorithm was testing rectangles that were too large, failing containment validation, and settling on smaller dimensions.

## Solution

### Fixed Dimension Calculation

Changed to calculate maximum width and height based on **opposite direction pairs**:

```javascript
// CORRECT APPROACH (lines 1351-1355)
const maxWidth = (dirDistances.distances['E'] || 0) + (dirDistances.distances['W'] || 0);
const maxHeight = (dirDistances.distances['N'] || 0) + (dirDistances.distances['S'] || 0);

// Use the larger dimension as base to ensure we can capture elongated shapes
const baseDimension = Math.max(maxWidth, maxHeight);
```

For the same parallelogram:
- `maxWidth = 170.2 + 133.0 = 303.2` (correct maximum)
- `maxHeight = 56.7 + 44.3 = 101.0` (correct maximum)
- `baseDimension = max(303.2, 101.0) = 303.2`

### Additional Bug Fix

Fixed a variable declaration order issue where debug logging referenced `aspectRatios` before it was declared:

```javascript
// Moved aspectRatios declaration BEFORE debug logging (line 1362)
const aspectRatios = [0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0];
```

## Implementation Details

### File Modified
`LazyMagic.BlazorSvg/wwwroot/SvgViewerOptimized.js`

### Function Modified
`tryRectangleAtAngle()` (lines 1294-1500+)

### Key Changes

1. **Lines 1351-1355**: Calculate `maxWidth` and `maxHeight` from opposite directional distances
2. **Line 1362**: Moved `aspectRatios` declaration before debug logging
3. **Lines 1365-1371**: Updated debug logging to show the new calculations

## Algorithm Flow

1. **Get Directional Distances**: Calculate distances from centroid to polygon edges in 8 directions (E, W, N, S, NE, NW, SE, SW)
2. **Calculate Maximum Dimensions**: Sum opposite directions to get true maximum width and height
3. **Test Aspect Ratios**: Try multiple aspect ratios (0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0)
4. **Binary Search**: For each aspect ratio, use binary search to find the largest valid scale
5. **Validate**: Check that all rectangle corners are inside the polygon
6. **Select Best**: Return the rectangle with the largest area

## Expected Results

With this fix, the algorithm should now:
- Properly utilize the available area in parallelogram shapes
- Find rectangles that extend correctly from the centroid based on actual edge distances
- Maintain the monotonic property for nested polygons (Shape 1 ⊆ Shape 2 → Area 1 ≤ Area 2)

## Testing

To verify the fix:
1. Test with the parallelogram shape (as shown in screenshots)
2. Verify rectangle fills most of the available area
3. Test with nested shapes (Shape 1 ⊆ Shape 2 ⊆ Shape 3) to confirm monotonic property
4. Check debug output shows correct `maxWidth` and `maxHeight` calculations

## Debug Output Format

When `debugMode = true` and testing angles 9° or 22°:

```
🔷 [RECT-DEBUG] >> Angle 9° rotated bounds: size 308.1×299.8
🔷 [RECT-DEBUG] >> Directional distances: min=44.3, max=170.2, H=133.0, V=44.3
🔷 [RECT-DEBUG] >> All directions: E=170.2, W=133.0, S=44.3, N=56.7, SE=94.0, SW=47.0, NE=60.1, NW=120.2
🔷 [RECT-DEBUG] >> maxWidth=303.2 (E+W), maxHeight=101.0 (N+S), baseDim=303.2
🔷 [RECT-DEBUG] >> Starting binary search with 7 aspect ratios
```

## Related Context

### Previous Session Issues
- **Monotonic Property Violation**: Nested polygons were producing inconsistent rectangle areas
- **Bounding Box Scaling**: Earlier versions used polygon bounding box dimensions, causing different rectangles at the same centroid/angle for nested shapes
- **Polylabel Integration**: The algorithm uses Polylabel for centroid selection combined with angle testing and grid sampling

### Algorithm Components
- **Polylabel**: Finds optimal centroid (pole of inaccessibility)
- **Grid Sampling**: Tests additional centroids on a 12-pixel grid
- **Angle Testing**: Tests rectangles at multiple rotation angles
- **Binary Search**: Finds largest valid rectangle at each centroid/angle combination
- **Fast Validation**: Uses edge sampling to validate rectangle containment

## Future Considerations

- Performance optimization if 620ms runtime is too slow
- Reduction of debug logging for production use
- Testing with more complex polygon shapes
- Verification of monotonic property with nested shapes