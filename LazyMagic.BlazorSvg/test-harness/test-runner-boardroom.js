#!/usr/bin/env node

/**
 * Boardroom Layout Test Runner
 *
 * Generates boardroom layout inscribed rectangles for all 251 valid combinations.
 * Parallel system to test-runner.js for constrained boardroom-style layouts.
 *
 * Boardroom constraints:
 * - Fixed width: 13 ft
 * - Variable length: 14, 20, 26, 32... ft (increments of 6 ft)
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

// Load dependencies for Optimized algorithm
const { SpatialHash, SpatialGrid } = require('../wwwroot/kdtree.js');

// Load Boundary-Based and Optimized algorithms
const { boundaryBasedInscribedRectangle } = require('../wwwroot/SvgViewerBoundaryBased.js');
const { fastInscribedRectangle } = require('../wwwroot/SvgViewerOptimized.js');

// Load boardroom algorithm
const { findBoardroomLayout } = require('../wwwroot/SvgViewerBoardroom.js');

// Make dependencies globally available for boardroom algorithm
global.SpatialGrid = SpatialGrid;
global.SpatialHash = SpatialHash;
global.boundaryBasedInscribedRectangle = boundaryBasedInscribedRectangle;
global.fastInscribedRectangle = fastInscribedRectangle;

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
 * Calculate boardroom layout area in square SVG inches
 */
function calculateBoardroomAreaInSvgInches(layout, scaleFactor = 48.345845) {
    const UNITS_PER_INCH = 96;

    // Boardroom area in pre-transform coordinates
    const areaPreTransform = layout.width * layout.height;

    // Convert to square user units by scaling up
    const areaUserUnits = areaPreTransform * (scaleFactor * scaleFactor);

    // Convert to square SVG inches
    const areaSvgInches = areaUserUnits / (UNITS_PER_INCH * UNITS_PER_INCH);

    return areaSvgInches;
}

/**
 * Extract path data from SVG content
 * (Reused from test-runner.js)
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

        // Try to find a rect element (handle multi-line)
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

                // Apply transform if present
                let transformMatch = rectTag.match(/transform="matrix\(([^)]*)\)"/);
                if (transformMatch) {
                    const matrixValues = transformMatch[1].split(/[\s,]+/).map(parseFloat);
                    if (matrixValues.length === 6) {
                        const [a, b, c, d, e, f] = matrixValues;
                        corners = corners.map(corner => ({
                            x: a * corner.x + c * corner.y + e,
                            y: b * corner.x + d * corner.y + f
                        }));
                    }
                }

                // Scale down to ballroom coordinate space
                const BALLROOM_SCALE = 48.345845;
                corners = corners.map(corner => ({
                    x: corner.x / BALLROOM_SCALE,
                    y: corner.y / BALLROOM_SCALE
                }));

                const d = `M ${corners[0].x},${corners[0].y} L ${corners[1].x},${corners[1].y} L ${corners[2].x},${corners[2].y} L ${corners[3].x},${corners[3].y} Z`;

                paths.push({
                    id: pathId,
                    d: d
                });
                continue;
            }
        }

        log(`  ❌ Path or rect "${pathId}" not found in SVG`, 'red');
        return null;
    }

    return paths;
}

/**
 * Run boardroom layout test for a single combination
 */
async function runBoardroomTest(testCase) {
    log(`\n${'='.repeat(60)}`, 'magenta');
    log(`Test: ${testCase.name}`, 'magenta');
    log(`Paths: ${testCase.paths.join(', ')}`, 'magenta');
    log(`${'='.repeat(60)}`, 'magenta');

    // Load SVG file
    const svgPath = path.resolve(config.svgPath);
    if (!fs.existsSync(svgPath)) {
        log(`  ❌ SVG file not found: ${svgPath}`, 'red');
        return { success: false, error: 'SVG file not found' };
    }

    const svgContent = fs.readFileSync(svgPath, 'utf8');

    // Extract path data
    const pathData = extractPathData(svgContent, testCase.paths);
    if (!pathData) {
        return { success: false, error: 'Failed to extract path data' };
    }

    log(`  ✓ Extracted ${pathData.length} paths`, 'green');

    // Parse paths to line segments
    log(`  Parsing paths to line segments...`, 'blue');
    const lineSegmentPaths = [];
    for (let i = 0; i < pathData.length; i++) {
        const segments = SvgViewerAlgorithms.parsePathToLineSegments(pathData[i].d, i);
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

    // Run boardroom layout algorithm
    log(`\n  Finding best boardroom layout...`, 'cyan');
    const startTime = performance.now();

    const boardroomOptions = {
        boardroomWidth: 13,         // Fixed 13 ft width
        minLength: 14,              // Minimum 14 ft length (1 set)
        lengthIncrement: 6,         // Add 6 ft per set
        angleSamples: 90,           // Test every 2 degrees (0-180°) - reduced from 180
        centroidSamples: 25,        // 5x5 grid of centroids - reduced from 49
        debugMode: false
    };

    const boardroomLayout = findBoardroomLayout(polygon, boardroomOptions);
    const endTime = performance.now();
    const computationTime = endTime - startTime;

    if (!boardroomLayout) {
        log(`  ❌ No valid boardroom layout found`, 'red');
        return {
            success: false,
            error: 'No valid boardroom layout found',
            polygon,
            polygonArea,
            pathCount: pathData.length,
            vertexCount: polygon.length,
            pathData,
            svgContent
        };
    }

    // Calculate boardroom area in square SVG inches
    const boardroomArea = calculateBoardroomAreaInSvgInches(boardroomLayout);

    log(`\n  ✓ Boardroom Layout Found:`, 'green');
    log(`    Sets: ${boardroomLayout.sets} (${boardroomLayout.tables} tables)`, 'yellow');
    log(`    Dimensions: ${boardroomLayout.width.toFixed(1)} × ${boardroomLayout.height.toFixed(1)} ft`, 'yellow');
    log(`    Area: ${boardroomLayout.area.toFixed(1)} sq ft (${boardroomArea.toFixed(2)} sq inches)`, 'yellow');
    log(`    Angle: ${boardroomLayout.angle.toFixed(1)}°`, 'yellow');
    log(`    Time: ${computationTime.toFixed(1)} ms`, 'yellow');
    log(`    Fill ratio: ${(boardroomArea / polygonArea * 100).toFixed(1)}%`, 'yellow');

    return {
        success: true,
        polygon,
        polygonArea,
        boardroomLayout,
        boardroomArea,
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
 * Main execution
 */
async function main() {
    log('\n' + '='.repeat(80), 'cyan');
    log('BOARDROOM LAYOUT TEST RUNNER', 'cyan');
    log('='.repeat(80) + '\n', 'cyan');

    // Create output directory (one level up from test-harness)
    const outputDir = path.resolve('../BoardroomResults');
    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    log(`Output directory: ${outputDir}\n`, 'blue');
    log(`Running ${testCases.length} boardroom layout tests...\n`, 'blue');

    const results = [];
    const startTime = Date.now();

    for (let i = 0; i < testCases.length; i++) {
        const testCase = testCases[i];
        log(`\n[${i + 1}/${testCases.length}] ${testCase.name}`, 'cyan');

        try {
            const result = await runBoardroomTest(testCase);

            if (result.success) {
                const outputPath = saveTestResult(testCase, result, outputDir);
                log(`  ✓ Saved to ${path.basename(outputPath)}`, 'green');

                results.push({
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
                    testCase: testCase.name,
                    success: false,
                    error: result.error
                });
            }
        } catch (error) {
            log(`  ❌ Exception: ${error.message}`, 'red');
            console.error(error);
            results.push({
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
    log('SUMMARY', 'cyan');
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

        // Save summary JSON
        const summaryPath = path.join(outputDir, 'boardroom-summary.json');
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

        log(`\nSummary saved to: ${summaryPath}`, 'green');
    }

    log('\n' + '='.repeat(80) + '\n', 'cyan');
}

// Run
main().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
