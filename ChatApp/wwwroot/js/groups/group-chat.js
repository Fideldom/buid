(() => {
  const app = document.getElementById("groupChatApp");
  if (!app) return;
  const id = app.dataset.groupId;
  const messages = document.getElementById("groupMessages");
  const input = document.getElementById("groupMessageInput");
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
  let connection;
  async function load() {
    const r = await fetch(`/api/groups/${id}`);
    if (!r.ok) return;
    const g = await r.json();
    document.getElementById("groupName").textContent = g.name;
    document.getElementById("groupMeta").textContent =
      `${g.memberCount} membros`;
    const mr = await fetch(`/api/groups/${id}/messages`);
    if (mr.ok) {
      const data = await mr.json();
      messages.innerHTML = data.map(render).join("");
      messages.scrollTop = messages.scrollHeight;
    }
  }
  function render(m) {
    return `<div class="group-message"><div class="group-message-avatar">${esc((m.senderName || "U")[0].toUpperCase())}</div><div><div><strong>${esc(m.senderName)}</strong> <small>${new Date(m.sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</small></div><div class="group-message-content">${esc(m.content || "")}</div></div></div>`;
  }
  async function connect() {
    if (!window.signalR) return;
    connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/group")
      .withAutomaticReconnect()
      .build();
    connection.on("GroupMessageReceived", (m) => {
      messages.insertAdjacentHTML("beforeend", render(m));
      messages.scrollTop = messages.scrollHeight;
    });
    connection.on("GroupCallStarted", (data) => {
      if (data?.startedBy !== undefined)
        window.dispatchEvent(
          new CustomEvent("chatapp:incoming-group-call", { detail: data }),
        );
    });
    await connection.start();
    await connection.invoke("JoinGroup", id);
  }
  async function send() {
    const value = input.value.trim();
    if (!value) return;
    if (connection?.state === "Connected") {
      await connection.invoke("SendMessage", id, value);
      input.value = "";
    } else alert("A ligação em tempo real ainda não está pronta.");
  }
  document.getElementById("groupSend")?.addEventListener("click", send);
  input?.addEventListener("keydown", (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      send();
    }
  });
  document
    .getElementById("groupAudioCall")
    ?.addEventListener("click", () =>
      window.dispatchEvent(
        new CustomEvent("chatapp:start-group-call", {
          detail: { groupId: id, type: "audio" },
        }),
      ),
    );
  document
    .getElementById("groupVideoCall")
    ?.addEventListener("click", () =>
      window.dispatchEvent(
        new CustomEvent("chatapp:start-group-call", {
          detail: { groupId: id, type: "video" },
        }),
      ),
    );
  load().then(connect).catch(console.error);
})();
