"""
Generate a human-friendly HTML report using Jinja2.
The report contains:
 - Lifter name
 - Link to LiftingCast roster link
 - OpenPowerlifting profile link (if we have an ID or can create a search link)
 - Table of top meet results if available
"""

from jinja2 import Template
from typing import List, Dict, Any
import os
import datetime

TEMPLATE = """
<!doctype html>
<html>
<head>
  <meta charset="utf-8"/>
  <title>Lifter Report</title>
  <style>
    body { font-family: Arial, sans-serif; max-width: 980px; margin: 20px auto; }
    h1 { font-size: 24px; }
    table { border-collapse: collapse; width: 100%; margin-bottom: 1.2rem;}
    th, td { border: 1px solid #ddd; padding: 8px; }
    th { background: #f3f3f3; text-align: left; }
    .person { margin-bottom: 1.4rem; padding: 8px; border-radius:4px; background:#fcfcfc; }
    .link { font-size:0.95rem; color:#1a73e8; }
  </style>
</head>
<body>
  <h1>LiftingCast → OpenPowerlifting Report</h1>
  <p>Generated: {{ now }}</p>

  {% for p in people %}
  <div class="person">
    <h2>{{ p.name }}</h2>
    <p>
      LiftingCast: <a class="link" href="{{ p.liftingcast_href }}" target="_blank">{{ p.liftingcast_href }}</a><br/>
      OpenPowerlifting: 
      {% if p.opl_profile %}
        <a class="link" href="{{ p.opl_profile }}" target="_blank">{{ p.opl_profile }}</a>
      {% else %}
        <em>No profile found</em> —
        <a class="link" href="https://www.openpowerlifting.org/search?name={{ p.name | urlencode }}" target="_blank">Search on OPL</a>
      {% endif %}
    </p>

    {% if p.opl_summary %}
      <h3>Summary</h3>
      <table>
        <thead><tr><th>Meet</th><th>Date</th><th>Total</th><th>Squat</th><th>Bench</th><th>Deadlift</th></tr></thead>
        <tbody>
        {% for r in p.opl_summary %}
          <tr>
            <td>{{ r.get('Meet name', '') }}</td>
            <td>{{ r.get('Date', '') }}</td>
            <td>{{ r.get('Total', '') }}</td>
            <td>{{ r.get('Best3SquatKg', r.get('Squat', '')) }}</td>
            <td>{{ r.get('Best3BenchKg', r.get('Bench', '')) }}</td>
            <td>{{ r.get('Best3DeadliftKg', r.get('Deadlift', '')) }}</td>
          </tr>
        {% endfor %}
        </tbody>
      </table>
    {% endif %}
  </div>
  {% endfor %}
</body>
</html>
"""

def generate_html_report(people: List[Dict[str, Any]], out_path: str):
    """
    people: list of dicts with keys:
        - name
        - liftingcast_href
        - opl_profile (optional)
        - opl_summary (optional: list of rows/dicts)
    """
    tpl = Template(TEMPLATE)
    rendered = tpl.render(people=people, now=datetime.datetime.utcnow().isoformat() + "Z")
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(rendered)
    return out_path
