const API_BASE = "https://liftingcastscraper.onrender.com";
let state = {
  people: [],
  filterWeight: "all"
};

document.addEventListener("DOMContentLoaded", () => {
  const runBtn = document.getElementById("run-btn");
  const meetUrlInput = document.getElementById("meet-url");
  const weightFilter = document.getElementById("weight-filter");

  runBtn.addEventListener("click", () => {
    const url = meetUrlInput.value.trim();
    if (!url) {
      setStatus("Please enter a LiftingCast roster URL.", "error");
      return;
    }
    fetchReport(url);
  });

  weightFilter.addEventListener("change", () => {
    state.filterWeight = weightFilter.value;
    renderPeople();
  });
});

function setStatus(message, type = "info") {
  const statusEl = document.getElementById("status");
  statusEl.textContent = message;
  statusEl.className = type;
}

async function fetchReport(meetUrl) {
  setStatus("Fetching data…");
  document.getElementById("controls").classList.add("hidden");
  document.getElementById("results").innerHTML = "";

  try {
    const resp = await fetch(`${API_BASE}/api/report`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ meet_url: meetUrl })
    });

    if (!resp.ok) {
      const text = await resp.text();
      throw new Error(`Backend error (${resp.status}): ${text}`);
    }

    const data = await resp.json();
    state.people = data.people || [];
    setStatus(`Got ${state.people.length} athletes.`, "success");

    rebuildWeightFilter();
    document.getElementById("controls").classList.remove("hidden");
    renderPeople();
  } catch (err) {
    console.error(err);
    setStatus(`Error: ${err.message}`, "error");
  }
}

function rebuildWeightFilter() {
  const select = document.getElementById("weight-filter");
  const weights = new Set();

  state.people.forEach((p) => {
    (p.opl_summary || []).forEach((meet) => {
      // adjust key if your dict uses a different name
      const wc = meet.WeightClassKg || meet.WeightClass || null;
      if (wc) weights.add(String(wc));
    });
  });

  const sorted = Array.from(weights).sort((a, b) => parseFloat(a) - parseFloat(b));

  select.innerHTML = `<option value="all">All</option>`;
  for (const w of sorted) {
    const opt = document.createElement("option");
    opt.value = w;
    opt.textContent = `${w} kg`;
    select.appendChild(opt);
  }

  state.filterWeight = "all";
}

function renderPeople() {
  const container = document.getElementById("results");
  container.innerHTML = "";

  const filtered = state.people.filter((p) => {
    if (state.filterWeight === "all") return true;
    const wc = state.filterWeight;
    return (p.opl_summary || []).some((meet) => {
      const mWc = meet.WeightClassKg || meet.WeightClass;
      return mWc && String(mWc) === wc;
    });
  });

  filtered.forEach((p, idx) => {
    container.appendChild(renderPersonCard(p, idx));
  });
}

function renderPersonCard(person, index) {
  const div = document.createElement("div");
  div.className = "person";

  const header = document.createElement("div");
  header.className = "person-header";

  const nameEl = document.createElement("div");
  nameEl.className = "person-name";
  nameEl.textContent = person.name;
  header.appendChild(nameEl);

  const links = document.createElement("div");
  links.innerHTML = `
    <a class="link" href="${person.liftingcast_href}" target="_blank">LiftingCast</a>
    ${
      person.opl_profile
        ? ` | <a class="link" href="${person.opl_profile}" target="_blank">OpenIPF</a>`
        : ""
    }
  `;
  header.appendChild(links);

  div.appendChild(header);

  if (!person.opl_summary || person.opl_summary.length === 0) {
    const noData = document.createElement("div");
    noData.textContent = "No OpenIPF data found.";
    noData.style.fontSize = "12px";
    noData.style.marginTop = "4px";
    div.appendChild(noData);
    return div;
  }

  const toggle = document.createElement("button");
  toggle.className = "toggle-btn";
  toggle.textContent = "Show history";
  div.appendChild(toggle);

  const meetsDiv = document.createElement("div");
  meetsDiv.className = "meets";
  meetsDiv.style.display = "none";
  meetsDiv.appendChild(buildMeetsTable(person.opl_summary));
  div.appendChild(meetsDiv);

  toggle.addEventListener("click", () => {
    if (meetsDiv.style.display === "none") {
      meetsDiv.style.display = "block";
      toggle.textContent = "Hide history";
    } else {
      meetsDiv.style.display = "none";
      toggle.textContent = "Show history";
    }
  });

  return div;
}

function buildMeetsTable(meets) {
  const wrapper = document.createElement("div");

  const table = document.createElement("table");
  const thead = document.createElement("thead");
  thead.innerHTML = `
    <tr>
      <th>Meet</th>
      <th>Date</th>
      <th>Weight</th>
      <th>Total</th>
      <th>Squat</th>
      <th>Bench</th>
      <th>Deadlift</th>
    </tr>
  `;
  table.appendChild(thead);

  const tbody = document.createElement("tbody");

  meets.forEach((m) => {
    const tr = document.createElement("tr");
    const meetName = m["Meet name"] || m["Meet"] || "";
    const date = m["Date"] || "";
    const weight = m["WeightClassKg"] || m["WeightClass"] || "";
    const total = m["Total"] || "";

    // falls back to your list-of-attempts fields if present
    const squat = m["Best3SquatKg"] ?? m["Squat"] ?? "";
    const bench = m["Best3BenchKg"] ?? m["Bench"] ?? "";
    const deadlift = m["Best3DeadliftKg"] ?? m["Deadlift"] ?? "";

    tr.innerHTML = `
      <td>${meetName}</td>
      <td>${date}</td>
      <td>${weight}</td>
      <td>${total}</td>
      <td>${formatLiftValue(squat)}</td>
      <td>${formatLiftValue(bench)}</td>
      <td>${formatLiftValue(deadlift)}</td>
    `;
    tbody.appendChild(tr);
  });

  table.appendChild(tbody);
  wrapper.appendChild(table);
  return wrapper;
}

function formatLiftValue(v) {
  // handles either numbers or arrays like [x, y, z]
  if (Array.isArray(v)) return `[${v.join(", ")}]`;
  return v ?? "";
}
