#!/usr/bin/env node

/**
 * Hollow Square Layout Test Runner
 *
 * Generates hollow square layout inscribed rectangles for all 251 valid combinations.
 * Parallel system to test-runner.js and test-runner-boardroom.js for constrained layouts.
 *
 * Hollow Square constraints:
 * - Minimum: 14 ft × 19 ft (4 tables)
 * - Both dimensions expand independently in 6ft increments
 * - Can be at any rotation
 */

import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { testCases, config } from './test-config.js';

// Load required modules
const require = createRequire(import.meta.url);

// Load SvgViewerAlgorithms (contains parsing and merging functions)
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');

// Load hollow square algorithm
const { findHollowSquareLayout } = require('../wwwroot/SvgViewerHollowSquare.js');

// Color codes for console output
const colors = {
    reset: '\x1b[0m',
    green: '\x1b[32m',
    red: '\x1b[31m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    cyan: '\x1b[36m',
    magenta: '\x1b[35m'
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

/**
 * Calculate polygon area in square SVG inches using shoelace formula
 */
function calculatePolygonAreaInSvgInches(polygon, scaleFactor = 48.345845) {
    const UNITS_PER_INCH = 96;

    // Calculate area in pre-transform coordinates using shoelace formula
    let areaPreTransform = 0;
    for (let i = 0; i < polygon.length; i++) {
        const j = (i + 1) % polygon.length;
        areaPreTransform += polygon[i].x * polygon[j].y;
        areaPreTransform -= polygon[j].x * polygon[i].y;
    }
    areaPreTransform = Math.abs(areaPreTransform) / 2;

    // Convert to square user units by scaling up
    const areaUserUnits = areaPreTransform * (scaleFactor * scaleFactor);

    // Convert to square SVG inches
    const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);

    return areaSvgInches;
}

/**
 * Calculate hollow square layout area in square SVG inches
 */
function calculateHollowSquareAreaInSvgInches(layout, scaleFactor = 48.345845) {
    const UNITS_PER_INCH = 96;

    // Hollow square area in pre-transform coordinates
    const areaPreTransform = layout.width * layout.height;

    // Convert to square user units by scaling up
    const areaUserUnits = areaPreTransform * (scaleFactor * scaleFactor);

    // Convert to square SVG inches
    const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);

    return areaSvgInches;
}

/**
 * Extract path data from SVG content
 */
function extractPathData(svgContent, pathIds) {
    const paths = [];

    for (const pathId of pathIds) {
        // First try to find a path element
        let regex = new RegExp(`<path[^>]*id="${pathId}"[^>]*d="([^"]*)"`, 'i');
        let match = svgContent.match(regex);

        if (match) {
            paths.push({
                id: pathId,
                d: match[1]
            });
            continue;
        }

        // If not found, try polygon element
        regex = new RegExp(`<polygon[^>]*id="${pathId}"[^>]*points="([^"]*)"`, 'i');
        match = svgContent.match(regex);

        if (match) {
            // Convert polygon points to SVG path
            const points = match[1].trim().split(/\s+/);
            const pathCommands = points.map((point, index) => {
                const [x, y] = point.split(',');
                return index === 0 ? `M ${x} ${y}` : `L ${x} ${y}`;
            }).join(' ') + ' Z';

            paths.push({
                id: pathId,
                d: pathCommands
            });
        }
    }

    return paths;
}

/**
 * Process a single test case
 */
function processTestCase(testCase) {
    log(`\n${'='.repeat(80)}`, 'cyan');
    log(`Test: ${testCase.name}`, 'cyan');
    log(`Paths: ${testCase.paths.join(', ')}`, 'cyan');
    log(`${'='.repeat(80)}`, 'cyan');

    // Read SVG file
    const svgPath = config.svgPath;
    const svgContent = fs.readFileSync(svgPath, 'utf8');

    // Extract path data
    log(`\nExtracting path data...`, 'blue');
    const pathData = extractPathData(svgContent, testCase.paths);

    if (pathData.length !== testCase.paths.length) {
        log(`  ❌ Could not find all paths. Found ${pathData.length}/${testCase.paths.length}`, 'red');
        return { success: false, error: 'Missing paths' };
    }
    log(`  ✓ Found ${pathData.length} paths`, 'green');

    // Parse paths into line segments
    log(`\nParsing paths into line segments...`, 'blue');
    const lineSegmentPaths = [];

    for (let i = 0; i < pathData.length; i++) {
        const pathD = pathData[i].d;
        const segments = SvgViewerAlgorithms.parsePathToLineSegments(pathD, i);

        lineSegmentPaths.push({
            pathIdx: i,
            pathId: pathData[i].id,
            segments: segments
        });
        log(`    - ${pathData[i].id}: ${segments.length} segments`, 'blue');
    }

    // Merge coincident points and create network
    log(`  Merging coincident points...`, 'blue');
    const allSegments = lineSegmentPaths.flatMap(p => p.segments);
    const mergedSegments = SvgViewerAlgorithms.mergeCoincidentPoints(allSegments, 0.5);
    log(`    Merged ${allSegments.length} segments into ${mergedSegments.length} unique segments`, 'blue');

    // Mark shared segments
    log(`  Marking shared segments...`, 'blue');
    const markedSegments = SvgViewerAlgorithms.markSharedSegments(mergedSegments, 0.5);

    // Join paths into network
    log(`  Joining paths into network...`, 'blue');
    const pathNetwork = SvgViewerAlgorithms.joinPathsIntoNetwork(markedSegments);

    // Traverse outer edge to get polygon boundary
    log(`  Traversing outer edge...`, 'blue');
    const polygon = SvgViewerAlgorithms.traverseOuterEdge(pathNetwork);

    if (!polygon || polygon.length < 3) {
        log(`  ❌ Failed to find polygon boundary`, 'red');
        return { success: false, error: 'Failed to find polygon boundary' };
    }

    log(`  ✓ Polygon boundary: ${polygon.length} vertices`, 'green');

    // Calculate polygon area in square SVG inches
    const polygonArea = calculatePolygonAreaInSvgInches(polygon);
    log(`  ✓ Polygon area: ${polygonArea.toFixed(2)} square SVG inches`, 'green');

    // Run hollow square layout algorithm
    log(`\n  Finding best hollow square layout...`, 'cyan');
    const startTime = performance.now();

    const hollowSquareOptions = {
        maxLengthRun: 20,          // Maximum lengthRun to test
        maxDepthRun: 20,           // Maximum depthRun to test
        angleSamples: 90,          // Test every 2 degrees (0-180°)
        centroidSamples: 25,       // 5x5 grid of centroids
        debugMode: false
    };

    const hollowSquareLayout = findHollowSquareLayout(polygon, hollowSquareOptions);
    const endTime = performance.now();
    const computationTime = endTime - startTime;

    if (!hollowSquareLayout) {
        log(`  ❌ No valid hollow square layout found`, 'red');
        return {
            success: false,
            error: 'No valid hollow square layout found',
            polygon,
            polygonArea,
            pathCount: pathData.length,
            vertexCount: polygon.length,
            pathData,
            svgContent
        };
    }

    // Calculate hollow square area in square SVG inches
    const hollowSquareArea = calculateHollowSquareAreaInSvgInches(hollowSquareLayout);

    log(`\n  ✓ Hollow Square Layout Found:`, 'green');
    log(`    Tables: ${hollowSquareLayout.tables}`, 'yellow');
    log(`    LengthRun: ${hollowSquareLayout.lengthRun}, DepthRun: ${hollowSquareLayout.depthRun}`, 'yellow');
    log(`    Dimensions: ${hollowSquareLayout.width.toFixed(1)} × ${hollowSquareLayout.height.toFixed(1)} ft`, 'yellow');
    log(`    Area: ${hollowSquareLayout.area.toFixed(1)} sq ft (${hollowSquareArea.toFixed(2)} sq inches)`, 'yellow');
    log(`    Angle: ${hollowSquareLayout.angle.toFixed(1)}°`, 'yellow');
    log(`    Time: ${computationTime.toFixed(1)} ms`, 'yellow');
    log(`    Fill ratio: ${(hollowSquareArea / polygonArea * 100).toFixed(1)}%`, 'yellow');

    return {
        success: true,
        polygon,
        polygonArea,
        hollowSquareLayout,
        hollowSquareArea,
        computationTime,
        pathCount: pathData.length,
        vertexCount: polygon.length,
        pathData,
        svgContent
    };
}

/**
 * Calculate bounds for viewBox
 */
function calculateBounds(polygon) {
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity, maxY = -Infinity;

    for (const point of polygon) {
        minX = Math.min(minX, point.x);
        maxX = Math.max(maxX, point.x);
        minY = Math.min(minY, point.y);
        maxY = Math.max(maxY, point.y);
    }

    return { minX, maxX, minY, maxY };
}

/**
 * Extract test paths content for visualization
 */
function extractTestPathsContent(pathData) {
    const paths = pathData.map(p => {
        return `<path id="${p.id}" d="${p.d}" fill="none" stroke="#808080" stroke-width="1"/>`;
    });
    return paths.join('\n    ');
}

/**
 * Generate SVG visualization for hollow square layout result
 */
function generateSvgVisualization(testCase, result) {
    const { polygon, polygonArea, hollowSquareLayout, hollowSquareArea, computationTime, pathData } = result;

    // Calculate bounds
    const bounds = calculateBounds(polygon);
    const padding = 50;
    const tableHeight = 120; // Space for table at bottom
    const compactTableWidth = 300;
    const keyHeight = 80;
    const horizontalMargin = 20;

    const vbX = bounds.minX - padding;
    const vbY = bounds.minY - padding;
    const polygonWidth = bounds.maxX - bounds.minX + 2 * padding;
    const requiredWidth = compactTableWidth + horizontalMargin * 2;
    const vbWidth = Math.max(polygonWidth, requiredWidth);
    const vbHeight = bounds.maxY - bounds.minY + 2 * padding + keyHeight + tableHeight;

    // Create polygon points string
    const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');

    // Create hollow square rectangle points string
    const corners = hollowSquareLayout.corners;
    const hollowSquarePoints = corners.map(c => `${c.x},${c.y}`).join(' ');

    // Create centroid marker
    const c = hollowSquareLayout.centroid;

    // Extract original test paths
    const testPaths = extractTestPathsContent(pathData);

    // Calculate fill ratio
    const fillRatio = (hollowSquareArea / polygonArea * 100).toFixed(1);

    // Table positioned at bottom of SVG
    const tableY = bounds.maxY - bounds.minY + 2 * padding + keyHeight + 10;

    const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg"
     viewBox="${vbX} ${vbY} ${vbWidth} ${vbHeight}"
     width="${vbWidth}" height="${vbHeight}"
     style="background: white;">

  <!-- Title -->
  <text x="${vbX + 10}" y="${vbY + 20}" font-size="16" font-weight="bold" fill="#000">
    ${testCase.name}
  </text>

  <!-- Original test paths (gray, semi-transparent) -->
  <g opacity="0.3">
    ${testPaths}
  </g>

  <!-- Boundary polygon (blue, dashed) -->
  <polygon points="${polygonPoints}"
           fill="none"
           stroke="#4080ff"
           stroke-width="2"
           stroke-dasharray="5,5"/>

  <!-- Hollow square layout (purple fill, solid border) -->
  <polygon points="${hollowSquarePoints}"
           fill="rgba(147, 51, 234, 0.15)"
           stroke="#9333ea"
           stroke-width="3"/>

  <!-- Centroid marker (purple circle) -->
  <circle cx="${c.x}" cy="${c.y}" r="4" fill="#9333ea"/>
  <text x="${c.x + 8}" y="${c.y + 5}" font-size="10" fill="#9333ea">hollow square</text>

  <!-- Corner markers -->
  ${corners.map((corner, i) => `
  <circle cx="${corner.x}" cy="${corner.y}" r="3" fill="#9333ea"/>
  <text x="${corner.x + 6}" y="${corner.y + 4}" font-size="8" fill="#9333ea">hs${i}</text>
  `).join('')}

  <!-- Polygon vertices -->
  ${polygon.map((p, i) => `
  <circle cx="${p.x}" cy="${p.y}" r="2" fill="#4080ff"/>
  <text x="${p.x - 8}" y="${p.y - 6}" font-size="8" fill="#4080ff">p${i}</text>
  `).join('')}

  <!-- Comparison Table (compact, positioned at bottom) -->
  <g transform="translate(${vbX + horizontalMargin}, ${tableY})">
    <!-- Table border -->
    <rect x="0" y="0" width="${compactTableWidth}" height="100" fill="white" stroke="#ccc" stroke-width="1"/>

    <!-- Header -->
    <rect x="0" y="0" width="${compactTableWidth}" height="25" fill="#f0f0f0"/>
    <text x="${compactTableWidth / 2}" y="17" font-size="12" font-weight="bold" text-anchor="middle">Hollow Square Layout</text>

    <!-- Row 1: Tables -->
    <text x="10" y="42" font-size="10" font-weight="bold">Tables:</text>
    <text x="${compactTableWidth - 10}" y="42" font-size="10" text-anchor="end">${hollowSquareLayout.tables} (${hollowSquareLayout.lengthRun}L × ${hollowSquareLayout.depthRun}D)</text>

    <!-- Row 2: Dimensions -->
    <text x="10" y="60" font-size="10" font-weight="bold">Dimensions:</text>
    <text x="${compactTableWidth - 10}" y="60" font-size="10" text-anchor="end">${hollowSquareLayout.width.toFixed(1)} × ${hollowSquareLayout.height.toFixed(1)} ft</text>

    <!-- Row 3: Areas -->
    <text x="10" y="78" font-size="10" font-weight="bold">Area:</text>
    <text x="${compactTableWidth - 10}" y="78" font-size="10" text-anchor="end">${hollowSquareArea.toFixed(2)} sq in (${fillRatio}%)</text>

    <!-- Row 4: Polygon Area -->
    <text x="10" y="95" font-size="10" font-weight="bold">Polygon:</text>
    <text x="${compactTableWidth - 10}" y="95" font-size="10" text-anchor="end">${polygonArea.toFixed(2)} sq in</text>
  </g>

  <!-- Area Data (embedded as XML comments for extraction) -->
  <!-- Area Data (Square SVG Inches) -->
  <!-- polygonArea: ${polygonArea.toFixed(4)} -->
  <!-- hollowSquareArea: ${hollowSquareArea.toFixed(4)} -->
  <!-- fillRatio: ${fillRatio} -->

</svg>`;

    return svg;
}

/**
 * Main execution
 */
async function main() {
    log('\n' + '='.repeat(80), 'magenta');
    log('HOLLOW SQUARE LAYOUT TEST RUNNER', 'magenta');
    log('='.repeat(80) + '\n', 'magenta');

    // Create output directory
    const outputDir = '../TestResults/HollowSquareResults';
    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    log(`Output directory: ${outputDir}\n`, 'blue');
    log(`Running ${testCases.length} hollow square layout tests...\n`, 'blue');

    // Process all test cases
    const results = [];
    let successCount = 0;
    let failureCount = 0;
    const startTime = Date.now();

    for (let i = 0; i < testCases.length; i++) {
        const testCase = testCases[i];

        log(`\n[${i + 1}/${testCases.length}] ${testCase.name}`, 'cyan');

        try {
            const result = processTestCase(testCase);

            if (result.success) {
                // Generate SVG visualization
                const svg = generateSvgVisualization(testCase, result);
                const outputPath = path.join(outputDir, `${testCase.name}.svg`);
                fs.writeFileSync(outputPath, svg, 'utf8');

                successCount++;
                results.push({
                    testCase: testCase.name,
                    pathIds: testCase.paths,
                    success: true,
                    tables: result.hollowSquareLayout.tables,
                    lengthRun: result.hollowSquareLayout.lengthRun,
                    depthRun: result.hollowSquareLayout.depthRun,
                    width: result.hollowSquareLayout.width,
                    height: result.hollowSquareLayout.height,
                    area: result.hollowSquareArea,
                    polygonArea: result.polygonArea,
                    fillRatio: (result.hollowSquareArea / result.polygonArea * 100).toFixed(1),
                    computationTime: result.computationTime
                });

                log(`  ✓ Saved to ${path.basename(outputPath)}`, 'green');
            } else {
                failureCount++;
                results.push({
                    testCase: testCase.name,
                    pathIds: testCase.paths,
                    success: false,
                    error: result.error
                });
                log(`  ❌ Failed: ${result.error}`, 'red');
            }
        } catch (error) {
            failureCount++;
            log(`  ❌ Exception: ${error.message}`, 'red');
            console.error(error);
            results.push({
                testCase: testCase.name,
                pathIds: testCase.paths,
                success: false,
                error: error.message
            });
        }
    }

    const endTime = Date.now();
    const totalTime = (endTime - startTime) / 1000;

    // Write summary
    const summaryPath = path.join(outputDir, 'hollowsquare-summary.json');
    const successfulResults = results.filter(r => r.success);
    const totalComputationTime = successfulResults.reduce((sum, r) => sum + r.computationTime, 0);
    const avgComputationTime = successCount > 0 ? totalComputationTime / successCount : 0;

    fs.writeFileSync(summaryPath, JSON.stringify({
        timestamp: new Date().toISOString(),
        totalTests: results.length,
        successful: successCount,
        failed: failureCount,
        totalTimeSeconds: totalTime,
        totalComputationTimeMs: totalComputationTime,
        averageComputationTimeMs: avgComputationTime,
        results: results
    }, null, 2), 'utf8');

    log('\n' + '='.repeat(80), 'magenta');
    log('TEST SUMMARY', 'magenta');
    log('='.repeat(80), 'magenta');
    log(`Total tests: ${testCases.length}`, 'cyan');
    log(`Successful: ${successCount}`, 'green');
    log(`Failed: ${failureCount}`, 'red');
    log(`Total time: ${totalTime.toFixed(1)} seconds`, 'cyan');

    if (successCount > 0) {
        log(`\nComputation statistics:`, 'yellow');
        log(`  Total computation time: ${totalComputationTime.toFixed(1)} ms`, 'yellow');
        log(`  Average per test: ${avgComputationTime.toFixed(1)} ms`, 'yellow');
    }

    log(`\nOutput directory: ${outputDir}`, 'cyan');
    log(`Summary: ${summaryPath}`, 'cyan');
    log('='.repeat(80) + '\n', 'magenta');
}

main().catch(error => {
    log(`Fatal error: ${error.message}`, 'red');
    console.error(error);
    process.exit(1);
});
