import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Read and parse results files
function parseResults(filePath) {
    const content = fs.readFileSync(filePath, 'utf8');
    const results = new Map();

    // Find the ALGORITHM COMPARISON RESULTS section
    const lines = content.split('\n');
    let inAlgoSection = false;

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        if (line.includes('ALGORITHM COMPARISON RESULTS')) {
            inAlgoSection = true;
            continue;
        }

        if (inAlgoSection && line.includes('ALL TEST RESULTS')) {
            break;
        }

        if (inAlgoSection && line.trim().startsWith('Combo_')) {
            // Parse line like: Combo_0001 |   18992.4 |    20.1 ms |   18992.4 |   1267.5 ms | BB     |   0.0%
            const parts = line.split('|').map(p => p.trim());
            if (parts.length >= 7) {
                const name = parts[0];
                const bbArea = parts[1] === 'FAIL' ? null : parseFloat(parts[1]);
                const optArea = parts[3] === 'FAIL' ? null : parseFloat(parts[3]);

                results.set(name, {
                    bbArea,
                    optArea
                });
            }
        }
    }

    return results;
}

// Main analysis
const normalPath = path.join(__dirname, '..', 'TestResultsNormal', 'results.txt');
const rotatedPath = path.join(__dirname, '..', 'TestResultsRotated', 'results.txt');

console.log('Analyzing test results for discrepancies...\n');

const normalResults = parseResults(normalPath);
const rotatedResults = parseResults(rotatedPath);

const discrepancies = [];

// Compare each combo
for (const [name, normalData] of normalResults) {
    const rotatedData = rotatedResults.get(name);
    if (!rotatedData) continue;

    let bbDiff = 0;
    let optDiff = 0;

    if (normalData.bbArea !== null && rotatedData.bbArea !== null) {
        bbDiff = Math.abs(normalData.bbArea - rotatedData.bbArea);
    }

    if (normalData.optArea !== null && rotatedData.optArea !== null) {
        optDiff = Math.abs(normalData.optArea - rotatedData.optArea);
    }

    // Check if either difference exceeds threshold
    if (bbDiff >= 1000 || optDiff >= 1000) {
        discrepancies.push({
            name,
            bbDiff,
            optDiff,
            totalDiff: bbDiff + optDiff,
            normalBB: normalData.bbArea,
            rotatedBB: rotatedData.bbArea,
            normalOpt: normalData.optArea,
            rotatedOpt: rotatedData.optArea
        });
    }
}

// Sort by total difference (highest first)
discrepancies.sort((a, b) => b.totalDiff - a.totalDiff);

console.log(`Found ${discrepancies.length} combos with ≥1000 sq px discrepancy:\n`);
console.log('Combo          | BB Diff  | Opt Diff | Total Diff | Normal BB | Rotated BB | Normal Opt | Rotated Opt');
console.log('---------------|----------|----------|------------|-----------|------------|------------|------------');

for (const d of discrepancies) {
    console.log(
        `${d.name.padEnd(14)} | ${d.bbDiff.toFixed(1).padStart(8)} | ${d.optDiff.toFixed(1).padStart(8)} | ${d.totalDiff.toFixed(1).padStart(10)} | ${(d.normalBB || 'FAIL').toString().padStart(9)} | ${(d.rotatedBB || 'FAIL').toString().padStart(10)} | ${(d.normalOpt || 'FAIL').toString().padStart(10)} | ${(d.rotatedOpt || 'FAIL').toString().padStart(11)}`
    );
}

console.log('\nCombo names for config update:');
console.log(discrepancies.map(d => `"${d.name}"`).join(', '));
