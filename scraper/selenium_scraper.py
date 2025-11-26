"""Selenium-based scraper for LiftingCast roster pages."""


from typing import List, Tuple
import logging
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from webdriver_manager.chrome import ChromeDriverManager


from .utils import lifter_link_selector, wait_for_lifters_condition


logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)




def get_driver(headless: bool = True):
    """Create and return a Chrome webdriver using webdriver-manager."""
    options = Options()
    if headless:
    # modern headless; if issues, try "--headless" instead
        options.add_argument("--headless=new")
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")
    options.add_argument("window-size=1200,900")


    driver = webdriver.Chrome(ChromeDriverManager().install(), options=options)
    return driver




def scrape_liftingcast_roster(url: str, headless: bool = True, timeout: int = 15) -> List[Tuple[str, str]]:
    """Scrape roster from a LiftingCast roster URL.


    Returns list of tuples: (name, href)
    """
    driver = get_driver(headless=headless)
    try:
        logger.info("Loading %s", url)
        driver.get(url)


        selector = lifter_link_selector()
        WebDriverWait(driver, timeout).until(wait_for_lifters_condition(selector))


        elements = driver.find_elements(By.CSS_SELECTOR, selector)
        results = []
        for el in elements:
            # .text returns the visible text inside the anchor (name usually)
            name = el.text.strip()
            href = el.get_attribute("href")
            if name:
                results.append((name, href))
        logger.info("Found %d lifters", len(results))
        return results
    finally:
        driver.quit()