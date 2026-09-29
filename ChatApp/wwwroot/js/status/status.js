(() => {
  const list = document.getElementById("statusList");
  if (!list) return;
  const esc = (s) =>
    String(s ?? "").replace(
      /[&<>'"]/g,
      (c) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          "'": "&#39;",
          '"': "&quot;",
        })[c],
    );
  const mediaInput = document.getElementById("statusMediaFile");
  const preview = document.getElementById("statusPreview");
  const errorBox = document.getElementById("statusError");
  let selectedFile = null;
  const showError = (m) => {
    errorBox.textContent = m || "Ocorreu um erro.";
    errorBox.classList.remove("d-none");
  };
  const clearError = () => {
    errorBox.classList.add("d-none");
    errorBox.textContent = "";
  };
  mediaInput?.addEventListener("change", () => {
    selectedFile = mediaInput.files?.[0] || null;
    preview.innerHTML = "";
    if (!selectedFile) return;
    const url = URL.createObjectURL(selectedFile);
    preview.innerHTML = selectedFile.type.startsWith("video/")
      ? `<video class="status-preview-media" controls src="${url}"></video>`
      : `<img class="status-preview-media" src="${url}" alt="Pré-visualização">`;
    document.getElementById("statusMediaLabel").innerHTML =
      `<i class="bi bi-check-circle me-2"></i>${esc(selectedFile.name)}`;
  });
  async function markViewed(id) {
    try {
      await fetch(`/api/status/${id}/view`, { method: "POST" });
    } catch {}
  }
  async function load() {
    const r = await fetch("/api/status");
    if (!r.ok) return;
    const data = await r.json();
    list.innerHTML = data.length
      ? data
          .map(
            (s) =>
              `<div class="col-sm-6 col-lg-4"><button type="button" class="status-card shadow-sm border-0 h-100 w-100 text-start bg-body" data-status-id="${s.id}"><div class="card-body"><div class="d-flex align-items-center gap-2 mb-3"><img class="status-avatar" src="${esc(s.userPhotoUrl || "/images/default-avatar.png")}" onerror="this.src='/images/default-avatar.png'" alt=""><div><strong>${esc(s.userName)}</strong><div class="small text-muted">${new Date(s.createdAt).toLocaleString()}</div></div></div>${s.mediaUrl && s.mediaType === "image" ? `<img class="status-media" src="${esc(s.mediaUrl)}" alt="Status">` : ""}${s.mediaUrl && s.mediaType === "video" ? `<video class="status-media" controls preload="metadata" src="${esc(s.mediaUrl)}"></video>` : ""}<p class="mb-2">${esc(s.text || "")}</p><small class="text-muted"><i class="bi bi-eye me-1"></i><span data-view-count>${s.viewCount}</span> visualizações</small></div></button></div>`,
          )
          .join("")
      : '<div class="col-12 text-center text-muted py-5">Nenhum status disponível.</div>';
    list.querySelectorAll("[data-status-id]").forEach((card) =>
      card.addEventListener("click", async () => {
        const id = Number(card.dataset.statusId);
        const s = data.find((x) => x.id === id);
        if (!s) return;
        if (!s.viewed) {
          await markViewed(id);
          s.viewed = true;
          s.viewCount = (s.viewCount || 0) + 1;
          card.querySelector("[data-view-count]").textContent = s.viewCount;
        }
      }),
    );
  }
  document
    .getElementById("createStatusBtn")
    ?.addEventListener("click", async () => {
      clearError();
      const btn = document.getElementById("createStatusBtn");
      const text = document.getElementById("statusText").value.trim();
      if (!text && !selectedFile)
        return showError("Adiciona texto ou uma foto/vídeo.");
      btn.disabled = true;
      btn.innerHTML =
        '<span class="spinner-border spinner-border-sm me-1"></span>A publicar...';
      try {
        let mediaUrl = null,
          mediaType = "none";
        if (selectedFile) {
          const fd = new FormData();
          fd.append("file", selectedFile);
          const u = await fetch("/api/uploads/status-media", {
            method: "POST",
            body: fd,
          });
          const ud = await u.json().catch(() => ({}));
          if (!u.ok)
            throw new Error(
              ud.message || "Não foi possível carregar o ficheiro.",
            );
          mediaUrl = ud.url;
          mediaType = selectedFile.type.startsWith("video/")
            ? "video"
            : "image";
        }
        const r = await fetch("/api/status", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ text, mediaUrl, mediaType }),
        });
        const d = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(d.message || "Erro ao publicar.");
        bootstrap.Modal.getInstance(
          document.getElementById("statusModal"),
        )?.hide();
        document.getElementById("statusText").value = "";
        if (mediaInput) mediaInput.value = "";
        selectedFile = null;
        preview.innerHTML = "";
        document.getElementById("statusMediaLabel").innerHTML =
          '<i class="bi bi-images me-2"></i>Escolher foto ou vídeo da galeria';
        await load();
      } catch (e) {
        showError(e.message);
      } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-send me-1"></i>Publicar';
      }
    });
  load();
})();
