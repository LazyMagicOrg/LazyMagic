// Parameter sweep test runner
// Runs tests with different algorithm parameters and records results

import { createRequire } from 'module';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { testCases, config } from './test-config.js';
import { parameterSets } from './algorithm-params.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const require = createRequire(import.meta.url);
const { performance } = require('perf_hooks');

// Load algorithm modules
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');
global.SpatialGrid = SvgViewerAlgorithms.SpatialGrid;
const { fastInscribedRectangle } = require('../wwwroot/SvgViewerOptimized.js');

// Colors for console output
const colors = {
    reset: '\x1b[0m',
    red: '\x1b[31m',
    green: '\x1b[32m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    cyan: '\x1b[36m'
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

// Reuse extraction and test logic from test-runner.js
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

async function runTestWithParams(testCase, paramSetName, paramSet) {
    const svgPath = path.resolve(config.svgPath);
    const svgContent = fs.readFileSync(svgPath, 'utf8');

    // Extract test paths
    const pathData = extractPathData(svgContent, testCase.paths);
    if (!pathData || pathData.length === 0) {
        return { error: 'Failed to extract paths' };
    }

    // Extract target
    const targetPathData = extractPathData(svgContent, [testCase.name]);
    let targetArea = null;
    if (targetPathData && targetPathData.length > 0) {
        const targetSegments = SvgViewerAlgorithms.parsePathToLineSegments(targetPathData[0].d, 0);
        if (targetSegments && targetSegments.length >= 4) {
            const targetPoints = targetSegments.map(s => s.start);
            targetArea = Math.abs(SvgViewerAlgorithms.calculatePolygonArea(targetPoints));
        }
    }

    // Parse paths to line segments
    const allSegments = [];
    for (let i = 0; i < pathData.length; i++) {
        const segments = SvgViewerAlgorithms.parsePathToLineSegments(pathData[i].d, i);
        allSegments.push(...segments);
    }

    // Merge and find boundary
    const mergedSegments = SvgViewerAlgorithms.mergeCoincidentPoints(allSegments);
    const markedSegments = SvgViewerAlgorithms.markSharedSegments(mergedSegments);
    const network = SvgViewerAlgorithms.joinPathsIntoNetwork(markedSegments);
    const polygon = SvgViewerAlgorithms.traverseOuterEdge(network);

    if (!polygon || polygon.length < 3) {
        return { error: 'Failed to find boundary polygon' };
    }

    // Run algorithm with parameters
    const startTime = performance.now();
    const rectangle = fastInscribedRectangle(polygon, {
        debugLog: false,  // Disable debug to reduce output
        maxTime: 1000,     // Allow up to 1000ms for complex polygons
        ...paramSet
    });
    const endTime = performance.now();
    const calculationTime = endTime - startTime;

    if (!rectangle) {
        return { error: 'No rectangle found' };
    }

    // Calculate coverage
    let coverage = null;
    if (targetArea !== null) {
        coverage = rectangle.area / targetArea;
    }

    return {
        area: rectangle.area,
        width: Math.sqrt(rectangle.area / (rectangle.type === 'optimized' ? 2.5 : 1.0)),
        angle: rectangle.angle || 0,
        targetArea,
        coverage,
        time: calculationTime,
        polygon: polygon.length
    };
}

async function runParameterSweep() {
    log('\n============================================================', 'cyan');
    log('Parameter Sweep Test Runner', 'cyan');
    log('============================================================', 'cyan');

    const results = {};
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);

    // Test each parameter set
    for (const [paramName, paramSet] of Object.entries(parameterSets)) {
        log(`\nTesting parameter set: ${paramSet.name}`, 'yellow');
        log(`  ${paramSet.description}`, 'blue');

        results[paramName] = {
            name: paramSet.name,
            description: paramSet.description,
            params: paramSet,
            tests: {}
        };

        // Run each test case
        for (const testCase of testCases) {
            process.stdout.write(`  Running ${testCase.name}... `);

            const result = await runTestWithParams(testCase, paramName, paramSet);
            results[paramName].tests[testCase.name] = result;

            if (result.error) {
                log(`FAILED: ${result.error}`, 'red');
            } else {
                const coveragePercent = result.coverage ? (result.coverage * 100).toFixed(1) : 'N/A';
                const status = result.coverage && result.coverage >= 0.98 ? '✓' : '✗';
                const color = result.coverage && result.coverage >= 0.98 ? 'green' : 'red';
                log(`${status} ${coveragePercent}% (${result.time.toFixed(1)}ms)`, color);
            }
        }
    }

    // Generate summary report
    const reportPath = path.join(__dirname, `sweep-results-${timestamp}.md`);
    let report = `# Parameter Sweep Results - ${new Date().toISOString()}\n\n`;

    // Summary table
    report += '## Summary Table\n\n';
    report += '| Parameter Set | Test01 | Test02 | Test03 | Avg Coverage | Avg Time |\n';
    report += '|---------------|--------|--------|--------|--------------|----------|\n';

    for (const [paramName, data] of Object.entries(results)) {
        const test01 = data.tests.Test01.coverage ? `${(data.tests.Test01.coverage * 100).toFixed(1)}%` : 'N/A';
        const test02 = data.tests.Test02.coverage ? `${(data.tests.Test02.coverage * 100).toFixed(1)}%` : 'N/A';
        const test03 = data.tests.Test03.coverage ? `${(data.tests.Test03.coverage * 100).toFixed(1)}%` : 'N/A';

        const coverages = [data.tests.Test01.coverage, data.tests.Test02.coverage, data.tests.Test03.coverage].filter(c => c !== null);
        const avgCoverage = coverages.length > 0 ? (coverages.reduce((a, b) => a + b, 0) / coverages.length * 100).toFixed(1) : 'N/A';

        const times = Object.values(data.tests).filter(t => t.time !== undefined).map(t => t.time);
        const avgTime = times.length > 0 ? (times.reduce((a, b) => a + b, 0) / times.length).toFixed(1) : 'N/A';

        report += `| ${data.name} | ${test01} | ${test02} | ${test03} | ${avgCoverage}% | ${avgTime}ms |\n`;
    }

    // Detailed results
    report += '\n## Detailed Results\n\n';

    for (const [paramName, data] of Object.entries(results)) {
        report += `### ${data.name}\n\n`;
        report += `**Description**: ${data.description}\n\n`;
        report += `**Parameters**:\n`;
        report += `- Grid Step: ${data.params.gridStep}px\n`;
        report += `- Polylabel Precision: ${data.params.polylabelPrecision}\n`;
        report += `- Aspect Ratios: [${data.params.aspectRatios.join(', ')}]\n`;
        report += `- Binary Search Precision: ${data.params.binarySearchPrecision}\n`;
        report += `- Binary Search Max Iterations: ${data.params.binarySearchMaxIterations}\n\n`;

        for (const [testName, result] of Object.entries(data.tests)) {
            if (result.error) {
                report += `**${testName}**: ❌ ${result.error}\n\n`;
            } else {
                const coverage = result.coverage ? `${(result.coverage * 100).toFixed(1)}%` : 'N/A';
                const status = result.coverage && result.coverage >= 0.98 ? '✅ PASS' : '❌ FAIL';
                report += `**${testName}**: ${status}\n`;
                report += `- Coverage: ${coverage}\n`;
                report += `- Found Area: ${result.area.toFixed(1)} sq px\n`;
                report += `- Target Area: ${result.targetArea ? result.targetArea.toFixed(1) : 'N/A'} sq px\n`;
                report += `- Time: ${result.time.toFixed(1)}ms\n`;
                report += `- Polygon Vertices: ${result.polygon}\n\n`;
            }
        }
    }

    fs.writeFileSync(reportPath, report);
    log(`\n✓ Results saved to: ${reportPath}`, 'green');

    return results;
}

// Run the sweep
runParameterSweep().catch(err => {
    console.error('Error running parameter sweep:', err);
    process.exit(1);
});
