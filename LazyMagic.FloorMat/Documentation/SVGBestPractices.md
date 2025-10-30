# SVG Best Practices for FloorMat

This document outlines best practices for creating SVG files that work correctly with the FloorMat polygon merging algorithms.

## Table of Contents
- [T-Junction Issue](#t-junction-issue)
- [Winding Direction](#winding-direction)
- [Path Naming Conventions](#path-naming-conventions)
- [Coordinate Precision](#coordinate-precision)

---

## T-Junction Issue

### Problem Description

**T-junctions** occur when a vertex from one polygon touches the edge (not a vertex) of an adjacent polygon. This causes the `traverseOuterEdge()` algorithm in `SvgViewerAlgorithms.js` to fail when merging polygons.

### Symptoms

When T-junctions are present, you'll see:
- Duplicate vertices in the merged boundary polygon
- Visual "slices" cutting through the boundary polygon rendering
- Incorrect polygon traversal with backtracking paths
- Reduced fill ratios in layout calculations

**Example from Level2 PreFunction_0020:**
```
Polygon had 14 vertices with duplicates:
- p1 = p11 at (255.685, 10.352)
- p2 = p8 at (314.690, 11.387)

Visual artifacts:
- Boundary polygon renders with internal "slices"
- Path doubles back: p10→p11 (jumps up), p11→p12 (doubles back partway)
```

### Root Cause

The polygon merging algorithm expects **vertices to meet vertices**. When a vertex touches an edge mid-span:

```
Before (T-junction):
Polygon A:          Polygon B:
   v1────────v2        v3
   │          │        │
   │          │        │
   v5────────v6        v7────v8────v9
                            ↑
                            v2 touches here (no vertex at this point)
```

The algorithm arrives at `v2` from Polygon A and tries to continue the boundary traversal to Polygon B, but Polygon B has no vertex at that exact location on its edge `v7─v8`. The algorithm gets confused and backtracks, creating duplicate vertices.

### Solution: Add Collinear Vertices

Split edges at T-junction points by inserting collinear vertices:

```
After (collinear vertex added):
Polygon A:          Polygon B:
   v1────────v2        v3
   │          │        │
   │          │        │
   v5────────v6        v7────v10───v8────v9
                            ↑
                            v10 added at same coordinates as v2
```

Now both polygons have matching vertices at the junction point (v2 and v10 share the same coordinates), and the algorithm can properly traverse the boundary.

### Implementation Steps

1. **Identify T-junctions:**
   - Look for vertices from one section that lie on edges of adjacent sections
   - Check sections that will be merged together (defined in `Rooms.json`)

2. **Add collinear vertices:**
   - In Inkscape or your SVG editor:
     - Select the path with the edge that's being touched
     - Add a node at the exact coordinates where the T-junction occurs
     - Ensure the new node is collinear (doesn't change the edge shape)

3. **Verify coordinates:**
   - The inserted vertex must have **exactly** the same coordinates as the touching vertex
   - Use high precision (6+ decimal places) to avoid floating-point mismatches

### Real Example Fix

**Scenario:** PreFunction_IslaTerrace has a corner vertex at (255.685, 10.352) that touches the top edge of PreFunction_Capri.

**Before:** PreFunction_Capri top edge goes from (121.114, 16.045) directly to (314.690, 11.387)
```xml
<path d="m 121.11406,16.045024 c 134.57117,-5.693395 ... V 86.953682 H 121.11406 Z" />
```

**After:** Added vertex at (255.685, 10.352) along that top edge
```xml
<path d="m 121.11406,16.045024 L 255.685225,10.3516285 L 314.68951,11.386791 ... V 86.953682 H 121.11406 Z" />
```

Now both PreFunction_Capri and PreFunction_IslaTerrace have vertices at (255.685, 10.352), eliminating the T-junction.

### Verification

After adding collinear vertices, verify the fix:

1. **Run the FloorMat pipeline:**
   ```bash
   npm run process-external -- "path/to/level"
   ```

2. **Check output SVG files:**
   - Open generated files in `ComputedLayouts/*-MaxInscribedResults/`
   - Look at the boundary polygon (blue dashed line)
   - Verify no visual "slices" cutting through the polygon

3. **Check for duplicate vertices:**
   - Use the `check-duplicates.js` utility:
   ```bash
   node check-duplicates.js "<polygon points>"
   ```
   - Should report 0 duplicates

4. **Compare fill ratios:**
   - Fill ratios should improve on combinations that had T-junctions

### Why This Worked

- **Level1**: All sections were axis-aligned rectangles with vertices naturally meeting at corners (no T-junctions)
- **Level2**: Irregular polygon shapes with slanted edges created T-junctions where corners touched mid-edge
- **Fix**: Adding collinear vertices eliminated T-junctions, allowing proper boundary traversal

---

## Winding Direction

### Requirement

All section paths in the SVG must have **consistent winding direction** (all clockwise or all counter-clockwise).

### Why It Matters

The polygon merging algorithm uses winding direction to:
- Determine inside vs outside of polygons
- Mark shared edges as internal
- Calculate signed areas for polygon orientation

Mixed winding directions cause:
- Corrupted polygon detection (area ratio < 1.0)
- Incorrect polygon merging
- Self-intersecting boundaries

### Checking Winding Direction

Use the provided `analyze-winding.js` tool:

```bash
node analyze-winding.js "path/to/Level.svg"
```

Example output:
```
Section ID                          | Vertices | Signed Area    | Abs Area       | Winding | Status
────────────────────────────────────────────────────────────────────────────────────────────────────
PreFunction_Room_Capri              | 4        |    21340800.92 |    21340800.92 | CCW ↺   | ✓ OK
PreFunction_Room_Coral              | 4        |    21424608.44 |    21424608.44 | CCW ↺   | ✓ OK
PreFunction_Room_IslaTerrace        | 4        |    20335105.67 |    20335105.67 | CCW ↺   | ✓ OK
PreFunction_Room_Oceana             | 4        |   -29465857.31 |    29465857.31 | CW ↻    | ⚠ REVERSE
```

### Fixing Winding Direction

If a section has opposite winding from the majority:

1. **In Inkscape:**
   - Select the path
   - Go to Path → Reverse

2. **Verify the fix:**
   - Run `analyze-winding.js` again
   - All sections should show the same winding direction

---

## Path Naming Conventions

### Required Format

Section paths must follow this naming pattern:
```
<RoomName>_Room_<SectionName>
<RoomName>_Aisle_<SectionName>
```

Examples:
- `Ballroom_Room_1`
- `PreFunction_Room_Capri`
- `Ballroom_Aisle_34`

### Why It Matters

The FloorMat pipeline:
- Parses section IDs to match with `Rooms.json` configuration
- Groups sections by room name prefix
- Generates unique IDs based on naming pattern

Invalid names cause:
- Sections not being recognized
- Failed polygon merging
- Missing layouts in output

### Validation

Check that your SVG path IDs match the sections defined in `Rooms.json`:

```json
{
  "rooms": [
    {
      "roomName": "PreFunction",
      "sections": [
        "PreFunction_Room_Capri",
        "PreFunction_Room_Coral",
        "PreFunction_Room_IslaTerrace",
        "PreFunction_Room_Oceana"
      ]
    }
  ]
}
```

---

## Coordinate Precision

### Recommendation

Use **6+ decimal places** for coordinates to avoid floating-point comparison issues.

### Why It Matters

The polygon merging algorithm uses epsilon-based comparisons to detect:
- Coincident points (vertices that should be merged)
- Collinear vertices
- Shared edges

Low precision can cause:
- Vertices that should match being treated as different
- Edges not being detected as shared
- T-junctions not being properly resolved

### Example

**Good:**
```xml
<path d="m 314.689510,11.386791 c 78.201030,0.286271..." />
```

**Avoid:**
```xml
<path d="m 314.69,11.39 c 78.20,0.29..." />
```

### SVG Export Settings

When exporting from design tools:
- **Inkscape**: Use "Plain SVG" format with default precision
- **Adobe Illustrator**: Set decimal places to 6+
- **Figma**: Export as SVG with "Include ID attribute" enabled

---

## Summary Checklist

Before processing an SVG file through FloorMat:

- [ ] All paths have consistent winding direction (verified with `analyze-winding.js`)
- [ ] No T-junctions exist (vertices meet vertices, not vertices meeting edges)
- [ ] Collinear vertices added at all junction points
- [ ] Path IDs follow naming convention and match `Rooms.json`
- [ ] Coordinates use 6+ decimal places
- [ ] Sections that should merge are properly adjacent in the SVG

---

**Last Updated:** 2025-10-30
