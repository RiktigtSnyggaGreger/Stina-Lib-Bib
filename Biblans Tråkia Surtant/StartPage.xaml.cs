using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant;

public partial class StartPage : Page
{
    private const string ConnectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";
    private readonly int _userId;
    private List<MediaItem> _allMedia = new();
    private ICollectionView? _mediaView;
    private readonly DispatcherTimer _overdueInvoiceTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _isUpdatingInvoices;

    public StartPage(int userId)
    {
        InitializeComponent();
        _userId = userId;
        Txt_Welcome.Text = $"Välkommen, {Session.CurrentUserName ?? "låntagare"}!";
        _overdueInvoiceTimer.Tick += OverdueInvoiceTimer_Tick;
        Loaded += StartPage_Loaded;
        Unloaded += StartPage_Unloaded;
    }

    private async void StartPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadMediaAsync();
        await UpdateOverdueInvoicesAsync();
        _overdueInvoiceTimer.Start();
    }

    private void StartPage_Unloaded(object sender, RoutedEventArgs e)
        => _overdueInvoiceTimer.Stop();

    private async void OverdueInvoiceTimer_Tick(object? sender, EventArgs e)
        => await UpdateOverdueInvoicesAsync();

    private async Task UpdateOverdueInvoicesAsync()
    {
        if (_isUpdatingInvoices)
            return;

        _isUpdatingInvoices = true;
        const string sql = @"
UPDATE Loans l
JOIN Copies c ON c.Copy_ID = l.Copy_ID
JOIN Media m ON m.Media_ID = c.Media_ID
SET l.InvoiceAmount = CEILING(COALESCE(m.Value, 0) * 1.5)
WHERE l.User_ID = @userId
  AND l.IsReturned = 0
  AND l.DueDate < UTC_TIMESTAMP()
  AND l.InvoiceAmount = 0;";

        try
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@userId", _userId);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Txt_Status.Text = $"Kunde inte kontrollera förseningsavgifter: {ex.Message}";
        }
        finally
        {
            _isUpdatingInvoices = false;
        }
    }

    private async Task LoadMediaAsync()
    {
        try
        {
            _allMedia = await GetMediaAsync();
            _mediaView = CollectionViewSource.GetDefaultView(_allMedia);
            _mediaView.Filter = MatchesSearch;
            MediaGrid.ItemsSource = _mediaView;
            UpdateCatalogStatus();
        }
        catch (Exception ex)
        {
            Txt_Status.Text = "Mediakatalogen kunde inte laddas.";
            MessageBox.Show($"Kunde inte ladda katalogen: {ex.Message}", "Databasfel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<List<MediaItem>> GetMediaAsync()
    {
        const string sql = @"
SELECT m.Media_ID, m.Name, m.Release_Year, m.Language, m.SAB, m.Description,
       CASE
           WHEN b.Media_ID IS NOT NULL THEN 'Bok'
           WHEN movie.Media_ID IS NOT NULL THEN 'Film'
           WHEN audio.Media_ID IS NOT NULL THEN 'Ljudbok'
       END AS MediaType,
       COALESCE(b.ISBN, audio.ISBN) AS ISBN,
       b.Pages,
       COALESCE(movie.ISAN, '') AS ISAN,
       COALESCE(movie.Length, audio.Length) AS Length,
       COALESCE(authors.Authors, '') AS Authors,
       CASE WHEN COALESCE(copies.TotalCopies, 0) = 0 THEN 1 ELSE copies.AvailableCopies END AS AvailableCopies
FROM Media m
LEFT JOIN Book b ON b.Media_ID = m.Media_ID
LEFT JOIN Movie movie ON movie.Media_ID = m.Media_ID
LEFT JOIN AudioBook audio ON audio.Media_ID = m.Media_ID
LEFT JOIN (
    SELECT ma.Media_ID, GROUP_CONCAT(CONCAT(a.Name, ' ', a.LastName) ORDER BY a.LastName, a.Name SEPARATOR ', ') AS Authors
    FROM Media_Authors ma
    JOIN Author a ON a.Author_ID = ma.Author_ID
    GROUP BY ma.Media_ID
) authors ON authors.Media_ID = m.Media_ID
LEFT JOIN (
    SELECT Media_ID, COUNT(*) AS TotalCopies,
           SUM(CASE WHEN Is_Loaned = 0 OR Is_Loaned IS NULL THEN 1 ELSE 0 END) AS AvailableCopies
    FROM Copies
    GROUP BY Media_ID
) copies ON copies.Media_ID = m.Media_ID
WHERE b.Media_ID IS NOT NULL OR movie.Media_ID IS NOT NULL OR audio.Media_ID IS NOT NULL
ORDER BY m.Name;";

        var mediaItems = new List<MediaItem>();
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            mediaItems.Add(new MediaItem
            {
                MediaId = reader.GetInt32("Media_ID"),
                Title = reader.GetString("Name"),
                MediaType = reader.GetString("MediaType"),
                ISBN = GetNullableString(reader, "ISBN"),
                Pages = GetNullableInt(reader, "Pages"),
                ISAN = GetNullableString(reader, "ISAN"),
                Length = GetNullableInt(reader, "Length"),
                ReleaseYear = GetNullableString(reader, "Release_Year"),
                Language = GetNullableString(reader, "Language"),
                SAB = GetNullableString(reader, "SAB"),
                Description = GetNullableString(reader, "Description"),
                Authors = reader.GetString("Authors"),
                AvailableCopies = reader.GetInt32("AvailableCopies")
            });
        }

        return mediaItems;
    }

    private static string? GetNullableString(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(column);

    private static int? GetNullableInt(MySqlDataReader reader, string column)
        => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetInt32(column);

    private bool MatchesSearch(object item)
    {
        if (item is not MediaItem media)
            return false;

        var searchText = Txt_Search.Text.Trim();
        if (searchText.Length == 0)
            return true;

        return Contains(media.Title, searchText)
            || Contains(media.Authors, searchText)
            || Contains(media.MediaType, searchText)
            || Contains(media.ISBN, searchText)
            || Contains(media.ISAN, searchText)
            || Contains(media.SAB, searchText)
            || Contains(media.ReleaseYear, searchText)
            || Contains(media.Language, searchText);
    }

    private static bool Contains(string? value, string searchText)
        => value?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) == true;

    private void Txt_Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        _mediaView?.Refresh();
        UpdateCatalogStatus();
    }

    private void UpdateCatalogStatus()
    {
        Txt_Status.Text = $"Visar {MediaGrid.Items.Count} av {_allMedia.Count} böcker, filmer och ljudböcker.";
    }

    private async void Btn_Refresh_Click(object sender, RoutedEventArgs e)
        => await LoadMediaAsync();

    private async void Btn_Borrow_Click(object sender, RoutedEventArgs e)
    {
        if (MediaGrid.SelectedItem is not MediaItem selectedMedia)
        {
            MessageBox.Show("Välj en bok, film eller ljudbok först.", "Inget material valt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var borrowed = await BorrowMediaAsync(_userId, selectedMedia.MediaId);
            if (!borrowed)
            {
                MessageBox.Show("Det valda materialet finns inte tillgängligt just nu.", "Inte tillgängligt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show($"Du har lånat {selectedMedia.MediaType.ToLowerInvariant()}en '{selectedMedia.Title}'. Förfallodatum är om 1 minut.", "Lån registrerat", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadMediaAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kunde inte låna materialet: {ex.Message}", "Databasfel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task<bool> BorrowMediaAsync(int userId, int mediaId)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Lock the media row to serialize first-copy creation and borrowing for this item.
            await using (var lockMedia = new MySqlCommand("SELECT Media_ID FROM Media WHERE Media_ID = @mediaId FOR UPDATE", connection, transaction))
            {
                lockMedia.Parameters.AddWithValue("@mediaId", mediaId);
                if (await lockMedia.ExecuteScalarAsync() is null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            const string validMediaSql = @"
SELECT EXISTS (
    SELECT Media_ID FROM Book WHERE Media_ID = @mediaId
    UNION ALL SELECT Media_ID FROM Movie WHERE Media_ID = @mediaId
    UNION ALL SELECT Media_ID FROM AudioBook WHERE Media_ID = @mediaId
);";
            await using (var validateMedia = new MySqlCommand(validMediaSql, connection, transaction))
            {
                validateMedia.Parameters.AddWithValue("@mediaId", mediaId);
                if (Convert.ToInt32(await validateMedia.ExecuteScalarAsync()) == 0)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            int copyId;
            await using (var findCopy = new MySqlCommand("SELECT Copy_ID FROM Copies WHERE Media_ID = @mediaId AND (Is_Loaned = 0 OR Is_Loaned IS NULL) LIMIT 1 FOR UPDATE", connection, transaction))
            {
                findCopy.Parameters.AddWithValue("@mediaId", mediaId);
                var availableCopy = await findCopy.ExecuteScalarAsync();

                if (availableCopy is null || availableCopy == DBNull.Value)
                {
                    await using var countCopies = new MySqlCommand("SELECT COUNT(*) FROM Copies WHERE Media_ID = @mediaId", connection, transaction);
                    countCopies.Parameters.AddWithValue("@mediaId", mediaId);
                    var copyCount = Convert.ToInt32(await countCopies.ExecuteScalarAsync());

                    if (copyCount > 0)
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }

                    await using var createCopy = new MySqlCommand("INSERT INTO Copies (Media_ID, Is_Loaned) VALUES (@mediaId, 1)", connection, transaction);
                    createCopy.Parameters.AddWithValue("@mediaId", mediaId);
                    await createCopy.ExecuteNonQueryAsync();
                    await using var getNewCopyId = new MySqlCommand("SELECT LAST_INSERT_ID()", connection, transaction);
                    copyId = Convert.ToInt32(await getNewCopyId.ExecuteScalarAsync());
                }
                else
                {
                    copyId = Convert.ToInt32(availableCopy);
                    await using var markLoaned = new MySqlCommand("UPDATE Copies SET Is_Loaned = 1 WHERE Copy_ID = @copyId", connection, transaction);
                    markLoaned.Parameters.AddWithValue("@copyId", copyId);
                    await markLoaned.ExecuteNonQueryAsync();
                }
            }

            await using var createLoan = new MySqlCommand("INSERT INTO Loans (User_ID, Copy_ID, DueDate) VALUES (@userId, @copyId, UTC_TIMESTAMP() + INTERVAL 1 MINUTE)", connection, transaction);
            createLoan.Parameters.AddWithValue("@userId", userId);
            createLoan.Parameters.AddWithValue("@copyId", copyId);
            await createLoan.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            throw;
        }
    }

    private void Btn_MyLoans_Click(object sender, RoutedEventArgs e)
    {
        NavigationService?.Navigate(new MyLoansPage(_userId));
    }

    private sealed class MediaItem
    {
        public int MediaId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string MediaType { get; init; } = string.Empty;
        public string? ISBN { get; init; }
        public int? Pages { get; init; }
        public string? ISAN { get; init; }
        public int? Length { get; init; }
        public string? ReleaseYear { get; init; }
        public string? Language { get; init; }
        public string? SAB { get; init; }
        public string? Description { get; init; }
        public string Authors { get; init; } = string.Empty;
        public int AvailableCopies { get; init; }
    }
}