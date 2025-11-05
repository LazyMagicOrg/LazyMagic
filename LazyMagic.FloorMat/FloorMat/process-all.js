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
            dataPath: path.join(inputDir, `${prefix}-data.json`),
            combinationsPath: path.join(outputDir, `${prefix}-valid-combinations.json`)
        };
    });
}

/**
 * Generate temporary config files for a project
 */
function generateConfigFiles(project) {
    const projectOutputDir = outputDir;
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

    // U-Shape config
    const ushapeConfig = {
        ...baseConfig,
        testName: `${project.prefix} - U-Shape Layout`,
        algorithm: "ushape",
        svgPath: project.svgPath,
        outputDir: path.join(computedLayoutsDir, `${project.prefix}-UShapeResults`),
        algorithmOptions: {
            width: { mode: "discrete", base: 16.5, increment: 6, minIncrements: 0 },
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
    const ushapePath = path.join(tempConfigDir, 'ushape.json');

    fs.writeFileSync(maxInscribedPath, JSON.stringify(maxInscribedConfig, null, 2));
    fs.writeFileSync(boardroomPath, JSON.stringify(boardroomConfig, null, 2));
    fs.writeFileSync(hollowSquarePath, JSON.stringify(hollowSquareConfig, null, 2));
    fs.writeFileSync(ushapePath, JSON.stringify(ushapeConfig, null, 2));

    return {
        maxInscribed: maxInscribedPath,
        boardroom: boardroomPath,
        hollowSquare: hollowSquarePath,
        ushape: ushapePath
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

    // Check if data.json file exists
    if (!fs.existsSync(project.dataPath)) {
        console.error(`✗ Error: Data file not found: ${project.prefix}-data.json`);
        console.error(`  Expected at: ${project.dataPath}`);
        console.error(`  Skipping project ${project.prefix}`);
        return false;
    }

    const projectOutputDir = outputDir;

    console.log(`✓ Output directory: output/`);
    console.log();

    try {
        // Step 0: Generate valid combinations from level data
        console.log('Step 0/5: Generating valid combinations...');

        const generateCombosCmd = `node compute-all-combinations.js "${project.prefix}" "${inputDir}" "${projectOutputDir}"`;
        await execAsync(generateCombosCmd, { cwd: __dirname });

        console.log('  ✓ Valid combinations generated\\n');

        // Verify combinations file was created
        if (!fs.existsSync(project.combinationsPath)) {
            throw new Error(`Failed to generate combinations file: ${project.combinationsPath}`);
        }

        // Generate temporary config files
        const configs = generateConfigFiles(project);

        // Step 1: Run tests for all four algorithms IN PARALLEL
        console.log('Step 1/5: Running tests (MaxInscribed, Boardroom, Hollow Square, U-Shape) in parallel...');
        console.log();

        // Execute all four algorithms simultaneously for ~4x speedup
        const execOptions = {
            cwd: __dirname,
            maxBuffer: 200 * 1024 * 1024 // 200MB buffer for verbose algorithm logging
        };

        const [maxResult, boardroomResult, hollowResult, ushapeResult] = await Promise.all([
            execAsync(`node run-tests.js "${configs.maxInscribed}"`, execOptions)
                .then(() => console.log('  ✓ MaxInscribed tests complete'))
                .catch(err => { throw new Error(`MaxInscribed failed: ${err.message}`); }),
            execAsync(`node run-tests.js "${configs.boardroom}"`, execOptions)
                .then(() => console.log('  ✓ Boardroom tests complete'))
                .catch(err => { throw new Error(`Boardroom failed: ${err.message}`); }),
            execAsync(`node run-tests.js "${configs.hollowSquare}"`, execOptions)
                .then(() => console.log('  ✓ Hollow Square tests complete'))
                .catch(err => { throw new Error(`Hollow Square failed: ${err.message}`); }),
            execAsync(`node run-tests.js "${configs.ushape}"`, execOptions)
                .then(() => console.log('  ✓ U-Shape tests complete'))
                .catch(err => { throw new Error(`U-Shape failed: ${err.message}`); })
        ]);

        console.log('  ✓ All tests complete\\n');

        // Step 2: Extract precomputed data
        console.log('Step 2/5: Extracting precomputed data...');

        const computedLayoutsDir = path.join(projectOutputDir, 'ComputedLayouts');
        const extractCmd = `node extract-precomputed-project.js "${project.prefix}" "${computedLayoutsDir}" "${project.combinationsPath}" "${projectOutputDir}"`;
        await execAsync(extractCmd, { cwd: __dirname });

        console.log('  ✓ Data extraction complete\\n');

        // Step 3: Calculate polygon areas
        console.log('Step 3/6: Calculating polygon areas...');

        const dataWithAreasPath = path.join(projectOutputDir, `${project.prefix}-data-with-areas.json`);
        const calculateAreasCmd = `node calculate-polygon-areas.js "${project.svgPath}" "${project.dataPath}" "${dataWithAreasPath}"`;

        const calculateAreasResult = await execAsync(calculateAreasCmd, { cwd: __dirname });

        // Show output from calculate-polygon-areas script
        if (calculateAreasResult.stdout) {
            console.log(calculateAreasResult.stdout);
        }
        if (calculateAreasResult.stderr) {
            console.error('  ⚠ stderr:', calculateAreasResult.stderr);
        }

        // Verify the output file was created
        if (!fs.existsSync(dataWithAreasPath)) {
            throw new Error(`Polygon area calculation failed: output file not created at ${dataWithAreasPath}`);
        }

        console.log('  ✓ Polygon areas calculated\\n');

        // Step 4: Embed path metadata from level data (with polygon areas)
        console.log('Step 4/6: Embedding path metadata...');

        const metadataSvgPath = path.join(projectOutputDir, `${project.prefix}-with-metadata.svg`);
        const embedMetadataCmd = `node embed-path-metadata.js "${project.prefix}" "${project.svgPath}" "${dataWithAreasPath}" "${metadataSvgPath}"`;

        const embedMetadataResult = await execAsync(embedMetadataCmd, { cwd: __dirname });

        // Show output from embed-path-metadata script
        if (embedMetadataResult.stdout) {
            console.log(embedMetadataResult.stdout);
        }
        if (embedMetadataResult.stderr) {
            console.error('  ⚠ stderr:', embedMetadataResult.stderr);
        }

        // Verify the output file was created
        if (!fs.existsSync(metadataSvgPath)) {
            throw new Error(`Metadata embedding failed: output file not created at ${metadataSvgPath}`);
        }

        console.log('  ✓ Path metadata embedded\\n');

        // Step 5: Embed layout data in SVG
        console.log('Step 5/6: Embedding layout data in SVG...');

        const embedCmd = `node embed-project.js "${project.prefix}" "${metadataSvgPath}" "${projectOutputDir}"`;
        await execAsync(embedCmd, { cwd: __dirname });

        console.log('  ✓ SVG embedding complete\\n');

        // Step 6: Clean up intermediate files
        console.log('Step 6/6: Cleaning up intermediate files...');

        let cleanedCount = 0;

        if (fs.existsSync(metadataSvgPath)) {
            fs.unlinkSync(metadataSvgPath);
            cleanedCount++;
        }

        if (fs.existsSync(dataWithAreasPath)) {
            fs.unlinkSync(dataWithAreasPath);
            cleanedCount++;
        }

        console.log(`  ✓ Cleaned up ${cleanedCount} intermediate file(s)\\n`);

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
