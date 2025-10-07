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

// Load rotating calipers algorithm
const { rotatingCalipersInscribedRectangle } = require('../wwwroot/SvgViewerRotatingCalipers.js');

// Load boundary-based and hybrid algorithms
const { boundaryBasedInscribedRectangle, hybridInscribedRectangle } = require('../wwwroot/SvgViewerBoundaryBased.js');

// Make algorithms globally available for hybrid algorithm
global.fastInscribedRectangle = fastInscribedRectangle;
global.rotatingCalipersInscribedRectangle = rotatingCalipersInscribedRectangle;

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

        // Try to find a rect element by id or inkscape:label (handle multi-line)
        regex = new RegExp(`<rect[^>]*(?:id="${pathId}"|inkscape:label="${pathId}")[^>]*/>`, 'is');
        match = svgContent.match(regex);

        if (match) {
            const rectTag = match[0];

            // Extract individual attributes
            const xMatch = rectTag.match(/x="([^"]*)"/);
            const yMatch = rectTag.match(/y="([^"]*)"/);
            const widthMatch = rectTag.match(/width="([^"]*)"/);
            const heightMatch = rectTag.match(/height="([^"]*)"/);

            if (xMatch && yMatch && widthMatch && heightMatch) {
                let x = parseFloat(xMatch[1]);
                let y = parseFloat(yMatch[1]);
                const width = parseFloat(widthMatch[1]);
                const height = parseFloat(heightMatch[1]);

                // Get the four corners of the rectangle
                let corners = [
                    { x: x, y: y },
                    { x: x + width, y: y },
                    { x: x + width, y: y + height },
                    { x: x, y: y + height }
                ];

                // Check for transform attribute and apply it
                let transformMatch = rectTag.match(/transform="matrix\(([^)]*)\)"/);
                let hasTransform = false;

                if (transformMatch) {
                    hasTransform = true;
                    const matrixValues = transformMatch[1].split(/[\s,]+/).map(parseFloat);
                    if (matrixValues.length === 6) {
                        const [a, b, c, d, e, f] = matrixValues;
                        console.log(`\n[DEBUG] Transform for ${pathId}:`);
                        console.log(`  Original rect: x=${x}, y=${y}, width=${width}, height=${height}`);
                        console.log(`  Original area: ${width * height}`);
                        console.log(`  Matrix: [${a}, ${b}, ${c}, ${d}, ${e}, ${f}]`);
                        console.log(`  Corners before transform:`, corners);

                        // Apply transformation matrix to each corner
                        corners = corners.map(corner => ({
                            x: a * corner.x + c * corner.y + e,
                            y: b * corner.x + d * corner.y + f
                        }));

                        console.log(`  Corners after transform:`, corners);

                        // CRITICAL: The test rectangles are in the untransformed SVG coordinate space,
                        // but the ballroom paths are inside a layer with transform="scale(48.345845)".
                        // We need to scale down the test rectangles to match the ballroom coordinate space.
                        const BALLROOM_SCALE = 48.345845;
                        corners = corners.map(corner => ({
                            x: corner.x / BALLROOM_SCALE,
                            y: corner.y / BALLROOM_SCALE
                        }));

                        console.log(`  Corners after scaling to ballroom space:`, corners);

                        // Calculate area using shoelace formula for verification
                        let area = 0;
                        for (let i = 0; i < corners.length; i++) {
                            const j = (i + 1) % corners.length;
                            area += corners[i].x * corners[j].y;
                            area -= corners[j].x * corners[i].y;
                        }
                        area = Math.abs(area) / 2;
                        console.log(`  Scaled area: ${area}\n`);
                    }
                }

                // Check for rotate transform
                if (!hasTransform) {
                    const rotateMatch = rectTag.match(/transform="rotate\(([^)]*)\)"/);
                    if (rotateMatch) {
                        hasTransform = true;
                        const angleStr = rotateMatch[1];
                        const angle = parseFloat(angleStr) * Math.PI / 180; // Convert to radians

                        console.log(`\n[DEBUG] Transform for ${pathId}:`);
                        console.log(`  Original rect: x=${x}, y=${y}, width=${width}, height=${height}`);
                        console.log(`  Original area: ${width * height}`);
                        console.log(`  Rotate angle: ${angleStr}° (${angle} rad)`);
                        console.log(`  Corners before transform:`, corners);

                        // Rotate around origin (0, 0) - SVG default for rotate
                        const cos = Math.cos(angle);
                        const sin = Math.sin(angle);
                        corners = corners.map(corner => ({
                            x: cos * corner.x - sin * corner.y,
                            y: sin * corner.x + cos * corner.y
                        }));

                        console.log(`  Corners after rotation:`, corners);

                        // Scale down to ballroom coordinate space
                        const BALLROOM_SCALE = 48.345845;
                        corners = corners.map(corner => ({
                            x: corner.x / BALLROOM_SCALE,
                            y: corner.y / BALLROOM_SCALE
                        }));

                        console.log(`  Corners after scaling to ballroom space:`, corners);

                        // Calculate area using shoelace formula for verification
                        let area = 0;
                        for (let i = 0; i < corners.length; i++) {
                            const j = (i + 1) % corners.length;
                            area += corners[i].x * corners[j].y;
                            area -= corners[j].x * corners[i].y;
                        }
                        area = Math.abs(area) / 2;
                        console.log(`  Scaled area: ${area}\n`);
                    }
                }

                // If no transform was found, still need to scale down
                if (!hasTransform) {
                    const BALLROOM_SCALE = 48.345845;
                    corners = corners.map(corner => ({
                        x: corner.x / BALLROOM_SCALE,
                        y: corner.y / BALLROOM_SCALE
                    }));
                }

                // Convert transformed corners to path data
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

/**
 * Extract goal-seeking rectangle for a test (if it exists in the SVG)
 * Goal rectangles are pre-drawn reference rectangles with IDs like "Test02", "Test06", etc.
 */
function extractGoalRectangle(svgContent, goalRectangleId) {
    if (!goalRectangleId) {
        return null;
    }

    // Extract the rect element with id matching goalRectangleId
    const rectRegex = new RegExp(`<rect[^>]*id="${goalRectangleId}"[^>]*/>`, 's');
    const match = svgContent.match(rectRegex);

    if (!match) {
        return null;
    }

    const rectTag = match[0];

    // Extract attributes
    const widthMatch = rectTag.match(/width="([^"]*)"/);
    const heightMatch = rectTag.match(/height="([^"]*)"/);
    const xMatch = rectTag.match(/x="([^"]*)"/);
    const yMatch = rectTag.match(/y="([^"]*)"/);
    const transformMatch = rectTag.match(/transform="([^"]*)"/);

    if (!widthMatch || !heightMatch || !xMatch || !yMatch) {
        return null;
    }

    let x = parseFloat(xMatch[1]);
    let y = parseFloat(yMatch[1]);
    const width = parseFloat(widthMatch[1]);
    const height = parseFloat(heightMatch[1]);

    // Create corners of the rectangle
    let corners = [
        { x: x, y: y },
        { x: x + width, y: y },
        { x: x + width, y: y + height },
        { x: x, y: y + height }
    ];

    // Apply transform if present (rotation matrix)
    if (transformMatch) {
        const transformStr = transformMatch[1];
        const matrixMatch = transformStr.match(/matrix\(([^)]+)\)/);

        if (matrixMatch) {
            const values = matrixMatch[1].split(/[\s,]+/).map(Number);
            // SVG matrix format: matrix(a, b, c, d, e, f)
            // Transform: x' = a*x + c*y + e, y' = b*x + d*y + f
            const [a, b, c, d, e, f] = values;

            corners = corners.map(corner => ({
                x: a * corner.x + c * corner.y + e,
                y: b * corner.x + d * corner.y + f
            }));
        }
    }

    // Scale down to ballroom coordinate space (same scale used for paths)
    const BALLROOM_SCALE = 48.345845;
    corners = corners.map(corner => ({
        x: corner.x / BALLROOM_SCALE,
        y: corner.y / BALLROOM_SCALE
    }));

    // Calculate area using shoelace formula
    let area = 0;
    for (let i = 0; i < corners.length; i++) {
        const j = (i + 1) % corners.length;
        area += corners[i].x * corners[j].y;
        area -= corners[j].x * corners[i].y;
    }
    area = Math.abs(area) / 2;

    return {
        corners,
        area,
        width: Math.sqrt((corners[1].x - corners[0].x) ** 2 + (corners[1].y - corners[0].y) ** 2),
        height: Math.sqrt((corners[3].x - corners[0].x) ** 2 + (corners[3].y - corners[0].y) ** 2)
    };
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

    // Extract goal rectangle if one exists for this test
    const goalRectangle = testCase.goalRectangle ? extractGoalRectangle(svgContent, testCase.goalRectangle) : null;
    if (goalRectangle) {
        log(`  ✓ Found goal rectangle: ${goalRectangle.area.toFixed(1)} sq px`, 'cyan');
    }

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

    // Calculate inscribed rectangle using hybrid approach (tries boundary-based first)
    log(`\n  Calculating inscribed rectangle with hybrid algorithm...`, 'cyan');
    const startTime = performance.now();

    // SMART HYBRID: Use boundary-based for simple shapes, optimized for complex concave shapes
    // Detect complexity: vertex count and concavity
    const vertexCount = polygon.length;
    const isComplex = vertexCount >= 10;  // 10+ vertices = complex polygon, likely concave

    const rectangle = hybridInscribedRectangle(polygon, {
        debugMode: false,
        // Smart threshold: Use boundary-based for simple shapes, optimized for complex
        coverageThreshold: isComplex ? 0.0 : 0.85,  // Complex = force optimized, Simple = allow boundary
        targetArea: goalRectangle ? goalRectangle.area : null,
        pathCount: pathData.length,

        // Boundary-based options (fast for simple shapes)
        maxAngles: 36,  // Test every 10° for edge alignment
        angleTolerance: 2,
        testPerpendicular: true,

        // Optimized grid-based algorithm (for complex concave shapes)
        maxTime: 60000,  // 60 seconds for complex shapes
        gridStep: 10.0,
        polylabelPrecision: 0.5,
        aspectRatios: [0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.7, 2.0, 2.5, 3.0],
        binarySearchPrecision: 0.01,
        binarySearchMaxIterations: 25
    });
    const endTime = performance.now();
    const calculationTime = endTime - startTime;

    if (!rectangle) {
        log(`  ❌ Failed to calculate inscribed rectangle`, 'red');
        return {
            success: false,
            error: 'Rectangle calculation failed',
            polygon,
            pathCount: pathData.length,
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

    // Show goal comparison if goal rectangle exists
    if (goalRectangle) {
        const gap = ((rectangle.area - goalRectangle.area) / goalRectangle.area * 100);
        log(`\n  Goal Comparison:`, 'yellow');
        log(`    Inscribed Area: ${rectangle.area.toFixed(1)} sq px`, 'yellow');
        log(`    Goal Area:      ${goalRectangle.area.toFixed(1)} sq px`, 'yellow');
        log(`    Gap:            ${gap >= 0 ? '+' : ''}${gap.toFixed(1)}%`, gap >= -5 ? 'green' : 'red');
    }

    log(`\n  Rectangle corners:`, 'yellow');
    for (let i = 0; i < rectangle.corners.length; i++) {
        const c = rectangle.corners[i];
        log(`    [${i}] (${c.x.toFixed(1)}, ${c.y.toFixed(1)})`, 'yellow');
    }

    return {
        success: true,
        polygon,
        rectangle,
        goalRectangle,
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
    const { polygon, rectangle, pathData, viewBox, calculationTime, goalRectangle } = result;

    // Calculate bounds from the polygon to create an appropriate viewBox
    const bounds = calculateBounds(polygon);
    const padding = 50;
    const vbX = bounds.minX - padding;
    const vbY = bounds.minY - padding;
    const vbWidth = bounds.maxX - bounds.minX + 2 * padding;
    const vbHeight = bounds.maxY - bounds.minY + 2 * padding;

    // Create clean path elements from path data
    const testPathsContent = extractTestPathsContent(pathData);

    if (!result.rectangle) {
        // No rectangle found case
        const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');

        return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vbX} ${vbY} ${vbWidth} ${vbHeight}" width="${vbWidth}" height="${vbHeight}">
  <title>${testCase.name} - FAILED (No Rectangle)</title>

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

  <!-- Legend -->
  <text x="${vbX + 10}" y="${vbY + 30}" font-size="12" font-weight="bold" fill="red">${testCase.name} - FAILED</text>
  <text x="${vbX + 10}" y="${vbY + 50}" font-size="10" fill="red">${result.error || 'Rectangle calculation failed'}</text>
</svg>`;
    }

    // Rectangle found case
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
  <text x="${vbX + 10}" y="${vbY + 110}" font-size="10">Time: ${calculationTime.toFixed(1)} ms</text>${goalRectangle ? `
  <text x="${vbX + 10}" y="${vbY + 135}" font-size="10" font-weight="bold">Goal: ${goalRectangle.area.toFixed(1)} sq px</text>
  <text x="${vbX + 10}" y="${vbY + 150}" font-size="10" fill="${((rectangle.area - goalRectangle.area) / goalRectangle.area * 100) >= -5 ? '#00aa00' : '#cc0000'}">Gap: ${((rectangle.area - goalRectangle.area) / goalRectangle.area * 100).toFixed(1)}% ${((rectangle.area - goalRectangle.area) / goalRectangle.area * 100) >= -5 ? '✓ PASS' : '✗ FAIL'}</text>` : ''}
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

function generateResultsFile(results, testResultsDir) {
    let output = `INSCRIBED RECTANGLE TEST RESULTS
${'='.repeat(80)}
Latest Test Run: ${new Date().toLocaleString()}

GOAL COMPARISON RESULTS
${'='.repeat(80)}
Test | Inscribed | Goal      | Gap     | Strategy | Status
-----|-----------|-----------|---------|----------|----------------------------------
`;

    // Goal comparison table - show all tests with goals
    for (const { testCase, result } of results) {
        if (result.goalRectangle) {
            const gap = ((result.rectangle.area - result.goalRectangle.area) / result.goalRectangle.area * 100);
            const strategy = result.rectangle.type || 'boundary-based';
            const status = gap > -4 ? '✓ Pass' : (gap >= -10 ? '⚠️  Fail (noticeable)' : '❌ Fail (significant)');
            output += `${testCase.name.replace('Test', '')}   | ${result.rectangle.area.toFixed(1).padStart(9)} | ${result.goalRectangle.area.toFixed(1).padStart(9)} | ${(gap >= 0 ? '+' : '') + gap.toFixed(1)}%`.padEnd(8) + ` | ${strategy.toUpperCase().padEnd(8)} | ${status}\n`;
        }
    }

    // Calculate gap statistics
    const gapsWithGoals = results
        .filter(r => r.result.goalRectangle)
        .map(r => ((r.result.rectangle.area - r.result.goalRectangle.area) / r.result.goalRectangle.area * 100));
    const avgGap = gapsWithGoals.reduce((sum, g) => sum + g, 0) / gapsWithGoals.length;
    const maxGap = Math.min(...gapsWithGoals);
    const minGap = Math.max(...gapsWithGoals);

    output += `\nGAP ANALYSIS
${'='.repeat(80)}
Average Gap: ${avgGap.toFixed(1)}%
Largest Gap: ${maxGap.toFixed(1)}% (furthest from goal)
Smallest Gap: ${minGap.toFixed(1)}% (closest to goal)

Gap Categories:
  Pass (<4%):         ${gapsWithGoals.filter(g => g > -4).length} tests
  Fail (4-10%):       ${gapsWithGoals.filter(g => g <= -4 && g >= -10).length} tests
  Fail (>10%):        ${gapsWithGoals.filter(g => g < -10).length} tests

ALL TEST RESULTS
${'='.repeat(80)}
Test | Area      | Speed    | Strategy        | Goal Area  | Gap
-----|-----------|----------|-----------------|------------|--------
`;

    // All tests table
    for (const { testCase, result } of results) {
        if (result.success && result.rectangle) {
            const strategy = result.rectangle.type || 'boundary-based';
            const goalInfo = result.goalRectangle
                ? `${result.goalRectangle.area.toFixed(1).padStart(10)} | ${(((result.rectangle.area - result.goalRectangle.area) / result.goalRectangle.area * 100).toFixed(1) + '%').padStart(6)}`
                : '          -'.padStart(10) + ' |      -';
            output += `${testCase.name.padEnd(4)} | ${result.rectangle.area.toFixed(1).padStart(9)} | ${result.calculationTime.toFixed(1).padStart(6)} ms | ${strategy.toUpperCase().padEnd(15)} | ${goalInfo}\n`;
        }
    }

    // Performance summary
    const avgTime = results.reduce((sum, r) => sum + (r.result.calculationTime || 0), 0) / results.length;
    const maxTime = Math.max(...results.map(r => r.result.calculationTime || 0));
    const minTime = Math.min(...results.map(r => r.result.calculationTime || 0));

    output += `\nPERFORMANCE METRICS
${'='.repeat(80)}
Speed:
  - Average: ${avgTime.toFixed(1)} ms
  - Fastest: ${minTime.toFixed(1)} ms
  - Slowest: ${maxTime.toFixed(1)} ms

Algorithm Distribution:
  - Boundary-based: ${results.filter(r => r.result.rectangle?.type === 'boundary-based').length} tests
  - Optimized: ${results.filter(r => r.result.rectangle?.type === 'optimized').length} tests

Centroid Strategy Distribution:
  - UNIFORM: ${results.filter(r => r.result.rectangle?.centroidStrategy === 'uniform').length} tests
  - HYBRID: ${results.filter(r => r.result.rectangle?.centroidStrategy === 'hybrid').length} tests

PRIORITY ISSUES
${'='.repeat(80)}
`;

    // List failing tests
    const failing = results.filter(r => {
        if (!r.result.goalRectangle) return false;
        const gap = ((r.result.rectangle.area - r.result.goalRectangle.area) / r.result.goalRectangle.area * 100);
        return gap <= -4;
    }).sort((a, b) => {
        const gapA = ((a.result.rectangle.area - a.result.goalRectangle.area) / a.result.goalRectangle.area * 100);
        const gapB = ((b.result.rectangle.area - b.result.goalRectangle.area) / b.result.goalRectangle.area * 100);
        return gapA - gapB;
    });

    if (failing.length > 0) {
        output += `Tests requiring attention (gap ≥ 4%):\n\n`;
        for (const { testCase, result } of failing) {
            const gap = ((result.rectangle.area - result.goalRectangle.area) / result.goalRectangle.area * 100);
            output += `${testCase.name}: ${gap.toFixed(1)}% gap\n`;
            output += `  Current: ${result.rectangle.area.toFixed(1)} sq px\n`;
            output += `  Goal:    ${result.goalRectangle.area.toFixed(1)} sq px\n`;
            output += `  Missing: ${(result.goalRectangle.area - result.rectangle.area).toFixed(1)} sq px\n\n`;
        }
    }

    output += `\nLast Updated: ${new Date().toLocaleString()}\n`;

    const resultsPath = path.join(testResultsDir, 'results.txt');
    fs.writeFileSync(resultsPath, output, 'utf8');
}

async function main() {
    log('\n' + '='.repeat(60), 'cyan');
    log('SVG Inscribed Rectangle Test Harness', 'cyan');
    log('='.repeat(60), 'cyan');

    // Clear TestResults directory (except tracking files)
    const testResultsDir = path.resolve('../TestResults');
    if (fs.existsSync(testResultsDir)) {
        log('Clearing previous test files...', 'yellow');
        const files = fs.readdirSync(testResultsDir);
        let clearedCount = 0;
        for (const file of files) {
            // Delete SVG files and test-output.txt, keep tracking .txt files
            if (file.endsWith('.svg') || file === 'test-output.txt') {
                fs.unlinkSync(path.join(testResultsDir, file));
                clearedCount++;
            }
        }
        log(`  ✓ Cleared ${clearedCount} files from TestResults (kept tracking .txt files)`, 'green');
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

    // Generate comprehensive results.txt
    log('\n  Generating results.txt...', 'cyan');
    generateResultsFile(results, testResultsDir);
    log(`  ✓ Saved results to results.txt`, 'green');

    log('\n' + '='.repeat(60), 'cyan');
    log(`Test results saved to: ${testResultsDir}`, 'cyan');
    log('='.repeat(60), 'cyan');
}

main().catch(err => {
    log(`Fatal error: ${err.message}`, 'red');
    console.error(err);
    process.exit(1);
});
