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

        // Button Click Event Handler (matches Click="Btn_Create_User_Click" in XAML)
        private void Btn_Create_User_Click(object sender, RoutedEventArgs e)
        {
            Create_User();
        }

        private async void Create_User()
        {
            string name = Create_User_Name.Text.Trim();
            string lastname = Create_User_Lastname.Text.Trim();
            string email = Create_User_Email.Text.Trim();
            string password = Create_User_Password.Password;
            bool isAdmin = Create_User_IsAdmin.IsChecked == true;

            // Validate all required fields
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(lastname) ||
                string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vänligen fyll i alla fält.", "Valideringsfel", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"INSERT INTO User (Name, Lastname, Email, PasswordHash, IsAdmin) 
                                     VALUES (@Name, @Lastname, @Email, @Password, @IsAdmin);";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Lastname", lastname);
                        cmd.Parameters.AddWithValue("@Email", email);
                        cmd.Parameters.AddWithValue("@Password", password);
                        cmd.Parameters.AddWithValue("@IsAdmin", isAdmin);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show("Användare har skapats!", "Framgång", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear input controls
                Create_User_Name.Clear();
                Create_User_Lastname.Clear();
                Create_User_Email.Clear();
                Create_User_Password.Clear();
                Create_User_IsAdmin.IsChecked = false;

                // Refresh the table to display the newly inserted user
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte skapa användare:\n{ex.Message}",
                                "Fel",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}