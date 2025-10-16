#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { testCases, config } from './test-config-rotated.js';

// Load required modules
const require = createRequire(import.meta.url);

// Load SvgViewerAlgorithms first (contains SpatialGrid and KDTree)
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');

// Make SpatialGrid globally available (needed by SvgViewerOptimized.js)
global.SpatialGrid = SvgViewerAlgorithms.SpatialGrid;

// Now load SvgViewerOptimized.js (needs global.SpatialGrid)
const { fastInscribedRectangle } = require('../wwwroot/SvgViewerOptimized.js');

// Load boundary-based and hybrid algorithms
const { boundaryBasedInscribedRectangle, hybridInscribedRectangle } = require('../wwwroot/SvgViewerBoundaryBased.js');

// Make algorithms globally available for hybrid algorithm
global.fastInscribedRectangle = fastInscribedRectangle;

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
    let polygon = SvgViewerAlgorithms.traverseOuterEdge(pathNetwork);
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

    // Remove collinear points from polygon before algorithms process it
    const originalPolygonLength = polygon.length;
    polygon = SvgViewerAlgorithms.removeCollinearPoints(polygon);
    if (polygon.length !== originalPolygonLength) {
        log(`  ✓ Removed ${originalPolygonLength - polygon.length} collinear points (${originalPolygonLength} → ${polygon.length} vertices)`, 'green');
    }

    log(`\n  Boundary polygon (${polygon.length} vertices):`, 'yellow');
    for (const v of polygon) {
        log(`    (${v.x.toFixed(1)}, ${v.y.toFixed(1)})`, 'yellow');
    }

    // Calculate inscribed rectangle using BOTH algorithms for comparison
    log(`\n  Calculating inscribed rectangles with BOTH algorithms...`, 'cyan');

    // Run boundary-based algorithm
    log(`\n  [1/2] Running boundary-based algorithm...`, 'cyan');
    const boundaryStartTime = performance.now();
    const boundaryOptions = {
        debugMode: false,
        maxAngles: 36,  // Test every 10° for edge alignment
        angleTolerance: 2,
        testPerpendicular: true
    };
    const boundaryRectangle = boundaryBasedInscribedRectangle(polygon, boundaryOptions);
    const boundaryEndTime = performance.now();
    const boundaryTime = boundaryEndTime - boundaryStartTime;

    // Run optimized algorithm WITH SEEDING from boundary-based results
    log(`\n  [2/2] Running optimized algorithm...`, 'cyan');
    const optimizedStartTime = performance.now();

    // Extract seed parameters from boundary-based results
    const seedCentroid = boundaryRectangle ? boundaryRectangle.centroid : null;
    const seedAngle = boundaryRectangle ? boundaryRectangle.angle : null;
    // Calculate aspect ratio from boundary-based result (width / height)
    const seedAspectRatio = (boundaryRectangle && boundaryRectangle.width && boundaryRectangle.height)
        ? boundaryRectangle.width / boundaryRectangle.height
        : null;
    // Pass entire rectangle as seed to ensure optimized never finds smaller
    const seedRectangle = boundaryRectangle || null;

    const optimizedOptions = {
        debugMode: false,
        maxTime: 60000,  // 60 seconds for complex shapes
        gridStep: 10.0,
        polylabelPrecision: 0.5,
        aspectRatios: [0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.7, 2.0, 2.5, 3.0],
        binarySearchPrecision: 0.01,
        binarySearchMaxIterations: 25,
        seedCentroid,      // Seed centroid from boundary-based result
        seedAngle,         // Seed angle from boundary-based result
        seedAspectRatio,   // Seed aspect ratio (width/height) from boundary-based result
        seedRectangle      // Entire boundary-based rectangle to use as baseline
    };
    const optimizedRectangle = fastInscribedRectangle(polygon, optimizedOptions);
    const optimizedEndTime = performance.now();
    const optimizedTime = optimizedEndTime - optimizedStartTime;

    // Process boundary-based result
    if (boundaryRectangle) {
        if (!boundaryRectangle.centroid && boundaryRectangle.corners) {
            const cx = boundaryRectangle.corners.reduce((sum, c) => sum + c.x, 0) / boundaryRectangle.corners.length;
            const cy = boundaryRectangle.corners.reduce((sum, c) => sum + c.y, 0) / boundaryRectangle.corners.length;
            boundaryRectangle.centroid = { x: cx, y: cy };
        }
        if (!boundaryRectangle.type) {
            boundaryRectangle.type = 'boundary-based';
        }
        log(`  ✓ Boundary-based found rectangle`, 'green');
        log(`    Area: ${boundaryRectangle.area.toFixed(1)} sq px`, 'green');
        log(`    Time: ${boundaryTime.toFixed(1)} ms`, 'green');
    } else {
        log(`  ✗ Boundary-based failed`, 'red');
    }

    // Process optimized result
    if (optimizedRectangle) {
        if (!optimizedRectangle.centroid && optimizedRectangle.corners) {
            const cx = optimizedRectangle.corners.reduce((sum, c) => sum + c.x, 0) / optimizedRectangle.corners.length;
            const cy = optimizedRectangle.corners.reduce((sum, c) => sum + c.y, 0) / optimizedRectangle.corners.length;
            optimizedRectangle.centroid = { x: cx, y: cy };
        }
        if (!optimizedRectangle.type) {
            optimizedRectangle.type = 'optimized';
        }
        log(`  ✓ Optimized found rectangle`, 'green');
        log(`    Area: ${optimizedRectangle.area.toFixed(1)} sq px`, 'green');
        log(`    Time: ${optimizedTime.toFixed(1)} ms`, 'green');
    } else {
        log(`  ✗ Optimized failed`, 'red');
    }

    // Determine best rectangle for primary result
    let bestRectangle = null;
    let bestTime = 0;
    if (boundaryRectangle && optimizedRectangle) {
        if (boundaryRectangle.area >= optimizedRectangle.area) {
            bestRectangle = boundaryRectangle;
            bestTime = boundaryTime;
            log(`\n  ✓ Boundary-based is best: ${boundaryRectangle.area.toFixed(1)} sq px`, 'green');
        } else {
            bestRectangle = optimizedRectangle;
            bestTime = optimizedTime;
            log(`\n  ✓ Optimized is best: ${optimizedRectangle.area.toFixed(1)} sq px`, 'green');
        }
    } else if (boundaryRectangle) {
        bestRectangle = boundaryRectangle;
        bestTime = boundaryTime;
    } else if (optimizedRectangle) {
        bestRectangle = optimizedRectangle;
        bestTime = optimizedTime;
    }

    if (!bestRectangle) {
        log(`  ❌ Both algorithms failed to calculate inscribed rectangle`, 'red');
        return {
            success: false,
            error: 'Both algorithms failed',
            polygon,
            pathCount: pathData.length,
            vertexCount: polygon.length,
            pathData,
            svgContent,
            viewBox
        };
    }

    log(`\n  Best Rectangle:`, 'yellow');
    log(`    Type: ${bestRectangle.type}`, 'yellow');
    log(`    Dimensions: ${bestRectangle.width.toFixed(1)} × ${bestRectangle.height.toFixed(1)}`, 'yellow');
    log(`    Area: ${bestRectangle.area.toFixed(1)} sq px`, 'yellow');
    log(`    Angle: ${bestRectangle.angle.toFixed(1)}°`, 'yellow');
    log(`    Time: ${bestTime.toFixed(1)} ms`, 'yellow');

    // Show goal comparison if goal rectangle exists
    if (goalRectangle) {
        const gap = ((bestRectangle.area - goalRectangle.area) / goalRectangle.area * 100);
        log(`\n  Goal Comparison:`, 'yellow');
        log(`    Best Area:      ${bestRectangle.area.toFixed(1)} sq px`, 'yellow');
        log(`    Goal Area:      ${goalRectangle.area.toFixed(1)} sq px`, 'yellow');
        log(`    Gap:            ${gap >= 0 ? '+' : ''}${gap.toFixed(1)}%`, gap >= -5 ? 'green' : 'red');
    }

    return {
        success: true,
        polygon,
        rectangle: bestRectangle,
        boundaryRectangle,
        optimizedRectangle,
        boundaryTime,
        optimizedTime,
        goalRectangle,
        pathCount: pathData.length,
        vertexCount: polygon.length,
        pathData,
        svgContent,
        viewBox,
        calculationTime: bestTime
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
    const { polygon, rectangle, boundaryRectangle, optimizedRectangle, boundaryTime, optimizedTime, pathData, viewBox, calculationTime, goalRectangle } = result;

    // Calculate bounds from the polygon to create an appropriate viewBox
    const bounds = calculateBounds(polygon);
    const padding = 50;
    const tableHeight = 120; // Space for table at bottom
    const compactTableWidth = 300; // Compact width for table
    const keyHeight = 80; // Space for key under title
    const horizontalMargin = 20; // Left and right margins for table

    const vbX = bounds.minX - padding;
    const vbY = bounds.minY - padding;
    const polygonWidth = bounds.maxX - bounds.minX + 2 * padding;
    // Calculate required width based on table + margins
    const requiredWidth = compactTableWidth + horizontalMargin * 2;
    const vbWidth = Math.max(polygonWidth, requiredWidth);
    const vbHeight = bounds.maxY - bounds.minY + 2 * padding + keyHeight + tableHeight;

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

    // Rectangle found case - show both boundary-based and optimized
    const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');

    // Build rectangles display
    let rectanglesDisplay = '';

    // Add boundary-based rectangle if available
    if (boundaryRectangle) {
        const boundaryPoints = boundaryRectangle.corners.map(c => `${c.x},${c.y}`).join(' ');
        rectanglesDisplay += `
  <!-- Boundary-Based Rectangle -->
  <polygon points="${boundaryPoints}"
           fill="rgba(255, 100, 100, 0.25)"
           stroke="#ff4040"
           stroke-width="2"
           stroke-dasharray="none"/>

  <!-- Boundary-Based Centroid -->
  <circle cx="${boundaryRectangle.centroid.x}" cy="${boundaryRectangle.centroid.y}" r="4" fill="#ff0000"/>
  <text x="${boundaryRectangle.centroid.x + 8}" y="${boundaryRectangle.centroid.y - 8}" font-size="8" fill="#ff0000">boundary</text>

  <!-- Boundary-Based Corners -->
  ${boundaryRectangle.corners.map((c, i) => `
  <circle cx="${c.x}" cy="${c.y}" r="3" fill="#ff4040"/>
  <text x="${c.x - 15}" y="${c.y + 15}" font-size="8" fill="#ff4040">b${i}</text>`).join('')}
`;
    }

    // Add optimized rectangle if available
    if (optimizedRectangle) {
        const optimizedPoints = optimizedRectangle.corners.map(c => `${c.x},${c.y}`).join(' ');
        rectanglesDisplay += `
  <!-- Optimized Rectangle -->
  <polygon points="${optimizedPoints}"
           fill="rgba(100, 255, 100, 0.25)"
           stroke="#40ff40"
           stroke-width="2"
           stroke-dasharray="5,5"/>

  <!-- Optimized Centroid -->
  <circle cx="${optimizedRectangle.centroid.x}" cy="${optimizedRectangle.centroid.y}" r="4" fill="#00aa00"/>
  <text x="${optimizedRectangle.centroid.x + 8}" y="${optimizedRectangle.centroid.y - 8}" font-size="8" fill="#00aa00">optimized</text>

  <!-- Optimized Corners -->
  ${optimizedRectangle.corners.map((c, i) => `
  <circle cx="${c.x}" cy="${c.y}" r="3" fill="#40ff40"/>
  <text x="${c.x + 10}" y="${c.y}" font-size="8" fill="#00aa00">o${i}</text>`).join('')}
`;
    }

    // Build key and comparison table at bottom
    const keyY = bounds.maxY + padding + 10; // Start key below the polygon
    const tableY = keyY + 40; // Start table below the key (reduced space)
    const tableX = vbX + 10;
    const fixedTableWidth = 300; // Fixed compact width for table

    // Title and Combinations Key
    const pathsKey = testCase.paths.join(' + ');
    let comparisonTable = `
  <!-- Title -->
  <text x="${tableX}" y="${keyY}" font-size="14" font-weight="bold">${testCase.name} - Algorithm Comparison</text>

  <!-- Combinations Key -->
  <text x="${tableX}" y="${keyY + 20}" font-size="9" fill="#666">Paths: ${pathsKey}</text>

  <!-- Table Header Background -->
  <rect x="${tableX}" y="${tableY}" width="${fixedTableWidth}" height="20" fill="#f0f0f0" stroke="#333" stroke-width="1"/>

  <!-- Table Header -->
  <text x="${tableX + 10}" y="${tableY + 14}" font-size="9" font-weight="bold">Algo</text>
  <text x="${tableX + 70}" y="${tableY + 14}" font-size="9" font-weight="bold">Area</text>
  <text x="${tableX + 150}" y="${tableY + 14}" font-size="9" font-weight="bold">Time</text>
  <text x="${tableX + 230}" y="${tableY + 14}" font-size="9" font-weight="bold">Status</text>
`;

    let rowY = tableY + 20;

    // Boundary-based row - ALWAYS show
    const bbIsBest = boundaryRectangle && (!optimizedRectangle || boundaryRectangle.area >= optimizedRectangle.area);
    const bbBgColor = bbIsBest ? 'rgba(255, 100, 100, 0.15)' : 'white';
    const bbAreaText = boundaryRectangle ? boundaryRectangle.area.toFixed(1) : 'FAIL';
    const bbTimeText = boundaryRectangle ? `${boundaryTime.toFixed(1)} ms` : 'FAIL';
    const bbStatusColor = boundaryRectangle ? (bbIsBest ? '#008800' : '#666') : '#cc0000';
    const bbStatusText = boundaryRectangle ? (bbIsBest ? '✓' : '') : '✗';
    comparisonTable += `
  <!-- Boundary-Based Row -->
  <rect x="${tableX}" y="${rowY}" width="${fixedTableWidth}" height="18" fill="${bbBgColor}" stroke="#333" stroke-width="1"/>
  <circle cx="${tableX + 15}" cy="${rowY + 9}" r="3" fill="#ff4040"/>
  <text x="${tableX + 23}" y="${rowY + 12}" font-size="8" fill="#ff4040">BB</text>
  <text x="${tableX + 70}" y="${rowY + 12}" font-size="8">${bbAreaText}</text>
  <text x="${tableX + 150}" y="${rowY + 12}" font-size="8">${bbTimeText}</text>
  <text x="${tableX + 230}" y="${rowY + 12}" font-size="8" fill="${bbStatusColor}">${bbStatusText}</text>
`;
    rowY += 18;

    // Optimized row - ALWAYS show
    const optIsBest = optimizedRectangle && (!boundaryRectangle || optimizedRectangle.area > boundaryRectangle.area);
    const optBgColor = optIsBest ? 'rgba(100, 255, 100, 0.15)' : 'white';
    const optAreaText = optimizedRectangle ? optimizedRectangle.area.toFixed(1) : 'FAIL';
    const optTimeText = optimizedRectangle ? `${optimizedTime.toFixed(1)} ms` : 'FAIL';
    const optStatusColor = optimizedRectangle ? (optIsBest ? '#008800' : '#666') : '#cc0000';
    const optStatusText = optimizedRectangle ? (optIsBest ? '✓' : '') : '✗';
    comparisonTable += `
  <!-- Optimized Row -->
  <rect x="${tableX}" y="${rowY}" width="${fixedTableWidth}" height="18" fill="${optBgColor}" stroke="#333" stroke-width="1"/>
  <circle cx="${tableX + 15}" cy="${rowY + 9}" r="3" fill="#40ff40"/>
  <text x="${tableX + 23}" y="${rowY + 12}" font-size="8" fill="#00aa00">Opt</text>
  <text x="${tableX + 70}" y="${rowY + 12}" font-size="8">${optAreaText}</text>
  <text x="${tableX + 150}" y="${rowY + 12}" font-size="8">${optTimeText}</text>
  <text x="${tableX + 230}" y="${rowY + 12}" font-size="8" fill="${optStatusColor}">${optStatusText}</text>
`;
    rowY += 18;

    // Summary row
    if (boundaryRectangle && optimizedRectangle) {
        const areaDiff = Math.abs(boundaryRectangle.area - optimizedRectangle.area);
        const areaDiffPercent = (areaDiff / Math.max(boundaryRectangle.area, optimizedRectangle.area)) * 100;
        comparisonTable += `
  <!-- Summary Row -->
  <rect x="${tableX}" y="${rowY}" width="${fixedTableWidth}" height="18" fill="#f8f8f8" stroke="#333" stroke-width="1"/>
  <text x="${tableX + 10}" y="${rowY + 12}" font-size="8" font-weight="bold">Diff:</text>
  <text x="${tableX + 70}" y="${rowY + 12}" font-size="8">${areaDiff.toFixed(1)} (${areaDiffPercent.toFixed(1)}%)</text>
`;
        rowY += 18;
    }

    // Goal comparison if available
    if (goalRectangle) {
        const gap = ((rectangle.area - goalRectangle.area) / goalRectangle.area * 100);
        const gapStatus = gap >= -5 ? '✓ PASS' : '✗ FAIL';
        const gapColor = gap >= -5 ? '#00aa00' : '#cc0000';
        comparisonTable += `
  <!-- Goal Row -->
  <rect x="${tableX}" y="${rowY}" width="${fixedTableWidth}" height="18" fill="#fff8e0" stroke="#333" stroke-width="1"/>
  <text x="${tableX + 10}" y="${rowY + 12}" font-size="8" font-weight="bold">Goal:</text>
  <text x="${tableX + 70}" y="${rowY + 12}" font-size="8">${goalRectangle.area.toFixed(1)}</text>
  <text x="${tableX + 150}" y="${rowY + 12}" font-size="8">Gap: ${gap.toFixed(1)}%</text>
  <text x="${tableX + 230}" y="${rowY + 12}" font-size="8" fill="${gapColor}" font-weight="bold">${gapStatus}</text>
`;
    }

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

  ${rectanglesDisplay}

  <!-- Boundary Vertices -->
  ${polygon.map((v, i) => `
  <circle cx="${v.x}" cy="${v.y}" r="3" fill="#4080ff"/>
  <text x="${v.x + 6}" y="${v.y - 6}" font-size="8" fill="#4080ff">p${i}</text>`).join('')}

  ${comparisonTable}
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

function generateResultsFile(results, testResultsDir, totalRuntimeMs) {
    let output = `INSCRIBED RECTANGLE TEST RESULTS
${'='.repeat(80)}
Latest Test Run: ${new Date().toLocaleString()}
Total Runtime: ${(totalRuntimeMs / 1000).toFixed(2)} seconds (${results.length} tests)

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

ALGORITHM COMPARISON RESULTS
${'='.repeat(80)}
Test | BB Area   | BB Time  | Opt Area  | Opt Time  | Winner | Diff %
-----|-----------|----------|-----------|-----------|--------|--------
`;

    // Algorithm comparison table - ALWAYS show both algorithms
    for (const { testCase, result } of results) {
        if (result.success) {
            const bbArea = result.boundaryRectangle ? result.boundaryRectangle.area.toFixed(1).padStart(9) : '    FAIL'.padStart(9);
            const bbTime = result.boundaryRectangle ? result.boundaryTime.toFixed(1).padStart(7) : '   FAIL'.padStart(7);
            const optArea = result.optimizedRectangle ? result.optimizedRectangle.area.toFixed(1).padStart(9) : '    FAIL'.padStart(9);
            const optTime = result.optimizedRectangle ? result.optimizedTime.toFixed(1).padStart(8) : '    FAIL'.padStart(8);

            let winner = '  -   ';
            let diffPercent = '    -';
            if (result.boundaryRectangle && result.optimizedRectangle) {
                winner = result.rectangle.type === 'boundary-based' ? 'BB' : 'Opt';
                const diff = Math.abs(result.boundaryRectangle.area - result.optimizedRectangle.area);
                diffPercent = (diff / Math.max(result.boundaryRectangle.area, result.optimizedRectangle.area) * 100).toFixed(1);
            }

            output += `${testCase.name.padEnd(4)} | ${bbArea} | ${bbTime} ms | ${optArea} | ${optTime} ms | ${winner.padEnd(6)} | ${diffPercent.padStart(5)}%\n`;
        }
    }

    output += `
ALL TEST RESULTS (BEST ALGORITHM)
${'='.repeat(80)}
Test | Area      | Speed    | Strategy        | Goal Area  | Gap
-----|-----------|----------|-----------------|------------|--------
`;

    // All tests table (best algorithm only)
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
    const mainStartTime = performance.now();

    log('\n' + '='.repeat(60), 'cyan');
    log('SVG Inscribed Rectangle Test Harness', 'cyan');
    log('='.repeat(60), 'cyan');

    // Clear TestResultsRotated directory (except tracking files)
    const testResultsDir = path.resolve('../TestResultsRotated');
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
        log(`  ✓ Cleared ${clearedCount} files from TestResultsRotated (kept tracking .txt files)`, 'green');
    } else {
        fs.mkdirSync(testResultsDir, { recursive: true });
        log('Created TestResultsRotated directory', 'green');
    }

    const results = [];

    // Run all sample tests
    const testsToRun = testCases;
    log(`Running ${testsToRun.length} tests from sample config`, 'yellow');

    for (const testCase of testsToRun) {
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
    const mainEndTime = performance.now();
    const totalRuntimeMs = mainEndTime - mainStartTime;
    generateResultsFile(results, testResultsDir, totalRuntimeMs);
    log(`  ✓ Saved results to results.txt`, 'green');

    log('\n' + '='.repeat(60), 'cyan');
    log(`Test results saved to: ${testResultsDir}`, 'cyan');
    log(`Total Runtime: ${(totalRuntimeMs / 1000).toFixed(2)} seconds`, 'cyan');
    log('='.repeat(60), 'cyan');
}

main().catch(err => {
    log(`Fatal error: ${err.message}`, 'red');
    console.error(err);
    process.exit(1);
});
