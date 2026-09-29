"use strict";

document.addEventListener("DOMContentLoaded", () => {
  const t = (key) => window.ChatI18n?.t?.(key) || key;

  // NAVEGAÇÃO

  const navItems = document.querySelectorAll(".settings-nav-item");
  const sections = document.querySelectorAll(".settings-section");

  function showSection(sectionName) {
    navItems.forEach((item) => {
      item.classList.toggle("active", item.dataset.section === sectionName);
    });

    sections.forEach((section) => {
      section.classList.toggle(
        "active",
        section.dataset.content === sectionName,
      );
    });

    // Conta / Perfil
    if (sectionName === "account") {
      loadProfile();
    }

    // Privacidade
    if (sectionName === "privacy") {
      initializePrivacySection();
    }
  }

  navItems.forEach((item) => {
    item.addEventListener("click", () => {
      const section = item.dataset.section;

      if (!section) {
        return;
      }

      showSection(section);

      if (window.innerWidth <= 620) {
        item.scrollIntoView({
          behavior: "smooth",
          block: "nearest",
          inline: "center",
        });
      }
    });
  });

  // TOAST

  const toast = document.getElementById("settingsToast");
  const toastIcon = document.getElementById("settingsToastIcon");
  const toastMessage = document.getElementById("settingsToastMessage");

  let toastTimeout = null;

  function showToast(message, success = true) {
    if (!toast || !toastMessage || !toastIcon) {
      return;
    }

    toastMessage.textContent = message;

    toastIcon.className = success
      ? "bi bi-check-circle-fill"
      : "bi bi-exclamation-circle-fill";

    toastIcon.style.color = success ? "#16a34a" : "#dc2626";

    toast.classList.add("show");

    clearTimeout(toastTimeout);

    toastTimeout = setTimeout(() => {
      toast.classList.remove("show");
    }, 3500);
  }

  // CSRF

  function getAntiForgeryToken() {
    const token = document.querySelector(
      'input[name="__RequestVerificationToken"]',
    );

    return token ? token.value : "";
  }

  // CONFIGURAÇÕES GERAIS

  const language = document.getElementById("settingLanguage");

  const timeZone = document.getElementById("settingTimeZone");

  const timeFormat = document.getElementById("settingTimeFormat");

  const saveButton = document.getElementById("btnSaveSettings");

  function setSaveLoading(loading) {
    if (!saveButton) {
      return;
    }

    if (loading) {
      saveButton.classList.add("loading");
      saveButton.disabled = true;

      saveButton.innerHTML = `
                <span class="spinner-border spinner-border-sm"
                      aria-hidden="true"></span>
                <span>${t("settings.saving")}</span>
            `;
    } else {
      saveButton.classList.remove("loading");
      saveButton.disabled = false;

      saveButton.innerHTML = `
                <span>${t("settings.save")}</span>
            `;
    }
  }

  async function loadSettings() {
    try {
      const response = await fetch("/Settings/Get", {
        method: "GET",

        headers: {
          Accept: "application/json",
        },

        credentials: "same-origin",
      });

      if (!response.ok) {
        throw new Error(t("settings.loadError"));
      }

      const data = await response.json();

      if (language) {
        language.value = data.language || "pt";
      }

      if (timeZone) {
        timeZone.value = data.timeZone || "Africa/Luanda";
      }

      if (timeFormat) {
        timeFormat.value = data.timeFormat || "24h";
      }
    } catch (error) {
      console.error("Erro ao carregar configurações:", error);

      showToast(t("settings.loadError"), false);
    }
  }

  async function saveSettings() {
    if (!language || !timeZone || !timeFormat || !saveButton) {
      return;
    }

    const payload = {
      language: language.value,

      timeZone: timeZone.value,

      timeFormat: timeFormat.value,
    };

    setSaveLoading(true);

    try {
      const token = getAntiForgeryToken();

      const headers = {
        "Content-Type": "application/json",

        Accept: "application/json",
      };

      if (token) {
        headers["RequestVerificationToken"] = token;
      }

      const response = await fetch("/Settings/Update", {
        method: "PUT",

        headers,

        credentials: "same-origin",

        body: JSON.stringify(payload),
      });

      let data = null;

      try {
        data = await response.json();
      } catch {
        data = null;
      }

      if (!response.ok) {
        throw new Error(t("settings.saveError"));
      }

      if (data?.settings && window.ChatPreferences?.set) {
        window.ChatPreferences.set(data.settings, {
          persist: true,
          broadcast: true,
        });
      } else if (window.ChatPreferences?.refresh) {
        await window.ChatPreferences.refresh();
      }

      showToast(t("settings.saveSuccess"));
    } catch (error) {
      console.error("Erro ao guardar configurações:", error);

      showToast(error.message || t("settings.saveError"), false);
    } finally {
      setSaveLoading(false);
    }
  }

  if (saveButton) {
    saveButton.addEventListener("click", saveSettings);
  }

  // PERFIL

  const profileFullName = document.getElementById("profileFullName");

  const profileEmail = document.getElementById("profileEmail");

  const profileUserName = document.getElementById("profileUserName");

  const profilePhone = document.getElementById("profilePhone");

  const profileDisplayName = document.getElementById("profileDisplayName");

  const profileDisplayEmail = document.getElementById("profileDisplayEmail");

  const profileCreatedAt = document.getElementById("profileCreatedAt");

  const profileAvatarInitial = document.getElementById("profileAvatarInitial");

  const profileAvatarImage = document.getElementById("profileAvatarImage");

  const saveProfileButton = document.getElementById("btnSaveProfile");

  const cancelProfileButton = document.getElementById("btnCancelProfile");

  const changePhotoButton = document.getElementById("btnChangeProfilePhoto");

  const selectPhotoButton = document.getElementById("btnSelectProfilePhoto");

  const photoInput = document.getElementById("profilePhotoInput");

  let originalProfile = null;

  function getInitial(name) {
    const value = String(name || "").trim();

    return value.charAt(0).toUpperCase() || "U";
  }

  function formatAccountDate(dateValue) {
    if (!dateValue) {
      return "—";
    }

    if (window.ChatPreferences?.formatDate) {
      return window.ChatPreferences.formatDate(dateValue, {
        day: "2-digit",
        month: "long",
        year: "numeric",
      });
    }

    const date = new Date(dateValue);

    if (Number.isNaN(date.getTime())) {
      return "—";
    }

    return date.toLocaleDateString("pt-PT", {
      day: "2-digit",
      month: "long",
      year: "numeric",
    });
  }

  function renderProfile(profile) {
    if (!profile) {
      return;
    }

    if (profileFullName) {
      profileFullName.value = profile.fullName || "";
    }

    if (profileEmail) {
      profileEmail.value = profile.email || "";
    }

    if (profileUserName) {
      profileUserName.value = profile.userName || "";
    }

    if (profilePhone) {
      profilePhone.value = profile.phoneNumber || "";
    }

    if (profileDisplayName) {
      profileDisplayName.textContent = profile.fullName || "Utilizador";
    }

    if (profileDisplayEmail) {
      profileDisplayEmail.textContent = profile.email || "";
    }

    if (profileCreatedAt) {
      profileCreatedAt.textContent = formatAccountDate(profile.createdAt);
    }

    const initial = getInitial(profile.fullName);

    if (profileAvatarInitial) {
      profileAvatarInitial.textContent = initial;
    }

    if (profileAvatarImage && profile.profilePhotoUrl) {
      profileAvatarImage.src = profile.profilePhotoUrl;

      profileAvatarImage.hidden = false;

      if (profileAvatarInitial) {
        profileAvatarInitial.hidden = true;
      }
    } else {
      if (profileAvatarImage) {
        profileAvatarImage.hidden = true;

        profileAvatarImage.removeAttribute("src");
      }

      if (profileAvatarInitial) {
        profileAvatarInitial.hidden = false;
      }
    }
  }

  async function loadProfile() {
    if (!profileFullName) {
      return;
    }

    try {
      const response = await fetch("/Settings/Profile", {
        method: "GET",

        headers: {
          Accept: "application/json",
        },

        credentials: "same-origin",
      });

      if (!response.ok) {
        throw new Error("Não foi possível carregar o perfil.");
      }

      const profile = await response.json();

      originalProfile = {
        ...profile,
      };

      renderProfile(profile);
    } catch (error) {
      console.error("Erro ao carregar perfil:", error);

      showToast(t("settings.profileLoadError"), false);
    }
  }

  function setProfileSaveLoading(loading) {
    if (!saveProfileButton) {
      return;
    }

    if (loading) {
      saveProfileButton.classList.add("loading");

      saveProfileButton.disabled = true;

      saveProfileButton.innerHTML = `
                <span class="spinner-border spinner-border-sm"
                      aria-hidden="true"></span>
                <span>${t("settings.saving")}</span>
            `;
    } else {
      saveProfileButton.classList.remove("loading");

      saveProfileButton.disabled = false;

      saveProfileButton.innerHTML = `
                <span>${t("settings.save")}</span>
            `;
    }
  }

  async function saveProfile() {
    if (!profileFullName) {
      return;
    }

    const fullName = profileFullName.value.trim();

    const phoneNumber = profilePhone ? profilePhone.value.trim() : "";

    if (fullName.length < 2) {
      showToast(t("settings.invalidName"), false);

      profileFullName.focus();

      return;
    }

    const payload = {
      fullName,

      phoneNumber,
    };

    setProfileSaveLoading(true);

    try {
      const token = getAntiForgeryToken();

      const headers = {
        "Content-Type": "application/json",

        Accept: "application/json",
      };

      if (token) {
        headers["RequestVerificationToken"] = token;
      }

      const response = await fetch("/Settings/Profile", {
        method: "PUT",

        headers,

        credentials: "same-origin",

        body: JSON.stringify(payload),
      });

      let data = null;

      try {
        data = await response.json();
      } catch {
        data = null;
      }

      if (!response.ok) {
        throw new Error(t("settings.profileSaveError"));
      }

      if (data?.profile) {
        originalProfile = {
          ...data.profile,
        };

        renderProfile(data.profile);
      }

      showToast(t("settings.profileSaved"));
    } catch (error) {
      console.error("Erro ao atualizar perfil:", error);

      showToast(error.message || t("settings.profileSaveError"), false);
    } finally {
      setProfileSaveLoading(false);
    }
  }

  function cancelProfileChanges() {
    if (!originalProfile) {
      return;
    }

    renderProfile(originalProfile);

    showToast(t("settings.cancelled"));
  }

  if (saveProfileButton) {
    saveProfileButton.addEventListener("click", saveProfile);
  }

  if (cancelProfileButton) {
    cancelProfileButton.addEventListener("click", cancelProfileChanges);
  }

  // FOTO DE PERFIL

  function openPhotoPicker() {
    if (photoInput) {
      photoInput.click();
    }
  }

  if (changePhotoButton) {
    changePhotoButton.addEventListener("click", openPhotoPicker);
  }

  if (selectPhotoButton) {
    selectPhotoButton.addEventListener("click", openPhotoPicker);
  }

  if (profileAvatarImage) {
    profileAvatarImage.addEventListener("error", () => {
      profileAvatarImage.hidden = true;

      if (profileAvatarInitial) {
        profileAvatarInitial.hidden = false;
      }
    });
  }

  async function uploadProfilePhoto(file) {
    if (!file) {
      return;
    }

    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];

    if (!allowedTypes.includes(file.type)) {
      showToast(t("settings.invalidPhoto"), false);

      return;
    }

    const maxSize = 5 * 1024 * 1024;

    if (file.size > maxSize) {
      showToast(t("settings.photoTooLarge"), false);

      return;
    }

    const formData = new FormData();

    formData.append("photo", file);

    try {
      if (selectPhotoButton) {
        selectPhotoButton.disabled = true;
      }

      if (changePhotoButton) {
        changePhotoButton.disabled = true;
      }

      const token = getAntiForgeryToken();

      const headers = {
        Accept: "application/json",
      };

      if (token) {
        headers["RequestVerificationToken"] = token;
      }

      const response = await fetch("/Settings/Profile/Photo", {
        method: "POST",

        headers,

        credentials: "same-origin",

        body: formData,
      });

      let data = null;

      try {
        data = await response.json();
      } catch {
        data = null;
      }

      if (!response.ok) {
        throw new Error(t("settings.photoError"));
      }

      if (data?.url && profileAvatarImage) {
        profileAvatarImage.src =
          data.url + (data.url.includes("?") ? "&" : "?") + "v=" + Date.now();

        profileAvatarImage.hidden = false;

        if (profileAvatarInitial) {
          profileAvatarInitial.hidden = true;
        }
      }

      showToast(t("settings.photoSaved"));
    } catch (error) {
      console.error("Erro ao atualizar foto:", error);

      showToast(error.message || t("settings.photoError"), false);
    } finally {
      if (selectPhotoButton) {
        selectPhotoButton.disabled = false;
      }

      if (changePhotoButton) {
        changePhotoButton.disabled = false;
      }

      if (photoInput) {
        photoInput.value = "";
      }
    }
  }

  if (photoInput) {
    photoInput.addEventListener("change", () => {
      const file = photoInput.files?.[0];

      uploadProfilePhoto(file);
    });
  }

  // PRIVACIDADE

  const privacyElements = {
    profileVisibility: document.getElementById("privacyProfileVisibility"),

    onlineStatus: document.getElementById("privacyOnlineStatus"),

    lastSeen: document.getElementById("privacyLastSeen"),

    messages: document.getElementById("privacyMessages"),

    calls: document.getElementById("privacyCalls"),

    friendRequests: document.getElementById("privacyFriendRequests"),

    discoverable: document.getElementById("privacyDiscoverable"),

    saveButton: document.getElementById("btnSavePrivacy"),
  };

  let privacyLoaded = false;

  async function loadPrivacySettings() {
    try {
      const response = await fetch("/Settings/Privacy", {
        method: "GET",

        headers: {
          Accept: "application/json",
        },

        credentials: "same-origin",
      });

      if (!response.ok) {
        throw new Error(`Erro ao carregar privacidade: ${response.status}`);
      }

      const data = await response.json();

      if (privacyElements.profileVisibility) {
        privacyElements.profileVisibility.value =
          data.profileVisibility || "everyone";
      }

      if (privacyElements.onlineStatus) {
        privacyElements.onlineStatus.value =
          data.onlineStatusVisibility || "everyone";
      }

      if (privacyElements.lastSeen) {
        privacyElements.lastSeen.value = data.lastSeenVisibility || "everyone";
      }

      if (privacyElements.messages) {
        privacyElements.messages.value = data.messagePrivacy || "everyone";
      }

      if (privacyElements.calls) {
        privacyElements.calls.value = data.callPrivacy || "everyone";
      }

      if (privacyElements.friendRequests) {
        privacyElements.friendRequests.value =
          data.friendRequestPrivacy || "everyone";
      }

      if (privacyElements.discoverable) {
        privacyElements.discoverable.checked = data.discoverable !== false;
      }
    } catch (error) {
      console.error("Erro ao carregar configurações de privacidade:", error);

      showToast(t("settings.privacyLoadError"), false);
    }
  }

  function setPrivacySaveLoading(loading) {
    const button = privacyElements.saveButton;

    if (!button) {
      return;
    }

    const normalContent = button.querySelector(".privacy-save-content");

    const loadingContent = button.querySelector(".privacy-save-loading");

    button.disabled = loading;

    if (loading) {
      normalContent?.classList.add("d-none");

      loadingContent?.classList.remove("d-none");
    } else {
      normalContent?.classList.remove("d-none");

      loadingContent?.classList.add("d-none");
    }
  }

  async function savePrivacySettings() {
    const button = privacyElements.saveButton;

    if (!button) {
      return;
    }

    const payload = {
      profileVisibility: privacyElements.profileVisibility?.value || "everyone",

      onlineStatusVisibility: privacyElements.onlineStatus?.value || "everyone",

      lastSeenVisibility: privacyElements.lastSeen?.value || "everyone",

      messagePrivacy: privacyElements.messages?.value || "everyone",

      callPrivacy: privacyElements.calls?.value || "everyone",

      friendRequestPrivacy: privacyElements.friendRequests?.value || "everyone",

      discoverable: privacyElements.discoverable?.checked ?? true,
    };

    setPrivacySaveLoading(true);

    try {
      const token = getAntiForgeryToken();

      const headers = {
        "Content-Type": "application/json",

        Accept: "application/json",
      };

      if (token) {
        headers["RequestVerificationToken"] = token;
      }

      const response = await fetch("/Settings/Privacy", {
        method: "PUT",

        headers,

        credentials: "same-origin",

        body: JSON.stringify(payload),
      });

      let result = null;

      try {
        result = await response.json();
      } catch {
        result = null;
      }

      if (!response.ok) {
        throw new Error(t("settings.saveError"));
      }

      showToast(t("settings.privacySaved"));
    } catch (error) {
      console.error("Erro ao guardar privacidade:", error);

      showToast(error.message || t("settings.saveError"), false);
    } finally {
      setPrivacySaveLoading(false);
    }
  }

  if (privacyElements.saveButton) {
    privacyElements.saveButton.addEventListener("click", savePrivacySettings);
  }

  function initializePrivacySection() {
    if (privacyLoaded) {
      return;
    }

    privacyLoaded = true;

    loadPrivacySettings();
  }

  // NOTIFICAÇÕES E APARÊNCIA AVANÇADAS

  let advancedPreferencesLoaded = false;

  function applyAdvancedAppearance(preferences) {
    const p = preferences || {};
    const root = document.documentElement;

    root.dataset.theme = p.theme || "system";
    root.dataset.density = p.uiDensity || "comfortable";
    root.classList.toggle("reduce-motion", p.reduceMotion === true);
    root.style.setProperty("--chatapp-accent", p.accentColor || "#2563eb");

    try {
      localStorage.setItem("chatapp.appearance", JSON.stringify(p));
    } catch (_) {}
  }

  async function loadAdvancedPreferences() {
    if (advancedPreferencesLoaded) return;

    try {
      const response = await fetch("/api/preferences", {
        method: "GET",
        headers: { Accept: "application/json" },
        credentials: "same-origin",
        cache: "no-store",
      });

      if (!response.ok) {
        throw new Error(`Erro ao carregar preferências: ${response.status}`);
      }

      const data = await response.json();
      const notifications = data.notifications || {};

      document.querySelectorAll(".advanced-notify").forEach((element) => {
        const key = element.dataset.key;
        if (key && Object.prototype.hasOwnProperty.call(notifications, key)) {
          element.checked = notifications[key] === true;
        }
      });

      const theme = document.getElementById("advancedTheme");
      const accent = document.getElementById("advancedAccent");
      const density = document.getElementById("advancedDensity");
      const reduceMotion = document.getElementById("advancedReduceMotion");

      if (theme) theme.value = data.theme || "system";
      if (accent) accent.value = data.accentColor || "#2563eb";
      if (density) density.value = data.uiDensity || "comfortable";
      if (reduceMotion) reduceMotion.checked = data.reduceMotion === true;

      applyAdvancedAppearance(data);
      advancedPreferencesLoaded = true;
    } catch (error) {
      console.error("Erro ao carregar preferências avançadas:", error);
    }
  }

  async function saveAdvancedPatch(patch, successMessage) {
    const token = getAntiForgeryToken();
    const headers = {
      "Content-Type": "application/json",
      Accept: "application/json",
    };

    if (token) headers["RequestVerificationToken"] = token;

    const response = await fetch("/api/preferences", {
      method: "PUT",
      headers,
      credentials: "same-origin",
      body: JSON.stringify(patch),
    });

    let data = null;
    try {
      data = await response.json();
    } catch (_) {}

    if (!response.ok) {
      throw new Error(
        data?.message || "Não foi possível guardar as preferências.",
      );
    }

    showToast(successMessage);
    return data;
  }

  const advancedNotificationsButton = document.getElementById(
    "saveAdvancedNotifications",
  );
  advancedNotificationsButton?.addEventListener("click", async () => {
    const patch = {};
    document.querySelectorAll(".advanced-notify").forEach((element) => {
      if (element.dataset.key) patch[element.dataset.key] = element.checked;
    });

    advancedNotificationsButton.disabled = true;

    try {
      await saveAdvancedPatch(patch, "Preferências de notificações guardadas.");
    } catch (error) {
      console.error(error);
      showToast(error.message, false);
    } finally {
      advancedNotificationsButton.disabled = false;
    }
  });

  const advancedAppearanceButton = document.getElementById(
    "saveAdvancedAppearance",
  );
  advancedAppearanceButton?.addEventListener("click", async () => {
    const patch = {
      theme: document.getElementById("advancedTheme")?.value || "system",
      accentColor:
        document.getElementById("advancedAccent")?.value || "#2563eb",
      uiDensity:
        document.getElementById("advancedDensity")?.value || "comfortable",
      reduceMotion:
        document.getElementById("advancedReduceMotion")?.checked === true,
    };

    advancedAppearanceButton.disabled = true;

    try {
      const data = await saveAdvancedPatch(
        patch,
        "Aparência guardada com sucesso.",
      );
      applyAdvancedAppearance({ ...patch, ...(data || {}) });
      window.dispatchEvent(
        new CustomEvent("chatapp:appearance-changed", { detail: patch }),
      );
    } catch (error) {
      console.error(error);
      showToast(error.message, false);
    } finally {
      advancedAppearanceButton.disabled = false;
    }
  });

  // SEGURANÇA

  const securityElements = {
    statusTitle: document.getElementById("securityStatusTitle"),
    statusText: document.getElementById("securityStatusText"),
    statusBadge: document.getElementById("securityStatusBadge"),
    currentPassword: document.getElementById("securityCurrentPassword"),
    newPassword: document.getElementById("securityNewPassword"),
    confirmPassword: document.getElementById("securityConfirmPassword"),
    changePassword: document.getElementById("btnChangePassword"),
    setupTwoFactor: document.getElementById("btnSetupTwoFactor"),
    twoFactorSetup: document.getElementById("twoFactorSetup"),
    twoFactorKey: document.getElementById("twoFactorKey"),
    twoFactorCode: document.getElementById("twoFactorCode"),
    enableTwoFactor: document.getElementById("btnEnableTwoFactor"),
    enabledTwoFactor: document.getElementById("twoFactorEnabledBox"),
    disableTwoFactor: document.getElementById("btnDisableTwoFactor"),
    twoFactorActions: document.getElementById("twoFactorActions"),
    twoFactorRecovery: document.getElementById("twoFactorRecovery"),
    twoFactorRecoveryCodes: document.getElementById("twoFactorRecoveryCodes"),
    copyRecoveryCodes: document.getElementById("btnCopyRecoveryCodes"),
    twoFactorTitle: document.getElementById("twoFactorTitle"),
    twoFactorDescription: document.getElementById("twoFactorDescription"),
    copyTwoFactorKey: document.getElementById("btnCopyTwoFactorKey"),
    revokeSessions: document.getElementById("btnRevokeSessions"),
  };

  let twoFactorEnabled = false;

  async function securityRequest(url, options = {}) {
    const headers = {
      Accept: "application/json",
      ...(options.headers || {}),
    };

    const token = getAntiForgeryToken();
    if (token) headers["RequestVerificationToken"] = token;

    const response = await fetch(url, {
      credentials: "same-origin",
      ...options,
      headers,
    });

    let data = null;
    try {
      data = await response.json();
    } catch (_) {}

    if (!response.ok) {
      const message =
        data?.message || `Operação recusada (${response.status}).`;
      const error = new Error(message);
      error.data = data;
      error.status = response.status;
      throw error;
    }

    return data;
  }

  function renderSecurityStatus() {
    if (!securityElements.statusTitle) return;

    if (twoFactorEnabled) {
      securityElements.statusTitle.textContent =
        "A tua conta tem proteção adicional ativa";
      securityElements.statusText.textContent =
        "A autenticação em dois fatores está configurada.";
      securityElements.statusBadge.textContent = "Protegida";
      securityElements.statusBadge.className =
        "security-status-badge is-secure";
    } else {
      securityElements.statusTitle.textContent =
        "A proteção adicional ainda não está ativa";
      securityElements.statusText.textContent =
        "Podes ativar a autenticação em dois fatores abaixo.";
      securityElements.statusBadge.textContent = "Reforçar";
      securityElements.statusBadge.className =
        "security-status-badge is-warning";
    }

    securityElements.twoFactorTitle.textContent = twoFactorEnabled
      ? "Autenticação em dois fatores ativa"
      : "Autenticação em dois fatores desativada";
    securityElements.twoFactorDescription.textContent = twoFactorEnabled
      ? "Um código adicional será exigido quando o segundo fator estiver configurado no fluxo de autenticação."
      : "Usa uma aplicação autenticadora para adicionar uma segunda camada de proteção.";

    securityElements.setupTwoFactor?.classList.toggle(
      "d-none",
      twoFactorEnabled,
    );
    securityElements.enabledTwoFactor?.classList.toggle(
      "d-none",
      !twoFactorEnabled,
    );
    securityElements.twoFactorSetup?.classList.add("d-none");
  }

  async function loadSecurity() {
    try {
      const data = await securityRequest("/Settings/Security", {
        method: "GET",
        cache: "no-store",
      });

      twoFactorEnabled = data?.twoFactorEnabled === true;
      renderSecurityStatus();
    } catch (error) {
      console.error("Erro ao carregar segurança:", error);
      showToast(
        error.message || "Não foi possível carregar a segurança.",
        false,
      );
    }
  }

  function setSecurityButtonLoading(button, loading, text) {
    if (!button) return;
    button.disabled = loading;
    if (loading) {
      button.dataset.originalHtml = button.innerHTML;
      button.innerHTML =
        '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>A processar...</span>';
    } else if (button.dataset.originalHtml) {
      button.innerHTML = button.dataset.originalHtml;
      delete button.dataset.originalHtml;
    } else if (text) {
      button.textContent = text;
    }
  }

  securityElements.changePassword?.addEventListener("click", async () => {
    const currentPassword = securityElements.currentPassword?.value || "";
    const newPassword = securityElements.newPassword?.value || "";
    const confirmPassword = securityElements.confirmPassword?.value || "";

    if (!currentPassword || !newPassword || !confirmPassword) {
      showToast("Preenche todos os campos da palavra-passe.", false);
      return;
    }

    if (newPassword !== confirmPassword) {
      showToast("A confirmação da nova palavra-passe não coincide.", false);
      return;
    }

    if (newPassword.length < 10) {
      showToast(
        "A nova palavra-passe deve ter pelo menos 10 caracteres.",
        false,
      );
      return;
    }

    setSecurityButtonLoading(securityElements.changePassword, true);

    try {
      await securityRequest("/Settings/Security/Password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ currentPassword, newPassword, confirmPassword }),
      });

      securityElements.currentPassword.value = "";
      securityElements.newPassword.value = "";
      securityElements.confirmPassword.value = "";
      showToast("Palavra-passe alterada com sucesso.");
    } catch (error) {
      console.error(error);
      showToast(
        error.message || "Não foi possível alterar a palavra-passe.",
        false,
      );
    } finally {
      setSecurityButtonLoading(securityElements.changePassword, false);
    }
  });

  document.querySelectorAll(".security-password-toggle").forEach((button) => {
    button.addEventListener("click", () => {
      const input = document.getElementById(button.dataset.passwordTarget);
      if (!input) return;
      const visible = input.type === "text";
      input.type = visible ? "password" : "text";
      button.innerHTML = visible
        ? '<i class="bi bi-eye"></i>'
        : '<i class="bi bi-eye-slash"></i>';
    });
  });

  securityElements.setupTwoFactor?.addEventListener("click", async () => {
    setSecurityButtonLoading(securityElements.setupTwoFactor, true);

    try {
      const data = await securityRequest("/Settings/Security/TwoFactor/Setup", {
        method: "POST",
      });

      if (data?.enabled) {
        twoFactorEnabled = true;
        renderSecurityStatus();
        return;
      }

      if (securityElements.twoFactorKey)
        securityElements.twoFactorKey.value = data?.key || "";
      securityElements.twoFactorSetup?.classList.remove("d-none");
      securityElements.twoFactorActions?.classList.add("d-none");
      showToast(
        "Chave gerada. Adiciona-a à tua aplicação autenticadora e confirma o código.",
      );
    } catch (error) {
      console.error(error);
      showToast(error.message || "Não foi possível preparar o 2FA.", false);
    } finally {
      setSecurityButtonLoading(securityElements.setupTwoFactor, false);
    }
  });

  securityElements.copyTwoFactorKey?.addEventListener("click", async () => {
    const value = securityElements.twoFactorKey?.value || "";
    if (!value) return;

    try {
      await navigator.clipboard.writeText(value);
      showToast("Chave copiada.");
    } catch (_) {
      securityElements.twoFactorKey.select();
      document.execCommand("copy");
      showToast("Chave copiada.");
    }
  });

  securityElements.enableTwoFactor?.addEventListener("click", async () => {
    const code = (securityElements.twoFactorCode?.value || "").replace(
      /\s+/g,
      "",
    );

    if (!/^\d{6,8}$/.test(code)) {
      showToast(
        "Introduz o código apresentado pela aplicação autenticadora.",
        false,
      );
      return;
    }

    setSecurityButtonLoading(securityElements.enableTwoFactor, true);

    try {
      const data = await securityRequest(
        "/Settings/Security/TwoFactor/Enable",
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ code }),
        },
      );

      twoFactorEnabled = true;
      if (securityElements.twoFactorCode)
        securityElements.twoFactorCode.value = "";

      const recoveryCodes = Array.isArray(data?.recoveryCodes)
        ? data.recoveryCodes
        : [];
      if (securityElements.twoFactorRecoveryCodes && recoveryCodes.length) {
        securityElements.twoFactorRecoveryCodes.innerHTML = recoveryCodes
          .map((item) => `<code>${String(item).replace(/[<>&"']/g, "")}</code>`)
          .join("");
        securityElements.twoFactorRecovery?.classList.remove("d-none");
      }

      renderSecurityStatus();
      showToast("Autenticação em dois fatores ativada.");
    } catch (error) {
      console.error(error);
      showToast(error.message || "Código inválido.", false);
    } finally {
      setSecurityButtonLoading(securityElements.enableTwoFactor, false);
    }
  });

  securityElements.copyRecoveryCodes?.addEventListener("click", async () => {
    const codes = Array.from(
      securityElements.twoFactorRecoveryCodes?.querySelectorAll("code") || [],
    )
      .map((element) => element.textContent || "")
      .filter(Boolean)
      .join("\n");

    if (!codes) return;

    try {
      await navigator.clipboard.writeText(codes);
      showToast("Códigos de recuperação copiados.");
    } catch (_) {
      showToast("Não foi possível copiar os códigos automaticamente.", false);
    }
  });

  securityElements.disableTwoFactor?.addEventListener("click", async () => {
    const password = window.prompt(
      "Para desativar o 2FA, introduz a tua palavra-passe atual:",
    );
    if (password === null) return;

    if (!password.trim()) {
      showToast("A palavra-passe é obrigatória.", false);
      return;
    }

    setSecurityButtonLoading(securityElements.disableTwoFactor, true);

    try {
      await securityRequest("/Settings/Security/TwoFactor/Disable", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ currentPassword: password }),
      });

      twoFactorEnabled = false;
      renderSecurityStatus();
      showToast("Autenticação em dois fatores desativada.");
    } catch (error) {
      console.error(error);
      showToast(error.message || "Não foi possível desativar o 2FA.", false);
    } finally {
      setSecurityButtonLoading(securityElements.disableTwoFactor, false);
    }
  });

  securityElements.revokeSessions?.addEventListener("click", async () => {
    if (
      !window.confirm(
        "Terminar as outras sessões? Esta ação vai invalidar os acessos antigos da tua conta.",
      )
    ) {
      return;
    }

    setSecurityButtonLoading(securityElements.revokeSessions, true);

    try {
      await securityRequest("/Settings/Security/Sessions/Revoke", {
        method: "POST",
      });
      showToast("As outras sessões foram terminadas.");
    } catch (error) {
      console.error(error);
      showToast(
        error.message || "Não foi possível terminar as outras sessões.",
        false,
      );
    } finally {
      setSecurityButtonLoading(securityElements.revokeSessions, false);
    }
  });

  const originalShowSection = showSection;
  showSection = function (sectionName) {
    originalShowSection(sectionName);
    if (sectionName === "notifications" || sectionName === "appearance") {
      loadAdvancedPreferences();
    }
    if (sectionName === "security") {
      loadSecurity();
    }
  };

  // INICIALIZAÇÃO

  (async () => {
    if (window.ChatPreferences?.ready) {
      await window.ChatPreferences.ready;
    }

    await loadSettings();
    loadProfile();
  })();
});

// CONFIGURAÇÕES AVANÇADAS — NOTIFICAÇÕES / APARÊNCIA
(function () {
  const token = () =>
    document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
    "";
  const load = async () => {
    try {
      const r = await fetch("/api/preferences");
      if (!r.ok) return;
      const p = await r.json();
      const n = p.notifications || {};
      document.querySelectorAll(".advanced-notify").forEach((el) => {
        if (el.dataset.key in n) el.checked = !!n[el.dataset.key];
      });
      const theme = document.getElementById("advancedTheme");
      if (theme) theme.value = p.theme || "system";
      const accent = document.getElementById("advancedAccent");
      if (accent) accent.value = p.accentColor || "#2563eb";
      const density = document.getElementById("advancedDensity");
      if (density) density.value = p.uiDensity || "comfortable";
      const motion = document.getElementById("advancedReduceMotion");
      if (motion) motion.checked = !!p.reduceMotion;
    } catch (e) {
      console.warn("Preferências avançadas:", e);
    }
  };
  document
    .getElementById("saveAdvancedNotifications")
    ?.addEventListener("click", async () => {
      const patch = {};
      document
        .querySelectorAll(".advanced-notify")
        .forEach((el) => (patch[el.dataset.key] = el.checked));
      const r = await fetch("/api/preferences", {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token(),
        },
        body: JSON.stringify(patch),
      });
      if (!r.ok) return alert("Não foi possível guardar as notificações.");
      alert("Notificações guardadas.");
    });
  document
    .getElementById("saveAdvancedAppearance")
    ?.addEventListener("click", async () => {
      const patch = {
        theme: document.getElementById("advancedTheme")?.value,
        accentColor: document.getElementById("advancedAccent")?.value,
        uiDensity: document.getElementById("advancedDensity")?.value,
        reduceMotion: document.getElementById("advancedReduceMotion")?.checked,
      };
      try {
        await window.ChatAppAdvancedPreferences.save(patch);
        alert("Aparência guardada.");
      } catch (e) {
        alert(e.message);
      }
    });
  if (document.readyState === "loading")
    document.addEventListener("DOMContentLoaded", load);
  else load();
})();
