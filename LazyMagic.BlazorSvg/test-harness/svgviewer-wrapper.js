// Wrapper to extract algorithm functions from SvgViewer.js for Node.js testing
// This reads the actual SvgViewer.js and extracts the pure algorithm functions

import fs from 'fs';
import path from 'path';
import { config } from './test-config.js';

// Read the SvgViewer.js source
const svgViewerPath = path.resolve(config.svgViewerPath);
const svgViewerSource = fs.readFileSync(svgViewerPath, 'utf8');

// Create a minimal mock environment for SvgViewer.js
const mockWindow = {};
const mockDocument = {};
const mockConsole = console;

// Extract and execute the algorithm functions
// We'll create a minimal class instance to access the methods

class SvgViewerAlgorithms {
    constructor() {
        // Copy algorithm methods from the actual SvgViewer.js implementation
    }

    // These methods will be populated from the actual SvgViewer.js
}

// For now, let's manually extract the key functions from SvgViewer.js
// TODO: Parse SvgViewer.js automatically to extract these functions

export function detectPolygonOrientation(polygon) {
    if (!polygon || polygon.length < 3) return 0;

    const edgeAngles = [];
    for (let i = 0; i < polygon.length; i++) {
        const p1 = polygon[i];
        const p2 = polygon[(i + 1) % polygon.length];
        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const length = Math.sqrt(dx * dx + dy * dy);
        const angle = Math.atan2(dy, dx) * 180 / Math.PI;
        edgeAngles.push({ angle, length });
    }

    let bestAngle = 0;
    let maxWidth = 0;
    const testedAngles = new Set();

    for (const edge of edgeAngles) {
        let testAngle = edge.angle;
        while (testAngle < 0) testAngle += 180;
        while (testAngle >= 180) testAngle -= 180;

        const angleKey = Math.round(testAngle * 10) / 10;
        if (testedAngles.has(angleKey)) continue;
        testedAngles.add(angleKey);

        const angleRad = testAngle * Math.PI / 180;
        const cos = Math.cos(angleRad);
        const sin = Math.sin(angleRad);

        let minRotX = Infinity, maxRotX = -Infinity;
        for (const p of polygon) {
            const rotX = p.x * cos + p.y * sin;
            minRotX = Math.min(minRotX, rotX);
            maxRotX = Math.max(maxRotX, rotX);
        }

        const width = maxRotX - minRotX;
        if (width > maxWidth) {
            maxWidth = width;
            bestAngle = testAngle;
        }
    }

    return bestAngle;
}

// Note: These are copies from SvgViewer.js - they should stay in sync
// TODO: Implement automatic extraction or use a headless browser approach

export { SvgViewerAlgorithms };
