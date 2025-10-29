// Extract precomputed data for a specific project
// Wrapper around extract-all-precomputed.js with project-specific paths

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Get arguments: prefix, testResultsDir, combinationsPath, outputDir
const args = process.argv.slice(2);

if (args.length < 4) {
    console.error('Usage: node extract-precomputed-project.js <prefix> <testResultsDir> <combinationsPath> <outputDir>');
    process.exit(1);
}

const [prefix, testResultsDir, combinationsPath, projectOutputDir] = args;

console.log(`Extracting precomputed data for project: ${prefix}`);
console.log(`  Test Results: ${testResultsDir}`);
console.log(`  Combinations: ${combinationsPath}`);
console.log(`  Output: ${projectOutputDir}`);
console.log();

// Load combinations
const combinationsData = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));
const combinations = combinationsData.combinations;

console.log(`  Loaded ${combinations.length} combinations`);
console.log();

/**
 * Extract layout data from JSON files in a directory
 */
function extractLayoutsFromDir(dirPath, filePrefix, combinations) {
    const layouts = [];
    let successCount = 0;
    let failCount = 0;

    console.log(`  Processing ${path.basename(dirPath)}...`);

    for (let i = 0; i < combinations.length; i++) {
        const combo = combinations[i];
        const testName = `${filePrefix}_${combo.id}`;
        const jsonPath = path.join(dirPath, `${testName}.json`);

        if (!fs.existsSync(jsonPath)) {
            failCount++;
            continue;
        }

        try {
            const data = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
            const key = combo.sections.slice().sort().join('_');

            layouts.push({
                key,
                sections: combo.sections,
                layout: data.layout,
                polygonArea: data.polygonArea,
                fillRatio: data.fillRatio,
                runtimeMs: data.runtimeMs
            });

            successCount++;
        } catch (error) {
            failCount++;
        }
    }

    console.log(`    ✓ Extracted ${successCount} layouts`);
    if (failCount > 0) {
        console.log(`    ⚠ Failed: ${failCount} layouts`);
    }

    return layouts;
}

/**
 * Calculate statistics
 */
function calculateStatistics(layouts) {
    const totalTime = layouts.reduce((sum, l) => sum + (l.runtimeMs || 0), 0);
    const avgTime = layouts.length > 0 ? totalTime / layouts.length : 0;

    return {
        totalLayouts: layouts.length,
        totalComputationTimeMs: totalTime,
        averageComputationTimeMs: avgTime,
        totalComputationTimeSec: (totalTime / 1000).toFixed(1),
        averageComputationTimeSec: (avgTime / 1000).toFixed(3)
    };
}

// Extract MaxInscribed rectangles
console.log('Extracting MaxInscribed rectangles...');
const maxInscribedPath = path.join(testResultsDir, `${prefix}-MaxInscribedResults`);
const rectangles = extractLayoutsFromDir(maxInscribedPath, `MaxInscribed`, combinations);
const rectStats = calculateStatistics(rectangles);

// Extract Boardroom layouts
console.log('Extracting Boardroom layouts...');
const boardroomPath = path.join(testResultsDir, `${prefix}-BoardroomResults`);
const boardroomLayouts = extractLayoutsFromDir(boardroomPath, `Boardroom`, combinations);
const boardroomStats = calculateStatistics(boardroomLayouts);

// Extract HollowSquare layouts
console.log('Extracting HollowSquare layouts...');
const hollowSquarePath = path.join(testResultsDir, `${prefix}-HollowSquareResults`);
const hollowSquareLayouts = extractLayoutsFromDir(hollowSquarePath, `HollowSquare`, combinations);
const hollowSquareStats = calculateStatistics(hollowSquareLayouts);

console.log();
console.log('Generating output files...');

// MaxInscribed output
const maxInscribedOutput = {
    generatedAt: new Date().toISOString(),
    project: prefix,
    totalCombinations: combinations.length,
    successfulComputations: rectangles.length,
    failedComputations: combinations.length - rectangles.length,
    statistics: rectStats,
    rectangles: rectangles.map(r => ({
        key: r.key,
        sections: r.sections,
        rectangle: r.layout,
        polygonArea: r.polygonArea,
        rectangleArea: r.layout.area,
        computationTimeMs: r.runtimeMs
    }))
};

const rectOutputPath = path.join(projectOutputDir, `${prefix}-rectangles.json`);
fs.writeFileSync(rectOutputPath, JSON.stringify(maxInscribedOutput, null, 2));
const rectSize = (fs.statSync(rectOutputPath).size / 1024).toFixed(1);
console.log(`  ✓ ${prefix}-rectangles.json (${rectSize} KB)`);

// Boardroom output
const boardroomOutput = {
    generatedAt: new Date().toISOString(),
    project: prefix,
    totalCombinations: combinations.length,
    successfulComputations: boardroomLayouts.length,
    failedComputations: combinations.length - boardroomLayouts.length,
    statistics: boardroomStats,
    boardroomLayouts: boardroomLayouts.map(b => ({
        key: b.key,
        sections: b.sections,
        boardroomLayout: b.layout,
        polygonArea: b.polygonArea,
        boardroomArea: b.layout.area,
        computationTimeMs: b.runtimeMs
    }))
};

const boardroomOutputPath = path.join(projectOutputDir, `${prefix}-boardroom.json`);
fs.writeFileSync(boardroomOutputPath, JSON.stringify(boardroomOutput, null, 2));
const boardroomSize = (fs.statSync(boardroomOutputPath).size / 1024).toFixed(1);
console.log(`  ✓ ${prefix}-boardroom.json (${boardroomSize} KB)`);

// HollowSquare output
const hollowSquareOutput = {
    generatedAt: new Date().toISOString(),
    project: prefix,
    totalCombinations: combinations.length,
    successfulComputations: hollowSquareLayouts.length,
    failedComputations: combinations.length - hollowSquareLayouts.length,
    statistics: hollowSquareStats,
    hollowSquareLayouts: hollowSquareLayouts.map(h => ({
        key: h.key,
        sections: h.sections,
        hollowSquareLayout: h.layout,
        polygonArea: h.polygonArea,
        hollowSquareArea: h.layout.area,
        computationTimeMs: h.runtimeMs
    }))
};

const hollowSquareOutputPath = path.join(projectOutputDir, `${prefix}-hollowsquare.json`);
fs.writeFileSync(hollowSquareOutputPath, JSON.stringify(hollowSquareOutput, null, 2));
const hollowSquareSize = (fs.statSync(hollowSquareOutputPath).size / 1024).toFixed(1);
console.log(`  ✓ ${prefix}-hollowsquare.json (${hollowSquareSize} KB)`);

const totalSize = parseFloat(rectSize) + parseFloat(boardroomSize) + parseFloat(hollowSquareSize);
console.log();
console.log(`Total data size: ${totalSize.toFixed(1)} KB`);
