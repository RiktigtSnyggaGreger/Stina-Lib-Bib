using System;
using System.Collections.Generic;
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
using System.Threading.Tasks;
using MySqlConnector;

namespace Biblans_Tråkia_Surtant
{
    /// <summary>
    /// Single-file BETA: load media directly from DB in this code-behind.
    /// All data access is here per your request.
    /// </summary>
    public partial class StartPage : Page
    {
        // Keep credentials here for the BETA; move to config for production.
        private readonly string ConnectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";

        public StartPage()
        {
            InitializeComponent();
            this.Loaded += StartPage_Loaded;
        }

        private async void StartPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadMediaAsync();
        }

        private async Task LoadMediaAsync()
        {
            try
            {
                var items = await GetMediaAsync();
                MediaGrid.ItemsSource = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load media: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadMediaAsync();
        }

        // Query the Media table and include authors and copy counts.
        private async Task<List<MediaItem>> GetMediaAsync()
        {   
            var list = new List<MediaItem>();

            using (var conn = new MySqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                // MySQL Poopy
                string query = @"
SELECT m.Media_ID,
       m.Name,
       m.Value,
       m.SAB,
       GROUP_CONCAT(DISTINCT CONCAT(a.Name, ' ', a.LastName) SEPARATOR ', ') AS Authors,
       SUM(CASE WHEN c.Is_Loaned = 0 OR c.Is_Loaned IS NULL THEN 1 ELSE 0 END) AS AvailableCopies,
       COUNT(c.Copy_ID) AS TotalCopies
FROM Media m
LEFT JOIN Media_Authors ma ON m.Media_ID = ma.Media_ID
LEFT JOIN Author a ON ma.Author_ID = a.Author_ID
LEFT JOIN Copies c ON m.Media_ID = c.Media_ID
GROUP BY m.Media_ID, m.Name, m.Value, m.SAB
LIMIT 500;";

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {   
                    while (await reader.ReadAsync())
                    {
                        var mi = new MediaItem();
                        mi.MediaId = reader.IsDBNull(reader.GetOrdinal("Media_ID")) ? 0 : reader.GetInt32("Media_ID");
                        mi.Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? null : reader.GetString("Name");
                        mi.Value = reader.IsDBNull(reader.GetOrdinal("Value")) ? (int?)null : reader.GetInt32("Value");
                        mi.SAB = reader.IsDBNull(reader.GetOrdinal("SAB")) ? null : reader.GetString("SAB");
                        mi.Authors = reader.IsDBNull(reader.GetOrdinal("Authors")) ? null : reader.GetString("Authors");
                        mi.AvailableCopies = reader.IsDBNull(reader.GetOrdinal("AvailableCopies")) ? 0 : reader.GetInt32("AvailableCopies");
                        mi.TotalCopies = reader.IsDBNull(reader.GetOrdinal("TotalCopies")) ? 0 : reader.GetInt32("TotalCopies");
                        list.Add(mi);
                    }
                }
            }

            return list;
        }

        private class MediaItem
        {
            public int MediaId { get; set; }
            public string Name { get; set; }
            public int? Value { get; set; }
            public string SAB { get; set; }
            public string Authors { get; set; }
            public int AvailableCopies { get; set; }
            public int TotalCopies { get; set; }
        }
    }
}
