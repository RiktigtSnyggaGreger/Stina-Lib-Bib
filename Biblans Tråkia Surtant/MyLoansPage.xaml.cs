using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant;

public partial class MyLoansPage : Page
{
    private const string ConnectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";
    private readonly int _userId;
    private readonly DispatcherTimer _overdueTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _isLoading;

    public MyLoansPage(int userId)
    {
        InitializeComponent();
        _userId = userId;
        _overdueTimer.Tick += OverdueTimer_Tick;
        Loaded += MyLoansPage_Loaded;
        Unloaded += MyLoansPage_Unloaded;
    }

    private async void MyLoansPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadLoansAsync();
        _overdueTimer.Start();
    }

    private void MyLoansPage_Unloaded(object sender, RoutedEventArgs e)
        => _overdueTimer.Stop();

    private async void OverdueTimer_Tick(object? sender, EventArgs e)
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
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            await UpdateOverdueInvoicesAsync(_userId);
            var loans = await GetActiveLoansAsync(_userId);
            LoansGrid.ItemsSource = loans;
            var totalInvoice = loans.Sum(loan => loan.InvoiceAmount);
            Txt_Status.Text = loans.Count == 0
                ? "Du har inga aktiva lån."
                : $"Du har {loans.Count} aktivt/aktiva lån. Obetald avgift: {totalInvoice}.";
        }
        catch (Exception ex)
        {
            Txt_Status.Text = "Lånen kunde inte laddas.";
            MessageBox.Show($"Kunde inte ladda dina lån: {ex.Message}", "Databasfel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static async Task UpdateOverdueInvoicesAsync(int userId)
    {
        const string sql = @"
UPDATE Loans l
JOIN Copies c ON c.Copy_ID = l.Copy_ID
JOIN Media m ON m.Media_ID = c.Media_ID
SET l.InvoiceAmount = CEILING(COALESCE(m.Value, 0) * 1.5)
WHERE l.User_ID = @userId
  AND l.IsReturned = 0
  AND l.DueDate < UTC_TIMESTAMP()
  AND l.InvoiceAmount = 0;";

        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userId", userId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<LoanItem>> GetActiveLoansAsync(int userId)
    {
        const string sql = @"
SELECT l.Loan_ID, m.Name AS Title, l.BorrowedDate, l.DueDate, l.InvoiceAmount,
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
                DueDate = reader.GetDateTime("DueDate"),
                InvoiceAmount = reader.GetInt32("InvoiceAmount")
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
            await UpdateOverdueInvoicesAsync(_userId);
            var result = await ReturnLoanAsync(selectedLoan.LoanId, _userId);
            if (result == ReturnResult.InvoiceDue)
            {
                await LoadLoansAsync();
                var currentLoan = LoansGrid.Items.OfType<LoanItem>()
                    .FirstOrDefault(loan => loan.LoanId == selectedLoan.LoanId);
                var invoiceAmount = currentLoan?.InvoiceAmount ?? selectedLoan.InvoiceAmount;

                MessageBox.Show(
                    $"Stopp! '{selectedLoan.Title}' har en obetald avgift på {invoiceAmount}. Bibliotekets bokvakt säger: betala först, lämna tillbaka sen! 📚💸",
                    "Bokvakten säger nej",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (result == ReturnResult.NotFound)
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

    private static async Task<ReturnResult> ReturnLoanAsync(int loanId, int userId)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            int? copyId = null;
            var invoiceAmount = 0;
            await using (var findLoan = new MySqlCommand(
                "SELECT Copy_ID, InvoiceAmount FROM Loans WHERE Loan_ID = @loanId AND User_ID = @userId AND IsReturned = 0 FOR UPDATE",
                connection,
                transaction))
            {
                findLoan.Parameters.AddWithValue("@loanId", loanId);
                findLoan.Parameters.AddWithValue("@userId", userId);
                await using var reader = await findLoan.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    copyId = reader.GetInt32("Copy_ID");
                    invoiceAmount = reader.GetInt32("InvoiceAmount");
                }
            }

            if (copyId is null)
            {
                await transaction.RollbackAsync();
                return ReturnResult.NotFound;
            }

            if (invoiceAmount > 0)
            {
                await transaction.RollbackAsync();
                return ReturnResult.InvoiceDue;
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
                    return ReturnResult.NotFound;
                }
            }

            await using (var updateCopy = new MySqlCommand("UPDATE Copies SET Is_Loaned = 0 WHERE Copy_ID = @copyId", connection, transaction))
            {
                updateCopy.Parameters.AddWithValue("@copyId", copyId.Value);
                await updateCopy.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return ReturnResult.Returned;
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    private enum ReturnResult
    {
        Returned,
        InvoiceDue,
        NotFound
    }

    private sealed class LoanItem
    {
        public int LoanId { get; init; }
        public string MediaType { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Authors { get; init; } = string.Empty;
        public DateTime BorrowedDate { get; init; }
        public DateTime DueDate { get; init; }
        public int InvoiceAmount { get; init; }
    }
}
