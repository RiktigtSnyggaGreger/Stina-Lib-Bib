using MySqlConnector;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace Biblans_Tråkia_Surtant
{
    public partial class AdminMeidaPage : Page
    {
        private string connectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";

        // Store copies table in memory for fast real-time filtering
        private DataTable copiesTable;
        private DataTable mediaTable;

        public AdminMeidaPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllMedia();
            LoadAllCopies();
        }


        private async void LoadAllMedia()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            Media.Media_ID, 
                            Media.Name AS Title, 
                            Media.Value, 
                            COALESCE(CONCAT(Author.Name, ' ', Author.LastName), 'Ingen författare') AS MediaAuthor
                        FROM Media
                        LEFT JOIN Media_Authors ON Media.Media_ID = Media_Authors.Media_ID
                        LEFT JOIN Author ON Media_Authors.Author_ID = Author.Author_ID;";
                    mediaTable = new DataTable();
                    using (var cmd = new MySqlCommand(query, connection))
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        mediaTable = new DataTable();
                        adapter.Fill(mediaTable);
                        ShowMediaGrid.ItemsSource = mediaTable.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte hämta media:\n{ex.Message}", "Fel vid laddning", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadAllCopies()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            Copies.Copy_ID,
                            Copies.Media_ID,
                            Media.Name AS Title,
                            IF(Copies.Is_Loaned, 'Ja', 'Nej') AS Is_Loaned
                        FROM Copies
                        JOIN Media ON Copies.Media_ID = Media.Media_ID
                        ORDER BY Copies.Copy_ID ASC;";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {

                        copiesTable = new DataTable();
                        adapter.Fill(copiesTable);
                        ShowCopiesGrid.ItemsSource = copiesTable.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte hämta exemplar:\n{ex.Message}", "Fel vid laddning", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void Txt_Search_Copies_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (copiesTable == null) return;

            string searchText = Txt_Search_Copies.Text.Trim().Replace("'", "''"); // Escape single quotes

            if (string.IsNullOrEmpty(searchText))
            {
                copiesTable.DefaultView.RowFilter = string.Empty; // Reset filter
            }
            else
            {
                if (int.TryParse(searchText, out int numericId))
                {
                    // Search numeric IDs or Title
                    copiesTable.DefaultView.RowFilter = $"Copy_ID = {numericId} OR Media_ID = {numericId} OR Title LIKE '%{searchText}%'";
                }
                else
                {
                    // Search Title text only
                    copiesTable.DefaultView.RowFilter = $"Title LIKE '%{searchText}%'";
                }
            }
        }

        private void Txt_Search_Media_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (mediaTable == null) return;

            string searchText = Txt_Search_Media.Text.Trim().Replace("'", "''"); // Escape single quotes

            if (string.IsNullOrEmpty(searchText))
            {
                mediaTable.DefaultView.RowFilter = string.Empty; // Clear filter
            }
            else
            {
                if (int.TryParse(searchText, out int numericId))
                {
                    // Search by Media_ID number or partial string match in Title / MediaAuthor
                    mediaTable.DefaultView.RowFilter = $"Media_ID = {numericId} OR Title LIKE '%{searchText}%' OR MediaAuthor LIKE '%{searchText}%'";
                }
                else
                {
                    // Search by Title or Author name
                    mediaTable.DefaultView.RowFilter = $"Title LIKE '%{searchText}%' OR MediaAuthor LIKE '%{searchText}%'";
                }
            }
        }

        private async void Btn_Admin_Remove_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int copyId = Convert.ToInt32(row["Copy_ID"]);

                var result = MessageBox.Show($"Är du säker på att du vill ta bort exemplar ID {copyId}?",
                                           "Bekräfta borttagning",
                                           MessageBoxButton.YesNo,
                                           MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = new MySqlConnection(connectionString))
                        {
                            await connection.OpenAsync();
                            string deleteQuery = "DELETE FROM Copies WHERE Copy_ID = @CopyID;";

                            using (var cmd = new MySqlCommand(deleteQuery, connection))
                            {
                                cmd.Parameters.AddWithValue("@CopyID", copyId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        MessageBox.Show("Exemplaret har tagits bort!", "Framgång", MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadAllCopies(); // Refresh list
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Kunde inte ta bort exemplaret (kan vara utlånat):\n{ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void Btn_Admin_Remove_Media_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int mediaId = Convert.ToInt32(row["Media_ID"]);

                var result = MessageBox.Show($"Är du säker på att du vill ta bort mediat med ID {mediaId}?\nAlla kopplade exemplar och detaljer kommer också att tas bort.",
                                           "Bekräfta borttagning",
                                           MessageBoxButton.YesNo,
                                           MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = new MySqlConnection(connectionString))
                        {
                            await connection.OpenAsync();

                            // Tar bort från Media-tabellen
                            string deleteQuery = "DELETE FROM Media WHERE Media_ID = @MediaID;";

                            using (var cmd = new MySqlCommand(deleteQuery, connection))
                            {
                                cmd.Parameters.AddWithValue("@MediaID", mediaId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        MessageBox.Show("Mediat och dess tillhörande exemplar har tagits bort!", "Framgång", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Uppdatera listorna i gränssnittet
                        LoadAllMedia();
                        LoadAllCopies();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Kunde inte ta bort mediet:\n{ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }


        private async void Btn_Add_Copies_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DataRowView row)
            {
                int mediaId = Convert.ToInt32(row["Media_ID"]);
                string mediaTitle = row["Title"].ToString();

                // Prompt for amount using VisualBasic InputBox (or a custom WPF dialog)
                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Hur många exemplar vill du lägga till för:\n'{mediaTitle}'?",
                    "Lägg till exemplar",
                    "1");

                if (int.TryParse(input, out int amountToAdd) && amountToAdd > 0)
                {
                    try
                    {
                        using (var connection = new MySqlConnection(connectionString))
                        {
                            await connection.OpenAsync();

                            // Insert x copies in a loop/single query
                            string query = "INSERT INTO Copies (Media_ID, Is_Loaned) VALUES (@MediaID, FALSE);";

                            for (int i = 0; i < amountToAdd; i++)
                            {
                                using (var cmd = new MySqlCommand(query, connection))
                                {
                                    cmd.Parameters.AddWithValue("@MediaID", mediaId);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        MessageBox.Show($"{amountToAdd} st exemplar har lagts till!", "Framgång", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Refresh the copies list underneath
                        LoadAllCopies();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Kunde inte lägga till exemplar:\n{ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }















        /////////////
        ////////
        ///             Create media och författare om författare inte finns
        ///////     
        ////////////






        private async void Btn_Create_Media_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validera indata
            string title = Create_Media_Title.Text.Trim();
            string sab = Create_Media_SAB.Text.Trim();
            string language = Create_Media_Language.Text.Trim();
            string authorName = Create_Author_Name.Text.Trim();
            string authorLastName = Create_Author_LastName.Text.Trim();

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(sab) ||
                string.IsNullOrEmpty(language) || string.IsNullOrEmpty(authorName) ||
                string.IsNullOrEmpty(authorLastName))
            {
                MessageBox.Show("Fyll i minst Titel, SAB-kod, Språk samt Författarens Förnamn och Efternamn.",
                                "Saknad information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int.TryParse(Create_Media_Value.Text.Trim(), out int value);
            string year = Create_Media_Year.Text.Trim();
            string description = Create_Media_Description.Text.Trim();

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Använd en transaktion för att säkerställa att allt sparas korrekt
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            //  Kolla om författaren redan finns, annars skapa ny
                            int authorId;
                            string checkAuthorQuery = "SELECT Author_ID FROM Author WHERE Name = @AName AND LastName = @ALastName LIMIT 1;";

                            using (var cmdCheck = new MySqlCommand(checkAuthorQuery, connection, transaction))
                            {
                                cmdCheck.Parameters.AddWithValue("@AName", authorName);
                                cmdCheck.Parameters.AddWithValue("@ALastName", authorLastName);

                                var result = await cmdCheck.ExecuteScalarAsync();

                                if (result != null && result != DBNull.Value)
                                {
                                    authorId = Convert.ToInt32(result);
                                }
                                else
                                {
                                    // Skapa ny författare
                                    string insertAuthorQuery = @"
                                INSERT INTO Author (Name, LastName) 
                                VALUES (@AName, @ALastName);
                                SELECT LAST_INSERT_ID();";

                                    using (var cmdInsertAuthor = new MySqlCommand(insertAuthorQuery, connection, transaction))
                                    {
                                        cmdInsertAuthor.Parameters.AddWithValue("@AName", authorName);
                                        cmdInsertAuthor.Parameters.AddWithValue("@ALastName", authorLastName);
                                        authorId = Convert.ToInt32(await cmdInsertAuthor.ExecuteScalarAsync());
                                    }
                                }
                            }

                            // Skapa mediet i Media-tabellen
                            string insertMediaQuery = @"
                        INSERT INTO Media (Name, Value, SAB, Release_Year, Language, Description) 
                        VALUES (@Name, @Value, @SAB, @Year, @Language, @Description);
                        SELECT LAST_INSERT_ID();";

                            int mediaId;
                            using (var cmdInsertMedia = new MySqlCommand(insertMediaQuery, connection, transaction))
                            {
                                cmdInsertMedia.Parameters.AddWithValue("@Name", title);
                                cmdInsertMedia.Parameters.AddWithValue("@Value", value > 0 ? (object)value : DBNull.Value);
                                cmdInsertMedia.Parameters.AddWithValue("@SAB", sab);
                                cmdInsertMedia.Parameters.AddWithValue("@Year", string.IsNullOrEmpty(year) ? DBNull.Value : (object)year);
                                cmdInsertMedia.Parameters.AddWithValue("@Language", language);
                                cmdInsertMedia.Parameters.AddWithValue("@Description", string.IsNullOrEmpty(description) ? DBNull.Value : (object)description);

                                mediaId = Convert.ToInt32(await cmdInsertMedia.ExecuteScalarAsync());
                            }

                            // Koppla Media och Författare i kopplings-tabellen Media_Authors
                            string insertRelationQuery = "INSERT INTO Media_Authors (Media_ID, Author_ID) VALUES (@MediaID, @AuthorID);";
                            using (var cmdRelation = new MySqlCommand(insertRelationQuery, connection, transaction))
                            {
                                cmdRelation.Parameters.AddWithValue("@MediaID", mediaId);
                                cmdRelation.Parameters.AddWithValue("@AuthorID", authorId);
                                await cmdRelation.ExecuteNonQueryAsync();
                            }

                            // Bekräfta transaktionen
                            await transaction.CommitAsync();

                            MessageBox.Show("Mediat har skapats och kopplats till författaren!", "Framgång", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Rensa formuläret
                            Create_Media_Title.Clear();
                            Create_Media_Value.Clear();
                            Create_Media_SAB.Clear();
                            Create_Media_Year.Clear();
                            Create_Media_Description.Clear();
                            Create_Author_Name.Clear();
                            Create_Author_LastName.Clear();

                            // Uppdatera media-tabellen i UI
                            LoadAllMedia();
                        }
                        catch
                        {
                            // Rulla tillbaka om något fel uppstod under processen
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunde inte skapa media:\n{ex.Message}", "Fel vid sparande", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

















        private void ShowMediaGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }



        private void Btn_Goto_Admin_User_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
            {
                this.NavigationService.Navigate(new AdminPage());
            }
        }
    }
}