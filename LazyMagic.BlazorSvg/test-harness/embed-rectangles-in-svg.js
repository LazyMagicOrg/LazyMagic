// Embed precomputed rectangles JSON directly into SVG files
// This eliminates the need for a separate JSON HTTP request at runtime

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Configuration
const precomputedJsonPath = path.join(__dirname, '..', '..', 'BlazorTest.WASM', 'wwwroot', 'precomputed-rectangles.json');
const svgInputDir = path.join(__dirname, '..', '..', 'BlazorTest.WASM', 'wwwroot');
const svgOutputDir = svgInputDir; // Overwrite in place, or change to create new files

// SVG files to process
const svgFiles = [
    'Level1.svg'
    // 'Level1-normal.svg',
    // 'Level1-rotated.svg',
    // 'Level2.svg'
];

/**
 * Embed precomputed rectangles JSON into SVG <defs> section
 * @param {string} svgContent - Original SVG content
 * @param {object} precomputedData - Precomputed rectangles JSON object
 * @returns {string} Modified SVG content with embedded data
 */
function embedRectanglesInSvg(svgContent, precomputedData) {
    // Convert precomputed data to compact JSON string
    const jsonString = JSON.stringify(precomputedData);

    // Create the script element to embed
    const scriptElement = `
  <script type="application/json" id="precomputed-rectangles"><![CDATA[
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
 * Remove existing embedded precomputed rectangles from SVG
 * @param {string} svgContent - SVG content
 * @returns {string} SVG content with embedded data removed
 */
function removeEmbeddedRectangles(svgContent) {
    // Remove <script id="precomputed-rectangles">...</script>
    return svgContent.replace(
        /<script\s+type="application\/json"\s+id="precomputed-rectangles">[\s\S]*?<\/script>/gi,
        ''
    );
}

/**
 * Main function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('EMBEDDING PRECOMPUTED RECTANGLES IN SVG FILES');
    console.log('='.repeat(80));
    console.log();

    // Step 1: Load precomputed rectangles JSON
    console.log('Step 1: Loading precomputed rectangles...');
    if (!fs.existsSync(precomputedJsonPath)) {
        console.error(`  ✗ Error: precomputed-rectangles.json not found at ${precomputedJsonPath}`);
        console.error(`  Please run extract-precomputed-rectangles.js first to generate this file.`);
        process.exit(1);
    }

    const precomputedData = JSON.parse(fs.readFileSync(precomputedJsonPath, 'utf8'));
    const jsonSize = Buffer.byteLength(JSON.stringify(precomputedData), 'utf8');

    console.log(`  ✓ Loaded precomputed rectangles`);
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

        // Remove any existing embedded data
        svgContent = removeEmbeddedRectangles(svgContent);

        // Embed the precomputed data
        svgContent = embedRectanglesInSvg(svgContent, precomputedData);

        // Write modified SVG
        fs.writeFileSync(outputPath, svgContent, 'utf8');
        const newSize = Buffer.byteLength(svgContent, 'utf8');

        console.log(`  ✓ Embedded data in ${svgFile}`);
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
    console.log(`Successfully embedded precomputed rectangles in ${successCount} SVG file(s)`);
    console.log();
    console.log('Next steps:');
    console.log('  1. Test the SVG files in your application');
    console.log('  2. Verify that rectangles are loaded from embedded data');
    console.log('  3. Remove or keep external precomputed-rectangles.json as fallback');
    console.log('='.repeat(80));
}

main().catch(error => {
    console.error('Error:', error.message);
    process.exit(1);
});
