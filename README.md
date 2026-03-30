# HomeStoq 🏠🍎

**HomeStoq** is a lightweight, AI-powered pantry management system designed to run on a local network. It helps you track inventory effortlessly using receipt scanning, voice commands via Google Tasks, and predictive analysis to generate smart shopping lists.

## 🚀 Features

-   **Stock Tracking**: Real-time view of your pantry items with manual override.
-   **Receipt OCR**: Snap a photo of your grocery receipt, and HomeStoq (powered by Gemini 2.5 Flash) will automatically extract items, quantities, and prices to update your inventory.
-   **Voice Sync**: Integrate with Google Tasks. Simply say "used the last milk" or "bought eggs" to your voice assistant (mapped to a Google Task list named "HomeStoq"), and the background worker will process the change.
-   **Smart Shopping List**: Predictive analysis based on your 30-day consumption history and current stock levels to suggest what you need to buy next.
-   **Privacy-First**: Runs locally in Docker with a SQLite database.

## 🛠 Tech Stack

-   **Backend**: ASP.NET Core 10 (Minimal APIs)
-   **Database**: SQLite (Dapper)
-   **AI Engine**: Google Gemini 2.5 Flash (OCR, Parsing, Prediction)
-   **Frontend**: Vanilla HTML5, CSS3, and JavaScript (No heavy frameworks)
-   **Voice Queue**: Google Tasks API
-   **Containerization**: Docker (Alpine Linux)

## 📋 Prerequisites

-   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for local development)
-   [Docker](https://www.docker.com/) (for deployment)
-   [Google AI Studio API Key](https://aistudio.google.com/) (for Gemini)
-   [Google Cloud Project Credentials](https://console.cloud.google.com/) (for Google Tasks/Voice Sync)

## ⚙️ Setup & Installation

### 1. Environment Configuration Guide
HomeStoq requires two main integrations from Google: **Gemini API** for intelligence and **Google Tasks API** for voice syncing.

#### Step A: Get a Gemini API Key (OCR & AI)
1.  Go to [Google AI Studio](https://aistudio.google.com/).
2.  Click on **"Get API key"** in the sidebar.
3.  Create a new API key in a new project.
4.  Copy this key; you will use it for the `GEMINI_API_KEY` variable.

#### Step B: Set up Google Tasks (Voice Sync)
1.  Go to the [Google Cloud Console](https://console.cloud.google.com/).
2.  Create a new project named "HomeStoq".
3.  Search for **"Google Tasks API"** and click **Enable**.
4.  Go to **APIs & Services > Credentials**.
5.  Click **Create Credentials > Service Account**.
6.  Click **Create and Continue** (you can skip the "Grant this service account access to project" and "Grant users access" steps by clicking **Done**).
7.  You will be returned to the credentials list. Find your new service account under the **Service Accounts** section (it will look like an email address). **Click on the email address** to open its details.
8.  At the top of the page, click the **Keys** tab.
9.  Click the **Add Key** button, then select **Create new key**.
10. Ensure **JSON** is selected in the pop-up, then click **Create**.
11. A `.json` file will automatically download to your computer.
12. **Important**: Rename this file to `key.json`. Create a folder named `creds` in your project root and move the file there.

#### Step C: Variable Reference
| Variable | Description | Example / Location |
| :--- | :--- | :--- |
| `GEMINI_API_KEY` | Your AI Studio Key | `AIzaSy...` |
| `GOOGLE_APPLICATION_CREDENTIALS` | Path to your JSON key | `/app/creds/key.json` |
| `DATABASE_PATH` | Path for SQLite DB | `/app/data/homestoq.db` |

#### Step D: How to set these variables
-   **For Docker**: Create a file named `.env` in the project root (see Step 2 below).
-   **For Local Development**: 
    -   **Option 1 (Easiest)**: Add them to `src/HomeStoq.Server/appsettings.json`.
    -   **Option 2 (Secure)**: Use .NET User Secrets:
        ```bash
        dotnet user-secrets set "GEMINI_API_KEY" "your_key" --project src/HomeStoq.Server/HomeStoq.Server.csproj
        ```

### 2. Running with Docker Compose (Recommended)
Docker Compose automatically handles relative paths for your database and credentials.

1.  **Create a `.env` file** in the project root and add your Gemini key:
    ```env
    GEMINI_API_KEY=your_actual_api_key_here
    ```
2.  **Ensure your `creds/key.json` is present** (from Step 1B).
3.  **Start the application**:
    ```bash
    docker compose up -d --build
    ```
4.  Access the UI at `http://localhost:8080`.

### 3. Local Development (No Docker)
```bash
dotnet run --project src/HomeStoq.Server/HomeStoq.Server.csproj
```
Access the UI at `http://localhost:8080`.

## 📖 Usage

For a detailed, day-to-day guide on how to use HomeStoq effectively, check out our **[Practical Usage Guide (USAGE.md)](USAGE.md)**.

1.  **Inventory**: Use the "Stock" tab to see what you have. Use the `+` and `-` buttons for manual adjustments.
2.  **Scan Receipts**: Go to the "Scan" tab, upload an image of a grocery receipt, and wait for Gemini to parse the items. The inventory will update automatically.
3.  **Voice Commands**: Create a list named **"HomeStoq"** in Google Tasks. Add tasks like "used 2 milk" or "bought 5 apples". The server polls this list every 10 seconds, parses the text via AI, updates the stock, and deletes the task.
4.  **Shopping List**: Click "Analyze Patterns" in the "Smart List" tab to see AI-generated suggestions based on your history.

## 📝 License
MIT License. See [LICENSE](LICENSE) for details.
