#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { testCases, config } from './test-config.js';
import { calculateInscribedRectangle } from './algorithms.js';

// Color codes for console output
const colors = {
    reset: '\x1b[0m',
    green: '\x1b[32m',
    red: '\x1b[31m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    cyan: '\x1b[36m'
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

function pathToVertices(pathData) {
    const commands = parseSvgPath(pathData);
    const vertices = [];
    let currentX = 0, currentY = 0;
    let startX = 0, startY = 0;

    for (const { command, params } of commands) {
        switch (command) {
            case 'm': // relative moveto
                currentX += params[0];
                currentY += params[1];
                startX = currentX;
                startY = currentY;
                vertices.push({ x: currentX, y: currentY });

                // Additional coordinate pairs are treated as lineto
                for (let i = 2; i < params.length; i += 2) {
                    currentX += params[i];
                    currentY += params[i + 1];
                    vertices.push({ x: currentX, y: currentY });
                }
                break;

            case 'M': // absolute moveto
                currentX = params[0];
                currentY = params[1];
                startX = currentX;
                startY = currentY;
                vertices.push({ x: currentX, y: currentY });

                for (let i = 2; i < params.length; i += 2) {
                    currentX = params[i];
                    currentY = params[i + 1];
                    vertices.push({ x: currentX, y: currentY });
                }
                break;

            case 'z':
            case 'Z': // closepath
                // Remove duplicate last vertex if it matches start
                if (vertices.length > 0) {
                    const last = vertices[vertices.length - 1];
                    if (Math.abs(last.x - startX) < 0.1 && Math.abs(last.y - startY) < 0.1) {
                        vertices.pop();
                    }
                }
                break;
        }
    }

    return vertices;
}

function findSharedVertices(paths) {
    const vertexMap = new Map();

    // Count occurrences of each vertex
    for (const path of paths) {
        for (const v of path.vertices) {
            const key = `${v.x.toFixed(1)},${v.y.toFixed(1)}`;
            vertexMap.set(key, (vertexMap.get(key) || 0) + 1);
        }
    }

    // Filter to outer vertices only (appear once)
    const outerVertices = [];
    const seenKeys = new Set();

    for (const path of paths) {
        for (const v of path.vertices) {
            const key = `${v.x.toFixed(1)},${v.y.toFixed(1)}`;
            if (vertexMap.get(key) === 1 && !seenKeys.has(key)) {
                outerVertices.push(v);
                seenKeys.add(key);
            }
        }
    }

    return outerVertices;
}

function sortVerticesByAngle(vertices) {
    // Calculate centroid
    const cx = vertices.reduce((sum, v) => sum + v.x, 0) / vertices.length;
    const cy = vertices.reduce((sum, v) => sum + v.y, 0) / vertices.length;

    // Sort by angle from centroid
    return vertices.sort((a, b) => {
        const angleA = Math.atan2(a.y - cy, a.x - cx);
        const angleB = Math.atan2(b.y - cy, b.x - cx);
        return angleA - angleB;
    });
}

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

    // Extract path data
    const pathData = extractPathData(svgContent, testCase.paths);
    if (!pathData) {
        return { success: false, error: 'Failed to extract path data' };
    }

    log(`  ✓ Extracted ${pathData.length} paths`, 'green');

    // Parse paths to vertices
    const paths = pathData.map(p => ({
        id: p.id,
        vertices: pathToVertices(p.d)
    }));

    for (const p of paths) {
        log(`    - ${p.id}: ${p.vertices.length} vertices`, 'blue');
    }

    // Find outer boundary
    const outerVertices = findSharedVertices(paths);
    log(`  ✓ Found ${outerVertices.length} outer boundary vertices`, 'green');

    // Sort vertices to form proper polygon
    const polygon = sortVerticesByAngle(outerVertices);

    log(`\n  Boundary polygon (${polygon.length} vertices):`, 'yellow');
    for (const v of polygon) {
        log(`    (${v.x.toFixed(1)}, ${v.y.toFixed(1)})`, 'yellow');
    }

    // Calculate inscribed rectangle
    log(`\n  Calculating inscribed rectangle...`, 'cyan');
    const rectangle = calculateInscribedRectangle(polygon);

    if (!rectangle) {
        log(`  ❌ Failed to calculate inscribed rectangle`, 'red');
        return {
            success: false,
            error: 'Rectangle calculation failed',
            polygon,
            pathCount: paths.length,
            vertexCount: polygon.length
        };
    }

    log(`  ✓ Found ${rectangle.type} rectangle`, 'green');
    log(`    Dimensions: ${rectangle.width.toFixed(1)} × ${rectangle.height.toFixed(1)}`, 'green');
    log(`    Area: ${rectangle.area.toFixed(1)} sq px`, 'green');
    log(`    Angle: ${rectangle.angle.toFixed(1)}°`, 'green');
    log(`    Centroid: (${rectangle.centroid.x.toFixed(1)}, ${rectangle.centroid.y.toFixed(1)})`, 'green');

    log(`\n  Rectangle corners:`, 'yellow');
    for (let i = 0; i < rectangle.corners.length; i++) {
        const c = rectangle.corners[i];
        log(`    [${i}] (${c.x.toFixed(1)}, ${c.y.toFixed(1)})`, 'yellow');
    }

    return {
        success: true,
        polygon,
        rectangle,
        pathCount: paths.length,
        vertexCount: polygon.length
    };
}

async function main() {
    log('\n' + '='.repeat(60), 'cyan');
    log('SVG Inscribed Rectangle Test Harness', 'cyan');
    log('='.repeat(60), 'cyan');

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
}

main().catch(err => {
    log(`Fatal error: ${err.message}`, 'red');
    console.error(err);
    process.exit(1);
});
