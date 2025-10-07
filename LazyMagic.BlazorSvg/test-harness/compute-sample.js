// Test optimized algorithm on a small sample (10 combinations)
// Modified version of compute-all-combinations.js for testing

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawn } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const combinationsPath = path.join(__dirname, 'valid-combinations-sample.json');
const outputPath = path.join(__dirname, 'precomputed-rectangles-sample.json');
const testConfigPath = path.join(__dirname, 'test-config.js');
const testRunnerPath = path.join(__dirname, 'test-runner.js');

/**
 * Generate a temporary test-config.js file with sample combinations
 */
function generateTestConfig(combinations) {
    const testCases = combinations.map((combo, index) => ({
        name: `Combo_${String(index + 1).padStart(4, '0')}`,
        paths: combo.sections,
        goalRectangle: null,
        expectedShape: "",
        description: combo.key
    }));

    const configContent = `// Auto-generated test configuration for sample pre-computation
export const testCases = ${JSON.stringify(testCases, null, 2)};

export const config = {
    // Path to the SVG file
    svgPath: "../../BlazorTest.WASM/wwwroot/Level1.svg",

    // Path to the SvgViewer.js library
    svgViewerPath: "../wwwroot/SvgViewer.js",

    // Output directory for test results
    outputDir: "./test-results",

    // Validation thresholds
    validation: {
        // Maximum gap between inscribed rectangle and boundary (pixels)
        maxGap: 2,

        // Maximum breach outside boundary (pixels)
        maxBreach: 0.5,

        // Minimum area coverage ratio (inscribed area / boundary area)
        minCoverageRatio: 0.98  // 98% minimum threshold
    }
};
`;

    const backupPath = testConfigPath + '.backup';

    // Backup original test-config.js
    if (fs.existsSync(testConfigPath)) {
        fs.copyFileSync(testConfigPath, backupPath);
        console.log(`  Backed up original test-config.js`);
    }

    // Write new test config
    fs.writeFileSync(testConfigPath, configContent);
    console.log(`  Generated test config with ${testCases.length} test cases`);

    return backupPath;
}

/**
 * Restore original test-config.js
 */
function restoreTestConfig(backupPath) {
    if (fs.existsSync(backupPath)) {
        fs.copyFileSync(backupPath, testConfigPath);
        fs.unlinkSync(backupPath);
        console.log(`  Restored original test-config.js`);
    }
}

/**
 * Run test-runner.js and capture results
 */
async function runTestRunner() {
    return new Promise((resolve, reject) => {
        const child = spawn('node', [testRunnerPath], {
            cwd: __dirname,
            stdio: 'inherit'
        });

        child.on('close', (code) => {
            if (code === 0) {
                resolve(true);
            } else {
                reject(new Error(`test-runner.js exited with code ${code}`));
            }
        });

        child.on('error', (error) => {
            reject(error);
        });
    });
}

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('OPTIMIZED ALGORITHM TEST - SAMPLE OF 10 COMBINATIONS');
    console.log('='.repeat(80));
    console.log();

    let backupPath = null;

    try {
        // Step 1: Load sample combinations
        console.log('Step 1: Loading sample combinations...');
        const combinationsData = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));
        const combinations = combinationsData.combinations;
        console.log(`  Loaded ${combinations.length} combinations`);
        console.log();

        // Step 2: Generate test config
        console.log('Step 2: Generating temporary test configuration...');
        backupPath = generateTestConfig(combinations);
        console.log();

        // Step 3: Run test-runner.js
        console.log('Step 3: Running test-runner.js with OPTIMIZED algorithm...');
        console.log(`  Computing ${combinations.length} combinations...`);
        console.log(`  This will test if optimized algorithm can handle the workload`);
        console.log();

        const startTime = Date.now();
        await runTestRunner();
        const totalTime = Date.now() - startTime;

        console.log();
        console.log(`  ✓ Computation completed in ${(totalTime / 1000).toFixed(1)}s`);
        console.log();

        console.log('='.repeat(80));
        console.log('SAMPLE TEST COMPLETE!');
        console.log(`Completed ${combinations.length} combinations in ${(totalTime / 1000).toFixed(1)}s`);
        console.log('Check TestResults directory for output SVG files');
        console.log('='.repeat(80));

    } catch (error) {
        console.error('\nError:', error.message);
        console.error(error.stack);
    } finally {
        // Restore original test config
        if (backupPath) {
            console.log();
            console.log('Cleanup: Restoring original test configuration...');
            restoreTestConfig(backupPath);
        }
    }
}

main().catch(console.error);
