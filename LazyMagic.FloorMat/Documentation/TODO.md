# FloorMat TODO

## Critical Priority

### Polygon Merging T-Junction Issue (RESOLVED)

**Status:** Resolved
**Date Resolved:** 2025-10-30

**Issue:**
Polygon merging created duplicate vertices and visual "slices" in boundary polygons due to T-junctions in SVG geometry.

**Solution:**
Added collinear vertices at T-junction points where one polygon's vertex touches another polygon's edge. See `SVGBestPractices.md` for detailed documentation.

---

## High Priority

### Migrate Boardroom and HollowSquare to Hybrid Algorithm

**Status:** Not Started
**Priority:** High
**Date Added:** 2025-10-28

**Issue:**
Currently, the three layout algorithms use inconsistent implementations:
- ✅ **MaxInscribed**: Uses `runHybridWithConstraints()` (BoundaryBased + Optimized)
- ❌ **Boardroom**: Uses old `boardroom.findBoardroomLayout()` from SvgViewerBoardroom.js
- ❌ **HollowSquare**: Uses old `unifiedAlgo.findInscribedRectangle()` from SvgViewerInscribedRect.js

**Goal:**
All three algorithms should use the hybrid approach (BoundaryBased + Optimized) with different dimension constraints.

**Location:**
`LazyMagic.FloorMat\FloorMat\run-tests.js` lines 602-619

**Current Code:**
```javascript
if (testConfig.algorithm === 'maxinscribed') {
    layout = runHybridWithConstraints(polygon, testConfig.algorithmOptions, testConfig.algorithm);
} else if (testConfig.algorithm === 'boardroom') {
    layout = boardroom.findBoardroomLayout(polygon, options);  // ❌ Uses old algorithm
} else {
    layout = unifiedAlgo.findInscribedRectangle(polygon, testConfig.algorithmOptions);  // ❌ Uses old algorithm
}
```

**Target Code:**
```javascript
if (testConfig.algorithm === 'maxinscribed') {
    layout = runHybridWithConstraints(polygon, testConfig.algorithmOptions, testConfig.algorithm);
} else if (testConfig.algorithm === 'boardroom') {
    layout = runHybridWithConstraints(polygon, testConfig.algorithmOptions, testConfig.algorithm);
} else {
    layout = runHybridWithConstraints(polygon, testConfig.algorithmOptions, testConfig.algorithm);
}
```

**Benefits:**
1. **Consistency**: All algorithms use the same high-performance hybrid approach
2. **Performance**: BoundaryBased + Optimized is faster and more accurate than old algorithms
3. **Maintainability**: Only need to maintain one algorithm implementation
4. **Code Cleanup**: Can potentially delete obsolete algorithm files after migration

**Files to Remove After Migration:**
- `LazyMagic.BlazorSvg\wwwroot\SvgViewerBoardroom.js` (if only used by run-tests.js)
- `LazyMagic.BlazorSvg\wwwroot\SvgViewerInscribedRect.js` (if only used by run-tests.js)
- `LazyMagic.BlazorSvg\wwwroot\SvgViewerHollowSquare.js` (already unused)

**Testing Required:**
- Run full test suite (251+ combinations × 3 algorithms)
- Verify Boardroom layouts still meet 13ft width constraint
- Verify HollowSquare layouts meet discrete dimension constraints (base 19×14, increment 6ft)
- Compare output quality (fill ratios) between old and new implementations

**Dependencies:**
- Ensure `convertConstraintsToParams()` function properly handles Boardroom and HollowSquare constraint types
- May need to update constraint validation in `checkDimensionValue()` function

---

## Medium Priority

### Multi-Room Processing Documentation

**Status:** Completed (2025-10-28)
**Priority:** Medium

Document the new multi-room processing capability added in v2.1:
- ✅ FloorMat-map.md updated
- ✅ FloorMat-Instructions.md updated
- ✅ All rooms in a level are now processed independently
- ✅ Global sequential IDs with room prefixes (e.g., Ballroom_0001, Foyer_0252)

---

## Low Priority

### Code Cleanup

**Files identified as potentially obsolete:**
- `LazyMagic.BlazorSvg\wwwroot\SvgViewerHollowSquare.js` - Not referenced anywhere
- Review other SvgViewer*.js files after hybrid migration is complete

---

## Future Enhancements

### External Directory Processing Improvements
- Add support for processing multiple levels in one command
- Add validation for Rooms.json structure before processing
- Add progress indicators for long-running operations

### Performance Optimization
- Consider parallel processing of combinations (currently sequential)
- Optimize console.log suppression mechanism
- Cache polygon parsing results for repeated tests

---

**Last Updated:** 2025-10-28
