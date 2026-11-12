(() => {
    const searchForm = document.getElementById('room-search');
    const dateInput = document.getElementById('booking-date');
    const message = document.getElementById('search-message');
    const allRooms = document.getElementById('all-rooms');
    const results = document.getElementById('available-rooms');
    const panel = document.getElementById('booking-panel');
    const bookForm = document.getElementById('book-room-form');
    const slotChoice = document.getElementById('slot-choice');
    const startTime = document.getElementById('book-start-time');
    const endTime = document.getElementById('book-end-time');
    const bookMessage = document.getElementById('booking-message');
    let selectedSlots = [];
    let requestVersion = 0;

    const vietnamToday = new Date(Date.now() + 7 * 3600000).toISOString().slice(0, 10);
    dateInput.value = vietnamToday;
    dateInput.min = vietnamToday;

    const timeInVietnam = iso => {
        const value = new Date(iso);
        return `${String((value.getUTCHours() + 7) % 24).padStart(2, '0')}:${String(value.getUTCMinutes()).padStart(2, '0')}`;
    };
    const slotLabel = slot => `${timeInVietnam(slot.startAt)} – ${timeInVietnam(slot.endAt) === '00:00' ? '24:00' : timeInVietnam(slot.endAt)}`;
    const showError = value => { bookMessage.textContent = value; bookMessage.hidden = false; };
    const clearSelection = () => { panel.hidden = true; selectedSlots = []; bookMessage.hidden = true; };
    searchForm.addEventListener('input', () => {
        requestVersion++;
        clearSelection();
        results.replaceChildren();
        results.hidden = true;
        allRooms.hidden = false;
        document.getElementById('room-list-title').textContent = 'Tất cả phòng';
        message.textContent = '';
    });

    function addText(parent, tag, value, className) {
        const element = document.createElement(tag);
        element.textContent = value;
        if (className) element.className = className;
        parent.appendChild(element);
        return element;
    }

    async function fetchAvailable(withFilters) {
        if (!dateInput.value || dateInput.value < vietnamToday)
            throw new Error('Vui lòng chọn ngày đặt chưa qua.');
        const params = new URLSearchParams({ date: dateInput.value });
        if (withFilters) {
            const building = document.getElementById('building').value;
            const floor = document.getElementById('floor').value;
            const capacity = document.getElementById('capacity').value;
            if (building) params.set('building', building);
            if (floor !== '') params.set('floor', floor);
            if (capacity !== '') params.set('capacity', capacity);
        }
        const response = await fetch(`${searchForm.dataset.apiUrl}?${params.toString()}`, {
            headers: { Accept: 'application/json' }
        });
        if (!response.headers.get('content-type')?.includes('application/json'))
            throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
        const data = await response.json();
        if (!response.ok) throw new Error(data.message || 'Không thể tìm phòng trống.');
        return data;
    }

    function selectSlot() {
        const slot = selectedSlots[Number(slotChoice.value)];
        if (!slot) return;
        startTime.value = timeInVietnam(slot.startAt);
        endTime.value = timeInVietnam(slot.endAt);
        bookMessage.hidden = true;
    }
    slotChoice.addEventListener('change', selectSlot);

    function openPanel(available) {
        selectedSlots = available.slots;
        slotChoice.replaceChildren();
        selectedSlots.forEach((slot, index) => {
            const option = document.createElement('option');
            option.value = index;
            option.textContent = slotLabel(slot);
            slotChoice.appendChild(option);
        });
        document.getElementById('booking-room-id').value = available.room.id;
        document.getElementById('selected-room').textContent = available.room.code;
        document.getElementById('selected-date').textContent = dateInput.value.split('-').reverse().join('/');
        selectSlot();
        panel.hidden = false;
        panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    document.querySelectorAll('[data-room-id]').forEach(button => {
        button.addEventListener('click', async () => {
            const version = ++requestVersion;
            clearSelection();
            message.textContent = `Đang kiểm tra khung giờ trống của phòng ${button.dataset.roomCode}…`;
            try {
                const available = (await fetchAvailable(false))
                    .find(item => item.room.id === Number(button.dataset.roomId));
                if (version !== requestVersion) return;
                if (!available) {
                    message.textContent = 'Phòng này không còn khung giờ trống trong ngày đã chọn.';
                    return;
                }
                message.textContent = '';
                openPanel(available);
            } catch (error) {
                if (version !== requestVersion) return;
                message.textContent = error.message || 'Không thể kiểm tra phòng.';
            }
        });
    });

    searchForm.addEventListener('submit', async event => {
        event.preventDefault();
        const version = ++requestVersion;
        clearSelection();
        results.replaceChildren();
        results.hidden = true;
        allRooms.hidden = false;
        document.getElementById('room-list-title').textContent = 'Tất cả phòng';
        message.textContent = 'Đang tìm phòng trống…';
        try {
            const data = await fetchAvailable(true);
            if (version !== requestVersion) return;
            for (const available of data) {
                const room = available.room;
                const card = document.createElement('article');
                card.className = 'room-card';
                const top = addText(card, 'div', '', 'room-card-top');
                addText(top, 'strong', room.code);
                addText(top, 'span', 'Còn trống', 'room-status available');
                addText(card, 'p', `${room.building} · Tầng ${room.floor} · ${room.capacity} người`);
                addText(card, 'small', `Thiết bị: ${room.assets.length ? room.assets.join(', ') : 'Chưa có'}`);
                addText(card, 'p', `Khung giờ còn trống: ${available.slots.map(slotLabel).join(', ')}`, 'available-times');
                const button = addText(card, 'button', 'Đặt phòng này →', 'primary-button room-book-button');
                button.type = 'button';
                button.addEventListener('click', () => openPanel(available));
                results.appendChild(card);
            }
            allRooms.hidden = true;
            results.hidden = false;
            document.getElementById('room-list-title').textContent = 'Phòng trống phù hợp';
            message.textContent = data.length ? `Tìm thấy ${data.length} phòng có khung giờ trống.` :
                'Không tìm thấy phòng có khung giờ trống trong ngày này.';
        } catch (error) {
            if (version !== requestVersion) return;
            message.textContent = error.message || 'Không thể tìm phòng trống.';
        }
    });

    bookForm.addEventListener('submit', event => {
        const slot = selectedSlots[Number(slotChoice.value)];
        if (!slot) { event.preventDefault(); showError('Vui lòng chọn khung giờ còn trống.'); return; }
        const start = new Date(`${dateInput.value}T${startTime.value}:00+07:00`);
        let end = new Date(`${dateInput.value}T${endTime.value}:00+07:00`);
        const dayEnd = new Date(`${dateInput.value}T00:00:00+07:00`).getTime() + 86400000;
        if (endTime.value === '00:00' && new Date(slot.endAt).getTime() === dayEnd)
            end = new Date(dayEnd);
        if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) ||
            start <= new Date() || end <= start ||
            start.getTime() < new Date(slot.startAt).getTime() || end.getTime() > new Date(slot.endAt).getTime()) {
            event.preventDefault();
            showError('Chọn giờ bắt đầu và kết thúc nằm trong khung giờ còn trống.');
            return;
        }
        document.getElementById('booking-start').value = start.toISOString();
        document.getElementById('booking-end').value = end.toISOString();
    });
})();
