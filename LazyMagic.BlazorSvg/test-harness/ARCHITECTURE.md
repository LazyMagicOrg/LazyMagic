# Test Harness Architecture

## Why I Keep Getting Confused

The test harness has a confusing architecture that creates ambiguity about which test set will run:

### The Core Issue

1. **Single Test Runner Implementation**: There is only ONE functional test runner: `test-runner.js`
2. **Config File Swapping**: To run different test sets, the system swaps out the config file that `test-runner.js` imports
3. **Misleading File Names**: The presence of `test-runner-sample.js` suggests a parallel runner, but it's just a stub

### File Structure

```
test-harness/
├── test-runner.js              # The ONLY functional test runner (imports test-config.js)
├── test-runner-sample.js       # STUB - doesn't actually run tests
├── test-config.js              # Generated file with 251 test cases (full suite)
├── test-config-sample.js       # Hardcoded file with 6 test cases (NOT used by runners)
├── valid-combinations.json     # Source data for full suite (251 combos)
├── valid-combinations-sample.json  # Source data for sample tests (7 combos)
└── compute-sample.js           # Script that swaps configs and runs test-runner.js
```

### The Actual Workflow

#### Running Full Test Suite
```bash
node test-runner.js
# Directly imports and uses test-config.js (251 tests)
```

#### Running Sample Test Suite
```bash
node compute-sample.js
# 1. Loads valid-combinations-sample.json (7 tests)
# 2. Backs up test-config.js → test-config.js.backup
# 3. Generates temporary test-config.js with 7 tests
# 4. Runs test-runner.js (which now loads the 7-test config)
# 5. Restores test-config.js from backup
```

### Why This Is Confusing

1. **File naming implies parallelism**: Having both `test-runner.js` and `test-runner-sample.js` suggests two independent runners
2. **Config swapping is hidden**: The mechanism of temporarily overwriting `test-config.js` is not obvious
3. **Unused files**: `test-config-sample.js` exists but isn't actually used by any runner
4. **Indirect execution**: To run sample tests, you don't run `test-runner-sample.js`, you run `compute-sample.js`

### The Correct Mental Model

Think of it as:
- **ONE test runner** (`test-runner.js`)
- **ONE config slot** (`test-config.js`)
- **TWO ways to populate the config slot**:
  - Default: Contains 251 tests from `valid-combinations.json`
  - Temporary: `compute-sample.js` swaps in 7 tests from `valid-combinations-sample.json`

### How to Avoid Confusion

**Before running any tests, ask yourself**:
1. Do I want to run ALL 251 tests or just the sample?
2. If sample → use `compute-sample.js` (not `test-runner-sample.js`)
3. If full suite → use `test-runner.js`

**To modify sample tests**:
1. Edit `valid-combinations-sample.json` (add/remove combinations)
2. Run `node compute-sample.js` (it will regenerate `test-config.js` temporarily)

**Never**:
- Run `test-runner-sample.js` directly (it's a stub)
- Manually edit `test-config.js` (it's auto-generated)
- Edit `test-config-sample.js` (it's not used)

## Improvement Suggestion

The architecture could be clearer if:
1. `test-runner-sample.js` was deleted (it's misleading)
2. `test-config-sample.js` was deleted (it's unused)
3. `compute-sample.js` was renamed to `run-sample-tests.js` (clearer intent)
4. OR: Make `test-runner.js` accept a `--config` parameter to specify which config to use
