/**
 * Creates a sample test configuration for hollow square tests
 * Extracts specific combinations by their index numbers
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Read the full valid combinations file
const validCombinationsPath = path.join(__dirname, 'valid-combinations.json');
const data = fs.readFileSync(validCombinationsPath, 'utf8');
const fullData = JSON.parse(data);

// Combo numbers requested by user (1-indexed)
const requestedCombos = [4, 7, 8, 29, 32, 33, 34, 49, 66, 112, 130, 179, 194, 217, 236, 251];

// Extract the requested combinations (convert to 0-indexed)
const sampleCombinations = requestedCombos.map(num => {
    const index = num - 1; // Convert to 0-index
    if (index < 0 || index >= fullData.combinations.length) {
        console.error(`⚠️  Warning: Combo ${num} is out of range (1-${fullData.combinations.length})`);
        return null;
    }
    return fullData.combinations[index];
}).filter(c => c !== null); // Remove any invalid entries

// Create sample data structure
const sampleData = {
    generatedAt: new Date().toISOString(),
    totalCombinations: sampleCombinations.length,
    note: `Sample test set with combinations: ${requestedCombos.join(', ')}`,
    sectionIds: fullData.sectionIds,
    combinations: sampleCombinations
};

// Write the sample file
const outputPath = path.join(__dirname, 'valid-combinations-hollowsquare-sample.json');
fs.writeFileSync(outputPath, JSON.stringify(sampleData, null, 2), 'utf8');

console.log(`✓ Created sample configuration with ${sampleCombinations.length} combinations`);
console.log(`✓ Saved to: ${outputPath}`);
console.log(`\nIncluded combos: ${requestedCombos.join(', ')}`);
