const chalk = require('chalk');
const boxen = require('boxen');
const gradient = require('gradient-string');

const commands = [
  { section: '🚀 Development', items: [
    { cmd: "npm run dev", desc: "Start everything locally (Fastest)" },
    { cmd: "npm run dev:docker", desc: "API in Docker + Scraper locally" },
  ]},
  { section: '📦 Services', items: [
    { cmd: "npm run api", desc: "Start only the backend locally" },
    { cmd: "npm run api:docker", desc: "Start only the backend in Docker" },
    { cmd: "npm run scraper", desc: "Start only the voice scraper" },
  ]},
  { section: '🧹 Utilities', items: [
    { cmd: "npm run setup", desc: "Initialize environment (.env, tools)" },
    { cmd: "npm run clean", desc: "Remove build artifacts (bin, obj, out)" },
    { cmd: "npm run playwright:install", desc: "Install required browsers" },
    { cmd: "npm run stop", desc: "Stop all Docker containers" },
  ]},
];

const notes = [
  `${chalk.blue('Local API:')}   http://localhost:5188`,
  `${chalk.green('Docker API:')}  http://localhost:80`,
  `${chalk.yellow('Google Keep:')} Log in once via visible browser window.`,
  `${chalk.magenta('Gemini Key:')} Ensure GEMINI_API_KEY is set in .env.`,
];

const headerText = `
  __  __                       ____  _               
 |  \\/  | ___  _ __   ___ _ __/ ___|| |_ ___   __ _ 
 | |\\/| |/ _ \\| '_ \\ / _ \\ '__\\___ \\| __/ _ \\ / _\` |
 | |  | | (_) | | | |  __/ |   ___) | || (_) | (_| |
 |_|  |_|\\___/|_| |_|\\___|_|  |____/ \\__\\___/ \\__, |
                                              |___/ 
`;

console.log(gradient.atlas.multiline(headerText));

let commandHelp = '';
commands.forEach(section => {
  commandHelp += chalk.bold.underline(`${section.section}\n`);
  section.items.forEach(item => {
    commandHelp += `  ${chalk.cyan(item.cmd.padEnd(25))} ${item.desc}\n`;
  });
  commandHelp += '\n';
});

console.log(boxen(commandHelp.trim(), {
  padding: 1,
  margin: 1,
  borderStyle: 'double',
  borderColor: 'cyan',
  title: 'Commands',
  titleAlignment: 'center'
}));

console.log(chalk.bold('Notes:'));
notes.forEach(note => {
  console.log(`  ${chalk.gray('•')} ${note}`);
});
console.log("");
