import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const normalDir = path.resolve(__dirname, '../TestResultsNormal');
const rotatedDir = path.resolve(__dirname, '../TestResultsRotated');

console.log('\n==========================================================================');
console.log('COMPARING NORMAL VS ROTATED TEST RESULTS');
console.log('==========================================================================\n');

const normalFiles = fs.readdirSync(normalDir).filter(f => f.endsWith('.svg'));
const rotatedFiles = fs.readdirSync(rotatedDir).filter(f => f.endsWith('.svg'));

console.log(`Found ${normalFiles.length} normal test results`);
console.log(`Found ${rotatedFiles.length} rotated test results\n`);

let totalTests = 0;
let matching = 0;
let different = 0;
const differences = [];

for (const file of normalFiles) {
    if (!rotatedFiles.includes(file)) {
        console.log(`⚠️  ${file} not found in rotated results`);
        continue;
    }

    const normalPath = path.join(normalDir, file);
    const rotatedPath = path.join(rotatedDir, file);

    const normalSvg = fs.readFileSync(normalPath, 'utf8');
    const rotatedSvg = fs.readFileSync(rotatedPath, 'utf8');

    // Extract boundary-based area from each SVG
    const normalAreaMatch = normalSvg.match(/<!-- Boundary-Based Row -->[\s\S]*?<text[^>]*>(\d+\.?\d*)</);
    const rotatedAreaMatch = rotatedSvg.match(/<!-- Boundary-Based Row -->[\s\S]*?<text[^>]*>(\d+\.?\d*)</);

    // Extract vertex counts - find the Boundary Vertices section and count circles
    const normalVertexSection = normalSvg.match(/<!-- Boundary Vertices -->([\s\S]*?)<!-- /);
    const rotatedVertexSection = rotatedSvg.match(/<!-- Boundary Vertices -->([\s\S]*?)<!-- /);

    const normalVertexCount = normalVertexSection ? (normalVertexSection[1].match(/<circle/g) || []).length : 0;
    const rotatedVertexCount = rotatedVertexSection ? (rotatedVertexSection[1].match(/<circle/g) || []).length : 0;

    if (normalAreaMatch && rotatedAreaMatch) {
        const normalArea = parseFloat(normalAreaMatch[1]);
        const rotatedArea = parseFloat(rotatedAreaMatch[1]);

        totalTests++;

        if (Math.abs(normalArea - rotatedArea) < 0.1) {
            matching++;
        } else {
            different++;
            const diff = Math.abs(normalArea - rotatedArea);
            const percentDiff = (diff / normalArea * 100).toFixed(2);
            differences.push({
                file,
                normalArea,
                rotatedArea,
                diff,
                percentDiff,
                normalVertexCount,
                rotatedVertexCount
            });
        }
    }
}

console.log('==========================================================================');
console.log('SUMMARY');
console.log('==========================================================================');
console.log(`Total tests compared: ${totalTests}`);
console.log(`✓ Matching areas: ${matching} (${(matching / totalTests * 100).toFixed(1)}%)`);
console.log(`✗ Different areas: ${different} (${(different / totalTests * 100).toFixed(1)}%)`);

if (differences.length > 0) {
    console.log('\n==========================================================================');
    console.log('DIFFERENCES FOUND');
    console.log('==========================================================================\n');

    // Sort by percent difference descending
    differences.sort((a, b) => parseFloat(b.percentDiff) - parseFloat(a.percentDiff));

    for (const d of differences) {
        console.log(`${d.file}:`);
        console.log(`  Normal:  ${d.normalArea.toFixed(1)} sq px (${d.normalVertexCount} vertices)`);
        console.log(`  Rotated: ${d.rotatedArea.toFixed(1)} sq px (${d.rotatedVertexCount} vertices)`);
        console.log(`  Diff:    ${d.diff.toFixed(1)} sq px (${d.percentDiff}%)\n`);
    }
} else {
    console.log('\n🎉 All test results match perfectly!\n');
}
