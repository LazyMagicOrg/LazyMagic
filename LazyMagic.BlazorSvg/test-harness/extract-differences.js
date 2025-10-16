import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const normalDir = path.resolve(__dirname, '../TestResultsNormal');
const rotatedDir = path.resolve(__dirname, '../TestResultsRotated');

console.log('\n==========================================================================');
console.log('EXTRACTING TEST CASES WITH DIFFERENCES');
console.log('==========================================================================\n');

const normalFiles = fs.readdirSync(normalDir).filter(f => f.endsWith('.svg'));
const rotatedFiles = fs.readdirSync(rotatedDir).filter(f => f.endsWith('.svg'));

const differences = [];

for (const file of normalFiles) {
    if (!rotatedFiles.includes(file)) {
        continue;
    }

    const normalPath = path.join(normalDir, file);
    const rotatedPath = path.join(rotatedDir, file);

    const normalSvg = fs.readFileSync(normalPath, 'utf8');
    const rotatedSvg = fs.readFileSync(rotatedPath, 'utf8');

    // Extract boundary-based area from each SVG
    const normalAreaMatch = normalSvg.match(/<!-- Boundary-Based Row -->[\s\S]*?<text[^>]*>(\d+\.?\d*)</);
    const rotatedAreaMatch = rotatedSvg.match(/<!-- Boundary-Based Row -->[\s\S]*?<text[^>]*>(\d+\.?\d*)</);

    if (normalAreaMatch && rotatedAreaMatch) {
        const normalArea = parseFloat(normalAreaMatch[1]);
        const rotatedArea = parseFloat(rotatedAreaMatch[1]);

        const diff = Math.abs(normalArea - rotatedArea);

        // Include all differences >= 0.1 sq px
        if (diff >= 0.1) {
            // Extract combo number from filename (e.g., "Combo_0152.svg" -> 152)
            const comboMatch = file.match(/Combo_(\d+)\.svg/);
            if (comboMatch) {
                const comboNum = parseInt(comboMatch[1]);
                differences.push({
                    comboNum,
                    file,
                    normalArea,
                    rotatedArea,
                    diff
                });
            }
        }
    }
}

// Sort by difference descending to get the largest differences first
differences.sort((a, b) => b.diff - a.diff);

console.log(`Found ${differences.length} test cases with differences >= 0.1 sq px\n`);

// Take only the top 50 largest differences
const top50 = differences.slice(0, 50);

console.log(`Selecting top 50 largest differences:\n`);
for (let i = 0; i < Math.min(10, top50.length); i++) {
    const d = top50[i];
    console.log(`  ${i + 1}. ${d.file}: ${d.diff.toFixed(1)} sq px (${(d.diff / d.normalArea * 100).toFixed(2)}%)`);
}
if (top50.length > 10) {
    console.log(`  ... and ${top50.length - 10} more\n`);
}

// Read the full test config to get the combo details
const fullConfigPath = path.resolve(__dirname, 'valid-combinations.json');
const fullConfig = JSON.parse(fs.readFileSync(fullConfigPath, 'utf8'));

// Create new sample configs with only the top 50 differences
const sampleCombinations = [];

for (const diff of top50) {
    // Find the matching combination in the full config
    const combo = fullConfig.combinations[diff.comboNum - 1]; // Combo numbers are 1-indexed
    if (combo) {
        sampleCombinations.push(combo);
    }
}

// Create the sample configs
const sampleConfig = {
    generatedAt: new Date().toISOString(),
    totalCombinations: sampleCombinations.length,
    sectionIds: fullConfig.sectionIds,
    combinations: sampleCombinations
};

// Write normal sample
const normalSamplePath = path.resolve(__dirname, 'valid-combinations-sample-normal.json');
fs.writeFileSync(normalSamplePath, JSON.stringify(sampleConfig, null, 2));
console.log(`✓ Created ${normalSamplePath}`);
console.log(`  Contains ${sampleCombinations.length} combinations with differences\n`);

// Write rotated sample (same content)
const rotatedSamplePath = path.resolve(__dirname, 'valid-combinations-sample-rotated.json');
fs.writeFileSync(rotatedSamplePath, JSON.stringify(sampleConfig, null, 2));
console.log(`✓ Created ${rotatedSamplePath}`);
console.log(`  Contains ${sampleCombinations.length} combinations with differences\n`);

// Print summary
console.log('==========================================================================');
console.log('SUMMARY');
console.log('==========================================================================');
console.log(`Sample files now contain top 50 test cases with largest differences`);
console.log(`Total combinations: ${sampleCombinations.length}`);
console.log(`\nRun these commands to test:`);
console.log(`  node compute-sample-normal.js`);
console.log(`  node compute-sample-rotated.js\n`);
