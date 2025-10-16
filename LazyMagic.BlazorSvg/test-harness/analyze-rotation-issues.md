# Analysis: Rotation Invariance Issues in Boundary-Based Algorithm

## Executive Summary

After investigating the 12 most problematic test cases (those with area differences >= 100 sq px), I've identified the root cause of the rotation invariance problem.

**Key Finding**: All 12 problematic cases have identical vertex counts between normal and rotated versions, but produce **drastically different rectangle dimensions and areas**. This proves the issue is NOT in the polygon winding/cleanup logic, but rather in the **boundary-based rectangle fitting algorithm itself**.

## Detailed Findings

### Pattern #1: Dimension Mismatch Despite Identical Boundaries

**Combo_0017** (Worst case: 44% area difference)
- Normal: 243.2 × 194.0 at 0° → Area: 47,183 sq px
- Rotated: 195.1 × 195.1 at 117.1° → Area: 26,335 sq px
- **Both have exactly 12 vertices**
- **Problem**: The rotated version found a nearly square rectangle (195×195), while the normal version found an elongated one (243×194)

**Combo_0038** (Second worst: 40% area difference)
- Normal: 223.4 × 194.0 at 0° → Area: 43,331 sq px
- Rotated: 193.5 × 193.5 at 120° → Area: 25,914 sq px
- **Both have exactly 12 vertices**
- **Problem**: Again, rotated version found a square-ish rectangle instead of elongated one

**Combo_0006** (9.4% area difference)
- Normal: 243.2 × 194.0 at 180° → Area: 47,183 sq px
- Rotated: 95.8 × 445.9 at 0° → Area: 42,728 sq px
- **Both have exactly 14 vertices**
- **Problem**: Completely different aspect ratios (1.25 vs 4.66)!

**Combo_0025** (13.2% area difference)
- Normal: 97.8 × 223.7 at 90° → Area: 21,876 sq px
- Rotated: 77.9 × 243.7 at 73.8° → Area: 18,991 sq px
- **Both have exactly 10 vertices**
- **Problem**: Different dimensions despite same boundary shape

### Pattern #2: Angle Derivation Issues

The boundary-based algorithm derives angles from polygon edges. Looking at the angles found:

| Test Case | Normal Angle | Rotated Angle | Notes |
|-----------|-------------|---------------|-------|
| Combo_0017 | 0° | 117.1° | Very different angles for same logical shape |
| Combo_0038 | 0° | 120° | Very different angles for same logical shape |
| Combo_0006 | 180° | 0° | 180° difference (equivalent but shows instability) |
| Combo_0025 | 90° | 73.8° | 16.2° difference for same logical shape |

**Observation**: When the coordinate system is rotated 45°, the algorithm is picking different edge angles as the basis for rectangle orientation. This causes it to find rectangles at completely different orientations relative to the boundary polygon.

### Pattern #3: Optimal Algorithm Selection Differs

In several cases, the **rotated version picks the optimized algorithm** as better, while the **normal version picks boundary-based**:

| Test Case | Normal Best | Rotated Best | Normal Area | Rotated Area (Optimized) |
|-----------|-------------|--------------|-------------|--------------------------|
| Combo_0017 | Boundary-based (47,183) | **Optimized (38,077)** | 47,183 | 38,077 (better than boundary's 26,336!) |
| Combo_0038 | Boundary-based (43,331) | **Optimized (37,449)** | 43,331 | 37,449 (better than boundary's 25,914!) |

**This is telling**: When the boundary-based algorithm fails on rotated geometry, the optimized algorithm actually finds a better solution. This suggests the boundary-based algorithm is fundamentally flawed in how it handles non-axis-aligned coordinate systems.

## Root Cause Analysis

### The Problem: Edge Angle Selection

The boundary-based algorithm in `SvgViewerAlgorithms.js` (around line 150-250) works as follows:

1. **Extract angles from polygon edges** (lines ~200-220)
2. **Test rectangles at those angles** (lines ~220-250)
3. **Find the largest inscribed rectangle at each angle**

**The bug**: When the coordinate system is rotated, the edges of the same logical polygon are described by different angle values. For example:
- A horizontal edge at 0° in normal coordinates
- Becomes a 45° edge in rotated coordinates

The algorithm treats these as **completely different test cases**, leading to:
- Different angles being tested
- Different rectangle orientations being found
- Drastically different final areas

### Why This Happens

Looking at the code in `findBoundaryBasedInscribedRectangle()`:

```javascript
// Extract unique angles from boundary edges
const angles = new Set();
for (let i = 0; i < boundary.length; i++) {
    const p1 = boundary[i];
    const p2 = boundary[(i + 1) % boundary.length];
    const angle = Math.atan2(p2.y - p1.y, p2.x - p1.x) * 180 / Math.PI;
    angles.add(angle);
}
```

This code extracts angles **in the current coordinate system**. When you rotate the entire SVG 45°:
- A 0° edge becomes a 45° edge
- A 90° edge becomes a 135° edge
- etc.

The algorithm then tests rectangles at these angles, but since the angles themselves are different, it finds different rectangles!

## Why Disabling Collinear Removal Helped

Disabling collinear point removal improved results from 34% matching to 66% acceptable because:

1. **It ensured consistent vertex counts** between normal and rotated versions
2. **It preserved more geometric information** that sometimes helps the algorithm find better rectangles
3. **But it didn't fix the fundamental angle derivation problem**

The 33.5% of cases that still have significant differences (>= 10 sq px) all exhibit this angle selection issue.

## Recommendations

### Option 1: Normalize Angles (Quick Fix)
Modify the boundary-based algorithm to normalize all angles to a canonical range before testing. For example, treat 0°, 90°, 180°, 270° as equivalent to their rotated counterparts.

### Option 2: Test All Angles (Comprehensive Fix)
Instead of deriving angles from edges, test a fixed set of angles (e.g., 0° to 180° in 1° increments). This would make the algorithm truly rotation-invariant but slower.

### Option 3: Use the Optimized Algorithm (Pragmatic Fix)
The optimized algorithm appears to be more rotation-invariant. Consider:
- Making it the default
- Only using boundary-based as a fallback
- Or improving the optimized algorithm's performance

### Option 4: Fix Angle Derivation (Correct Fix)
Transform edge angles to be invariant under rotation. This requires:
1. Computing angles relative to the polygon's orientation (e.g., relative to its principal axis)
2. Or deriving angles from geometric properties that don't change under rotation

## Test Case Summary

All 12 problematic cases share these characteristics:
- ✓ Identical vertex counts (rotation invariant)
- ✗ Different rectangle dimensions (rotation dependent)
- ✗ Different angles selected (rotation dependent)
- ✗ Different final areas (rotation dependent)

This confirms the boundary-based algorithm's angle selection is the root cause.

## Root Cause Identified

**Location**: `SvgViewerBoundaryBased.js`, lines 56-58 in `extractBoundaryEdges()` function

```javascript
// Calculate angle of this edge (in degrees, 0-180 range)
let angle = Math.atan2(dy, dx) * 180 / Math.PI;
if (angle < 0) angle += 180; // Normalize to 0-180
```

**The Problem**: This code extracts edge angles **in the current coordinate system**, not relative to the polygon's intrinsic geometry. When the entire SVG is rotated 45°:

1. **Normal coordinate system**: A horizontal edge has angle = 0°
2. **Rotated coordinate system (45°)**: The same logical edge now has angle = 45°

The boundary-based algorithm then uses these angles to test rectangle orientations. Since the angles are different, it tests **completely different rectangles** for the same logical shape.

### Concrete Example from Combo_0017

**Normal (axis-aligned)**:
- Edges are at 0°, 90°, 180°, 270° in the coordinate system
- Algorithm tests rectangles at these angles
- Finds best: 243.2 × 194.0 at 0° → 47,183 sq px

**Rotated (45° rotation)**:
- Same logical edges are now at 45°, 135°, 225°, 315° in the rotated coordinate system
- Algorithm tests rectangles at THESE angles
- Finds best: 195.1 × 195.1 at 117.1° → 26,336 sq px

**Result**: 44% area difference for the same logical polygon shape!

## Why This Matters

The boundary-based algorithm is supposed to be **geometry-driven**, finding the best rectangle based on the polygon's **shape**. But instead, it's **coordinate-system-driven**, finding different rectangles based on how that shape is described in x/y coordinates.

This violates a fundamental principle: **geometric algorithms should be invariant under coordinate transformations** (rotation, translation, scaling).

## Solution Approaches

### Option 1: Canonical Angle Representation (Recommended)
Instead of using absolute angles in the coordinate system, compute angles **relative to the polygon's principal axis**:

```javascript
function extractBoundaryEdges(polygon) {
    // First, compute the polygon's principal axis
    const principalAngle = computePrincipalAxis(polygon);

    const edges = [];
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];

        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const length = Math.sqrt(dx * dx + dy * dy);

        // Calculate angle RELATIVE to principal axis
        let absoluteAngle = Math.atan2(dy, dx) * 180 / Math.PI;
        let angle = absoluteAngle - principalAngle;

        // Normalize to 0-180
        while (angle < 0) angle += 180;
        while (angle >= 180) angle -= 180;

        edges.push({ p1, p2, length, angle, dx, dy, index: i });
    }

    return edges;
}

function computePrincipalAxis(polygon) {
    // Use PCA (Principal Component Analysis) or
    // Find the longest edge and use its angle
    // This gives a rotation-invariant reference direction

    let maxLength = 0;
    let principalAngle = 0;

    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];
        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const length = Math.sqrt(dx * dx + dy * dy);

        if (length > maxLength) {
            maxLength = length;
            principalAngle = Math.atan2(dy, dx) * 180 / Math.PI;
        }
    }

    return principalAngle;
}
```

### Option 2: Test Comprehensive Angle Range
Instead of deriving angles from edges, test ALL angles systematically:

```javascript
// Test every degree from 0-180
const testAngles = [];
for (let angle = 0; angle < 180; angle++) {
    testAngles.push(angle);
}
// Then test rectangles at all these angles
```

**Pros**: Guaranteed rotation-invariant
**Cons**: Much slower (180× more tests)

### Option 3: Hybrid Approach
Use Option 1 for fast testing, with Option 2 as a refinement step:

```javascript
// Phase 1: Quick test using edge-derived angles (rotation-invariant)
const candidates = testBoundaryAngles(polygon);

// Phase 2: Refine around best candidates
const best = refineBestCandidates(polygon, candidates, angleStep=1);
```

## Files for Further Investigation

All debug logs and summaries have been extracted to:
- `debug-extracts/{combo}_normal.txt` - Full debug output for normal test
- `debug-extracts/{combo}_rotated.txt` - Full debug output for rotated test
- `debug-extracts/{combo}_summary.txt` - Side-by-side comparison

**Key file to modify**: `wwwroot/SvgViewerBoundaryBased.js`
- Function: `extractBoundaryEdges()` (lines 45-75)
- Issue: Edge angle calculation (lines 56-58)
- Fix location: Add principal axis computation and relative angle calculation
