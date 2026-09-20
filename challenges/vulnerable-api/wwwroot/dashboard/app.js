// Acme admin dashboard. Uses the platform API key to load data.
const API_KEY = "ak_live_5Xf2Qb8Kd3Np7Rt9Vw1Yz";
async function load(){
  const r = await fetch('/api/inventory', { headers: { 'X-Api-Key': API_KEY } });
  const items = await r.json();
  document.getElementById('inv').innerHTML =
    items.map(i => `<li>${i.sku} — ${i.name} (${i.stock} in stock)</li>`).join('');
}
load();
