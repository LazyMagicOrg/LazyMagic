#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { testCases, config } from './test-config.js';

// Load required modules
const require = createRequire(import.meta.url);

// Load SvgViewerAlgorithms first (contains SpatialGrid and KDTree)
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');

// Make SpatialGrid globally available (needed by SvgViewerOptimized.js)
global.SpatialGrid = SvgViewerAlgorithms.SpatialGrid;

// Now load SvgViewerOptimized.js (needs global.SpatialGrid)
const { fastInscribedRectangle } = require('../wwwroot/SvgViewerOptimized.js');

// Color codes for console output
const colors = {
    reset: '\x1b[0m',
    green: '\x1b[32m',
    red: '\x1b[31m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    cyan: '\x1b[36m'
};

// Capture console output
let capturedOutput = [];
const originalLog = console.log;
const originalWarn = console.warn;
const originalDebug = console.debug;

console.log = (...args) => {
    capturedOutput.push(args.join(' '));
    originalLog(...args);
};
console.warn = (...args) => {
    capturedOutput.push('[WARN] ' + args.join(' '));
    originalWarn(...args);
};
console.debug = (...args) => {
    capturedOutput.push('[DEBUG] ' + args.join(' '));
    originalDebug(...args);
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

function extractPathData(svgContent, pathIds) {
    const paths = [];

    for (const pathId of pathIds) {
        // Find path with matching id
        const regex = new RegExp(`<path[^>]*id="${pathId}"[^>]*d="([^"]*)"`, 'i');
        const match = svgContent.match(regex);

        if (!match) {
            log(`  ❌ Path "${pathId}" not found in SVG`, 'red');
            return null;
        }

        paths.push({
            id: pathId,
            d: match[1]
        });
    }

    return paths;
}

function parseSvgPath(pathString) {
    // Simple path parser for 'm' (moveto) and 'z' (closepath) commands
    const commands = [];
    const regex = /([mMlLhHvVcCsSqQtTaAzZ])\s*([^mMlLhHvVcCsSqQtTaAzZ]*)/g;
    let match;

    while ((match = regex.exec(pathString)) !== null) {
        const command = match[1];
        const params = match[2].trim().split(/[\s,]+/).filter(p => p).map(Number);
        commands.push({ command, params });
    }

    return commands;
}

// Removed old boundary finding functions - now using proper network traversal from SvgViewerAlgorithms

async function runTest(testCase) {
    log(`\n${'='.repeat(60)}`, 'cyan');
    log(`Test: ${testCase.name}`, 'cyan');
    log(`Paths: ${testCase.paths.join(', ')}`, 'cyan');
    log(`${'='.repeat(60)}`, 'cyan');

    // Load SVG file
    const svgPath = path.resolve(config.svgPath);
    if (!fs.existsSync(svgPath)) {
        log(`  ❌ SVG file not found: ${svgPath}`, 'red');
        return { success: false, error: 'SVG file not found' };
    }

    const svgContent = fs.readFileSync(svgPath, 'utf8');

    // Extract SVG viewBox and dimensions for later use
    const viewBoxMatch = svgContent.match(/viewBox="([^"]*)"/);
    const viewBox = viewBoxMatch ? viewBoxMatch[1] : null;

    // Extract path data
    const pathData = extractPathData(svgContent, testCase.paths);
    if (!pathData) {
        return { success: false, error: 'Failed to extract path data' };
    }

    log(`  ✓ Extracted ${pathData.length} paths`, 'green');

    // Parse paths to line segments using the library function
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
    log(`    Total segments: ${allSegments.length}`, 'blue');
    const mergedSegments = SvgViewerAlgorithms.mergeCoincidentPoints(allSegments, 0.5);
    log(`    Merged segments: ${mergedSegments.length}`, 'blue');

    // Mark shared segments
    log(`  Marking shared segments...`, 'blue');
    const markedSegments = SvgViewerAlgorithms.markSharedSegments(mergedSegments, 0.5);
    const internalCount = markedSegments.filter(s => s.isInternal).length;
    log(`    Internal segments: ${internalCount}, External segments: ${markedSegments.length - internalCount}`, 'blue');

    // Join into network
    log(`  Building path network...`, 'blue');
    const pathNetwork = SvgViewerAlgorithms.joinPathsIntoNetwork(markedSegments);
    log(`    Network has ${pathNetwork.segments.length} segments, ${pathNetwork.pointToSegments.size} points`, 'blue');

    // Traverse outer edge
    log(`  Traversing outer boundary...`, 'blue');
    const polygon = SvgViewerAlgorithms.traverseOuterEdge(pathNetwork);
    log(`    Polygon has ${polygon ? polygon.length : 0} vertices`, 'blue');

    if (!polygon || polygon.length < 3) {
        log(`  ❌ Failed to find valid boundary polygon`, 'red');
        return {
            success: false,
            error: 'Failed to find valid boundary polygon',
            pathData,
            svgContent,
            viewBox
        };
    }

    log(`  ✓ Found boundary polygon with ${polygon.length} vertices`, 'green');

    log(`\n  Boundary polygon (${polygon.length} vertices):`, 'yellow');
    for (const v of polygon) {
        log(`    (${v.x.toFixed(1)}, ${v.y.toFixed(1)})`, 'yellow');
    }

    // Calculate inscribed rectangle using fastInscribedRectangle
    log(`\n  Calculating inscribed rectangle...`, 'cyan');
    const startTime = performance.now();
    const rectangle = fastInscribedRectangle(polygon, {
        debugLog: true
    });
    const endTime = performance.now();
    const calculationTime = endTime - startTime;

    if (!rectangle) {
        log(`  ❌ Failed to calculate inscribed rectangle`, 'red');
        return {
            success: false,
            error: 'Rectangle calculation failed',
            polygon,
            pathCount: paths.length,
            vertexCount: polygon.length,
            pathData,
            svgContent,
            viewBox
        };
    }

    // Calculate centroid from corners if not provided
    if (!rectangle.centroid && rectangle.corners) {
        const cx = rectangle.corners.reduce((sum, c) => sum + c.x, 0) / rectangle.corners.length;
        const cy = rectangle.corners.reduce((sum, c) => sum + c.y, 0) / rectangle.corners.length;
        rectangle.centroid = { x: cx, y: cy };
    }

    // Set type if not provided
    if (!rectangle.type) {
        rectangle.type = 'optimized';
    }

    log(`  ✓ Found ${rectangle.type} rectangle`, 'green');
    log(`    Dimensions: ${rectangle.width.toFixed(1)} × ${rectangle.height.toFixed(1)}`, 'green');
    log(`    Area: ${rectangle.area.toFixed(1)} sq px`, 'green');
    log(`    Angle: ${rectangle.angle.toFixed(1)}°`, 'green');
    log(`    Centroid: (${rectangle.centroid.x.toFixed(1)}, ${rectangle.centroid.y.toFixed(1)})`, 'green');
    log(`    Time: ${calculationTime.toFixed(1)} ms`, 'green');

    log(`\n  Rectangle corners:`, 'yellow');
    for (let i = 0; i < rectangle.corners.length; i++) {
        const c = rectangle.corners[i];
        log(`    [${i}] (${c.x.toFixed(1)}, ${c.y.toFixed(1)})`, 'yellow');
    }

    return {
        success: true,
        polygon,
        rectangle,
        pathCount: pathData.length,
        vertexCount: polygon.length,
        pathData,
        svgContent,
        viewBox,
        calculationTime
    };
}

function extractTestPathsContent(pathData) {
    // Create simple, clean path elements from the path data
    const paths = pathData.map(p => {
        return `<path id="${p.id}" d="${p.d}" fill="none" stroke="#808080" stroke-width="1"/>`;
    });

    return paths.join('\n    ');
}

function generateSvgVisualization(testCase, result) {
    const { polygon, rectangle, pathData, viewBox, calculationTime } = result;

    // Calculate bounds from the polygon to create an appropriate viewBox
    const bounds = calculateBounds(polygon);
    const padding = 50;
    const vbX = bounds.minX - padding;
    const vbY = bounds.minY - padding;
    const vbWidth = bounds.maxX - bounds.minX + 2 * padding;
    const vbHeight = bounds.maxY - bounds.minY + 2 * padding;

    // Create clean path elements from path data
    const testPathsContent = extractTestPathsContent(pathData);

    if (!result.success || !result.rectangle) {
        // Failed case
        const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');

        return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vbX} ${vbY} ${vbWidth} ${vbHeight}" width="${vbWidth}" height="${vbHeight}">
  <title>${testCase.name} - FAILED</title>

  <!-- White Background -->
  <rect x="${vbX}" y="${vbY}" width="${vbWidth}" height="${vbHeight}" fill="white"/>

  <!-- Test Paths -->
  <g opacity="0.3">
    ${testPathsContent}
  </g>

  <!-- Boundary Polygon -->
  <polygon points="${polygonPoints}"
           fill="rgba(200, 200, 200, 0.3)"
           stroke="#666"
           stroke-width="1"/>

  <!-- Boundary Vertices -->
  ${polygon.map((v, i) => `
  <circle cx="${v.x}" cy="${v.y}" r="3" fill="#666"/>
  <text x="${v.x + 5}" y="${v.y - 5}" font-size="10" fill="#333">v${i}</text>`).join('')}

  <!-- Failure message -->
  <text x="${vbWidth/2}" y="50" text-anchor="middle" font-size="24" font-weight="bold" fill="red">
    FAILED: ${result.error || 'Rectangle calculation failed'}
  </text>
</svg>`;
    }

    // Success case
    const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');
    const rectanglePoints = rectangle.corners.map(c => `${c.x},${c.y}`).join(' ');

    return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vbX} ${vbY} ${vbWidth} ${vbHeight}" width="${vbWidth}" height="${vbHeight}">
  <title>${testCase.name}</title>

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

  <!-- Inscribed Rectangle -->
  <polygon points="${rectanglePoints}"
           fill="rgba(255, 100, 100, 0.3)"
           stroke="#ff4040"
           stroke-width="1"/>

  <!-- Rectangle Centroid -->
  <circle cx="${rectangle.centroid.x}" cy="${rectangle.centroid.y}" r="4" fill="#ff0000"/>
  <text x="${rectangle.centroid.x + 8}" y="${rectangle.centroid.y - 8}" font-size="8" fill="#ff0000">centroid</text>

  <!-- Boundary Vertices -->
  ${polygon.map((v, i) => `
  <circle cx="${v.x}" cy="${v.y}" r="3" fill="#4080ff"/>
  <text x="${v.x + 6}" y="${v.y - 6}" font-size="8" fill="#4080ff">b${i}</text>`).join('')}

  <!-- Rectangle Corners -->
  ${rectangle.corners.map((c, i) => `
  <circle cx="${c.x}" cy="${c.y}" r="3" fill="#ff4040"/>
  <text x="${c.x - 15}" y="${c.y + 15}" font-size="8" fill="#ff4040">r${i}</text>`).join('')}

  <!-- Legend -->
  <text x="${vbX + 10}" y="${vbY + 30}" font-size="12" font-weight="bold">${testCase.name}</text>
  <text x="${vbX + 10}" y="${vbY + 50}" font-size="10">Type: ${rectangle.type}</text>
  <text x="${vbX + 10}" y="${vbY + 65}" font-size="10">Size: ${rectangle.width.toFixed(1)} × ${rectangle.height.toFixed(1)} px</text>
  <text x="${vbX + 10}" y="${vbY + 80}" font-size="10">Area: ${rectangle.area.toFixed(1)} sq px</text>
  <text x="${vbX + 10}" y="${vbY + 95}" font-size="10">Angle: ${rectangle.angle.toFixed(1)}°</text>
  <text x="${vbX + 10}" y="${vbY + 110}" font-size="10">Time: ${calculationTime.toFixed(1)} ms</text>
</svg>`;
}

function calculateBounds(points) {
    let minX = Infinity, maxX = -Infinity;
    let minY = Infinity, maxY = -Infinity;

    for (const p of points) {
        minX = Math.min(minX, p.x);
        maxX = Math.max(maxX, p.x);
        minY = Math.min(minY, p.y);
        maxY = Math.max(maxY, p.y);
    }

    return { minX, maxX, minY, maxY };
}

async function main() {
    log('\n' + '='.repeat(60), 'cyan');
    log('SVG Inscribed Rectangle Test Harness', 'cyan');
    log('='.repeat(60), 'cyan');

    // Clear TestResults directory
    const testResultsDir = path.resolve('../TestResults');
    if (fs.existsSync(testResultsDir)) {
        log('Clearing previous test results...', 'yellow');
        const files = fs.readdirSync(testResultsDir);
        for (const file of files) {
            fs.unlinkSync(path.join(testResultsDir, file));
        }
        log(`  ✓ Cleared ${files.length} files from TestResults`, 'green');
    } else {
        fs.mkdirSync(testResultsDir, { recursive: true });
        log('Created TestResults directory', 'green');
    }

    const results = [];

    for (const testCase of testCases) {
        const result = await runTest(testCase);
        results.push({ testCase, result });
    }

    // Summary
    log('\n' + '='.repeat(60), 'cyan');
    log('Test Summary', 'cyan');
    log('='.repeat(60), 'cyan');

    const passed = results.filter(r => r.result.success).length;
    const failed = results.length - passed;

    log(`Total: ${results.length}`, 'blue');
    log(`Passed: ${passed}`, 'green');
    log(`Failed: ${failed}`, failed > 0 ? 'red' : 'green');

    if (failed === 0) {
        log('\n✓ All tests passed!', 'green');
    } else {
        log('\n✗ Some tests failed', 'red');
    }

    // Generate SVG visualizations for each test
    log('\n' + '='.repeat(60), 'cyan');
    log('Generating SVG visualizations...', 'cyan');
    log('='.repeat(60), 'cyan');

    for (const { testCase, result } of results) {
        const svgContent = generateSvgVisualization(testCase, result);
        const fileName = testCase.name.replace(/\s+/g, '_') + '.svg';
        const filePath = path.join(testResultsDir, fileName);
        fs.writeFileSync(filePath, svgContent, 'utf8');
        log(`  ✓ Generated ${fileName}`, 'green');
    }

    // Write test output to text file
    const outputFileName = 'test-output.txt';
    const outputFilePath = path.join(testResultsDir, outputFileName);

    // Strip ANSI color codes from output
    const cleanOutput = capturedOutput.map(line =>
        line.replace(/\x1b\[[0-9;]*m/g, '')
    ).join('\n');

    fs.writeFileSync(outputFilePath, cleanOutput, 'utf8');
    log(`  ✓ Saved test output to ${outputFileName}`, 'green');

    log('\n' + '='.repeat(60), 'cyan');
    log(`Test results saved to: ${testResultsDir}`, 'cyan');
    log('='.repeat(60), 'cyan');
}

main().catch(err => {
    log(`Fatal error: ${err.message}`, 'red');
    console.error(err);
    process.exit(1);
});
