// Compute inscribed rectangles for all valid connected combinations
// Reuses the existing test infrastructure from test-runner.js

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawn } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const combinationsPath = path.join(__dirname, 'valid-combinations.json');
const outputPath = path.join(__dirname, 'precomputed-rectangles.json');
const testConfigPath = path.join(__dirname, 'test-config.js');
const testRunnerPath = path.join(__dirname, 'test-runner.js');

/**
 * Generate a temporary test-config.js file with all combinations
 */
function generateTestConfig(combinations) {
    const testCases = combinations.map((combo, index) => ({
        name: `Combo_${String(index + 1).padStart(4, '0')}`,
        paths: combo.sections,
        goalRectangle: null,
        expectedShape: "",
        description: combo.key
    }));

    const configContent = `// Auto-generated test configuration for pre-computation
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
 * Parse results.txt to extract rectangle data from JSON format
 */
function parseResultsFromJson() {
    const resultsJsonPath = path.join(__dirname, '../TestResults/results.json');

    if (!fs.existsSync(resultsJsonPath)) {
        throw new Error(`Results JSON not found: ${resultsJsonPath}`);
    }

    const resultsData = JSON.parse(fs.readFileSync(resultsJsonPath, 'utf8'));

    const rectangles = [];

    for (const testResult of resultsData.testResults || []) {
        const { testName, result } = testResult;

        if (result && result.rectangle) {
            rectangles.push({
                testName: testName,
                rectangle: {
                    x: result.rectangle.x,
                    y: result.rectangle.y,
                    width: result.rectangle.width,
                    height: result.rectangle.height,
                    angle: result.rectangle.angle || 0,
                    area: result.rectangle.area,
                    corners: result.rectangle.corners,
                    type: result.rectangle.type,
                    centroidStrategy: result.rectangle.centroidStrategy
                },
                computationTimeMs: result.computationTime
            });
        }
    }

    return rectangles;
}

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('INSCRIBED RECTANGLE PRE-COMPUTATION');
    console.log('Using existing test-runner.js infrastructure');
    console.log('='.repeat(80));
    console.log();

    let backupPath = null;

    try {
        // Step 1: Load combinations
        console.log('Step 1: Loading valid combinations...');
        const combinationsData = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));
        const combinations = combinationsData.combinations;
        console.log(`  Loaded ${combinations.length} combinations`);
        console.log();

        // Step 2: Generate test config
        console.log('Step 2: Generating temporary test configuration...');
        backupPath = generateTestConfig(combinations);
        console.log();

        // Step 3: Run test-runner.js
        console.log('Step 3: Running test-runner.js to compute all rectangles...');
        console.log(`  Computing ${combinations.length} combinations...`);
        console.log(`  Estimated time: ~${(combinations.length * 0.15).toFixed(0)} seconds`);
        console.log();

        const startTime = Date.now();
        await runTestRunner();
        const totalTime = Date.now() - startTime;

        console.log();
        console.log(`  ✓ Computation completed in ${(totalTime / 1000).toFixed(1)}s (${(totalTime / 60000).toFixed(1)} minutes)`);
        console.log();

        // Step 4: Parse results
        console.log('Step 4: Parsing results...');
        const rectangles = parseResultsFromJson();
        console.log(`  Parsed ${rectangles.length} rectangle results`);
        console.log();

        // Step 5: Map results back to combinations
        console.log('Step 5: Mapping results to combinations...');
        const results = [];

        for (let i = 0; i < combinations.length; i++) {
            const combo = combinations[i];
            const testName = `Combo_${String(i + 1).padStart(4, '0')}`;
            const rectData = rectangles.find(r => r.testName === testName);

            if (rectData && rectData.rectangle) {
                results.push({
                    key: combo.key,
                    sections: combo.sections,
                    rectangle: rectData.rectangle,
                    computationTimeMs: rectData.computationTimeMs
                });
            }
        }

        console.log(`  Successfully mapped ${results.length}/${combinations.length} results`);
        console.log();

        // Step 6: Save results
        console.log('Step 6: Saving results...');
        const output = {
            generatedAt: new Date().toISOString(),
            totalCombinations: combinations.length,
            successfulComputations: results.length,
            failedComputations: combinations.length - results.length,
            totalComputationTimeMs: totalTime,
            rectangles: results
        };

        fs.writeFileSync(outputPath, JSON.stringify(output, null, 2));
        console.log(`  Results saved to: ${outputPath}`);
        console.log(`  File size: ${(fs.statSync(outputPath).size / 1024).toFixed(1)} KB`);
        console.log();

        // Step 7: Statistics
        console.log('Step 7: Computation Statistics');
        console.log('='.repeat(80));
        console.log(`  Total combinations: ${combinations.length}`);
        console.log(`  Successful: ${results.length}`);
        console.log(`  Failed: ${combinations.length - results.length}`);
        console.log(`  Success rate: ${(results.length / combinations.length * 100).toFixed(1)}%`);
        console.log(`  Total time: ${(totalTime / 1000).toFixed(1)}s (${(totalTime / 60000).toFixed(1)} minutes)`);
        console.log(`  Average time per combination: ${(totalTime / combinations.length).toFixed(0)}ms`);
        console.log();

        console.log('='.repeat(80));
        console.log('COMPUTATION COMPLETE!');
        console.log('Next step: Embed results into Level1.svg');
        console.log('='.repeat(80));

    } catch (error) {
        console.error('\nError:', error.message);
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
