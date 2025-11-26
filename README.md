# LiftingCast → OpenPowerlifting Scraper


This small project scrapes lifter names from a LiftingCast roster page, looks up each person on OpenPowerlifting, and produces an HTML report. It contains two scrapers: Selenium (synchronous) and Playwright (async). There's also a Streamlit app for a quick UI.


**Important**: LiftingCast is a React SPA; scraping requires a JS-capable browser (Selenium or Playwright).


### Quick start


1. Create and activate a virtual environment:


```bash
python3 -m venv .venv
source .venv/bin/activate


2. Install Dependencies

pip install -r requirements.txt
# if using playwright:
playwright install


3. Run the Selenium pipeline:

python main.py --url "https://liftingcast.com/meets/mfnfcu3cri6q/roster" --engine selenium

# mfnfcu3cri6q is the identifier for a specific competition

4. Or run the Streamlit UI:
streamlit run app/streamlit_app.py