import requests
from bs4 import BeautifulSoup
from typing import List, Dict, Optional

# thank you to georgehawkins0 for this code, changed it to my style
class Page:
    """A class to represent a page on openipf.org or openpowerlifting.org."""
    BASE_URLS = (
        "https://www.openipf.org/u/",
        "https://www.openpowerlifting.org/u/",
    )
    HEADER_ROW_INDEX = 0
    FIRST_DATA_ROW_INDEX = 1
    SUCCESS_STATUS_CODE = 200

    def __init__(self, url: Optional[str] = None, username: Optional[str] = None, fetch: bool = True) -> None:
        self.fetched = False

        if url:
            self.url = url
        else:
            if not username:
                raise ValueError('Either url or username must be provided')
            self.url = f"https://www.openipf.org/u/{username}"
        
        if not self.url_validator():
            raise ValueError(f"Invalid url: {self.url}")
        
        if fetch:
            self.fetch()
            
    def fetch(self) -> None:
        """Fetch the page data and store it"""
        self.data = self.request()
        self.fetched = True

    @staticmethod
    def extract_lift_attempts(cells):
        """
        Extract float values from lift attempt HTML elements.
        Ignores failures or non-numeric entries.
        """
        attempts = []
        for cell in cells:
            text = cell.text.strip() 
            try:
                attempts.append(float(text))
            except ValueError:
                continue
        return attempts

    def request(self) -> List[Dict]:
        """Send request, parse HTML, return structured table data."""
        response = requests.get(self.url)
        if response.status_code != self.SUCCESS_STATUS_CODE:
            raise ValueError(f"URL returned {response.status_code}: {self.url}")
        
        soup = BeautifulSoup(response.text, 'html.parser')

        tables = soup.find_all("table")
        if len(tables) < 2:
            raise ValueError(f"No data table found at URL: {self.url}")
        
        table = tables[self.FIRST_DATA_ROW_INDEX]

        rows = table.find_all("tr")
        if not rows:
            raise ValueError("No table rows found.")
        
        header_cells = rows[self.HEADER_ROW_INDEX].find_all(['td', 'th'])
        keys = [cell.text.strip() for cell in header_cells]

        data = []

        for row in rows[self.FIRST_DATA_ROW_INDEX:]:
            squat_attempts = self.extract_lift_attempts(row.find_all("td", class_="squat"))
            bench_attempts = self.extract_lift_attempts(row.find_all("td", class_="bench"))
            deadlift_attempts = self.extract_lift_attempts(row.find_all("td", class_="deadlift"))
        
            cells = row.find_all(["td", "th"])
            row_data = [cell.text.strip() for cell in cells]

            d = dict(zip(keys, row_data))
            d["Squat"] = squat_attempts
            d["Bench"] = bench_attempts
            d["Deadlift"] = deadlift_attempts

            data.append(d)
        
        return data
    
    # learning note, when passing in a tuple, python iterates through each element
    def url_validator(self):
        """Check if URL begins with known valid base URLs."""
        return self.url.startswith(self.BASE_URLS)
        
    def get_data(self,refresh_data=False):
        """Return the parsed data"""
        if refresh_data or not self.fetched:
            self.fetch()
        return self.data
    
