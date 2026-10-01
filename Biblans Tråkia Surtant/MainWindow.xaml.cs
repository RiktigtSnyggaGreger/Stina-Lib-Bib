using System;
using System.Windows;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant
{
    public partial class MainWindow : Window
    {
        private string connectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";

        public MainWindow()
        {
            InitializeComponent();

            // Ladda mediakatalogen direkt vid start som gäst (ID 0) utan krav på inloggning
            MainFrame.Navigate(new StartPage(0));
        }

        private void Btn_OpenLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Visible;
            Login_Email_TextBox.Focus();
        }

        private void Btn_CloseLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
        }

        private async void Btn_Connection_Click(object sender, RoutedEventArgs e)
        {
            string email = Login_Email_TextBox.Text.Trim();
            string password = Login_Password_Box.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vänligen fyll i både e-post och lösenord.", "Valideringsfel", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"SELECT User_ID, Name, Lastname, IsAdmin 
                                     FROM User 
                                     WHERE Email = @Email AND PasswordHash = @Password;";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@Password", password);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int userId = reader.GetInt32("User_ID");
                                string firstName = reader.GetString("Name");
                                string lastName = reader.GetString("Lastname");
                                bool isAdmin = reader.GetBoolean("IsAdmin");
                                Session.CurrentUserId = userId;
                                Session.CurrentUserName = $"{firstName} {lastName}";

                                MessageBox.Show($"Välkommen {firstName} {lastName}!\nRoll: {(isAdmin ? "Admin" : "användare")}",
                                                "Inloggad",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Information);

                                LoginPanel.Visibility = Visibility.Collapsed;
                                Btn_OpenLogin.Visibility = Visibility.Collapsed;
                                Btn_Logout.Visibility = Visibility.Visible;
                                Login_Email_TextBox.Clear();
                                Login_Password_Box.Clear();

                                // Navigera till rätt sida med den inloggade användarens ID
                                if (isAdmin)
                                {
                                    MainFrame.Navigate(new AdminPage());
                                }
                                else
                                {
                                    MainFrame.Navigate(new StartPage(userId));
                                }
                            }
                            else
                            {
                                MessageBox.Show("Felaktig e-post eller lösenord.", "Inloggning misslyckades", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ett fel uppstod vid anslutning:\n{ex.Message}", "Anslutningsfel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Btn_Logout_Click(object sender, RoutedEventArgs e)
        {
            Session.CurrentUserId = null;
            Session.CurrentUserName = null;

            Btn_Logout.Visibility = Visibility.Collapsed;
            Btn_OpenLogin.Visibility = Visibility.Visible;

            // Gå tillbaka till startsidan som gäst (ID 0)
            MainFrame.Navigate(new StartPage(0));
        }
    }
}