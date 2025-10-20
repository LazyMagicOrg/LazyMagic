// Embed precomputed boardroom layouts JSON directly into SVG files
// This eliminates the need for a separate JSON HTTP request at runtime
// Works alongside the existing precomputed-rectangles embedding

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Configuration
const precomputedJsonPath = path.join(__dirname, 'precomputed-boardroom.json');
const svgInputDir = path.join(__dirname, '..', '..', 'BlazorTest.WASM', 'wwwroot');
const svgOutputDir = svgInputDir; // Overwrite in place

// SVG files to process
const svgFiles = [
    'Level1.svg'
    // 'Level1-normal.svg',
    // 'Level1-rotated.svg',
    // 'Level2.svg'
];

/**
 * Embed precomputed boardroom layouts JSON into SVG <defs> section
 * @param {string} svgContent - Original SVG content
 * @param {object} precomputedData - Precomputed boardroom layouts JSON object
 * @returns {string} Modified SVG content with embedded data
 */
function embedBoardroomInSvg(svgContent, precomputedData) {
    // Convert precomputed data to compact JSON string
    const jsonString = JSON.stringify(precomputedData);

    // Create the script element to embed
    const scriptElement = `
  <script type="application/json" id="precomputed-boardroom"><![CDATA[
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
 * Remove existing embedded boardroom data from SVG
 * @param {string} svgContent - SVG content
 * @returns {string} SVG content with embedded boardroom data removed
 */
function removeEmbeddedBoardroom(svgContent) {
    // Remove <script id="precomputed-boardroom">...</script>
    return svgContent.replace(
        /<script\s+type="application\/json"\s+id="precomputed-boardroom">[\s\S]*?<\/script>/gi,
        ''
    );
}

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('EMBEDDING PRECOMPUTED BOARDROOM LAYOUTS IN SVG FILES');
    console.log('='.repeat(80));
    console.log();

    // Step 1: Load precomputed boardroom layouts JSON
    console.log('Step 1: Loading precomputed boardroom layouts...');
    if (!fs.existsSync(precomputedJsonPath)) {
        console.error(`  ✗ Error: precomputed-boardroom.json not found at ${precomputedJsonPath}`);
        console.error(`  Please run extract-precomputed-boardroom.js first to generate this file.`);
        process.exit(1);
    }

    const precomputedData = JSON.parse(fs.readFileSync(precomputedJsonPath, 'utf8'));
    const jsonSize = Buffer.byteLength(JSON.stringify(precomputedData), 'utf8');

    console.log(`  ✓ Loaded precomputed boardroom layouts`);
    console.log(`    - Total combinations: ${precomputedData.totalCombinations}`);
    console.log(`    - Successful computations: ${precomputedData.successfulComputations}`);
    console.log(`    - JSON size: ${(jsonSize / 1024).toFixed(1)} KB`);
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

        // Remove any existing embedded boardroom data
        svgContent = removeEmbeddedBoardroom(svgContent);

        // Embed the precomputed boardroom data
        svgContent = embedBoardroomInSvg(svgContent, precomputedData);

        // Write modified SVG
        fs.writeFileSync(outputPath, svgContent, 'utf8');
        const newSize = Buffer.byteLength(svgContent, 'utf8');

        console.log(`  ✓ Embedded boardroom data in ${svgFile}`);
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
    console.log(`Successfully embedded precomputed boardroom layouts in ${successCount} SVG file(s)`);
    console.log();
    console.log('Next steps:');
    console.log('  1. Update JavaScript to load boardroom data from embedded <script> element');
    console.log('  2. Test the SVG files in your application');
    console.log('  3. Verify that boardroom layouts are loaded from embedded data');
    console.log();
    console.log('The SVG now contains TWO embedded datasets:');
    console.log('  - <script id="precomputed-rectangles"> (largest rectangles)');
    console.log('  - <script id="precomputed-boardroom"> (boardroom layouts)');
    console.log('='.repeat(80));
}

main().catch(error => {
    console.error('Error:', error.message);
    process.exit(1);
});
