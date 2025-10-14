# Combo_0007 Boundary-Based Algorithm Analysis

## Problem Summary

For Combo_0007 (6-section complex shape), the boundary-based algorithm finds a suboptimal rectangle with area **38,802.6 sq px**, while the optimized algorithm finds **40,667.4 sq px** (4.8% larger).

## Visual Analysis

### Boundary Polygon (6 vertices)
- p0: (234.81, 32.26) - Top-left
- p1: (350.97, 32.26) - Top-middle
- p2: (458.20, 32.26) - Top-right
- p3: (458.20, 259.13) - Bottom-right
- p4: (350.97, 239.28) - Bottom-middle
- p5: (234.81, 225.87) - Bottom-left

### Optimized Rectangle (WINNER - Green)
- Area: **40,667.4 sq px**
- Dimensions: 209.5 × 194.1 pixels
- Angle: 90° (axis-aligned)
- Corners: (245.23, 32.96) to (454.77, 227.04)
- Top-left corner is **10.4px** from p0 horizontally, **0.7px** vertically
- **Excellent fit** - very close to optimal axis-aligned rectangle

### Boundary-Based Rectangle (LOSER - Red)
- Area: **38,802.6 sq px**
- Dimensions: ~220 × 176 pixels
- Angle: Slightly rotated (not axis-aligned)
- Corner b2: (237.87, 48.75)
- **Problem**: b2 is **16.5px below** p0's y-coordinate (48.75 vs 32.26)
- Missing ~16px of height at the top

## Root Cause Analysis

### Algorithm Flow

The boundary-based algorithm in `SvgViewerBoundaryBased.js`:

1. **Angle Selection** (lines 1623-1655):
   - Finds dominant angles from polygon edges
   - Tests top 6 angles + perpendiculars
   - **Always includes 0°** (line 1653) ✓

2. **Rectangle Search** (`findMaxRectangleAtAngle`, lines 673-1145):
   - Rotates polygon to align with test angle
   - For non-4-vertex shapes, uses centroid sampling
   - Tests multiple aspect ratios with binary search

3. **Binary Search** (lines 916-998):
   - For each centroid, finds max scale factor
   - Tests if rectangle corners are inside polygon
   - **KEY ISSUE**: Dimensions capped at rotated bounding box

### The Core Problem: Bounding Box Limitation

**Lines 918-932:**
```javascript
const baseDim = Math.max(width, height);  // width=223.4, height=226.9
let testW = baseDim * scale;
let testH = baseDim * scale / aspectRatio;

// PROBLEM: Caps dimensions at rotated bounding box
testW = Math.min(testW, width);   // ← Artificially limits width to 223.4
testH = Math.min(testH, height);  // ← Artificially limits height to 226.9
```

**Why this is wrong:**
- The **rotated bounding box** (223.4 × 226.9) is NOT the same as the **maximum inscribable rectangle**
- For Combo_0007, the optimal rectangle is **209.5 × 194.1** which is within the bounding box
- But the binary search is constrained by `scale ∈ [0, 1]`, and `baseDim * 1.0 = 226.9`
- With aspectRatio = 0.5: maxW = 226.9, maxH = 226.9/0.5 = 453.8
- After capping: W = min(226.9, 223.4) = **223.4**, H = min(453.8, 226.9) = **226.9**
- This gives only **50,695 sq px** max search space vs optimal **40,667 sq px**

Wait, that's actually LARGER than the optimal. Let me reconsider...

### Alternative Theory: Centroid Sampling

For Combo_0007:
- **6 vertices** → Uses "uniform" strategy (not "hybrid")
- Uniform strategy uses:
  - Base grid: 8×8 = 64 centroids
  - Dense grid: 20×20 = 400 centroids
  - Plus polygon centroid and 6 vertices
  - **Total: ~471 centroid positions**

**Possible issues:**
1. The optimal centroid (350, 130) might not be tested
2. The grid might miss the sweet spot where the largest rectangle fits
3. Edge sampling (5 points per edge) might reject valid rectangles due to floating point precision

### Most Likely Cause: Centroid Position

Looking at the optimized result:
- Best centroid: **(350, 130)** at angle 90°
- This position allows the rectangle to extend from near p0 (234.81, 32.26) down to ~227

The boundary-based algorithm likely:
1. **Tests wrong centroids** - The uniform grid with 20×20 spacing might place centroids at positions like (350, 140) or (350, 120), missing the optimal (350, 130)
2. **Uses wrong aspect ratio** - Testing aspectRatio = 0.5 (W=2H) when optimal is 209.5/194.1 ≈ **1.08**
3. **Binary search precision** - With scale resolution of 0.001, might stop at local maximum

### Verification

Looking at the adaptive aspect ratios (line 673):
```javascript
adaptiveAspectRatios = [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.5, 1.7, 2.0, 2.3]
```

The optimal ratio **1.08** falls between 1.0 and 1.2, so it should be refined in the second pass (lines 1062-1145).

## Recommended Fixes

### Option 1: Increase Centroid Density (Conservative)
- Change base grid from 8×8 to 12×12
- Change dense grid from 20×20 to 30×30
- **Cost**: +200 centroids (~900 total)
- **Benefit**: Better coverage, might find optimal position

### Option 2: Add Boundary-Aligned Centroids (Targeted)
- For each polygon vertex, add centroids at offsets like (±5px, ±5px), (±10px, ±10px)
- Specifically for Combo_0007, would add centroid near (350, 130) based on p0 position
- **Cost**: ~24 additional centroids (6 vertices × 4 offsets)
- **Benefit**: Finds rectangles well-aligned to boundary vertices

### Option 3: Improve Edge-Based Algorithm for N>4 (Best)
- Currently, `findRectangleFromEdge` only runs for 4-vertex polygons (line 689)
- Extend it to work with N-vertex polygons:
  - For each edge, try placing rectangle with that edge as one side
  - Binary search for maximum perpendicular distance
  - Test all major edges (sorted by length)
- **Cost**: More computation (test 6 edges for Combo_0007)
- **Benefit**: Guaranteed to find edge-aligned solutions like the optimized algorithm did

### Option 4: Fix Aspect Ratio Refinement
- The second pass refinement (lines 1062-1145) should use finer steps
- Current: Tests midpoints between adjacent ratios
- Better: Binary search between adjacent ratios with 0.05 precision
- **Cost**: Minimal (same number of aspect ratios tested)
- **Benefit**: Finds more precise optimal ratio

## Recommended Action

**Implement Option 3**: Extend `findRectangleFromEdge` to work with N>4 vertex polygons.

This would:
1. Test each of the 6 edges as potential rectangle sides
2. For the top edge (p0→p1→p2), it would find maximum perpendicular distance
3. Naturally discover the optimal rectangle extending from near p0 downward
4. Match the optimized algorithm's edge-focused approach
5. Only add ~50-100ms to computation time (acceptable for 6-section shapes)

The code change would be minimal - remove or relax the `if (rotated.length === 4)` check at line 689 and ensure `findRectangleFromEdge` handles arbitrary edge configurations.
