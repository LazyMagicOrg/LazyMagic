# Exit Notes - Inscribed Rectangle Rotation Sensitivity Issue

**Date:** 2025-10-15
**Status:** In Progress - Paused for other priorities

## Current State

### Test Results (Latest - 2:30 PM)
- **Normal (unrotated) polygons:** 20/23 wins (87% win rate)
  - Optimized algorithm beats Boundary-Based on 20 tests
  - Boundary-Based wins/ties on 3 tests (Combo_0011, 0013, 0014)
  - Example: Combo_0001 finds 36,305 area vs BB's 33,003 (9.1% improvement)

- **Rotated (22.5°) polygons:** 7/23 wins (30% win rate) - NOT FIXED YET
  - Significant performance degradation when polygon is rotated
  - Example: Combo_0001 rotated finds 33,003 area (same as BB, no improvement)

### The Problem
The Optimized rectangle-fitting algorithm shows **rotation sensitivity** - it performs well on polygons in "normal" orientation but poorly when the same polygons are rotated 22.5°. This violates the principle that geometric algorithms should be **rotation-invariant**.

### What We've Fixed
1. ✅ **maxRadius calculation** (lines 728-737 in getMultipleCentroids)
   - Changed from using bounding box dimensions to calculating from vertex distances
   - This is rotation-invariant since vertex distances don't change with rotation

2. ✅ **Aligned grid centroid sampling** (lines 723-745 in getMultipleCentroids)
   - Uses fixed 12px step size aligned to absolute coordinates (0,0)
   - Grid points: `gridStartX = Math.ceil(minX / stepSize) * stepSize`
   - This ensures overlapping polygon regions test the same centroid points

3. ✅ **Directional distance calculation** (lines 1349-1419 in getDirectionalDistancesToEdge)
   - Uses ray-casting in 8 directions (N/S/E/W/NE/NW/SE/SW) from centroid
   - Calculates maxWidth/maxHeight from opposite direction pairs (E+W, N+S)
   - More accurate than simple bounding box dimensions

4. ✅ **Normal version performance restored**
   - Previous test: 19/23 wins
   - Current test: 20/23 wins (87% win rate)

### What Still Needs Fixing
The **rotated version still performs poorly** (7/23 wins, 30% win rate).

### Key Files
- **Main algorithm:** `wwwroot/SvgViewerOptimized.js`
  - `fastInscribedRectangle()` - Main entry point (lines 959-1327)
  - `getMultipleCentroids()` - Centroid generation with aligned grid (lines 653-824)
  - `tryRectangleAtAngle()` - Tests rectangle at specific angle (lines 1447-1715)
  - `getDirectionalDistancesToEdge()` - Ray-casting for sizing (lines 1349-1419)

- **Test results:**
  - Normal: `TestResultsNormal/results.txt` and `test-output.txt`
  - Rotated: `TestResultsRotated/results.txt` and `test-output.txt`

## Failed Approaches (DO NOT RETRY)

1. ❌ **Polar/radial grid centered on area centroid**
   - Tested rotation-invariant radial sampling at various angles
   - Result: Made normal version WORSE (dropped from 20 wins to 19 wins)
   - Reason: Different grid points for rotated polygons, not truly rotation-invariant

2. ❌ **Removing directional distances, using only bounding box**
   - Replaced `getDirectionalDistancesToEdge()` with simple bbox dimensions
   - Result: Made normal version WORSE (dropped to 17/23 wins)
   - Reason: Less accurate rectangle sizing

3. ❌ **4° angle increment pattern**
   - Changed base angle pattern from custom list to systematic 4° steps
   - Result: NO IMPROVEMENT for rotated version
   - Reason: Even with same angle values tested, rotated polygons produce different results

## The Core Mystery

**Even when testing the exact same angle values (0°, 4°, 8°, 12°, ..., 176°), the algorithm finds different quality rectangles for rotated vs normal polygons.**

Example (Combo_0001):
- Normal at 10°: Finds 36,305 area (optimal)
- Rotated at 10°: Finds 33,003 area (suboptimal, same as axis-aligned)

### Hypothesis
The issue may be in how `tryRectangleAtAngle()` works:
1. It rotates the polygon by the test angle around the centroid
2. Calculates an axis-aligned bounding box of the rotated polygon
3. Uses `getDirectionalDistancesToEdge()` with axis-aligned rays (E/W/N/S)

For a rotated polygon, even testing at the "correct" absolute angle may not work because:
- The polygon's edges are at different absolute angles
- The directional distances (E/W/N/S rays) measure different parts of the shape
- The optimal angle **relative to the polygon's own geometry** may differ from the optimal absolute angle

### Potential Next Steps (Not Attempted Yet)

1. **Angle normalization relative to polygon edges**
   - Instead of testing absolute angles (0°, 4°, 8°, ...), offset all angles by the polygon's dominant edge angle
   - For a polygon rotated 22.5°, test angles (22.5°, 26.5°, 30.5°, ...) to align with its own geometry
   - This may make the angle search rotation-invariant

2. **Investigate directional distances on rotated coordinate frame**
   - The `getDirectionalDistancesToEdge()` function uses axis-aligned rays
   - For a rotated polygon at angle 10°, the N/S/E/W rays may not align well with the polygon's structure
   - Consider using rotated directional rays aligned with the polygon's own axes

3. **Debug logging comparison**
   - Add detailed logging to compare:
     - What centroids are tested for normal vs rotated
     - What angles are tested at each centroid
     - What rectangles are found at each angle
     - Why rotated version rejects better rectangles
   - Look for differences in centroid selection, angle testing, or validation

## Code Architecture

### Centroid Generation (`getMultipleCentroids`)
Generates candidate rectangle center points using:
- Polylabel (pole of inaccessibility)
- Area centroid, vertex centroid, bbox center
- Polygon vertices and edge midpoints
- **Aligned grid** with 12px steps from origin (0,0)
- Offset patterns around pole and area centroid

### Rectangle Testing (`fastInscribedRectangle`)
For each centroid:
1. Calculate polygon edge angles
2. Generate strategic angles (edge angles + perpendiculars + 4° base pattern)
3. Test each angle with `tryRectangleAtAngle()`
4. Track best rectangle found

### Angle Testing (`tryRectangleAtAngle`)
For a given centroid and angle:
1. Rotate polygon by angle around centroid (forward rotation)
2. Calculate directional distances (E/W/N/S ray-casting)
3. Test multiple aspect ratios (0.5, 0.7, 1.0, 1.4, 2.0, ...)
4. Binary search to find largest scale that fits
5. Validate with `fastRectangleValidation()`
6. Return best rectangle for this angle

## User Feedback History
- "Nope you made the normal one worse, not the rotated one better." - About removing directional distances
- "Alright. We fixed the unrotated version, but didn't improve the rotated version." - About offset centroid fix
- "I think you are wrong, but to appease you, I ran another test. As you can see, no change." - About 4° angle increment
- "NO. WE ALREADY TRIED THAT AND IT DOESN"T WORK" - About trying bounding box dimensions again
- "No, it is worse than previous tests." - About polar/radial grid attempt

## Recommendations for Next Session

1. **Start with angle normalization** - This is the most promising unexplored approach
   - Offset all test angles by the polygon's dominant edge angle
   - May make the algorithm truly rotation-invariant

2. **Add comprehensive debug logging** - Before making changes
   - Log centroids tested, angles at each centroid, rectangles found
   - Compare normal vs rotated to identify the divergence point

3. **Don't retry failed approaches** - User has explicitly rejected:
   - Removing directional distances
   - Bounding box only (no directional distances)
   - Radial/polar grid

4. **Preserve normal version performance** - It's now at 87% win rate (20/23)
   - Any changes should improve rotated WITHOUT degrading normal
   - Test both versions after each change

## Success Criteria
- **Target:** 80%+ win rate on BOTH normal and rotated polygons (18-19+ wins out of 23)
- **Current:** 87% normal (20/23), 30% rotated (7/23)
- **Gap to close:** Improve rotated by ~50% win rate without degrading normal

## Files Requiring Updates

### Analysis Tools
1. **`test-harness/analyze-breaches.js`** - Path needs updating
   - Current: Expects `../TestResults/Combos.txt`
   - Should be: `../TestResultsNormal/Combos.txt` or `../TestResultsRotated/Combos.txt`
   - Also needs: Test runner to generate `Combos.txt` file with breached combination numbers
   - Status: Dormant tool, could be useful if breach analysis becomes relevant

2. **`test-harness/analyze-goals.js`** - Test naming and structure mismatch
   - Current: Expects `Test02`, `Test06`, `Test08`, etc. with goal rectangles in SVG
   - Should use: `Combo_0001`, `Combo_0002`, etc. (current naming convention)
   - Issue: All test-config.js entries have `goalRectangle: null` - no goals defined
   - SVG path: Points to wrong location (BlazorTest.WASM vs BlazorizeTest.WASM)
   - Status: Outdated, from previous testing methodology with manually-defined goal rectangles

### Test Runners
3. **`test-harness/browser-test-runner.js` + `test-page.html`** - Needs updating to current test format
   - Current: Uses old ballroom test cases and `SvgViewer.js`
   - Should use: Current Combo_XXXX format and test `SvgViewerOptimized.js`
   - Status: Functional browser-based test harness with Puppeteer, but not aligned with current rotation sensitivity testing
   - Value: Useful for browser-based debugging and screenshot capture

### Data Extraction Tools
4. **`test-harness/extract-precomputed-rectangles.js`** - Path needs updating
   - Current: Expects `../TestResults` (old structure)
   - Should be: `../TestResultsNormal` or `../TestResultsRotated` (current structure)
   - Purpose: Extracts rectangle data from SVG test results to generate `precomputed-rectangles.json`
   - Used by: `compute-all-combinations.js` and `compute-inscribed-rectangles.js`
   - Status: Outdated path, but precomputed-rectangles.json files exist in multiple locations (wwwroot, test-harness, bin folders)

### Documentation
5. **`test-harness/testharness.md`** - Outdated testing methodology documentation
   - Current: Documents Test01/Test02/Test03 with coverage percentage methodology
   - Current: References `TestResults/` directory and deleted analysis scripts
   - Should use: Combo_XXXX naming, TestResultsNormal/TestResultsRotated structure, rotation sensitivity focus
   - Valuable content: Algorithm explanations (lines 75-160) still conceptually valid
   - Status: Comprehensive but outdated documentation from parameter optimization era
   - Options: Delete (ExitNotes.md is primary docs) OR Update to current testing methodology
