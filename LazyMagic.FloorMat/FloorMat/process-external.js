// FloorMat - Process External Directory
// Processes SVG files in an external directory structure
// Usage: node process-external.js <target-directory>

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Get target directory from command line
const args = process.argv.slice(2);

if (args.length < 1) {
    console.error('Usage: node process-external.js <target-directory>');
    console.error('');
    console.error('Example:');
    console.error('  node process-external.js C:\\path\\to\\venue');
    console.error('');
    console.error('Expected structure:');
    console.error('  <target-directory>/');
    console.error('    input/');
    console.error('      YourVenue.svg');
    console.error('      YourVenue-Rooms.json');
    console.error('    output/          (created automatically)');
    process.exit(1);
}

const targetDir = path.resolve(args[0]);
const inputDir = path.join(targetDir, 'input');
const outputDir = path.join(targetDir, 'output');

console.log('='.repeat(80));
console.log('FLOORMAT - EXTERNAL DIRECTORY PROCESSING');
console.log('='.repeat(80));
console.log();
console.log(`Target Directory: ${targetDir}`);
console.log();

// Ensure target directory exists
if (!fs.existsSync(targetDir)) {
    console.error(`✗ Error: Target directory does not exist: ${targetDir}`);
    process.exit(1);
}

// Create input directory if it doesn't exist
if (!fs.existsSync(inputDir)) {
    console.log(`Creating input directory: ${inputDir}`);
    fs.mkdirSync(inputDir, { recursive: true });
}

// Look for SVG files in input directory
const files = fs.readdirSync(inputDir);
const svgFiles = files.filter(f => f.toLowerCase().endsWith('.svg'));

if (svgFiles.length === 0) {
    console.error(`✗ Error: No SVG files found in ${inputDir}`);
    console.error('');
    console.error('Please provide an SVG file and run the command again:');
    console.error(`  npm run process-external "${targetDir}"`);
    console.error('');
    process.exit(1);
}

// Process the first SVG found
const svgFilename = svgFiles[0];
const prefix = path.basename(svgFilename, '.svg');
const svgPath = path.join(inputDir, svgFilename);
const roomsPath = path.join(inputDir, `${prefix}-Rooms.json`);

console.log(`✓ Found SVG: ${svgFilename}`);
console.log(`✓ Project prefix: ${prefix}`);
console.log();

// Check for Rooms.json
if (!fs.existsSync(roomsPath)) {
    console.error(`✗ Error: Rooms file not found: ${prefix}-Rooms.json`);
    console.error(`  Expected at: ${roomsPath}`);
    console.error('');
    process.exit(1);
}

console.log(`✓ Found Rooms file: ${prefix}-Rooms.json`);
console.log();

// Create output directory structure
const projectOutputDir = path.join(outputDir, `${prefix}-output`);
const computedLayoutsDir = path.join(projectOutputDir, 'ComputedLayouts');

if (!fs.existsSync(computedLayoutsDir)) {
    fs.mkdirSync(computedLayoutsDir, { recursive: true });
}

console.log(`✓ Output directory: ${projectOutputDir}`);
console.log();

/**
 * Generate temporary config files for a project
 */
function generateConfigFiles() {
    const combinationsPath = path.join(projectOutputDir, `${prefix}-valid-combinations.json`);

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
        testName: `${prefix} - Max Inscribed (Hybrid Algorithm)`,
        algorithm: "maxinscribed",
        svgPath: svgPath,
        outputDir: path.join(computedLayoutsDir, `${prefix}-MaxInscribedResults`),
        algorithmOptions: {
            coverageThreshold: 0,
            maxTime: 5000,
            gridStep: 8.0,
            polylabelPrecision: 0.5,
            aspectRatios: [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0],
            binarySearchPrecision: 0.0001,
            centroidSamples: 1,
            debugMode: false
        },
        combinationsFile: combinationsPath
    };

    // Boardroom config
    const boardroomConfig = {
        ...baseConfig,
        testName: `${prefix} - Boardroom Layout`,
        algorithm: "boardroom",
        svgPath: svgPath,
        outputDir: path.join(computedLayoutsDir, `${prefix}-BoardroomResults`),
        algorithmOptions: {
            width: { mode: "discrete", base: 42, increment: 12, minIncrements: 0 },
            height: { mode: "discrete", base: 36, increment: 6, minIncrements: 0 },
            maxTime: 5000,
            angleSamples: 25,
            centroidSamples: 25,
            debugMode: false
        },
        combinationsFile: combinationsPath
    };

    // Hollow Square config
    const hollowSquareConfig = {
        ...baseConfig,
        testName: `${prefix} - Hollow Square Layout`,
        algorithm: "hollowsquare",
        svgPath: svgPath,
        outputDir: path.join(computedLayoutsDir, `${prefix}-HollowSquareResults`),
        algorithmOptions: {
            width: { mode: "discrete", base: 19, increment: 6, minIncrements: 0 },
            height: { mode: "discrete", base: 14, increment: 6, minIncrements: 0 },
            maxTime: 5000,
            angleSamples: 25,
            centroidSamples: 25,
            debugMode: false
        },
        combinationsFile: combinationsPath
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
        hollowSquare: hollowSquarePath,
        combinationsPath: combinationsPath
    };
}

/**
 * Main processing function
 */
async function processProject() {
    console.log('='.repeat(80));
    console.log(`PROCESSING PROJECT: ${prefix}`);
    console.log('='.repeat(80));
    console.log();

    try {
        // Step 0: Generate valid combinations from Rooms.json
        console.log('Step 0/4: Generating valid combinations...');

        const generateCombosCmd = `node compute-all-combinations.js "${prefix}" "${inputDir}" "${projectOutputDir}"`;
        await execAsync(generateCombosCmd, { cwd: __dirname });

        console.log('  ✓ Valid combinations generated\n');

        // Generate temporary config files
        const configs = generateConfigFiles();

        // Verify combinations file was created
        if (!fs.existsSync(configs.combinationsPath)) {
            throw new Error(`Failed to generate combinations file: ${configs.combinationsPath}`);
        }

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

        console.log('  ✓ All tests complete\n');

        // Step 2: Extract precomputed data
        console.log('Step 2/4: Extracting precomputed data...');

        const extractCmd = `node extract-precomputed-project.js "${prefix}" "${computedLayoutsDir}" "${configs.combinationsPath}" "${projectOutputDir}"`;
        await execAsync(extractCmd, { cwd: __dirname });

        console.log('  ✓ Data extraction complete\n');

        // Step 3: Embed data in SVG
        console.log('Step 3/4: Embedding data in SVG...');

        const embedCmd = `node embed-project.js "${prefix}" "${svgPath}" "${projectOutputDir}"`;
        await execAsync(embedCmd, { cwd: __dirname });

        console.log('  ✓ SVG embedding complete\n');

        console.log('='.repeat(80));
        console.log(`✓ PROJECT ${prefix} COMPLETE!`);
        console.log(`  Output location: ${projectOutputDir}`);
        console.log(`  Final SVG: ${path.join(projectOutputDir, `${prefix}-output.svg`)}`);
        console.log('='.repeat(80));
        console.log();

        return true;
    } catch (error) {
        console.error();
        console.error('='.repeat(80));
        console.error(`✗ PROJECT ${prefix} FAILED`);
        console.error(`  Error: ${error.message}`);
        if (error.stderr) console.error(`  Details: ${error.stderr}`);
        console.error('='.repeat(80));
        console.error();
        return false;
    }
}

// Run the processing
processProject().then(success => {
    process.exit(success ? 0 : 1);
});
