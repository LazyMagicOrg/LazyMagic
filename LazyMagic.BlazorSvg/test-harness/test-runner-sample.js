#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { testCases, config } from './test-config-sample.js';

// Rest of the imports are the same as test-runner.js
const require = createRequire(import.meta.url);

// Load SvgViewerAlgorithms first (contains SpatialGrid and KDTree)
const SvgViewerAlgorithms = require('../wwwroot/SvgViewerAlgorithms.js');

// Make SpatialGrid globally available (needed by SvgViewerOptimized.js)
global.SpatialGrid = SvgViewerAlgorithms.SpatialGrid;

// Now load SvgViewerOptimized.js (needs global.SpatialGrid)
const { fastInscribedRectangle } = require('../wwwroot/SvgViewerOptimized.js');

// Load boundary-based and hybrid algorithms
const { boundaryBasedInscribedRectangle, hybridInscribedRectangle } = require('../wwwroot/SvgViewerBoundaryBased.js');

// Make algorithms globally available for hybrid algorithm
global.fastInscribedRectangle = fastInscribedRectangle;

// Color codes for console output
const colors = {
    reset: '\x1b[0m',
    green: '\x1b[32m',
    red: '\x1b[31m',
    yellow: '\x1b[33m',
    blue: '\x1b[34m',
    cyan: '\x1b[36m'
};

function log(message, color = 'reset') {
    console.log(`${colors[color]}${message}${colors.reset}`);
}

// Import the main function logic from test-runner.js by reading and evaluating it
// For now, just run a simple version
async function main() {
    log('\n' + '='.repeat(60), 'cyan');
    log('SVG Inscribed Rectangle Test Harness - SAMPLE (6 tests)', 'cyan');
    log('='.repeat(60), 'cyan');

    log(`Running ${testCases.length} sample tests...`, 'yellow');

    // Just report what would run
    for (const testCase of testCases) {
        log(`  - ${testCase.name}: ${testCase.paths.join(', ')}`, 'blue');
    }

    log('\nTo run the actual tests, use the full test-runner.js', 'yellow');
    log('This file is a placeholder. Copy the logic from test-runner.js to complete it.', 'yellow');
}

main().catch(err => {
    log(`Fatal error: ${err.message}`, 'red');
    console.error(err);
    process.exit(1);
});
