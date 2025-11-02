/**
 * Generic Test Runner for Inscribed Rectangle Algorithms
 *
 * Loads test configuration from JSON and executes tests using the
 * unified inscribed rectangle algorithm or legacy algorithms.
 *
 * Usage:
 *   node run-tests.js <config-file>
 *
 * Example:
 *   node run-tests.js test-configs/hollowsquare-sample.json
 */

import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';

// Suppress verbose console output from algorithms during testing
// MUST be done BEFORE loading algorithm modules
// Keep console.error and console.warn for actual errors
const originalConsoleLog = console.log;
console.log = function(...args) {
    // Only log if VERBOSE environment variable is set
    if (process.env.VERBOSE === 'true') {
        originalConsoleLog.apply(console, args);
    }
    // Otherwise, suppress all console.log output
};

// Load the algorithms (CommonJS modules) from LazyMagic.BlazorSvg
// console.log is now suppressed, so algorithms won't flood output
const require = createRequire(import.meta.url);
const kdtree = require('../../LazyMagic.BlazorSvg/wwwroot/kdtree.js'); // Required by optimized algorithm
const unifiedAlgo = require('../../LazyMagic.BlazorSvg/wwwroot/SvgViewerInscribedRect.js');
const boundaryBased = require('../../LazyMagic.BlazorSvg/wwwroot/SvgViewerBoundaryBased.js');
const optimized = require('../../LazyMagic.BlazorSvg/wwwroot/SvgViewerOptimized.js');
const boardroom = require('../../LazyMagic.BlazorSvg/wwwroot/SvgViewerBoardroom.js');

// Make KDTree and SpatialGrid available globally for the algorithms
global.KDTree = kdtree.KDTree;
global.SpatialGrid = kdtree.SpatialGrid;
global.SpatialHash = kdtree.SpatialHash;
global.boundaryBasedInscribedRectangle = boundaryBased.boundaryBasedInscribedRectangle;
global.fastInscribedRectangle = optimized.fastInscribedRectangle;

/**
 * Parse SVG transform string into a transformation matrix
 * Returns a 2D transformation matrix [a, b, c, d, e, f] representing:
 * | a c e |   (e, f = translation)
 * | b d f |   (a, d = scale)
 * | 0 0 1 |   (b, c = rotation/skew)
 */
function parseTransformMatrix(transformString) {
    // Identity matrix
    let matrix = { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0 };

    if (!transformString) return matrix;

    // Parse translate(x, y) or translate(x)
    const translateMatch = transformString.match(/translate\(\s*([^,\s)]+)(?:[\s,]+([^)]+))?\s*\)/i);
    if (translateMatch) {
        matrix.e += parseFloat(translateMatch[1]);
        matrix.f += parseFloat(translateMatch[2] || 0);
    }

    // Parse scale(x, y) or scale(x)
    const scaleMatch = transformString.match(/scale\(\s*([^,\s)]+)(?:[\s,]+([^)]+))?\s*\)/i);
    if (scaleMatch) {
        const sx = parseFloat(scaleMatch[1]);
        const sy = parseFloat(scaleMatch[2] || scaleMatch[1]); // If sy not specified, use sx
        matrix.a *= sx;
        matrix.d *= sy;
        // Scale also affects translation
        matrix.e *= sx;
        matrix.f *= sy;
    }

    // Parse rotate(angle, cx, cy) or rotate(angle)
    const rotateMatch = transformString.match(/rotate\(\s*([^,\s)]+)(?:[\s,]+([^,\s)]+)[\s,]+([^)]+))?\s*\)/i);
    if (rotateMatch) {
        const angle = parseFloat(rotateMatch[1]) * Math.PI / 180; // Convert to radians
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        if (rotateMatch[2] && rotateMatch[3]) {
            // Rotate around point (cx, cy)
            const cx = parseFloat(rotateMatch[2]);
            const cy = parseFloat(rotateMatch[3]);
            // translate(-cx, -cy) * rotate * translate(cx, cy)
            const newA = matrix.a * cos - matrix.b * sin;
            const newB = matrix.a * sin + matrix.b * cos;
            const newC = matrix.c * cos - matrix.d * sin;
            const newD = matrix.c * sin + matrix.d * cos;
            matrix.e += cx - (cx * cos - cy * sin);
            matrix.f += cy - (cx * sin + cy * cos);
            matrix.a = newA;
            matrix.b = newB;
            matrix.c = newC;
            matrix.d = newD;
        } else {
            // Rotate around origin
            const newA = matrix.a * cos - matrix.b * sin;
            const newB = matrix.a * sin + matrix.b * cos;
            const newC = matrix.c * cos - matrix.d * sin;
            const newD = matrix.c * sin + matrix.d * cos;
            matrix.a = newA;
            matrix.b = newB;
            matrix.c = newC;
            matrix.d = newD;
        }
    }

    // Parse matrix(a, b, c, d, e, f)
    const matrixMatch = transformString.match(/matrix\(\s*([^,\s)]+)[\s,]+([^,\s)]+)[\s,]+([^,\s)]+)[\s,]+([^,\s)]+)[\s,]+([^,\s)]+)[\s,]+([^)]+)\s*\)/i);
    if (matrixMatch) {
        matrix = {
            a: parseFloat(matrixMatch[1]),
            b: parseFloat(matrixMatch[2]),
            c: parseFloat(matrixMatch[3]),
            d: parseFloat(matrixMatch[4]),
            e: parseFloat(matrixMatch[5]),
            f: parseFloat(matrixMatch[6])
        };
    }

    return matrix;
}

/**
 * Multiply two transformation matrices
 */
function multiplyMatrices(m1, m2) {
    return {
        a: m1.a * m2.a + m1.c * m2.b,
        b: m1.b * m2.a + m1.d * m2.b,
        c: m1.a * m2.c + m1.c * m2.d,
        d: m1.b * m2.c + m1.d * m2.d,
        e: m1.a * m2.e + m1.c * m2.f + m1.e,
        f: m1.b * m2.e + m1.d * m2.f + m1.f
    };
}

/**
 * Extract parent group transforms by finding the path and traversing up the DOM
 */
function extractParentTransforms(svgContent, pathId) {
    const escapedId = pathId.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

    // Find the path element position in the content
    const pathRegex = new RegExp(`<path[^>]*\\bid="${escapedId}"[^>]*`, 'i');
    const pathMatch = pathRegex.exec(svgContent);

    if (!pathMatch) return { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0 }; // Identity matrix

    const pathPosition = pathMatch.index;

    // Find all <g> tags before the path
    const beforePath = svgContent.substring(0, pathPosition);
    const groupMatches = [...beforePath.matchAll(/<g[^>]*>/gi)];

    // Build a stack of open groups (accounting for closing tags)
    let openGroups = [];
    let position = 0;

    for (const match of groupMatches) {
        const groupTag = match[0];
        const groupPosition = match.index;

        // Check for closing tags between last position and this group
        const between = svgContent.substring(position, groupPosition);
        const closingTags = (between.match(/<\/g>/gi) || []).length;

        // Remove closed groups from stack
        openGroups = openGroups.slice(0, openGroups.length - closingTags);

        // Add this group to stack
        openGroups.push(groupTag);
        position = groupPosition + groupTag.length;
    }

    // Combine all parent transforms (innermost to outermost order)
    let combinedMatrix = { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0 }; // Identity

    for (const groupTag of openGroups) {
        const transformMatch = groupTag.match(/\btransform="([^"]*)"/i);
        if (transformMatch) {
            const groupMatrix = parseTransformMatrix(transformMatch[1]);
            combinedMatrix = multiplyMatrices(combinedMatrix, groupMatrix);
        }
    }

    return combinedMatrix;
}

/**
 * Extract path data and transform information from SVG content
 * Returns path data in original coordinate space plus transform info
 * Handles both <path> elements with 'd' attribute and <polygon> elements with 'points' attribute
 */
function extractPathData(svgContent, pathIds) {
    const paths = [];
    for (const pathId of pathIds) {
        // Escape pathId for use in regex (in case it contains special chars)
        const escapedId = pathId.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

        // Find the <path> element opening tag
        const pathRegex = new RegExp(`<path[^>]*\\bid="${escapedId}"[^>]*\\/>|<path[^>]*\\bid="${escapedId}"[^>]*>`, 'i');
        const pathMatch = svgContent.match(pathRegex);

        if (pathMatch) {
            const element = pathMatch[0];

            // Extract 'd' attribute
            const dMatch = element.match(/\bd="([^"]*)"/i);
            if (!dMatch) continue;

            let pathD = dMatch[1];

            // Get parent group transforms (but don't apply them yet)
            let combinedMatrix = extractParentTransforms(svgContent, pathId);

            // Extract path's own transform attribute if present
            const transformMatch = element.match(/\btransform="([^"]*)"/i);
            if (transformMatch) {
                const pathMatrix = parseTransformMatrix(transformMatch[1]);
                combinedMatrix = multiplyMatrices(combinedMatrix, pathMatrix);
            }

            // Store both the original path data and the transform matrix
            // The path data stays in original coordinate space
            paths.push({
                id: pathId,
                d: pathD,
                transform: combinedMatrix
            });
            continue;
        }

        // Try to find <polygon> element with 'points' attribute
        const polygonRegex = new RegExp(`<polygon[^>]*\\bid="${escapedId}"[^>]*points="([^"]*)"`, 'i');
        const polygonMatch = svgContent.match(polygonRegex);

        if (polygonMatch) {
            // Convert polygon points to path 'd' format
            const points = polygonMatch[1].trim().split(/\s+/);
            const pathCommands = points.map((point, index) => {
                const [x, y] = point.split(',');
                return index === 0 ? `M ${x} ${y}` : `L ${x} ${y}`;
            }).join(' ') + ' Z';

            // Get transform for polygon too
            let combinedMatrix = extractParentTransforms(svgContent, pathId);

            paths.push({
                id: pathId,
                d: pathCommands,
                transform: combinedMatrix
            });
        }
    }

    return paths;
}

/**
 * Apply transformation matrix to a single point
 */
function transformPoint(x, y, matrix) {
    return {
        x: matrix.a * x + matrix.c * y + matrix.e,
        y: matrix.b * x + matrix.d * y + matrix.f
    };
}

/**
 * Apply transformation matrix to SVG path data
 * Handles M, L, H, V, C, S, Q, T, A, Z commands with both absolute and relative coordinates
 */
function applyMatrixToPath(pathD, matrix) {
    // Parse path commands and apply matrix transformation to all coordinates
    const commands = pathD.match(/[MLHVCSQTAZ][^MLHVCSQTAZ]*/gi) || [];
    let currentX = 0, currentY = 0;
    let startX = 0, startY = 0;
    let lastControlX = 0, lastControlY = 0; // For smooth curve commands

    const transformedCommands = [];

    for (const cmd of commands) {
        const type = cmd[0].toUpperCase();
        const isRelative = cmd[0] === cmd[0].toLowerCase() && type !== 'Z';
        const params = cmd.slice(1).trim().split(/[\s,]+/).map(Number).filter(n => !isNaN(n));

        if (type === 'M') {
            // M can have multiple pairs of coordinates (implicit L commands)
            for (let i = 0; i < params.length; i += 2) {
                currentX = isRelative ? currentX + params[i] : params[i];
                currentY = isRelative ? currentY + params[i + 1] : params[i + 1];

                const transformed = transformPoint(currentX, currentY, matrix);

                if (i === 0) {
                    startX = transformed.x;
                    startY = transformed.y;
                    transformedCommands.push(`M ${transformed.x} ${transformed.y}`);
                } else {
                    // Subsequent coordinate pairs are treated as L commands
                    transformedCommands.push(`L ${transformed.x} ${transformed.y}`);
                }

                lastControlX = transformed.x;
                lastControlY = transformed.y;
            }
        } else if (type === 'L') {
            // L can also have multiple pairs of coordinates
            for (let i = 0; i < params.length; i += 2) {
                currentX = isRelative ? currentX + params[i] : params[i];
                currentY = isRelative ? currentY + params[i + 1] : params[i + 1];

                const transformed = transformPoint(currentX, currentY, matrix);
                transformedCommands.push(`L ${transformed.x} ${transformed.y}`);

                lastControlX = transformed.x;
                lastControlY = transformed.y;
            }
        } else if (type === 'H') {
            currentX = isRelative ? currentX + params[0] : params[0];
            const transformed = transformPoint(currentX, currentY, matrix);
            transformedCommands.push(`L ${transformed.x} ${transformed.y}`);
            lastControlX = transformed.x;
            lastControlY = transformed.y;
        } else if (type === 'V') {
            currentY = isRelative ? currentY + params[0] : params[0];
            const transformed = transformPoint(currentX, currentY, matrix);
            transformedCommands.push(`L ${transformed.x} ${transformed.y}`);
            lastControlX = transformed.x;
            lastControlY = transformed.y;
        } else if (type === 'C') {
            // Cubic Bezier curve
            for (let i = 0; i < params.length; i += 6) {
                const x1 = isRelative ? currentX + params[i] : params[i];
                const y1 = isRelative ? currentY + params[i + 1] : params[i + 1];
                const x2 = isRelative ? currentX + params[i + 2] : params[i + 2];
                const y2 = isRelative ? currentY + params[i + 3] : params[i + 3];
                currentX = isRelative ? currentX + params[i + 4] : params[i + 4];
                currentY = isRelative ? currentY + params[i + 5] : params[i + 5];

                const t1 = transformPoint(x1, y1, matrix);
                const t2 = transformPoint(x2, y2, matrix);
                const t3 = transformPoint(currentX, currentY, matrix);

                transformedCommands.push(`C ${t1.x} ${t1.y} ${t2.x} ${t2.y} ${t3.x} ${t3.y}`);

                lastControlX = t2.x;
                lastControlY = t2.y;
            }
        } else if (type === 'S') {
            // Smooth cubic Bezier curve
            for (let i = 0; i < params.length; i += 4) {
                const x2 = isRelative ? currentX + params[i] : params[i];
                const y2 = isRelative ? currentY + params[i + 1] : params[i + 1];
                currentX = isRelative ? currentX + params[i + 2] : params[i + 2];
                currentY = isRelative ? currentY + params[i + 3] : params[i + 3];

                const t2 = transformPoint(x2, y2, matrix);
                const t3 = transformPoint(currentX, currentY, matrix);

                transformedCommands.push(`L ${t3.x} ${t3.y}`); // Approximate as line

                lastControlX = t2.x;
                lastControlY = t2.y;
            }
        } else if (type === 'Q') {
            // Quadratic Bezier curve
            for (let i = 0; i < params.length; i += 4) {
                const x1 = isRelative ? currentX + params[i] : params[i];
                const y1 = isRelative ? currentY + params[i + 1] : params[i + 1];
                currentX = isRelative ? currentX + params[i + 2] : params[i + 2];
                currentY = isRelative ? currentY + params[i + 3] : params[i + 3];

                const t1 = transformPoint(x1, y1, matrix);
                const t2 = transformPoint(currentX, currentY, matrix);

                transformedCommands.push(`L ${t2.x} ${t2.y}`); // Approximate as line

                lastControlX = t1.x;
                lastControlY = t1.y;
            }
        } else if (type === 'T') {
            // Smooth quadratic Bezier curve
            for (let i = 0; i < params.length; i += 2) {
                currentX = isRelative ? currentX + params[i] : params[i];
                currentY = isRelative ? currentY + params[i + 1] : params[i + 1];

                const transformed = transformPoint(currentX, currentY, matrix);
                transformedCommands.push(`L ${transformed.x} ${transformed.y}`); // Approximate as line

                lastControlX = transformed.x;
                lastControlY = transformed.y;
            }
        } else if (type === 'A') {
            // Arc - approximate as line to endpoint
            for (let i = 0; i < params.length; i += 7) {
                currentX = isRelative ? currentX + params[i + 5] : params[i + 5];
                currentY = isRelative ? currentY + params[i + 6] : params[i + 6];

                const transformed = transformPoint(currentX, currentY, matrix);
                transformedCommands.push(`L ${transformed.x} ${transformed.y}`);

                lastControlX = transformed.x;
                lastControlY = transformed.y;
            }
        } else if (type === 'Z') {
            currentX = startX;
            currentY = startY;
            transformedCommands.push('Z');
        } else {
            // For other commands, just return as-is
            transformedCommands.push(cmd);
        }
    }

    return transformedCommands.join(' ');
}

/**
 * Parse SVG file and extract polygon from specified sections
 * Uses the same approach as run-hollowsquare-samples.js
 */
function parseSvgCombination(svgPath, sectionIds) {
    const svgContent = fs.readFileSync(svgPath, 'utf8');

    // Load SvgViewerAlgorithms to parse path data from LazyMagic.BlazorSvg
    const SvgViewerAlgorithms = require('../../LazyMagic.BlazorSvg/wwwroot/SvgViewerAlgorithms.js');

    // Extract path data
    const pathData = extractPathData(svgContent, sectionIds);

    if (pathData.length !== sectionIds.length) {
        throw new Error(`Could not find all paths. Found ${pathData.length}/${sectionIds.length}`);
    }

    // Parse paths into line segments
    const lineSegmentPaths = [];
    for (let j = 0; j < pathData.length; j++) {
        const pathD = pathData[j].d;
        const segments = SvgViewerAlgorithms.parsePathToLineSegments(pathD, j);
        lineSegmentPaths.push({
            pathIdx: j,
            pathId: pathData[j].id,
            segments: segments
        });
    }

    // Build boundary polygon
    const allSegments = lineSegmentPaths.flatMap(p => p.segments);
    const mergedSegments = SvgViewerAlgorithms.mergeCoincidentPoints(allSegments, 0.5);
    const markedSegments = SvgViewerAlgorithms.markSharedSegments(mergedSegments, 0.5);
    const pathNetwork = SvgViewerAlgorithms.joinPathsIntoNetwork(markedSegments);
    const polygon = SvgViewerAlgorithms.traverseOuterEdge(pathNetwork);

    if (!polygon || polygon.length < 3) {
        throw new Error('Failed to find polygon boundary');
    }

    return polygon;
}

/**
 * Extract original SVG path data for visual reference
 */
function extractOriginalPaths(svgPath, sectionIds) {
    const svgContent = fs.readFileSync(svgPath, 'utf8');
    const paths = [];

    for (const sectionId of sectionIds) {
        // Try <path> element
        let regex = new RegExp(`<path[^>]*id="${sectionId}"[^>]*d="([^"]*)"`, 'i');
        let match = svgContent.match(regex);

        if (match) {
            paths.push({ id: sectionId, d: match[1], type: 'path' });
            continue;
        }

        // Try <polygon> element
        regex = new RegExp(`<polygon[^>]*id="${sectionId}"[^>]*points="([^"]*)"`, 'i');
        match = svgContent.match(regex);

        if (match) {
            const points = match[1].trim().split(/\s+/);
            const pathD = points.map((point, index) => {
                const [x, y] = point.split(',');
                return index === 0 ? `M ${x},${y}` : `L ${x},${y}`;
            }).join(' ') + ' Z';
            paths.push({ id: sectionId, d: pathD, type: 'polygon' });
        }
    }

    return paths;
}

/**
 * Calculate layout-specific data (tables, runs, etc.)
 */
function calculateLayoutData(layout, algorithmType) {
    const data = {
        dimensions: `${layout.width.toFixed(1)} × ${layout.height.toFixed(1)} ft`,
        area: layout.area.toFixed(2),
        angle: layout.angle.toFixed(1)
    };

    // Add algorithm-specific data
    if (algorithmType === 'hollowsquare') {
        // Calculate lengthRun and depthRun from dimensions
        const lengthRun = Math.round((layout.width - 19) / 6);
        const depthRun = Math.round((layout.height - 14) / 6);
        const tables = (lengthRun + 1) * 2 + (depthRun + 1) * 2;

        data.tables = `${tables} (${lengthRun + 1}L × ${depthRun + 1}D)`;
        data.type = 'Hollow Square Layout';
        data.color = '#9333ea'; // purple
        // Note which algorithm was used (boundary-based or unified)
        if (layout.type) {
            data.algorithmUsed = layout.type;
        }
    } else if (algorithmType === 'boardroom') {
        const sets = Math.round((Math.max(layout.width, layout.height) - 14) / 6) + 1;
        data.tables = `${sets * 2} (${sets} sets)`;
        data.type = 'Boardroom Layout';
        data.color = '#059669'; // green
        // Note which algorithm was used (boundary-based or unified)
        if (layout.type) {
            data.algorithmUsed = layout.type;
        }
    } else if (algorithmType === 'maxinscribed') {
        data.type = 'Max Inscribed Rectangle';
        data.color = '#dc2626'; // red
        // Note which algorithm was used (boundary or optimized)
        if (layout.type) {
            data.algorithmUsed = layout.type;
        }
    } else {
        data.type = 'Max Rectangle';
        data.color = '#dc2626'; // red
    }

    return data;
}

/**
 * Generate SVG output file matching the original aesthetic style
 */
function generateSvgOutput(polygon, layout, outputPath, testId, sectionIds, svgPath, algorithmType, polygonArea, runtimeMs, layoutRestriction = 'allowed') {
    // Calculate bounds for viewBox
    const bounds = unifiedAlgo.getPolygonBounds(polygon);
    const padding = 20;
    const tableWidth = 320;
    const tableHeight = 160; // Max height (when tables are shown)

    // Calculate total dimensions including the table positioned to the right
    const totalWidth = (bounds.maxX - bounds.minX) + (3 * padding) + tableWidth;
    const totalHeight = Math.max(bounds.maxY - bounds.minY + 2 * padding, tableHeight + padding);

    const viewBox = `${bounds.minX - padding} ${bounds.minY - padding} ${totalWidth} ${totalHeight}`;

    // Extract original paths for visual reference
    const originalPaths = extractOriginalPaths(svgPath, sectionIds);

    // Validate layout structure
    if (!layout.corners || !Array.isArray(layout.corners)) {
        throw new Error(`Invalid layout: corners is ${layout.corners}`);
    }
    if (!layout.centroid) {
        throw new Error(`Invalid layout: centroid is ${layout.centroid}`);
    }
    if (layout.corners.some(c => !c || c.x === undefined || c.y === undefined)) {
        const badCorner = layout.corners.findIndex(c => !c || c.x === undefined || c.y === undefined);
        throw new Error(`Invalid layout: corner[${badCorner}] is ${JSON.stringify(layout.corners[badCorner])}`);
    }
    if (layout.centroid.x === undefined || layout.centroid.y === undefined) {
        throw new Error(`Invalid layout: centroid is ${JSON.stringify(layout.centroid)}`);
    }

    // Format polygon points
    const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');

    // Format rectangle corners
    const rectPoints = layout.corners.map(c => `${c.x},${c.y}`).join(' ');

    // Calculate layout data
    const layoutData = calculateLayoutData(layout, algorithmType);
    const fillRatio = ((layout.area / polygonArea) * 100).toFixed(1);

    // Generate corner markers
    const cornerMarkers = layout.corners.map((corner, i) => `
  <circle cx="${corner.x}" cy="${corner.y}" r="3" fill="${layoutData.color}"/>
  <text x="${corner.x + 6}" y="${corner.y + 4}" font-size="8" fill="${layoutData.color}">c${i}</text>`).join('');

    // Generate vertex markers
    const vertexMarkers = polygon.map((point, i) => `
  <circle cx="${point.x}" cy="${point.y}" r="2" fill="#4080ff"/>
  <text x="${point.x - 8}" y="${point.y - 6}" font-size="8" fill="#4080ff">p${i}</text>`).join('');

    // Warning/Restriction banner (if applicable)
    let restrictionBanner = '';
    if (layoutRestriction === 'warning') {
        restrictionBanner = `
  <!-- Layout Warning Banner -->
  <rect x="${bounds.minX}" y="${bounds.minY + 30}" width="${totalWidth - tableWidth - padding}" height="30" fill="#FEF3C7" stroke="#F59E0B" stroke-width="2" rx="4"/>
  <text x="${bounds.minX + 10}" y="${bounds.minY + 50}" font-size="12" font-weight="bold" fill="#92400E">
    ⚠ Layout Warning: This area may have obstacles or constraints that affect table placement
  </text>
`;
    } else if (layoutRestriction === 'restricted') {
        restrictionBanner = `
  <!-- Layout Restriction Banner -->
  <rect x="${bounds.minX}" y="${bounds.minY + 30}" width="${totalWidth - tableWidth - padding}" height="30" fill="#FEE2E2" stroke="#DC2626" stroke-width="2" rx="4"/>
  <text x="${bounds.minX + 10}" y="${bounds.minY + 50}" font-size="12" font-weight="bold" fill="#991B1B">
    🚫 Restricted Area: This layout is for reference only - not recommended for table placement
  </text>
`;
    }

    const svgContent = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg"
     viewBox="${viewBox}"
     width="${totalWidth}" height="${totalHeight}"
     style="background: white;">

  <!-- Title -->
  <text x="${bounds.minX}" y="${bounds.minY + 20}" font-size="16" font-weight="bold" fill="#000">
    ${testId}
  </text>
${restrictionBanner}
  <!-- Original test paths (gray, semi-transparent) -->
  <g opacity="0.3">
${originalPaths.map(p => `    <path id="${p.id}" d="${p.d}" fill="none" stroke="#808080" stroke-width="1"/>`).join('\n')}
  </g>

  <!-- Boundary polygon (blue, dashed) -->
  <polygon points="${polygonPoints}" fill="none" stroke="#4080ff" stroke-width="2" stroke-dasharray="5,5"/>

  <!-- Layout rectangle (colored fill, solid border) -->
  <polygon points="${rectPoints}" fill="rgba(${parseInt(layoutData.color.slice(1, 3), 16)}, ${parseInt(layoutData.color.slice(3, 5), 16)}, ${parseInt(layoutData.color.slice(5, 7), 16)}, 0.15)" stroke="${layoutData.color}" stroke-width="3"/>

  <!-- Centroid marker -->
  <circle cx="${layout.centroid.x}" cy="${layout.centroid.y}" r="4" fill="${layoutData.color}"/>
  <text x="${layout.centroid.x + 8}" y="${layout.centroid.y + 5}" font-size="10" fill="${layoutData.color}">${algorithmType}</text>

  <!-- Corner markers -->
${cornerMarkers}

  <!-- Vertex markers -->
${vertexMarkers}

  <!-- Data Table (positioned to the right) -->
  <g transform="translate(${bounds.maxX + padding}, ${bounds.minY})">
    <rect x="0" y="0" width="320" height="${layout.usedBoundaryBased !== undefined ? (layoutData.tables ? 220 : 200) : (layoutData.tables ? 160 : 140)}" fill="white" stroke="#ccc" stroke-width="1"/>
    <rect x="0" y="0" width="320" height="30" fill="#f0f0f0"/>
    <text x="160" y="20" font-size="14" font-weight="bold" text-anchor="middle">${layoutData.type}</text>

${layoutData.tables ? `    <text x="10" y="50" font-size="11" font-weight="bold">Tables:</text>
    <text x="310" y="50" font-size="11" text-anchor="end">${layoutData.tables}</text>

` : ''}    <text x="10" y="${layoutData.tables ? 70 : 50}" font-size="11" font-weight="bold">Dimensions:</text>
    <text x="310" y="${layoutData.tables ? 70 : 50}" font-size="11" text-anchor="end">${layoutData.dimensions}</text>

    <text x="10" y="${layoutData.tables ? 90 : 70}" font-size="11" font-weight="bold">Area:</text>
    <text x="310" y="${layoutData.tables ? 90 : 70}" font-size="11" text-anchor="end">${layoutData.area} sq ft</text>

    <text x="10" y="${layoutData.tables ? 110 : 90}" font-size="11" font-weight="bold">Fill Ratio:</text>
    <text x="310" y="${layoutData.tables ? 110 : 90}" font-size="11" text-anchor="end">${fillRatio}%</text>

    <text x="10" y="${layoutData.tables ? 130 : 110}" font-size="11" font-weight="bold">Angle:</text>
    <text x="310" y="${layoutData.tables ? 130 : 110}" font-size="11" text-anchor="end">${layoutData.angle}°</text>

    <text x="10" y="${layoutData.tables ? 150 : 130}" font-size="11" font-weight="bold">Runtime:</text>
    <text x="310" y="${layoutData.tables ? 150 : 130}" font-size="11" text-anchor="end">${runtimeMs < 1000 ? runtimeMs + 'ms' : (runtimeMs / 1000).toFixed(2) + 's'}</text>
${layout.usedBoundaryBased !== undefined ? `
    <!-- Hybrid Algorithm Info -->
    <line x1="10" x2="310" y1="${layoutData.tables ? 165 : 145}" y2="${layoutData.tables ? 165 : 145}" stroke="#ccc" stroke-width="1"/>
    <text x="10" y="${layoutData.tables ? 180 : 160}" font-size="11" font-weight="bold">Algorithm Used:</text>
    <text x="310" y="${layoutData.tables ? 180 : 160}" font-size="11" text-anchor="end">${layout.selectedAlgorithm || layout.type || 'N/A'}</text>

    <text x="10" y="${layoutData.tables ? 200 : 180}" font-size="10" fill="#666">Boundary: ${layout.usedBoundaryBased ? '✓' : '✗'} | Optimized: ${layout.usedOptimized ? '✓' : '✗'}</text>
` : ''}  </g>

  <!-- Area Data (embedded as XML comments for extraction) -->
  <!-- polygonArea: ${polygonArea.toFixed(4)} -->
  <!-- layoutArea: ${layoutData.area} -->
  <!-- fillRatio: ${fillRatio} -->

</svg>`;

    fs.writeFileSync(outputPath, svgContent, 'utf8');
}

/**
 * Hybrid algorithm runner: Boundary-Based + Optimized with dimension constraints
 */
function runHybridWithConstraints(polygon, options, algorithmType) {
    const debugMode = options.debugMode || false;
    const startTime = performance.now();

    // Convert dimension constraints to algorithm parameters
    const constraintParams = convertConstraintsToParams(options, algorithmType);

    // Step 1: Run boundary-based algorithm (fast, edge-aligned)
    if (debugMode) {
        console.log('[hybrid] Step 1: Running boundary-based algorithm...');
    }

    const boundaryStart = performance.now();
    const boundaryOptions = {
        debugMode: debugMode,
        maxAngles: 6,
        angleTolerance: 5,
        testPerpendicular: true,
        ...constraintParams.boundary
    };
    let boundaryRaw = null;
    try {
        boundaryRaw = boundaryBased.boundaryBasedInscribedRectangle(polygon, boundaryOptions);
    } catch (err) {
        console.error(`[hybrid] ⚠️  Boundary-based algorithm error: ${err.message}`);
        if (debugMode) console.error(err.stack);
    }
    const boundaryTime = performance.now() - boundaryStart;

    let boundaryResult = null;
    if (boundaryRaw) {
        boundaryResult = boundaryRaw;
        boundaryResult.type = 'boundary-based';
        if (debugMode) {
            console.log(`[hybrid] Boundary: ${boundaryResult.width.toFixed(1)}×${boundaryResult.height.toFixed(1)} = ${boundaryResult.area.toFixed(1)} sq ft`);
        }
    }

    // Step 2: Run optimized algorithm (grid-based search)
    if (debugMode) {
        console.log('[hybrid] Step 2: Running optimized algorithm...');
    }

    const optimizedStart = performance.now();
    const optimizedOptions = {
        debugMode: debugMode,
        maxTime: options.maxTime || 5000,
        gridStep: options.gridStep || 8.0,
        polylabelPrecision: options.polylabelPrecision || 0.5,
        binarySearchPrecision: options.binarySearchPrecision || 0.0001,
        binarySearchMaxIterations: options.binarySearchMaxIterations || 20,
        ...constraintParams.optimized
    };
    let optimizedRaw = null;
    try {
        optimizedRaw = optimized.fastInscribedRectangle(polygon, optimizedOptions);
    } catch (err) {
        console.error(`[hybrid] ⚠️  Optimized algorithm error: ${err.message}`);
        if (debugMode) console.error(err.stack);
    }
    const optimizedTime = performance.now() - optimizedStart;

    let optimizedResult = null;
    if (optimizedRaw) {
        optimizedResult = optimizedRaw;
        optimizedResult.type = 'optimized';
        if (debugMode) {
            console.log(`[hybrid] Optimized: ${optimizedResult.width.toFixed(1)}×${optimizedResult.height.toFixed(1)} = ${optimizedResult.area.toFixed(1)} sq ft`);
        }
    }

    // Step 3: Compare and select best result
    const endTime = performance.now();
    let bestResult = null;

    if (!optimizedResult && !boundaryResult) {
        console.log('[hybrid] ✗ Both algorithms failed');
        return null;
    } else if (!optimizedResult) {
        console.log('[hybrid] ✓ Using boundary-based (optimized failed)');
        bestResult = boundaryResult;
    } else if (!boundaryResult) {
        console.log('[hybrid] ✓ Using optimized (boundary-based failed)');
        bestResult = optimizedResult;
    } else {
        // Both succeeded - compare areas
        if (debugMode) {
            console.log(`[hybrid] Comparing: boundary=${boundaryResult.area.toFixed(1)} vs optimized=${optimizedResult.area.toFixed(1)}`);
        }

        if (boundaryResult.area >= optimizedResult.area) {
            bestResult = boundaryResult;
            console.log(`[hybrid] ✓ Using boundary-based (${boundaryResult.area.toFixed(1)} ≥ ${optimizedResult.area.toFixed(1)})`);
        } else {
            bestResult = optimizedResult;
            console.log(`[hybrid] ✓ Using optimized (${optimizedResult.area.toFixed(1)} > ${boundaryResult.area.toFixed(1)})`);
        }
    }

    // Add hybrid metadata
    bestResult.hybridTotalTime = endTime - startTime;
    bestResult.usedBoundaryBased = boundaryResult !== null;
    bestResult.usedOptimized = optimizedResult !== null;
    bestResult.selectedAlgorithm = bestResult.type;
    bestResult.boundaryTime = boundaryTime;
    bestResult.optimizedTime = optimizedTime;

    if (debugMode) {
        console.log(`[hybrid] Total time: ${bestResult.hybridTotalTime.toFixed(1)}ms (boundary: ${boundaryTime.toFixed(1)}ms, optimized: ${optimizedTime.toFixed(1)}ms)`);
    }

    return bestResult;
}

/**
 * Convert dimension constraints to algorithm parameters
 */
function convertConstraintsToParams(options, algorithmType) {
    const result = {
        boundary: {},
        optimized: {}
    };

    if (algorithmType === 'maxinscribed') {
        // No constraints - use default aspect ratios
        result.optimized.aspectRatios = options.aspectRatios || [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0];
        return result;
    }

    if (algorithmType === 'boardroom') {
        // Fixed width, continuous height
        const widthConstraint = options.width || { mode: 'fixed', value: 13 };
        const heightConstraint = options.height || { mode: 'continuous', min: 6, max: 1000, samples: 25 };

        if (widthConstraint.mode === 'fixed' && heightConstraint.mode === 'continuous') {
            // Generate aspect ratios from width and height samples
            const width = widthConstraint.value;
            const heightSamples = heightConstraint.samples || 25;
            const aspectRatios = [];

            for (let i = 0; i < heightSamples; i++) {
                const t = i / (heightSamples - 1);
                const height = heightConstraint.min + t * (heightConstraint.max - heightConstraint.min);
                const aspect = width / height;
                aspectRatios.push(aspect);
            }

            result.optimized.aspectRatios = aspectRatios;
            result.optimized.fixedWidth = width;
        }

        return result;
    }

    if (algorithmType === 'hollowsquare') {
        // Discrete width and height
        const widthConstraint = options.width || { mode: 'discrete', base: 19, increment: 6, minIncrements: 0 };
        const heightConstraint = options.height || { mode: 'discrete', base: 14, increment: 6, minIncrements: 0 };

        if (widthConstraint.mode === 'discrete' && heightConstraint.mode === 'discrete') {
            // Generate aspect ratios from discrete width/height combinations
            const aspectRatios = [];
            const maxIncrements = 20; // Reasonable upper limit

            for (let wi = widthConstraint.minIncrements || 0; wi < maxIncrements; wi++) {
                const width = widthConstraint.base + wi * widthConstraint.increment;

                for (let hi = heightConstraint.minIncrements || 0; hi < maxIncrements; hi++) {
                    const height = heightConstraint.base + hi * heightConstraint.increment;
                    const aspect = width / height;

                    // Avoid duplicates and extremes
                    if (!aspectRatios.includes(aspect) && aspect > 0.3 && aspect < 5.0) {
                        aspectRatios.push(aspect);
                    }
                }
            }

            // Sort and limit to reasonable number
            aspectRatios.sort((a, b) => a - b);
            result.optimized.aspectRatios = aspectRatios.slice(0, 50);
        }

        return result;
    }

    // Default: no constraints
    result.optimized.aspectRatios = options.aspectRatios || [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0];
    return result;
}

/**
 * Check if a rectangle meets dimension constraints for the algorithm type
 */
function checkDimensionConstraints(rectangle, options, algorithmType) {
    if (algorithmType === 'maxinscribed') {
        // Max inscribed has no constraints - any rectangle is valid
        return true;
    } else if (algorithmType === 'boardroom') {
        // Boardroom: width must be exactly 13 ft
        const widthConstraint = options.width || { mode: 'fixed', value: 13 };
        return checkDimensionValue(rectangle.width, widthConstraint);
    } else if (algorithmType === 'hollowsquare') {
        // Hollow square: width and height must meet discrete constraints
        const widthConstraint = options.width || { mode: 'discrete', base: 19, increment: 6, minIncrements: 0 };
        const heightConstraint = options.height || { mode: 'discrete', base: 14, increment: 6, minIncrements: 0 };
        return checkDimensionValue(rectangle.width, widthConstraint) &&
               checkDimensionValue(rectangle.height, heightConstraint);
    }

    return true;
}

/**
 * Check if a dimension value meets a specific constraint
 */
function checkDimensionValue(value, constraint) {
    if (constraint.mode === 'fixed') {
        // Must exactly match (with tolerance)
        return Math.abs(value - constraint.value) < 0.5;
    } else if (constraint.mode === 'discrete') {
        // Must be base + n*increment for some integer n >= minIncrements
        const minIncrements = constraint.minIncrements || 0;
        const minValue = constraint.base + (minIncrements * constraint.increment);

        if (value < minValue - 0.5) return false;

        const n = Math.round((value - constraint.base) / constraint.increment);
        const expectedValue = constraint.base + (n * constraint.increment);
        return Math.abs(value - expectedValue) < 0.5;
    } else if (constraint.mode === 'continuous') {
        // Must be within range
        return value >= constraint.min && value <= constraint.max;
    }

    return true;
}

/**
 * Run a single test combination
 */
function runTest(testConfig, combination, testIndex, totalTests) {
    const startTime = Date.now();

    console.log(`\n[${testIndex + 1}/${totalTests}] Testing combination: ${combination.id}`);
    console.log(`  Sections: ${combination.sections.join(', ')}`);
    if (combination.overrides) {
        console.log(`  Overrides: ${JSON.stringify(combination.overrides)}`);
    }

    try {
        // Parse SVG and extract polygon
        const polygon = parseSvgCombination(testConfig.svgPath, combination.sections);
        console.log(`  Polygon vertices: ${polygon.length}`);

        // Calculate polygon area from geometry
        let calculatedArea = 0;
        for (let i = 0; i < polygon.length; i++) {
            const p1 = polygon[i];
            const p2 = polygon[(i + 1) % polygon.length];
            calculatedArea += p1.x * p2.y - p2.x * p1.y;
        }
        calculatedArea = Math.abs(calculatedArea / 2);

        // Check for override value from Rooms.json
        const polygonArea = combination.overrides?.area !== undefined
            ? combination.overrides.area
            : calculatedArea;

        if (combination.overrides?.area !== undefined) {
            console.log(`  Area override: Using ${polygonArea} sq ft (calculated: ${calculatedArea.toFixed(2)} sq ft)`);
        }

        // Run the algorithm based on type
        console.log(`  Running algorithm...`);
        let layout;

        if (testConfig.algorithm === 'maxinscribed') {
            // Max inscribed: Use hybrid approach (Boundary-Based + Optimized, no constraints)
            layout = runHybridWithConstraints(polygon, testConfig.algorithmOptions, testConfig.algorithm);
        } else if (testConfig.algorithm === 'boardroom') {
            // Boardroom: Use dual BB/Optimized approach with incremental expansion
            const options = {
                boardroomWidth: testConfig.algorithmOptions.width?.value || 13,
                minLength: testConfig.algorithmOptions.height?.min || 14,
                lengthIncrement: 6,
                angleSamples: testConfig.algorithmOptions.angleSamples || 25,
                centroidSamples: testConfig.algorithmOptions.centroidSamples || 25,
                debugMode: testConfig.algorithmOptions.debugMode || false
            };
            layout = boardroom.findBoardroomLayout(polygon, options);
        } else {
            // Hollow Square: Use Unified algorithm (handles dimension constraints)
            layout = unifiedAlgo.findInscribedRectangle(polygon, testConfig.algorithmOptions);
        }

        if (!layout) {
            console.log(`  ❌ FAILED: No valid layout found`);
            return {
                id: combination.id,
                sections: combination.sections,
                passed: false,
                error: 'No valid layout found',
                runtimeMs: Date.now() - startTime
            };
        }

        // Calculate fill ratio
        const fillRatio = (layout.area / polygonArea) * 100;

        console.log(`  ✓ Rectangle: ${layout.width.toFixed(1)} × ${layout.height.toFixed(1)} ft`);
        console.log(`  ✓ Area: ${layout.area.toFixed(1)} sq ft (${fillRatio.toFixed(1)}% fill)`);
        console.log(`  ✓ Angle: ${layout.angle.toFixed(1)}°`);

        // Generate algorithm-specific file names
        let algorithmPrefix;
        if (testConfig.algorithm === 'maxinscribed') {
            algorithmPrefix = 'MaxInscribed';
        } else {
            algorithmPrefix = testConfig.algorithm.charAt(0).toUpperCase() +
                             testConfig.algorithm.slice(1).replace(/square/i, 'Square').replace(/room/i, 'room');
        }
        const testName = `${algorithmPrefix}_${combination.id}`;

        const runtime = Date.now() - startTime;

        // Generate output files
        if (testConfig.outputOptions.generateSVG) {
            const svgOutputPath = path.join(
                testConfig.outputDir,
                `${testName}.svg`
            );
            generateSvgOutput(
                polygon,
                layout,
                svgOutputPath,
                testName,
                combination.sections,
                testConfig.svgPath,
                testConfig.algorithm,
                polygonArea,
                runtime,
                combination.layoutRestriction || 'allowed'
            );
        }

        if (testConfig.outputOptions.generateJSON) {
            const jsonOutputPath = path.join(
                testConfig.outputDir,
                `${testName}.json`
            );
            fs.writeFileSync(jsonOutputPath, JSON.stringify({
                testId: testName,
                sections: combination.sections,
                layout: layout,
                polygonArea: polygonArea,
                fillRatio: fillRatio,
                runtimeMs: runtime
            }, null, 2), 'utf8');
        }

        console.log(`  Runtime: ${runtime}ms (${(runtime / 1000).toFixed(1)}s)`);

        return {
            id: combination.id,
            sections: combination.sections,
            layoutRestriction: combination.layoutRestriction || 'allowed',
            passed: true,
            layout: {
                width: layout.width,
                height: layout.height,
                area: layout.area,
                angle: layout.angle
            },
            polygonArea: polygonArea,
            fillRatio: fillRatio,
            runtimeMs: runtime
        };

    } catch (error) {
        console.log(`  ❌ FAILED: ${error.message}`);
        return {
            id: combination.id,
            sections: combination.sections,
            layoutRestriction: combination.layoutRestriction || 'allowed',
            passed: false,
            error: error.message,
            runtimeMs: Date.now() - startTime
        };
    }
}

/**
 * Main test runner
 */
function main() {
    const args = process.argv.slice(2);

    if (args.length === 0) {
        console.error('Usage: node run-tests.js <config-file>');
        console.error('Example: node run-tests.js test-configs/hollowsquare-sample.json');
        process.exit(1);
    }

    const configPath = args[0];

    if (!fs.existsSync(configPath)) {
        console.error(`Error: Config file not found: ${configPath}`);
        process.exit(1);
    }

    console.log('='.repeat(80));
    console.log(`Loading test configuration: ${configPath}`);
    console.log('='.repeat(80));

    // Load test configuration
    const testConfig = JSON.parse(fs.readFileSync(configPath, 'utf8'));

    // Load combinations from external file if specified
    if (testConfig.combinationsFile) {
        const combinationsPath = path.resolve(path.dirname(configPath), testConfig.combinationsFile);
        console.log(`\nLoading combinations from: ${combinationsPath}`);

        const combinationsData = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));

        // Convert combinations to testCombinations format
        // Use combo.id from file (includes room prefix), or comboNumbers if provided
        // Note: We do NOT pass overrides to the algorithms - they are metadata only for the UI
        if (testConfig.comboNumbers && testConfig.comboNumbers.length === combinationsData.combinations.length) {
            testConfig.testCombinations = combinationsData.combinations.map((combo, index) => ({
                id: String(testConfig.comboNumbers[index]).padStart(4, '0'),
                sections: combo.sections
            }));
        } else {
            // Use the id from the combinations file (e.g., "Ballroom_0001")
            testConfig.testCombinations = combinationsData.combinations.map(combo => ({
                id: combo.id,
                sections: combo.sections
            }));
        }

        console.log(`Loaded ${testConfig.testCombinations.length} combinations`);
    }

    console.log(`\nTest Name: ${testConfig.testName}`);
    console.log(`Algorithm: ${testConfig.algorithm}`);
    console.log(`SVG Source: ${testConfig.svgPath}`);
    console.log(`Output Directory: ${testConfig.outputDir}`);
    console.log(`Total Tests: ${testConfig.testCombinations.length}`);

    // Create output directory
    if (!fs.existsSync(testConfig.outputDir)) {
        fs.mkdirSync(testConfig.outputDir, { recursive: true });
    }

    // Delete existing results if requested
    if (testConfig.outputOptions.deleteExistingResults) {
        let algorithmPrefix;
        if (testConfig.algorithm === 'maxinscribed') {
            algorithmPrefix = 'MaxInscribed';
        } else {
            algorithmPrefix = testConfig.algorithm.charAt(0).toUpperCase() +
                             testConfig.algorithm.slice(1).replace(/square/i, 'Square').replace(/room/i, 'room');
        }
        const files = fs.readdirSync(testConfig.outputDir);
        for (const file of files) {
            if (file.startsWith(`${algorithmPrefix}_`) || file.includes('-summary.json')) {
                fs.unlinkSync(path.join(testConfig.outputDir, file));
            }
        }
        console.log(`Cleaned existing ${algorithmPrefix} results`);
    }

    // Run all tests
    const startTime = Date.now();
    const results = [];

    for (let i = 0; i < testConfig.testCombinations.length; i++) {
        const result = runTest(testConfig, testConfig.testCombinations[i], i, testConfig.testCombinations.length);
        results.push(result);
    }

    const totalRuntime = Date.now() - startTime;

    // Generate summary
    const passed = results.filter(r => r.passed).length;
    const failed = results.filter(r => !r.passed).length;

    console.log('\n' + '='.repeat(80));
    console.log('TEST SUMMARY');
    console.log('='.repeat(80));
    console.log(`Total Tests: ${results.length}`);
    console.log(`Passed: ${passed}`);
    console.log(`Failed: ${failed}`);
    console.log(`Total Runtime: ${totalRuntime}ms (${(totalRuntime / 1000).toFixed(1)}s)`);
    console.log(`Average Runtime: ${(totalRuntime / results.length).toFixed(0)}ms per test`);

    if (testConfig.outputOptions.generateSummary) {
        let algorithmPrefix;
        if (testConfig.algorithm === 'maxinscribed') {
            algorithmPrefix = 'MaxInscribed';
        } else {
            algorithmPrefix = testConfig.algorithm.charAt(0).toUpperCase() +
                             testConfig.algorithm.slice(1).replace(/square/i, 'Square').replace(/room/i, 'room');
        }

        const summary = {
            timestamp: new Date().toISOString(),
            testName: testConfig.testName,
            algorithm: testConfig.algorithm,
            totalTests: results.length,
            passed: passed,
            failed: failed,
            totalRuntimeMs: totalRuntime,
            totalRuntimeSec: parseFloat((totalRuntime / 1000).toFixed(1)),
            configuration: testConfig.algorithmOptions,
            results: results
        };

        const summaryPath = path.join(
            testConfig.outputDir,
            `${algorithmPrefix}-summary.json`
        );
        fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2), 'utf8');
        console.log(`\nSummary saved to: ${summaryPath}`);
    }

    console.log('='.repeat(80));
    process.exit(failed > 0 ? 1 : 0);
}

// Run if executed directly
main();

export { parseSvgCombination, runTest };
