"use strict";
function openChannelSettings() {
  if (!ChannelUI.canManageSettings()) {
    showSettingsMessage(
      "Apenas o proprietário pode acessar as configurações.",
      "danger",
    );
    return;
  }
  const c = ChannelUI.getCurrent();
  if (!c) return;
  const box = document.getElementById("channelSettingsContent");
  if (!box) return;
  box.innerHTML = `<label class="form-label">Nome</label><input id="channelSettingName" class="form-control mb-2" maxlength="120" value="${String(c.name || "").replace(/"/g, "&quot;")}"><label class="form-label">Descrição</label><textarea id="channelSettingDescription" class="form-control mb-2" maxlength="500">${String(c.description || "")}</textarea><label class="form-label">Foto</label><input id="channelSettingPhoto" type="file" accept="image/*" class="form-control mb-2"><label class="form-check"><input id="channelSettingPrivate" class="form-check-input" type="checkbox" ${c.isPrivate ? "checked" : ""}> <span class="form-check-label">Canal privado</span></label><div id="channelSettingsError" class="small text-danger mt-2"></div>`;
  bootstrap.Modal.getOrCreateInstance(
    document.getElementById("channelSettingsModal"),
  ).show();
}
document.addEventListener("click", (e) => {
  const b = e.target.closest(
    "#btnChannelSettings,#channelSettingsBtn,#settingsChannelBtn,#channelSettings,#btnSettings,#settingsBtn,.channel-settings-btn,.channel-config-btn",
  );
  if (b) {
    e.preventDefault();
    openChannelSettings();
  }
});
document
  .getElementById("btnSaveChannelSettings")
  ?.addEventListener("click", async () => {
    const c = ChannelUI.getCurrent();
    const err = document.getElementById("channelSettingsError");
    err.textContent = "";
    let photo = c?.photoUrl || null;
    const f = document.getElementById("channelSettingPhoto")?.files?.[0];
    if (f) {
      const fd = new FormData();
      fd.append("file", f);
      const r = await fetch("/api/uploads/image", { method: "POST", body: fd });
      const d = await r.json().catch(() => ({}));
      if (!r.ok) {
        err.textContent = d.message || "Upload falhou.";
        return;
      }
      photo = d.url;
    }
    const r = await fetch(`/api/channels/${c.id}/settings`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: document.getElementById("channelSettingName").value,
        description: document.getElementById("channelSettingDescription").value,
        photoUrl: photo,
        isPrivate: document.getElementById("channelSettingPrivate").checked,
      }),
    });
    const d = await r.json().catch(() => ({}));
    if (!r.ok) {
      err.textContent = d.message || "Não foi possível guardar.";
      return;
    }
    Object.assign(c, d);
    bootstrap.Modal.getInstance(
      document.getElementById("channelSettingsModal"),
    )?.hide();
    window.ChannelUI?.load?.();
  });
function showSettingsMessage(message, type = "info") {
  if (typeof showPostMessage === "function") showPostMessage(message, type);
  else alert(message);
}
window.ChannelSettings = { open: openChannelSettings };
