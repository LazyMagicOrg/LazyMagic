// Extract all pre-computed layout data from test results
// Generates three JSON files: precomputed-rectangles.json, precomputed-boardroom.json, precomputed-hollowsquare.json

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const testResultsDir = path.join(__dirname, '../TestResults');
const combinationsPath = path.join(__dirname, 'valid-combinations.json');

/**
 * Extract layout data from JSON files in a directory
 * @param {string} dirPath - Directory containing JSON test results
 * @param {string} prefix - File prefix (e.g., "MaxInscribed", "Boardroom")
 * @param {Array} combinations - Array of combination objects
 * @returns {Array} Array of extracted layout data
 */
function extractLayoutsFromDir(dirPath, prefix, combinations) {
    const layouts = [];
    let successCount = 0;
    let failCount = 0;

    console.log(`\nProcessing ${prefix} results from ${path.basename(dirPath)}...`);

    for (let i = 0; i < combinations.length; i++) {
        const combo = combinations[i];
        const testName = `${prefix}_${String(i + 1).padStart(4, '0')}`;
        const jsonPath = path.join(dirPath, `${testName}.json`);

        if (!fs.existsSync(jsonPath)) {
            console.warn(`  ⚠ JSON not found: ${testName}.json`);
            failCount++;
            continue;
        }

        try {
            const data = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));

            // Create sorted key from sections for lookup
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
            console.warn(`  ⚠ Failed to parse: ${testName}.json - ${error.message}`);
            failCount++;
        }

        // Progress indicator
        if ((i + 1) % 50 === 0) {
            console.log(`  Processed ${i + 1}/${combinations.length} files...`);
        }
    }

    console.log(`  ✓ Successfully extracted ${successCount} layouts`);
    if (failCount > 0) {
        console.log(`  ⚠ Failed to extract ${failCount} layouts`);
    }

    return layouts;
}

/**
 * Calculate statistics for extracted layouts
 * @param {Array} layouts - Array of layout objects
 * @param {string} areaField - Field name for area (e.g., "area", "area", "area")
 * @returns {Object} Statistics object
 */
function calculateStatistics(layouts, areaField = 'area') {
    const totalTime = layouts.reduce((sum, l) => sum + (l.runtimeMs || 0), 0);
    const avgTime = layouts.length > 0 ? totalTime / layouts.length : 0;

    const stats = {
        totalLayouts: layouts.length,
        totalComputationTimeMs: totalTime,
        averageComputationTimeMs: avgTime,
        totalComputationTimeSec: (totalTime / 1000).toFixed(1),
        averageComputationTimeSec: (avgTime / 1000).toFixed(3)
    };

    // Calculate area statistics if layouts exist
    if (layouts.length > 0) {
        const areas = layouts.map(l => l.layout[areaField] || 0);
        stats.minArea = Math.min(...areas);
        stats.maxArea = Math.max(...areas);
        stats.avgArea = areas.reduce((sum, a) => sum + a, 0) / areas.length;
    }

    return stats;
}

/**
 * Main extraction function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('EXTRACTING ALL PRE-COMPUTED LAYOUT DATA');
    console.log('='.repeat(80));

    // Step 1: Load combinations
    console.log('\nStep 1: Loading valid combinations...');
    const combinationsData = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));
    const combinations = combinationsData.combinations;
    console.log(`  Loaded ${combinations.length} combinations`);

    // Step 2: Extract MaxInscribed rectangles
    console.log('\nStep 2: Extracting MaxInscribed rectangles...');
    const maxInscribedPath = path.join(testResultsDir, 'MaxInscribedResults');
    const rectangles = extractLayoutsFromDir(maxInscribedPath, 'MaxInscribed', combinations);
    const rectStats = calculateStatistics(rectangles);

    // Step 3: Extract Boardroom layouts
    console.log('\nStep 3: Extracting Boardroom layouts...');
    const boardroomPath = path.join(testResultsDir, 'BoardroomResults');
    const boardroomLayouts = extractLayoutsFromDir(boardroomPath, 'Boardroom', combinations);
    const boardroomStats = calculateStatistics(boardroomLayouts);

    // Step 4: Extract HollowSquare layouts
    console.log('\nStep 4: Extracting HollowSquare layouts...');
    const hollowSquarePath = path.join(testResultsDir, 'HollowSquareResults');
    const hollowSquareLayouts = extractLayoutsFromDir(hollowSquarePath, 'HollowSquare', combinations);
    const hollowSquareStats = calculateStatistics(hollowSquareLayouts);

    // Step 5: Generate output files
    console.log('\nStep 5: Generating precomputed JSON files...');

    // MaxInscribed output
    const maxInscribedOutput = {
        generatedAt: new Date().toISOString(),
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

    const rectOutputPath = path.join(__dirname, 'precomputed-rectangles.json');
    fs.writeFileSync(rectOutputPath, JSON.stringify(maxInscribedOutput, null, 2));
    const rectSize = (fs.statSync(rectOutputPath).size / 1024).toFixed(1);
    console.log(`  ✓ Saved: precomputed-rectangles.json (${rectSize} KB)`);

    // Boardroom output
    const boardroomOutput = {
        generatedAt: new Date().toISOString(),
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

    const boardroomOutputPath = path.join(__dirname, 'precomputed-boardroom.json');
    fs.writeFileSync(boardroomOutputPath, JSON.stringify(boardroomOutput, null, 2));
    const boardroomSize = (fs.statSync(boardroomOutputPath).size / 1024).toFixed(1);
    console.log(`  ✓ Saved: precomputed-boardroom.json (${boardroomSize} KB)`);

    // HollowSquare output
    const hollowSquareOutput = {
        generatedAt: new Date().toISOString(),
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

    const hollowSquareOutputPath = path.join(__dirname, 'precomputed-hollowsquare.json');
    fs.writeFileSync(hollowSquareOutputPath, JSON.stringify(hollowSquareOutput, null, 2));
    const hollowSquareSize = (fs.statSync(hollowSquareOutputPath).size / 1024).toFixed(1);
    console.log(`  ✓ Saved: precomputed-hollowsquare.json (${hollowSquareSize} KB)`);

    // Summary
    console.log('\n' + '='.repeat(80));
    console.log('EXTRACTION COMPLETE!');
    console.log('='.repeat(80));
    console.log(`\nMaxInscribed Rectangles: ${rectangles.length}/${combinations.length} (${rectSize} KB)`);
    console.log(`  - Total computation time: ${rectStats.totalComputationTimeSec}s`);
    console.log(`  - Average computation time: ${rectStats.averageComputationTimeSec}s`);

    console.log(`\nBoardroom Layouts: ${boardroomLayouts.length}/${combinations.length} (${boardroomSize} KB)`);
    console.log(`  - Total computation time: ${boardroomStats.totalComputationTimeSec}s`);
    console.log(`  - Average computation time: ${boardroomStats.averageComputationTimeSec}s`);

    console.log(`\nHollowSquare Layouts: ${hollowSquareLayouts.length}/${combinations.length} (${hollowSquareSize} KB)`);
    console.log(`  - Total computation time: ${hollowSquareStats.totalComputationTimeSec}s`);
    console.log(`  - Average computation time: ${hollowSquareStats.averageComputationTimeSec}s`);

    const totalSize = parseFloat(rectSize) + parseFloat(boardroomSize) + parseFloat(hollowSquareSize);
    console.log(`\nTotal data size: ${totalSize.toFixed(1)} KB`);
    console.log('='.repeat(80));
}

main().catch(console.error);
