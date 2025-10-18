// Extract pre-computed rectangle data from TestResults SVG files
// and generate precomputed-rectangles.json

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { DOMParser } from 'xmldom';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const testResultsDir = path.join(__dirname, '../TestResults');
const combinationsPath = path.join(__dirname, 'valid-combinations.json');
const outputPath = path.join(__dirname, 'precomputed-rectangles.json');

/**
 * Extract algorithm result from SVG comparison table
 * @param {Document} doc - Parsed SVG document
 * @param {string} algoType - 'BB' or 'Opt'
 * @returns {Object|null} Algorithm result with area and time
 */
function extractAlgorithmResult(doc, algoType) {
    const comments = doc.getElementsByTagName('*');
    let foundComment = false;

    // Find the comment marking the algorithm row
    const commentText = algoType === 'BB' ? '<!-- Boundary-Based Row -->' : '<!-- Optimized Row -->';

    for (let i = 0; i < comments.length; i++) {
        if (comments[i].nodeType === 8 && comments[i].textContent.trim() === commentText.trim().replace('<!--', '').replace('-->', '').trim()) {
            foundComment = true;
            break;
        }
    }

    // Extract from text elements in the table
    const textElements = doc.getElementsByTagName('text');
    let area = null;
    let time = null;

    for (let i = 0; i < textElements.length; i++) {
        const textNode = textElements[i];
        const textContent = textNode.textContent || '';
        const fill = textNode.getAttribute('fill');

        // Look for BB or Opt label with matching color
        const isBBLabel = textContent.trim() === 'BB' && fill && fill.includes('#ff4040');
        const isOptLabel = textContent.trim() === 'Opt' && fill && fill.includes('#00aa00');

        if ((algoType === 'BB' && isBBLabel) || (algoType === 'Opt' && isOptLabel)) {
            // Found the label, now extract area and time from subsequent text elements
            // Area is typically 3-4 elements after the label
            // Time is typically 5-6 elements after the label
            for (let j = i + 1; j < Math.min(i + 8, textElements.length); j++) {
                const content = textElements[j].textContent || '';

                // Match area (number with optional decimal)
                if (!area && /^\d+\.?\d*$/.test(content.trim())) {
                    area = parseFloat(content.trim());
                }
                // Match time (number followed by " ms")
                else if (!time && /^\d+\.?\d* ms$/.test(content.trim())) {
                    time = parseFloat(content.trim().replace(' ms', ''));
                }

                if (area && time) break;
            }
            break;
        }
    }

    if (!area || !time) {
        return null;
    }

    return { area, time };
}

/**
 * Parse SVG file to extract rectangle data for BEST algorithm
 * @param {string} svgPath - Path to SVG file
 * @returns {Object|null} Best rectangle data
 */
function parseSvgRectangle(svgPath) {
    try {
        const svgContent = fs.readFileSync(svgPath, 'utf8');
        const parser = new DOMParser();
        const doc = parser.parseFromString(svgContent, 'text/xml');

        // Extract area data from comments
        let polygonArea = null;
        let rectangleArea = null;
        const polygonAreaMatch = svgContent.match(/<!--\s*polygonArea:\s*([0-9.]+)\s*-->/);
        const rectangleAreaMatch = svgContent.match(/<!--\s*rectangleArea:\s*([0-9.]+)\s*-->/);
        if (polygonAreaMatch) polygonArea = parseFloat(polygonAreaMatch[1]);
        if (rectangleAreaMatch) rectangleArea = parseFloat(rectangleAreaMatch[1]);

        // Extract results for both algorithms
        const bbResult = extractAlgorithmResult(doc, 'BB');
        const optResult = extractAlgorithmResult(doc, 'Opt');

        if (!bbResult && !optResult) {
            console.warn(`Could not extract algorithm results from ${svgPath}`);
            return null;
        }

        // Determine which algorithm won (highest area)
        const winner = (!optResult || (bbResult && bbResult.area > optResult.area)) ? 'BB' : 'Opt';
        const winningResult = winner === 'BB' ? bbResult : optResult;
        const type = winner === 'BB' ? 'boundary-based' : 'optimized';

        // Extract polygon corners and centroid for winning algorithm
        const polygons = doc.getElementsByTagName('polygon');
        let corners = null;

        for (let i = 0; i < polygons.length; i++) {
            const polygon = polygons[i];
            const fill = polygon.getAttribute('fill');
            const stroke = polygon.getAttribute('stroke');

            // Look for the winning rectangle
            // BB: red fill (255, 100, 100) and #ff4040 stroke
            // Opt: green fill (100, 255, 100) and #40ff40 stroke
            const isBB = fill && fill.includes('255, 100, 100') && stroke && stroke.includes('#ff4040');
            const isOpt = fill && fill.includes('100, 255, 100') && stroke && stroke.includes('#40ff40');

            if ((winner === 'BB' && isBB) || (winner === 'Opt' && isOpt)) {
                const pointsStr = polygon.getAttribute('points');
                if (pointsStr) {
                    const pointPairs = pointsStr.trim().split(/\s+/);
                    corners = pointPairs.map(pair => {
                        const [x, y] = pair.split(',').map(Number);
                        return { x, y };
                    });
                }
                break;
            }
        }

        // Extract centroid for winning algorithm
        const circles = doc.getElementsByTagName('circle');
        let centroid = null;

        for (let i = 0; i < circles.length; i++) {
            const circle = circles[i];
            const fill = circle.getAttribute('fill');
            const r = circle.getAttribute('r');

            // Look for centroid marker (r=4)
            // BB: #ff0000 (red)
            // Opt: #00aa00 (green)
            if (r === '4') {
                const isBBCentroid = fill === '#ff0000';
                const isOptCentroid = fill === '#00aa00';

                if ((winner === 'BB' && isBBCentroid) || (winner === 'Opt' && isOptCentroid)) {
                    centroid = {
                        x: parseFloat(circle.getAttribute('cx')),
                        y: parseFloat(circle.getAttribute('cy'))
                    };
                    break;
                }
            }
        }

        if (!corners || !winningResult.area) {
            return null;
        }

        // Calculate width and height from corners (assumes rectangle)
        let width = 0;
        let height = 0;
        if (corners.length >= 2) {
            const dx1 = corners[1].x - corners[0].x;
            const dy1 = corners[1].y - corners[0].y;
            const dx2 = corners[2].x - corners[1].x;
            const dy2 = corners[2].y - corners[1].y;
            width = Math.sqrt(dx1 * dx1 + dy1 * dy1);
            height = Math.sqrt(dx2 * dx2 + dy2 * dy2);
        }

        // Calculate angle from first edge
        let angle = 0;
        if (corners.length >= 2) {
            const dx = corners[1].x - corners[0].x;
            const dy = corners[1].y - corners[0].y;
            angle = Math.atan2(dy, dx) * 180 / Math.PI;
            // Normalize to 0-360
            if (angle < 0) angle += 360;
        }

        return {
            corners,
            width: Math.round(width * 10) / 10,
            height: Math.round(height * 10) / 10,
            area: winningResult.area,
            angle: Math.round(angle * 10) / 10,
            centroid,
            type,
            computationTimeMs: winningResult.time,
            polygonArea,           // NEW: Polygon area in square SVG inches
            rectangleArea          // NEW: Rectangle area in square SVG inches
        };

    } catch (error) {
        console.error(`Error parsing ${svgPath}:`, error.message);
        return null;
    }
}

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('EXTRACTING PRE-COMPUTED RECTANGLES');
    console.log('='.repeat(80));
    console.log();

    // Step 1: Load combinations
    console.log('Step 1: Loading valid combinations...');
    const combinationsData = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));
    const combinations = combinationsData.combinations;
    console.log(`  Loaded ${combinations.length} combinations`);
    console.log();

    // Step 2: Parse SVG files
    console.log('Step 2: Parsing SVG files from TestResults...');
    const rectangles = [];
    let successCount = 0;
    let failCount = 0;

    for (let i = 0; i < combinations.length; i++) {
        const combo = combinations[i];
        const testName = `Combo_${String(i + 1).padStart(4, '0')}`;
        const svgPath = path.join(testResultsDir, `${testName}.svg`);

        if (!fs.existsSync(svgPath)) {
            console.warn(`  ⚠ SVG not found: ${testName}.svg`);
            failCount++;
            continue;
        }

        const rectData = parseSvgRectangle(svgPath);

        if (rectData) {
            rectangles.push({
                testName,
                key: combo.key,
                sections: combo.sections,
                rectangle: rectData
            });
            successCount++;
        } else {
            console.warn(`  ⚠ Failed to parse: ${testName}.svg`);
            failCount++;
        }

        // Progress indicator
        if ((i + 1) % 50 === 0) {
            console.log(`  Processed ${i + 1}/${combinations.length} SVG files...`);
        }
    }

    console.log(`  ✓ Successfully parsed ${successCount} SVG files`);
    if (failCount > 0) {
        console.log(`  ⚠ Failed to parse ${failCount} SVG files`);
    }
    console.log();

    // Step 3: Calculate statistics
    console.log('Step 3: Calculating statistics...');

    const totalTime = rectangles.reduce((sum, r) => sum + (r.rectangle.computationTimeMs || 0), 0);
    const avgTime = totalTime / rectangles.length;

    const boundaryCount = rectangles.filter(r => r.rectangle.type === 'boundary-based').length;
    const optimizedCount = rectangles.filter(r => r.rectangle.type === 'optimized').length;

    console.log(`  Total rectangles: ${rectangles.length}`);
    console.log(`  Boundary-based: ${boundaryCount}`);
    console.log(`  Optimized: ${optimizedCount}`);
    console.log(`  Average computation time: ${avgTime.toFixed(1)} ms`);
    console.log(`  Total computation time: ${(totalTime / 1000).toFixed(1)} seconds`);
    console.log();

    // Step 4: Save results
    console.log('Step 4: Saving results...');
    const output = {
        generatedAt: new Date().toISOString(),
        totalCombinations: combinations.length,
        successfulComputations: rectangles.length,
        failedComputations: failCount,
        totalComputationTimeMs: totalTime,
        averageComputationTimeMs: avgTime,
        statistics: {
            boundaryBasedCount: boundaryCount,
            optimizedCount: optimizedCount
        },
        rectangles: rectangles.map(r => ({
            key: r.key,
            sections: r.sections,
            rectangle: {
                corners: r.rectangle.corners,
                width: r.rectangle.width,
                height: r.rectangle.height,
                area: r.rectangle.area,
                angle: r.rectangle.angle,
                centroid: r.rectangle.centroid,
                type: r.rectangle.type
            },
            polygonArea: r.rectangle.polygonArea,           // NEW: Polygon area in square SVG inches
            rectangleArea: r.rectangle.rectangleArea,       // NEW: Rectangle area in square SVG inches
            computationTimeMs: r.rectangle.computationTimeMs
        }))
    };

    fs.writeFileSync(outputPath, JSON.stringify(output, null, 2));
    console.log(`  ✓ Results saved to: ${outputPath}`);
    console.log(`  File size: ${(fs.statSync(outputPath).size / 1024).toFixed(1)} KB`);
    console.log();

    console.log('='.repeat(80));
    console.log('EXTRACTION COMPLETE!');
    console.log(`Successfully extracted ${rectangles.length}/${combinations.length} pre-computed rectangles`);
    console.log('='.repeat(80));
}

main().catch(console.error);
