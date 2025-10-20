#!/usr/bin/env node

/**
 * Boardroom Layout Sample Test Runner
 * Tests only specific combos that show edge breach issues
 */

import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { testCases, config } from './test-config.js';

const require = createRequire(import.meta.url);

// Load required modules
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');
const { findBoardroomLayout } = require('../wwwroot/SvgViewerBoardroom.js');

// Define specific combo indices to test (1-based)
const SAMPLE_COMBOS = [
    21, 22, 36, 38, 42, 43, 46, 55, 67, 71, 72, 75, 84, 95, 98, 100,
    108, 110, 114, 115, 118, 127, 134, 135, 138, 147, 152, 153, 154, 157,
    176, 178, 181, 187, 192, 200, 205, 211, 223, 226, 246
];

// Filter test cases to only include sample combos
const sampleTestCases = testCases.filter((testCase, index) => {
    return SAMPLE_COMBOS.includes(index + 1);
});

console.log(`\nFiltered ${testCases.length} combos down to ${sampleTestCases.length} sample combos for testing\n`);

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
    let areaPreTransform = 0;
    for (let i = 0; i < polygon.length; i++) {
        const j = (i + 1) % polygon.length;
        areaPreTransform += polygon[i].x * polygon[j].y;
        areaPreTransform -= polygon[j].x * polygon[i].y;
    }
    areaPreTransform = Math.abs(areaPreTransform) / 2;
    const areaUserUnits = areaPreTransform * (scaleFactor * scaleFactor);
    const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);
    return areaSvgInches;
}

/**
 * Calculate boardroom layout area in square SVG inches
 */
function calculateBoardroomAreaInSvgInches(layout, scaleFactor = 48.345845) {
    const UNITS_PER_INCH = 96;
    const areaPreTransform = layout.width * layout.height;
    const areaUserUnits = areaPreTransform * (scaleFactor * scaleFactor);
    const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);
    return areaSvgInches;
}

/**
 * Extract path data from SVG content (simplified version)
 */
function extractPathData(svgContent, pathIds) {
    const paths = [];
    for (const pathId of pathIds) {
        let regex = new RegExp(`<path[^>]*id="${pathId}"[^>]*d="([^"]*)"`, 'i');
        let match = svgContent.match(regex);

        if (match) {
            paths.push({ id: pathId, d: match[1] });
            continue;
        }

        regex = new RegExp(`<rect[^>]*(?:id="${pathId}"|inkscape:label="${pathId}")[^>]*/>`, 'is');
        match = svgContent.match(regex);

        if (match) {
            const rectTag = match[0];
            const xMatch = rectTag.match(/x="([^"]*)"/);
            const yMatch = rectTag.match(/y="([^"]*)"/);
            const widthMatch = rectTag.match(/width="([^"]*)"/);
            const heightMatch = rectTag.match(/height="([^"]*)"/);

            if (xMatch && yMatch && widthMatch && heightMatch) {
                let x = parseFloat(xMatch[1]);
                let y = parseFloat(yMatch[1]);
                const width = parseFloat(widthMatch[1]);
                const height = parseFloat(heightMatch[1]);

                let corners = [
                    { x: x, y: y },
                    { x: x + width, y: y },
                    { x: x + width, y: y + height },
                    { x: x, y: y + height }
                ];

                const BALLROOM_SCALE = 48.345845;
                corners = corners.map(corner => ({
                    x: corner.x / BALLROOM_SCALE,
                    y: corner.y / BALLROOM_SCALE
                }));

                const d = `M ${corners[0].x},${corners[0].y} L ${corners[1].x},${corners[1].y} L ${corners[2].x},${corners[2].y} L ${corners[3].x},${corners[3].y} Z`;
                paths.push({ id: pathId, d: d });
                continue;
            }
        }

        log(`  ❌ Path or rect "${pathId}" not found in SVG`, 'red');
        return null;
    }
    return paths;
}

/**
 * Calculate bounds for visualization
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
 * Generate SVG visualization for boardroom layout result (matching original aesthetic)
 */
function generateSvgVisualization(testCase, result) {
    const { polygon, polygonArea, boardroomLayout, boardroomArea, computationTime, pathData } = result;

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

    // Create boardroom rectangle points string
    const corners = boardroomLayout.corners;
    const boardroomPoints = corners.map(c => `${c.x},${c.y}`).join(' ');

    // Create centroid marker
    const c = boardroomLayout.centroid;

    // Test paths content
    const testPathsContent = extractTestPathsContent(pathData);

    // Calculate positions for table
    const tableY = bounds.maxY + padding + keyHeight;
    const titleY = tableY - 10;
    const keyY = tableY - 30;
    const tableX = vbX + 10;

    // Calculate fill ratio
    const fillRatio = (boardroomArea / polygonArea * 100).toFixed(1);

    return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vbX} ${vbY} ${vbWidth} ${vbHeight}" width="${vbWidth}" height="${vbHeight}">
  <title>${testCase.name}</title>

  <!-- Area Data (Square SVG Inches) -->
  <!-- polygonArea: ${polygonArea.toFixed(4)} -->
  <!-- boardroomArea: ${boardroomArea.toFixed(4)} -->
  <!-- sets: ${boardroomLayout.sets} -->
  <!-- tables: ${boardroomLayout.tables} -->
  <!-- computationTimeMs: ${computationTime.toFixed(1)} -->

  <!-- White Background -->
  <rect x="${vbX}" y="${vbY}" width="${vbWidth}" height="${vbHeight}" fill="white"/>

  <!-- Test Paths -->
  <g opacity="0.3">
    ${testPathsContent}
  </g>

  <!-- Boundary Polygon (outer boundary) -->
  <polygon points="${polygonPoints}"
           fill="rgba(100, 150, 255, 0.2)"
           stroke="#4080ff"
           stroke-width="1"
           stroke-dasharray="5,5"/>


  <!-- Boardroom Layout Rectangle -->
  <polygon points="${boardroomPoints}"
           fill="rgba(255, 165, 0, 0.25)"
           stroke="#ff8800"
           stroke-width="2"
           stroke-dasharray="none"/>

  <!-- Boardroom Centroid -->
  <circle cx="${c.x}" cy="${c.y}" r="4" fill="#ff8800"/>
  <text x="${c.x + 8}" y="${c.y - 8}" font-size="8" fill="#ff6600">boardroom</text>

  <!-- Boardroom Corners -->
  ${corners.map((corner, i) => `
  <circle cx="${corner.x}" cy="${corner.y}" r="3" fill="#ff8800"/>
  <text x="${corner.x + (i % 2 === 0 ? 6 : -20)}" y="${corner.y + (i < 2 ? 15 : -5)}" font-size="8" fill="#ff6600">br${i}</text>`).join('')}

  <!-- Boundary Vertices -->
  ${polygon.map((p, i) => `
  <circle cx="${p.x}" cy="${p.y}" r="3" fill="#4080ff"/>
  <text x="${p.x + 6}" y="${p.y - 6}" font-size="8" fill="#4080ff">p${i}</text>`).join('')}


  <!-- Title -->
  <text x="${tableX}" y="${titleY}" font-size="14" font-weight="bold">${testCase.name} - Boardroom Layout</text>

  <!-- Combinations Key -->
  <text x="${tableX}" y="${keyY}" font-size="9" fill="#666">Paths: ${testCase.paths.join(', ')}</text>

  <!-- Table Header Background -->
  <rect x="${tableX}" y="${tableY}" width="${compactTableWidth}" height="20" fill="#f0f0f0" stroke="#333" stroke-width="1"/>

  <!-- Table Header -->
  <text x="${tableX + 10}" y="${tableY + 14}" font-size="9" font-weight="bold">Metric</text>
  <text x="${tableX + 120}" y="${tableY + 14}" font-size="9" font-weight="bold">Value</text>

  <!-- Data Row 1: Sets/Tables -->
  <rect x="${tableX}" y="${tableY + 20}" width="${compactTableWidth}" height="18" fill="rgba(255, 165, 0, 0.15)" stroke="#333" stroke-width="1"/>
  <circle cx="${tableX + 15}" cy="${tableY + 29}" r="3" fill="#ff8800"/>
  <text x="${tableX + 23}" y="${tableY + 32}" font-size="8" fill="#ff6600">Sets/Tables</text>
  <text x="${tableX + 120}" y="${tableY + 32}" font-size="8">${boardroomLayout.sets} sets (${boardroomLayout.tables} tables)</text>

  <!-- Data Row 2: Dimensions -->
  <rect x="${tableX}" y="${tableY + 38}" width="${compactTableWidth}" height="18" fill="white" stroke="#333" stroke-width="1"/>
  <text x="${tableX + 10}" y="${tableY + 50}" font-size="8">Dimensions</text>
  <text x="${tableX + 120}" y="${tableY + 50}" font-size="8">${boardroomLayout.width.toFixed(1)} × ${boardroomLayout.height.toFixed(1)} ft</text>

  <!-- Data Row 3: Boardroom Area -->
  <rect x="${tableX}" y="${tableY + 56}" width="${compactTableWidth}" height="18" fill="white" stroke="#333" stroke-width="1"/>
  <text x="${tableX + 10}" y="${tableY + 68}" font-size="8">Boardroom Area</text>
  <text x="${tableX + 120}" y="${tableY + 68}" font-size="8">${boardroomArea.toFixed(2)} sq in</text>

  <!-- Data Row 4: Polygon Area -->
  <rect x="${tableX}" y="${tableY + 74}" width="${compactTableWidth}" height="18" fill="white" stroke="#333" stroke-width="1"/>
  <text x="${tableX + 10}" y="${tableY + 86}" font-size="8">Polygon Area</text>
  <text x="${tableX + 120}" y="${tableY + 86}" font-size="8">${polygonArea.toFixed(2)} sq in</text>

  <!-- Summary Row: Fill Ratio -->
  <rect x="${tableX}" y="${tableY + 92}" width="${compactTableWidth}" height="18" fill="#f8f8f8" stroke="#333" stroke-width="1"/>
  <text x="${tableX + 10}" y="${tableY + 104}" font-size="8" font-weight="bold">Fill Ratio:</text>
  <text x="${tableX + 120}" y="${tableY + 104}" font-size="8">${fillRatio}%</text>

</svg>`;
}

/**
 * Save result to file
 */
function saveTestResult(testCase, result, outputDir) {
    const svgContent = generateSvgVisualization(testCase, result);
    const outputPath = path.join(outputDir, `${testCase.name}.svg`);
    fs.writeFileSync(outputPath, svgContent, 'utf8');
    return outputPath;
}

/**
 * Run boardroom layout test for a single combination
 */
async function runBoardroomTest(testCase) {
    const svgPath = path.resolve(config.svgPath);
    if (!fs.existsSync(svgPath)) {
        return { success: false, error: 'SVG file not found' };
    }

    const svgContent = fs.readFileSync(svgPath, 'utf8');
    const pathData = extractPathData(svgContent, testCase.paths);
    if (!pathData) {
        return { success: false, error: 'Failed to extract path data' };
    }

    const lineSegmentPaths = [];
    for (let i = 0; i < pathData.length; i++) {
        const segments = SvgViewerAlgorithms.parsePathToLineSegments(pathData[i].d, i);
        lineSegmentPaths.push({ pathIdx: i, pathId: pathData[i].id, segments: segments });
    }

    const allSegments = lineSegmentPaths.flatMap(p => p.segments);
    const mergedSegments = SvgViewerAlgorithms.mergeCoincidentPoints(allSegments, 0.5);
    const markedSegments = SvgViewerAlgorithms.markSharedSegments(mergedSegments, 0.5);
    const pathNetwork = SvgViewerAlgorithms.joinPathsIntoNetwork(markedSegments);
    const polygon = SvgViewerAlgorithms.traverseOuterEdge(pathNetwork);

    if (!polygon || polygon.length < 3) {
        return { success: false, error: 'Failed to find polygon boundary' };
    }

    const polygonArea = calculatePolygonAreaInSvgInches(polygon);

    const startTime = performance.now();
    const boardroomOptions = {
        boardroomWidth: 13,
        minLength: 14,
        lengthIncrement: 6,
        angleSamples: 90,          // Reduced from 180 (every 2° instead of every 1°)
        centroidSamples: 25,       // Reduced from 49 (5×5 grid instead of 7×7)
        debugMode: false
    };

    const boardroomLayout = findBoardroomLayout(polygon, boardroomOptions);
    const endTime = performance.now();
    const computationTime = endTime - startTime;

    if (!boardroomLayout) {
        return { success: false, error: 'No valid boardroom layout found', polygon, polygonArea };
    }

    const boardroomArea = calculateBoardroomAreaInSvgInches(boardroomLayout);

    return {
        success: true,
        polygon,
        polygonArea,
        boardroomLayout,
        boardroomArea,
        computationTime,
        pathData
    };
}

/**
 * Main execution
 */
async function main() {
    log('\n' + '='.repeat(80), 'cyan');
    log('BOARDROOM LAYOUT SAMPLE TEST', 'cyan');
    log(`Testing ${sampleTestCases.length} combos that show edge breach issues`, 'cyan');
    log('='.repeat(80) + '\n', 'cyan');

    // Create output directory for sample results (one level up from test-harness)
    const outputDir = path.resolve('../BoardroomResults/Sample');
    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    log(`Output directory: ${outputDir}\n`, 'blue');

    const results = [];
    const startTime = Date.now();

    for (let i = 0; i < sampleTestCases.length; i++) {
        const testCase = sampleTestCases[i];
        const comboNum = SAMPLE_COMBOS[i];
        log(`\n[${i + 1}/${sampleTestCases.length}] ${testCase.name} (Original #${comboNum})`, 'cyan');

        try {
            const result = await runBoardroomTest(testCase);

            if (result.success) {
                const outputPath = saveTestResult(testCase, result, outputDir);
                log(`  ✓ Found: ${result.boardroomLayout.sets} sets (${result.boardroomLayout.tables} tables)`, 'green');
                log(`  ✓ Dimensions: ${result.boardroomLayout.width.toFixed(1)} × ${result.boardroomLayout.height.toFixed(1)} ft`, 'green');
                log(`  ✓ Time: ${result.computationTime.toFixed(1)} ms`, 'green');
                log(`  ✓ Saved to ${path.basename(outputPath)}`, 'green');

                results.push({
                    comboNum: comboNum,
                    testCase: testCase.name,
                    success: true,
                    sets: result.boardroomLayout.sets,
                    tables: result.boardroomLayout.tables,
                    area: result.boardroomArea,
                    computationTime: result.computationTime
                });
            } else {
                log(`  ❌ Failed: ${result.error}`, 'red');
                results.push({
                    comboNum: comboNum,
                    testCase: testCase.name,
                    success: false,
                    error: result.error
                });
            }
        } catch (error) {
            log(`  ❌ Exception: ${error.message}`, 'red');
            results.push({
                comboNum: comboNum,
                testCase: testCase.name,
                success: false,
                error: error.message
            });
        }
    }

    const endTime = Date.now();
    const totalTime = (endTime - startTime) / 1000;

    // Summary
    log('\n\n' + '='.repeat(80), 'cyan');
    log('SAMPLE TEST SUMMARY', 'cyan');
    log('='.repeat(80), 'cyan');

    const successCount = results.filter(r => r.success).length;
    const failureCount = results.length - successCount;

    log(`\nTotal tests: ${results.length}`, 'blue');
    log(`Successful: ${successCount}`, 'green');
    log(`Failed: ${failureCount}`, failureCount > 0 ? 'red' : 'blue');
    log(`Total time: ${totalTime.toFixed(1)} seconds`, 'blue');

    if (successCount > 0) {
        const successfulResults = results.filter(r => r.success);
        const totalComputationTime = successfulResults.reduce((sum, r) => sum + r.computationTime, 0);
        const avgComputationTime = totalComputationTime / successCount;

        log(`\nComputation statistics:`, 'yellow');
        log(`  Total computation time: ${totalComputationTime.toFixed(1)} ms`, 'yellow');
        log(`  Average per test: ${avgComputationTime.toFixed(1)} ms`, 'yellow');
    }

    log('\n' + '='.repeat(80) + '\n', 'cyan');

    log('Combo numbers tested:', 'blue');
    log(SAMPLE_COMBOS.join(', '), 'blue');
    log(`\nSVG files saved to: ${outputDir}`, 'green');
    log('');
}

main().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
