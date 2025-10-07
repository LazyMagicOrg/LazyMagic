// Compute inscribed rectangles for all valid connected combinations
// Uses Puppeteer to run computations in a real browser environment

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import puppeteer from 'puppeteer';
import http from 'http';
import httpServer from 'http-server';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const combinationsPath = path.join(__dirname, 'valid-combinations.json');
const outputPath = path.join(__dirname, 'precomputed-rectangles.json');

// Test configuration
const SERVER_PORT = 8081;
const TEST_URL = `http://localhost:${SERVER_PORT}/test-page-precompute.html`;

/**
 * Start HTTP server
 */
function startServer() {
    const serverRoot = path.join(__dirname, '..');
    const server = httpServer.createServer({
        root: serverRoot,
        cache: -1,
        cors: true
    });

    return new Promise((resolve) => {
        server.listen(SERVER_PORT, () => {
            console.log(`  HTTP server started on port ${SERVER_PORT}`);
            console.log(`  Serving from: ${serverRoot}`);
            resolve(server);
        });
    });
}

/**
 * Create test HTML page for computation
 */
function createTestPage() {
    const testPagePath = path.join(__dirname, '../test-page-precompute.html');
    const htmlContent = `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Inscribed Rectangle Pre-computation</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/svg.js/3.2.4/svg.min.js"></script>
    <script src="wwwroot/SvgViewerBoundaryBased.js"></script>
    <script src="wwwroot/SvgViewer.js"></script>
</head>
<body>
    <div id="svg-container"></div>
    <script>
        // This page will be controlled by Puppeteer to compute inscribed rectangles
        window.computeInscribedRectangle = async function(pathIds) {
            try {
                // Load the SVG
                const response = await fetch('../BlazorTest.WASM/wwwroot/Level1.svg');
                const svgText = await response.text();

                // Parse SVG
                const parser = new DOMParser();
                const svgDoc = parser.parseFromString(svgText, 'image/svg+xml');
                const svgElement = svgDoc.documentElement;

                // Create SVG.js instance
                const container = document.getElementById('svg-container');
                container.innerHTML = '';
                container.appendChild(svgElement);

                const draw = SVG(svgElement);

                // Create SvgViewer instance
                const viewer = new SvgViewer(draw);

                // Generate merged path outline
                const outlinePathData = viewer.generateGroupOutline(pathIds, {
                    gapHopPx: 3,
                    kStart: 10,
                    kMax: 25,
                    maxEdgePx: 100,
                    sampleStride: 1,
                    downsampleEveryN: 1,
                    minContainment: 0.0,
                    debugShowUnifiedPath: false
                });

                if (!outlinePathData) {
                    return { error: 'Failed to generate outline' };
                }

                // Parse the path data to extract polygon points
                const pathElement = draw.path(outlinePathData);
                const pathLength = pathElement.length();
                const numSamples = Math.max(100, Math.floor(pathLength / 5));

                const polygon = [];
                for (let i = 0; i < numSamples; i++) {
                    const point = pathElement.pointAt(i / numSamples * pathLength);
                    polygon.push({ x: point.x, y: point.y });
                }

                pathElement.remove();

                // Compute inscribed rectangle using hybridInscribedRectangle
                if (typeof window.hybridInscribedRectangle === 'undefined') {
                    return { error: 'hybridInscribedRectangle not loaded' };
                }

                const options = {
                    gridSize: 30,
                    minArea: 50,
                    pathCount: pathIds.length,
                    centroidStrategy: 'auto',
                    timeoutMs: 60000,
                    accuracyMode: true
                };

                const result = window.hybridInscribedRectangle(polygon, options);

                if (!result || result.area === 0) {
                    return { error: 'No rectangle found' };
                }

                return {
                    success: true,
                    rectangle: {
                        x: result.x,
                        y: result.y,
                        width: result.width,
                        height: result.height,
                        angle: result.angle || 0,
                        area: result.area,
                        corners: result.corners,
                        type: result.type,
                        centroidStrategy: result.centroidStrategy
                    }
                };
            } catch (error) {
                return { error: error.message };
            }
        };

        console.log('Pre-computation page ready');
    </script>
</body>
</html>`;

    fs.writeFileSync(testPagePath, htmlContent);
    console.log(`  Test page created: ${testPagePath}`);
}

/**
 * Main computation function
 */
async function main() {
    console.log('='.repeat(80));
    console.log('INSCRIBED RECTANGLE PRE-COMPUTATION');
    console.log('='.repeat(80));
    console.log();

    // Step 1: Load combinations
    console.log('Step 1: Loading valid combinations...');
    const combinationsData = JSON.parse(fs.readFileSync(combinationsPath, 'utf8'));
    const combinations = combinationsData.combinations;
    console.log(`  Loaded ${combinations.length} combinations`);
    console.log();

    // Step 2: Create test page
    console.log('Step 2: Creating test page...');
    createTestPage();
    console.log();

    // Step 3: Start HTTP server
    console.log('Step 3: Starting HTTP server...');
    const server = await startServer();
    console.log();

    // Step 4: Launch browser
    console.log('Step 4: Launching browser...');
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });
    const page = await browser.newPage();

    // Set up console logging from browser
    page.on('console', msg => {
        const text = msg.text();
        if (text.includes('[')) { // Only log algorithm messages
            console.log(`    Browser: ${text}`);
        }
    });

    console.log(`  Navigating to ${TEST_URL}...`);
    await page.goto(TEST_URL, { waitUntil: 'networkidle0' });
    console.log('  Page loaded successfully');
    console.log();

    // Step 5: Compute rectangles
    console.log('Step 5: Computing inscribed rectangles...');
    console.log('='.repeat(80));

    const results = [];
    const startTime = Date.now();
    let successCount = 0;
    let failCount = 0;

    for (let i = 0; i < combinations.length; i++) {
        const combo = combinations[i];
        const progress = ((i + 1) / combinations.length * 100).toFixed(1);

        process.stdout.write(`  [${progress}%] ${i + 1}/${combinations.length}: ${combo.key}...`);

        const comboStartTime = Date.now();

        try {
            const result = await page.evaluate((pathIds) => {
                return window.computeInscribedRectangle(pathIds);
            }, combo.sections);

            const comboTime = Date.now() - comboStartTime;

            if (result.success && result.rectangle && result.rectangle.area > 0) {
                results.push({
                    key: combo.key,
                    sections: combo.sections,
                    rectangle: result.rectangle,
                    computationTimeMs: comboTime
                });
                successCount++;
                console.log(` ✓ (${comboTime}ms, area=${result.rectangle.area.toFixed(1)})`);
            } else {
                failCount++;
                console.log(` ✗ (${comboTime}ms, error: ${result.error || 'unknown'})`);
            }
        } catch (error) {
            const comboTime = Date.now() - comboStartTime;
            failCount++;
            console.log(` ✗ (${comboTime}ms, exception: ${error.message})`);
        }

        // Progress update every 50 combinations
        if ((i + 1) % 50 === 0) {
            const elapsed = Date.now() - startTime;
            const avgTime = elapsed / (i + 1);
            const remaining = (combinations.length - i - 1) * avgTime;
            console.log(`    Elapsed: ${(elapsed / 1000).toFixed(0)}s, Est. remaining: ${(remaining / 1000).toFixed(0)}s`);
        }
    }

    const totalTime = Date.now() - startTime;

    // Clean up
    await browser.close();
    server.close();

    console.log('='.repeat(80));
    console.log();

    // Step 6: Statistics
    console.log('Step 6: Computation Statistics');
    console.log('='.repeat(80));
    console.log(`  Total combinations: ${combinations.length}`);
    console.log(`  Successful: ${successCount}`);
    console.log(`  Failed: ${failCount}`);
    console.log(`  Success rate: ${(successCount / combinations.length * 100).toFixed(1)}%`);
    console.log(`  Total time: ${(totalTime / 1000).toFixed(1)}s (${(totalTime / 60000).toFixed(1)} minutes)`);
    console.log(`  Average time per combination: ${(totalTime / combinations.length).toFixed(0)}ms`);
    console.log();

    // Step 7: Save results
    console.log('Step 7: Saving results...');
    const output = {
        generatedAt: new Date().toISOString(),
        totalCombinations: combinations.length,
        successfulComputations: successCount,
        failedComputations: failCount,
        totalComputationTimeMs: totalTime,
        rectangles: results
    };

    fs.writeFileSync(outputPath, JSON.stringify(output, null, 2));
    console.log(`  Results saved to: ${outputPath}`);
    console.log(`  File size: ${(fs.statSync(outputPath).size / 1024).toFixed(1)} KB`);
    console.log();

    console.log('='.repeat(80));
    console.log('COMPUTATION COMPLETE!');
    console.log('Next step: Embed results into Level1.svg');
    console.log('='.repeat(80));
}

main().catch(console.error);
