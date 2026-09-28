using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant
{
    /// <summary>
    /// Interaction logic for AdminPage.xaml
    /// </summary>
    public partial class AdminPage : Page
    {
        // Connection string to local MySQL database
        private string connectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";

        public AdminPage()
        {
            InitializeComponent();

            // Automatically load the users into the grid when this page opens
            LoadUsers();
        }

        private async void LoadUsers()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Query that retrieves user info while omitting PasswordHash
                    string query = "SELECT User_ID, Name, Lastname, Email, IsAdmin FROM User;";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dataTable = new DataTable();

                        // Fill the DataTable with query results
                        adapter.Fill(dataTable);

                        // Bind the data directly to the WPF UI control
                        Admin_Show_Users.ItemsSource = dataTable.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte hämta användare:\n{ex.Message}",
                                "Fel vid laddning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}