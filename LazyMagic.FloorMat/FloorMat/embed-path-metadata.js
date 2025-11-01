#!/usr/bin/env node

/**
 * embed-path-metadata.js
 *
 * Embeds room section metadata from level data JSON into SVG path elements as custom floormat:* attributes.
 *
 * Usage: node embed-path-metadata.js <prefix> <svgPath> <dataJsonPath> <outputSvgPath>
 *
 * Example:
 *   node embed-path-metadata.js "Level1" "./input/Level1.svg" "./input/Level1-data.json" "./output/Level1-with-metadata.svg"
 */

import fs from 'fs';
import path from 'path';
import { JSDOM } from 'jsdom';
import XmlBeautify from 'xml-beautify';
import { DOMParser } from 'xmldom';

const FLOORMAT_NAMESPACE = 'http://lazymagic.com/floormat';

/**
 * Main function to embed path metadata
 */
function embedPathMetadata(prefix, svgPath, dataJsonPath, outputSvgPath) {
    console.log(`\n[embed-path-metadata] Processing ${prefix}...`);
    console.log(`  SVG: ${svgPath}`);
    console.log(`  Data JSON: ${dataJsonPath}`);
    console.log(`  Output: ${outputSvgPath}`);

    // Load and parse level data JSON
    console.log('  Loading level data...');
    let dataContent = fs.readFileSync(dataJsonPath, 'utf8');
    // Remove BOM if present
    if (dataContent.charCodeAt(0) === 0xFEFF) {
        dataContent = dataContent.slice(1);
    }
    const levelData = JSON.parse(dataContent);

    // Validate level data structure
    if (!levelData || !levelData.Rooms) {
        throw new Error(`Invalid data structure: missing "Rooms" array in ${prefix}-data.json`);
    }

    // Collect all room sections with their metadata
    const sectionMetadata = new Map();

    // Properties that should not be embedded as floormat:* attributes
    // (they're either already in the SVG or used for other purposes)
    const reservedProperties = new Set([
        'Id',              // Already the path element's id attribute
        'RoomSections',    // Structural property, not section metadata
        'Joins'            // Structural property for graph connectivity
    ]);

    for (const room of levelData.Rooms) {
        if (!room.RoomSections) continue;

        for (const section of room.RoomSections) {
            // Start with core metadata
            const metadata = {
                sectionType: section.SectionType || 'Room', // Default to "Room"
                layoutRestriction: section.LayoutRestriction || 'allowed'
            };

            // Dynamically add all other properties from the section
            for (const [key, value] of Object.entries(section)) {
                if (!reservedProperties.has(key) && value !== undefined && value !== null && value !== '') {
                    // Convert property name to lowercase for consistency
                    const propName = key.charAt(0).toLowerCase() + key.slice(1);
                    metadata[propName] = value;
                }
            }

            sectionMetadata.set(section.Id, metadata);
        }
    }

    console.log(`  Found ${sectionMetadata.size} room sections`);

    // Parse SVG
    console.log('  Parsing SVG...');
    const svgContent = fs.readFileSync(svgPath, 'utf8');
    const dom = new JSDOM(svgContent, { contentType: 'image/svg+xml' });
    const document = dom.window.document;

    // Add floormat namespace to root <svg> element
    const svgElement = document.querySelector('svg');
    if (!svgElement) {
        throw new Error('No <svg> element found in SVG file');
    }

    svgElement.setAttribute('xmlns:floormat', FLOORMAT_NAMESPACE);
    console.log('  ✓ Added floormat namespace');

    // Process each section and add metadata attributes to corresponding paths
    let pathsProcessed = 0;
    let pathsNotFound = 0;

    for (const [sectionId, metadata] of sectionMetadata) {
        const pathElement = document.querySelector(`path[id="${sectionId}"]`);

        if (!pathElement) {
            console.warn(`  ⚠ Path not found for section: ${sectionId}`);
            pathsNotFound++;
            continue;
        }

        // Dynamically add all metadata properties as floormat:* attributes
        for (const [key, value] of Object.entries(metadata)) {
            if (value !== undefined && value !== null && value !== '') {
                // Convert camelCase to kebab-case for attribute names
                // e.g., sectionType → section-type, layoutRestriction → layout-restriction
                const attrName = key.replace(/([A-Z])/g, '-$1').toLowerCase();

                // Convert value to string (handles numbers, booleans, strings)
                const attrValue = typeof value === 'object' ? JSON.stringify(value) : String(value);

                pathElement.setAttribute(`floormat:${attrName}`, attrValue);
            }
        }

        pathsProcessed++;
    }

    console.log(`  ✓ Processed ${pathsProcessed} paths`);
    if (pathsNotFound > 0) {
        console.log(`  ⚠ ${pathsNotFound} paths not found in SVG`);
    }

    // Serialize back to string
    console.log('  Writing output SVG...');
    let modifiedSvg = dom.serialize();

    // Remove incorrect namespace prefixes added by jsdom
    modifiedSvg = modifiedSvg.replace(/<svg:svg /g, '<svg ');
    modifiedSvg = modifiedSvg.replace(/<\/svg:svg>/g, '</svg>');
    modifiedSvg = modifiedSvg.replace(/<svg:([a-z])/g, '<$1'); // Remove svg: prefix from other elements
    modifiedSvg = modifiedSvg.replace(/<\/svg:([a-z])/g, '</$1');

    // Format the SVG for readability using xml-beautify
    console.log('  Formatting SVG...');
    try {
        const beautifier = new XmlBeautify({ parser: DOMParser });
        modifiedSvg = beautifier.beautify(modifiedSvg, {
            indent: '  ',  // 2 spaces
            useSelfClosingElement: true
        });

        // Post-process: put namespace-prefixed attributes (*:attr) on separate lines
        // Find the base indentation of each element and add 4 spaces for attributes
        modifiedSvg = modifiedSvg.replace(/^(\s*)(<\w+(?::\w+)?)\s+(.+?)([/>])$/gm, (match, indent, tag, attrs, close) => {
            // Only process if there are namespace-prefixed attributes
            if (!attrs.includes(':')) {
                return match;
            }

            // Split attributes and format namespace-prefixed ones on new lines
            const attrIndent = indent + '    ';  // base indent + 4 spaces
            const formattedAttrs = attrs.replace(/\s+(\w+:\S+="[^"]*")/g, `\n${attrIndent}$1`);

            return `${indent}${tag} ${formattedAttrs.trimStart()}${close}`;
        });
    } catch (err) {
        console.warn('  Warning: XML formatting encountered an issue:', err.message);
    }

    // Ensure output directory exists
    const outputDir = path.dirname(outputSvgPath);
    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    fs.writeFileSync(outputSvgPath, modifiedSvg, 'utf8');
    console.log(`  ✓ Metadata embedded successfully`);
    console.log(`  Output: ${outputSvgPath}\n`);
}

// Command-line execution
if (process.argv.length < 6) {
    console.error('Usage: node embed-path-metadata.js <prefix> <svgPath> <dataJsonPath> <outputSvgPath>');
    console.error('');
    console.error('Example:');
    console.error('  node embed-path-metadata.js "Level1" "./input/Level1.svg" "./input/Level1-data.json" "./output/Level1-with-metadata.svg"');
    process.exit(1);
}

const [,, prefix, svgPath, dataJsonPath, outputSvgPath] = process.argv;

try {
    embedPathMetadata(prefix, svgPath, dataJsonPath, outputSvgPath);
} catch (error) {
    console.error('\n❌ Error embedding path metadata:');
    console.error(error.message);
    console.error(error.stack);
    process.exit(1);
}
