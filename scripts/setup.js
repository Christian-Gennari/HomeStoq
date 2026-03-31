const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const chalk = require('chalk');
const boxen = require('boxen');

console.log("");
console.log(chalk.bold.cyan("🏠 HomeStoq Setup"));
console.log(chalk.gray("=".repeat(50)));
console.log("");

// 1. Initialize .env
const envPath = path.join(__dirname, '..', '.env');
const envExamplePath = path.join(__dirname, '..', '.env-example');

if (!fs.existsSync(envPath)) {
  console.log(chalk.yellow("Initializing .env file..."));
  fs.copyFileSync(envExamplePath, envPath);
  console.log(`  ${chalk.green('✅')} Created .env from .env-example`);
  console.log(`  ${chalk.yellow('⚠️')}  ${chalk.bold('Action Required:')} Open .env and add your GEMINI_API_KEY.`);
} else {
  console.log(`  ${chalk.green('✅')} .env file already exists.`);
}

// 2. Check for .NET SDK
try {
  const dotnetVersion = execSync('dotnet --version', { encoding: 'utf8' }).trim();
  console.log(`  ${chalk.green('✅')} .NET SDK found: ${chalk.bold('v' + dotnetVersion)}`);
} catch (e) {
  console.log(`  ${chalk.red('❌')} .NET SDK not found. Please install .NET 8.0 or higher.`);
}

// 3. Check for Playwright browsers
const scraperBin = path.join(__dirname, '..', 'src', 'KeepScraper', 'bin');
if (!fs.existsSync(scraperBin)) {
  console.log("");
  console.log(chalk.blue("Note: You will need to run 'npm run playwright:install' after your first build"));
  console.log(chalk.blue("to download the browsers required for the voice scraper."));
}

const summary = chalk.bold("Setup complete!\n\n") + 
                `Use ${chalk.cyan('npm run help')} to see available commands.\n` +
                `Start developing with ${chalk.green('npm run dev')}.`;

console.log("");
console.log(boxen(summary, {
  padding: 1,
  borderColor: 'green',
  borderStyle: 'round'
}));
console.log("");
