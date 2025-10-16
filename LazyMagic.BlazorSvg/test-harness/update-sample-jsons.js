import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// The 23 combos with discrepancies (using 0-based index)
const problemComboIndices = [
  4,  // Combo_0005
  9,  // Combo_0010
  12, // Combo_0013
  14, // Combo_0015
  15, // Combo_0016
  17, // Combo_0018
  19, // Combo_0020
  21, // Combo_0022
  22, // Combo_0023
  23, // Combo_0024
  24, // Combo_0025
  25, // Combo_0026
  26, // Combo_0027
  27, // Combo_0028
  29, // Combo_0030
  30, // Combo_0031
  31, // Combo_0032
  36, // Combo_0037
  37, // Combo_0038
  39, // Combo_0040
  40, // Combo_0041
  42, // Combo_0043
  44  // Combo_0045
];

// Read the current sample files (which have 50 combos)
const normalPath = path.join(__dirname, 'valid-combinations-sample-normal.json');
const rotatedPath = path.join(__dirname, 'valid-combinations-sample-rotated.json');

const normalData = JSON.parse(fs.readFileSync(normalPath, 'utf8'));
const rotatedData = JSON.parse(fs.readFileSync(rotatedPath, 'utf8'));

// Filter to problem combos
const normalFiltered = {
  combinations: problemComboIndices.map(i => normalData.combinations[i])
};

const rotatedFiltered = {
  combinations: problemComboIndices.map(i => rotatedData.combinations[i])
};

// Write to sample files
const normalSamplePath = path.join(__dirname, 'valid-combinations-sample-normal.json');
const rotatedSamplePath = path.join(__dirname, 'valid-combinations-sample-rotated.json');

fs.writeFileSync(normalSamplePath, JSON.stringify(normalFiltered, null, 2));
fs.writeFileSync(rotatedSamplePath, JSON.stringify(rotatedFiltered, null, 2));

console.log(`Updated valid-combinations-sample-normal.json: 50 → ${normalFiltered.combinations.length} combos`);
console.log(`Updated valid-combinations-sample-rotated.json: 50 → ${rotatedFiltered.combinations.length} combos`);
console.log('\nProblem combo indices:', problemComboIndices.join(', '));
