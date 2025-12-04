const API_BASE = "https://liftingcastscraper.onrender.com";

const STORAGE_KEY = "liftingcast_state_v1";

const FEMALE_CLASSES = ["47", "52", "57", "63", "69", "76", "84", "84+"];
const MALE_CLASSES   = ["59", "66", "74", "83", "93", "105", "120", "120+"];

let state = {
  people: [],
  filterGender: "all", // "all" | "f" | "m"
  filterWeight: "all"  // IPF class string, e.g. "63"
};

document.addEventListener("DOMContentLoaded", () => {
  const runBtn       = document.getElementById("run-btn");
  const meetUrlInput = document.getElementById("meet-url");
  const weightFilter = document.getElementById("weight-filter");
  const genderFilter = document.getElementById("gender-filter");

  runBtn.addEventListener("click", () => {
    const url = meetUrlInput.value.trim();
    if (!url) {
      setStatus("Please enter a LiftingCast roster URL.", "error");
      return;
    }
    fetchReport(url);
  });

  genderFilter.addEventListener("change", () => {
    state.filterGender = genderFilter.value;

    // If switching back to "all", reset the weight filter to "all" too.
    if (state.filterGender === "all") {
      state.filterWeight = "all";
    }

    rebuildWeightFilter();

    // Ensure the <select> reflects the current state
    const weightSelect = document.getElementById("weight-filter");
    weightSelect.value = state.filterWeight;

    renderPeople();
    saveState();
  });

  weightFilter.addEventListener("change", () => {
    state.filterWeight = weightFilter.value;
    renderPeople();
    saveState();
  });

  // Try to restore previous session
  loadStateFromStorage();
});

/* ---------- status helper ---------- */

function setStatus(message, type = "info") {
  const statusEl = document.getElementById("status");
  statusEl.textContent = message;
  statusEl.className = type;
}

/* ---------- storage helpers ---------- */

function saveState() {
  try {
    if (!chrome || !chrome.storage || !chrome.storage.local) return;

    const meetUrlInput = document.getElementById("meet-url");
    const payload = {
      people: state.people,
      filterGender: state.filterGender,
      filterWeight: state.filterWeight,
      lastMeetUrl: meetUrlInput.value.trim(),
      timestamp: Date.now(), // <— important for expiry logic
    };

    chrome.storage.local.set({ [STORAGE_KEY]: payload });
  } catch (e) {
    console.warn("Could not save state:", e);
  }
}

function loadStateFromStorage() {
  try {
    if (!chrome || !chrome.storage || !chrome.storage.local) return;

    chrome.storage.local.get(STORAGE_KEY, (res) => {
      const saved = res[STORAGE_KEY];
      if (!saved) return;

      // Expiry check (e.g. 6 hours)
      const MAX_AGE_MS = 6 * 60 * 60 * 1000; // 6 hours
      if (saved.timestamp) {
        const ts =
          typeof saved.timestamp === "string"
            ? new Date(saved.timestamp).getTime()
            : saved.timestamp;
        const ageMs = Date.now() - ts;

        if (Number.isFinite(ageMs) && ageMs > MAX_AGE_MS) {
          console.log("Cached lifting data expired, clearing.");
          chrome.storage.local.remove(STORAGE_KEY);
          return;
        }
      }

      state.people       = saved.people || [];
      state.filterGender = saved.filterGender || "all";
      state.filterWeight = saved.filterWeight || "all";

      const meetUrlInput = document.getElementById("meet-url");
      const genderFilter = document.getElementById("gender-filter");
      const weightFilter = document.getElementById("weight-filter");

      if (saved.lastMeetUrl) {
        meetUrlInput.value = saved.lastMeetUrl;
      }

      // re-annotate gender (safe even if it was already saved)
      annotateGenderForPeople();
      rebuildWeightFilter();

      genderFilter.value = state.filterGender;

      // ensure selected weight exists in current options
      const hasWeightOption = Array.from(weightFilter.options).some(
        (opt) => opt.value === state.filterWeight
      );
      if (hasWeightOption) {
        weightFilter.value = state.filterWeight;
      } else {
        state.filterWeight = "all";
        weightFilter.value = "all";
      }

      if (state.people.length) {
        document.getElementById("controls").classList.remove("hidden");
        setStatus(
          `Loaded ${state.people.length} athletes from last session.`,
          "info"
        );
        renderPeople();
      }
    });
  } catch (e) {
    console.warn("Could not load state:", e);
  }
}

/* ---------- helpers for meet data ---------- */

function getWeightClass(meet) {
  const raw =
    meet.Class ||
    meet.WeightClassKg ||
    meet.WeightClass ||
    null;
  if (!raw) return null;
  return String(raw).trim();
}

function getBodyweight(meet) {
  const w =
    meet.Weight ||
    meet.BodyweightKg ||
    meet.Bodyweight ||
    null;
  return w ? String(w) : "";
}

function getMeetName(meet) {
  return (
    meet.Competition ||
    meet["Meet name"] ||
    meet.Meet ||
    meet.MeetName ||
    ""
  );
}

function getDots(meet) {
  return (
    meet.Dots ||       // OPL column
    meet.DOTS ||
    meet.GLP ||        // legacy name in some exports
    meet["GLP"] ||
    ""
  );
}

function guessGenderFromClass(cls) {
  if (!cls) return null;
  const c = String(cls).trim();
  if (FEMALE_CLASSES.includes(c)) return "f";
  if (MALE_CLASSES.includes(c)) return "m";
  return null;
}

function annotateGenderForPeople() {
  state.people.forEach((p) => {
    let gender = null;
    (p.opl_summary || []).some((meet) => {
      const g = guessGenderFromClass(getWeightClass(meet));
      if (g) {
        gender = g;
        return true;
      }
      return false;
    });
    p.gender = gender; // "f" | "m" | null
  });
}

function sortWeightClasses(classes, gender) {
  const baseOrder =
    gender === "f"
      ? FEMALE_CLASSES
      : gender === "m"
      ? MALE_CLASSES
      : Array.from(new Set([...FEMALE_CLASSES, ...MALE_CLASSES]));

  return classes.sort((a, b) => {
    const ia = baseOrder.indexOf(a);
    const ib = baseOrder.indexOf(b);
    if (ia === -1 && ib === -1) {
      return parseFloat(a) - parseFloat(b);
    }
    if (ia === -1) return 1;
    if (ib === -1) return -1;
    return ia - ib;
  });
}

/* ---------- API & state ---------- */

async function fetchReport(meetUrl) {
  setStatus("Fetching data…");
  document.getElementById("controls").classList.add("hidden");
  document.getElementById("results").innerHTML = "";

  try {
    const resp = await fetch(`${API_BASE}/api/report`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ meet_url: meetUrl })
    });

    if (!resp.ok) {
      const text = await resp.text();
      throw new Error(`Backend error (${resp.status}): ${text}`);
    }

    const data = await resp.json();
    state.people = data.people || [];

    annotateGenderForPeople();

    setStatus(`Got ${state.people.length} athletes.`, "success");

    rebuildWeightFilter();

    // Make sure dropdown shows the current filter
    const weightFilter = document.getElementById("weight-filter");
    weightFilter.value = state.filterWeight;

    document.getElementById("controls").classList.remove("hidden");
    renderPeople();
    saveState();
  } catch (err) {
    console.error(err);
    setStatus(`Error: ${err.message}`, "error");
  }
}

/* ---------- filters & rendering ---------- */

function rebuildWeightFilter() {
  const select = document.getElementById("weight-filter");
  const weights = new Set();
  const gender = state.filterGender;

  state.people.forEach((p) => {
    if (gender !== "all") {
      if (!p.gender || p.gender !== gender) return;
    }

    (p.opl_summary || []).forEach((meet) => {
      const cls = getWeightClass(meet);
      if (!cls) return;

      const meetGender = guessGenderFromClass(cls);
      if (gender !== "all" && meetGender && meetGender !== gender) {
        return;
      }
      weights.add(String(cls));
    });
  });

  const sorted = sortWeightClasses(Array.from(weights), gender);

  select.innerHTML = `<option value="all">All</option>`;
  for (const w of sorted) {
    const opt = document.createElement("option");
    opt.value = w;
    opt.textContent = `${w} kg`;
    select.appendChild(opt);
  }

  // keep whatever current filterWeight is if possible
  if (!sorted.includes(state.filterWeight)) {
    state.filterWeight = "all";
  }
}

function renderPeople() {
  const container = document.getElementById("results");
  container.innerHTML = "";

  const filtered = state.people.filter((p) => {
    const g = p.gender || null;

    if (state.filterGender !== "all") {
      if (!g || g !== state.filterGender) return false;
    }

    if (state.filterWeight === "all") return true;

    const targetClass = state.filterWeight;
    return (p.opl_summary || []).some((meet) => {
      const cls = getWeightClass(meet);
      return cls && String(cls) === targetClass;
    });
  });

  filtered.forEach((p, idx) => {
    container.appendChild(renderPersonCard(p, idx));
  });
}

function buildOpenPowerliftingUrl(openipfUrl) {
  if (!openipfUrl) return null;
  return openipfUrl.replace("openipf.org", "openpowerlifting.org");
}

function buildLatestSummary(meets) {
  if (!meets || !meets.length) return null;

  const latest = meets[0]; // assume newest first from OPL
  const meetName = getMeetName(latest);
  const date = latest.Date || "";
  const cls = getWeightClass(latest) || "";
  const bw = getBodyweight(latest) || "";
  const total = latest.Total || "";
  const dots = getDots(latest) || "";

  const div = document.createElement("div");
  div.className = "latest-meet";
  div.textContent =
    `Latest: ${date} — ${meetName} ` +
    (cls ? `| Class ${cls}` : "") +
    (bw ? ` | ${bw} kg` : "") +
    (total ? ` | Total ${total}` : "") +
    (dots ? ` | Dots ${dots}` : "");
  return div;
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
  const openipf = person.opl_profile || null;
  const opl = openipf ? buildOpenPowerliftingUrl(openipf) : null;

  let linksHtml = "";
  if (person.liftingcast_href) {
    linksHtml += `<a class="link" href="${person.liftingcast_href}" target="_blank">LiftingCast</a>`;
  }
  if (opl) {
    linksHtml += ` | <a class="link" href="${opl}" target="_blank">OpenPowerlifting</a>`;
  }
  if (openipf) {
    linksHtml += ` | <a class="link" href="${openipf}" target="_blank">OpenIPF</a>`;
  }
  links.innerHTML = linksHtml;
  header.appendChild(links);

  div.appendChild(header);

  const hasSummary = person.opl_summary && person.opl_summary.length;

  // compact latest-meet line
  if (hasSummary) {
    const summary = buildLatestSummary(person.opl_summary);
    if (summary) div.appendChild(summary);
  }

  if (!hasSummary) {
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
      <th>Class</th>
      <th>Weight</th>
      <th>Total</th>
      <th>Dots</th>
      <th>Squat</th>
      <th>Bench</th>
      <th>Deadlift</th>
    </tr>
  `;
  table.appendChild(thead);

  const tbody = document.createElement("tbody");

  meets.forEach((m) => {
    const tr = document.createElement("tr");
    const meetName = getMeetName(m);
    const date = m.Date || "";
    const cls = getWeightClass(m) || "";
    const bw = getBodyweight(m) || "";
    const total = m.Total || "";
    const dots = getDots(m) || "";

    const squat = m.Best3SquatKg ?? m.Squat ?? "";
    const bench = m.Best3BenchKg ?? m.Bench ?? "";
    const deadlift = m.Best3DeadliftKg ?? m.Deadlift ?? "";

    tr.innerHTML = `
      <td>${meetName}</td>
      <td>${date}</td>
      <td>${cls}</td>
      <td>${bw}</td>
      <td>${total}</td>
      <td>${dots}</td>
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
  if (Array.isArray(v)) return `[${v.join(", ")}]`;
  return v ?? "";
}
