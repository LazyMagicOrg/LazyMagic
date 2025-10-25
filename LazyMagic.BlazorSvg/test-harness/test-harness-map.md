# Test Harness Directory Map

**Created:** 2025-10-25
**Purpose:** Document all files in test-harness directory, their dependencies, interactions, and which files are active vs. deprecated.

---

## Table of Contents

1. [Overview](#overview)
2. [Directory Structure](#directory-structure)
3. [Current Production Pipeline](#current-production-pipeline)
4. [File Inventory](#file-inventory)
5. [Dependency Graph](#dependency-graph)
6. [Files by Category](#files-by-category)
7. [Deprecated/Outdated Files](#deprecatedoutdated-files)
8. [Recommendations for Cleanup](#recommendations-for-cleanup)

---

## Overview

The test-harness directory contains tools for generating, extracting, and embedding precomputed geometric layout data for SVG floor plans. The system has evolved significantly over time, resulting in **multiple generations** of files serving similar purposes.

**Current State:**
- ✅ **Production Ready:** Unified pipeline using `run-tests.js` + config files
- ⚠️ **Legacy:** Multiple standalone test-runner scripts (test-runner.js, test-runner-boardroom.js, etc.)
- ❌ **Deprecated:** Old analysis, debugging, and sample-only scripts
- 🔧 **Utility:** Extraction and embedding scripts (still active)

---

## Directory Structure

```
test-harness/
├── Configuration Files
│   ├── package.json                              [Active] npm package config
│   ├── package-lock.json                         [Active] npm lockfile
│   └── test-configs/                             [Active] JSON configs for run-tests.js
│       ├── maxinscribed-full.json
│       ├── maxinscribed-sample.json
│       ├── boardroom-full.json
│       ├── boardroom-sample.json
│       ├── hollowsquare-full.json
│       └── hollowsquare-sample.json
│
├── Valid Combinations
│   ├── valid-combinations.json                   [Active] Master list of 251 valid combos
│   ├── valid-combinations-maxinscribed-full.json [Duplicate?] Same as above
│   ├── valid-combinations-boardroom-sample.json  [Sample] Small subset for testing
│   ├── valid-combinations-hollowsquare-sample.json [Sample] Small subset
│   ├── valid-combinations-sample-normal.json     [Deprecated] Old sample set
│   └── valid-combinations-sample-rotated.json    [Deprecated] Old sample set
│
├── Test Runners (NEW - Unified)
│   └── run-tests.js                              [Active] Unified runner using config files
│
├── Test Runners (OLD - Standalone)
│   ├── test-runner.js                            [Legacy] Standalone max-inscribed runner
│   ├── test-runner-boardroom.js                  [Legacy] Standalone boardroom runner
│   ├── test-runner-hollowsquare.js               [Legacy] Standalone hollow square runner
│   ├── test-runner-normal.js                     [Deprecated] Old variant
│   ├── test-runner-rotated.js                    [Deprecated] Old variant
│   ├── test-runner-sample.js                     [Deprecated] Old sample runner
│   ├── test-runner-boardroom-sample.js           [Deprecated?] Sample-only boardroom
│   ├── test-runner-hollowsquare-sample.js        [Deprecated?] Sample-only hollow square
│   └── test-runner-hollowsquare-sample-base.js   [Deprecated?] Base for sample
│
├── Extraction Scripts
│   ├── extract-all-precomputed.js                [Active] Unified extractor (all 3 systems)
│   ├── extract-precomputed-rectangles.js         [Legacy] Standalone max-inscribed extractor
│   └── extract-precomputed-boardroom.js          [Legacy] Standalone boardroom extractor
│
├── Embedding Scripts
│   ├── embed-all-in-svg.js                       [Active] Unified embedder (all 3 systems)
│   ├── embed-rectangles-in-svg.js                [Legacy] Standalone max-inscribed embedder
│   └── embed-boardroom-in-svg.js                 [Legacy] Standalone boardroom embedder
│
├── Output Data Files
│   ├── precomputed-rectangles.json               [Generated] Max-inscribed data
│   ├── precomputed-boardroom.json                [Generated] Boardroom data
│   └── precomputed-hollowsquare.json             [Generated] Hollow square data
│
├── Algorithm Support
│   ├── algorithms.js                             [Utility] Shared algorithm helpers
│   └── algorithm-params.js                       [Config] Default algorithm parameters
│
├── Test Configuration (OLD)
│   ├── test-config.js                            [Legacy] Hardcoded config for test-runner.js
│   ├── test-config-sample.js                     [Deprecated] Old sample config
│   ├── test-config-normal.js                     [Deprecated] Old config
│   └── test-config-rotated.js                    [Deprecated] Old config
│
├── Combination Generation
│   ├── compute-all-combinations.js               [Active] Generates valid-combinations.json
│   └── compute-inscribed-rectangles.js           [Deprecated?] Old rectangle computation
│
├── Sample Generators
│   ├── run-hollowsquare-samples.js               [Sample] Generates sample hollow square tests
│   ├── create-hollowsquare-samples.js            [Sample] Helper for creating samples
│   ├── compute-sample-normal.js                  [Deprecated] Old sample generator
│   ├── compute-sample-rotated.js                 [Deprecated] Old sample generator
│   └── update-sample-jsons.js                    [Deprecated] Old sample updater
│
├── Analysis & Debugging Tools
│   ├── analyze-breaches.js                       [Debug] Analyze polygon boundary breaches
│   ├── analyze-discrepancies.js                  [Debug] Compare algorithm results
│   ├── analyze-rotation-issues.md                [Debug] Documentation of rotation issues
│   ├── compare-results.js                        [Debug] Compare test outputs
│   ├── extract-differences.js                    [Debug] Extract result differences
│   ├── update-config-with-discrepancies.js       [Debug] Update configs based on analysis
│   ├── check-limits.cjs                          [Debug] Check iteration limits
│   └── debug-iterations.js                       [Debug] Debug iteration counts
│
├── Browser Testing (Experimental)
│   ├── browser-test-runner.js                    [Experimental] Puppeteer-based runner
│   ├── test-boardroom-single.js                  [Experimental] Single test in browser
│   └── test-page.html                            [Experimental] HTML test page
│
├── Documentation
│   ├── testharness.md                            [Active] Original test harness documentation
│   ├── test-harness-map.md                       [Active] This file
│   └── analyze-rotation-issues.md                [Debug] Rotation analysis documentation
│
├── Logs & Temp Files
│   ├── run-boardroom.log                         [Generated] Last boardroom run log
│   ├── nul                                       [Junk] Empty file (Windows artifact?)
│   └── precompute-rectangles.js                  [Deprecated?] Old precomputation script
│
├── Output Directories
│   └── BoardroomResults/                         [Generated] Old boardroom results in harness dir
│
└── External Dependencies
    └── node_modules/                             [npm] Installed packages
        ├── jsdom                                 Used for SVG parsing
        ├── xmldom                                Used for XML parsing
        ├── puppeteer                             Used for browser testing
        └── http-server                           Used for serving test pages
```

---

## Current Production Pipeline

### **Recommended Workflow (Unified)**

```
1. Generate valid combinations (one-time)
   └─→ node compute-all-combinations.js
       └─→ Output: valid-combinations.json

2. Run tests for each algorithm
   └─→ node run-tests.js test-configs/maxinscribed-full.json
   └─→ node run-tests.js test-configs/boardroom-full.json
   └─→ node run-tests.js test-configs/hollowsquare-full.json
       └─→ Output: ../TestResults/{MaxInscribedResults,BoardroomResults,HollowSquareResults}/*.json

3. Extract all precomputed data
   └─→ node extract-all-precomputed.js
       └─→ Output: precomputed-{rectangles,boardroom,hollowsquare}.json

4. Embed all data in SVG
   └─→ node embed-all-in-svg.js
       └─→ Output: ../../BlazorTest.WASM/wwwroot/Level1.svg (updated)
```

### **Legacy Workflow (Deprecated but still functional)**

```
1. Run individual test runners
   └─→ node test-runner.js
   └─→ node test-runner-boardroom.js
   └─→ node test-runner-hollowsquare.js

2. Extract individual data files
   └─→ node extract-precomputed-rectangles.js
   └─→ node extract-precomputed-boardroom.js
   └─→ (no extract for hollow square - uses extract-all)

3. Embed individual data files
   └─→ node embed-rectangles-in-svg.js
   └─→ node embed-boardroom-in-svg.js
   └─→ (no embed for hollow square - uses embed-all)
```

---

## File Inventory

### **Active Production Files** ✅

| File | Purpose | Dependencies | Output |
|------|---------|--------------|--------|
| `run-tests.js` | Unified test runner | test-configs/*.json, valid-combinations.json, wwwroot/*.js | ../TestResults/*/*.json |
| `extract-all-precomputed.js` | Extract all 3 layout types | ../TestResults/*/*.json | precomputed-*.json (3 files) |
| `embed-all-in-svg.js` | Embed all 3 data files | precomputed-*.json (3 files) | ../../BlazorTest.WASM/wwwroot/Level1.svg |
| `compute-all-combinations.js` | Generate valid combos | Rooms.json | valid-combinations.json |
| `valid-combinations.json` | Master combo list | compute-all-combinations.js | Used by all runners |
| `test-configs/*.json` | Test configurations | None | Used by run-tests.js |
| `algorithms.js` | Shared algorithm utilities | None | Used by test runners |
| `algorithm-params.js` | Default parameters | None | Used by test runners |
| `package.json` | npm configuration | None | Defines dependencies |

### **Legacy Files (Still Functional)** ⚠️

| File | Purpose | Replaced By | Can Remove? |
|------|---------|-------------|-------------|
| `test-runner.js` | Standalone max-inscribed runner | run-tests.js + config | **YES** after migration |
| `test-runner-boardroom.js` | Standalone boardroom runner | run-tests.js + config | **YES** after migration |
| `test-runner-hollowsquare.js` | Standalone hollow square runner | run-tests.js + config | **YES** after migration |
| `extract-precomputed-rectangles.js` | Extract max-inscribed only | extract-all-precomputed.js | **YES** after migration |
| `extract-precomputed-boardroom.js` | Extract boardroom only | extract-all-precomputed.js | **YES** after migration |
| `embed-rectangles-in-svg.js` | Embed max-inscribed only | embed-all-in-svg.js | **YES** after migration |
| `embed-boardroom-in-svg.js` | Embed boardroom only | embed-all-in-svg.js | **YES** after migration |
| `test-config.js` | Hardcoded test config | test-configs/maxinscribed-full.json | **YES** |

### **Deprecated Files** ❌

| File | Purpose | Why Deprecated | Safe to Remove? |
|------|---------|----------------|-----------------|
| `test-runner-normal.js` | Old variant (normal rotation) | Replaced by test-runner.js | **YES** |
| `test-runner-rotated.js` | Old variant (rotated) | Replaced by test-runner.js | **YES** |
| `test-runner-sample.js` | Old sample runner | Replaced by run-tests.js + sample config | **YES** |
| `test-config-sample.js` | Old sample config | Replaced by test-configs/maxinscribed-sample.json | **YES** |
| `test-config-normal.js` | Old normal config | Replaced by JSON configs | **YES** |
| `test-config-rotated.js` | Old rotated config | Replaced by JSON configs | **YES** |
| `valid-combinations-sample-normal.json` | Old sample set | Replaced by test-configs/maxinscribed-sample.json | **YES** |
| `valid-combinations-sample-rotated.json` | Old sample set | Replaced by test-configs/maxinscribed-sample.json | **YES** |
| `compute-sample-normal.js` | Old sample generator | Replaced by run-tests.js + sample configs | **YES** |
| `compute-sample-rotated.js` | Old sample generator | Replaced by run-tests.js + sample configs | **YES** |
| `update-sample-jsons.js` | Old sample updater | No longer needed | **YES** |
| `precompute-rectangles.js` | Old precomputation | Replaced by test-runner.js | **YES** |
| `compute-inscribed-rectangles.js` | Old computation | Replaced by test-runner.js | **MAYBE** - verify not used |

### **Sample/Testing Files** 🧪

| File | Purpose | Keep? |
|------|---------|-------|
| `run-hollowsquare-samples.js` | Generate hollow square samples | **MAYBE** - useful for testing |
| `create-hollowsquare-samples.js` | Helper for sample generation | **MAYBE** - useful for testing |
| `test-runner-boardroom-sample.js` | Boardroom sample runner | **NO** - use run-tests.js with sample config |
| `test-runner-hollowsquare-sample.js` | Hollow square sample runner | **NO** - use run-tests.js with sample config |
| `test-runner-hollowsquare-sample-base.js` | Base for hollow square samples | **NO** - consolidate into run-tests.js |
| `valid-combinations-boardroom-sample.json` | Boardroom sample combos | **YES** - used by test-configs |
| `valid-combinations-hollowsquare-sample.json` | Hollow square sample combos | **YES** - used by test-configs |

### **Debug/Analysis Files** 🔍

| File | Purpose | Keep? |
|------|---------|-------|
| `analyze-breaches.js` | Analyze polygon breaches | **MAYBE** - useful for debugging |
| `analyze-discrepancies.js` | Compare algorithm results | **MAYBE** - useful for validation |
| `analyze-rotation-issues.md` | Rotation analysis docs | **YES** - documentation |
| `compare-results.js` | Compare test outputs | **MAYBE** - useful for debugging |
| `extract-differences.js` | Extract differences | **NO** - specific to old analysis |
| `update-config-with-discrepancies.js` | Auto-update configs | **NO** - specific to old workflow |
| `check-limits.cjs` | Check iteration limits | **MAYBE** - useful for debugging |
| `debug-iterations.js` | Debug iteration counts | **MAYBE** - useful for debugging |

### **Browser Testing Files** 🌐

| File | Purpose | Keep? |
|------|---------|-------|
| `browser-test-runner.js` | Puppeteer-based runner | **MAYBE** - alternative testing approach |
| `test-boardroom-single.js` | Single test in browser | **NO** - experimental |
| `test-page.html` | HTML test page | **MAYBE** - useful for manual testing |

### **Junk/Artifact Files** 🗑️

| File | Purpose | Action |
|------|---------|--------|
| `nul` | Empty file (Windows artifact) | **DELETE** |
| `run-boardroom.log` | Old log file | **DELETE** (regenerated on next run) |
| `BoardroomResults/` | Old output dir in harness | **DELETE** (should be in ../TestResults/) |

### **Duplicate/Unclear Files** ❓

| File | Suspected Duplicate Of | Action |
|------|------------------------|--------|
| `valid-combinations-maxinscribed-full.json` | `valid-combinations.json` | **VERIFY** - compare contents, delete if identical |

---

## Dependency Graph

### **Core Dependencies**

```
run-tests.js
  ├─→ test-configs/*.json (configuration)
  ├─→ valid-combinations.json (test cases)
  ├─→ ../wwwroot/SvgViewerInscribedRect.js (unified algorithm)
  ├─→ ../wwwroot/SvgViewerBoundaryBased.js (boundary algorithm)
  ├─→ ../wwwroot/SvgViewerOptimized.js (optimized algorithm)
  ├─→ ../wwwroot/SvgViewerBoardroom.js (boardroom algorithm)
  ├─→ ../wwwroot/SvgViewerAlgorithms.js (shared utilities)
  ├─→ ../wwwroot/kdtree.js (spatial data structures)
  └─→ ../../BlazorTest.WASM/wwwroot/Level1-normal.svg (SVG data)
       └─→ Output: ../TestResults/{algorithm}/*.json
```

```
extract-all-precomputed.js
  ├─→ valid-combinations.json (combo metadata)
  ├─→ ../TestResults/MaxInscribedResults/*.json
  ├─→ ../TestResults/BoardroomResults/*.json
  └─→ ../TestResults/HollowSquareResults/*.json
       └─→ Output: precomputed-{rectangles,boardroom,hollowsquare}.json
```

```
embed-all-in-svg.js
  ├─→ precomputed-rectangles.json
  ├─→ precomputed-boardroom.json
  └─→ precomputed-hollowsquare.json
       └─→ Output: ../../BlazorTest.WASM/wwwroot/Level1.svg (updated)
```

### **Legacy Dependencies**

```
test-runner.js
  ├─→ test-config.js (hardcoded config)
  ├─→ ../wwwroot/SvgViewerAlgorithms.js
  ├─→ ../wwwroot/SvgViewerBoundaryBased.js
  └─→ ../wwwroot/SvgViewerOptimized.js
       └─→ Output: ../TestResults/MaxInscribedResults/*.svg (old format)
```

```
test-runner-boardroom.js
  ├─→ valid-combinations.json
  ├─→ ../wwwroot/SvgViewerAlgorithms.js
  └─→ ../wwwroot/SvgViewerBoardroom.js
       └─→ Output: ../TestResults/BoardroomResults/*.svg
```

---

## Files by Category

### **1. Configuration & Metadata**
- `package.json` - npm package definition
- `test-configs/*.json` - Test configuration files (6 files)
- `valid-combinations.json` - Master list of valid path combinations
- `algorithm-params.js` - Default algorithm parameters
- `test-config.js` - Legacy hardcoded config

### **2. Test Execution**
- `run-tests.js` - **[PRIMARY]** Unified test runner
- `test-runner.js` - **[LEGACY]** Standalone max-inscribed runner
- `test-runner-boardroom.js` - **[LEGACY]** Standalone boardroom runner
- `test-runner-hollowsquare.js` - **[LEGACY]** Standalone hollow square runner

### **3. Data Processing**
- `extract-all-precomputed.js` - **[PRIMARY]** Extract all layout data
- `embed-all-in-svg.js` - **[PRIMARY]** Embed all data in SVG
- `extract-precomputed-rectangles.js` - **[LEGACY]** Extract max-inscribed only
- `extract-precomputed-boardroom.js` - **[LEGACY]** Extract boardroom only
- `embed-rectangles-in-svg.js` - **[LEGACY]** Embed max-inscribed only
- `embed-boardroom-in-svg.js` - **[LEGACY]** Embed boardroom only

### **4. Utilities**
- `algorithms.js` - Shared algorithm helpers
- `compute-all-combinations.js` - Generate valid combinations

### **5. Analysis & Debug**
- `analyze-breaches.js` - Analyze polygon boundary violations
- `analyze-discrepancies.js` - Compare algorithm outputs
- `compare-results.js` - Compare test results
- `check-limits.cjs` - Check iteration limits
- `debug-iterations.js` - Debug iteration counts

### **6. Documentation**
- `testharness.md` - Original documentation
- `test-harness-map.md` - This file
- `analyze-rotation-issues.md` - Rotation analysis documentation

---

## Deprecated/Outdated Files

### **Safe to Delete Immediately**

1. **Old Test Runners:**
   - `test-runner-normal.js`
   - `test-runner-rotated.js`
   - `test-runner-sample.js`
   - `test-runner-boardroom-sample.js`
   - `test-runner-hollowsquare-sample.js`
   - `test-runner-hollowsquare-sample-base.js`

2. **Old Configs:**
   - `test-config-sample.js`
   - `test-config-normal.js`
   - `test-config-rotated.js`
   - `valid-combinations-sample-normal.json`
   - `valid-combinations-sample-rotated.json`

3. **Old Sample Generators:**
   - `compute-sample-normal.js`
   - `compute-sample-rotated.js`
   - `update-sample-jsons.js`

4. **Junk:**
   - `nul` (empty file)
   - `run-boardroom.log` (regenerated)
   - `BoardroomResults/` directory (wrong location)

### **Can Delete After Migration to Unified Pipeline**

1. **Legacy Runners:**
   - `test-runner.js`
   - `test-runner-boardroom.js`
   - `test-runner-hollowsquare.js`
   - `test-config.js`

2. **Legacy Extractors/Embedders:**
   - `extract-precomputed-rectangles.js`
   - `extract-precomputed-boardroom.js`
   - `embed-rectangles-in-svg.js`
   - `embed-boardroom-in-svg.js`

### **Keep for Now (Useful)**

1. **Analysis Tools:**
   - `analyze-breaches.js`
   - `analyze-discrepancies.js`
   - `compare-results.js`
   - `check-limits.cjs`
   - `debug-iterations.js`

2. **Sample Generators:**
   - `run-hollowsquare-samples.js`
   - `create-hollowsquare-samples.js`

3. **Documentation:**
   - `testharness.md`
   - `analyze-rotation-issues.md`

4. **Browser Testing:**
   - `test-page.html` (useful for manual debugging)

---

## Recommendations for Cleanup

### **Phase 1: Immediate Cleanup (Safe)**

**DELETE:**
- All `test-runner-{normal,rotated,sample,*-sample}.js` files (7 files)
- All `test-config-{sample,normal,rotated}.js` files (3 files)
- All `valid-combinations-sample-{normal,rotated}.json` files (2 files)
- `compute-sample-{normal,rotated}.js` (2 files)
- `update-sample-jsons.js`
- `nul`
- `run-boardroom.log`
- `BoardroomResults/` directory in test-harness (move to ../TestResults if needed)

**Total: ~20 files**

### **Phase 2: Migrate to Unified Pipeline**

**ACTION:**
1. Update all documentation to reference `run-tests.js` + config files
2. Verify `run-tests.js` handles all use cases of old runners
3. Test unified pipeline end-to-end
4. Update package.json scripts

**THEN DELETE:**
- `test-runner.js`
- `test-runner-boardroom.js`
- `test-runner-hollowsquare.js`
- `test-config.js`
- `extract-precomputed-rectangles.js`
- `extract-precomputed-boardroom.js`
- `embed-rectangles-in-svg.js`
- `embed-boardroom-in-svg.js`

**Total: 8 files**

### **Phase 3: Consolidate Analysis Tools**

**ACTION:**
1. Review each analysis script
2. Determine which are still useful
3. Consolidate similar functionality
4. Move to dedicated `analysis/` subdirectory

**MAYBE DELETE:**
- `extract-differences.js` (specific to old analysis)
- `update-config-with-discrepancies.js` (no longer needed)

### **Phase 4: Verify and Remove Duplicates**

**ACTION:**
1. Compare `valid-combinations-maxinscribed-full.json` vs `valid-combinations.json`
2. If identical, delete the duplicate

### **Final Directory Structure (After Cleanup)**

```
test-harness/
├── Configuration
│   ├── package.json
│   ├── package-lock.json
│   ├── algorithm-params.js
│   └── test-configs/
│       ├── maxinscribed-full.json
│       ├── maxinscribed-sample.json
│       ├── boardroom-full.json
│       ├── boardroom-sample.json
│       ├── hollowsquare-full.json
│       └── hollowsquare-sample.json
│
├── Valid Combinations
│   ├── valid-combinations.json
│   ├── valid-combinations-boardroom-sample.json
│   └── valid-combinations-hollowsquare-sample.json
│
├── Core Pipeline
│   ├── run-tests.js
│   ├── extract-all-precomputed.js
│   ├── embed-all-in-svg.js
│   └── compute-all-combinations.js
│
├── Utilities
│   └── algorithms.js
│
├── Sample Generators (optional)
│   ├── run-hollowsquare-samples.js
│   └── create-hollowsquare-samples.js
│
├── Analysis Tools (optional - move to analysis/)
│   ├── analyze-breaches.js
│   ├── analyze-discrepancies.js
│   ├── compare-results.js
│   ├── check-limits.cjs
│   └── debug-iterations.js
│
├── Browser Testing (optional)
│   ├── browser-test-runner.js
│   └── test-page.html
│
├── Documentation
│   ├── testharness.md
│   ├── test-harness-map.md
│   └── analyze-rotation-issues.md
│
└── Generated Data
    ├── precomputed-rectangles.json
    ├── precomputed-boardroom.json
    └── precomputed-hollowsquare.json
```

**Reduction:** From ~55 files to ~25 files (~45% reduction)

---

## Migration Checklist

### **Before Deleting Legacy Files:**

- [ ] Verify `run-tests.js` produces identical output to `test-runner.js`
- [ ] Verify `run-tests.js` produces identical output to `test-runner-boardroom.js`
- [ ] Verify `run-tests.js` produces identical output to `test-runner-hollowsquare.js`
- [ ] Verify `extract-all-precomputed.js` extracts all 3 data types correctly
- [ ] Verify `embed-all-in-svg.js` embeds all 3 data types correctly
- [ ] Update all documentation references
- [ ] Update package.json scripts
- [ ] Test full pipeline end-to-end
- [ ] Backup any custom logic from legacy files

### **After Cleanup:**

- [ ] Update testharness.md with new structure
- [ ] Update all documentation/*.md files
- [ ] Consider renaming "test-harness" to "floorgen"
- [ ] Organize analysis tools into subdirectory
- [ ] Add README.md to explain directory purpose

---

## Notes

1. **`valid-combinations-maxinscribed-full.json`** appears to be a duplicate of `valid-combinations.json`. Need to verify and delete one.

2. **Browser testing files** (`browser-test-runner.js`, `test-page.html`) might be useful for debugging but seem experimental. Keep for now.

3. **Sample generators** for hollow square might be useful for creating test datasets. Consider keeping or moving to a `samples/` directory.

4. **Analysis scripts** could be consolidated into an `analysis/` subdirectory to reduce clutter in main directory.

5. **Log files** should be added to `.gitignore` to prevent accidental commits.

6. **Test output directories** (`BoardroomResults/` in test-harness) should be moved or deleted - outputs should go to `../TestResults/`.

7. Consider creating a **`.gitignore`** to exclude:
   - `*.log`
   - `node_modules/`
   - `precomputed-*.json` (generated files)
   - `nul`

8. **Package.json scripts** should be updated to reference the unified pipeline:
   ```json
   "scripts": {
     "test:maxinscribed": "node run-tests.js test-configs/maxinscribed-full.json",
     "test:boardroom": "node run-tests.js test-configs/boardroom-full.json",
     "test:hollowsquare": "node run-tests.js test-configs/hollowsquare-full.json",
     "extract": "node extract-all-precomputed.js",
     "embed": "node embed-all-in-svg.js"
   }
   ```

---

**End of Test Harness Map**
