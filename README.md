# TextReport

TextReport анализирует текст и отправляет HTML-отчёт на email. В отчёте есть статистика, частые слова, длинные и повторяющиеся предложения. Если настроен DeepSeek, отчёт также содержит до пяти рекомендаций по редактированию.

## Запуск через Docker Compose

Из корня проекта запусти Docker Desktop или Docker Engine с Compose и выполни команды:

```powershell
Copy-Item .env.example .env
docker compose up -d --build --wait
```

При повторном запуске используй имеющийся `.env`. Compose поднимает приложение, PostgreSQL 17 и Mailpit.

| Адрес | Назначение |
| --- | --- |
| `http://localhost:5080/` | Лендинг и форма заявки |
| `http://localhost:5080/admin` | Список и карточки заявок |
| `http://localhost:8025/` | Письма, принятые Mailpit |
| `http://localhost:5080/health/ready` | Проверка подключения к PostgreSQL |

Для входа в админку заполни `TEXTREPORT_ADMIN_PASSWORD` в `.env`. Логин задаёт `TEXTREPORT_ADMIN_USERNAME`, по умолчанию это `admin`. Для AI-рекомендаций добавь свой ключ DeepSeek в `TEXTREPORT_AI_API_KEY`. Без ключа работает базовый анализ. После изменения `.env` примени настройки командой `docker compose up -d app`.

Вставь текст длиной от 20 до 15 000 символов, укажи email и отправь заявку. Оплата тестовая. После обработки письмо появится в Mailpit. Он принимает его локально и не пересылает получателю. Если включён DeepSeek, исходный текст и статистика передаются модели. Для демонстрации используй вымышленный текст.

`docker compose down` останавливает сервисы. Данные PostgreSQL хранятся в томе `postgres-data`.

## Запуск с локальным .NET 8 SDK

Если приложение уже запущено в Compose, выполни `docker compose stop app`, чтобы освободить порт 5080.

```powershell
docker compose up -d --wait postgres mailpit
dotnet run --project src/TextReport/TextReport.csproj
```

Для локального запуска из IDE или терминала скопируй `src/TextReport/appsettings.Local.example.json` в `src/TextReport/appsettings.Local.json`. Укажи пароль в `Admin.Password` и, если нужны рекомендации, ключ в `Ai.ApiKey`. Имеющийся локальный файл можно использовать повторно. Файлы `.env` и `appsettings.Local.json` игнорируются Git и исключены из Docker-образа.

## Как устроена обработка

1. `POST /api/orders` проверяет email и текст и одной транзакцией сохраняет заявку и тестовый платёж.
2. `PaymentWorker` подтверждает платёж. `OrderWorker` подбирает оплаченную заявку, рассчитывает статистику и сохраняет отчёт. При сбое DeepSeek сохраняется базовый отчёт.
3. `EmailWorker` подбирает готовый отчёт, отправляет HTML-письмо через SMTP и записывает отметку отправки после ответа сервера.

Этапы связаны состоянием в PostgreSQL. Воркеры опрашивают БД независимо и хранят счётчик попыток для своего этапа. Ошибка SMTP приводит к повтору отправки. Если SMTP уже принял письмо, а приложение остановилось до записи отметки, при повторе возможен дубликат.

Код сгруппирован в `Features/` по операциям. В `Product/TextAnalysis/` лежат расчёт статистики и интеграция с DeepSeek через Semantic Kernel. В `Components/Admin/` находятся компоненты Blazor Interactive Server на Bootstrap.

Compose настроен для локальной демонстрации в Development. Приложение рассчитано на один экземпляр. В Development пароль администратора хранится вне PostgreSQL, а пересоздание БД из админки требует ввести `УДАЛИТЬ`. Сброс удаляет все заявки, платежи и отчёты. Во время сброса текущие HTTP-запросы и попытки воркеров могут завершиться ошибкой. Для обычного старта используется `EnsureCreatedAsync`, который создаёт пустую схему и не обновляет уже существующие таблицы.

## Проверки

```powershell
dotnet build TextReport.sln
dotnet test TextReport.sln
```

Unit-тесты проверяют анализ текста, откат к базовому отчёту при сбое AI и формирование HTML-письма.

Для интеграционного теста воркеров подними PostgreSQL и задай строку подключения. Тест создаёт отдельную БД со случайным именем, проверяет порядок этапов и повтор после сбоя SMTP, затем удаляет эту БД. У пользователя PostgreSQL должно быть право создавать базы.

```powershell
docker compose up -d --wait postgres
$env:TEXTREPORT_TEST_POSTGRES = 'Host=localhost;Port=5432;Username=textreport;Password=textreport-dev'
dotnet test TextReport.sln
```
