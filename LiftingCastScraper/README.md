# LiftingCast → OpenPowerlifting Scraper


This project scrapes lifter names from a LiftingCast roster page, looks up each person on OpenIPF / OpenPowerlifting, and generates a clean HTML report summarizing their meet history.

It includes:

Async scraper for OpenIPF (aiohttp)

Selenium scraper for LiftingCast (needed because LC is a React SPA)

A Jinja2-based HTML report generator

**🚨 Important Notes**

LiftingCast is a React Single-Page App, meaning HTML is not present until the JS executes.
→ You must use a Javascript-capable browser (Selenium or Playwright).

This project is structured as a proper Python package under src/

**Important**: LiftingCast is a React SPA; scraping requires a JS-capable browser (Selenium or Playwright).

**Project Structure**
LiftingCastProject/
│
├── LiftingCastScraper/
│   ├── Dockerfile
│   ├── requirements.txt
│   ├── pyproject.toml
│   ├── src/
│   │   └── liftingcastscraper/
│   │       ├── main.py
│   │       ├── pipeline.py
│   │       ├── server/
│   │       └── scraper/
│   └── output/
│
└── lifting-extension/


### Quick start


1. Create and activate a virtual environment:


# Create and activate virtual environment
```bash
cd LiftingCastScraper:
python3 -m venv .venv

2. Activate venv - Every time you open a new terminal and want to work on the project:

cd ~/PATH/TO/LiftingCastScraper, not the src/liftingcastscraper/ 
source .venv/bin/activate

2. Install Dependencies

pip install -r requirements.txt

3. Ensure Python can see src
export PYTHONPATH=src

Running the Pipeline Locally:

1. From project root:
python -m liftingcastscraper.main

# You can also edit main.py to change the meet URL you want to scrape.

Output HTML reports will be written to:

output/report_<slugified_meet_url>.html

Running the FastAPI server locally

1. From project root:
uvicorn liftingcastscraper.server.main:app --reload --port 8000

2.
Test using curl (macOS version):
curl -X POST \
  -H "Content-Type: application/json" \
  -d '{"meet_url":"https://liftingcast.com/meets/<MEET>/roster"}' \
  http://127.0.0.1:8000/api/report

Docker (local test)
1. From project root
docker build -t liftingcast-backend .
docker run -p 8000:8000 liftingcast-backend

2.
Test via:
http://localhost:8000/healthz


FYI:
pyproject.toml is the same type of file as .csproj(C# / .NET)
package.json (Node)
It defines the project, lists dependencies, specifies build settings, contains metadata (name, version, description), controls how tools build/install the project