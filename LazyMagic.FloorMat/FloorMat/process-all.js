// FloorMat - Process All Projects
// Auto-detects and processes all SVG files in the input/ directory

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const inputDir = path.join(__dirname, 'input');
const outputDir = path.join(__dirname, 'output');

/**
 * Find all SVG files in the input directory
 */
function findSvgFiles() {
    if (!fs.existsSync(inputDir)) {
        console.error(`Error: input/ directory not found at ${inputDir}`);
        console.log('Please create an input/ directory and place your SVG files there.');
        process.exit(1);
    }

    const files = fs.readdirSync(inputDir);
    const svgFiles = files.filter(f => f.toLowerCase().endsWith('.svg'));

    return svgFiles.map(filename => {
        const prefix = path.basename(filename, '.svg');
        return {
            filename,
            prefix,
            svgPath: path.join(inputDir, filename),
            roomsPath: path.join(inputDir, `${prefix}-Rooms.json`),
            combinationsPath: path.join(outputDir, `${prefix}-output`, `${prefix}-valid-combinations.json`)
        };
    });
}

/**
 * Generate temporary config files for a project
 */
function generateConfigFiles(project) {
    const projectOutputDir = path.join(outputDir, `${project.prefix}-output`);
    const computedLayoutsDir = path.join(projectOutputDir, 'ComputedLayouts');

    // Create output directories
    if (!fs.existsSync(computedLayoutsDir)) {
        fs.mkdirSync(computedLayoutsDir, { recursive: true });
    }

    const configs = [];

    // Template configuration options
    const baseConfig = {
        timeout: 3600000,
        outputOptions: {
            generateSVG: true,
            generateJSON: true,
            generateSummary: true,
            deleteExistingResults: true
        }
    };

    // MaxInscribed config
    const maxInscribedConfig = {
        ...baseConfig,
        testName: `${project.prefix} - Max Inscribed (Hybrid Algorithm)`,
        algorithm: "maxinscribed",
        svgPath: project.svgPath,
        outputDir: path.join(computedLayoutsDir, `${project.prefix}-MaxInscribedResults`),
        algorithmOptions: {
            coverageThreshold: 0,
            maxTime: 5000,
            gridStep: 8.0,
            polylabelPrecision: 0.5,
            aspectRatios: [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0],
            binarySearchPrecision: 0.0001,
            binarySearchMaxIterations: 20,
            debugMode: false
        },
        combinationsFile: project.combinationsPath
    };

    // Boardroom config
    const boardroomConfig = {
        ...baseConfig,
        testName: `${project.prefix} - Boardroom Layout`,
        algorithm: "boardroom",
        svgPath: project.svgPath,
        outputDir: path.join(computedLayoutsDir, `${project.prefix}-BoardroomResults`),
        algorithmOptions: {
            width: { mode: "fixed", value: 13 },
            height: { mode: "continuous", min: 6, max: 1000, samples: 25 },
            angleSamples: 25,
            centroidSamples: 25,
            debugMode: false
        },
        combinationsFile: project.combinationsPath
    };

    // Hollow Square config
    const hollowSquareConfig = {
        ...baseConfig,
        testName: `${project.prefix} - Hollow Square Layout`,
        algorithm: "hollowsquare",
        svgPath: project.svgPath,
        outputDir: path.join(computedLayoutsDir, `${project.prefix}-HollowSquareResults`),
        algorithmOptions: {
            width: { mode: "discrete", base: 19, increment: 6, minIncrements: 0 },
            height: { mode: "discrete", base: 14, increment: 6, minIncrements: 0 },
            maxTime: 5000,
            angleSamples: 25,
            centroidSamples: 25,
            debugMode: false
        },
        combinationsFile: project.combinationsPath
    };

    // Write temporary config files
    const tempConfigDir = path.join(projectOutputDir, '.temp-configs');
    if (!fs.existsSync(tempConfigDir)) {
        fs.mkdirSync(tempConfigDir, { recursive: true });
    }

    const maxInscribedPath = path.join(tempConfigDir, 'maxinscribed.json');
    const boardroomPath = path.join(tempConfigDir, 'boardroom.json');
    const hollowSquarePath = path.join(tempConfigDir, 'hollowsquare.json');

    fs.writeFileSync(maxInscribedPath, JSON.stringify(maxInscribedConfig, null, 2));
    fs.writeFileSync(boardroomPath, JSON.stringify(boardroomConfig, null, 2));
    fs.writeFileSync(hollowSquarePath, JSON.stringify(hollowSquareConfig, null, 2));

    return {
        maxInscribed: maxInscribedPath,
        boardroom: boardroomPath,
        hollowSquare: hollowSquarePath
    };
}

/**
 * Process a single project
 */
async function processProject(project) {
    console.log('='.repeat(80));
    console.log(`PROCESSING PROJECT: ${project.prefix}`);
    console.log('='.repeat(80));
    console.log();

    // Check if Rooms.json file exists
    if (!fs.existsSync(project.roomsPath)) {
        console.error(`✗ Error: Rooms file not found: ${project.prefix}-Rooms.json`);
        console.error(`  Expected at: ${project.roomsPath}`);
        console.error(`  Skipping project ${project.prefix}`);
        return false;
    }

    const projectOutputDir = path.join(outputDir, `${project.prefix}-output`);

    console.log(`✓ Output directory: ${project.prefix}-output/`);
    console.log();

    try {
        // Step 0: Generate valid combinations from Rooms.json
        console.log('Step 0/4: Generating valid combinations...');

        const generateCombosCmd = `node compute-all-combinations.js "${project.prefix}" "${inputDir}" "${projectOutputDir}"`;
        await execAsync(generateCombosCmd, { cwd: __dirname });

        console.log('  ✓ Valid combinations generated\\n');

        // Verify combinations file was created
        if (!fs.existsSync(project.combinationsPath)) {
            throw new Error(`Failed to generate combinations file: ${project.combinationsPath}`);
        }

        // Generate temporary config files
        const configs = generateConfigFiles(project);

        // Step 1: Run tests for all three algorithms
        console.log('Step 1/4: Running tests (MaxInscribed, Boardroom, Hollow Square)...');
        console.log();

        console.log('  Running MaxInscribed tests...');
        await execAsync(`node run-tests.js "${configs.maxInscribed}"`, {
            cwd: __dirname,
            maxBuffer: 200 * 1024 * 1024 // 200MB buffer for verbose algorithm logging
        });

        console.log('  Running Boardroom tests...');
        await execAsync(`node run-tests.js "${configs.boardroom}"`, {
            cwd: __dirname,
            maxBuffer: 200 * 1024 * 1024
        });

        console.log('  Running Hollow Square tests...');
        await execAsync(`node run-tests.js "${configs.hollowSquare}"`, {
            cwd: __dirname,
            maxBuffer: 200 * 1024 * 1024
        });

        console.log('  ✓ All tests complete\\n');

        // Step 2: Extract precomputed data
        console.log('Step 2/4: Extracting precomputed data...');

        const computedLayoutsDir = path.join(projectOutputDir, 'ComputedLayouts');
        const extractCmd = `node extract-precomputed-project.js "${project.prefix}" "${computedLayoutsDir}" "${project.combinationsPath}" "${projectOutputDir}"`;
        await execAsync(extractCmd, { cwd: __dirname });

        console.log('  ✓ Data extraction complete\\n');

        // Step 3: Embed data in SVG
        console.log('Step 3/4: Embedding data in SVG...');

        const embedCmd = `node embed-project.js "${project.prefix}" "${project.svgPath}" "${projectOutputDir}"`;
        await execAsync(embedCmd, { cwd: __dirname });

        console.log('  ✓ SVG embedding complete\\n');

        console.log('='.repeat(80));
        console.log(`✓ PROJECT ${project.prefix} COMPLETE!`);
        console.log(`  Output location: ${projectOutputDir}`);
        console.log('='.repeat(80));
        console.log();

        return true;
    } catch (error) {
        console.error();
        console.error('='.repeat(80));
        console.error(`✗ PROJECT ${project.prefix} FAILED`);
        console.error(`  Error: ${error.message}`);
        if (error.stderr) console.error(`  Details: ${error.stderr}`);
        console.error('='.repeat(80));
        console.error();
        return false;
    }
}

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('FLOORMAT - MULTI-PROJECT PIPELINE');
    console.log('='.repeat(80));
    console.log();

    const projects = findSvgFiles();

    if (projects.length === 0) {
        console.log('No SVG files found in input/ directory.');
        console.log();
        console.log('To use FloorMat:');
        console.log('  1. Place your SVG file in the input/ directory (e.g., input/MyProject.svg)');
        console.log('  2. Create a combinations file (e.g., input/MyProject-combinations.json)');
        console.log('  3. Run: npm run process');
        console.log();
        process.exit(0);
    }

    console.log(`Found ${projects.length} project(s) to process:`);
    projects.forEach(p => console.log(`  - ${p.prefix}`));
    console.log();

    let successCount = 0;
    let failCount = 0;

    for (const project of projects) {
        const success = await processProject(project);
        if (success) {
            successCount++;
        } else {
            failCount++;
        }
    }

    // Summary
    console.log();
    console.log('='.repeat(80));
    console.log('PIPELINE SUMMARY');
    console.log('='.repeat(80));
    console.log(`  Total projects: ${projects.length}`);
    console.log(`  ✓ Successful: ${successCount}`);
    if (failCount > 0) {
        console.log(`  ✗ Failed: ${failCount}`);
    }
    console.log('='.repeat(80));

    process.exit(failCount > 0 ? 1 : 0);
}

main().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
