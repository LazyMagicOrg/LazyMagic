// Pre-compute inscribed rectangles for all valid connected combinations of ballroom sections
// This script generates all possible connected subgraphs from the Rooms.json connectivity data,
// calculates the optimal inscribed rectangle for each using the accurate algorithm (no time limit),
// and stores the results in the SVG file for instant runtime lookup.

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const roomsJsonPath = path.join(__dirname, '../../../../BCProjects/BCTenancies/bcs-cerulean/base/SetsCmp/data/Rooms.json');

/**
 * Build adjacency graph from Rooms.json Joins data
 */
function buildAdjacencyGraph(joins) {
    const graph = new Map();

    for (const join of joins) {
        const { Section1Id, Section2Id } = join;

        // Add Section1Id -> Section2Id
        if (!graph.has(Section1Id)) {
            graph.set(Section1Id, new Set());
        }
        graph.get(Section1Id).add(Section2Id);

        // Add Section2Id -> Section1Id (undirected graph)
        if (!graph.has(Section2Id)) {
            graph.set(Section2Id, new Set());
        }
        graph.get(Section2Id).add(Section1Id);
    }

    return graph;
}

/**
 * Check if a set of nodes forms a connected subgraph
 */
function isConnected(nodes, graph) {
    if (nodes.length === 0) return false;
    if (nodes.length === 1) return true;

    const visited = new Set();
    const queue = [nodes[0]];
    visited.add(nodes[0]);

    while (queue.length > 0) {
        const current = queue.shift();
        const neighbors = graph.get(current) || new Set();

        for (const neighbor of neighbors) {
            if (nodes.includes(neighbor) && !visited.has(neighbor)) {
                visited.add(neighbor);
                queue.push(neighbor);
            }
        }
    }

    return visited.size === nodes.length;
}

/**
 * Parse section ID to extract type and identifier
 * Format: Ballroom_Type_Id (e.g., Ballroom_Room_1, Ballroom_Aisle_12, Ballroom_Crossing_3456)
 */
function parseSectionId(sectionId) {
    const parts = sectionId.split('_');
    if (parts.length < 3) {
        throw new Error(`Invalid section ID format: ${sectionId}`);
    }

    return {
        room: parts[0],           // "Ballroom"
        type: parts[1],           // "Room", "Aisle", "Crossing"
        id: parts.slice(2).join('_'), // "1", "12", "3456", "Grand"
        fullId: sectionId
    };
}

/**
 * Build metadata about section connectivity from the graph
 * Returns maps of which rooms/aisles connect to which sections
 */
function buildConnectivityMetadata(sectionIds, graph) {
    const metadata = {
        rooms: new Set(),
        aisles: new Set(),
        crossings: new Set(),
        aisleToRooms: new Map(),      // Which rooms each aisle connects to
        crossingToAisles: new Map()   // Which aisles each crossing connects to
    };

    // Categorize all sections by type
    for (const sectionId of sectionIds) {
        const parsed = parseSectionId(sectionId);

        if (parsed.type === 'Room') {
            metadata.rooms.add(sectionId);
        } else if (parsed.type === 'Aisle') {
            metadata.aisles.add(sectionId);
        } else if (parsed.type === 'Crossing') {
            metadata.crossings.add(sectionId);
        }
    }

    // Build aisle-to-room connections from graph
    for (const aisle of metadata.aisles) {
        const connectedRooms = [];
        const neighbors = graph.get(aisle) || new Set();

        for (const neighbor of neighbors) {
            if (metadata.rooms.has(neighbor)) {
                connectedRooms.push(neighbor);
            }
        }

        metadata.aisleToRooms.set(aisle, connectedRooms);
    }

    // Build crossing-to-aisle connections from graph
    for (const crossing of metadata.crossings) {
        const connectedAisles = [];
        const neighbors = graph.get(crossing) || new Set();

        for (const neighbor of neighbors) {
            if (metadata.aisles.has(neighbor)) {
                connectedAisles.push(neighbor);
            }
        }

        metadata.crossingToAisles.set(crossing, connectedAisles);
    }

    return metadata;
}

/**
 * Check if a combination is valid based on generic connectivity rules
 * Rules (aligned with C# FloorViewModel.FixPaths logic + additional JS-only structural rules):
 * 0. [C#] Single section must be a Room (not Aisle or Crossing)
 * 1. [C#] Each aisle must connect to at least one room (not just other aisles/crossings)
 * 2. [JS] Each crossing must have at least 2 sections total in the combination
 * 3. [C#] Each crossing must have at least 2 of its connecting aisles present
 * 4. [JS] If 3+ aisles that connect to a crossing are present, the crossing must be present
 * 5. [JS] If an aisle is missing but 2+ of its connected rooms are present, invalid (U-shape)
 * 6. [JS] Crossings cannot be the only bridge connecting components (removing should not disconnect)
 *
 * [C#] = Matches C# FloorViewModel.cs logic
 * [JS] = Additional JavaScript validation for structural integrity
 */
function isValidCombination(sections, graph, metadata) {
    const sectionSet = new Set(sections);

    // Rule 0: Single section must be a Room (C# FloorViewModel.cs lines 125-131)
    if (sections.length === 1) {
        const onlySection = sections[0];
        const parsed = parseSectionId(onlySection);

        if (parsed.type !== 'Room') {
            return false; // Single Aisle or Crossing not allowed
        }
    }

    // Rule 1: Each aisle must connect to at least one room
    for (const aisle of metadata.aisles) {
        if (sectionSet.has(aisle)) {
            const connectedRooms = metadata.aisleToRooms.get(aisle) || [];
            const hasConnectedRoom = connectedRooms.some(room => sectionSet.has(room));

            if (!hasConnectedRoom) {
                return false; // Aisle present but no connected rooms
            }
        }
    }

    // Rules 2, 3: Crossing validation
    for (const crossing of metadata.crossings) {
        if (sectionSet.has(crossing)) {
            // Rule 2: Crossing must have at least 2 sections total
            if (sections.length < 2) {
                return false;
            }

            // Rule 3: At least 2 connecting aisles must be present
            const connectedAisles = metadata.crossingToAisles.get(crossing) || [];
            const presentAisles = connectedAisles.filter(aisle => sectionSet.has(aisle)).length;

            if (presentAisles < 2) {
                return false;
            }
        }
    }

    // Rule 4: If 3+ aisles connecting to a crossing are present, crossing must be present
    for (const crossing of metadata.crossings) {
        if (!sectionSet.has(crossing)) {
            const connectedAisles = metadata.crossingToAisles.get(crossing) || [];
            const presentAisles = connectedAisles.filter(aisle => sectionSet.has(aisle)).length;

            if (presentAisles >= 3) {
                return false; // Need crossing to avoid central overlap
            }
        }
    }

    // Rule 5: U-shape prevention - aisle missing but 2+ connected rooms present
    for (const aisle of metadata.aisles) {
        if (!sectionSet.has(aisle)) {
            const connectedRooms = metadata.aisleToRooms.get(aisle) || [];
            const presentRooms = connectedRooms.filter(room => sectionSet.has(room)).length;

            if (presentRooms >= 2) {
                return false; // Would create U-shape without aisle
            }
        }
    }

    // Rule 6: Crossings cannot be the only bridge
    for (const crossing of metadata.crossings) {
        if (sectionSet.has(crossing)) {
            const sectionsWithoutCrossing = sections.filter(s => s !== crossing);

            if (sectionsWithoutCrossing.length > 0) {
                if (!isConnected(sectionsWithoutCrossing, graph)) {
                    return false; // Crossing is the only bridge
                }
            }
        }
    }

    return true;
}

/**
 * Generate all possible subsets of an array
 */
function* generateSubsets(array) {
    const n = array.length;
    const totalSubsets = Math.pow(2, n);

    for (let i = 1; i < totalSubsets; i++) { // Start at 1 to skip empty set
        const subset = [];
        for (let j = 0; j < n; j++) {
            if (i & (1 << j)) {
                subset.push(array[j]);
            }
        }
        yield subset;
    }
}

/**
 * Generate all valid connected combinations from the adjacency graph
 */
function generateConnectedCombinations(sectionIds, graph) {
    const validCombinations = [];

    console.log(`Generating all subsets from ${sectionIds.length} sections...`);

    // Build connectivity metadata (generic, based on section types)
    console.log('Building connectivity metadata from graph...');
    const metadata = buildConnectivityMetadata(sectionIds, graph);
    console.log(`  Found ${metadata.rooms.size} rooms, ${metadata.aisles.size} aisles, ${metadata.crossings.size} crossings`);

    let totalSubsets = 0;
    let connectedSubsets = 0;
    let validSubsets = 0;
    let invalidByRules = 0;

    for (const subset of generateSubsets(sectionIds)) {
        totalSubsets++;
        if (isConnected(subset, graph)) {
            connectedSubsets++;
            // Apply generic connectivity rules (no hardcoded section IDs)
            if (isValidCombination(subset, graph, metadata)) {
                validCombinations.push(subset);
                validSubsets++;
            } else {
                invalidByRules++;
            }
        }

        // Progress logging every 1000 subsets
        if (totalSubsets % 1000 === 0) {
            console.log(`  Checked ${totalSubsets} subsets, ${connectedSubsets} connected, ${validSubsets} valid, ${invalidByRules} filtered...`);
        }
    }

    console.log(`Total subsets checked: ${totalSubsets}`);
    console.log(`Connected combinations: ${connectedSubsets}`);
    console.log(`Invalid by generic rules: ${invalidByRules}`);
    console.log(`Valid combinations after filtering: ${validSubsets}`);

    return validCombinations;
}

/**
 * Create a unique key for a combination of sections
 */
function createCombinationKey(sectionIds) {
    return sectionIds.slice().sort().join('_');
}

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('INSCRIBED RECTANGLE PRE-COMPUTATION');
    console.log('='.repeat(80));
    console.log();

    // Step 1: Load Rooms.json
    console.log('Step 1: Loading Rooms.json...');
    let roomsContent = fs.readFileSync(roomsJsonPath, 'utf8');
    // Remove BOM if present
    if (roomsContent.charCodeAt(0) === 0xFEFF) {
        roomsContent = roomsContent.substring(1);
    }
    const roomsData = JSON.parse(roomsContent);
    const level1 = roomsData.find(level => level.Id === 'Level1');
    if (!level1) {
        throw new Error('Level1 not found in Rooms.json');
    }

    const ballroom = level1.Rooms.find(room => room.Id === 'Ballroom');
    if (!ballroom) {
        throw new Error('Ballroom not found in Level1');
    }

    const sectionIds = ballroom.RoomSections.map(section => section.Id);
    console.log(`  Found ${sectionIds.length} ballroom sections`);
    console.log(`  Sections: ${sectionIds.join(', ')}`);
    console.log();

    // Step 2: Build adjacency graph
    console.log('Step 2: Building adjacency graph from Joins...');
    const graph = buildAdjacencyGraph(ballroom.Joins);
    console.log(`  Graph has ${graph.size} nodes`);
    console.log();

    // Print the graph for verification
    console.log('Adjacency Graph:');
    for (const [node, neighbors] of graph.entries()) {
        console.log(`  ${node} -> [${Array.from(neighbors).join(', ')}]`);
    }
    console.log();

    // Step 3: Generate all valid connected combinations
    console.log('Step 3: Generating all valid connected combinations...');
    const validCombinations = generateConnectedCombinations(sectionIds, graph);
    console.log();

    // Step 4: Display statistics
    console.log('Step 4: Statistics');
    console.log('='.repeat(80));

    // Group by size
    const bySize = new Map();
    for (const combo of validCombinations) {
        const size = combo.length;
        if (!bySize.has(size)) {
            bySize.set(size, []);
        }
        bySize.get(size).push(combo);
    }

    console.log('Valid combinations by size:');
    let totalCombinations = 0;
    for (let size = 1; size <= sectionIds.length; size++) {
        const combos = bySize.get(size) || [];
        totalCombinations += combos.length;
        console.log(`  Size ${size.toString().padStart(2)}: ${combos.length.toString().padStart(4)} combinations`);
    }
    console.log(`  Total: ${totalCombinations} combinations`);
    console.log();

    // Step 5: Estimate computation time
    console.log('Step 5: Time Estimation');
    console.log('='.repeat(80));
    const avgTimePerComboSec = 1.0; // Assume 1 second per combination with accurate algorithm
    const totalTimeSec = totalCombinations * avgTimePerComboSec;
    const totalTimeMin = totalTimeSec / 60;

    console.log(`  Assuming ${avgTimePerComboSec}s per combination (accurate algorithm):`);
    console.log(`  Total computation time: ~${totalTimeSec.toFixed(0)}s (~${totalTimeMin.toFixed(1)} minutes)`);
    console.log();

    // Step 6: Save combinations list to file for review
    console.log('Step 6: Saving combinations to file...');
    const outputPath = path.join(__dirname, 'valid-combinations.json');
    const output = {
        generatedAt: new Date().toISOString(),
        totalCombinations: totalCombinations,
        sectionIds: sectionIds,
        combinations: validCombinations.map(combo => ({
            key: createCombinationKey(combo),
            sections: combo,
            size: combo.length
        }))
    };

    fs.writeFileSync(outputPath, JSON.stringify(output, null, 2));
    console.log(`  Saved to: ${outputPath}`);
    console.log();

    console.log('='.repeat(80));
    console.log('NEXT STEPS:');
    console.log('  1. Review valid-combinations.json to verify combinations');
    console.log('  2. Run computation phase to calculate inscribed rectangles');
    console.log('  3. Store results in Level1.svg for runtime lookup');
    console.log('='.repeat(80));
}

main().catch(console.error);
