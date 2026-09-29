"use strict";

(() => {
    const app = document.getElementById("groupChatApp");
    if (!app) return;

    const groupId = app.dataset.groupId;
    if (!groupId) return;

    const $ = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? "").replace(/[&<>'"]/g, (char) => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        "'": "&#39;",
        '"': "&quot;"
    })[char]);

    let group = null;
    let searchTimer = null;

    function showOwnerControls(isOwner) {
        const button = $("groupSettingsBtn");
        if (!button) return;

        button.classList.toggle("d-none", !isOwner);
        button.setAttribute("aria-hidden", isOwner ? "false" : "true");
        button.disabled = !isOwner;

        // Never leave the settings modal accessible to a non-owner through stale UI state.
        if (!isOwner) {
            const modal = $("groupSettingsModal");
            if (modal) {
                modal.classList.remove("show");
                modal.setAttribute("aria-hidden", "true");
                modal.style.display = "none";
            }
        }
    }

    async function loadGroup() {
        const response = await fetch(`/api/groups/${encodeURIComponent(groupId)}`, {
            method: "GET",
            credentials: "same-origin",
            cache: "no-store",
            headers: { Accept: "application/json" }
        });

        if (!response.ok) {
            showOwnerControls(false);
            return false;
        }

        group = await response.json();

        const name = $("groupName");
        const meta = $("groupMeta");
        if (name) name.textContent = group.name || "Grupo";
        if (meta) meta.textContent = `${Number(group.memberCount) || 0} membros`;

        showOwnerControls(group.isOwner === true);
        return true;
    }

    async function loadMembers() {
        const box = $("groupMembersList");
        if (!box) return;

        const response = await fetch(`/api/groups/${encodeURIComponent(groupId)}/members`, {
            credentials: "same-origin",
            cache: "no-store",
            headers: { Accept: "application/json" }
        });

        if (!response.ok) {
            box.innerHTML = '<div class="text-danger">Não foi possível carregar os membros.</div>';
            return;
        }

        const members = await response.json();
        box.innerHTML = members.length
            ? members.map((member) => `
                <div class="d-flex align-items-center justify-content-between py-2 border-bottom">
                    <span>
                        ${esc(member.fullName)}
                        <small class="text-muted ms-1">${esc(member.role)}</small>
                    </span>
                    <span class="small ${member.isOnline ? "text-success" : "text-secondary"}">
                        ● ${member.isOnline ? "online" : "offline"}
                    </span>
                </div>
            `).join("")
            : '<div class="text-muted">Sem membros.</div>';
    }

    async function searchUsers(query) {
        const box = $("groupUserResults");
        if (!box) return;

        if (query.length < 2) {
            box.innerHTML = "";
            return;
        }

        const response = await fetch(`/api/channels/users/search?q=${encodeURIComponent(query)}`, {
            credentials: "same-origin",
            cache: "no-store",
            headers: { Accept: "application/json" }
        });

        if (!response.ok) {
            box.innerHTML = '<div class="text-danger small">Não foi possível pesquisar utilizadores.</div>';
            return;
        }

        const users = await response.json();
        box.innerHTML = users.length
            ? users.map((user) => `
                <div class="d-flex justify-content-between align-items-center py-2 border-bottom">
                    <span>${esc(user.fullName || user.userName || "Utilizador")}</span>
                    <button type="button" class="btn btn-sm btn-primary" data-user-id="${esc(user.id)}">
                        Convidar
                    </button>
                </div>
            `).join("")
            : '<div class="text-muted">Nenhum utilizador encontrado.</div>';

        box.querySelectorAll("button[data-user-id]").forEach((button) => {
            button.addEventListener("click", async () => {
                const targetUserId = button.dataset.userId;
                if (!targetUserId) return;

                button.disabled = true;

                try {
                    const response = await fetch(`/api/groups/${encodeURIComponent(groupId)}/invite`, {
                        method: "POST",
                        credentials: "same-origin",
                        headers: {
                            "Content-Type": "application/json",
                            Accept: "application/json"
                        },
                        body: JSON.stringify({ userId: targetUserId })
                    });

                    const data = await response.json().catch(() => ({}));

                    if (!response.ok) {
                        throw new Error(data.message || "Não foi possível enviar o convite.");
                    }

                    button.textContent = "Enviado";
                    button.classList.remove("btn-primary");
                    button.classList.add("btn-success");
                } catch (error) {
                    button.disabled = false;
                    window.alert(error.message || "Não foi possível enviar o convite.");
                }
            });
        });
    }

    async function openSettings() {
        // Always re-read ownership before opening the modal.
        const loaded = await loadGroup();
        if (!loaded || group?.isOwner !== true) {
            showOwnerControls(false);
            return;
        }

        const modalElement = $("groupSettingsModal");
        if (!modalElement || !window.bootstrap?.Modal) return;

        $("groupSettingsName").value = group.name || "";
        $("groupSettingsDescription").value = group.description || "";
        $("groupSettingsPhoto").value = "";
        $("groupSettingsError").textContent = "";

        bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    async function saveSettings() {
        const errorBox = $("groupSettingsError");
        const saveButton = $("saveGroupSettings");

        if (errorBox) errorBox.textContent = "";
        if (group?.isOwner !== true) {
            if (errorBox) errorBox.textContent = "Apenas o criador do grupo pode alterar as configurações.";
            return;
        }

        const name = $("groupSettingsName")?.value.trim() || "";
        const description = $("groupSettingsDescription")?.value.trim() || null;
        const file = $("groupSettingsPhoto")?.files?.[0] || null;

        if (name.length < 2 || name.length > 120) {
            if (errorBox) errorBox.textContent = "O nome deve ter entre 2 e 120 caracteres.";
            return;
        }

        if (description && description.length > 500) {
            if (errorBox) errorBox.textContent = "A descrição não pode ultrapassar 500 caracteres.";
            return;
        }

        if (saveButton) saveButton.disabled = true;

        try {
            let photoUrl = group.photoUrl || null;

            if (file) {
                const formData = new FormData();
                formData.append("file", file);

                const uploadResponse = await fetch("/api/uploads/image", {
                    method: "POST",
                    credentials: "same-origin",
                    body: formData,
                    headers: { Accept: "application/json" }
                });

                const uploadData = await uploadResponse.json().catch(() => ({}));
                if (!uploadResponse.ok) {
                    throw new Error(uploadData.message || "Não foi possível enviar a fotografia.");
                }

                photoUrl = uploadData.url || photoUrl;
            }

            const response = await fetch(`/api/groups/${encodeURIComponent(groupId)}/settings`, {
                method: "PUT",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    Accept: "application/json"
                },
                body: JSON.stringify({
                    name,
                    description,
                    photoUrl
                })
            });

            const data = await response.json().catch(() => ({}));

            if (!response.ok) {
                throw new Error(data.message || "Não foi possível guardar as configurações.");
            }

            group = { ...group, ...data, isOwner: true };
            await loadGroup();

            const modal = $("groupSettingsModal");
            if (modal && window.bootstrap?.Modal) {
                bootstrap.Modal.getInstance(modal)?.hide();
            }
        } catch (error) {
            if (errorBox) errorBox.textContent = error.message || "Ocorreu um erro ao guardar.";
        } finally {
            if (saveButton) saveButton.disabled = false;
        }
    }

    $("groupMembersBtn")?.addEventListener("click", async () => {
        await loadMembers();
        const modal = $("groupMembersModal");
        if (modal && window.bootstrap?.Modal) {
            bootstrap.Modal.getOrCreateInstance(modal).show();
        }
    });

    $("groupUserSearch")?.addEventListener("input", (event) => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => searchUsers(event.target.value.trim()), 300);
    });

    $("groupSettingsBtn")?.addEventListener("click", openSettings);
    $("saveGroupSettings")?.addEventListener("click", saveSettings);

    showOwnerControls(false);
    loadGroup().catch((error) => {
        console.error("Erro ao carregar grupo:", error);
        showOwnerControls(false);
    });
})();
