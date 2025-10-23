const { findHollowSquareLayout } = require('../wwwroot/SvgViewerHollowSquare.js');

// Simple rectangle polygon for testing
const polygon = [
    {x: 0, y: 0},
    {x: 223, y: 0},
    {x: 223, y: 96},
    {x: 0, y: 96}
];

console.log('Testing with angleSamples=12, centroidSamples=5...');
console.log('Polygon: 223×96 (similar to Combo_0004)');
console.log('');

const start = Date.now();
const result = findHollowSquareLayout(polygon, {
    angleSamples: 12,
    centroidSamples: 5,
    debugMode: true
});
const elapsed = Date.now() - start;

console.log('');
console.log('Result:', result ? `${result.tables} tables, ${result.width.toFixed(1)}×${result.height.toFixed(1)}` : 'null');
console.log('Time:', elapsed, 'ms');
