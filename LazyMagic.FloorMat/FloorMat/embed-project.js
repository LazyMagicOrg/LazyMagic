// Embed precomputed data into SVG for a specific project
// Creates a new SVG file with embedded data, preserving the original

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Get arguments: prefix, svgPath, outputDir
const args = process.argv.slice(2);

if (args.length < 3) {
    console.error('Usage: node embed-project.js <prefix> <svgPath> <outputDir>');
    process.exit(1);
}

const [prefix, svgPath, projectOutputDir] = args;

console.log(`Embedding data for project: ${prefix}`);
console.log(`  Source SVG: ${svgPath}`);
console.log(`  Output Dir: ${projectOutputDir}`);
console.log();

// Paths to precomputed data files
const dataFiles = [
    {
        path: path.join(projectOutputDir, `${prefix}-rectangles.json`),
        id: 'precomputed-rectangles',
        name: 'MaxInscribed Rectangles'
    },
    {
        path: path.join(projectOutputDir, `${prefix}-boardroom.json`),
        id: 'precomputed-boardroom',
        name: 'Boardroom Layouts'
    },
    {
        path: path.join(projectOutputDir, `${prefix}-hollowsquare.json`),
        id: 'precomputed-hollowsquare',
        name: 'Hollow Square Layouts'
    }
];

/**
 * Remove existing embedded data from SVG
 */
function removeEmbeddedData(svgContent, id) {
    const regex = new RegExp(
        `<script\\s+type="application\\/json"\\s+id="${id}">\\s*<!\\[CDATA\\[[\\s\\S]*?\\]\\]>\\s*<\\/script>`,
        'gi'
    );
    return svgContent.replace(regex, '');
}

/**
 * Embed precomputed data JSON into SVG <defs> section
 */
function embedDataInSvg(svgContent, precomputedData, id) {
    const jsonString = JSON.stringify(precomputedData);

    const scriptElement = `
  <script type="application/json" id="${id}"><![CDATA[
${jsonString}
  ]]></script>`;

    // Find the <defs> tag and insert our script at the beginning
    const defsMatch = svgContent.match(/(<defs[^>]*>)/i);

    if (defsMatch) {
        const defsTag = defsMatch[1];
        const insertPos = svgContent.indexOf(defsTag) + defsTag.length;
        return svgContent.slice(0, insertPos) + scriptElement + svgContent.slice(insertPos);
    } else {
        // No <defs> section found, create one after the opening <svg> tag
        const svgMatch = svgContent.match(/(<svg[^>]*>)/i);
        if (svgMatch) {
            const svgTag = svgMatch[1];
            const insertPos = svgContent.indexOf(svgTag) + svgTag.length;
            return svgContent.slice(0, insertPos) + `
  <defs>${scriptElement}
  </defs>` + svgContent.slice(insertPos);
        } else {
            throw new Error('Could not find <svg> tag in SVG content');
        }
    }
}

// Load all precomputed data files
console.log('Loading precomputed data files...');
const loadedData = [];
let totalDataSize = 0;

for (const dataFile of dataFiles) {
    if (!fs.existsSync(dataFile.path)) {
        console.error(`  ✗ Error: ${path.basename(dataFile.path)} not found`);
        process.exit(1);
    }

    const data = JSON.parse(fs.readFileSync(dataFile.path, 'utf8'));
    const jsonSize = Buffer.byteLength(JSON.stringify(data), 'utf8');
    totalDataSize += jsonSize;

    loadedData.push({
        ...dataFile,
        data,
        size: jsonSize
    });

    console.log(`  ✓ ${dataFile.name} (${(jsonSize / 1024).toFixed(1)} KB)`);
}

console.log(`  Total: ${(totalDataSize / 1024).toFixed(1)} KB`);
console.log();

// Read original SVG
console.log('Processing SVG...');
let svgContent = fs.readFileSync(svgPath, 'utf8');
const originalSize = Buffer.byteLength(svgContent, 'utf8');

// Remove any existing embedded data
for (const dataFile of loadedData) {
    svgContent = removeEmbeddedData(svgContent, dataFile.id);
}

// Embed all precomputed data files
for (const dataFile of loadedData) {
    svgContent = embedDataInSvg(svgContent, dataFile.data, dataFile.id);
}

// Write modified SVG to output directory
const outputSvgPath = path.join(projectOutputDir, `${prefix}-output.svg`);
fs.writeFileSync(outputSvgPath, svgContent, 'utf8');
const newSize = Buffer.byteLength(svgContent, 'utf8');

console.log(`  ✓ Embedded data in ${prefix}-output.svg`);
console.log(`    Original size: ${(originalSize / 1024).toFixed(1)} KB`);
console.log(`    New size: ${(newSize / 1024).toFixed(1)} KB (+${((newSize - originalSize) / 1024).toFixed(1)} KB)`);
console.log();
console.log(`Output SVG: ${outputSvgPath}`);
