# FloorMat - Quick Start Instructions

Generate precomputed layout data for SVG floor plans.

---

## Quick Start

### Step 1: Prepare Your Files

You need two files from the venue:

1. **`[VenueName].svg`** - The floor plan SVG
2. **`[VenueName]-Rooms.json`** - Graph connectivity data (sections and joins)

Example for "Level1" venue:
- `Level1.svg`
- `Level1-Rooms.json`

### Step 2: Place Files in Input Directory

```bash
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.FloorMat\FloorMat

# Your input directory should look like:
# input/
# ├── Level1.svg
# └── Level1-Rooms.json
```

### Step 3: Run the Pipeline

```bash
npm run process
```

**That's it!** The pipeline will:
- Generate 251 valid room combinations (using 10 validation rules)
- Run MaxInscribed, Boardroom, and Hollow Square layout algorithms
- Extract precomputed data
- Embed data into final SVG

---

## Output

Everything is generated in `output/[VenueName]-output/`:

```
output/Level1-output/
├── Level1-valid-combinations.json    # All valid room combinations
├── ComputedLayouts/                  # Raw algorithm results
│   ├── Level1-MaxInscribedResults/
│   ├── Level1-BoardroomResults/
│   └── Level1-HollowSquareResults/
├── Level1-rectangles.json            # Precomputed rectangle data
├── Level1-boardroom.json             # Precomputed boardroom data
├── Level1-hollowsquare.json          # Precomputed hollow square data
└── Level1-output.svg                 # ← FINAL FILE: SVG with embedded data
```

**The file you want:** `output/Level1-output/Level1-output.svg`

---

## Manual Step-by-Step (Optional)

If you prefer granular control, you can run each stage separately:

### Stage 1: Generate Valid Combinations

```bash
node compute-all-combinations.js Level1
```

Output: `output/Level1-output/Level1-valid-combinations.json`

### Stage 2: Run Layout Tests

```bash
# MaxInscribed
node run-tests.js test-configs/maxinscribed-full.json

# Boardroom
node run-tests.js test-configs/boardroom-full.json

# Hollow Square
node run-tests.js test-configs/hollowsquare-full.json
```

### Stage 3: Extract Precomputed Data

```bash
node extract-precomputed-project.js "Level1" "output/Level1-output/ComputedLayouts" "output/Level1-output/Level1-valid-combinations.json" "output/Level1-output"
```

### Stage 4: Embed into SVG

```bash
node embed-project.js "Level1" "input/Level1.svg" "output/Level1-output"
```

---

## Troubleshooting

### Error: "Rooms file not found"
- Make sure your Rooms.json file follows the naming pattern: `[VenueName]-Rooms.json`
- Check that it's in the `input/` directory

### Error: "Failed to generate combinations"
- Verify `[VenueName]-Rooms.json` is valid JSON
- Check that it contains `Level1.Rooms.Ballroom.RoomSections` and `Joins`

### Tests taking too long
- Expected runtime: ~30-60 minutes for all 251 combinations × 3 algorithms
- For testing: Use sample configs in `test-configs/*-sample.json`

### Need different venue?
Just change the file names:
- `Cerulean.svg` + `Cerulean-Rooms.json` → outputs to `output/Cerulean-output/`
- Pipeline auto-detects all projects in `input/` and processes them

---

## More Information

- **Complete Documentation:** `FloorMat/FloorMat-map.md`
- **Algorithm Details:** See other `.md` files in `Documentation/`
- **Validation Rules:** `InscribedRectangle-Guide.md` (Rules 1-10)
