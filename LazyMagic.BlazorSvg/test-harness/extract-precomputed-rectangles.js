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
 * Parse SVG file to extract rectangle data
 */
function parseSvgRectangle(svgPath) {
    try {
        const svgContent = fs.readFileSync(svgPath, 'utf8');
        const parser = new DOMParser();
        const doc = parser.parseFromString(svgContent, 'text/xml');

        // Extract legend text elements
        const textElements = doc.getElementsByTagName('text');
        let type = null;
        let size = null;
        let area = null;
        let angle = null;
        let time = null;

        for (let i = 0; i < textElements.length; i++) {
            const textNode = textElements[i];
            const textContent = textNode.textContent || '';

            if (textContent.startsWith('Type:')) {
                type = textContent.replace('Type:', '').trim().toLowerCase();
            } else if (textContent.startsWith('Size:')) {
                const match = textContent.match(/Size: ([\d.]+) × ([\d.]+) px/);
                if (match) {
                    size = {
                        width: parseFloat(match[1]),
                        height: parseFloat(match[2])
                    };
                }
            } else if (textContent.startsWith('Area:')) {
                const match = textContent.match(/Area: ([\d.]+) sq px/);
                if (match) {
                    area = parseFloat(match[1]);
                }
            } else if (textContent.startsWith('Angle:')) {
                const match = textContent.match(/Angle: ([\d.]+)°/);
                if (match) {
                    angle = parseFloat(match[1]);
                }
            } else if (textContent.startsWith('Time:')) {
                const match = textContent.match(/Time: ([\d.]+) ms/);
                if (match) {
                    time = parseFloat(match[1]);
                }
            }
        }

        // Extract rectangle corners from polygon element
        const polygons = doc.getElementsByTagName('polygon');
        let corners = null;

        for (let i = 0; i < polygons.length; i++) {
            const polygon = polygons[i];
            const fill = polygon.getAttribute('fill');
            const stroke = polygon.getAttribute('stroke');

            // Look for the inscribed rectangle (red fill and stroke)
            if (fill && fill.includes('255, 100, 100') && stroke && stroke.includes('#ff')) {
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

        // Extract centroid from circle element
        const circles = doc.getElementsByTagName('circle');
        let centroid = null;

        for (let i = 0; i < circles.length; i++) {
            const circle = circles[i];
            const fill = circle.getAttribute('fill');
            const r = circle.getAttribute('r');

            // Look for centroid marker (red fill, r=4)
            if (fill === '#ff0000' && r === '4') {
                centroid = {
                    x: parseFloat(circle.getAttribute('cx')),
                    y: parseFloat(circle.getAttribute('cy'))
                };
                break;
            }
        }

        if (!corners || !area || !size) {
            return null;
        }

        return {
            corners,
            width: size.width,
            height: size.height,
            area,
            angle: angle || 0,
            centroid,
            type,
            computationTimeMs: time
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
