# Run Boardroom Layout Test

Quick reference for generating boardroom layout visualizations.

## Command

```bash
cd "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\test-harness"
node test-runner-boardroom.js
```

## What This Does

Generates 251 SVG files showing boardroom layouts for all valid combinations:

- **Output Directory:** `LazyMagic.BlazorSvg/BoardroomResults/`
- **Files:** `Combo_0001.svg` through `Combo_0251.svg`
- **Summary:** `boardroom-summary.json`
- **Duration:** ~15-20 minutes

## Output Details

Each SVG file contains:

- **White background** with comparison table (matching original aesthetic)
- **Test paths** (gray, 30% opacity)
- **Boundary polygon** (blue, dashed)
- **Boardroom rectangle** (orange fill, solid orange border)
- **Centroid marker** (orange circle with "boardroom" label)
- **Corner markers** (labeled br0, br1, br2, br3)
- **Polygon vertices** (labeled p0, p1, p2, etc.)
- **Data table** showing:
  - Sets/Tables (e.g., "2 sets (4 tables)")
  - Dimensions (e.g., "13.0 × 20.0 ft")
  - Boardroom Area (square inches)
  - Polygon Area (square inches)
  - Fill Ratio (percentage)

## Boardroom Constraints

- **Fixed width:** 13 ft
- **Variable length:** 14, 20, 26, 32... ft (adds 6 ft per set)
- **Sets:** 1 set = 2 tables, 2 sets = 4 tables, etc.
- **Rotation:** Can be at any angle

## Color Scheme

- **Boundary polygon:** Blue (#4080ff)
- **Boardroom layout:** Orange (#ff8800, #ff6600)
- **Centroid:** Orange (#ff8800)
- **Corners:** Orange (#ff8800)
- **Vertices:** Blue (#4080ff)

## Viewing Results

Open any SVG file in a browser or SVG viewer:

```bash
# Example: View first result
start BoardroomResults/Combo_0001.svg
```

Or navigate to the directory and open files manually:
```
C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.BlazorSvg\BoardroomResults\
```

## Troubleshooting

### If test runner fails:

1. Check that `SvgViewerBoardroom.js` exists in `wwwroot/`
2. Verify `valid-combinations.json` exists in `test-harness/`
3. Check Node.js is installed: `node --version`
4. Make sure you're in the correct directory

### If no layouts found:

- Polygons may be too small for 13×14 ft minimum
- Check console output for error messages
- Review `boardroom-summary.json` for details

## Next Steps (After Test Completion)

Once you're satisfied with the results:

1. Run extraction: `node extract-precomputed-boardroom.js` (~30 sec)
2. Embed in SVG: `node embed-boardroom-in-svg.js` (~5 sec)
3. Test in application

But for now, just generate and review the visualizations!
