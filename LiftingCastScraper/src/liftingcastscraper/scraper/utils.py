"""
Scraper utility helpers.
All small functions shared between scrapers live here.
"""

import logging
import re #regular expressions
import os # for direectory creation and file operatinos
import psutil  # for memory usage monitoring
# from selenium.webdriver.common.by import By
# from selenium.webdriver.support import expected_conditions as EC

logger = logging.getLogger(__name__)


def lifter_link_selector() -> str:
    """
    CSS selector that matches lifter anchor elements on LiftingCast:
    find any <a> tag whose href contains '/lifter/'.
    """
    return 'a[href*="/lifter/"]'


def wait_for_lifters_condition(selector: str):
    """
    For Selenium expected_conditions usage: wait for presence of elements.
    Returns a callable function for WebDriverWait.
    """
    def _cond(driver):
        return EC.presence_of_all_elements_located((By.CSS_SELECTOR, selector))(driver)
    return _cond

def slugify(value: str) -> str:
    """
    Turn a URL or string into a filesystem-safe-ish slug.
    """
    # slugify("https://liftingcast.com/meet/2024 Nationals")  → "liftingcast-com-meet-2024-nationals"
    value = value.strip().lower()
    # strip scheme, e.g. http:// or https://
    value = re.sub(r"^https?://", "", value)
    # replace non-alnum with hyphens
    value = re.sub(r"[^a-z0-9]+", "-", value)
    value = value.strip("-")
    return value or "report"

def save_html_report(html: str, path: str):
    # Ensure the output directory exists
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)

    with open(path, "w", encoding="utf-8") as f:
        f.write(html)

    return path


def clean_lifter_name(raw_label: str) -> str:
    """
    Convert something like '105 - Anthony Hill' → 'Anthony Hill'.
    If there's no ' - ', just return the original string.
    """
    if " - " in raw_label:
        _, name = raw_label.split(" - ", 1)
        return name.strip()
    return raw_label.strip()

def normalize_liftingcast_url(url: str) -> str:
    """
    Normalize any LiftingCast meet URL so that it always ends with /roster

    Examples:
        https://liftingcast.com/meets/abc123/roster    -> same
        https://liftingcast.com/meets/abc123/results   -> /roster
        https://liftingcast.com/meets/abc123           -> /roster
        https://liftingcast.com/meets/abc123/          -> /roster
        https://liftingcast.com/meets/abc123/lifter/x  -> /roster

    Requirements:
        - Meet ID always follows `/meets/<id>`
    """
    url = url.strip()

    # Extract the meet ID using a regex
    match = re.search(r"/meets/([^/]+)", url)
    if not match:
        raise ValueError(f"Invalid LiftingCast meet URL: {url}")

    meet_id = match.group(1)

    # Build normalized roster URL
    return f"https://liftingcast.com/meets/{meet_id}/roster"

def log_mem(tag=""):
    p = psutil.Process(os.getpid())
    mem = p.memory_info().rss / (1024 * 1024)
    print(f"[MEM] {tag}: {mem:.1f} MB")