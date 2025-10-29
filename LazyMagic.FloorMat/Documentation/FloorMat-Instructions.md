# FloorMat - Quick Start Instructions

Generate precomputed layout data for SVG floor plans.

---

## Quick Start (Two Options)

### Option 1: Process Internal Projects (FloorMat/input/)

#### Step 1: Prepare Your Files

You need two files from the venue:

1. **`[VenueName].svg`** - The floor plan SVG
2. **`[VenueName]-Rooms.json`** - Graph connectivity data (sections and joins)

Example for "Level1" venue:
- `Level1.svg`
- `Level1-Rooms.json`

#### Step 2: Place Files in Input Directory

```bash
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.FloorMat\FloorMat

# Your input directory should look like:
# input/
# ├── Level1.svg
# └── Level1-Rooms.json
```

#### Step 3: Run the Pipeline

```bash
npm run process
```

**That's it!** The pipeline will:
- Generate 251 valid room combinations (using 10 validation rules)
- Run MaxInscribed, Boardroom, and Hollow Square layout algorithms
- Extract precomputed data
- Embed data into final SVG

---

### Option 2: Process External Projects (Any Directory)

#### Step 1: Prepare Your Files

Create your project directory anywhere with this structure:

```
C:\path\to\your\venue\
├── input/
│   ├── [VenueName].svg
│   └── [VenueName]-Rooms.json
└── output/  (created automatically)
```

Example:
```
C:\BCProjects\BCTenancies\bcs-cerulean\FloorMat\Level1\
├── input/
│   ├── Level1.svg
│   └── Level1-Rooms.json
```

#### Step 2: Run the Pipeline from FloorMat Directory

**Important:** You must run the command from the FloorMat directory, not from your project directory.

```bash
# Navigate to FloorMat directory
cd C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.FloorMat\FloorMat

# Process external directory
npm run process-external "C:\path\to\your\venue"
```

Example:
```bash
npm run process-external "C:\BCProjects\BCTenancies\bcs-cerulean\FloorMat\Level1"
```

**That's it!** The pipeline will:
- Auto-detect the SVG in your external input/ folder
- Generate 251 valid room combinations
- Run all layout algorithms
- Output everything to your external output/ folder

---

## Output

### Option 1 Output (Internal)
Everything is generated in `FloorMat/output/[VenueName]-output/`:

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

**The file you want:** `FloorMat/output/Level1-output/Level1-output.svg`

### Option 2 Output (External)
Everything is generated in your external directory's `output/[VenueName]-output/`:

```
C:\path\to\your\venue\output\Level1-output\
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

**The file you want:** `C:\path\to\your\venue\output\Level1-output\Level1-output.svg`

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

### Error: "No SVG files found in [path]"
**For external processing:**
- Make sure you have an SVG file in the `input/` folder of your target directory
- Check that the file extension is `.svg` (lowercase)
- The script will show: "Please provide an SVG file and run the command again"

### Error: "Rooms file not found"
- Make sure your Rooms.json file follows the naming pattern: `[VenueName]-Rooms.json`
- Check that it's in the `input/` directory
- For external processing: Verify the `input/` folder exists in your target directory

### Error: "Failed to generate combinations"
- Verify `[VenueName]-Rooms.json` is valid JSON
- Check that it contains `Level1.Rooms.Ballroom.RoomSections` and `Joins`

### Error: "Target directory does not exist"
**For external processing:**
- Make sure the path you provided exists
- Use absolute paths (e.g., `C:\full\path\to\directory`)
- Check for typos in the directory path

### Tests taking too long
- Expected runtime: ~30-60 minutes for all 251 combinations × 3 algorithms
- For testing: Use sample configs in `test-configs/*-sample.json`

### Need different venue?
**Option 1 (Internal):**
- Just change the file names in `FloorMat/input/`
- `Cerulean.svg` + `Cerulean-Rooms.json` → outputs to `output/Cerulean-output/`
- Pipeline auto-detects all projects in `input/` and processes them

**Option 2 (External):**
- Create a new directory structure anywhere
- Use `npm run process-external "C:\path\to\new\venue"`
- Each venue can have its own directory

---

## More Information

- **Complete Documentation:** `FloorMat/FloorMat-map.md`
- **Algorithm Details:** See other `.md` files in `Documentation/`
- **Validation Rules:** `InscribedRectangle-Guide.md` (Rules 1-10)
