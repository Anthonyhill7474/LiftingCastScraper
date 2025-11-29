"""Selenium-based scraper for LiftingCast roster pages."""


from typing import List, Tuple
import logging
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.chrome.service import Service
from webdriver_manager.chrome import ChromeDriverManager

# webdriver.Chrome launches a Chrome browser controlled by Selenium
# Options() configures headless mode, flags, etc.
# By - a type of selector (e.g., By.CSS_SELECTOR, XPATH, etc.)
# WebDriverWait - wait for certain conditions to be True
# Service - to manage the ChromeDriver service, tells Selenium where the ChromeDriver binary is
# webdriver_manager - automatically downloads and manages the correct ChromeDriver binary

from .utils import lifter_link_selector, wait_for_lifters_condition


logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)

def get_driver(headless: bool = True): 
    """Create and return a Chrome webdriver using webdriver-manager."""
    options = Options()
    if headless:
    # modern headless; if issues, try "--headless" instead
        options.add_argument("--headless=new") # headless - runs chrome headless, no UI
    options.add_argument("--no-sandbox") # needed for some environments
    options.add_argument("--disable-dev-shm-usage") # overcome limited resource problems
    options.add_argument("window-size=1200,900") # set window size for consistent rendering


    service = Service(ChromeDriverManager().install()) # Selenium 4+ requires Service object specifying driver path, chromeDriverManager downloads the correct ChromeDriver version and returns its path
    driver = webdriver.Chrome(service=service, options=options) # creates/launches Chrome webdriver instance
    return driver




def scrape_liftingcast_roster(url: str, headless: bool = True, timeout: int = 15) -> List[Tuple[str, str]]:
    """Scrape roster from a LiftingCast roster URL.

    Returns list of tuples: (name, href)
    """
    driver = get_driver(headless=headless) # get a Chrome webdriver instance
    try:
        logger.info("Loading %s", url)
        driver.get(url) # loads the page, blocks until initial HTML is loaded

        selector = lifter_link_selector() # retrieve CSS selector for lifter links
        WebDriverWait(driver, timeout).until(wait_for_lifters_condition(selector)) # wait until lifter links are present or timeout

        elements = driver.find_elements(By.CSS_SELECTOR, selector) # returns list of WebElement objects matching the selector
        results = []
        for el in elements:
            # .text returns the visible text inside the anchor (name usually)
            name = el.text.strip()
            href = el.get_attribute("href") # get the href attribute value, profile link
            if name:
                results.append((name, href))
        logger.info("Found %d lifters", len(results)) # c style logging, this is not a string, used for logging efficiency
        return results
    finally: # code always runs after try, even if exception occurs
        driver.quit() # ensure the driver is closed, doesn't leave a zombine chrome process running on the machine