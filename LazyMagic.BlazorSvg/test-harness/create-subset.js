// Create a subset of combinations for testing
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// How many combinations to include
const SUBSET_SIZE = 10;

const inputPath = path.join(__dirname, 'valid-combinations.json');
const outputPath = path.join(__dirname, 'valid-combinations-subset.json');

const allData = JSON.parse(fs.readFileSync(inputPath, 'utf8'));

const subsetData = {
  ...allData,
  totalCombinations: SUBSET_SIZE,
  combinations: allData.combinations.slice(0, SUBSET_SIZE)
};

fs.writeFileSync(outputPath, JSON.stringify(subsetData, null, 2));

console.log(`Created valid-combinations-subset.json with ${SUBSET_SIZE} combinations`);
console.log(`Original file had ${allData.totalCombinations} combinations`);
