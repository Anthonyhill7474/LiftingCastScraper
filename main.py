#!/usr/bin/env python3
"""
Main entry point for the LiftingCast → OpenPowerlifting scraper pipeline.
"""
import os

from scraper.selenium_scraper import (
    scrape_liftingcast_roster,
    get_driver,
)

from scraper.utils import (
    save_html_report,
    slugify,
    clean_lifter_name,
)

from opl_ipf.lookup import(
    try_fetch_openipf,
)

from reports.html_report import generate_html_report


BASE_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(BASE_DIR, "output")

def run_pipeline(meet_url: str) -> None:
    print(f"\n🔍 Fetching roster from: {meet_url}")
    roster = scrape_liftingcast_roster(meet_url)
    print(f" → Found {len(roster)} athletes\n")

    people = []

    for entry in roster:
        raw_label, liftingcast_url = entry
        lifter_name = clean_lifter_name(raw_label)

        print(f"🔎 Checking OpenIPF for: {lifter_name}")

        ipf_data = try_fetch_openipf(lifter_name)

        if ipf_data:
            print(f" ✓ Found OpenIPF profile: {ipf_data['profile_url']}")
            opl_profile = ipf_data["profile_url"]
            summary = ipf_data["meet_history"]
        else:
            print(" ✗ No OpenIPF match found")
            opl_profile = None
            summary = None

        people.append(
            {
                "name": lifter_name,
                "liftingcast_href": liftingcast_url,
                "opl_profile": opl_profile,
                "opl_summary": summary,
            }
        )

    print("\n📄 Building HTML report…")
    html = generate_html_report(people)

    filename = os.path.join(OUTPUT_DIR, f"report_{slugify(meet_url)}.html")
    save_html_report(html, filename)

    print(f"✅ Report saved → {filename}")


if __name__ == "__main__":
    example_url = "https://liftingcast.com/meets/mfnfcu3cri6q/roster"
    run_pipeline(example_url)