const fs = require('fs');
const path = require('path');
const chalk = require('chalk');

const targets = ['bin', 'obj', 'out'];
const srcDir = path.join(__dirname, '..', 'src');

function cleanDir(dir) {
  if (!fs.existsSync(dir)) return;

  const entries = fs.readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (targets.includes(entry.name)) {
        console.log(`  ${chalk.red('🗑')} Removing ${chalk.gray(fullPath)}...`);
        fs.rmSync(fullPath, { recursive: true, force: true });
      } else {
        cleanDir(fullPath);
      }
    }
  }
}

console.log("");
console.log(chalk.bold.red("🧹 Cleaning project artifacts..."));
console.log(chalk.gray("=".repeat(50)));
console.log("");

cleanDir(srcDir);

console.log("");
console.log(chalk.bold.green("✨ Clean complete!"));
console.log("");
