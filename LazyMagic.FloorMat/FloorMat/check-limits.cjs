const { findHollowSquareLayout } = require('../wwwroot/SvgViewerHollowSquare.js');

// Simple 223x96 rectangle (like Combo_0004)
const polygon = [
    {x: 234.8, y: 32.3},
    {x: 458.2, y: 32.3},
    {x: 458.2, y: 128.1},
    {x: 234.8, y: 128.1}
];

console.log('Testing with 223×96 polygon (Combo_0004 dimensions)');
console.log('angleSamples=12, centroidSamples=5');
console.log('');

const start = Date.now();
const result = findHollowSquareLayout(polygon, {
    angleSamples: 12,
    centroidSamples: 5,
    debugMode: true
});
const elapsed = Date.now() - start;

console.log('');
console.log('Time:', elapsed, 'ms');
console.log('Result:', result ? `${result.tables} tables, ${result.width}×${result.height}` : 'null');
