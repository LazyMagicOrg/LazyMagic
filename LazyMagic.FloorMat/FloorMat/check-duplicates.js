// Check for duplicate vertices in polygon
const pointsStr = process.argv[2];
if (!pointsStr) {
    console.error('Usage: node check-duplicates.js "<space-separated points>"');
    process.exit(1);
}

const points = pointsStr.split(' ');
console.log('Total vertices:', points.length);

const coords = points.map(p => p.split(',').map(Number));
const eps = 0.01;
const duplicates = [];

for (let i = 0; i < coords.length; i++) {
    for (let j = i + 1; j < coords.length; j++) {
        const dx = Math.abs(coords[i][0] - coords[j][0]);
        const dy = Math.abs(coords[i][1] - coords[j][1]);
        if (dx < eps && dy < eps) {
            duplicates.push([i, j, coords[i]]);
        }
    }
}

console.log('Duplicates found:', duplicates.length);
duplicates.forEach(([i, j, coord]) => {
    console.log(`  p${i} = p${j} at (${coord[0].toFixed(3)}, ${coord[1].toFixed(3)})`);
});
