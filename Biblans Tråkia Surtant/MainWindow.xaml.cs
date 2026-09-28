using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string connectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Btn_Connection_Click(object sender, RoutedEventArgs e)
        {
            // Read values from XAML controls
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

                    // Updated query targeting English column names (Name, Lastname)
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

                                // Navigate or load user session here
                                if (isAdmin)
                                {
                                    LoginPanel.Visibility = Visibility.Collapsed;
                                    //User is an admin -> Load AdminPage inside MainFrame
                                    MainFrame.Navigate(new AdminPage());
                                }
                                else
                                {
                                    // User is a standard user -> Load StartPage inside MainFrame
                                    LoginPanel.Visibility = Visibility.Collapsed;
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
    }
}