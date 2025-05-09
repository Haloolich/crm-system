// NotificationPage.xaml.cs
using crmV1.Models;
using crmV1.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class NotificationsPage : ContentPage // ЗМІНЕНО НА NotificationPage
    {
        private readonly int _managerId;
        private readonly int _clubId;

        public ObservableCollection<Booking> NewBookings { get; set; }

        // Конструктор приймає managerId та clubId
        public NotificationsPage(int managerId, int clubId)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            _managerId = managerId;
            _clubId = clubId;

            NewBookings = new ObservableCollection<Booking>();
            newBookingsList.ItemsSource = NewBookings; // Переконайтесь, що ім'я ListView правильне (newBookingsList в XAML)

            this.BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[NotificationPage] OnAppearing called. Loading data."); // ЗМІНЕНО
            await LoadNewBookingsAsync();
        }

        // Метод для завантаження нових бронювань
        private async Task LoadNewBookingsAsync()
        {
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
            NewBookings.Clear();

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "get_new_bookings" },
                    { "club_id", _clubId }
                    // Можливо, додати manager_id, якщо фільтрація на сервері відбувається і за менеджером
                    // { "manager_id", _managerId }
                };

                Debug.WriteLine("[NotificationPage] Sending API request: get_new_bookings..."); // ЗМІНЕНО
                var response = await ApiClient.SendRequestAsync(requestData); // Використовуємо ваш наданий ApiClient.SendRequestAsync
                Debug.WriteLine("[NotificationPage] Received API response for get_new_bookings."); // ЗМІНЕНО

                if (response != null && response.ContainsKey("success") && (bool)response["success"])
                {
                    Debug.WriteLine("[NotificationPage] get_new_bookings response success = true."); // ЗМІНЕНО
                    // Перевіряємо, чи є ключ "bookings" і чи він є JArray
                    if (response.TryGetValue("bookings", out object bookingsObject) && bookingsObject is JArray bookingsJArray)
                    {
                        Debug.WriteLine($"[NotificationPage] Received {bookingsJArray.Count} new bookings."); // ЗМІНЕНО
                        // Десеріалізуємо JArray в List<Booking>
                        var bookingsList = bookingsJArray.ToObject<List<Booking>>();

                        Device.BeginInvokeOnMainThread(() =>
                        {
                            foreach (var booking in bookingsList)
                            {
                                // Якщо сервер гарантує статус "New", додаткова перевірка тут не потрібна.
                                // Якщо ні, можна додати: if (booking.Status == "New")
                                NewBookings.Add(booking);
                                Debug.WriteLine($"[NotificationPage] Added booking {booking.Id}: {booking.ClientName} at {booking.DisplayTime}"); // ЗМІНЕНО
                            }
                            Debug.WriteLine($"[NotificationPage] Finished populating list with {NewBookings.Count} items."); // ЗМІНЕНО
                        });
                    }
                    else
                    {
                        // Обробка випадку, коли "bookings" відсутній або не є масивом
                        Debug.WriteLine("[NotificationPage] get_new_bookings response does not contain 'bookings' JArray."); // ЗМІНЕНО
                        string message = response.ContainsKey("message") ? response["message"].ToString() : "Невідома помилка формату даних.";
                        Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка даних", message, "OK"));
                    }
                }
                else
                {
                    // Обробка success = false
                    string errorMessage = response != null && response.ContainsKey("message") ? response["message"].ToString() : "Невідома помилка сервера.";
                    Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ][NotificationPage] get_new_bookings response success = false. Message: {errorMessage}"); // ЗМІНЕНО
                    Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка сервера", $"Не вдалося завантажити нові бронювання: {errorMessage}", "OK"));
                }
            }
            catch (Exception ex)
            {
                // Обробка загальних винятків під час завантаження
                Console.WriteLine($"[КРИТИЧНА ПОМИЛКА КЛІЄНТ][NotificationPage] Помилка при завантаженні нових бронювань: {ex.Message}\n{ex.StackTrace}"); // ЗМІНЕНО
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Критична помилка", $"Не вдалося завантажити дані: {ex.Message}", "OK");
                });
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;
                    Debug.WriteLine("[NotificationPage] loadingIndicator.IsRunning = false (finally)"); // ЗМІНЕНО
                });
            }
        }

        // Обробник натискання кнопки "Видалити"
        private async void OnDeleteBookingClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.BindingContext is Booking booking)
            {
                Debug.WriteLine($"[NotificationPage] Delete clicked for booking ID: {booking.Id}"); // ЗМІНЕНО

                bool confirmDelete = await DisplayAlert("Підтвердження", $"Ви впевнені, що хочете видалити бронювання від {booking.ClientName} на {booking.DisplayTime}?", "Так", "Скасувати");

                if (confirmDelete)
                {
                    await DeleteBookingAsync(booking);
                }
            }
            else
            {
                Debug.WriteLine("[NotificationPage] Delete button BindingContext is not Booking."); // ЗМІНЕНО
            }
        }

        // Асинхронний метод для видалення бронювання
        private async Task DeleteBookingAsync(Booking booking)
        {
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "delete_new_booking" }, // Дія для видалення
                    { "booking_id", booking.Id }
                };

                Debug.WriteLine($"[NotificationPage] Sending API request: delete_new_booking for ID {booking.Id}..."); // ЗМІНЕНО
                var response = await ApiClient.SendRequestAsync(requestData); // Використовуємо ваш наданий ApiClient.SendRequestAsync
                Debug.WriteLine($"[NotificationPage] Received API response for delete_new_booking ID {booking.Id}."); // ЗМІНЕНО

                // У файлі NotificationPage.xaml.cs, в методі DeleteBookingAsync
                if (response != null && response.TryGetValue("success", out object successObj))
                {
                    bool isSuccess = false;
                    try
                    {
                        isSuccess = Convert.ToBoolean(successObj); // Безпечне перетворення
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ][NotificationPage] Delete: Error converting 'success' to bool. Value: {successObj?.GetType().Name} ({successObj}). Error: {ex.Message}");
                        // Якщо конвертація невдала, вважаємо це помилкою сервера
                        Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка даних", "Неочікуваний формат відповіді сервера.", "OK"));
                        loadingIndicator.IsVisible = false; // Перемістити сюди з finally, якщо ловите помилку тут
                        loadingIndicator.IsRunning = false;
                        return; // Вийти з методу
                    }

                    if (isSuccess)
                    {
                        Debug.WriteLine($"[NotificationPage] Booking ID {booking.Id} deleted successfully.");
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            NewBookings.Remove(booking);
                            Debug.WriteLine($"[NotificationPage] Removed booking ID {booking.Id} from list.");
                        });
                        // Видалити індикатор тут при успіху, якщо він у finally
                        // loadingIndicator.IsVisible = false;
                        // loadingIndicator.IsRunning = false;
                        await DisplayAlert("Успіх", "Бронювання успішно видалено.", "OK");
                    }
                    else
                    {
                        // success=false. Отримуємо повідомлення про помилку з сервера.
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Сервер повідомив про помилку без деталізації.";
                        Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ][NotificationPage] Failed to delete booking ID {booking.Id}. Message: {errorMessage}");
                        // Видалити індикатор тут при помилці, якщо він у finally
                        // loadingIndicator.IsVisible = false;
                        // loadingIndicator.IsRunning = false;
                        Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка", $"Не вдалося видалити бронювання: {errorMessage}", "OK"));
                    }
                }
                else
                {
                    // Відповідь сервера null або відсутній ключ "success" - критична помилка формату відповіді
                    Debug.WriteLine("[ПОМИЛКА КЛІЄНТ][NotificationPage] Delete: API response is null or missing 'success' key.");
                    Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка зв'язку", "Неочікувана відповідь від сервера (відсутній ключ 'success').", "OK"));
                    // Видалити індикатор тут, якщо він у finally
                    // loadingIndicator.IsVisible = false;
                    // loadingIndicator.IsRunning = false;
                }
                // Прибрати індикатор з finally, якщо ви обробляєте всі гілки тут
                // Блок finally залишається тільки для очищення інших ресурсів, якщо є.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[КРИТИЧНА ПОМИЛКА КЛІЄНТ][NotificationPage] Error deleting booking ID {booking.Id}: {ex.Message}\n{ex.StackTrace}"); // ЗМІНЕНО
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Критична помилка", $"Помилка при видаленні бронювання: {ex.Message}", "OK");
                });
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;
                    Debug.WriteLine("[NotificationPage] loadingIndicator.IsRunning = false (delete finally)"); // ЗМІНЕНО
                });
            }
        }


        // Обробник натискання кнопки "Підтвердити"
        private async void OnConfirmBookingClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.BindingContext is Booking booking)
            {
                Debug.WriteLine($"[NotificationPage] Confirm clicked for booking ID: {booking.Id}"); // ЗМІНЕНО

                bool confirmConfirm = await DisplayAlert("Підтвердження", $"Ви впевнені, що хочете підтвердити бронювання від {booking.ClientName} на {booking.DisplayTime}?", "Так", "Скасувати");

                if (confirmConfirm)
                {
                    await ConfirmBookingAsync(booking);
                }
            }
            else
            {
                Debug.WriteLine("[NotificationPage] Confirm button BindingContext is not Booking."); // ЗМІНЕНО
            }
        }

        // Асинхронний метод для підтвердження бронювання (зміни статусу на Pending)
        private async Task ConfirmBookingAsync(Booking booking)
        {
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "update_booking_status" }, // Дія для зміни статусу
                    { "booking_id", booking.Id },
                    { "status", "Pending" } // Новий статус
                    // Можливо, потрібно передати manager_id, який підтверджує?
                    // { "manager_id", _managerId }
                };

                Debug.WriteLine($"[NotificationPage] Sending API request: update_booking_status for ID {booking.Id} to Pending..."); // ЗМІНЕНО
                var response = await ApiClient.SendRequestAsync(requestData); // Використовуємо ваш наданий ApiClient.SendRequestAsync
                Debug.WriteLine($"[NotificationPage] Received API response for update_booking_status ID {booking.Id}."); // ЗМІНЕНО


                // У файлі NotificationPage.xaml.cs, в методі ConfirmBookingAsync
                if (response != null && response.TryGetValue("success", out object successObj))
                {
                    bool isSuccess = false;
                    try
                    {
                        isSuccess = Convert.ToBoolean(successObj); // Безпечне перетворення
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ][NotificationPage] Confirm: Error converting 'success' to bool. Value: {successObj?.GetType().Name} ({successObj}). Error: {ex.Message}");
                        // Якщо конвертація невдала, вважаємо це помилкою сервера
                        Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка даних", "Неочікуваний формат відповіді сервера.", "OK"));
                        loadingIndicator.IsVisible = false; // Перемістити сюди з finally, якщо ловите помилку тут
                        loadingIndicator.IsRunning = false;
                        return; // Вийти з методу
                    }

                    if (isSuccess)
                    {
                        Debug.WriteLine($"[NotificationPage] Booking ID {booking.Id} status updated to Pending successfully.");
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            NewBookings.Remove(booking); // Видаляємо зі списку "нових"
                            Debug.WriteLine($"[NotificationPage] Removed booking ID {booking.Id} from list after confirmation.");
                        });
                        // Видалити індикатор тут при успіху, якщо він у finally
                        // loadingIndicator.IsVisible = false;
                        // loadingIndicator.IsRunning = false;
                        await DisplayAlert("Успіх", "Бронювання успішно підтверджено.", "OK");
                    }
                    else
                    {
                        // success=false. Отримуємо повідомлення про помилку з сервера.
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Сервер повідомив про помилку без деталізації.";
                        Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ][NotificationPage] Failed to confirm booking ID {booking.Id}. Message: {errorMessage}");
                        // Видалити індикатор тут при помилці, якщо він у finally
                        // loadingIndicator.IsVisible = false;
                        // loadingIndicator.IsRunning = false;
                        Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка", $"Не вдалося підтвердити бронювання: {errorMessage}", "OK"));
                    }
                }
                else
                {
                    // Відповідь сервера null або відсутній ключ "success" - критична помилка формату відповіді
                    Debug.WriteLine("[ПОМИЛКА КЛІЄНТ][NotificationPage] Confirm: API response is null or missing 'success' key.");
                    Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка зв'язку", "Неочікувана відповідь від сервера (відсутній ключ 'success').", "OK"));
                    // Видалити індикатор тут, якщо він у finally
                    // loadingIndicator.IsVisible = false;
                    // loadingIndicator.IsRunning = false;
                }
                // Прибрати індикатор з finally, якщо ви обробляєте всі гілки тут
                // Блок finally залишається тільки для очищення інших ресурсів, якщо є.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[КРИТИЧНА ПОМИЛКА КЛІЄНТ][NotificationPage] Error confirming booking ID {booking.Id}: {ex.Message}\n{ex.StackTrace}"); // ЗМІНЕНО
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Критична помилка", $"Помилка при підтвердженні бронювання: {ex.Message}", "OK");
                });
            }
            finally
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;
                    Debug.WriteLine("[NotificationPage] loadingIndicator.IsRunning = false (confirm finally)"); // ЗМІНЕНО
                });
            }
        }

        // Залишаємо або видаляємо цей метод, якщо не потрібен
        private void NewBookingsList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (sender is ListView lv) lv.SelectedItem = null; // Знімаємо виділення
        }
    }
}