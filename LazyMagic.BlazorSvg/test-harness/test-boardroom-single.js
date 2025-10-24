#!/usr/bin/env node

/**
 * Quick test of single boardroom combo with dual-algorithm approach
 */

import { createRequire } from 'module';
const require = createRequire(import.meta.url);

// Load required modules
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');
const { boundaryBasedInscribedRectangle } = require('../wwwroot/SvgViewerBoundaryBased.js');
const { fastInscribedRectangle } = require('../wwwroot/SvgViewerOptimized.js');
const { findBoardroomLayout } = require('../wwwroot/SvgViewerBoardroom.js');

// Make BB and Optimized globally available
global.boundaryBasedInscribedRectangle = boundaryBasedInscribedRectangle;
global.fastInscribedRectangle = fastInscribedRectangle;

// Test combo 66 - the one you mentioned
const testCombo = {
    name: "Combo_0066",
    paths: [
        "Ballroom_Room_2",
        "Ballroom_Room_4",
        "Ballroom_Room_Grand",
        "Ballroom_Aisle_12",
        "Ballroom_Aisle_46"
    ]
};

console.log(`\nTesting ${testCombo.name}...`);

// Parse and merge paths
const svgPath = '../BlazorTest.WASM/wwwroot/Level1-normal.svg';

const mergeResult = SvgViewerAlgorithms.parseAndMergePaths(svgPath, testCombo.paths, true);

if (!mergeResult.success) {
    console.log(`❌ Failed to merge paths: ${mergeResult.error}`);
    process.exit(1);
}

const polygon = mergeResult.polygon;
console.log(`✓ Merged ${testCombo.paths.length} paths into polygon with ${polygon.length} vertices`);

// Run boardroom layout with debug mode
const boardroomOptions = {
    boardroomWidth: 13,
    minLength: 14,
    lengthIncrement: 6,
    angleSamples: 90,
    centroidSamples: 25,
    debugMode: true  // Enable debug output
};

console.log('\nRunning dual-algorithm boardroom search...\n');

const result = findBoardroomLayout(polygon, boardroomOptions);

if (!result) {
    console.log('❌ No boardroom layout found');
    process.exit(1);
}

console.log('\n=== RESULTS ===');
console.log(`Winner: ${result.winningAlgorithm}`);
console.log(`Area: ${result.area.toFixed(1)} sq ft`);
console.log(`Dimensions: ${result.width.toFixed(1)} × ${result.height.toFixed(1)} ft`);
console.log(`Sets: ${result.sets} (${result.tables} tables)`);
console.log(`Angle: ${result.angle.toFixed(1)}°`);
console.log(`Computation time: ${result.computationTime.toFixed(1)}ms`);
console.log('✓ Test complete\n');
