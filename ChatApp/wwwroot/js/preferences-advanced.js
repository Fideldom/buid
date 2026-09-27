(() => {
  const token=()=>document.querySelector('input[name="__RequestVerificationToken"]')?.value||'';
  const apply=(p={})=>{document.documentElement.dataset.theme=p.theme||'system';document.documentElement.style.setProperty('--chatapp-accent',p.accentColor||'#2563eb');document.documentElement.dataset.density=p.uiDensity||'comfortable';document.documentElement.classList.toggle('reduce-motion',!!p.reduceMotion);localStorage.setItem('chatapp.appearance',JSON.stringify(p));};
  async function load(){try{const r=await fetch('/api/preferences');if(!r.ok)return;const p=await r.json();apply(p);window.dispatchEvent(new CustomEvent('chatapp:preferences-advanced',{detail:p}));}catch(e){console.warn(e)}}
  window.ChatAppAdvancedPreferences={load,apply,save:async patch=>{const r=await fetch('/api/preferences',{method:'PUT',headers:{'Content-Type':'application/json','RequestVerificationToken':token()},body:JSON.stringify(patch)});if(!r.ok)throw new Error('Não foi possível guardar a preferência.');await load();}};
  try{const cached=JSON.parse(localStorage.getItem('chatapp.appearance')||'null');if(cached)apply(cached)}catch{} load();
})();
