using Microsoft.AspNetCore.Http;

namespace BatchPlant.Api
{
    /// <summary>
    /// The operator console, served as a single embedded page.
    ///
    /// Embedded rather than a wwwroot folder on purpose: the simulator ships as a container built
    /// from a Dockerfile that copies only the publish output, so a static-files directory would
    /// need csproj content globs AND a Dockerfile change AND would still be one forgotten COPY
    /// away from a 404 in production. A const string cannot go missing.
    /// </summary>
    public partial class ApiServer
    {
        private IResult HandleSimUi() => Results.Content(SimUiHtml, "text/html; charset=utf-8");

        private const string SimUiHtml = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Plant Simulator — Vibration</title>
<style>
  :root{
    --bg:#0f1216; --panel:#161b22; --line:#252c36; --ink:#e6edf3; --muted:#8b949e;
    --ok:#3fb950; --warn:#d29922; --crit:#f85149; --accent:#58a6ff;
  }
  *{box-sizing:border-box}
  body{margin:0;background:var(--bg);color:var(--ink);
       font:14px/1.5 ui-monospace,SFMono-Regular,Menlo,Consolas,monospace}
  header{padding:16px 20px;border-bottom:1px solid var(--line);display:flex;
         align-items:baseline;gap:16px;flex-wrap:wrap}
  h1{margin:0;font-size:16px;letter-spacing:.02em}
  .sub{color:var(--muted);font-size:12px}
  .spacer{flex:1}
  button{font:inherit;background:var(--panel);color:var(--ink);border:1px solid var(--line);
         border-radius:6px;padding:6px 12px;cursor:pointer}
  button:hover{border-color:var(--accent)}
  button.primary{background:var(--accent);border-color:var(--accent);color:#04121f;font-weight:600}
  button.danger{border-color:var(--crit);color:var(--crit)}
  main{padding:20px;display:grid;gap:20px;max-width:1400px}
  .area{border:1px solid var(--line);border-radius:8px;overflow:hidden}
  .area>h2{margin:0;padding:10px 14px;background:var(--panel);font-size:12px;
           text-transform:uppercase;letter-spacing:.09em;color:var(--muted);
           border-bottom:1px solid var(--line)}
  table{width:100%;border-collapse:collapse}
  th{ text-align:left;font-size:11px;text-transform:uppercase;letter-spacing:.08em;
      color:var(--muted);padding:8px 14px;border-bottom:1px solid var(--line);font-weight:600}
  td{padding:8px 14px;border-bottom:1px solid var(--line);vertical-align:middle}
  tr:last-child td{border-bottom:0}
  .name{font-weight:600}
  .tag{color:var(--muted);font-size:12px}
  .num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
  .bar{position:relative;height:6px;background:#0b0e12;border-radius:3px;overflow:hidden;min-width:90px}
  .bar>i{position:absolute;inset:0 auto 0 0;background:var(--ok);border-radius:3px}
  .s-ok{color:var(--ok)} .s-warn{color:var(--warn)} .s-crit{color:var(--crit)}
  .pill{display:inline-block;padding:1px 8px;border-radius:99px;font-size:11px;
        border:1px solid var(--line);color:var(--muted)}
  .pill.on{border-color:var(--crit);color:var(--crit)}
  .controls{display:flex;gap:6px;align-items:center;flex-wrap:wrap}
  select,input[type=number]{font:inherit;background:#0b0e12;color:var(--ink);
        border:1px solid var(--line);border-radius:6px;padding:4px 6px}
  input[type=number]{width:64px}
  .empty{padding:24px;color:var(--muted)}
  footer{padding:12px 20px;color:var(--muted);font-size:12px;border-top:1px solid var(--line)}
</style>
</head>
<body>
<header>
  <h1>Plant Simulator — Vibration</h1>
  <span class="sub" id="meta">connecting…</span>
  <span class="spacer"></span>
  <label class="sub"><input type="checkbox" id="auto" checked> auto-refresh</label>
  <button id="refresh">Refresh</button>
  <button id="clearAll" class="danger">Clear all faults</button>
</header>
<main id="root"><div class="empty">Loading assets…</div></main>
<footer>
  Severity is a multiplier on the asset's healthy vibration.
  <strong>Step</strong> jumps and holds · <strong>Ramp</strong> climbs over the ramp window ·
  <strong>Spike</strong> bursts intermittently.
  Duration 0 = until cleared. Faults only apply while the asset is running.
</footer>
<script>
const $ = s => document.querySelector(s);
let assets = [];

function sev(a){
  if(!a.anomaly) return a.factor > 1.5 ? 'warn' : 'ok';
  return a.factor >= 3 ? 'crit' : 'warn';
}
function row(a){
  const cls = sev(a);
  const pct = Math.max(2, Math.min(100, (a.vibration / 12) * 100));
  const colour = cls === 'crit' ? 'var(--crit)' : cls === 'warn' ? 'var(--warn)' : 'var(--ok)';
  const an = a.anomaly;
  return `<tr data-id="${a.assetId}">
    <td><div class="name">${a.name}</div><div class="tag">${a.vibrationTag || a.assetId}</div></td>
    <td class="num s-${cls}">${a.vibration.toFixed(2)}<span class="tag"> mm/s</span></td>
    <td class="num tag">${a.healthyVibration.toFixed(2)}</td>
    <td style="width:120px"><div class="bar"><i style="width:${pct}%;background:${colour}"></i></div></td>
    <td class="num tag">${a.bearingTemp.toFixed(1)}°C</td>
    <td class="num tag">${a.mcsaIndex.toFixed(0)}%</td>
    <td>${an ? `<span class="pill on">${an.mode} ×${an.severity}${an.durationMinutes>0?` · ${Math.max(0,(an.durationMinutes-an.elapsedMinutes)).toFixed(0)}m left`:''}</span>`
              : '<span class="pill">healthy</span>'}</td>
    <td>
      <div class="controls">
        <select class="mode">
          <option>Step</option><option selected>Ramp</option><option>Spike</option>
        </select>
        <input type="number" class="sevr" value="3" min="1" max="25" step="0.5" title="severity multiplier">
        <input type="number" class="dur" value="0" min="0" step="5" title="duration, simulated minutes (0 = until cleared)">
        <button class="inject primary">Inject</button>
        <button class="clear">Clear</button>
      </div>
    </td></tr>`;
}
function render(){
  const byArea = {};
  for(const a of assets){ (byArea[a.area || 'Other'] ||= []).push(a); }
  const areas = Object.keys(byArea).sort();
  $('#root').innerHTML = areas.map(ar => `
    <section class="area"><h2>${ar} — ${byArea[ar].length} assets</h2>
      <table><thead><tr>
        <th>Asset</th><th class="num">Vibration</th><th class="num">Healthy</th><th></th>
        <th class="num">Bearing</th><th class="num">MCSA</th><th>State</th><th>Inject fault</th>
      </tr></thead><tbody>${byArea[ar].map(row).join('')}</tbody></table>
    </section>`).join('');
}
async function load(){
  try{
    const r = await fetch('/api/vibration/assets');
    const d = await r.json();
    assets = d.assets || [];
    $('#meta').textContent = `${d.count} monitored assets · plant time ${new Date(d.plantTime).toLocaleString()}`;
    render();
  }catch(e){ $('#meta').textContent = 'simulator unreachable: ' + e.message; }
}
async function post(url, body){
  const r = await fetch(url, {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(body)});
  if(!r.ok){ const t = await r.text(); alert('Failed: ' + t); }
  await load();
}
document.addEventListener('click', async ev => {
  const tr = ev.target.closest('tr'); 
  if(ev.target.id === 'refresh') return load();
  if(ev.target.id === 'clearAll') return post('/api/vibration/anomaly/clear', {all:true});
  if(!tr) return;
  const id = tr.dataset.id;
  if(ev.target.classList.contains('clear')) return post('/api/vibration/anomaly/clear', {assetId:id});
  if(ev.target.classList.contains('inject')){
    return post('/api/vibration/anomaly', {
      assetId:id,
      mode: tr.querySelector('.mode').value,
      severity: parseFloat(tr.querySelector('.sevr').value),
      rampMinutes: 30,
      durationMinutes: parseFloat(tr.querySelector('.dur').value),
      reason: 'injected from operator console'
    });
  }
});
load();
setInterval(() => { if($('#auto').checked) load(); }, 3000);
</script>
</body>
</html>
""";
    }
}
