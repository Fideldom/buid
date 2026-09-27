(() => {
  const list = document.getElementById('groupsList');
  if (!list) return;
  const esc = s => String(s ?? '').replace(/[&<>'"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));
  const photoInput = document.getElementById('groupPhotoFile');
  const preview = document.getElementById('groupPhotoPreview');
  const errorBox = document.getElementById('groupCreateError');

  function showError(message) { errorBox.textContent = message || 'Ocorreu um erro.'; errorBox.classList.remove('d-none'); }
  function clearError() { errorBox.textContent = ''; errorBox.classList.add('d-none'); }

  photoInput?.addEventListener('change', () => {
    const file = photoInput.files?.[0];
    if (!file) return;
    if (!file.type.startsWith('image/')) { showError('Seleciona uma imagem válida.'); photoInput.value = ''; return; }
    const url = URL.createObjectURL(file);
    preview.innerHTML = `<img src="${url}" alt="Pré-visualização">`;
  });

  async function load() {
    const r = await fetch('/api/groups');
    if (!r.ok) { list.innerHTML = '<div class="col-12"><div class="alert alert-danger">Não foi possível carregar os grupos.</div></div>'; return; }
    const groups = await r.json();
    list.innerHTML = groups.length ? groups.map(g => `<div class="col-md-6 col-xl-4"><a class="text-decoration-none" href="/Groups/${g.id}"><div class="card h-100 shadow-sm border-0"><div class="card-body"><div class="d-flex gap-3 align-items-center"><div class="group-avatar">${g.photoUrl ? `<img src="${esc(g.photoUrl)}" alt="">` : esc((g.name || 'G')[0].toUpperCase())}</div><div class="flex-grow-1"><h5 class="mb-1 text-dark">${esc(g.name)}</h5><p class="text-muted mb-1">${esc(g.description || 'Sem descrição')}</p><small class="text-muted"><i class="bi bi-people me-1"></i>${g.memberCount} membros</small></div></div></div></div></a></div>`).join('') : '<div class="col-12"><div class="text-center text-muted py-5">Ainda não tens grupos.</div></div>';
  }

  document.getElementById('createGroupBtn')?.addEventListener('click', async () => {
    clearError();
    const btn = document.getElementById('createGroupBtn');
    const name = document.getElementById('groupName').value.trim();
    const description = document.getElementById('groupDescription').value.trim();
    const file = photoInput?.files?.[0] || null;
    if (!name) return showError('Indica o nome do grupo.');
    if (name.length < 2) return showError('O nome do grupo deve ter pelo menos 2 caracteres.');
    btn.disabled = true; btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>A criar...';
    try {
      let photoUrl = null;
      if (file) {
        const fd = new FormData(); fd.append('file', file);
        const upload = await fetch('/api/uploads/image', { method: 'POST', body: fd });
        const ud = await upload.json().catch(() => ({}));
        if (!upload.ok) throw new Error(ud.message || 'Não foi possível carregar a foto do grupo.');
        photoUrl = ud.url;
      }
      const r = await fetch('/api/groups', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({name, description, photoUrl, memberIds:[]}) });
      const data = await r.json().catch(() => ({}));
      if (!r.ok) throw new Error(data.detail || data.message || 'Não foi possível criar o grupo.');
      bootstrap.Modal.getInstance(document.getElementById('createGroupModal'))?.hide();
      document.getElementById('groupName').value=''; document.getElementById('groupDescription').value='';
      if (photoInput) photoInput.value=''; preview.innerHTML='<i class="bi bi-camera fs-3"></i>';
      await load();
    } catch (err) { showError(err.message); }
    finally { btn.disabled = false; btn.innerHTML = '<i class="bi bi-people me-1"></i>Criar grupo'; }
  });
  load();
})();
