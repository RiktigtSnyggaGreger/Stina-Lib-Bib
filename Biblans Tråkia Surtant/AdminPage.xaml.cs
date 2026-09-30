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

            // Automatically load all grids when page opens
            LoadUsers();
            LoadActiveLoans();
            LoadInvoicedOverdueLoans();
        }

        private async void LoadUsers()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT User_ID, Name, Lastname, Email, IsAdmin FROM User;";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);
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

        private async void LoadInvoicedOverdueLoans()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Matchar XAML: FullName & MediaTitle
                    string queryInvoicedLoans = @"
                    SELECT 
                        Loans.Loan_ID,
                        Copies.Copy_ID,
                        CONCAT(User.Name, ' ', User.Lastname) AS FullName,
                        Media.Name AS MediaTitle,
                        Loans.DueDate AS DueDate,
                        Loans.InvoiceAmount AS InvoiceAmount
                    FROM Loans
                    JOIN User ON Loans.User_ID = User.User_ID
                    JOIN Copies ON Loans.Copy_ID = Copies.Copy_ID
                    JOIN Media ON Copies.Media_ID = Media.Media_ID
                    WHERE Loans.InvoiceAmount > 0;";

                    using (var cmd = new MySqlCommand(queryInvoicedLoans, connection))
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        InvoicedLoansGrid.ItemsSource = dataTable.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte hämta fakturerade lån:\n{ex.Message}",
                                "Fel vid laddning av lån",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private async void LoadActiveLoans()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Matchar XAML: FullName & MediaTitle
                    string queryActiveLoans = @"
                    SELECT 
                        Loans.Loan_ID,
                        Copies.Copy_ID,
                        CONCAT(User.Name, ' ', User.Lastname) AS FullName,
                        Media.Name AS MediaTitle,
                        Loans.DueDate AS DueDate
                    FROM Loans
                    JOIN User ON Loans.User_ID = User.User_ID
                    JOIN Copies ON Loans.Copy_ID = Copies.Copy_ID
                    JOIN Media ON Copies.Media_ID = Media.Media_ID
                    WHERE Loans.IsReturned = FALSE 
                      AND (Loans.InvoiceAmount = 0 OR Loans.InvoiceAmount IS NULL);";

                    using (var cmd = new MySqlCommand(queryActiveLoans, connection))
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        ActiveLoansGrid.ItemsSource = dataTable.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte hämta aktiva lån:\n{ex.Message}",
                                "Fel vid laddning av lån",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

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

                Create_User_Name.Clear();
                Create_User_Lastname.Clear();
                Create_User_Email.Clear();
                Create_User_Password.Clear();
                Create_User_IsAdmin.IsChecked = false;

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

        private void Btn_Goto_Media_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new AdminMeidaPage());
        }

        private async void Btn_Delete_Selected_Click(object sender, RoutedEventArgs e)
        {
            if (Admin_Show_Users.SelectedItem == null)
            {
                MessageBox.Show("Vänligen markera en användare i tabellen först.",
                                "Ingen markering",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            DataRowView selectedRow = (DataRowView)Admin_Show_Users.SelectedItem;
            int userId = Convert.ToInt32(selectedRow["User_ID"]);
            string userName = selectedRow["Name"].ToString();
            string userLastName = selectedRow["Lastname"].ToString();

            MessageBoxResult confirm = MessageBox.Show(
                $"Är du säker på att du vill ta bort {userName} {userLastName} (ID: {userId})?",
                "Bekräfta borttagning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = "DELETE FROM User WHERE User_ID = @UserId;";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show($"Användare {userName} {userLastName} har tagits bort.",
                                "Framgång",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte ta bort användaren:\n{ex.Message}",
                                "Fel vid borttagning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void ActiveLoansGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private async void Btn_Admin_Deloan_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button?.DataContext is DataRowView selectedRow)
            {
                int loanId = Convert.ToInt32(selectedRow["Loan_ID"]);
                int copyId = Convert.ToInt32(selectedRow["Copy_ID"]);
                string user = selectedRow["FullName"].ToString();
                string title = selectedRow["MediaTitle"].ToString();

                MessageBoxResult confirm = MessageBox.Show(
                    $"Vill du återlämna \"{title}\" lånad av {user}?",
                    "Bekräfta återlämning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                    return;

                try
                {
                    using (var connection = new MySqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        string updateLoanQuery = @"
                        UPDATE Loans 
                        SET IsReturned = TRUE, ReturnedDate = NOW() 
                        WHERE Loan_ID = @LoanId;";

                        using (var cmd = new MySqlCommand(updateLoanQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@LoanId", loanId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string updateCopyQuery = @"
                        UPDATE Copies 
                        SET Is_Loaned = FALSE 
                        WHERE Copy_ID = @CopyId;";

                        using (var cmd = new MySqlCommand(updateCopyQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@CopyId", copyId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    MessageBox.Show("Lånet har återlämnats!", "Framgång", MessageBoxButton.OK, MessageBoxImage.Information);

                    LoadActiveLoans();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kunde inte återlämna lån:\n{ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void Btn_Paid_And_Return_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                try
                {
                    int loanId = Convert.ToInt32(row["Loan_ID"]);
                    int copyId = Convert.ToInt32(row["Copy_ID"]);
                    string title = row["MediaTitle"]?.ToString() ?? "Okänd media";
                    int amount = row["InvoiceAmount"] != DBNull.Value ? Convert.ToInt32(row["InvoiceAmount"]) : 0;

                    var result = MessageBox.Show(
                        $"Vill du markera fakturan på {amount} kr som betald och ta bort exemplar {copyId} ({title}) från systemet?",
                        "Bekräfta betalning och borttagning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;

                    using (var connection = new MySqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        using (var transaction = await connection.BeginTransactionAsync())
                        {
                            try
                            {
                                string deleteLoanQuery = "DELETE FROM Loans WHERE Loan_ID = @LoanID;";
                                using (var cmdLoan = new MySqlCommand(deleteLoanQuery, connection, transaction))
                                {
                                    cmdLoan.Parameters.AddWithValue("@LoanID", loanId);
                                    await cmdLoan.ExecuteNonQueryAsync();
                                }

                                string deleteOtherLoansQuery = "DELETE FROM Loans WHERE Copy_ID = @CopyID;";
                                using (var cmdOtherLoans = new MySqlCommand(deleteOtherLoansQuery, connection, transaction))
                                {
                                    cmdOtherLoans.Parameters.AddWithValue("@CopyID", copyId);
                                    await cmdOtherLoans.ExecuteNonQueryAsync();
                                }

                                string deleteCopyQuery = "DELETE FROM Copies WHERE Copy_ID = @CopyID;";
                                using (var cmdCopy = new MySqlCommand(deleteCopyQuery, connection, transaction))
                                {
                                    cmdCopy.Parameters.AddWithValue("@CopyID", copyId);
                                    await cmdCopy.ExecuteNonQueryAsync();
                                }

                                await transaction.CommitAsync();
                            }
                            catch
                            {
                                await transaction.RollbackAsync();
                                throw;
                            }
                        }
                    }

                    MessageBox.Show("Betalning registrerad och exemplaret har tagits bort ur systemet!",
                                    "Framgång",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);

                    LoadActiveLoans();
                    LoadInvoicedOverdueLoans();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kunde inte slutföra åtgärden:\n{ex.Message}",
                                    "Fel vid hantering",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }
    }
}