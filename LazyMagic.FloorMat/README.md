# FloorMat - SVG Floor Plan Layout Generation System

FloorMat is a build-time pipeline system that generates optimal geometric layouts for SVG floor plans. It computes and embeds precomputed layout data directly into SVG files for instant runtime lookup.

## Features

- **Max-Inscribed Rectangle Algorithm**: Finds the largest rectangle that fits within irregular polygon boundaries using hybrid binary search and centroid sampling
- **Boardroom Layout Generator**: Automatically places furniture (tables, chairs) within complex room geometries
- **Hollow Square Layout Generator**: Creates hollow square arrangements with configurable dimensions
- **Multi-Project Pipeline**: Process multiple floor plans in a single run with auto-detection
- **External Directory Support**: Process projects outside the FloorMat directory structure
- **Combination Generation**: Generates all valid room combinations based on connectivity data (251 combinations for typical projects)
- **Embedded Data Pattern**: Precomputed layouts embedded in SVG `<script>` tags for instant browser access

## Quick Start

### 1. Install Dependencies

```bash
cd LazyMagic.FloorMat/FloorMat
npm install
```

### 2. Prepare Your Project

Create your project directory with:
- `{ProjectName}.svg` - Your floor plan SVG file
- `{ProjectName}-data.json` - Room connectivity and metadata

Place these in either:
- `FloorMat/input/` (internal projects)
- Any external directory (external projects)

### 3. Run the Pipeline

**For internal projects:**
```bash
npm run process
```

**For external projects:**
```bash
npm run process-external "C:\path\to\your\project\directory"
```

The pipeline will generate:
- Valid room combinations
- Max-inscribed rectangle layouts
- Boardroom furniture layouts
- Hollow square layouts
- Final SVG with embedded precomputed data at `output/{ProjectName}-output.svg`

## Documentation

### Quick References
- [FloorMat Instructions](Documentation/FloorMat-Instructions.md) - Getting started guide
- [Command Reference](Documentation/EmbedData-QuickRef.md) - Quick command cheatsheet
- [Directory Map](FloorMat/FloorMat-map.md) - Complete file structure

### Architecture & System Design
- [System Architecture Guide](Documentation/InscribedRectangle-Guide.md) - Complete system overview
- [Algorithm Implementation Details](Documentation/Algorithms-Implementation.md) - Low-level algorithm documentation

### Pipeline Documentation
- [Max-Inscribed Pipeline](Documentation/MaxInscribedLayoutPipeline.md) - Largest rectangle algorithm
- [Boardroom Pipeline](Documentation/BoardroomLayoutPipeline.md) - Furniture placement system
- [Hollow Square Pipeline](Documentation/HollowSquareLayoutPipeline.md) - Hollow square layout generator

### Data & Authoring
- [Rooms JSON Schema](Documentation/RoomsJsonSchema.md) - Data file format specification
- [SVG Best Practices](Documentation/SVGBestPractices.md) - SVG authoring guidelines

### Development
- [TODO](Documentation/TODO.md) - Development roadmap and outstanding tasks

## Project Structure

```
LazyMagic.FloorMat/
├── README.md (this file)
├── Documentation/          # Comprehensive documentation
└── FloorMat/               # Pipeline implementation
    ├── input/              # Internal project files
    ├── output/             # Generated output files
    ├── *.cjs               # Algorithm implementations (CommonJS)
    ├── process-all.js      # Internal multi-project orchestrator
    ├── process-external.js # External directory processor
    └── run-tests.js        # Unified test runner
```

## Algorithm Files

FloorMat uses CommonJS modules (`.cjs`) for Node.js compatibility:

- `kdtree.cjs` - Spatial data structures (KDTree, SpatialGrid)
- `SvgViewerBoundaryBased.cjs` - Fast edge-aligned rectangle search
- `SvgViewerOptimized.cjs` - Grid-based centroid sampling with binary search
- `SvgViewerInscribedRect.cjs` - Unified inscribed rectangle algorithm
- `SvgViewerBoardroom.cjs` - Boardroom-specific layout algorithm
- `SvgViewerHollowSquare.cjs` - Hollow square layout algorithm
- `SvgViewerAlgorithms.cjs` - SVG path parsing utilities

## Runtime Integration

The generated SVG files are designed to work with the LazyMagic.BlazorSvg `SvgViewer` component, which:
- Loads the SVG content
- Extracts embedded precomputed data from `<script>` tags
- Displays layouts instantly via lookup (no runtime computation)
- Provides interactive path selection and visualization

See `LazyMagic.BlazorSvg/SvgViewer_Dependency_Analysis.md` for details on the runtime component architecture.

## Pipeline Workflow

```
1. SVG + Data Input
   ├─ {ProjectName}.svg
   └─ {ProjectName}-data.json

2. Combination Generation (compute-all-combinations.js)
   └─ {ProjectName}-valid-combinations.json (251 combinations)

3. Layout Testing (run-tests.js)
   ├─ Max-Inscribed layouts
   ├─ Boardroom layouts
   └─ Hollow Square layouts

4. Data Extraction (extract-precomputed-project.js)
   └─ {ProjectName}-precomputed-data.json

5. SVG Embedding (embed-project.js)
   ├─ Add path metadata
   └─ Embed precomputed data in <script> tags

6. Final Output
   └─ output/{ProjectName}-output.svg (ready for deployment)
```

## Requirements

- Node.js 22.19.0+ (ES6 module support)
- Dependencies:
  - `jsdom` - DOM manipulation
  - `xml-beautify` - SVG formatting
  - `xmldom` - XML parsing

## Version

**Current Version:** 1.0.0 (Post-Cleanup)

See [FloorMat-map.md](FloorMat/FloorMat-map.md) for detailed version history.

## License

MIT
