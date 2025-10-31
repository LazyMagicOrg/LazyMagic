#!/usr/bin/env node

/**
 * add-layout-restriction.js
 *
 * Adds LayoutRestriction property (default: "allowed") to all RoomSections in a Rooms.json file
 * that don't already have it.
 *
 * Usage: node add-layout-restriction.js <path-to-rooms.json>
 */

import fs from 'fs';

const args = process.argv.slice(2);

if (args.length < 1) {
    console.error('Usage: node add-layout-restriction.js <path-to-rooms.json>');
    console.error('Example: node add-layout-restriction.js C:\\path\\to\\Rooms.json');
    process.exit(1);
}

const roomsPath = args[0];

console.log('Adding LayoutRestriction property to Rooms.json...\n');
console.log(`File: ${roomsPath}\n`);

if (!fs.existsSync(roomsPath)) {
    console.error(`Error: File not found: ${roomsPath}`);
    process.exit(1);
}

// Read file and remove BOM if present
let fileContent = fs.readFileSync(roomsPath, 'utf8');
if (fileContent.charCodeAt(0) === 0xFEFF) {
    fileContent = fileContent.slice(1);
}

const data = JSON.parse(fileContent);
let totalSections = 0;
let addedCount = 0;

// Process each level
for (const level of data) {
    console.log(`Level: ${level.Name || level.Id}`);

    for (const room of level.Rooms) {
        console.log(`  Room: ${room.Id}`);

        for (const section of room.RoomSections) {
            totalSections++;

            if (!section.LayoutRestriction) {
                section.LayoutRestriction = "allowed";
                addedCount++;
                console.log(`    ✓ Added LayoutRestriction to ${section.Id}`);
            } else {
                console.log(`    - ${section.Id} already has LayoutRestriction: ${section.LayoutRestriction}`);
            }
        }
    }
    console.log();
}

// Write back to file
fs.writeFileSync(roomsPath, JSON.stringify(data, null, 2), 'utf8');

console.log('='.repeat(80));
console.log('SUMMARY');
console.log('='.repeat(80));
console.log(`Total sections: ${totalSections}`);
console.log(`Added LayoutRestriction: ${addedCount}`);
console.log(`Already had property: ${totalSections - addedCount}`);
console.log();
console.log(`✓ Updated file: ${roomsPath}`);
console.log();
