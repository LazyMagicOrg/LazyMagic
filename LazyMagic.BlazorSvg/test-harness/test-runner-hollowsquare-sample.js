/**
 * Hollow Square Layout Test Runner - Sample Subset
 *
 * Tests the hollow square layout algorithm on a sample subset of valid combinations.
 * This is a faster version for testing algorithm improvements.
 *
 * Usage: node test-runner-hollowsquare-sample.js
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import puppeteer from 'puppeteer';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Import configuration
import { config } from './test-config.js';

// ANSI color codes for console output
const colors = {
    red: '\x1b[31m',
    green: '\x1b[32m',
    blue: '\x1b[34m',
    yellow: '\x1b[33m',
    cyan: '\x1b[36m',
    magenta: '\x1b[35m',
    reset: '\x1b[0m',
    bold: '\x1b[1m'
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

// Load sample combinations
const validCombinationsPath = path.join(__dirname, 'valid-combinations-hollowsquare-sample.json');
const data = fs.readFileSync(validCombinationsPath, 'utf8');
const validCombinations = JSON.parse(data);

const combinations = validCombinations.combinations;
const totalCombos = combinations.length;

log('\n═══════════════════════════════════════════════════════', 'cyan');
log('  Hollow Square Layout Test Runner - Sample Subset', 'cyan');
log('═══════════════════════════════════════════════════════\n', 'cyan');

log(`Total sample combinations to test: ${totalCombos}`, 'bold');
log(`Note: ${validCombinations.note || 'Sample subset for faster testing'}\n`, 'yellow');

// Test statistics
const stats = {
    passed: 0,
    failed: 0,
    noLayout: 0,
    totalTime: 0,
    results: []
};

async function processTestCase(browser, combination, comboIndex, originalComboNumber) {
    log(`\n${'─'.repeat(60)}`, 'blue');
    log(`Test ${comboIndex + 1}/${totalCombos}: Combo_${String(originalComboNumber).padStart(4, '0')}`, 'bold');
    log(`Sections (${combination.sections.length}): ${combination.sections.join(', ')}`, 'cyan');

    const page = await browser.newPage();

    try {
        // Load SVG file
        const svgPath = config.svgPath;
        const svgContent = fs.readFileSync(svgPath, 'utf8');

        // Create HTML page with algorithm scripts
        const htmlContent = `
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <script src="${path.join(__dirname, '../wwwroot/SvgViewerAlgorithms.js')}"></script>
    <script src="${path.join(__dirname, '../wwwroot/SvgViewerHollowSquare.js')}"></script>
</head>
<body>
    <div id="svg-container">${svgContent}</div>
</body>
</html>
`;

        await page.setContent(htmlContent);

        // Inject algorithm scripts
        const algorithmsScript = fs.readFileSync(path.join(__dirname, '../wwwroot/SvgViewerAlgorithms.js'), 'utf8');
        const hollowSquareScript = fs.readFileSync(path.join(__dirname, '../wwwroot/SvgViewerHollowSquare.js'), 'utf8');

        await page.evaluate(algorithmsScript);
        await page.evaluate(hollowSquareScript);

        // Extract path data for the combination's sections
        const pathData = await page.evaluate((sectionIds) => {
            const svg = document.querySelector('svg');
            const paths = [];

            sectionIds.forEach(id => {
                const element = svg.querySelector(`[id="${id}"]`);
                if (!element) {
                    console.warn(`Path with id="${id}" not found`);
                    return;
                }

                const d = element.getAttribute('d');
                if (d) {
                    paths.push({ id, d });
                }
            });

            return paths;
        }, combination.sections);

        if (pathData.length === 0) {
            throw new Error('No paths found for combination');
        }

        log(`\nParsing paths into line segments...`, 'blue');
        const lineSegmentPaths = [];

        for (let i = 0; i < pathData.length; i++) {
            const pathD = pathData[i].d;
            const segments = await page.evaluate((pathD, pathIdx) => {
                return window.parsePathToLineSegments(pathD, pathIdx);
            }, pathD, i);

            lineSegmentPaths.push({
                pathIdx: i,
                pathId: pathData[i].id,
                segments: segments
            });
        }

        log(`Total line segments: ${lineSegmentPaths.reduce((sum, p) => sum + p.segments.length, 0)}`, 'green');

        // Build boundary polygon
        log(`\nBuilding boundary polygon from line segments...`, 'blue');
        const polygon = await page.evaluate((lineSegmentPaths) => {
            return window.buildBoundaryPolygonFromLineSegments(lineSegmentPaths);
        }, lineSegmentPaths);

        log(`Boundary vertices: ${polygon.length}`, 'green');

        // Calculate polygon area
        const polygonArea = await page.evaluate((polygon) => {
            return window.calculatePolygonArea(polygon);
        }, polygon);

        log(`Polygon area: ${polygonArea.toFixed(2)} sq inches`, 'green');

        // Run hollow square layout algorithm
        log(`\nRunning hollow square layout algorithm...`, 'blue');
        const startTime = Date.now();

        const hollowSquareLayout = await page.evaluate((polygon) => {
            const options = {
                // No explicit maxLengthRun or maxDepthRun - will use dynamic calculation
                angleSamples: 90,
                centroidSamples: 25,
                debugMode: false
            };

            return window.findHollowSquareLayout(polygon, options);
        }, polygon);

        const elapsedTime = Date.now() - startTime;

        if (!hollowSquareLayout) {
            log(`✗ No hollow square layout found`, 'red');
            stats.noLayout++;
            stats.results.push({
                combo: originalComboNumber,
                success: false,
                reason: 'No layout found',
                time: elapsedTime
            });
            return;
        }

        log(`✓ Hollow Square Layout Found:`, 'green');
        log(`  Tables: ${hollowSquareLayout.tables}`, 'cyan');
        log(`  LengthRun: ${hollowSquareLayout.lengthRun}, DepthRun: ${hollowSquareLayout.depthRun}`, 'cyan');
        log(`  Dimensions: ${hollowSquareLayout.width.toFixed(1)} × ${hollowSquareLayout.height.toFixed(1)} ft`, 'cyan');
        log(`  Area: ${hollowSquareLayout.area.toFixed(1)} sq ft (${(hollowSquareLayout.area * 144).toFixed(2)} sq inches)`, 'cyan');
        log(`  Angle: ${hollowSquareLayout.angle.toFixed(1)}°`, 'cyan');
        log(`  Time: ${elapsedTime.toFixed(1)} ms`, 'yellow');

        const fillRatio = (hollowSquareLayout.area * 144) / polygonArea;
        log(`  Fill ratio: ${(fillRatio * 100).toFixed(1)}%`, fillRatio >= 0.5 ? 'green' : 'yellow');

        stats.passed++;
        stats.totalTime += elapsedTime;
        stats.results.push({
            combo: originalComboNumber,
            success: true,
            tables: hollowSquareLayout.tables,
            lengthRun: hollowSquareLayout.lengthRun,
            depthRun: hollowSquareLayout.depthRun,
            dimensions: `${hollowSquareLayout.width.toFixed(1)} × ${hollowSquareLayout.height.toFixed(1)} ft`,
            area: hollowSquareLayout.area,
            angle: hollowSquareLayout.angle,
            fillRatio: fillRatio,
            time: elapsedTime
        });

        // Generate SVG visualization
        const svgVisualization = generateSVG(
            polygon,
            hollowSquareLayout,
            pathData,
            `Combo_${String(originalComboNumber).padStart(4, '0')}`,
            polygonArea
        );

        // Save SVG to BlazorTest.WASM TestResultsSamples directory
        const outputDir = path.join(__dirname, '../../BlazorTest.WASM/TestResultsSamples/HollowSquareResultsSamples');
        if (!fs.existsSync(outputDir)) {
            fs.mkdirSync(outputDir, { recursive: true });
        }

        const outputPath = path.join(outputDir, `Combo_${String(originalComboNumber).padStart(4, '0')}.svg`);
        fs.writeFileSync(outputPath, svgVisualization, 'utf8');

        log(`✓ Saved to Combo_${String(originalComboNumber).padStart(4, '0')}.svg`, 'green');

    } catch (error) {
        log(`✗ Test failed: ${error.message}`, 'red');
        console.error(error);
        stats.failed++;
        stats.results.push({
            combo: originalComboNumber,
            success: false,
            error: error.message
        });
    } finally {
        await page.close();
    }
}

function generateSVG(polygon, hollowSquareLayout, pathData, title, polygonArea) {
    // Calculate viewBox from polygon bounds
    const bounds = {
        minX: Math.min(...polygon.map(p => p.x)),
        maxX: Math.max(...polygon.map(p => p.x)),
        minY: Math.min(...polygon.map(p => p.y)),
        maxY: Math.max(...polygon.map(p => p.y))
    };

    const padding = 20;
    const tableHeight = 100; // Space for data table at bottom
    const viewBoxX = bounds.minX - padding;
    const viewBoxY = bounds.minY - padding;
    const viewBoxWidth = (bounds.maxX - bounds.minX) + (padding * 2);
    const viewBoxHeight = (bounds.maxY - bounds.minY) + (padding * 2) + tableHeight;

    // Generate path elements (gray, semi-transparent)
    const pathElements = pathData.map(p =>
        `<path id="${p.id}" d="${p.d}" fill="none" stroke="#808080" stroke-width="1"/>`
    ).join('\n    ');

    // Polygon points
    const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');

    // Hollow square corners
    const c = hollowSquareLayout.corners;
    const hollowSquarePoints = `${c[0].x},${c[0].y} ${c[1].x},${c[1].y} ${c[2].x},${c[2].y} ${c[3].x},${c[3].y}`;

    // Centroid
    const centroid = hollowSquareLayout.centroid;

    // Corner markers
    const cornerMarkers = c.map((corner, i) => `
  <circle cx="${corner.x}" cy="${corner.y}" r="3" fill="#9333ea"/>
  <text x="${corner.x + 6}" y="${corner.y + 4}" font-size="8" fill="#9333ea">hs${i}</text>
  `).join('');

    // Vertex markers
    const vertexMarkers = polygon.map((vertex, i) => `
  <circle cx="${vertex.x}" cy="${vertex.y}" r="2" fill="#4080ff"/>
  <text x="${vertex.x - 8}" y="${vertex.y - 6}" font-size="8" fill="#4080ff">p${i}</text>
  `).join('');

    // Data table calculations
    const hollowSquareAreaSqInches = hollowSquareLayout.area * 144;
    const fillRatio = (hollowSquareAreaSqInches / polygonArea) * 100;

    // Table position (at bottom)
    const tableX = viewBoxX + 10;
    const tableY = bounds.maxY + tableHeight - 10;

    const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg"
     viewBox="${viewBoxX} ${viewBoxY} ${viewBoxWidth} ${viewBoxHeight}"
     width="${viewBoxWidth}" height="${viewBoxHeight}"
     style="background: white;">

  <!-- Title -->
  <text x="${viewBoxX + 10}" y="${viewBoxY + 20}" font-size="16" font-weight="bold" fill="#000">
    ${title}
  </text>

  <!-- Original test paths (gray, semi-transparent) -->
  <g opacity="0.3">
    ${pathElements}
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
  <circle cx="${centroid.x}" cy="${centroid.y}" r="4" fill="#9333ea"/>
  <text x="${centroid.x + 8}" y="${centroid.y + 5}" font-size="10" fill="#9333ea">hollow square</text>

  <!-- Corner markers -->
  ${cornerMarkers}

  <!-- Polygon vertices -->
  ${vertexMarkers}

  <!-- Comparison Table (compact, positioned at bottom) -->
  <g transform="translate(${tableX}, ${tableY - 100})">
    <!-- Table border -->
    <rect x="0" y="0" width="300" height="100" fill="white" stroke="#ccc" stroke-width="1"/>

    <!-- Header -->
    <rect x="0" y="0" width="300" height="25" fill="#f0f0f0"/>
    <text x="150" y="17" font-size="12" font-weight="bold" text-anchor="middle">Hollow Square Layout</text>

    <!-- Row 1: Tables -->
    <text x="10" y="42" font-size="10" font-weight="bold">Tables:</text>
    <text x="290" y="42" font-size="10" text-anchor="end">${hollowSquareLayout.tables} (${hollowSquareLayout.lengthRun}L × ${hollowSquareLayout.depthRun}D)</text>

    <!-- Row 2: Dimensions -->
    <text x="10" y="60" font-size="10" font-weight="bold">Dimensions:</text>
    <text x="290" y="60" font-size="10" text-anchor="end">${hollowSquareLayout.width.toFixed(1)} × ${hollowSquareLayout.height.toFixed(1)} ft</text>

    <!-- Row 3: Areas -->
    <text x="10" y="78" font-size="10" font-weight="bold">Area:</text>
    <text x="290" y="78" font-size="10" text-anchor="end">${hollowSquareAreaSqInches.toFixed(2)} sq in (${fillRatio.toFixed(1)}%)</text>

    <!-- Row 4: Polygon Area -->
    <text x="10" y="95" font-size="10" font-weight="bold">Polygon:</text>
    <text x="290" y="95" font-size="10" text-anchor="end">${polygonArea.toFixed(2)} sq in</text>
  </g>

  <!-- Area Data (embedded as XML comments for extraction) -->
  <!-- Area Data (Square SVG Inches) -->
  <!-- polygonArea: ${polygonArea.toFixed(4)} -->
  <!-- hollowSquareArea: ${hollowSquareAreaSqInches.toFixed(4)} -->
  <!-- fillRatio: ${fillRatio.toFixed(1)} -->

</svg>
`;

    return svg;
}

// Main test execution
(async () => {
    const browser = await puppeteer.launch({ headless: true });

    try {
        // Map combo index to original combo number (for proper file naming)
        const requestedCombos = [4, 7, 8, 29, 32, 33, 34, 49, 66, 112, 130, 179, 194, 217, 236, 251];

        for (let i = 0; i < combinations.length; i++) {
            const originalComboNumber = requestedCombos[i];
            await processTestCase(browser, combinations[i], i, originalComboNumber);
        }

        // Print summary
        log('\n═══════════════════════════════════════════════════════', 'cyan');
        log('  Test Summary', 'cyan');
        log('═══════════════════════════════════════════════════════\n', 'cyan');

        log(`Total tests:        ${totalCombos}`, 'bold');
        log(`Passed:             ${stats.passed}`, 'green');
        log(`Failed:             ${stats.failed}`, stats.failed > 0 ? 'red' : 'green');
        log(`No layout found:    ${stats.noLayout}`, stats.noLayout > 0 ? 'yellow' : 'green');
        log(`Average time:       ${(stats.totalTime / totalCombos).toFixed(1)} ms`, 'yellow');
        log(`Total time:         ${(stats.totalTime / 1000).toFixed(1)} seconds\n`, 'yellow');

        // Save summary JSON
        const summaryPath = path.join(__dirname, '../../BlazorTest.WASM/TestResultsSamples/HollowSquareResultsSamples/hollowsquare-sample-summary.json');
        fs.writeFileSync(summaryPath, JSON.stringify({
            generatedAt: new Date().toISOString(),
            totalTests: totalCombos,
            passed: stats.passed,
            failed: stats.failed,
            noLayout: stats.noLayout,
            averageTime: stats.totalTime / totalCombos,
            totalTime: stats.totalTime,
            results: stats.results
        }, null, 2), 'utf8');

        log(`✓ Summary saved to hollowsquare-sample-summary.json\n`, 'green');

    } finally {
        await browser.close();
    }
})();
