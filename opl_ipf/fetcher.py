import requests
from bs4 import BeautifulSoup

class Page:
    '''A class to represent a page on openipf.org or openpowerlifting.org.'''
    def __init__(self, url=None, username=None, fetch=True):
        self.fetched = False
        # if url is not provided, create the url from the username
        self.url = url if url else f'https://www.openipf.org/u/{username}' 
        if not self.url_validator():
            raise ValueError('Invalid url')
        if fetch: 
            self.fetch()

    def fetch(self):
        '''Call the request method and set the data attribute.'''
        self.data = self.request()
        self.fetched = True

    @staticmethod
    def extract_lift_attempts(lifts):
        attempts = []
        for lift in lifts:
            text = lift.text.strip()
            try:
                # Convert to integer if possible
                attempts.append(float(text))
            except ValueError:
                pass
        return attempts

    def request(self):
        '''Make a request to the url and return the data.'''
        r = requests.get(self.url)
        status_code = r.status_code
        if status_code != 200:
            raise ValueError(f'Invalid url {self.url}') 
        data = r.text 
        soup = BeautifulSoup(data, 'html.parser') # Parse the html
        tables = soup.find_all('table') # Find all tables on the page. There are two tables on the page
        table = tables[1] # The second table contains the data we want
        
        key_row = table.find_all('tr')[0]
        keys = [cell.text.strip() for cell in key_row.find_all(['td', 'th'])]

        data = []
        for row in table.find_all('tr')[1:]:
            # Extract row data for squat, bench, and deadlift attempts
            row_data = [cell.text.strip() for cell in row.find_all(['td', 'th'])]
            squat_attempts = Page.extract_lift_attempts(row.find_all('td', class_='squat'))
            bench_attempts = Page.extract_lift_attempts(row.find_all('td', class_='bench'))
            deadlift_attempts = Page.extract_lift_attempts(row.find_all('td', class_='deadlift'))

            # Remove all but one of the squat, bench, and deadlift columns
            for class_name in ['squat', 'bench', 'deadlift']:
                first = True
                for td in row.find_all("td", class_=class_name):
                    if first:
                        first = False
                    else:
                        td.decompose()  # Removes the element from the tree

            # Get row data again just with all but one of the squat, bench, and deadlift columns removed
            # This is for all the other data in the row
            row_data = [cell.text.strip() for cell in row.find_all(['td', 'th'])]
            d = dict(zip(keys, row_data))
            d['Squat'] = squat_attempts
            d['Bench'] = bench_attempts
            d['Deadlift'] = deadlift_attempts
            data.append(d) # Create a dictionary from the keys and adjusted_row_data

        return data
    
    def url_validator(self):
        # Check url in format https://www.openipf.org/u/ or https://www.openpowerlifting.org/u/
        valid = ('https://www.openipf.org/u/','https://www.openpowerlifting.org/u/')
        if self.url.startswith(valid):
            return True
        
    def get_data(self,refresh_data=False):
        '''Return the data attribute.'''
        if not self.fetched:
            self.fetch() # Fetch the data if it hasn't been fetched yet
        return self.data
    
