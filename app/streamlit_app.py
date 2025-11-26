"""
Streamlit app: paste a LiftingCast roster link, click 'Fetch', shows a quick table and download HTML report.
Run:
    streamlit run app/streamlit_app.py
"""

import streamlit as st
from scraper.selenium_scraper import scrape_liftingcast_roster
from opl.opl_api import find_first_match_from_api
from reports.html_report import generate_html_report
import tempfile
import webbrowser

st.set_page_config(page_title="LiftingCast → OpenPowerlifting", layout="wide")

st.title("LiftingCast → OpenPowerlifting lookup")

url = st.text_input("Paste LiftingCast roster URL", value="https://liftingcast.com/meets/mfnfcu3cri6q/roster")

if st.button("Fetch and build report"):
    with st.spinner("Loading roster (Selenium) — this launches a headless browser..."):
        try:
            lifters = scrape_liftingcast_roster(url)
        except Exception as e:
            st.error(f"Failed to scrape roster: {e}")
            lifters = []

    if not lifters:
        st.warning("No lifters found. Check the roster URL or try Playwright option.")
    else:
        st.write(f"Found {len(lifters)} lifters.")
        people = []
        for name, href in lifters:
            match = find_first_match_from_api(name)
            opl_profile = match.get("url") if match else None
            people.append({
                "name": name,
                "liftingcast_href": href,
                "opl_profile": opl_profile,
                "opl_summary": None
            })

        st.dataframe(people)
        # create HTML report and provide download link
        tmpdir = tempfile.mkdtemp()
        out = generate_html_report(people, out_path=f"{tmpdir}/lifter_report_streamlit.html")
        st.success("Report generated")
        st.markdown(f"[Open report file]({out})")
        # Optionally open in new tab (local)
        if st.button("Open report in browser"):
            webbrowser.open(out)
