#!/usr/bin/env node

/**
 * calculate-polygon-areas.js
 *
 * Calculates polygon areas from SVG path geometries and adds them to the level data JSON.
 * Uses the shoelace formula to calculate area from path coordinates.
 *
 * Usage: node calculate-polygon-areas.js <svgPath> <dataJsonPath> <outputJsonPath>
 *
 * Example:
 *   node calculate-polygon-areas.js "./input/Level1.svg" "./input/Level1-data.json" "./input/Level1-data-with-areas.json"
 */

import fs from 'fs';
import { JSDOM } from 'jsdom';

/**
 * Parse SVG path data and extract polygon coordinates
 * Handles both absolute and relative commands
 * Approximates curves by using their endpoints
 */
function parseSvgPath(pathData) {
    const polygon = [];

    // Parser for path commands
    // Handles M/m, L/l, H/h, V/v, C/c, S/s, Q/q, T/t, A/a, Z/z
    const commands = pathData.match(/[MLHVCSQTAZmlhvcsqtaz][^MLHVCSQTAZmlhvcsqtaz]*/g) || [];
    let currentX = 0;
    let currentY = 0;
    let lastControlX = currentX;
    let lastControlY = currentY;

    for (const cmd of commands) {
        const type = cmd[0];
        const args = cmd.slice(1).trim().split(/[\s,]+/).filter(x => x).map(parseFloat);
        const isRelative = type === type.toLowerCase() && type !== 'z';

        switch (type.toUpperCase()) {
            case 'M': // Move to
                // First pair is moveto, subsequent pairs are implicit lineto
                if (isRelative) {
                    currentX += args[0];
                    currentY += args[1];
                } else {
                    currentX = args[0];
                    currentY = args[1];
                }
                polygon.push({ x: currentX, y: currentY });
                lastControlX = currentX;
                lastControlY = currentY;

                // Handle additional coordinate pairs as implicit lineto
                for (let i = 2; i < args.length; i += 2) {
                    if (isRelative) {
                        currentX += args[i];
                        currentY += args[i + 1];
                    } else {
                        currentX = args[i];
                        currentY = args[i + 1];
                    }
                    polygon.push({ x: currentX, y: currentY });
                    lastControlX = currentX;
                    lastControlY = currentY;
                }
                break;

            case 'L': // Line to
                // Can have multiple coordinate pairs
                for (let i = 0; i < args.length; i += 2) {
                    if (isRelative) {
                        currentX += args[i];
                        currentY += args[i + 1];
                    } else {
                        currentX = args[i];
                        currentY = args[i + 1];
                    }
                    polygon.push({ x: currentX, y: currentY });
                    lastControlX = currentX;
                    lastControlY = currentY;
                }
                break;

            case 'H': // Horizontal line
                if (isRelative) {
                    currentX += args[0];
                } else {
                    currentX = args[0];
                }
                polygon.push({ x: currentX, y: currentY });
                lastControlX = currentX;
                lastControlY = currentY;
                break;

            case 'V': // Vertical line
                if (isRelative) {
                    currentY += args[0];
                } else {
                    currentY = args[0];
                }
                polygon.push({ x: currentX, y: currentY });
                lastControlX = currentX;
                lastControlY = currentY;
                break;

            case 'C': // Cubic Bezier curve
                // C has 6 args: x1,y1 x2,y2 x,y
                // For area calculation, use the endpoint (x,y)
                if (args.length >= 6) {
                    for (let i = 0; i < args.length; i += 6) {
                        if (isRelative) {
                            lastControlX = currentX + args[i + 2];
                            lastControlY = currentY + args[i + 3];
                            currentX += args[i + 4];
                            currentY += args[i + 5];
                        } else {
                            lastControlX = args[i + 2];
                            lastControlY = args[i + 3];
                            currentX = args[i + 4];
                            currentY = args[i + 5];
                        }
                        polygon.push({ x: currentX, y: currentY });
                    }
                }
                break;

            case 'S': // Smooth cubic Bezier
                // S has 4 args: x2,y2 x,y
                if (args.length >= 4) {
                    for (let i = 0; i < args.length; i += 4) {
                        if (isRelative) {
                            lastControlX = currentX + args[i];
                            lastControlY = currentY + args[i + 1];
                            currentX += args[i + 2];
                            currentY += args[i + 3];
                        } else {
                            lastControlX = args[i];
                            lastControlY = args[i + 1];
                            currentX = args[i + 2];
                            currentY = args[i + 3];
                        }
                        polygon.push({ x: currentX, y: currentY });
                    }
                }
                break;

            case 'Q': // Quadratic Bezier
                // Q has 4 args: x1,y1 x,y
                if (args.length >= 4) {
                    for (let i = 0; i < args.length; i += 4) {
                        if (isRelative) {
                            lastControlX = currentX + args[i];
                            lastControlY = currentY + args[i + 1];
                            currentX += args[i + 2];
                            currentY += args[i + 3];
                        } else {
                            lastControlX = args[i];
                            lastControlY = args[i + 1];
                            currentX = args[i + 2];
                            currentY = args[i + 3];
                        }
                        polygon.push({ x: currentX, y: currentY });
                    }
                }
                break;

            case 'T': // Smooth quadratic Bezier
                // T has 2 args: x,y
                if (args.length >= 2) {
                    for (let i = 0; i < args.length; i += 2) {
                        if (isRelative) {
                            currentX += args[i];
                            currentY += args[i + 1];
                        } else {
                            currentX = args[i];
                            currentY = args[i + 1];
                        }
                        polygon.push({ x: currentX, y: currentY });
                        lastControlX = currentX;
                        lastControlY = currentY;
                    }
                }
                break;

            case 'A': // Arc
                // A has 7 args: rx,ry x-axis-rotation large-arc-flag sweep-flag x,y
                // For area calculation, use the endpoint (x,y)
                if (args.length >= 7) {
                    for (let i = 0; i < args.length; i += 7) {
                        if (isRelative) {
                            currentX += args[i + 5];
                            currentY += args[i + 6];
                        } else {
                            currentX = args[i + 5];
                            currentY = args[i + 6];
                        }
                        polygon.push({ x: currentX, y: currentY });
                        lastControlX = currentX;
                        lastControlY = currentY;
                    }
                }
                break;

            case 'Z': // Close path
                // No action needed - polygon is closed
                break;

            default:
                console.warn(`  Warning: Unknown path command '${type}'`);
                break;
        }
    }

    return polygon;
}

/**
 * Calculate polygon area using the shoelace formula
 */
function calculatePolygonArea(polygon) {
    if (polygon.length < 3) return 0;

    let area = 0;
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];
        area += p1.x * p2.y - p2.x * p1.y;
    }

    return Math.abs(area / 2);
}

/**
 * Main function to calculate and add polygon areas
 */
function calculatePolygonAreas(svgPath, dataJsonPath, outputJsonPath) {
    console.log(`\n[calculate-polygon-areas] Processing...`);
    console.log(`  SVG: ${svgPath}`);
    console.log(`  Input JSON: ${dataJsonPath}`);
    console.log(`  Output JSON: ${outputJsonPath}`);

    // Load SVG
    console.log('  Loading SVG...');
    const svgContent = fs.readFileSync(svgPath, 'utf8');
    const dom = new JSDOM(svgContent, { contentType: 'image/svg+xml' });
    const document = dom.window.document;

    // Load data JSON
    console.log('  Loading data JSON...');
    let dataContent = fs.readFileSync(dataJsonPath, 'utf8');
    // Remove BOM if present
    if (dataContent.charCodeAt(0) === 0xFEFF) {
        dataContent = dataContent.slice(1);
    }
    const levelData = JSON.parse(dataContent);

    if (!levelData || !levelData.Rooms) {
        throw new Error('Invalid data structure: missing "Rooms" array');
    }

    // Calculate polygon areas for each section
    let sectionsProcessed = 0;
    let sectionsNotFound = 0;
    let sectionsCalculated = 0;

    for (const room of levelData.Rooms) {
        if (!room.RoomSections) continue;

        for (const section of room.RoomSections) {
            sectionsProcessed++;

            // Find corresponding path in SVG
            const pathElement = document.querySelector(`path[id="${section.Id}"]`);

            if (!pathElement) {
                console.warn(`  ⚠ Path not found for section: ${section.Id}`);
                sectionsNotFound++;
                continue;
            }

            // Get path data
            const pathData = pathElement.getAttribute('d');
            if (!pathData) {
                console.warn(`  ⚠ No path data for section: ${section.Id}`);
                continue;
            }

            // Parse path and calculate area
            try {
                const polygon = parseSvgPath(pathData);
                const area = calculatePolygonArea(polygon);

                // Add to section data
                section.PolygonArea = parseFloat(area.toFixed(2));
                sectionsCalculated++;

                console.log(`  ✓ ${section.Id}: ${area.toFixed(2)} sq units`);
            } catch (err) {
                console.warn(`  ⚠ Error calculating area for ${section.Id}:`, err.message);
            }
        }
    }

    console.log(`\n  Summary:`);
    console.log(`    Total sections: ${sectionsProcessed}`);
    console.log(`    Areas calculated: ${sectionsCalculated}`);
    console.log(`    Paths not found: ${sectionsNotFound}`);

    // Write output JSON
    console.log(`\n  Writing output JSON...`);
    fs.writeFileSync(outputJsonPath, JSON.stringify(levelData, null, 2), 'utf8');
    console.log(`  ✓ Output written: ${outputJsonPath}\n`);
}

// Command-line execution
if (process.argv.length < 5) {
    console.error('Usage: node calculate-polygon-areas.js <svgPath> <dataJsonPath> <outputJsonPath>');
    console.error('');
    console.error('Example:');
    console.error('  node calculate-polygon-areas.js "./input/Level1.svg" "./input/Level1-data.json" "./input/Level1-data-with-areas.json"');
    process.exit(1);
}

const [,, svgPath, dataJsonPath, outputJsonPath] = process.argv;

try {
    calculatePolygonAreas(svgPath, dataJsonPath, outputJsonPath);
} catch (error) {
    console.error('\n❌ Error calculating polygon areas:');
    console.error(error.message);
    console.error(error.stack);
    process.exit(1);
}
