// Analyze breach patterns in failed combinations

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const combosPath = path.join(__dirname, '../TestResults/Combos.txt');
const validCombinationsPath = path.join(__dirname, 'valid-combinations.json');

function main() {
    console.log('='.repeat(80));
    console.log('BREACH PATTERN ANALYSIS');
    console.log('='.repeat(80));
    console.log();

    // Load breach list
    const breachList = fs.readFileSync(combosPath, 'utf8')
        .split('\n')
        .map(line => line.trim())
        .filter(line => line.length > 0)
        .map(line => parseInt(line.replace('Combo_', '')));

    console.log(`Total breached combinations: ${breachList.length}`);
    console.log();

    // Load valid combinations
    const validCombinations = JSON.parse(fs.readFileSync(validCombinationsPath, 'utf8'));
    console.log(`Total valid combinations: ${validCombinations.totalCombinations}`);
    console.log();

    // Map breach combo numbers to actual sections
    const breachedCombos = breachList.map(num => {
        const combo = validCombinations.combinations[num - 1]; // 1-indexed
        return {
            number: num,
            key: combo.key,
            sections: combo.sections,
            size: combo.size
        };
    });

    // Analyze patterns
    console.log('='.repeat(80));
    console.log('PATTERN ANALYSIS');
    console.log('='.repeat(80));
    console.log();

    // 1. Distribution by combination size
    console.log('1. Breach Rate by Combination Size:');
    const sizeStats = new Map();
    for (let size = 1; size <= 12; size++) {
        const totalOfSize = validCombinations.combinations.filter(c => c.size === size).length;
        const breachedOfSize = breachedCombos.filter(c => c.size === size).length;
        const rate = totalOfSize > 0 ? (breachedOfSize / totalOfSize * 100) : 0;
        sizeStats.set(size, { total: totalOfSize, breached: breachedOfSize, rate: rate });

        if (totalOfSize > 0) {
            console.log(`   Size ${size.toString().padStart(2)}: ${breachedOfSize.toString().padStart(3)}/${totalOfSize.toString().padStart(3)} breached (${rate.toFixed(1)}%)`);
        }
    }
    console.log();

    // 2. Section frequency in breached combinations
    console.log('2. Most Common Sections in Breached Combinations:');
    const sectionFreq = new Map();
    for (const combo of breachedCombos) {
        for (const section of combo.sections) {
            sectionFreq.set(section, (sectionFreq.get(section) || 0) + 1);
        }
    }

    const sortedSections = Array.from(sectionFreq.entries())
        .sort((a, b) => b[1] - a[1]);

    for (const [section, count] of sortedSections) {
        const percentage = (count / breachList.length * 100).toFixed(1);
        console.log(`   ${section.padEnd(25)} ${count.toString().padStart(3)} occurrences (${percentage}%)`);
    }
    console.log();

    // 3. Check if aisles are problematic
    console.log('3. Aisle Involvement:');
    const withAisles = breachedCombos.filter(c =>
        c.sections.some(s => s.includes('Aisle'))
    ).length;
    const withoutAisles = breachedCombos.length - withAisles;
    console.log(`   With aisles: ${withAisles} (${(withAisles / breachList.length * 100).toFixed(1)}%)`);
    console.log(`   Without aisles: ${withoutAisles} (${(withoutAisles / breachList.length * 100).toFixed(1)}%)`);
    console.log();

    // 4. Specific aisle patterns
    console.log('4. Aisle-Specific Patterns:');
    const aisles = ['Ballroom_Aisle_12', 'Ballroom_Aisle_34', 'Ballroom_Aisle_35', 'Ballroom_Aisle_46', 'Ballroom_Aisle_56', 'Ballroom_Aisle_3456'];
    for (const aisle of aisles) {
        const withThisAisle = breachedCombos.filter(c => c.sections.includes(aisle)).length;
        const percentage = (withThisAisle / breachList.length * 100).toFixed(1);
        console.log(`   ${aisle.padEnd(25)} ${withThisAisle.toString().padStart(3)} breached (${percentage}%)`);
    }
    console.log();

    // 5. Check for specific ballroom patterns
    console.log('5. Ballroom-Specific Patterns:');
    const ballrooms = ['Ballroom_1', 'Ballroom_2', 'Ballroom_3', 'Ballroom_4', 'Ballroom_5', 'Ballroom_Grand'];
    for (const ballroom of ballrooms) {
        const withThisBallroom = breachedCombos.filter(c => c.sections.includes(ballroom)).length;
        const percentage = (withThisBallroom / breachList.length * 100).toFixed(1);
        console.log(`   ${ballroom.padEnd(25)} ${withThisBallroom.toString().padStart(3)} breached (${percentage}%)`);
    }
    console.log();

    // 6. Combinations with crossing aisle (Aisle_3456)
    console.log('6. Central Crossing Aisle (Aisle_3456):');
    const withCrossing = breachedCombos.filter(c => c.sections.includes('Ballroom_Aisle_3456')).length;
    console.log(`   Breached combos with Aisle_3456: ${withCrossing} (${(withCrossing / breachList.length * 100).toFixed(1)}%)`);
    console.log();

    // 7. Complex combinations (5+ sections)
    console.log('7. Complex Combinations (5+ sections):');
    const complex = breachedCombos.filter(c => c.size >= 5).length;
    console.log(`   Breached combos with 5+ sections: ${complex} (${(complex / breachList.length * 100).toFixed(1)}%)`);

    const complexTotal = validCombinations.combinations.filter(c => c.size >= 5).length;
    console.log(`   Total combos with 5+ sections: ${complexTotal}`);
    console.log(`   Breach rate for complex combos: ${(complex / complexTotal * 100).toFixed(1)}%`);
    console.log();

    // 8. Show first 20 breached combinations for inspection
    console.log('8. Sample Breached Combinations (first 20):');
    for (let i = 0; i < Math.min(20, breachedCombos.length); i++) {
        const combo = breachedCombos[i];
        console.log(`   Combo_${String(combo.number).padStart(4, '0')}: [${combo.sections.join(', ')}]`);
    }
    console.log();

    console.log('='.repeat(80));
    console.log('SUMMARY');
    console.log('='.repeat(80));
    console.log(`Total combinations analyzed: ${validCombinations.totalCombinations}`);
    console.log(`Breached combinations: ${breachList.length} (${(breachList.length / validCombinations.totalCombinations * 100).toFixed(1)}%)`);
    console.log(`Success rate: ${((1 - breachList.length / validCombinations.totalCombinations) * 100).toFixed(1)}%`);
}

main();
