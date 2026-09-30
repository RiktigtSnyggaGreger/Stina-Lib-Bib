using MySqlConnector;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace Biblans_Tråkia_Surtant
{

    public class TempAuthor
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public override string ToString()
        {
            return $"{FirstName} {LastName}";
        }
    }
    public partial class AdminMeidaPage : Page
    {
        private string connectionString = "Server=127.0.0.1;Port=3306;Database=Biblioteks_System;User ID=root;Password=hemligt-losenord;";

        private System.Collections.Generic.List<TempAuthor> selectedAuthors = new System.Collections.Generic.List<TempAuthor>();

        // Store copies table in memory for fast real-time filtering
        private DataTable copiesTable;
        private DataTable mediaTable;

        public AdminMeidaPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSubtypeFieldsVisibility();
            LoadAllMedia();
            LoadAllCopies();
        }

        private void Create_Media_Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSubtypeFieldsVisibility();
        }

        private void UpdateSubtypeFieldsVisibility()
        {
            if (BookFieldsPanel == null || MovieFieldsPanel == null || AudioBookFieldsPanel == null)
                return;

            string mediaType = (Create_Media_Type.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bok";
            BookFieldsPanel.Visibility = mediaType == "Bok" ? Visibility.Visible : Visibility.Collapsed;
            MovieFieldsPanel.Visibility = mediaType == "Film" ? Visibility.Visible : Visibility.Collapsed;
            AudioBookFieldsPanel.Visibility = mediaType == "Ljudbok" ? Visibility.Visible : Visibility.Collapsed;
        }


        private async void LoadAllMedia()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Här klistrar du in den nya SQL-frågan:
                    string query = @"
                SELECT 
                    Media.Media_ID, 
                    Media.Name AS Title, 
                    Media.Value, 
                    COALESCE(GROUP_CONCAT(CONCAT(Author.Name, ' ', Author.LastName) SEPARATOR ', '), 'Ingen författare') AS MediaAuthor
                FROM Media
                LEFT JOIN Media_Authors ON Media.Media_ID = Media_Authors.Media_ID
                LEFT JOIN Author ON Media_Authors.Author_ID = Author.Author_ID
                GROUP BY Media.Media_ID, Media.Name, Media.Value;";

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


                        // make possible to have more then 1 author per media
        private void Btn_Add_Author_To_List_Click(object sender, RoutedEventArgs e)
        {
            string fName = Create_Author_Name.Text.Trim();
            string lName = Create_Author_LastName.Text.Trim();

            if (string.IsNullOrEmpty(fName) || string.IsNullOrEmpty(lName))
            {
                MessageBox.Show("Fyll i både förnamn och efternamn på författaren.", "Incomplete input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lägg till i listan
            selectedAuthors.Add(new TempAuthor { FirstName = fName, LastName = lName });

            // Uppdatera UI-listan
            Lst_Added_Authors.ItemsSource = null;
            Lst_Added_Authors.ItemsSource = selectedAuthors;

            // Rensa textfälten för författare så man enkelt kan skriva nästa
            Create_Author_Name.Clear();
            Create_Author_LastName.Clear();
        }

        private void Btn_Clear_Authors_Click(object sender, RoutedEventArgs e)
        {
            selectedAuthors.Clear();
            Lst_Added_Authors.ItemsSource = null;
        }















        /////////////
        ////////
        ///             Create media och författare om författare inte finns
        ///////     
        ////////////






        private async void Btn_Create_Media_Click(object sender, RoutedEventArgs e)
        {
            string title = Create_Media_Title.Text.Trim();
            string sab = Create_Media_SAB.Text.Trim();
            string language = Create_Media_Language.Text.Trim();
            string selectedType = (Create_Media_Type.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bok";

            // If user typed an author without clicking "+ Lägg till", append it automatically
            if (!string.IsNullOrEmpty(Create_Author_Name.Text.Trim()) && !string.IsNullOrEmpty(Create_Author_LastName.Text.Trim()))
            {
                selectedAuthors.Add(new TempAuthor
                {
                    FirstName = Create_Author_Name.Text.Trim(),
                    LastName = Create_Author_LastName.Text.Trim()
                });
            }

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(sab) || string.IsNullOrEmpty(language))
            {
                MessageBox.Show("Fyll i minst Titel, SAB-kod och Språk.", "Saknad information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedAuthors.Count == 0)
            {
                MessageBox.Show("Lägg till minst en författare till mediet.", "Saknar författare", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // 1. Create main Media record
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

                            // 2. Insert into subtype table based on selected media type
                            if (selectedType == "Bok")
                            {
                                string isbn = Create_Book_ISBN.Text.Trim();
                                int.TryParse(Create_Book_Pages.Text.Trim(), out int pages);

                                string insertBookQuery = "INSERT INTO Book (Media_ID, ISBN, Pages) VALUES (@MediaID, @ISBN, @Pages);";
                                using (var cmdBook = new MySqlCommand(insertBookQuery, connection, transaction))
                                {
                                    cmdBook.Parameters.AddWithValue("@MediaID", mediaId);
                                    cmdBook.Parameters.AddWithValue("@ISBN", isbn);
                                    cmdBook.Parameters.AddWithValue("@Pages", pages > 0 ? (object)pages : DBNull.Value);
                                    await cmdBook.ExecuteNonQueryAsync();
                                }
                            }
                            else if (selectedType == "Film")
                            {
                                string isan = Create_Movie_ISAN.Text.Trim();
                                int.TryParse(Create_Movie_Length.Text.Trim(), out int length);

                                string insertMovieQuery = "INSERT INTO Movie (Media_ID, ISAN, Length) VALUES (@MediaID, @ISAN, @Length);";
                                using (var cmdMovie = new MySqlCommand(insertMovieQuery, connection, transaction))
                                {
                                    cmdMovie.Parameters.AddWithValue("@MediaID", mediaId);
                                    cmdMovie.Parameters.AddWithValue("@ISAN", isan);
                                    cmdMovie.Parameters.AddWithValue("@Length", length);
                                    await cmdMovie.ExecuteNonQueryAsync();
                                }
                            }
                            else if (selectedType == "Ljudbok")
                            {
                                string isbn = Create_AudioBook_ISBN.Text.Trim();
                                int.TryParse(Create_AudioBook_Length.Text.Trim(), out int length);

                                string insertAudioBookQuery = "INSERT INTO AudioBook (Media_ID, ISBN, Length) VALUES (@MediaID, @ISBN, @Length);";
                                using (var cmdAudio = new MySqlCommand(insertAudioBookQuery, connection, transaction))
                                {
                                    cmdAudio.Parameters.AddWithValue("@MediaID", mediaId);
                                    cmdAudio.Parameters.AddWithValue("@ISBN", isbn);
                                    cmdAudio.Parameters.AddWithValue("@Length", length);
                                    await cmdAudio.ExecuteNonQueryAsync();
                                }
                            }

                            // 3. Process each author in the list
                            foreach (var author in selectedAuthors)
                            {
                                int authorId;

                                string checkAuthorQuery = "SELECT Author_ID FROM Author WHERE Name = @AName AND LastName = @ALastName LIMIT 1;";
                                using (var cmdCheck = new MySqlCommand(checkAuthorQuery, connection, transaction))
                                {
                                    cmdCheck.Parameters.AddWithValue("@AName", author.FirstName);
                                    cmdCheck.Parameters.AddWithValue("@ALastName", author.LastName);

                                    var result = await cmdCheck.ExecuteScalarAsync();

                                    if (result != null && result != DBNull.Value)
                                    {
                                        authorId = Convert.ToInt32(result);
                                    }
                                    else
                                    {
                                        string insertAuthorQuery = @"
                                            INSERT INTO Author (Name, LastName) 
                                            VALUES (@AName, @ALastName);
                                            SELECT LAST_INSERT_ID();";

                                        using (var cmdInsertAuthor = new MySqlCommand(insertAuthorQuery, connection, transaction))
                                        {
                                            cmdInsertAuthor.Parameters.AddWithValue("@AName", author.FirstName);
                                            cmdInsertAuthor.Parameters.AddWithValue("@ALastName", author.LastName);
                                            authorId = Convert.ToInt32(await cmdInsertAuthor.ExecuteScalarAsync());
                                        }
                                    }
                                }

                                // 4. Link Author to Media
                                string insertRelationQuery = "INSERT INTO Media_Authors (Media_ID, Author_ID) VALUES (@MediaID, @AuthorID);";
                                using (var cmdRelation = new MySqlCommand(insertRelationQuery, connection, transaction))
                                {
                                    cmdRelation.Parameters.AddWithValue("@MediaID", mediaId);
                                    cmdRelation.Parameters.AddWithValue("@AuthorID", authorId);
                                    await cmdRelation.ExecuteNonQueryAsync();
                                }
                            }

                            // Commit transaction
                            await transaction.CommitAsync();

                            MessageBox.Show($"Mediet '{title}' ({selectedType}) har skapats och kopplats till {selectedAuthors.Count} st författare!", "Framgång", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Clear main fields
                            Create_Media_Title.Clear();
                            Create_Media_Value.Clear();
                            Create_Media_SAB.Clear();
                            Create_Media_Year.Clear();
                            Create_Media_Description.Clear();
                            Create_Author_Name.Clear();
                            Create_Author_LastName.Clear();

                            // Clear subtype fields
                            Create_Book_ISBN.Clear();
                            Create_Book_Pages.Clear();
                            Create_Movie_ISAN.Clear();
                            Create_Movie_Length.Clear();
                            Create_AudioBook_ISBN.Clear();
                            Create_AudioBook_Length.Clear();

                            selectedAuthors.Clear();
                            Lst_Added_Authors.ItemsSource = null;

                            // Refresh UI grid
                            LoadAllMedia();
                        }
                        catch
                        {
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