// Analyze goal rectangles to find patterns
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { config } from './test-config.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const BALLROOM_SCALE = 48.345845;

function extractGoalRectangle(svgContent, testName) {
    const rectRegex = new RegExp(`<rect[^>]*id="${testName}"[^>]*/>`, 's');
    const match = svgContent.match(rectRegex);

    if (!match) return null;

    const rectTag = match[0];

    const widthMatch = rectTag.match(/width="([^"]*)"/);
    const heightMatch = rectTag.match(/height="([^"]*)"/);
    const xMatch = rectTag.match(/x="([^"]*)"/);
    const yMatch = rectTag.match(/y="([^"]*)"/);
    const transformMatch = rectTag.match(/transform="([^"]*)"/);

    if (!widthMatch || !heightMatch || !xMatch || !yMatch) return null;

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

    let angle = 0;
    if (transformMatch) {
        const transformStr = transformMatch[1];
        const matrixMatch = transformStr.match(/matrix\(([^)]+)\)/);

        if (matrixMatch) {
            const values = matrixMatch[1].split(/[\s,]+/).map(Number);
            const [a, b, c, d, e, f] = values;

            // Calculate angle from matrix
            angle = Math.atan2(b, a) * 180 / Math.PI;
            if (angle < 0) angle += 180;
            if (angle >= 180) angle -= 180;

            corners = corners.map(corner => ({
                x: a * corner.x + c * corner.y + e,
                y: b * corner.x + d * corner.y + f
            }));
        }
    }

    // Scale to ballroom coordinates
    corners = corners.map(corner => ({
        x: corner.x / BALLROOM_SCALE,
        y: corner.y / BALLROOM_SCALE
    }));

    // Calculate centroid
    const centroid = {
        x: corners.reduce((sum, c) => sum + c.x, 0) / corners.length,
        y: corners.reduce((sum, c) => sum + c.y, 0) / corners.length
    };

    // Calculate area
    let area = 0;
    for (let i = 0; i < corners.length; i++) {
        const j = (i + 1) % corners.length;
        area += corners[i].x * corners[j].y;
        area -= corners[j].x * corners[i].y;
    }
    area = Math.abs(area) / 2;

    const scaledWidth = Math.sqrt((corners[1].x - corners[0].x) ** 2 + (corners[1].y - corners[0].y) ** 2);
    const scaledHeight = Math.sqrt((corners[3].x - corners[0].x) ** 2 + (corners[3].y - corners[0].y) ** 2);
    const aspectRatio = scaledWidth / scaledHeight;

    return {
        corners,
        area,
        width: scaledWidth,
        height: scaledHeight,
        aspectRatio,
        angle,
        centroid
    };
}

async function analyzeGoals() {
    const svgPath = path.resolve(config.svgPath);
    const svgContent = fs.readFileSync(svgPath, 'utf8');

    const testsWithGoals = ['Test02', 'Test06', 'Test08', 'Test10', 'Test11', 'Test12', 'Test15', 'Test17', 'Test19'];

    console.log('GOAL RECTANGLE ANALYSIS');
    console.log('='.repeat(80));
    console.log('\nExtracting patterns from goal rectangles...\n');

    const goals = [];

    for (const testName of testsWithGoals) {
        const goal = extractGoalRectangle(svgContent, testName);
        if (goal) {
            goals.push({ testName, ...goal });
            console.log(`${testName}:`);
            console.log(`  Area:         ${goal.area.toFixed(1)} sq px`);
            console.log(`  Dimensions:   ${goal.width.toFixed(1)} × ${goal.height.toFixed(1)}`);
            console.log(`  Aspect Ratio: ${goal.aspectRatio.toFixed(3)}`);
            console.log(`  Angle:        ${goal.angle.toFixed(1)}°`);
            console.log(`  Centroid:     (${goal.centroid.x.toFixed(1)}, ${goal.centroid.y.toFixed(1)})`);
            console.log();
        }
    }

    console.log('\nPATTERN ANALYSIS');
    console.log('='.repeat(80));

    // Angle patterns
    const angles = goals.map(g => g.angle);
    const uniqueAngles = [...new Set(angles.map(a => Math.round(a * 10) / 10))].sort((a, b) => a - b);
    console.log('\nAngles used in goal rectangles:');
    console.log(`  ${uniqueAngles.join('°, ')}°`);
    console.log(`  Most common: ${angles.filter(a => Math.abs(a - 9.4) < 0.5).length} use ~9.4°`);
    console.log(`  Most common: ${angles.filter(a => Math.abs(a - 99.4) < 0.5).length} use ~99.4°`);
    console.log(`  Most common: ${angles.filter(a => Math.abs(a - 16.0) < 0.5).length} use ~16.0°`);

    // Aspect ratio patterns
    const aspectRatios = goals.map(g => g.aspectRatio).sort((a, b) => a - b);
    console.log('\nAspect ratios used:');
    aspectRatios.forEach((ar, i) => {
        console.log(`  ${goals.find(g => Math.abs(g.aspectRatio - ar) < 0.001).testName}: ${ar.toFixed(3)}`);
    });

    const avgAspect = aspectRatios.reduce((sum, ar) => sum + ar, 0) / aspectRatios.length;
    const minAspect = Math.min(...aspectRatios);
    const maxAspect = Math.max(...aspectRatios);
    console.log(`  Range: ${minAspect.toFixed(3)} - ${maxAspect.toFixed(3)}`);
    console.log(`  Average: ${avgAspect.toFixed(3)}`);

    // Check which aspect ratios we're missing
    const currentRatios = [0.5, 0.6, 0.7, 0.75, 0.85, 1.0, 1.1, 1.15, 1.2, 1.3, 1.4, 1.5, 1.7, 2.0, 2.2, 2.3, 2.5, 2.8, 3.0];
    const missingRatios = aspectRatios.filter(ar => {
        return !currentRatios.some(cr => Math.abs(cr - ar) < 0.05);
    });

    if (missingRatios.length > 0) {
        console.log('\n⚠️  MISSING ASPECT RATIOS in current algorithm:');
        missingRatios.forEach(ar => {
            const test = goals.find(g => Math.abs(g.aspectRatio - ar) < 0.001).testName;
            console.log(`  ${ar.toFixed(3)} (needed for ${test})`);
        });
    }

    // Area patterns
    const areas = goals.map(g => g.area);
    console.log('\nArea distribution:');
    console.log(`  Small (<5K):    ${areas.filter(a => a < 5000).length} tests`);
    console.log(`  Medium (5-20K): ${areas.filter(a => a >= 5000 && a < 20000).length} tests`);
    console.log(`  Large (>20K):   ${areas.filter(a => a >= 20000).length} tests`);

    console.log('\n' + '='.repeat(80));
}

analyzeGoals().catch(err => {
    console.error('Error:', err);
    process.exit(1);
});
