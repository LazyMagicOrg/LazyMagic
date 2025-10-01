#!/usr/bin/env node

import puppeteer from 'puppeteer';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

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

async function main() {
    log('\n' + '='.repeat(60), 'cyan');
    log('SvgViewer.js Browser Test Harness (Puppeteer)', 'cyan');
    log('='.repeat(60), 'cyan');

    let browser;
    try {
        // Launch browser
        log('\nLaunching browser...', 'blue');
        browser = await puppeteer.launch({
            headless: true,
            args: ['--no-sandbox', '--disable-setuid-sandbox']
        });

        const page = await browser.newPage();

        // Enable console logging from the browser
        page.on('console', msg => {
            const text = msg.text();
            if (text.includes('[outline]') || text.includes('[rectangle]') ||
                text.includes('[parallelogram]') || text.includes('[trapezoid]')) {
                log(`  Browser: ${text}`, 'blue');
            }
        });

        // Listen for page errors
        page.on('pageerror', error => {
            log(`  Page Error: ${error.message}`, 'red');
        });

        // Load the test page
        const testPagePath = `file://${path.resolve(__dirname, 'test-page.html')}`;
        log(`Loading test page: ${testPagePath}`, 'blue');

        await page.goto(testPagePath, {
            waitUntil: 'networkidle0',
            timeout: 30000
        });

        log('Test page loaded', 'green');

        // Wait for tests to complete
        log('\nWaiting for tests to complete...', 'yellow');
        await page.waitForFunction(
            () => window.testsComplete === true,
            { timeout: 60000 }
        );

        // Get test results
        const results = await page.evaluate(() => {
            return {
                tests: window.testResults,
                html: document.getElementById('test-results').innerHTML
            };
        });

        // Display results
        log('\n' + '='.repeat(60), 'cyan');
        log('Test Results', 'cyan');
        log('='.repeat(60), 'cyan');

        let passed = 0;
        let failed = 0;

        for (const test of results.tests) {
            log(`\nTest: ${test.name}`, test.success ? 'green' : 'red');
            log(`Paths: ${test.paths.join(', ')}`, 'blue');

            if (test.success) {
                passed++;
                log('✓ Success', 'green');
                if (test.data) {
                    log(`  ${JSON.stringify(test.data, null, 2)}`, 'yellow');
                }
            } else {
                failed++;
                log('✗ Failed', 'red');
                if (test.error) {
                    log(`  Error: ${test.error}`, 'red');
                }
            }
        }

        // Summary
        log('\n' + '='.repeat(60), 'cyan');
        log('Summary', 'cyan');
        log('='.repeat(60), 'cyan');
        log(`Total: ${results.tests.length}`, 'blue');
        log(`Passed: ${passed}`, 'green');
        log(`Failed: ${failed}`, failed > 0 ? 'red' : 'green');

        if (failed === 0) {
            log('\n✓ All tests passed!', 'green');
        } else {
            log('\n✗ Some tests failed', 'red');
        }

        // Optionally take a screenshot
        const screenshotPath = path.join(__dirname, 'test-results.png');
        await page.screenshot({ path: screenshotPath, fullPage: true });
        log(`\nScreenshot saved: ${screenshotPath}`, 'blue');

    } catch (error) {
        log(`\nFatal error: ${error.message}`, 'red');
        console.error(error);
        process.exit(1);
    } finally {
        if (browser) {
            await browser.close();
        }
    }
}

main();
