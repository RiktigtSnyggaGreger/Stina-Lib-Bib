using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant;

public partial class Historik : Page
{
    private const string ConnectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";
    private readonly int _userId;

    public Historik() : this(0)
    {
    }

    public Historik(int userId)
    {
        InitializeComponent();
        _userId = userId;
        Loaded += Historik_Loaded;
    }

    private async void Historik_Loaded(object sender, RoutedEventArgs e)
        => await LoadHistoryAsync();

    private async void Btn_Refresh_Click(object sender, RoutedEventArgs e)
        => await LoadHistoryAsync();

    private void Btn_Back_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService?.CanGoBack == true)
            NavigationService.GoBack();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var history = await GetHistoryAsync(_userId);
            HistoryGrid.ItemsSource = history;
            Txt_Status.Text = history.Count == 0
                ? "Du har ingen lånehistorik ännu."
                : $"Visar {history.Count} lån.";
        }
        catch (Exception ex)
        {
            Txt_Status.Text = "Lånehistoriken kunde inte laddas.";
            MessageBox.Show($"Kunde inte ladda lånehistoriken: {ex.Message}", "Databasfel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task<List<LoanHistoryItem>> GetHistoryAsync(int userId)
    {
        const string sql = @"
SELECT l.Loan_ID,
       m.Name AS Title,
       CASE
           WHEN b.Media_ID IS NOT NULL THEN 'Bok'
           WHEN movie.Media_ID IS NOT NULL THEN 'Film'
           WHEN audio.Media_ID IS NOT NULL THEN 'Ljudbok'
           ELSE 'Äldre media'
       END AS MediaType,
       COALESCE(authors.Authors, '') AS Authors,
       l.BorrowedDate,
       l.DueDate,
       l.ReturnedDate,
       l.IsReturned,
       COALESCE(l.InvoiceAmount, 0) AS InvoiceAmount
FROM Loans l
JOIN Copies c ON c.Copy_ID = l.Copy_ID
JOIN Media m ON m.Media_ID = c.Media_ID
LEFT JOIN Book b ON b.Media_ID = m.Media_ID
LEFT JOIN Movie movie ON movie.Media_ID = m.Media_ID
LEFT JOIN AudioBook audio ON audio.Media_ID = m.Media_ID
LEFT JOIN (
    SELECT ma.Media_ID,
           GROUP_CONCAT(CONCAT(a.Name, ' ', a.LastName) ORDER BY a.LastName, a.Name SEPARATOR ', ') AS Authors
    FROM Media_Authors ma
    JOIN Author a ON a.Author_ID = ma.Author_ID
    GROUP BY ma.Media_ID
) authors ON authors.Media_ID = m.Media_ID
WHERE l.User_ID = @userId
ORDER BY l.BorrowedDate DESC, l.Loan_ID DESC;";

        var history = new List<LoanHistoryItem>();
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add(new LoanHistoryItem
            {
                MediaType = reader.GetString("MediaType"),
                Title = reader.GetString("Title"),
                Authors = reader.GetString("Authors"),
                BorrowedDate = reader.GetDateTime("BorrowedDate"),
                DueDate = reader.GetDateTime("DueDate"),
                ReturnedDate = reader.IsDBNull(reader.GetOrdinal("ReturnedDate"))
                    ? null
                    : reader.GetDateTime("ReturnedDate"),
                IsReturned = reader.GetBoolean("IsReturned"),
                InvoiceAmount = reader.GetInt32("InvoiceAmount")
            });
        }

        return history;
    }

    private sealed class LoanHistoryItem
    {
        public string MediaType { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Authors { get; init; } = string.Empty;
        public DateTime BorrowedDate { get; init; }
        public DateTime DueDate { get; init; }
        public DateTime? ReturnedDate { get; init; }
        public bool IsReturned { get; init; }
        public int InvoiceAmount { get; init; }
        public string Status => IsReturned ? "Återlämnad" : "Aktivt lån";
    }
}
