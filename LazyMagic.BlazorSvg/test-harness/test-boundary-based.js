// Test boundary-based algorithm vs optimized algorithm
import { createRequire } from 'module';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { testCases, config } from './test-config.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const require = createRequire(import.meta.url);
const { performance } = require('perf_hooks');

// Load algorithm modules
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');
global.SpatialGrid = SvgViewerAlgorithms.SpatialGrid;
const { fastInscribedRectangle } = require('../wwwroot/SvgViewerOptimized.js');
const { boundaryBasedInscribedRectangle } = require('../wwwroot/SvgViewerBoundaryBased.js');

const colors = {
    reset: '\x1b[0m',
    red: '\x1b[31m',
    green: '\x1b[32m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    cyan: '\x1b[36m',
    magenta: '\x1b[35m'
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

// Extract path data (same as parameter-sweep.js)
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

                let transformMatch = rectTag.match(/transform="matrix\(([^)]*)\)"/);
                let hasTransform = false;

                if (transformMatch) {
                    hasTransform = true;
                    const matrixValues = transformMatch[1].split(/[\s,]+/).map(parseFloat);
                    if (matrixValues.length === 6) {
                        const [a, b, c, d, e, f] = matrixValues;
                        corners = corners.map(corner => ({
                            x: a * corner.x + c * corner.y + e,
                            y: b * corner.x + d * corner.y + f
                        }));
                        const BALLROOM_SCALE = 48.345845;
                        corners = corners.map(corner => ({
                            x: corner.x / BALLROOM_SCALE,
                            y: corner.y / BALLROOM_SCALE
                        }));
                    }
                }

                if (!hasTransform) {
                    const rotateMatch = rectTag.match(/transform="rotate\(([^)]*)\)"/);
                    if (rotateMatch) {
                        hasTransform = true;
                        const angleStr = rotateMatch[1];
                        const angle = parseFloat(angleStr) * Math.PI / 180;
                        const cos = Math.cos(angle);
                        const sin = Math.sin(angle);
                        corners = corners.map(corner => ({
                            x: cos * corner.x - sin * corner.y,
                            y: sin * corner.x + cos * corner.y
                        }));
                        const BALLROOM_SCALE = 48.345845;
                        corners = corners.map(corner => ({
                            x: corner.x / BALLROOM_SCALE,
                            y: corner.y / BALLROOM_SCALE
                        }));
                    }
                }

                if (!hasTransform) {
                    const BALLROOM_SCALE = 48.345845;
                    corners = corners.map(corner => ({
                        x: corner.x / BALLROOM_SCALE,
                        y: corner.y / BALLROOM_SCALE
                    }));
                }

                const d = `M ${corners[0].x},${corners[0].y} L ${corners[1].x},${corners[1].y} L ${corners[2].x},${corners[2].y} L ${corners[3].x},${corners[3].y} Z`;
                paths.push({ id: pathId, d: d });
                continue;
            }
        }
    }

    return paths;
}

async function compareAlgorithms() {
    log('\n============================================================', 'cyan');
    log('Boundary-Based vs Optimized Algorithm Comparison', 'cyan');
    log('============================================================', 'cyan');

    const svgPath = path.resolve(config.svgPath);
    const svgContent = fs.readFileSync(svgPath, 'utf8');

    const results = [];

    for (const testCase of testCases) {
        log(`\n${testCase.name}: ${testCase.description}`, 'yellow');
        log(`Paths: ${testCase.paths.join(', ')}`, 'blue');

        // Extract paths and build polygon
        const pathData = extractPathData(svgContent, testCase.paths);
        if (!pathData || pathData.length === 0) {
            log('  ❌ Failed to extract paths', 'red');
            continue;
        }

        const allSegments = [];
        for (let i = 0; i < pathData.length; i++) {
            const segments = SvgViewerAlgorithms.parsePathToLineSegments(pathData[i].d, i);
            allSegments.push(...segments);
        }

        const mergedSegments = SvgViewerAlgorithms.mergeCoincidentPoints(allSegments);
        const markedSegments = SvgViewerAlgorithms.markSharedSegments(mergedSegments);
        const network = SvgViewerAlgorithms.joinPathsIntoNetwork(markedSegments);
        const polygon = SvgViewerAlgorithms.traverseOuterEdge(network);

        if (!polygon || polygon.length < 3) {
            log('  ❌ Failed to find boundary polygon', 'red');
            continue;
        }

        // Extract target area
        const targetPathData = extractPathData(svgContent, [testCase.name]);
        let targetArea = null;
        if (targetPathData && targetPathData.length > 0) {
            const targetSegments = SvgViewerAlgorithms.parsePathToLineSegments(targetPathData[0].d, 0);
            if (targetSegments && targetSegments.length >= 4) {
                const targetPoints = targetSegments.map(s => s.start);
                targetArea = Math.abs(SvgViewerAlgorithms.calculatePolygonArea(targetPoints));
            }
        }

        log(`  Polygon: ${polygon.length} vertices`, 'blue');
        if (targetArea) {
            log(`  Target area: ${targetArea.toFixed(1)} sq px`, 'blue');
        }

        // Test 1: Boundary-based algorithm
        log('\n  Testing boundary-based algorithm...', 'magenta');
        const boundaryResult = boundaryBasedInscribedRectangle(polygon, {
            debugMode: true,
            maxAngles: 8,
            testPerpendicular: true
        });

        // Test 2: Optimized algorithm
        log('\n  Testing optimized algorithm...', 'magenta');
        const optimizedResult = fastInscribedRectangle(polygon, {
            debugLog: false,
            maxTime: 1000,
            gridStep: 8.0,
            polylabelPrecision: 0.5,
            aspectRatios: [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0],
            binarySearchPrecision: 0.0001,
            binarySearchMaxIterations: 20
        });

        // Compare results
        log('\n  Comparison:', 'cyan');

        const boundaryArea = boundaryResult?.area || 0;
        const optimizedArea = optimizedResult?.area || 0;
        const boundaryTime = boundaryResult?.computeTime || 0;
        const optimizedTime = optimizedResult?.computeTime || 0;

        log(`    Boundary-based: ${boundaryArea.toFixed(1)} sq px in ${boundaryTime.toFixed(1)}ms`,
            boundaryArea >= optimizedArea ? 'green' : 'yellow');
        log(`    Optimized:      ${optimizedArea.toFixed(1)} sq px in ${optimizedTime.toFixed(1)}ms`,
            optimizedArea >= boundaryArea ? 'green' : 'yellow');

        if (targetArea) {
            const boundaryCoverage = (boundaryArea / targetArea * 100).toFixed(1);
            const optimizedCoverage = (optimizedArea / targetArea * 100).toFixed(1);
            log(`    Boundary coverage:  ${boundaryCoverage}%`,
                parseFloat(boundaryCoverage) >= 98 ? 'green' : 'yellow');
            log(`    Optimized coverage: ${optimizedCoverage}%`,
                parseFloat(optimizedCoverage) >= 98 ? 'green' : 'yellow');
        }

        const speedup = optimizedTime / boundaryTime;
        const areaImprovement = ((boundaryArea - optimizedArea) / optimizedArea * 100).toFixed(1);

        log(`    Speed: ${speedup.toFixed(2)}x ${speedup > 1 ? 'faster' : 'slower'}`,
            speedup > 1 ? 'green' : 'yellow');
        log(`    Area: ${areaImprovement > 0 ? '+' : ''}${areaImprovement}% vs optimized`,
            parseFloat(areaImprovement) > 0 ? 'green' : 'yellow');

        results.push({
            test: testCase.name,
            boundaryArea,
            optimizedArea,
            boundaryTime,
            optimizedTime,
            targetArea,
            boundaryCoverage: targetArea ? (boundaryArea / targetArea) : null,
            optimizedCoverage: targetArea ? (optimizedArea / targetArea) : null,
            speedup,
            polygon: polygon.length
        });
    }

    // Summary
    log('\n============================================================', 'cyan');
    log('Summary', 'cyan');
    log('============================================================', 'cyan');

    const avgBoundaryArea = results.reduce((sum, r) => sum + r.boundaryArea, 0) / results.length;
    const avgOptimizedArea = results.reduce((sum, r) => sum + r.optimizedArea, 0) / results.length;
    const avgBoundaryTime = results.reduce((sum, r) => sum + r.boundaryTime, 0) / results.length;
    const avgOptimizedTime = results.reduce((sum, r) => sum + r.optimizedTime, 0) / results.length;
    const avgSpeedup = avgOptimizedTime / avgBoundaryTime;

    const avgBoundaryCoverage = results
        .filter(r => r.boundaryCoverage !== null)
        .reduce((sum, r) => sum + r.boundaryCoverage, 0) / results.filter(r => r.boundaryCoverage !== null).length;
    const avgOptimizedCoverage = results
        .filter(r => r.optimizedCoverage !== null)
        .reduce((sum, r) => sum + r.optimizedCoverage, 0) / results.filter(r => r.optimizedCoverage !== null).length;

    log(`\nAverage area:`, 'yellow');
    log(`  Boundary-based: ${avgBoundaryArea.toFixed(1)} sq px`, avgBoundaryArea >= avgOptimizedArea ? 'green' : 'yellow');
    log(`  Optimized:      ${avgOptimizedArea.toFixed(1)} sq px`, avgOptimizedArea >= avgBoundaryArea ? 'green' : 'yellow');

    log(`\nAverage time:`, 'yellow');
    log(`  Boundary-based: ${avgBoundaryTime.toFixed(1)}ms`, avgBoundaryTime <= avgOptimizedTime ? 'green' : 'yellow');
    log(`  Optimized:      ${avgOptimizedTime.toFixed(1)}ms`, avgOptimizedTime <= avgBoundaryTime ? 'green' : 'yellow');

    log(`\nAverage coverage:`, 'yellow');
    log(`  Boundary-based: ${(avgBoundaryCoverage * 100).toFixed(1)}%`, avgBoundaryCoverage >= 0.98 ? 'green' : 'yellow');
    log(`  Optimized:      ${(avgOptimizedCoverage * 100).toFixed(1)}%`, avgOptimizedCoverage >= 0.98 ? 'green' : 'yellow');

    log(`\nSpeed improvement: ${avgSpeedup.toFixed(2)}x ${avgSpeedup > 1 ? 'faster' : 'slower'}`,
        avgSpeedup > 1 ? 'green' : 'yellow');

    const areaChange = ((avgBoundaryArea - avgOptimizedArea) / avgOptimizedArea * 100).toFixed(1);
    log(`Area change: ${areaChange > 0 ? '+' : ''}${areaChange}%`,
        parseFloat(areaChange) >= 0 ? 'green' : 'yellow');

    log('');
}

compareAlgorithms().catch(err => {
    console.error('Error:', err);
    process.exit(1);
});
