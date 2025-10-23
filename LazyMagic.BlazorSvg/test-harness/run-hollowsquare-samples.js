#!/usr/bin/env node

/**
 * Run Hollow Square Sample Tests
 *
 * Wrapper script that runs the hollow square test harness on a sample subset.
 * This loads the sample combinations and generates results in the BlazorTest.WASM samples directory.
 */

import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Load sample combinations
const validCombinationsPath = path.join(__dirname, 'valid-combinations-hollowsquare-sample.json');
const validCombinationsData = JSON.parse(fs.readFileSync(validCombinationsPath, 'utf8'));
const sampleCombinations = validCombinationsData.combinations;

// Map sample combinations to requested combo numbers (1-indexed)
const requestedComboNumbers = [4, 7, 8, 29, 32, 33, 34, 49, 66, 112, 130, 179, 194, 217, 236, 251];

console.log('\n='.repeat(60));
console.log('Hollow Square Sample Tests');
console.log('='.repeat(60));
console.log(`\nTesting ${sampleCombinations.length} sample combinations:`);
console.log(requestedComboNumbers.join(', '));
console.log('');

// Load required modules
const require = createRequire(import.meta.url);
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');
const { findHollowSquareLayout } = require('../wwwroot/SvgViewerHollowSquare.js');
const { config } = await import('./test-config.js');

// Read SVG file once
const svgPath = config.svgPath;
const svgContent = fs.readFileSync(svgPath, 'utf8');

// Create output directory and clean old SVG files
const outputDir = path.join(__dirname, '../TestResultsSamples/HollowSquareResultsSamples');
if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
} else {
    // Delete existing SVG files
    const existingFiles = fs.readdirSync(outputDir).filter(f => f.endsWith('.svg'));
    for (const file of existingFiles) {
        fs.unlinkSync(path.join(outputDir, file));
    }
    if (existingFiles.length > 0) {
        console.log(`Deleted ${existingFiles.length} existing SVG file(s)\n`);
    }
}

console.log(`Output directory: ${outputDir}\n`);

// Track total runtime
const startTime = Date.now();

// Helper functions
function extractPathData(svgContent, pathIds) {
    const paths = [];
    for (const pathId of pathIds) {
        let regex = new RegExp(`<path[^>]*id="${pathId}"[^>]*d="([^"]*)"`, 'i');
        let match = svgContent.match(regex);

        if (match) {
            paths.push({ id: pathId, d: match[1] });
            continue;
        }

        regex = new RegExp(`<polygon[^>]*id="${pathId}"[^>]*points="([^"]*)"`, 'i');
        match = svgContent.match(regex);

        if (match) {
            const points = match[1].trim().split(/\s+/);
            const pathCommands = points.map((point, index) => {
                const [x, y] = point.split(',');
                return index === 0 ? `M ${x} ${y}` : `L ${x} ${y}`;
            }).join(' ') + ' Z';

            paths.push({ id: pathId, d: pathCommands });
        }
    }

    return paths;
}

// Process each sample
let passed = 0;
let failed = 0;

for (let i = 0; i < sampleCombinations.length; i++) {
    const combo = sampleCombinations[i];
    const comboNumber = requestedComboNumbers[i];
    const comboName = `Combo_${String(comboNumber).padStart(4, '0')}`;

    console.log(`\n[${i + 1}/${sampleCombinations.length}] Processing ${comboName}...`);
    console.log(`  Sections: ${combo.sections.join(', ')}`);

    try {
        // Extract path data
        const pathData = extractPathData(svgContent, combo.sections);

        if (pathData.length !== combo.sections.length) {
            console.log(`  ❌ Could not find all paths. Found ${pathData.length}/${combo.sections.length}`);
            failed++;
            continue;
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
            console.log(`  ❌ Failed to find polygon boundary`);
            failed++;
            continue;
        }

        // Calculate polygon area
        const polygonArea = SvgViewerAlgorithms.calculatePolygonArea(polygon);

        // Run hollow square layout algorithm (with dynamic limits!)
        const hollowSquareLayout = findHollowSquareLayout(polygon, {
            // Use 12 angles × 23×23 centroids = 6,348 positions for speed/quality balance
            angleSamples: 12,
            centroidSamples: 23,
            debugMode: false
        });

        if (!hollowSquareLayout) {
            console.log(`  ❌ No valid hollow square layout found`);
            failed++;
            continue;
        }

        // Both areas are in the same SVG units (1 SVG unit = 1 ft), so no conversion needed
        const fillRatio = hollowSquareLayout.area / polygonArea;

        console.log(`  ✓ Found layout:`);
        console.log(`    Tables: ${hollowSquareLayout.tables} (${hollowSquareLayout.lengthRun}L × ${hollowSquareLayout.depthRun}D)`);
        console.log(`    Dimensions: ${hollowSquareLayout.width.toFixed(1)} × ${hollowSquareLayout.height.toFixed(1)} ft`);
        console.log(`    Fill ratio: ${(fillRatio * 100).toFixed(1)}%`);

        // Generate SVG visualization
        const bounds = {
            minX: Math.min(...polygon.map(p => p.x)),
            maxX: Math.max(...polygon.map(p => p.x)),
            minY: Math.min(...polygon.map(p => p.y)),
            maxY: Math.max(...polygon.map(p => p.y))
        };

        const padding = 20;
        const tableWidth = 320;
        const tableHeight = 120;
        const tablePadding = 30;
        const titleHeight = 30;

        const viewBoxX = bounds.minX - padding;
        const viewBoxY = bounds.minY - padding - titleHeight;
        const viewBoxWidth = (bounds.maxX - bounds.minX) + (padding * 2) + tableWidth + tablePadding;
        const viewBoxHeight = (bounds.maxY - bounds.minY) + (padding * 2) + titleHeight;

        const pathElements = pathData.map(p =>
            `<path id="${p.id}" d="${p.d}" fill="none" stroke="#808080" stroke-width="1"/>`
        ).join('\n    ');

        const polygonPoints = polygon.map(p => `${p.x},${p.y}`).join(' ');
        const c = hollowSquareLayout.corners;
        const hollowSquarePoints = `${c[0].x},${c[0].y} ${c[1].x},${c[1].y} ${c[2].x},${c[2].y} ${c[3].x},${c[3].y}`;
        const centroid = hollowSquareLayout.centroid;

        const cornerMarkers = c.map((corner, idx) => `
  <circle cx="${corner.x}" cy="${corner.y}" r="3" fill="#9333ea"/>
  <text x="${corner.x + 6}" y="${corner.y + 4}" font-size="8" fill="#9333ea">hs${idx}</text>`).join('');

        const vertexMarkers = polygon.map((vertex, idx) => `
  <circle cx="${vertex.x}" cy="${vertex.y}" r="2" fill="#4080ff"/>
  <text x="${vertex.x - 8}" y="${vertex.y - 6}" font-size="8" fill="#4080ff">p${idx}</text>`).join('');

        // SVG units are 1:1 with real-world feet, so area is just width × height (no conversion)
        const hollowSquareAreaSqInches = hollowSquareLayout.area;
        const tableX = bounds.maxX + tablePadding;
        const tableY = bounds.minY;

        const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg"
     viewBox="${viewBoxX} ${viewBoxY} ${viewBoxWidth} ${viewBoxHeight}"
     width="${viewBoxWidth}" height="${viewBoxHeight}"
     style="background: white;">

  <!-- Title -->
  <text x="${viewBoxX + 10}" y="${viewBoxY + 20}" font-size="16" font-weight="bold" fill="#000">
    ${comboName}
  </text>

  <!-- Original test paths (gray, semi-transparent) -->
  <g opacity="0.3">
    ${pathElements}
  </g>

  <!-- Boundary polygon (blue, dashed) -->
  <polygon points="${polygonPoints}" fill="none" stroke="#4080ff" stroke-width="2" stroke-dasharray="5,5"/>

  <!-- Hollow square layout (purple fill, solid border) -->
  <polygon points="${hollowSquarePoints}" fill="rgba(147, 51, 234, 0.15)" stroke="#9333ea" stroke-width="3"/>

  <!-- Centroid marker -->
  <circle cx="${centroid.x}" cy="${centroid.y}" r="4" fill="#9333ea"/>
  <text x="${centroid.x + 8}" y="${centroid.y + 5}" font-size="10" fill="#9333ea">hollow square</text>

  <!-- Corner markers -->
  ${cornerMarkers}

  <!-- Vertex markers -->
  ${vertexMarkers}

  <!-- Data Table (positioned to the right) -->
  <g transform="translate(${tableX}, ${tableY})">
    <rect x="0" y="0" width="${tableWidth}" height="${tableHeight}" fill="white" stroke="#ccc" stroke-width="1"/>
    <rect x="0" y="0" width="${tableWidth}" height="30" fill="#f0f0f0"/>
    <text x="${tableWidth / 2}" y="20" font-size="14" font-weight="bold" text-anchor="middle">Hollow Square Layout</text>

    <text x="10" y="50" font-size="11" font-weight="bold">Tables:</text>
    <text x="${tableWidth - 10}" y="50" font-size="11" text-anchor="end">${hollowSquareLayout.tables} (${hollowSquareLayout.lengthRun}L × ${hollowSquareLayout.depthRun}D)</text>

    <text x="10" y="70" font-size="11" font-weight="bold">Dimensions:</text>
    <text x="${tableWidth - 10}" y="70" font-size="11" text-anchor="end">${hollowSquareLayout.width.toFixed(1)} × ${hollowSquareLayout.height.toFixed(1)} ft</text>

    <text x="10" y="90" font-size="11" font-weight="bold">Area:</text>
    <text x="${tableWidth - 10}" y="90" font-size="11" text-anchor="end">${hollowSquareAreaSqInches.toFixed(2)} sq in</text>

    <text x="10" y="110" font-size="11" font-weight="bold">Fill Ratio:</text>
    <text x="${tableWidth - 10}" y="110" font-size="11" text-anchor="end">${(fillRatio * 100).toFixed(1)}%</text>
  </g>

  <!-- Area Data (embedded as XML comments for extraction) -->
  <!-- polygonArea: ${polygonArea.toFixed(4)} -->
  <!-- hollowSquareArea: ${hollowSquareAreaSqInches.toFixed(4)} -->
  <!-- fillRatio: ${(fillRatio * 100).toFixed(1)} -->

</svg>
`;

        // Save SVG
        const outputPath = path.join(outputDir, `${comboName}.svg`);
        fs.writeFileSync(outputPath, svg, 'utf8');
        console.log(`  ✓ Saved to ${comboName}.svg`);

        passed++;

    } catch (error) {
        console.log(`  ❌ Error: ${error.message}`);
        failed++;
    }
}

// Calculate total runtime
const endTime = Date.now();
const totalRuntimeMs = endTime - startTime;
const totalRuntimeSec = (totalRuntimeMs / 1000).toFixed(2);

console.log('\n' + '='.repeat(60));
console.log('Summary');
console.log('='.repeat(60));
console.log(`Passed: ${passed}`);
console.log(`Failed: ${failed}`);
console.log(`Total Runtime: ${totalRuntimeSec}s`);
console.log('');

// Save summary JSON
const summary = {
    timestamp: new Date().toISOString(),
    totalTests: sampleCombinations.length,
    passed: passed,
    failed: failed,
    totalRuntimeMs: totalRuntimeMs,
    totalRuntimeSec: parseFloat(totalRuntimeSec),
    configuration: {
        angleSamples: 12,
        centroidSamples: 23,
        totalPositions: 12 * 23 * 23,
        gridSpacing: '23×23 = 529 centroids per angle'
    }
};

const summaryPath = path.join(outputDir, 'hollowsquare-sample-summary.json');
fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2), 'utf8');
console.log(`Summary saved to: hollowsquare-sample-summary.json\n`);
