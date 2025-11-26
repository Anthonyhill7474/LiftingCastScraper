"""Helpers to query OpenPowerlifting (API + CSV fallback).


Note: Public endpoints and CSV schema may change; this module isolates OPL-related logic.
"""


from typing import Optional, List, Dict
import requests
import pandas as pd
import urllib.parse
import logging


logger = logging.getLogger(__name__)


OPL_SEARCH_API = "https://api.openpowerlifting.org/api/lifter?name="




def query_opl_api_by_name(name: str) -> Optional[List[Dict]]:
    """Query the OPL API; return JSON list or None on failure."""
    try:
        url = OPL_SEARCH_API + urllib.parse.quote_plus(name)
        resp = requests.get(url, timeout=10)
        resp.raise_for_status()
        return resp.json()
    except Exception as e:
        logger.warning("OPL API request failed: %s", e)
        return None




def find_first_match_from_api(name: str) -> Optional[Dict]:
    data = query_opl_api_by_name(name)
    if data:
    # Data format depends on endpoint; we return the first item
        return data[0]
    return None




def load_opl_csv(path: str) -> pd.DataFrame:
    """Load OPL CSV (download separately) and return DataFrame."""
    return pd.read_csv(path, low_memory=False)




def find_matches_in_csv(name: str, df: pd.DataFrame) -> pd.DataFrame:
    return df[df['Name'].str.strip().str.lower() == name.strip().lower()]