import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// The 23 combos with discrepancies
const problemCombos = [
  "Combo_0015", "Combo_0010", "Combo_0013", "Combo_0040", "Combo_0038",
  "Combo_0016", "Combo_0018", "Combo_0025", "Combo_0041", "Combo_0022",
  "Combo_0030", "Combo_0032", "Combo_0031", "Combo_0045", "Combo_0037",
  "Combo_0020", "Combo_0005", "Combo_0024", "Combo_0027", "Combo_0028",
  "Combo_0043", "Combo_0023", "Combo_0026"
];

// Read the current configs
const normalConfigPath = path.join(__dirname, 'test-config-normal.js');
const rotatedConfigPath = path.join(__dirname, 'test-config-rotated.js');

const normalContent = fs.readFileSync(normalConfigPath, 'utf8');
const rotatedContent = fs.readFileSync(rotatedConfigPath, 'utf8');

// Parse the test cases from the normal config
const normalMatch = normalContent.match(/export const testCases = (\[[\s\S]*\]);/);
if (!normalMatch) {
  console.error('Could not parse test-config-normal.js');
  process.exit(1);
}

const rotatedMatch = rotatedContent.match(/export const testCases = (\[[\s\S]*\]);/);
if (!rotatedMatch) {
  console.error('Could not parse test-config-rotated.js');
  process.exit(1);
}

// Parse JSON (with eval since it's already validated code)
const normalCases = eval(normalMatch[1]);
const rotatedCases = eval(rotatedMatch[1]);

// Filter to only problem combos
const normalFiltered = normalCases.filter(tc => problemCombos.includes(tc.name));
const rotatedFiltered = rotatedCases.filter(tc => problemCombos.includes(tc.name));

console.log(`Filtered normal config: ${normalCases.length} → ${normalFiltered.length} test cases`);
console.log(`Filtered rotated config: ${rotatedCases.length} → ${rotatedFiltered.length} test cases`);

// Write updated configs
const newNormalContent = `// Auto-generated test configuration for pre-computation
// Updated to focus on 23 combos with ≥1000 sq px discrepancy between normal and rotated
export const testCases = ${JSON.stringify(normalFiltered, null, 2)};
`;

const newRotatedContent = `// Auto-generated test configuration for pre-computation
// Updated to focus on 23 combos with ≥1000 sq px discrepancy between normal and rotated
export const testCases = ${JSON.stringify(rotatedFiltered, null, 2)};
`;

fs.writeFileSync(normalConfigPath, newNormalContent);
fs.writeFileSync(rotatedConfigPath, newRotatedContent);

console.log('\nConfig files updated successfully!');
console.log('Problem combos:', problemCombos.join(', '));
