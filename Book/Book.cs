using BenchmarkDotNet.Attributes;
using System.Collections;
using System.Text;

namespace BookCollection
{
    [MemoryDiagnoser]
    public class Test
    {
        [Benchmark]
        public async Task TestBookCollection()
        {
            try
            {
                var book1 = new Book("T1", 2023, ["text-1", "text-2", "text-3"]);
                var book2 = new Book("T2", 2024, ["text-1", "text-2", "text-3"]);
                var book3 = new Book("T2", 1924, ["text-1", "text-2", "text-3"]);
                var books = new BookCollection(book1, book2, book3);
                //тут можно включить генератор через yield
                books.GenerateElementsYield(1_000_000);
                //тут можно включить генератор
                //books.GenerateElementsNoYield(1_000_000);

                //Console.WriteLine($"Count = {books.Books.Count()}");
                //Console.WriteLine($"Avg = {books.Avg()}");
                //Console.WriteLine($"GC.GetGeneration(books) = {GC.GetGeneration(books)}");
                CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
                CancellationToken token = cancelTokenSource.Token;
                
                var oneBook = book1.SaveAsync(new FileBooksStorageAsync("bookOne.txt"));
                //books.Save(new FileBooksStorage("g:\\mybooks.txt"));
                //Console.WriteLine($"ManagedThreadId = {Thread.CurrentThread.ManagedThreadId}");
                var allBooks = books.SaveAsync(new FileBooksStorageAsync("booksAsync.txt"), token);
                //Console.WriteLine($"ManagedThreadId = {Thread.CurrentThread.ManagedThreadId}");
                using var memStorage = new MemoryBooksStorage();
                books.Save(memStorage);
                var fres = books.Find("T2");
                //cancelTokenSource.CancelAfter(200);
                await Task.WhenAll(oneBook, allBooks);
                //Console.WriteLine($"ManagedThreadId={Thread.CurrentThread.ManagedThreadId}");
                //Console.WriteLine($"ElapsedMilliseconds = {wath.ElapsedMilliseconds}");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex?.Message);
            }
        }
    }
    public record Book : IEnumerable<string>
    {
        public string Title { get; init; }
        public int Year { get; init; }
        public int CurrentPage { get; init; }
        public IEnumerable<string> Pages { get; init; }
        public Book(string title, int year, List<string> pages)
        {
            Title = title;
            Year = year;
            Pages = pages;
        }
        public byte[] ToBytes() => UTF8Encoding.UTF8.GetBytes(ToString());
        public void Save(IBooksStorage storage) => storage.Save(this);
        public Task SaveAsync(IBooksStorageAsync storage, CancellationToken token = default) => storage.SaveAsync(this, token);
        public IEnumerator<string> GetEnumerator() => Pages.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    public class BookCollection
    {
        public IEnumerable<Book> Books { get; set; }
        public BookCollection(int capcity = 0) => Books = new List<Book>(capcity);
        public BookCollection(params Book[] books) => Books = books.ToList();
        public BookCollection(IEnumerable<Book> books) => Books = books;
        public void Save(IBooksStorage storage) => storage.Save(this);
        public Task SaveAsync(IBooksStorageAsync storage,CancellationToken token = default) => storage.SaveAsync(this, token);
        public void Add(Book book) => Books = Books.Append(book);
        public void Clear() => Books.ToList().Clear();
        public List<Book> Find(string title) => Books.Where(e => e.Title == title).ToList();
        public List<Book> Find(string title, int year) => Books.Where(e => e.Title == title && e.Year == year).ToList();
        public int Avg() => (int)Books.Average(e => e.Year);
        public void GenerateElementsYield(int count) => Books = Generate(count);
        public void GenerateElementsNoYield(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Books = Books.Append(new Book($"Title-{i}", i, ["text-1", "text-2", "text-3"]));
            }
        }
        public override string ToString()
        {
            var str = new StringBuilder();
            foreach (var book in Books)
            {
                str.AppendLine(book.ToString());
            }
            return str.ToString();
        }
        private static IEnumerable<Book> Generate(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return new Book($"Title-{i}", i, ["text-1", "text-2", "text-3"]);
            }
        }
    }
    public interface IBooksStorage
    {
        public void Save(BookCollection books);
        public void Save(Book books);
    }
    public interface IBooksStorageAsync
    {
        public Task SaveAsync(BookCollection books, CancellationToken token);
        public Task SaveAsync(Book book, CancellationToken token);
    }
    public class FileBooksStorage : IBooksStorage
    {
        string FileName { get; set; }
        int BufferSize { get; set; }
        FileMode FileMode { get; set; }
        FileAccess FileAccess { get; set; }
        FileShare FileShare { get; set; }
        public FileBooksStorage(string fileName, 
            int bufferSize = 4096, 
            FileMode fileMode = FileMode.Create,
            FileAccess fileAccess = FileAccess.Write,
            FileShare fileShare = FileShare.None)
        {
            FileName = fileName;
            BufferSize = bufferSize;
            FileMode = fileMode;
            FileAccess = fileAccess;
            FileShare = fileShare;
        }

        public void Save(BookCollection books)
        {
            using var file = new FileStream(FileName,
                FileMode,
                FileAccess,
                FileShare,
                bufferSize: BufferSize,
                useAsync: false);
            foreach (var book in books.Books ?? [])
            {
                file.Write(book.ToBytes());
            }
        }
        public void Save(Book book)
        {
            using var file = new FileStream(FileName,
                FileMode,
                FileAccess,
                FileShare,
                bufferSize: BufferSize,
                useAsync: false);
            file.Write(book.ToBytes());
        }
    }
    public class FileBooksStorageAsync : IBooksStorageAsync
    {
        string FileName { get; set; }
        int BufferSize { get; set; }
        FileMode FileMode { get; set; }
        FileAccess FileAccess { get; set; }
        FileShare FileShare { get; set; }
        public FileBooksStorageAsync(string fileName,
            int bufferSize = 4096,
            FileMode fileMode = FileMode.Create,
            FileAccess fileAccess = FileAccess.Write,
            FileShare fileShare = FileShare.None)
        {
            FileName = fileName;
            BufferSize = bufferSize;
            FileMode = fileMode;
            FileAccess = fileAccess;
            FileShare = fileShare;
        }
        public async Task SaveAsync(BookCollection books, CancellationToken token = default)
        {
            using var file = new FileStream(FileName, FileMode, FileAccess, FileShare, bufferSize: BufferSize, useAsync: true);
            foreach (var book in books.Books ?? [])
            {
                await file.WriteAsync(book.ToBytes(), token);
            }
        }
        public async Task SaveAsync(Book book, CancellationToken token = default)
        {
            using var file = new FileStream(FileName, FileMode, FileAccess, FileShare, bufferSize: BufferSize, useAsync: true);
            await file.WriteAsync(book.ToBytes(), token);
        }
    }
    public class MemoryBooksStorage : IBooksStorage, IDisposable
    {
        MemoryStream MemoryStream { get; set; } = new MemoryStream();
        public void Save(BookCollection books)
        {
            foreach (var book in books.Books ?? [])
            {
                MemoryStream.Write(book.ToBytes());
            }
        }
        public void Save(Book book) => MemoryStream.Write(book.ToBytes());
        public void Dispose() => MemoryStream?.Dispose();
        public override string ToString() => Encoding.Default.GetString(MemoryStream.ToArray());
    }
}
