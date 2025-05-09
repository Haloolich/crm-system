// ClubDetailsPage.xaml.cs

using System;
using System.Collections.Generic;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using System.Diagnostics;
using crmV1.Models; // !!! Переконайтесь, що цей using вказує на правильний простір імен з класом Club !!!
using crmV1.Services;
using Newtonsoft.Json.Linq;

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ClubDetailsPage : ContentPage
    {
        private int _clubId;

        public ClubDetailsPage(int clubId)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            Title = "Деталі клубу";

            _clubId = clubId;

            LoadClubDetailsAsync(_clubId);
        }

        private async void LoadClubDetailsAsync(int clubId)
        {
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
            saveButton.IsEnabled = false;

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "get_club_details" },
                    { "club_id", clubId }
                };

                Debug.WriteLine($"[ClubDetailsPage] Sending get_club_details request for ID: {clubId}...");
                var response = await ApiClient.SendRequestAsync(requestData);
                Debug.WriteLine($"[ClubDetailsPage] Received response for get_club_details: {response?.GetType().Name ?? "null"}");

                if (response != null && response.ContainsKey("success"))
                {
                    bool isSuccess = false;
                    try { isSuccess = Convert.ToBoolean(response["success"]); } catch { }

                    if (isSuccess)
                    {
                        if (response.TryGetValue("club", out object clubObject) && clubObject is JObject clubJObject)
                        {
                            try
                            {
                                // Використовуємо Club, тому клас Club має бути доступний
                                var club = clubJObject.ToObject<Club>();
                                if (club != null)
                                {
                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        clubIdEntry.Text = club.ClubId.ToString();
                                        nameEntry.Text = club.Name;
                                        addressEntry.Text = club.Address;
                                        phoneEntry.Text = club.PhoneNumber;
                                        emailEntry.Text = club.Email;
                                        maxPsEntry.Text = club.MaxPsZones.ToString();
                                        maxVrEntry.Text = club.MaxVrQuestZones.ToString();
                                        if (statusPicker.Items.Contains(club.Status))
                                        {
                                            statusPicker.SelectedItem = club.Status;
                                        }
                                        else
                                        {
                                            statusPicker.SelectedItem = "Open";
                                        }
                                        Debug.WriteLine($"[ClubDetailsPage] Populated fields for club ID {club.ClubId}. Status: {club.Status}");
                                    });
                                }
                                else
                                {
                                    Debug.WriteLine("[ClubDetailsPage] Failed to deserialize club object from response.");
                                    await DisplayAlert("Помилка", "Не вдалося отримати дані клубу.", "OK");
                                    await Navigation.PopAsync();
                                }
                            }
                            catch (Exception deserializeEx)
                            {
                                Debug.WriteLine($"[ClubDetailsPage] Error deserializing club object from JObject: {deserializeEx.Message}");
                                await DisplayAlert("Помилка даних", "Помилка обробки даних клубу.", "OK");
                                await Navigation.PopAsync();
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[ClubDetailsPage] Server response success=true, but missing 'club' JObject.");
                            await DisplayAlert("Помилка даних", "Сервер повернув неочікуваний формат даних клубу.", "OK");
                            await Navigation.PopAsync();
                        }
                    }
                    else
                    {
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Не вдалося завантажити деталі клубу.";
                        Debug.WriteLine($"[ClubDetailsPage] Server reported error loading details: {errorMessage}");
                        await DisplayAlert("Помилка сервера", errorMessage, "OK");
                        await Navigation.PopAsync();
                    }
                }
                else
                {
                    Debug.WriteLine("[ClubDetailsPage] Server response has unexpected format (missing 'success' key).");
                    await DisplayAlert("Помилка", "Неочікуваний формат відповіді від сервера.", "OK");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClubDetailsPage] Critical error loading club details: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Критична помилка", $"Не вдалося завантажити деталі клубу: {ex.Message}", "OK");
                await Navigation.PopAsync();
            }
            finally
            {
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
                saveButton.IsEnabled = true;
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            string name = nameEntry.Text;
            string address = addressEntry.Text;
            string phone = phoneEntry.Text;
            string email = emailEntry.Text;
            int maxPs = 0;
            int maxVr = 0;
            string status = statusPicker.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(status))
            {
                await DisplayAlert("Помилка", "Будь ласка, заповніть назву, адресу та виберіть статус клубу.", "OK");
                return;
            }

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


            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
            saveButton.IsEnabled = false;

            try
            {
                var clubData = new Dictionary<string, object>
                {
                    { "action", "update_club" },
                    { "club_id", _clubId },
                    { "name", name },
                    { "address", address },
                    { "phone_number", phone ?? string.Empty },
                    { "email", email ?? string.Empty },
                    { "max_ps_zones", maxPs },
                    { "max_vr_quest_zones", maxVr },
                    { "status", status }
                };

                Debug.WriteLine($"[ClubDetailsPage] Sending update_club request for ID: {_clubId}...");
                var response = await ApiClient.SendRequestAsync(clubData);
                Debug.WriteLine($"[ClubDetailsPage] Received response for update_club: {response?.GetType().Name ?? "null"}");

                if (response != null && response.ContainsKey("success"))
                {
                    bool isSuccess = false;
                    try { isSuccess = Convert.ToBoolean(response["success"]); } catch { }

                    if (isSuccess)
                    {
                        Debug.WriteLine($"[ClubDetailsPage] Club {_clubId} updated successfully by server.");
                        string successMessage = response.ContainsKey("message") ? response["message"].ToString() : "Зміни збережено успішно.";
                        await DisplayAlert("Успіх", successMessage, "OK");

                        await Navigation.PopAsync();
                    }
                    else
                    {
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Не вдалося зберегти зміни (невідома причина).";
                        Debug.WriteLine($"[ClubDetailsPage] Server reported update error: {errorMessage}");
                        await DisplayAlert("Помилка сервера", errorMessage, "OK");
                    }
                }
                else
                {
                    Debug.WriteLine("[ClubDetailsPage] Server update response has unexpected format.");
                    await DisplayAlert("Помилка", "Неочікуваний формат відповіді від сервера.", "OK");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClubDetailsPage] Critical error updating club {_clubId}: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Критична помилка", $"Не вдалося зберегти зміни: {ex.Message}", "OK");
            }
            finally
            {
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
                saveButton.IsEnabled = true;
            }
        }

        // private async void OnCancelClicked(object sender, EventArgs e)
        // {
        // Debug.WriteLine("[ClubDetailsPage] OnCancelClicked called. Navigating back.");
        // await Navigation.PopAsync();
        //}

        private void StatusPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            Debug.WriteLine($"[ClubDetailsPage] StatusPicker value changed to: {statusPicker.SelectedItem}");
        }
    }
}