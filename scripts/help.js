const chalk = require("chalk");
const boxen = require("boxen");
const gradient = require("gradient-string");

const commands = [
  {
    section: "🚀 Development",
    items: [
      { cmd: "npm run dev", desc: "Start everything in Docker (Default)" },
      { cmd: "npm run dev:local", desc: "Start API + Scraper locally" },
    ],
  },
  {
    section: "📦 Docker",
    items: [
      { cmd: "npm run docker:build", desc: "Rebuild all Docker containers" },
      { cmd: "npm run docker:down", desc: "Stop all Docker containers" },
      { cmd: "npm run docker:clean", desc: "Deep clean Docker & build files" },
    ],
  },
  {
    section: "🔧 Maintenance",
    items: [
      { cmd: "npm run setup", desc: "Initialize environment (.env, tools)" },
      { cmd: "npm run clean", desc: "Remove build artifacts locally" },
      { cmd: "npm run api:local", desc: "Start only backend locally" },
      { cmd: "npm run scraper:local", desc: "Start only scraper locally" },
    ],
  },
];

const notes = [
  `${chalk.blue("Browser URL:")}  http://localhost:5050`,
  `${chalk.cyan("noVNC Login:")}  http://localhost:6080 (if manual login needed)`,
  `${chalk.yellow("Auto-Login:")}   Add GOOGLE_USERNAME/PASSWORD in .env.`,
  `${chalk.magenta("Gemini Key:")}  Ensure GEMINI_API_KEY is set in .env.`,
];

const headerText = `
  _   _                      ____  _              
 | | | | ___  _ __ ___   ___/ ___|| |_ ___   __ _ 
 | |_| |/ _ \\| '_ \` _ \\ / _ \\___ \\| __/ _ \\ / _\` |
 |  _  | (_) | | | | | |  __/___) | || (_) | (_| |
 |_| |_|\\___/|_| |_| |_|\\___|____/ \\__\\___/ \\__, |
                                               |_|
`;

console.log(gradient.atlas.multiline(headerText));

let commandHelp = "";
commands.forEach((section) => {
  commandHelp += chalk.bold.underline(`${section.section}\n`);
  section.items.forEach((item) => {
    commandHelp += `  ${chalk.cyan(item.cmd.padEnd(25))} ${item.desc}\n`;
  });
  commandHelp += "\n";
});

console.log(
  boxen(commandHelp.trim(), {
    padding: 1,
    margin: 1,
    borderStyle: "double",
    borderColor: "cyan",
    title: "Commands",
    titleAlignment: "center",
  }),
);

console.log(chalk.bold("Notes:"));
notes.forEach((note) => {
  console.log(`  ${chalk.gray("•")} ${note}`);
});
console.log("");
