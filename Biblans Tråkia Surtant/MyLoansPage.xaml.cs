using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant;

public partial class MyLoansPage : Page
{
    private const string ConnectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";
    private readonly int _userId;

    public MyLoansPage(int userId)
    {
        InitializeComponent();
        _userId = userId;
        Loaded += MyLoansPage_Loaded;
    }

    private async void MyLoansPage_Loaded(object sender, RoutedEventArgs e)
        => await LoadLoansAsync();

    private async void Btn_Refresh_Click(object sender, RoutedEventArgs e)
        => await LoadLoansAsync();

    private void Btn_Back_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService?.CanGoBack == true)
            NavigationService.GoBack();
    }

    private async Task LoadLoansAsync()
    {
        try
        {
            var loans = await GetActiveLoansAsync(_userId);
            LoansGrid.ItemsSource = loans;
            Txt_Status.Text = loans.Count == 0
                ? "Du har inga aktiva lån."
                : $"Du har {loans.Count} aktivt/aktiva lån.";
        }
        catch (Exception ex)
        {
            Txt_Status.Text = "Lånen kunde inte laddas.";
            MessageBox.Show($"Kunde inte ladda dina lån: {ex.Message}", "Databasfel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task<List<LoanItem>> GetActiveLoansAsync(int userId)
    {
        const string sql = @"
SELECT l.Loan_ID, m.Name AS Title, l.BorrowedDate, l.DueDate,
       CASE
           WHEN b.Media_ID IS NOT NULL THEN 'Bok'
           WHEN movie.Media_ID IS NOT NULL THEN 'Film'
           WHEN audio.Media_ID IS NOT NULL THEN 'Ljudbok'
       END AS MediaType,
       COALESCE(authors.Authors, '') AS Authors
FROM Loans l
JOIN Copies c ON c.Copy_ID = l.Copy_ID
JOIN Media m ON m.Media_ID = c.Media_ID
LEFT JOIN Book b ON b.Media_ID = m.Media_ID
LEFT JOIN Movie movie ON movie.Media_ID = m.Media_ID
LEFT JOIN AudioBook audio ON audio.Media_ID = m.Media_ID
LEFT JOIN (
    SELECT ma.Media_ID, GROUP_CONCAT(CONCAT(a.Name, ' ', a.LastName) ORDER BY a.LastName, a.Name SEPARATOR ', ') AS Authors
    FROM Media_Authors ma
    JOIN Author a ON a.Author_ID = ma.Author_ID
    GROUP BY ma.Media_ID
) authors ON authors.Media_ID = m.Media_ID
WHERE l.User_ID = @userId AND l.IsReturned = 0
  AND (b.Media_ID IS NOT NULL OR movie.Media_ID IS NOT NULL OR audio.Media_ID IS NOT NULL)
ORDER BY l.DueDate, m.Name;";

        var loans = new List<LoanItem>();
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            loans.Add(new LoanItem
            {
                LoanId = reader.GetInt32("Loan_ID"),
                MediaType = reader.GetString("MediaType"),
                Title = reader.GetString("Title"),
                Authors = reader.GetString("Authors"),
                BorrowedDate = reader.GetDateTime("BorrowedDate"),
                DueDate = reader.GetDateTime("DueDate")
            });
        }

        return loans;
    }

    private async void Btn_Return_Click(object sender, RoutedEventArgs e)
    {
        if (LoansGrid.SelectedItem is not LoanItem selectedLoan)
        {
            MessageBox.Show("Välj ett lån att lämna tillbaka.", "Inget lån valt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            if (!await ReturnLoanAsync(selectedLoan.LoanId, _userId))
            {
                MessageBox.Show("Lånet är redan återlämnat eller tillhör inte ditt konto.", "Lånet kunde inte återlämnas", MessageBoxButton.OK, MessageBoxImage.Warning);
                await LoadLoansAsync();
                return;
            }

            MessageBox.Show($"'{selectedLoan.Title}' har återlämnats.", "Återlämning klar", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadLoansAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kunde inte lämna tillbaka materialet: {ex.Message}", "Databasfel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task<bool> ReturnLoanAsync(int loanId, int userId)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            int copyId;
            await using (var findLoan = new MySqlCommand(
                "SELECT Copy_ID FROM Loans WHERE Loan_ID = @loanId AND User_ID = @userId AND IsReturned = 0 FOR UPDATE",
                connection,
                transaction))
            {
                findLoan.Parameters.AddWithValue("@loanId", loanId);
                findLoan.Parameters.AddWithValue("@userId", userId);
                var result = await findLoan.ExecuteScalarAsync();
                if (result is null || result == DBNull.Value)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                copyId = Convert.ToInt32(result);
            }

            await using (var updateLoan = new MySqlCommand(
                "UPDATE Loans SET ReturnedDate = UTC_TIMESTAMP(), IsReturned = 1 WHERE Loan_ID = @loanId AND User_ID = @userId AND IsReturned = 0",
                connection,
                transaction))
            {
                updateLoan.Parameters.AddWithValue("@loanId", loanId);
                updateLoan.Parameters.AddWithValue("@userId", userId);
                if (await updateLoan.ExecuteNonQueryAsync() != 1)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            await using (var updateCopy = new MySqlCommand("UPDATE Copies SET Is_Loaned = 0 WHERE Copy_ID = @copyId", connection, transaction))
            {
                updateCopy.Parameters.AddWithValue("@copyId", copyId);
                await updateCopy.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    private sealed class LoanItem
    {
        public int LoanId { get; init; }
        public string MediaType { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Authors { get; init; } = string.Empty;
        public DateTime BorrowedDate { get; init; }
        public DateTime DueDate { get; init; }
    }
}
