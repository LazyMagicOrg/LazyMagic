// Embed all precomputed layout data (rectangles, boardroom, hollowsquare) directly into SVG files
// This eliminates the need for separate JSON HTTP requests at runtime

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Configuration
const precomputedDir = __dirname;
const svgInputDir = path.join(__dirname, '..', '..', 'BlazorTest.WASM', 'wwwroot');
const svgOutputDir = svgInputDir; // Overwrite in place

// SVG files to process
// Note: Level1-normal.svg is excluded as it's a control file
const svgFiles = [
    'Level1.svg'
];

// Precomputed data files to embed
const dataFiles = [
    {
        path: path.join(precomputedDir, 'precomputed-rectangles.json'),
        id: 'precomputed-rectangles',
        name: 'MaxInscribed Rectangles'
    },
    {
        path: path.join(precomputedDir, 'precomputed-boardroom.json'),
        id: 'precomputed-boardroom',
        name: 'Boardroom Layouts'
    },
    {
        path: path.join(precomputedDir, 'precomputed-hollowsquare.json'),
        id: 'precomputed-hollowsquare',
        name: 'Hollow Square Layouts'
    }
];

/**
 * Remove existing embedded data from SVG
 * @param {string} svgContent - SVG content
 * @param {string} id - Script element ID to remove
 * @returns {string} SVG content with embedded data removed
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
 * @param {string} svgContent - Original SVG content
 * @param {object} precomputedData - Precomputed data JSON object
 * @param {string} id - Script element ID
 * @returns {string} Modified SVG content with embedded data
 */
function embedDataInSvg(svgContent, precomputedData, id) {
    // Convert precomputed data to compact JSON string
    const jsonString = JSON.stringify(precomputedData);

    // Create the script element to embed
    const scriptElement = `
  <script type="application/json" id="${id}"><![CDATA[
${jsonString}
  ]]></script>`;

    // Find the <defs> tag and insert our script at the beginning
    const defsMatch = svgContent.match(/(<defs[^>]*>)/i);

    if (defsMatch) {
        // Insert after the opening <defs> tag
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

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('EMBEDDING ALL PRECOMPUTED LAYOUT DATA IN SVG FILES');
    console.log('='.repeat(80));
    console.log();

    // Step 1: Load all precomputed data files
    console.log('Step 1: Loading precomputed data files...');
    const loadedData = [];
    let totalDataSize = 0;

    for (const dataFile of dataFiles) {
        if (!fs.existsSync(dataFile.path)) {
            console.error(`  ✗ Error: ${path.basename(dataFile.path)} not found`);
            console.error(`  Please run extract-all-precomputed.js first to generate this file.`);
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

        console.log(`  ✓ Loaded ${dataFile.name}`);
        console.log(`    - Total combinations: ${data.totalCombinations}`);
        console.log(`    - Successful computations: ${data.successfulComputations}`);
        console.log(`    - JSON size: ${(jsonSize / 1024).toFixed(1)} KB`);
    }

    console.log();
    console.log(`  Total data size: ${(totalDataSize / 1024).toFixed(1)} KB`);
    console.log();

    // Step 2: Process each SVG file
    console.log('Step 2: Processing SVG files...');
    let successCount = 0;
    let skipCount = 0;

    for (const svgFile of svgFiles) {
        const inputPath = path.join(svgInputDir, svgFile);
        const outputPath = path.join(svgOutputDir, svgFile);

        // Check if file exists
        if (!fs.existsSync(inputPath)) {
            console.log(`  ⊘ Skipping ${svgFile} (not found)`);
            skipCount++;
            continue;
        }

        // Read original SVG
        let svgContent = fs.readFileSync(inputPath, 'utf8');
        const originalSize = Buffer.byteLength(svgContent, 'utf8');

        // Remove any existing embedded data
        for (const dataFile of loadedData) {
            svgContent = removeEmbeddedData(svgContent, dataFile.id);
        }

        // Embed all precomputed data files
        for (const dataFile of loadedData) {
            svgContent = embedDataInSvg(svgContent, dataFile.data, dataFile.id);
        }

        // Write modified SVG
        fs.writeFileSync(outputPath, svgContent, 'utf8');
        const newSize = Buffer.byteLength(svgContent, 'utf8');

        console.log(`  ✓ Embedded all data in ${svgFile}`);
        console.log(`    - Original size: ${(originalSize / 1024).toFixed(1)} KB`);
        console.log(`    - New size: ${(newSize / 1024).toFixed(1)} KB (+${((newSize - originalSize) / 1024).toFixed(1)} KB)`);

        successCount++;
    }

    console.log();
    console.log(`  Processed: ${successCount} files`);
    if (skipCount > 0) {
        console.log(`  Skipped: ${skipCount} files`);
    }
    console.log();

    // Step 3: Summary
    console.log('='.repeat(80));
    console.log('EMBEDDING COMPLETE!');
    console.log(`Successfully embedded all precomputed data in ${successCount} SVG file(s)`);
    console.log();
    console.log('Embedded datasets:');
    for (const dataFile of loadedData) {
        console.log(`  - ${dataFile.name} (${(dataFile.size / 1024).toFixed(1)} KB)`);
    }
    console.log();
    console.log('Next steps:');
    console.log('  1. Test the SVG files in your application');
    console.log('  2. Verify that layouts are loaded from embedded data');
    console.log('  3. External JSON files can be kept as fallback or removed');
    console.log('='.repeat(80));
}

main().catch(error => {
    console.error('Error:', error.message);
    process.exit(1);
});
