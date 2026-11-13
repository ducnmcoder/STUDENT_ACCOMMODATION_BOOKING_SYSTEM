(function () {
  function openPopup(message, kind) {
    return new Promise(function (resolve) {
      let overlay = document.getElementById("guard-popup-overlay");
      if (!overlay) {
        overlay = document.createElement("div");
        overlay.id = "guard-popup-overlay";
        overlay.className = "guard-popup-overlay";
        overlay.setAttribute("aria-hidden", "true");
        overlay.innerHTML = `
                    <div class="guard-popup-card"
                         role="dialog"
                         aria-modal="true"
                         aria-labelledby="guard-popup-title"
                         aria-describedby="guard-popup-message">
                        <div class="guard-popup-icon" id="guard-popup-icon"></div>
                        <h2 class="guard-popup-title" id="guard-popup-title"></h2>
                        <p class="guard-popup-message" id="guard-popup-message"></p>
                        <div class="guard-popup-actions">
                            <button type="button"
                                    class="guard-popup-button guard-popup-button--cancel"
                                    id="guard-popup-cancel">
                                Hủy
                            </button>
                            <button type="button"
                                    class="guard-popup-button guard-popup-button--ok"
                                    id="guard-popup-ok">
                                Xác nhận
                            </button>
                        </div>
                    </div>
                `;
        document.body.appendChild(overlay);
      }
      const isConfirm = kind === "confirm";
      const title = overlay.querySelector("#guard-popup-title");
      const messageElement = overlay.querySelector("#guard-popup-message");
      const icon = overlay.querySelector("#guard-popup-icon");
      const cancelButton = overlay.querySelector("#guard-popup-cancel");
      const okButton = overlay.querySelector("#guard-popup-ok");
      const previousFocus = document.activeElement;
      overlay.dataset.kind = kind;
      title.textContent = isConfirm
        ? "Xác nhận thao tác"
        : kind === "success"
          ? "Thành công"
          : "Thông báo";
      messageElement.textContent = message;
      icon.textContent = isConfirm ? "?" : kind === "success" ? "✓" : "!";
      cancelButton.hidden = !isConfirm;
      okButton.textContent = isConfirm ? "Xác nhận" : "Đóng";
      function closePopup(result) {
        overlay.classList.remove("is-visible");
        overlay.setAttribute("aria-hidden", "true");
        okButton.removeEventListener("click", handleOk);
        cancelButton.removeEventListener("click", handleCancel);
        document.removeEventListener("keydown", handleKeydown);
        if (previousFocus && previousFocus.focus) {
          previousFocus.focus();
        }
        resolve(result);
      }
      function handleOk() {
        closePopup(true);
      }
      function handleCancel() {
        closePopup(false);
      }
      function handleKeydown(event) {
        if (event.key === "Escape") {
          event.preventDefault();
          closePopup(isConfirm ? false : true);
        }
      }
      okButton.addEventListener("click", handleOk);
      cancelButton.addEventListener("click", handleCancel);
      document.addEventListener("keydown", handleKeydown);
      overlay.classList.add("is-visible");
      overlay.setAttribute("aria-hidden", "false");
      okButton.focus();
    });
  }
  window.GuardDialog = {
    confirm: function (message) {
      return openPopup(message, "confirm");
    },
    notify: function (message, success) {
      return openPopup(message, success ? "success" : "error");
    },
    bindPostForms: function (selector, question, successAction) {
      document.querySelectorAll(selector).forEach(function (form) {
        form.addEventListener("submit", async function (event) {
          event.preventDefault();
          const agreed = await GuardDialog.confirm(question);
          if (!agreed) {
            return;
          }
          const submitButton = form.querySelector('button[type="submit"]');
          submitButton.disabled = true;
          try {
            const token = form.querySelector(
              'input[name="__RequestVerificationToken"]',
            ).value;
            const response = await fetch(form.action, {
              method: "POST",
              headers: {
                RequestVerificationToken: token,
              },
              body: new FormData(form),
            });
            let result = null;
            try {
              result = await response.json();
            } catch {}
            const message =
              result?.message ||
              (response.ok
                ? "Thao tác thành công."
                : "Không thể thực hiện thao tác.");
            await GuardDialog.notify(message, response.ok);
            if (response.ok) {
              if (successAction === "reload") {
                window.location.reload();
              } else {
                window.location.href = successAction;
              }
            }
          } catch (error) {
            await GuardDialog.notify("Không thể kết nối đến máy chủ.", false);
          } finally {
            submitButton.disabled = false;
          }
        });
      });
    },
  };
})();