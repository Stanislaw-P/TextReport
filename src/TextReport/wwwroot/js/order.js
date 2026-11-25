const form = document.querySelector("#order-form");
const text = form.elements.text;
const email = form.elements.email;
const button = form.querySelector("button");
const count = document.querySelector("#count");
const status = document.querySelector("#status");
const formatNumber = new Intl.NumberFormat("ru-RU");

function updateCount() {
    count.textContent = `${formatNumber.format(text.value.length)} / ${formatNumber.format(text.maxLength)}`;
}

function showFieldError(field, message = "") {
    const error = document.querySelector(`#${field.id}-error`);
    error.textContent = message;
    error.hidden = !message;
    if (message) {
        field.setAttribute("aria-invalid", "true");
    } else {
        field.removeAttribute("aria-invalid");
    }
}

text.addEventListener("input", () => {
    updateCount();
    showFieldError(text);
});
email.addEventListener("input", () => showFieldError(email));
updateCount();

form.addEventListener("submit", async (event) => {
    event.preventDefault();
    showFieldError(text);
    showFieldError(email);
    status.textContent = "";

    if (!text.value.trim()) {
        showFieldError(text, "Введите текст для анализа.");
        text.focus();
        return;
    }

    button.disabled = true;
    status.textContent = "Отправляем заявку…";
    try {
        const response = await fetch("/api/orders", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email: email.value, text: text.value })
        });

        if (response.status === 400) {
            const { errors } = await response.json();
            showFieldError(text, errors?.text?.[0]);
            showFieldError(email, errors?.email?.[0]);
            status.textContent = "Проверьте данные и повторите отправку.";
            form.querySelector('[aria-invalid="true"]')?.focus();
            return;
        }

        if (!response.ok) {
            status.textContent = "Не удалось принять заявку. Попробуйте позже.";
            return;
        }

        const { orderId } = await response.json();
        form.reset();
        updateCount();
        status.textContent = `Заявка принята. Номер заявки: ${orderId}`;
    } catch {
        status.textContent = "Не удалось связаться с сервером. Проверьте подключение.";
    } finally {
        button.disabled = false;
    }
});
