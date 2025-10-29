#!/usr/bin/env node

/**
 * compute-all-combinations.js
 *
 * Generates all valid room section combinations based on connectivity rules.
 * This is the first step in the FloorMat pipeline before running layout tests.
 *
 * Input:  input/Rooms.json (graph connectivity)
 * Output: valid-combinations.json (all valid combinations)
 *
 * Validation Rules:
 * - Rule 1: If two or more rooms share an aisle, the aisle MUST be selected
 * - Rule 2: Single section must be Room (not Aisle/Crossing)
 * - Rule 3: An aisle can be included with a single room
 * - Rule 4: Each crossing has ≥2 connected aisles present
 * - Rule 5: If 3+ aisles connect to crossing, crossing must be present
 * - Rule 6: If two rooms share an aisle, the aisle cannot be missing (U-shape)
 * - Rule 7: All sections must form a single connected component
 * - Rule 8: Must include at least one room
 * - Rule 9: Each aisle must have at least one connected room selected
 * - Rule 10: Crossing cannot be the only bridge between disconnected components
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// =============================================================================
// CONFIGURATION
// =============================================================================

// Get prefix from command line arguments
const args = process.argv.slice(2);

if (args.length < 1) {
    console.error('Usage: node compute-all-combinations.js <prefix> [inputDir] [outputDir]');
    console.error('Example: node compute-all-combinations.js Level1');
    console.error('Example: node compute-all-combinations.js Level1 ./input ./output/Level1-output');
    process.exit(1);
}

const prefix = args[0];
const inputDir = args[1] || path.resolve(__dirname, 'input');
const outputDir = args[2] || path.resolve(__dirname, 'output', `${prefix}-output`);

// Paths based on prefix
const ROOMS_JSON_PATH = path.resolve(inputDir, `${prefix}-Rooms.json`);
const OUTPUT_PATH = path.resolve(outputDir, `${prefix}-valid-combinations.json`);

// =============================================================================
// LOAD ROOMS DATA
// =============================================================================

function loadRoomsData() {
    console.log('================================================================================');
    console.log('COMPUTE ALL VALID COMBINATIONS');
    console.log('================================================================================\n');

    console.log(`Step 1: Loading Rooms.json from: ${ROOMS_JSON_PATH}`);

    if (!fs.existsSync(ROOMS_JSON_PATH)) {
        console.error(`\n❌ ERROR: Rooms.json not found at: ${ROOMS_JSON_PATH}`);
        console.error('\nPlease ensure Rooms.json is in the input/ directory or update ROOMS_JSON_PATH in this script.\n');
        process.exit(1);
    }

    // Read file and remove BOM if present
    let fileContent = fs.readFileSync(ROOMS_JSON_PATH, 'utf8');
    if (fileContent.charCodeAt(0) === 0xFEFF) {
        fileContent = fileContent.slice(1);
    }
    const data = JSON.parse(fileContent);

    // Find the level by prefix
    const level = data.find(lvl => lvl.Id === prefix);
    if (!level) {
        console.error(`❌ ERROR: Could not find ${prefix} in Rooms.json`);
        process.exit(1);
    }

    console.log(`  ✓ Found level: ${level.Name || prefix}`);
    console.log(`  ✓ Found ${level.Rooms.length} room(s)\n`);

    // Return all rooms in the level
    return level.Rooms.map(room => ({
        roomId: room.Id,
        sections: room.RoomSections,
        joins: room.Joins || [] // Joins may not exist for single-section rooms
    }));
}

// =============================================================================
// BUILD GRAPH STRUCTURE
// =============================================================================

function buildGraph(sections, joins) {
    console.log('Step 2: Building connectivity graph...');

    // Map section IDs to their types
    const sectionTypes = new Map();
    const sectionList = [];

    sections.forEach(section => {
        const id = section.Id;
        const type = section.SectionType || 'Room'; // Default to Room if not specified
        sectionTypes.set(id, type);
        sectionList.push(id);
    });

    // Build adjacency graph
    const adjacency = new Map();
    sectionList.forEach(id => adjacency.set(id, new Set()));

    joins.forEach(join => {
        const s1 = join.Section1Id;
        const s2 = join.Section2Id;
        adjacency.get(s1).add(s2);
        adjacency.get(s2).add(s1);
    });

    // Identify rooms, aisles, and crossings
    const rooms = sectionList.filter(id => sectionTypes.get(id) === 'Room');
    const aisles = sectionList.filter(id => sectionTypes.get(id) === 'Aisle');
    const crossings = sectionList.filter(id => sectionTypes.get(id) === 'Crossing');

    console.log(`  ✓ Rooms: ${rooms.length}`);
    console.log(`  ✓ Aisles: ${aisles.length}`);
    console.log(`  ✓ Crossings: ${crossings.length}`);

    // Build aisle connectivity map (which rooms does each aisle connect)
    const aisleConnections = new Map();
    aisles.forEach(aisle => {
        const connectedRooms = Array.from(adjacency.get(aisle))
            .filter(neighbor => sectionTypes.get(neighbor) === 'Room');
        aisleConnections.set(aisle, connectedRooms);
    });

    // Build crossing connectivity map (which aisles does each crossing connect)
    const crossingConnections = new Map();
    crossings.forEach(crossing => {
        const connectedAisles = Array.from(adjacency.get(crossing))
            .filter(neighbor => sectionTypes.get(neighbor) === 'Aisle');
        crossingConnections.set(crossing, connectedAisles);
    });

    console.log('  ✓ Graph built successfully\n');

    return {
        sectionTypes,
        sectionList,
        adjacency,
        rooms,
        aisles,
        crossings,
        aisleConnections,
        crossingConnections
    };
}

// =============================================================================
// VALIDATION RULES
// =============================================================================

function isValidCombination(combination, graph) {
    const selected = new Set(combination);
    const { sectionTypes, aisleConnections, crossingConnections, adjacency } = graph;

    // Rule 2: Single section must be Room (not Aisle/Crossing)
    if (combination.length === 1) {
        const type = sectionTypes.get(combination[0]);
        if (type !== 'Room') {
            return false; // Single aisle or crossing is invalid
        }
        return true;
    }

    // Rule 1: Adjacency Constraint
    // If two or more rooms that share an aisle are selected, the aisle MUST be selected
    for (const [aisle, connectedRooms] of aisleConnections.entries()) {
        if (connectedRooms.length === 2) {
            const [room1, room2] = connectedRooms;
            // If both rooms that this aisle connects are selected
            if (selected.has(room1) && selected.has(room2)) {
                // The aisle MUST be selected
                if (!selected.has(aisle)) {
                    return false; // Missing required aisle between two selected rooms
                }
            }
        }
    }

    // Rule 3: Aisle Can Include Single Room
    // (No validation needed - this is a permissive rule that allows Room + Aisle)
    // Just ensure that if an aisle is alone, it's caught by Rule 2 above

    // Rule 4: Crossing Requires Multiple Aisles
    // Each crossing must have ≥2 connected aisles present
    for (const crossing of combination) {
        if (sectionTypes.get(crossing) === 'Crossing') {
            const connectedAisles = crossingConnections.get(crossing);
            const selectedAislesCount = connectedAisles.filter(aisle => selected.has(aisle)).length;
            if (selectedAislesCount < 2) {
                return false; // Crossing must have at least 2 aisles
            }
        }
    }

    // Rule 5: Three or More Aisles Require Crossing
    // If 3+ aisles connect to crossing, crossing must be present
    for (const [crossing, connectedAisles] of crossingConnections.entries()) {
        if (connectedAisles.length >= 3) {
            const selectedAislesCount = connectedAisles.filter(aisle => selected.has(aisle)).length;
            if (selectedAislesCount >= 3 && !selected.has(crossing)) {
                return false; // 3+ aisles selected but crossing missing
            }
        }
    }

    // Rule 6: U-Shape Aisle Requirement
    // (This is the same as Rule 1, already implemented above)
    // If two rooms share an aisle, the aisle cannot be missing

    // Rule 7: All Sections Must Be Connected
    // All sections must form a single connected component
    if (!isConnectedGraph(selected, adjacency)) {
        return false;
    }

    // Rule 8: Must Include At Least One Room
    // Any combination with aisles/crossings must have at least one room
    const hasRoom = combination.some(id => sectionTypes.get(id) === 'Room');
    if (!hasRoom) {
        return false; // No rooms present - invalid combination
    }

    // Rule 9: Aisle Must Have At Least One Connected Room
    // Each aisle must have at least one of its connected rooms selected
    for (const aisle of combination) {
        if (sectionTypes.get(aisle) === 'Aisle') {
            const connectedRooms = aisleConnections.get(aisle);
            const hasConnectedRoom = connectedRooms.some(room => selected.has(room));
            if (!hasConnectedRoom) {
                return false; // Aisle has no connected rooms selected
            }
        }
    }

    // Rule 10: Crossing Cannot Be Only Bridge
    // A crossing cannot be the only connection between separate components
    for (const crossing of combination) {
        if (sectionTypes.get(crossing) === 'Crossing') {
            // Create a temporary set without this crossing
            const withoutCrossing = new Set(selected);
            withoutCrossing.delete(crossing);

            // If removing the crossing disconnects the graph, it's a bridge
            if (!isConnectedGraph(withoutCrossing, adjacency)) {
                return false; // Crossing is the only bridge - invalid
            }
        }
    }

    return true;
}

function isConnectedGraph(selected, adjacency) {
    if (selected.size === 0) return true;
    if (selected.size === 1) return true;

    const visited = new Set();
    const queue = [Array.from(selected)[0]]; // Start from any selected section
    visited.add(queue[0]);

    while (queue.length > 0) {
        const current = queue.shift();
        const neighbors = adjacency.get(current);

        for (const neighbor of neighbors) {
            if (selected.has(neighbor) && !visited.has(neighbor)) {
                visited.add(neighbor);
                queue.push(neighbor);
            }
        }
    }

    return visited.size === selected.size;
}

// =============================================================================
// GENERATE ALL COMBINATIONS
// =============================================================================

function generateAllCombinations(graph) {
    console.log('Step 3: Generating all possible combinations...');

    const { sectionList } = graph;
    const n = sectionList.length;
    const totalPossible = Math.pow(2, n);

    console.log(`  Total sections: ${n}`);
    console.log(`  Total possible combinations: ${totalPossible.toLocaleString()}\n`);

    console.log('Step 4: Validating combinations against rules...');

    const validCombinations = [];
    let validCount = 0;
    let lastPercent = 0;

    for (let i = 1; i < totalPossible; i++) { // Start from 1 to skip empty set
        const combination = [];

        for (let j = 0; j < n; j++) {
            if (i & (1 << j)) {
                combination.push(sectionList[j]);
            }
        }

        if (isValidCombination(combination, graph)) {
            validCount++;

            // Sort sections alphabetically for consistent keys
            const sorted = combination.slice().sort();
            const key = sorted.join('_');

            validCombinations.push({
                key,
                sections: sorted,
                size: sorted.length
            });
        }

        // Progress indicator
        const percent = Math.floor((i / totalPossible) * 100);
        if (percent !== lastPercent && percent % 10 === 0) {
            console.log(`  Progress: ${percent}%`);
            lastPercent = percent;
        }
    }

    console.log(`  Progress: 100%`);
    console.log(`\n  ✓ Found ${validCount} valid combinations (${((validCount / totalPossible) * 100).toFixed(2)}%)\n`);

    return validCombinations;
}

// =============================================================================
// SAVE OUTPUT
// =============================================================================

function saveOutputMultiRoom(combinations, roomSummary) {
    console.log('\nSaving results...');

    const output = {
        generatedAt: new Date().toISOString(),
        totalCombinations: combinations.length,
        roomSummary: roomSummary,
        combinations: combinations
    };

    // Ensure output directory exists
    const outputDirPath = path.dirname(OUTPUT_PATH);
    if (!fs.existsSync(outputDirPath)) {
        fs.mkdirSync(outputDirPath, { recursive: true });
        console.log(`  ✓ Created output directory: ${outputDirPath}`);
    }

    fs.writeFileSync(OUTPUT_PATH, JSON.stringify(output, null, 2), 'utf8');

    console.log(`  ✓ Saved to: ${OUTPUT_PATH}`);
    console.log(`  ✓ File size: ${(fs.statSync(OUTPUT_PATH).size / 1024).toFixed(1)} KB`);
}

// =============================================================================
// STATISTICS
// =============================================================================

function printStatistics(combinations, graph) {
    console.log('================================================================================');
    console.log('STATISTICS');
    console.log('================================================================================\n');

    // By size
    const bySize = new Map();
    combinations.forEach(combo => {
        const size = combo.size;
        bySize.set(size, (bySize.get(size) || 0) + 1);
    });

    console.log('Combinations by size:');
    Array.from(bySize.keys()).sort((a, b) => a - b).forEach(size => {
        console.log(`  Size ${size}: ${bySize.get(size)} combinations`);
    });

    // By section type composition
    const { sectionTypes } = graph;
    const roomOnly = combinations.filter(c =>
        c.sections.every(s => sectionTypes.get(s) === 'Room')
    ).length;
    const withAisles = combinations.filter(c =>
        c.sections.some(s => sectionTypes.get(s) === 'Aisle')
    ).length;
    const withCrossings = combinations.filter(c =>
        c.sections.some(s => sectionTypes.get(s) === 'Crossing')
    ).length;

    console.log('\nBy composition:');
    console.log(`  Room-only: ${roomOnly}`);
    console.log(`  With aisles: ${withAisles}`);
    console.log(`  With crossings: ${withCrossings}`);

    console.log('\n================================================================================');
    console.log('COMPLETE!');
    console.log(`Successfully generated ${combinations.length} valid combinations`);
    console.log('================================================================================\n');
}

// =============================================================================
// MAIN
// =============================================================================

function main() {
    try {
        const allRoomsData = loadRoomsData(); // Returns array of {roomId, sections, joins}

        let allCombinations = [];
        const roomSummary = {};
        let globalCounter = 1;

        // Process each room independently
        allRoomsData.forEach(roomData => {
            console.log(`\nProcessing room: ${roomData.roomId}`);
            console.log('─'.repeat(80));

            const graph = buildGraph(roomData.sections, roomData.joins);
            const combinations = generateAllCombinations(graph);

            // Add room metadata and global sequential IDs
            combinations.forEach(combo => {
                const id = `${roomData.roomId}_${String(globalCounter).padStart(4, '0')}`;
                allCombinations.push({
                    id: id,
                    roomId: roomData.roomId,
                    key: combo.key,
                    sections: combo.sections,
                    size: combo.size
                });
                globalCounter++;
            });

            roomSummary[roomData.roomId] = combinations.length;
            console.log(`  ✓ ${combinations.length} valid combinations for ${roomData.roomId}`);
        });

        console.log('\n' + '='.repeat(80));
        console.log('SUMMARY');
        console.log('='.repeat(80));
        console.log('Combinations by room:');
        Object.entries(roomSummary).forEach(([roomId, count]) => {
            console.log(`  ${roomId}: ${count}`);
        });
        console.log(`\nTotal: ${allCombinations.length} combinations across ${allRoomsData.length} room(s)`);

        // Create a combined graph for statistics (optional - use first room's graph structure)
        const firstGraph = buildGraph(allRoomsData[0].sections, allRoomsData[0].joins);

        saveOutputMultiRoom(allCombinations, roomSummary);

        console.log('\n' + '='.repeat(80));
        console.log('COMPLETE!');
        console.log(`Successfully generated ${allCombinations.length} valid combinations`);
        console.log('='.repeat(80) + '\n');
    } catch (error) {
        console.error('\n❌ ERROR:', error.message);
        console.error(error.stack);
        process.exit(1);
    }
}

main();
