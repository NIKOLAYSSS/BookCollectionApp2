using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using Npgsql;
using Newtonsoft.Json;
using Aspose.Pdf;
using Aspose.Words;

namespace BookCollectionApp
{
    public partial class Form1 : Form
    {

        

        // Создание экземпляра менеджера книг для управления коллекцией книг.
        private BookManager bookManager = new BookManager();
        private string connectionString = BookManager.connectionString;
        private readonly string _userRole;
        private QRCodeGeneratorHelper _qrCodeGeneratorHelper;

        // Создание источника данных для связывания данных с элементами управления (например, DataGridView).
        private BindingSource bindingSource = new BindingSource();

        // Конструктор формы, который выполняет инициализацию компонентов и связывает DataGridView с BindingSource.
        public Form1(string userRole)
        {
            
            InitializeComponent();  // Инициализация компонентов формы (автоматически генерируемый код)
            _userRole = userRole;
            ConfigureAccessByRole();

            // Устанавливаем BindingSource в качестве источника данных для DataGridView.
            dataGridBooks.DataSource = bindingSource;

            // Автоматически генерировать столбцы в DataGridView на основе свойств объекта книги.
            dataGridBooks.AutoGenerateColumns = true;

        }
        private void ConfigureAccessByRole()
        {
            // Отключаем или скрываем элементы интерфейса в зависимости от роли
            if (_userRole == "Admin")
            {
                btnImport.Enabled = true;
                btnExport.Enabled = true;
                btnConvert.Enabled = true;
            }
            else if (_userRole == "User")
            {
                btnImport.Enabled = false;
                btnExport.Enabled = false;
                btnConvert.Enabled = false;
            }
        }

        private void AdjustWindowForQRCode(Bitmap qrCodeImage)
        {
            // Рассчитываем ширину окна
            int qrCodeWidth = qrCodeImage.Width; // Ширина QR-кода
            int padding = 20; // Отступы

            // Рассчитываем необходимую ширину формы
            int formBorderWidth = this.Width - this.ClientSize.Width; // Разница между полной шириной и клиентской областью
            int newFormWidth = 650 + qrCodeWidth + padding;

            // Если текущая ширина меньше необходимой, увеличиваем
            if (this.Width < newFormWidth | this.Width > newFormWidth)
            {
                this.Width = newFormWidth;
            }

            // Размещаем QR-код
            pictureBoxQRCode.Location = new System.Drawing.Point(this.ClientSize.Width - qrCodeImage.Width - padding, 10);
            pictureBoxQRCode.Size = qrCodeImage.Size;

            // Обновляем окно
            this.PerformLayout();
            this.Update();
        }
        private void btnGenerateQRCode_Click(object sender, EventArgs e)
        {
            if (dataGridBooks.SelectedRows.Count > 0)
            {
                // Получаем выбранную книгу
                var selectedRow = dataGridBooks.SelectedRows[0];
                Book selectedBook = new Book
                {
                    Title = selectedRow.Cells["Title"].Value.ToString(),
                    Author = selectedRow.Cells["Author"].Value.ToString(),
                    Year = Convert.ToInt32(selectedRow.Cells["Year"].Value)
                };

                try
                {
                    // Генерация URL и QR-кода
                    _qrCodeGeneratorHelper = new QRCodeGeneratorHelper();
                    string searchUrl = _qrCodeGeneratorHelper.GenerateSearchUrl(selectedBook);
                    Bitmap qrCodeImage = QRCodeGeneratorHelper.GenerateQRCode(searchUrl);

                    // Подстраиваем ширину окна
                    AdjustWindowForQRCode(qrCodeImage);
                    // Отображение QR-кода в PictureBox
                    pictureBoxQRCode.Image = qrCodeImage;


                    MessageBox.Show("QR-код успешно сгенерирован!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при генерации QR-кода: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Выберите книгу из списка.");
            }
        }
        // Загрузка списка книг из базы данных
        private void LoadBooks()
        {
            string query = "SELECT id, title, author, year FROM books";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    DataTable dt = new DataTable();
                    dt.Load(reader);
                    dataGridBooks.DataSource = dt;
                }
            }
        }

        

        
        // Обработчик события для добавления новой книги.
        private void btnAddBook_Click(object sender, EventArgs e)
        {
            // Получение данных из текстовых полей для названия, автора и года.
            string title = txtTitle.Text;
            string author = txtAuthor.Text;


            // Проверка, является ли введенное значение года допустимым числом.
            if (int.TryParse(txtYear.Text, out int year))
            {
                // Открытие диалога выбора файла
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx";
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Получаем путь к выбранному файлу
                        string filePath = openFileDialog.FileName;
                        // Чтение файла в массив байтов
                        byte[] fileBytes = File.ReadAllBytes(filePath);

                        Guid bookId = Guid.NewGuid();  // Генерация нового GUID для книги

                        string query = "INSERT INTO books (id, title, author, year, file_data) VALUES (@id, @title, @author, @year, @file_data)";
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            connection.Open();
                            using (var command = new NpgsqlCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("id", bookId);
                                command.Parameters.AddWithValue("title", title);
                                command.Parameters.AddWithValue("author", author);
                                command.Parameters.AddWithValue("year", year);
                                command.Parameters.AddWithValue("file_data", fileBytes);
                                command.ExecuteNonQuery();
                            }
                        }

                        MessageBox.Show("Книга добавлена!");
                        LoadBooks();  // Обновляем список книг
                    }
                }
            }
            else
            {
                // Если год некорректный, показать сообщение об ошибке
                MessageBox.Show("Введен некорректный год.");
            }
        }

        // Обработчик события для удаления книги.
        private void btnRemoveBook_Click(object sender, EventArgs e)
        {
            // Проверка, выбрана ли строка.
            if (dataGridBooks.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridBooks.SelectedRows[0];
                Guid bookId = (Guid)selectedRow.Cells["id"].Value;

                // Отладочное сообщение
                //MessageBox.Show($"ID выбранной книги: {bookId}");

                string query = "DELETE FROM books WHERE id = @id";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("id", bookId);
                        command.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Книга удалена!");
                LoadBooks();  // Обновляем список книг
            }
            else
            {
                // Если книга не выбрана, показать сообщение о необходимости выбора книги.
                MessageBox.Show("Выберите книгу для удаления.");
            }
        }

        // Обработчик события для поиска книги по названию.
        private void btnSearchByTitle_Click(object sender, EventArgs e)
        {
            string title = txtTitle.Text;

            string query = "SELECT id, title, author, year FROM books WHERE title ILIKE @title";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("title", "%" + title + "%");
                    using (var reader = command.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        dataGridBooks.DataSource = dt;
                    }
                }
            }
        }

        // Обработчик события для поиска книги по автору.
        private void btnSearchByAuthor_Click(object sender, EventArgs e)
        {
            string author = txtAuthor.Text;

            string query = "SELECT id, title, author, year FROM books WHERE author ILIKE @author";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("author", "%" + author + "%");
                    using (var reader = command.ExecuteReader())
                    {
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        dataGridBooks.DataSource = dt;
                    }
                }
            }
        }

        // Обработчик события для отображения всех книг.
        private void btnShowAllBooks_Click(object sender, EventArgs e)
        {
            // Отображение всех книг в DataGridView.
            LoadBooks();  // Перезагружаем список всех книг
        }




        private void btnImportBooks_Click(object sender, EventArgs e)
        {
            // Окно для выбора JSON файла
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON Files (*.json)|*.json";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Чтение содержимого файла
                string json = File.ReadAllText(openFileDialog.FileName);

                // Десериализация JSON в список книг
                List<Book> books = JsonConvert.DeserializeObject<List<Book>>(json);

                // Вставка данных в базу данных
                string query = "INSERT INTO books (id, title, author, year, file_data) VALUES (@id, @title, @author, @year, @file_data)";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var book in books)
                        {
                            // Вставка данных книги в базу
                            using (var command = new NpgsqlCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("id", book.Id);
                                command.Parameters.AddWithValue("title", book.Title);
                                command.Parameters.AddWithValue("author", book.Author);
                                command.Parameters.AddWithValue("year", book.Year);

                                // Вставка бинарных данных файла (не Base64 строка)
                                command.Parameters.AddWithValue("file_data", book.FileData);  // Прямо передаем массив байтов
                                command.ExecuteNonQuery();
                            }
                        }

                        // Подтверждаем транзакцию
                        transaction.Commit();
                    }
                }

                MessageBox.Show("Книги импортированы из JSON!");
                LoadBooks();  // Обновляем список книг
            }
        }

        // Кнопка для экспорта книг в файл (JSON) Filter = "JSON Files (*.json)|*.json"
        private void btnExportBooks_Click(object sender, EventArgs e)
        {
            // Извлекаем список всех книг из базы данных
            List<Book> books = new List<Book>();

            string query = "SELECT id, title, author, year, file_data FROM books";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Book book = new Book
                            {
                                Id = reader.GetGuid(0),
                                Title = reader.GetString(1),
                                Author = reader.GetString(2),
                                Year = reader.GetInt32(3),
                                FileData = reader["file_data"] as byte[] // Получаем файл как массив байтов
                            };
                            books.Add(book);
                        }
                    }
                }
            }

            // Окно сохранения файла
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON Files (*.json)|*.json";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Сериализация списка книг в JSON
                string json = JsonConvert.SerializeObject(books, Formatting.Indented);

                // Сохранение JSON в файл
                File.WriteAllText(saveFileDialog.FileName, json);
                MessageBox.Show("Список книг экспортирован в JSON!");
            }
        }

        private void btnConvertBook_Click(object sender, EventArgs e)
        {
            // Проверка, выбрана ли строка в DataGridView
            if (dataGridBooks.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridBooks.SelectedRows[0];
                Guid bookId = (Guid)selectedRow.Cells["Id"].Value;

                // Извлекаем книгу из базы данных
                Book selectedBook = bookManager.GetBookById(bookId);

                if (selectedBook != null)
                {
                    // Открытие диалогового окна для выбора формата файла
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.Filter = "PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string selectedFormat = Path.GetExtension(saveFileDialog.FileName).ToLower();

                        // Если формат совпадает с текущим, просто сохраняем файл
                        if ((selectedFormat == ".pdf" && !bookManager.IsPdfFile(selectedBook)) ||
                            (selectedFormat == ".docx" && !bookManager.IsDocxFile(selectedBook)))
                        {
                            // Если нужно, конвертируем файл
                            bookManager.ConvertFile(selectedBook, selectedFormat, saveFileDialog.FileName);
                        }
                        else
                        {
                            // Сохраняем файл без конвертации
                            File.WriteAllBytes(saveFileDialog.FileName, selectedBook.FileData);
                            MessageBox.Show("Файл сохранен без конвертации!");
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите книгу для конвертации!");
            }
        }


    }
}