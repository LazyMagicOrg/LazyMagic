// Check and install dependencies if needed
import { existsSync } from 'fs';
import { exec } from 'child_process';
import { promisify } from 'util';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const execAsync = promisify(exec);
const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Required dependencies
const requiredDeps = ['jsdom', 'xml-beautify', 'xmldom'];

/**
 * Check if a package is installed
 */
function isPackageInstalled(packageName) {
    const packagePath = join(__dirname, 'node_modules', packageName);
    return existsSync(packagePath);
}

/**
 * Check and install missing dependencies
 */
async function checkAndInstallDependencies() {
    console.log('Checking dependencies...');

    const missingDeps = requiredDeps.filter(dep => !isPackageInstalled(dep));

    if (missingDeps.length === 0) {
        console.log('✓ All dependencies are installed');
        return;
    }

    console.log(`Missing dependencies: ${missingDeps.join(', ')}`);
    console.log('Installing missing dependencies...');

    try {
        await execAsync('npm install', { cwd: __dirname });
        console.log('✓ Dependencies installed successfully');
    } catch (error) {
        console.error('✗ Error installing dependencies:', error.message);
        process.exit(1);
    }
}

// Run the check
checkAndInstallDependencies().catch(error => {
    console.error('Fatal error:', error);
    process.exit(1);
});
