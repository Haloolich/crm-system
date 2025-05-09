using System;
using System.Collections.Generic;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using System.Diagnostics;
using crmV1.Services; // !!! ДОДАНО: Для ApiClient !!!
using crmV1.Models; // !!! ДОДАНО: Якщо модель Club потрібна для чогось ще на клієнті (не для цього коду, але корисно) !!!

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AddClubPage : ContentPage
    {
        public AddClubPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            // Можна приховати стандартний NavigationBar, якщо потрібно
            // NavigationPage.SetHasNavigationBar(this, false);
        }

        // Обробник натискання кнопки "Зберегти клуб"
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // Зчитуємо дані з полів вводу
            string name = nameEntry.Text;
            string address = addressEntry.Text;
            string phone = phoneEntry.Text;
            string email = emailEntry.Text;
            // Спроба парсингу числових значень
            int maxPs = 0;
            int maxVr = 0;

            // Базова клієнтська валідація
            if (string.IsNullOrWhiteSpace(name))
            {
                await DisplayAlert("Помилка", "Будь ласка, введіть назву клубу.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(address))
            {
                await DisplayAlert("Помилка", "Будь ласка, введіть адресу клубу.", "OK");
                return;
            }

            // Валідація числових полів
            if (!int.TryParse(maxPsEntry.Text, out maxPs) || maxPs < 0)
            {
                await DisplayAlert("Помилка", "Кількість зон PS повинна бути невід'ємним числом.", "OK");
                return;
            }

            if (!int.TryParse(maxVrEntry.Text, out maxVr) || maxVr < 0)
            {
                await DisplayAlert("Помилка", "Кількість зон VR/Quest повинна бути невід'ємним числом.", "OK");
                return;
            }

            // --- ДОДАНО: ЛОГІКА ВІДПРАВКИ ДАНИХ НА СЕРВЕР ---

            // Показуємо індикатор завантаження та блокуємо кнопку
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
            saveButton.IsEnabled = false; // Блокуємо кнопку, щоб уникнути подвійного натискання

            try
            {
                // Збираємо дані у словник для відправки на сервер
                var clubData = new Dictionary<string, object>
                {
                    { "action", "add_club" }, // Дія для сервера
                    { "name", name },
                    { "address", address },
                    { "phone_number", phone ?? string.Empty }, // Відправляємо порожній рядок, якщо null
                    { "email", email ?? string.Empty },       // Відправляємо порожній рядок, якщо null
                    { "max_ps_zones", maxPs },
                    { "max_vr_quest_zones", maxVr }
                    // Якщо потрібно, додайте ID менеджера, який додає клуб
                    // { "manager_id", GlobalSettings.CurrentManagerId } // Приклад, якщо у вас є
                };

                Debug.WriteLine("[AddClubPage] Sending add_club request to server...");
                // Надсилаємо запит
                var response = await ApiClient.SendRequestAsync(clubData);
                Debug.WriteLine($"[AddClubPage] Received response for add_club: {response}");


                // Обробка відповіді сервера
                if (response != null && response.ContainsKey("success"))
                {
                    bool isSuccess = false;
                    try
                    {
                        // Намагаємося перетворити на bool
                        isSuccess = Convert.ToBoolean(response["success"]);
                    }
                    catch (Exception convertEx)
                    {
                        // Якщо перетворення не вдалося (не "true", "false", 1, 0 тощо)
                        Debug.WriteLine($"[AddClubPage] Error converting 'success' value '{response["success"]}' to bool: {convertEx.Message}");
                        // Вважаємо це помилкою формату відповіді або помилкою сервера
                        await DisplayAlert("Помилка формату відповіді", "Сервер повернув некоректний статус успіху.", "OK");
                        return; // Виходимо з обробника
                    }


                    if (isSuccess) // Використовуємо отримане булеве значення
                    {
                        Debug.WriteLine("[AddClubPage] Club added successfully according to server.");
                        string successMessage = response.ContainsKey("message") ? response["message"].ToString() : "Клуб успішно додано.";
                        await DisplayAlert("Успіх", successMessage, "OK");

                        // Повертаємося на попередню сторінку (головну)
                        await Navigation.PopAsync();
                    }
                    else
                    {
                        // Сервер повідомив про помилку
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Не вдалося додати клуб (невідома причина).";
                        Debug.WriteLine($"[AddClubPage] Server reported error: {errorMessage}");
                        await DisplayAlert("Помилка сервера", errorMessage, "OK");
                    }
                }
                else
                {
                    // Неочікуваний формат відповіді сервера (відсутнє поле "success")
                    Debug.WriteLine("[AddClubPage] Server response has unexpected format (missing 'success' key).");
                    await DisplayAlert("Помилка", "Неочікуваний формат відповіді від сервера (відсутній статус).", "OK");
                }
            }
            catch (Exception ex)
            {
                // Помилка під час відправки запиту (мережа, з'єднання тощо)
                Console.WriteLine($"[AddClubPage] Critical error sending add_club request: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Критична помилка", $"Не вдалося з'єднатися з сервером або обробити запит: {ex.Message}", "OK");
            }
            finally
            {
                // Приховуємо індикатор та розблоковуємо кнопку
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
                saveButton.IsEnabled = true;
            }
        }
        // --- КІНЕЦЬ: ЛОГІКА ВІДПРАВКИ ДАНИХ НА СЕРВЕР ---

        // !!! ВИДАЛЕНО: ЗАГЛУШКА З ПОВІДОМЛЕННЯМ "Дані зібрано..." !!!
        // await DisplayAlert("Дані зібрано", "Дані для нового клубу зібрані, але не збережені на сервері (серверна логіка ще не реалізована).", "OK");
        // await Navigation.PopAsync();
    }
}