// Analyze winding direction of SVG paths
// Usage: node analyze-winding.js <path-to-svg>

import fs from 'fs';
import { DOMParser } from 'xmldom';

const args = process.argv.slice(2);
if (args.length < 1) {
    console.error('Usage: node analyze-winding.js <path-to-svg>');
    console.error('Example: node analyze-winding.js "C:\\path\\to\\Level2.svg"');
    process.exit(1);
}

const svgPath = args[0];

// Parse path data to extract vertices
function parsePathData(pathData) {
    const vertices = [];

    // Simplified path parser - handles M, L, C, H, V, Z commands
    const commands = pathData.match(/[MmLlCcHhVvZz][^MmLlCcHhVvZz]*/g);
    if (!commands) return vertices;

    let currentX = 0;
    let currentY = 0;

    for (const cmd of commands) {
        const type = cmd[0];
        const args = cmd.slice(1).trim().split(/[\s,]+/).filter(s => s).map(parseFloat);

        switch (type) {
            case 'M': // Move absolute
                if (args.length >= 2) {
                    currentX = args[0];
                    currentY = args[1];
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'm': // Move relative
                if (args.length >= 2) {
                    currentX += args[0];
                    currentY += args[1];
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'L': // Line absolute
                for (let i = 0; i < args.length; i += 2) {
                    currentX = args[i];
                    currentY = args[i + 1];
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'l': // Line relative
                for (let i = 0; i < args.length; i += 2) {
                    currentX += args[i];
                    currentY += args[i + 1];
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'H': // Horizontal line absolute
                for (const x of args) {
                    currentX = x;
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'h': // Horizontal line relative
                for (const dx of args) {
                    currentX += dx;
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'V': // Vertical line absolute
                for (const y of args) {
                    currentY = y;
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'v': // Vertical line relative
                for (const dy of args) {
                    currentY += dy;
                    vertices.push({ x: currentX, y: currentY });
                }
                break;
            case 'C': // Cubic bezier absolute
                for (let i = 0; i < args.length; i += 6) {
                    // Take only the endpoint of the curve
                    if (i + 5 < args.length) {
                        currentX = args[i + 4];
                        currentY = args[i + 5];
                        vertices.push({ x: currentX, y: currentY });
                    }
                }
                break;
            case 'c': // Cubic bezier relative
                for (let i = 0; i < args.length; i += 6) {
                    if (i + 5 < args.length) {
                        currentX += args[i + 4];
                        currentY += args[i + 5];
                        vertices.push({ x: currentX, y: currentY });
                    }
                }
                break;
            case 'Z':
            case 'z':
                // Close path - no new vertex needed
                break;
        }
    }

    return vertices;
}

// Calculate signed area using Shoelace formula
function calculateSignedArea(vertices) {
    if (vertices.length < 3) return 0;

    let area = 0;
    for (let i = 0; i < vertices.length; i++) {
        const j = (i + 1) % vertices.length;
        area += vertices[i].x * vertices[j].y;
        area -= vertices[j].x * vertices[i].y;
    }

    return area / 2;
}

// Parse transform attribute
function parseTransform(transformStr) {
    if (!transformStr) return { scale: 1, translateX: 0, translateY: 0 };

    const result = { scale: 1, translateX: 0, translateY: 0 };

    const scaleMatch = transformStr.match(/scale\(([\d.]+)\)/);
    if (scaleMatch) {
        result.scale = parseFloat(scaleMatch[1]);
    }

    const translateMatch = transformStr.match(/translate\(([\d.]+)[,\s]+([\d.]+)\)/);
    if (translateMatch) {
        result.translateX = parseFloat(translateMatch[1]);
        result.translateY = parseFloat(translateMatch[2]);
    }

    return result;
}

// Apply transform to vertices
function applyTransform(vertices, transform) {
    return vertices.map(v => ({
        x: v.x * transform.scale + transform.translateX,
        y: v.y * transform.scale + transform.translateY
    }));
}

console.log('Analyzing SVG path winding directions...\n');
console.log(`File: ${svgPath}\n`);

// Read and parse SVG
const svgContent = fs.readFileSync(svgPath, 'utf8');
const parser = new DOMParser();
const doc = parser.parseFromString(svgContent, 'text/xml');

// Find all path elements
const paths = doc.getElementsByTagName('path');
const results = [];

for (let i = 0; i < paths.length; i++) {
    const path = paths[i];
    const id = path.getAttribute('id');

    // Only analyze room section paths
    if (!id || !id.includes('Room')) continue;

    const pathData = path.getAttribute('d');
    if (!pathData) continue;

    // Parse vertices
    let vertices = parsePathData(pathData);
    if (vertices.length < 3) continue;

    // Get transforms from parent groups
    let element = path;
    const transforms = [];
    while (element.parentNode) {
        element = element.parentNode;
        if (element.getAttribute) {
            const transform = element.getAttribute('transform');
            if (transform) {
                transforms.push(parseTransform(transform));
            }
        }
    }

    // Also check path's own transform
    const pathTransform = path.getAttribute('transform');
    if (pathTransform) {
        transforms.unshift(parseTransform(pathTransform));
    }

    // Apply all transforms
    for (const transform of transforms) {
        vertices = applyTransform(vertices, transform);
    }

    // Calculate signed area
    const signedArea = calculateSignedArea(vertices);
    const winding = signedArea > 0 ? 'CCW ↺' : 'CW ↻';

    results.push({
        id,
        vertexCount: vertices.length,
        signedArea: signedArea.toFixed(2),
        absArea: Math.abs(signedArea).toFixed(2),
        winding
    });
}

// Sort by ID for easier reading
results.sort((a, b) => a.id.localeCompare(b.id));

// Count winding directions
const ccwCount = results.filter(r => r.winding.includes('CCW')).length;
const cwCount = results.filter(r => r.winding.includes('CW')).length;
const majorityWinding = ccwCount >= cwCount ? 'CCW ↺' : 'CW ↻';

console.log('═'.repeat(100));
console.log('WINDING DIRECTION ANALYSIS');
console.log('═'.repeat(100));
console.log();

// Print table
console.log('Section ID                          | Vertices | Signed Area    | Abs Area       | Winding | Status');
console.log('─'.repeat(100));

for (const result of results) {
    const needsReverse = result.winding !== majorityWinding ? '⚠ REVERSE' : '✓ OK';
    const status = result.winding !== majorityWinding ? '\x1b[33m' : '\x1b[32m'; // Yellow or Green
    const reset = '\x1b[0m';

    console.log(
        `${result.id.padEnd(35)} | ${String(result.vertexCount).padEnd(8)} | ` +
        `${result.signedArea.padStart(14)} | ${result.absArea.padStart(14)} | ` +
        `${status}${result.winding.padEnd(7)}${reset} | ${status}${needsReverse}${reset}`
    );
}

console.log('─'.repeat(100));
console.log();
console.log('SUMMARY:');
console.log(`  Total sections analyzed: ${results.length}`);
console.log(`  Counter-clockwise (CCW ↺): ${ccwCount}`);
console.log(`  Clockwise (CW ↻): ${cwCount}`);
console.log(`  Majority winding: ${majorityWinding}`);
console.log();

const toReverse = results.filter(r => r.winding !== majorityWinding);
if (toReverse.length > 0) {
    console.log('⚠ SECTIONS THAT NEED REVERSING:');
    toReverse.forEach(r => console.log(`  - ${r.id}`));
    console.log();
    console.log('These sections have opposite winding from the majority and will cause');
    console.log('corruption when merged with adjacent sections.');
} else {
    console.log('✓ All sections have consistent winding direction!');
}

console.log();
console.log('═'.repeat(100));
