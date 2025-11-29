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


### Quick start


1. Create and activate a virtual environment:


# Create virtual environment
```bash
From project root:
python3 -m venv .venv

2. Activate venv - Every time you open a new terminal and want to work on the project:

cd ~/PATH/TO/LiftingCastScraper, not the src/liftingcastscraper/ 
source .venv/bin/activate

2. Install Dependencies

pip install -r requirements.txt

3. From the project root

python -m liftingcastscraper.main

# You can also edit main.py to change the meet URL you want to scrape.

Output HTML reports will be written to:

output/



FYI:
pyproject.toml is the same type of file as .csproj(C# / .NET)
package.json (Node)
It defines the project, lists dependencies, specifies build settings, contains metadata (name, version, description), controls how tools build/install the project