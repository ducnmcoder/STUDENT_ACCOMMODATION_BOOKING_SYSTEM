(() => {
  const list = document.getElementById('profile-items');
  const add = document.getElementById('add-profile-item');
  if (!list || !add) return;
  let next = list.querySelectorAll('[data-item-row]').length;

  add.addEventListener('click', () => {
    const key = `new${next++}`;
    const row = document.createElement('div');
    row.className = 'custom-item';
    row.dataset.itemRow = '';
    row.innerHTML = `<input type="hidden" name="Items.Index" value="${key}">
      <input type="hidden" name="Items[${key}].Id" value="0">
      <input type="hidden" name="Items[${key}].Delete" value="false" data-delete-flag>
      <div class="field-group"><label>Tên thông tin</label><input name="Items[${key}].Label" maxlength="80" class="form-control" required placeholder="Ví dụ: Số điện thoại"></div>
      <div class="field-group"><label>Nội dung</label><input name="Items[${key}].Value" maxlength="500" class="form-control" required placeholder="Nhập nội dung"></div>
      <button type="button" class="remove-item" data-remove-item aria-label="Xóa mục thông tin">Xóa</button>`;
    list.appendChild(row);
    row.querySelector('input.form-control').focus();
  });

  list.addEventListener('click', event => {
    const button = event.target.closest('[data-remove-item]');
    if (!button) return;
    const row = button.closest('[data-item-row]');
    row.querySelector('[data-delete-flag]').value = 'true';
    row.querySelectorAll('input.form-control').forEach(input => {
      input.required = false;
      input.disabled = true;
    });
    row.hidden = true;
  });
})();
