// Extract pre-computed boardroom layout data from BoardroomResults SVG files
// and generate precomputed-boardroom.json

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { DOMParser } from 'xmldom';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const testResultsDir = path.join(__dirname, '../BoardroomResults');
const combinationsPath = path.join(__dirname, 'valid-combinations.json');
const outputPath = path.join(__dirname, 'precomputed-boardroom.json');

/**
 * Parse SVG file to extract boardroom layout data
 * @param {string} svgPath - Path to SVG file
 * @returns {Object|null} Boardroom layout data
 */
function parseSvgBoardroom(svgPath) {
    try {
        const svgContent = fs.readFileSync(svgPath, 'utf8');
        const parser = new DOMParser();
        const doc = parser.parseFromString(svgContent, 'text/xml');

        // Extract area data from comments
        let polygonArea = null;
        let boardroomArea = null;
        let sets = null;
        let tables = null;
        let computationTimeMs = null;

        const polygonAreaMatch = svgContent.match(/<!--\s*polygonArea:\s*([0-9.]+)\s*-->/);
        const boardroomAreaMatch = svgContent.match(/<!--\s*boardroomArea:\s*([0-9.]+)\s*-->/);
        const setsMatch = svgContent.match(/<!--\s*sets:\s*(\d+)\s*-->/);
        const tablesMatch = svgContent.match(/<!--\s*tables:\s*(\d+)\s*-->/);
        const timeMatch = svgContent.match(/<!--\s*computationTimeMs:\s*([0-9.]+)\s*-->/);

        if (polygonAreaMatch) polygonArea = parseFloat(polygonAreaMatch[1]);
        if (boardroomAreaMatch) boardroomArea = parseFloat(boardroomAreaMatch[1]);
        if (setsMatch) sets = parseInt(setsMatch[1], 10);
        if (tablesMatch) tables = parseInt(tablesMatch[1], 10);
        if (timeMatch) computationTimeMs = parseFloat(timeMatch[1]);

        // Extract boardroom rectangle polygon (comment: "Boardroom Layout Rectangle")
        // Look for polygon with orange stroke (#ff8800)
        const polygons = doc.getElementsByTagName('polygon');
        let boardroomPoints = null;

        for (let i = 0; i < polygons.length; i++) {
            const stroke = polygons[i].getAttribute('stroke');
            if (stroke === '#ff8800') {
                boardroomPoints = polygons[i].getAttribute('points');
                break;
            }
        }

        if (!boardroomPoints) {
            console.warn(`Warning: No boardroom rectangle found in ${svgPath}`);
            return null;
        }

        // Parse rectangle corners from points attribute
        // Points format: "x1,y1 x2,y2 x3,y3 x4,y4"
        const coordRegex = /([+-]?\d+\.?\d*),([+-]?\d+\.?\d*)/g;
        const corners = [];
        let match;

        while ((match = coordRegex.exec(boardroomPoints)) !== null) {
            corners.push({
                x: parseFloat(match[1]),
                y: parseFloat(match[2])
            });
        }

        if (corners.length !== 4) {
            console.warn(`Warning: Expected 4 corners, found ${corners.length} in ${svgPath}`);
            return null;
        }

        // Calculate width, height, and angle from corners
        // Width is distance from corner 0 to corner 1
        // Height is distance from corner 0 to corner 3
        const dx1 = corners[1].x - corners[0].x;
        const dy1 = corners[1].y - corners[0].y;
        const dx2 = corners[3].x - corners[0].x;
        const dy2 = corners[3].y - corners[0].y;

        const width = Math.sqrt(dx1 * dx1 + dy1 * dy1);
        const height = Math.sqrt(dx2 * dx2 + dy2 * dy2);

        // Angle is the arctangent of the first edge
        let angle = Math.atan2(dy1, dx1) * 180 / Math.PI;
        if (angle < 0) angle += 180; // Normalize to 0-180°

        // Calculate centroid
        const centroid = {
            x: (corners[0].x + corners[1].x + corners[2].x + corners[3].x) / 4,
            y: (corners[0].y + corners[1].y + corners[2].y + corners[3].y) / 4
        };

        // Calculate area from width × height
        const area = width * height;

        return {
            corners,
            width,
            height,
            area,
            angle,
            centroid,
            sets,
            tables,
            polygonArea,
            boardroomArea,
            computationTimeMs,
            type: 'boardroom'
        };

    } catch (error) {
        console.error(`Error parsing ${svgPath}:`, error.message);
        return null;
    }
}

/**
 * Main extraction function
 */
async function extractBoardroomData() {
    console.log('Boardroom Layout Data Extraction');
    console.log('='.repeat(60));
    console.log();

    // Load valid combinations
    const combinations = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));
    console.log(`Loaded ${combinations.combinations.length} valid combinations`);

    // Check if BoardroomResults directory exists
    if (!fs.existsSync(testResultsDir)) {
        console.error(`Error: BoardroomResults directory not found: ${testResultsDir}`);
        console.error('Run test-runner-boardroom.js first to generate boardroom layouts.');
        process.exit(1);
    }

    const boardroomLayouts = [];
    let successCount = 0;
    let failureCount = 0;
    let totalComputationTime = 0;

    // Process each combination
    for (let i = 0; i < combinations.combinations.length; i++) {
        const combo = combinations.combinations[i];
        const comboName = `Combo_${String(i + 1).padStart(4, '0')}`;
        const svgPath = path.join(testResultsDir, `${comboName}.svg`);

        if (!fs.existsSync(svgPath)) {
            console.warn(`Warning: SVG file not found: ${comboName}.svg`);
            failureCount++;
            continue;
        }

        const layoutData = parseSvgBoardroom(svgPath);

        if (!layoutData) {
            console.warn(`Warning: Failed to extract data from ${comboName}.svg`);
            failureCount++;
            continue;
        }

        // Build output object
        boardroomLayouts.push({
            key: combo.key,
            sections: combo.sections,
            boardroomLayout: {
                corners: layoutData.corners,
                width: layoutData.width,
                height: layoutData.height,
                area: layoutData.area,
                angle: layoutData.angle,
                centroid: layoutData.centroid,
                sets: layoutData.sets,
                tables: layoutData.tables,
                type: 'boardroom'
            },
            polygonArea: layoutData.polygonArea,
            boardroomArea: layoutData.boardroomArea,
            computationTimeMs: layoutData.computationTimeMs
        });

        totalComputationTime += layoutData.computationTimeMs || 0;
        successCount++;

        if ((i + 1) % 50 === 0) {
            console.log(`Processed ${i + 1}/${combinations.combinations.length} combinations...`);
        }
    }

    console.log();
    console.log('Extraction complete:');
    console.log(`  Successful: ${successCount}`);
    console.log(`  Failed: ${failureCount}`);
    console.log(`  Total computation time: ${totalComputationTime.toFixed(1)} ms`);
    console.log(`  Average computation time: ${(totalComputationTime / successCount).toFixed(1)} ms`);

    // Build output JSON
    const output = {
        generatedAt: new Date().toISOString(),
        totalCombinations: combinations.combinations.length,
        successfulComputations: successCount,
        failedComputations: failureCount,
        totalComputationTimeMs: totalComputationTime,
        averageComputationTimeMs: totalComputationTime / successCount,
        boardroomLayouts: boardroomLayouts
    };

    // Write output file
    fs.writeFileSync(outputPath, JSON.stringify(output, null, 2), 'utf8');

    console.log();
    console.log(`Output written to: ${outputPath}`);

    const stats = fs.statSync(outputPath);
    console.log(`File size: ${(stats.size / 1024).toFixed(2)} KB`);
    console.log();
}

// Run extraction
extractBoardroomData().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
